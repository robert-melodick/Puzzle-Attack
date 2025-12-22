using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Manages status effects on tiles. Handles ticking, spreading, and visual feedback.
    /// </summary>
    public class StatusEffectManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Effect Durations")]
        public float frozenDuration = 5f;
        public float burningDuration = 10f;
        public float poisonedDuration = 8f;
        public float lockedDuration = 3f;

        [Header("Poison Settings")]
        public float poisonSpreadInterval = 2f;
        public float poisonSpreadChance = 0.3f;

        [Header("Visual Prefabs")]
        public GameObject frozenEffectPrefab;
        public GameObject burningEffectPrefab;
        public GameObject poisonedEffectPrefab;
        public GameObject lockedEffectPrefab;

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private MatchDetector _matchDetector;
        private readonly Dictionary<GameObject, GameObject> _effectVisuals = new();
        
        // Poison Variables
        private float _poisonSpreadTimer;
        
        // Fire Variables
        private float _fireSpreadTimer;
        private float _igniteHealth;
        
        // Ice Variables
        private float _iceSlideSpeed;

        #endregion

        #region Initialization

        public void Initialize(GridManager gridManager, MatchDetector matchDetector)
        {
            _gridManager = gridManager;
            _matchDetector = matchDetector;
        }

        #endregion

        #region Update

        private void Update()
        {
            if (_gridManager == null) return;

            TickAllStatuses();
            HandlePoisonSpreading();
            HandleFireSpreading();
        }

        private void TickAllStatuses()
        {
            var grid = _gridManager.Grid;
            if (grid == null) return;

            for (var x = 0; x < _gridManager.Width; x++)
            for (var y = 0; y < _gridManager.Height; y++)
            {
                var tileObj = grid[x, y];
                if (tileObj == null) continue;

                var tile = tileObj.GetComponent<Tile>();
                if (tile == null || !tile.HasStatus) continue;

                tile.TickStatus(Time.deltaTime);

                // Clean up visual if status cleared
                if (!tile.HasStatus && _effectVisuals.ContainsKey(tileObj))
                {
                    Destroy(_effectVisuals[tileObj]);
                    _effectVisuals.Remove(tileObj);
                }
            }
        }

        private void HandlePoisonSpreading()
        {
            _poisonSpreadTimer += Time.deltaTime;
            if (_poisonSpreadTimer < poisonSpreadInterval) return;

            _poisonSpreadTimer = 0f;

            var grid = _gridManager.Grid;
            var tilesToPoison = new List<GameObject>();

            // Find tiles adjacent to poisoned tiles
            for (var x = 0; x < _gridManager.Width; x++)
            for (var y = 0; y < _gridManager.Height; y++)
            {
                var tileObj = grid[x, y];
                if (tileObj == null) continue;

                var tile = tileObj.GetComponent<Tile>();
                if (tile == null || tile.CurrentStatus != TileStatus.Poisoned) continue;

                // Check adjacent tiles for spreading
                var adjacent = _matchDetector.GetAdjacentTiles(x, y);
                foreach (var adj in adjacent)
                {
                    var adjTile = adj.GetComponent<Tile>();
                    if (adjTile != null && !adjTile.HasStatus && Random.value < poisonSpreadChance)
                        tilesToPoison.Add(adj);
                }
            }

            // Apply poison to collected tiles
            foreach (var tileObj in tilesToPoison)
            {
                var tile = tileObj.GetComponent<Tile>();
                ApplyStatus(tile, TileStatus.Poisoned);
            }
        }

        private void HandleFireSpreading()
        {
            // if a tile is on fire, deplete igniteHealth on all tiles surrounding the burning tiles
        }

        #endregion

        #region Public API

        /// <summary>
        /// Apply a status effect to a tile with default duration.
        /// </summary>
        public void ApplyStatus(Tile tile, TileStatus status)
        {
            if (tile == null) return;

            var duration = GetDurationForStatus(status);
            tile.ApplyStatus(status, duration);

            CreateStatusVisual(tile.gameObject, status);
        }

        /// <summary>
        /// Apply a status effect with custom duration.
        /// </summary>
        public void ApplyStatus(Tile tile, TileStatus status, float duration)
        {
            if (tile == null) return;

            tile.ApplyStatus(status, duration);
            CreateStatusVisual(tile.gameObject, status);
        }

        /// <summary>
        /// Clear status from a tile.
        /// </summary>
        public void ClearStatus(Tile tile)
        {
            if (tile == null) return;

            tile.ClearStatus();

            if (_effectVisuals.TryGetValue(tile.gameObject, out var visual))
            {
                Destroy(visual);
                _effectVisuals.Remove(tile.gameObject);
            }
        }

        /// <summary>
        /// Apply a random status effect (for testing or random events).
        /// </summary>
        public void ApplyRandomStatus(Tile tile)
        {
            var statuses = new[] { TileStatus.Frozen, TileStatus.Burning, TileStatus.Poisoned, TileStatus.Locked };
            var randomStatus = statuses[Random.Range(0, statuses.Length)];
            ApplyStatus(tile, randomStatus);
        }

        #endregion

        #region Private Helpers

        private float GetDurationForStatus(TileStatus status)
        {
            return status switch
            {
                TileStatus.Frozen => frozenDuration,
                TileStatus.Burning => burningDuration,
                TileStatus.Poisoned => poisonedDuration,
                TileStatus.Locked => lockedDuration,
                _ => 0f
            };
        }

        private void CreateStatusVisual(GameObject tileObj, TileStatus status)
        {
            // Remove existing visual if present
            if (_effectVisuals.TryGetValue(tileObj, out var existing))
            {
                Destroy(existing);
                _effectVisuals.Remove(tileObj);
            }

            var prefab = GetPrefabForStatus(status);
            if (prefab == null) return;

            var visual = Instantiate(prefab, tileObj.transform);
            visual.transform.localPosition = Vector3.zero;
            _effectVisuals[tileObj] = visual;
        }

        private GameObject GetPrefabForStatus(TileStatus status)
        {
            return status switch
            {
                TileStatus.Frozen => frozenEffectPrefab,
                TileStatus.Burning => burningEffectPrefab,
                TileStatus.Poisoned => poisonedEffectPrefab,
                TileStatus.Locked => lockedEffectPrefab,
                _ => null
            };
        }

        #endregion
    }
}