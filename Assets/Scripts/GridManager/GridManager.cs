using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Grid Settings")]
    public int gridWidth = 8;
    public int gridHeight = 8;
    public float tileSize = 1f;

    [Header("Grid Initialization")]
    public int initialFillRows = 4; // How many rows to preload at start
    public int preloadRows = 2;     // Extra rows preloaded below visible grid

    [Header("Swap Settings")]
    public float swapDuration = 0.15f; // Time in seconds for swap animation

    [Header("Drop Settings")]
    public float dropDuration = 0.15f; // Time in seconds PER TILE UNIT (gravity speed)

    [Header("Components")]
    public CursorController cursorController;
    public GridRiser gridRiser;
    public MatchDetector matchDetector;
    public MatchProcessor matchProcessor;
    public TileSpawner tileSpawner;
    public BlockSlipManager blockSlipManager; // NEW

    #endregion

    #region Internal State

    private GameObject[,] grid;         // Main grid of tiles
    private GameObject[,] preloadGrid;  // Extra rows below main grid
    private bool isSwapping = false;

    // Animated Tile Tracking
    private HashSet<GameObject> swappingTiles = new HashSet<GameObject>(); // Tiles currently being swapped
    private HashSet<GameObject> droppingTiles = new HashSet<GameObject>(); // Tiles currently dropping

    #endregion

    #region Public Properties / Queries

    public bool IsSwapping => isSwapping;
    public bool IsTileSwapping(GameObject tile) => swappingTiles.Contains(tile);
    public bool IsTileDropping(GameObject tile) => droppingTiles.Contains(tile);
    public bool IsTileAnimating(GameObject tile) => swappingTiles.Contains(tile) || droppingTiles.Contains(tile);

    public void SetIsSwapping(bool value) => isSwapping = value;

    /// <summary>
    /// Finds the grid position of a given tile GameObject 
    /// </summary>
    public Vector2Int? FindTilePosition(GameObject tile)
    {
        if (tile == null) return null;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y] == tile)
                {
                    return new Vector2Int(x, y);
                }
            }
        }
        return null;
    }

    public void AddBreathingRoom(int tilesMatched)
    {
        gridRiser.AddBreathingRoom(tilesMatched);
    }

    #endregion

    #region Unity Lifecycle & Initialization

    void Start()
    {
        grid = new GameObject[gridWidth, gridHeight];
        preloadGrid = new GameObject[gridWidth, preloadRows];

        // Initialize all components
        cursorController.Initialize(this, tileSize, gridWidth, gridHeight);
        tileSpawner.Initialize(this, tileSize, grid, preloadGrid, gridWidth, gridHeight, preloadRows);
        matchDetector.Initialize(grid, gridWidth, gridHeight);
        matchProcessor.Initialize(this, grid, matchDetector);
        gridRiser.Initialize(this, grid, preloadGrid, tileSpawner, cursorController, matchDetector, matchProcessor, tileSize, gridWidth, gridHeight);

        // Initialize BlockSlipManager
        blockSlipManager.Initialize(
            this,
            grid,
            gridWidth,
            gridHeight,
            tileSize,
            swapDuration,
            dropDuration,
            gridRiser,
            matchDetector,
            matchProcessor,
            cursorController,
            swappingTiles,
            droppingTiles
        );

        // Create background and initialize grid
        tileSpawner.CreateBackgroundTiles();
        StartCoroutine(InitializeGrid());
    }

    IEnumerator InitializeGrid()
    {
        // Fill preload rows (below visible grid)
        for (int y = 0; y < preloadRows; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                tileSpawner.SpawnPreloadTile(x, y, gridRiser.CurrentGridOffset);
                yield return new WaitForSeconds(0.01f);
            }
        }

        // Fill bottom rows of main grid
        for (int y = 0; y < initialFillRows; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                tileSpawner.SpawnTile(x, y, gridRiser.CurrentGridOffset);
                yield return new WaitForSeconds(0.02f);
            }
        }

        // Check for initial matches
        yield return StartCoroutine(matchProcessor.CheckAndClearMatches());

        // Start rising
        gridRiser.StartRising();
    }

    void Update()
    {
        if (gridRiser.IsGameOver) return;

        // Clean up only destroyed (null) tiles from animation sets
        swappingTiles.RemoveWhere(tile => tile == null);
        droppingTiles.RemoveWhere(tile => tile == null);

        // Clean up Block Slip tracking dictionaries inside BlockSlipManager
        blockSlipManager.CleanupTracking();

        // Handle cursor input
        cursorController.HandleInput(gridWidth, gridHeight);

        // Optional debug indicator
        // blockSlipManager.UpdateBlockSlipIndicator();

        HandleSwapInput();
    }

    #endregion

    #region Input & Swap Handling

    private void HandleSwapInput()
    {
        if (isSwapping) return;

        // Swap with Z (primary), K (alternate), or Space
        if (!(Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Space)))
            return;

        Vector2Int cursorPos = cursorController.CursorPosition;
        int leftX = cursorPos.x;
        int rightX = cursorPos.x + 1;
        int y = cursorPos.y;

        GameObject leftTile = grid[leftX, y];
        GameObject rightTile = grid[rightX, y];

        // First give BlockSlipManager a chance
        if (blockSlipManager.TryHandleBlockSlipAtCursor(cursorPos))
        {
            // Block Slip took over, so skip normal swap
            return;
        }

        // Normal swap sanity checks
        bool leftProcessed = matchProcessor.IsTileBeingProcessed(leftX, y);
        bool rightProcessed = matchProcessor.IsTileBeingProcessed(rightX, y);
        bool leftSwapping = leftTile != null && swappingTiles.Contains(leftTile);
        bool rightSwapping = rightTile != null && swappingTiles.Contains(rightTile);
        bool leftDropping = leftTile != null && droppingTiles.Contains(leftTile);
        bool rightDropping = rightTile != null && droppingTiles.Contains(rightTile);

        if (!leftProcessed && !rightProcessed &&
            !leftSwapping && !rightSwapping &&
            !leftDropping && !rightDropping)
        {
            StartCoroutine(SwapCursorTiles());
        }
        else
        {
            Debug.Log($">>> SWAP BLOCKED <<<");
            Debug.Log($"Reason: leftProcessed={leftProcessed}, rightProcessed={rightProcessed}, leftSwapping={leftSwapping}, rightSwapping={rightSwapping}, leftDropping={leftDropping}, rightDropping={rightDropping}");
        }
    }

    IEnumerator SwapCursorTiles()
    {
        isSwapping = true;

        Vector2Int cursorPos = cursorController.CursorPosition;
        int leftX = cursorPos.x;
        int rightX = cursorPos.x + 1;
        int y = cursorPos.y;

        GameObject leftTile = grid[leftX, y];
        GameObject rightTile = grid[rightX, y];

        // Mark tiles as swapping BEFORE updating grid array to prevent race conditions
        if (leftTile != null) swappingTiles.Add(leftTile);
        if (rightTile != null) swappingTiles.Add(rightTile);

        // Update grid array after protection is applied
        grid[leftX, y] = rightTile;
        grid[rightX, y] = leftTile;

        try
        {
            // Start swap animations WITHOUT updating grid coordinates yet
            if (leftTile != null)
            {
                blockSlipManager.StartSwapAnimation(leftTile, new Vector2Int(rightX, y));
            }

            if (rightTile != null)
            {
                blockSlipManager.StartSwapAnimation(rightTile, new Vector2Int(leftX, y));
            }

            yield return new WaitForSeconds(swapDuration);

            // NOW update grid coordinates after animation completes
            Vector2Int? leftPos = FindTilePosition(leftTile);
            Vector2Int? rightPos = FindTilePosition(rightTile);

            if (leftTile != null && leftPos.HasValue)
            {
                Tile tile = leftTile.GetComponent<Tile>();
                tile.Initialize(leftPos.Value.x, leftPos.Value.y, tile.TileType, this);
                leftTile.transform.position =
                    new Vector3(leftPos.Value.x * tileSize, leftPos.Value.y * tileSize + gridRiser.CurrentGridOffset, 0);
            }

            if (rightTile != null && rightPos.HasValue)
            {
                Tile tile = rightTile.GetComponent<Tile>();
                tile.Initialize(rightPos.Value.x, rightPos.Value.y, tile.TileType, this);
                rightTile.transform.position =
                    new Vector3(rightPos.Value.x * tileSize, rightPos.Value.y * tileSize + gridRiser.CurrentGridOffset, 0);
            }

            // Drop any tiles that can fall after swap
            yield return StartCoroutine(DropTiles());

            // Check entire grid for matches since tiles may have dropped far from original position
            List<GameObject> matches = matchDetector.GetAllMatches();
            if (matches.Count > 0 && !matchProcessor.IsProcessingMatches)
            {
                StartCoroutine(matchProcessor.ProcessMatches(matches));
            }
        }
        finally
        {
            swappingTiles.Remove(leftTile);
            swappingTiles.Remove(rightTile);
            isSwapping = false;
        }
    }

    #endregion

    #region Drop Logic

    public IEnumerator DropTiles()
    {
        List<(GameObject tile, Vector2Int from, Vector2Int to)> drops =
            new List<(GameObject, Vector2Int, Vector2Int)>();
        int maxDropDistance = 0;

        // Collect all drops first
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
                            GameObject tile = grid[x, aboveY];
                            int dropDistance = aboveY - y;
                            maxDropDistance = Mathf.Max(maxDropDistance, dropDistance);
                            drops.Add((tile, new Vector2Int(x, aboveY), new Vector2Int(x, y)));

                            // Update grid array
                            grid[x, y] = tile;
                            grid[x, aboveY] = null;
                            break;
                        }
                    }
                }
            }
        }

        // Update coordinates and start animations
        foreach (var (tile, from, to) in drops)
        {
            if (tile != null)
            {
                Tile tileScript = tile.GetComponent<Tile>();
                tileScript.Initialize(to.x, to.y, tileScript.TileType, this);

                // Let BlockSlipManager handle animation + tracking
                blockSlipManager.BeginDrop(tile, from, to);
            }
        }

        // Wait for the longest drop to complete (plus a small buffer)
        if (drops.Count > 0)
        {
            float waitTime = dropDuration * maxDropDistance + 0.05f;
            yield return new WaitForSeconds(waitTime);
        }
    }

    #endregion
}
