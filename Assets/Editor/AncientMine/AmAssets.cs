#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>The cave shell of one area: its floor-and-walls mesh and its ceiling mesh (two meshes so the ceiling can skip shadow casting and each area is culled on its own).</summary>
    public sealed class AmShell
    {
        public Mesh Floor, Ceiling;
        public int FloorTriangles, CeilingTriangles;
    }

    public static class AmAssets
    {
        public const string Root = "Assets/Art/AncientMine";
        public const string MeshFolder = Root + "/Meshes";
        public const string MaterialFolder = Root + "/Materials";
        public const string TextureFolder = Root + "/Textures";
        public const string DressingFolder = MeshFolder + "/Dressing";
        public const string ShellFolder = MeshFolder + "/Shell";

        private const float BandHeight = 3.0f; // steep faces are cut into horizontal courses of this height (stone courses / rock strata)

        // ================================================================ materials

        public static Material Lit(string name, Color color, Texture2D map)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetTexture("_BaseMap", map);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material PaletteMaterial(Texture2D palette) => Lit("AM_Palette", Color.white, palette);

        /// <summary>The same palette, self-lit: crystals, the portal and the lantern glass. The only emissive material; no transparency.</summary>
        public static Material GlowMaterial(Texture2D palette)
        {
            Material mat = Lit("AM_Glow", Color.white, palette);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.9f));
            mat.SetTexture("_EmissionMap", palette);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ================================================================ the cave shell

        public static Dictionary<AmZone, AmShell> BuildShells()
        {
            AmLayout.ResetGrid();
            AmLayout.Sample[,] g = AmLayout.Grid;
            var floors = new Dictionary<AmZone, AmMesh>();
            var ceilings = new Dictionary<AmZone, AmMesh>();
            foreach (AmZone zone in new[] { AmZone.Gate, AmZone.Hall, AmZone.Crystal, AmZone.Workshop, AmZone.Vault })
            {
                floors[zone] = new AmMesh();
                ceilings[zone] = new AmMesh();
            }

            for (int j = 0; j < AmLayout.CellsZ; j++)
            {
                for (int i = 0; i < AmLayout.CellsX; i++)
                {
                    AmLayout.Sample sa = g[i, j], sb = g[i + 1, j], sc = g[i, j + 1], sd = g[i + 1, j + 1];
                    if (AmLayout.IsSolid(sa) && AmLayout.IsSolid(sb) && AmLayout.IsSolid(sc) && AmLayout.IsSolid(sd))
                    {
                        continue; // rock inside rock: nothing to see, nothing to collide with
                    }

                    float x0 = -AmLayout.HalfX + i * AmLayout.Cell, z0 = -AmLayout.HalfZ + j * AmLayout.Cell, x1 = x0 + AmLayout.Cell, z1 = z0 + AmLayout.Cell;
                    AmLayout.Sample centre = AmLayout.Evaluate((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
                    AmZone zone = AmLayout.Shapes[centre.Shape].Zone;
                    int cellHash = Mathf.Abs(i * 7919 + j * 104729 + 13) % 997;

                    Vector3 fa = new(x0, sa.Floor, z0), fb = new(x1, sb.Floor, z0), fc = new(x0, sc.Floor, z1), fd = new(x1, sd.Floor, z1);
                    Vector3 ca = new(x0, sa.Ceiling, z0), cb = new(x1, sb.Ceiling, z0), cc = new(x0, sc.Ceiling, z1), cd = new(x1, sd.Ceiling, z1);
                    bool checker = ((i + j) & 1) == 0;
                    // the same two triangles the height function interpolates: (a, c, b) and (b, c, d)
                    Surface(floors[zone], zone, fa, fc, fb, false, checker, cellHash);
                    Surface(floors[zone], zone, fb, fc, fd, false, checker, cellHash + 1);
                    Surface(ceilings[zone], zone, ca, cc, cb, true, checker, cellHash + 2);
                    Surface(ceilings[zone], zone, cb, cc, cd, true, checker, cellHash + 3);
                }
            }

            var result = new Dictionary<AmZone, AmShell>();
            foreach (var pair in floors)
            {
                AmZone zone = pair.Key;
                var shell = new AmShell
                {
                    Floor = pair.Value.ToMesh($"AM_{zone}_Floor", ShellFolder),
                    Ceiling = ceilings[zone].ToMesh($"AM_{zone}_Ceiling", ShellFolder),
                    FloorTriangles = pair.Value.Triangles,
                    CeilingTriangles = ceilings[zone].Triangles,
                };
                result[zone] = shell;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static bool IsArchitectural(AmZone zone) => zone != AmZone.Crystal;

        private static void Surface(AmMesh mesh, AmZone zone, Vector3 a, Vector3 b, Vector3 c, bool ceiling, bool checker, int hash)
        {
            Vector3 facing = ceiling ? Vector3.down : Vector3.up;
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-8f)
            {
                return;
            }

            float ny = Mathf.Abs(n.normalized.y);
            if (ny > 0.72f)
            {
                int tone;
                if (ceiling)
                {
                    tone = 1 + (hash % 2);
                }
                else if (IsArchitectural(zone))
                {
                    tone = (checker ? 5 : 4) + (hash % 5 == 0 ? 1 : 0); // flagstones: a calm two-tone pattern with the odd lighter slab
                }
                else
                {
                    tone = 3 + (hash % 3); // cave floor: uneven, natural
                }

                mesh.FaceWorld(P.Rock(zone, tone), facing, a, b, c);
                return;
            }

            // steep: cut into horizontal courses and give each its own tone (stone courses in the halls, strata in the caves)
            float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            int first = Mathf.FloorToInt(minY / BandHeight), last = Mathf.FloorToInt(maxY / BandHeight);
            var poly = new List<Vector3> { a, b, c };
            for (int band = first; band <= last; band++)
            {
                List<Vector3> piece = ClipY(ClipY(poly, band * BandHeight, true), (band + 1) * BandHeight, false);
                if (piece.Count < 3 || Area(piece) < 0.02f)
                {
                    continue;
                }

                int tone = ceiling ? 1 : 2 + ((band * 5 + hash) % 3) - Mathf.Clamp(band / 6, 0, 2);
                mesh.FaceWorld(P.Rock(zone, tone), facing, piece.ToArray());
            }
        }

        private static List<Vector3> ClipY(List<Vector3> poly, float y, bool keepAbove)
        {
            var result = new List<Vector3>(poly.Count + 2);
            for (int i = 0; i < poly.Count; i++)
            {
                Vector3 cur = poly[i], next = poly[(i + 1) % poly.Count];
                bool curIn = keepAbove ? cur.y >= y : cur.y <= y;
                bool nextIn = keepAbove ? next.y >= y : next.y <= y;
                if (curIn)
                {
                    result.Add(cur);
                }

                if (curIn != nextIn)
                {
                    float t = (y - cur.y) / (next.y - cur.y);
                    result.Add(Vector3.Lerp(cur, next, t));
                }
            }

            return result;
        }

        private static float Area(List<Vector3> p)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 1; i < p.Count - 1; i++)
            {
                sum += Vector3.Cross(p[i] - p[0], p[i + 1] - p[0]);
            }

            return sum.magnitude * 0.5f;
        }
    }
}
#endif
