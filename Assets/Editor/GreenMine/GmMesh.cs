// Part of the DISABLED Green Mine generator: see the header of GreenMineSceneBuilder.cs.
#if GREEN_MINE_GENERATOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools.GreenMine
{
    /// <summary>
    /// Flat-shaded low-poly mesh builder for the Green Mine props. Shapes are appended with a material key and baked into ONE mesh with
    /// one sub-mesh per material, so every prop is a single renderer. Solid shapes are oriented outwards automatically (<see cref="TriOut"/>),
    /// so no winding has to be worked out by hand.
    /// </summary>
    internal sealed class GmMesh
    {
        private readonly List<Vector3> verts = new();
        private readonly List<Vector3> normals = new();
        private readonly List<Vector2> uvs = new();
        private readonly Dictionary<string, List<int>> tris = new();
        private readonly List<string> order = new();
        private readonly Stack<Matrix4x4> stack = new();
        private Matrix4x4 m = Matrix4x4.identity;

        /// <summary>Maps a mesh-space point to a UV. Default: planar (x, z).</summary>
        public Func<Vector3, Vector2> UvMap;

        public int TriangleCount { get; private set; }
        public IReadOnlyList<string> Keys => order;

        // ================================================================ transform stack

        public void Push(Vector3 position, Vector3 euler = default, Vector3? scale = null)
        {
            stack.Push(m);
            m *= Matrix4x4.TRS(position, Quaternion.Euler(euler), scale ?? Vector3.one);
        }

        public void Pop()
        {
            m = stack.Pop();
        }

        private Vector3 T(Vector3 p) => m.MultiplyPoint3x4(p);

        // ================================================================ triangles

        private void Emit(string mat, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f)
            {
                return;
            }

            n.Normalize();
            if (!tris.TryGetValue(mat, out List<int> list))
            {
                list = new List<int>();
                tris[mat] = list;
                order.Add(mat);
            }

            int i = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            uvs.Add(Uv(a));
            uvs.Add(Uv(b));
            uvs.Add(Uv(c));
            list.Add(i);
            list.Add(i + 1);
            list.Add(i + 2);
            TriangleCount++;
        }

        private Vector2 Uv(Vector3 p) => UvMap != null ? UvMap(p) : new Vector2(p.x, p.z);

        /// <summary>One triangle with the winding as given (Unity: clockwise seen from the front).</summary>
        public void Tri(string mat, Vector3 a, Vector3 b, Vector3 c) => Emit(mat, T(a), T(b), T(c));

        /// <summary>One triangle that faces away from <paramref name="inside"/> (any point inside the solid).</summary>
        public void TriOut(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            a = T(a);
            b = T(b);
            c = T(c);
            inside = T(inside);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - inside) < 0f)
            {
                (b, c) = (c, b);
            }

            Emit(mat, a, b, c);
        }

        public void Quad(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Tri(mat, a, b, c);
            Tri(mat, a, c, d);
        }

        public void QuadOut(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
        {
            TriOut(mat, a, b, c, inside);
            TriOut(mat, a, c, d, inside);
        }

        // ================================================================ solids

        public void Box(string mat, Vector3 center, Vector3 size, Vector3 euler = default)
        {
            Push(center, euler);
            Vector3 h = size * 0.5f;
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                p[i] = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            }

            Vector3 o = Vector3.zero;
            QuadOut(mat, p[0], p[1], p[3], p[2], o); // -z
            QuadOut(mat, p[4], p[5], p[7], p[6], o); // +z
            QuadOut(mat, p[0], p[2], p[6], p[4], o); // -x
            QuadOut(mat, p[1], p[3], p[7], p[5], o); // +x
            QuadOut(mat, p[0], p[1], p[5], p[4], o); // -y
            QuadOut(mat, p[2], p[3], p[7], p[6], o); // +y
            Pop();
        }

        /// <summary>A box between two points (rails, braces, fence rails). <paramref name="w"/> / <paramref name="h"/> are the cross-section.</summary>
        public void Beam(string mat, Vector3 from, Vector3 to, float w, float h)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 1e-4f)
            {
                return;
            }

            Vector3 up = Mathf.Abs(Vector3.Dot(dir / length, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            Quaternion q = Quaternion.LookRotation(dir / length, up);
            Push((from + to) * 0.5f, q.eulerAngles);
            Box(mat, Vector3.zero, new Vector3(w, h, length));
            Pop();
        }

        /// <summary>Cylinder / cone (r1 = 0) standing on <paramref name="baseCenter"/>, along local +Y.</summary>
        public void Prism(string mat, Vector3 baseCenter, float r0, float r1, float height, int sides, Vector3 euler = default, bool caps = true, float twist = 0f)
        {
            Push(baseCenter, euler);
            var ring0 = new Vector3[sides];
            var ring1 = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = twist + i * Mathf.PI * 2f / sides;
                ring0[i] = new Vector3(Mathf.Cos(a) * r0, 0f, Mathf.Sin(a) * r0);
                ring1[i] = new Vector3(Mathf.Cos(a) * r1, height, Mathf.Sin(a) * r1);
            }

            var inside = new Vector3(0f, height * 0.5f, 0f);
            var apex = new Vector3(0f, height, 0f);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                if (r1 <= 0.0001f)
                {
                    TriOut(mat, ring0[i], ring0[j], apex, inside);
                }
                else
                {
                    TriOut(mat, ring0[i], ring0[j], ring1[j], inside);
                    TriOut(mat, ring0[i], ring1[j], ring1[i], inside);
                    if (caps)
                    {
                        TriOut(mat, apex, ring1[i], ring1[j], inside);
                    }
                }

                if (caps && r0 > 0.0001f)
                {
                    TriOut(mat, Vector3.zero, ring0[j], ring0[i], inside);
                }
            }

            Pop();
        }

        /// <summary>Lumpy faceted ball (rocks, foliage, gold heaps). <paramref name="flatBottom"/> 0..1 cuts the lowest part flat.</summary>
        public void Blob(string mat, Vector3 center, Vector3 radii, int seed, float rough = 0.25f, int subdiv = 0, float flatBottom = 0f, Vector3 euler = default)
        {
            Push(center, euler);
            Ico ico = Ico.Get(subdiv);
            var pts = new Vector3[ico.Verts.Count];
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 d = ico.Verts[i];
                float f = 1f - rough * 0.5f + Noise(d, seed) * rough;
                Vector3 p = Vector3.Scale(d * f, radii);
                if (flatBottom > 0f && p.y < -flatBottom * radii.y)
                {
                    p.y = -flatBottom * radii.y;
                }

                pts[i] = p;
            }

            foreach (int[] f in ico.Faces)
            {
                TriOut(mat, pts[f[0]], pts[f[1]], pts[f[2]], Vector3.zero);
            }

            Pop();
        }

        /// <summary>Convex polygon in the local XY plane, extruded along Z (signs, banners, planks).</summary>
        public void Extrude(string mat, IList<Vector2> poly, float thickness, Vector3 position, Vector3 euler = default, Vector3? scale = null)
        {
            Push(position, euler, scale);
            float z = thickness * 0.5f;
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in poly)
            {
                centroid += p;
            }

            centroid /= poly.Count;
            var cFront = new Vector3(centroid.x, centroid.y, z);
            var cBack = new Vector3(centroid.x, centroid.y, -z);
            for (int i = 0; i < poly.Count; i++)
            {
                int j = (i + 1) % poly.Count;
                Vector3 af = new(poly[i].x, poly[i].y, z), bf = new(poly[j].x, poly[j].y, z);
                Vector3 ab = new(poly[i].x, poly[i].y, -z), bb = new(poly[j].x, poly[j].y, -z);
                TriOut(mat, cFront, af, bf, Vector3.zero);
                TriOut(mat, cBack, ab, bb, Vector3.zero);
                QuadOut(mat, af, bf, bb, ab, Vector3.zero);
            }

            Pop();
        }

        // ================================================================ noise / icosphere

        public static float Noise(Vector3 d, int seed)
        {
            float s = seed * 1.37f + 5f;
            float a = Mathf.PerlinNoise(d.x * 1.9f + s, d.y * 1.9f + s * 0.5f + 5f);
            float b = Mathf.PerlinNoise(d.y * 1.9f + s * 1.3f + 5f, d.z * 1.9f + 16f);
            float c = Mathf.PerlinNoise(d.z * 1.9f + s * 0.7f + 5f, d.x * 1.9f + 28f);
            return (a + b + c) / 3f;
        }

        private sealed class Ico
        {
            private static readonly Dictionary<int, Ico> Cache = new();
            public readonly List<Vector3> Verts = new();
            public List<int[]> Faces = new();

            public static Ico Get(int subdiv)
            {
                if (Cache.TryGetValue(subdiv, out Ico cached))
                {
                    return cached;
                }

                var ico = new Ico();
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                Vector3[] baseVerts =
                {
                    new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
                    new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
                };
                foreach (Vector3 v in baseVerts)
                {
                    ico.Verts.Add(v.normalized);
                }

                ico.Faces.AddRange(new[]
                {
                    new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 },
                    new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
                    new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 },
                    new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 },
                });
                for (int s = 0; s < subdiv; s++)
                {
                    var midpoints = new Dictionary<long, int>();
                    int Mid(int a, int b)
                    {
                        long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                        if (midpoints.TryGetValue(key, out int found))
                        {
                            return found;
                        }

                        ico.Verts.Add(((ico.Verts[a] + ico.Verts[b]) * 0.5f).normalized);
                        midpoints[key] = ico.Verts.Count - 1;
                        return ico.Verts.Count - 1;
                    }

                    var next = new List<int[]>();
                    foreach (int[] f in ico.Faces)
                    {
                        int ab = Mid(f[0], f[1]), bc = Mid(f[1], f[2]), ca = Mid(f[2], f[0]);
                        next.Add(new[] { f[0], ab, ca });
                        next.Add(new[] { f[1], bc, ab });
                        next.Add(new[] { f[2], ca, bc });
                        next.Add(new[] { ab, bc, ca });
                    }

                    ico.Faces = next;
                }

                Cache[subdiv] = ico;
                return ico;
            }
        }

        // ================================================================ bake

        public Mesh Bake(string assetPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = Path.GetFileNameWithoutExtension(assetPath) };
                AssetDatabase.CreateAsset(mesh, assetPath);
            }

            mesh.Clear();
            if (verts.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = Mathf.Max(1, order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                mesh.SetTriangles(tris[order[i]], i);
            }

            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
#endif
