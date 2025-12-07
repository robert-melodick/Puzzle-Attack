using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the pause menu UI.
/// Subscribes to GameStateManager events to show/hide itself.
/// Handles button clicks for Resume, Restart, and Quit.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    [Header("Optional: Title Text")]
    [SerializeField] private TextMeshProUGUI titleText;

    void Start()
    {
        // Subscribe to GameStateManager events
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGamePaused += ShowPauseMenu;
            GameStateManager.Instance.OnGameResumed += HidePauseMenu;
            GameStateManager.Instance.OnGameOver += ShowGameOverMenu;
        }
        else
        {
            Debug.LogError("GameStateManager not found! Make sure it exists in the scene.");
        }

        // Setup button listeners
        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumeClicked);

        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        // Hide menu initially
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGamePaused -= ShowPauseMenu;
            GameStateManager.Instance.OnGameResumed -= HidePauseMenu;
            GameStateManager.Instance.OnGameOver -= ShowGameOverMenu;
        }
    }

    void ShowPauseMenu()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);

            // Update title if exists
            if (titleText != null)
                titleText.text = "PAUSED";
        }
    }

    void HidePauseMenu()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
    }

    void ShowGameOverMenu()
    {
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);

            // Update title if exists
            if (titleText != null)
                titleText.text = "GAME OVER";

            // Optional: Hide resume button on game over
            if (resumeButton != null)
                resumeButton.gameObject.SetActive(false);
        }
    }

    // Button callback methods
    void OnResumeClicked()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.ResumeGame();
    }

    void OnRestartClicked()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.RestartGame();
    }

    void OnQuitClicked()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.QuitGame();
    }
}