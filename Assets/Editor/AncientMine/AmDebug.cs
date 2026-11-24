#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEngine;

namespace ItemCollector3D.EditorTools.AncientMine
{
    /// <summary>Design review helpers (edit mode, no scene needed): a report of the map's numbers (extents, heights, slopes, widths, reachability, overlaps) and a top-down picture of it.</summary>
    public static class AmDebug
    {
        public static string Report()
        {
            AmLayout.ResetGrid();
            var sb = new StringBuilder();
            float minFloor = float.MaxValue, maxFloor = float.MinValue, maxCeiling = float.MinValue, minClear = float.MaxValue;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            int floorCells = 0;
            Vector2 minClearAt = default;
            for (float x = -AmLayout.HalfX + 1; x < AmLayout.HalfX - 1; x += 1f)
            {
                for (float z = -AmLayout.HalfZ + 1; z < AmLayout.HalfZ - 1; z += 1f)
                {
                    if (AmLayout.EdgeDistance(x, z) <= -2.5f)
                    {
                        floorCells++;
                        float h = AmLayout.Surface(x, z), c = AmLayout.CeilingAt(x, z);
                        minFloor = Mathf.Min(minFloor, h);
                        maxFloor = Mathf.Max(maxFloor, h);
                        maxCeiling = Mathf.Max(maxCeiling, c);
                        if (c - h < minClear)
                        {
                            minClear = c - h;
                            minClearAt = new Vector2(x, z);
                        }

                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        minZ = Mathf.Min(minZ, z);
                        maxZ = Mathf.Max(maxZ, z);
                    }
                }
            }

            sb.AppendLine($"walkable extents x {minX:F1}..{maxX:F1} ({maxX - minX:F1} m), z {minZ:F1}..{maxZ:F1} ({maxZ - minZ:F1} m); floor cells (1 m): {floorCells}");
            sb.AppendLine($"floor height {minFloor:F2}..{maxFloor:F2} m (range {maxFloor - minFloor:F2}), highest ceiling {maxCeiling:F1} m, lowest clearance above a walkable point {minClear:F1} m at {minClearAt}");

            bool[,] reach = AmLayout.Reachable(new Vector2(AmLayout.Spawn.x, AmLayout.Spawn.z));
            int reachable = 0;
            for (int ix = 0; ix <= 2 * (int)AmLayout.HalfX; ix++)
            {
                for (int iz = 0; iz <= 2 * (int)AmLayout.HalfZ; iz++)
                {
                    if (AmLayout.EdgeDistance(ix - AmLayout.HalfX, iz - AmLayout.HalfZ) <= -2.5f && reach[ix, iz])
                    {
                        reachable++;
                    }
                }
            }

            sb.AppendLine($"cells reachable from the spawn (step <= {AmLayout.WalkableStep}): {reachable} of {floorCells} ({100f * reachable / floorCells:F1} %)");
            foreach (var a in AmLayout.Anchors)
            {
                sb.AppendLine($"anchor {a.name,-22} {a.point} reachable={Reach(reach, a.point)} floor {AmLayout.Surface(a.point.x, a.point.y):F2} zone {AmLayout.ZoneAt(a.point.x, a.point.y)}");
            }

            int unreachable = 0;
            foreach (var cand in AmLayout.TreasureCandidates)
            {
                bool ok = Reach(reach, cand.point) && AmLayout.EdgeDistance(cand.point.x, cand.point.y) <= -2f;
                unreachable += ok ? 0 : 1;
                if (!ok)
                {
                    sb.AppendLine($"treasure candidate {cand.id} {cand.point}: NOT reachable / not inside a cave");
                }
            }

            sb.AppendLine($"treasure candidates: {AmLayout.TreasureCandidates.Length}, not reachable or too close to a wall: {unreachable}");
            foreach (var p in AmLayout.SidePlaces)
            {
                sb.AppendLine($"place {p.name,-22} {p.point} reachable={Reach(reach, p.point)} floor {AmLayout.Surface(p.point.x, p.point.y):F2}");
            }

            foreach ((string routeName, Vector2[] pts) in AmLayout.AllRoutes())
            {
                float steepest = 0f, narrowest = float.MaxValue, lowest = float.MaxValue, length = 0f;
                int narrowAt = 0, steepAt = 0;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1];
                    float len = Vector2.Distance(a, b);
                    length += len;
                    for (float d = 0f; d <= len; d += 0.5f)
                    {
                        Vector2 p = Vector2.Lerp(a, b, d / len);
                        float slope = AmLayout.Slope(p.x, p.y);
                        if (slope > steepest)
                        {
                            steepest = slope;
                            steepAt = i;
                        }

                        float free = AmLayout.FreeRadius(p.x, p.y);
                        if (free < narrowest)
                        {
                            narrowest = free;
                            narrowAt = i;
                        }

                        lowest = Mathf.Min(lowest, AmLayout.CeilingAt(p.x, p.y) - AmLayout.Surface(p.x, p.y));
                    }
                }

