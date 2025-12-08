using UnityEngine;
using System;

/// <summary>
/// Central authority for game state management.
/// Controls pause, game over, and other global states.
/// Uses Time.timeScale for automatic animation/physics pausing.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("State")]
    [SerializeField] private GameState currentState = GameState.Playing;

    [Header("Pause Settings")]
    [SerializeField] private bool canPauseDuringGameOver = false;

    // Events for state changes (UI and other systems can subscribe)
    public event Action OnGamePaused;
    public event Action OnGameResumed;
    public event Action OnGameOver;
    public event Action OnGameRestarted;

    // Public properties
    public bool IsPaused => currentState == GameState.Paused;
    public bool IsPlaying => currentState == GameState.Playing;
    public bool IsGameOver => currentState == GameState.GameOver;
    public GameState CurrentState => currentState;

    void Awake()
    {
        // Singleton pattern (per-scene, not persistent)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Don't persist across scenes - each scene gets its own GameStateManager
        // This prevents event subscription issues when restarting
    }

    void Update()
    {
        // Toggle pause with ESC key (you can customize this)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsPaused)
            {
                ResumeGame();
            }
            else if (IsPlaying || (IsGameOver && canPauseDuringGameOver))
            {
                PauseGame();
            }
        }
    }

    /// <summary>
    /// Pauses the game by setting Time.timeScale to 0.
    /// This automatically stops all time-based operations (animations, coroutines using Time.deltaTime, etc.)
    /// </summary>
    public void PauseGame()
    {
        if (IsPaused)
        {
            return;
        }

        currentState = GameState.Paused;
        Time.timeScale = 0f;

        OnGamePaused?.Invoke();
    }

    /// <summary>
    /// Resumes the game by restoring Time.timeScale to 1.
    /// </summary>
    public void ResumeGame()
    {
        if (!IsPaused)
        {
            return;
        }

        currentState = GameState.Playing;
        Time.timeScale = 1f;
        OnGameResumed?.Invoke();
    }

    /// <summary>
    /// Triggers game over state.
    /// </summary>
    public void TriggerGameOver()
    {
        if (IsGameOver)
        {
            return;
        }

        currentState = GameState.GameOver;
        Time.timeScale = 0f; // Stop the game
        OnGameOver?.Invoke();
    }

    /// <summary>
    /// Restarts the game (call this from a "Restart" button).
    /// You'll need to implement the actual restart logic (reload scene, reset GridManager, etc.)
    /// </summary>
    public void RestartGame()
    {
        currentState = GameState.Playing;
        Time.timeScale = 1f;

        OnGameRestarted?.Invoke();

        // Reload the current scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }

    /// <summary>
    /// Quit the game (for Quit button).
    /// </summary>
    public void QuitGame()
    {
        // Return to main menu
        UnityEngine.SceneManagement.SceneManager.LoadScene("main_menu");
    }

    void OnDestroy()
    {
        // Always restore time scale when this object is destroyed
        Time.timeScale = 1f;

        // Clear singleton reference if this is the current instance
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

/// <summary>
/// Enum for different game states.
/// Easily extendable (e.g., add Menu, LevelComplete, etc.)
/// </summary>
public enum GameState
{
    Playing,
    Paused,
    GameOver,
    // Add more states as needed:
    // Menu,
    // LevelComplete,
    // Loading
}