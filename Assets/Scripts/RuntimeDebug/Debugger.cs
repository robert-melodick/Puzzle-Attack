using PuzzleAttack.Grid;
using TMPro;
using UnityEngine;
using System.Text;
using System.Collections;
using UnityEditor.Experimental.GraphView;
using UnityEngine.InputSystem;

/// <summary>
/// Ultimate debug tool for monitoring game state, performance, and grid information.
/// Press F1 to toggle the debug overlay on/off.
/// Available in both Editor and Build for production debugging.
/// </summary>
public class Debugger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GridRiser gridRiser;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private MatchProcessor matchProcessor;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private GameStateManager gameStateManager;

    [Header("UI Settings")]
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private bool startVisible = false;
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.7f);
    [SerializeField] private float updateInterval = 0.1f; // Update debug text 10 times per second

    [Header("Performance Monitoring")]
    [SerializeField] private bool showFPS = true;
    [SerializeField] private bool showMemory = true;
    [SerializeField] private int fpsAveragingFrames = 30;

    // FPS tracking
    private float[] _fpsBuffer;
    private int _fpsBufferIndex;
    private float _fpsUpdateTimer;

    // Memory tracking
    private float _memoryUpdateTimer;
    private float _lastMemoryUsage;

    // State
    private bool _isVisible;
    private float _debugUpdateTimer;
    private StringBuilder _sb;

    // Performance metrics
    private int _frameCount;
    private float _deltaTimeSum;

    void Awake()
    {
        // Initialize FPS buffer
        _fpsBuffer = new float[fpsAveragingFrames];
        _sb = new StringBuilder(1024);

        // Auto-find references if not set
        if (gridManager == null)
            gridManager = FindObjectOfType<GridManager>();
        if (gridRiser == null)
            gridRiser = FindObjectOfType<GridRiser>();
        if (cursorController == null)
            cursorController = FindObjectOfType<CursorController>();
        if (matchProcessor == null)
            matchProcessor = FindObjectOfType<MatchProcessor>();
        if (scoreManager == null)
            scoreManager = FindObjectOfType<ScoreManager>();
        if (gameStateManager == null)
            gameStateManager = GameStateManager.Instance;

        // Create debug UI if not assigned
        if (debugPanel == null || debugText == null)
        {
            CreateDebugUI();
        }

        // Set initial visibility
        _isVisible = startVisible;
        if (debugPanel != null)
            debugPanel.SetActive(_isVisible);
    }

    void Start()
    {
        // Try to find GameStateManager again in case it wasn't ready in Awake
        if (gameStateManager == null)
            gameStateManager = GameStateManager.Instance;
    }

    void Update()
    {
        // Toggle debug overlay with F1
        if (Input.GetKeyDown(KeyCode.F1))
        {
            ToggleDebugOverlay();
        }

        // Time scale controls (available in DEBUG builds)
        #if DEBUG || UNITY_EDITOR
        HandleTimeScaleControls();
        #endif

        // Update FPS tracking
        if (showFPS)
        {
            UpdateFPSTracking();
        }

        // Update debug text periodically
        if (_isVisible)
        {
            _debugUpdateTimer += Time.unscaledDeltaTime;
            if (_debugUpdateTimer >= updateInterval)
            {
                _debugUpdateTimer = 0f;
                UpdateDebugDisplay();
            }
        }
    }

    void CreateDebugUI()
    {
        // Create canvas if needed
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DebugCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        
        // Create debug panel
        debugPanel = new GameObject("DebugPanel");
        debugPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = debugPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0.5f);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(10, -10);
        panelRect.sizeDelta = new Vector2(500, 600);

        // Add background
        UnityEngine.UI.Image bgImage = debugPanel.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = backgroundColor;
        bgImage.raycastTarget = false;  // Don't block clicks
        
        // Create text
        GameObject textObj = new GameObject("DebugText");
        textObj.transform.SetParent(debugPanel.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        debugText = textObj.AddComponent<TextMeshProUGUI>();
        debugText.fontSize = 14;
        debugText.color = Color.white;
        debugText.alignment = TextAlignmentOptions.TopLeft;
        debugText.margin = new Vector4(10, 10, 10, 10);
        debugText.enableWordWrapping = false;
        debugText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    void ToggleDebugOverlay()
    {
        _isVisible = !_isVisible;
        if (debugPanel != null)
        {
            debugPanel.SetActive(_isVisible);
        }

        if (_isVisible)
        {
            UpdateDebugDisplay(); // Immediate update when showing
        }
    }

    void HandleTimeScaleControls()
    {
        // Timescale Hard Setters
        if (Input.GetKeyDown(KeyCode.Alpha1)) // 1 key
        {
            Time.timeScale = 1f; // normal
            Debug.Log("Time scale: " + Time.timeScale);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2)) // 2 key
        {
            Time.timeScale = 2f; // fast
            Debug.Log("Time scale: " + Time.timeScale);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3)) // 3 key
        {
            Time.timeScale = 3f; // ultra
            Debug.Log("Time scale: " + Time.timeScale);
        }
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Alpha0)) // 4 key
        {
            Time.timeScale = 0f; // pause
            Debug.Log("Time scale: " + Time.timeScale);
        }
        
        // Timescale soft incrementors
        if (Input.GetKeyDown(KeyCode.Minus))
        {
            if (Time.timeScale != 0f)
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    Time.timeScale -= 0.01f;
                    Debug.Log("Time scale: " + Time.timeScale);
                }
                else
                {
                    Time.timeScale -= 0.1f;
                    Debug.Log("Time scale: " + Time.timeScale);  
                }
            }
        }
        
        if (Input.GetKeyDown(KeyCode.Equals))
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                Time.timeScale += 0.01f;
                Debug.Log("Time scale: " + Time.timeScale);
            }
            else
            {
                Time.timeScale += 0.1f;
                Debug.Log("Time scale: " + Time.timeScale);  
            }
        }
        
    }

    void UpdateFPSTracking()
    {
        // Store FPS in circular buffer
        _fpsBuffer[_fpsBufferIndex] = 1f / Time.unscaledDeltaTime;
        _fpsBufferIndex = (_fpsBufferIndex + 1) % fpsAveragingFrames;
    }

    float GetAverageFPS()
    {
        float sum = 0f;
        for (int i = 0; i < fpsAveragingFrames; i++)
        {
            sum += _fpsBuffer[i];
        }
        return sum / fpsAveragingFrames;
    }

    void UpdateDebugDisplay()
    {
        if (debugText == null) return;

        _sb.Clear();
        _sb.AppendLine("=== DEBUG OVERLAY (F1 to toggle) ===");
        _sb.AppendLine();

        // Performance Section
        if (showFPS || showMemory)
        {
            _sb.AppendLine("--- PERFORMANCE ---");

            if (showFPS)
            {
                float avgFPS = GetAverageFPS();
                float currentFPS = 1f / Time.unscaledDeltaTime;
                _sb.AppendLine($"FPS: {currentFPS:F0} (Avg: {avgFPS:F0})");
                _sb.AppendLine($"Frame Time: {Time.unscaledDeltaTime * 1000f:F2}ms");
            }

            if (showMemory)
            {
                float memoryMB = (System.GC.GetTotalMemory(false) / 1024f / 1024f);
                _sb.AppendLine($"Memory: {memoryMB:F2} MB");
            }

            _sb.AppendLine($"Time Scale: {Time.timeScale:F2}x");
            _sb.AppendLine();
        }

        // Game State Section
        _sb.AppendLine("--- GAME STATE ---");
        if (gameStateManager != null)
        {
            _sb.AppendLine($"State: {gameStateManager.CurrentState}");
            _sb.AppendLine($"Paused: {gameStateManager.IsPaused}");
            _sb.AppendLine($"Game Over: {gameStateManager.IsGameOver}");
        }
        else
        {
            _sb.AppendLine("GameStateManager: NOT FOUND");
        }
        _sb.AppendLine();

        // Score Section
        if (scoreManager != null)
        {
            _sb.AppendLine("--- SCORE ---");
            _sb.AppendLine($"Score: {scoreManager.GetScore()}");
            _sb.AppendLine($"Combo: x{scoreManager.GetCombo()}");
            _sb.AppendLine();
        }

        // Grid Section
        if (gridManager != null)
        {
            _sb.AppendLine("--- GRID STATE ---");
            _sb.AppendLine($"Swapping: {gridManager.IsSwapping}");

            if (matchProcessor != null)
            {
                _sb.AppendLine($"Processing Matches: {matchProcessor.IsProcessingMatches}");
            }
            _sb.AppendLine();
        }

        // GridRiser Section
        if (gridRiser != null)
        {
            _sb.AppendLine("--- GRID RISER ---");
            _sb.AppendLine($"Grid Offset: {gridRiser.CurrentGridOffset:F3}");
            _sb.AppendLine($"Grace Period: {gridRiser.IsInGracePeriod}");
            _sb.AppendLine($"Game Over: {gridRiser.IsGameOver}");
            _sb.AppendLine();
        }

        // Cursor Section
        if (cursorController != null)
        {
            _sb.AppendLine("--- CURSOR ---");
            var cursorPos = cursorController.CursorPosition;
            _sb.AppendLine($"Position: ({cursorPos.x}, {cursorPos.y})");
            _sb.AppendLine();
        }

        debugText.text = _sb.ToString();
    }

    // Public method to manually update (can be called from other scripts)
    public void ForceUpdate()
    {
        if (_isVisible)
        {
            UpdateDebugDisplay();
        }
    }

    // Public method to show specific message
    public void LogDebugMessage(string message)
    {
        if (_isVisible && debugText != null)
        {
            _sb.Clear();
            _sb.AppendLine(debugText.text);
            _sb.AppendLine();
            _sb.AppendLine("--- CUSTOM MESSAGE ---");
            _sb.AppendLine(message);
            debugText.text = _sb.ToString();
        }
    }
}
