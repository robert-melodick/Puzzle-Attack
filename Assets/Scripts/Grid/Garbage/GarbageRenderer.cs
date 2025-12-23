using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Handles 9-slice rendering for garbage blocks.
    /// Dynamically assembles child sprites based on garbage dimensions.
    /// Supports center decoration sprite and smooth shrinking animation.
    /// Uses SpriteEffects2D for shader effects.
    /// </summary>
    public class GarbageRenderer : MonoBehaviour
    {
        #region Enums

        private enum SliceIndex
        {
            TopLeft = 0,
            Top = 1,
            TopRight = 2,
            Left = 3,
            Center = 4,
            Right = 5,
            BottomLeft = 6,
            Bottom = 7,
            BottomRight = 8
        }

        #endregion

        #region Inspector Fields

        [Header("9-Slice Sprites")]
        [Tooltip("Sprites in order: TL, T, TR, L, C, R, BL, B, BR")]
        public Sprite[] sliceSprites = new Sprite[9];

        [Header("Center Decoration")]
        [Tooltip("Optional sprite to display in the center of the garbage block")]
        public Sprite centerDecoration;
        public Vector2 decorationOffset = Vector2.zero;
        public bool showDecoration = true;
        [Tooltip("Minimum garbage size to show decoration (width, height)")]
        public Vector2Int minimumSizeForDecoration = new Vector2Int(2, 2);

        [Header("Rendering")]
        public float tileSize = 1f;
        public int sortingOrder = 5;
        public string sortingLayerName = "Default";
        public Material spriteMaterial;

        [Header("Animation")]
        public float shrinkAnimationDuration = 0.2f;

        #endregion

        #region Private Fields

        private readonly List<GameObject> _sliceObjects = new();
        private readonly List<SpriteEffects2D> _sliceEffects = new();
        private SpriteRenderer _decorationRenderer;
        private SpriteEffects2D _decorationEffects;
        private int _currentWidth;
        private int _currentHeight;
        private Transform _sliceContainer;

        // Animation state
        private bool _isAnimatingShrink;
        private float _shrinkTimer;
        private float _shrinkStartY;
        private float _shrinkTargetY;
        private int _targetHeight;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Create container for slice sprites
            var containerObj = new GameObject("SliceContainer");
            containerObj.transform.SetParent(transform);
            containerObj.transform.localPosition = Vector3.zero;
            _sliceContainer = containerObj.transform;
        }

        private void Update()
        {
            if (_isAnimatingShrink)
            {
                _shrinkTimer += Time.deltaTime;
                var progress = Mathf.Clamp01(_shrinkTimer / shrinkAnimationDuration);

                // Ease out
                progress = 1f - (1f - progress) * (1f - progress);

                // Animate the visual offset
                var currentY = Mathf.Lerp(_shrinkStartY, _shrinkTargetY, progress);
                _sliceContainer.localPosition = new Vector3(0, currentY, 0);

                if (progress >= 1f)
                {
                    _isAnimatingShrink = false;
                    _sliceContainer.localPosition = new Vector3(0, _shrinkTargetY, 0);
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Build the visual representation for a garbage block of given dimensions.
        /// </summary>
        public void RebuildVisual(int width, int height)
        {
            ClearVisual();

            _currentWidth = width;
            _currentHeight = height;

            if (width <= 0 || height <= 0) return;
            if (sliceSprites == null || sliceSprites.Length < 9) return;

            // Build the 9-slice grid
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var sliceIndex = GetSliceIndex(x, y, width, height);
                    var sprite = sliceSprites[(int)sliceIndex];

                    if (sprite == null) continue;

                    var sliceObj = new GameObject($"Slice_{x}_{y}");
                    sliceObj.transform.SetParent(_sliceContainer);
                    sliceObj.transform.localPosition = new Vector3(x * tileSize, y * tileSize, 0);

                    var sr = sliceObj.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = sortingOrder;
                    sr.sortingLayerName = sortingLayerName;
                    if (spriteMaterial != null)
                        sr.material = spriteMaterial;

                    // Add SpriteEffects2D for shader control
                    var effects = sliceObj.AddComponent<SpriteEffects2D>();

                    _sliceObjects.Add(sliceObj);
                    _sliceEffects.Add(effects);
                }
            }

            // Add center decoration if applicable
            UpdateDecorationVisibility();

            Debug.Log($"[GarbageRenderer] Built {width}x{height} visual with {_sliceObjects.Count} slices");
        }

        /// <summary>
        /// Animate shrinking from the bottom by one row.
        /// </summary>
        public void ShrinkFromBottom(int newHeight)
        {
            if (newHeight <= 0)
            {
                ClearVisual();
                return;
            }

            // Start shrink animation
            _isAnimatingShrink = true;
            _shrinkTimer = 0f;
            _shrinkStartY = _sliceContainer.localPosition.y;
            _shrinkTargetY = _shrinkStartY + tileSize; // Move up by one tile
            _targetHeight = newHeight;

            // Rebuild visual at end of animation would cause pop
            // Instead, we hide the bottom row immediately and animate the rest up
            HideBottomRow();

            // After animation completes, rebuild cleanly
            StartCoroutine(RebuildAfterShrink(newHeight));
        }

        /// <summary>
        /// Set the flash amount on all slice renderers.
        /// </summary>
        public void SetFlashAmount(float amount)
        {
            foreach (var effects in _sliceEffects)
            {
                if (effects != null)
                {
                    effects.SetFlashAmount(amount);
                }
            }

            if (_decorationEffects != null)
            {
                _decorationEffects.SetFlashAmount(amount);
            }
        }

        /// <summary>
        /// Apply a flash effect to all slices.
        /// </summary>
        public void ApplyFlash(Color color, float duration = 0.1f)
        {
            foreach (var effects in _sliceEffects)
            {
                if (effects != null)
                {
                    effects.ApplyFlash(color, duration);
                }
            }

            if (_decorationEffects != null)
            {
                _decorationEffects.ApplyFlash(color, duration);
            }
        }

        /// <summary>
        /// Set a tint color on all slice renderers.
        /// </summary>
        public void SetTint(Color color, float amount)
        {
            foreach (var effects in _sliceEffects)
            {
                if (effects != null)
                {
                    effects.SetTint(color, amount);
                }
            }

            if (_decorationEffects != null)
            {
                _decorationEffects.SetTint(color, amount);
            }
        }

        /// <summary>
        /// Set tint with specific blend mode.
        /// </summary>
        public void SetTint(Color color, float amount, SpriteEffects2D.TintMode mode)
        {
            foreach (var effects in _sliceEffects)
            {
                if (effects != null)
                {
                    effects.SetTint(color, amount, mode);
                }
            }

            if (_decorationEffects != null)
            {
                _decorationEffects.SetTint(color, amount, mode);
            }
        }

        /// <summary>
        /// Clear tint on all slices.
        /// </summary>
        public void ClearTint()
        {
            foreach (var effects in _sliceEffects)
            {
                if (effects != null)
                {
                    effects.ClearTint();
                }
            }

            if (_decorationEffects != null)
            {
                _decorationEffects.ClearTint();
            }
        }

        /// <summary>
        /// Highlight a specific row (for scan effect during conversion).
        /// </summary>
        public void HighlightRow(int row, Color highlightColor, float intensity)
        {
            var startIndex = row * _currentWidth;
            var endIndex = startIndex + _currentWidth;

            for (var i = 0; i < _sliceEffects.Count; i++)
            {
                var effects = _sliceEffects[i];
                if (effects == null) continue;

                if (i >= startIndex && i < endIndex)
                {
                    effects.SetTint(highlightColor, intensity, SpriteEffects2D.TintMode.Additive);
                }
                else
                {
                    effects.ClearTint();
                }
            }
        }

        /// <summary>
        /// Clear all highlights.
        /// </summary>
        public void ClearHighlight()
        {
            ClearTint();
        }

        /// <summary>
        /// Apply wobble effect to all slices.
        /// </summary>
        public void SetWobble(bool enabled, float amplitude = 0.01f)
        {
            foreach (var effects in _sliceEffects)
            {
                if (effects != null)
                {
                    effects.SetWobble(enabled, amplitude);
                }
            }

            if (_decorationEffects != null)
            {
                _decorationEffects.SetWobble(enabled, amplitude);
            }
        }

        /// <summary>
        /// Apply dissolve effect to all slices.
        /// </summary>
        public void SetDissolve(bool enabled, float amount = 0f, Color edgeColor = default)
        {
            foreach (var effects in _sliceEffects)
            {
                if (effects != null)
                {
                    effects.SetDissolve(enabled, amount, 0.02f, edgeColor);
                }
            }

            if (_decorationEffects != null)
            {
                _decorationEffects.SetDissolve(enabled, amount, 0.02f, edgeColor);
            }
        }

        /// <summary>
        /// Get the current visual dimensions.
        /// </summary>
        public Vector2Int GetCurrentSize() => new Vector2Int(_currentWidth, _currentHeight);

        #endregion

        #region Private Methods

        private SliceIndex GetSliceIndex(int x, int y, int width, int height)
        {
            var isLeft = x == 0;
            var isRight = x == width - 1;
            var isBottom = y == 0;
            var isTop = y == height - 1;

            // Handle 1-wide or 1-tall edge cases
            if (width == 1)
            {
                isLeft = true;
                isRight = true;
            }
            if (height == 1)
            {
                isBottom = true;
                isTop = true;
            }

            // Corners
            if (isBottom && isLeft) return SliceIndex.BottomLeft;
            if (isBottom && isRight) return SliceIndex.BottomRight;
            if (isTop && isLeft) return SliceIndex.TopLeft;
            if (isTop && isRight) return SliceIndex.TopRight;

            // Edges
            if (isBottom) return SliceIndex.Bottom;
            if (isTop) return SliceIndex.Top;
            if (isLeft) return SliceIndex.Left;
            if (isRight) return SliceIndex.Right;

            // Center
            return SliceIndex.Center;
        }

        private void ClearVisual()
        {
            foreach (var obj in _sliceObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }
            _sliceObjects.Clear();
            _sliceEffects.Clear();

            if (_decorationRenderer != null)
            {
                Destroy(_decorationRenderer.gameObject);
                _decorationRenderer = null;
                _decorationEffects = null;
            }

            _currentWidth = 0;
            _currentHeight = 0;
        }

        private void HideBottomRow()
        {
            // Hide the bottom row of slices
            for (var i = 0; i < _currentWidth && i < _sliceObjects.Count; i++)
            {
                if (_sliceObjects[i] != null)
                {
                    var sr = _sliceObjects[i].GetComponent<SpriteRenderer>();
                    if (sr != null)
                        sr.enabled = false;
                }
            }
        }

        private System.Collections.IEnumerator RebuildAfterShrink(int newHeight)
        {
            yield return new WaitForSeconds(shrinkAnimationDuration);

            // Reset container position
            _sliceContainer.localPosition = Vector3.zero;

            // Rebuild at new size
            RebuildVisual(_currentWidth, newHeight);
        }

        private void UpdateDecorationVisibility()
        {
            // Remove existing decoration
            if (_decorationRenderer != null)
            {
                Destroy(_decorationRenderer.gameObject);
                _decorationRenderer = null;
                _decorationEffects = null;
            }

            // Check if we should show decoration
            if (!showDecoration || centerDecoration == null) return;
            if (_currentWidth < minimumSizeForDecoration.x || _currentHeight < minimumSizeForDecoration.y) return;

            // Create decoration sprite
            var decoObj = new GameObject("CenterDecoration");
            decoObj.transform.SetParent(_sliceContainer);

            // Position in center of garbage block
            var centerX = (_currentWidth - 1) * tileSize * 0.5f;
            var centerY = (_currentHeight - 1) * tileSize * 0.5f;
            decoObj.transform.localPosition = new Vector3(
                centerX + decorationOffset.x,
                centerY + decorationOffset.y,
                -0.01f // Slightly in front
            );

            _decorationRenderer = decoObj.AddComponent<SpriteRenderer>();
            _decorationRenderer.sprite = centerDecoration;
            _decorationRenderer.sortingOrder = sortingOrder + 1;
            _decorationRenderer.sortingLayerName = sortingLayerName;
            if (spriteMaterial != null)
                _decorationRenderer.material = spriteMaterial;

            // Add effects to decoration
            _decorationEffects = decoObj.AddComponent<SpriteEffects2D>();
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [ContextMenu("Preview 3x2 Garbage")]
        private void PreviewGarbage()
        {
            RebuildVisual(3, 2);
        }

        [ContextMenu("Preview 6x3 Garbage")]
        private void PreviewLargeGarbage()
        {
            RebuildVisual(6, 3);
        }
#endif

        #endregion
    }
}