using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PuzzleAttack;
using PuzzleAttack.Grid.AI;

/// <summary>
/// UI component for configuring a single player slot.
/// Simplified design supporting arcade-style drop-in and Party Mode.
/// </summary>
public class PlayerSlotUI : MonoBehaviour
{
    #region Inspector Fields

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI playerLabel;
    [SerializeField] private TextMeshProUGUI statusLabel;
    [SerializeField] private Image slotBackground;
    [SerializeField] private Image playerIconImage;
    
    [Header("Sprites (Drag your sprites here)")]
    [SerializeField] private Sprite humanIconSprite;
    [SerializeField] private Sprite cpuIconSprite;
    [SerializeField] private Sprite emptyIconSprite;

    [Header("Slot Controls")]
    [Tooltip("Button to cycle through slot states (Empty/CPU/Human if device available)")]
    [SerializeField] private Button cycleSlotButton;
    
    [Tooltip("Text showing current slot type")]
    [SerializeField] private TextMeshProUGUI slotTypeText;

    [Header("CPU Settings (shown when slot is CPU)")]
    [SerializeField] private GameObject cpuSettingsPanel;
    [SerializeField] private TMP_Dropdown aiDifficultyDropdown;

    [Header("Shared Settings")]
    [SerializeField] private TMP_Dropdown gridDifficultyDropdown;
    [SerializeField] private TMP_Dropdown startingSpeedDropdown;

    [Header("Join Prompt (for arcade-style drop-in)")]
    [SerializeField] private GameObject joinPromptPanel;
    [SerializeField] private TextMeshProUGUI joinPromptText;

    [Header("Visual Feedback Colors")]
    [SerializeField] private Color humanColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color cpuColor = new Color(1f, 0.5f, 0.3f, 1f);
    [SerializeField] private Color emptyColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private Color waitingForPlayerColor = new Color(0.5f, 0.5f, 0.2f, 0.7f);

    #endregion

    #region Private Fields

    private int _slotIndex;
    private PlayerSlotConfig _config;
    private List<GridDifficultySettings> _gridDifficulties;
    private List<AIDifficultySettings> _aiDifficulties;
    private Action<int, PlayerSlotConfig> _onConfigChanged;
    private bool _isInitialized;
    private bool _isLocked; // Slot 0 is often locked to human
    private bool _inputDeviceAvailable;
    private SlotMode _currentMode;

    #endregion

    #region Enums

    public enum SlotMode
    {
        Empty,
        CPU,
        Human,
        WaitingForPlayer  // Arcade-style "Press Start to Join"
    }

    #endregion

    #region Properties

    public SlotMode CurrentMode => _currentMode;
    public bool IsActive => _currentMode != SlotMode.Empty && _currentMode != SlotMode.WaitingForPlayer;
    public bool IsHuman => _currentMode == SlotMode.Human;
    public bool IsCPU => _currentMode == SlotMode.CPU;
    public int SlotIndex => _slotIndex;

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize the slot UI.
    /// </summary>
    public void Initialize(
        int slotIndex,
        List<GridDifficultySettings> gridDifficulties,
        List<AIDifficultySettings> aiDifficulties,
        Action<int, PlayerSlotConfig> onConfigChanged)
    {
        _slotIndex = slotIndex;
        _gridDifficulties = gridDifficulties ?? new List<GridDifficultySettings>();
        _aiDifficulties = aiDifficulties ?? new List<AIDifficultySettings>();
        _onConfigChanged = onConfigChanged;

        _config = new PlayerSlotConfig
        {
            playerIndex = slotIndex,
            isActive = false
        };

        SetupDropdowns();
        SetupButtons();
        
        _isInitialized = true;
        
        // Default to empty
        SetMode(SlotMode.Empty);
    }

    private void SetupDropdowns()
    {
        // AI Difficulty dropdown
        if (aiDifficultyDropdown != null && _aiDifficulties.Count > 0)
        {
            aiDifficultyDropdown.ClearOptions();
            var options = new List<string>();
            foreach (var diff in _aiDifficulties)
            {
                options.Add(diff.displayName);
            }
            aiDifficultyDropdown.AddOptions(options);
            
            // Default to Medium/Normal
            int defaultIndex = _aiDifficulties.FindIndex(d => 
                d.displayName.ToLower().Contains("medium") || 
                d.displayName.ToLower().Contains("normal"));
            aiDifficultyDropdown.value = Mathf.Max(0, defaultIndex);
            aiDifficultyDropdown.onValueChanged.AddListener(OnAIDifficultyChanged);
        }

        // Grid Difficulty dropdown
        if (gridDifficultyDropdown != null && _gridDifficulties.Count > 0)
        {
            gridDifficultyDropdown.ClearOptions();
            var options = new List<string>();
            foreach (var diff in _gridDifficulties)
            {
                options.Add(diff.displayName);
            }
            gridDifficultyDropdown.AddOptions(options);
            
            int defaultIndex = _gridDifficulties.FindIndex(d => 
                d.displayName.ToLower() == "normal");
            gridDifficultyDropdown.value = Mathf.Max(0, defaultIndex);
            gridDifficultyDropdown.onValueChanged.AddListener(OnGridDifficultyChanged);
        }

        // Starting Speed dropdown
        if (startingSpeedDropdown != null)
        {
            startingSpeedDropdown.ClearOptions();
            var options = new List<string>();
            for (int i = 1; i <= 20; i++)
            {
                options.Add($"Lv {i}");
            }
            startingSpeedDropdown.AddOptions(options);
            startingSpeedDropdown.value = 0;
            startingSpeedDropdown.onValueChanged.AddListener(OnStartingSpeedChanged);
        }
    }

