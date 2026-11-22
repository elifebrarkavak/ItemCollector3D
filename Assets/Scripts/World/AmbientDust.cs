using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// A few specks of sand and dust drifting on the canyon wind: ONE small ParticleSystem (built here, like the dig dirt), a hard limit of <see cref="MaxParticles"/> live specks, opaque shared
    /// material (no transparency: the specks shrink away instead of fading), world-space motion along a gentle wind, no collisions, no lights, no sub-emitters. Specks are tiny (4 - 10 cm), so
    /// they never block the view or compete with the treasure effects.
    ///
    /// Two modes: <b>follow</b> (a box of air round the player, rate per area from the <see cref="CanyonZoneTracker"/> weights: a little at the outpost, more in the open riverbed and at the
    /// overlook, almost none in the ravine) and <b>fixed</b> (the lane of wind across the bridge: it only emits while the player is within <see cref="activeRadius"/> metres, in slow gusts).
    /// The rate is refreshed four times a second, not every frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientDust : MonoBehaviour
    {
        public const int MaxParticles = 36;

        [SerializeField] private Material material;
        [SerializeField] private CanyonZoneTracker zones;
        [SerializeField] private ThirdPersonCameraController cameraController;

        [Header("Where")]
        [Tooltip("true: the box follows the player. false: it stays where it was placed and only emits while the player is near.")]
        [SerializeField] private bool followPlayer = true;
        [SerializeField] private Vector3 area = new(16f, 3f, 16f);
        [SerializeField, Min(5f)] private float activeRadius = 40f;

        [Header("How much (specks per second)")]
        [Tooltip("Follow mode: rate per area, in the order Outpost, Riverbed, Railway, Overlook, Ravine. Fixed mode: only the first value is used.")]
        [SerializeField] private float[] zoneRates = { 1.4f, 2.0f, 1.0f, 1.6f, 0.3f };

        [Header("Look")]
        [SerializeField] private Vector3 wind = new(1.1f, 0.05f, 0.45f);
        [SerializeField] private Color color = new(0.86f, 0.74f, 0.56f);
        [SerializeField] private Vector2 size = new(0.04f, 0.10f);
        [SerializeField] private Vector2 lifetime = new(5f, 8f);
        [Tooltip("0 = steady. 1 = the rate swings between nothing and double in slow gusts.")]
        [SerializeField, Range(0f, 1f)] private float gustiness = 0.4f;

        private ParticleSystem particles;
        private Transform target;
        private float nextRate;
        private float currentRate;

        public ParticleSystem Particles => particles;
        public float CurrentRate => currentRate;
        public bool FollowsPlayer => followPlayer;

        private void Awake()
        {
            if (zones == null)
            {
                zones = FindAnyObjectByType<CanyonZoneTracker>();
            }

            if (cameraController == null)
            {
                cameraController = FindAnyObjectByType<ThirdPersonCameraController>();
            }

            Build();
        }

        private void Build()
        {
            var go = new GameObject("Specks");
            go.transform.SetParent(transform, false);
            particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxParticles;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;

            // drift along the wind, no turbulence, no collisions
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x * 0.7f, wind.x * 1.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(wind.y * 0.5f, wind.y * 1.5f + 0.05f);
            velocity.z = new ParticleSystem.MinMaxCurve(wind.z * 0.7f, wind.z * 1.3f);

            // grow in and shrink away instead of fading: works with the cheap opaque material
            ParticleSystem.SizeOverLifetimeModule sizeOverLife = particles.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 0f)));

            var rendererComponent = go.GetComponent<ParticleSystemRenderer>();
            rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
            rendererComponent.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rendererComponent.receiveShadows = false;
            if (material != null)
            {
                rendererComponent.sharedMaterial = material;
            }
        }

        private void OnEnable()
        {
            if (particles != null)
            {
                particles.Play();
            }
        }

        private void OnDisable()
        {
            if (particles != null)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void LateUpdate()
        {
            if (particles == null || GamePause.IsPaused)
            {
                return;
            }

            if (target == null && cameraController != null)
            {
                target = cameraController.Target;
            }

            if (target == null)
            {
                return;
            }

            if (followPlayer)
            {
                transform.position = target.position + new Vector3(0f, 0.6f, 0f);
            }

            if (Time.time < nextRate)
            {
                return;
            }

            nextRate = Time.time + 0.25f;
            float rate;
            if (followPlayer)
            {
                rate = 0f;
                for (int z = 0; z < zoneRates.Length && z < CanyonZoneTracker.ZoneCount; z++)
                {
                    rate += (zones != null ? zones.Weight((CanyonZone)z) : 0f) * zoneRates[z];
                }
            }
            else
            {
                float distance = Vector3.Distance(target.position, transform.position);
                rate = distance <= activeRadius ? (zoneRates.Length > 0 ? zoneRates[0] : 1f) : 0f;
            }

            float t = Time.time;
            float gust = 1f + gustiness * (Mathf.Sin(t * 0.43f) * 0.6f + Mathf.Sin(t * 0.17f + 2f) * 0.4f);
            currentRate = Mathf.Max(0f, rate * gust);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = currentRate;
        }
    }
}
