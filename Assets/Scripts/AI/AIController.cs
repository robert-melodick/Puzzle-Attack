using UnityEngine;

namespace PuzzleAttack.Grid.AI
{
    /// <summary>
    /// Main AI controller that coordinates thinking and execution.
    /// Works with its own GridManager instance - does not share with player.
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    public class AIController : MonoBehaviour, ICursorCommands
    {
        #region Inspector Fields

        [Header("AI Settings")]
        [SerializeField] private AIDifficultySettings _difficultySettings;

        [Header("Cursor Visual")]
        [Tooltip("Prefab for the AI cursor. If null, a simple cursor will be generated.")]
        [SerializeField] private GameObject _cursorPrefab;
        
        [Tooltip("Color tint for the AI cursor (applied to SpriteRenderer if present)")]
        [SerializeField] private Color _cursorColor = new Color(1f, 0.5f, 0.5f, 0.7f);
        
        [Tooltip("Sorting order for the cursor sprite")]
        [SerializeField] private int _cursorSortingOrder = 10;

        [Header("Debug")]
        [SerializeField] private bool _showDebugInfo = false;
        [SerializeField] private bool _logDecisions = false;

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private AIBrain _brain;
        private AIHands _hands;

        private GameObject _cursorVisual;
        private SpriteRenderer _cursorRenderer;

        private float _thinkTimer;
        private float _postSwapCooldown;
        private AISwapCandidate _currentTarget;
        private bool _isPanicking;
        private bool _isEnabled = true;

        private int? _seed;

        // Fast rise state
        private float _fastRiseCooldownTimer;
        private float _fastRiseDurationTimer;
        private bool _isFastRising;

        #endregion

        #region Properties

        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        public Vector2Int CursorPosition => _hands?.CursorPosition ?? Vector2Int.zero;
        public bool IsPanicking => _isPanicking;
        public AISwapCandidate CurrentTarget => _currentTarget;
        public AIDifficultySettings DifficultySettings => _difficultySettings;
        public GridManager GridManager => _gridManager;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _gridManager = GetComponent<GridManager>();

            if (_difficultySettings == null)
            {
                Debug.LogWarning("[AIController] No difficulty settings assigned, creating default Medium settings");
                _difficultySettings = AIDifficultySettings.CreateMedium();
            }
        }

        private void Start()
        {
            InitializeAI(_seed);
            CreateCursorVisual();
        }

        private void Update()
        {
            if (!_isEnabled || _gridManager == null)
                return;

            if (_gridManager.gridRiser != null && _gridManager.gridRiser.IsGameOver)
                return;

            if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused)
                return;

            // Update panic state
            UpdatePanicState();

            // Update fast rise behavior
            UpdateFastRise();

            // Handle post-swap cooldown
            if (_postSwapCooldown > 0f)
            {
                _postSwapCooldown -= Time.deltaTime;
                UpdateCursorVisual(); // Keep cursor synced with grid offset
                return;
            }

            // If hands are busy executing, let them work
            if (_hands != null && _hands.IsExecuting)
            {
                Vector2Int? swapPos = _hands.Update(Time.deltaTime);

                if (swapPos.HasValue)
                {
                    // Hands reached target - perform the swap
                    PerformSwapAtPosition(swapPos.Value);
                    _postSwapCooldown = _difficultySettings.postSwapCooldown;
                    _currentTarget = AISwapCandidate.Invalid;

                    if (_logDecisions)
                        Debug.Log($"[AI] Swap completed at ({swapPos.Value.x}, {swapPos.Value.y})");
                }

                UpdateCursorVisual();
                return;
            }

            // Can't think while grid is busy
            if (_gridManager.IsSwapping)
            {
                UpdateCursorVisual();
                return;
            }

            if (_gridManager.matchProcessor != null && _gridManager.matchProcessor.IsProcessingMatches)
            {
                UpdateCursorVisual();
                return;
            }

            // Reaction delay (think timer)
            _thinkTimer -= Time.deltaTime;
            if (_thinkTimer > 0f)
            {
                UpdateCursorVisual();
                return;
            }

