#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ItemCollector3D.Characters;
using ItemCollector3D.Player;
using ItemCollector3D.Progress;
using ItemCollector3D.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ItemCollector3D.Tests.PlayMode
{
    /// <summary>
    /// Green Mine exterior polish (stage 10B.3): the river and the waterfall move (texture scroll through a MaterialPropertyBlock, no new material or
    /// collider), the pale strip that the river bed paint left on the slope behind the end of the water is gone, and the small camp additions stay cheap and
    /// off the trails and out of the camera's way.
    /// </summary>
    public sealed class GreenMineExteriorPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/GreenMine.unity";
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

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

        private static float GroundHeight(float x, float z)
        {
            float best = float.NaN;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, 40f, z), Vector3.down, 80f))
            {
                if (hit.collider.name == "Ground")
                {
                    best = hit.point.y;
                }
            }

            return best;
        }

        // ================================================================ water motion

        private static float Wrap(float delta) => Mathf.Repeat(delta + 0.5f, 1f) - 0.5f;

        [UnityTest]
        public IEnumerator Water_FlowsDownstreamAndDownTheFall_FreezesWithThePause_AndReusesTheExistingMaterials()
        {
            yield return LoadScene();
            var motion = Root("GameplaySystems").GetComponentInChildren<WaterMotion>(true);
            Assert.That(motion, Is.Not.Null, "the water motion lives under GameplaySystems (the environment stays script-free)");
            Assert.That(motion.FlowCount, Is.EqualTo(2), "river and waterfall");

            Transform water = Root("GreenMineEnvironment").transform.Find("Water");
            Renderer river = water.Find("River").GetComponent<Renderer>();
            Renderer fall = water.Find("Waterfall").GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            Vector4 St(Renderer r)
            {
                r.GetPropertyBlock(block);
                return block.GetVector(BaseMapSt);
            }

            yield return null;
            Vector4 river0 = St(river), fall0 = St(fall);
            yield return new WaitForSeconds(0.25f);
            Vector4 river1 = St(river), fall1 = St(fall);

            Assert.That(Wrap(river1.z - river0.z), Is.LessThan(-0.01f), "the river pattern travels towards larger u = downstream (north to south)");
            Assert.That(Wrap(fall1.w - fall0.w), Is.GreaterThan(0.1f), "the waterfall pattern travels towards smaller v = down the fall");
            Assert.That(fall1.y, Is.GreaterThanOrEqualTo(2f), "several tiles over the fall height, so the streaks are visibly small and fast");

            // the paused game freezes the water with everything else
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.1f);
            Vector4 frozen0 = St(river);
            yield return new WaitForSecondsRealtime(0.25f);
            Vector4 frozen1 = St(river);
            Time.timeScale = 1f;
            Assert.That(frozen1.z, Is.EqualTo(frozen0.z).Within(1e-4f), "no water motion while paused");

            // existing shared materials only, never instanced, never edited; still no colliders on the water, the bridge keeps its own
            Assert.That(river.sharedMaterial.name, Is.EqualTo("GM_Water"));
            Assert.That(fall.sharedMaterial.name, Is.EqualTo("GM_Waterfall"));
            Assert.That(river.sharedMaterial.GetTextureOffset("_BaseMap"), Is.EqualTo(Vector2.zero), "the material asset is not scrolled, only the renderers' property blocks");
            Assert.That(fall.sharedMaterial.GetTextureOffset("_BaseMap"), Is.EqualTo(Vector2.zero));
            Assert.That(river.GetComponent<Collider>(), Is.Null);
            Assert.That(fall.GetComponent<Collider>(), Is.Null);
            Assert.That(water.Find("Bridge_Wood").GetComponents<Collider>().Length, Is.GreaterThan(0), "the bridge colliders are untouched");
            Assert.That(fall.sharedMaterial.mainTexture.wrapModeV, Is.EqualTo(TextureWrapMode.Repeat), "the fall texture tiles along the fall so it can scroll");
        }

        // ================================================================ river end

        [UnityTest]
        public IEnumerator RiverEnd_TheDrySlopeBehindTheWaterIsNotPaintedAsRiverBed()
        {
            yield return LoadScene();
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(tex.LoadImage(File.ReadAllBytes("Assets/Art/GreenMine/Textures/GreenMine_TerrainColor.png")), Is.True);

            int sampled = 0;
            var teal = new List<string>();
            for (float x = 16.9f; x <= 20.5f; x += 0.4f)
            {
                for (float z = -20.5f; z >= -22.6f; z -= 0.3f)
                {
                    float h = GroundHeight(x, z);
                    if (float.IsNaN(h) || h < -0.05f)
                    {
                        continue; // still under the water: the bed colour belongs there
                    }

                    sampled++;
                    Color c = tex.GetPixelBilinear((x + 36f) / 72f, (z + 36f) / 72f);
                    if (c.b > c.r + 0.05f && c.g > c.r + 0.10f)
                    {
                        teal.Add($"({x:0.0},{z:0.0})");
                    }
                }
            }

            Assert.That(sampled, Is.GreaterThan(30), "the slope behind the end of the river was sampled");
            Assert.That(teal, Is.Empty, "river-bed teal on dry ground behind the end of the water shows as a pale strip: " + string.Join(" ", teal));
        }

        // ================================================================ camp

        [UnityTest]
        public IEnumerator Camp_AdditionsStayCheap_OffTheTrails_AndCarryNoColliderOnFoliage()
        {
            yield return LoadScene();
            Transform environment = Root("GreenMineEnvironment").transform;

            int renderers = 0;
            var materials = new HashSet<Material>();
            foreach (MeshRenderer r in environment.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                foreach (Material m in r.sharedMaterials)
                {
                    materials.Add(m);
                }
            }

            Debug.Log($"GreenMine exterior: {renderers} renderers, {materials.Count} materials.");
            Assert.That(renderers, Is.LessThanOrEqualTo(660), "the camp touch-up added a handful of renderers (627 before)");
            Assert.That(materials.Count, Is.LessThanOrEqualTo(66), "no new material (66 before)");

            foreach (string group in new[] { "GrassTufts", "Flowers" })
            {
                foreach (Collider c in environment.Find("Vegetation/" + group).GetComponentsInChildren<Collider>(true))
                {
                    Assert.Fail("foliage never blocks the player or the camera: " + c.name);
                }
            }

            // the log seat south of the fire: a low seat that is nowhere near the spawn or the trails
            Transform log = environment.Find("StartCamp/Log_A");
            Assert.That(log, Is.Not.Null, "log seat at the camp fire");
            Vector2 at = new(log.position.x, log.position.z);
            Assert.That(Vector2.Distance(at, new Vector2(-8f, -10.4f)), Is.GreaterThan(3f), "not at the spawn");
            Vector2[][] trails =
            {
                new Vector2[] { new(-4.4f, -19.5f), new(-4.4f, -15.5f), new(-4.7f, -11f), new(-5f, -7.8f) },
                new Vector2[] { new(-4.7f, -10.4f), new(-7.6f, -10.5f), new(-10f, -10.9f), new(-12.4f, -11.2f) },
            };
            foreach (Vector2[] trail in trails)
            {
                for (int i = 0; i < trail.Length - 1; i++)
                {
                    Vector2 a = trail[i], b = trail[i + 1], ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(at - a, ab) / ab.sqrMagnitude);
                    Assert.That(Vector2.Distance(at, a + ab * t), Is.GreaterThan(2.6f), "the log leaves the 3.4 m trail and its verge free");
                }
            }
        }

        [UnityTest]
        public IEnumerator Camp_CameraKeepsItsDistance_BesideTheFireAndTheLogSeat()
        {
            yield return LoadScene();
            Camera camera = Camera.main;
            var so = new SerializedObject(camera.GetComponent<ThirdPersonCameraController>());
            Transform player = Root("Player").transform;

            var spots = new[]
            {
                new Vector2(-8f, -10.4f),     // the spawn
                new Vector2(-9.3f, -13.4f),   // between the fire and the log seat
                new Vector2(-9.3f, -15.9f),   // south of the log seat
            };
            float worst = float.MaxValue;
            foreach (Vector2 spot in spots)
            {
                Teleport(player, new Vector3(spot.x, GroundHeight(spot.x, spot.y) + 0.2f, spot.y));
                for (int step = 0; step < 8; step++)
                {
                    so.FindProperty("yaw").floatValue = step * 45f;
                    so.FindProperty("pitch").floatValue = 18f; // the default orbit height
                    so.ApplyModifiedPropertiesWithoutUndo();
                    yield return new WaitForSecondsRealtime(0.5f);
                    Vector3 pivot = player.position + new Vector3(0f, 1.4f, 0f);
                    float distance = Vector3.Distance(camera.transform.position, pivot);
                    worst = Mathf.Min(worst, distance);
                    Assert.That(Physics.CheckSphere(camera.transform.position, 0.15f, ~0, QueryTriggerInteraction.Ignore), Is.False,
                        $"the camera never ends up inside scenery at {spot} yaw {step * 45}");
                    Assert.That(distance, Is.GreaterThan(2.0f), $"the camp props never squeeze the camera at {spot} yaw {step * 45}");
                }
            }

            Debug.Log($"GreenMine camp camera: closest orbit distance {worst:0.00} m.");
        }
    }
}
#endif
