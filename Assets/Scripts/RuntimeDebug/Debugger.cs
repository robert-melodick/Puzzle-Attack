using PuzzleAttack.Grid;
using TMPro;
using UnityEngine;
using System.Text;
using System.Collections;

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
    private float[] fpsBuffer;
    private int fpsBufferIndex;
    private float fpsUpdateTimer;

    // Memory tracking
    private float memoryUpdateTimer;
    private float lastMemoryUsage;

    // State
    private bool isVisible;
    private float debugUpdateTimer;
    private StringBuilder sb;

    // Performance metrics
    private int frameCount;
    private float deltaTimeSum;

    void Awake()
    {
        // Initialize FPS buffer
        fpsBuffer = new float[fpsAveragingFrames];
        sb = new StringBuilder(1024);

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
        isVisible = startVisible;
        if (debugPanel != null)
            debugPanel.SetActive(isVisible);
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
        if (isVisible)
        {
            debugUpdateTimer += Time.unscaledDeltaTime;
            if (debugUpdateTimer >= updateInterval)
            {
                debugUpdateTimer = 0f;
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
        isVisible = !isVisible;
        if (debugPanel != null)
        {
            debugPanel.SetActive(isVisible);
        }

        if (isVisible)
        {
            UpdateDebugDisplay(); // Immediate update when showing
        }
    }

    void HandleTimeScaleControls()
    {
        if (Input.GetKeyDown(KeyCode.Minus)) // - key
        {
            Time.timeScale = 0.1f; // slow motion
            Debug.Log("Time scale: 0.1x (Slow Motion)");
        }
        if (Input.GetKeyDown(KeyCode.Equals)) // = key
        {
            Time.timeScale = 1f; // normal
            Debug.Log("Time scale: 1.0x (Normal)");
        }
        if (Input.GetKeyDown(KeyCode.Alpha0)) // 0 key
        {
            Time.timeScale = 0f; // freeze
            Debug.Log("Time scale: 0.0x (Frozen)");
        }
        if (Input.GetKeyDown(KeyCode.Alpha9)) // 9 key
        {
            Time.timeScale = 2f; // fast
            Debug.Log("Time scale: 2.0x (Fast)");
        }
    }

    void UpdateFPSTracking()
    {
        // Store FPS in circular buffer
        fpsBuffer[fpsBufferIndex] = 1f / Time.unscaledDeltaTime;
        fpsBufferIndex = (fpsBufferIndex + 1) % fpsAveragingFrames;
    }

    float GetAverageFPS()
    {
        float sum = 0f;
        for (int i = 0; i < fpsAveragingFrames; i++)
        {
            sum += fpsBuffer[i];
        }
        return sum / fpsAveragingFrames;
    }

    void UpdateDebugDisplay()
    {
        if (debugText == null) return;

        sb.Clear();
        sb.AppendLine("=== DEBUG OVERLAY (F1 to toggle) ===");
        sb.AppendLine();

        // Performance Section
        if (showFPS || showMemory)
        {
            sb.AppendLine("--- PERFORMANCE ---");

            if (showFPS)
            {
                float avgFPS = GetAverageFPS();
                float currentFPS = 1f / Time.unscaledDeltaTime;
                sb.AppendLine($"FPS: {currentFPS:F0} (Avg: {avgFPS:F0})");
                sb.AppendLine($"Frame Time: {Time.unscaledDeltaTime * 1000f:F2}ms");
            }

            if (showMemory)
            {
                float memoryMB = (System.GC.GetTotalMemory(false) / 1024f / 1024f);
                sb.AppendLine($"Memory: {memoryMB:F2} MB");
            }

            sb.AppendLine($"Time Scale: {Time.timeScale:F2}x");
            sb.AppendLine();
        }

        // Game State Section
        sb.AppendLine("--- GAME STATE ---");
        if (gameStateManager != null)
        {
            sb.AppendLine($"State: {gameStateManager.CurrentState}");
            sb.AppendLine($"Paused: {gameStateManager.IsPaused}");
            sb.AppendLine($"Game Over: {gameStateManager.IsGameOver}");
        }
        else
        {
            sb.AppendLine("GameStateManager: NOT FOUND");
        }
        sb.AppendLine();

        // Score Section
        if (scoreManager != null)
        {
            sb.AppendLine("--- SCORE ---");
            sb.AppendLine($"Score: {scoreManager.GetScore()}");
            sb.AppendLine($"Combo: x{scoreManager.GetCombo()}");
            sb.AppendLine();
        }

        // Grid Section
        if (gridManager != null)
        {
            sb.AppendLine("--- GRID STATE ---");
            sb.AppendLine($"Swapping: {gridManager.IsSwapping}");

            if (matchProcessor != null)
            {
                sb.AppendLine($"Processing Matches: {matchProcessor.IsProcessingMatches}");
            }
            sb.AppendLine();
        }

        // GridRiser Section
        if (gridRiser != null)
        {
            sb.AppendLine("--- GRID RISER ---");
            sb.AppendLine($"Grid Offset: {gridRiser.CurrentGridOffset:F3}");
            sb.AppendLine($"Grace Period: {gridRiser.IsInGracePeriod}");
            sb.AppendLine($"Game Over: {gridRiser.IsGameOver}");
            sb.AppendLine();
        }

        // Cursor Section
        if (cursorController != null)
        {
            sb.AppendLine("--- CURSOR ---");
            var cursorPos = cursorController.CursorPosition;
            sb.AppendLine($"Position: ({cursorPos.x}, {cursorPos.y})");
            sb.AppendLine();
        }

        // Controls Help
        #if DEBUG || UNITY_EDITOR
        sb.AppendLine("--- TIME CONTROLS (Debug Only) ---");
        sb.AppendLine("0: Freeze  |  -: 0.1x  |  =: 1.0x  |  9: 2.0x");
        #endif

        debugText.text = sb.ToString();
    }

    // Public method to manually update (can be called from other scripts)
    public void ForceUpdate()
    {
        if (isVisible)
        {
            UpdateDebugDisplay();
        }
    }

    // Public method to show specific message
    public void LogDebugMessage(string message)
    {
        if (isVisible && debugText != null)
        {
            sb.Clear();
            sb.AppendLine(debugText.text);
            sb.AppendLine();
            sb.AppendLine("--- CUSTOM MESSAGE ---");
            sb.AppendLine(message);
            debugText.text = sb.ToString();
        }
    }
}
