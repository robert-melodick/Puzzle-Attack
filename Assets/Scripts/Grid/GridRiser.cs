using System.Collections;
using TMPro;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    public class GridRiser : MonoBehaviour
    {
        [Header("Rising Mechanics")] public float normalRiseSpeed = 0.5f; // Units per second
        public float fastRiseSpeed = 2f; // Units per second when holding X
        public float gracePeriod = 2f; // Seconds before game over when block reaches top

        [Header("Breathing Room")] public bool enableBreathingRoom = true; // Toggle breathing room feature
        public float breathingRoomPerTile = 0.2f; // Seconds of breathing room per tile matched
        public float maxBreathingRoom = 5f; // Maximum breathing room duration

        [Header("Catch-up Mechanics")]
        public float catchUpMultiplier = 1.5f; // Speed multiplier when catching up from pauses (1.5 = 50% faster)

        [SerializeField] private TextMeshProUGUI timeDebtText;
        [SerializeField] private TextMeshProUGUI breathingRoomText;
        [SerializeField] private TextMeshProUGUI gracePeriodText;
        private float _breathingRoomTimer; // Time remaining before grid resumes rising

        private CursorController _cursorController;
        private float _gracePeriodTimer = 1.5f;
        private GameObject[,] _grid;
        private int _gridHeight;

        private GridManager _gridManager;
        private int _gridWidth;
        private bool _hasBlockAtTop;
        private MatchDetector _matchDetector;
        private MatchProcessor _matchProcessor;
        private float _nextRowSpawnOffset; // When to spawn next row
        private float _pausedTimeDebt; // Accumulated time while paused (only from swapping)
        private GameObject[,] _preloadGrid;
        private float _tileSize;
        private TileSpawner _tileSpawner;

        public float CurrentGridOffset { get; private set; }

        public bool IsInGracePeriod { get; private set; }

        public bool IsGameOver { get; private set; }

        public void Initialize(GridManager manager, GameObject[,] grid, GameObject[,] preloadGrid, TileSpawner spawner,
            CursorController cursor, MatchDetector detector, MatchProcessor processor, float tileSize, int gridWidth,
            int gridHeight)
        {
            _gridManager = manager;
            this._grid = grid;
            this._preloadGrid = preloadGrid;
            _tileSpawner = spawner;
            _cursorController = cursor;
            _matchDetector = detector;
            _matchProcessor = processor;
            this._tileSize = tileSize;
            this._gridWidth = gridWidth;
            this._gridHeight = gridHeight;
        }

        // Called by CursorController.RiseGrid()
        public void RequestFastRise()
        {
            if (!_gridManager.IsSwapping && !_matchProcessor.isProcessingMatches) StartCoroutine(RiseGrid());
        }

        public void StartRising()
        {
            StartCoroutine(RiseGrid());
        }

        public void StopRising()
        {
            StopCoroutine(RiseGrid());
        }

        public void AddBreathingRoom(int tilesMatched)
        {
            if (!enableBreathingRoom) return;

            var additionalTime = tilesMatched * breathingRoomPerTile;
            _breathingRoomTimer = Mathf.Min(_breathingRoomTimer + additionalTime, maxBreathingRoom);
            Debug.Log(
                $"Breathing room: +{additionalTime:F2}s for {tilesMatched} tiles (total: {_breathingRoomTimer:F2}s)");
        }

        public void DisplayDebugInfo()
        {
            if (timeDebtText != null) timeDebtText.text = $"Time Debt: {_pausedTimeDebt:F2}s";

            if (breathingRoomText != null) breathingRoomText.text = $"Breathing Room: {_breathingRoomTimer:F2}s";

            if (gracePeriodText != null)
                gracePeriodText.text = IsInGracePeriod ? $"Grace Period: {_gracePeriodTimer:F2}s" : "Grace Period: N/A";
        }

        private IEnumerator RiseGrid()
        {
            while (!IsGameOver)
            {
                // Handle breathing room countdown
                if (_breathingRoomTimer > 0f)
                {
                    _breathingRoomTimer -= Time.deltaTime;
                    if (_breathingRoomTimer < 0f) _breathingRoomTimer = 0f;
                }

                // Handle grace period separately
                if (IsInGracePeriod)
                {
                    // Check if top row is still occupied (need to update hasBlockAtTop)
                    CheckTopRow();

                    // Only count down grace period timer if not processing matches
                    if (!_matchProcessor.isProcessingMatches)
                    {
                        _gracePeriodTimer -= Time.deltaTime;
                        Debug.Log($"Grace Period: {_gracePeriodTimer:F2}s remaining!");
                    }

                    if (_gracePeriodTimer <= 0f)
                    {
                        TriggerGameOver();
                    }
                    else if (!_hasBlockAtTop)
                    {
                        // Block was cleared, exit grace period and reset timer
                        IsInGracePeriod = false;
                        _gracePeriodTimer = gracePeriod; // Reset timer for next grace period
                        Debug.Log("Grace period ended - blocks cleared from top row!");
                    }
                }
                // Check if grid should be paused (excluding grace period which is handled above)
                else if (_gridManager.IsSwapping)
                {
                    // Accumulate time debt while paused from swapping
                    _pausedTimeDebt += Time.deltaTime;
                }
                else if (_matchProcessor.isProcessingMatches)
                {
                    // Pause for match processing, but don't accumulate time debt
                    // (player shouldn't be penalized for making matches)
                }
                else
                {
                    // Grid is rising - calculate speed with catch-up multiplier if we have debt
                    var baseSpeed =
                        Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.L) || Input.GetKey(KeyCode.LeftShift)
                            ? fastRiseSpeed
                            : normalRiseSpeed;

                    // Apply catch-up multiplier if we have time debt
                    var speedMultiplier = _pausedTimeDebt > 0f ? catchUpMultiplier : 1.0f;
                    var riseSpeed = baseSpeed * speedMultiplier;
                    var riseAmount = riseSpeed * Time.deltaTime;

                    // Pay off time debt based on extra distance traveled
                    if (_pausedTimeDebt > 0f && speedMultiplier > 1.0f)
                    {
                        var extraSpeed = baseSpeed * (speedMultiplier - 1.0f);
                        var debtPaidOff =
                            extraSpeed * Time.deltaTime / baseSpeed; // Time equivalent of extra distance
                        _pausedTimeDebt = Mathf.Max(0f, _pausedTimeDebt - debtPaidOff);
                    }

                    CurrentGridOffset += riseAmount;
                    _nextRowSpawnOffset += riseAmount;

                    // Update all tile positions (except tiles currently animating)
                    foreach (var tile in _grid)
                        if (tile != null && !_gridManager.IsTileAnimating(tile))
                        {
                            var tileScript = tile.GetComponent<Tile>();
                            tile.transform.position = new Vector3(
                                tileScript.GridX * _tileSize,
                                tileScript.GridY * _tileSize + CurrentGridOffset,
                                0
                            );
                            _tileSpawner.UpdateTileActiveState(tile, tileScript.GridY, CurrentGridOffset);
                        }

                    // Update preload tile positions
                    foreach (var tile in _preloadGrid)
                        if (tile != null)
                        {
                            var tileScript = tile.GetComponent<Tile>();
                            tile.transform.position = new Vector3(
                                tileScript.GridX * _tileSize,
                                tileScript.GridY * _tileSize + CurrentGridOffset,
                                0
                            );
                            _tileSpawner.UpdateTileActiveState(tile, tileScript.GridY, CurrentGridOffset);
                        }

                    _cursorController.UpdateCursorPosition(CurrentGridOffset);

                    // Spawn new row when grid has risen one tile height
                    if (_nextRowSpawnOffset >= _tileSize)
                    {
                        _nextRowSpawnOffset -= _tileSize;
                        CurrentGridOffset -= _tileSize;
                        _tileSpawner.SpawnRowAtBottom(CurrentGridOffset, _cursorController);

                        // Update positions after spawn (except tiles currently animating)
                        foreach (var tile in _grid)
                            if (tile != null && !_gridManager.IsTileAnimating(tile))
                            {
                                var tileScript = tile.GetComponent<Tile>();
                                tile.transform.position = new Vector3(
                                    tileScript.GridX * _tileSize,
                                    tileScript.GridY * _tileSize + CurrentGridOffset,
                                    0
                                );
                                _tileSpawner.UpdateTileActiveState(tile, tileScript.GridY, CurrentGridOffset);
                            }

                        // Update preload tile positions
                        foreach (var tile in _preloadGrid)
                            if (tile != null)
                            {
                                var tileScript = tile.GetComponent<Tile>();
                                tile.transform.position = new Vector3(
                                    tileScript.GridX * _tileSize,
                                    tileScript.GridY * _tileSize + CurrentGridOffset,
                                    0
                                );
                                _tileSpawner.UpdateTileActiveState(tile, tileScript.GridY, CurrentGridOffset);
                            }

                        // Check for matches after spawning new row (only if not already processing)
                        var matches = _matchDetector.GetAllMatches();
                        if (matches.Count > 0 && !_matchProcessor.IsProcessingMatches)
                            StartCoroutine(_matchProcessor.CheckAndClearMatches());
                    }

                    // Check if any block reached the top
                    CheckTopRow();
                }

                yield return null;
            }
        }

        private void CheckTopRow()
        {
            _hasBlockAtTop = false;

            // Check if any tile in the top row is at or above the danger threshold
            for (var x = 0; x < _gridWidth; x++)
                if (_grid[x, _gridHeight - 1] != null)
                {
                    // Calculate the actual world position of the top row
                    var topRowWorldY = (_gridHeight - 1) * _tileSize + CurrentGridOffset;

                    // Only trigger grace period if top row is actually at the top of the screen
                    // (gridHeight - 1) * tileSize is the maximum allowed position
                    if (topRowWorldY >= (_gridHeight - 1) * _tileSize)
                    {
                        _hasBlockAtTop = true;
                        if (!IsInGracePeriod)
                        {
                            IsInGracePeriod = true;
                            _gracePeriodTimer = gracePeriod;
                            Debug.Log("WARNING: Block reached the top! Grace period started!");
                        }

                        break;
                    }
                }
        }

        private void TriggerGameOver()
        {
            IsGameOver = true;
            Debug.Log("GAME OVER! Blocks reached the top!");

            // Trigger game over through GameStateManager
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.TriggerGameOver();
            }
        }
    }
}