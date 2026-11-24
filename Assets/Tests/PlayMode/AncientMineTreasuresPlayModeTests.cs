#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
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
    /// Ancient Mine treasures and the final progression (stage 16C): exactly 25 permanent treasures worth 1240 gold in the agreed rarity and area split, unique ids that never clash with Green
    /// Mine's or Rocky Canyon's, placed on real, reachable, clear ground inside their own area (checked with a physics walk graph made of the miner's own capsule), the real DIG / Reveal /
    /// COLLECT flow of every treasure with both miners, gold paid once, progress kept over scene changes and an app restart, NEW GAME resetting all three areas (and only progress), and the
    /// cumulative 500 / 1200 / 2500 thresholds with the 2705 gold ceiling. Only in-memory stores are used; the developer's real save file is hashed before the first and after the last test.
    /// Walking uses the real movement system; DIG / COLLECT are pressed with onClick.Invoke (a real finger on a phone is NOT tested here).
    /// </summary>
    public sealed class AncientMineTreasuresPlayModeTests
    {
        private const string AncientPath = "Assets/Scenes/AncientMine.unity";
        private const string RockyPath = "Assets/Scenes/RockyCanyon.unity";
        private const string GreenMinePath = "Assets/Scenes/GreenMine.unity";
        private static readonly string[] Miners = { CharacterSelection.MaleId, CharacterSelection.FemaleId };

        // zone group -> expected (common, uncommon, rare, epic, gold)
        private static readonly (string group, string prefix, int c, int u, int r, int e, int gold)[] Split =
        {
            ("A_AncientGate", "gate", 2, 1, 0, 0, 45),
            ("B_HallOfPillars", "hall", 1, 2, 2, 0, 160),
            ("C_CrystalCaverns", "crystal", 1, 1, 3, 2, 385),
            ("D_ForgottenWorkshop", "workshop", 0, 2, 2, 1, 250),
            ("E_RoyalVault", "vault", 0, 0, 2, 3, 400),
        };

        private static string realSavePath;
        private static string realSaveHashBefore;

        private Scene scene;
        private MemoryProgressStore store;
        private float previousTimeScale;

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
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
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
            GameSettings.StoreFactoryOverride = () => new MemorySettingsStore();
            GameSettings.ResetCurrent();
        }

        // ------------------------------------------------------------------ helpers

        private IEnumerator Load(string path, string saveJson = null, MemoryProgressStore useStore = null)
        {
            store = useStore ?? (saveJson != null ? new MemoryProgressStore(saveJson) : new MemoryProgressStore());
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

        private IEnumerator Unload()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            scene = default;
            yield return null;
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

        private List<TreasureSpot> AncientSpots() => RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true).OrderBy(s => s.SpotId, System.StringComparer.Ordinal).ToList();

        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static float GroundUnder(Vector3 position, Transform ignoreRoot)
        {
            float best = float.MinValue;
            foreach (RaycastHit hit in Physics.RaycastAll(position, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                if ((ignoreRoot == null || hit.collider.transform.root != ignoreRoot) && hit.point.y > best)
                {
                    best = hit.point.y;
                }
            }

            return best;
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

        private static IEnumerator WaitUntil(System.Func<bool> condition, float realSeconds)
        {
            float end = Time.realtimeSinceStartup + realSeconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        /// <summary>A save of a player who finished Green Mine and Rocky Canyon (1465 gold): Ancient Mine is open, its own goal is not reached.</summary>
        private static string FinishedTwoAreasSave(IEnumerable<string> ids, int gold)
        {
            string list = string.Join(",", ids.Select(i => "\"" + i + "\""));
            return "{\"version\":1,\"totalGold\":" + gold + ",\"collectedTreasureIds\":[" + list + "],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\",\"ancient_mine\"],\"completedAreaIds\":[\"green_mine\",\"rocky_canyon\"]}";
        }

        /// <summary>The ids and gold of Green Mine and Rocky Canyon together, and the matching save text.</summary>
        private IEnumerator ReadOtherTwo(List<(string id, int gold)> result)
        {
            yield return ReadScene(GreenMinePath, result);
            yield return ReadScene(RockyPath, result);
            Assert.That(result.Count, Is.EqualTo(35), "15 Green Mine + 20 Rocky Canyon treasures");
            Assert.That(result.Sum(g => g.gold), Is.EqualTo(1465), "625 + 840 gold");
        }

        private IEnumerator ReadGreenMine(List<(string id, int gold)> result) => ReadScene(GreenMinePath, result);

        /// <summary>Loads a finished area once (empty save) to read its treasure ids and their gold, and unloads it again.</summary>
        private IEnumerator ReadScene(string path, List<(string id, int gold)> result)
        {
            yield return Load(path);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TreasureSpot s in root.GetComponentsInChildren<TreasureSpot>(true))
                {
                    result.Add((s.SpotId, s.Definition.GoldValue));
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            yield return Unload();
        }

        // ================================================================ a physics walk graph made of the miner's own capsule

        private sealed class WalkGraph
        {
            private const float Cell = 0.5f;
            public readonly List<Vector3> Feet = new();
            private readonly List<List<(int to, float cost)>> edges = new();
            private readonly Dictionary<long, int> index = new();

            private static long Key(int ix, int iz, float y) => ((long)(ix + 20000) << 32) | ((long)(iz + 20000) << 12) | (uint)(Mathf.RoundToInt(y / 0.8f) + 200);

            private static bool CapsuleFree(Vector3 feet, float radius) =>
                !Physics.CheckCapsule(feet + new Vector3(0f, 0.55f, 0f), feet + new Vector3(0f, 1.45f, 0f), radius, ~0, QueryTriggerInteraction.Ignore);

            private static bool Ground(float x, float z, float fromFeetY, out float y)
            {
                y = 0f;
                if (!Physics.Raycast(new Vector3(x, fromFeetY + 0.6f, z), Vector3.down, out RaycastHit hit, 1.35f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.76f)
                {
                    return false;
                }

                y = hit.point.y;
                return y <= fromFeetY + 0.33f && y >= fromFeetY - 0.65f;
            }

            /// <summary>Flood fill from the player's feet over everything the CharacterController could walk (steps up to 0.33 m, slopes to ~40 degrees, a capsule of radius 0.47 must fit).</summary>
            public static WalkGraph Build(Transform player)
            {
                var controller = player.GetComponent<CharacterController>();
                controller.enabled = false; // the miner's own capsule must not block the graph
                Physics.SyncTransforms();
                var graph = new WalkGraph();
                float startY = GroundUnder(player.position, player);
                Vector3 start = new(player.position.x, startY, player.position.z);
                var queue = new Queue<int>();
                graph.Add(start, queue);
                var offsets = new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) };
                while (queue.Count > 0 && graph.Feet.Count < 120000)
                {
                    int current = queue.Dequeue();
                    Vector3 from = graph.Feet[current];
                    foreach (Vector2 o in offsets)
                    {
                        float x = from.x + o.x * Cell, z = from.z + o.y * Cell;
                        if (!Ground(x, z, from.y, out float y))
                        {
                            continue;
                        }

                        var feet = new Vector3(x, y, z);
                        if (!CapsuleFree(feet, 0.47f))
                        {
                            continue;
                        }

                        long key = Key(Mathf.RoundToInt(x / Cell), Mathf.RoundToInt(z / Cell), y);
                        if (!graph.index.TryGetValue(key, out int to))
                        {
                            to = graph.Add(feet, queue);
                        }

                        // paths keep away from walls and things: tight places cost more
                        float cost = o.magnitude * Cell * (CapsuleFree(feet, 1.0f) ? 1f : 2.5f);
                        graph.edges[current].Add((to, cost));
                    }
                }

                controller.enabled = true;
                Physics.SyncTransforms();
                return graph;
            }

            private int Add(Vector3 feet, Queue<int> queue)
            {
                int id = Feet.Count;
                Feet.Add(feet);
                edges.Add(new List<(int, float)>());
                index[Key(Mathf.RoundToInt(feet.x / Cell), Mathf.RoundToInt(feet.z / Cell), feet.y)] = id;
                queue.Enqueue(id);
                return id;
            }

            public int Nearest(Vector3 feet)
            {
                int best = -1;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < Feet.Count; i++)
                {
                    Vector3 d = Feet[i] - feet;
                    float distance = new Vector2(d.x, d.z).sqrMagnitude + d.y * d.y * 4f;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = i;
                    }
                }

                return best;
            }

            /// <summary>The nearest node (horizontally) within <paramref name="radius"/> of the treasure on its own level; -1 if there is none.</summary>
            public int StandNode(Vector3 treasure, float radius)
            {
                int best = -1;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < Feet.Count; i++)
                {
                    Vector3 d = Feet[i] - treasure;
                    float horizontal = new Vector2(d.x, d.z).magnitude;
                    if (horizontal <= radius && Mathf.Abs(d.y) <= 1.0f && Mathf.Abs(horizontal - 1.5f) < bestDistance)
                    {
                        bestDistance = Mathf.Abs(horizontal - 1.5f);
                        best = i;
                    }
                }

                return best;
            }

            /// <summary>Cheapest path (Dijkstra) between two nodes as feet positions; null when not connected.</summary>
            public List<Vector3> Path(int from, int to)
            {
                var distance = new float[Feet.Count];
                var previous = new int[Feet.Count];
                for (int i = 0; i < distance.Length; i++)
                {
                    distance[i] = float.MaxValue;
                    previous[i] = -1;
                }

                var open = new SortedSet<(float d, int id)>();
                distance[from] = 0f;
                open.Add((0f, from));
                while (open.Count > 0)
                {
                    (float d, int id) = open.Min;
                    open.Remove(open.Min);
                    if (id == to)
                    {
                        break;
                    }

                    if (d > distance[id])
                    {
                        continue;
                    }

                    foreach ((int next, float cost) in edges[id])
                    {
                        float nd = d + cost;
                        if (nd < distance[next])
                        {
                            distance[next] = nd;
                            previous[next] = id;
                            open.Add((nd, next));
                        }
                    }
                }

                if (distance[to] == float.MaxValue)
                {
                    return null;
                }

                var path = new List<Vector3>();
                for (int at = to; at != -1; at = previous[at])
                {
                    path.Add(Feet[at]);
                }

                path.Reverse();
                return path;
            }

            public bool Connected(int from, int to) => Path(from, to) != null;
        }

        // ================================================================ walking and camera statistics

        private sealed class Stats
        {
            public int Frames, CameraInRock, CameraBlocked, Run, MaxRun;
            public float MinCameraDistance = float.MaxValue;
            public string FirstRock = "";
        }

        private static void Sample(Transform player, Camera camera, Stats stats)
        {
            stats.Frames++;
            Vector3 pivot = player.position + Vector3.up * 1.4f;
            stats.MinCameraDistance = Mathf.Min(stats.MinCameraDistance, Vector3.Distance(camera.transform.position, pivot));
            Collider[] inside = Physics.OverlapSphere(camera.transform.position, 0.05f, ~0, QueryTriggerInteraction.Ignore);
            stats.Run = inside.Length > 0 ? stats.Run + 1 : 0;
            stats.MaxRun = Mathf.Max(stats.MaxRun, stats.Run);
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

        /// <summary>Walks along a list of feet positions (a path from the walk graph): waypoints about 1.5 m apart, a sidestep when the miner hardly moves.</summary>
        private IEnumerator WalkPath(Transform player, ThirdPersonPlayerController controller, Camera camera, List<Vector3> path, string label, Stats stats)
        {
            var way = new List<Vector3>();
            for (int i = 3; i < path.Count; i += 3)
            {
                way.Add(path[i]);
            }

            way.Add(path[path.Count - 1]);
            float legTime = 0f, checkTime = 0f, sidestepUntil = 0f, sidestepSign = 1f;
            float length = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                length += Vector3.Distance(path[i - 1], path[i]);
            }

            Vector3 checkPosition = player.position;
            foreach (Vector3 target in way)
            {
                bool last = target == path[path.Count - 1];
                while (true)
                {
                    Vector3 delta = new(target.x - player.position.x, 0f, target.z - player.position.z);
                    if (delta.magnitude < (last ? 0.35f : 0.9f))
                    {
                        break;
                    }

                    legTime += Time.deltaTime;
                    Assert.That(legTime, Is.LessThan(length / 2.2f + 10f), $"stuck on the way to {label} at {player.position} (heading for {target})");
                    checkTime += Time.deltaTime;
                    if (checkTime >= 0.6f)
                    {
                        if ((player.position - checkPosition).magnitude < 0.5f)
                        {
                            sidestepUntil = Time.time + 0.5f;
                            sidestepSign = -sidestepSign;
                        }

                        checkPosition = player.position;
                        checkTime = 0f;
                    }

                    Vector3 d = delta.normalized;
                    if (Time.time < sidestepUntil)
                    {
                        d = Quaternion.Euler(0f, 60f * sidestepSign, 0f) * d;
                    }

                    Push(controller, camera, d);
                    yield return null;
                    Sample(player, camera, stats);
                }
            }

            controller.ClearMoveInput();
        }

        /// <summary>Visible things still under a collected spot: model renderers, or a particle emitter that still has live particles (an idle emitter shows nothing).</summary>
        private static int VisibleLeftovers(TreasureSpot spot)
        {
            int n = 0;
            foreach (Renderer r in spot.GetComponentsInChildren<Renderer>(false))
            {
                var ps = r.GetComponent<ParticleSystem>();
                if (ps != null && ps.particleCount == 0)
                {
                    continue;
                }

                n++;
            }

            return n;
        }
        private Transform Env => RootObject("AncientMineEnvironment").transform;

        private static bool IsShell(Collider c) => c.name == "Floor" || c.name == "Ceiling" || c.name == "SafetyFloor";

        // ================================================================ 1. the table

        [UnityTest]
        public IEnumerator ExactlyTwentyFiveTreasures_1240Gold_InTheAgreedRarityAndAreaSplit()
        {
            yield return Load(AncientPath);
            List<TreasureSpot> all = AncientSpots();
            Assert.That(all.Count, Is.EqualTo(25), "exactly 25 Ancient Mine treasures");
            Assert.That(TreasureSpot.Active.Count, Is.EqualTo(25), "and all of them are in play");
            Assert.That(all.Sum(s => s.Definition.GoldValue), Is.EqualTo(1240), "1240 gold in total");

            int c = 0, u = 0, r = 0, e = 0;
            Transform root = RootObject("TreasureSpots").transform;
            Assert.That(root.childCount, Is.EqualTo(5), "five area groups, nothing else (no test points)");
            foreach ((string group, string prefix, int ec, int eu, int er, int ee, int gold) in Split)
            {
                Transform g = root.Find(group);
                Assert.That(g, Is.Not.Null, group);
                var spots = g.GetComponentsInChildren<TreasureSpot>(true);
                Assert.That(spots.Length, Is.EqualTo(ec + eu + er + ee), group + ": treasure count");
                Assert.That(spots.Count(s => s.Definition.Rarity == TreasureRarity.Common), Is.EqualTo(ec), group + ": Common");
                Assert.That(spots.Count(s => s.Definition.Rarity == TreasureRarity.Uncommon), Is.EqualTo(eu), group + ": Uncommon");
                Assert.That(spots.Count(s => s.Definition.Rarity == TreasureRarity.Rare), Is.EqualTo(er), group + ": Rare");
                Assert.That(spots.Count(s => s.Definition.Rarity == TreasureRarity.Epic), Is.EqualTo(ee), group + ": Epic");
                Assert.That(spots.Sum(s => s.Definition.GoldValue), Is.EqualTo(gold), group + ": gold");
                foreach (TreasureSpot s in spots)
                {
                    Assert.That(s.SpotId, Does.StartWith("ancient_mine." + prefix + "_"), group + ": ids carry their area");
                }

                c += ec;
                u += eu;
                r += er;
                e += ee;
            }

            Assert.That((c, u, r, e), Is.EqualTo((4, 6, 9, 6)), "4 Common, 6 Uncommon, 9 Rare, 6 Epic");

            // the existing reward system is used unchanged: the four rarities pay 10 / 25 / 50 / 100
            foreach (TreasureSpot s in all)
            {
                int expected = s.Definition.Rarity switch { TreasureRarity.Common => 10, TreasureRarity.Uncommon => 25, TreasureRarity.Rare => 50, _ => 100 };
                Assert.That(s.Definition.GoldValue, Is.EqualTo(expected), s.SpotId);
            }

            // the same spot components and prefabs as the other two areas: nothing new was invented (no extra detector / hunt controller / treasure system)
            Assert.That(One<TreasureHuntController>(), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<TreasureDetector>(FindObjectsInactive.Include).Count(d => d.gameObject.scene == scene), Is.EqualTo(1), "one detector");
            Assert.That(Object.FindObjectsByType<TreasureHuntController>(FindObjectsInactive.Include).Count(d => d.gameObject.scene == scene), Is.EqualTo(1), "one hunt controller");
        }

        [UnityTest]
        public IEnumerator Ids_AreUniqueExplicitAndPermanent_AndNeverClashWithGreenMineOrRockyCanyon()
        {
            yield return Load(AncientPath);
            List<TreasureSpot> all = AncientSpots();
            var pattern = new Regex(@"^ancient_mine\.(gate|hall|crystal|workshop|vault)_\d\d$");
            foreach (TreasureSpot s in all)
            {
                Assert.That(s.HasExplicitId, Is.True, s.name + " has an explicit id (not a position-based fallback)");
                Assert.That(pattern.IsMatch(s.SpotId), Is.True, s.SpotId);
            }

            Assert.That(all.Select(s => s.SpotId).Distinct().Count(), Is.EqualTo(25), "all 25 ids are different");
            Assert.That(TreasureIdValidator.Validate(TreasureSpot.Active).IsValid, Is.True);
            var ids = all.Select(s => s.SpotId).ToList();
            yield return Unload();

            // permanent: the scene read a second time gives the very same ids
            yield return Load(AncientPath);
            Assert.That(AncientSpots().Select(s => s.SpotId).ToList(), Is.EqualTo(ids), "the ids are the same every time the scene opens");
            yield return Unload();

            var others = new List<(string id, int gold)>();
            yield return ReadOtherTwo(others);
            Assert.That(others.Count(o => o.id.StartsWith("green_mine.")), Is.EqualTo(15));
            Assert.That(others.Count(o => o.id.StartsWith("rocky_canyon.")), Is.EqualTo(20));
            Assert.That(others.Count(o => o.id.StartsWith("green_mine.")) == 15 && others.Where(o => o.id.StartsWith("green_mine.")).Sum(o => o.gold) == 625, Is.True, "Green Mine keeps its 15 treasures / 625 gold");
            Assert.That(others.Where(o => o.id.StartsWith("rocky_canyon.")).Sum(o => o.gold), Is.EqualTo(840), "Rocky Canyon keeps its 20 treasures / 840 gold");
            Assert.That(new HashSet<string>(ids).Overlaps(others.Select(o => o.id)), Is.False, "no id is shared with Green Mine or Rocky Canyon");
            Assert.That(others.Sum(o => o.gold) + 1240, Is.EqualTo(2705), "the three areas together can pay 2705 gold");
        }

        // ================================================================ 2. placement: real floor, clear, in its own area, reachable

        [UnityTest]
        public IEnumerator EveryTreasure_StandsOnTheRealFloor_InItsOwnArea_ClearOfDecor_AndApartFromTheOthers()
        {
            yield return Load(AncientPath);
            Transform env = Env;
            var decor = new List<Vector3>();
            foreach (MeshFilter f in env.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.name == "Structures" || f.name == "Crystals")
                {
                    foreach (Vector3 v in f.sharedMesh.vertices)
                    {
                        decor.Add(f.transform.TransformPoint(v));
                    }
                }
            }

            Assert.That(decor.Count, Is.GreaterThan(10000), "the decoration meshes were found");
            List<TreasureSpot> all = AncientSpots();
            foreach (TreasureSpot s in all)
            {
                Vector3 p = s.transform.position;
                string area = s.transform.parent.name;
                Assert.That(Physics.Raycast(p + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore), Is.True, s.SpotId + ": a floor under it");
                Assert.That(hit.collider.name, Is.EqualTo("Floor"), s.SpotId + ": the first thing under it is the cave floor, not a prop");
                Assert.That(hit.collider.transform.parent.name, Is.EqualTo(area), s.SpotId + ": it stands in its own area (" + area + ")");
                Assert.That(Mathf.Abs(hit.point.y - p.y), Is.LessThan(0.1f), s.SpotId + ": sits ON the floor (not buried, not floating)");
                Assert.That(hit.normal.y, Is.GreaterThan(0.95f), s.SpotId + ": on gentle ground (slope " + Mathf.Acos(hit.normal.y) * Mathf.Rad2Deg + " deg)");
                foreach (Collider c in Physics.OverlapSphere(p + Vector3.up * 0.6f, 1.0f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Assert.That(IsShell(c), Is.True, $"{s.SpotId}: {c.transform.parent?.name}/{c.name} is within 1 m (a pillar, crystal, statue or rubble must not hold the treasure or the miner)");
                }

                Assert.That(Physics.Raycast(p + Vector3.up * 0.5f, Vector3.up, out RaycastHit ceiling, 60f, ~0, QueryTriggerInteraction.Ignore), Is.True, s.SpotId + ": a ceiling above");
                Assert.That(ceiling.distance, Is.GreaterThan(4f), s.SpotId + ": at least 4 m of headroom, nothing overhead (" + ceiling.collider.name + ")");

                float nearest = float.MaxValue;
                foreach (Vector3 v in decor)
                {
                    if (v.y >= p.y + 0.15f && v.y <= p.y + 2f)
                    {
                        nearest = Mathf.Min(nearest, new Vector2(v.x - p.x, v.z - p.z).magnitude);
                    }
                }

                Assert.That(nearest, Is.GreaterThan(0.5f), s.SpotId + ": no decoration pokes into the treasure (nearest vertex " + nearest + " m)");

                // not on an edge: floor all round at 1.5 m, at about the same height
                for (int k = 0; k < 8; k++)
                {
                    Vector3 q = p + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * 1.5f;
                    float floorY = float.NaN;
                    foreach (RaycastHit h in Physics.RaycastAll(q + Vector3.up * 1.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider.name == "Floor")
                        {
                            floorY = h.point.y;
                        }
                    }

                    Assert.That(float.IsNaN(floorY), Is.False, s.SpotId + ": floor round it");
                    Assert.That(Mathf.Abs(floorY - p.y), Is.LessThan(0.8f), s.SpotId + ": level floor round it, not an edge");
                }
            }

            // treasure search areas do not crowd each other: every pair is far enough apart to be a separate find
            float closest = float.MaxValue;
            string closestPair = "";
            for (int i = 0; i < all.Count; i++)
            {
                for (int j = i + 1; j < all.Count; j++)
                {
                    float d = Vector3.Distance(all[i].transform.position, all[j].transform.position);
                    if (d < closest)
                    {
                        closest = d;
                        closestPair = all[i].SpotId + " / " + all[j].SpotId;
                    }
                }
            }

            Debug.Log($"Ancient Mine treasures: closest pair {closest:F1} m ({closestPair})");
            Assert.That(closest, Is.GreaterThan(6f), "no two treasures are close together: " + closestPair);

            // the places kept clear in 16B are still bare markers: a marker that no treasure uses never became one
            Transform candidates = Env.Find("Markers/TreasureCandidates");
            Assert.That(candidates.childCount, Is.EqualTo(26), "the 26 candidate markers are all still there");
            int used = 0, unused = 0;
            foreach (Transform marker in candidates)
            {
                Assert.That(marker.GetComponentsInChildren<Component>().Length, Is.EqualTo(1), marker.name + " is still an empty marker");
                bool taken = all.Any(s => Vector2.Distance(new Vector2(s.transform.position.x, s.transform.position.z), new Vector2(marker.position.x, marker.position.z)) < 0.5f);
                used += taken ? 1 : 0;
                unused += taken ? 0 : 1;
            }

            Debug.Log($"Ancient Mine treasure candidates: {used} used, {unused} left as bare markers (25 treasures = {used} on a marker + {25 - used} on points chosen in 16C)");
            Assert.That(unused, Is.GreaterThanOrEqualTo(1), "at least one marker stays unused");
            Assert.That(used, Is.GreaterThanOrEqualTo(15), "the treasures are mostly on the prepared marker places");
        }

        [UnityTest]
        public IEnumerator TheTreasures_FitTheirAreas_EasyAtTheGate_NoEpicAtTheStart_HiddenOnesOffTheMainRoute_AndTheAltar()
        {
            yield return Load(AncientPath);
            Vector3 spawn = Env.Find("Markers/PlayerSpawn").position;
            var mainRoute = new List<Vector3>();
            foreach (Transform p in Env.Find("Markers/Routes/Main"))
            {
                mainRoute.Add(p.position);
            }

            Assert.That(mainRoute.Count, Is.GreaterThan(10));
            Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
            float FromMain(Vector3 v) => mainRoute.Min(r => Vector3.Distance(Flat(r), Flat(v)));
            foreach (TreasureSpot s in AncientSpots())
            {
                Vector3 p = s.transform.position;
                bool epic = s.Definition.Rarity == TreasureRarity.Epic;
                if (s.SpotId.StartsWith("ancient_mine.gate_"))
                {
                    Assert.That(epic, Is.False, s.SpotId + ": no Epic at the start");
                    Assert.That(Vector3.Distance(Flat(spawn), Flat(p)), Is.LessThan(15f), s.SpotId + ": the gate treasures are easy to find from the start");
                }

                if (s.SpotId.StartsWith("ancient_mine.crystal_") && epic)
                {
                    Assert.That(FromMain(p), Is.GreaterThan(12f), s.SpotId + ": a Crystal Caverns Epic needs exploring: it is off the main route");
                }
            }

            // the Royal Vault: three Epic + two Rare, one Epic by the central altar, three treasures in front of the three alcoves, one by the giant crystal
            List<TreasureSpot> vault = AncientSpots().Where(s => s.SpotId.StartsWith("ancient_mine.vault_")).ToList();
            Assert.That(vault.Count(s => s.Definition.Rarity == TreasureRarity.Epic), Is.EqualTo(3));
            Assert.That(vault.Count(s => s.Definition.Rarity == TreasureRarity.Rare), Is.EqualTo(2));
            Collider altar = Env.Find("E_RoyalVault/Colliders").GetComponentsInChildren<Collider>().First(c => c.name.StartsWith("Altar"));
            float altarDistance = vault.Where(s => s.Definition.Rarity == TreasureRarity.Epic).Min(s => Vector3.Distance(Flat(altar.ClosestPoint(s.transform.position)), Flat(s.transform.position)));
            Assert.That(altarDistance, Is.LessThan(3.5f), "an Epic stands by the central altar (" + altarDistance + " m)");
            foreach (float x in new[] { 0f, 10f, 20f })
            {
                Assert.That(vault.Any(s => Mathf.Abs(s.transform.position.x - x) < 0.5f && s.transform.position.z < -44f), Is.True, "a treasure in front of the alcove at x=" + x);
            }
        }

        [UnityTest]
        public IEnumerator EveryTreasure_CanBeReachedOnFoot_AndTheMinerCanStandInDigRange_BothMiners()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load(AncientPath);
                Transform player = RootObject("Player").transform;
                WalkGraph graph = WalkGraph.Build(player);
                int start = graph.Nearest(player.position - Vector3.up * 1.05f);
                Debug.Log($"Ancient Mine walk graph ({id}): {graph.Feet.Count} walkable nodes");
                Assert.That(graph.Feet.Count, Is.GreaterThan(5000), "the graph covers the five areas");
                foreach (TreasureSpot s in AncientSpots())
                {
                    int stand = graph.StandNode(s.transform.position, 2.0f);
                    Assert.That(stand, Is.GreaterThanOrEqualTo(0), $"{id}: {s.SpotId}: a standing place within 2 m (the DIG radius is 2.5 m) exists");
                    Assert.That(graph.Connected(start, stand), Is.True, $"{id}: {s.SpotId} can be walked to from the start");
                }

                yield return Unload();
            }
        }

        [UnityTest]
        public IEnumerator Camera_StaysClearOfTheScenery_WhereverTheMinerDigs_AtEverySide()
        {
            CharacterSelection.Select(CharacterSelection.FemaleId);
            yield return Load(AncientPath);
            Transform player = RootObject("Player").transform;
            var cameraController = One<ThirdPersonCameraController>();
            var yawField = typeof(ThirdPersonCameraController).GetField("yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Camera camera = Camera.main;
            WalkGraph graph = WalkGraph.Build(player);
            var stats = new Stats();
            int samples = 0;
            string worst = "";
            foreach (TreasureSpot s in AncientSpots())
            {
                int stand = graph.StandNode(s.transform.position, 2.0f);
                Assert.That(stand, Is.GreaterThanOrEqualTo(0), s.SpotId);
                Teleport(player, graph.Feet[stand] + Vector3.up * 1.05f);
                for (int yaw = 0; yaw < 360; yaw += 90)
                {
                    yawField.SetValue(cameraController, (float)yaw);
                    for (int i = 0; i < 20; i++)
                    {
                        yield return null;
                    }

                    samples++;
                    float before = stats.MinCameraDistance;
                    Sample(player, camera, stats);
                    if (stats.MinCameraDistance < before)
                    {
                        worst = $"{s.SpotId} at yaw {yaw}";
                    }
                }
            }

            Debug.Log($"Ancient Mine dig positions: {samples} camera samples, closest {stats.MinCameraDistance:F2} m ({worst}), in a collider {stats.CameraInRock}, miner hidden {stats.CameraBlocked}");
            Assert.That(stats.CameraInRock, Is.EqualTo(0), "the camera is never inside rock, a pillar or a crystal where the miner digs: " + stats.FirstRock);
            Assert.That(stats.MinCameraDistance, Is.GreaterThan(1.15f), "never closer than the camera's own minimum distance (" + worst + ")");
            Assert.That(stats.CameraBlocked / (float)stats.Frames, Is.LessThan(0.06f), "the miner stays in view while digging");
        }

        // ================================================================ 3. the real flow, both miners, all 25, on foot

        private static readonly string[] TourOrder =
        {
            "gate_01", "gate_02", "gate_03", "hall_01", "hall_04", "hall_05", "hall_03", "hall_02",
            "workshop_01", "workshop_02", "workshop_03", "workshop_05", "workshop_04",
            "crystal_04", "crystal_03", "crystal_06", "crystal_02", "crystal_01", "crystal_05", "crystal_07",
            "vault_01", "vault_05", "vault_02", "vault_03", "vault_04",
        };

        [UnityTest]
        [RealisticFrameRate]
        [Timeout(1200000)]
        public IEnumerator Barnaby_WalksToAllTwentyFiveTreasures_DigRevealCollect_PayOnce_1240Gold_ReachesTheGoalAndKeepsCounting()
        {
            yield return Tour(CharacterSelection.MaleId);
        }

        [UnityTest]
        [RealisticFrameRate]
        [Timeout(1200000)]
        public IEnumerator Ruby_WalksToAllTwentyFiveTreasures_DigRevealCollect_PayOnce_1240Gold_ReachesTheGoalAndKeepsCounting()
        {
            yield return Tour(CharacterSelection.FemaleId);
        }

        private IEnumerator Tour(string miner)
        {
            var others = new List<(string id, int gold)>();
            yield return ReadOtherTwo(others);
            string save = FinishedTwoAreasSave(others.Select(g => g.id), others.Sum(g => g.gold));
            Assert.That(TourOrder.Length, Is.EqualTo(25));
            Assert.That(TourOrder.Distinct().Count(), Is.EqualTo(25));

            CharacterSelection.Select(miner);
            yield return Load(AncientPath, save);
            Transform player = RootObject("Player").transform;
            var controller = player.GetComponent<ThirdPersonPlayerController>();
            var hunt = One<TreasureHuntController>();
            var detector = One<TreasureDetector>();
            var hud = One<ItemCollector3D.UI.HudController>();
            Button dig = FindButton("DigButton"), collect = FindButton("CollectButton");
            Camera camera = Camera.main;
            ProgressManager manager = One<ProgressManager>();
            ProgressService service = manager.Service;
            var completedEvents = new List<string>();
            var unlockedEvents = new List<string>();
            manager.AreaCompleted += a => completedEvents.Add(a);
            manager.AreaUnlocked += a => unlockedEvents.Add(a);
            yield return new WaitForSecondsRealtime(0.6f);
            WalkGraph graph = WalkGraph.Build(player);
            Dictionary<string, TreasureSpot> byId = AncientSpots().ToDictionary(s => s.SpotId.Substring("ancient_mine.".Length));
            Assert.That(hunt.TotalGold, Is.EqualTo(1465), miner + ": starts from the 625 + 840 gold of the other two areas");
            Assert.That(hud.MaximumGold, Is.EqualTo(2500), miner + ": the HUD counts towards the final 2500 goal");
            Assert.That(hud.CurrentGold, Is.EqualTo(1465), miner + ": and shows the cumulative gold");
            Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.True);
            Assert.That(service.IsAreaCompleted("ancient_mine"), Is.False);

            var stats = new Stats();
            int gold = 1465;
            string crossedAt = null;
            int hiddenAtCrossing = -1;
            foreach (string key in TourOrder)
            {
                TreasureSpot spot = byId[key];
                int from = graph.Nearest(player.position - Vector3.up * 1.05f);
                int stand = graph.StandNode(spot.transform.position, 1.9f);
                Assert.That(stand, Is.GreaterThanOrEqualTo(0), miner + ": " + key);
                List<Vector3> path = graph.Path(from, stand);
                Assert.That(path, Is.Not.Null, $"{miner}: a way on foot to {key}");

                Time.timeScale = 2f;
                if (path.Count > 1)
                {
                    yield return WalkPath(player, controller, camera, path, key, stats);
                }

                Time.timeScale = 1f;
                yield return new WaitForSecondsRealtime(0.15f);

                // the detector senses it, DIG is offered
                yield return WaitUntil(() => detector.HasSignal, 1.5f);
                Assert.That(detector.HasSignal, Is.True, $"{miner}: {key}: the detector has a signal here");
                Assert.That(spot.HorizontalDistanceTo(player.position), Is.LessThanOrEqualTo(spot.Definition.DigRadius), $"{miner}: {key}: inside the dig radius");
                yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
                Assert.That(dig.gameObject.activeInHierarchy && dig.interactable, Is.True, $"{miner}: {key}: DIG is offered");
                Assert.That(spot.State, Is.EqualTo(TreasureState.Hidden));
                int before = hunt.TotalGold;
                dig.onClick.Invoke();
                float t0 = Time.realtimeSinceStartup;
                while (spot.State != TreasureState.Revealed && Time.realtimeSinceStartup - t0 < 3f)
                {
                    yield return null;
                    Sample(player, camera, stats);
                }

                Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), $"{miner}: {key}: the treasure is revealed by the dig");
                Assert.That(hunt.TotalGold, Is.EqualTo(before), "no gold before COLLECT");
                Assert.That(spot.VisualRoot.activeSelf, Is.True, $"{miner}: {key}: the treasure is visible");
                float revealEnd = Time.realtimeSinceStartup + 1.1f;
                while (Time.realtimeSinceStartup < revealEnd)
                {
                    yield return null;
                    Sample(player, camera, stats);
                }

                Vector3 shown = spot.VisualRoot.transform.position;
                Assert.That(shown.y, Is.GreaterThanOrEqualTo(spot.transform.position.y - 0.02f), $"{miner}: {key}: the revealed treasure rose out of the ground (it is not buried)");
                foreach (Collider c in Physics.OverlapSphere(shown + Vector3.up * 0.15f, 0.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Assert.That(c.gameObject.name == "Floor" || c.transform.root == player, Is.True, $"{miner}: {key}: the revealed treasure is inside {c.name}");
                }

                yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
                Assert.That(collect.gameObject.activeInHierarchy && collect.interactable, Is.True, $"{miner}: {key}: COLLECT is offered");
                collect.onClick.Invoke();
                yield return null;
                Assert.That(spot.State, Is.EqualTo(TreasureState.Collected), $"{miner}: {key}: collected");
                gold += spot.Definition.GoldValue;
                Assert.That(hunt.TotalGold, Is.EqualTo(gold), $"{miner}: {key}: the exact gold of its rarity was paid");
                Assert.That(hud.CurrentGold, Is.EqualTo(Mathf.Min(gold, 2500)), $"{miner}: {key}: the HUD bar follows the cumulative gold (it only STOPS FILLING at its 2500 goal)");

                // a second press pays nothing
                collect.onClick.Invoke();
                dig.onClick.Invoke();
                yield return null;
                Assert.That(hunt.TotalGold, Is.EqualTo(gold), $"{miner}: {key}: nothing is paid twice");
                Assert.That(service.RegisterCollected(spot.SpotId, spot.Definition.GoldValue), Is.EqualTo(RegisterResult.AlreadyCollected));

                // the collect animation plays out and leaves nothing behind
                yield return WaitUntil(() => !spot.VisualRoot.activeSelf, 3.5f);
                Assert.That(spot.VisualRoot.activeSelf, Is.False, $"{miner}: {key}: no treasure model is left behind");
                yield return WaitUntil(() => VisibleLeftovers(spot) == 0, 4f);
                Assert.That(VisibleLeftovers(spot), Is.EqualTo(0), $"{miner}: {key}: nothing visible is left on the spot (an Epic's sparkle emitter stays, but with no particles)");

                if (crossedAt == null && gold >= 2500)
                {
                    crossedAt = key;
                    hiddenAtCrossing = AncientSpots().Count(s => s.State == TreasureState.Hidden);
                    Assert.That(service.IsAreaCompleted("ancient_mine"), Is.True, $"{miner}: the 2500 goal is reached at {gold} gold");
                    Assert.That(completedEvents.Count(a => a == "ancient_mine"), Is.EqualTo(1), "announced once");
                    Assert.That(hiddenAtCrossing, Is.GreaterThanOrEqualTo(1), "reaching 2500 does NOT mean everything was collected: treasures are still waiting");
                }
                else if (crossedAt == null)
                {
                    Assert.That(service.IsAreaCompleted("ancient_mine"), Is.False, $"{miner}: not complete at {gold} gold");
                }
                else
                {
                    Assert.That(service.IsAreaCompleted("ancient_mine"), Is.True, "still complete, and the gold keeps counting past 2500");
                }
            }

            Time.timeScale = 1f;
            Assert.That(gold, Is.EqualTo(1465 + 1240));
            Assert.That(hunt.TotalGold, Is.EqualTo(2705), miner + ": 625 + 840 + 1240, nothing reset or lost at 2500");
            Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(60), miner + ": 15 + 20 + 25 ids");
            Assert.That(AncientSpots().All(s => s.State == TreasureState.Collected), Is.True);
            Assert.That(hud.MaximumGold, Is.EqualTo(2500));
            Assert.That(hud.CurrentGold, Is.EqualTo(2500), "the bar is full at the goal, the saved total is 2705");
            Assert.That(JsonUtility.FromJson<GameProgressData>(store.Json).totalGold, Is.EqualTo(2705), "and 2705 is what is saved");
            Assert.That(completedEvents.Count(a => a == "ancient_mine"), Is.EqualTo(1), "AREA COMPLETE for the last map was raised exactly once");
            Assert.That(unlockedEvents, Is.Empty, "no new area unlocks after Ancient Mine");
            Assert.That(stats.MaxRun, Is.LessThanOrEqualTo(4), miner + ": the camera never stays inside scenery on the whole tour (" + stats.CameraInRock + " frames, longest run " + stats.MaxRun + "): " + stats.FirstRock);
            Assert.That(stats.CameraInRock / (float)stats.Frames, Is.LessThan(0.005f), miner + ": and touches it in less than 0.5 % of the frames: " + stats.FirstRock);
            Assert.That(stats.CameraBlocked / (float)stats.Frames, Is.LessThan(0.04f), miner + ": the miner stayed in view");
            Debug.Log($"Ancient Mine tour ({miner}): 25 treasures, {stats.Frames} frames, 2500 reached at {crossedAt} with {hiddenAtCrossing} still hidden, camera closest {stats.MinCameraDistance:F2} m, camera touched scenery in {stats.CameraInRock} frames (longest run {stats.MaxRun}), miner hidden {100f * stats.CameraBlocked / stats.Frames:F1} %");
            yield return Unload();
        }

        // ================================================================ 4. saving

        /// <summary>Digs and collects a treasure for real, teleporting next to it first (the walk itself is covered by the tours).</summary>
        private IEnumerator CollectNow(WalkGraph graph, Transform player, string key, List<int> goldLog)
        {
            TreasureSpot spot = AncientSpots().First(s => s.SpotId == "ancient_mine." + key);
            Button dig = FindButton("DigButton"), collect = FindButton("CollectButton");
            var hunt = One<TreasureHuntController>();
            Teleport(player, graph.Feet[graph.StandNode(spot.transform.position, 1.9f)] + Vector3.up * 1.05f);
            yield return WaitUntil(() => dig.gameObject.activeInHierarchy && dig.interactable, 2f);
            dig.onClick.Invoke();
            yield return WaitUntil(() => spot.State == TreasureState.Revealed, 3f);
            Assert.That(spot.State, Is.EqualTo(TreasureState.Revealed), key);
            yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
            collect.onClick.Invoke();
            yield return null;
            Assert.That(spot.State, Is.EqualTo(TreasureState.Collected), key);
            goldLog.Add(hunt.TotalGold);
        }

        [UnityTest]
        public IEnumerator Progress_IsKept_OverSceneChanges_AndAnAppRestart_AndNeverPaysTwice()
        {
            var others = new List<(string id, int gold)>();
            yield return ReadOtherTwo(others);
            string save = FinishedTwoAreasSave(others.Select(g => g.id), others.Sum(g => g.gold));
            string[] picked = { "gate_01", "hall_04", "vault_01" };

            yield return Load(AncientPath, save);
            MemoryProgressStore playSession = store;
            Transform player = RootObject("Player").transform;
            WalkGraph graph = WalkGraph.Build(player);
            var log = new List<int>();
            foreach (string key in picked)
            {
                yield return CollectNow(graph, player, key, log);
            }

            Assert.That(log, Is.EqualTo(new[] { 1475, 1525, 1625 }), "10 + 50 + 100 gold on top of the 1465 of the other two areas");
            GameProgressData saved = JsonUtility.FromJson<GameProgressData>(playSession.Json);
            Assert.That(saved.totalGold, Is.EqualTo(1625), "saved at once");
            foreach (string key in picked)
            {
                Assert.That(saved.collectedTreasureIds, Contains.Item("ancient_mine." + key));
            }

            Assert.That(saved.collectedTreasureIds.Count, Is.EqualTo(38));
            yield return Unload();

            // 1. over a scene change: the other two areas see the same total and keep every treasure collected, then Ancient Mine comes back with the three collected
            yield return Load(GreenMinePath, null, playSession);
            Assert.That(One<TreasureHuntController>().TotalGold, Is.EqualTo(1625), "Green Mine shows the cumulative total");
            Assert.That(TreasureSpot.Active.Count(s => s.State == TreasureState.Collected), Is.EqualTo(15), "and all its own treasures are still collected");
            yield return Unload();
            yield return Load(RockyPath, null, playSession);
            Assert.That(One<TreasureHuntController>().TotalGold, Is.EqualTo(1625), "Rocky Canyon shows the cumulative total");
            Assert.That(TreasureSpot.Active.Count(s => s.State == TreasureState.Collected), Is.EqualTo(20), "and all its own treasures are still collected");
            yield return Unload();

            yield return CheckAncientRestored(playSession, picked, 1625);

            // 2. over an app restart: a brand-new service and a brand-new scene load from the saved text only
            var restarted = new MemoryProgressStore(playSession.Json);
            ProgressService fresh = ProgressService.Create(One<ProgressManager>().Service.Catalog, restarted);
            Assert.That(fresh.TotalGold, Is.EqualTo(1625));
            Assert.That(fresh.CollectedTreasureIds.Count, Is.EqualTo(38));
            Assert.That(fresh.IsAreaUnlocked("ancient_mine"), Is.True);
            Assert.That(fresh.IsAreaCompleted("ancient_mine"), Is.False);
            yield return CheckAncientRestored(restarted, picked, 1625);

            // 3. and a treasure collected before is never paid again, however it is asked
            ProgressService again = ProgressService.Create(One<ProgressManager>().Service.Catalog, restarted);
            Assert.That(again.RegisterCollected("ancient_mine.gate_01", 10), Is.EqualTo(RegisterResult.AlreadyCollected));
            Assert.That(again.TotalGold, Is.EqualTo(1625));
        }

        private IEnumerator CheckAncientRestored(MemoryProgressStore from, string[] collectedKeys, int expectedGold)
        {
            yield return Unload();
            yield return Load(AncientPath, null, from);
            var hunt = One<TreasureHuntController>();
            Assert.That(hunt.TotalGold, Is.EqualTo(expectedGold));
            foreach (TreasureSpot s in AncientSpots())
            {
                bool should = collectedKeys.Any(k => s.SpotId == "ancient_mine." + k);
                Assert.That(s.State, Is.EqualTo(should ? TreasureState.Collected : TreasureState.Hidden), s.SpotId + (should ? " stays collected" : " is still hidden"));
                if (should)
                {
                    Assert.That(s.VisualRoot.activeSelf, Is.False, s.SpotId + ": no model of a collected treasure");
                }
            }

            // standing right at a collected treasure offers nothing and pays nothing
            Transform player = RootObject("Player").transform;
            TreasureSpot collected = AncientSpots().First(s => s.SpotId == "ancient_mine." + collectedKeys[0]);
            Teleport(player, collected.transform.position + new Vector3(1.2f, 1.1f, 0f));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(FindButton("DigButton").gameObject.activeInHierarchy, Is.False, "no DIG for a collected treasure");
            FindButton("DigButton").onClick.Invoke();
            FindButton("CollectButton").onClick.Invoke();
            yield return null;
            Assert.That(hunt.TotalGold, Is.EqualTo(expectedGold), "no gold for a treasure that was collected in an earlier session");
        }

        [UnityTest]
        public IEnumerator NewGame_ResetsAllThreeAreas_ButKeepsTheMinerAndTheSettings()
        {
            var others = new List<(string id, int gold)>();
            yield return ReadOtherTwo(others);
            CharacterSelection.Select(CharacterSelection.FemaleId);
            GameSettings.Current.SetMusicVolume(0.3f);
            GameSettings.Current.SetSfxVolume(0.6f);
            yield return Load(AncientPath, FinishedTwoAreasSave(others.Select(g => g.id), others.Sum(g => g.gold)));
            MemoryProgressStore session = store;
            Transform player = RootObject("Player").transform;
            WalkGraph graph = WalkGraph.Build(player);
            var log = new List<int>();
            yield return CollectNow(graph, player, "gate_02", log);
            yield return CollectNow(graph, player, "vault_04", log);
            Assert.That(log, Is.EqualTo(new[] { 1475, 1575 }));

            // exactly what the Settings panel's NEW GAME does: StartNewGame on the progress service
            ProgressService service = One<ProgressManager>().Service;
            Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(37), "15 + 20 + 2 ids before the reset");
            Assert.That(service.StartNewGame(), Is.True);
            Assert.That(service.TotalGold, Is.EqualTo(0));
            Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(0), "the ids of ALL THREE areas are gone");
            Assert.That(service.UnlockedAreaIds, Is.EquivalentTo(new[] { "green_mine" }), "only Green Mine is open again");
            Assert.That(service.IsAreaCompleted("green_mine") || service.IsAreaCompleted("rocky_canyon") || service.IsAreaCompleted("ancient_mine"), Is.False, "nothing is complete");
            GameProgressData saved = JsonUtility.FromJson<GameProgressData>(session.Json);
            Assert.That(saved.totalGold, Is.EqualTo(0));
            Assert.That(saved.collectedTreasureIds.Count, Is.EqualTo(0), "and the reset was written");
            yield return Unload();

            // every treasure of all three areas is hidden again after the reset
            yield return Load(AncientPath, null, session);
            Assert.That(AncientSpots().All(s => s.State == TreasureState.Hidden), Is.True, "all 25 Ancient Mine treasures are back");
            Assert.That(One<TreasureHuntController>().TotalGold, Is.EqualTo(0));
            yield return Unload();
            yield return Load(RockyPath, null, session);
            Assert.That(TreasureSpot.Active.Count, Is.EqualTo(20));
            Assert.That(TreasureSpot.Active.All(s => s.State == TreasureState.Hidden), Is.True, "and all 20 Rocky Canyon treasures");
            yield return Unload();
            yield return Load(GreenMinePath, null, session);
            Assert.That(TreasureSpot.Active.Count, Is.EqualTo(15));
            Assert.That(TreasureSpot.Active.All(s => s.State == TreasureState.Hidden), Is.True, "and all 15 Green Mine treasures");

            // the miner and the sound / camera settings are not progress: they stay
            Assert.That(CharacterSelection.SelectedId, Is.EqualTo(CharacterSelection.FemaleId), "Ruby is still the chosen miner");
            Assert.That(GameSettings.Current.MusicVolume, Is.EqualTo(0.3f).Within(1e-4f));
            Assert.That(GameSettings.Current.SfxVolume, Is.EqualTo(0.6f).Within(1e-4f));
        }

        // ================================================================ 5. thresholds (isolated: the service only)

        [UnityTest]
        public IEnumerator Thresholds_500_1200_2500_AreKept_TheTotalIs2705_AndGoldKeepsCountingPastTheGoal()
        {
            var green = new List<(string id, int gold)>();
            var rocky = new List<(string id, int gold)>();
            yield return ReadScene(GreenMinePath, green);
            yield return ReadScene(RockyPath, rocky);
            yield return Load(AncientPath);
            List<(string id, int gold)> ancient = AncientSpots().Select(s => (s.SpotId, s.Definition.GoldValue)).ToList();
            AreaCatalog catalog = One<ProgressManager>().Service.Catalog;
            Assert.That(catalog.Get("green_mine").goalTotalGold, Is.EqualTo(500));
            Assert.That(catalog.Get("rocky_canyon").unlockTotalGold, Is.EqualTo(500));
            Assert.That(catalog.Get("rocky_canyon").goalTotalGold, Is.EqualTo(1200));
            Assert.That(catalog.Get("ancient_mine").unlockTotalGold, Is.EqualTo(1200), "the 1200 unlock threshold is unchanged");
            Assert.That(catalog.Get("ancient_mine").goalTotalGold, Is.EqualTo(2500), "the final goal is 2500 cumulative gold");
            Assert.That(green.Sum(g => g.gold), Is.EqualTo(625));
            Assert.That(rocky.Sum(g => g.gold), Is.EqualTo(840));
            Assert.That(ancient.Sum(g => g.gold), Is.EqualTo(1240));
            Assert.That(green.Sum(g => g.gold) + rocky.Sum(g => g.gold) + ancient.Sum(g => g.gold), Is.EqualTo(2705), "2705 gold in the whole game");
            Assert.That(2705, Is.GreaterThan(2500), "the goal can be reached without collecting everything");

            var memory = new MemoryProgressStore();
            ProgressService service = ProgressService.Create(catalog, memory);
            var unlocks = new List<string>();
            var completes = new List<string>();
            service.AreaUnlocked += unlocks.Add;
            service.AreaCompleted += completes.Add;

            int rockyCrossing = -1;
            foreach ((string id, int gold) in green)
            {
                int before = service.TotalGold;
                Assert.That(service.RegisterCollected(id, gold), Is.EqualTo(RegisterResult.Accepted));
                Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.EqualTo(service.TotalGold >= 500), $"Rocky Canyon at {service.TotalGold} gold");
                if (rockyCrossing < 0 && service.TotalGold >= 500)
                {
                    rockyCrossing = service.TotalGold;
                    Assert.That(before, Is.LessThan(500), "it opened on the registration that crossed 500");
                }
            }

            Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.False);
            int ancientCrossing = -1, finalCrossing = -1, idsAtFinal = 0;
            foreach ((string id, int gold) in rocky)
            {
                int before = service.TotalGold;
                Assert.That(service.RegisterCollected(id, gold), Is.EqualTo(RegisterResult.Accepted), id);
                bool open = service.IsAreaUnlocked("ancient_mine");
                Assert.That(open, Is.EqualTo(service.TotalGold >= 1200), $"Ancient Mine at {service.TotalGold} gold");
                if (open && ancientCrossing < 0)
                {
                    ancientCrossing = service.TotalGold;
                    Assert.That(before, Is.LessThan(1200), "it opened on the registration that crossed 1200, not before");
                }
            }

            Assert.That(service.TotalGold, Is.EqualTo(1465));
            Assert.That(service.IsAreaCompleted("rocky_canyon"), Is.True);
            Assert.That(service.IsAreaCompleted("ancient_mine"), Is.False);
            int registered = 0;
            foreach ((string id, int gold) in ancient)
            {
                int before = service.TotalGold;
                Assert.That(service.RegisterCollected(id, gold), Is.EqualTo(RegisterResult.Accepted), id);
                registered++;
                Assert.That(service.TotalGold, Is.EqualTo(before + gold), "Ancient Mine gold is added to the cumulative total");
                Assert.That(service.IsAreaCompleted("ancient_mine"), Is.EqualTo(service.TotalGold >= 2500), $"the final goal at {service.TotalGold} gold");
                if (service.IsAreaCompleted("ancient_mine") && finalCrossing < 0)
                {
                    finalCrossing = service.TotalGold;
                    idsAtFinal = registered;
                    Assert.That(before, Is.LessThan(2500), "it completed on the registration that crossed 2500");
                }
            }

            Assert.That(finalCrossing, Is.GreaterThanOrEqualTo(2500));
            Assert.That(idsAtFinal, Is.LessThan(25), "2500 was reached while treasures were still uncollected: the area does not assume they are all found");
            Assert.That(service.TotalGold, Is.EqualTo(2705), "nothing is reset or capped at 2500: the whole 2705 counts");
            Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(60));
            Assert.That(unlocks, Is.EqualTo(new[] { "rocky_canyon", "ancient_mine" }), "each unlock was announced exactly once, in order");
            Assert.That(completes, Is.EqualTo(new[] { "green_mine", "rocky_canyon", "ancient_mine" }), "each goal was announced exactly once, in order");
            Assert.That(JsonUtility.FromJson<GameProgressData>(memory.Json).totalGold, Is.EqualTo(2705), "saved");
            foreach ((string id, int gold) in ancient)
            {
                Assert.That(service.RegisterCollected(id, gold), Is.EqualTo(RegisterResult.AlreadyCollected), id + " pays only once");
            }

            Assert.That(service.TotalGold, Is.EqualTo(2705), "and the total did not move");

            // a save written before this stage (Green Mine + Rocky Canyon only, the old format) still loads, with Ancient Mine open and unfinished
            var legacy = ProgressService.Create(catalog, new MemoryProgressStore("{\"version\":1,\"totalGold\":1465,\"collectedTreasureIds\":[\"" + green[0].id + "\",\"" + rocky[0].id + "\"],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\"],\"completedAreaIds\":[\"green_mine\",\"rocky_canyon\"]}"));
            Assert.That(legacy.TotalGold, Is.EqualTo(1465));
            Assert.That(legacy.IsAreaUnlocked("ancient_mine"), Is.True);
            Assert.That(legacy.IsAreaCompleted("ancient_mine"), Is.False);

            // the exact edges, on the service alone (this does NOT stand for the DIG / COLLECT flow, which the tours above walk for real)
            foreach ((int gold, bool rockyOpen, bool ancientOpen, bool ancientDone) in new[] { (499, false, false, false), (500, true, false, false), (1199, true, false, false), (1200, true, true, false), (2499, true, true, false), (2500, true, true, true) })
            {
                var edge = ProgressService.Create(catalog, new MemoryProgressStore());
                edge.RegisterCollected("edge.test", gold);
                Assert.That((edge.IsAreaUnlocked("rocky_canyon"), edge.IsAreaUnlocked("ancient_mine"), edge.IsAreaCompleted("ancient_mine")), Is.EqualTo((rockyOpen, ancientOpen, ancientDone)), gold + " gold");
            }
        }
    }
}
#endif
