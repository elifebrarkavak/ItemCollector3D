using UnityEngine;

namespace ItemCollector3D.UI
{
    /// <summary>
    /// Short, smooth entrance for a UI element each time it is enabled (menu pieces sliding in, panels popping up). Fades with a
    /// CanvasGroup and moves / scales its RectTransform, then rests exactly on the authored pose. Unscaled time: it also plays while
    /// the game is paused. Never affects raycasts or layout.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiEntrance : MonoBehaviour
    {
        public enum Style { Pop, SlideFromLeft, SlideFromRight, SlideFromTop, SlideFromBottom, Fade }

        [SerializeField] private Style style = Style.Pop;
        [SerializeField, Min(0f)] private float delay;
        [SerializeField, Min(0.05f)] private float duration = 0.32f;
        [SerializeField, Min(0f)] private float distance = 160f;

        private RectTransform rect;
        private CanvasGroup group;
        private Vector2 restPosition;
        private Vector3 restScale;
        private bool captured;
        private float time;
        private bool playing;

        public bool IsPlaying => playing;
        public Style EntranceStyle { get => style; set => style = value; }

        private void Capture()
        {
            if (captured)
            {
                return;
            }

            rect = (RectTransform)transform;
            restPosition = rect.anchoredPosition;
            restScale = rect.localScale;
            group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }

            captured = true;
        }

        private void OnEnable()
        {
            Capture();
            time = -delay;
            playing = true;
            Evaluate(0f);
        }

        private void OnDisable()
        {
            if (captured)
            {
                Evaluate(1f);
            }

            playing = false;
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }

            time += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float p = Mathf.Clamp01(time / duration);
            Evaluate(p);
            if (p >= 1f)
            {
                playing = false;
            }
        }

        private void Evaluate(float p)
        {
            float alpha = Mathf.Clamp01(p * 2.2f);
            Vector2 position = restPosition;
            Vector3 scale = restScale;
            switch (style)
            {
                case Style.Pop:
                {
                    float s = Mathf.LerpUnclamped(0.82f, 1f, EaseOutBack(p));
                    scale = restScale * s;
                    break;
                }
                case Style.SlideFromLeft:
                    position += new Vector2(-distance * (1f - EaseOutCubic(p)), 0f);
                    break;
                case Style.SlideFromRight:
                    position += new Vector2(distance * (1f - EaseOutCubic(p)), 0f);
                    break;
                case Style.SlideFromTop:
                    position += new Vector2(0f, distance * (1f - EaseOutBack(p, 1.2f)));
                    break;
                case Style.SlideFromBottom:
                    position += new Vector2(0f, -distance * (1f - EaseOutCubic(p)));
                    break;
            }

            rect.anchoredPosition = position;
            rect.localScale = scale;
            group.alpha = alpha;
        }

        private static float EaseOutCubic(float x)
        {
            float f = 1f - x;
            return 1f - f * f * f;
        }

        private static float EaseOutBack(float x, float overshoot = 1.70158f)
        {
            float c3 = overshoot + 1f;
            float f = x - 1f;
            return 1f + c3 * f * f * f + overshoot * f * f;
        }
    }
}
