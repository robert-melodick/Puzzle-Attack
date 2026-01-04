using System.Collections;
using TMPro;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Controls grid rising mechanics, speed levels, breathing room, and game over detection.
    /// Handles both regular tiles and garbage blocks.
    /// All positions are relative to the GridManager's transform position.
    /// Reports eliminations to MatchScoreTracker for VS mode support.
    /// </summary>
    public class GridRiser : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Rising Mechanics")]
        public float baseRiseSpeed = 0.1f;
        public float fastRiseMultiplier = 4f;
        public int speedLevel = 1;
        public float speedLevelInterval = 60f;
        public int maxSpeedLevel = 99;
        public float gracePeriod = 2f;

        [Header("Breathing Room")]
        public bool enableBreathingRoom = true;
        public float breathingRoomPerTile = 0.2f;
        public float maxBreathingRoom = 5f;
        public float breathingRoomFlashSpeed = 2f;

        [Header("Catch-up")]
        public float catchUpMultiplier = 1.5f;

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI _timeDebtText;
        [SerializeField] private TextMeshProUGUI _breathingRoomText;
        [SerializeField] private TextMeshProUGUI _gracePeriodText;
        [SerializeField] private TextMeshProUGUI _speedLevelText;
        [SerializeField] private GameObject _breathingRoomImage;
        [SerializeField] private TextMeshProUGUI _breathingRoomFlashText;

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private GameObject[,] _grid;
        private GameObject[,] _preloadGrid;
        private TileSpawner _tileSpawner;
        private CursorController _cursorController;
        private MatchDetector _matchDetector;
        private MatchProcessor _matchProcessor;
        private GarbageManager _garbageManager;

        private float _tileSize;
        private int _gridWidth;
        private int _gridHeight;

        private float _breathingRoomTimer;
        private float _speedLevelTimer;
        private float _gracePeriodTimer = 1.5f;
        private float _pausedTimeDebt;
        private float _nextRowSpawnOffset;
        private bool _hasBlockAtTop;
        private bool _isBreathingRoomActive;
        private Coroutine _breathingRoomFlashCoroutine;

        // Player identification for VS modes
        private int _playerIndex = 0;

        #endregion

        #region Properties

        public float CurrentGridOffset { get; private set; }
        public bool IsInGracePeriod { get; private set; }
        public bool IsGameOver { get; private set; }
        
        /// <summary>
        /// The player index this grid belongs to (0-3).
        /// Set by GameplaySceneInitializer.
        /// </summary>
        public int PlayerIndex
        {
            get => _playerIndex;
            set => _playerIndex = value;
        }

        #endregion

        #region Initialization

        public void Initialize(GridManager manager, GameObject[,] grid, GameObject[,] preloadGrid,
            TileSpawner spawner, CursorController cursor, MatchDetector detector,
            MatchProcessor processor, GarbageManager garbageMgr, float tileSize, int gridWidth, int gridHeight)
        {
            _gridManager = manager;
            _grid = grid;
            _preloadGrid = preloadGrid;
            _tileSpawner = spawner;
            _cursorController = cursor;
            _matchDetector = detector;
            _matchProcessor = processor;
            _garbageManager = garbageMgr;
            _tileSize = tileSize;
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
        }

        public void StartRising() => StartCoroutine(RiseLoop());
        public void StopRising() => StopCoroutine(RiseLoop());

        #endregion

        #region Public API

        /// <summary>
        /// Whether fast rise is currently active (set by player input or AI).
        /// </summary>
        public bool IsFastRising { get; set; }

        /// <summary>
        /// Request fast rise mode. Called by CursorController for player input.
        /// </summary>
        public void RequestFastRise()
        {
            if (!_gridManager.IsSwapping && !_matchProcessor.IsProcessingMatches)
                IsFastRising = true;
        }

        /// <summary>
        /// Stop fast rise mode.
        /// </summary>
        public void StopFastRise()
        {
            IsFastRising = false;
        }

        public void AddBreathingRoom(int tilesMatched)
        {
            if (!enableBreathingRoom) return;

            var additionalTime = tilesMatched * breathingRoomPerTile;
            _breathingRoomTimer = Mathf.Min(_breathingRoomTimer + additionalTime, maxBreathingRoom);
            Debug.Log($"Breathing room: +{additionalTime:F2}s ({_breathingRoomTimer:F2}s total)");

            if (!_isBreathingRoomActive)
                ActivateBreathingRoomUI();
        }

        public void DisplayDebugInfo()
        {
            if (_timeDebtText != null)
                _timeDebtText.text = $"Time Debt: {_pausedTimeDebt:F2}s";
            if (_breathingRoomText != null)
                _breathingRoomText.text = $"Breathing Room: {_breathingRoomTimer:F2}s";
            if (_gracePeriodText != null)
                _gracePeriodText.text = IsInGracePeriod ? $"Grace Period: {_gracePeriodTimer:F2}s" : "Grace Period: N/A";
            if (_speedLevelText != null)
                _speedLevelText.text = speedLevel.ToString();
        }

        #endregion

        #region Rise Loop

        private IEnumerator RiseLoop()
        {
            while (!IsGameOver)
            {
                UpdateSpeedLevel();
                UpdateBreathingRoom();

                if (IsInGracePeriod)
                    HandleGracePeriod();
                else if (_gridManager.IsSwapping)
                    _pausedTimeDebt += Time.deltaTime;
                else if (_matchProcessor.IsProcessingMatches)
                { /* Pause without debt */ }
                else if (_breathingRoomTimer > 0f)
                { /* Pause for breathing room */ }
                else
                    PerformRise();

                yield return null;
            }
        }

        private void UpdateSpeedLevel()
        {
            _speedLevelTimer += Time.deltaTime;

            if (_speedLevelTimer >= speedLevelInterval && speedLevel < maxSpeedLevel)
            {
                speedLevel++;
                _speedLevelTimer = 0f;
                Debug.Log($"Speed Level increased to {speedLevel}! ({GetSpeedForLevel():F3} units/s)");
            }
        }

        private void UpdateBreathingRoom()
        {
            if (_breathingRoomTimer <= 0f || _matchProcessor.IsProcessingMatches) return;

            _breathingRoomTimer -= Time.deltaTime;
            if (_breathingRoomTimer <= 0f)
            {
                _breathingRoomTimer = 0f;
                DeactivateBreathingRoomUI();
            }
        }

        private void HandleGracePeriod()
        {
            CheckTopRow();

            if (!_matchProcessor.IsProcessingMatches)
            {
                _gracePeriodTimer -= Time.deltaTime;
                Debug.Log($"Grace Period: {_gracePeriodTimer:F2}s");
            }

            if (_gracePeriodTimer <= 0f)
                TriggerGameOver();
            else if (!_hasBlockAtTop)
            {
                IsInGracePeriod = false;
                _gracePeriodTimer = gracePeriod;
                Debug.Log("Grace period ended - top cleared!");
            }
        }

        private void PerformRise()
        {
            var levelSpeed = GetSpeedForLevel();
            
            // Use the IsFastRising flag instead of direct input check
            // This allows per-grid control (player input sets their grid's flag, AI sets its grid's flag)
            var baseSpeed = IsFastRising ? levelSpeed * fastRiseMultiplier : levelSpeed;

            var speedMultiplier = _pausedTimeDebt > 0f ? catchUpMultiplier : 1.0f;
            var riseSpeed = baseSpeed * speedMultiplier;
            var riseAmount = riseSpeed * Time.deltaTime;

            // Pay off time debt
            if (_pausedTimeDebt > 0f && speedMultiplier > 1.0f)
            {
                var extraSpeed = baseSpeed * (speedMultiplier - 1.0f);
                var debtPaidOff = extraSpeed * Time.deltaTime / baseSpeed;
                _pausedTimeDebt = Mathf.Max(0f, _pausedTimeDebt - debtPaidOff);
            }

            CurrentGridOffset += riseAmount;
            _nextRowSpawnOffset += riseAmount;

            UpdateTilePositions();
            
            if (_cursorController != null)
            {
                _cursorController.UpdateCursorPosition(CurrentGridOffset);
            }

            // Spawn new row when risen enough
            if (_nextRowSpawnOffset >= _tileSize)
            {
                _nextRowSpawnOffset -= _tileSize;
                CurrentGridOffset -= _tileSize;
                _tileSpawner.SpawnRowAtBottom(CurrentGridOffset, _cursorController);
                UpdateTilePositions();

                // Notify garbage manager that grid shifted up
                if (_garbageManager != null)
                {
                    _garbageManager.OnGridShiftedUp();
                }

                var matches = _matchDetector.GetAllMatches();
                if (matches.Count > 0 && !_matchProcessor.IsProcessingMatches)
                    StartCoroutine(_matchProcessor.CheckAndClearMatches());
            }

            CheckTopRow();
        }

        private void UpdateTilePositions()
        {
            // Update main grid - handle both tiles and garbage
            for (var x = 0; x < _gridWidth; x++)
            {
                for (var y = 0; y < _gridHeight; y++)
                {
                    var cell = _grid[x, y];
                    if (cell == null) continue;

                    // Skip animating tiles
                    if (_gridManager.IsTileAnimating(cell)) continue;

                    // Check if it's a regular tile
                    var tile = cell.GetComponent<Tile>();
                    if (tile != null)
                    {
                        // Use GridManager's position helper for proper offset
                        cell.transform.position = _gridManager.GridToWorldPosition(tile.GridX, tile.GridY, CurrentGridOffset);
                        _tileSpawner.UpdateTileActiveState(cell, tile.GridY, CurrentGridOffset);
                        continue;
                    }

                    // Check if it's garbage (only update the anchor, not references)
                    var garbageBlock = cell.GetComponent<GarbageBlock>();
                    if (garbageBlock != null && !garbageBlock.IsFalling && !garbageBlock.IsConverting && !garbageBlock.IsShrinking)
                    {
                        // Use GridManager's position helper for proper offset
                        cell.transform.position = _gridManager.GridToWorldPosition(
                            garbageBlock.AnchorPosition.x, 
                            garbageBlock.AnchorPosition.y, 
                            CurrentGridOffset);
                        continue;
                    }

                    // Handle shrinking garbage - keep at original position
                    if (garbageBlock != null && garbageBlock.IsShrinking)
                    {
                        garbageBlock.UpdateVisualPosition(CurrentGridOffset);
                        continue;
                    }

                    // GarbageReference objects don't need position updates - they're children of the GarbageBlock
                    // or their position is relative to the owner
                }
            }

            // Update preload tiles
            for (var x = 0; x < _gridWidth; x++)
            {
                for (var y = 0; y < _preloadGrid.GetLength(1); y++)
                {
                    var cell = _preloadGrid[x, y];
                    if (cell == null) continue;

                    var tile = cell.GetComponent<Tile>();
                    if (tile != null)
                    {
                        // Use GridManager's position helper for proper offset
                        cell.transform.position = _gridManager.GridToWorldPosition(tile.GridX, tile.GridY, CurrentGridOffset);
                        _tileSpawner.UpdateTileActiveState(cell, tile.GridY, CurrentGridOffset);
                    }
                }
            }
        }

        #endregion

        #region Game State

        private void CheckTopRow()
        {
            _hasBlockAtTop = false;

            for (var x = 0; x < _gridWidth; x++)
            {
                var cell = _grid[x, _gridHeight - 1];
                if (cell == null) continue;

                // Calculate world Y using grid origin
                var topRowWorldY = _gridManager.GridToWorldPosition(0, _gridHeight - 1, CurrentGridOffset).y;
                var thresholdY = _gridManager.GridToWorldPosition(0, _gridHeight - 1, 0).y;
                
                if (topRowWorldY >= thresholdY)
                {
                    _hasBlockAtTop = true;
                    if (!IsInGracePeriod)
                    {
                        IsInGracePeriod = true;
                        _gracePeriodTimer = gracePeriod;
                        Debug.Log("Block reached top! Grace period started!");
                    }
                    break;
                }
            }
        }

        private void TriggerGameOver()
        {
            IsGameOver = true;
            Debug.Log($"GAME OVER for Player {_playerIndex}!");

            // Report to MatchScoreTracker for VS mode handling
            if (MatchScoreTracker.Instance != null)
            {
                MatchScoreTracker.Instance.EliminatePlayer(_playerIndex);
            }
            else
            {
                // Fallback to old behavior if no tracker exists
                if (GameStateManager.Instance != null)
                    GameStateManager.Instance.TriggerGameOver();
            }
        }

        /// <summary>
        /// Exponential speed curve from level 1 to 99.
        /// </summary>
        private float GetSpeedForLevel()
        {
            if (speedLevel <= 1) return baseRiseSpeed;

            const float speedMultiplier = 80f;
            var normalizedLevel = (speedLevel - 1) / (float)(maxSpeedLevel - 1);
            var speedFactor = Mathf.Pow(speedMultiplier, normalizedLevel);

            return baseRiseSpeed * speedFactor;
        }

        #endregion

        #region UI

        private void ActivateBreathingRoomUI()
        {
            _isBreathingRoomActive = true;

            if (_breathingRoomImage != null)
                _breathingRoomImage.SetActive(true);

            if (_breathingRoomFlashText != null)
            {
                _breathingRoomFlashText.gameObject.SetActive(true);
                if (_breathingRoomFlashCoroutine != null)
                    StopCoroutine(_breathingRoomFlashCoroutine);
                _breathingRoomFlashCoroutine = StartCoroutine(FlashBreathingRoomText());
            }

            Debug.Log("Breathing room UI activated");
        }

        private void DeactivateBreathingRoomUI()
        {
            _isBreathingRoomActive = false;

            if (_breathingRoomImage != null)
                _breathingRoomImage.SetActive(false);

            if (_breathingRoomFlashCoroutine != null)
            {
                StopCoroutine(_breathingRoomFlashCoroutine);
                _breathingRoomFlashCoroutine = null;
            }

            if (_breathingRoomFlashText != null)
                _breathingRoomFlashText.gameObject.SetActive(false);

            Debug.Log("Breathing room UI deactivated");
        }

        private IEnumerator FlashBreathingRoomText()
        {
            if (_breathingRoomFlashText == null) yield break;

            var originalColor = _breathingRoomFlashText.color;

            while (_isBreathingRoomActive)
            {
                // Fade out
                var alpha = 1f;
                while (alpha > 0f)
                {
                    alpha -= Time.deltaTime * breathingRoomFlashSpeed;
                    _breathingRoomFlashText.color = new Color(originalColor.r, originalColor.g, originalColor.b, Mathf.Max(0f, alpha));
                    yield return null;
                }

                // Fade in
                while (alpha < 1f)
                {
                    alpha += Time.deltaTime * breathingRoomFlashSpeed;
                    _breathingRoomFlashText.color = new Color(originalColor.r, originalColor.g, originalColor.b, Mathf.Min(1f, alpha));
                    yield return null;
                }
            }

            _breathingRoomFlashText.color = originalColor;
        }

        #endregion
    }
}