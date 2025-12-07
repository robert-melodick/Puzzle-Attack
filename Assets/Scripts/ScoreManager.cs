using TMPro;
using UnityEngine;

// For TextMeshPro support

public class ScoreManager : MonoBehaviour
{
    [Header("Score Settings")] public int pointsPerTile = 10;

    public float comboMultiplier = 0.5f; // Each combo adds 50% more points

    [Header("OR use TextMeshPro (TMP)")] public TextMeshProUGUI scoreText; // TextMeshPro

    public TextMeshProUGUI comboText; // TextMeshPro
    private int currentCombo;

    private int currentScore;

    private void Start()
    {
        UpdateUI();
    }

    public void AddScore(int tilesMatched)
    {
        if (tilesMatched <= 0) return;

        // Calculate base points
        var basePoints = tilesMatched * pointsPerTile;

        // Apply combo multiplier
        var multiplier = 1f + currentCombo * comboMultiplier;
        var earnedPoints = Mathf.RoundToInt(basePoints * multiplier);

        currentScore += earnedPoints;
        currentCombo++;

        UpdateUI();

        // Optional: Log for debugging
        Debug.Log($"Matched {tilesMatched} tiles | Combo x{currentCombo} | Earned {earnedPoints} points");
    }

    public void ResetCombo()
    {
        if (currentCombo > 0) Debug.Log($"Combo ended at x{currentCombo}");
        currentCombo = 0;
        UpdateUI();
    }

    private void UpdateUI()
    {
        var scoreString = $"Score: {currentScore}";
        var comboString = $"Combo x{currentCombo}";

        // Update TextMeshPro if assigned
        if (scoreText != null) scoreText.text = scoreString;

        if (comboText != null)
        {
            if (currentCombo > 1)
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
        return currentScore;
    }

    public int GetCombo()
    {
        return currentCombo;
    }

    public void ResetScore()
    {
        currentScore = 0;
        currentCombo = 0;
        UpdateUI();
    }
}