    private void SetupButtons()
    {
        if (cycleSlotButton != null)
        {
            cycleSlotButton.onClick.AddListener(OnCycleSlotClicked);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Set the slot mode directly.
    /// </summary>
    public void SetMode(SlotMode mode)
    {
        _currentMode = mode;
        
        // Update config
        switch (mode)
        {
            case SlotMode.Empty:
                _config.isActive = false;
                _config.controllerType = PlayerControllerType.None;
                _config.playerName = "Empty";
                break;
                
            case SlotMode.CPU:
                _config.isActive = true;
                _config.controllerType = PlayerControllerType.CPU;
                _config.playerName = $"CPU {_slotIndex + 1}";
                UpdateAIDifficultyFromDropdown();
                break;
                
            case SlotMode.Human:
                _config.isActive = true;
                _config.controllerType = PlayerControllerType.Human;
                _config.playerName = $"P{_slotIndex + 1}";
                _config.inputDeviceIndex = _slotIndex;
                break;
                
            case SlotMode.WaitingForPlayer:
                _config.isActive = false;
                _config.controllerType = PlayerControllerType.None;
                _config.playerName = "Press Start";
                break;
        }

        UpdateVisuals();
        UpdateConfigFromUI();
        NotifyConfigChanged();
    }

    /// <summary>
    /// Lock the slot (prevents cycling, used for Player 1).
    /// </summary>
    public void SetLocked(bool locked)
    {
        _isLocked = locked;
        if (cycleSlotButton != null)
        {
            cycleSlotButton.interactable = !locked;
        }
    }

    /// <summary>
    /// Set whether an input device is available for this slot.
    /// </summary>
    public void SetInputDeviceAvailable(bool available)
    {
        _inputDeviceAvailable = available;
    }

    /// <summary>
    /// Called when a player "joins" this slot (arcade-style drop-in).
    /// </summary>
    public void PlayerJoined(int inputDeviceIndex)
    {
        _config.inputDeviceIndex = inputDeviceIndex;
        SetMode(SlotMode.Human);
    }

    /// <summary>
    /// Called when a player leaves this slot.
    /// </summary>
    public void PlayerLeft()
    {
        SetMode(SlotMode.WaitingForPlayer);
    }

    /// <summary>
    /// Get the current configuration.
    /// </summary>
    public PlayerSlotConfig GetConfig()
    {
        UpdateConfigFromUI();
        return _config;
    }

    /// <summary>
    /// Set interactable state for all controls.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (cycleSlotButton != null && !_isLocked)
            cycleSlotButton.interactable = interactable;
        if (aiDifficultyDropdown != null)
            aiDifficultyDropdown.interactable = interactable;
        if (gridDifficultyDropdown != null)
            gridDifficultyDropdown.interactable = interactable;
        if (startingSpeedDropdown != null)
            startingSpeedDropdown.interactable = interactable;
    }

    /// <summary>
    /// Show/hide the settings (grid difficulty, speed) for minimal UI modes.
    /// </summary>
    public void ShowSettings(bool show)
    {
        if (gridDifficultyDropdown != null)
            gridDifficultyDropdown.gameObject.SetActive(show);
        if (startingSpeedDropdown != null)
            startingSpeedDropdown.gameObject.SetActive(show);
    }

    #endregion

    #region UI Updates

    private void UpdateVisuals()
    {
        // Update background color
        if (slotBackground != null)
        {
            switch (_currentMode)
            {
                case SlotMode.Human:
                    slotBackground.color = humanColor;
                    break;
                case SlotMode.CPU:
                    slotBackground.color = cpuColor;
                    break;
                case SlotMode.WaitingForPlayer:
                    slotBackground.color = waitingForPlayerColor;
                    break;
                default:
                    slotBackground.color = emptyColor;
                    break;
            }
        }

        // Update icon
        if (playerIconImage != null)
        {
            switch (_currentMode)
            {
                case SlotMode.Human:
                    if (humanIconSprite != null)
                    {
                        playerIconImage.sprite = humanIconSprite;
                        playerIconImage.enabled = true;
                    }
                    break;
                case SlotMode.CPU:
                    if (cpuIconSprite != null)
                    {
                        playerIconImage.sprite = cpuIconSprite;
                        playerIconImage.enabled = true;
                    }
                    break;
                default:
                    if (emptyIconSprite != null)
                    {
                        playerIconImage.sprite = emptyIconSprite;
                        playerIconImage.enabled = true;
                    }
                    else
                    {
                        playerIconImage.enabled = false;
                    }
                    break;
            }
        }

        // Update labels
        if (playerLabel != null)
        {
            playerLabel.text = _currentMode == SlotMode.Empty ? $"Slot {_slotIndex + 1}" : _config.playerName;
        }

        if (slotTypeText != null)
        {
            switch (_currentMode)
            {
                case SlotMode.Human:
                    slotTypeText.text = "HUMAN";
                    break;
                case SlotMode.CPU:
                    slotTypeText.text = "CPU";
                    break;
                case SlotMode.WaitingForPlayer:
                    slotTypeText.text = "JOIN";
                    break;
                default:
                    slotTypeText.text = "OFF";
                    break;
            }
        }

        if (statusLabel != null)
        {
            switch (_currentMode)
            {
                case SlotMode.Human:
                    statusLabel.text = "Ready!";
                    break;
                case SlotMode.CPU:
                    var aiDiff = GetSelectedAIDifficulty();
                    statusLabel.text = aiDiff != null ? aiDiff.displayName : "CPU";
                    break;
                case SlotMode.WaitingForPlayer:
                    statusLabel.text = "Press Start";
                    break;
                default:
                    statusLabel.text = "---";
                    break;
            }
        }

        // Show/hide panels
        if (cpuSettingsPanel != null)
        {
            cpuSettingsPanel.SetActive(_currentMode == SlotMode.CPU);
        }

        if (joinPromptPanel != null)
        {
            joinPromptPanel.SetActive(_currentMode == SlotMode.WaitingForPlayer);
        }

        // Update join prompt text
        if (joinPromptText != null && _currentMode == SlotMode.WaitingForPlayer)
        {
            joinPromptText.text = "Press Start\nto Join";
        }
    }

    #endregion

    #region Event Handlers

    private void OnCycleSlotClicked()
    {
        if (_isLocked) return;

        // Cycle through available modes
        switch (_currentMode)
        {
            case SlotMode.Empty:
                SetMode(SlotMode.CPU);
                break;
            case SlotMode.CPU:
                // Only allow Human if input device is available
                if (_inputDeviceAvailable)
                    SetMode(SlotMode.Human);
                else
                    SetMode(SlotMode.Empty);
                break;
            case SlotMode.Human:
                SetMode(SlotMode.Empty);
                break;
            case SlotMode.WaitingForPlayer:
                SetMode(SlotMode.CPU);
                break;
        }
    }

    private void OnAIDifficultyChanged(int value)
    {
        if (!_isInitialized) return;
        UpdateAIDifficultyFromDropdown();
        UpdateVisuals(); // Update status label
        NotifyConfigChanged();
    }

    private void OnGridDifficultyChanged(int value)
    {
        if (!_isInitialized) return;
        UpdateConfigFromUI();
        NotifyConfigChanged();
    }

    private void OnStartingSpeedChanged(int value)
    {
        if (!_isInitialized) return;
        _config.startingSpeed = value + 1;
        NotifyConfigChanged();
    }

    #endregion

    #region Helpers

    private void UpdateAIDifficultyFromDropdown()
    {
        if (aiDifficultyDropdown != null && 
            aiDifficultyDropdown.value >= 0 && 
            aiDifficultyDropdown.value < _aiDifficulties.Count)
        {
            _config.aiDifficulty = _aiDifficulties[aiDifficultyDropdown.value];
        }
    }

    private AIDifficultySettings GetSelectedAIDifficulty()
    {
        if (aiDifficultyDropdown != null && 
            aiDifficultyDropdown.value >= 0 && 
            aiDifficultyDropdown.value < _aiDifficulties.Count)
        {
            return _aiDifficulties[aiDifficultyDropdown.value];
        }
        return null;
    }

    private void UpdateConfigFromUI()
    {
        // Grid difficulty
        if (gridDifficultyDropdown != null && 
            gridDifficultyDropdown.value >= 0 && 
            gridDifficultyDropdown.value < _gridDifficulties.Count)
        {
            _config.gridDifficulty = _gridDifficulties[gridDifficultyDropdown.value];
        }

        // Starting speed
        if (startingSpeedDropdown != null)
        {
            _config.startingSpeed = startingSpeedDropdown.value + 1;
        }

        // AI difficulty (if CPU)
        if (_currentMode == SlotMode.CPU)
        {
            UpdateAIDifficultyFromDropdown();
        }
    }

    private void NotifyConfigChanged()
    {
        _onConfigChanged?.Invoke(_slotIndex, _config);
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (cycleSlotButton != null)
            cycleSlotButton.onClick.RemoveListener(OnCycleSlotClicked);
        if (aiDifficultyDropdown != null)
            aiDifficultyDropdown.onValueChanged.RemoveListener(OnAIDifficultyChanged);
        if (gridDifficultyDropdown != null)
            gridDifficultyDropdown.onValueChanged.RemoveListener(OnGridDifficultyChanged);
        if (startingSpeedDropdown != null)
            startingSpeedDropdown.onValueChanged.RemoveListener(OnStartingSpeedChanged);
    }

    #endregion
}