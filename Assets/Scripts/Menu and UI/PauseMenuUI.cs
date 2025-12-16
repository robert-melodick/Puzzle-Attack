using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

        // Ensure EventSystem exists
        EnsureEventSystemExists();

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

            // Ensure all buttons are visible and clickable for pause menu
            if (resumeButton != null)
                resumeButton.gameObject.SetActive(true);

            // Ensure buttons are interactable
            EnsureButtonsClickable();
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

            // Hide resume button on game over
            if (resumeButton != null)
                resumeButton.gameObject.SetActive(false);

            // Ensure buttons are interactable and don't have frozen animators
            EnsureButtonsClickable();
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

    /// <summary>
    /// Ensures buttons are clickable even when Time.timeScale = 0.
    /// Fixes issue where button animations freeze and prevent clicks.
    /// </summary>
    void EnsureButtonsClickable()
    {
        // Make sure buttons are interactable
        if (resumeButton != null)
        {
            resumeButton.interactable = true;
            DisableButtonAnimator(resumeButton);
        }

        if (restartButton != null)
        {
            restartButton.interactable = true;
            DisableButtonAnimator(restartButton);
        }

        if (quitButton != null)
        {
            quitButton.interactable = true;
            DisableButtonAnimator(quitButton);
        }

        // Ensure the panel itself isn't blocking raycasts
        CanvasGroup canvasGroup = pauseMenuPanel.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>
    /// Disables Animator on button to prevent animation freezing issues when Time.timeScale = 0.
    /// </summary>
    void DisableButtonAnimator(Button button)
    {
        if (button == null) return;

        Animator animator = button.GetComponent<Animator>();
        if (animator != null)
        {
            // Disable animator to prevent frozen transitions from blocking clicks
            animator.enabled = false;
        }
    }

    /// <summary>
    /// Ensures an EventSystem exists in the scene for UI interaction.
    /// </summary>
    void EnsureEventSystemExists()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
            Debug.LogWarning("No EventSystem found! Created one automatically.");
        }
        else
        {
            // Make sure it's enabled
            eventSystem.enabled = true;
        }
    }
}