#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>
    /// Stage 15B: the visual design of the five Rocky Canyon areas. It works MODULARLY on the zone roots that already exist in RockyCanyon.unity (A_DesertOutpost ... E_HiddenRavine,
    /// Backdrop, Lighting): <see cref="DressZone"/> clears and refills only that zone's "Props" (merged render meshes) and "Colliders" (a few plain boxes / spheres) children, the
    /// Player, camera, HUD, detector, treasure root and the terrain object are never touched. All props use the one palette material (and the one emissive material for veins, fire and
    /// lantern glass), so the whole environment is 3 materials and a few dozen draw calls. Everything is deterministic (seeded) and positioned on <see cref="RcLayout.Surface"/>.
    /// </summary>
    public static partial class RcDressing
    {
        public const string EnvironmentName = "RockyCanyonEnvironment";

        public static readonly string[] ZoneNames = { "A_DesertOutpost", "B_DryRiverbed", "C_OldRailway", "D_CanyonOverlook", "E_HiddenRavine" };

        private static Material paletteMat, glowMat;
        private static System.Random rng;
        private static readonly List<Vector3> Taken = new(); // x = world x, y = radius, z = world z: places the hand-placed props already use, scattered nature keeps off them

        // ================================================================ entry points

        /// <summary>Refresh the terrain mesh / collider / colour map from <see cref="RcLayout"/> and dress every zone, the backdrop and the lighting of the OPEN Rocky Canyon scene. Does not save.</summary>
        public static string DressAll(bool refreshTerrain = true)
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform env = Environment(scene);
            if (env == null)
            {
                return "RockyCanyonEnvironment not found in the active scene.";
            }

            var report = new System.Text.StringBuilder();
            Prepare();
            if (refreshTerrain)
            {
                RefreshTerrain(env);
            }

            Taken.Clear();
            foreach (string zone in ZoneNames)
            {
                report.AppendLine(DressZone(env, zone));
            }

            report.AppendLine(DressBackdrop(env));
            report.AppendLine(RemoveDuplicateTreasureGroups(scene));
            ApplyAtmosphere(env);
            EditorSceneManager.MarkSceneDirty(scene);
            return report.ToString();
        }

        /// <summary>The 15A scene was built more than once and kept 3 copies of every (empty) treasure group and of the inactive test points: keep the first of each name so 15C starts from clean groups.</summary>
        private static string RemoveDuplicateTreasureGroups(Scene scene)
        {
            GameObject root = null;
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name == "TreasureSpots")
                {
                    root = r;
                }
            }

            if (root == null)
            {
                return "TreasureSpots root not found";
            }

            var seen = new HashSet<string>();
            int removed = 0;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                if (!seen.Add(child.name))
                {
                    Object.DestroyImmediate(child.gameObject);
                    i--;
                    removed++;
                }
            }

            return $"TreasureSpots: {root.transform.childCount} groups, {removed} duplicates removed";
        }

        public static Transform Environment(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == EnvironmentName)
                {
                    return root.transform;
                }
            }

            return null;
        }

        /// <summary>The terrain mesh asset is refilled IN PLACE (the scene objects keep pointing at it), the collider is re-baked and the colour map repainted.</summary>
        public static void RefreshTerrain(Transform env)
        {
            RcLayout.ResetGrid();
            Mesh mesh = RcAssets.BuildTerrainMesh();
            RcAssets.PaintColorMap();
            Transform terrain = env.Find("Terrain");
            var collider = terrain.GetComponent<MeshCollider>();
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
            terrain.GetComponent<MeshFilter>().sharedMesh = mesh;
            AssetDatabase.SaveAssets();
        }

        /// <summary>The player starts at the outpost looking along the first leg of the riverbed (about 68 degrees), so the bridge's towers are in the left part of the first view.</summary>
        public static string ApplySpawnView(float yaw = 68f)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Player")
                {
                    player = root;
                }
            }

            if (player == null)
            {
                return "Player not found";
            }

            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var camera = Object.FindFirstObjectByType<ItemCollector3D.Player.ThirdPersonCameraController>();
            var so = new SerializedObject(camera);
            so.FindProperty("yaw").floatValue = yaw;
            so.ApplyModifiedPropertiesWithoutUndo();
            Vector3 pivot = player.transform.position + new Vector3(0f, 1.4f, 0f);
            camera.transform.rotation = Quaternion.Euler(20f, yaw, 0f);
            camera.transform.position = pivot + camera.transform.rotation * Vector3.back * 5f;
            EditorSceneManager.MarkSceneDirty(scene);
            return "spawn view yaw " + yaw;
        }

        private static void Prepare()
        {
            RcLayout.ResetGrid();
            Texture2D palette = RcPalette.Build();
            paletteMat = RcAssets.PaletteMaterial(palette);
            glowMat = RcAssets.GlowMaterial(palette);
            AssetDatabase.SaveAssets();
        }

        public static string DressZone(Transform env, string zone)
        {
            if (paletteMat == null)
            {
                Prepare();
            }

            Transform root = env.Find(zone);
            if (root == null)
            {
                return "zone root not found: " + zone;
            }

            Transform props = Fresh(root, "Props");
            Transform cols = Fresh(root, "Colliders");
            int zoneIndex = System.Array.IndexOf(ZoneNames, zone);
            rng = new System.Random(1000 + zoneIndex * 77);
            switch (zoneIndex)
            {
                case 0: Outpost(props, cols); break;
                case 1: Riverbed(props, cols); break;
                case 2: Railway(props, cols); break;
                case 3: Overlook(props, cols); break;
                case 4: Ravine(props, cols); break;
            }

            int tris = 0, renderers = 0;
            foreach (MeshFilter f in props.GetComponentsInChildren<MeshFilter>(true))
            {
                renderers++;
                tris += f.sharedMesh.triangles.Length / 3;
            }

            return $"{zone}: {renderers} renderers, {tris} triangles, {cols.GetComponentsInChildren<Collider>(true).Length} colliders";
        }

        private static Transform Fresh(Transform zone, string name)
        {
            Transform old = zone.Find(name);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var go = new GameObject(name);
            go.transform.SetParent(zone, false);
            return go.transform;
        }

        // ================================================================ placing meshes and colliders

        private static void Emit(Transform parent, string name, RcMesh rm)
        {
            if (rm.IsEmpty)
            {
                return;
            }

            Mesh mesh = rm.ToMesh("RC_" + parent.parent.name + "_" + name);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = rm.HasGlow ? new[] { paletteMat, glowMat } : new[] { paletteMat };
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>
        /// Structures the miner walks past (shacks, towers, posts, the crane, the flag pole ...) block only their lowest 2 m: the orbiting camera's ray starts 2.45 m above the feet, so a taller
        /// collider next to a path would squeeze or swallow the camera (found by the stage 15C walking tour). Their meshes keep their full height; the colliders just stop at 2 m.
        /// </summary>
        private static readonly HashSet<string> LowColliders = new()
        {
            "MinerShack", "SupplyShed", "SignPostL", "SignPostR", "HeadframeLegs", "MineEntrance", "CraneMast", "SignalPost", "FlagPole", "Saguaro",
        };

        public const float LowColliderHeight = 2.0f;

        private static void Box(Transform cols, string name, Vector3 centre, Vector3 size, float yaw = 0f, float pitch = 0f, float roll = 0f)
        {
            if (LowColliders.Contains(name) && pitch == 0f && roll == 0f && size.y > LowColliderHeight)
            {
                float bottom = centre.y - size.y * 0.5f;
                size.y = LowColliderHeight;
                centre.y = bottom + LowColliderHeight * 0.5f;
            }

            var go = new GameObject(name);
            go.transform.SetParent(cols, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(pitch, yaw, roll));
            go.AddComponent<BoxCollider>().size = size;
        }

        private static void Ball(Transform cols, string name, Vector3 centre, float radius)
        {
            // like the boxes above: a sphere never reaches higher than 2 m over the ground it stands on (the bottom stays where it was)
            float over = centre.y + radius - G(centre.x, centre.z) - LowColliderHeight;
            if (over > 0f && radius - over * 0.5f > 0.5f)
            {
                radius -= over * 0.5f;
                centre.y -= over * 0.5f;
            }

            var go = new GameObject(name);
            go.transform.SetParent(cols, false);
            go.transform.position = centre;
            go.AddComponent<SphereCollider>().radius = radius;
        }

        // ================================================================ small helpers

        private static float G(float x, float z) => RcLayout.Surface(x, z);

        private static Vector3 At(float x, float z, float lift = 0f) => new(x, RcLayout.Surface(x, z) + lift, z);

        private static float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static int Seed() => rng.Next(1, 100000);

        private static float YawToward(float fromX, float fromZ, float toX, float toZ) => Mathf.Atan2(toX - fromX, toZ - fromZ) * Mathf.Rad2Deg;

        private static void Take(float x, float z, float radius) => Taken.Add(new Vector3(x, radius, z));

        private static bool IsFree(float x, float z, float radius)
        {
            foreach (Vector3 t in Taken)
            {
                float d = radius + t.y;
                if ((t.x - x) * (t.x - x) + (t.z - z) * (t.z - z) < d * d)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The gradient of the terrain (pointing uphill) at (x, z); a wall's outward normal is its negative.</summary>
        private static Vector2 Uphill(float x, float z)
        {
            return new Vector2(G(x + 0.6f, z) - G(x - 0.6f, z), G(x, z + 0.6f) - G(x, z - 0.6f)).normalized;
        }

        private static Vector3 Normal(float x, float z)
        {
            float dx = G(x + 0.5f, z) - G(x - 0.5f, z), dz = G(x, z + 0.5f) - G(x, z - 0.5f);
            return new Vector3(-dx, 1f, -dz).normalized;
        }

        private static readonly P[] RockMain = { P.RockRed2, P.RockOrange, P.RockRed, P.RockOrange2, P.RockSand };
        private static readonly P[] RockAlt = { P.RockRed, P.RockOrange2, P.RockRed2, P.RockSand, P.RockOrange };

        /// <summary>A natural rock of a given size class: colours differ per rock (controlled variety), the top face is always lit sandstone.</summary>
        private static void NaturalRock(RcMesh rm, Vector3 position, float size, int pick, bool bigDetail = false)
        {
            int i = Mathf.Abs(pick) % RockMain.Length;
            P top = i % 2 == 0 ? P.RockSand : P.RockCream;
            float yaw = R(0f, 360f);
            rm.Push(position, yaw);
            rm.Rock(new Vector3(0f, size * 0.30f, 0f), new Vector3(size * R(0.9f, 1.25f), size * R(0.55f, 0.8f), size * R(0.9f, 1.25f)), Seed(), RockMain[i], RockAlt[i], top, P.RockDeep, bigDetail ? 1 : 0, 0.22f, 0.25f);
            rm.Pop();
        }

        /// <summary>
        /// Scatters natural clusters (one big rock, a few mid and small ones, sometimes a plant) over the floor of one zone between two edge distances. Rocks above
        /// <paramref name="colliderFrom"/> m get a sphere collider. Returns the number of clusters made.
        /// </summary>
        private static int RockClusters(RcMesh rm, Transform cols, string label, RcZone[] zones, int clusters, float minEdge, float maxEdge, float spacing, float bigMin, float bigMax, float colliderFrom, System.Func<float, float, bool> allowed = null)
        {
            var centres = new List<Vector2>();
            int made = 0;
            for (int attempt = 0; attempt < 6000 && made < clusters; attempt++)
            {
                float x = R(-RcLayout.HalfX + 8f, RcLayout.HalfX - 8f), z = R(-RcLayout.HalfZ + 8f, RcLayout.HalfZ - 8f);
                if (System.Array.IndexOf(zones, RcLayout.ZoneAt(x, z)) < 0)
                {
                    continue;
                }

                float edge = RcLayout.EdgeDistance(x, z);
                if (edge < minEdge || edge > maxEdge || RcLayout.Slope(x, z) > 0.25f || !IsFree(x, z, 1.8f) || (allowed != null && !allowed(x, z)))
                {
                    continue;
                }

                bool clash = false;
                foreach (Vector2 c in centres)
                {
                    clash |= (c - new Vector2(x, z)).sqrMagnitude < spacing * spacing;
                }

                if (clash)
                {
                    continue;
                }

                centres.Add(new Vector2(x, z));
                float big = R(bigMin, bigMax);
                NaturalRock(rm, At(x, z, -0.12f * big), big, Seed(), big > 1.6f);
                if (big >= colliderFrom)
                {
                    Ball(cols, label + "_Rock" + made, At(x, z, big * 0.30f), big * 0.52f);
                }

                int extra = rng.Next(2, 5);
                for (int k = 0; k < extra; k++)
                {
                    float a = R(0f, Mathf.PI * 2f), d = R(big * 0.75f, big * 1.6f), s = R(0.3f, 0.75f) * big * 0.55f;
                    float ex = x + Mathf.Cos(a) * d, ez = z + Mathf.Sin(a) * d;
                    if (RcLayout.EdgeDistance(ex, ez) > -0.3f)
                    {
                        continue;
                    }

                    NaturalRock(rm, At(ex, ez, -0.05f), Mathf.Max(0.28f, s), Seed());
                }

                made++;
            }

            return made;
        }

        // ================================================================ lighting and atmosphere

        /// <summary>Sun, ambient and fog of the canyon: a warm, dry, clear day. Directional light only (no other realtime lights except the pocket's glow), no post-processing.</summary>
        private static void ApplyAtmosphere(Transform env)
        {
            RcAssets.PaintSky();
            Transform sun = env.Find("Lighting/Sun");
            if (sun != null)
            {
                // the light travels towards NE-ish: the sun stands low in the south-west behind the player's first view (up the riverbed), so ledges are lit, the strata glow and the shade falls on the far side
                sun.rotation = Quaternion.Euler(44f, 52f, 0f);
                var light = sun.GetComponent<Light>();
                light.color = new Color(1f, 0.93f, 0.80f);
                light.intensity = 1.25f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.62f;
                RenderSettings.sun = light;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.72f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.90f, 0.77f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.50f, 0.37f, 0.29f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0062f;
            RenderSettings.fogColor = new Color(0.94f, 0.83f, 0.70f);
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(RcAssets.MaterialFolder + "/RC_Sky.mat");
        }
    }
}
#endif
