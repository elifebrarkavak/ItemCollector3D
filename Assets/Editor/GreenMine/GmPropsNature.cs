// Part of the DISABLED Green Mine generator: see the header of GreenMineSceneBuilder.cs.
#if GREEN_MINE_GENERATOR
using UnityEngine;

namespace ItemCollector3D.EditorTools.GreenMine
{
    /// <summary>Trees, bushes, flowers, rocks, crystals: the stylised low-poly vegetation and stone of the valley.</summary>
    internal static partial class GmProps
    {
        private static void BuildNature()
        {
            // ---- pines (reference art: layered cone trees, saturated green)
            Pine("Pine_S", 4.6f, 3, 1, "pineDark", "pineMid", "pineLight", 0.26f);
            Pine("Pine_M", 7.2f, 4, 2, "pineDark", "pineMid", "pineLight", 0.32f);
            Pine("Pine_L", 10.4f, 5, 3, "pineDark", "pineMid", "pineLight", 0.40f);
            Pine("Pine_M_Light", 7.0f, 4, 4, "pineMid", "pineLight", "leafB", 0.32f);

            // ---- round broadleaf trees
            RoundTree("Tree_Round_A", 1, "leafA", "leafB", "leafC", 1f);
            RoundTree("Tree_Round_B", 2, "leafB", "leafC", "leafB", 0.8f);

            // ---- bushes, grass, flowers
            Bush("Bush_A", 1, "leafA", "leafB", false);
            Bush("Bush_B", 2, "leafB", "leafC", false);
            Bush("Bush_Berry", 3, "leafA", "leafB", true);
            GrassTuft("Grass_Tuft_A", 1);
            GrassTuft("Grass_Tuft_B", 2);
            Flower("Flower_White", "flowerWhite", 1);
            Flower("Flower_Yellow", "flowerYellow", 2);
            Flower("Flower_Pink", "flowerPink", 3);
            Lavender("Flower_Lavender", 4);
            Mushroom("Mushroom_Red");

            // ---- rocks (grey-lilac, mossy caps)
            Rock("Rock_S_A", new Vector3(0.45f, 0.32f, 0.42f), 1, "rock", false, 0);
            Rock("Rock_S_B", new Vector3(0.55f, 0.36f, 0.5f), 2, "rockDark", false, 0);
            Rock("Rock_M_A", new Vector3(1.1f, 0.8f, 1.0f), 3, "rock", false, 0);
            Rock("Rock_M_B", new Vector3(1.0f, 0.9f, 1.2f), 4, "rockDark", true, 0);
            Rock("Rock_L_A", new Vector3(2.0f, 1.5f, 1.8f), 5, "rock", true, 0);
            Rock("Rock_L_B", new Vector3(1.8f, 1.7f, 2.1f), 6, "rockWarm", false, 0);
            Rock("Rock_XL_A", new Vector3(3.2f, 2.5f, 2.8f), 7, "rockDark", true, 1);
            Rock("Rock_XL_B", new Vector3(3.0f, 3.2f, 3.0f), 8, "rock", true, 1);
            RockCluster("RockCluster_Meadow", 11, false);
            RockCluster("RockCluster_Crystal", 12, true);

            // ---- crystals (DECORATION only: no collider, never a treasure)
            CrystalCluster("Crystal_Cluster_S", 1, 0.55f);
            CrystalCluster("Crystal_Cluster_M", 2, 1.0f);
            CrystalCluster("Crystal_Cluster_L", 3, 1.7f);

            // ---- logs and stumps
            Log("Log_A");
            Stump("Stump_A");
        }

        private static void Pine(string name, float h, int tiers, int seed, string c0, string c1, string c2, float colliderRadius)
        {
            var m = new GmMesh();
            m.Prism("trunk", Vector3.zero, h * 0.05f, h * 0.034f, h * 0.30f, 6, caps: false);
            float baseR = h * 0.25f;
            for (int i = 0; i < tiers; i++)
            {
                float t = tiers == 1 ? 0f : i / (float)(tiers - 1);
                float y0 = Mathf.Lerp(h * 0.17f, h * 0.60f, t);
                float r = Mathf.Lerp(baseR, baseR * 0.42f, t);
                float ht = Mathf.Lerp(h * 0.34f, h * 0.40f, t);
                string mat = t < 0.34f ? c0 : t < 0.72f ? c1 : c2;
                m.Prism(mat, new Vector3(0f, y0, 0f), r, 0f, ht, 7, twist: i * 0.55f + seed * 0.3f);
            }

            Reg("Nature", name, m, true, Capsule(new Vector3(0f, 1.1f, 0f), colliderRadius, 2.2f));
        }

