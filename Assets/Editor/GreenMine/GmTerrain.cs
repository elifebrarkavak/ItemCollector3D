// Part of the DISABLED Green Mine generator: see the header of GreenMineSceneBuilder.cs.
#if GREEN_MINE_GENERATOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static ItemCollector3D.EditorTools.GreenMine.GmLayout;

namespace ItemCollector3D.EditorTools.GreenMine
{
    /// <summary>Terrain mesh, its painted colour map, the river / waterfall meshes and their textures. One mesh + one 1024 texture for the whole ground.</summary>
    internal static class GmTerrain
    {
        public const string MeshFolder = "Assets/Art/GreenMine/Meshes";
        public const string TextureFolder = "Assets/Art/GreenMine/Textures";
        public const int Cells = 72; // 1 m cells over 72 m
        private static float[,] grid;

        public static void Reset()
        {
            grid = null;
        }

        private static void EnsureGrid()
        {
            if (grid != null)
            {
                return;
            }

            grid = new float[Cells + 1, Cells + 1];
            for (int i = 0; i <= Cells; i++)
            {
                for (int j = 0; j <= Cells; j++)
                {
                    grid[i, j] = Height(-TerrainHalf + i, -TerrainHalf + j);
                }
            }
        }

        /// <summary>Height of the TERRAIN MESH at (x, z): interpolated exactly like the triangles, so props sit on the real surface.</summary>
        public static float SurfaceHeight(float x, float z)
        {
            EnsureGrid();
            float fx = Mathf.Clamp(x + TerrainHalf, 0f, Cells - 0.001f);
            float fz = Mathf.Clamp(z + TerrainHalf, 0f, Cells - 0.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = grid[i, j], b = grid[i + 1, j], c = grid[i, j + 1], d = grid[i + 1, j + 1];
            return tx + tz <= 1f ? a + (b - a) * tx + (c - a) * tz : d + (c - d) * (1f - tx) + (b - d) * (1f - tz);
        }

        /// <summary>Slope (rise over run) of the terrain around (x, z).</summary>
        public static float Slope(float x, float z)
        {
            float dx = SurfaceHeight(x + 0.5f, z) - SurfaceHeight(x - 0.5f, z);
            float dz = SurfaceHeight(x, z + 0.5f) - SurfaceHeight(x, z - 0.5f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static Vector3 Ground(float x, float z) => new(x, SurfaceHeight(x, z), z);

        // ================================================================ terrain mesh

        public static Mesh BuildMesh()
        {
            EnsureGrid();
            Directory.CreateDirectory(MeshFolder);
            string path = MeshFolder + "/GreenMine_Terrain.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "GreenMine_Terrain" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            int n = Cells + 1;
            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    verts[i + j * n] = new Vector3(-TerrainHalf + i, grid[i, j], -TerrainHalf + j);
                    uvs[i + j * n] = new Vector2(i / (float)Cells, j / (float)Cells);
                }
            }

            var tris = new List<int>(Cells * Cells * 6);
            for (int j = 0; j < Cells; j++)
            {
                for (int i = 0; i < Cells; i++)
                {
                    int a = i + j * n, b = a + 1, c = a + n, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ================================================================ colour map

        private static readonly Color GrassDeep = new(0.30f, 0.60f, 0.17f);
        private static readonly Color GrassLight = new(0.47f, 0.76f, 0.21f);
        private static readonly Color GrassYellow = new(0.66f, 0.82f, 0.28f);
        private static readonly Color ForestFloor = new(0.20f, 0.46f, 0.17f);
        private static readonly Color DirtA = new(0.82f, 0.57f, 0.31f);
        private static readonly Color DirtB = new(0.72f, 0.47f, 0.25f);
        private static readonly Color Pebble = new(0.70f, 0.64f, 0.58f);
        private static readonly Color RockGround = new(0.62f, 0.55f, 0.47f);
        private static readonly Color RockCliff = new(0.56f, 0.53f, 0.60f);
        private static readonly Color Sand = new(0.80f, 0.70f, 0.48f);
        private static readonly Color BedColor = new(0.40f, 0.58f, 0.52f);

        public static Texture2D PaintColorMap(int size = 1024)
        {
            EnsureGrid();
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                float z = -TerrainHalf + (py + 0.5f) / size * (TerrainHalf * 2f);
                for (int px = 0; px < size; px++)
                {
                    float x = -TerrainHalf + (px + 0.5f) / size * (TerrainHalf * 2f);
                    pixels[px + py * size] = PaintPixel(x, z);
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return SavePng(tex, "GreenMine_TerrainColor", true, TextureWrapMode.Clamp);
        }

        private static Color32 PaintPixel(float x, float z)
        {
            float n1 = Mathf.PerlinNoise(x * 0.11f + 40f, z * 0.11f + 8f);
            float n2 = Mathf.PerlinNoise(x * 0.55f + 3f, z * 0.55f + 90f);
            float n3 = Mathf.PerlinNoise(x * 2.3f + 17f, z * 2.3f + 5f);

            Color c = Color.Lerp(GrassDeep, GrassLight, S(0.28f, 0.72f, n1));
            c = Color.Lerp(c, GrassYellow, 0.24f * S(0.55f, 0.85f, n2));
            c *= 0.95f + 0.1f * n3;

            // forest floor: darker under the rim forest and in the forest corner
            float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float forestCorner = 1f - S(4f, 12f, Vector2.Distance(new Vector2(x, z), ForestCenter));
            c = Color.Lerp(c, ForestFloor, Mathf.Max(S(15f, 24f, r) * 0.7f, forestCorner * 0.45f));

            float pd = PathDistance(x, z);
            float verge = S(PathHalfWidth - 0.1f, PathHalfWidth + 0.5f, pd) * (1f - S(PathHalfWidth + 0.5f, PathHalfWidth + 1.5f, pd));
            c = Color.Lerp(c, GrassDeep * 0.92f, verge * 0.35f);

            // rocky treasure area: pale gravel patches
            float rockyD = Vector2.Distance(new Vector2(x, z), RockyCenter) + (n1 - 0.5f) * 5f;
            float rockyW = 1f - S(4.5f, 9.5f, rockyD);
            Color gravel = Color.Lerp(RockGround, Pebble, S(0.45f, 0.7f, n2));
            c = Color.Lerp(c, gravel, rockyW * (0.55f + 0.35f * S(0.4f, 0.65f, n3)));

            // river banks and bed
            float rd = RiverDistance(x, z, out float half);
            float slope = Slope(x, z);
            float lowGround = 1f - S(0.5f, 1.6f, SurfaceHeight(x, z)); // the river is only painted where it really is: not up the rim hills it would cross
            c = Color.Lerp(c, Sand, (1f - S(half + 0.2f, half + 1.8f, rd + (n2 - 0.5f) * 0.8f)) * (1f - S(0.15f, 0.45f, slope)) * lowGround);
            c = Color.Lerp(c, BedColor, (1f - S(half - 0.5f, half + 0.3f, rd)) * lowGround);

            // trails, camp clearing and mine apron: warm dirt
            float edgeNoise = (n2 - 0.5f) * 0.55f + (n3 - 0.5f) * 0.25f;
            float dirtMask = 1f - S(PathHalfWidth - 0.25f, PathHalfWidth + 0.4f, pd + edgeNoise);
            float camp = 1f - S(3.6f, 5.8f, CampDistance(x, z) + (n2 - 0.5f) * 1.6f);
            float apron = Mathf.Sqrt(Mathf.Pow((x - ApronCenter.x) / 6.2f, 2f) + Mathf.Pow((z - ApronCenter.y) / 2.5f, 2f));
            float apronMask = 1f - S(0.75f, 1.15f, apron + (n2 - 0.5f) * 0.25f);
            float gate = 1f - S(1.8f, 3.4f, Vector2.Distance(new Vector2(x, z), GatePos) + (n2 - 0.5f));
            dirtMask = Mathf.Max(dirtMask, Mathf.Max(camp, Mathf.Max(apronMask, gate)));
            Color dirt = Color.Lerp(DirtA, DirtB, S(0.35f, 0.72f, n2));
            dirt *= 0.94f + 0.1f * n3;
            if (n3 > 0.80f)
            {
                dirt = Color.Lerp(dirt, Pebble, 0.45f);
            }

            c = Color.Lerp(c, dirt, dirtMask);

            // steep ground (cliffs, hillside) turns into rock
            Color cliff = Color.Lerp(RockCliff, RockCliff * 0.82f, S(0.4f, 0.7f, n2));
            c = Color.Lerp(c, cliff, S(0.55f, 1.15f, slope));

            c.a = 1f;
            return c;
        }

        // ================================================================ water textures

        public static Texture2D WaterTexture()
        {
            const int w = 128, h = 128;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f); // across the river
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w; // along the river (tiles)
                    float ripple = 0.5f + 0.5f * Mathf.Sin((u * 4f + 0.35f * Mathf.Sin(v * Mathf.PI * 4f)) * Mathf.PI * 2f);
                    ripple = S(0.72f, 0.95f, ripple) * (1f - Mathf.Abs(v - 0.5f) * 1.2f);
                    Color c = Color.Lerp(new Color(0.12f, 0.62f, 0.90f), new Color(0.34f, 0.82f, 0.98f), 0.35f + 0.25f * Mathf.Sin(v * Mathf.PI));
                    c = Color.Lerp(c, new Color(0.78f, 0.95f, 1f), ripple * 0.6f);
                    float foam = Mathf.Max(S(0.16f, 0.02f, v), S(0.84f, 0.98f, v));
                    c = Color.Lerp(c, Color.white, foam * 0.9f);
                    c.a = Mathf.Lerp(0.84f, 0.97f, foam);
                    pixels[x + y * w] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return SavePng(tex, "GreenMine_Water", true, TextureWrapMode.Repeat);
        }

        public static Texture2D WaterfallTexture()
        {
            const int w = 64, h = 128;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f); // 0 = bottom, 1 = top
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    float streak = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 5f + Mathf.Sin(v * 9f + u * 6f) * 0.8f);
                    streak = S(0.35f, 0.9f, streak);
                    Color c = Color.Lerp(new Color(0.40f, 0.78f, 0.98f), new Color(0.92f, 0.99f, 1f), streak);
                    c = Color.Lerp(c, Color.white, S(0.22f, 0f, v)); // foam at the bottom
                    float edge = Mathf.Min(S(0f, 0.12f, u), S(1f, 0.88f, u));
                    c.a = Mathf.Lerp(0.55f, 0.93f, edge) * (0.82f + 0.18f * streak);
                    pixels[x + y * w] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return SavePng(tex, "GreenMine_Waterfall", true, TextureWrapMode.Clamp);
        }

        /// <summary>
        /// Lat-long sky for the Panoramic skybox: light blue zenith, white-blue horizon, a few soft clouds, haze below the horizon. One 1024 x 512 texture, no
        /// procedural sky maths per pixel at runtime (reference art 2 has a bright sky with puffy clouds).
        /// </summary>
        public static Texture2D SkyTexture()
        {
            const int w = 1024, h = 512;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (h - 1f);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    Color c;
                    if (v >= 0.5f)
                    {
                        float t = (v - 0.5f) * 2f; // 0 horizon .. 1 zenith
                        c = Color.Lerp(new Color(0.86f, 0.95f, 1.00f), new Color(0.30f, 0.60f, 0.97f), Mathf.Pow(t, 0.62f));
                        // periodic noise around the circle so the clouds wrap without a seam
                        float a = u * Mathf.PI * 2f;
                        float cx = Mathf.Cos(a) * 2.2f, cz = Mathf.Sin(a) * 2.2f;
                        float n = Mathf.PerlinNoise(cx + 11f, cz + t * 3.2f + 7f) * 0.6f + Mathf.PerlinNoise(cx * 2.3f + 3f, cz * 2.3f + t * 7f + 1f) * 0.4f;
                        float cloud = S(0.56f, 0.74f, n) * S(0.03f, 0.16f, t) * (1f - S(0.55f, 0.95f, t));
                        c = Color.Lerp(c, Color.white, cloud * 0.92f);
                        c = Color.Lerp(c, new Color(0.93f, 0.97f, 1f), (1f - S(0f, 0.12f, t)) * 0.7f);
                    }
                    else
                    {
                        float t = (0.5f - v) * 2f;
                        c = Color.Lerp(new Color(0.86f, 0.95f, 1.00f), new Color(0.55f, 0.74f, 0.55f), S(0f, 0.5f, t));
                    }

                    c.a = 1f;
                    pixels[x + y * w] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return SavePng(tex, "GreenMine_Sky", false, TextureWrapMode.Repeat);
        }

        /// <summary>Radial falloff for the additive lantern / fire glow: black (no light) at the edge, so the quads never show a rim.</summary>
        public static Texture2D GlowTexture()
        {
            const int n = 64;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt(Mathf.Pow((x + 0.5f) / n * 2f - 1f, 2f) + Mathf.Pow((y + 0.5f) / n * 2f - 1f, 2f));
                    float v = Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f);
                    var c = new Color(v, v, v, v);
                    pixels[x + y * n] = c;
                }
            }

            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return SavePng(tex, "GreenMine_Glow", false, TextureWrapMode.Clamp);
        }

