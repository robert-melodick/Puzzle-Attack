using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PuzzleAttack;
using PuzzleAttack.Grid;

/// <summary>
/// Controls game over UI for all game modes.
/// Shows appropriate panels for Marathon (game over) and VS modes (victory/defeat).
/// </summary>
public class GameOverUIController : MonoBehaviour
{
    #region Inspector Fields - Panels

    [Header("Panels")]
    [SerializeField] private GameObject marathonGameOverPanel;
    [SerializeField] private GameObject vsVictoryPanel;

    #endregion

    #region Inspector Fields - Marathon Panel

    [Header("Marathon Game Over Panel")]
    [SerializeField] private TextMeshProUGUI marathonTitleText;
    [SerializeField] private TextMeshProUGUI marathonScoreText;
    [SerializeField] private TextMeshProUGUI marathonHighestComboText;
    [SerializeField] private TextMeshProUGUI marathonHighestChainText;
    [SerializeField] private TextMeshProUGUI marathonSpeedLevelText;
    [SerializeField] private TextMeshProUGUI marathonNewHighScoreText;
    [SerializeField] private Button marathonRetryButton;
    [SerializeField] private Button marathonMainMenuButton;

    #endregion

    #region Inspector Fields - VS Victory Panel

    [Header("VS Victory Panel")]
    [SerializeField] private TextMeshProUGUI victoryTitleText;
    [SerializeField] private Image victoryIconImage;
    [SerializeField] private TextMeshProUGUI victorySubtitleText;
    
    [Header("VS Victory - Player Stats Container")]
    [SerializeField] private Transform playerStatsContainer;
    [SerializeField] private GameObject playerStatsPrefab;

    [Header("VS Victory - Buttons")]
    [SerializeField] private Button vsRematchButton;
    [SerializeField] private Button vsMainMenuButton;

    [Header("VS Victory - Colors")]
    [SerializeField] private Color humanWinColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color cpuWinColor = new Color(1f, 0.5f, 0.3f);
    [SerializeField] private Color firstPlaceColor = new Color(1f, 0.84f, 0f);   // Gold
    [SerializeField] private Color secondPlaceColor = new Color(0.75f, 0.75f, 0.75f); // Silver
    [SerializeField] private Color thirdPlaceColor = new Color(0.8f, 0.5f, 0.2f);  // Bronze
    [SerializeField] private Color fourthPlaceColor = new Color(0.5f, 0.5f, 0.5f); // Gray

    #endregion

    #region Inspector Fields - Grid Elimination Overlay

    [Header("Grid Elimination Overlay")]
    [SerializeField] private GameObject eliminationOverlayPrefab;
    [SerializeField] private string eliminationText = "RETIRED";

    #endregion

    #region Private Fields

    private List<GameObject> _instantiatedStatRows = new List<GameObject>();
    private Dictionary<int, GameObject> _eliminationOverlays = new Dictionary<int, GameObject>();

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Hide all panels initially
        HideAllPanels();
        
        // Setup button listeners
        SetupButtons();

        // Subscribe to events
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    #endregion

    #region Setup

