using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Manages garbage block spawning, falling, cluster detection, and conversion.
    /// Coordinates with GridManager for grid state and TileSpawner for creating converted tiles.
    /// All positions are relative to the GridManager's transform position.
    /// </summary>
    public class GarbageManager : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Prefabs")]
        public GameObject garbagePrefab;

        [Header("Settings")]
        public GarbageConversionSettings conversionSettings;

        [Header("Garbage Spawning")]
        [Tooltip("Delay between dropping queued garbage blocks")]
        public float garbageDropDelay = 1f;
        
        [Tooltip("Maximum width for a single garbage block")]
        public int maxGarbageWidth = 6;
        
        [Tooltip("Maximum pending garbage in queue")]
        public int maxPendingGarbage = 12;

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private TileSpawner _tileSpawner;
        private MatchDetector _matchDetector;
        private GridRiser _gridRiser;

        private readonly Queue<GarbageRequest> _pendingGarbage = new();
        private readonly List<GarbageBlock> _activeGarbage = new();
        private readonly List<GarbageCluster> _clusters = new();
        private readonly HashSet<GarbageBlock> _fallingGarbage = new();
        private readonly HashSet<GarbageBlock> _convertingGarbage = new();
        
        // Tiles held during conversion (don't fall until conversion completes)
        private readonly HashSet<GameObject> _heldTiles = new();
        
        private bool _isProcessingGarbage;
        private bool _isProcessingConversion;

        #endregion

        #region Properties

        public bool IsProcessingGarbage => _isProcessingGarbage;
        public bool IsProcessingConversion => _isProcessingConversion;
        public bool HasActiveGarbage => _activeGarbage.Count > 0;
        public IReadOnlyList<GarbageBlock> ActiveGarbage => _activeGarbage;

        #endregion

        #region Initialization

        public void Initialize(GridManager gridManager, TileSpawner tileSpawner,
            MatchDetector matchDetector, GridRiser gridRiser)
        {
            _gridManager = gridManager;
            _tileSpawner = tileSpawner;
            _matchDetector = matchDetector;
            _gridRiser = gridRiser;

            // Create default settings if none assigned
            if (conversionSettings == null)
            {
                conversionSettings = GarbageConversionSettings.GetDefault();
            }
        }

        #endregion

        #region Public API - Garbage Queue

        /// <summary>
        /// Queue garbage to be dropped on the player.
        /// </summary>
        public void QueueGarbage(int width, int height = 1)
        {
            if (_pendingGarbage.Count >= maxPendingGarbage)
            {
                Debug.LogWarning("[GarbageManager] Pending garbage queue full");
                return;
            }

            width = Mathf.Clamp(width, 1, maxGarbageWidth);
            height = Mathf.Max(1, height);
            _pendingGarbage.Enqueue(new GarbageRequest(width, height));
            Debug.Log($"[GarbageManager] Queued garbage: {width}x{height}");
        }

        /// <summary>
        /// Cancel pending garbage (counter-attack).
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
            return _pendingGarbage.Sum(g => g.Height);
        }

        /// <summary>
        /// Drop pending garbage immediately.
        /// </summary>
        public void DropPendingGarbage()
        {
            if (!_isProcessingGarbage && _pendingGarbage.Count > 0)
                StartCoroutine(ProcessGarbageQueue());
        }

        #endregion

        #region Public API - Garbage State

        /// <summary>
        /// Check if a position contains garbage.
        /// </summary>
        public bool IsGarbageAt(int x, int y)
        {
            var cell = _gridManager.Grid[x, y];
            if (cell == null) return false;
            return cell.GetComponent<GarbageReference>() != null || cell.GetComponent<GarbageBlock>() != null;
        }

        /// <summary>
        /// Get the garbage block at a position.
        /// </summary>
        public GarbageBlock GetGarbageAt(int x, int y)
        {
            var cell = _gridManager.Grid[x, y];
            if (cell == null) return null;

            var block = cell.GetComponent<GarbageBlock>();
            if (block != null) return block;

            var reference = cell.GetComponent<GarbageReference>();
            return reference?.Owner;
        }

        /// <summary>
        /// Check if any garbage is currently falling.
        /// </summary>
        public bool HasFallingGarbage()
        {
            return _fallingGarbage.Count > 0;
        }

        /// <summary>
        /// Check if any garbage blocks need to fall (have no bottom support).
        /// </summary>
        public bool HasGarbageThatNeedsFalling()
        {
            foreach (var garbage in _activeGarbage)
            {
                if (garbage == null || garbage.IsConverting) continue;
                if (!garbage.HasBottomSupport(_gridManager.Grid, _gridManager.Height))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check if a tile is being held during garbage conversion.
        /// </summary>
        public bool IsTileHeld(GameObject tile)
        {
            return _heldTiles.Contains(tile);
        }

        #endregion

        #region Public API - Conversion Trigger

        /// <summary>
        /// Called when a match occurs adjacent to garbage.
        /// Triggers conversion for the garbage and its cluster.
        /// </summary>
        public void OnMatchAdjacentToGarbage(List<Vector2Int> matchPositions)
        {
            if (_isProcessingConversion) return;

            // Find all garbage blocks adjacent to the match
            var adjacentGarbage = new HashSet<GarbageBlock>();

            foreach (var pos in matchPositions)
            {
                // Check cardinal directions
                CheckGarbageAdjacent(pos.x - 1, pos.y, adjacentGarbage);
                CheckGarbageAdjacent(pos.x + 1, pos.y, adjacentGarbage);
                CheckGarbageAdjacent(pos.x, pos.y - 1, adjacentGarbage);
                CheckGarbageAdjacent(pos.x, pos.y + 1, adjacentGarbage);
            }

            // Filter to only settled garbage
            var settledGarbage = adjacentGarbage.Where(g => g.IsSettled).ToList();

            if (settledGarbage.Count == 0) return;

            StartCoroutine(ProcessConversion(settledGarbage));
        }

        private void CheckGarbageAdjacent(int x, int y, HashSet<GarbageBlock> result)
        {
            if (x < 0 || x >= _gridManager.Width || y < 0 || y >= _gridManager.Height)
                return;

            var garbage = GetGarbageAt(x, y);
            if (garbage != null)
            {
                if (!result.Contains(garbage))
                    result.Add(garbage);
            }
        }

        #endregion

        #region Garbage Spawning

        private IEnumerator ProcessGarbageQueue()
        {
            _isProcessingGarbage = true;

            while (_pendingGarbage.Count > 0)
            {
                var request = _pendingGarbage.Dequeue();
                yield return StartCoroutine(SpawnAndDropGarbage(request.Width, request.Height));
                yield return new WaitForSeconds(garbageDropDelay);
            }

            _isProcessingGarbage = false;
        }

        private IEnumerator SpawnAndDropGarbage(int width, int height)
        {
            // Find spawn position at top of grid
            var spawnX = FindGarbageSpawnX(width);
            if (spawnX < 0)
            {
                Debug.LogWarning("[GarbageManager] No space to spawn garbage");
                yield break;
            }

            var spawnY = _gridManager.Height - height;

            // Check if spawn area is clear
            for (var x = spawnX; x < spawnX + width; x++)
            {
                for (var y = spawnY; y < _gridManager.Height; y++)
                {
                    if (_gridManager.Grid[x, y] != null)
                    {
                        Debug.LogWarning("[GarbageManager] Spawn position blocked");
                        yield break;
                    }
                }
            }

            // Spawn the garbage block
            var garbage = SpawnGarbageBlock(spawnX, spawnY, width, height);
            if (garbage == null) yield break;

            // Let it fall
            yield return StartCoroutine(ProcessGarbageFalling());
        }

        /// <summary>
        /// Spawn a garbage block at the specified position.
        /// </summary>
        public GarbageBlock SpawnGarbageBlock(int x, int y, int width, int height)
        {
            if (garbagePrefab == null)
            {
                Debug.LogError("[GarbageManager] Garbage prefab not assigned!");
                return null;
            }

            var currentOffset = _gridRiser?.CurrentGridOffset ?? 0f;
            
            // Use GridManager's position helper for proper offset
            var pos = _gridManager.GridToWorldPosition(x, y, currentOffset);
            
            // Parent under GridContainer so garbage moves with the grid
            var garbageObj = Instantiate(garbagePrefab, pos, Quaternion.identity, _gridManager.GridContainer);
            garbageObj.name = $"Garbage_{x}_{y}_{width}x{height}";

            var block = garbageObj.GetComponent<GarbageBlock>();
            if (block == null)
            {
                block = garbageObj.AddComponent<GarbageBlock>();
            }

            var renderer = garbageObj.GetComponent<GarbageRenderer>();
            if (renderer == null)
            {
                renderer = garbageObj.AddComponent<GarbageRenderer>();
            }
            renderer.tileSize = _gridManager.TileSize;

            block.Initialize(x, y, width, height, _gridManager);

            // Place in grid - anchor cell gets the GarbageBlock, others get references
            PlaceGarbageInGrid(block);

            _activeGarbage.Add(block);
            UpdateClusters();

            return block;
        }

        private void PlaceGarbageInGrid(GarbageBlock block)
        {
            for (var gx = 0; gx < block.Width; gx++)
            {
                for (var gy = 0; gy < block.CurrentHeight; gy++)
                {
                    var gridX = block.AnchorPosition.x + gx;
                    var gridY = block.AnchorPosition.y + gy;

                    if (gridX < 0 || gridX >= _gridManager.Width ||
                        gridY < 0 || gridY >= _gridManager.Height)
                        continue;

                    if (gx == 0 && gy == 0)
                    {
                        // Anchor cell - use the GarbageBlock's GameObject directly
                        _gridManager.Grid[gridX, gridY] = block.gameObject;
                    }
                    else
                    {
                        // Non-anchor cell - create a reference
                        var refObj = new GameObject($"GarbageRef_{gridX}_{gridY}");
                        refObj.transform.SetParent(block.transform);
                        refObj.transform.localPosition = new Vector3(gx * _gridManager.TileSize, gy * _gridManager.TileSize, 0);

                        var reference = refObj.AddComponent<GarbageReference>();
                        reference.Initialize(block, new Vector2Int(gx, gy));

                        _gridManager.Grid[gridX, gridY] = refObj;
                    }
                }
            }
        }

        private void ClearGarbageFromGrid(GarbageBlock block)
        {
            for (var gx = 0; gx < block.Width; gx++)
            {
                for (var gy = 0; gy < block.CurrentHeight; gy++)
                {
                    var gridX = block.AnchorPosition.x + gx;
                    var gridY = block.AnchorPosition.y + gy;

                    if (gridX < 0 || gridX >= _gridManager.Width ||
                        gridY < 0 || gridY >= _gridManager.Height)
                        continue;

                    var cell = _gridManager.Grid[gridX, gridY];
                    if (cell == null) continue;

                    // Check if this cell belongs to this garbage block
                    if (cell == block.gameObject)
                    {
                        _gridManager.Grid[gridX, gridY] = null;
                    }
                    else
                    {
                        var reference = cell.GetComponent<GarbageReference>();
                        if (reference != null && reference.Owner == block)
                        {
                            _gridManager.Grid[gridX, gridY] = null;
                            Destroy(cell);
                        }
                    }
                }
            }
        }

        private int FindGarbageSpawnX(int width)
        {
            // Try centered first
            var centerX = (_gridManager.Width - width) / 2;
            if (IsSpawnPositionClear(centerX, width))
                return centerX;

            // Try any valid position
            for (var x = 0; x <= _gridManager.Width - width; x++)
            {
                if (IsSpawnPositionClear(x, width))
                    return x;
            }

            return -1;
        }

        private bool IsSpawnPositionClear(int startX, int width)
        {
            var topY = _gridManager.Height - 1;
            for (var x = startX; x < startX + width; x++)
            {
                if (_gridManager.Grid[x, topY] != null)
                    return false;
            }
            return true;
        }

        #endregion

        #region Garbage Falling

        public IEnumerator ProcessGarbageFalling()
        {
            // Find all garbage that should fall
            _fallingGarbage.Clear();
            
            foreach (var garbage in _activeGarbage)
            {
                if (garbage == null || garbage.IsConverting) continue;
                if (!garbage.HasBottomSupport(_gridManager.Grid, _gridManager.Height))
                {
                    _fallingGarbage.Add(garbage);
                }
            }

            if (_fallingGarbage.Count == 0) yield break;

            // Calculate fall targets
            var fallTargets = new Dictionary<GarbageBlock, Vector2Int>();
            var startPositions = new Dictionary<GarbageBlock, float>(); // Store start Y in grid coords
            
            foreach (var garbage in _fallingGarbage)
            {
                var target = CalculateFallTarget(garbage);
                fallTargets[garbage] = target;
                startPositions[garbage] = garbage.AnchorPosition.y;
                
                // Update grid immediately
                ClearGarbageFromGrid(garbage);
                garbage.StartFalling(target);
            }

            // Animate falling
            var maxFallDistance = 0;
            foreach (var kvp in fallTargets)
            {
                var distance = kvp.Key.AnchorPosition.y - kvp.Value.y;
                maxFallDistance = Mathf.Max(maxFallDistance, distance);
            }

            var fallDuration = maxFallDistance / _gridManager.dropSpeed;
            var elapsed = 0f;

            while (elapsed < fallDuration)
            {
                foreach (var garbage in _fallingGarbage)
                {
                    if (garbage == null) continue;

                    var target = fallTargets[garbage];
                    var startY = startPositions[garbage];
                    var progress = Mathf.Clamp01(elapsed / fallDuration);

                    var currentY = Mathf.Lerp(startY, target.y, progress);
                    var currentOffset = _gridRiser?.CurrentGridOffset ?? 0f;

                    // Use GridManager's position helper
                    garbage.transform.position = _gridManager.GridToWorldPosition(
                        garbage.AnchorPosition.x, 
                        (int)currentY, 
                        currentOffset + (currentY - (int)currentY) * _gridManager.TileSize);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Finalize positions
            foreach (var garbage in _fallingGarbage)
            {
                if (garbage == null) continue;

                var target = fallTargets[garbage];
                garbage.SetAnchorPosition(target.x, target.y);
                garbage.StopFalling();

                var currentOffset = _gridRiser?.CurrentGridOffset ?? 0f;
                
                // Use GridManager's position helper
                garbage.transform.position = _gridManager.GridToWorldPosition(target.x, target.y, currentOffset);

                PlaceGarbageInGrid(garbage);
                garbage.PlayLandSound();
            }

            _fallingGarbage.Clear();
            UpdateClusters();

            // Check if more falling is needed (chain reaction)
            yield return StartCoroutine(ProcessGarbageFalling());
        }

        private Vector2Int CalculateFallTarget(GarbageBlock garbage)
        {
            var targetY = garbage.AnchorPosition.y;

            // Find lowest valid position
            for (var y = garbage.AnchorPosition.y - 1; y >= 0; y--)
            {
                var canFallTo = true;

                // Check all cells in the bottom row at this Y
                for (var x = garbage.AnchorPosition.x; x < garbage.AnchorPosition.x + garbage.Width; x++)
                {
                    if (x < 0 || x >= _gridManager.Width)
                    {
                        canFallTo = false;
                        break;
                    }

                    var cell = _gridManager.Grid[x, y];
                    if (cell != null)
                    {
                        // Check if it's part of this garbage (shouldn't happen but be safe)
                        var reference = cell.GetComponent<GarbageReference>();
                        if (reference != null && reference.Owner == garbage) continue;
                        if (cell == garbage.gameObject) continue;

                        canFallTo = false;
                        break;
                    }
                }

                if (canFallTo)
                {
                    targetY = y;
                }
                else
                {
                    break;
                }
            }

            return new Vector2Int(garbage.AnchorPosition.x, targetY);
        }

        /// <summary>
        /// Called when the grid shifts up by one row (new row spawned at bottom).
        /// Updates falling garbage targets to account for the shift.
        /// </summary>
        public void OnGridShiftedUp()
        {
            if (_fallingGarbage.Count == 0) return;

            foreach (var garbage in _fallingGarbage)
            {
                if (garbage == null) continue;

                // Shift target up by 1
                var currentTarget = garbage.TargetPosition;
                var newTarget = new Vector2Int(currentTarget.x, currentTarget.y + 1);
                garbage.RetargetFall(newTarget);
            }
        }

        #endregion

        #region Cluster Management

        private void UpdateClusters()
        {
            // Clear existing clusters
            foreach (var cluster in _clusters)
            {
                foreach (var block in cluster.Blocks)
                {
                    if (block != null)
                        block.Cluster = null;
                }
            }
            _clusters.Clear();

            // Build new clusters using flood-fill
            var processed = new HashSet<GarbageBlock>();

            foreach (var garbage in _activeGarbage)
            {
                if (garbage == null || processed.Contains(garbage)) continue;

                var cluster = new GarbageCluster();
                FloodFillCluster(garbage, cluster, processed);
                
                if (cluster.Blocks.Count > 0)
                {
                    _clusters.Add(cluster);
                }
            }
        }

        private void FloodFillCluster(GarbageBlock start, GarbageCluster cluster, HashSet<GarbageBlock> processed)
        {
            if (start == null || processed.Contains(start)) return;

            processed.Add(start);
            cluster.AddBlock(start);

            // Find all adjacent garbage blocks
            foreach (var other in _activeGarbage)
            {
                if (other == null || processed.Contains(other)) continue;
                if (start.IsAdjacentTo(other))
                {
                    FloodFillCluster(other, cluster, processed);
                }
            }
        }

        #endregion

        #region Conversion Processing

        private IEnumerator ProcessConversion(List<GarbageBlock> triggeredGarbage)
        {
            _isProcessingConversion = true;

            // Group by cluster for proper handling
            var clustersToProcess = new HashSet<GarbageCluster>();
            var directlyTriggered = new HashSet<GarbageBlock>(triggeredGarbage);

            foreach (var garbage in triggeredGarbage)
            {
                if (garbage.Cluster != null)
                {
                    clustersToProcess.Add(garbage.Cluster);
                }
            }

            // Process directly triggered garbage first (potentially async)
            var directCoroutines = new List<Coroutine>();
            foreach (var garbage in directlyTriggered)
            {
                directCoroutines.Add(StartCoroutine(ConvertGarbageBlock(garbage)));
            }

            // Wait for directly triggered to complete
            foreach (var coroutine in directCoroutines)
            {
                yield return coroutine;
            }

            // If cluster propagation is enabled, process remaining cluster members
            if (conversionSettings.propagateToCluster)
            {
                foreach (var cluster in clustersToProcess)
                {
                    var remainingBlocks = cluster.GetBlocksBottomUp()
                        .Where(b => b != null && !directlyTriggered.Contains(b) && !b.IsFullyConverted())
                        .ToList();

                    if (conversionSettings.clusterConvertsSequentially)
                    {
                        // Process bottom-to-top, one at a time
                        foreach (var garbage in remainingBlocks)
                        {
                            yield return StartCoroutine(ConvertGarbageBlock(garbage));
                        }
                    }
                    else
                    {
                        // Process all simultaneously
                        var clusterCoroutines = remainingBlocks
                            .Select(g => StartCoroutine(ConvertGarbageBlock(g)))
                            .ToList();

                        foreach (var coroutine in clusterCoroutines)
                        {
                            yield return coroutine;
                        }
                    }
                }
            }

            // Release held tiles
            ReleaseHeldTiles();

            _isProcessingConversion = false;

            // Trigger falling for everything (DropTiles now handles garbage falling too)
            yield return _gridManager.StartCoroutine(_gridManager.DropTiles());
        }

        private IEnumerator ConvertGarbageBlock(GarbageBlock garbage)
        {
            if (garbage == null || garbage.IsConverting) yield break;

            garbage.BeginConversion();
            _convertingGarbage.Add(garbage);

            var renderer = garbage.GetComponent<GarbageRenderer>();

            // Initial delay
            yield return new WaitForSeconds(conversionSettings.conversionStartDelay);

            // Flash effect
            if (conversionSettings.conversionStartFlashDuration > 0 && renderer != null)
            {
                renderer.SetFlashAmount(1f);
                yield return new WaitForSeconds(conversionSettings.conversionStartFlashDuration);
                renderer.SetFlashAmount(0f);
            }

            // Scan effect (travels up the garbage block)
            if (conversionSettings.useScanEffect && renderer != null)
            {
                yield return StartCoroutine(PlayScanEffect(garbage, renderer));
            }

            // Convert rows
            var rowsToConvert = Mathf.Min(conversionSettings.rowsPerMatch, garbage.CurrentHeight);

            for (var i = 0; i < rowsToConvert && garbage.CurrentHeight > 0; i++)
            {
                yield return StartCoroutine(ConvertBottomRow(garbage));

                if (i < rowsToConvert - 1)
                {
                    yield return new WaitForSeconds(conversionSettings.timeBetweenRowConversions);
                }
            }

            // Apply the visual shrink once after all rows are converted
            float shrinkDuration = 0f;
            if (!garbage.IsFullyConverted())
            {
                shrinkDuration = garbage.ApplyVisualShrink(_gridManager);
            }

            // Wait for shrink animation to complete before ending conversion
            if (shrinkDuration > 0f)
            {
                yield return new WaitForSeconds(shrinkDuration);
            }

            garbage.EndConversion();
            _convertingGarbage.Remove(garbage);

            // Check if fully converted
            if (garbage.IsFullyConverted())
            {
                DestroyGarbageBlock(garbage);
            }
        }

        private IEnumerator PlayScanEffect(GarbageBlock garbage, GarbageRenderer renderer)
        {
            var totalHeight = conversionSettings.iterateThroughEmptySpace 
                ? garbage.Height  // Original height for dramatic effect
                : garbage.CurrentHeight;

            var timePerRow = conversionSettings.scanEffectDuration / Mathf.Max(1, totalHeight);

            for (var row = 0; row < totalHeight; row++)
            {
                renderer.HighlightRow(row, conversionSettings.scanHighlightColor, conversionSettings.scanHighlightIntensity);
                
                // Play tick sound
                if (conversionSettings.scanTickSound != null)
                {
                    AudioSource.PlayClipAtPoint(conversionSettings.scanTickSound, garbage.transform.position);
                }

                yield return new WaitForSeconds(timePerRow);
            }

            renderer.ClearHighlight();
        }

        private IEnumerator ConvertBottomRow(GarbageBlock garbage)
        {
            var cells = garbage.GetNextConversionRowCells(conversionSettings.tileSpawnOrder);

            // Clear bottom row from grid first
            foreach (var cell in cells)
            {
                var gridCell = _gridManager.Grid[cell.x, cell.y];
                if (gridCell != null)
                {
                    var reference = gridCell.GetComponent<GarbageReference>();
                    if (reference != null && reference.Owner == garbage)
                    {
                        _gridManager.Grid[cell.x, cell.y] = null;
                        Destroy(gridCell);
                    }
                    else if (gridCell == garbage.gameObject)
                    {
                        _gridManager.Grid[cell.x, cell.y] = null;
                    }
                }
            }

            // Spawn tiles in sequence
            var spawnedTiles = new List<GameObject>();
            
            foreach (var cell in cells)
            {
                var tile = SpawnConvertedTile(cell.x, cell.y);
                if (tile != null)
                {
                    spawnedTiles.Add(tile);
                    
                    if (conversionSettings.holdTilesUntilComplete)
                    {
                        _heldTiles.Add(tile);
                    }

                    // Play spawn sound
                    if (conversionSettings.tileSpawnSound != null)
                    {
                        AudioSource.PlayClipAtPoint(conversionSettings.tileSpawnSound, tile.transform.position, 0.5f);
                    }

                    garbage.PlayConvertSound();
                }

                yield return new WaitForSeconds(conversionSettings.timeBetweenTileSpawns);
            }

            // Update garbage block state
            garbage.OnRowConverted();

            // Update anchor position in grid if garbage still exists
            if (!garbage.IsFullyConverted())
            {
                // Re-place remaining garbage in grid with new anchor
                PlaceGarbageInGrid(garbage);
            }
        }

        private GameObject SpawnConvertedTile(int x, int y)
        {
            var currentOffset = _gridRiser?.CurrentGridOffset ?? 0f;
            
            // Use GridManager's position helper
            var pos = _gridManager.GridToWorldPosition(x, y, currentOffset);
            var typeIndex = Random.Range(0, _tileSpawner.tileSprites.Length);
            
            // Parent under GridContainer so tile moves with the grid
            var tile = Instantiate(_tileSpawner.tilePrefab, pos, Quaternion.identity, _gridManager.GridContainer);
            
            var sr = tile.GetComponent<SpriteRenderer>();
            sr.sprite = _tileSpawner.tileSprites[typeIndex];

            var ts = tile.GetComponent<Tile>();
            ts.Initialize(x, y, typeIndex, _gridManager);

            _gridManager.Grid[x, y] = tile;
            
            return tile;
        }

        private void ReleaseHeldTiles()
        {
            _heldTiles.Clear();
            
            // Play completion sound
            if (conversionSettings.conversionCompleteSound != null && _activeGarbage.Count > 0)
            {
                AudioSource.PlayClipAtPoint(conversionSettings.conversionCompleteSound, 
                    _activeGarbage[0].transform.position);
            }
        }

        private void DestroyGarbageBlock(GarbageBlock garbage)
        {
            ClearGarbageFromGrid(garbage);
            _activeGarbage.Remove(garbage);
            _fallingGarbage.Remove(garbage);
            _convertingGarbage.Remove(garbage);

            if (garbage.Cluster != null)
            {
                garbage.Cluster.RemoveBlock(garbage);
            }

            Destroy(garbage.gameObject);
            UpdateClusters();
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            // Clean up null references
            _activeGarbage.RemoveAll(g => g == null);
            _fallingGarbage.RemoveWhere(g => g == null);
            _convertingGarbage.RemoveWhere(g => g == null);
            _heldTiles.RemoveWhere(t => t == null);
        }

        #endregion

        #region Helper Types

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