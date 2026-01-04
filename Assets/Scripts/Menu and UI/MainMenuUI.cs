using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PuzzleAttack;
using PuzzleAttack.Grid.AI;

/// <summary>
/// Main menu UI controller with:
/// - Marathon Mode: Single player endless
/// - VS Mode: 1v1 with CPU, supports arcade-style player 2 drop-in
/// - Party Mode: 2-4 players, flexible slot configuration
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    #region Inspector Fields - Panels

    [Header("Main Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject modeSelectPanel;
    [SerializeField] private GameObject marathonSetupPanel;
    [SerializeField] private GameObject vsSetupPanel;
    [SerializeField] private GameObject partySetupPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject leaderboardPanel;

    #endregion

    #region Inspector Fields - Main Menu Buttons

    [Header("Main Menu Buttons")]
    [SerializeField] private Button gamemodeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button leaderboardButton;
    [SerializeField] private Button exitButton;

    #endregion

    #region Inspector Fields - Mode Select Buttons

    [Header("Mode Select Buttons")]
    [SerializeField] private Button marathonButton;
    [SerializeField] private Button vsButton;
    [SerializeField] private Button partyButton;
    [SerializeField] private Button sandboxButton;
    [SerializeField] private Button modeSelectBackButton;

    #endregion

    #region Inspector Fields - Settings

    [Header("Settings Panel")]
    [SerializeField] private Button settingsBackButton;
    // TODO: Add settings controls (audio sliders, controls rebinding, etc.)

    #endregion

    #region Inspector Fields - Marathon Setup

    [Header("Marathon Setup")]
    [SerializeField] private TMP_Dropdown marathonDifficultyDropdown;
    [SerializeField] private TMP_Dropdown marathonSpeedDropdown;
    [SerializeField] private TextMeshProUGUI marathonDifficultyDescription;
    [SerializeField] private Button marathonStartButton;
    [SerializeField] private Button marathonBackButton;

    #endregion

    #region Inspector Fields - VS Setup (1v1 Arcade Style)

    [Header("VS Setup - 1v1 Arcade Style")]
    [SerializeField] private PlayerSlotUI vsPlayer1Slot;
    [SerializeField] private PlayerSlotUI vsPlayer2Slot;
    [SerializeField] private TextMeshProUGUI vsStatusText;
    [SerializeField] private Button vsStartButton;
    [SerializeField] private Button vsBackButton;
    
    [Header("VS Mode - Player 2 Join")]
    [SerializeField] private TextMeshProUGUI player2JoinPrompt;

    #endregion

    #region Inspector Fields - Party Setup (2-4 Players)

    [Header("Party Setup - 2-4 Players")]
    [SerializeField] private List<PlayerSlotUI> partySlots = new List<PlayerSlotUI>();
    [SerializeField] private TextMeshProUGUI partyStatusText;
    [SerializeField] private Button partyStartButton;
    [SerializeField] private Button partyBackButton;

    #endregion

    #region Inspector Fields - Leaderboard

    [Header("Leaderboard UI")]
    [SerializeField] private Transform leaderboardEntriesContainer;
    [SerializeField] private GameObject leaderboardEntryPrefab;
    [SerializeField] private Button leaderboardBackButton;

    #endregion

    #region Inspector Fields - Presets

    [Header("Available Presets")]
    [SerializeField] private List<GameModeConfig> gameModes = new List<GameModeConfig>();
    [SerializeField] private List<GridDifficultySettings> gridDifficulties = new List<GridDifficultySettings>();
    [SerializeField] private List<AIDifficultySettings> aiDifficulties = new List<AIDifficultySettings>();

    #endregion

    #region Private Fields

    private List<GameObject> _instantiatedLeaderboardEntries = new List<GameObject>();
    private bool _vsPlayer2Joined;

    // Input detection for arcade-style join
    private float _inputCheckTimer;
    private const float InputCheckInterval = 0.1f;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        InitializeGameModeManager();
        SetupDropdowns();
        SetupButtons();
        ShowMainMenu();
    }

    private void Update()
    {
        // Check for player 2 join in VS mode
        if (vsSetupPanel != null && vsSetupPanel.activeSelf && !_vsPlayer2Joined)
        {
            CheckForPlayer2Join();
        }

        // Check for player joins in Party mode
        if (partySetupPanel != null && partySetupPanel.activeSelf)
        {
            CheckForPartyJoins();
        }
    }

    #endregion

    #region Initialization

    private void InitializeGameModeManager()
    {
        var manager = GameModeManager.Instance;
        manager.RegisterPresets(gameModes, aiDifficulties, gridDifficulties);
    }

    private void SetupDropdowns()
    {
        // Marathon difficulty dropdown
        if (marathonDifficultyDropdown != null && gridDifficulties.Count > 0)
        {
            marathonDifficultyDropdown.ClearOptions();
            var options = new List<string>();
            foreach (var diff in gridDifficulties)
            {
                options.Add(diff.displayName);
            }
            marathonDifficultyDropdown.AddOptions(options);
            marathonDifficultyDropdown.onValueChanged.AddListener(OnMarathonDifficultyChanged);
            
            // Default to Normal
            int normalIndex = gridDifficulties.FindIndex(d => d.displayName.ToLower() == "normal");
            marathonDifficultyDropdown.value = Mathf.Max(0, normalIndex);
        }

        // Marathon speed dropdown
        if (marathonSpeedDropdown != null)
        {
            marathonSpeedDropdown.ClearOptions();
            var speedOptions = new List<string>();
            for (int i = 1; i <= 20; i++)
            {
                speedOptions.Add($"Level {i}");
            }
            marathonSpeedDropdown.AddOptions(speedOptions);
        }
    }

    private void SetupButtons()
    {
        // Main menu
        if (gamemodeButton != null) gamemodeButton.onClick.AddListener(OnGamemodeClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (leaderboardButton != null) leaderboardButton.onClick.AddListener(OnLeaderboardClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);

        // Mode select
        if (marathonButton != null) marathonButton.onClick.AddListener(OnMarathonClicked);
        if (vsButton != null) vsButton.onClick.AddListener(OnVsClicked);
        if (partyButton != null) partyButton.onClick.AddListener(OnPartyClicked);
        if (sandboxButton != null) sandboxButton.onClick.AddListener(OnSandboxClicked);
        if (modeSelectBackButton != null) modeSelectBackButton.onClick.AddListener(ShowMainMenu);

        // Marathon
        if (marathonStartButton != null) marathonStartButton.onClick.AddListener(OnMarathonStart);
        if (marathonBackButton != null) marathonBackButton.onClick.AddListener(ShowModeSelect);

        // VS Mode
        if (vsStartButton != null) vsStartButton.onClick.AddListener(OnVsStart);
        if (vsBackButton != null) vsBackButton.onClick.AddListener(ShowModeSelect);

        // Party Mode
        if (partyStartButton != null) partyStartButton.onClick.AddListener(OnPartyStart);
        if (partyBackButton != null) partyBackButton.onClick.AddListener(ShowModeSelect);

        // Settings
        if (settingsBackButton != null) settingsBackButton.onClick.AddListener(ShowMainMenu);

        // Leaderboard
        if (leaderboardBackButton != null) leaderboardBackButton.onClick.AddListener(ShowMainMenu);
    }

    #endregion

    #region Panel Navigation

    private void HideAllPanels()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (modeSelectPanel != null) modeSelectPanel.SetActive(false);
        if (marathonSetupPanel != null) marathonSetupPanel.SetActive(false);
        if (vsSetupPanel != null) vsSetupPanel.SetActive(false);
        if (partySetupPanel != null) partySetupPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
    }

    private void ShowMainMenu()
    {
        HideAllPanels();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    private void ShowModeSelect()
    {
        HideAllPanels();
        if (modeSelectPanel != null) modeSelectPanel.SetActive(true);
    }

    #endregion

    #region Main Menu Actions

    private void OnGamemodeClicked()
    {
        ShowModeSelect();
    }

    private void OnSettingsClicked()
    {
        HideAllPanels();
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    private void OnExitClicked()
    {
        Application.Quit();
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    #endregion

    #region Marathon Mode

    private void OnMarathonClicked()
    {
        HideAllPanels();
        if (marathonSetupPanel != null) marathonSetupPanel.SetActive(true);
        
        // Update description
        OnMarathonDifficultyChanged(marathonDifficultyDropdown?.value ?? 0);
    }

    private void OnMarathonDifficultyChanged(int index)
    {
        if (marathonDifficultyDescription != null && index >= 0 && index < gridDifficulties.Count)
        {
            marathonDifficultyDescription.text = gridDifficulties[index].description;
        }
    }

    private void OnMarathonStart()
    {
        var manager = GameModeManager.Instance;
        
        // Find Marathon mode config
        var marathonMode = gameModes.Find(m => m.modeType == GameModeType.Marathon);
        manager.SetGameMode(marathonMode);

        // Get settings
        GridDifficultySettings gridDiff = null;
        if (marathonDifficultyDropdown != null && marathonDifficultyDropdown.value < gridDifficulties.Count)
        {
            gridDiff = gridDifficulties[marathonDifficultyDropdown.value];
        }

        int startingSpeed = (marathonSpeedDropdown?.value ?? 0) + 1;

        // Setup and start
        manager.SetupMarathon(gridDiff, startingSpeed);
        manager.LogConfiguration();
        manager.StartGame();
    }

    #endregion

    #region VS Mode (1v1 Arcade Style)

    private void OnVsClicked()
    {
        HideAllPanels();
        if (vsSetupPanel != null) vsSetupPanel.SetActive(true);
        
        _vsPlayer2Joined = false;
        InitializeVsMode();
    }

    private void InitializeVsMode()
    {
        // Initialize Player 1 slot (always human, locked)
        if (vsPlayer1Slot != null)
        {
            vsPlayer1Slot.Initialize(0, gridDifficulties, aiDifficulties, OnVsSlotChanged);
            vsPlayer1Slot.SetMode(PlayerSlotUI.SlotMode.Human);
            vsPlayer1Slot.SetLocked(true);
            vsPlayer1Slot.SetInputDeviceAvailable(true);
        }

        // Initialize Player 2 slot (CPU by default, can be joined by human)
        if (vsPlayer2Slot != null)
        {
            vsPlayer2Slot.Initialize(1, gridDifficulties, aiDifficulties, OnVsSlotChanged);
            vsPlayer2Slot.SetMode(PlayerSlotUI.SlotMode.CPU);
            vsPlayer2Slot.SetLocked(false);
            vsPlayer2Slot.SetInputDeviceAvailable(HasSecondInputDevice());
        }

        UpdateVsStatus();
    }

    private void OnVsSlotChanged(int slotIndex, PlayerSlotConfig config)
    {
        UpdateVsStatus();
    }

    private void UpdateVsStatus()
    {
        if (vsStatusText == null) return;

        bool p2IsHuman = vsPlayer2Slot != null && vsPlayer2Slot.IsHuman;
        bool p2IsCPU = vsPlayer2Slot != null && vsPlayer2Slot.IsCPU;

        if (p2IsHuman)
        {
            vsStatusText.text = "Player vs Player - Ready!";
        }
        else if (p2IsCPU)
        {
            var config = vsPlayer2Slot.GetConfig();
            string aiName = config.aiDifficulty?.displayName ?? "CPU";
            vsStatusText.text = $"Player vs {aiName}";
        }

        // Update join prompt
        if (player2JoinPrompt != null)
        {
            player2JoinPrompt.gameObject.SetActive(!p2IsHuman && HasSecondInputDevice());
            if (!p2IsHuman)
            {
                player2JoinPrompt.text = "Player 2: Press Start to Join!";
            }
        }
    }

    private void CheckForPlayer2Join()
    {
        _inputCheckTimer += Time.deltaTime;
        if (_inputCheckTimer < InputCheckInterval) return;
        _inputCheckTimer = 0f;

        // Check for second controller/keyboard input
        // You'll want to customize this based on your input system
        
        // Example: Check for specific "join" buttons
        // Gamepad: Start button on second controller
        // Keyboard: Enter key (if using split keyboard)
        
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            // This is a placeholder - integrate with your input system
            if (HasSecondInputDevice() && vsPlayer2Slot != null && !vsPlayer2Slot.IsHuman)
            {
                Player2JoinVs();
            }
        }

        // Check for gamepad start buttons (example using old input system)
        // For new Input System, you'd use PlayerInput or InputAction
        for (int i = 1; i <= 4; i++)
        {
            string startButton = $"joystick {i} button 7"; // Common "Start" button
            try
            {
                if (Input.GetKeyDown(startButton))
                {
                    if (vsPlayer2Slot != null && !vsPlayer2Slot.IsHuman)
                    {
                        Player2JoinVs(i);
                        break;
                    }
                }
            }
            catch { } // Ignore if button doesn't exist
        }
    }

    private void Player2JoinVs(int inputDeviceIndex = 1)
    {
        if (vsPlayer2Slot == null) return;
        
        _vsPlayer2Joined = true;
        vsPlayer2Slot.PlayerJoined(inputDeviceIndex);
        UpdateVsStatus();
        
        Debug.Log($"[MainMenu] Player 2 joined VS mode with input device {inputDeviceIndex}");
    }

    private void OnVsStart()
    {
        var manager = GameModeManager.Instance;

        // Set mode based on whether P2 is human or CPU
        bool p2IsHuman = vsPlayer2Slot != null && vsPlayer2Slot.IsHuman;
        var mode = gameModes.Find(m => m.modeType == (p2IsHuman ? GameModeType.VsHuman : GameModeType.VsCPU));
        manager.SetGameMode(mode);

        // Configure slots
        if (vsPlayer1Slot != null)
            manager.ConfigurePlayerSlot(0, vsPlayer1Slot.GetConfig());
        if (vsPlayer2Slot != null)
            manager.ConfigurePlayerSlot(1, vsPlayer2Slot.GetConfig());

        manager.SetPlayerCount(2);

        if (manager.ValidateConfiguration())
        {
            manager.LogConfiguration();
            manager.StartGame();
        }
        else
        {
            Debug.LogError("[MainMenu] VS Mode configuration invalid!");
        }
    }

    #endregion

    #region Sandbox Mode

    private void OnSandboxClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("sandbox");
    }

    #endregion

    #region Party Mode (2-4 Players)

    private void OnPartyClicked()
    {
        HideAllPanels();
        if (partySetupPanel != null) partySetupPanel.SetActive(true);
        
        InitializePartyMode();
    }

    private void InitializePartyMode()
    {
        int availableDevices = GetAvailableInputDeviceCount();

        for (int i = 0; i < partySlots.Count; i++)
        {
            var slot = partySlots[i];
            if (slot == null) continue;

            slot.Initialize(i, gridDifficulties, aiDifficulties, OnPartySlotChanged);
            
            bool hasDevice = i < availableDevices;
            slot.SetInputDeviceAvailable(hasDevice);

            if (i == 0)
            {
                // Slot 0: Always human, locked
                slot.SetMode(PlayerSlotUI.SlotMode.Human);
                slot.SetLocked(true);
            }
            else if (i == 1)
            {
                // Slot 1: CPU by default (can be changed to human if device available)
                slot.SetMode(PlayerSlotUI.SlotMode.CPU);
                slot.SetLocked(false);
            }
            else
            {
                // Slots 2-3: Empty by default
                slot.SetMode(PlayerSlotUI.SlotMode.Empty);
                slot.SetLocked(false);
            }
        }

        UpdatePartyStatus();
    }

    private void OnPartySlotChanged(int slotIndex, PlayerSlotConfig config)
    {
        UpdatePartyStatus();
    }

    private void UpdatePartyStatus()
    {
        if (partyStatusText == null) return;

        int humanCount = 0;
        int cpuCount = 0;
        int activeCount = 0;

        foreach (var slot in partySlots)
        {
            if (slot == null) continue;
            if (slot.IsHuman) { humanCount++; activeCount++; }
            else if (slot.IsCPU) { cpuCount++; activeCount++; }
        }

        if (activeCount < 2)
        {
            partyStatusText.text = "Need at least 2 players!";
            if (partyStartButton != null) partyStartButton.interactable = false;
        }
        else
        {
            string statusParts = "";
            if (humanCount > 0) statusParts += $"{humanCount} Human";
            if (humanCount > 0 && cpuCount > 0) statusParts += " vs ";
            if (cpuCount > 0) statusParts += $"{cpuCount} CPU";
            
            partyStatusText.text = $"{activeCount} Players Ready! ({statusParts})";
            if (partyStartButton != null) partyStartButton.interactable = true;
        }
    }

    private void CheckForPartyJoins()
    {
        // Similar to VS mode, but for multiple slots
        // Check for input devices pressing "join" and assign to empty/waiting slots
        
        _inputCheckTimer += Time.deltaTime;
        if (_inputCheckTimer < InputCheckInterval) return;
        _inputCheckTimer = 0f;

        // This is where you'd integrate with your input system
        // to detect new devices wanting to join
    }

    private void OnPartyStart()
    {
        var manager = GameModeManager.Instance;

        // Determine mode type based on composition
        int humanCount = 0;
        int cpuCount = 0;
        foreach (var slot in partySlots)
        {
            if (slot == null) continue;
            if (slot.IsHuman) humanCount++;
            else if (slot.IsCPU) cpuCount++;
        }

        GameModeType modeType;
        if (cpuCount == 0) modeType = GameModeType.VsHuman;
        else if (humanCount == 1) modeType = GameModeType.VsCPU;
        else modeType = GameModeType.Mixed;

        var mode = gameModes.Find(m => m.modeType == modeType);
        if (mode == null) mode = gameModes.Find(m => m.modeType == GameModeType.Mixed);
        manager.SetGameMode(mode);

        // Configure all active slots
        int activeCount = 0;
        for (int i = 0; i < partySlots.Count; i++)
        {
            var slot = partySlots[i];
            if (slot == null) continue;
            
            if (slot.IsActive)
            {
                manager.ConfigurePlayerSlot(i, slot.GetConfig());
                activeCount++;
            }
        }

        manager.SetPlayerCount(activeCount);

        if (manager.ValidateConfiguration())
        {
            manager.LogConfiguration();
            manager.StartGame();
        }
        else
        {
            Debug.LogError("[MainMenu] Party Mode configuration invalid!");
        }
    }

    #endregion

    #region Input Device Detection

    /// <summary>
    /// Check if a second input device is available.
    /// Override/expand this based on your input system.
    /// </summary>
    private bool HasSecondInputDevice()
    {
        // Basic check - you'll want to integrate with your input system
        // This checks if any gamepad is connected
        string[] joysticks = Input.GetJoystickNames();
        foreach (var name in joysticks)
        {
            if (!string.IsNullOrEmpty(name))
                return true;
        }
        
        // Or if you support split keyboard, always return true
        return true; // Assume keyboard can be split for 2 players
    }

    /// <summary>
    /// Get count of available input devices.
    /// Override/expand this based on your input system.
    /// </summary>
    private int GetAvailableInputDeviceCount()
    {
        int count = 1; // Keyboard always counts as 1
        
        string[] joysticks = Input.GetJoystickNames();
        foreach (var name in joysticks)
        {
            if (!string.IsNullOrEmpty(name))
                count++;
        }
        
        return Mathf.Min(count, 4); // Cap at 4
    }

    #endregion

    #region Leaderboard

    private void OnLeaderboardClicked()
    {
        HideAllPanels();
        if (leaderboardPanel != null) leaderboardPanel.SetActive(true);
        PopulateLeaderboard();
    }

    private void PopulateLeaderboard()
    {
        ClearLeaderboardEntries();

        if (HighScoreManager.Instance == null)
        {
            Debug.LogWarning("HighScoreManager not found!");
            return;
        }

        var highScores = HighScoreManager.Instance.GetHighScores();

        if (leaderboardEntryPrefab == null || leaderboardEntriesContainer == null)
        {
            Debug.LogWarning("Leaderboard prefab or container not assigned!");
            return;
        }

        int maxEntries = 10;
        for (int i = 0; i < maxEntries; i++)
        {
            var entryObj = Instantiate(leaderboardEntryPrefab, leaderboardEntriesContainer);
            _instantiatedLeaderboardEntries.Add(entryObj);

            var rankText = entryObj.transform.Find("RankText")?.GetComponent<TextMeshProUGUI>();
            var scoreText = entryObj.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
            var comboText = entryObj.transform.Find("ComboText")?.GetComponent<TextMeshProUGUI>();
            var speedText = entryObj.transform.Find("SpeedLevelText")?.GetComponent<TextMeshProUGUI>();
            var dateText = entryObj.transform.Find("DateText")?.GetComponent<TextMeshProUGUI>();

            if (i < highScores.Count)
            {
                var entry = highScores[i];
                if (rankText != null) rankText.text = $"#{i + 1}";
                if (scoreText != null) scoreText.text = entry.score.ToString("N0");
                if (comboText != null) comboText.text = $"x{entry.highestCombo}";
                if (speedText != null) speedText.text = $"Lv{Mathf.Max(1, entry.speedLevel)}";
                if (dateText != null) dateText.text = entry.date;
            }
            else
            {
                if (rankText != null) rankText.text = $"#{i + 1}";
                if (scoreText != null) scoreText.text = "---";
                if (comboText != null) comboText.text = "---";
                if (speedText != null) speedText.text = "---";
                if (dateText != null) dateText.text = "---";
            }
        }
    }

    private void ClearLeaderboardEntries()
    {
        foreach (var entry in _instantiatedLeaderboardEntries)
        {
            if (entry != null) Destroy(entry);
        }
        _instantiatedLeaderboardEntries.Clear();
    }

    #endregion

    #region Legacy Support (for existing button hookups)

    // These methods maintain compatibility with existing button OnClick events
    public void OnStartGame() => OnGamemodeClicked();
    public void OnStartSandbox() => OnSandboxClicked();
    public void OnLeaderboard() => OnLeaderboardClicked();
    public void OnCloseLeaderboard() => ShowMainMenu();
    public void OnQuitGame() => OnExitClicked();

    #endregion
}