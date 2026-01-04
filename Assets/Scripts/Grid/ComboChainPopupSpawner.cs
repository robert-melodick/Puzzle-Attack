using UnityEngine;
using System.Collections.Generic;
using PuzzleAttack.Grid;

namespace PuzzleAttack.UI
{
    /// <summary>
    /// Spawns combo and chain popup labels at match locations.
    /// Attach to the same GameObject as GridManager or reference it.
    /// </summary>
    public class ComboChainPopupSpawner : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Prefab")]
        [SerializeField] private GameObject popupPrefab;

        [Header("References")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private ScoreManager scoreManager;

        [Header("Spawn Settings")]
        [Tooltip("Offset from match center position for combo popups")]
        [SerializeField] private Vector3 comboSpawnOffset = new Vector3(0.5f, 0.5f, -1f);

        [Tooltip("Offset from match center position for chain popups")]
        [SerializeField] private Vector3 chainSpawnOffset = new Vector3(1.5f, 1.0f, -1f);

        [Header("Display Settings")]
        [Tooltip("Minimum combo count to show popup (1 = show all)")]
        [SerializeField] private int minComboToShow = 2;
        
        [Tooltip("Minimum chain count to show popup (1 = show all)")]
        [SerializeField] private int minChainToShow = 2;

        [Tooltip("Show combo popups")]
        [SerializeField] private bool showComboPopups = true;
        
        [Tooltip("Show chain popups")]
        [SerializeField] private bool showChainPopups = true;

        #endregion

        #region Private Fields

        private Vector3 _lastMatchCenter;
        private List<Vector2Int> _currentMatchPositions = new List<Vector2Int>();

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Auto-find references if not assigned
            TryFindReferences();

            // Subscribe to score manager events
            TrySubscribeToEvents();
        }

        private void Update()
        {
            // Keep trying to find and subscribe if not yet connected (handles late initialization)
            if (scoreManager == null)
            {
                TryFindReferences();
                TrySubscribeToEvents();
            }
        }

        private void TryFindReferences()
        {
            if (gridManager == null)
            {
                gridManager = GetComponent<GridManager>();
                if (gridManager == null)
                {
                    gridManager = GetComponentInParent<GridManager>();
                }
            }

            if (scoreManager == null && gridManager != null)
            {
                scoreManager = gridManager.scoreManager;
            }
        }

        private bool _isSubscribed = false;

