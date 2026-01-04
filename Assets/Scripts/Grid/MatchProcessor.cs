using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Processes matches: blink animation, scoring, popping, and cascade handling.
    /// Notifies adjacent tiles (for status effect curing) and garbage blocks (for conversion).
    /// Tracks chain state for ScoreManager integration.
    /// </summary>
    public class MatchProcessor : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Match Processing")]
        public float processMatchDuration = 1.5f;
        public float blinkSpeed = 0.15f;
        public float delayBetweenPops = 1.5f;

        [Header("References")]
        public ScoreManager scoreManager;
        public GarbageManager garbageManager;

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private GameObject[,] _grid;
        private MatchDetector _matchDetector;
        private readonly HashSet<Vector2Int> _processingTiles = new();
        private bool _isProcessingMatches;
        
        // Chain tracking
        private bool _isProcessingCascade;
        private int _currentChainLevel;

        #endregion

        #region Properties

        public bool IsProcessingMatches => _isProcessingMatches;
        
        /// <summary>
        /// Current chain level (1 = first match, 2+ = cascade matches).
        /// </summary>
        public int CurrentChainLevel => _currentChainLevel;

        #endregion

        #region Initialization

        public void Initialize(GridManager manager, GameObject[,] grid, MatchDetector matchDetector, 
            GarbageManager garbageMgr = null, ScoreManager scoreMgr = null)
        {
            _gridManager = manager;
            _grid = grid;
            _matchDetector = matchDetector;

            if (garbageMgr != null)
            {
                garbageManager = garbageMgr;
            }
            
            if (scoreMgr != null)
            {
                scoreManager = scoreMgr;
            }
            
            // Auto-detect ScoreManager if not provided
            if (scoreManager == null)
            {
                scoreManager = GetComponent<ScoreManager>() ?? GetComponentInParent<ScoreManager>();
            }
            
            Debug.Log($"[MatchProcessor] Initialized with ScoreManager: {scoreManager?.GetInstanceID()}");
        }

        #endregion

        #region Public API

        public bool IsTileBeingProcessed(int x, int y)
        {
            return _processingTiles.Contains(new Vector2Int(x, y));
        }

        /// <summary>
        /// Check entire grid for matches and process them.
        /// Used during initialization to clear starting matches.
        /// </summary>
        public IEnumerator CheckAndClearMatches()
        {
            _isProcessingMatches = true;
            _isProcessingCascade = false;
            _currentChainLevel = 0;
            
            var matchGroups = _matchDetector.GetMatchGroups();

            while (matchGroups.Count > 0)
            {
                yield return StartCoroutine(ProcessMatchGroups(matchGroups));
                
                // Any subsequent matches are cascades
                _isProcessingCascade = true;
                matchGroups = _matchDetector.GetMatchGroups();
            }

            // End combo when all matches are done
            scoreManager?.ResetCombo();
            
            _isProcessingMatches = false;
            _isProcessingCascade = false;
            _currentChainLevel = 0;
        }

        /// <summary>
        /// Process a specific list of matched tiles.
        /// This is the main entry point for player-triggered matches.
        /// </summary>
        public IEnumerator ProcessMatches(List<GameObject> matches)
        {
            if (matches.Count == 0) yield break;

            _isProcessingMatches = true;
            _isProcessingCascade = false;
            _currentChainLevel = 1;
            
            var matchGroups = _matchDetector.GroupMatchedTiles(matches);

            yield return StartCoroutine(ProcessMatchGroups(matchGroups));

            // Check for cascade matches
            _isProcessingCascade = true;
            
            var cascadeGroups = _matchDetector.GetMatchGroups();
            while (cascadeGroups.Count > 0)
            {
                // Increment chain level for each cascade
                _currentChainLevel++;
                
                Debug.Log($"[MatchProcessor] Chain x{_currentChainLevel} detected!");
                
                var cascadeMatches = new List<GameObject>();
                foreach (var group in cascadeGroups)
                    cascadeMatches.AddRange(group);

                var cascadeMatchGroups = _matchDetector.GroupMatchedTiles(cascadeMatches);
                yield return StartCoroutine(ProcessMatchGroups(cascadeMatchGroups));
                
                // Check for more cascades
                cascadeGroups = _matchDetector.GetMatchGroups();
            }

            // All matches complete - end combo
            scoreManager?.ResetCombo();
            
            // Notify score manager that drop completed with no more matches
            scoreManager?.NotifyDropComplete(false);
            
            _isProcessingMatches = false;
            _isProcessingCascade = false;
            _currentChainLevel = 0;
        }

        #endregion

        #region Match Processing

        private IEnumerator ProcessMatchGroups(List<List<GameObject>> matchGroups)
        {
            // Collect all match positions for garbage notification
            var allMatchPositions = new List<Vector2Int>();

            // Mark all tiles as processing
            foreach (var group in matchGroups)
            {
                foreach (var tile in group)
                {
                    if (tile == null) continue;
                    var ts = tile.GetComponent<Tile>();
                    if (ts != null)
                    {
                        var pos = new Vector2Int(ts.GridX, ts.GridY);
                        _processingTiles.Add(pos);
                        allMatchPositions.Add(pos);
                    }
                }
            }

            // Collect all tiles for blinking
            var allMatchedTiles = new List<GameObject>();
            foreach (var group in matchGroups)
                allMatchedTiles.AddRange(group);

            // Notify adjacent tiles before processing (for status effect curing)
            NotifyAdjacentTiles(allMatchedTiles);

            // Notify garbage manager of adjacent matches
            if (garbageManager != null && allMatchPositions.Count > 0)
            {
                garbageManager.OnMatchAdjacentToGarbage(allMatchPositions);
            }

            yield return StartCoroutine(BlinkTiles(allMatchedTiles, processMatchDuration));

            // Get current combo before scoring (for breathing room calculation)
            var currentCombo = scoreManager?.GetCombo() ?? 0;

            // Process each match group separately for scoring and garbage
            // Panel de Pon style: each group contributes garbage based on its own size
            foreach (var group in matchGroups)
            {
                int groupSize = group.Count;
                // Add score for this group
                // isChainMatch is true if this is a cascade (not the initial player-triggered match)
                // numGroups = 1 since we're processing each group individually
                scoreManager?.AddScore(groupSize, _isProcessingCascade, 1);
            }

            // Calculate total tiles for breathing room
            var totalTiles = 0;
            foreach (var group in matchGroups)
                totalTiles += group.Count;

            // Breathing room for combos (not first match)
            if (currentCombo > 0)
                _gridManager.AddBreathingRoom(totalTiles);

            var comboNumber = currentCombo + 1;

            // Pop each group
            var popCoroutines = new List<Coroutine>();
            foreach (var group in matchGroups)
                popCoroutines.Add(StartCoroutine(PopTilesInSequence(group, comboNumber)));

            foreach (var coroutine in popCoroutines)
                yield return coroutine;

            yield return new WaitForSeconds(0.1f);

            _processingTiles.Clear();

            // Wait for garbage conversion to complete before dropping
            if (garbageManager != null)
            {
                while (garbageManager.IsProcessingConversion)
                {
                    yield return null;
                }
            }

            // Notify score manager that tiles are about to fall
            scoreManager?.NotifyTilesFalling();

            yield return StartCoroutine(_gridManager.DropTiles());
        }

        /// <summary>
        /// Notify tiles adjacent to matched tiles (for curing burning status, converting garbage).
        /// </summary>
        private void NotifyAdjacentTiles(List<GameObject> matchedTiles)
        {
            var notifiedPositions = new HashSet<Vector2Int>();

            foreach (var matchedTile in matchedTiles)
            {
                if (matchedTile == null) continue;
                var ts = matchedTile.GetComponent<Tile>();
                if (ts == null) continue;

                var adjacentTiles = _matchDetector.GetAdjacentTiles(ts.GridX, ts.GridY);
                foreach (var adjacent in adjacentTiles)
                {
                    if (adjacent == null || matchedTiles.Contains(adjacent)) continue;

                    // Check if it's a regular tile
                    var adjTs = adjacent.GetComponent<Tile>();
                    if (adjTs != null)
                    {
                        var pos = new Vector2Int(adjTs.GridX, adjTs.GridY);
                        if (notifiedPositions.Contains(pos)) continue;

                        adjTs.OnAdjacentMatch();
                        notifiedPositions.Add(pos);
                    }
                }
            }
        }

        private IEnumerator BlinkTiles(List<GameObject> tiles, float duration)
        {
            var elapsed = 0f;
            var isVisible = true;

            var spriteRenderers = new List<SpriteRenderer>();
            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                var sr = tile.GetComponent<SpriteRenderer>();
                if (sr != null) spriteRenderers.Add(sr);
            }

            while (elapsed < duration)
            {
                isVisible = !isVisible;
                foreach (var sr in spriteRenderers)
                    if (sr != null)
                        sr.enabled = isVisible;

                yield return new WaitForSeconds(blinkSpeed);
                elapsed += blinkSpeed;
            }

            foreach (var sr in spriteRenderers)
                if (sr != null)
                    sr.enabled = true;
        }

        private IEnumerator PopTilesInSequence(List<GameObject> tiles, int combo)
        {
            foreach (var tile in tiles)
            {
                if (tile == null) continue;

                var ts = tile.GetComponent<Tile>();
                if (ts == null) continue;

                // Pass chain level to sound system for escalating pitch/effects
                ts.PlayMatchSound(combo, _currentChainLevel);

                // Remove from grid
                _grid[ts.GridX, ts.GridY] = null;

                // Hide visually
                var sr = tile.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = false;

                yield return new WaitForSeconds(delayBetweenPops);

                Destroy(tile);
            }
        }

        #endregion
    }
}