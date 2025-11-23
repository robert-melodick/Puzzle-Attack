using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridWidth = 8;
    public int gridHeight = 8;
    public float tileSize = 1f;
    
    [Header("Prefabs")]
    public GameObject tilePrefab; // The tile prefab to instantiate
    public Sprite[] tileSprites; // Array of sprites for tiles
    public GameObject tileBackground;
    
    private GameObject[,] grid;
    private GameObject selectedTile;
    private Vector2Int selectedPos;
    private bool isProcessing = false;
    
    void Start()
    {
        grid = new GameObject[gridWidth, gridHeight];
        CreateGrid();
        StartCoroutine(FillGrid());
    }
    
    void CreateGrid()
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
    
    IEnumerator FillGrid()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == null)
                {
                    SpawnTile(x, y);
                    yield return new WaitForSeconds(0.05f);
                }
            }
        }
        
        // Check for initial matches and clear them
        yield return StartCoroutine(CheckAndClearMatches());
    }
    
    void SpawnTile(int x, int y)
    {
        Vector3 pos = new Vector3(x * tileSize, y * tileSize, 0);
        int randomIndex = Random.Range(0, tileSprites.Length);
        
        GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
        
        // Set the sprite
        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        sr.sprite = tileSprites[randomIndex];
        
        Tile tileScript = tile.GetComponent<Tile>();
        tileScript.Initialize(x, y, randomIndex, this);
        
        grid[x, y] = tile;
    }
    
    void Update()
    {
        if (isProcessing) return;
        
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            int x = Mathf.RoundToInt(mousePos.x / tileSize);
            int y = Mathf.RoundToInt(mousePos.y / tileSize);
            
            if (IsValidPosition(x, y))
            {
                if (selectedTile == null)
                {
                    SelectTile(x, y);
                }
                else
                {
                    if (IsAdjacent(selectedPos, new Vector2Int(x, y)))
                    {
                        StartCoroutine(SwapTiles(selectedPos, new Vector2Int(x, y)));
                    }
                    DeselectTile();
                }
            }
        }
    }
    
    void SelectTile(int x, int y)
    {
        selectedTile = grid[x, y];
        selectedPos = new Vector2Int(x, y);
        
        // Visual feedback
        selectedTile.transform.localScale = Vector3.one * 1.1f;
    }
    
    void DeselectTile()
    {
        if (selectedTile != null)
        {
            selectedTile.transform.localScale = Vector3.one;
        }
        selectedTile = null;
    }
    
    IEnumerator SwapTiles(Vector2Int pos1, Vector2Int pos2)
    {
        isProcessing = true;
        
        GameObject tile1 = grid[pos1.x, pos1.y];
        GameObject tile2 = grid[pos2.x, pos2.y];
        
        // Swap in grid
        grid[pos1.x, pos1.y] = tile2;
        grid[pos2.x, pos2.y] = tile1;
        
        // Update tile positions
        tile1.GetComponent<Tile>().gridX = pos2.x;
        tile1.GetComponent<Tile>().gridY = pos2.y;
        tile2.GetComponent<Tile>().gridX = pos1.x;
        tile2.GetComponent<Tile>().gridY = pos1.y;
        
        // Animate swap
        yield return StartCoroutine(MoveTile(tile1, pos2));
        yield return StartCoroutine(MoveTile(tile2, pos1));
        
        // Check for matches
        List<GameObject> matches = GetAllMatches();
        
        if (matches.Count > 0)
        {
            yield return StartCoroutine(CheckAndClearMatches());
        }
        else
        {
            // Swap back if no matches
            grid[pos1.x, pos1.y] = tile1;
            grid[pos2.x, pos2.y] = tile2;
            tile1.GetComponent<Tile>().gridX = pos1.x;
            tile1.GetComponent<Tile>().gridY = pos1.y;
            tile2.GetComponent<Tile>().gridX = pos2.x;
            tile2.GetComponent<Tile>().gridY = pos2.y;
            
            yield return StartCoroutine(MoveTile(tile1, pos1));
            yield return StartCoroutine(MoveTile(tile2, pos2));
        }
        
        isProcessing = false;
    }
    
    IEnumerator MoveTile(GameObject tile, Vector2Int targetPos)
    {
        Vector3 targetWorldPos = new Vector3(targetPos.x * tileSize, targetPos.y * tileSize, 0);
        float duration = 0.2f;
        float elapsed = 0;
        Vector3 startPos = tile.transform.position;
        
        while (elapsed < duration)
        {
            tile.transform.position = Vector3.Lerp(startPos, targetWorldPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        tile.transform.position = targetWorldPos;
    }
    
    IEnumerator CheckAndClearMatches()
    {
        List<GameObject> allMatches = GetAllMatches();
        
        while (allMatches.Count > 0)
        {
            // Clear matches
            foreach (GameObject tile in allMatches)
            {
                if (tile != null)
                {
                    Tile tileScript = tile.GetComponent<Tile>();
                    grid[tileScript.gridX, tileScript.gridY] = null;
                    Destroy(tile);
                }
            }
            
            yield return new WaitForSeconds(0.3f);
            
            // Drop tiles
            yield return StartCoroutine(DropTiles());
            
            // Fill empty spaces
            yield return StartCoroutine(FillEmptySpaces());
            
            // Check for new matches
            allMatches = GetAllMatches();
        }
    }
    
    IEnumerator DropTiles()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == null)
                {
                    for (int aboveY = y + 1; aboveY < gridHeight; aboveY++)
                    {
                        if (grid[x, aboveY] != null)
                        {
                            grid[x, y] = grid[x, aboveY];
                            grid[x, aboveY] = null;
                            
                            Tile tile = grid[x, y].GetComponent<Tile>();
                            tile.gridY = y;
                            StartCoroutine(MoveTile(grid[x, y], new Vector2Int(x, y)));
                            break;
                        }
                    }
                }
            }
        }
        
        yield return new WaitForSeconds(0.3f);
    }
    
    IEnumerator FillEmptySpaces()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == null)
                {
                    SpawnTile(x, y);
                    yield return new WaitForSeconds(0.05f);
                }
            }
        }
    }
    
    List<GameObject> GetAllMatches()
    {
        HashSet<GameObject> matches = new HashSet<GameObject>();
        
        // Check horizontal matches
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth - 2; x++)
            {
                if (grid[x, y] != null && grid[x + 1, y] != null && grid[x + 2, y] != null)
                {
                    int type1 = grid[x, y].GetComponent<Tile>().tileType;
                    int type2 = grid[x + 1, y].GetComponent<Tile>().tileType;
                    int type3 = grid[x + 2, y].GetComponent<Tile>().tileType;
                    
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
                    int type1 = grid[x, y].GetComponent<Tile>().tileType;
                    int type2 = grid[x, y + 1].GetComponent<Tile>().tileType;
                    int type3 = grid[x, y + 2].GetComponent<Tile>().tileType;
                    
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
    
    bool IsAdjacent(Vector2Int pos1, Vector2Int pos2)
    {
        return (Mathf.Abs(pos1.x - pos2.x) == 1 && pos1.y == pos2.y) ||
               (Mathf.Abs(pos1.y - pos2.y) == 1 && pos1.x == pos2.x);
    }
    
    bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }
}

public class Tile : MonoBehaviour
{
    public int gridX;
    public int gridY;
    public int tileType;
    private GridManager gridManager;
    
    public void Initialize(int x, int y, int type, GridManager manager)
    {
        gridX = x;
        gridY = y;
        tileType = type;
        gridManager = manager;
    }
}