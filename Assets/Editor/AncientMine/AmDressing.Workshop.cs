#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    public static partial class AmDressing
    {
        /// <summary>A square stone pier of the workshop: base, shaft, bronze band, capital, and a crossed-pickaxe relief on every face.</summary>
        private static void SquarePier(Ctx c, float x, float z, float size, float height)
        {
            AmMesh m = c.Mesh;
            const AmZone Z = AmZone.Workshop;
            Vector3 b = c.On(x, z);
            int dk = P.Rock(Z, 3), st = P.Rock(Z, 4), lt = P.Rock(Z, 6);
            m.Block(b, V(size * 1.3f, 0.7f, size * 1.3f), dk, st);
            m.Block(b + Vector3.up * 0.7f, V(size, height - 1.9f, size), st, st);
            m.Block(b + Vector3.up * 1.3f, V(size * 1.06f, 0.22f, size * 1.06f), P.Bronze, P.Bronze);
            m.Block(b + Vector3.up * (height - 1.2f), V(size * 1.22f, 0.5f, size * 1.22f), lt, lt);
            m.Block(b + Vector3.up * (height - 0.7f), V(size * 1.4f, 0.7f, size * 1.4f), dk, lt);
            for (int k = 0; k < 4; k++)
            {
                float yaw = k * 90f;
                Vector3 face = Quaternion.Euler(0f, yaw, 0f) * V(0f, 0f, size * 0.5f);
                Relief(c, new WallRef { Origin = b + Vector3.up * 3.6f + face, Yaw = yaw, Lean = 0f }, size * 0.72f, 2.2f, 2, st, P.Bronze, P.GoldBright);
            }

            c.Box("Pier", b + Vector3.up * 4.5f, V(size + 0.6f, 9f, size + 0.6f));
        }

        /// <summary>A timber support frame across a tunnel: two posts, a lintel and two braces. Visual only (the posts stand in the rock skirt at the tunnel's edge).</summary>
        private static void TimberFrame(Ctx c, Vector3 centre, Vector3 axis, float halfWidth, float height)
        {
            AmMesh m = c.Mesh;
            m.Push(centre, Quaternion.LookRotation(axis.normalized, Vector3.up));
            for (int side = -1; side <= 1; side += 2)
            {
                m.Box(V(side * halfWidth, height * 0.5f, 0f), V(0.5f, height, 0.5f), P.WoodAged, P.WoodLight);
                m.Beam(V(side * halfWidth, height - 1.6f, 0f), V(side * (halfWidth - 1.5f), height, 0f), 0.3f, 0.3f, P.WoodAged);
            }

            m.Box(V(0f, height + 0.05f, 0f), V(halfWidth * 2f + 1.0f, 0.5f, 0.6f), P.WoodLight, P.WoodLight);
            m.Pop();
        }

        /// <summary>A hanging warm lamp: a chain and a glowing lantern (emissive trim, no light), high above the walkers.</summary>
        private static void HangingLamp(Ctx c, float x, float z, float drop)
        {
            AmMesh m = c.Mesh;
            float top = AmLayout.CeilingAt(x, z);
            m.Box(V(x, top - drop * 0.5f, z), V(0.08f, drop, 0.08f), P.Iron, P.Iron);
            m.Block(V(x, top - drop - 0.2f, z), V(0.8f, 0.2f, 0.8f), P.Bronze, P.Bronze);
            m.Glow = true;
            m.Box(V(x, top - drop + 0.35f, z), V(0.6f, 0.7f, 0.6f), P.LampWarm, P.LampWarm);
            m.Glow = false;
            m.Poly(V(x, top - drop + 0.7f, z), 0.5f, 0f, 0.4f, 4, P.Bronze, P.Bronze, 45f);
        }

        private static void StoneCrate(Ctx c, float x, float z, float size, float yaw, bool collider = true)
        {
            AmMesh m = c.Mesh;
            Vector3 b = c.On(x, z);
            m.Push(b, yaw);
            m.Block(V(0f, 0f, 0f), V(size, size * 0.85f, size), P.WarmStoneMid, P.WarmStoneLight);
            m.Block(V(0f, size * 0.35f, 0f), V(size * 1.04f, 0.14f, size * 1.04f), P.Bronze, P.Bronze);
            m.Pop();
            if (collider)
            {
                c.Box("Crate", b + Vector3.up * size * 0.43f, V(size, size * 0.85f, size), yaw);
            }
        }

        // ================================================================ D  FORGOTTEN WORKSHOP (floor 8.5): an older, warmer, differently built place

        private static void DressWorkshop(Ctx c)
        {
            AmMesh m = c.Mesh;
            const AmZone Z = AmZone.Workshop;
            const float f = 8.5f;
            int stone = P.Rock(Z, 4), light = P.Rock(Z, 6), dark = P.Rock(Z, 2);

            // ---- the old road continues into the workshop and to the tunnel east; a bronze-and-gold work circle in the middle of the floor
            Ribbon(c, new List<Vector2> { new(0f, 28f), new(0f, 33f), new(-12f, 40f) }, 5.6f, P.WarmStoneLight, 0.05f);
            Ribbon(c, new List<Vector2> { new(-12f, 40f), new(4f, 40f), new(12f, 41f) }, 5.6f, P.WarmStoneLight, 0.05f);
            Ribbon(c, new List<Vector2> { new(0f, 28f), new(0f, 33f), new(-12f, 40f), new(4f, 40f), new(12f, 41f) }, 5.9f, P.Bronze, 0.04f);
            Ribbon(c, new List<Vector2> { new(0f, 28f), new(0f, 33f), new(-12f, 40f), new(4f, 40f), new(12f, 41f) }, 0.4f, P.RoadInlay, 0.1f, true);
            FloorRing(c, -12f, 40f, 2.9f, 3.1f, P.Bronze, 0.07f, 28, false, f);
            FloorRing(c, -12f, 40f, 1.5f, 1.65f, P.GoldBright, 0.08f, 24, false, f);
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI / 2f + Mathf.PI / 4f;
                FloorPolygon(c, P.Bronze, 0.075f, false, f, new Vector2(-12f + Mathf.Cos(a) * 1.7f, 40f + Mathf.Sin(a) * 1.7f), new Vector2(-12f + Mathf.Cos(a - 0.3f) * 2.8f, 40f + Mathf.Sin(a - 0.3f) * 2.8f),
                    new Vector2(-12f + Mathf.Cos(a + 0.3f) * 2.8f, 40f + Mathf.Sin(a + 0.3f) * 2.8f));
            }

            // ---- big square piers holding the roof, each carved with crossed pickaxes (the guild's sign)
            SquarePier(c, -22f, 35f, 2.6f, 14f);
            SquarePier(c, -22f, 45f, 2.6f, 14f);
            SquarePier(c, 5f, 35f, 2.6f, 14f);

            // ---- the stone crusher: a mortar bowl with a crystal in it and a leaning pestle
            {
                Vector3 b = c.On(-28f, 46f);
                m.Block(b, V(5f, 1.6f, 5f), stone, light);
                m.Block(b + Vector3.up * 1.6f, V(5.2f, 0.18f, 5.2f), P.Bronze, P.Bronze);
                m.Poly(b + Vector3.up * 1.6f, 2.0f, 2.4f, 1.5f, 10, light, dark, 18f);
                m.Pipe(b + V(0.6f, 2.0f, 0f), b + V(2.4f, 6.2f, 0.8f), 0.45f, 8, P.Rock(Z, 3), P.Bronze);
                Sprinkle(c, -28.2f, 46f, 1.2f, 3, 4, 810, c.Floor(-28f, 46f) + 1.9f);
                c.Box("Crusher", b + Vector3.up * 1.5f, V(5f, 3f, 5f));
            }

            // ---- the ore wagon: a carved stone body on two big wheels, loaded with raw crystal, on rails with stone sleepers
            {
                Vector3 b = c.On(-14f, 46.5f);
                m.Box(b + V(0f, 1.9f, 0f), V(5f, 1.1f, 3f), stone, light);
                m.Box(b + V(0f, 2.7f, 0f), V(4.4f, 0.6f, 2.4f), dark, dark);
                m.Block(b + V(0f, 1.2f, 1.52f), V(4.6f, 0.2f, 0.1f), P.Bronze, P.Bronze);
                CrystalSign(m, b + V(0f, 1.9f, 1.55f), 0f, 0.9f, P.Gold, P.GoldBright);
                m.Pipe(b + V(-1.6f, 1.3f, -1.9f), b + V(-1.6f, 1.3f, 1.9f), 1.3f, 10, P.Rock(Z, 3), P.Bronze);
                m.Pipe(b + V(1.6f, 1.3f, -1.9f), b + V(1.6f, 1.3f, 1.9f), 1.3f, 10, P.Rock(Z, 3), P.Bronze);
                Sprinkle(c, -14f, 46.5f, 1.5f, 0, 5, 811, b.y + 3.0f);
                c.Box("Cart", b + V(0f, 1.6f, 0f), V(5.2f, 3.2f, 3.8f));
            }

            m.Beam(c.On(-30f, 44.4f, 0.07f), c.On(8f, 44.4f, 0.07f), 0.25f, 0.14f, P.IronDark);
            m.Beam(c.On(-30f, 45.9f, 0.07f), c.On(8f, 45.9f, 0.07f), 0.25f, 0.14f, P.IronDark);
            for (float x = -29f; x < 8f; x += 2.8f)
            {
                m.Block(c.On(x, 45.15f), V(0.5f, 0.1f, 2.4f), P.WarmStoneDark, P.WarmStoneDark);
            }

            // ---- the gantry crane in the north-east: stone posts, a timber beam, a pulley, a raw crystal block hanging on a rope
            {
                m.Block(c.On(-4f, 47f), V(1.5f, 9f, 1.5f), stone, light);
                m.Block(c.On(4f, 47f), V(1.5f, 9f, 1.5f), stone, light);
                m.Block(c.On(-4f, 47f), V(2.2f, 0.6f, 2.2f), dark, stone);
                m.Block(c.On(4f, 47f), V(2.2f, 0.6f, 2.2f), dark, stone);
                m.Box(c.On(0f, 47f, 9.4f), V(10.6f, 1.0f, 1.8f), P.WoodLight, P.WoodLight);
                m.Poly(c.On(1f, 47f, 8.4f), 0.55f, 0.55f, 0.5f, 8, P.Bronze, P.Bronze, 0f);
                m.Box(c.On(1f, 47f, 6.4f), V(0.18f, 4.8f, 0.18f), P.Rope, P.Rope);
                m.Box(c.On(1f, 47f, 3.7f), V(2.2f, 1.6f, 2.2f), stone, light);
                Sprinkle(c, 1f, 47f, 1.5f, 2, 4, 812, c.Floor(1f, 47f) + 5.3f);
                c.Box("CranePost", c.On(-4f, 47f, 4.5f), V(1.5f, 9f, 1.5f));
                c.Box("CranePost", c.On(4f, 47f, 4.5f), V(1.5f, 9f, 1.5f));
            }

            // ---- work benches: carved slabs on stone legs with tools and crystal chunks
            foreach (var bench in new[] { new Vector3(-31.5f, 0f, 35f), new Vector3(-31f, 0f, 46f), new Vector3(-22f, 0f, 48.6f) })
            {
                Vector3 b = c.On(bench.x, bench.z);
                m.Block(b + V(-1.4f, 0f, 0f), V(0.6f, 0.9f, 1.4f), stone, stone);
                m.Block(b + V(1.4f, 0f, 0f), V(0.6f, 0.9f, 1.4f), stone, stone);
                m.Box(b + V(0f, 1.05f, 0f), V(4f, 0.3f, 1.8f), light, light);
                m.Box(b + V(0f, 1.25f, 0.8f), V(4.1f, 0.1f, 0.12f), P.Bronze, P.Bronze);
                m.Beam(b + V(-1.4f, 1.3f, -0.2f), b + V(-0.4f, 1.3f, 0.2f), 0.12f, 0.12f, P.Wood);
                m.Box(b + V(-0.4f, 1.42f, 0.2f), V(0.4f, 0.14f, 0.22f), P.Iron, P.Iron);
                Sprinkle(c, b.x + 1.0f, b.z, 0.55f, 0, 3, 820 + (int)bench.x, b.y + 1.2f);
                c.Box("Bench", b + V(0f, 0.6f, 0f), V(4f, 1.2f, 1.8f));
            }

            // ---- two crystal-working platforms (round, 0.22 m: walked over) with a raw crystal on a stone stand, and a gold ring
            foreach (var p in new[] { new Vector2(-17f, 33.5f), new Vector2(-8.5f, 31.9f) })
            {
                float g = c.Floor(p.x, p.y);
                m.Poly(V(p.x, g, p.y), 2.7f, 2.55f, 0.22f, 12, stone, light, 0f);
                FloorRing(c, p.x, p.y, 1.75f, 1.9f, P.GoldBright, 0.25f, 24, false, g);
                FloorRing(c, p.x, p.y, 2.2f, 2.3f, P.Bronze, 0.25f, 24, false, g);
                m.Poly(V(p.x, g + 0.22f, p.y), 0.8f, 0.6f, 0.9f, 8, dark, stone, 0f);
                Formation(c, p.x, p.y, 2.5f, p.x < -12f ? 0 : 3, 4, 0.55f, 2.0f, 830 + (int)p.x, -1, false, g + 1.1f);
            }

            // ---- storage corner in the north-east: stone crates and urns
            StoneCrate(c, 7.4f, 45.2f, 1.6f, 10f);
            StoneCrate(c, 8.6f, 47.2f, 1.5f, 35f);
            StoneCrate(c, 7.6f, 45.2f, 1.1f, 40f, false);
            Vector3 urn = c.On(6.2f, 48.6f);
            m.Poly(urn, 0.5f, 0.75f, 0.7f, 8, P.WarmStoneMid, P.WarmStoneMid, 0f);
            m.Poly(urn + Vector3.up * 0.7f, 0.75f, 0.45f, 0.6f, 8, P.WarmStoneLight, P.WarmStoneLight, 0f);
            c.Capsule("Urn", urn, 0.7f, 1.3f);

            // ---- racks of old picks along the north wall
            foreach (float x in new[] { -18f, -8f, 2f })
            {
                Vector3 b = c.On(x, 49.3f);
                m.Box(b + V(0f, 2.2f, 0f), V(3.2f, 0.18f, 0.45f), P.WoodLight, P.WoodLight);
                m.Box(b + V(-1.5f, 1.2f, 0f), V(0.2f, 2.4f, 0.4f), P.WoodAged, P.WoodAged);
                m.Box(b + V(1.5f, 1.2f, 0f), V(0.2f, 2.4f, 0.4f), P.WoodAged, P.WoodAged);
                for (int k = 0; k < 3; k++)
                {
                    m.Beam(b + V(-1.0f + k * 1.0f, 2.1f, -0.1f), b + V(-0.8f + k * 1.0f, 1.0f, -0.1f), 0.12f, 0.12f, P.Iron);
                }
            }

            // ---- the walls: the guild's pickaxe-and-crystal crests and a bronze frieze (a mining people's signs)
            foreach (float x in new[] { -26f, -13f, 0f })
            {
                Relief(c, Wall("Workshop", 2, x, f + 6.5f), 5.2f, 4f, x == -13f ? 0 : 2, stone, P.Bronze, P.GoldBright);
            }

            foreach (float x in new[] { -24f, -4f })
            {
                Relief(c, Wall("Workshop", 3, x, f + 6.5f), 4.4f, 3.6f, 2, stone, P.Bronze, P.GoldBright);
            }

            foreach (float z in new[] { 34.5f, 46f })
            {
                Relief(c, Wall("Workshop", 0, z, f + 6.5f), 3.2f, 3.6f, 0, stone, P.Bronze, P.GoldBright);
            }

            Frieze(c, "Workshop", 2, -30f, 6f, f + 9.6f, 0.8f, P.Bronze, P.GoldBright);
            Frieze(c, "Workshop", 3, -30f, -6f, f + 9.6f, 0.8f, P.Bronze, P.GoldBright);

            // ---- warm lamps: stone posts with lanterns at the corners and the doors, lanterns hanging from the roof over the middle
            foreach (var p in new[] { new Vector2(-32f, 31.8f), new Vector2(8f, 31.8f), new Vector2(-32f, 48.2f), new Vector2(8f, 48.2f), new Vector2(-12f, 31.8f), new Vector2(-12f, 48.2f) })
            {
                LanternPost(c, p.x, p.y);
            }

            foreach (var p in new[] { new Vector2(-28f, 39f), new Vector2(-19f, 41f), new Vector2(-5f, 38f), new Vector2(4f, 41.5f), new Vector2(-12f, 44f), new Vector2(-12f, 36f) })
            {
                HangingLamp(c, p.x, p.y, 4.2f);
            }

            TryFormation(c, 7.6f, 47.4f, 4.4f, 2, 4, 1.1f, 3.5f, 700);
            TryFormation(c, -32.0f, 33.0f, 3.6f, 2, 3, 1.0f, 3f, 701);

            // ---- the partly collapsed west tunnel: timber supports, a rubble slump along one wall (the lane stays 5 m wide), shards on the floor, the cache chamber behind it
            TimberFrame(c, c.On(-36.4f, 40.8f), new Vector3(-0.96f, 0f, 0.27f), 3.3f, 6.2f);
            TimberFrame(c, c.On(-43.4f, 42.5f), new Vector3(-0.96f, 0f, 0.27f), 3.3f, 6.2f);
            Rubble(c, -40.3f, 45.1f, 1.3f, 11, true, true);
            Rubble(c, -46.5f, 39.6f, 0.9f, 14, false, true);
            m.Beam(c.On(-39.2f, 38.2f, 0.3f), c.On(-41.8f, 38.6f, 0.9f), 0.4f, 0.35f, P.WoodAged);
            Sprinkle(c, -39.5f, 38.6f, 1.0f, 3, 4, 840);
            Sprinkle(c, -47f, 46.5f, 1.0f, 0, 4, 841);
            Scraps(c, -42f, 42f, 8, 5f, 842, 3);
            Formation(c, -58.0f, 49.0f, 4.0f, 3, 4, 1.1f, 3f, 702);
            Formation(c, -57.5f, 42.0f, 3.2f, 2, 3, 0.9f, 2.6f, 703);
            Rubble(c, -51.0f, 50.0f, 1.4f, 13, true, true);
            // the cache: an offering table with a gold ring round it, two stone coffers with gold bands
            {
                Vector3 b = c.On(-54f, 46f);
                m.Block(b, V(2.6f, 0.9f, 2.6f), P.Rock(Z, 4), P.Rock(Z, 6));
                m.Block(b + Vector3.up * 0.9f, V(1.6f, 0.5f, 1.6f), P.Rock(Z, 5), P.Gold);
                c.Box("Offering", b + Vector3.up * 0.7f, V(2.6f, 1.4f, 2.6f));
                FloorRing(c, -54f, 46f, 2.1f, 2.25f, P.GoldBright, 0.09f, 24, false, b.y);
                foreach (var cp in new[] { new Vector3(-57f, 0f, 44.4f), new Vector3(-56.2f, 0f, 48.4f) })
                {
                    Vector3 cb = c.On(cp.x, cp.z);
                    m.Block(cb, V(1.8f, 1.0f, 1.1f), P.WarmStoneMid, P.WarmStoneLight);
                    m.Block(cb + Vector3.up * 0.55f, V(1.85f, 0.14f, 1.15f), P.Gold, P.GoldBright);
                    c.Box("Coffer", cb + Vector3.up * 0.55f, V(1.8f, 1.1f, 1.1f));
                }
            }

            // ---- the collapsed spur: sealed by a fall of rock, timbers and red flags: the dead end can not be mistaken for a way on
            Rubble(c, -37.2f, 15.4f, 2.0f, 21, true, true);
            Rubble(c, -34.0f, 14.4f, 2.2f, 22, true, true);
            Rubble(c, -31.8f, 16.6f, 1.6f, 23, true, true);
            Rubble(c, -30.6f, 19.0f, 1.4f, 24, true, true);
            Rubble(c, -37.6f, 19.2f, 1.5f, 25, true, true);
            m.Beam(c.On(-37f, 22.0f, 1.0f), c.On(-30f, 22.6f, 2.4f), 0.3f, 0.3f, P.WoodAged);
            m.Beam(c.On(-37f, 22.0f, 2.4f), c.On(-30f, 22.6f, 1.0f), 0.3f, 0.3f, P.WoodAged);
            foreach (var p in new[] { new Vector2(-36.3f, 22.0f), new Vector2(-29.7f, 22.6f) })
            {
                Vector3 b = c.On(p.x, p.y);
                m.Block(b, V(0.14f, 3.0f, 0.14f), P.WoodAged, P.WoodAged);
                m.Box(b + V(0.5f, 2.5f, 0f), V(0.95f, 0.6f, 0.05f), P.ClothRed, P.ClothRed);
            }

            // ---- roof: warm rock spikes
            for (int k = 0; k < 24; k++)
            {
                float x = -33f + Rand(c, k, 80) * 42f, z = 31f + Rand(c, k, 81) * 18f;
                if (AmLayout.EdgeDistance(x, z) < -1.5f)
                {
                    Hanging(c, x, z, 2f + Rand(c, k, 82) * 3f, 2, false, dark);
                }
            }

            m.Glow = false;
        }
    }
}
#endif
