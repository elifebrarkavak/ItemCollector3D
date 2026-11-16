using TMPro;
using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Short-lived floating reward text ("RARE! +50 Gold") on the existing HUD canvas.
    /// Pop-in, rise and fade. Colour, size and duration are driven by the feedback profile intensity;
    /// the min/max ranges below are the only tuning values. It never blocks UI input (non-raycast).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingRewardText : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private CanvasGroup group;

        [Header("Animation (ranges scaled by profile effect intensity 0..1)")]
        [SerializeField, Min(0.05f)] private float popDuration = 0.18f;
        [SerializeField] private float riseDistance = 140f;
        [SerializeField, Min(0.3f)] private float minDuration = 0.9f;
        [SerializeField, Min(0.3f)] private float maxDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float minScale = 0.9f;
        [SerializeField, Min(0.1f)] private float maxScale = 1.4f;
        [SerializeField, Range(0.05f, 0.9f)] private float fadeFraction = 0.3f;

        private RectTransform rect;
        private Vector2 startPosition;
        private float elapsed;
        private float duration;
        private float baseScale = 1f;

        public bool IsVisible { get; private set; }
        public string CurrentMessage => text != null ? text.text : string.Empty;
        public Color CurrentColor => text != null ? text.color : Color.white;
        public float CurrentDuration => duration;
        public float CurrentScale => baseScale;
        public int ShowCount { get; private set; }

        private void Awake()
        {
            rect = transform as RectTransform;
            if (rect != null)
            {
                startPosition = rect.anchoredPosition;
            }

            if (text != null)
            {
                text.raycastTarget = false;
            }

            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        public float DurationFor(float intensity)
        {
            return Mathf.Lerp(minDuration, maxDuration, Mathf.Clamp01(intensity));
        }

        public float ScaleFor(float intensity)
        {
            return Mathf.Lerp(minScale, maxScale, Mathf.Clamp01(intensity));
        }

        public void Show(string message, Color color, float intensity)
        {
            if (text == null)
            {
                return;
            }

            text.text = message;
            text.color = color;
            duration = DurationFor(intensity);
            baseScale = ScaleFor(intensity);
            elapsed = 0f;
            IsVisible = true;
            ShowCount++;
            Apply();
        }

        private void Update()
        {
            if (!IsVisible)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                IsVisible = false;
                if (group != null)
                {
                    group.alpha = 0f;
                }

                return;
            }

            Apply();
        }

        private void Apply()
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float pop = EaseOutBack(Mathf.Clamp01(elapsed / popDuration));
            transform.localScale = Vector3.one * (baseScale * pop);

            if (rect != null)
            {
                rect.anchoredPosition = startPosition + Vector2.up * (riseDistance * EaseOutQuad(t));
            }

            if (group != null)
            {
                float fadeStart = 1f - fadeFraction;
                group.alpha = t < fadeStart ? 1f : 1f - (t - fadeStart) / fadeFraction;
            }
        }

        private static float EaseOutQuad(float x)
        {
            return 1f - (1f - x) * (1f - x);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = x - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
