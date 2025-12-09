using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace PuzzleAttack.Grid
{
    public class GridRiser : MonoBehaviour
    {
        [FormerlySerializedAs("normalRiseSpeed")][Header("Rising Mechanics")]
        public float baseRiseSpeed = 0.1f; // Base units per second at level 1
        public float fastRiseMultiplier = 4f; // Multiplier when holding X
        public int speedLevel = 1; // Current speed level of the grid, goes up as time goes on
        public float speedLevelInterval = 60f; // Seconds between speed level increases
        public int maxSpeedLevel = 99; // Maximum speed level
        public float gracePeriod = 2f; // Seconds before game over when block reaches top

        [Header("Breathing Room")]
        public bool enableBreathingRoom = true; // Toggle breathing room feature
        public float breathingRoomPerTile = 0.2f; // Seconds of breathing room per tile matched
        public float maxBreathingRoom = 5f; // Maximum breathing room duration
        public float breathingRoomFlashSpeed = 2f; // Speed of text flash animation

        [Header("Catch-up Mechanics")]
        public float catchUpMultiplier = 1.5f; // Speed multiplier when catching up from pauses (1.5 = 50% faster)

        [Header("UI Elements")]
        [SerializeField]
        private TextMeshProUGUI timeDebtText;
        [SerializeField]
        private TextMeshProUGUI breathingRoomText;
        [SerializeField]
        private TextMeshProUGUI gracePeriodText;
        [SerializeField]
        private TextMeshProUGUI speedLevelText;
        [SerializeField]
        private GameObject breathingRoomImage; // Image to show when breathing room is active
        [SerializeField]
        private TextMeshProUGUI breathingRoomFlashText; // Text to flash when breathing room is active

        private float _breathingRoomTimer; // Time remaining before grid resumes rising
        private float _speedLevelTimer; // Time until next speed level increase
        private bool _isBreathingRoomActive; // Track if breathing room is currently active
        private Coroutine _breathingRoomFlashCoroutine; // Reference to the flash coroutine

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

            // Activate breathing room UI if not already active
            if (!_isBreathingRoomActive)
            {
                ActivateBreathingRoomUI();
            }
        }

        public void DisplayDebugInfo()
        {
            if (timeDebtText != null) timeDebtText.text = $"Time Debt: {_pausedTimeDebt:F2}s";

            if (breathingRoomText != null) breathingRoomText.text = $"Breathing Room: {_breathingRoomTimer:F2}s";

            if (gracePeriodText != null)
                gracePeriodText.text = IsInGracePeriod ? $"Grace Period: {_gracePeriodTimer:F2}s" : "Grace Period: N/A";

            if (speedLevelText != null)
                speedLevelText.text = speedLevel.ToString();
        }

        private IEnumerator RiseGrid()
        {
            while (!IsGameOver)
            {
                // Update speed level timer - always ticks during gameplay (pauses automatically when game is paused via Time.timeScale)
                _speedLevelTimer += Time.deltaTime;

                // Debug log every 10 seconds to track progress
                if (Mathf.FloorToInt(_speedLevelTimer) % 10 == 0 && Mathf.FloorToInt(_speedLevelTimer - Time.deltaTime) % 10 != 0)
                {
                    Debug.Log($"Speed level timer: {_speedLevelTimer:F1}s / {speedLevelInterval}s (Level {speedLevel})");
                }

                if (_speedLevelTimer >= speedLevelInterval && speedLevel < maxSpeedLevel)
                {
                    speedLevel++;
                    _speedLevelTimer = 0f;
                    Debug.Log($"Speed Level increased to {speedLevel}! Grid speed: {GetSpeedForLevel():F3} units/s");
                }

                // Handle breathing room countdown (only when not processing matches)
                if (_breathingRoomTimer > 0f && !_matchProcessor.isProcessingMatches)
                {
                    _breathingRoomTimer -= Time.deltaTime;
                    if (_breathingRoomTimer <= 0f)
                    {
                        _breathingRoomTimer = 0f;
                        DeactivateBreathingRoomUI();
                    }
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
                else if (_breathingRoomTimer > 0f)
                {
                    // Pause for breathing room, don't accumulate time debt
                    // (player earned this break through combos)
                }
                else
                {
                    // Grid is rising - calculate speed based on current level
                    var levelSpeed = GetSpeedForLevel();
                    var baseSpeed = Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.L) || Input.GetKey(KeyCode.LeftShift)
                        ? levelSpeed * fastRiseMultiplier
                        : levelSpeed;

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

        /// <summary>
        /// Calculates the grid rise speed based on the current speed level.
        /// Uses an exponential curve from level 1 (slow, beginner-friendly) to level 99 (Tetris GM speed).
        /// </summary>
        private float GetSpeedForLevel()
        {
            if (speedLevel <= 1) return baseRiseSpeed;

            // Exponential growth: 80x speed increase from level 1 to 99
            // Level 1: baseRiseSpeed (0.1 units/s)
            // Level 99: baseRiseSpeed * 80 (8 units/s)
            float speedMultiplier = 80f;
            float normalizedLevel = (speedLevel - 1) / (float)(maxSpeedLevel - 1);
            float speedFactor = Mathf.Pow(speedMultiplier, normalizedLevel);

            return baseRiseSpeed * speedFactor;
        }

        private void IncreaseSpeedLevel(int amount)
        {
            speedLevel+=amount;
        }
        
        private void DecreaseSpeedLevel(int amount)
        {
            speedLevel-=amount;
        }

        private void SetSpeedLevel(int amount)
        {
            speedLevel = amount;
        }

        private void ActivateBreathingRoomUI()
        {
            _isBreathingRoomActive = true;

            // Activate the image
            if (breathingRoomImage != null)
            {
                breathingRoomImage.SetActive(true);
            }

            // Start flashing the text
            if (breathingRoomFlashText != null)
            {
                breathingRoomFlashText.gameObject.SetActive(true);
                if (_breathingRoomFlashCoroutine != null)
                {
                    StopCoroutine(_breathingRoomFlashCoroutine);
                }
                _breathingRoomFlashCoroutine = StartCoroutine(FlashBreathingRoomText());
            }

            Debug.Log("Breathing room UI activated!");
        }

        private void DeactivateBreathingRoomUI()
        {
            _isBreathingRoomActive = false;

            // Deactivate the image
            if (breathingRoomImage != null)
            {
                breathingRoomImage.SetActive(false);
            }

            // Stop flashing the text
            if (_breathingRoomFlashCoroutine != null)
            {
                StopCoroutine(_breathingRoomFlashCoroutine);
                _breathingRoomFlashCoroutine = null;
            }

            // Hide the text
            if (breathingRoomFlashText != null)
            {
                breathingRoomFlashText.gameObject.SetActive(false);
            }

            Debug.Log("Breathing room UI deactivated!");
        }

        private IEnumerator FlashBreathingRoomText()
        {
            if (breathingRoomFlashText == null) yield break;

            Color originalColor = breathingRoomFlashText.color;

            while (_isBreathingRoomActive)
            {
                // Fade out
                float alpha = 1f;
                while (alpha > 0f)
                {
                    alpha -= Time.deltaTime * breathingRoomFlashSpeed;
                    breathingRoomFlashText.color = new Color(originalColor.r, originalColor.g, originalColor.b, Mathf.Max(0f, alpha));
                    yield return null;
                }

                // Fade in
                while (alpha < 1f)
                {
                    alpha += Time.deltaTime * breathingRoomFlashSpeed;
                    breathingRoomFlashText.color = new Color(originalColor.r, originalColor.g, originalColor.b, Mathf.Min(1f, alpha));
                    yield return null;
                }
            }

            // Restore original color when done
            breathingRoomFlashText.color = originalColor;
        }
    }
}