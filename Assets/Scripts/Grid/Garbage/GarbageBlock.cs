using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Core garbage block entity. Represents a single garbage unit that can span multiple grid cells.
    /// The anchor position is the bottom-left corner of the garbage block.
    /// </summary>
    public class GarbageBlock : MonoBehaviour
    {
        #region Enums

        public enum ConversionOrder
        {
            LeftToRight,
            RightToLeft,
            CenterOut,
            Random
        }

        #endregion

        #region Inspector Fields

        [Header("Audio")]
        public AudioClip convertSound;
        public AudioClip landSound;

        #endregion

        #region Properties

        // Dimensions and position
        public Vector2Int AnchorPosition { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int CurrentHeight { get; private set; }

        // State
        public bool IsFalling { get; private set; }
        public bool IsConverting { get; private set; }
        public bool IsSettled => !IsFalling && !IsConverting;

        // Conversion progress
        public int RowsConverted { get; private set; }
        public int CurrentConversionRow { get; private set; }

        // Cluster management
        public GarbageCluster Cluster { get; set; }

        // Animation
        public Vector2Int TargetPosition { get; private set; }
        public int AnimationVersion { get; set; }

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private AudioSource _audioSource;
        private GarbageRenderer _renderer;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            
            _renderer = GetComponent<GarbageRenderer>();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize a new garbage block at the specified position with given dimensions.
        /// </summary>
        public void Initialize(int anchorX, int anchorY, int width, int height, GridManager manager)
        {
            AnchorPosition = new Vector2Int(anchorX, anchorY);
            TargetPosition = AnchorPosition;
            Width = width;
            Height = height;
            CurrentHeight = height;
            RowsConverted = 0;
            CurrentConversionRow = 0;
            IsFalling = false;
            IsConverting = false;
            _gridManager = manager;

            if (_renderer != null)
            {
                _renderer.RebuildVisual(width, height);
            }
        }

        /// <summary>
        /// Update the anchor position (used during falling/movement).
        /// </summary>
        public void SetAnchorPosition(int x, int y)
        {
            AnchorPosition = new Vector2Int(x, y);
        }

        #endregion

        #region Movement

        /// <summary>
        /// Start falling toward a target position.
        /// </summary>
        public void StartFalling(Vector2Int target)
        {
            IsFalling = true;
            TargetPosition = target;
            AnimationVersion++;
        }

        /// <summary>
        /// Stop falling and finalize position.
        /// </summary>
        public void StopFalling()
        {
            IsFalling = false;
            AnchorPosition = TargetPosition;
        }

        /// <summary>
        /// Retarget fall to a new position (when obstruction is found mid-fall).
        /// </summary>
        public void RetargetFall(Vector2Int newTarget)
        {
            TargetPosition = newTarget;
        }

        /// <summary>
        /// Check if any cell in the bottom row of this garbage block has support.
        /// </summary>
        public bool HasBottomSupport(GameObject[,] grid, int gridHeight)
        {
            var bottomY = AnchorPosition.y;
            
            // If at grid bottom, we have support
            if (bottomY <= 0) return true;

            var checkY = bottomY - 1;
            if (checkY < 0) return true;

            // Check each cell in the bottom row
            for (var x = AnchorPosition.x; x < AnchorPosition.x + Width; x++)
            {
                if (x < 0 || x >= grid.GetLength(0)) continue;
                
                var below = grid[x, checkY];
                if (below != null)
                {
                    // Check if it's another garbage block that's also falling
                    var garbageRef = below.GetComponent<GarbageReference>();
                    if (garbageRef != null && garbageRef.Owner != null && garbageRef.Owner.IsFalling)
                    {
                        continue; // Don't count falling garbage as support
                    }
                    
                    // Check if it's a tile that's falling
                    var tile = below.GetComponent<Tile>();
                    if (tile != null && tile.IsFalling)
                    {
                        continue; // Don't count falling tiles as support
                    }
                    
                    return true; // Found solid support
                }
            }

            return false;
        }

        /// <summary>
        /// Get all grid cells this garbage block occupies.
        /// </summary>
        public List<Vector2Int> GetOccupiedCells()
        {
            var cells = new List<Vector2Int>();
            for (var x = AnchorPosition.x; x < AnchorPosition.x + Width; x++)
            {
                for (var y = AnchorPosition.y; y < AnchorPosition.y + CurrentHeight; y++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
            return cells;
        }

        /// <summary>
        /// Get the bottom row cells of this garbage block.
        /// </summary>
        public List<Vector2Int> GetBottomRowCells()
        {
            var cells = new List<Vector2Int>();
            for (var x = AnchorPosition.x; x < AnchorPosition.x + Width; x++)
            {
                cells.Add(new Vector2Int(x, AnchorPosition.y));
            }
            return cells;
        }

        #endregion

        #region Conversion

        /// <summary>
        /// Begin the conversion process for this garbage block.
        /// </summary>
        public void BeginConversion()
        {
            if (IsConverting) return;
            
            IsConverting = true;
            CurrentConversionRow = 0;
            Debug.Log($"[GarbageBlock] Beginning conversion for {Width}x{CurrentHeight} block at ({AnchorPosition.x},{AnchorPosition.y})");
        }

        /// <summary>
        /// Called when a row has been fully converted to tiles.
        /// </summary>
        public void OnRowConverted()
        {
            RowsConverted++;
            CurrentConversionRow++;
            CurrentHeight--;

            // Move anchor up since bottom row is gone
            AnchorPosition = new Vector2Int(AnchorPosition.x, AnchorPosition.y + 1);

            // Update visual
            if (_renderer != null)
            {
                _renderer.ShrinkFromBottom(CurrentHeight);
            }

            Debug.Log($"[GarbageBlock] Row converted. Remaining height: {CurrentHeight}, New anchor: ({AnchorPosition.x},{AnchorPosition.y})");
        }

        /// <summary>
        /// Called when conversion is complete.
        /// </summary>
        public void EndConversion()
        {
            IsConverting = false;
            Debug.Log($"[GarbageBlock] Conversion ended. Remaining height: {CurrentHeight}");
        }

        /// <summary>
        /// Check if this garbage block is fully converted.
        /// </summary>
        public bool IsFullyConverted()
        {
            return CurrentHeight <= 0;
        }

        /// <summary>
        /// Get the cells that will be converted in the current/next row.
        /// </summary>
        public List<Vector2Int> GetNextConversionRowCells(ConversionOrder order = ConversionOrder.LeftToRight)
        {
            var cells = new List<Vector2Int>();
            var rowY = AnchorPosition.y; // Always convert bottom row

            switch (order)
            {
                case ConversionOrder.LeftToRight:
                    for (var x = AnchorPosition.x; x < AnchorPosition.x + Width; x++)
                        cells.Add(new Vector2Int(x, rowY));
                    break;

                case ConversionOrder.RightToLeft:
                    for (var x = AnchorPosition.x + Width - 1; x >= AnchorPosition.x; x--)
                        cells.Add(new Vector2Int(x, rowY));
                    break;

                case ConversionOrder.CenterOut:
                    var center = AnchorPosition.x + Width / 2;
                    var added = new HashSet<int>();
                    for (var offset = 0; offset <= Width; offset++)
                    {
                        var left = center - offset;
                        var right = center + offset;
                        if (left >= AnchorPosition.x && left < AnchorPosition.x + Width && !added.Contains(left))
                        {
                            cells.Add(new Vector2Int(left, rowY));
                            added.Add(left);
                        }
                        if (right >= AnchorPosition.x && right < AnchorPosition.x + Width && !added.Contains(right))
                        {
                            cells.Add(new Vector2Int(right, rowY));
                            added.Add(right);
                        }
                    }
                    break;

                case ConversionOrder.Random:
                    var tempCells = new List<Vector2Int>();
                    for (var x = AnchorPosition.x; x < AnchorPosition.x + Width; x++)
                        tempCells.Add(new Vector2Int(x, rowY));
                    
                    // Fisher-Yates shuffle
                    for (var i = tempCells.Count - 1; i > 0; i--)
                    {
                        var j = Random.Range(0, i + 1);
                        (tempCells[i], tempCells[j]) = (tempCells[j], tempCells[i]);
                    }
                    cells = tempCells;
                    break;
            }

            return cells;
        }

        #endregion

        #region Audio

        public void PlayConvertSound()
        {
            if (convertSound != null && _audioSource != null)
                _audioSource.PlayOneShot(convertSound);
        }

        public void PlayLandSound()
        {
            if (landSound != null && _audioSource != null)
                _audioSource.PlayOneShot(landSound);
        }

        #endregion

        #region Adjacency

        /// <summary>
        /// Check if this garbage block is adjacent to a position (shares an edge, not corner).
        /// </summary>
        public bool IsAdjacentTo(int x, int y)
        {
            // Check all cells of this garbage block
            for (var gx = AnchorPosition.x; gx < AnchorPosition.x + Width; gx++)
            {
                for (var gy = AnchorPosition.y; gy < AnchorPosition.y + CurrentHeight; gy++)
                {
                    // Check cardinal directions only (not diagonals)
                    if ((gx == x && Mathf.Abs(gy - y) == 1) ||  // Same column, adjacent row
                        (gy == y && Mathf.Abs(gx - x) == 1))   // Same row, adjacent column
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Check if this garbage block is adjacent to another garbage block (shares an edge).
        /// </summary>
        public bool IsAdjacentTo(GarbageBlock other)
        {
            if (other == null || other == this) return false;

            // Check each cell of this block against each cell of the other
            for (var gx = AnchorPosition.x; gx < AnchorPosition.x + Width; gx++)
            {
                for (var gy = AnchorPosition.y; gy < AnchorPosition.y + CurrentHeight; gy++)
                {
                    for (var ox = other.AnchorPosition.x; ox < other.AnchorPosition.x + other.Width; ox++)
                    {
                        for (var oy = other.AnchorPosition.y; oy < other.AnchorPosition.y + other.CurrentHeight; oy++)
                        {
                            // Check cardinal adjacency only
                            if ((gx == ox && Mathf.Abs(gy - oy) == 1) ||
                                (gy == oy && Mathf.Abs(gx - ox) == 1))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        #endregion
    }

    /// <summary>
    /// Represents a cluster of connected garbage blocks.
    /// </summary>
    public class GarbageCluster
    {
        public List<GarbageBlock> Blocks { get; } = new();
        public bool IsConverting { get; set; }
        public int CurrentConversionIndex { get; set; }

        public void AddBlock(GarbageBlock block)
        {
            if (!Blocks.Contains(block))
            {
                Blocks.Add(block);
                block.Cluster = this;
            }
        }

        public void RemoveBlock(GarbageBlock block)
        {
            Blocks.Remove(block);
            if (block.Cluster == this)
                block.Cluster = null;
        }

        /// <summary>
        /// Get blocks sorted from bottom to top (by anchor Y position).
        /// </summary>
        public List<GarbageBlock> GetBlocksBottomUp()
        {
            var sorted = new List<GarbageBlock>(Blocks);
            sorted.Sort((a, b) => a.AnchorPosition.y.CompareTo(b.AnchorPosition.y));
            return sorted;
        }
    }
}