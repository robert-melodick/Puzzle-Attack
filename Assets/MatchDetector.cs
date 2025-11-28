using UnityEngine;
using System.Collections.Generic;

public class MatchDetector : MonoBehaviour
{
    private GameObject[,] grid;
    private int gridWidth;
    private int gridHeight;

    public void Initialize(GameObject[,] grid, int gridWidth, int gridHeight)
    {
        this.grid = grid;
        this.gridWidth = gridWidth;
        this.gridHeight = gridHeight;
    }

    public List<GameObject> GetAllMatches()
    {
        HashSet<GameObject> matches = new HashSet<GameObject>();

        // Check horizontal matches
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth - 2; x++)
            {
                if (grid[x, y] != null && grid[x + 1, y] != null && grid[x + 2, y] != null)
                {
                    int type1 = grid[x, y].GetComponent<Tile>().TileType;
                    int type2 = grid[x + 1, y].GetComponent<Tile>().TileType;
                    int type3 = grid[x + 2, y].GetComponent<Tile>().TileType;

                    if (type1 == type2 && type2 == type3)
                    {
                        matches.Add(grid[x, y]);
                        matches.Add(grid[x + 1, y]);
                        matches.Add(grid[x + 2, y]);
                    }
                }
            }
        }

        // Check vertical matches
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight - 2; y++)
            {
                if (grid[x, y] != null && grid[x, y + 1] != null && grid[x, y + 2] != null)
                {
                    int type1 = grid[x, y].GetComponent<Tile>().TileType;
                    int type2 = grid[x, y + 1].GetComponent<Tile>().TileType;
                    int type3 = grid[x, y + 2].GetComponent<Tile>().TileType;

                    if (type1 == type2 && type2 == type3)
                    {
                        matches.Add(grid[x, y]);
                        matches.Add(grid[x, y + 1]);
                        matches.Add(grid[x, y + 2]);
                    }
                }
            }
        }

        return new List<GameObject>(matches);
    }

    public List<List<GameObject>> GetMatchGroups()
    {
        // First get all matched tiles
        HashSet<GameObject> allMatches = new HashSet<GameObject>(GetAllMatches());

        if (allMatches.Count == 0)
            return new List<List<GameObject>>();

        // Group matched tiles into separate connected groups
        return GroupMatchedTiles(allMatches);
    }

    public List<GameObject> GetMatchesInArea(int minX, int maxX, int minY, int maxY)
    {
        HashSet<GameObject> matches = new HashSet<GameObject>();

        minX = Mathf.Max(0, minX);
        maxX = Mathf.Min(gridWidth - 1, maxX);
        minY = Mathf.Max(0, minY);
        maxY = Mathf.Min(gridHeight - 1, maxY);

        // Check horizontal matches
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX - 2; x++)
            {
                if (grid[x, y] != null && grid[x + 1, y] != null && grid[x + 2, y] != null)
                {
                    int type1 = grid[x, y].GetComponent<Tile>().TileType;
                    int type2 = grid[x + 1, y].GetComponent<Tile>().TileType;
                    int type3 = grid[x + 2, y].GetComponent<Tile>().TileType;

                    if (type1 == type2 && type2 == type3)
                    {
                        matches.Add(grid[x, y]);
                        matches.Add(grid[x + 1, y]);
                        matches.Add(grid[x + 2, y]);
                    }
                }
            }
        }

        // Check vertical matches
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY - 2; y++)
            {
                if (grid[x, y] != null && grid[x, y + 1] != null && grid[x, y + 2] != null)
                {
                    int type1 = grid[x, y].GetComponent<Tile>().TileType;
                    int type2 = grid[x, y + 1].GetComponent<Tile>().TileType;
                    int type3 = grid[x, y + 2].GetComponent<Tile>().TileType;

                    if (type1 == type2 && type2 == type3)
                    {
                        matches.Add(grid[x, y]);
                        matches.Add(grid[x, y + 1]);
                        matches.Add(grid[x, y + 2]);
                    }
                }
            }
        }

        return new List<GameObject>(matches);
    }

    public List<List<GameObject>> GroupMatchedTiles(List<GameObject> matches)
    {
        HashSet<GameObject> allMatches = new HashSet<GameObject>(matches);
        return GroupMatchedTiles(allMatches);
    }

    private List<List<GameObject>> GroupMatchedTiles(HashSet<GameObject> allMatches)
    {
        if (allMatches.Count == 0)
            return new List<List<GameObject>>();

        // Group matched tiles into separate connected groups
        List<List<GameObject>> groups = new List<List<GameObject>>();
        HashSet<GameObject> processed = new HashSet<GameObject>();

        foreach (GameObject tile in allMatches)
        {
            if (processed.Contains(tile) || tile == null)
                continue;

            // Start a new group with flood fill
            List<GameObject> group = new List<GameObject>();
            Queue<GameObject> queue = new Queue<GameObject>();
            queue.Enqueue(tile);
            processed.Add(tile);

            while (queue.Count > 0)
            {
                GameObject current = queue.Dequeue();
                group.Add(current);

                Tile currentTile = current.GetComponent<Tile>();

                // Check adjacent tiles (up, down, left, right)
                Vector2Int[] neighbors = new Vector2Int[]
                {
                    new Vector2Int(currentTile.GridX - 1, currentTile.GridY),
                    new Vector2Int(currentTile.GridX + 1, currentTile.GridY),
                    new Vector2Int(currentTile.GridX, currentTile.GridY - 1),
                    new Vector2Int(currentTile.GridX, currentTile.GridY + 1)
                };

                foreach (Vector2Int neighborPos in neighbors)
                {
                    if (IsValidPosition(neighborPos.x, neighborPos.y))
                    {
                        GameObject neighbor = grid[neighborPos.x, neighborPos.y];
                        if (neighbor != null && allMatches.Contains(neighbor) && !processed.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                            processed.Add(neighbor);
                        }
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    private bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }
}
