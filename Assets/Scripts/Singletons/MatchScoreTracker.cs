using System;
using System.Collections.Generic;
using UnityEngine;
using PuzzleAttack.Grid;

namespace PuzzleAttack
{
    /// <summary>
    /// Tracks scores for all grids in a match and manages VS match state.
    /// Monitors eliminations and determines winners.
    /// </summary>
    public class MatchScoreTracker : MonoBehaviour
    {
        #region Singleton

        private static MatchScoreTracker _instance;
        public static MatchScoreTracker Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MatchScoreTracker>();
                }
                return _instance;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Fired when a player is eliminated (their grid topped out).
        /// Parameters: playerIndex, placement (e.g., 4th place in 4-player = 4)
        /// </summary>
        public event Action<int, int> OnPlayerEliminated;

        /// <summary>
        /// Fired when there's a winner in VS mode (only 1 player remaining).
        /// Parameters: winnerIndex, isHumanPlayer
        /// </summary>
        public event Action<int, bool> OnMatchWinner;

        /// <summary>
        /// Fired when Marathon/single-player game ends (player eliminated).
        /// </summary>
        public event Action OnMarathonGameOver;

        /// <summary>
        /// Fired when all players are eliminated (draw scenario, shouldn't happen normally).
        /// </summary>
        public event Action OnMatchDraw;

        /// <summary>
        /// Fired when a score is registered/updated.
        /// Parameters: playerIndex, newScore
        /// </summary>
        public event Action<int, int> OnScoreUpdated;

        #endregion

        #region Private Fields

        private Dictionary<int, PlayerMatchData> _playerData = new Dictionary<int, PlayerMatchData>();
        private List<int> _eliminationOrder = new List<int>(); // First eliminated = index 0
        private int _totalPlayers;
        private bool _matchEnded;
        private int _winnerIndex = -1;

        #endregion

        #region Properties

        public int TotalPlayers => _totalPlayers;
        public int RemainingPlayers => _totalPlayers - _eliminationOrder.Count;
        public bool MatchEnded => _matchEnded;
        public int WinnerIndex => _winnerIndex;
        public IReadOnlyList<int> EliminationOrder => _eliminationOrder;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the tracker for a new match.
        /// </summary>
        public void InitializeMatch(int playerCount)
        {
            _playerData.Clear();
            _eliminationOrder.Clear();
            _totalPlayers = playerCount;
            _matchEnded = false;
            _winnerIndex = -1;

            Debug.Log($"[MatchScoreTracker] Initialized for {playerCount} players");
        }

        /// <summary>
        /// Register a player's ScoreManager with the tracker.
        /// </summary>
        public void RegisterPlayer(int playerIndex, ScoreManager scoreManager, GridManager gridManager, bool isHuman)
        {
            if (_playerData.ContainsKey(playerIndex))
            {
                Debug.LogWarning($"[MatchScoreTracker] Player {playerIndex} already registered, updating...");
            }

            var data = new PlayerMatchData
            {
                PlayerIndex = playerIndex,
                ScoreManager = scoreManager,
                GridManager = gridManager,
                IsHuman = isHuman,
                IsEliminated = false,
                FinalScore = 0,
                FinalCombo = 0,
                FinalChain = 0,
                Placement = 0
            };

            _playerData[playerIndex] = data;

            // Subscribe to score updates
            if (scoreManager != null)
            {
                scoreManager.OnMatchScored += (tiles, combo, chain) => HandleScoreUpdate(playerIndex);
            }

            Debug.Log($"[MatchScoreTracker] Registered player {playerIndex} (Human: {isHuman})");
        }

        #endregion

        #region Player Elimination

