using UnityEngine;
using TMPro;
using System.Collections;

namespace PuzzleAttack.UI
{
    /// <summary>
    /// A floating popup label that displays combo or chain counts.
    /// Spawns at match location, animates upward, then fades out.
    /// </summary>
    public class ComboChainPopup : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Text")]
        [SerializeField] private TextMeshPro worldText; // For world space
        [SerializeField] private TextMeshProUGUI uiText; // For screen space (optional)

        [Header("Animation")]
        [SerializeField] private float floatSpeed = 2f;
        [SerializeField] private float floatDistance = 1.5f;
        [SerializeField] private float lifetime = 1.2f;
        [SerializeField] private float fadeStartTime = 0.6f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float maxScale = 1.2f;
        [SerializeField] private float scaleAnimDuration = 0.2f;

        [Header("Colors")]
        [SerializeField] private Color comboColor = new Color(1f, 0.9f, 0.2f); // Yellow
        [SerializeField] private Color chainColor = new Color(0.2f, 0.8f, 1f); // Cyan
        [SerializeField] private Color highComboColor = new Color(1f, 0.5f, 0f); // Orange (5+)
        [SerializeField] private Color highChainColor = new Color(1f, 0.2f, 0.8f); // Magenta (3+)

        [Header("Thresholds")]
        [SerializeField] private int highComboThreshold = 5;
        [SerializeField] private int highChainThreshold = 3;

        #endregion

        #region Private Fields

        private Vector3 _startPosition;
        private float _timer;
        private Color _baseColor;
        private bool _isInitialized;

        #endregion

        #region Public API

        /// <summary>
        /// Initialize and show a combo popup.
        /// </summary>
        public void ShowCombo(int comboCount, Vector3 worldPosition)
        {
            SetText($"Combo x{comboCount}");
            _baseColor = comboCount >= highComboThreshold ? highComboColor : comboColor;
            Initialize(worldPosition);
        }

        /// <summary>
        /// Initialize and show a chain popup.
        /// </summary>
        public void ShowChain(int chainCount, Vector3 worldPosition)
        {
            SetText($"Chain x{chainCount}");
            _baseColor = chainCount >= highChainThreshold ? highChainColor : chainColor;
            Initialize(worldPosition);
        }

        /// <summary>
        /// Initialize and show a custom popup.
        /// </summary>
        public void ShowCustom(string text, Color color, Vector3 worldPosition)
        {
            SetText(text);
            _baseColor = color;
            Initialize(worldPosition);
        }

        #endregion

        #region Private Methods

        private void Initialize(Vector3 worldPosition)
        {
            _startPosition = worldPosition;
            transform.position = worldPosition;
            _timer = 0f;
            _isInitialized = true;

            // Set initial color
            SetColor(_baseColor);

            // Start scale animation
            StartCoroutine(ScaleAnimation());
        }

        private void SetText(string text)
        {
            if (worldText != null)
            {
                worldText.text = text;
            }
            if (uiText != null)
            {
                uiText.text = text;
            }
        }

        private void SetColor(Color color)
        {
            if (worldText != null)
            {
                worldText.color = color;
            }
            if (uiText != null)
            {
                uiText.color = color;
            }
        }

        private void SetAlpha(float alpha)
        {
            Color color = _baseColor;
            color.a = alpha;
            SetColor(color);
        }

        private void SetScale(float scale)
        {
            transform.localScale = Vector3.one * scale;
        }

        private IEnumerator ScaleAnimation()
        {
            float elapsed = 0f;

            // Pop in
            while (elapsed < scaleAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / scaleAnimDuration;
                float scale = scaleCurve.Evaluate(t) * maxScale;
                
                // Overshoot then settle
                if (t < 0.5f)
                {
                    scale = Mathf.Lerp(0f, maxScale, t * 2f);
                }
                else
                {
                    scale = Mathf.Lerp(maxScale, 1f, (t - 0.5f) * 2f);
                }

                SetScale(scale);
                yield return null;
            }

            SetScale(1f);
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (!_isInitialized) return;

            _timer += Time.deltaTime;

            // Float upward
            float floatProgress = _timer / lifetime;
            Vector3 newPosition = _startPosition + Vector3.up * (floatDistance * floatProgress);
            transform.position = newPosition;

            // Fade out
            if (_timer >= fadeStartTime)
            {
                float fadeProgress = (_timer - fadeStartTime) / (lifetime - fadeStartTime);
                float alpha = 1f - fadeProgress;
                SetAlpha(alpha);
            }

            // Destroy when lifetime is over
            if (_timer >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        #endregion
    }
}