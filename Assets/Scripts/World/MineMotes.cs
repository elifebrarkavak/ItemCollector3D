using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// A few slow specks of light or dust for one place in the Ancient Mine (stage 16D): crystal glints, grotto motes, workshop dust, the vault's floating sparks. ONE small ParticleSystem per place
    /// (built here, like <see cref="AmbientDust"/>), a hard limit of <see cref="maxParticles"/> live specks, an opaque shared unlit material (no transparency: the specks grow in and shrink away
    /// instead of fading, so there is no overdraw), world-space motion, no collisions, no lights, no sub-emitters. Specks are tiny (4 - 12 cm), so they never hide a treasure or the player.
    /// A place is only emitting while the player is close (<see cref="activeRadius"/>, the rate fades out towards the edge) and, for a place tied to one area, while the player is in it;
    /// otherwise its system is stopped and cleared. The rate is refreshed four times a second.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MineMotes : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private MineZoneTracker zones;
        [SerializeField] private ThirdPersonCameraController cameraController;

        [Header("Where")]
        [Tooltip("true: the box of air follows the player (used with Only In Zone). false: it stays where it was placed.")]
        [SerializeField] private bool followPlayer;
        [SerializeField] private Vector3 area = new(10f, 3f, 10f);
        [SerializeField, Min(5f)] private float activeRadius = 35f;
        [Tooltip("Only emits while the player is in this area.")]
        [SerializeField] private bool onlyInZone;
        [SerializeField] private MineZone zone;

        [Header("How much")]
        [SerializeField, Range(1, 40)] private int maxParticles = 12;
        [Tooltip("Specks per second when the player is close.")]
        [SerializeField, Min(0f)] private float rate = 1f;

        [Header("Look")]
        [SerializeField] private Color colorA = new(0.6f, 0.9f, 1f);
        [SerializeField] private Color colorB = new(0.8f, 0.7f, 1f);
        [SerializeField] private Vector2 size = new(0.05f, 0.11f);
        [SerializeField] private Vector2 lifetime = new(3f, 5f);
        [Tooltip("Slow drift in metres per second (mostly up).")]
        [SerializeField] private Vector3 drift = new(0.03f, 0.18f, 0.02f);

        private ParticleSystem particles;
        private Transform target;
        private float nextRate;
        private float currentRate;
        private bool playing;

        public ParticleSystem Particles => particles;
        public int MaxParticles => maxParticles;
        public float CurrentRate => currentRate;
        public MineZone Zone => zone;
        public bool OnlyInZone => onlyInZone;
        public float ActiveRadius => activeRadius;

        private void Awake()
        {
            if (zones == null)
            {
                zones = FindAnyObjectByType<MineZoneTracker>();
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
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; // at most 16 specks, only near the player: simulating them off screen is cheaper than a pause / catch-up

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-drift.x, drift.x);
            velocity.y = new ParticleSystem.MinMaxCurve(drift.y * 0.5f, drift.y * 1.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(-drift.z, drift.z);

            // grow in and shrink away instead of fading: works with the cheap opaque material
            ParticleSystem.SizeOverLifetimeModule sizeOverLife = particles.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(0.7f, 0.85f), new Keyframe(1f, 0f)));

            var rendererComponent = go.GetComponent<ParticleSystemRenderer>();
            rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
            rendererComponent.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rendererComponent.receiveShadows = false;
            if (material != null)
            {
                rendererComponent.sharedMaterial = material;
            }
        }

        private void OnDisable()
        {
            if (particles != null)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                playing = false;
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
                transform.position = target.position + new Vector3(0f, 0.8f, 0f);
            }

            if (Time.time < nextRate)
            {
                return;
            }

            nextRate = Time.time + 0.25f;
            Vector3 d = target.position - transform.position;
            float distance = followPlayer ? 0f : Mathf.Sqrt(d.x * d.x + d.z * d.z) + Mathf.Abs(d.y) * 2f; // another floor above or below does not count
            float near = Mathf.Clamp01((activeRadius - distance) / (activeRadius * 0.4f));
            bool inZone = !onlyInZone || (zones != null && zones.HasZone && zones.Current == zone);
            currentRate = inZone ? rate * near : 0f;

            if (currentRate > 0f && !playing)
            {
                particles.Play();
                playing = true;
            }
            else if (currentRate <= 0f && playing && particles.particleCount == 0)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                playing = false;
            }

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = currentRate;
        }
    }
}
