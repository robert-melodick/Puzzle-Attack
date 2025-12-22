using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Manages garbage block spawning, conversion, and multiplayer garbage attacks.
    /// </summary>
    public class GarbageManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Garbage Settings")]
        public float garbageDropDelay = 1f;
        public int maxGarbageWidth = 6;
        public int maxPendingGarbage = 12;

        [Header("Conversion")]
        public float conversionDelay = 0.5f;

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private TileSpawner _tileSpawner;
        private MatchDetector _matchDetector;
        private GridRiser _gridRiser;

        private readonly Queue<GarbageRequest> _pendingGarbage = new();
        private readonly List<GameObject> _activeGarbage = new();
        private bool _isProcessingGarbage;

        #endregion
        
        #region Public Properties
        public bool IsProcessingGarbage() => _isProcessingGarbage;
        
        #endregion
        
        #region Initialization

        public void Initialize(GridManager gridManager, TileSpawner tileSpawner, 
            MatchDetector matchDetector, GridRiser gridRiser)
        {
            _gridManager = gridManager;
            _tileSpawner = tileSpawner;
            _matchDetector = matchDetector;
            _gridRiser = gridRiser;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Queue garbage to be dropped on the player (from attacks or game mode).
        /// </summary>
        public void QueueGarbage(int width, int height = 1)
        {
            if (_pendingGarbage.Count >= maxPendingGarbage)
            {
                Debug.LogWarning("[GarbageManager] Pending garbage queue full");
                return;
            }

            width = Mathf.Clamp(width, 1, maxGarbageWidth);
            _pendingGarbage.Enqueue(new GarbageRequest(width, height));
            Debug.Log($"[GarbageManager] Queued garbage: {width}x{height}");
        }

        /// <summary>
        /// Cancel pending garbage (when player sends garbage back).
        /// </summary>
        public int CancelGarbage(int amount)
        {
            var cancelled = 0;
            while (cancelled < amount && _pendingGarbage.Count > 0)
            {
                _pendingGarbage.Dequeue();
                cancelled++;
            }
            return cancelled;
        }

        /// <summary>
        /// Get count of pending garbage rows.
        /// </summary>
        public int GetPendingGarbageCount()
        {
            var count = 0;
            foreach (var g in _pendingGarbage)
                count += g.Height;
            return count;
        }

        /// <summary>
        /// Drop pending garbage immediately.
        /// </summary>
        public void DropPendingGarbage()
        {
            if (!_isProcessingGarbage && _pendingGarbage.Count > 0)
                StartCoroutine(ProcessGarbageQueue());
        }

        /// <summary>
        /// Called when garbage is cleared by adjacent match.
        /// </summary>
        public void OnGarbageCleared(GameObject garbage)
        {
            if (_activeGarbage.Contains(garbage))
            {
                _activeGarbage.Remove(garbage);
                StartCoroutine(ConvertGarbageToTiles(garbage));
            }
        }

        #endregion

        #region Garbage Processing

        private IEnumerator ProcessGarbageQueue()
        {
            _isProcessingGarbage = true;

            while (_pendingGarbage.Count > 0)
            {
                var request = _pendingGarbage.Dequeue();
                yield return StartCoroutine(DropGarbage(request.Width, request.Height));
                yield return new WaitForSeconds(garbageDropDelay);
            }

            _isProcessingGarbage = false;
        }

        private IEnumerator DropGarbage(int width, int height)
        {
            // Find valid spawn position at top of grid
            var spawnX = FindGarbageSpawnX(width);
            if (spawnX < 0)
            {
                Debug.LogWarning("[GarbageManager] No space to spawn garbage");
                yield break;
            }

            var spawnY = _gridManager.Height - height;

            // Check if there's room
            for (var x = spawnX; x < spawnX + width; x++)
            for (var y = spawnY; y < _gridManager.Height; y++)
            {
                if (_gridManager.Grid[x, y] != null)
                {
                    Debug.LogWarning("[GarbageManager] Spawn position blocked");
                    yield break;
                }
            }

            // Spawn the garbage block
            var garbage = _tileSpawner.SpawnGarbageBlock(spawnX, spawnY, width, height, _gridRiser.CurrentGridOffset);
            _activeGarbage.Add(garbage);

            Debug.Log($"[GarbageManager] Dropped garbage at ({spawnX}, {spawnY}) size {width}x{height}");

            // Let it fall
            yield return _gridManager.StartCoroutine(_gridManager.DropTiles());
        }

        private int FindGarbageSpawnX(int width)
        {
            // Try to spawn centered, then find any valid position
            var centerX = (_gridManager.Width - width) / 2;

            if (IsSpawnPositionClear(centerX, width))
                return centerX;

            for (var x = 0; x <= _gridManager.Width - width; x++)
                if (IsSpawnPositionClear(x, width))
                    return x;

            return -1;
        }

        private bool IsSpawnPositionClear(int startX, int width)
        {
            var topY = _gridManager.Height - 1;
            for (var x = startX; x < startX + width; x++)
                if (_gridManager.Grid[x, topY] != null)
                    return false;
            return true;
        }

        private IEnumerator ConvertGarbageToTiles(GameObject garbage)
        {
            var tile = garbage.GetComponent<Tile>();
            if (tile == null) yield break;

            var startX = tile.GridX;
            var startY = tile.GridY;
            var width = tile.GarbageWidth;
            var height = tile.GarbageHeight;

            // Clear garbage from grid
            for (var x = startX; x < startX + width && x < _gridManager.Width; x++)
            for (var y = startY; y < startY + height && y < _gridManager.Height; y++)
            {
                if (_gridManager.Grid[x, y] == garbage)
                    _gridManager.Grid[x, y] = null;
            }

            Destroy(garbage);

            yield return new WaitForSeconds(conversionDelay);

            // Spawn normal tiles in place of garbage
            for (var x = startX; x < startX + width && x < _gridManager.Width; x++)
            for (var y = startY; y < startY + height && y < _gridManager.Height; y++)
            {
                if (_gridManager.Grid[x, y] == null)
                {
                    _tileSpawner.SpawnTile(x, y, _gridRiser.CurrentGridOffset);
                    yield return new WaitForSeconds(0.05f);
                }
            }

            yield return _gridManager.StartCoroutine(_gridManager.DropTiles());
        }

        #endregion

        #region Nested Types

        private struct GarbageRequest
        {
            public int Width;
            public int Height;

            public GarbageRequest(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }

        #endregion
    }
}