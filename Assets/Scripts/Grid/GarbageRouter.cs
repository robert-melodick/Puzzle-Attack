using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Routes garbage between grids in VS mode.
    /// Handles attack targeting, garbage queuing, and counter mechanics.
    /// </summary>
    public class GarbageRouter : MonoBehaviour
    {
        #region Enums & Data Structures

        /// <summary>
        /// How garbage is distributed when there are multiple opponents.
        /// </summary>
        public enum TargetingMode
        {
            /// <summary>All garbage goes to the next player in sequence.</summary>
            Sequential,

            /// <summary>Garbage is split evenly among all opponents.</summary>
            SplitEvenly,

            /// <summary>All garbage goes to all opponents (brutal mode).</summary>
            AllOpponents,

            /// <summary>Garbage goes to a randomly selected opponent.</summary>
            Random,

            /// <summary>Garbage targets the opponent with the lowest stack.</summary>
            LowestStack,

            /// <summary>Garbage targets the opponent with the highest stack.</summary>
            HighestStack
        }

        /// <summary>
        /// Defines a garbage block size and its cost in garbage score.
        /// </summary>
        [System.Serializable]
        public class GarbageBlockCost
        {
            [Tooltip("Width of the garbage block (in tiles)")]
            public int width = 1;

            [Tooltip("Height of the garbage block (in tiles)")]
            public int height = 1;

            [Tooltip("Garbage score required to spawn this block (0 = disabled)")]
            public int cost = 0;

            public GarbageBlockCost(int w, int h, int c)
            {
                width = w;
                height = h;
                cost = c;
            }
        }

        /// <summary>
        /// Tracks match data during a combo for score calculation.
        /// </summary>
        private class ComboMatchData
        {
            public List<int> matchSizes = new List<int>();
            public int maxChain = 0;
        }

        #endregion

        #region Inspector Fields

        [Header("Routing Settings")]
        [Tooltip("How garbage is distributed to opponents")]
        public TargetingMode targetingMode = TargetingMode.Sequential;

        [Tooltip("Delay before garbage is sent after a combo/chain ends")]
        public float garbageSendDelay = 0.5f;

        [Tooltip("If true, pending garbage can be countered by making matches")]
        public bool allowCountering = true;

        [Header("Garbage Score System")]
        [Tooltip("Garbage score per match size (index = tiles matched, e.g. [0]=unused, [3]=3-match, [4]=4-match, etc.)")]
        public int[] matchSizeScores = { 0, 0, 0, 50, 100, 175, 300, 400, 500, 550, 600 };

        [Tooltip("Garbage score bonus per combo level (index = combo count, e.g. [1]=1combo=0, [2]=2combo=100, etc.)")]
        public int[] comboBonusScores = { 0, 0, 100, 150, 200, 250, 300, 350, 400, 450, 500 };

        [Tooltip("Garbage score bonus per chain level (index = chain count, e.g. [1]=1chain=0, [2]=2chain=100, etc.)")]
        public int[] chainBonusScores = { 0, 0, 100, 200, 300, 400, 500, 600, 700, 800, 900 };

        [Header("Garbage Block Costs")]
        [Tooltip("Available garbage block sizes and their costs (sorted by cost descending for greedy algorithm)")]
        public List<GarbageBlockCost> garbageBlockCosts = new List<GarbageBlockCost>();

        [Header("Grid References")]
        [Tooltip("All grids participating in VS mode")]
        public List<GridManager> grids = new List<GridManager>();

        [Header("Debug")]
        public bool logGarbageEvents = true;

        #endregion

        #region Events

        /// <summary>
        /// Fired when garbage is sent from one player to another.
        /// Parameters: senderIndex, targetIndex, garbageAmount
        /// </summary>
        public event Action<int, int, int> OnGarbageSent;

        /// <summary>
        /// Fired when garbage is countered.
        /// Parameters: playerIndex, amountCountered, amountRemaining
        /// </summary>
        public event Action<int, int, int> OnGarbageCountered;

        /// <summary>
        /// Fired when garbage is received by a player.
        /// Parameters: playerIndex, garbageAmount
        /// </summary>
        public event Action<int, int> OnGarbageReceived;

        #endregion

        #region Private Fields

        // Track pending garbage score for each player (waiting to be sent)
        private int[] _pendingOutgoingScore;

        // Track incoming garbage score for each player (can be countered)
        private int[] _pendingIncomingScore;

        // Current target for sequential mode
        private int _sequentialTargetIndex = 0;

        // Track combo match data for each player
        private ComboMatchData[] _comboData;

        // Track if a player is currently in an active combo/chain
        private bool[] _isInActiveCombo;

        // Random for random targeting mode
        private System.Random _random;

        // Match processor references for event subscription
        private MatchProcessor[] _matchProcessors;

        #endregion

        #region Properties

        /// <summary>
        /// Number of grids in this VS session.
        /// </summary>
        public int GridCount => grids.Count;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Debug.Log($"[GarbageRouter] Start() called. grids.Count = {grids.Count}, logGarbageEvents = {logGarbageEvents}");
            
            // Initialize after a frame delay to ensure GridManagers have finished their Start() initialization
            if (grids.Count > 0)
            {
                StartCoroutine(InitializeDelayed());
            }
            else
            {
                Debug.LogWarning("[GarbageRouter] No grids assigned! Please assign grids in the Inspector.");
            }
        }

        private System.Collections.IEnumerator InitializeDelayed()
        {
            // Wait one frame for all GridManagers to finish Start()
            yield return null;
            
            Initialize();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the garbage router with the current grid list.
        /// </summary>
        public void Initialize()
        {
            int count = grids.Count;

            _pendingOutgoingScore = new int[count];
            _pendingIncomingScore = new int[count];
            _comboData = new ComboMatchData[count];
            _isInActiveCombo = new bool[count];
            _matchProcessors = new MatchProcessor[count];

            // Initialize combo data for each player
            for (int i = 0; i < count; i++)
            {
                _comboData[i] = new ComboMatchData();
            }

            _random = new System.Random();

            // Initialize default garbage block costs if list is empty
            if (garbageBlockCosts.Count == 0)
            {
                InitializeDefaultGarbageBlockCosts();
            }

            // Sort garbage block costs by cost descending (for greedy algorithm)
            garbageBlockCosts.Sort((a, b) => b.cost.CompareTo(a.cost));
            
            // Subscribe to each grid's match events
            for (int i = 0; i < count; i++)
            {
                if (grids[i] == null) continue;
                
                // Get ScoreManager directly from GridManager (preferred) or via MatchProcessor (fallback)
                var scoreManager = grids[i].scoreManager ?? grids[i].matchProcessor?.scoreManager;
                
                if (logGarbageEvents)
                {
                    Debug.Log($"[GarbageRouter] Registering grid {i}: {grids[i].name} at position {grids[i].transform.position}");
                    Debug.Log($"[GarbageRouter]   GarbageManager: {grids[i].garbageManager?.GetInstanceID()}");
                    Debug.Log($"[GarbageRouter]   ScoreManager: {scoreManager?.GetInstanceID()}");
                }
                
                _matchProcessors[i] = grids[i].matchProcessor;
                
                // Subscribe to score manager events for combo/chain tracking
                if (scoreManager != null)
                {
                    int playerIndex = i; // Capture for closure
                    scoreManager.OnComboStarted += () => HandleComboStarted(playerIndex);
                    scoreManager.OnComboEnded += (combo, chain) => HandleComboEnded(playerIndex, combo, chain);
                    scoreManager.OnMatchScored += (tiles, combo, chain) => HandleMatchScored(playerIndex, tiles, combo, chain);
                    
                    if (logGarbageEvents)
                        Debug.Log($"[GarbageRouter] Subscribed to ScoreManager events for player {playerIndex}");
                }
                else
                {
                    Debug.LogWarning($"[GarbageRouter] No ScoreManager found for grid {i}: {grids[i].name}");
                }
            }
            
            if (logGarbageEvents)
                Debug.Log($"[GarbageRouter] Initialized with {count} grids");
        }

        /// <summary>
        /// Add a grid to the router.
        /// </summary>
        public void AddGrid(GridManager grid)
        {
            if (grid == null || grids.Contains(grid)) return;
            
            grids.Add(grid);
            
            // Reinitialize arrays
            Initialize();
        }

        /// <summary>
        /// Remove a grid from the router.
        /// </summary>
        public void RemoveGrid(GridManager grid)
        {
            int index = grids.IndexOf(grid);
            if (index < 0) return;
            
            grids.RemoveAt(index);
            Initialize();
        }

        private void UnsubscribeFromEvents()
        {
            // Note: In a full implementation, you'd store the delegates to unsubscribe properly
            // For now, the objects will be destroyed together
        }

        /// <summary>
        /// Initialize default garbage block costs with values up to 6x6.
        /// </summary>
        private void InitializeDefaultGarbageBlockCosts()
        {
            // Default Garbage Block Costs (sorted by cost descending)
            garbageBlockCosts.Add(new GarbageBlockCost(6, 6, 10000)); // XXL
            garbageBlockCosts.Add(new GarbageBlockCost(6, 5, 8000));
            garbageBlockCosts.Add(new GarbageBlockCost(6, 4, 7000));
            garbageBlockCosts.Add(new GarbageBlockCost(6, 3, 6000));
            garbageBlockCosts.Add(new GarbageBlockCost(6, 2, 4000));
            garbageBlockCosts.Add(new GarbageBlockCost(6, 1, 1500));
            garbageBlockCosts.Add(new GarbageBlockCost(5, 2, 1000));
            garbageBlockCosts.Add(new GarbageBlockCost(4, 2, 800));
            garbageBlockCosts.Add(new GarbageBlockCost(3, 2, 700));
            garbageBlockCosts.Add(new GarbageBlockCost(5, 1, 600));
            garbageBlockCosts.Add(new GarbageBlockCost(2, 2, 500));
            garbageBlockCosts.Add(new GarbageBlockCost(1, 2, 400));
            garbageBlockCosts.Add(new GarbageBlockCost(1, 1, 250)); // Tiny

            Debug.Log($"[GarbageRouter] Initialized {garbageBlockCosts.Count} default garbage block costs");
        }

        #endregion

        #region Event Handlers

        private void HandleComboStarted(int playerIndex)
        {
            _isInActiveCombo[playerIndex] = true;

            // Reset combo data
            _comboData[playerIndex].matchSizes.Clear();
            _comboData[playerIndex].maxChain = 0;

            if (logGarbageEvents)
                Debug.Log($"[GarbageRouter] Player {playerIndex} started combo");
        }

        private void HandleComboEnded(int playerIndex, int totalCombo, int maxChain)
        {
            _isInActiveCombo[playerIndex] = false;

            // Calculate garbage score from the entire combo
            int garbageScore = CalculateGarbageScore(playerIndex, totalCombo, maxChain);

            if (garbageScore > 0)
            {
                // First try to counter incoming garbage
                if (allowCountering && _pendingIncomingScore[playerIndex] > 0)
                {
                    int countered = Mathf.Min(garbageScore, _pendingIncomingScore[playerIndex]);
                    _pendingIncomingScore[playerIndex] -= countered;
                    garbageScore -= countered;

                    OnGarbageCountered?.Invoke(playerIndex, countered, _pendingIncomingScore[playerIndex]);

                    if (logGarbageEvents)
                        Debug.Log($"[GarbageRouter] Player {playerIndex} countered {countered} score, {_pendingIncomingScore[playerIndex]} remaining");
                }

                // Send remaining garbage
                if (garbageScore > 0)
                {
                    StartCoroutine(SendGarbageDelayed(playerIndex, garbageScore));

                    if (logGarbageEvents)
                        Debug.Log($"[GarbageRouter] Player {playerIndex} sending {garbageScore} garbage score");
                }
            }

            // Clear combo data
            _comboData[playerIndex].matchSizes.Clear();
            _comboData[playerIndex].maxChain = 0;

            if (logGarbageEvents)
                Debug.Log($"[GarbageRouter] Player {playerIndex} ended combo (combo:{totalCombo}, chain:{maxChain}, score:{garbageScore})");
        }

        private void HandleMatchScored(int playerIndex, int tilesMatched, int comboStep, int chainLevel)
        {
            if (logGarbageEvents)
            {
                Debug.Log($"[GarbageRouter] HandleMatchScored - Player {playerIndex} ({grids[playerIndex]?.name}) matched {tilesMatched} tiles, combo:{comboStep}, chain:{chainLevel}");
            }

            // Track match size
            _comboData[playerIndex].matchSizes.Add(tilesMatched);

            // Track max chain level
            if (chainLevel > _comboData[playerIndex].maxChain)
            {
                _comboData[playerIndex].maxChain = chainLevel;
            }
        }

        #endregion

        #region Garbage Calculation

        /// <summary>
        /// Calculate total garbage score from all matches in a combo.
        /// </summary>
        private int CalculateGarbageScore(int playerIndex, int totalCombo, int maxChain)
        {
            var comboData = _comboData[playerIndex];
            int totalScore = 0;

            // Calculate score from each match
            int matchScore = 0;
            foreach (int matchSize in comboData.matchSizes)
            {
                int score = GetMatchSizeScore(matchSize);
                matchScore += score;

                if (logGarbageEvents)
                    Debug.Log($"[GarbageRouter] Match of {matchSize} tiles = {score} score");
            }
            totalScore += matchScore;

            // Add combo bonus
            int comboBonus = GetComboBonusScore(totalCombo);
            totalScore += comboBonus;

            // Add chain bonus
            int chainBonus = GetChainBonusScore(maxChain);
            totalScore += chainBonus;

            if (logGarbageEvents)
            {
                Debug.Log($"[GarbageRouter] Total Garbage Score for Player {playerIndex}:");
                Debug.Log($"  - Match Score: {matchScore} (from {comboData.matchSizes.Count} matches)");
                Debug.Log($"  - Combo Bonus: {comboBonus} (combo x{totalCombo})");
                Debug.Log($"  - Chain Bonus: {chainBonus} (chain x{maxChain})");
                Debug.Log($"  - TOTAL: {totalScore}");
            }

            return totalScore;
        }

        /// <summary>
        /// Get garbage score for a specific match size.
        /// </summary>
        private int GetMatchSizeScore(int matchSize)
        {
            if (matchSize < 0 || matchSize >= matchSizeScores.Length)
            {
                // Default to last value for oversized matches
                if (matchSizeScores.Length > 0)
                    return matchSizeScores[matchSizeScores.Length - 1];
                return 0;
            }

            return matchSizeScores[matchSize];
        }

        /// <summary>
        /// Get garbage score bonus for combo count.
        /// </summary>
        private int GetComboBonusScore(int comboCount)
        {
            if (comboCount < 0 || comboCount >= comboBonusScores.Length)
            {
                // Default to last value for high combos
                if (comboBonusScores.Length > 0)
                    return comboBonusScores[comboBonusScores.Length - 1];
                return 0;
            }

            return comboBonusScores[comboCount];
        }

        /// <summary>
        /// Get garbage score bonus for chain level.
        /// </summary>
        private int GetChainBonusScore(int chainLevel)
        {
            if (chainLevel < 0 || chainLevel >= chainBonusScores.Length)
            {
                // Default to last value for high chains
                if (chainBonusScores.Length > 0)
                    return chainBonusScores[chainBonusScores.Length - 1];
                return 0;
            }

            return chainBonusScores[chainLevel];
        }

        /// <summary>
        /// Convert garbage score to list of garbage blocks using greedy algorithm.
        /// Buys the most expensive blocks first, then continues with remaining score.
        /// </summary>
        private List<(int width, int height)> ConvertScoreToBlocks(int score)
        {
            var blocks = new List<(int width, int height)>();
            int remainingScore = score;

            if (logGarbageEvents)
                Debug.Log($"[GarbageRouter] Converting {score} score to garbage blocks...");

            // Greedy algorithm: buy most expensive blocks first
            foreach (var blockCost in garbageBlockCosts)
            {
                // Skip disabled blocks (cost = 0)
                if (blockCost.cost <= 0) continue;

                // Buy as many of this block as we can afford
                while (remainingScore >= blockCost.cost)
                {
                    blocks.Add((blockCost.width, blockCost.height));
                    remainingScore -= blockCost.cost;

                    if (logGarbageEvents)
                        Debug.Log($"[GarbageRouter]   Bought {blockCost.width}x{blockCost.height} for {blockCost.cost}, remaining: {remainingScore}");
                }
            }

            if (remainingScore > 0 && logGarbageEvents)
            {
                Debug.Log($"[GarbageRouter]   Leftover score: {remainingScore} (wasted)");
            }

            if (logGarbageEvents)
                Debug.Log($"[GarbageRouter]   Total blocks spawned: {blocks.Count}");

            return blocks;
        }

        #endregion

        #region Garbage Routing

        private System.Collections.IEnumerator SendGarbageDelayed(int senderIndex, int amount)
        {
            yield return new WaitForSeconds(garbageSendDelay);
            
            SendGarbage(senderIndex, amount);
        }

        /// <summary>
        /// Send garbage from one player to opponents based on targeting mode.
        /// </summary>
        public void SendGarbage(int senderIndex, int amount)
        {
            if (amount <= 0) return;
            if (grids.Count < 2) return;
            
            List<int> targets = DetermineTargets(senderIndex);
            
            if (targets.Count == 0) return;
            
            switch (targetingMode)
            {
                case TargetingMode.SplitEvenly:
                    // Divide garbage among targets
                    int perTarget = amount / targets.Count;
                    int remainder = amount % targets.Count;
                    
                    for (int i = 0; i < targets.Count; i++)
                    {
                        int targetAmount = perTarget + (i < remainder ? 1 : 0);
                        if (targetAmount > 0)
                        {
                            QueueGarbageForPlayer(senderIndex, targets[i], targetAmount);
                        }
                    }
                    break;
                    
                case TargetingMode.AllOpponents:
                    // Full amount to all targets
                    foreach (int target in targets)
                    {
                        QueueGarbageForPlayer(senderIndex, target, amount);
                    }
                    break;
                    
                default:
                    // Single target modes (Sequential, Random, LowestStack, HighestStack)
                    if (targets.Count > 0)
                    {
                        QueueGarbageForPlayer(senderIndex, targets[0], amount);
                    }
                    break;
            }
            
            // Update sequential target for next time
            if (targetingMode == TargetingMode.Sequential)
            {
                _sequentialTargetIndex = (_sequentialTargetIndex + 1) % grids.Count;
                if (_sequentialTargetIndex == senderIndex)
                    _sequentialTargetIndex = (_sequentialTargetIndex + 1) % grids.Count;
            }
        }

        private List<int> DetermineTargets(int senderIndex)
        {
            var targets = new List<int>();
            
            switch (targetingMode)
            {
                case TargetingMode.Sequential:
                    int target = _sequentialTargetIndex;
                    if (target == senderIndex)
                        target = (target + 1) % grids.Count;
                    if (grids[target] != null)
                        targets.Add(target);
                    break;
                    
                case TargetingMode.Random:
                    var validTargets = new List<int>();
                    for (int i = 0; i < grids.Count; i++)
                    {
                        if (i != senderIndex && grids[i] != null)
                            validTargets.Add(i);
                    }
                    if (validTargets.Count > 0)
                        targets.Add(validTargets[_random.Next(validTargets.Count)]);
                    break;
                    
                case TargetingMode.LowestStack:
                    targets.Add(FindPlayerWithLowestStack(senderIndex));
                    break;
                    
                case TargetingMode.HighestStack:
                    targets.Add(FindPlayerWithHighestStack(senderIndex));
                    break;
                    
                case TargetingMode.SplitEvenly:
                case TargetingMode.AllOpponents:
                    for (int i = 0; i < grids.Count; i++)
                    {
                        if (i != senderIndex && grids[i] != null)
                            targets.Add(i);
                    }
                    break;
            }
            
            // Remove invalid targets
            targets.RemoveAll(t => t < 0 || t >= grids.Count || grids[t] == null);
            
            return targets;
        }

        private int FindPlayerWithLowestStack(int excludeIndex)
        {
            int lowestIndex = -1;
            int lowestHeight = int.MaxValue;
            
            for (int i = 0; i < grids.Count; i++)
            {
                if (i == excludeIndex || grids[i] == null) continue;
                
                int height = GetStackHeight(grids[i]);
                if (height < lowestHeight)
                {
                    lowestHeight = height;
                    lowestIndex = i;
                }
            }
            
            return lowestIndex;
        }

        private int FindPlayerWithHighestStack(int excludeIndex)
        {
            int highestIndex = -1;
            int highestHeight = -1;
            
            for (int i = 0; i < grids.Count; i++)
            {
                if (i == excludeIndex || grids[i] == null) continue;
                
                int height = GetStackHeight(grids[i]);
                if (height > highestHeight)
                {
                    highestHeight = height;
                    highestIndex = i;
                }
            }
            
            return highestIndex;
        }

        private int GetStackHeight(GridManager grid)
        {
            var gridArray = grid.Grid;
            int width = grid.Width;
            int height = grid.Height;
            int maxY = 0;
            
            for (int x = 0; x < width; x++)
            {
                for (int y = height - 1; y >= 0; y--)
                {
                    if (gridArray[x, y] != null)
                    {
                        maxY = Mathf.Max(maxY, y);
                        break;
                    }
                }
            }
            
            return maxY;
        }

        private void QueueGarbageForPlayer(int senderIndex, int targetIndex, int score)
        {
            if (targetIndex < 0 || targetIndex >= grids.Count) return;
            if (grids[targetIndex] == null) return;

            // Add to pending incoming score (can be countered)
            _pendingIncomingScore[targetIndex] += score;

            OnGarbageSent?.Invoke(senderIndex, targetIndex, score);

            if (logGarbageEvents)
            {
                Debug.Log($"[GarbageRouter] Player {senderIndex} -> Player {targetIndex}: {score} garbage score queued");
                Debug.Log($"[GarbageRouter] Target grid: {grids[targetIndex].name}, GarbageManager: {grids[targetIndex].garbageManager?.GetInstanceID()}");
            }

            // If the target is not in an active combo, deliver immediately
            if (!_isInActiveCombo[targetIndex])
            {
                DeliverPendingGarbage(targetIndex);
            }
        }

        private void DeliverPendingGarbage(int playerIndex)
        {
            int score = _pendingIncomingScore[playerIndex];
            if (score <= 0) return;

            _pendingIncomingScore[playerIndex] = 0;

            var targetGrid = grids[playerIndex];
            var garbageManager = targetGrid?.garbageManager;
            if (garbageManager == null) return;

            if (logGarbageEvents)
            {
                Debug.Log($"[GarbageRouter] Delivering {score} garbage score to Player {playerIndex}");
                Debug.Log($"[GarbageRouter] Target grid name: {targetGrid.name}");
                Debug.Log($"[GarbageRouter] Target grid position: {targetGrid.transform.position}");
                Debug.Log($"[GarbageRouter] GarbageManager instance: {garbageManager.GetInstanceID()}");
            }

            // Convert score to garbage blocks using greedy algorithm
            var blocks = ConvertScoreToBlocks(score);

            // Queue each block to the garbage manager
            foreach (var (width, height) in blocks)
            {
                garbageManager.QueueGarbage(width, height);
            }

            OnGarbageReceived?.Invoke(playerIndex, score);

            if (logGarbageEvents)
                Debug.Log($"[GarbageRouter] Delivered {blocks.Count} garbage blocks (from {score} score) to Player {playerIndex}");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get pending incoming garbage score for a player (can be countered).
        /// </summary>
        public int GetPendingIncomingGarbage(int playerIndex)
        {
            if (_pendingIncomingScore == null || playerIndex < 0 || playerIndex >= _pendingIncomingScore.Length)
                return 0;
            return _pendingIncomingScore[playerIndex];
        }

        /// <summary>
        /// Get current match count during active combo for a player.
        /// </summary>
        public int GetPendingOutgoingGarbage(int playerIndex)
        {
            if (_comboData == null || playerIndex < 0 || playerIndex >= _comboData.Length)
                return 0;
            return _comboData[playerIndex].matchSizes.Count;
        }

        /// <summary>
        /// Get current chain level for a player.
        /// </summary>
        public int GetCurrentChainLevel(int playerIndex)
        {
            if (_comboData == null || playerIndex < 0 || playerIndex >= _comboData.Length)
                return 0;
            return _comboData[playerIndex].maxChain;
        }

        /// <summary>
        /// Check if a player is currently in an active combo.
        /// </summary>
        public bool IsPlayerInCombo(int playerIndex)
        {
            if (_isInActiveCombo == null || playerIndex < 0 || playerIndex >= _isInActiveCombo.Length)
                return false;
            return _isInActiveCombo[playerIndex];
        }

        /// <summary>
        /// Force deliver all pending garbage to a player (e.g., when their combo times out).
        /// </summary>
        public void ForceDeliverGarbage(int playerIndex)
        {
            DeliverPendingGarbage(playerIndex);
        }

        /// <summary>
        /// Manually send garbage score from one player to another (for testing or special mechanics).
        /// </summary>
        public void ManualSendGarbage(int senderIndex, int targetIndex, int score)
        {
            QueueGarbageForPlayer(senderIndex, targetIndex, score);

            if (!_isInActiveCombo[targetIndex])
            {
                DeliverPendingGarbage(targetIndex);
            }
        }

        /// <summary>
        /// Clear all pending garbage for a player.
        /// </summary>
        public void ClearPendingGarbage(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < _pendingIncomingScore.Length)
            {
                _pendingIncomingScore[playerIndex] = 0;
                _comboData[playerIndex].matchSizes.Clear();
                _comboData[playerIndex].maxChain = 0;
            }
        }

        #endregion

        #region Debug

        private void OnGUI()
        {
            if (!logGarbageEvents) return;
            if (_pendingIncomingScore == null) return; // Not initialized yet

            GUILayout.BeginArea(new Rect(10, Screen.height - 180, 350, 170));
            GUILayout.BeginVertical("box");

            GUILayout.Label("<b>Garbage Router (Score System)</b>");
            GUILayout.Label($"Mode: {targetingMode}");

            for (int i = 0; i < grids.Count && i < _pendingIncomingScore.Length; i++)
            {
                string status = _isInActiveCombo[i] ? " [COMBO]" : "";
                int matches = _comboData[i].matchSizes.Count;
                int chain = _comboData[i].maxChain;
                GUILayout.Label($"P{i}: InScore:{_pendingIncomingScore[i]} Matches:{matches} Chain:{chain}{status}");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        #endregion
    }
}