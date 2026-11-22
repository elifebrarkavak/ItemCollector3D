#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>A  Desert Outpost: a small abandoned miners' settlement round an open yard. The start (spawn -37,-22, camera behind it to the west) looks east / north-east along the riverbed.</summary>
    public static partial class RcDressing
    {
        /// <summary>A sloped slab whose TOP surface is the quad a-b-c-d (a-b along the eave); it is <paramref name="thickness"/> m thick below that.</summary>
        private static void Slab(RcMesh rm, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float thickness, P color, P? top = null)
        {
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            if (n.y < 0f)
            {
                n = -n;
            }

            rm.Hexa(new[] { a - n * thickness, b - n * thickness, c - n * thickness, d - n * thickness }, new[] { a, b, c, d }, color, top);
        }

        private static void Outpost(Transform props, Transform cols)
        {
            var rm = new RcMesh();
            Take(-37f, -22f, 3.2f);   // the spawn: nothing stands here
            Take(-42f, -22f, 4.6f);   // and the camera behind it
            Take(-44f, -19f, 4.0f);   // the camera swings round the player: keep the west side free of tall plants
            Take(-27f, -22f, 4.5f);   // the way out east
            Take(-32f, -11.5f, 4.0f); // and north (the railway)

            // the shack sits on the south edge of the yard facing north (its long side along the wall), the shed east of it, the tower in the north-west corner
            const float shackX = -35.8f, shackZ = -30.9f;
            float shackYaw = YawToward(shackX, shackZ, -34f, -22f);
            Vector3 Local(float lx, float lz)
            {
                Vector3 q = At(shackX, shackZ) + Quaternion.Euler(0f, shackYaw, 0f) * new Vector3(lx, 0f, lz);
                return At(q.x, q.z);
            }

            MinerShack(rm, cols, shackX, shackZ);
            SupplyShed(rm, cols, -27.6f, -29.6f);
            WaterTower(rm, cols, -41.2f, -15.4f);
            MiningSign(rm, cols, -25.6f, -16.2f);
            Campfire(rm, -31.2f, -24.6f);

            // directions: an arrow post at the trail out to the riverbed, a lantern at the shack and one at the exit
            SignPost(rm, At(-26.4f, -25.2f), 90f, false);
            Take(-26.4f, -25.2f, 0.9f);
            Vector3 lamp = Local(-3.5f, 4.2f);
            LanternPost(rm, lamp, shackYaw + 90f);
            Take(lamp.x, lamp.z, 0.8f);
            LanternPost(rm, At(-24.6f, -19.4f), 300f);
            Take(-24.6f, -19.4f, 0.8f);

            // ---- goods: crates and sacks at the shack, barrels by the shed, tools leaning on the walls
            Vector3 c0 = Local(-4.5f, 0.2f), c1 = Local(-4.6f, -1.2f), c2 = Local(-4.1f, 1.5f);
            Crate(rm, c0, 0.95f, shackYaw + 8f);
            Crate(rm, c1, 0.85f, shackYaw - 14f);
            Crate(rm, c2, 0.8f, shackYaw + 33f);
            Crate(rm, c0 + new Vector3(0f, 0.95f, 0f), 0.75f, shackYaw + 41f);
            Box(cols, "Crates", c0 + new Vector3(0f, 0.9f, -0.2f), new Vector3(1.7f, 1.8f, 3.6f), shackYaw);
            Take(c0.x, c0.z, 2.2f);
            Vector3 s0 = Local(-2.7f, 3.6f), s1 = Local(-3.4f, 3.0f), s2 = Local(-3.1f, 4.3f);
            Sack(rm, s0, 1.0f, 20f);
            Sack(rm, s1, 0.9f, 80f);
            Sack(rm, s2, 1.0f, 140f, P.CanvasDark);
            Take(s1.x, s1.z, 1.6f);
            for (int i = 0; i < 4; i++)
            {
                Barrel(rm, At(-31.4f + (i % 2) * 0.95f, -29.0f - (i / 2) * 0.95f), 0.42f, 0.95f, R(0f, 40f), i == 2 ? P.WoodDark : P.Wood);
            }

            Barrel(rm, At(-31.0f, -29.5f, 0.95f), 0.42f, 0.95f, 12f, P.WoodDark);
            Box(cols, "Barrels", At(-30.9f, -29.5f, 0.5f), new Vector3(2.2f, 1.0f, 2.2f), 0f);
            Take(-30.9f, -29.5f, 1.8f);
            Vector3 wb = Local(3.6f, 3.8f);
            Wheelbarrow(rm, wb, shackYaw + 200f);
            Take(wb.x, wb.z, 1.2f);

            Vector3 wall = Local(-3.35f, -0.9f);
            PickAxe(rm, wall, wall + new Vector3(-0.25f, 1.7f, 0.2f), new Vector3(0f, 0f, 1f));
            Shovel(rm, wall + new Vector3(0f, 0f, 0.55f), wall + new Vector3(-0.3f, 1.8f, 0.6f));
            Vector3 shed = At(-25.35f, -29.6f);
            PickAxe(rm, shed, shed + new Vector3(0.3f, 1.6f, 0.1f), new Vector3(0.1f, 0f, 1f));

            // the old ore cart parked at the north exit where the track starts
            Wagon(rm, cols, "OreCart", At(-32.0f, -13.9f, 0.14f), 6f, 0f, true);
            Take(-32f, -13.9f, 2.2f);

            // ---- nature: rocks on the canyon-side edge, a few cacti, dry shrubs and grass; the yard, the exits and the spawn stay open
            var nature = new RcMesh();
            var cols2 = cols;
            RcZone[] zone = { RcZone.Outpost };
            RockClusters(nature, cols2, "Outpost", zone, 7, -3.4f, -0.9f, 6.5f, 0.9f, 1.8f, 1.3f);
            Plants(nature, cols2, zone, -4.0f, -0.8f, 2, 2, 1, 7, 26, 1);
            Pebbles(nature, zone, 40, -9f, -0.5f);
            Emit(props, "Nature", nature);
            Emit(props, "Structures", rm);
        }

        // ------------------------------------------------------------------ the main miners' shack

        private static void MinerShack(RcMesh rm, Transform cols, float x, float z)
        {
            const float W = 6.2f, D = 4.4f, H = 2.8f;
            float yaw = YawToward(x, z, -34f, -22f);
            Vector3 pos = At(x, z);
            Take(x, z, 4.6f);
            float wallTop = 0.3f + H;
            rm.Push(pos, yaw);
            rm.Block(new Vector3(0f, -0.4f, 0f), new Vector3(W + 0.3f, 0.7f, D + 0.3f), P.RockDark, P.RockSand);   // stone footing
            rm.Block(new Vector3(0f, 0.3f, 0f), new Vector3(W, H, D), P.WoodLight, P.WoodGrey);
            for (int i = 0; i < 7; i++)
            {
                float bx = -W / 2f + (i + 0.5f) * W / 7f;
                rm.Box(new Vector3(bx, 0.3f + H / 2f, D / 2f + 0.015f), new Vector3(0.07f, H, 0.03f), P.Wood);
                rm.Box(new Vector3(bx, 0.3f + H / 2f, -D / 2f - 0.015f), new Vector3(0.07f, H, 0.03f), P.Wood);
            }

            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    rm.Block(new Vector3(sx * W / 2f, 0.3f, sz * D / 2f), new Vector3(0.2f, H + 0.05f, 0.2f), P.WoodDark);
                }
            }

            // front: door and two windows with shutters
            float fz = D / 2f + 0.03f;
            rm.Box(new Vector3(-1.2f, 0.3f + 1.0f, fz), new Vector3(1.0f, 2.0f, 0.06f), P.WoodDark);
            rm.Box(new Vector3(-1.2f, 0.3f + 2.08f, fz + 0.01f), new Vector3(1.3f, 0.14f, 0.07f), P.WoodLight);
            rm.Box(new Vector3(-1.62f, 0.3f + 0.85f, fz + 0.045f), new Vector3(0.08f, 0.09f, 0.04f), P.Iron);
            foreach (float wx in new[] { -2.55f, 1.15f })
            {
                rm.Box(new Vector3(wx, 0.3f + 1.45f, fz), new Vector3(0.95f, 0.8f, 0.06f), P.RockShade);
                rm.Box(new Vector3(wx, 0.3f + 1.45f, fz - 0.02f), new Vector3(1.15f, 1.0f, 0.05f), P.WoodLight);
                rm.Box(new Vector3(wx - 0.66f, 0.3f + 1.45f, fz + 0.03f), new Vector3(0.4f, 0.86f, 0.05f), P.ClothTeal);
                rm.Box(new Vector3(wx + 0.66f, 0.3f + 1.45f, fz + 0.03f), new Vector3(0.4f, 0.86f, 0.05f), P.ClothTeal);
                rm.Box(new Vector3(wx, 0.3f + 0.93f, fz + 0.1f), new Vector3(1.2f, 0.07f, 0.2f), P.WoodDark);
            }

            // side windows
            foreach (float sx in new[] { -1f, 1f })
            {
                rm.Box(new Vector3(sx * (W / 2f + 0.03f), 0.3f + 1.5f, 0f), new Vector3(0.06f, 0.8f, 0.9f), P.RockShade);
                rm.Box(new Vector3(sx * (W / 2f + 0.015f), 0.3f + 1.5f, 0f), new Vector3(0.05f, 1.0f, 1.1f), P.WoodLight);
            }

            // gable ends and the corrugated iron roof
            float roofRise = 0.5f, half = D / 2f + 0.55f, xe = W / 2f + 0.5f;
            float eave = wallTop + 0.16f - (half - D / 2f) * roofRise;
            float ridge = eave + half * roofRise;
            foreach (float sx in new[] { -1f, 1f })
            {
                rm.Face(P.WoodLight, new Vector3(0f, wallTop, 0f), new Vector3(sx * W / 2f, wallTop, -D / 2f), new Vector3(sx * W / 2f, wallTop, D / 2f), new Vector3(sx * W / 2f, wallTop + D / 2f * roofRise - 0.05f, 0f));
            }

            foreach (float sz in new[] { -1f, 1f })
            {
                Slab(rm, new Vector3(-xe, eave, sz * half), new Vector3(xe, eave, sz * half), new Vector3(xe, ridge, 0f), new Vector3(-xe, ridge, 0f), 0.16f, P.Rust, P.Rust);
                for (int i = 0; i < 8; i++)
                {
                    float rx = -xe + 0.45f + i * (2f * xe - 0.9f) / 7f;
                    rm.Beam(new Vector3(rx, eave + 0.09f, sz * half), new Vector3(rx, ridge + 0.09f, 0f), 0.07f, 0.07f, P.RustDark);
                }
            }

            rm.Beam(new Vector3(-xe, ridge + 0.1f, 0f), new Vector3(xe, ridge + 0.1f, 0f), 0.2f, 0.12f, P.RustDark);
            rm.Block(new Vector3(W / 2f - 0.9f, wallTop - 0.4f, -0.7f), new Vector3(0.75f, 2.7f, 0.75f), P.RockOrange, P.RockShade);
            rm.Block(new Vector3(W / 2f - 0.9f, wallTop + 2.2f, -0.7f), new Vector3(0.95f, 0.14f, 0.95f), P.RockDark);

            // porch: a low deck, two posts and a striped canvas awning
            rm.Block(new Vector3(0f, 0.05f, D / 2f + 0.95f), new Vector3(W - 0.5f, 0.15f, 1.9f), P.WoodGrey, P.WoodBleach);
            foreach (float sx in new[] { -1f, 1f })
            {
                rm.Block(new Vector3(sx * (W / 2f - 0.35f), 0.15f, D / 2f + 1.75f), new Vector3(0.16f, wallTop - 0.75f, 0.16f), P.WoodDark);
            }

            int strips = 8;
            float awningW = W - 0.3f;
            for (int i = 0; i < strips; i++)
            {
                float x0 = -awningW / 2f + i * awningW / strips, x1 = x0 + awningW / strips;
                Vector3 a = new(x0, wallTop - 0.45f, D / 2f + 0.02f), b = new(x1, wallTop - 0.45f, D / 2f + 0.02f);
                Vector3 c = new(x1, wallTop - 1.05f, D / 2f + 1.95f), d = new(x0, wallTop - 1.05f, D / 2f + 1.95f);
                Slab(rm, a, b, c, d, 0.05f, i % 2 == 0 ? P.Canvas : P.ClothRed, i % 2 == 0 ? P.Canvas : P.ClothRed);
            }

            // a bench by the door
            rm.Box(new Vector3(1.9f, 0.45f, D / 2f + 0.55f), new Vector3(1.5f, 0.09f, 0.4f), P.WoodLight);
            rm.Block(new Vector3(1.3f, 0.15f, D / 2f + 0.55f), new Vector3(0.1f, 0.3f, 0.34f), P.WoodDark);
            rm.Block(new Vector3(2.5f, 0.15f, D / 2f + 0.55f), new Vector3(0.1f, 0.3f, 0.34f), P.WoodDark);
            rm.Pop();
            Box(cols, "MinerShack", pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 1.75f, 0f), new Vector3(W, 3.5f, D), yaw);
        }

        // ------------------------------------------------------------------ the supply shed

        private static void SupplyShed(RcMesh rm, Transform cols, float x, float z)
        {
            const float W = 4.4f, D = 3.2f, H = 2.4f;
            float yaw = YawToward(x, z, -33f, -22f);
            Vector3 pos = At(x, z);
            Take(x, z, 3.4f);
            rm.Push(pos, yaw);
            rm.Block(new Vector3(0f, -0.4f, 0f), new Vector3(W + 0.3f, 0.6f, D + 0.3f), P.RockDark, P.RockSand);
            rm.Block(new Vector3(0f, 0.2f, 0f), new Vector3(W, H, D), P.WoodGrey, P.WoodBleach);
            for (int i = 0; i < 5; i++)
            {
                float bx = -W / 2f + (i + 0.5f) * W / 5f;
                rm.Box(new Vector3(bx, 0.2f + H / 2f, D / 2f + 0.015f), new Vector3(0.07f, H, 0.03f), P.WoodDark);
            }

            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    rm.Block(new Vector3(sx * W / 2f, 0.2f, sz * D / 2f), new Vector3(0.18f, H + 0.03f, 0.18f), P.WoodDark);
                }
            }

            // double doors, held shut by a cross bar, with a hoist beam above
            foreach (float dx in new[] { -0.55f, 0.55f })
            {
                rm.Box(new Vector3(dx, 0.2f + 1.0f, D / 2f + 0.04f), new Vector3(1.05f, 2.0f, 0.07f), P.WoodDark);
                rm.Beam(new Vector3(dx - 0.45f, 0.2f + 0.2f, D / 2f + 0.09f), new Vector3(dx + 0.45f, 0.2f + 1.8f, D / 2f + 0.09f), 0.04f, 0.1f, P.Wood);
            }

            rm.Box(new Vector3(0f, 0.2f + 1.0f, D / 2f + 0.14f), new Vector3(2.4f, 0.1f, 0.05f), P.Iron);
            rm.Beam(new Vector3(0f, 0.2f + H - 0.1f, D / 2f), new Vector3(0f, 0.2f + H - 0.1f, D / 2f + 1.1f), 0.14f, 0.14f, P.WoodDark);
            rm.Pipe(new Vector3(-0.07f, 0.2f + H - 0.2f, D / 2f + 1.0f), new Vector3(0.07f, 0.2f + H - 0.2f, D / 2f + 1.0f), 0.13f, 8, P.Iron, P.IronLight);
            // a single sloping roof (lean-to)
            float top = 0.2f + H;
            float xe = W / 2f + 0.4f;
            Slab(rm, new Vector3(-xe, top + 0.75f, -D / 2f - 0.4f), new Vector3(xe, top + 0.75f, -D / 2f - 0.4f), new Vector3(xe, top - 0.08f, D / 2f + 0.6f), new Vector3(-xe, top - 0.08f, D / 2f + 0.6f), 0.16f, P.Rust, P.Rust);
            for (int i = 0; i < 7; i++)
            {
                float rx = -xe + 0.4f + i * (2f * xe - 0.8f) / 6f;
                rm.Beam(new Vector3(rx, top + 0.84f, -D / 2f - 0.4f), new Vector3(rx, top + 0.01f, D / 2f + 0.6f), 0.07f, 0.07f, P.RustDark);
            }

            foreach (float sx in new[] { -1f, 1f })
            {
                rm.Face(P.WoodGrey, new Vector3(0f, top, 0f), new Vector3(sx * W / 2f, top, -D / 2f), new Vector3(sx * W / 2f, top, D / 2f), new Vector3(sx * W / 2f, top + 0.62f, -D / 2f));
            }

            rm.Pop();
            Box(cols, "SupplyShed", pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 1.6f, 0f), new Vector3(W, 3.2f, D), yaw);
        }

        // ------------------------------------------------------------------ the water tower (the camp's tall landmark)

        private static void WaterTower(RcMesh rm, Transform cols, float x, float z)
        {
            Vector3 pos = At(x, z);
            float yaw = 30f;
            Take(x, z, 3.4f);
            const float leg = 3.5f, tankR = 1.3f, tankH = 1.9f;
            rm.Push(pos, yaw);
            for (int ix = -1; ix <= 1; ix += 2)
            {
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    Vector3 lo = new(ix * 1.3f, -0.2f, iz * 1.3f), hi = new(ix * 1.05f, leg, iz * 1.05f);
                    rm.Beam(lo, hi, 0.3f, 0.3f, P.WoodDark);
                    rm.Block(new Vector3(ix * 1.3f, -0.25f, iz * 1.3f), new Vector3(0.55f, 0.3f, 0.55f), P.RockDark, P.RockSand); // stone feet
                }
            }

            // X braces on the four faces at two levels
            for (int level = 0; level < 2; level++)
            {
                float y0 = 0.3f + level * 1.6f, y1 = y0 + 1.5f;
                float k0 = Mathf.Lerp(1.3f, 1.05f, y0 / leg), k1 = Mathf.Lerp(1.3f, 1.05f, y1 / leg);
                rm.Beam(new Vector3(-k0, y0, k0), new Vector3(k1, y1, k1), 0.12f, 0.12f, P.Wood);
                rm.Beam(new Vector3(k0, y0, k0), new Vector3(-k1, y1, k1), 0.12f, 0.12f, P.Wood);
                rm.Beam(new Vector3(-k0, y0, -k0), new Vector3(k1, y1, -k1), 0.12f, 0.12f, P.Wood);
                rm.Beam(new Vector3(k0, y0, -k0), new Vector3(-k1, y1, -k1), 0.12f, 0.12f, P.Wood);
                rm.Beam(new Vector3(k0, y0, -k0), new Vector3(k1, y1, k1), 0.12f, 0.12f, P.Wood);
                rm.Beam(new Vector3(k0, y0, k0), new Vector3(k1, y1, -k1), 0.12f, 0.12f, P.Wood);
                rm.Beam(new Vector3(-k0, y0, -k0), new Vector3(-k1, y1, k1), 0.12f, 0.12f, P.Wood);
                rm.Beam(new Vector3(-k0, y0, k0), new Vector3(-k1, y1, -k1), 0.12f, 0.12f, P.Wood);
            }

            // platform, tank with iron hoops, cone roof
            rm.Poly(new Vector3(0f, leg - 0.1f, 0f), 1.75f, 1.75f, 0.2f, 8, P.WoodDark, P.WoodGrey, 22.5f, true, P.WoodDark);
            rm.Poly(new Vector3(0f, leg + 0.1f, 0f), tankR, tankR * 1.03f, tankH, 10, P.Wood, P.WoodDark, 0f, false);
            foreach (float hy in new[] { 0.25f, 0.95f, 1.65f })
            {
                rm.Poly(new Vector3(0f, leg + 0.1f + hy, 0f), tankR * 1.045f, tankR * 1.045f, 0.09f, 10, P.Iron, null, 0f);
            }

            rm.Poly(new Vector3(0f, leg + 0.1f + tankH, 0f), tankR * 1.2f, 0.06f, 1.0f, 10, P.RustDark, null, 0f);
            // ladder on one face and a spout
            for (int s = -1; s <= 1; s += 2)
            {
                rm.Beam(new Vector3(s * 0.28f, -0.1f, 1.45f), new Vector3(s * 0.28f, leg + 0.9f, 1.32f), 0.07f, 0.07f, P.WoodDark);
            }

            for (int i = 0; i < 10; i++)
            {
                float y = 0.2f + i * 0.4f;
                rm.Beam(new Vector3(-0.28f, y, 1.45f - y * 0.03f), new Vector3(0.28f, y, 1.45f - y * 0.03f), 0.06f, 0.06f, P.Wood);
            }

            rm.Pipe(new Vector3(-1.0f, leg + 0.35f, -0.9f), new Vector3(-1.25f, leg - 0.55f, -1.5f), 0.07f, 5, P.Iron);
            rm.Pop();
            // only the lower 2 m of the legs block the miner: a taller collider beside the camp paths would squeeze the orbiting camera (its ray starts 2.45 m above the feet)
            Box(cols, "WaterTowerBase", pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 1.0f, 0f), new Vector3(3.1f, 2.0f, 3.1f), yaw);
        }

        // ------------------------------------------------------------------ the mining company's sign at the way out

        private static void MiningSign(RcMesh rm, Transform cols, float x, float z)
        {
            float yaw = 255f; // faces west into the camp; the back carries the same board for those who leave
            Vector3 pos = At(x, z);
            Take(x, z, 2.6f);
            rm.Push(pos, yaw);
            foreach (float sx in new[] { -1f, 1f })
            {
                rm.Block(new Vector3(sx * 1.9f, -0.2f, 0f), new Vector3(0.24f, 3.7f, 0.24f), P.WoodDark, P.WoodGrey);
                rm.Beam(new Vector3(sx * 1.9f, 0.5f, 0.1f), new Vector3(sx * 1.45f, 1.1f, 0.1f), 0.1f, 0.1f, P.WoodDark);
                rm.Beam(new Vector3(sx * 1.9f, 0.5f, -0.1f), new Vector3(sx * 1.45f, 1.1f, -0.1f), 0.1f, 0.1f, P.WoodDark);
            }

            rm.Beam(new Vector3(-1.9f, 3.35f, 0f), new Vector3(1.9f, 3.35f, 0f), 0.2f, 0.2f, P.WoodDark);
            foreach (float cx in new[] { -1.4f, 1.4f })
            {
                rm.Beam(new Vector3(cx, 3.25f, 0f), new Vector3(cx, 2.95f, 0f), 0.04f, 0.04f, P.Iron);
            }

            for (int side = 0; side < 2; side++)
            {
                float zf = side == 0 ? 0.08f : -0.08f;
                rm.Push(new Vector3(0f, 0f, zf), side == 0 ? 0f : 180f);
                Board(rm, new Vector3(0f, 2.55f, 0f), 0f, 3.2f, 0.95f, P.WoodLight, P.ClothRed);
                Emblem(rm, new Vector3(0f, 2.55f, 0.02f), 0f, 0.62f);
                rm.Box(new Vector3(-1.15f, 2.55f, 0.06f), new Vector3(0.55f, 0.14f, 0.03f), P.White);
                rm.Box(new Vector3(1.15f, 2.55f, 0.06f), new Vector3(0.55f, 0.14f, 0.03f), P.White);
                rm.Box(new Vector3(0f, 1.95f, 0.04f), new Vector3(1.5f, 0.32f, 0.05f), P.WoodBleach);
                rm.Box(new Vector3(0f, 1.95f, 0.07f), new Vector3(1.3f, 0.07f, 0.03f), P.ClothRed);
                rm.Pop();
            }

            rm.Pop();
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 post = pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(sx * 1.9f, 1.4f, 0f);
                Box(cols, "SignPost" + (sx < 0 ? "L" : "R"), post, new Vector3(0.3f, 2.8f, 0.3f), yaw);
            }
        }

        // ------------------------------------------------------------------ the camp fire

        private static void Campfire(RcMesh rm, float x, float z)
        {
            Vector3 pos = At(x, z);
            Take(x, z, 3.0f);
            rm.Push(pos, 0f);
            rm.Poly(new Vector3(0f, -0.02f, 0f), 0.9f, 0.9f, 0.06f, 9, P.Ash, P.Coal, 10f);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                rm.Rock(new Vector3(Mathf.Cos(a) * 0.88f, 0.1f, Mathf.Sin(a) * 0.88f), new Vector3(R(0.2f, 0.3f), R(0.14f, 0.2f), R(0.2f, 0.28f)), Seed(), i % 2 == 0 ? P.RockDark : P.Pebble, P.RockDeep, P.PebbleLight, null, 0, 0.18f, 0.2f);
            }

            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + 0.3f;
                rm.Pipe(new Vector3(Mathf.Cos(a) * 0.55f, 0.07f, Mathf.Sin(a) * 0.55f), new Vector3(0f, 0.5f, 0f), 0.07f, 5, P.WoodDark, P.Coal);
            }

            rm.Glow = true;
            rm.Poly(new Vector3(0f, 0.2f, 0f), 0.3f, 0f, 0.95f, 5, P.Flame, null, 10f);
            rm.Poly(new Vector3(0.14f, 0.2f, 0.05f), 0.17f, 0f, 0.65f, 4, P.FlameCore, null, 40f);
            rm.Poly(new Vector3(-0.2f, 0.2f, -0.06f), 0.16f, 0f, 0.55f, 4, P.Flame, null, 70f);
            rm.Poly(new Vector3(0f, 0.2f, 0f), 0.15f, 0f, 0.55f, 4, P.FlameCore, null, 0f);
            rm.Glow = false;

            // three log seats round it, a tripod and pot
            for (int i = 0; i < 3; i++)
            {
                float a = (200f + i * 105f) * Mathf.Deg2Rad;
                Vector3 c = new(Mathf.Cos(a) * 2.2f, 0.2f, Mathf.Sin(a) * 2.2f);
                Vector3 t = new(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                rm.Pipe(c - t * 0.75f, c + t * 0.75f, 0.2f, 7, P.Wood, P.WoodLight);
            }

            rm.Pop();
        }
    }
}
#endif
