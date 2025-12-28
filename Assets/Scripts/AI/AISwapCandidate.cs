using UnityEngine;

namespace PuzzleAttack.Grid.AI
{
    /// <summary>
    /// Represents a potential swap the AI could make, with scoring information.
    /// </summary>
    public struct AISwapCandidate
    {
        /// <summary>Position of the LEFT tile of the swap (cursor position).</summary>
        public Vector2Int Position;

        /// <summary>Overall desirability score (higher = better).</summary>
        public float Score;

        /// <summary>How many tiles would be matched immediately.</summary>
        public int ImmediateMatchCount;

        /// <summary>Predicted chain depth if this swap triggers cascades.</summary>
        public int EstimatedChainLength;

        /// <summary>Whether this swap would help clear adjacent garbage.</summary>
        public bool ClearsGarbage;

        /// <summary>Whether this swap involves tiles in the danger zone.</summary>
        public bool IsInDangerZone;

        /// <summary>Estimated garbage lines this swap would send (in VS mode).</summary>
        public int EstimatedGarbageSent;

        /// <summary>
        /// Invalid/empty candidate for comparison.
        /// </summary>
        public static AISwapCandidate Invalid => new AISwapCandidate
        {
            Position = new Vector2Int(-1, -1),
            Score = float.MinValue
        };

        public bool IsValid => Position.x >= 0 && Position.y >= 0;

        public override string ToString()
        {
            return $"Swap({Position.x},{Position.y}) Score:{Score:F1} Match:{ImmediateMatchCount} Chain:{EstimatedChainLength}";
        }
    }
}