        private void TrySubscribeToEvents()
        {
            if (scoreManager != null && !_isSubscribed)
            {
                scoreManager.OnMatchScored += HandleMatchScored;
                scoreManager.OnChainIncreased += HandleChainIncreased;
                _isSubscribed = true;
                Debug.Log($"[ComboChainPopupSpawner] Successfully subscribed to ScoreManager events");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (scoreManager != null && _isSubscribed)
            {
                scoreManager.OnMatchScored -= HandleMatchScored;
                scoreManager.OnChainIncreased -= HandleChainIncreased;
                _isSubscribed = false;
            }
        }

        #endregion

        #region Event Handlers

        private void HandleMatchScored(int tilesMatched, int comboStep, int chainLevel)
        {
            if (!showComboPopups) return;
            if (comboStep < minComboToShow) return;

            // Spawn combo popup
            Vector3 spawnPos = GetSpawnPosition(comboSpawnOffset);
            SpawnComboPopup(comboStep, spawnPos);
        }

        private void HandleChainIncreased(int newChainLevel)
        {
            if (!showChainPopups) return;
            if (newChainLevel < minChainToShow) return;

            // Spawn chain popup with separate offset
            Vector3 spawnPos = GetSpawnPosition(chainSpawnOffset);
            SpawnChainPopup(newChainLevel, spawnPos);
        }

        #endregion

        #region Popup Spawning

        private void SpawnComboPopup(int comboCount, Vector3 position)
        {
            if (popupPrefab == null)
            {
                Debug.LogWarning("[ComboChainPopupSpawner] Popup prefab not assigned!");
                return;
            }

            GameObject popupObj = Instantiate(popupPrefab, position, Quaternion.identity);
            ComboChainPopup popup = popupObj.GetComponent<ComboChainPopup>();

            if (popup != null)
            {
                popup.ShowCombo(comboCount, position);
            }
            else
            {
                Debug.LogWarning("[ComboChainPopupSpawner] Popup prefab missing ComboChainPopup component!");
                Destroy(popupObj);
            }
        }

        private void SpawnChainPopup(int chainCount, Vector3 position)
        {
            if (popupPrefab == null)
            {
                Debug.LogWarning("[ComboChainPopupSpawner] Popup prefab not assigned!");
                return;
            }

            GameObject popupObj = Instantiate(popupPrefab, position, Quaternion.identity);
            ComboChainPopup popup = popupObj.GetComponent<ComboChainPopup>();

            if (popup != null)
            {
                popup.ShowChain(chainCount, position);
            }
            else
            {
                Debug.LogWarning("[ComboChainPopupSpawner] Popup prefab missing ComboChainPopup component!");
                Destroy(popupObj);
            }
        }

        /// <summary>
        /// Spawn a custom popup with specified text and color.
        /// </summary>
        public void SpawnCustomPopup(string text, Color color, Vector3 position)
        {
            if (popupPrefab == null) return;

            GameObject popupObj = Instantiate(popupPrefab, position, Quaternion.identity);
            ComboChainPopup popup = popupObj.GetComponent<ComboChainPopup>();

            if (popup != null)
            {
                popup.ShowCustom(text, color, position);
            }
        }

        #endregion

        #region Position Calculation

        private Vector3 GetSpawnPosition(Vector3 offset)
        {
            // Try to get the center of the last match
            // If we have match positions stored, use their center
            if (_currentMatchPositions.Count > 0 && gridManager != null)
            {
                Vector3 center = CalculateMatchCenter(_currentMatchPositions);
                return center + offset;
            }

            // Fallback: use stored last match center
            if (_lastMatchCenter != Vector3.zero)
            {
                return _lastMatchCenter + offset;
            }

            // Last resort: use grid center
            if (gridManager != null)
            {
                float gridOffset = gridManager.gridRiser != null ? gridManager.gridRiser.CurrentGridOffset : 0f;
                return gridManager.GetGridCenter(gridOffset) + offset;
            }

            return transform.position + offset;
        }

        private Vector3 CalculateMatchCenter(List<Vector2Int> positions)
        {
            if (positions.Count == 0 || gridManager == null) return Vector3.zero;

            float gridOffset = gridManager.gridRiser != null ? gridManager.gridRiser.CurrentGridOffset : 0f;

            Vector3 sum = Vector3.zero;
            foreach (var pos in positions)
            {
                sum += gridManager.GridToWorldPosition(pos, gridOffset);
            }

            return sum / positions.Count;
        }

        /// <summary>
        /// Set the positions of the current match (call from MatchProcessor before scoring).
        /// </summary>
        public void SetMatchPositions(List<Vector2Int> positions)
        {
            _currentMatchPositions.Clear();
            if (positions != null)
            {
                _currentMatchPositions.AddRange(positions);
                
                if (gridManager != null)
                {
                    _lastMatchCenter = CalculateMatchCenter(_currentMatchPositions);
                }
            }
        }

        /// <summary>
        /// Set the last match center position directly.
        /// </summary>
        public void SetLastMatchCenter(Vector3 worldPosition)
        {
            _lastMatchCenter = worldPosition;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Manually spawn a combo popup at a specific position.
        /// </summary>
        public void ShowComboAt(int comboCount, Vector3 worldPosition)
        {
            if (comboCount >= minComboToShow)
            {
                SpawnComboPopup(comboCount, worldPosition + comboSpawnOffset);
            }
        }

        /// <summary>
        /// Manually spawn a chain popup at a specific position.
        /// </summary>
        public void ShowChainAt(int chainCount, Vector3 worldPosition)
        {
            if (chainCount >= minChainToShow)
            {
                SpawnChainPopup(chainCount, worldPosition + chainSpawnOffset);
            }
        }

        #endregion
    }
}