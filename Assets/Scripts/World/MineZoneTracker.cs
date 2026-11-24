using System;
using ItemCollector3D.Player;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>The five exploration areas of the Ancient Mine, in the order of the scene's zone roots (A..E).</summary>
    public enum MineZone
    {
        Gate = 0,
        Hall = 1,
        Caverns = 2,
        Workshop = 3,
        Vault = 4,
    }

    /// <summary>
    /// Tells the Ancient Mine's atmosphere (ambience, crystal glow, particles, the area-name banner) which area the player is in. It reads the same thing <see cref="CaveZoneMood"/> reads: the
    /// FLOOR the player stands on, whose zone root ("A_AncientGate" .. "E_RoyalVault") names the area, so no map data is duplicated. One short raycast every <see cref="RefreshSeconds"/>.
    /// A change of area only counts after the new floor has been under the player for <see cref="SwitchSeconds"/> (a quick step across a border, or walking back and forth on it, changes nothing),
    /// and then <see cref="ZoneEntered"/> is raised once. The weights are one-hot: the audio smooths them (2 s) so every change is a cross-fade. Owns no gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MineZoneTracker : MonoBehaviour
    {
        public const int ZoneCount = 5;
        public const float RefreshSeconds = 0.125f;
        public const float SwitchSeconds = 0.4f;

        /// <summary>What the entry banner says for each area.</summary>
        public static readonly string[] DisplayNames = { "ANCIENT GATE", "HALL OF PILLARS", "CRYSTAL CAVERNS", "FORGOTTEN WORKSHOP", "ROYAL VAULT" };

        private static readonly string[] RootNames = { "A_AncientGate", "B_HallOfPillars", "C_CrystalCaverns", "D_ForgottenWorkshop", "E_RoyalVault" };
        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        [SerializeField] private ThirdPersonCameraController cameraController;

        private readonly int[] visits = new int[ZoneCount];
        private Transform target;
        private float nextRefresh;
        private bool hasZone;
        private MineZone current;
        private bool hasCandidate;
        private MineZone candidate;
        private float candidateSince;

        public MineZone Current => current;

        public bool HasZone => hasZone;

        /// <summary>How many times <see cref="Current"/> was set or changed, the first fix included.</summary>
        public int EntryCount { get; private set; }

        /// <summary>Raised when the player has clearly entered an area (and once for the starting area).</summary>
        public event Action<MineZone> ZoneEntered;

        public static string DisplayName(MineZone zone) => DisplayNames[(int)zone];

        /// <summary>How many times the player has entered an area (the first fix counts): the Royal Vault uses "1" for its first-entry moment.</summary>
        public int Visits(MineZone zone) => visits[(int)zone];

        /// <summary>1 for the current area, 0 for the others (the consumers smooth it).</summary>
        public float Weight(MineZone zone) => hasZone && zone == current ? 1f : 0f;

        private void Awake()
        {
            if (cameraController == null)
            {
                cameraController = FindAnyObjectByType<ThirdPersonCameraController>();
            }
        }

        private void OnEnable()
        {
            nextRefresh = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.unscaledTime + RefreshSeconds;
            Refresh(false);
        }

        /// <summary>Re-measures now. <paramref name="immediate"/> skips the settling time (tests after a teleport).</summary>
        public void Refresh(bool immediate)
        {
            if (target == null && cameraController != null)
            {
                target = cameraController.Target;
            }

            if (target == null || !TryZoneAt(target.position, out MineZone found))
            {
                return; // no floor under the player (a fall, a teleport in progress): keep the last answer
            }

            if (!hasZone)
            {
                Enter(found);
                return;
            }

            if (found == current)
            {
                hasCandidate = false;
                return;
            }

            if (!hasCandidate || candidate != found)
            {
                hasCandidate = true;
                candidate = found;
                candidateSince = Time.unscaledTime;
            }

            if (immediate || Time.unscaledTime - candidateSince >= SwitchSeconds)
            {
                hasCandidate = false;
                Enter(found);
            }
        }

        /// <summary>The area of the floor under a point (nearest floor within 8 m below).</summary>
        public static bool TryZoneAt(Vector3 position, out MineZone zone)
        {
            int count = Physics.RaycastNonAlloc(position + Vector3.up * 0.3f, Vector3.down, Hits, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            string root = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider.name == "Floor" && hit.collider.transform.parent != null && hit.distance < best)
                {
                    best = hit.distance;
                    root = hit.collider.transform.parent.name;
                }
            }

            for (int z = 0; z < ZoneCount; z++)
            {
                if (root == RootNames[z])
                {
                    zone = (MineZone)z;
                    return true;
                }
            }

            zone = MineZone.Gate;
            return false;
        }

        private void Enter(MineZone zone)
        {
            hasZone = true;
            current = zone;
            EntryCount++;
            visits[(int)zone]++;
            ZoneEntered?.Invoke(zone);
        }
    }
}
