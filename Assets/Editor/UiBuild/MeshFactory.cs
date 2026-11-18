using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools
{
    /// <summary>Procedural low-poly meshes (flat shaded) for the cave menu scenery. Saved as assets so scenes reference real meshes.</summary>
    public static class MeshFactory
    {
        public const string Folder = "Assets/Art/Menu/Meshes";

        private static Mesh Store(string name, List<Vector3> triVerts)
        {
            System.IO.Directory.CreateDirectory(Folder);
            string path = $"{Folder}/{name}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            var vertices = new List<Vector3>(triVerts.Count);
            var indices = new List<int>(triVerts.Count);
            var uvs = new List<Vector2>(triVerts.Count);
            for (int i = 0; i < triVerts.Count; i++)
            {
                vertices.Add(triVerts[i]);
                indices.Add(i);
                uvs.Add(new Vector2(triVerts[i].x, triVerts[i].z));
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static float Noise(Vector3 p, float seed)
        {
            float a = Mathf.PerlinNoise(p.x * 1.7f + seed, p.y * 1.7f + seed * 0.5f);
            float b = Mathf.PerlinNoise(p.y * 1.7f + seed * 1.3f, p.z * 1.7f + 11f);
            float c = Mathf.PerlinNoise(p.z * 1.7f + seed * 0.7f, p.x * 1.7f + 23f);
            return (a + b + c) / 3f;
        }

        /// <summary>Faceted rock: a displaced, flat-shaded icosphere (radius ~1).</summary>
        public static Mesh Rock(string name, int seed, float roughness = 0.38f, int subdivisions = 1)
        {
            var verts = new List<Vector3>();
            var faces = new List<int[]>();
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] baseVerts =
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
                new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            foreach (Vector3 v in baseVerts) { verts.Add(v.normalized); }
            int[][] baseFaces =
            {
                new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 },
                new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
                new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 },
                new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 },
            };
            faces.AddRange(baseFaces);

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

            for (int i = 0; i < verts.Count; i++)
            {
                float n = Noise(verts[i], seed * 3.17f);
                verts[i] *= 1f - roughness * 0.5f + n * roughness;
            }

            var tris = new List<Vector3>();
            foreach (int[] f in faces)
            {
                tris.Add(verts[f[0]]);
                tris.Add(verts[f[2]]);
                tris.Add(verts[f[1]]);
            }

            return Store(name, tris);
        }

        /// <summary>Faceted gem crystal along +Y (base at y = 0): six-sided prism with a pointed, slightly leaning tip.</summary>
        public static Mesh Crystal(string name, int seed, int sides = 6)
        {
            var random = new System.Random(seed);
            float Rand(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var tris = new List<Vector3>();
            float r0 = 0.5f, r1 = 0.46f, midY = 0.7f, topY = 1f;
            Vector3 tip = new Vector3(Rand(-0.08f, 0.08f), topY, Rand(-0.08f, 0.08f));
            var baseRing = new Vector3[sides];
            var midRing = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f + Rand(-0.08f, 0.08f);
                baseRing[i] = new Vector3(Mathf.Cos(a) * r0 * Rand(0.9f, 1.1f), 0f, Mathf.Sin(a) * r0 * Rand(0.9f, 1.1f));
                midRing[i] = new Vector3(Mathf.Cos(a) * r1 * Rand(0.88f, 1.05f), midY + Rand(-0.04f, 0.04f), Mathf.Sin(a) * r1 * Rand(0.88f, 1.05f));
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                tris.Add(baseRing[i]); tris.Add(midRing[j]); tris.Add(midRing[i]);
                tris.Add(baseRing[i]); tris.Add(baseRing[j]); tris.Add(midRing[j]);
                tris.Add(midRing[i]); tris.Add(midRing[j]); tris.Add(tip);
            }

            return Store(name, tris);
        }

        /// <summary>Gently uneven floor: a flat-shaded grid with small noise bumps (size x size, centred, y ~ 0).</summary>
        public static Mesh Floor(string name, float size, int cells, float bump)
        {
            var grid = new Vector3[cells + 1, cells + 1];
            for (int z = 0; z <= cells; z++)
            {
                for (int x = 0; x <= cells; x++)
                {
                    float px = (x / (float)cells - 0.5f) * size, pz = (z / (float)cells - 0.5f) * size;
                    float h = (Mathf.PerlinNoise(px * 0.35f + 7f, pz * 0.35f + 3f) - 0.5f) * bump;
                    grid[x, z] = new Vector3(px, h, pz);
                }
            }

            var tris = new List<Vector3>();
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    Vector3 a = grid[x, z], b = grid[x + 1, z], c = grid[x, z + 1], d = grid[x + 1, z + 1];
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            return Store(name, tris);
        }
    }
}
