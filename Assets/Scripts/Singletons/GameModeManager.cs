using System.Collections.Generic;
using UnityEngine;
using PuzzleAttack.Grid.AI;

namespace PuzzleAttack
{
    /// <summary>
    /// Singleton manager that persists game mode configuration between scenes.
    /// Set up in the menu, read by the gameplay scene to configure the session.
    /// </summary>
    public class GameModeManager : MonoBehaviour
    {
        #region Singleton

        private static GameModeManager _instance;
        public static GameModeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Try to find existing instance
                    _instance = FindObjectOfType<GameModeManager>();
                    
                    if (_instance == null)
                    {
                        // Create new instance
                        var go = new GameObject("GameModeManager");
                        _instance = go.AddComponent<GameModeManager>();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Configuration

        [Header("Current Mode")]
        [SerializeField] private GameModeConfig _currentMode;
        
        [Header("Player Slots")]
        [SerializeField] private List<PlayerSlotConfig> _playerSlots = new List<PlayerSlotConfig>();
        
        [Header("Available Presets")]
        [SerializeField] private List<GameModeConfig> _availableModes = new List<GameModeConfig>();
        [SerializeField] private List<AIDifficultySettings> _availableAIDifficulties = new List<AIDifficultySettings>();
        [SerializeField] private List<GridDifficultySettings> _availableGridDifficulties = new List<GridDifficultySettings>();

        [Header("Session Settings")]
        [SerializeField] private int _sessionSeed = -1; // -1 = random

        #endregion

        #region Constants

        public const int MaxPlayers = 4;

        #endregion

        #region Properties

        public GameModeConfig CurrentMode => _currentMode;
        public IReadOnlyList<PlayerSlotConfig> PlayerSlots => _playerSlots;
        public int ActivePlayerCount => _playerSlots.FindAll(s => s.isActive).Count;
        public int HumanPlayerCount => _playerSlots.FindAll(s => s.IsHuman).Count;
        public int CPUPlayerCount => _playerSlots.FindAll(s => s.IsCPU).Count;
        public int SessionSeed => _sessionSeed;
        
        public IReadOnlyList<GameModeConfig> AvailableModes => _availableModes;
        public IReadOnlyList<AIDifficultySettings> AvailableAIDifficulties => _availableAIDifficulties;
        public IReadOnlyList<GridDifficultySettings> AvailableGridDifficulties => _availableGridDifficulties;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton setup
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePlayerSlots();
        }

        #endregion

        #region Initialization

        private void InitializePlayerSlots()
        {
            _playerSlots.Clear();
            for (int i = 0; i < MaxPlayers; i++)
            {
                _playerSlots.Add(PlayerSlotConfig.CreateEmptySlot(i));
            }
        }

        /// <summary>
        /// Register available presets (call from menu initialization).
        /// </summary>
        public void RegisterPresets(
            List<GameModeConfig> modes,
            List<AIDifficultySettings> aiDifficulties,
            List<GridDifficultySettings> gridDifficulties)
        {
            if (modes != null) _availableModes = modes;
            if (aiDifficulties != null) _availableAIDifficulties = aiDifficulties;
            if (gridDifficulties != null) _availableGridDifficulties = gridDifficulties;
        }

        #endregion

        #region Mode Selection

        /// <summary>
        /// Set the current game mode.
        /// </summary>
        public void SetGameMode(GameModeConfig mode)
        {
            _currentMode = mode;
            Debug.Log($"[GameModeManager] Mode set to: {mode?.displayName ?? "None"}");
            
            // Reset player slots when mode changes
            ResetPlayerSlotsForMode();
        }

