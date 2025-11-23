using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    [Header("Score Settings")]
    public int pointsPerTile = 10;
    public float comboMultiplier = 0.5f; // Each combo adds 50% more points
    
    [Header("UI References")]
    public Text scoreText;
    public Text comboText;
    
    private int currentScore = 0;
    private int currentCombo = 0;
    
    void Start()
    {
        UpdateUI();
    }
    
    public void AddScore(int tilesMatched)
    {
        if (tilesMatched <= 0) return;
        
        // Calculate base points
        int basePoints = tilesMatched * pointsPerTile;
        
        // Apply combo multiplier
        float multiplier = 1f + (currentCombo * comboMultiplier);
        int earnedPoints = Mathf.RoundToInt(basePoints * multiplier);
        
        currentScore += earnedPoints;
        currentCombo++;
        
        UpdateUI();
        
        // Optional: Log for debugging
        Debug.Log($"Matched {tilesMatched} tiles | Combo x{currentCombo} | Earned {earnedPoints} points");
    }
    
    public void ResetCombo()
    {
        if (currentCombo > 0)
        {
            Debug.Log($"Combo ended at x{currentCombo}");
        }
        currentCombo = 0;
        UpdateUI();
    }
    
    void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {currentScore}";
        }
        
        if (comboText != null)
        {
            if (currentCombo > 0)
            {
                comboText.text = $"Combo x{currentCombo + 1}";
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