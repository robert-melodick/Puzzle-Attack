using UnityEngine;

namespace PuzzleAttack
{
    /// <summary>
    /// Defines the type of game mode.
    /// </summary>
    public enum GameModeType
    {
        Marathon,       // Single player endless
        VsCPU,          // Player vs AI opponent(s)
        VsHuman,        // Local multiplayer
        Mixed           // Mix of human and AI players
    }

    /// <summary>
    /// Configuration for a game mode. Used by menus and GameModeManager
    /// to set up the appropriate game session.
    /// </summary>
    [CreateAssetMenu(fileName = "GameMode", menuName = "Puzzle Attack/Game Mode Config")]
    public class GameModeConfig : ScriptableObject
    {
        [Header("Mode Identity")]
        [Tooltip("Display name shown in menus")]
        public string displayName = "Marathon";
        
        [Tooltip("Description shown in UI")]
        [TextArea(2, 4)]
        public string description = "Classic endless mode. Survive as long as you can!";
        
        [Tooltip("Icon for this mode (optional)")]
        public Sprite icon;

        [Header("Mode Type")]
        public GameModeType modeType = GameModeType.Marathon;

        [Header("Player Configuration")]
        [Tooltip("Minimum number of players/grids")]
        [Range(1, 4)]
        public int minPlayers = 1;
        
        [Tooltip("Maximum number of players/grids")]
        [Range(1, 4)]
        public int maxPlayers = 1;
        
        [Tooltip("Default number of players")]
        [Range(1, 4)]
        public int defaultPlayers = 1;

        [Header("AI Configuration")]
        [Tooltip("Whether AI opponents are allowed in this mode")]
        public bool allowAI = false;
        
        [Tooltip("Whether AI difficulty selection is available")]
        public bool allowDifficultySelection = false;

        [Header("Gameplay Options")]
        [Tooltip("Whether players can select starting speed")]
        public bool allowSpeedSelection = true;
        
        [Tooltip("Whether players can select grid difficulty")]
        public bool allowGridDifficultySelection = true;
        
        [Tooltip("Default starting speed level")]
        [Range(1, 50)]
        public int defaultStartingSpeed = 1;

        [Header("Win Conditions")]
        [Tooltip("In VS modes, does losing one grid end the match?")]
        public bool eliminationMode = true;
        
        [Tooltip("Time limit in seconds (0 = no limit)")]
        public float timeLimit = 0f;
        
        [Tooltip("Score target to win (0 = no target)")]
        public int scoreTarget = 0;

        [Header("Garbage Settings (VS Modes)")]
        [Tooltip("Whether garbage is sent between players")]
        public bool enableGarbageSending = false;
        
        [Tooltip("Multiplier for garbage sent (1 = normal)")]
        [Range(0.5f, 3f)]
        public float garbageMultiplier = 1f;

        [Header("Scene Configuration")]
        [Tooltip("Scene to load for this game mode")]
        public string targetScene = "gameplay_scene";

        /// <summary>
        /// Check if this mode supports the given player count.
        /// </summary>
        public bool SupportsPlayerCount(int count)
        {
            return count >= minPlayers && count <= maxPlayers;
        }

        /// <summary>
        /// Check if this is a multiplayer mode.
        /// </summary>
        public bool IsMultiplayer => maxPlayers > 1;

        /// <summary>
        /// Check if this mode requires AI opponents.
        /// </summary>
        public bool RequiresAI => modeType == GameModeType.VsCPU;
    }
}