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
    /// Checks if a swap has placed blocks in the path of falling blocks and handles the merge.
    /// Should be called after swap animation completes but before DropTiles().
    /// Returns true if mid-air interception was handled.
    /// </summary>
    public bool HandleMidAirSwapInterception(GameObject leftTile, GameObject rightTile, Vector2Int leftPos, Vector2Int rightPos)
    {
        bool handledLeft = CheckAndHandleMidAirIntercept(leftTile, leftPos);
        bool handledRight = CheckAndHandleMidAirIntercept(rightTile, rightPos);
        return handledLeft || handledRight;
    }

    /// <summary>
    /// Check if a single swapped tile is in the path of falling blocks and handle cascade.
    /// </summary>
    private bool CheckAndHandleMidAirIntercept(GameObject swappedTile, Vector2Int swappedPos)
    {
        if (swappedTile == null) return false;

        int x = swappedPos.x;
        int y = swappedPos.y;

        // Collect all falling blocks in this column that are in/above the swapped block's position
        List<(GameObject tile, Vector2Int gridPos)> fallingBlocksAbove = new List<(GameObject, Vector2Int)>();

        for (int checkY = y; checkY < gridHeight; checkY++)
        {
            GameObject blockAtPos = grid[x, checkY];
            if (blockAtPos != null && blockAtPos != swappedTile && droppingTiles.Contains(blockAtPos))
            {
                fallingBlocksAbove.Add((blockAtPos, new Vector2Int(x, checkY)));
                Debug.Log($"[MidAirSwapIntercept] Found falling block at ({x}, {checkY})");
            }
        }

        // If no falling blocks above, no interception needed
        if (fallingBlocksAbove.Count == 0)
            return false;

        Debug.Log($"[MidAirSwapIntercept] Swapped block at ({x}, {y}) has {fallingBlocksAbove.Count} falling blocks above. Handling cascade.");

        // STOP all falling blocks and remove them from dropping state
        foreach (var (tile, gridPos) in fallingBlocksAbove)
        {
            droppingTiles.Remove(tile);
            droppingProgress.Remove(tile);
            droppingTargets.Remove(tile);
            dropAnimationVersion[tile] = GetVersion(tile) + 1; // Cancel their animations
            swappingTiles.Add(tile); // Protect them during cascade

            Tile ts = tile.GetComponent<Tile>();
            if (ts != null) ts.FinishMovement();
        }

        // CASCADE blocks upward to make room (top to bottom to avoid overwriting)
        for (int i = fallingBlocksAbove.Count - 1; i >= 0; i--)
        {
            var (tile, oldPos) = fallingBlocksAbove[i];
            int newY = oldPos.y + 1;

            if (newY >= gridHeight)
            {
                // Can't cascade higher, abort
                Debug.LogWarning($"[MidAirSwapIntercept] Cannot cascade block higher than grid! Aborting.");
                // Clean up swapping tiles protection
                foreach (var (t, p) in fallingBlocksAbove)
                {
                    swappingTiles.Remove(t);
                }
                return false;
            }

            Vector2Int newPos = new Vector2Int(x, newY);
            grid[newPos.x, newPos.y] = tile;
            grid[oldPos.x, oldPos.y] = null;

            Debug.Log($"[MidAirSwapIntercept] Cascading block from ({oldPos.x}, {oldPos.y}) to ({newPos.x}, {newY})");

            // Update the list for animation
            fallingBlocksAbove[i] = (tile, newPos);
        }

        // Now animate the cascaded blocks upward (quick nudge)
        foreach (var (tile, newPos) in fallingBlocksAbove)
        {
            StartCoroutine(MoveTileCascadeQuickNudge(tile, newPos));
        }

        return true;
    }

    /// <summary>
    /// Try to do a BlockSlip when exactly one cursor tile is falling and the other is idle.
    /// Kicks the idle tile under the falling one.
    /// </summary>
    public bool TryKickUnderFallingAtCursor(Vector2Int cursorPos)
    {
        int leftX = cursorPos.x;
        int rightX = cursorPos.x + 1;
        int y = cursorPos.y;

        GameObject leftTile = grid[leftX, y];
        GameObject rightTile = grid[rightX, y];

        Tile leftScript = leftTile != null ? leftTile.GetComponent<Tile>() : null;
        Tile rightScript = rightTile != null ? rightTile.GetComponent<Tile>() : null;

        bool leftFalling = leftScript != null && leftScript.State == Tile.MovementState.Falling;
        bool rightFalling = rightScript != null && rightScript.State == Tile.MovementState.Falling;

        bool leftIdle = leftScript != null && leftScript.State == Tile.MovementState.Idle;
        bool rightIdle = rightScript != null && rightScript.State == Tile.MovementState.Idle;

        // Only handle "exactly one falling, the other idle"
        if (leftFalling && rightIdle)
        {
            // Falling LEFT, stationary RIGHT -> kick RIGHT under left column
            StartCoroutine(HandleBlockSlip(rightTile, leftTile, new Vector2Int(leftX, y)));
            return true;
        }
        else if (rightFalling && leftIdle)
        {
            // Falling RIGHT, stationary LEFT -> kick LEFT under right column
            StartCoroutine(HandleBlockSlip(leftTile, rightTile, new Vector2Int(rightX, y)));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Try to do a BlockSlip when both cursor tiles are idle but there's a falling block
    /// higher up in one of the columns that will pass through this row.
    /// </summary>
    public bool TryHandleBlockSlipAtCursor(Vector2Int cursorPos)
    {
        int leftX = cursorPos.x;
        int rightX = cursorPos.x + 1;
        int y = cursorPos.y;

        GameObject leftTile = grid[leftX, y];
        GameObject rightTile = grid[rightX, y];

        Tile leftScript = leftTile != null ? leftTile.GetComponent<Tile>() : null;
        Tile rightScript = rightTile != null ? rightTile.GetComponent<Tile>() : null;

        bool leftIdle = leftScript == null || leftScript.State == Tile.MovementState.Idle;
        bool rightIdle = rightScript == null || rightScript.State == Tile.MovementState.Idle;

        // We only handle the "both idle" case here; falling case is TryKickUnderFallingAtCursor
        if (!leftIdle || !rightIdle)
            return false;

        GameObject fallingBlock;

        // Case: slip RIGHT tile into LEFT column (falling above in left column)
        if (rightTile != null &&
            FindFallingBlockPassingRowInColumn(leftX, y, out fallingBlock))
        {
            StartCoroutine(HandleBlockSlip(rightTile, fallingBlock, new Vector2Int(leftX, y)));
            return true;
        }

        // Case: slip LEFT tile into RIGHT column
        if (leftTile != null &&
            FindFallingBlockPassingRowInColumn(rightX, y, out fallingBlock))
        {
            StartCoroutine(HandleBlockSlip(leftTile, fallingBlock, new Vector2Int(rightX, y)));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Used by GridManager.SwapCursorTiles() for normal swapping.
    /// </summary>
    public void StartSwapAnimation(GameObject tile, Vector2Int targetPos)
    {
        if (tile == null) return;

        Tile ts = tile.GetComponent<Tile>();
        if (ts != null)
            ts.StartSwapping(targetPos);

        StartCoroutine(MoveTileSwap(tile, targetPos));
    }


    /// <summary>
    /// Used by GridManager.DropTiles() for normal drops (no obstruction checking).
    /// </summary>
    public void BeginDrop(GameObject tile, Vector2Int from, Vector2Int to)
    {
        if (tile == null) return;

        droppingTiles.Add(tile);

        // Tell the Tile it’s now falling
        Tile ts = tile.GetComponent<Tile>();
        if (ts != null)
            ts.StartFalling(to);

        // IMPORTANT: start from the tile’s current world position
        Vector3 fromWorldPos = tile.transform.position;
        StartCoroutine(MoveTileDrop(tile, fromWorldPos, to, false));
    }





    #endregion

    #region Block Slip Core

    /// <summary>
    /// Look for a falling block in the given column whose path will cross the given row.
    /// It uses the tile's current world height and its dropping target.
    /// </summary>
    private bool FindFallingBlockPassingRowInColumn(
        int columnX,
        int rowY,
        out GameObject fallingBlock)
    {
        fallingBlock = null;

        if (droppingTiles == null || droppingTiles.Count == 0)
            return false;

        float offsetY = gridRiser != null ? gridRiser.CurrentGridOffset : 0f;

        float bestDistance = float.MaxValue;

        foreach (var tile in droppingTiles)
        {
            if (tile == null) continue;

            if (!droppingTargets.TryGetValue(tile, out Vector2Int target))
                continue;

            if (target.x != columnX)
                continue;

            float currentWorldY = tile.transform.position.y;
            int currentGridY = Mathf.RoundToInt((currentWorldY - offsetY) / tileSize);

            // Must currently be above this row
            if (currentGridY <= rowY)
                continue;

            // Must eventually land at or below this row
            if (target.y > rowY)
                continue;

            float distance = currentGridY - rowY;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                fallingBlock = tile;
            }
        }

        return fallingBlock != null;
    }

    /// <summary>
    /// Executes the Block Slip mechanic.
    /// swappingBlock = the stationary block we kick under,
    /// fallingBlock  = any one of the blocks that was falling in that column
    /// (we don't actually need its original target here),
    /// fallingBlockPos = column and row where slip happens (cursor row).
    /// 
    /// Behaviour:
    /// - Insert swappingBlock at fallingBlockPos
    /// - All tiles in that column at/above that row get pushed UP by 1 cell
    /// - Then DropTiles() runs so the whole column falls together.
    /// </summary>
    private IEnumerator HandleBlockSlip(GameObject swappingBlock, GameObject fallingBlock, Vector2Int fallingBlockPos)
    {
        gridManager.SetIsSwapping(true);

        if (swappingBlock == null)
        {
            gridManager.SetIsSwapping(false);
            yield break;
        }

        int col = fallingBlockPos.x;
        int row = fallingBlockPos.y;

        // Where is the swapping block right now?
        Vector2Int? swappingBlockPosOpt = gridManager.FindTilePosition(swappingBlock);
        if (!swappingBlockPosOpt.HasValue)
        {
            gridManager.SetIsSwapping(false);
            yield break;
        }
        Vector2Int swappingBlockPos = swappingBlockPosOpt.Value;

        // 1. Collect every tile in this column at/above the slip row (excluding the swapping block).
        //    These will all be pushed UP by 1 row.
        List<(GameObject tile, Vector2Int from, Vector2Int to)> columnBlocks =
            new List<(GameObject, Vector2Int, Vector2Int)>();

        for (int y = row; y < gridHeight; y++)
        {
            GameObject t = grid[col, y];
            if (t == null || t == swappingBlock) continue;

            int newY = y + 1;
            if (newY >= gridHeight)
            {
                // Would overflow the grid - abort safely
                Debug.LogWarning("[BlockSlip] Cascade would overflow top of grid. Aborting slip.");
                gridManager.SetIsSwapping(false);
                yield break;
            }

            columnBlocks.Add((t, new Vector2Int(col, y), new Vector2Int(col, newY)));
        }

        // 2. Cancel any active drops on those column tiles so their old coroutines stop cleanly.
        foreach (var (tile, from, to) in columnBlocks)
        {
            if (tile == null) continue;

            droppingTiles.Remove(tile);
            droppingProgress.Remove(tile);
            droppingTargets.Remove(tile);

            dropAnimationVersion[tile] = GetVersion(tile) + 1;

            Tile ts = tile.GetComponent<Tile>();
            if (ts != null)
            {
                ts.FinishMovement(); // no longer "falling"
            }
        }

        // Also cancel the falling block's drop if it's still tracked
        if (fallingBlock != null && droppingTiles.Contains(fallingBlock))
        {
            droppingTiles.Remove(fallingBlock);
            droppingProgress.Remove(fallingBlock);
            droppingTargets.Remove(fallingBlock);
            dropAnimationVersion[fallingBlock] = GetVersion(fallingBlock) + 1;

            Tile fbTs = fallingBlock.GetComponent<Tile>();
            if (fbTs != null) fbTs.FinishMovement();
        }

        // 3. Update the grid:
        //    - clear old positions for the swapping block and all column tiles
        //    - place swapping block at slip row
        //    - place column tiles at their new (y+1) positions
        grid[swappingBlockPos.x, swappingBlockPos.y] = null;

        foreach (var (tile, from, to) in columnBlocks)
        {
            grid[from.x, from.y] = null;
        }

        // Place the kicked block in the slip cell
        grid[col, row] = swappingBlock;

        // Push everything above up by one
        foreach (var (tile, from, to) in columnBlocks)
        {
            grid[to.x, to.y] = tile;
        }

        // Mark as "protected" while we animate
        swappingTiles.Add(swappingBlock);
        foreach (var (tile, from, to) in columnBlocks)
        {
            if (tile != null) swappingTiles.Add(tile);
        }

        // 4. Animate:
        //    - horizontal swap of the kicked block into the column
        //    - quick upward nudge for the column tiles
        StartSwapAnimation(swappingBlock, fallingBlockPos);

        float maxCascadeDuration = 0f;
        foreach (var (tile, from, to) in columnBlocks)
        {
            if (tile == null) continue;

            // We'll reuse the cascade "smooth" animation to move them UP one row
            Vector3 startWorldPos = tile.transform.position;
            Vector3 targetWorldPos = new Vector3(
                to.x * tileSize,
                to.y * tileSize + gridRiser.CurrentGridOffset,
                0);

            float distance = Mathf.Abs(targetWorldPos.y - startWorldPos.y) / tileSize;
            float duration = dropDuration * distance;  // same speed as gravity
            maxCascadeDuration = Mathf.Max(maxCascadeDuration, duration);

            // Kick off the actual animation coroutine
            StartCoroutine(MoveTileCascadeSmooth(tile, to));
        }

        // Wait for both the swap and the cascade to finish
        float waitTime = Mathf.Max(swapDuration, maxCascadeDuration);
        if (waitTime > 0f)
            yield return new WaitForSeconds(waitTime);

        // 5. Let gravity resolve everything: all blocks above empty cells will fall together.
        //    This also fixes any tiny discrepancies that may have slipped through.
        swappingTiles.Remove(swappingBlock);
        foreach (var (tile, from, to) in columnBlocks)
        {
            swappingTiles.Remove(tile);
        }

        yield return gridManager.StartCoroutine(gridManager.DropTiles());

        // 6. Check for matches after the slip + gravity resolution
        List<GameObject> matches = matchDetector.GetAllMatches();
        if (matches.Count > 0 && !matchProcessor.IsProcessingMatches)
        {
            gridManager.StartCoroutine(matchProcessor.ProcessMatches(matches));
        }

        gridManager.SetIsSwapping(false);
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

        Tile ts = tile.GetComponent<Tile>();

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

        if (ts != null)
        {
            ts.FinishMovement();
        }

        if (sr != null) sr.sortingOrder = originalSortingOrder;

        if (tile != null)
        {
            Tile _ts = tile.GetComponent<Tile>();
            if (_ts != null)
                _ts.FinishMovement();
        }


    }

    private IEnumerator MoveTileCascadeSmooth(GameObject tile, Vector2Int toPos)
    {
        if (tile == null)
        {
            swappingTiles.Remove(tile);
            yield break;
        }

        Tile ts = tile.GetComponent<Tile>();

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
                if (ts != null)
                {
                    ts.Initialize(toPos.x, toPos.y, ts.TileType, gridManager);
                    ts.FinishMovement();
                }

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

        Tile ts = tile.GetComponent<Tile>();

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
                if (ts != null)
                {
                    ts.Initialize(toPos.x, toPos.y, ts.TileType, gridManager);
                    ts.FinishMovement();
                }

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

        Tile ts = tile.GetComponent<Tile>();

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

                                if (ts != null)
                                {
                                    ts.Initialize(currentTarget.x, currentTarget.y, ts.TileType, gridManager);
                                    ts.RetargetFall(currentTarget);
                                }

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

            if (tile != null &&
                dropAnimationVersion.TryGetValue(tile, out int latestVersion) &&
                latestVersion == myVersion)
            {
                tile.transform.position = new Vector3(currentTarget.x * tileSize, currentTarget.y * tileSize + gridRiser.CurrentGridOffset, 0);

                if (ts != null)
                {
                    ts.Initialize(currentTarget.x, currentTarget.y, ts.TileType, gridManager);
                    ts.FinishMovement();
                    ts.PlayLandSound();
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

                Tile _ts = tile.GetComponent<Tile>();
                if (_ts != null)
                    _ts.FinishMovement();
                    
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
