using System;
using ItemCollector3D.Player;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>
    /// Ancient Mine lighting character per area (stage 16B), with no extra lights: while the player walks through the five areas the scene's AMBIENT light and FOG colour
    /// ease towards that area's mood (neutral grand stone in the Hall, blue and purple in the Crystal Caverns, warm in the Workshop, purple / turquoise in the Royal Vault, a bright
    /// cool welcome in the Ancient Gate). The area is read from the floor the player stands on: every area's floor mesh sits under its own zone root, so the root's name is the zone
    /// (no zone data is duplicated here). One short raycast every <see cref="probeInterval"/> seconds; RenderSettings are written only while a blend is running; they are restored on disable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaveZoneMood : MonoBehaviour
    {
        [Serializable]
        public struct Mood
        {
            [Tooltip("Name of the zone root that owns the floor (e.g. B_HallOfPillars).")]
            public string zone;
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
            public Color fogColor;
            [Min(0f)] public float fogDensity;
        }

        [Tooltip("The player (its position is probed). Empty: the target of the scene's third-person camera.")]
        [SerializeField] private Transform target;
        [SerializeField] private Mood[] moods = Array.Empty<Mood>();
        [Tooltip("Roughly how long a change of mood takes.")]
        [SerializeField, Min(0.1f)] private float blendSeconds = 1.8f;
        [SerializeField, Min(0.05f)] private float probeInterval = 0.2f;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private int current = -1;
        private float probeTimer;
        private bool captured;
        private Color sky, equator, ground, fog;
        private float density;
        private Color startSky, startEquator, startGround, startFog;
        private float startDensity;
        private bool startFogOn;

        /// <summary>The zone root name the player is standing in ("" before the first probe or while no floor is found).</summary>
        public string CurrentZone => current >= 0 && current < moods.Length ? moods[current].zone : string.Empty;

        public int MoodCount => moods.Length;

        public bool TryGetMood(string zone, out Mood mood)
        {
            foreach (Mood m in moods)
            {
                if (m.zone == zone)
                {
                    mood = m;
                    return true;
                }
            }

            mood = default;
            return false;
        }

        /// <summary>The ambient sky colour currently written to the scene (for tests and tools).</summary>
        public Color DisplayedSky => sky;

        private void OnEnable()
        {
            startSky = sky = RenderSettings.ambientSkyColor;
            startEquator = equator = RenderSettings.ambientEquatorColor;
            startGround = ground = RenderSettings.ambientGroundColor;
            startFog = fog = RenderSettings.fogColor;
            startDensity = density = RenderSettings.fogDensity;
            startFogOn = RenderSettings.fog;
            captured = true;
            probeTimer = 0f;
            current = -1;
        }

        private void OnDisable()
        {
            if (!captured)
            {
                return;
            }

            RenderSettings.ambientSkyColor = startSky;
            RenderSettings.ambientEquatorColor = startEquator;
            RenderSettings.ambientGroundColor = startGround;
            RenderSettings.fogColor = startFog;
            RenderSettings.fogDensity = startDensity;
            RenderSettings.fog = startFogOn;
            captured = false;
        }

        private void Update()
        {
            if (moods.Length == 0)
            {
                return;
            }

            probeTimer -= Time.unscaledDeltaTime;
            if (probeTimer <= 0f)
            {
                probeTimer = probeInterval;
                Probe();
            }

            if (current < 0)
            {
                return;
            }

            Mood goal = moods[current];
            float k = 1f - Mathf.Exp(-Time.deltaTime * 3f / blendSeconds);
            if (k <= 0f)
            {
                return; // paused: nothing moves
            }

            bool changed = false;
            changed |= Ease(ref sky, goal.ambientSky, k);
            changed |= Ease(ref equator, goal.ambientEquator, k);
            changed |= Ease(ref ground, goal.ambientGround, k);
            changed |= Ease(ref fog, goal.fogColor, k);
            float newDensity = Mathf.Lerp(density, goal.fogDensity, k);
            if (Mathf.Abs(newDensity - density) > 0.00002f)
            {
                density = newDensity;
                changed = true;
            }

            if (changed)
            {
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                RenderSettings.fogColor = fog;
                RenderSettings.fogDensity = density;
            }
        }

        private static bool Ease(ref Color value, Color goal, float k)
        {
            Color next = Color.Lerp(value, goal, k);
            if (Mathf.Abs(next.r - value.r) + Mathf.Abs(next.g - value.g) + Mathf.Abs(next.b - value.b) < 0.0004f)
            {
                return false;
            }

            value = next;
            return true;
        }

        private void Probe()
        {
            if (target == null)
            {
                var cameraController = FindAnyObjectByType<ThirdPersonCameraController>();
                target = cameraController != null ? cameraController.Target : null;
                if (target == null)
                {
                    return;
                }
            }

            int count = Physics.RaycastNonAlloc(target.position + Vector3.up * 0.3f, Vector3.down, Hits, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            string zone = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider.name == "Floor" && hit.collider.transform.parent != null && hit.distance < best)
                {
                    best = hit.distance;
                    zone = hit.collider.transform.parent.name;
                }
            }

            if (zone == null)
            {
                return; // no floor under the player (a fall, a teleport in progress): keep the current mood
            }

            for (int i = 0; i < moods.Length; i++)
            {
                if (moods[i].zone == zone)
                {
                    current = i;
                    return;
                }
            }
        }
    }
}
