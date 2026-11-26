#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    public static partial class AmDressing
    {
        /// <summary>Small crystals growing along both sides of a route (visual only, no colliders), kept against the cave walls and out of the walking line.</summary>
        private static void FlankRoute(Ctx c, IList<Vector2> points, float spacing, float offset, int theme, int seed)
        {
            int n = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                float length = Vector2.Distance(a, b);
                Vector2 dir = (b - a) / Mathf.Max(0.01f, length), side = new(-dir.y, dir.x);
                for (float d = spacing * 0.5f; d < length; d += spacing)
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector2 p = a + dir * d + side * (offset + Rand(c, seed, n) * 0.6f) * s;
                        n++;
                        if (AmLayout.EdgeDistance(p.x, p.y) > -0.5f || AmLayout.EdgeDistance(p.x, p.y) < -2.6f)
                        {
                            continue;
                        }

                        Sprinkle(c, p.x, p.y, 0.9f + Rand(c, seed, n + 40) * 0.9f, (theme + n) % 5, 3 + (n & 1), seed * 10 + n);
                    }
                }
            }
        }

        // ================================================================ C  CRYSTAL CAVERNS (floors 7.6 .. 3.2): the most magical place of the mine

        private static void DressCaverns(Ctx c)
        {
            AmMesh m = c.Mesh;
            const AmZone Z = AmZone.Crystal;
            int placed = 0;
            float gy = c.Floor(48f, -4f);

            // ---- the atrium: a paved plaza with a cyan ring, a GIANT formation opposite the entrance (the first thing you see when the road from the hall opens into the cave),
            //      a ring of medium formations round the rim (never on a route) and layered rock outcrops against the walls
            FloorDisc(c, 48f, -4f, 5.2f, P.RoadStoneDark, 0.17f, 28);
            FloorDisc(c, 48f, -4f, 4.4f, P.RoadStone, 0.18f, 28);
            FloorRing(c, 48f, -4f, 3.6f, 3.75f, P.RoadInlay, 0.2f, 28, true);

            Formation(c, 56.2f, -4f, 14.5f, 2, 10, 3.3f, 8f, 100, 0);
            placed++;
            (float x, float z, float h, int theme, int accent)[] ring =
            {
                (54.5f, 1.8f, 8f, 0, 2), (54.5f, -9.8f, 8.5f, 3, 1), (58.8f, 0.2f, 6.5f, 4, 2), (58.6f, -8.4f, 6f, 1, 4), (51.5f, 5.8f, 6f, 3, 0),
                (41.4f, 4.6f, 7f, 1, 2), (41.2f, -11.5f, 6.5f, 0, 3), (44.5f, -14.2f, 5.5f, 2, 4), (38.2f, 1.2f, 4.5f, 3, 0),
            };
            for (int k = 0; k < ring.Length; k++)
            {
                placed += TryFormation(c, ring[k].x, ring[k].z, ring[k].h, ring[k].theme, 6 + k % 3, 1.5f, 5f, 110 + k, ring[k].accent) ? 1 : 0;
            }

            for (int k = 0; k < 14; k++)
            {
                float a = Rand(c, k, 30) * Mathf.PI * 2f, r = 2f + Rand(c, k, 31) * 9.5f;
                float x = 48f + Mathf.Cos(a) * r, z = -4f + Mathf.Sin(a) * r;
                if (AmLayout.RouteDistance(x, z) > 3.4f && AmLayout.EdgeDistance(x, z) < -1.5f)
                {
                    Sprinkle(c, x, z, 1.0f + Rand(c, k, 32) * 0.9f, k % 5, 4, 200 + k);
                }
            }

            int strata = 0;
            for (int k = 0; k < 20; k++)
            {
                float a = (k * 18f + 6f) * Mathf.Deg2Rad;
                float x = 48f + Mathf.Cos(a) * 11.6f, z = -4f + Mathf.Sin(a) * 11.6f;
                if (AmLayout.RouteDistance(x, z) > 6.2f && AmLayout.EdgeDistance(x, z) < -0.8f && strata < 7)
                {
                    Strata(c, x, z, 1.5f, 4.6f, 4, 300 + k);
                    strata++;
                }
            }

            // ---- paved paths with small crystals along both sides: the main route to the vault, the north branch and the grotto path
            var mainPath = new List<Vector2>();
            for (int i = 9; i <= 16; i++)
            {
                mainPath.Add(AmLayout.MainRoute[i].point);
            }

            var northPath = new List<Vector2>();
            for (int i = 10; i < AmLayout.AlternateRoute.Length - 4; i++)
            {
                northPath.Add(AmLayout.AlternateRoute[i].point);
            }

            Ribbon(c, mainPath, 3.4f, P.RoadStone, 0.17f, false, 2f);
            Ribbon(c, mainPath, 0.35f, P.RoadInlay, 0.2f, true, 2f);
            Ribbon(c, northPath, 3.4f, P.RoadStone, 0.17f, false, 2f);
            Ribbon(c, northPath, 0.35f, P.RoadInlay, 0.2f, true, 2f);
            FlankRoute(c, mainPath, 5.2f, 3.9f, 0, 21);
            FlankRoute(c, northPath, 5.2f, 3.9f, 3, 22);

            // ---- the northern lobe and the southern lobe (the last cave before the Royal Vault: the biggest crystals)
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 60f + 30f) * Mathf.Deg2Rad;
                placed += TryFormation(c, 46f + Mathf.Cos(a) * 5.6f, 33f + Mathf.Sin(a) * 5.6f, 5.5f + Rand(c, k, 40) * 5f, (k + 2) % 5, 6, 1.4f, 4.5f, 400 + k, (k + 4) % 5) ? 1 : 0;
            }

            for (int k = 0; k < 6; k++)
            {
                float a = (k * 60f + 10f) * Mathf.Deg2Rad;
                placed += TryFormation(c, 47f + Mathf.Cos(a) * 5.6f, -37f + Mathf.Sin(a) * 5.6f, 7f + Rand(c, k, 50) * 6f, (k + 1) % 5, 7, 1.6f, 5f, 500 + k, (k + 3) % 5) ? 1 : 0;
            }

            // ---- the grotto: a special place. A huge violet and magenta formation at the back, a turquoise and a cyan one beside it, a glowing pool, a sun symbol round the route's end, small crystals in a circle
            float g = c.Floor(36f, 14f);
            Formation(c, 31.4f, 14f, 10.5f, 4, 9, 2.2f, 7f, 600, 2);
            Formation(c, 33.4f, 9.6f, 5.4f, 3, 6, 1.2f, 4f, 601, 1);
            Formation(c, 33.4f, 18.4f, 5.6f, 0, 6, 1.2f, 4f, 602, 4);
            placed += 3;
            FloorDisc(c, 37.8f, 9.8f, 1.7f, P.WaterDeep, 0.17f, 18, true);
            FloorDisc(c, 37.8f, 9.8f, 1.35f, P.WaterTurquoise, 0.19f, 18, true);
            FloorRing(c, 37.8f, 9.8f, 1.75f, 1.9f, P.GoldBright, 0.2f, 20, false);
            for (int k = 0; k < 8; k++)
            {
                float a = (k * 45f + 10f) * Mathf.Deg2Rad;
                Sprinkle(c, 36f + Mathf.Cos(a) * 4.2f, 14f + Mathf.Sin(a) * 4.2f, 1.1f, k % 2 == 0 ? 4 : 3, 3, 700 + k);
            }

            // ---- the ceiling: rock spikes and many glowing crystals
            var anchors = new[] { new Vector2(48f, -4f), new Vector2(52f, 12f), new Vector2(46f, 33f), new Vector2(50f, -20f), new Vector2(47f, -37f), new Vector2(36f, 14f), new Vector2(24f, 38f) };
            int hang = 0;
            for (int k = 0; k < 84; k++)
            {
                Vector2 a = anchors[k % anchors.Length];
                float x = a.x + (Rand(c, k, 70) - 0.5f) * 20f, z = a.y + (Rand(c, k, 71) - 0.5f) * 20f;
                if (AmLayout.EdgeDistance(x, z) > -1.5f)
                {
                    continue;
                }

                Hanging(c, x, z, 1.8f + Rand(c, k, 72) * 3.8f, k % 5, k % 2 == 0, P.Rock(Z, 2));
                hang++;
            }

            Debug.Log($"AncientMine Crystal Caverns: {placed} formations on the floor, {strata} rock outcrops, {hang} ceiling spikes and crystals");
            m.Glow = false;
        }
    }
}
#endif
