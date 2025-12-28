using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Spawns tiles, manages preload rows, and handles row spawning during grid rise.
    /// Handles both regular tiles and garbage blocks when shifting the grid.
    /// All tiles are spawned relative to the GridManager's transform position.
    /// Note: Garbage spawning is handled by GarbageManager.
    /// </summary>
    public class TileSpawner : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Prefabs")]
        public GameObject tilePrefab;
        public Sprite[] tileSprites;
        public GameObject tileBackground;

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private GameObject[,] _grid;
        private GameObject[,] _preloadGrid;
        private float _tileSize;
        private int _gridWidth;
        private int _gridHeight;
        private int _preloadRows;
        private System.Random _seededRandom;
        private bool _useSeededRandom;

        #endregion

        #region Initialization

        public void Initialize(GridManager manager, float tileSize, GameObject[,] grid,
            GameObject[,] preloadGrid, int gridWidth, int gridHeight, int preloadRows)
        {
            _gridManager = manager;
            _tileSize = tileSize;
            _grid = grid;
            _preloadGrid = preloadGrid;
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _preloadRows = preloadRows;
        }

        public void CreateBackgroundTiles()
        {
            if (tileBackground == null) return;

            for (var x = 0; x < _gridWidth; x++)
            {
                for (var y = -_preloadRows; y < _gridHeight; y++)
                {
                    // Use GridManager's position helper for proper offset
                    var pos = _gridManager.GridToWorldPosition(x, y, 0f);
                    pos.z = 0.1f; // Push background behind tiles
                    
                    // Parent under GridContainer so background moves with grid
                    Instantiate(tileBackground, pos, Quaternion.identity, _gridManager.GridContainer);
                }
            }
        }

        #endregion

        #region Seeding

        /// <summary>
        /// Set a seed for deterministic tile spawning.
        /// Call this before the grid starts initializing.
        /// </summary>
        public void SetSeed(int seed)
        {
            _seededRandom = new System.Random(seed);
            _useSeededRandom = true;
            Debug.Log($"[TileSpawner] Seeded with: {seed}");
        }

        /// <summary>
        /// Clear the seed and return to Unity's random.
        /// </summary>
        public void ClearSeed()
        {
            _seededRandom = null;
            _useSeededRandom = false;
        }

        /// <summary>
        /// Get a random tile type index using the seeded random if available.
        /// </summary>
        private int GetRandomTileType()
        {
            if (_useSeededRandom && _seededRandom != null)
            {
                return _seededRandom.Next(0, tileSprites.Length);
            }
            return Random.Range(0, tileSprites.Length);
        }

        #endregion

        #region Tile Spawning

        public void SpawnTile(int x, int y, float currentGridOffset)
        {
            // Use GridManager's position helper
            var pos = _gridManager.GridToWorldPosition(x, y, currentGridOffset);
            var typeIndex = GetRandomTileType();
            
            // Parent under GridContainer
            var tile = Instantiate(tilePrefab, pos, Quaternion.identity, _gridManager.GridContainer);

            var sr = tile.GetComponent<SpriteRenderer>();
            sr.sprite = tileSprites[typeIndex];

            var ts = tile.GetComponent<Tile>();
            ts.Initialize(x, y, typeIndex, _gridManager);

            _grid[x, y] = tile;
            UpdateTileActiveState(tile, y, currentGridOffset);
        }

        /// <summary>
        /// Spawn a tile with a specific type (used for garbage conversion).
        /// </summary>
        public GameObject SpawnTileWithType(int x, int y, int typeIndex, float currentGridOffset)
        {
            // Use GridManager's position helper
            var pos = _gridManager.GridToWorldPosition(x, y, currentGridOffset);
            typeIndex = Mathf.Clamp(typeIndex, 0, tileSprites.Length - 1);

            // Parent under GridContainer
            var tile = Instantiate(tilePrefab, pos, Quaternion.identity, _gridManager.GridContainer);

            var sr = tile.GetComponent<SpriteRenderer>();
            sr.sprite = tileSprites[typeIndex];

            var ts = tile.GetComponent<Tile>();
            ts.Initialize(x, y, typeIndex, _gridManager);

            _grid[x, y] = tile;
            UpdateTileActiveState(tile, y, currentGridOffset);

            return tile;
        }

        public void SpawnPreloadTile(int x, int y, float currentGridOffset)
        {
            // Preload tiles are below the visible grid (negative y in grid space)
            // But stored in preloadGrid at positive indices
            int visualY = y - _preloadRows; // Convert to visual position below grid
            
            // Use GridManager's position helper
            var pos = _gridManager.GridToWorldPosition(x, visualY, currentGridOffset);
            var typeIndex = GetRandomTileType();
            
            // Parent under GridContainer
            var tile = Instantiate(tilePrefab, pos, Quaternion.identity, _gridManager.GridContainer);

            var sr = tile.GetComponent<SpriteRenderer>();
            sr.sprite = tileSprites[typeIndex];
            sr.color = Color.gray; // Grayscale until active

            var ts = tile.GetComponent<Tile>();
            ts.Initialize(x, visualY, typeIndex, _gridManager);

            _preloadGrid[x, y] = tile;
        }

        #endregion

        #region Tile State

        public void UpdateTileActiveState(GameObject tile, int gridY, float currentGridOffset)
        {
            if (tile == null) return;

            // Calculate world Y position using grid origin
            var worldY = _gridManager.GridToWorldPosition(0, gridY, currentGridOffset).y;
            var gridOriginY = _gridManager.GridOrigin.y;
            
            // Tile is active if it's at or above the grid origin
            var isActive = worldY >= gridOriginY;

            var sr = tile.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = isActive ? Color.white : Color.gray;
            }
        }

        public bool IsTileActive(GameObject tile, float currentGridOffset)
        {
            if (tile == null) return false;

            var ts = tile.GetComponent<Tile>();
            if (ts == null) return false;

            // Calculate world Y position
            var worldY = _gridManager.GridToWorldPosition(0, ts.GridY, currentGridOffset).y;
            var gridOriginY = _gridManager.GridOrigin.y;

            return worldY >= gridOriginY;
        }

        #endregion

        #region Row Management

        public void SpawnRowAtBottom(float currentGridOffset, CursorController cursorController)
        {
            // Track garbage blocks we've already processed (they span multiple cells)
            var processedGarbage = new HashSet<GarbageBlock>();

            // First pass: Update garbage blocks before shifting grid
            for (var x = 0; x < _gridWidth; x++)
            {
                for (var y = 0; y < _gridHeight; y++)
                {
                    var cell = _grid[x, y];
                    if (cell == null) continue;

                    // Check for garbage block anchor
                    var garbageBlock = cell.GetComponent<GarbageBlock>();
                    if (garbageBlock != null && !processedGarbage.Contains(garbageBlock))
                    {
                        if (!garbageBlock.IsFalling && !garbageBlock.IsConverting)
                        {
                            var newAnchorY = garbageBlock.AnchorPosition.y + 1;
                            garbageBlock.SetAnchorPosition(garbageBlock.AnchorPosition.x, newAnchorY);
                        }
                        processedGarbage.Add(garbageBlock);
                        continue;
                    }

                    // Check for garbage reference
                    var garbageRef = cell.GetComponent<GarbageReference>();
                    if (garbageRef != null && garbageRef.Owner != null)
                    {
                        if (!processedGarbage.Contains(garbageRef.Owner))
                        {
                            if (!garbageRef.Owner.IsFalling && !garbageRef.Owner.IsConverting)
                            {
                                var newAnchorY = garbageRef.Owner.AnchorPosition.y + 1;
                                garbageRef.Owner.SetAnchorPosition(garbageRef.Owner.AnchorPosition.x, newAnchorY);
                            }
                            processedGarbage.Add(garbageRef.Owner);
                        }
                    }
                }
            }

            // Second pass: Shift all grid entries up
            for (var x = 0; x < _gridWidth; x++)
            {
                for (var y = _gridHeight - 1; y > 0; y--)
                {
                    _grid[x, y] = _grid[x, y - 1];

                    var cell = _grid[x, y];
                    if (cell == null) continue;
                    if (_gridManager.IsTileAnimating(cell)) continue;

                    // Update tile coordinates and position
                    var tile = cell.GetComponent<Tile>();
                    if (tile != null)
                    {
                        tile.Initialize(x, y, tile.TileType, _gridManager);
                        
                        // Update world position
                        cell.transform.position = _gridManager.GridToWorldPosition(x, y, currentGridOffset);
                        
                        UpdateTileActiveState(cell, y, currentGridOffset);
                    }
                }

                // Move preload tile into main grid at row 0
                if (_preloadGrid[x, _preloadRows - 1] != null)
                {
                    var tile = _preloadGrid[x, _preloadRows - 1];
                    _grid[x, 0] = tile;
                    var ts = tile.GetComponent<Tile>();
                    if (ts != null)
                    {
                        ts.Initialize(x, 0, ts.TileType, _gridManager);
                        
                        // Update world position
                        tile.transform.position = _gridManager.GridToWorldPosition(x, 0, currentGridOffset);
                        
                        UpdateTileActiveState(tile, 0, currentGridOffset);
                    }
                }

                // Shift preload tiles up
                for (var py = _preloadRows - 1; py > 0; py--)
                {
                    _preloadGrid[x, py] = _preloadGrid[x, py - 1];
                    if (_preloadGrid[x, py] != null)
                    {
                        var ts = _preloadGrid[x, py].GetComponent<Tile>();
                        if (ts != null)
                        {
                            int visualY = py - _preloadRows;
                            ts.Initialize(x, visualY, ts.TileType, _gridManager);
                            
                            // Update world position
                            _preloadGrid[x, py].transform.position = _gridManager.GridToWorldPosition(x, visualY, currentGridOffset);
                        }
                    }
                }

                // Spawn new preload tile at bottom
                SpawnPreloadTile(x, 0, currentGridOffset);
            }

            if (cursorController != null)
            {
                cursorController.ShiftCursorUp(_gridHeight, currentGridOffset);
            }
        }

        public IEnumerator FillEmptySpaces(float currentGridOffset)
        {
            for (var x = 0; x < _gridWidth; x++)
            {
                for (var y = 0; y < _gridHeight; y++)
                {
                    if (_grid[x, y] == null)
                    {
                        SpawnTile(x, y, currentGridOffset);
                        yield return new WaitForSeconds(0.05f);
                    }
                }
            }
        }

        #endregion

        #region Public Properties

        public int TileTypeCount => tileSprites?.Length ?? 0;

        #endregion
    }
}