        private static Texture2D SavePng(Texture2D tex, string name, bool mipmaps, TextureWrapMode wrap)
        {
            Directory.CreateDirectory(TextureFolder);
            string path = $"{TextureFolder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = mipmaps;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 2;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ================================================================ water meshes

        /// <summary>The river as a ribbon on the (dense) centre line; u along the flow (tiling), v across (foam on both edges).</summary>
        public static Mesh BuildRiverMesh()
        {
            Vector3[] pts = RiverDense;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float length = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 dir = i < pts.Length - 1
                    ? new Vector2(pts[i + 1].x - pts[i].x, pts[i + 1].y - pts[i].y)
                    : new Vector2(pts[i].x - pts[i - 1].x, pts[i].y - pts[i - 1].y);
                dir.Normalize();
                Vector2 side = new(-dir.y, dir.x);
                float half = pts[i].z + 0.35f; // the water laps a little over the bed edge
                Vector2 c = new(pts[i].x, pts[i].y);
                Vector2 l = c + side * half, rgt = c - side * half;
                verts.Add(new Vector3(l.x, WaterLevel, l.y));
                verts.Add(new Vector3(rgt.x, WaterLevel, rgt.y));
                if (i > 0)
                {
                    length += Vector2.Distance(c, new Vector2(pts[i - 1].x, pts[i - 1].y));
                }

                uvs.Add(new Vector2(length / 5f, 0f));
                uvs.Add(new Vector2(length / 5f, 1f));
            }

            for (int i = 0; i < pts.Length - 1; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                // up-facing in Unity's clockwise convention; the ribbon may run either way, so orient per quad
                if (Vector3.Cross(verts[c] - verts[a], verts[b] - verts[a]).y >= 0f)
                {
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
                else
                {
                    tris.AddRange(new[] { a, b, c, b, d, c });
                }
            }

            return StoreMesh("GreenMine_River", verts, uvs, tris);
        }

        /// <summary>A ribbon that hugs the cliff face from the top of the hill down to the pool.</summary>
        public static Mesh BuildWaterfallMesh(float x, float zBottom, float zTop, float halfWidth)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int rows = 14;
            int cols = 3;
            for (int r = 0; r <= rows; r++)
            {
                float t = r / (float)rows;
                float z = Mathf.Lerp(zBottom, zTop, t);
                for (int c = 0; c <= cols; c++)
                {
                    float u = c / (float)cols;
                    float px = x + Mathf.Lerp(-halfWidth, halfWidth, u);
                    float y = SurfaceHeight(px, z);
                    verts.Add(new Vector3(px, y + 0.22f, z - 0.12f));
                    uvs.Add(new Vector2(u, t));
                }
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int a = r * (cols + 1) + c, b = a + 1, d = a + cols + 1, e = d + 1;
                    // the fall faces -z (towards the player): clockwise seen from -z
                    tris.AddRange(new[] { a, d, b, b, d, e });
                }
            }

            return StoreMesh("GreenMine_Waterfall", verts, uvs, tris);
        }

        private static Mesh StoreMesh(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            Directory.CreateDirectory(MeshFolder);
            string path = $"{MeshFolder}/{name}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
#endif
