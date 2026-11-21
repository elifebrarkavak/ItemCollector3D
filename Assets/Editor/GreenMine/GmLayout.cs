// Part of the DISABLED Green Mine generator: see the header of GreenMineSceneBuilder.cs.
#if GREEN_MINE_GENERATOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.GreenMine
{
    /// <summary>
    /// The map of Green Mine in one place: key positions, the path network, the river, the terrain height function and the treasure spots.
    /// World units are metres; x = east, z = north, the playable area is about 40 x 40 around the origin (reference art 3: camp bottom-left,
    /// mine top-centre-right, waterfall top-right, rocky treasure area bottom-right, forest top-left).
    /// </summary>
    internal static class GmLayout
    {
        public const float Play = 19.6f;          // inner edge of the invisible walls (the playable area is ~39 m wide)
        public const float TerrainHalf = 36f;     // the terrain mesh reaches this far (rim hills hide its edge)
        public const float PathHalfWidth = 1.7f;  // 3.4 m wide trails (player + third-person camera)

        // ---- key places (x, z)
        public static readonly Vector2 CampCenter = new(-11.6f, -11.4f);
        public static readonly Vector2 TentPos = new(-14.2f, -9.6f);
        public static readonly Vector2 FirePos = new(-10.6f, -12.6f);
        public static readonly Vector2 SpawnPos = new(-8.0f, -10.4f);
        public static readonly Vector2 GatePos = new(-4.4f, -18.2f);
        public static readonly Vector2 MineFront = new(6.3f, 14.5f);   // centre of the entrance frame, on the front plane
        public static readonly Vector2 ApronCenter = new(6.3f, 12.2f);
        public static readonly Vector2 MeadowRocks = new(0.6f, 3.4f);
        public static readonly Vector2 RockyCenter = new(11.0f, -10.0f);
        public static readonly Vector2 ForestCenter = new(-13.5f, 9.5f);
        public static readonly Vector2 BridgePos = new(14.5f, 3.7f);
        public static readonly Vector2 FallPos = new(17.6f, 14.4f);

        // ---- paths (open polylines, trail centre lines)
        public static readonly Vector2[][] Paths =
        {
            // 0: gate -> first junction
            new Vector2[] { new(-4.4f, -19.5f), new(-4.4f, -15.5f), new(-4.7f, -11.0f), new(-5.0f, -7.8f), new(-5.4f, -6.0f) },
            // 1: loop, west side: junction -> forest edge -> meadow north -> mine apron
            new Vector2[]
            {
                new(-5.4f, -6.0f), new(-6.6f, -4.4f), new(-9.0f, -3.0f), new(-11.4f, -1.2f), new(-12.2f, 1.5f), new(-11.0f, 4.2f),
                new(-8.6f, 6.4f), new(-5.3f, 7.7f), new(-1.6f, 8.4f), new(2.2f, 8.4f), new(4.9f, 9.7f), new(5.9f, 11.8f), new(6.3f, 13.2f),
            },
            // 2: loop, east side: mine apron -> east -> back to the junction (closes the loop)
            new Vector2[]
            {
                new(5.9f, 11.8f), new(8.2f, 10.4f), new(9.6f, 7.7f), new(9.2f, 4.2f), new(7.6f, 1.2f), new(5.0f, -1.0f), new(2.0f, -2.6f),
                new(-1.0f, -3.8f), new(-3.6f, -5.0f), new(-5.4f, -6.0f),
            },
            // 3: camp spur
            new Vector2[] { new(-4.7f, -10.4f), new(-7.6f, -10.5f), new(-10.0f, -10.9f), new(-12.4f, -11.2f) },
            // 4: forest trail
            new Vector2[] { new(-11.0f, 4.2f), new(-13.0f, 6.6f), new(-14.6f, 9.0f), new(-15.2f, 11.0f) },
            // 5: rocky trail
            new Vector2[] { new(5.0f, -1.0f), new(7.8f, -3.8f), new(10.2f, -6.4f), new(12.0f, -9.4f), new(12.4f, -12.0f) },
            // 6: bridge trail (to the river and the far bank)
            new Vector2[] { new(9.2f, 4.2f), new(11.4f, 3.9f), new(13.0f, 3.7f), new(16.0f, 3.7f), new(17.8f, 3.9f) },
        };

        // ---- river (centre line + half width of the water)
        public static readonly Vector3[] River =
        {
            new(17.6f, 14.2f, 3.0f), new(17.0f, 11.8f, 3.0f), new(15.8f, 8.4f, 2.0f), new(14.9f, 5.6f, 1.9f), new(14.5f, 3.7f, 1.8f),
            new(14.4f, 1.0f, 1.8f), new(15.2f, -2.6f, 1.7f), new(16.4f, -6.4f, 1.7f), new(17.4f, -10.4f, 1.7f), new(18.0f, -14.6f, 1.8f),
            new(18.6f, -19.0f, 1.9f), new(19.4f, -26.0f, 2.2f), new(20.4f, -36.0f, 2.4f),
        };

        public const float WaterLevel = -0.12f;
        public const float RiverBed = -0.46f;

        // ---- treasure spots: ids are about the PLACE (not the treasure type), so Emre can swap types without breaking saves
        public readonly struct SpotDef
        {
            public readonly string Id;
            public readonly string Prefab;
            public readonly Vector2 Pos;
            public readonly string Zone;

            public SpotDef(string id, string prefab, float x, float z, string zone)
            {
                Id = id;
                Prefab = prefab;
                Pos = new Vector2(x, z);
                Zone = zone;
            }
        }

        public static readonly SpotDef[] Spots =
        {
            new("green_mine.forest_01", "Treasure_GoldCoin", -16.2f, 8.0f, "Forest"),
            new("green_mine.forest_02", "Treasure_GoldNugget", -9.6f, 14.2f, "Forest"),
            new("green_mine.meadow_01", "Treasure_GoldCoin", -1.4f, 0.4f, "Meadow"),
            new("green_mine.mine_01", "Treasure_AncientArtifact", -3.6f, 11.0f, "Mine"),
            new("green_mine.rocky_01", "Treasure_GoldNugget", 8.6f, -9.6f, "Rocky"),
            new("green_mine.rocky_02", "Treasure_GoldBar", 6.4f, -14.6f, "Rocky"),
            new("green_mine.rocky_03", "Treasure_GoldCoin", 14.4f, -13.4f, "Rocky"),
            new("green_mine.riverbank_01", "Treasure_GoldNugget", 18.0f, 6.4f, "River"),
        };

        // ================================================================ math helpers

        public static float S(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }

        // ---- smooth (Catmull-Rom) versions of the trails and the river, and distance fields over them (0.5 m resolution)

        private const int FieldRes = 2;
        private const float FieldHalf = 40f;
        private static Vector2[][] pathsDense;
        private static Vector3[] riverDense;
        private static float[,] pathField, riverField, riverHalfField;

        public static Vector2[][] PathsDense
        {
            get
            {
                EnsureFields();
                return pathsDense;
            }
        }

        public static Vector3[] RiverDense
        {
            get
            {
                EnsureFields();
                return riverDense;
            }
        }

        public static void Reset()
        {
            pathsDense = null;
            riverDense = null;
            pathField = riverField = riverHalfField = null;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) + (-p0 + 3f * p1 - 3f * p2 + p3) * (t * t * t));
        }

        private static Vector2[] Smooth(Vector2[] pts, int sub)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 p0 = pts[Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(i + 2, pts.Length - 1)];
                for (int s = 0; s < sub; s++)
                {
                    result.Add(CatmullRom(p0, p1, p2, p3, s / (float)sub));
                }
            }

            result.Add(pts[pts.Length - 1]);
            return result.ToArray();
        }

        private static void EnsureFields()
        {
            if (pathField != null)
            {
                return;
            }

            pathsDense = new Vector2[Paths.Length][];
            for (int i = 0; i < Paths.Length; i++)
            {
                pathsDense[i] = Smooth(Paths[i], 4);
            }

            var xz = new Vector2[River.Length];
            for (int i = 0; i < River.Length; i++)
            {
                xz[i] = new Vector2(River[i].x, River[i].y);
            }

            Vector2[] smooth = Smooth(xz, 4);
            riverDense = new Vector3[smooth.Length];
            for (int i = 0; i < smooth.Length; i++)
            {
                float f = i / 4f;
                int a = Mathf.Min((int)f, River.Length - 2);
                riverDense[i] = new Vector3(smooth[i].x, smooth[i].y, Mathf.Lerp(River[a].z, River[a + 1].z, Mathf.Clamp01(f - a)));
            }

            int n = (int)(FieldHalf * 2f * FieldRes) + 1;
            pathField = new float[n, n];
            riverField = new float[n, n];
            riverHalfField = new float[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    var p = new Vector2(-FieldHalf + i / (float)FieldRes, -FieldHalf + j / (float)FieldRes);
                    float best = float.MaxValue;
                    foreach (Vector2[] path in pathsDense)
                    {
                        for (int k = 0; k < path.Length - 1; k++)
                        {
                            best = Mathf.Min(best, SegmentDistance(p, path[k], path[k + 1], out _));
                        }
                    }

                    pathField[i, j] = best;
                    float rb = float.MaxValue, half = riverDense[0].z;
                    for (int k = 0; k < riverDense.Length - 1; k++)
                    {
                        float d = SegmentDistance(p, new Vector2(riverDense[k].x, riverDense[k].y), new Vector2(riverDense[k + 1].x, riverDense[k + 1].y), out float t);
                        if (d < rb)
                        {
                            rb = d;
                            half = Mathf.Lerp(riverDense[k].z, riverDense[k + 1].z, t);
                        }
                    }

                    riverField[i, j] = rb;
                    riverHalfField[i, j] = half;
                }
            }
        }

        private static float Sample(float[,] field, float x, float z)
        {
            int n = field.GetLength(0);
            float fx = Mathf.Clamp((x + FieldHalf) * FieldRes, 0f, n - 1.001f);
            float fz = Mathf.Clamp((z + FieldHalf) * FieldRes, 0f, n - 1.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = Mathf.Lerp(field[i, j], field[i + 1, j], tx);
            float b = Mathf.Lerp(field[i, j + 1], field[i + 1, j + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        /// <summary>Distance to the nearest trail centre line (any trail).</summary>
        public static float PathDistance(float x, float z)
        {
            EnsureFields();
            return Sample(pathField, x, z);
        }

        /// <summary>Distance to the river centre line, and the water half width there.</summary>
        public static float RiverDistance(float x, float z, out float half)
        {
            EnsureFields();
            half = Sample(riverHalfField, x, z);
            return Sample(riverField, x, z);
        }

        public static float CampDistance(float x, float z) => Vector2.Distance(new Vector2(x, z), CampCenter);

        // ================================================================ terrain height

        /// <summary>Analytic ground height. Gentle rolling meadow inside the playable area, hills and cliffs on the rim, a flat river bed, flat camp / gate / mine apron.</summary>
        public static float Height(float x, float z)
        {
            float h = (Mathf.PerlinNoise(x * 0.07f + 31f, z * 0.07f + 17f) - 0.5f) * 1.0f
                      + (Mathf.PerlinNoise(x * 0.19f + 7f, z * 0.19f + 53f) - 0.5f) * 0.3f;

            float calm = 1f - S(1.6f, 4.2f, PathDistance(x, z));
            h *= 1f - 0.75f * calm; // trails are smoother than the meadow

            // flat places
            h = Mathf.Lerp(h, 0.05f, 1f - S(4.6f, 8.0f, CampDistance(x, z)));
            h = Mathf.Lerp(h, 0.00f, 1f - S(2.4f, 5.6f, Vector2.Distance(new Vector2(x, z), GatePos + new Vector2(0f, 0.8f))));
            float apron = Mathf.Sqrt(Mathf.Pow((x - ApronCenter.x) / 6.8f, 2f) + Mathf.Pow((z - ApronCenter.y) / 2.4f, 2f));
            h = Mathf.Lerp(h, 0.04f, 1f - S(0.7f, 1.3f, apron));
            float bay = (1f - S(0f, 1.5f, Mathf.Abs(x - 6.3f) - 5.6f)) * (1f - S(0f, 1.5f, Mathf.Abs(z - 16f) - 6.2f));
            h = Mathf.Lerp(h, 0.04f, bay);
            float rocky = Vector2.Distance(new Vector2(x, z), RockyCenter + new Vector2(-2f, -1.5f));
            h = Mathf.Lerp(h, h * 0.6f, 1f - S(3f, 7f, rocky));

            // river bed (shallow, walkable banks)
            float rd = RiverDistance(x, z, out float half);
            h = Mathf.Lerp(h, 0.10f, 1f - S(half + 1.0f, half + 5.0f, rd));
            h = Mathf.Lerp(h, RiverBed, 1f - S(half * 0.55f, half + 1.7f, rd));

            // rim hills (hide the terrain edge) and the mine hillside in the north
            float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float rim = S(18.5f, 31f, r) * (6.5f + 4f * Mathf.PerlinNoise(x * 0.06f + 2f, z * 0.06f + 5f));
            float wMine = S(-6f, -1f, x);
            // in front of the mine the hillside starts further back (a flat bay under the rock face); cliffs on both sides of it
            float hillStart = Mathf.Lerp(14.7f, 22f, S(0.2f, 1.6f, x) * (1f - S(11.4f, 12.8f, x)));
            float north = wMine * S(hillStart, hillStart + 4f, z) * 9.5f + (1f - wMine) * S(15f, 30f, z) * 4.5f;
            return h + Mathf.Max(rim * (1f - bay), north); // the mine bay stays flat under the tunnel
        }

        // ================================================================ playable boundary (invisible walls)

        public static readonly Vector2[] Boundary =
        {
            new(-Play, -Play), new(Play, -Play), new(Play, 15.3f), new(-1.5f, 15.3f), new(-5.2f, Play), new(-Play, Play),
        };
    }
}
#endif
