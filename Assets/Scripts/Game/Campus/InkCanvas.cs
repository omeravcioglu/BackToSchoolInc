using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>Colour tokens from the "blue ballpoint on graph paper" design canvas.</summary>
    public static class InkPalette
    {
        public static readonly Color Ink = Hex(0x1E3A6D);
        public static readonly Color SoftInk = Hex(0x3F5F93);
        public static readonly Color NoteInk = Hex(0x4A6A9E);
        public static readonly Color Blue = Hex(0x2F6FD0);
        public static readonly Color DeepBlue = Hex(0x1F5CB8);
        public static readonly Color Mid = Hex(0x5A9BEA);
        public static readonly Color Sky = Hex(0xA8D1F7);
        public static readonly Color Pale = Hex(0xDCEBFB);
        public static readonly Color Paper = Hex(0xFBFDFF);
        public static readonly Color Graph = Hex(0xEEF5FD);
        public static readonly Color GraphLine = Hex(0xDCE9F7);
        public static readonly Color Marker = Hex(0xFFE066);
        public static readonly Color Coral = Hex(0xFF8C6E);
        public static readonly Color CoralText = Hex(0xC2462A);
        public static readonly Color CoralDark = Hex(0xB23C20);

        // World
        public static readonly Color Lawn = Hex(0xE4F0FC);
        public static readonly Color LawnHatch = Hex(0xCFE2F6);
        public static readonly Color LawnEdge = Hex(0x9CC3EC);
        public static readonly Color Shadow = Hex(0xC3DAF3);
        public static readonly Color FloorBase = Hex(0xF6FAFF);
        public static readonly Color Foliage = Hex(0xBFDCF6);
        public static readonly Color Hedge = Hex(0xD2E6FA);
        public static readonly Color Board = Hex(0x2B4F8A);
        public static readonly Color SiteHatch = Hex(0xC9DEF5);
        public static readonly Color TileFill = Hex(0xF4F9FF);
        public static readonly Color TileLine = Hex(0xD5E5F6);
        public static readonly Color Wet = Hex(0xD2E6FA);
        public static readonly Color WetLine = Hex(0xB9D5F2);
        public static readonly Color ClassroomFloor = Hex(0xDCEBFB);
        public static readonly Color OfficeFloor = Hex(0xCFE3F8);
        public static readonly Color CafeteriaFloor = Hex(0xE8F2FC);
        public static readonly Color DormFloor = Hex(0xD6E8FA);
        public static readonly Color White = Color.white;

        public static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    /// <summary>
    /// A tiny software rasterizer for hand-drawn art. Shapes are evaluated as signed distance
    /// fields, which gives clean anti-aliasing, and an optional smooth noise "wobble" pushes their
    /// edges around so lines look drawn with a pen. Coordinates are pixels, origin bottom-left.
    /// </summary>
    public sealed class InkCanvas
    {
        public readonly int Width;
        public readonly int Height;

        /// <summary>Maximum edge displacement in pixels (0 = perfectly clean shapes).</summary>
        public float Wobble;
        public float WobbleFrequency = 0.03f;

        readonly float[] _r, _g, _b, _a;
        readonly int _seed;

        delegate float Sdf(float x, float y);

        public InkCanvas(int width, int height, int seed = 1)
        {
            Width = width;
            Height = height;
            _seed = seed;
            int n = width * height;
            _r = new float[n];
            _g = new float[n];
            _b = new float[n];
            _a = new float[n];
            // Transparent pixels carry paper colour so filtering never pulls in dark fringes.
            Clear(InkPalette.WithAlpha(InkPalette.Paper, 0f));
        }

        public void Clear(Color c)
        {
            for (int i = 0; i < _a.Length; i++)
            {
                _r[i] = c.r;
                _g[i] = c.g;
                _b[i] = c.b;
                _a[i] = c.a;
            }
        }

        // ---------------------------------------------------------------------------------
        // Noise
        // ---------------------------------------------------------------------------------

        float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + _seed * 144269504;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFF) / 32767.5f - 1f;
            }
        }

        float Noise(float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0), b = Hash(x0 + 1, y0), c = Hash(x0, y0 + 1), d = Hash(x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        float WobbleAt(float x, float y)
        {
            if (Wobble == 0f) return 0f;
            float f = WobbleFrequency;
            return Wobble * (Noise(x * f, y * f) * 0.7f + Noise(x * f * 2.7f + 31f, y * f * 2.7f - 17f) * 0.3f);
        }

        // ---------------------------------------------------------------------------------
        // Compositing
        // ---------------------------------------------------------------------------------

        void Plot(int x, int y, Color c, float coverage)
        {
            if (coverage <= 0f) return;
            if (coverage > 1f) coverage = 1f;
            int i = y * Width + x;
            float a = c.a * coverage;
            float da = _a[i];
            float outA = a + da * (1f - a);
            if (outA < 1e-5f) return;
            float k = da * (1f - a);
            _r[i] = (c.r * a + _r[i] * k) / outA;
            _g[i] = (c.g * a + _g[i] * k) / outA;
            _b[i] = (c.b * a + _b[i] * k) / outA;
            _a[i] = outA;
        }

        void Shape(float minX, float minY, float maxX, float maxY, Color c, Sdf sdf)
        {
            float pad = 2f + Mathf.Abs(Wobble) * 1.3f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX - pad));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY - pad));
            int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(maxX + pad));
            int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY + pad));
            for (int y = y0; y <= y1; y++)
            {
                float py = y + 0.5f;
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f;
                    float d = sdf(px, py) + WobbleAt(px, py);
                    if (d < 0.5f) Plot(x, y, c, 0.5f - d);
                }
            }
        }

        static float BoxSdf(float px, float py, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px - cx) - (hx - r);
            float qy = Mathf.Abs(py - cy) - (hy - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        // ---------------------------------------------------------------------------------
        // Primitives
        // ---------------------------------------------------------------------------------

        public void FillRect(float x0, float y0, float x1, float y1, Color c, float radius = 0f)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, hx = Mathf.Abs(x1 - x0) * 0.5f, hy = Mathf.Abs(y1 - y0) * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(hx, hy));
            Shape(cx - hx, cy - hy, cx + hx, cy + hy, c, (px, py) => BoxSdf(px, py, cx, cy, hx, hy, radius));
        }

        public void StrokeRect(float x0, float y0, float x1, float y1, Color c, float width, float radius = 0f)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f, hx = Mathf.Abs(x1 - x0) * 0.5f, hy = Mathf.Abs(y1 - y0) * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(hx, hy));
            float hw = width * 0.5f;
            Shape(cx - hx - hw, cy - hy - hw, cx + hx + hw, cy + hy + hw, c,
                (px, py) => Mathf.Abs(BoxSdf(px, py, cx, cy, hx, hy, radius)) - hw);
        }

        /// <summary>Filled, ink-outlined rectangle: the basic furniture shape.</summary>
        public void Box(float x0, float y0, float x1, float y1, Color fill, float stroke = 2.5f, float radius = 4f)
        {
            FillRect(x0, y0, x1, y1, fill, radius);
            if (stroke > 0f) StrokeRect(x0, y0, x1, y1, InkPalette.Ink, stroke, radius);
        }

        public void FillCircle(float cx, float cy, float r, Color c)
        {
            Shape(cx - r, cy - r, cx + r, cy + r, c, (px, py) =>
            {
                float dx = px - cx, dy = py - cy;
                return Mathf.Sqrt(dx * dx + dy * dy) - r;
            });
        }

        public void StrokeCircle(float cx, float cy, float r, Color c, float width)
        {
            float hw = width * 0.5f;
            Shape(cx - r - hw, cy - r - hw, cx + r + hw, cy + r + hw, c, (px, py) =>
            {
                float dx = px - cx, dy = py - cy;
                return Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) - hw;
            });
        }

        public void Dot(float cx, float cy, float r, Color fill, float stroke = 2f)
        {
            FillCircle(cx, cy, r, fill);
            if (stroke > 0f) StrokeCircle(cx, cy, r, InkPalette.Ink, stroke);
        }

        public void FillEllipse(float cx, float cy, float rx, float ry, Color c)
        {
            float m = Mathf.Min(rx, ry);
            Shape(cx - rx, cy - ry, cx + rx, cy + ry, c, (px, py) =>
            {
                float dx = (px - cx) / rx, dy = (py - cy) / ry;
                return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * m;
            });
        }

        public void StrokeEllipse(float cx, float cy, float rx, float ry, Color c, float width)
        {
            float m = Mathf.Min(rx, ry), hw = width * 0.5f;
            Shape(cx - rx - hw, cy - ry - hw, cx + rx + hw, cy + ry + hw, c, (px, py) =>
            {
                float dx = (px - cx) / rx, dy = (py - cy) / ry;
                return Mathf.Abs((Mathf.Sqrt(dx * dx + dy * dy) - 1f) * m) - hw;
            });
        }

        /// <summary>A line with round caps. Dash and gap are in pixels along the line.</summary>
        public void Line(float x0, float y0, float x1, float y1, Color c, float width, float dash = 0f, float gap = 0f, float phase = 0f)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4f)
            {
                FillCircle(x0, y0, width * 0.5f, c);
                return;
            }
            float ux = dx / len, uy = dy / len, hw = width * 0.5f;
            float period = dash + gap;
            Shape(Mathf.Min(x0, x1) - hw, Mathf.Min(y0, y1) - hw, Mathf.Max(x0, x1) + hw, Mathf.Max(y0, y1) + hw, c, (px, py) =>
            {
                float t = (px - x0) * ux + (py - y0) * uy;
                float perp = -(px - x0) * uy + (py - y0) * ux;
                if (dash > 0f && period > 0f)
                {
                    // Distance to the nearest dash, each dash being a round-capped segment.
                    float k = Mathf.Floor((t + phase) / period);
                    float best = float.MaxValue;
                    for (int j = -1; j <= 1; j++)
                    {
                        float s0 = Mathf.Max((k + j) * period - phase, 0f);
                        float s1 = Mathf.Min((k + j) * period - phase + dash, len);
                        if (s1 < s0) continue;
                        float along = t - Mathf.Clamp(t, s0, s1);
                        best = Mathf.Min(best, Mathf.Sqrt(along * along + perp * perp));
                    }
                    return best - hw;
                }
                float tc = Mathf.Clamp(t, 0f, len);
                float ex = px - (x0 + ux * tc), ey = py - (y0 + uy * tc);
                return Mathf.Sqrt(ex * ex + ey * ey) - hw;
            });
        }

        /// <summary>Arc from angle a0 to a1 (radians, counter-clockwise).</summary>
        public void Arc(float cx, float cy, float r, float a0, float a1, Color c, float width, float dash = 0f, float gap = 0f)
        {
            float hw = width * 0.5f, span = a1 - a0, period = dash + gap;
            Shape(cx - r - hw, cy - r - hw, cx + r + hw, cy + r + hw, c, (px, py) =>
            {
                float dx = px - cx, dy = py - cy;
                float ang = Mathf.Repeat(Mathf.Atan2(dy, dx) - a0, Mathf.PI * 2f);
                if (ang > span)
                {
                    // Distance to the nearer end point.
                    float e0x = cx + Mathf.Cos(a0) * r - px, e0y = cy + Mathf.Sin(a0) * r - py;
                    float e1x = cx + Mathf.Cos(a1) * r - px, e1y = cy + Mathf.Sin(a1) * r - py;
                    return Mathf.Sqrt(Mathf.Min(e0x * e0x + e0y * e0y, e1x * e1x + e1y * e1y)) - hw;
                }
                if (dash > 0f && period > 0f && Mathf.Repeat(ang * r, period) > dash) return 99f;
                return Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) - hw;
            });
        }

        public void FillTriangle(float ax, float ay, float bx, float by, float cx, float cy, Color c)
        {
            Shape(Mathf.Min(ax, Mathf.Min(bx, cx)), Mathf.Min(ay, Mathf.Min(by, cy)),
                  Mathf.Max(ax, Mathf.Max(bx, cx)), Mathf.Max(ay, Mathf.Max(by, cy)), c,
                  (px, py) => TriangleSdf(px, py, ax, ay, bx, by, cx, cy));
        }

        public void StrokeTriangle(float ax, float ay, float bx, float by, float cx, float cy, Color c, float width)
        {
            float hw = width * 0.5f;
            Shape(Mathf.Min(ax, Mathf.Min(bx, cx)) - hw, Mathf.Min(ay, Mathf.Min(by, cy)) - hw,
                  Mathf.Max(ax, Mathf.Max(bx, cx)) + hw, Mathf.Max(ay, Mathf.Max(by, cy)) + hw, c,
                  (px, py) => Mathf.Abs(TriangleSdf(px, py, ax, ay, bx, by, cx, cy)) - hw);
        }

        static float TriangleSdf(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            // Inigo Quilez's exact triangle distance.
            float e0x = bx - ax, e0y = by - ay, e1x = cx - bx, e1y = cy - by, e2x = ax - cx, e2y = ay - cy;
            float v0x = px - ax, v0y = py - ay, v1x = px - bx, v1y = py - by, v2x = px - cx, v2y = py - cy;
            float t0 = Mathf.Clamp01((v0x * e0x + v0y * e0y) / (e0x * e0x + e0y * e0y));
            float t1 = Mathf.Clamp01((v1x * e1x + v1y * e1y) / (e1x * e1x + e1y * e1y));
            float t2 = Mathf.Clamp01((v2x * e2x + v2y * e2y) / (e2x * e2x + e2y * e2y));
            float p0x = v0x - e0x * t0, p0y = v0y - e0y * t0;
            float p1x = v1x - e1x * t1, p1y = v1y - e1y * t1;
            float p2x = v2x - e2x * t2, p2y = v2y - e2y * t2;
            float s = Mathf.Sign(e0x * e2y - e0y * e2x);
            float d0 = p0x * p0x + p0y * p0y, s0 = s * (v0x * e0y - v0y * e0x);
            float d1 = p1x * p1x + p1y * p1y, s1 = s * (v1x * e1y - v1y * e1x);
            float d2 = p2x * p2x + p2y * p2y, s2 = s * (v2x * e2y - v2y * e2x);
            float d = Mathf.Min(d0, Mathf.Min(d1, d2));
            float sg = Mathf.Min(s0, Mathf.Min(s1, s2));
            return -Mathf.Sqrt(d) * Mathf.Sign(sg);
        }

        /// <summary>Fills polygons (closed rings of pixel points) with the non-zero winding rule, anti-aliased.</summary>
        public void FillPolygons(List<Vector2[]> rings, Color c)
        {
            if (rings == null || rings.Count == 0 || c.a <= 0f) return;
            Bounds(rings, 0f, out float minX, out float minY, out float maxX, out float maxY);
            Shape(minX, minY, maxX, maxY, c, (px, py) =>
            {
                float best = float.MaxValue;
                int winding = 0;
                foreach (var ring in rings)
                {
                    int n = ring.Length;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        Vector2 a = ring[j], b = ring[i];
                        float ex = b.x - a.x, ey = b.y - a.y, wx = px - a.x, wy = py - a.y;
                        float len2 = ex * ex + ey * ey;
                        float t = len2 > 1e-9f ? Mathf.Clamp01((wx * ex + wy * ey) / len2) : 0f;
                        float dx = wx - ex * t, dy = wy - ey * t;
                        float d2 = dx * dx + dy * dy;
                        if (d2 < best) best = d2;
                        float cross = ex * wy - ey * wx;
                        if (a.y <= py) { if (b.y > py && cross > 0f) winding++; }
                        else if (b.y <= py && cross < 0f) winding--;
                    }
                }
                float d = Mathf.Sqrt(best);
                return winding != 0 ? -d : d;
            });
        }

        /// <summary>Strokes polylines (pixel points) with round caps and joins.</summary>
        public void StrokePolylines(List<Vector2[]> lines, Color c, float width)
        {
            if (lines == null || lines.Count == 0 || c.a <= 0f || width <= 0f) return;
            float hw = width * 0.5f;
            Bounds(lines, hw, out float minX, out float minY, out float maxX, out float maxY);
            Shape(minX, minY, maxX, maxY, c, (px, py) =>
            {
                float best = float.MaxValue;
                foreach (var line in lines)
                {
                    if (line.Length == 1)
                    {
                        float ox = px - line[0].x, oy = py - line[0].y;
                        best = Mathf.Min(best, ox * ox + oy * oy);
                        continue;
                    }
                    for (int i = 1; i < line.Length; i++)
                    {
                        Vector2 a = line[i - 1], b = line[i];
                        float ex = b.x - a.x, ey = b.y - a.y, wx = px - a.x, wy = py - a.y;
                        float len2 = ex * ex + ey * ey;
                        float t = len2 > 1e-9f ? Mathf.Clamp01((wx * ex + wy * ey) / len2) : 0f;
                        float dx = wx - ex * t, dy = wy - ey * t;
                        float d2 = dx * dx + dy * dy;
                        if (d2 < best) best = d2;
                    }
                }
                return Mathf.Sqrt(best) - hw;
            });
        }

        static void Bounds(List<Vector2[]> rings, float pad, out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;
            foreach (var ring in rings)
                foreach (var p in ring)
                {
                    if (p.x < minX) minX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y > maxY) maxY = p.y;
                }
            minX -= pad; minY -= pad; maxX += pad; maxY += pad;
        }

        /// <summary>A short wavy scribble (chalk writing, water ripples).</summary>
        public void Squiggle(float x0, float y, float length, float amplitude, float waveLength, Color c, float width)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(length / 4f));
            float px = x0, py = y;
            for (int i = 1; i <= steps; i++)
            {
                float x = x0 + length * i / steps;
                float yy = y + Mathf.Sin((x - x0) / waveLength * Mathf.PI * 2f) * amplitude;
                Line(px, py, x, yy, c, width);
                px = x;
                py = yy;
            }
        }

        // ---------------------------------------------------------------------------------
        // Output
        // ---------------------------------------------------------------------------------

        /// <summary>Returns a copy turned counter-clockwise by 90° × <paramref name="quarterTurns"/>.</summary>
        public InkCanvas Rotated(int quarterTurns)
        {
            quarterTurns &= 3;
            if (quarterTurns == 0) return this;
            bool swap = (quarterTurns & 1) == 1;
            var dst = new InkCanvas(swap ? Height : Width, swap ? Width : Height, _seed);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int nx, ny;
                    switch (quarterTurns)
                    {
                        case 1: nx = Height - 1 - y; ny = x; break;
                        case 2: nx = Width - 1 - x; ny = Height - 1 - y; break;
                        default: nx = y; ny = Width - 1 - x; break;
                    }
                    int s = y * Width + x, d = ny * dst.Width + nx;
                    dst._r[d] = _r[s];
                    dst._g[d] = _g[s];
                    dst._b[d] = _b[s];
                    dst._a[d] = _a[s];
                }
            }
            return dst;
        }

        /// <summary>Makes a texture of the canvas. It frees its CPU copy unless <paramref name="readable"/> (to save it as a file).</summary>
        public Texture2D ToTexture(bool mipmaps = true, string name = null, bool readable = false)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, mipmaps)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 2,
                name = name ?? "InkCanvas",
            };
            var px = new Color32[Width * Height];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(
                    (byte)(Mathf.Clamp01(_r[i]) * 255f + 0.5f),
                    (byte)(Mathf.Clamp01(_g[i]) * 255f + 0.5f),
                    (byte)(Mathf.Clamp01(_b[i]) * 255f + 0.5f),
                    (byte)(Mathf.Clamp01(_a[i]) * 255f + 0.5f));
            }
            tex.SetPixels32(px);
            tex.Apply(mipmaps, !readable);
            return tex;
        }
    }
}
