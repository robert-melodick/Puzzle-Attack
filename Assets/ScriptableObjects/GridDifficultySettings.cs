using UnityEngine;

namespace PuzzleAttack
{
    /// <summary>
    /// Difficulty settings that affect core gameplay mechanics.
    /// Similar to difficulty settings in Panel de Pon / Pokemon Puzzle Challenge.
    /// These affect the grid behavior, not AI behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "GridDifficulty", menuName = "Puzzle Attack/Grid Difficulty Settings")]
    public class GridDifficultySettings : ScriptableObject
    {
        [Header("Preset Info")]
        [Tooltip("Display name for this difficulty")]
        public string displayName = "Normal";
        
        [Tooltip("Description shown in UI")]
        [TextArea(2, 4)]
        public string description = "Standard gameplay experience.";

        [Header("Rise Speed")]
        [Tooltip("Base rise speed multiplier (1 = normal)")]
        [Range(0.5f, 2f)]
        public float baseRiseSpeedMultiplier = 1f;
        
        [Tooltip("How much rise speed increases per level")]
        [Range(0f, 0.2f)]
        public float speedIncreasePerLevel = 0.05f;
        
        [Tooltip("Maximum rise speed multiplier")]
        [Range(1f, 5f)]
        public float maxRiseSpeedMultiplier = 3f;

        [Header("Grace Periods")]
        [Tooltip("Base grace period after matches (seconds)")]
        [Range(0f, 5f)]
        public float baseGracePeriod = 1.5f;
        
        [Tooltip("Grace period reduction per level (seconds)")]
        [Range(0f, 0.2f)]
        public float gracePeriodReductionPerLevel = 0.02f;
        
        [Tooltip("Minimum grace period (seconds)")]
        [Range(0f, 1f)]
        public float minimumGracePeriod = 0.3f;

        [Header("Danger Zone")]
        [Tooltip("Number of rows before top that triggers danger state")]
        [Range(1, 5)]
        public int dangerZoneRows = 2;
        
        [Tooltip("Additional grace period when in danger zone")]
        [Range(0f, 2f)]
        public float dangerZoneGraceBonus = 0.5f;

        [Header("Tile Generation")]
        [Tooltip("Number of tile colors/types to use")]
        [Range(4, 8)]
        public int tileColorCount = 6;
        
        [Tooltip("Chance of 'hard' patterns that are difficult to clear")]
        [Range(0f, 0.5f)]
        public float hardPatternChance = 0.1f;

        [Header("Garbage (VS Mode)")]
        [Tooltip("Delay before received garbage drops (seconds)")]
        [Range(0f, 5f)]
        public float garbageDropDelay = 1f;
        
        [Tooltip("Maximum garbage blocks that can be pending")]
        [Range(1, 20)]
        public int maxPendingGarbage = 10;

        [Header("Combo System")]
        [Tooltip("Time window for chain continuation (seconds)")]
        [Range(0.5f, 3f)]
        public float chainTimeWindow = 1.5f;
        
        [Tooltip("Bonus points multiplier for combos")]
        [Range(1f, 3f)]
        public float comboScoreMultiplier = 1.5f;

        [Header("Level Progression")]
        [Tooltip("Points needed to increase level")]
        public int pointsPerLevel = 1000;
        
        [Tooltip("Tiles cleared needed to increase level (alternative to points)")]
        public int tilesPerLevel = 50;
        
        [Tooltip("Use tiles cleared instead of points for leveling")]
        public bool useTilesForLeveling = false;

        #region Computed Properties

        /// <summary>
        /// Get effective rise speed for a given level.
        /// </summary>
        public float GetRiseSpeedMultiplier(int level)
        {
            float multiplier = baseRiseSpeedMultiplier + (level - 1) * speedIncreasePerLevel;
            return Mathf.Min(multiplier, maxRiseSpeedMultiplier);
        }

        /// <summary>
        /// Get effective grace period for a given level.
        /// </summary>
        public float GetGracePeriod(int level, bool inDangerZone = false)
        {
            float grace = baseGracePeriod - (level - 1) * gracePeriodReductionPerLevel;
            grace = Mathf.Max(grace, minimumGracePeriod);
            
            if (inDangerZone)
            {
                grace += dangerZoneGraceBonus;
            }
            
            return grace;
        }

        #endregion

        #region Preset Factory Methods

        /// <summary>
        /// Create Easy difficulty preset.
        /// </summary>
        public static GridDifficultySettings CreateEasy()
        {
            var settings = CreateInstance<GridDifficultySettings>();
            settings.displayName = "Easy";
            settings.description = "Relaxed pace for beginners. Slower rise speed and longer grace periods.";
            settings.baseRiseSpeedMultiplier = 0.7f;
            settings.speedIncreasePerLevel = 0.03f;
            settings.maxRiseSpeedMultiplier = 2f;
            settings.baseGracePeriod = 2f;
            settings.gracePeriodReductionPerLevel = 0.01f;
            settings.minimumGracePeriod = 0.5f;
            settings.dangerZoneRows = 3;
            settings.dangerZoneGraceBonus = 1f;
            settings.tileColorCount = 5;
            settings.hardPatternChance = 0f;
            settings.garbageDropDelay = 2f;
            settings.chainTimeWindow = 2f;
            settings.comboScoreMultiplier = 1.25f;
            settings.pointsPerLevel = 1500;
            settings.tilesPerLevel = 75;
            return settings;
        }

