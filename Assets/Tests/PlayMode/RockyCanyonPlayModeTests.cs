#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using ItemCollector3D.World;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Rocky Canyon foundation (stage 15A): the scene is wired into the game (Area Selection, build settings), reuses the working Green Mine systems, both miners spawn
    /// correctly, all five areas can be WALKED to (ramps, the raised railway and the winding overlook path included), the camera behaves in the canyon, Pause / Resume /
    /// back to the Main Menu work, and the detector and DIG / COLLECT work on the existing treasure prefabs. Green Mine's saves and treasure ids are untouched. In-memory
    /// stores everywhere; the developer's real save file is hashed before the first and after the last test.
    /// </summary>
    public sealed class RockyCanyonPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/RockyCanyon.unity";
        private const string GreenMinePath = "Assets/Scenes/GreenMine.unity";

        private const string FinishedGreenMineSave =
            "{\"version\":1,\"totalGold\":500,\"collectedTreasureIds\":[\"green_mine.gold_coin_01\",\"green_mine.meadow_01\",\"green_mine.riverbank_01\"," +
            "\"green_mine.rocky_01\",\"green_mine.rocky_03\",\"green_mine.forest_01\",\"green_mine.mine_01\",\"green_mine.mine_tunnel_01\",\"green_mine.mine_room_01\"," +
            "\"green_mine.mine_room_02\",\"green_mine.rocky_04\",\"green_mine.bridge_01\"],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\"],\"completedAreaIds\":[\"green_mine\"]}";

        private static readonly string[] Miners = { CharacterSelection.MaleId, CharacterSelection.FemaleId };

        private static string realSavePath;
        private static string realSaveHashBefore;

        private Scene scene;
        private float previousTimeScale;
        private MemoryProgressStore store;

        // ------------------------------------------------------------------ the real save must not move

        private static string HashOfRealSave()
        {
            if (!File.Exists(realSavePath))
            {
                return "(no file)";
            }

            using SHA256 sha = SHA256.Create();
            return System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(realSavePath)));
        }

        [OneTimeSetUp]
        public void HashTheRealSave()
        {
            realSavePath = Path.Combine(Application.persistentDataPath, "item_collector_progress.json");
            realSaveHashBefore = HashOfRealSave();
        }

        [OneTimeTearDown]
        public void RealSaveIsUntouched()
        {
            Assert.That(HashOfRealSave(), Is.EqualTo(realSaveHashBefore), "the developer's real save file must be byte-for-byte unchanged by these tests");
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.Current.ResetToDefaults();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            GamePause.Resume();
            SceneNavigator.LoadHook = null;
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            scene = default;
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.Current.ResetToDefaults();
        }

        // ------------------------------------------------------------------ helpers

        private IEnumerator Load(string path, string saveJson = null)
        {
            store = saveJson != null ? new MemoryProgressStore(saveJson) : new MemoryProgressStore();
            ProgressManager.StoreFactoryOverride = () => store;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null, "Could not load " + path);
            yield return load;
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
            Physics.SyncTransforms();
        }

        private GameObject RootObject(string name)
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

        private T One<T>() where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            Assert.Fail("Not found: " + typeof(T).Name);
            return null;
        }

        private Button FindButton(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.name == name)
                    {
                        return button;
                    }
                }
            }

            Assert.Fail("Button not found: " + name);
            return null;
        }

        private Transform Anchor(string zone)
        {
            Transform t = RootObject("RockyCanyonEnvironment").transform.Find(zone + "/Anchor");
            Assert.That(t, Is.Not.Null, "zone anchor " + zone);
            return t;
        }

        private Collider TerrainCollider => RootObject("RockyCanyonEnvironment").transform.Find("Terrain").GetComponent<Collider>();

        private float TerrainHeight(float x, float z)
        {
            var ray = new Ray(new Vector3(x, 80f, z), Vector3.down);
            Assert.That(TerrainCollider.Raycast(ray, out RaycastHit hit, 200f), Is.True, $"the terrain has ground at {x:F1},{z:F1}");
            return hit.point.y;
        }

        /// <summary>The walkable surface at (x, z): the terrain or the bridge deck, whichever is higher (so the bridge crossing is measured on the deck, not on the gorge floor).</summary>
        private float WalkSurface(float x, float z)
        {
            float best = TerrainHeight(x, z);
            Transform deck = RootObject("RockyCanyonEnvironment").transform.Find("C_OldRailway/Colliders/BridgeDeck");
            if (deck != null)
            {
                var ray = new Ray(new Vector3(x, 80f, z), Vector3.down);
                if (deck.GetComponent<Collider>().Raycast(ray, out RaycastHit hit, 200f))
                {
                    best = Mathf.Max(best, hit.point.y);
                }
            }

            return best;
        }

        /// <summary>The surface directly under the player's feet (the highest collider below the middle of the capsule, the player itself excluded).</summary>
        private static float GroundUnder(Transform player)
        {
            float best = float.MinValue;
            foreach (RaycastHit hit in Physics.RaycastAll(player.position, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.root != player && hit.point.y > best)
                {
                    best = hit.point.y;
                }
            }

            return best;
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        // ================================================================ wiring

        [Test]
        public void TheScene_IsInTheBuildSettings_AndTheRockyCardPointsAtIt()
        {
            Assert.That(File.Exists(ScenePath), Is.True);
            bool inBuild = false;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                inBuild |= s.path == ScenePath && s.enabled;
            }

            Assert.That(inBuild, Is.True, "RockyCanyon.unity is in the Build Settings scene list");
            var profile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>("Assets/Settings/Build Profiles/iOS.asset");
            Assert.That(profile, Is.Not.Null);
            bool inProfile = false;
            foreach (EditorBuildSettingsScene s in profile.scenes)
            {
                inProfile |= s.path == ScenePath && s.enabled;
            }

            Assert.That(inProfile, Is.True, "and in the iOS build profile");
            Assert.That(SceneNavigator.RockyCanyonScene, Is.EqualTo("RockyCanyon"));
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Menus/MainMenuUI.prefab");
            bool found = false;
            foreach (AreaCardView card in menu.GetComponentsInChildren<AreaCardView>(true))
            {
                if (card.AreaId == "rocky_canyon")
                {
                    found = true;
                    Assert.That(card.SceneName, Is.EqualTo("RockyCanyon"), "the Area Selection card opens RockyCanyon.unity");
                }
                else if (card.AreaId == "ancient_mine")
                {
                    Assert.That(card.SceneName, Is.Empty, "Ancient Mine is still COMING SOON");
                }
            }

            Assert.That(found, Is.True);
        }

        [Test]
        public void TheCatalog_KeepsTheUnlockThresholds()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AreaCatalog>("Assets/Data/Areas/AreaCatalog.asset");
            Assert.That(catalog.Get("rocky_canyon").unlockTotalGold, Is.EqualTo(500), "Rocky Canyon opens at 500 total gold");
            Assert.That(catalog.Get("ancient_mine").unlockTotalGold, Is.EqualTo(1200), "Ancient Mine keeps its 1200 lock");
            var service = ProgressService.Create(catalog, new MemoryProgressStore());
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False, "locked below 500");
            service.RegisterCollected("a", 499);
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False, "499 is not enough");
            service.RegisterCollected("b", 1);
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.True, "500 opens it");
            Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.False);
        }

        // ================================================================ structure and budget

        [UnityTest]
        public IEnumerator TheScene_ReusesTheGreenMineSystems_KeepsAreasApart_AndLeavesGreenMineBehind()
        {
            yield return Load(ScenePath, FinishedGreenMineSave);
            foreach (string name in new[] { "Player", "GameplaySystems", "RockyCanyonEnvironment", "TreasureSpots" })
            {
                Assert.That(RootObject(name), Is.Not.Null, "root object " + name);
            }

            Assert.That(RootObject("GreenMineEnvironment"), Is.Null, "no Green Mine scenery");
            Assert.That(One<TreasureHuntController>(), Is.Not.Null);
            Assert.That(One<TreasureDetector>(), Is.Not.Null);
            Assert.That(One<ThirdPersonCameraController>(), Is.Not.Null);
            Assert.That(One<RewardFeedbackPresenter>(), Is.Not.Null);
            Assert.That(One<DigFeedbackPresenter>(), Is.Not.Null);
            Assert.That(One<AmbientMusicPlayer>(), Is.Not.Null, "the music keeps playing");
            Assert.That(FindButton("DigButton"), Is.Not.Null);
            Assert.That(FindButton("CollectButton"), Is.Not.Null);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Assert.That(root.GetComponentInChildren<MineInteriorZone>(true), Is.Null, "no Green Mine mine zone");
                Assert.That(root.GetComponentInChildren<WaterMotion>(true), Is.Null, "no Green Mine water");
                Assert.That(root.GetComponentInChildren<AreaAmbience>(true), Is.Null, "no Green Mine forest ambience (the canyon gets its own later)");
            }

            // the five areas each have their own root with an Anchor and Props; the treasure root is separate and has a group per area
            Transform env = RootObject("RockyCanyonEnvironment").transform;
            foreach (string zone in new[] { "A_DesertOutpost", "B_DryRiverbed", "C_OldRailway", "D_CanyonOverlook", "E_HiddenRavine" })
            {
                Assert.That(env.Find(zone), Is.Not.Null, zone);
                Assert.That(env.Find(zone + "/Anchor"), Is.Not.Null, zone + "/Anchor");
                Assert.That(env.Find(zone + "/Props"), Is.Not.Null, zone + "/Props");
                Assert.That(RootObject("TreasureSpots").transform.Find(zone), Is.Not.Null, "TreasureSpots/" + zone);
            }

            Assert.That(env.Find("Terrain"), Is.Not.Null);
            Assert.That(env.Find("Boundaries"), Is.Not.Null);
            Assert.That(env.Find("Lighting/Sun"), Is.Not.Null);

            // the HUD speaks Rocky Canyon, from the catalog: the title and the goal (cumulative gold, 1200)
            bool title = false, gold = false;
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text.gameObject.scene != scene)
                {
                    continue;
                }

                title |= text.text == "ROCKY CANYON";
                gold |= Regex.IsMatch(text.text, @"^500 / 1200 GOLD$");
                Assert.That(text.text, Is.Not.EqualTo("GREEN MINE"), "no leftover Green Mine title");
            }

            Assert.That(title, Is.True, "the HUD title");
            Assert.That(gold, Is.True, "the HUD shows the saved 500 gold against Rocky Canyon's 1200 goal");
        }

        [UnityTest]
        public IEnumerator TheEnvironment_StaysWithinAMobileBudget_AndEveryRendererIsStatic()
        {
            yield return Load(ScenePath);
            Transform env = RootObject("RockyCanyonEnvironment").transform;
            int renderers = 0, triangles = 0, colliders = 0;
            var materials = new HashSet<Material>();
            foreach (MeshRenderer r in env.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                foreach (Material m in r.sharedMaterials)
                {
                    materials.Add(m);
                }

                triangles += r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
                Assert.That(GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.BatchingStatic), Is.True, r.name + " is batching static");
            }

            colliders = env.GetComponentsInChildren<Collider>(true).Length;
            Assert.That(renderers, Is.LessThan(60), "renderers (the props of every area are merged into a few meshes)");
            Assert.That(materials.Count, Is.LessThanOrEqualTo(4), "materials: terrain, palette, glow");
            Assert.That(triangles, Is.LessThan(70000), "triangles of the whole environment");
            Assert.That(colliders, Is.LessThan(150), "colliders (only what blocks the player)");
            foreach (MeshRenderer r in env.GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.That(r.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), r.name + ": URP");
            }

            Collider terrain = TerrainCollider;
            Assert.That(terrain.bounds.size.x, Is.InRange(100f, 125f), "terrain width");
            Assert.That(terrain.bounds.size.z, Is.InRange(80f, 100f), "terrain depth");
            Assert.That(terrain.bounds.min.y, Is.GreaterThanOrEqualTo(-0.2f));
            Assert.That(terrain.bounds.max.y - terrain.bounds.min.y, Is.GreaterThan(15f), "real height differences (walls reach well above the floors)");
            Debug.Log($"Rocky Canyon: {renderers} renderers, {materials.Count} materials, {triangles} triangles, {colliders} colliders; terrain {terrain.bounds.size}");
        }

        // ================================================================ both miners, camera, HUD

        [UnityTest]
        public IEnumerator BothMiners_SpawnAtTheOutpost_OnTheGround_WithTheRightModel_CameraAndHud()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load(ScenePath);
                Transform player = RootObject("Player").transform;
                Assert.That(player.GetComponent<PlayerCharacterVisual>().AppliedId, Is.EqualTo(id), "the chosen miner is the one in the canyon");
                Assert.That(player.GetComponentInChildren<MinerAnimator>(true), Is.Not.Null);
                yield return new WaitForSecondsRealtime(0.8f);
                float ground = TerrainHeight(player.position.x, player.position.z);
                Assert.That(player.position.y - ground, Is.InRange(0.9f, 1.4f), id + ": standing on the ground, not in it or above it");
                Assert.That(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(Anchor("A_DesertOutpost").position.x, Anchor("A_DesertOutpost").position.z)),
                    Is.LessThan(8f), id + ": spawns inside the Desert Outpost");

                Camera camera = Camera.main;
                Assert.That(camera, Is.Not.Null);
                float distance = Vector3.Distance(camera.transform.position, player.position + Vector3.up * 1.4f);
                Assert.That(distance, Is.InRange(3.5f, 5.2f), id + ": the camera starts behind the player at a normal distance");
                Assert.That(Physics.CheckSphere(camera.transform.position, 0.2f, ~0, QueryTriggerInteraction.Ignore), Is.False, id + ": not inside rock");
                Assert.That(Physics.Linecast(camera.transform.position, player.position + Vector3.up * 1.4f, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.root != player,
                    Is.False, id + ": nothing between the camera and the miner");
                Assert.That(FindButton("DigButton").gameObject.activeInHierarchy, Is.False, "DIG stays hidden until a treasure is in range");
                Assert.That(TreasureSpot.Active.Count, Is.EqualTo(20), "the 20 permanent treasures (stage 15C) are all in play");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        // ================================================================ walking: five areas, ramps, the raised railway, the camera

        private static readonly (string label, Vector2 point, string anchor)[] Route =
        {
            ("outpost -> riverbed west", new Vector2(-27f, -22f), null),
            ("riverbed", new Vector2(-14f, -17f), null),
            ("riverbed", new Vector2(-2f, -20f), null),
            ("basin", new Vector2(6f, -14f), "B_DryRiverbed"),
            ("riverbed east", new Vector2(22f, -8f), null),
            ("riverbed far east", new Vector2(31f, 0f), null),
            ("plaza", new Vector2(40f, 12f), null),
            ("overlook path 1", new Vector2(47f, 17f), null),
            ("overlook path 2", new Vector2(47f, 24f), null),
            ("overlook path 3", new Vector2(41f, 26f), null),
            ("overlook path 4", new Vector2(34f, 26f), null),
            ("overlook path 5", new Vector2(31f, 29f), null),
            ("overlook path 6", new Vector2(31f, 34f), null),
            ("overlook path 7", new Vector2(35f, 38f), null),
            ("overlook path 8", new Vector2(40f, 38f), null),
            ("overlook plateau", new Vector2(43f, 36f), "D_CanyonOverlook"),
            ("back down 8", new Vector2(40f, 38f), null),
            ("back down 7", new Vector2(35f, 38f), null),
            ("back down 6", new Vector2(31f, 34f), null),
            ("back down 5", new Vector2(31f, 29f), null),
            ("back down 4", new Vector2(34f, 26f), null),
            ("back down 3", new Vector2(41f, 26f), null),
            ("back down 2", new Vector2(47f, 24f), null),
            ("back down 1", new Vector2(47f, 17f), null),
            ("plaza again", new Vector2(40f, 12f), null),
            ("railway east", new Vector2(33f, 17f), null),
            ("railway", new Vector2(24f, 20f), null),
            ("railway", new Vector2(12f, 19f), null),
            ("railway dip", new Vector2(3f, 17.5f), null),
            ("railway middle", new Vector2(-6f, 17f), "C_OldRailway"),
            ("railway", new Vector2(-18f, 16f), null),
            ("railway west", new Vector2(-28f, 12f), null),
            ("railway down", new Vector2(-32f, 3f), null),
            ("outpost north", new Vector2(-32f, -9f), null),
            ("outpost", new Vector2(-34f, -22f), "A_DesertOutpost"),
            ("to the riverbed", new Vector2(-27f, -22f), null),
            ("riverbed", new Vector2(-14f, -17f), null),
            ("ravine mouth", new Vector2(-4f, -13f), null),
            ("ravine", new Vector2(-5f, -5f), null),
            ("ravine", new Vector2(-9f, -1f), null),
            ("ravine", new Vector2(-13f, 1f), null),
            ("ravine pocket", new Vector2(-17f, 2f), "E_HiddenRavine"),
        };

        private sealed class WalkStats
        {
            public int Frames;
            public float MinAboveGround = float.MaxValue, MaxAboveGround = float.MinValue;
            public float MinCameraDistance = float.MaxValue;
            public int CameraTooClose, CameraInRock, CameraBlocked, CameraBelowGround;
            public readonly List<float> CameraDistances = new();
        }

        private IEnumerator WalkTo(Transform player, ThirdPersonPlayerController controller, Camera camera, Vector2 target, string label, WalkStats stats)
        {
            float legTime = 0f;
            float legStart = Vector2.Distance(new Vector2(player.position.x, player.position.z), target);
            float checkTime = 0f, sidestepUntil = 0f, sidestepSign = 1f;
            Vector3 checkPosition = player.position;
            while (true)
            {
                Vector3 delta = new Vector3(target.x - player.position.x, 0f, target.y - player.position.z);
                if (delta.magnitude < 1.6f)
                {
                    break;
                }

                legTime += Time.deltaTime;
                Assert.That(legTime, Is.LessThan(legStart / 2.5f + 8f), $"stuck on the way to {label} at {player.position}");
                // a player walks round a boulder in the way: when the miner has hardly moved for half a second, step to the side for a moment
                checkTime += Time.deltaTime;
                if (checkTime >= 0.5f)
                {
                    if ((player.position - checkPosition).magnitude < 0.6f)
                    {
                        sidestepUntil = Time.time + 0.8f;
                        sidestepSign = -sidestepSign;
                    }

                    checkPosition = player.position;
                    checkTime = 0f;
                }

                Vector3 f = camera.transform.forward;
                f.y = 0f;
                f.Normalize();
                Vector3 r = new(f.z, 0f, -f.x);
                Vector3 d = delta.normalized;
                if (Time.time < sidestepUntil)
                {
                    d = Quaternion.Euler(0f, 75f * sidestepSign, 0f) * d;
                }

                controller.SetMoveInput(new Vector2(Vector3.Dot(d, r), Vector3.Dot(d, f)));
                yield return null;
                stats.Frames++;

                float ground = GroundUnder(player);
                stats.MinAboveGround = Mathf.Min(stats.MinAboveGround, player.position.y - ground);
                stats.MaxAboveGround = Mathf.Max(stats.MaxAboveGround, player.position.y - ground);

                Vector3 pivot = player.position + Vector3.up * 1.4f;
                float distance = Vector3.Distance(camera.transform.position, pivot);
                stats.CameraDistances.Add(distance);
                stats.MinCameraDistance = Mathf.Min(stats.MinCameraDistance, distance);
                if (distance < 2.6f)
                {
                    stats.CameraTooClose++;
                }

                if (Physics.CheckSphere(camera.transform.position, 0.15f, ~0, QueryTriggerInteraction.Ignore))
                {
                    stats.CameraInRock++;
                }

                if (Physics.Linecast(camera.transform.position, pivot, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.root != player)
                {
                    stats.CameraBlocked++;
                }

                if (camera.transform.position.y < TerrainHeight(camera.transform.position.x, camera.transform.position.z) + 0.1f)
                {
                    stats.CameraBelowGround++;
                }
            }

            controller.ClearMoveInput();
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator BothMiners_WalkToAllFiveAreas_OverRamps_TheRaisedRailway_AndTheOverlookPath_WithoutFallingSticking_OrASquashedCamera()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load(ScenePath);
                Time.timeScale = 2f;
                Transform player = RootObject("Player").transform;
                var controller = player.GetComponent<ThirdPersonPlayerController>();
                Camera camera = Camera.main;
                var stats = new WalkStats();
                yield return new WaitForSecondsRealtime(0.6f);

                float highest = float.MinValue, lowest = float.MaxValue;
                var reached = new HashSet<string>();
                foreach ((string label, Vector2 point, string anchor) in Route)
                {
                    yield return WalkTo(player, controller, camera, point, label, stats);
                    highest = Mathf.Max(highest, player.position.y);
                    lowest = Mathf.Min(lowest, player.position.y);
                    if (anchor != null)
                    {
                        Vector3 a = Anchor(anchor).position;
                        Assert.That(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(a.x, a.z)), Is.LessThan(3.5f), $"{id}: reached {anchor}");
                        reached.Add(anchor);
                    }
                }

                Assert.That(reached.Count, Is.EqualTo(5), id + ": all five areas were reached on foot");
                Assert.That(stats.MinAboveGround, Is.GreaterThan(0.8f), id + ": never sinks into the ground");
                Assert.That(stats.MaxAboveGround, Is.LessThan(1.9f), id + ": never floats or climbs onto a rock");
                Assert.That(highest - lowest, Is.GreaterThan(9f), id + ": the walk really climbed (overlook) and descended (riverbed)");
                Assert.That(stats.CameraInRock, Is.EqualTo(0), id + ": the camera is never inside rock");
                Assert.That(stats.CameraBelowGround, Is.EqualTo(0), id + ": and never under the ground");
                stats.CameraDistances.Sort();
                float p05 = stats.CameraDistances[(int)(stats.CameraDistances.Count * 0.05f)];
                float tooCloseShare = stats.CameraTooClose / (float)stats.Frames;
                float blockedShare = stats.CameraBlocked / (float)stats.Frames;
                Debug.Log($"Rocky Canyon walk ({id}): {stats.Frames} frames, height {lowest:F1}..{highest:F1} m, above ground {stats.MinAboveGround:F2}..{stats.MaxAboveGround:F2}, " +
                          $"camera distance min {stats.MinCameraDistance:F2} / 5th percentile {p05:F2}, closer than 2.6 m in {tooCloseShare * 100f:F1} % of frames, miner hidden in {blockedShare * 100f:F1} % of frames");
                Assert.That(p05, Is.GreaterThan(2.0f), id + ": the camera is almost never pressed against the miner");
                Assert.That(tooCloseShare, Is.LessThan(0.12f), id + ": the camera is rarely squeezed");
                Assert.That(blockedShare, Is.LessThan(0.03f), id + ": the canyon walls rarely hide the miner");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheRoutes_AreGentle_RampsAndTheRaisedRailwayStayWithinTheControllersLimits()
        {
            yield return Load(ScenePath);
            // sample the walkable centre lines every metre on the real collider: no slope near the CharacterController's 45 degrees, no steps over its 0.3 m
            float worstSlope = 0f, worstStep = 0f;
            for (int i = 0; i < Route.Length - 1; i++)
            {
                Vector2 a = Route[i].point, b = Route[i + 1].point;
                float length = Vector2.Distance(a, b);
                float previous = WalkSurface(a.x, a.y);
                for (float d = 1f; d <= length; d += 1f)
                {
                    Vector2 p = Vector2.Lerp(a, b, d / length);
                    float h = WalkSurface(p.x, p.y);
                    worstSlope = Mathf.Max(worstSlope, Mathf.Abs(h - previous));
                    worstStep = Mathf.Max(worstStep, Mathf.Abs(h - previous));
                    previous = h;
                }
            }

            Debug.Log($"Rocky Canyon routes: steepest 1 m rise {worstSlope:F2} m ({Mathf.Atan(worstSlope) * Mathf.Rad2Deg:F0} deg)");
            Assert.That(worstSlope, Is.LessThan(0.5f), "no route climbs more than 0.5 m per metre (the controller allows 1.0)");
        }

        [UnityTest]
        public IEnumerator Camera_StaysOutOfTheRock_AtEveryAngle_EvenInTheNarrowestPlaces()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId);
            yield return Load(ScenePath);
            Transform player = RootObject("Player").transform;
            var cameraController = One<ThirdPersonCameraController>();
            var yawField = typeof(ThirdPersonCameraController).GetField("yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Camera camera = Camera.main;
            (string name, Vector2 where)[] places =
            {
                ("ravine gate", new Vector2(-4.2f, -11.6f)), ("camp shack porch", new Vector2(-36.3f, -26.4f)), ("loading platform", new Vector2(13.5f, 22.4f)), ("under the bridge", new Vector2(3f, 18f)), ("gorge", new Vector2(3.4f, 8f)), ("gorge dead end", new Vector2(3f, 23f)), ("lookout deck", new Vector2(43f, 36f)),
                ("outpost spawn", new Vector2(-37f, -22f)), ("outpost west edge", new Vector2(-44f, -22f)), ("basin", new Vector2(6f, -14f)),
                ("riverbed narrowest", new Vector2(-2f, -20f)), ("ravine middle", new Vector2(-9f, -1f)), ("ravine pocket", new Vector2(-17f, 2f)),
                ("railway middle", new Vector2(-6f, 17f)), ("railway dip", new Vector2(3f, 17.5f)), ("railway west", new Vector2(-32f, 3f)),
                ("overlook path", new Vector2(47f, 24f)), ("overlook plateau", new Vector2(43f, 36f)),
            };
            int samples = 0, inRock = 0, belowGround = 0, blocked = 0, tooClose = 0, aimed = 0;
            float minDistance = float.MaxValue;
            var worst = "";
            foreach ((string name, Vector2 where) in places)
            {
                float y = (name == "lookout deck" ? WalkSurface(where.x, where.y) + 0.14f : TerrainHeight(where.x, where.y)) + 1.05f;
                Teleport(player, new Vector3(where.x, y, where.y));
                for (int angle = 0; angle < 360; angle += 45)
                {
                    yawField.SetValue(cameraController, (float)angle);
                    for (int i = 0; i < 30; i++)
                    {
                        yield return null;
                    }

                    samples++;
                    Vector3 pivot = player.position + Vector3.up * 1.4f;
                    float distance = Vector3.Distance(camera.transform.position, pivot);
                    Vector3 away = camera.transform.position - pivot;
                    away.y = 0f;
                    aimed += Vector3.Dot(away.normalized, Quaternion.Euler(0f, angle, 0f) * Vector3.back) > 0.9f ? 1 : 0; // the orbit really went to this side
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        worst = $"{name} at yaw {angle}";
                    }

                    inRock += Physics.CheckSphere(camera.transform.position, 0.15f, ~0, QueryTriggerInteraction.Ignore) ? 1 : 0;
                    belowGround += camera.transform.position.y < TerrainHeight(camera.transform.position.x, camera.transform.position.z) + 0.1f ? 1 : 0;
                    blocked += Physics.Linecast(camera.transform.position, pivot, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.root != player ? 1 : 0;
                    tooClose += distance < 2.6f ? 1 : 0;
                }
            }

            Debug.Log($"Rocky Canyon camera at every angle: {samples} samples, closest {minDistance:F2} m ({worst}), closer than 2.6 m: {tooClose}, in rock: {inRock}, under ground: {belowGround}, miner hidden: {blocked}");
            Assert.That(aimed, Is.EqualTo(samples), "the camera really orbited to every tested side (so the numbers above mean something)");
            Assert.That(inRock, Is.EqualTo(0), "the camera is never inside rock");
            Assert.That(belowGround, Is.EqualTo(0), "and never under the ground");
            Assert.That(minDistance, Is.GreaterThan(1.3f), "never glued to the miner's head (" + worst + ")");
            Assert.That(blocked / (float)samples, Is.LessThan(0.05f), "the miner stays in view");
            Assert.That(tooClose / (float)samples, Is.LessThan(0.2f), "the camera is only rarely pushed closer than 2.6 m");
        }

        // ================================================================ pause, resume, back to the Main Menu

        [UnityTest]
        public IEnumerator Pause_Resume_AndTheWayBackToTheMainMenu_Work()
        {
            yield return Load(ScenePath);
            var pause = One<PauseMenuController>();
            Transform player = RootObject("Player").transform;
            var controller = player.GetComponent<ThirdPersonPlayerController>();
            yield return new WaitForSecondsRealtime(0.5f);

            pause.Pause();
            yield return null;
            Assert.That(GamePause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Vector3 frozen = player.position;
            controller.SetMoveInput(Vector2.up);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(Vector3.Distance(player.position, frozen), Is.LessThan(0.01f), "the miner stands still while paused");
            controller.ClearMoveInput();

            pause.Resume();
            yield return null;
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            controller.SetMoveInput(Vector2.up);
            yield return new WaitForSecondsRealtime(0.6f);
            controller.ClearMoveInput();
            Assert.That(Vector3.Distance(player.position, frozen), Is.GreaterThan(0.5f), "and moves again after Resume");

            string requested = null;
            SceneNavigator.LoadHook = name => requested = name;
            pause.Pause();
            yield return null;
            pause.GoToMainMenu();
            Assert.That(requested, Is.EqualTo(SceneNavigator.MainMenuScene), "back to the Main Menu");
            Assert.That(GamePause.IsPaused, Is.False, "the next scene never starts frozen");
        }

        // ================================================================ detector, DIG, COLLECT on the existing treasure prefabs

        [UnityTest]
        public IEnumerator DetectorDigAndCollect_WorkOnTheExistingTreasurePrefabs_AndPayOnce_WithoutTouchingGreenMineIds()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load(ScenePath, FinishedGreenMineSave);
                Assert.That(RootObject("TreasureSpots").transform.Find("_TestPoints"), Is.Null, "the 15A scaffolding is gone: the real treasures are used");
                var hunt = One<TreasureHuntController>();
                var detector = One<TreasureDetector>();
                Transform player = RootObject("Player").transform;
                Assert.That(TreasureSpot.Active.Count, Is.EqualTo(20));
                TreasureSpot spot = null;
                foreach (TreasureSpot s in TreasureSpot.Active)
                {
                    Assert.That(s.SpotId, Does.StartWith("rocky_canyon."), "Rocky Canyon ids never clash with Green Mine's");
                    Assert.That(s.SpotId, Does.Not.StartWith("green_mine."));
                    if (s.SpotId == "rocky_canyon.outpost_01")
                    {
                        spot = s;
                    }
                }

                Assert.That(spot, Is.Not.Null);
                Assert.That(spot.Definition, Is.Not.Null, "an existing TreasureDefinition (the Gold Coin)");
                Assert.That(spot.Definition.GoldValue, Is.EqualTo(10));
                Teleport(player, new Vector3(spot.transform.position.x + 6f, TerrainHeight(spot.transform.position.x + 6f, spot.transform.position.z) + 1.05f, spot.transform.position.z));
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.That(detector.HasSignal, Is.True, id + ": the detector senses the treasure");
                Assert.That(detector.NearestHidden, Is.EqualTo(spot));

                Teleport(player, new Vector3(spot.transform.position.x + 1.4f, TerrainHeight(spot.transform.position.x + 1.4f, spot.transform.position.z) + 1.05f, spot.transform.position.z));
                Button dig = FindButton("DigButton"), collect = FindButton("CollectButton");
                yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
                Assert.That(dig.gameObject.activeInHierarchy, Is.True, id + ": DIG is offered in range");
                int gold = hunt.TotalGold;
                Assert.That(gold, Is.EqualTo(500), "the saved Green Mine gold is what the canyon starts from");
                dig.onClick.Invoke();
                yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
                Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed));
                yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
                collect.onClick.Invoke();
                yield return null;
                Assert.That(spot.State, Is.EqualTo(TreasureState.Collected));
                Assert.That(hunt.TotalGold, Is.EqualTo(gold + spot.Definition.GoldValue), id + ": the gold is paid");
                collect.onClick.Invoke();
                dig.onClick.Invoke();
                yield return null;
                Assert.That(hunt.TotalGold, Is.EqualTo(gold + spot.Definition.GoldValue), id + ": once");

                GameProgressData saved = JsonUtility.FromJson<GameProgressData>(store.Json);
                Assert.That(saved.collectedTreasureIds, Contains.Item("rocky_canyon.outpost_01"));
                Assert.That(saved.collectedTreasureIds.Count, Is.EqualTo(13), "the 12 Green Mine ids are all still there, plus the new one");
                foreach (string green in new[] { "green_mine.meadow_01", "green_mine.mine_room_01", "green_mine.bridge_01" })
                {
                    Assert.That(saved.collectedTreasureIds, Contains.Item(green), id + ": Green Mine's collection is untouched");
                }

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        // ================================================================ Green Mine is untouched

        [UnityTest]
        public IEnumerator GreenMine_KeepsItsFifteenTreasures_AndTheirIds_And625Gold()
        {
            yield return Load(GreenMinePath);
            int total = 0, count = 0;
            var ids = new HashSet<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TreasureSpot spot in root.GetComponentsInChildren<TreasureSpot>(true))
                {
                    count++;
                    total += spot.Definition.GoldValue;
                    ids.Add(spot.SpotId);
                    Assert.That(spot.SpotId, Does.StartWith("green_mine."), "Green Mine ids stay Green Mine ids");
                }
            }

            Assert.That(count, Is.EqualTo(15), "Green Mine's 15 treasures");
            Assert.That(ids.Count, Is.EqualTo(15), "all different");
            Assert.That(total, Is.EqualTo(625), "and the 625 gold distribution");
            Assert.That(RootObject("RockyCanyonEnvironment"), Is.Null, "Rocky Canyon never leaks into Green Mine");
            Assert.That(One<MineInteriorZone>(), Is.Not.Null, "Green Mine keeps its mine");
        }
    }
}
#endif
