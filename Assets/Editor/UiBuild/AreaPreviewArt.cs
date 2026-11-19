using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static ItemCollector3D.EditorTools.UiPalette;
using Object = UnityEngine.Object;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// Renders the three preview pictures of the Area Selection cards (Assets/UI/Areas): a small low-poly diorama per area (built from generated
    /// meshes in a throw-away scene, rendered at 2x and filtered down, rounded corners and a dark outline baked in). Same look as the menu cave:
    /// flat shaded URP Lit, warm key light, glowing crystals. Nothing here is kept in a scene; only the PNGs are assets.
    /// </summary>
    public static class AreaPreviewArt
    {
        public const string Folder = "Assets/UI/Areas";
        public const int Width = 1024;
        public const int Height = 700;
        private const int Supersample = 2;

        public static string SpritePath(string areaId) => $"{Folder}/area_{areaId}.png";

        public static void BuildAll()
        {
            BuildOne("green_mine");
            BuildOne("rocky_canyon");
            BuildOne("ancient_mine");
            AssetDatabase.Refresh();
        }

        public static void BuildOne(string areaId)
        {
            Directory.CreateDirectory(Folder);
            Composer composer = areaId switch
            {
                "green_mine" => ComposeGreenMine,
                "rocky_canyon" => ComposeRockyCanyon,
                "ancient_mine" => ComposeAncientMine,
                _ => throw new ArgumentException("Unknown area " + areaId)
            };

            Texture2D raw = Render(composer);
            Texture2D final = Finish(raw);
            File.WriteAllBytes(SpritePath(areaId), final.EncodeToPNG());
            Object.DestroyImmediate(raw);
            Object.DestroyImmediate(final);
            AssetDatabase.ImportAsset(SpritePath(areaId), ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath(areaId));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 75;
            importer.maxTextureSize = 1024;
            importer.spriteBorder = Vector4.zero;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        // ================================================================ rendering

        private delegate void Composer(Scene scene, Transform root, Camera camera, Light sun, Builder b);

        private static Texture2D Render(Composer composer)
        {
            // other open scenes must not leak into the picture (their lights and meshes): switch them off while rendering
            var switchedOff = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                foreach (GameObject root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                {
                    if (root.activeSelf)
                    {
                        root.SetActive(false);
                        switchedOff.Add(root);
                    }
                }
            }

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var builder = new Builder();
            RenderTexture target = null;
            try
            {
                Transform root = new GameObject("Diorama").transform;
                var camGo = new GameObject("Cam");
                Camera camera = camGo.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Hex("#202030");
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 120f;
                camera.allowHDR = true;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

                var sunGo = new GameObject("Sun");
                Light sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;

                RenderSettings.skybox = null;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.ambientMode = AmbientMode.Trilight;

                composer(scene, root, camera, sun, builder);

                // soft bloom: crystals and lanterns glow
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var bloom = profile.Add<Bloom>();
                bloom.threshold.Override(0.95f);
                bloom.intensity.Override(builder.BloomIntensity);
                bloom.scatter.Override(0.65f);
                var volumeGo = new GameObject("Volume");
                var volume = volumeGo.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;

                target = new RenderTexture(Width * Supersample, Height * Supersample, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                camera.targetTexture = target;
                camera.aspect = Width / (float)Height;
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, false);
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                RenderTexture.active = null;
                Object.DestroyImmediate(profile);
                return texture;
            }
            finally
            {
                RenderTexture.active = null;
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }

                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
                builder.Dispose();
                foreach (GameObject root in switchedOff)
                {
                    if (root != null) { root.SetActive(true); }
                }
            }
        }

        /// <summary>GLSL style smoothstep: 0 at edge0, 1 at edge1 (edge0 may be larger than edge1).</summary>
        private static float Edge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Filters the 2x render down, rounds the corners, adds a soft vignette and the dark outline of the card art.</summary>
        private static Texture2D Finish(Texture2D raw)
        {
            var result = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
            Color[] source = raw.GetPixels();
            var pixels = new Color[Width * Height];
            Raster.Sdf outer = Raster.RoundRectBox(0, 0, Width, Height, 50f);
            Raster.Sdf inner = Raster.RoundRectBox(9, 9, Width - 9, Height - 9, 41f);
            Raster.Sdf rim = Raster.RoundRectBox(11, 11, Width - 11, Height - 11, 39f);
            int sw = raw.width;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Color c = Color.clear;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            c += source[(y * Supersample + sy) * sw + x * Supersample + sx];
                        }
                    }

                    c /= Supersample * Supersample;
                    c.a = 1f;

                    float u = (x + 0.5f) / Width, v = (y + 0.5f) / Height; // v: 0 = bottom of the picture
                    float edge = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) * 1.3f + (v - 0.5f) * (v - 0.5f));
                    float vignette = 1f - 0.24f * Edge(0.36f, 0.8f, edge);
                    float bottom = 1f - 0.2f * Edge(0.3f, 0f, v);
                    c.r *= vignette * bottom;
                    c.g *= vignette * bottom;
                    c.b *= vignette * bottom;

                    float py = y + 0.5f, px = x + 0.5f;
                    float yTop = Height - py; // image space for the rounded boxes (y down)
                    float dOuter = outer(px, yTop);
                    float dInner = inner(px, yTop);
                    float dRim = rim(px, yTop);
                    float alpha = Mathf.Clamp01(0.5f - dOuter);
                    Color outline = Outline;
                    float insideBody = Mathf.Clamp01(0.5f - dInner);
                    Color shown = Color.Lerp(outline, c, insideBody);
                    // a thin cream highlight just inside the outline, strongest at the top edge
                    float rimLine = Mathf.Clamp01(0.5f - Mathf.Abs(dRim + 1f)) * Mathf.Lerp(0.14f, 0.42f, Mathf.Clamp01((v - 0.4f) / 0.6f));
                    shown = Color.Lerp(shown, Cream, rimLine);
                    shown.a = alpha;
                    pixels[y * Width + x] = shown;
                }
            }

            result.SetPixels(pixels);
            result.Apply();
            return result;
        }

        // ================================================================ builder helpers

        private sealed class Builder : IDisposable
        {
            private readonly List<Object> owned = new();
            private readonly System.Random random = new(7);

            public float BloomIntensity = 0.55f;
            public float Saturation = 10f;

            public static Mesh Cube => Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            public static Mesh Sphere => Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            public static Mesh Cylinder => Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

            public float Rand() => (float)random.NextDouble();
            public float Rand(float min, float max) => min + Rand() * (max - min);

            public Material Lit(Color color, float smoothness = 0.12f, Color? emission = null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.HideAndDontSave };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", 0f);
                if (emission.HasValue)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission.Value);
                }

                owned.Add(material);
                return material;
            }

            public Material Unlit(Color color)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
                material.SetColor("_BaseColor", color);
                owned.Add(material);
                return material;
            }

            public Mesh Own(Mesh mesh)
            {
                mesh.hideFlags = HideFlags.HideAndDontSave;
                owned.Add(mesh);
                return mesh;
            }

            public GameObject Obj(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, Vector3? euler = null, bool shadows = true)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = position;
                go.transform.localScale = scale;
                go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = shadows;
                return go;
            }

            public GameObject Glow(Transform parent, Vector3 position, float size, Color color)
            {
                var go = new GameObject("Glow");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = position;
                go.transform.localScale = Vector3.one * size;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = UiSpriteFactory.Load("ui_glow");
                sr.color = color;
                sr.shadowCastingMode = ShadowCastingMode.Off;
                sr.receiveShadows = false;
                return go;
            }

            public Light Point(Transform parent, Vector3 position, Color color, float intensity, float range)
            {
                var go = new GameObject("Light");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = position;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.intensity = intensity;
                light.range = range;
                light.shadows = LightShadows.None;
                return light;
            }

            /// <summary>A full-frame vertical colour gradient hung in front of the camera, far away (the sky / cave backdrop).</summary>
            public void Backdrop(Camera camera, Color top, Color middle, Color bottom, float distance = 40f)
            {
                var texture = new Texture2D(2, 128, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < 128; y++)
                {
                    float t = y / 127f;
                    Color c = t < 0.5f ? Color.Lerp(bottom, middle, t * 2f) : Color.Lerp(middle, top, (t - 0.5f) * 2f);
                    texture.SetPixel(0, y, c);
                    texture.SetPixel(1, y, c);
                }

                texture.Apply();
                owned.Add(texture);
                Material material = Unlit(Color.white);
                material.SetTexture("_BaseMap", texture);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = 1000; // drawn first, behind everything whatever its distance
                Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                float height = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2.4f;
                GameObject go = Obj(camera.transform, "Backdrop", quad, material, new Vector3(0f, 0f, distance), new Vector3(height * camera.aspect, height, 1f), null, false);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            public void Dispose()
            {
                foreach (Object item in owned)
                {
                    if (item != null) { Object.DestroyImmediate(item); }
                }

                owned.Clear();
            }

            // ------------------------------------------------------------ generated low-poly meshes (flat shaded, in memory)

            private static void Tri(List<Vector3> list, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
                {
                    (b, c) = (c, b);
                }

                list.Add(a);
                list.Add(b);
                list.Add(c);
            }

            private Mesh Finish(List<Vector3> tris)
            {
                var mesh = new Mesh();
                var indices = new int[tris.Count];
                for (int i = 0; i < indices.Length; i++) { indices[i] = i; }
                mesh.SetVertices(tris);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return Own(mesh);
            }

            /// <summary>A faceted column / mesa layer / cone: a ring of 'sides' at the bottom (y = 0) and one at the top, jittered, with a top cap.</summary>
            public Mesh Prism(int sides, float bottomRadius, float topRadius, float height, float jitter = 0.08f, float heightJitter = 0.04f)
            {
                var tris = new List<Vector3>();
                var bottom = new Vector3[sides];
                var top = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = (i + Rand(-0.12f, 0.12f)) / sides * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    bottom[i] = new Vector3(c * bottomRadius * (1f + Rand(-jitter, jitter)), 0f, s * bottomRadius * (1f + Rand(-jitter, jitter)));
                    top[i] = new Vector3(c * topRadius * (1f + Rand(-jitter, jitter)), height * (1f + Rand(-heightJitter, heightJitter)), s * topRadius * (1f + Rand(-jitter, jitter)));
                }

                Vector3 centre = Vector3.zero;
                foreach (Vector3 v in top) { centre += v; }
                centre /= sides;
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Vector3 outward = new Vector3(bottom[i].x + bottom[j].x, 0f, bottom[i].z + bottom[j].z);
                    Tri(tris, bottom[i], top[i], top[j], outward);
                    Tri(tris, bottom[i], top[j], bottom[j], outward);
                    Tri(tris, centre, top[j], top[i], Vector3.up);
                }

                return Finish(tris);
            }

            /// <summary>A gem crystal along +Y (base at y = 0): hexagonal shaft and a pointed, slightly leaning tip.</summary>
            public Mesh Spire(float radius, float height, int sides = 6)
            {
                var tris = new List<Vector3>();
                var baseRing = new Vector3[sides];
                var midRing = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = (i + Rand(-0.1f, 0.1f)) / sides * Mathf.PI * 2f;
                    baseRing[i] = new Vector3(Mathf.Cos(a) * radius * Rand(0.9f, 1.1f), 0f, Mathf.Sin(a) * radius * Rand(0.9f, 1.1f));
                    midRing[i] = new Vector3(Mathf.Cos(a) * radius * 0.86f * Rand(0.9f, 1.05f), height * 0.68f * Rand(0.96f, 1.04f), Mathf.Sin(a) * radius * 0.86f * Rand(0.9f, 1.05f));
                }

                var tip = new Vector3(Rand(-0.08f, 0.08f) * radius, height, Rand(-0.08f, 0.08f) * radius);
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Vector3 outward = new Vector3(baseRing[i].x + baseRing[j].x, 0f, baseRing[i].z + baseRing[j].z);
                    Tri(tris, baseRing[i], midRing[i], midRing[j], outward);
                    Tri(tris, baseRing[i], midRing[j], baseRing[j], outward);
                    Tri(tris, midRing[i], tip, midRing[j], outward + Vector3.up * 0.4f);
                }

                return Finish(tris);
            }

            /// <summary>Faceted rock: a displaced, flat shaded icosphere (radius about 1).</summary>
            public Mesh Rock(float roughness = 0.4f, int subdivisions = 1)
            {
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                var verts = new List<Vector3>
                {
                    new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
                    new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
                };
                for (int i = 0; i < verts.Count; i++) { verts[i] = verts[i].normalized; }
                var faces = new List<int[]>
                {
                    new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 },
                    new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
                    new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 },
                    new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 },
                };
                for (int s = 0; s < subdivisions; s++)
                {
                    var cache = new Dictionary<long, int>();
                    int Mid(int a, int b)
                    {
                        long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                        if (cache.TryGetValue(key, out int found)) { return found; }
                        verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                        cache[key] = verts.Count - 1;
                        return verts.Count - 1;
                    }

                    var next = new List<int[]>();
                    foreach (int[] f in faces)
                    {
                        int ab = Mid(f[0], f[1]), bc = Mid(f[1], f[2]), ca = Mid(f[2], f[0]);
                        next.Add(new[] { f[0], ab, ca });
                        next.Add(new[] { f[1], bc, ab });
                        next.Add(new[] { f[2], ca, bc });
                        next.Add(new[] { ab, bc, ca });
                    }

                    faces = next;
                }

                float seed = Rand() * 100f;
                for (int i = 0; i < verts.Count; i++)
                {
                    Vector3 p = verts[i];
                    float n = (Mathf.PerlinNoise(p.x * 1.7f + seed, p.y * 1.7f + seed * 0.5f) + Mathf.PerlinNoise(p.y * 1.7f + seed * 1.3f, p.z * 1.7f + 11f)
                               + Mathf.PerlinNoise(p.z * 1.7f + seed * 0.7f, p.x * 1.7f + 23f)) / 3f;
                    verts[i] = p * (1f - roughness * 0.5f + n * roughness);
                }

                var tris = new List<Vector3>();
                foreach (int[] f in faces)
                {
                    Tri(tris, verts[f[0]], verts[f[1]], verts[f[2]], verts[f[0]] + verts[f[1]] + verts[f[2]]);
                }

                return Finish(tris);
            }

            /// <summary>A gently uneven, flat shaded ground sheet (size x size, centred, y about 0).</summary>
            public Mesh Ground(float size, int cells, float bump)
            {
                float seed = Rand() * 50f;
                var grid = new Vector3[cells + 1, cells + 1];
                for (int z = 0; z <= cells; z++)
                {
                    for (int x = 0; x <= cells; x++)
                    {
                        float px = (x / (float)cells - 0.5f) * size, pz = (z / (float)cells - 0.5f) * size;
                        grid[x, z] = new Vector3(px, (Mathf.PerlinNoise(px * 0.3f + seed, pz * 0.3f + seed) - 0.5f) * bump, pz);
                    }
                }

                var tris = new List<Vector3>();
                for (int z = 0; z < cells; z++)
                {
                    for (int x = 0; x < cells; x++)
                    {
                        Tri(tris, grid[x, z], grid[x, z + 1], grid[x + 1, z], Vector3.up);
                        Tri(tris, grid[x + 1, z], grid[x, z + 1], grid[x + 1, z + 1], Vector3.up);
                    }
                }

                return Finish(tris);
            }
        }

        // ================================================================ the three scenes

        private static void ComposeGreenMine(Scene scene, Transform root, Camera cam, Light sun, Builder b)
        {
            cam.fieldOfView = 38f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 3.5f, -8.5f), Quaternion.LookRotation(new Vector3(0f, 2.3f, 7.5f) - new Vector3(0f, 3.5f, -8.5f)));
            cam.aspect = Width / (float)Height;
            b.Backdrop(cam, Hex("#5FB6E8"), Hex("#BFE7EE"), Hex("#FFF1C9"));
            b.BloomIntensity = 0.45f;

            sun.color = new Color(1f, 0.93f, 0.78f);
            sun.intensity = 1.55f;
            sun.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            RenderSettings.ambientSkyColor = Hex("#9CC9E6");
            RenderSettings.ambientEquatorColor = Hex("#8E9E88");
            RenderSettings.ambientGroundColor = Hex("#5B4A3A");
            RenderSettings.fogColor = Hex("#CFE8E0");
            RenderSettings.fogDensity = 0.0085f;

            Material grass = b.Lit(Hex("#5EAA4B"), 0.05f);
            Material grassDark = b.Lit(Hex("#3F8B45"), 0.05f);
            Material grassFar = b.Lit(Hex("#7FBC8C"), 0.02f);
            Material rockGrey = b.Lit(Hex("#8A8374"), 0.1f);
            Material rockWarm = b.Lit(Hex("#9C8468"), 0.1f);
            Material moss = b.Lit(Hex("#559E45"), 0.05f);
            Material dirt = b.Lit(Hex("#9A7550"), 0.04f);
            Material wood = b.Lit(Hex("#8F5E38"), 0.15f);
            Material woodDark = b.Lit(Hex("#5E3B24"), 0.12f);
            Material steel = b.Lit(Hex("#8C99AD"), 0.55f);
            Material cartMat = b.Lit(Hex("#7B5A47"), 0.3f);
            Material pitch = b.Lit(Hex("#07090D"), 0f);
            Material gold = b.Lit(Hex("#F5B52B"), 0.6f, new Color(1f, 0.62f, 0.08f) * 0.55f);
            Material emerald = b.Lit(Hex("#22B07F"), 0.6f, new Color(0.08f, 0.6f, 0.4f) * 0.6f);
            Material lamp = b.Lit(Hex("#FFE7A0"), 0.8f, new Color(1f, 0.75f, 0.3f) * 2.4f);
            Material trunk = b.Lit(Hex("#6B4630"), 0.1f);
            Material pineA = b.Lit(Hex("#2E8048"), 0.05f);
            Material pineB = b.Lit(Hex("#3C9A55"), 0.05f);
            Material cloud = b.Unlit(new Color(1.25f, 1.25f, 1.2f, 1f));

            // ground, far hills
            b.Obj(root, "Ground", b.Ground(80f, 30, 0.45f), grass, new Vector3(0f, -0.05f, 12f), Vector3.one);
            b.Obj(root, "HillFarL", b.Rock(0.15f, 2), grassFar, new Vector3(-13f, -1.2f, 30f), new Vector3(14f, 6.5f, 8f), null, false);
            b.Obj(root, "HillFarR", b.Rock(0.15f, 2), grassFar, new Vector3(13f, -1.6f, 32f), new Vector3(16f, 7.5f, 8f), null, false);
            b.Obj(root, "HillFarC", b.Rock(0.15f, 2), grassFar, new Vector3(2f, -2.2f, 40f), new Vector3(18f, 7f, 8f), null, false);

            // the rock face with the mine entrance
            b.Obj(root, "RockMain", b.Rock(0.3f), rockGrey, new Vector3(0f, 2.1f, 11.4f), new Vector3(5.6f, 4.0f, 3.8f), new Vector3(0f, 8f, 0f));
            b.Obj(root, "RockLeft", b.Rock(0.3f), rockWarm, new Vector3(-5.6f, 1.5f, 10.2f), new Vector3(3.3f, 3.0f, 2.9f), new Vector3(0f, -20f, 0f));
            b.Obj(root, "RockRight", b.Rock(0.3f), rockGrey, new Vector3(5.4f, 1.7f, 10.6f), new Vector3(3.4f, 3.4f, 3f), new Vector3(0f, 30f, 0f));
            b.Obj(root, "MossCap", b.Rock(0.3f), moss, new Vector3(0.2f, 5.2f, 11.2f), new Vector3(4.0f, 1.1f, 2.6f));
            b.Obj(root, "MossCapL", b.Rock(0.3f), moss, new Vector3(-5.5f, 3.9f, 10f), new Vector3(2.3f, 0.8f, 1.9f));
            b.Obj(root, "MossCapR", b.Rock(0.3f), moss, new Vector3(5.4f, 4.4f, 10.4f), new Vector3(2.4f, 0.8f, 2f));

            // timber framed entrance (the opening is set into the rock face)
            b.Obj(root, "Opening", Builder.Cube, pitch, new Vector3(0f, 1.35f, 7.7f), new Vector3(2.6f, 2.7f, 0.8f), null, false);
            b.Obj(root, "PostL", Builder.Cube, wood, new Vector3(-1.5f, 1.55f, 7.1f), new Vector3(0.36f, 3.2f, 0.42f));
            b.Obj(root, "PostR", Builder.Cube, wood, new Vector3(1.5f, 1.55f, 7.1f), new Vector3(0.36f, 3.2f, 0.42f));
            b.Obj(root, "Beam", Builder.Cube, wood, new Vector3(0f, 3.05f, 7.1f), new Vector3(3.8f, 0.42f, 0.5f));
            b.Obj(root, "BraceL", Builder.Cube, woodDark, new Vector3(-1.1f, 2.6f, 7.0f), new Vector3(0.24f, 1.05f, 0.32f), new Vector3(0f, 0f, 45f));
            b.Obj(root, "BraceR", Builder.Cube, woodDark, new Vector3(1.1f, 2.6f, 7.0f), new Vector3(0.24f, 1.05f, 0.32f), new Vector3(0f, 0f, -45f));
            b.Obj(root, "LampL", Builder.Sphere, lamp, new Vector3(-1.95f, 2.5f, 6.8f), Vector3.one * 0.24f, null, false);
            b.Obj(root, "LampR", Builder.Sphere, lamp, new Vector3(1.95f, 2.5f, 6.8f), Vector3.one * 0.24f, null, false);
            b.Glow(root, new Vector3(-1.95f, 2.5f, 6.6f), 2.1f, new Color(1f, 0.75f, 0.32f, 0.6f));
            b.Glow(root, new Vector3(1.95f, 2.5f, 6.6f), 2.1f, new Color(1f, 0.75f, 0.32f, 0.6f));
            b.Glow(root, new Vector3(0f, 0.8f, 7.0f), 3.6f, new Color(1f, 0.78f, 0.34f, 0.4f));
            for (int i = 0; i < 6; i++)
            {
                b.Obj(root, "InsideGold" + i, Builder.Sphere, gold, new Vector3(Mathf.Lerp(-0.9f, 0.9f, i / 5f) + b.Rand(-0.12f, 0.12f), 0.2f + b.Rand() * 0.2f, 7.05f), Vector3.one * b.Rand(0.18f, 0.3f), null, false);
            }

            // dirt path, rails and sleepers leading out of the mine
            b.Obj(root, "Path", Builder.Cube, dirt, new Vector3(0f, 0.0f, 2.4f), new Vector3(2.4f, 0.06f, 12.4f));
            b.Obj(root, "RailL", Builder.Cube, steel, new Vector3(-0.56f, 0.1f, 2.4f), new Vector3(0.1f, 0.08f, 12.4f));
            b.Obj(root, "RailR", Builder.Cube, steel, new Vector3(0.56f, 0.1f, 2.4f), new Vector3(0.1f, 0.08f, 12.4f));
            for (int i = 0; i < 10; i++)
            {
                b.Obj(root, "Sleeper" + i, Builder.Cube, woodDark, new Vector3(0f, 0.06f, -3.2f + i * 1.12f), new Vector3(1.55f, 0.08f, 0.24f));
            }

            // mine cart full of gold
            Transform cart = new GameObject("Cart").transform;
            cart.SetParent(root, false);
            cart.localPosition = new Vector3(0.05f, 0f, 0.9f);
            cart.localRotation = Quaternion.Euler(0f, 10f, 0f);
            b.Obj(cart, "Body", Builder.Cube, cartMat, new Vector3(0f, 0.6f, 0f), new Vector3(1.15f, 0.62f, 1.5f));
            b.Obj(cart, "Rim", Builder.Cube, steel, new Vector3(0f, 0.93f, 0f), new Vector3(1.25f, 0.08f, 1.6f));
            foreach (float wx in new[] { -0.6f, 0.6f })
            {
                foreach (float wz in new[] { -0.5f, 0.5f })
                {
                    b.Obj(cart, "Wheel", Builder.Cylinder, woodDark, new Vector3(wx, 0.24f, wz), new Vector3(0.24f, 0.07f, 0.24f), new Vector3(0f, 0f, 90f));
                }
            }

            for (int i = 0; i < 9; i++)
            {
                b.Obj(cart, "Nugget" + i, b.Rock(0.35f, 0), gold, new Vector3(b.Rand(-0.42f, 0.42f), 0.98f + b.Rand() * 0.18f, b.Rand(-0.55f, 0.55f)), Vector3.one * b.Rand(0.17f, 0.27f), new Vector3(b.Rand(0f, 90f), b.Rand(0f, 90f), 0f));
            }

            b.Glow(cart, new Vector3(0f, 1.2f, 0f), 2.6f, new Color(1f, 0.8f, 0.3f, 0.45f));

            // crystal clusters: gold on the left, emerald on the right
            Cluster(b, root, new Vector3(-3.15f, 0f, 1.4f), gold, new[] { 1.9f, 1.35f, 1.0f, 0.7f }, new Color(1f, 0.75f, 0.25f, 0.5f), rockGrey);
            Cluster(b, root, new Vector3(3.3f, 0f, 0.5f), emerald, new[] { 1.35f, 1.0f, 0.72f }, new Color(0.3f, 0.95f, 0.65f, 0.4f), rockWarm);
            for (int i = 0; i < 4; i++)
            {
                b.Obj(root, "GroundNugget" + i, b.Rock(0.3f, 0), gold, new Vector3(b.Rand(1.4f, 2.9f), 0.12f, b.Rand(-0.6f, 1.8f)), Vector3.one * b.Rand(0.16f, 0.26f), new Vector3(0f, b.Rand(0f, 90f), 0f));
            }

            // trees and bushes
            foreach (Vector3 p in new[] { new Vector3(-6.6f, 0f, 4.8f), new Vector3(-5f, 0f, 6.4f), new Vector3(6.4f, 0f, 5.2f), new Vector3(7.8f, 0f, 3.2f), new Vector3(-8.2f, 0f, 2.4f) })
            {
                Pine(b, root, p, b.Rand(0.9f, 1.3f), trunk, b.Rand() > 0.5f ? pineA : pineB);
            }

            foreach (Vector3 p in new[] { new Vector3(-2.2f, 0.2f, 5.3f), new Vector3(2.4f, 0.2f, 5.6f), new Vector3(-6.2f, 0.25f, 0.4f), new Vector3(6.2f, 0.25f, -0.6f), new Vector3(-1.4f, 0.12f, -2.8f) })
            {
                b.Obj(root, "Bush", Builder.Sphere, b.Rand() > 0.5f ? grass : grassDark, p, new Vector3(b.Rand(0.8f, 1.2f), b.Rand(0.45f, 0.65f), b.Rand(0.8f, 1.1f)));
            }

            // a few clouds
            foreach (Vector3 p in new[] { new Vector3(-9f, 12f, 40f), new Vector3(10f, 14.5f, 44f), new Vector3(-1f, 17f, 50f) })
            {
                for (int i = 0; i < 4; i++)
                {
                    b.Obj(root, "Cloud", Builder.Sphere, cloud, p + new Vector3(i * 2.4f - 3.6f, (i % 2) * 0.8f, 0f), new Vector3(3.6f - Mathf.Abs(i - 1.5f), 1.5f, 1.4f), null, false);
                }
            }
        }

        private static void ComposeRockyCanyon(Scene scene, Transform root, Camera cam, Light sun, Builder b)
        {
            cam.fieldOfView = 36f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 3.6f, -9f), Quaternion.LookRotation(new Vector3(0f, 3.0f, 10f) - new Vector3(0f, 3.6f, -9f)));
            cam.aspect = Width / (float)Height;
            b.Backdrop(cam, Hex("#D9604F"), Hex("#FF9F63"), Hex("#FFE2A6"));
            b.BloomIntensity = 0.6f;
            b.Saturation = 14f;

            sun.color = new Color(1f, 0.76f, 0.5f);
            sun.intensity = 1.9f;
            sun.transform.rotation = Quaternion.Euler(30f, -52f, 0f);
            RenderSettings.ambientSkyColor = Hex("#E58B66");
            RenderSettings.ambientEquatorColor = Hex("#B86A55");
            RenderSettings.ambientGroundColor = Hex("#6F3A2E");
            RenderSettings.fogColor = Hex("#F7B681");
            RenderSettings.fogDensity = 0.0075f;

            Material sand = b.Lit(Hex("#D88F55"), 0.04f);
            Material sandLight = b.Lit(Hex("#EDB67A"), 0.04f);
            Material[] strata =
            {
                b.Lit(Hex("#E39A5C"), 0.05f), b.Lit(Hex("#C4573A"), 0.05f), b.Lit(Hex("#EDB070"), 0.05f),
                b.Lit(Hex("#A8432F"), 0.05f), b.Lit(Hex("#D4743F"), 0.05f), b.Lit(Hex("#8C3626"), 0.05f)
            };
            Material boulder = b.Lit(Hex("#B2553A"), 0.06f);
            Material boulderDark = b.Lit(Hex("#8A3F2E"), 0.06f);
            Material cactus = b.Lit(Hex("#5E8F55"), 0.08f);
            Material amber = b.Lit(Hex("#F59A1B"), 0.6f, new Color(1f, 0.45f, 0.05f) * 0.6f);
            Material gold = b.Lit(Hex("#F5B52B"), 0.6f, new Color(1f, 0.62f, 0.08f) * 0.5f);
            Material wood = b.Lit(Hex("#7A5236"), 0.12f);
            Material woodDark = b.Lit(Hex("#4E3120"), 0.1f);

            b.Obj(root, "Ground", b.Ground(90f, 32, 0.5f), sand, new Vector3(0f, -0.05f, 14f), Vector3.one);
            b.Obj(root, "Trail", Builder.Cube, sandLight, new Vector3(0f, 0.0f, 6f), new Vector3(2.6f, 0.05f, 22f));
            b.Glow(root, new Vector3(3f, 13f, 60f), 52f, new Color(1f, 0.85f, 0.5f, 0.85f));
            b.Glow(root, new Vector3(3f, 13f, 59.5f), 17f, new Color(1f, 0.97f, 0.82f, 1f));

            // canyon walls: stacked, banded mesas
            Mesa(b, root, new Vector3(-7.2f, 0f, 6.5f), 3.5f, new[] { 1.8f, 1.4f, 1.6f, 1.3f, 1.5f }, strata, 0);
            Mesa(b, root, new Vector3(7.6f, 0f, 7.5f), 3.9f, new[] { 1.5f, 1.7f, 1.3f, 1.6f, 1.4f, 1.2f }, strata, 2);
            Mesa(b, root, new Vector3(-2.9f, 0f, 14.5f), 2.1f, new[] { 1.6f, 1.2f, 1.5f, 1.1f }, strata, 1);
            Mesa(b, root, new Vector3(3.6f, 0f, 16f), 2.5f, new[] { 1.7f, 1.4f, 1.8f, 1.2f }, strata, 3);
            Mesa(b, root, new Vector3(0.5f, 0f, 27f), 6.5f, new[] { 1.9f, 1.8f, 1.6f, 1.4f }, strata, 4);
            Mesa(b, root, new Vector3(-12f, 0f, 22f), 5f, new[] { 1.8f, 1.6f, 1.4f, 1.5f, 1.2f }, strata, 5);
            Mesa(b, root, new Vector3(13f, 0f, 24f), 5.5f, new[] { 1.9f, 1.5f, 1.7f, 1.3f, 1.2f }, strata, 1);

            // a slender rock spire in the mid distance
            Mesa(b, root, new Vector3(-0.6f, 0f, 11.5f), 0.95f, new[] { 1.3f, 1.2f, 1.2f, 1.1f, 0.9f }, strata, 3, 0.86f);

            // boulders
            foreach (var (pos, size, dark) in new[]
            {
                (new Vector3(-3.7f, 0.5f, 2.2f), 1.1f, false), (new Vector3(-4.7f, 0.35f, 3.6f), 0.8f, true), (new Vector3(-2.8f, 0.3f, 4.6f), 0.6f, false),
                (new Vector3(4.2f, 0.5f, 3.4f), 1.0f, true), (new Vector3(5.4f, 0.4f, 4.6f), 0.85f, false), (new Vector3(3.3f, 0.3f, 6.2f), 0.6f, true)
            })
            {
                b.Obj(root, "Boulder", b.Rock(0.4f), dark ? boulderDark : boulder, pos, new Vector3(size * 1.15f, size * 0.8f, size), new Vector3(0f, b.Rand(0f, 90f), 0f));
            }

            // cacti
            Cactus(b, root, new Vector3(-2.3f, 0f, 3.2f), 1f, cactus);
            Cactus(b, root, new Vector3(2.6f, 0f, 5.4f), 0.8f, cactus);

            // dead timber + an old mine cart wheel sign post

            // amber crystals in front
            Cluster(b, root, new Vector3(2.9f, 0f, 0.3f), amber, new[] { 1.55f, 1.1f, 0.8f }, new Color(1f, 0.62f, 0.2f, 0.5f), boulderDark);
            Cluster(b, root, new Vector3(-3.4f, 0f, 0.0f), amber, new[] { 1.0f, 0.7f }, new Color(1f, 0.62f, 0.2f, 0.4f), boulder);
            for (int i = 0; i < 4; i++)
            {
                b.Obj(root, "Nugget" + i, b.Rock(0.3f, 0), gold, new Vector3(b.Rand(0.4f, 1.8f), 0.12f, b.Rand(-0.2f, 1.6f)), Vector3.one * b.Rand(0.15f, 0.24f), new Vector3(0f, b.Rand(0f, 90f), 0f));
            }
        }

        private static void ComposeAncientMine(Scene scene, Transform root, Camera cam, Light sun, Builder b)
        {
            cam.fieldOfView = 40f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 3.3f, -6.6f), Quaternion.LookRotation(new Vector3(0f, 2.3f, 8f) - new Vector3(0f, 3.3f, -6.6f)));
            cam.aspect = Width / (float)Height;
            b.Backdrop(cam, Hex("#120C2B"), Hex("#2A1D5A"), Hex("#3D2C78"));
            b.BloomIntensity = 1.1f;
            b.Saturation = 18f;

            sun.color = new Color(0.55f, 0.62f, 1f);
            sun.intensity = 0.75f;
            sun.transform.rotation = Quaternion.Euler(52f, -25f, 0f);
            RenderSettings.ambientSkyColor = Hex("#3A2E7A");
            RenderSettings.ambientEquatorColor = Hex("#2A2260");
            RenderSettings.ambientGroundColor = Hex("#15102E");
            RenderSettings.fogColor = Hex("#2B1F62");
            RenderSettings.fogDensity = 0.028f;

            Material floorMat = b.Lit(Hex("#3E3763"), 0.28f);
            Material wall = b.Lit(Hex("#2D2656"), 0.12f);
            Material wallLight = b.Lit(Hex("#3F3777"), 0.12f);
            Material stone = b.Lit(Hex("#7C779F"), 0.18f);
            Material stoneDark = b.Lit(Hex("#5C5880"), 0.15f);
            Material violet = b.Lit(Hex("#7A3FD6"), 0.35f, new Color(0.5f, 0.18f, 1f) * 0.9f);
            Material blue = b.Lit(Hex("#2878E8"), 0.35f, new Color(0.1f, 0.4f, 1f) * 0.9f);
            Material cyan = b.Lit(Hex("#12BFAE"), 0.35f, new Color(0.05f, 0.75f, 0.65f) * 1.0f);
            Material rune = b.Lit(Hex("#18C9B8"), 0.4f, new Color(0.05f, 0.85f, 0.8f) * 1.15f);
            Material dark = b.Lit(Hex("#0B0818"), 0f);
            Material mote = b.Unlit(new Color(0.45f, 0.85f, 1f));

            b.Obj(root, "Floor", b.Ground(60f, 26, 0.25f), floorMat, new Vector3(0f, -0.05f, 12f), Vector3.one);

            // cavern: walls, ceiling stalactites
            foreach (var (pos, scale, light) in new[]
            {
                (new Vector3(-8.5f, 3.0f, 5f), new Vector3(4.5f, 6f, 5.5f), false), (new Vector3(8.8f, 3.2f, 4.5f), new Vector3(4.6f, 6.4f, 5.5f), true),
                (new Vector3(-6.5f, 2.2f, 12f), new Vector3(5f, 5f, 4f), true), (new Vector3(7f, 2.4f, 12.5f), new Vector3(5.2f, 5.4f, 4f), false),
                (new Vector3(0f, 6.5f, 17f), new Vector3(12f, 7.5f, 4f), false)
            })
            {
                b.Obj(root, "Wall", b.Rock(0.4f), light ? wallLight : wall, pos, scale, new Vector3(0f, b.Rand(0f, 90f), 0f));
            }

            for (int i = 0; i < 9; i++)
            {
                float x = Mathf.Lerp(-7f, 7f, i / 8f) + b.Rand(-0.5f, 0.5f);
                float h = b.Rand(1.2f, 2.6f);
                b.Obj(root, "Stalactite", b.Prism(5, 0.42f, 0.03f, h, 0.1f), wall, new Vector3(x, 8.6f - h * 0.2f, b.Rand(5f, 11f)), Vector3.one, new Vector3(180f, 0f, 0f));
            }

            // the portal glow behind the arch
            b.Glow(root, new Vector3(0f, 2.6f, 10.4f), 11f, new Color(0.45f, 0.38f, 1f, 0.85f));
            b.Glow(root, new Vector3(0f, 2.5f, 10.2f), 6.5f, new Color(0.55f, 0.9f, 1f, 0.85f));
            b.Point(root, new Vector3(0f, 2.5f, 8.8f), new Color(0.5f, 0.75f, 1f), 28f, 13f);

            // the ancient arch: two pillars, a lintel and a keystone with a glowing rune
            foreach (float side in new[] { -1f, 1f })
            {
                Transform pillar = new GameObject("Pillar").transform;
                pillar.SetParent(root, false);
                pillar.localPosition = new Vector3(side * 2.3f, 0f, 8.2f);
                b.Obj(pillar, "Foot", Builder.Cube, stoneDark, new Vector3(0f, 0.25f, 0f), new Vector3(1.7f, 0.5f, 1.7f));
                b.Obj(pillar, "Shaft", b.Prism(8, 0.62f, 0.55f, 4.0f, 0.04f), stone, new Vector3(0f, 0.5f, 0f), Vector3.one);
                b.Obj(pillar, "Cap", Builder.Cube, stoneDark, new Vector3(0f, 4.65f, 0f), new Vector3(1.5f, 0.32f, 1.5f));
                b.Obj(pillar, "Band", Builder.Cube, rune, new Vector3(0f, 2.4f, 0.55f), new Vector3(0.7f, 0.07f, 0.1f), null, false);
                b.Obj(pillar, "Band2", Builder.Cube, rune, new Vector3(0f, 3.1f, 0.55f), new Vector3(0.45f, 0.07f, 0.1f), null, false);
            }

            b.Obj(root, "Lintel", Builder.Cube, stone, new Vector3(0f, 5.1f, 8.2f), new Vector3(6.6f, 0.78f, 1.4f));
            b.Obj(root, "Keystone", Builder.Cube, stoneDark, new Vector3(0f, 5.1f, 7.45f), new Vector3(1.15f, 1.0f, 0.3f));
            b.Obj(root, "KeyRune", Builder.Cube, rune, new Vector3(0f, 5.1f, 7.27f), new Vector3(0.45f, 0.45f, 0.06f), new Vector3(0f, 0f, 45f), false);
            b.Glow(root, new Vector3(0f, 5.1f, 7.1f), 2.2f, new Color(0.3f, 1f, 0.95f, 0.65f));

            // steps up to the arch
            b.Obj(root, "Step1", Builder.Cube, stoneDark, new Vector3(0f, 0.12f, 6.2f), new Vector3(5.6f, 0.26f, 1.6f));
            b.Obj(root, "Step2", Builder.Cube, stone, new Vector3(0f, 0.3f, 6.9f), new Vector3(4.6f, 0.3f, 1.4f));

            // rune circle on the floor
            b.Obj(root, "RuneRing", Builder.Cylinder, rune, new Vector3(0f, 0.02f, 3.2f), new Vector3(2.7f, 0.02f, 2.7f), null, false);
            b.Obj(root, "RuneInner", Builder.Cylinder, dark, new Vector3(0f, 0.035f, 3.2f), new Vector3(2.45f, 0.02f, 2.45f), null, false);
            b.Obj(root, "RuneRing2", Builder.Cylinder, rune, new Vector3(0f, 0.045f, 3.2f), new Vector3(1.5f, 0.02f, 1.5f), null, false);
            b.Obj(root, "RuneInner2", Builder.Cylinder, dark, new Vector3(0f, 0.06f, 3.2f), new Vector3(1.32f, 0.02f, 1.32f), null, false);
            for (int i = 0; i < 4; i++)
            {
                b.Obj(root, "RuneSpoke" + i, Builder.Cube, rune, new Vector3(0f, 0.07f, 3.2f), new Vector3(0.09f, 0.02f, 4.9f), new Vector3(0f, i * 45f, 0f), false);
            }

            b.Obj(root, "RuneCore", Builder.Cylinder, rune, new Vector3(0f, 0.08f, 3.2f), new Vector3(0.45f, 0.02f, 0.45f), null, false);
            b.Glow(root, new Vector3(0f, 0.15f, 3.2f), 5.4f, new Color(0.2f, 0.95f, 0.9f, 0.32f)).transform.rotation = Quaternion.Euler(78f, 0f, 0f);

            // broken columns and fallen blocks
            BrokenColumn(b, root, new Vector3(-4.6f, 0f, 6.8f), 3.1f, stone, stoneDark);
            BrokenColumn(b, root, new Vector3(4.9f, 0f, 7.6f), 2.3f, stone, stoneDark);
            BrokenColumn(b, root, new Vector3(-5.9f, 0f, 10.2f), 4.0f, stoneDark, stone);
            b.Obj(root, "FallenColumn", b.Prism(8, 0.5f, 0.46f, 2.6f, 0.04f), stone, new Vector3(2.6f, 0.45f, 4.4f), Vector3.one, new Vector3(0f, 28f, 90f));
            b.Obj(root, "Block1", Builder.Cube, stoneDark, new Vector3(-2.7f, 0.25f, 4.7f), new Vector3(0.9f, 0.5f, 0.8f), new Vector3(0f, 20f, 0f));
            b.Obj(root, "Block2", Builder.Cube, stone, new Vector3(3.8f, 0.22f, 5.6f), new Vector3(0.7f, 0.45f, 0.7f), new Vector3(0f, -25f, 0f));

            // glowing crystals
            Cluster(b, root, new Vector3(-3.1f, 0f, 1.6f), violet, new[] { 2.6f, 1.9f, 1.4f, 1.0f, 0.7f }, new Color(0.7f, 0.4f, 1f, 0.55f), wallLight);
            Cluster(b, root, new Vector3(3.2f, 0f, 1.2f), blue, new[] { 2.1f, 1.6f, 1.1f, 0.8f }, new Color(0.35f, 0.65f, 1f, 0.5f), wall);
            Cluster(b, root, new Vector3(1.5f, 0f, 9.4f), cyan, new[] { 1.1f, 0.8f }, new Color(0.2f, 0.95f, 0.9f, 0.4f), wallLight);
            Cluster(b, root, new Vector3(-1.9f, 0f, 9.6f), violet, new[] { 0.9f, 0.6f }, new Color(0.7f, 0.4f, 1f, 0.4f), wall);
            b.Point(root, new Vector3(-3.2f, 2.2f, 1.2f), new Color(0.7f, 0.4f, 1f), 22f, 10f);
            b.Point(root, new Vector3(3.4f, 2.0f, 0.6f), new Color(0.35f, 0.65f, 1f), 20f, 10f);

            // floating dust
            for (int i = 0; i < 34; i++)
            {
                b.Obj(root, "Mote", Builder.Sphere, mote, new Vector3(b.Rand(-5.5f, 5.5f), b.Rand(0.4f, 5.5f), b.Rand(0f, 10f)), Vector3.one * b.Rand(0.025f, 0.055f), null, false);
            }
        }

        // ================================================================ shared pieces

        /// <summary>A cluster of crystal spires of the given heights around a point (leaning outwards a little), on a few rocks, with a glow behind.</summary>
        private static void Cluster(Builder b, Transform parent, Vector3 centre, Material crystal, float[] heights, Color glow, Material rockMat)
        {
            var group = new GameObject("CrystalCluster").transform;
            group.SetParent(parent, false);
            group.localPosition = centre;
            b.Obj(group, "Rock", b.Rock(0.4f), rockMat, new Vector3(0f, 0.1f, 0.1f), new Vector3(1.25f, 0.5f, 1.1f));
            for (int i = 0; i < heights.Length; i++)
            {
                float angle = i / (float)heights.Length * Mathf.PI * 2f + 0.6f;
                float r = i == 0 ? 0f : 0.55f + 0.1f * i;
                float lean = i == 0 ? 3f : 14f + i * 3f;
                var position = new Vector3(Mathf.Cos(angle) * r, 0.05f, Mathf.Sin(angle) * r * 0.8f);
                var euler = new Vector3(Mathf.Sin(angle) * lean, b.Rand(0f, 360f), -Mathf.Cos(angle) * lean);
                b.Obj(group, "Crystal" + i, b.Spire(0.3f + heights[i] * 0.07f, heights[i]), crystal, position, Vector3.one, euler);
            }

            b.Glow(group, new Vector3(0f, heights[0] * 0.5f, -0.5f), heights[0] * 2.6f, glow);
        }

        private static void Mesa(Builder b, Transform parent, Vector3 position, float radius, float[] layerHeights, Material[] strata, int colourOffset, float taper = 0.93f)
        {
            var group = new GameObject("Mesa").transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            group.localRotation = Quaternion.Euler(0f, b.Rand(0f, 360f), 0f);
            float y = 0f;
            float r = radius;
            for (int i = 0; i < layerHeights.Length; i++)
            {
                float next = r * taper;
                float flare = i == 0 ? 1.12f : 1f; // the foot is a little wider
                b.Obj(group, "Layer" + i, b.Prism(9, r * flare, next, layerHeights[i], 0.09f, 0.02f), strata[(i + colourOffset) % strata.Length], new Vector3(0f, y, 0f), Vector3.one);
                y += layerHeights[i];
                r = next * 0.96f;
            }

            b.Obj(group, "Top", b.Rock(0.3f, 0), strata[(colourOffset + 2) % strata.Length], new Vector3(0f, y + 0.02f, 0f), new Vector3(r * 0.95f, 0.45f, r * 0.9f));
        }

        private static void Pine(Builder b, Transform parent, Vector3 position, float scale, Material trunk, Material leaves)
        {
            var group = new GameObject("Pine").transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            group.localScale = Vector3.one * scale;
            b.Obj(group, "Trunk", b.Prism(6, 0.22f, 0.17f, 1.1f, 0.05f), trunk, Vector3.zero, Vector3.one);
            float y = 0.7f;
            float r = 1.05f;
            for (int i = 0; i < 3; i++)
            {
                b.Obj(group, "Tier" + i, b.Prism(7, r, 0.05f, 1.45f, 0.1f, 0.03f), leaves, new Vector3(0f, y, 0f), Vector3.one);
                y += 0.95f;
                r *= 0.76f;
            }
        }

        private static void Cactus(Builder b, Transform parent, Vector3 position, float scale, Material material)
        {
            var group = new GameObject("Cactus").transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            group.localScale = Vector3.one * scale;
            b.Obj(group, "Body", b.Prism(7, 0.27f, 0.2f, 2.0f, 0.05f), material, Vector3.zero, Vector3.one);
            b.Obj(group, "ArmL", b.Prism(6, 0.15f, 0.12f, 0.9f, 0.05f), material, new Vector3(-0.26f, 0.95f, 0f), Vector3.one, new Vector3(0f, 0f, 38f));
            b.Obj(group, "ArmLUp", b.Prism(6, 0.13f, 0.1f, 0.7f, 0.05f), material, new Vector3(-0.78f, 1.5f, 0f), Vector3.one);
            b.Obj(group, "ArmR", b.Prism(6, 0.15f, 0.12f, 0.7f, 0.05f), material, new Vector3(0.24f, 1.15f, 0f), Vector3.one, new Vector3(0f, 0f, -38f));
            b.Obj(group, "ArmRUp", b.Prism(6, 0.13f, 0.1f, 0.5f, 0.05f), material, new Vector3(0.62f, 1.5f, 0f), Vector3.one);
        }

        private static void BrokenColumn(Builder b, Transform parent, Vector3 position, float height, Material stone, Material stoneDark)
        {
            var group = new GameObject("Column").transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            b.Obj(group, "Foot", Builder.Cube, stoneDark, new Vector3(0f, 0.2f, 0f), new Vector3(1.4f, 0.4f, 1.4f));
            b.Obj(group, "Shaft", b.Prism(8, 0.5f, 0.45f, height, 0.05f, 0.1f), stone, new Vector3(0f, 0.4f, 0f), Vector3.one);
            b.Obj(group, "Rubble", b.Rock(0.4f, 0), stoneDark, new Vector3(0.8f, 0.2f, -0.3f), new Vector3(0.45f, 0.3f, 0.4f));
        }
    }
}
