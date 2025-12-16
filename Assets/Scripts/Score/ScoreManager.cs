using TMPro;
using UnityEngine;

// For TextMeshPro support

public class ScoreManager : MonoBehaviour
{
    [Header("Score Settings")]
    public int pointsPerTile = 10;
    public float comboMultiplier = 0.5f; // Each combo adds 50% more points

    [Header("UI References")]
    public GameObject scorePanel;
    public TextMeshProUGUI highScoreText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI comboText;

    [Header("Game References")]
    public PuzzleAttack.Grid.GridRiser gridRiser; // Reference to get speed level

    private int _highScore;
    private int _currentCombo;
    private int _highestCombo; // Track the highest combo reached this game
    private int _currentScore;

    // Subscribe to GameStateManager events
    private void OnEnable()
    {
        var gsm = GameStateManager.Instance;
        if (gsm == null)    // If no manager, abort
            return;
        GameStateManager.Instance.OnGameOver += HideScorePanel;
        GameStateManager.Instance.OnGameRestarted += ShowScorePanel;
        GameStateManager.Instance.OnGameResumed += ShowScorePanel;
    }
    
    // Unsubscribe
    private void OnDisable()
    {
        var gsm = GameStateManager.Instance;
        if (gsm == null) // Manager already gone, abort
            return;
        GameStateManager.Instance.OnGameOver -= HideScorePanel;
        GameStateManager.Instance.OnGameRestarted -= ShowScorePanel;
        GameStateManager.Instance.OnGameResumed -= ShowScorePanel;
    }
    
    private void Start()
    {
        // Check if there's a high score
        _highScore = HighScoreManager.Instance != null ? HighScoreManager.Instance.HighScore : 0;
        DisplayHighScore();
        UpdateUI();
    }

    public void AddScore(int tilesMatched)
    {
        if (tilesMatched <= 0) return;

        // Calculate base points
        var basePoints = tilesMatched * pointsPerTile;

        // Apply combo multiplier
        var multiplier = 1f + _currentCombo * comboMultiplier;
        var earnedPoints = Mathf.RoundToInt(basePoints * multiplier);

        _currentScore += earnedPoints;
        _currentCombo++;

        // Track highest combo
        if (_currentCombo > _highestCombo)
        {
            _highestCombo = _currentCombo;
        }

        UpdateUI();

        // Optional: Log for debugging
        Debug.Log($"Matched {tilesMatched} tiles | Combo x{_currentCombo} | Earned {earnedPoints} points");
    }

    public void ResetCombo()
    {
        if (_currentCombo > 0) Debug.Log($"Combo ended at x{_currentCombo}");
        _currentCombo = 0;
        UpdateUI();
    }

    private void UpdateUI()
    {
        var scoreString = _currentScore.ToString("D9");
        var comboString = $"Combo x{_currentCombo}";

        // Update TextMeshPro if assigned
        if (scoreText != null) scoreText.text = scoreString;

        if (comboText != null)
        {
            if (_currentCombo > 1)
            {
                comboText.text = comboString;
                comboText.gameObject.SetActive(true);
            }
            else
            {
                comboText.gameObject.SetActive(false);
            }
        }
    }

    public int GetScore()
    {
        return _currentScore;
    }

    public int GetCombo()
    {
        return _currentCombo;
    }

    public int GetHighestCombo()
    {
        return _highestCombo;
    }

    public int GetSpeedLevel()
    {
        if (gridRiser != null)
        {
            int level = gridRiser.speedLevel;
            Debug.Log($"[ScoreManager] Getting speed level from GridRiser: {level}");
            return level;
        }

        Debug.LogWarning("[ScoreManager] GridRiser reference is NULL! Returning default level 1. Please assign GridRiser in the Inspector.");
        return 1; // Default to level 1 if no GridRiser reference
    }

    public void ResetScore()
    {
        _currentScore = 0;
        _currentCombo = 0;
        _highestCombo = 0;
        UpdateUI();
    }

    void DisplayHighScore()
    {
        highScoreText.text = _highScore.ToString("D9");
    }

    private void ShowScorePanel()
    {
        scorePanel.SetActive(true);
    }

    private void HideScorePanel()
    {
        scorePanel.SetActive(false);
    }
}