using System;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// The slow glow of the Ancient Mine's crystals and gold (stage 16D) with ONE manager for the whole map: no per-crystal component, no extra material, no light. Every area's crystal mesh
    /// (and the gold inlay slot of its structures mesh) keeps the shared AM_Glow material; this component only writes a MaterialPropertyBlock (the emission colour) to a handful of renderers,
    /// about 15 times a second, and only for renderers within <see cref="activeDistance"/> metres of the player.
    ///
    /// Because the palette texture is the emission map, a TINT that cycles the red, green and blue channels on different phases makes the blue, the violet and the turquoise crystals of one
    /// mesh brighten at different moments: they never pulse together. Each entry (an area's mesh) also has its own period and phase, and the Royal Vault and the Crystal Caverns glow strongest.
    /// The first time the player enters the Royal Vault its glow swells for ~8 s. Cycle times are 7 .. 14 s (nothing fast). Restores the plain material on disable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MineCrystalGlow : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public Renderer renderer;
            [Tooltip("Material slot to animate (-1 = every slot of the renderer).")]
            public int materialIndex = -1;
            public MineZone zone;
            [Tooltip("Emission multiplier the glow breathes around (1 = the material as authored).")]
            public float intensity = 1f;
            [Tooltip("How far the multiplier swings up and down.")]
            public float swing = 0.2f;
            [Tooltip("Seconds for one breath. Keep it slow.")]
            public float period = 10f;
            public float phase;
            [Tooltip("How strongly the red / green / blue channels drift apart (0 = a plain brightness change).")]
            public float tint = 0.25f;
            [Tooltip("Gets the Royal Vault's first-entry swell on top.")]
            public bool vaultSwell;
        }

        public const float UpdateInterval = 1f / 15f;
        public const float VaultSwellSeconds = MineAmbience.VaultSwellSeconds;
        public const float VaultSwellBoost = 0.45f;

        [SerializeField] private MineZoneTracker zones;
        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [Tooltip("Renderers further than this from the player are left alone.")]
        [SerializeField, Min(5f)] private float activeDistance = 60f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock block;
        private Transform target;
        private float nextUpdate;
        private float swellStart = float.NegativeInfinity;

        public int EntryCount => entries.Length;
        public Entry GetEntry(int index) => entries[index];

        /// <summary>The colour last written for an entry (white when it was never active).</summary>
        public Color LastColor(int index) => lastColors != null && index < lastColors.Length ? lastColors[index] : Color.white;

        /// <summary>How many renderer updates were made so far (tests: renderers far away must not be touched).</summary>
        public int WriteCount { get; private set; }

        private Color[] lastColors;

        /// <summary>The emission colour of an entry at a time, with the first-entry swell (0 .. 1) applied. Pure, so tests and tools can sample it.</summary>
        public static Color Evaluate(Entry e, float time, float swell)
        {
            float w = Mathf.PI * 2f / Mathf.Max(1f, e.period);
            float breath = 0.65f * Mathf.Sin(w * time + e.phase) + 0.35f * Mathf.Sin(w * 0.61f * time + e.phase * 1.7f + 1f); // -1 .. 1, two incommensurate slow waves
            float k = e.intensity + e.swing * breath;
            if (e.vaultSwell)
            {
                k *= 1f + VaultSwellBoost * swell;
            }

            return new Color(
                k * (1f + e.tint * Mathf.Sin(w * time + e.phase)),
                k * (1f + e.tint * Mathf.Sin(w * time + e.phase + 2.1f)),
                k * (1f + e.tint * 0.5f * Mathf.Sin(w * time + e.phase + 4.2f)),
                1f);
        }

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

            block = new MaterialPropertyBlock();
            lastColors = new Color[entries.Length];
            for (int i = 0; i < lastColors.Length; i++)
            {
                lastColors[i] = Color.white;
            }
        }

        private void OnEnable()
        {
            if (zones != null)
            {
                zones.ZoneEntered += HandleEntered;
            }

            nextUpdate = 0f;
        }

        private void OnDisable()
        {
            if (zones != null)
            {
                zones.ZoneEntered -= HandleEntered;
            }

            foreach (Entry e in entries)
            {
                if (e.renderer != null)
                {
                    if (e.materialIndex < 0)
                    {
                        e.renderer.SetPropertyBlock(null);
                    }
                    else
                    {
                        e.renderer.SetPropertyBlock(null, e.materialIndex);
                    }
                }
            }
        }

        private void HandleEntered(MineZone zone)
        {
            if (zone == MineZone.Vault && zones.Visits(MineZone.Vault) == 1)
            {
                swellStart = Time.time;
            }
        }

        /// <summary>0 .. 1: how much of the vault's first-entry swell is left.</summary>
        public float Swell
        {
            get
            {
                float left = Mathf.Clamp01(1f - (Time.time - swellStart) / VaultSwellSeconds);
                return left * left;
            }
        }

        private void Update()
        {
            if (GamePause.IsPaused || Time.time < nextUpdate)
            {
                return;
            }

            nextUpdate = Time.time + UpdateInterval;
            if (target == null && cameraController != null)
            {
                target = cameraController.Target;
            }

            Vector3 p = target != null ? target.position : Vector3.zero;
            float limit = activeDistance * activeDistance;
            float swell = Swell;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                if (e.renderer == null || (target != null && e.renderer.bounds.SqrDistance(p) > limit))
                {
                    continue;
                }

                Color color = Evaluate(e, Time.time, swell);
                lastColors[i] = color;
                block.Clear();
                block.SetColor(EmissionColor, color);
                if (e.materialIndex < 0)
                {
                    e.renderer.SetPropertyBlock(block);
                }
                else
                {
                    e.renderer.SetPropertyBlock(block, e.materialIndex);
                }

                WriteCount++;
            }
        }
    }
}
