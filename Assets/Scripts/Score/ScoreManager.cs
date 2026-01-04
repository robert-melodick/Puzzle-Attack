using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Manages score, combo, and chain tracking for a single grid.
/// Fires events for garbage routing in VS mode.
/// Each grid has its own ScoreManager instance.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("Score Settings")]
    public int pointsPerTile = 10;
    public float comboMultiplier = 0.5f; // Each combo adds 50% more points
    public int chainBonusPerLevel = 50;  // Bonus points per chain level

    [Header("Display Settings")]
    [Tooltip("Whether to show score UI for this grid (disable for AI)")]
    public bool showScoreUI = true;

    [Header("UI References (Optional)")]
    public GameObject scorePanel;
    public TextMeshProUGUI highScoreText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI comboText;
    public TextMeshProUGUI chainText; // Optional: display current chain

    [Header("Game References")]
    public PuzzleAttack.Grid.GridRiser gridRiser;

    [Header("Timing")]
    [Tooltip("Time window to continue a combo after a match")]
    public float comboTimeout = 2f;
    
    [Tooltip("Time window for chain detection (cascading matches)")]
    public float chainWindow = 0.5f;

    #region Private Fields

    private int _highScore;
    private int _currentScore;
    
    // Combo tracking (sequential matches within timeout)
    private int _currentCombo;
    private int _highestCombo;
    private bool _isInCombo;
    private float _comboTimer;
    
    // Chain tracking (cascading matches from gravity)
    private int _currentChain;
    private int _highestChain;
    private int _maxChainThisCombo;
    private float _chainTimer;
    private bool _isChainActive;

    // Player identification
    private int _playerIndex = 0;
    private bool _isHumanPlayer = true;

    #endregion

    #region Properties

    public int CurrentScore => _currentScore;
    public int CurrentCombo => _currentCombo;
    public int HighestCombo => _highestCombo;
    public int CurrentChain => _currentChain;
    public int HighestChain => _highestChain;
    public bool IsInCombo => _isInCombo;
    public bool IsChainActive => _isChainActive;

    /// <summary>
    /// The player index this ScoreManager belongs to (0-3).
    /// Set by GameplaySceneInitializer.
    /// </summary>
    public int PlayerIndex
    {
        get => _playerIndex;
        set => _playerIndex = value;
    }

    /// <summary>
    /// Whether this ScoreManager belongs to a human player.
    /// Used for high score saving (only save human scores).
    /// </summary>
    public bool IsHumanPlayer
    {
        get => _isHumanPlayer;
        set => _isHumanPlayer = value;
    }

    #endregion

    #region Garbage Routing Events

    /// <summary>
    /// Fired when a new combo sequence starts.
    /// </summary>
    public event Action OnComboStarted;

    /// <summary>
    /// Fired when a combo sequence ends.
    /// Parameters: totalCombo, maxChainReached
    /// </summary>
    public event Action<int, int> OnComboEnded;

    /// <summary>
    /// Fired each time a match is scored during a combo.
    /// Parameters: tilesMatched, comboStep, chainLevel
    /// </summary>
    public event Action<int, int, int> OnMatchScored;

    /// <summary>
    /// Fired when a chain increases.
    /// Parameters: newChainLevel
    /// </summary>
    public event Action<int> OnChainIncreased;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        var gsm = GameStateManager.Instance;
        if (gsm == null) return;
        
        GameStateManager.Instance.OnGameOver += HideScorePanel;
        GameStateManager.Instance.OnGameRestarted += HandleGameRestarted;
        GameStateManager.Instance.OnGameResumed += ShowScorePanel;
    }

    private void OnDisable()
    {
        var gsm = GameStateManager.Instance;
        if (gsm == null) return;
        
        GameStateManager.Instance.OnGameOver -= HideScorePanel;
        GameStateManager.Instance.OnGameRestarted -= HandleGameRestarted;
        GameStateManager.Instance.OnGameResumed -= ShowScorePanel;
    }

    private void Start()
    {
        if (showScoreUI)
        {
            _highScore = HighScoreManager.Instance != null ? HighScoreManager.Instance.HighScore : 0;
            DisplayHighScore();
            UpdateUI();
        }
    }

    private void Update()
    {
        // Combo timeout
        if (_isInCombo)
        {
            _comboTimer += Time.deltaTime;

            if (_comboTimer >= comboTimeout)
            {
                EndCombo();
            }
        }

        // Chain timeout (chains end faster than combos)
        if (_isChainActive)
        {
            _chainTimer += Time.deltaTime;

            if (_chainTimer >= chainWindow)
            {
                EndChain();
            }
        }
    }

    #endregion

    #region Public API - Scoring

    /// <summary>
    /// Add score for a match. Call this from MatchProcessor.
    /// </summary>
    /// <param name="tilesMatched">Number of tiles in the match</param>
    /// <param name="isChainMatch">True if this match resulted from tiles falling after a previous match</param>
    /// <param name="numMatchGroups">Number of separate match groups (for Panel de Pon combo behavior)</param>
    public void AddScore(int tilesMatched, bool isChainMatch = false, int numMatchGroups = 1)
    {
        if (tilesMatched <= 0) return;

        // Handle chain logic (Panel de Pon style: only cascades count as chains)
        if (isChainMatch)
        {
            if (_isChainActive)
            {
                // Continue the chain
                _currentChain++;
                _chainTimer = 0f;

                if (_currentChain > _highestChain)
                    _highestChain = _currentChain;

                if (_currentChain > _maxChainThisCombo)
                    _maxChainThisCombo = _currentChain;

                OnChainIncreased?.Invoke(_currentChain);
                Debug.Log($"[ScoreManager P{_playerIndex}] Chain x{_currentChain}!");
            }
            else
            {
                // First cascade - start chain at x2
                StartChain();
            }
        }
        else if (_isChainActive)
        {
            // Non-chain match during active chain - reset chain timer but keep chain active
            _chainTimer = 0f;
        }

        // Handle combo logic
        if (!_isInCombo)
        {
            StartCombo();
        }

        // Reset combo timer on any match
        _comboTimer = 0f;

        // Calculate points
        int earnedPoints = CalculatePoints(tilesMatched, _currentCombo, _currentChain);
        _currentScore += earnedPoints;

        // Increment combo by number of match groups (Panel de Pon style)
        _currentCombo += numMatchGroups;

        if (_currentCombo > _highestCombo)
            _highestCombo = _currentCombo;

        UpdateUI();

        // Fire event for garbage routing
        OnMatchScored?.Invoke(tilesMatched, _currentCombo, _currentChain);

        Debug.Log($"[ScoreManager P{_playerIndex}] Matched {tilesMatched} tiles ({numMatchGroups} groups) | Combo x{_currentCombo} | Chain x{_currentChain} | +{earnedPoints} pts");
    }

    /// <summary>
    /// Signal that tiles are about to fall (potential chain incoming).
    /// Call this from MatchProcessor after clearing tiles but before dropping.
    /// </summary>
    public void NotifyTilesFalling()
    {
        if (_isChainActive)
        {
            // Reset chain timer - we're expecting a chain match
            _chainTimer = 0f;
        }
    }

    /// <summary>
    /// Signal that all tiles have settled and no matches were found.
    /// Call this when drop completes with no new matches.
    /// </summary>
    public void NotifyDropComplete(bool hadMatches)
    {
        if (!hadMatches && _isChainActive)
        {
            // No chain match occurred - end the chain
            EndChain();
        }
    }

    /// <summary>
    /// Reset combo manually (e.g., when grid rises or garbage lands).
    /// </summary>
    public void ResetCombo()
    {
        if (_isInCombo)
        {
            EndCombo();
        }
    }

    /// <summary>
    /// Reset all scoring state.
    /// </summary>
    public void ResetScore()
    {
        _currentScore = 0;
        _currentCombo = 0;
        _highestCombo = 0;
        _currentChain = 0;
        _highestChain = 0;
        _maxChainThisCombo = 0;
        _isInCombo = false;
        _isChainActive = false;
        _comboTimer = 0f;
        _chainTimer = 0f;
        
        UpdateUI();
    }

    #endregion

    #region Public API - Getters (Legacy Support)

    public int GetScore() => _currentScore;
    public int GetCombo() => _currentCombo;
    public int GetHighestCombo() => _highestCombo;
    public int GetChain() => _currentChain;
    public int GetHighestChain() => _highestChain;

    public int GetSpeedLevel()
    {
        if (gridRiser != null)
        {
            return gridRiser.speedLevel;
        }

        Debug.LogWarning($"[ScoreManager P{_playerIndex}] GridRiser reference is NULL! Returning default level 1.");
        return 1;
    }

    #endregion

    #region Private Methods - Combo/Chain Management

    private void StartCombo()
    {
        _isInCombo = true;
        _currentCombo = 0;
        _maxChainThisCombo = 0;
        _comboTimer = 0f;

        OnComboStarted?.Invoke();
        Debug.Log($"[ScoreManager P{_playerIndex}] Combo started");
    }

    private void EndCombo()
    {
        if (!_isInCombo) return;

        int finalCombo = _currentCombo;
        int finalMaxChain = _maxChainThisCombo;

        _isInCombo = false;
        _currentCombo = 0;
        _comboTimer = 0f;

        // Also end any active chain
        if (_isChainActive)
        {
            EndChain();
        }

        _maxChainThisCombo = 0;

        OnComboEnded?.Invoke(finalCombo, finalMaxChain);
        
        if (finalCombo > 1)
            Debug.Log($"[ScoreManager P{_playerIndex}] Combo ended at x{finalCombo} (max chain: x{finalMaxChain})");

        UpdateUI();
    }

    private void StartChain()
    {
        _isChainActive = true;
        _currentChain = 2; // Panel de Pon style: first cascade = chain x2
        _chainTimer = 0f;

        if (_currentChain > _maxChainThisCombo)
            _maxChainThisCombo = _currentChain;

        OnChainIncreased?.Invoke(_currentChain);
        Debug.Log($"[ScoreManager P{_playerIndex}] Chain started at x{_currentChain}");
    }

    private void EndChain()
    {
        if (!_isChainActive) return;

        if (_currentChain > 1)
            Debug.Log($"[ScoreManager P{_playerIndex}] Chain ended at x{_currentChain}");

        _isChainActive = false;
        _currentChain = 0;
        _chainTimer = 0f;

        UpdateUI();
    }

    private int CalculatePoints(int tilesMatched, int combo, int chain)
    {
        // Base points from tiles
        int basePoints = tilesMatched * pointsPerTile;

        // Combo multiplier (combo 1 = 1x, combo 2 = 1.5x, combo 3 = 2x, etc.)
        float comboMult = 1f + (combo * comboMultiplier);
        
        // Chain bonus (flat bonus per chain level)
        int chainBonus = (chain > 1) ? (chain - 1) * chainBonusPerLevel : 0;

        // Bonus for large matches
        int matchSizeBonus = 0;
        if (tilesMatched >= 6) matchSizeBonus = 100;
        else if (tilesMatched >= 5) matchSizeBonus = 50;
        else if (tilesMatched >= 4) matchSizeBonus = 20;

        int totalPoints = Mathf.RoundToInt(basePoints * comboMult) + chainBonus + matchSizeBonus;

        return totalPoints;
    }

    #endregion

    #region Private Methods - UI

    private void UpdateUI()
    {
        if (!showScoreUI) return;
        
        // Score
        if (scoreText != null)
        {
            scoreText.text = _currentScore.ToString("D9");
        }

        // Combo
        if (comboText != null)
        {
            if (_currentCombo > 1)
            {
                comboText.text = $"Combo x{_currentCombo}";
                comboText.gameObject.SetActive(true);
            }
            else
            {
                comboText.gameObject.SetActive(false);
            }
        }

        // Chain (optional display)
        if (chainText != null)
        {
            if (_currentChain > 1)
            {
                chainText.text = $"Chain x{_currentChain}";
                chainText.gameObject.SetActive(true);
            }
            else
            {
                chainText.gameObject.SetActive(false);
            }
        }
    }

    private void DisplayHighScore()
    {
        if (!showScoreUI) return;
        
        if (highScoreText != null)
        {
            highScoreText.text = _highScore.ToString("D9");
        }
    }

    private void ShowScorePanel()
    {
        if (!showScoreUI) return;
        
        if (scorePanel != null)
            scorePanel.SetActive(true);
    }

    private void HideScorePanel()
    {
        if (!showScoreUI) return;
        
        if (scorePanel != null)
            scorePanel.SetActive(false);
    }

    private void HandleGameRestarted()
    {
        ShowScorePanel();
        ResetScore();
    }

    #endregion
}