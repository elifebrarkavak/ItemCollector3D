// Part of the DISABLED Green Mine generator: see the header of GreenMineSceneBuilder.cs.
#if GREEN_MINE_GENERATOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ItemCollector3D.EditorTools.GreenMine
{
    /// <summary>
    /// Prop prefab factory (core). Every prop is ONE mesh asset (one sub-mesh per shared material) + one prefab under Assets/Prefabs/GreenMine/{category}.
    /// Re-running the builder overwrites the mesh and prefab in place, so GUIDs and scene references stay stable.
    /// </summary>
    internal static partial class GmProps
    {
        public const string PrefabRoot = "Assets/Prefabs/GreenMine";

        public static readonly Dictionary<string, GameObject> Prefabs = new();
        public static readonly Dictionary<string, int> Triangles = new();

        public static GameObject Get(string name)
        {
            if (!Prefabs.TryGetValue(name, out GameObject prefab) || prefab == null)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FindPath(name));
                Prefabs[name] = prefab;
            }

            return prefab;
        }

        private static string FindPath(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { PrefabRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                {
                    return path;
                }
            }

            return null;
        }

        // ================================================================ registration

        private static Material[] Resolve(IReadOnlyList<string> keys)
        {
            var mats = new Material[keys.Count];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = keys[i] == "glow" ? GmMats.Glow(GlowTexture) : GmMats.Get(keys[i]);
            }

            return mats;
        }

        private static Texture2D glowTexture;

        private static Texture2D GlowTexture
        {
            get
            {
                if (glowTexture == null)
                {
                    glowTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GmTerrain.TextureFolder + "/GreenMine_Glow.png");
                }

                return glowTexture;
            }
        }

        private static GameObject Reg(string category, string name, GmMesh gm, bool shadows = true, Action<GameObject, Mesh> setup = null)
        {
            Mesh mesh = gm.Bake($"{GmTerrain.MeshFolder}/{name}.asset");
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = Resolve(gm.Keys);
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            setup?.Invoke(go, mesh);

            string folder = $"{PrefabRoot}/{category}";
            Directory.CreateDirectory(folder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(go);
            Prefabs[name] = prefab;
            Triangles[name] = gm.TriangleCount;
            return prefab;
        }

        // ---- collider helpers
        private static Action<GameObject, Mesh> Capsule(Vector3 center, float radius, float height)
        {
            return (go, _) =>
            {
                var c = go.AddComponent<CapsuleCollider>();
                c.center = center;
                c.radius = radius;
                c.height = height;
            };
        }

        private static Action<GameObject, Mesh> BoxCol(Vector3 center, Vector3 size)
        {
            return (go, _) =>
            {
                var c = go.AddComponent<BoxCollider>();
                c.center = center;
                c.size = size;
            };
        }

        private static Action<GameObject, Mesh> SphereCol(Vector3 center, float radius)
        {
            return (go, _) =>
            {
                var c = go.AddComponent<SphereCollider>();
                c.center = center;
                c.radius = radius;
            };
        }

        private static Action<GameObject, Mesh> ConvexCol()
        {
            return (go, mesh) =>
            {
                var c = go.AddComponent<MeshCollider>();
                c.sharedMesh = mesh;
                c.convex = true;
            };
        }

        private static Action<GameObject, Mesh> Both(Action<GameObject, Mesh> a, Action<GameObject, Mesh> b)
        {
            return (go, mesh) =>
            {
                a(go, mesh);
                b(go, mesh);
            };
        }

        // ================================================================ build everything

        public static void BuildAll()
        {
            BuildNature();
            BuildMine();
            BuildCamp();
            BuildWaterAndFar();
        }

        // ================================================================ shared pieces

        /// <summary>A hanging mine lantern: iron frame, glowing glass, cap and a soft halo. Origin = the hook (top).</summary>
        private static void Lantern(GmMesh m, Vector3 top, float s = 1f, bool halo = true)
        {
            m.Push(top, Vector3.zero, Vector3.one * s);
            m.Prism("iron", new Vector3(0f, -0.03f, 0f), 0.07f, 0.07f, 0.06f, 6);                 // ring
            m.Prism("iron", new Vector3(0f, -0.10f, 0f), 0.19f, 0.03f, 0.10f, 4, twist: Mathf.PI / 4f); // cap
            m.Box("lanternGlass", new Vector3(0f, -0.34f, 0f), new Vector3(0.20f, 0.26f, 0.20f));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                m.Box("iron", new Vector3(sx * 0.11f, -0.34f, sz * 0.11f), new Vector3(0.035f, 0.32f, 0.035f));
            }

            m.Box("iron", new Vector3(0f, -0.52f, 0f), new Vector3(0.26f, 0.045f, 0.26f));
            m.Box("iron", new Vector3(0f, -0.16f, 0f), new Vector3(0.26f, 0.04f, 0.26f));
            if (halo)
            {
                Halo(m, new Vector3(0f, -0.34f, 0f), 0.8f);
            }

            m.Pop();
        }

        /// <summary>Soft additive glow: three crossed quads (no per-frame cost, no light).</summary>
        private static void Halo(GmMesh m, Vector3 centre, float size)
        {
            float h = size * 0.5f;
            m.Push(centre);
            m.Quad("glow", new Vector3(-h, -h, 0f), new Vector3(-h, h, 0f), new Vector3(h, h, 0f), new Vector3(h, -h, 0f));
            m.Quad("glow", new Vector3(h, -h, 0f), new Vector3(h, h, 0f), new Vector3(-h, h, 0f), new Vector3(-h, -h, 0f));
            m.Quad("glow", new Vector3(0f, -h, -h), new Vector3(0f, h, -h), new Vector3(0f, h, h), new Vector3(0f, -h, h));
            m.Quad("glow", new Vector3(0f, -h, h), new Vector3(0f, h, h), new Vector3(0f, h, -h), new Vector3(0f, -h, -h));
            m.Quad("glow", new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h));
            m.Pop();
        }

        /// <summary>A small crystal: hexagonal prism + pointed tip, leaning by <paramref name="tilt"/> degrees towards <paramref name="azimuth"/>.</summary>
        private static void Crystal(GmMesh m, Vector3 basePos, float height, float radius, float tilt, float azimuth, string body = "crystal", string tip = "crystalLight")
        {
            m.Push(basePos, new Vector3(tilt, azimuth, 0f));
            m.Prism(body, Vector3.zero, radius * 0.96f, radius, height * 0.62f, 6, caps: false);
            m.Prism(tip, new Vector3(0f, height * 0.62f, 0f), radius, 0f, height * 0.38f, 6);
            m.Pop();
        }
    }
}
#endif
