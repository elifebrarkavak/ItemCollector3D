using UnityEngine;

namespace ItemCollector3D.Characters
{
    /// <summary>
    /// Procedural life for a stylized miner (no Animator controller needed): breathing, head sway and a lamp glow while standing,
    /// leg and arm swing while moving. Only child transforms of the model are written, never the player root, the
    /// CharacterController or the camera. Uses unscaled time for the idle so the pause menu preview keeps breathing.
    /// While digging, <c>PlayerDigAnimator</c> drives <see cref="SetDigPose"/>: both arms (and the pickaxe, which is flipped in the right hand so
    /// its head leads the swing) go from a raised wind-up to a chop at the ground and back; everything returns exactly to rest afterwards.
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
        private Transform pickaxe, hand;
        private Vector3 pickRestPosition;
        private Quaternion pickRestRotation;
        private bool pickMoved;
        private float digAmount, digWeight;
        private float speed01;
        private float walkPhase;
        private MaterialPropertyBlock lensBlock;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public bool MenuPreview { get => menuPreview; set => menuPreview = value; }
        public float Speed01 => speed01;

        /// <summary>Arm swing of the dig pose: 0 = raised wind-up, 1 = the chop at the ground.</summary>
        public float DigAmount => digAmount;

        /// <summary>How much of the dig pose is applied (0 = none, the normal walk / idle arms).</summary>
        public float DigWeight => digWeight;

        /// <summary>Raised wind-up and ground-strike angles of the arms (degrees about the shoulder; negative = raised forward / up).</summary>
        public const float DigRaisedAngle = -165f;
        public const float DigStrikeAngle = -35f;

        public Transform RightArm => rightArm;
        public Transform LeftArm => leftArm;
        public Quaternion RightArmRest => rightArmRest;
        public Quaternion LeftArmRest => leftArmRest;
        public Transform Pickaxe => pickaxe;
        public Vector3 PickRestPosition => pickRestPosition;
        public Quaternion PickRestRotation => pickRestRotation;

        /// <summary>Sets the dig pose for this frame (called every frame while digging). <paramref name="amount"/> 0 = raised, 1 = strike.</summary>
        public void SetDigPose(float amount, float weight)
        {
            digAmount = Mathf.Clamp01(amount);
            digWeight = Mathf.Clamp01(weight);
        }

        /// <summary>Ends the dig pose; the arms and the pickaxe go straight back to their normal pose.</summary>
        public void ClearDigPose()
        {
            digAmount = 0f;
            digWeight = 0f;
        }

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

        private void OnDisable()
        {
            ClearDigPose();
            RestorePick();
        }

        private void CaptureRest()
        {
            if (body != null) { bodyRest = body.localPosition; bodyScaleRest = body.localScale; }
            if (head != null) { headRest = head.localRotation; }
            if (leftArm != null) { leftArmRest = leftArm.localRotation; }
            if (rightArm != null) { rightArmRest = rightArm.localRotation; }
            if (leftLeg != null) { leftLegRest = leftLeg.localRotation; }
            if (rightLeg != null) { rightLegRest = rightLeg.localRotation; }
            if (rightArm != null)
            {
                pickaxe = rightArm.Find("Pickaxe");
                hand = rightArm.Find("Hand");
                if (pickaxe != null)
                {
                    pickRestPosition = pickaxe.localPosition;
                    pickRestRotation = pickaxe.localRotation;
                }
            }
        }

        private void RestorePick()
        {
            if (pickMoved && pickaxe != null)
            {
                pickaxe.localPosition = pickRestPosition;
                pickaxe.localRotation = pickRestRotation;
            }

            pickMoved = false;
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
            float leftAngle = -swing * 0.8f + armIdle;
            float rightAngle = swing * 0.8f - armIdle;
            if (digWeight > 0f)
            {
                // both hands on the pick: wind up high, chop down (the strike is the stroke impact), blended in and out by the weight
                float dig = Mathf.Lerp(DigRaisedAngle, DigStrikeAngle, digAmount);
                leftAngle = Mathf.Lerp(leftAngle, dig, digWeight);
                rightAngle = Mathf.Lerp(rightAngle, dig, digWeight);
            }

            if (leftArm != null) { leftArm.localRotation = leftArmRest * Quaternion.Euler(leftAngle, 0f, 0f); }
            if (rightArm != null) { rightArm.localRotation = rightArmRest * Quaternion.Euler(rightAngle, 0f, 0f); }
            if (pickaxe != null)
            {
                if (digWeight > 0f)
                {
                    // flip the pick in the hand (about the grip, in the arm's swing plane) so its head, not its handle, leads the chop
                    pickaxe.localPosition = pickRestPosition;
                    pickaxe.localRotation = pickRestRotation;
                    Vector3 grip = hand != null ? hand.position : pickaxe.position;
                    pickaxe.RotateAround(grip, rightArm.TransformDirection(Vector3.right), 180f * digWeight);
                    pickMoved = true;
                }
                else
                {
                    RestorePick();
                }
            }

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
