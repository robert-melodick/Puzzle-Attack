using UnityEngine;
using PuzzleAttack.Grid.AI;

namespace PuzzleAttack
{
    /// <summary>
    /// Represents the type of controller for a player slot.
    /// </summary>
    public enum PlayerControllerType
    {
        None,       // Slot not in use
        Human,      // Human player (keyboard/gamepad)
        CPU         // AI controlled
    }

    /// <summary>
    /// Configuration for a single player slot.
    /// Used during game setup to configure each participant.
    /// </summary>
    [System.Serializable]
    public class PlayerSlotConfig
    {
        [Header("Slot Settings")]
        [Tooltip("Whether this slot is active")]
        public bool isActive = false;
        
        [Tooltip("Controller type for this slot")]
        public PlayerControllerType controllerType = PlayerControllerType.None;
        
        [Tooltip("Player index (0-3)")]
        public int playerIndex = 0;

        [Header("Input (Human Players)")]
        [Tooltip("Input device/scheme for human players")]
        public int inputDeviceIndex = 0; // 0 = keyboard, 1-4 = gamepads
        
        [Header("AI Settings (CPU Players)")]
        [Tooltip("AI difficulty settings (for CPU players)")]
        public AIDifficultySettings aiDifficulty;
        
        [Header("Gameplay Settings")]
        [Tooltip("Grid difficulty settings")]
        public GridDifficultySettings gridDifficulty;
        
        [Tooltip("Starting speed level")]
        [Range(1, 50)]
        public int startingSpeed = 1;

        [Header("Display")]
        [Tooltip("Display name for this player")]
        public string playerName = "Player 1";
        
        [Tooltip("Character/theme selection (for future use)")]
        public int characterIndex = 0;

        /// <summary>
        /// Create a default human player slot.
        /// </summary>
        public static PlayerSlotConfig CreateHumanSlot(int index)
        {
            return new PlayerSlotConfig
            {
                isActive = true,
                controllerType = PlayerControllerType.Human,
                playerIndex = index,
                inputDeviceIndex = index,
                playerName = $"Player {index + 1}",
                startingSpeed = 1
            };
        }

        /// <summary>
        /// Create a default CPU player slot.
        /// </summary>
        public static PlayerSlotConfig CreateCPUSlot(int index, AIDifficultySettings difficulty = null)
        {
            return new PlayerSlotConfig
            {
                isActive = true,
                controllerType = PlayerControllerType.CPU,
                playerIndex = index,
                aiDifficulty = difficulty,
                playerName = $"CPU {index + 1}",
                startingSpeed = 1
            };
        }

        /// <summary>
        /// Create an empty/inactive slot.
        /// </summary>
        public static PlayerSlotConfig CreateEmptySlot(int index)
        {
            return new PlayerSlotConfig
            {
                isActive = false,
                controllerType = PlayerControllerType.None,
                playerIndex = index,
                playerName = $"Empty",
                startingSpeed = 1
            };
        }

        /// <summary>
        /// Copy settings from another slot config.
        /// </summary>
        public void CopyFrom(PlayerSlotConfig other)
        {
            isActive = other.isActive;
            controllerType = other.controllerType;
            playerIndex = other.playerIndex;
            inputDeviceIndex = other.inputDeviceIndex;
            aiDifficulty = other.aiDifficulty;
            gridDifficulty = other.gridDifficulty;
            startingSpeed = other.startingSpeed;
            playerName = other.playerName;
            characterIndex = other.characterIndex;
        }

        /// <summary>
        /// Reset slot to default inactive state.
        /// </summary>
        public void Reset()
        {
            isActive = false;
            controllerType = PlayerControllerType.None;
            aiDifficulty = null;
            gridDifficulty = null;
            startingSpeed = 1;
            playerName = "Empty";
            characterIndex = 0;
        }

        /// <summary>
        /// Check if this is a human-controlled slot.
        /// </summary>
        public bool IsHuman => isActive && controllerType == PlayerControllerType.Human;

        /// <summary>
        /// Check if this is a CPU-controlled slot.
        /// </summary>
        public bool IsCPU => isActive && controllerType == PlayerControllerType.CPU;
    }
}