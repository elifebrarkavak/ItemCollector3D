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

        private sealed class Mats
        {
            public Material Ground, Red, Orange, Sand, Dark, Wood, Metal, Cloth, Sky;
        }

        private static Mats mats;
        private static Mesh boulder, pillar, cube, cylinder;
        private static System.Random rng;

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

            // the player starts at the outpost, looking along the riverbed; the camera starts behind the player
            Vector3 spawn = RcLayout.Spawn;
            float ground = RcLayout.Surface(spawn.x, spawn.z);
            Transform player = Root(scene, "Player").transform;
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = new Vector3(spawn.x, ground + 1.05f, spawn.z);
            player.rotation = Quaternion.Euler(0f, 90f, 0f);
            controller.enabled = true;

            var cameraController = systems.Find("Main Camera").GetComponent<ItemCollector3D.Player.ThirdPersonCameraController>();
            var cso = new SerializedObject(cameraController);
            cso.FindProperty("yaw").floatValue = 90f;
            cso.ApplyModifiedPropertiesWithoutUndo();
            Vector3 pivot = player.position + new Vector3(0f, 1.4f, 0f);
            cameraController.transform.rotation = Quaternion.Euler(20f, 90f, 0f);
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

        private enum Col
        {
            None,
            Box,
            Sphere,
        }

        private static GameObject Prop(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale, Col collider = Col.None, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider == Col.Box)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = mesh.bounds.center;
                box.size = mesh.bounds.size;
            }
            else if (collider == Col.Sphere)
            {
                var sphere = go.AddComponent<SphereCollider>();
                sphere.center = new Vector3(0f, 0.04f, 0f);
                sphere.radius = 0.42f;
            }

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static Material PickRock() => rng.NextDouble() < 0.45 ? mats.Red : rng.NextDouble() < 0.6 ? mats.Orange : mats.Sand;

        private static void BuildEnvironment(Scene scene)
        {
            rng = new System.Random(4242);
            Texture2D colorMap = RcAssets.PaintColorMap();
            Texture2D sky = RcAssets.PaintSky();
            mats = new Mats
            {
                Ground = RcAssets.Lit("RC_Ground", Color.white, colorMap),
                Red = RcAssets.Lit("RC_RedRock", new Color(0.72f, 0.30f, 0.18f)),
                Orange = RcAssets.Lit("RC_OrangeRock", new Color(0.86f, 0.46f, 0.22f)),
                Sand = RcAssets.Lit("RC_Sandstone", new Color(0.90f, 0.72f, 0.48f)),
                Dark = RcAssets.Lit("RC_DarkRock", new Color(0.42f, 0.24f, 0.18f)),
                Wood = RcAssets.Lit("RC_Wood", new Color(0.56f, 0.37f, 0.21f)),
                Metal = RcAssets.Lit("RC_Metal", new Color(0.31f, 0.31f, 0.34f)),
                Cloth = RcAssets.Lit("RC_Cloth", new Color(0.84f, 0.72f, 0.52f)),
                Sky = RcAssets.Sky(sky),
            };
            boulder = RcAssets.BoulderMesh();
            pillar = RcAssets.PillarMesh();
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            Mesh terrainMesh = RcAssets.BuildTerrainMesh();
            AssetDatabase.SaveAssets();

            SceneManager.SetActiveScene(scene);
            var env = new GameObject(EnvironmentName);
            SceneManager.MoveGameObjectToScene(env, scene);

            // ---- terrain: ONE flat-shaded mesh and one mesh collider
            var terrain = Child(env.transform, "Terrain");
            terrain.gameObject.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
            var terrainRenderer = terrain.gameObject.AddComponent<MeshRenderer>();
            terrainRenderer.sharedMaterial = mats.Ground;
            terrain.gameObject.AddComponent<MeshCollider>().sharedMesh = terrainMesh;
            GameObjectUtility.SetStaticEditorFlags(terrain.gameObject, StaticEditorFlags.BatchingStatic);

            // ---- the five areas, each with its own root, an Anchor (its walkable centre) and a Props child
            Transform a = BuildOutpost(env.transform);
            Transform b = BuildRiverbed(env.transform);
            Transform c = BuildRailway(env.transform);
            Transform d = BuildOverlook(env.transform);
            Transform e = BuildRavine(env.transform);
            BuildBackdrop(env.transform);
            BuildBoundaries(env.transform);
            BuildLighting(env.transform);
            BuildSpawnMarker(env.transform);

            // ---- treasures: a separate root with one (empty) group per area, plus two INACTIVE test points that are not part of the game
            BuildTreasureRoot(scene);
            _ = (a, b, c, d, e);
        }

        private static Transform Zone(Transform env, string name, Vector3 anchor)
        {
            Transform zone = Child(env, name);
            Child(zone, "Anchor", anchor);
            Child(zone, "Props");
            return zone;
        }

        // ---- A  Desert Outpost
        private static Transform BuildOutpost(Transform env)
        {
            Transform zone = Zone(env, ZoneNames[0], OnGround(-34f, -22f));
            Transform props = zone.Find("Props");

            Shack(props, "Shack_A", -41f, -28f, 18f, mats.Cloth);
            Shack(props, "Shack_B", -29f, -30f, -12f, mats.Orange);
            Shack(props, "ToolShed", -43f, -17f, 80f, mats.Sand, 0.7f);

            Prop(props, "Platform_Visual", cube, mats.Wood, OnGround(-35f, -16.5f, 0.1f), Quaternion.Euler(0f, 4f, 0f), new Vector3(6.5f, 0.2f, 4f), Col.None, false); // 0.2 m: no lip to trip on
            for (int i = 0; i < 8; i++)
            {
                float x = -44f + (i % 4) * 1.4f + R(-0.2f, 0.2f), z = -24.5f - (i / 4) * 1.5f;
                float s = R(0.85f, 1.15f);
                Prop(props, "Crate_" + i, cube, mats.Wood, OnGround(x, z, s * 0.5f), Quaternion.Euler(0f, R(-25f, 25f), 0f), new Vector3(s, s, s), Col.Box);
            }

            Prop(props, "Crate_Stacked", cube, mats.Wood, OnGround(-43.4f, -24.6f, 1.5f), Quaternion.Euler(0f, 12f, 0f), new Vector3(0.9f, 0.9f, 0.9f), Col.None);
            for (int i = 0; i < 4; i++)
            {
                Prop(props, "Barrel_" + i, cylinder, mats.Metal, OnGround(-26.5f + i * 1.0f, -30.5f, 0.5f), Quaternion.identity, new Vector3(0.75f, 0.5f, 0.75f), Col.Box);
            }

            // a sign post at the way out to the riverbed
            Prop(props, "SignPost", cube, mats.Wood, OnGround(-24f, -17f, 1.1f), Quaternion.identity, new Vector3(0.18f, 2.2f, 0.18f), Col.Box);
            Prop(props, "SignBoard", cube, mats.Wood, OnGround(-24f, -17f, 1.9f), Quaternion.Euler(0f, 20f, 0f), new Vector3(1.4f, 0.55f, 0.1f));
            Scatter(props, "Rock", mats, 5, RcZone.Outpost, -6f, -3f, 3f, 0.9f, 1.6f,
                new[] { new Vector3(-37f, 0f, -22f), new Vector3(-41f, 0f, -28f), new Vector3(-29f, 0f, -30f), new Vector3(-43f, 0f, -17f), new Vector3(-24f, 0f, -17f) }, 6f);
            return zone;
        }

        private static void Shack(Transform parent, string name, float x, float z, float yaw, Material wall, float scale = 1f)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 body = new Vector3(4.2f, 2.6f, 3.2f) * scale;
            Prop(parent, name + "_Body", cube, wall, OnGround(x, z, body.y * 0.5f), rot, body, Col.Box);
            Prop(parent, name + "_Roof", cube, mats.Wood, OnGround(x, z, body.y + 0.1f), rot, new Vector3(body.x + 0.7f, 0.22f, body.z + 0.7f));
            Prop(parent, name + "_Door", cube, mats.Dark, OnGround(x, z, 0.9f) + rot * new Vector3(0f, 0f, -body.z * 0.5f - 0.02f), rot, new Vector3(0.9f, 1.8f, 0.06f));
        }

        // ---- B  Dry Riverbed
        private static Transform BuildRiverbed(Transform env)
        {
            Transform zone = Zone(env, ZoneNames[1], OnGround(6f, -14f));
            Transform props = zone.Find("Props");
            var keepClear = new List<Vector3> { RcLayout.Spawn };
            Scatter(props, "Boulder", mats, 14, RcZone.Riverbed, -9f, -1.2f, 4.2f, 0.8f, 1.9f, keepClear.ToArray(), 0f);  // the basin and the banks
            Scatter(props, "Rock", mats, 14, RcZone.Riverbed, -5f, -0.6f, 3f, 0.4f, 0.9f, keepClear.ToArray(), 0f);      // small stones
            return zone;
        }

        // ---- C  Old Railway
        private static Transform BuildRailway(Transform env)
        {
            Transform zone = Zone(env, ZoneNames[2], OnGround(-6f, 17f, 0f));
            Transform props = zone.Find("Props");
            RcLayout.Shape rail = System.Array.Find(RcLayout.Shapes, s => s.Name == "Railway");
            Vector3[] p = rail.Points;
            float carried = 0f;
            for (int i = 0; i < p.Length - 1; i++)
            {
                Vector3 from = OnGround(p[i].x, p[i].z, 0.14f), to = OnGround(p[i + 1].x, p[i + 1].z, 0.14f);
                Vector3 dir = to - from;
                float length = dir.magnitude;
                Quaternion along = Quaternion.LookRotation(dir.normalized, Vector3.up);
                Vector3 side = along * Vector3.right;
                foreach (float s in new[] { -0.75f, 0.75f })
                {
                    Prop(props, "Rail_" + i + (s < 0f ? "L" : "R"), cube, mats.Metal, (from + to) * 0.5f + side * s, along, new Vector3(0.12f, 0.12f, length), Col.None, false);
                }

                // sleepers every 2.4 m, carried across the polyline's corners
                for (float d = 2.4f - carried; d < length; d += 2.4f)
                {
                    Vector3 pos = OnGround(Mathf.Lerp(p[i].x, p[i + 1].x, d / length), Mathf.Lerp(p[i].z, p[i + 1].z, d / length), 0.06f);
                    Prop(props, "Sleeper_" + i + "_" + Mathf.RoundToInt(d), cube, mats.Wood, pos, along, new Vector3(2.3f, 0.1f, 0.34f), Col.None, false);
                    carried = length - d;
                }
            }

            // where the bridge will stand: the track dips here (a marker, nothing to walk on or collide with)
            Child(zone, "BridgeSite", OnGround(3f, 17.5f));
            Scatter(props, "Boulder", mats, 6, RcZone.Railway, -3.2f, -1.9f, 5f, 0.8f, 1.3f, new Vector3[0], 0f); // along the corridor walls only
            return zone;
        }

        // ---- D  Canyon Overlook
        private static Transform BuildOverlook(Transform env)
        {
            Transform zone = Zone(env, ZoneNames[3], OnGround(RcLayout.OverlookCentre.x, RcLayout.OverlookCentre.y));
            Transform props = zone.Find("Props");
            Vector3 centre = OnGround(RcLayout.OverlookCentre.x, RcLayout.OverlookCentre.y);
            // a low parapet of boulders around the plateau except where the path arrives (west), so the camera can still look over it
            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * 360f;
                if (angle > 115f && angle < 180f)
                {
                    continue;
                }

                float rad = angle * Mathf.Deg2Rad;
                float x = centre.x + Mathf.Cos(rad) * 4.3f, z = centre.z + Mathf.Sin(rad) * 4.3f;
                float s = R(0.9f, 1.25f);
                GameObject g = Prop(props, "ParapetRock_" + i, boulder, PickRock(), OnGround(x, z, 0.1f), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(s * 1.3f, s * 0.9f, s * 1.3f), Col.Sphere);
                g.transform.position = new Vector3(x, RcLayout.Surface(x, z) + 0.05f, z);
            }

            Prop(props, "LookoutDeck_Visual", cube, mats.Wood, OnGround(centre.x, centre.z - 1f, 0.1f), Quaternion.Euler(0f, 20f, 0f), new Vector3(4.2f, 0.2f, 2.8f), Col.None, false);
            Prop(props, "ViewSign_Post", cube, mats.Wood, OnGround(centre.x - 2.5f, centre.z - 2.5f, 0.9f), Quaternion.identity, new Vector3(0.16f, 1.8f, 0.16f), Col.Box);
            Prop(props, "ViewSign_Board", cube, mats.Wood, OnGround(centre.x - 2.5f, centre.z - 2.5f, 1.6f), Quaternion.Euler(0f, 200f, 0f), new Vector3(1.1f, 0.45f, 0.08f));
            return zone;
        }

        // ---- E  Hidden Ravine
        private static Transform BuildRavine(Transform env)
        {
            Transform zone = Zone(env, ZoneNames[4], OnGround(-17f, 2f));
            Transform props = zone.Find("Props");
            // two boulders at the mouth, one on each side, leaving the middle open
            Prop(props, "MouthRock_L", boulder, mats.Red, OnGround(-0.8f, -11.2f, 0.1f), Quaternion.Euler(0f, 30f, 0f), new Vector3(1.8f, 1.4f, 1.8f), Col.Sphere);
            Prop(props, "MouthRock_R", boulder, mats.Orange, OnGround(-7.2f, -10.4f, 0.1f), Quaternion.Euler(0f, 110f, 0f), new Vector3(1.6f, 1.2f, 1.6f), Col.Sphere);
            // a ring of small stones marks the pocket where the hidden treasure area will be (no colliders)
            Vector3 centre = OnGround(-17f, 2f);
            for (int i = 0; i < 8; i++)
            {
                float rad = i / 8f * Mathf.PI * 2f;
                float x = centre.x + Mathf.Cos(rad) * 3.4f, z = centre.z + Mathf.Sin(rad) * 3.4f;
                Prop(props, "PocketStone_" + i, boulder, mats.Sand, OnGround(x, z, 0f), Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * R(0.45f, 0.7f), Col.None);
            }

            Child(zone, "HiddenTreasureArea", centre);
            return zone;
        }

        // ---- the rim: tall pillars on the rock mass (silhouette only: no colliders, beyond the walls)
        private static void BuildBackdrop(Transform env)
        {
            Transform backdrop = Child(env, "Backdrop");
            int placed = 0;
            for (int attempt = 0; attempt < 2000 && placed < 22; attempt++)
            {
                float x = R(-50f, 50f), z = R(-41f, 41f);
                if (RcLayout.EdgeDistance(x, z) < 12f)
                {
                    continue;
                }

                float width = R(3f, 5.5f), height = R(5f, 11f);
                Prop(backdrop, "Pillar_" + placed, pillar, placed % 2 == 0 ? mats.Red : mats.Orange, new Vector3(x, RcLayout.Surface(x, z) - 1.2f, z), Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(width, height, width), Col.None);
                placed++;
            }
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
            go.transform.rotation = Quaternion.Euler(48f, 205f, 0f);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            RenderSettings.sun = sun;
            RenderSettings.skybox = mats.Sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.70f, 0.78f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.86f, 0.74f, 0.60f);
            RenderSettings.ambientGroundColor = new Color(0.46f, 0.36f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0055f;
            RenderSettings.fogColor = new Color(0.93f, 0.85f, 0.72f);
        }

        private static void BuildSpawnMarker(Transform env)
        {
            Transform markers = Child(env, "Markers");
            Child(markers, "PlayerSpawn", OnGround(RcLayout.Spawn.x, RcLayout.Spawn.z));
        }

        private static void BuildTreasureRoot(Scene scene)
        {
            GameObject root = Root(scene, TreasureRootName);
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

        // ================================================================ scatter

        /// <summary>Boulders on the floor of one area: between edge distances [minEdge, maxEdge] (negative = inside), gentle ground, spaced apart, off the given points.</summary>
        private static void Scatter(Transform parent, string baseName, Mats m, int count, RcZone zone, float minEdge, float maxEdge, float spacing, float minScale, float maxScale, Vector3[] avoid, float avoidRadius)
        {
            var placed = new List<Vector2>();
            int made = 0;
            for (int attempt = 0; attempt < 4000 && made < count; attempt++)
            {
                float x = R(-RcLayout.HalfX + 6f, RcLayout.HalfX - 6f), z = R(-RcLayout.HalfZ + 6f, RcLayout.HalfZ - 6f);
                if (RcLayout.ZoneAt(x, z) != zone)
                {
                    continue;
                }

                float edge = RcLayout.EdgeDistance(x, z);
                if (edge < minEdge || edge > maxEdge || RcLayout.Slope(x, z) > 0.3f)
                {
                    continue;
                }

                bool clash = false;
                foreach (Vector2 q in placed)
                {
                    clash |= (q - new Vector2(x, z)).sqrMagnitude < spacing * spacing;
                }

                foreach (Vector3 v in avoid)
                {
                    clash |= new Vector2(v.x - x, v.z - z).sqrMagnitude < avoidRadius * avoidRadius;
                }

                if (clash)
                {
                    continue;
                }

                placed.Add(new Vector2(x, z));
                float s = R(minScale, maxScale);
                Col collider = s >= 1.0f ? Col.Sphere : Col.None;
                Prop(parent, baseName + "_" + made, boulder, PickRock(), OnGround(x, z, 0.05f), Quaternion.Euler(R(-6f, 6f), R(0f, 360f), R(-6f, 6f)), new Vector3(s * R(1f, 1.3f), s * R(0.8f, 1.1f), s * R(1f, 1.3f)), collider);
                made++;
            }
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
