using System;
using ItemCollector3D.Player;
using UnityEngine;

namespace ItemCollector3D.World
{
    /// <summary>The five exploration areas of Rocky Canyon, in the order of the scene's zone roots (A..E).</summary>
    public enum CanyonZone
    {
        Outpost = 0,
        Riverbed = 1,
        Railway = 2,
        Overlook = 3,
        Ravine = 4,
    }

    /// <summary>
    /// Tells the rest of Rocky Canyon's atmosphere which area the player is in. The map is a set of walkable "floors" (discs and polylines with a radius and a floor height, the same data the
    /// terrain was carved from; the editor tool copies them into <see cref="pieces"/>). For the player's position it measures how far inside / outside each area's floors it is, and returns
    /// (a) soft WEIGHTS per area (1 deep inside, cross-fading over <see cref="blendWidth"/> metres towards a neighbouring floor, so sounds and moods change gradually) and (b) one discrete
    /// <see cref="Current"/> area with hysteresis (it only changes when another area is clearly nearer), raising <see cref="ZoneEntered"/> when it does.
    /// The player's height matters: a floor 3 m or more above or below the player's feet is ignored, so the bridge deck (floor 5 m) and the gorge under it (floor 0.5 m) are different areas.
    /// It only reads the player's position (about 8 times a second) and owns no gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CanyonZoneTracker : MonoBehaviour
    {
        [Serializable]
        public sealed class Piece
        {
            public CanyonZone zone;
            [Tooltip("x, FLOOR HEIGHT, z of a disc's centre or of a polyline's points.")]
            public Vector3[] points = Array.Empty<Vector3>();
            public float radius = 4f;
        }

        public const int ZoneCount = 5;
        public const float RefreshSeconds = 0.125f;

        /// <summary>What the entry notification says for each area.</summary>
        public static readonly string[] DisplayNames = { "DESERT OUTPOST", "DRY RIVERBED", "OLD RAILWAY", "CANYON OVERLOOK", "HIDDEN RAVINE" };

        [SerializeField] private ThirdPersonCameraController cameraController;
        [SerializeField] private Piece[] pieces = Array.Empty<Piece>();
        [Tooltip("Metres over which the weight of one area fades into the next one.")]
        [SerializeField, Min(0.5f)] private float blendWidth = 5f;
        [Tooltip("A floor this far above or below the player's feet does not count (bridge deck versus gorge).")]
        [SerializeField, Min(0.5f)] private float verticalTolerance = 3f;
        [Tooltip("Another area must be this many metres nearer than the current one before the current area changes.")]
        [SerializeField, Min(0f)] private float switchMargin = 1.5f;

        private readonly float[] weights = new float[ZoneCount];
        private readonly float[] edges = new float[ZoneCount];
        private CharacterController body;
        private Transform watched;
        private float nextRefresh;
        private bool hasZone;
        private CanyonZone current;

        /// <summary>The area the player is in (valid when <see cref="HasZone"/>).</summary>
        public CanyonZone Current => current;

        public bool HasZone => hasZone;

        /// <summary>How many times <see cref="Current"/> was set or changed, the first fix included.</summary>
        public int EntryCount { get; private set; }

        /// <summary>Raised when the player has clearly entered an area (and once for the starting area).</summary>
        public event Action<CanyonZone> ZoneEntered;

        public int PieceCount => pieces != null ? pieces.Length : 0;

        /// <summary>The soft weight (0 .. 1, they add up to 1) of an area for the player's position.</summary>
        public float Weight(CanyonZone zone) => weights[(int)zone];

        public static string DisplayName(CanyonZone zone) => DisplayNames[(int)zone];

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
            Refresh();
        }

        /// <summary>Re-measures now (also used by tests after a teleport).</summary>
        public void Refresh()
        {
            Transform target = cameraController != null ? cameraController.Target : null;
            if (target == null || pieces == null || pieces.Length == 0)
            {
                return;
            }

            if (watched != target)
            {
                watched = target;
                body = target.GetComponent<CharacterController>();
            }

            Vector3 p = target.position;
            float feet = body != null ? body.bounds.min.y : p.y - 1f;
            for (int z = 0; z < ZoneCount; z++)
            {
                edges[z] = float.MaxValue;
            }

            foreach (Piece piece in pieces)
            {
                float edge = EdgeOf(piece, p.x, p.z, feet);
                int z = (int)piece.zone;
                if (edge < edges[z])
                {
                    edges[z] = edge;
                }
            }

            int best = -1;
            float bestEdge = float.MaxValue;
            for (int z = 0; z < ZoneCount; z++)
            {
                if (edges[z] < bestEdge)
                {
                    bestEdge = edges[z];
                    best = z;
                }
            }

            if (best < 0)
            {
                return; // no floor under the player (falling, a test teleport): keep the last answer
            }

            float sum = 0f;
            for (int z = 0; z < ZoneCount; z++)
            {
                float w = edges[z] == float.MaxValue ? 0f : Mathf.Clamp01(1f - (edges[z] - bestEdge) / blendWidth);
                weights[z] = w;
                sum += w;
            }

            for (int z = 0; z < ZoneCount; z++)
            {
                weights[z] /= sum;
            }

            if (!hasZone)
            {
                Enter((CanyonZone)best);
            }
            else if ((int)current != best && (edges[(int)current] - bestEdge > switchMargin || edges[(int)current] > 1f))
            {
                Enter((CanyonZone)best);
            }
        }

        private void Enter(CanyonZone zone)
        {
            hasZone = true;
            current = zone;
            EntryCount++;
            ZoneEntered?.Invoke(zone);
        }

        /// <summary>Distance from (x, z) to the edge of a piece: negative inside, MaxValue when the piece's floor is not near the player's height.</summary>
        private float EdgeOf(Piece piece, float x, float z, float feet)
        {
            Vector3[] pts = piece.points;
            if (pts == null || pts.Length == 0)
            {
                return float.MaxValue;
            }

            float best = float.MaxValue, floor = pts[0].y;
            if (pts.Length == 1)
            {
                best = Mathf.Sqrt((x - pts[0].x) * (x - pts[0].x) + (z - pts[0].z) * (z - pts[0].z));
            }
            else
            {
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    float ax = pts[i].x, az = pts[i].z, dx = pts[i + 1].x - ax, dz = pts[i + 1].z - az;
                    float t = Mathf.Clamp01(((x - ax) * dx + (z - az) * dz) / Mathf.Max(1e-4f, dx * dx + dz * dz));
                    float cx = ax + dx * t - x, cz = az + dz * t - z;
                    float d = Mathf.Sqrt(cx * cx + cz * cz);
                    if (d < best)
                    {
                        best = d;
                        floor = Mathf.Lerp(pts[i].y, pts[i + 1].y, t);
                    }
                }
            }

            return Mathf.Abs(feet - floor) > verticalTolerance ? float.MaxValue : best - piece.radius;
        }
    }
}
