#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>The parts library of stage 16B: crystal shapes and formations, pillars, statues, wall reliefs, friezes, banners, signs, layered rock, rubble, lanterns.</summary>
    public static partial class AmDressing
    {
        private static Vector3 V(float x, float y, float z) => new(x, y, z);

        // ================================================================ crystals: ONE shape generator, many looks

        /// <summary>Colour sets {body, light, dark}: cyan, blue, violet, turquoise, magenta.</summary>
        private static readonly int[][] CrystalSets =
        {
            new[] { P.CrystalCyan, P.CrystalCyanLight, P.CrystalCyanDark },
            new[] { P.CrystalBlue, P.CrystalBlueLight, P.CrystalBlueDark },
            new[] { P.CrystalViolet, P.CrystalVioletLight, P.CrystalVioletDark },
            new[] { P.CrystalTurquoise, P.CrystalTurquoiseLight, P.CrystalTurquoiseDark },
            new[] { P.CrystalMagenta, P.CrystalMagentaLight, P.CrystalMagentaDark },
        };

        /// <summary>
        /// One crystal: an n-gon shaft that narrows a little (<paramref name="topScale"/>) up to <paramref name="shaft"/> of the height, then a pyramid tip whose apex may sit off-centre. Leans by
        /// <paramref name="lean"/> degrees towards <paramref name="leanYaw"/>. Alternate faces use the body and the dark colour, the tip the light colour: the "cut gem" look at no texture cost.
        /// </summary>
        private static void Shard(AmMesh m, Vector3 bottomCentre, float radius, float height, int sides, float shaft, float topScale, Vector2 tipShift, float lean, float leanYaw, float spin, int[] set, int tipColour)
        {
            m.Push(bottomCentre, Quaternion.Euler(0f, leanYaw, 0f) * Quaternion.Euler(lean, 0f, 0f));
            float sh = height * shaft;
            var lo = new Vector3[sides];
            var hi = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = (k / (float)sides * 360f + spin) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                lo[k] = new Vector3(cos * radius, 0f, sin * radius);
                hi[k] = new Vector3(cos * radius * topScale, sh, sin * radius * topScale);
            }

            Vector3 apex = new(tipShift.x, height, tipShift.y);
            Vector3 inside = new(0f, sh * 0.5f, 0f);
            for (int k = 0; k < sides; k++)
            {
                int n = (k + 1) % sides;
                m.Face((k & 1) == 0 ? set[0] : set[2], inside, lo[k], hi[k], hi[n], lo[n]);
                m.Face((k & 1) == 0 ? tipColour : set[1], inside, hi[k], apex, hi[n]);
            }

            m.Pop();
        }

        /// <summary>
        /// A crystal formation: a tall main crystal, satellites of different heights, widths, sides, leans and (sometimes) an accent colour, a few small ones at the foot and a rock mound under it.
        /// Every shard has its own parameters, so no two formations (and no two crystals of one formation) look alike. <paramref name="colliderHeight"/> &gt; 0 gives it a capsule collider.
        /// </summary>
        private static void Formation(Ctx c, float x, float z, float height, int theme, int shards, float spread, float colliderHeight, int seed, int accent = -1, bool mound = true, float groundY = float.NaN)
        {
            AmMesh m = c.Crystals;
            bool previous = m.Glow;
            m.Glow = true;
            float y = float.IsNaN(groundY) ? c.Floor(x, z) : groundY;
            Vector3 b = new(x, y, z);
            int[] main = CrystalSets[theme % CrystalSets.Length];
            int[] second = CrystalSets[(accent >= 0 ? accent : theme + 2) % CrystalSets.Length];

            Shard(m, b - Vector3.up * 0.3f, height * (0.15f + Rand(c, seed, 90) * 0.04f), height + 0.3f, 6 + (int)(Rand(c, seed, 91) * 2f), 0.6f + Rand(c, seed, 92) * 0.1f, 0.78f, new Vector2((Rand(c, seed, 93) - 0.5f) * 0.4f, (Rand(c, seed, 94) - 0.5f) * 0.4f), 2f + Rand(c, seed, 95) * 4f, Rand(c, seed, 96) * 360f, Rand(c, seed, 97) * 60f, main, main[1]);
            for (int k = 0; k < shards; k++)
            {
                float angle = (k / (float)shards + (Rand(c, seed, k + 3) - 0.5f) * 0.35f) * 360f;
                float radial = spread * (0.3f + Rand(c, seed, k + 9) * 0.75f);
                float h = height * (0.26f + Rand(c, seed, k + 20) * 0.5f);
                float r = h * (0.12f + Rand(c, seed, k + 30) * 0.11f);
                int sides = 5 + (int)(Rand(c, seed, k + 40) * 3f);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 p = b + new Vector3(Mathf.Cos(rad) * radial, -0.3f, Mathf.Sin(rad) * radial);
                int[] set = Rand(c, seed, k + 50) < 0.32f ? second : main;
                int tip = Rand(c, seed, k + 55) < 0.2f ? P.CrystalWhite : set[1];
                Shard(m, p, r, h, sides, 0.42f + Rand(c, seed, k + 60) * 0.32f, 0.6f + Rand(c, seed, k + 65) * 0.36f,
                    new Vector2((Rand(c, seed, k + 70) - 0.5f) * r * 1.1f, (Rand(c, seed, k + 75) - 0.5f) * r * 1.1f), 8f + Rand(c, seed, k + 80) * 24f, 90f - angle + (Rand(c, seed, k + 85) - 0.5f) * 40f, Rand(c, seed, k + 87) * 60f, set, tip);
            }

            for (int k = 0; k < 3; k++)
            {
                float angle = Rand(c, seed, k + 110) * 360f * Mathf.Deg2Rad;
                float radial = spread * (0.7f + Rand(c, seed, k + 113) * 0.8f);
                float h = height * (0.07f + Rand(c, seed, k + 116) * 0.1f);
                Shard(m, b + new Vector3(Mathf.Cos(angle) * radial, -0.1f, Mathf.Sin(angle) * radial), h * 0.22f, h, 5, 0.5f, 0.8f, Vector2.zero, 14f + Rand(c, seed, k + 119) * 14f, Rand(c, seed, k + 122) * 360f, 0f, k == 1 ? second : main, main[1]);
            }

            m.Glow = previous;
            if (mound)
            {
                c.Mesh.Rock(b + Vector3.up * (height * 0.025f), new Vector3(spread * 1.05f, height * 0.09f + 0.25f, spread * 1.05f), seed, P.Rock(c.Id, 2), P.Rock(c.Id, 1), P.Rock(c.Id, 3));
            }

            c.Formations++;
            if (colliderHeight > 0f)
            {
                c.Capsule("Crystal", b, Mathf.Max(0.7f, spread * 0.7f), colliderHeight);
            }
        }

        /// <summary>A formation only where it keeps clear of every route and of the cave walls (the passages stay as wide as designed).</summary>
        private static bool TryFormation(Ctx c, float x, float z, float height, int theme, int shards, float spread, float colliderHeight, int seed, int accent = -1)
        {
            if (AmLayout.RouteDistance(x, z) < 3.6f + spread || AmLayout.EdgeDistance(x, z) > -(spread + 0.8f))
            {
                return false;
            }

            Formation(c, x, z, height, theme, shards, spread, colliderHeight, seed, accent);
            return true;
        }

        /// <summary>A few small crystals (no collider, no mound) growing from a spot: path flanks, wall bases, scattered shards.</summary>
        private static void Sprinkle(Ctx c, float x, float z, float height, int theme, int count, int seed, float groundY = float.NaN)
        {
            AmMesh m = c.Crystals;
            bool previous = m.Glow;
            m.Glow = true;
            float y = float.IsNaN(groundY) ? c.Floor(x, z) : groundY;
            int[] main = CrystalSets[theme % CrystalSets.Length];
            for (int k = 0; k < count; k++)
            {
                float a = Rand(c, seed, k) * Mathf.PI * 2f, d = Rand(c, seed, k + 7) * height * 0.5f;
                float h = height * (0.45f + Rand(c, seed, k + 14) * 0.7f);
                Shard(m, new Vector3(x + Mathf.Cos(a) * d, y - 0.05f, z + Mathf.Sin(a) * d), h * 0.2f, h, 5 + (k & 1), 0.55f, 0.8f, Vector2.zero, 6f + Rand(c, seed, k + 21) * 22f, Rand(c, seed, k + 28) * 360f, Rand(c, seed, k + 35) * 60f, main, main[1]);
            }

            m.Glow = previous;
        }

        /// <summary>A crystal or a rock spike hanging from the ceiling (visual only).</summary>
        private static void Hanging(Ctx c, float x, float z, float length, int theme, bool glowing, int rockTone)
        {
            float top = AmLayout.CeilingAt(x, z) + 0.3f;
            if (glowing)
            {
                AmMesh m = c.Crystals;
                bool previous = m.Glow;
                m.Glow = true;
                int[] set = CrystalSets[theme % CrystalSets.Length];
                m.Push(new Vector3(x, top, z), Quaternion.Euler(180f, 0f, 0f));
                Shard(m, Vector3.zero, length * 0.2f, length, 6, 0.55f, 0.75f, Vector2.zero, 3f, Rand(c, (int)(x * 7f), (int)(z * 5f)) * 360f, 30f, set, set[1]);
                m.Pop();
                m.Glow = previous;
            }
            else
            {
                AmMesh m = c.Mesh;
                m.Push(new Vector3(x, top, z), Quaternion.Euler(180f, 0f, 0f));
                m.Poly(Vector3.zero, length * 0.2f, 0f, length, 6, rockTone, rockTone, 20f);
                m.Pop();
            }
        }

        // ================================================================ pillars

        private static void FlutedShaft(AmMesh m, Vector3 bottomCentre, float rBottom, float rTop, float height, int sides, int colorA, int colorB, float rotate)
        {
            var lo = new Vector3[sides];
            var hi = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = (k / (float)sides * 360f + rotate) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                lo[k] = bottomCentre + new Vector3(cos * rBottom, 0f, sin * rBottom);
                hi[k] = bottomCentre + new Vector3(cos * rTop, height, sin * rTop);
            }

            Vector3 inside = bottomCentre + new Vector3(0f, height * 0.5f, 0f);
            for (int k = 0; k < sides; k++)
            {
                int n = (k + 1) % sides;
                m.Face((k & 1) == 0 ? colorA : colorB, inside, lo[k], hi[k], hi[n], lo[n]);
            }
        }

        /// <summary>
        /// A pillar of the old kingdom: two plinth steps, a rounded base with a gold ring, a fluted 12-sided shaft with a gold band, a gold neck band, a flared capital with a gold rim and a square abacus.
        /// The collider is a capsule as tall as the camera can reach, not the whole shaft.
        /// </summary>
        private static void Pillar(Ctx c, float x, float z, float radius, float height, AmZone zone, bool collider = true, float groundY = float.NaN, int glowRing = -1)
        {
            AmMesh m = c.Mesh;
            float r = radius;
            Vector3 b = new(x, float.IsNaN(groundY) ? c.Floor(x, z) : groundY, z);
            int plinth = P.Rock(zone, 3), step = P.Rock(zone, 4), shaftA = P.Rock(zone, 5), shaftB = P.Rock(zone, 4), cap = P.Rock(zone, 6), capTop = P.Rock(zone, 7);
            m.Block(b, V(r * 2.9f, 0.55f, r * 2.9f), plinth, step);
            m.Block(b + Vector3.up * 0.55f, V(r * 2.4f, 0.5f, r * 2.4f), step, shaftA);
            m.Poly(b + Vector3.up * 1.05f, r * 1.28f, r * 1.1f, 0.4f, 12, cap, capTop, 15f);
            m.Poly(b + Vector3.up * 1.45f, r * 1.12f, r * 1.12f, 0.13f, 12, P.GoldBright, P.GoldBright, 15f);
            if (glowRing >= 0)
            {
                // an emissive ring round the foot (the vault: turquoise): the pillars stand in their own light
                m.Glow = true;
                m.Poly(b + Vector3.up * 1.05f, r * 1.3f, r * 1.3f, 0.09f, 12, glowRing, glowRing, 15f);
                m.Glow = false;
            }

            float shaftBottom = 1.58f, shaftTop = height - 2.45f;
            FlutedShaft(m, b + Vector3.up * shaftBottom, r, r * 0.9f, shaftTop - shaftBottom, 12, shaftA, shaftB, 15f);
            m.Poly(b + Vector3.up * (shaftBottom + (shaftTop - shaftBottom) * 0.45f), r * 1.0f, r * 1.0f, 0.2f, 12, P.Gold, P.Gold, 15f);
            m.Poly(b + Vector3.up * shaftTop, r * 0.95f, r * 0.95f, 0.2f, 12, P.GoldBright, P.GoldBright, 15f);
            m.Poly(b + Vector3.up * (shaftTop + 0.2f), r * 1.1f, r * 1.65f, 1.2f, 12, cap, capTop, 15f);
            m.Poly(b + Vector3.up * (shaftTop + 1.4f), r * 1.66f, r * 1.66f, 0.18f, 12, P.Gold, P.Gold, 15f);
            m.Block(b + Vector3.up * (shaftTop + 1.58f), V(r * 3.5f, 0.87f, r * 3.5f), cap, capTop);
            if (collider)
            {
                c.Capsule("Pillar", b, r * 1.05f, Mathf.Min(height, 9f));
            }
        }

        /// <summary>A broken pillar: a stump with a jagged top, a gold band, and a fallen drum and capital beside it.</summary>
        private static void BrokenPillar(Ctx c, float x, float z, float radius, float height, float drumYaw, AmZone zone, bool drum = true, float groundY = float.NaN)
        {
            AmMesh m = c.Mesh;
            float r = radius;
            Vector3 b = new(x, float.IsNaN(groundY) ? c.Floor(x, z) : groundY, z);
            int plinth = P.Rock(zone, 3), step = P.Rock(zone, 4), shaftA = P.Rock(zone, 5), shaftB = P.Rock(zone, 4), cap = P.Rock(zone, 6);
            m.Block(b, V(r * 2.9f, 0.5f, r * 2.9f), plinth, step);
            m.Poly(b + Vector3.up * 0.5f, r * 1.22f, r * 1.05f, 0.35f, 12, cap, cap, 15f);
            m.Poly(b + Vector3.up * 0.85f, r * 1.08f, r * 1.08f, 0.12f, 12, P.Gold, P.Gold, 15f);
            float top = Mathf.Max(1.6f, height - 0.9f);
            FlutedShaft(m, b + Vector3.up * 0.97f, r, r * 0.93f, top - 0.97f, 12, shaftA, shaftB, 15f);
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90f + 20f + Rand(c, (int)(x * 3f), k) * 30f) * Mathf.Deg2Rad;
                float h = 0.5f + Rand(c, (int)(z * 3f), k + 4) * 1.1f;
                m.Push(b + new Vector3(Mathf.Cos(a) * r * 0.45f, top - 0.05f, Mathf.Sin(a) * r * 0.45f), Quaternion.Euler(Rand(c, k, 2) * 18f - 9f, k * 90f, Rand(c, k, 3) * 18f - 9f));
                m.Poly(Vector3.zero, r * 0.5f, r * 0.12f, h, 4 + (k & 1), shaftA, shaftB, 45f);
                m.Pop();
            }

            c.Capsule("BrokenPillar", b, r * 1.05f, Mathf.Min(top + 0.4f, 6f));
            if (drum)
            {
                Vector3 axis = new(Mathf.Cos(drumYaw * Mathf.Deg2Rad), 0f, Mathf.Sin(drumYaw * Mathf.Deg2Rad));
                Vector3 d = b + axis * (r * 3.4f);
                d.y = c.Floor(d.x, d.z);
                Vector3 centre = d + Vector3.up * r * 0.95f;
                m.Pipe(centre - axis * r * 1.4f, centre + axis * r * 1.4f, r * 0.95f, 12, shaftA, shaftB);
                m.Poly(centre + axis * r * 1.4f - Vector3.up * 0.0f, r * 0.95f, r * 0.95f, 0.1f, 12, P.Gold, P.Gold, 15f);
                c.Box("Drum", centre, V(r * 2.8f, r * 1.9f, r * 1.9f), -drumYaw);
                // the fallen capital further along
                Vector3 cc = d + axis * (r * 3.4f);
                cc.y = c.Floor(cc.x, cc.z);
                m.Push(cc + Vector3.up * r * 0.9f, Quaternion.Euler(0f, -drumYaw, 90f));
                m.Poly(V(0f, -0.6f, 0f), r * 1.1f, r * 1.65f, 1.2f, 12, cap, cap, 15f);
                m.Pop();
                c.Box("FallenCapital", cc + Vector3.up * r * 0.9f, V(r * 2.4f, r * 1.8f, r * 1.9f), -drumYaw);
            }
        }

        // ================================================================ statues

        /// <summary>
        /// A stone statue made of boxes and prisms (about 11 m with its spear at scale 1). <paramref name="kind"/> 0 = a guardian with spear and shield, 1 = a king with crown and scepter. Gold trim, teal
        /// cloth, glowing eyes and a glowing chest gem; the colliders are a capsule for the body (statues stand beside the routes, never on them).
        /// </summary>
        private static void Statue(Ctx c, float x, float z, float yaw, float scale, int kind, float groundY = float.NaN, bool collider = true)
        {
            AmMesh m = c.Mesh;
            Vector3 b = new(x, float.IsNaN(groundY) ? c.Floor(x, z) : groundY, z);
            int light = P.StatueLight, mid = P.StatueMid, dark = P.StatueDark, teal = P.StatueTeal, gold = P.Gold, bright = P.GoldBright;
            m.PushScaled(b, Quaternion.Euler(0f, yaw, 0f), scale);
            // plinth with a gold band and a crystal sign on its front
            m.Block(V(0f, 0f, 0f), V(3.6f, 0.9f, 3.6f), dark, mid);
            m.Block(V(0f, 0.9f, 0f), V(3.0f, 0.55f, 3.0f), mid, light);
            m.Block(V(0f, 1.45f, 0f), V(3.12f, 0.14f, 3.12f), bright, bright);
            CrystalSign(m, V(0f, 0.45f, 1.82f), 0f, 0.9f, gold, bright);
            // legs and boots
            for (int side = -1; side <= 1; side += 2)
            {
                m.Block(V(side * 0.75f, 1.59f, 0.15f), V(0.95f, 0.4f, 1.45f), dark, mid);
                m.Block(V(side * 0.75f, 1.99f, 0f), V(0.8f, 2.2f, 0.9f), mid, mid);
            }

            // kilt or robe, belt
            if (kind == 0)
            {
                m.Poly(V(0f, 3.95f, 0f), 1.55f, 1.15f, 1.45f, 8, teal, mid, 22.5f);
                m.Poly(V(0f, 3.95f, 0f), 1.58f, 1.58f, 0.14f, 8, bright, bright, 22.5f);
            }
            else
            {
                m.Poly(V(0f, 1.99f, 0f), 1.95f, 1.3f, 3.4f, 10, teal, mid, 18f);
                m.Poly(V(0f, 1.99f, 0f), 1.98f, 1.98f, 0.16f, 10, bright, bright, 18f);
            }

            m.Poly(V(0f, 5.35f, 0f), 1.28f, 1.28f, 0.3f, 8, bright, bright, 22.5f);
            // torso, chest plate, glowing gem
            m.Box(V(0f, 6.65f, 0f), V(2.3f, 2.4f, 1.4f), light, light);
            m.Box(V(0f, 6.6f, 0.72f), V(1.3f, 1.2f, 0.12f), gold, bright);
            m.Glow = true;
            m.Box(V(0f, 6.6f, 0.8f), V(0.36f, 0.56f, 0.1f), P.PortalGlowBright);
            m.Glow = false;
            // shoulders with gold caps, arms, hands
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(V(side * 1.65f, 7.7f, 0f), V(1.0f, 0.85f, 1.25f), mid, light);
                m.Box(V(side * 1.65f, 8.17f, 0f), V(1.08f, 0.14f, 1.33f), bright, bright);
            }

            m.Box(V(-1.78f, 6.2f, 0.1f), V(0.75f, 2.5f, 0.85f), mid, mid);
            m.Box(V(1.78f, 6.6f, 0.2f), V(0.75f, 2.0f, 0.85f), mid, mid);
            m.Box(V(-1.78f, 4.85f, 0.3f), V(0.65f, 0.6f, 0.65f), light, light);
            m.Box(V(1.85f, 5.45f, 0.4f), V(0.7f, 0.7f, 0.7f), light, light);
            if (kind == 0)
            {
                // spear
                m.Beam(V(1.85f, 1.6f, 0.4f), V(1.85f, 10.9f, 0.4f), 0.22f, 0.22f, dark);
                m.Poly(V(1.85f, 10.9f, 0.4f), 0.32f, 0f, 1.2f, 4, bright, bright, 45f);
                m.Poly(V(1.85f, 9.9f, 0.4f), 0.3f, 0.3f, 0.18f, 6, gold, gold, 0f);
                // shield
                m.Pipe(V(-2.45f, 6.0f, 0.55f), V(-2.45f, 6.0f, 0.9f), 1.3f, 6, dark, mid);
                m.Pipe(V(-2.45f, 6.0f, 0.88f), V(-2.45f, 6.0f, 0.98f), 1.36f, 6, gold, bright);
                m.Pipe(V(-2.45f, 6.0f, 0.95f), V(-2.45f, 6.0f, 1.08f), 0.38f, 6, bright, bright);
            }
            else
            {
                // scepter with a glowing gem
                m.Beam(V(1.85f, 1.6f, 0.4f), V(1.85f, 9.0f, 0.4f), 0.2f, 0.2f, gold);
                m.Glow = true;
                m.Poly(V(1.85f, 9.0f, 0.4f), 0.42f, 0f, 1.0f, 5, P.GemGold, P.GemGold, 0f);
                m.Glow = false;
            }

            // cape, neck, head
            m.Box(V(0f, 6.1f, -0.85f), V(2.0f, 3.6f, 0.14f), teal, teal);
            m.Box(V(0f, 8.35f, 0f), V(0.75f, 0.5f, 0.75f), mid, mid);
            m.Box(V(0f, 9.15f, 0f), V(1.3f, 1.3f, 1.3f), light, light);
            if (kind == 0)
            {
                m.Poly(V(0f, 9.65f, 0f), 0.95f, 0.72f, 0.75f, 6, mid, dark, 0f);
                m.Box(V(0f, 10.55f, -0.1f), V(0.24f, 0.8f, 1.3f), bright, bright);
            }
            else
            {
                m.Poly(V(0f, 9.75f, 0f), 0.8f, 0.8f, 0.35f, 6, bright, bright, 0f);
                for (int k = 0; k < 5; k++)
                {
                    float a = k * Mathf.PI * 2f / 5f;
                    m.Poly(V(Mathf.Cos(a) * 0.64f, 10.1f, Mathf.Sin(a) * 0.64f), 0.15f, 0f, 0.6f, 4, bright, bright, 45f);
                }
            }

            m.Glow = true;
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(V(side * 0.3f, 9.2f, 0.66f), V(0.26f, 0.13f, 0.07f), P.PortalGlowBright);
            }

            m.Glow = false;
            m.Pop();
            if (collider)
            {
                c.Capsule("Statue", b, 1.7f * scale, Mathf.Min(9f, 9.5f * scale));
            }
        }

        // ================================================================ signs, reliefs, friezes, banners

        /// <summary>A small crystal sign (a gold diamond with a bright core) in the CURRENT local frame, facing +z.</summary>
        private static void CrystalSign(AmMesh m, Vector3 pos, float yaw, float size, int gold, int bright)
        {
            m.Push(pos, yaw);
            float h = size * 0.5f, w = size * 0.26f;
            m.Face(gold, V(0f, 0f, -1f), V(0f, h, 0f), V(w, 0f, 0f), V(0f, -h, 0f), V(-w, 0f, 0f));
            m.Face(bright, V(0f, 0f, -1f), V(0f, h * 0.56f, 0.012f), V(w * 0.52f, 0f, 0.012f), V(0f, -h * 0.56f, 0.012f), V(-w * 0.52f, 0f, 0.012f));
            m.Pop();
        }

        /// <summary>
        /// A carved wall panel (a slab 0.1 m proud of the wall, a gold border and a motif). <paramref name="style"/>: 0 crystal sign, 1 sun, 2 crossed pickaxes (mining guild), 3 crown. No collider.
        /// </summary>
        private static void Relief(Ctx c, WallRef w, float width, float height, int style, int stone, int trim, int motif)
        {
            AmMesh m = c.Mesh;
            PushWall(m, w);
            m.Box(V(0f, 0f, -0.6f), V(width, height, 1.4f), stone, stone);
            float t = 0.2f;
            m.Box(V(0f, height * 0.5f - t * 0.5f, 0.1f), V(width, t, 0.08f), trim, trim);
            m.Box(V(0f, -height * 0.5f + t * 0.5f, 0.1f), V(width, t, 0.08f), trim, trim);
            m.Box(V(-width * 0.5f + t * 0.5f, 0f, 0.1f), V(t, height - 2f * t, 0.08f), trim, trim);
            m.Box(V(width * 0.5f - t * 0.5f, 0f, 0.1f), V(t, height - 2f * t, 0.08f), trim, trim);
            float s = Mathf.Min(width, height) * 0.36f;
            switch (style)
            {
                case 0:
                    m.Face(trim, V(0f, 0f, -1f), V(0f, s, 0.12f), V(s * 0.55f, 0f, 0.12f), V(0f, -s, 0.12f), V(-s * 0.55f, 0f, 0.12f));
                    m.Face(motif, V(0f, 0f, -1f), V(0f, s * 0.58f, 0.135f), V(s * 0.3f, 0f, 0.135f), V(0f, -s * 0.58f, 0.135f), V(-s * 0.3f, 0f, 0.135f));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        m.Face(trim, V(0f, 0f, -1f), V(side * s * 0.95f, s * 0.42f, 0.12f), V(side * s * 1.18f, 0f, 0.12f), V(side * s * 0.95f, -s * 0.42f, 0.12f), V(side * s * 0.72f, 0f, 0.12f));
                    }

                    break;
                case 1:
                    {
                        var disc = new Vector3[10];
                        for (int k = 0; k < 10; k++)
                        {
                            float a = -k / 10f * Mathf.PI * 2f;
                            disc[k] = V(Mathf.Cos(a) * s * 0.55f, Mathf.Sin(a) * s * 0.55f, 0.12f);
                        }

                        m.Face(trim, V(0f, 0f, -1f), disc);
                        for (int k = 0; k < 8; k++)
                        {
                            float a = k * Mathf.PI / 4f;
                            m.Face(trim, V(0f, 0f, -1f), V(Mathf.Cos(a - 0.17f) * s * 0.65f, Mathf.Sin(a - 0.17f) * s * 0.65f, 0.12f), V(Mathf.Cos(a) * s * 1.0f, Mathf.Sin(a) * s * 1.0f, 0.12f), V(Mathf.Cos(a + 0.17f) * s * 0.65f, Mathf.Sin(a + 0.17f) * s * 0.65f, 0.12f));
                        }

                        m.Face(motif, V(0f, 0f, -1f), V(0f, s * 0.26f, 0.14f), V(s * 0.2f, 0f, 0.14f), V(0f, -s * 0.26f, 0.14f), V(-s * 0.2f, 0f, 0.14f));
                        break;
                    }

                case 2:
                    for (int side = -1; side <= 1; side += 2)
                    {
                        m.Beam(V(-side * s * 0.8f, -s * 0.9f, 0.14f), V(side * s * 0.8f, s * 0.7f, 0.14f), 0.16f, 0.1f, P.Bronze);
                        m.Beam(V(side * s * 0.35f, s * 0.7f, 0.15f), V(side * s * 1.1f, s * 0.7f, 0.15f), 0.2f, 0.12f, trim);
                    }

                    m.Face(motif, V(0f, 0f, -1f), V(0f, s * 0.4f, 0.16f), V(s * 0.2f, 0.1f, 0.16f), V(0f, -s * 0.2f, 0.16f), V(-s * 0.2f, 0.1f, 0.16f));
                    break;
                default:
                    m.Box(V(0f, -s * 0.45f, 0.12f), V(s * 1.5f, s * 0.35f, 0.08f), trim, trim);
                    for (int k = -1; k <= 1; k++)
                    {
                        m.Face(trim, V(0f, 0f, -1f), V(k * s * 0.5f - s * 0.22f, -s * 0.3f, 0.12f), V(k * s * 0.5f, s * (k == 0 ? 0.85f : 0.55f), 0.12f), V(k * s * 0.5f + s * 0.22f, -s * 0.3f, 0.12f));
                    }

                    m.Face(motif, V(0f, 0f, -1f), V(-s * 0.1f, -s * 0.45f, 0.15f), V(0f, -s * 0.2f, 0.15f), V(s * 0.1f, -s * 0.45f, 0.15f));
                    break;
            }

            m.Pop();
        }

        /// <summary>A gold band along a wall (a frieze), 0.1 m proud of the wall, with small studs.</summary>
        private static void Frieze(Ctx c, string shape, int side, float from, float to, float y, float height, int color, int stud)
        {
            AmMesh m = c.Mesh;
            float mid = (from + to) * 0.5f, length = Mathf.Abs(to - from);
            WallRef w = Wall(shape, side, mid, y);
            PushWall(m, w);
            m.Box(V(0f, 0f, -0.4f), V(length, height, 1.0f), color, color);
            int studs = Mathf.FloorToInt(length / 1.8f);
            for (int k = 0; k < studs; k++)
            {
                m.Box(V(-length * 0.5f + (k + 0.5f) * (length / studs), 0f, 0.13f), V(0.28f, height * 0.5f, 0.08f), stud, stud);
            }

            m.Pop();
        }

        /// <summary>A hanging banner (cloth, gold bars, a crystal sign). Visual only; hang it above head height.</summary>
        private static void Banner(Ctx c, Vector3 pos, float yaw, float width, float height, int cloth, int gold, int bright)
        {
            AmMesh m = c.Mesh;
            m.Push(pos, yaw);
            m.Box(V(0f, 0f, 0f), V(width, height, 0.1f), cloth, cloth);
            m.Box(V(0f, height * 0.5f + 0.08f, 0.02f), V(width * 1.2f, 0.16f, 0.2f), gold, gold);
            m.Box(V(0f, -height * 0.5f - 0.02f, 0.02f), V(width, 0.14f, 0.14f), gold, gold);
            m.Face(cloth, V(0f, 0f, -1f), V(-width * 0.5f, -height * 0.5f - 0.1f, 0f), V(0f, -height * 0.5f - 0.7f, 0f), V(width * 0.5f, -height * 0.5f - 0.1f, 0f));
            CrystalSign(m, V(0f, height * 0.05f, 0.07f), 0f, width * 0.7f, bright, gold);
            m.Pop();
        }

        // ================================================================ layered rock, rubble, lanterns, debris

        /// <summary>A layered rock formation (strata): stacked, slightly different 7-sided slabs that narrow upwards, in alternating rock tones with lighter ledges.</summary>
        private static void Strata(Ctx c, float x, float z, float radius, float height, int tiers, int seed, bool collider = true)
        {
            AmMesh m = c.Mesh;
            Vector3 b = c.On(x, z);
            float y = 0f;
            for (int t = 0; t < tiers; t++)
            {
                float rb = radius * (1f - 0.13f * t) * (0.86f + Rand(c, seed, t) * 0.3f);
                float h = height / tiers * (0.75f + Rand(c, seed, t + 8) * 0.5f);
                Vector3 shift = new((Rand(c, seed, t + 16) - 0.5f) * radius * 0.2f, 0f, (Rand(c, seed, t + 24) - 0.5f) * radius * 0.2f);
                m.Poly(b + shift + Vector3.up * y, rb, rb * 0.9f, h, 7, P.Rock(c.Id, 2 + (t & 1) * 2), P.Rock(c.Id, 5), Rand(c, seed, t + 32) * 50f);
                y += h;
            }

            if (collider)
            {
                c.Sphere("Strata", b + Vector3.up * Mathf.Min(height * 0.4f, 2.4f), radius * 0.85f);
            }
        }

        private static void Rubble(Ctx c, float x, float z, float radius, int seed, bool collider = true, bool warm = false)
        {
            Vector3 b = c.On(x, z);
            int a = warm ? P.RubbleWarm : P.Rubble, d = warm ? P.RubbleWarmDark : P.RubbleDark;
            c.Mesh.Rock(b + Vector3.up * radius * 0.55f, V(radius, radius * 0.8f, radius), seed, a, d, P.StoneLight);
            c.Mesh.Rock(b + V(radius * 0.8f, radius * 0.3f, radius * 0.4f), V(radius * 0.5f, radius * 0.4f, radius * 0.5f), seed + 3, d, a, P.StoneLight);
            if (collider)
            {
                c.Sphere("Rubble", b + Vector3.up * radius * 0.5f, radius * 0.85f);
            }
        }

        /// <summary>Small visual-only stone scraps (no collider, lower than 0.25 m so they never catch a boot): fallen blocks, arch wedges.</summary>
        private static void Scraps(Ctx c, float x, float z, int count, float spread, int seed, int tone, float groundY = float.NaN)
        {
            AmMesh m = c.Mesh;
            for (int k = 0; k < count; k++)
            {
                float a = Rand(c, seed, k) * Mathf.PI * 2f, d = Rand(c, seed, k + 5) * spread;
                float px = x + Mathf.Cos(a) * d, pz = z + Mathf.Sin(a) * d;
                if (AmLayout.EdgeDistance(px, pz) > -1.2f || AmLayout.RouteDistance(px, pz) < 2.4f)
                {
                    continue;
                }

                float gy = float.IsNaN(groundY) ? c.Floor(px, pz) : groundY;
                float s = 0.35f + Rand(c, seed, k + 10) * 0.5f;
                m.Push(new Vector3(px, gy, pz), Quaternion.Euler(Rand(c, seed, k + 15) * 8f, Rand(c, seed, k + 20) * 360f, Rand(c, seed, k + 25) * 8f));
                m.Box(V(0f, 0.09f, 0f), V(s, 0.2f, s * (0.6f + Rand(c, seed, k + 30) * 0.5f)), P.Rock(c.Id, tone + (k & 1)), P.Rock(c.Id, tone + 2));
                m.Pop();
            }
        }

        private static void LanternPost(Ctx c, float x, float z, AmZone zone = AmZone.Workshop)
        {
            AmMesh m = c.Mesh;
            Vector3 b = c.On(x, z);
            m.Block(b, V(0.9f, 0.5f, 0.9f), P.Rock(zone, 3), P.Rock(zone, 5));
            m.Poly(b + Vector3.up * 0.5f, 0.28f, 0.22f, 2.8f, 6, P.Rock(zone, 4), P.Rock(zone, 5), 0f);
            m.Block(b + Vector3.up * 3.3f, V(0.8f, 0.2f, 0.8f), P.Bronze, P.Bronze);
            m.Glow = true;
            m.Box(b + Vector3.up * 3.75f, V(0.55f, 0.7f, 0.55f), P.LampWarm, P.LampWarm);
            m.Glow = false;
            m.Poly(b + Vector3.up * 4.1f, 0.5f, 0f, 0.5f, 4, P.Bronze, P.Bronze, 45f);
            c.Capsule("Lantern", b, 0.4f, 3.4f);
        }
    }
}
#endif
