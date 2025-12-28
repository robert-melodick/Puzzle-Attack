using UnityEngine;

namespace PuzzleAttack.Grid.AI
{
    /// <summary>
    /// Difficulty settings for AI opponents.
    /// Create different presets (Easy, Medium, Hard, etc.) as separate assets.
    /// </summary>
    [CreateAssetMenu(fileName = "AIDifficulty", menuName = "Puzzle Attack/AI Difficulty Settings")]
    public class AIDifficultySettings : ScriptableObject
    {
        [Header("Preset Info")]
        [Tooltip("Display name for this difficulty")]
        public string displayName = "Normal";
        
        [Tooltip("Description shown in UI")]
        [TextArea(2, 4)]
        public string description = "A balanced AI opponent.";

        [Header("Execution Speed")]
        [Tooltip("How many cursor moves or swaps the AI can perform per second")]
        [Range(1f, 20f)] 
        public float inputsPerSecond = 4f;

        [Tooltip("Delay before AI reacts to new board states (simulates thinking time)")]
        [Range(0f, 1f)] 
        public float reactionDelaySeconds = 0.15f;

        [Tooltip("Extra delay after completing a swap before thinking again")]
        [Range(0f, 0.5f)] 
        public float postSwapCooldown = 0.1f;

        [Header("Intelligence")]
        [Tooltip("1 = immediate matches only, 2+ = considers chain potential")]
        [Range(1, 4)] 
        public int chainLookaheadDepth = 1;

        [Tooltip("0 = greedy (always take immediate matches), 1 = patient (prefer setting up chains)")]
        [Range(0f, 1f)] 
        public float setupVsGreedBias = 0.3f;

        [Tooltip("How much to prioritize clearing garbage blocks (1 = normal, 2+ = high priority)")]
        [Range(0f, 3f)] 
        public float garbageClearingWeight = 1.5f;

        [Tooltip("How much to avoid swaps near the danger zone")]
        [Range(0f, 2f)] 
        public float safetyWeight = 1f;

        [Tooltip("Minimum score threshold - AI won't make swaps below this value")]
        [Range(0f, 20f)]
        public float minimumSwapScore = 5f;

        [Header("Humanization")]
        [Tooltip("Chance to pick a suboptimal move instead of the best one")]
        [Range(0f, 0.5f)] 
        public float suboptimalMoveChance = 0.1f;

        [Tooltip("Chance to hesitate briefly before starting execution")]
        [Range(0f, 0.5f)] 
        public float hesitationChance = 0.05f;

        [Tooltip("Maximum hesitation duration in seconds")]
        [Range(0f, 0.5f)]
        public float maxHesitationDuration = 0.3f;

        [Tooltip("Chance to 'miss' an obvious match (looks more human)")]
        [Range(0f, 0.3f)]
        public float missObviousMatchChance = 0f;

        [Header("Panic Behavior")]
        [Tooltip("Danger intensity threshold where AI enters panic mode (0-1)")]
        [Range(0.3f, 0.95f)] 
        public float panicThreshold = 0.6f;

        [Tooltip("Input speed multiplier when panicking")]
        [Range(1f, 2.5f)] 
        public float panicSpeedMultiplier = 1.5f;

        [Tooltip("Mistake chance multiplier when panicking (panic makes AI sloppier)")]
        [Range(1f, 3f)] 
        public float panicMistakeMultiplier = 2f;

        [Tooltip("When panicking, reduce lookahead to focus on immediate survival")]
        public bool reduceLookaheadWhenPanicking = true;

        [Header("Aggression (VS Mode)")]
        [Tooltip("How aggressively to build chains to send garbage (0 = defensive, 1 = aggressive)")]
        [Range(0f, 1f)]
        public float aggressionBias = 0.5f;

        [Tooltip("Minimum chain length before AI considers it 'worth' building")]
        [Range(2, 6)]
        public int minimumChainGoal = 2;

        [Header("Fast Rise Behavior")]
        [Tooltip("Whether the AI will use fast rise at all")]
        public bool canFastRise = true;

        [Tooltip("Stack height (as fraction of grid) below which AI considers fast rising to get more tiles")]
        [Range(0.1f, 0.5f)]
        public float fastRiseStackThreshold = 0.3f;

        [Tooltip("Chance per second to fast rise when stack is low and safe")]
        [Range(0f, 1f)]
        public float fastRiseChance = 0.3f;

        [Tooltip("Maximum duration of a fast rise burst in seconds")]
        [Range(0.1f, 2f)]
        public float maxFastRiseDuration = 0.5f;

        [Tooltip("Minimum time between fast rise attempts")]
        [Range(0.5f, 5f)]
        public float fastRiseCooldown = 2f;

        // Computed properties
        public float SecondsPerInput => 1f / inputsPerSecond;

        /// <summary>
        /// Get effective inputs per second accounting for panic state.
        /// </summary>
        public float GetEffectiveInputRate(bool isPanicking)
        {
            return isPanicking ? inputsPerSecond * panicSpeedMultiplier : inputsPerSecond;
        }

        /// <summary>
        /// Get effective mistake chance accounting for panic state.
        /// </summary>
        public float GetEffectiveMistakeChance(bool isPanicking)
        {
            return isPanicking 
                ? Mathf.Min(suboptimalMoveChance * panicMistakeMultiplier, 0.8f) 
                : suboptimalMoveChance;
        }

        /// <summary>
        /// Get effective lookahead depth accounting for panic state.
        /// </summary>
        public int GetEffectiveLookahead(bool isPanicking)
        {
            if (isPanicking && reduceLookaheadWhenPanicking)
                return 1;
            return chainLookaheadDepth;
        }

        #region Preset Factory Methods

