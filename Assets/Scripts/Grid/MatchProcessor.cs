using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Processes matches: blink animation, scoring, popping, and cascade handling.
    /// Notifies adjacent tiles (for status effect curing and garbage conversion).
    /// </summary>
    public class MatchProcessor : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Match Processing")]
        public float processMatchDuration = 1.5f;
        public float blinkSpeed = 0.15f;
        public float delayBetweenPops = 1.5f;

        public ScoreManager scoreManager;
        public bool isProcessingMatches;

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private GameObject[,] _grid;
        private MatchDetector _matchDetector;
        private readonly HashSet<Vector2Int> _processingTiles = new();

        #endregion

        #region Properties

        public bool IsProcessingMatches => isProcessingMatches;

        #endregion

        #region Initialization

        public void Initialize(GridManager manager, GameObject[,] grid, MatchDetector matchDetector)
        {
            _gridManager = manager;
            _grid = grid;
            _matchDetector = matchDetector;
        }

        #endregion

        #region Public API

        public bool IsTileBeingProcessed(int x, int y)
        {
            return _processingTiles.Contains(new Vector2Int(x, y));
        }

        /// <summary>
        /// Check entire grid for matches and process them.
        /// </summary>
        public IEnumerator CheckAndClearMatches()
        {
            isProcessingMatches = true;
            var matchGroups = _matchDetector.GetMatchGroups();

            while (matchGroups.Count > 0)
            {
                yield return StartCoroutine(ProcessMatchGroups(matchGroups));
                matchGroups = _matchDetector.GetMatchGroups();
            }

            scoreManager?.ResetCombo();
            isProcessingMatches = false;
        }

        /// <summary>
        /// Process a specific list of matched tiles.
        /// </summary>
        public IEnumerator ProcessMatches(List<GameObject> matches)
        {
            if (matches.Count == 0) yield break;

            isProcessingMatches = true;
            var matchGroups = _matchDetector.GroupMatchedTiles(matches);

            yield return StartCoroutine(ProcessMatchGroups(matchGroups));

            // Check for cascade matches
            var cascadeGroups = _matchDetector.GetMatchGroups();
            if (cascadeGroups.Count > 0)
            {
                var cascadeMatches = new List<GameObject>();
                foreach (var group in cascadeGroups)
                    cascadeMatches.AddRange(group);

                yield return StartCoroutine(ProcessMatches(cascadeMatches));
            }
            else
            {
                scoreManager?.ResetCombo();
                isProcessingMatches = false;
            }
        }

        #endregion

        #region Match Processing

        private IEnumerator ProcessMatchGroups(List<List<GameObject>> matchGroups)
        {
            // Mark all tiles as processing
            foreach (var group in matchGroups)
            foreach (var tile in group)
            {
                if (tile == null) continue;
                var ts = tile.GetComponent<Tile>();
                _processingTiles.Add(new Vector2Int(ts.GridX, ts.GridY));
            }

            // Collect all tiles for blinking
            var allMatchedTiles = new List<GameObject>();
            foreach (var group in matchGroups)
                allMatchedTiles.AddRange(group);

            // Notify adjacent tiles before processing (for status effect curing)
            NotifyAdjacentTiles(allMatchedTiles);

            yield return StartCoroutine(BlinkTiles(allMatchedTiles, processMatchDuration));

            // Scoring
            var currentCombo = scoreManager?.GetCombo() ?? 0;
            var totalTiles = 0;
            foreach (var group in matchGroups)
                totalTiles += group.Count;

            scoreManager?.AddScore(totalTiles);

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

                    var adjTs = adjacent.GetComponent<Tile>();
                    if (adjTs == null) continue;

                    var pos = new Vector2Int(adjTs.GridX, adjTs.GridY);
                    if (notifiedPositions.Contains(pos)) continue;

                    adjTs.OnAdjacentMatch();
                    notifiedPositions.Add(pos);
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

                ts.PlayMatchSound(combo);

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