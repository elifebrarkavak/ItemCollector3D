#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    public static partial class AmDressing
    {
        /// <summary>A formation only where it keeps clear of both routes and of the cave walls (the passages stay as wide as designed).</summary>
        private static bool TryCluster(Ctx c, float x, float z, float height, int theme, int count, float spread, float colliderHeight, int seed)
        {
            if (AmLayout.RouteDistance(x, z) < 3.6f + spread || AmLayout.EdgeDistance(x, z) > -(spread + 0.8f))
            {
                return false;
            }

            Cluster(c, x, z, height, theme, count, spread, colliderHeight, seed);
            return true;
        }

        // ================================================================ B  HALL OF PILLARS

        private static void DressHall(Ctx c)
        {
            AmMesh m = c.Mesh;
            float f = 11f;
            int shaft = P.Rock(AmZone.Hall, 5), plinth = P.Rock(AmZone.Hall, 3), bands = P.Rock(AmZone.Hall, 6);

            // ---- the road: west door -> centre -> east door, with a medallion in the middle of the hall
            var road = new List<Vector2> { new(-24f, -2f), new(-10f, 0f), new(0f, 0f), new(12f, -2f), new(24f, -4f) };
            Ribbon(c, road, 6.6f, P.RoadStone, 0.05f);
            Ribbon(c, road, 0.4f, P.RoadInlay, 0.08f, true);
            FloorDisc(c, 0f, 0f, 7.6f, P.RoadStoneDark, 0.06f);
            FloorDisc(c, 0f, 0f, 6.2f, P.StoneMid, 0.07f);
            FloorDisc(c, 0f, 0f, 4.2f, P.RoadStoneDark, 0.08f);
            FloorDisc(c, 0f, 0f, 3.3f, P.Marble, 0.09f);
            FloorDisc(c, 0f, 0f, 1.5f, P.RoadInlay, 0.1f, 16, true);

            // ---- the two rows of pillars along the hall, the aisle between them stays free
            for (int row = -1; row <= 1; row += 2)
            {
                float z = row * 8f;
                for (int k = 0; k < 6; k++)
                {
                    Column(c, -21f + k * 8.4f, z, 1.15f, 21f, shaft, plinth, bands); // 8.4 m apart: 6.1 m clear between two pillars
                }

                m.Beam(new Vector3(-22f, f + 17.4f, z), new Vector3(22f, f + 17.4f, z), 1.7f, 1.7f, P.Rock(AmZone.Hall, 4));
            }

            // cross beams on the two middle pillar pairs (the hall's skeleton, high above any walker)
            foreach (float x in new[] { -4.2f, 4.2f })
            {
                m.Beam(new Vector3(x, f + 17.4f, -8f), new Vector3(x, f + 17.4f, 8f), 1.5f, 1.5f, P.Rock(AmZone.Hall, 4));
            }

            // ---- doorways: a big arch in front of each of the three exits
            Gateway(c, new Vector3(-24.8f, f, -2f), 90f, 5.0f, 1.9f, 2.6f, 9f, shaft, plinth, true);
            Gateway(c, new Vector3(24.8f, f, -4f), 90f, 5.0f, 1.9f, 2.6f, 9f, shaft, plinth, true);
            Gateway(c, new Vector3(0f, f, 20.8f), 0f, 5.0f, 1.9f, 2.6f, 9f, shaft, plinth, true);

            // ---- the four daises: a low plinth wall along the front and crystals in the corners (visual weight, not obstacles)
            foreach (var d in new[] { (-1f, 1f), (1f, 1f), (-1f, -1f), (1f, -1f) })
            {
                float cx = d.Item1 * 20f, cz = d.Item2 * 17f;
                Cluster(c, d.Item1 * 22.2f, d.Item2 * 18.2f, 3.6f, d.Item1 > 0f ? 0 : 2, 4, 1.1f, 3f, (int)(d.Item1 * 3 + d.Item2 * 5 + 10));
                m.Block(new Vector3(cx, 13.4f, cz - d.Item2 * 3.2f), new Vector3(8f, 0.5f, 0.6f), plinth, bands);
            }

            // ---- light from above: crystals hanging from the ceiling (emissive, no real-time light) and rock spikes
            for (int k = 0; k < 12; k++)
            {
                Hanging(c, -19f + (k % 4) * 12.6f + Rand(c, k) * 2f, -13f + (k / 4) * 13f + Rand(c, k, 1) * 2f, 3.0f + Rand(c, k, 2) * 2.4f, k % 5, true, shaft);
            }

            for (int k = 0; k < 10; k++)
            {
                Hanging(c, -22f + Rand(c, k, 5) * 44f, -18f + Rand(c, k, 6) * 36f, 2.0f + Rand(c, k, 7) * 2.5f, 0, false, P.Rock(AmZone.Hall, 2));
            }
        }

        // ================================================================ C  CRYSTAL CAVERNS

        private static void DressCaverns(Ctx c)
        {
            AmMesh m = c.Mesh;
            int placed = 0;
            // ---- the atrium: big formations round its rim (never on a route), small ones scattered between
            for (int k = 0; k < 24; k++)
            {
                float a = (k * 15f + 7f) * Mathf.Deg2Rad, r = 9.4f + (k % 2) * 0.8f;
                placed += TryCluster(c, 48f + Mathf.Cos(a) * r, -4f + Mathf.Sin(a) * r, 5.5f + Rand(c, k) * 5f, k % 5, 4 + k % 3, 1.5f, 5f, 100 + k) ? 1 : 0;
            }

            for (int k = 0; k < 26; k++)
            {
                float a = Rand(c, k, 30) * Mathf.PI * 2f, r = 2f + Rand(c, k, 31) * 9f;
                float x = 48f + Mathf.Cos(a) * r, z = -4f + Mathf.Sin(a) * r;
                if (AmLayout.RouteDistance(x, z) > 4.4f + 1.0f && AmLayout.EdgeDistance(x, z) < -1.5f)
                {
                    Cluster(c, x, z, 0.8f + Rand(c, k, 32) * 1.1f, k % 5, 3, 0.45f, 0f, 200 + k);
                }
            }

            // ---- the northern branch and lobe
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 60f + 30f) * Mathf.Deg2Rad;
                placed += TryCluster(c, 46f + Mathf.Cos(a) * 5.6f, 33f + Mathf.Sin(a) * 5.6f, 4.5f + Rand(c, k, 40) * 4.5f, (k + 2) % 5, 5, 1.3f, 4.5f, 300 + k) ? 1 : 0;
            }

            // ---- the southern lobe (the last cave before the Royal Vault: the biggest crystals)
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 60f + 10f) * Mathf.Deg2Rad;
                placed += TryCluster(c, 47f + Mathf.Cos(a) * 5.6f, -37f + Mathf.Sin(a) * 5.6f, 6f + Rand(c, k, 50) * 5f, (k + 1) % 5, 5, 1.5f, 5f, 400 + k) ? 1 : 0;
            }

            // ---- the side grotto (a special place: one huge formation at the back, smaller ones round it)
            Cluster(c, 31.6f, 14f, 8.5f, 4, 6, 1.8f, 6f, 500);
            placed++;
            placed += TryCluster(c, 36f, 18.2f, 3.2f, 0, 4, 1.0f, 3f, 501) ? 1 : 0;
            placed += TryCluster(c, 36f, 9.8f, 3.6f, 2, 4, 1.0f, 3f, 502) ? 1 : 0;

            // ---- ceiling: rock spikes and glowing crystals
            var anchors = new[] { new Vector2(48f, -4f), new Vector2(52f, 12f), new Vector2(46f, 33f), new Vector2(50f, -20f), new Vector2(47f, -37f), new Vector2(36f, 14f), new Vector2(24f, 38f) };
            int hang = 0;
            for (int k = 0; k < 70; k++)
            {
                Vector2 a = anchors[k % anchors.Length];
                float x = a.x + (Rand(c, k, 70) - 0.5f) * 20f, z = a.y + (Rand(c, k, 71) - 0.5f) * 20f;
                if (AmLayout.EdgeDistance(x, z) > -1.5f)
                {
                    continue;
                }

                Hanging(c, x, z, 1.8f + Rand(c, k, 72) * 3.4f, k % 5, k % 3 == 0, P.Rock(AmZone.Crystal, 2));
                hang++;
            }

            Debug.Log($"AncientMine Crystal Caverns: {placed} formations placed, {hang} ceiling spikes");
            m.Glow = false;
        }

        // ================================================================ D  FORGOTTEN WORKSHOP

        private static void LanternPost(Ctx c, float x, float z)
        {
            AmMesh m = c.Mesh;
            Vector3 b = c.On(x, z);
            m.Block(b, new Vector3(0.9f, 0.5f, 0.9f), P.Rock(AmZone.Workshop, 3), P.Rock(AmZone.Workshop, 5));
            m.Poly(b + Vector3.up * 0.5f, 0.28f, 0.22f, 2.8f, 6, P.Rock(AmZone.Workshop, 4), P.Rock(AmZone.Workshop, 5), 0f);
            m.Block(b + Vector3.up * 3.3f, new Vector3(0.8f, 0.2f, 0.8f), P.Bronze, P.Bronze);
            m.Glow = true;
            m.Box(b + Vector3.up * 3.75f, new Vector3(0.55f, 0.7f, 0.55f), P.LampWarm, P.LampWarm);
            m.Glow = false;
            m.Poly(b + Vector3.up * 4.1f, 0.5f, 0f, 0.5f, 4, P.Bronze, P.Bronze, 45f);
            c.Capsule("Lantern", b, 0.4f, 3.4f);
        }

        private static void Rubble(Ctx c, float x, float z, float radius, int seed, bool collider = true)
        {
            Vector3 b = c.On(x, z);
            c.Mesh.Rock(b + Vector3.up * radius * 0.55f, new Vector3(radius, radius * 0.8f, radius), seed, P.Rubble, P.RubbleDark, P.StoneLight);
            c.Mesh.Rock(b + new Vector3(radius * 0.8f, radius * 0.3f, radius * 0.4f), new Vector3(radius * 0.5f, radius * 0.4f, radius * 0.5f), seed + 3, P.RubbleDark, P.Rubble, P.StoneLight);
            if (collider)
            {
                c.Sphere("Rubble", b + Vector3.up * radius * 0.5f, radius * 0.85f);
            }
        }

        private static void DressWorkshop(Ctx c)
        {
            AmMesh m = c.Mesh;
            int stone = P.Rock(AmZone.Workshop, 4), light = P.Rock(AmZone.Workshop, 6), dark = P.Rock(AmZone.Workshop, 2);

            // ---- the old road continues into the workshop and to the tunnel east
            Ribbon(c, new List<Vector2> { new(0f, 28f), new(0f, 33f), new(-12f, 40f) }, 5.6f, P.RoadStone, 0.05f);
            Ribbon(c, new List<Vector2> { new(-12f, 40f), new(4f, 40f), new(12f, 41f) }, 5.6f, P.RoadStone, 0.05f);
            Ribbon(c, new List<Vector2> { new(0f, 28f), new(0f, 33f), new(-12f, 40f), new(4f, 40f), new(12f, 41f) }, 0.4f, P.RoadInlay, 0.08f, true);

            // ---- big square piers holding the roof (tall: they reach the ceiling)
            foreach (var p in new[] { new Vector2(-22f, 35f), new Vector2(-22f, 45f), new Vector2(5f, 35f) })
            {
                Vector3 b = c.On(p.x, p.y);
                m.Block(b, new Vector3(3.6f, 0.8f, 3.6f), stone, light);
                m.Block(b + Vector3.up * 0.8f, new Vector3(2.6f, 12.6f, 2.6f), stone, stone);
                m.Block(b + Vector3.up * 13.4f, new Vector3(3.4f, 0.8f, 3.4f), stone, light);
                c.Box("Pier", b + Vector3.up * 4.5f, new Vector3(3.2f, 9f, 3.2f));
            }

            // ---- the stone crusher: a mortar bowl and a leaning pestle
            {
                Vector3 b = c.On(-28f, 46f);
                m.Block(b, new Vector3(5f, 1.6f, 5f), stone, light);
                m.Poly(b + Vector3.up * 1.6f, 2.0f, 2.4f, 1.5f, 8, light, dark, 22.5f);
                m.Pipe(b + new Vector3(0.6f, 2.0f, 0f), b + new Vector3(2.4f, 6.2f, 0.8f), 0.45f, 6, P.Rock(AmZone.Workshop, 3), P.Bronze);
                c.Box("Crusher", b + Vector3.up * 1.5f, new Vector3(5f, 3f, 5f));
            }

            // ---- a stone ore cart on two big wheels, and rails along the north wall
            {
                Vector3 b = c.On(-14f, 46.5f);
                m.Box(b + new Vector3(0f, 1.9f, 0f), new Vector3(5f, 1.1f, 3f), stone, light);
                m.Box(b + new Vector3(0f, 2.8f, 0f), new Vector3(4.4f, 0.8f, 2.4f), dark, dark);
                m.Pipe(b + new Vector3(-1.6f, 1.3f, -1.9f), b + new Vector3(-1.6f, 1.3f, 1.9f), 1.3f, 8, P.Rock(AmZone.Workshop, 3), P.Bronze);
                m.Pipe(b + new Vector3(1.6f, 1.3f, -1.9f), b + new Vector3(1.6f, 1.3f, 1.9f), 1.3f, 8, P.Rock(AmZone.Workshop, 3), P.Bronze);
                c.Box("Cart", b + new Vector3(0f, 1.6f, 0f), new Vector3(5.2f, 3.2f, 3.8f));
            }

            m.Beam(c.On(-30f, 44.4f, 0.07f), c.On(8f, 44.4f, 0.07f), 0.25f, 0.14f, P.IronDark);
            m.Beam(c.On(-30f, 45.9f, 0.07f), c.On(8f, 45.9f, 0.07f), 0.25f, 0.14f, P.IronDark);

            // ---- the gantry crane in the north-east: two posts, a beam, a hanging stone block
            {
                m.Block(c.On(-4f, 47f), new Vector3(1.5f, 9f, 1.5f), stone, light);
                m.Block(c.On(4f, 47f), new Vector3(1.5f, 9f, 1.5f), stone, light);
                m.Box(c.On(0f, 47f, 9.4f), new Vector3(10.6f, 1.0f, 1.8f), light, light);
                m.Box(c.On(1f, 47f, 6.4f), new Vector3(0.18f, 4.8f, 0.18f), P.Rope, P.Rope);
                m.Box(c.On(1f, 47f, 3.7f), new Vector3(2.2f, 1.6f, 2.2f), stone, light);
                c.Box("CranePost", c.On(-4f, 47f, 4.5f), new Vector3(1.5f, 9f, 1.5f));
                c.Box("CranePost", c.On(4f, 47f, 4.5f), new Vector3(1.5f, 9f, 1.5f));
            }

            // ---- work benches: slabs on stone legs, a rack of old picks on the wall
            foreach (var bench in new[] { new Vector3(-31.5f, 0f, 35f), new Vector3(-31f, 0f, 46f), new Vector3(-22f, 0f, 48.6f) })
            {
                Vector3 b = c.On(bench.x, bench.z);
                m.Block(b + new Vector3(-1.4f, 0f, 0f), new Vector3(0.6f, 0.9f, 1.4f), stone, stone);
                m.Block(b + new Vector3(1.4f, 0f, 0f), new Vector3(0.6f, 0.9f, 1.4f), stone, stone);
                m.Box(b + new Vector3(0f, 1.05f, 0f), new Vector3(4f, 0.3f, 1.8f), light, light);
                m.Box(b + new Vector3(-0.8f, 1.35f, 0.2f), new Vector3(0.9f, 0.3f, 0.2f), P.Wood, P.Wood);
                c.Box("Bench", b + new Vector3(0f, 0.6f, 0f), new Vector3(4f, 1.2f, 1.8f));
            }

            foreach (float x in new[] { -18f, -8f, 2f })
            {
                Vector3 b = c.On(x, 49.3f);
                m.Box(b + new Vector3(0f, 2.2f, 0f), new Vector3(3.2f, 0.18f, 0.45f), P.Wood, P.Wood);
                m.Box(b + new Vector3(-1.5f, 1.2f, 0f), new Vector3(0.2f, 2.4f, 0.4f), P.WoodDark, P.WoodDark);
                m.Box(b + new Vector3(1.5f, 1.2f, 0f), new Vector3(0.2f, 2.4f, 0.4f), P.WoodDark, P.WoodDark);
                for (int k = 0; k < 3; k++)
                {
                    m.Beam(b + new Vector3(-1.0f + k * 1.0f, 2.1f, -0.1f), b + new Vector3(-0.8f + k * 1.0f, 1.0f, -0.1f), 0.12f, 0.12f, P.Iron);
                }
            }

            // ---- warm lanterns on stone posts (the only warm light in the mine)
            foreach (var p in new[] { new Vector2(-32f, 31.8f), new Vector2(8f, 31.8f), new Vector2(-32f, 48.2f), new Vector2(8f, 48.2f), new Vector2(-12f, 31.8f), new Vector2(-12f, 48.2f) })
            {
                LanternPost(c, p.x, p.y);
            }

            // ---- violet crystals in the corners
            TryCluster(c, 7.6f, 47.4f, 4.4f, 2, 4, 1.1f, 3.5f, 700);
            TryCluster(c, -32.0f, 33.0f, 3.6f, 2, 3, 1.0f, 3f, 701);

            // ---- the partly collapsed west tunnel: heaps from the ceiling's fall narrow it to one lane (5 m clear), the cache chamber behind it
            Rubble(c, -40.3f, 45.1f, 1.3f, 11);
            Cluster(c, -58.0f, 49.0f, 4.0f, 3, 4, 1.1f, 3f, 702);
            Cluster(c, -57.5f, 42.0f, 3.2f, 2, 3, 0.9f, 2.6f, 703);
            Rubble(c, -51.0f, 50.0f, 1.4f, 13);
            OfferingTable(c, -54f, 46f);

            // ---- the collapsed spur: a dead end, sealed by a fall of rock
            Rubble(c, -37.2f, 15.4f, 2.0f, 21);
            Rubble(c, -34.0f, 14.4f, 2.2f, 22);
            Rubble(c, -31.8f, 16.6f, 1.6f, 23);

            // ---- the spur and the tunnel to the caverns: rock spikes from the ceiling
            for (int k = 0; k < 24; k++)
            {
                float x = -33f + Rand(c, k, 80) * 42f, z = 31f + Rand(c, k, 81) * 18f;
                if (AmLayout.EdgeDistance(x, z) < -1.5f)
                {
                    Hanging(c, x, z, 2f + Rand(c, k, 82) * 3f, 2, k % 6 == 0, dark);
                }
            }

            m.Glow = false;
        }

        /// <summary>The offering table of the cache chamber (kept as one small helper so 16B can swap it).</summary>
        private static void OfferingTable(Ctx c, float x, float z)
        {
            Vector3 b = c.On(x, z);
            c.Mesh.Block(b, new Vector3(2.6f, 0.9f, 2.6f), P.Rock(AmZone.Workshop, 4), P.Rock(AmZone.Workshop, 6));
            c.Mesh.Block(b + Vector3.up * 0.9f, new Vector3(1.6f, 0.5f, 1.6f), P.Rock(AmZone.Workshop, 5), P.Gold);
            c.Box("Offering", b + Vector3.up * 0.7f, new Vector3(2.6f, 1.4f, 2.6f));
        }

        // ================================================================ E  ROYAL VAULT

        private static void DressVault(Ctx c)
        {
            AmMesh m = c.Mesh;
            int shaft = P.Rock(AmZone.Vault, 5), plinth = P.Rock(AmZone.Vault, 3), bands = P.Rock(AmZone.Vault, 6);

            // ---- the grand entrance in the east wall: a big half-round arch with gold trim
            Gateway(c, new Vector3(29.4f, 0f, -37f), 90f, 6.2f, 2.2f, 3.2f, 8f, shaft, plinth, true);

            // ---- two rows of four pillars (the alcoves between them stay open), a ring of gold inlay and the processional road
            foreach (int row in new[] { -1, 1 })
            {
                float z = -38f + row * 6.5f;
                foreach (float x in new[] { -5f, 5f, 15f, 24f })
                {
                    Column(c, x, z, 1.2f, 22f, shaft, plinth, bands);
                }

                m.Beam(new Vector3(-6f, 18f, z), new Vector3(26f, 18f, z), 1.6f, 1.6f, P.Rock(AmZone.Vault, 4));
            }

            Ribbon(c, new List<Vector2> { new(41f, -37f), new(28f, -37f), new(10f, -38f) }, 6.6f, P.RoadStone, 0.05f);
            Ribbon(c, new List<Vector2> { new(41f, -37f), new(28f, -37f), new(10f, -38f) }, 0.4f, P.RoadInlay, 0.08f, true);

            // ---- the central dais: a ring inlay and an altar block on its north side (the way across the dais stays free)
            float dais = 1.8f;
            FloorDisc(c, 10f, -38f, 4.0f, P.GoldDark, dais + 0.05f, 24);
            FloorDisc(c, 10f, -38f, 3.4f, P.Rock(AmZone.Vault, 6), dais + 0.06f, 24);
            FloorDisc(c, 10f, -38f, 2.3f, P.Gold, dais + 0.07f, 24);
            FloorDisc(c, 10f, -38f, 1.9f, P.PortalGlowBright, dais + 0.08f, 16, true);
            m.Block(new Vector3(10f, dais, -34.6f), new Vector3(4.4f, 1.2f, 1.8f), plinth, bands);
            m.Block(new Vector3(10f, dais + 1.2f, -34.6f), new Vector3(3.6f, 0.5f, 1.4f), shaft, P.Gold);
            c.Box("Altar", new Vector3(10f, dais + 0.85f, -34.6f), new Vector3(4.4f, 1.7f, 1.8f));

            // ---- the giant crystals: behind the dais, in the corners and over the entrance
            Cluster(c, -6.8f, -38f, 13f, 2, 6, 2.2f, 8f, 800);
            Cluster(c, -5.0f, -30.0f, 9f, 0, 5, 1.8f, 7f, 801);
            Cluster(c, -5.0f, -46.2f, 9f, 3, 5, 1.8f, 7f, 802);
            Cluster(c, 25.8f, -29.6f, 8f, 1, 5, 1.6f, 6f, 803);
            Cluster(c, 25.8f, -46.4f, 8f, 4, 5, 1.6f, 6f, 804);

            // ---- three treasure alcoves in the south wall: a pedestal each, framed in gold (the treasures themselves come in stage 16C)
            foreach (float x in new[] { 0f, 10f, 20f })
            {
                Vector3 b = c.On(x, -50f);
                m.Block(b, new Vector3(3.4f, 0.9f, 2.4f), plinth, bands);
                m.Block(b + Vector3.up * 0.9f, new Vector3(2.4f, 0.4f, 1.6f), shaft, P.Gold);
                c.Box("Pedestal", b + Vector3.up * 0.55f, new Vector3(3.4f, 1.1f, 2.4f));
                Cluster(c, x - 2.5f, -51.4f, 2.4f, (int)(x / 10f) + 1, 3, 0.7f, 0f, 810 + (int)x);
                Cluster(c, x + 2.5f, -51.4f, 2.4f, (int)(x / 10f) + 3, 3, 0.7f, 0f, 820 + (int)x);
            }

            // ---- light from above: big hanging crystals over the dais and the aisles
            for (int k = 0; k < 14; k++)
            {
                Hanging(c, -5f + (k % 5) * 7.5f + Rand(c, k) * 1.5f, -46f + (k / 5) * 8f + Rand(c, k, 1) * 1.5f, 3f + Rand(c, k, 2) * 3.4f, k % 5, true, shaft);
            }

            for (int k = 0; k < 10; k++)
            {
                Hanging(c, -7f + Rand(c, k, 5) * 34f, -48f + Rand(c, k, 6) * 20f, 2f + Rand(c, k, 7) * 2.5f, 0, false, P.Rock(AmZone.Vault, 2));
            }
        }
    }
}
#endif