        /// <summary>
        /// Create default Easy settings (for runtime creation).
        /// </summary>
        public static AIDifficultySettings CreateEasy()
        {
            var settings = CreateInstance<AIDifficultySettings>();
            settings.displayName = "Easy";
            settings.description = "A beginner-friendly opponent that makes frequent mistakes.";
            settings.inputsPerSecond = 2f;
            settings.reactionDelaySeconds = 0.4f;
            settings.postSwapCooldown = 0.2f;
            settings.chainLookaheadDepth = 1;
            settings.setupVsGreedBias = 0.1f;
            settings.garbageClearingWeight = 1f;
            settings.safetyWeight = 0.5f;
            settings.minimumSwapScore = 3f;
            settings.suboptimalMoveChance = 0.35f;
            settings.hesitationChance = 0.2f;
            settings.maxHesitationDuration = 0.4f;
            settings.missObviousMatchChance = 0.15f;
            settings.panicThreshold = 0.7f;
            settings.panicSpeedMultiplier = 1.3f;
            settings.panicMistakeMultiplier = 2.5f;
            settings.aggressionBias = 0.2f;
            settings.minimumChainGoal = 2;
            settings.canFastRise = false; // Easy AI never fast rises
            settings.fastRiseStackThreshold = 0.2f;
            settings.fastRiseChance = 0f;
            settings.maxFastRiseDuration = 0f;
            settings.fastRiseCooldown = 10f;
            return settings;
        }

        /// <summary>
        /// Create default Medium settings (for runtime creation).
        /// </summary>
        public static AIDifficultySettings CreateMedium()
        {
            var settings = CreateInstance<AIDifficultySettings>();
            settings.displayName = "Medium";
            settings.description = "A balanced opponent suitable for most players.";
            settings.inputsPerSecond = 4f;
            settings.reactionDelaySeconds = 0.2f;
            settings.postSwapCooldown = 0.1f;
            settings.chainLookaheadDepth = 2;
            settings.setupVsGreedBias = 0.4f;
            settings.garbageClearingWeight = 1.5f;
            settings.safetyWeight = 1f;
            settings.minimumSwapScore = 5f;
            settings.suboptimalMoveChance = 0.15f;
            settings.hesitationChance = 0.1f;
            settings.maxHesitationDuration = 0.25f;
            settings.missObviousMatchChance = 0.05f;
            settings.panicThreshold = 0.6f;
            settings.panicSpeedMultiplier = 1.5f;
            settings.panicMistakeMultiplier = 2f;
            settings.aggressionBias = 0.5f;
            settings.minimumChainGoal = 3;
            settings.canFastRise = true;
            settings.fastRiseStackThreshold = 0.25f;
            settings.fastRiseChance = 0.15f;
            settings.maxFastRiseDuration = 0.3f;
            settings.fastRiseCooldown = 3f;
            return settings;
        }

        /// <summary>
        /// Create default Hard settings (for runtime creation).
        /// </summary>
        public static AIDifficultySettings CreateHard()
        {
            var settings = CreateInstance<AIDifficultySettings>();
            settings.displayName = "Hard";
            settings.description = "A challenging opponent that plans ahead and punishes mistakes.";
            settings.inputsPerSecond = 6f;
            settings.reactionDelaySeconds = 0.1f;
            settings.postSwapCooldown = 0.05f;
            settings.chainLookaheadDepth = 3;
            settings.setupVsGreedBias = 0.6f;
            settings.garbageClearingWeight = 2f;
            settings.safetyWeight = 1.5f;
            settings.minimumSwapScore = 8f;
            settings.suboptimalMoveChance = 0.05f;
            settings.hesitationChance = 0.02f;
            settings.maxHesitationDuration = 0.1f;
            settings.missObviousMatchChance = 0f;
            settings.panicThreshold = 0.5f;
            settings.panicSpeedMultiplier = 1.8f;
            settings.panicMistakeMultiplier = 1.5f;
            settings.aggressionBias = 0.7f;
            settings.minimumChainGoal = 4;
            settings.canFastRise = true;
            settings.fastRiseStackThreshold = 0.3f;
            settings.fastRiseChance = 0.3f;
            settings.maxFastRiseDuration = 0.5f;
            settings.fastRiseCooldown = 2f;
            return settings;
        }

        /// <summary>
        /// Create default Expert settings (for runtime creation).
        /// </summary>
        public static AIDifficultySettings CreateExpert()
        {
            var settings = CreateInstance<AIDifficultySettings>();
            settings.displayName = "Expert";
            settings.description = "A ruthless opponent with near-perfect play. Good luck.";
            settings.inputsPerSecond = 10f;
            settings.reactionDelaySeconds = 0.05f;
            settings.postSwapCooldown = 0.02f;
            settings.chainLookaheadDepth = 4;
            settings.setupVsGreedBias = 0.8f;
            settings.garbageClearingWeight = 2.5f;
            settings.safetyWeight = 2f;
            settings.minimumSwapScore = 10f;
            settings.suboptimalMoveChance = 0.01f;
            settings.hesitationChance = 0f;
            settings.maxHesitationDuration = 0f;
            settings.missObviousMatchChance = 0f;
            settings.panicThreshold = 0.4f;
            settings.panicSpeedMultiplier = 2f;
            settings.panicMistakeMultiplier = 1.2f;
            settings.reduceLookaheadWhenPanicking = false;
            settings.aggressionBias = 0.9f;
            settings.minimumChainGoal = 5;
            settings.canFastRise = true;
            settings.fastRiseStackThreshold = 0.35f;
            settings.fastRiseChance = 0.5f;
            settings.maxFastRiseDuration = 0.8f;
            settings.fastRiseCooldown = 1.5f;
            return settings;
        }

        #endregion
    }
}