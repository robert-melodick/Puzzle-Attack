using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    public class MatchProcessor : MonoBehaviour
    {
        [Header("Match Processing")] public float processMatchDuration = 1.5f;
        public float blinkSpeed = 0.15f;
        public float delayBetweenPops = 1.5f;

        public ScoreManager scoreManager;
        public bool isProcessingMatches;

        private GameObject[,] _grid;
        private GridManager _gridManager;
        private MatchDetector _matchDetector;
        private readonly HashSet<Vector2Int> _processingTiles = new();

        public bool IsProcessingMatches => isProcessingMatches;

        public void Initialize(GridManager manager, GameObject[,] grid, MatchDetector matchDetector)
        {
            _gridManager = manager;
            this._grid = grid;
            this._matchDetector = matchDetector;
        }

        public bool IsTileBeingProcessed(int x, int y)
        {
            return _processingTiles.Contains(new Vector2Int(x, y));
        }

        public void AddBreathingRoom(int tilesMatched)
        {
            // Delegate to GridRiser through GridManager if needed
            // This will be called from GridManager's GridRiser component
        }

        public IEnumerator CheckAndClearMatches()
        {
            isProcessingMatches = true;
            var matchGroups = _matchDetector.GetMatchGroups();

            while (matchGroups.Count > 0)
            {
                // Flatten all groups to mark tiles as processing
                foreach (var group in matchGroups)
                foreach (var tile in group)
                    if (tile != null)
                    {
                        var tileScript = tile.GetComponent<Tile>();
                        _processingTiles.Add(new Vector2Int(tileScript.GridX, tileScript.GridY));
                    }

                // Flatten for blinking
                var allMatchedTiles = new List<GameObject>();
                foreach (var group in matchGroups) allMatchedTiles.AddRange(group);

                yield return StartCoroutine(BlinkTiles(allMatchedTiles, processMatchDuration));

                // Get the current combo
                var currentCombo = scoreManager != null ? scoreManager.GetCombo() : 0;

                // Count total tiles across all groups for this match
                var totalTiles = 0;
                foreach (var group in matchGroups) totalTiles += group.Count;

                // Add score once for all tiles matched from this action
                if (scoreManager != null) scoreManager.AddScore(totalTiles);

                // Add breathing room only for combos (not the first match)
                if (currentCombo > 0)
                {
                    _gridManager.AddBreathingRoom(totalTiles);
                }

                // All groups from this match use the same combo number
                var comboNumber = currentCombo + 1;

                // Pop each group asynchronously with the same combo
                var popCoroutines = new List<Coroutine>();
                foreach (var group in matchGroups)
                    popCoroutines.Add(StartCoroutine(PopTilesInSequence(group, comboNumber)));

                // Wait for all groups to finish popping
                foreach (var coroutine in popCoroutines) yield return coroutine;

                yield return new WaitForSeconds(0.1f);

                // Clear processing tiles now that matched tiles are destroyed
                // This allows falling blocks to not be incorrectly marked as "processed"
                _processingTiles.Clear();

                // Drop tiles to fill the gaps
                yield return StartCoroutine(_gridManager.DropTiles());

                matchGroups = _matchDetector.GetMatchGroups();
            }

            if (scoreManager != null) scoreManager.ResetCombo();

            isProcessingMatches = false;
        }

        public IEnumerator ProcessMatches(List<GameObject> matches)
        {
            if (matches.Count == 0) yield break;

            isProcessingMatches = true;

            // Group the matches into separate connected groups
            var matchGroups = _matchDetector.GroupMatchedTiles(matches);

            // Mark all tiles as processing
            foreach (var group in matchGroups)
            foreach (var tile in group)
                if (tile != null)
                {
                    var tileScript = tile.GetComponent<Tile>();
                    _processingTiles.Add(new Vector2Int(tileScript.GridX, tileScript.GridY));
                }

            yield return StartCoroutine(BlinkTiles(matches, processMatchDuration));

            // Get the current combo
            var currentCombo = scoreManager != null ? scoreManager.GetCombo() : 0;

            // Count total tiles across all groups for this match
            var totalTiles = 0;
            foreach (var group in matchGroups) totalTiles += group.Count;

            // Add score once for all tiles matched from this action
            if (scoreManager != null) scoreManager.AddScore(totalTiles);

            // Add breathing room only for combos (not the first match)
            if (currentCombo > 0)
            {
                _gridManager.AddBreathingRoom(totalTiles);
            }

            // All groups from this match use the same combo number
            var comboNumber = currentCombo + 1;

            // Pop each group asynchronously with the same combo
            var popCoroutines = new List<Coroutine>();
            foreach (var group in matchGroups)
                popCoroutines.Add(StartCoroutine(PopTilesInSequence(group, comboNumber)));

            // Wait for all groups to finish popping
            foreach (var coroutine in popCoroutines) yield return coroutine;

            yield return new WaitForSeconds(0.1f);

            // Clear processing tiles now that matched tiles are destroyed
            // This allows falling blocks to not be incorrectly marked as "processed"
            _processingTiles.Clear();

            // Drop tiles to fill the gaps
            yield return StartCoroutine(_gridManager.DropTiles());

            var cascadeMatchGroups = _matchDetector.GetMatchGroups();
            if (cascadeMatchGroups.Count > 0)
            {
                // Flatten and recurse
                var cascadeMatches = new List<GameObject>();
                foreach (var group in cascadeMatchGroups) cascadeMatches.AddRange(group);

                yield return StartCoroutine(ProcessMatches(cascadeMatches));
            }
            else
            {
                if (scoreManager != null) scoreManager.ResetCombo();

                isProcessingMatches = false;
            }
        }

        private IEnumerator BlinkTiles(List<GameObject> tiles, float duration)
        {
            var elapsed = 0f;
            var isVisible = true;

            var spriteRenderers = new List<SpriteRenderer>();
            foreach (var tile in tiles)
                if (tile != null)
                {
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
                if (tile != null)
                {
                    var tileScript = tile.GetComponent<Tile>();
                    if (tileScript != null)
                    {
                        // Play match sound for this tile
                        tileScript.PlayMatchSound(combo);

                        // Remove from grid (but don't destroy yet, so sound can play)
                        _grid[tileScript.GridX, tileScript.GridY] = null;

                        // Hide the tile visually
                        var sr = tile.GetComponent<SpriteRenderer>();
                        if (sr != null) sr.enabled = false;

                        // Wait before next tile (this gives time for sound to play)
                        yield return new WaitForSeconds(delayBetweenPops);

                        // Now destroy the tile
                        Destroy(tile);
                    }
                }
        }
    }
}