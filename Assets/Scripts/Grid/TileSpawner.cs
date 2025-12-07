using System.Collections;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    public class TileSpawner : MonoBehaviour
    {
        [Header("Prefabs")] public GameObject tilePrefab;
        public Sprite[] tileSprites;
        public GameObject tileBackground;
        private GameObject[,] _grid;
        private int _gridHeight;

        private GridManager _gridManager;
        private int _gridWidth;
        private GameObject[,] _preloadGrid;
        private int _preloadRows;
        private float _tileSize;

        public void Initialize(GridManager manager, float tileSize, GameObject[,] grid, GameObject[,] preloadGrid,
            int gridWidth, int gridHeight, int preloadRows)
        {
            _gridManager = manager;
            this._tileSize = tileSize;
            this._grid = grid;
            this._preloadGrid = preloadGrid;
            this._gridWidth = gridWidth;
            this._gridHeight = gridHeight;
            this._preloadRows = preloadRows;
        }

        public void CreateBackgroundTiles()
        {
            // Create background tiles
            for (var x = 0; x < _gridWidth; x++)
            for (var y = 0 - _preloadRows; y < _gridHeight; y++)
                if (tileBackground != null)
                {
                    var pos = new Vector3(x * _tileSize, y * _tileSize, 0.1f);
                    Instantiate(tileBackground, pos, Quaternion.identity, transform);
                }
        }

        public void SpawnTile(int x, int y, float currentGridOffset)
        {
            var pos = new Vector3(x * _tileSize, y * _tileSize + currentGridOffset, 0);
            var randomIndex = Random.Range(0, tileSprites.Length);
            var tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);

            var sr = tile.GetComponent<SpriteRenderer>();
            sr.sprite = tileSprites[randomIndex];

            var tileScript = tile.GetComponent<Tile>();
            tileScript.Initialize(x, y, randomIndex, _gridManager);

            _grid[x, y] = tile;

            // Check if tile should be active (66% visible = y position + offset >= -0.33 tiles)
            UpdateTileActiveState(tile, y, currentGridOffset);
        }

        public void SpawnPreloadTile(int x, int y, float currentGridOffset)
        {
            // Preload tiles spawn at negative Y positions
            var pos = new Vector3(x * _tileSize, (y - _preloadRows) * _tileSize + currentGridOffset, 0);
            var randomIndex = Random.Range(0, tileSprites.Length);
            var tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);

            var sr = tile.GetComponent<SpriteRenderer>();
            sr.sprite = tileSprites[randomIndex];
            sr.color = Color.gray; // Start as grayscale

            var tileScript = tile.GetComponent<Tile>();
            tileScript.Initialize(x, y - _preloadRows, randomIndex, _gridManager);

            _preloadGrid[x, y] = tile;
        }

        public void UpdateTileActiveState(GameObject tile, int gridY, float currentGridOffset)
        {
            if (tile == null) return;

            var worldY = gridY * _tileSize + currentGridOffset;
            var visibilityThreshold = 0f; // Only active when fully at y=0 or above

            var sr = tile.GetComponent<SpriteRenderer>();
            if (worldY >= visibilityThreshold)
                // Active - full color
                sr.color = Color.white;
            else
                // Inactive - grayscale
                sr.color = Color.gray;
        }

        public bool IsTileActive(GameObject tile, float currentGridOffset)
        {
            if (tile == null) return false;

            var tileScript = tile.GetComponent<Tile>();
            var worldY = tileScript.GridY * _tileSize + currentGridOffset;
            var visibilityThreshold = 0f; // Only active when fully at y=0 or above

            return worldY >= visibilityThreshold;
        }

        public void SpawnRowAtBottom(float currentGridOffset, CursorController cursorController)
        {
            // Shift all tiles up one row in grid array
            for (var x = 0; x < _gridWidth; x++)
            {
                for (var y = _gridHeight - 1; y > 0; y--)
                {
                    _grid[x, y] = _grid[x, y - 1];
                    if (_grid[x, y] != null)
                        // Skip updating tiles that are currently animating (swapping or dropping)
                        // They will update their own coordinates when animation completes
                        if (!_gridManager.IsTileAnimating(_grid[x, y]))
                        {
                            var tile = _grid[x, y].GetComponent<Tile>();
                            tile.Initialize(x, y, tile.TileType, _gridManager);
                            UpdateTileActiveState(_grid[x, y], y, currentGridOffset);
                        }
                }

                // Move preload tile into main grid at y=0
                if (_preloadGrid[x, _preloadRows - 1] != null)
                {
                    var tile = _preloadGrid[x, _preloadRows - 1];
                    _grid[x, 0] = tile;
                    var tileScript = tile.GetComponent<Tile>();
                    tileScript.Initialize(x, 0, tileScript.TileType, _gridManager);
                    UpdateTileActiveState(tile, 0, currentGridOffset);
                }

                // Shift preload tiles up
                for (var py = _preloadRows - 1; py > 0; py--)
                {
                    _preloadGrid[x, py] = _preloadGrid[x, py - 1];
                    if (_preloadGrid[x, py] != null)
                    {
                        var tile = _preloadGrid[x, py].GetComponent<Tile>();
                        tile.Initialize(x, py - _preloadRows, tile.TileType, _gridManager);
                    }
                }

                // Spawn new preload tile at bottom
                SpawnPreloadTile(x, 0, currentGridOffset);
            }

            // Shift cursor up in grid coordinates to maintain world position
            cursorController.ShiftCursorUp(_gridHeight, currentGridOffset);
        }

        public IEnumerator FillEmptySpaces(float currentGridOffset)
        {
            for (var x = 0; x < _gridWidth; x++)
            for (var gridY = 0; gridY < _gridHeight; gridY++)
                if (_grid[x, gridY] == null)
                {
                    SpawnTile(x, gridY, currentGridOffset);
                    yield return new WaitForSeconds(0.05f);
                }
        }
    }
}