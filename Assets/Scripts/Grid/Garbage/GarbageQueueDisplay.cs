using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Displays a preview of pending garbage blocks above a player's grid.
    /// Shows queued garbage as small block representations.
    /// </summary>
    public class GarbageQueueDisplay : MonoBehaviour
    {
        #region Inspector Fields

        [Header("References")]
        [Tooltip("The GridManager this display is for")]
        public GridManager gridManager;
        
        [Tooltip("The GarbageRouter to get pending garbage info from")]
        public GarbageRouter garbageRouter;
        
        [Tooltip("The player index in the GarbageRouter (0 = player 1, 1 = player 2, etc.)")]
        public int playerIndex = 0;

        [Header("Display Settings")]
        [Tooltip("Vertical offset above the grid (in tiles)")]
        public float verticalOffset = 1.5f;
        
        [Tooltip("Horizontal padding from grid edge")]
        public float horizontalPadding = 0.25f;
        
        [Tooltip("Scale of preview blocks relative to tile size")]
        public float previewScale = 0.4f;
        
        [Tooltip("Gap between preview blocks")]
        public float blockGap = 0.1f;
        
        [Tooltip("Maximum blocks to show (older ones get hidden)")]
        public int maxVisibleBlocks = 8;
        
        [Tooltip("Whether to align blocks to the left, center, or right")]
        public Alignment alignment = Alignment.Right;

        [Header("Appearance")]
        [Tooltip("Color for small garbage (1 row)")]
        public Color smallGarbageColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        
        [Tooltip("Color for medium garbage (2 rows)")]
        public Color mediumGarbageColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        [Tooltip("Color for large garbage (3+ rows)")]
        public Color largeGarbageColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        
        [Tooltip("Color for dangerous garbage (4+ rows)")]
        public Color dangerousGarbageColor = new Color(0.8f, 0.3f, 0.3f, 1f);
        
        [Tooltip("Sprite to use for preview blocks (if null, uses a generated square)")]
        public Sprite previewSprite;
        
        [Tooltip("Sorting layer for preview blocks")]
        public string sortingLayer = "Default";
        
        [Tooltip("Sorting order for preview blocks")]
        public int sortingOrder = 20;

        [Header("Animation")]
        [Tooltip("Whether to animate new blocks appearing")]
        public bool animateNewBlocks = true;
        
        [Tooltip("Duration of the appear animation")]
        public float appearDuration = 0.2f;
        
        [Tooltip("Whether blocks pulse when about to be delivered")]
        public bool pulseWhenImminent = true;
        
        [Tooltip("Pulse speed (cycles per second)")]
        public float pulseSpeed = 3f;

        #endregion

        #region Enums

        public enum Alignment
        {
            Left,
            Center,
            Right
        }

        #endregion

        #region Private Fields

        private readonly List<GarbagePreviewBlock> _previewBlocks = new();
        private Transform _container;
        private float _tileSize;
        private int _lastPendingCount;
        private int _lastIncomingGarbage;
        private bool _isInitialized;

        // Generated sprite for when none is provided
        private Sprite _generatedSprite;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_isInitialized) return;

            UpdateDisplay();
            UpdateAnimations();
        }

        private void OnDestroy()
        {
            if (_generatedSprite != null)
            {
                Destroy(_generatedSprite.texture);
                Destroy(_generatedSprite);
            }
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            // Auto-detect GridManager if not assigned
            if (gridManager == null)
                gridManager = GetComponentInParent<GridManager>();

            if (gridManager == null)
            {
                Debug.LogError("[GarbageQueueDisplay] No GridManager found!");
                return;
            }

            _tileSize = gridManager.TileSize;

            // Create container for preview blocks
            var containerObj = new GameObject("GarbageQueueContainer");
            containerObj.transform.SetParent(gridManager.GridContainer);
            containerObj.transform.localPosition = Vector3.zero;
            _container = containerObj.transform;

            // Generate sprite if none provided
            if (previewSprite == null)
            {
                _generatedSprite = GeneratePreviewSprite();
                previewSprite = _generatedSprite;
            }

            _isInitialized = true;
            
            Debug.Log($"[GarbageQueueDisplay] Initialized for {gridManager.name}, player index {playerIndex}");
        }

        private Sprite GeneratePreviewSprite()
        {
            // Create a simple rounded square sprite
            int size = 32;
            var texture = new Texture2D(size, size);
            var pixels = new Color[size * size];

            int cornerRadius = 4;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Check if pixel is inside rounded rectangle
                    bool inside = true;
                    
                    // Check corners
                    if (x < cornerRadius && y < cornerRadius)
                        inside = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, cornerRadius)) <= cornerRadius;
                    else if (x >= size - cornerRadius && y < cornerRadius)
                        inside = Vector2.Distance(new Vector2(x, y), new Vector2(size - cornerRadius - 1, cornerRadius)) <= cornerRadius;
                    else if (x < cornerRadius && y >= size - cornerRadius)
                        inside = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, size - cornerRadius - 1)) <= cornerRadius;
                    else if (x >= size - cornerRadius && y >= size - cornerRadius)
                        inside = Vector2.Distance(new Vector2(x, y), new Vector2(size - cornerRadius - 1, size - cornerRadius - 1)) <= cornerRadius;

                    pixels[y * size + x] = inside ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        #endregion

        #region Display Update

        private void UpdateDisplay()
        {
            if (garbageRouter == null) return;

            int pendingIncoming = garbageRouter.GetPendingIncomingGarbage(playerIndex);
            
            // Also check the GarbageManager's internal queue
            int queuedInManager = 0;
            if (gridManager.garbageManager != null)
            {
                queuedInManager = gridManager.garbageManager.GetPendingGarbageCount();
            }

            int totalPending = pendingIncoming + queuedInManager;

            // Only rebuild if count changed
            if (totalPending != _lastIncomingGarbage)
            {
                _lastIncomingGarbage = totalPending;
                RebuildPreviewBlocks(totalPending);
            }

            // Update positions (in case grid moved)
            PositionBlocks();
        }

        private void RebuildPreviewBlocks(int totalGarbageLines)
        {
            // Clear existing blocks
            foreach (var block in _previewBlocks)
            {
                if (block.GameObject != null)
                    Destroy(block.GameObject);
            }
            _previewBlocks.Clear();

            if (totalGarbageLines <= 0) return;

            // Convert total lines into visual blocks
            // We'll represent garbage as blocks of varying sizes
            int remaining = totalGarbageLines;
            var blockSizes = new List<int>();

            while (remaining > 0)
            {
                if (remaining >= 4)
                {
                    blockSizes.Add(4);
                    remaining -= 4;
                }
                else if (remaining >= 2)
                {
                    blockSizes.Add(2);
                    remaining -= 2;
                }
                else
                {
                    blockSizes.Add(1);
                    remaining -= 1;
                }
            }

            // Limit visible blocks
            int startIndex = Mathf.Max(0, blockSizes.Count - maxVisibleBlocks);

            for (int i = startIndex; i < blockSizes.Count; i++)
            {
                var block = CreatePreviewBlock(blockSizes[i], i - startIndex, animateNewBlocks);
                _previewBlocks.Add(block);
            }
        }

        private GarbagePreviewBlock CreatePreviewBlock(int size, int index, bool animate)
        {
            var obj = new GameObject($"GarbagePreview_{index}");
            obj.transform.SetParent(_container);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = previewSprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder;
            sr.color = GetColorForSize(size);

            // Scale based on size (taller blocks are slightly larger)
            float heightScale = 1f + (size - 1) * 0.3f;
            float blockScale = _tileSize * previewScale;
            obj.transform.localScale = new Vector3(blockScale, blockScale * heightScale, 1f);

            var block = new GarbagePreviewBlock
            {
                GameObject = obj,
                Renderer = sr,
                Size = size,
                Index = index,
                TargetScale = obj.transform.localScale,
                BaseColor = sr.color
            };

            if (animate)
            {
                block.AnimationTimer = 0f;
                block.IsAnimating = true;
                obj.transform.localScale = Vector3.zero;
            }

            return block;
        }

        private Color GetColorForSize(int size)
        {
            return size switch
            {
                1 => smallGarbageColor,
                2 => mediumGarbageColor,
                3 => largeGarbageColor,
                _ => dangerousGarbageColor
            };
        }

        private void PositionBlocks()
        {
            if (_previewBlocks.Count == 0) return;

            float gridWidth = gridManager.Width * _tileSize;
            float gridHeight = gridManager.Height * _tileSize;
            float blockSize = _tileSize * previewScale;
            
            // Calculate total width of all blocks
            float totalWidth = 0f;
            foreach (var block in _previewBlocks)
            {
                totalWidth += blockSize + blockGap;
            }
            totalWidth -= blockGap; // Remove last gap

            // Calculate starting X position based on alignment
            float startX;
            switch (alignment)
            {
                case Alignment.Left:
                    startX = horizontalPadding;
                    break;
                case Alignment.Center:
                    startX = (gridWidth - totalWidth) / 2f;
                    break;
                case Alignment.Right:
                default:
                    startX = gridWidth - totalWidth - horizontalPadding;
                    break;
            }

            // Y position above the grid
            float yPos = gridHeight + (verticalOffset * _tileSize);

            // Position each block
            float currentX = startX;
            foreach (var block in _previewBlocks)
            {
                if (block.GameObject == null) continue;

                float heightScale = 1f + (block.Size - 1) * 0.3f;
                float blockHeight = blockSize * heightScale;
                
                block.GameObject.transform.localPosition = new Vector3(
                    currentX + blockSize / 2f,
                    yPos + blockHeight / 2f,
                    -0.1f
                );

                currentX += blockSize + blockGap;
            }
        }

        #endregion

        #region Animation

        private void UpdateAnimations()
        {
            bool isImminent = false;
            if (garbageRouter != null)
            {
                isImminent = !garbageRouter.IsPlayerInCombo(playerIndex) && _lastIncomingGarbage > 0;
            }

            foreach (var block in _previewBlocks)
            {
                if (block.GameObject == null) continue;

                // Appear animation
                if (block.IsAnimating)
                {
                    block.AnimationTimer += Time.deltaTime;
                    float progress = Mathf.Clamp01(block.AnimationTimer / appearDuration);
                    
                    // Ease out back for a nice pop effect
                    float easedProgress = EaseOutBack(progress);
                    block.GameObject.transform.localScale = block.TargetScale * easedProgress;

                    if (progress >= 1f)
                    {
                        block.IsAnimating = false;
                    }
                }

                // Pulse animation when about to be delivered
                if (pulseWhenImminent && isImminent && !block.IsAnimating)
                {
                    float pulse = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) / 2f;
                    float scaleMod = 1f + pulse * 0.15f;
                    block.GameObject.transform.localScale = block.TargetScale * scaleMod;

                    // Also pulse color slightly
                    Color pulseColor = Color.Lerp(block.BaseColor, Color.white, pulse * 0.3f);
                    block.Renderer.color = pulseColor;
                }
                else if (!block.IsAnimating)
                {
                    block.Renderer.color = block.BaseColor;
                }
            }
        }

        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Manually refresh the display.
        /// </summary>
        public void Refresh()
        {
            _lastIncomingGarbage = -1; // Force rebuild
        }

        /// <summary>
        /// Set the player index this display tracks.
        /// </summary>
        public void SetPlayerIndex(int index)
        {
            playerIndex = index;
            Refresh();
        }

        /// <summary>
        /// Flash all blocks (e.g., when garbage is about to drop).
        /// </summary>
        public void FlashWarning()
        {
            StartCoroutine(FlashCoroutine());
        }

        private System.Collections.IEnumerator FlashCoroutine()
        {
            for (int i = 0; i < 3; i++)
            {
                foreach (var block in _previewBlocks)
                {
                    if (block.Renderer != null)
                        block.Renderer.color = Color.white;
                }
                yield return new WaitForSeconds(0.1f);

                foreach (var block in _previewBlocks)
                {
                    if (block.Renderer != null)
                        block.Renderer.color = block.BaseColor;
                }
                yield return new WaitForSeconds(0.1f);
            }
        }

        #endregion

        #region Helper Class

        private class GarbagePreviewBlock
        {
            public GameObject GameObject;
            public SpriteRenderer Renderer;
            public int Size;
            public int Index;
            public Vector3 TargetScale;
            public Color BaseColor;
            public bool IsAnimating;
            public float AnimationTimer;
        }

        #endregion
    }
}