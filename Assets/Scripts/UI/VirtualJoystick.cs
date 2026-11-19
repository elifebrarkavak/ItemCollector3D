using ItemCollector3D.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ItemCollector3D.UI
{
    [DisallowMultipleComponent]
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private ThirdPersonPlayerController playerController;
        [SerializeField, Range(0.1f, 1f)] private float handleRange = 0.55f;
        [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.08f;

        [Header("Look (optional)")]
        [Tooltip("The translucent base: calmer while idle, fully visible while a finger is on the joystick.")]
        [SerializeField] private Graphic fadeTarget;
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.7f;
        [SerializeField, Range(0f, 1f)] private float activeAlpha = 1f;

        private Canvas parentCanvas;
        private int activePointerId = int.MinValue;

        public Vector2 Value { get; private set; }

        private void Awake()
        {
            parentCanvas = GetComponentInParent<Canvas>();
            background ??= transform as RectTransform;
            SetFade(idleAlpha);
        }

        private void OnDisable()
        {
            ResetInput();
        }

        public void SetPlayerController(ThirdPersonPlayerController controller)
        {
            playerController = controller;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != int.MinValue)
            {
                return;
            }

            activePointerId = eventData.pointerId;
            UpdateInput(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                UpdateInput(eventData);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                ResetInput();
            }
        }

        private void UpdateInput(PointerEventData eventData)
        {
            Camera eventCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? parentCanvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background,
                    eventData.position,
                    eventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Vector2 radius = background.rect.size * 0.5f;
            Vector2 normalized = new(
                radius.x > 0f ? localPoint.x / radius.x : 0f,
                radius.y > 0f ? localPoint.y / radius.y : 0f);

            SetFade(activeAlpha);
            Value = Vector2.ClampMagnitude(normalized, 1f);
            if (Value.magnitude < deadZone)
            {
                Value = Vector2.zero;
            }

            if (handle != null)
            {
                handle.anchoredPosition = new Vector2(
                    Value.x * radius.x * handleRange,
                    Value.y * radius.y * handleRange);
            }

            playerController?.SetMoveInput(Value);
        }

        private void ResetInput()
        {
            activePointerId = int.MinValue;
            Value = Vector2.zero;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }

            SetFade(idleAlpha);
            playerController?.ClearMoveInput();
        }

        private void SetFade(float alpha)
        {
            if (fadeTarget == null)
            {
                return;
            }

            Color color = fadeTarget.color;
            color.a = alpha;
            fadeTarget.color = color;
        }
    }
}