        private static void RoundTree(string name, int seed, string a, string b, string c, float scale)
        {
            var m = new GmMesh();
            m.Prism("trunk", Vector3.zero, 0.36f * scale, 0.22f * scale, 3.0f * scale, 6, caps: false);
            m.Beam("trunk", new Vector3(0f, 2.3f * scale, 0f), new Vector3(1.0f * scale, 3.5f * scale, 0.3f * scale), 0.18f, 0.18f);
            m.Beam("trunk", new Vector3(0f, 2.5f * scale, 0f), new Vector3(-0.9f * scale, 3.5f * scale, -0.3f * scale), 0.18f, 0.18f);
            m.Blob(a, new Vector3(0f, 4.3f, 0f) * scale, new Vector3(2.4f, 1.9f, 2.4f) * scale, seed, 0.28f, 1);
            m.Blob(b, new Vector3(1.5f, 3.5f, 0.5f) * scale, new Vector3(1.6f, 1.4f, 1.6f) * scale, seed + 3, 0.26f, 1);
            m.Blob(a, new Vector3(-1.5f, 3.6f, -0.5f) * scale, new Vector3(1.6f, 1.35f, 1.6f) * scale, seed + 5, 0.26f, 1);
            m.Blob(c, new Vector3(0.3f, 5.5f, -0.1f) * scale, new Vector3(1.5f, 1.15f, 1.5f) * scale, seed + 7, 0.24f, 1);
            m.Blob(c, new Vector3(-0.7f, 4.8f, 1.1f) * scale, new Vector3(0.9f, 0.8f, 0.9f) * scale, seed + 9, 0.22f, 0);
            Reg("Nature", name, m, true, Capsule(new Vector3(0f, 1.2f, 0f), 0.34f * scale, 2.4f));
        }

        private static void Bush(string name, int seed, string a, string b, bool berries)
        {
            var m = new GmMesh();
            m.Blob(a, new Vector3(0f, 0.5f, 0f), new Vector3(0.95f, 0.65f, 0.95f), seed, 0.26f, 1, 0.45f);
            m.Blob(b, new Vector3(0.65f, 0.38f, 0.3f), new Vector3(0.62f, 0.46f, 0.62f), seed + 2, 0.24f, 0, 0.4f);
            m.Blob(a, new Vector3(-0.6f, 0.36f, -0.25f), new Vector3(0.58f, 0.44f, 0.58f), seed + 4, 0.24f, 0, 0.4f);
            if (berries)
            {
                var spots = new[]
                {
                    new Vector3(0.3f, 0.85f, 0.5f), new Vector3(-0.35f, 0.9f, 0.4f), new Vector3(0.7f, 0.6f, -0.2f),
                    new Vector3(-0.8f, 0.55f, 0.3f), new Vector3(0.05f, 1.0f, -0.35f), new Vector3(0.9f, 0.5f, 0.55f),
                };
                foreach (Vector3 p in spots)
                {
                    m.Blob("berry", p, Vector3.one * 0.09f, seed, 0.1f, 0);
                }
            }

            Reg("Nature", name, m, true);
        }

        private static void GrassTuft(string name, int seed)
        {
            var m = new GmMesh();
            var rng = new System.Random(seed * 17);
            for (int i = 0; i < 7; i++)
            {
                float az = (float)rng.NextDouble() * 360f;
                float tilt = 8f + (float)rng.NextDouble() * 22f;
                float h = 0.45f + (float)rng.NextDouble() * 0.35f;
                string mat = i % 3 == 0 ? "grassLight" : "grassDark";
                var basePos = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.18f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.18f);
                m.Prism(mat, basePos, 0.055f, 0f, h, 3, new Vector3(tilt, az, 0f));
            }

            Reg("Nature", name, m, false);
        }

