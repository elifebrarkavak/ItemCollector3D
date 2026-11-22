using System;
using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// The visible life of a treasure: it RISES out of the ground when the dig finishes, and PLAYS A SHORT COLLECT animation (up, spin, shrink away)
    /// when it is collected. Epic treasure is clearly different: a slower, higher, spinning rise with an overshoot, then a floating hover with a soft
    /// spin and gold sparkles until it is collected, and a bigger, longer collect flourish.
    ///
    /// It only writes the local position / rotation / scale of the treasure's visual (never the spot, its collider or any radius) and puts the
    /// exact authored pose back when an animation ends or is cut short, so <see cref="TreasureSpot"/> state, dig and collect distances, gold,
    /// rewards and the save are untouched. It is created by the spot itself, uses scaled time (a pause freezes it, Resume continues) and allocates
    /// nothing per frame. The reward text, sound and burst are NOT delayed by it: they fire on the collect event, the visual just plays out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TreasureVisualAnimator : MonoBehaviour
    {
        public const float RevealSeconds = 0.45f;
        public const float EpicRevealSeconds = 0.8f;
        public const float CollectSeconds = 0.4f;
        public const float EpicCollectSeconds = 0.85f;

        public const float RevealSink = 0.45f;     // metres below the resting pose the treasure starts
        public const float EpicRevealSink = 0.7f;
        public const float CollectRise = 0.9f;     // metres it flies up while collected
        public const float EpicCollectRise = 1.7f;
        public const float EpicHoverHeight = 0.14f;
        public const float EpicHoverAmplitude = 0.05f;

        private enum Mode
        {
            None,
            Reveal,
            Hover,
            Collect,
        }

        private Mode mode;
        private Transform visual;
        private bool epic;
        private bool epicStyle;
        private float elapsed;
        private float duration;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 restScale;
        private Vector3 collectStartPosition;
        private Quaternion collectStartRotation;
        private Vector3 collectStartScale;
        private Action onCollectDone;
        private ParticleSystem sparkles;
        private bool sparklesTried;
        private Color accent = new(1f, 0.82f, 0.25f, 1f);

        public bool IsRevealing => mode == Mode.Reveal;
        public bool IsHovering => mode == Mode.Hover;
        public bool IsCollecting => mode == Mode.Collect;
        public bool IsAnimating => mode != Mode.None;
        public bool IsEpic => epic;
        public int RevealCount { get; private set; }
        public int CollectCount { get; private set; }

        /// <summary>Highest the visual rose above its resting pose during the current / last reveal or collect (metres).</summary>
        public float MaxHeightAboveRest { get; private set; }

        /// <summary>Deepest the visual was below its resting pose (metres): the start of the reveal.</summary>
        public float MaxDepthBelowRest { get; private set; }

        /// <summary>Largest scale factor reached (relative to the resting scale) during the current / last animation.</summary>
        public float MaxScaleFactor { get; private set; }

        /// <summary>Total yaw the visual turned during the current / last animation (degrees, absolute).</summary>
        public float MaxSpinDegrees { get; private set; }

        public ParticleSystem Sparkles => sparkles;

        public static float RevealDuration(bool epicTreasure) => epicTreasure ? EpicRevealSeconds : RevealSeconds;

        public static float CollectDuration(bool epicTreasure) => epicTreasure ? EpicCollectSeconds : CollectSeconds;

        /// <summary>Starts the rise out of the ground. The visual must already be active.</summary>
        public void PlayReveal(Transform target, bool epicTreasure, Color accentColor)
        {
            if (target == null)
            {
                return;
            }

            FinishNow();
            visual = target;
            epic = epicTreasure;
            accent = accentColor;
            CapturePose();
            elapsed = 0f;
            duration = RevealDuration(epic);
            mode = Mode.Reveal;
            RevealCount++;
            ResetStats();
            Apply(0f);
        }

        /// <summary>Starts the collect animation from the current pose. <paramref name="done"/> runs when it ends or is cut short.</summary>
        /// <returns>false when there is nothing to animate (the caller hides the visual itself).</returns>
        public bool PlayCollect(Transform target, bool epicTreasure, Action done)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            bool wasMine = visual == target && mode != Mode.None;
            if (!wasMine)
            {
                visual = target;
                CapturePose();
            }

            epic = epicTreasure;
            collectStartPosition = visual.localPosition;
            collectStartRotation = visual.localRotation;
            collectStartScale = visual.localScale;
            elapsed = 0f;
            duration = CollectDuration(epic);
            mode = Mode.Collect;
            onCollectDone = done;
            CollectCount++;
            ResetStats();
            StopSparkles();
            return true;
        }

        /// <summary>Ends whatever is playing at once and puts the visual back to its authored pose.</summary>
        public void FinishNow()
        {
            if (mode == Mode.None)
            {
                return;
            }

            bool wasCollect = mode == Mode.Collect;
            mode = Mode.None;
            RestorePose();
            StopSparkles();
            if (wasCollect)
            {
                Action done = onCollectDone;
                onCollectDone = null;
                done?.Invoke();
            }
        }

        private void OnDisable()
        {
            FinishNow();
        }

        private void Update()
        {
            if (mode == Mode.None || visual == null)
            {
                if (mode != Mode.None)
                {
                    mode = Mode.None;
                }

                return;
            }

            float dt = Time.deltaTime;
            elapsed += dt;
            switch (mode)
            {
                case Mode.Reveal:
                    if (elapsed >= duration)
                    {
                        if (epic)
                        {
                            mode = Mode.Hover;
                            elapsed = 0f;
                            StartSparkles();
                        }
                        else
                        {
                            mode = Mode.None;
                            RestorePose();
                        }

                        break;
                    }

                    Apply(elapsed / duration);
                    break;
                case Mode.Hover:
                    ApplyHover(elapsed);
                    break;
                case Mode.Collect:
                    if (elapsed >= duration)
                    {
                        mode = Mode.None;
                        RestorePose();
                        Action done = onCollectDone;
                        onCollectDone = null;
                        done?.Invoke();
                        break;
                    }

                    ApplyCollect(elapsed / duration);
                    break;
            }
        }

        // ------------------------------------------------------------------ poses

        /// <summary>Ease-out with a small overshoot (0 -> 1, passing about 1.1 on the way).</summary>
        public static float EaseOutBack(float t, float overshoot)
        {
            t = Mathf.Clamp01(t) - 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        private void Apply(float t)
        {
            float sink = epic ? EpicRevealSink : RevealSink;
            float e = EaseOutBack(t, epic ? 2.2f : 1.4f);
            float rise = -sink * (1f - e);
            if (epic)
            {
                rise += EpicHoverHeight * Mathf.SmoothStep(0f, 1f, t); // settles into the hover height
            }

            float scale = Mathf.LerpUnclamped(epic ? 0.45f : 0.6f, 1f, e);
            float spin = (epic ? 360f : 90f) * (1f - Mathf.SmoothStep(0f, 1f, t)); // unwinds to the authored yaw
            Pose(rise, scale, spin);
        }

        private void ApplyHover(float t)
        {
            float bob = EpicHoverHeight + EpicHoverAmplitude * Mathf.Sin(t * 2f * Mathf.PI * 0.9f);
            Pose(bob, 1f, t * 40f);
        }

        private void ApplyCollect(float t)
        {
            float rise = (epic ? EpicCollectRise : CollectRise) * (1f - Mathf.Pow(1f - t, 2.5f)); // fast up, easing out
            float swell = epic ? 1.35f : 1.15f;
            float scale = t < 0.3f ? Mathf.Lerp(1f, swell, t / 0.3f) : Mathf.Lerp(swell, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.3f) / 0.7f));
            float spin = (epic ? 720f : 360f) * t * t;
            visual.localPosition = collectStartPosition + Vector3.up * rise;
            visual.localRotation = Quaternion.AngleAxis(spin, Vector3.up) * collectStartRotation;
            visual.localScale = collectStartScale * scale;
            Track(collectStartPosition.y + rise - restPosition.y, scale * (collectStartScale.x / Mathf.Max(1e-5f, restScale.x)), spin);
        }

        private void Pose(float rise, float scale, float spinDegrees)
        {
            visual.localPosition = restPosition + Vector3.up * rise;
            visual.localRotation = Quaternion.AngleAxis(spinDegrees, Vector3.up) * restRotation;
            visual.localScale = restScale * scale;
            Track(rise, scale, spinDegrees);
        }

        private void Track(float rise, float scale, float spinDegrees)
        {
            MaxHeightAboveRest = Mathf.Max(MaxHeightAboveRest, rise);
            MaxDepthBelowRest = Mathf.Max(MaxDepthBelowRest, -rise);
            MaxScaleFactor = Mathf.Max(MaxScaleFactor, scale);
            MaxSpinDegrees = Mathf.Max(MaxSpinDegrees, Mathf.Abs(spinDegrees));
        }

        private void ResetStats()
        {
            MaxHeightAboveRest = 0f;
            MaxDepthBelowRest = 0f;
            MaxScaleFactor = 0f;
            MaxSpinDegrees = 0f;
        }

        private void CapturePose()
        {
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
            restScale = visual.localScale;
        }

        private void RestorePose()
        {
            if (visual != null)
            {
                visual.localPosition = restPosition;
                visual.localRotation = restRotation;
                visual.localScale = restScale;
            }
        }

        // ------------------------------------------------------------------ epic sparkles (re-using the collect burst's particle material)

        private void StartSparkles()
        {
            if (!sparklesTried)
            {
                sparklesTried = true;
                BuildSparkles();
            }

            if (sparkles != null)
            {
                ParticleSystem.MainModule main = sparkles.main;
                main.startColor = accent;
                sparkles.transform.position = visual != null ? visual.position : transform.position;
                if (!sparkles.isPlaying)
                {
                    sparkles.Play();
                }
            }
        }

        private void StopSparkles()
        {
            if (sparkles != null && sparkles.isPlaying)
            {
                sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void BuildSparkles()
        {
            CollectBurstEffect burst = FindAnyObjectByType<CollectBurstEffect>();
            Material material = burst != null ? burst.ParticleMaterial : null;
            if (material == null)
            {
                return; // no shared particle material in this scene: the motion alone tells Epic apart
            }

            var go = new GameObject("EpicSparkles");
            go.transform.SetParent(transform, false);
            sparkles = go.AddComponent<ParticleSystem>();
            sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = sparkles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 0.9f;
            main.startSpeed = 0.35f;
            main.startSize = 0.07f;
            main.gravityModifier = -0.15f; // drifts upward
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            ParticleSystem.EmissionModule emission = sparkles.emission;
            emission.rateOverTime = 14f;
            ParticleSystem.ShapeModule shape = sparkles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // a ring on the ground plane
            ParticleSystem.SizeOverLifetimeModule size = sparkles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }
    }
}