        /// <summary>
        /// Called when a player's grid tops out.
        /// </summary>
        public void EliminatePlayer(int playerIndex)
        {
            if (_matchEnded)
            {
                Debug.Log($"[MatchScoreTracker] Match already ended, ignoring elimination of player {playerIndex}");
                return;
            }

            if (!_playerData.TryGetValue(playerIndex, out var data))
            {
                Debug.LogError($"[MatchScoreTracker] Cannot eliminate unknown player {playerIndex}");
                return;
            }

            if (data.IsEliminated)
            {
                Debug.Log($"[MatchScoreTracker] Player {playerIndex} already eliminated");
                return;
            }

            // Mark as eliminated
            data.IsEliminated = true;
            _eliminationOrder.Add(playerIndex);

            // Calculate placement (last eliminated = 2nd place in 2-player, etc.)
            int placement = _totalPlayers - _eliminationOrder.Count + 1;
            data.Placement = placement;

            // Store final stats
            if (data.ScoreManager != null)
            {
                data.FinalScore = data.ScoreManager.GetScore();
                data.FinalCombo = data.ScoreManager.GetHighestCombo();
                data.FinalChain = data.ScoreManager.GetHighestChain();
            }

            Debug.Log($"[MatchScoreTracker] Player {playerIndex} eliminated! Placement: {placement}, Remaining: {RemainingPlayers}");

            // Fire elimination event
            OnPlayerEliminated?.Invoke(playerIndex, placement);

            // Check for match end
            CheckMatchEnd();
        }

        /// <summary>
        /// Check if the match should end (1 or 0 players remaining).
        /// </summary>
        private void CheckMatchEnd()
        {
            if (_matchEnded) return;

            var gameMode = GameModeManager.Instance?.CurrentMode;
            bool isMarathon = gameMode?.modeType == GameModeType.Marathon;

            // Also check if we only had 1 player to begin with (test config marathon)
            bool isSinglePlayer = _totalPlayers == 1;

            if (isMarathon || isSinglePlayer)
            {
                // Marathon/Single player: game ends when the player is eliminated
                // No winner, just game over
                EndMatchMarathon();
            }
            else
            {
                // VS modes: game ends when 1 player remains
                if (RemainingPlayers <= 1)
                {
                    // Find the winner (non-eliminated player)
                    int winnerIdx = -1;
                    foreach (var kvp in _playerData)
                    {
                        if (!kvp.Value.IsEliminated)
                        {
                            winnerIdx = kvp.Key;
                            break;
                        }
                    }

                    if (winnerIdx >= 0)
                    {
                        // We have a winner!
                        var winnerData = _playerData[winnerIdx];
                        winnerData.Placement = 1;

                        // Store final stats for winner
                        if (winnerData.ScoreManager != null)
                        {
                            winnerData.FinalScore = winnerData.ScoreManager.GetScore();
                            winnerData.FinalCombo = winnerData.ScoreManager.GetHighestCombo();
                            winnerData.FinalChain = winnerData.ScoreManager.GetHighestChain();
                        }

                        EndMatchWithWinner(winnerIdx);
                    }
                    else if (RemainingPlayers == 0)
                    {
                        // Everyone eliminated at once? (shouldn't happen)
                        Debug.LogWarning("[MatchScoreTracker] All players eliminated - draw!");
                        _matchEnded = true;
                        OnMatchDraw?.Invoke();
                    }
                }
            }
        }

        private void EndMatchMarathon()
        {
            _matchEnded = true;
            _winnerIndex = -1;

            Debug.Log("[MatchScoreTracker] Marathon match ended (game over)");
            
            // Fire marathon-specific event
            OnMarathonGameOver?.Invoke();
        }

        private void EndMatchWithWinner(int winnerIndex)
        {
            _matchEnded = true;
            _winnerIndex = winnerIndex;

            if (_playerData.TryGetValue(winnerIndex, out var winnerData))
            {
                Debug.Log($"[MatchScoreTracker] VS match ended! Winner: Player {winnerIndex} (Human: {winnerData.IsHuman})");
                OnMatchWinner?.Invoke(winnerIndex, winnerData.IsHuman);
            }
        }

        #endregion

        #region Score Tracking

