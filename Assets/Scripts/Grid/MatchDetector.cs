using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Detects matches in the grid. Respects tile status effects that prevent matching.
    /// </summary>
    public class MatchDetector : MonoBehaviour
    {
        #region Private Fields

        private GameObject[,] _grid;
        private int _gridWidth;
        private int _gridHeight;

        #endregion

        #region Initialization

        public void Initialize(GameObject[,] grid, int gridWidth, int gridHeight)
        {
            _grid = grid;
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get all matched tiles across the entire grid.
        /// </summary>
        public List<GameObject> GetAllMatches()
        {
            var matches = new HashSet<GameObject>();

            // Horizontal matches
            for (var y = 0; y < _gridHeight; y++)
            for (var x = 0; x < _gridWidth - 2; x++)
            {
                if (!CanMatch(x, y) || !CanMatch(x + 1, y) || !CanMatch(x + 2, y))
                    continue;

                var type1 = GetTileType(x, y);
                var type2 = GetTileType(x + 1, y);
                var type3 = GetTileType(x + 2, y);

                if (type1 >= 0 && type1 == type2 && type2 == type3)
                {
                    matches.Add(_grid[x, y]);
                    matches.Add(_grid[x + 1, y]);
                    matches.Add(_grid[x + 2, y]);
                }
            }

            // Vertical matches
            for (var x = 0; x < _gridWidth; x++)
            for (var y = 0; y < _gridHeight - 2; y++)
            {
                if (!CanMatch(x, y) || !CanMatch(x, y + 1) || !CanMatch(x, y + 2))
                    continue;

                var type1 = GetTileType(x, y);
                var type2 = GetTileType(x, y + 1);
                var type3 = GetTileType(x, y + 2);

                if (type1 >= 0 && type1 == type2 && type2 == type3)
                {
                    matches.Add(_grid[x, y]);
                    matches.Add(_grid[x, y + 1]);
                    matches.Add(_grid[x, y + 2]);
                }
            }

            return new List<GameObject>(matches);
        }

        /// <summary>
        /// Get matches grouped into connected components.
        /// </summary>
        public List<List<GameObject>> GetMatchGroups()
        {
            var allMatches = new HashSet<GameObject>(GetAllMatches());
            return allMatches.Count == 0 
                ? new List<List<GameObject>>() 
                : GroupMatchedTiles(allMatches);
        }

        /// <summary>
        /// Get matches within a specific area.
        /// </summary>
        public List<GameObject> GetMatchesInArea(int minX, int maxX, int minY, int maxY)
        {
            var matches = new HashSet<GameObject>();

            minX = Mathf.Max(0, minX);
            maxX = Mathf.Min(_gridWidth - 1, maxX);
            minY = Mathf.Max(0, minY);
            maxY = Mathf.Min(_gridHeight - 1, maxY);

            // Horizontal
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX - 2; x++)
            {
                if (!CanMatch(x, y) || !CanMatch(x + 1, y) || !CanMatch(x + 2, y))
                    continue;

                var type1 = GetTileType(x, y);
                var type2 = GetTileType(x + 1, y);
                var type3 = GetTileType(x + 2, y);

                if (type1 >= 0 && type1 == type2 && type2 == type3)
                {
                    matches.Add(_grid[x, y]);
                    matches.Add(_grid[x + 1, y]);
                    matches.Add(_grid[x + 2, y]);
                }
            }

            // Vertical
            for (var x = minX; x <= maxX; x++)
            for (var y = minY; y <= maxY - 2; y++)
            {
                if (!CanMatch(x, y) || !CanMatch(x, y + 1) || !CanMatch(x, y + 2))
                    continue;

                var type1 = GetTileType(x, y);
                var type2 = GetTileType(x, y + 1);
                var type3 = GetTileType(x, y + 2);

                if (type1 >= 0 && type1 == type2 && type2 == type3)
                {
                    matches.Add(_grid[x, y]);
                    matches.Add(_grid[x, y + 1]);
                    matches.Add(_grid[x, y + 2]);
                }
            }

            return new List<GameObject>(matches);
        }

        /// <summary>
        /// Group a list of matched tiles into connected components.
        /// </summary>
        public List<List<GameObject>> GroupMatchedTiles(List<GameObject> matches)
        {
            return GroupMatchedTiles(new HashSet<GameObject>(matches));
        }

        /// <summary>
        /// Find tiles adjacent to a position that have a specific status or are garbage.
        /// Used for curing burning blocks and converting garbage.
        /// </summary>
        public List<GameObject> GetAdjacentTiles(int x, int y)
        {
            var adjacent = new List<GameObject>();
            var directions = new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), 
                                      new Vector2Int(0, -1), new Vector2Int(0, 1) };

            foreach (var dir in directions)
            {
                var nx = x + dir.x;
                var ny = y + dir.y;

                if (IsValidPosition(nx, ny) && _grid[nx, ny] != null)
                    adjacent.Add(_grid[nx, ny]);
            }

            return adjacent;
        }

        #endregion

        #region Private Helpers

        private List<List<GameObject>> GroupMatchedTiles(HashSet<GameObject> allMatches)
        {
            if (allMatches.Count == 0)
                return new List<List<GameObject>>();

            var groups = new List<List<GameObject>>();
            var processed = new HashSet<GameObject>();

            foreach (var tile in allMatches)
            {
                if (processed.Contains(tile) || tile == null)
                    continue;

                // Flood fill to find connected group
                var group = new List<GameObject>();
                var queue = new Queue<GameObject>();
                queue.Enqueue(tile);
                processed.Add(tile);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    group.Add(current);

                    var currentTile = current.GetComponent<Tile>();
                    if (currentTile == null) continue;

                    var neighbors = new[]
                    {
                        new Vector2Int(currentTile.GridX - 1, currentTile.GridY),
                        new Vector2Int(currentTile.GridX + 1, currentTile.GridY),
                        new Vector2Int(currentTile.GridX, currentTile.GridY - 1),
                        new Vector2Int(currentTile.GridX, currentTile.GridY + 1)
                    };

                    foreach (var pos in neighbors)
                    {
                        if (!IsValidPosition(pos.x, pos.y)) continue;

                        var neighbor = _grid[pos.x, pos.y];
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

        private int GetTileType(int x, int y)
        {
            if (_grid[x, y] == null) return -1;
            var tile = _grid[x, y].GetComponent<Tile>();
            return tile != null ? tile.TileType : -1;
        }

        /// <summary>
        /// Check if tile at position can participate in matches.
        /// </summary>
        private bool CanMatch(int x, int y)
        {
            if (_grid[x, y] == null) return false;

            var tile = _grid[x, y].GetComponent<Tile>();
            if (tile == null) return false;

            return tile.CanMatch();
        }

        #endregion
    }
}