using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Manages danger zone detection, per-column tracking, and broadcasts events for
    /// other systems (music, UI, character sprites) to respond to.
    /// </summary>
    public class DangerZoneManager : MonoBehaviour
    {
        #region Enums

        public enum DangerLevel
        {
            None,       // All columns safe
            Warning,    // At least one column in danger zone
            Critical    // Grace period active - game about to end
        }

        #endregion

        #region Events

        /// <summary>
        /// Fired when the overall danger level changes (None -> Warning -> Critical).
        /// </summary>
        public event Action<DangerLevel, DangerLevel> OnDangerLevelChanged;

        /// <summary>
        /// Fired when a specific column enters the danger zone.
        /// </summary>
        public event Action<int> OnColumnEnterDanger;

        /// <summary>
        /// Fired when a specific column exits the danger zone.
        /// </summary>
        public event Action<int> OnColumnExitDanger;

        /// <summary>
        /// Fired when grace period begins (critical danger).
        /// </summary>
        public event Action OnGracePeriodEntered;

        /// <summary>
        /// Fired when grace period ends (player cleared the top).
        /// </summary>
        public event Action OnGracePeriodExited;

        /// <summary>
        /// Fired every frame while in danger with the current danger level.
        /// Useful for intensity-based effects (e.g., pulsing faster as time runs out).
        /// </summary>
        public event Action<DangerLevel, float> OnDangerTick;

        #endregion

        #region Inspector Fields

        [Header("Danger Zone Settings")]
        [Tooltip("How many rows below the top row triggers danger zone")]
        [Range(1, 5)]
        public int dangerZoneRows = 2;

        [Header("Tile Danger Materials")]
        [Tooltip("Material applied to tiles in warning danger zone")]
        public Material tileWarningMaterial;
        
        [Tooltip("Material applied to tiles in critical danger (falls back to warning if not set)")]
        public Material tileCriticalMaterial;

        [Header("Garbage Danger Materials")]
        [Tooltip("Material applied to garbage blocks in warning danger zone")]
        public Material garbageWarningMaterial;
        
        [Tooltip("Material applied to garbage blocks in critical danger (falls back to warning if not set)")]
        public Material garbageCriticalMaterial;

        [Header("Debug")]
        public bool showDebugInfo = false;

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private GridRiser _gridRiser;
        private GarbageManager _garbageManager;
        private GameObject[,] _grid;

        private int _gridWidth;
        private int _gridHeight;
        private float _tileSize;

        // Per-column danger tracking
        private bool[] _columnInDanger;
        private Dictionary<GameObject, Material> _tilesInDanger = new();
        private Dictionary<GameObject, Material> _garbageInDanger = new();

        // State
        private DangerLevel _currentDangerLevel = DangerLevel.None;
        private bool _wasInGracePeriod;

        // Calculated danger zone threshold (row index)
        private int _dangerZoneStartRow;

        #endregion

        #region Properties

        public DangerLevel CurrentDangerLevel => _currentDangerLevel;
        public bool IsInDanger => _currentDangerLevel != DangerLevel.None;
        public bool IsCritical => _currentDangerLevel == DangerLevel.Critical;
        public int DangerZoneStartRow => _dangerZoneStartRow;

        /// <summary>
        /// Returns the number of columns currently in danger.
        /// </summary>
        public int ColumnsInDangerCount
        {
            get
            {
                if (_columnInDanger == null) return 0;
                int count = 0;
                for (int i = 0; i < _columnInDanger.Length; i++)
                    if (_columnInDanger[i]) count++;
                return count;
            }
        }

        /// <summary>
        /// Returns 0-1 representing how critical the danger is.
        /// 0 = just entered danger zone, 1 = about to game over.
        /// </summary>
        public float DangerIntensity
        {
            get
            {
                if (_currentDangerLevel == DangerLevel.None) return 0f;
                if (_currentDangerLevel == DangerLevel.Critical) return 1f;
                
                // Calculate based on highest tile position
                float highestNormalized = 0f;
                for (int x = 0; x < _gridWidth; x++)
                {
                    int highestY = GetHighestOccupiedRow(x);
                    if (highestY >= _dangerZoneStartRow)
                    {
                        float normalized = (float)(highestY - _dangerZoneStartRow + 1) / dangerZoneRows;
                        highestNormalized = Mathf.Max(highestNormalized, normalized);
                    }
                }
                return Mathf.Clamp01(highestNormalized);
            }
        }

        #endregion

        #region Initialization

        public void Initialize(GridManager gridManager, GridRiser gridRiser, GarbageManager garbageManager)
        {
            _gridManager = gridManager;
            _gridRiser = gridRiser;
            _garbageManager = garbageManager;
            _grid = gridManager.GetGrid();
            _gridWidth = gridManager.Width;
            _gridHeight = gridManager.Height;
            _tileSize = gridManager.TileSize;

            _columnInDanger = new bool[_gridWidth];
            _dangerZoneStartRow = _gridHeight - dangerZoneRows;

            Debug.Log($"[DangerZoneManager] Initialized. Danger zone starts at row {_dangerZoneStartRow} (top {dangerZoneRows} rows)");
        }

        #endregion

        #region Update Loop

        /// <summary>
        /// Called from GridManager's Update to check danger state.
        /// </summary>
        public void UpdateDangerState()
        {
            if (_gridRiser == null || _gridRiser.IsGameOver) return;

            // Check grace period state from GridRiser
            bool currentlyInGracePeriod = _gridRiser.IsInGracePeriod;

            // Handle grace period transitions
            if (currentlyInGracePeriod && !_wasInGracePeriod)
            {
                EnterGracePeriod();
            }
            else if (!currentlyInGracePeriod && _wasInGracePeriod)
            {
                ExitGracePeriod();
            }
            _wasInGracePeriod = currentlyInGracePeriod;

            // Update per-column danger state
            UpdateColumnDangerStates();

            // Update overall danger level
            UpdateOverallDangerLevel(currentlyInGracePeriod);

            // Update visual effects
            UpdateDangerVisuals();

            // Tick event for intensity-based systems
            if (_currentDangerLevel != DangerLevel.None)
            {
                OnDangerTick?.Invoke(_currentDangerLevel, DangerIntensity);
            }

            if (showDebugInfo)
            {
                DisplayDebugInfo();
            }
        }

        #endregion

        #region Column Danger Tracking

        private void UpdateColumnDangerStates()
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                bool wasInDanger = _columnInDanger[x];
                bool isNowInDanger = IsColumnInDangerZone(x);

                if (isNowInDanger && !wasInDanger)
                {
                    _columnInDanger[x] = true;
                    OnColumnEnterDanger?.Invoke(x);
                    Debug.Log($"[DangerZone] Column {x} entered danger zone");
                }
                else if (!isNowInDanger && wasInDanger)
                {
                    _columnInDanger[x] = false;
                    OnColumnExitDanger?.Invoke(x);
                    Debug.Log($"[DangerZone] Column {x} exited danger zone");
                }
            }
        }

        private bool IsColumnInDangerZone(int x)
        {
            // Check if any cell in this column at or above danger zone start has content
            for (int y = _dangerZoneStartRow; y < _gridHeight; y++)
            {
                if (_grid[x, y] != null)
                {
                    return true;
                }
            }
            return false;
        }

        private int GetHighestOccupiedRow(int x)
        {
            for (int y = _gridHeight - 1; y >= 0; y--)
            {
                if (_grid[x, y] != null)
                {
                    return y;
                }
            }
            return -1;
        }

        /// <summary>
        /// Check if a specific column is currently in the danger zone.
        /// </summary>
        public bool IsColumnInDanger(int x)
        {
            if (x < 0 || x >= _gridWidth) return false;
            return _columnInDanger[x];
        }

        /// <summary>
        /// Check if a specific cell is in the danger zone (by row).
        /// </summary>
        public bool IsCellInDangerZone(int x, int y)
        {
            return y >= _dangerZoneStartRow;
        }

        #endregion

        #region Overall Danger Level

        private void UpdateOverallDangerLevel(bool inGracePeriod)
        {
            DangerLevel newLevel;

            if (inGracePeriod)
            {
                newLevel = DangerLevel.Critical;
            }
            else if (ColumnsInDangerCount > 0)
            {
                newLevel = DangerLevel.Warning;
            }
            else
            {
                newLevel = DangerLevel.None;
            }

            if (newLevel != _currentDangerLevel)
            {
                var previousLevel = _currentDangerLevel;
                _currentDangerLevel = newLevel;
                
                if (newLevel == DangerLevel.None)
                {
                    // Danger cleared
                }

                OnDangerLevelChanged?.Invoke(previousLevel, newLevel);
                Debug.Log($"[DangerZone] Level changed: {previousLevel} -> {newLevel}");
            }
        }

        #endregion

        #region Grace Period

        private void EnterGracePeriod()
        {
            Debug.Log("[DangerZone] CRITICAL - Grace period entered!");
            OnGracePeriodEntered?.Invoke();
            
            // Notify all garbage blocks to show excited face
            NotifyGarbageBlocksCritical(true);
        }

        private void ExitGracePeriod()
        {
            Debug.Log("[DangerZone] Grace period exited - player cleared top!");
            OnGracePeriodExited?.Invoke();
            
            // Revert garbage block faces
            NotifyGarbageBlocksCritical(false);
        }

        private void NotifyGarbageBlocksCritical(bool isCritical)
        {
            if (_garbageManager == null) return;

            // Get all active garbage blocks and update their face state
            // The GarbageManager should expose a method to get all blocks
            // For now, we'll scan the grid
            var processedBlocks = new HashSet<GarbageBlock>();
            
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    var cell = _grid[x, y];
                    if (cell == null) continue;

                    // Check for garbage block
                    var garbageBlock = cell.GetComponent<GarbageBlock>();
                    if (garbageBlock != null && !processedBlocks.Contains(garbageBlock))
                    {
                        processedBlocks.Add(garbageBlock);
                        
                        if (isCritical)
                        {
                            // Show excited face - garbage is happy player is about to lose
                            SetGarbageExcitedFace(garbageBlock, true);
                        }
                        else
                        {
                            // Revert to normal state based on current state
                            SetGarbageExcitedFace(garbageBlock, false);
                        }
                    }

                    // Check for garbage reference
                    var garbageRef = cell.GetComponent<GarbageReference>();
                    if (garbageRef != null && garbageRef.Owner != null && !processedBlocks.Contains(garbageRef.Owner))
                    {
                        processedBlocks.Add(garbageRef.Owner);
                        
                        if (isCritical)
                        {
                            SetGarbageExcitedFace(garbageRef.Owner, true);
                        }
                        else
                        {
                            SetGarbageExcitedFace(garbageRef.Owner, false);
                        }
                    }
                }
            }
        }

        private void SetGarbageExcitedFace(GarbageBlock block, bool excited)
        {
            if (block == null) return;
            
            // Get the renderer and set face state
            var renderer = block.GetComponent<GarbageRenderer>();
            if (renderer != null)
            {
                if (excited)
                {
                    // Use a new "Excited" state or repurpose an existing one
                    // Based on GarbageRenderer.FaceState enum, we might need to add Excited
                    // For now, use the existing states creatively
                    renderer.SetFaceState(GarbageRenderer.FaceState.Triggered);
                }
                else
                {
                    // Revert based on block's actual state
                    if (block.IsConverting)
                        renderer.SetFaceState(GarbageRenderer.FaceState.Converting);
                    else if (block.IsFalling)
                        renderer.SetFaceState(GarbageRenderer.FaceState.Falling);
                    else
                        renderer.SetFaceState(GarbageRenderer.FaceState.Normal);
                }
            }
        }

        #endregion

        #region Visual Effects (Material Swap)

        private void UpdateDangerVisuals()
        {
            // Determine which material to use based on current danger level
            Material tileMaterial = GetTileDangerMaterial();
            Material garbageMaterial = GetGarbageDangerMaterial();

            // Track what should currently be in danger
            var currentDangerTiles = new HashSet<GameObject>();
            var currentDangerGarbage = new HashSet<GameObject>();
            var processedGarbageBlocks = new HashSet<GarbageBlock>();

            // Find all objects in danger zone columns
            for (int x = 0; x < _gridWidth; x++)
            {
                if (!_columnInDanger[x]) continue;

                // Apply effect to entire column
                for (int y = 0; y < _gridHeight; y++)
                {
                    var cell = _grid[x, y];
                    if (cell == null) continue;

                    // Check for regular tile
                    var tile = cell.GetComponent<Tile>();
                    if (tile != null)
                    {
                        currentDangerTiles.Add(cell);
                        continue;
                    }

                    // Check for garbage block (only process the anchor, not references)
                    var garbageBlock = cell.GetComponent<GarbageBlock>();
                    if (garbageBlock != null && !processedGarbageBlocks.Contains(garbageBlock))
                    {
                        processedGarbageBlocks.Add(garbageBlock);
                        currentDangerGarbage.Add(cell);
                        continue;
                    }

                    // Check for garbage reference - get the owner
                    var garbageRef = cell.GetComponent<GarbageReference>();
                    if (garbageRef != null && garbageRef.Owner != null && 
                        !processedGarbageBlocks.Contains(garbageRef.Owner))
                    {
                        processedGarbageBlocks.Add(garbageRef.Owner);
                        currentDangerGarbage.Add(garbageRef.Owner.gameObject);
                    }
                }
            }

            // Update tiles - add new, update existing, remove cleared
            UpdateDangerMaterials(
                currentDangerTiles, 
                _tilesInDanger, 
                tileMaterial,
                GetTileRenderer);

            // Update garbage - add new, update existing, remove cleared
            UpdateDangerMaterials(
                currentDangerGarbage, 
                _garbageInDanger, 
                garbageMaterial,
                GetGarbageRenderer);
        }

        private void UpdateDangerMaterials(
            HashSet<GameObject> currentInDanger,
            Dictionary<GameObject, Material> trackedObjects,
            Material dangerMaterial,
            Func<GameObject, SpriteRenderer> getRenderer)
        {
            // Remove objects no longer in danger
            var toRemove = new List<GameObject>();
            foreach (var kvp in trackedObjects)
            {
                var obj = kvp.Key;
                var originalMaterial = kvp.Value;

                if (obj == null)
                {
                    toRemove.Add(obj);
                    continue;
                }

                if (!currentInDanger.Contains(obj))
                {
                    // Restore original material
                    var renderer = getRenderer(obj);
                    if (renderer != null && originalMaterial != null)
                    {
                        renderer.material = originalMaterial;
                    }
                    toRemove.Add(obj);
                }
                else if (dangerMaterial != null)
                {
                    // Update material in case danger level changed (warning -> critical)
                    var renderer = getRenderer(obj);
                    if (renderer != null && renderer.material != dangerMaterial)
                    {
                        renderer.material = dangerMaterial;
                    }
                }
            }

            foreach (var obj in toRemove)
            {
                trackedObjects.Remove(obj);
            }

            // Add new objects to danger
            foreach (var obj in currentInDanger)
            {
                if (obj == null) continue;

                if (!trackedObjects.ContainsKey(obj))
                {
                    var renderer = getRenderer(obj);
                    if (renderer != null)
                    {
                        // Cache original material
                        trackedObjects[obj] = renderer.material;

                        // Apply danger material
                        if (dangerMaterial != null)
                        {
                            renderer.material = dangerMaterial;
                        }
                    }
                }
            }
        }

        private Material GetTileDangerMaterial()
        {
            if (_currentDangerLevel == DangerLevel.Critical && tileCriticalMaterial != null)
                return tileCriticalMaterial;
            
            return tileWarningMaterial; // Falls back to warning (or null if not set)
        }

        private Material GetGarbageDangerMaterial()
        {
            if (_currentDangerLevel == DangerLevel.Critical && garbageCriticalMaterial != null)
                return garbageCriticalMaterial;
            
            return garbageWarningMaterial; // Falls back to warning (or null if not set)
        }

        private SpriteRenderer GetTileRenderer(GameObject obj)
        {
            return obj?.GetComponent<SpriteRenderer>();
        }

        private SpriteRenderer GetGarbageRenderer(GameObject obj)
        {
            // Garbage blocks might have the renderer on a child or use GarbageRenderer
            var garbageRenderer = obj?.GetComponent<GarbageRenderer>();
            if (garbageRenderer != null)
            {
                // Try to get the main sprite renderer from garbage renderer
                // Adjust this based on your GarbageRenderer structure
                return obj.GetComponentInChildren<SpriteRenderer>();
            }
            return obj?.GetComponent<SpriteRenderer>();
        }

        private void RemoveDangerEffect(Tile tile)
        {
            // No longer needed - handled by UpdateDangerMaterials
        }

        /// <summary>
        /// Force refresh of all danger visuals. Call after major grid changes.
        /// </summary>
        public void RefreshDangerVisuals()
        {
            // Restore all materials first
            foreach (var kvp in _tilesInDanger)
            {
                if (kvp.Key == null) continue;
                var renderer = GetTileRenderer(kvp.Key);
                if (renderer != null && kvp.Value != null)
                {
                    renderer.material = kvp.Value;
                }
            }
            _tilesInDanger.Clear();

            foreach (var kvp in _garbageInDanger)
            {
                if (kvp.Key == null) continue;
                var renderer = GetGarbageRenderer(kvp.Key);
                if (renderer != null && kvp.Value != null)
                {
                    renderer.material = kvp.Value;
                }
            }
            _garbageInDanger.Clear();

            // Re-evaluate
            UpdateDangerVisuals();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Manually trigger danger state check. Useful after spawning garbage.
        /// </summary>
        public void ForceCheckDangerState()
        {
            UpdateColumnDangerStates();
            UpdateOverallDangerLevel(_gridRiser.IsInGracePeriod);
            UpdateDangerVisuals();
        }

        /// <summary>
        /// Get the danger zone row threshold (first row of danger zone).
        /// </summary>
        public int GetDangerZoneStartRow()
        {
            return _dangerZoneStartRow;
        }

        /// <summary>
        /// Check if any tiles are currently in the danger zone.
        /// </summary>
        public bool HasTilesInDanger()
        {
            return _tilesInDanger.Count > 0 || _garbageInDanger.Count > 0;
        }

        /// <summary>
        /// Get array of which columns are in danger.
        /// </summary>
        public bool[] GetColumnDangerStates()
        {
            return (bool[])_columnInDanger.Clone();
        }

        #endregion

        #region Debug

        private void DisplayDebugInfo()
        {
            string dangerColumns = "";
            for (int x = 0; x < _gridWidth; x++)
            {
                dangerColumns += _columnInDanger[x] ? "!" : ".";
            }
            Debug.Log($"[DangerZone] Level: {_currentDangerLevel} | Intensity: {DangerIntensity:F2} | Columns: [{dangerColumns}]");
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _gridManager == null) return;

            // Draw danger zone boundary
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            
            float zoneHeight = dangerZoneRows * _tileSize;
            float zoneY = _dangerZoneStartRow * _tileSize + _gridRiser.CurrentGridOffset;
            float gridWidth = _gridWidth * _tileSize;

            Vector3 center = new Vector3(gridWidth / 2f - _tileSize / 2f, zoneY + zoneHeight / 2f - _tileSize / 2f, 0);
            Vector3 size = new Vector3(gridWidth, zoneHeight, 0.1f);
            
            Gizmos.DrawCube(center, size);

            // Draw danger line
            Gizmos.color = Color.red;
            Vector3 lineStart = new Vector3(-_tileSize / 2f, zoneY - _tileSize / 2f, 0);
            Vector3 lineEnd = new Vector3(gridWidth - _tileSize / 2f, zoneY - _tileSize / 2f, 0);
            Gizmos.DrawLine(lineStart, lineEnd);
        }

        #endregion
    }
}