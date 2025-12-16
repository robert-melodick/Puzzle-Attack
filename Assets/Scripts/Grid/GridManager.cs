using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    public class GridManager : MonoBehaviour
    {
        #region Drop Logic

        public IEnumerator DropTiles()
        {
            List<(GameObject tile, Vector2Int from, Vector2Int to)> drops = new();
            var maxDropDistance = 0;

            // Collect all drops first
            for (var x = 0; x < gridWidth; x++)
            for (var y = 0; y < gridHeight; y++)
                if (_grid[x, y] == null)
                    for (var aboveY = y + 1; aboveY < gridHeight; aboveY++)
                        if (_grid[x, aboveY] != null)
                        {
                            var tile = _grid[x, aboveY];

                            // Check if this block is already dropping (e.g., retargeted by blockslip)
                            if (_droppingTiles.Contains(tile))
                            {
                                // This tile is already dropping and will land somewhere
                                // We can't determine where it will land, and any block above it
                                // can't drop past it anyway, so stop searching this column
                                Debug.Log(
                                    $"[DropTiles] Found already-dropping tile at ({x}, {aboveY}), stopping search (can't drop blocks above a dropping block)");
                                break; // Stop searching - nothing above can drop past this
                            }

                            // Check if this block is being processed for matches (in stasis)
                            if (matchProcessor.IsTileBeingProcessed(x, aboveY))
                            {
                                // This tile is being processed (match animation, etc.)
                                // It's in stasis and shouldn't fall yet. Nothing above can drop past it.
                                Debug.Log(
                                    $"[DropTiles] Found tile being processed at ({x}, {aboveY}), stopping search (protecting match stasis)");
                                break; // Stop searching - protect tiles in match processing
                            }

                            var dropDistance = aboveY - y;
                            maxDropDistance = Mathf.Max(maxDropDistance, dropDistance);
                            drops.Add((tile, new Vector2Int(x, aboveY), new Vector2Int(x, y)));

                            // Update grid array
                            _grid[x, y] = tile;
                            _grid[x, aboveY] = null;
                            break;
                        }

            // Check for conflicts before starting animations
            var targetMap = new Dictionary<Vector2Int, GameObject>();
            foreach (var (tile, from, to) in drops)
            {
                if (targetMap.ContainsKey(to))
                    Debug.LogError(
                        $"[DropTiles] CONFLICT: Both {targetMap[to].name} and {tile.name} want to drop to ({to.x}, {to.y})!");

                targetMap[to] = tile;
            }

            Debug.Log($"[DropTiles] Collected {drops.Count} drops, max distance: {maxDropDistance}");

            // Update coordinates and start animations
            foreach (var (tile, from, to) in drops)
                if (tile != null)
                {
                    var tileScript = tile.GetComponent<Tile>();
                    tileScript.Initialize(to.x, to.y, tileScript.TileType, this);

                    // Let BlockSlipManager handle animation + tracking
                    blockSlipManager.BeginDrop(tile, from, to);
                }

            // Wait for the longest drop to complete (plus a small buffer)
            if (drops.Count > 0)
            {
                var waitTime = dropDuration * maxDropDistance + 0.05f;
                Debug.Log($"[DropTiles] Waiting {waitTime:F2}s for drops to complete");
                yield return new WaitForSeconds(waitTime);
            }

            Debug.Log("[DropTiles] ========== DROP CHECK COMPLETE ==========");
        }

        #endregion

        #region Inspector Fields

        [Header("Grid Settings")] public int gridWidth = 6;
        public int gridHeight = 14;
        public float tileSize = 1f;

        [Header("Grid Initialization")] public int initialFillRows = 4; // How many rows to preload at start

        public int preloadRows = 2; // Extra rows preloaded below visible grid

        [Header("Swap Settings")] public float swapDuration = 0.15f; // Time in seconds for swap animation

        [Header("Drop Settings")] public float dropDuration = 0.15f; // Time in seconds PER TILE UNIT (gravity speed)

        [Header("Components")] public CursorController cursorController;
        public GridRiser gridRiser;
        public MatchDetector matchDetector;
        public MatchProcessor matchProcessor;
        public TileSpawner tileSpawner;
        public BlockSlipManager blockSlipManager;

        #endregion

        #region Internal State

        private GameObject[,] _grid; // Main grid of tiles
        private GameObject[,] _preloadGrid; // Extra rows below main grid

        // Animated Tile Tracking
        private readonly HashSet<GameObject> _swappingTiles = new(); // Tiles currently being swapped
        private readonly HashSet<GameObject> _droppingTiles = new(); // Tiles currently dropping

        #endregion

        #region Public Properties / Queries

        public bool IsSwapping { get; private set; }

        public bool IsTileSwapping(GameObject tile)
        {
            return _swappingTiles.Contains(tile);
        }

        public bool IsTileDropping(GameObject tile)
        {
            return _droppingTiles.Contains(tile);
        }

        public bool IsTileAnimating(GameObject tile)
        {
            return _swappingTiles.Contains(tile) || _droppingTiles.Contains(tile);
        }

        public void SetIsSwapping(bool value)
        {
            IsSwapping = value;
        }

        // Called by CursorController.Swap()
        public void RequestSwapAtCursor()
        {
            if (!IsSwapping) StartCoroutine(SwapCursorTiles());
        }

        /// <summary>
        /// Finds the grid position of a given tile GameObject
        /// </summary>
        public Vector2Int? FindTilePosition(GameObject tile)
        {
            if (tile == null) return null;

            for (var x = 0; x < gridWidth; x++)
            for (var y = 0; y < gridHeight; y++)
                if (_grid[x, y] == tile)
                    return new Vector2Int(x, y);

            return null;
        }

        public void AddBreathingRoom(int tilesMatched)
        {
            gridRiser.AddBreathingRoom(tilesMatched);
        }
        
        /// <summary>
        /// Get the grid array (for debug tools and external systems)
        /// </summary>
        public GameObject[,] GetGrid()
        {
            return _grid;
        }

        #endregion

        #region Unity Lifecycle & Initialization

        private void Start()
        {
            _grid = new GameObject[gridWidth, gridHeight];
            _preloadGrid = new GameObject[gridWidth, preloadRows];

            // Initialize all components
            tileSpawner.Initialize(this, tileSize, _grid, _preloadGrid, gridWidth, gridHeight, preloadRows);
            cursorController.Initialize(this, tileSize, gridWidth, gridHeight, tileSpawner, gridRiser);
            matchDetector.Initialize(_grid, gridWidth, gridHeight);
            matchProcessor.Initialize(this, _grid, matchDetector);
            gridRiser.Initialize(this, _grid, _preloadGrid, tileSpawner, cursorController, matchDetector, matchProcessor,
                tileSize, gridWidth, gridHeight);

            // Initialize BlockSlipManager
            blockSlipManager.Initialize(
                this,
                _grid,
                gridWidth,
                gridHeight,
                tileSize,
                swapDuration,
                dropDuration,
                gridRiser,
                matchDetector,
                matchProcessor,
                cursorController,
                _swappingTiles,
                _droppingTiles
            );

            // Create background and initialize grid
            tileSpawner.CreateBackgroundTiles();
            StartCoroutine(InitializeGrid());
        }

        private IEnumerator InitializeGrid()
        {
            // Fill preload rows (below visible grid)
            for (var y = 0; y < preloadRows; y++)
            for (var x = 0; x < gridWidth; x++)
            {
                tileSpawner.SpawnPreloadTile(x, y, gridRiser.CurrentGridOffset);
                yield return new WaitForSeconds(0.01f);
            }

            // Fill bottom rows of main grid
            for (var y = 0; y < initialFillRows; y++)
            for (var x = 0; x < gridWidth; x++)
            {
                tileSpawner.SpawnTile(x, y, gridRiser.CurrentGridOffset);
                yield return new WaitForSeconds(0.02f);
            }

            // Check for initial matches
            yield return StartCoroutine(matchProcessor.CheckAndClearMatches());

            // Start rising
            gridRiser.StartRising();
        }

        private void Update()
        {
            if (gridRiser.IsGameOver) return;

            // Don't process input when game is paused (Time.timeScale handles animation pausing)
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused)
                return;

            // Clean up only destroyed (null) tiles from animation sets
            _swappingTiles.RemoveWhere(tile => tile == null);
            _droppingTiles.RemoveWhere(tile => tile == null);

            // Clean up Block Slip tracking dictionaries inside BlockSlipManager
            blockSlipManager.CleanupTracking();

            // Handle cursor input
            cursorController.HandleInput();

            // Optional debug indicator
            // blockSlipManager.UpdateBlockSlipIndicator();

            HandleSwapInput();
            gridRiser.DisplayDebugInfo();
        }

        #endregion

        #region Input & Swap Handling

        private void HandleSwapInput()
        {
            if (IsSwapping) return;

            // Swap with Z (primary), K (alternate), or Space
            if (!(Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Space)))
                return;

            var cursorPos = cursorController.CursorPosition;
            var leftX = cursorPos.x;
            var rightX = cursorPos.x + 1;
            var y = cursorPos.y;

            var leftTile = _grid[leftX, y];
            var rightTile = _grid[rightX, y];

            var leftTileScript = leftTile != null ? leftTile.GetComponent<Tile>() : null;
            var rightTileScript = rightTile != null ? rightTile.GetComponent<Tile>() : null;

            var leftFalling = leftTileScript != null && leftTileScript.State == Tile.MovementState.Falling;
            var rightFalling = rightTileScript != null && rightTileScript.State == Tile.MovementState.Falling;

            var leftIdle = leftTileScript == null || leftTileScript.State == Tile.MovementState.Idle;
            var rightIdle = rightTileScript == null || rightTileScript.State == Tile.MovementState.Idle;

            // Check if tiles are being processed (matched)
            var leftProcessed = matchProcessor.IsTileBeingProcessed(leftX, y);
            var rightProcessed = matchProcessor.IsTileBeingProcessed(rightX, y);

            if (leftProcessed || rightProcessed)
            {
                Debug.Log(">>> SWAP BLOCKED - tiles being processed <<<");
                return;
            }

            // Check if tiles are currently swapping
            var leftSwapping = leftTile != null && _swappingTiles.Contains(leftTile);
            var rightSwapping = rightTile != null && _swappingTiles.Contains(rightTile);

            if (leftSwapping || rightSwapping)
            {
                Debug.Log(">>> SWAP BLOCKED - tiles swapping <<<");
                return;
            }

            // Check if tiles are active (above visibility threshold)
            var leftActive = leftTile == null || tileSpawner.IsTileActive(leftTile, gridRiser.CurrentGridOffset);
            var rightActive = rightTile == null || tileSpawner.IsTileActive(rightTile, gridRiser.CurrentGridOffset);

            if (!leftActive || !rightActive)
            {
                Debug.Log(">>> SWAP BLOCKED - tiles not yet active <<<");
                return;
            }

            // Case 1: exactly one tile falling at cursor -> this MUST be a BlockSlip kick-under
            if (leftFalling ^ rightFalling)
            {
                if (blockSlipManager.TryKickUnderFallingAtCursor(cursorPos))
                    // BlockSlip handled it; no normal swap
                    return;

                Debug.Log(">>> SWAP BLOCKED - one tile falling, BlockSlip not available <<<");
                return;
            }

            // Case 2: both tiles idle (or null) -> try "falling higher up" BlockSlip first
            if (leftIdle && rightIdle)
                if (blockSlipManager.TryHandleBlockSlipAtCursor(cursorPos))
                    // BlockSlip handled it
                    return;

            // Check if any falling blocks in either column are past the 50% threshold
            if (blockSlipManager.IsBlockSlipTooLate(leftX, y) || blockSlipManager.IsBlockSlipTooLate(rightX, y))
            {
                Debug.Log(">>> SWAP BLOCKED - falling block past 50% threshold <<<");
                return;
            }

            // Fallback: normal swap, but only if not dropping at cursor
            var leftDropping = leftTile != null && _droppingTiles.Contains(leftTile);
            var rightDropping = rightTile != null && _droppingTiles.Contains(rightTile);

            if (!leftDropping && !rightDropping)
                StartCoroutine(SwapCursorTiles());
            else
                Debug.Log(">>> SWAP BLOCKED - tiles dropping at cursor <<<");
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

            // Mark tiles as swapping BEFORE updating grid array to prevent race conditions
            if (leftTile != null) _swappingTiles.Add(leftTile);
            if (rightTile != null) _swappingTiles.Add(rightTile);

            // Update grid array after protection is applied
            _grid[leftX, y] = rightTile;
            _grid[rightX, y] = leftTile;

            try
            {
                // Start swap animations WITHOUT updating grid coordinates yet
                if (leftTile != null) blockSlipManager.StartSwapAnimation(leftTile, new Vector2Int(rightX, y));

                if (rightTile != null) blockSlipManager.StartSwapAnimation(rightTile, new Vector2Int(leftX, y));

                yield return new WaitForSeconds(swapDuration);

                // NOW update grid coordinates after animation completes
                var leftPos = FindTilePosition(leftTile);
                var rightPos = FindTilePosition(rightTile);

                if (leftTile != null && leftPos.HasValue)
                {
                    var tile = leftTile.GetComponent<Tile>();
                    tile.Initialize(leftPos.Value.x, leftPos.Value.y, tile.TileType, this);
                    leftTile.transform.position =
                        new Vector3(leftPos.Value.x * tileSize,
                            leftPos.Value.y * tileSize + gridRiser.CurrentGridOffset, 0);
                }

                if (rightTile != null && rightPos.HasValue)
                {
                    var tile = rightTile.GetComponent<Tile>();
                    tile.Initialize(rightPos.Value.x, rightPos.Value.y, tile.TileType, this);
                    rightTile.transform.position =
                        new Vector3(rightPos.Value.x * tileSize,
                            rightPos.Value.y * tileSize + gridRiser.CurrentGridOffset, 0);
                }

                // Swap animation is complete
                IsSwapping = false;

                // Check if swap placed blocks in path of falling blocks and handle mid-air interception
                var midAirInterceptHandled = false;
                if (leftPos.HasValue && rightPos.HasValue)
                    midAirInterceptHandled = blockSlipManager.HandleMidAirSwapInterception(
                        leftTile, rightTile, leftPos.Value, rightPos.Value);

                // If mid-air intercept was handled, wait for cascade animation
                if (midAirInterceptHandled)
                    yield return new WaitForSeconds(dropDuration * 0.5f); // Wait for quick nudge cascade

                // Drop any tiles that can fall after swap
                yield return StartCoroutine(DropTiles());

                // Check entire grid for matches since tiles may have dropped far from original position
                var matches = matchDetector.GetAllMatches();
                if (matches.Count > 0 && !matchProcessor.IsProcessingMatches)
                    StartCoroutine(matchProcessor.ProcessMatches(matches));
            }
            finally
            {
                _swappingTiles.Remove(leftTile);
                _swappingTiles.Remove(rightTile);
                IsSwapping = false;
            }
        }

        #endregion
    }
}