using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// A hand-drawn picture kept as a list of SVG-style shapes (fills, strokes, dashes, hatching)
    /// in a fixed design box, drawn with Painter2D at any scale. Used for the title backdrop, the
    /// level-up confetti and other illustrations from the design canvas.
    /// </summary>
    public sealed class InkDrawing
    {
        sealed class Op
        {
            public List<InkPath.Seg> Segs;
            public List<Vector2> Lines;      // pre-cut line pairs (hatching), drawn as strokes
            public Color Fill, Stroke;
            public float Width, DashOn, DashOff;
            public bool Round = true;
        }

        readonly List<Op> _ops = new List<Op>();
        public readonly float Width, Height;

        public InkDrawing(float width, float height)
        {
            Width = width;
            Height = height;
        }

        // --- Building -----------------------------------------------------------------

        public InkDrawing Shape(string d, Color fill, Color stroke = default, float width = 0f) =>
            Add(InkPath.Parse(d), fill, stroke, width);

        public InkDrawing Line(string d, Color stroke, float width, bool round = true)
        {
            Add(InkPath.Parse(d), Color.clear, stroke, width);
            _ops[_ops.Count - 1].Round = round;
            return this;
        }

        public InkDrawing Dashed(string d, Color stroke, float width, float on, float off, bool round = false)
        {
            Add(InkPath.Parse(d), Color.clear, stroke, width);
            var op = _ops[_ops.Count - 1];
            op.DashOn = on;
            op.DashOff = off;
            op.Round = round;
            return this;
        }

        public InkDrawing Circle(float cx, float cy, float r, Color fill, Color stroke = default, float width = 0f) =>
            Add(InkPath.Circle(cx, cy, r), fill, stroke, width);

        public InkDrawing Ellipse(float cx, float cy, float rx, float ry, Color fill, Color stroke = default, float width = 0f) =>
            Add(InkPath.Ellipse(cx, cy, rx, ry), fill, stroke, width);

        public InkDrawing Rect(float x, float y, float w, float h, Color fill, Color stroke = default, float width = 0f, float radius = 0f) =>
            Add(InkPath.RoundRect(x, y, w, h, radius), fill, stroke, width);

        /// <summary>A rounded rectangle turned by <paramref name="degrees"/> around (cx, cy), like SVG's rotate().</summary>
        public InkDrawing RotatedRect(float x, float y, float w, float h, float radius, float degrees, float cx, float cy,
            Color fill, Color stroke = default, float width = 0f)
        {
            float a = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            var c = new Vector2(cx, cy);
            Vector2 Rot(Vector2 p)
            {
                var d = p - c;
                return c + new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);
            }
            return Add(Map(InkPath.RoundRect(x, y, w, h, radius), Rot), fill, stroke, width);
        }

        /// <summary>Fills a closed shape with parallel lines (an SVG hatch pattern), cut to the shape's outline.</summary>
        public InkDrawing Hatch(string d, Color stroke, float width, float spacing, float degrees)
        {
            var outline = Flatten(InkPath.Parse(d));
            if (outline.Count == 0) return this;
            float a = degrees * Mathf.Deg2Rad;
            var lines = new List<Vector2>();
            HatchLines(outline[0], new Vector2(Mathf.Cos(a), Mathf.Sin(a)), spacing, lines);
            _ops.Add(new Op { Lines = lines, Stroke = stroke, Width = width, Round = false });
            return this;
        }

        /// <summary>Adds another drawing's shapes, moved and scaled (e.g. a small figure in a scene).</summary>
        public InkDrawing Place(InkDrawing other, float x, float y, float scale)
        {
            var offset = new Vector2(x, y);
            foreach (var op in other._ops)
            {
                _ops.Add(new Op
                {
                    Segs = op.Segs != null ? Map(op.Segs, p => offset + p * scale) : null,
                    Lines = op.Lines?.ConvertAll(p => offset + p * scale),
                    Fill = op.Fill,
                    Stroke = op.Stroke,
                    Width = op.Width * scale,
                    DashOn = op.DashOn * scale,
                    DashOff = op.DashOff * scale,
                    Round = op.Round,
                });
            }
            return this;
        }

        InkDrawing Add(List<InkPath.Seg> segs, Color fill, Color stroke, float width)
        {
            _ops.Add(new Op { Segs = segs, Fill = fill, Stroke = stroke, Width = width });
            return this;
        }

        static List<InkPath.Seg> Map(List<InkPath.Seg> segs, Func<Vector2, Vector2> f)
        {
            var result = new List<InkPath.Seg>(segs.Count);
            foreach (var s in segs) result.Add(new InkPath.Seg(s.Op, f(s.A), f(s.B), f(s.C)));
            return result;
        }

        /// <summary>Adds an already-parsed path (e.g. an icon's shape) with its own colours.</summary>
        public InkDrawing AddPath(List<InkPath.Seg> segs, Color fill, Color stroke, float width) => Add(segs, fill, stroke, width);

        // --- Drawing ------------------------------------------------------------------

        /// <summary>
        /// Paints the picture into a software canvas (for campus sprites), with its design box
        /// mapped to offset + point * scale. The canvas's origin is bottom-left, so y flips.
        /// </summary>
        public void Rasterize(InkCanvas canvas, Vector2 offset, float scale)
        {
            float height = canvas.Height;
            Vector2 Map(Vector2 p) => new Vector2(offset.x + p.x * scale, height - (offset.y + p.y * scale));
            foreach (var op in _ops)
            {
                if (op.Lines != null)
                {
                    var pairs = new List<Vector2[]>(op.Lines.Count / 2);
                    for (int i = 0; i + 1 < op.Lines.Count; i += 2) pairs.Add(new[] { Map(op.Lines[i]), Map(op.Lines[i + 1]) });
                    canvas.StrokePolylines(pairs, op.Stroke, op.Width * scale);
                    continue;
                }
                var rings = Flatten(op.Segs);
                if (rings.Count == 0) continue;
                var mapped = new List<Vector2[]>(rings.Count);
                foreach (var ring in rings) mapped.Add(ring.ConvertAll(Map).ToArray());
                if (op.Fill.a > 0f) canvas.FillPolygons(mapped, op.Fill);
                if (op.Width > 0f && op.Stroke.a > 0f) canvas.StrokePolylines(mapped, op.Stroke, op.Width * scale);
            }
        }

        /// <summary>Draws the picture with its design box mapped to offset + point * scale.</summary>
        public void Draw(Painter2D p, Vector2 offset, float scale)
        {
            p.lineJoin = LineJoin.Round;
            foreach (var op in _ops)
            {
                if (op.Lines != null)
                {
                    if (op.Lines.Count < 2) continue;
                    p.strokeColor = op.Stroke;
                    p.lineWidth = op.Width * scale;
                    p.lineCap = op.Round ? LineCap.Round : LineCap.Butt;
                    p.BeginPath();
                    for (int i = 0; i + 1 < op.Lines.Count; i += 2)
                    {
                        p.MoveTo(offset + op.Lines[i] * scale);
                        p.LineTo(offset + op.Lines[i + 1] * scale);
                    }
                    p.Stroke();
                    continue;
                }

                if (op.Fill.a > 0f)
                {
                    p.fillColor = op.Fill;
                    p.BeginPath();
                    InkPath.Emit(p, op.Segs, offset, scale);
                    p.Fill();
                }
                if (op.Width <= 0f || op.Stroke.a <= 0f) continue;
                p.strokeColor = op.Stroke;
                p.lineWidth = op.Width * scale;
                p.lineCap = op.Round ? LineCap.Round : LineCap.Butt;
                p.BeginPath();
                if (op.DashOn > 0f) EmitDashes(p, op.Segs, offset, scale, op.DashOn, op.DashOff);
                else InkPath.Emit(p, op.Segs, offset, scale);
                p.Stroke();
            }
        }

        static void EmitDashes(Painter2D p, List<InkPath.Seg> segs, Vector2 offset, float scale, float on, float off)
        {
            foreach (var line in Flatten(segs))
            {
                bool drawing = true;
                float remain = on;
                Vector2 start = line[0];
                for (int i = 1; i < line.Count; i++)
                {
                    Vector2 a = line[i - 1], b = line[i];
                    float len = Vector2.Distance(a, b), pos = 0f;
                    while (len - pos > 1e-4f)
                    {
                        float step = Mathf.Min(remain, len - pos);
                        pos += step;
                        remain -= step;
                        if (remain > 1e-4f) continue;
                        var at = Vector2.Lerp(a, b, pos / len);
                        if (drawing)
                        {
                            p.MoveTo(offset + start * scale);
                            p.LineTo(offset + at * scale);
                        }
                        else start = at;
                        drawing = !drawing;
                        remain = drawing ? on : off;
                    }
                }
                if (drawing && remain < on)
                {
                    p.MoveTo(offset + start * scale);
                    p.LineTo(offset + line[line.Count - 1] * scale);
                }
            }
        }

        /// <summary>Path segments to polylines, one per subpath.</summary>
        public static List<List<Vector2>> Flatten(List<InkPath.Seg> segs)
        {
            var result = new List<List<Vector2>>();
            List<Vector2> current = null;
            Vector2 cur = Vector2.zero;
            foreach (var s in segs)
            {
                switch (s.Op)
                {
                    case InkPath.Op.Move:
                        current = new List<Vector2> { s.A };
                        result.Add(current);
                        cur = s.A;
                        break;
                    case InkPath.Op.Line:
                        if (current == null) { current = new List<Vector2> { cur }; result.Add(current); }
                        current.Add(s.A);
                        cur = s.A;
                        break;
                    case InkPath.Op.Cubic:
                        if (current == null) { current = new List<Vector2> { cur }; result.Add(current); }
                        for (int i = 1; i <= 16; i++)
                        {
                            float t = i / 16f, u = 1f - t;
                            current.Add(u * u * u * cur + 3f * u * u * t * s.A + 3f * u * t * t * s.B + t * t * t * s.C);
                        }
                        cur = s.C;
                        break;
                    case InkPath.Op.Close:
                        if (current != null && current.Count > 0) current.Add(current[0]);
                        cur = s.A;
                        current = null;
                        break;
                }
            }
            result.RemoveAll(l => l.Count < 2);
            return result;
        }

        static void HatchLines(List<Vector2> polygon, Vector2 dir, float spacing, List<Vector2> pairs)
        {
            var n = new Vector2(-dir.y, dir.x);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in polygon)
            {
                float t = Vector2.Dot(v, n);
                min = Mathf.Min(min, t);
                max = Mathf.Max(max, t);
            }
            var hits = new List<float>();
            for (float c = Mathf.Ceil(min / spacing) * spacing; c <= max; c += spacing)
            {
                hits.Clear();
                for (int i = 0; i < polygon.Count; i++)
                {
                    Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                    float da = Vector2.Dot(a, n) - c, db = Vector2.Dot(b, n) - c;
                    if (da > 0f == db > 0f) continue;
                    var hit = Vector2.Lerp(a, b, da / (da - db));
                    hits.Add(Vector2.Dot(hit, dir));
                }
                hits.Sort();
                for (int i = 0; i + 1 < hits.Count; i += 2)
                {
                    pairs.Add(n * c + dir * hits[i]);
                    pairs.Add(n * c + dir * hits[i + 1]);
                }
            }
        }
    }

    /// <summary>Shows an <see cref="InkDrawing"/>, scaled to fit (or to cover) the element.</summary>
    public class InkPicture : VisualElement
    {
        InkDrawing _drawing;
        bool _cover;

        public InkPicture() : this(null) { }

        public InkPicture(InkDrawing drawing, bool cover = false)
        {
            _drawing = drawing;
            _cover = cover;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public InkDrawing Drawing
        {
            get => _drawing;
            set
            {
                _drawing = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Scale and offset that map the drawing's box onto this element.</summary>
        public void Fit(out Vector2 offset, out float scale)
        {
            float w = layout.width, h = layout.height;
            float sx = w / _drawing.Width, sy = h / _drawing.Height;
            scale = _cover ? Mathf.Max(sx, sy) : Mathf.Min(sx, sy);
            offset = new Vector2((w - _drawing.Width * scale) / 2f, (h - _drawing.Height * scale) / 2f);
        }

        protected virtual void Background(Painter2D p, float w, float h, Vector2 offset, float scale) { }

        void Draw(MeshGenerationContext ctx)
        {
            if (_drawing == null) return;
            float w = layout.width, h = layout.height;
            if (w < 1f || h < 1f || float.IsNaN(w) || float.IsNaN(h)) return;
            Fit(out var offset, out float scale);
            Background(ctx.painter2D, w, h, offset, scale);
            _drawing.Draw(ctx.painter2D, offset, scale);
        }
    }
}
