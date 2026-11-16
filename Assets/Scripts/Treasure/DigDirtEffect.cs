using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// Low-count earth-coloured dirt burst at the dig spot. ONE reusable ParticleSystem (no per-dig allocation);
    /// particles die by lifetime. Particles are sprayed up and outwards, with a wide cone and generous size, so they
    /// stay visible even when the camera looks over the player's shoulder. Colours, counts, size and the material are
    /// placeholders that can be changed in the Inspector (final art: swap the material / numbers).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DigDirtEffect : MonoBehaviour
    {
        [SerializeField] private Material particleMaterial;

        [Header("Earth tones (random between the two)")]
        [SerializeField] private Color lightDirt = new(0.60f, 0.42f, 0.25f, 1f);
        [SerializeField] private Color darkDirt = new(0.33f, 0.21f, 0.11f, 1f);

        [Header("Counts - kept low for mobile")]
        [SerializeField, Min(1)] private int particlesPerStroke = 8;
        [SerializeField, Min(1)] private int particlesOnReveal = 14;

        [Header("Look")]
        [SerializeField, Min(0.1f)] private float minSpeed = 3f;
        [SerializeField, Min(0.1f)] private float maxSpeed = 6f;
        [SerializeField, Min(0.1f)] private float minLifetime = 0.6f;
        [SerializeField, Min(0.1f)] private float maxLifetime = 0.9f;
        [SerializeField, Min(0.01f)] private float minSize = 0.2f;
        [SerializeField, Min(0.01f)] private float maxSize = 0.32f;
        [SerializeField, Range(5f, 80f)] private float coneAngle = 65f;
        [SerializeField, Min(0f)] private float spawnHeight = 0.1f;

        private ParticleSystem particles;

        public int TotalEmitted { get; private set; }
        public int LastEmitCount { get; private set; }
        public Vector3 LastEmitPosition { get; private set; }
        public int ParticlesPerStroke => particlesPerStroke;
        public int ParticlesOnReveal => particlesOnReveal;
        public int LiveParticleCount => particles != null ? particles.particleCount : 0;
        public int MaxParticleCap => particles != null ? particles.main.maxParticles : 0;
        public Color LightDirt => lightDirt;
        public Color DarkDirt => darkDirt;

        private void Awake()
        {
            BuildParticles();
        }

        public void PlayStroke(Vector3 position)
        {
            Emit(position, particlesPerStroke);
        }

        public void PlayReveal(Vector3 position)
        {
            Emit(position, particlesOnReveal);
        }

        private void Emit(Vector3 position, int count)
        {
            if (particles == null || count <= 0)
            {
                return;
            }

            LastEmitPosition = position + Vector3.up * spawnHeight;
            LastEmitCount = count;
            TotalEmitted += count;
            particles.transform.position = LastEmitPosition;
            particles.Emit(count);
        }

        private void BuildParticles()
        {
            var go = new GameObject("DirtParticles");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // cone points up
            particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(lightDirt, darkDirt);
            main.gravityModifier = 0.9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = (particlesPerStroke * 4) + particlesOnReveal * 2;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false; // emitted from code via Emit()

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = 0.2f;

            // Shrink away instead of alpha-fading: works with a cheap opaque material.
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f)));

            var rendererComponent = go.GetComponent<ParticleSystemRenderer>();
            rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
            if (particleMaterial != null)
            {
                rendererComponent.sharedMaterial = particleMaterial;
            }
        }
    }
}
