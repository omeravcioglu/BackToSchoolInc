using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// An assistant's face in the style of the canvas kids: shirt, round head, hair, and a face
    /// and accessory that match their personality. <see cref="Look"/> varies hair and colours.
    /// </summary>
    public sealed class Portrait : InkPicture
    {
        static readonly Color[] Hair =
        {
            InkIcons.Ink, InkIcons.Marker, InkIcons.Coral, InkIcons.Hex(0x8A5A3C), InkIcons.Hex(0x3F5F93),
        };
        static readonly Color[] Shirts =
        {
            InkIcons.Sky, InkIcons.Mid, InkIcons.Hex(0x2F6FD0), InkIcons.Pale, InkIcons.Hex(0x9CC3EC),
        };

        AssistantStyle _style;
        int _look = -1;

        public Portrait() : base(null) { }

        public void Show(AssistantStyle style, int look)
        {
            if (style == _style && look == _look && Drawing != null) return;
            _style = style;
            _look = look;
            Drawing = Build(style, look);
        }

        static InkDrawing Build(AssistantStyle style, int look)
        {
            var ink = InkIcons.Ink;
            var hair = Hair[look % Hair.Length];
            var shirt = Shirts[look / 7 % Shirts.Length];
            int cut = look / 3 % 4;
            var cheeks = new Color(1f, 0.549f, 0.431f, 0.55f);
            var d = new InkDrawing(32f, 36f);

            d.Shape("M5 37c0-7.5 4.8-11.5 11-11.5S27 29.5 27 37z", shirt, ink, 1.6f);
            if (cut == 2) d.Shape("M5.8 15.5C5.8 9 10.2 5.4 16 5.4s10.2 3.6 10.2 10.1v8.6c-1.4.8-2.8.8-4 0v-6.6h-12.4v6.6c-1.2.8-2.6.8-4 0z", hair, ink, 1.3f);
            d.Circle(16f, 15f, 9.6f, Color.white, ink, 1.6f);

            if (style == AssistantStyle.Handy)
            {
                d.Shape("M6.6 12.6c0-5.2 4.2-8.4 9.4-8.4s9.4 3.2 9.4 8.4z", InkIcons.Marker, ink, 1.4f);
                d.Line("M4.8 12.8h22.4", ink, 1.6f);
            }
            else
            {
                switch (cut)
                {
                    case 1:
                        d.Circle(16f, 4.4f, 3.2f, hair, ink, 1.3f);
                        d.Shape("M6.6 13.6C7.1 8 11.1 5.4 16 5.4s8.9 2.6 9.4 8.2c-2.5-2.1-5.8-3.1-9.4-3.1s-6.9 1-9.4 3.1z", hair, ink, 1.3f);
                        break;
                    case 3:
                        d.Shape("M6.2 14.4c-.7-2.2.3-4 1.9-4.6.2-2.2 2-3.6 4-3.2 1-1.6 3-2 4.6-1 1.8-.8 3.8 0 4.6 1.8 2 .2 3.4 2 3 4.2 1.4.8 1.8 2.2 1.2 3.4-2.6-1.8-6.2-2.8-9.4-2.8s-7.1.8-9.9 2.2z",
                            hair, ink, 1.3f);
                        break;
                    default:
                        d.Shape("M6.6 13.6C7.1 8 11.1 5.4 16 5.4s8.9 2.6 9.4 8.2c-2.5-2.1-5.8-3.1-9.4-3.1s-6.9 1-9.4 3.1z", hair, ink, 1.3f);
                        break;
                }
            }

            // Eyes
            if (style == AssistantStyle.Cheerful || style == AssistantStyle.Charming)
                d.Line("M10.9 16.9q1.5-1.6 3 0M18.1 16.9q1.5-1.6 3 0", ink, 1.2f);
            else
            {
                d.Circle(12.4f, 16.5f, 1.3f, ink);
                d.Circle(19.6f, 16.5f, 1.3f, ink);
            }
            if (style == AssistantStyle.Strict) d.Line("M10.7 13.6l3.1.6M21.3 13.6l-3.1.6", ink, 1.2f);
            if (style == AssistantStyle.Bookish)
            {
                d.Circle(12.4f, 16.5f, 2.7f, Color.clear, ink, 1.1f);
                d.Circle(19.6f, 16.5f, 2.7f, Color.clear, ink, 1.1f);
                d.Line("M15.1 16.2h1.8", ink, 1.1f);
            }
            d.Ellipse(9.9f, 19.6f, 1.8f, 1.05f, cheeks);
            d.Ellipse(22.1f, 19.6f, 1.8f, 1.05f, cheeks);

            // Mouth
            switch (style)
            {
                case AssistantStyle.Cheerful: d.Shape("M14 19.6h4c0 1.7-.9 2.7-2 2.7s-2-1-2-2.7z", ink); break;
                case AssistantStyle.Strict: d.Line("M14.2 21h3.6", ink, 1.2f); break;
                case AssistantStyle.Charming: d.Line("M13.6 20.2c1.2 1.6 3.6 1.6 4.8 0", ink, 1.2f); break;
                case AssistantStyle.Handy: d.Line("M14 20.8c1.4.6 2.8.4 4-.6", ink, 1.2f); break;
                default: d.Line("M14.4 20.6c1 .9 2.2.9 3.2 0", ink, 1.2f); break;
            }

            // Accessories
            switch (style)
            {
                case AssistantStyle.Charming:
                    d.Shape("M13 26.6l3 1.5-3 1.5zM19 26.6l-3 1.5 3 1.5z", InkIcons.Coral, ink, 0.9f);
                    break;
                case AssistantStyle.Strict:
                    d.Shape("M15.2 26h1.6l.6 4.2-1.4 1.6-1.4-1.6z", ink);
                    break;
                case AssistantStyle.Thrifty:
                    d.Circle(21.6f, 31.2f, 1.9f, InkIcons.Marker, ink, 0.9f);
                    break;
                case AssistantStyle.Cheerful:
                    d.Circle(8.6f, 8.6f, 1.7f, InkIcons.Coral, ink, 0.9f);
                    break;
                case AssistantStyle.Handy:
                    d.Line("M20.5 28l3.5 5", ink, 1.6f);
                    d.Line("M20.5 28l3.5 5", InkIcons.Marker, 0.8f);
                    break;
            }
            return d;
        }
    }

    /// <summary>The school crest from the New school screen: a shield in the school colour, an emblem and initials.</summary>
    public sealed class CrestView : VisualElement
    {
        static readonly List<InkPath.Seg> Shield = InkPath.Parse("M20 26Q120 -2 220 26V150C220 212 170 250 120 270C70 250 20 212 20 150Z");
        static readonly List<InkPath.Seg> Inner = InkPath.Parse("M38 40Q120 16 202 40V150C202 202 162 234 120 252C78 234 38 202 38 150Z");
        static readonly List<InkPath.Seg> Ribbon = InkPath.Parse("M4 196H236L224 214L236 232H4L16 214Z");

        readonly InkDrawing _inner;
        readonly Label _initials;
        Color _color = InkIcons.Hex(0x2F6FD0);
        IconShape[] _emblem = InkIcons.Get("crest");

        public CrestView()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            _inner = new InkDrawing(240f, 280f).Dashed("M38 40Q120 16 202 40V150C202 202 162 234 120 252C78 234 38 202 38 150Z",
                new Color(1f, 1f, 1f, 0.85f), 3f, 9f, 7f, round: true);
            _initials = new Label();
            _initials.AddToClassList("crest-initials");
            _initials.AddToClassList("font-hand");
            Add(_initials);
        }

        public void Show(Color color, string emblem, string schoolName)
        {
            _color = color;
            _emblem = InkIcons.Get(emblem) ?? InkIcons.Get("crest");
            _initials.text = Initials(schoolName);
            MarkDirtyRepaint();
        }

        public static string Initials(string name)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var word in (name ?? "").Split(' '))
            {
                if (word.Length == 0 || !char.IsLetter(word[0])) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(word[0]));
                if (sb.Length >= 5) break;
            }
            return sb.ToString();
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 1f || h < 1f || float.IsNaN(w)) return;
            float s = Mathf.Min(w / 240f, h / 280f);
            var o = new Vector2((w - 240f * s) / 2f, (h - 280f * s) / 2f);
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;

            Path(p, Shield, o, s, _color, InkIcons.Ink, 5f);
            _inner.Draw(p, o, s);
            p.fillColor = InkIcons.Paper;
            p.strokeColor = InkIcons.Ink;
            p.lineWidth = 4f * s;
            p.BeginPath();
            InkPath.Emit(p, InkPath.Circle(120f, 126f, 62f), o, s);
            p.Fill();
            p.Stroke();
            Path(p, Ribbon, o, s, InkIcons.Marker, InkIcons.Ink, 3.5f);

            // The emblem, drawn like a big icon (thinner strokes at this size).
            if (_emblem == null) return;
            float es = 106f / 24f * s;
            var eo = o + new Vector2(67f, 73f) * s;
            p.lineCap = LineCap.Round;
            foreach (var shape in _emblem)
            {
                var fill = shape.FillInk ? InkIcons.Ink : shape.Fill;
                if (fill.a > 0f)
                {
                    p.fillColor = fill;
                    p.BeginPath();
                    InkPath.Emit(p, shape.Segs, eo, es);
                    p.Fill();
                }
                if (shape.Width <= 0f) continue;
                p.strokeColor = shape.StrokeInk ? InkIcons.Ink : shape.Stroke;
                p.lineWidth = Mathf.Min(shape.Width, 1.3f) * es;
                p.BeginPath();
                InkPath.Emit(p, shape.Segs, eo, es);
                p.Stroke();
            }
        }

        static void Path(Painter2D p, List<InkPath.Seg> segs, Vector2 o, float s, Color fill, Color stroke, float width)
        {
            p.fillColor = fill;
            p.strokeColor = stroke;
            p.lineWidth = width * s;
            p.BeginPath();
            InkPath.Emit(p, segs, o, s);
            p.Fill();
            p.Stroke();
        }
    }

    /// <summary>
    /// The top bar's day clock: a dial that sweeps once per school day, sky by day and ink by
    /// night, so the always-running clock is always visible.
    /// </summary>
    [UxmlElement]
    public sealed partial class DayDial : VisualElement
    {
        float _progress;

        public DayDial()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public float Progress
        {
            get => _progress;
            set
            {
                value = Mathf.Repeat(value, 1f);
                if (Mathf.Abs(value - _progress) < 0.004f) return;
                _progress = value;
                MarkDirtyRepaint();
            }
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 4f || float.IsNaN(w)) return;
            var p = ctx.painter2D;
            var c = new Vector2(w / 2f, h / 2f);
            float r = Mathf.Min(w, h) / 2f - 1.5f;

            // Day (top half) and night (bottom half) of the face.
            p.fillColor = InkIcons.Pale;
            p.BeginPath();
            p.Arc(c, r, 180f, 360f);
            p.ClosePath();
            p.Fill();
            p.fillColor = InkIcons.Hex(0x3F5F93);
            p.BeginPath();
            p.Arc(c, r, 0f, 180f);
            p.ClosePath();
            p.Fill();

            // How much of today has gone.
            float start = -90f, end = start + _progress * 360f;
            if (_progress > 0.01f)
            {
                p.fillColor = new Color(1f, 0.878f, 0.4f, 0.75f);
                p.BeginPath();
                p.MoveTo(c);
                p.Arc(c, r * 0.92f, start, end);
                p.ClosePath();
                p.Fill();
            }

            p.strokeColor = InkIcons.Ink;
            p.lineWidth = 2f;
            p.BeginPath();
            p.Arc(c, r, 0f, 360f);
            p.Stroke();

            float a = end * Mathf.Deg2Rad;
            p.lineCap = LineCap.Round;
            p.lineWidth = 2.2f;
            p.BeginPath();
            p.MoveTo(c);
            p.LineTo(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.78f);
            p.Stroke();
            p.fillColor = InkIcons.Ink;
            p.BeginPath();
            p.Arc(c, 2.2f, 0f, 360f);
            p.Fill();
        }
    }

    /// <summary>
    /// A hand-inked line chart on graph paper. Values are plotted left to right; the page around
    /// it labels the axes. <see cref="Invert"/> puts small numbers on top (for ranks).
    /// </summary>
    public class LineChart : VisualElement
    {
        static readonly Color Grid = InkIcons.Hex(0xDCE9F7);
        static readonly Color Zero = InkIcons.Hex(0x9CC3EC);

        float[] _values = System.Array.Empty<float>();
        float _min, _max;

        public bool Invert;
        public bool Area = true;
        public bool ShowGrid = true;
        public float LineWidth = 2.6f;
        public Color LineColor = InkIcons.Ink;
        public Color AreaColor = new Color(0.659f, 0.82f, 0.969f, 0.45f);

        public LineChart()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public float Min => _min;
        public float Max => _max;

        /// <summary>Sets the series and its vertical range (pass NaN to fit the data).</summary>
        public void SetValues(float[] values, float min = float.NaN, float max = float.NaN)
        {
            _values = values ?? System.Array.Empty<float>();
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var v in _values)
            {
                lo = Mathf.Min(lo, v);
                hi = Mathf.Max(hi, v);
            }
            if (_values.Length == 0) { lo = 0f; hi = 1f; }
            _min = float.IsNaN(min) ? lo : min;
            _max = float.IsNaN(max) ? hi : max;
            if (_max - _min < 1e-3f)
            {
                _max += 1f;
                _min -= 1f;
            }
            MarkDirtyRepaint();
        }

        Vector2 Point(int i, float w, float h, float pad)
        {
            float x = _values.Length <= 1 ? w - pad : pad + (w - 2f * pad) * i / (_values.Length - 1);
            float t = (_values[i] - _min) / (_max - _min);
            if (Invert) t = 1f - t;
            return new Vector2(x, h - pad - t * (h - 2f * pad));
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 8f || h < 8f || float.IsNaN(w)) return;
            var p = ctx.painter2D;
            float pad = Mathf.Max(LineWidth * 2f, 4f);

            if (ShowGrid)
            {
                p.strokeColor = Grid;
                p.lineWidth = 1f;
                p.BeginPath();
                for (float x = 0f; x <= w; x += 24f) { p.MoveTo(new Vector2(x, 0f)); p.LineTo(new Vector2(x, h)); }
                for (float y = h; y >= 0f; y -= 24f) { p.MoveTo(new Vector2(0f, y)); p.LineTo(new Vector2(w, y)); }
                p.Stroke();
                if (_min < 0f && _max > 0f && !Invert)
                {
                    float zy = h - pad - (0f - _min) / (_max - _min) * (h - 2f * pad);
                    p.strokeColor = Zero;
                    p.lineWidth = 2f;
                    p.BeginPath();
                    p.MoveTo(new Vector2(0f, zy));
                    p.LineTo(new Vector2(w, zy));
                    p.Stroke();
                }
            }
            if (_values.Length == 0) return;

            if (Area && _values.Length > 1)
            {
                p.fillColor = AreaColor;
                p.BeginPath();
                p.MoveTo(new Vector2(Point(0, w, h, pad).x, h));
                for (int i = 0; i < _values.Length; i++) p.LineTo(Point(i, w, h, pad));
                p.LineTo(new Vector2(Point(_values.Length - 1, w, h, pad).x, h));
                p.ClosePath();
                p.Fill();
            }

            p.strokeColor = LineColor;
            p.lineWidth = LineWidth;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(Point(0, w, h, pad));
            for (int i = 1; i < _values.Length; i++) p.LineTo(Point(i, w, h, pad));
            p.Stroke();

            // Today, as a marker dot.
            var last = Point(_values.Length - 1, w, h, pad);
            float r = Mathf.Max(3f, LineWidth * 1.6f);
            p.fillColor = InkIcons.Marker;
            p.strokeColor = InkIcons.Ink;
            p.lineWidth = Mathf.Max(1.5f, LineWidth * 0.7f);
            p.BeginPath();
            p.Arc(last, r, 0f, 360f);
            p.Fill();
            p.Stroke();
        }
    }

    /// <summary>The starburst badge from the level-up card, with the level number on top.</summary>
    public sealed class Starburst : VisualElement
    {
        static readonly InkDrawing Drawing = new InkDrawing(160f, 160f)
            .Shape("M80 4L95 24L118 14.2L121 39L145.8 42L136 65L156 80L136 95L145.8 118L121 121L118 145.8L95 136L80 156L65 136L42 145.8L39 121L14.2 118L24 95L4 80L24 65L14.2 42L39 39L42 14.2L65 24Z",
                InkIcons.Marker, InkIcons.Ink, 4f)
            .Circle(80f, 80f, 44f, InkIcons.Paper, InkIcons.Ink, 3.5f)
            .Line("M52 64c4-10 12-16 22-18", InkIcons.Pale, 5f);

        public readonly Label Number;

        public Starburst()
        {
            pickingMode = PickingMode.Ignore;
            Add(new InkPicture(Drawing) { style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 } });
            Number = new Label();
            Number.AddToClassList("burst-number");
            Number.AddToClassList("font-hand");
            Add(Number);
        }
    }
}
