#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Rocky Canyon environment design (stage 15B): the five areas have their landmarks and colliders, the whole environment stays a cheap mobile scene (three materials, merged meshes,
    /// plain colliders, no transparency, one extra light), the big timber bridge is safe (both miners cross it both ways, cannot be pushed off its sides or into the gap beside it), the gorge
    /// under it stays open, the lookout and the hidden ravine can be used, the camp and the lookout see the bridge, the spawn stays open and no treasure was added. In-memory stores
    /// everywhere; the developer's real save file is hashed before the first and after the last test.
    /// </summary>
    public sealed class RockyCanyonEnvironmentPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/RockyCanyon.unity";
        private static readonly string[] Miners = { CharacterSelection.MaleId, CharacterSelection.FemaleId };

        // the bridge frame (same numbers as the builder: its middle, the direction from the corridor's west end to its east start)
        private static readonly Vector3 BridgeCentre = new(3f, 5f, 18f);
        private static readonly float BridgeYaw = Mathf.Atan2(18f, 2f) * Mathf.Rad2Deg;

        private static string realSavePath;
        private static string realSaveHashBefore;

        private Scene scene;
        private float previousTimeScale;

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

        private IEnumerator Load()
        {
            var store = new MemoryProgressStore();
            ProgressManager.StoreFactoryOverride = () => store;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            Assert.That(load, Is.Not.Null, "Could not load " + ScenePath);
            yield return load;
            scene = SceneManager.GetSceneByPath(ScenePath);
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

        private Transform Env => RootObject("RockyCanyonEnvironment").transform;

        private Collider TerrainCollider => Env.Find("Terrain").GetComponent<Collider>();

        private float TerrainHeight(float x, float z)
        {
            Assert.That(TerrainCollider.Raycast(new Ray(new Vector3(x, 80f, z), Vector3.down), out RaycastHit hit, 200f), Is.True, $"terrain at {x:F1},{z:F1}");
            return hit.point.y;
        }

        /// <summary>The highest surface under (x, z) that is the terrain or one of the walkable structures (bridge deck, platform, ramp, lookout deck).</summary>
        private float WalkSurface(float x, float z)
        {
            float best = TerrainHeight(x, z);
            foreach (string path in new[] { "C_OldRailway/Colliders/BridgeDeck", "D_CanyonOverlook/Colliders/LookoutDeck" })
            {
                Transform t = Env.Find(path);
                if (t != null && t.GetComponent<Collider>().Raycast(new Ray(new Vector3(x, 80f, z), Vector3.down), out RaycastHit hit, 200f))
                {
                    best = Mathf.Max(best, hit.point.y);
                }
            }

            return best;
        }

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

        private void PlaceOnSurface(Transform player, float x, float z)
        {
            Teleport(player, new Vector3(x, WalkSurface(x, z) + 1.05f, z));
        }

        private static void Push(ThirdPersonPlayerController controller, Camera camera, Vector3 worldDirection)
        {
            Vector3 f = camera.transform.forward;
            f.y = 0f;
            f.Normalize();
            Vector3 r = new(f.z, 0f, -f.x);
            Vector3 d = worldDirection;
            d.y = 0f;
            d.Normalize();
            controller.SetMoveInput(new Vector2(Vector3.Dot(d, r), Vector3.Dot(d, f)));
        }

        private sealed class Stats
        {
            public int Frames, CameraInRock, CameraBlocked, CameraTooClose;
            public float MinY = float.MaxValue, MaxY = float.MinValue, MinCameraDistance = float.MaxValue;
            public string FirstRock = "";
        }

        private IEnumerator WalkTo(Transform player, ThirdPersonPlayerController controller, Camera camera, Vector2 target, string label, Stats stats)
        {
            float legTime = 0f, checkTime = 0f, sidestepUntil = 0f, sidestepSign = 1f;
            float legStart = Vector2.Distance(new Vector2(player.position.x, player.position.z), target);
            Vector3 checkPosition = player.position;
            while (true)
            {
                Vector3 delta = new(target.x - player.position.x, 0f, target.y - player.position.z);
                if (delta.magnitude < 1.4f)
                {
                    break;
                }

                legTime += Time.deltaTime;
                Assert.That(legTime, Is.LessThan(legStart / 2.5f + 8f), $"stuck on the way to {label} at {player.position}");
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

                Vector3 d = delta.normalized;
                if (Time.time < sidestepUntil)
                {
                    d = Quaternion.Euler(0f, 75f * sidestepSign, 0f) * d;
                }

                Push(controller, camera, d);
                yield return null;
                Sample(player, camera, stats);
            }

            controller.ClearMoveInput();
        }

        private static void Sample(Transform player, Camera camera, Stats stats)
        {
            stats.Frames++;
            stats.MinY = Mathf.Min(stats.MinY, player.position.y);
            stats.MaxY = Mathf.Max(stats.MaxY, player.position.y);
            Vector3 pivot = player.position + Vector3.up * 1.4f;
            float distance = Vector3.Distance(camera.transform.position, pivot);
            stats.MinCameraDistance = Mathf.Min(stats.MinCameraDistance, distance);
            stats.CameraTooClose += distance < 2.4f ? 1 : 0;
            Collider[] inside = Physics.OverlapSphere(camera.transform.position, 0.15f, ~0, QueryTriggerInteraction.Ignore);
            if (inside.Length > 0)
            {
                stats.CameraInRock++;
                if (stats.FirstRock.Length == 0)
                {
                    stats.FirstRock = $"{inside[0].name} (camera {camera.transform.position}, miner {player.position})";
                }
            }

            stats.CameraBlocked += Physics.Linecast(camera.transform.position, pivot, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.root != player ? 1 : 0;
        }

        private static Vector3 BridgeWorld(float lx, float y, float lz) => BridgeCentre + Quaternion.Euler(0f, BridgeYaw, 0f) * new Vector3(lx, y, lz);

        // ================================================================ design, budget, mobile friendliness

        [UnityTest]
        public IEnumerator Environment_IsACheapMobileScene_ThreeMaterials_MergedMeshes_PlainColliders_NoTransparency_OneExtraLight()
        {
            yield return Load();
            Transform env = Env;
            var materials = new HashSet<Material>();
            int renderers = 0, triangles = 0;
            foreach (MeshRenderer r in env.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                foreach (Material m in r.sharedMaterials)
                {
                    materials.Add(m);
                    Assert.That(m.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), r.name);
                    Assert.That(m.renderQueue, Is.LessThan(2500), r.name + ": opaque only (no transparent surface)");
                }

                Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                triangles += mesh.triangles.Length / 3;
                Assert.That(GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.BatchingStatic), Is.True, r.name + " is batching static");
                Assert.That(r.GetComponent<Collider>() == null || r.name == "Terrain", Is.True, r.name + ": renderers carry no collider (colliders are separate simple boxes and spheres)");
            }

            var names = new List<string>();
            foreach (Material m in materials)
            {
                names.Add(m.name);
            }

            Assert.That(materials.Count, Is.LessThanOrEqualTo(3), "materials: " + string.Join(", ", names));
            CollectionAssert.IsSubsetOf(names, new[] { "RC_Ground", "RC_Palette", "RC_Glow" });
            Assert.That(renderers, Is.LessThan(40), "renderers");
            Assert.That(triangles, Is.LessThan(70000), "triangles");

            int meshColliders = 0, simple = 0;
            foreach (Collider c in env.GetComponentsInChildren<Collider>(true))
            {
                if (c is MeshCollider)
                {
                    meshColliders++;
                }
                else
                {
                    Assert.That(c is BoxCollider || c is SphereCollider, Is.True, c.name + ": only boxes and spheres (" + c.GetType().Name + ")");
                    simple++;
                }
            }

            Assert.That(meshColliders, Is.EqualTo(1), "only the terrain has a mesh collider");
            Assert.That(simple, Is.LessThan(150), "simple colliders");
            Debug.Log($"Rocky Canyon 15B: {renderers} renderers, {materials.Count} materials ({string.Join(", ", names)}), {triangles} triangles, {simple} box/sphere colliders + terrain");

            // lights: the sun (directional) and a single soft point light in the hidden pocket
            int point = 0;
            foreach (Light l in env.GetComponentsInChildren<Light>(true))
            {
                if (l.type == LightType.Directional)
                {
                    Assert.That(l.shadows, Is.Not.EqualTo(LightShadows.None), "the sun casts the canyon's shadows");
                }
                else
                {
                    point++;
                    Assert.That(l.type, Is.EqualTo(LightType.Point));
                    Assert.That(l.shadows, Is.EqualTo(LightShadows.None), "no shadow-casting extra light");
                }
            }

            Assert.That(point, Is.EqualTo(1), "one soft warm light in the whole environment, in the hidden ravine's pocket (the miner's own lamp is part of the player)");
            Assert.That(RenderSettings.fog, Is.True, "a little haze for depth");
            Assert.That(RenderSettings.skybox, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator FiveAreas_HaveTheirLandmarksAndColliders_AndTheTwentyTreasures()
        {
            yield return Load();
            Transform env = Env;
            (string zone, string[] colliders)[] expected =
            {
                ("A_DesertOutpost", new[] { "MinerShack", "SupplyShed", "WaterTowerBase", "SignPostL", "SignPostR", "Crates", "Barrels", "OreCart" }),
                ("B_DryRiverbed", new[] { "HeadframeCollar", "MineEntrance", "WagonWreck" }),
                ("C_OldRailway", new[] { "BridgeDeck", "BridgeRailL", "BridgeRailR", "PlatformRamp", "PlatformDeck", "CraneMast", "WagonA", "WagonB", "WagonC", "SignalPost" }),
                ("D_CanyonOverlook", new[] { "LookoutDeck", "FlagPole" }),
                ("E_HiddenRavine", new[] { "GateRockL", "GateRockR" }),
            };
            foreach ((string zone, string[] colliders) in expected)
            {
                Assert.That(env.Find(zone + "/Anchor"), Is.Not.Null, zone);
                Transform props = env.Find(zone + "/Props"), cols = env.Find(zone + "/Colliders");
                Assert.That(props, Is.Not.Null, zone + "/Props");
                Assert.That(cols, Is.Not.Null, zone + "/Colliders");
                Assert.That(props.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThanOrEqualTo(1), zone + ": has its dressed meshes");
                foreach (string name in colliders)
                {
                    Assert.That(cols.Find(name), Is.Not.Null, zone + ": " + name);
                }
            }

            Assert.That(CountNamed(env.Find("C_OldRailway/Colliders"), "TrestlePost"), Is.EqualTo(0), "trestle posts are visual only (a tall collider beside the walkway would squeeze the camera)");
            Assert.That(CountNamed(env.Find("C_OldRailway/Colliders"), "TowerPost"), Is.EqualTo(0), "so are the portal towers");

            // landmarks are tall: the camp's water tower, the bridge's portal towers, the overlook's flag pole stand clearly above their floors
            float Top(string path, float floor) => env.Find(path).GetComponent<MeshRenderer>().bounds.max.y - floor;
            Assert.That(Top("A_DesertOutpost/Props/Structures", 2f), Is.GreaterThan(6f), "water tower");
            Assert.That(Top("C_OldRailway/Props/Structures", 5f), Is.GreaterThan(8f), "bridge portal towers");
            Assert.That(Top("D_CanyonOverlook/Props/Structures", 11f), Is.GreaterThan(6f), "lookout flag pole and rock spires");
            Assert.That(Top("B_DryRiverbed/Props/Structures", 0f), Is.GreaterThan(3.5f), "the headframe over the old shaft");

            // the hidden ravine: gold veins are a glowing, collider-free mesh; the pocket has its one warm light
            Transform veins = env.Find("E_HiddenRavine/Props/Veins");
            Assert.That(veins, Is.Not.Null);
            Assert.That(veins.GetComponent<Collider>(), Is.Null, "veins are decoration only");
            Assert.That(veins.GetComponent<MeshRenderer>().sharedMaterials.Length, Is.EqualTo(2));
            Transform glow = env.Find("E_HiddenRavine/Props/PocketGlow");
            Assert.That(glow, Is.Not.Null);
            Assert.That(Vector2.Distance(new Vector2(glow.position.x, glow.position.z), new Vector2(-17f, 2f)), Is.LessThan(1f), "the light is over the pocket");

            // treasure (15C): the 20 permanent treasures live in the zone groups; the 15A scaffolding is gone
            Assert.That(TreasureSpot.Active.Count, Is.EqualTo(20));
            Assert.That(RootObject("TreasureSpots").transform.Find("_TestPoints"), Is.Null);
            int spots = 0;
            foreach (TreasureSpot s in RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true))
            {
                spots++;
                Assert.That(s.SpotId, Does.StartWith("rocky_canyon."));
                Assert.That(s.SpotId, Does.Not.Contain("test_"));
            }

            Assert.That(spots, Is.EqualTo(20));
        }

        private static int CountNamed(Transform parent, string prefix)
        {
            int n = 0;
            foreach (Transform t in parent)
            {
                n += t.name.StartsWith(prefix) ? 1 : 0;
            }

            return n;
        }

        // ================================================================ the start, the first view, the lookout's view

        [UnityTest]
        public IEnumerator TheStart_IsOpen_NothingNearTheMiner_OrTheCamera_AndTheBridgeTowersAreInTheFirstView()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load();
                Transform player = RootObject("Player").transform;
                yield return new WaitForSecondsRealtime(0.8f);
                Camera camera = Camera.main;
                foreach ((Vector3 centre, float radius, string label) in new[] { (player.position, 3.4f, "the miner"), (camera.transform.position, 3.0f, "the camera") })
                {
                    foreach (Collider c in Physics.OverlapSphere(centre, radius, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Assert.That(c.transform.root == player || c.gameObject.name == "Terrain", Is.True, $"{id}: {c.name} is within {radius} m of {label}");
                    }
                }

                // the first view looks along the riverbed: the bridge's portal towers are inside the view and nothing solid hides their crowns
                Vector3 crown = BridgeWorld(0f, 8.7f, -6.2f);
                Vector3 toCrown = crown - camera.transform.position;
                float angle = Vector3.Angle(Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up), Vector3.ProjectOnPlane(toCrown, Vector3.up));
                Assert.That(angle, Is.LessThan(40f), id + ": the west tower is well inside the first view (" + angle + " deg off the centre)");
                Assert.That(Physics.Linecast(camera.transform.position, crown, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore), Is.False, id + ": the tower's crown is visible from the first view, blocked by " + (hit.collider != null ? hit.collider.name : "-"));
                Assert.That(Physics.Linecast(camera.transform.position, BridgeWorld(0f, 9.4f, 6.2f), out hit, ~0, QueryTriggerInteraction.Ignore), Is.False, id + ": and the east tower's too");
                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheLookout_SeesTheBridge_TheRiverbed_AndTheCanyonWalls()
        {
            yield return Load();
            Transform player = RootObject("Player").transform;
            Vector3 eye = new Vector3(43f, WalkSurface(43f, 36f) + 0.12f + 1.75f, 36f);
            (string what, Vector3 target)[] targets =
            {
                ("the bridge's east crown", BridgeWorld(0f, 8.7f, 6.2f)), ("the bridge's west crown", BridgeWorld(0f, 8.7f, -6.2f)),
                ("the dry riverbed", new Vector3(22f, TerrainHeight(22f, -8f) + 1f, -8f)), ("the far dry riverbed", new Vector3(31f, TerrainHeight(31f, 0f) + 1f, 0f)),
                ("the canyon wall south of the riverbed", new Vector3(10f, 11f, -30f)),
            };
            int seen = 0;
            var blocked = new List<string>();
            foreach ((string what, Vector3 target) in targets)
            {
                if (!Physics.Linecast(eye, target, ~0, QueryTriggerInteraction.Ignore))
                {
                    seen++;
                }
                else
                {
                    blocked.Add(what);
                }
            }

            Debug.Log($"Rocky Canyon lookout: sees {seen} of {targets.Length} targets; blocked: {string.Join(", ", blocked)}");
            Assert.That(seen, Is.GreaterThanOrEqualTo(4), "from the lookout the bridge, the riverbed and the canyon walls are in view (blocked: " + string.Join(", ", blocked) + ")");
            Assert.That(blocked, Does.Not.Contain("the bridge's east crown"));
            Assert.That(blocked, Does.Not.Contain("the dry riverbed"));
            _ = player;
        }

        // ================================================================ the bridge

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator Bridge_BothMiners_CrossItBothWays_StayOnTheDeck_AndCannotBePushedOverTheSides()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load();
                Time.timeScale = 2f;
                Transform player = RootObject("Player").transform;
                var controller = player.GetComponent<ThirdPersonPlayerController>();
                Camera camera = Camera.main;
                yield return new WaitForSecondsRealtime(0.5f);
                PlaceOnSurface(player, -11f, 16.6f);
                yield return new WaitForSecondsRealtime(0.4f);

                var stats = new Stats();
                float deckY = float.NaN;
                foreach (Vector2 p in new[] { new Vector2(-4f, 17.1f), new Vector2(3f, 18f), new Vector2(9f, 18.8f), new Vector2(15f, 19.3f), new Vector2(9f, 18.8f), new Vector2(3f, 18f), new Vector2(-4f, 17.1f), new Vector2(-11f, 16.6f) })
                {
                    yield return WalkTo(player, controller, camera, p, "the bridge", stats);
                    if (Mathf.Abs(p.x - 3f) < 0.5f)
                    {
                        deckY = player.position.y;
                        Assert.That(GroundUnder(player), Is.InRange(5.0f, 5.2f), id + ": standing on the deck");
                    }
                }

                Assert.That(stats.MinY, Is.GreaterThan(5.5f), id + ": never dropped below the deck level on the crossing (lowest " + stats.MinY + ")");
                Assert.That(stats.MaxY, Is.LessThan(6.8f), id + ": never climbed onto a rail or a tower");
                Assert.That(stats.CameraInRock, Is.EqualTo(0), id + ": the camera is never inside rock or timber: " + stats.FirstRock);
                Assert.That(stats.MinCameraDistance, Is.GreaterThan(1.8f), id + ": the camera is not glued to the miner on the bridge");
                Assert.That(stats.CameraBlocked / (float)stats.Frames, Is.LessThan(0.03f), id + ": the bridge's posts hide the miner rarely");
                Debug.Log($"Rocky Canyon bridge ({id}): {stats.Frames} frames, y {stats.MinY:F2}..{stats.MaxY:F2}, camera min {stats.MinCameraDistance:F2}, blocked {100f * stats.CameraBlocked / stats.Frames:F1} %, deck y {deckY:F2}");

                // push sideways against each rail for two seconds, in the middle of the deck: the rails hold
                Vector3 lateral = Quaternion.Euler(0f, BridgeYaw, 0f) * Vector3.right;
                foreach (float sign in new[] { 1f, -1f })
                {
                    PlaceOnSurface(player, 3f, 18f);
                    yield return new WaitForSecondsRealtime(0.3f);
                    float end = Time.realtimeSinceStartup + 1.6f;
                    while (Time.realtimeSinceStartup < end)
                    {
                        Push(controller, camera, lateral * sign);
                        yield return null;
                        float offset = Mathf.Abs(Vector3.Dot(player.position - BridgeCentre, lateral));
                        Assert.That(offset, Is.LessThan(1.6f), id + ": the rail keeps the miner on the deck");
                        Assert.That(player.position.y, Is.GreaterThan(5.8f), id + ": and above the gorge");
                    }

                    controller.ClearMoveInput();
                }

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator CliffEdges_BesideTheBridge_AreFenced_NobodyWalksIntoTheGap()
        {
            // start points on the two abutments (inside the fences) and the direction towards the gap
            (string label, Vector2 start, Vector2 direction)[] tries =
            {
                ("west, north side", new Vector2(-5.2f, 20.1f), new Vector2(1f, -0.35f)), ("west, north side, steeper", new Vector2(-6.0f, 19.8f), new Vector2(1f, -0.7f)),
                ("west, south side", new Vector2(-5.2f, 14.4f), new Vector2(1f, 0.3f)), ("west, south side, steeper", new Vector2(-6.0f, 14.2f), new Vector2(1f, 0.7f)),
                ("east, north side", new Vector2(11.0f, 21.7f), new Vector2(-1f, -0.4f)), ("east, north side, steeper", new Vector2(11.5f, 21.6f), new Vector2(-1f, -0.9f)),
                ("east, south side", new Vector2(10.6f, 16.7f), new Vector2(-1f, 0.3f)), ("east, south side, steeper", new Vector2(11.8f, 16.4f), new Vector2(-1f, 0.9f)),
            };
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load();
                Time.timeScale = 2f;
                Transform player = RootObject("Player").transform;
                var controller = player.GetComponent<ThirdPersonPlayerController>();
                Camera camera = Camera.main;
                yield return new WaitForSecondsRealtime(0.5f);
                foreach ((string label, Vector2 start, Vector2 direction) in tries)
                {
                    PlaceOnSurface(player, start.x, start.y);
                    yield return new WaitForSecondsRealtime(0.3f);
                    float startY = player.position.y;
                    float end = Time.realtimeSinceStartup + 2.2f;
                    while (Time.realtimeSinceStartup < end)
                    {
                        Push(controller, camera, new Vector3(direction.x, 0f, direction.y));
                        yield return null;
                        Assert.That(player.position.y, Is.GreaterThan(startY - 0.6f), $"{id}: {label}: the fence holds, the miner did not fall into the gap (y {player.position.y:F2} from {startY:F2} at {player.position})");
                    }

                    controller.ClearMoveInput();
                }

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator Gorge_UnderTheBridge_StaysOpen_FromTheBasinToItsEnd_AndBack_WithAWellBehavedCamera()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load();
                Time.timeScale = 2f;
                Transform player = RootObject("Player").transform;
                var controller = player.GetComponent<ThirdPersonPlayerController>();
                Camera camera = Camera.main;
                yield return new WaitForSecondsRealtime(0.5f);
                PlaceOnSurface(player, 6f, -9f);
                yield return new WaitForSecondsRealtime(0.4f);
                var stats = new Stats();
                foreach (Vector2 p in new[] { new Vector2(4.8f, 1f), new Vector2(3.6f, 9f), new Vector2(3f, 14f), new Vector2(3f, 18f), new Vector2(3f, 22.5f), new Vector2(3f, 14f), new Vector2(3.6f, 6f), new Vector2(6f, -8f) })
                {
                    yield return WalkTo(player, controller, camera, p, "the gorge", stats);
                }

                Assert.That(stats.MaxY, Is.LessThan(3.2f), id + ": the whole gorge walk happens on the gorge floor, below the deck");
                Assert.That(stats.CameraInRock, Is.EqualTo(0), id + ": the camera is never inside rock or timber under the bridge: " + stats.FirstRock);
                Assert.That(stats.MinCameraDistance, Is.GreaterThan(1.5f), id + ": nor pressed against the miner");
                Assert.That(stats.CameraBlocked / (float)stats.Frames, Is.LessThan(0.05f), id + ": the walls and posts rarely hide the miner");
                Debug.Log($"Rocky Canyon gorge ({id}): {stats.Frames} frames, y {stats.MinY:F2}..{stats.MaxY:F2}, camera min {stats.MinCameraDistance:F2}, blocked {100f * stats.CameraBlocked / stats.Frames:F1} %");
                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        // ================================================================ lookout and hidden ravine

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator Lookout_CanBeWalkedOnto_Used_AndItsRailsHold()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load();
                Time.timeScale = 2f;
                Transform player = RootObject("Player").transform;
                var controller = player.GetComponent<ThirdPersonPlayerController>();
                Camera camera = Camera.main;
                yield return new WaitForSecondsRealtime(0.5f);
                PlaceOnSurface(player, 35f, 38f);
                yield return new WaitForSecondsRealtime(0.4f);
                var stats = new Stats();
                foreach (Vector2 p in new[] { new Vector2(40f, 38f), new Vector2(43.5f, 36.5f), new Vector2(42f, 35f), new Vector2(41f, 37f) })
                {
                    yield return WalkTo(player, controller, camera, p, "the lookout", stats);
                }

                Assert.That(stats.MinY, Is.GreaterThan(11.0f), id + ": climbing the last metres and standing on the deck never drops below the path's end");
                Assert.That(stats.MaxY, Is.LessThan(12.6f), id + ": nor onto a rail");
                Assert.That(stats.CameraInRock, Is.EqualTo(0), id + ": the camera is never inside rock or timber at the top: " + stats.FirstRock);

                // push towards the canyon, towards the far side and sideways for 2 s each: the miner stays inside the plateau
                foreach (float yaw in new[] { 230f, 50f, 140f, 320f })
                {
                    PlaceOnSurface(player, 43f, 36f);
                    yield return new WaitForSecondsRealtime(0.3f);
                    float end = Time.realtimeSinceStartup + 2f;
                    while (Time.realtimeSinceStartup < end)
                    {
                        Push(controller, camera, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
                        yield return null;
                        Assert.That(player.position.y, Is.InRange(yaw == 320f ? 10.4f : 11.5f, 13.1f), $"{id}: pushing towards {yaw} degrees: still on the plateau (y {player.position.y:F2})");
                        if (yaw != 320f) // (320 is the way back down the path, which leaves the plateau on purpose)
                        {
                            Assert.That(Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(43f, 36f)), Is.LessThan(6.3f), $"{id}: and inside its rim");
                        }
                        if (yaw == 230f)
                        {
                            // the front rail faces the canyon: its collider keeps the miner on the deck (the rail stands 1.75 m from the deck's middle)
                            Vector3 view = Quaternion.Euler(0f, 230f, 0f) * Vector3.forward;
                            Assert.That(Vector3.Dot(player.position - new Vector3(43f, player.position.y, 36f), view), Is.LessThan(1.5f), $"{id}: the front rail holds");
                        }
                    }

                    controller.ClearMoveInput();
                }

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HiddenRavine_TheEntrance_IsOpenAndWide_NotAWall()
        {
            yield return Load();
            // scan the line between the two gate rocks with the miner's capsule: the longest free stretch must be a comfortable gate
            Vector3 a = new(-8.6f, 0f, -10.2f), b = new(0.4f, 0f, -10.8f);
            float best = 0f, run = 0f, step = 0.2f;
            int n = Mathf.CeilToInt(Vector3.Distance(a, b) / step);
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)n);
                float ground = TerrainHeight(p.x, p.z);
                bool free = !Physics.CheckCapsule(new Vector3(p.x, ground + 0.7f, p.z), new Vector3(p.x, ground + 1.6f, p.z), 0.45f, ~0, QueryTriggerInteraction.Ignore);
                run = free ? run + step : 0f;
                best = Mathf.Max(best, run);
            }

            Debug.Log($"Rocky Canyon ravine gate: widest free stretch {best:F1} m");
            Assert.That(best, Is.GreaterThan(4.2f), "the hidden ravine's entrance is easy to enter (widest free stretch " + best + " m)");
        }
    }
}
#endif