    private void SetupButtons()
    {
        // Marathon buttons
        if (marathonRetryButton != null)
            marathonRetryButton.onClick.AddListener(OnRetryClicked);
        if (marathonMainMenuButton != null)
            marathonMainMenuButton.onClick.AddListener(OnMainMenuClicked);

        // VS buttons
        if (vsRematchButton != null)
            vsRematchButton.onClick.AddListener(OnRetryClicked);
        if (vsMainMenuButton != null)
            vsMainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    private void SubscribeToEvents()
    {
        if (MatchScoreTracker.Instance != null)
        {
            MatchScoreTracker.Instance.OnPlayerEliminated += HandlePlayerEliminated;
            MatchScoreTracker.Instance.OnMatchWinner += HandleMatchWinner;
            MatchScoreTracker.Instance.OnMarathonGameOver += HandleMarathonGameOver;
            MatchScoreTracker.Instance.OnMatchDraw += HandleMatchDraw;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (MatchScoreTracker.Instance != null)
        {
            MatchScoreTracker.Instance.OnPlayerEliminated -= HandlePlayerEliminated;
            MatchScoreTracker.Instance.OnMatchWinner -= HandleMatchWinner;
            MatchScoreTracker.Instance.OnMarathonGameOver -= HandleMarathonGameOver;
            MatchScoreTracker.Instance.OnMatchDraw -= HandleMatchDraw;
        }
    }

    private void HideAllPanels()
    {
        if (marathonGameOverPanel != null)
            marathonGameOverPanel.SetActive(false);
        if (vsVictoryPanel != null)
            vsVictoryPanel.SetActive(false);
    }

    #endregion

    #region Event Handlers

    private void HandlePlayerEliminated(int playerIndex, int placement)
    {
        Debug.Log($"[GameOverUI] Player {playerIndex} eliminated, placement: {placement}");
        
        // Show elimination overlay on that player's grid (only for VS modes, not marathon)
        if (MatchScoreTracker.Instance != null && MatchScoreTracker.Instance.TotalPlayers > 1)
        {
            ShowEliminationOverlay(playerIndex);
        }
    }

    private void HandleMarathonGameOver()
    {
        Debug.Log("[GameOverUI] Marathon game over triggered");
        
        ShowMarathonGameOver();

        // Save high scores for human players
        MatchScoreTracker.Instance?.SaveHumanHighScores();

        // Pause the game
        Time.timeScale = 0f;
    }

    private void HandleMatchWinner(int winnerIndex, bool isHuman)
    {
        Debug.Log($"[GameOverUI] VS match winner: Player {winnerIndex} (Human: {isHuman})");
        
        // This is only called for VS modes now
        ShowVsVictory(winnerIndex, isHuman);

        // Save high scores for human players
        MatchScoreTracker.Instance?.SaveHumanHighScores();

        // Pause the game
        Time.timeScale = 0f;
    }

    private void HandleMatchDraw()
    {
        Debug.Log("[GameOverUI] Match ended in a draw!");
        
        // Show VS panel with draw message
        if (vsVictoryPanel != null)
        {
            vsVictoryPanel.SetActive(true);
            
            if (victoryTitleText != null)
                victoryTitleText.text = "DRAW!";
            if (victorySubtitleText != null)
                victorySubtitleText.text = "No winner this time...";
        }

        Time.timeScale = 0f;
    }

    #endregion

    #region Marathon Game Over

    private void ShowMarathonGameOver()
    {
        if (marathonGameOverPanel == null) return;

        marathonGameOverPanel.SetActive(true);

        // Get player data
        var playerData = MatchScoreTracker.Instance?.GetPlayerData(0);
        
        if (playerData != null)
        {
            int score = playerData.FinalScore;
            int combo = playerData.FinalCombo;
            int chain = playerData.FinalChain;
            int speedLevel = playerData.ScoreManager?.GetSpeedLevel() ?? 1;

            // Update UI
            if (marathonTitleText != null)
                marathonTitleText.text = "GAME OVER";

            if (marathonScoreText != null)
                marathonScoreText.text = score.ToString();

            if (marathonHighestComboText != null)
                marathonHighestComboText.text = combo.ToString();

            if (marathonHighestChainText != null)
                marathonHighestChainText.text = chain.ToString();

            if (marathonSpeedLevelText != null)
                marathonSpeedLevelText.text = speedLevel.ToString();

            // Check for new high score
            if (marathonNewHighScoreText != null)
            {
                bool isNewHighScore = HighScoreManager.Instance != null && 
                                      score >= HighScoreManager.Instance.HighScore &&
                                      score > 0;
                marathonNewHighScoreText.gameObject.SetActive(isNewHighScore);
                if (isNewHighScore)
                    marathonNewHighScoreText.text = "NEW HIGH SCORE!";
            }
        }

        Debug.Log("[GameOverUI] Showing Marathon game over panel");
    }

    #endregion

    #region VS Victory

    private void ShowVsVictory(int winnerIndex, bool isHuman)
    {
        if (vsVictoryPanel == null) return;

        vsVictoryPanel.SetActive(true);

        var winnerData = MatchScoreTracker.Instance?.GetPlayerData(winnerIndex);
        
        // Update victory title
        if (victoryTitleText != null)
        {
            string winnerName = winnerData?.GetDisplayName() ?? $"Player {winnerIndex + 1}";
            victoryTitleText.text = $"{winnerName} WINS!";
            victoryTitleText.color = isHuman ? humanWinColor : cpuWinColor;
        }

        // Update subtitle
        if (victorySubtitleText != null)
        {
            if (winnerData != null)
            {
                victorySubtitleText.text = $"Score: {winnerData.FinalScore:N0}";
            }
        }

        // Update icon color
        if (victoryIconImage != null)
        {
            victoryIconImage.color = isHuman ? humanWinColor : cpuWinColor;
        }

        // Populate player stats
        PopulatePlayerStats();

        Debug.Log($"[GameOverUI] Showing VS victory panel - Winner: Player {winnerIndex}");
    }

    private void PopulatePlayerStats()
    {
        // Clear old stat rows
        ClearPlayerStats();

        if (playerStatsContainer == null || playerStatsPrefab == null) return;

        // Get results sorted by placement
        var results = MatchScoreTracker.Instance?.GetResultsSortedByPlacement();
        if (results == null) return;

        foreach (var data in results)
        {
            var statRow = Instantiate(playerStatsPrefab, playerStatsContainer);
            _instantiatedStatRows.Add(statRow);

            // Try to find and populate text elements
            var nameText = statRow.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            var scoreText = statRow.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
            var comboText = statRow.transform.Find("ComboText")?.GetComponent<TextMeshProUGUI>();
            var placementText = statRow.transform.Find("PlacementText")?.GetComponent<TextMeshProUGUI>();
            var background = statRow.GetComponent<Image>();

            if (nameText != null)
                nameText.text = data.GetDisplayName();

            if (scoreText != null)
                scoreText.text = data.FinalScore.ToString("N0");

            if (comboText != null)
                comboText.text = $"x{data.FinalCombo}";

            if (placementText != null)
            {
                placementText.text = GetPlacementString(data.Placement);
                placementText.color = GetPlacementColor(data.Placement);
            }

            if (background != null)
            {
                var color = GetPlacementColor(data.Placement);
                color.a = 0.3f; // Semi-transparent background
                background.color = color;
            }
        }
    }

    private void ClearPlayerStats()
    {
        foreach (var row in _instantiatedStatRows)
        {
            if (row != null)
                Destroy(row);
        }
        _instantiatedStatRows.Clear();
    }

    private string GetPlacementString(int placement)
    {
        switch (placement)
        {
            case 1: return "1ST";
            case 2: return "2ND";
            case 3: return "3RD";
            case 4: return "4TH";
            default: return $"{placement}TH";
        }
    }

    private Color GetPlacementColor(int placement)
    {
        switch (placement)
        {
            case 1: return firstPlaceColor;
            case 2: return secondPlaceColor;
            case 3: return thirdPlaceColor;
            case 4: return fourthPlaceColor;
            default: return fourthPlaceColor;
        }
    }

    #endregion

    #region Elimination Overlay

    private void ShowEliminationOverlay(int playerIndex)
    {
        if (eliminationOverlayPrefab == null) return;

        // Get the player's grid
        var playerData = MatchScoreTracker.Instance?.GetPlayerData(playerIndex);
        if (playerData?.GridManager == null) return;

        // Don't create duplicate overlays
        if (_eliminationOverlays.ContainsKey(playerIndex)) return;

        // Create overlay as child of the grid
        var overlay = Instantiate(eliminationOverlayPrefab, playerData.GridManager.transform);
        overlay.name = $"EliminationOverlay_P{playerIndex}";
        
        // Position it to cover the grid
        var gridCenter = playerData.GridManager.GetGridCenter();
        overlay.transform.position = new Vector3(gridCenter.x, gridCenter.y, -5f); // In front of grid

        // Set the text
        var text = overlay.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = eliminationText;
        }

        _eliminationOverlays[playerIndex] = overlay;

        // Optionally darken the grid
        DarkenGrid(playerData.GridManager);

        Debug.Log($"[GameOverUI] Showing elimination overlay for player {playerIndex}");
    }

    private void DarkenGrid(GridManager grid)
    {
        // Find all SpriteRenderers in the grid and darken them
        var spriteRenderers = grid.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in spriteRenderers)
        {
            var color = sr.color;
            color.r *= 0.4f;
            color.g *= 0.4f;
            color.b *= 0.4f;
            sr.color = color;
        }
    }

