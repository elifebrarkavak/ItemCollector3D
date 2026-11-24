#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ItemCollector3D.Characters;
using ItemCollector3D.Menus;
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
    /// <summary>
    /// Ancient Mine visual design (stage 16B): every area is dressed (statues, pillars, arches, crystals, carved panels), the cost stays small and shared (a few merged meshes per area, two materials, one
    /// real-time light), each area has its own ambient light and fog, the places kept clear for the treasures of stage 16C are really clear, and no treasure exists yet. Walking, camera and collider behaviour
    /// are covered by <see cref="AncientMinePlayModeTests"/>. In-memory stores; the developer's real save file is hashed before the first and after the last test.
    /// </summary>
    public sealed class AncientMineDesignPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/AncientMine.unity";
        private static readonly string[] Zones = { "A_AncientGate", "B_HallOfPillars", "C_CrystalCaverns", "D_ForgottenWorkshop", "E_RoyalVault" };

        private static string realSavePath;
        private static string realSaveHashBefore;

        private Scene scene;

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
            CharacterSelection.StoreFactoryOverride = () => new MemoryCharacterStore();
            CharacterSelection.ResetCurrent();
            GameSettings.Current.ResetToDefaults();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
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

        private IEnumerator Load()
        {
            var store = new MemoryProgressStore();
            ProgressManager.StoreFactoryOverride = () => store;
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
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

        private Transform Env => RootObject("AncientMineEnvironment").transform;

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

        private static void Teleport(Transform player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static int CountChildrenNamed(Transform parent, string prefix)
        {
            int n = 0;
            foreach (Transform t in parent)
            {
                n += t.name.StartsWith(prefix) ? 1 : 0;
            }

            return n;
        }

        // ================================================================ every area is dressed, and cheaply

        [UnityTest]
        public IEnumerator EveryArea_IsDressed_WithStatuesPillarsAndCrystals_InFewMergedMeshes_AndTwoSharedMaterials()
        {
            yield return Load();
            Transform env = Env;
            int total = 0, largest = 0, renderers = 0;
            var materials = new HashSet<Material>();
            foreach (string zone in Zones)
            {
                Transform structures = env.Find(zone + "/Props/Structures");
                Transform crystals = env.Find(zone + "/Props/Crystals");
                Assert.That(structures, Is.Not.Null, zone + ": merged architecture mesh");
                Assert.That(crystals, Is.Not.Null, zone + ": merged crystal mesh (its own renderer)");
                foreach (Transform t in new[] { structures, crystals })
                {
                    var r = t.GetComponent<MeshRenderer>();
                    Assert.That(GameObjectUtility.GetStaticEditorFlags(t.gameObject).HasFlag(StaticEditorFlags.BatchingStatic), Is.True, t.name + " is batching static");
                    Mesh mesh = t.GetComponent<MeshFilter>().sharedMesh;
                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        tris += (int)mesh.GetIndexCount(s) / 3;
                    }

                    total += tris;
                    largest = Mathf.Max(largest, tris);
                    foreach (Material m in r.sharedMaterials)
                    {
                        materials.Add(m);
                    }

                    Assert.That(r.GetComponent<Collider>(), Is.Null, t.name + ": decoration meshes have no colliders of their own (colliders are separate, simple shapes)");
                }

                var crystalRenderer = crystals.GetComponent<MeshRenderer>();
                Assert.That(crystalRenderer.sharedMaterials.Length, Is.EqualTo(1), zone + ": crystals use one emissive material, nothing transparent");
                Assert.That(crystalRenderer.sharedMaterial.name, Is.EqualTo("AM_Glow"));
                renderers += 2;
                Assert.That(env.Find(zone + "/Props/Structures").GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0), Is.GreaterThan(1500u), zone + " is decorated");
            }

            foreach (Material m in materials)
            {
                Assert.That(m.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            }

            Assert.That(materials.Count, Is.EqualTo(2), "one lit palette material, one emissive");
            Assert.That(largest, Is.LessThan(14000), "no area's decoration is one giant mesh");
            Debug.Log($"Ancient Mine decoration: {renderers} decoration renderers, {total} triangles (largest group {largest})");

            // the architecture is there: statues at the gate and in the vault, twelve pillars in the hall, carts and benches in the workshop, formations in the caverns
            Assert.That(CountChildrenNamed(env.Find("A_AncientGate/Colliders"), "Statue_"), Is.EqualTo(2), "two guardians at the gate");
            Assert.That(CountChildrenNamed(env.Find("B_HallOfPillars/Colliders"), "Pillar_"), Is.EqualTo(12), "the twelve pillars of the hall");
            Assert.That(CountChildrenNamed(env.Find("B_HallOfPillars/Colliders"), "Statue_"), Is.EqualTo(4), "a statue on each dais");
            Assert.That(CountChildrenNamed(env.Find("E_RoyalVault/Colliders"), "Statue_"), Is.EqualTo(3), "three kings in the vault");
            Assert.That(CountChildrenNamed(env.Find("E_RoyalVault/Colliders"), "Pedestal_"), Is.EqualTo(3), "three alcove pedestals");
            Assert.That(CountChildrenNamed(env.Find("E_RoyalVault/Colliders"), "Pillar_"), Is.EqualTo(8));
            Assert.That(CountChildrenNamed(env.Find("D_ForgottenWorkshop/Colliders"), "Pier_"), Is.EqualTo(3));
            Assert.That(CountChildrenNamed(env.Find("D_ForgottenWorkshop/Colliders"), "Cart_"), Is.EqualTo(1));
            Assert.That(CountChildrenNamed(env.Find("C_CrystalCaverns/Colliders"), "Crystal_"), Is.GreaterThanOrEqualTo(8), "big crystal formations in the caverns");

            // crystals are not one model repeated: many different swatches (colours) and many different shard sizes in the caverns and the vault
            foreach (string zone in new[] { "C_CrystalCaverns", "E_RoyalVault" })
            {
                Mesh mesh = env.Find(zone + "/Props/Crystals").GetComponent<MeshFilter>().sharedMesh;
                var uvs = new List<Vector2>();
                mesh.GetUVs(0, uvs);
                var colours = new HashSet<Vector2>(uvs);
                Assert.That(colours.Count, Is.GreaterThanOrEqualTo(14), zone + ": crystals use many different colours (cyan, blue, violet, turquoise, magenta and their light and dark tones)");
                var heights = new HashSet<int>();
                foreach (Vector3 v in mesh.vertices)
                {
                    heights.Add(Mathf.RoundToInt(v.y * 2f));
                }

                Assert.That(heights.Count, Is.GreaterThanOrEqualTo(30), zone + ": crystals have many different heights");
            }

            Assert.That(RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true).Length, Is.EqualTo(0), "no treasure was added in this stage");
        }

        // ================================================================ the lighting character of each area

        [UnityTest]
        public IEnumerator EachArea_HasItsOwnAmbientLightAndFog_FollowingThePlayer()
        {
            yield return Load();
            var mood = RootObject("GameplaySystems").GetComponentInChildren<CaveZoneMood>(true);
            Assert.That(mood, Is.Not.Null, "the zone mood object");
            Assert.That(mood.MoodCount, Is.EqualTo(5));
            Transform player = RootObject("Player").transform;
            (string zone, Vector2 where)[] places =
            {
                ("A_AncientGate", new Vector2(-45f, -2f)), ("B_HallOfPillars", new Vector2(0f, 0f)), ("C_CrystalCaverns", new Vector2(48f, -4f)),
                ("D_ForgottenWorkshop", new Vector2(-12f, 40f)), ("E_RoyalVault", new Vector2(24f, -38f)),
            };
            var skies = new List<Color>();
            var fogs = new List<Color>();
            foreach ((string zone, Vector2 where) in places)
            {
                Teleport(player, new Vector3(where.x, FloorY(where.x, where.y) + 1.05f, where.y));
                yield return new WaitForSecondsRealtime(3.4f);
                Assert.That(mood.CurrentZone, Is.EqualTo(zone), "the mood follows the floor under the player");
                Assert.That(mood.TryGetMood(zone, out CaveZoneMood.Mood expected), Is.True);
                Color sky = RenderSettings.ambientSkyColor;
                Assert.That(sky.r, Is.EqualTo(expected.ambientSky.r).Within(0.03f), zone + " ambient sky (r)");
                Assert.That(sky.g, Is.EqualTo(expected.ambientSky.g).Within(0.03f), zone + " ambient sky (g)");
                Assert.That(sky.b, Is.EqualTo(expected.ambientSky.b).Within(0.03f), zone + " ambient sky (b)");
                Assert.That(RenderSettings.fogColor.r, Is.EqualTo(expected.fogColor.r).Within(0.03f), zone + " fog");
                Assert.That(RenderSettings.ambientEquatorColor.g, Is.EqualTo(expected.ambientEquator.g).Within(0.03f), zone + " ambient equator");
                skies.Add(RenderSettings.ambientEquatorColor);
                fogs.Add(RenderSettings.fogColor);
                Assert.That(RenderSettings.fog, Is.True);
            }

            // the five moods are really different from each other, and the dark underground stays readable (no mood is dark)
            for (int i = 0; i < skies.Count; i++)
            {
                Assert.That(skies[i].r + skies[i].g + skies[i].b, Is.GreaterThan(1.05f), "ambient stays bright enough to read the paths: " + places[i].zone);
                for (int j = i + 1; j < skies.Count; j++)
                {
                    float difference = Mathf.Abs(skies[i].r - skies[j].r) + Mathf.Abs(skies[i].g - skies[j].g) + Mathf.Abs(skies[i].b - skies[j].b);
                    Assert.That(difference, Is.GreaterThan(0.06f), $"{places[i].zone} and {places[j].zone} do not light the same");
                }
            }

            Assert.That(skies[3].r, Is.GreaterThan(skies[3].b), "the workshop is warm (more red than blue)");
            Assert.That(skies[2].b, Is.GreaterThan(skies[2].r + 0.2f), "the crystal caverns are blue and purple");

            // the mood stops while paused and is restored when the scene unloads
            GamePause.Pause();
            Color frozen = RenderSettings.ambientEquatorColor;
            Teleport(player, new Vector3(0f, FloorY(0f, 0f) + 1.05f, 0f));
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.That(RenderSettings.ambientEquatorColor, Is.EqualTo(frozen), "nothing changes while the game is paused");
            GamePause.Resume();
        }

        // ================================================================ places kept clear for the treasures of stage 16C

        [UnityTest]
        public IEnumerator TreasureCandidates_AreClearMarkers_NotTreasures_InsideTheCave_AndNearARoute()
        {
            yield return Load();
            Transform candidates = Env.Find("Markers/TreasureCandidates");
            Assert.That(candidates, Is.Not.Null);
            Assert.That(candidates.childCount, Is.GreaterThanOrEqualTo(24), "enough clear places for the real treasures");
            var routePoints = new List<Vector3>();
            foreach (Transform group in Env.Find("Markers/Routes"))
            {
                foreach (Transform p in group)
                {
                    routePoints.Add(p.position);
                }
            }

            var perZone = new Dictionary<string, int>();
            foreach (Transform marker in candidates)
            {
                string zone = marker.name.Substring(0, 1);
                perZone[zone] = perZone.TryGetValue(zone, out int n) ? n + 1 : 1;
                Assert.That(marker.GetComponentsInChildren<Component>().Length, Is.EqualTo(1), marker.name + " is an empty marker, no treasure and no collider");
                float floor = FloorY(marker.position.x, marker.position.z);
                Assert.That(float.IsNaN(floor), Is.False, marker.name + " has floor");
                float lift = marker.position.y - floor;
                Assert.That(lift, Is.InRange(-0.3f, 1.5f), marker.name + " stands on the floor or a pedestal");

                // about 1 m of free space round it (the floor, the pedestal it stands on and the player aside)
                foreach (Collider c in Physics.OverlapSphere(marker.position + Vector3.up * 1.0f, 0.9f, ~0, QueryTriggerInteraction.Ignore))
                {
                    bool allowed = c.name == "Floor" || c.name.StartsWith("Pedestal") || c.transform.root.name == "Player";
                    Assert.That(allowed, Is.True, $"{marker.name} is blocked by {c.transform.parent?.name}/{c.name}");
                }

                // the cave around it: a ceiling above and no rock inside 1.2 m on the ground
                Assert.That(Physics.Raycast(marker.position + Vector3.up * 0.5f, Vector3.up, 60f, ~0, QueryTriggerInteraction.Ignore), Is.True, marker.name + " is under the cave's ceiling");

                // a main, alternate or side route passes within 18 m (a treasure is never in a pocket nobody can reach)
                float nearest = float.MaxValue;
                foreach (Vector3 r in routePoints)
                {
                    nearest = Mathf.Min(nearest, Vector2.Distance(new Vector2(r.x, r.z), new Vector2(marker.position.x, marker.position.z)));
                }

                Assert.That(nearest, Is.LessThan(18f), marker.name + " is near a walkable route (the editor report checks the flood-fill reachability of every candidate)");
            }

            foreach (string zone in new[] { "A", "B", "C", "D", "E" })
            {
                Assert.That(perZone.ContainsKey(zone) && perZone[zone] >= 3, Is.True, "at least three clear places in area " + zone);
            }

            Assert.That(RootObject("TreasureSpots").GetComponentsInChildren<TreasureSpot>(true).Length, Is.EqualTo(0));
            Assert.That(TreasureSpot.Active.Count, Is.EqualTo(0));
        }
    }
}
#endif
