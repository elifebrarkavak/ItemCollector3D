using TMPro;
using UnityEngine;

namespace ItemCollector3D.Menus
{
    /// <summary>
    /// A short pop-in / hold / fade banner (title + subtitle). Uses UNSCALED time so it also finishes while the game is paused,
    /// and never blocks touches (no raycast, no interaction).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NotificationBanner : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private CanvasGroup group;
        [SerializeField, Min(0.05f)] private float popDuration = 0.25f;
        [SerializeField, Min(0.1f)] private float holdDuration = 1.3f;
        [SerializeField, Min(0.05f)] private float fadeDuration = 0.4f;

        private float elapsed;

        public bool IsShowing { get; private set; }
        public int ShowCount { get; private set; }
        public string CurrentTitle => titleText != null ? titleText.text : string.Empty;
        public string CurrentSubtitle => subtitleText != null ? subtitleText.text : string.Empty;
        public float TotalDuration => popDuration + holdDuration + fadeDuration;

        private void Awake()
        {
            if (titleText != null)
            {
                titleText.raycastTarget = false;
            }

            if (subtitleText != null)
            {
                subtitleText.raycastTarget = false;
            }

            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        public void Show(string title, string subtitle, Color color)
        {
            if (titleText != null)
            {
                titleText.text = title;
                titleText.color = color;
            }

            if (subtitleText != null)
            {
                subtitleText.text = subtitle ?? string.Empty;
            }

            elapsed = 0f;
            IsShowing = true;
            ShowCount++;
            Apply();
        }

        private void Update()
        {
            if (!IsShowing)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= TotalDuration)
            {
                IsShowing = false;
                if (group != null)
                {
                    group.alpha = 0f;
                }

                transform.localScale = Vector3.one;
                return;
            }

            Apply();
        }

        private void Apply()
        {
            float scale = 1f;
            float alpha = 1f;
            if (elapsed < popDuration)
            {
                float t = elapsed / popDuration;
                scale = Mathf.Lerp(0.6f, 1.08f, EaseOut(t));
                alpha = Mathf.Clamp01(t * 2f);
            }
            else if (elapsed < popDuration + 0.12f)
            {
                scale = Mathf.Lerp(1.08f, 1f, (elapsed - popDuration) / 0.12f); // settle
            }
            else if (elapsed > popDuration + holdDuration)
            {
                alpha = 1f - (elapsed - popDuration - holdDuration) / fadeDuration;
            }

            transform.localScale = Vector3.one * scale;
            if (group != null)
            {
                group.alpha = Mathf.Clamp01(alpha);
            }
        }

        private static float EaseOut(float x)
        {
            return 1f - (1f - x) * (1f - x);
        }
    }
}
