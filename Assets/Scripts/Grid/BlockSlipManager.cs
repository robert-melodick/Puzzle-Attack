using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Handles BlockSlip mechanics, swap animations, and drop animations.
    /// Manages tile movement during swaps and falls including mid-air interception.
    /// </summary>
    public class BlockSlipManager : MonoBehaviour
    {
        #region Private Fields

        private GridManager _gridManager;
        private GameObject[,] _grid;
        private GridRiser _gridRiser;
        private MatchDetector _matchDetector;
        private MatchProcessor _matchProcessor;
        private CursorController _cursorController;

        private HashSet<GameObject> _swappingTiles;
        private HashSet<GameObject> _droppingTiles;

        private int _gridWidth;
        private int _gridHeight;
        private float _tileSize;
        private float _swapDuration;
        private float _dropSpeed; // tiles per second

        // Drop tracking
        private readonly Dictionary<GameObject, int> _dropAnimVersions = new();
        private readonly Dictionary<GameObject, float> _dropProgress = new();
        private readonly Dictionary<GameObject, Vector2Int> _dropTargets = new();
        private readonly HashSet<GameObject> _retargetedDrops = new();

        #endregion

        #region Tuning Parameters

        [Header("BlockSlip Thresholds")]
        [Tooltip("How much of the falling tile must be in the cursor row to allow swap (0.0-1.0). " +
                 "0.5 = tile center must be in row, 0.3 = more forgiving (late input), 0.7 = stricter (early input)")]
        [SerializeField, Range(0f, 1f)] 
        private float _swapWithFallingThreshold = 0.5f;

        [Tooltip("Threshold for blocking slide-under (original blockslip-too-late). " +
                 "Lower = more forgiving for slide-under attempts.")]
        [SerializeField, Range(0f, 1f)]
        private float _slideUnderBlockedThreshold = 0.4f;

        [Tooltip("Threshold for quick-nudge activation. Tile must be above this point in the row. " +
                 "Higher = stricter (must catch earlier).")]
        [SerializeField, Range(0f, 1f)]
        private float _quickNudgeThreshold = 0.5f;

        #endregion

        #region Initialization

        public void Initialize(
            GridManager gridManager,
            GameObject[,] grid,
            int gridWidth, int gridHeight, float tileSize,
            float swapDuration, float dropSpeed,
            GridRiser gridRiser,
            MatchDetector matchDetector,
            MatchProcessor matchProcessor,
            CursorController cursorController,
            HashSet<GameObject> swappingTiles,
            HashSet<GameObject> droppingTiles)
        {
            _gridManager = gridManager;
            _grid = grid;
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _tileSize = tileSize;
            _swapDuration = swapDuration;
            _dropSpeed = dropSpeed;
            _gridRiser = gridRiser;
            _matchDetector = matchDetector;
            _matchProcessor = matchProcessor;
            _cursorController = cursorController;
            _swappingTiles = swappingTiles;
            _droppingTiles = droppingTiles;
        }

        public void CleanupTracking()
        {
            var keysToRemove = new List<GameObject>();
            foreach (var key in _dropProgress.Keys)
                if (key == null)
                    keysToRemove.Add(key);

            foreach (var key in keysToRemove)
            {
                _dropProgress.Remove(key);
                _dropTargets.Remove(key);
                _dropAnimVersions.Remove(key);
            }

            _retargetedDrops.RemoveWhere(t => t == null);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Start a horizontal swap animation for a tile.
        /// </summary>
        public void StartSwapAnimation(GameObject tile, Vector2Int targetPos)
        {
            if (tile == null) return;

            var ts = tile.GetComponent<Tile>();
            ts?.StartSwapping(targetPos);

            StartCoroutine(AnimateSwap(tile, targetPos));
        }

        /// <summary>
        /// Begin a drop animation from current position to target.
        /// </summary>
        public void BeginDrop(GameObject tile, Vector2Int from, Vector2Int to)
        {
            if (tile == null) return;

            if (_droppingTiles.Contains(tile))
            {
                Debug.LogWarning($"[BeginDrop] {tile.name} already dropping from {from} to {to}");
                return;
            }

            _droppingTiles.Add(tile);

            var ts = tile.GetComponent<Tile>();
            ts?.StartFalling(to);

            var fromWorldPos = tile.transform.position;
            Debug.Log($"[BeginDrop] {tile.name} from Y:{fromWorldPos.y:F3} to grid ({to.x},{to.y})");

            StartCoroutine(AnimateDrop(tile, fromWorldPos, to, false));
        }

        /// <summary>
        /// Check if any tile is currently dropping toward this cell.
        /// </summary>
        public bool IsCellTargetedByDrop(int x, int y)
        {
            var targetPos = new Vector2Int(x, y);
            foreach (var kvp in _dropTargets)
            {
                if (kvp.Key != null && kvp.Value == targetPos)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if any falling block in the column has passed the slide-under threshold.
        /// </summary>
        public bool IsBlockSlipTooLate(int columnX, int rowY)
        {
            if (_droppingTiles.Count == 0) return false;

            var offsetY = _gridRiser?.CurrentGridOffset ?? 0f;
            var swapRowWorldY = rowY * _tileSize + offsetY;
            var swapRowThreshold = swapRowWorldY + _tileSize * _slideUnderBlockedThreshold;
            var swapRowTop = swapRowWorldY + _tileSize;

            foreach (var tile in _droppingTiles)
            {
                if (tile == null) continue;
                if (!_dropTargets.TryGetValue(tile, out var target) || target.x != columnX) continue;
                if (target.y > rowY) continue;

                var tileWorldY = tile.transform.position.y;
                if (tileWorldY >= swapRowWorldY && tileWorldY < swapRowTop && tileWorldY < swapRowThreshold)
                {
                    Debug.Log($"[BlockSlip] TOO LATE: Tile at Y:{tileWorldY:F2} past threshold {swapRowThreshold:F2}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Try to swap an idle tile with a falling tile at cursor position.
        /// Returns true if the swap was initiated.
        /// </summary>
        public bool TrySwapWithFallingTile(Vector2Int cursorPos)
        {
            var leftX = cursorPos.x;
            var rightX = cursorPos.x + 1;
            var y = cursorPos.y;

            var leftTile = _grid[leftX, y];
            var rightTile = _grid[rightX, y];

            var leftScript = leftTile?.GetComponent<Tile>();
            var rightScript = rightTile?.GetComponent<Tile>();

            var leftFalling = leftScript != null && leftScript.IsFalling;
            var rightFalling = rightScript != null && rightScript.IsFalling;
            var leftIdle = leftScript == null || leftScript.IsIdle;
            var rightIdle = rightScript == null || rightScript.IsIdle;

            // Check for falling tile in cursor row that we can swap with
            if (leftFalling && rightIdle && rightTile != null)
            {
                if (IsTileInSwapRange(leftTile, y))
                {
                    StartCoroutine(ExecuteSwapWithFalling(rightTile, leftTile, 
                        new Vector2Int(rightX, y), new Vector2Int(leftX, y)));
                    return true;
                }
            }

            if (rightFalling && leftIdle && leftTile != null)
            {
                if (IsTileInSwapRange(rightTile, y))
                {
                    StartCoroutine(ExecuteSwapWithFalling(leftTile, rightTile,
                        new Vector2Int(leftX, y), new Vector2Int(rightX, y)));
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Try to kick an idle tile under a falling tile at cursor.
        /// </summary>
        public bool TryKickUnderFallingAtCursor(Vector2Int cursorPos)
        {
            var leftX = cursorPos.x;
            var rightX = cursorPos.x + 1;
            var y = cursorPos.y;

            // First try to swap with falling tile directly
            if (TrySwapWithFallingTile(cursorPos))
                return true;

            if (IsBlockSlipTooLate(leftX, y) || IsBlockSlipTooLate(rightX, y))
            {
                Debug.Log(">>> BLOCKSLIP BLOCKED - past slide-under threshold <<<");
                return false;
            }

            var leftTile = _grid[leftX, y];
            var rightTile = _grid[rightX, y];

            var leftScript = leftTile?.GetComponent<Tile>();
            var rightScript = rightTile?.GetComponent<Tile>();

            var leftFalling = leftScript != null && leftScript.IsFalling;
            var rightFalling = rightScript != null && rightScript.IsFalling;
            var leftIdle = leftScript != null && leftScript.IsIdle;
            var rightIdle = rightScript != null && rightScript.IsIdle;

            if (leftFalling && rightIdle)
            {
                StartCoroutine(ExecuteBlockSlip(rightTile, leftTile, new Vector2Int(leftX, y)));
                return true;
            }

            if (rightFalling && leftIdle)
            {
                StartCoroutine(ExecuteBlockSlip(leftTile, rightTile, new Vector2Int(rightX, y)));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Try to handle BlockSlip when both cursor tiles are idle but there's a falling block above.
        /// </summary>
        public bool TryHandleBlockSlipAtCursor(Vector2Int cursorPos)
        {
            var leftX = cursorPos.x;
            var rightX = cursorPos.x + 1;
            var y = cursorPos.y;

            if (IsBlockSlipTooLate(leftX, y) || IsBlockSlipTooLate(rightX, y))
                return false;

            var leftTile = _grid[leftX, y];
            var rightTile = _grid[rightX, y];

            var leftScript = leftTile?.GetComponent<Tile>();
            var rightScript = rightTile?.GetComponent<Tile>();

            var leftIdle = leftScript == null || leftScript.IsIdle;
            var rightIdle = rightScript == null || rightScript.IsIdle;

            if (!leftIdle || !rightIdle) return false;

            // Check for falling block above in either column
            if (rightTile != null && FindFallingBlockInColumn(leftX, y, out var fallingBlock))
            {
                StartCoroutine(ExecuteBlockSlip(rightTile, fallingBlock, new Vector2Int(leftX, y)));
                return true;
            }

            if (leftTile != null && FindFallingBlockInColumn(rightX, y, out fallingBlock))
            {
                StartCoroutine(ExecuteBlockSlip(leftTile, fallingBlock, new Vector2Int(rightX, y)));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handle mid-air swap interception - when a swap places blocks in path of falling blocks.
        /// </summary>
        public bool HandleMidAirSwapInterception(GameObject leftTile, GameObject rightTile, 
            Vector2Int leftPos, Vector2Int rightPos)
        {
            var handledLeft = CheckMidAirIntercept(leftTile, leftPos);
            var handledRight = CheckMidAirIntercept(rightTile, rightPos);
            return handledLeft || handledRight;
        }

        #endregion

        #region Swap With Falling Tile

        /// <summary>
        /// Check if a falling tile is within the swappable range of the cursor row.
        /// </summary>
        private bool IsTileInSwapRange(GameObject tile, int rowY)
        {
            if (tile == null) return false;

            var offsetY = _gridRiser?.CurrentGridOffset ?? 0f;
            var tileWorldY = tile.transform.position.y;
            
            var swapRowWorldY = rowY * _tileSize + offsetY;
            var swapRowTop = swapRowWorldY + _tileSize;
            
            // Check if tile is in the row at all
            if (tileWorldY < swapRowWorldY || tileWorldY >= swapRowTop)
                return false;

            // Check threshold - tile must be above threshold point to be swappable
            var thresholdY = swapRowWorldY + _tileSize * (1f - _swapWithFallingThreshold);
            
            Debug.Log($"[SwapRange] Tile Y:{tileWorldY:F2}, Row:{swapRowWorldY:F2}-{swapRowTop:F2}, Threshold:{thresholdY:F2}, InRange:{tileWorldY >= thresholdY}");
            
            return tileWorldY >= thresholdY;
        }

        /// <summary>
        /// Execute a swap between an idle tile and a falling tile.
        /// The idle tile takes the falling tile's position in the stack.
        /// Tiles above the falling tile get nudged up, tiles below continue falling.
        /// </summary>
        private IEnumerator ExecuteSwapWithFalling(GameObject idleTile, GameObject fallingTile,
            Vector2Int idlePos, Vector2Int fallingPos)
        {
            _gridManager.SetIsSwapping(true);
            Debug.Log($"[SwapWithFalling] START: Idle {idleTile.name} at ({idlePos.x},{idlePos.y}) <-> Falling {fallingTile.name} at grid ({fallingPos.x},{fallingPos.y})");

            var fallingCol = fallingPos.x;
            var swapRow = fallingPos.y;
            var offsetY = _gridRiser?.CurrentGridOffset ?? 0f;
            var swapRowWorldY = swapRow * _tileSize + offsetY;

            // --- PHASE 1: Snapshot and categorize all tiles ---
            
            // Freeze all dropping tiles in this column to get consistent positions
            var frozenPositions = new Dictionary<GameObject, float>();
            foreach (var tile in _droppingTiles)
            {
                if (tile == null) continue;
                if (!_dropTargets.TryGetValue(tile, out var target) || target.x != fallingCol) continue;
                
                frozenPositions[tile] = tile.transform.position.y;
                _dropAnimVersions[tile] = GetVersion(tile) + 1; // Freeze animation
            }

            // Categorize: tiles that need to be nudged up vs tiles below that continue falling
            var tilesToNudge = new List<(GameObject tile, Vector2Int gridPos, float worldY)>();
            var tilesBelow = new List<(GameObject tile, Vector2Int target)>();

            // First, add the falling tiles (excluding the one we're swapping with)
            foreach (var tile in new List<GameObject>(_droppingTiles))
            {
                if (tile == null || tile == fallingTile) continue;
                if (!_dropTargets.TryGetValue(tile, out var target) || target.x != fallingCol) continue;

                var tileWorldY = frozenPositions.GetValueOrDefault(tile, tile.transform.position.y);

                if (tileWorldY >= swapRowWorldY)
                {
                    // This tile is at or above the swap row - needs to be nudged
                    tilesToNudge.Add((tile, target, tileWorldY));
                    Debug.Log($"[SwapWithFalling] Falling tile to nudge: {tile.name} at worldY:{tileWorldY:F2}, target:({target.x},{target.y})");
                }
                else
                {
                    // This tile is below - continues falling
                    tilesBelow.Add((tile, target));
                    Debug.Log($"[SwapWithFalling] Falling tile below (continues): {tile.name} at worldY:{tileWorldY:F2}");
                }
            }

            // Also find stationary tiles at or above the swap row in the falling column
            for (var y = swapRow; y < _gridHeight; y++)
            {
                var tile = _grid[fallingCol, y];
                if (tile == null || tile == fallingTile || tile == idleTile) continue;
                if (tilesToNudge.Exists(t => t.tile == tile)) continue; // Already added as falling
                if (_swappingTiles.Contains(tile)) continue;
                
                var tileWorldY = tile.transform.position.y;
                tilesToNudge.Add((tile, new Vector2Int(fallingCol, y), tileWorldY));
                Debug.Log($"[SwapWithFalling] Stationary tile to nudge: {tile.name} at grid ({fallingCol},{y})");
            }

            // Sort by world Y (lowest first) so we process from bottom to top
            tilesToNudge.Sort((a, b) => a.worldY.CompareTo(b.worldY));

            Debug.Log($"[SwapWithFalling] Total: {tilesToNudge.Count} tiles to nudge, {tilesBelow.Count} tiles below");

            // --- PHASE 2: Check for grid overflow ---
            var highestNudgeTarget = swapRow;
            foreach (var (tile, gridPos, worldY) in tilesToNudge)
            {
                highestNudgeTarget++;
            }
            
            if (highestNudgeTarget >= _gridHeight)
            {
                Debug.LogWarning($"[SwapWithFalling] Would overflow grid (need row {highestNudgeTarget}), aborting");
                
                // Unfreeze animations
                foreach (var tile in frozenPositions.Keys)
                {
                    if (tile != null && _droppingTiles.Contains(tile))
                    {
                        var target = _dropTargets.GetValueOrDefault(tile, Vector2Int.zero);
                        StartCoroutine(AnimateDrop(tile, tile.transform.position, target, true));
                    }
                }
                
                _gridManager.SetIsSwapping(false);
                yield break;
            }

            // --- PHASE 3: Cancel drops for tiles being nudged ---
            CancelDrop(fallingTile);
            
            foreach (var (tile, gridPos, worldY) in tilesToNudge)
            {
                CancelDrop(tile);
            }

            // --- PHASE 4: Update grid positions ---
            
            // Clear all old positions first
            _grid[idlePos.x, idlePos.y] = null;
            
            // Clear the falling tile's grid position (it might be at its target, not swapRow)
            var fallingTileGridPos = _gridManager.FindTilePosition(fallingTile);
            if (fallingTileGridPos.HasValue)
                _grid[fallingTileGridPos.Value.x, fallingTileGridPos.Value.y] = null;
            
            // Clear positions of tiles to be nudged
            foreach (var (tile, gridPos, worldY) in tilesToNudge)
            {
                var actualPos = _gridManager.FindTilePosition(tile);
                if (actualPos.HasValue)
                    _grid[actualPos.Value.x, actualPos.Value.y] = null;
            }

            // Place the idle tile at the swap row
            _grid[fallingCol, swapRow] = idleTile;
            Debug.Log($"[SwapWithFalling] Placed idle tile at ({fallingCol},{swapRow})");
            
            // Place the falling tile at the idle position
            _grid[idlePos.x, idlePos.y] = fallingTile;
            Debug.Log($"[SwapWithFalling] Placed falling tile at ({idlePos.x},{idlePos.y})");

            // Place nudged tiles at their new positions (each one row higher than previous)
            var nextNudgeRow = swapRow + 1;
            var nudgePlan = new List<(GameObject tile, Vector2Int newPos)>();
            
            foreach (var (tile, oldGridPos, worldY) in tilesToNudge)
            {
                var newPos = new Vector2Int(fallingCol, nextNudgeRow);
                _grid[newPos.x, newPos.y] = tile;
                nudgePlan.Add((tile, newPos));
                Debug.Log($"[SwapWithFalling] Nudging {tile.name} to ({newPos.x},{newPos.y})");
                nextNudgeRow++;
            }

            // --- PHASE 5: Mark tiles and start animations ---
            
            _swappingTiles.Add(idleTile);
            _swappingTiles.Add(fallingTile);
            foreach (var (tile, newPos) in nudgePlan)
                _swappingTiles.Add(tile);

            // Start swap animations
            var idleTileScript = idleTile.GetComponent<Tile>();
            var fallingTileScript = fallingTile.GetComponent<Tile>();

            idleTileScript?.StartSwapping(new Vector2Int(fallingCol, swapRow));
            fallingTileScript?.StartSwapping(idlePos);

            StartCoroutine(AnimateSwap(idleTile, new Vector2Int(fallingCol, swapRow)));
            StartCoroutine(AnimateSwap(fallingTile, idlePos));

            // Start nudge animations
            var maxNudgeDuration = 0f;
            foreach (var (tile, newPos) in nudgePlan)
            {
                var ts = tile.GetComponent<Tile>();
                ts?.Initialize(newPos.x, newPos.y, ts.TileType, _gridManager);
                
                StartCoroutine(AnimateCascadeSmooth(tile, newPos));
                
                var startY = tile.transform.position.y;
                var endY = newPos.y * _tileSize + offsetY;
                var distance = Mathf.Abs(endY - startY) / _tileSize;
                maxNudgeDuration = Mathf.Max(maxNudgeDuration, distance / _dropSpeed);
            }

            // Unfreeze tiles below - they continue their drops
            foreach (var (tile, target) in tilesBelow)
            {
                if (tile == null || !_droppingTiles.Contains(tile)) continue;
                
                var currentPos = tile.transform.position;
                _dropAnimVersions[tile] = GetVersion(tile) + 1;
                StartCoroutine(AnimateDrop(tile, currentPos, target, true));
            }

            // --- PHASE 6: Wait for swap animation only ---
            yield return new WaitForSeconds(_swapDuration);

            // --- PHASE 7: Finalize positions ---
            
            // Finalize idle tile (now in falling column at swap row)
            if (idleTile != null)
            {
                var finalOffsetY = _gridRiser?.CurrentGridOffset ?? 0f;
                idleTileScript?.Initialize(fallingCol, swapRow, idleTileScript.TileType, _gridManager);
                idleTileScript?.FinishMovement();
                idleTile.transform.position = new Vector3(
                    fallingCol * _tileSize,
                    swapRow * _tileSize + finalOffsetY,
                    0);
                
                // Verify grid consistency
                if (_grid[fallingCol, swapRow] != idleTile)
                {
                    Debug.LogError($"[SwapWithFalling] DESYNC: Grid at ({fallingCol},{swapRow}) is not idleTile! Fixing...");
                    _grid[fallingCol, swapRow] = idleTile;
                }
            }

            // Finalize falling tile (now in idle column)
            if (fallingTile != null)
            {
                var finalOffsetY = _gridRiser?.CurrentGridOffset ?? 0f;
                fallingTileScript?.Initialize(idlePos.x, idlePos.y, fallingTileScript.TileType, _gridManager);
                fallingTileScript?.FinishMovement();
                fallingTile.transform.position = new Vector3(
                    idlePos.x * _tileSize,
                    idlePos.y * _tileSize + finalOffsetY,
                    0);
            }

            // --- PHASE 8: Cleanup ---
            _swappingTiles.Remove(idleTile);
            _swappingTiles.Remove(fallingTile);
            // Note: nudged tiles are removed from _swappingTiles in AnimateCascadeSmooth's finally block

            _gridManager.SetIsSwapping(false);

            // --- PHASE 9: Post-swap drop checks (non-blocking) ---
            
            // Check if swapped tiles need to drop - initiate drops but don't wait
            CheckAndStartDrop(idleTile, fallingCol, swapRow);
            CheckAndStartDrop(fallingTile, idlePos.x, idlePos.y);

            // Trigger general drop logic without waiting for completion
            // This allows the game to continue while drops animate
            _gridManager.StartCoroutine(_gridManager.DropTiles());

            // Check for matches (will run after drops complete due to DropTiles' internal logic)
            var matches = _matchDetector.GetAllMatches();
            if (matches.Count > 0 && !_matchProcessor.IsProcessingMatches)
                _gridManager.StartCoroutine(_matchProcessor.ProcessMatches(matches));
            
            Debug.Log("[SwapWithFalling] COMPLETE");
        }

        /// <summary>
        /// Check if a tile needs to drop after being placed, and initiate drop if needed.
        /// Non-blocking version - just starts the drop, doesn't wait.
        /// </summary>
        private void CheckAndStartDrop(GameObject tile, int x, int y)
        {
            if (tile == null) return;
            if (_droppingTiles.Contains(tile)) return;

            // Find lowest empty position or position above a dropping tile
            var targetY = y;
            
            for (var checkY = y - 1; checkY >= 0; checkY--)
            {
                var below = _grid[x, checkY];
                
                if (below == null)
                {
                    targetY = checkY;
                }
                else if (_droppingTiles.Contains(below))
                {
                    // There's a dropping tile below - we should drop to just above its target
                    if (_dropTargets.TryGetValue(below, out var belowTarget))
                    {
                        targetY = belowTarget.y + 1;
                    }
                    break;
                }
                else
                {
                    // Solid tile below
                    break;
                }
            }

            if (targetY < y)
            {
                Debug.Log($"[CheckAndStartDrop] {tile.name} needs to drop from ({x},{y}) to ({x},{targetY})");
                
                // Update grid
                _grid[x, y] = null;
                _grid[x, targetY] = tile;

                var ts = tile.GetComponent<Tile>();
                ts?.Initialize(x, targetY, ts.TileType, _gridManager);

                BeginDrop(tile, new Vector2Int(x, y), new Vector2Int(x, targetY));
            }
        }

        /// <summary>
        /// Cancel an active drop animation for a tile.
        /// </summary>
        private void CancelDrop(GameObject tile)
        {
            if (tile == null) return;
            
            _droppingTiles.Remove(tile);
            _dropProgress.Remove(tile);
            _dropTargets.Remove(tile);
            _dropAnimVersions[tile] = GetVersion(tile) + 1;
            
            tile.GetComponent<Tile>()?.FinishMovement();
        }

        #endregion

        #region BlockSlip Core

        private bool FindFallingBlockInColumn(int columnX, int rowY, out GameObject fallingBlock)
        {
            fallingBlock = null;
            if (_droppingTiles.Count == 0) return false;

            var offsetY = _gridRiser?.CurrentGridOffset ?? 0f;
            var bestDistance = float.MaxValue;

            foreach (var tile in _droppingTiles)
            {
                if (tile == null) continue;
                if (!_dropTargets.TryGetValue(tile, out var target) || target.x != columnX) continue;

                var currentWorldY = tile.transform.position.y;
                var currentGridY = Mathf.RoundToInt((currentWorldY - offsetY) / _tileSize);

                if (currentGridY <= rowY || target.y > rowY) continue;

                var distance = currentGridY - rowY;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    fallingBlock = tile;
                }
            }

            return fallingBlock != null;
        }

        private IEnumerator ExecuteBlockSlip(GameObject swappingBlock, GameObject fallingBlock, Vector2Int slipPos)
        {
            _gridManager.SetIsSwapping(true);

            if (swappingBlock == null)
            {
                _gridManager.SetIsSwapping(false);
                yield break;
            }

            var col = slipPos.x;
            var row = slipPos.y;

            var swappingBlockPos = _gridManager.FindTilePosition(swappingBlock);
            if (!swappingBlockPos.HasValue)
            {
                _gridManager.SetIsSwapping(false);
                yield break;
            }

            // Collect blocks that need handling
            var blocksToNudgeUp = new List<(GameObject tile, Vector2Int from, Vector2Int to)>();
            var blocksToRetarget = new List<(GameObject tile, Vector2Int currentTarget)>();
            var processedBlocks = new HashSet<GameObject>();

            var swapRowWorldY = row * _tileSize + _gridRiser.CurrentGridOffset;
            var swapRowQuickNudgeThreshold = swapRowWorldY + _tileSize * _quickNudgeThreshold;

            // Snapshot falling block positions
            var snapshotPositions = new Dictionary<GameObject, float>();
            foreach (var t in _droppingTiles)
            {
                if (t != null && t != swappingBlock && _dropTargets.TryGetValue(t, out var target) && target.x == col)
                {
                    snapshotPositions[t] = t.transform.position.y;
                    _dropAnimVersions[t] = GetVersion(t) + 1; // Freeze animation
                }
            }

            // Categorize falling blocks
            foreach (var t in _droppingTiles)
            {
                if (t == null || t == swappingBlock) continue;
                if (!_dropTargets.TryGetValue(t, out var target) || target.x != col) continue;

                var tileWorldY = snapshotPositions.GetValueOrDefault(t, t.transform.position.y);
                var currentGridY = Mathf.RoundToInt((tileWorldY - _gridRiser.CurrentGridOffset) / _tileSize);

                if (currentGridY < row) continue;

                var swapRowTop = swapRowWorldY + _tileSize;
                var isInSwapRow = tileWorldY >= swapRowWorldY && tileWorldY < swapRowTop;

                if (isInSwapRow)
                {
                    if (tileWorldY < swapRowQuickNudgeThreshold)
                        blocksToRetarget.Add((t, target));
                    else
                    {
                        var gridPos = _gridManager.FindTilePosition(t);
                        if (gridPos.HasValue)
                            blocksToNudgeUp.Add((t, gridPos.Value, new Vector2Int(col, gridPos.Value.y + 1)));
                    }
                }
                else if (tileWorldY >= swapRowTop)
                    blocksToRetarget.Add((t, target));

                processedBlocks.Add(t);
            }

            // Add stationary blocks above swap row
            for (var y = row; y < _gridHeight; y++)
            {
                var t = _grid[col, y];
                if (t == null || t == swappingBlock || processedBlocks.Contains(t)) continue;

                var newY = y + 1;
                if (newY >= _gridHeight)
                {
                    Debug.LogWarning("[BlockSlip] Would overflow grid, aborting");
                    _gridManager.SetIsSwapping(false);
                    yield break;
                }

                blocksToNudgeUp.Add((t, new Vector2Int(col, y), new Vector2Int(col, newY)));
            }

            // Cancel drops for nudged blocks
            foreach (var (tile, from, to) in blocksToNudgeUp)
            {
                if (tile == null) continue;
                CancelDrop(tile);
            }

            // Cancel falling block's drop if tracked
            if (fallingBlock != null && _droppingTiles.Contains(fallingBlock))
            {
                CancelDrop(fallingBlock);
            }

            // Update grid: clear old positions
            _grid[swappingBlockPos.Value.x, swappingBlockPos.Value.y] = null;
            foreach (var (tile, from, to) in blocksToNudgeUp)
                _grid[from.x, from.y] = null;

            // Place swapping block and nudged blocks
            _grid[col, row] = swappingBlock;
            foreach (var (tile, from, to) in blocksToNudgeUp)
                _grid[to.x, to.y] = tile;

            // Mark as protected
            _swappingTiles.Add(swappingBlock);
            foreach (var (tile, from, to) in blocksToNudgeUp)
                if (tile != null)
                    _swappingTiles.Add(tile);

            // Start animations
            StartSwapAnimation(swappingBlock, slipPos);

            var maxCascadeDuration = 0f;
            foreach (var (tile, from, to) in blocksToNudgeUp)
            {
                if (tile == null) continue;
                var distance = Mathf.Abs(to.y - from.y);
                maxCascadeDuration = Mathf.Max(maxCascadeDuration, distance / _dropSpeed);
                StartCoroutine(AnimateCascadeSmooth(tile, to));
            }

            // Handle retargeted blocks
            blocksToRetarget.Sort((a, b) => 
                a.tile.transform.position.y.CompareTo(b.tile.transform.position.y));

            var nextRow = row;
            var retargetPlan = new List<(GameObject tile, Vector2Int oldTarget, Vector2Int newTarget)>();

            foreach (var (tile, oldTarget) in blocksToRetarget)
            {
                if (tile == null) continue;
                nextRow++;
                retargetPlan.Add((tile, oldTarget, new Vector2Int(col, nextRow)));
            }

            // Clear old grid positions for retargeted blocks
            foreach (var (tile, oldTarget, newTarget) in retargetPlan)
                if (_grid[oldTarget.x, oldTarget.y] == tile)
                    _grid[oldTarget.x, oldTarget.y] = null;

            // Set new grid positions
            foreach (var (tile, oldTarget, newTarget) in retargetPlan)
                _grid[newTarget.x, newTarget.y] = tile;

            // Start retargeted drops
            var maxRetargetDuration = 0f;
            foreach (var (tile, oldTarget, newTarget) in retargetPlan)
            {
                _dropAnimVersions[tile] = GetVersion(tile) + 1;
                _droppingTiles.Remove(tile);
                _dropProgress.Remove(tile);
                _dropTargets.Remove(tile);

                var ts = tile.GetComponent<Tile>();
                ts?.Initialize(newTarget.x, newTarget.y, ts.TileType, _gridManager);
                ts?.StartFalling(newTarget);

                var currentPos = tile.transform.position;
                var targetWorldY = newTarget.y * _tileSize + _gridRiser.CurrentGridOffset;
                var distance = Mathf.Abs(currentPos.y - targetWorldY) / _tileSize;
                maxRetargetDuration = Mathf.Max(maxRetargetDuration, distance / _dropSpeed);

                _droppingTiles.Add(tile);
                _dropTargets[tile] = newTarget;
                StartCoroutine(AnimateDrop(tile, currentPos, newTarget, true));
            }

            // Wait for all animations
            var waitTime = Mathf.Max(_swapDuration, maxCascadeDuration, maxRetargetDuration);
            if (waitTime > 0f)
                yield return new WaitForSeconds(waitTime);

            // Finalize swapping block
            if (swappingBlock != null)
            {
                var ts = swappingBlock.GetComponent<Tile>();
                ts?.Initialize(col, row, ts.TileType, _gridManager);
                ts?.FinishMovement();
                swappingBlock.transform.position = new Vector3(
                    col * _tileSize,
                    row * _tileSize + _gridRiser.CurrentGridOffset,
                    0);
            }

            // Cleanup
            _swappingTiles.Remove(swappingBlock);
            foreach (var (tile, from, to) in blocksToNudgeUp)
                _swappingTiles.Remove(tile);

            yield return _gridManager.StartCoroutine(_gridManager.DropTiles());

            // Check for matches
            var matches = _matchDetector.GetAllMatches();
            if (matches.Count > 0 && !_matchProcessor.IsProcessingMatches)
                _gridManager.StartCoroutine(_matchProcessor.ProcessMatches(matches));

            _gridManager.SetIsSwapping(false);
        }

        private bool CheckMidAirIntercept(GameObject swappedTile, Vector2Int swappedPos)
        {
            if (swappedTile == null) return false;

            var x = swappedPos.x;
            var y = swappedPos.y;

            // Find falling blocks above swapped position
            var fallingAbove = new List<(GameObject tile, Vector2Int gridPos)>();
            for (var checkY = y; checkY < _gridHeight; checkY++)
            {
                var block = _grid[x, checkY];
                if (block != null && block != swappedTile && _droppingTiles.Contains(block))
                    fallingAbove.Add((block, new Vector2Int(x, checkY)));
            }

            if (fallingAbove.Count == 0) return false;

            Debug.Log($"[MidAirIntercept] {fallingAbove.Count} falling blocks above ({x}, {y})");

            // Stop all falling blocks
            foreach (var (tile, gridPos) in fallingAbove)
            {
                CancelDrop(tile);
                _swappingTiles.Add(tile);
            }

            // Cascade blocks upward
            for (var i = fallingAbove.Count - 1; i >= 0; i--)
            {
                var (tile, oldPos) = fallingAbove[i];
                var newY = oldPos.y + 1;

                if (newY >= _gridHeight)
                {
                    Debug.LogWarning("[MidAirIntercept] Cannot cascade higher, aborting");
                    foreach (var (t, p) in fallingAbove)
                        _swappingTiles.Remove(t);
                    return false;
                }

                var newPos = new Vector2Int(x, newY);
                _grid[newPos.x, newPos.y] = tile;
                _grid[oldPos.x, oldPos.y] = null;

                fallingAbove[i] = (tile, newPos);
            }

            // Animate cascade
            foreach (var (tile, newPos) in fallingAbove)
            {
                var ts = tile.GetComponent<Tile>();
                ts?.Initialize(newPos.x, newPos.y, ts.TileType, _gridManager);
                StartCoroutine(AnimateCascadeQuick(tile, newPos));
            }

            return true;
        }

        private int GetVersion(GameObject tile)
        {
            return _dropAnimVersions.TryGetValue(tile, out var v) ? v : 0;
        }

        #endregion

        #region Animation Coroutines

        private IEnumerator AnimateSwap(GameObject tile, Vector2Int targetPos)
        {
            if (tile == null) yield break;

            var ts = tile.GetComponent<Tile>();
            var sr = tile.GetComponent<SpriteRenderer>();
            var originalSortOrder = sr?.sortingOrder ?? 0;
            if (sr != null) sr.sortingOrder = 10;

            var startWorldPos = tile.transform.position;
            var startGridX = startWorldPos.x / _tileSize;
            var startGridY = (startWorldPos.y - _gridRiser.CurrentGridOffset) / _tileSize;
            var currentTarget = targetPos;
            var targetGridY = (float)currentTarget.y;

            var previousOffset = _gridRiser.CurrentGridOffset;
            var elapsed = 0f;

            while (elapsed < _swapDuration)
            {
                // Detect row spawn
                var currentOffset = _gridRiser.CurrentGridOffset;
                if (currentOffset < previousOffset - (_tileSize * 0.1f))
                {
                    startGridY += 1f;
                    targetGridY += 1f;
                    currentTarget = new Vector2Int(currentTarget.x, currentTarget.y + 1);
                    ts?.Initialize(currentTarget.x, currentTarget.y, ts.TileType, _gridManager);
                }
                previousOffset = currentOffset;

                var progress = elapsed / _swapDuration;
                var currentGridX = Mathf.Lerp(startGridX, currentTarget.x, progress);
                var currentGridY = Mathf.Lerp(startGridY, targetGridY, progress);

                tile.transform.position = new Vector3(
                    currentGridX * _tileSize,
                    currentGridY * _tileSize + _gridRiser.CurrentGridOffset,
                    0);

                elapsed += Time.deltaTime;
                yield return null;
            }

            tile.transform.position = new Vector3(
                currentTarget.x * _tileSize,
                currentTarget.y * _tileSize + _gridRiser.CurrentGridOffset,
                0);

            ts?.FinishMovement();
            if (sr != null) sr.sortingOrder = originalSortOrder;
        }

        private IEnumerator AnimateCascadeSmooth(GameObject tile, Vector2Int toPos)
        {
            if (tile == null)
            {
                _swappingTiles.Remove(tile);
                yield break;
            }

            var ts = tile.GetComponent<Tile>();
            var startWorldPos = tile.transform.position;
            var startGridY = (startWorldPos.y - _gridRiser.CurrentGridOffset) / _tileSize;
            var currentTarget = toPos;
            var targetGridY = (float)currentTarget.y;

            var distance = Mathf.Abs(targetGridY - startGridY);
            var duration = distance / _dropSpeed;
            var elapsed = 0f;
            var previousOffset = _gridRiser.CurrentGridOffset;

            try
            {
                while (elapsed < duration && tile != null)
                {
                    var currentOffset = _gridRiser.CurrentGridOffset;
                    if (currentOffset < previousOffset - (_tileSize * 0.5f))
                    {
                        var oldTarget = currentTarget;
                        startGridY += 1f;
                        targetGridY += 1f;
                        currentTarget = new Vector2Int(currentTarget.x, currentTarget.y + 1);

                        if (_grid[oldTarget.x, oldTarget.y] == tile)
                            _grid[oldTarget.x, oldTarget.y] = null;
                        _grid[currentTarget.x, currentTarget.y] = tile;

                        ts?.Initialize(currentTarget.x, currentTarget.y, ts.TileType, _gridManager);
                    }
                    previousOffset = currentOffset;

                    var progress = elapsed / duration;
                    var currentGridY = Mathf.Lerp(startGridY, targetGridY, progress);
                    var worldY = currentGridY * _tileSize + _gridRiser.CurrentGridOffset;

                    tile.transform.position = new Vector3(currentTarget.x * _tileSize, worldY, 0);
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (tile != null)
                {
                    ts?.Initialize(currentTarget.x, currentTarget.y, ts.TileType, _gridManager);
                    ts?.FinishMovement();
                    tile.transform.position = new Vector3(
                        currentTarget.x * _tileSize,
                        currentTarget.y * _tileSize + _gridRiser.CurrentGridOffset,
                        0);
                }
            }
            finally
            {
                _swappingTiles.Remove(tile);
            }
        }

        private IEnumerator AnimateCascadeQuick(GameObject tile, Vector2Int toPos)
        {
            if (tile == null)
            {
                _swappingTiles.Remove(tile);
                yield break;
            }

            var ts = tile.GetComponent<Tile>();
            var startWorldPos = tile.transform.position;
            var startGridY = (startWorldPos.y - _gridRiser.CurrentGridOffset) / _tileSize;
            var currentTarget = toPos;
            var targetGridY = (float)currentTarget.y;

            var distance = Mathf.Abs(targetGridY - startGridY);
            var duration = distance / _dropSpeed * 0.5f; // Quick nudge
            var elapsed = 0f;
            var previousOffset = _gridRiser.CurrentGridOffset;

            try
            {
                while (elapsed < duration && tile != null)
                {
                    var currentOffset = _gridRiser.CurrentGridOffset;
                    if (currentOffset < previousOffset - (_tileSize * 0.5f))
                    {
                        var oldTarget = currentTarget;
                        startGridY += 1f;
                        targetGridY += 1f;
                        currentTarget = new Vector2Int(currentTarget.x, currentTarget.y + 1);

                        if (_grid[oldTarget.x, oldTarget.y] == tile)
                            _grid[oldTarget.x, oldTarget.y] = null;
                        _grid[currentTarget.x, currentTarget.y] = tile;

                        ts?.Initialize(currentTarget.x, currentTarget.y, ts.TileType, _gridManager);
                    }
                    previousOffset = currentOffset;

                    var progress = elapsed / duration;
                    var currentGridY = Mathf.Lerp(startGridY, targetGridY, progress);
                    var worldY = currentGridY * _tileSize + _gridRiser.CurrentGridOffset;

                    tile.transform.position = new Vector3(currentTarget.x * _tileSize, worldY, 0);
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (tile != null)
                {
                    ts?.Initialize(currentTarget.x, currentTarget.y, ts.TileType, _gridManager);
                    ts?.FinishMovement();
                    tile.transform.position = new Vector3(
                        currentTarget.x * _tileSize,
                        currentTarget.y * _tileSize + _gridRiser.CurrentGridOffset,
                        0);
                }
            }
            finally
            {
                _swappingTiles.Remove(tile);
            }
        }

        private IEnumerator AnimateDrop(GameObject tile, Vector3 fromWorldPos, Vector2Int toPos, bool checkObstructions)
        {
            if (tile == null)
            {
                _droppingTiles.Remove(tile);
                yield break;
            }

            var ts = tile.GetComponent<Tile>();
            var myVersion = GetVersion(tile) + 1;
            _dropAnimVersions[tile] = myVersion;
            _dropTargets[tile] = toPos;

            var sr = tile.GetComponent<SpriteRenderer>();
            var originalSortOrder = sr?.sortingOrder ?? 0;
            if (sr != null) sr.sortingOrder = 10;

            var startGridY = (fromWorldPos.y - _gridRiser.CurrentGridOffset) / _tileSize;
            var currentTarget = toPos;
            var targetGridY = currentTarget.y;

            var distance = Mathf.Abs(startGridY - targetGridY);
            var duration = distance / _dropSpeed;
            var elapsed = 0f;
            var previousOffset = _gridRiser.CurrentGridOffset;

            try
            {
                while (elapsed < duration && tile != null && _droppingTiles.Contains(tile))
                {
                    // Check version - if we've been superseded, exit
                    if (_dropAnimVersions.TryGetValue(tile, out var currentVersion) && currentVersion != myVersion)
                    {
                        Debug.Log($"[AnimateDrop] Version mismatch for {tile.name}: {myVersion} vs {currentVersion}, exiting");
                        yield break;
                    }

                    // Detect row spawn
                    var currentOffset = _gridRiser.CurrentGridOffset;
                    if (currentOffset < previousOffset - (_tileSize * 0.5f))
                    {
                        var oldTarget = currentTarget;
                        startGridY += 1f;
                        targetGridY += 1;
                        currentTarget = new Vector2Int(currentTarget.x, currentTarget.y + 1);

                        if (_grid[oldTarget.x, oldTarget.y] == tile)
                            _grid[oldTarget.x, oldTarget.y] = null;
                        _grid[currentTarget.x, currentTarget.y] = tile;
                        _dropTargets[tile] = currentTarget;

                        ts?.Initialize(currentTarget.x, currentTarget.y, ts.TileType, _gridManager);
                        ts?.StartFalling(currentTarget);
                    }
                    previousOffset = currentOffset;

                    // Check for obstructions
                    if (checkObstructions)
                    {
                        var currentWorldY = tile.transform.position.y;
                        var targetWorldY = currentTarget.y * _tileSize + _gridRiser.CurrentGridOffset;
                        var distanceToTarget = Mathf.Abs(currentWorldY - targetWorldY) / _tileSize;

                        if (distanceToTarget > 0.5f)
                        {
                            var newTarget = CheckPathObstruction(tile, currentTarget);
                            if (newTarget.HasValue && newTarget.Value.y != currentTarget.y)
                            {
                                var atNewTarget = _grid[newTarget.Value.x, newTarget.Value.y];
                                if (atNewTarget == null || atNewTarget == tile)
                                {
                                    var oldTarget = currentTarget;
                                    currentTarget = newTarget.Value;

                                    if (_grid[oldTarget.x, oldTarget.y] == tile)
                                        _grid[oldTarget.x, oldTarget.y] = null;
                                    _grid[currentTarget.x, currentTarget.y] = tile;

                                    ts?.Initialize(currentTarget.x, currentTarget.y, ts.TileType, _gridManager);
                                    ts?.RetargetFall(currentTarget);
                                    _dropTargets[tile] = currentTarget;
                                    _retargetedDrops.Add(tile);

                                    var retargetGridY = (tile.transform.position.y - _gridRiser.CurrentGridOffset) / _tileSize;
                                    var remaining = Mathf.Abs(retargetGridY - currentTarget.y);

                                    startGridY = retargetGridY;
                                    targetGridY = currentTarget.y;
                                    duration = remaining / _dropSpeed;
                                    elapsed = 0;

                                    Debug.Log($"[Drop] Retargeted to ({currentTarget.x}, {currentTarget.y})");
                                }
                            }
                        }
                    }

                    var progress = duration > 0 ? elapsed / duration : 1f;
                    _dropProgress[tile] = progress;

                    var currentGridY = Mathf.Lerp(startGridY, targetGridY, progress);
                    var worldY = currentGridY * _tileSize + _gridRiser.CurrentGridOffset;

                    tile.transform.position = new Vector3(currentTarget.x * _tileSize, worldY, 0);
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                // Finalize position
                if (tile != null && _dropAnimVersions.TryGetValue(tile, out var latestVersion) && latestVersion == myVersion)
                {
                    // Verify grid consistency before finalizing
                    var gridOccupant = _grid[currentTarget.x, currentTarget.y];
                    if (gridOccupant != null && gridOccupant != tile)
                    {
                        Debug.LogError($"[AnimateDrop] DESYNC PREVENTED: {tile.name} tried to finalize at ({currentTarget.x},{currentTarget.y}) " +
                                       $"but cell is occupied by {gridOccupant.name}! Finding correct position...");
                        
                        // Try to find where this tile actually belongs in the grid
                        var actualPos = _gridManager.FindTilePosition(tile);
                        if (actualPos.HasValue)
                        {
                            currentTarget = actualPos.Value;
                            Debug.Log($"[AnimateDrop] Found {tile.name} at grid ({currentTarget.x},{currentTarget.y}), using that position");
                        }
                        else
                        {
                            Debug.LogError($"[AnimateDrop] {tile.name} is not in grid at all! Orphaned tile detected.");
                            // Don't finalize - tile is orphaned
                            if (sr != null) sr.sortingOrder = originalSortOrder;
                            yield break;
                        }
                    }

                    tile.transform.position = new Vector3(
                        currentTarget.x * _tileSize,
                        currentTarget.y * _tileSize + _gridRiser.CurrentGridOffset,
                        0);

                    ts?.Initialize(currentTarget.x, currentTarget.y, ts.TileType, _gridManager);
                    ts?.FinishMovement();
                    ts?.PlayLandSound();

                    if (sr != null) sr.sortingOrder = originalSortOrder;
                }
            }
            finally
            {
                if (tile != null)
                {
                    var latestVersion = _dropAnimVersions.TryGetValue(tile, out var v) ? v : 0;

                    if (latestVersion <= myVersion)
                    {
                        _droppingTiles.Remove(tile);
                        _dropProgress.Remove(tile);
                        _dropTargets.Remove(tile);
                        _retargetedDrops.Remove(tile);

                        if (latestVersion == myVersion)
                        {
                            tile.GetComponent<Tile>()?.FinishMovement();
                            _dropAnimVersions.Remove(tile);
                            if (sr != null) sr.sortingOrder = originalSortOrder;
                        }
                        else if (sr != null)
                        {
                            sr.sortingOrder = originalSortOrder;
                        }
                    }
                }
            }
        }

        private Vector2Int? CheckPathObstruction(GameObject tile, Vector2Int targetPos)
        {
            if (tile == null) return null;

            var currentWorldY = tile.transform.position.y;
            var currentGridY = Mathf.RoundToInt((currentWorldY - _gridRiser.CurrentGridOffset) / _tileSize);
            currentGridY = Mathf.Clamp(currentGridY, 0, _gridHeight - 1);

            var x = targetPos.x;

            for (var y = currentGridY - 1; y >= targetPos.y; y--)
            {
                if (y < 0 || y >= _gridHeight) continue;

                var blockAtPos = _grid[x, y];
                if (blockAtPos == null || blockAtPos == tile) continue;

                var isSolid = false;
                if (_droppingTiles.Contains(blockAtPos))
                {
                    if (_retargetedDrops.Contains(blockAtPos) &&
                        _dropTargets.TryGetValue(blockAtPos, out var otherTarget) &&
                        otherTarget.y <= y)
                    {
                        isSolid = true;
                    }
                }
                else
                {
                    isSolid = true;
                }

                if (isSolid)
                {
                    var newTargetY = y + 1;
                    if (newTargetY < _gridHeight && newTargetY != targetPos.y)
                    {
                        var atLanding = _grid[x, newTargetY];
                        if (atLanding == null || atLanding == tile)
                            return new Vector2Int(x, newTargetY);
                    }
                }
            }

            return null;
        }

        #endregion
    }
}