        private static void Flower(string name, string petals, int seed)
        {
            var m = new GmMesh();
            var rng = new System.Random(seed * 31);
            for (int i = 0; i < 3; i++)
            {
                float x = ((float)rng.NextDouble() - 0.5f) * 0.45f, z = ((float)rng.NextDouble() - 0.5f) * 0.45f;
                float h = 0.30f + (float)rng.NextDouble() * 0.14f;
                m.Prism("grassDark", new Vector3(x, 0f, z), 0.022f, 0.018f, h, 3, caps: false);
                m.Prism(petals, new Vector3(x, h, z), 0.14f, 0.12f, 0.035f, 6);
                m.Prism("flowerYellow", new Vector3(x, h + 0.03f, z), 0.06f, 0.05f, 0.03f, 5);
                m.Blob("grassLight", new Vector3(x + 0.12f, 0.06f, z), new Vector3(0.13f, 0.04f, 0.06f), seed + i, 0.1f, 0);
                m.Blob("grassLight", new Vector3(x - 0.1f, 0.06f, z + 0.05f), new Vector3(0.12f, 0.04f, 0.06f), seed + i + 5, 0.1f, 0);
            }

            Reg("Nature", name, m, false);
        }

        private static void Lavender(string name, int seed)
        {
            var m = new GmMesh();
            var rng = new System.Random(seed * 13);
            for (int s = 0; s < 4; s++)
            {
                float x = ((float)rng.NextDouble() - 0.5f) * 0.4f, z = ((float)rng.NextDouble() - 0.5f) * 0.4f;
                float h = 0.38f + (float)rng.NextDouble() * 0.16f;
                m.Prism("grassDark", new Vector3(x, 0f, z), 0.02f, 0.016f, h, 3, caps: false);
                for (int k = 0; k < 4; k++)
                {
                    m.Prism("flowerPurple", new Vector3(x, h - 0.05f + k * 0.075f, z), 0.065f - k * 0.008f, 0f, 0.13f, 4, twist: k);
                }
            }

            Reg("Nature", name, m, false);
        }

        private static void Mushroom(string name)
        {
            var m = new GmMesh();
            void One(Vector3 p, float s)
            {
                m.Push(p, Vector3.zero, Vector3.one * s);
                m.Prism("canvas", Vector3.zero, 0.06f, 0.05f, 0.14f, 6);
                m.Blob("mushroom", new Vector3(0f, 0.17f, 0f), new Vector3(0.17f, 0.10f, 0.17f), 3, 0.08f, 1, 0.35f);
                m.Blob("flowerWhite", new Vector3(0.06f, 0.25f, 0.04f), Vector3.one * 0.035f, 1, 0.1f, 0);
                m.Blob("flowerWhite", new Vector3(-0.07f, 0.23f, -0.03f), Vector3.one * 0.03f, 2, 0.1f, 0);
                m.Blob("flowerWhite", new Vector3(0.0f, 0.26f, -0.08f), Vector3.one * 0.028f, 3, 0.1f, 0);
                m.Pop();
            }

            One(new Vector3(0f, 0f, 0f), 1.2f);
            One(new Vector3(0.28f, 0f, 0.1f), 0.8f);
            One(new Vector3(-0.1f, 0f, 0.26f), 0.65f);
            Reg("Nature", name, m, false);
        }

        // ================================================================ rocks

        private static void RockBody(GmMesh m, Vector3 r, int seed, string mat, bool moss, int subdiv, Vector3 offset = default)
        {
            m.Blob(mat, offset + new Vector3(0f, r.y * 0.55f, 0f), r, seed, subdiv == 0 ? 0.52f : 0.46f, subdiv, 0.55f);
            if (moss)
            {
                m.Blob("moss", offset + new Vector3(0f, r.y * 1.02f, 0f), new Vector3(r.x * 0.72f, r.y * 0.28f, r.z * 0.72f), seed + 3, 0.28f, 0, 0.2f);
            }
        }

        private static void Rock(string name, Vector3 r, int seed, string mat, bool moss, int subdiv)
        {
            var m = new GmMesh();
            RockBody(m, r, seed, mat, moss, subdiv);
            bool solid = r.x >= 1f;
            Reg("Rocks", name, m, r.x >= 0.9f, solid ? ConvexCol() : null);
        }

