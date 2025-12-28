using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid.AI
{
    /// <summary>
    /// The AI's decision-making system. Evaluates the grid and finds good swaps.
    /// Pure logic class - no MonoBehaviour dependencies.
    /// </summary>
    public class AIBrain
    {
        private readonly AIDifficultySettings _settings;
        private readonly GridManager _gridManager;
        private readonly MatchDetector _matchDetector;
        private readonly DangerZoneManager _dangerZoneManager;
        private readonly GarbageManager _garbageManager;

        // Cache for swap evaluation
        private readonly List<AISwapCandidate> _candidateCache = new List<AISwapCandidate>(64);

        // Random instance for deterministic behavior when seeded
        private System.Random _random;

        public AIBrain(
            AIDifficultySettings settings,
            GridManager gridManager,
            MatchDetector matchDetector,
            DangerZoneManager dangerZoneManager,
            GarbageManager garbageManager,
            int? seed = null)
        {
            _settings = settings;
            _gridManager = gridManager;
            _matchDetector = matchDetector;
            _dangerZoneManager = dangerZoneManager;
            _garbageManager = garbageManager;

            // Use seeded random for deterministic AI decisions if seed provided
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        /// <summary>
        /// Re-seed the random number generator (for match restarts, etc.)
        /// </summary>
        public void SetSeed(int seed)
        {
            _random = new System.Random(seed);
        }

        /// <summary>
        /// Get a random float between 0 and 1 (useful for probability checks).
        /// </summary>
        public float GetRandomFloat()
        {
            return (float)_random.NextDouble();
        }

        /// <summary>
        /// Analyze the grid and return the best swap candidate.
        /// </summary>
        public AISwapCandidate FindBestSwap(bool isPanicking)
        {
            _candidateCache.Clear();

            int width = _gridManager.Width;
            int height = _gridManager.Height;

            // Evaluate every possible horizontal swap
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    // Skip if swap isn't valid
                    if (!CanSwapAt(x, y))
                        continue;

                    var candidate = EvaluateSwap(x, y, isPanicking);

                    // Only consider swaps above minimum threshold
                    if (candidate.Score >= _settings.minimumSwapScore)
                    {
                        _candidateCache.Add(candidate);
                    }
                }
            }

            if (_candidateCache.Count == 0)
                return AISwapCandidate.Invalid;

            // Sort by score descending
            _candidateCache.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Maybe "miss" an obvious match (humanization)
            if (_settings.missObviousMatchChance > 0 && RandomFloat() < _settings.missObviousMatchChance)
            {
                // Skip the best candidate if there are alternatives
                if (_candidateCache.Count > 1)
                {
                    _candidateCache.RemoveAt(0);
                }
            }

            // Maybe pick a suboptimal move for humanization
            float mistakeChance = _settings.GetEffectiveMistakeChance(isPanicking);

            if (_candidateCache.Count > 1 && RandomFloat() < mistakeChance)
            {
                // Pick from top candidates instead of absolute best
                int maxIndex = Mathf.Min(_candidateCache.Count - 1, RandomRange(1, 5));
                int chosenIndex = RandomRange(1, maxIndex + 1);
                return _candidateCache[chosenIndex];
            }

            return _candidateCache[0];
        }

        /// <summary>
        /// Check if a swap at this position is currently valid.
        /// </summary>
        private bool CanSwapAt(int x, int y)
        {
            var grid = _gridManager.Grid;
            var leftTile = grid[x, y];
            var rightTile = grid[x + 1, y];

            // Need at least one tile to swap
            if (leftTile == null && rightTile == null)
                return false;

            // Check garbage - can't swap garbage blocks
            if (_gridManager.IsGarbageAt(x, y) || _gridManager.IsGarbageAt(x + 1, y))
                return false;

            // Check if tiles can swap (status effects, etc.)
            if (!_gridManager.CanTileSwap(x, y) || !_gridManager.CanTileSwap(x + 1, y))
                return false;

            // Check if tiles are animating
            if (leftTile != null && _gridManager.IsTileAnimating(leftTile))
                return false;
            if (rightTile != null && _gridManager.IsTileAnimating(rightTile))
                return false;

            // Check if tiles are being processed for matches
            var matchProcessor = _gridManager.matchProcessor;
            if (matchProcessor != null)
            {
                if (matchProcessor.IsTileBeingProcessed(x, y) || matchProcessor.IsTileBeingProcessed(x + 1, y))
                    return false;
            }

            // Check if tiles are held during garbage conversion
            if (_garbageManager != null)
            {
                if (leftTile != null && _garbageManager.IsTileHeld(leftTile))
                    return false;
                if (rightTile != null && _garbageManager.IsTileHeld(rightTile))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Evaluate a potential swap and return a scored candidate.
        /// </summary>
        private AISwapCandidate EvaluateSwap(int x, int y, bool isPanicking)
        {
            var candidate = new AISwapCandidate
            {
                Position = new Vector2Int(x, y),
                Score = 0f
            };

            var grid = _gridManager.Grid;
            var leftTile = grid[x, y];
            var rightTile = grid[x + 1, y];

            // Get tile types
            int leftType = GetTileType(leftTile);
            int rightType = GetTileType(rightTile);

            // Skip if both tiles are the same type (no point swapping)
            if (leftType == rightType && leftType >= 0)
                return candidate; // Score stays 0

            // Simulate the swap and count matches
            int matchCount = CountMatchesAfterSwap(x, y, leftType, rightType);
            candidate.ImmediateMatchCount = matchCount;

            // Base score from immediate matches
            if (matchCount >= 3)
            {
                candidate.Score += matchCount * 10f;

                // Bonus for longer matches
                if (matchCount >= 4) candidate.Score += 15f;
                if (matchCount >= 5) candidate.Score += 25f;
                if (matchCount >= 6) candidate.Score += 40f; // Rare but powerful
            }

            // Chain potential (if lookahead enabled)
            int lookahead = _settings.GetEffectiveLookahead(isPanicking);
            if (lookahead > 1 && matchCount >= 3)
            {
                int chainDepth = EstimateChainDepth(x, y, leftType, rightType);
                candidate.EstimatedChainLength = chainDepth;

                if (chainDepth > 1)
                {
                    float chainBonus = chainDepth * 20f * _settings.setupVsGreedBias;
                    candidate.Score += chainBonus;

                    // Estimate garbage sent (for VS mode consideration)
                    candidate.EstimatedGarbageSent = EstimateGarbageSent(matchCount, chainDepth);
                    
                    // Aggression bonus
                    if (candidate.EstimatedGarbageSent > 0)
                    {
                        candidate.Score += candidate.EstimatedGarbageSent * 5f * _settings.aggressionBias;
                    }
                }
            }
            else
            {
                candidate.EstimatedChainLength = matchCount >= 3 ? 1 : 0;
            }

            // Garbage clearing priority
            bool clearsGarbage = CheckIfClearsGarbage(x, y, leftType, rightType, matchCount);
            candidate.ClearsGarbage = clearsGarbage;
            if (clearsGarbage)
            {
                candidate.Score += 30f * _settings.garbageClearingWeight;
            }

            // Danger zone consideration
            bool inDangerZone = _dangerZoneManager != null &&
                               (_dangerZoneManager.IsCellInDangerZone(x, y) ||
                                _dangerZoneManager.IsCellInDangerZone(x + 1, y));
            candidate.IsInDangerZone = inDangerZone;

            if (inDangerZone && matchCount >= 3)
            {
                // Prioritize clearing danger zone
                candidate.Score += 25f * _settings.safetyWeight;
            }
            else if (inDangerZone && matchCount == 0)
            {
                // Penalize non-matching swaps in danger zone (wasting time)
                candidate.Score -= 10f * _settings.safetyWeight;
            }

            // Setup value (swaps that don't match now but enable future chains)
            if (matchCount == 0 && _settings.setupVsGreedBias > 0.3f && !isPanicking)
            {
                float setupValue = EvaluateSetupPotential(x, y, leftType, rightType);
                candidate.Score += setupValue * _settings.setupVsGreedBias;
            }

            return candidate;
        }

        /// <summary>
        /// Count how many tiles would match if we performed this swap.
        /// </summary>
        private int CountMatchesAfterSwap(int x, int y, int leftType, int rightType)
        {
            // Swap moves leftTile to x+1, rightTile to x
            int totalMatches = 0;

            // Check if leftTile (now at x+1) creates matches
            if (leftType >= 0)
            {
                totalMatches += CountMatchesForTileAt(leftType, x + 1, y, x, y);
            }

            // Check if rightTile (now at x) creates matches
            if (rightType >= 0)
            {
                totalMatches += CountMatchesForTileAt(rightType, x, y, x + 1, y);
            }

            return totalMatches;
        }

        /// <summary>
        /// Count matches a tile of given type would create at position.
        /// excludeX, excludeY is the position to ignore (the other swap tile's original position).
        /// </summary>
        private int CountMatchesForTileAt(int tileType, int x, int y, int excludeX, int excludeY)
        {
            int width = _gridManager.Width;
            int height = _gridManager.Height;

            // Horizontal matches
            int horzCount = 1;

            // Check left
            for (int checkX = x - 1; checkX >= 0; checkX--)
            {
                if (checkX == excludeX && y == excludeY) break; // Don't count through the swap
                if (GetTileTypeAt(checkX, y) == tileType)
                    horzCount++;
                else
                    break;
            }

            // Check right
            for (int checkX = x + 1; checkX < width; checkX++)
            {
                if (checkX == excludeX && y == excludeY) break;
                if (GetTileTypeAt(checkX, y) == tileType)
                    horzCount++;
                else
                    break;
            }

            int matchCount = 0;
            if (horzCount >= 3)
                matchCount += horzCount;

            // Vertical matches
            int vertCount = 1;

            // Check down
            for (int checkY = y - 1; checkY >= 0; checkY--)
            {
                if (x == excludeX && checkY == excludeY) break;
                if (GetTileTypeAt(x, checkY) == tileType)
                    vertCount++;
                else
                    break;
            }

            // Check up
            for (int checkY = y + 1; checkY < height; checkY++)
            {
                if (x == excludeX && checkY == excludeY) break;
                if (GetTileTypeAt(x, checkY) == tileType)
                    vertCount++;
                else
                    break;
            }

            if (vertCount >= 3)
                matchCount += vertCount;

            return matchCount;
        }

        /// <summary>
        /// Estimate chain depth (how many cascading matches would occur).
        /// </summary>
        private int EstimateChainDepth(int x, int y, int leftType, int rightType)
        {
            int chainDepth = 1;
            var grid = _gridManager.Grid;
            int height = _gridManager.Height;
            int width = _gridManager.Width;

            // Heuristic: Check for tiles above the match area that could cascade
            // Also check for "stacked" same-color tiles that might align after drops

            int matchStartX = Mathf.Max(0, x - 2);
            int matchEndX = Mathf.Min(width - 1, x + 3);

            // Count potential cascade opportunities
            int cascadePotential = 0;

            for (int checkX = matchStartX; checkX <= matchEndX; checkX++)
            {
                bool foundGap = false;
                int tilesAboveGap = 0;

                for (int checkY = y; checkY < Mathf.Min(height, y + 5); checkY++)
                {
                    var tile = grid[checkX, checkY];

                    if (tile == null)
                    {
                        foundGap = true;
                    }
                    else if (foundGap && !_gridManager.IsGarbageAt(checkX, checkY))
                    {
                        tilesAboveGap++;
                    }
                }

                if (tilesAboveGap > 0)
                    cascadePotential++;
            }

            // More sophisticated check: look for same-color clusters that would align
            if (cascadePotential >= 2)
                chainDepth = 2;
            if (cascadePotential >= 4)
                chainDepth = 3;
            if (cascadePotential >= 6)
                chainDepth = 4;

            return Mathf.Min(chainDepth, _settings.chainLookaheadDepth);
        }

        /// <summary>
        /// Estimate garbage lines sent based on match size and chain depth.
        /// Based on typical Panel de Pon garbage tables.
        /// </summary>
        private int EstimateGarbageSent(int matchCount, int chainDepth)
        {
            // Base garbage from match size
            int baseGarbage = 0;
            if (matchCount >= 4) baseGarbage = 1;
            if (matchCount >= 5) baseGarbage = 2;
            if (matchCount >= 6) baseGarbage = 3;

            // Chain bonus (exponential-ish)
            int chainGarbage = 0;
            if (chainDepth >= 2) chainGarbage = 1;
            if (chainDepth >= 3) chainGarbage = 2;
            if (chainDepth >= 4) chainGarbage = 4;
            if (chainDepth >= 5) chainGarbage = 6;

            return baseGarbage + chainGarbage;
        }

        /// <summary>
        /// Check if this swap would clear or help clear garbage.
        /// </summary>
        private bool CheckIfClearsGarbage(int x, int y, int leftType, int rightType, int matchCount)
        {
            if (matchCount < 3) return false;
            if (_garbageManager == null || !_garbageManager.HasActiveGarbage) return false;

            // Check if either swap position is adjacent to garbage
            var directions = new Vector2Int[]
            {
                new Vector2Int(-1, 0), // Left of left tile
                new Vector2Int(2, 0),  // Right of right tile
                new Vector2Int(0, -1), // Below left tile
                new Vector2Int(0, 1),  // Above left tile
                new Vector2Int(1, -1), // Below right tile
                new Vector2Int(1, 1)   // Above right tile
            };

            foreach (var dir in directions)
            {
                int checkX = x + dir.x;
                int checkY = y + dir.y;

                if (checkX >= 0 && checkX < _gridManager.Width &&
                    checkY >= 0 && checkY < _gridManager.Height)
                {
                    if (_gridManager.IsGarbageAt(checkX, checkY))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Evaluate setup potential - swaps that position tiles for future chains.
        /// </summary>
        private float EvaluateSetupPotential(int x, int y, int leftType, int rightType)
        {
            float setupValue = 0f;

            // Check if this swap creates 2-in-a-row setups
            if (leftType >= 0)
            {
                int adjacentCount = CountAdjacentSameType(leftType, x + 1, y, x, y);
                if (adjacentCount == 2) // Creates a 2-in-a-row
                    setupValue += 5f;
            }

            if (rightType >= 0)
            {
                int adjacentCount = CountAdjacentSameType(rightType, x, y, x + 1, y);
                if (adjacentCount == 2)
                    setupValue += 5f;
            }

            return setupValue;
        }

        private int CountAdjacentSameType(int tileType, int x, int y, int excludeX, int excludeY)
        {
            int count = 0;
            var directions = new Vector2Int[]
            {
                new Vector2Int(-1, 0), new Vector2Int(1, 0),
                new Vector2Int(0, -1), new Vector2Int(0, 1)
            };

            foreach (var dir in directions)
            {
                int checkX = x + dir.x;
                int checkY = y + dir.y;

                if (checkX == excludeX && checkY == excludeY)
                    continue;

                if (GetTileTypeAt(checkX, checkY) == tileType)
                    count++;
            }

            return count;
        }

        private int GetTileType(GameObject tileObj)
        {
            if (tileObj == null) return -1;
            var tile = tileObj.GetComponent<Tile>();
            return tile != null ? tile.TileType : -1;
        }

        private int GetTileTypeAt(int x, int y)
        {
            if (x < 0 || x >= _gridManager.Width || y < 0 || y >= _gridManager.Height)
                return -1;

            return GetTileType(_gridManager.Grid[x, y]);
        }

        // Deterministic random helpers
        private float RandomFloat()
        {
            return (float)_random.NextDouble();
        }

        private int RandomRange(int min, int maxExclusive)
        {
            return _random.Next(min, maxExclusive);
        }
    }
}