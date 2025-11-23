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
    public ScoreManager scoreManager;
    public GameObject tilePrefab;
    public Sprite[] tileSprites;
    public GameObject tileBackground;
    public GameObject cursorPrefab; // Visual indicator for the cursor
    
    [Header("Cursor Settings")]
    public int cursorWidth = 2; // Cursor covers 2 tiles horizontally
    public int cursorHeight = 1; // Cursor covers 1 tile vertically
    public Color cursorColor = new Color(1f, 1f, 1f, 0.5f);
    private bool usingPrefabCursor = false;
    
    private GameObject[,] grid;
    private Vector2Int cursorPosition; // Left position of the 1x2 cursor
    private GameObject cursorVisual;
    private bool isSwapping = false; // Only blocks swapping, not cursor movement
    
    void Start()
    {
        grid = new GameObject[gridWidth, gridHeight];
        cursorPosition = new Vector2Int(0, gridHeight / 2);
        CreateGrid();
        CreateCursor();
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
    
    void CreateCursor()
    {
        if (cursorPrefab != null)
        {
            cursorVisual = Instantiate(cursorPrefab, Vector3.zero, Quaternion.identity, transform);
            usingPrefabCursor = true;
        }
        else
        {
            // Create a simple cursor visual
            cursorVisual = new GameObject("Cursor");
            cursorVisual.transform.SetParent(transform);
            
            SpriteRenderer sr = cursorVisual.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCursorSprite();
            sr.color = cursorColor;
            sr.sortingOrder = 10;
            usingPrefabCursor = false;
        }
        
        UpdateCursorPosition();
    }
    
    Sprite CreateCursorSprite()
    {
        // Create a simple rectangular sprite for the cursor
        Texture2D tex = new Texture2D(200, 100);
        Color[] pixels = new Color[200 * 100];
        
        // Fill with transparent
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }
        
        // Draw border
        for (int x = 0; x < 200; x++)
        {
            for (int y = 0; y < 100; y++)
            {
                if (x < 5 || x >= 195 || y < 5 || y >= 95)
                {
                    pixels[y * 200 + x] = Color.white;
                }
            }
        }
        
        tex.SetPixels(pixels);
        tex.Apply();
        
        return Sprite.Create(tex, new Rect(0, 0, 200, 100), new Vector2(0.5f, 0.5f), 100);
    }
    
    void UpdateCursorPosition()
    {
        // Center the cursor between the two tiles it covers
        float centerX = (cursorPosition.x + 1f) * tileSize; // Position between left and right tile
        float centerY = cursorPosition.y * tileSize;
        cursorVisual.transform.position = new Vector3(centerX - 0.5f * tileSize, centerY, -1f);

        if(!usingPrefabCursor)
        {
            // Scale to cover exactly 2 tiles wide and 1 tile tall
        cursorVisual.transform.localScale = new Vector3(cursorWidth * tileSize, cursorHeight * tileSize, 1f);
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
        
        yield return StartCoroutine(CheckAndClearMatches());
    }
    
    void SpawnTile(int x, int y)
    {
        Vector3 pos = new Vector3(x * tileSize, y * tileSize, 0);
        int randomIndex = Random.Range(0, tileSprites.Length);
        GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
        
        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        sr.sprite = tileSprites[randomIndex];
        
        Tile tileScript = tile.GetComponent<Tile>();
        tileScript.Initialize(x, y, randomIndex, this);
        
        grid[x, y] = tile;
    }
    
    void Update()
    {
        // Cursor movement (always allowed)
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            MoveCursor(-1, 0);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            MoveCursor(1, 0);
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            MoveCursor(0, 1);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            MoveCursor(0, -1);
        }
        
        // Swap tiles (only when not already swapping)
        if (!isSwapping && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            StartCoroutine(SwapCursorTiles());
        }
    }
    
    void MoveCursor(int deltaX, int deltaY)
    {
        int newX = Mathf.Clamp(cursorPosition.x + deltaX, 0, gridWidth - 2);
        int newY = Mathf.Clamp(cursorPosition.y + deltaY, 0, gridHeight - 1);
        
        cursorPosition = new Vector2Int(newX, newY);
        UpdateCursorPosition();
    }
    
    IEnumerator SwapCursorTiles()
    {
        isSwapping = true;
        
        int leftX = cursorPosition.x;
        int rightX = cursorPosition.x + 1;
        int y = cursorPosition.y;
        
        GameObject leftTile = grid[leftX, y];
        GameObject rightTile = grid[rightX, y];
        
        // Swap in grid
        grid[leftX, y] = rightTile;
        grid[rightX, y] = leftTile;
        
        // Update tile data and animate
        if (leftTile != null)
        {
            leftTile.GetComponent<Tile>().Initialize(rightX, y, leftTile.GetComponent<Tile>().TileType, this);
            StartCoroutine(MoveTile(leftTile, new Vector2Int(rightX, y)));
        }
        
        if (rightTile != null)
        {
            rightTile.GetComponent<Tile>().Initialize(leftX, y, rightTile.GetComponent<Tile>().TileType, this);
            StartCoroutine(MoveTile(rightTile, new Vector2Int(leftX, y)));
        }
        
        yield return new WaitForSeconds(0.3f);
        
        // Check for matches and process
        yield return StartCoroutine(CheckAndClearMatches());
        
        isSwapping = false;
    }
    
    IEnumerator MoveTile(GameObject tile, Vector2Int targetPos)
    {
        if (tile == null) yield break;
        
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
        Debug.Log($"Found {allMatches.Count} matched tiles.");
        while (allMatches.Count > 0)
        {
            // Add score for matched tiles
            if (scoreManager != null)
            {
                Debug.Log($"Matched {allMatches.Count} tiles.");
                scoreManager.AddScore(allMatches.Count);
            }
            
            foreach (GameObject tile in allMatches)
            {
                if (tile != null)
                {
                    Tile tileScript = tile.GetComponent<Tile>();
                    grid[tileScript.GridX, tileScript.GridY] = null;
                    Destroy(tile);
                }
            }
            
            yield return new WaitForSeconds(0.3f);
            yield return StartCoroutine(DropTiles());
            yield return StartCoroutine(FillEmptySpaces());
            
            allMatches = GetAllMatches();
        }
        
        // Reset combo when no more matches found
        if (scoreManager != null)
        {
            scoreManager.ResetCombo();
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
                            tile.Initialize(x, y, tile.TileType, this);
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
    
    bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }
}