    private void ClearEliminationOverlays()
    {
        foreach (var overlay in _eliminationOverlays.Values)
        {
            if (overlay != null)
                Destroy(overlay);
        }
        _eliminationOverlays.Clear();
    }

    #endregion

    #region Button Handlers

    private void OnRetryClicked()
    {
        ClearEliminationOverlays();
        ClearPlayerStats();
        HideAllPanels();

        // Restore time scale
        Time.timeScale = 1f;

        // Reload the current scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }

    private void OnMainMenuClicked()
    {
        ClearEliminationOverlays();
        ClearPlayerStats();
        HideAllPanels();

        // Restore time scale
        Time.timeScale = 1f;

        // Return to main menu
        UnityEngine.SceneManagement.SceneManager.LoadScene("main_menu");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Manually show marathon game over (for backwards compatibility).
    /// </summary>
    public void ShowMarathonGameOverManual(int score, int combo, int chain, int speedLevel, bool isNewHighScore)
    {
        if (marathonGameOverPanel == null) return;

        marathonGameOverPanel.SetActive(true);

        if (marathonTitleText != null)
            marathonTitleText.text = "GAME OVER";

        if (marathonScoreText != null)
            marathonScoreText.text = score.ToString();

        if (marathonHighestComboText != null)
            marathonHighestComboText.text = combo.ToString();

        if (marathonHighestChainText != null)
            marathonHighestChainText.text = chain.ToString();

        if (marathonSpeedLevelText != null)
            marathonSpeedLevelText.text = speedLevel.ToString();

        if (marathonNewHighScoreText != null)
        {
            marathonNewHighScoreText.gameObject.SetActive(isNewHighScore);
            if (isNewHighScore)
                marathonNewHighScoreText.text = "NEW HIGH SCORE!";
        }

        Time.timeScale = 0f;
    }

    #endregion
}