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
        private float _dropDuration;

        // Drop tracking
        private readonly Dictionary<GameObject, int> _dropAnimVersions = new();
        private readonly Dictionary<GameObject, float> _dropProgress = new();
        private readonly Dictionary<GameObject, Vector2Int> _dropTargets = new();
        private readonly HashSet<GameObject> _retargetedDrops = new();

        #endregion

        #region Initialization

        public void Initialize(
            GridManager gridManager,
            GameObject[,] grid,
            int gridWidth, int gridHeight, float tileSize,
            float swapDuration, float dropDuration,
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
            _dropDuration = dropDuration;
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
        /// Check if any falling block in the column has passed the 50% threshold.
        /// </summary>
        public bool IsBlockSlipTooLate(int columnX, int rowY)
        {
            if (_droppingTiles.Count == 0) return false;

            var offsetY = _gridRiser?.CurrentGridOffset ?? 0f;
            var swapRowWorldY = rowY * _tileSize + offsetY;
            var swapRowMidpoint = swapRowWorldY + _tileSize * 0.4f; // 10% buffer
            var swapRowTop = swapRowWorldY + _tileSize;

            foreach (var tile in _droppingTiles)
            {
                if (tile == null) continue;
                if (!_dropTargets.TryGetValue(tile, out var target) || target.x != columnX) continue;
                if (target.y > rowY) continue;

                var tileWorldY = tile.transform.position.y;
                if (tileWorldY >= swapRowWorldY && tileWorldY < swapRowTop && tileWorldY < swapRowMidpoint)
                {
                    Debug.Log($"[BlockSlip] TOO LATE: Tile at Y:{tileWorldY:F2} past midpoint {swapRowMidpoint:F2}");
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

            if (IsBlockSlipTooLate(leftX, y) || IsBlockSlipTooLate(rightX, y))
            {
                Debug.Log(">>> BLOCKSLIP BLOCKED - past 50% threshold <<<");
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
            var swapRowMidpoint = swapRowWorldY + _tileSize * 0.4f;

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
                    if (tileWorldY < swapRowMidpoint)
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
                _droppingTiles.Remove(tile);
                _dropProgress.Remove(tile);
                _dropTargets.Remove(tile);
                _dropAnimVersions[tile] = GetVersion(tile) + 1;
                tile.GetComponent<Tile>()?.FinishMovement();
            }

            // Cancel falling block's drop if tracked
            if (fallingBlock != null && _droppingTiles.Contains(fallingBlock))
            {
                _droppingTiles.Remove(fallingBlock);
                _dropProgress.Remove(fallingBlock);
                _dropTargets.Remove(fallingBlock);
                _dropAnimVersions[fallingBlock] = GetVersion(fallingBlock) + 1;
                fallingBlock.GetComponent<Tile>()?.FinishMovement();
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
                maxCascadeDuration = Mathf.Max(maxCascadeDuration, _dropDuration * distance);
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
                maxRetargetDuration = Mathf.Max(maxRetargetDuration, _dropDuration * distance);

                _droppingTiles.Add(tile);
                _dropTargets[tile] = newTarget;
                StartCoroutine(AnimateDrop(tile, currentPos, newTarget, false));
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
                _droppingTiles.Remove(tile);
                _dropProgress.Remove(tile);
                _dropTargets.Remove(tile);
                _dropAnimVersions[tile] = GetVersion(tile) + 1;
                _swappingTiles.Add(tile);
                tile.GetComponent<Tile>()?.FinishMovement();
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
            var duration = _dropDuration * distance;
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
            var duration = _dropDuration * distance * 0.5f; // Quick nudge
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
            var duration = _dropDuration * distance;
            var elapsed = 0f;
            var previousOffset = _gridRiser.CurrentGridOffset;

            try
            {
                while (elapsed < duration && tile != null && _droppingTiles.Contains(tile))
                {
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
                                    duration = _dropDuration * remaining;
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