using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ItemCollector3D.UI
{
    /// <summary>
    /// Press feedback for the stylized buttons: a springy squash on touch down, a small bounce back on release, the label sinking
    /// into the button, and (optional) a calm idle pulse for the main call to action. Uses unscaled time so it also works while the
    /// game is paused. It only moves this button's own RectTransform scale / label, never layout or input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        [Tooltip("Scaled element. Defaults to this RectTransform.")]
        [SerializeField] private RectTransform target;
        [Tooltip("Optional label that sinks while pressed.")]
        [SerializeField] private RectTransform label;
        [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.93f;
        [SerializeField, Min(0f)] private float labelSink = 7f;
        [SerializeField] private bool idlePulse;
        [SerializeField, Range(0f, 0.08f)] private float pulseAmount = 0.025f;
        [SerializeField, Min(0.1f)] private float pulseSpeed = 2.4f;

        private Button button;
        private bool pressed;
        private float scale = 1f;
        private float velocity;
        private float labelOffset;
        private Vector2 labelRest;
        private bool labelCaptured;

        public bool IsPressed => pressed;
        public float CurrentScale => scale;

        private void Awake()
        {
            if (target == null)
            {
                target = (RectTransform)transform;
            }

            button = GetComponent<Button>();
            CaptureLabel();
        }

        private void CaptureLabel()
        {
            if (label != null && !labelCaptured)
            {
                labelRest = label.anchoredPosition;
                labelCaptured = true;
            }
        }

        private void OnEnable()
        {
            pressed = false;
            scale = 1f;
            velocity = 0f;
            labelOffset = 0f;
            Apply();
        }

        private void OnDisable()
        {
            pressed = false;
            if (target != null)
            {
                target.localScale = Vector3.one;
            }

            if (label != null && labelCaptured)
            {
                label.anchoredPosition = labelRest;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Interactable)
            {
                pressed = true;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pressed = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
        }

        private bool Interactable => button == null || button.IsInteractable();

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float rest = 1f + (idlePulse && !pressed ? Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount : 0f);
            float goal = pressed ? pressedScale : rest;

            // underdamped spring: quick squash, small overshoot on release
            velocity += (goal - scale) * 420f * dt;
            velocity *= Mathf.Exp(-17f * dt);
            scale += velocity * dt;
            labelOffset = Mathf.Lerp(labelOffset, pressed ? labelSink : 0f, 1f - Mathf.Exp(-30f * dt));
            Apply();
        }

        private void Apply()
        {
            if (target != null)
            {
                target.localScale = new Vector3(scale, scale, 1f);
            }

            if (label != null && labelCaptured)
            {
                label.anchoredPosition = labelRest + new Vector2(0f, -labelOffset);
            }
        }
    }
}
