#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// The Green Mine forest trail (west meadow -> forest edge -> north meadow, and the branch to the forest trail end): tree crowns have no collider, so
    /// the camera can end up inside or behind foliage. These tests measure how often the player is hidden behind tree geometry while the camera orbits
    /// (statically on a grid of trail positions x orbit angles, and walking with both miners while the camera keeps turning).
    /// </summary>
    public sealed class GreenMineForestPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/GreenMine.unity";

        // forest trail legs: West Meadow -> Forest Edge -> North Meadow, and the branch Forest Edge -> Forest Trail End (the tour waypoints of the scene)
        private static readonly Vector2 WestMeadow = new(-11.4f, -1.2f);
        private static readonly Vector2 ForestEdge = new(-11.0f, 4.2f);
        private static readonly Vector2 NorthMeadow = new(-1.6f, 8.4f);
        private static readonly Vector2 ForestTrailEnd = new(-14.6f, 9.0f);

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
            if (scene.IsValid() && scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            scene = default;
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
        }

        private IEnumerator LoadScene()
        {
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore(); // never the developer's save file
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

        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
        }

        // ================================================================ foliage geometry

        private sealed class Crown
        {
            public string Name;
            public Vector3 Center;
            public Vector3[] Vertices;
            public int[] Triangles;
        }

        private List<Crown> Crowns()
        {
            var crowns = new List<Crown>();
            foreach (MeshFilter filter in Root("GreenMineEnvironment").GetComponentsInChildren<MeshFilter>(false))
            {
                if (!filter.name.StartsWith("Pine") && !filter.name.StartsWith("Tree_Round"))
                {
                    continue;
                }

                Mesh mesh = filter.sharedMesh;
                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                Vector3[] local = mesh.vertices;
                var world = new Vector3[local.Length];
                for (int i = 0; i < local.Length; i++)
                {
                    world[i] = toWorld.MultiplyPoint3x4(local[i]);
                }

                crowns.Add(new Crown { Name = filter.name, Center = filter.transform.position, Vertices = world, Triangles = mesh.triangles });
            }

            return crowns;
        }

        /// <summary>True when the segment a-b crosses the tree's triangles (a camera inside the crown or leaves between the camera and the player).</summary>
        private static bool Blocks(Crown crown, Vector3 a, Vector3 b)
        {
            Vector3 direction = b - a;
            float length = direction.magnitude;
            direction /= length;
            Vector3[] v = crown.Vertices;
            int[] t = crown.Triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 p0 = v[t[i]];
                Vector3 e1 = v[t[i + 1]] - p0, e2 = v[t[i + 2]] - p0;
                Vector3 pv = Vector3.Cross(direction, e2);
                float det = Vector3.Dot(e1, pv);
                if (Mathf.Abs(det) < 1e-7f)
                {
                    continue;
                }

                float inv = 1f / det;
                Vector3 s = a - p0;
                float u = Vector3.Dot(s, pv) * inv;
                if (u < 0f || u > 1f)
                {
                    continue;
                }

                Vector3 q = Vector3.Cross(s, e1);
                float w = Vector3.Dot(direction, q) * inv;
                if (w < 0f || u + w > 1f)
                {
                    continue;
                }

                float hit = Vector3.Dot(e2, q) * inv;
                if (hit > 0f && hit < length)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Hidden(List<Crown> crowns, Vector3 camera, Vector3 pivot)
        {
            foreach (Crown crown in crowns)
            {
                if ((crown.Center - pivot).sqrMagnitude < 14f * 14f && Blocks(crown, camera, pivot))
                {
                    return true;
                }
            }

            return false;
        }

        // ================================================================ static sight lines

        [UnityTest]
        public IEnumerator ForestTrail_KeepsThePlayerVisible_FromEveryCameraAngle()
        {
            yield return LoadScene();
            List<Crown> crowns = Crowns();
            Assert.That(crowns.Count, Is.GreaterThan(12), "the forest still has its trees");

            Camera camera = Camera.main;
            var so = new SerializedObject(camera.GetComponent<ThirdPersonCameraController>());
            float distance = so.FindProperty("distance").floatValue;
            float pitch = so.FindProperty("pitch").floatValue;
            Vector3 offset = so.FindProperty("targetOffset").vector3Value;
            float radius = so.FindProperty("collisionRadius").floatValue;
            float padding = so.FindProperty("collisionPadding").floatValue;
            float minimum = so.FindProperty("minimumDistance").floatValue;
            int mask = (int)so.FindProperty("collisionMask").FindPropertyRelative("m_Bits").uintValue;

            Collider ground = Root("GreenMineEnvironment").transform.Find("Terrain/Ground").GetComponent<Collider>();
            Transform player = Root("Player").transform;
            Assert.That(ground.Raycast(new Ray(player.position + Vector3.up * 5f, Vector3.down), out RaycastHit spawnGround, 30f), Is.True);
            float standHeight = player.position.y - spawnGround.point.y; // the grounded height of the player's transform

            Vector2[][] legs = { new[] { WestMeadow, ForestEdge }, new[] { ForestEdge, NorthMeadow }, new[] { ForestEdge, ForestTrailEnd } };
            int samples = 0, hidden = 0, positions = 0;
            float worst = 0f;
            Vector2 worstAt = default;
            float edgeFraction = -1f;
            foreach (Vector2[] leg in legs)
            {
                Vector2 along = (leg[1] - leg[0]).normalized, side = new(-along.y, along.x);
                int steps = Mathf.CeilToInt(Vector2.Distance(leg[0], leg[1]));
                for (int step = 0; step <= steps; step++)
                {
                    for (float lateral = -1.2f; lateral <= 1.21f; lateral += 1.2f)
                    {
                        Vector2 p = Vector2.Lerp(leg[0], leg[1], step / (float)steps) + side * lateral;
                        if (!ground.Raycast(new Ray(new Vector3(p.x, 30f, p.y), Vector3.down), out RaycastHit gh, 60f))
                        {
                            continue;
                        }

                        Vector3 pivot = new Vector3(p.x, gh.point.y + standHeight, p.y) + offset;
                        int count = 0, hiddenHere = 0;
                        for (int yaw = 0; yaw < 360; yaw += 20)
                        {
                            Vector3 direction = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
                            float resolved = distance;
                            if (Physics.SphereCast(pivot, radius, direction, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
                            {
                                resolved = Mathf.Clamp(hit.distance - padding, Mathf.Min(minimum, distance), distance);
                            }

                            count++;
                            if (Hidden(crowns, pivot + direction * resolved, pivot))
                            {
                                hiddenHere++;
                            }
                        }

                        samples += count;
                        hidden += hiddenHere;
                        positions++;
                        float fraction = hiddenHere / (float)count;
                        if (fraction > worst)
                        {
                            worst = fraction;
                            worstAt = p;
                        }

                        if (Mathf.Abs(lateral) < 0.01f && Vector2.Distance(p, ForestEdge) < 0.6f)
                        {
                            edgeFraction = Mathf.Max(edgeFraction, fraction);
                        }
                    }
                }
            }

            Debug.Log($"GreenMine forest trail sight lines: {positions} positions x 18 orbit angles, player hidden behind foliage in {hidden}/{samples} ({100f * hidden / samples:F1}%), worst position {worstAt} loses the player at {100f * worst:F0}% of the angles, Forest Edge {100f * edgeFraction:F0}%.");
            Assert.That(positions, Is.GreaterThan(40));
            Assert.That(hidden / (float)samples, Is.LessThan(0.04f), "the player is rarely hidden behind foliage on the forest trail");
            Assert.That(worst, Is.LessThan(0.30f), "no spot of the trail loses the player at more than ~1/4 of the camera angles: " + worstAt);
            Assert.That(edgeFraction, Is.GreaterThanOrEqualTo(0f), "the Forest Edge waypoint was sampled");
            Assert.That(edgeFraction, Is.LessThan(0.12f), "the Forest Edge (-11, 4) keeps the player in view");
        }

        // ================================================================ walking with both miners

        private sealed class Stats
        {
            public int Frames, Hidden, CameraInside;
            public float MinY = float.MaxValue, MaxY = float.MinValue, MinCamera = float.MaxValue;
            public string FirstInside = string.Empty;
        }

        private IEnumerator WalkForestTrail(string miner, float degreesPerSecond, Stats stats)
        {
            CharacterSelection.Select(miner);
            yield return LoadScene();
            Time.timeScale = 2f;
            Transform player = Root("Player").transform;
            var controller = player.GetComponent<ThirdPersonPlayerController>();
            Camera camera = Camera.main;
            var so = new SerializedObject(camera.GetComponent<ThirdPersonCameraController>());
            Collider ground = Root("GreenMineEnvironment").transform.Find("Terrain/Ground").GetComponent<Collider>();
            List<Crown> crowns = Crowns();

            Assert.That(ground.Raycast(new Ray(new Vector3(WestMeadow.x, 30f, WestMeadow.y), Vector3.down), out RaycastHit start, 60f), Is.True);
            Teleport(player, new Vector3(WestMeadow.x, start.point.y + 1.3f, WestMeadow.y));
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Vector2[] route = { ForestEdge, NorthMeadow, ForestEdge, ForestTrailEnd, ForestEdge, WestMeadow };
            float yaw = so.FindProperty("yaw").floatValue;
            foreach (Vector2 target in route)
            {
                float legTime = 0f;
                float legStart = Vector2.Distance(new Vector2(player.position.x, player.position.z), target);
                while (true)
                {
                    Vector3 delta = new Vector3(target.x - player.position.x, 0f, target.y - player.position.z);
                    if (delta.magnitude < 0.9f)
                    {
                        break;
                    }

                    legTime += Time.deltaTime;
                    Assert.That(legTime, Is.LessThan(legStart / 3.0f + 4f), $"{miner} is stuck on the way to {target} at {player.position}");

                    yaw += degreesPerSecond * Time.deltaTime; // the camera keeps turning all the way
                    so.FindProperty("yaw").floatValue = yaw;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    Vector3 f = camera.transform.forward;
                    f.y = 0f;
                    f.Normalize();
                    Vector3 r = new(f.z, 0f, -f.x);
                    Vector3 d = delta.normalized;
                    controller.SetMoveInput(new Vector2(Vector3.Dot(d, r), Vector3.Dot(d, f)));
                    yield return null;

                    stats.Frames++;
                    Vector3 pos = player.position;
                    if (ground.Raycast(new Ray(pos + Vector3.up * 5f, Vector3.down), out RaycastHit gh, 30f))
                    {
                        stats.MinY = Mathf.Min(stats.MinY, pos.y - gh.point.y);
                        stats.MaxY = Mathf.Max(stats.MaxY, pos.y - gh.point.y);
                    }

                    Vector3 pivot = pos + new Vector3(0f, 1.4f, 0f);
                    stats.MinCamera = Mathf.Min(stats.MinCamera, Vector3.Distance(camera.transform.position, pivot));
                    if (Hidden(crowns, camera.transform.position, pivot))
                    {
                        stats.Hidden++;
                    }

                    foreach (Collider c in Physics.OverlapSphere(camera.transform.position, 0.12f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (!c.transform.IsChildOf(player))
                        {
                            stats.CameraInside++;
                            if (stats.FirstInside.Length == 0)
                            {
                                stats.FirstInside = $"{c.name} (camera {camera.transform.position}, player {pos})";
                            }

                            break;
                        }
                    }
                }
            }

            controller.ClearMoveInput();
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            yield return null;
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator BothMiners_WalkTheForestTrail_WithATurningCamera_AndStayInView()
        {
            int frames = 0;
            foreach ((string miner, float turn) in new[] { (CharacterSelection.MaleId, 40f), (CharacterSelection.FemaleId, -40f) })
            {
                var s = new Stats();
                yield return WalkForestTrail(miner, turn, s);
                Debug.Log($"GreenMine forest walk ({miner}, camera turning {turn:F0} deg/s): {s.Frames} frames, player hidden behind foliage in {s.Hidden} ({100f * s.Hidden / Mathf.Max(1, s.Frames):F1}%), camera inside geometry {s.CameraInside}, min camera distance {s.MinCamera:F2} m, player height above ground {s.MinY:F2}..{s.MaxY:F2} m.");
                Assert.That(s.Frames, Is.GreaterThan(150), miner + " walked the whole forest trail");
                Assert.That(s.Hidden / (float)s.Frames, Is.LessThan(0.05f), miner + " is rarely hidden behind foliage while the camera turns");
                Assert.That(s.CameraInside, Is.Zero, "the camera never enters a trunk or rock on the forest trail: " + s.FirstInside);
                Assert.That(s.MinCamera, Is.GreaterThan(1.6f), "the camera is never pushed into the player");
                Assert.That(s.MinY, Is.GreaterThan(0.6f), "the player never sinks into the ground");
                Assert.That(s.MaxY, Is.LessThan(2.0f), "the player never floats or climbs onto something");
                frames += s.Frames;
            }

            Assert.That(frames, Is.GreaterThan(300));
        }
    }
}
#endif
