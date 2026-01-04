using System.Collections.Generic;
using UnityEngine;
using PuzzleAttack.Grid;
using PuzzleAttack.Grid.AI;

namespace PuzzleAttack
{
    /// <summary>
    /// Initializes the gameplay scene based on GameModeManager configuration.
    /// Spawns grids, sets up AI controllers, and configures the session.
    /// </summary>
    public class GameplaySceneInitializer : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Grid Prefabs")]
        [Tooltip("Prefab for human player grids")]
        [SerializeField] private GameObject gridPrefab;
        
        [Tooltip("Prefab variant for AI-controlled grids (uses gridPrefab if not assigned)")]
        [SerializeField] private GameObject aiGridPrefab;

        [Header("Spawn Configurations")]
        [Tooltip("Spawn position for Marathon mode (single player)")]
        [SerializeField] private Vector3 marathonSpawnPosition = new Vector3(0f, 0f, 0f);

        [Tooltip("Spawn positions for VS mode (2 players)")]
        [SerializeField] private Vector3 vs2P_Player1Position = new Vector3(-5f, 0f, 0f);
        [SerializeField] private Vector3 vs2P_Player2Position = new Vector3(1f, 0f, 0f);

        [Tooltip("Spawn positions for Party mode (3 players)")]
        [SerializeField] private Vector3 party3P_Player1Position = new Vector3(-6f, 0f, 0f);
        [SerializeField] private Vector3 party3P_Player2Position = new Vector3(0f, 0f, 0f);
        [SerializeField] private Vector3 party3P_Player3Position = new Vector3(6f, 0f, 0f);

        [Tooltip("Spawn positions for Party mode (4 players)")]
        [SerializeField] private Vector3 party4P_Player1Position = new Vector3(-5f, 4f, 0f);
        [SerializeField] private Vector3 party4P_Player2Position = new Vector3(1f, 4f, 0f);
        [SerializeField] private Vector3 party4P_Player3Position = new Vector3(-5f, -4f, 0f);
        [SerializeField] private Vector3 party4P_Player4Position = new Vector3(1f, -4f, 0f);

        [Header("References")]
        [SerializeField] private GameSession gameSession;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool useTestConfiguration = false;
        [SerializeField] private GameModeType testModeType = GameModeType.Marathon;
        [SerializeField] private int testPlayerCount = 2;

        #endregion

        #region Private Fields

        private List<GridManager> _spawnedGrids = new List<GridManager>();
        private List<AIController> _spawnedAIControllers = new List<AIController>();

        #endregion

        #region Properties

        public IReadOnlyList<GridManager> SpawnedGrids => _spawnedGrids;
        public IReadOnlyList<AIController> SpawnedAIControllers => _spawnedAIControllers;

        #endregion

        #region Spawn Position Helpers

        /// <summary>
        /// Get spawn positions based on game mode and player count.
        /// </summary>
        private List<Vector3> GetSpawnPositions(GameModeType modeType, int playerCount)
        {
            var positions = new List<Vector3>();

            switch (modeType)
            {
                case GameModeType.Marathon:
                    positions.Add(marathonSpawnPosition);
                    break;

                case GameModeType.VsCPU:
                case GameModeType.VsHuman:
                    // 2 player VS mode
                    positions.Add(vs2P_Player1Position);
                    positions.Add(vs2P_Player2Position);
                    break;

                case GameModeType.Mixed:
                    // Party mode - depends on player count
                    if (playerCount <= 2)
                    {
                        positions.Add(vs2P_Player1Position);
                        positions.Add(vs2P_Player2Position);
                    }
                    else if (playerCount == 3)
                    {
                        positions.Add(party3P_Player1Position);
                        positions.Add(party3P_Player2Position);
                        positions.Add(party3P_Player3Position);
                    }
                    else // 4 players
                    {
                        positions.Add(party4P_Player1Position);
                        positions.Add(party4P_Player2Position);
                        positions.Add(party4P_Player3Position);
                        positions.Add(party4P_Player4Position);
                    }
                    break;

                default:
                    // Fallback to marathon position
                    positions.Add(marathonSpawnPosition);
                    break;
            }

            return positions;
        }