        /// <summary>
        /// Create Normal difficulty preset.
        /// </summary>
        public static GridDifficultySettings CreateNormal()
        {
            var settings = CreateInstance<GridDifficultySettings>();
            settings.displayName = "Normal";
            settings.description = "Standard gameplay experience. Balanced for most players.";
            settings.baseRiseSpeedMultiplier = 1f;
            settings.speedIncreasePerLevel = 0.05f;
            settings.maxRiseSpeedMultiplier = 3f;
            settings.baseGracePeriod = 1.5f;
            settings.gracePeriodReductionPerLevel = 0.02f;
            settings.minimumGracePeriod = 0.3f;
            settings.dangerZoneRows = 2;
            settings.dangerZoneGraceBonus = 0.5f;
            settings.tileColorCount = 6;
            settings.hardPatternChance = 0.1f;
            settings.garbageDropDelay = 1f;
            settings.chainTimeWindow = 1.5f;
            settings.comboScoreMultiplier = 1.5f;
            settings.pointsPerLevel = 1000;
            settings.tilesPerLevel = 50;
            return settings;
        }

        /// <summary>
        /// Create Hard difficulty preset.
        /// </summary>
        public static GridDifficultySettings CreateHard()
        {
            var settings = CreateInstance<GridDifficultySettings>();
            settings.displayName = "Hard";
            settings.description = "Fast-paced challenge. Quick thinking required!";
            settings.baseRiseSpeedMultiplier = 1.3f;
            settings.speedIncreasePerLevel = 0.07f;
            settings.maxRiseSpeedMultiplier = 4f;
            settings.baseGracePeriod = 1f;
            settings.gracePeriodReductionPerLevel = 0.03f;
            settings.minimumGracePeriod = 0.2f;
            settings.dangerZoneRows = 2;
            settings.dangerZoneGraceBonus = 0.3f;
            settings.tileColorCount = 6;
            settings.hardPatternChance = 0.2f;
            settings.garbageDropDelay = 0.75f;
            settings.chainTimeWindow = 1.25f;
            settings.comboScoreMultiplier = 1.75f;
            settings.pointsPerLevel = 800;
            settings.tilesPerLevel = 40;
            return settings;
        }

        /// <summary>
        /// Create Very Hard difficulty preset.
        /// </summary>
        public static GridDifficultySettings CreateVeryHard()
        {
            var settings = CreateInstance<GridDifficultySettings>();
            settings.displayName = "Very Hard";
            settings.description = "Intense action for experts. No mercy!";
            settings.baseRiseSpeedMultiplier = 1.5f;
            settings.speedIncreasePerLevel = 0.08f;
            settings.maxRiseSpeedMultiplier = 5f;
            settings.baseGracePeriod = 0.75f;
            settings.gracePeriodReductionPerLevel = 0.04f;
            settings.minimumGracePeriod = 0.15f;
            settings.dangerZoneRows = 1;
            settings.dangerZoneGraceBonus = 0.2f;
            settings.tileColorCount = 7;
            settings.hardPatternChance = 0.3f;
            settings.garbageDropDelay = 0.5f;
            settings.chainTimeWindow = 1f;
            settings.comboScoreMultiplier = 2f;
            settings.pointsPerLevel = 600;
            settings.tilesPerLevel = 30;
            return settings;
        }

        /// <summary>
        /// Create Super Hard difficulty preset.
        /// </summary>
        public static GridDifficultySettings CreateSuperHard()
        {
            var settings = CreateInstance<GridDifficultySettings>();
            settings.displayName = "S-Hard";
            settings.description = "Maximum difficulty. Only for the truly skilled!";
            settings.baseRiseSpeedMultiplier = 1.75f;
            settings.speedIncreasePerLevel = 0.1f;
            settings.maxRiseSpeedMultiplier = 6f;
            settings.baseGracePeriod = 0.5f;
            settings.gracePeriodReductionPerLevel = 0.05f;
            settings.minimumGracePeriod = 0.1f;
            settings.dangerZoneRows = 1;
            settings.dangerZoneGraceBonus = 0.1f;
            settings.tileColorCount = 8;
            settings.hardPatternChance = 0.4f;
            settings.garbageDropDelay = 0.25f;
            settings.chainTimeWindow = 0.75f;
            settings.comboScoreMultiplier = 2.5f;
            settings.pointsPerLevel = 500;
            settings.tilesPerLevel = 25;
            return settings;
        }

        #endregion
    }
}