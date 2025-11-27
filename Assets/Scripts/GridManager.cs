using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridWidth = 8;
    public int gridHeight = 8;
    public float tileSize = 1f;
    
    [Header("Grid Initialization")]
    public int initialFillRows = 4; // How many rows to preload at start
    public int preloadRows = 2; // Extra rows preloaded below visible grid
    
    [Header("Rising Mechanics")]
    public float normalRiseSpeed = 0.5f; // Units per second
    public float fastRiseSpeed = 2f; // Units per second when holding X
    public float gracePeriod = 2f; // Seconds before game over when block reaches top

    [Header("Breathing Room")]
    public bool enableBreathingRoom = true; // Toggle breathing room feature
    public float breathingRoomPerTile = 0.2f; // Seconds of breathing room per tile matched
    public float maxBreathingRoom = 5f; // Maximum breathing room duration

    [Header("Match Processing")]
    public float processMatchDuration = 1.5f;
    public float blinkSpeed = 0.15f;
    public float delayBetweenPops = 1.5f; // Time between each tile popping in sequence
    
    [Header("Prefabs")]
    public ScoreManager scoreManager;
    public GameObject tilePrefab;
    public Sprite[] tileSprites;
    public GameObject tileBackground;
    public GameObject cursorPrefab;
    
    [Header("Cursor Settings")]
    public int cursorWidth = 2;
    public int cursorHeight = 1;
    public Color cursorColor = new Color(1f, 1f, 1f, 0.5f);
    private bool usingPrefabCursor = false;
    
    private GameObject[,] grid;
    private GameObject[,] preloadGrid; // Extra rows below main grid
    private Vector2Int cursorPosition;
    private GameObject cursorVisual;
    private bool isSwapping = false;
    private bool isProcessingMatches = false;
    private HashSet<Vector2Int> processingTiles = new HashSet<Vector2Int>();
    
    private float currentGridOffset = 0f; // How much the grid has risen
    private float nextRowSpawnOffset = 0f; // When to spawn next row
    private bool isInGracePeriod = false;
    private float gracePeriodTimer = 1.5f;
    private bool hasBlockAtTop = false;
    private bool gameOver = false;
    private float breathingRoomTimer = 0f; // Time remaining before grid resumes rising
    
    void Start()
    {
        grid = new GameObject[gridWidth, gridHeight];
        preloadGrid = new GameObject[gridWidth, preloadRows];
        cursorPosition = new Vector2Int(0, gridHeight / 2);
        CreateGrid();
        CreateCursor();
        StartCoroutine(InitializeGrid());
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
        Texture2D tex = new Texture2D(200, 100);
        Color[] pixels = new Color[200 * 100];
        
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }
        
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
    
    IEnumerator InitializeGrid()
    {
        // Fill preload rows (below visible grid)
        for (int y = 0; y < preloadRows; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                SpawnPreloadTile(x, y);
                yield return new WaitForSeconds(0.01f);
            }
        }
        
        // Fill bottom rows of main grid
        for (int y = 0; y < initialFillRows; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                SpawnTile(x, y);
                yield return new WaitForSeconds(0.02f);
            }
        }
        
        // Check for initial matches
        yield return StartCoroutine(CheckAndClearMatches());
        
        // Start rising
        StartCoroutine(RiseGrid());
    }
    
    void SpawnTile(int x, int y)
    {
        Vector3 pos = new Vector3(x * tileSize, y * tileSize + currentGridOffset, 0);
        int randomIndex = Random.Range(0, tileSprites.Length);
        GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
        
        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        sr.sprite = tileSprites[randomIndex];
        
        Tile tileScript = tile.GetComponent<Tile>();
        tileScript.Initialize(x, y, randomIndex, this);
        
        grid[x, y] = tile;
        
        // Check if tile should be active (66% visible = y position + offset >= -0.33 tiles)
        UpdateTileActiveState(tile, y);
    }
    
    void SpawnPreloadTile(int x, int y)
    {
        // Preload tiles spawn at negative Y positions
        Vector3 pos = new Vector3(x * tileSize, (y - preloadRows) * tileSize + currentGridOffset, 0);
        int randomIndex = Random.Range(0, tileSprites.Length);
        GameObject tile = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
        
        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        sr.sprite = tileSprites[randomIndex];
        sr.color = Color.gray; // Start as grayscale
        
        Tile tileScript = tile.GetComponent<Tile>();
        tileScript.Initialize(x, y - preloadRows, randomIndex, this);
        
        preloadGrid[x, y] = tile;
    }
    
    void UpdateTileActiveState(GameObject tile, int gridY)
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
    
    void SpawnRowAtBottom()
    {
        // Shift all tiles up one row in grid array
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = gridHeight - 1; y > 0; y--)
            {
                grid[x, y] = grid[x, y - 1];
                if (grid[x, y] != null)
                {
                    Tile tile = grid[x, y].GetComponent<Tile>();
                    tile.Initialize(x, y, tile.TileType, this);
                    UpdateTileActiveState(grid[x, y], y);
                }
            }

            // Move preload tile into main grid at y=0
            if (preloadGrid[x, preloadRows - 1] != null)
            {
                GameObject tile = preloadGrid[x, preloadRows - 1];
                grid[x, 0] = tile;
                Tile tileScript = tile.GetComponent<Tile>();
                tileScript.Initialize(x, 0, tileScript.TileType, this);
                UpdateTileActiveState(tile, 0);
            }

            // Shift preload tiles up
            for (int py = preloadRows - 1; py > 0; py--)
            {
                preloadGrid[x, py] = preloadGrid[x, py - 1];
                if (preloadGrid[x, py] != null)
                {
                    Tile tile = preloadGrid[x, py].GetComponent<Tile>();
                    tile.Initialize(x, py - preloadRows, tile.TileType, this);
                }
            }

            // Spawn new preload tile at bottom
            SpawnPreloadTile(x, 0);
        }

        // Shift cursor up in grid coordinates to maintain world position
        cursorPosition.y = Mathf.Min(cursorPosition.y + 1, gridHeight - 1);
    }
    
    IEnumerator RiseGrid()
    {
        while (!gameOver)
        {
            // Handle breathing room countdown
            if (breathingRoomTimer > 0f)
            {
                breathingRoomTimer -= Time.deltaTime;
                if (breathingRoomTimer < 0f)
                {
                    breathingRoomTimer = 0f;
                }
            }

            if (!isInGracePeriod && !isProcessingMatches && breathingRoomTimer <= 0f)
            {
                // X (primary) or L (alternate) to speed up rising
                float riseSpeed = (Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.L)) ? fastRiseSpeed : normalRiseSpeed;
                float riseAmount = riseSpeed * Time.deltaTime;
                
                currentGridOffset += riseAmount;
                nextRowSpawnOffset += riseAmount;
                
                // Update all tile positions
                foreach (GameObject tile in grid)
                {
                    if (tile != null)
                    {
                        Tile tileScript = tile.GetComponent<Tile>();
                        tile.transform.position = new Vector3(
                            tileScript.GridX * tileSize,
                            tileScript.GridY * tileSize + currentGridOffset,
                            0
                        );
                        UpdateTileActiveState(tile, tileScript.GridY);
                    }
                }
                
                // Update preload tile positions
                foreach (GameObject tile in preloadGrid)
                {
                    if (tile != null)
                    {
                        Tile tileScript = tile.GetComponent<Tile>();
                        tile.transform.position = new Vector3(
                            tileScript.GridX * tileSize,
                            tileScript.GridY * tileSize + currentGridOffset,
                            0
                        );
                        UpdateTileActiveState(tile, tileScript.GridY);
                    }
                }
                
                UpdateCursorPosition();
                
                // Spawn new row when grid has risen one tile height
                if (nextRowSpawnOffset >= tileSize)
                {
                    nextRowSpawnOffset -= tileSize;
                    currentGridOffset -= tileSize;
                    SpawnRowAtBottom();
                    
                    // Update positions after spawn
                    foreach (GameObject tile in grid)
                    {
                        if (tile != null)
                        {
                            Tile tileScript = tile.GetComponent<Tile>();
                            tile.transform.position = new Vector3(
                                tileScript.GridX * tileSize,
                                tileScript.GridY * tileSize + currentGridOffset,
                                0
                            );
                            UpdateTileActiveState(tile, tileScript.GridY);
                        }
                    }
                    
                    // Update preload tile positions
                    foreach (GameObject tile in preloadGrid)
                    {
                        if (tile != null)
                        {
                            Tile tileScript = tile.GetComponent<Tile>();
                            tile.transform.position = new Vector3(
                                tileScript.GridX * tileSize,
                                tileScript.GridY * tileSize + currentGridOffset,
                                0
                            );
                            UpdateTileActiveState(tile, tileScript.GridY);
                        }
                    }
                }
                
                // Check if any block reached the top
                CheckTopRow();
            }
            else if (isInGracePeriod)
            {
                // Grace period countdown
                gracePeriodTimer -= Time.deltaTime;
                Debug.Log($"Grace Period: {gracePeriodTimer:F2}s remaining!");
                
                if (gracePeriodTimer <= 0f)
                {
                    GameOver();
                }
                else if (!hasBlockAtTop)
                {
                    // Block was cleared, exit grace period
                    isInGracePeriod = false;
                    Debug.Log("Grace period ended - blocks cleared!");
                }
            }
            
            yield return null;
        }
    }
    
    void CheckTopRow()
    {
        hasBlockAtTop = false;
        
        // Check if any tile in the top row is at or above the danger threshold
        for (int x = 0; x < gridWidth; x++)
        {
            if (grid[x, gridHeight - 1] != null)
            {
                // Calculate the actual world position of the top row
                float topRowWorldY = (gridHeight - 1) * tileSize + currentGridOffset;
                
                // Only trigger grace period if top row is actually at the top of the screen
                // (gridHeight - 1) * tileSize is the maximum allowed position
                if (topRowWorldY >= (gridHeight - 1) * tileSize)
                {
                    hasBlockAtTop = true;
                    if (!isInGracePeriod)
                    {
                        isInGracePeriod = true;
                        gracePeriodTimer = gracePeriod;
                        Debug.Log("WARNING: Block reached the top! Grace period started!");
                    }
                    break;
                }
            }
        }
    }
    
    void GameOver()
    {
        gameOver = true;
        Debug.Log("GAME OVER! Blocks reached the top!");
        // TODO: Show game over screen, stop all gameplay
    }
    
    void Update()
    {
        if (gameOver) return;
        
        // Cursor movement - Arrow Keys (primary) or WASD (alternate)
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
        
        // Swap with Z (primary), K (alternate), or Space
        if (!isSwapping && (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Space)))
        {
            int leftX = cursorPosition.x;
            int rightX = cursorPosition.x + 1;
            int y = cursorPosition.y;
            
            if (!IsTileBeingProcessed(leftX, y) && !IsTileBeingProcessed(rightX, y))
            {
                StartCoroutine(SwapCursorTiles());
            }
            else
            {
                Debug.Log("Cannot swap - tiles are being processed!");
            }
        }
    }
    
    void MoveCursor(int deltaX, int deltaY)
    {
        int newX = Mathf.Clamp(cursorPosition.x + deltaX, 0, gridWidth - 2);
        int newY = Mathf.Clamp(cursorPosition.y + deltaY, 0, gridHeight - 1);
        
        cursorPosition = new Vector2Int(newX, newY);
        UpdateCursorPosition();
    }
    
    void UpdateCursorPosition()
    {
        float centerX = (cursorPosition.x + 1f) * tileSize;
        float centerY = cursorPosition.y * tileSize + currentGridOffset;
        cursorVisual.transform.position = new Vector3(centerX - 0.5f * tileSize, centerY, -1f);

        if(!usingPrefabCursor)
        {
            cursorVisual.transform.localScale = new Vector3(cursorWidth * tileSize, cursorHeight * tileSize, 1f);
        }
    }
    
    IEnumerator SwapCursorTiles()
    {
        isSwapping = true;

        int leftX = cursorPosition.x;
        int rightX = cursorPosition.x + 1;
        int y = cursorPosition.y;

        GameObject leftTile = grid[leftX, y];
        GameObject rightTile = grid[rightX, y];

        grid[leftX, y] = rightTile;
        grid[rightX, y] = leftTile;

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

        yield return new WaitForSeconds(0.15f);

        // Allow new swaps immediately after animation completes
        isSwapping = false;

        // Drop any tiles that can fall after swap
        yield return StartCoroutine(DropTiles());

        // Check entire grid for matches since tiles may have dropped far from original position
        List<GameObject> matches = GetAllMatches();

        if (matches.Count > 0)
        {
            StartCoroutine(ProcessMatches(matches));
        }
    }
    
    IEnumerator MoveTile(GameObject tile, Vector2Int targetPos, bool playLandSound = false)
    {
        if (tile == null) yield break;

        Vector3 targetWorldPos = new Vector3(targetPos.x * tileSize, targetPos.y * tileSize + currentGridOffset, 0);
        float duration = 0.15f;
        float elapsed = 0;
        Vector3 startPos = tile.transform.position;

        while (elapsed < duration)
        {
            tile.transform.position = Vector3.Lerp(startPos, targetWorldPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        tile.transform.position = targetWorldPos;

        if (playLandSound)
        {
            Tile tileScript = tile.GetComponent<Tile>();
            if (tileScript != null)
            {
                tileScript.PlayLandSound();
            }
        }
    }
    
    IEnumerator CheckAndClearMatches()
    {
        isProcessingMatches = true;
        List<List<GameObject>> matchGroups = GetMatchGroups();

        while (matchGroups.Count > 0)
        {
            // Flatten all groups to mark tiles as processing
            foreach (List<GameObject> group in matchGroups)
            {
                foreach (GameObject tile in group)
                {
                    if (tile != null)
                    {
                        Tile tileScript = tile.GetComponent<Tile>();
                        processingTiles.Add(new Vector2Int(tileScript.GridX, tileScript.GridY));
                    }
                }
            }

            // Flatten for blinking
            List<GameObject> allMatchedTiles = new List<GameObject>();
            foreach (List<GameObject> group in matchGroups)
            {
                allMatchedTiles.AddRange(group);
            }

            yield return StartCoroutine(BlinkTiles(allMatchedTiles, processMatchDuration));

            // Get the current combo
            int currentCombo = scoreManager != null ? scoreManager.GetCombo() : 0;

            // Count total tiles across all groups for this match
            int totalTiles = 0;
            foreach (List<GameObject> group in matchGroups)
            {
                totalTiles += group.Count;
            }

            // Add score once for all tiles matched from this action
            if (scoreManager != null)
            {
                scoreManager.AddScore(totalTiles);
            }

            // Add breathing room based on tiles matched
            AddBreathingRoom(totalTiles);

            // All groups from this match use the same combo number
            int comboNumber = currentCombo + 1;

            // Pop each group asynchronously with the same combo
            List<Coroutine> popCoroutines = new List<Coroutine>();
            foreach (List<GameObject> group in matchGroups)
            {
                popCoroutines.Add(StartCoroutine(PopTilesInSequence(group, comboNumber)));
            }

            // Wait for all groups to finish popping
            foreach (Coroutine coroutine in popCoroutines)
            {
                yield return coroutine;
            }

            yield return new WaitForSeconds(0.1f);
            yield return StartCoroutine(DropTiles());

            processingTiles.Clear();

            matchGroups = GetMatchGroups();
        }

        if (scoreManager != null)
        {
            scoreManager.ResetCombo();
        }

        isProcessingMatches = false;
    }

    IEnumerator ProcessMatches(List<GameObject> matches)
    {
        if (matches.Count == 0) yield break;

        isProcessingMatches = true;

        // Group the matches into separate connected groups
        List<List<GameObject>> matchGroups = GroupMatchedTiles(matches);

        // Mark all tiles as processing
        foreach (List<GameObject> group in matchGroups)
        {
            foreach (GameObject tile in group)
            {
                if (tile != null)
                {
                    Tile tileScript = tile.GetComponent<Tile>();
                    processingTiles.Add(new Vector2Int(tileScript.GridX, tileScript.GridY));
                }
            }
        }

        yield return StartCoroutine(BlinkTiles(matches, processMatchDuration));

        // Get the current combo
        int currentCombo = scoreManager != null ? scoreManager.GetCombo() : 0;

        // Count total tiles across all groups for this match
        int totalTiles = 0;
        foreach (List<GameObject> group in matchGroups)
        {
            totalTiles += group.Count;
        }

        // Add score once for all tiles matched from this action
        if (scoreManager != null)
        {
            scoreManager.AddScore(totalTiles);
        }

        // Add breathing room based on tiles matched
        AddBreathingRoom(totalTiles);

        // All groups from this match use the same combo number
        int comboNumber = currentCombo + 1;

        // Pop each group asynchronously with the same combo
        List<Coroutine> popCoroutines = new List<Coroutine>();
        foreach (List<GameObject> group in matchGroups)
        {
            popCoroutines.Add(StartCoroutine(PopTilesInSequence(group, comboNumber)));
        }

        // Wait for all groups to finish popping
        foreach (Coroutine coroutine in popCoroutines)
        {
            yield return coroutine;
        }

        yield return new WaitForSeconds(0.1f);
        yield return StartCoroutine(DropTiles());

        processingTiles.Clear();

        List<List<GameObject>> cascadeMatchGroups = GetMatchGroups();
        if (cascadeMatchGroups.Count > 0)
        {
            // Flatten and recurse
            List<GameObject> cascadeMatches = new List<GameObject>();
            foreach (List<GameObject> group in cascadeMatchGroups)
            {
                cascadeMatches.AddRange(group);
            }
            yield return StartCoroutine(ProcessMatches(cascadeMatches));
        }
        else
        {
            if (scoreManager != null)
            {
                scoreManager.ResetCombo();
            }
            isProcessingMatches = false;
        }
    }

    List<List<GameObject>> GroupMatchedTiles(List<GameObject> matches)
    {
        HashSet<GameObject> allMatches = new HashSet<GameObject>(matches);

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
                            StartCoroutine(MoveTile(grid[x, y], new Vector2Int(x, y), true));
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
    
    IEnumerator BlinkTiles(List<GameObject> tiles, float duration)
    {
        float elapsed = 0f;
        bool isVisible = true;

        List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
        foreach (GameObject tile in tiles)
        {
            if (tile != null)
            {
                SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    spriteRenderers.Add(sr);
                }
            }
        }

        while (elapsed < duration)
        {
            isVisible = !isVisible;
            foreach (SpriteRenderer sr in spriteRenderers)
            {
                if (sr != null)
                {
                    sr.enabled = isVisible;
                }
            }

            yield return new WaitForSeconds(blinkSpeed);
            elapsed += blinkSpeed;
        }

        foreach (SpriteRenderer sr in spriteRenderers)
        {
            if (sr != null)
            {
                sr.enabled = true;
            }
        }
    }

    IEnumerator PopTilesInSequence(List<GameObject> tiles, int combo)
    {
        foreach (GameObject tile in tiles)
        {
            if (tile != null)
            {
                Tile tileScript = tile.GetComponent<Tile>();
                if (tileScript != null)
                {
                    // Play match sound for this tile
                    tileScript.PlayMatchSound(combo);

                    // Remove from grid (but don't destroy yet, so sound can play)
                    grid[tileScript.GridX, tileScript.GridY] = null;

                    // Hide the tile visually
                    SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.enabled = false;
                    }

                    // Wait before next tile (this gives time for sound to play)
                    yield return new WaitForSeconds(delayBetweenPops);

                    // Now destroy the tile
                    Destroy(tile);
                }
            }
        }
    }
    
    List<GameObject> GetAllMatches()
    {
        HashSet<GameObject> matches = new HashSet<GameObject>();

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

    List<List<GameObject>> GetMatchGroups()
    {
        // First get all matched tiles
        HashSet<GameObject> allMatches = new HashSet<GameObject>(GetAllMatches());

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
    
    List<GameObject> GetMatchesInArea(int minX, int maxX, int minY, int maxY)
    {
        HashSet<GameObject> matches = new HashSet<GameObject>();

        minX = Mathf.Max(0, minX);
        maxX = Mathf.Min(gridWidth - 1, maxX);
        minY = Mathf.Max(0, minY);
        maxY = Mathf.Min(gridHeight - 1, maxY);

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
    
    bool IsTileActive(GameObject tile)
    {
        if (tile == null) return false;
        
        Tile tileScript = tile.GetComponent<Tile>();
        float worldY = tileScript.GridY * tileSize + currentGridOffset;
        float visibilityThreshold = -0.33f * tileSize; // 66% visible
        
        return worldY >= visibilityThreshold;
    }
    
    bool IsTileBeingProcessed(int x, int y)
    {
        return processingTiles.Contains(new Vector2Int(x, y));
    }
    
    bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }

    void AddBreathingRoom(int tilesMatched)
    {
        if (!enableBreathingRoom) return;

        float additionalTime = tilesMatched * breathingRoomPerTile;
        breathingRoomTimer = Mathf.Min(breathingRoomTimer + additionalTime, maxBreathingRoom);
        Debug.Log($"Breathing room: +{additionalTime:F2}s for {tilesMatched} tiles (total: {breathingRoomTimer:F2}s)");
    }
}