        /// <summary>
        /// Reset player slots based on current mode requirements.
        /// </summary>
        private void ResetPlayerSlotsForMode()
        {
            if (_currentMode == null) return;

            // Clear all slots
            for (int i = 0; i < MaxPlayers; i++)
            {
                _playerSlots[i].Reset();
                _playerSlots[i].playerIndex = i;
            }

            // Set up default configuration based on mode
            switch (_currentMode.modeType)
            {
                case GameModeType.Marathon:
                    // Single player
                    _playerSlots[0] = PlayerSlotConfig.CreateHumanSlot(0);
                    break;

                case GameModeType.VsCPU:
                    // Player 1 = Human, Player 2 = CPU
                    _playerSlots[0] = PlayerSlotConfig.CreateHumanSlot(0);
                    _playerSlots[1] = PlayerSlotConfig.CreateCPUSlot(1, GetDefaultAIDifficulty());
                    break;

                case GameModeType.VsHuman:
                    // Player 1 & 2 = Human
                    _playerSlots[0] = PlayerSlotConfig.CreateHumanSlot(0);
                    _playerSlots[1] = PlayerSlotConfig.CreateHumanSlot(1);
                    break;

                case GameModeType.Mixed:
                    // Default to 2 players, configurable
                    _playerSlots[0] = PlayerSlotConfig.CreateHumanSlot(0);
                    _playerSlots[1] = PlayerSlotConfig.CreateEmptySlot(1);
                    break;
            }

            // Apply default grid difficulty
            var defaultGridDifficulty = GetDefaultGridDifficulty();
            foreach (var slot in _playerSlots)
            {
                if (slot.isActive)
                {
                    slot.gridDifficulty = defaultGridDifficulty;
                }
            }
        }

        #endregion

        #region Player Slot Configuration

        /// <summary>
        /// Configure a specific player slot.
        /// </summary>
        public void ConfigurePlayerSlot(int index, PlayerSlotConfig config)
        {
            if (index < 0 || index >= MaxPlayers) return;
            _playerSlots[index].CopyFrom(config);
            Debug.Log($"[GameModeManager] Slot {index} configured: {config.controllerType}, Active: {config.isActive}");
        }

        /// <summary>
        /// Set the controller type for a slot.
        /// </summary>
        public void SetSlotControllerType(int index, PlayerControllerType type)
        {
            if (index < 0 || index >= MaxPlayers) return;
            
            var slot = _playerSlots[index];
            slot.controllerType = type;
            slot.isActive = type != PlayerControllerType.None;
            
            // Set appropriate defaults
            if (type == PlayerControllerType.Human)
            {
                slot.playerName = $"Player {index + 1}";
                slot.inputDeviceIndex = index;
            }
            else if (type == PlayerControllerType.CPU)
            {
                slot.playerName = $"CPU {index + 1}";
                slot.aiDifficulty = GetDefaultAIDifficulty();
            }
            else
            {
                slot.playerName = "Empty";
            }

            Debug.Log($"[GameModeManager] Slot {index} type set to: {type}");
        }

        /// <summary>
        /// Set AI difficulty for a slot.
        /// </summary>
        public void SetSlotAIDifficulty(int index, AIDifficultySettings difficulty)
        {
            if (index < 0 || index >= MaxPlayers) return;
            _playerSlots[index].aiDifficulty = difficulty;
            Debug.Log($"[GameModeManager] Slot {index} AI difficulty set to: {difficulty?.displayName ?? "None"}");
        }

        /// <summary>
        /// Set grid difficulty for a slot.
        /// </summary>
        public void SetSlotGridDifficulty(int index, GridDifficultySettings difficulty)
        {
            if (index < 0 || index >= MaxPlayers) return;
            _playerSlots[index].gridDifficulty = difficulty;
            Debug.Log($"[GameModeManager] Slot {index} grid difficulty set to: {difficulty?.displayName ?? "None"}");
        }

        /// <summary>
        /// Set starting speed for a slot.
        /// </summary>
        public void SetSlotStartingSpeed(int index, int speed)
        {
            if (index < 0 || index >= MaxPlayers) return;
            _playerSlots[index].startingSpeed = Mathf.Clamp(speed, 1, 50);
            Debug.Log($"[GameModeManager] Slot {index} starting speed set to: {speed}");
        }

