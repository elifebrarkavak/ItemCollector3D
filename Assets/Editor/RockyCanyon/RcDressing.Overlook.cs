#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>D  Canyon Overlook (the S-shaped climb and the 11 m plateau) and E  Hidden Ravine (a winding side passage that ends in a quiet pocket with gold veins in its walls).</summary>
    public static partial class RcDressing
    {
        private struct PathPoint
        {
            public Vector2 Position;
            public Vector2 Direction;
        }

        /// <summary>Points every <paramref name="spacing"/> m along a layout shape's centre line.</summary>
        private static List<PathPoint> Along(string shapeName, float spacing, float start = 0f)
        {
            var result = new List<PathPoint>();
            foreach (RcLayout.Shape s in RcLayout.Shapes)
            {
                if (s.Name != shapeName)
                {
                    continue;
                }

                float carried = start;
                for (int i = 0; i < s.Points.Length - 1; i++)
                {
                    Vector2 a = new(s.Points[i].x, s.Points[i].z), b = new(s.Points[i + 1].x, s.Points[i + 1].z);
                    float length = Vector2.Distance(a, b);
                    for (float d = carried; d < length; d += spacing)
                    {
                        result.Add(new PathPoint { Position = Vector2.Lerp(a, b, d / length), Direction = (b - a).normalized });
                        carried = d + spacing - length;
                    }

                    carried = Mathf.Max(0f, carried);
                }
            }

            return result;
        }

        // ================================================================ D  Canyon Overlook

        private static void Overlook(Transform props, Transform cols)
        {
            var rm = new RcMesh();
            Vector2 centre = RcLayout.OverlookCentre;
            Take(centre.x, centre.y, 5.2f);

            // ---- the path: small stones along both edges (never on the middle 4 m), cairns at the bends that point the way up
            List<PathPoint> path = Along("OverlookPath", 3.0f);
            for (int i = 0; i < path.Count; i++)
            {
                PathPoint p = path[i];
                Vector2 side = new(p.Direction.y, -p.Direction.x);
                float sign = i % 2 == 0 ? 1f : -1f;
                float off = R(2.45f, 3.0f);
                Vector2 q = p.Position + side * sign * off;
                if (RcLayout.EdgeDistance(q.x, q.y) > -0.2f || RcLayout.Slope(q.x, q.y) > 0.5f)
                {
                    continue;
                }

                float s = R(0.25f, 0.65f);
                NaturalRock(rm, At(q.x, q.y, -0.04f), s, Seed());
                if (i % 3 == 0)
                {
                    Vector2 q2 = q + p.Direction * R(0.5f, 1.0f) + side * sign * 0.25f;
                    if (RcLayout.EdgeDistance(q2.x, q2.y) < -0.1f)
                    {
                        NaturalRock(rm, At(q2.x, q2.y, -0.03f), s * 0.6f, Seed());
                    }
                }
            }

            foreach (Vector2 bend in new[] { new Vector2(47f, 15f), new Vector2(42f, 26.5f), new Vector2(31.2f, 31.2f), new Vector2(36.5f, 38.6f) })
            {
                Vector2 p = Snap(bend.x, bend.y, -1.6f, 2.5f);
                Cairn(rm, At(p.x, p.y), R(0f, 360f));
                Take(p.x, p.y, 0.8f);
            }

            // ---- layered outcrops against the walls beside the path (they lean out of the rock, no collider: the wall already blocks)
            var picked = new List<Vector2>();
            for (int i = 0; i < path.Count; i += 4)
            {
                PathPoint p = path[i];
                Vector2 side = new(p.Direction.y, -p.Direction.x);
                float sign = (i / 4) % 2 == 0 ? 1f : -1f;
                for (int attempt = 0; attempt < 14; attempt++)
                {
                    float r = R(1.3f, 2.0f), h = R(2.8f, 5.5f);
                    Vector2 q = p.Position + side * sign * (3.2f + R(0.8f, 2.4f));
                    float e = RcLayout.EdgeDistance(q.x, q.y);
                    if (e < r * 0.7f - 0.1f || e > 2.8f || !IsFree(q.x, q.y, 1.2f))
                    {
                        continue;
                    }

                    // the view from the lookout (towards the canyon, 230 degrees +- 55) must stay open
                    Vector2 fromDeck = q - RcLayout.OverlookCentre;
                    if (fromDeck.magnitude < 18f && Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(fromDeck.x, fromDeck.y) * Mathf.Rad2Deg, 230f)) < 55f)
                    {
                        continue;
                    }

                    bool near = false;
                    foreach (Vector2 o in picked)
                    {
                        near |= (o - q).sqrMagnitude < 7f * 7f;
                    }

                    if (near)
                    {
                        continue;
                    }

                    picked.Add(q);
                    Outcrop(rm, At(q.x, q.y), r, h, h > 4f ? 4 : 3, h > 4.6f);
                    Take(q.x, q.y, r);
                    break;
                }
            }

            // ---- the summit: a wooden lookout deck facing the canyon, rails with an opening on the arrival side, bench, telescope, a tall flagpole
            float yaw = YawToward(centre.x, centre.y, 0f, 0f);
            Lookout(rm, cols, centre.x, centre.y, yaw);

            // ---- rock spires round the rim (decor seen from the whole canyon) and a few plants
            foreach (Vector2 s in new[] { new Vector2(49.5f, 41.5f), new Vector2(52f, 34.5f), new Vector2(46.5f, 44f), new Vector2(51f, 28f), new Vector2(40f, 43f) })
            {
                Vector2 p = Snap(s.x, s.y, 4.5f, 3f);
                if (RcLayout.EdgeDistance(p.x, p.y) < 2.5f)
                {
                    continue;
                }

                Spire(rm, At(p.x, p.y), R(1.2f, 1.8f), R(6.5f, 10f), Seed());
            }

            var nature = new RcMesh();
            RcZone[] zone = { RcZone.Overlook };
            Plants(nature, cols, zone, -3.0f, -0.9f, 1, 2, 2, 5, 18, 0, (x, z) => RcLayout.EdgeDistance(x, z) < -0.9f && new Vector2(x - centre.x, z - centre.y).magnitude > 4.6f);
            Pebbles(nature, zone, 40, -4f, -0.5f, (x, z) => new Vector2(x - centre.x, z - centre.y).magnitude > 3.3f);
            Emit(props, "Nature", nature);
            Emit(props, "Structures", rm);
        }

        private static void Cairn(RcMesh rm, Vector3 pos, float yaw)
        {
            rm.Push(pos, yaw);
            rm.Rock(new Vector3(0f, 0.18f, 0f), new Vector3(0.52f, 0.2f, 0.46f), Seed(), P.Pebble, P.RockSand, P.PebbleLight, null, 0, 0.12f, 0.3f);
            rm.Rock(new Vector3(0.03f, 0.50f, 0f), new Vector3(0.38f, 0.17f, 0.34f), Seed(), P.RockSand, P.Pebble, P.PebbleLight, null, 0, 0.12f, 0.3f);
            rm.Rock(new Vector3(-0.02f, 0.76f, 0.02f), new Vector3(0.26f, 0.14f, 0.24f), Seed(), P.Pebble, P.RockCream, P.RockCream, null, 0, 0.12f, 0.3f);
            rm.Pop();
        }

        private static void Lookout(RcMesh rm, Transform cols, float x, float z, float yaw)
        {
            Vector3 pos = At(x, z);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            const float hw = 2.3f, hd = 1.75f, lift = 0.12f;
            rm.Push(pos, yaw);
            rm.Box(new Vector3(0f, lift - 0.09f, 0f), new Vector3(hw * 2f, 0.18f, hd * 2f), P.WoodGrey, P.WoodBleach);
            for (int i = 0; i < 7; i++)
            {
                rm.Box(new Vector3(0f, lift + 0.004f, -hd + 0.25f + i * 0.5f), new Vector3(hw * 2f, 0.012f, 0.05f), P.WoodDark);
            }

            rm.Beam(new Vector3(-hw, 0.05f, -hd), new Vector3(-hw, 0.05f, hd), 0.2f, 0.26f, P.WoodDark);
            rm.Beam(new Vector3(hw, 0.05f, -hd), new Vector3(hw, 0.05f, hd), 0.2f, 0.26f, P.WoodDark);
            // bench against the back, a telescope on a post at the front-left corner
            rm.Box(new Vector3(-0.9f, 0.5f, -hd + 0.4f), new Vector3(1.7f, 0.09f, 0.42f), P.WoodLight);
            rm.Block(new Vector3(-1.5f, lift, -hd + 0.4f), new Vector3(0.1f, 0.4f, 0.36f), P.WoodDark);
            rm.Block(new Vector3(-0.3f, lift, -hd + 0.4f), new Vector3(0.1f, 0.4f, 0.36f), P.WoodDark);
            rm.Block(new Vector3(-1.5f, lift, hd - 0.4f), new Vector3(0.16f, 1.2f, 0.16f), P.Iron);
            rm.Pipe(new Vector3(-1.5f, lift + 1.25f, hd - 0.6f), new Vector3(-1.5f, lift + 1.4f, hd + 0.35f), 0.11f, 7, P.IronLight, P.Iron);
            rm.Pop();

            // rails: post-and-rail on the front, the left and part of the back and right; the opening is on the right (arrival from the path, north-west)
            Vector2 W(float lx, float lz)
            {
                Vector3 q = pos + rot * new Vector3(lx, 0f, lz);
                return new Vector2(q.x, q.z);
            }

            Fence(rm, cols, "LookoutRailFront", new[] { W(-hw, -hd), W(-hw, hd), W(hw, hd), W(hw, 0.4f) }, 1.1f, 1.15f, true, lift, 1.2f);
            Fence(rm, cols, "LookoutRailBack", new[] { W(-hw, -hd), W(0.2f, -hd) }, 1.1f, 1.15f, true, lift, 1.2f);
            Box(cols, "LookoutDeck", pos + rot * new Vector3(0f, lift - 0.12f, 0f), new Vector3(hw * 2f, 0.24f, hd * 2f), yaw);

            // the flagpole behind the deck on the east side, taller than anything else up here
            Vector3 mast = pos + rot * new Vector3(hw + 1.2f, 0f, -hd - 0.4f);
            mast = At(mast.x, mast.z);
            rm.Push(mast, yaw);
            rm.Block(new Vector3(0f, -0.3f, 0f), new Vector3(0.22f, 7.2f, 0.22f), P.WoodDark, P.WoodGrey);
            rm.Block(new Vector3(0f, -0.3f, 0f), new Vector3(0.8f, 0.5f, 0.8f), P.RockDark, P.RockSand);
            rm.Face(P.ClothRed, new Vector3(0f, 6.2f, -0.8f), new Vector3(0f, 6.9f, 0f), new Vector3(0f, 5.8f, 0f), new Vector3(0f, 6.35f, -1.8f));
            rm.Face(P.White, new Vector3(0f, 6.2f, -0.8f), new Vector3(0f, 6.35f, -1.8f), new Vector3(0f, 5.8f, 0f), new Vector3(0f, 5.6f, -1.2f));
            rm.Pop();
            Box(cols, "FlagPole", mast + new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 3.0f, 0.5f), yaw);
        }

        // ================================================================ E  Hidden Ravine

        private static void Ravine(Transform props, Transform cols)
        {
            var rm = new RcMesh();
            Vector3 pocket = At(-17f, 2f);
            Take(-17f, 2f, 3.2f);
            Take(-4f, -12f, 3.0f);

            // ---- the mouth: two layered rock pillars with cap rocks stand at the walls and leave about 6 m between them; a small cairn marks it for those who look
            Vector2 gl = Snap(-8.6f, -10.2f, 0.9f, 2.5f), gr = Snap(0.4f, -10.8f, 0.9f, 2.5f);
            foreach (Vector2 g in new[] { gl, gr })
            {
                Outcrop(rm, At(g.x, g.y), 1.7f, 5.4f, 4, true, 2);
                Ball(cols, "GateRock" + (g == gl ? "L" : "R"), At(g.x, g.y, 1.6f), 1.5f);
                Take(g.x, g.y, 2.4f);
            }

            Vector2 cairn = Snap(-6.4f, -9.0f, -2.6f, 1.5f);
            Cairn(rm, At(cairn.x, cairn.y), 30f);
            Take(cairn.x, cairn.y, 0.9f);

            // ---- big rock protrusions in the walls, alternating sides (colliders: the camera must not fly into them)
            List<PathPoint> path = Along("Ravine", 1f);
            foreach ((float t, float sign) in new[] { (0.30f, 1f), (0.52f, -1f), (0.75f, 1f) })
            {
                PathPoint p = path[Mathf.Clamp(Mathf.RoundToInt(t * (path.Count - 1)), 0, path.Count - 1)];
                Vector2 side = new(p.Direction.y, -p.Direction.x);
                Vector2 q = Snap(p.Position.x + side.x * sign * 4f, p.Position.y + side.y * sign * 4f, -0.1f, 1.5f);
                Outcrop(rm, At(q.x, q.y), 1.5f, 3.6f, 3, true, 1);
                Ball(cols, "Protrusion" + t, At(q.x, q.y, 1.2f), 1.3f);
                Take(q.x, q.y, 2.0f);
            }

            // ---- gold veins on the walls (thin glowing strips lying on the rock), strongest near the pocket
            var veins = new RcMesh { Glow = true };
            Vein(veins, -9.6f, -3.2f, 1f, 7.5f, 0.26f);
            Vein(veins, -12.8f, -3.3f, -1f, 6.5f, 0.2f);
            Vein(veins, -5.8f, -2.0f, -1f, 5.5f, 0.16f);
            Vein(veins, -17.5f, 2.0f, 1f, 4.5f, 0.3f);
            Vein(veins, -20.0f, 5.2f, -1f, 6.0f, 0.24f);
            Vein(veins, -14.5f, 6.0f, 1f, 4.0f, 0.2f);
            Emit(props, "Veins", veins);

            // ---- the pocket: warmer, paler rock and sand stones round it, a stone ring, a lantern left behind, one soft warm light; nothing to collect here yet
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f + 0.2f;
                float x = -17f + Mathf.Cos(a) * R(3.0f, 3.6f), zz = 2f + Mathf.Sin(a) * R(3.0f, 3.6f);
                if (RcLayout.EdgeDistance(x, zz) > -0.4f)
                {
                    continue;
                }

                float s = R(0.35f, 0.6f);
                rm.Push(At(x, zz, -0.04f), R(0f, 360f));
                rm.Rock(new Vector3(0f, s * 0.3f, 0f), new Vector3(s, s * 0.6f, s * 0.9f), Seed(), P.RockSand, P.RockCream, P.RockCream, P.RockOrange, 0, 0.2f, 0.2f);
                rm.Pop();
            }

            Vector2 backWall = Snap(-21.0f, 3.0f, -0.5f, 2f);
            Outcrop(rm, At(backWall.x, backWall.y), 1.5f, 3.2f, 3, false, 1);
            LanternPost(rm, At(-15.2f, 4.4f), 200f, 1.5f);
            var lightGo = new GameObject("PocketGlow");
            lightGo.transform.SetParent(props, false);
            lightGo.transform.position = pocket + new Vector3(0f, 2.3f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.72f, 0.38f);
            light.intensity = 1.6f;
            light.range = 9f;
            light.shadows = LightShadows.None;

            // ---- floor: gravel and stones, dry shrubs; the passage stays open
            var nature = new RcMesh();
            RcZone[] zone = { RcZone.Ravine };
            Plants(nature, cols, zone, -3.6f, -0.9f, 0, 1, 1, 4, 14, 0);
            Pebbles(nature, zone, 110, -6f, -0.4f, null, 0.08f, 0.3f);
            RockClusters(nature, cols, "Ravine", zone, 3, -3.2f, -1.2f, 8f, 0.7f, 1.2f, 99f);
            Emit(props, "Nature", nature);
            Emit(props, "Structures", rm);
        }

        /// <summary>A zig-zag vein of gold from the wall base at (x, z) going up and along the wall; <paramref name="along"/> is the side (+1 / -1) it runs to, and it is a ribbon of the terrain surface.</summary>
        private static void Vein(RcMesh veins, float x, float z, float along, float length, float width)
        {
            Vector2 start = Snap(x, z, 0.8f, 2.5f);
            Vector2 n = IntoWall(start.x, start.y);
            Vector2 t = new Vector2(-n.y, n.x) * along;
            var centres = new List<Vector3>();
            var normals = new List<Vector3>();
            var widths = new List<float>();
            int steps = Mathf.RoundToInt(length / 0.5f);
            float zig = 0f;
            for (int i = 0; i <= steps; i++)
            {
                float f = i / (float)steps;
                zig += R(-0.25f, 0.25f);
                zig = Mathf.Clamp(zig, -0.5f, 0.5f);
                Vector2 p = start + t * (f * length) + n * (f * 1.4f + 0.2f + zig * 0.5f);
                centres.Add(new Vector3(p.x, G(p.x, p.y), p.y));
                normals.Add(Normal(p.x, p.y));
                widths.Add(width * Mathf.Sin(Mathf.PI * Mathf.Clamp(f, 0.08f, 0.92f)) + 0.05f);
            }

            veins.Strip(centres, normals, widths, P.Gold, 0.09f);
            // a short branch
            int b = steps / 2;
            var bc = new List<Vector3>();
            var bn = new List<Vector3>();
            var bw = new List<float>();
            for (int i = 0; i <= 4; i++)
            {
                Vector2 p = new Vector2(centres[b].x, centres[b].z) + t * (i * 0.35f) + n * (i * 0.12f);
                bc.Add(new Vector3(p.x, G(p.x, p.y), p.y));
                bn.Add(Normal(p.x, p.y));
                bw.Add(Mathf.Max(0.04f, width * 0.5f * (1f - i / 4f)));
            }

            veins.Strip(bc, bn, bw, P.GoldBright, 0.1f);
        }

        // ================================================================ backdrop: tall buttes on the rim, a skirt of hills beyond the terrain and far mesas (silhouettes, no colliders)

        private static string DressBackdrop(Transform env)
        {
            Transform backdrop = env.Find("Backdrop");
            if (backdrop == null)
            {
                backdrop = new GameObject("Backdrop").transform;
                backdrop.SetParent(env, false);
            }

            for (int i = backdrop.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(backdrop.GetChild(i).gameObject);
            }

            rng = new System.Random(9090);
            var rim = new RcMesh();
            int placed = 0;
            for (int attempt = 0; attempt < 4000 && placed < 16; attempt++)
            {
                float x = R(-50f, 50f), z = R(-41f, 41f);
                if (RcLayout.EdgeDistance(x, z) < 9f || !FarFromOthers(x, z, 11f))
                {
                    continue;
                }

                BackdropSpots.Add(new Vector2(x, z));
                float r = R(2.4f, 4.4f), h = R(5f, 13f);
                if (rng.NextDouble() < 0.55)
                {
                    Outcrop(rim, At(x, z, -0.5f), r, h, 4, true);
                }
                else
                {
                    Spire(rim, At(x, z, -0.5f), r * 0.55f, h * 1.1f, Seed());
                }

                placed++;
            }

            BackdropSpots.Clear();
            Emit(backdrop, "RimButtes", rim);

            // skirt: the terrain's border ring continues outwards and down as flat-shaded hills so the edge of the world is never visible
            var far = new RcMesh();
            const int segs = 72;
            Vector3 centre = new(0f, 0f, 0f);
            var inner = new Vector3[segs];
            var outer = new Vector3[segs];
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                Vector2 dir = new(Mathf.Cos(a), Mathf.Sin(a));
                float k = Mathf.Min(RcLayout.HalfX / Mathf.Max(0.001f, Mathf.Abs(dir.x)), RcLayout.HalfZ / Mathf.Max(0.001f, Mathf.Abs(dir.y))) - 0.3f;
                Vector2 b = dir * k;
                inner[i] = new Vector3(b.x, G(b.x, b.y), b.y);
                float far1 = k + 55f;
                outer[i] = new Vector3(dir.x * far1, 7.5f + 2.5f * Mathf.PerlinNoise(a * 3f, 2.3f), dir.y * far1);
            }

            for (int i = 0; i < segs; i++)
            {
                int n = (i + 1) % segs;
                far.Face(P.Rim, centre + new Vector3(0f, -40f, 0f), inner[i], inner[n], outer[n], outer[i]);
            }

            // far mesas on a ring 85-150 m out, each a few tiers with a cap rock, standing on a lowered base
            for (int i = 0; i < 15; i++)
            {
                float a = (i / 15f + R(-0.02f, 0.02f)) * Mathf.PI * 2f;
                float d = R(88f, 150f);
                Vector3 p = new(Mathf.Cos(a) * d, -6f, Mathf.Sin(a) * d);
                float r = R(14f, 30f), h = R(24f, 46f);
                P[] colors = i % 3 == 0 ? new[] { P.RockRed, P.RockRed2, P.RockOrange } : i % 3 == 1 ? new[] { P.RockRed2, P.RockOrange, P.RockOrange2 } : new[] { P.RockDeep, P.RockRed, P.RockRed2 };
                far.Butte(p, r, h, 3, Seed(), colors, P.RockOrange2, 8, 0.78f, true);
                if (i % 2 == 0)
                {
                    far.Butte(new Vector3(p.x + R(-18f, 18f), -6f, p.z + R(-18f, 18f)), r * 0.35f, h * 1.1f, 4, Seed(), colors, P.RockSand, 6, 0.55f, true);
                }
            }

            Emit(backdrop, "FarField", far);
            return $"Backdrop: {placed} rim buttes, skirt and 15 far mesas";
        }

        private static readonly List<Vector2> BackdropSpots = new();

        private static bool FarFromOthers(float x, float z, float distance)
        {
            foreach (Vector2 p in BackdropSpots)
            {
                if ((p.x - x) * (p.x - x) + (p.y - z) * (p.y - z) < distance * distance)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
#endif
