// Part of the DISABLED Green Mine generator: see the header of GreenMineSceneBuilder.cs.
#if GREEN_MINE_GENERATOR
using UnityEngine;

namespace ItemCollector3D.EditorTools.GreenMine
{
    /// <summary>Start camp, fences, gate, bridge and the distant backdrop.</summary>
    internal static partial class GmProps
    {
        private static void BuildCamp()
        {
            Tent("Tent_Camp");
            Campfire("Campfire");
            NoticeBoard("Notice_Board");
            Stall("Camp_Stall");
            Bedroll("Bedroll_Pack");
            WoodPile("WoodPile");
            FenceSection("Fence_Section");
            FencePost("Fence_End");
            GateArch("Gate_Arch");
        }

        private static void Tent(string name)
        {
            var m = new GmMesh();
            const float hw = 1.7f, hl = 1.8f, h = 2.6f;
            Vector3 o = new(0f, 1f, 0f);
            m.TriOut("canvas", new Vector3(-hw, 0f, -hl), new Vector3(-hw, 0f, hl), new Vector3(0f, h, hl), o);
            m.TriOut("canvas", new Vector3(-hw, 0f, -hl), new Vector3(0f, h, hl), new Vector3(0f, h, -hl), o);
            m.TriOut("canvas", new Vector3(hw, 0f, hl), new Vector3(hw, 0f, -hl), new Vector3(0f, h, -hl), o);
            m.TriOut("canvas", new Vector3(hw, 0f, hl), new Vector3(0f, h, -hl), new Vector3(0f, h, hl), o);
            m.TriOut("canvasShade", new Vector3(-hw, 0f, -hl), new Vector3(0f, h, -hl), new Vector3(hw, 0f, -hl), o);
            // front with a door
            m.TriOut("canvas", new Vector3(-hw, 0f, hl), new Vector3(0f, h, hl), new Vector3(0f, 1.9f, hl), o);
            m.TriOut("canvas", new Vector3(-hw, 0f, hl), new Vector3(0f, 1.9f, hl), new Vector3(-0.7f, 0f, hl), o);
            m.TriOut("canvas", new Vector3(hw, 0f, hl), new Vector3(0f, 1.9f, hl), new Vector3(0f, h, hl), o);
            m.TriOut("canvas", new Vector3(hw, 0f, hl), new Vector3(0.7f, 0f, hl), new Vector3(0f, 1.9f, hl), o);
            m.Tri("tunnel", new Vector3(-0.7f, 0f, hl - 0.4f), new Vector3(0f, 1.9f, hl - 0.4f), new Vector3(0.7f, 0f, hl - 0.4f));
            m.Tri("tunnel", new Vector3(0.7f, 0f, hl - 0.4f), new Vector3(0f, 1.9f, hl - 0.4f), new Vector3(-0.7f, 0f, hl - 0.4f));
            // door flaps tied back
            m.TriOut("canvasShade", new Vector3(-0.7f, 0f, hl), new Vector3(0f, 1.9f, hl), new Vector3(-0.95f, 0.05f, hl + 0.25f), o);
            m.TriOut("canvasShade", new Vector3(0.7f, 0f, hl), new Vector3(0.95f, 0.05f, hl + 0.25f), new Vector3(0f, 1.9f, hl), o);
            // ridge, pole and flag
            m.Beam("woodDark", new Vector3(0f, h, -hl - 0.1f), new Vector3(0f, h, hl + 0.1f), 0.1f, 0.1f);
            m.Beam("woodDark", new Vector3(0f, h, -hl), new Vector3(0f, h + 1.0f, -hl), 0.06f, 0.06f);
            m.Extrude("flagRed", new[] { new Vector2(0f, 0.2f), new Vector2(0.7f, 0.2f), new Vector2(0.55f, 0f), new Vector2(0.7f, -0.2f), new Vector2(0f, -0.2f) }, 0.03f,
                new Vector3(0f, h + 0.78f, -hl), new Vector3(0f, 90f, 0f));
            // guy ropes and stakes
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var stake = new Vector3(sx * (hw + 0.9f), 0.04f, sz * (hl + 0.7f));
                    m.Beam("rope", new Vector3(sx * hw * 0.55f, 1.1f, sz * hl), stake, 0.03f, 0.03f);
                    m.Prism("woodDark", stake + new Vector3(0f, -0.05f, 0f), 0.04f, 0.03f, 0.22f, 4);
                }
            }

