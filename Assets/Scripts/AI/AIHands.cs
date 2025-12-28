using System;
using UnityEngine;

namespace PuzzleAttack.Grid.AI
{
    /// <summary>
    /// The AI's input simulation system. Manages cursor movement and timing.
    /// Provides the human-like appearance of playing.
    /// </summary>
    public class AIHands
    {
        public Vector2Int CursorPosition { get; private set; }
        public bool IsExecuting => _currentPlan != null;
        public bool IsHesitating => _hesitationTimer > 0f;

        public event Action<Vector2Int> OnCursorMoved;
        public event Action<Vector2Int> OnSwapExecuted;

        private readonly AIDifficultySettings _settings;
        private readonly int _gridWidth;
        private readonly int _gridHeight;

        private ExecutionPlan _currentPlan;
        private float _inputTimer;
        private float _hesitationTimer;

        // Seeded random for deterministic hesitation
        private System.Random _random;

        public AIHands(AIDifficultySettings settings, int gridWidth, int gridHeight, int? seed = null)
        {
            _settings = settings;
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;

            // Start cursor at center of grid
            CursorPosition = new Vector2Int(gridWidth / 2 - 1, gridHeight / 2);

            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        /// <summary>
        /// Re-seed the random number generator.
        /// </summary>
        public void SetSeed(int seed)
        {
            _random = new System.Random(seed);
        }

        /// <summary>
        /// Begin executing a swap at the target position.
        /// </summary>
        public void ExecuteSwap(Vector2Int targetPos, bool isPanicking)
        {
            _currentPlan = new ExecutionPlan
            {
                TargetPosition = targetPos,
                IsPanicking = isPanicking
            };

            // Maybe hesitate before starting (humanization)
            if (!isPanicking && _settings.hesitationChance > 0 && RandomFloat() < _settings.hesitationChance)
            {
                _hesitationTimer = RandomFloat() * _settings.maxHesitationDuration;
            }
            else
            {
                _hesitationTimer = 0f;
            }

            _inputTimer = 0f;
        }

        /// <summary>
        /// Cancel current execution plan.
        /// </summary>
        public void CancelPlan()
        {
            _currentPlan = null;
            _hesitationTimer = 0f;
        }

        /// <summary>
        /// Update the hands - process inputs over time.
        /// Returns the swap position if a swap should be performed this frame, null otherwise.
        /// </summary>
        public Vector2Int? Update(float deltaTime)
        {
            if (_currentPlan == null)
                return null;

            // Handle hesitation
            if (_hesitationTimer > 0f)
            {
                _hesitationTimer -= deltaTime;
                return null;
            }

            // Calculate effective input rate
            float secondsPerInput = 1f / _settings.GetEffectiveInputRate(_currentPlan.IsPanicking);

            _inputTimer += deltaTime;

            if (_inputTimer < secondsPerInput)
                return null;

            _inputTimer -= secondsPerInput;

            // Process next input
            return ProcessNextInput();
        }

        private Vector2Int? ProcessNextInput()
        {
            if (_currentPlan == null)
                return null;

            Vector2Int target = _currentPlan.TargetPosition;

            // Move cursor toward target
            if (CursorPosition != target)
            {
                Vector2Int newPos = CursorPosition;

                // Move one step - prioritize horizontal movement
                if (CursorPosition.x < target.x)
                    newPos.x++;
                else if (CursorPosition.x > target.x)
                    newPos.x--;
                else if (CursorPosition.y < target.y)
                    newPos.y++;
                else if (CursorPosition.y > target.y)
                    newPos.y--;

                // Clamp to valid cursor range
                newPos.x = Mathf.Clamp(newPos.x, 0, _gridWidth - 2);
                newPos.y = Mathf.Clamp(newPos.y, 0, _gridHeight - 1);

                CursorPosition = newPos;
                OnCursorMoved?.Invoke(CursorPosition);
                return null;
            }

            // Cursor is at target - signal that swap should be performed
            Vector2Int swapPos = _currentPlan.TargetPosition;
            _currentPlan = null;
            OnSwapExecuted?.Invoke(swapPos);
            return swapPos;
        }

        /// <summary>
        /// Force set cursor position (for initialization or reset).
        /// </summary>
        public void SetCursorPosition(Vector2Int pos)
        {
            pos.x = Mathf.Clamp(pos.x, 0, _gridWidth - 2);
            pos.y = Mathf.Clamp(pos.y, 0, _gridHeight - 1);
            CursorPosition = pos;
            OnCursorMoved?.Invoke(CursorPosition);
        }

        /// <summary>
        /// Get estimated time to reach a target position and perform swap.
        /// </summary>
        public float EstimateTimeToExecute(Vector2Int target, bool isPanicking)
        {
            int manhattanDistance = Mathf.Abs(target.x - CursorPosition.x) +
                                    Mathf.Abs(target.y - CursorPosition.y);
            float secondsPerInput = 1f / _settings.GetEffectiveInputRate(isPanicking);
            return (manhattanDistance + 1) * secondsPerInput; // +1 for the swap itself
        }

        private float RandomFloat()
        {
            return (float)_random.NextDouble();
        }

        private class ExecutionPlan
        {
            public Vector2Int TargetPosition;
            public bool IsPanicking;
        }
    }
}
