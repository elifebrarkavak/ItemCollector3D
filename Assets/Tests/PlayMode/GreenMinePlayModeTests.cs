#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using ItemCollector3D.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// The real GreenMine scene (Assets/Scenes/GreenMine.unity), in PlayMode, with an in-memory progress store (never the developer's save file):
    /// structure and ownership of the four roots, treasure ids and gold, the chosen miner, reachability of every exploration area, the third-person
    /// camera along the whole trail network, detector / DIG / COLLECT, persistence, pause, and the mobile performance guard rails.
    /// </summary>
    public sealed class GreenMinePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/GreenMine.unity";
        private const string GameplayTestId = "green_mine.gold_coin_01";

        private Scene scene;
        private float previousTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
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
        }

        // ================================================================ helpers

        private IEnumerator LoadScene(IProgressStore store = null)
        {
            ProgressManager.StoreFactoryOverride = () => store ?? new MemoryProgressStore();
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

        private IEnumerator UnloadScene()
        {
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            yield return null;
        }

        private GameObject Root(string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            Assert.Fail("Root object not found in GreenMine: " + name);
            return null;
        }

        private T[] All<T>() where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return result.ToArray();
        }

        private Button FindButton(string name)
        {
            foreach (Button button in All<Button>())
            {
                if (button.name == name)
                {
                    return button;
                }
            }

            Assert.Fail("Button not found in GreenMine: " + name);
            return null;
        }

        private TreasureSpot Spot(string id)
        {
            foreach (TreasureSpot spot in All<TreasureSpot>())
            {
                if (spot.SpotId == id)
                {
                    return spot;
                }
            }

            Assert.Fail("Treasure spot not found: " + id);
            return null;
        }

        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
        }

        private Transform Player => Root("Player").transform;

        private static GameProgressData Saved(MemoryProgressStore store)
        {
            Assert.That(store.Json, Is.Not.Null.And.Not.Empty, "something was saved");
            return JsonUtility.FromJson<GameProgressData>(store.Json);
        }

        /// <summary>A free standing place within the dig range of a spot (open ground to stand on, nothing solid in the way).</summary>
        private Vector3 StandingPlaceNear(TreasureSpot spot)
        {
            Vector3 centre = spot.transform.position;
            for (float radius = 1.2f; radius <= 2.2f; radius += 0.5f)
            {
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f;
                    Vector3 p = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                    if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)
                        && !Physics.CheckCapsule(hit.point + Vector3.up * 0.6f, hit.point + Vector3.up * 1.5f, 0.45f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        return hit.point + Vector3.up * 1.1f;
                    }
                }
            }

            Assert.Fail($"No free standing place within the dig range of {spot.SpotId}");
            return default;
        }

        private IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }

        private IEnumerator DigAndCollect(TreasureSpot spot)
        {
            Transform player = Player;
            Teleport(player, StandingPlaceNear(spot));
            yield return null;
            yield return null;
            FindButton("DigButton").onClick.Invoke();
            float end = Time.time + 3f;
            while (spot.State == TreasureState.Hidden && Time.time < end)
            {
                yield return null;
            }

            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), $"DIG reveals {spot.SpotId}");
            yield return WaitSeconds(0.1f);
            FindButton("CollectButton").onClick.Invoke();
            yield return null;
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected), $"COLLECT collects {spot.SpotId}");
        }

        // ================================================================ structure

        [UnityTest]
        public IEnumerator Scene_HasFourSeparatedRoots_AndTheEnvironmentIsPureArt()
        {
            yield return LoadScene();

            GameObject player = Root("Player");
            GameObject systems = Root("GameplaySystems");
            GameObject environment = Root("GreenMineEnvironment");
            GameObject spots = Root("TreasureSpots");
            Assert.That(player.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(player.GetComponent<ThirdPersonPlayerController>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerCharacterVisual>(), Is.Not.Null, "the chosen miner is placed by the existing visual component");

            Assert.That(systems.GetComponentInChildren<ThirdPersonCameraController>(true), Is.Not.Null, "the third-person camera");
            Assert.That(systems.GetComponentInChildren<HudController>(true), Is.Not.Null, "the gameplay HUD");
            Assert.That(systems.GetComponentInChildren<VirtualJoystick>(true), Is.Not.Null, "the joystick");
            Assert.That(systems.GetComponentInChildren<TreasureHuntController>(true), Is.Not.Null);
            Assert.That(systems.GetComponentInChildren<TreasureDetector>(true), Is.Not.Null);
            Assert.That(systems.GetComponentInChildren<ProgressManager>(true), Is.Not.Null);
            Assert.That(systems.GetComponentInChildren<PauseMenuController>(true), Is.Not.Null);

            foreach (string group in new[] { "Terrain", "Water", "Path", "StartCamp", "MineEntrance", "Forest", "Meadow", "RockyArea", "Perimeter", "Vegetation", "Backdrop", "Lighting", "Boundaries", "Markers" })
            {
                Assert.That(environment.transform.Find(group), Is.Not.Null, "environment group " + group);
            }

            // Beyza edits the environment without touching game code: it holds no gameplay scripts at all
            foreach (MonoBehaviour behaviour in environment.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour.GetType().Name == "UniversalAdditionalLightData")
                {
                    continue; // the render pipeline's own companion of the sun
                }

                Assert.Fail("The environment must be art only, found script " + behaviour.GetType().Name + " on " + behaviour.name);
            }

            // ... and the gameplay roots hold no environment art
            Assert.That(systems.GetComponentsInChildren<MeshRenderer>(true).Length, Is.LessThanOrEqualTo(0), "no scenery in GameplaySystems");
            Assert.That(spots.transform.childCount, Is.InRange(6, 8), "6-8 temporary treasure points");
            foreach (TreasureSpot spot in All<TreasureSpot>())
            {
                Assert.That(spot.transform.IsChildOf(spots.transform), Is.True, spot.name + " must live under TreasureSpots");
            }

            Assert.That(environment.transform.Find("Path/TourWaypoints").childCount, Is.GreaterThanOrEqualTo(8), "path waypoints for the team");
            Assert.That(environment.transform.Find("Markers").childCount, Is.GreaterThanOrEqualTo(5), "zone markers for the team");

            var hunt = systems.GetComponentInChildren<TreasureHuntController>(true);
            Assert.That(hunt.AreaId, Is.EqualTo("green_mine"));
            Assert.That(hunt.IsAreaAccessible, Is.True);
            Assert.That(EditorBuildSettings.scenes, Has.Some.Matches<EditorBuildSettingsScene>(s => s.path == ScenePath && s.enabled), "GreenMine is in Build Settings");
        }

        [UnityTest]
        public IEnumerator Environment_ZonesAreAllPresent_AndDecorativeCrystalsAreNotTreasure()
        {
            yield return LoadScene();
            Transform environment = Root("GreenMineEnvironment").transform;
            Assert.That(environment.Find("StartCamp").GetComponentsInChildren<Transform>(true).Length, Is.GreaterThan(15), "camp with tent, fire, board, fences, gate");
            Assert.That(environment.Find("MineEntrance/Mine_Entrance"), Is.Not.Null, "the mine entrance is the visual focus");
            Assert.That(environment.Find("MineEntrance").GetComponentsInChildren<Transform>(true).Length, Is.GreaterThan(8), "rails, cart, crates, barrels, lanterns, crystals");
            Assert.That(environment.Find("Forest").GetComponentsInChildren<Transform>(true).Length, Is.GreaterThan(12));
            Assert.That(environment.Find("RockyArea").GetComponentsInChildren<Transform>(true).Length, Is.GreaterThan(8));
            Assert.That(environment.Find("Water/River"), Is.Not.Null);
            Assert.That(environment.Find("Water/Waterfall"), Is.Not.Null);

            // the bridge deck must stand ABOVE the water (it was once placed at the river bed height and sat under the surface)
            Transform bridge = environment.Find("Water/Bridge_Wood");
            Assert.That(bridge, Is.Not.Null);
            float deckTop = float.MinValue;
            foreach (BoxCollider box in bridge.GetComponents<BoxCollider>())
            {
                if (box.size.y < 0.3f) { deckTop = Mathf.Max(deckTop, box.bounds.max.y); } // the thin deck box (railings are tall)
            }

            const float waterLevel = -0.12f; // GmLayout.WaterLevel
            Assert.That(deckTop, Is.GreaterThan(waterLevel + 0.4f), "the bridge deck is clearly above the water surface");

            var crystals = new List<Transform>();
            foreach (Transform t in environment.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Crystal"))
                {
                    crystals.Add(t);
                    Assert.That(t.GetComponentInChildren<TreasureSpot>(true), Is.Null, t.name + " is decoration, never a treasure");
                }
            }

            Assert.That(crystals.Count, Is.GreaterThanOrEqualTo(5), "gold crystals are spread around the valley");
            foreach (Transform crystal in crystals)
            {
                if (!crystal.name.StartsWith("Crystal_Cluster"))
                {
                    continue; // the cluster props are the free-standing decoration; crates / rocks that carry crystals are checked by the distance below
                }

                Assert.That(crystal.GetComponents<Collider>().Length, Is.EqualTo(0), crystal.name + " has no collider");
            }

            foreach (TreasureSpot spot in All<TreasureSpot>())
            {
                foreach (Transform crystal in crystals)
                {
                    Vector3 d = crystal.position - spot.transform.position;
                    d.y = 0f;
                    Assert.That(d.magnitude, Is.GreaterThan(3.0f), $"decorative {crystal.name} is clearly apart from the treasure point {spot.SpotId}");
                }
            }
        }

        // ================================================================ treasure data

        [UnityTest]
        public IEnumerator TreasureSpots_HaveUniquePersistentIds_AndDoNotCompleteTheGoal()
        {
            yield return LoadScene();
            TreasureSpot[] spots = All<TreasureSpot>();
            Assert.That(spots.Length, Is.InRange(6, 8));

            var seen = new HashSet<string>();
            int total = 0;
            foreach (TreasureSpot spot in spots)
            {
                Assert.That(spot.HasExplicitId, Is.True, spot.name + " needs an explicit, position-independent id");
                Assert.That(spot.SpotId, Does.StartWith("green_mine."));
                Assert.That(spot.SpotId, Is.Not.EqualTo(GameplayTestId), "must not share a save id with the GameplayTest sandbox");
                Assert.That(seen.Add(spot.SpotId), Is.True, "duplicate id " + spot.SpotId);
                Assert.That(spot.Definition, Is.Not.Null);
                Assert.That(spot.Definition.Validate(out string error), Is.True, error);
                total += spot.Definition.GoldValue;
            }

            Assert.That(TreasureIdValidator.Validate(spots).IsValid, Is.True);
            var catalog = AreaCatalog.CreateDefault();
            Assert.That(total, Is.LessThan(catalog.Get("green_mine").goalTotalGold), "the temporary points must not complete the 500 gold goal artificially");
            Assert.That(total, Is.GreaterThan(100), "but there is real gold to find");
            Debug.Log($"GreenMine temporary treasure: {spots.Length} points, {total} gold in total (goal {catalog.Get("green_mine").goalTotalGold}).");
        }

        [UnityTest]
        public IEnumerator EveryTreasure_CanBeDetected_Dug_AndCollected_AndSavedOnce_WithoutCompletingTheArea()
        {
            var store = new MemoryProgressStore();
            yield return LoadScene(store);
            var hunt = Root("GameplaySystems").GetComponentInChildren<TreasureHuntController>(true);
            var detector = Root("GameplaySystems").GetComponentInChildren<TreasureDetector>(true);
            TreasureSpot[] spots = All<TreasureSpot>();
            Assert.That(hunt.TotalGold, Is.EqualTo(0), "a fresh in-memory save");

            // the detector: no signal far from everything, a stronger signal closer
            Teleport(Player, new Vector3(-8f, 2f, -17f));
            yield return null;
            yield return null;
            yield return null;

            int expected = 0;
            foreach (TreasureSpot spot in spots)
            {
                Teleport(Player, StandingPlaceNear(spot) + Vector3.up * 0f);
                yield return null;
                yield return null;
                Assert.That(detector.HasSignal, Is.True, "the detector signals near " + spot.SpotId);
                Assert.That(detector.NearestHidden, Is.SameAs(spot), "nearest hidden treasure is the one being searched: " + spot.SpotId);
                Assert.That(detector.Proximity, Is.GreaterThan(0.8f), "inside the dig range the signal is at its strongest");

                yield return DigAndCollect(spot);
                expected += spot.Definition.GoldValue;
                Assert.That(hunt.TotalGold, Is.EqualTo(expected), "gold after " + spot.SpotId);
                Assert.That(Saved(store).collectedTreasureIds, Contains.Item(spot.SpotId), "the id is saved");
            }

            ProgressService service = Root("GameplaySystems").GetComponentInChildren<ProgressManager>(true).Service;
            Assert.That(service.IsAreaCompleted("green_mine"), Is.False, "finding every temporary treasure does not complete Green Mine (goal 500)");
            Assert.That(service.TotalGold, Is.EqualTo(expected));

            // collecting again pays nothing
            FindButton("CollectButton").onClick.Invoke();
            yield return null;
            Assert.That(hunt.TotalGold, Is.EqualTo(expected), "no double payment");
        }

        [UnityTest]
        public IEnumerator CollectedTreasure_IsRememberedAfterTheSceneIsLoadedAgain()
        {
            var store = new MemoryProgressStore();
            yield return LoadScene(store);
            TreasureSpot forest = Spot("green_mine.forest_01");
            yield return DigAndCollect(forest);
            int gold = forest.Definition.GoldValue;
            var hunt = Root("GameplaySystems").GetComponentInChildren<TreasureHuntController>(true);
            Assert.That(hunt.TotalGold, Is.EqualTo(gold));
            Assert.That(Saved(store).totalGold, Is.EqualTo(gold), "saved to the store");
            yield return UnloadScene();

            yield return LoadScene(store); // "a new run" on the same save
            Assert.That(Spot("green_mine.forest_01").State, Is.EqualTo(TreasureState.Collected), "already collected: not available again");
            Assert.That(Spot("green_mine.forest_02").State, Is.EqualTo(TreasureState.Hidden));
            Assert.That(Root("GameplaySystems").GetComponentInChildren<TreasureHuntController>(true).TotalGold, Is.EqualTo(gold), "gold survives");
        }

        // ================================================================ player and character

        [UnityTest]
        public IEnumerator Player_SpawnsWithTheChosenMiner_Barnaby_And_Ruby()
        {
            foreach (string id in new[] { CharacterSelection.MaleId, CharacterSelection.FemaleId })
            {
                CharacterSelection.Select(id);
                yield return LoadScene();
                var visual = Player.GetComponent<PlayerCharacterVisual>();
                Assert.That(visual.AppliedId, Is.EqualTo(id), "the miner chosen in the menu is the one in GreenMine");
                Assert.That(visual.Model, Is.Not.Null);
                Assert.That(visual.Model.GetComponentInChildren<Renderer>(true), Is.Not.Null, "a visible model");
                yield return UnloadScene();
            }
        }

        [UnityTest]
        public IEnumerator Player_StartsInTheCamp_Grounded_WithTheCameraBehind()
        {
            yield return LoadScene();
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            Transform player = Player;
            var controller = player.GetComponent<CharacterController>();
            Assert.That(controller.isGrounded, Is.True, "the player stands on the ground at the start");
            Transform camp = Root("GreenMineEnvironment").transform.Find("StartCamp");
            Assert.That(camp, Is.Not.Null);
            Vector3 fire = camp.Find("Campfire").position;
            Vector3 d = player.position - fire;
            d.y = 0f;
            Assert.That(d.magnitude, Is.LessThan(8f), "the player starts in the safe camp area");

            Camera cam = Camera.main;
            Assert.That(cam, Is.Not.Null);
            float distance = Vector3.Distance(cam.transform.position, player.position + new Vector3(0f, 1.4f, 0f));
            Assert.That(distance, Is.InRange(3.5f, 5.3f), "the third-person camera sits at its normal distance at the start");
            Assert.That(Camera.main.GetComponent<ThirdPersonCameraController>().Target, Is.SameAs(player));
        }

        // ================================================================ reachability (physics grid, no NavMesh)

        /// <summary>1 m grid over the valley. A cell is walkable when the terrain there is not too steep and a player-sized capsule fits; two cells are connected
        /// when a CharacterController-sized capsule can travel between them (so walls, trunks, rocks, fences and the like cut the connection).</summary>
        private sealed class WalkGrid
        {
            public const int Half = 23;
            public const int N = Half * 2 + 1;
            public readonly float[,] Ground = new float[N, N];
            public readonly bool[,] HasGround = new bool[N, N];
            public readonly bool[,] Free = new bool[N, N];

            public Vector3 P(int i, int j) => new(i - Half, Ground[i, j], j - Half);

            public static Vector2Int Cell(Vector3 p) => new(Mathf.RoundToInt(p.x) + Half, Mathf.RoundToInt(p.z) + Half);

            public void Build(Collider terrain)
            {
                for (int i = 0; i < N; i++)
                {
                    for (int j = 0; j < N; j++)
                    {
                        var ray = new Ray(new Vector3(i - Half, 60f, j - Half), Vector3.down);
                        if (!terrain.Raycast(ray, out RaycastHit hit, 120f) || hit.normal.y < 0.74f)
                        {
                            continue; // steeper than ~42 degrees: the CharacterController (slope limit 45) cannot use it comfortably
                        }

                        HasGround[i, j] = true;
                        Ground[i, j] = hit.point.y;
                        Free[i, j] = !Physics.CheckCapsule(hit.point + Vector3.up * 0.6f, hit.point + Vector3.up * 1.4f, 0.45f, ~0, QueryTriggerInteraction.Ignore);
                    }
                }
            }

            private bool CanStep(int ci, int cj, int ni, int nj)
            {
                if (ni < 0 || nj < 0 || ni >= N || nj >= N || !HasGround[ni, nj] || !Free[ni, nj] || Mathf.Abs(Ground[ni, nj] - Ground[ci, cj]) > 0.45f)
                {
                    return false;
                }

                Vector3 a = P(ci, cj), b = P(ni, nj);
                Vector3 dir = b - a;
                return !Physics.CapsuleCast(a + Vector3.up * 0.6f, a + Vector3.up * 1.4f, 0.4f, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore);
            }

            /// <summary>Breadth-first search from <paramref name="start"/>; returns the parent map (null for unreached) and how many cells were reached.</summary>
            public Vector2Int?[,] Flood(Vector2Int start, out int count, Vector2Int? stopAt = null)
            {
                var parent = new Vector2Int?[N, N];
                var seen = new bool[N, N];
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                seen[start.x, start.y] = true;
                parent[start.x, start.y] = start;
                count = 1;
                while (queue.Count > 0)
                {
                    Vector2Int c = queue.Dequeue();
                    if (stopAt.HasValue && c == stopAt.Value)
                    {
                        break;
                    }

                    for (int di = -1; di <= 1; di++)
                    {
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            int ni = c.x + di, nj = c.y + dj;
                            if ((di == 0 && dj == 0) || ni < 0 || nj < 0 || ni >= N || nj >= N || seen[ni, nj] || !CanStep(c.x, c.y, ni, nj))
                            {
                                continue;
                            }

                            seen[ni, nj] = true;
                            parent[ni, nj] = c;
                            queue.Enqueue(new Vector2Int(ni, nj));
                            count++;
                        }
                    }
                }

                return parent;
            }

            /// <summary>Cells of a walkable route from <paramref name="from"/> to the reachable cell nearest to <paramref name="to"/> (within 2.2 m), or null.</summary>
            public List<Vector3> Route(Vector3 from, Vector3 to)
            {
                Vector2Int start = Cell(from);
                Vector2Int?[,] parent = Flood(start, out _);
                Vector2Int? best = null;
                float bestDistance = 2.2f;
                for (int i = 0; i < N; i++)
                {
                    for (int j = 0; j < N; j++)
                    {
                        if (parent[i, j] == null)
                        {
                            continue;
                        }

                        float d = new Vector2(i - Half - to.x, j - Half - to.z).magnitude;
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = new Vector2Int(i, j);
                        }
                    }
                }

                if (!best.HasValue)
                {
                    return null;
                }

                var route = new List<Vector3>();
                Vector2Int cell = best.Value;
                while (cell != start)
                {
                    route.Add(P(cell.x, cell.y));
                    cell = parent[cell.x, cell.y].Value;
                }

                route.Reverse();
                return route;
            }
        }

        private IEnumerator BuildGrid(System.Action<WalkGrid> done)
        {
            Collider terrain = Root("GreenMineEnvironment").transform.Find("Terrain/Ground").GetComponent<Collider>();
            Teleport(Player, new Vector3(500f, 500f, 500f)); // the player's own capsule must not block the analysis
            yield return null;
            Physics.SyncTransforms();
            var grid = new WalkGrid();
            grid.Build(terrain);
            done(grid);
        }

        [UnityTest]
        public IEnumerator EveryExplorationArea_IsReachableOnFoot_AndTheWallsKeepThePlayerInside()
        {
            yield return LoadScene();
            Vector3 spawn = Player.position;
            WalkGrid grid = null;
            yield return BuildGrid(g => grid = g);

            Vector2Int start = WalkGrid.Cell(spawn);
            Assert.That(grid.Free[start.x, start.y], Is.True, "the spawn cell is free");
            Vector2Int?[,] parent = grid.Flood(start, out int count);
            Debug.Log($"GreenMine reachability: {count} walkable 1 m cells reachable from the spawn.");
            Assert.That(count, Is.GreaterThan(700), "a large part of the ~39 x 39 m valley is walkable");

            bool NearReached(Vector3 p, float radius)
            {
                for (int i = 0; i < WalkGrid.N; i++)
                {
                    for (int j = 0; j < WalkGrid.N; j++)
                    {
                        if (parent[i, j] != null && new Vector2(i - WalkGrid.Half - p.x, j - WalkGrid.Half - p.z).magnitude <= radius)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            foreach (TreasureSpot spot in All<TreasureSpot>())
            {
                Assert.That(NearReached(spot.transform.position, 2.0f), Is.True, $"{spot.SpotId} can be reached: a walkable cell lies within the dig range (2.5 m)");
            }

            foreach (Transform waypoint in Root("GreenMineEnvironment").transform.Find("Path/TourWaypoints"))
            {
                Assert.That(NearReached(waypoint.position, 1.5f), Is.True, waypoint.name + " is reachable");
            }

            foreach (string zone in new[] { "Zone_StartCamp", "Zone_MineEntrance", "Zone_Forest", "Zone_RockyTreasureArea", "Zone_Meadow" })
            {
                Transform marker = Root("GreenMineEnvironment").transform.Find("Markers/" + zone);
                Assert.That(NearReached(marker.position, 3.0f), Is.True, zone + " can be reached");
            }

            // the invisible walls: nothing outside the playable area is reachable (the grid reaches 23 m, the walls stand at ~19.6 m)
            int outside = 0;
            for (int i = 0; i < WalkGrid.N; i++)
            {
                for (int j = 0; j < WalkGrid.N; j++)
                {
                    bool mineCorridor = i - WalkGrid.Half > 3 && i - WalkGrid.Half < 10 && j - WalkGrid.Half > 14; // the walkable tunnel behind the open portal
                    if (parent[i, j] != null && !mineCorridor && (Mathf.Abs(i - WalkGrid.Half) > 20 || Mathf.Abs(j - WalkGrid.Half) > 20))
                    {
                        outside++;
                    }
                }
            }

            Assert.That(outside, Is.EqualTo(0), "the player cannot leave the playable area");
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator WalkingOffTheTrail_ToEveryTreasurePoint_ReachesIt_AndTheCameraKeepsItsDistance()
        {
            yield return LoadScene();
            WalkGrid grid = null;
            yield return BuildGrid(g => grid = g);
            Transform player = Player;
            var pc = player.GetComponent<ThirdPersonPlayerController>();
            Camera camera = Camera.main;
            Collider terrain = Root("GreenMineEnvironment").transform.Find("Terrain/Ground").GetComponent<Collider>();

            // start again in the camp
            Vector3 spawnPoint = grid.P(WalkGrid.Cell(new Vector3(-8f, 0f, -10.4f)).x, WalkGrid.Cell(new Vector3(-8f, 0f, -10.4f)).y);
            Teleport(player, spawnPoint + Vector3.up * 1.1f);
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            Time.timeScale = 2f;
            float minCamera = float.MaxValue;
            int frames = 0, squeezed = 0, inFoliage = 0;

            // foliage has no collider (only the trunk does), so measure how often the camera ends up inside a tree crown
            var crowns = new List<Bounds>();
            foreach (MeshRenderer mr in Root("GreenMineEnvironment").GetComponentsInChildren<MeshRenderer>(false))
            {
                if (mr.name.StartsWith("Pine") || mr.name.StartsWith("Tree_Round"))
                {
                    crowns.Add(mr.bounds);
                }
            }

            foreach (TreasureSpot spot in All<TreasureSpot>())
            {
                List<Vector3> route = grid.Route(player.position - Vector3.up * 1.1f, spot.transform.position);
                Assert.That(route, Is.Not.Null, "a walkable route exists to " + spot.SpotId);
                foreach (Vector3 cell in route)
                {
                    float legTime = 0f;
                    while (true)
                    {
                        Vector3 delta = new Vector3(cell.x - player.position.x, 0f, cell.z - player.position.z);
                        if (delta.magnitude < 0.7f)
                        {
                            break;
                        }

                        legTime += Time.deltaTime;
                        Assert.That(legTime, Is.LessThan(5f), $"stuck on the way to {spot.SpotId} near {cell} (player at {player.position})");

                        Vector3 f = camera.transform.forward;
                        f.y = 0f;
                        f.Normalize();
                        Vector3 r = new(f.z, 0f, -f.x);
                        Vector3 d = delta.normalized;
                        pc.SetMoveInput(new Vector2(Vector3.Dot(d, r), Vector3.Dot(d, f)));
                        yield return null;

                        frames++;
                        float distance = Vector3.Distance(camera.transform.position, player.position + new Vector3(0f, 1.4f, 0f));
                        minCamera = Mathf.Min(minCamera, distance);
                        if (distance < 3.0f)
                        {
                            squeezed++;
                        }

                        Vector3 cp = camera.transform.position;
                        foreach (Bounds crown in crowns)
                        {
                            if (new Vector2(cp.x - crown.center.x, cp.z - crown.center.z).magnitude < crown.extents.x * 0.55f && cp.y > crown.min.y + 1.0f && cp.y < crown.max.y)
                            {
                                inFoliage++;
                                break;
                            }
                        }
                    }
                }

                pc.ClearMoveInput();
                float arrived = new Vector2(player.position.x - spot.transform.position.x, player.position.z - spot.transform.position.z).magnitude;
                Assert.That(arrived, Is.LessThan(2.5f + 0.5f), $"the player stands within the dig range of {spot.SpotId} ({arrived:F1} m)");
            }

            pc.ClearMoveInput();
            Debug.Log($"GreenMine off-trail walk to all {All<TreasureSpot>().Length} treasure points: {frames} frames, min camera distance {minCamera:F2} m, camera closer than 3 m in {squeezed} frames ({100f * squeezed / Mathf.Max(1, frames):F1}%), camera inside a tree crown in {inFoliage} frames ({100f * inFoliage / Mathf.Max(1, frames):F1}%).");
            Assert.That(inFoliage / (float)Mathf.Max(1, frames), Is.LessThan(0.12f), "the camera rarely ends up inside a tree crown");
            Assert.That(minCamera, Is.GreaterThan(1.2f), "the camera is never pushed into the player");
            Assert.That(squeezed / (float)Mathf.Max(1, frames), Is.LessThan(0.15f), "the camera is rarely squeezed by scenery while walking through the forest and rocks");
            Assert.That(terrain, Is.Not.Null);
        }

        // ================================================================ walking tour (real CharacterController + camera)

        private static readonly Vector2[] Tour =
        {
            new(-5.0f, -7.8f),    // first junction
            new(-9.0f, -3.0f),    // west meadow
            new(-12.0f, 1.5f),
            new(-11.0f, 4.2f),    // forest edge
            new(-13.0f, 6.6f),    // forest trail
            new(-14.6f, 9.0f),    // forest clearing
            new(-11.0f, 4.2f),
            new(-8.6f, 6.4f),
            new(-1.6f, 8.4f),     // north meadow
            new(2.2f, 8.4f),
            new(4.9f, 9.7f),
            new(5.9f, 11.8f),     // mine apron
            new(8.2f, 10.4f),
            new(9.6f, 7.7f),
            new(9.2f, 4.2f),
            new(13.0f, 3.7f),     // bridge, west end
            new(17.6f, 3.8f),     // far bank (over the deck)
            new(13.0f, 3.7f),
            new(9.2f, 4.2f),
            new(7.6f, 1.2f),
            new(5.0f, -1.0f),
            new(7.8f, -3.8f),     // rocky trail
            new(10.2f, -6.4f),
            new(12.0f, -9.4f),    // rocky clearing
            new(10.2f, -6.4f),
            new(5.0f, -1.0f),
            new(2.0f, -2.6f),
            new(-1.0f, -3.8f),
            new(-3.6f, -5.0f),
            new(-5.0f, -7.8f),
        };

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator WalkingTheWholeTrailNetwork_NeverGetsStuck_StaysOnTheGround_AndTheCameraDoesNotSqueeze()
        {
            yield return LoadScene();
            Time.timeScale = 2f; // the same code path, half the wall-clock time
            Transform player = Player;
            var controller = player.GetComponent<ThirdPersonPlayerController>();
            var cam = Camera.main.GetComponent<ThirdPersonCameraController>();
            Camera camera = Camera.main;
            Collider terrain = Root("GreenMineEnvironment").transform.Find("Terrain/Ground").GetComponent<Collider>();

            float minCameraDistance = float.MaxValue;
            int squeezedFrames = 0, frames = 0;
            float worstHeight = 0f, lowestHeight = float.MaxValue, maxYOnBridge = float.MinValue;
            for (int leg = 0; leg < Tour.Length; leg++)
            {
                Vector2 target = Tour[leg];
                float legTime = 0f;
                float legStart = Vector2.Distance(new Vector2(player.position.x, player.position.z), target);
                while (true)
                {
                    Vector3 delta = new Vector3(target.x - player.position.x, 0f, target.y - player.position.z);
                    if (delta.magnitude < 1.1f)
                    {
                        break;
                    }

                    legTime += Time.deltaTime;
                    Assert.That(legTime, Is.LessThan(legStart / 3.5f + 4f), $"stuck on leg {leg} towards {target} at {player.position}");

                    Vector3 f = camera.transform.forward;
                    f.y = 0f;
                    f.Normalize();
                    Vector3 r = new(f.z, 0f, -f.x);
                    Vector3 d = delta.normalized;
                    controller.SetMoveInput(new Vector2(Vector3.Dot(d, r), Vector3.Dot(d, f)));
                    yield return null;

                    frames++;
                    if (Mathf.Abs(player.position.x - 14.5f) < 1.2f && Mathf.Abs(player.position.z - 3.7f) < 1.2f)
                    {
                        maxYOnBridge = Mathf.Max(maxYOnBridge, player.position.y);
                    }

                    float cameraDistance = Vector3.Distance(camera.transform.position, player.position + new Vector3(0f, 1.4f, 0f));
                    minCameraDistance = Mathf.Min(minCameraDistance, cameraDistance);
                    if (cameraDistance < 3.0f)
                    {
                        squeezedFrames++;
                    }

                    if (terrain.Raycast(new Ray(player.position + Vector3.up * 5f, Vector3.down), out RaycastHit hit, 30f))
                    {
                        float height = player.position.y - hit.point.y;
                        worstHeight = Mathf.Max(worstHeight, height);
                        lowestHeight = Mathf.Min(lowestHeight, height);
                    }
                }
            }

            controller.ClearMoveInput();
            Debug.Log($"GreenMine walk: {Tour.Length} legs, {frames} frames, min camera distance {minCameraDistance:F2} m, frames with the camera closer than 3 m: {squeezedFrames} ({100f * squeezedFrames / Mathf.Max(1, frames):F1}%), player height above ground {lowestHeight:F2}..{worstHeight:F2} m.");
            Assert.That(maxYOnBridge, Is.GreaterThan(-0.12f + 0.4f + 0.9f), "the player really walks over the bridge deck (feet above the water), not through the stream below it");
            Assert.That(lowestHeight, Is.GreaterThan(0.6f), "the player never sinks into the ground");
            Assert.That(worstHeight, Is.LessThan(2.0f), "the player never floats or climbs onto something");
            Assert.That(minCameraDistance, Is.GreaterThan(1.6f), "the camera is never pushed into the player");
            Assert.That(squeezedFrames / (float)frames, Is.LessThan(0.08f), "the camera is rarely squeezed by scenery along the whole tour");
            Assert.That(cam.Target, Is.SameAs(player));
        }

        [UnityTest]
        public IEnumerator InvisibleWalls_NeverPullTheCameraIn()
        {
            yield return LoadScene();
            Camera camera = Camera.main;
            var cameraController = camera.GetComponent<ThirdPersonCameraController>();
            var so = new SerializedObject(cameraController);
            uint mask = so.FindProperty("collisionMask").FindPropertyRelative("m_Bits").uintValue;
            Assert.That(mask, Is.Not.Zero, "a zero mask would make the camera collide with nothing at all");
            Assert.That((mask & 1u) != 0u, Is.True, "the camera collides with the default layer (terrain, rocks, trunks)");
            Assert.That((mask & (1u << 2)) == 0u, Is.True, "the camera collision mask ignores the 'Ignore Raycast' layer (invisible walls)");

            Transform walls = Root("GreenMineEnvironment").transform.Find("Boundaries");
            Assert.That(walls.childCount, Is.GreaterThanOrEqualTo(4));
            foreach (Transform wall in walls)
            {
                Assert.That(wall.gameObject.layer, Is.EqualTo(2), wall.name + " is on the Ignore Raycast layer");
                Assert.That(wall.GetComponent<BoxCollider>(), Is.Not.Null);
            }

            // standing at an open edge with the camera looking outwards: only the wall is behind the player
            Teleport(Player, new Vector3(-8f, 0.5f, -17.6f));
            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }

            float distance = Vector3.Distance(camera.transform.position, Player.position + new Vector3(0f, 1.4f, 0f));
            Assert.That(distance, Is.GreaterThan(3.0f), "near the south wall the camera keeps its distance (the wall is invisible to it)");
        }

        [UnityTest]
        public IEnumerator Camera_CollidesWithScenery_AndKeepsItsFullDistanceInTheOpen()
        {
            yield return LoadScene();
            Camera camera = Camera.main;
            var so = new SerializedObject(camera.GetComponent<ThirdPersonCameraController>());
            float nominal = so.FindProperty("distance").floatValue;
            Transform player = Player;

            // the player stands 1.3 m in front of the mine's rock face (beside the open portal) and the camera orbits to the far side of it
            Teleport(player, new Vector3(11.6f, 1.2f, 13.2f));
            so.FindProperty("yaw").floatValue = 180f;
            so.FindProperty("pitch").floatValue = 18f;
            so.ApplyModifiedPropertiesWithoutUndo();
            yield return new WaitForSecondsRealtime(0.9f); // the camera eases to its target in ~0.3 s
            Vector3 pivot = player.position + new Vector3(0f, 1.4f, 0f);
            float blocked = Vector3.Distance(camera.transform.position, pivot);
            Assert.That(blocked, Is.LessThan(nominal - 1.5f), "the camera is stopped by the rock face behind the player");
            Assert.That(Physics.CheckSphere(camera.transform.position, 0.15f, ~0, QueryTriggerInteraction.Ignore), Is.False, "the camera never ends up inside scenery");

            // in the open meadow it rides at its full distance
            Teleport(player, new Vector3(-1.4f, 1.2f, 0.4f));
            so.FindProperty("yaw").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            yield return new WaitForSecondsRealtime(0.9f);
            pivot = player.position + new Vector3(0f, 1.4f, 0f);
            Assert.That(Vector3.Distance(camera.transform.position, pivot), Is.EqualTo(nominal).Within(0.15f), "no scenery behind the player: full camera distance");
        }

        // ================================================================ pause / menu

        [UnityTest]
        public IEnumerator Pause_Resume_AndReturnToMainMenu_Work()
        {
            yield return LoadScene();
            string requested = null;
            SceneNavigator.LoadHook = name => requested = name;

            FindButton("PauseButton").onClick.Invoke();
            Assert.That(GamePause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            // while paused, DIG does nothing
            TreasureSpot spot = Spot("green_mine.forest_01");
            Teleport(Player, StandingPlaceNear(spot));
            yield return null;
            FindButton("DigButton").onClick.Invoke();
            yield return null;
            Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden), "gameplay is frozen while paused");

            FindButton("ResumeButton").onClick.Invoke();
            Assert.That(GamePause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            FindButton("PauseButton").onClick.Invoke();
            FindButton("MainMenuButton").onClick.Invoke();
            Assert.That(requested, Is.EqualTo(SceneNavigator.MainMenuScene), "MAIN MENU asks for the menu scene");
            Assert.That(GamePause.IsPaused, Is.False, "the menu must never start frozen");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        // ================================================================ mobile guard rails (static budget, NOT a device measurement)

        [UnityTest]
        public IEnumerator Environment_StaysWithinTheMobileBudget()
        {
            yield return LoadScene();
            Transform environment = Root("GreenMineEnvironment").transform;

            int triangles = 0, renderers = 0, transparent = 0;
            var materials = new HashSet<Material>();
            foreach (MeshRenderer r in environment.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                var filter = r.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    triangles += filter.sharedMesh.triangles.Length / 3;
                }

                foreach (Material m in r.sharedMaterials)
                {
                    Assert.That(m, Is.Not.Null, "no missing material on " + r.name);
                    Assert.That(m.shader.name, Does.Not.Contain("Hidden/InternalErrorShader"), r.name);
                    if (materials.Add(m) && m.renderQueue >= 3000)
                    {
                        transparent++;
                    }
                }

                Assert.That(GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic), Is.True, r.name + " is batching-static");
            }

            Debug.Log($"GreenMine environment: {renderers} renderers, {triangles} triangles (every instance counted, nothing culled), {materials.Count} materials ({transparent} transparent).");
            Assert.That(triangles, Is.LessThan(140000), "triangle budget for the whole valley (every instance counted)");
            Assert.That(renderers, Is.LessThan(700));
            Assert.That(materials.Count, Is.LessThan(70), "a small shared material set keeps batching effective");
            Assert.That(transparent, Is.LessThanOrEqualTo(5), "few transparent materials (water, waterfall, glow)");

            // lights: the sun outdoors; the mine interior adds a handful of shadowless point lights and nothing else
            var lights = new List<Light>();
            int interiorLights = 0;
            foreach (Light l in All<Light>())
            {
                if (l.enabled && l.gameObject.activeInHierarchy)
                {
                    if (l.transform.IsChildOf(environment.Find("MineInterior")))
                    {
                        interiorLights++;
                        Assert.That(l.type, Is.EqualTo(LightType.Point), l.name);
                        Assert.That(l.shadows, Is.EqualTo(LightShadows.None), l.name);
                        continue;
                    }

                    lights.Add(l);
                }
            }

            Assert.That(interiorLights, Is.LessThanOrEqualTo(8), "a few warm lights inside the mine");
            Assert.That(lights.Count, Is.EqualTo(1), "one directional light outdoors, no other point / spot lights: " + string.Join(", ", lights.ConvertAll(l => l.name + "/" + l.type)));
            Assert.That(lights[0].type, Is.EqualTo(LightType.Directional));
            Assert.That(RenderSettings.sun, Is.SameAs(lights[0]));

            // colliders only where needed
            foreach (Collider c in environment.GetComponentsInChildren<Collider>(true))
            {
                string n = c.gameObject.name;
                foreach (string decor in new[] { "Flower", "Grass", "Bush", "Mushroom", "Rock_S", "Crystal_Cluster", "Backdrop", "Mountain", "RailTrack", "Foam" })
                {
                    Assert.That(n, Does.Not.StartWith(decor), n + " is decoration and needs no collider");
                }

                if (c is MeshCollider mc && c.gameObject.name != "Ground")
                {
                    Assert.That(mc.convex, Is.True, n + ": only the terrain uses a non-convex mesh collider");
                }
            }

            Assert.That(environment.GetComponentsInChildren<Collider>(true).Length, Is.LessThan(260));
            Assert.That(RenderSettings.skybox, Is.Not.Null);
            Assert.That(RenderSettings.fog, Is.True);
        }
    }
}
#endif
