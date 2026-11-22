using UnityEngine;

namespace ItemCollector3D.Treasure
{
    /// <summary>
    /// One reusable, mobile-light collect effect: a single particle burst (count and speed scale with the profile
    /// effect intensity) plus an optional short glow light (profile glow). No third-party assets; the default
    /// particles are placeholders. A profile's own particle prefab replaces them via <see cref="RewardFeedbackPresenter"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CollectBurstEffect : MonoBehaviour
    {
        [SerializeField] private Material particleMaterial;

        [Header("Ranges (scaled by profile effect intensity 0..1)")]
        [SerializeField, Min(1)] private int minParticles = 10;
        [SerializeField, Min(1)] private int maxParticles = 36;
        [SerializeField, Min(0.1f)] private float minSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float maxSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float lifetime = 0.6f;
        [SerializeField, Min(0.01f)] private float particleSize = 0.14f;

        [Header("Glow light (strength from profile glow 0..1)")]
        [SerializeField, Min(0f)] private float maxGlowIntensity = 4f;
        [SerializeField, Min(0.05f)] private float glowDuration = 0.3f;
        [SerializeField, Min(0.5f)] private float glowRange = 4f;

        private ParticleSystem particles;
        private Light glowLight;
        private float glowTimer;
        private float glowStrength;

        public int LastEmitCount { get; private set; }
        public float LastGlow { get; private set; }
        public float LastIntensity { get; private set; }
        public int PlayCount { get; private set; }
        public bool GlowActive => glowLight != null && glowLight.enabled;
        public float GlowLightIntensity => glowLight != null && glowLight.enabled ? glowLight.intensity : 0f;
        public int MaxParticles => maxParticles;

        /// <summary>The (cheap, shared) particle material, also used by the Epic treasure sparkles.</summary>
        public Material ParticleMaterial => particleMaterial;

        public int ParticleCountFor(float intensity)
        {
            return Mathf.RoundToInt(Mathf.Lerp(minParticles, maxParticles, Mathf.Clamp01(intensity)));
        }

        private void Awake()
        {
            BuildParticles();
            BuildGlow();
        }

        /// <param name="emitDefaultParticles">false when a profile supplies its own particle prefab.</param>
        public void Play(Vector3 position, Color color, float intensity, float glow, bool emitDefaultParticles = true)
        {
            PlayCount++;
            LastIntensity = intensity;
            LastGlow = glow;
            LastEmitCount = 0;

            if (emitDefaultParticles && particles != null)
            {
                int count = ParticleCountFor(intensity);
                LastEmitCount = count;

                ParticleSystem.MainModule main = particles.main;
                main.startSpeed = Mathf.Lerp(minSpeed, maxSpeed, Mathf.Clamp01(intensity));
                particles.transform.position = position;

                var emit = new ParticleSystem.EmitParams
                {
                    startColor = color,
                    applyShapeToPosition = true
                };
                particles.Emit(emit, count);
            }

            if (glow > 0f && glowLight != null)
            {
                glowStrength = Mathf.Clamp01(glow);
                glowTimer = glowDuration;
                glowLight.transform.position = position + Vector3.up * 0.6f;
                glowLight.color = color;
                glowLight.intensity = maxGlowIntensity * glowStrength;
                glowLight.enabled = true;
            }
        }

        private void Update()
        {
            if (glowTimer <= 0f || glowLight == null)
            {
                return;
            }

            glowTimer -= Time.deltaTime;
            if (glowTimer <= 0f)
            {
                glowLight.enabled = false;
                return;
            }

            glowLight.intensity = maxGlowIntensity * glowStrength * (glowTimer / glowDuration);
        }

        private void BuildParticles()
        {
            var go = new GameObject("BurstParticles");
            go.transform.SetParent(transform, false);
            particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = lifetime;
            main.startSize = particleSize;
            main.gravityModifier = 0.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles * 2; // headroom for two quick collects; still tiny for mobile

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false; // bursts are emitted from code via Emit()

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            // Shrink to nothing instead of alpha-fading: works with a cheap opaque material.
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var rendererComponent = go.GetComponent<ParticleSystemRenderer>();
            rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
            if (particleMaterial != null)
            {
                rendererComponent.sharedMaterial = particleMaterial;
            }
        }

        private void BuildGlow()
        {
            var go = new GameObject("GlowLight");
            go.transform.SetParent(transform, false);
            glowLight = go.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.range = glowRange;
            glowLight.shadows = LightShadows.None;
            glowLight.enabled = false;
        }
    }
}
