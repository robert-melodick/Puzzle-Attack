using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    public class MatchDetector : MonoBehaviour
    {
        private GameObject[,] _grid;
        private int _gridHeight;
        private int _gridWidth;

        public void Initialize(GameObject[,] grid, int gridWidth, int gridHeight)
        {
            this._grid = grid;
            this._gridWidth = gridWidth;
            this._gridHeight = gridHeight;
        }

        public List<GameObject> GetAllMatches()
        {
            var matches = new HashSet<GameObject>();

            // Check horizontal matches
            for (var y = 0; y < _gridHeight; y++)
            for (var x = 0; x < _gridWidth - 2; x++)
                if (_grid[x, y] != null && _grid[x + 1, y] != null && _grid[x + 2, y] != null)
                {
                    var type1 = _grid[x, y].GetComponent<Tile>().TileType;
                    var type2 = _grid[x + 1, y].GetComponent<Tile>().TileType;
                    var type3 = _grid[x + 2, y].GetComponent<Tile>().TileType;

                    if (type1 == type2 && type2 == type3)
                    {
                        matches.Add(_grid[x, y]);
                        matches.Add(_grid[x + 1, y]);
                        matches.Add(_grid[x + 2, y]);
                    }
                }

            // Check vertical matches
            for (var x = 0; x < _gridWidth; x++)
            for (var y = 0; y < _gridHeight - 2; y++)
                if (_grid[x, y] != null && _grid[x, y + 1] != null && _grid[x, y + 2] != null)
                {
                    var type1 = _grid[x, y].GetComponent<Tile>().TileType;
                    var type2 = _grid[x, y + 1].GetComponent<Tile>().TileType;
                    var type3 = _grid[x, y + 2].GetComponent<Tile>().TileType;

                    if (type1 == type2 && type2 == type3)
                    {
                        matches.Add(_grid[x, y]);
                        matches.Add(_grid[x, y + 1]);
                        matches.Add(_grid[x, y + 2]);
                    }
                }

            return new List<GameObject>(matches);
        }

        public List<List<GameObject>> GetMatchGroups()
        {
            // First get all matched tiles
            var allMatches = new HashSet<GameObject>(GetAllMatches());

            if (allMatches.Count == 0)
                return new List<List<GameObject>>();

            // Group matched tiles into separate connected groups
            return GroupMatchedTiles(allMatches);
        }

        public List<GameObject> GetMatchesInArea(int minX, int maxX, int minY, int maxY)
        {
            var matches = new HashSet<GameObject>();

            minX = Mathf.Max(0, minX);
            maxX = Mathf.Min(_gridWidth - 1, maxX);
            minY = Mathf.Max(0, minY);
            maxY = Mathf.Min(_gridHeight - 1, maxY);

            // Check horizontal matches
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX - 2; x++)
                if (_grid[x, y] != null && _grid[x + 1, y] != null && _grid[x + 2, y] != null)
                {
                    var type1 = _grid[x, y].GetComponent<Tile>().TileType;
                    var type2 = _grid[x + 1, y].GetComponent<Tile>().TileType;
                    var type3 = _grid[x + 2, y].GetComponent<Tile>().TileType;

                    if (type1 == type2 && type2 == type3)
                    {
                        matches.Add(_grid[x, y]);
                        matches.Add(_grid[x + 1, y]);
                        matches.Add(_grid[x + 2, y]);
                    }
                }

            // Check vertical matches
            for (var x = minX; x <= maxX; x++)
            for (var y = minY; y <= maxY - 2; y++)
                if (_grid[x, y] != null && _grid[x, y + 1] != null && _grid[x, y + 2] != null)
                {
                    var type1 = _grid[x, y].GetComponent<Tile>().TileType;
                    var type2 = _grid[x, y + 1].GetComponent<Tile>().TileType;
                    var type3 = _grid[x, y + 2].GetComponent<Tile>().TileType;

                    if (type1 == type2 && type2 == type3)
                    {
                        matches.Add(_grid[x, y]);
                        matches.Add(_grid[x, y + 1]);
                        matches.Add(_grid[x, y + 2]);
                    }
                }

            return new List<GameObject>(matches);
        }

        public List<List<GameObject>> GroupMatchedTiles(List<GameObject> matches)
        {
            var allMatches = new HashSet<GameObject>(matches);
            return GroupMatchedTiles(allMatches);
        }

        private List<List<GameObject>> GroupMatchedTiles(HashSet<GameObject> allMatches)
        {
            if (allMatches.Count == 0)
                return new List<List<GameObject>>();

            // Group matched tiles into separate connected groups
            var groups = new List<List<GameObject>>();
            var processed = new HashSet<GameObject>();

            foreach (var tile in allMatches)
            {
                if (processed.Contains(tile) || tile == null)
                    continue;

                // Start a new group with flood fill
                var group = new List<GameObject>();
                var queue = new Queue<GameObject>();
                queue.Enqueue(tile);
                processed.Add(tile);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    group.Add(current);

                    var currentTile = current.GetComponent<Tile>();

                    // Check adjacent tiles (up, down, left, right)
                    var neighbors = new[]
                    {
                        new Vector2Int(currentTile.GridX - 1, currentTile.GridY),
                        new Vector2Int(currentTile.GridX + 1, currentTile.GridY),
                        new Vector2Int(currentTile.GridX, currentTile.GridY - 1),
                        new Vector2Int(currentTile.GridX, currentTile.GridY + 1)
                    };

                    foreach (var neighborPos in neighbors)
                        if (IsValidPosition(neighborPos.x, neighborPos.y))
                        {
                            var neighbor = _grid[neighborPos.x, neighborPos.y];
                            if (neighbor != null && allMatches.Contains(neighbor) && !processed.Contains(neighbor))
                            {
                                queue.Enqueue(neighbor);
                                processed.Add(neighbor);
                            }
                        }
                }

                groups.Add(group);
            }

            return groups;
        }

        private bool IsValidPosition(int x, int y)
        {
            return x >= 0 && x < _gridWidth && y >= 0 && y < _gridHeight;
        }
    }
}