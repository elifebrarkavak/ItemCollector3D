// ====================================================================================================================================================
// GREEN MINE GENERATOR - DISABLED BY DEFAULT (compiled only with the scripting define GREEN_MINE_GENERATOR).
//
// This editor code was a one-time bootstrap: it generated Assets/Scenes/GreenMine.unity, the prop prefabs (Assets/Prefabs/GreenMine) and the meshes,
// materials and textures (Assets/Art/GreenMine). Those assets are now the source of truth and are edited by hand (Beyza: environment, Emre: TreasureSpots).
// Running the generator REPLACES them and the scene, so it is off: no menu items, no public entry points, nothing a script can call by accident.
//
// Only to regenerate from scratch on purpose: commit / back up first, add `-define:GREEN_MINE_GENERATOR` to an Assets/csc.rsp (or the Scripting Define
// Symbols), expect everything above to be overwritten, and remove the define again afterwards.
// ====================================================================================================================================================
#if GREEN_MINE_GENERATOR
using System;
using System.Collections.Generic;
using System.IO;
using ItemCollector3D.Player;
using ItemCollector3D.Treasure;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static ItemCollector3D.EditorTools.GreenMine.GmLayout;
using Object = UnityEngine.Object;

namespace ItemCollector3D.EditorTools.GreenMine
{
    /// <summary>
    /// Bootstrap generator of the GreenMine scene. It builds the low-poly props as prefabs (Assets/Prefabs/GreenMine), the terrain / river meshes and
    /// textures (Assets/Art/GreenMine), and a <c>GreenMine.unity</c> scene that is a copy of GameplayTest (so the player, camera, HUD, joystick,
    /// detector, DIG / COLLECT, progress and pause wiring are reused as they are) with the test ground replaced by four separate roots:
    /// <c>Player</c>, <c>GameplaySystems</c>, <c>GreenMineEnvironment</c> and <c>TreasureSpots</c>.
    /// "Rebuild Environment Only" regenerates just <c>GreenMineEnvironment</c> and <c>TreasureSpots</c>; once the art team edits the scene by hand, use that
    /// (or nothing) rather than "Create Scene", which starts again from GameplayTest.
    /// </summary>
    public static class GreenMineSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/GreenMine.unity";
        public const string TemplatePath = "Assets/Scenes/GameplayTest.unity";
        public const string EnvironmentRoot = "GreenMineEnvironment";
        public const string SpotsRoot = "TreasureSpots";
        public const string SystemsRoot = "GameplaySystems";
        private const int IgnoreRaycastLayer = 2;

        // ---- placement state (rebuilt every run)
        private static System.Random rng;
        private static readonly List<(Vector2 p, float r)> Solids = new();

        // ================================================================ menu entry points

        [MenuItem("ItemCollector3D/Green Mine/Reset Scene From GameplayTest (discards manual edits)")]
        private static void MenuReset()
        {
            if (EditorUtility.DisplayDialog("Green Mine - DESTRUCTIVE",
                    "This REPLACES GreenMine.unity with a fresh copy of GameplayTest and regenerates every generated prop prefab, mesh, material and texture " +
                    "(Assets/Prefabs/GreenMine, Assets/Art/GreenMine).\n\nEvery manual edit is lost: environment, TreasureSpots (treasure ids / types / positions), " +
                    "HUD, camera, lighting.\n\nThe previous scene is copied to Library/GreenMineBackups first (local only, never committed). Continue?", "Reset", "Cancel"))
            {
                BuildEverything();
            }
        }

        [MenuItem("ItemCollector3D/Green Mine/Rebuild Environment Only (discards manual environment edits)")]
        private static void MenuRebuildEnvironment()
        {
            if (EditorUtility.DisplayDialog("Green Mine - DESTRUCTIVE",
                    "This regenerates GreenMineEnvironment AND TreasureSpots from code and overwrites every generated prop prefab, mesh, material and texture " +
                    "(Assets/Prefabs/GreenMine, Assets/Art/GreenMine).\n\nManual edits under those roots and to those assets are lost, including changed treasure " +
                    "positions / types / ids. The player, camera, HUD and other systems are kept.\n\nThe previous scene is copied to Library/GreenMineBackups first " +
                    "(local only, never committed). Continue?", "Rebuild", "Cancel"))
            {
                RebuildEnvironment();
            }
        }

        /// <summary>Props, meshes, textures, materials, then a fresh scene copied from GameplayTest. Also the batch / CLI entry point.</summary>
        public static void BuildEverything()
        {
            BuildAssets();
            BuildScene(true);
        }

        public static void RebuildEnvironment()
        {
            BuildAssets();
            BuildScene(false);
        }

        // ================================================================ assets