                sb.AppendLine($"route {routeName}: {length:F0} m, steepest slope {steepest:F2} ({Mathf.Atan(steepest) * Mathf.Rad2Deg:F0} deg) at leg {steepAt}, narrowest free radius {narrowest:F2} m (width about {narrowest * 2f:F1}) at leg {narrowAt}, lowest ceiling {lowest:F1} m");
            }

            // steep floor mesh slopes inside the shapes (daises and ramps' sides are real steps; everything else should be gentle)
            float worst = 0f;
            Vector2 worstAt = default;
            int steepCells = 0;
            for (float x = -AmLayout.HalfX + 2; x < AmLayout.HalfX - 2; x += 1f)
            {
                for (float z = -AmLayout.HalfZ + 2; z < AmLayout.HalfZ - 2; z += 1f)
                {
                    if (AmLayout.EdgeDistance(x, z) <= -1.5f)
                    {
                        float s = AmLayout.Slope(x, z);
                        if (s > worst)
                        {
                            worst = s;
                            worstAt = new Vector2(x, z);
                        }

                        if (s > 0.45f)
                        {
                            steepCells++;
                        }
                    }
                }
            }

            sb.AppendLine($"steepest slope inside any shape (1.5 m from its edge): {worst:F2} at {worstAt}; samples above 0.45: {steepCells}");
            return sb.ToString();
        }

        private static bool Reach(bool[,] reach, Vector2 p)
        {
            int ix = Mathf.RoundToInt(p.x + AmLayout.HalfX), iz = Mathf.RoundToInt(p.y + AmLayout.HalfZ);
            return reach[ix, iz];
        }

        /// <summary>A top-down picture of the floor heights (blue = low, yellow = high), walls dark, routes in white / orange, anchors red. 4 px per metre.</summary>
        public static string Map(string path)
        {
            AmLayout.ResetGrid();
            const int scale = 4;
            int w = (int)(2 * AmLayout.HalfX) * scale, h = (int)(2 * AmLayout.HalfZ) * scale;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var pixels = new Color[w * h];
            for (int py = 0; py < h; py++)
            {
                for (int px = 0; px < w; px++)
                {
                    float x = -AmLayout.HalfX + (px + 0.5f) / scale, z = -AmLayout.HalfZ + (py + 0.5f) / scale;
                    float e = AmLayout.EdgeDistance(x, z);
                    Color c;
                    if (e <= -0.3f)
                    {
                        float t = Mathf.InverseLerp(0f, 16f, AmLayout.Surface(x, z));
                        c = Color.Lerp(new Color(0.15f, 0.25f, 0.8f), new Color(1f, 0.9f, 0.3f), t);
                    }
                    else if (e <= 4f)
                    {
                        c = new Color(0.22f, 0.2f, 0.3f);
                    }
                    else
                    {
                        c = new Color(0.08f, 0.08f, 0.1f);
                    }

                    pixels[py * w + px] = c;
                }
            }

            void Dot(Vector2 p, Color col, int r)
            {
                int cx = Mathf.RoundToInt((p.x + AmLayout.HalfX) * scale), cy = Mathf.RoundToInt((p.y + AmLayout.HalfZ) * scale);
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x >= 0 && y >= 0 && x < w && y < h && dx * dx + dy * dy <= r * r)
                        {
                            pixels[y * w + x] = col;
                        }
                    }
                }
            }

            void Line(Vector2 a, Vector2 b, Color col)
            {
                float len = Vector2.Distance(a, b);
                for (float d = 0f; d <= len; d += 0.25f)
                {
                    Dot(Vector2.Lerp(a, b, d / len), col, 1);
                }
            }

            for (int i = 0; i < AmLayout.MainRoute.Length - 1; i++)
            {
                Line(AmLayout.MainRoute[i].point, AmLayout.MainRoute[i + 1].point, Color.white);
            }

            for (int i = 0; i < AmLayout.AlternateRoute.Length - 1; i++)
            {
                Line(AmLayout.AlternateRoute[i].point, AmLayout.AlternateRoute[i + 1].point, new Color(1f, 0.5f, 0.1f));
            }

            foreach ((string n, Vector2[] pts) in AmLayout.SideRoutes)
            {
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Line(pts[i], pts[i + 1], new Color(0.3f, 1f, 0.3f));
                }
            }

            foreach (var a in AmLayout.Anchors)
            {
                Dot(a.point, Color.red, 5);
            }

            foreach (var p in AmLayout.SidePlaces)
            {
                Dot(p.point, Color.green, 3);
            }

            Dot(new Vector2(AmLayout.Spawn.x, AmLayout.Spawn.z), Color.magenta, 4);
            tex.SetPixels(pixels);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }
    }
}
#endif
