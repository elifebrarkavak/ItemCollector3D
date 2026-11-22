#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>
    /// Stage 15C: the 20 permanent Rocky Canyon treasures (840 gold) as DATA, and the tool that places them. The existing treasure prefabs (Gold Coin / Gold Nugget / Gold Bar / Ancient Artifact
    /// = Common 10 / Uncommon 25 / Rare 50 / Epic 100 gold) are instantiated unchanged under the empty zone groups of the TreasureSpots root, each with an explicit, unique id
    /// "rocky_canyon.&lt;zone&gt;_NN" (Green Mine's are "green_mine.*"). Nothing else in the scene is touched; running it again replaces only the treasures (idempotent).
    /// The coordinates were chosen on the real 15B geometry: flat (slope &lt;= 0.25), inside a floor, at least 1.3 m from any collider and about 1 m from any decoration vertex, reachable on foot.
    /// </summary>
    public static class RcTreasures
    {
        public const string AreaId = "rocky_canyon";
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
            public readonly float X, Z;
            public readonly float Yaw;

            public Entry(string id, string group, Kind kind, float x, float z, float yaw)
            {
                Id = id;
                Group = group;
                Kind = kind;
                X = x;
                Z = z;
                Yaw = yaw;
            }
        }

        private const string A = "A_DesertOutpost", B = "B_DryRiverbed", C = "C_OldRailway", D = "D_CanyonOverlook", E = "E_HiddenRavine";

        public static readonly Entry[] Table =
        {
            // A  Desert Outpost: 2 Common + 1 Uncommon = 45 (easy ones: by the crates west of the shack, by the water tower, between the fire and the supply shed)
            new("rocky_canyon.outpost_01", A, Kind.Common, -42.30f, -26.25f, 20f),
            new("rocky_canyon.outpost_02", A, Kind.Common, -37.40f, -17.40f, 140f),
            new("rocky_canyon.outpost_03", A, Kind.Uncommon, -28.05f, -26.25f, 75f),

            // B  Dry Riverbed: 1 Common + 2 Uncommon + 2 Rare = 160 (spread over the whole bed: the wreck, the narrow middle, the old shaft, the boarded mine entrance, the far east)
            new("rocky_canyon.riverbed_01", B, Kind.Common, -11.50f, -21.75f, 200f),
            new("rocky_canyon.riverbed_02", B, Kind.Uncommon, -3.00f, -19.00f, 310f),
            new("rocky_canyon.riverbed_03", B, Kind.Uncommon, 19.85f, -7.20f, 95f),
            new("rocky_canyon.riverbed_04", B, Kind.Rare, 15.50f, -14.75f, 250f),
            new("rocky_canyon.riverbed_05", B, Kind.Rare, 31.00f, 3.80f, 30f),

            // C  Old Railway: 1 Common + 1 Uncommon + 2 Rare + 1 Epic = 235 (west track, by the loading station, by the crane; the Epic in the open gorge just south of the bridge, with the bridge as its backdrop; a Rare in the gorge's quiet dead end behind the bridge)
            new("rocky_canyon.railway_01", C, Kind.Common, -22.00f, 11.30f, 60f),
            new("rocky_canyon.railway_02", C, Kind.Uncommon, 19.25f, 16.65f, 170f),
            new("rocky_canyon.railway_03", C, Kind.Epic, 3.30f, 11.35f, 280f),
            new("rocky_canyon.railway_04", C, Kind.Rare, 24.75f, 22.90f, 10f),
            new("rocky_canyon.railway_05", C, Kind.Rare, 2.75f, 23.60f, 120f),

            // D  Canyon Overlook: 1 Uncommon + 1 Rare + 1 Epic = 175 (along the climb; the Epic on the summit plateau, behind the lookout deck, well inside its rim)
            new("rocky_canyon.overlook_01", D, Kind.Uncommon, 47.60f, 22.50f, 45f),
            new("rocky_canyon.overlook_02", D, Kind.Rare, 36.75f, 25.75f, 215f),
            new("rocky_canyon.overlook_03", D, Kind.Epic, 45.50f, 38.25f, 330f),

            // E  Hidden Ravine: 1 Uncommon + 2 Rare + 1 Epic = 225 (from the gate towards the pocket; the Epic at the back of the pocket)
            new("rocky_canyon.ravine_01", E, Kind.Uncommon, -4.50f, -10.50f, 90f),
            new("rocky_canyon.ravine_02", E, Kind.Rare, -6.30f, -5.70f, 160f),
            new("rocky_canyon.ravine_03", E, Kind.Rare, -9.25f, -0.55f, 235f),
            new("rocky_canyon.ravine_04", E, Kind.Epic, -18.55f, 2.60f, 300f),
        };

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

        /// <summary>Replaces the treasures under the zone groups of the scene's TreasureSpots root with the table above (and removes the 15A scaffolding group "_TestPoints"). Does not save.</summary>
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

            RcLayout.ResetGrid();
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform group = root.transform.GetChild(i);
                if (group.name == "_TestPoints")
                {
                    Object.DestroyImmediate(group.gameObject);
                    continue;
                }

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
                instance.transform.SetPositionAndRotation(new Vector3(e.X, RcLayout.Surface(e.X, e.Z), e.Z), Quaternion.Euler(0f, e.Yaw, 0f));
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
