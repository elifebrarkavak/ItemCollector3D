using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ItemCollector3D.Player
{
    [DisallowMultipleComponent]
    public sealed class ThirdPersonCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new(0f, 1.4f, 0f);

        [Header("Orbit")]
        [SerializeField, Min(0.1f)] private float distance = 5f;
        [SerializeField] private float yaw;
        [SerializeField, Range(-80f, 80f)] private float pitch = 20f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Min(0f)] private float touchSensitivity = 0.08f;
        [SerializeField] private Vector2 pitchLimits = new(-30f, 65f);

        [Header("Smoothing")]
        [SerializeField, Min(0f)] private float positionSmoothTime = 0.08f;

        [Header("Collision")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
        [SerializeField, Min(0f)] private float collisionPadding = 0.1f;
        [SerializeField, Min(0.1f)] private float minimumDistance = 0.5f;

        private Vector3 positionVelocity;
        private int activeOrbitTouchId = -1;

        public Transform Target => target;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            ReadOrbitInput();

            pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + targetOffset;
            Vector3 cameraDirection = rotation * Vector3.back;
            float resolvedDistance = ResolveCollisionDistance(pivot, cameraDirection);
            Vector3 desiredPosition = pivot + cameraDirection * resolvedDistance;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime);
            transform.rotation = rotation;
        }

        private void ReadOrbitInput()
        {
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                yaw += delta.x * mouseSensitivity;
                pitch -= delta.y * mouseSensitivity;
            }

            if (Touchscreen.current == null)
            {
                activeOrbitTouchId = -1;
                return;
            }

            foreach (var touch in Touchscreen.current.touches)
            {
                int touchId = touch.touchId.ReadValue();
                bool isActiveTouch = touchId == activeOrbitTouchId;

                if (activeOrbitTouchId < 0 && touch.press.wasPressedThisFrame)
                {
                    Vector2 position = touch.position.ReadValue();
                    bool overUi = EventSystem.current != null
                        && EventSystem.current.IsPointerOverGameObject(touchId);

                    if (position.x >= Screen.width * 0.5f && !overUi)
                    {
                        activeOrbitTouchId = touchId;
                        isActiveTouch = true;
                    }
                }

                if (!isActiveTouch)
                {
                    continue;
                }

                if (touch.press.isPressed)
                {
                    Vector2 delta = touch.delta.ReadValue();
                    yaw += delta.x * touchSensitivity;
                    pitch -= delta.y * touchSensitivity;
                }

                if (touch.press.wasReleasedThisFrame)
                {
                    activeOrbitTouchId = -1;
                }
            }
        }

        private float ResolveCollisionDistance(Vector3 pivot, Vector3 cameraDirection)
        {
            if (!Physics.SphereCast(
                    pivot,
                    collisionRadius,
                    cameraDirection,
                    out RaycastHit hit,
                    distance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return distance;
            }

            return Mathf.Clamp(hit.distance - collisionPadding, minimumDistance, distance);
        }
    }
}
