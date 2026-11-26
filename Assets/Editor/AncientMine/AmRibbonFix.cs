#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>
    /// Stage 16D fix for the zig-zag / dashed road lines. The flat ribbons lying on the floor (road stone, glowing path inlay, gold edge lines) were generated from corner heights of the floor height
    /// field, but between its corners the floor follows a 2 m triangle grid and the ribbons are straight, so in places the floor poked through the stone, and the inlay / gold lines (only 3 - 4 cm
    /// above the stone) were swallowed by it. This tool patches the EXISTING structure meshes in place, ONCE per mesh:
    /// (A) floor pass: every up-facing floor-hugging triangle is lifted just enough that all sample points are at least <see cref="MinLift"/> above the floor collider;
    /// (B) line pass: every glow inlay triangle (slot 1) and every thin gold line triangle (slot 0) is lifted so it is at least <see cref="LineGap"/> above the wide stone ribbon beneath it.
    /// Safety: only vertices that belong to flat up-facing triangles only are moved (anything shared with a wall, step riser or pillar is left alone, so stairs, daises and pillar bases never
    /// move), the XZ position never changes, no vertex rises more than <see cref="MaxRaise"/> in one go, the work is computed from the mesh as it is and applied in one step (no iteration),
    /// and a mesh that was patched is renamed with the suffix <see cref="Marker"/> so running the tool again does nothing (earlier iterative runs crept upward through shared vertices).
    /// NOTE: regenerating a zone (DressZoneInOpenScene / RebuildEnvironment) writes new meshes, which loses this patch: run <c>AmRibbonFix.Apply(false)</c> again afterwards.
    /// Colliders, scene objects, treasures and the atmosphere are untouched. Run via <c>script eval</c>.
    /// </summary>
    public static class AmRibbonFix
    {
        public const string Marker = "+ribbonfix";
        public const float MinLift = 0.045f;
        public const float LineGap = 0.06f;
        public const float LineMaxRaise = 0.18f;
        public const float MaxRaise = 0.12f;

        private static readonly List<float[]> Bary = BuildSamples(8);

        private static List<float[]> BuildSamples(int n)
        {
            var list = new List<float[]>();
            for (int x = 0; x <= n; x++)
            {
                for (int y = 0; y <= n - x; y++)
                {
                    list.Add(new[] { x / (float)n, y / (float)n, 1f - (x + y) / (float)n });
                }
            }

            return list;
        }

        /// <summary>
        /// Patches the structure mesh of a REGENERATED zone again (null = every zone). DressZone refills the mesh asset but keeps its name, so the "+ribbonfix" suffix lies about a mesh that is
        /// no longer patched: this forgets the suffix of the named zone(s) only (a mesh that really is patched must never be patched twice) and runs <see cref="Apply"/>.
        /// </summary>
        public static string Reapply(string zoneName)
        {
            GameObject env = GameObject.Find("AncientMineEnvironment");
            foreach (MeshRenderer mr in env.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.name != "Structures" || (zoneName != null && mr.transform.parent.parent.name != zoneName))
                {
                    continue;
                }

                Mesh mesh = mr.GetComponent<MeshFilter>().sharedMesh;
                if (mesh.name.EndsWith(Marker))
                {
                    mesh.name = mesh.name.Substring(0, mesh.name.Length - Marker.Length);
                }
            }

            return Apply(false);
        }

        public static string Apply(bool dryRun)
        {
            Physics.SyncTransforms();
            GameObject env = GameObject.Find("AncientMineEnvironment");
            var report = new System.Text.StringBuilder();
            foreach (MeshRenderer mr in env.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.name != "Structures")
                {
                    continue;
                }

                Mesh mesh = mr.GetComponent<MeshFilter>().sharedMesh;
                string zone = mr.transform.parent.parent.name;
                if (mesh.name.EndsWith(Marker))
                {
                    report.AppendLine($"{zone}: already patched, nothing done");
                    continue;
                }

                Vector3[] v = mesh.vertices;
                int[][] tris = new int[mesh.subMeshCount][];
                for (int sm = 0; sm < mesh.subMeshCount; sm++)
                {
                    tris[sm] = mesh.GetTriangles(sm);
                }

                // vertices that belong to anything but a flat up-facing triangle (walls, risers, pillar sides, tilted things) never move
                var pinned = new bool[v.Length];
                for (int sm = 0; sm < tris.Length; sm++)
                {
                    for (int i = 0; i < tris[sm].Length; i += 3)
                    {
                        if (Vector3.Cross(v[tris[sm][i + 1]] - v[tris[sm][i]], v[tris[sm][i + 2]] - v[tris[sm][i]]).normalized.y < 0.8f)
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                pinned[tris[sm][i + k]] = true;
                            }
                        }
                    }
                }

                var pinValue = new float[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    pinValue[i] = pinned[i] ? 1f : 0f;
                }

                Weld(v, pinValue);
                for (int i = 0; i < v.Length; i++)
                {
                    pinned[i] = pinValue[i] > 0.5f;
                }

                // ---- A: floor pass
                var raise = new float[v.Length];
                int floorTris = 0;
                for (int sm = 0; sm < tris.Length; sm++)
                {
                    for (int i = 0; i < tris[sm].Length; i += 3)
                    {
                        Vector3 a = v[tris[sm][i]], b = v[tris[sm][i + 1]], c = v[tris[sm][i + 2]];
                        if (Vector3.Cross(b - a, c - a).normalized.y < 0.8f || Mathf.Min(PinnedCount(pinned, tris[sm], i), 3) > 0)
                        {
                            continue;
                        }

                        float need = 0f;
                        bool hugging = false;
                        foreach (float[] w in Bary)
                        {
                            Vector3 p = mr.transform.TransformPoint(a * w[0] + b * w[1] + c * w[2]);
                            float floor = FloorBelow(p);
                            if (float.IsNaN(floor))
                            {
                                continue;
                            }

                            float d = p.y - floor;
                            if (d > 0.2f || d < -MaxRaise)
                            {
                                continue; // not a floor-hugging ribbon sample
                            }

                            hugging = true;
                            need = Mathf.Max(need, MinLift - d);
                        }

                        if (hugging && need > 0.0005f && need <= MaxRaise)
                        {
                            floorTris++;
                            Raise(raise, tris[sm], i, need);
                        }
                    }
                }

                Weld(v, raise);
                Vector3[] v1 = (Vector3[])v.Clone();
                for (int i = 0; i < v1.Length; i++)
                {
                    v1[i].y += raise[i];
                }

                // ---- B: line pass, against the surface after A
                var raiseB = new float[v.Length];
                int lineTris = 0, sharedSkips = 0;
                Split(v1, tris[0], out int[] wide, out int[] thin);
                var wideVertices = new HashSet<int>(wide);
                var jobs = new List<(int[] targets, int[] support)>();
                if (tris.Length > 1)
                {
                    jobs.Add((tris[1], tris[0]));
                }

                jobs.Add((thin, wide));
                foreach ((int[] targets, int[] support) in jobs)
                {
                    for (int i = 0; i < targets.Length; i += 3)
                    {
                        if (wideVertices.Contains(targets[i]) || wideVertices.Contains(targets[i + 1]) || wideVertices.Contains(targets[i + 2]) || PinnedCount(pinned, targets, i) > 0)
                        {
                            sharedSkips++;
                            continue; // shares a vertex with the stone itself or with a wall: moving it would move that too
                        }

                        Vector3 a = v1[targets[i]], b = v1[targets[i + 1]], c = v1[targets[i + 2]];
                        if (Vector3.Cross(b - a, c - a).normalized.y < 0.8f)
                        {
                            continue;
                        }

                        float need = 0f;
                        foreach (float[] w in Bary)
                        {
                            Vector3 p = a * w[0] + b * w[1] + c * w[2];
                            float top = StoneHeight(v1, support, p);
                            if (!float.IsNaN(top))
                            {
                                need = Mathf.Max(need, top + LineGap - p.y);
                            }
                        }

                        if (need > 0.0005f && need <= LineMaxRaise)
                        {
                            lineTris++;
                            Raise(raiseB, targets, i, need);
                        }
                    }
                }

                Weld(v, raiseB);
                int moved = 0;
                float max = 0f;
                for (int i = 0; i < v.Length; i++)
                {
                    float total = raise[i] + raiseB[i];
                    if (total > 0f && !pinned[i])
                    {
                        v[i].y += total;
                        moved++;
                        max = Mathf.Max(max, total);
                    }
                }

                report.AppendLine($"{zone}: floor pass {floorTris} triangles, line pass {lineTris} triangles, {moved} vertices moved, highest raise {max:F3} m, {sharedSkips} line triangles skipped (shared with stone / walls)");
                if (!dryRun && moved > 0)
                {
                    mesh.vertices = v;
                    mesh.RecalculateBounds();
                    mesh.name += Marker;
                    EditorUtility.SetDirty(mesh);
                }
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
            }

            return report.ToString();
        }


        /// <summary>
        /// Vertices that sit at the same place (the end corners of one ribbon quad and the start corners of the next one are separate vertices) must be lifted by the SAME amount, otherwise every
        /// joint gets a small step: the highest value of each position wins. Also used for the pinned flags (one pinned vertex pins everything at its position).
        /// </summary>
        private static void Weld(Vector3[] v, float[] values)
        {
            var best = new Dictionary<(int, int, int), float>();
            for (int i = 0; i < v.Length; i++)
            {
                var key = (Mathf.RoundToInt(v[i].x * 500f), Mathf.RoundToInt(v[i].y * 500f), Mathf.RoundToInt(v[i].z * 500f));
                best[key] = best.TryGetValue(key, out float old) ? Mathf.Max(old, values[i]) : values[i];
            }

            for (int i = 0; i < v.Length; i++)
            {
                values[i] = best[(Mathf.RoundToInt(v[i].x * 500f), Mathf.RoundToInt(v[i].y * 500f), Mathf.RoundToInt(v[i].z * 500f))];
            }
        }

        private static int PinnedCount(bool[] pinned, int[] tris, int i) => (pinned[tris[i]] ? 1 : 0) + (pinned[tris[i + 1]] ? 1 : 0) + (pinned[tris[i + 2]] ? 1 : 0);

        private static void Raise(float[] raise, int[] tris, int i, float need)
        {
            for (int k = 0; k < 3; k++)
            {
                raise[tris[i + k]] = Mathf.Max(raise[tris[i + k]], need);
            }
        }

        /// <summary>Splits slot-0 up-facing triangles into WIDE (road stone: triangle height >= 0.4 m) and THIN (gold edge line: height < 0.25 m).</summary>
        private static void Split(Vector3[] v, int[] stone, out int[] wide, out int[] thin)
        {
            var w = new List<int>();
            var t = new List<int>();
            for (int i = 0; i < stone.Length; i += 3)
            {
                Vector3 a = v[stone[i]], b = v[stone[i + 1]], c = v[stone[i + 2]];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.normalized.y < 0.8f)
                {
                    continue;
                }

                float longest = Mathf.Max((b - a).magnitude, Mathf.Max((c - b).magnitude, (a - c).magnitude));
                float height = cross.magnitude / Mathf.Max(longest, 1e-4f);
                List<int> list = height >= 0.4f ? w : height < 0.25f ? t : null;
                if (list != null)
                {
                    list.Add(stone[i]);
                    list.Add(stone[i + 1]);
                    list.Add(stone[i + 2]);
                }
            }

            wide = w.ToArray();
            thin = t.ToArray();
        }

        /// <summary>Height of the highest up-facing triangle of <paramref name="support"/> that lies at most 0.3 m below and 0.12 m above the point (in plan view the point is inside it). NaN if none.</summary>
        private static float StoneHeight(Vector3[] v, int[] support, Vector3 p)
        {
            float best = float.NaN;
            for (int i = 0; i < support.Length; i += 3)
            {
                Vector3 a = v[support[i]], b = v[support[i + 1]], c = v[support[i + 2]];
                if (p.x < Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - 0.01f || p.x > Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 0.01f ||
                    p.z < Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - 0.01f || p.z > Mathf.Max(a.z, Mathf.Max(b.z, c.z)) + 0.01f)
                {
                    continue;
                }

                float det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(det) < 1e-6f)
                {
                    continue;
                }

                float l1 = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.z - c.z)) / det;
                float l2 = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.z - c.z)) / det;
                float l3 = 1f - l1 - l2;
                if (l1 < -0.001f || l2 < -0.001f || l3 < -0.001f)
                {
                    continue;
                }

                float y = a.y * l1 + b.y * l2 + c.y * l3;
                if (y < p.y - 0.3f || y > p.y + 0.12f)
                {
                    continue;
                }

                if (float.IsNaN(best) || y > best)
                {
                    best = y;
                }
            }

            return best;
        }

        private static float FloorBelow(Vector3 world)
        {
            float best = float.NaN;
            foreach (RaycastHit h in Physics.RaycastAll(new Vector3(world.x, world.y + 3f, world.z), Vector3.down, 6f))
            {
                if (h.collider.name == "Floor" && (float.IsNaN(best) || h.point.y > best))
                {
                    best = h.point.y;
                }
            }

            return best;
        }
    }
}
#endif
