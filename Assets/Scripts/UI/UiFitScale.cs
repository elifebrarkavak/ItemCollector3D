using UnityEngine;

namespace ItemCollector3D.UI
{
    /// <summary>
    /// Sits on a container (the screen's panel, which fills the safe-area root) and scales a block inside it DOWN (never up) so the block's design size
    /// always fits: with a big camera cut-out or gesture bar the block shrinks a little instead of running off screen. On a normal phone the container is
    /// large enough and the scale is exactly 1, so touch targets keep their authored size. The block hangs from the top centre of the container, so
    /// shrinking never moves it towards the title. Reacts to the container's size changing at once (no frame of lag).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiFitScale : MonoBehaviour
    {
        [Tooltip("The block that is scaled to fit this container.")]
        [SerializeField] private RectTransform target;
        [Tooltip("The size the block was designed for (reference units, at scale 1).")]
        [SerializeField] private Vector2 designSize = new Vector2(1700f, 780f);
        [Tooltip("Distance of the block's top edge from the top of the container: that part of the container height is not available to it.")]
        [SerializeField] private float topInset = 160f;
        [SerializeField] private float bottomMargin = 20f;
        [SerializeField] private float sideMargin = 24f;
        [SerializeField, Range(0.3f, 1f)] private float minScale = 0.5f;
        [Tooltip("When the container is taller than the block needs (4:3), move the block down by half of the spare height so it does not hug the top (stage 18B).")]
        [SerializeField] private bool centreVertically;
        [SerializeField] private float centreMaxShift = 260f;

        private RectTransform container;
        private Vector2 lastSize = new Vector2(-1f, -1f);
        private float scale = 1f;
        private float baseY;
        private bool baseCaptured;

        /// <summary>1 when the block fits at its authored size, less when the safe area is tighter.</summary>
        public float CurrentScale => scale;

        private void OnEnable()
        {
            lastSize = new Vector2(-1f, -1f);
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
            {
                Apply();
            }
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (target == null)
            {
                return;
            }

            if (container == null)
            {
                container = (RectTransform)transform;
            }

            Vector2 size = container.rect.size;
            if (size == lastSize || size.x <= 1f || size.y <= 1f)
            {
                return;
            }

            lastSize = size;
            float availableWidth = size.x - 2f * sideMargin;
            float availableHeight = size.y - topInset - bottomMargin;
            scale = Mathf.Clamp(Mathf.Min(availableWidth / designSize.x, availableHeight / designSize.y), minScale, 1f);
            target.localScale = new Vector3(scale, scale, 1f);

            if (centreVertically)
            {
                if (!baseCaptured)
                {
                    baseCaptured = true;
                    baseY = target.anchoredPosition.y;
                }

                float spare = Mathf.Max(0f, availableHeight - designSize.y * scale);
                target.anchoredPosition = new Vector2(target.anchoredPosition.x, baseY - Mathf.Min(spare * 0.5f, centreMaxShift));
            }
        }
    }
}
