using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>One filled and/or stroked shape of an icon, in a 24 x 24 box.</summary>
    public sealed class IconShape
    {
        public List<InkPath.Seg> Segs;
        public Color Fill = Color.clear;      // alpha 0 = no fill
        public bool FillInk;                  // fill with the icon's ink colour instead
        public Color Stroke = Color.clear;    // used when StrokeInk is false
        public bool StrokeInk = true;
        public float Width = -1f;             // stroke width in icon units; 0 = no stroke, -1 = icon default
    }

    /// <summary>
    /// The HUD's icon set: the ballpoint-pen icons from the design canvas, as SVG path data. Shapes
    /// stroked "in ink" take the colour of the <see cref="InkIcon"/> drawing them, so one icon can
    /// be dark blue on paper or white on a blue button.
    /// </summary>
    public static class InkIcons
    {
        public static readonly Color Ink = Hex(0x1E3A6D);
        public static readonly Color Paper = Hex(0xFBFDFF);
        public static readonly Color Sky = Hex(0xA8D1F7);
        public static readonly Color Pale = Hex(0xDCEBFB);
        public static readonly Color Mid = Hex(0x5A9BEA);
        public static readonly Color Marker = Hex(0xFFE066);
        public static readonly Color Blush = Hex(0xFFE3DA);
        public static readonly Color Coral = Hex(0xFF8C6E);

        static readonly Dictionary<string, IconShape[]> Library = Build();

        public static IconShape[] Get(string name) => name != null && Library.TryGetValue(name, out var s) ? s : null;

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        static Dictionary<string, IconShape[]> Build()
        {
            var d = new Dictionary<string, IconShape[]>
            {
                // Top bar
                ["crest"] = Icon(1.7f,
                    P("M7.4 11.4h9.2c0 4.3-2.1 7.6-4.6 8.8-2.5-1.2-4.6-4.5-4.6-8.8z", Marker),
                    P("M6.3 11.4c0-2.4 2.5-4 5.7-4s5.7 1.6 5.7 4z", Mid),
                    Colored(P("M8.6 9.4h.1M11 8.7h.1M13.4 8.9h.1M15.6 9.6h.1"), Paper, 1.4f),
                    P("M12 7.4V5.2c.8-1 2-1.5 3.4-1.3")),
                ["coin"] = Icon(1.8f,
                    E(12f, 12.4f, 8.7f, 8.4f, Marker),
                    P("M14.4 9.5c-.5-.7-1.4-1.1-2.4-1.1-1.4 0-2.3.7-2.3 1.7 0 2.3 4.8 1.2 4.8 3.8 0 1-1 1.8-2.5 1.8-1.1 0-2-.4-2.6-1.2M12 6.9v1.5M12 15.8v1.6")),
                ["students"] = Icon(1.8f,
                    C(16f, 8.8f, 2.6f, Pale),
                    P("M13.6 13.5c.7-.3 1.5-.4 2.3-.4 2.7 0 4.4 1.8 4.8 5.2h-5", Pale),
                    C(9f, 8.2f, 3.1f, Sky),
                    P("M3.4 19.2c.4-3.6 2.5-5.6 5.6-5.6s5.2 2 5.6 5.6z", Sky)),
                ["star"] = Icon(1.8f,
                    P("M12 3.2l2.7 5.4 5.9.9-4.3 4.1 1 5.9L12 16.7l-5.3 2.8 1-5.9-4.3-4.1 5.9-.9z", Marker)),
                ["smile"] = Icon(1.8f,
                    C(12f, 12f, 8.8f, Sky),
                    P("M8.4 13.8c.9 1.5 2.2 2.3 3.6 2.3s2.7-.8 3.6-2.3"),
                    P("M9.2 9.6v.6M14.8 9.6v.6")),
                ["heart"] = Icon(1.8f,
                    P("M12 19.6s-7.4-4.3-7.4-9.7c0-2.4 1.8-4.2 4-4.2 1.5 0 2.7.8 3.4 2 .7-1.2 1.9-2 3.4-2 2.2 0 4 1.8 4 4.2 0 5.4-7.4 9.7-7.4 9.7z", Sky)),
                ["heart-red"] = Icon(2f,
                    P("M12 20s-7.4-4.3-7.4-9.7c0-2.4 1.8-4.2 4-4.2 1.5 0 2.7.8 3.4 2 .7-1.2 1.9-2 3.4-2 2.2 0 4 1.8 4 4.2C19.4 15.7 12 20 12 20z", Coral)),
                ["pause"] = Icon(3f, P("M9 6.5v11M15 6.5v11")),
                ["play"] = Icon(2f, InkFill(P("M8.5 5.8l10 6.2-10 6.2z"))),

                // Divider tabs
                ["envelope"] = Icon(2f,
                    R(3.5f, 6f, 17f, 12.5f, 2f, Paper),
                    P("M4 7l8 6.2L20 7")),
                ["people"] = Icon(2f,
                    C(16f, 8.8f, 2.6f, Paper),
                    P("M13.6 13.5c.7-.3 1.5-.4 2.3-.4 2.7 0 4.4 1.8 4.8 5.2h-5"),
                    C(9f, 8.2f, 3.1f, Paper),
                    P("M3.4 19.2c.4-3.6 2.5-5.6 5.6-5.6s5.2 2 5.6 5.6z", Paper)),
                ["staff"] = Icon(2f,
                    C(12f, 7.5f, 3.4f, Paper),
                    P("M5 20c.6-4 3.3-6.5 7-6.5s6.4 2.5 7 6.5z", Paper),
                    InkFill(P("M12 13.8l-1.2 3 1.2 2.1 1.2-2.1z"))),
                ["pencil"] = Icon(2f,
                    P("M4.5 19.5l1-4.4L15.3 5.3a2 2 0 0 1 2.8 0l.6.6a2 2 0 0 1 0 2.8L8.9 18.5z", Marker),
                    P("M13.7 6.9l3.4 3.4M4.5 19.5l4.4-1")),
                ["chart"] = Icon(2f,
                    P("M4 19.5V4.5M4 19.5h16"),
                    P("M7.5 15l3.5-4 3 2.5 5-6"),
                    P("M16 7.5h3v3")),
                ["trophy"] = Icon(2f,
                    P("M7.5 4.5h9v4.5a4.5 4.5 0 0 1-9 0z", Paper),
                    P("M7.5 6.5H5c0 2.4 1.2 3.8 3 4M16.5 6.5H19c0 2.4-1.2 3.8-3 4M12 13.5v3.5M8.5 20h7M9.5 17h5")),
                ["clipboard"] = Icon(2f,
                    R(5.5f, 5f, 13f, 15.5f, 2f, Paper),
                    P("M9 5V3.8h6V5"),
                    P("M8.5 10h7M8.5 13.2h7M8.5 16.4h4")),
                ["sliders"] = Icon(2f,
                    P("M5 7h14M5 12h14M5 17h14"),
                    C(9f, 7f, 2f, Paper),
                    C(15f, 12f, 2f, Paper),
                    C(8f, 17f, 2f, Paper)),
                ["dots"] = Icon(0f,
                    InkFill(C(5.5f, 12f, 2f, Color.clear)),
                    InkFill(C(12f, 12f, 2f, Color.clear)),
                    InkFill(C(18.5f, 12f, 2f, Color.clear))),
                ["lock"] = Icon(2.6f,
                    R(5.5f, 11f, 13f, 9f, 2f, Color.clear),
                    P("M8.5 11V8.3a3.5 3.5 0 0 1 7 0V11")),

                // Right column
                ["arrow"] = Icon(2.4f, P("M5 12.5c4-.5 9-.5 13-.5M13.5 7l5 5-5 5")),
                ["help"] = Icon(2.2f,
                    C(12f, 12f, 9f, Color.clear),
                    P("M9.6 9.5a2.5 2.5 0 1 1 3.4 2.3c-.7.3-1 .9-1 1.6v.4M12 16.8v.2")),
                ["book"] = Icon(2f,
                    P("M4 5.5c2.5-1 5.5-1 8 .8 2.5-1.8 5.5-1.8 8-.8V19c-2.5-1-5.5-1-8 .8-2.5-1.8-5.5-1.8-8-.8z", Pale),
                    P("M12 6.3v13.5")),
                ["apple"] = Icon(2f,
                    P("M12 8c-1.5-1.2-5.5-1.5-6.5 2.5-.8 3.5 1.5 8.5 4 8.5 1 0 1.5-.5 2.5-.5s1.5.5 2.5.5c2.5 0 4.8-5 4-8.5C17.5 6.5 13.5 6.8 12 8z", Pale),
                    P("M12 8c0-2 .8-3.5 2.5-4.5")),
                ["house"] = Icon(2f,
                    P("M6 9.5v10h12v-10", Blush),
                    P("M4 11l8-6.5 8 6.5"),
                    P("M10 19.5v-5h4v5")),
                ["music"] = Icon(2f,
                    P("M9 18V6l10-2v12"),
                    C(6.5f, 18f, 2.5f, Blush),
                    C(16.5f, 16f, 2.5f, Blush)),
                ["mug"] = Icon(2f,
                    P("M5 9h11v5a5 5 0 0 1-5 5h-1a5 5 0 0 1-5-5z", Pale),
                    P("M16 10.5h1.5a2.5 2.5 0 0 1 0 5H15.5M8.5 3.5c-.8 1 .8 2 0 3M12 3.5c-.8 1 .8 2 0 3")),
                ["shield"] = Icon(2f,
                    P("M12 3.5l7 2.5v5.5c0 4.5-3 7.5-7 9-4-1.5-7-4.5-7-9V6z", Pale)),
                ["up"] = Icon(3f, P("M12 19V5M6 11l6-6 6 6")),
                ["down"] = Icon(3f, P("M12 5v14M6 13l6 6 6-6")),
                ["steady"] = Icon(3f, P("M5 12h14M14 7l5 5-5 5")),

                // Toasts, buttons and menus
                ["close"] = Icon(3f, P("M6.5 6.5l11 11M17.5 6.5l-11 11")),
                ["check"] = Icon(2.6f, P("M5 12.5l4.5 4.5L19 7.5")),
                ["alert"] = Icon(2.8f, P("M12 6.5v7M12 17v.5")),
                ["gear"] = Icon(2f,
                    C(12f, 12f, 3.2f, Paper),
                    P("M12 2.5v3M12 18.5v3M4.6 4.6l2.1 2.1M17.3 17.3l2.1 2.1M2.5 12h3M18.5 12h3M4.6 19.4l2.1-2.1M17.3 6.7l2.1-2.1")),
                ["eye"] = Icon(2.2f,
                    P("M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6z"),
                    InkFill(C(12f, 12f, 2.6f, Color.clear))),

                // Crest emblems (the acorn is "crest")
                ["owl"] = Icon(1.7f,
                    P("M6 19.5c0-8 2.7-13 6-13s6 5 6 13c-1.8.8-3.8 1.2-6 1.2s-4.2-.4-6-1.2z", Sky),
                    P("M7.6 8.6L7 5.2l2.8 2.4M16.4 8.6l.6-3.4-2.8 2.4"),
                    C(9.8f, 11f, 2.2f, Paper),
                    C(14.2f, 11f, 2.2f, Paper),
                    InkFill(C(9.8f, 11f, 0.8f, Color.clear)),
                    InkFill(C(14.2f, 11f, 0.8f, Color.clear)),
                    P("M11.2 13.2h1.6L12 14.6z", Marker)),
                ["bell"] = Icon(1.7f,
                    P("M6 16.5V11a6 6 0 0 1 12 0v5.5l1.5 1.5h-15z", Marker),
                    P("M10 20.5a2 2 0 0 0 4 0M12 5V3.5")),
                ["sun"] = Icon(1.7f,
                    P("M12 3v2.5M12 18.5V21M3 12h2.5M18.5 12H21M5.6 5.6l1.8 1.8M16.6 16.6l1.8 1.8M5.6 18.4l1.8-1.8M16.6 7.4l1.8-1.8"),
                    C(12f, 12f, 4.6f, Marker)),

                // Title menu and settings
                ["sprout"] = Icon(2f,
                    P("M12 20.5V12"),
                    P("M12 12.5c0-4.4 3-7.5 7.5-7.5 0 4.4-3 7.5-7.5 7.5z", Sky),
                    P("M12 14.5c0-3.3-2.5-5.8-6.5-5.8 0 3.3 2.5 5.8 6.5 5.8z", Sky),
                    P("M7.5 20.5h9")),
                ["folder"] = Icon(2f,
                    P("M3.5 7.5v10A1.5 1.5 0 0 0 5 19h14a1.5 1.5 0 0 0 1.5-1.5V9.5A1.5 1.5 0 0 0 19 8h-7l-2-2.5H5A1.5 1.5 0 0 0 3.5 7z", Marker),
                    P("M3.5 10.5h17")),
                ["cog"] = Icon(2f,
                    Wide(P("M12 3v2.6M12 18.4V21M3 12h2.6M18.4 12H21M5.6 5.6l1.9 1.9M16.5 16.5l1.9 1.9M5.6 18.4l1.9-1.9M16.5 7.5l1.9-1.9"), 2.6f),
                    C(12f, 12f, 6.2f, Sky),
                    C(12f, 12f, 2.4f, Paper)),
                ["door"] = Icon(2f,
                    P("M5.5 20.5V4.5h8v16", Paper),
                    P("M3.5 20.5h12"),
                    InkFill(C(11f, 12.5f, 1f, Color.clear)),
                    P("M16.5 12h5M19.5 9.5l2.5 2.5-2.5 2.5")),
                ["dice"] = Icon(2f,
                    R(4f, 4f, 16f, 16f, 3.5f, Paper),
                    InkFill(C(8.5f, 8.5f, 1.5f, Color.clear)),
                    InkFill(C(12f, 12f, 1.5f, Color.clear)),
                    InkFill(C(15.5f, 15.5f, 1.5f, Color.clear))),
                ["bed"] = Icon(1.8f,
                    P("M3 18V7.5M3 14h18v4M21 14v-2.5A2.5 2.5 0 0 0 18.5 9H11v5", Pale),
                    C(7f, 11f, 2f, Marker)),
                ["board"] = Icon(1.8f,
                    InkFill(R(3.5f, 4.5f, 17f, 11f, 1.5f, Color.clear)),
                    Colored(P("M7 8.5c1.5-1.5 3 1.5 4.5 0M13.5 10.5h3.5"), Paper, 1.8f),
                    P("M7.5 15.5L6 20M16.5 15.5L18 20")),
                ["screen"] = Icon(1.9f,
                    R(3.5f, 4.5f, 17f, 12f, 2f, Paper),
                    P("M9 20h6M12 16.5V20"),
                    P("M7.5 9.5v-2h2M16.5 9.5v-2h-2M7.5 11.5v2h2M16.5 11.5v2h-2")),
                ["letters"] = Icon(2f,
                    P("M3 19L7.8 6.5 12.6 19M4.9 14.8h5.8"),
                    P("M13.6 19l3.4-8.6 3.4 8.6M14.9 16.2h4.2")),

                // News, stats, rankings, the assistant
                ["news"] = Icon(1.9f,
                    P("M18.5 8.5h2v9.5a2 2 0 0 1-2 2", Pale),
                    R(3.5f, 4.5f, 15f, 15.5f, 2f, Paper),
                    P("M6.5 8.5h9M6.5 11.5h3.5M6.5 14.5h3.5M6.5 17.5h9"),
                    R(11.8f, 11f, 3.7f, 4f, 0.8f, Sky)),
                ["podium"] = Icon(1.9f,
                    P("M3.5 14h5.5v6.5H3.5z", Pale),
                    P("M15 15.5h5.5v5H15z", Sky),
                    P("M9 10h6v10.5H9z", Marker),
                    P("M2.5 20.5h19"),
                    P("M12 3.4l.9 1.9 2 .3-1.5 1.4.4 2-1.8-1-1.8 1 .4-2-1.5-1.4 2-.3z", Marker)),
                ["speech"] = Icon(1.9f,
                    P("M4.5 5h15A2 2 0 0 1 21.5 7v8a2 2 0 0 1-2 2H11l-4.5 3.5V17h-2a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2z", Paper),
                    P("M7.5 9.5h9M7.5 12.5h6")),
                ["cloud"] = Icon(1.9f,
                    P("M7 18.5h10.5a4 4 0 0 0 .5-8 5.5 5.5 0 0 0-10.6-1.2A4.6 4.6 0 0 0 7 18.5z", Pale)),
                ["sparkle"] = Icon(1.6f,
                    P("M12 2.8l1.9 6.3 6.3 1.9-6.3 1.9-1.9 6.3-1.9-6.3-6.3-1.9 6.3-1.9z", Marker)),
                ["minus"] = Icon(3f, P("M6 12h12")),
                ["plus"] = Icon(3f, P("M12 6v12M6 12h12")),
                ["back"] = Icon(2.4f, P("M19 12H5.5M11 6l-6 6 6 6")),
                ["next"] = Icon(2.4f, P("M5 12h13.5M13 6l6 6-6 6")),
            };
            return d;
        }

        /// <summary>
        /// Adds or replaces an icon at runtime, so a page can bring its own glyphs without editing
        /// this file. Each spec is SVG path data stroked in ink at <paramref name="width"/>, with
        /// optional prefixes: "fill:#RRGGBB " fills it too, "ink " fills it with ink, "bare " drops
        /// the stroke. Instead of path data a spec can be "circle:cx,cy,r" or "rect:x,y,w,h,r".
        /// Example: Define("cup", 2f, "fill:#FFE066 M7 4h10v5a5 5 0 0 1-10 0z", "M12 14v5M8 20h8").
        /// </summary>
        public static void Define(string name, float width, params string[] specs)
        {
            var shapes = new List<IconShape>(specs.Length);
            foreach (var raw in specs)
            {
                string spec = raw.Trim();
                var color = Color.clear;
                bool ink = false, bare = false;
                while (true)
                {
                    if (spec.StartsWith("fill:#"))
                    {
                        int space = spec.IndexOf(' ');
                        color = Hex(int.Parse(spec.Substring(6, space - 6), System.Globalization.NumberStyles.HexNumber));
                        spec = spec.Substring(space + 1).TrimStart();
                    }
                    else if (spec.StartsWith("ink ")) { ink = true; spec = spec.Substring(4).TrimStart(); }
                    else if (spec.StartsWith("bare ")) { bare = true; spec = spec.Substring(5).TrimStart(); }
                    else break;
                }
                IconShape shape;
                if (spec.StartsWith("circle:"))
                {
                    var v = Numbers(spec.Substring(7));
                    shape = C(v[0], v[1], v[2], color);
                }
                else if (spec.StartsWith("rect:"))
                {
                    var v = Numbers(spec.Substring(5));
                    shape = R(v[0], v[1], v[2], v[3], v.Length > 4 ? v[4] : 0f, color);
                }
                else shape = P(spec, color);
                shape.FillInk = ink;
                if (bare) shape.Width = 0f;
                shapes.Add(shape);
            }
            Library[name] = Icon(width, shapes.ToArray());
        }

        static float[] Numbers(string csv)
        {
            var parts = csv.Split(',');
            var v = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                v[i] = float.Parse(parts[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            return v;
        }

        // --- Builders ---------------------------------------------------------------------

        static IconShape[] Icon(float width, params IconShape[] shapes)
        {
            foreach (var s in shapes)
                if (s.Width < 0f) s.Width = width;
            return shapes;
        }

        static IconShape P(string d, Color fill = default) => new IconShape { Segs = InkPath.Parse(d), Fill = fill };

        static IconShape C(float cx, float cy, float r, Color fill) => new IconShape { Segs = InkPath.Circle(cx, cy, r), Fill = fill };

        static IconShape E(float cx, float cy, float rx, float ry, Color fill) => new IconShape { Segs = InkPath.Ellipse(cx, cy, rx, ry), Fill = fill };

        static IconShape R(float x, float y, float w, float h, float r, Color fill) => new IconShape { Segs = InkPath.RoundRect(x, y, w, h, r), Fill = fill };

        static IconShape InkFill(IconShape s)
        {
            s.FillInk = true;
            return s;
        }

        /// <summary>A stroke in ink with its own width.</summary>
        static IconShape Wide(IconShape s, float width)
        {
            s.Width = width;
            return s;
        }

        static IconShape Colored(IconShape s, Color stroke, float width)
        {
            s.StrokeInk = false;
            s.Stroke = stroke;
            s.Width = width;
            return s;
        }
    }
}
