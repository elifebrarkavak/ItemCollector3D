#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>
    /// Builds Assets/Scenes/RockyCanyon.unity. The scene starts as a COPY of the working Green Mine scene, so the Player, camera, HUD, detector, DIG / COLLECT,
    /// feedback, music and progress wiring are exactly the ones that already work; everything specific to Green Mine (the environment, the 15 treasures, the mine zone,
    /// the water, the forest ambience) is removed and a new, modular RockyCanyonEnvironment built from <see cref="RcLayout"/> takes its place. Green Mine is only read.
    /// Run once with <see cref="BuildScene"/>; <see cref="RebuildEnvironment"/> replaces ONLY the environment root (it wipes hand edits there, so it asks for a confirmation flag).
    /// </summary>
    public static class RockyCanyonBuilder
    {
        public const string ScenePath = "Assets/Scenes/RockyCanyon.unity";
        public const string SourceScenePath = "Assets/Scenes/GreenMine.unity";
        public const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        public const string EnvironmentName = "RockyCanyonEnvironment";
        public const string TreasureRootName = "TreasureSpots";

        public static readonly string[] ZoneNames = { "A_DesertOutpost", "B_DryRiverbed", "C_OldRailway", "D_CanyonOverlook", "E_HiddenRavine" };

        // ================================================================ entry points

        public static string BuildScene(bool overwrite = false)
        {
            if (File.Exists(ScenePath) && !overwrite)
            {
                return "RockyCanyon.unity already exists (pass overwrite = true to replace it).";
            }

            if (SceneManager.GetActiveScene().isDirty)
            {
                return "The open scene has unsaved changes: save or discard them first.";
            }

            RcLayout.ResetGrid();
            if (File.Exists(ScenePath))
            {
                AssetDatabase.DeleteAsset(ScenePath);
            }

            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
            {
                return "Could not copy the Green Mine scene.";
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            StripGreenMine(scene);
            BuildEnvironment(scene);
            Wire(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            string report = Report(scene);
            EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            return report;
        }

        public static string RebuildEnvironment(bool confirmOverwrite)
        {
            if (!confirmOverwrite)
            {
                return "RebuildEnvironment replaces RockyCanyonEnvironment (hand edits in it are lost): call it with confirmOverwrite = true.";
            }

            RcLayout.ResetGrid();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == EnvironmentName)
                {
                    Object.DestroyImmediate(root);
                }
            }

            BuildEnvironment(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            string report = Report(scene);
            EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            return report;
        }

        // ================================================================ strip what belongs to Green Mine

        private static GameObject Root(Scene scene, string name)
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

        private static void StripGreenMine(Scene scene)
        {
            Object.DestroyImmediate(Root(scene, "GreenMineEnvironment"));
            GameObject spots = Root(scene, TreasureRootName);
            for (int i = spots.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(spots.transform.GetChild(i).gameObject);
            }

            Transform systems = Root(scene, "GameplaySystems").transform;
            foreach (string name in new[] { "MineInteriorZone", "WaterMotion", "AreaAmbience" })
            {
                Transform t = systems.Find(name);
                if (t != null)
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }
        }

        // ================================================================ wire the copied systems to this area

        private static void Wire(Scene scene)
        {
            Transform systems = Root(scene, "GameplaySystems").transform;
            var hunt = systems.Find("TreasureSystems").GetComponent<ItemCollector3D.Treasure.TreasureHuntController>();
            var so = new SerializedObject(hunt);
            so.FindProperty("areaId").stringValue = "rocky_canyon";
            so.FindProperty("levelTitle").stringValue = "ROCKY CANYON";
            so.FindProperty("goldTarget").intValue = 1200;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var hud in Object.FindObjectsByType<ItemCollector3D.UI.HudController>(FindObjectsInactive.Include))
            {
                if (hud.gameObject.scene == scene)
                {
                    var hso = new SerializedObject(hud);
                    hso.FindProperty("maximumGold").intValue = 1200;
                    hso.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            {
                if (text.gameObject.scene == scene && text.text == "GREEN MINE")
                {
                    text.text = "ROCKY CANYON";
                }
            }

            // the player starts at the outpost, looking along the first leg of the riverbed (the bridge's towers stand in the left part of that view); the camera starts behind the player
            Vector3 spawn = RcLayout.Spawn;
            float ground = RcLayout.Surface(spawn.x, spawn.z);
            Transform player = Root(scene, "Player").transform;
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = new Vector3(spawn.x, ground + 1.05f, spawn.z);
            player.rotation = Quaternion.Euler(0f, 68f, 0f);
            controller.enabled = true;

            var cameraController = systems.Find("Main Camera").GetComponent<ItemCollector3D.Player.ThirdPersonCameraController>();
            var cso = new SerializedObject(cameraController);
            cso.FindProperty("yaw").floatValue = 68f;
            cso.ApplyModifiedPropertiesWithoutUndo();
            Vector3 pivot = player.position + new Vector3(0f, 1.4f, 0f);
            cameraController.transform.rotation = Quaternion.Euler(20f, 68f, 0f);
            cameraController.transform.position = pivot + cameraController.transform.rotation * Vector3.back * 5f;
        }

        // ================================================================ environment

        private static Transform Child(Transform parent, string name, Vector3? position = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (position.HasValue)
            {
                go.transform.position = position.Value;
            }

            return go.transform;
        }

        private static Vector3 OnGround(float x, float z, float lift = 0f) => new(x, RcLayout.Surface(x, z) + lift, z);

        /// <summary>
        /// Builds the environment ROOT from scratch: terrain, the five (empty) zone roots, boundaries, sun, spawn marker, treasure groups. The look of the five areas, the backdrop and the
        /// atmosphere come from <see cref="RcDressing"/> (stage 15B), which is called at the end; it can also be run on its own on an existing scene (modular, no rebuild).
        /// </summary>
        private static void BuildEnvironment(Scene scene)
        {
            Texture2D colorMap = RcAssets.PaintColorMap();
            Texture2D sky = RcAssets.PaintSky();
            Material ground = RcAssets.Lit("RC_Ground", Color.white, colorMap);
            RcAssets.Sky(sky);
            Mesh terrainMesh = RcAssets.BuildTerrainMesh();
            AssetDatabase.SaveAssets();

            SceneManager.SetActiveScene(scene);
            var env = new GameObject(EnvironmentName);
            SceneManager.MoveGameObjectToScene(env, scene);

            // ---- terrain: ONE flat-shaded mesh and one mesh collider
            var terrain = Child(env.transform, "Terrain");
            terrain.gameObject.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
            var terrainRenderer = terrain.gameObject.AddComponent<MeshRenderer>();
            terrainRenderer.sharedMaterial = ground;
            terrain.gameObject.AddComponent<MeshCollider>().sharedMesh = terrainMesh;
            GameObjectUtility.SetStaticEditorFlags(terrain.gameObject, StaticEditorFlags.BatchingStatic);

            // ---- the five areas, each with its own root, an Anchor (its walkable centre) and (filled by RcDressing) Props and Colliders children
            Zone(env.transform, ZoneNames[0], OnGround(-34f, -22f));
            Zone(env.transform, ZoneNames[1], OnGround(6f, -14f));
            Zone(env.transform, ZoneNames[2], OnGround(-6f, 17f));
            Zone(env.transform, ZoneNames[3], OnGround(RcLayout.OverlookCentre.x, RcLayout.OverlookCentre.y));
            Zone(env.transform, ZoneNames[4], OnGround(-17f, 2f));
            Child(env.transform, "Backdrop");
            BuildBoundaries(env.transform);
            BuildLighting(env.transform);
            BuildSpawnMarker(env.transform);

            // ---- treasures: a separate root with one (empty) group per area, plus two INACTIVE test points that are not part of the game
            BuildTreasureRoot(scene);
            RcDressing.DressAll(false);
        }

        private static Transform Zone(Transform env, string name, Vector3 anchor)
        {
            Transform zone = Child(env, name);
            Child(zone, "Anchor", anchor);
            return zone;
        }

        private static void BuildBoundaries(Transform env)
        {
            // invisible safety walls INSIDE the rock at the edge of the mesh (the walls themselves already stop the player)
            Transform boundaries = Child(env, "Boundaries");
            (string name, Vector3 centre, Vector3 size)[] walls =
            {
                ("Wall_E", new Vector3(54.5f, 20f, 0f), new Vector3(1f, 40f, 92f)), ("Wall_W", new Vector3(-54.5f, 20f, 0f), new Vector3(1f, 40f, 92f)),
                ("Wall_N", new Vector3(0f, 20f, 44.5f), new Vector3(112f, 40f, 1f)), ("Wall_S", new Vector3(0f, 20f, -44.5f), new Vector3(112f, 40f, 1f)),
            };
            foreach ((string name, Vector3 centre, Vector3 size) in walls)
            {
                Transform t = Child(boundaries, name, centre);
                var box = t.gameObject.AddComponent<BoxCollider>();
                box.size = size;
            }
        }

        private static void BuildLighting(Transform env)
        {
            Transform lighting = Child(env, "Lighting");
            var go = new GameObject("Sun");
            go.transform.SetParent(lighting, false);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            RenderSettings.sun = sun; // colour, direction, ambient and fog are set by RcDressing.ApplyAtmosphere
        }

        private static void BuildSpawnMarker(Transform env)
        {
            Transform markers = Child(env, "Markers");
            Child(markers, "PlayerSpawn", OnGround(RcLayout.Spawn.x, RcLayout.Spawn.z));
        }

        private static void BuildTreasureRoot(Scene scene)
        {
            GameObject root = Root(scene, TreasureRootName);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject); // idempotent: a second run must not add a second set of groups and test points
            }

            foreach (string zone in ZoneNames)
            {
                Child(root.transform, zone);
            }

            // two TEST points (the existing treasure prefabs, so they can be checked here); the group is inactive: they are not part of the game and pay no gold
            Transform tests = Child(root.transform, "_TestPoints");
            AddTestSpot(tests, "Treasure_test_a", "Assets/Prefabs/Treasure/Treasure_GoldCoin.prefab", "rocky_canyon.test_a", OnGround(-29f, -22f));
            AddTestSpot(tests, "Treasure_test_b", "Assets/Prefabs/Treasure/Treasure_GoldBar.prefab", "rocky_canyon.test_b", OnGround(8f, -10f));
            tests.gameObject.SetActive(false);
        }

        private static void AddTestSpot(Transform parent, string name, string prefabPath, string id, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.position = position;
            var spot = instance.GetComponent<ItemCollector3D.Treasure.TreasureSpot>();
            var so = new SerializedObject(spot);
            so.FindProperty("spotId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================ report

        public static string Report(Scene scene)
        {
            var sb = new System.Text.StringBuilder();
            GameObject env = Root(scene, EnvironmentName);
            int renderers = 0, colliders = 0, triangles = 0;
            var materials = new HashSet<Material>();
            foreach (MeshRenderer r in env.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                foreach (Material m in r.sharedMaterials)
                {
                    materials.Add(m);
                }

                var f = r.GetComponent<MeshFilter>();
                if (f != null && f.sharedMesh != null)
                {
                    triangles += f.sharedMesh.triangles.Length / 3;
                }
            }

            colliders = env.GetComponentsInChildren<Collider>(true).Length;
            sb.AppendLine($"environment: {renderers} renderers, {materials.Count} materials, {triangles} triangles, {colliders} colliders");
            sb.AppendLine("zone roots: " + string.Join(", ", ZoneNames));
            return sb.ToString();
        }
    }
}
#endif