        public static void BuildAssets()
        {
            Reset();
            Directory.CreateDirectory(GmTerrain.TextureFolder);
            Texture2D color = GmTerrain.PaintColorMap();
            Texture2D waterTex = GmTerrain.WaterTexture();
            Texture2D fallTex = GmTerrain.WaterfallTexture();
            GmTerrain.GlowTexture();
            GmMats.Textured("Ground", color);
            GmMats.Transparent("Water", waterTex, Color.white, 0.55f);
            GmMats.Transparent("Waterfall", fallTex, Color.white, 0.4f, new Color(0.18f, 0.22f, 0.26f));
            GmMats.Sky(GmTerrain.SkyTexture());
            GmProps.BuildAll();
            GmProps.BackdropGround(700f, 0f);
            MakeFarVariants();
            GmTerrain.BuildMesh();
            GmTerrain.BuildRiverMesh();
            GmTerrain.BuildWaterfallMesh(FallPos.x, FallPos.y - 0.2f, FallPos.y + 4.8f, 1.7f);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void Reset()
        {
            GmLayout.Reset();
            GmTerrain.Reset();
            Solids.Clear();
            rng = new System.Random(20251121);
        }

        /// <summary>Collider-free copies of the pines for the rim forest beyond the invisible walls (never reachable, so no physics cost).</summary>
        private static void MakeFarVariants()
        {
            foreach (string source in new[] { "Pine_S", "Pine_M", "Pine_L", "Pine_M_Light" })
            {
                string path = AssetDatabase.GetAssetPath(GmProps.Get(source));
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(GmProps.Get(source));
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (Collider c in instance.GetComponents<Collider>())
                {
                    Object.DestroyImmediate(c);
                }

                instance.name = source + "_Far";
                PrefabUtility.SaveAsPrefabAsset(instance, Path.GetDirectoryName(path).Replace('\\', '/') + "/" + instance.name + ".prefab");
                Object.DestroyImmediate(instance);
            }

            AssetDatabase.Refresh();
        }

        // ================================================================ scene

        public static void BuildScene(bool fresh)
        {
            Reset();
            if (SceneManager.GetActiveScene().isDirty)
            {
                EditorSceneManager.SaveOpenScenes();
            }

            bool create = fresh || !File.Exists(ScenePath);
            BackupExistingScene();
            if (create)
            {
                // overwrite the file in place: the scene keeps its GUID (and Build Settings entries) across rebuilds
                File.Copy(TemplatePath, ScenePath, true);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // leftovers of the test scene
            DestroyRoot(scene, "Ground");
            DestroyRoot(scene, "Treasure_GoldCoin_01");
            DestroyRoot(scene, EnvironmentRoot);
            DestroyRoot(scene, SpotsRoot);

            Transform systems = GroupSystems(scene);
            Transform env = new GameObject(EnvironmentRoot).transform;
            Transform spots = new GameObject(SpotsRoot).transform;
            SceneManager.MoveGameObjectToScene(env.gameObject, scene);
            SceneManager.MoveGameObjectToScene(spots.gameObject, scene);

            BuildEnvironment(env, scene);
            BuildSpots(spots);
            if (create)
            {
                ConfigurePlayerAndCamera(scene, systems); // only on a fresh scene: later hand edits of the player / camera are kept
            }

            ConfigureLighting(scene, env);
            AddToBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"GreenMine: scene built. {Describe(scene)}");
        }

        /// <summary>Copies the current GreenMine.unity to Library/GreenMineBackups before it is overwritten or regenerated (local safety net only: Library is git-ignored).</summary>
        private static void BackupExistingScene()
        {
            if (!File.Exists(ScenePath))
            {
                return;
            }

            string folder = "Library/GreenMineBackups";
            Directory.CreateDirectory(folder);
            string target = $"{folder}/GreenMine_{DateTime.Now:yyyyMMdd_HHmmss}.unity";
            File.Copy(ScenePath, target, true);
            Debug.Log("GreenMine: previous scene backed up to " + target);
        }

        private static void DestroyRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        /// <summary>Gameplay systems (camera, UI, event system, treasure / feedback / progress / music) under one root, separate from Player and Environment.</summary>
        private static Transform GroupSystems(Scene scene)
        {
            GameObject existing = FindRoot(scene, SystemsRoot);
            if (existing != null)
            {
                return existing.transform;
            }

            var root = new GameObject(SystemsRoot);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (string name in new[] { "Main Camera", "UI Canvas", "EventSystem", "TreasureSystems", "RewardFeedback", "DigFeedback", "ProgressManager", "AmbientMusic" })
            {
                GameObject go = FindRoot(scene, name);
                if (go != null)
                {
                    go.transform.SetParent(root.transform, true);
                }
                else
                {
                    Debug.LogWarning("GreenMine: expected root '" + name + "' not found in the template scene.");
                }
            }

            return root.transform;
        }

        // ================================================================ environment

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Inst(string prefab, Transform parent, float x, float z, float yaw = 0f, float scale = 1f, float dy = 0f)
        {
            GameObject source = GmProps.Get(prefab);
            if (source == null)
            {
                Debug.LogError("GreenMine: missing prefab " + prefab);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            go.transform.SetPositionAndRotation(new Vector3(x, GmTerrain.SurfaceHeight(x, z) + dy, z), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static float YawFacing(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;     // local +Z -> dir
        private static float YawPlusX(Vector2 dir) => -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;     // local +X -> dir

        private static float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static void BuildEnvironment(Transform env, Scene scene)
        {
            Transform terrain = Group(env, "Terrain");
            Transform water = Group(env, "Water");
            Transform path = Group(env, "Path");
            Transform camp = Group(env, "StartCamp");
            Transform mine = Group(env, "MineEntrance");
            Transform forest = Group(env, "Forest");
            Transform meadow = Group(env, "Meadow");
            Transform rocky = Group(env, "RockyArea");
            Transform perimeter = Group(env, "Perimeter");
            Transform vegetation = Group(env, "Vegetation");
            Transform backdrop = Group(env, "Backdrop");
            Group(env, "Lighting");
            Transform bounds = Group(env, "Boundaries");
            Transform markers = Group(env, "Markers");

            // ---- terrain (one mesh, one texture, one mesh collider)
            var ground = new GameObject("Ground");
            ground.transform.SetParent(terrain, false);
            ground.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(GmTerrain.MeshFolder + "/GreenMine_Terrain.asset");
            var gr = ground.AddComponent<MeshRenderer>();
            gr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GmMats.Folder + "/GM_Ground.mat");
            gr.lightProbeUsage = LightProbeUsage.Off;
            gr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            ground.AddComponent<MeshCollider>().sharedMesh = gr.GetComponent<MeshFilter>().sharedMesh;
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            BuildWater(water);
            BuildPathMarkers(path, markers);
            BuildCamp(camp);
            BuildMine(mine);
            BuildForest(forest, perimeter);
            BuildMeadowAndRocks(meadow, rocky);
            BuildVegetation(vegetation);
            BuildBackdrop(backdrop);
            BuildBoundaries(bounds);
        }

        // ---- water: river ribbon, waterfall ribbon, foam, bridge
        private static void BuildWater(Transform water)
        {
            Material waterMat = AssetDatabase.LoadAssetAtPath<Material>(GmMats.Folder + "/GM_Water.mat");
            Material fallMat = AssetDatabase.LoadAssetAtPath<Material>(GmMats.Folder + "/GM_Waterfall.mat");
            MakeRenderer(water, "River", AssetDatabase.LoadAssetAtPath<Mesh>(GmTerrain.MeshFolder + "/GreenMine_River.asset"), waterMat, false);
            MakeRenderer(water, "Waterfall", AssetDatabase.LoadAssetAtPath<Mesh>(GmTerrain.MeshFolder + "/GreenMine_Waterfall.asset"), fallMat, false);
            Inst("Foam_Pool", water, FallPos.x, FallPos.y - 0.7f, 0f, 1.3f, WaterLevel + 0.02f - GmTerrain.SurfaceHeight(FallPos.x, FallPos.y - 0.7f));
            // the deck stands on the BANKS (the shallow river bed in the middle is below the water): its origin is the bank height, not the bed height
            float bank = Mathf.Max(GmTerrain.SurfaceHeight(BridgePos.x - 4.2f, BridgePos.y), GmTerrain.SurfaceHeight(BridgePos.x + 4.2f, BridgePos.y));
            Inst("Bridge_Wood", water, BridgePos.x, BridgePos.y, 0f, 1f, bank - GmTerrain.SurfaceHeight(BridgePos.x, BridgePos.y));
        }

        private static GameObject MakeRenderer(Transform parent, string name, Mesh mesh, Material material, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ---- waypoints / zone markers (for Beyza, Emre and the tests; no code reads them at runtime)
        public static readonly (string name, Vector2 pos)[] WalkTour =
        {
            ("Walk_00_Spawn", SpawnPos),
            ("Walk_01_PathJunction", new Vector2(-5.0f, -7.8f)),
            ("Walk_02_WestMeadow", new Vector2(-11.4f, -1.2f)),
            ("Walk_03_ForestEdge", new Vector2(-11.0f, 4.2f)),
            ("Walk_04_NorthMeadow", new Vector2(-1.6f, 8.4f)),
            ("Walk_05_MineApron", new Vector2(5.9f, 11.8f)),
            ("Walk_06_EastReturn", new Vector2(9.6f, 7.7f)),
            ("Walk_07_BridgeTrail", new Vector2(9.2f, 4.2f)),
            ("Walk_08_RockyTrailStart", new Vector2(5.0f, -1.0f)),
            ("Walk_09_RockyClearing", new Vector2(12.0f, -9.4f)),
            ("Walk_10_BackToJunction", new Vector2(-3.6f, -5.0f)),
            ("Walk_11_ForestTrailEnd", new Vector2(-14.6f, 9.0f)),
        };

        private static void BuildPathMarkers(Transform path, Transform markers)
        {
            Transform walk = Group(path, "TourWaypoints");
            foreach ((string name, Vector2 pos) in WalkTour)
            {
                var go = new GameObject(name);
                go.transform.SetParent(walk, false);
                go.transform.position = GmTerrain.Ground(pos.x, pos.y);
            }

            Marker(markers, "Zone_StartCamp", CampCenter, 3);
            Marker(markers, "Zone_MineEntrance", ApronCenter, 4);
            Marker(markers, "Zone_Forest", ForestCenter, 2);
            Marker(markers, "Zone_RockyTreasureArea", RockyCenter, 5);
            Marker(markers, "Zone_Meadow", MeadowRocks, 6);
            Marker(markers, "Zone_Waterfall", new Vector2(FallPos.x, FallPos.y - 1f), 1);
        }

        private static void Marker(Transform parent, string name, Vector2 pos, int icon)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = GmTerrain.Ground(pos.x, pos.y) + Vector3.up * 1.5f;
            var texture = EditorGUIUtility.IconContent("sv_label_" + icon).image as Texture2D;
            if (texture != null)
            {
                EditorGUIUtility.SetIconForObject(go, texture);
            }
        }

        // ---- start camp
        private static void BuildCamp(Transform camp)
        {
            Vector2 toFire = FirePos - TentPos;
            Inst("Tent_Camp", camp, TentPos.x, TentPos.y, YawFacing(toFire));
            Inst("Campfire", camp, FirePos.x, FirePos.y);
            Inst("Notice_Board", camp, -8.4f, -7.9f, 155f);
            Inst("Camp_Stall", camp, -14.6f, -14.4f, 40f);
            Inst("Bedroll_Pack", camp, -12.2f, -10.4f, 20f);
            Inst("WoodPile", camp, -17.2f, -11.8f, 90f);
            Inst("Crate_Stack", camp, -16.6f, -7.6f, 25f);
            Inst("Barrel_Lantern", camp, -11.6f, -15.2f, 0f);
            Inst("Barrel", camp, -12.6f, -15.6f, 0f);
            Inst("Lantern_Post", camp, -8.2f, -13.4f, 200f);
            Inst("Lantern_Post", camp, -10.6f, -7.4f, 10f);
            Inst("Lantern_Post", camp, -6.6f, -8.5f, 180f);

            // sign at the first junction: shows the way to the mine
            Vector2 toMine = MineFront - new Vector2(-3.4f, -7.0f);
            Inst("Signpost_Mine", camp, -3.5f, -7.2f, YawPlusX(toMine.normalized));

            // start gate on the south edge, fences along the south and west edges
            Inst("Gate_Arch", camp, GatePos.x, GatePos.y, 0f);
            for (float x = -18.6f; x < -6.8f; x += 2.4f)
            {
                Inst("Fence_Section", camp, x, -18.2f, 0f);
            }

            Inst("Fence_End", camp, -6.6f, -18.2f);
            for (float x = -2.2f; x < 13.5f; x += 2.4f)
            {
                Inst("Fence_Section", camp, x, -18.2f, 0f);
            }

            for (float z = -18.2f; z < -3.5f; z += 2.4f)
            {
                Inst("Fence_Section", camp, -18.6f, z, 90f);
            }

            Inst("Fence_End", camp, -18.6f, -3.4f);
            // lamps along the loop (reference art 3)
            foreach (Vector2 p in new[] { new Vector2(-7.4f, -1.2f), new Vector2(-9.6f, 5.8f), new Vector2(-3.0f, 6.0f), new Vector2(7.8f, 7.6f), new Vector2(3.4f, -0.2f), new Vector2(-2.0f, -2.2f) })
            {
                Inst("Lantern_Post", camp, p.x, p.y, Rand(0f, 360f));
            }
        }

        // ---- the old mine
        private static void BuildMine(Transform mine)
        {
            GameObject entrance = Inst("Mine_Entrance", mine, MineFront.x, MineFront.y, 180f, 1f, 0.01f);
            entrance.name = "Mine_Entrance";

            // rails: out of the tunnel, across the trail, ending near the cart
            var pts = new List<Vector3>();
            Vector2[] rail = { new(6.3f, 20.2f), new(6.3f, 15.2f), new(6.5f, 13.6f), new(7.7f, 12.7f), new(9.3f, 12.3f), new(11.0f, 12.5f), new(12.1f, 12.9f) };
            var smooth = new List<Vector2>();
            for (int i = 0; i < rail.Length - 1; i++)
            {
                Vector2 p0 = rail[Mathf.Max(i - 1, 0)], p1 = rail[i], p2 = rail[i + 1], p3 = rail[Mathf.Min(i + 2, rail.Length - 1)];
                for (int s = 0; s < 4; s++)
                {
                    float t = s / 4f;
                    smooth.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
                }
            }

            smooth.Add(rail[rail.Length - 1]);
            foreach (Vector2 p in smooth)
            {
                float y = p.y > 14.6f ? 0.09f : GmTerrain.SurfaceHeight(p.x, p.y) + 0.0f;
                pts.Add(new Vector3(p.x, y, p.y));
            }

            GameObject track = GmProps.RailTrack("RailTrack_Main", pts);
            var trackInstance = (GameObject)PrefabUtility.InstantiatePrefab(track, mine);
            trackInstance.transform.position = Vector3.zero;

            Inst("MineCart_Gold", mine, 10.5f, 12.45f, -7f, 1f, 0.02f);
            Inst("Pickaxe_Leaning", mine, 9.6f, 11.4f, 215f);
            Inst("Crate_Stack_Crystal", mine, 2.6f, 13.7f, 12f);
            Inst("Barrel_Lantern", mine, 4.0f, 13.7f, 0f);
            Inst("Banner_Pole", mine, 1.0f, 12.2f, 20f);
            Inst("Signpost_Mine", mine, 8.9f, 13.5f, 180f + 20f);
            Inst("Lantern_Post", mine, 4.6f, 10.9f, 0f);
            Inst("Lantern_Post", mine, 9.4f, 9.2f, 180f);
            Inst("Crystal_Cluster_L", mine, 1.4f, 14.4f, 30f);
            Inst("Crystal_Cluster_M", mine, 11.6f, 14.1f, 120f);
            Inst("Crystal_Cluster_S", mine, 12.3f, 11.6f, 200f);
        }

        // ---- forest + perimeter
        private static void BuildForest(Transform forest, Transform perimeter)
        {
            // north-west forest: dense mixed trees (collider = trunk only)
            for (int i = 0; i < 2600; i++)
            {
                float x = Rand(-19f, -6.0f), z = Rand(2.0f, 19.0f);
                string kind = Pick(new[] { "Pine_M", "Pine_M", "Pine_S", "Pine_L", "Tree_Round_A", "Tree_Round_B", "Pine_M_Light" });
                float scale = Rand(0.9f, 1.25f);
                TryPlaceLarge(kind, forest, x, z, 1.05f, 0.35f, scale);
            }

            // trees on the other edges of the valley: a loose ring that hides the invisible walls
            for (int i = 0; i < 1500; i++)
            {
                float x = Rand(-19.2f, 19.2f), z = Rand(-18.6f, 19.2f);
                float edge = EdgeDistance(x, z);
                if (edge > 3.4f)
                {
                    continue;
                }

                string kind = Pick(new[] { "Pine_M", "Pine_L", "Pine_M_Light", "Pine_M", "Tree_Round_A", "Pine_S" });
                TryPlaceLarge(kind, perimeter, x, z, 1.0f, 1.1f, Rand(0.95f, 1.35f));
            }

            // a few lone trees on the meadow (reference art 2: a big round tree beside the signpost)
            foreach (Vector3 t in new[] { new Vector3(-16.5f, -3.0f, 1.2f), new Vector3(2.5f, 17.5f, 1f), new Vector3(15.2f, -14.5f, 1f), new Vector3(-6.5f, -15.5f, 1.1f) })
            {
                TryPlaceLarge("Tree_Round_A", perimeter, t.x, t.y, 1.1f, 0.5f, t.z);
            }

            // beyond the walls: collider-free rim forest on the hills (hides the terrain edge, never reachable)
            Transform rim = Group(perimeter, "RimForest");
            for (int i = 0; i < 2600; i++)
            {
                float x = Rand(-35f, 35f), z = Rand(-35f, 35f);
                float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                if (r < 21f)
                {
                    continue;
                }

                float rd = RiverDistance(x, z, out float half);
                if (rd < half + 2.2f)
                {
                    continue;
                }

                if (z > 14f && x > 0.5f && x < 14f)
                {
                    continue; // the rock face of the mine
                }

                if (!IsFreeSolid(x, z, 2.0f, 1.6f))
                {
                    continue;
                }

                string kind = Pick(new[] { "Pine_L_Far", "Pine_M_Far", "Pine_L_Far", "Pine_M_Light_Far", "Pine_S_Far" });
                Solids.Add((new Vector2(x, z), 2.0f));
                Inst(kind, rim, x, z, Rand(0f, 360f), Rand(1.0f, 1.6f), -0.15f);
            }

            // forest floor details
            Transform details = Group(forest, "Details");
            foreach (Vector3 p in new[] { new Vector3(-12.8f, 7.6f, 20f), new Vector3(-8.6f, 12.4f, 80f), new Vector3(-17.0f, 13.5f, 140f) })
            {
                if (LargeFree(p.x, p.y, 0.8f))
                {
                    Inst("Log_A", details, p.x, p.y, p.z);
                }
            }

            foreach (Vector3 p in new[] { new Vector3(-8.2f, 9.6f, 0f), new Vector3(-14.8f, 5.2f, 0f) })
            {
                if (LargeFree(p.x, p.y, 0.6f))
                {
                    Inst("Stump_A", details, p.x, p.y, p.z);
                }
            }

            for (int i = 0; i < 60 && details.childCount < 14; i++)
            {
                float x = Rand(-18.5f, -7f), z = Rand(3f, 18f);
                if (SmallFree(x, z, 1.6f, 1.4f))
                {
                    Inst("Mushroom_Red", details, x, z, Rand(0f, 360f), Rand(0.9f, 1.3f));
                    Solids.Add((new Vector2(x, z), 0.5f));
                }
            }
        }

        private static string Pick(string[] options) => options[rng.Next(options.Length)];

        /// <summary>Distance to the nearest edge of the playable area (negative outside).</summary>
        private static float EdgeDistance(float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Boundary.Length; i++)
            {
                Vector2 a = Boundary[i], b = Boundary[(i + 1) % Boundary.Length];
                best = Mathf.Min(best, SegmentDistance(new Vector2(x, z), a, b, out _));
            }

            return InsidePlay(x, z) ? best : -best;
        }

        private static bool InsidePlay(float x, float z)
        {
            bool inside = false;
            for (int i = 0, j = Boundary.Length - 1; i < Boundary.Length; j = i++)
            {
                Vector2 a = Boundary[i], b = Boundary[j];
                if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        // ---- placement rules
        private static bool IsFreeSolid(float x, float z, float radius, float gap)
        {
            var p = new Vector2(x, z);
            foreach ((Vector2 q, float r) in Solids)
            {
                if ((q - p).sqrMagnitude < (radius + r + gap) * (radius + r + gap))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Open ground for large props: clear of trails (the camera needs room), river, camp, gate, bridge, mine bay and treasure spots.</summary>
        private static bool LargeFree(float x, float z, float radius, float pathFactor = 0.5f)
        {
            var p = new Vector2(x, z);
            if (!InsidePlay(x, z))
            {
                return false;
            }

            if (PathDistance(x, z) < PathHalfWidth + 0.5f + radius * pathFactor)
            {
                return false;
            }

            float rd = RiverDistance(x, z, out float half);
            if (rd < half + 0.8f + radius * 0.4f)
            {
                return false;
            }

            if (Vector2.Distance(p, CampCenter) < 7.2f || Vector2.Distance(p, GatePos) < 4.6f || Vector2.Distance(p, BridgePos) < 4.6f
                || Vector2.Distance(p, MeadowRocks) < 3.4f)
            {
                return false;
            }

            if (x > -1.5f && x < 14f && z > 9.2f)
            {
                return false; // the mine bay
            }

            foreach (SpotDef s in Spots)
            {
                if (Vector2.Distance(p, s.Pos) < 3.3f + radius * 0.45f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SmallFree(float x, float z, float spacing, float pathMargin)
        {
            if (!InsidePlay(x, z) || PathDistance(x, z) < PathHalfWidth + pathMargin)
            {
                return false;
            }

            float rd = RiverDistance(x, z, out float half);
            if (rd < half + 0.8f || Vector2.Distance(new Vector2(x, z), CampCenter) < 6f || (x > -0.5f && x < 13f && z > 9.5f))
            {
                return false;
            }

            foreach (SpotDef s in Spots)
            {
                if (Vector2.Distance(new Vector2(x, z), s.Pos) < 2.4f)
                {
                    return false;
                }
            }

            return IsFreeSolid(x, z, 0.2f, spacing);
        }

        private static void TryPlaceLarge(string prefab, Transform parent, float x, float z, float radius, float gap, float scale)
        {
            float r = radius * scale;
            // trees keep their whole foliage off the trail (the third-person camera rides along it)
            if (!LargeFree(x, z, r, prefab.StartsWith("Pine") || prefab.StartsWith("Tree") ? 1.25f : 0.5f) || !IsFreeSolid(x, z, r, gap))
            {
                return;
            }

            Solids.Add((new Vector2(x, z), r));
            Inst(prefab, parent, x, z, Rand(0f, 360f), scale, -0.05f);
        }

        // ---- meadow rocks, rocky treasure area
        private static void BuildMeadowAndRocks(Transform meadow, Transform rocky)
        {
            Inst("RockCluster_Meadow", meadow, MeadowRocks.x, MeadowRocks.y, 25f, 1.0f, -0.05f);
            Solids.Add((MeadowRocks, 3.2f));
            Inst("Bush_B", meadow, MeadowRocks.x - 2.4f, MeadowRocks.y + 1.9f, 10f, 1.1f);
            Inst("Bush_A", meadow, MeadowRocks.x + 2.7f, MeadowRocks.y - 1.4f, 70f, 0.9f);

            // rocky area: crystal rock clusters framing open clearings (treasure spots keep 3.8 m clear)
            int clusters = 0;
            for (int i = 0; i < 4000 && clusters < 3; i++)
            {
                float x = Rand(3.5f, 16f), z = Rand(-17.5f, -3.5f);
                if (Vector2.Distance(new Vector2(x, z), RockyCenter) > 9f || !LargeFree(x, z, 1.2f) || !IsFreeSolid(x, z, 2.4f, 0.3f))
                {
                    continue;
                }

                Inst("RockCluster_Crystal", rocky, x, z, Rand(0f, 360f), 1f, -0.05f);
                Solids.Add((new Vector2(x, z), 2.6f));
                clusters++;
            }

            for (int i = 0; i < 700; i++)
            {
                float x = Rand(3f, 17.5f), z = Rand(-18f, -2.5f);
                if (Vector2.Distance(new Vector2(x, z), RockyCenter) > 9.5f)
                {
                    continue;
                }

                string kind = Pick(new[] { "Rock_L_A", "Rock_L_B", "Rock_M_A", "Rock_M_B", "Rock_XL_A", "Rock_M_A" });
                float scale = Rand(0.9f, 1.3f);
                TryPlaceLarge(kind, rocky, x, z, kind.Contains("XL") ? 3.0f : kind.Contains("_L_") ? 2.0f : 1.1f, 0.8f, scale);
            }

            // boulders along the south fence and the river edge give the area its rocky frame
            foreach (Vector3 c in new[]
            {
                new Vector3(10.6f, -16.4f, 20f), new Vector3(13.4f, -16.0f, 140f), new Vector3(3.6f, -12.6f, 70f), new Vector3(3.8f, -16.2f, 200f),
                new Vector3(15.6f, -10.6f, 10f), new Vector3(11.4f, -12.6f, 250f), new Vector3(4.6f, -6.6f, 300f),
            })
            {
                float rr = 1.2f;
                bool nearSpot = false;
                foreach (SpotDef s in Spots)
                {
                    nearSpot |= Vector2.Distance(new Vector2(c.x, c.y), s.Pos) < 3.2f;
                }

                if (!nearSpot && InsidePlay(c.x, c.y) && PathDistance(c.x, c.y) > PathHalfWidth + 0.8f && IsFreeSolid(c.x, c.y, rr, 0.2f))
                {
                    Solids.Add((new Vector2(c.x, c.y), rr));
                    Inst(rng.NextDouble() < 0.5 ? "Rock_L_A" : "Rock_L_B", rocky, c.x, c.y, c.z, Rand(0.8f, 1.15f), -0.05f);
                }
            }

            Inst("Crystal_Cluster_M", rocky, 13.1f, -4.6f, 20f);
            Inst("Crystal_Cluster_S", rocky, 6.4f, -6.8f, 150f);
            Inst("Crystal_Cluster_M", rocky, 15.0f, -8.0f, 90f);

            // cliff rocks closing the north edge around the mine pocket, and flanking the waterfall
            foreach (Vector3 c in new[] { new Vector3(-8.6f, 17.2f, 100f), new Vector3(-12.2f, 18.0f, 200f) })
            {
                Inst(c.z < 150f ? "Rock_XL_A" : "Rock_XL_B", rocky, c.x, c.y, c.z, Rand(1.1f, 1.4f), -0.5f);
            }
        }

        // ---- bushes, grass, flowers
        private static void BuildVegetation(Transform veg)
        {
            Transform bushes = Group(veg, "Bushes");
            Transform flowers = Group(veg, "Flowers");
            Transform grass = Group(veg, "GrassTufts");
            Transform rocks = Group(veg, "SmallRocks");

            for (int i = 0; i < 700 && bushes.childCount < 60; i++)
            {
                float x = Rand(-19f, 19f), z = Rand(-18.5f, 18.5f);
                float pd = PathDistance(x, z);
                bool nearTrail = pd > PathHalfWidth + 0.7f && pd < PathHalfWidth + 3.8f;
                bool inForest = x < -6f && z > 2f;
                if (!(nearTrail || inForest) || !SmallFree(x, z, 1.7f, 0.7f))
                {
                    continue;
                }

                string kind = rng.NextDouble() < 0.14 ? "Bush_Berry" : rng.NextDouble() < 0.5 ? "Bush_A" : "Bush_B";
                Solids.Add((new Vector2(x, z), 0.7f));
                Inst(kind, bushes, x, z, Rand(0f, 360f), Rand(0.8f, 1.3f), -0.04f);
            }

            string[] flowerKinds = { "Flower_White", "Flower_Yellow", "Flower_Pink", "Flower_Lavender", "Flower_White", "Flower_Yellow" };
            for (int i = 0; i < 1200 && flowers.childCount < 95; i++)
            {
                float x = Rand(-19f, 19f), z = Rand(-18.5f, 18.5f);
                float pd = PathDistance(x, z);
                if (pd < PathHalfWidth + 0.5f || pd > 8f || !SmallFree(x, z, 1.1f, 0.4f))
                {
                    continue;
                }

                Solids.Add((new Vector2(x, z), 0.2f));
                Inst(Pick(flowerKinds), flowers, x, z, Rand(0f, 360f), Rand(0.9f, 1.4f));
            }

            for (int i = 0; i < 1800 && grass.childCount < 190; i++)
            {
                float x = Rand(-19f, 19f), z = Rand(-18.5f, 18.5f);
                if (PathDistance(x, z) < PathHalfWidth + 0.35f || !SmallFree(x, z, 0.9f, 0.2f))
                {
                    continue;
                }

                Solids.Add((new Vector2(x, z), 0.15f));
                Inst(rng.NextDouble() < 0.5 ? "Grass_Tuft_A" : "Grass_Tuft_B", grass, x, z, Rand(0f, 360f), Rand(0.8f, 1.5f));
            }

            for (int i = 0; i < 900 && rocks.childCount < 46; i++)
            {
                float x = Rand(-19f, 19f), z = Rand(-18.5f, 18.5f);
                float pd = PathDistance(x, z);
                if (pd < PathHalfWidth + 0.6f || pd > 6f || !SmallFree(x, z, 1.4f, 0.5f))
                {
                    continue;
                }

                Solids.Add((new Vector2(x, z), 0.4f));
                Inst(rng.NextDouble() < 0.5 ? "Rock_S_A" : "Rock_S_B", rocks, x, z, Rand(0f, 360f), Rand(0.7f, 1.4f), -0.05f);
            }
        }

        // ---- backdrop: far ground, mountains
        private static void BuildBackdrop(Transform backdrop)
        {
            Inst("Backdrop_Ground", backdrop, 0f, 0f, 0f, 1f, -1.2f - GmTerrain.SurfaceHeight(0f, 0f));
            (string prefab, Vector2 pos, float scale, float yaw)[] mountains =
            {
                ("Mountain_C", new Vector2(10f, 118f), 1.2f, 0f), ("Mountain_A", new Vector2(78f, 100f), 1.0f, 40f), ("Mountain_B", new Vector2(-62f, 104f), 1.1f, 80f),
                ("Mountain_D", new Vector2(120f, 30f), 1.1f, 20f), ("Mountain_A", new Vector2(-118f, 20f), 1.0f, 120f), ("Mountain_B", new Vector2(-30f, -118f), 1.1f, 10f),
                ("Mountain_C", new Vector2(86f, -100f), 0.9f, 70f), ("Mountain_D", new Vector2(-92f, -96f), 1.0f, 160f), ("Mountain_B", new Vector2(130f, -50f), 0.9f, 200f),
            };
            foreach ((string prefab, Vector2 pos, float scale, float yaw) in mountains)
            {
                Inst(prefab, backdrop, pos.x, pos.y, yaw, scale, 0f).transform.position = new Vector3(pos.x, 0f, pos.y);
            }
        }

        // ---- invisible walls (layer "Ignore Raycast": the camera's collision mask ignores them)
        private static void BuildBoundaries(Transform bounds)
        {
            for (int i = 0; i < Boundary.Length; i++)
            {
                Vector2 a = Boundary[i], b = Boundary[(i + 1) % Boundary.Length];
                var go = new GameObject("Wall_" + i);
                go.layer = IgnoreRaycastLayer;
                go.transform.SetParent(bounds, false);
                Vector2 mid = (a + b) * 0.5f;
                go.transform.position = new Vector3(mid.x, 4f, mid.y);
                go.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg + 90f, 0f);
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3((b - a).magnitude + 0.6f, 20f, 0.8f);
                col.center = new Vector3(0f, 0f, 0.4f); // the wall's inner face sits exactly on the boundary line
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccluderStatic);
            }
        }

        // ================================================================ treasure spots

        private static void BuildSpots(Transform spots)
        {
            foreach (SpotDef def in Spots)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Treasure/{def.Prefab}.prefab");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, spots);
                go.name = "Treasure_" + def.Id.Substring(def.Id.IndexOf('.') + 1);
                go.transform.position = GmTerrain.Ground(def.Pos.x, def.Pos.y);
                var spot = go.GetComponent<TreasureSpot>();
                var so = new SerializedObject(spot);
                so.FindProperty("spotId").stringValue = def.Id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ================================================================ player, camera, systems

        private static void ConfigurePlayerAndCamera(Scene scene, Transform systems)
        {
            GameObject player = FindRoot(scene, "Player");
            Vector2 toMine = MineFront - SpawnPos;
            float yaw = YawFacing(toMine);
            player.transform.SetPositionAndRotation(GmTerrain.Ground(SpawnPos.x, SpawnPos.y) + Vector3.up * 1.1f, Quaternion.Euler(0f, yaw, 0f));

            Transform camTransform = systems.Find("Main Camera");
            var cam = camTransform.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 280f;
            cam.fieldOfView = 60f;
            var controller = camTransform.GetComponent<ThirdPersonCameraController>();
            var so = new SerializedObject(controller);
            so.FindProperty("yaw").floatValue = yaw;
            so.FindProperty("pitch").floatValue = 18f;
            // everything except "Ignore Raycast": the invisible boundary walls must never pull the camera in
            so.FindProperty("collisionMask").FindPropertyRelative("m_Bits").uintValue = ~(1u << IgnoreRaycastLayer); // uint: a negative intValue is clamped to 0 (= collide with nothing)
            so.ApplyModifiedPropertiesWithoutUndo();
            Quaternion rot = Quaternion.Euler(18f, yaw, 0f);
            camTransform.SetPositionAndRotation(player.transform.position + new Vector3(0f, 1.4f, 0f) + rot * Vector3.back * 5f, rot);

            var hunt = systems.Find("TreasureSystems").GetComponent<TreasureHuntController>();
            var hso = new SerializedObject(hunt);
            hso.FindProperty("levelTitle").stringValue = "GREEN MINE";
            hso.FindProperty("areaId").stringValue = "green_mine";
            hso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================ lighting

        private static void ConfigureLighting(Scene scene, Transform env)
        {
            Transform lighting = env.Find("Lighting");
            Light sun = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light l in root.GetComponentsInChildren<Light>(true))
                {
                    if (l.type == LightType.Directional)
                    {
                        sun = l;
                    }
                }
            }

            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.name = "Sun";
            sun.transform.SetParent(lighting, false);
            sun.transform.rotation = Quaternion.Euler(50f, -38f, 0f);
            sun.transform.position = new Vector3(0f, 30f, 0f);
            sun.color = new Color(1f, 0.95f, 0.82f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sun.lightmapBakeType = LightmapBakeType.Realtime;

            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(GmMats.Folder + "/GM_Sky.mat");
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.78f, 0.98f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.74f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.34f, 0.24f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.87f, 0.97f);
            RenderSettings.fogStartDistance = 48f;
            RenderSettings.fogEndDistance = 170f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        }

        // ================================================================ build settings

        private static void AddToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene s in list)
            {
                if (s.path == ScenePath)
                {
                    s.enabled = true;
                    EditorBuildSettings.scenes = list.ToArray();
                    return;
                }
            }

            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ================================================================ diagnostics

        public static string Describe(Scene scene)
        {
            int renderers = 0, colliders = 0, meshColliders = 0, lights = 0, triangles = 0, instances = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderers++;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && root.name == EnvironmentRoot)
                    {
                        triangles += (int)(mf.sharedMesh.triangles.Length / 3);
                    }
                }

                colliders += root.GetComponentsInChildren<Collider>(true).Length;
                meshColliders += root.GetComponentsInChildren<MeshCollider>(true).Length;
                lights += root.GetComponentsInChildren<Light>(true).Length;
                if (root.name == EnvironmentRoot)
                {
                    instances = root.GetComponentsInChildren<Transform>(true).Length;
                }
            }

            return $"environment objects={instances} meshRenderers(all)={renderers} environmentTriangles(sum of all meshes)={triangles} colliders={colliders} (mesh colliders {meshColliders}) lights={lights}";
        }
    }
}
#endif
