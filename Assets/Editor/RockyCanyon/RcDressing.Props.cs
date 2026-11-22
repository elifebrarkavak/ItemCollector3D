#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>The prop library of the Rocky Canyon: every function adds faces to an <see cref="RcMesh"/> at a world position (bottom centre) with a yaw; none creates objects.</summary>
    public static partial class RcDressing
    {
        // ================================================================ camp goods

        private static void Crate(RcMesh rm, Vector3 pos, float s, float yaw)
        {
            rm.Push(pos, yaw);
            rm.Block(Vector3.zero, new Vector3(s, s, s), P.Wood, P.WoodLight);
            float t = s * 0.10f;
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    rm.Block(new Vector3(sx * s * 0.5f, 0f, sz * s * 0.5f), new Vector3(t, s * 1.01f, t), P.WoodDark);
                }
            }

            rm.Beam(new Vector3(-s * 0.5f, s * 0.14f, s * 0.5f + 0.02f), new Vector3(s * 0.5f, s * 0.86f, s * 0.5f + 0.02f), 0.03f, t * 0.9f, P.WoodDark);
            rm.Beam(new Vector3(s * 0.5f + 0.02f, s * 0.14f, -s * 0.5f), new Vector3(s * 0.5f + 0.02f, s * 0.86f, s * 0.5f), 0.03f, t * 0.9f, P.WoodDark);
            rm.Pop();
        }

        private static void Barrel(RcMesh rm, Vector3 pos, float r, float h, float yaw, P wood = P.Wood)
        {
            rm.Push(pos, yaw);
            rm.Poly(Vector3.zero, r * 0.84f, r, h * 0.5f, 8, wood, null, 11f, true, P.WoodDark);
            rm.Poly(new Vector3(0f, h * 0.5f, 0f), r, r * 0.84f, h * 0.5f, 8, wood, P.WoodDark, 11f);
            rm.Poly(new Vector3(0f, h * 0.18f, 0f), r * 0.94f, r * 0.94f, h * 0.07f, 8, P.Iron, null, 11f);
            rm.Poly(new Vector3(0f, h * 0.76f, 0f), r * 0.94f, r * 0.94f, h * 0.07f, 8, P.Iron, null, 11f);
            rm.Pop();
        }

        private static void Sack(RcMesh rm, Vector3 pos, float s, float yaw, P color = P.Canvas)
        {
            rm.Push(pos, yaw);
            rm.Rock(new Vector3(0f, 0.27f * s, 0f), new Vector3(0.34f * s, 0.30f * s, 0.28f * s), Seed(), color, P.CanvasDark, color, null, 0, 0.10f, 0.25f);
            rm.Poly(new Vector3(0f, 0.50f * s, 0f), 0.09f * s, 0.03f * s, 0.14f * s, 5, P.CanvasDark);
            rm.Pop();
        }

        private static void Post(RcMesh rm, Vector3 pos, float h, float thick, P color = P.WoodDark)
        {
            rm.Block(pos, new Vector3(thick, h, thick), color, P.WoodGrey);
        }

        /// <summary>A lamp post with a hanging lantern: the glass is emissive (no real light).</summary>
        private static void LanternPost(RcMesh rm, Vector3 pos, float yaw, float h = 2.1f)
        {
            rm.Push(pos, yaw);
            rm.Block(Vector3.zero, new Vector3(0.14f, h, 0.14f), P.WoodDark, P.WoodGrey);
            rm.Beam(new Vector3(0f, h - 0.05f, 0f), new Vector3(0.5f, h - 0.05f, 0f), 0.08f, 0.08f, P.WoodDark);
            rm.Beam(new Vector3(0.05f, h - 0.5f, 0f), new Vector3(0.5f, h - 0.05f, 0f), 0.06f, 0.06f, P.WoodDark);
            rm.Block(new Vector3(0.5f, h - 0.42f, 0f), new Vector3(0.22f, 0.06f, 0.22f), P.Iron);
            rm.Glow = true;
            rm.Box(new Vector3(0.5f, h - 0.27f, 0f), new Vector3(0.18f, 0.24f, 0.18f), P.Glass);
            rm.Glow = false;
            rm.Poly(new Vector3(0.5f, h - 0.15f, 0f), 0.16f, 0.03f, 0.12f, 4, P.Iron, null, 45f);
            rm.Pop();
        }

        /// <summary>A board on its own frame: used for signs. Faces +z; the frame is slightly larger and sits behind it.</summary>
        private static void Board(RcMesh rm, Vector3 centre, float yaw, float w, float h, P board, P frame)
        {
            rm.Push(centre, yaw);
            rm.Box(new Vector3(0f, 0f, -0.02f), new Vector3(w + 0.14f, h + 0.14f, 0.07f), frame);
            rm.Box(Vector3.zero, new Vector3(w, h, 0.07f), board);
            rm.Pop();
        }

        /// <summary>Two crossed pick axes on a plane facing +z (the miners' emblem).</summary>
        private static void Emblem(RcMesh rm, Vector3 centre, float yaw, float size)
        {
            rm.Push(centre, yaw);
            float s = size * 0.5f;
            rm.Beam(new Vector3(-s, -s, 0.05f), new Vector3(s, s, 0.05f), 0.04f * size, 0.05f * size, P.WoodDark);
            rm.Beam(new Vector3(s, -s, 0.06f), new Vector3(-s, s, 0.06f), 0.04f * size, 0.05f * size, P.WoodDark);
            rm.Beam(new Vector3(-s * 1.1f, s * 0.55f, 0.075f), new Vector3(-s * 0.2f, s * 1.1f, 0.075f), 0.05f * size, 0.11f * size, P.Iron);
            rm.Beam(new Vector3(s * 1.1f, s * 0.55f, 0.075f), new Vector3(s * 0.2f, s * 1.1f, 0.075f), 0.05f * size, 0.11f * size, P.Iron);
            rm.Pop();
        }

        private static void PickAxe(RcMesh rm, Vector3 bottom, Vector3 top, Vector3 across)
        {
            rm.Beam(bottom, top, 0.05f, 0.05f, P.WoodLight);
            across = across.normalized * 0.32f;
            rm.Beam(top - across, top + across, 0.06f, 0.07f, P.Iron);
        }

        private static void Shovel(RcMesh rm, Vector3 bottom, Vector3 top)
        {
            rm.Beam(bottom + (top - bottom).normalized * 0.28f, top, 0.05f, 0.05f, P.WoodLight);
            rm.Beam(bottom, bottom + (top - bottom).normalized * 0.34f, 0.2f, 0.03f, P.IronLight);
        }

        private static void Wheelbarrow(RcMesh rm, Vector3 pos, float yaw)
        {
            rm.Push(pos, yaw);
            rm.Frustum(new Vector3(0f, 0.34f, 0.05f), new Vector2(0.5f, 0.7f), new Vector2(0.78f, 1.0f), 0.34f, P.Wood, P.WoodDark);
            rm.Pipe(new Vector3(-0.05f, 0.24f, 0.62f), new Vector3(0.05f, 0.24f, 0.62f), 0.24f, 8, P.Iron, P.WoodDark);
            rm.Beam(new Vector3(-0.28f, 0.45f, -0.45f), new Vector3(-0.16f, 0.36f, 0.62f), 0.05f, 0.05f, P.WoodLight);
            rm.Beam(new Vector3(0.28f, 0.45f, -0.45f), new Vector3(0.16f, 0.36f, 0.62f), 0.05f, 0.05f, P.WoodLight);
            rm.Beam(new Vector3(-0.26f, 0f, -0.35f), new Vector3(-0.26f, 0.42f, -0.35f), 0.05f, 0.05f, P.WoodDark);
            rm.Beam(new Vector3(0.26f, 0f, -0.35f), new Vector3(0.26f, 0.42f, -0.35f), 0.05f, 0.05f, P.WoodDark);
            rm.Pop();
        }

        private static void Plank(RcMesh rm, Vector3 pos, float length, float yaw, float pitch, float roll, P color = P.WoodGrey)
        {
            rm.Push(pos, yaw, pitch, roll);
            rm.Box(Vector3.zero, new Vector3(length, 0.05f, 0.2f), color, P.WoodBleach);
            rm.Pop();
        }

        /// <summary>A cart wheel standing on its edge (axis along local x after the yaw).</summary>
        private static void Wheel(RcMesh rm, Vector3 centre, Vector3 axis, float radius, float width, P rim, P face)
        {
            rm.Pipe(centre - axis * width * 0.5f, centre + axis * width * 0.5f, radius, 10, rim, face);
            rm.Pipe(centre - axis * width * 0.7f, centre + axis * width * 0.7f, radius * 0.24f, 6, P.Iron);
        }

        // ================================================================ mine wagon

        private static void Wagon(RcMesh rm, Transform cols, string name, Vector3 pos, float yaw, float roll, bool loaded, P body = P.Rust)
        {
            rm.Push(pos, yaw, 0f, roll);
            rm.Box(new Vector3(0f, 0.46f, 0f), new Vector3(1.4f, 0.16f, 2.9f), P.Iron);
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Wheel(rm, new Vector3(sx * 0.7f, 0.3f, sz * 0.95f), Vector3.right, 0.3f, 0.1f, P.Iron, P.IronLight);
                }
            }

            rm.Frustum(new Vector3(0f, 0.54f, 0f), new Vector2(1.5f, 2.5f), new Vector2(1.95f, 3.05f), 1.0f, body, P.RockShade);
            foreach (float sz in new[] { -1f, 1f })
            {
                rm.Beam(new Vector3(-0.99f, 1.55f, sz * 1.54f), new Vector3(0.99f, 1.55f, sz * 1.54f), 0.12f, 0.1f, P.RustDark);
            }

            foreach (float sx in new[] { -1f, 1f })
            {
                rm.Beam(new Vector3(sx * 0.99f, 1.55f, -1.54f), new Vector3(sx * 0.99f, 1.55f, 1.54f), 0.1f, 0.12f, P.RustDark);
                foreach (float z in new[] { -0.6f, 0.6f })
                {
                    rm.Beam(new Vector3(sx * 0.78f, 0.55f, z), new Vector3(sx * 0.98f, 1.54f, z), 0.07f, 0.08f, P.RustDark);
                }
            }

            foreach (float sz in new[] { -1f, 1f })
            {
                rm.Pipe(new Vector3(0f, 0.6f, sz * 1.5f), new Vector3(0f, 0.6f, sz * 1.82f), 0.11f, 6, P.Iron);
                rm.Beam(new Vector3(-0.5f, 0.6f, sz * 1.82f), new Vector3(0.5f, 0.6f, sz * 1.82f), 0.1f, 0.12f, P.Iron);
            }

            if (loaded)
            {
                for (int i = 0; i < 5; i++)
                {
                    rm.Rock(new Vector3(R(-0.55f, 0.55f), 1.5f + R(0f, 0.12f), R(-1.0f, 1.0f)), new Vector3(R(0.3f, 0.5f), R(0.2f, 0.3f), R(0.3f, 0.5f)), Seed(), P.RockDark, P.Coal, i == 2 ? P.Gold : P.RockDark, null, 0, 0.2f, 0.2f);
                }
            }

            rm.Pop();
            if (cols != null)
            {
                Quaternion rot = Quaternion.Euler(0f, yaw, roll);
                Box(cols, name, pos + rot * new Vector3(0f, 0.95f, 0f), new Vector3(2.0f, 1.8f, 3.3f), yaw, 0f, roll);
            }
        }

        // ================================================================ plants

        private static void Saguaro(RcMesh rm, Transform cols, Vector3 pos, float h, float yaw)
        {
            float r = 0.075f * h;
            rm.Push(pos, yaw);
            rm.Poly(new Vector3(0f, -0.05f, 0f), r * 1.15f, r, h * 0.86f, 7, P.Cactus, P.Cactus, 9f);
            rm.Poly(new Vector3(0f, h * 0.81f, 0f), r, 0f, h * 0.19f, 7, P.CactusLight, null, 9f);
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * 360f + 9f;
                rm.Beam(new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * r * 0.9f, h * 0.06f, Mathf.Sin(a * Mathf.Deg2Rad) * r * 0.9f), new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * r * 0.98f, h * 0.82f, Mathf.Sin(a * Mathf.Deg2Rad) * r * 0.98f), 0.03f, 0.03f, P.CactusDark);
            }

            float ar = r * 0.58f;
            Vector3 a0 = new(r * 0.7f, h * 0.40f, 0f), a1 = new(r * 0.7f + h * 0.20f, h * 0.43f, 0f), a2 = new(a1.x, h * 0.70f, 0f);
            rm.Pipe(a0, a1, ar, 6, P.Cactus);
            rm.Pipe(a1, a2, ar, 6, P.Cactus);
            rm.Poly(a2, ar, 0f, ar * 2f, 6, P.CactusLight);
            Vector3 b0 = new(-r * 0.7f, h * 0.55f, 0f), b1 = new(-r * 0.7f - h * 0.16f, h * 0.58f, 0f), b2 = new(b1.x, h * 0.80f, 0f);
            rm.Pipe(b0, b1, ar * 0.9f, 6, P.Cactus);
            rm.Pipe(b1, b2, ar * 0.9f, 6, P.Cactus);
            rm.Poly(b2, ar * 0.9f, 0f, ar * 1.8f, 6, P.CactusLight);
            rm.Pop();
            if (cols != null)
            {
                Box(cols, "Saguaro", pos + new Vector3(0f, h * 0.5f, 0f), new Vector3(r * 2.2f, h, r * 2.2f), yaw);
            }
        }

        private static void BarrelCactus(RcMesh rm, Vector3 pos, float s)
        {
            rm.Rock(pos + new Vector3(0f, 0.27f * s, 0f), new Vector3(0.34f * s, 0.32f * s, 0.34f * s), Seed(), P.Cactus, P.CactusDark, P.CactusLight, null, 0, 0.08f, 0.25f);
            rm.Poly(pos + new Vector3(0f, 0.56f * s, 0f), 0.09f * s, 0f, 0.14f * s, 5, P.Flower);
        }

        private static void PricklyPear(RcMesh rm, Vector3 pos, float s, float yaw)
        {
            rm.Push(pos, yaw);
            for (int i = 0; i < 3; i++)
            {
                rm.Push(new Vector3((i - 1) * 0.32f * s, 0f, 0f), 0f, 0f, (i - 1) * 24f);
                rm.Rock(new Vector3(0f, 0.30f * s + i % 2 * 0.1f * s, 0f), new Vector3(0.26f * s, 0.32f * s, 0.07f * s), Seed(), P.Cactus, P.CactusDark, P.CactusLight, null, 0, 0.06f, 1f);
                rm.Pop();
            }

            rm.Poly(new Vector3(0.3f * s, 0.62f * s, 0f), 0.05f * s, 0f, 0.1f * s, 4, P.Flower);
            rm.Poly(new Vector3(-0.28f * s, 0.55f * s, 0f), 0.05f * s, 0f, 0.1f * s, 4, P.Flower);
            rm.Pop();
        }

        private static void Bush(RcMesh rm, Vector3 pos, float s)
        {
            int n = rng.Next(2, 4);
            for (int k = 0; k < n; k++)
            {
                float sc = R(0.7f, 1.1f) * s;
                rm.Rock(pos + new Vector3(R(-0.35f, 0.35f) * s, 0.16f * sc, R(-0.35f, 0.35f) * s), new Vector3(0.5f * sc, 0.34f * sc, 0.46f * sc), Seed(), P.Shrub, P.ShrubDark, P.Shrub, null, 0, 0.30f, 0.1f);
            }

            for (int k = 0; k < 3; k++)
            {
                float a = R(0f, 6.28f);
                rm.Beam(pos + new Vector3(0f, 0.1f * s, 0f), pos + new Vector3(Mathf.Cos(a) * 0.45f * s, 0.5f * s, Mathf.Sin(a) * 0.45f * s), 0.03f, 0.03f, P.GrassDark);
            }
        }

        /// <summary>A tuft of dry grass: six thin leaning blades (3 faces each).</summary>
        private static void Tuft(RcMesh rm, Vector3 pos, float s)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * 360f + R(-20f, 20f);
                rm.Push(pos + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.05f * s, 0f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.05f * s), -a + 90f, R(12f, 30f), 0f);
                rm.Poly(Vector3.zero, 0.035f * s, 0f, R(0.35f, 0.6f) * s, 3, i % 2 == 0 ? P.Grass : P.GrassDark);
                rm.Pop();
            }
        }

        private static void Tumbleweed(RcMesh rm, Vector3 pos, float s)
        {
            rm.Rock(pos + new Vector3(0f, 0.34f * s, 0f), new Vector3(0.36f * s, 0.34f * s, 0.36f * s), Seed(), P.GrassDark, P.Grass, P.Grass, null, 0, 0.35f, 0.2f);
        }

        private static void DeadTree(RcMesh rm, Transform cols, Vector3 pos, float h, float yaw)
        {
            rm.Push(pos, yaw, 0f, R(-5f, 5f));
            rm.Poly(new Vector3(0f, -0.1f, 0f), h * 0.075f, h * 0.045f, h * 0.7f, 5, P.WoodGrey, P.WoodBleach, 20f);
            Vector3 top = new(0f, h * 0.6f, 0f);
            rm.Beam(top, top + new Vector3(0.55f * h * 0.4f, 0.55f * h * 0.4f, 0.1f), 0.1f, 0.1f, P.WoodGrey);
            rm.Beam(top + new Vector3(0.55f * h * 0.4f, 0.55f * h * 0.4f, 0.1f), top + new Vector3(0.95f * h * 0.4f, 0.85f * h * 0.4f, 0.2f), 0.06f, 0.06f, P.WoodBleach);
            Vector3 mid = new(0f, h * 0.42f, 0f);
            rm.Beam(mid, mid + new Vector3(-0.7f * h * 0.35f, 0.45f * h * 0.35f, -0.1f), 0.09f, 0.09f, P.WoodGrey);
            rm.Beam(mid + new Vector3(-0.7f * h * 0.35f, 0.45f * h * 0.35f, -0.1f), mid + new Vector3(-1.0f * h * 0.35f, 0.9f * h * 0.35f, 0.1f), 0.055f, 0.055f, P.WoodBleach);
            rm.Beam(new Vector3(0f, h * 0.66f, 0f), new Vector3(-0.15f, h * 0.9f, 0.25f), 0.07f, 0.07f, P.WoodBleach);
            rm.Pop();
            if (cols != null)
            {
                Box(cols, "DeadTree", pos + new Vector3(0f, h * 0.3f, 0f), new Vector3(h * 0.16f, h * 0.6f, h * 0.16f), yaw);
            }
        }

        // ================================================================ fences

        /// <summary>A weathered post-and-rail fence along a ground-following polyline, with a simple box collider per section.</summary>
        private static void Fence(RcMesh rm, Transform cols, string name, IList<Vector2> points, float height = 1.15f, float spacing = 1.7f, bool collide = true, float lift = 0f, float colliderHeight = 1.7f)
        {
            var samples = new List<Vector3>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                float length = Vector2.Distance(points[i], points[i + 1]);
                int steps = Mathf.Max(1, Mathf.RoundToInt(length / spacing));
                for (int k = 0; k < steps; k++)
                {
                    Vector2 p = Vector2.Lerp(points[i], points[i + 1], k / (float)steps);
                    samples.Add(At(p.x, p.y, lift));
                }
            }

            samples.Add(At(points[points.Count - 1].x, points[points.Count - 1].y, lift));
            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 s = samples[i];
                float lean = R(-3f, 3f);
                rm.Push(s + new Vector3(0f, -0.12f, 0f), R(0f, 360f), lean, lean * 0.5f);
                rm.Block(Vector3.zero, new Vector3(0.15f, height + 0.12f + R(-0.06f, 0.06f), 0.15f), P.WoodGrey, P.WoodBleach);
                rm.Pop();
                if (i == samples.Count - 1)
                {
                    continue;
                }

                Vector3 n = samples[i + 1];
                rm.Beam(s + new Vector3(0f, height * 0.9f, 0f), n + new Vector3(0f, height * 0.9f - R(0f, 0.06f), 0f), 0.07f, 0.13f, P.WoodDark);
                rm.Beam(s + new Vector3(0f, height * 0.5f, 0f), n + new Vector3(0f, height * 0.5f, 0f), 0.07f, 0.12f, P.WoodGrey);
                if (collide)
                {
                    Vector3 mid = (s + n) * 0.5f + new Vector3(0f, colliderHeight * 0.5f - 0.1f, 0f);
                    Vector3 d = n - s;
                    Box(cols, name + "_" + i, mid, new Vector3(0.26f, colliderHeight, new Vector2(d.x, d.z).magnitude + 0.1f), Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                }
            }
        }

        /// <summary>A red-and-white danger board on a post (faces +z in its own frame).</summary>
        private static void DangerSign(RcMesh rm, Vector3 pos, float yaw)
        {
            rm.Push(pos, yaw);
            rm.Block(Vector3.zero, new Vector3(0.13f, 1.7f, 0.13f), P.WoodDark, P.WoodGrey);
            rm.Box(new Vector3(0f, 1.55f, 0.1f), new Vector3(0.95f, 0.5f, 0.06f), P.White);
            rm.Box(new Vector3(0f, 1.55f, 0.115f), new Vector3(0.85f, 0.1f, 0.05f), P.ClothRed);
            rm.Beam(new Vector3(-0.38f, 1.34f, 0.14f), new Vector3(0.38f, 1.76f, 0.14f), 0.025f, 0.09f, P.ClothRed);
            rm.Beam(new Vector3(0.38f, 1.34f, 0.15f), new Vector3(-0.38f, 1.76f, 0.15f), 0.025f, 0.09f, P.ClothRed);
            rm.Pop();
        }

        // ================================================================ rock features

        private static readonly P[] Layers = { P.RockRed2, P.RockOrange, P.RockRed, P.RockOrange2, P.RockSand };

        /// <summary>A layered outcrop of 2-4 tiers (the Rocky Canyon's signature silhouette).</summary>
        private static void Outcrop(RcMesh rm, Vector3 pos, float radius, float height, int tiers, bool cap = false, int variant = -1)
        {
            int v = variant >= 0 ? variant : rng.Next(0, 3);
            P[] colors = v == 0 ? new[] { P.RockRed, P.RockRed2, P.RockRed, P.RockOrange } : v == 1 ? new[] { P.RockRed2, P.RockOrange, P.RockRed2, P.RockOrange2 } : new[] { P.RockDeep, P.RockRed, P.RockRed2, P.RockOrange };
            rm.Butte(pos + new Vector3(0f, -0.6f, 0f), radius, height + 0.6f, tiers, Seed(), colors, v == 2 ? P.RockOrange : P.RockOrange2, 7, 0.7f, cap);
        }

        /// <summary>A tall slender rock tower (spire) of tiered, leaning sections: silhouette against the sky.</summary>
        private static void Spire(RcMesh rm, Vector3 pos, float radius, float height, int seed)
        {
            rm.Butte(pos + new Vector3(0f, -1.0f, 0f), radius, height + 1.0f, 4, seed, new[] { P.RockRed, P.RockRed2, P.RockRed, P.RockOrange }, P.RockOrange2, 6, 0.6f, true);
        }
    }
}
#endif