        private static void RockCluster(string name, int seed, bool crystals)
        {
            var m = new GmMesh();
            RockBody(m, new Vector3(2.0f, 1.5f, 1.8f), seed, "rock", true, 0);
            RockBody(m, new Vector3(1.1f, 0.85f, 1.0f), seed + 2, "rockDark", false, 0, new Vector3(2.2f, 0f, 0.6f));
            RockBody(m, new Vector3(0.9f, 0.7f, 0.85f), seed + 4, "rockWarm", false, 0, new Vector3(-1.9f, 0f, 0.9f));
            RockBody(m, new Vector3(0.55f, 0.4f, 0.5f), seed + 6, "rock", false, 0, new Vector3(0.6f, 0f, -2.0f));
            if (crystals)
            {
                Crystal(m, new Vector3(0.2f, 1.9f, 0.3f), 1.5f, 0.3f, 6f, 20f);
                Crystal(m, new Vector3(-0.5f, 1.6f, 0.9f), 1.0f, 0.22f, 22f, 200f);
                Crystal(m, new Vector3(0.9f, 1.5f, -0.4f), 0.85f, 0.2f, 24f, 80f);
                Crystal(m, new Vector3(2.0f, 0.75f, 0.9f), 0.8f, 0.19f, 18f, 340f);
                Crystal(m, new Vector3(-1.9f, 0.6f, 1.3f), 0.6f, 0.15f, 28f, 250f);
            }

            Reg("Rocks", name, m, true, Both(SphereCol(new Vector3(0f, 1.0f, 0f), 1.7f), SphereCol(new Vector3(2.2f, 0.5f, 0.6f), 0.95f)));
        }

        private static void CrystalCluster(string name, int seed, float scale)
        {
            var m = new GmMesh();
            m.Blob("rockDark", new Vector3(0f, 0.12f, 0f) * scale, new Vector3(0.7f, 0.25f, 0.7f) * scale, seed, 0.3f, 0, 0.5f);
            var rng = new System.Random(seed * 7);
            Crystal(m, new Vector3(0f, 0.12f, 0f) * scale, 1.25f * scale, 0.22f * scale, 4f, 10f);
            for (int i = 0; i < 5; i++)
            {
                float az = i * 72f + (float)rng.NextDouble() * 30f;
                float h = (0.55f + (float)rng.NextDouble() * 0.45f) * scale;
                Crystal(m, new Vector3(Mathf.Cos(az * Mathf.Deg2Rad) * 0.25f, 0.1f, Mathf.Sin(az * Mathf.Deg2Rad) * 0.25f) * scale, h, 0.15f * scale,
                    18f + (float)rng.NextDouble() * 12f, -az + 90f, i % 2 == 0 ? "crystal" : "crystalLight", "crystalLight");
            }

            Reg("Rocks", name, m, scale > 0.8f); // decoration: deliberately no collider
        }

        private static void Log(string name)
        {
            var m = new GmMesh();
            const float len = 2.6f, r = 0.28f;
            m.Prism("trunk", new Vector3(-len / 2f, r, 0f), r, r, len, 7, new Vector3(0f, 0f, -90f), caps: false);
            m.Prism("wedge", new Vector3(len / 2f - 0.01f, r, 0f), r * 0.94f, r * 0.94f, 0.03f, 7, new Vector3(0f, 0f, -90f));
            m.Prism("wedge", new Vector3(-len / 2f - 0.02f, r, 0f), r * 0.94f, r * 0.94f, 0.03f, 7, new Vector3(0f, 0f, -90f));
            m.Prism("trunkDark", new Vector3(0.2f, r * 1.9f, 0.05f), 0.03f, 0.02f, 0.35f, 3, new Vector3(30f, 20f, 40f));
            Reg("Nature", name, m, true, BoxCol(new Vector3(0f, r, 0f), new Vector3(len, r * 2f, r * 2f)));
        }

        private static void Stump(string name)
        {
            var m = new GmMesh();
            m.Prism("trunk", Vector3.zero, 0.42f, 0.34f, 0.5f, 8, caps: false);
            m.Prism("wedge", new Vector3(0f, 0.49f, 0f), 0.34f, 0.32f, 0.03f, 8);
            m.Prism("trunkDark", new Vector3(0f, 0.515f, 0f), 0.17f, 0.15f, 0.02f, 8);
            Reg("Nature", name, m, true, Capsule(new Vector3(0f, 0.25f, 0f), 0.36f, 0.5f));
        }
    }
}
#endif
