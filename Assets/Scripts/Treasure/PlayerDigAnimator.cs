using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Short dig motion for the player, applied ONLY to a visual child (never to the player root), so the real
    /// Transform, the CharacterController and the camera follow are untouched. The motion is a placeholder for the
    /// capsule (forward tilt + dip + squash, two strokes). To use a real miner later, assign an <see cref="Animator"/>
    /// (plus trigger name) and/or point <c>visual</c> at the miner model: the procedural motion is skipped when an
    /// Animator is assigned. The visual returns exactly to its rest pose when the dig ends or the component is disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDigAnimator : MonoBehaviour
    {
        [Tooltip("Child transform that is animated (e.g. the player model). Falls back to a child named 'Visual'.")]
        [SerializeField] private Transform visual;
        [Tooltip("Optional: a real miner Animator. When set, the trigger below is fired instead of the procedural motion.")]
        [SerializeField] private Animator animator;
        [SerializeField] private string digTrigger = "Dig";

        [Header("Placeholder motion")]
        [SerializeField, Range(0f, 45f)] private float tiltDegrees = 30f;
        [Tooltip("Side-to-side lean per stroke (alternating), so the motion is readable from the camera behind the player.")]
        [SerializeField, Range(0f, 25f)] private float rollDegrees = 9f;
        [SerializeField, Min(0f)] private float dipDistance = 0.18f;
        [SerializeField, Range(0f, 0.4f)] private float squash = 0.15f;
        [SerializeField, Min(1)] private int strokes = 2;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 restScale;
        private bool restCaptured;
        private float elapsed;
        private float duration;

        public bool IsPlaying { get; private set; }
        public int PlayCount { get; private set; }
        public int CancelCount { get; private set; }
        public int Strokes => strokes;
        public Transform Visual => visual;

        /// <summary>Largest distance the visual moved from its rest position during the current/last dig (metres).</summary>
        public float MaxVisualOffset { get; private set; }

        /// <summary>Largest tilt (degrees) applied during the current/last dig.</summary>
        public float MaxTiltApplied { get; private set; }

        /// <summary>0 at the start and end, 1 at each stroke impact ((k + 0.5) / strokes).</summary>
        public static float StrokeCurve(float normalizedTime, int strokeCount)
        {
            return Mathf.Abs(Mathf.Sin(Mathf.PI * Mathf.Max(1, strokeCount) * Mathf.Clamp01(normalizedTime)));
        }

        public bool IsAtRest()
        {
            if (visual == null || !restCaptured)
            {
                return true;
            }

            return (visual.localPosition - restPosition).sqrMagnitude < 1e-8f
                && Quaternion.Angle(visual.localRotation, restRotation) < 0.01f
                && (visual.localScale - restScale).sqrMagnitude < 1e-8f;
        }

        private void Awake()
        {
            if (visual == null)
            {
                visual = transform.Find("Visual");
            }

            CaptureRest();
        }

        private void OnDisable()
        {
            Stop();
        }

        public void Play(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            CaptureRest();
            elapsed = 0f;
            duration = seconds;
            IsPlaying = true;
            PlayCount++;
            MaxVisualOffset = 0f;
            MaxTiltApplied = 0f;

            if (animator != null && !string.IsNullOrEmpty(digTrigger))
            {
                animator.SetTrigger(digTrigger);
            }
        }

        /// <summary>Aborts a running dig motion and puts the visual straight back to its rest pose.</summary>
        public void Cancel()
        {
            if (!IsPlaying)
            {
                return;
            }

            CancelCount++;
            Stop();
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                Stop();
                return;
            }

            if (animator == null && visual != null)
            {
                Apply(elapsed / duration);
            }
        }

        private void Apply(float t)
        {
            float s = StrokeCurve(t, strokes);
            float tilt = tiltDegrees * s;
            int stroke = Mathf.Min(strokes - 1, Mathf.FloorToInt(t * strokes));
            float roll = rollDegrees * s * (stroke % 2 == 0 ? 1f : -1f);
            visual.localRotation = restRotation * Quaternion.Euler(tilt, 0f, roll);
            visual.localPosition = restPosition + Vector3.down * (dipDistance * s);
            visual.localScale = Vector3.Scale(restScale, new Vector3(1f + squash * 0.5f * s, 1f - squash * s, 1f + squash * 0.5f * s));

            MaxTiltApplied = Mathf.Max(MaxTiltApplied, tilt);
            MaxVisualOffset = Mathf.Max(MaxVisualOffset, (visual.localPosition - restPosition).magnitude);
        }

        private void Stop()
        {
            IsPlaying = false;
            if (visual != null && restCaptured)
            {
                visual.localPosition = restPosition;
                visual.localRotation = restRotation;
                visual.localScale = restScale;
            }
        }

        private void CaptureRest()
        {
            if (visual == null || IsPlaying)
            {
                return;
            }

            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
            restScale = visual.localScale;
            restCaptured = true;
        }
    }
}
