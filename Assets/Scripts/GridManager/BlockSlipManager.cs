using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles Block Slip behavior and all tile movement animations (swap, drop, cascades)
/// so GridManager can focus on grid state and match logic.
/// </summary>
public class BlockSlipManager : MonoBehaviour
{
    // Core references
    private GridManager gridManager;
    private GridRiser gridRiser;
    private MatchDetector matchDetector;
    private MatchProcessor matchProcessor;
    private CursorController cursorController;

    // Grid info
    private GameObject[,] grid;
    private int gridWidth;
    private int gridHeight;
    private float tileSize;

    // Timings
    private float swapDuration;
    private float dropDuration;

    // Shared animation sets (owned by GridManager, but we operate on them)
    private HashSet<GameObject> swappingTiles;
    private HashSet<GameObject> droppingTiles;

    // Block Slip tracking
    private Dictionary<GameObject, float> droppingProgress = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, Vector2Int> droppingTargets = new Dictionary<GameObject, Vector2Int>();
    private Dictionary<GameObject, int> dropAnimationVersion = new Dictionary<GameObject, int>();
    private HashSet<GameObject> retargetedDrops = new HashSet<GameObject>();

    #region Initialization / Maintenance

    public void Initialize(
        GridManager gridManager,
        GameObject[,] grid,
        int gridWidth,
        int gridHeight,
        float tileSize,
        float swapDuration,
        float dropDuration,
        GridRiser gridRiser,
        MatchDetector matchDetector,
        MatchProcessor matchProcessor,
        CursorController cursorController,
        HashSet<GameObject> swappingTiles,
        HashSet<GameObject> droppingTiles)
    {
        this.gridManager = gridManager;
        this.grid = grid;
        this.gridWidth = gridWidth;
        this.gridHeight = gridHeight;
        this.tileSize = tileSize;
        this.swapDuration = swapDuration;
        this.dropDuration = dropDuration;
        this.gridRiser = gridRiser;
        this.matchDetector = matchDetector;
        this.matchProcessor = matchProcessor;
        this.cursorController = cursorController;
        this.swappingTiles = swappingTiles;
        this.droppingTiles = droppingTiles;
    }

    /// <summary>
    /// Cleanup null entries in Block Slip tracking (call from GridManager.Update)
    /// </summary>
    public void CleanupTracking()
    {
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

        retargetedDrops.RemoveWhere(t => t == null);
    }

    #endregion

    #region Public API used by GridManager

    /// <summary>
    /// Optional: call each frame if you want the Block Slip indicator to update.
    /// </summary>
    public void UpdateBlockSlipIndicator()
    {
        if (gridManager.IsSwapping)
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

        bool leftSwapping = leftTile != null && swappingTiles.Contains(leftTile);
        bool rightSwapping = rightTile != null && swappingTiles.Contains(rightTile);
        bool leftDropping = leftTile != null && droppingTiles.Contains(leftTile);
        bool rightDropping = rightTile != null && droppingTiles.Contains(rightTile);

        bool canCheckBlockSlip = !leftSwapping && !rightSwapping;
        bool hasBlockSlipOpportunity = false;

        if (canCheckBlockSlip)
        {
            // Case 1: left dropping, right solid
            if (leftTile != null && leftDropping && rightTile != null && !rightDropping)
            {
                if (droppingTargets.TryGetValue(leftTile, out Vector2Int leftTarget))
                {
                    if (leftTarget.x == leftX && leftTarget.y == y)
                    {
                        float progress = CalculateProgressIntoDestinationTile(leftTile, leftTarget);
                        if (progress < 0.5f)
                            hasBlockSlipOpportunity = true;
                    }
                }
            }
            // Case 2: right dropping, left solid
            else if (rightTile != null && rightDropping && leftTile != null && !leftDropping)
            {
                if (droppingTargets.TryGetValue(rightTile, out Vector2Int rightTarget))
                {
                    if (rightTarget.x == rightX && rightTarget.y == y)
                    {
                        float progress = CalculateProgressIntoDestinationTile(rightTile, rightTarget);
                        if (progress < 0.5f)
                            hasBlockSlipOpportunity = true;
                    }
                }
            }
        }

        cursorController.SetBlockSlipIndicator(hasBlockSlipOpportunity);
    }

