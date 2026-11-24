#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using ItemCollector3D.UI;
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
    /// Ancient Mine foundation (stage 16A): the scene is wired into the game (Area Selection card, build settings, the 1200 gold lock), reuses the working Green Mine systems, both miners
    /// spawn correctly, all five areas and both routes can be WALKED (ramps, daises and the secret side places included), the camera stays inside the cave (never in a wall, a pillar or the
    /// ceiling), the player cannot pass through walls, pillars and arches, Pause / Resume / back to the Main Menu work, and the detector and DIG / COLLECT work on a temporary isolated
    /// treasure spot (the real Ancient Mine treasures come in stage 16C). Green Mine's 15 treasures / 625 gold and Rocky Canyon's 20 treasures / 840 gold are untouched. In-memory stores
    /// everywhere; the developer's real save file is hashed before the first and after the last test.
    /// </summary>
    public sealed class AncientMinePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/AncientMine.unity";
        private const string GreenMinePath = "Assets/Scenes/GreenMine.unity";
        private const string RockyPath = "Assets/Scenes/RockyCanyon.unity";

        private static readonly string[] ZoneNames = { "A_AncientGate", "B_HallOfPillars", "C_CrystalCaverns", "D_ForgottenWorkshop", "E_RoyalVault" };
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

        private Transform Env => RootObject("AncientMineEnvironment").transform;

        private Transform Anchor(string zone)
        {
            Transform t = Env.Find(zone + "/Anchor");
            Assert.That(t, Is.Not.Null, "zone anchor " + zone);
            return t;
        }

        private Vector3[] RoutePoints(string group)
        {
            Transform g = Env.Find("Markers/Routes/" + group);
            Assert.That(g, Is.Not.Null, "route markers " + group);
            var list = new List<Vector3>();
            foreach (Transform t in g)
            {
                list.Add(t.position);
            }

            Assert.That(list.Count, Is.GreaterThanOrEqualTo(3), group + " has waypoints");
            return list.ToArray();
        }

        private IEnumerable<string> SideRouteGroups()
        {
            foreach (Transform t in Env.Find("Markers/Routes"))
            {
                if (t.name.StartsWith("Side_"))
                {
                    yield return t.name;
                }
            }
        }

        /// <summary>The walkable floor at (x, z): the cave's floor mesh only (props and the safety floor are ignored). NaN where there is none.</summary>
        private static float FloorY(float x, float z)
        {
            float best = float.NaN;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.name == "Floor" && (float.IsNaN(best) || hit.point.y > best))
                {
                    best = hit.point.y;
                }
            }

            return best;
        }

        /// <summary>True when a point lies inside the cave: a ceiling above it and a real floor (not the safety floor) below it. A point inside rock, outside the map or above the ceiling fails.</summary>
        private static bool InsideCave(Vector3 p, out string why)
        {
            bool ceiling = Physics.Raycast(p, Vector3.up, out RaycastHit up, 80f, ~0, QueryTriggerInteraction.Ignore);
            bool floor = Physics.Raycast(p, Vector3.down, out RaycastHit down, 80f, ~0, QueryTriggerInteraction.Ignore);
            why = null;
            if (!ceiling)
            {
                why = "nothing above (outside the ceiling)";
                return false;
            }

            if (!floor)
            {
                why = "nothing below";
                return false;
            }

            if (down.collider.name == "SafetyFloor")
            {
                why = "only the safety floor below (outside the cave floor)";
                return false;
            }

            if (up.collider.name.StartsWith("Wall_"))
            {
                why = "boundary wall above";
                return false;
            }

            return true;
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
            Physics.SyncTransforms();
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
        public void TheScene_IsInTheBuildSettingsAndTheIosProfile_AndTheAncientMineCardPointsAtIt()
        {
            Assert.That(File.Exists(ScenePath), Is.True);
            Assert.That(File.Exists(ScenePath + ".meta"), Is.True, "the scene has its .meta file");
            bool inBuild = false;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                inBuild |= s.path == ScenePath && s.enabled;
            }

            Assert.That(inBuild, Is.True, "AncientMine.unity is in the Build Settings scene list");
            var profile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>("Assets/Settings/Build Profiles/iOS.asset");
            Assert.That(profile, Is.Not.Null);
            bool inProfile = false;
            foreach (EditorBuildSettingsScene s in profile.scenes)
            {
                inProfile |= s.path == ScenePath && s.enabled;
            }

            Assert.That(inProfile, Is.True, "and in the iOS build profile");
            Assert.That(AssetDatabase.FindAssets("t:BuildProfile", new[] { "Assets/Settings/Build Profiles" }).Length, Is.EqualTo(1), "iOS is the only build profile (there is no Android profile to update)");
            Assert.That(SceneNavigator.AncientMineScene, Is.EqualTo("AncientMine"));

            var menu = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Menus/MainMenuUI.prefab");
            var cards = new Dictionary<string, string>();
            foreach (AreaCardView card in menu.GetComponentsInChildren<AreaCardView>(true))
            {
                cards[card.AreaId] = card.SceneName;
            }

            Assert.That(cards["ancient_mine"], Is.EqualTo("AncientMine"), "the Area Selection card opens AncientMine.unity (no COMING SOON)");
            Assert.That(cards["green_mine"], Is.EqualTo("GreenMine"));
            Assert.That(cards["rocky_canyon"], Is.EqualTo("RockyCanyon"));
        }

        [Test]
        public void AncientMine_IsLockedAt1199Gold_OpensAt1200_AndHasItsScene()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AreaCatalog>("Assets/Data/Areas/AreaCatalog.asset");
            Assert.That(catalog.Get("green_mine").unlockTotalGold, Is.EqualTo(0), "Green Mine is open from the start");
            Assert.That(catalog.Get("rocky_canyon").unlockTotalGold, Is.EqualTo(500), "Rocky Canyon opens at 500 total gold");
            Assert.That(catalog.Get("ancient_mine").unlockTotalGold, Is.EqualTo(1200), "Ancient Mine opens at 1200 total gold");
            var memory = new MemoryProgressStore();
            ProgressService service = ProgressService.Create(catalog, memory);
            Assert.That(AreaSelectController.Evaluate(service, "ancient_mine", SceneNavigator.AncientMineScene).Availability, Is.EqualTo(AreaAvailability.Locked), "0 gold");
            service.RegisterCollected("a", 1199);
            Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.False, "1199 gold keeps it locked");
            Assert.That(AreaSelectController.Evaluate(service, "ancient_mine", SceneNavigator.AncientMineScene).Availability, Is.EqualTo(AreaAvailability.Locked));
            Assert.That(AreaSelectController.Evaluate(service, "ancient_mine", SceneNavigator.AncientMineScene).GoldText, Is.EqualTo("1 GOLD TO UNLOCK"));
            service.RegisterCollected("b", 1);
            Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.True, "1200 gold opens it");
            AreaCardState open = AreaSelectController.Evaluate(service, "ancient_mine", SceneNavigator.AncientMineScene);
            Assert.That(open.Availability, Is.EqualTo(AreaAvailability.Playable), "open and with a scene: PLAY, not COMING SOON");
            Assert.That(open.StatusText, Is.EqualTo("PLAY"));
            Assert.That(AreaSelectController.Evaluate(ProgressService.Create(catalog, memory), "ancient_mine", SceneNavigator.AncientMineScene).Availability, Is.EqualTo(AreaAvailability.Playable), "the state survives a reload of the save");
            Assert.That(catalog.Get("ancient_mine").goalTotalGold, Is.EqualTo(2500), "the catalog's goal is unchanged (stage 16C decides the treasures)");
        }

        // ================================================================ structure and budget

        [UnityTest]
        public IEnumerator TheScene_Loads_WithTheDocumentedHierarchy_ReusesTheSystems_AndHasNoTreasuresYet()
        {
            var seed = new MemoryProgressStore();
            ProgressService.Create(AssetDatabase.LoadAssetAtPath<AreaCatalog>("Assets/Data/Areas/AreaCatalog.asset"), seed).RegisterCollected("seed", 1200);
            yield return Load(ScenePath, seed.Json);
            foreach (string name in new[] { "Player", "GameplaySystems", "AncientMineEnvironment", "TreasureSpots" })
            {
                Assert.That(RootObject(name), Is.Not.Null, "root object " + name);
            }

            Assert.That(RootObject("GreenMineEnvironment"), Is.Null, "no Green Mine scenery");
            Assert.That(RootObject("RockyCanyonEnvironment"), Is.Null, "no Rocky Canyon scenery");
            Assert.That(RootObject("CanyonAtmosphere"), Is.Null, "no canyon atmosphere");
            Assert.That(One<TreasureHuntController>(), Is.Not.Null);
            Assert.That(One<TreasureDetector>(), Is.Not.Null);
            Assert.That(One<ThirdPersonCameraController>(), Is.Not.Null);
            Assert.That(One<ThirdPersonPlayerController>(), Is.Not.Null);
            Assert.That(One<RewardFeedbackPresenter>(), Is.Not.Null);
            Assert.That(One<DigFeedbackPresenter>(), Is.Not.Null);
            Assert.That(One<PauseMenuController>(), Is.Not.Null);
            Assert.That(One<VirtualJoystick>(), Is.Not.Null, "the mobile joystick");
            Assert.That(One<AmbientMusicPlayer>(), Is.Not.Null, "the music keeps playing");
            Assert.That(FindButton("DigButton"), Is.Not.Null);
            Assert.That(FindButton("CollectButton"), Is.Not.Null);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Assert.That(root.GetComponentInChildren<WaterMotion>(true), Is.Null, "no Green Mine water");
                Assert.That(root.GetComponentInChildren<AreaAmbience>(true), Is.Null, "no Green Mine forest ambience");
                Assert.That(root.GetComponentInChildren<CanyonAmbience>(true), Is.Null, "no canyon desert ambience");
            }

            // the five areas each have their own root with an Anchor, the cave shell (Floor, Ceiling), Props and Colliders; the treasure root is separate with an EMPTY group per area
            foreach (string zone in ZoneNames)
            {
                Assert.That(Env.Find(zone), Is.Not.Null, zone);
                foreach (string child in new[] { "Anchor", "Floor", "Ceiling", "Props/Structures", "Colliders" })
                {
                    Assert.That(Env.Find(zone + "/" + child), Is.Not.Null, zone + "/" + child);
                }

                Transform group = RootObject("TreasureSpots").transform.Find(zone);
                Assert.That(group, Is.Not.Null, "TreasureSpots/" + zone);
                Assert.That(group.childCount, Is.EqualTo(0), "no treasure yet in " + zone + " (stage 16C)");
            }

            Assert.That(RootObject("TreasureSpots").transform.Find("_TestPoints"), Is.Null, "no test points left in the scene");
            Assert.That(RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true).Length, Is.EqualTo(0));
            Assert.That(TreasureSpot.Active.Count, Is.EqualTo(0), "no treasure spot is in play");
            Assert.That(Env.Find("Boundaries"), Is.Not.Null);
            Assert.That(Env.Find("Lighting/Sun"), Is.Not.Null);
            Assert.That(Env.Find("Markers/PlayerSpawn"), Is.Not.Null);

            // the HUD speaks Ancient Mine, from the catalog: the title and the goal (cumulative gold, 2500); the saved gold is the 1200 that opened the area
            var hunt = One<TreasureHuntController>();
            Assert.That(hunt.AreaId, Is.EqualTo("ancient_mine"));
            Assert.That(hunt.IsAreaAccessible, Is.True, "1200 gold: the area is open");
            bool title = false, gold = false;
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (text.gameObject.scene != scene)
                {
                    continue;
                }

                title |= text.text == "ANCIENT MINE";
                gold |= Regex.IsMatch(text.text, @"^1200 / 2500 GOLD$");
                Assert.That(text.text, Is.Not.EqualTo("GREEN MINE"), "no leftover Green Mine title");
            }

            Assert.That(title, Is.True, "the HUD shows ANCIENT MINE");
            Assert.That(gold, Is.True, "and the saved 1200 gold against the 2500 goal");
        }

        [UnityTest]
        public IEnumerator TheScene_IsLockedForALowGoldSave()
        {
            yield return Load(ScenePath);
            Assert.That(One<TreasureHuntController>().IsAreaAccessible, Is.False, "0 gold: the area's lock is still in force in the scene (1200 needed)");
        }

        [UnityTest]
        public IEnumerator TheEnvironment_StaysWithinAMobileBudget_EveryRendererIsStatic_AndTheMapIsBig()
        {
            yield return Load(ScenePath);
            Transform env = Env;
            int renderers = 0, triangles = 0, colliders = 0, meshColliders = 0;
            var materials = new HashSet<Material>();
            Bounds shell = default;
            bool first = true;
            foreach (MeshRenderer r in env.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                foreach (Material m in r.sharedMaterials)
                {
                    materials.Add(m);
                    Assert.That(m.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), r.name + ": URP Lit");
                }

                Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    triangles += (int)mesh.GetIndexCount(s) / 3;
                }

                Assert.That(GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.BatchingStatic), Is.True, r.name + " is batching static");
                if (r.name == "Floor" || r.name == "Ceiling")
                {
                    if (first)
                    {
                        shell = r.bounds;
                        first = false;
                    }
                    else
                    {
                        shell.Encapsulate(r.bounds);
                    }

                    Assert.That(r.gameObject.GetComponent<MeshCollider>(), Is.Not.Null, r.name + " has its collider");
                    Assert.That(r.shadowCastingMode, Is.EqualTo(r.name == "Ceiling" ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On), r.name + " shadow casting");
                }
            }

            foreach (Collider c in env.GetComponentsInChildren<Collider>(true))
            {
                colliders++;
                meshColliders += c is MeshCollider ? 1 : 0;
            }

            Assert.That(renderers, Is.LessThan(40), "renderers (each area: floor, ceiling and ONE merged props mesh; no single giant mesh)");
            Assert.That(materials.Count, Is.LessThanOrEqualTo(3), "materials: palette and glow");
            Assert.That(triangles, Is.LessThan(80000), "triangles of the whole environment");
            Assert.That(colliders, Is.LessThan(220), "colliders (only what blocks the player or the camera)");
            Assert.That(meshColliders, Is.EqualTo(10), "one floor and one ceiling collider per area");
            int lights = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light l in root.GetComponentsInChildren<Light>(true))
                {
                    if (!l.enabled || !l.gameObject.activeInHierarchy)
                    {
                        continue; // the miner's headlamp and the treasure glow are existing gameplay lights that are off until used
                    }

                    lights++;
                    Assert.That(l.type, Is.EqualTo(LightType.Directional), "no real-time point or spot lights: " + l.name);
                }
            }

            Assert.That(lights, Is.EqualTo(1), "one real-time light only (the directional light)");
            Assert.That(env.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(1), "the environment itself holds just that one light");
            Assert.That(shell.size.x, Is.InRange(115f, 140f), "the cave is about 120 m wide");
            Assert.That(shell.size.z, Is.InRange(98f, 125f), "and about 100 m deep (Rocky Canyon: 96 x 75 m)");

            // the walkable ground: the markers span more than Rocky Canyon's 96 x 75 m, and the floors differ by more than 15 m
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Transform m in env.Find("Markers/Routes").GetComponentsInChildren<Transform>())
            {
                if (m.childCount > 0)
                {
                    continue;
                }

                minX = Mathf.Min(minX, m.position.x);
                maxX = Mathf.Max(maxX, m.position.x);
                minZ = Mathf.Min(minZ, m.position.z);
                maxZ = Mathf.Max(maxZ, m.position.z);
                minY = Mathf.Min(minY, m.position.y);
                maxY = Mathf.Max(maxY, m.position.y);
            }

            Debug.Log($"Ancient Mine: {renderers} renderers, {materials.Count} materials, {triangles} triangles, {colliders} colliders ({meshColliders} mesh); cave {shell.size}; route markers x {maxX - minX:F0} m, z {maxZ - minZ:F0} m, floor {minY:F1}..{maxY:F1} m");
            Assert.That(maxX - minX, Is.GreaterThan(105f), "walkable width");
            Assert.That(maxZ - minZ, Is.GreaterThan(90f), "walkable depth");
            Assert.That(maxY - minY, Is.GreaterThan(15f), "the routes climb and descend more than 15 m");
            Assert.That(shell.size.y, Is.GreaterThan(30f), "high ceilings: the cave is more than 30 m tall");
        }

        // ================================================================ both miners, camera, HUD

        [UnityTest]
        public IEnumerator BothMiners_SpawnInTheAncientGate_OnTheFloor_WithTheRightModel_CameraAndHud()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load(ScenePath);
                Transform player = RootObject("Player").transform;
                Assert.That(player.GetComponent<PlayerCharacterVisual>().AppliedId, Is.EqualTo(id), "the chosen miner is the one in the mine");
                Assert.That(player.GetComponentInChildren<MinerAnimator>(true), Is.Not.Null);
                yield return new WaitForSecondsRealtime(1.2f);
                float floor = FloorY(player.position.x, player.position.z);
                Assert.That(player.position.y - floor, Is.InRange(0.9f, 1.4f), id + ": standing on the floor, not in it or above it");
                Vector3 a = Anchor("A_AncientGate").position;
                Assert.That(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(a.x, a.z)), Is.LessThan(9f), id + ": spawns inside the Ancient Gate");
                Assert.That(InsideCave(player.position + Vector3.up, out string why), Is.True, why);

                // wide empty space round the spawn: nothing solid within 4 m of the miner (spawn and camera area)
                Assert.That(Offenders(player.position, 4f), Is.Empty, id + ": clear space (4 m) round the spawn");

                Camera camera = Camera.main;
                Assert.That(camera, Is.Not.Null);
                float distance = Vector3.Distance(camera.transform.position, player.position + Vector3.up * 1.4f);
                Assert.That(distance, Is.InRange(3.4f, 5.2f), id + ": the camera starts behind the player at a normal distance");
                Assert.That(Physics.CheckSphere(camera.transform.position, 0.2f, ~0, QueryTriggerInteraction.Ignore), Is.False, id + ": not inside a wall");
                Assert.That(InsideCave(camera.transform.position, out string cameraWhy), Is.True, "camera: " + cameraWhy);
                Assert.That(Physics.Linecast(camera.transform.position, player.position + Vector3.up * 1.4f, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.root != player,
                    Is.False, id + ": nothing between the camera and the miner");
                Assert.That(FindButton("DigButton").gameObject.activeInHierarchy, Is.False, "DIG stays hidden until a treasure is in range");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        /// <summary>True when something other than the cave floor (and the player) overlaps a miner standing at <paramref name="centre"/> (the capsule centre).</summary>
        private static bool Blocked(Vector3 centre)
        {
            foreach (Collider c in Physics.OverlapCapsule(centre + Vector3.down * 0.5f, centre + Vector3.up * 0.5f, 0.55f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.name != "Floor" && c.transform.root.name != "Player")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The names of the colliders within the radius that are neither the cave floor nor the player.</summary>
        private static string Offenders(Vector3 centre, float radius)
        {
            var names = new List<string>();
            foreach (Collider c in Physics.OverlapSphere(centre, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.name != "Floor" && c.transform.root.name != "Player")
                {
                    names.Add(c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name);
                }
            }

            return string.Join(", ", names);
        }

        /// <summary>True when every collider within the radius is the cave's own floor (so nothing else stands near).</summary>
        private static bool OnlyFloor(Vector3 centre, float radius)
        {
            foreach (Collider c in Physics.OverlapSphere(centre, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.name != "Floor" && c.transform.root.name != "Player")
                {
                    return false;
                }
            }

            return true;
        }

        // ================================================================ walking: five areas, both routes, ramps, platforms, the secret places

        private sealed class WalkStats
        {
            public int Frames;
            public float MinAboveGround = float.MaxValue, MaxAboveGround = float.MinValue;
            public float MinCameraDistance = float.MaxValue;
            public int CameraTooClose, CameraInWall, CameraOutsideCave, CameraBlocked, PlayerOutsideCave;
            public readonly List<float> CameraDistances = new();
            public float Highest = float.MinValue, Lowest = float.MaxValue;
            public string FirstWall = "", FirstOutside = "";
        }

        private IEnumerator WalkTo(Transform player, ThirdPersonPlayerController controller, Camera camera, Vector3 target, string label, WalkStats stats)
        {
            Vector2 goal = new(target.x, target.z);
            float legTime = 0f;
            float legStart = Vector2.Distance(new Vector2(player.position.x, player.position.z), goal);
            float checkTime = 0f, sidestepUntil = 0f, sidestepSign = 1f;
            Vector3 checkPosition = player.position;
            while (true)
            {
                Vector3 delta = new Vector3(goal.x - player.position.x, 0f, goal.y - player.position.z);
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
                stats.Highest = Mathf.Max(stats.Highest, player.position.y);
                stats.Lowest = Mathf.Min(stats.Lowest, player.position.y);
                if (!InsideCave(player.position + Vector3.up * 0.5f, out _))
                {
                    stats.PlayerOutsideCave++;
                }

                Vector3 pivot = player.position + Vector3.up * 1.4f;
                float distance = Vector3.Distance(camera.transform.position, pivot);
                stats.CameraDistances.Add(distance);
                stats.MinCameraDistance = Mathf.Min(stats.MinCameraDistance, distance);
                if (distance < 2.6f)
                {
                    stats.CameraTooClose++;
                }

                if (Physics.CheckSphere(camera.transform.position, 0.05f, ~0, QueryTriggerInteraction.Ignore)) // a real penetration (the smoothed path may graze a pillar for a frame at 0.05 m)
                {
                    stats.CameraInWall++;
                    if (stats.FirstWall == "")
                    {
                        var names = new List<string>();
                        foreach (Collider c in Physics.OverlapSphere(camera.transform.position, 0.05f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            names.Add(c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name);
                        }

                        stats.FirstWall = $"{label}: camera {camera.transform.position} touches {string.Join(", ", names)}, player {player.position}";
                    }
                }

                if (!InsideCave(camera.transform.position, out string outsideWhy))
                {
                    stats.CameraOutsideCave++;
                    if (stats.FirstOutside == "")
                    {
                        stats.FirstOutside = $"{label}: camera {camera.transform.position} ({outsideWhy}), player {player.position}";
                    }
                }

                if (Physics.Linecast(camera.transform.position, pivot, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.root != player)
                {
                    stats.CameraBlocked++;
                }
            }

            controller.ClearMoveInput();
        }

        private IEnumerator WalkRoute(Transform player, ThirdPersonPlayerController controller, Camera camera, Vector3[] points, string label, WalkStats stats, List<string> reached = null)
        {
            for (int i = 0; i < points.Length; i++)
            {
                yield return WalkTo(player, controller, camera, points[i], $"{label} #{i}", stats);
                reached?.Add(label + "/" + i);
            }
        }

        private void AssertWalkStats(string who, WalkStats stats)
        {
            Assert.That(stats.MinAboveGround, Is.GreaterThan(0.8f), who + ": never sinks into the floor");
            Assert.That(stats.MaxAboveGround, Is.LessThan(2.0f), who + ": never floats or climbs onto a prop");
            Assert.That(stats.PlayerOutsideCave, Is.EqualTo(0), who + ": the miner never leaves the cave volume (no clipping through a wall, no falling under the floor)");
            Assert.That(stats.CameraInWall, Is.EqualTo(0), who + ": the camera is never inside a wall, pillar or ceiling. First: " + stats.FirstWall);
            Assert.That(stats.CameraOutsideCave, Is.EqualTo(0), who + ": the camera is never outside the cave volume (above the ceiling or in the rock). First: " + stats.FirstOutside);
            stats.CameraDistances.Sort();
            float p05 = stats.CameraDistances[(int)(stats.CameraDistances.Count * 0.05f)];
            float tooCloseShare = stats.CameraTooClose / (float)stats.Frames;
            float blockedShare = stats.CameraBlocked / (float)stats.Frames;
            Debug.Log($"Ancient Mine walk ({who}): {stats.Frames} frames, height {stats.Lowest:F1}..{stats.Highest:F1} m, above ground {stats.MinAboveGround:F2}..{stats.MaxAboveGround:F2}, " +
                      $"camera distance min {stats.MinCameraDistance:F2} / 5th percentile {p05:F2}, closer than 2.6 m in {tooCloseShare * 100f:F1} % of frames, miner hidden in {blockedShare * 100f:F1} % of frames");
            Assert.That(stats.MinCameraDistance, Is.GreaterThan(1.3f), who + ": never glued to the miner's head");
            Assert.That(p05, Is.GreaterThan(2.0f), who + ": the camera is almost never pressed against the miner");
            Assert.That(tooCloseShare, Is.LessThan(0.12f), who + ": the camera is rarely squeezed");
            Assert.That(blockedShare, Is.LessThan(0.04f), who + ": walls and pillars rarely hide the miner");
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator Barnaby_WalksTheMainRoute_ThroughFourAreas_ThenTheAlternateRoute_ThroughTheWorkshop_AndClosesTheLoop()
        {
            CharacterSelection.Select(CharacterSelection.MaleId);
            yield return Load(ScenePath);
            Time.timeScale = 2f;
            Transform player = RootObject("Player").transform;
            var controller = player.GetComponent<ThirdPersonPlayerController>();
            Camera camera = Camera.main;
            var stats = new WalkStats();
            yield return new WaitForSecondsRealtime(1.2f);

            // ---- the main route: Ancient Gate -> Hall of Pillars -> Crystal Caverns -> Royal Vault
            var reached = new List<string>();
            Vector3[] main = RoutePoints("Main");
            for (int i = 0; i < main.Length; i++)
            {
                yield return WalkTo(player, controller, camera, main[i], "main #" + i, stats);
                reached.Add("main/" + i);
                foreach (string zone in new[] { "A_AncientGate", "B_HallOfPillars", "C_CrystalCaverns", "E_RoyalVault" })
                {
                    Vector3 a = Anchor(zone).position;
                    if (Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(a.x, a.z)) < 3.5f)
                    {
                        reached.Add(zone);
                    }
                }
            }

            foreach (string zone in new[] { "A_AncientGate", "B_HallOfPillars", "C_CrystalCaverns", "E_RoyalVault" })
            {
                Assert.That(reached, Contains.Item(zone), "the main route reaches " + zone + " on foot");
            }

            float vaultHeight = player.position.y;
            Assert.That(vaultHeight, Is.LessThan(5f), "the vault is the deepest place");

            // ---- the alternate route: Hall -> Workshop -> Crystal Caverns -> back to the Hall (a loop)
            Teleport(player, new Vector3(0f, FloorY(0f, 0f) + 1.05f, 0f));
            yield return new WaitForSecondsRealtime(0.5f);
            Vector3[] alternate = RoutePoints("Alternate");
            bool workshop = false;
            for (int i = 0; i < alternate.Length; i++)
            {
                yield return WalkTo(player, controller, camera, alternate[i], "alternate #" + i, stats);
                Vector3 d = Anchor("D_ForgottenWorkshop").position;
                workshop |= Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(d.x, d.z)) < 3.5f;
            }

            Assert.That(workshop, Is.True, "the alternate route reaches the Forgotten Workshop");
            Assert.That(Vector2.Distance(new Vector2(player.position.x, player.position.z), Vector2.zero), Is.LessThan(3.5f), "and the loop closes: it ends back in the Hall of Pillars (via the Crystal Caverns and the east door)");
            Assert.That(stats.Highest - stats.Lowest, Is.GreaterThan(8f), "the walk really climbed and descended");
            AssertWalkStats("Barnaby main + alternate", stats);
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator Ruby_WalksTheMainRoute_AndEverySecretSidePlace_AndTheRaisedDaises()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId);
            yield return Load(ScenePath);
            Time.timeScale = 2f;
            Transform player = RootObject("Player").transform;
            var controller = player.GetComponent<ThirdPersonPlayerController>();
            Camera camera = Camera.main;
            var stats = new WalkStats();
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(player.GetComponent<PlayerCharacterVisual>().AppliedId, Is.EqualTo(CharacterSelection.FemaleId));

            Vector3[] main = RoutePoints("Main");
            yield return WalkRoute(player, controller, camera, main, "main", stats);
            Vector3 vault = Anchor("E_RoyalVault").position;
            Assert.That(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(vault.x, vault.z)), Is.LessThan(3.5f), "Ruby reaches the Royal Vault too");

            // each side route starts on a route point: the miner is put there (the walk to it is the main / alternate route), then walks the side route to its end
            var places = new List<string>();
            foreach (string group in SideRouteGroups())
            {
                Vector3[] points = RoutePoints(group);
                Teleport(player, new Vector3(points[0].x, FloorY(points[0].x, points[0].z) + 1.05f, points[0].z));
                yield return new WaitForSecondsRealtime(0.4f);
                yield return WalkRoute(player, controller, camera, points, group, stats);
                Vector3 end = points[points.Length - 1];
                Assert.That(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(end.x, end.z)), Is.LessThan(2.2f), group + ": reached on foot");
                places.Add(group);
            }

            Assert.That(places.Count, Is.EqualTo(8), "cistern, grotto, cache chamber, collapsed spur, two daises, the vault alcove and the vault's north-west corner");
            AssertWalkStats("Ruby main + side places", stats);
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator Ruby_ReachesTheRoyalVault_AndWalksAllTheWayBackToTheAncientGate()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId);
            yield return Load(ScenePath);
            Time.timeScale = 2f;
            Transform player = RootObject("Player").transform;
            var controller = player.GetComponent<ThirdPersonPlayerController>();
            Camera camera = Camera.main;
            var stats = new WalkStats();
            yield return new WaitForSecondsRealtime(1.2f);

            // from the vault's dais (the end of the main route) back along the main route, in reverse, to the gate where the miner started
            Vector3[] main = RoutePoints("Main");
            Teleport(player, new Vector3(main[main.Length - 1].x, FloorY(main[main.Length - 1].x, main[main.Length - 1].z) + 1.05f, main[main.Length - 1].z));
            yield return new WaitForSecondsRealtime(0.5f);
            var back = new List<Vector3>(main);
            back.Reverse();
            yield return WalkRoute(player, controller, camera, back.ToArray(), "back", stats);
            Vector3 gate = Anchor("A_AncientGate").position;
            Assert.That(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(main[0].x, main[0].z)), Is.LessThan(3.5f), "Ruby is back where she started, in the Ancient Gate");
            Assert.That(Vector2.Distance(new Vector2(main[0].x, main[0].z), new Vector2(gate.x, gate.z)), Is.LessThan(9f));
            Assert.That(player.position.y, Is.GreaterThan(14f), "and on the gate's level again (about 15.5 m above the vault)");
            AssertWalkStats("Ruby way back", stats);
        }

        [UnityTest]
        public IEnumerator TheRoutes_AreGentle_RampsAndPlatformsStayWithinTheControllersLimits_AndAreWideEnough()
        {
            yield return Load(ScenePath);
            var groups = new List<string> { "Main", "Alternate" };
            groups.AddRange(SideRouteGroups());
            float worstSlope = 0f, narrowest = float.MaxValue, alcoveNarrowest = float.MaxValue, narrowestMain = float.MaxValue, daisNarrowest = float.MaxValue;
            string mainWhere = "";
            string worstGroup = "", narrowGroup = "", narrowWhat = "";
            int samples = 0;
            foreach (string group in groups)
            {
                Vector3[] points = RoutePoints(group);
                for (int i = 0; i < points.Length - 1; i++)
                {
                    Vector2 a = new(points[i].x, points[i].z), b = new(points[i + 1].x, points[i + 1].z);
                    float length = Vector2.Distance(a, b);
                    float previous = FloorY(a.x, a.y);
                    Assert.That(float.IsNaN(previous), Is.False, $"{group} #{i}: there is a floor under the route");
                    for (float d = 0.5f; d <= length; d += 0.5f)
                    {
                        Vector2 p = Vector2.Lerp(a, b, d / length);
                        float h = FloorY(p.x, p.y);
                        Assert.That(float.IsNaN(h), Is.False, $"{group}: a hole in the floor at {p}");
                        float slope = Mathf.Abs(h - previous) / 0.5f;
                        if (slope > worstSlope)
                        {
                            worstSlope = slope;
                            worstGroup = group;
                        }

                        previous = h;
                        samples++;

                        // free width: the nearest solid thing sideways at chest height (16 directions)
                        float free = 8f;
                        string what = "";
                        for (int k = 0; k < 16; k++)
                        {
                            Vector3 dir = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(p.x, h + 1.2f, p.y), dir, 8f, ~0, QueryTriggerInteraction.Ignore))
                            {
                                if (hit.collider.transform.root.name != "Player" && hit.distance < free)
                                {
                                    free = hit.distance;
                                    what = hit.collider.transform.parent != null ? hit.collider.transform.parent.name + "/" + hit.collider.name : hit.collider.name;
                                }
                            }
                        }

                        if (group == "Side_VaultAlcove")
                        {
                            alcoveNarrowest = Mathf.Min(alcoveNarrowest, free); // the last leg walks up to a treasure alcove in the vault's south wall: it is allowed to be close to that wall
                        }
                        else if (group == "Main" && i >= points.Length - 2)
                        {
                            daisNarrowest = Mathf.Min(daisNarrowest, free); // the last leg crosses the vault's dais, past its altar
                        }
                        else if (group == "Main" || group == "Alternate")
                        {
                            if (free < narrowestMain)
                            {
                                narrowestMain = free;
                                mainWhere = $"{group} at {p} ({what})";
                            }
                        }
                        else if (free < narrowest)
                        {
                            narrowest = free;
                            narrowGroup = $"{group} at {p}";
                            narrowWhat = what;
                        }
                    }
                }
            }

            Debug.Log($"Ancient Mine routes: {samples} samples, steepest 1 m rise {worstSlope:F2} m per m ({Mathf.Atan(worstSlope) * Mathf.Rad2Deg:F0} deg) on {worstGroup}, nearest wall to a main / alternate route point {narrowestMain:F2} m, to a side route point {narrowest:F2} m on {narrowGroup} ({narrowWhat}), alcove approach {alcoveNarrowest:F2} m, vault dais {daisNarrowest:F2} m");
            Assert.That(worstSlope, Is.LessThan(0.5f), "no route climbs more than 0.5 m per metre (the controller allows 1.0): ramps and platforms are safe");
            Assert.That(narrowestMain, Is.GreaterThan(2.9f), $"the main and the alternate route have about 6 m or more of room everywhere (passages and ramps 7 - 9 m wide, 6.1 m between two hall pillars): {mainWhere}");
            Assert.That(daisNarrowest, Is.GreaterThan(1.5f), "the walk across the vault's dais keeps clear of the altar");
            Assert.That(narrowest, Is.GreaterThan(2.0f), $"every side route has at least about 4 m of room (partly collapsed tunnels, side caves): {narrowGroup} {narrowWhat}");
            Assert.That(alcoveNarrowest, Is.GreaterThan(0.9f), "the walk to the vault alcove keeps clear of the wall and the pillars (the strip between the pillars and the wall is 3.3 m wide)");
        }

        // ================================================================ camera: never in a wall, a pillar or the ceiling

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator Camera_StaysInsideTheCave_AtEveryAngle_EvenNextToPillarsArchesAndInTheNarrowestPlaces()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId);
            yield return Load(ScenePath);
            Transform player = RootObject("Player").transform;
            var cameraController = One<ThirdPersonCameraController>();
            FieldInfo yawField = typeof(ThirdPersonCameraController).GetField("yaw", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo pitchField = typeof(ThirdPersonCameraController).GetField("pitch", BindingFlags.NonPublic | BindingFlags.Instance);
            Camera camera = Camera.main;
            yield return new WaitForSecondsRealtime(1.2f);

            (string name, Vector2 where)[] places =
            {
                ("gate spawn", new Vector2(-45f, -2f)), ("gate by the portal", new Vector2(-57f, -2f)), ("gate corner", new Vector2(-57f, 3f)), ("gate mouth", new Vector2(-40f, -2f)), ("road ramp", new Vector2(-31f, -2f)),
                ("cistern passage", new Vector2(-56f, -18f)), ("cistern", new Vector2(-49.5f, -33f)),
                ("hall west door", new Vector2(-23f, -2f)), ("hall next to a pillar", new Vector2(-4.2f, 5.6f)), ("hall between pillars", new Vector2(-8.4f, 8f)), ("hall corner", new Vector2(-22f, 8f)),
                ("hall dais ramp", new Vector2(-11f, 17f)), ("hall dais", new Vector2(-20f, 17f)), ("hall north door", new Vector2(0f, 21f)), ("hall east door", new Vector2(24f, -4f)),
                ("north ramp", new Vector2(0f, 26f)), ("workshop", new Vector2(-12f, 40f)), ("workshop by a pier", new Vector2(-19.5f, 35f)), ("west tunnel", new Vector2(-42f, 41.5f)), ("cache chamber", new Vector2(-51f, 42f)),
                ("collapsed spur end", new Vector2(-34f, 20.5f)), ("east tunnel", new Vector2(24f, 38f)),
                ("atrium", new Vector2(48f, -4f)), ("atrium rim", new Vector2(57f, -8f)), ("north branch", new Vector2(54f, 13f)), ("north lobe", new Vector2(46f, 33f)), ("grotto", new Vector2(36f, 14f)),
                ("south branch", new Vector2(51f, -22f)), ("south lobe", new Vector2(47f, -37f)),
                ("vault road", new Vector2(33f, -37f)), ("vault door", new Vector2(28f, -37f)), ("vault between pillars", new Vector2(10f, -32.2f)), ("vault dais", new Vector2(10f, -38f)), ("vault alcove", new Vector2(10f, -47.1f)),
            };
            int samples = 0, inWall = 0, outside = 0, blocked = 0, tooClose = 0, aimed = 0, belowCeilingMargin = 0;
            var skipped = new List<string>();
            float minDistance = float.MaxValue;
            var worst = "";
            foreach ((string name, Vector2 where) in places)
            {
                float y = FloorY(where.x, where.y);
                Assert.That(float.IsNaN(y), Is.False, "floor at " + name);
                if (Blocked(new Vector3(where.x, y + 1.05f, where.y)))
                {
                    skipped.Add(name); // a prop stands exactly there (the miner could not stand on that spot): the place is skipped, and the number of skipped places is limited below
                    continue;
                }

                Teleport(player, new Vector3(where.x, y + 1.05f, where.y));
                for (int angle = 0; angle < 360; angle += 45)
                {
                    yawField.SetValue(cameraController, (float)angle);
                    pitchField.SetValue(cameraController, 18f);
                    yield return new WaitForSecondsRealtime(0.3f);

                    samples++;
                    Vector3 pivot = player.position + Vector3.up * 1.4f;
                    float distance = Vector3.Distance(camera.transform.position, pivot);
                    Vector3 away = camera.transform.position - pivot;
                    away.y = 0f;
                    aimed += Vector3.Dot(away.normalized, Quaternion.Euler(0f, angle, 0f) * Vector3.back) > 0.8f ? 1 : 0; // the orbit really went to this side (it may swing away from a wall)
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        worst = $"{name} at yaw {angle}";
                    }

                    inWall += Physics.CheckSphere(camera.transform.position, 0.15f, ~0, QueryTriggerInteraction.Ignore) ? 1 : 0;
                    outside += InsideCave(camera.transform.position, out _) ? 0 : 1;
                    blocked += Physics.Linecast(camera.transform.position, pivot, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.root != player ? 1 : 0;
                    tooClose += distance < 2.6f ? 1 : 0;
                    float ceilingY = Physics.Raycast(camera.transform.position, Vector3.up, out RaycastHit up, 80f, ~0, QueryTriggerInteraction.Ignore) ? up.point.y : float.MaxValue;
                    belowCeilingMargin += ceilingY - camera.transform.position.y < 0.3f ? 1 : 0;
                }
            }

            // the player can also tilt the camera steeply (up to 65 degrees): the ceiling must stay out of the way
            int steep = 0, steepInside = 0;
            foreach (string name in new[] { "gate spawn", "hall between pillars", "workshop", "west tunnel", "atrium", "south branch", "vault door" })
            {
                foreach ((string n, Vector2 where) in places)
                {
                    if (n != name)
                    {
                        continue;
                    }

                    Teleport(player, new Vector3(where.x, FloorY(where.x, where.y) + 1.05f, where.y));
                    for (int angle = 0; angle < 360; angle += 90)
                    {
                        yawField.SetValue(cameraController, (float)angle);
                        pitchField.SetValue(cameraController, 65f);
                        yield return new WaitForSecondsRealtime(0.3f);
                        steep++;
                        steepInside += InsideCave(camera.transform.position, out _) && !Physics.CheckSphere(camera.transform.position, 0.15f, ~0, QueryTriggerInteraction.Ignore) ? 1 : 0;
                        minDistance = Mathf.Min(minDistance, Vector3.Distance(camera.transform.position, player.position + Vector3.up * 1.4f));
                    }
                }
            }

            Debug.Log($"Ancient Mine camera at every angle: {samples} samples, closest {minDistance:F2} m ({worst}), closer than 2.6 m: {tooClose}, in a wall: {inWall}, outside the cave: {outside}, miner hidden: {blocked}, within 0.3 m of the ceiling: {belowCeilingMargin}, aimed {aimed}; steep tilt {steepInside}/{steep} inside");
            Assert.That(skipped.Count, Is.LessThanOrEqualTo(3), "at most three tested places were occupied by a prop: " + string.Join(", ", skipped));
            Assert.That(inWall, Is.EqualTo(0), "the camera is never inside a wall, pillar or arch");
            Assert.That(outside, Is.EqualTo(0), "never above the ceiling, under the floor or in the rock");
            Assert.That(belowCeilingMargin, Is.EqualTo(0), "never touching the ceiling");
            Assert.That(steepInside, Is.EqualTo(steep), "and also not when the player tilts the camera to 65 degrees");
            Assert.That(minDistance, Is.GreaterThan(1.3f), "never glued to the miner's head (" + worst + ")");
            Assert.That(blocked / (float)samples, Is.LessThan(0.06f), "the miner stays in view");
            Assert.That(tooClose / (float)samples, Is.LessThan(0.2f), "the camera is only rarely pushed closer than 2.6 m");
            Assert.That(aimed / (float)samples, Is.GreaterThan(0.6f), "the camera really orbited to the tested sides (so the numbers above mean something)");
        }

        // ================================================================ the walls are real

        private IEnumerator Push(Transform player, ThirdPersonPlayerController controller, Camera camera, Vector3 start, Vector2 direction, float seconds)
        {
            Teleport(player, new Vector3(start.x, FloorY(start.x, start.z) + 1.05f, start.z));
            yield return new WaitForSecondsRealtime(0.4f);
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                Vector3 f = camera.transform.forward;
                f.y = 0f;
                f.Normalize();
                Vector3 r = new(f.z, 0f, -f.x);
                Vector3 d = new(direction.x, 0f, direction.y);
                controller.SetMoveInput(new Vector2(Vector3.Dot(d, r), Vector3.Dot(d, f)));
                yield return null;
            }

            controller.ClearMoveInput();
            yield return null;
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator ThePlayer_CannotWalkThroughWalls_Pillars_Arches_Crystals_OrTheMapEdge()
        {
            CharacterSelection.Select(CharacterSelection.MaleId);
            yield return Load(ScenePath);
            Transform player = RootObject("Player").transform;
            var controller = player.GetComponent<ThirdPersonPlayerController>();
            Camera camera = Camera.main;
            yield return new WaitForSecondsRealtime(1.2f);

            // (what, start, direction, a check on the final position: true when the player is still on the right side of the obstacle)
            var cases = new (string what, Vector3 start, Vector2 direction, System.Func<Vector3, bool> held)[]
            {
                ("a hall pillar (-4.2, 8)", new Vector3(-4.2f, 0f, 0f), Vector2.up, p => p.z < 6.9f),
                ("the hall's north wall", new Vector3(-12f, 0f, 12f), Vector2.up, p => p.z < 20.1f),
                ("the hall's west wall", new Vector3(-20f, 0f, 3f), Vector2.left, p => p.x > -24.1f),
                ("the hall's east wall", new Vector3(20f, 0f, 4f), Vector2.right, p => p.x < 24.1f),
                ("the gate's west wall at the great portal", new Vector3(-55f, 0f, -2f), Vector2.left, p => p.x > -61.2f),
                ("the great portal's pier", new Vector3(-55f, 0f, -8.8f), Vector2.left, p => p.x > -61.2f),
                ("a broken column (round: the player may slide round it, never into it)", new Vector3(-55.5f, 0f, 1.6f), Vector2.up, p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(-55.5f, 6.5f)) > 1.6f),
                ("the south wall of the gate", new Vector3(-48f, 0f, -6f), Vector2.down, p => p.z > -14.2f),
                ("the vault's east wall beside the gate", new Vector3(24.5f, 0f, -41.5f), Vector2.right, p => p.x < 28.6f),
                ("a vault pillar (24, -31.5)", new Vector3(24f, 0f, -37f), Vector2.up, p => p.z < -32.2f),
                ("the vault's south alcove", new Vector3(10f, 0f, -46.2f), Vector2.down, p => p.z > -52.5f),
                ("a crystal formation in the vault", new Vector3(-3f, 0f, -38f), Vector2.left, p => p.x > -8.5f),
                ("the atrium's east wall", new Vector3(50f, 0f, -4f), Vector2.right, p => p.x < 62.2f),
                ("the workshop's north wall", new Vector3(-6f, 0f, 41f), Vector2.up, p => p.z < 50.2f),
                ("a workshop pier (5, 35)", new Vector3(5f, 0f, 31.5f), Vector2.up, p => p.z < 33.6f),
            };
            int index = 0;
            foreach ((string what, Vector3 start, Vector2 direction, System.Func<Vector3, bool> held) in cases)
            {
                index++;
                float startFloor = FloorY(start.x, start.z);
                Assert.That(float.IsNaN(startFloor), Is.False, what + ": start has floor");
                Assert.That(Blocked(new Vector3(start.x, startFloor + 1.05f, start.z)), Is.False, what + ": the start is free standing room");
                yield return Push(player, controller, camera, start, direction, 3.2f);
                Vector3 p = player.position;
                Assert.That(held(p), Is.True, $"{what}: the player was stopped ({p})");
                Assert.That(InsideCave(p + Vector3.up * 0.5f, out string why), Is.True, $"{what}: the player is still inside the cave ({why}) at {p}");
                float ground = GroundUnder(player);
                Assert.That(p.y - ground, Is.InRange(0.8f, 2.0f), $"{what}: still standing on the floor ({p})");
            }

            Assert.That(index, Is.EqualTo(cases.Length));
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

        // ================================================================ detector, DIG, COLLECT on a TEMPORARY, isolated treasure spot

        [UnityTest]
        public IEnumerator DetectorDigAndCollect_WorkOnATemporaryIsolatedSpot_AndPayOnce_WithoutTouchingOtherIds()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AreaCatalog>("Assets/Data/Areas/AreaCatalog.asset");
            var seed = new MemoryProgressStore();
            ProgressService seedService = ProgressService.Create(catalog, seed);
            seedService.RegisterCollected("green_mine.meadow_01", 500);
            seedService.RegisterCollected("rocky_canyon.outpost_01", 700); // 1200 total: Ancient Mine is open
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load(ScenePath, seed.Json);
                var hunt = One<TreasureHuntController>();
                var detector = One<TreasureDetector>();
                Transform player = RootObject("Player").transform;
                Assert.That(TreasureSpot.Active.Count, Is.EqualTo(0), "the scene itself has no treasure");

                // a temporary spot made from the existing Gold Coin prefab: it lives in this test's scene copy only and is gone when the scene is unloaded
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Treasure/Treasure_GoldCoin.prefab");
                GameObject temp = Object.Instantiate(prefab, new Vector3(-47f, FloorY(-47f, -6f), -6f), Quaternion.identity);
                SceneManager.MoveGameObjectToScene(temp, scene);
                temp.name = "TEMP_Treasure_16A";
                var spot = temp.GetComponent<TreasureSpot>();
                typeof(TreasureSpot).GetField("spotId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(spot, "ancient_mine.test_16a");
                yield return null;
                Assert.That(spot.SpotId, Is.EqualTo("ancient_mine.test_16a"));
                Assert.That(spot.Definition.GoldValue, Is.EqualTo(10));

                Teleport(player, new Vector3(spot.transform.position.x + 6f, FloorY(spot.transform.position.x + 6f, -6f) + 1.05f, -6f));
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.That(detector.HasSignal, Is.True, id + ": the detector senses the treasure");
                Assert.That(detector.NearestHidden, Is.EqualTo(spot));

                Teleport(player, new Vector3(spot.transform.position.x + 1.4f, FloorY(spot.transform.position.x + 1.4f, -6f) + 1.05f, -6f));
                Button dig = FindButton("DigButton"), collect = FindButton("CollectButton");
                yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
                Assert.That(dig.gameObject.activeInHierarchy, Is.True, id + ": DIG is offered in range");
                int gold = hunt.TotalGold;
                Assert.That(gold, Is.EqualTo(1200), "the saved gold is what the mine starts from");
                dig.onClick.Invoke();
                yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
                Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), id + ": the dig reveals it");
                yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
                collect.onClick.Invoke();
                yield return null;
                Assert.That(spot.State, Is.EqualTo(TreasureState.Collected));
                Assert.That(hunt.TotalGold, Is.EqualTo(gold + 10), id + ": the gold is paid");
                collect.onClick.Invoke();
                dig.onClick.Invoke();
                yield return null;
                Assert.That(hunt.TotalGold, Is.EqualTo(gold + 10), id + ": once");

                GameProgressData saved = JsonUtility.FromJson<GameProgressData>(store.Json);
                Assert.That(saved.collectedTreasureIds, Contains.Item("ancient_mine.test_16a"));
                Assert.That(saved.collectedTreasureIds, Contains.Item("green_mine.meadow_01"), "Green Mine's collection is untouched");
                Assert.That(saved.collectedTreasureIds, Contains.Item("rocky_canyon.outpost_01"), "Rocky Canyon's collection is untouched");
                Assert.That(saved.collectedTreasureIds.Count, Is.EqualTo(3));
                Assert.That(seed.Json, Does.Not.Contain("test_16a"), "the seed save of this test never saw the temporary spot (it lives in memory only)");

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        // ================================================================ the other two areas are untouched

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
                    Assert.That(spot.SpotId, Does.Not.StartWith("ancient_mine."));
                }
            }

            Assert.That(count, Is.EqualTo(15), "Green Mine's 15 treasures");
            Assert.That(ids.Count, Is.EqualTo(15), "all different");
            Assert.That(total, Is.EqualTo(625), "and the 625 gold distribution");
            Assert.That(RootObject("AncientMineEnvironment"), Is.Null, "Ancient Mine never leaks into Green Mine");
        }

        [UnityTest]
        public IEnumerator RockyCanyon_KeepsItsTwentyTreasures_AndTheirIds_And840Gold()
        {
            yield return Load(RockyPath);
            int total = 0, count = 0;
            var ids = new HashSet<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TreasureSpot spot in root.GetComponentsInChildren<TreasureSpot>(true))
                {
                    count++;
                    total += spot.Definition.GoldValue;
                    ids.Add(spot.SpotId);
                    Assert.That(spot.SpotId, Does.StartWith("rocky_canyon."), "Rocky Canyon ids stay Rocky Canyon ids");
                    Assert.That(spot.SpotId, Does.Not.StartWith("ancient_mine."));
                }
            }

            Assert.That(count, Is.EqualTo(20), "Rocky Canyon's 20 treasures");
            Assert.That(ids.Count, Is.EqualTo(20), "all different");
            Assert.That(total, Is.EqualTo(840), "and the 840 gold distribution");
            Assert.That(RootObject("AncientMineEnvironment"), Is.Null, "Ancient Mine never leaks into Rocky Canyon");
        }
    }
}
#endif
