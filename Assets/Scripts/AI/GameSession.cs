using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Manages a game session with multiple grid instances.
    /// Ensures all grids use the same seed for fair play.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Session Settings")]
        [Tooltip("Seed for this game session. -1 = random seed.")]
        [SerializeField] private int _sessionSeed = -1;
        
        [Tooltip("If true, display the seed on screen (for tournaments, etc.)")]
        [SerializeField] private bool _showSeed = false;

        [Header("Grid Instances")]
        [Tooltip("All grid instances participating in this session")]
        [SerializeField] private List<GridManager> _gridInstances = new List<GridManager>();

        [Header("AI Controllers")]
        [Tooltip("AI controllers (if any) - will be initialized with session seed")]
        [SerializeField] private List<AI.AIController> _aiControllers = new List<AI.AIController>();

        #endregion

        #region Private Fields

        private int _activeSeed;
        private bool _isInitialized;

        #endregion

        #region Properties

        /// <summary>
        /// The active seed being used for this session.
        /// </summary>
        public int ActiveSeed => _activeSeed;

        /// <summary>
        /// All grid instances in this session.
        /// </summary>
        public IReadOnlyList<GridManager> GridInstances => _gridInstances;

        /// <summary>
        /// Number of players/grids in this session.
        /// </summary>
        public int PlayerCount => _gridInstances.Count;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSession();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the session with all grids using the same seed.
        /// </summary>
        public void InitializeSession()
        {
            if (_isInitialized) return;

            // Generate or use provided seed
            _activeSeed = _sessionSeed == -1 
                ? GenerateRandomSeed() 
                : _sessionSeed;

            Debug.Log($"[GameSession] Initializing with seed: {_activeSeed}");

            // Initialize all grid TileSpawners with the same seed
            for (int i = 0; i < _gridInstances.Count; i++)
            {
                var grid = _gridInstances[i];
                if (grid == null) continue;

                var spawner = grid.tileSpawner;
                if (spawner != null)
                {
                    // Each grid gets the same seed so they generate identical tile sequences
                    spawner.SetSeed(_activeSeed);
                    Debug.Log($"[GameSession] Grid {i} TileSpawner seeded with: {_activeSeed}");
                }
            }

            // Initialize AI controllers with derived seeds
            for (int i = 0; i < _aiControllers.Count; i++)
            {
                var ai = _aiControllers[i];
                if (ai == null) continue;

                // AI gets a different derived seed for its decisions
                // but still deterministic across restarts
                int aiSeed = _activeSeed + (i + 1) * 10000;
                ai.Initialize(aiSeed);
                Debug.Log($"[GameSession] AI {i} seeded with: {aiSeed}");
            }

            _isInitialized = true;
        }

        /// <summary>
        /// Restart the session with the same seed (rematch).
        /// </summary>
        public void RestartWithSameSeed()
        {
            Debug.Log($"[GameSession] Restarting with same seed: {_activeSeed}");
            
            // Reset all grids and re-seed
            foreach (var grid in _gridInstances)
            {
                if (grid == null) continue;
                
                grid.tileSpawner?.SetSeed(_activeSeed);
                // Note: Full grid reset logic would need to be implemented in GridManager
            }

            for (int i = 0; i < _aiControllers.Count; i++)
            {
                var ai = _aiControllers[i];
                if (ai == null) continue;

                int aiSeed = _activeSeed + (i + 1) * 10000;
                ai.SetSeed(aiSeed);
            }
        }

        /// <summary>
        /// Restart the session with a new random seed.
        /// </summary>
        public void RestartWithNewSeed()
        {
            _sessionSeed = -1;
            _activeSeed = GenerateRandomSeed();
            _isInitialized = false;
            InitializeSession();
        }

        /// <summary>
        /// Set a specific seed and reinitialize.
        /// </summary>
        public void SetSeed(int seed)
        {
            _sessionSeed = seed;
            _isInitialized = false;
            InitializeSession();
        }

        private int GenerateRandomSeed()
        {
            // Generate a seed that's easy to share/remember (5 digits)
            return Random.Range(10000, 99999);
        }

        #endregion

        #region Grid Management

        /// <summary>
        /// Add a grid instance to the session.
        /// </summary>
        public void AddGrid(GridManager grid)
        {
            if (grid == null || _gridInstances.Contains(grid)) return;
            
            _gridInstances.Add(grid);
            
            if (_isInitialized && grid.tileSpawner != null)
            {
                grid.tileSpawner.SetSeed(_activeSeed);
            }
        }

        /// <summary>
        /// Remove a grid instance from the session.
        /// </summary>
        public void RemoveGrid(GridManager grid)
        {
            _gridInstances.Remove(grid);
        }

        /// <summary>
        /// Add an AI controller to the session.
        /// </summary>
        public void AddAIController(AI.AIController ai)
        {
            if (ai == null || _aiControllers.Contains(ai)) return;
            
            _aiControllers.Add(ai);
            
            if (_isInitialized)
            {
                int aiSeed = _activeSeed + _aiControllers.Count * 10000;
                ai.Initialize(aiSeed);
            }
        }

        /// <summary>
        /// Remove an AI controller from the session.
        /// </summary>
        public void RemoveAIController(AI.AIController ai)
        {
            _aiControllers.Remove(ai);
        }

        /// <summary>
        /// Get a specific grid instance by index.
        /// </summary>
        public GridManager GetGrid(int index)
        {
            if (index < 0 || index >= _gridInstances.Count) return null;
            return _gridInstances[index];
        }

        /// <summary>
        /// Get a specific AI controller by index.
        /// </summary>
        public AI.AIController GetAIController(int index)
        {
            if (index < 0 || index >= _aiControllers.Count) return null;
            return _aiControllers[index];
        }

        #endregion

        #region Debug

        private void OnGUI()
        {
            if (!_showSeed) return;

            GUILayout.BeginArea(new Rect(10, 10, 200, 60));
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>Session Seed:</b> {_activeSeed}");
            GUILayout.Label($"Players: {_gridInstances.Count} | AIs: {_aiControllers.Count}");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        #endregion
    }
}
