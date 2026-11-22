#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    public static partial class RcDressing
    {
        /// <summary>Sparse desert plants on one set of zones between two edge distances: loose, uneven placement, never on the hand-placed props, never on a steep slope.</summary>
        private static void Plants(RcMesh rm, Transform cols, RcZone[] zones, float minEdge, float maxEdge, int saguaros, int barrels, int pears, int bushes, int tufts, int tumble, System.Func<float, float, bool> allowed = null, float tuftsMinEdge = float.NaN)
        {
            int[] counts = { saguaros, barrels, pears, bushes, tufts, tumble };
            float[] radius = { 1.1f, 0.7f, 0.8f, 0.9f, 0.35f, 0.7f };
            var placed = new List<Vector2>();
            for (int kind = 0; kind < counts.Length; kind++)
            {
                int made = 0;
                for (int attempt = 0; attempt < 5000 && made < counts[kind]; attempt++)
                {
                    float x = R(-RcLayout.HalfX + 6f, RcLayout.HalfX - 6f), z = R(-RcLayout.HalfZ + 6f, RcLayout.HalfZ - 6f);
                    if (System.Array.IndexOf(zones, RcLayout.ZoneAt(x, z)) < 0)
                    {
                        continue;
                    }

                    float edge = RcLayout.EdgeDistance(x, z);
                    float lo = kind == 4 && !float.IsNaN(tuftsMinEdge) ? tuftsMinEdge : minEdge;
                    if (edge < lo || edge > maxEdge || RcLayout.Slope(x, z) > 0.3f || !IsFree(x, z, radius[kind]) || (allowed != null && !allowed(x, z)))
                    {
                        continue;
                    }

                    bool clash = false;
                    foreach (Vector2 q in placed)
                    {
                        clash |= (q - new Vector2(x, z)).sqrMagnitude < (kind == 4 ? 0.8f : 2.6f) * (kind == 4 ? 0.8f : 2.6f);
                    }

                    if (clash)
                    {
                        continue;
                    }

                    placed.Add(new Vector2(x, z));
                    Vector3 p = At(x, z, -0.02f);
                    switch (kind)
                    {
                        case 0: Saguaro(rm, cols, p, R(2.2f, 3.6f), R(0f, 360f)); break;
                        case 1: BarrelCactus(rm, p, R(0.8f, 1.3f)); break;
                        case 2: PricklyPear(rm, p, R(0.8f, 1.2f), R(0f, 360f)); break;
                        case 3: Bush(rm, p, R(0.8f, 1.4f)); break;
                        case 4: Tuft(rm, p, R(0.8f, 1.3f)); break;
                        default: Tumbleweed(rm, p, R(0.9f, 1.3f)); break;
                    }

                    made++;
                }
            }
        }

        /// <summary>Small stones and gravel (merged into the mesh, no colliders), more of them near the foot of the walls.</summary>
        private static void Pebbles(RcMesh rm, RcZone[] zones, int count, float minEdge, float maxEdge, System.Func<float, float, bool> allowed = null, float minSize = 0.10f, float maxSize = 0.34f)
        {
            int made = 0;
            for (int attempt = 0; attempt < count * 30 && made < count; attempt++)
            {
                float x = R(-RcLayout.HalfX + 5f, RcLayout.HalfX - 5f), z = R(-RcLayout.HalfZ + 5f, RcLayout.HalfZ - 5f);
                if (System.Array.IndexOf(zones, RcLayout.ZoneAt(x, z)) < 0)
                {
                    continue;
                }

                float edge = RcLayout.EdgeDistance(x, z);
                if (edge < minEdge || edge > maxEdge || RcLayout.Slope(x, z) > 0.35f || (allowed != null && !allowed(x, z)))
                {
                    continue;
                }

                // a pebble cluster: 1-4 stones close together
                int n = rng.Next(1, 4);
                for (int k = 0; k < n; k++)
                {
                    float px = x + R(-0.5f, 0.5f), pz = z + R(-0.5f, 0.5f);
                    if (!IsFree(px, pz, 0.2f))
                    {
                        continue;
                    }

                    float s = R(minSize, maxSize);
                    rm.Push(At(px, pz, -0.03f), R(0f, 360f));
                    rm.Rock(new Vector3(0f, s * 0.28f, 0f), new Vector3(s, s * 0.6f, s * R(0.7f, 1f)), Seed(), rng.NextDouble() < 0.5 ? P.Pebble : P.RockSand, P.PebbleLight, P.PebbleLight, null, 0, 0.18f, 0.2f);
                    rm.Pop();
                }

                made++;
            }
        }

        /// <summary>A wooden Western sign post with two boards, used by the zones to point the way (no text: simple painted stripes).</summary>
        private static void SignPost(RcMesh rm, Vector3 pos, float yaw, bool arrowLeft)
        {
            rm.Push(pos, yaw);
            rm.Block(Vector3.zero, new Vector3(0.17f, 2.3f, 0.17f), P.WoodDark, P.WoodGrey);
            rm.Box(new Vector3(0f, 1.95f, 0.12f), new Vector3(1.25f, 0.34f, 0.06f), P.WoodLight);
            rm.Box(new Vector3(0f, 1.95f, 0.155f), new Vector3(1.0f, 0.06f, 0.03f), P.ClothRed);
            float dir = arrowLeft ? -1f : 1f;
            rm.Push(new Vector3(dir * 0.76f, 1.95f, 0.09f), 0f, 90f, 0f);
            rm.Poly(Vector3.zero, 0.22f, 0.22f, 0.06f, 3, P.WoodLight, P.WoodLight, arrowLeft ? 180f : 0f);
            rm.Pop();
            rm.Pop();
        }
    }
}
#endif
