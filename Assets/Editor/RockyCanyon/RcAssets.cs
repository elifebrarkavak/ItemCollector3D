#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>The Rocky Canyon's generated assets: one terrain mesh, one colour map, two shared rock meshes, a sky texture and nine cheap URP Lit materials.</summary>
    public static class RcAssets
    {
        public const string Root = "Assets/Art/RockyCanyon";
        public const string MeshFolder = Root + "/Meshes";
        public const string MaterialFolder = Root + "/Materials";
        public const string TextureFolder = Root + "/Textures";

        public const int ColorMapWidth = 896;   // 8 px per metre over 112 m
        public const int ColorMapHeight = 736;  // 8 px per metre over 92 m

        // ================================================================ terrain mesh

        public static Mesh BuildTerrainMesh()
        {
            Directory.CreateDirectory(MeshFolder);
            string path = MeshFolder + "/RockyCanyon_Terrain.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "RockyCanyon_Terrain" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            float[,] g = RcLayout.Grid;
            int cx = RcLayout.CellsX, cz = RcLayout.CellsZ;
            var verts = new List<Vector3>(cx * cz * 6);
            var uvs = new List<Vector2>(cx * cz * 6);
            var tris = new List<int>(cx * cz * 6);
            for (int j = 0; j < cz; j++)
            {
                for (int i = 0; i < cx; i++)
                {
                    Vector3 a = Vertex(g, i, j), b = Vertex(g, i + 1, j), c = Vertex(g, i, j + 1), d = Vertex(g, i + 1, j + 1);
                    // the same two triangles the height function interpolates: (a, c, b) and (b, c, d); every triangle has its own vertices (flat shading)
                    AddTriangle(verts, uvs, tris, a, c, b);
                    AddTriangle(verts, uvs, tris, b, c, d);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Vector3 Vertex(float[,] g, int i, int j) => new(-RcLayout.HalfX + i * RcLayout.Cell, g[i, j], -RcLayout.HalfZ + j * RcLayout.Cell);

        private static void AddTriangle(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = verts.Count;
            Vector3[] corner = { a, b, c };
            for (int k = 0; k < 3; k++)
            {
                verts.Add(corner[k]);
                uvs.Add(new Vector2((corner[k].x + RcLayout.HalfX) / (2f * RcLayout.HalfX), (corner[k].z + RcLayout.HalfZ) / (2f * RcLayout.HalfZ)));
                tris.Add(start + k);
            }
        }

        // ================================================================ colour map

        private static readonly Color[] Strata =
        {
            new(0.62f, 0.28f, 0.19f), new(0.76f, 0.37f, 0.22f), new(0.86f, 0.52f, 0.29f), new(0.93f, 0.70f, 0.45f),
        };

        public static Texture2D PaintColorMap()
        {
            int w = ColorMapWidth, h = ColorMapHeight;
            var pixels = new Color32[w * h];
            for (int py = 0; py < h; py++)
            {
                for (int px = 0; px < w; px++)
                {
                    float x = -RcLayout.HalfX + (px + 0.5f) * (2f * RcLayout.HalfX / w);
                    float z = -RcLayout.HalfZ + (py + 0.5f) * (2f * RcLayout.HalfZ / h);
                    pixels[py * w + px] = Texel(x, z, px, py);
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(TextureFolder);
            string path = TextureFolder + "/RockyCanyon_TerrainColor.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ seed * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        private static Color32 Texel(float x, float z, int px, int py)
        {
            float height = RcLayout.Surface(x, z);
            float slope = RcLayout.Slope(x, z);
            RcLayout.Height(x, z, out RcZone zone, out float floor);
            float edge = RcLayout.EdgeDistance(x, z);
            float n1 = Mathf.PerlinNoise(x * 0.11f + 10f, z * 0.11f + 30f);
            float n2 = Mathf.PerlinNoise(x * 0.37f + 50f, z * 0.37f + 70f);
            float speck = Hash01(px / 2, py / 2, 11);

            // floor colour by area
            Color ground;
            switch (zone)
            {
                case RcZone.Outpost:
                    ground = Color.Lerp(new Color(0.95f, 0.84f, 0.57f), new Color(0.88f, 0.73f, 0.48f), Mathf.SmoothStep(0.35f, 0.7f, n1));
                    break;
                case RcZone.Riverbed:
                    ground = Color.Lerp(new Color(0.92f, 0.83f, 0.64f), new Color(0.84f, 0.70f, 0.50f), Mathf.Clamp01(1f - floor / 1.6f) * 0.7f);
                    ground = Color.Lerp(ground, speck > 0.5f ? new Color(0.56f, 0.46f, 0.38f) : new Color(0.94f, 0.88f, 0.77f), n2 > 0.55f ? 0.45f : 0f); // pebbles
                    break;
                case RcZone.Railway:
                    ground = Color.Lerp(new Color(0.74f, 0.60f, 0.45f), new Color(0.64f, 0.50f, 0.38f), n2);
                    ground = Color.Lerp(ground, new Color(0.47f, 0.37f, 0.30f), speck > 0.82f ? 0.6f : 0f); // ballast
                    break;
                case RcZone.Overlook:
                    ground = Color.Lerp(new Color(0.96f, 0.85f, 0.60f), new Color(0.90f, 0.75f, 0.50f), n1);
                    break;
                case RcZone.Ravine:
                    ground = Color.Lerp(new Color(0.93f, 0.74f, 0.45f), new Color(0.86f, 0.62f, 0.37f), n1);
                    break;
                default:
                    ground = new Color(0.80f, 0.62f, 0.44f); // the rim above the walls
                    break;
            }

            // rock faces: horizontal strata bands that follow the height
            float band = Mathf.Clamp01(Mathf.Sin(height * 2.4f + n1 * 5f) * 0.5f + 0.5f + (n2 - 0.5f) * 0.3f);
            float pos = band * 3f;
            int stop = Mathf.Min(2, (int)pos);
            Color rock = Color.Lerp(Strata[stop], Strata[stop + 1], pos - stop); // a smooth ramp through the four stops, no hard stripes
            float shade = Mathf.Lerp(0.88f, 1.06f, n2);
            rock *= shade;

            float wallness = Mathf.Max(Mathf.SmoothStep(0.30f, 0.85f, slope), Mathf.SmoothStep(0f, 1.2f, edge) * 0.8f);
            Color c = Color.Lerp(ground, rock, wallness);
            c.a = 1f;
            return c;
        }

        // ================================================================ sky

        public static Texture2D PaintSky()
        {
            const int w = 512, h = 256;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);                 // 0 = down .. 1 = up (equirectangular)
                Color c;
                if (t < 0.5f)
                {
                    c = Color.Lerp(new Color(0.78f, 0.56f, 0.38f), new Color(0.98f, 0.80f, 0.60f), t * 2f); // below the horizon: dusty
                }
                else
                {
                    float u = (t - 0.5f) * 2f;
                    c = Color.Lerp(new Color(0.99f, 0.84f, 0.64f), new Color(0.42f, 0.66f, 0.90f), Mathf.Pow(u, 0.55f)); // warm horizon to a clear blue
                }

                for (int x = 0; x < w; x++)
                {
                    float glow = Mathf.Exp(-Mathf.Pow((x / (float)w - 0.72f) * 7f, 2f)) * Mathf.Exp(-Mathf.Pow((t - 0.62f) * 4.5f, 2f)) * 0.22f; // the sun's glow
                    pixels[y * w + x] = c + new Color(glow, glow * 0.8f, glow * 0.45f);
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(TextureFolder);
            string path = TextureFolder + "/RockyCanyon_Sky.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ================================================================ shared rock meshes

        /// <summary>A faceted boulder (20 triangles, its own vertices for flat shading), about 1 m across, flat at the bottom.</summary>
        public static Mesh BoulderMesh()
        {
            string path = MeshFolder + "/RC_Boulder.asset";
            Mesh mesh = LoadOrCreateMesh(path, "RC_Boulder");
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] v =
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
                new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var rng = new System.Random(77);
            var jitter = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = v[i].normalized * (0.42f + 0.1f * (float)rng.NextDouble());
                p.y = Mathf.Max(p.y * 0.8f, -0.18f); // squashed, flat underneath so it sits in the ground
                jitter[i] = p;
            }

            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = jitter[f[i]], b = jitter[f[i + 1]], c = jitter[f[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f) < 0f)
                {
                    (b, c) = (c, b); // wind every face so that its normal (Unity: clockwise) points away from the centre
                }

                foreach (Vector3 corner in new[] { a, b, c })
                {
                    tris.Add(verts.Count);
                    verts.Add(corner);
                }
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>A rock pillar / butte: a hexagonal prism, 1 m wide and 1 m tall, narrower at the top (16 triangles, flat shaded). Scale it tall.</summary>
        public static Mesh PillarMesh()
        {
            string path = MeshFolder + "/RC_Pillar.asset";
            Mesh mesh = LoadOrCreateMesh(path, "RC_Pillar");
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const int sides = 6;
            var rng = new System.Random(5);
            Vector3[] bottom = new Vector3[sides], top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                float jitter = 0.9f + 0.2f * (float)rng.NextDouble();
                bottom[i] = new Vector3(Mathf.Cos(a) * 0.5f * jitter, 0f, Mathf.Sin(a) * 0.5f * jitter);
                top[i] = new Vector3(Mathf.Cos(a) * 0.34f, 1f, Mathf.Sin(a) * 0.34f);
            }

            for (int i = 0; i < sides; i++)
            {
                int n = (i + 1) % sides;
                AddQuad(verts, tris, bottom[i], top[i], top[n], bottom[n]);
            }

            for (int i = 1; i < sides - 1; i++) // top cap
            {
                AddTri(verts, tris, top[0], top[i + 1], top[i]);
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            tris.Add(verts.Count); verts.Add(a);
            tris.Add(verts.Count); verts.Add(b);
            tris.Add(verts.Count); verts.Add(c);
        }

        private static void AddQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddTri(verts, tris, a, b, c);
            AddTri(verts, tris, a, c, d);
        }

        private static Mesh LoadOrCreateMesh(string path, string name)
        {
            Directory.CreateDirectory(MeshFolder);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            return mesh;
        }

        // ================================================================ materials

        public static Material Lit(string name, Color color, Texture2D map = null)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetTexture("_BaseMap", map);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material Sky(Texture2D sky)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = MaterialFolder + "/RC_Sky.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Panoramic")) { name = "RC_Sky" };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_MainTex", sky);
            mat.SetFloat("_Mapping", 1f);
            mat.SetFloat("_ImageType", 0f);
            mat.SetFloat("_Exposure", 1f);
            mat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
#endif
