#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    public static partial class AmDressing
    {
        // ================================================================ B  HALL OF PILLARS (floor 11): the grand, neutral heart of the mine

        private static void DressHall(Ctx c)
        {
            AmMesh m = c.Mesh;
            const float f = 11f;
            const AmZone Z = AmZone.Hall;
            int stone = P.Rock(Z, 5), dark = P.Rock(Z, 3), light = P.Rock(Z, 6);

            // ---- the road: west door -> centre -> east door, a carpet of stone with gold lines, and a sun medallion with rings in the middle of the hall
            var road = new List<Vector2> { new(-24f, -2f), new(-10f, 0f), new(0f, 0f), new(12f, -2f), new(24f, -4f) };
            Ribbon(c, road, 6.6f, P.RoadStone, 0.05f);
            Ribbon(c, new List<Vector2> { new(-24f, -5.1f), new(-10f, -3.1f), new(0f, -3.1f), new(12f, -5.1f), new(24f, -7.1f) }, 0.14f, P.GoldBright, 0.09f);
            Ribbon(c, new List<Vector2> { new(-24f, 1.1f), new(-10f, 3.1f), new(0f, 3.1f), new(12f, 0.9f), new(24f, -1.1f) }, 0.14f, P.GoldBright, 0.09f);
            Ribbon(c, road, 0.4f, P.RoadInlay, 0.1f, true);
            FloorDisc(c, 0f, 0f, 7.6f, P.RoadStoneDark, 0.06f);
            FloorDisc(c, 0f, 0f, 6.2f, P.StatueMid, 0.07f);
            FloorRing(c, 0f, 0f, 8.1f, 8.3f, P.GoldBright, 0.08f, 36, false, f);
            FloorRing(c, 0f, 0f, 11.6f, 11.75f, P.Gold, 0.08f, 44, false, f);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f + Mathf.PI / 8f;
                float px = Mathf.Cos(a) * 9.9f, pz = Mathf.Sin(a) * 9.9f;
                FloorPolygon(c, P.Gold, 0.08f, false, f, new Vector2(px + Mathf.Cos(a) * 0.6f, pz + Mathf.Sin(a) * 0.6f), new Vector2(px - Mathf.Sin(a) * 0.3f, pz + Mathf.Cos(a) * 0.3f),
                    new Vector2(px - Mathf.Cos(a) * 0.6f, pz - Mathf.Sin(a) * 0.6f), new Vector2(px + Mathf.Sin(a) * 0.3f, pz - Mathf.Cos(a) * 0.3f));
            }

            // ---- two rows of six great pillars, 8.4 m apart (6.1 m clear between two), joined by arch ribs across the aisle and by beams along the rows
            for (int row = -1; row <= 1; row += 2)
            {
                float z = row * 8f;
                for (int k = 0; k < 6; k++)
                {
                    Pillar(c, -21f + k * 8.4f, z, 1.15f, 21f, Z);
                }

                m.Beam(new Vector3(-22f, f + 17.4f, z), new Vector3(22f, f + 17.4f, z), 1.3f, 1.3f, P.Rock(Z, 4));
            }

            for (int k = 0; k < 6; k++)
            {
                m.Push(new Vector3(-21f + k * 8.4f, f + 11f, 0f), 90f);
                m.ArchRing(Vector3.zero, 7.0f, 8.6f, 1.5f, 12, stone, dark);
                m.Pop();
                m.Block(new Vector3(-21f + k * 8.4f, f + 11f + 7.6f, 0f), new Vector3(1.0f, 1.3f, 1.7f), P.Gold, P.GoldBright);   // a gold keystone on every rib
            }

            // banners hang on the aisle side of the pillars, high above the walkers
            for (int row = -1; row <= 1; row += 2)
            {
                for (int k = 0; k < 6; k++)
                {
                    Banner(c, new Vector3(-21f + k * 8.4f, f + 10f, row * 6.7f), row > 0 ? 180f : 0f, 1.3f, 6f, (k + row) % 2 == 0 ? P.ClothIndigo : P.ClothTeal, P.Gold, P.GoldBright);
                }
            }

            // ---- the three exits: a carved arch in front of each door
            OrnateGateway(c, new Vector3(-24.8f, f, -2f), 90f, 5.0f, 1.9f, 2.6f, 9f, stone, dark, P.Gold, P.GoldBright);
            OrnateGateway(c, new Vector3(24.8f, f, -4f), 90f, 5.0f, 1.9f, 2.6f, 9f, stone, dark, P.Gold, P.GoldBright);
            OrnateGateway(c, new Vector3(0f, f, 20.8f), 0f, 5.0f, 1.9f, 2.6f, 9f, stone, dark, P.Gold, P.GoldBright);

            // ---- the four daises: a sentinel statue in each corner facing the hall, a gold ring inlay, a low plinth wall along the front
            foreach (var d in new[] { (-1f, 1f), (1f, 1f), (-1f, -1f), (1f, -1f) })
            {
                float cx = d.Item1 * 20f, cz = d.Item2 * 17f;
                float sx = d.Item1 * 22.2f, sz = d.Item2 * 18.2f;
                Statue(c, sx, sz, Mathf.Atan2(-sx, -sz) * Mathf.Rad2Deg, 0.5f, d.Item1 * d.Item2 > 0f ? 0 : 1, 13.4f);
                FloorRing(c, cx, cz, 1.5f, 1.65f, P.GoldBright, 0.08f, 20, false, 13.4f);
                FloorPolygon(c, P.Gold, 0.08f, false, 13.4f, new Vector2(cx, cz + 1.2f), new Vector2(cx + 0.55f, cz), new Vector2(cx, cz - 1.2f), new Vector2(cx - 0.55f, cz));
                m.Block(new Vector3(cx, 13.4f, cz - d.Item2 * 3.2f), new Vector3(8f, 0.5f, 0.6f), P.Rock(Z, 3), P.GoldBright);
                Sprinkle(c, d.Item1 * 17.5f, d.Item2 * 19.2f, 1.1f, d.Item1 > 0f ? 0 : 2, 3, 400 + (int)(d.Item1 * 3 + d.Item2 * 5), 13.4f);
            }

            // ---- broken pillars in the outer aisles (the hall is old)
            BrokenPillar(c, -21.5f, 12.6f, 1.15f, 5.2f, 0f, Z, false);
            BrokenPillar(c, 21.5f, 12.6f, 1.15f, 3.4f, 0f, Z, false);
            BrokenPillar(c, -21.5f, -12.6f, 1.15f, 4.0f, 0f, Z, false);
            BrokenPillar(c, 21.5f, -12.6f, 1.15f, 6.0f, 0f, Z, false);
            Scraps(c, -14f, 12f, 8, 4f, 501, 4);
            Scraps(c, 15f, -12f, 8, 4f, 502, 4);
            Scraps(c, 0f, 17f, 6, 3f, 503, 4);

            // ---- the walls: carved panels in all four wall faces and two gold friezes
            float ry = f + 7.2f;
            int style = 0;
            foreach (float x in new[] { -16.5f, -10.5f, 10.5f, 16.5f })
            {
                Relief(c, Wall("Hall", 2, x, ry), 3.6f, 6f, style++ % 4, stone, P.Gold, P.GoldBright);
            }

            foreach (float x in new[] { -16f, -8f, 0f, 8f, 16f })
            {
                Relief(c, Wall("Hall", 3, x, ry), 3.6f, 6f, style++ % 4, stone, P.Gold, P.GoldBright);
            }

            foreach (float z in new[] { -14.2f, -11f, 8.5f, 12f })
            {
                Relief(c, Wall("Hall", 1, z, ry), 2.8f, 6f, style++ % 4, stone, P.Gold, P.GoldBright);
            }

            foreach (float z in new[] { -14.6f, 6f, 9.5f })
            {
                Relief(c, Wall("Hall", 0, z, ry), 2.8f, 6f, style++ % 4, stone, P.Gold, P.GoldBright);
            }

            Frieze(c, "Hall", 2, -22f, -7.5f, f + 11.2f, 0.9f, P.Gold, P.GoldBright);
            Frieze(c, "Hall", 2, 7.5f, 22f, f + 11.2f, 0.9f, P.Gold, P.GoldBright);
            Frieze(c, "Hall", 3, -22f, 22f, f + 11.2f, 0.9f, P.Gold, P.GoldBright);

            // ---- a few crystal formations against the walls (never on a route), and light from above: hanging crystals, rock spikes
            int placed = 0;
            (float x, float z, float h, int theme)[] spots = { (-22f, 6f, 4f, 2), (22f, 4f, 4f, 0), (-22f, -7f, 3.6f, 1), (22f, -13f, 3.4f, 3), (-12f, -19f, 4f, 0), (12f, 19f, 4.2f, 2), (-6f, 19f, 3.6f, 1) };
            for (int k = 0; k < spots.Length; k++)
            {
                placed += TryFormation(c, spots[k].x, spots[k].z, spots[k].h, spots[k].theme, 4, 1.1f, 3.2f, 600 + k) ? 1 : 0;
            }

            for (int k = 0; k < 12; k++)
            {
                Hanging(c, -19f + (k % 4) * 12.6f + Rand(c, k) * 2f, -13f + (k / 4) * 13f + Rand(c, k, 1) * 2f, 3.0f + Rand(c, k, 2) * 2.4f, k % 5, true, stone);
            }

            for (int k = 0; k < 10; k++)
            {
                Hanging(c, -22f + Rand(c, k, 5) * 44f, -18f + Rand(c, k, 6) * 36f, 2.0f + Rand(c, k, 7) * 2.5f, 0, false, P.Rock(Z, 2));
            }

            Debug.Log($"AncientMine Hall of Pillars: {placed} wall formations");
        }
    }
}
#endif
