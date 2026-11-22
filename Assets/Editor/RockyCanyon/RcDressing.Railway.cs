#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>
    /// C  Old Railway: the raised corridor with its abandoned track, three mine wagons, a loading platform with a ramp, a timber jib crane, a signal and THE BRIDGE: a big timber trestle bridge
    /// over the gorge (deck 5 m up, 13 m long, side rails, trestle bents, two tall portal towers that make it a landmark). The deck, the low side rails and the fences at the
    /// cliff edges are plain box colliders; the trestle posts and the tall towers have none (they stand out of the lanes; a tall collider beside a walkway squeezes the orbiting camera); the gorge floor stays open.
    /// </summary>
    public static partial class RcDressing
    {
        // the bridge frame: its middle, its direction (from the west end of the corridor to its east start) and the deck level
        private static readonly Vector3 BridgeCentre = new(3f, 5f, 18f);
        private static readonly float BridgeYaw = Mathf.Atan2(18f, 2f) * Mathf.Rad2Deg;
        private const float DeckTop = 0.08f;
        private const float TowerHeight = 8.4f;

        private static Vector3 BridgeWorld(float lx, float y, float lz) => BridgeCentre + Quaternion.Euler(0f, BridgeYaw, 0f) * new Vector3(lx, y, lz);

        private static float TrackY(float x, float z) => x > -3.8f && x < 9.8f ? 5.02f : G(x, z);

        /// <summary>The track's centre line: from the camp, up the west corridor, across the bridge, along the east corridor to the plaza.</summary>
        private static List<Vector2> TrackLine()
        {
            var pts = new List<Vector2> { new(-32f, -13.5f) };
            foreach (RcLayout.Shape s in RcLayout.Shapes)
            {
                if (s.Name == "RailwayWest" || s.Name == "RailwayEast")
                {
                    foreach (Vector3 p in s.Points)
                    {
                        pts.Add(new Vector2(p.x, p.z));
                    }
                }
            }

            return pts;
        }

        private static void Railway(Transform props, Transform cols)
        {
            var rm = new RcMesh();
            Take(3f, 18f, 11f);   // the bridge area: nothing scattered
            Take(-32f, -12f, 2f);

            Track(rm);
            Bridge(rm, cols);
            CliffFences(rm, cols);

            // ---- wagons: one standing loaded on the west track, one tipped over beside it, one loaded in front of the platform
            Wagon(rm, cols, "WagonA", At(-13.0f, 16.4f, 0.33f), 85f, 0f, true);
            Take(-13f, 16.4f, 2.4f);
            Vector3 tip = At(-23.6f, 15.3f, 0.1f);
            Wagon(rm, cols, "WagonB", tip, 70f, 24f, false, P.RustDark);
            Take(tip.x, tip.z, 2.6f);
            for (int i = 0; i < 7; i++)
            {
                rm.Push(At(-23.0f + R(-2.8f, 1.2f), 15.8f + R(-1.8f, 1.8f), -0.02f), R(0f, 360f));
                float s = R(0.18f, 0.4f);
                rm.Rock(new Vector3(0f, s * 0.3f, 0f), new Vector3(s, s * 0.7f, s), Seed(), P.RockDark, P.Coal, i == 3 ? P.Gold : P.RockDeep, null, 0, 0.25f, 0.2f);
                rm.Pop();
            }

            Wagon(rm, cols, "WagonC", At(19.0f, 19.55f, 0.33f), 85f, 0f, true);
            Take(19f, 19.55f, 2.4f);

            LoadingPlatform(rm, cols);
            JibCrane(rm, cols);
            Signal(rm, cols, 13.8f, 15.4f);

            // ---- fallen rocks against the corridor walls, a few plants, ballast and gravel (the track and the bridge stay clear)
            var nature = new RcMesh();
            RcZone[] zone = { RcZone.Railway };
            System.Func<float, float, bool> offBridge = (x, z) => !(x > -9f && x < 15f && z > 10f);
            RockClusters(nature, cols, "Railway", zone, 8, -2.3f, -0.7f, 7.5f, 0.9f, 1.6f, 1.2f, offBridge);
            Plants(nature, cols, zone, -3.6f, -0.7f, 2, 2, 2, 7, 36, 2, offBridge);
            Pebbles(nature, zone, 70, -4.4f, -0.5f, offBridge, 0.10f, 0.28f);
            Emit(props, "Nature", nature);
            Emit(props, "Structures", rm);
        }

        // ------------------------------------------------------------------ the track

        private static void Track(RcMesh rm)
        {
            List<Vector2> pts = TrackLine();
            var samples = new List<Vector2>();
            float carried = 0f;
            const float spacing = 1.35f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float length = Vector2.Distance(pts[i], pts[i + 1]);
                bool deck = pts[i].x > -7f && pts[i + 1].x < 13f && pts[i].y > 16.5f && pts[i + 1].y > 16.5f && Mathf.Abs(pts[i + 1].x - pts[i].x) > 15f;
                float step = deck ? 0.62f : spacing;
                for (float d = deck ? 0f : step - carried; d < length; d += step)
                {
                    samples.Add(Vector2.Lerp(pts[i], pts[i + 1], d / length));
                    carried = length - d;
                }

                carried = deck ? 0f : carried;
            }

            samples.Add(pts[pts.Count - 1]);
            int missingRail = 0;
            Vector3[] left = new Vector3[samples.Count], right = new Vector3[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                Vector2 prev = samples[Mathf.Max(0, i - 1)], next = samples[Mathf.Min(samples.Count - 1, i + 1)];
                Vector2 dir = (next - prev).normalized;
                Vector2 side = new(dir.y, -dir.x);
                float y = TrackY(samples[i].x, samples[i].y);
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                float ahead = TrackY(next.x, next.y) - TrackY(prev.x, prev.y);
                float pitch = -Mathf.Atan2(ahead, Vector2.Distance(prev, next)) * Mathf.Rad2Deg;
                Vector3 c = new(samples[i].x, y + 0.06f, samples[i].y);
                left[i] = c + new Vector3(-side.x * 0.72f, 0.21f, -side.y * 0.72f);
                right[i] = c + new Vector3(side.x * 0.72f, 0.21f, side.y * 0.72f);
                bool onDeck = samples[i].x > -3.8f && samples[i].x < 9.8f;
                if (!onDeck && rng.NextDouble() < 0.07)
                {
                    continue; // a missing tie
                }

                rm.Push(c, yaw + (onDeck ? 0f : R(-3f, 3f)), pitch, onDeck ? 0f : R(-3f, 3f));
                float roll = (float)rng.NextDouble();
                rm.Box(new Vector3(0f, 0.05f, 0f), new Vector3(2.35f, 0.13f, 0.26f), roll < 0.55f ? P.Wood : roll < 0.85f ? P.WoodDark : P.WoodGrey, P.WoodLight);
                rm.Pop();
            }

            for (int i = 0; i < samples.Count - 1; i++)
            {
                bool onDeck = samples[i].x > -3.8f && samples[i + 1].x < 9.8f;
                bool gap = !onDeck && (i == 7 || i == 25 || i == 41);   // a rail section missing: the sections lie bent in the sand next to the track
                P color = (i % 11 == 4 && !onDeck) ? P.Rust : P.Iron;
                if (gap)
                {
                    missingRail++;
                    Vector3 a = left[i], b = left[i + 1];
                    rm.Beam(a + new Vector3(-0.9f, -0.12f, 0.2f), b + new Vector3(-0.95f, -0.14f, 0.5f), 0.11f, 0.14f, P.Rust);
                    rm.Beam(right[i], right[i + 1], 0.11f, 0.14f, color);
                    continue;
                }

                rm.Beam(left[i], left[i + 1], 0.11f, 0.14f, color);
                rm.Beam(right[i], right[i + 1], 0.11f, 0.14f, color);
            }

            // the end of the line at the camp: a buffer block
            rm.Push(At(-32f, -14.4f, 0f), 0f);
            rm.Block(new Vector3(-0.72f, 0f, 0f), new Vector3(0.3f, 0.5f, 0.3f), P.RustDark);
            rm.Block(new Vector3(0.72f, 0f, 0f), new Vector3(0.3f, 0.5f, 0.3f), P.RustDark);
            rm.Beam(new Vector3(-0.8f, 0.4f, 0f), new Vector3(0.8f, 0.4f, 0f), 0.2f, 0.22f, P.WoodDark);
            rm.Pop();
            _ = missingRail;
        }

        // ------------------------------------------------------------------ THE BRIDGE

        private static void Bridge(RcMesh rm, Transform cols)
        {
            Quaternion rot = Quaternion.Euler(0f, BridgeYaw, 0f);
            rm.Push(BridgeCentre, BridgeYaw);
            const float len = 13.2f, half = len / 2f, width = 3.6f;

            // deck: a thick slab of weathered planks with darker boards along it, and the stringers beneath
            rm.Box(new Vector3(0f, DeckTop - 0.19f, 0f), new Vector3(width, 0.38f, len), P.WoodGrey, P.WoodBleach);
            for (int i = 0; i < 6; i++)
            {
                rm.Box(new Vector3(-1.5f + i * 0.6f, DeckTop + 0.004f, 0f), new Vector3(0.05f, 0.012f, len), P.WoodDark);
            }

            foreach (float lx in new[] { -1.45f, -0.5f, 0.5f, 1.45f })
            {
                rm.Box(new Vector3(lx, -0.75f, 0f), new Vector3(0.36f, 0.5f, len - 0.4f), P.WoodDark);
            }

            // side lattice rails (the "korkuluk"): posts every 1.65 m, a top rail, a mid rail and one diagonal per bay
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (width / 2f - 0.05f);
                int bays = 8;
                for (int i = 0; i <= bays; i++)
                {
                    float z = -half + 0.2f + i * (len - 0.4f) / bays;
                    rm.Block(new Vector3(x, -0.3f, z), new Vector3(0.2f, 1.6f, 0.2f), P.WoodDark, P.WoodLight);
                    if (i < bays)
                    {
                        float z2 = -half + 0.2f + (i + 1) * (len - 0.4f) / bays;
                        if (i % 2 == 0)
                        {
                            rm.Beam(new Vector3(x, 0.18f, z), new Vector3(x, 1.12f, z2), 0.08f, 0.1f, P.Wood);
                        }
                        else
                        {
                            rm.Beam(new Vector3(x, 1.12f, z), new Vector3(x, 0.18f, z2), 0.08f, 0.1f, P.Wood);
                        }
                    }
                }

                rm.Beam(new Vector3(x, 1.27f, -half + 0.2f), new Vector3(x, 1.27f, half - 0.2f), 0.2f, 0.16f, P.WoodLight);
                rm.Beam(new Vector3(x, 0.62f, -half + 0.2f), new Vector3(x, 0.62f, half - 0.2f), 0.12f, 0.12f, P.WoodDark);
            }

            // trestle bents: two, each with two thick posts, a cap beam, in-plane X bracing and knee braces up high (the passage below, 3 m above the floor, is open)
            foreach (float bz in new[] { -2.3f, 2.3f })
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 world = BridgeWorld(s * 1.4f, 0f, bz);
                    float floor = G(world.x, world.z) - 5f;
                    rm.Beam(new Vector3(s * 1.52f, floor - 0.4f, bz), new Vector3(s * 1.4f, -0.45f, bz), 0.52f, 0.52f, P.WoodDark);
                    rm.Block(new Vector3(s * 1.52f, floor - 0.55f, bz), new Vector3(1.0f, 0.55f, 1.0f), P.RockDark, P.RockSand);
                    rm.Beam(new Vector3(s * 1.4f, -0.15f, bz - 0.28f), new Vector3(s * 1.4f, -0.15f, bz + 0.28f), 0.3f, 0.3f, P.Iron); // an iron strap
                }

                float f0 = G(BridgeWorld(0f, 0f, bz).x, BridgeWorld(0f, 0f, bz).z) - 5f;
                rm.Beam(new Vector3(-2.0f, -0.55f, bz), new Vector3(2.0f, -0.55f, bz), 0.52f, 0.42f, P.WoodDark);
                rm.Beam(new Vector3(-1.4f, f0 + 0.8f, bz), new Vector3(1.4f, -0.9f, bz), 0.22f, 0.22f, P.Wood);
                rm.Beam(new Vector3(1.4f, f0 + 0.8f, bz), new Vector3(-1.4f, -0.9f, bz), 0.22f, 0.22f, P.Wood);
                rm.Beam(new Vector3(-1.45f, f0 + 0.8f, bz), new Vector3(1.45f, f0 + 0.8f, bz), 0.26f, 0.24f, P.WoodDark);
                foreach (float s in new[] { -1f, 1f })
                {
                    rm.Beam(new Vector3(s * 1.4f, -1.6f, bz), new Vector3(s * 2.0f, -0.6f, bz), 0.2f, 0.2f, P.Wood);
                }
            }

            foreach (float s in new[] { -1f, 1f })
            {
                rm.Beam(new Vector3(s * 1.4f, -0.95f, -2.3f), new Vector3(s * 1.4f, -0.95f, 2.3f), 0.28f, 0.3f, P.WoodDark);
            }

            // sills under the deck ends, resting on the abutments
            foreach (float z in new[] { -5.3f, 5.3f })
            {
                rm.Beam(new Vector3(-1.6f, -0.55f, z), new Vector3(1.6f, -0.55f, z), 0.5f, 0.42f, P.WoodDark);
            }

            // the rails run on across the deck (drawn by Track); portal towers at both ends
            foreach (float z in new[] { -6.2f, 6.2f })
            {
                PortalTower(rm, z, z < 0f);
            }

            rm.Pop();

            // ---- colliders: the deck and the two (low) side rails
            Box(cols, "BridgeDeck", BridgeWorld(0f, DeckTop - 0.2f, 0f), new Vector3(width, 0.4f, len), BridgeYaw);
            Box(cols, "BridgeRailL", BridgeWorld(-(width / 2f - 0.05f), 0.65f, 0f), new Vector3(0.25f, 1.3f, len), BridgeYaw);
            Box(cols, "BridgeRailR", BridgeWorld(width / 2f - 0.05f, 0.65f, 0f), new Vector3(0.25f, 1.3f, len), BridgeYaw);
            // (the trestle posts and the portal towers are VISUAL ONLY: tall thin colliders next to a walkway would squeeze the orbiting camera; the miner never reaches them: the rails and fences keep the deck lanes)

            // a few loose boards and ropes hanging at the abutments, planks lying near the ends
            rm.Push(BridgeCentre, BridgeYaw);
            Plank(rm, BridgeWorld(1.9f, 0.1f, -4.9f) - BridgeCentre, 1.3f, 12f, 0f, 0f, P.WoodBleach);
            rm.Pop();
        }

        /// <summary>A tall timber gateway over the track (two posts, a top beam, knee braces, a cross brace) with a hanging sign board, a lantern and a pennant: the bridge's landmark.</summary>
        private static void PortalTower(RcMesh rm, float z, bool west)
        {
            const float h = TowerHeight;
            foreach (float s in new[] { -1f, 1f })
            {
                rm.Beam(new Vector3(s * 2.4f, -0.4f, z), new Vector3(s * 2.3f, h, z), 0.6f, 0.6f, P.WoodDark);
                rm.Beam(new Vector3(s * 2.4f, 2.3f, z), new Vector3(s * 2.3f, 2.5f, z), 0.7f, 0.2f, P.Iron);
                rm.Beam(new Vector3(s * 2.3f, 5.2f, z), new Vector3(s * 2.3f, 5.4f, z), 0.7f, 0.2f, P.Iron);
                rm.Beam(new Vector3(s * 2.3f, h - 1.4f, z), new Vector3(s * 1.2f, h - 0.05f, z), 0.3f, 0.3f, P.Wood);
                rm.Block(new Vector3(s * 2.4f, -0.8f, z), new Vector3(1.0f, 0.55f, 1.0f), P.RockDark, P.RockSand);
            }

            rm.Beam(new Vector3(-3.1f, h, z), new Vector3(3.1f, h, z), 0.55f, 0.55f, P.WoodDark);
            rm.Beam(new Vector3(-2.3f, 5.0f, z), new Vector3(2.3f, 5.0f, z), 0.32f, 0.36f, P.Wood);
            rm.Beam(new Vector3(-2.2f, 5.0f, z), new Vector3(2.2f, h - 0.3f, z), 0.2f, 0.2f, P.WoodLight);
            rm.Beam(new Vector3(2.2f, 5.0f, z), new Vector3(-2.2f, h - 0.3f, z), 0.2f, 0.2f, P.WoodLight);
            // the sign board hangs below the top beam, facing outwards; the lantern hangs inside
            float face = west ? -1f : 1f;
            foreach (float cx in new[] { -1.3f, 1.3f })
            {
                rm.Beam(new Vector3(cx, h - 0.3f, z), new Vector3(cx, h - 1.0f, z), 0.05f, 0.05f, P.Iron);
            }

            rm.Push(new Vector3(0f, 0f, z), west ? 180f : 0f);
            Board(rm, new Vector3(0f, h - 1.5f, 0.04f), 0f, 3.0f, 0.95f, P.WoodLight, P.ClothRed);
            Emblem(rm, new Vector3(0f, h - 1.5f, 0.06f), 0f, 0.6f);
            rm.Box(new Vector3(-1.05f, h - 1.5f, 0.09f), new Vector3(0.55f, 0.13f, 0.03f), P.White);
            rm.Box(new Vector3(1.05f, h - 1.5f, 0.09f), new Vector3(0.55f, 0.13f, 0.03f), P.White);
            rm.Pop();
            _ = face;
            rm.Beam(new Vector3(0f, h - 0.3f, z + face * -0.0f), new Vector3(0f, h - 0.8f, z), 0.04f, 0.04f, P.Iron);
            rm.Glow = true;
            rm.Box(new Vector3(0f, h - 1.05f, z - (west ? -0.9f : 0.9f)), new Vector3(0.22f, 0.3f, 0.22f), P.Glass);
            rm.Glow = false;
            rm.Beam(new Vector3(0f, h - 0.3f, z), new Vector3(0f, h - 0.9f, z - (west ? -0.9f : 0.9f)), 0.04f, 0.04f, P.Iron);
            // a pennant on the top beam
            rm.Beam(new Vector3(2.8f, h + 0.1f, z), new Vector3(2.8f, h + 1.6f, z), 0.07f, 0.07f, P.WoodDark);
            rm.Face(P.ClothRed, new Vector3(2.8f, h + 1.0f, z - 0.5f), new Vector3(2.8f, h + 1.55f, z), new Vector3(2.8f, h + 0.85f, z), new Vector3(2.8f, h + 1.1f, z + (west ? -0.9f : 0.9f)));
        }

        /// <summary>A post-and-rail fence round the round cliff edges at both ends of the deck, so no one steps into the gap beside the bridge (box colliders, 1.7 m high).</summary>
        private static void CliffFences(RcMesh rm, Transform cols)
        {
            // west abutment: a circle of radius 4.0 round (-6, 17), east abutment round (12, 19); from beside the deck rail (29 degrees) out to the corridor's wall (90)
            foreach (bool west in new[] { true, false })
            {
                foreach (float sign in new[] { 1f, -1f })
                {
                    var pts = new List<Vector2>();
                    Vector2 c = west ? new Vector2(-6f, 17f) : new Vector2(12f, 19f);
                    for (float deg = 29f; deg <= 100f; deg += 17.75f)
                    {
                        float a = deg * Mathf.Deg2Rad;
                        float x = west ? c.x + Mathf.Cos(a) * 4.0f : c.x - Mathf.Cos(a) * 4.0f;
                        float z = c.y + sign * Mathf.Sin(a) * 4.0f;
                        pts.Add(new Vector2(x, z));
                    }

                    Fence(rm, cols, (west ? "FenceW" : "FenceE") + (sign > 0 ? "N" : "S"), pts, 1.15f, 1.5f, true);
                }
            }

            DangerSign(rm, At(-1.4f, 20.6f), YawToward(-1.4f, 20.6f, -8f, 24f) + 60f);
            DangerSign(rm, At(7.0f, 14.9f), YawToward(7f, 14.9f, 12f, 12f) - 100f);
        }

        // ------------------------------------------------------------------ loading platform, crane, signal

        private static void LoadingPlatform(RcMesh rm, Transform cols)
        {
            const float x0 = 15.2f, x1 = 22.4f, z = 22.45f, h = 0.9f;
            Take((x0 + x1) / 2f, z, 4.4f);
            float cx = (x0 + x1) / 2f, len = x1 - x0;
            float ground = G(cx, z);
            // posts, deck boards and edge beams
            for (int i = 0; i < 5; i++)
            {
                float px = x0 + 0.3f + i * (len - 0.6f) / 4f;
                foreach (float pz in new[] { z - 1.3f, z + 1.3f })
                {
                    rm.Block(new Vector3(px, ground - 0.2f, pz), new Vector3(0.28f, h + 0.2f, 0.28f), P.WoodDark);
                }
            }

            rm.Box(new Vector3(cx, ground + h - 0.09f, z), new Vector3(len, 0.18f, 3.0f), P.WoodGrey, P.WoodBleach);
            for (int i = 0; i < 6; i++)
            {
                rm.Box(new Vector3(cx, ground + h + 0.004f, z - 1.25f + i * 0.5f), new Vector3(len, 0.012f, 0.05f), P.WoodDark);
            }

            foreach (float pz in new[] { z - 1.5f, z + 1.5f })
            {
                rm.Beam(new Vector3(x0, ground + h - 0.25f, pz), new Vector3(x1, ground + h - 0.25f, pz), 0.14f, 0.3f, P.WoodDark);
            }

            // ramp up from the west end (decor and collider are the same slab)
            Vector3 a = At(x0 - 2.5f, z - 0.85f, 0.05f), b = At(x0 - 2.5f, z + 0.85f, 0.05f);
            Slab(rm, a, b, new Vector3(x0, ground + h, z + 0.85f), new Vector3(x0, ground + h, z - 0.85f), 0.12f, P.WoodGrey, P.WoodBleach);
            float rise = h - 0.05f, run = 2.5f;
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            Box(cols, "PlatformRamp", new Vector3(x0 - run / 2f, ground + 0.05f + rise / 2f - 0.06f, z), new Vector3(1.7f, 0.14f, Mathf.Sqrt(run * run + rise * rise) + 0.1f), 90f, -angle, 0f);
            Box(cols, "PlatformDeck", new Vector3(cx, ground + h - 0.15f, z), new Vector3(len, 0.3f, 3.0f), 0f);
            // goods on the deck: crates, barrels, sacks; an ore bin at the east end
            Crate(rm, new Vector3(x0 + 1.4f, ground + h, z - 0.8f), 1.0f, 8f);
            Crate(rm, new Vector3(x0 + 2.5f, ground + h, z - 0.9f), 0.9f, -6f);
            Crate(rm, new Vector3(x0 + 1.9f, ground + h + 1.0f, z - 0.82f), 0.8f, 20f);
            Crate(rm, new Vector3(x0 + 3.6f, ground + h, z + 0.9f), 0.85f, 30f);
            Barrel(rm, new Vector3(x0 + 4.2f, ground + h, z - 0.9f), 0.4f, 0.9f, 0f);
            Barrel(rm, new Vector3(x0 + 5.0f, ground + h, z - 1.0f), 0.4f, 0.9f, 20f, P.WoodDark);
            Sack(rm, new Vector3(x0 + 2.2f, ground + h, z + 0.9f), 1.0f, 40f);
            Sack(rm, new Vector3(x0 + 2.8f, ground + h, z + 1.15f), 0.9f, 100f);
            Frustum(rm, new Vector3(x1 - 1.0f, ground + h, z + 0.2f));
            Box(cols, "PlatformGoods", new Vector3(x0 + 2.3f, ground + h + 0.7f, z - 0.2f), new Vector3(3.4f, 1.4f, 2.4f), 0f);
            Box(cols, "OreBin", new Vector3(x1 - 1.0f, ground + h + 0.8f, z + 0.2f), new Vector3(1.7f, 1.6f, 1.7f), 0f);
            LanternPost(rm, new Vector3(x0 + 0.3f, ground + h, z + 1.25f), 270f, 2.0f);
        }

        private static void Frustum(RcMesh rm, Vector3 bottom)
        {
            rm.Frustum(bottom, new Vector2(1.0f, 1.0f), new Vector2(1.9f, 1.9f), 1.2f, P.WoodGrey, P.RockShade);
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    rm.Beam(bottom + new Vector3(sx * 0.5f, 0f, sz * 0.5f), bottom + new Vector3(sx * 0.95f, 1.2f, sz * 0.95f), 0.1f, 0.1f, P.Iron);
                }
            }

            rm.Beam(bottom + new Vector3(-0.95f, 1.2f, 0f), bottom + new Vector3(0.95f, 1.2f, 0f), 0.12f, 0.12f, P.WoodDark);
        }

        private static void JibCrane(RcMesh rm, Transform cols)
        {
            // a wooden mast on the platform's far end with a boom swung out over the track, a brace and a rope with a crate on a hook
            Vector3 b = At(21.0f, 23.2f, 0.9f);
            Take(21f, 23.2f, 1.8f);
            rm.Push(b, 0f);
            rm.Block(Vector3.zero, new Vector3(0.45f, 5.4f, 0.45f), P.WoodDark, P.WoodGrey);
            rm.Beam(new Vector3(0f, 4.8f, 0f), new Vector3(0f, 5.5f, -3.9f), 0.32f, 0.32f, P.Wood);
            rm.Beam(new Vector3(0f, 2.4f, 0f), new Vector3(0f, 5.2f, -3.3f), 0.2f, 0.2f, P.WoodLight);
            rm.Beam(new Vector3(0f, 1.4f, 0f), new Vector3(0f, 2.4f, -0.9f), 0.18f, 0.18f, P.WoodDark);
            rm.Pipe(new Vector3(-0.16f, 5.7f, -3.8f), new Vector3(0.16f, 5.7f, -3.8f), 0.18f, 8, P.Iron, P.IronLight);
            rm.Beam(new Vector3(0f, 5.5f, -3.8f), new Vector3(0f, 3.0f, -3.8f), 0.04f, 0.04f, P.Iron);
            rm.Poly(new Vector3(0f, 2.75f, -3.8f), 0.12f, 0.12f, 0.1f, 6, P.Iron, P.IronLight);
            Crate(rm, new Vector3(0f, 1.8f, -3.8f), 0.9f, 15f);
            rm.Pop();
            Box(cols, "CraneMast", b + new Vector3(0f, 2.5f, 0f), new Vector3(0.6f, 5f, 0.6f), 0f);
        }

        private static void Signal(RcMesh rm, Transform cols, float x, float z)
        {
            Vector3 p = At(x, z);
            Take(x, z, 1.4f);
            rm.Push(p, 100f);
            rm.Block(Vector3.zero, new Vector3(0.22f, 4.6f, 0.22f), P.WoodDark, P.WoodGrey);
            rm.Beam(new Vector3(0.1f, 0.1f, 0f), new Vector3(0.1f, 3.2f, 0f), 0.06f, 0.06f, P.Iron);
            rm.Push(new Vector3(0.12f, 4.1f, 0f), 0f, 0f, -28f);
            rm.Box(new Vector3(0.9f, 0f, 0.14f), new Vector3(1.9f, 0.3f, 0.06f), P.White);
            rm.Box(new Vector3(0.45f, 0f, 0.17f), new Vector3(0.3f, 0.3f, 0.05f), P.ClothRed);
            rm.Box(new Vector3(1.35f, 0f, 0.17f), new Vector3(0.3f, 0.3f, 0.05f), P.ClothRed);
            rm.Pop();
            rm.Glow = true;
            rm.Box(new Vector3(-0.18f, 4.35f, 0.14f), new Vector3(0.2f, 0.24f, 0.18f), P.Glass);
            rm.Glow = false;
            rm.Pop();
            Box(cols, "SignalPost", p + new Vector3(0f, 1.5f, 0f), new Vector3(0.4f, 3f, 0.4f), 0f);
        }
    }
}
#endif
