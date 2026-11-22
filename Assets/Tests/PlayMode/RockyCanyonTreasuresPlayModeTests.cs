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
    /// Rocky Canyon treasures and progression (stage 15C): exactly 20 permanent treasures worth 840 gold in the agreed split, unique ids that never clash with Green Mine's, placed on real,
    /// reachable, clear ground (checked with a physics walk graph made of the miner's own capsule), the real DIG / Reveal / COLLECT flow of every treasure with both miners, gold paid once,
    /// progress kept over scene changes and an app restart, NEW GAME resetting both areas (and only progress), and the 1200-gold Ancient Mine unlock. Only in-memory stores are used; the
    /// developer's real save file is hashed before the first and after the last test.
    /// </summary>
    public sealed class RockyCanyonTreasuresPlayModeTests
    {
        private const string RockyPath = "Assets/Scenes/RockyCanyon.unity";
        private const string GreenMinePath = "Assets/Scenes/GreenMine.unity";
        private static readonly string[] Miners = { CharacterSelection.MaleId, CharacterSelection.FemaleId };

        // zone group -> expected (common, uncommon, rare, epic, gold)
        private static readonly (string group, string prefix, int c, int u, int r, int e, int gold)[] Split =
        {
            ("A_DesertOutpost", "outpost", 2, 1, 0, 0, 45),
            ("B_DryRiverbed", "riverbed", 1, 2, 2, 0, 160),
            ("C_OldRailway", "railway", 1, 1, 2, 1, 235),
            ("D_CanyonOverlook", "overlook", 0, 1, 1, 1, 175),
            ("E_HiddenRavine", "ravine", 0, 1, 2, 1, 225),
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

        private List<TreasureSpot> RockySpots() => RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true).OrderBy(s => s.SpotId, System.StringComparer.Ordinal).ToList();

        private Collider TerrainCollider => RootObject("RockyCanyonEnvironment").transform.Find("Terrain").GetComponent<Collider>();

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

        private static string FinishedGreenMineSave(IEnumerable<string> greenIds, int gold)
        {
            string ids = string.Join(",", greenIds.Select(i => "\"" + i + "\""));
            return "{\"version\":1,\"totalGold\":" + gold + ",\"collectedTreasureIds\":[" + ids + "],\"unlockedAreaIds\":[\"green_mine\",\"rocky_canyon\"],\"completedAreaIds\":[\"green_mine\"]}";
        }

        /// <summary>Loads Green Mine once (empty save) to read its 15 treasure ids and their gold, and unloads it again.</summary>
        private IEnumerator ReadGreenMine(List<(string id, int gold)> result)
        {
            yield return Load(GreenMinePath);
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
            Collider[] inside = Physics.OverlapSphere(camera.transform.position, 0.15f, ~0, QueryTriggerInteraction.Ignore);
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

        // ================================================================ 1. the table

        [UnityTest]
        public IEnumerator ExactlyTwentyTreasures_840Gold_InTheAgreedRarityAndZoneSplit()
        {
            yield return Load(RockyPath);
            List<TreasureSpot> all = RockySpots();
            Assert.That(all.Count, Is.EqualTo(20), "exactly 20 Rocky Canyon treasures");
            Assert.That(TreasureSpot.Active.Count, Is.EqualTo(20), "and all of them are in play");
            Assert.That(all.Sum(s => s.Definition.GoldValue), Is.EqualTo(840), "840 gold in total");

            int c = 0, u = 0, r = 0, e = 0;
            Transform root = RootObject("TreasureSpots").transform;
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
                    Assert.That(s.SpotId, Does.StartWith("rocky_canyon." + prefix + "_"), group + ": ids carry their zone");
                }

                c += ec;
                u += eu;
                r += er;
                e += ee;
            }

            Assert.That((c, u, r, e), Is.EqualTo((4, 6, 7, 3)), "4 Common, 6 Uncommon, 7 Rare, 3 Epic");

            // the existing reward system is used unchanged: the four rarities pay 10 / 25 / 50 / 100
            foreach (TreasureSpot s in all)
            {
                int expected = s.Definition.Rarity switch { TreasureRarity.Common => 10, TreasureRarity.Uncommon => 25, TreasureRarity.Rare => 50, _ => 100 };
                Assert.That(s.Definition.GoldValue, Is.EqualTo(expected), s.SpotId);
            }
        }

        [UnityTest]
        public IEnumerator Ids_AreUniqueExplicitAndPermanent_AndNeverClashWithGreenMine()
        {
            yield return Load(RockyPath);
            List<TreasureSpot> all = RockySpots();
            var pattern = new Regex(@"^rocky_canyon\.(outpost|riverbed|railway|overlook|ravine)_\d\d$");
            foreach (TreasureSpot s in all)
            {
                Assert.That(s.HasExplicitId, Is.True, s.name + " has an explicit id (not a position-based fallback)");
                Assert.That(pattern.IsMatch(s.SpotId), Is.True, s.SpotId);
            }

            Assert.That(all.Select(s => s.SpotId).Distinct().Count(), Is.EqualTo(20), "all 20 ids are different");
            Assert.That(TreasureIdValidator.Validate(TreasureSpot.Active).IsValid, Is.True);
            var rockyIds = new HashSet<string>(all.Select(s => s.SpotId));

            var green = new List<(string id, int gold)>();
            yield return Unload();
            yield return ReadGreenMine(green);
            Assert.That(green.Count, Is.EqualTo(15), "Green Mine keeps its 15 treasures");
            Assert.That(green.Sum(g => g.gold), Is.EqualTo(625), "and its 625 gold");
            Assert.That(green.All(g => g.id.StartsWith("green_mine.")), Is.True);
            Assert.That(rockyIds.Overlaps(green.Select(g => g.id)), Is.False, "no id is shared between the two areas");
            Assert.That(green.Sum(g => g.gold) + 840, Is.EqualTo(1465), "the two areas together pay 1465 gold");
        }

        // ================================================================ 2. placement: real ground, clear, reachable

        [UnityTest]
        public IEnumerator EveryTreasure_StandsOnRealGround_NotBuriedNotUnderDecorNotAtACliff()
        {
            yield return Load(RockyPath);
            Collider terrain = TerrainCollider;
            Transform env = RootObject("RockyCanyonEnvironment").transform;
            var decor = new List<Vector3>();
            foreach (MeshFilter f in env.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.name == "Terrain" || f.transform.parent.name == "Backdrop")
                {
                    continue;
                }

                foreach (Vector3 v in f.sharedMesh.vertices)
                {
                    decor.Add(f.transform.TransformPoint(v));
                }
            }

            foreach (TreasureSpot s in RockySpots())
            {
                Vector3 p = s.transform.position;
                Assert.That(terrain.Raycast(new Ray(p + Vector3.up * 60f, Vector3.down), out RaycastHit hit, 120f), Is.True, s.SpotId + ": ground under it");
                Assert.That(Mathf.Abs(hit.point.y - p.y), Is.LessThan(0.1f), s.SpotId + ": sits ON the ground (not buried, not floating)");
                Assert.That(hit.normal.y, Is.GreaterThan(0.9f), s.SpotId + ": on gentle ground (slope " + Mathf.Acos(hit.normal.y) * Mathf.Rad2Deg + " deg)");
                foreach (Collider c in Physics.OverlapSphere(p + Vector3.up * 0.6f, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    Assert.That(c.gameObject.name, Is.EqualTo("Terrain"), $"{s.SpotId}: {c.name} is within 1.2 m (a rock, wall, wagon or rail must not hold the treasure or the miner)");
                }

                Assert.That(Physics.Raycast(p + Vector3.up * 0.5f, Vector3.up, 8f, ~0, QueryTriggerInteraction.Ignore), Is.False, s.SpotId + ": nothing overhead (not under the bridge, a roof or an overhang)");

                float nearest = float.MaxValue;
                foreach (Vector3 v in decor)
                {
                    if (v.y >= p.y - 0.3f && v.y <= p.y + 2f)
                    {
                        nearest = Mathf.Min(nearest, new Vector2(v.x - p.x, v.z - p.z).magnitude);
                    }
                }

                Assert.That(nearest, Is.GreaterThan(0.7f), s.SpotId + ": no decoration pokes into the treasure (nearest vertex " + nearest + " m)");

                // not on a cliff edge: ground all round at 1.5 m, at about the same height
                for (int k = 0; k < 8; k++)
                {
                    Vector3 q = p + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * 1.5f;
                    Assert.That(terrain.Raycast(new Ray(q + Vector3.up * 60f, Vector3.down), out RaycastHit around, 120f), Is.True);
                    Assert.That(Mathf.Abs(around.point.y - p.y), Is.LessThan(0.8f), s.SpotId + ": level ground round it, not a cliff edge");
                }
            }
        }

        [UnityTest]
        public IEnumerator EveryTreasure_CanBeReachedOnFoot_AndTheMinerCanStandInDigRange()
        {
            foreach (string id in Miners)
            {
                CharacterSelection.Select(id);
                yield return Load(RockyPath);
                Transform player = RootObject("Player").transform;
                WalkGraph graph = WalkGraph.Build(player);
                int start = graph.Nearest(player.position - Vector3.up * 1.05f);
                Debug.Log($"Rocky Canyon walk graph ({id}): {graph.Feet.Count} walkable nodes");
                Assert.That(graph.Feet.Count, Is.GreaterThan(3000), "the graph covers the five areas");
                foreach (TreasureSpot s in RockySpots())
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
            yield return Load(RockyPath);
            Transform player = RootObject("Player").transform;
            var cameraController = One<ThirdPersonCameraController>();
            var yawField = typeof(ThirdPersonCameraController).GetField("yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Camera camera = Camera.main;
            WalkGraph graph = WalkGraph.Build(player);
            var stats = new Stats();
            int samples = 0;
            string worst = "";
            foreach (TreasureSpot s in RockySpots())
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

            Debug.Log($"Rocky Canyon dig positions: {samples} camera samples, closest {stats.MinCameraDistance:F2} m ({worst}), in a collider {stats.CameraInRock}, miner hidden {stats.CameraBlocked}");
            Assert.That(stats.CameraInRock, Is.EqualTo(0), "the camera is never inside rock or timber where the miner digs: " + stats.FirstRock);
            Assert.That(stats.MinCameraDistance, Is.GreaterThan(1.15f), "never closer than the camera's own minimum distance (" + worst + ")");
            Assert.That(stats.CameraBlocked / (float)stats.Frames, Is.LessThan(0.06f), "the miner stays in view while digging");
        }

        // ================================================================ 3. the real flow, both miners, all 20, on foot

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator BothMiners_WalkToAllTwentyTreasures_DigRevealCollect_PayOnce_840Gold_AndAncientMineOpensAt1200()
        {
            var green = new List<(string id, int gold)>();
            yield return ReadGreenMine(green);
            string save = FinishedGreenMineSave(green.Select(g => g.id), green.Sum(g => g.gold));
            string[] order =
            {
                "outpost_01", "outpost_02", "outpost_03", "riverbed_01", "riverbed_02", "riverbed_04", "riverbed_03", "riverbed_05",
                "overlook_01", "overlook_02", "overlook_03", "railway_02", "railway_04", "railway_01", "railway_03", "railway_05",
                "ravine_01", "ravine_02", "ravine_03", "ravine_04",
            };

            foreach (string miner in Miners)
            {
                CharacterSelection.Select(miner);
                yield return Load(RockyPath, save);
                Transform player = RootObject("Player").transform;
                var controller = player.GetComponent<ThirdPersonPlayerController>();
                var hunt = One<TreasureHuntController>();
                var detector = One<TreasureDetector>();
                Button dig = FindButton("DigButton"), collect = FindButton("CollectButton");
                Camera camera = Camera.main;
                ProgressService service = One<ProgressManager>().Service;
                var unlockedEvents = new List<string>();
                One<ProgressManager>().AreaUnlocked += a => unlockedEvents.Add(a);
                yield return new WaitForSecondsRealtime(0.6f);
                WalkGraph graph = WalkGraph.Build(player);
                Dictionary<string, TreasureSpot> byId = RockySpots().ToDictionary(s => s.SpotId.Substring("rocky_canyon.".Length));
                Assert.That(hunt.TotalGold, Is.EqualTo(625), miner + ": starts from Green Mine's 625 gold");
                Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.False, miner + ": Ancient Mine is still locked");

                var stats = new Stats();
                int gold = 625;
                string crossedAt = null;
                foreach (string key in order)
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
                        Assert.That(c.gameObject.name == "Terrain" || c.transform.root == player, Is.True, $"{miner}: {key}: the revealed treasure is inside {c.name}");
                    }

                    yield return WaitUntil(() => collect.gameObject.activeInHierarchy && collect.interactable, 2f);
                    Assert.That(collect.gameObject.activeInHierarchy && collect.interactable, Is.True, $"{miner}: {key}: COLLECT is offered");
                    collect.onClick.Invoke();
                    yield return null;
                    Assert.That(spot.State, Is.EqualTo(TreasureState.Collected), $"{miner}: {key}: collected");
                    gold += spot.Definition.GoldValue;
                    Assert.That(hunt.TotalGold, Is.EqualTo(gold), $"{miner}: {key}: the exact gold of its rarity was paid");

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

                    if (crossedAt == null && gold >= 1200)
                    {
                        crossedAt = key;
                        Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.True, $"{miner}: Ancient Mine unlocks at {gold} gold");
                        Assert.That(unlockedEvents, Contains.Item("ancient_mine"));
                    }
                    else if (crossedAt == null)
                    {
                        Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.False, $"{miner}: still locked at {gold} gold");
                    }
                }

                Time.timeScale = 1f;
                Assert.That(gold, Is.EqualTo(625 + 840));
                Assert.That(hunt.TotalGold, Is.EqualTo(1465), miner + ": 625 + 840");
                Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(35), miner + ": 15 Green Mine + 20 Rocky Canyon ids");
                Assert.That(AreaSelectController.Evaluate(service, "ancient_mine", "").Availability, Is.EqualTo(AreaAvailability.ComingSoon), "Ancient Mine is open but still COMING SOON");
                Assert.That(unlockedEvents.Count(a => a == "ancient_mine"), Is.EqualTo(1), "the unlock was announced once");
                // the camera system itself may touch a wall for a frame or two when the miner turns fast in the narrow gorge / ravine: never for long, never for more than a hair of the tour
                Assert.That(stats.MaxRun, Is.LessThanOrEqualTo(4), miner + ": the camera never stays inside scenery on the whole tour (" + stats.CameraInRock + " frames, longest run " + stats.MaxRun + "): " + stats.FirstRock);
                Assert.That(stats.CameraInRock / (float)stats.Frames, Is.LessThan(0.005f), miner + ": and touches it in less than 0.5 % of the frames: " + stats.FirstRock);
                Assert.That(stats.CameraBlocked / (float)stats.Frames, Is.LessThan(0.04f), miner + ": the miner stayed in view");
                Debug.Log($"Rocky Canyon tour ({miner}): 20 treasures, {stats.Frames} frames, Ancient Mine unlocked at {crossedAt}, camera closest {stats.MinCameraDistance:F2} m, camera touched scenery in {stats.CameraInRock} frames (longest run {stats.MaxRun}), miner hidden {100f * stats.CameraBlocked / stats.Frames:F1} %");
                yield return Unload();
            }
        }

        // ================================================================ 4. saving

        /// <summary>Digs and collects a treasure for real, teleporting next to it first (the walk itself is covered by the tour).</summary>
        private IEnumerator CollectNow(WalkGraph graph, Transform player, string key, List<int> goldLog)
        {
            TreasureSpot spot = RockySpots().First(s => s.SpotId == "rocky_canyon." + key);
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
            var green = new List<(string id, int gold)>();
            yield return ReadGreenMine(green);
            string save = FinishedGreenMineSave(green.Select(g => g.id), green.Sum(g => g.gold));
            string[] picked = { "outpost_01", "railway_03", "overlook_03" };

            yield return Load(RockyPath, save);
            MemoryProgressStore playSession = store;
            Transform player = RootObject("Player").transform;
            WalkGraph graph = WalkGraph.Build(player);
            var log = new List<int>();
            foreach (string key in picked)
            {
                yield return CollectNow(graph, player, key, log);
            }

            Assert.That(log, Is.EqualTo(new[] { 635, 735, 835 }), "10 + 100 + 100 gold on top of Green Mine's 625");
            GameProgressData saved = JsonUtility.FromJson<GameProgressData>(playSession.Json);
            Assert.That(saved.totalGold, Is.EqualTo(835), "saved at once");
            foreach (string key in picked)
            {
                Assert.That(saved.collectedTreasureIds, Contains.Item("rocky_canyon." + key));
            }

            Assert.That(saved.collectedTreasureIds.Count, Is.EqualTo(18));
            yield return Unload();

            // 1. over a scene change: Green Mine sees the same total, then Rocky Canyon comes back with the three collected
            yield return Load(GreenMinePath, null, playSession);
            Assert.That(One<TreasureHuntController>().TotalGold, Is.EqualTo(835), "Green Mine shows the cumulative total");
            Assert.That(TreasureSpot.Active.Count(s => s.State == TreasureState.Collected), Is.EqualTo(15), "and all its own treasures are still collected");
            yield return Unload();

            yield return CheckRockyRestored(playSession, picked, 835);

            // 2. over an app restart: a brand-new service and a brand-new scene load from the saved text only
            var restarted = new MemoryProgressStore(playSession.Json);
            ProgressService fresh = ProgressService.Create(One<ProgressManager>().Service.Catalog, restarted);
            Assert.That(fresh.TotalGold, Is.EqualTo(835));
            Assert.That(fresh.CollectedTreasureIds.Count, Is.EqualTo(18));
            Assert.That(fresh.IsAreaUnlocked("rocky_canyon"), Is.True);
            Assert.That(fresh.IsAreaUnlocked("ancient_mine"), Is.False);
            yield return CheckRockyRestored(restarted, picked, 835);

            // 3. and a treasure collected before is never paid again, however it is asked
            ProgressService again = ProgressService.Create(One<ProgressManager>().Service.Catalog, restarted);
            Assert.That(again.RegisterCollected("rocky_canyon.outpost_01", 10), Is.EqualTo(RegisterResult.AlreadyCollected));
            Assert.That(again.TotalGold, Is.EqualTo(835));
        }

        private IEnumerator CheckRockyRestored(MemoryProgressStore from, string[] collectedKeys, int expectedGold)
        {
            yield return Unload();
            yield return Load(RockyPath, null, from);
            var hunt = One<TreasureHuntController>();
            Assert.That(hunt.TotalGold, Is.EqualTo(expectedGold));
            foreach (TreasureSpot s in RockySpots())
            {
                bool should = collectedKeys.Any(k => s.SpotId == "rocky_canyon." + k);
                Assert.That(s.State, Is.EqualTo(should ? TreasureState.Collected : TreasureState.Hidden), s.SpotId + (should ? " stays collected" : " is still hidden"));
                if (should)
                {
                    Assert.That(s.VisualRoot.activeSelf, Is.False, s.SpotId + ": no model of a collected treasure");
                }
            }

            // standing right at a collected treasure offers nothing and pays nothing
            Transform player = RootObject("Player").transform;
            TreasureSpot collected = RockySpots().First(s => s.SpotId == "rocky_canyon." + collectedKeys[0]);
            Teleport(player, collected.transform.position + new Vector3(1.2f, 1.1f, 0f));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(FindButton("DigButton").gameObject.activeInHierarchy, Is.False, "no DIG for a collected treasure");
            FindButton("DigButton").onClick.Invoke();
            FindButton("CollectButton").onClick.Invoke();
            yield return null;
            Assert.That(hunt.TotalGold, Is.EqualTo(expectedGold), "no gold for a treasure that was collected in an earlier session");
        }

        [UnityTest]
        public IEnumerator NewGame_ResetsBothAreas_ButKeepsTheMinerAndTheSettings()
        {
            var green = new List<(string id, int gold)>();
            yield return ReadGreenMine(green);
            CharacterSelection.Select(CharacterSelection.FemaleId);
            GameSettings.Current.SetMusicVolume(0.3f);
            GameSettings.Current.SetSfxVolume(0.6f);
            yield return Load(RockyPath, FinishedGreenMineSave(green.Select(g => g.id), green.Sum(g => g.gold)));
            MemoryProgressStore session = store;
            Transform player = RootObject("Player").transform;
            WalkGraph graph = WalkGraph.Build(player);
            var log = new List<int>();
            yield return CollectNow(graph, player, "outpost_02", log);
            yield return CollectNow(graph, player, "ravine_04", log);
            Assert.That(log, Is.EqualTo(new[] { 635, 735 }));

            // exactly what the Settings panel's NEW GAME does: StartNewGame on the progress service
            ProgressService service = One<ProgressManager>().Service;
            Assert.That(service.StartNewGame(), Is.True);
            Assert.That(service.TotalGold, Is.EqualTo(0));
            Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(0), "the ids of BOTH areas are gone");
            Assert.That(service.UnlockedAreaIds, Is.EquivalentTo(new[] { "green_mine" }), "only Green Mine is open again");
            GameProgressData saved = JsonUtility.FromJson<GameProgressData>(session.Json);
            Assert.That(saved.totalGold, Is.EqualTo(0));
            Assert.That(saved.collectedTreasureIds.Count, Is.EqualTo(0), "and the reset was written");
            yield return Unload();

            // every treasure of both areas is hidden again after the reset
            yield return Load(RockyPath, null, session);
            Assert.That(RockySpots().All(s => s.State == TreasureState.Hidden), Is.True, "all 20 Rocky Canyon treasures are back");
            Assert.That(One<TreasureHuntController>().TotalGold, Is.EqualTo(0));
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
        public IEnumerator Thresholds_AncientMineOpensAtExactly1200_RockyCanyonAt500_TheirGoalsFollowTheTotal()
        {
            var green = new List<(string id, int gold)>();
            yield return ReadGreenMine(green);
            yield return Load(RockyPath);
            List<(string id, int gold)> rocky = RockySpots().Select(s => (s.SpotId, s.Definition.GoldValue)).ToList();
            AreaCatalog catalog = One<ProgressManager>().Service.Catalog;
            var memory = new MemoryProgressStore();
            ProgressService service = ProgressService.Create(catalog, memory);
            var unlocks = new List<string>();
            var completes = new List<string>();
            service.AreaUnlocked += unlocks.Add;
            service.AreaCompleted += completes.Add;

            Assert.That(service.IsAreaUnlocked("green_mine"), Is.True, "Green Mine is open from the start");
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.False);
            int total = 0;
            foreach ((string id, int gold) in green)
            {
                // Green Mine's 15 treasures (625 gold): Rocky Canyon opens on the registration that reaches 500
                Assert.That(service.RegisterCollected(id, gold), Is.EqualTo(RegisterResult.Accepted));
                total += gold;
                Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.EqualTo(total >= 500), $"Rocky Canyon at {total} gold");
            }

            Assert.That(total, Is.EqualTo(625));
            Assert.That(service.TotalGold, Is.EqualTo(625));
            Assert.That(service.IsAreaUnlocked("rocky_canyon"), Is.True);
            Assert.That(service.IsAreaUnlocked("ancient_mine"), Is.False);
            Assert.That(AreaSelectController.Evaluate(service, "ancient_mine", "").Availability, Is.EqualTo(AreaAvailability.Locked));
            Assert.That(AreaSelectController.Evaluate(service, "rocky_canyon", "RockyCanyon").Availability, Is.EqualTo(AreaAvailability.Playable));

            int crossing = -1;
            foreach ((string id, int gold) in rocky)
            {
                int before = service.TotalGold;
                Assert.That(service.RegisterCollected(id, gold), Is.EqualTo(RegisterResult.Accepted), id);
                Assert.That(service.TotalGold, Is.EqualTo(before + gold), "Rocky Canyon gold is added to the cumulative total");
                bool open = service.IsAreaUnlocked("ancient_mine");
                Assert.That(open, Is.EqualTo(service.TotalGold >= 1200), $"Ancient Mine at {service.TotalGold} gold");
                if (open && crossing < 0)
                {
                    crossing = service.TotalGold;
                    Assert.That(before, Is.LessThan(1200), "it opened on the registration that crossed 1200, not before");
                    Assert.That(AreaSelectController.Evaluate(service, "ancient_mine", "").Availability, Is.EqualTo(AreaAvailability.ComingSoon), "open, but COMING SOON (no map yet)");
                }
            }

            Assert.That(crossing, Is.GreaterThanOrEqualTo(1200));
            Assert.That(service.TotalGold, Is.EqualTo(total + 840));
            Assert.That(service.TotalGold, Is.EqualTo(1465), "Green Mine 625 + Rocky Canyon 840");
            Assert.That(unlocks, Is.EqualTo(new[] { "rocky_canyon", "ancient_mine" }), "each unlock was announced exactly once, in order");
            Assert.That(completes, Does.Contain("green_mine"));
            Assert.That(completes, Does.Contain("rocky_canyon"), "the 1200 goal of Rocky Canyon is reached");
            Assert.That(service.CollectedTreasureIds.Count, Is.EqualTo(35));
            foreach ((string id, int gold) in rocky)
            {
                Assert.That(service.RegisterCollected(id, gold), Is.EqualTo(RegisterResult.AlreadyCollected), id + " pays only once");
            }

            Assert.That(service.TotalGold, Is.EqualTo(1465), "and the total did not move");
            Assert.That(JsonUtility.FromJson<GameProgressData>(memory.Json).totalGold, Is.EqualTo(1465), "saved");
        }
    }
}
#endif
