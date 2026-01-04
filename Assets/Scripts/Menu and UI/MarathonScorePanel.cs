using UnityEngine;
using TMPro;

namespace PuzzleAttack.UI
{
    /// <summary>
    /// Displays live game statistics during Marathon mode.
    /// Shows high score, current score, elapsed time, and speed level.
    /// </summary>
    public class MarathonScorePanel : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Panel")]
        [SerializeField] private GameObject panel;

        [Header("Score Display")]
        [SerializeField] private TextMeshProUGUI highScoreLabel;
        [SerializeField] private TextMeshProUGUI highScoreText;
        [SerializeField] private TextMeshProUGUI currentScoreLabel;
        [SerializeField] private TextMeshProUGUI currentScoreText;

        [Header("Time Display")]
        [SerializeField] private TextMeshProUGUI timeLabel;
        [SerializeField] private TextMeshProUGUI timeText;

        [Header("Speed Display")]
        [SerializeField] private TextMeshProUGUI speedLabel;
        [SerializeField] private TextMeshProUGUI speedLevelText;

        [Header("References")]
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private PuzzleAttack.Grid.GridRiser gridRiser;

        [Header("Formatting")]
        [SerializeField] private string scoreFormat = "N0"; // Number with commas
        [SerializeField] private bool showMilliseconds = true;

        [Header("Visual Feedback")]
        [SerializeField] private Color normalScoreColor = Color.white;
        [SerializeField] private Color beatingHighScoreColor = new Color(1f, 0.84f, 0f); // Gold
        [SerializeField] private bool flashWhenBeatingHighScore = true;
        [SerializeField] private float flashSpeed = 2f;

        #endregion

        #region Private Fields

        private float _elapsedTime;
        private int _highScore;
        private bool _isBeatingHighScore;
        private float _flashTimer;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Try to auto-find references if not assigned
            if (scoreManager == null)
            {
                scoreManager = FindObjectOfType<ScoreManager>();
            }

            if (gridRiser == null && scoreManager != null)
            {
                gridRiser = scoreManager.gridRiser;
            }

            // Get high score
            if (HighScoreManager.Instance != null)
            {
                _highScore = HighScoreManager.Instance.HighScore;
            }

            // Initialize display
            UpdateHighScoreDisplay();
            UpdateScoreDisplay();
            UpdateTimeDisplay();
            UpdateSpeedDisplay();

