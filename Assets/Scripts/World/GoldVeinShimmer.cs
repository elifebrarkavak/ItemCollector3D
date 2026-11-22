using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// A quiet shimmer on the gold veins of the Hidden Ravine: the veins' glow breathes by a few per cent (a MaterialPropertyBlock on the vein renderer's glow slot, so the shared material stays
    /// untouched and nothing is allocated per frame) and now and then a tiny gold speck twinkles on a vein (one ParticleSystem emitting from the vein mesh itself, at most
    /// <see cref="MaxParticles"/> live specks, the same cheap opaque material as the dust). Both only run while the player is within <see cref="activeRadius"/> metres of the veins.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoldVeinShimmer : MonoBehaviour
    {
        public const int MaxParticles = 10;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private MeshRenderer veins;
        [Tooltip("Index of the glow material in the vein renderer's materials.")]
        [SerializeField, Min(0)] private int glowMaterialIndex = 1;
        [SerializeField] private Mesh veinMesh;
        [SerializeField] private Material specMaterial;
        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField, Min(5f)] private float activeRadius = 14f;

        [Header("Look")]
        [Tooltip("The glow swings between (1 - amount) and (1 + amount) of the material's own glow.")]
        [SerializeField, Range(0f, 0.5f)] private float amount = 0.16f;
        [SerializeField, Min(0.5f)] private float period = 3.4f;
        [SerializeField] private Color specColor = new(1f, 0.86f, 0.45f);
        [SerializeField, Min(0f)] private float specsPerSecond = 1.6f;

        private MaterialPropertyBlock block;
        private Color baseEmission;
        private ParticleSystem particles;
        private Transform target;
        private bool active;
        private float nextCheck;

        public ParticleSystem Particles => particles;

        /// <summary>The multiplier last applied to the veins' glow (1 = the material's own).</summary>
        public float CurrentFactor { get; private set; } = 1f;

        public bool IsActive => active;

        private void Awake()
        {
            if (cameraController == null)
            {
                cameraController = FindAnyObjectByType<ThirdPersonCameraController>();
            }

            block = new MaterialPropertyBlock();
            if (veins != null)
            {
                Material[] mats = veins.sharedMaterials;
                if (glowMaterialIndex < mats.Length && mats[glowMaterialIndex] != null && mats[glowMaterialIndex].HasProperty(EmissionColor))
                {
                    baseEmission = mats[glowMaterialIndex].GetColor(EmissionColor);
                }
                else
                {
                    baseEmission = Color.white;
                }
            }

            BuildSpecs();
        }

        private void BuildSpecs()
        {
            if (veinMesh == null)
            {
                return;
            }

            var go = new GameObject("VeinSpecs");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(veins != null ? veins.transform.position : Vector3.zero, veins != null ? veins.transform.rotation : Quaternion.identity);
            particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = specColor;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxParticles;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Mesh;
            shape.mesh = veinMesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.normalOffset = 0.06f; // just off the rock

            // twinkle: grow quickly, shrink slowly
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.18f, 1f), new Keyframe(0.5f, 0.35f), new Keyframe(0.75f, 0.7f), new Keyframe(1f, 0f)));

            var rendererComponent = go.GetComponent<ParticleSystemRenderer>();
            rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
            rendererComponent.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rendererComponent.receiveShadows = false;
            if (specMaterial != null)
            {
                rendererComponent.sharedMaterial = specMaterial;
            }
        }

        private void OnDisable()
        {
            SetActive(false);
            if (veins != null)
            {
                veins.SetPropertyBlock(null, glowMaterialIndex);
            }
        }

        private void Update()
        {
            if (veins == null || GamePause.IsPaused)
            {
                return;
            }

            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.25f;
                if (target == null && cameraController != null)
                {
                    target = cameraController.Target;
                }

                bool near = target != null && veins.bounds.SqrDistance(target.position) <= activeRadius * activeRadius;
                SetActive(near);
            }

            if (!active)
            {
                return;
            }

            float t = Time.time;
            CurrentFactor = 1f + amount * (0.7f * Mathf.Sin(t * Mathf.PI * 2f / period) + 0.3f * Mathf.Sin(t * Mathf.PI * 2f / (period * 0.37f) + 1.4f));
            veins.GetPropertyBlock(block, glowMaterialIndex);
            block.SetColor(EmissionColor, baseEmission * CurrentFactor);
            veins.SetPropertyBlock(block, glowMaterialIndex);
        }

        private void SetActive(bool value)
        {
            if (active == value)
            {
                return;
            }

            active = value;
            if (particles != null)
            {
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = value ? specsPerSecond : 0f;
                if (value && !particles.isPlaying)
                {
                    particles.Play();
                }
            }

            if (!value && veins != null)
            {
                veins.SetPropertyBlock(null, glowMaterialIndex);
                CurrentFactor = 1f;
            }
        }
    }
}
