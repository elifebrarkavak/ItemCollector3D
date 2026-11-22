#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>B  Dry Riverbed: a wide sandy bed with a search basin in the middle. Banks carry layered outcrops, boulder clusters, sparse cacti and the remains of old mining; the middle stays open.</summary>
    public static partial class RcDressing
    {
        /// <summary>The nearest spot to (ax, az) whose floor edge distance is closest to <paramref name="targetEdge"/> (negative = inside a floor): used to set props against a wall.</summary>
        private static Vector2 Snap(float ax, float az, float targetEdge, float radius = 4f)
        {
            float best = float.MaxValue;
            var p = new Vector2(ax, az);
            for (float dx = -radius; dx <= radius; dx += 0.5f)
            {
                for (float dz = -radius; dz <= radius; dz += 0.5f)
                {
                    float x = ax + dx, z = az + dz;
                    float score = Mathf.Abs(RcLayout.EdgeDistance(x, z) - targetEdge) + 0.04f * Mathf.Sqrt(dx * dx + dz * dz);
                    if (score < best)
                    {
                        best = score;
                        p = new Vector2(x, z);
                    }
                }
            }

            return p;
        }

        /// <summary>The horizontal direction pointing INTO the rock wall (towards larger edge distance) at (x, z).</summary>
        private static Vector2 IntoWall(float x, float z)
        {
            return new Vector2(RcLayout.EdgeDistance(x + 0.6f, z) - RcLayout.EdgeDistance(x - 0.6f, z), RcLayout.EdgeDistance(x, z + 0.6f) - RcLayout.EdgeDistance(x, z - 0.6f)).normalized;
        }

        /// <summary>The yaw (degrees) that makes a prop's local +z face away from the wall behind it (towards the floor).</summary>
        private static float FaceFloorYaw(float x, float z)
        {
            Vector2 w = IntoWall(x, z);
            return Mathf.Atan2(-w.x, -w.y) * Mathf.Rad2Deg;
        }

        private static bool InGorge(float x, float z) => x > -3f && x < 9f && z > -3.5f && z < 27f;

        private static void Riverbed(Transform props, Transform cols)
        {
            var rm = new RcMesh();
            RcZone[] zone = { RcZone.Riverbed };

            // ---- set pieces on the banks
            Vector2 shaft = Snap(17.0f, -4.6f, -2.2f);
            Headframe(rm, cols, shaft.x, shaft.y);
            Vector2 portal = Snap(17.6f, -14.0f, -0.3f, 3f);
            MineEntrance(rm, cols, portal.x, portal.y);
            Vector2 wreck = Snap(-6.5f, -25.8f, -2.0f);
            WagonWreck(rm, cols, wreck.x, wreck.y);
            Vector2 tree = Snap(-18.5f, -12.2f, -1.8f);
            Take(tree.x, tree.y, 1.4f);
            DeadTree(rm, cols, At(tree.x, tree.y, -0.1f), 3.4f, 40f);
            Vector2 tree2 = Snap(35.5f, 5.5f, -1.8f);
            Take(tree2.x, tree2.y, 1.4f);
            DeadTree(rm, cols, At(tree2.x, tree2.y, -0.1f), 2.8f, 200f);

            // driftwood and old timbers half buried in the sand, a few cart wheels
            foreach (Vector2 q in new[] { new Vector2(12.5f, -20.5f), new Vector2(26f, -3.5f), new Vector2(-12f, -21.5f) })
            {
                Vector2 p = Snap(q.x, q.y, -3.6f, 2f);
                if (!IsFree(p.x, p.y, 1.6f))
                {
                    continue;
                }

                Take(p.x, p.y, 1.8f);
                float yaw = R(0f, 360f);
                rm.Push(At(p.x, p.y, 0.12f), yaw, R(-4f, 4f), R(-8f, 8f));
                rm.Pipe(new Vector3(-1.3f, 0f, 0f), new Vector3(1.4f, 0.05f, 0.1f), 0.16f, 6, P.WoodGrey, P.WoodBleach);
                rm.Pipe(new Vector3(-0.2f, 0.1f, 0.4f), new Vector3(0.4f, 0.55f, 1.1f), 0.07f, 5, P.WoodBleach, P.WoodGrey);
                rm.Pop();
                Plank(rm, At(p.x + 1.2f, p.y + 0.9f, 0.08f), 1.4f, R(0f, 180f), R(-6f, 6f), R(-12f, 12f));
            }

            foreach (Vector2 q in new[] { new Vector2(2f, -23.5f), new Vector2(29f, -7.5f) })
            {
                Vector2 p = Snap(q.x, q.y, -3.0f, 2f);
                if (!IsFree(p.x, p.y, 1.2f))
                {
                    continue;
                }

                Take(p.x, p.y, 1.2f);
                rm.Push(At(p.x, p.y, 0.1f), R(0f, 360f), 0f, R(55f, 75f));
                Wheel(rm, new Vector3(0f, 0.2f, 0f), Vector3.right, 0.55f, 0.1f, P.WoodDark, P.Wood);
                rm.Pop();
            }

            // ---- layered outcrops standing against the banks (with a simple collider each), boulder clusters, sparse plants, gravel
            BankOutcrops(rm, cols, zone, 9, 8.5f, 1.2f, 2.0f, 2.2f, 4.6f, x => !InGorge(x, 0f));
            var nature = new RcMesh();
            RockClusters(nature, cols, "Riverbed", zone, 13, -4.2f, -0.8f, 7.0f, 0.9f, 2.0f, 1.2f, (x, z) => !(z > -3.5f && x > -3f && x < 9f && z < 27f));
            Plants(nature, cols, zone, -4.4f, -0.8f, 4, 5, 4, 12, 55, 3, (x, z) => !(z > -3.5f && x > -3f && x < 9f && z < 27f));
            Pebbles(nature, zone, 70, -12f, -3.0f, (x, z) => !(z > -3.5f && x > -3f && x < 9f && z < 27f));   // gravel in the middle: small, flat, rarely in the way
            Pebbles(nature, zone, 70, -3.2f, -0.2f, (x, z) => !(z > -3.5f && x > -3f && x < 9f && z < 27f), 0.12f, 0.42f);
            Gorge(nature, cols);
            Emit(props, "Nature", nature);
            Emit(props, "Structures", rm);
        }

        /// <summary>Layered rock outcrops at the foot of the walls of a zone: each is a stack of 2-4 tiers with a simple sphere collider.</summary>
        private static void BankOutcrops(RcMesh rm, Transform cols, RcZone[] zones, int count, float spacing, float rMin, float rMax, float hMin, float hMax, System.Func<float, bool> unused = null)
        {
            var centres = new List<Vector2>();
            int made = 0;
            for (int attempt = 0; attempt < 8000 && made < count; attempt++)
            {
                float x = R(-RcLayout.HalfX + 10f, RcLayout.HalfX - 10f), z = R(-RcLayout.HalfZ + 10f, RcLayout.HalfZ - 10f);
                if (System.Array.IndexOf(zones, RcLayout.NearestZone(x, z)) < 0 || InGorge(x, z))
                {
                    continue;
                }

                float edge = RcLayout.EdgeDistance(x, z);
                if (edge < -0.1f || edge > 0.7f || !IsFree(x, z, 2.2f))
                {
                    continue;
                }

                bool clash = false;
                foreach (Vector2 c in centres)
                {
                    clash |= (c - new Vector2(x, z)).sqrMagnitude < spacing * spacing;
                }

                if (clash)
                {
                    continue;
                }

                centres.Add(new Vector2(x, z));
                float r = R(rMin, rMax), h = R(hMin, hMax);
                Vector3 p = At(x, z);
                Outcrop(rm, p, r, h, h > 3.4f ? 4 : 3, h > 4f);
                Ball(cols, "Outcrop" + made, p + new Vector3(0f, h * 0.35f, 0f), r * 0.82f);
                Take(x, z, r + 0.9f);
                made++;
            }
        }

        /// <summary>The Basin's old timber headframe (windlass) over a boarded shaft: an A-frame, a pulley, a rope with a bucket.</summary>
        private static void Headframe(RcMesh rm, Transform cols, float x, float z)
        {
            Vector3 pos = At(x, z);
            float yaw = FaceFloorYaw(x, z) + 90f;
            Take(x, z, 3.4f);
            rm.Push(pos, yaw);
            // boarded shaft collar
            rm.Block(new Vector3(0f, -0.1f, 0f), new Vector3(2.5f, 0.45f, 2.5f), P.WoodGrey, P.WoodBleach);
            for (int i = 0; i < 5; i++)
            {
                rm.Box(new Vector3(-1.0f + i * 0.5f, 0.38f, 0f), new Vector3(0.42f, 0.06f, 2.5f), i % 2 == 0 ? P.WoodDark : P.Wood);
            }

            rm.Block(new Vector3(0f, 0.34f, 0f), new Vector3(0.9f, 0.1f, 0.9f), P.RockShade);
            // A-frame
            foreach (float s in new[] { -1f, 1f })
            {
                rm.Beam(new Vector3(0f, 0.2f, s * 1.45f), new Vector3(0f, 4.4f, s * 0.2f), 0.26f, 0.26f, P.WoodDark);
                rm.Beam(new Vector3(0f, 0.2f, s * 1.9f), new Vector3(0f, 2.6f, s * 0.95f), 0.14f, 0.14f, P.Wood);
            }

            rm.Beam(new Vector3(0f, 1.6f, -0.95f), new Vector3(0f, 1.6f, 0.95f), 0.18f, 0.18f, P.Wood);
            rm.Beam(new Vector3(-0.1f, 4.4f, 0f), new Vector3(0.1f, 4.4f, 0f), 0.4f, 0.4f, P.WoodDark);
            rm.Pipe(new Vector3(-0.2f, 4.15f, 0f), new Vector3(0.2f, 4.15f, 0f), 0.42f, 10, P.Iron, P.IronLight);
            rm.Beam(new Vector3(0f, 4.1f, -0.38f), new Vector3(0f, 0.55f, 0.1f), 0.04f, 0.04f, P.WoodGrey);
            rm.Block(new Vector3(0.1f, 0.4f, 0.02f), new Vector3(0.4f, 0.4f, 0.4f), P.Rust);
            rm.Pop();
            Box(cols, "HeadframeCollar", pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.2f, 0f), new Vector3(2.6f, 0.9f, 2.6f), yaw);
            Box(cols, "HeadframeLegs", pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 2.2f, 0f), new Vector3(0.7f, 4.4f, 3.2f), yaw);
            for (int i = 0; i < 3; i++)
            {
                NaturalRock(rm, At(x + R(-2.2f, 2.2f), z + R(-2.2f, 2.2f), -0.05f), R(0.3f, 0.5f), Seed());
            }
        }

        /// <summary>A boarded-up timber mine entrance set into the basin's east wall (the wall is steep, so the frame leans back with it).</summary>
        private static void MineEntrance(RcMesh rm, Transform cols, float x, float z)
        {
            Vector3 pos = At(x, z, -0.1f);
            float yaw = FaceFloorYaw(x, z);
            Take(x, z, 3.6f);
            rm.Push(pos, yaw, -22f);
            rm.Box(new Vector3(0f, 1.2f, -0.35f), new Vector3(2.5f, 2.5f, 0.5f), P.RockShade);                       // the dark opening
            foreach (float s in new[] { -1f, 1f })
            {
                rm.Block(new Vector3(s * 1.45f, -0.2f, 0.12f), new Vector3(0.4f, 3.0f, 0.4f), P.WoodDark, P.WoodGrey);
                rm.Beam(new Vector3(s * 1.45f, 2.55f, 0.1f), new Vector3(s * 0.7f, 3.05f, 0.1f), 0.2f, 0.22f, P.WoodDark);
            }

            rm.Beam(new Vector3(-1.9f, 2.95f, 0.1f), new Vector3(1.9f, 2.95f, 0.1f), 0.42f, 0.4f, P.WoodDark);
            // boards across the opening, one fallen
            rm.Beam(new Vector3(-1.2f, 0.4f, 0.2f), new Vector3(1.2f, 1.1f, 0.2f), 0.05f, 0.24f, P.WoodLight);
            rm.Beam(new Vector3(-1.2f, 1.5f, 0.22f), new Vector3(1.2f, 0.7f, 0.22f), 0.05f, 0.24f, P.Wood);
            rm.Beam(new Vector3(-1.1f, 2.0f, 0.2f), new Vector3(1.1f, 2.0f, 0.2f), 0.05f, 0.24f, P.WoodGrey);
            rm.Pop();
            DangerSign(rm, At(x + Mathf.Sin(yaw * Mathf.Deg2Rad) * 2.3f + 1.2f * Mathf.Cos(yaw * Mathf.Deg2Rad), z + Mathf.Cos(yaw * Mathf.Deg2Rad) * 2.3f - 1.2f * Mathf.Sin(yaw * Mathf.Deg2Rad)), yaw + 10f);
            // fallen rocks in front
            for (int i = 0; i < 5; i++)
            {
                float a = (yaw + R(-60f, 60f)) * Mathf.Deg2Rad, d = R(1.4f, 2.8f);
                NaturalRock(rm, At(x + Mathf.Sin(a) * d, z + Mathf.Cos(a) * d, -0.05f), R(0.35f, 0.8f), Seed());
            }

            Box(cols, "MineEntrance", pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 1.3f, -0.6f), new Vector3(3.6f, 2.8f, 1.2f), yaw);
        }

        /// <summary>A mine wagon on its side, half buried: a wheel off, boards, scattered ore.</summary>
        private static void WagonWreck(RcMesh rm, Transform cols, float x, float z)
        {
            Take(x, z, 3.6f);
            Wagon(rm, cols, "WagonWreck", At(x, z, -0.28f), R(0f, 360f), 38f, false, P.RustDark);
            Wheel(rm, At(x + 2.4f, z + 1.2f, 0.2f), new Vector3(0.2f, 1f, 0.1f).normalized, 0.3f, 0.1f, P.Iron, P.IronLight);
            for (int i = 0; i < 4; i++)
            {
                Plank(rm, At(x + R(-2.5f, 2.5f), z + R(-2.5f, 2.5f), 0.06f), R(0.8f, 1.5f), R(0f, 180f), R(-8f, 8f), R(-10f, 10f), i % 2 == 0 ? P.WoodGrey : P.Wood);
            }

            for (int i = 0; i < 4; i++)
            {
                NaturalRock(rm, At(x + R(-2.2f, 2.2f), z + R(-2.2f, 2.2f), -0.03f), R(0.2f, 0.4f), Seed());
            }
        }

        /// <summary>The dry gorge under the bridge: a sandy floor with a few fallen rocks along its walls (the middle three metres stay clear), driftwood and gravel.</summary>
        private static void Gorge(RcMesh rm, Transform cols)
        {
            Take(3f, 18f, 0f);
            for (int i = 0; i < 10; i++)
            {
                float z = R(-1f, 25f);
                float x = 3f + (rng.NextDouble() < 0.5 ? -1f : 1f) * R(2.0f, 2.9f);
                x += (z - 17.5f) * 0.0f;
                if (RcLayout.ZoneAt(x, z) != RcZone.Riverbed || RcLayout.Slope(x, z) > 0.3f || !IsFree(x, z, 1.0f))
                {
                    continue;
                }

                Take(x, z, 1.0f);
                NaturalRock(rm, At(x, z, -0.05f), R(0.4f, 1.0f), Seed());
            }

            for (int i = 0; i < 24; i++)
            {
                float z = R(-1f, 25f), x = 3f + R(-2.9f, 2.9f);
                if (RcLayout.ZoneAt(x, z) != RcZone.Riverbed || RcLayout.Slope(x, z) > 0.3f)
                {
                    continue;
                }

                if (rng.NextDouble() < 0.6)
                {
                    Tuft(rm, At(x, z, -0.02f), R(0.7f, 1.1f));
                }
                else
                {
                    rm.Push(At(x, z, -0.03f), R(0f, 360f));
                    rm.Rock(new Vector3(0f, 0.04f, 0f), new Vector3(R(0.12f, 0.3f), 0.06f, R(0.1f, 0.25f)), Seed(), P.Pebble, P.PebbleLight, P.PebbleLight, null, 0, 0.18f, 0.2f);
                    rm.Pop();
                }
            }

            // driftwood trapped by the narrowest part
            rm.Push(At(2.4f, 6.0f, 0.12f), 70f, 0f, 6f);
            rm.Pipe(new Vector3(-1.2f, 0f, 0f), new Vector3(1.3f, 0.04f, 0.05f), 0.13f, 6, P.WoodGrey, P.WoodBleach);
            rm.Pipe(new Vector3(-0.3f, 0.1f, 0.2f), new Vector3(0.5f, 0.5f, 0.7f), 0.06f, 5, P.WoodBleach, P.WoodGrey);
            rm.Pop();
        }
    }
}
#endif
