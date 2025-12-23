using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Central grid coordinator. Manages tile array, swap requests, and drop logic.
    /// Delegates animation to TileAnimator and movement logic to BlockSlipManager.
    /// Integrates with GarbageManager for garbage block handling.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Grid Settings")]
        public int gridWidth = 6;
        public int gridHeight = 14;
        public float tileSize = 1f;

        [Header("Initialization")]
        public int initialFillRows = 4;
        public int preloadRows = 2;

        [Header("Timing")]
        public float swapDuration = 0.15f;
        public float dropSpeed = 6.67f; // tiles per second

        [Header("Components")]
        public CursorController cursorController;
        public GridRiser gridRiser;
        public MatchDetector matchDetector;
        public MatchProcessor matchProcessor;
        public TileSpawner tileSpawner;
        public BlockSlipManager blockSlipManager;
        public GarbageManager garbageManager;

        #endregion

        #region Internal State

        private GameObject[,] _grid;
        private GameObject[,] _preloadGrid;
        private readonly HashSet<GameObject> _swappingTiles = new();
        private readonly HashSet<GameObject> _droppingTiles = new();
        
        // Track if we're currently in a DropTiles coroutine to prevent re-entry
        private bool _isProcessingDrops;

        #endregion

        #region Properties

        public bool IsSwapping { get; private set; }
        public GameObject[,] Grid => _grid;
        public int Width => gridWidth;
        public int Height => gridHeight;
        public float TileSize => tileSize;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            InitializeGridArrays();
            InitializeComponents();
            tileSpawner.CreateBackgroundTiles();
            StartCoroutine(InitializeGrid());
        }

        private void Update()
        {
            if (gridRiser.IsGameOver) return;
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused) return;

            CleanupNullTiles();
            blockSlipManager.CleanupTracking();
            ProcessGarbage();
            cursorController.HandleInput();
            HandleSwapInput();
            gridRiser.DisplayDebugInfo();
        }

        #endregion

        #region Initialization

        private void InitializeGridArrays()
        {
            _grid = new GameObject[gridWidth, gridHeight];
            _preloadGrid = new GameObject[gridWidth, preloadRows];
        }

        private void InitializeComponents()
        {
            tileSpawner.Initialize(this, tileSize, _grid, _preloadGrid, gridWidth, gridHeight, preloadRows);
            cursorController.Initialize(this, tileSize, gridWidth, gridHeight, tileSpawner, gridRiser);
            matchDetector.Initialize(_grid, gridWidth, gridHeight);
            matchProcessor.Initialize(this, _grid, matchDetector);
            gridRiser.Initialize(this, _grid, _preloadGrid, tileSpawner, cursorController, 
                matchDetector, matchProcessor, tileSize, gridWidth, gridHeight);

            blockSlipManager.Initialize(this, _grid, gridWidth, gridHeight, tileSize,
                swapDuration, dropSpeed, gridRiser, matchDetector, matchProcessor,
                cursorController, _swappingTiles, _droppingTiles);
            
            garbageManager.Initialize(this, tileSpawner, matchDetector, gridRiser);
        }

        private IEnumerator InitializeGrid()
        {
            // Fill preload rows
            for (var y = 0; y < preloadRows; y++)
            for (var x = 0; x < gridWidth; x++)
            {
                tileSpawner.SpawnPreloadTile(x, y, gridRiser.CurrentGridOffset);
                yield return new WaitForSeconds(0.01f);
            }

            // Fill initial rows
            for (var y = 0; y < initialFillRows; y++)
            for (var x = 0; x < gridWidth; x++)
            {
                tileSpawner.SpawnTile(x, y, gridRiser.CurrentGridOffset);
                yield return new WaitForSeconds(0.02f);
            }

            yield return StartCoroutine(matchProcessor.CheckAndClearMatches());
            gridRiser.StartRising();
        }

        private void CleanupNullTiles()
        {
            _swappingTiles.RemoveWhere(t => t == null);
            _droppingTiles.RemoveWhere(t => t == null);
        }

        #endregion

        #region Public API

        public void SetIsSwapping(bool value) => IsSwapping = value;

        public void RequestSwapAtCursor()
        {
            if (!IsSwapping) StartCoroutine(SwapCursorTiles());
        }

        public bool IsTileSwapping(GameObject tile) => _swappingTiles.Contains(tile);
        public bool IsTileDropping(GameObject tile) => _droppingTiles.Contains(tile);
        public bool IsTileAnimating(GameObject tile) => _swappingTiles.Contains(tile) || _droppingTiles.Contains(tile);

        /// <summary>
        /// Check if any tile is currently dropping toward this cell.
        /// </summary>
        public bool IsCellTargetedByDrop(int x, int y)
        {
            return blockSlipManager.IsCellTargetedByDrop(x, y);
        }

        /// <summary>
        /// Check if there are any active drops in progress.
        /// </summary>
        public bool HasActiveDrops()
        {
            return _droppingTiles.Count > 0;
        }

        public void AddBreathingRoom(int tilesMatched) => gridRiser.AddBreathingRoom(tilesMatched);

        public GameObject[,] GetGrid() => _grid;

        public Vector2Int? FindTilePosition(GameObject tile)
        {
            if (tile == null) return null;

            for (var x = 0; x < gridWidth; x++)
            for (var y = 0; y < gridHeight; y++)
                if (_grid[x, y] == tile)
                    return new Vector2Int(x, y);

            return null;
        }

        /// <summary>
        /// Check if a tile at position can be swapped (considering status effects and garbage).
        /// </summary>
        public bool CanTileSwap(int x, int y)
        {
            var cell = _grid[x, y];
            if (cell == null) return true; // Empty space can always "swap"
            
            // Check if it's garbage
            var garbageRef = cell.GetComponent<GarbageReference>();
            if (garbageRef != null) return false; // Can't swap garbage
            
            var garbageBlock = cell.GetComponent<GarbageBlock>();
            if (garbageBlock != null) return false; // Can't swap garbage
            
            var tileScript = cell.GetComponent<Tile>();
            if (tileScript == null) return true;
            
            return tileScript.CanSwap();
        }

        /// <summary>
        /// Check if a cell contains garbage.
        /// </summary>
        public bool IsGarbageAt(int x, int y)
        {
            if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return false;
            return garbageManager.IsGarbageAt(x, y);
        }

        /// <summary>
        /// Get the garbage block at a position (if any).
        /// </summary>
        public GarbageBlock GetGarbageAt(int x, int y)
        {
            if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return null;
            return garbageManager.GetGarbageAt(x, y);
        }

        #endregion

        #region Input & Swap Handling

        private void HandleSwapInput()
        {
            if (IsSwapping) return;

            if (!(Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Space)))
                return;

            var cursorPos = cursorController.CursorPosition;
            var leftX = cursorPos.x;
            var rightX = cursorPos.x + 1;
            var y = cursorPos.y;

            var leftTile = _grid[leftX, y];
            var rightTile = _grid[rightX, y];

            // Garbage check - can't swap garbage
            if (IsGarbageAt(leftX, y) || IsGarbageAt(rightX, y))
            {
                Debug.Log(">>> SWAP BLOCKED - garbage in cursor <<<");
                return;
            }

            // Status effect check
            if (!CanTileSwap(leftX, y) || !CanTileSwap(rightX, y))
            {
                Debug.Log(">>> SWAP BLOCKED - tile status prevents swap <<<");
                return;
            }

            var leftScript = leftTile?.GetComponent<Tile>();
            var rightScript = rightTile?.GetComponent<Tile>();

            var leftFalling = leftScript != null && leftScript.IsFalling;
            var rightFalling = rightScript != null && rightScript.IsFalling;
            var leftIdle = leftScript == null || leftScript.IsIdle;
            var rightIdle = rightScript == null || rightScript.IsIdle;

            // Match processing check
            if (matchProcessor.IsTileBeingProcessed(leftX, y) || matchProcessor.IsTileBeingProcessed(rightX, y))
            {
                Debug.Log(">>> SWAP BLOCKED - tiles being processed <<<");
                return;
            }

            // Swapping check
            if ((leftTile != null && _swappingTiles.Contains(leftTile)) || 
                (rightTile != null && _swappingTiles.Contains(rightTile)))
            {
                Debug.Log(">>> SWAP BLOCKED - tiles swapping <<<");
                return;
            }

            // Active check
            if (!IsTileActive(leftTile) || !IsTileActive(rightTile))
            {
                Debug.Log(">>> SWAP BLOCKED - tiles not yet active <<<");
                return;
            }

            // Held tile check (during garbage conversion)
            if ((leftTile != null && garbageManager.IsTileHeld(leftTile)) ||
                (rightTile != null && garbageManager.IsTileHeld(rightTile)))
            {
                Debug.Log(">>> SWAP BLOCKED - tiles held during garbage conversion <<<");
                return;
            }

            // Case 1: One tile falling at cursor -> BlockSlip kick-under
            if (leftFalling ^ rightFalling)
            {
                if (blockSlipManager.TryKickUnderFallingAtCursor(cursorPos))
                    return;

                Debug.Log(">>> SWAP BLOCKED - one tile falling, BlockSlip not available <<<");
                return;
            }

            // Case 2: Both idle -> check for falling blocks higher up
            if (leftIdle && rightIdle)
                if (blockSlipManager.TryHandleBlockSlipAtCursor(cursorPos))
                    return;

            // 50% threshold check
            if (blockSlipManager.IsBlockSlipTooLate(leftX, y) || blockSlipManager.IsBlockSlipTooLate(rightX, y))
            {
                Debug.Log(">>> SWAP BLOCKED - falling block past 50% threshold <<<");
                return;
            }

            // Normal swap (only if not dropping at cursor)
            var leftDropping = leftTile != null && _droppingTiles.Contains(leftTile);
            var rightDropping = rightTile != null && _droppingTiles.Contains(rightTile);

            if (!leftDropping && !rightDropping)
                StartCoroutine(SwapCursorTiles());
            else
                Debug.Log(">>> SWAP BLOCKED - tiles dropping at cursor <<<");
        }

        private bool IsTileActive(GameObject tile)
        {
            return tile == null || tileSpawner.IsTileActive(tile, gridRiser.CurrentGridOffset);
        }

        private IEnumerator SwapCursorTiles()
        {
            IsSwapping = true;

            var cursorPos = cursorController.CursorPosition;
            var leftX = cursorPos.x;
            var rightX = cursorPos.x + 1;
            var y = cursorPos.y;

            var leftTile = _grid[leftX, y];
            var rightTile = _grid[rightX, y];

            // Mark as swapping before grid update
            if (leftTile != null) _swappingTiles.Add(leftTile);
            if (rightTile != null) _swappingTiles.Add(rightTile);

            // Update grid array
            _grid[leftX, y] = rightTile;
            _grid[rightX, y] = leftTile;

            try
            {
                // Start animations
                if (leftTile != null) blockSlipManager.StartSwapAnimation(leftTile, new Vector2Int(rightX, y));
                if (rightTile != null) blockSlipManager.StartSwapAnimation(rightTile, new Vector2Int(leftX, y));

                yield return new WaitForSeconds(swapDuration);

                // Update coordinates after animation
                FinalizeSwappedTile(leftTile);
                FinalizeSwappedTile(rightTile);

                // Remove from swapping set BEFORE checking drops
                _swappingTiles.Remove(leftTile);
                _swappingTiles.Remove(rightTile);

                IsSwapping = false;

                // Check for momentum (frozen tiles continue moving)
                HandlePostSwapMomentum(leftTile, rightTile);

                // Mid-air interception check
                var leftPos = FindTilePosition(leftTile);
                var rightPos = FindTilePosition(rightTile);
                var midAirHandled = false;
                
                if (leftPos.HasValue && rightPos.HasValue)
                    midAirHandled = blockSlipManager.HandleMidAirSwapInterception(leftTile, rightTile, leftPos.Value, rightPos.Value);

                if (midAirHandled)
                    yield return new WaitForSeconds(0.5f / dropSpeed);

                yield return StartCoroutine(DropTiles());

                // Check for matches
                var matches = matchDetector.GetAllMatches();
                if (matches.Count > 0 && !matchProcessor.IsProcessingMatches)
                    StartCoroutine(matchProcessor.ProcessMatches(matches));
            }
            finally
            {
                // Ensure cleanup even if coroutine is stopped
                _swappingTiles.Remove(leftTile);
                _swappingTiles.Remove(rightTile);
                IsSwapping = false;
            }
        }

        private void FinalizeSwappedTile(GameObject tileObj)
        {
            if (tileObj == null) return;
            
            var pos = FindTilePosition(tileObj);
            if (!pos.HasValue) return;

            var tile = tileObj.GetComponent<Tile>();
            if (tile == null) return;
            
            tile.Initialize(pos.Value.x, pos.Value.y, tile.TileType, this);
            tileObj.transform.position = new Vector3(
                pos.Value.x * tileSize,
                pos.Value.y * tileSize + gridRiser.CurrentGridOffset, 
                0);
        }

        /// <summary>
        /// Handles momentum for frozen tiles that continue moving after swap.
        /// </summary>
        private void HandlePostSwapMomentum(GameObject leftTile, GameObject rightTile)
        {
            CheckTileMomentum(leftTile, 1);  // Left tile moved right
            CheckTileMomentum(rightTile, -1); // Right tile moved left
        }

        private void CheckTileMomentum(GameObject tileObj, int direction)
        {
            if (tileObj == null) return;
            
            var tile = tileObj.GetComponent<Tile>();
            if (tile == null || !tile.HasMomentum()) return;

            // Frozen tile continues moving in same direction until blocked
            StartCoroutine(ContinueMomentum(tileObj, direction));
        }

        private IEnumerator ContinueMomentum(GameObject tileObj, int direction)
        {
            while (tileObj != null)
            {
                var tile = tileObj.GetComponent<Tile>();
                if (tile == null || !tile.HasMomentum()) yield break;

                var pos = FindTilePosition(tileObj);
                if (!pos.HasValue) yield break;

                var nextX = pos.Value.x + direction;
                
                // Check bounds
                if (nextX < 0 || nextX >= gridWidth) yield break;
                
                // Check if next position is blocked
                if (_grid[nextX, pos.Value.y] != null) yield break;

                // Move to next position
                _grid[pos.Value.x, pos.Value.y] = null;
                _grid[nextX, pos.Value.y] = tileObj;
                
                _swappingTiles.Add(tileObj);
                blockSlipManager.StartSwapAnimation(tileObj, new Vector2Int(nextX, pos.Value.y));
                
                yield return new WaitForSeconds(swapDuration);
                
                tile.Initialize(nextX, pos.Value.y, tile.TileType, this);
                _swappingTiles.Remove(tileObj);
            }
        }

        #endregion

        #region Drop Logic

        public IEnumerator DropTiles()
        {
            // Prevent re-entry - if we're already processing drops, skip
            if (_isProcessingDrops)
            {
                Debug.Log("[DropTiles] Already processing drops, skipping");
                yield break;
            }

            _isProcessingDrops = true;

            try
            {
                // Keep dropping until no more drops are needed
                int iterations = 0;
                const int maxIterations = 20; // Safety limit

                while (iterations < maxIterations)
                {
                    iterations++;

                    var drops = CollectDrops();
                    if (drops.Count == 0) break;

                    ValidateDropTargets(drops);
                    var maxDropDistance = ExecuteDrops(drops);

                    if (drops.Count > 0)
                    {
                        var waitTime = maxDropDistance / dropSpeed + 0.05f;
                        Debug.Log($"[DropTiles] Iteration {iterations}: Waiting {waitTime:F2}s for {drops.Count} drops");
                        yield return new WaitForSeconds(waitTime);
                    }

                    // Small delay between iterations to let animations fully settle
                    yield return new WaitForSeconds(0.02f);
                }

                if (iterations >= maxIterations)
                {
                    Debug.LogWarning("[DropTiles] Hit max iterations limit!");
                }

                Debug.Log($"[DropTiles] Complete after {iterations} iteration(s)");
            }
            finally
            {
                _isProcessingDrops = false;
            }
        }

        private List<(GameObject tile, Vector2Int from, Vector2Int to)> CollectDrops()
        {
            var drops = new List<(GameObject tile, Vector2Int from, Vector2Int to)>();
            var pendingTargets = new HashSet<Vector2Int>(); // Track targets we're about to assign

            for (var x = 0; x < gridWidth; x++)
            {
                // Process each column from bottom to top
                for (var y = 0; y < gridHeight; y++)
                {
                    // Skip non-empty cells
                    if (_grid[x, y] != null) continue;
                    
                    // Skip cells that already have a drop targeting them
                    if (blockSlipManager.IsCellTargetedByDrop(x, y))
                    {
                        Debug.Log($"[CollectDrops] Cell ({x},{y}) already targeted by active drop, skipping");
                        continue;
                    }

                    // Skip cells we're about to assign in this batch
                    if (pendingTargets.Contains(new Vector2Int(x, y)))
                    {
                        continue;
                    }

                    // Find the first tile above that can drop
                    for (var aboveY = y + 1; aboveY < gridHeight; aboveY++)
                    {
                        var cell = _grid[x, aboveY];
                        if (cell == null) continue;

                        // Check if it's garbage - garbage has its own falling logic
                        var garbageRef = cell.GetComponent<GarbageReference>();
                        if (garbageRef != null)
                        {
                            Debug.Log($"[CollectDrops] Cell ({x},{aboveY}) is garbage reference, stopping column search");
                            break;
                        }
                        
                        var garbageBlock = cell.GetComponent<GarbageBlock>();
                        if (garbageBlock != null)
                        {
                            Debug.Log($"[CollectDrops] Cell ({x},{aboveY}) is garbage block, stopping column search");
                            break;
                        }

                        // Skip tiles already dropping
                        if (_droppingTiles.Contains(cell))
                        {
                            Debug.Log($"[CollectDrops] Tile at ({x},{aboveY}) already dropping, stopping column search");
                            break;
                        }

                        // Skip tiles being swapped
                        if (_swappingTiles.Contains(cell))
                        {
                            Debug.Log($"[CollectDrops] Tile at ({x},{aboveY}) is swapping, stopping column search");
                            break;
                        }

                        // Skip tiles being processed for matches
                        if (matchProcessor.IsTileBeingProcessed(x, aboveY))
                        {
                            Debug.Log($"[CollectDrops] Tile at ({x},{aboveY}) being processed, stopping column search");
                            break;
                        }

                        // Skip tiles held during garbage conversion
                        if (garbageManager.IsTileHeld(cell))
                        {
                            Debug.Log($"[CollectDrops] Tile at ({x},{aboveY}) held during conversion, stopping column search");
                            break;
                        }

                        // Found a tile to drop
                        var from = new Vector2Int(x, aboveY);
                        var to = new Vector2Int(x, y);

                        drops.Add((cell, from, to));
                        pendingTargets.Add(to);
                        
                        // Update grid immediately
                        _grid[x, y] = cell;
                        _grid[x, aboveY] = null;

                        Debug.Log($"[CollectDrops] Tile {cell.name} will drop from ({from.x},{from.y}) to ({to.x},{to.y})");
                        break;
                    }
                }
            }

            return drops;
        }

        private void ValidateDropTargets(List<(GameObject tile, Vector2Int from, Vector2Int to)> drops)
        {
            var targetMap = new Dictionary<Vector2Int, GameObject>();
            foreach (var (tile, from, to) in drops)
            {
                if (targetMap.ContainsKey(to))
                {
                    Debug.LogError($"[DropTiles] CONFLICT: Multiple tiles targeting ({to.x}, {to.y})! " +
                                   $"Tiles: {targetMap[to].name} and {tile.name}");
                }
                targetMap[to] = tile;
            }
        }

        private int ExecuteDrops(List<(GameObject tile, Vector2Int from, Vector2Int to)> drops)
        {
            var maxDistance = 0;

            foreach (var (tileObj, from, to) in drops)
            {
                if (tileObj == null) continue;

                var distance = from.y - to.y;
                maxDistance = Mathf.Max(maxDistance, distance);

                var tile = tileObj.GetComponent<Tile>();
                if (tile != null)
                {
                    tile.Initialize(to.x, to.y, tile.TileType, this);
                }
                blockSlipManager.BeginDrop(tileObj, from, to);
            }

            return maxDistance;
        }

        #endregion
        
        #region Garbage Management

        private void ProcessGarbage()
        {
            // Only drop garbage when conditions are right
            if (garbageManager.GetPendingGarbageCount() > 0 
                && !garbageManager.IsProcessingGarbage
                && !garbageManager.IsProcessingConversion
                && !matchProcessor.IsProcessingMatches
                && !HasActiveDrops()
                && !garbageManager.HasFallingGarbage())
            {
                garbageManager.DropPendingGarbage();
            }
        }

        #endregion

        #region Debug

        /// <summary>
        /// Debug method to spawn garbage for testing.
        /// </summary>
        [ContextMenu("Spawn Test Garbage 3x1")]
        public void SpawnTestGarbage3x1()
        {
            garbageManager.QueueGarbage(3, 1);
        }

        [ContextMenu("Spawn Test Garbage 6x2")]
        public void SpawnTestGarbage6x2()
        {
            garbageManager.QueueGarbage(6, 2);
        }

        [ContextMenu("Spawn Test Garbage 4x3")]
        public void SpawnTestGarbage4x3()
        {
            garbageManager.QueueGarbage(4, 3);
        }

        #endregion
    }
}