        private void HandleScoreUpdate(int playerIndex)
        {
            if (!_playerData.TryGetValue(playerIndex, out var data)) return;
            if (data.ScoreManager == null) return;

            int currentScore = data.ScoreManager.GetScore();
            OnScoreUpdated?.Invoke(playerIndex, currentScore);
        }

        #endregion

        #region Public Queries

        /// <summary>
        /// Get data for a specific player.
        /// </summary>
        public PlayerMatchData GetPlayerData(int playerIndex)
        {
            return _playerData.TryGetValue(playerIndex, out var data) ? data : null;
        }

        /// <summary>
        /// Get all player data.
        /// </summary>
        public IEnumerable<PlayerMatchData> GetAllPlayerData()
        {
            return _playerData.Values;
        }

        /// <summary>
        /// Get data for all human players (for high score saving).
        /// </summary>
        public IEnumerable<PlayerMatchData> GetHumanPlayerData()
        {
            foreach (var data in _playerData.Values)
            {
                if (data.IsHuman)
                    yield return data;
            }
        }

        /// <summary>
        /// Check if a specific player is eliminated.
        /// </summary>
        public bool IsPlayerEliminated(int playerIndex)
        {
            return _playerData.TryGetValue(playerIndex, out var data) && data.IsEliminated;
        }

        /// <summary>
        /// Get current score for a player.
        /// </summary>
        public int GetPlayerScore(int playerIndex)
        {
            if (_playerData.TryGetValue(playerIndex, out var data))
            {
                if (data.IsEliminated)
                    return data.FinalScore;
                if (data.ScoreManager != null)
                    return data.ScoreManager.GetScore();
            }
            return 0;
        }

        /// <summary>
        /// Get the winner's data (null if no winner yet or marathon mode).
        /// </summary>
        public PlayerMatchData GetWinnerData()
        {
            if (_winnerIndex >= 0 && _playerData.TryGetValue(_winnerIndex, out var data))
                return data;
            return null;
        }

        /// <summary>
        /// Get all player data sorted by placement (1st, 2nd, 3rd, 4th).
        /// </summary>
        public List<PlayerMatchData> GetResultsSortedByPlacement()
        {
            var results = new List<PlayerMatchData>(_playerData.Values);
            results.Sort((a, b) => a.Placement.CompareTo(b.Placement));
            return results;
        }

        #endregion

        #region High Score Saving

        /// <summary>
        /// Save high scores for human players only.
        /// Call this when the match ends.
        /// </summary>
        public void SaveHumanHighScores()
        {
            if (HighScoreManager.Instance == null)
            {
                Debug.LogWarning("[MatchScoreTracker] HighScoreManager not found, cannot save scores");
                return;
            }

            foreach (var data in _playerData.Values)
            {
                if (!data.IsHuman) continue;

                int score = data.IsEliminated ? data.FinalScore : data.ScoreManager?.GetScore() ?? 0;
                int combo = data.IsEliminated ? data.FinalCombo : data.ScoreManager?.GetHighestCombo() ?? 0;
                int speedLevel = data.ScoreManager?.GetSpeedLevel() ?? 1;

                if (score > 0)
                {
                    int rank = HighScoreManager.Instance.AddScore(score, combo, speedLevel);
                    if (rank > 0)
                    {
                        Debug.Log($"[MatchScoreTracker] Player {data.PlayerIndex} score {score} ranked #{rank}");
                    }
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Data container for a player's match statistics.
    /// </summary>
    [System.Serializable]
    public class PlayerMatchData
    {
        public int PlayerIndex;
        public ScoreManager ScoreManager;
        public GridManager GridManager;
        public bool IsHuman;
        public bool IsEliminated;
        public int Placement; // 1 = winner, 2 = second, etc.
        
        // Final stats (frozen when eliminated or match ends)
        public int FinalScore;
        public int FinalCombo;
        public int FinalChain;

        /// <summary>
        /// Get display name for this player.
        /// </summary>
        public string GetDisplayName()
        {
            if (IsHuman)
                return $"Player {PlayerIndex + 1}";
            else
                return $"CPU {PlayerIndex + 1}";
        }
    }
}