#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>Colours of the ONE palette texture every Rocky Canyon prop uses (one material, one draw call group): a face samples the centre of its swatch.</summary>
    public enum P
    {
        // rock
        RockDeep, RockRed, RockRed2, RockOrange, RockOrange2, RockSand, RockCream, RockDark, RockShade, RockFar1, RockFar2, RockFar3, Rim,
        // wood
        WoodLight, Wood, WoodDark, WoodGrey, WoodBleach,
        // metal and cloth
        Iron, IronLight, Rust, RustDark, Canvas, CanvasDark, ClothRed, ClothTeal,
        // plants
        Cactus, CactusDark, CactusLight, Grass, GrassDark, Shrub, ShrubDark, Flower,
        // misc
        Gold, GoldBright, Flame, FlameCore, Coal, Ash, Bone, Black, Pebble, PebbleLight, SandBright, White, Glass,
    }

    public static class RcPalette
    {
        public const int Columns = 16;
        public const int Cell = 8;

        public static Color Color(P p)
        {
            switch (p)
            {
                case P.RockDeep: return new Color(0.42f, 0.19f, 0.13f);
                case P.RockRed: return new Color(0.62f, 0.26f, 0.17f);
                case P.RockRed2: return new Color(0.72f, 0.33f, 0.20f);
                case P.RockOrange: return new Color(0.84f, 0.45f, 0.23f);
                case P.RockOrange2: return new Color(0.90f, 0.55f, 0.29f);
                case P.RockSand: return new Color(0.92f, 0.72f, 0.47f);
                case P.RockCream: return new Color(0.96f, 0.84f, 0.62f);
                case P.RockDark: return new Color(0.36f, 0.21f, 0.16f);
                case P.RockShade: return new Color(0.22f, 0.13f, 0.10f);
                case P.RockFar1: return new Color(0.82f, 0.56f, 0.42f);
                case P.RockFar2: return new Color(0.87f, 0.66f, 0.50f);
                case P.RockFar3: return new Color(0.91f, 0.75f, 0.60f);
                case P.Rim: return new Color(0.80f, 0.62f, 0.44f);
                case P.WoodLight: return new Color(0.74f, 0.52f, 0.31f);
                case P.Wood: return new Color(0.60f, 0.40f, 0.23f);
                case P.WoodDark: return new Color(0.41f, 0.27f, 0.17f);
                case P.WoodGrey: return new Color(0.55f, 0.47f, 0.40f);
                case P.WoodBleach: return new Color(0.78f, 0.68f, 0.52f);
                case P.Iron: return new Color(0.30f, 0.31f, 0.34f);
                case P.IronLight: return new Color(0.52f, 0.53f, 0.55f);
                case P.Rust: return new Color(0.66f, 0.34f, 0.18f);
                case P.RustDark: return new Color(0.48f, 0.24f, 0.13f);
                case P.Canvas: return new Color(0.90f, 0.82f, 0.64f);
                case P.CanvasDark: return new Color(0.76f, 0.66f, 0.48f);
                case P.ClothRed: return new Color(0.74f, 0.22f, 0.16f);
                case P.ClothTeal: return new Color(0.22f, 0.50f, 0.52f);
                case P.Cactus: return new Color(0.38f, 0.60f, 0.32f);
                case P.CactusDark: return new Color(0.27f, 0.46f, 0.27f);
                case P.CactusLight: return new Color(0.55f, 0.72f, 0.42f);
                case P.Grass: return new Color(0.84f, 0.72f, 0.36f);
                case P.GrassDark: return new Color(0.68f, 0.54f, 0.26f);
                case P.Shrub: return new Color(0.55f, 0.54f, 0.30f);
                case P.ShrubDark: return new Color(0.40f, 0.42f, 0.24f);
                case P.Flower: return new Color(0.92f, 0.42f, 0.50f);
                case P.Gold: return new Color(0.98f, 0.78f, 0.22f);
                case P.GoldBright: return new Color(1.00f, 0.90f, 0.45f);
                case P.Flame: return new Color(1.00f, 0.55f, 0.15f);
                case P.FlameCore: return new Color(1.00f, 0.85f, 0.35f);
                case P.Coal: return new Color(0.17f, 0.14f, 0.14f);
                case P.Ash: return new Color(0.45f, 0.40f, 0.38f);
                case P.Bone: return new Color(0.92f, 0.88f, 0.78f);
                case P.Black: return new Color(0.10f, 0.09f, 0.09f);
                case P.Pebble: return new Color(0.62f, 0.54f, 0.48f);
                case P.PebbleLight: return new Color(0.82f, 0.74f, 0.64f);
                case P.SandBright: return new Color(0.95f, 0.83f, 0.58f);
                case P.White: return new Color(0.95f, 0.93f, 0.88f);
                case P.Glass: return new Color(1.00f, 0.80f, 0.40f);
                default: return UnityEngine.Color.magenta;
            }
        }

        public static int Count => System.Enum.GetValues(typeof(P)).Length;

        public static int Rows => (Count + Columns - 1) / Columns;

        public static Vector2 Uv(P p)
        {
            int i = (int)p;
            return new Vector2(((i % Columns) * Cell + Cell * 0.5f) / (Columns * Cell), ((i / Columns) * Cell + Cell * 0.5f) / (Rows * Cell));
        }

        public static Texture2D Build()
        {
            int w = Columns * Cell, h = Rows * Cell;
            var pixels = new Color32[w * h];
            for (int i = 0; i < Rows * Columns; i++)
            {
                Color c = i < Count ? Color((P)i) : UnityEngine.Color.magenta;
                int cx = (i % Columns) * Cell, cy = (i / Columns) * Cell;
                for (int y = 0; y < Cell; y++)
                {
                    for (int x = 0; x < Cell; x++)
                    {
                        pixels[(cy + y) * w + cx + x] = c;
                    }
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            Directory.CreateDirectory(RcAssets.TextureFolder);
            string path = RcAssets.TextureFolder + "/RockyCanyon_Palette.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    /// <summary>
    /// Builds one low-poly mesh out of simple solids (boxes, tapered boxes, n-gon prisms and cones, beams, faceted rocks, tiered buttes, ribbons). Every face has its own
    /// vertices (flat shading), a palette colour (the UV is the swatch centre) and an automatically correct winding: a face points away from the "inside" point of its solid.
    /// Faces go to submesh 0 (lit palette) or, while <see cref="Glow"/> is on, to submesh 1 (the emissive material). A transform stack positions solids.
    /// </summary>
    public sealed class RcMesh
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
        public void Face(P color, Vector3 inside, params Vector3[] p)
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
            Vector2 uv = RcPalette.Uv(color);
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

        // ------------------------------------------------------------------ solids

        /// <summary>An axis-aligned box (in the current local frame) with an optional different top and bottom colour.</summary>
        public void Box(Vector3 c, Vector3 size, P side, P? top = null, P? bottom = null)
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
        public void Block(Vector3 bottomCentre, Vector3 size, P side, P? top = null) => Box(bottomCentre + new Vector3(0f, size.y * 0.5f, 0f), size, side, top);

        /// <summary>A beam (box) from a to b with a w x h cross section; its h axis is as close to "up" as possible.</summary>
        public void Beam(Vector3 a, Vector3 b, float w, float h, P color)
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

        /// <summary>A tapered box: the bottom face is (bottom.x, bottom.y) wide and deep, the top face (top.x, top.y); the top can be shifted sideways.</summary>
        public void Frustum(Vector3 bottomCentre, Vector2 bottom, Vector2 top, float height, P side, P? topColor = null, Vector2 topShift = default)
        {
            Vector3[] lo = new Vector3[4], hi = new Vector3[4];
            float[] sx = { -1f, 1f, 1f, -1f }, sz = { -1f, -1f, 1f, 1f };
            for (int k = 0; k < 4; k++)
            {
                lo[k] = bottomCentre + new Vector3(sx[k] * bottom.x * 0.5f, 0f, sz[k] * bottom.y * 0.5f);
                hi[k] = bottomCentre + new Vector3(topShift.x + sx[k] * top.x * 0.5f, height, topShift.y + sz[k] * top.y * 0.5f);
            }

            Hexa(lo, hi, side, topColor);
        }

        /// <summary>Any convex hexahedron from 4 bottom and 4 top corners (same order round the axis).</summary>
        public void Hexa(Vector3[] lo, Vector3[] hi, P side, P? topColor = null, P? bottomColor = null)
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
        public void Poly(Vector3 bottomCentre, float rBottom, float rTop, float height, int sides, P side, P? topColor = null, float rotate = 0f, bool bottomCap = false, P? bottomColor = null)
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

        /// <summary>A cylinder lying between two points (barrel logs, pipes, wheels seen from the side).</summary>
        public void Pipe(Vector3 a, Vector3 b, float radius, int sides, P side, P? cap = null)
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

        // ------------------------------------------------------------------ organic solids

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

        /// <summary>
        /// A faceted rock / bush / sack: a jittered icosahedron (20 faces, or 80 with <paramref name="detail"/> = 1) scaled by <paramref name="radii"/>, flat on the bottom. Faces looking up get
        /// the top colour, faces on the lower side the bottom colour, the rest alternate between two colours in horizontal bands (stratified sandstone).
        /// </summary>
        public void Rock(Vector3 c, Vector3 radii, int seed, P main, P alt, P top, P? under = null, int detail = 0, float jitter = 0.20f, float flatBottom = 0.30f)
        {
            var tris = new List<Vector3[]>();
            for (int f = 0; f < IcoFaces.Length; f += 3)
            {
                Vector3 a = IcoVerts[IcoFaces[f]], b = IcoVerts[IcoFaces[f + 1]], d = IcoVerts[IcoFaces[f + 2]];
                if (detail <= 0)
                {
                    tris.Add(new[] { a, b, d });
                }
                else
                {
                    Vector3 ab = ((a + b) * 0.5f).normalized, bd = ((b + d) * 0.5f).normalized, da = ((d + a) * 0.5f).normalized;
                    tris.Add(new[] { a, ab, da });
                    tris.Add(new[] { ab, b, bd });
                    tris.Add(new[] { da, bd, d });
                    tris.Add(new[] { ab, bd, da });
                }
            }

            foreach (Vector3[] t in tris)
            {
                var p = new Vector3[3];
                Vector3 centroid = Vector3.zero;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 dir = t[k];
                    float r = 1f + (Hash(dir * 3.7f, seed) - 0.5f) * 2f * jitter;
                    Vector3 q = new Vector3(dir.x * r * radii.x, dir.y * r * radii.y, dir.z * r * radii.z);
                    q.y = Mathf.Max(q.y, -radii.y * flatBottom);
                    p[k] = c + q;
                    centroid += p[k];
                }

                centroid = (centroid / 3f - c);
                Vector3 n = Vector3.Cross(p[1] - p[0], p[2] - p[0]).normalized;
                if (Vector3.Dot(n, centroid) < 0f)
                {
                    n = -n;
                }

                P color;
                if (n.y > 0.55f)
                {
                    color = top;
                }
                else if (n.y < -0.35f && under.HasValue)
                {
                    color = under.Value;
                }
                else
                {
                    color = Mathf.FloorToInt((centroid.y / Mathf.Max(0.01f, radii.y) + 1f) * 1.6f + Hash(centroid, seed + 9) * 0.6f) % 2 == 0 ? main : alt;
                }

                Face(color, c, p[0], p[1], p[2]);
            }
        }

        /// <summary>
        /// A tiered butte / rock tower: stacked irregular n-gon prisms that narrow upwards, one colour per tier, light ledges between the tiers and an optional wider cap rock.
        /// <paramref name="colors"/> are used bottom to top (repeating); the tiers' heights add up to <paramref name="height"/>.
        /// </summary>
        public void Butte(Vector3 bottomCentre, float baseRadius, float height, int tiers, int seed, P[] colors, P ledge, int sides = 7, float topRadiusFactor = 0.62f, bool capRock = false)
        {
            float y = 0f;
            Vector3[] prevHi = null;
            float prevRadius = baseRadius;
            for (int tier = 0; tier < tiers; tier++)
            {
                float t = tier / (float)Mathf.Max(1, tiers - 1);
                float tierHeight = height / tiers * (0.75f + 0.5f * Hash(new Vector3(tier, 1f, 2f), seed));
                float rBottom = Mathf.Lerp(baseRadius, baseRadius * topRadiusFactor, t) * (tier == 0 ? 1f : 0.92f);
                float rTop = rBottom * 0.86f;
                bool cap = capRock && tier == tiers - 1 && tier > 0;
                Vector3 shift = new Vector3(Hash(new Vector3(tier, 7f, 1f), seed) - 0.5f, 0f, Hash(new Vector3(tier, 8f, 2f), seed) - 0.5f) * baseRadius * 0.35f;
                if (cap)
                {
                    rBottom = prevRadius * 1.18f;
                    rTop = prevRadius * 1.08f;
                    tierHeight = height * 0.14f;
                }

                var lo = new Vector3[sides];
                var hi = new Vector3[sides];
                for (int k = 0; k < sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    float j = 0.78f + 0.44f * Hash(new Vector3(k, tier, 3f), seed);
                    lo[k] = bottomCentre + shift + new Vector3(Mathf.Cos(a) * rBottom * j, y, Mathf.Sin(a) * rBottom * j);
                    hi[k] = bottomCentre + shift + new Vector3(Mathf.Cos(a) * rTop * j, y + tierHeight, Mathf.Sin(a) * rTop * j);
                }

                P color = colors[tier % colors.Length];
                Vector3 inside = bottomCentre + new Vector3(0f, y + tierHeight * 0.5f, 0f);
                for (int k = 0; k < sides; k++)
                {
                    int n = (k + 1) % sides;
                    Face(color, inside, lo[k], hi[k], hi[n], lo[n]);
                }

                if (prevHi != null)
                {
                    // the ring between the previous tier's top edge and this tier's bottom edge: a lit ledge, or the shaded underside of an overhanging cap
                    bool overhang = cap;
                    for (int k = 0; k < sides; k++)
                    {
                        int n = (k + 1) % sides;
                        Face(overhang ? P.RockShade : ledge, overhang ? inside + new Vector3(0f, 5f, 0f) : inside - new Vector3(0f, 5f, 0f), prevHi[k], lo[k], lo[n], prevHi[n]);
                    }
                }

                Face(ledge, inside, hi);
                prevHi = hi;
                prevRadius = rTop;
                y += tierHeight;
            }
        }

        /// <summary>A flat strip lying ON a surface: <paramref name="centres"/> are the centre line, <paramref name="normals"/> the surface normals there (the strip is lifted a little along them).</summary>
        public void Strip(IList<Vector3> centres, IList<Vector3> normals, IList<float> widths, P color, float lift = 0.07f)
        {
            for (int i = 0; i < centres.Count - 1; i++)
            {
                Vector3 d = (centres[i + 1] - centres[i]).normalized;
                Vector3 s0 = Vector3.Cross(normals[i], d).normalized * widths[i] * 0.5f;
                Vector3 s1 = Vector3.Cross(normals[i + 1], d).normalized * widths[i + 1] * 0.5f;
                Vector3 p0 = centres[i] + normals[i] * lift, p1 = centres[i + 1] + normals[i + 1] * lift;
                Face(color, centres[i] - normals[i] * 2f, p0 - s0, p0 + s0, p1 + s1, p1 - s1);
            }
        }

        // ------------------------------------------------------------------ output

        public Mesh ToMesh(string name)
        {
            Directory.CreateDirectory(RcAssets.DressingFolder);
            string path = RcAssets.DressingFolder + "/" + name + ".asset";
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