        /// <summary>
        /// Set the number of active players (for VS modes).
        /// </summary>
        public void SetPlayerCount(int count)
        {
            count = Mathf.Clamp(count, _currentMode?.minPlayers ?? 1, _currentMode?.maxPlayers ?? MaxPlayers);
            
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (i < count)
                {
                    if (!_playerSlots[i].isActive)
                    {
                        // Activate slot with appropriate default
                        if (_currentMode?.modeType == GameModeType.VsCPU && i > 0)
                        {
                            _playerSlots[i] = PlayerSlotConfig.CreateCPUSlot(i, GetDefaultAIDifficulty());
                        }
                        else
                        {
                            _playerSlots[i] = PlayerSlotConfig.CreateHumanSlot(i);
                        }
                        _playerSlots[i].gridDifficulty = GetDefaultGridDifficulty();
                    }
                }
                else
                {
                    _playerSlots[i].Reset();
                    _playerSlots[i].playerIndex = i;
                }
            }

            Debug.Log($"[GameModeManager] Player count set to: {count}");
        }

        /// <summary>
        /// Get a specific player slot.
        /// </summary>
        public PlayerSlotConfig GetPlayerSlot(int index)
        {
            if (index < 0 || index >= MaxPlayers) return null;
            return _playerSlots[index];
        }

        /// <summary>
        /// Get all active player slots.
        /// </summary>
        public List<PlayerSlotConfig> GetActiveSlots()
        {
            return _playerSlots.FindAll(s => s.isActive);
        }

        #endregion

        #region Quick Setup Methods

        /// <summary>
        /// Quick setup for Marathon mode.
        /// </summary>
        public void SetupMarathon(GridDifficultySettings gridDifficulty = null, int startingSpeed = 1)
        {
            // Find or use provided marathon mode config
            var marathonMode = _availableModes.Find(m => m.modeType == GameModeType.Marathon);
            SetGameMode(marathonMode);
            
            _playerSlots[0].gridDifficulty = gridDifficulty ?? GetDefaultGridDifficulty();
            _playerSlots[0].startingSpeed = startingSpeed;
            
            Debug.Log($"[GameModeManager] Marathon setup complete - Difficulty: {_playerSlots[0].gridDifficulty?.displayName}, Speed: {startingSpeed}");
        }

        /// <summary>
        /// Quick setup for VS CPU mode.
        /// </summary>
        public void SetupVsCPU(
            AIDifficultySettings aiDifficulty = null,
            GridDifficultySettings playerGridDifficulty = null,
            GridDifficultySettings cpuGridDifficulty = null,
            int playerStartingSpeed = 1,
            int cpuStartingSpeed = 1)
        {
            var vsCpuMode = _availableModes.Find(m => m.modeType == GameModeType.VsCPU);
            SetGameMode(vsCpuMode);

            // Player 1 (Human)
            _playerSlots[0].gridDifficulty = playerGridDifficulty ?? GetDefaultGridDifficulty();
            _playerSlots[0].startingSpeed = playerStartingSpeed;

            // Player 2 (CPU)
            _playerSlots[1].aiDifficulty = aiDifficulty ?? GetDefaultAIDifficulty();
            _playerSlots[1].gridDifficulty = cpuGridDifficulty ?? GetDefaultGridDifficulty();
            _playerSlots[1].startingSpeed = cpuStartingSpeed;

            Debug.Log($"[GameModeManager] VS CPU setup complete - AI: {_playerSlots[1].aiDifficulty?.displayName}");
        }