    /// <summary>
    /// Called from GridManager.HandleSwapInput() when swap button is pressed.
    /// Returns true if a Block Slip was triggered (and normal swap should be skipped).
    /// </summary>
    public bool TryHandleBlockSlipAtCursor(Vector2Int cursorPos)
    {
        int leftX = cursorPos.x;
        int rightX = cursorPos.x + 1;
        int y = cursorPos.y;

        GameObject leftTile = grid[leftX, y];
        GameObject rightTile = grid[rightX, y];

        if (leftTile == null && rightTile == null)
            return false;

        bool leftSwapping = leftTile != null && swappingTiles.Contains(leftTile);
        bool rightSwapping = rightTile != null && swappingTiles.Contains(rightTile);
        bool leftDropping = leftTile != null && droppingTiles.Contains(leftTile);
        bool rightDropping = rightTile != null && droppingTiles.Contains(rightTile);

        // Block Slip only if nothing is actively swapping
        if (leftSwapping || rightSwapping)
            return false;

        GameObject fallingBlock = null;
        GameObject swappingBlock = null;
        Vector2Int fallingBlockPos = Vector2Int.zero;
        bool canBlockSlip = false;

        // Case 1: Left tile is dropping, right solid
        if (leftTile != null && leftDropping && rightTile != null && !rightDropping)
        {
            if (droppingTargets.TryGetValue(leftTile, out Vector2Int leftTarget) &&
                leftTarget.x == leftX && leftTarget.y == y)
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
        // Case 2: Right tile is dropping, left solid
        else if (rightTile != null && rightDropping && leftTile != null && !leftDropping)
        {
            if (droppingTargets.TryGetValue(rightTile, out Vector2Int rightTarget) &&
                rightTarget.x == rightX && rightTarget.y == y)
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

        if (!canBlockSlip)
            return false;

        // Fire the Block Slip coroutine
        StartCoroutine(HandleBlockSlip(swappingBlock, fallingBlock, fallingBlockPos));
        return true;
    }

    /// <summary>
    /// Used by GridManager.SwapCursorTiles() for normal swapping.
    /// </summary>
    public void StartSwapAnimation(GameObject tile, Vector2Int targetPos)
    {
        if (tile == null) return;
        StartCoroutine(MoveTileSwap(tile, targetPos));
    }

    /// <summary>
    /// Used by GridManager.DropTiles() for normal drops (no obstruction checking).
    /// </summary>
    public void BeginDrop(GameObject tile, Vector2Int from, Vector2Int to)
    {
        if (tile == null) return;

        droppingTiles.Add(tile);
        Vector3 fromWorldPos = new Vector3(from.x * tileSize, from.y * tileSize + gridRiser.CurrentGridOffset, 0);
        StartCoroutine(MoveTileDrop(tile, fromWorldPos, to, false));
    }

    #endregion

    #region Block Slip Core

    /// <summary>
    /// Calculates how far (0-1) a falling tile has entered its destination tile.
    /// 0 = just entering top, 1 = fully landed.
    /// </summary>
    private float CalculateProgressIntoDestinationTile(GameObject tile, Vector2Int destinationPos)
    {
        if (tile == null) return 1f;

        float currentY = tile.transform.position.y;

        float destinationTop = (destinationPos.y + 1) * tileSize + gridRiser.CurrentGridOffset;
        float progressIntoTile = (destinationTop - currentY) / tileSize;
        return Mathf.Clamp01(progressIntoTile);
    }

    /// <summary>
    /// Executes the Block Slip mechanic and cascading.
    /// </summary>
    private IEnumerator HandleBlockSlip(GameObject swappingBlock, GameObject fallingBlock, Vector2Int fallingBlockPos)
    {
        gridManager.SetIsSwapping(true);

        // Find current grid position of swapping block
        Vector2Int? swappingBlockPos = gridManager.FindTilePosition(swappingBlock);
        if (!swappingBlockPos.HasValue)
        {
            gridManager.SetIsSwapping(false);
            yield break;
        }

        Vector2Int stationaryBlockOriginalPos = swappingBlockPos.Value;
        Vector2Int fallingBlockNewTarget = new Vector2Int(fallingBlockPos.x, fallingBlockPos.y + 1);

        // Collect tiles that need to cascade upward
        List<(GameObject tile, Vector2Int from, Vector2Int to)> cascadingBlocks =
            new List<(GameObject, Vector2Int, Vector2Int)>();

        Debug.Log($"[BlockSlip] Collecting cascading blocks in column {fallingBlockPos.x}");
        Debug.Log($"[BlockSlip] Falling block is at grid[{fallingBlockPos.x}, {fallingBlockPos.y}], will redirect to ({fallingBlockPos.x}, {fallingBlockNewTarget.y})");

        // Debug column contents
        Debug.Log($"[BlockSlip DEBUG] Checking entire column {fallingBlockPos.x}:");
        for (int debugY = 0; debugY < gridHeight; debugY++)
        {
            GameObject debugBlock = grid[fallingBlockPos.x, debugY];
            if (debugBlock != null)
            {
                bool isDropping = droppingTiles.Contains(debugBlock);
                bool isFallingBlock = debugBlock == fallingBlock;
                Vector2Int? dropTarget = droppingTargets.ContainsKey(debugBlock) ? droppingTargets[debugBlock] : (Vector2Int?)null;
                Vector3 worldPos = debugBlock.transform.position;
                Tile tileScript = debugBlock.GetComponent<Tile>();
                Vector2Int tileGridPos = new Vector2Int(tileScript.GridX, tileScript.GridY);
                Debug.Log($"[BlockSlip DEBUG] grid[{fallingBlockPos.x}, {debugY}] = {debugBlock.name}, WorldY={worldPos.y:F2}, TileGridPos={tileGridPos}, IsDropping={isDropping}, IsFallingBlock={isFallingBlock}, DropTarget={dropTarget}");
            }
        }

        // Collect falling blocks below slip point
        for (int y = 0; y < fallingBlockPos.y; y++)
        {
            GameObject blockBelow = grid[fallingBlockPos.x, y];
            if (blockBelow != null && droppingTiles.Contains(blockBelow))
            {
                int newY = y + 1;
                Debug.Log($"[BlockSlip] Found falling block BELOW slip point at grid ({fallingBlockPos.x}, {y}) -> ({fallingBlockPos.x}, {newY}). Will cascade up.");
                cascadingBlocks.Add((blockBelow, new Vector2Int(fallingBlockPos.x, y), new Vector2Int(fallingBlockPos.x, newY)));
            }
        }

        // Collect blocks at/above the new target
        int currentY = fallingBlockNewTarget.y;
        while (currentY < gridHeight)
        {
            GameObject blockAtPosition = grid[fallingBlockPos.x, currentY];

            if (blockAtPosition != null && blockAtPosition != fallingBlock)
            {
                int newY = currentY + 1;
                if (newY >= gridHeight)
                {
                    gridManager.SetIsSwapping(false);
                    yield break;
                }

                bool isDropping = droppingTiles.Contains(blockAtPosition);
                Debug.Log($"[BlockSlip] Found cascading block at grid ({fallingBlockPos.x}, {currentY}) -> ({fallingBlockPos.x}, {newY}). IsDropping={isDropping}");

                cascadingBlocks.Add((blockAtPosition, new Vector2Int(fallingBlockPos.x, currentY), new Vector2Int(fallingBlockPos.x, newY)));
                currentY++;
            }
            else
            {
                string reason = blockAtPosition == null ? "null" : "falling block";
                Debug.Log($"[BlockSlip] Stopping cascade collection at Y={currentY} ({reason})");
                break;
            }
        }

        Debug.Log($"[BlockSlip] Total cascading blocks: {cascadingBlocks.Count}");

        if (swappingBlock != null) swappingTiles.Add(swappingBlock);

        try
        {
            // 1. Stop falling block's animation
            droppingTiles.Remove(fallingBlock);
            droppingProgress.Remove(fallingBlock);
            droppingTargets.Remove(fallingBlock);

            dropAnimationVersion[fallingBlock] = GetVersion(fallingBlock) + 1;

            // 2. Stop and protect cascading blocks
            foreach (var (tile, from, to) in cascadingBlocks)
            {
                if (tile == null) continue;

                droppingTiles.Remove(tile);
                droppingProgress.Remove(tile);
                droppingTargets.Remove(tile);

                dropAnimationVersion[tile] = GetVersion(tile) + 1;
                swappingTiles.Add(tile);
            }

            // 3. Update grid for cascading blocks (top to bottom)
            for (int i = cascadingBlocks.Count - 1; i >= 0; i--)
            {
                var (tile, from, to) = cascadingBlocks[i];
                grid[to.x, to.y] = tile;
                grid[from.x, from.y] = null;
            }

            // 4. Update main swap positions
            grid[fallingBlockPos.x, fallingBlockPos.y] = swappingBlock;
            grid[stationaryBlockOriginalPos.x, stationaryBlockOriginalPos.y] = null;
            grid[fallingBlockNewTarget.x, fallingBlockNewTarget.y] = fallingBlock;

            // 4b. Animate swappingBlock into fallingBlock's destination
            StartSwapAnimation(swappingBlock, fallingBlockPos);

            // 5. Redirect falling block
            Vector3 fallingBlockCurrentWorldPos = fallingBlock.transform.position;
            float fallingBlockTargetY = fallingBlockNewTarget.y * tileSize + gridRiser.CurrentGridOffset;
            float fallingBlockDistance = Mathf.Abs(fallingBlockCurrentWorldPos.y - fallingBlockTargetY) / tileSize;
            float fallingBlockTravelTime = dropDuration * fallingBlockDistance;

            Tile fallingTileScript = fallingBlock.GetComponent<Tile>();
            fallingTileScript.Initialize(fallingBlockNewTarget.x, fallingBlockNewTarget.y, fallingTileScript.TileType, gridManager);

            droppingTiles.Add(fallingBlock);
            StartCoroutine(MoveTileDrop(fallingBlock, fallingBlockCurrentWorldPos, fallingBlockNewTarget, true));

            yield return null;

            // 7. Cascade blocks upward
            float maxCascadeDuration = 0f;
            List<(GameObject tile, Vector2Int to, bool useQuickNudge)> animationPlan =
                new List<(GameObject, Vector2Int, bool)>();

            foreach (var (tile, from, to) in cascadingBlocks)
            {
                if (tile == null) continue;

                Vector3 currentWorldPos = tile.transform.position;
                float currentWorldY = currentWorldPos.y;
                float targetWorldY = to.y * tileSize + gridRiser.CurrentGridOffset;

                Debug.Log($"[BlockSlip] Cascading tile at grid {from} -> {to}. CurrentWorldY={currentWorldY:F2}, TargetWorldY={targetWorldY:F2}");

                if (currentWorldY > targetWorldY)
                {
                    float remainingDistance = (currentWorldY - targetWorldY) / tileSize;
                    float cascadeDuration = dropDuration * remainingDistance;
                    maxCascadeDuration = Mathf.Max(maxCascadeDuration, cascadeDuration);

                    Debug.Log($"[BlockSlip] -> Using SMOOTH CASCADE (block above target). Distance={remainingDistance:F2} tiles, Duration={cascadeDuration:F2}s");
                    animationPlan.Add((tile, to, false));
                }
                else
                {
                    float nudgeDistance = Mathf.Abs(currentWorldY - targetWorldY) / tileSize;
                    float nudgeDuration = dropDuration * nudgeDistance * 0.5f;
                    maxCascadeDuration = Mathf.Max(maxCascadeDuration, nudgeDuration);

                    Debug.Log($"[BlockSlip] -> Using QUICK NUDGE (block at/below target). Distance={nudgeDistance:F2} tiles, Duration={nudgeDuration:F2}s");
                    animationPlan.Add((tile, to, true));
                }
            }

            // Sorting order control
            Dictionary<GameObject, int> originalSortingOrders = new Dictionary<GameObject, int>();
            for (int i = 0; i < animationPlan.Count; i++)
            {
                var (tile, to, useQuickNudge) = animationPlan[i];
                SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    originalSortingOrders[tile] = sr.sortingOrder;
                    sr.sortingOrder = 10 + i;
                }
            }

            // Start cascade animations
            foreach (var (tile, to, useQuickNudge) in animationPlan)
            {
                if (useQuickNudge)
                    StartCoroutine(MoveTileCascadeQuickNudge(tile, to));
                else
                    StartCoroutine(MoveTileCascadeSmooth(tile, to));
            }

            // Wait for swap animation
            yield return new WaitForSeconds(swapDuration);

            // Snap swapping block to final pos
            Tile swapTileScript = swappingBlock.GetComponent<Tile>();
            swapTileScript.Initialize(fallingBlockPos.x, fallingBlockPos.y, swapTileScript.TileType, gridManager);
            swappingBlock.transform.position = new Vector3(fallingBlockPos.x * tileSize, fallingBlockPos.y * tileSize + gridRiser.CurrentGridOffset, 0);

            // Wait for longest cascade / falling time
            float longestAnimationTime = Mathf.Max(fallingBlockTravelTime, maxCascadeDuration);
            yield return new WaitForSeconds(longestAnimationTime);

            // Restore sorting orders
            foreach (var kvp in originalSortingOrders)
            {
                GameObject tile = kvp.Key;
                int originalOrder = kvp.Value;
                if (tile != null)
                {
                    SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sortingOrder = originalOrder;
                }
            }

            // Additional drops + matches
            yield return gridManager.StartCoroutine(gridManager.DropTiles());

            List<GameObject> matches = matchDetector.GetAllMatches();
            if (matches.Count > 0 && !matchProcessor.IsProcessingMatches)
            {
                StartCoroutine(matchProcessor.ProcessMatches(matches));
            }
        }
        finally
        {
            swappingTiles.Remove(swappingBlock);
            gridManager.SetIsSwapping(false);
        }
    }

    private int GetVersion(GameObject tile)
    {
        return dropAnimationVersion.TryGetValue(tile, out int v) ? v : 0;
    }

    #endregion

    #region Movement Coroutines (Swap, Cascade, Drop)

    private IEnumerator MoveTileSwap(GameObject tile, Vector2Int targetPos)
    {
        if (tile == null) yield break;

        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        int originalSortingOrder = sr != null ? sr.sortingOrder : 0;

        if (sr != null) sr.sortingOrder = 10;

        Vector3 startPos = tile.transform.position;
        float duration = swapDuration;
        float elapsed = 0;

        while (elapsed < duration)
        {
            Vector3 targetWorldPos = new Vector3(targetPos.x * tileSize, targetPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
            tile.transform.position = Vector3.Lerp(startPos, targetWorldPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 finalPos = new Vector3(targetPos.x * tileSize, targetPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
        tile.transform.position = finalPos;

        if (sr != null) sr.sortingOrder = originalSortingOrder;
    }

    private IEnumerator MoveTileCascadeSmooth(GameObject tile, Vector2Int toPos)
    {
        if (tile == null)
        {
            swappingTiles.Remove(tile);
            yield break;
        }

        Vector3 startWorldPos = tile.transform.position;
        Vector3 targetWorldPos = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
        float distance = Mathf.Abs(targetWorldPos.y - startWorldPos.y) / tileSize;
        float duration = dropDuration * distance;
        float elapsed = 0;

        try
        {
            while (elapsed < duration && tile != null)
            {
                Vector3 calculatedWorldPos = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
                tile.transform.position = Vector3.Lerp(startWorldPos, calculatedWorldPos, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (tile != null)
            {
                Tile tileScript = tile.GetComponent<Tile>();
                tileScript.Initialize(toPos.x, toPos.y, tileScript.TileType, gridManager);

                tile.transform.position = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
            }
        }
        finally
        {
            swappingTiles.Remove(tile);
        }
    }

    private IEnumerator MoveTileCascadeQuickNudge(GameObject tile, Vector2Int toPos)
    {
        if (tile == null)
        {
            swappingTiles.Remove(tile);
            yield break;
        }

        Vector3 startWorldPos = tile.transform.position;
        Vector3 targetWorldPos = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);

        float distance = Mathf.Abs(targetWorldPos.y - startWorldPos.y) / tileSize;
        float duration = dropDuration * distance * 0.5f;
        float elapsed = 0;

        try
        {
            while (elapsed < duration && tile != null)
            {
                Vector3 calculatedWorldPos = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
                tile.transform.position = Vector3.Lerp(startWorldPos, calculatedWorldPos, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (tile != null)
            {
                Tile tileScript = tile.GetComponent<Tile>();
                tileScript.Initialize(toPos.x, toPos.y, tileScript.TileType, gridManager);

                tile.transform.position = new Vector3(toPos.x * tileSize, toPos.y * tileSize + gridRiser.CurrentGridOffset, 0);
            }
        }
        finally
        {
            swappingTiles.Remove(tile);
        }
    }

    private IEnumerator MoveTileDrop(GameObject tile, Vector3 fromWorldPos, Vector2Int toPos, bool checkForObstructions)
    {
        if (tile == null)
        {
            droppingTiles.Remove(tile);
            yield break;
        }

        int myVersion = GetVersion(tile) + 1;
        dropAnimationVersion[tile] = myVersion;

        droppingTargets[tile] = toPos;

        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
        int originalSortingOrder = sr != null ? sr.sortingOrder : 0;
        if (sr != null) sr.sortingOrder = 10;

        Vector3 startWorldPos = fromWorldPos;
        Vector2Int currentTarget = toPos;

        float targetY = currentTarget.y * tileSize + gridRiser.CurrentGridOffset;
        float distance = Mathf.Abs(startWorldPos.y - targetY) / tileSize;
        float duration = dropDuration * distance;
        float elapsed = 0;

        try
        {
            while (elapsed < duration && tile != null && droppingTiles.Contains(tile))
            {
                if (checkForObstructions)
                {
                    float currentWorldY = tile.transform.position.y;
                    float targetWorldY = currentTarget.y * tileSize + gridRiser.CurrentGridOffset;
                    float distanceToTarget = Mathf.Abs(currentWorldY - targetWorldY) / tileSize;

                    if (distanceToTarget > 0.5f)
                    {
                        Vector2Int? newTarget = CheckForPathObstruction(tile, currentTarget);
                        if (newTarget.HasValue && newTarget.Value.y != currentTarget.y)
                        {
                            GameObject atNewTarget = grid[newTarget.Value.x, newTarget.Value.y];
                            if (atNewTarget == null || atNewTarget == tile)
                            {
                                Vector2Int oldTarget = currentTarget;
                                currentTarget = newTarget.Value;

                                if (grid[oldTarget.x, oldTarget.y] == tile)
                                {
                                    grid[oldTarget.x, oldTarget.y] = null;
                                }
                                grid[currentTarget.x, currentTarget.y] = tile;

                                Tile tileScript = tile.GetComponent<Tile>();
                                tileScript.Initialize(currentTarget.x, currentTarget.y, tileScript.TileType, gridManager);

                                droppingTargets[tile] = currentTarget;
                                retargetedDrops.Add(tile);

                                Vector3 currentPos = tile.transform.position;
                                float newTargetY = currentTarget.y * tileSize + gridRiser.CurrentGridOffset;
                                float remainingDistance = Mathf.Abs(currentPos.y - newTargetY) / tileSize;

                                startWorldPos = currentPos;
                                targetY = newTargetY;
                                duration = dropDuration * remainingDistance;
                                elapsed = 0;

                                Debug.Log($"[Drop] Path obstructed! Retargeting from ({oldTarget.x}, {oldTarget.y}) to ({currentTarget.x}, {currentTarget.y})");
                            }
                        }
                    }
                }

                float progress = duration > 0 ? elapsed / duration : 1f;
                droppingProgress[tile] = progress;

                Vector3 targetWorldPos = new Vector3(currentTarget.x * tileSize, currentTarget.y * tileSize + gridRiser.CurrentGridOffset, 0);
                tile.transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, progress);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (tile != null && dropAnimationVersion.TryGetValue(tile, out int latestVersion) && latestVersion == myVersion)
            {
                tile.transform.position = new Vector3(currentTarget.x * tileSize, currentTarget.y * tileSize + gridRiser.CurrentGridOffset, 0);

                Tile tileScript = tile.GetComponent<Tile>();
                if (tileScript != null)
                {
                    tileScript.PlayLandSound();
                }

                if (sr != null) sr.sortingOrder = originalSortingOrder;
            }
        }
        finally
        {
            if (tile != null &&
                dropAnimationVersion.TryGetValue(tile, out int latestVersion) &&
                latestVersion == myVersion)
            {
                droppingTiles.Remove(tile);
                droppingProgress.Remove(tile);
                droppingTargets.Remove(tile);
                dropAnimationVersion.Remove(tile);
                retargetedDrops.Remove(tile);
                if (sr != null) sr.sortingOrder = originalSortingOrder;
            }
        }
    }

    /// <summary>
    /// Check for solid obstruction between tile's current height and its target.
    /// </summary>
    private Vector2Int? CheckForPathObstruction(GameObject tile, Vector2Int targetPos)
    {
        if (tile == null) return null;

        float currentWorldY = tile.transform.position.y;
        int currentGridY = Mathf.RoundToInt((currentWorldY - gridRiser.CurrentGridOffset) / tileSize);
        currentGridY = Mathf.Clamp(currentGridY, 0, gridHeight - 1);

        int x = targetPos.x;

        for (int y = currentGridY - 1; y >= targetPos.y; y--)
        {
            if (y < 0 || y >= gridHeight) continue;

            GameObject blockAtPos = grid[x, y];
            if (blockAtPos == null || blockAtPos == tile) continue;

            bool isSolidObstruction = false;

            if (droppingTiles.Contains(blockAtPos))
            {
                if (retargetedDrops.Contains(blockAtPos) &&
                    droppingTargets.TryGetValue(blockAtPos, out Vector2Int otherTarget) &&
                    otherTarget.y >= y)
                {
                    isSolidObstruction = true;
                }
            }
            else
            {
                isSolidObstruction = true;
            }

            if (isSolidObstruction)
            {
                int newTargetY = y + 1;
                if (newTargetY < gridHeight && newTargetY != targetPos.y)
                {
                    GameObject atLandingPos = grid[x, newTargetY];
                    if (atLandingPos == null || atLandingPos == tile)
                    {
                        return new Vector2Int(x, newTargetY);
                    }
                }
            }
        }

        return null;
    }

    #endregion
}
