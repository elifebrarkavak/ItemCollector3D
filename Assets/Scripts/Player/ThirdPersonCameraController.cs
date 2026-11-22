using ItemCollector3D.Menus;
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

        [Header("Narrow places (only while a level volume such as the mine has raised the minimum distance)")]
        [Tooltip("When the free orbit distance is shorter than this, the camera swings around the player towards open space instead of pressing against the wall.")]
        [SerializeField, Min(0f)] private float swingComfortDistance = 2.6f;
        [SerializeField, Min(0f)] private float swingSpeed = 300f;

        private Vector3 positionVelocity;
        private int activeOrbitTouchId = -1;
        private bool orbitInputActive;
        private float environmentDistanceScale = 1f;
        private float environmentMinimumDistance;

        public Transform Target => target;

        /// <summary>The orbit distance currently in force (the configured distance times the level's scale, e.g. shortened inside the mine).</summary>
        public float EffectiveDistance => distance * environmentDistanceScale;

        /// <summary>
        /// Lets a level volume (the mine) shorten the orbit and raise the collision floor so the camera stays inside narrow spaces and keeps the player in
        /// view. (1, 0) restores the defaults.
        /// </summary>
        public void SetEnvironmentLimits(float distanceScale, float minimumDistanceFloor)
        {
            environmentDistanceScale = Mathf.Clamp(distanceScale, 0.2f, 1f);
            environmentMinimumDistance = Mathf.Max(0f, minimumDistanceFloor);
        }

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
            float resolvedDistance = ResolveCollisionDistance(pivot, cameraDirection, out float freeDistance);
            if (environmentMinimumDistance > 0f && !orbitInputActive && freeDistance < SwingComfort)
            {
                // a wall right behind the player (a narrow tunnel while the camera looks sideways): swing the orbit round to the nearest open side rather than
                // squeezing the camera against the wall, where the player's head would fill the screen. The player's own orbit input always wins.
                float offset = OpenYawOffset(pivot);
                if (offset != 0f)
                {
                    yaw = Mathf.MoveTowardsAngle(yaw, yaw + offset, swingSpeed * Time.deltaTime);
                    rotation = Quaternion.Euler(pitch, yaw, 0f);
                    cameraDirection = rotation * Vector3.back;
                    resolvedDistance = ResolveCollisionDistance(pivot, cameraDirection, out freeDistance);
                    if (freeDistance < Mathf.Min(environmentMinimumDistance, EffectiveDistance))
                    {
                        resolvedDistance = Mathf.Clamp(freeDistance, minimumDistance, EffectiveDistance); // mid-swing: never place the camera inside the wall
                    }
                }
            }

            if (environmentMinimumDistance > 0f && freeDistance < Mathf.Min(environmentMinimumDistance, EffectiveDistance))
            {
                // squeezed between the player and a wall (narrow places such as the mine): swing the camera up and over the player instead of pushing it into the wall
                float floor = Mathf.Min(environmentMinimumDistance, EffectiveDistance);
                for (float extra = 15f; extra <= 75f; extra += 15f)
                {
                    Vector3 lifted = Quaternion.Euler(Mathf.Min(pitch + extra, 80f), yaw, 0f) * Vector3.back;
                    float liftedDistance = ResolveCollisionDistance(pivot, lifted, out float liftedFree);
                    if (liftedFree >= floor)
                    {
                        cameraDirection = lifted;
                        resolvedDistance = liftedDistance;
                        rotation = Quaternion.LookRotation(-lifted, Vector3.up);
                        break;
                    }
                }
            }

            Vector3 desiredPosition = pivot + cameraDirection * resolvedDistance;

            Vector3 smoothed = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime);
            if (environmentMinimumDistance > 0f && IsInsideScenery(smoothed))
            {
                // the eased path cut a corner (e.g. a timber post at the mine mouth while the player walks on): jump to the safe position instead
                smoothed = desiredPosition;
                positionVelocity = Vector3.zero;
            }

            transform.position = smoothed;
            transform.rotation = rotation;
        }

        private float SwingComfort => Mathf.Min(swingComfortDistance, EffectiveDistance);

        /// <summary>The smallest yaw change (degrees, either side) after which the orbit has at least the comfortable free distance; 0 when none does.</summary>
        private float OpenYawOffset(Vector3 pivot)
        {
            float comfort = SwingComfort;
            for (int step = 1; step <= 9; step++)
            {
                float offset = step * 20f;
                float freeLeft = FreeDistanceAtYaw(pivot, yaw + offset);
                float freeRight = FreeDistanceAtYaw(pivot, yaw - offset);
                if (freeLeft >= comfort || freeRight >= comfort)
                {
                    return freeLeft >= freeRight ? offset : -offset;
                }
            }

            return 0f;
        }

        private float FreeDistanceAtYaw(Vector3 pivot, float yawDegrees)
        {
            ResolveCollisionDistance(pivot, Quaternion.Euler(pitch, yawDegrees, 0f) * Vector3.back, out float free);
            return free;
        }

        private void ReadOrbitInput()
        {
            orbitInputActive = false;
            if (GamePause.IsPaused)
            {
                return; // the orbit reads raw input without deltaTime, so it must be stopped explicitly while paused
            }

            float sensitivityScale = GameSettings.CameraSensitivityScale; // Settings > Camera Sensitivity

            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                orbitInputActive = true;
                Vector2 delta = Mouse.current.delta.ReadValue();
                yaw += delta.x * mouseSensitivity * sensitivityScale;
                pitch -= delta.y * mouseSensitivity * sensitivityScale;
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
                    orbitInputActive = true;
                    Vector2 delta = touch.delta.ReadValue();
                    yaw += delta.x * touchSensitivity * sensitivityScale;
                    pitch -= delta.y * touchSensitivity * sensitivityScale;
                }

                if (touch.press.wasReleasedThisFrame)
                {
                    activeOrbitTouchId = -1;
                }
            }
        }

        private static readonly Collider[] OverlapBuffer = new Collider[8];

        private bool IsInsideScenery(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position, collisionRadius * 0.7f, OverlapBuffer, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (target == null || !OverlapBuffer[i].transform.IsChildOf(target))
                {
                    return true;
                }
            }

            return false;
        }

        private float ResolveCollisionDistance(Vector3 pivot, Vector3 cameraDirection, out float freeDistance)
        {
            float maxDistance = EffectiveDistance;
            if (!Physics.SphereCast(
                    pivot,
                    collisionRadius,
                    cameraDirection,
                    out RaycastHit hit,
                    maxDistance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                freeDistance = maxDistance;
                return maxDistance;
            }

            freeDistance = hit.distance - collisionPadding;
            float floor = Mathf.Min(Mathf.Max(minimumDistance, environmentMinimumDistance), maxDistance);
            return Mathf.Clamp(freeDistance, floor, maxDistance);
        }
    }
}
