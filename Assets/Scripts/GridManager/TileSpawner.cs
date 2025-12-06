using UnityEngine;
using System.Collections;

namespace PuzzleAttack.Grid
{
    public class TileSpawner : MonoBehaviour
    {
        [Header("Prefabs")] public GameObject tilePrefab;
        public Sprite[] tileSprites;
        public GameObject tileBackground;

        private GridManager gridManager;
        private float tileSize;
        private GameObject[,] grid;
        private GameObject[,] preloadGrid;
        private int gridWidth;
        private int gridHeight;
        private int preloadRows;

        public void Initialize(GridManager manager, float tileSize, GameObject[,] grid, GameObject[,] preloadGrid,
            int gridWidth, int gridHeight, int preloadRows)
        {
            this.gridManager = manager;
            this.tileSize = tileSize;
            this.grid = grid;
            this.preloadGrid = preloadGrid;
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
            this.preloadRows = preloadRows;
        }

        public void CreateBackgroundTiles()
        {
            // Create background tiles
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (tileBackground != null)
                    {
                        Vector3 pos = new Vector3(x * tileSize, y * tileSize, 0.1f);
                        Instantiate(tileBackground, pos, Quaternion.identity, transform);
                    }
                }
            }
        }

        public void SpawnTile(int x, int y, float currentGridOffset)
        {
            Vector3 pos = new Vector3(x * tileSize, y * tileSize + currentGridOffset, 0);
            int randomIndex = Random.Range(0, tileSprites.Length);
            GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);

            SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
            sr.sprite = tileSprites[randomIndex];

            Tile tileScript = tile.GetComponent<Tile>();
            tileScript.Initialize(x, y, randomIndex, gridManager);

            grid[x, y] = tile;

            // Check if tile should be active (66% visible = y position + offset >= -0.33 tiles)
            UpdateTileActiveState(tile, y, currentGridOffset);
        }

        public void SpawnPreloadTile(int x, int y, float currentGridOffset)
        {
            // Preload tiles spawn at negative Y positions
            Vector3 pos = new Vector3(x * tileSize, (y - preloadRows) * tileSize + currentGridOffset, 0);
            int randomIndex = Random.Range(0, tileSprites.Length);
            GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);

            SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
            sr.sprite = tileSprites[randomIndex];
            sr.color = Color.gray; // Start as grayscale

            Tile tileScript = tile.GetComponent<Tile>();
            tileScript.Initialize(x, y - preloadRows, randomIndex, gridManager);

            preloadGrid[x, y] = tile;
        }

        public void UpdateTileActiveState(GameObject tile, int gridY, float currentGridOffset)
        {
            if (tile == null) return;

            float worldY = gridY * tileSize + currentGridOffset;
            float visibilityThreshold = -0.33f * tileSize; // 66% visible = 33% below y=0

            SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
            if (worldY >= visibilityThreshold)
            {
                // Active - full color
                sr.color = Color.white;
            }
            else
            {
                // Inactive - grayscale
                sr.color = Color.gray;
            }
        }

        public bool IsTileActive(GameObject tile, float currentGridOffset)
        {
            if (tile == null) return false;

            Tile tileScript = tile.GetComponent<Tile>();
            float worldY = tileScript.GridY * tileSize + currentGridOffset;
            float visibilityThreshold = -0.33f * tileSize; // 66% visible

            return worldY >= visibilityThreshold;
        }

        public void SpawnRowAtBottom(float currentGridOffset, CursorController cursorController)
        {
            // Shift all tiles up one row in grid array
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = gridHeight - 1; y > 0; y--)
                {
                    grid[x, y] = grid[x, y - 1];
                    if (grid[x, y] != null)
                    {
                        // Skip updating tiles that are currently animating (swapping or dropping)
                        // They will update their own coordinates when animation completes
                        if (!gridManager.IsTileAnimating(grid[x, y]))
                        {
                            Tile tile = grid[x, y].GetComponent<Tile>();
                            tile.Initialize(x, y, tile.TileType, gridManager);
                            UpdateTileActiveState(grid[x, y], y, currentGridOffset);
                        }
                    }
                }

                // Move preload tile into main grid at y=0
                if (preloadGrid[x, preloadRows - 1] != null)
                {
                    GameObject tile = preloadGrid[x, preloadRows - 1];
                    grid[x, 0] = tile;
                    Tile tileScript = tile.GetComponent<Tile>();
                    tileScript.Initialize(x, 0, tileScript.TileType, gridManager);
                    UpdateTileActiveState(tile, 0, currentGridOffset);
                }

                // Shift preload tiles up
                for (int py = preloadRows - 1; py > 0; py--)
                {
                    preloadGrid[x, py] = preloadGrid[x, py - 1];
                    if (preloadGrid[x, py] != null)
                    {
                        Tile tile = preloadGrid[x, py].GetComponent<Tile>();
                        tile.Initialize(x, py - preloadRows, tile.TileType, gridManager);
                    }
                }

                // Spawn new preload tile at bottom
                SpawnPreloadTile(x, 0, currentGridOffset);
            }

            // Shift cursor up in grid coordinates to maintain world position
            cursorController.ShiftCursorUp(gridHeight, currentGridOffset);
        }

        public IEnumerator FillEmptySpaces(float currentGridOffset)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int gridY = 0; gridY < gridHeight; gridY++)
                {
                    if (grid[x, gridY] == null)
                    {
                        SpawnTile(x, gridY, currentGridOffset);
                        yield return new WaitForSeconds(0.05f);
                    }
                }
            }
        }
    }
}