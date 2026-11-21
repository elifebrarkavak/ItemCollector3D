// Part of the DISABLED Green Mine generator: see the header of GreenMineSceneBuilder.cs.
#if GREEN_MINE_GENERATOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.GreenMine
{
    /// <summary>The old gold mine: timber entrance in a rock face, decorative tunnel, rails, gold cart, crates, barrels, lanterns, signs.</summary>
    internal static partial class GmProps
    {
        private static void BuildMine()
        {
            MineEntrance("Mine_Entrance");
            MineCart("MineCart_Gold");
            CrateStack("Crate_Stack", false);
            CrateStack("Crate_Stack_Crystal", true);
            Barrel("Barrel", false);
            Barrel("Barrel_Lantern", true);
            LanternPost("Lantern_Post");
            Signpost("Signpost_Mine");
            BannerPole("Banner_Pole");
            Pickaxe("Pickaxe_Leaning");
        }

        // ================================================================ entrance

        /// <summary>Local frame: +Z is outwards (towards the player), the tunnel runs towards -Z. Origin: ground, centre of the opening.</summary>
        private static void MineEntrance(string name)
        {
            var m = new GmMesh();

            // ---- rock face around the opening (reference art 1: boulder pile with a timber portal)
            m.Blob("rock", new Vector3(-5.2f, 2.6f, -1.5f), new Vector3(3.1f, 3.4f, 2.7f), 21, 0.46f, 0, 0.5f);
            m.Blob("rockDark", new Vector3(-7.6f, 1.6f, 0.4f), new Vector3(2.3f, 2.0f, 2.3f), 22, 0.46f, 0, 0.5f);
            m.Blob("rockWarm", new Vector3(-4.2f, 5.6f, -2.3f), new Vector3(2.9f, 2.1f, 2.5f), 23, 0.46f, 0);
            m.Blob("rock", new Vector3(5.2f, 2.7f, -1.5f), new Vector3(3.2f, 3.5f, 2.7f), 24, 0.46f, 0, 0.5f);
            m.Blob("rockDark", new Vector3(7.8f, 1.8f, 0.2f), new Vector3(2.4f, 2.1f, 2.3f), 25, 0.46f, 0, 0.5f);
            m.Blob("rock", new Vector3(4.4f, 5.9f, -2.4f), new Vector3(2.8f, 2.2f, 2.5f), 26, 0.46f, 0);
            m.Blob("rockDark", new Vector3(0f, 7.4f, -2.8f), new Vector3(5.2f, 2.9f, 3.2f), 27, 0.46f, 0);
            m.Blob("rock", new Vector3(-2.7f, 6.7f, -1.9f), new Vector3(2.7f, 1.8f, 2.3f), 28, 0.46f, 0);
            m.Blob("rockWarm", new Vector3(2.9f, 6.9f, -1.7f), new Vector3(2.5f, 1.7f, 2.3f), 29, 0.46f, 0);
            m.Blob("rockDark", new Vector3(0f, 4.6f, -12.4f), new Vector3(9.0f, 6.4f, 5.2f), 30, 0.42f, 0);
            m.Blob("rock", new Vector3(-9.6f, 3.0f, -2.4f), new Vector3(2.6f, 3.2f, 2.6f), 31, 0.46f, 0, 0.5f);
            m.Blob("rock", new Vector3(10.0f, 3.2f, -2.6f), new Vector3(2.8f, 3.4f, 2.7f), 32, 0.46f, 0, 0.5f);
            // moss caps and vines
            m.Blob("moss", new Vector3(0f, 9.9f, -2.8f), new Vector3(3.6f, 0.7f, 2.4f), 33, 0.3f, 0, 0.2f);
            m.Blob("moss", new Vector3(-4.2f, 7.5f, -2.3f), new Vector3(2.0f, 0.5f, 1.7f), 34, 0.3f, 0, 0.2f);
            m.Blob("moss", new Vector3(4.4f, 7.9f, -2.4f), new Vector3(2.0f, 0.5f, 1.7f), 35, 0.3f, 0, 0.2f);
            m.Blob("vine", new Vector3(-1.4f, 5.4f, 0.45f), new Vector3(0.85f, 0.4f, 0.55f), 36, 0.3f, 1);
            m.Blob("vine", new Vector3(1.5f, 5.45f, 0.4f), new Vector3(0.9f, 0.45f, 0.6f), 37, 0.3f, 1);
            m.Blob("leafB", new Vector3(-0.2f, 5.55f, 0.3f), new Vector3(0.6f, 0.3f, 0.45f), 38, 0.3f, 0);
            m.Blob("vine", new Vector3(-2.55f, 3.5f, 0.62f), new Vector3(0.22f, 1.15f, 0.3f), 39, 0.3f, 1);
            m.Blob("vine", new Vector3(2.6f, 4.0f, 0.58f), new Vector3(0.24f, 0.95f, 0.3f), 40, 0.3f, 1);
            m.Blob("leafA", new Vector3(-2.7f, 5.1f, 0.5f), new Vector3(0.5f, 0.38f, 0.4f), 41, 0.3f, 0);

            // ---- timber portal
            for (int s = -1; s <= 1; s += 2)
            {
                m.Box("stone", new Vector3(s * 2.0f, 0.32f, 0.12f), new Vector3(1.15f, 0.64f, 1.1f));                 // stone footing
                m.Box("wood", new Vector3(s * 2.0f, 2.45f, 0f), new Vector3(0.78f, 4.2f, 0.82f));                       // post
                m.Box("woodDark", new Vector3(s * 2.0f, 2.45f, 0.43f), new Vector3(0.2f, 4.0f, 0.06f));                 // plank line on the post front
                m.Box("iron", new Vector3(s * 2.0f, 1.05f, 0.44f), new Vector3(0.84f, 0.18f, 0.07f));                   // iron straps
                m.Box("iron", new Vector3(s * 2.0f, 3.1f, 0.44f), new Vector3(0.84f, 0.18f, 0.07f));
                m.Prism("ironLight", new Vector3(s * 1.78f, 1.05f, 0.49f), 0.05f, 0.05f, 0.03f, 6, new Vector3(90f, 0f, 0f));
                m.Prism("ironLight", new Vector3(s * 2.22f, 1.05f, 0.49f), 0.05f, 0.05f, 0.03f, 6, new Vector3(90f, 0f, 0f));
                m.Prism("ironLight", new Vector3(s * 1.78f, 3.1f, 0.49f), 0.05f, 0.05f, 0.03f, 6, new Vector3(90f, 0f, 0f));
                m.Prism("ironLight", new Vector3(s * 2.22f, 3.1f, 0.49f), 0.05f, 0.05f, 0.03f, 6, new Vector3(90f, 0f, 0f));
                m.Beam("wood", new Vector3(s * 1.6f, 3.05f, 0.05f), new Vector3(s * 0.55f, 4.2f, 0.05f), 0.36f, 0.4f);   // braces
                m.Box("stone", new Vector3(s * 2.3f, 0.12f, 1.05f), new Vector3(1.7f, 0.24f, 0.9f), new Vector3(0f, s * 6f, 0f)); // step slabs
            }

            m.Box("woodLight", new Vector3(0f, 4.62f, 0f), new Vector3(5.4f, 0.78f, 0.92f));                            // header
            m.Box("wood", new Vector3(0f, 5.18f, 0f), new Vector3(4.6f, 0.5f, 0.85f));
            m.Box("woodDark", new Vector3(0f, 4.62f, 0.47f), new Vector3(5.2f, 0.1f, 0.05f));
            m.Box("iron", new Vector3(-1.1f, 4.62f, 0.48f), new Vector3(0.18f, 0.8f, 0.06f));
            m.Box("iron", new Vector3(1.1f, 4.62f, 0.48f), new Vector3(0.18f, 0.8f, 0.06f));

            // ---- sign: dark plaque with crossed pickaxes
            m.Box("woodRed", new Vector3(0f, 5.78f, 0.28f), new Vector3(2.5f, 1.1f, 0.24f));
            m.Box("woodDark", new Vector3(0f, 5.78f, 0.41f), new Vector3(2.62f, 1.22f, 0.04f));
            m.Box("woodRed", new Vector3(0f, 5.78f, 0.45f), new Vector3(2.4f, 1.02f, 0.04f));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sy = (i & 2) == 0 ? -1f : 1f;
                m.Prism("ironLight", new Vector3(sx * 1.08f, 5.78f + sy * 0.42f, 0.47f), 0.055f, 0.055f, 0.03f, 6, new Vector3(90f, 0f, 0f));
            }

            Pickaxes(m, new Vector3(0f, 5.78f, 0.5f), 0.46f);

            // ---- hanging lanterns on iron brackets
            for (int s = -1; s <= 1; s += 2)
            {
                m.Beam("iron", new Vector3(s * 2.0f, 3.9f, 0.5f), new Vector3(s * 2.0f, 3.9f, 1.0f), 0.09f, 0.09f);
                m.Beam("iron", new Vector3(s * 2.0f, 3.5f, 0.5f), new Vector3(s * 2.0f, 3.88f, 0.98f), 0.07f, 0.07f);
                m.Prism("iron", new Vector3(s * 2.0f, 3.55f, 1.0f), 0.012f, 0.012f, 0.35f, 3, caps: false);
                Lantern(m, new Vector3(s * 2.0f, 3.55f, 1.0f), 1.15f);
            }

            // ---- decorative tunnel (the player cannot enter): dark walls fading to black, three timber frames, warm bulbs on a wire
            m.Box("tunnelWall", new Vector3(-2.15f, 2.0f, -2.2f), new Vector3(0.3f, 4.0f, 3.6f));
            m.Box("tunnelWall", new Vector3(2.15f, 2.0f, -2.2f), new Vector3(0.3f, 4.0f, 3.6f));
            m.Box("tunnel", new Vector3(-2.15f, 2.0f, -5.4f), new Vector3(0.3f, 4.0f, 2.9f));
            m.Box("tunnel", new Vector3(2.15f, 2.0f, -5.4f), new Vector3(0.3f, 4.0f, 2.9f));
            m.Box("tunnel", new Vector3(0f, 3.88f, -3.5f), new Vector3(4.6f, 0.3f, 6.4f));
            m.Box("tunnel", new Vector3(0f, 0.04f, -3.5f), new Vector3(4.0f, 0.1f, 6.4f));
            m.Box("tunnelDeep", new Vector3(0f, 2.0f, -6.9f), new Vector3(4.6f, 4.0f, 0.3f));
            for (int i = 0; i < 3; i++)
            {
                float z = -1.4f - i * 1.7f;
                string mat = i == 2 ? "tunnel" : "woodDark";
                m.Box(mat, new Vector3(-1.85f, 1.85f, z), new Vector3(0.42f, 3.7f, 0.46f));
                m.Box(mat, new Vector3(1.85f, 1.85f, z), new Vector3(0.42f, 3.7f, 0.46f));
                m.Box(mat, new Vector3(0f, 3.62f, z), new Vector3(4.2f, 0.44f, 0.5f));
                if (i < 2)
                {
                    m.Blob("bulb", new Vector3(0f, 3.25f, z - 0.85f), Vector3.one * 0.13f, i, 0.1f, 0);
                    Halo(m, new Vector3(0f, 3.25f, z - 0.85f), 1.35f);
                }
            }

            m.Beam("iron", new Vector3(0f, 3.34f, -0.4f), new Vector3(0f, 3.3f, -6.2f), 0.025f, 0.025f);
            m.Blob("bulb", new Vector3(0f, 3.25f, -4.8f), Vector3.one * 0.12f, 4, 0.1f, 0);
            Halo(m, new Vector3(0f, 3.25f, -4.8f), 1.1f);

            Reg("Mine", name, m, true, (go, mesh) =>
            {
                void Box(Vector3 c, Vector3 s)
                {
                    var col = go.AddComponent<BoxCollider>();
                    col.center = c;
                    col.size = s;
                }

                Box(new Vector3(-2.0f, 2.2f, 0f), new Vector3(0.82f, 4.4f, 0.86f));      // posts
                Box(new Vector3(2.0f, 2.2f, 0f), new Vector3(0.82f, 4.4f, 0.86f));
                Box(new Vector3(-5.6f, 2.4f, -1.8f), new Vector3(5.2f, 4.8f, 3.6f));      // rock face
                Box(new Vector3(5.6f, 2.4f, -1.8f), new Vector3(5.2f, 4.8f, 3.6f));
                Box(new Vector3(0f, 2.0f, -1.0f), new Vector3(3.4f, 4.0f, 0.4f));         // the dark mouth is not enterable (placeholder)
            });
        }

        /// <summary>Two crossed gold pickaxes (the mine's emblem), centred on <paramref name="c"/> and facing +Z.</summary>
        private static void Pickaxes(GmMesh m, Vector3 c, float s)
        {
            for (int d = -1; d <= 1; d += 2)
            {
                Vector3 a = c + new Vector3(-d * s * 1.15f, -s * 0.78f, 0f);
                Vector3 b = c + new Vector3(d * s * 1.15f, s * 0.78f, 0f);
                m.Beam("goldLight", a, b, 0.07f, 0.07f);
                Vector3 dir = (b - a).normalized;
                Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
                Vector3 head = b - dir * 0.08f;
                m.Beam("gold", head - perp * 0.3f, head + perp * 0.3f, 0.1f, 0.1f);
                m.Beam("gold", head - perp * 0.3f, head - perp * 0.38f + dir * 0.1f, 0.07f, 0.07f);
                m.Beam("gold", head + perp * 0.3f, head + perp * 0.38f + dir * 0.1f, 0.07f, 0.07f);
            }
        }

        // ================================================================ cart, crates, barrels

        private static void MineCart(string name)
        {
            var m = new GmMesh();
            m.Box("woodDark", new Vector3(0f, 0.52f, 0f), new Vector3(1.7f, 0.12f, 0.96f));
            for (int s = -1; s <= 1; s += 2)
            {
                m.Box("wood", new Vector3(0f, 0.8f, s * 0.47f), new Vector3(1.7f, 0.52f, 0.08f));
                m.Box("woodLight", new Vector3(0f, 0.8f, s * 0.515f), new Vector3(1.64f, 0.1f, 0.03f));
                m.Box("wood", new Vector3(s * 0.82f, 0.8f, 0f), new Vector3(0.08f, 0.52f, 0.96f));
                m.Box("iron", new Vector3(0f, 1.08f, s * 0.48f), new Vector3(1.74f, 0.09f, 0.12f));
                m.Box("iron", new Vector3(s * 0.83f, 1.08f, 0f), new Vector3(0.12f, 0.09f, 1.0f));
                for (int k = -1; k <= 1; k++)
                {
                    m.Box("iron", new Vector3(k * 0.6f, 0.8f, s * 0.515f), new Vector3(0.1f, 0.56f, 0.04f));
                }

                m.Beam("iron", new Vector3(-0.45f, 0.3f, s * 0.55f), new Vector3(0.45f, 0.3f, s * 0.55f), 0.07f, 0.07f);
                for (int a = -1; a <= 1; a += 2)
                {
                    m.Prism("iron", new Vector3(a * 0.45f, 0.3f, s * 0.55f - 0.04f), 0.27f, 0.27f, 0.08f, 10, new Vector3(90f, 0f, 0f));
                    m.Prism("ironLight", new Vector3(a * 0.45f, 0.3f, s * 0.55f + s * 0.04f - 0.04f), 0.1f, 0.1f, 0.09f, 6, new Vector3(90f, 0f, 0f));
                }
            }

            m.Box("iron", new Vector3(0f, 0.38f, 0f), new Vector3(1.0f, 0.08f, 0.2f));
            m.Beam("iron", new Vector3(0.85f, 0.45f, 0f), new Vector3(1.25f, 0.4f, 0f), 0.09f, 0.09f);
            // heap of gold
            m.Blob("gold", new Vector3(0f, 1.05f, 0f), new Vector3(0.72f, 0.26f, 0.42f), 3, 0.3f, 1);
            var nuggets = new[]
            {
                new Vector3(-0.45f, 1.3f, 0.1f), new Vector3(-0.1f, 1.38f, -0.15f), new Vector3(0.3f, 1.34f, 0.12f), new Vector3(0.55f, 1.22f, -0.12f),
                new Vector3(-0.3f, 1.25f, -0.22f), new Vector3(0.05f, 1.28f, 0.22f), new Vector3(0.65f, 1.18f, 0.2f), new Vector3(-0.62f, 1.18f, -0.1f),
            };
            for (int i = 0; i < nuggets.Length; i++)
            {
                m.Blob(i % 2 == 0 ? "goldLight" : "gold", nuggets[i], Vector3.one * (0.2f + (i % 3) * 0.035f), 10 + i, 0.3f, 0);
            }

            Reg("Mine", name, m, true, BoxCol(new Vector3(0.1f, 0.65f, 0f), new Vector3(2.0f, 1.0f, 1.2f)));
        }

        private static void Crate(GmMesh m, Vector3 pos, float yaw, float s = 1f)
        {
            m.Push(pos, new Vector3(0f, yaw, 0f), Vector3.one * s);
            m.Box("woodDark", new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                m.Box("woodLight", new Vector3(sx * 0.42f, 0.45f, sz * 0.42f), new Vector3(0.14f, 0.94f, 0.14f));
            }

            m.Box("woodLight", new Vector3(0f, 0.9f, 0f), new Vector3(0.96f, 0.07f, 0.96f));
            m.Box("woodLight", new Vector3(0f, 0.03f, 0f), new Vector3(0.96f, 0.07f, 0.96f));
            for (int side = -1; side <= 1; side += 2)
            {
                m.Beam("woodLight", new Vector3(-0.4f, 0.1f, side * 0.46f), new Vector3(0.4f, 0.8f, side * 0.46f), 0.09f, 0.05f);
                m.Beam("woodLight", new Vector3(-0.4f, 0.8f, side * 0.46f), new Vector3(0.4f, 0.1f, side * 0.46f), 0.09f, 0.05f);
            }

            m.Pop();
        }

        private static void CrateStack(string name, bool crystals)
        {
            var m = new GmMesh();
            Crate(m, new Vector3(0f, 0f, 0f), 4f);
            Crate(m, new Vector3(1.0f, 0f, 0.2f), -9f);
            Crate(m, new Vector3(0.45f, 0.9f, 0.08f), 20f, 0.95f);
            if (crystals)
            {
                Crystal(m, new Vector3(0.4f, 1.78f, 0.1f), 0.9f, 0.18f, 6f, 30f);
                Crystal(m, new Vector3(0.12f, 1.78f, -0.1f), 0.55f, 0.13f, 24f, 200f);
                Crystal(m, new Vector3(0.72f, 1.78f, 0.22f), 0.5f, 0.12f, 26f, 80f);
            }

            Reg("Mine", name, m, true, BoxCol(new Vector3(0.5f, 0.9f, 0.1f), new Vector3(2.0f, 1.8f, 1.1f)));
        }

        private static void Barrel(string name, bool lantern)
        {
            var m = new GmMesh();
            m.Prism("woodDark", Vector3.zero, 0.38f, 0.46f, 0.45f, 9, caps: false);
            m.Prism("wood", new Vector3(0f, 0.45f, 0f), 0.46f, 0.38f, 0.45f, 9, caps: false);
            m.Prism("woodLight", new Vector3(0f, 0.9f, 0f), 0.37f, 0.35f, 0.03f, 9);
            m.Prism("iron", new Vector3(0f, 0.1f, 0f), 0.425f, 0.425f, 0.07f, 9, caps: false);
            m.Prism("iron", new Vector3(0f, 0.42f, 0f), 0.475f, 0.475f, 0.07f, 9, caps: false);
            m.Prism("iron", new Vector3(0f, 0.74f, 0f), 0.425f, 0.425f, 0.07f, 9, caps: false);
            m.Prism("iron", Vector3.zero, 0.38f, 0.38f, 0.02f, 9);
            if (lantern)
            {
                m.Push(new Vector3(0.04f, 0.93f, 0.02f));
                m.Box("iron", new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.05f, 0.3f));
                m.Box("lanternGlass", new Vector3(0f, 0.2f, 0f), new Vector3(0.2f, 0.26f, 0.2f));
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? -1f : 1f;
                    m.Box("iron", new Vector3(sx * 0.11f, 0.2f, sz * 0.11f), new Vector3(0.035f, 0.32f, 0.035f));
                }

                m.Prism("iron", new Vector3(0f, 0.36f, 0f), 0.19f, 0.03f, 0.1f, 4, twist: Mathf.PI / 4f);
                m.Prism("iron", new Vector3(0f, 0.45f, 0f), 0.06f, 0.06f, 0.05f, 6);
                Halo(m, new Vector3(0f, 0.22f, 0f), 1.0f);
                m.Pop();
            }

            Reg("Mine", name, m, true, Capsule(new Vector3(0f, 0.47f, 0f), 0.46f, 0.94f));
        }

        // ================================================================ posts, signs, banner

        private static void LanternPost(string name)
        {
            var m = new GmMesh();
            m.Box("wood", new Vector3(0f, 1.2f, 0f), new Vector3(0.16f, 2.4f, 0.16f));
            m.Box("woodDark", new Vector3(0f, 2.42f, 0f), new Vector3(0.24f, 0.08f, 0.24f));
            m.Beam("wood", new Vector3(0f, 2.28f, 0f), new Vector3(0.55f, 2.28f, 0f), 0.09f, 0.11f);
            m.Beam("wood", new Vector3(0f, 1.85f, 0f), new Vector3(0.45f, 2.24f, 0f), 0.08f, 0.09f);
            m.Prism("iron", new Vector3(0.55f, 1.95f, 0f), 0.012f, 0.012f, 0.33f, 3, caps: false);
            Lantern(m, new Vector3(0.55f, 1.97f, 0f), 1.1f);
            m.Blob("rock", new Vector3(0.18f, 0.12f, 0.1f), new Vector3(0.28f, 0.2f, 0.26f), 1, 0.3f, 0, 0.4f);
            m.Blob("rockDark", new Vector3(-0.16f, 0.1f, -0.12f), new Vector3(0.24f, 0.17f, 0.22f), 2, 0.3f, 0, 0.4f);
            Reg("Mine", name, m, true, Capsule(new Vector3(0f, 1.2f, 0f), 0.14f, 2.4f));
        }

        private static void Signpost(string name)
        {
            var m = new GmMesh();
            m.Box("woodDark", new Vector3(0f, 1.45f, 0f), new Vector3(0.2f, 2.9f, 0.2f));
            m.Box("woodLight", new Vector3(0f, 2.92f, 0f), new Vector3(0.28f, 0.08f, 0.28f));
            var arrow = new[] { new Vector2(-0.6f, 0.19f), new Vector2(0.46f, 0.19f), new Vector2(0.78f, 0f), new Vector2(0.46f, -0.19f), new Vector2(-0.6f, -0.19f) };
            var arrowBack = new[] { new Vector2(0.6f, 0.19f), new Vector2(-0.46f, 0.19f), new Vector2(-0.78f, 0f), new Vector2(-0.46f, -0.19f), new Vector2(0.6f, -0.19f) };
            m.Extrude("wood", arrow, 0.08f, new Vector3(0.52f, 2.42f, 0.14f), new Vector3(0f, 6f, 0f));
            m.Extrude("woodLight", arrow, 0.04f, new Vector3(0.52f, 2.42f, 0.14f), new Vector3(0f, 6f, 0f), new Vector3(1.06f, 1.18f, 1f));
            m.Extrude("wood", arrowBack, 0.08f, new Vector3(-0.5f, 1.9f, 0.14f), new Vector3(0f, -8f, 0f));
            m.Extrude("woodLight", arrowBack, 0.04f, new Vector3(-0.5f, 1.9f, 0.14f), new Vector3(0f, -8f, 0f), new Vector3(1.06f, 1.18f, 1f));
            // icons: pickaxes on the upper board, a crystal on the lower one
            m.Push(new Vector3(0.34f, 2.42f, 0.2f), new Vector3(0f, 6f, 0f));
            Pickaxes(m, Vector3.zero, 0.075f);
            m.Pop();
            var hex = new[] { new Vector2(0f, 0.15f), new Vector2(0.1f, 0.06f), new Vector2(0.1f, -0.08f), new Vector2(0f, -0.15f), new Vector2(-0.1f, -0.08f), new Vector2(-0.1f, 0.06f) };
            m.Extrude("goldLight", hex, 0.05f, new Vector3(-0.38f, 1.9f, 0.2f), new Vector3(0f, -8f, 0f));
            m.Prism("rope", new Vector3(0f, 2.26f, 0f), 0.13f, 0.13f, 0.05f, 6, caps: false);
            Reg("Mine", name, m, true, Capsule(new Vector3(0f, 1.45f, 0f), 0.16f, 2.9f));
        }

        private static void BannerPole(string name)
        {
            var m = new GmMesh();
            m.Box("wood", new Vector3(0f, 1.9f, 0f), new Vector3(0.2f, 3.8f, 0.2f));
            m.Box("woodDark", new Vector3(0f, 3.84f, 0f), new Vector3(0.3f, 0.09f, 0.3f));
            m.Beam("woodLight", new Vector3(-0.08f, 3.45f, 0f), new Vector3(1.15f, 3.45f, 0f), 0.1f, 0.1f);
            m.Prism("rope", new Vector3(0.12f, 3.38f, 0f), 0.14f, 0.14f, 0.1f, 6, caps: false);
            var pennant = new[] { new Vector2(-0.46f, 0f), new Vector2(0.46f, 0f), new Vector2(0.46f, -1.05f), new Vector2(0f, -1.45f), new Vector2(-0.46f, -1.05f) };
            m.Extrude("bannerEdge", pennant, 0.05f, new Vector3(0.62f, 3.4f, 0.02f), Vector3.zero, new Vector3(1.0f, 1.0f, 1f));
            m.Extrude("banner", pennant, 0.05f, new Vector3(0.62f, 3.36f, 0.04f), Vector3.zero, new Vector3(0.86f, 0.9f, 1f));
            var crystal = new[] { new Vector2(0f, 0.26f), new Vector2(0.15f, 0.12f), new Vector2(0.15f, -0.12f), new Vector2(0f, -0.26f), new Vector2(-0.15f, -0.12f), new Vector2(-0.15f, 0.12f) };
            m.Extrude("goldLight", crystal, 0.04f, new Vector3(0.62f, 2.72f, 0.09f));
            m.Extrude("gold", new[] { new Vector2(0f, 0.2f), new Vector2(0.09f, 0.1f), new Vector2(0.09f, -0.1f), new Vector2(0f, -0.2f) }, 0.04f, new Vector3(0.66f, 2.72f, 0.12f));
            m.Blob("rock", new Vector3(0.12f, 0.1f, 0.12f), new Vector3(0.3f, 0.2f, 0.28f), 5, 0.3f, 0, 0.4f);
            m.Blob("rockDark", new Vector3(-0.2f, 0.09f, -0.1f), new Vector3(0.24f, 0.17f, 0.22f), 6, 0.3f, 0, 0.4f);
            Reg("Mine", name, m, true, Capsule(new Vector3(0f, 1.9f, 0f), 0.14f, 3.8f));
        }

        private static void Pickaxe(string name)
        {
            var m = new GmMesh();
            m.Beam("woodLight", new Vector3(0f, 0.05f, 0f), new Vector3(0.1f, 1.45f, 0.5f), 0.08f, 0.08f);
            Vector3 top = new(0.1f, 1.45f, 0.5f);
            m.Beam("ironLight", top + new Vector3(-0.5f, -0.18f, 0f), top + new Vector3(0f, 0.04f, 0f), 0.09f, 0.09f);
            m.Beam("ironLight", top + new Vector3(0f, 0.04f, 0f), top + new Vector3(0.5f, -0.18f, 0f), 0.09f, 0.09f);
            m.Beam("ironLight", top + new Vector3(-0.5f, -0.18f, 0f), top + new Vector3(-0.62f, -0.34f, 0f), 0.06f, 0.06f);
            m.Beam("ironLight", top + new Vector3(0.5f, -0.18f, 0f), top + new Vector3(0.62f, -0.34f, 0f), 0.06f, 0.06f);
            Reg("Mine", name, m, true);
        }

        // ================================================================ rails

        /// <summary>Narrow-gauge track along a polyline (world positions). Sleepers every 0.7 m, two rails.</summary>
        public static GameObject RailTrack(string name, IList<Vector3> pts)
        {
            var m = new GmMesh();
            float carry = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len < 1e-3f)
                {
                    continue;
                }

                dir /= len;
                Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, flat);
                float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                for (float d = carry; d < len; d += 0.7f)
                {
                    Vector3 p = a + dir * d;
                    m.Box("woodDark", p + Vector3.up * 0.05f, new Vector3(1.5f, 0.09f, 0.2f), new Vector3(0f, yaw, 0f));
                    carry = d + 0.7f - len;
                }

                m.Beam("ironLight", a + side * 0.52f + Vector3.up * 0.13f, b + side * 0.52f + Vector3.up * 0.13f, 0.09f, 0.1f);
                m.Beam("ironLight", a - side * 0.52f + Vector3.up * 0.13f, b - side * 0.52f + Vector3.up * 0.13f, 0.09f, 0.1f);
            }

            return Reg("Mine", name, m, false);
        }
    }
}
#endif
