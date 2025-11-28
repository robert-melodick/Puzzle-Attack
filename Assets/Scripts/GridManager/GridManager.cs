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

    [Header("Components")]
    public CursorController cursorController;
    public GridRiser gridRiser;
    public MatchDetector matchDetector;
    public MatchProcessor matchProcessor;
    public TileSpawner tileSpawner;

    private GameObject[,] grid;
    private GameObject[,] preloadGrid; // Extra rows below main grid
    private bool isSwapping = false;
    private HashSet<GameObject> swappingTiles = new HashSet<GameObject>(); // Tiles currently being swapped
    private HashSet<GameObject> droppingTiles = new HashSet<GameObject>(); // Tiles currently dropping

    public bool IsTileSwapping(GameObject tile) => swappingTiles.Contains(tile);
    public bool IsTileDropping(GameObject tile) => droppingTiles.Contains(tile);
    public bool IsTileAnimating(GameObject tile) => swappingTiles.Contains(tile) || droppingTiles.Contains(tile);

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

        // Handle cursor input
        cursorController.HandleInput(gridWidth, gridHeight);

        // Swap with Z (primary), K (alternate), or Space
        if (!isSwapping && (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Space)))
        {
            Vector2Int cursorPos = cursorController.CursorPosition;
            int leftX = cursorPos.x;
            int rightX = cursorPos.x + 1;
            int y = cursorPos.y;

            GameObject leftTile = grid[leftX, y];
            GameObject rightTile = grid[rightX, y];

            // Check if tiles are being processed, swapping, or dropping
            bool leftProcessed = matchProcessor.IsTileBeingProcessed(leftX, y);
            bool rightProcessed = matchProcessor.IsTileBeingProcessed(rightX, y);
            bool leftSwapping = leftTile != null && swappingTiles.Contains(leftTile);
            bool rightSwapping = rightTile != null && swappingTiles.Contains(rightTile);
            bool leftDropping = leftTile != null && droppingTiles.Contains(leftTile);
            bool rightDropping = rightTile != null && droppingTiles.Contains(rightTile);

            if (!leftProcessed && !rightProcessed && !leftSwapping && !rightSwapping && !leftDropping && !rightDropping)
            {
                StartCoroutine(SwapCursorTiles());
            }
            else
            {
                Debug.Log("Cannot swap - tiles are being processed, swapping, or dropping!");
            }
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

        grid[leftX, y] = rightTile;
        grid[rightX, y] = leftTile;

        // Mark tiles as swapping so GridRiser won't update their positions
        if (leftTile != null) swappingTiles.Add(leftTile);
        if (rightTile != null) swappingTiles.Add(rightTile);

        try
        {
            // Start swap animations WITHOUT updating grid coordinates yet
            // This prevents GridRiser from teleporting them before animation completes
            if (leftTile != null)
            {
                StartCoroutine(MoveTileSwap(leftTile, new Vector2Int(rightX, y)));
            }

            if (rightTile != null)
            {
                StartCoroutine(MoveTileSwap(rightTile, new Vector2Int(leftX, y)));
            }

            yield return new WaitForSeconds(0.15f);

            // NOW update grid coordinates after animation completes
            if (leftTile != null)
            {
                leftTile.GetComponent<Tile>().Initialize(rightX, y, leftTile.GetComponent<Tile>().TileType, this);
                // Snap to exact swapped position to prevent diagonal movement when dropping
                leftTile.transform.position = new Vector3(rightX * tileSize, y * tileSize + gridRiser.CurrentGridOffset, 0);
            }

            if (rightTile != null)
            {
                rightTile.GetComponent<Tile>().Initialize(leftX, y, rightTile.GetComponent<Tile>().TileType, this);
                // Snap to exact swapped position to prevent diagonal movement when dropping
                rightTile.transform.position = new Vector3(leftX * tileSize, y * tileSize + gridRiser.CurrentGridOffset, 0);
            }
        }
        finally
        {
            // ALWAYS remove tiles from swapping set, even if an exception occurred
            swappingTiles.Remove(leftTile);
            swappingTiles.Remove(rightTile);

            // Allow new swaps immediately after animation completes
            isSwapping = false;
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

    IEnumerator MoveTileSwap(GameObject tile, Vector2Int targetPos)
    {
        // Special version for swapping that doesn't rely on tile's GridX/GridY
        if (tile == null) yield break;

        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        int originalSortingOrder = sr != null ? sr.sortingOrder : 0;

        // Bring tile to front during swap
        if (sr != null) sr.sortingOrder = 10;

        Vector3 startPos = tile.transform.position;
        float duration = 0.15f;
        float elapsed = 0;

        while (elapsed < duration)
        {
            // Calculate target position dynamically using current grid offset
            Vector3 targetWorldPos = new Vector3(targetPos.x * tileSize, targetPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
            tile.transform.position = Vector3.Lerp(startPos, targetWorldPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Final position snap
        Vector3 finalPos = new Vector3(targetPos.x * tileSize, targetPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
        tile.transform.position = finalPos;

        // Restore original sorting order
        if (sr != null) sr.sortingOrder = originalSortingOrder;
    }

    IEnumerator MoveTileDrop(GameObject tile, Vector2Int fromPos, Vector2Int toPos)
    {
        if (tile == null)
        {
            droppingTiles.Remove(tile);
            yield break;
        }

        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        int originalSortingOrder = sr != null ? sr.sortingOrder : 0;

        // Bring tile to front during drop to prevent clipping behind tiles below
        if (sr != null) sr.sortingOrder = 10;

        Vector3 startWorldPos = new Vector3(fromPos.x * tileSize, fromPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
        float duration = 0.15f;
        float elapsed = 0;

        try
        {
            while (elapsed < duration && tile != null)
            {
                // Calculate target position dynamically using current grid offset
                Vector3 targetWorldPos = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
                tile.transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Final position snap
            if (tile != null)
            {
                tile.transform.position = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);

                Tile tileScript = tile.GetComponent<Tile>();
                if (tileScript != null)
                {
                    tileScript.PlayLandSound();
                }

                // Restore original sorting order
                if (sr != null) sr.sortingOrder = originalSortingOrder;
            }
        }
        finally
        {
            // ALWAYS remove from dropping set and restore sorting order
            droppingTiles.Remove(tile);
            if (sr != null) sr.sortingOrder = originalSortingOrder;
        }
    }

    IEnumerator MoveTile(GameObject tile, Vector2Int targetPos, bool playLandSound = false)
    {
        if (tile == null) yield break;

        Vector3 targetWorldPos = new Vector3(targetPos.x * tileSize, targetPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
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

    public IEnumerator DropTiles()
    {
        List<(GameObject tile, Vector2Int from, Vector2Int to)> drops = new List<(GameObject, Vector2Int, Vector2Int)>();

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
                // Update tile coordinates immediately
                Tile tileScript = tile.GetComponent<Tile>();
                tileScript.Initialize(to.x, to.y, tileScript.TileType, this);

                // Add to dropping set to protect from GridRiser updates
                droppingTiles.Add(tile);
                StartCoroutine(MoveTileDrop(tile, from, to));
            }
        }

        yield return new WaitForSeconds(0.3f);
    }

    public void AddBreathingRoom(int tilesMatched)
    {
        gridRiser.AddBreathingRoom(tilesMatched);
    }
}