        /// <summary>
        /// Quick setup for VS Human mode.
        /// </summary>
        public void SetupVsHuman(
            int playerCount = 2,
            GridDifficultySettings sharedGridDifficulty = null,
            int sharedStartingSpeed = 1)
        {
            var vsHumanMode = _availableModes.Find(m => m.modeType == GameModeType.VsHuman);
            SetGameMode(vsHumanMode);
            SetPlayerCount(playerCount);

            var gridDifficulty = sharedGridDifficulty ?? GetDefaultGridDifficulty();
            foreach (var slot in _playerSlots)
            {
                if (slot.isActive)
                {
                    slot.gridDifficulty = gridDifficulty;
                    slot.startingSpeed = sharedStartingSpeed;
                }
            }

            Debug.Log($"[GameModeManager] VS Human setup complete - {playerCount} players");
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Set a specific seed for the session.
        /// </summary>
        public void SetSessionSeed(int seed)
        {
            _sessionSeed = seed;
            Debug.Log($"[GameModeManager] Session seed set to: {seed}");
        }

        /// <summary>
        /// Use a random seed for the session.
        /// </summary>
        public void UseRandomSeed()
        {
            _sessionSeed = -1;
        }

        /// <summary>
        /// Start the game with current configuration.
        /// </summary>
        public void StartGame()
        {
            if (_currentMode == null)
            {
                Debug.LogError("[GameModeManager] Cannot start game - no mode selected!");
                return;
            }

            // Validate configuration
            if (!ValidateConfiguration())
            {
                Debug.LogError("[GameModeManager] Configuration validation failed!");
                return;
            }

            Debug.Log($"[GameModeManager] Starting game - Mode: {_currentMode.displayName}, Players: {ActivePlayerCount}");
            
            // Load the target scene
            UnityEngine.SceneManagement.SceneManager.LoadScene(_currentMode.targetScene);
        }

        /// <summary>
        /// Validate current configuration before starting.
        /// </summary>
        public bool ValidateConfiguration()
        {
            if (_currentMode == null)
            {
                Debug.LogWarning("[GameModeManager] No mode selected");
                return false;
            }

            int activeCount = ActivePlayerCount;
            if (!_currentMode.SupportsPlayerCount(activeCount))
            {
                Debug.LogWarning($"[GameModeManager] Invalid player count: {activeCount} (mode requires {_currentMode.minPlayers}-{_currentMode.maxPlayers})");
                return false;
            }

            // Validate CPU slots have AI difficulty assigned
            foreach (var slot in _playerSlots)
            {
                if (slot.IsCPU && slot.aiDifficulty == null)
                {
                    Debug.LogWarning($"[GameModeManager] CPU slot {slot.playerIndex} has no AI difficulty assigned");
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Helpers

        private AIDifficultySettings GetDefaultAIDifficulty()
        {
            if (_availableAIDifficulties.Count > 0)
            {
                // Try to find "Normal" or "Medium" difficulty
                var normal = _availableAIDifficulties.Find(d => 
                    d.displayName.ToLower().Contains("normal") || 
                    d.displayName.ToLower().Contains("medium"));
                return normal ?? _availableAIDifficulties[0];
            }
            return null;
        }

        private GridDifficultySettings GetDefaultGridDifficulty()
        {
            if (_availableGridDifficulties.Count > 0)
            {
                var normal = _availableGridDifficulties.Find(d => 
                    d.displayName.ToLower().Contains("normal"));
                return normal ?? _availableGridDifficulties[0];
            }
            return null;
        }

        #endregion

        #region Debug

        /// <summary>
        /// Log current configuration for debugging.
        /// </summary>
        public void LogConfiguration()
        {
            Debug.Log("=== GameModeManager Configuration ===");
            Debug.Log($"Mode: {_currentMode?.displayName ?? "None"}");
            Debug.Log($"Active Players: {ActivePlayerCount}");
            Debug.Log($"Session Seed: {_sessionSeed}");
            
            for (int i = 0; i < MaxPlayers; i++)
            {
                var slot = _playerSlots[i];
                if (slot.isActive)
                {
                    Debug.Log($"  Slot {i}: {slot.playerName} ({slot.controllerType})");
                    Debug.Log($"    - Grid Difficulty: {slot.gridDifficulty?.displayName ?? "None"}");
                    Debug.Log($"    - Starting Speed: {slot.startingSpeed}");
                    if (slot.IsCPU)
                    {
                        Debug.Log($"    - AI Difficulty: {slot.aiDifficulty?.displayName ?? "None"}");
                    }
                }
            }
            Debug.Log("=====================================");
        }

        #endregion
    }
}