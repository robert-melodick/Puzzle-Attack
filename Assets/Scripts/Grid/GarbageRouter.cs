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
        #region Enums

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

        #endregion

        #region Inspector Fields

        [Header("Routing Settings")]
        [Tooltip("How garbage is distributed to opponents")]
        public TargetingMode targetingMode = TargetingMode.Sequential;

        [Tooltip("Delay before garbage is sent after a combo/chain ends")]
        public float garbageSendDelay = 0.5f;

        [Tooltip("If true, pending garbage can be countered by making matches")]
        public bool allowCountering = true;

        [Header("Garbage Scaling")]
        [Tooltip("Base garbage lines for a 4-match")]
        public int baseGarbageFor4Match = 1;
        
        [Tooltip("Base garbage lines for a 5-match")]
        public int baseGarbageFor5Match = 2;
        
        [Tooltip("Base garbage lines for a 6+ match")]
        public int baseGarbageFor6Match = 3;

        [Tooltip("Garbage multiplier per chain level (chain 2 = 1x, chain 3 = 2x, etc.)")]
        public int[] chainGarbageBonus = { 0, 0, 1, 2, 4, 6, 8, 10 };

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

        // Track pending garbage for each player (waiting to be sent)
        private int[] _pendingOutgoingGarbage;
        
        // Track incoming garbage for each player (can be countered)
        private int[] _pendingIncomingGarbage;
        
        // Current target for sequential mode
        private int _sequentialTargetIndex = 0;
        
        // Track current chain level for each player
        private int[] _currentChainLevel;
        
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
            
            _pendingOutgoingGarbage = new int[count];
            _pendingIncomingGarbage = new int[count];
            _currentChainLevel = new int[count];
            _isInActiveCombo = new bool[count];
            _matchProcessors = new MatchProcessor[count];
            
            _random = new System.Random();
            
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

        #endregion

        #region Event Handlers

        private void HandleComboStarted(int playerIndex)
        {
            _isInActiveCombo[playerIndex] = true;
            _currentChainLevel[playerIndex] = 1;
            
            if (logGarbageEvents)
                Debug.Log($"[GarbageRouter] Player {playerIndex} started combo");
        }

        private void HandleComboEnded(int playerIndex, int totalCombo, int maxChain)
        {
            _isInActiveCombo[playerIndex] = false;
            
            // Send accumulated garbage
            int garbageToSend = _pendingOutgoingGarbage[playerIndex];
            if (garbageToSend > 0)
            {
                StartCoroutine(SendGarbageDelayed(playerIndex, garbageToSend));
                _pendingOutgoingGarbage[playerIndex] = 0;
            }
            
            _currentChainLevel[playerIndex] = 0;
            
            if (logGarbageEvents)
                Debug.Log($"[GarbageRouter] Player {playerIndex} ended combo (combo:{totalCombo}, chain:{maxChain})");
        }

        private void HandleMatchScored(int playerIndex, int tilesMatched, int comboStep, int chainLevel)
        {
            _currentChainLevel[playerIndex] = chainLevel;
            
            if (logGarbageEvents)
            {
                Debug.Log($"[GarbageRouter] HandleMatchScored - Player {playerIndex} ({grids[playerIndex]?.name}) matched {tilesMatched} tiles, combo:{comboStep}, chain:{chainLevel}");
            }
            
            // Calculate garbage from this match
            int garbage = CalculateGarbage(tilesMatched, chainLevel);
            
            if (garbage > 0)
            {
                // First try to counter incoming garbage
                if (allowCountering && _pendingIncomingGarbage[playerIndex] > 0)
                {
                    int countered = Mathf.Min(garbage, _pendingIncomingGarbage[playerIndex]);
                    _pendingIncomingGarbage[playerIndex] -= countered;
                    garbage -= countered;
                    
                    OnGarbageCountered?.Invoke(playerIndex, countered, _pendingIncomingGarbage[playerIndex]);
                    
                    if (logGarbageEvents)
                        Debug.Log($"[GarbageRouter] Player {playerIndex} countered {countered} garbage, {_pendingIncomingGarbage[playerIndex]} remaining");
                }
                
                // Accumulate remaining garbage to send
                if (garbage > 0)
                {
                    _pendingOutgoingGarbage[playerIndex] += garbage;
                    
                    if (logGarbageEvents)
                        Debug.Log($"[GarbageRouter] Player {playerIndex} ({grids[playerIndex]?.name}) accumulated {garbage} garbage (total pending: {_pendingOutgoingGarbage[playerIndex]})");
                }
            }
        }

        #endregion

        #region Garbage Calculation

        /// <summary>
        /// Calculate garbage to send based on match size and chain level.
        /// </summary>
        public int CalculateGarbage(int tilesMatched, int chainLevel)
        {
            int baseGarbage = 0;
            
            // Base garbage from match size
            if (tilesMatched >= 6)
                baseGarbage = baseGarbageFor6Match;
            else if (tilesMatched >= 5)
                baseGarbage = baseGarbageFor5Match;
            else if (tilesMatched >= 4)
                baseGarbage = baseGarbageFor4Match;
            
            // Chain bonus
            int chainBonus = 0;
            if (chainLevel > 1 && chainLevel - 1 < chainGarbageBonus.Length)
            {
                chainBonus = chainGarbageBonus[chainLevel - 1];
            }
            else if (chainLevel >= chainGarbageBonus.Length)
            {
                // Cap at max defined bonus
                chainBonus = chainGarbageBonus[chainGarbageBonus.Length - 1];
            }
            
            return baseGarbage + chainBonus;
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

        private void QueueGarbageForPlayer(int senderIndex, int targetIndex, int amount)
        {
            if (targetIndex < 0 || targetIndex >= grids.Count) return;
            if (grids[targetIndex] == null) return;
            
            // Add to pending incoming (can be countered)
            _pendingIncomingGarbage[targetIndex] += amount;
            
            OnGarbageSent?.Invoke(senderIndex, targetIndex, amount);
            
            if (logGarbageEvents)
            {
                Debug.Log($"[GarbageRouter] Player {senderIndex} -> Player {targetIndex}: {amount} garbage queued");
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
            int amount = _pendingIncomingGarbage[playerIndex];
            if (amount <= 0) return;
            
            _pendingIncomingGarbage[playerIndex] = 0;
            
            var targetGrid = grids[playerIndex];
            var garbageManager = targetGrid?.garbageManager;
            if (garbageManager == null) return;
            
            if (logGarbageEvents)
            {
                Debug.Log($"[GarbageRouter] Delivering {amount} garbage to Player {playerIndex}");
                Debug.Log($"[GarbageRouter] Target grid name: {targetGrid.name}");
                Debug.Log($"[GarbageRouter] Target grid position: {targetGrid.transform.position}");
                Debug.Log($"[GarbageRouter] GarbageManager instance: {garbageManager.GetInstanceID()}");
            }
            
            // Convert garbage amount to actual garbage blocks
            // Standard: 1 "garbage line" = 1 row of width-sized garbage
            // But we can vary the width for variety
            DeliverGarbageBlocks(garbageManager, amount, targetGrid.Width);
            
            OnGarbageReceived?.Invoke(playerIndex, amount);
            
            if (logGarbageEvents)
                Debug.Log($"[GarbageRouter] Delivered {amount} garbage to Player {playerIndex}");
        }

        private void DeliverGarbageBlocks(GarbageManager garbageManager, int totalLines, int gridWidth)
        {
            // Strategy: 
            // - Large amounts (4+) become full-width tall blocks
            // - Smaller amounts become narrower blocks or multiple small blocks
            
            int remaining = totalLines;
            
            while (remaining > 0)
            {
                if (remaining >= 4)
                {
                    // Full-width 4-row garbage (the scary kind)
                    garbageManager.QueueGarbage(gridWidth, 4);
                    remaining -= 4;
                }
                else if (remaining >= 2)
                {
                    // Full-width 2-row garbage
                    garbageManager.QueueGarbage(gridWidth, 2);
                    remaining -= 2;
                }
                else
                {
                    // Single row, variable width
                    int width = Mathf.Max(3, gridWidth - 2); // Slightly narrower for 1-line garbage
                    garbageManager.QueueGarbage(width, 1);
                    remaining -= 1;
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get pending incoming garbage for a player (can be countered).
        /// </summary>
        public int GetPendingIncomingGarbage(int playerIndex)
        {
            if (_pendingIncomingGarbage == null || playerIndex < 0 || playerIndex >= _pendingIncomingGarbage.Length)
                return 0;
            return _pendingIncomingGarbage[playerIndex];
        }

        /// <summary>
        /// Get pending outgoing garbage for a player (accumulated during combo).
        /// </summary>
        public int GetPendingOutgoingGarbage(int playerIndex)
        {
            if (_pendingOutgoingGarbage == null || playerIndex < 0 || playerIndex >= _pendingOutgoingGarbage.Length)
                return 0;
            return _pendingOutgoingGarbage[playerIndex];
        }

        /// <summary>
        /// Get current chain level for a player.
        /// </summary>
        public int GetCurrentChainLevel(int playerIndex)
        {
            if (_currentChainLevel == null || playerIndex < 0 || playerIndex >= _currentChainLevel.Length)
                return 0;
            return _currentChainLevel[playerIndex];
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
        /// Manually send garbage from one player to another (for testing or special mechanics).
        /// </summary>
        public void ManualSendGarbage(int senderIndex, int targetIndex, int amount)
        {
            QueueGarbageForPlayer(senderIndex, targetIndex, amount);
            
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
            if (playerIndex >= 0 && playerIndex < _pendingIncomingGarbage.Length)
            {
                _pendingIncomingGarbage[playerIndex] = 0;
                _pendingOutgoingGarbage[playerIndex] = 0;
            }
        }

        #endregion

        #region Debug

        private void OnGUI()
        {
            if (!logGarbageEvents) return;
            if (_pendingIncomingGarbage == null) return; // Not initialized yet
            
            GUILayout.BeginArea(new Rect(10, Screen.height - 150, 300, 140));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("<b>Garbage Router</b>");
            GUILayout.Label($"Mode: {targetingMode}");
            
            for (int i = 0; i < grids.Count && i < _pendingIncomingGarbage.Length; i++)
            {
                string status = _isInActiveCombo[i] ? " [COMBO]" : "";
                GUILayout.Label($"P{i}: In:{_pendingIncomingGarbage[i]} Out:{_pendingOutgoingGarbage[i]} Chain:{_currentChainLevel[i]}{status}");
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        #endregion
    }
}