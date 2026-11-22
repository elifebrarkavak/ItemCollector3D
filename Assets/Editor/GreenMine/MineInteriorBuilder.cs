using System.Collections.Generic;
using ItemCollector3D.Player;
using ItemCollector3D.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ItemCollector3D.EditorTools.GreenMine
{
    /// <summary>
    /// Builds the walkable mine (12 m tunnel + 8 x 10 m treasure room) as modular prefabs under GreenMineEnvironment/MineInterior and opens the old decorative
    /// entrance. It is NOT the disabled GREEN_MINE_GENERATOR: it has no menu item, never rebuilds the scene, TreasureSpots or any other environment group, and only
    /// replaces the MineInterior root (idempotent). Called once from the Editor; the scene and prefabs are then the source of truth.
    /// </summary>
    public static class MineInteriorBuilder
    {
        private const string ArtDir = "Assets/Art/GreenMine/";
        private const string PrefabDir = "Assets/Prefabs/GreenMine/MineInterior";
        private const string ScenePath = "Assets/Scenes/GreenMine.unity";

        // world layout (the portal of Mine_Entrance stands at x 6.3, z 14.5 and faces -Z; the tunnel runs towards +Z)
        private const float AxisX = 6.3f, FloorY = 0.08f, Wall = 0.5f;
        private const float TunnelW = 4.6f, TunnelH = 4.2f, TunnelZ0 = 14.5f, SegLen = 2.4f;
        private const int Segments = 5;                                   // 12 m
        private const float RoomW = 8f, RoomD = 10f, RoomH = 4.8f;       // interior; the room starts where the tunnel ends
        private static float TunnelZ1 => TunnelZ0 + SegLen * Segments;    // 26.5
        private static float RoomZ1 => TunnelZ1 + RoomD;                  // 36.5

        public static string Build()
        {
            var report = new List<string>();
            EnsureFolder();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            report.Add(PatchEntrance());
            report.Add(LowerTerrain());

            GameObject segment = BuildSegment();
            GameObject frame = BuildFrame("MineTimber_Frame", TunnelW, TunnelH);
            GameObject roomFrame = BuildFrame("MineTimber_FrameWide", RoomW, RoomH);
            GameObject lantern = BuildLantern();
            GameObject rail = BuildRail();
            GameObject chest = BuildChest();
            GameObject shell = BuildRoomShell();
            AssetDatabase.SaveAssets();

            Transform env = GameObject.Find("GreenMineEnvironment").transform;
            Transform old = env.Find("MineInterior");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var rootGo = new GameObject("MineInterior");
            rootGo.transform.SetParent(env, false);
            Transform root = rootGo.transform;
            Transform tunnel = Child(root, "Tunnel");
            Transform room = Child(root, "TreasureRoom");

            for (int i = 0; i < Segments; i++)
            {
                Place(segment, tunnel, new Vector3(AxisX, 0f, TunnelZ0 + SegLen * (i + 0.5f)), 0f, 1f, "MineTunnel_Segment_" + i);
            }

            Box(tunnel, "Threshold_Floor", new Vector3(AxisX, FloorY - 0.5f, TunnelZ0 - 0.25f), new Vector3(TunnelW, 1f, 0.5f), "mineFloor", true);
            for (int i = 0; i < Segments; i++)
            {
                float z = TunnelZ0 + 1.2f + SegLen * i;
                Place(frame, tunnel, new Vector3(AxisX, 0f, z), 0f, 1f, "MineTimber_Frame_" + i);
                if (i % 2 == 0)
                {
                    Place(lantern, tunnel, new Vector3(AxisX + (i == 2 ? 1.1f : -1.1f), FloorY + TunnelH - 0.4f, z), 0f, 1f, "MineLantern_" + i);
                }
            }

            for (int i = 0; i < 3; i++) // the old track runs in until z = 20.3: continue it to the room
            {
                Place(rail, tunnel, new Vector3(AxisX, 0f, 20.3f + SegLen * (i + 0.5f)), 0f, 1f, "MineRail_Segment_" + i);
            }

            Vector3 c = new Vector3(AxisX, 0f, TunnelZ1 + RoomD * 0.5f);
            Place(shell, room, c, 0f, 1f, "MineRoom_Shell");
            Place(roomFrame, room, new Vector3(AxisX, 0f, TunnelZ1 + 3.2f), 0f, 1f, "MineTimber_FrameWide_0");
            Place(lantern, room, new Vector3(AxisX, FloorY + RoomH - 0.4f, TunnelZ1 + 3.2f), 0f, 1f, "MineLantern_Room");
            Place(rail, room, new Vector3(AxisX, 0f, 20.3f + SegLen * 3.5f), 0f, 1f, "MineRail_Segment_3");

            // ---- props (existing prefabs, no new art): gold crystals along the north wall, chests, crates, barrels, a cart; the middle stays free for the detector
            Transform props = Child(room, "Props");
            Prefab("Rocks/Crystal_Cluster_L", props, new Vector3(3.9f, FloorY, 35.0f), 20f, 1.6f, "GoldCrystal_L_West", true);
            Prefab("Rocks/Crystal_Cluster_L", props, new Vector3(8.8f, FloorY, 35.0f), 200f, 1.7f, "GoldCrystal_L_East", true);
            Prefab("Rocks/Crystal_Cluster_M", props, new Vector3(6.3f, FloorY, 35.8f), 75f, 1.7f, "GoldCrystal_M_North", true);
            Prefab("Rocks/Crystal_Cluster_M", props, new Vector3(2.9f, FloorY, 33.2f), 140f, 1.5f, "GoldCrystal_M_WestWall", true);
            Prefab("Rocks/Crystal_Cluster_M", props, new Vector3(9.7f, FloorY, 33.6f), 310f, 1.4f, "GoldCrystal_M_EastWall", true);
            Prefab("Rocks/Crystal_Cluster_S", props, new Vector3(5.2f, FloorY, 34.1f), 15f, 1.3f, "GoldCrystal_S_A", false);
            Prefab("Rocks/Crystal_Cluster_S", props, new Vector3(7.5f, FloorY, 34.4f), 250f, 1.1f, "GoldCrystal_S_B", false);
            Prefab("Rocks/Rock_L_A", props, new Vector3(3.0f, FloorY - 0.1f, 36.0f), 40f, 0.7f, "Rock_L_A_NW", false);
            Prefab("Rocks/Rock_L_B", props, new Vector3(9.8f, FloorY - 0.1f, 36.0f), 130f, 0.7f, "Rock_L_B_NE", false);
            Prefab("Rocks/Rock_M_B", props, new Vector3(9.9f, FloorY - 0.1f, 27.2f), 200f, 0.6f, "Rock_M_B_SE", false);
            Prefab("Rocks/Rock_M_A", props, new Vector3(2.8f, FloorY - 0.1f, 27.3f), 80f, 0.6f, "Rock_M_A_SW", false);
            Prefab("Mine/Crate_Stack", props, new Vector3(3.1f, FloorY, 28.6f), 90f, 1f, "Crate_Stack_West", false);
            Prefab("Mine/Barrel", props, new Vector3(2.9f, FloorY, 30.2f), 0f, 1f, "Barrel_West_A", false);
            Prefab("Mine/Barrel", props, new Vector3(3.2f, FloorY, 31.0f), 30f, 1f, "Barrel_West_B", false);
            Prefab("Mine/Pickaxe_Leaning", props, new Vector3(2.6f, FloorY, 32.0f), 270f, 1f, "Pickaxe_West", false);
            PlaceAt(chest, props, new Vector3(3.3f, FloorY, 29.6f), 90f, 1f, "Chest_Wood_West");
            PlaceAt(chest, props, new Vector3(9.4f, FloorY, 31.3f), 270f, 1f, "Chest_Wood_East_A");
            PlaceAt(chest, props, new Vector3(9.5f, FloorY, 32.3f), 262f, 1f, "Chest_Wood_East_B");
            Prefab("Mine/MineCart_Gold", props, new Vector3(9.3f, FloorY, 29.0f), 90f, 1f, "MineCart_Gold_Room", false);
            Prefab("Mine/Barrel_Lantern", props, new Vector3(9.7f, FloorY, 27.6f), 0f, 1f, "Barrel_Lantern_East", false);

            // the gold glow: one warm light among the crystals (the lantern prefab brings the others)
            var glow = new GameObject("CrystalGlow");
            glow.transform.SetParent(Child(room, "Lights"), false);
            glow.transform.position = new Vector3(AxisX, 2.4f, 33.4f);
            var gl = glow.AddComponent<Light>();
            gl.type = LightType.Point;
            gl.color = new Color(1f, 0.74f, 0.30f);
            gl.intensity = 2.6f;
            gl.range = 10f;
            gl.shadows = LightShadows.None;

            PatchBoundaryWall();
            report.Add(SetupZone());

            EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
            EditorSceneManager.SaveScene(env.gameObject.scene);
            AssetDatabase.SaveAssets();
            return string.Join(" | ", report);
        }

        // ================================================================ entrance + terrain + wall patches

        private static string PatchEntrance()
        {
            // 1) the placeholder collider that closed the dark mouth (local centre 0, 2, -1)
            string path = "Assets/Prefabs/GreenMine/Mine/Mine_Entrance.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            int removed = 0;
            foreach (BoxCollider b in contents.GetComponents<BoxCollider>())
            {
                if (Mathf.Abs(b.center.x) < 0.01f && Mathf.Abs(b.center.y - 2f) < 0.01f && Mathf.Abs(b.center.z + 1f) < 0.01f)
                {
                    Object.DestroyImmediate(b);
                    removed++;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);

            // 2) the baked decorative tunnel (walls / ceiling / frames / bulbs) and every triangle of the rock that intrudes into the new tunnel volume
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ArtDir + "Meshes/Mine_Entrance.asset");
            var renderer = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<MeshRenderer>();
            Vector3[] v = mesh.vertices;
            int cut = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                string mat = renderer.sharedMaterials[s].name;
                bool all = mat == "GM_tunnel" || mat == "GM_tunnelWall" || mat == "GM_tunnelDeep";
                int[] t = mesh.GetIndices(s);
                var keep = new List<int>(t.Length);
                for (int i = 0; i < t.Length; i += 3)
                {
                    if (all || InCut(v[t[i]], v[t[i + 1]], v[t[i + 2]]))
                    {
                        cut++;
                        continue;
                    }

                    keep.Add(t[i]);
                    keep.Add(t[i + 1]);
                    keep.Add(t[i + 2]);
                }

                if (keep.Count != t.Length)
                {
                    mesh.SetIndices(keep, MeshTopology.Triangles, s);
                }
            }

            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return $"entrance: {removed} mouth collider removed, {cut} decor/rock triangles cut";
        }

        // entrance-local frame (+Z towards the player, the tunnel towards -Z): any triangle touching the tunnel / room volumes (plus their shell) is removed, so no
        // rock of the old mountain shows inside the new interior. Everything in front of the portal plane (z > -0.45) is untouched.
        private static bool InCut(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 lo = Vector3.Min(a, Vector3.Min(b, c)), hi = Vector3.Max(a, Vector3.Max(b, c));
            return Overlap(lo, hi, new Vector3(-2.6f, -0.3f, -12.1f), new Vector3(2.6f, 5.0f, -0.45f))
                || Overlap(lo, hi, new Vector3(-4.6f, -0.3f, -22.6f), new Vector3(4.6f, 5.6f, -11.9f));
        }

        private static bool Overlap(Vector3 lo, Vector3 hi, Vector3 bmin, Vector3 bmax) =>
            lo.x < bmax.x && hi.x > bmin.x && lo.y < bmax.y && hi.y > bmin.y && lo.z < bmax.z && hi.z > bmin.z;

        private static string LowerTerrain()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ArtDir + "Meshes/GreenMine_Terrain.asset");
            Vector3[] v = mesh.vertices;
            int n = 0;
            for (int i = 0; i < v.Length; i++)
            {
                float x = v[i].x, z = v[i].z;
                bool tunnelCut = x > 3.5f && x < 9.5f && z > 20.5f && z < 25.5f;
                bool roomCut = x > 1.5f && x < 11.5f && z > 25.5f && z < 37.5f;
                if ((tunnelCut || roomCut) && v[i].y > -3f)
                {
                    v[i].y = -3f;
                    n++;
                }
            }

            mesh.vertices = v;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var ground = GameObject.Find("GreenMineEnvironment").transform.Find("Terrain/Ground").GetComponent<MeshCollider>();
            ground.sharedMesh = null;
            ground.sharedMesh = mesh;
            return $"terrain: {n} vertices sunk under the tunnel / room";
        }

        private static void PatchBoundaryWall()
        {
            // Wall_2 (z 15.3) closed the mine: two boxes now leave the 6 m gap of the portal (the tunnel's own walls close it)
            Transform wall = GameObject.Find("GreenMineEnvironment").transform.Find("Boundaries/Wall_2");
            BoxCollider[] boxes = wall.GetComponents<BoxCollider>();
            BoxCollider a = boxes[0];
            BoxCollider b = boxes.Length > 1 ? boxes[1] : wall.gameObject.AddComponent<BoxCollider>();
            float x0 = wall.position.x;
            void Set(BoxCollider box, float from, float to)
            {
                box.center = new Vector3((from + to) * 0.5f - x0, 0f, 0.4f);
                box.size = new Vector3(to - from, 20f, 0.8f);
            }

            Set(a, -1.8f, 3.3f);
            Set(b, 9.3f, 19.9f);
        }

        private static string SetupZone()
        {
            Transform systems = GameObject.Find("GameplaySystems").transform;
            Transform zoneT = systems.Find("MineInteriorZone");
            if (zoneT == null)
            {
                zoneT = new GameObject("MineInteriorZone").transform;
                zoneT.SetParent(systems, false);
            }

            var existing = zoneT.GetComponent<MineInteriorZone>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing); // fresh defaults (mood colours) on every build
            }

            var zone = zoneT.gameObject.AddComponent<MineInteriorZone>();
            var so = new SerializedObject(zone);
            SerializedProperty list = so.FindProperty("interiorBounds");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).boundsValue = new Bounds(new Vector3(AxisX, 2.3f, (TunnelZ0 + TunnelZ1) * 0.5f), new Vector3(TunnelW, 4.6f, TunnelZ1 - TunnelZ0));
            list.GetArrayElementAtIndex(1).boundsValue = new Bounds(new Vector3(AxisX, 2.5f, (TunnelZ1 + RoomZ1) * 0.5f), new Vector3(RoomW, 5.0f, RoomD));
            so.FindProperty("cameraController").objectReferenceValue = Object.FindAnyObjectByType<ThirdPersonCameraController>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return "zone ok";
        }

        // ================================================================ modules (prefabs)

        private static GameObject BuildSegment()
        {
            var root = new GameObject("MineTunnel_Segment");
            float top = FloorY + TunnelH;
            Box(root.transform, "Floor", new Vector3(0f, FloorY - 0.5f, 0f), new Vector3(TunnelW + 2f * Wall, 1f, SegLen), "mineFloor", true);
            float wy = (top + 0.5f - 0.9f) * 0.5f, wh = top + 0.5f + 0.9f;
            Box(root.transform, "Wall_W", new Vector3(-(TunnelW + Wall) * 0.5f, wy, 0f), new Vector3(Wall, wh, SegLen), "mineRock", true);
            Box(root.transform, "Wall_E", new Vector3((TunnelW + Wall) * 0.5f, wy, 0f), new Vector3(Wall, wh, SegLen), "mineRock", true);
            Box(root.transform, "Ceiling", new Vector3(0f, top + 0.25f, 0f), new Vector3(TunnelW + 2f * Wall, 0.5f, SegLen), "tunnel", true);
            return Save(root);
        }

        private static GameObject BuildFrame(string name, float width, float height)
        {
            var root = new GameObject(name);
            float top = FloorY + height;
            for (int s = -1; s <= 1; s += 2)
            {
                Box(root.transform, "Post", new Vector3(s * (width * 0.5f - 0.2f), FloorY + height * 0.5f, 0f), new Vector3(0.4f, height, 0.4f), "wood", true);
                // a 45 degree gusset between the post and the cap beam (long axis = local z, turned into the x-y plane)
                Box(root.transform, "Brace", new Vector3(s * (width * 0.5f - 0.75f), top - 0.8f, 0f), new Vector3(0.2f, 0.2f, 1.3f), "woodDark", false,
                    Quaternion.LookRotation(new Vector3(-s * 0.7071f, 0.7071f, 0f), Vector3.forward));
            }

            Box(root.transform, "Beam", new Vector3(0f, top - 0.2f, 0f), new Vector3(width, 0.4f, 0.45f), "woodDark", true);
            return Save(root);
        }

        private static GameObject BuildLantern()
        {
            var root = new GameObject("MineLantern_Wall");
            Box(root.transform, "Chain", new Vector3(0f, -0.2f, 0f), new Vector3(0.03f, 0.4f, 0.03f), "iron", false);
            Box(root.transform, "Cap", new Vector3(0f, -0.43f, 0f), new Vector3(0.36f, 0.07f, 0.36f), "iron", false);
            Box(root.transform, "Glass", new Vector3(0f, -0.67f, 0f), new Vector3(0.26f, 0.4f, 0.26f), "mineLamp", false);
            Box(root.transform, "Base", new Vector3(0f, -0.92f, 0f), new Vector3(0.34f, 0.08f, 0.34f), "iron", false);
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.67f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.70f, 0.34f);
            l.intensity = 3.0f;
            l.range = 8f;
            l.shadows = LightShadows.None;
            return Save(root);
        }

        private static GameObject BuildRail()
        {
            var root = new GameObject("MineRail_Segment");
            for (int s = -1; s <= 1; s += 2)
            {
                Box(root.transform, "Rail", new Vector3(s * 0.5f, FloorY + 0.13f, 0f), new Vector3(0.07f, 0.09f, SegLen), "ironLight", false);
            }

            for (int i = 0; i < 5; i++)
            {
                Box(root.transform, "Sleeper", new Vector3(0f, FloorY + 0.04f, -SegLen * 0.5f + SegLen * (i + 0.5f) / 5f), new Vector3(1.5f, 0.08f, 0.2f), "woodDark", false);
            }

            return Save(root);
        }

        private static GameObject BuildChest()
        {
            var root = new GameObject("Chest_Wood");
            Box(root.transform, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 0.7f), "woodDark", true);
            Box(root.transform, "Lid", new Vector3(0f, 0.72f, 0f), new Vector3(1.16f, 0.26f, 0.76f), "wood", false);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(root.transform, "Strap", new Vector3(s * 0.38f, 0.45f, 0f), new Vector3(0.1f, 0.9f, 0.78f), "iron", false);
            }

            Box(root.transform, "Lock", new Vector3(0f, 0.62f, 0.38f), new Vector3(0.14f, 0.18f, 0.05f), "gold", false);
            return Save(root);
        }

        private static GameObject BuildRoomShell()
        {
            var root = new GameObject("MineRoom_Shell"); // origin: centre of the room floor
            Transform t = root.transform;
            float top = FloorY + RoomH, wy = (top + 0.5f - 0.9f) * 0.5f, wh = top + 0.5f + 0.9f;
            float hw = RoomW * 0.5f, hd = RoomD * 0.5f, door = TunnelW * 0.5f;
            Box(t, "Floor", new Vector3(0f, FloorY - 0.5f, 0f), new Vector3(RoomW + 2f * Wall, 1f, RoomD + 2f * Wall), "mineFloor", true);
            Box(t, "Ceiling", new Vector3(0f, top + 0.25f, 0f), new Vector3(RoomW + 2f * Wall, 0.5f, RoomD + 2f * Wall), "tunnel", true);
            Box(t, "Wall_W", new Vector3(-(hw + Wall * 0.5f), wy, 0f), new Vector3(Wall, wh, RoomD + 2f * Wall), "mineRock", true);
            Box(t, "Wall_E", new Vector3(hw + Wall * 0.5f, wy, 0f), new Vector3(Wall, wh, RoomD + 2f * Wall), "mineRock", true);
            Box(t, "Wall_N", new Vector3(0f, wy, hd + Wall * 0.5f), new Vector3(RoomW, wh, Wall), "mineRock", true);
            float side = hw - door;
            Box(t, "Wall_S_W", new Vector3(-(door + side * 0.5f), wy, -(hd + Wall * 0.5f)), new Vector3(side, wh, Wall), "mineRock", true);
            Box(t, "Wall_S_E", new Vector3(door + side * 0.5f, wy, -(hd + Wall * 0.5f)), new Vector3(side, wh, Wall), "mineRock", true);
            float lintelBottom = FloorY + TunnelH;
            Box(t, "Lintel_S", new Vector3(0f, (lintelBottom + top + 0.5f) * 0.5f, -(hd + Wall * 0.5f)), new Vector3(TunnelW, top + 0.5f - lintelBottom, Wall), "mineRock", true);
            return Save(root);
        }

        // ================================================================ helpers

        private static Material Mat(string n)
        {
            string path = ArtDir + "Materials/GM_" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null)
            {
                return m;
            }

            switch (n)
            {
                case "mineRock": return NewMat(path, "tunnelWall", new Color(0.36f, 0.28f, 0.25f), null);
                case "mineFloor": return NewMat(path, "tunnelWall", new Color(0.28f, 0.20f, 0.15f), null);
                case "mineLamp": return NewMat(path, "lanternGlass", new Color(1f, 0.84f, 0.46f), new Color(2.2f, 1.45f, 0.55f));
            }

            Debug.LogError("Missing material GM_" + n);
            return null;
        }

        private static Material NewMat(string path, string basedOn, Color color, Color? emission)
        {
            var m = new Material(AssetDatabase.LoadAssetAtPath<Material>(ArtDir + "Materials/GM_" + basedOn + ".mat"));
            m.SetColor("_BaseColor", color);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, string mat, bool collider, Quaternion? rotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            if (rotation.HasValue)
            {
                go.transform.localRotation = rotation.Value;
            }

            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mat(mat);
            r.shadowCastingMode = collider ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider)
            {
                go.AddComponent<BoxCollider>();
            }

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static GameObject Save(GameObject root)
        {
            string path = PrefabDir + "/" + root.name + ".prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return asset;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/GreenMine", "MineInterior");
            }
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
        }

        private static void PlaceAt(GameObject prefab, Transform parent, Vector3 pos, float yaw, float scale, string name) => Place(prefab, parent, pos, yaw, scale, name);

        private static void Prefab(string relative, Transform parent, Vector3 pos, float yaw, float scale, string name, bool collider)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GreenMine/" + relative + ".prefab");
            Place(prefab, parent, pos, yaw, scale, name);
            if (collider)
            {
                Transform t = parent.Find(name);
                var cgo = new GameObject(name + "_Collider");
                cgo.transform.SetParent(t, false);
                cgo.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                var box = cgo.AddComponent<BoxCollider>();
                box.size = new Vector3(1.1f, 2.0f, 1.1f); // local units: the instance scale applies
            }
        }
    }
}
