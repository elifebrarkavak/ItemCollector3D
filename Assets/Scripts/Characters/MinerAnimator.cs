using UnityEngine;

namespace ItemCollector3D.Characters
{
    /// <summary>
    /// Procedural life for a stylized miner (no Animator controller needed): breathing, head sway and a lamp glow while standing,
    /// leg and arm swing while moving. Only child transforms of the model are written, never the player root, the
    /// CharacterController or the camera. Uses unscaled time for the idle so the pause menu preview keeps breathing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinerAnimator : MonoBehaviour
    {
        [Header("Parts (pivots at the joints)")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Renderer lampLens;
        [SerializeField] private Light lampLight;

        [Header("Behaviour")]
        [Tooltip("Menu previews always idle; gameplay measures the horizontal speed of the model's root.")]
        [SerializeField] private bool menuPreview;
        [SerializeField, Min(0.1f)] private float walkSpeedForFullSwing = 5f;

        private Vector3 bodyRest;
        private Vector3 bodyScaleRest;
        private Quaternion headRest;
        private Quaternion leftArmRest, rightArmRest, leftLegRest, rightLegRest;
        private Vector3 lastPosition;
        private float speed01;
        private float walkPhase;
        private MaterialPropertyBlock lensBlock;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public bool MenuPreview { get => menuPreview; set => menuPreview = value; }
        public float Speed01 => speed01;

        public void Bind(Transform bodyT, Transform headT, Transform lArm, Transform rArm, Transform lLeg, Transform rLeg, Renderer lens, Light light)
        {
            body = bodyT; head = headT; leftArm = lArm; rightArm = rArm; leftLeg = lLeg; rightLeg = rLeg; lampLens = lens; lampLight = light;
        }

        public void SetLampLightEnabled(bool on)
        {
            if (lampLight != null)
            {
                lampLight.enabled = on;
            }
        }

        private void Awake()
        {
            CaptureRest();
            lastPosition = transform.position;
        }

        private void CaptureRest()
        {
            if (body != null) { bodyRest = body.localPosition; bodyScaleRest = body.localScale; }
            if (head != null) { headRest = head.localRotation; }
            if (leftArm != null) { leftArmRest = leftArm.localRotation; }
            if (rightArm != null) { rightArmRest = rightArm.localRotation; }
            if (leftLeg != null) { leftLegRest = leftLeg.localRotation; }
            if (rightLeg != null) { rightLegRest = rightLeg.localRotation; }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            float t = Time.unscaledTime;

            float targetSpeed = 0f;
            if (!menuPreview && dt > 1e-5f)
            {
                Vector3 delta = transform.position - lastPosition;
                delta.y = 0f;
                targetSpeed = Mathf.Clamp01(delta.magnitude / dt / walkSpeedForFullSwing);
            }

            lastPosition = transform.position;
            float smoothing = dt > 0f ? 1f - Mathf.Exp(-14f * dt) : 1f;
            speed01 = Mathf.Lerp(speed01, targetSpeed, smoothing);
            walkPhase += dt * Mathf.Lerp(0f, 11f, speed01);

            float idle = 1f - speed01;
            float breathe = Mathf.Sin(t * 2.1f);

            if (body != null)
            {
                float bounce = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.07f * speed01;
                body.localPosition = bodyRest + new Vector3(0f, bounce + breathe * 0.012f * idle, 0f);
                float s = 1f + breathe * 0.018f * idle;
                body.localScale = new Vector3(bodyScaleRest.x * (2f - s), bodyScaleRest.y * s, bodyScaleRest.z * (2f - s));
            }

            if (head != null)
            {
                float sway = Mathf.Sin(t * 1.3f) * 4f * idle;
                float nod = Mathf.Sin(t * 2.1f + 0.6f) * 1.5f * idle;
                head.localRotation = headRest * Quaternion.Euler(nod, sway, Mathf.Sin(t * 1.3f + 1f) * 2f * idle);
            }

            float swing = Mathf.Sin(walkPhase) * 38f * speed01;
            if (leftLeg != null) { leftLeg.localRotation = leftLegRest * Quaternion.Euler(swing, 0f, 0f); }
            if (rightLeg != null) { rightLeg.localRotation = rightLegRest * Quaternion.Euler(-swing, 0f, 0f); }
            float armIdle = Mathf.Sin(t * 1.8f) * 3f * idle;
            if (leftArm != null) { leftArm.localRotation = leftArmRest * Quaternion.Euler(-swing * 0.8f + armIdle, 0f, 0f); }
            if (rightArm != null) { rightArm.localRotation = rightArmRest * Quaternion.Euler(swing * 0.8f - armIdle, 0f, 0f); }

            if (lampLens != null)
            {
                lensBlock ??= new MaterialPropertyBlock();
                float glow = 1.6f + Mathf.Sin(t * 3.2f) * 0.25f;
                lensBlock.SetColor(EmissionColor, new Color(1f, 0.86f, 0.45f) * glow);
                lampLens.SetPropertyBlock(lensBlock);
            }
        }
    }
}
