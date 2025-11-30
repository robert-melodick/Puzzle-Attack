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

    private GameObject[,] grid;         // Main grid of tiles
    private GameObject[,] preloadGrid;  // Extra rows below main grid
    private bool isSwapping = false;
    private HashSet<GameObject> swappingTiles = new HashSet<GameObject>(); // Tiles currently being swapped
    private HashSet<GameObject> droppingTiles = new HashSet<GameObject>(); // Tiles currently dropping

    // Block Slip mechanic: Track drop animation progress and targets
    private Dictionary<GameObject, float> droppingProgress = new Dictionary<GameObject, float>(); // Animation progress (0-1)
    private Dictionary<GameObject, Vector2Int> droppingTargets = new Dictionary<GameObject, Vector2Int>(); // Target positions
    private Dictionary<GameObject, int> dropAnimationVersion = new Dictionary<GameObject, int>(); // Version counter to handle animation interruption
    private HashSet<GameObject> retargetedDrops = new HashSet<GameObject>(); // Tiles that changed target mid-drop (others should land on top of these)

    public bool IsSwapping => isSwapping;
    public bool IsTileSwapping(GameObject tile) => swappingTiles.Contains(tile);
    public bool IsTileDropping(GameObject tile) => droppingTiles.Contains(tile);
    public bool IsTileAnimating(GameObject tile) => swappingTiles.Contains(tile) || droppingTiles.Contains(tile);

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

        // Clean up Block Slip tracking dictionaries
        List<GameObject> keysToRemove = new List<GameObject>();
        foreach (var key in droppingProgress.Keys)
        {
            if (key == null) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove)
        {
            droppingProgress.Remove(key);
            droppingTargets.Remove(key);
            dropAnimationVersion.Remove(key);
        }
        retargetedDrops.RemoveWhere(tile => tile == null);

        // Handle cursor input
        cursorController.HandleInput(gridWidth, gridHeight);

        // **Blockslip Debugger** 
        // Check for Block Slip opportunities and update visual indicator
        // UpdateBlockSlipIndicator();

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

            // Check for Block Slip opportunities (swapping with a dropping tile <50% into destination)
            GameObject fallingBlock = null;
            GameObject swappingBlock = null;
            Vector2Int fallingBlockPos = Vector2Int.zero;
            bool canBlockSlip = false;

            // Block Slip should work even if tiles are being processed - only block if actively swapping
            if (!leftSwapping && !rightSwapping)
            {
                // Case 1: Left tile is dropping, swap right tile with it
                if (leftTile != null && leftDropping && rightTile != null && !rightDropping)
                {
                    if (droppingTargets.TryGetValue(leftTile, out Vector2Int leftTarget) && leftTarget.x == leftX && leftTarget.y == y)
                    {
                        float progress = CalculateProgressIntoDestinationTile(leftTile, leftTarget);
                        if (progress < 0.5f)
                        {
                            fallingBlock = leftTile;
                            swappingBlock = rightTile;
                            fallingBlockPos = new Vector2Int(leftX, y);
                            canBlockSlip = true;
                        }
                    }
                }
                // Case 2: Right tile is dropping, swap left tile with it
                else if (rightTile != null && rightDropping && leftTile != null && !leftDropping)
                {
                    if (droppingTargets.TryGetValue(rightTile, out Vector2Int rightTarget) && rightTarget.x == rightX && rightTarget.y == y)
                    {
                        float progress = CalculateProgressIntoDestinationTile(rightTile, rightTarget);
                        if (progress < 0.5f)
                        {
                            fallingBlock = rightTile;
                            swappingBlock = leftTile;
                            fallingBlockPos = new Vector2Int(rightX, y);
                            canBlockSlip = true;
                        }
                    }
                }
            }
            if (canBlockSlip)
            {
                StartCoroutine(HandleBlockSlip(swappingBlock, fallingBlock, fallingBlockPos));
            }
            else if (!leftProcessed && !rightProcessed && !leftSwapping && !rightSwapping && !leftDropping && !rightDropping)
            {
                StartCoroutine(SwapCursorTiles());
            }
            else
            {
                Debug.Log($">>> SWAP BLOCKED <<<");
                Debug.Log($"Reason: leftProcessed={leftProcessed}, rightProcessed={rightProcessed}, leftSwapping={leftSwapping}, rightSwapping={rightSwapping}, leftDropping={leftDropping}, rightDropping={rightDropping}");
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

        // Mark tiles as swapping BEFORE updating grid array to prevent race conditions
        if (leftTile != null) swappingTiles.Add(leftTile);
        if (rightTile != null) swappingTiles.Add(rightTile);

        // Update grid array after protection is applied
        grid[leftX, y] = rightTile;
        grid[rightX, y] = leftTile;

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

            yield return new WaitForSeconds(swapDuration);

            // NOW update grid coordinates after animation completes
            // Use actual grid positions in case grid rose during swap
            Vector2Int? leftPos = FindTilePosition(leftTile);
            Vector2Int? rightPos = FindTilePosition(rightTile);

            if (leftTile != null && leftPos.HasValue)
            {
                leftTile.GetComponent<Tile>().Initialize(leftPos.Value.x, leftPos.Value.y, leftTile.GetComponent<Tile>().TileType, this);
                // Snap to exact position to prevent diagonal movement when dropping
                leftTile.transform.position = new Vector3(leftPos.Value.x * tileSize, leftPos.Value.y * tileSize + gridRiser.CurrentGridOffset, 0);
            }

            if (rightTile != null && rightPos.HasValue)
            {
                rightTile.GetComponent<Tile>().Initialize(rightPos.Value.x, rightPos.Value.y, rightTile.GetComponent<Tile>().TileType, this);
                // Snap to exact position to prevent diagonal movement when dropping
                rightTile.transform.position = new Vector3(rightPos.Value.x * tileSize, rightPos.Value.y * tileSize + gridRiser.CurrentGridOffset, 0);
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
            // ALWAYS remove tiles from swapping set, even if an exception occurred
            swappingTiles.Remove(leftTile);
            swappingTiles.Remove(rightTile);

            // Release swap lock after ALL operations complete (including drops and match detection)
            isSwapping = false;
        }
    }

    /// <summary>
    /// Updates the Block Slip visual indicator based on current cursor position
    /// </summary>
    void UpdateBlockSlipIndicator()
    {
        if (isSwapping)
        {
            cursorController.SetBlockSlipIndicator(false);
            return;
        }

        Vector2Int cursorPos = cursorController.CursorPosition;
        int leftX = cursorPos.x;
        int rightX = cursorPos.x + 1;
        int y = cursorPos.y;

        GameObject leftTile = grid[leftX, y];
        GameObject rightTile = grid[rightX, y];

        // Check if tiles are being processed or in invalid states
        bool leftProcessed = matchProcessor.IsTileBeingProcessed(leftX, y);
        bool rightProcessed = matchProcessor.IsTileBeingProcessed(rightX, y);
        bool leftSwapping = leftTile != null && swappingTiles.Contains(leftTile);
        bool rightSwapping = rightTile != null && swappingTiles.Contains(rightTile);
        bool leftDropping = leftTile != null && droppingTiles.Contains(leftTile);
        bool rightDropping = rightTile != null && droppingTiles.Contains(rightTile);

        // Block Slip should work even if tiles are being processed (they just finished dropping/matching)
        // Only block if tiles are actively swapping
        bool canCheckBlockSlip = !leftSwapping && !rightSwapping;
        bool hasBlockSlipOpportunity = false;

        if (canCheckBlockSlip)
        {
            // NEW LOGIC: Check if either position has a tile that's currently dropping with <50% progress
            // Case 1: Left tile is dropping, right has a solid tile
            if (leftTile != null && leftDropping && rightTile != null && !rightDropping)
            {
                // Check if left tile is dropping to this exact position with <50% progress
                if (droppingTargets.TryGetValue(leftTile, out Vector2Int leftTarget))
                {
                    if (leftTarget.x == leftX && leftTarget.y == y)
                    {
                        float progress = CalculateProgressIntoDestinationTile(leftTile, leftTarget);
                        if (progress < 0.5f)
                        {
                            hasBlockSlipOpportunity = true;
                        }
                    }
                }
            }
            // Case 2: Right tile is dropping, left has a solid tile
            else if (rightTile != null && rightDropping && leftTile != null && !leftDropping)
            {
                // Check if right tile is dropping to this exact position with <50% progress
                if (droppingTargets.TryGetValue(rightTile, out Vector2Int rightTarget))
                {
                    if (rightTarget.x == rightX && rightTarget.y == y)
                    {
                        float progress = CalculateProgressIntoDestinationTile(rightTile, rightTarget);
                        if (progress < 0.5f)
                        {
                            hasBlockSlipOpportunity = true;
                        }
                    }
                }
            }
        }

        cursorController.SetBlockSlipIndicator(hasBlockSlipOpportunity);
    }

    /// <summary>
    /// Calculates how far (0-1) a falling tile has entered its destination tile
    /// 0 = just entering top of destination, 1 = landed at bottom
    /// </summary>
    float CalculateProgressIntoDestinationTile(GameObject tile, Vector2Int destinationPos)
    {
        if (tile == null) return 1f; // Assume fully in if null

        float currentY = tile.transform.position.y;

        // Top of destination tile (where tile enters)
        float destinationTop = (destinationPos.y + 1) * tileSize + gridRiser.CurrentGridOffset;

        // Bottom of destination tile (where tile lands)
        float destinationBottom = destinationPos.y * tileSize + gridRiser.CurrentGridOffset;

        // Calculate progress into tile
        float progressIntoTile = (destinationTop - currentY) / tileSize;

        // Clamp to 0-1 range
        progressIntoTile = Mathf.Clamp01(progressIntoTile);

        return progressIntoTile;
    }

    /// <summary>
    /// Executes the Block Slip mechanic:
    /// - Swaps swappingBlock into fallingBlockPos (where the falling tile was going)
    /// - Redirects fallingBlock upward to its current position above the stationary block
    /// </summary>
    IEnumerator HandleBlockSlip(GameObject swappingBlock, GameObject fallingBlock, Vector2Int fallingBlockPos)
    {
        isSwapping = true;

        // Get the current position of swappingBlock
        Vector2Int? swappingBlockPos = FindTilePosition(swappingBlock);
        if (!swappingBlockPos.HasValue)
        {
            isSwapping = false;
            yield break;
        }

        Vector2Int stationaryBlockOriginalPos = swappingBlockPos.Value;

        // Falling block will go directly above the stationary block's new position
        Vector2Int fallingBlockNewTarget = new Vector2Int(fallingBlockPos.x, fallingBlockPos.y + 1);

        // Collect all blocks that need to cascade upward (starting from where falling block will land)
        List<(GameObject tile, Vector2Int from, Vector2Int to)> cascadingBlocks = new List<(GameObject, Vector2Int, Vector2Int)>();

        int currentY = fallingBlockNewTarget.y;
        while (currentY < gridHeight)
        {
            GameObject blockAtPosition = grid[fallingBlockPos.x, currentY];

            if (blockAtPosition != null && blockAtPosition != fallingBlock)
            {
                // This block needs to shift up by 1
                int newY = currentY + 1;

                // Check if we can shift this block up
                if (newY >= gridHeight)
                {
                    isSwapping = false;
                    yield break;
                }

                cascadingBlocks.Add((blockAtPosition, new Vector2Int(fallingBlockPos.x, currentY), new Vector2Int(fallingBlockPos.x, newY)));
                currentY++;
            }
            else
            {
                // Empty space or falling block itself, stop cascading
                break;
            }
        }

        // Mark tiles as swapping/dropping
        if (swappingBlock != null) swappingTiles.Add(swappingBlock);

        try
        {
            // Step 1: Stop the falling block's animation
            droppingTiles.Remove(fallingBlock);
            droppingProgress.Remove(fallingBlock);
            droppingTargets.Remove(fallingBlock);

            // Step 2: PROTECT cascading blocks from GridRiser AND stop their drop animations
            foreach (var (tile, from, to) in cascadingBlocks)
            {
                if (tile != null)
                {
                    // Stop any existing drop animation for this tile
                    droppingTiles.Remove(tile);
                    droppingProgress.Remove(tile);
                    droppingTargets.Remove(tile);

                    // Protect from GridRiser
                    swappingTiles.Add(tile);
                }
            }

            // Step 3: Update grid array for cascading blocks (from top to bottom to avoid overwrites)
            for (int i = cascadingBlocks.Count - 1; i >= 0; i--)
            {
                var (tile, from, to) = cascadingBlocks[i];
                grid[to.x, to.y] = tile;
                grid[from.x, from.y] = null; // Clear old position
            }

            // Step 4: Update grid array for main swap
            // Move swappingBlock to falling block's destination
            grid[fallingBlockPos.x, fallingBlockPos.y] = swappingBlock;
            grid[stationaryBlockOriginalPos.x, stationaryBlockOriginalPos.y] = null;

            // Place falling block at its new target position (directly above stationary)
            grid[fallingBlockNewTarget.x, fallingBlockNewTarget.y] = fallingBlock;

            // Step 4: Animate swappingBlock to falling block's original destination
            StartCoroutine(MoveTileSwap(swappingBlock, fallingBlockPos));

            // Step 5: Redirect fallingBlock upward to new target
            // Get falling block's current world position (mid-animation) - use ACTUAL position!
            Vector3 fallingBlockCurrentWorldPos = fallingBlock.transform.position;

            // Calculate the distance the falling block needs to travel (for wait time)
            // Use actual world position for accurate timing
            float fallingBlockTargetY = fallingBlockNewTarget.y * tileSize + gridRiser.CurrentGridOffset;
            float fallingBlockDistance = Mathf.Abs(fallingBlockCurrentWorldPos.y - fallingBlockTargetY) / tileSize;
            float fallingBlockTravelTime = dropDuration * fallingBlockDistance;

            // Update falling block's grid coordinates to new target
            Tile fallingTileScript = fallingBlock.GetComponent<Tile>();
            fallingTileScript.Initialize(fallingBlockNewTarget.x, fallingBlockNewTarget.y, fallingTileScript.TileType, this);

            // Start drop animation from ACTUAL current world position to new target
            // This prevents the snap/delay caused by rounding to grid coordinates
            droppingTiles.Add(fallingBlock);
            StartCoroutine(MoveTileDrop(fallingBlock, fallingBlockCurrentWorldPos, fallingBlockNewTarget));

            // Step 6: Cascade all blocks above upward with smooth animation
            // Also track max cascade duration to ensure we wait long enough
            float maxCascadeDuration = 0f;
            foreach (var (tile, from, to) in cascadingBlocks)
            {
                if (tile != null)
                {
                    // Get the tile's current world position
                    Vector3 currentWorldPos = tile.transform.position;
                    float currentWorldY = currentWorldPos.y;

                    // Calculate the new target world position
                    float targetWorldY = to.y * tileSize + gridRiser.CurrentGridOffset;

                    Debug.Log($"[BlockSlip] Cascading tile at grid {from} -> {to}. CurrentWorldY={currentWorldY:F2}, TargetWorldY={targetWorldY:F2}");

                    // Check if the tile is still above its new target or within threshold
                    if (currentWorldY > targetWorldY)
                    {
                        // Block is still above new target - stop it early at the new target
                        // Calculate remaining distance to fall to new target
                        float remainingDistance = (currentWorldY - targetWorldY) / tileSize;
                        float cascadeDuration = dropDuration * remainingDistance;
                        maxCascadeDuration = Mathf.Max(maxCascadeDuration, cascadeDuration);

                        Debug.Log($"[BlockSlip] -> Using SMOOTH CASCADE (block above target). Distance={remainingDistance:F2} tiles");

                        // Continue drop to new target position (skip the old position entirely)
                        StartCoroutine(MoveTileCascadeSmooth(tile, to));
                    }
                    else
                    {
                        // Block is at or below new target - quick nudge up to new position
                        float nudgeDistance = Mathf.Abs(currentWorldY - targetWorldY) / tileSize;
                        float nudgeDuration = dropDuration * nudgeDistance * 0.5f; // Faster nudge (50% of normal speed)
                        maxCascadeDuration = Mathf.Max(maxCascadeDuration, nudgeDuration);

                        Debug.Log($"[BlockSlip] -> Using QUICK NUDGE (block at/below target). Distance={nudgeDistance:F2} tiles");

                        // Quick nudge up to new target (using faster animation)
                        StartCoroutine(MoveTileCascadeQuickNudge(tile, to));
                    }
                }
            }

            // Wait for swap animation to complete
            yield return new WaitForSeconds(swapDuration);

            // Snap swappingBlock to final position and update coordinates
            swappingBlock.GetComponent<Tile>().Initialize(fallingBlockPos.x, fallingBlockPos.y, swappingBlock.GetComponent<Tile>().TileType, this);
            swappingBlock.transform.position = new Vector3(fallingBlockPos.x * tileSize, fallingBlockPos.y * tileSize + gridRiser.CurrentGridOffset, 0);

            // Wait for the longest animation (falling block OR cascading blocks) to complete
            float longestAnimationTime = Mathf.Max(fallingBlockTravelTime, maxCascadeDuration);
            yield return new WaitForSeconds(longestAnimationTime);

            // Drop any other tiles that can fall
            yield return StartCoroutine(DropTiles());

            // Check for matches
            List<GameObject> matches = matchDetector.GetAllMatches();
            if (matches.Count > 0 && !matchProcessor.IsProcessingMatches)
            {
                StartCoroutine(matchProcessor.ProcessMatches(matches));
            }
        }
        finally
        {
            // Cleanup
            swappingTiles.Remove(swappingBlock);
            isSwapping = false;
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
        float duration = swapDuration;
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

    IEnumerator MoveTileCascadeSmooth(GameObject tile, Vector2Int toPos)
    {
        // Smooth upward animation for cascading blocks during Block Slip
        // Uses actual current world position as start point to prevent lag
        if (tile == null)
        {
            swappingTiles.Remove(tile);
            yield break;
        }

        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        int originalSortingOrder = sr != null ? sr.sortingOrder : 0;

        // Bring tile to front during cascade
        if (sr != null) sr.sortingOrder = 10;

        // USE ACTUAL CURRENT POSITION - this prevents lag from coordinate updates
        Vector3 startWorldPos = tile.transform.position;

        // Calculate duration based on distance (matching gravity speed of drops)
        Vector3 targetWorldPos = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
        float distance = Mathf.Abs(targetWorldPos.y - startWorldPos.y) / tileSize; // Distance in tile units
        float duration = dropDuration * distance; // Use same speed as falling blocks
        float elapsed = 0;

        try
        {
            while (elapsed < duration && tile != null)
            {
                // Calculate target position dynamically using current grid offset
                Vector3 calculatedWorldPosition = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
                tile.transform.position = Vector3.Lerp(startWorldPos, calculatedWorldPosition, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Final position snap and coordinate update
            if (tile != null)
            {
                // NOW update coordinates after animation completes
                Tile tileScript = tile.GetComponent<Tile>();
                tileScript.Initialize(toPos.x, toPos.y, tileScript.TileType, this);

                tile.transform.position = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);

                // Restore original sorting order
                if (sr != null) sr.sortingOrder = originalSortingOrder;
            }
        }
        finally
        {
            // ALWAYS remove from swapping set and restore sorting order
            swappingTiles.Remove(tile);
            if (sr != null) sr.sortingOrder = originalSortingOrder;
        }
    }

    IEnumerator MoveTileCascadeQuickNudge(GameObject tile, Vector2Int toPos)
    {
        // Quick upward nudge for blocks that are already at/below their new target position
        // This is faster than the normal cascade to make the block slip feel snappier
        if (tile == null)
        {
            swappingTiles.Remove(tile);
            yield break;
        }

        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        int originalSortingOrder = sr != null ? sr.sortingOrder : 0;

        // Bring tile to front during cascade
        if (sr != null) sr.sortingOrder = 10;

        // Use actual current position
        Vector3 startWorldPos = tile.transform.position;

        // Calculate target position
        Vector3 targetWorldPos = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);

        // Quick nudge duration (50% faster than normal drop speed)
        float distance = Mathf.Abs(targetWorldPos.y - startWorldPos.y) / tileSize;
        float duration = dropDuration * distance * 0.5f;
        float elapsed = 0;

        try
        {
            while (elapsed < duration && tile != null)
            {
                // Calculate target position dynamically using current grid offset
                Vector3 calculatedWorldPosition = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
                tile.transform.position = Vector3.Lerp(startWorldPos, calculatedWorldPosition, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Final position snap and coordinate update
            if (tile != null)
            {
                // Update coordinates after animation completes
                Tile tileScript = tile.GetComponent<Tile>();
                tileScript.Initialize(toPos.x, toPos.y, tileScript.TileType, this);

                tile.transform.position = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);

                // Restore original sorting order
                if (sr != null) sr.sortingOrder = originalSortingOrder;
            }
        }
        finally
        {
            // ALWAYS remove from swapping set and restore sorting order
            swappingTiles.Remove(tile);
            if (sr != null) sr.sortingOrder = originalSortingOrder;
        }
    }

    /// <summary>
    /// Overload that accepts grid positions - converts to world position and calls main implementation
    /// Used by DropTiles for normal drops (no obstruction checking needed)
    /// </summary>
    IEnumerator MoveTileDrop(GameObject tile, Vector2Int fromPos, Vector2Int toPos)
    {
        Vector3 fromWorldPos = new Vector3(fromPos.x * tileSize, fromPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
        yield return StartCoroutine(MoveTileDrop(tile, fromWorldPos, toPos, false));
    }

    /// <summary>
    /// Overload for Block Slip - uses world position and enables obstruction checking
    /// </summary>
    IEnumerator MoveTileDrop(GameObject tile, Vector3 fromWorldPos, Vector2Int toPos)
    {
        yield return StartCoroutine(MoveTileDrop(tile, fromWorldPos, toPos, true));
    }

    /// <summary>
    /// Main drop animation - uses actual world position to prevent snapping/delay
    /// </summary>
    /// <param name="checkForObstructions">If true, checks for path obstructions each frame (used for Block Slip).
    /// Normal drops from DropTiles should pass false since paths are pre-calculated.</param>
    IEnumerator MoveTileDrop(GameObject tile, Vector3 fromWorldPos, Vector2Int toPos, bool checkForObstructions)
    {
        if (tile == null)
        {
            droppingTiles.Remove(tile);
            yield break;
        }

        // Increment version counter - this lets us know if we're still the "owner" of this animation
        // If another animation starts for this tile, it will increment the version and we'll know not to clean up
        int myVersion = dropAnimationVersion.GetValueOrDefault(tile, 0) + 1;
        dropAnimationVersion[tile] = myVersion;

        // Track target position for Block Slip mechanic
        droppingTargets[tile] = toPos;

        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        int originalSortingOrder = sr != null ? sr.sortingOrder : 0;

        // Bring tile to front during drop to prevent clipping behind tiles below
        if (sr != null) sr.sortingOrder = 10;

        // Use the passed world position directly - no recalculation that causes snapping!
        Vector3 startWorldPos = fromWorldPos;
        Vector2Int currentTarget = toPos;

        // Calculate duration based on actual distance in world units
        float targetY = currentTarget.y * tileSize + gridRiser.CurrentGridOffset;
        float distance = Mathf.Abs(startWorldPos.y - targetY) / tileSize;
        float duration = dropDuration * distance; // Time per unit * distance
        float elapsed = 0;

        try
        {
            while (elapsed < duration && tile != null && droppingTiles.Contains(tile))
            {
                // Only check for obstructions if enabled (Block Slip scenarios)
                // Normal drops from DropTiles have pre-calculated non-conflicting paths
                if (checkForObstructions)
                {
                    // Only check if we're still more than 0.5 tiles away from target
                    // This avoids edge cases with floating point rounding near the destination
                    float currentWorldY = tile.transform.position.y;
                    float targetWorldY = currentTarget.y * tileSize + gridRiser.CurrentGridOffset;
                    float distanceToTarget = Mathf.Abs(currentWorldY - targetWorldY) / tileSize;
                    
                    if (distanceToTarget > 0.5f)
                    {
                        // Check if path is now obstructed (e.g., Block Slip placed something in our way)
                        Vector2Int? newTarget = CheckForPathObstruction(tile, currentTarget);
                        if (newTarget.HasValue && newTarget.Value.y != currentTarget.y)
                        {
                            // Verify the new target position is actually empty (or already ours)
                            GameObject atNewTarget = grid[newTarget.Value.x, newTarget.Value.y];
                            if (atNewTarget == null || atNewTarget == tile)
                            {
                                // Path is blocked! Update our target to land on top of the obstruction
                                Vector2Int oldTarget = currentTarget;
                                currentTarget = newTarget.Value;
                                
                                // Update grid array: remove from old target, place at new target
                                if (grid[oldTarget.x, oldTarget.y] == tile)
                                {
                                    grid[oldTarget.x, oldTarget.y] = null;
                                }
                                grid[currentTarget.x, currentTarget.y] = tile;
                                
                                // Update tile's grid coordinates
                                Tile tileScript = tile.GetComponent<Tile>();
                                tileScript.Initialize(currentTarget.x, currentTarget.y, tileScript.TileType, this);
                                
                                // Update tracking - mark as retargeted so tiles above us know to land on top
                                droppingTargets[tile] = currentTarget;
                                retargetedDrops.Add(tile);
                                
                                // Recalculate duration based on new shorter distance from CURRENT position
                                Vector3 currentPos = tile.transform.position;
                                float newTargetY = currentTarget.y * tileSize + gridRiser.CurrentGridOffset;
                                float remainingDistance = Mathf.Abs(currentPos.y - newTargetY) / tileSize;
                                
                                // Reset animation with current position as new start
                                startWorldPos = currentPos;
                                targetY = newTargetY;
                                duration = dropDuration * remainingDistance;
                                elapsed = 0;
                                
                                Debug.Log($"[Drop] Path obstructed! Retargeting from ({oldTarget.x}, {oldTarget.y}) to ({currentTarget.x}, {currentTarget.y})");
                            }
                        }
                    }
                }
                
                // Track progress for Block Slip mechanic
                float progress = duration > 0 ? elapsed / duration : 1f;
                droppingProgress[tile] = progress;

                // Calculate target position dynamically using current grid offset
                Vector3 targetWorldPos = new Vector3(currentTarget.x * tileSize, currentTarget.y * tileSize + gridRiser.CurrentGridOffset, 0);
                tile.transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, progress);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Final position snap - only if we're still the current animation
            if (tile != null && dropAnimationVersion.GetValueOrDefault(tile, 0) == myVersion)
            {
                tile.transform.position = new Vector3(currentTarget.x * tileSize, currentTarget.y * tileSize + gridRiser.CurrentGridOffset, 0);

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
            // Only clean up if we're still the current version (no new animation took over)
            if (dropAnimationVersion.GetValueOrDefault(tile, 0) == myVersion)
            {
                droppingTiles.Remove(tile);
                droppingProgress.Remove(tile);
                droppingTargets.Remove(tile);
                dropAnimationVersion.Remove(tile);
                retargetedDrops.Remove(tile);
                if (sr != null) sr.sortingOrder = originalSortingOrder;
            }
            // If version doesn't match, a new animation took over - don't touch anything
        }
    }

    /// <summary>
    /// Checks if there's now a block obstructing the path between the tile's current position and target.
    /// Returns a new target position (one above the obstruction) if blocked, or null if path is clear.
    /// </summary>
    Vector2Int? CheckForPathObstruction(GameObject tile, Vector2Int targetPos)
    {
        if (tile == null) return null;
        
        // Get current world Y and convert to grid Y
        float currentWorldY = tile.transform.position.y;
        int currentGridY = Mathf.RoundToInt((currentWorldY - gridRiser.CurrentGridOffset) / tileSize);
        
        // Clamp to valid range
        currentGridY = Mathf.Clamp(currentGridY, 0, gridHeight - 1);
        
        int x = targetPos.x;
        
        // Check from just below current position down to target
        // We loop top-to-bottom to find the HIGHEST obstruction first
        for (int y = currentGridY - 1; y >= targetPos.y; y--)
        {
            if (y >= 0 && y < gridHeight)
            {
                GameObject blockAtPos = grid[x, y];
                
                if (blockAtPos != null && blockAtPos != tile)
                {
                    bool isSolidObstruction = false;
                    
                    if (droppingTiles.Contains(blockAtPos))
                    {
                        // This block is dropping - only treat as obstruction if it was RETARGETED
                        // (Original drops from DropTiles were planned together and don't conflict)
                        if (retargetedDrops.Contains(blockAtPos))
                        {
                            // Retargeted tile - check if it's landing at or above this Y
                            if (droppingTargets.TryGetValue(blockAtPos, out Vector2Int otherTarget))
                            {
                                if (otherTarget.y >= y)
                                {
                                    isSolidObstruction = true;
                                }
                            }
                        }
                        // Non-retargeted dropping tiles are not obstructions
                    }
                    else
                    {
                        // Not dropping at all = solid obstruction (e.g., Block Slip placed it here)
                        isSolidObstruction = true;
                    }
                    
                    if (isSolidObstruction)
                    {
                        // Found an obstruction! Check if we can land on top of it
                        int newTargetY = y + 1;
                        if (newTargetY < gridHeight && newTargetY != targetPos.y)
                        {
                            // Verify the landing position is empty or already ours
                            GameObject atLandingPos = grid[x, newTargetY];
                            if (atLandingPos == null || atLandingPos == tile)
                            {
                                return new Vector2Int(x, newTargetY);
                            }
                            // If landing spot is taken by another tile, continue looking for lower obstruction
                        }
                    }
                }
            }
        }
        
        return null; // Path is clear
    }

    public IEnumerator DropTiles()
    {
        List<(GameObject tile, Vector2Int from, Vector2Int to)> drops = new List<(GameObject, Vector2Int, Vector2Int)>();
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
                // Update tile coordinates immediately
                Tile tileScript = tile.GetComponent<Tile>();
                tileScript.Initialize(to.x, to.y, tileScript.TileType, this);

                // Add to dropping set to protect from GridRiser updates
                droppingTiles.Add(tile);
                StartCoroutine(MoveTileDrop(tile, from, to));
            }
        }

        // Wait for the longest drop to complete (plus a small buffer)
        if (drops.Count > 0)
        {
            float waitTime = dropDuration * maxDropDistance + 0.05f;
            yield return new WaitForSeconds(waitTime);
        }
    }

    public void AddBreathingRoom(int tilesMatched)
    {
        gridRiser.AddBreathingRoom(tilesMatched);
    }
}