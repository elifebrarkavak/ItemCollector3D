#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>
    /// Builds Assets/Scenes/AncientMine.unity (stage 16A). Like Rocky Canyon, the scene starts as a COPY of the working Green Mine scene, so the Player, camera, HUD, detector, DIG / COLLECT,
    /// feedback, music and progress wiring are exactly the ones that already work. Everything specific to Green Mine (its environment, its 15 treasures, the water, the forest ambience) is
    /// removed and a new, modular AncientMineEnvironment built from <see cref="AmLayout"/> takes its place. Green Mine and Rocky Canyon are only READ; none of their builders is run.
    /// Run once with <see cref="BuildScene"/>; <see cref="RebuildEnvironment"/> replaces ONLY the environment root; <see cref="AmDressing.DressZone"/> refills one area on the open scene.
    /// </summary>
    public static class AncientMineBuilder
    {
        public const string ScenePath = "Assets/Scenes/AncientMine.unity";
        public const string SourceScenePath = "Assets/Scenes/GreenMine.unity";
        public const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        public const string EnvironmentName = "AncientMineEnvironment";
        public const string TreasureRootName = "TreasureSpots";
        public const string IosProfilePath = "Assets/Settings/Build Profiles/iOS.asset";
        public const string MenuPrefabPath = "Assets/Prefabs/Menus/MainMenuUI.prefab";

        // the cave's colours: cool blue, violet and turquoise ambient, a soft moon-like key light
        private static readonly Color AmbientSky = new(0.44f, 0.48f, 0.80f);
        private static readonly Color AmbientEquator = new(0.36f, 0.38f, 0.66f);
        private static readonly Color AmbientGround = new(0.20f, 0.17f, 0.38f);
        private static readonly Color FogColour = new(0.14f, 0.13f, 0.30f);

        // ================================================================ entry points

        public static string BuildScene(bool overwrite = false)
        {
            if (File.Exists(ScenePath) && !overwrite)
            {
                return "AncientMine.unity already exists (pass overwrite = true to replace it).";
            }

            if (SceneManager.GetActiveScene().isDirty)
            {
                return "The open scene has unsaved changes: save or discard them first.";
            }

            AmLayout.ResetGrid();
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
            RegisterScene();
            return Report(scene);
        }

        public static string RebuildEnvironment(bool confirmOverwrite)
        {
            if (!confirmOverwrite)
            {
                return "RebuildEnvironment replaces AncientMineEnvironment (hand edits in it are lost): call it with confirmOverwrite = true.";
            }

            AmLayout.ResetGrid();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == EnvironmentName)
                {
                    Object.DestroyImmediate(root);
                }
            }

            BuildEnvironment(scene);
            Wire(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return Report(scene);
        }

        /// <summary>Refills the props and colliders of ONE area in the open Ancient Mine scene (the stage 16B workflow) and saves.</summary>
        public static string DressZoneInOpenScene(string zoneName)
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform env = Root(scene, EnvironmentName).transform;
            string result = AmDressing.DressZone(env, zoneName);
            // the rebuilt zone has a new mesh (no ribbon patch) and new objects (the glow manager lost its renderers): repair both (stage 19A)
            result += "\n" + AmRibbonFix.Reapply(zoneName) + "\n" + AmAtmosphere.RelinkGlow();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return result;
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
            foreach (string name in new[] { "WaterMotion", "AreaAmbience" })
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
            so.FindProperty("areaId").stringValue = "ancient_mine";
            so.FindProperty("levelTitle").stringValue = "ANCIENT MINE";
            so.FindProperty("goldTarget").intValue = 2500;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var hud in Object.FindObjectsByType<ItemCollector3D.UI.HudController>(FindObjectsInactive.Include))
            {
                if (hud.gameObject.scene == scene)
                {
                    var hso = new SerializedObject(hud);
                    hso.FindProperty("maximumGold").intValue = 2500;
                    hso.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            {
                if (text.gameObject.scene == scene && text.text == "GREEN MINE")
                {
                    text.text = "ANCIENT MINE";
                }
            }

            // the player stands in the Ancient Gate, looking east along the old stone road; the camera starts behind the player
            Vector3 spawn = AmLayout.Spawn;
            float ground = AmLayout.Surface(spawn.x, spawn.z);
            Transform player = Root(scene, "Player").transform;
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = new Vector3(spawn.x, ground + 1.05f, spawn.z);
            player.rotation = Quaternion.Euler(0f, AmLayout.SpawnYaw, 0f);
            controller.enabled = true;

            var cameraController = systems.Find("Main Camera").GetComponent<ItemCollector3D.Player.ThirdPersonCameraController>();
            var cso = new SerializedObject(cameraController);
            cso.FindProperty("yaw").floatValue = AmLayout.SpawnYaw;
            cso.ApplyModifiedPropertiesWithoutUndo();
            Vector3 pivot = player.position + new Vector3(0f, 1.4f, 0f);
            cameraController.transform.rotation = Quaternion.Euler(18f, AmLayout.SpawnYaw, 0f);
            cameraController.transform.position = pivot + cameraController.transform.rotation * Vector3.back * 5f;

            // underground: no sky. The camera clears to the deep violet of the cave, the ambient light and the fog are the cave's
            var camera = cameraController.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.045f, 0.12f);
            ApplyAtmosphere(Root(scene, EnvironmentName).transform);
            ConfigureInteriorZone(systems);
        }

        /// <summary>
        /// The existing MineInteriorZone (Green Mine's camera helper) is reused for the whole cave: it shortens the orbit a little and raises the camera's collision floor, which switches on
        /// the camera's "narrow place" behaviour (swing round to open space, lift over the player) so the camera never ends inside the miner's head. Its mood colours are the cave's own
        /// ambient colours, so the fade changes nothing visible.
        /// </summary>
        private static void ConfigureInteriorZone(Transform systems)
        {
            Transform zone = systems.Find("MineInteriorZone") ?? systems.Find("CaveInteriorZone"); // a rebuild finds it under its new name
            if (zone == null)
            {
                Debug.LogWarning("The copied scene has no MineInteriorZone to reuse.");
                return;
            }

            zone.name = "CaveInteriorZone";
            var component = zone.GetComponent<ItemCollector3D.World.MineInteriorZone>();
            var so = new SerializedObject(component);
            SerializedProperty bounds = so.FindProperty("interiorBounds");
            bounds.arraySize = 1;
            bounds.GetArrayElementAtIndex(0).boundsValue = new Bounds(new Vector3(0f, 17f, 0f), new Vector3(2f * AmLayout.HalfX, 40f, 2f * AmLayout.HalfZ));
            so.FindProperty("approachMargin").floatValue = 1f;
            so.FindProperty("cameraDistanceScale").floatValue = 0.9f;
            so.FindProperty("cameraMinimumDistance").floatValue = 1.8f;
            so.FindProperty("ambientSky").colorValue = AmbientSky;
            so.FindProperty("ambientEquator").colorValue = AmbientEquator;
            so.FindProperty("ambientGround").colorValue = AmbientGround;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        private static Vector3 OnGround(float x, float z, float lift = 0f) => new(x, AmLayout.Surface(x, z) + lift, z);

        /// <summary>
        /// Builds the environment ROOT from scratch: the cave shell (floor + walls and ceiling meshes, one pair per area), the five area roots, boundaries, light, markers, the empty
        /// treasure groups; then the placeholders of every area (<see cref="AmDressing"/>).
        /// </summary>
        private static void BuildEnvironment(Scene scene)
        {
            Texture2D palette = P.Build();
            Material lit = AmAssets.PaletteMaterial(palette);
            Material glow = AmAssets.GlowMaterial(palette);
            Dictionary<AmZone, AmShell> shells = AmAssets.BuildShells();
            AssetDatabase.SaveAssets();

            SceneManager.SetActiveScene(scene);
            var env = new GameObject(EnvironmentName);
            SceneManager.MoveGameObjectToScene(env, scene);

            for (int i = 0; i < AmDressing.ZoneNames.Length; i++)
            {
                string zoneName = AmDressing.ZoneNames[i];
                AmZone zone = AmDressing.ZoneOf(zoneName);
                Transform root = Child(env.transform, zoneName);
                Vector2 anchor = AmLayout.Anchors[i].point;
                Child(root, "Anchor", OnGround(anchor.x, anchor.y));
                AmShell shell = shells[zone];
                Shell(root, "Floor", shell.Floor, lit, true, true);
                Shell(root, "Ceiling", shell.Ceiling, lit, false, true);
            }

            BuildBoundaries(env.transform);
            BuildLighting(env.transform);
            BuildMarkers(env.transform);
            BuildTreasureRoot(scene);
            AmDressing.DressAll(env.transform);
            EditorUtility.SetDirty(glow);
        }

        private static void Shell(Transform zone, string name, Mesh mesh, Material material, bool castShadows, bool collider)
        {
            Transform t = Child(zone, name);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = t.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider)
            {
                t.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        private static void BuildBoundaries(Transform env)
        {
            // invisible safety walls far inside the solid rock at the edge of the map and a safety floor under everything (the cave walls already stop the player; these only catch mistakes)
            Transform boundaries = Child(env, "Boundaries");
            float hx = AmLayout.HalfX, hz = AmLayout.HalfZ;
            (string name, Vector3 centre, Vector3 size)[] walls =
            {
                ("Wall_E", new Vector3(hx - 0.5f, 20f, 0f), new Vector3(1f, 60f, 2f * hz)), ("Wall_W", new Vector3(-hx + 0.5f, 20f, 0f), new Vector3(1f, 60f, 2f * hz)),
                ("Wall_N", new Vector3(0f, 20f, hz - 0.5f), new Vector3(2f * hx, 60f, 1f)), ("Wall_S", new Vector3(0f, 20f, -hz + 0.5f), new Vector3(2f * hx, 60f, 1f)),
                ("SafetyFloor", new Vector3(0f, -1.5f, 0f), new Vector3(2f * hx, 1f, 2f * hz)),
            };
            foreach ((string name, Vector3 centre, Vector3 size) in walls)
            {
                Transform t = Child(boundaries, name, centre);
                t.gameObject.AddComponent<BoxCollider>().size = size;
            }
        }

        private static void BuildLighting(Transform env)
        {
            Transform lighting = Child(env, "Lighting");
            var go = new GameObject("Sun");
            go.transform.SetParent(lighting, false);
            go.AddComponent<Light>().type = LightType.Directional; // colour, direction, shadows: ApplyAtmosphere
        }

        public static void ApplyAtmosphere(Transform env)
        {
            Transform sun = env.Find("Lighting/Sun");
            if (sun != null)
            {
                // a cool moon-like key light from above-left: it reads the floors and the faces of the walls; the ceiling shells cast no shadow, so it reaches the whole cave
                sun.rotation = Quaternion.Euler(58f, 32f, 0f);
                var light = sun.GetComponent<Light>();
                light.color = new Color(0.72f, 0.80f, 1f);
                light.intensity = 1.05f;
                light.shadows = LightShadows.Hard;
                light.shadowStrength = 0.5f;
                RenderSettings.sun = light;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0065f;
            RenderSettings.fogColor = FogColour;
            RenderSettings.skybox = null;
        }

        private static void BuildMarkers(Transform env)
        {
            Transform markers = Child(env, "Markers");
            Child(markers, "PlayerSpawn", OnGround(AmLayout.Spawn.x, AmLayout.Spawn.z));
            BuildRouteMarkers(markers);
            BuildCandidateMarkers(markers);
        }

        private static void BuildRouteMarkers(Transform markers)
        {
            Transform routes = Child(markers, "Routes");
            foreach ((string group, (string name, Vector2 point)[] points) in new[] { ("Main", AmLayout.MainRoute), ("Alternate", AmLayout.AlternateRoute) })
            {
                Transform g = Child(routes, group);
                foreach ((string name, Vector2 point) in points)
                {
                    Child(g, name, OnGround(point.x, point.y));
                }
            }

            // side routes: walks that start on a main / alternate route point and end in a side area or a dead end (the tests walk them)
            foreach ((string name, Vector2[] points) in AmLayout.SideRoutes)
            {
                Transform g = Child(routes, "Side_" + name);
                for (int i = 0; i < points.Length; i++)
                {
                    Child(g, $"{i:00}", OnGround(points[i].x, points[i].y));
                }
            }
        }

        /// <summary>Empty marker transforms for the places kept clear for the treasures of stage 16C (they are NOT treasures: nothing here is collectable).</summary>
        private static void BuildCandidateMarkers(Transform markers)
        {
            Transform candidates = Child(markers, "TreasureCandidates");
            foreach ((string id, Vector2 point, float lift) in AmLayout.TreasureCandidates)
            {
                Child(candidates, id, OnGround(point.x, point.y, lift));
            }
        }

        /// <summary>
        /// Rebuilds ONLY the route markers and the treasure-candidate markers of the open Ancient Mine scene (nothing else in the scene is touched: not the shells, the props, the colliders, the
        /// systems or the treasure groups) and saves.
        /// </summary>
        public static string RefreshMarkers()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform markers = Root(scene, EnvironmentName).transform.Find("Markers");
            foreach (string name in new[] { "Routes", "TreasureCandidates" })
            {
                Transform old = markers.Find(name);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            AmLayout.ResetGrid();
            BuildRouteMarkers(markers);
            BuildCandidateMarkers(markers);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"markers refreshed: {markers.Find("Routes").childCount} route groups, {markers.Find("TreasureCandidates").childCount} treasure candidates";
        }

        /// <summary>
        /// Regenerates the palette texture and the cave shell meshes IN PLACE (the same mesh assets, so every reference in the scene stays valid; the geometry is identical, only the swatch UVs follow
        /// the palette layout) and re-assigns the shell colliders. Nothing else in the scene is touched.
        /// </summary>
        public static string RefreshShells()
        {
            Scene scene = SceneManager.GetActiveScene();
            Texture2D palette = P.Build();
            AmAssets.PaletteMaterial(palette);
            AmAssets.GlowMaterial(palette);
            Dictionary<AmZone, AmShell> shells = AmAssets.BuildShells();
            Transform env = Root(scene, EnvironmentName).transform;
            int triangles = 0;
            foreach (string zoneName in AmDressing.ZoneNames)
            {
                AmShell shell = shells[AmDressing.ZoneOf(zoneName)];
                foreach ((string part, Mesh mesh) in new[] { ("Floor", shell.Floor), ("Ceiling", shell.Ceiling) })
                {
                    Transform t = env.Find(zoneName + "/" + part);
                    t.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var collider = t.GetComponent<MeshCollider>();
                    collider.sharedMesh = null;
                    collider.sharedMesh = mesh;
                    triangles += mesh.triangles.Length / 3;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"shells refreshed: {triangles} triangles";
        }

        // ================================================================ lighting character per area (stage 16B)

        private static CaveZoneMoodPreset[] ZoneMoods()
        {
            return new[]
            {
                // the Ancient Gate: a bright, cool welcome
                new CaveZoneMoodPreset("A_AncientGate", new Color(0.54f, 0.60f, 0.92f), new Color(0.46f, 0.50f, 0.78f), new Color(0.28f, 0.26f, 0.46f), new Color(0.16f, 0.18f, 0.34f), 0.0055f),
                // the Hall of Pillars: neutral, grand stone
                new CaveZoneMoodPreset("B_HallOfPillars", new Color(0.52f, 0.52f, 0.66f), new Color(0.44f, 0.44f, 0.58f), new Color(0.27f, 0.25f, 0.35f), new Color(0.17f, 0.16f, 0.26f), 0.0055f),
                // the Crystal Caverns: saturated blue and purple
                new CaveZoneMoodPreset("C_CrystalCaverns", new Color(0.42f, 0.40f, 0.90f), new Color(0.34f, 0.30f, 0.76f), new Color(0.22f, 0.15f, 0.46f), new Color(0.16f, 0.12f, 0.36f), 0.0062f),
                // the Forgotten Workshop: old and warm
                new CaveZoneMoodPreset("D_ForgottenWorkshop", new Color(0.66f, 0.56f, 0.54f), new Color(0.58f, 0.46f, 0.40f), new Color(0.34f, 0.25f, 0.22f), new Color(0.25f, 0.17f, 0.15f), 0.006f),
                // the Royal Vault: purple and turquoise
                new CaveZoneMoodPreset("E_RoyalVault", new Color(0.66f, 0.50f, 0.96f), new Color(0.40f, 0.56f, 0.84f), new Color(0.38f, 0.22f, 0.54f), new Color(0.20f, 0.12f, 0.36f), 0.0055f),
            };
        }

        private readonly struct CaveZoneMoodPreset
        {
            public readonly string Zone;
            public readonly Color Sky, Equator, Ground, Fog;
            public readonly float Density;

            public CaveZoneMoodPreset(string zone, Color sky, Color equator, Color ground, Color fog, float density)
            {
                Zone = zone;
                Sky = sky;
                Equator = equator;
                Ground = ground;
                Fog = fog;
                Density = density;
            }
        }

        /// <summary>Adds (or updates) the CaveZoneMood object under GameplaySystems (the ambient and fog colour of each area). Idempotent; saves the scene.</summary>
        public static string AddZoneMood()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform systems = Root(scene, "GameplaySystems").transform;
            Transform t = systems.Find("CaveZoneMood");
            if (t == null)
            {
                t = Child(systems, "CaveZoneMood");
                t.gameObject.AddComponent<ItemCollector3D.World.CaveZoneMood>();
            }

            var so = new SerializedObject(t.GetComponent<ItemCollector3D.World.CaveZoneMood>());
            so.FindProperty("target").objectReferenceValue = Root(scene, "Player").transform;
            so.FindProperty("blendSeconds").floatValue = 1.8f;
            CaveZoneMoodPreset[] presets = ZoneMoods();
            SerializedProperty array = so.FindProperty("moods");
            array.arraySize = presets.Length;
            for (int i = 0; i < presets.Length; i++)
            {
                SerializedProperty e = array.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("zone").stringValue = presets[i].Zone;
                e.FindPropertyRelative("ambientSky").colorValue = presets[i].Sky;
                e.FindPropertyRelative("ambientEquator").colorValue = presets[i].Equator;
                e.FindPropertyRelative("ambientGround").colorValue = presets[i].Ground;
                e.FindPropertyRelative("fogColor").colorValue = presets[i].Fog;
                e.FindPropertyRelative("fogDensity").floatValue = presets[i].Density;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // the camera helper (the reused MineInteriorZone) fades the ambient to ITS mood colours when the camera is inside the cave: those are the Gate's, so its first fade changes nothing
            Transform interior = systems.Find("CaveInteriorZone");
            if (interior != null)
            {
                var iso = new SerializedObject(interior.GetComponent<ItemCollector3D.World.MineInteriorZone>());
                iso.FindProperty("ambientSky").colorValue = presets[0].Sky;
                iso.FindProperty("ambientEquator").colorValue = presets[0].Equator;
                iso.FindProperty("ambientGround").colorValue = presets[0].Ground;
                iso.ApplyModifiedPropertiesWithoutUndo();
            }

            // the scene's own ambient is the Gate's: the player starts there, so nothing jumps when the mood takes over
            RenderSettings.ambientSkyColor = presets[0].Sky;
            RenderSettings.ambientEquatorColor = presets[0].Equator;
            RenderSettings.ambientGroundColor = presets[0].Ground;
            RenderSettings.fogColor = presets[0].Fog;
            RenderSettings.fogDensity = presets[0].Density;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return $"CaveZoneMood: {presets.Length} moods";
        }

        private static void BuildTreasureRoot(Scene scene)
        {
            GameObject root = Root(scene, TreasureRootName);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject); // idempotent
            }

            // one EMPTY group per area. The real treasures (ids "ancient_mine.*") come in stage 16C; nothing is placed here, not even test points
            foreach (string zone in AmDressing.ZoneNames)
            {
                Child(root.transform, zone);
            }
        }

        // ================================================================ build settings (Build Settings list and the iOS build profile)

        public static string RegisterScene()
        {
            var sb = new System.Text.StringBuilder();
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                sb.AppendLine("Build Settings: AncientMine.unity added");
            }

            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(IosProfilePath);
            if (profile != null)
            {
                var list = new List<EditorBuildSettingsScene>(profile.scenes);
                if (!list.Exists(s => s.path == ScenePath))
                {
                    list.Add(new EditorBuildSettingsScene(ScenePath, true));
                    profile.scenes = list.ToArray();
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssetIfDirty(profile);
                    sb.AppendLine("iOS build profile: AncientMine.unity added");
                }
            }

            sb.AppendLine("Build Settings scenes: " + string.Join(", ", System.Array.ConvertAll(EditorBuildSettings.scenes, s => Path.GetFileName(s.path))));
            return sb.ToString();
        }

        /// <summary>Points the Ancient Mine card of the Area Selection (inside the Main Menu prefab) at AncientMine.unity, which removes its COMING SOON state.</summary>
        public static string WireAreaCard()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MenuPrefabPath);
            try
            {
                foreach (ItemCollector3D.Menus.AreaCardView card in root.GetComponentsInChildren<ItemCollector3D.Menus.AreaCardView>(true))
                {
                    if (card.AreaId == "ancient_mine")
                    {
                        var so = new SerializedObject(card);
                        so.FindProperty("sceneName").stringValue = ItemCollector3D.Menus.SceneNavigator.AncientMineScene;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, MenuPrefabPath);
                        return "Ancient Mine card -> " + ItemCollector3D.Menus.SceneNavigator.AncientMineScene;
                    }
                }

                return "no ancient_mine card found";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ================================================================ report

        public static string Report(Scene scene)
        {
            var sb = new System.Text.StringBuilder();
            GameObject env = Root(scene, EnvironmentName);
            int renderers = 0, triangles = 0;
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
                    for (int s = 0; s < f.sharedMesh.subMeshCount; s++)
                    {
                        triangles += (int)f.sharedMesh.GetIndexCount(s) / 3;
                    }
                }
            }

            int colliders = env.GetComponentsInChildren<Collider>(true).Length;
            sb.AppendLine($"environment: {renderers} renderers, {materials.Count} materials, {triangles} triangles, {colliders} colliders");
            sb.AppendLine("zone roots: " + string.Join(", ", AmDressing.ZoneNames));
            return sb.ToString();
        }
    }
}
#endif