            // Check if we should show the panel based on game mode
            CheckGameModeVisibility();
        }

        private void Update()
        {
            // Try to find ScoreManager if not yet assigned (handles late initialization)
            if (scoreManager == null)
            {
                scoreManager = FindObjectOfType<ScoreManager>();
                if (scoreManager != null && gridRiser == null)
                {
                    gridRiser = scoreManager.gridRiser;
                }
            }

            // Don't update if paused or game over
            if (GameStateManager.Instance != null)
            {
                if (GameStateManager.Instance.IsPaused || GameStateManager.Instance.IsGameOver)
                    return;
            }

            // Update elapsed time
            _elapsedTime += Time.deltaTime;

            // Update displays
            UpdateScoreDisplay();
            UpdateTimeDisplay();
            UpdateSpeedDisplay();

            // Handle high score flash effect
            if (_isBeatingHighScore && flashWhenBeatingHighScore)
            {
                _flashTimer += Time.deltaTime * flashSpeed;
                float alpha = (Mathf.Sin(_flashTimer * Mathf.PI * 2f) + 1f) / 2f;
                Color flashColor = Color.Lerp(normalScoreColor, beatingHighScoreColor, alpha);
                if (currentScoreText != null)
                {
                    currentScoreText.color = flashColor;
                }
            }
        }

        private void OnEnable()
        {
            // Subscribe to game state events
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnGameRestarted += HandleGameRestarted;
            }
        }

        private void OnDisable()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnGameRestarted -= HandleGameRestarted;
            }
        }

        #endregion

        #region Display Updates

        private void UpdateHighScoreDisplay()
        {
            if (highScoreText != null)
            {
                highScoreText.text = _highScore.ToString(scoreFormat);
            }
        }

        private void UpdateScoreDisplay()
        {
            if (scoreManager == null || currentScoreText == null) return;

            int currentScore = scoreManager.GetScore();
            currentScoreText.text = currentScore.ToString(scoreFormat);

            // Check if beating high score
            bool wasBeatingsHighScore = _isBeatingHighScore;
            _isBeatingHighScore = currentScore > _highScore && _highScore > 0;

            // Just started beating high score
            if (_isBeatingHighScore && !wasBeatingsHighScore)
            {
                _flashTimer = 0f;
                if (currentScoreText != null)
                {
                    currentScoreText.color = beatingHighScoreColor;
                }
            }
            // No longer beating (shouldn't happen, but just in case)
            else if (!_isBeatingHighScore && wasBeatingsHighScore)
            {
                if (currentScoreText != null)
                {
                    currentScoreText.color = normalScoreColor;
                }
            }
        }

        private void UpdateTimeDisplay()
        {
            if (timeText == null) return;

            int minutes = Mathf.FloorToInt(_elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(_elapsedTime % 60f);
            int milliseconds = Mathf.FloorToInt((_elapsedTime * 1000f) % 1000f);

            if (showMilliseconds)
            {
                timeText.text = $"{minutes:00}:{seconds:00}:{milliseconds:000}";
            }
            else
            {
                timeText.text = $"{minutes:00}:{seconds:00}";
            }
        }

        private void UpdateSpeedDisplay()
        {
            if (gridRiser == null || speedLevelText == null) return;

            speedLevelText.text = gridRiser.speedLevel.ToString();
        }

        #endregion

        #region Visibility

        private void CheckGameModeVisibility()
        {
            // Show panel only in Marathon mode or single-player test mode
            bool shouldShow = true;

            var gameMode = GameModeManager.Instance?.CurrentMode;
            if (gameMode != null)
            {
                shouldShow = gameMode.modeType == GameModeType.Marathon;
            }

            // Also show if we're in single-player test config
            if (MatchScoreTracker.Instance != null && MatchScoreTracker.Instance.TotalPlayers == 1)
            {
                shouldShow = true;
            }

            if (panel != null)
            {
                panel.SetActive(shouldShow);
            }
        }

        /// <summary>
        /// Manually show the panel.
        /// </summary>
        public void Show()
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        /// <summary>
        /// Manually hide the panel.
        /// </summary>
        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        #endregion

        #region Event Handlers

        private void HandleGameRestarted()
        {
            _elapsedTime = 0f;
            _isBeatingHighScore = false;
            _flashTimer = 0f;

            if (currentScoreText != null)
            {
                currentScoreText.color = normalScoreColor;
            }

            // Refresh high score in case it changed
            if (HighScoreManager.Instance != null)
            {
                _highScore = HighScoreManager.Instance.HighScore;
            }

            UpdateHighScoreDisplay();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Set the ScoreManager reference.
        /// </summary>
        public void SetScoreManager(ScoreManager manager)
        {
            scoreManager = manager;
            if (manager != null && gridRiser == null)
            {
                gridRiser = manager.gridRiser;
            }
        }

        /// <summary>
        /// Set the GridRiser reference.
        /// </summary>
        public void SetGridRiser(PuzzleAttack.Grid.GridRiser riser)
        {
            gridRiser = riser;
        }

        /// <summary>
        /// Get the current elapsed time.
        /// </summary>
        public float GetElapsedTime()
        {
            return _elapsedTime;
        }

        /// <summary>
        /// Reset the timer (e.g., on game restart).
        /// </summary>
        public void ResetTimer()
        {
            _elapsedTime = 0f;
        }

        #endregion
    }
}