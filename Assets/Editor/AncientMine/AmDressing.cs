#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>
    /// The look of the five areas (stage 16B): architecture (pillars, arches, gateways, statues, reliefs, friezes, floor inlays), crystal formations and the colliders that make them solid.
    /// MODULAR: <see cref="DressZone"/> clears only that area's "Props" and "Colliders" children and refills them (the cave shell, the routes and markers, the other areas and the treasure groups
    /// stay as they are). Per area ONE merged "Structures" mesh (lit palette + a little emissive trim) and ONE merged "Crystals" mesh (emissive only, its own renderer). Colliders only where the player
    /// or the camera can really touch something; ornaments high above the walkway, reliefs and floor inlays have none.
    /// </summary>
    public static partial class AmDressing
    {
        public const string PropsName = "Props";
        public const string CollidersName = "Colliders";

        public static readonly string[] ZoneNames = { "A_AncientGate", "B_HallOfPillars", "C_CrystalCaverns", "D_ForgottenWorkshop", "E_RoyalVault" };

        public static AmZone ZoneOf(string zoneName)
        {
            return zoneName switch
            {
                "A_AncientGate" => AmZone.Gate,
                "B_HallOfPillars" => AmZone.Hall,
                "C_CrystalCaverns" => AmZone.Crystal,
                "D_ForgottenWorkshop" => AmZone.Workshop,
                "E_RoyalVault" => AmZone.Vault,
                _ => AmZone.None,
            };
        }

        /// <summary>The state of one area while it is being dressed.</summary>
        private sealed class Ctx
        {
            public Transform Zone, Colliders;
            public AmZone Id;
            public readonly AmMesh Mesh = new();      // architecture (lit) and small emissive trim
            public readonly AmMesh Crystals = new();  // crystals only (emissive)
            public int Count;
            public int Seed;
            public int Formations;

            public float Floor(float x, float z) => AmLayout.Surface(x, z);

            public Vector3 On(float x, float z, float lift = 0f) => new(x, AmLayout.Surface(x, z) + lift, z);

            public GameObject Collider(string name, Vector3 position, Quaternion rotation)
            {
                var go = new GameObject($"{name}_{Count++:00}");
                go.transform.SetParent(Colliders, false);
                go.transform.SetPositionAndRotation(position, rotation);
                return go;
            }

            public void Box(string name, Vector3 centre, Vector3 size, float yaw = 0f)
            {
                GameObject go = Collider(name, centre, Quaternion.Euler(0f, yaw, 0f));
                go.AddComponent<BoxCollider>().size = size;
            }

            public void Capsule(string name, Vector3 bottom, float radius, float height)
            {
                GameObject go = Collider(name, bottom + Vector3.up * height * 0.5f, Quaternion.identity);
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.radius = radius;
                capsule.height = Mathf.Max(height, radius * 2f);
            }

            public void Sphere(string name, Vector3 centre, float radius)
            {
                GameObject go = Collider(name, centre, Quaternion.identity);
                go.AddComponent<SphereCollider>().radius = radius;
            }
        }

        // ================================================================ entry points

        public static string DressAll(Transform env)
        {
            var sb = new System.Text.StringBuilder();
            foreach (string zone in ZoneNames)
            {
                sb.AppendLine(DressZone(env, zone));
            }

            return sb.ToString();
        }

        public static string DressZone(Transform env, string zoneName)
        {
            Transform zone = env.Find(zoneName);
            if (zone == null)
            {
                return "zone not found: " + zoneName;
            }

            foreach (string child in new[] { PropsName, CollidersName })
            {
                Transform old = zone.Find(child);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            AmLayout.ResetGrid();
            var ctx = new Ctx { Zone = zone, Id = ZoneOf(zoneName), Colliders = new GameObject(CollidersName).transform, Seed = (zoneName.GetHashCode() & 0xFFFF) + 17 };
            ctx.Colliders.SetParent(zone, false);
            switch (ctx.Id)
            {
                case AmZone.Gate:
                    DressGate(ctx);
                    break;
                case AmZone.Hall:
                    DressHall(ctx);
                    break;
                case AmZone.Crystal:
                    DressCaverns(ctx);
                    break;
                case AmZone.Workshop:
                    DressWorkshop(ctx);
                    break;
                case AmZone.Vault:
                    DressVault(ctx);
                    break;
            }

            Material lit = AssetDatabase.LoadAssetAtPath<Material>(AmAssets.MaterialFolder + "/AM_Palette.mat");
            Material glow = AssetDatabase.LoadAssetAtPath<Material>(AmAssets.MaterialFolder + "/AM_Glow.mat");
            Transform props = new GameObject(PropsName).transform;
            props.SetParent(zone, false);

            var structures = new GameObject("Structures");
            structures.transform.SetParent(props, false);
            structures.AddComponent<MeshFilter>().sharedMesh = ctx.Mesh.ToMesh($"AM_{ctx.Id}_Structures", AmAssets.DressingFolder);
            structures.AddComponent<MeshRenderer>().sharedMaterials = ctx.Mesh.HasGlow ? new[] { lit, glow } : new[] { lit };
            GameObjectUtility.SetStaticEditorFlags(structures, StaticEditorFlags.BatchingStatic);

            int crystalTriangles = 0;
            if (!ctx.Crystals.IsEmpty)
            {
                var crystals = new GameObject("Crystals");
                crystals.transform.SetParent(props, false);
                crystals.AddComponent<MeshFilter>().sharedMesh = ctx.Crystals.ToMesh($"AM_{ctx.Id}_Crystals", AmAssets.DressingFolder);
                crystals.AddComponent<MeshRenderer>().sharedMaterials = ctx.Crystals.IsGlowOnly ? new[] { glow } : new[] { lit, glow };
                GameObjectUtility.SetStaticEditorFlags(crystals, StaticEditorFlags.BatchingStatic);
                crystalTriangles = ctx.Crystals.Triangles;
            }

            return $"{zoneName}: {ctx.Mesh.Triangles} triangles in Structures, {crystalTriangles} in Crystals ({ctx.Formations} formations), {ctx.Colliders.childCount} colliders";
        }

        // ================================================================ shared basics

        private static float Rand(Ctx c, int a, int b = 0)
        {
            float s = Mathf.Sin((a * 12.9898f + b * 78.233f + c.Seed * 0.37f) * 3.1f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        /// <summary>A flat ribbon lying on the floor along a polyline (the old stone road, inlays). <paramref name="lift"/> is above the floor mesh.</summary>
        private static void Ribbon(Ctx c, IList<Vector2> points, float width, int color, float lift, bool glow = false, float step = 1.5f)
        {
            AmMesh m = c.Mesh;
            bool previous = m.Glow;
            m.Glow = glow;
            var centres = new List<Vector2>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                float length = Vector2.Distance(points[i], points[i + 1]);
                int n = Mathf.Max(1, Mathf.CeilToInt(length / step));
                for (int k = 0; k < n; k++)
                {
                    centres.Add(Vector2.Lerp(points[i], points[i + 1], k / (float)n));
                }
            }

            centres.Add(points[points.Count - 1]);
            // mitred corners: the side vector at every centre point follows the average direction of its two segments, so bends leave no gap and no overlap
            int n2 = centres.Count;
            var left = new Vector2[n2];
            var right = new Vector2[n2];
            for (int i = 0; i < n2; i++)
            {
                Vector2 d0 = (centres[Mathf.Min(i + 1, n2 - 1)] - centres[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 dPrev = i > 0 ? (centres[i] - centres[i - 1]).normalized : d0;
                Vector2 side = new Vector2(-d0.y, d0.x);
                float scale = 1f / Mathf.Max(0.55f, Vector2.Dot(side, new Vector2(-dPrev.y, dPrev.x)));
                left[i] = centres[i] - side * width * 0.5f * scale;
                right[i] = centres[i] + side * width * 0.5f * scale;
            }

            for (int i = 0; i < n2 - 1; i++)
            {
                Vector3 p0 = c.On(left[i].x, left[i].y, lift), p1 = c.On(right[i].x, right[i].y, lift);
                Vector3 p2 = c.On(right[i + 1].x, right[i + 1].y, lift), p3 = c.On(left[i + 1].x, left[i + 1].y, lift);
                m.FaceWorld(color, Vector3.up, p0, p1, p2, p3);
            }

            m.Glow = previous;
        }

        /// <summary>A flat disc lying on a floor of constant height.</summary>
        private static void FloorDisc(Ctx c, float x, float z, float radius, int color, float lift, int sides = 24, bool glow = false, float groundY = float.NaN)
        {
            AmMesh m = c.Mesh;
            bool previous = m.Glow;
            m.Glow = glow;
            float y = (float.IsNaN(groundY) ? c.Floor(x, z) : groundY) + lift;
            var pts = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = -k / (float)sides * Mathf.PI * 2f;
                pts[k] = new Vector3(x + Mathf.Cos(a) * radius, y, z + Mathf.Sin(a) * radius);
            }

            m.FaceWorld(color, Vector3.up, pts);
            m.Glow = previous;
        }

        /// <summary>A flat ring (annulus) lying on a floor of constant height.</summary>
        private static void FloorRing(Ctx c, float x, float z, float r0, float r1, int color, float lift, int sides = 28, bool glow = false, float groundY = float.NaN, float arcStart = 0f, float arcDegrees = 360f)
        {
            AmMesh m = c.Mesh;
            bool previous = m.Glow;
            m.Glow = glow;
            float y = (float.IsNaN(groundY) ? c.Floor(x, z) : groundY) + lift;
            for (int k = 0; k < sides; k++)
            {
                float a0 = (arcStart + arcDegrees * k / sides) * Mathf.Deg2Rad, a1 = (arcStart + arcDegrees * (k + 1) / sides) * Mathf.Deg2Rad;
                Vector3 p0 = new(x + Mathf.Cos(a0) * r0, y, z + Mathf.Sin(a0) * r0), p1 = new(x + Mathf.Cos(a0) * r1, y, z + Mathf.Sin(a0) * r1);
                Vector3 p2 = new(x + Mathf.Cos(a1) * r1, y, z + Mathf.Sin(a1) * r1), p3 = new(x + Mathf.Cos(a1) * r0, y, z + Mathf.Sin(a1) * r0);
                m.FaceWorld(color, Vector3.up, p0, p1, p2, p3);
            }

            m.Glow = previous;
        }

        /// <summary>A flat convex polygon lying on the floor (diamonds, stars, triangles of the floor symbols).</summary>
        private static void FloorPolygon(Ctx c, int color, float lift, bool glow, float groundY, params Vector2[] points)
        {
            AmMesh m = c.Mesh;
            bool previous = m.Glow;
            m.Glow = glow;
            var pts = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                pts[i] = new Vector3(points[i].x, groundY + lift, points[i].y);
            }

            m.FaceWorld(color, Vector3.up, pts);
            m.Glow = previous;
        }

        /// <summary>A sun symbol on the floor: a ring, a disc, rays (gold) and an emissive core when asked (coreGlow >= 0).</summary>
        private static void FloorSun(Ctx c, float x, float z, float radius, float groundY, int gold, int coreGlow, int rays = 12)
        {
            FloorRing(c, x, z, radius * 0.78f, radius * 0.88f, gold, 0.07f, 28, false, groundY);
            FloorDisc(c, x, z, radius * 0.5f, gold, 0.075f, 20, false, groundY);
            if (coreGlow >= 0)
            {
                FloorDisc(c, x, z, radius * 0.3f, coreGlow, 0.085f, 16, true, groundY);
            }

            for (int k = 0; k < rays; k++)
            {
                float a = k * Mathf.PI * 2f / rays;
                float a0 = a - 0.11f, a1 = a + 0.11f;
                FloorPolygon(c, gold, 0.07f, false, groundY,
                    new Vector2(x + Mathf.Cos(a0) * radius * 0.92f, z + Mathf.Sin(a0) * radius * 0.92f),
                    new Vector2(x + Mathf.Cos(a) * radius * 1.22f, z + Mathf.Sin(a) * radius * 1.22f),
                    new Vector2(x + Mathf.Cos(a1) * radius * 0.92f, z + Mathf.Sin(a1) * radius * 0.92f));
            }
        }

        /// <summary>A free-standing arch: a half-round ring on two piers, across a passage that runs along local z of <paramref name="yaw"/>.</summary>
        private static void Gateway(Ctx c, Vector3 baseCentre, float yaw, float innerRadius, float ringThickness, float depth, float springHeight, int stone, int alt, bool pierColliders)
        {
            AmMesh m = c.Mesh;
            float outer = innerRadius + ringThickness;
            m.Push(baseCentre, yaw);
            m.Push(new Vector3(0f, springHeight, 0f), Quaternion.identity);
            m.ArchRing(Vector3.zero, innerRadius, outer, depth, 10, stone, alt);
            m.Pop();
            for (int side = -1; side <= 1; side += 2)
            {
                float px = side * (innerRadius + ringThickness * 0.5f);
                m.Block(new Vector3(px, 0f, 0f), new Vector3(ringThickness, springHeight, depth), stone, alt);
                m.Block(new Vector3(px, 0f, 0f), new Vector3(ringThickness * 1.3f, 0.7f, depth * 1.2f), alt, alt);
            }

            m.Pop();
            if (pierColliders)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 local = new(side * (innerRadius + ringThickness * 0.5f), springHeight * 0.5f, 0f);
                    Vector3 world = baseCentre + Quaternion.Euler(0f, yaw, 0f) * local;
                    c.Box("ArchPier", world, new Vector3(ringThickness * 1.15f, springHeight, depth * 1.1f), yaw);
                }
            }
        }

        /// <summary>
        /// A gateway with a carved look: the stone arch of <see cref="Gateway"/> plus a thin gold trim ring on its front and back, a gold keystone, gold bands on the piers and a crystal sign on each
        /// pier face. Same footprint and colliders as the plain gateway.
        /// </summary>
        private static void OrnateGateway(Ctx c, Vector3 baseCentre, float yaw, float innerRadius, float ringThickness, float depth, float springHeight, int stone, int alt, int gold, int goldBright, bool colliders = true)
        {
            AmMesh m = c.Mesh;
            Gateway(c, baseCentre, yaw, innerRadius, ringThickness, depth, springHeight, stone, alt, colliders);
            float outer = innerRadius + ringThickness;
            m.Push(baseCentre, yaw);
            for (int face = -1; face <= 1; face += 2)
            {
                m.Push(new Vector3(0f, springHeight, face * (depth * 0.5f + 0.03f)), Quaternion.identity);
                m.ArchRing(Vector3.zero, innerRadius + 0.12f, innerRadius + 0.42f, 0.08f, 10, gold, goldBright);   // gold trim on the inner edge
                m.ArchRing(Vector3.zero, outer - 0.42f, outer - 0.12f, 0.08f, 10, gold, goldBright);              // and on the outer edge
                m.Pop();
            }

            m.Block(new Vector3(0f, springHeight + outer - 0.15f, 0f), new Vector3(1.4f, 1.1f, depth * 1.05f), goldBright, gold);   // keystone
            m.Block(new Vector3(0f, springHeight + innerRadius - 0.1f, 0f), new Vector3(0.9f, 0.9f, depth * 1.08f), gold, goldBright);
            for (int side = -1; side <= 1; side += 2)
            {
                float px = side * (innerRadius + ringThickness * 0.5f);
                m.Block(new Vector3(px, springHeight - 0.5f, 0f), new Vector3(ringThickness * 1.12f, 0.35f, depth * 1.12f), gold, goldBright);
                m.Block(new Vector3(px, 0.7f, 0f), new Vector3(ringThickness * 1.18f, 0.25f, depth * 1.18f), gold, goldBright);
                for (int face = -1; face <= 1; face += 2)
                {
                    CrystalSign(m, new Vector3(px, springHeight * 0.45f, face * (depth * 0.5f + 0.04f)), face > 0 ? 0f : 180f, ringThickness * 0.7f, gold, goldBright);
                }
            }

            m.Pop();
        }

        // ================================================================ walls: where a rectangular room's wall really is at a given height

        /// <summary>A point on a wall surface (the hall walls lean outwards over their height), the yaw that makes a panel face into the room and the lean to match the wall.</summary>
        private struct WallRef
        {
            public Vector3 Origin;
            public float Yaw;
            public float Lean;
        }

        /// <summary>
        /// <paramref name="side"/>: 0 = the +x wall, 1 = the -x wall, 2 = the +z wall, 3 = the -z wall of the rectangular room <paramref name="shapeName"/>;
        /// <paramref name="along"/> is the coordinate along the wall (z for the x walls, x for the z walls); <paramref name="y"/> the world height.
        /// </summary>
        private static WallRef Wall(string shapeName, int side, float along, float y)
        {
            AmLayout.Shape s = null;
            foreach (AmLayout.Shape candidate in AmLayout.Shapes)
            {
                if (candidate.IsRect && candidate.Name == shapeName)
                {
                    s = candidate;
                }
            }

            float height = s.CeilAbs - s.Floor;
            float lean = Mathf.Atan(s.Run / height) * Mathf.Rad2Deg;
            float d = s.Run * Mathf.Clamp01((y - s.Floor) / height);
            var w = new WallRef { Lean = lean };
            switch (side)
            {
                case 0:
                    w.Origin = new Vector3(s.Centre.x + s.Half.x + d, y, along);
                    w.Yaw = -90f;
                    break;
                case 1:
                    w.Origin = new Vector3(s.Centre.x - s.Half.x - d, y, along);
                    w.Yaw = 90f;
                    break;
                case 2:
                    w.Origin = new Vector3(along, y, s.Centre.y + s.Half.y + d);
                    w.Yaw = 180f;
                    break;
                default:
                    w.Origin = new Vector3(along, y, s.Centre.y - s.Half.y - d);
                    w.Yaw = 0f;
                    break;
            }

            return w;
        }

        /// <summary>The local frame of a wall panel: origin on the wall, +z into the room, +y up (leaning back with the wall), +x along the wall.</summary>
        private static void PushWall(AmMesh m, WallRef w)
        {
            m.Push(w.Origin, Quaternion.Euler(0f, w.Yaw, 0f) * Quaternion.Euler(-w.Lean, 0f, 0f));
        }
    }
}
#endif