        /// <summary>
        /// Get spawn positions for VS modes based on player count.
        /// </summary>
        private List<Vector3> GetVsSpawnPositions(int playerCount)
        {
            var positions = new List<Vector3>();

            if (playerCount <= 2)
            {
                positions.Add(vs2P_Player1Position);
                positions.Add(vs2P_Player2Position);
            }
            else if (playerCount == 3)
            {
                positions.Add(party3P_Player1Position);
                positions.Add(party3P_Player2Position);
                positions.Add(party3P_Player3Position);
            }
            else // 4 players
            {
                positions.Add(party4P_Player1Position);
                positions.Add(party4P_Player2Position);
                positions.Add(party4P_Player3Position);
                positions.Add(party4P_Player4Position);
            }

            return positions;
        }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            InitializeScene();
        }

        #endregion

        #region Initialization

        private void InitializeScene()
        {
            var manager = GameModeManager.Instance;
            
            if (manager == null || manager.CurrentMode == null)
            {
                Debug.LogWarning("[GameplaySceneInitializer] No GameModeManager config found, using defaults");
                
                if (useTestConfiguration)
                {
                    SetupTestConfiguration();
                }
                else
                {
                    SetupDefaultConfiguration();
                }
                return;
            }

            Debug.Log($"[GameplaySceneInitializer] Initializing scene for mode: {manager.CurrentMode.displayName}");
            manager.LogConfiguration();

            // Initialize MatchScoreTracker
            InitializeMatchScoreTracker(manager);

            switch (manager.CurrentMode.modeType)
            {
                case GameModeType.Marathon:
                    SetupMarathonMode(manager);
                    break;
                case GameModeType.VsCPU:
                case GameModeType.VsHuman:
                case GameModeType.Mixed:
                    SetupVsMode(manager);
                    break;
            }

            // Initialize the game session with all spawned components
            InitializeGameSession();
        }

        private void InitializeMatchScoreTracker(GameModeManager manager)
        {
            // Find or create MatchScoreTracker
            var tracker = MatchScoreTracker.Instance;
            if (tracker == null)
            {
                var trackerObj = new GameObject("MatchScoreTracker");
                tracker = trackerObj.AddComponent<MatchScoreTracker>();
            }

            // Initialize for the number of active players
            int playerCount = manager.ActivePlayerCount;
            tracker.InitializeMatch(playerCount);

            Debug.Log($"[GameplaySceneInitializer] MatchScoreTracker initialized for {playerCount} players");
        }

        private void SetupMarathonMode(GameModeManager manager)
        {
            var playerSlot = manager.GetPlayerSlot(0);
            
            // Spawn single grid at marathon position
            var grid = SpawnGridAtPosition(marathonSpawnPosition, playerSlot, 0, false);
            
            // Enable player input
            if (grid != null && grid.cursorController != null)
            {
                grid.cursorController.IsInputEnabled = true;
            }

            // Register with MatchScoreTracker
            RegisterPlayerWithTracker(0, grid, true);

            Debug.Log($"[GameplaySceneInitializer] Marathon mode setup complete");
        }

        private void SetupVsMode(GameModeManager manager)
        {
            var activeSlots = manager.GetActiveSlots();
            var spawnPositions = GetVsSpawnPositions(activeSlots.Count);
            
            for (int i = 0; i < activeSlots.Count && i < spawnPositions.Count; i++)
            {
                var slot = activeSlots[i];
                var spawnPos = spawnPositions[i];
                
                // Spawn grid (use AI prefab for CPU players)
                var grid = SpawnGridAtPosition(spawnPos, slot, i, slot.IsCPU);
                
                // Setup based on controller type
                if (slot.IsHuman)
                {
                    SetupHumanPlayer(grid, slot);
                }
                else if (slot.IsCPU)
                {
                    SetupCPUPlayer(grid, slot);
                }

                // Register with MatchScoreTracker
                RegisterPlayerWithTracker(i, grid, slot.IsHuman);
            }

            Debug.Log($"[GameplaySceneInitializer] VS mode setup complete - {activeSlots.Count} players");
        }

        private void RegisterPlayerWithTracker(int playerIndex, GridManager grid, bool isHuman)
        {
            if (grid == null) return;

            var tracker = MatchScoreTracker.Instance;
            if (tracker == null)
            {
                Debug.LogWarning("[GameplaySceneInitializer] MatchScoreTracker not found, cannot register player");
                return;
            }

            // Set player index on GridRiser
            if (grid.gridRiser != null)
            {
                grid.gridRiser.PlayerIndex = playerIndex;
            }

            // Set player index on ScoreManager
            if (grid.scoreManager != null)
            {
                grid.scoreManager.PlayerIndex = playerIndex;
                grid.scoreManager.IsHumanPlayer = isHuman;
            }

            // Register with tracker
            tracker.RegisterPlayer(playerIndex, grid.scoreManager, grid, isHuman);

            Debug.Log($"[GameplaySceneInitializer] Registered player {playerIndex} (Human: {isHuman}) with MatchScoreTracker");
        }

        private GridManager SpawnGridAtPosition(Vector3 position, PlayerSlotConfig slot, int index, bool isCPU = false)
        {
            // Choose the appropriate prefab
            GameObject prefabToUse = isCPU && aiGridPrefab != null ? aiGridPrefab : gridPrefab;
            
            if (prefabToUse == null)
            {
                Debug.LogError($"[GameplaySceneInitializer] {(isCPU ? "AI Grid" : "Grid")} prefab not assigned!");
                return null;
            }

            // Spawn the grid
            var gridObj = Instantiate(prefabToUse, position, Quaternion.identity);
            gridObj.name = $"Grid_{(isCPU ? "CPU" : "Player")}{index + 1}_{slot.playerName}";
            
            var grid = gridObj.GetComponent<GridManager>();
            if (grid == null)
            {
                Debug.LogError("[GameplaySceneInitializer] Grid prefab missing GridManager component!");
                Destroy(gridObj);
                return null;
            }

            _spawnedGrids.Add(grid);

            // Apply grid difficulty settings
            ApplyGridDifficulty(grid, slot.gridDifficulty);
            
            // Apply starting speed
            ApplyStartingSpeed(grid, slot.startingSpeed);

            Debug.Log($"[GameplaySceneInitializer] Spawned {(isCPU ? "AI" : "player")} grid for {slot.playerName} at {position} using {prefabToUse.name}");
            
            return grid;
        }

        private void SetupHumanPlayer(GridManager grid, PlayerSlotConfig slot)
        {
            // Enable cursor input
            if (grid.cursorController != null)
            {
                grid.cursorController.IsInputEnabled = true;
                
                // TODO: Configure input device based on slot.inputDeviceIndex
                // This will require input system integration
                Debug.Log($"[GameplaySceneInitializer] Human player {slot.playerName} using input device {slot.inputDeviceIndex}");
            }
        }

        private void SetupCPUPlayer(GridManager grid, PlayerSlotConfig slot)
        {
            // Disable cursor input for AI-controlled grid
            if (grid.cursorController != null)
            {
                grid.cursorController.IsInputEnabled = false;
            }

            // Check if grid already has an AIController (from prefab)
            var aiController = grid.GetComponent<AIController>();
            
            if (aiController == null)
            {
                // Add AIController component directly to the grid GameObject
                // This satisfies the [RequireComponent(typeof(GridManager))] requirement
                aiController = grid.gameObject.AddComponent<AIController>();
            }

            if (aiController != null)
            {
                // Initialize with difficulty settings and unique seed based on player index
                // This ensures each AI behaves differently even with same difficulty
                int aiSeed = System.Environment.TickCount + (slot.playerIndex * 12345);
                aiController.Initialize(aiSeed, slot.aiDifficulty);
                
                _spawnedAIControllers.Add(aiController);
                
                Debug.Log($"[GameplaySceneInitializer] AI controller configured for {slot.playerName} with difficulty {slot.aiDifficulty?.displayName}, seed {aiSeed}");
            }
        }

        private void ApplyGridDifficulty(GridManager grid, GridDifficultySettings difficulty)
        {
            if (difficulty == null)
            {
                Debug.Log("[GameplaySceneInitializer] No grid difficulty specified, using defaults");
                return;
            }

            // Apply difficulty settings to grid components
            // Note: This requires GridRiser and other components to have setter methods
            // or we store the difficulty reference for them to query
            
            var gridRiser = grid.gridRiser;
            if (gridRiser != null)
            {
                // TODO: Add methods to GridRiser to accept difficulty settings
                // gridRiser.SetDifficultySettings(difficulty);
                Debug.Log($"[GameplaySceneInitializer] Applied grid difficulty: {difficulty.displayName}");
            }

            // Store difficulty reference on grid for other systems to access
            var difficultyHolder = grid.gameObject.AddComponent<GridDifficultyHolder>();
            difficultyHolder.difficulty = difficulty;
        }

        private void ApplyStartingSpeed(GridManager grid, int startingSpeed)
        {
            var gridRiser = grid.gridRiser;
            if (gridRiser != null)
            {
                // TODO: Add method to GridRiser to set starting speed
                // gridRiser.SetStartingSpeedLevel(startingSpeed);
                Debug.Log($"[GameplaySceneInitializer] Set starting speed: Level {startingSpeed}");
            }
        }

        private void InitializeGameSession()
        {
            if (gameSession == null)
            {
                // Try to find or create game session
                gameSession = FindObjectOfType<GameSession>();
                if (gameSession == null)
                {
                    var sessionObj = new GameObject("GameSession");
                    gameSession = sessionObj.AddComponent<GameSession>();
                }
            }

            // Add all spawned grids to session
            foreach (var grid in _spawnedGrids)
            {
                gameSession.AddGrid(grid);
            }

            // Add all AI controllers to session
            foreach (var ai in _spawnedAIControllers)
            {
                gameSession.AddAIController(ai);
            }

            // Set session seed if specified
            var manager = GameModeManager.Instance;
            if (manager != null && manager.SessionSeed != -1)
            {
                gameSession.SetSeed(manager.SessionSeed);
            }

            Debug.Log($"[GameplaySceneInitializer] Game session initialized with {_spawnedGrids.Count} grids and {_spawnedAIControllers.Count} AI controllers");
        }

        /// <summary>
        /// Initialize MatchScoreTracker for test/default configurations (when no GameModeManager config exists).
        /// </summary>
        private void InitializeMatchScoreTrackerForTest(int playerCount)
        {
            // Find or create MatchScoreTracker
            var tracker = MatchScoreTracker.Instance;
            if (tracker == null)
            {
                var trackerObj = new GameObject("MatchScoreTracker");
                tracker = trackerObj.AddComponent<MatchScoreTracker>();
            }

            tracker.InitializeMatch(playerCount);
            Debug.Log($"[GameplaySceneInitializer] MatchScoreTracker initialized for test config with {playerCount} players");
        }

        #endregion

        #region Fallback/Test Configuration

        private void SetupDefaultConfiguration()
        {
            Debug.Log("[GameplaySceneInitializer] Setting up default single-player configuration");
            
            // Initialize MatchScoreTracker for 1 player
            InitializeMatchScoreTrackerForTest(1);
            
            // Use shared marathon grid setup
            SetupDefaultMarathonGrid();
        }

        private void SetupTestConfiguration()
        {
            Debug.Log($"[GameplaySceneInitializer] Setting up test configuration: {testModeType} with {testPlayerCount} players");
            
            // Initialize MatchScoreTracker based on test mode
            int playerCount = testModeType == GameModeType.Marathon ? 1 : testPlayerCount;
            InitializeMatchScoreTrackerForTest(playerCount);
            
            switch (testModeType)
            {
                case GameModeType.Marathon:
                    SetupDefaultMarathonGrid();
                    break;
                    
                case GameModeType.VsCPU:
                    SetupTestVsCPU();
                    break;
                    
                case GameModeType.VsHuman:
                    SetupTestVsHuman();
                    break;

                case GameModeType.Mixed:
                    SetupTestParty();
                    break;
            }
        }

        /// <summary>
        /// Spawns a single marathon grid and registers with tracker.
        /// Called by both SetupDefaultConfiguration and SetupTestConfiguration.
        /// </summary>
        private void SetupDefaultMarathonGrid()
        {
            if (gridPrefab != null)
            {
                var gridObj = Instantiate(gridPrefab, marathonSpawnPosition, Quaternion.identity);
                gridObj.name = "Grid_Player1";
                
                var grid = gridObj.GetComponent<GridManager>();
                if (grid != null)
                {
                    _spawnedGrids.Add(grid);
                    
                    if (grid.cursorController != null)
                    {
                        grid.cursorController.IsInputEnabled = true;
                    }

                    // Register with MatchScoreTracker
                    RegisterPlayerWithTracker(0, grid, true);
                }
            }

            InitializeGameSession();
        }

        private void SetupTestVsCPU()
        {
            var positions = GetVsSpawnPositions(2);

            // Player 1 - Human
            GridManager grid1 = null;
            if (gridPrefab != null)
            {
                var grid1Obj = Instantiate(gridPrefab, positions[0], Quaternion.identity);
                grid1Obj.name = "Grid_Player1";
                grid1 = grid1Obj.GetComponent<GridManager>();
                if (grid1 != null)
                {
                    _spawnedGrids.Add(grid1);
                    if (grid1.cursorController != null)
                        grid1.cursorController.IsInputEnabled = true;
                }
            }

            // Player 2 - CPU (use AI prefab if available)
            GridManager grid2 = null;
            GameObject cpuPrefab = aiGridPrefab != null ? aiGridPrefab : gridPrefab;
            if (cpuPrefab != null)
            {
                var grid2Obj = Instantiate(cpuPrefab, positions[1], Quaternion.identity);
                grid2Obj.name = "Grid_CPU1";
                grid2 = grid2Obj.GetComponent<GridManager>();
                if (grid2 != null)
                {
                    _spawnedGrids.Add(grid2);
                    if (grid2.cursorController != null)
                        grid2.cursorController.IsInputEnabled = false;

                    // Check if AI prefab already has AIController, if not add one
                    var ai = grid2.GetComponent<AIController>();
                    if (ai == null)
                    {
                        ai = grid2.gameObject.AddComponent<AIController>();
                    }
                    
                    if (ai != null)
                    {
                        // Use medium difficulty by default with unique seed
                        var mediumDiff = AIDifficultySettings.CreateMedium();
                        int aiSeed = System.Environment.TickCount + 12345;
                        ai.Initialize(aiSeed, mediumDiff);
                        _spawnedAIControllers.Add(ai);
                    }
                }
            }

            // Register players with tracker
            if (grid1 != null) RegisterPlayerWithTracker(0, grid1, true);
            if (grid2 != null) RegisterPlayerWithTracker(1, grid2, false);

            InitializeGameSession();
        }

        private void SetupTestVsHuman()
        {
            var positions = GetVsSpawnPositions(2);

            for (int i = 0; i < 2; i++)
            {
                if (gridPrefab != null)
                {
                    var gridObj = Instantiate(gridPrefab, positions[i], Quaternion.identity);
                    gridObj.name = $"Grid_Player{i + 1}";
                    var grid = gridObj.GetComponent<GridManager>();
                    if (grid != null)
                    {
                        _spawnedGrids.Add(grid);
                        if (grid.cursorController != null)
                            grid.cursorController.IsInputEnabled = true;

                        // Register with tracker
                        RegisterPlayerWithTracker(i, grid, true);
                    }
                }
            }

            InitializeGameSession();
        }

        private void SetupTestParty()
        {
            int playerCount = Mathf.Clamp(testPlayerCount, 2, 4);
            var positions = GetVsSpawnPositions(playerCount);

            for (int i = 0; i < playerCount; i++)
            {
                // Only player 0 is human, all others are CPU
                bool isCPU = i > 0;
                GameObject prefab = isCPU && aiGridPrefab != null ? aiGridPrefab : gridPrefab;
                
                if (prefab != null)
                {
                    var gridObj = Instantiate(prefab, positions[i], Quaternion.identity);
                    gridObj.name = isCPU ? $"Grid_CPU{i}" : $"Grid_Player{i + 1}";
                    var grid = gridObj.GetComponent<GridManager>();
                    
                    if (grid != null)
                    {
                        _spawnedGrids.Add(grid);
                        
                        if (grid.cursorController != null)
                            grid.cursorController.IsInputEnabled = !isCPU;

                        if (isCPU)
                        {
                            var ai = grid.GetComponent<AIController>();
                            if (ai == null)
                            {
                                ai = grid.gameObject.AddComponent<AIController>();
                            }
                            
                            if (ai != null)
                            {
                                var mediumDiff = AIDifficultySettings.CreateMedium();
                                // Give each AI a unique seed based on player index
                                int aiSeed = System.Environment.TickCount + (i * 12345);
                                ai.Initialize(aiSeed, mediumDiff);
                                _spawnedAIControllers.Add(ai);
                            }
                        }

                        // Register with tracker
                        RegisterPlayerWithTracker(i, grid, !isCPU);
                    }
                }
            }

            InitializeGameSession();
        }

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            float gridWidth = 6f;  // Approximate grid width for visualization
            float gridHeight = 12f; // Approximate grid height for visualization
            
            // Marathon spawn (cyan)
            DrawGridGizmo(marathonSpawnPosition, Color.cyan, "Marathon", gridWidth, gridHeight);
            
            // VS 2P spawns (green)
            DrawGridGizmo(vs2P_Player1Position, Color.green, "VS P1", gridWidth, gridHeight);
            DrawGridGizmo(vs2P_Player2Position, new Color(0f, 0.7f, 0f), "VS P2", gridWidth, gridHeight);
            
            // Party 3P spawns (yellow)
            DrawGridGizmo(party3P_Player1Position, Color.yellow, "3P-1", gridWidth, gridHeight);
            DrawGridGizmo(party3P_Player2Position, new Color(0.8f, 0.8f, 0f), "3P-2", gridWidth, gridHeight);
            DrawGridGizmo(party3P_Player3Position, new Color(0.6f, 0.6f, 0f), "3P-3", gridWidth, gridHeight);
            
            // Party 4P spawns (magenta)
            DrawGridGizmo(party4P_Player1Position, Color.magenta, "4P-1", gridWidth, gridHeight);
            DrawGridGizmo(party4P_Player2Position, new Color(0.8f, 0f, 0.8f), "4P-2", gridWidth, gridHeight);
            DrawGridGizmo(party4P_Player3Position, new Color(0.6f, 0f, 0.6f), "4P-3", gridWidth, gridHeight);
            DrawGridGizmo(party4P_Player4Position, new Color(0.4f, 0f, 0.4f), "4P-4", gridWidth, gridHeight);
        }

        private void DrawGridGizmo(Vector3 position, Color color, string label, float width, float height)
        {
            Gizmos.color = color;
            
            // Draw a rectangle representing the grid area
            Vector3 center = position + new Vector3(width / 2f, height / 2f, 0f);
            Vector3 size = new Vector3(width, height, 0.1f);
            Gizmos.DrawWireCube(center, size);
            
            // Draw origin point
            Gizmos.DrawSphere(position, 0.3f);
            
            #if UNITY_EDITOR
            // Draw label
            UnityEditor.Handles.Label(position + Vector3.up * (height + 0.5f), label);
            #endif
        }

        #endregion
    }

    /// <summary>
    /// Simple component to hold grid difficulty reference for other systems to access.
    /// </summary>
    public class GridDifficultyHolder : MonoBehaviour
    {
        public GridDifficultySettings difficulty;
    }
}