            Lantern(m, new Vector3(0f, 1.95f, hl + 0.12f), 0.8f);
            Reg("Camp", name, m, true, BoxCol(new Vector3(0f, 1.0f, 0f), new Vector3(3.0f, 2.0f, 3.3f)));
        }

        private static void Campfire(string name)
        {
            var m = new GmMesh();
            m.Prism("tunnelDeep", new Vector3(0f, 0.01f, 0f), 0.55f, 0.55f, 0.03f, 10);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                m.Blob(i % 2 == 0 ? "rock" : "rockDark", new Vector3(Mathf.Cos(a) * 0.7f, 0.1f, Mathf.Sin(a) * 0.7f), new Vector3(0.2f, 0.15f, 0.18f), i + 1, 0.3f, 0, 0.4f);
            }

            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f + 0.4f;
                m.Beam("trunkDark", new Vector3(Mathf.Cos(a) * 0.5f, 0.08f, Mathf.Sin(a) * 0.5f), new Vector3(Mathf.Cos(a) * 0.08f, 0.6f, Mathf.Sin(a) * 0.08f), 0.15f, 0.15f);
            }

            m.Prism("fire", new Vector3(0f, 0.12f, 0f), 0.32f, 0f, 0.9f, 6);
            m.Prism("fire", new Vector3(0.2f, 0.12f, 0.1f), 0.2f, 0f, 0.6f, 5, new Vector3(0f, 0f, -18f));
            m.Prism("fire", new Vector3(-0.2f, 0.12f, -0.08f), 0.2f, 0f, 0.55f, 5, new Vector3(0f, 0f, 20f));
            m.Prism("fireCore", new Vector3(0f, 0.14f, 0f), 0.17f, 0f, 0.55f, 5);
            Halo(m, new Vector3(0f, 0.55f, 0f), 2.0f);
            Reg("Camp", name, m, false, Capsule(new Vector3(0f, 0.3f, 0f), 0.7f, 0.6f));
        }

        private static void NoticeBoard(string name)
        {
            var m = new GmMesh();
            m.Box("woodDark", new Vector3(-0.95f, 1.1f, 0f), new Vector3(0.16f, 2.2f, 0.16f));
            m.Box("woodDark", new Vector3(0.95f, 1.1f, 0f), new Vector3(0.16f, 2.2f, 0.16f));
            m.Box("woodDark", new Vector3(0f, 1.62f, 0f), new Vector3(2.3f, 1.4f, 0.1f));
            m.Box("wood", new Vector3(0f, 1.62f, 0.05f), new Vector3(2.1f, 1.2f, 0.06f));
            m.Box("paper", new Vector3(0f, 1.62f, 0.1f), new Vector3(1.75f, 0.95f, 0.03f));
            m.Beam("mapInk", new Vector3(-0.7f, 1.35f, 0.125f), new Vector3(-0.1f, 1.6f, 0.125f), 0.05f, 0.012f);
            m.Beam("mapInk", new Vector3(-0.1f, 1.6f, 0.125f), new Vector3(0.45f, 1.5f, 0.125f), 0.05f, 0.012f);
            m.Beam("mapInk", new Vector3(0.45f, 1.5f, 0.125f), new Vector3(0.62f, 1.9f, 0.125f), 0.05f, 0.012f);
            m.Box("leafB", new Vector3(-0.55f, 1.95f, 0.125f), new Vector3(0.45f, 0.3f, 0.012f));
            m.Box("bedroll", new Vector3(0.5f, 1.3f, 0.125f), new Vector3(0.4f, 0.22f, 0.012f));
            m.Beam("flagRed", new Vector3(0.52f, 1.82f, 0.13f), new Vector3(0.72f, 1.98f, 0.13f), 0.05f, 0.015f);
            m.Beam("flagRed", new Vector3(0.52f, 1.98f, 0.13f), new Vector3(0.72f, 1.82f, 0.13f), 0.05f, 0.015f);
            m.Box("woodLight", new Vector3(0f, 2.42f, 0.08f), new Vector3(2.5f, 0.1f, 0.5f), new Vector3(-12f, 0f, 0f));
            Reg("Camp", name, m, true, BoxCol(new Vector3(0f, 1.1f, 0f), new Vector3(2.3f, 2.2f, 0.3f)));
        }

        private static void Stall(string name)
        {
            var m = new GmMesh();
            for (int sx = -1; sx <= 1; sx += 2)
            {
                m.Box("woodDark", new Vector3(sx * 1.1f, 1.0f, 0.7f), new Vector3(0.12f, 2.0f, 0.12f));
                m.Box("woodDark", new Vector3(sx * 1.1f, 1.25f, -0.7f), new Vector3(0.12f, 2.5f, 0.12f));
            }

            m.Box("wood", new Vector3(0f, 0.78f, 0.15f), new Vector3(2.0f, 0.1f, 0.9f));
            m.Box("woodDark", new Vector3(0f, 0.38f, 0.15f), new Vector3(1.9f, 0.7f, 0.8f));
            // striped awning
            for (int i = 0; i < 6; i++)
            {
                float x = -1.25f + i * 0.5f;
                m.Box(i % 2 == 0 ? "canvas" : "flagRed", new Vector3(x + 0.25f, 2.02f, 0f), new Vector3(0.5f, 0.06f, 1.9f), new Vector3(-12f, 0f, 0f));
            }

            Crate(m, new Vector3(1.9f, 0f, 0.2f), 14f, 0.8f);
            m.Blob("gold", new Vector3(-0.5f, 0.9f, 0.2f), new Vector3(0.2f, 0.1f, 0.2f), 3, 0.3f, 0);
            m.Blob("goldLight", new Vector3(-0.15f, 0.9f, 0.1f), new Vector3(0.16f, 0.09f, 0.16f), 4, 0.3f, 0);
            Reg("Camp", name, m, true, BoxCol(new Vector3(0f, 1.0f, 0.1f), new Vector3(2.3f, 2.0f, 1.0f)));
        }

        private static void Bedroll(string name)
        {
            var m = new GmMesh();
            m.Prism("bedroll", new Vector3(-0.5f, 0.2f, 0f), 0.2f, 0.2f, 1.0f, 8, new Vector3(0f, 0f, -90f));
            m.Prism("woodDark", new Vector3(-0.25f, 0.2f, 0f), 0.21f, 0.21f, 0.07f, 8, new Vector3(0f, 0f, -90f), caps: false);
            m.Prism("woodDark", new Vector3(0.25f, 0.2f, 0f), 0.21f, 0.21f, 0.07f, 8, new Vector3(0f, 0f, -90f), caps: false);
            m.Prism("canvasShade", new Vector3(-0.51f, 0.2f, 0f), 0.15f, 0.15f, 0.02f, 8, new Vector3(0f, 0f, -90f));
            m.Box("woodRed", new Vector3(0.1f, 0.3f, 0.65f), new Vector3(0.55f, 0.6f, 0.35f));
            m.Box("woodDark", new Vector3(0.1f, 0.62f, 0.65f), new Vector3(0.5f, 0.08f, 0.32f));
            m.Box("iron", new Vector3(0.1f, 0.3f, 0.84f), new Vector3(0.14f, 0.1f, 0.03f));
            Reg("Camp", name, m, true);
        }

        private static void WoodPile(string name)
        {
            var m = new GmMesh();
            void Row(float y, int n, float x0)
            {
                for (int i = 0; i < n; i++)
                {
                    m.Prism(i % 2 == 0 ? "trunk" : "trunkDark", new Vector3(x0 + i * 0.36f, y, -0.55f), 0.17f, 0.17f, 1.1f, 7, new Vector3(90f, 0f, 0f), caps: false);
                    m.Prism("wedge", new Vector3(x0 + i * 0.36f, y, 0.55f), 0.15f, 0.15f, 0.02f, 7, new Vector3(90f, 0f, 0f));
                }
            }

            Row(0.17f, 4, -0.54f);
            Row(0.47f, 3, -0.36f);
            Row(0.77f, 2, -0.18f);
            m.Box("woodDark", new Vector3(-0.8f, 0.5f, -0.6f), new Vector3(0.08f, 1.0f, 0.08f));
            m.Box("woodDark", new Vector3(0.8f, 0.5f, -0.6f), new Vector3(0.08f, 1.0f, 0.08f));
            m.Box("woodDark", new Vector3(-0.8f, 0.5f, 0.6f), new Vector3(0.08f, 1.0f, 0.08f));
            m.Box("woodDark", new Vector3(0.8f, 0.5f, 0.6f), new Vector3(0.08f, 1.0f, 0.08f));
            Reg("Camp", name, m, true, BoxCol(new Vector3(0f, 0.5f, 0f), new Vector3(1.8f, 1.0f, 1.3f)));
        }

        // ================================================================ fences and gate

        private static void FencePostGeometry(GmMesh m, float x)
        {
            m.Box("wood", new Vector3(x, 0.55f, 0f), new Vector3(0.17f, 1.1f, 0.17f));
            m.Prism("woodLight", new Vector3(x, 1.1f, 0f), 0.13f, 0f, 0.14f, 4, twist: Mathf.PI / 4f);
        }

        private static void FenceSection(string name)
        {
            var m = new GmMesh();
            const float len = 2.4f;
            FencePostGeometry(m, 0f);
            m.Beam("woodLight", new Vector3(0f, 0.86f, 0f), new Vector3(len, 0.86f, 0f), 0.11f, 0.11f);
            m.Beam("wood", new Vector3(0f, 0.46f, 0f), new Vector3(len, 0.46f, 0f), 0.11f, 0.11f);
            m.Prism("rope", new Vector3(0f, 0.8f, 0f), 0.11f, 0.11f, 0.12f, 6, caps: false);
            m.Prism("rope", new Vector3(0f, 0.4f, 0f), 0.11f, 0.11f, 0.12f, 6, caps: false);
            Reg("Camp", name, m, true, BoxCol(new Vector3(len * 0.5f, 0.55f, 0f), new Vector3(len, 1.1f, 0.22f)));
        }

        private static void FencePost(string name)
        {
            var m = new GmMesh();
            FencePostGeometry(m, 0f);
            m.Prism("rope", new Vector3(0f, 0.8f, 0f), 0.11f, 0.11f, 0.12f, 6, caps: false);
            Reg("Camp", name, m, true, Capsule(new Vector3(0f, 0.55f, 0f), 0.1f, 1.1f));
        }

        private static void GateArch(string name)
        {
            var m = new GmMesh();
            for (int s = -1; s <= 1; s += 2)
            {
                m.Box("stone", new Vector3(s * 2.2f, 0.3f, 0f), new Vector3(0.95f, 0.6f, 0.95f));
                m.Box("wood", new Vector3(s * 2.2f, 2.0f, 0f), new Vector3(0.52f, 3.4f, 0.55f));
                m.Box("iron", new Vector3(s * 2.2f, 1.0f, 0.29f), new Vector3(0.56f, 0.16f, 0.05f));
                m.Box("iron", new Vector3(s * 2.2f, 2.6f, 0.29f), new Vector3(0.56f, 0.16f, 0.05f));
                m.Beam("wood", new Vector3(s * 1.95f, 2.7f, 0f), new Vector3(s * 1.1f, 3.6f, 0f), 0.3f, 0.34f);
                Lantern(m, new Vector3(s * 1.45f, 3.1f, 0.35f), 1.0f);
                m.Beam("iron", new Vector3(s * 1.45f, 3.15f, 0.0f), new Vector3(s * 1.45f, 3.15f, 0.36f), 0.07f, 0.07f);
            }

            m.Box("woodLight", new Vector3(0f, 3.9f, 0f), new Vector3(5.2f, 0.5f, 0.6f));
            m.Box("wood", new Vector3(0f, 4.3f, 0f), new Vector3(4.3f, 0.36f, 0.5f));
            m.Box("woodRed", new Vector3(0f, 4.85f, 0.1f), new Vector3(1.7f, 0.75f, 0.14f));
            Pickaxes(m, new Vector3(0f, 4.85f, 0.2f), 0.3f);
            m.Blob("vine", new Vector3(-1.2f, 4.25f, 0.2f), new Vector3(0.7f, 0.3f, 0.4f), 2, 0.3f, 1);
            m.Blob("vine", new Vector3(1.4f, 4.3f, 0.2f), new Vector3(0.6f, 0.28f, 0.4f), 3, 0.3f, 1);
            Reg("Camp", name, m, true, (go, _) =>
            {
                foreach (float x in new[] { -2.2f, 2.2f })
                {
                    var c = go.AddComponent<BoxCollider>();
                    c.center = new Vector3(x, 1.8f, 0f);
                    c.size = new Vector3(0.9f, 3.6f, 0.9f);
                }
            });
        }

        // ================================================================ bridge, foam, mountains

        private static void BuildWaterAndFar()
        {
            Bridge("Bridge_Wood");
            FoamPool("Foam_Pool");
            Mountain("Mountain_A", 34f, 34f, 1, true);
            Mountain("Mountain_B", 30f, 26f, 2, false);
            Mountain("Mountain_C", 38f, 40f, 3, true);
            Mountain("Mountain_D", 26f, 20f, 4, false);
        }

        /// <summary>Wooden footbridge along local X (4.4 m, deck 2.4 m wide, 0.26 m above the banks) with railings and ramps.</summary>
        private static void Bridge(string name)
        {
            var m = new GmMesh();
            const float deckY = 0.26f, half = 2.2f;
            for (int i = 0; i < 12; i++)
            {
                float x = -half + 0.2f + i * (half * 2f - 0.4f) / 11f;
                m.Box(i % 2 == 0 ? "woodLight" : "wood", new Vector3(x, deckY - 0.05f, 0f), new Vector3(0.36f, 0.09f, 2.4f));
            }

            m.Beam("woodDark", new Vector3(-half, deckY - 0.2f, 0.9f), new Vector3(half, deckY - 0.2f, 0.9f), 0.2f, 0.22f);
            m.Beam("woodDark", new Vector3(-half, deckY - 0.2f, -0.9f), new Vector3(half, deckY - 0.2f, -0.9f), 0.2f, 0.22f);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * 1.12f;
                foreach (float x in new[] { -half + 0.1f, -0.7f, 0.7f, half - 0.1f })
                {
                    m.Box("woodDark", new Vector3(x, deckY + 0.5f, z), new Vector3(0.17f, 1.0f, 0.17f));
                    m.Prism("woodLight", new Vector3(x, deckY + 1.0f, z), 0.13f, 0f, 0.13f, 4, twist: Mathf.PI / 4f);
                }

                m.Beam("woodLight", new Vector3(-half + 0.1f, deckY + 0.85f, z), new Vector3(half - 0.1f, deckY + 0.85f, z), 0.1f, 0.1f);
                m.Beam("wood", new Vector3(-half + 0.1f, deckY + 0.45f, z), new Vector3(half - 0.1f, deckY + 0.45f, z), 0.09f, 0.09f);
                m.Prism("rope", new Vector3(-half + 0.1f, deckY + 0.78f, z), 0.115f, 0.115f, 0.12f, 6, caps: false);
                m.Prism("rope", new Vector3(half - 0.1f, deckY + 0.78f, z), 0.115f, 0.115f, 0.12f, 6, caps: false);
            }

            // ramps from the banks up to the deck
            for (int s = -1; s <= 1; s += 2)
            {
                m.Beam("wood", new Vector3(s * (half + 0.75f), 0.02f, 0f), new Vector3(s * (half - 0.05f), deckY - 0.04f, 0f), 2.4f, 0.08f);
            }

            Reg("Water", name, m, true, (go, _) =>
            {
                var deck = go.AddComponent<BoxCollider>();
                deck.center = new Vector3(0f, deckY - 0.1f, 0f);
                deck.size = new Vector3(half * 2f, 0.16f, 2.4f);
                foreach (int s in new[] { -1, 1 })
                {
                    var ramp = new GameObject("Ramp" + (s < 0 ? "_W" : "_E"));
                    ramp.transform.SetParent(go.transform, false);
                    ramp.transform.localPosition = new Vector3(s * (half + 0.4f), 0.11f, 0f);
                    ramp.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 20f);
                    var c = ramp.AddComponent<BoxCollider>();
                    c.size = new Vector3(0.9f, 0.08f, 2.4f);
                    ramp.isStatic = true;
                    var rail = go.AddComponent<BoxCollider>();
                    rail.center = new Vector3(0f, deckY + 0.5f, s * 1.12f);
                    rail.size = new Vector3(half * 2f, 1.0f, 0.12f);
                }
            });
        }

        private static void FoamPool(string name)
        {
            var m = new GmMesh();
            m.Blob("foam", new Vector3(0f, 0.05f, 0f), new Vector3(1.3f, 0.3f, 0.9f), 1, 0.3f, 1, 0.4f);
            m.Blob("foam", new Vector3(-1.5f, 0.02f, 0.3f), new Vector3(0.8f, 0.2f, 0.6f), 2, 0.3f, 0, 0.4f);
            m.Blob("foam", new Vector3(1.4f, 0.02f, 0.2f), new Vector3(0.9f, 0.22f, 0.6f), 3, 0.3f, 0, 0.4f);
            m.Blob("foam", new Vector3(0.3f, 0.02f, 1.0f), new Vector3(0.7f, 0.18f, 0.5f), 4, 0.3f, 0, 0.4f);
            m.Blob("foam", new Vector3(-0.6f, 0.4f, -0.4f), new Vector3(0.8f, 0.5f, 0.5f), 5, 0.35f, 0, 0.4f);
            Reg("Water", name, m, false);
        }

        private static void Mountain(string name, float radius, float height, int seed, bool snow)
        {
            var m = new GmMesh();
            const int sides = 9;
            float[] rr = { 1f, 0.66f, 0.40f, 0.18f };
            float[] hh = { 0f, 0.34f, 0.64f, 0.86f };
            string[] mats = { "mountainGreen", "mountain", "mountainLight", snow ? "snow" : "mountainLight" };
            var rings = new Vector3[rr.Length, sides];
            var rng = new System.Random(seed * 101);
            for (int k = 0; k < rr.Length; k++)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a = (i + (k % 2) * 0.5f) / sides * Mathf.PI * 2f;
                    float jr = 0.82f + (float)rng.NextDouble() * 0.36f;
                    float jh = 1f + ((float)rng.NextDouble() - 0.5f) * 0.12f;
                    rings[k, i] = new Vector3(Mathf.Cos(a) * radius * rr[k] * jr, height * hh[k] * jh, Mathf.Sin(a) * radius * rr[k] * jr);
                }
            }

            var apex = new Vector3(radius * 0.04f, height, 0f);
            var inside = new Vector3(0f, height * 0.4f, 0f);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                for (int k = 0; k < rr.Length - 1; k++)
                {
                    m.QuadOut(mats[k], rings[k, i], rings[k, j], rings[k + 1, j], rings[k + 1, i], inside);
                }

                m.TriOut(mats[rr.Length - 1], rings[rr.Length - 1, i], rings[rr.Length - 1, j], apex, inside);
            }

            Reg("Backdrop", name, m, false);
        }

        /// <summary>A huge flat quad used under the far distance so no gap shows beyond the rim hills.</summary>
        public static GameObject BackdropGround(float size, float y)
        {
            var m = new GmMesh();
            float h = size * 0.5f;
            m.Quad("mountainGreen", new Vector3(-h, y, -h), new Vector3(-h, y, h), new Vector3(h, y, h), new Vector3(h, y, -h));
            return Reg("Backdrop", "Backdrop_Ground", m, false);
        }
    }
}
#endif
