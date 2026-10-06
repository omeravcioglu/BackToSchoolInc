using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// A hand-drawn box: paper fill, ink outline, a solid offset shadow and wobbly elliptical
    /// corners, none of which USS can draw. Styled from USS through custom properties:
    /// <c>--ink-fill</c>, <c>--ink-border</c>, <c>--ink-border-width</c>, <c>--ink-shadow</c>,
    /// <c>--ink-shadow-x</c>, <c>--ink-shadow-y</c>, <c>--ink-dash</c> (dash length, 0 = solid),
    /// <c>--ink-dots</c> (dotted-paper colour), <c>--ink-lines</c> (ruled-paper colour) and
    /// <c>--ink-radius</c>, a CSS border-radius string such as "22 8 20 10 / 10 20 8 22" or
    /// "50% 44% 52% 46%". Use padding, not USS borders, to keep children off the outline.
    /// </summary>
    [UxmlElement]
    public partial class InkBox : VisualElement
    {
        static readonly CustomStyleProperty<Color> FillProp = new CustomStyleProperty<Color>("--ink-fill");
        static readonly CustomStyleProperty<Color> BorderProp = new CustomStyleProperty<Color>("--ink-border");
        static readonly CustomStyleProperty<float> BorderWidthProp = new CustomStyleProperty<float>("--ink-border-width");
        static readonly CustomStyleProperty<Color> ShadowProp = new CustomStyleProperty<Color>("--ink-shadow");
        static readonly CustomStyleProperty<float> ShadowXProp = new CustomStyleProperty<float>("--ink-shadow-x");
        static readonly CustomStyleProperty<float> ShadowYProp = new CustomStyleProperty<float>("--ink-shadow-y");
        static readonly CustomStyleProperty<float> DashProp = new CustomStyleProperty<float>("--ink-dash");
        static readonly CustomStyleProperty<string> RadiusProp = new CustomStyleProperty<string>("--ink-radius");
        static readonly CustomStyleProperty<Color> DotsProp = new CustomStyleProperty<Color>("--ink-dots");
        static readonly CustomStyleProperty<Color> LinesProp = new CustomStyleProperty<Color>("--ink-lines");

        Color _fill, _border = InkIcons.Ink, _shadow, _dots, _lines;
        float _borderWidth, _shadowX, _shadowY, _dash;
        string _radiusSource;

        // Horizontal radii tl, tr, br, bl then vertical radii tl, tr, br, bl; percentages flagged.
        readonly float[] _radius = new float[8];
        readonly bool[] _percent = new bool[8];
        readonly float[] _resolved = new float[8];

        Color? _fillOverride;

        public InkBox()
        {
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }

        /// <summary>Overrides the USS fill colour from code (swatches, the school's crest colour).</summary>
        public void SetFill(Color? color)
        {
            _fillOverride = color;
            MarkDirtyRepaint();
        }

        void OnStyleResolved(CustomStyleResolvedEvent e)
        {
            var s = e.customStyle;
            _fill = s.TryGetValue(FillProp, out var fill) ? fill : Color.clear;
            _border = s.TryGetValue(BorderProp, out var border) ? border : InkIcons.Ink;
            _borderWidth = s.TryGetValue(BorderWidthProp, out var bw) ? bw : 0f;
            _shadow = s.TryGetValue(ShadowProp, out var shadow) ? shadow : Color.clear;
            _shadowX = s.TryGetValue(ShadowXProp, out var sx) ? sx : 0f;
            _shadowY = s.TryGetValue(ShadowYProp, out var sy) ? sy : 0f;
            _dash = s.TryGetValue(DashProp, out var dash) ? dash : 0f;
            _dots = s.TryGetValue(DotsProp, out var dots) ? dots : Color.clear;
            _lines = s.TryGetValue(LinesProp, out var lines) ? lines : Color.clear;
            s.TryGetValue(RadiusProp, out var radius);
            if (radius != _radiusSource)
            {
                _radiusSource = radius;
                ParseRadius(radius);
            }
            MarkDirtyRepaint();
        }

        void ParseRadius(string text)
        {
            for (int i = 0; i < 8; i++)
            {
                _radius[i] = 0f;
                _percent[i] = false;
            }
            if (string.IsNullOrWhiteSpace(text)) return;
            var halves = text.Split('/');
            Expand(halves[0], 0);
            Expand(halves.Length > 1 ? halves[1] : halves[0], 4);
        }

        void Expand(string part, int offset)
        {
            var tokens = part.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return;
            var v = new float[4];
            var p = new bool[4];
            for (int i = 0; i < tokens.Length && i < 4; i++)
            {
                string t = tokens[i].Trim().Replace("px", "");
                p[i] = t.EndsWith("%");
                if (p[i]) t = t.Substring(0, t.Length - 1);
                float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v[i]);
            }
            // CSS shorthand: 1 = all, 2 = tl+br / tr+bl, 3 = tl / tr+bl / br, 4 = tl tr br bl.
            int[] map = tokens.Length switch
            {
                1 => new[] { 0, 0, 0, 0 },
                2 => new[] { 0, 1, 0, 1 },
                3 => new[] { 0, 1, 2, 1 },
                _ => new[] { 0, 1, 2, 3 },
            };
            for (int i = 0; i < 4; i++)
            {
                _radius[offset + i] = v[map[i]];
                _percent[offset + i] = p[map[i]];
            }
        }

        void Resolve(float w, float h)
        {
            for (int i = 0; i < 8; i++)
                _resolved[i] = _percent[i] ? _radius[i] * (i < 4 ? w : h) / 100f : _radius[i];
            // CSS rule: shrink every radius by the same factor until adjacent corners fit.
            float f = 1f;
            f = Fit(f, w, _resolved[0] + _resolved[1]);
            f = Fit(f, w, _resolved[3] + _resolved[2]);
            f = Fit(f, h, _resolved[4] + _resolved[7]);
            f = Fit(f, h, _resolved[5] + _resolved[6]);
            if (f < 1f)
                for (int i = 0; i < 8; i++) _resolved[i] *= f;
        }

        static float Fit(float f, float side, float sum) => sum > side && sum > 0f ? Mathf.Min(f, side / sum) : f;

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 1f || h < 1f || float.IsNaN(w) || float.IsNaN(h)) return;
            Resolve(w, h);
            var p = ctx.painter2D;

            if (_shadow.a > 0f && (_shadowX != 0f || _shadowY != 0f))
            {
                p.fillColor = _shadow;
                p.BeginPath();
                Trace(p, _shadowX, _shadowY, w, h, 0f);
                p.Fill();
            }
            var fill = _fillOverride ?? _fill;
            if (fill.a > 0f)
            {
                p.fillColor = fill;
                p.BeginPath();
                Trace(p, 0f, 0f, w, h, 0f);
                p.Fill();
            }
            if (_dots.a > 0f) Dots(ctx, w, h);
            if (_lines.a > 0f) Lines(p, w, h);
            if (_borderWidth > 0f && _border.a > 0f)
            {
                float half = _borderWidth * 0.5f;
                p.strokeColor = _border;
                p.lineWidth = _borderWidth;
                p.lineJoin = LineJoin.Round;
                if (_dash > 0f)
                {
                    p.lineCap = LineCap.Butt;
                    Dashes(p, half, half, w - _borderWidth, h - _borderWidth, half, _dash, _dash * 0.7f);
                }
                else
                {
                    p.BeginPath();
                    Trace(p, half, half, w - _borderWidth, h - _borderWidth, half);
                    p.Stroke();
                }
            }
        }

        /// <summary>
        /// Dotted notebook paper: a 20 px grid of dots, kept clear of the rounded corners. The dots are
        /// plain quads in batches; as one anti-aliased Painter2D fill, a page on a tall or wide screen
        /// passed the 65535 vertices a single mesh can hold.
        /// </summary>
        void Dots(MeshGenerationContext ctx, float w, float h)
        {
            const float step = 20f, half = 1.2f;
            const int DotsPerMesh = 8000;   // four vertices each
            float margin = 12f;
            for (int i = 0; i < 8; i++) margin = Mathf.Max(margin, _resolved[i] * 0.6f);
            int columns = Mathf.FloorToInt((w - 2f * margin) / step) + 1;
            int rows = Mathf.FloorToInt((h - 2f * margin) / step) + 1;
            if (columns <= 0 || rows <= 0) return;
            int total = columns * rows;
            Color32 tint = _dots;
            for (int first = 0; first < total; first += DotsPerMesh)
            {
                int count = Mathf.Min(DotsPerMesh, total - first);
                var mesh = ctx.Allocate(count * 4, count * 6);
                for (int k = 0; k < count; k++)
                {
                    int i = first + k;
                    float x = margin + i % columns * step, y = margin + i / columns * step;
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x - half, y - half, Vertex.nearZ), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x + half, y - half, Vertex.nearZ), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x + half, y + half, Vertex.nearZ), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x - half, y + half, Vertex.nearZ), tint = tint });
                    ushort v = (ushort)(k * 4);
                    mesh.SetNextIndex(v);
                    mesh.SetNextIndex((ushort)(v + 1));
                    mesh.SetNextIndex((ushort)(v + 2));
                    mesh.SetNextIndex(v);
                    mesh.SetNextIndex((ushort)(v + 2));
                    mesh.SetNextIndex((ushort)(v + 3));
                }
            }
        }

        /// <summary>Ruled index-card paper: a faint line every 26 px, kept clear of the rounded corners.</summary>
        void Lines(Painter2D p, float w, float h)
        {
            const float step = 26f;
            float top = 0f;
            for (int i = 4; i < 8; i++) top = Mathf.Max(top, _resolved[i]);
            float inset = _borderWidth + 2f;
            p.strokeColor = _lines;
            p.lineWidth = 1f;
            p.lineCap = LineCap.Butt;
            p.BeginPath();
            for (float y = step; y < h - Mathf.Max(top, 8f); y += step)
            {
                if (y < top) continue;
                p.MoveTo(new Vector2(inset, y));
                p.LineTo(new Vector2(w - inset, y));
            }
            p.Stroke();
        }

        void Trace(Painter2D p, float x, float y, float w, float h, float inset)
        {
            float tlx = R(0, inset), trx = R(1, inset), brx = R(2, inset), blx = R(3, inset);
            float tly = R(4, inset), tr_y = R(5, inset), bry = R(6, inset), bly = R(7, inset);
            p.MoveTo(new Vector2(x + tlx, y));
            p.LineTo(new Vector2(x + w - trx, y));
            Corner(p, new Vector2(x + w - trx, y), new Vector2(x + w, y), new Vector2(x + w, y + tr_y));
            p.LineTo(new Vector2(x + w, y + h - bry));
            Corner(p, new Vector2(x + w, y + h - bry), new Vector2(x + w, y + h), new Vector2(x + w - brx, y + h));
            p.LineTo(new Vector2(x + blx, y + h));
            Corner(p, new Vector2(x + blx, y + h), new Vector2(x, y + h), new Vector2(x, y + h - bly));
            p.LineTo(new Vector2(x, y + tly));
            Corner(p, new Vector2(x, y + tly), new Vector2(x, y), new Vector2(x + tlx, y));
            p.ClosePath();
        }

        float R(int i, float inset) => Mathf.Max(0f, _resolved[i] - inset);

        static void Corner(Painter2D p, Vector2 from, Vector2 corner, Vector2 to)
        {
            if ((from - to).sqrMagnitude < 1e-6f) return;
            p.BezierCurveTo(from + InkPath.Kappa * (corner - from), to + InkPath.Kappa * (corner - to), to);
        }

        void Dashes(Painter2D p, float x, float y, float w, float h, float inset, float on, float off)
        {
            var pts = Outline(x, y, w, h, inset);
            bool drawing = true;
            float remain = on;
            Vector2 dashStart = pts[0];
            for (int i = 1; i <= pts.Count; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i % pts.Count];
                float len = Vector2.Distance(a, b);
                float pos = 0f;
                while (len - pos > 1e-4f)
                {
                    float step = Mathf.Min(remain, len - pos);
                    pos += step;
                    remain -= step;
                    if (remain > 1e-4f) continue;
                    Vector2 at = Vector2.Lerp(a, b, pos / len);
                    if (drawing)
                    {
                        p.BeginPath();
                        p.MoveTo(dashStart);
                        p.LineTo(at);
                        p.Stroke();
                    }
                    else dashStart = at;
                    drawing = !drawing;
                    remain = drawing ? on : off;
                }
            }
        }

        List<Vector2> Outline(float x, float y, float w, float h, float inset)
        {
            float tlx = R(0, inset), trx = R(1, inset), brx = R(2, inset), blx = R(3, inset);
            float tly = R(4, inset), tr_y = R(5, inset), bry = R(6, inset), bly = R(7, inset);
            var pts = new List<Vector2>(40);
            pts.Add(new Vector2(x + tlx, y));
            Sample(pts, new Vector2(x + w - trx, y), new Vector2(x + w, y), new Vector2(x + w, y + tr_y));
            Sample(pts, new Vector2(x + w, y + h - bry), new Vector2(x + w, y + h), new Vector2(x + w - brx, y + h));
            Sample(pts, new Vector2(x + blx, y + h), new Vector2(x, y + h), new Vector2(x, y + h - bly));
            Sample(pts, new Vector2(x, y + tly), new Vector2(x, y), new Vector2(x + tlx, y));
            return pts;
        }

        static void Sample(List<Vector2> pts, Vector2 from, Vector2 corner, Vector2 to)
        {
            pts.Add(from);
            if ((from - to).sqrMagnitude < 1e-6f) return;
            Vector2 c1 = from + InkPath.Kappa * (corner - from), c2 = to + InkPath.Kappa * (corner - to);
            for (int i = 1; i <= 6; i++)
            {
                float t = i / 6f, u = 1f - t;
                pts.Add(u * u * u * from + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * to);
            }
        }
    }

    /// <summary>
    /// An icon from <see cref="InkIcons"/>, drawn with Painter2D at the element's size. The
    /// <c>--icon-ink</c> USS property recolours its ink strokes.
    /// </summary>
    [UxmlElement]
    public partial class InkIcon : VisualElement
    {
        static readonly CustomStyleProperty<Color> InkProp = new CustomStyleProperty<Color>("--icon-ink");

        string _icon;
        IconShape[] _shapes;
        Color _ink = InkIcons.Ink;

        [UxmlAttribute]
        public string Icon
        {
            get => _icon;
            set
            {
                if (_icon == value) return;
                _icon = value;
                _shapes = InkIcons.Get(value);
                MarkDirtyRepaint();
            }
        }

        public InkIcon()
        {
            AddToClassList("ink-icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                _ink = e.customStyle.TryGetValue(InkProp, out var c) ? c : InkIcons.Ink;
                MarkDirtyRepaint();
            });
        }

        public InkIcon(string icon) : this() => Icon = icon;

        void Draw(MeshGenerationContext ctx)
        {
            if (_shapes == null) return;
            float w = layout.width, h = layout.height;
            if (w < 1f || h < 1f || float.IsNaN(w) || float.IsNaN(h)) return;
            float scale = Mathf.Min(w, h) / 24f;
            var offset = new Vector2((w - 24f * scale) / 2f, (h - 24f * scale) / 2f);
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            foreach (var s in _shapes)
            {
                var fill = s.FillInk ? _ink : s.Fill;
                if (fill.a > 0f)
                {
                    p.fillColor = fill;
                    p.BeginPath();
                    InkPath.Emit(p, s.Segs, offset, scale);
                    p.Fill();
                }
                if (s.Width <= 0f) continue;
                var stroke = s.StrokeInk ? _ink : s.Stroke;
                if (stroke.a <= 0f) continue;
                p.strokeColor = stroke;
                p.lineWidth = s.Width * scale;
                p.BeginPath();
                InkPath.Emit(p, s.Segs, offset, scale);
                p.Stroke();
            }
        }
    }

    /// <summary>A strip of striped washi tape with zigzag ends. <c>--tape-color</c> sets its colour.</summary>
    [UxmlElement]
    public partial class Tape : VisualElement
    {
        static readonly CustomStyleProperty<Color> ColorProp = new CustomStyleProperty<Color>("--tape-color");
        static readonly Color Stripe = new Color(1f, 1f, 1f, 0.42f);

        Color _color = InkIcons.Sky;

        public Tape()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                _color = e.customStyle.TryGetValue(ColorProp, out var c) ? c : InkIcons.Sky;
                MarkDirtyRepaint();
            });
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 1f || h < 1f || float.IsNaN(w) || float.IsNaN(h)) return;
            var p = ctx.painter2D;
            p.fillColor = _color;
            p.BeginPath();
            p.MoveTo(new Vector2(0f, 0f));
            p.LineTo(new Vector2(w, 0f));
            p.LineTo(new Vector2(w * 0.95f, h * 0.25f));
            p.LineTo(new Vector2(w, h * 0.5f));
            p.LineTo(new Vector2(w * 0.95f, h * 0.75f));
            p.LineTo(new Vector2(w, h));
            p.LineTo(new Vector2(0f, h));
            p.LineTo(new Vector2(w * 0.05f, h * 0.75f));
            p.LineTo(new Vector2(0f, h * 0.5f));
            p.LineTo(new Vector2(w * 0.05f, h * 0.25f));
            p.ClosePath();
            p.Fill();

            p.fillColor = Stripe;
            for (float x = w * 0.05f + 3f; x + 5f < w * 0.95f; x += 10f)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(x, 0f));
                p.LineTo(new Vector2(x + 5f, 0f));
                p.LineTo(new Vector2(x + 5f, h));
                p.LineTo(new Vector2(x, h));
                p.ClosePath();
                p.Fill();
            }
        }
    }

    /// <summary>
    /// A dashed rule along the element's long side. <c>--dash-color</c>, <c>--dash-on</c> and
    /// <c>--dash-off</c> set colour and rhythm; the short side is the line thickness.
    /// </summary>
    [UxmlElement]
    public partial class DashLine : VisualElement
    {
        static readonly CustomStyleProperty<Color> ColorProp = new CustomStyleProperty<Color>("--dash-color");
        static readonly CustomStyleProperty<float> OnProp = new CustomStyleProperty<float>("--dash-on");
        static readonly CustomStyleProperty<float> OffProp = new CustomStyleProperty<float>("--dash-off");

        Color _color = InkIcons.Hex(0xBCD5F0);
        float _on = 6f, _off = 4f;

        public DashLine()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                var s = e.customStyle;
                _color = s.TryGetValue(ColorProp, out var c) ? c : InkIcons.Hex(0xBCD5F0);
                _on = s.TryGetValue(OnProp, out var on) && on > 0f ? on : 6f;
                _off = s.TryGetValue(OffProp, out var off) && off >= 0f ? off : 4f;
                MarkDirtyRepaint();
            });
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 0.5f || h < 0.5f || float.IsNaN(w) || float.IsNaN(h)) return;
            bool vertical = h > w;
            float length = vertical ? h : w;
            var p = ctx.painter2D;
            p.strokeColor = _color;
            p.lineWidth = vertical ? w : h;
            p.lineCap = LineCap.Butt;
            for (float t = 0f; t < length; t += _on + _off)
            {
                float end = Mathf.Min(length, t + _on);
                p.BeginPath();
                if (vertical)
                {
                    p.MoveTo(new Vector2(w / 2f, t));
                    p.LineTo(new Vector2(w / 2f, end));
                }
                else
                {
                    p.MoveTo(new Vector2(t, h / 2f));
                    p.LineTo(new Vector2(end, h / 2f));
                }
                p.Stroke();
            }
        }
    }
}
