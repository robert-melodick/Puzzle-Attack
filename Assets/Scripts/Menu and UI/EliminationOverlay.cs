using UnityEngine;
using TMPro;

namespace PuzzleAttack
{
    /// <summary>
    /// Component for the elimination overlay displayed on grids that have been eliminated.
    /// Shows "RETIRED" text with a darkened background.
    /// </summary>
    public class EliminationOverlay : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI eliminationText;
        [SerializeField] private SpriteRenderer backgroundOverlay;
        [SerializeField] private UnityEngine.UI.Image backgroundImage; // Alternative for UI-based overlay

        [Header("Settings")]
        [SerializeField] private string defaultText = "RETIRED";
        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color textColor = Color.white;

        [Header("Animation")]
        [SerializeField] private bool animateEntry = true;
        [SerializeField] private float fadeInDuration = 0.5f;

        private void Start()
        {
            // Set default text
            if (eliminationText != null)
            {
                if (string.IsNullOrEmpty(eliminationText.text))
                {
                    eliminationText.text = defaultText;
                }
                eliminationText.color = textColor;
            }

            // Set overlay color
            if (backgroundOverlay != null)
            {
                backgroundOverlay.color = overlayColor;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = overlayColor;
            }

            // Animate entry
            if (animateEntry)
            {
                StartCoroutine(FadeIn());
            }
        }

        private System.Collections.IEnumerator FadeIn()
        {
            float elapsed = 0f;

            // Get starting colors with alpha = 0
            Color startOverlayColor = overlayColor;
            startOverlayColor.a = 0f;

            Color startTextColor = textColor;
            startTextColor.a = 0f;

            // Set initial state
            if (backgroundOverlay != null)
                backgroundOverlay.color = startOverlayColor;
            if (backgroundImage != null)
                backgroundImage.color = startOverlayColor;
            if (eliminationText != null)
                eliminationText.color = startTextColor;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime; // Use unscaled time in case game is paused
                float t = elapsed / fadeInDuration;
                t = Mathf.SmoothStep(0f, 1f, t); // Smooth easing

                // Fade overlay
                Color currentOverlayColor = Color.Lerp(startOverlayColor, overlayColor, t);
                if (backgroundOverlay != null)
                    backgroundOverlay.color = currentOverlayColor;
                if (backgroundImage != null)
                    backgroundImage.color = currentOverlayColor;

                // Fade text
                if (eliminationText != null)
                {
                    Color currentTextColor = Color.Lerp(startTextColor, textColor, t);
                    eliminationText.color = currentTextColor;
                }

                yield return null;
            }

            // Ensure final state
            if (backgroundOverlay != null)
                backgroundOverlay.color = overlayColor;
            if (backgroundImage != null)
                backgroundImage.color = overlayColor;
            if (eliminationText != null)
                eliminationText.color = textColor;
        }

        /// <summary>
        /// Set the elimination text.
        /// </summary>
        public void SetText(string text)
        {
            if (eliminationText != null)
            {
                eliminationText.text = text;
            }
        }

        /// <summary>
        /// Set the overlay color.
        /// </summary>
        public void SetOverlayColor(Color color)
        {
            overlayColor = color;

            if (backgroundOverlay != null)
                backgroundOverlay.color = color;
            if (backgroundImage != null)
                backgroundImage.color = color;
        }

        /// <summary>
        /// Set the text color.
        /// </summary>
        public void SetTextColor(Color color)
        {
            textColor = color;

            if (eliminationText != null)
                eliminationText.color = color;
        }
    }
}