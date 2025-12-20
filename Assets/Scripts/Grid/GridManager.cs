using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Central grid coordinator. Manages tile array, swap requests, and drop logic.
    /// Delegates animation to TileAnimator and movement logic to BlockSlipManager.
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
        public float dropDuration = 0.15f;

        [Header("Components")]
        public CursorController cursorController;
        public GridRiser gridRiser;
        public MatchDetector matchDetector;
        public MatchProcessor matchProcessor;
        public TileSpawner tileSpawner;
        public BlockSlipManager blockSlipManager;

        #endregion

        #region Internal State

        private GameObject[,] _grid;
        private GameObject[,] _preloadGrid;
        private readonly HashSet<GameObject> _swappingTiles = new();
        private readonly HashSet<GameObject> _droppingTiles = new();

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
                swapDuration, dropDuration, gridRiser, matchDetector, matchProcessor,
                cursorController, _swappingTiles, _droppingTiles);
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
        /// Check if a tile at position can be swapped (considering status effects).
        /// </summary>
        public bool CanTileSwap(int x, int y)
        {
            var tile = _grid[x, y];
            if (tile == null) return true; // Empty space can always "swap"
            
            var tileScript = tile.GetComponent<Tile>();
            if (tileScript == null) return true;
            
            return tileScript.CanSwap();
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
                    yield return new WaitForSeconds(dropDuration * 0.5f);

                yield return StartCoroutine(DropTiles());

                // Check for matches
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

        private void FinalizeSwappedTile(GameObject tileObj)
        {
            if (tileObj == null) return;
            
            var pos = FindTilePosition(tileObj);
            if (!pos.HasValue) return;

            var tile = tileObj.GetComponent<Tile>();
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
            var drops = CollectDrops();
            if (drops.Count == 0) yield break;

            ValidateDropTargets(drops);
            var maxDropDistance = ExecuteDrops(drops);

            if (drops.Count > 0)
            {
                var waitTime = dropDuration * maxDropDistance + 0.05f;
                Debug.Log($"[DropTiles] Waiting {waitTime:F2}s for {drops.Count} drops");
                yield return new WaitForSeconds(waitTime);
            }

            Debug.Log("[DropTiles] Complete");
        }

        private List<(GameObject tile, Vector2Int from, Vector2Int to)> CollectDrops()
        {
            var drops = new List<(GameObject tile, Vector2Int from, Vector2Int to)>();

            for (var x = 0; x < gridWidth; x++)
            for (var y = 0; y < gridHeight; y++)
            {
                if (_grid[x, y] != null) continue;

                for (var aboveY = y + 1; aboveY < gridHeight; aboveY++)
                {
                    if (_grid[x, aboveY] == null) continue;

                    var tile = _grid[x, aboveY];

                    // Skip already dropping tiles
                    if (_droppingTiles.Contains(tile))
                    {
                        Debug.Log($"[DropTiles] Tile at ({x}, {aboveY}) already dropping, stopping column search");
                        break;
                    }

                    // Skip tiles being processed
                    if (matchProcessor.IsTileBeingProcessed(x, aboveY))
                    {
                        Debug.Log($"[DropTiles] Tile at ({x}, {aboveY}) being processed, stopping column search");
                        break;
                    }

                    drops.Add((tile, new Vector2Int(x, aboveY), new Vector2Int(x, y)));
                    _grid[x, y] = tile;
                    _grid[x, aboveY] = null;
                    break;
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
                    Debug.LogError($"[DropTiles] CONFLICT: Multiple tiles targeting ({to.x}, {to.y})!");
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
                tile.Initialize(to.x, to.y, tile.TileType, this);
                blockSlipManager.BeginDrop(tileObj, from, to);
            }

            return maxDistance;
        }

        #endregion
    }
}