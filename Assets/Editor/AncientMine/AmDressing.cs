#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>
    /// Stage 16A placeholders for the five areas: big architecture (pillars, arches, the great portal, platforms), simple crystal formations, and the colliders that make them solid.
    /// MODULAR: <see cref="DressZone"/> clears only that area's "Props" and "Colliders" children and refills them (the cave shell, the other areas and the treasure groups stay as they are),
    /// so stage 16B can rework one area at a time. Props of an area are merged into ONE mesh ("Structures": lit palette + emissive submesh). Colliders are only added where the player
    /// or the camera can really touch something; ornaments high above the walkway have none.
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
            public readonly AmMesh Mesh = new();
            public int Count;
            public int Seed;

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
            var ctx = new Ctx { Zone = zone, Id = ZoneOf(zoneName), Colliders = new GameObject(CollidersName).transform, Seed = zoneName.GetHashCode() & 0xFFFF };
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

            Transform props = new GameObject(PropsName).transform;
            props.SetParent(zone, false);
            var structures = new GameObject("Structures");
            structures.transform.SetParent(props, false);
            Mesh mesh = ctx.Mesh.ToMesh($"AM_{ctx.Id}_Structures", AmAssets.DressingFolder);
            structures.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = structures.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = ctx.Mesh.HasGlow
                ? new[] { AssetDatabase.LoadAssetAtPath<Material>(AmAssets.MaterialFolder + "/AM_Palette.mat"), AssetDatabase.LoadAssetAtPath<Material>(AmAssets.MaterialFolder + "/AM_Glow.mat") }
                : new[] { AssetDatabase.LoadAssetAtPath<Material>(AmAssets.MaterialFolder + "/AM_Palette.mat") };
            GameObjectUtility.SetStaticEditorFlags(structures, StaticEditorFlags.BatchingStatic);
            return $"{zoneName}: {ctx.Mesh.Triangles} triangles in Structures, {ctx.Colliders.childCount} colliders";
        }

        // ================================================================ shared building blocks

        private static float Rand(Ctx c, int a, int b = 0)
        {
            float s = Mathf.Sin((a * 12.9898f + b * 78.233f + c.Seed * 0.37f) * 3.1f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        /// <summary>A free-standing column: plinth, shaft (tapered n-gon), two ring bands and a capital. Its collider is a capsule as tall as the camera can reach, not the whole shaft.</summary>
        private static void Column(Ctx c, float x, float z, float radius, float height, int shaft, int plinth, int bands, bool collider = true, float groundY = float.NaN, int sides = 8)
        {
            AmMesh m = c.Mesh;
            Vector3 b = new(x, float.IsNaN(groundY) ? c.Floor(x, z) : groundY, z);
            m.Block(b, new Vector3(radius * 2.7f, 0.7f, radius * 2.7f), plinth, bands);
            m.Poly(b + Vector3.up * 0.7f, radius, radius * 0.9f, height - 1.9f, sides, shaft, shaft, 22.5f);
            for (int k = 1; k <= 2; k++)
            {
                m.Poly(b + Vector3.up * (0.7f + (height - 1.9f) * k / 3f), radius * 1.12f, radius * 1.12f, 0.35f, sides, bands, bands, 22.5f);
            }

            m.Block(b + Vector3.up * (height - 1.2f), new Vector3(radius * 3.0f, 1.2f, radius * 3.0f), plinth, bands);
            if (collider)
            {
                c.Capsule("Column", b, radius * 1.05f, Mathf.Min(height, 9f));
            }
        }

        /// <summary>A broken column: a stump with a ragged top and a fallen drum beside it.</summary>
        private static void BrokenColumn(Ctx c, float x, float z, float radius, float height, float drumYaw, int body, int top, bool drum = true)
        {
            AmMesh m = c.Mesh;
            Vector3 b = c.On(x, z);
            m.Block(b, new Vector3(radius * 2.7f, 0.6f, radius * 2.7f), body, top);
            m.Poly(b + Vector3.up * 0.6f, radius, radius * 0.93f, height - 0.6f, 7, body, top, 10f);
            // the ragged break: a shard rising from the top
            m.Poly(b + Vector3.up * (height) + new Vector3(radius * 0.3f, 0f, 0f), radius * 0.55f, radius * 0.1f, 0.9f, 5, body, top, 0f);
            c.Capsule("BrokenColumn", b, radius * 1.05f, Mathf.Min(height, 5f));
            if (drum)
            {
                Vector3 d = new(x + Mathf.Cos(drumYaw * Mathf.Deg2Rad) * (radius * 3.2f), 0f, z + Mathf.Sin(drumYaw * Mathf.Deg2Rad) * (radius * 3.2f));
                d.y = c.Floor(d.x, d.z);
                Vector3 axis = new(Mathf.Cos(drumYaw * Mathf.Deg2Rad), 0f, Mathf.Sin(drumYaw * Mathf.Deg2Rad));
                Vector3 centre = d + Vector3.up * radius * 0.95f;
                m.Pipe(centre - axis * radius * 1.3f, centre + axis * radius * 1.3f, radius * 0.95f, 7, body, top);
                c.Box("Drum", centre, new Vector3(radius * 2.6f, radius * 1.9f, radius * 1.9f), -drumYaw);
            }
        }

        private static readonly int[][] Themes =
        {
            new[] { P.CrystalCyan, P.CrystalCyanLight },
            new[] { P.CrystalBlue, P.CrystalBlueLight },
            new[] { P.CrystalViolet, P.CrystalVioletLight },
            new[] { P.CrystalTurquoise, P.CrystalTurquoiseLight },
            new[] { P.CrystalMagenta, P.CrystalMagentaLight },
        };

        /// <summary>A crystal formation: one tall crystal and satellites round it, all emissive. <paramref name="colliderHeight"/> &gt; 0 gives it a capsule collider.</summary>
        private static void Cluster(Ctx c, float x, float z, float height, int theme, int count, float spread, float colliderHeight, int seed, float groundY = float.NaN)
        {
            AmMesh m = c.Mesh;
            int[] colours = Themes[theme % Themes.Length];
            Vector3 b = new(x, float.IsNaN(groundY) ? c.Floor(x, z) : groundY, z);
            bool previous = m.Glow;
            m.Glow = true;
            m.Crystal(b - Vector3.up * 0.2f, height * 0.17f, height + 0.2f, colours[0], colours[1], 3f, Rand(c, seed) * 360f, 6, Rand(c, seed, 1) * 60f);
            for (int k = 0; k < count; k++)
            {
                float angle = (k / (float)count + Rand(c, seed, k + 3) * 0.3f) * 360f;
                float radius = spread * (0.45f + Rand(c, seed, k + 9) * 0.6f);
                float h = height * (0.35f + Rand(c, seed, k + 20) * 0.45f);
                Vector3 p = b + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, -0.2f, Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
                int pick = (k & 1) == 0 ? 0 : 1;
                m.Crystal(p, h * 0.2f, h, colours[pick], colours[1 - pick], 10f + Rand(c, seed, k + 40) * 16f, 90f - angle, 6, Rand(c, seed, k + 60) * 60f);
            }

            m.Glow = previous;
            if (colliderHeight > 0f)
            {
                c.Capsule("Crystal", b, Mathf.Max(0.7f, spread * 0.7f), colliderHeight);
            }
        }

        /// <summary>A crystal or a rock spike hanging from the ceiling (visual only).</summary>
        private static void Hanging(Ctx c, float x, float z, float length, int theme, bool glowing, int rockTone)
        {
            AmMesh m = c.Mesh;
            float top = AmLayout.CeilingAt(x, z) + 0.3f;
            m.Push(new Vector3(x, top, z), Quaternion.Euler(180f, 0f, 0f));
            bool previous = m.Glow;
            m.Glow = glowing;
            if (glowing)
            {
                int[] colours = Themes[theme % Themes.Length];
                m.Crystal(Vector3.zero, length * 0.2f, length, colours[0], colours[1], 4f, 0f, 6, 30f);
            }
            else
            {
                m.Poly(Vector3.zero, length * 0.2f, 0f, length, 6, rockTone, rockTone, 20f);
            }

            m.Glow = previous;
            m.Pop();
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
        private static void FloorDisc(Ctx c, float x, float z, float radius, int color, float lift, int sides = 24, bool glow = false)
        {
            AmMesh m = c.Mesh;
            bool previous = m.Glow;
            m.Glow = glow;
            var pts = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = -k / (float)sides * Mathf.PI * 2f;
                pts[k] = new Vector3(x + Mathf.Cos(a) * radius, c.Floor(x, z) + lift, z + Mathf.Sin(a) * radius);
            }

            m.FaceWorld(color, Vector3.up, pts);
            m.Glow = previous;
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

        // ================================================================ A  ANCIENT GATE

        private static void DressGate(Ctx c)
        {
            AmMesh m = c.Mesh;
            float f = 15.5f;

            // ---- the great portal in the west wall: a tall half-round arch on two piers, a glowing doorway behind it
            Vector3 portal = new(-60.4f, f, -2f);
            Gateway(c, portal, 90f, 5.6f, 2.4f, 3.4f, 9f, P.Rock(AmZone.Gate, 5), P.Rock(AmZone.Gate, 3), true);
            // the doorway: a glowing panel (an arched polygon) just in front of the wall
            m.Glow = true;
            var door = new List<Vector3>();
            door.Add(new Vector3(-60.2f, f, -2f + 5.6f));
            for (int k = 0; k <= 10; k++)
            {
                float a = Mathf.PI * k / 10f;
                door.Add(new Vector3(-60.2f, f + 9f + Mathf.Sin(a) * 5.6f, -2f + Mathf.Cos(a) * 5.6f));
            }

            door.Add(new Vector3(-60.2f, f, -2f - 5.6f));
            m.FaceWorld(P.PortalGlow, Vector3.right, door.ToArray());
            // a brighter core so the panel is not one flat colour
            var core = new List<Vector3> { new Vector3(-60.15f, f, -2f + 3.0f) };
            for (int k = 0; k <= 8; k++)
            {
                float a = Mathf.PI * k / 8f;
                core.Add(new Vector3(-60.15f, f + 9f + Mathf.Sin(a) * 3.0f, -2f + Mathf.Cos(a) * 3.0f));
            }

            core.Add(new Vector3(-60.15f, f, -2f - 3.0f));
            m.FaceWorld(P.PortalGlowBright, Vector3.right, core.ToArray());
            m.Glow = false;
            // steps in front of the portal
            m.Block(new Vector3(-59.0f, f, -2f), new Vector3(2.4f, 0.25f, 13.5f), P.RoadStoneDark, P.RoadStone); // 0.25 m: lower than the controller's 0.3 m step, so it is walked over, not a wall
            c.Box("PortalStep", new Vector3(-59.0f, f + 0.125f, -2f), new Vector3(2.4f, 0.25f, 13.5f));

            // ---- the old stone road: from the portal through the hall and down the ramp to the Hall of Pillars
            var road = new List<Vector2> { new(-58f, -2f), new(-40f, -2f), new(-32f, -2f), new(-24f, -2f) };
            Ribbon(c, road, 6.6f, P.RoadStone, 0.05f);
            Ribbon(c, new List<Vector2> { new(-58f, -5.0f), new(-24f, -5.0f) }, 0.5f, P.RoadStoneDark, 0.07f);
            Ribbon(c, new List<Vector2> { new(-58f, 1.0f), new(-24f, 1.0f) }, 0.5f, P.RoadStoneDark, 0.07f);
            Ribbon(c, road, 0.4f, P.RoadInlay, 0.08f, true);

            // ---- the second, smaller arch over the way out (the road leaves the hall under it)
            Gateway(c, new Vector3(-38.6f, c.Floor(-38.6f, -2f), -2f), 90f, 4.9f, 1.7f, 2.6f, 8f, P.Rock(AmZone.Gate, 5), P.Rock(AmZone.Gate, 3), true);

            // ---- fallen and broken columns beside the road, leaving it free; two tall standing columns flank the way out
            BrokenColumn(c, -55.5f, 6.5f, 1.25f, 5.5f, 20f, P.Rock(AmZone.Gate, 4), P.Rock(AmZone.Gate, 6));
            BrokenColumn(c, -50.5f, 7.2f, 1.2f, 3.2f, 200f, P.Rock(AmZone.Gate, 4), P.Rock(AmZone.Gate, 6));
            BrokenColumn(c, -45.0f, 6.4f, 1.3f, 7.0f, 60f, P.Rock(AmZone.Gate, 4), P.Rock(AmZone.Gate, 6));
            BrokenColumn(c, -51.0f, -9.8f, 1.2f, 4.0f, 330f, P.Rock(AmZone.Gate, 4), P.Rock(AmZone.Gate, 6));
            BrokenColumn(c, -45.5f, -10.2f, 1.25f, 2.6f, 200f, P.Rock(AmZone.Gate, 4), P.Rock(AmZone.Gate, 6));
            BrokenColumn(c, -58.0f, 8.0f, 1.1f, 2.2f, 300f, P.Rock(AmZone.Gate, 4), P.Rock(AmZone.Gate, 6), false);
            Column(c, -42.5f, -9.6f, 1.2f, 13f, P.Rock(AmZone.Gate, 5), P.Rock(AmZone.Gate, 3), P.Rock(AmZone.Gate, 6));
            Column(c, -42.5f, 6.4f, 1.2f, 13f, P.Rock(AmZone.Gate, 5), P.Rock(AmZone.Gate, 3), P.Rock(AmZone.Gate, 6));
            // the entrance of the hidden passage is half-covered by a leaning slab (it does not block it)
            m.Push(c.On(-59.2f, -11.9f), 20f);
            m.Box(new Vector3(0f, 1.6f, 0f), new Vector3(1.0f, 3.2f, 3.4f), P.Rock(AmZone.Gate, 3), P.Rock(AmZone.Gate, 5));
            m.Pop();
            c.Box("LeaningSlab", c.On(-59.2f, -11.9f, 1.6f), new Vector3(1.0f, 3.2f, 3.4f), -20f);

            // ---- crystal accents (the "light" of the start): small clusters in the corners and along the walls, cool cyan and blue
            Cluster(c, -59.2f, 8.4f, 3.4f, 0, 4, 1.1f, 3f, 1);
            Cluster(c, -59.2f, -12.2f, 2.8f, 1, 3, 0.9f, 2.4f, 2);
            Cluster(c, -52.0f, 9.0f, 2.4f, 3, 3, 0.8f, 0f, 3);
            Cluster(c, -40.6f, 9.0f, 3.0f, 0, 4, 1.0f, 2.6f, 4);
            Cluster(c, -40.6f, -12.6f, 3.0f, 2, 4, 1.0f, 2.6f, 5);
            Cluster(c, -47.5f, -13.0f, 2.0f, 1, 3, 0.8f, 0f, 6);
            // the old cistern: crystals around the water basin
            Cluster(c, -58.0f, -37.0f, 4.5f, 3, 5, 1.3f, 3.5f, 7);
            Cluster(c, -49.0f, -38.0f, 3.4f, 1, 4, 1.1f, 3f, 8);
            Cluster(c, -54.0f, -40.5f, 3.0f, 0, 3, 0.9f, 0f, 9);
            m.Block(c.On(-54f, -33f), new Vector3(5.2f, 0.5f, 5.2f), P.Rock(AmZone.Gate, 3), P.StoneTeal);
            c.Box("CisternBasin", c.On(-54f, -33f, 0.25f), new Vector3(5.2f, 0.5f, 5.2f));

            // ---- the ceiling: hanging rock spikes and a few lights
            for (int k = 0; k < 14; k++)
            {
                Hanging(c, -59f + Rand(c, k, 1) * 19f, -12f + Rand(c, k, 2) * 21f, 2.4f + Rand(c, k, 3) * 3.2f, k % 3, k % 4 == 0, P.Rock(AmZone.Gate, 2));
            }
        }
    }
}
#endif
