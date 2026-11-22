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
        public const string DressingFolder = MeshFolder + "/Dressing";

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

        // the sandstone ramp, dark red at the bottom to cream at the top: a rock layer picks ONE of these (hard-edged bands, like real strata)
        private static readonly Color[] Strata =
        {
            new(0.44f, 0.20f, 0.14f), new(0.62f, 0.26f, 0.17f), new(0.73f, 0.34f, 0.20f), new(0.85f, 0.46f, 0.24f),
            new(0.91f, 0.57f, 0.30f), new(0.93f, 0.73f, 0.48f), new(0.96f, 0.84f, 0.63f),
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
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>GLSL-style smoothstep(edge0, edge1, x) (also with edge0 &gt; edge1: 1 below edge1, 0 above edge0).</summary>
        private static float Ss(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
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

        /// <summary>Layer boundaries every 0.55-1.9 m: returns the layer index and the distance (m) above its lower boundary.</summary>
        private static int Layer(float h, out float aboveBoundary)
        {
            float y = -6f;
            int i = 0;
            while (true)
            {
                float thickness = 0.55f + 1.35f * Hash01(i, 5, 71);
                if (h < y + thickness || i > 60)
                {
                    aboveBoundary = h - y;
                    return i;
                }

                y += thickness;
                i++;
            }
        }

        private static float ZoneTone(RcZone zone)
        {
            switch (zone)
            {
                case RcZone.Ravine: return -0.22f;     // deeper, redder walls: the hidden place
                case RcZone.Riverbed: return 0.0f;
                case RcZone.Outpost: return 0.08f;
                case RcZone.Railway: return 0.04f;
                case RcZone.Overlook: return 0.14f;    // pale, sunny sandstone
                default: return 0.05f;
            }
        }

        private static Color32 Texel(float x, float z, int px, int py)
        {
            float height = RcLayout.Surface(x, z);
            float slope = RcLayout.Slope(x, z);
            RcLayout.Height(x, z, out RcZone zone, out float floor);
            RcZone near = RcLayout.NearestZone(x, z);
            float edge = RcLayout.EdgeDistance(x, z);
            float n1 = Mathf.PerlinNoise(x * 0.11f + 10f, z * 0.11f + 30f);
            float n2 = Mathf.PerlinNoise(x * 0.37f + 50f, z * 0.37f + 70f);
            float n3 = Mathf.PerlinNoise(x * 1.3f + 5f, z * 1.3f + 9f);
            float speck = Hash01(px / 2, py / 2, 11);

            // ---- floor colour by area (sand, pebbles, ballast, worn trail)
            Color ground;
            switch (zone)
            {
                case RcZone.Outpost:
                    ground = Color.Lerp(new Color(0.95f, 0.83f, 0.56f), new Color(0.89f, 0.73f, 0.48f), Ss(0.30f, 0.75f, n1));
                    ground = Color.Lerp(ground, new Color(0.78f, 0.62f, 0.42f), speck > 0.9f ? 0.5f : 0f);
                    ground = Color.Lerp(ground, new Color(0.76f, 0.56f, 0.38f), Ss(1.1f, 0.3f, OutpostTrailDistance(x, z)) * 0.7f); // worn footpaths
                    break;
                case RcZone.Riverbed:
                    ground = Color.Lerp(new Color(0.94f, 0.85f, 0.65f), new Color(0.86f, 0.72f, 0.51f), Mathf.Clamp01(1f - floor / 1.6f) * 0.7f);
                    ground = Color.Lerp(ground, new Color(0.80f, 0.66f, 0.47f), Mathf.Clamp01(Mathf.Sin(x * 1.7f + n1 * 9f + z * 0.6f) * 0.5f + 0.5f) * 0.28f); // wind ripples
                    ground = Color.Lerp(ground, speck > 0.5f ? new Color(0.58f, 0.48f, 0.40f) : new Color(0.95f, 0.89f, 0.78f), n2 > 0.58f ? 0.5f : 0f); // pebbles
                    ground = Color.Lerp(ground, new Color(0.78f, 0.62f, 0.44f), Ss(2.4f, 0.4f, TrailDistance("Riverbed", x, z) + 1.2f * (n1 - 0.5f)) * 0.5f); // the old channel: darker, packed sand
                    break;
                case RcZone.Railway:
                    ground = Color.Lerp(new Color(0.76f, 0.62f, 0.46f), new Color(0.66f, 0.52f, 0.39f), n2);
                    ground = Color.Lerp(ground, new Color(0.45f, 0.36f, 0.30f), speck > 0.78f ? 0.6f : 0f); // ballast
                    break;
                case RcZone.Overlook:
                    ground = Color.Lerp(new Color(0.96f, 0.85f, 0.60f), new Color(0.91f, 0.76f, 0.52f), n1);
                    ground = Color.Lerp(ground, new Color(0.80f, 0.62f, 0.42f), Ss(0.9f, 0.1f, TrailDistance("OverlookPath", x, z)) * 0.55f); // the worn path
                    break;
                case RcZone.Ravine:
                    ground = Color.Lerp(new Color(0.94f, 0.74f, 0.45f), new Color(0.88f, 0.62f, 0.36f), n1);
                    ground = Color.Lerp(ground, new Color(0.72f, 0.45f, 0.30f), speck > 0.85f ? 0.5f : 0f);
                    ground = Color.Lerp(ground, new Color(0.80f, 0.52f, 0.33f), Ss(1.8f, 0.3f, TrailDistance("Ravine", x, z)) * 0.5f);
                    break;
                default:
                    ground = Color.Lerp(new Color(0.84f, 0.64f, 0.44f), new Color(0.74f, 0.50f, 0.34f), Ss(0.3f, 0.7f, n1)); // the rim above the walls
                    break;
            }

            ground = Color.Lerp(ground, new Color(0.86f, 0.60f, 0.36f), 0.24f); // a warmer, slightly deeper desert sand (the sun is strong)

            // ---- rock faces: hard-edged horizontal layers that dip a little with the noise, a dark line under every layer, vertical weathering streaks
            float h = height + (n1 - 0.5f) * 1.6f + x * 0.03f;
            int layer = Layer(h, out float above);
            float tone = Mathf.Clamp01(0.42f * Hash01(layer, 3, 13) + 0.45f * Mathf.Clamp01(height / 20f) + 0.13f * n2 + ZoneTone(near));
            Color rock = Strata[Mathf.Clamp(Mathf.FloorToInt(tone * Strata.Length), 0, Strata.Length - 1)];
            float shadowLine = Ss(0.26f, 0.0f, above);
            rock = Color.Lerp(rock, rock * 0.72f, shadowLine * 0.85f);
            float streak = Mathf.PerlinNoise((x + z) * 2.6f + 3f, (x - z) * 0.35f + 7f);
            rock *= Mathf.Lerp(0.86f, 1.06f, streak);
            rock *= Mathf.Lerp(0.95f, 1.04f, n3);

            // ---- the foot of a wall: fallen scree
            float scree = Ss(0f, 0.8f, edge) * Ss(3.2f, 0.8f, edge) * Ss(0.62f, 0.25f, slope);
            Color screeColor = speck > 0.55f ? Color.Lerp(rock, new Color(0.66f, 0.50f, 0.40f), 0.55f) : Color.Lerp(ground, rock, 0.5f);
            float wallness = Ss(0.32f, 0.78f, slope);
            Color c = Color.Lerp(ground, rock, wallness);
            c = Color.Lerp(c, screeColor, scree * 0.45f * (1f - wallness));

            // ---- the cap of the rock mass: flat, light, sun-baked sandstone with a lumpy pattern
            if (zone == RcZone.None && slope < 0.34f && edge > 0.5f)
            {
                Color top = Color.Lerp(new Color(0.88f, 0.62f, 0.40f), new Color(0.93f, 0.74f, 0.50f), n1);
                top = Color.Lerp(top, new Color(0.74f, 0.45f, 0.30f), Ss(0.55f, 0.8f, n3) * 0.6f);
                c = Color.Lerp(c, top, 1f - wallness);
            }

            // ---- a dark crease where the floor meets the wall (cheap ambient occlusion)
            c *= 1f - 0.16f * Ss(1.0f, 0f, Mathf.Abs(edge + 0.1f)) * Ss(0.3f, 0.7f, slope + 0.2f);
            c.a = 1f;
            return c;
        }

        private static float SegmentDistance(float ax, float az, float bx, float bz, float x, float z)
        {
            float dx = bx - ax, dz = bz - az;
            float t = Mathf.Clamp01(((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz));
            float cx = ax + dx * t - x, cz = az + dz * t - z;
            return Mathf.Sqrt(cx * cx + cz * cz);
        }

        /// <summary>Distance (m) to the centre line of a layout shape (for the worn-trail tints of the overlook path, the riverbed's thalweg and the ravine path).</summary>
        private static float TrailDistance(string shapeName, float x, float z)
        {
            foreach (RcLayout.Shape s in RcLayout.Shapes)
            {
                if (s.Name != shapeName)
                {
                    continue;
                }

                float best = float.MaxValue;
                Vector3[] p = s.Points;
                for (int i = 0; i < p.Length - 1; i++)
                {
                    best = Mathf.Min(best, SegmentDistance(p[i].x, p[i].z, p[i + 1].x, p[i + 1].z, x, z));
                }

                return best;
            }

            return 99f;
        }

        // the outpost's footpaths (worn earth between the shack door, the camp fire, the shed, the water tower and the two ways out)
        private static readonly Vector2[][] OutpostTrails =
        {
            new[] { new Vector2(-37.5f, -28.2f), new Vector2(-35f, -25.6f), new Vector2(-32f, -24.2f), new Vector2(-29f, -22.8f), new Vector2(-25.5f, -21.6f), new Vector2(-20f, -20f) },
            new[] { new Vector2(-33f, -24.6f), new Vector2(-30.5f, -26.5f), new Vector2(-28f, -27.6f) },
            new[] { new Vector2(-34.5f, -24.5f), new Vector2(-33.8f, -19.5f), new Vector2(-32.4f, -14f) },
            new[] { new Vector2(-37f, -22.5f), new Vector2(-39.5f, -18.5f), new Vector2(-40.8f, -16.4f) },
        };

        private static float OutpostTrailDistance(float x, float z)
        {
            float best = float.MaxValue;
            foreach (Vector2[] t in OutpostTrails)
            {
                for (int i = 0; i < t.Length - 1; i++)
                {
                    best = Mathf.Min(best, SegmentDistance(t[i].x, t[i].y, t[i + 1].x, t[i + 1].y, x, z));
                }
            }

            return best;
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
                    c = Color.Lerp(new Color(0.99f, 0.84f, 0.64f), new Color(0.36f, 0.62f, 0.92f), Mathf.Pow(u, 0.5f)); // warm horizon to a clear blue
                }

                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    float glow = Mathf.Exp(-Mathf.Pow((u - 0.72f) * 7f, 2f)) * Mathf.Exp(-Mathf.Pow((t - 0.62f) * 4.5f, 2f)) * 0.22f; // the sun's glow
                    Color px = c + new Color(glow, glow * 0.8f, glow * 0.45f);
                    // a few soft, flat clouds high above the horizon: a clear sky with some character
                    float cloud = 0f;
                    for (int k = 0; k < 7; k++)
                    {
                        float cx = (0.07f + k * 0.143f + 0.05f * Hash01(k, 1, 3)) % 1f, ct = 0.60f + 0.20f * Hash01(k, 2, 3);
                        float rx = 0.05f + 0.06f * Hash01(k, 3, 3), rt = 0.012f + 0.018f * Hash01(k, 4, 3);
                        float dx = Mathf.Min(Mathf.Abs(u - cx), 1f - Mathf.Abs(u - cx)) / rx, dt = (t - ct) / rt;
                        float d2 = dx * dx + dt * dt;
                        cloud = Mathf.Max(cloud, Ss(1f, 0.25f, d2));
                    }

                    pixels[y * w + x] = Color.Lerp(px, new Color(1f, 0.97f, 0.92f), cloud * 0.8f);
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

        public static Material PaletteMaterial(Texture2D palette) => Lit("RC_Palette", Color.white, palette);

        /// <summary>The same palette, but self-lit (gold veins, camp fire, lantern glass): the only emissive material, no transparency.</summary>
        public static Material GlowMaterial(Texture2D palette)
        {
            Material mat = Lit("RC_Glow", Color.white, palette);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.62f, 0.54f, 0.42f));
            mat.SetTexture("_EmissionMap", palette);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
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
