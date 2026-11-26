#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    public static partial class AmDressing
    {
        // ================================================================ A  ANCIENT GATE (floor 15.5) and the Hidden Cistern

        private static void DressGate(Ctx c)
        {
            AmMesh m = c.Mesh;
            const float f = 15.5f;
            const AmZone Z = AmZone.Gate;
            int stone = P.Rock(Z, 5), dark = P.Rock(Z, 3), light = P.Rock(Z, 6);

            // ---- the great portal in the west wall: layered arches, gold trim and keystone, crystal signs on the piers, a glowing doorway behind it, a low step in front
            Vector3 portal = new(-60.4f, f, -2f);
            OrnateGateway(c, portal, 90f, 5.6f, 2.4f, 3.4f, 9f, stone, dark, P.Gold, P.GoldBright);
            m.Push(portal + Vector3.up * 9f, 90f);
            m.ArchRing(Vector3.zero, 8.0f, 8.9f, 2.8f, 12, light, stone);   // an outer, shallower ring: the doorway "steps" into the wall
            m.Pop();
            m.Glow = true;
            var door = new List<Vector3> { new(-60.2f, f, -2f + 5.6f) };
            for (int k = 0; k <= 10; k++)
            {
                float a = Mathf.PI * k / 10f;
                door.Add(new Vector3(-60.2f, f + 9f + Mathf.Sin(a) * 5.6f, -2f + Mathf.Cos(a) * 5.6f));
            }

            door.Add(new Vector3(-60.2f, f, -2f - 5.6f));
            m.FaceWorld(P.PortalGlow, Vector3.right, door.ToArray());
            var core = new List<Vector3> { new(-60.15f, f, -2f + 3.0f) };
            for (int k = 0; k <= 8; k++)
            {
                float a = Mathf.PI * k / 8f;
                core.Add(new Vector3(-60.15f, f + 9f + Mathf.Sin(a) * 3.0f, -2f + Mathf.Cos(a) * 3.0f));
            }

            core.Add(new Vector3(-60.15f, f, -2f - 3.0f));
            m.FaceWorld(P.PortalGlowBright, Vector3.right, core.ToArray());
            m.Glow = false;
            m.Block(new Vector3(-59.0f, f, -2f), new Vector3(2.4f, 0.25f, 13.5f), P.RoadStoneDark, P.RoadStone); // 0.25 m: lower than the controller's 0.3 m step, so it is walked over, not a wall
            c.Box("PortalStep", new Vector3(-59.0f, f + 0.125f, -2f), new Vector3(2.4f, 0.25f, 13.5f));

            // ---- the old stone road: from the portal through the hall and down the ramp to the Hall of Pillars, with gold lines along its edges and a sun medallion in front of the portal
            var road = new List<Vector2> { new(-58f, -2f), new(-40f, -2f), new(-32f, -2f), new(-24f, -2f) };
            Ribbon(c, road, 6.6f, P.RoadStone, 0.05f);
            Ribbon(c, new List<Vector2> { new(-58f, -5.0f), new(-24f, -5.0f) }, 0.5f, P.RoadStoneDark, 0.07f);
            Ribbon(c, new List<Vector2> { new(-58f, 1.0f), new(-24f, 1.0f) }, 0.5f, P.RoadStoneDark, 0.07f);
            Ribbon(c, new List<Vector2> { new(-58f, -5.45f), new(-24f, -5.45f) }, 0.14f, P.GoldBright, 0.09f);
            Ribbon(c, new List<Vector2> { new(-58f, 1.45f), new(-24f, 1.45f) }, 0.14f, P.GoldBright, 0.09f);
            Ribbon(c, road, 0.4f, P.RoadInlay, 0.1f, true);

            // ---- the second arch over the way out, and the two great guardians that flank the road (they face each other across it)
            OrnateGateway(c, new Vector3(-38.6f, c.Floor(-38.6f, -2f), -2f), 90f, 4.9f, 1.7f, 2.6f, 8f, stone, dark, P.Gold, P.GoldBright);
            Statue(c, -42.5f, -9.6f, 0f, 1f, 0, f);
            Statue(c, -42.5f, 6.4f, 180f, 1f, 0, f);

            // ---- fallen and broken columns and arch stones beside the road, leaving it free
            BrokenPillar(c, -55.5f, 6.5f, 1.25f, 5.5f, 340f, Z, true, f);
            BrokenPillar(c, -50.4f, 7.4f, 1.2f, 3.2f, 200f, Z, false, f);
            BrokenPillar(c, -46.8f, 6.8f, 1.15f, 6.4f, 60f, Z, false, f);
            BrokenPillar(c, -51.0f, -9.8f, 1.2f, 4.0f, 330f, Z, false, f);
            BrokenPillar(c, -47.2f, -10.4f, 1.25f, 2.6f, 100f, Z, false, f);
            BrokenPillar(c, -58.0f, 8.4f, 1.1f, 2.2f, 300f, Z, false, f);
            for (int k = 0; k < 3; k++)
            {
                float wx = new[] { -49.5f, -53.5f, -45.6f }[k], wz = new[] { 3.8f, 4.6f, -7.0f }[k], yaw = new[] { 25f, 110f, 70f }[k];
                m.Push(new Vector3(wx, f, wz), Quaternion.Euler(0f, yaw, 0f));
                m.Hexa(new[] { V(-0.8f, 0f, -0.5f), V(0.8f, 0f, -0.5f), V(0.8f, 0f, 0.5f), V(-0.8f, 0f, 0.5f) }, new[] { V(-0.55f, 1.0f, -0.5f), V(0.55f, 1.0f, -0.5f), V(0.55f, 1.0f, 0.5f), V(-0.55f, 1.0f, 0.5f) }, stone, light);
                m.Pop();
                c.Box("ArchStone", new Vector3(wx, f + 0.5f, wz), new Vector3(1.6f, 1.0f, 1.0f), yaw);
            }

            Scraps(c, -52f, 2f, 10, 6f, 101, 4, f);
            Scraps(c, -47f, -8f, 8, 5f, 102, 4, f);
            Scraps(c, -43f, 3f, 5, 3f, 103, 4, f);
            m.Push(c.On(-59.2f, -11.9f), 20f);   // the hidden passage is half-covered by a leaning slab (it does not block it)
            m.Box(new Vector3(0f, 1.6f, 0f), new Vector3(1.0f, 3.2f, 3.4f), stone, light);
            m.Pop();
            c.Box("LeaningSlab", c.On(-59.2f, -11.9f, 1.6f), new Vector3(1.0f, 3.2f, 3.4f), -20f);

            // ---- the walls: carved panels (crystal signs, suns, crossed pickaxes, a crown) and gold friezes; ancient symbols are the room's decoration
            foreach (float x in new[] { -55f, -50f, -45f })
            {
                Relief(c, Wall("GateHall", 2, x, f + 7f), 3.4f, 6.4f, x == -50f ? 1 : 0, stone, P.Gold, P.GoldBright);
            }

            foreach (float x in new[] { -48f, -44f })
            {
                Relief(c, Wall("GateHall", 3, x, f + 7f), 3.4f, 6.4f, x == -48f ? 2 : 3, stone, P.Gold, P.GoldBright);
            }

            Frieze(c, "GateHall", 2, -58f, -43f, f + 11.5f, 0.9f, P.Gold, P.GoldBright);
            Frieze(c, "GateHall", 3, -50f, -42f, f + 11.5f, 0.9f, P.Gold, P.GoldBright);

            // ---- crystals: formations in the corners and a few small ones along the road, cool cyan, blue and turquoise (the light of the start)
            Formation(c, -59.4f, 8.2f, 4.4f, 0, 5, 1.2f, 3.5f, 1);
            Formation(c, -52.5f, 8.9f, 3.0f, 3, 4, 0.9f, 0f, 2, -1, false);
            Formation(c, -40.4f, 9.0f, 3.6f, 2, 4, 1.0f, 2.8f, 3);
            Formation(c, -40.6f, -12.6f, 3.4f, 1, 4, 1.0f, 2.6f, 4);
            Formation(c, -47.6f, -13.1f, 2.4f, 3, 3, 0.8f, 0f, 5, -1, false);
            for (int k = 0; k < 7; k++)
            {
                float x = -57f + k * 2.9f;
                Sprinkle(c, x, 4.1f + Rand(c, k) * 0.6f, 1.0f, k % 2 == 0 ? 0 : 3, 3, 200 + k, f);
                Sprinkle(c, x + 1.2f, -8.2f - Rand(c, k, 1) * 0.6f, 1.0f, k % 2 == 0 ? 3 : 1, 3, 230 + k, f);
            }

            // ---- the ceiling: rock spikes and glowing crystals
            for (int k = 0; k < 16; k++)
            {
                Hanging(c, -59f + Rand(c, k, 1) * 19f, -12f + Rand(c, k, 2) * 21f, 2.4f + Rand(c, k, 3) * 3.2f, k % 5, k % 3 == 0, P.Rock(Z, 2));
            }

            DressCistern(c);
        }

        /// <summary>The Hidden Cistern: a round stone basin of glowing turquoise water in a dark cave, two gate posts, a gold ring and diamonds on the floor, crystals round the walls.</summary>
        private static void DressCistern(Ctx c)
        {
            AmMesh m = c.Mesh;
            const AmZone Z = AmZone.Gate;
            float g = c.Floor(-54f, -33f);
            Vector3 b = new(-54f, g, -33f);
            m.Poly(b, 2.95f, 2.8f, 0.5f, 8, P.StatueDark, P.StatueMid, 22.5f);
            FloorRing(c, -54f, -33f, 2.4f, 2.95f, P.GoldBright, 0.52f, 16, false, g);
            FloorDisc(c, -54f, -33f, 2.4f, P.WaterTurquoise, 0.54f, 18, true, g);
            FloorRing(c, -54f, -33f, 1.2f, 1.4f, P.WaterDeep, 0.55f, 18, true, g);
            c.Box("CisternBasin", b + Vector3.up * 0.25f, new Vector3(5.2f, 0.5f, 5.2f));
            c.Box("CisternBasin", b + Vector3.up * 0.25f, new Vector3(5.2f, 0.5f, 5.2f), 45f);
            FloorRing(c, -54f, -33f, 3.4f, 3.55f, P.GoldBright, 0.08f, 28, false, g);
            for (int k = 0; k < 8; k++)
            {
                float a = (k * 45f + 22.5f) * Mathf.Deg2Rad;
                float px = -54f + Mathf.Cos(a) * 4.3f, pz = -33f + Mathf.Sin(a) * 4.3f;
                FloorPolygon(c, P.Gold, 0.08f, false, g, new Vector2(px, pz + 0.45f), new Vector2(px + 0.28f, pz), new Vector2(px, pz - 0.45f), new Vector2(px - 0.28f, pz));
            }

            // the way into the cistern: a small gold-trimmed arch over the passage (4.9 m clear between its piers)
            OrnateGateway(c, new Vector3(-54.6f, c.Floor(-54.6f, -25.5f), -25.5f), 0f, 3.0f, 0.7f, 0.9f, 4.5f, P.Rock(Z, 5), P.Rock(Z, 3), P.Gold, P.GoldBright);
            Formation(c, -58f, -37f, 4.6f, 3, 6, 1.3f, 3.5f, 7, 0);
            Formation(c, -49f, -38f, 3.6f, 1, 5, 1.1f, 3f, 8, 3);
            Formation(c, -54f, -40.6f, 3.2f, 0, 4, 0.9f, 0f, 9, 3);
            Sprinkle(c, -59.5f, -33f, 1.0f, 3, 4, 301);
            Sprinkle(c, -48.2f, -34.5f, 1.0f, 0, 4, 302);
            for (int k = 0; k < 10; k++)
            {
                Hanging(c, -60f + Rand(c, k, 41) * 12f, -40f + Rand(c, k, 42) * 12f, 2.0f + Rand(c, k, 43) * 3.4f, k % 2 == 0 ? 3 : 0, k % 2 == 0, P.Rock(Z, 2));
            }
        }
    }
}
#endif
