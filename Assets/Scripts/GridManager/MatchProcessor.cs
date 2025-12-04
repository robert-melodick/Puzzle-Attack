using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MatchProcessor : MonoBehaviour
{
    [Header("Match Processing")]
    public float processMatchDuration = 1.5f;
    public float blinkSpeed = 0.15f;
    public float delayBetweenPops = 1.5f;

    public ScoreManager scoreManager;

    private GameObject[,] grid;
    private HashSet<Vector2Int> processingTiles = new HashSet<Vector2Int>();
    public bool isProcessingMatches = false;
    private MatchDetector matchDetector;
    private GridManager gridManager;

    public bool IsProcessingMatches => isProcessingMatches;

    public void Initialize(GridManager manager, GameObject[,] grid, MatchDetector matchDetector)
    {
        this.gridManager = manager;
        this.grid = grid;
        this.matchDetector = matchDetector;
    }

    public bool IsTileBeingProcessed(int x, int y)
    {
        return processingTiles.Contains(new Vector2Int(x, y));
    }

    public void AddBreathingRoom(int tilesMatched)
    {
        // Delegate to GridRiser through GridManager if needed
        // This will be called from GridManager's GridRiser component
    }

    public IEnumerator CheckAndClearMatches()
    {
        isProcessingMatches = true;
        List<List<GameObject>> matchGroups = matchDetector.GetMatchGroups();

        while (matchGroups.Count > 0)
        {
            // Flatten all groups to mark tiles as processing
            foreach (List<GameObject> group in matchGroups)
            {
                foreach (GameObject tile in group)
                {
                    if (tile != null)
                    {
                        Tile tileScript = tile.GetComponent<Tile>();
                        processingTiles.Add(new Vector2Int(tileScript.GridX, tileScript.GridY));
                    }
                }
            }

            // Flatten for blinking
            List<GameObject> allMatchedTiles = new List<GameObject>();
            foreach (List<GameObject> group in matchGroups)
            {
                allMatchedTiles.AddRange(group);
            }

            yield return StartCoroutine(BlinkTiles(allMatchedTiles, processMatchDuration));

            // Get the current combo
            int currentCombo = scoreManager != null ? scoreManager.GetCombo() : 0;

            // Count total tiles across all groups for this match
            int totalTiles = 0;
            foreach (List<GameObject> group in matchGroups)
            {
                totalTiles += group.Count;
            }

            // Add score once for all tiles matched from this action
            if (scoreManager != null)
            {
                scoreManager.AddScore(totalTiles);
            }

            // Add breathing room based on tiles matched
            gridManager.AddBreathingRoom(totalTiles);

            // All groups from this match use the same combo number
            int comboNumber = currentCombo + 1;

            // Pop each group asynchronously with the same combo
            List<Coroutine> popCoroutines = new List<Coroutine>();
            foreach (List<GameObject> group in matchGroups)
            {
                popCoroutines.Add(StartCoroutine(PopTilesInSequence(group, comboNumber)));
            }

            // Wait for all groups to finish popping
            foreach (Coroutine coroutine in popCoroutines)
            {
                yield return coroutine;
            }

            yield return new WaitForSeconds(0.1f);

            // Clear processing tiles now that matched tiles are destroyed
            // This allows falling blocks to not be incorrectly marked as "processed"
            processingTiles.Clear();

            // Drop tiles to fill the gaps
            yield return StartCoroutine(gridManager.DropTiles());

            matchGroups = matchDetector.GetMatchGroups();
        }

        if (scoreManager != null)
        {
            scoreManager.ResetCombo();
        }

        isProcessingMatches = false;
    }

    public IEnumerator ProcessMatches(List<GameObject> matches)
    {
        if (matches.Count == 0) yield break;

        isProcessingMatches = true;

        // Group the matches into separate connected groups
        List<List<GameObject>> matchGroups = matchDetector.GroupMatchedTiles(matches);

        // Mark all tiles as processing
        foreach (List<GameObject> group in matchGroups)
        {
            foreach (GameObject tile in group)
            {
                if (tile != null)
                {
                    Tile tileScript = tile.GetComponent<Tile>();
                    processingTiles.Add(new Vector2Int(tileScript.GridX, tileScript.GridY));
                }
            }
        }

        yield return StartCoroutine(BlinkTiles(matches, processMatchDuration));

        // Get the current combo
        int currentCombo = scoreManager != null ? scoreManager.GetCombo() : 0;

        // Count total tiles across all groups for this match
        int totalTiles = 0;
        foreach (List<GameObject> group in matchGroups)
        {
            totalTiles += group.Count;
        }

        // Add score once for all tiles matched from this action
        if (scoreManager != null)
        {
            scoreManager.AddScore(totalTiles);
        }

        // Add breathing room based on tiles matched
        gridManager.AddBreathingRoom(totalTiles);

        // All groups from this match use the same combo number
        int comboNumber = currentCombo + 1;

        // Pop each group asynchronously with the same combo
        List<Coroutine> popCoroutines = new List<Coroutine>();
        foreach (List<GameObject> group in matchGroups)
        {
            popCoroutines.Add(StartCoroutine(PopTilesInSequence(group, comboNumber)));
        }

        // Wait for all groups to finish popping
        foreach (Coroutine coroutine in popCoroutines)
        {
            yield return coroutine;
        }

        yield return new WaitForSeconds(0.1f);

        // Clear processing tiles now that matched tiles are destroyed
        // This allows falling blocks to not be incorrectly marked as "processed"
        processingTiles.Clear();

        // Drop tiles to fill the gaps
        yield return StartCoroutine(gridManager.DropTiles());

        List<List<GameObject>> cascadeMatchGroups = matchDetector.GetMatchGroups();
        if (cascadeMatchGroups.Count > 0)
        {
            // Flatten and recurse
            List<GameObject> cascadeMatches = new List<GameObject>();
            foreach (List<GameObject> group in cascadeMatchGroups)
            {
                cascadeMatches.AddRange(group);
            }
            yield return StartCoroutine(ProcessMatches(cascadeMatches));
        }
        else
        {
            if (scoreManager != null)
            {
                scoreManager.ResetCombo();
            }
            isProcessingMatches = false;
        }
    }

    IEnumerator BlinkTiles(List<GameObject> tiles, float duration)
    {
        float elapsed = 0f;
        bool isVisible = true;

        List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
        foreach (GameObject tile in tiles)
        {
            if (tile != null)
            {
                SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    spriteRenderers.Add(sr);
                }
            }
        }

        while (elapsed < duration)
        {
            isVisible = !isVisible;
            foreach (SpriteRenderer sr in spriteRenderers)
            {
                if (sr != null)
                {
                    sr.enabled = isVisible;
                }
            }

            yield return new WaitForSeconds(blinkSpeed);
            elapsed += blinkSpeed;
        }

        foreach (SpriteRenderer sr in spriteRenderers)
        {
            if (sr != null)
            {
                sr.enabled = true;
            }
        }
    }

    IEnumerator PopTilesInSequence(List<GameObject> tiles, int combo)
    {
        foreach (GameObject tile in tiles)
        {
            if (tile != null)
            {
                Tile tileScript = tile.GetComponent<Tile>();
                if (tileScript != null)
                {
                    // Play match sound for this tile
                    tileScript.PlayMatchSound(combo);

                    // Remove from grid (but don't destroy yet, so sound can play)
                    grid[tileScript.GridX, tileScript.GridY] = null;

                    // Hide the tile visually
                    SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.enabled = false;
                    }

                    // Wait before next tile (this gives time for sound to play)
                    yield return new WaitForSeconds(delayBetweenPops);

                    // Now destroy the tile
                    Destroy(tile);
                }
            }
        }
    }
}
