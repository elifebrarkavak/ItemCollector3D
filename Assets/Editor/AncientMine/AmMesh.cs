#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>
    /// Builds one low-poly mesh out of simple solids (boxes, beams, n-gon prisms and cones, crystals, rubble heaps, arch rings, flat quads). Every face has its own vertices (flat
    /// shading), a palette swatch (the UV is the swatch centre) and an automatically correct winding: a face points away from the "inside" point of its solid. Faces go to
    /// submesh 0 (lit palette) or, while <see cref="Glow"/> is on, to submesh 1 (the emissive material). A transform stack positions solids.
    /// </summary>
    public sealed class AmMesh
    {
        private readonly List<Vector3> verts = new(), norms = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<int> lit = new(), glow = new();
        private readonly Stack<Matrix4x4> stack = new();
        private Matrix4x4 m = Matrix4x4.identity;

        /// <summary>Faces added while this is true use the emissive submesh.</summary>
        public bool Glow;

        public int Triangles => (lit.Count + glow.Count) / 3;

        public bool IsEmpty => verts.Count == 0;

        public bool HasGlow => glow.Count > 0;

        public void Push(Vector3 position, float yaw = 0f, float pitch = 0f, float roll = 0f) => Push(position, Quaternion.Euler(pitch, yaw, roll));

        public void Push(Vector3 position, Quaternion rotation)
        {
            stack.Push(m);
            m = m * Matrix4x4.TRS(position, rotation, Vector3.one);
        }

        public void Pop() => m = stack.Pop();

        // ------------------------------------------------------------------ faces

        /// <summary>A convex planar polygon given in local coordinates; it faces away from <paramref name="inside"/>.</summary>
        public void Face(int color, Vector3 inside, params Vector3[] p)
        {
            int k = p.Length;
            var w = new Vector3[k];
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < k; i++)
            {
                w[i] = m.MultiplyPoint3x4(p[i]);
                centroid += w[i];
            }

            centroid /= k;
            Vector3 normal = Vector3.Cross(w[1] - w[0], w[2] - w[0]);
            if (normal.sqrMagnitude < 1e-12f)
            {
                return;
            }

            bool flip = Vector3.Dot(normal, centroid - m.MultiplyPoint3x4(inside)) < 0f;
            normal = normal.normalized * (flip ? -1f : 1f);
            int start = verts.Count;
            Vector2 uv = P.Uv(color);
            for (int i = 0; i < k; i++)
            {
                verts.Add(w[i]);
                norms.Add(normal);
                uvs.Add(uv);
            }

            List<int> list = Glow ? glow : lit;
            for (int i = 1; i < k - 1; i++)
            {
                list.Add(start);
                list.Add(flip ? start + i + 1 : start + i);
                list.Add(flip ? start + i : start + i + 1);
            }
        }

        /// <summary>A world-space convex polygon with an explicit facing direction (cave shells, floor strips): <paramref name="facing"/> is where the front side looks.</summary>
        public void FaceWorld(int color, Vector3 facing, params Vector3[] p)
        {
            int k = p.Length;
            Vector3 normal = Vector3.zero; // Newell's method: stable for polygons with nearly collinear corners
            for (int i = 0; i < k; i++)
            {
                Vector3 a = p[i], b = p[(i + 1) % k];
                normal.x += (a.y - b.y) * (a.z + b.z);
                normal.y += (a.z - b.z) * (a.x + b.x);
                normal.z += (a.x - b.x) * (a.y + b.y);
            }

            if (normal.sqrMagnitude < 1e-10f)
            {
                return;
            }

            // Newell's sum equals cross(p1 - p0, p2 - p0) for a planar polygon, which is the FRONT normal of the winding as given (Unity front faces are clockwise)
            normal = normal.normalized;
            bool flip = Vector3.Dot(normal, facing) < 0f;
            normal = flip ? -normal : normal;
            int start = verts.Count;
            Vector2 uv = P.Uv(color);
            for (int i = 0; i < k; i++)
            {
                verts.Add(p[i]);
                norms.Add(normal);
                uvs.Add(uv);
            }

            List<int> list = Glow ? glow : lit;
            for (int i = 1; i < k - 1; i++)
            {
                list.Add(start);
                list.Add(flip ? start + i + 1 : start + i);
                list.Add(flip ? start + i : start + i + 1);
            }
        }

        // ------------------------------------------------------------------ solids

        /// <summary>A box (in the current local frame) with an optional different top and bottom colour.</summary>
        public void Box(Vector3 c, Vector3 size, int side, int? top = null, int? bottom = null)
        {
            Vector3 h = size * 0.5f;
            Vector3 a = c + new Vector3(-h.x, -h.y, -h.z), b = c + new Vector3(h.x, -h.y, -h.z), d = c + new Vector3(h.x, -h.y, h.z), e = c + new Vector3(-h.x, -h.y, h.z);
            Vector3 f = c + new Vector3(-h.x, h.y, -h.z), g = c + new Vector3(h.x, h.y, -h.z), i = c + new Vector3(h.x, h.y, h.z), j = c + new Vector3(-h.x, h.y, h.z);
            Face(top ?? side, c, f, j, i, g);
            Face(bottom ?? side, c, a, b, d, e);
            Face(side, c, a, f, g, b);
            Face(side, c, e, d, i, j);
            Face(side, c, a, e, j, f);
            Face(side, c, b, g, i, d);
        }

        /// <summary>A box standing on <paramref name="bottomCentre"/> (the middle of its bottom face).</summary>
        public void Block(Vector3 bottomCentre, Vector3 size, int side, int? top = null) => Box(bottomCentre + new Vector3(0f, size.y * 0.5f, 0f), size, side, top);

        /// <summary>A beam (box) from a to b with a w x h cross section; its h axis is as close to "up" as possible.</summary>
        public void Beam(Vector3 a, Vector3 b, float w, float h, int color)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            if (length < 1e-4f)
            {
                return;
            }

            Vector3 up = Mathf.Abs(Vector3.Dot(d / length, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            Push((a + b) * 0.5f, Quaternion.LookRotation(d / length, up));
            Box(Vector3.zero, new Vector3(w, h, length), color);
            Pop();
        }

        /// <summary>Any convex hexahedron from 4 bottom and 4 top corners (same order round the axis).</summary>
        public void Hexa(Vector3[] lo, Vector3[] hi, int side, int? topColor = null, int? bottomColor = null)
        {
            Vector3 inside = Vector3.zero;
            for (int k = 0; k < 4; k++)
            {
                inside += lo[k] + hi[k];
            }

            inside /= 8f;
            for (int k = 0; k < 4; k++)
            {
                int n = (k + 1) % 4;
                Face(side, inside, lo[k], hi[k], hi[n], lo[n]);
            }

            Face(topColor ?? side, inside, hi[0], hi[1], hi[2], hi[3]);
            if (bottomColor.HasValue)
            {
                Face(bottomColor.Value, inside, lo[0], lo[1], lo[2], lo[3]);
            }
        }

        /// <summary>An n-gon prism / cone / frustum along local y; rTop = 0 makes a cone. Optional bottom cap (needed when seen from below).</summary>
        public void Poly(Vector3 bottomCentre, float rBottom, float rTop, float height, int sides, int side, int? topColor = null, float rotate = 0f, bool bottomCap = false, int? bottomColor = null)
        {
            var lo = new Vector3[sides];
            var hi = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = (k / (float)sides * 360f + rotate) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                lo[k] = bottomCentre + new Vector3(cos * rBottom, 0f, sin * rBottom);
                hi[k] = bottomCentre + new Vector3(cos * rTop, height, sin * rTop);
            }

            Vector3 inside = bottomCentre + new Vector3(0f, height * 0.5f, 0f);
            for (int k = 0; k < sides; k++)
            {
                int n = (k + 1) % sides;
                if (rTop < 0.001f)
                {
                    Face(side, inside, lo[k], hi[k], lo[n]);
                }
                else
                {
                    Face(side, inside, lo[k], hi[k], hi[n], lo[n]);
                }
            }

            if (rTop >= 0.001f)
            {
                Face(topColor ?? side, inside, hi);
            }

            if (bottomCap)
            {
                Face(bottomColor ?? side, inside, lo);
            }
        }

        /// <summary>A cylinder lying between two points (stone wheels' axles, fallen column drums).</summary>
        public void Pipe(Vector3 a, Vector3 b, float radius, int sides, int side, int? cap = null)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            if (length < 1e-4f)
            {
                return;
            }

            Push(a, Quaternion.FromToRotation(Vector3.up, d / length));
            Poly(Vector3.zero, radius, radius, length, sides, side, cap ?? side, 0f, true, cap ?? side);
            Pop();
        }

        /// <summary>
        /// A crystal: a tapered n-gon prism topped by a pyramid tip, tilted by <paramref name="lean"/> degrees towards <paramref name="leanYaw"/>. Standing on <paramref name="bottomCentre"/>.
        /// </summary>
        public void Crystal(Vector3 bottomCentre, float radius, float height, int body, int tip, float lean = 0f, float leanYaw = 0f, int sides = 6, float spin = 0f)
        {
            Push(bottomCentre, Quaternion.Euler(0f, leanYaw, 0f) * Quaternion.Euler(lean, 0f, 0f));
            float shaft = height * 0.62f;
            var lo = new Vector3[sides];
            var hi = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = (k / (float)sides * 360f + spin) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                lo[k] = new Vector3(cos * radius, 0f, sin * radius);
                hi[k] = new Vector3(cos * radius * 0.82f, shaft, sin * radius * 0.82f);
            }

            Vector3 apex = new Vector3(0f, height, 0f);
            Vector3 inside = new Vector3(0f, shaft * 0.5f, 0f);
            for (int k = 0; k < sides; k++)
            {
                int n = (k + 1) % sides;
                Face(body, inside, lo[k], hi[k], hi[n], lo[n]);
                Face((k & 1) == 0 ? tip : body, inside, hi[k], apex, hi[n]);
            }

            Pop();
        }

        /// <summary>A faceted rubble heap / boulder: a jittered icosahedron scaled by <paramref name="radii"/>, flat on the bottom.</summary>
        public void Rock(Vector3 c, Vector3 radii, int seed, int main, int alt, int top, float jitter = 0.22f, float flatBottom = 0.3f)
        {
            for (int f = 0; f < IcoFaces.Length; f += 3)
            {
                var p = new Vector3[3];
                Vector3 centroid = Vector3.zero;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 dir = IcoVerts[IcoFaces[f + k]];
                    float r = 1f + (Hash(dir * 3.7f, seed) - 0.5f) * 2f * jitter;
                    var q = new Vector3(dir.x * r * radii.x, dir.y * r * radii.y, dir.z * r * radii.z);
                    q.y = Mathf.Max(q.y, -radii.y * flatBottom);
                    p[k] = c + q;
                    centroid += p[k];
                }

                centroid = centroid / 3f - c;
                Vector3 n = Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
                if (Vector3.Dot(n, centroid) < 0f)
                {
                    n = -n;
                }

                int color = n.y > 0.55f ? top : (Hash(centroid, seed + 9) > 0.5f ? main : alt);
                Face(color, c, p[0], p[1], p[2]);
            }
        }

        /// <summary>A half-round arch ring in the local x/y plane (opening width 2 * innerRadius, springing at y = 0), <paramref name="depth"/> thick along z.</summary>
        public void ArchRing(Vector3 centre, float innerRadius, float outerRadius, float depth, int segments, int color, int? altColor = null)
        {
            for (int k = 0; k < segments; k++)
            {
                float a0 = Mathf.PI * k / segments, a1 = Mathf.PI * (k + 1) / segments;
                Vector3 i0 = new(Mathf.Cos(a0) * innerRadius, Mathf.Sin(a0) * innerRadius, 0f), i1 = new(Mathf.Cos(a1) * innerRadius, Mathf.Sin(a1) * innerRadius, 0f);
                Vector3 o0 = new(Mathf.Cos(a0) * outerRadius, Mathf.Sin(a0) * outerRadius, 0f), o1 = new(Mathf.Cos(a1) * outerRadius, Mathf.Sin(a1) * outerRadius, 0f);
                Vector3 dz = new(0f, 0f, depth * 0.5f);
                int c = (k & 1) == 0 || !altColor.HasValue ? color : altColor.Value;
                Vector3 inside = centre + (i0 + i1 + o0 + o1) * 0.25f;
                Vector3 mid = (i0 + i1 + o0 + o1) * 0.25f;
                Vector3 inner = mid - (mid.normalized) * ((outerRadius - innerRadius) * 0.25f);
                inside = centre + inner;
                Face(c, inside, centre + i0 + dz, centre + o0 + dz, centre + o1 + dz, centre + i1 + dz);   // front
                Face(c, inside, centre + i0 - dz, centre + i1 - dz, centre + o1 - dz, centre + o0 - dz);   // back
                Face(c, inside, centre + i0 + dz, centre + i1 + dz, centre + i1 - dz, centre + i0 - dz);   // soffit (the underside you see walking through)
                Face(c, inside, centre + o0 + dz, centre + o0 - dz, centre + o1 - dz, centre + o1 + dz);   // top
            }
        }

        // ------------------------------------------------------------------ organic helper data

        private static float Hash(Vector3 p, int seed)
        {
            float s = Mathf.Sin(p.x * 12.9898f + p.y * 78.233f + p.z * 37.719f + seed * 4.1414f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        private static readonly Vector3[] IcoVerts = BuildIco();

        private static readonly int[] IcoFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        private static Vector3[] BuildIco()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] v =
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
                new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = v[i].normalized;
            }

            return v;
        }

        // ------------------------------------------------------------------ output

        public Mesh ToMesh(string name, string folder)
        {
            Directory.CreateDirectory(folder);
            string path = folder + "/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            if (verts.Count > 60000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = HasGlow ? 2 : 1;
            mesh.SetTriangles(lit, 0);
            if (HasGlow)
            {
                mesh.SetTriangles(glow, 1);
            }

            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
#endif
