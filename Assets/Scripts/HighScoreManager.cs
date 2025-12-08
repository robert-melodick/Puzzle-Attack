using UnityEngine;

/// <summary>
/// Manages high score persistence across game sessions using Unity's PlayerPrefs.
/// PlayerPrefs is Unity's built-in key-value storage system that persists between sessions.
/// </summary>
public class HighScoreManager : MonoBehaviour
{
    // Singleton pattern ensures only one instance exists
    public static HighScoreManager Instance { get; private set; }

    [Header("High Score Settings")]
    [Tooltip("The key used to store the high score in PlayerPrefs")]
    public string highScoreKey = "HighScore";

    private int _currentHighScore = 0;

    /// <summary>
    /// Gets the current high score value
    /// </summary>
    public int HighScore => _currentHighScore;

    void Awake()
    {
        // Implement singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        
        // Optional: Keep this object alive between scenes
        // DontDestroyOnLoad(gameObject);

        // Load high score from persistent storage
        LoadHighScore();
    }

    /// <summary>
    /// Loads the high score from PlayerPrefs.
    /// Returns 0 if no high score exists yet.
    /// </summary>
    void LoadHighScore()
    {
        _currentHighScore = PlayerPrefs.GetInt(highScoreKey, 0);
        Debug.Log($"High Score loaded: {_currentHighScore}");
    }

    /// <summary>
    /// Saves the high score to PlayerPrefs.
    /// PlayerPrefs.Save() forces immediate write to disk (optional but recommended for important data).
    /// </summary>
    void SaveHighScore()
    {
        PlayerPrefs.SetInt(highScoreKey, _currentHighScore);
        PlayerPrefs.Save(); // Force write to disk immediately
        Debug.Log($"High Score saved: {_currentHighScore}");
    }

    /// <summary>
    /// Attempts to update the high score with a new score.
    /// Only saves if the new score is higher than the current high score.
    /// </summary>
    /// <param name="newScore">The score to compare against the high score</param>
    /// <returns>True if a new high score was set, false otherwise</returns>
    public bool TrySetHighScore(int newScore)
    {
        if (newScore > _currentHighScore)
        {
            _currentHighScore = newScore;
            SaveHighScore();
            Debug.Log($"NEW HIGH SCORE: {_currentHighScore}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets the high score to 0. Useful for testing or settings menu.
    /// </summary>
    public void ResetHighScore()
    {
        _currentHighScore = 0;
        SaveHighScore();
        Debug.Log("High Score reset to 0");
    }

    /// <summary>
    /// Returns a formatted string for display in UI
    /// </summary>
    public string GetHighScoreText()
    {
        return _currentHighScore.ToString("D9");
    }
}