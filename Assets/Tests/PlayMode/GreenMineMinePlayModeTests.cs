#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.Treasure;
using ItemCollector3D.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>The walkable Green Mine (GreenMineEnvironment/MineInterior): open mouth, 12 m tunnel, 8 x 10 m treasure room, camera behaviour inside, and the way back out.</summary>
    public sealed class GreenMineMinePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/GreenMine.unity";
        private const float AxisX = 6.3f;

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
            ProgressManager.StoreFactoryOverride = () => new MemoryProgressStore();
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

        private Transform Player => Root("Player").transform;

        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
        }

        private static Bounds CombinedBounds(IEnumerable<Collider> colliders)
        {
            bool first = true;
            Bounds b = default;
            foreach (Collider c in colliders)
            {
                if (first)
                {
                    b = c.bounds;
                    first = false;
                }
                else
                {
                    b.Encapsulate(c.bounds);
                }
            }

            return b;
        }

        // ================================================================ structure

        [UnityTest]
        public IEnumerator MineInterior_IsSeparatedModular_AndTheMouthIsOpen()
        {
            yield return LoadScene();
            Transform env = Root("GreenMineEnvironment").transform;
            Transform interior = env.Find("MineInterior");
            Assert.That(interior, Is.Not.Null, "MineInterior lives under GreenMineEnvironment");
            Transform tunnel = interior.Find("Tunnel"), room = interior.Find("TreasureRoom");
            Assert.That(tunnel, Is.Not.Null);
            Assert.That(room, Is.Not.Null);

            foreach (MonoBehaviour behaviour in interior.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Assert.That(behaviour.GetType().Name, Is.EqualTo("UniversalAdditionalLightData"), "the interior is art only: " + behaviour.name);
            }

            Assert.That(Root("GameplaySystems").GetComponentInChildren<MineInteriorZone>(true), Is.Not.Null, "the interior's mechanics live in GameplaySystems");

            // modular prefabs, not loose boxes
            foreach (string module in new[] { "MineTunnel_Segment_0", "MineTimber_Frame_0", "MineLantern_0", "MineRail_Segment_0" })
            {
                Transform t = tunnel.Find(module);
                Assert.That(t, Is.Not.Null, module);
            }

            foreach (string prefab in new[] { "MineTunnel_Segment", "MineTimber_Frame", "MineTimber_FrameWide", "MineLantern_Wall", "MineRail_Segment", "MineRoom_Shell", "Chest_Wood" })
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GreenMine/MineInterior/" + prefab + ".prefab"), Is.Not.Null, prefab + " is a prefab asset");
            }

            // tunnel 12-15 m, room about 8 x 10 m
            var segments = new List<Collider>();
            foreach (Transform child in tunnel)
            {
                if (child.name.StartsWith("MineTunnel_Segment_"))
                {
                    segments.AddRange(child.GetComponentsInChildren<Collider>());
                }
            }

            Assert.That(CombinedBounds(segments).size.z, Is.InRange(12f, 15f), "tunnel length");
            Transform shell = room.Find("MineRoom_Shell");
            float width = shell.Find("Wall_E").GetComponent<Collider>().bounds.min.x - shell.Find("Wall_W").GetComponent<Collider>().bounds.max.x;
            float depth = shell.Find("Wall_N").GetComponent<Collider>().bounds.min.z - shell.Find("Wall_S_W").GetComponent<Collider>().bounds.max.z;
            Assert.That(width, Is.EqualTo(8f).Within(0.3f), "room width");
            Assert.That(depth, Is.EqualTo(10f).Within(0.3f), "room depth");

            // the old placeholder collider and the invisible wall no longer stop the player at the entrance
            Assert.That(Physics.Raycast(new Vector3(AxisX, 1f, 10f), Vector3.forward, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore), Is.True, "the room's far side is solid");
            Assert.That(hit.point.z, Is.GreaterThan(33f), "nothing blocks the axis from the apron to the crystals, first hit: " + hit.collider.name + " at z=" + hit.point.z);
            Assert.That(Physics.CheckCapsule(new Vector3(AxisX, 0.6f, 14.6f), new Vector3(AxisX, 1.6f, 14.6f), 0.48f, ~0, QueryTriggerInteraction.Ignore), Is.False, "the portal is open");
            foreach (BoxCollider wall in env.Find("Boundaries/Wall_2").GetComponents<BoxCollider>())
            {
                Assert.That(wall.bounds.Contains(new Vector3(AxisX, 2f, 15.7f)), Is.False, "the boundary wall leaves the mine entrance free");
            }

            // lighting: a few warm point lights, no shadows; the sun stays the only directional light
            int lights = 0;
            foreach (Light l in interior.GetComponentsInChildren<Light>(true))
            {
                lights++;
                Assert.That(l.type, Is.EqualTo(LightType.Point));
                Assert.That(l.shadows, Is.EqualTo(LightShadows.None));
            }

            Assert.That(lights, Is.InRange(3, 8), "few lights");

            // no new collectible: the existing treasure points are all outside the interior
            foreach (TreasureSpot spot in Root("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true))
            {
                Vector3 p = spot.transform.position;
                Assert.That(p.x > 1.5f && p.x < 11.5f && p.z > 14.5f, Is.False, spot.name + " stays where it was");
            }

            Assert.That(Root("TreasureSpots").transform.childCount, Is.InRange(6, 8));
        }

        // ================================================================ walking in and out with the real controller and camera

        private sealed class Stats
        {
            public int Frames, CameraInside, PlayerHidden;
            public string FirstInside = "", FirstHidden = "";
            public float MinMineCameraDistance = float.MaxValue, MinY = float.MaxValue, MaxY = float.MinValue, MaxMood;
            public bool ReachedRoom, ReachedTunnelEnd, DimAmbientSeen;
            public int GlobalLightTouched;
        }

        private IEnumerator WalkRoute(string miner, float yaw, Stats stats)
        {
            CharacterSelection.Select(miner);
            yield return LoadScene();
            Time.timeScale = 2f;
            Transform player = Player;
            var controller = player.GetComponent<ThirdPersonPlayerController>();
            Camera camera = Camera.main;
            var cameraController = camera.GetComponent<ThirdPersonCameraController>();
            var zone = Root("GameplaySystems").GetComponentInChildren<MineInteriorZone>(true);

            var so = new SerializedObject(cameraController);
            so.FindProperty("yaw").floatValue = yaw;
            so.FindProperty("pitch").floatValue = 18f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor, fog = RenderSettings.fogColor;
            float sun = RenderSettings.sun.intensity, fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;

            Teleport(player, new Vector3(AxisX, 1.3f, 10.5f));
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Vector2[] route =
            {
                new(AxisX, 13.0f), new(AxisX, 17.5f), new(AxisX, 24.0f), new(AxisX, 29.0f),   // apron, portal, tunnel, room door
                new(AxisX, 31.5f), new(8.6f, 32.0f), new(4.4f, 31.0f), new(AxisX, 29.0f),      // the open middle of the room
                new(AxisX, 24.0f), new(AxisX, 17.5f), new(AxisX, 12.0f),                       // and out again
            };

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

                    Vector3 f = camera.transform.forward;
                    f.y = 0f;
                    f.Normalize();
                    Vector3 r = new(f.z, 0f, -f.x);
                    Vector3 d = delta.normalized;
                    controller.SetMoveInput(new Vector2(Vector3.Dot(d, r), Vector3.Dot(d, f)));
                    yield return null;

                    stats.Frames++;
                    Vector3 pos = player.position;
                    stats.MinY = Mathf.Min(stats.MinY, pos.y);
                    stats.MaxY = Mathf.Max(stats.MaxY, pos.y);
                    stats.MaxMood = Mathf.Max(stats.MaxMood, zone.MoodBlend);
                    if (RenderSettings.sun.intensity != sun || RenderSettings.fogColor != fog || RenderSettings.fogStartDistance != fogStart || RenderSettings.fogEndDistance != fogEnd)
                    {
                        stats.GlobalLightTouched++; // the daylight seen through the mouth must stay daylight: no global sun / fog change
                    }

                    if (zone.MoodBlend > 0.95f)
                    {
                        stats.DimAmbientSeen |= RenderSettings.ambientSkyColor.grayscale < sky.grayscale * 0.8f;
                    }
                    stats.ReachedTunnelEnd |= pos.z > 25f;
                    stats.ReachedRoom |= pos.z > 29f && Mathf.Abs(pos.x - AxisX) < 3.7f;

                    Vector3 pivot = pos + new Vector3(0f, 1.4f, 0f);
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

                    if (Physics.Linecast(camera.transform.position, pivot, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(player))
                    {
                        stats.PlayerHidden++;
                        if (stats.FirstHidden.Length == 0)
                        {
                            stats.FirstHidden = $"{hit.collider.name} (camera {camera.transform.position}, player {pos})";
                        }
                    }

                    if (pos.z > 15.6f && pos.z < 36f)
                    {
                        stats.MinMineCameraDistance = Mathf.Min(stats.MinMineCameraDistance, Vector3.Distance(camera.transform.position, pivot));
                    }
                }
            }

            controller.ClearMoveInput();
            so.FindProperty("yaw").floatValue = 0f; // the camera swings back behind the player, outside the tunnel
            so.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < 120; i++) // let the camera and the mood settle outside again
            {
                yield return null;
            }

            Assert.That(zone.MoodBlend, Is.Zero, $"outside again ({miner}, yaw {yaw}): no underground mood left, camera at {camera.transform.position}");
            Assert.That(zone.CameraBlend, Is.Zero, "outside again: the camera orbit is back to normal");
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(sky), "the sky light is restored exactly");
            Assert.That(RenderSettings.ambientEquatorColor, Is.EqualTo(equator), "the equator light is restored exactly");
            Assert.That(RenderSettings.ambientGroundColor, Is.EqualTo(ground), "the ground light is restored exactly");
            Assert.That(RenderSettings.fogColor, Is.EqualTo(fog), "the fog is untouched");
            Assert.That(RenderSettings.sun.intensity, Is.EqualTo(sun), "the sun is untouched");
            Assert.That(player.position.z, Is.LessThan(13.5f), "back on the apron");
            yield return SceneManager.UnloadSceneAsync(scene);
            scene = default;
            yield return null;
        }

        [UnityTest]
        [RealisticFrameRate]
        public IEnumerator BothMiners_WalkIntoTheTunnel_ReachTheTreasureRoom_AndOut_WithAWellBehavedCamera()
        {
            var all = new Stats();
            foreach ((string miner, float yaw) in new[] { (CharacterSelection.MaleId, 0f), (CharacterSelection.FemaleId, 110f), (CharacterSelection.MaleId, 200f) })
            {
                var s = new Stats();
                yield return WalkRoute(miner, yaw, s);
                Debug.Log($"GreenMine mine walk ({miner}, camera yaw {yaw}): {s.Frames} frames, reached tunnel end {s.ReachedTunnelEnd}, room {s.ReachedRoom}, min camera distance in the mine {s.MinMineCameraDistance:F2} m, camera inside geometry {s.CameraInside}, player hidden {s.PlayerHidden}, y {s.MinY:F2}..{s.MaxY:F2}, max mood {s.MaxMood:F2}.");
                Assert.That(s.ReachedTunnelEnd, Is.True, miner + " reaches the end of the tunnel");
                Assert.That(s.ReachedRoom, Is.True, miner + " reaches the treasure room");
                Assert.That(s.CameraInside, Is.Zero, "the camera never enters a wall, the ceiling or a beam (yaw " + yaw + "): " + s.FirstInside);
                Assert.That(s.PlayerHidden, Is.Zero, "nothing is ever between the camera and the player (yaw " + yaw + "): " + s.FirstHidden);
                Assert.That(s.MinMineCameraDistance, Is.GreaterThan(1.2f), "the camera is never pushed into the player's head");
                Assert.That(s.MinY, Is.GreaterThan(0.9f), "the player never sinks into the floor");
                Assert.That(s.MaxY, Is.LessThan(1.7f), "the player never climbs onto crates, rocks or crystals");
                Assert.That(s.MaxMood, Is.GreaterThan(0.95f), "the underground mood reaches full strength inside");
                Assert.That(s.DimAmbientSeen, Is.True, "the sky light is dimmed to a warm underground tone inside");
                Assert.That(s.GlobalLightTouched, Is.Zero, "the sun and the fog never change on the way through the mine (outdoors stays daylight)");
                all.Frames += s.Frames;
            }

            Assert.That(all.Frames, Is.GreaterThan(300));
        }

        [UnityTest]
        public IEnumerator Lighting_SurvivesPauseAndResume_InsideAndWhileLeavingTheMine()
        {
            yield return LoadScene();
            Transform player = Player;
            var zone = Root("GameplaySystems").GetComponentInChildren<MineInteriorZone>(true);
            var so = new SerializedObject(Camera.main.GetComponent<ThirdPersonCameraController>());
            so.FindProperty("yaw").floatValue = 0f;
            so.FindProperty("pitch").floatValue = 18f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Color outdoorSky = RenderSettings.ambientSkyColor, outdoorEquator = RenderSettings.ambientEquatorColor, fog = RenderSettings.fogColor;
            float sun = RenderSettings.sun.intensity;

            Teleport(player, new Vector3(AxisX, 1.3f, 20.5f));
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(zone.MoodBlend, Is.GreaterThan(0.95f), "inside the tunnel");
            Color inside = RenderSettings.ambientSkyColor;
            Assert.That(inside.grayscale, Is.LessThan(outdoorSky.grayscale * 0.8f), "dim, warm ambient underground");

            GamePause.Pause();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(inside), "paused inside: the mood stays as it was");
            GamePause.Resume();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(inside), "resumed inside: still the underground mood");

            Teleport(player, new Vector3(-1.4f, 1.2f, 0.4f)); // out in the open: the mood fades away
            float waited = 0f;
            while ((zone.MoodBlend <= 0.05f || zone.MoodBlend >= 0.95f) && waited < 3f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(zone.MoodBlend, Is.InRange(0.05f, 0.95f), "caught the fade half way");
            GamePause.Pause();
            Color halfWay = RenderSettings.ambientSkyColor;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(halfWay), "paused mid-fade: nothing moves");
            GamePause.Resume();
            yield return new WaitForSecondsRealtime(1.8f);

            Assert.That(zone.MoodBlend, Is.Zero, "outdoors again");
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(outdoorSky), "the original sky light is back exactly");
            Assert.That(RenderSettings.ambientEquatorColor, Is.EqualTo(outdoorEquator));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(fog), "the fog was never touched");
            Assert.That(RenderSettings.sun.intensity, Is.EqualTo(sun), "the sun was never touched");
        }

        [UnityTest]
        public IEnumerator Camera_SwingsOffTheWall_InsteadOfSqueezingTheHeadOntoTheScreen_ForBothMiners()
        {
            foreach (string miner in new[] { CharacterSelection.MaleId, CharacterSelection.FemaleId })
            {
                CharacterSelection.Select(miner);
                yield return LoadScene();
                Transform player = Player;
                Camera camera = Camera.main;
                var so = new SerializedObject(camera.GetComponent<ThirdPersonCameraController>());
                so.FindProperty("pitch").floatValue = 18f;
                Assert.That(so.FindProperty("swingComfortDistance").floatValue, Is.GreaterThan(2f), "the swing is switched on in the scene (a zero would silently disable it)");

                // the player stands 1.2-1.3 m from a wall (or just in front of the crystals) with the orbit pointing at it: (x, z, camera yaw)
                var cases = new[]
                {
                    (5.2f, 20.0f, 90f), (7.4f, 20.0f, 270f), (5.2f, 22.5f, 90f), (7.4f, 17.5f, 270f), (6.3f, 33.0f, 180f), (3.6f, 28.0f, 90f),
                };
                foreach ((float x, float z, float yaw) c in cases)
                {
                    so.FindProperty("yaw").floatValue = c.yaw;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Teleport(player, new Vector3(c.x, 1.3f, c.z));
                    yield return new WaitForSecondsRealtime(1.4f);

                    Vector3 pivot = player.position + new Vector3(0f, 1.4f, 0f);
                    float distance = Vector3.Distance(camera.transform.position, pivot);
                    string where = $"{miner} at ({c.x}, {c.z}) with the orbit pointing at the wall (yaw {c.yaw}), camera at {camera.transform.position}";
                    Assert.That(distance, Is.GreaterThan(2.3f), "the helmet does not fill the screen: " + where);
                    foreach (Collider hit in Physics.OverlapSphere(camera.transform.position, 0.12f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Assert.That(hit.transform.IsChildOf(player), Is.True, $"the camera is not inside {hit.name}: " + where);
                    }

                    if (Physics.Linecast(camera.transform.position, pivot, out RaycastHit blocker, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Assert.That(blocker.transform.IsChildOf(player), Is.True, $"nothing between the camera and the player ({blocker.collider.name}): " + where);
                    }
                }

                yield return SceneManager.UnloadSceneAsync(scene);
                scene = default;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Camera_PullsInInsideTheMine_AndIsRestoredOutside()
        {
            yield return LoadScene();
            Transform player = Player;
            Camera camera = Camera.main;
            var cameraController = camera.GetComponent<ThirdPersonCameraController>();
            var so = new SerializedObject(cameraController);
            float nominal = so.FindProperty("distance").floatValue;
            so.FindProperty("yaw").floatValue = 0f;
            so.FindProperty("pitch").floatValue = 18f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Teleport(player, new Vector3(AxisX, 1.3f, 20.5f));
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3 pivot = player.position + new Vector3(0f, 1.4f, 0f);
            Assert.That(Vector3.Distance(camera.transform.position, pivot), Is.LessThan(nominal * 0.75f), "the orbit is shorter inside the tunnel");
            Assert.That(cameraController.EffectiveDistance, Is.LessThan(nominal));

            Teleport(player, new Vector3(-1.4f, 1.2f, 0.4f));
            yield return new WaitForSecondsRealtime(1.5f);
            pivot = player.position + new Vector3(0f, 1.4f, 0f);
            Assert.That(Vector3.Distance(camera.transform.position, pivot), Is.EqualTo(nominal).Within(0.15f), "back to the full distance outdoors");
            Assert.That(cameraController.EffectiveDistance, Is.EqualTo(nominal).Within(0.001f));
        }
    }
}
#endif