            // Time to think!
            Think();
            UpdateCursorVisual();
        }

        private void OnDestroy()
        {
            if (_hands != null)
            {
                _hands.OnCursorMoved -= HandleCursorMoved;
                _hands.OnSwapExecuted -= HandleSwapExecuted;
            }

            if (_cursorVisual != null)
                Destroy(_cursorVisual);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the AI with an optional seed for deterministic behavior.
        /// Call this before Start() if you need to set a seed.
        /// </summary>
        public void Initialize(int? seed = null, AIDifficultySettings settings = null)
        {
            _seed = seed;

            if (settings != null)
                _difficultySettings = settings;
        }

        private void InitializeAI(int? seed)
        {
            _brain = new AIBrain(
                _difficultySettings,
                _gridManager,
                _gridManager.matchDetector,
                _gridManager.dangerZoneManager,
                _gridManager.garbageManager,
                seed
            );

            _hands = new AIHands(
                _difficultySettings,
                _gridManager.Width,
                _gridManager.Height,
                seed.HasValue ? seed.Value + 1000 : null // Offset seed for hands
            );

            _hands.OnCursorMoved += HandleCursorMoved;
            _hands.OnSwapExecuted += HandleSwapExecuted;

            // Set initial think delay
            _thinkTimer = _difficultySettings.reactionDelaySeconds;

            if (_logDecisions)
                Debug.Log($"[AIController] Initialized with seed: {seed?.ToString() ?? "random"}");
        }

        /// <summary>
        /// Re-seed the AI (for match restarts with same seed).
        /// </summary>
        public void SetSeed(int seed)
        {
            _seed = seed;
            _brain?.SetSeed(seed);
            _hands?.SetSeed(seed + 1000);
        }

        #endregion

        #region Cursor Visual

        private void CreateCursorVisual()
        {
            if (_cursorPrefab != null)
            {
                // Use provided prefab
                _cursorVisual = Instantiate(_cursorPrefab, transform);
                _cursorVisual.name = "AI_Cursor";

                // Try to get and configure SpriteRenderer
                _cursorRenderer = _cursorVisual.GetComponent<SpriteRenderer>();
                if (_cursorRenderer == null)
                    _cursorRenderer = _cursorVisual.GetComponentInChildren<SpriteRenderer>();

                if (_cursorRenderer != null)
                {
                    _cursorRenderer.color = _cursorColor;
                    _cursorRenderer.sortingOrder = _cursorSortingOrder;
                }
            }
            else
            {
                // Create fallback cursor
                _cursorVisual = new GameObject("AI_Cursor");
                _cursorVisual.transform.SetParent(transform);

                _cursorRenderer = _cursorVisual.AddComponent<SpriteRenderer>();
                _cursorRenderer.sprite = CreateFallbackCursorSprite();
                _cursorRenderer.color = _cursorColor;
                _cursorRenderer.sortingOrder = _cursorSortingOrder;
            }

            UpdateCursorVisual();
        }

        private Sprite CreateFallbackCursorSprite()
        {
            // Create a simple 2-tile-wide cursor outline
            int width = 200;
            int height = 100;
            var tex = new Texture2D(width, height);
            var pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            // Draw border
            int borderWidth = 6;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (x < borderWidth || x >= width - borderWidth ||
                        y < borderWidth || y >= height - borderWidth)
                    {
                        pixels[y * width + x] = Color.white;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100);
        }

        private void UpdateCursorVisual()
        {
            if (_cursorVisual == null || _hands == null || _gridManager == null)
                return;

            float tileSize = _gridManager.TileSize;
            float gridOffset = _gridManager.gridRiser?.CurrentGridOffset ?? 0f;

            // Position cursor to cover both tiles using grid origin
            Vector2Int cursorPos = _hands.CursorPosition;
            Vector3 worldPos = _gridManager.GridToWorldPosition(cursorPos.x, cursorPos.y, gridOffset);
            
            // Offset to center between two tiles
            worldPos.x += tileSize * 0.5f;
            worldPos.z = -1f;

            _cursorVisual.transform.position = worldPos;

            // Only scale if using fallback cursor (prefab handles its own scale)
            if (_cursorPrefab == null)
            {
                _cursorVisual.transform.localScale = new Vector3(tileSize * 2f, tileSize, 1f);
            }
        }

        #endregion

        #region AI Logic

        private void Think()
        {
            // Reset think timer
            float reactionDelay = _difficultySettings.reactionDelaySeconds;
            if (_isPanicking)
                reactionDelay *= 0.5f;

            _thinkTimer = reactionDelay;

            // Find best swap
            _currentTarget = _brain.FindBestSwap(_isPanicking);

            if (_currentTarget.IsValid)
            {
                if (_logDecisions)
                {
                    Debug.Log($"[AI] Target: {_currentTarget} | Panic: {_isPanicking}");
                }

                // Tell hands to move to target and execute
                _hands.ExecuteSwap(_currentTarget.Position, _isPanicking);
            }
            else
            {
                if (_logDecisions)
                    Debug.Log("[AI] No valid swap found");
            }
        }

        private void PerformSwapAtPosition(Vector2Int position)
        {
            // Validate the swap is still possible
            if (_gridManager.IsSwapping)
            {
                if (_logDecisions)
                    Debug.Log("[AI] Swap aborted - grid already swapping");
                return;
            }

            if (_gridManager.IsGarbageAt(position.x, position.y) ||
                _gridManager.IsGarbageAt(position.x + 1, position.y))
            {
                if (_logDecisions)
                    Debug.Log("[AI] Swap aborted - garbage at position");
                return;
            }

            if (!_gridManager.CanTileSwap(position.x, position.y) ||
                !_gridManager.CanTileSwap(position.x + 1, position.y))
            {
                if (_logDecisions)
                    Debug.Log("[AI] Swap aborted - tiles cannot swap");
                return;
            }

            // Perform the swap using GridManager's direct swap method
            _gridManager.PerformSwapAtPosition(position);
        }

        private void UpdatePanicState()
        {
            if (_gridManager.dangerZoneManager != null)
            {
                float intensity = _gridManager.dangerZoneManager.DangerIntensity;
                _isPanicking = intensity >= _difficultySettings.panicThreshold;
            }
            else
            {
                _isPanicking = CalculateBoardFillPercentage() >= 0.75f;
            }
        }

        private void UpdateFastRise()
        {
            if (_gridManager.gridRiser == null)
                return;

            // Handle cooldown
            if (_fastRiseCooldownTimer > 0f)
            {
                _fastRiseCooldownTimer -= Time.deltaTime;
            }

            // Handle active fast rise
            if (_isFastRising)
            {
                _fastRiseDurationTimer -= Time.deltaTime;
                
                if (_fastRiseDurationTimer <= 0f)
                {
                    // Stop fast rising
                    _isFastRising = false;
                    _gridManager.gridRiser.IsFastRising = false;
                    _fastRiseCooldownTimer = _difficultySettings.fastRiseCooldown;
                    
                    if (_logDecisions)
                        Debug.Log("[AI] Fast rise ended");
                }
                return;
            }

            // Check if we should start fast rising
            if (!_difficultySettings.canFastRise)
                return;

            if (_fastRiseCooldownTimer > 0f)
                return;

            // Don't fast rise while panicking or if stack is too high
            if (_isPanicking)
                return;

            // Check stack height
            float stackHeight = GetStackHeightRatio();
            if (stackHeight >= _difficultySettings.fastRiseStackThreshold)
                return;

            // Don't fast rise during combos or matches
            if (_gridManager.matchProcessor != null && _gridManager.matchProcessor.IsProcessingMatches)
                return;

            if (_gridManager.IsSwapping)
                return;

            // Random chance to fast rise
            float rollPerSecond = _difficultySettings.fastRiseChance;
            float rollThisFrame = rollPerSecond * Time.deltaTime;
            
            if (_brain != null && _brain.GetRandomFloat() < rollThisFrame)
            {
                // Start fast rising
                _isFastRising = true;
                _fastRiseDurationTimer = _difficultySettings.maxFastRiseDuration;
                _gridManager.gridRiser.IsFastRising = true;
                
                if (_logDecisions)
                    Debug.Log($"[AI] Fast rise started (stack at {stackHeight:P0})");
            }
        }

        private float GetStackHeightRatio()
        {
            var grid = _gridManager.Grid;
            int width = _gridManager.Width;
            int height = _gridManager.Height;
            int highestRow = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = height - 1; y >= 0; y--)
                {
                    if (grid[x, y] != null)
                    {
                        highestRow = Mathf.Max(highestRow, y + 1);
                        break;
                    }
                }
            }

            return (float)highestRow / height;
        }

        private float CalculateBoardFillPercentage()
        {
            var grid = _gridManager.Grid;
            int width = _gridManager.Width;
            int height = _gridManager.Height;

            int filledCells = 0;
            int highestRow = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (grid[x, y] != null)
                    {
                        filledCells++;
                        highestRow = Mathf.Max(highestRow, y);
                    }
                }
            }

            float fillRatio = (float)filledCells / (width * height);
            float heightRatio = (float)highestRow / height;

            return Mathf.Max(fillRatio, heightRatio);
        }

        #endregion

        #region ICursorCommands Implementation

        public void MoveLeft() => _hands?.SetCursorPosition(_hands.CursorPosition + Vector2Int.left);
        public void MoveRight() => _hands?.SetCursorPosition(_hands.CursorPosition + Vector2Int.right);
        public void MoveUp() => _hands?.SetCursorPosition(_hands.CursorPosition + Vector2Int.up);
        public void MoveDown() => _hands?.SetCursorPosition(_hands.CursorPosition + Vector2Int.down);
        public void Swap() => PerformSwapAtPosition(_hands?.CursorPosition ?? Vector2Int.zero);
        public void FastRiseGrid() => _gridManager?.gridRiser?.RequestFastRise();

        #endregion

        #region Event Handlers

        private void HandleCursorMoved(Vector2Int newPos)
        {
            UpdateCursorVisual();
        }

        private void HandleSwapExecuted(Vector2Int pos)
        {
            // Visual/audio feedback could go here
        }

        #endregion

        #region Public API

        /// <summary>
        /// Force the AI to immediately reconsider its current plan.
        /// </summary>
        public void InterruptAndRethink()
        {
            _hands?.CancelPlan();
            _thinkTimer = 0f;
            _currentTarget = AISwapCandidate.Invalid;
        }

        /// <summary>
        /// Set a new difficulty configuration at runtime.
        /// </summary>
        public void SetDifficulty(AIDifficultySettings newSettings)
        {
            _difficultySettings = newSettings;

            // Reinitialize with new settings
            InitializeAI(_seed);
        }

        /// <summary>
        /// Pause or unpause the AI.
        /// </summary>
        public void SetPaused(bool paused)
        {
            _isEnabled = !paused;
        }

        #endregion

        #region Debug

        private void OnGUI()
        {
            if (!_showDebugInfo || _hands == null)
                return;

            GUILayout.BeginArea(new Rect(Screen.width - 320, 10, 310, 220));
            GUILayout.BeginVertical("box");

            GUILayout.Label($"<b>AI: {_difficultySettings.displayName}</b>");
            GUILayout.Label($"State: {GetStateString()}");
            GUILayout.Label($"Cursor: ({_hands.CursorPosition.x}, {_hands.CursorPosition.y})");
            GUILayout.Label($"Panicking: {_isPanicking}");
            GUILayout.Label($"Think Timer: {_thinkTimer:F2}s");
            GUILayout.Label($"Cooldown: {_postSwapCooldown:F2}s");

            if (_currentTarget.IsValid)
            {
                GUILayout.Space(5);
                GUILayout.Label($"<b>Current Target:</b>");
                GUILayout.Label($"  Position: ({_currentTarget.Position.x}, {_currentTarget.Position.y})");
                GUILayout.Label($"  Score: {_currentTarget.Score:F1}");
                GUILayout.Label($"  Matches: {_currentTarget.ImmediateMatchCount}");
                GUILayout.Label($"  Chain: {_currentTarget.EstimatedChainLength}");
                GUILayout.Label($"  Clears Garbage: {_currentTarget.ClearsGarbage}");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private string GetStateString()
        {
            if (_hands == null) return "Not Initialized";
            if (_hands.IsHesitating) return "Hesitating";
            if (_hands.IsExecuting) return "Executing";
            if (_postSwapCooldown > 0) return "Cooldown";
            return "Thinking";
        }

        #endregion
    }
}