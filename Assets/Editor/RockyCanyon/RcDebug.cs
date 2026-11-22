#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEngine;

namespace ItemCollector3D.EditorTools.RockyCanyon
{
    /// <summary>Design review helpers for the Rocky Canyon layout (height / zone / reachability picture and a text report). No effect on the scene.</summary>
    public static class RcDebug
    {
        public static string RenderMap(string path, int pixelsPerMetre = 8)
        {
            RcLayout.ResetGrid();
            bool[,] reach = RcLayout.Reachable(new Vector2(RcLayout.Spawn.x, RcLayout.Spawn.z));
            int w = (int)(2 * RcLayout.HalfX * pixelsPerMetre), h = (int)(2 * RcLayout.HalfZ * pixelsPerMetre);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color[w * h];
            for (int py = 0; py < h; py++)
            {
                for (int pxx = 0; pxx < w; pxx++)
                {
                    float x = -RcLayout.HalfX + (pxx + 0.5f) / pixelsPerMetre, z = -RcLayout.HalfZ + (py + 0.5f) / pixelsPerMetre;
                    float height = RcLayout.Surface(x, z);
                    float slope = RcLayout.Slope(x, z);
                    float shade = Mathf.Clamp01(0.35f + height / 26f);
                    Color c = Color.Lerp(new Color(0.25f, 0.12f, 0.10f), new Color(1f, 0.9f, 0.7f), shade);
                    if (slope > 1.0f)
                    {
                        c *= 0.7f;
                    }

                    int ix = Mathf.Clamp(Mathf.RoundToInt(x + RcLayout.HalfX), 0, (int)(2 * RcLayout.HalfX));
                    int iz = Mathf.Clamp(Mathf.RoundToInt(z + RcLayout.HalfZ), 0, (int)(2 * RcLayout.HalfZ));
                    if (reach[ix, iz])
                    {
                        c = Color.Lerp(c, new Color(0.2f, 0.9f, 0.3f), 0.55f);
                    }

                    // contour lines every 2 m
                    if (Mathf.Abs(Mathf.Repeat(height, 2f) - 1f) < 0.05f)
                    {
                        c *= 0.8f;
                    }

                    px[py * w + pxx] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        public static string Report()
        {
            RcLayout.ResetGrid();
            var sb = new StringBuilder();
            float min = float.MaxValue, max = float.MinValue;
            foreach (float v in RcLayout.Grid)
            {
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }

            sb.AppendLine($"terrain height {min:F1} .. {max:F1} m");
            bool[,] reach = RcLayout.Reachable(new Vector2(RcLayout.Spawn.x, RcLayout.Spawn.z));
            (string name, float x, float z)[] targets =
            {
                ("Outpost centre", -34f, -22f), ("Riverbed west", -14f, -17f), ("Basin", 6f, -14f), ("Riverbed east", 31f, 0f), ("Plaza", 40f, 12f),
                ("Railway west", -32f, 3f), ("Railway middle", -6f, 17f), ("Railway dip", 3f, 17.5f), ("Railway east", 24f, 20f),
                ("Overlook path mid", 34f, 26f), ("Overlook plateau", 43f, 36f), ("Ravine mouth", -4f, -13f), ("Ravine middle", -9f, -1f), ("Ravine pocket", -17f, 2f),
            };
            foreach ((string name, float x, float z) in targets)
            {
                int ix = Mathf.RoundToInt(x + RcLayout.HalfX), iz = Mathf.RoundToInt(z + RcLayout.HalfZ);
                sb.AppendLine($"{name,-18} ({x,5:F0},{z,5:F0}) height {RcLayout.Surface(x, z),5:F1} reachable={reach[ix, iz]} zone={RcLayout.ZoneAt(x, z)}");
            }

            // steepest walkable step between neighbours and the steepest slope inside any floor
            float worstSlope = 0f;
            Vector2 worstAt = default;
            for (float x = -RcLayout.HalfX + 2; x < RcLayout.HalfX - 2; x += 1f)
            {
                for (float z = -RcLayout.HalfZ + 2; z < RcLayout.HalfZ - 2; z += 1f)
                {
                    if (RcLayout.EdgeDistance(x, z) <= -0.4f)
                    {
                        float s = RcLayout.Slope(x, z);
                        if (s > worstSlope)
                        {
                            worstSlope = s;
                            worstAt = new Vector2(x, z);
                        }
                    }
                }
            }

            sb.AppendLine($"steepest slope inside a floor: {worstSlope:F2} ({Mathf.Atan(worstSlope) * Mathf.Rad2Deg:F0} deg) at {worstAt}");
            int reachableCells = 0, floorCells = 0;
            for (int ix = 0; ix <= 2 * (int)RcLayout.HalfX; ix++)
            {
                for (int iz = 0; iz <= 2 * (int)RcLayout.HalfZ; iz++)
                {
                    if (RcLayout.EdgeDistance(ix - RcLayout.HalfX, iz - RcLayout.HalfZ) <= -0.4f)
                    {
                        floorCells++;
                        if (reach[ix, iz])
                        {
                            reachableCells++;
                        }
                    }
                }
            }

            sb.AppendLine($"floor cells {floorCells}, reachable from the spawn {reachableCells} ({100f * reachableCells / floorCells:F1} %)");
            return sb.ToString();
        }
    }
}
#endif
