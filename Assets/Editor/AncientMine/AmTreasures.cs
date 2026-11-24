#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>
    /// Stage 16C: the 25 permanent Ancient Mine treasures (1240 gold) as DATA, and the tool that places them (same method as Rocky Canyon's RcTreasures). The existing treasure prefabs
    /// (Gold Coin / Gold Nugget / Gold Bar / Ancient Artifact = Common 10 / Uncommon 25 / Rare 50 / Epic 100 gold) are instantiated unchanged under the empty zone groups of the
    /// TreasureSpots root, each with an explicit unique id "ancient_mine.&lt;zone&gt;_NN". Nothing else in the scene is touched; running it again replaces only the treasures (idempotent).
    /// Points come from the 16B TreasureCandidates markers (the Y is the floor surface plus the marker lift, so the three Royal Vault pedestals keep their top), plus two extra
    /// Crystal Caverns points chosen with <see cref="Clearance"/>.
    /// </summary>
    public static class AmTreasures
    {
        public const string AreaId = "ancient_mine";
        public const string TreasureRootName = "TreasureSpots";

        public enum Kind
        {
            Common,
            Uncommon,
            Rare,
            Epic,
        }

        public readonly struct Entry
        {
            public readonly string Id;
            public readonly string Group;
            public readonly Kind Kind;
            public readonly float X, Z, Lift, Yaw;

            public Entry(string id, string group, Kind kind, float x, float z, float lift, float yaw)
            {
                Id = id;
                Group = group;
                Kind = kind;
                X = x;
                Z = z;
                Lift = lift;
                Yaw = yaw;
            }
        }

        private const string A = "A_AncientGate", B = "B_HallOfPillars", C = "C_CrystalCaverns", D = "D_ForgottenWorkshop", E = "E_RoyalVault";

        public static readonly Entry[] Table = BuildTable();

        private static Entry[] BuildTable()
        {
            return new[]
            {
                // A  Ancient Gate: 2 Common + 1 Uncommon = 45 (easy: by the old entrance stones in the hall, the third one by the west wall beside the portal; no Epic at the start)
                new Entry("ancient_mine.gate_01", A, Kind.Common, -49.5f, 0.8f, 0f, 20f),
                new Entry("ancient_mine.gate_02", A, Kind.Common, -49.0f, -7.0f, 0f, 140f),
                new Entry("ancient_mine.gate_03", A, Kind.Uncommon, -56.0f, 2.0f, 0f, 75f),

                // B  Hall of Pillars: 1 Common + 2 Uncommon + 2 Rare = 160 (the two pillar aisles, then the corner daises: the Rares are on the far west and south-west platforms)
                new Entry("ancient_mine.hall_01", B, Kind.Common, -16.0f, -8.5f, 0f, 200f),
                new Entry("ancient_mine.hall_02", B, Kind.Uncommon, 20.0f, 17.0f, 0f, 310f),
                new Entry("ancient_mine.hall_03", B, Kind.Uncommon, 16.0f, 8.5f, 0f, 95f),
                new Entry("ancient_mine.hall_04", B, Kind.Rare, -20.0f, -17.0f, 0f, 250f),
                new Entry("ancient_mine.hall_05", B, Kind.Rare, -20.0f, 17.0f, 0f, 30f),

                // C  Crystal Caverns: 1 Common + 1 Uncommon + 3 Rare + 2 Epic = 385 (a pocket south of the atrium, the big atrium formation, the north branch, both lobes; the Epics are in the grotto and at the far end of the north branch, both off the main route; the south lobe holds Rares)
                new Entry("ancient_mine.crystal_01", C, Kind.Common, 54.0f, -10.0f, 0f, 60f),
                new Entry("ancient_mine.crystal_02", C, Kind.Uncommon, 45.5f, 2.5f, 0f, 170f),
                new Entry("ancient_mine.crystal_03", C, Kind.Rare, 52.0f, 11.0f, 0f, 280f),
                new Entry("ancient_mine.crystal_04", C, Kind.Epic, 43.0f, 36.0f, 0f, 10f),
                new Entry("ancient_mine.crystal_05", C, Kind.Rare, 44.5f, -33.5f, 0f, 120f),
                new Entry("ancient_mine.crystal_06", C, Kind.Epic, 36.0f, 14.0f, 0f, 45f),
                new Entry("ancient_mine.crystal_07", C, Kind.Rare, 43.5f, -40.0f, 0f, 215f),

                // D  Forgotten Workshop: 2 Uncommon + 2 Rare + 1 Epic = 250 (the work hall, the old working floor, the collapsed spur, and the Epic in the cache chamber at the end of the west tunnel)
                new Entry("ancient_mine.workshop_01", D, Kind.Uncommon, -5.5f, 43.5f, 0f, 330f),
                new Entry("ancient_mine.workshop_02", D, Kind.Uncommon, -17.0f, 38.0f, 0f, 90f),
                new Entry("ancient_mine.workshop_03", D, Kind.Rare, -24.5f, 41.8f, 0f, 160f),
                new Entry("ancient_mine.workshop_04", D, Kind.Rare, -33.5f, 18.5f, 0f, 235f),
                new Entry("ancient_mine.workshop_05", D, Kind.Epic, -51.5f, 45.2f, 0f, 300f),

                // E  Royal Vault: 3 Epic + 2 Rare = 400 (an Epic on the dais beside the altar; one in the centre of the gold ring in front of each alcove: Epic, Rare, Epic; a Rare by the giant crystal)
                new Entry("ancient_mine.vault_01", E, Kind.Epic, 15.0f, -35.3f, 0f, 20f),
                new Entry("ancient_mine.vault_02", E, Kind.Epic, 0.0f, -46.2f, 0f, 100f),
                new Entry("ancient_mine.vault_03", E, Kind.Rare, 10.0f, -46.2f, 0f, 200f),
                new Entry("ancient_mine.vault_04", E, Kind.Epic, 20.0f, -46.2f, 0f, 260f),
                new Entry("ancient_mine.vault_05", E, Kind.Rare, -2.0f, -33.5f, 0f, 340f),
            };
        }

        public static string PrefabPath(Kind kind)
        {
            switch (kind)
            {
                case Kind.Common: return "Assets/Prefabs/Treasure/Treasure_GoldCoin.prefab";
                case Kind.Uncommon: return "Assets/Prefabs/Treasure/Treasure_GoldNugget.prefab";
                case Kind.Rare: return "Assets/Prefabs/Treasure/Treasure_GoldBar.prefab";
                default: return "Assets/Prefabs/Treasure/Treasure_AncientArtifact.prefab";
            }
        }

        public static int Gold(Kind kind) => kind == Kind.Common ? 10 : kind == Kind.Uncommon ? 25 : kind == Kind.Rare ? 50 : 100;

        private static bool IsShell(Collider c) => c.name == "Floor" || c.name == "Ceiling" || c.name == "SafetyFloor";

        /// <summary>Radius of free space round a point on the floor, measured against the decorations only (not the cave shell): the smallest sphere (0.1 m steps, up to 4 m) that touches a prop collider at 0.5 / 1.4 m above the floor.</summary>
        public static float Clearance(float x, float z, float lift = 0f)
        {
            Physics.SyncTransforms();
            float y = AmLayout.Surface(x, z) + lift;
            float best = 4f;
            foreach (float h in new[] { 0.15f, 0.8f, 1.5f })
            {
                var centre = new Vector3(x, y + h, z);
                for (float r = 0.1f; r < best; r += 0.1f)
                {
                    bool hit = false;
                    foreach (Collider c in Physics.OverlapSphere(centre, r, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (!IsShell(c) && c.transform.root.name != "Player")
                        {
                            hit = true;
                            break;
                        }
                    }

                    if (hit)
                    {
                        best = r;
                        break;
                    }
                }
            }

            return best;
        }

        /// <summary>Diagnostic: one line per treasure candidate and per table entry (floor, zone, wall distance, prop clearance).</summary>
        public static string Scan()
        {
            var sb = new StringBuilder();
            foreach ((string id, Vector2 p, float lift) in AmLayout.TreasureCandidates)
            {
                sb.AppendLine($"cand {id,-16} ({p.x,6:F1},{p.y,6:F1}) floor {AmLayout.Surface(p.x, p.y):F2} lift {lift:F2} wall {-AmLayout.EdgeDistance(p.x, p.y):F1} m clear {Clearance(p.x, p.y, lift):F1} m slope {AmLayout.Slope(p.x, p.y):F2} route {RouteDistance(p.x, p.y):F1}");
            }

            return sb.ToString();
        }

        /// <summary>Horizontal distance from (x, z) to the nearest vertex of the merged decoration meshes (Structures / Crystals) between 0.15 m and 2 m above the floor (flat inlays and rings on the floor are not obstacles) (the test uses 0.7 m).</summary>
        public static float DecorGap(float x, float z, float lift = 0f)
        {
            float y = AmLayout.Surface(x, z) + lift;
            float best = 99f;
            foreach (MeshFilter f in GameObject.Find("AncientMineEnvironment").GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.name != "Structures" && f.name != "Crystals")
                {
                    continue;
                }

                foreach (Vector3 local in f.sharedMesh.vertices)
                {
                    Vector3 v = f.transform.TransformPoint(local);
                    if (v.y >= y + 0.15f && v.y <= y + 2f)
                    {
                        best = Mathf.Min(best, new Vector2(v.x - x, v.z - z).magnitude);
                    }
                }
            }

            return best;
        }

        /// <summary>Distance from (x, z) to the nearest main / alternate / side route polyline.</summary>
        public static float RouteDistance(float x, float z)
        {
            float best = float.MaxValue;
            foreach ((string _, Vector2[] pts) in AmLayout.AllRoutes())
            {
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1], ab = b - a, ap = new Vector2(x, z) - a;
                    float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude);
                    best = Mathf.Min(best, (a + ab * t - new Vector2(x, z)).magnitude);
                }
            }

            return best;
        }

        /// <summary>Diagnostic grid search: free points (wall >= minWall, clearance >= minClear) of a zone every <paramref name="step"/> metres.</summary>
        public static string Find(AmZone zone, float minWall, float minClear, float step)
        {
            var sb = new StringBuilder();
            for (float x = -62f; x <= 62f; x += step)
            {
                for (float z = -53f; z <= 51f; z += step)
                {
                    if (AmLayout.ZoneAt(x, z) != zone || -AmLayout.EdgeDistance(x, z) < minWall || AmLayout.Slope(x, z) > 0.3f)
                    {
                        continue;
                    }

                    float c = Clearance(x, z);
                    if (c >= minClear)
                    {
                        sb.AppendLine($"({x:F1},{z:F1}) floor {AmLayout.Surface(x, z):F2} wall {-AmLayout.EdgeDistance(x, z):F1} clear {c:F1} route {RouteDistance(x, z):F1}");
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>Replaces the treasures under the zone groups of the scene's TreasureSpots root with the table above. Does not save.</summary>
        public static string PlaceAll(Scene scene)
        {
            GameObject root = null;
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name == TreasureRootName)
                {
                    root = r;
                }
            }

            if (root == null)
            {
                return "TreasureSpots root not found.";
            }

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform group = root.transform.GetChild(i);
                for (int k = group.childCount - 1; k >= 0; k--)
                {
                    Object.DestroyImmediate(group.GetChild(k).gameObject);
                }
            }

            var prefabs = new Dictionary<Kind, GameObject>();
            int total = 0;
            foreach (Entry e in Table)
            {
                if (!prefabs.TryGetValue(e.Kind, out GameObject prefab))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(e.Kind));
                    prefabs[e.Kind] = prefab;
                }

                Transform group = root.transform.Find(e.Group);
                if (group == null)
                {
                    return "Zone group missing under TreasureSpots: " + e.Group;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                instance.name = "Treasure_" + e.Id.Substring(AreaId.Length + 1);
                instance.transform.SetPositionAndRotation(new Vector3(e.X, AmLayout.Surface(e.X, e.Z) + e.Lift, e.Z), Quaternion.Euler(0f, e.Yaw, 0f));
                var spot = instance.GetComponent<ItemCollector3D.Treasure.TreasureSpot>();
                var so = new SerializedObject(spot);
                so.FindProperty("spotId").stringValue = e.Id;
                so.ApplyModifiedPropertiesWithoutUndo();
                total += Gold(e.Kind);
            }

            return $"{Table.Length} treasures placed, {total} gold";
        }
    }
}
#endif
