using System.Collections.Generic;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Handles tile-based rendering for garbage blocks (Panel de Pon style).
    /// Each cell gets the appropriate sprite based on its edge position.
    /// Supports face overlay sprite that scales/positions based on block size.
    /// </summary>
    public class GarbageRenderer : MonoBehaviour
    {
        #region Enums

        public enum FaceState
        {
            Normal,     // Default idle face
            Falling,    // Surprised/worried while falling
            Triggered,  // Adjacent match detected, about to convert
            Converting, // Actively being converted (X_X eyes)
            Destroyed   // Final frame before removal (optional)
        }

        #endregion

        #region Sprite Configuration

        [System.Serializable]
        public class GarbageSprites
        {
            [Header("Corner Sprites")]
            public Sprite topLeft;
            public Sprite topRight;
            public Sprite bottomLeft;
            public Sprite bottomRight;

            [Header("Edge Sprites (for blocks with interior)")]
            public Sprite top;
            public Sprite bottom;
            public Sprite left;
            public Sprite right;

            [Header("Center Fill")]
            public Sprite center;

            [Header("Single Row/Column Edge Cases")]
            [Tooltip("Used for 1-tall blocks: left cap")]
            public Sprite horizontalLeft;
            [Tooltip("Used for 1-tall blocks: middle sections")]
            public Sprite horizontalMiddle;
            [Tooltip("Used for 1-tall blocks: right cap")]
            public Sprite horizontalRight;
            [Tooltip("Used for 1-wide blocks: bottom cap")]
            public Sprite verticalBottom;
            [Tooltip("Used for 1-wide blocks: middle sections")]
            public Sprite verticalMiddle;
            [Tooltip("Used for 1-wide blocks: top cap")]
            public Sprite verticalTop;

            [Header("1x1 Block")]
            public Sprite single;
        }

        [System.Serializable]
        public class FaceSprites
        {
            [Tooltip("Default idle expression")]
            public Sprite normal;
            [Tooltip("Surprised/worried while falling")]
            public Sprite falling;
            [Tooltip("Nervous - adjacent match detected")]
            public Sprite triggered;
            [Tooltip("X_X eyes during conversion")]
            public Sprite converting;
            [Tooltip("Optional final frame before destruction")]
            public Sprite destroyed;
        }

        #endregion

        #region Inspector Fields

        [Header("Sprite Set")]
        public GarbageSprites sprites;

        [Header("Face Sprites")]
        public FaceSprites faceSprites;

        [Header("Face Overlay")]
        [Tooltip("Legacy: single face sprite (use FaceSprites instead)")]
        public Sprite faceSprite;
        [Tooltip("Offset from center of block")]
        public Vector2 faceOffset = Vector2.zero;
        [Tooltip("Minimum block size to show face (width, height)")]
        public Vector2Int minimumSizeForFace = new Vector2Int(1, 1);
        [Tooltip("Face sorting order relative to body")]
        public int faceSortingOrderOffset = 1;

        [Header("Rendering Settings")]
        public float tileSize = 1f;
        public int sortingOrder = 5;
        public string sortingLayerName = "Default";
        public Material spriteMaterial;

        [Header("Animation")]
        public float shrinkAnimationDuration = 0.2f;

        #endregion

        #region Private Fields

        private readonly List<GameObject> _tileObjects = new();
        private readonly List<SpriteRenderer> _tileRenderers = new();
        private readonly List<SpriteEffects2D> _tileEffects = new();
        
        private GameObject _faceObject;
        private SpriteRenderer _faceRenderer;
        private SpriteEffects2D _faceEffects;
        private FaceState _currentFaceState = FaceState.Normal;
        
        private Transform _tileContainer;
        private int _currentWidth;
        private int _currentHeight;

        // Animation state
        private bool _isAnimatingShrink;
        private float _shrinkTimer;
        private float _shrinkStartY;
        private float _shrinkTargetY;
        private int _targetHeight;
        private GarbageBlock _garbageBlock;
        private GridManager _gridManager;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            var containerObj = new GameObject("TileContainer");
            containerObj.transform.SetParent(transform);
            containerObj.transform.localPosition = Vector3.zero;
            _tileContainer = containerObj.transform;
        }

        private void Update()
        {
            if (_isAnimatingShrink && _garbageBlock != null)
            {
                _shrinkTimer += Time.deltaTime;
                var progress = Mathf.Clamp01(_shrinkTimer / shrinkAnimationDuration);

                // Ease out curve
                progress = 1f - (1f - progress) * (1f - progress);

                var currentY = Mathf.Lerp(_shrinkStartY, _shrinkTargetY, progress);
                _tileContainer.localPosition = new Vector3(0, currentY, 0);

                if (progress >= 1f)
                {
                    _isAnimatingShrink = false;
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

            // Build each cell
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var sprite = GetSpriteForCell(x, y, width, height);
                    if (sprite == null) continue;

                    var tileObj = new GameObject($"Tile_{x}_{y}");
                    tileObj.transform.SetParent(_tileContainer);
                    tileObj.transform.localPosition = new Vector3(x * tileSize, y * tileSize, 0);

                    var sr = tileObj.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = sortingOrder;
                    sr.sortingLayerName = sortingLayerName;
                    if (spriteMaterial != null)
                        sr.material = spriteMaterial;

                    _tileObjects.Add(tileObj);
                    _tileRenderers.Add(sr);

                    // Add effects component if available
                    var effects = tileObj.AddComponent<SpriteEffects2D>();
                    _tileEffects.Add(effects);
                }
            }

            // Add face overlay
            CreateFaceOverlay();

            Debug.Log($"[GarbageRenderer] Built {width}x{height} garbage with {_tileObjects.Count} tiles");
        }

        /// <summary>
        /// Animate shrinking from the bottom by removing rows.
        /// </summary>
        public void ShrinkFromBottom(int newHeight, int rowsShrunk, GarbageBlock garbageBlock, GridManager gridManager)
        {
            if (newHeight <= 0)
            {
                ClearVisual();
                return;
            }

            _garbageBlock = garbageBlock;
            _gridManager = gridManager;

            _isAnimatingShrink = true;
            _shrinkTimer = 0f;
            _shrinkStartY = 0f;
            _shrinkTargetY = tileSize * rowsShrunk;
            _targetHeight = newHeight;

            // Hide bottom rows
            HideBottomRows(rowsShrunk);

            StartCoroutine(RebuildAfterShrink(newHeight));
        }

        /// <summary>
        /// Get the current visual dimensions.
        /// </summary>
        public Vector2Int GetCurrentSize() => new Vector2Int(_currentWidth, _currentHeight);

        #endregion

        #region Sprite Selection

        /// <summary>
        /// Determines which sprite to use for a cell based on its position within the block.
        /// </summary>
        private Sprite GetSpriteForCell(int x, int y, int width, int height)
        {
            // Special case: 1x1 block
            if (width == 1 && height == 1)
            {
                return sprites.single ?? sprites.center;
            }

            // Special case: 1-tall horizontal strip
            if (height == 1)
            {
                if (x == 0)
                    return sprites.horizontalLeft ?? sprites.bottomLeft;
                if (x == width - 1)
                    return sprites.horizontalRight ?? sprites.bottomRight;
                return sprites.horizontalMiddle ?? sprites.bottom;
            }

            // Special case: 1-wide vertical strip
            if (width == 1)
            {
                if (y == 0)
                    return sprites.verticalBottom ?? sprites.bottomLeft;
                if (y == height - 1)
                    return sprites.verticalTop ?? sprites.topLeft;
                return sprites.verticalMiddle ?? sprites.left;
            }

            // Standard 2D block (2x2 or larger)
            var isLeft = x == 0;
            var isRight = x == width - 1;
            var isBottom = y == 0;
            var isTop = y == height - 1;

            // Corners
            if (isBottom && isLeft) return sprites.bottomLeft;
            if (isBottom && isRight) return sprites.bottomRight;
            if (isTop && isLeft) return sprites.topLeft;
            if (isTop && isRight) return sprites.topRight;

            // Edges
            if (isBottom) return sprites.bottom;
            if (isTop) return sprites.top;
            if (isLeft) return sprites.left;
            if (isRight) return sprites.right;

            // Center
            return sprites.center;
        }

        #endregion

        #region Face Overlay

        private void CreateFaceOverlay()
        {
            // Get the appropriate starting sprite
            var startingFace = GetFaceSpriteForState(FaceState.Normal);
            if (startingFace == null) return;
            if (_currentWidth < minimumSizeForFace.x || _currentHeight < minimumSizeForFace.y) return;

            _faceObject = new GameObject("Face");
            _faceObject.transform.SetParent(_tileContainer);

            // Position face at center of block
            var centerX = (_currentWidth - 1) * tileSize * 0.5f;
            var centerY = (_currentHeight - 1) * tileSize * 0.5f;
            _faceObject.transform.localPosition = new Vector3(
                centerX + faceOffset.x,
                centerY + faceOffset.y,
                -0.01f
            );

            _faceRenderer = _faceObject.AddComponent<SpriteRenderer>();
            _faceRenderer.sprite = startingFace;
            _faceRenderer.sortingOrder = sortingOrder + faceSortingOrderOffset;
            _faceRenderer.sortingLayerName = sortingLayerName;
            if (spriteMaterial != null)
                _faceRenderer.material = spriteMaterial;

            _faceEffects = _faceObject.AddComponent<SpriteEffects2D>();
            _currentFaceState = FaceState.Normal;
        }

        /// <summary>
        /// Get the sprite for a given face state, with fallbacks.
        /// </summary>
        private Sprite GetFaceSpriteForState(FaceState state)
        {
            if (faceSprites == null)
                return faceSprite; // Legacy fallback

            return state switch
            {
                FaceState.Normal => faceSprites.normal ?? faceSprite,
                FaceState.Falling => faceSprites.falling ?? faceSprites.normal ?? faceSprite,
                FaceState.Triggered => faceSprites.triggered ?? faceSprites.normal ?? faceSprite,
                FaceState.Converting => faceSprites.converting ?? faceSprites.triggered ?? faceSprite,
                FaceState.Destroyed => faceSprites.destroyed ?? faceSprites.converting ?? faceSprite,
                _ => faceSprites.normal ?? faceSprite
            };
        }

        /// <summary>
        /// Change the face to a specific state.
        /// </summary>
        public void SetFaceState(FaceState state)
        {
            if (_currentFaceState == state) return;
            
            _currentFaceState = state;
            var sprite = GetFaceSpriteForState(state);
            
            if (_faceRenderer != null && sprite != null)
            {
                _faceRenderer.sprite = sprite;
            }
        }

        /// <summary>
        /// Get the current face state.
        /// </summary>
        public FaceState GetFaceState() => _currentFaceState;

        /// <summary>
        /// Change the face sprite directly (legacy support).
        /// </summary>
        public void SetFaceSprite(Sprite newFace)
        {
            if (_faceRenderer != null)
            {
                _faceRenderer.sprite = newFace;
            }
        }

        /// <summary>
        /// Show or hide the face.
        /// </summary>
        public void SetFaceVisible(bool visible)
        {
            if (_faceObject != null)
            {
                _faceObject.SetActive(visible);
            }
        }

        #endregion

        #region Visual Effects

        public void SetFlashAmount(float amount)
        {
            foreach (var effects in _tileEffects)
            {
                if (effects != null)
                    effects.SetFlashAmount(amount);
            }
            if (_faceEffects != null)
                _faceEffects.SetFlashAmount(amount);
        }

        public void ApplyFlash(Color color, float duration = 0.1f)
        {
            foreach (var effects in _tileEffects)
            {
                if (effects != null)
                    effects.ApplyFlash(color, duration);
            }
            if (_faceEffects != null)
                _faceEffects.ApplyFlash(color, duration);
        }

        public void SetTint(Color color, float amount)
        {
            foreach (var effects in _tileEffects)
            {
                if (effects != null)
                    effects.SetTint(color, amount);
            }
            if (_faceEffects != null)
                _faceEffects.SetTint(color, amount);
        }

        public void SetTint(Color color, float amount, SpriteEffects2D.TintMode mode)
        {
            foreach (var effects in _tileEffects)
            {
                if (effects != null)
                    effects.SetTint(color, amount, mode);
            }
            if (_faceEffects != null)
                _faceEffects.SetTint(color, amount, mode);
        }

        public void ClearTint()
        {
            foreach (var effects in _tileEffects)
            {
                if (effects != null)
                    effects.ClearTint();
            }
            if (_faceEffects != null)
                _faceEffects.ClearTint();
        }

        public void HighlightRow(int row, Color highlightColor, float intensity)
        {
            var startIndex = row * _currentWidth;
            var endIndex = startIndex + _currentWidth;

            for (var i = 0; i < _tileEffects.Count; i++)
            {
                var effects = _tileEffects[i];
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

        public void ClearHighlight() => ClearTint();

        public void SetWobble(bool enabled, float amplitude = 0.01f)
        {
            foreach (var effects in _tileEffects)
            {
                if (effects != null)
                    effects.SetWobble(enabled, amplitude);
            }
            if (_faceEffects != null)
                _faceEffects.SetWobble(enabled, amplitude);
        }

        public void SetDissolve(bool enabled, float amount = 0f, Color edgeColor = default)
        {
            foreach (var effects in _tileEffects)
            {
                if (effects != null)
                    effects.SetDissolve(enabled, amount, 0.02f, edgeColor);
            }
            if (_faceEffects != null)
                _faceEffects.SetDissolve(enabled, amount, 0.02f, edgeColor);
        }

        #endregion

        #region Private Helpers

        private void ClearVisual()
        {
            foreach (var obj in _tileObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }
            _tileObjects.Clear();
            _tileRenderers.Clear();
            _tileEffects.Clear();

            if (_faceObject != null)
            {
                Destroy(_faceObject);
                _faceObject = null;
                _faceRenderer = null;
                _faceEffects = null;
            }

            _currentWidth = 0;
            _currentHeight = 0;
        }

        private void HideBottomRows(int rowCount)
        {
            var tilesToHide = _currentWidth * rowCount;
            for (var i = 0; i < tilesToHide && i < _tileRenderers.Count; i++)
            {
                if (_tileRenderers[i] != null)
                    _tileRenderers[i].enabled = false;
            }
        }

        private System.Collections.IEnumerator RebuildAfterShrink(int newHeight)
        {
            yield return new WaitForSeconds(shrinkAnimationDuration);

            if (_garbageBlock != null && _gridManager != null)
            {
                var gridOffset = _gridManager.gridRiser?.CurrentGridOffset ?? 0f;
                _garbageBlock.OnShrinkComplete(gridOffset);
            }

            _tileContainer.localPosition = Vector3.zero;
            RebuildVisual(_currentWidth, newHeight);

            _garbageBlock = null;
            _gridManager = null;
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [ContextMenu("Preview 1x1")]
        private void Preview1x1() => RebuildVisual(1, 1);

        [ContextMenu("Preview 3x1 Horizontal")]
        private void Preview3x1() => RebuildVisual(3, 1);

        [ContextMenu("Preview 1x3 Vertical")]
        private void Preview1x3() => RebuildVisual(1, 3);

        [ContextMenu("Preview 3x2")]
        private void Preview3x2() => RebuildVisual(3, 2);

        [ContextMenu("Preview 6x3")]
        private void Preview6x3() => RebuildVisual(6, 3);

        [ContextMenu("Preview 6x1")]
        private void Preview6x1() => RebuildVisual(6, 1);
#endif

        #endregion
    }
}