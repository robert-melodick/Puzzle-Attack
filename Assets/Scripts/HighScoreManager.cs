using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Data structure for a single high score entry
/// </summary>
[System.Serializable]
public class HighScoreEntry
{
    public int score;
    public int highestCombo;
    public int speedLevel;
    public string date; // Track when the score was achieved

    public HighScoreEntry(int score, int highestCombo, int speedLevel)
    {
        this.score = score;
        this.highestCombo = highestCombo;
        this.speedLevel = speedLevel;
        this.date = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
    }
}

/// <summary>
/// Wrapper class for JSON serialization of the high score list
/// </summary>
[System.Serializable]
public class HighScoreList
{
    public List<HighScoreEntry> entries = new List<HighScoreEntry>();
}

/// <summary>
/// Manages high score persistence across game sessions using Unity's PlayerPrefs.
/// Maintains a leaderboard of the top 10 scores with combo tracking.
/// </summary>
public class HighScoreManager : MonoBehaviour
{
    // Singleton pattern ensures only one instance exists
    public static HighScoreManager Instance { get; private set; }

    [Header("High Score Settings")]
    [Tooltip("The key used to store the high score data in PlayerPrefs")]
    public string highScoreKey = "HighScoreData";

    [Tooltip("Maximum number of high scores to keep")]
    public int maxHighScores = 10;

    private HighScoreList _highScoreList = new HighScoreList();

    /// <summary>
    /// Gets the highest score value (for backwards compatibility)
    /// </summary>
    public int HighScore => _highScoreList.entries.Count > 0 ? _highScoreList.entries[0].score : 0;

    /// <summary>
    /// Gets the full list of high score entries
    /// </summary>
    public List<HighScoreEntry> GetHighScores() => new List<HighScoreEntry>(_highScoreList.entries);

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
    /// Loads the high score list from PlayerPrefs.
    /// Uses JSON serialization to store multiple entries.
    /// </summary>
    void LoadHighScore()
    {
        string json = PlayerPrefs.GetString(highScoreKey, "");

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _highScoreList = JsonUtility.FromJson<HighScoreList>(json);
                Debug.Log($"Loaded {_highScoreList.entries.Count} high scores. Top score: {HighScore}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load high scores: {e.Message}");
                _highScoreList = new HighScoreList();
            }
        }
        else
        {
            Debug.Log("No saved high scores found. Starting fresh.");
            _highScoreList = new HighScoreList();
        }
    }

    /// <summary>
    /// Saves the high score list to PlayerPrefs using JSON.
    /// PlayerPrefs.Save() forces immediate write to disk.
    /// </summary>
    void SaveHighScore()
    {
        string json = JsonUtility.ToJson(_highScoreList);
        PlayerPrefs.SetString(highScoreKey, json);
        PlayerPrefs.Save(); // Force write to disk immediately
        Debug.Log($"Saved {_highScoreList.entries.Count} high scores. Top score: {HighScore}");
    }

    /// <summary>
    /// Adds a new score to the leaderboard if it qualifies.
    /// Automatically sorts and trims the list to keep only the top scores.
    /// </summary>
    /// <param name="newScore">The score achieved</param>
    /// <param name="highestCombo">The highest combo achieved during that game</param>
    /// <param name="speedLevel">The speed level reached during that game</param>
    /// <returns>The rank (1-based) if the score made it to the leaderboard, 0 otherwise</returns>
    public int AddScore(int newScore, int highestCombo, int speedLevel)
    {
        // Check if this score qualifies for the leaderboard
        bool qualifies = _highScoreList.entries.Count < maxHighScores ||
                        newScore > _highScoreList.entries.LastOrDefault()?.score;

        if (!qualifies)
        {
            Debug.Log($"Score {newScore} did not qualify for leaderboard.");
            return 0;
        }

        // Create new entry
        var newEntry = new HighScoreEntry(newScore, highestCombo, speedLevel);
        _highScoreList.entries.Add(newEntry);

        // Sort by score descending
        _highScoreList.entries = _highScoreList.entries.OrderByDescending(e => e.score).ToList();

        // Trim to max size
        if (_highScoreList.entries.Count > maxHighScores)
        {
            _highScoreList.entries.RemoveRange(maxHighScores, _highScoreList.entries.Count - maxHighScores);
            Debug.Log($"Trimmed leaderboard to top {maxHighScores} scores.");
        }

        // Find the rank of the new entry
        int rank = _highScoreList.entries.FindIndex(e => e == newEntry) + 1;

        SaveHighScore();

        if (rank == 1)
        {
            Debug.Log($"NEW HIGH SCORE: {newScore} with combo x{highestCombo} at speed level {speedLevel}!");
        }
        else
        {
            Debug.Log($"New score added at rank #{rank}: {newScore} with combo x{highestCombo} at speed level {speedLevel}");
        }

        return rank;
    }

    /// <summary>
    /// Attempts to update the high score with a new score (backwards compatibility).
    /// </summary>
    /// <param name="newScore">The score to compare against the high score</param>
    /// <returns>True if a new high score was set, false otherwise</returns>
    public bool TrySetHighScore(int newScore)
    {
        int rank = AddScore(newScore, 0, 0);
        return rank == 1;
    }

    /// <summary>
    /// Resets all high scores. Useful for testing or settings menu.
    /// </summary>
    public void ResetHighScore()
    {
        _highScoreList.entries.Clear();
        SaveHighScore();
        Debug.Log("All high scores reset.");
    }

    /// <summary>
    /// Returns a formatted string for display in UI (backwards compatibility)
    /// </summary>
    public string GetHighScoreText()
    {
        return HighScore.ToString("D9");
    }

    /// <summary>
    /// Checks if a score would qualify for the leaderboard
    /// </summary>
    public bool WouldQualify(int score)
    {
        return _highScoreList.entries.Count < maxHighScores ||
               score > _highScoreList.entries.LastOrDefault()?.score;
    }
}