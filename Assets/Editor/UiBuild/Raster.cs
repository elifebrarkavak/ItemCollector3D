using System;
using UnityEngine;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// Tiny anti-aliased 2D rasteriser for the generated UI art. Shapes are signed-distance functions in pixels (negative inside),
    /// painted back to front. Image space: x to the right, y DOWN (flipped when the texture is created).
    /// </summary>
    public sealed class Raster
    {
        public readonly int Width;
        public readonly int Height;
        private readonly Color[] pixels;

        public delegate float Sdf(float x, float y);
        public delegate Color ColorFn(float x, float y);

        public Raster(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color[width * height];
        }

        // ---------------------------------------------------------------- shapes (signed distance, pixels)

        public static Sdf RoundRect(float cx, float cy, float halfW, float halfH, float radius)
        {
            radius = Mathf.Min(radius, Mathf.Min(halfW, halfH));
            return (x, y) =>
            {
                float qx = Mathf.Abs(x - cx) - (halfW - radius);
                float qy = Mathf.Abs(y - cy) - (halfH - radius);
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            };
        }

        public static Sdf RoundRectBox(float left, float top, float right, float bottom, float radius)
        {
            return RoundRect((left + right) * 0.5f, (top + bottom) * 0.5f, (right - left) * 0.5f, (bottom - top) * 0.5f, radius);
        }

        public static Sdf Circle(float cx, float cy, float r)
        {
            return (x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
        }

        public static Sdf Ellipse(float cx, float cy, float rx, float ry)
        {
            // good-enough approximation of the ellipse distance
            return (x, y) =>
            {
                float px = (x - cx) / rx, py = (y - cy) / ry;
                float k = Mathf.Sqrt(px * px + py * py);
                return (k - 1f) * Mathf.Min(rx, ry);
            };
        }

        public static Sdf Capsule(float ax, float ay, float bx, float by, float r)
        {
            return (x, y) =>
            {
                float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
                float h = Mathf.Clamp01((pax * bax + pay * bay) / Mathf.Max(1e-6f, bax * bax + bay * bay));
                float dx = pax - bax * h, dy = pay - bay * h;
                return Mathf.Sqrt(dx * dx + dy * dy) - r;
            };
        }

        public static Sdf Polygon(params Vector2[] points)
        {
            return (x, y) =>
            {
                float d = float.MaxValue;
                bool inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    Vector2 a = points[i], b = points[j];
                    float ex = b.x - a.x, ey = b.y - a.y;
                    float wx = x - a.x, wy = y - a.y;
                    float t = Mathf.Clamp01((wx * ex + wy * ey) / Mathf.Max(1e-6f, ex * ex + ey * ey));
                    float dx = wx - ex * t, dy = wy - ey * t;
                    d = Mathf.Min(d, dx * dx + dy * dy);
                    if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    {
                        inside = !inside;
                    }
                }

                d = Mathf.Sqrt(d);
                return inside ? -d : d;
            };
        }

        public static Sdf Union(params Sdf[] shapes)
        {
            return (x, y) =>
            {
                float d = float.MaxValue;
                foreach (Sdf s in shapes) { d = Mathf.Min(d, s(x, y)); }
                return d;
            };
        }

        public static Sdf Subtract(Sdf a, Sdf b)
        {
            return (x, y) => Mathf.Max(a(x, y), -b(x, y));
        }

        public static Sdf Intersect(Sdf a, Sdf b)
        {
            return (x, y) => Mathf.Max(a(x, y), b(x, y));
        }

        public static Sdf Grow(Sdf a, float amount)
        {
            return (x, y) => a(x, y) - amount;
        }

        public static Sdf Offset(Sdf a, float dx, float dy)
        {
            return (x, y) => a(x - dx, y - dy);
        }

        public static Sdf Ring(float cx, float cy, float radius, float halfThickness)
        {
            return (x, y) => Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - radius) - halfThickness;
        }

        // ---------------------------------------------------------------- painting

        public void Fill(Sdf shape, Color color)
        {
            Fill(shape, (x, y) => color);
        }

        public void Fill(Sdf shape, ColorFn colorFn)
        {
            for (int py = 0; py < Height; py++)
            {
                for (int px = 0; px < Width; px++)
                {
                    float x = px + 0.5f, y = py + 0.5f;
                    float coverage = Mathf.Clamp01(0.5f - shape(x, y));
                    if (coverage <= 0f)
                    {
                        continue;
                    }

                    Color c = colorFn(x, y);
                    Over(px, py, c, coverage);
                }
            }
        }

        public void FillAll(ColorFn colorFn)
        {
            for (int py = 0; py < Height; py++)
            {
                for (int px = 0; px < Width; px++)
                {
                    Over(px, py, colorFn(px + 0.5f, py + 0.5f), 1f);
                }
            }
        }

        public void Overwrite(int px, int py, Color c)
        {
            pixels[py * Width + px] = c;
        }

        private void Over(int px, int py, Color src, float coverage)
        {
            int i = py * Width + px;
            Color dst = pixels[i];
            float sa = src.a * coverage;
            float outA = sa + dst.a * (1f - sa);
            if (outA <= 1e-5f)
            {
                pixels[i] = default;
                return;
            }

            float r = (src.r * sa + dst.r * dst.a * (1f - sa)) / outA;
            float g = (src.g * sa + dst.g * dst.a * (1f - sa)) / outA;
            float b = (src.b * sa + dst.b * dst.a * (1f - sa)) / outA;
            pixels[i] = new Color(r, g, b, outA);
        }

        public Texture2D ToTexture()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
            var flipped = new Color[pixels.Length];
            for (int y = 0; y < Height; y++)
            {
                Array.Copy(pixels, y * Width, flipped, (Height - 1 - y) * Width, Width);
            }

            texture.SetPixels(flipped);
            texture.Apply(false);
            return texture;
        }

        // ---------------------------------------------------------------- colour helpers

        public static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));

        public static ColorFn VerticalGradient(float top, float bottom, Color topColor, Color bottomColor)
        {
            return (x, y) => Color.Lerp(topColor, bottomColor, Mathf.Clamp01((y - top) / Mathf.Max(1e-4f, bottom - top)));
        }

        public static ColorFn HorizontalGradient(float left, float right, Color leftColor, Color rightColor)
        {
            return (x, y) => Color.Lerp(leftColor, rightColor, Mathf.Clamp01((x - left) / Mathf.Max(1e-4f, right - left)));
        }
    }
}
