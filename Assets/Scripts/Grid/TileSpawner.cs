using System.Collections;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Spawns tiles, manages preload rows, and handles row spawning during grid rise.
    /// </summary>
    public class TileSpawner : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Prefabs")]
        public GameObject tilePrefab;
        public Sprite[] tileSprites;
        public GameObject tileBackground;

        [Header("Garbage Blocks")]
        public GameObject garbagePrefab;
        public Sprite garbageSprite;

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

        /// <summary>
        /// Spawn a garbage block at position. Garbage blocks span multiple tiles.
        /// </summary>
        public GameObject SpawnGarbageBlock(int x, int y, int width, int height, float currentGridOffset)
        {
            var prefab = garbagePrefab != null ? garbagePrefab : tilePrefab;
            var pos = new Vector3(x * _tileSize, y * _tileSize + currentGridOffset, 0);
            var garbage = Instantiate(prefab, pos, Quaternion.identity, transform);

            var sr = garbage.GetComponent<SpriteRenderer>();
            if (garbageSprite != null)
                sr.sprite = garbageSprite;
            sr.color = new Color(0.5f, 0.5f, 0.5f); // Gray for garbage

            // Scale to cover multiple tiles
            garbage.transform.localScale = new Vector3(width, height, 1);

            var ts = garbage.GetComponent<Tile>();
            ts.InitializeAsGarbage(x, y, width, height, _gridManager);

            // Place in grid (garbage occupies all cells it covers)
            for (var gx = x; gx < x + width && gx < _gridWidth; gx++)
            for (var gy = y; gy < y + height && gy < _gridHeight; gy++)
                _grid[gx, gy] = garbage;

            return garbage;
        }

        #endregion

        #region Tile State

        public void UpdateTileActiveState(GameObject tile, int gridY, float currentGridOffset)
        {
            if (tile == null) return;

            var worldY = gridY * _tileSize + currentGridOffset;
            const float visibilityThreshold = 0f;

            var sr = tile.GetComponent<SpriteRenderer>();
            sr.color = worldY >= visibilityThreshold ? Color.white : Color.gray;
        }

        public bool IsTileActive(GameObject tile, float currentGridOffset)
        {
            if (tile == null) return false;

            var ts = tile.GetComponent<Tile>();
            var worldY = ts.GridY * _tileSize + currentGridOffset;
            const float visibilityThreshold = 0f;

            return worldY >= visibilityThreshold;
        }

        #endregion

        #region Row Management

        public void SpawnRowAtBottom(float currentGridOffset, CursorController cursorController)
        {
            // Shift all tiles up in grid array
            for (var x = 0; x < _gridWidth; x++)
            {
                for (var y = _gridHeight - 1; y > 0; y--)
                {
                    _grid[x, y] = _grid[x, y - 1];
                    if (_grid[x, y] != null && !_gridManager.IsTileAnimating(_grid[x, y]))
                    {
                        var ts = _grid[x, y].GetComponent<Tile>();
                        ts.Initialize(x, y, ts.TileType, _gridManager);
                        UpdateTileActiveState(_grid[x, y], y, currentGridOffset);
                    }
                }

                // Move preload tile into main grid
                if (_preloadGrid[x, _preloadRows - 1] != null)
                {
                    var tile = _preloadGrid[x, _preloadRows - 1];
                    _grid[x, 0] = tile;
                    var ts = tile.GetComponent<Tile>();
                    ts.Initialize(x, 0, ts.TileType, _gridManager);
                    UpdateTileActiveState(tile, 0, currentGridOffset);
                }

                // Shift preload tiles up
                for (var py = _preloadRows - 1; py > 0; py--)
                {
                    _preloadGrid[x, py] = _preloadGrid[x, py - 1];
                    if (_preloadGrid[x, py] != null)
                    {
                        var ts = _preloadGrid[x, py].GetComponent<Tile>();
                        ts.Initialize(x, py - _preloadRows, ts.TileType, _gridManager);
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
    }
}