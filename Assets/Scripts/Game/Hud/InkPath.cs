using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// A small SVG path reader for Painter2D. Parses path data (M L H V C S Q T A Z, absolute and
    /// relative) into move, line and cubic segments, so the hand-drawn icons from the design can be
    /// drawn crisply at any size without image files.
    /// </summary>
    public static class InkPath
    {
        public enum Op : byte { Move, Line, Cubic, Close }

        public readonly struct Seg
        {
            public readonly Op Op;
            public readonly Vector2 A, B, C;   // Move/Line/Close: A. Cubic: controls A and B, end C.

            public Seg(Op op, Vector2 a, Vector2 b = default, Vector2 c = default)
            {
                Op = op;
                A = a;
                B = b;
                C = c;
            }
        }

        /// <summary>Bezier handle length for a quarter circle.</summary>
        public const float Kappa = 0.5522848f;

        public static List<Seg> Parse(string d)
        {
            var segs = new List<Seg>();
            var r = new Reader(d);
            Vector2 cur = Vector2.zero, start = Vector2.zero, lastCtrl = Vector2.zero;
            char cmd = ' ', prev = ' ';
            while (r.SkipSeparators())
            {
                if (r.AtCommand(out char c))
                {
                    cmd = c;
                    r.Advance();
                }
                else if (cmd == ' ' || cmd == 'Z' || cmd == 'z')
                {
                    throw new FormatException("Unexpected number in path: " + d);
                }

                bool rel = char.IsLower(cmd);
                Vector2 o = rel ? cur : Vector2.zero;
                char up = char.ToUpperInvariant(cmd);
                switch (up)
                {
                    case 'M':
                        cur = o + r.Point();
                        start = cur;
                        segs.Add(new Seg(Op.Move, cur));
                        cmd = rel ? 'l' : 'L';   // further pairs are implicit line-tos
                        prev = 'M';
                        continue;
                    case 'L':
                        cur = o + r.Point();
                        segs.Add(new Seg(Op.Line, cur));
                        break;
                    case 'H':
                        cur = new Vector2((rel ? cur.x : 0f) + r.Number(), cur.y);
                        segs.Add(new Seg(Op.Line, cur));
                        break;
                    case 'V':
                        cur = new Vector2(cur.x, (rel ? cur.y : 0f) + r.Number());
                        segs.Add(new Seg(Op.Line, cur));
                        break;
                    case 'C':
                    {
                        var c1 = o + r.Point();
                        var c2 = o + r.Point();
                        var e = o + r.Point();
                        segs.Add(new Seg(Op.Cubic, c1, c2, e));
                        lastCtrl = c2;
                        cur = e;
                        break;
                    }
                    case 'S':
                    {
                        var c1 = prev == 'C' || prev == 'S' ? 2f * cur - lastCtrl : cur;
                        var c2 = o + r.Point();
                        var e = o + r.Point();
                        segs.Add(new Seg(Op.Cubic, c1, c2, e));
                        lastCtrl = c2;
                        cur = e;
                        break;
                    }
                    case 'Q':
                    {
                        var q = o + r.Point();
                        var e = o + r.Point();
                        AddQuad(segs, cur, q, e);
                        lastCtrl = q;
                        cur = e;
                        break;
                    }
                    case 'T':
                    {
                        var q = prev == 'Q' || prev == 'T' ? 2f * cur - lastCtrl : cur;
                        var e = o + r.Point();
                        AddQuad(segs, cur, q, e);
                        lastCtrl = q;
                        cur = e;
                        break;
                    }
                    case 'A':
                    {
                        float rx = r.Number(), ry = r.Number(), rotation = r.Number();
                        bool large = r.Flag(), sweep = r.Flag();
                        var e = o + r.Point();
                        AddArc(segs, cur, rx, ry, rotation, large, sweep, e);
                        cur = e;
                        break;
                    }
                    case 'Z':
                        segs.Add(new Seg(Op.Close, start));
                        cur = start;
                        break;
                    default:
                        throw new FormatException($"Unsupported path command '{cmd}' in: {d}");
                }
                prev = up;
            }
            return segs;
        }

        public static List<Seg> Ellipse(float cx, float cy, float rx, float ry)
        {
            float kx = Kappa * rx, ky = Kappa * ry;
            return new List<Seg>(6)
            {
                new Seg(Op.Move, new Vector2(cx + rx, cy)),
                new Seg(Op.Cubic, new Vector2(cx + rx, cy + ky), new Vector2(cx + kx, cy + ry), new Vector2(cx, cy + ry)),
                new Seg(Op.Cubic, new Vector2(cx - kx, cy + ry), new Vector2(cx - rx, cy + ky), new Vector2(cx - rx, cy)),
                new Seg(Op.Cubic, new Vector2(cx - rx, cy - ky), new Vector2(cx - kx, cy - ry), new Vector2(cx, cy - ry)),
                new Seg(Op.Cubic, new Vector2(cx + kx, cy - ry), new Vector2(cx + rx, cy - ky), new Vector2(cx + rx, cy)),
                new Seg(Op.Close, new Vector2(cx + rx, cy)),
            };
        }

        public static List<Seg> Circle(float cx, float cy, float r) => Ellipse(cx, cy, r, r);

        public static List<Seg> RoundRect(float x, float y, float w, float h, float r)
        {
            r = Mathf.Min(r, w / 2f, h / 2f);
            float k = Kappa * r;
            return new List<Seg>(10)
            {
                new Seg(Op.Move, new Vector2(x + r, y)),
                new Seg(Op.Line, new Vector2(x + w - r, y)),
                new Seg(Op.Cubic, new Vector2(x + w - r + k, y), new Vector2(x + w, y + r - k), new Vector2(x + w, y + r)),
                new Seg(Op.Line, new Vector2(x + w, y + h - r)),
                new Seg(Op.Cubic, new Vector2(x + w, y + h - r + k), new Vector2(x + w - r + k, y + h), new Vector2(x + w - r, y + h)),
                new Seg(Op.Line, new Vector2(x + r, y + h)),
                new Seg(Op.Cubic, new Vector2(x + r - k, y + h), new Vector2(x, y + h - r + k), new Vector2(x, y + h - r)),
                new Seg(Op.Line, new Vector2(x, y + r)),
                new Seg(Op.Cubic, new Vector2(x, y + r - k), new Vector2(x + r - k, y), new Vector2(x + r, y)),
                new Seg(Op.Close, new Vector2(x + r, y)),
            };
        }

        /// <summary>Feeds segments to the painter, scaled and offset. Call BeginPath first.</summary>
        public static void Emit(Painter2D p, List<Seg> segs, Vector2 offset, float scale)
        {
            bool needMove = false;
            Vector2 movePoint = Vector2.zero;
            foreach (var s in segs)
            {
                if (needMove && s.Op != Op.Move && s.Op != Op.Close)
                {
                    p.MoveTo(offset + movePoint * scale);
                    needMove = false;
                }
                switch (s.Op)
                {
                    case Op.Move:
                        p.MoveTo(offset + s.A * scale);
                        needMove = false;
                        break;
                    case Op.Line:
                        p.LineTo(offset + s.A * scale);
                        break;
                    case Op.Cubic:
                        p.BezierCurveTo(offset + s.A * scale, offset + s.B * scale, offset + s.C * scale);
                        break;
                    case Op.Close:
                        p.ClosePath();
                        needMove = true;   // drawing after Z continues from the subpath start
                        movePoint = s.A;
                        break;
                }
            }
        }

        static void AddQuad(List<Seg> segs, Vector2 p0, Vector2 q, Vector2 p1)
        {
            segs.Add(new Seg(Op.Cubic, p0 + 2f / 3f * (q - p0), p1 + 2f / 3f * (q - p1), p1));
        }

        /// <summary>SVG elliptical arc to cubic beziers (SVG spec, appendix F.6).</summary>
        static void AddArc(List<Seg> segs, Vector2 p0, float rx, float ry, float rotationDeg, bool large, bool sweep, Vector2 p1)
        {
            if ((p0 - p1).sqrMagnitude < 1e-10f) return;
            rx = Mathf.Abs(rx);
            ry = Mathf.Abs(ry);
            if (rx < 1e-6f || ry < 1e-6f)
            {
                segs.Add(new Seg(Op.Line, p1));
                return;
            }

            float phi = rotationDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(phi), sin = Mathf.Sin(phi);
            float dx2 = (p0.x - p1.x) / 2f, dy2 = (p0.y - p1.y) / 2f;
            float x1 = cos * dx2 + sin * dy2;
            float y1 = -sin * dx2 + cos * dy2;

            float lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1f)
            {
                float s = Mathf.Sqrt(lambda);
                rx *= s;
                ry *= s;
            }

            float rx2 = rx * rx, ry2 = ry * ry;
            float num = rx2 * ry2 - rx2 * y1 * y1 - ry2 * x1 * x1;
            float den = rx2 * y1 * y1 + ry2 * x1 * x1;
            float coef = (large != sweep ? 1f : -1f) * Mathf.Sqrt(Mathf.Max(0f, num / den));
            float cxp = coef * (rx * y1 / ry);
            float cyp = coef * -(ry * x1 / rx);
            float cx = cos * cxp - sin * cyp + (p0.x + p1.x) / 2f;
            float cy = sin * cxp + cos * cyp + (p0.y + p1.y) / 2f;

            var u = new Vector2((x1 - cxp) / rx, (y1 - cyp) / ry);
            var v = new Vector2((-x1 - cxp) / rx, (-y1 - cyp) / ry);
            float theta = SignedAngle(Vector2.right, u);
            float delta = SignedAngle(u, v);
            if (!sweep && delta > 0f) delta -= 2f * Mathf.PI;
            else if (sweep && delta < 0f) delta += 2f * Mathf.PI;

            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(delta) / (Mathf.PI / 2f) - 1e-4f));
            float step = delta / n;
            float t = 4f / 3f * Mathf.Tan(step / 4f);
            for (int i = 0; i < n; i++)
            {
                float a1 = theta + i * step, a2 = a1 + step;
                float c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1), c2 = Mathf.Cos(a2), s2 = Mathf.Sin(a2);
                var q1 = new Vector2(c1 - t * s1, s1 + t * c1);
                var q2 = new Vector2(c2 + t * s2, s2 - t * c2);
                var e = new Vector2(c2, s2);
                segs.Add(new Seg(Op.Cubic, Map(q1), Map(q2), Map(e)));
            }

            Vector2 Map(Vector2 p) => new Vector2(cx + rx * p.x * cos - ry * p.y * sin, cy + rx * p.x * sin + ry * p.y * cos);
        }

        static float SignedAngle(Vector2 u, Vector2 v) => Mathf.Atan2(u.x * v.y - u.y * v.x, u.x * v.x + u.y * v.y);

        sealed class Reader
        {
            readonly string _s;
            int _i;

            public Reader(string s) => _s = s;

            public bool SkipSeparators()
            {
                while (_i < _s.Length && (char.IsWhiteSpace(_s[_i]) || _s[_i] == ',')) _i++;
                return _i < _s.Length;
            }

            public bool AtCommand(out char c)
            {
                c = _s[_i];
                return char.IsLetter(c);
            }

            public void Advance() => _i++;

            public float Number()
            {
                SkipSeparators();
                int begin = _i;
                if (_i < _s.Length && (_s[_i] == '-' || _s[_i] == '+')) _i++;
                bool dot = false, digits = false;
                while (_i < _s.Length)
                {
                    char ch = _s[_i];
                    if (char.IsDigit(ch))
                    {
                        digits = true;
                        _i++;
                    }
                    else if (ch == '.' && !dot)
                    {
                        dot = true;
                        _i++;
                    }
                    else break;
                }
                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    int save = _i++;
                    if (_i < _s.Length && (_s[_i] == '-' || _s[_i] == '+')) _i++;
                    if (_i < _s.Length && char.IsDigit(_s[_i]))
                        while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                    else _i = save;
                }
                if (!digits) throw new FormatException($"Expected a number at {begin} in: {_s}");
                return float.Parse(_s.Substring(begin, _i - begin), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            public Vector2 Point()
            {
                float x = Number();
                float y = Number();
                return new Vector2(x, y);
            }

            public bool Flag()
            {
                SkipSeparators();
                if (_i >= _s.Length) throw new FormatException("Missing arc flag in: " + _s);
                char ch = _s[_i++];
                if (ch != '0' && ch != '1') throw new FormatException("Expected an arc flag in: " + _s);
                return ch == '1';
            }
        }
    }
}
