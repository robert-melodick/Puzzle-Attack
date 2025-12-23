using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Spawns tiles, manages preload rows, and handles row spawning during grid rise.
    /// Handles both regular tiles and garbage blocks when shifting the grid.
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
            for (var y = -_preloadRows; y < _gridHeight; y++)
            {
                var pos = new Vector3(x * _tileSize, y * _tileSize, 0.1f);
                Instantiate(tileBackground, pos, Quaternion.identity, transform);
            }
        }

        #endregion

        #region Tile Spawning

        public void SpawnTile(int x, int y, float currentGridOffset)
        {
            var pos = new Vector3(x * _tileSize, y * _tileSize + currentGridOffset, 0);
            var typeIndex = Random.Range(0, tileSprites.Length);
            var tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);

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
            var pos = new Vector3(x * _tileSize, y * _tileSize + currentGridOffset, 0);
            typeIndex = Mathf.Clamp(typeIndex, 0, tileSprites.Length - 1);
            
            var tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);

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
            var pos = new Vector3(x * _tileSize, (y - _preloadRows) * _tileSize + currentGridOffset, 0);
            var typeIndex = Random.Range(0, tileSprites.Length);
            var tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);

            var sr = tile.GetComponent<SpriteRenderer>();
            sr.sprite = tileSprites[typeIndex];
            sr.color = Color.gray; // Grayscale until active

            var ts = tile.GetComponent<Tile>();
            ts.Initialize(x, y - _preloadRows, typeIndex, _gridManager);

            _preloadGrid[x, y] = tile;
        }

        #endregion

        #region Tile State

        public void UpdateTileActiveState(GameObject tile, int gridY, float currentGridOffset)
        {
            if (tile == null) return;

            var worldY = gridY * _tileSize + currentGridOffset;
            const float visibilityThreshold = 0f;

            var sr = tile.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = worldY >= visibilityThreshold ? Color.white : Color.gray;
            }
        }

        public bool IsTileActive(GameObject tile, float currentGridOffset)
        {
            if (tile == null) return false;

            var ts = tile.GetComponent<Tile>();
            if (ts == null) return false;
            
            var worldY = ts.GridY * _tileSize + currentGridOffset;
            const float visibilityThreshold = 0f;

            return worldY >= visibilityThreshold;
        }

        #endregion

        #region Row Management

        public void SpawnRowAtBottom(float currentGridOffset, CursorController cursorController)
        {
            // Track garbage blocks we've already processed (they span multiple cells)
            var processedGarbage = new HashSet<GarbageBlock>();

            // First pass: Update garbage blocks before shifting grid
            // We need to do this separately because garbage spans multiple cells
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
                        // Don't shift garbage that's falling or converting
                        if (!garbageBlock.IsFalling && !garbageBlock.IsConverting)
                        {
                            // Shift anchor position up by 1
                            var newAnchorY = garbageBlock.AnchorPosition.y + 1;
                            garbageBlock.SetAnchorPosition(garbageBlock.AnchorPosition.x, newAnchorY);
                        }
                        processedGarbage.Add(garbageBlock);
                        continue;
                    }

                    // Check for garbage reference - just mark the owner as processed
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

                    // Update tile coordinates
                    var tile = cell.GetComponent<Tile>();
                    if (tile != null)
                    {
                        tile.Initialize(x, y, tile.TileType, _gridManager);
                        UpdateTileActiveState(cell, y, currentGridOffset);
                    }
                    
                    // Note: Garbage blocks and references don't need individual coordinate updates here
                    // because we updated the anchor position in the first pass
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
                            ts.Initialize(x, py - _preloadRows, ts.TileType, _gridManager);
                        }
                    }
                }

                // Spawn new preload tile
                SpawnPreloadTile(x, 0, currentGridOffset);
            }

            cursorController.ShiftCursorUp(_gridHeight, currentGridOffset);
        }

        public IEnumerator FillEmptySpaces(float currentGridOffset)
        {
            for (var x = 0; x < _gridWidth; x++)
            for (var y = 0; y < _gridHeight; y++)
            {
                if (_grid[x, y] == null)
                {
                    SpawnTile(x, y, currentGridOffset);
                    yield return new WaitForSeconds(0.05f);
                }
            }
        }

        #endregion

        #region Public Properties

        public int TileTypeCount => tileSprites?.Length ?? 0;

        #endregion
    }
}