#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    public enum RcZone
    {
        None,
        Outpost,   // A  Desert Outpost: the start
        Riverbed,  // B  Dry Riverbed: the wide search area
        Railway,   // C  Old Railway: a raised corridor
        Overlook,  // D  Canyon Overlook: the high viewpoint
        Ravine,    // E  Hidden Ravine: a narrow side passage
    }

    /// <summary>
    /// The Rocky Canyon map as DATA: a handful of walkable "floors" (discs and polylines with a radius and a floor height along them) carved out of one high rock
    /// mass. The terrain height is the blended floor height plus a steep wall that rises with the distance from the nearest floor (capped per area), so every
    /// walkable place, its slope and its connections are decided here and nothing else. The builder turns this into one flat-shaded mesh; the tests walk it.
    ///
    /// Units are metres, x east, z north, y up. The playable ground is about 96 x 75 m (x -47..49, z -35..40) inside a mesh of 112 x 92 m; floors run from 0 m
    /// (dry riverbed) to 11 m (overlook), the rock walls reach about 17 m above the riverbed.
    /// </summary>
    public static class RcLayout
    {
        public const float HalfX = 56f;
        public const float HalfZ = 46f;
        public const float Cell = 2f;
        public const int CellsX = 56;   // 2 * HalfX / Cell
        public const int CellsZ = 46;   // 2 * HalfZ / Cell

        private const float Tau = 1.2f;             // how far (m) a floor's height reaches into the walls around it (small: neighbouring floors must not pull each other up or down)
        private const float WallSlope = 2.2f;       // rise per metre of wall: about 65 degrees, never climbable (the CharacterController limit is 45)

        public sealed class Shape
        {
            public string Name;
            public RcZone Zone;
            public Vector3[] Points;   // x, FLOOR HEIGHT, z (a single point is a disc)
            public float Radius;
            public float Cap;          // how high the walls around this floor rise above it

            public Shape(string name, RcZone zone, float radius, float cap, params Vector3[] points)
            {
                Name = name;
                Zone = zone;
                Radius = radius;
                Cap = cap;
                Points = points;
            }
        }

        public static readonly Vector3 Spawn = new(-37f, 2f, -22f);
        public static readonly Vector2 OverlookCentre = new(43f, 36f);

        public static readonly Shape[] Shapes =
        {
            // A  Desert Outpost: a wide, flat, safe start
            new("Outpost", RcZone.Outpost, 12.5f, 12f, new Vector3(-34f, 2.0f, -22f)),

            // B  Dry Riverbed: a wide winding bed and a search basin in the middle
            new("Riverbed", RcZone.Riverbed, 7f, 12f,
                new Vector3(-27f, 2.0f, -22f), new Vector3(-14f, 1.0f, -17f), new Vector3(-2f, 0.0f, -20f), new Vector3(10f, 0.0f, -15f),
                new Vector3(22f, 0.3f, -8f), new Vector3(31f, 1.0f, 0f), new Vector3(38f, 2.0f, 8f)),
            new("Basin", RcZone.Riverbed, 12f, 12f, new Vector3(6f, 0.0f, -14f)),

            // junction where the riverbed, the railway and the overlook path meet
            new("Plaza", RcZone.Riverbed, 6f, 12f, new Vector3(40f, 2.0f, 12f)),

            // C  Old Railway: a raised corridor (floor 5 m). Stage 15B cuts it in two at the bridge site (the 15A shallow dip becomes a real gap about 9 m wide, the
            // two halves end in round abutments) and carves the gorge below the gap; the wooden bridge deck is the only way across. Everything else is unchanged.
            new("RailwayWest", RcZone.Railway, 4.5f, 12f,
                new Vector3(-32f, 2.0f, -9f), new Vector3(-32f, 3.0f, 3f), new Vector3(-28f, 4.8f, 12f), new Vector3(-18f, 5.0f, 16f),
                new Vector3(-6f, 5.0f, 17f)),
            new("RailwayEast", RcZone.Railway, 4.5f, 12f,
                new Vector3(12f, 5.0f, 19f), new Vector3(24f, 5.0f, 20f), new Vector3(33f, 3.6f, 17f), new Vector3(40f, 2.0f, 12f)),

            // the gorge under the bridge: a dry side channel of the riverbed (floor 0 at the basin, 0.8 m at its dead end behind the bridge), 6.4 m wide
            new("Gorge", RcZone.Riverbed, 3.2f, 12f,
                new Vector3(6f, 0.0f, -6f), new Vector3(4.5f, 0.1f, 3f), new Vector3(3.5f, 0.3f, 10f), new Vector3(3f, 0.5f, 17.5f), new Vector3(3f, 0.8f, 23f)),

            // D  Canyon Overlook: a winding path (never steeper than about 17 degrees) up to a plateau with a low parapet
            new("OverlookPath", RcZone.Overlook, 3.2f, 12f,
                new Vector3(40f, 2.0f, 12f), new Vector3(47f, 3.2f, 17f), new Vector3(47f, 4.6f, 24f), new Vector3(41f, 5.8f, 26f), new Vector3(34f, 6.8f, 26f),
                new Vector3(31f, 7.6f, 29f), new Vector3(31f, 8.9f, 34f), new Vector3(35f, 10.1f, 38f), new Vector3(40f, 11.0f, 38f)),
            new("OverlookPlateau", RcZone.Overlook, 5f, 1.7f, new Vector3(43f, 11.0f, 36f)),

            // E  Hidden Ravine: a side passage (8 m wide) off the basin that ends in a small pocket, on purpose
            new("Ravine", RcZone.Ravine, 4f, 12f,
                new Vector3(-4f, 0.0f, -13f), new Vector3(-5f, 1.0f, -5f), new Vector3(-9f, 1.6f, -1f), new Vector3(-13f, 2.2f, 1f)),
            new("RavinePocket", RcZone.Ravine, 4.5f, 9f, new Vector3(-17f, 2.4f, 2f)),
        };

        /// <summary>
        /// Stage 15B "view windows": (x, z, radius, depth) places where the ROCK MASS (never a floor) is lowered a little, so the bridge's two tall portal towers can be seen from the camp's
        /// first view. They sit on the rock between the outpost, the ravine and the railway; no walkable floor or slope changes.
        /// </summary>
        public static readonly Vector4[] Cuts =
        {
            new(-23f, -5.2f, 6.5f, 3.2f),
            new(-7f, 9f, 7.5f, 3.4f),
        };

        // ------------------------------------------------------------------ height field

        /// <summary>Distance from (x, z) to a shape's centre line (or centre) and the floor height at the nearest point of it.</summary>
        private static float Distance(Shape s, float x, float z, out float floor)
        {
            Vector3[] p = s.Points;
            if (p.Length == 1)
            {
                floor = p[0].y;
                return Mathf.Sqrt((x - p[0].x) * (x - p[0].x) + (z - p[0].z) * (z - p[0].z));
            }

            float best = float.MaxValue;
            floor = p[0].y;
            for (int i = 0; i < p.Length - 1; i++)
            {
                float ax = p[i].x, az = p[i].z, bx = p[i + 1].x, bz = p[i + 1].z;
                float dx = bx - ax, dz = bz - az;
                float t = Mathf.Clamp01(((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz));
                float cx = ax + dx * t - x, cz = az + dz * t - z;
                float d = Mathf.Sqrt(cx * cx + cz * cz);
                if (d < best)
                {
                    best = d;
                    floor = Mathf.Lerp(p[i].y, p[i + 1].y, t);
                }
            }

            return best;
        }

        private static float Wall(float edge)
        {
            if (edge <= 0f)
            {
                return 0f;
            }

            return edge < 1f ? 1.1f * edge * edge : WallSlope * (edge - 0.5f);
        }

        private static float Noise(float x, float z)
        {
            // smooth value noise from a hash: cheap, deterministic, no allocation
            float n = 0f, amp = 1f, freq = 1f / 9f;
            for (int o = 0; o < 3; o++)
            {
                float fx = x * freq, fz = z * freq;
                int ix = Mathf.FloorToInt(fx), iz = Mathf.FloorToInt(fz);
                float tx = fx - ix, tz = fz - iz;
                tx = tx * tx * (3f - 2f * tx);
                tz = tz * tz * (3f - 2f * tz);
                float a = Hash(ix, iz), b = Hash(ix + 1, iz), c = Hash(ix, iz + 1), d = Hash(ix + 1, iz + 1);
                n += Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz) * amp;
                amp *= 0.5f;
                freq *= 2.1f;
            }

            return n / 1.75f; // about -0.5 .. +0.5
        }

        private static float Hash(int x, int z)
        {
            unchecked
            {
                int h = x * 374761393 + z * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return ((h & 0xFFFF) / 65535f) - 0.5f;
            }
        }

        /// <summary>The terrain height at (x, z) and the zone of the nearest floor.</summary>
        public static float Height(float x, float z, out RcZone zone, out float floorHeight)
        {
            float edgeMin = float.MaxValue;
            RcZone nearest = RcZone.None;
            Shape nearestShape = null;
            // first pass: nearest edge (for the stable weights), second pass: the weighted floor and cap
            int count = Shapes.Length;
            float[] edge = new float[count];
            float[] floor = new float[count];
            for (int i = 0; i < count; i++)
            {
                float d = Distance(Shapes[i], x, z, out floor[i]);
                edge[i] = d - Shapes[i].Radius;
                if (edge[i] < edgeMin)
                {
                    edgeMin = edge[i];
                    nearest = Shapes[i].Zone;
                    nearestShape = Shapes[i];
                }
            }

            float wSum = 0f, fSum = 0f, cSum = 0f;
            float baseEdge = Mathf.Max(0f, edgeMin);
            for (int i = 0; i < count; i++)
            {
                float over = (Mathf.Max(0f, edge[i]) - baseEdge) / Tau;
                float w = Mathf.Exp(-over * over); // gaussian: a floor's height is felt only about 2 m beyond its own edge
                wSum += w;
                fSum += w * floor[i];
                cSum += w * Shapes[i].Cap;
            }

            float f = fSum / wSum, cap = cSum / wSum;
            float wall = Mathf.Min(Wall(edgeMin), cap);
            float rough = Mathf.Lerp(0.10f, 1.3f, Mathf.Clamp01(wall / 3f)); // floors stay smooth, rock faces are irregular
            float h = f + wall + Noise(x, z) * rough * (wall > 0f ? 1f : 0.35f);
            foreach (Vector4 c in Cuts)
            {
                float d = Mathf.Sqrt((x - c.x) * (x - c.x) + (z - c.y) * (z - c.y));
                if (d < c.z)
                {
                    float t = 1f - d / c.z;
                    h -= c.w * t * t * (3f - 2f * t) * Mathf.Clamp01((wall - 0.8f) / 1.6f);
                }
            }

            zone = edgeMin <= 0f ? nearest : RcZone.None;
            floorHeight = f;
            return Mathf.Max(h, f - 0.05f);
        }

        public static float Height(float x, float z) => Height(x, z, out _, out _);

        // ------------------------------------------------------------------ the mesh grid (what the game really stands on)

        private static float[,] grid;

        public static void ResetGrid()
        {
            grid = null;
        }

        public static float[,] Grid
        {
            get
            {
                if (grid == null)
                {
                    grid = new float[CellsX + 1, CellsZ + 1];
                    for (int i = 0; i <= CellsX; i++)
                    {
                        for (int j = 0; j <= CellsZ; j++)
                        {
                            grid[i, j] = Height(-HalfX + i * Cell, -HalfZ + j * Cell);
                        }
                    }
                }

                return grid;
            }
        }

        /// <summary>Height of the TERRAIN MESH at (x, z), interpolated exactly like its triangles, so props and colliders sit on the real surface.</summary>
        public static float Surface(float x, float z)
        {
            float[,] g = Grid;
            float fx = Mathf.Clamp((x + HalfX) / Cell, 0f, CellsX - 0.001f);
            float fz = Mathf.Clamp((z + HalfZ) / Cell, 0f, CellsZ - 0.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = g[i, j], b = g[i + 1, j], c = g[i, j + 1], d = g[i + 1, j + 1];
            return tx + tz <= 1f ? a + (b - a) * tx + (c - a) * tz : d + (c - d) * (1f - tx) + (b - d) * (1f - tz);
        }

        /// <summary>Rise over run of the terrain mesh around (x, z).</summary>
        public static float Slope(float x, float z)
        {
            float dx = Surface(x + 0.5f, z) - Surface(x - 0.5f, z);
            float dz = Surface(x, z + 0.5f) - Surface(x, z - 0.5f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The zone of the nearest floor, also for points inside the rock walls (used to tint walls by area).</summary>
        public static RcZone NearestZone(float x, float z)
        {
            float best = float.MaxValue;
            RcZone zone = RcZone.None;
            foreach (Shape s in Shapes)
            {
                float d = Distance(s, x, z, out _) - s.Radius;
                if (d < best)
                {
                    best = d;
                    zone = s.Zone;
                }
            }

            return zone;
        }

        public static RcZone ZoneAt(float x, float z)
        {
            Height(x, z, out RcZone zone, out _);
            return zone;
        }

        /// <summary>Distance from (x, z) to the nearest floor's edge (negative inside a floor).</summary>
        public static float EdgeDistance(float x, float z)
        {
            float best = float.MaxValue;
            foreach (Shape s in Shapes)
            {
                best = Mathf.Min(best, Distance(s, x, z, out _) - s.Radius);
            }

            return best;
        }

        // ------------------------------------------------------------------ reachability (the same limits the CharacterController has, with a margin)

        public const float WalkableSlope = 0.60f; // about 31 degrees, inside the controller's 45

        /// <summary>Flood fill over a 1 m grid from <paramref name="start"/>: a step is allowed when the terrain between two neighbouring samples is gentle enough.</summary>
        public static bool[,] Reachable(Vector2 start)
        {
            int nx = (int)(2 * HalfX), nz = (int)(2 * HalfZ);
            var seen = new bool[nx + 1, nz + 1];
            var queue = new Queue<Vector2Int>();
            var s0 = new Vector2Int(Mathf.RoundToInt(start.x + HalfX), Mathf.RoundToInt(start.y + HalfZ));
            seen[s0.x, s0.y] = true;
            queue.Enqueue(s0);
            Vector2Int[] dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                float h = Surface(c.x - HalfX, c.y - HalfZ);
                foreach (Vector2Int d in dirs)
                {
                    int x = c.x + d.x, z = c.y + d.y;
                    if (x < 0 || z < 0 || x > nx || z > nz || seen[x, z])
                    {
                        continue;
                    }

                    float h2 = Surface(x - HalfX, z - HalfZ);
                    if (Mathf.Abs(h2 - h) <= WalkableSlope && EdgeDistance(x - HalfX, z - HalfZ) <= -0.4f)
                    {
                        seen[x, z] = true;
                        queue.Enqueue(new Vector2Int(x, z));
                    }
                }
            }

            return seen;
        }
    }
}
#endif
