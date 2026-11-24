#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    public enum AmZone
    {
        None,
        Gate,      // A  Ancient Gate: the start
        Hall,      // B  Hall of Pillars: the centre and the main junction
        Crystal,   // C  Crystal Caverns: organic caves on the east side
        Workshop,  // D  Forgotten Workshop: the alternative route
        Vault,     // E  Royal Vault: the last exploration area
    }

    /// <summary>
    /// The Ancient Mine map as DATA (stage 16A). A cave is the space between a FLOOR height field and a CEILING height field, both sampled on a 2 m grid. The walkable
    /// places are "shapes" (rounded rectangles for the halls, chains of capsules for passages and caves, discs for chambers); each has a floor height, a ceiling and
    /// a wall "run" (how many metres of rock the wall needs to climb from the floor to the ceiling: 2 m = a crisp hall wall, 3-4.5 m = an organic cave wall).
    /// Inside a shape the floor is the shape's floor (the highest priority wins where shapes overlap, equal-priority shapes must agree on the height);
    /// outside every shape the floor climbs the wall of the NEAREST shape and meets the ceiling: solid rock. Nothing else decides where the player can stand,
    /// how steep it is and which areas connect: the builder turns this into meshes and colliders, the tests walk it.
    ///
    /// Units are metres, x east, z north, y up. Walkable ground: about 122 x 102 m (x -61.5..61.5, z -52.5..50) inside a mesh of 140 x 120 m.
    /// Floors run from 0 (Royal Vault) to 15.5 (Ancient Gate), ceilings reach 33.5.
    /// </summary>
    public static class AmLayout
    {
        public const float HalfX = 70f;
        public const float HalfZ = 60f;
        public const float Cell = 2f;
        public const int CellsX = 70;   // 2 * HalfX / Cell
        public const int CellsZ = 60;   // 2 * HalfZ / Cell

        public sealed class Shape
        {
            public string Name;
            public AmZone Zone;
            public bool IsRect;
            public Vector2 Centre, Half;         // rect
            public float Yaw, Corner;            // rect (degrees, metres)
            public float Floor;                  // rect
            public Vector3[] Points;             // chain: x, FLOOR HEIGHT, z (one point = a disc)
            public float Radius;                 // chain
            public float Clear = 10f;            // ceiling above the floor
            public float CeilAbs;                // > 0: absolute ceiling height instead of floor + Clear
            public float Run = 3f;               // horizontal run of the wall from floor to ceiling
            public float Power = 1.5f;           // wall profile exponent (1 = flat plane, > 1 = concave rock skirt)
            public float Rough;                  // floor undulation (m)
            public float CeilRough;              // ceiling undulation (m)
            public int Priority;                 // the highest priority decides the floor where shapes overlap

            public float Ceiling(float floor) => CeilAbs > 0f ? CeilAbs : floor + Clear;

            /// <summary>Signed distance to the shape's edge (negative inside) and the floor height at the nearest point of it.</summary>
            public float Distance(float x, float z, out float floor)
            {
                if (IsRect)
                {
                    floor = Floor;
                    float rad = Yaw * Mathf.Deg2Rad, c = Mathf.Cos(rad), s = Mathf.Sin(rad);
                    float dx = x - Centre.x, dz = z - Centre.y;
                    float lx = Mathf.Abs(dx * c + dz * s), lz = Mathf.Abs(-dx * s + dz * c);
                    float qx = lx - Half.x + Corner, qz = lz - Half.y + Corner;
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qz, 0f) * Mathf.Max(qz, 0f));
                    return outside + Mathf.Min(Mathf.Max(qx, qz), 0f) - Corner;
                }

                Vector3[] p = Points;
                if (p.Length == 1)
                {
                    floor = p[0].y;
                    return Mathf.Sqrt((x - p[0].x) * (x - p[0].x) + (z - p[0].z) * (z - p[0].z)) - Radius;
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

                return best - Radius;
            }
        }

        private static Shape Rect(string name, AmZone zone, float cx, float cz, float hx, float hz, float floor, float ceilAbs, float run = 2f, float corner = 3f, int priority = 0, float rough = 0f)
        {
            return new Shape { Name = name, Zone = zone, IsRect = true, Centre = new Vector2(cx, cz), Half = new Vector2(hx, hz), Floor = floor, CeilAbs = ceilAbs, Run = run, Corner = corner, Power = 1f, Priority = priority, Rough = rough };
        }

        private static Shape Chain(string name, AmZone zone, float radius, float clear, float run, float rough, int priority, params Vector3[] points)
        {
            return new Shape { Name = name, Zone = zone, Radius = radius, Clear = clear, Run = run, Rough = rough, Priority = priority, Points = points, Power = 1.3f };
        }

        private static Shape Disc(string name, AmZone zone, float cx, float cz, float r, float floor, float clear, float run, float rough, float ceilRough = 0.7f)
        {
            return new Shape { Name = name, Zone = zone, Radius = r, Clear = clear, Run = run, Rough = rough, CeilRough = ceilRough, Points = new[] { new Vector3(cx, floor, cz) }, Power = 1.5f };
        }

        public static readonly Vector3 Spawn = new(-45f, 15.5f, -2f);
        public const float SpawnYaw = 90f;

        public static readonly Shape[] Shapes = BuildShapes();

        private static Shape[] BuildShapes()
        {
            var s = new List<Shape>
            {
                // ---------------------------------------------------------------- A  Ancient Gate (floor 15.5): the start, a wide hall with the old stone road and the great portal on its west wall
                Rect("GateHall", AmZone.Gate, -50f, -2f, 11f, 12f, 15.5f, 33.5f),
                // the old stone road leaves the hall in the east and descends to the Hall of Pillars (0.30 rise per metre, 8.8 m wide)
                Chain("GateRoad", AmZone.Gate, 4.4f, 10f, 2.5f, 0f, 0, new Vector3(-42f, 15.5f, -2f), new Vector3(-39f, 15.5f, -2f), new Vector3(-24f, 11.0f, -2f), new Vector3(-21f, 11.0f, -2f)),
                // a hidden side passage from the south-west corner of the hall to a dead-end chamber (the Old Cistern)
                Chain("CisternPath", AmZone.Gate, 3.6f, 9.5f, 3f, 0.08f, 0, new Vector3(-56f, 15.5f, -12f), new Vector3(-56f, 15.5f, -15f), new Vector3(-56f, 15.0f, -19f), new Vector3(-55f, 14.0f, -23f), new Vector3(-54f, 14.0f, -29f)),
                Disc("Cistern", AmZone.Gate, -54f, -34f, 7.5f, 14.0f, 11f, 4f, 0.12f),

                // ---------------------------------------------------------------- B  Hall of Pillars (floor 11): the centre, three exits, four raised daises in the corners
                Rect("Hall", AmZone.Hall, 0f, 0f, 24f, 20f, 11.0f, 32f),
                Chain("HallNorth", AmZone.Hall, 4.4f, 10f, 2.5f, 0f, 0, new Vector3(0f, 11.0f, 17f), new Vector3(0f, 11.0f, 20f), new Vector3(0f, 8.5f, 30f), new Vector3(0f, 8.5f, 33f)),
                Chain("HallEast", AmZone.Hall, 4.4f, 10f, 2.5f, 0f, 0, new Vector3(21f, 11.0f, -4f), new Vector3(24f, 11.0f, -4f), new Vector3(35f, 7.6f, -4f), new Vector3(39f, 7.6f, -4f)),
                Rect("DaisNW", AmZone.Hall, -20f, 17f, 4f, 3f, 13.4f, 32f, 2f, 0.5f, 1),
                Rect("DaisNE", AmZone.Hall, 20f, 17f, 4f, 3f, 13.4f, 32f, 2f, 0.5f, 1),
                Rect("DaisSW", AmZone.Hall, -20f, -17f, 4f, 3f, 13.4f, 32f, 2f, 0.5f, 1),
                Rect("DaisSE", AmZone.Hall, 20f, -17f, 4f, 3f, 13.4f, 32f, 2f, 0.5f, 1),
                Chain("DaisRampNW", AmZone.Hall, 3f, 10f, 2f, 0f, 1, new Vector3(-16f, 13.4f, 17f), new Vector3(-7f, 11.0f, 17f)),
                Chain("DaisRampNE", AmZone.Hall, 3f, 10f, 2f, 0f, 1, new Vector3(16f, 13.4f, 17f), new Vector3(7f, 11.0f, 17f)),
                Chain("DaisRampSW", AmZone.Hall, 3f, 10f, 2f, 0f, 1, new Vector3(-16f, 13.4f, -17f), new Vector3(-7f, 11.0f, -17f)),
                Chain("DaisRampSE", AmZone.Hall, 3f, 10f, 2f, 0f, 1, new Vector3(16f, 13.4f, -17f), new Vector3(7f, 11.0f, -17f)),

                // ---------------------------------------------------------------- C  Crystal Caverns (floors 7.6 .. 3.2): an atrium, a northern and a southern branch with a lobe each, a side grotto
                Disc("Atrium", AmZone.Crystal, 48f, -4f, 13f, 7.6f, 14f, 4.5f, 0.14f),
                Chain("CrystalNorth", AmZone.Crystal, 4.4f, 11.5f, 3.5f, 0.1f, 0, new Vector3(50f, 7.6f, 5f), new Vector3(54f, 7.2f, 13f), new Vector3(50f, 6.3f, 21f), new Vector3(47f, 5.7f, 25f), new Vector3(46f, 5.6f, 28f)),
                Disc("LobeNorth", AmZone.Crystal, 46f, 33f, 8.5f, 5.6f, 13f, 4.5f, 0.14f),
                Chain("CrystalSouth", AmZone.Crystal, 4.4f, 11.5f, 3.5f, 0.1f, 0, new Vector3(46f, 7.6f, -8f), new Vector3(54f, 7.2f, -14f), new Vector3(51f, 6.0f, -22f), new Vector3(53f, 4.4f, -28f), new Vector3(48f, 3.4f, -31f)),
                Disc("LobeSouth", AmZone.Crystal, 47f, -37f, 8.5f, 3.2f, 13f, 4.5f, 0.14f),
                Chain("GrottoPath", AmZone.Crystal, 3.4f, 10f, 3.5f, 0.1f, 0, new Vector3(51f, 6.6f, 18f), new Vector3(44f, 6.5f, 16f), new Vector3(40f, 6.4f, 14f)),
                Disc("Grotto", AmZone.Crystal, 36f, 14f, 5.5f, 6.4f, 11f, 4f, 0.12f),

                // ---------------------------------------------------------------- D  Forgotten Workshop (floor 8.5): the big work hall, the tunnel east to the caverns, a partly collapsed tunnel west, a collapsed spur
                Rect("Workshop", AmZone.Workshop, -12f, 40f, 22f, 10f, 8.5f, 22.5f, 2.5f),
                Chain("WorkshopEast", AmZone.Workshop, 4f, 11f, 3.5f, 0.06f, 0, new Vector3(7f, 8.5f, 40f), new Vector3(12f, 8.4f, 41f), new Vector3(24f, 7.4f, 38f), new Vector3(34f, 6.0f, 35f), new Vector3(38f, 5.6f, 33.5f), new Vector3(41f, 5.6f, 33f)),
                Chain("WestTunnel", AmZone.Workshop, 3.6f, 10f, 3f, 0.08f, 0, new Vector3(-31f, 8.5f, 40f), new Vector3(-38f, 8.5f, 41f), new Vector3(-45f, 8.1f, 43f), new Vector3(-49f, 7.9f, 45f)),
                Disc("CacheChamber", AmZone.Workshop, -54f, 46f, 6.5f, 7.9f, 11f, 4f, 0.12f),
                Chain("CollapsedSpur", AmZone.Workshop, 3.4f, 10f, 3f, 0.08f, 0, new Vector3(-26f, 8.5f, 31f), new Vector3(-30f, 8.3f, 26f), new Vector3(-34f, 8.0f, 21f)),
                Disc("SpurEnd", AmZone.Workshop, -35f, 18f, 4.6f, 8.0f, 10f, 3.5f, 0.1f),

                // ---------------------------------------------------------------- E  Royal Vault (floor 0): a big symmetric hall, a central dais with two wide ramps, three treasure alcoves
                Rect("Vault", AmZone.Vault, 10f, -38f, 18f, 11f, 0f, 22f),
                Chain("VaultRoad", AmZone.Vault, 4.4f, 16f, 2.5f, 0f, 0, new Vector3(41f, 3.2f, -37f), new Vector3(39f, 3.2f, -37f), new Vector3(28f, 0f, -37f), new Vector3(25f, 0f, -37f)),
                Rect("VaultDais", AmZone.Vault, 10f, -38f, 7f, 4.5f, 1.8f, 22f, 2f, 0.5f, 1),
                Chain("DaisRampW", AmZone.Vault, 3.2f, 12f, 2f, 0f, 1, new Vector3(3f, 1.8f, -38f), new Vector3(-3f, 0f, -38f), new Vector3(-4f, 0f, -38f)),
                Chain("DaisRampE", AmZone.Vault, 3.2f, 12f, 2f, 0f, 1, new Vector3(17f, 1.8f, -38f), new Vector3(24f, 0f, -38f), new Vector3(25f, 0f, -38f)),
                Rect("AlcoveSouth1", AmZone.Vault, 0f, -50f, 3.2f, 2.4f, 0f, 15f, 2.5f, 0.5f),
                Rect("AlcoveSouth2", AmZone.Vault, 10f, -50f, 3.2f, 2.4f, 0f, 15f, 2.5f, 0.5f),
                Rect("AlcoveSouth3", AmZone.Vault, 20f, -50f, 3.2f, 2.4f, 0f, 15f, 2.5f, 0.5f),
            };
            return s.ToArray();
        }

        // ------------------------------------------------------------------ zone anchors and routes (the scene gets them as marker transforms; the tests walk them)

        public static readonly (AmZone zone, string name, Vector2 point)[] Anchors =
        {
            (AmZone.Gate, "A_AncientGate", new Vector2(-47f, -2f)),
            (AmZone.Hall, "B_HallOfPillars", new Vector2(0f, 0f)),
            (AmZone.Crystal, "C_CrystalCaverns", new Vector2(48f, -4f)),
            (AmZone.Workshop, "D_ForgottenWorkshop", new Vector2(-12f, 40f)),
            (AmZone.Vault, "E_RoyalVault", new Vector2(10f, -38f)),
        };

        /// <summary>The main route, Ancient Gate -> Hall of Pillars -> Crystal Caverns -> Royal Vault.</summary>
        public static readonly (string name, Vector2 point)[] MainRoute =
        {
            ("00_Spawn", new Vector2(-45f, -2f)), ("01_GateMouth", new Vector2(-40f, -2f)), ("02_RoadMiddle", new Vector2(-31f, -2f)), ("03_HallWestDoor", new Vector2(-22f, -2f)),
            ("04_HallAisleWest", new Vector2(-10f, 0f)), ("05_HallCentre", new Vector2(0f, 0f)), ("06_HallAisleEast", new Vector2(12f, -2f)), ("07_HallEastDoor", new Vector2(24f, -4f)),
            ("08_EastRamp", new Vector2(32f, -4f)), ("09_AtriumDoor", new Vector2(40f, -4f)), ("10_Atrium", new Vector2(48f, -4f)), ("11_SouthBranch1", new Vector2(46f, -8f)),
            ("12_SouthBranch2", new Vector2(54f, -14f)), ("13_SouthBranch3", new Vector2(51f, -22f)), ("14_SouthBranch4", new Vector2(53f, -28f)), ("15_SouthBranch5", new Vector2(48f, -31f)),
            ("16_LobeSouth", new Vector2(47f, -37f)), ("17_VaultRoad1", new Vector2(41f, -37f)), ("18_VaultRoad2", new Vector2(34f, -37f)), ("19_VaultDoor", new Vector2(29f, -37f)),
            ("20_VaultRamp", new Vector2(24f, -38f)), ("21_VaultDais", new Vector2(10f, -38f)),
        };

        /// <summary>The alternative route, Hall of Pillars -> Forgotten Workshop -> Crystal Caverns (back to the atrium, which closes the loop with the east door of the hall).</summary>
        public static readonly (string name, Vector2 point)[] AlternateRoute =
        {
            ("00_HallCentre", new Vector2(0f, 0f)), ("01_HallNorth", new Vector2(0f, 12f)), ("02_HallNorthDoor", new Vector2(0f, 20f)), ("03_NorthRamp", new Vector2(0f, 26f)),
            ("04_WorkshopDoor", new Vector2(0f, 33f)), ("05_WorkshopCentre", new Vector2(-12f, 40f)), ("06_WorkshopEast", new Vector2(4f, 40f)), ("07_EastTunnel1", new Vector2(12f, 41f)),
            ("08_EastTunnel2", new Vector2(24f, 38f)), ("09_EastTunnel3", new Vector2(34f, 35f)), ("10_LobeNorth", new Vector2(45f, 32f)), ("11_NorthBranch1", new Vector2(46f, 28f)), ("11b_NorthBranch1b", new Vector2(47f, 25f)),
            ("12_NorthBranch2", new Vector2(50f, 21f)), ("13_NorthBranch3", new Vector2(54f, 13f)), ("14_NorthBranch4", new Vector2(50f, 5f)), ("15_Atrium", new Vector2(48f, -4f)),
            ("16_AtriumDoor", new Vector2(40f, -4f)), ("17_EastRamp", new Vector2(32f, -4f)), ("18_HallEastDoor", new Vector2(24f, -4f)), ("19_HallCentre", new Vector2(0f, 0f)),
        };

        /// <summary>Side areas, dead ends and special exploration places: walks that start ON the main or the alternate route and end in the named place.</summary>
        public static readonly (string name, Vector2[] points)[] SideRoutes =
        {
            ("Cistern", new[] { new Vector2(-45f, -2f), new Vector2(-53f, -4f), new Vector2(-55.5f, -9f), new Vector2(-56f, -15f), new Vector2(-56f, -19f), new Vector2(-55f, -23f), new Vector2(-54f, -28f), new Vector2(-49.5f, -33f) }),
            ("Grotto", new[] { new Vector2(48f, -4f), new Vector2(50f, 5f), new Vector2(54f, 13f), new Vector2(51f, 18f), new Vector2(44f, 16f), new Vector2(40f, 14f), new Vector2(36f, 14f) }),
            ("CacheChamber", new[] { new Vector2(-12f, 40f), new Vector2(-31f, 40f), new Vector2(-38f, 41f), new Vector2(-45f, 43f), new Vector2(-49f, 45f), new Vector2(-50.5f, 42.5f) }),
            ("CollapsedSpur", new[] { new Vector2(-12f, 40f), new Vector2(-26f, 40f), new Vector2(-27f, 35f), new Vector2(-26.5f, 32f), new Vector2(-30f, 26f), new Vector2(-34f, 20.5f) }),
            ("DaisNW", new[] { new Vector2(0f, 0f), new Vector2(-8.4f, 3f), new Vector2(-8.4f, 13f), new Vector2(-10f, 17f), new Vector2(-16f, 17f), new Vector2(-18.5f, 16f) }),
            ("DaisSE", new[] { new Vector2(0f, 0f), new Vector2(8.4f, -3f), new Vector2(8.4f, -13f), new Vector2(10f, -17f), new Vector2(16f, -17f), new Vector2(18.5f, -16f) }),
            ("VaultAlcove", new[] { new Vector2(24f, -38f), new Vector2(21f, -41.5f), new Vector2(19.5f, -44f), new Vector2(19.5f, -47.1f), new Vector2(10f, -47.1f) }),
            ("VaultNorthWest", new[] { new Vector2(10f, -38f), new Vector2(3f, -38f), new Vector2(-3f, -38f), new Vector2(-1f, -35.5f), new Vector2(0f, -33.5f) }),
        };

        /// <summary>The end points of the side routes.</summary>
        public static readonly (string name, Vector2 point)[] SidePlaces = BuildSidePlaces();

        private static (string, Vector2)[] BuildSidePlaces()
        {
            var list = new (string, Vector2)[SideRoutes.Length];
            for (int i = 0; i < SideRoutes.Length; i++)
            {
                list[i] = (SideRoutes[i].name, SideRoutes[i].points[SideRoutes[i].points.Length - 1]);
            }

            return list;
        }

        /// <summary>
        /// Places kept clear for the treasures of stage 16C (NOT treasures: only empty marker transforms under Markers/TreasureCandidates): id, point, lift above the floor (a pedestal top).
        /// Every one is inside a walkable shape, reachable, and has about 1 m of free space round it (checked by the tests).
        /// </summary>
        public static readonly (string id, Vector2 point, float lift)[] TreasureCandidates =
        {
            ("A_Gate_01", new Vector2(-49.5f, 0.8f), 0f), ("A_Gate_02", new Vector2(-49f, -7f), 0f), ("A_Gate_03", new Vector2(-56f, 2f), 0f), ("A_Cistern_01", new Vector2(-49f, -35.5f), 0f),
            ("B_Dais_NW", new Vector2(-20f, 17f), 0f), ("B_Dais_NE", new Vector2(20f, 17f), 0f), ("B_Dais_SW", new Vector2(-20f, -17f), 0f), ("B_Dais_SE", new Vector2(20f, -17f), 0f),
            ("B_Aisle_South", new Vector2(-14.5f, -11.5f), 0f), ("B_Aisle_North", new Vector2(14.5f, 11.5f), 0f),
            ("C_Grotto_01", new Vector2(36f, 14f), 0f), ("C_Atrium_01", new Vector2(50.5f, -0.5f), 0f), ("C_LobeSouth_01", new Vector2(44.5f, -33.5f), 0f), ("C_LobeSouth_02", new Vector2(43.5f, -40f), 0f),
            ("C_LobeNorth_01", new Vector2(43f, 36f), 0f),
            ("D_Workshop_01", new Vector2(-5.5f, 43.5f), 0f), ("D_Workshop_02", new Vector2(-17f, 38f), 0f), ("D_Workshop_03", new Vector2(-24.5f, 41.8f), 0f), ("D_Cache_01", new Vector2(-51.5f, 45.2f), 0f),
            ("D_Spur_01", new Vector2(-33.5f, 18.5f), 0f),
            ("E_Dais_01", new Vector2(10f, -38.2f), 0f), ("E_Dais_02", new Vector2(14.5f, -39.5f), 0f), ("E_Dais_03", new Vector2(5.5f, -40f), 0f),
            ("E_Pedestal_01", new Vector2(0f, -50f), 1.35f), ("E_Pedestal_02", new Vector2(10f, -50f), 1.35f), ("E_Pedestal_03", new Vector2(20f, -50f), 1.35f),
        };

        // ------------------------------------------------------------------ height field

        private static float Noise(float x, float z)
        {
            float n = 0f, amp = 1f, freq = 1f / 8f;
            for (int o = 0; o < 3; o++)
            {
                float fx = x * freq, fz = z * freq;
                int ix = Mathf.FloorToInt(fx), iz = Mathf.FloorToInt(fz);
                float tx = fx - ix, tz = fz - iz;
                tx = tx * tx * (3f - 2f * tx);
                tz = tz * tz * (3f - 2f * tz);
                n += Mathf.Lerp(Mathf.Lerp(Hash(ix, iz), Hash(ix + 1, iz), tx), Mathf.Lerp(Hash(ix, iz + 1), Hash(ix + 1, iz + 1), tx), tz) * amp;
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

        public struct Sample
        {
            public float Floor, Ceiling;
            public bool Solid;      // outside every cave: rock
            public bool Inside;     // inside a shape (floor level, walkable)
            public int Shape;       // the shape that owns this point (deepest inside / nearest outside)
            public float Edge;      // signed distance to the owner's edge
        }

        private const float BlendLength = 2.5f; // metres of depth over which same-priority floors blend

        private static readonly float[] EdgeBuffer = new float[64];
        private static readonly float[] FloorBuffer = new float[64];

        public static Sample Evaluate(float x, float z)
        {
            Shape[] shapes = Shapes;
            int n = shapes.Length;
            float minE = float.MaxValue;
            int nearest = -1, maxPriority = int.MinValue;
            for (int i = 0; i < n; i++)
            {
                float e = shapes[i].Distance(x, z, out float f);
                EdgeBuffer[i] = e;
                FloorBuffer[i] = f;
                if (e < minE)
                {
                    minE = e;
                    nearest = i;
                }

                if (e <= 0f && shapes[i].Priority > maxPriority)
                {
                    maxPriority = shapes[i].Priority;
                }
            }

            var result = new Sample();
            if (maxPriority != int.MinValue)
            {
                float sum = 0f, weightSum = 0f, ceiling = float.MinValue, deepest = float.MaxValue;
                int count = 0, owner = -1;
                for (int i = 0; i < n; i++)
                {
                    if (EdgeBuffer[i] > 0f)
                    {
                        continue;
                    }

                    ceiling = Mathf.Max(ceiling, shapes[i].Ceiling(FloorBuffer[i]));
                    if (shapes[i].Priority == maxPriority)
                    {
                        // shapes of the same priority blend by how DEEP the point lies inside each (zero weight at an edge, so the floor is continuous where a shape starts):
                        // a passage's floor melts into the floor of the cave it enters instead of leaving a step
                        float weight = Mathf.Max(1e-3f, Mathf.Exp(-EdgeBuffer[i] / BlendLength) - 1f);
                        sum += FloorBuffer[i] * weight;
                        weightSum += weight;
                        count++;
                        if (EdgeBuffer[i] < deepest)
                        {
                            deepest = EdgeBuffer[i];
                            owner = i;
                        }
                    }
                }

                Shape o = shapes[owner];
                result.Floor = sum / weightSum + Noise(x, z) * o.Rough * Mathf.Clamp01(-deepest / 1.5f);
                result.Ceiling = ceiling + (o.CeilRough > 0f ? Noise(x + 91f, z - 47f) * o.CeilRough : 0f);
                result.Inside = true;
                result.Shape = owner;
                result.Edge = deepest;
                return result;
            }

            Shape w = shapes[nearest];
            float floor = FloorBuffer[nearest];
            float top = w.Ceiling(floor);
            result.Shape = nearest;
            result.Edge = minE;
            if (minE >= w.Run)
            {
                result.Solid = true;
                result.Floor = top;
                result.Ceiling = top;
                return result;
            }

            float t = Mathf.Clamp01(minE / w.Run);
            float rise = (top - floor) * Mathf.Pow(t, w.Power);
            float rock = Noise(x, z) * (w.Power > 1.1f ? 1.1f : 0.1f) * t * (1f - t) * 4f;
            result.Floor = Mathf.Min(floor + rise + rock, top);
            result.Ceiling = top + (w.CeilRough > 0f ? Noise(x + 91f, z - 47f) * w.CeilRough : 0f);
            return result;
        }

        // ------------------------------------------------------------------ the mesh grid (what the game really stands under and on)

        private static Sample[,] grid;

        public static void ResetGrid()
        {
            grid = null;
        }

        public static Sample[,] Grid
        {
            get
            {
                if (grid == null)
                {
                    grid = new Sample[CellsX + 1, CellsZ + 1];
                    for (int i = 0; i <= CellsX; i++)
                    {
                        for (int j = 0; j <= CellsZ; j++)
                        {
                            Sample v = Evaluate(-HalfX + i * Cell, -HalfZ + j * Cell);
                            v.Floor = Mathf.Min(v.Floor, v.Ceiling); // the floor never pierces the ceiling: where it would, the two meet and the rock is closed
                            grid[i, j] = v;
                        }
                    }
                }

                return grid;
            }
        }

        /// <summary>A grid vertex is rock when the floor reaches the ceiling there.</summary>
        public static bool IsSolid(Sample s) => s.Solid || s.Ceiling - s.Floor < 0.05f;

        /// <summary>Height of the floor MESH at (x, z), interpolated exactly like its triangles (props sit on the real surface).</summary>
        public static float Surface(float x, float z)
        {
            Sample[,] g = Grid;
            float fx = Mathf.Clamp((x + HalfX) / Cell, 0f, CellsX - 0.001f);
            float fz = Mathf.Clamp((z + HalfZ) / Cell, 0f, CellsZ - 0.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = g[i, j].Floor, b = g[i + 1, j].Floor, c = g[i, j + 1].Floor, d = g[i + 1, j + 1].Floor;
            return tx + tz <= 1f ? a + (b - a) * tx + (c - a) * tz : d + (c - d) * (1f - tx) + (b - d) * (1f - tz);
        }

        /// <summary>Rise over run of the floor mesh around (x, z).</summary>
        public static float Slope(float x, float z)
        {
            float dx = Surface(x + 0.5f, z) - Surface(x - 0.5f, z);
            float dz = Surface(x, z + 0.5f) - Surface(x, z - 0.5f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Height of the ceiling at (x, z) (bilinear on the grid; props that hang from the ceiling use it).</summary>
        public static float CeilingAt(float x, float z)
        {
            Sample[,] g = Grid;
            float fx = Mathf.Clamp((x + HalfX) / Cell, 0f, CellsX - 0.001f);
            float fz = Mathf.Clamp((z + HalfZ) / Cell, 0f, CellsZ - 0.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = g[i, j].Ceiling, b = g[i + 1, j].Ceiling, c = g[i, j + 1].Ceiling, d = g[i + 1, j + 1].Ceiling;
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        /// <summary>Every route as a list of points: main, alternate and the side routes.</summary>
        public static System.Collections.Generic.IEnumerable<(string name, Vector2[] points)> AllRoutes()
        {
            yield return ("main", System.Array.ConvertAll(MainRoute, r => r.point));
            yield return ("alternate", System.Array.ConvertAll(AlternateRoute, r => r.point));
            foreach ((string name, Vector2[] points) in SideRoutes)
            {
                yield return ("side " + name, points);
            }
        }

        /// <summary>Distance from (x, z) to the nearest leg of any route (props keep clear of the routes).</summary>
        public static float RouteDistance(float x, float z)
        {
            float best = float.MaxValue;
            foreach ((string name, Vector2[] route) in AllRoutes())
            {
                for (int i = 0; i < route.Length - 1; i++)
                {
                    Vector2 a = route[i], b = route[i + 1], p = new(x, z), ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                    best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
                }
            }

            return best;
        }

        /// <summary>Signed distance of (x, z) to the nearest cave shape's edge (negative = walkable inside, the number is the free radius to the wall).</summary>
        public static float EdgeDistance(float x, float z)
        {
            float best = float.MaxValue;
            foreach (Shape s in Shapes)
            {
                best = Mathf.Min(best, s.Distance(x, z, out _));
            }

            return best;
        }

        /// <summary>The true free radius round (x, z): the distance to the nearest rock (a point outside every shape), found on rings of 16 points. Stops at 12 m.</summary>
        public static float FreeRadius(float x, float z)
        {
            for (float r = 0.25f; r <= 12f; r += 0.25f)
            {
                for (int k = 0; k < 16; k++)
                {
                    float a = k / 16f * Mathf.PI * 2f;
                    if (EdgeDistance(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r) > 0f)
                    {
                        return r;
                    }
                }
            }

            return 12f;
        }

        public static AmZone ZoneAt(float x, float z)
        {
            Sample s = Evaluate(x, z);
            return Shapes[s.Shape].Zone;
        }

        // ------------------------------------------------------------------ reachability (the limits of the CharacterController, with a margin)

        public const float WalkableStep = 0.6f; // metres of rise per metre of walk

        /// <summary>Flood fill over a 1 m grid from <paramref name="start"/> through points inside a shape (0.5 m from the wall) whose neighbouring heights differ by at most <see cref="WalkableStep"/>.</summary>
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
                    if (Mathf.Abs(h2 - h) <= WalkableStep && EdgeDistance(x - HalfX, z - HalfZ) <= -0.5f)
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
