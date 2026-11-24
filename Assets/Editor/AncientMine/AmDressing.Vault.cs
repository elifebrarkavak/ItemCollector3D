#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    public static partial class AmDressing
    {
        // ================================================================ E  ROYAL VAULT (floor 0): the finale. A colossal gate, a processional road, a symmetrical hall of pillars and kings, the dais, a giant crystal behind it

        private static void DressVault(Ctx c)
        {
            AmMesh m = c.Mesh;
            const AmZone Z = AmZone.Vault;
            int stone = P.Rock(Z, 5), dark = P.Rock(Z, 3), light = P.Rock(Z, 6);
            const float dais = 1.8f;
            m.GoldGlows = true;   // the vault's gold is self-lit: it shines in the purple light

            // ---- the colossal entrance in the east wall: a half-round arch with gold trim and keystone, a second shallower ring round it
            OrnateGateway(c, new Vector3(29.4f, 0f, -37f), 90f, 6.2f, 2.2f, 3.2f, 8f, stone, dark, P.Gold, P.GoldBright);
            m.Push(new Vector3(29.4f, 8f, -37f), 90f);
            m.ArchRing(Vector3.zero, 8.4f, 9.3f, 3.5f, 14, light, stone);
            m.Pop();

            // ---- two rows of four pillars joined by arch ribs across the hall and by beams along the rows; banners on the aisle faces
            foreach (int row in new[] { -1, 1 })
            {
                float z = -38f + row * 6.5f;
                foreach (float x in new[] { -5f, 5f, 15f, 24f })
                {
                    Pillar(c, x, z, 1.2f, 22f, Z, true, float.NaN, P.CrystalTurquoise);
                    Banner(c, new Vector3(x, 10.5f, z - row * 1.35f), row > 0 ? 180f : 0f, 1.3f, 6.2f, (x > 10f) ? P.ClothPurple : P.ClothGold, P.Gold, P.GoldBright);
                }

                m.Beam(new Vector3(-6f, 18f, z), new Vector3(26f, 18f, z), 1.4f, 1.4f, P.Rock(Z, 4));
            }

            foreach (float x in new[] { -5f, 5f, 15f, 24f })
            {
                m.Push(new Vector3(x, 13f, -38f), 90f);
                m.ArchRing(Vector3.zero, 5.1f, 6.6f, 1.4f, 12, stone, dark);
                m.Pop();
                m.Block(new Vector3(x, 13f + 5.85f, -38f), new Vector3(0.9f, 1.2f, 1.6f), P.Gold, P.GoldBright);
            }

            // ---- the processional road from the gate to the dais, with gold lines
            var road = new List<Vector2> { new(41f, -37f), new(28f, -37f), new(10f, -38f) };
            Ribbon(c, road, 6.6f, P.RoadStone, 0.05f);
            Ribbon(c, new List<Vector2> { new(41f, -33.9f), new(28f, -33.9f), new(10f, -34.9f) }, 0.14f, P.GoldBright, 0.09f);
            Ribbon(c, new List<Vector2> { new(41f, -40.1f), new(28f, -40.1f), new(10f, -41.1f) }, 0.14f, P.GoldBright, 0.09f);
            Ribbon(c, road, 0.4f, P.RoadInlay, 0.1f, true);

            // ---- the central dais: gold rings and a gold border on top, the altar on its north side (the way across and the centre stay free for the treasures of stage 16C)
            FloorDisc(c, 10f, -38f, 4.0f, P.GoldDark, dais + 0.05f, 24, false, dais - 0.05f);
            FloorDisc(c, 10f, -38f, 3.4f, P.Rock(Z, 6), dais + 0.06f, 24, false, dais - 0.05f);
            FloorSun(c, 10f, -38f, 2.3f, dais - 0.05f, P.Gold, P.PortalGlowBright);
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(new Vector3(10f, dais + 0.03f, -38f + side * 4.35f), new Vector3(13.6f, 0.06f, 0.3f), P.GoldBright, P.GoldBright);
                m.Box(new Vector3(10f + side * 6.85f, dais + 0.03f, -38f), new Vector3(0.3f, 0.06f, 8.8f), P.GoldBright, P.GoldBright);
            }

            m.Block(new Vector3(10f, dais, -34.6f), new Vector3(4.4f, 1.2f, 1.8f), dark, light);
            m.Block(new Vector3(10f, dais + 1.2f, -34.6f), new Vector3(3.6f, 0.5f, 1.4f), stone, P.Gold);
            CrystalSign(m, new Vector3(10f, dais + 0.7f, -33.68f), 0f, 0.9f, P.GoldBright, P.Gold);
            c.Box("Altar", new Vector3(10f, dais + 0.85f, -34.6f), new Vector3(4.4f, 1.7f, 1.8f));

            // ---- three kings along the north wall facing the dais (between the pillars), carved panels and gold friezes on the walls
            foreach (float x in new[] { 0f, 10f, 20f })
            {
                Statue(c, x, -29f, 180f, 1.1f, 1, 0f);
            }

            foreach (float x in new[] { 5f, 15f })
            {
                Relief(c, Wall("Vault", 2, x, 8f), 3.0f, 6f, x < 10f ? 3 : 0, stone, P.Gold, P.GoldBright);
                Relief(c, Wall("Vault", 3, x, 8f), 2.8f, 6f, x < 10f ? 1 : 3, stone, P.Gold, P.GoldBright);
            }

            foreach (float z in new[] { -31.8f, -44.2f })
            {
                Relief(c, Wall("Vault", 1, z, 8f), 2.4f, 6f, z > -38f ? 3 : 1, stone, P.Gold, P.GoldBright);
            }

            Frieze(c, "Vault", 2, -6f, 25f, 11.8f, 1.0f, P.Gold, P.GoldBright);
            Frieze(c, "Vault", 3, -6f, 25f, 11.8f, 1.0f, P.Gold, P.GoldBright);
            Frieze(c, "Vault", 1, -45f, -31f, 11.8f, 1.0f, P.Gold, P.GoldBright);

            // ---- crystals: the giant one behind the dais with a glowing ring round its foot (visible down the whole axis from the gate), four big ones in the corners
            Formation(c, -6.8f, -38f, 13f, 2, 9, 2.4f, 8f, 800, 0);
            FloorRing(c, -6.8f, -38f, 3.2f, 3.4f, P.WaterTurquoise, 0.1f, 24, true, 0f);
            Formation(c, -6.3f, -28.9f, 8f, 0, 6, 1.3f, 6f, 801, 3);
            Formation(c, -6.3f, -47.2f, 8.5f, 3, 6, 1.3f, 6f, 802, 1);
            Formation(c, 26.2f, -28.9f, 8f, 1, 6, 1.3f, 6f, 803, 4);
            Formation(c, 26.2f, -47.2f, 8f, 4, 6, 1.3f, 6f, 804, 2);

            // ---- three treasure alcoves in the south wall: a gold-framed niche, a pedestal, crystals either side, a gold ring on the floor (the treasures themselves come in stage 16C)
            foreach (float x in new[] { 0f, 10f, 20f })
            {
                OrnateGateway(c, new Vector3(x, 0f, -51.75f), 0f, 2.5f, 0.8f, 0.5f, 3.6f, stone, dark, P.Gold, P.GoldBright, false);   // a niche frame BEHIND the pedestal: nothing narrows the way in
                Vector3 b = c.On(x, -50f);
                m.Block(b, new Vector3(3.4f, 0.9f, 2.4f), dark, light);
                m.Block(b + Vector3.up * 0.9f, new Vector3(2.4f, 0.4f, 1.6f), stone, P.Gold);
                CrystalSign(m, b + new Vector3(0f, 0.45f, 1.22f), 0f, 0.8f, P.GoldBright, P.Gold);
                c.Box("Pedestal", b + Vector3.up * 0.55f, new Vector3(3.4f, 1.1f, 2.4f));
                FloorRing(c, x, -46.2f, 1.3f, 1.45f, P.GoldBright, 0.09f, 20, false, 0f);
                Formation(c, x - 2.4f, -51.3f, 2.6f, (int)(x / 10f) + 1, 3, 0.7f, 0f, 810 + (int)x, -1, false);
                Formation(c, x + 2.4f, -51.3f, 2.6f, (int)(x / 10f) + 3, 3, 0.7f, 0f, 820 + (int)x, -1, false);
            }

            // ---- light from above: a ring of hanging crystals over the dais, more over the aisles, and rock spikes
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                Hanging(c, 10f + Mathf.Cos(a) * 5.6f, -38f + Mathf.Sin(a) * 5.0f, 4.4f + (k & 1) * 1.4f, k % 5, true, stone);
            }

            for (int k = 0; k < 14; k++)
            {
                Hanging(c, -5f + (k % 5) * 7.5f + Rand(c, k) * 1.5f, -46f + (k / 5) * 8f + Rand(c, k, 1) * 1.5f, 3f + Rand(c, k, 2) * 3.4f, (k + 2) % 5, true, stone);
            }

            for (int k = 0; k < 10; k++)
            {
                Hanging(c, -7f + Rand(c, k, 5) * 34f, -48f + Rand(c, k, 6) * 20f, 2f + Rand(c, k, 7) * 2.5f, 0, false, P.Rock(Z, 2));
            }

            Scraps(c, 21f, -45f, 5, 3f, 830, 4, 0f);
        }
    }
}
#endif
