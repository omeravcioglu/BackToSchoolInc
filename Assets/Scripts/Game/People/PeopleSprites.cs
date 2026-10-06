using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>
    /// Campus sprites for people, painted from the <see cref="PeopleArt"/> drawings with the ink
    /// rasterizer and cached: figures in four poses, thought bubbles with an icon, a soft shadow, a
    /// ball and the marker ring under whoever is selected.
    /// </summary>
    public static class PeopleSprites
    {
        public const int Stand = 0, WalkA = 1, WalkB = 2, Sit = 3;

        const float Scale = 2f;               // texture pixels per design unit
        const float PixelsPerUnit = 96f;      // a 46-unit figure is about 0.96 tiles tall

        const int PaintsPerFrame = 2;

        static readonly Dictionary<long, Sprite> Figures = new Dictionary<long, Sprite>();
        static readonly Dictionary<string, Sprite> Misc = new Dictionary<string, Sprite>();
        static int _paintFrame = -1, _paintedThisFrame;

        /// <summary>
        /// A person's figure in one pose. Painting is spread over frames: when a few new figures were
        /// already painted this frame, it returns <paramref name="fallback"/> (when given) and paints
        /// this one on a later frame, so a crowd changing poses at once doesn't stall the game.
        /// </summary>
        public static Sprite Figure(PersonLook look, Expression face, int frame, Sprite fallback = null)
        {
            long key = PeopleArt.Key(look, face) * 8 + frame;
            if (Figures.TryGetValue(key, out var cached)) return cached;
            if (fallback != null)
            {
                if (Time.frameCount != _paintFrame)
                {
                    _paintFrame = Time.frameCount;
                    _paintedThisFrame = 0;
                }
                if (_paintedThisFrame >= PaintsPerFrame) return fallback;
                _paintedThisFrame++;
            }
            if (Figures.Count > 2400) Figures.Clear();   // long sessions: let old faces go

            var drawing = PeopleArt.Figure(look, face, frame);
            int w = Mathf.CeilToInt(drawing.Width * Scale) + 4, h = Mathf.CeilToInt(drawing.Height * Scale) + 4;
            var canvas = new InkCanvas(w, h, 7);
            drawing.Rasterize(canvas, new Vector2(2f, 2f), Scale);
            float feet = h - (2f + 45f * Scale);
            var sprite = Make(canvas, "person", new Vector2(0.5f, feet / h));
            Figures[key] = sprite;
            return sprite;
        }

        /// <summary>A white thought bubble holding one of the HUD's icons.</summary>
        public static Sprite Bubble(string icon)
        {
            string key = "bubble_" + icon;
            if (Misc.TryGetValue(key, out var cached)) return cached;
            var d = new InkDrawing(30f, 30f);
            d.Shape("M15 2.5c6.9 0 12.5 4.7 12.5 10.6S21.9 23.7 15 23.7c-1.4 0-2.8-.2-4.1-.5L5.6 26.8l1.5-5C4.2 19.8 2.5 16.6 2.5 13.1 2.5 7.2 8.1 2.5 15 2.5z",
                Color.white, InkIcons.Ink, 1.7f);
            var shapes = InkIcons.Get(icon);
            if (shapes != null)
            {
                const float s = 0.7f;
                var offset = new Vector2(15f - 12f * s, 13f - 12f * s);
                foreach (var shape in shapes)
                {
                    var segs = new List<InkPath.Seg>(shape.Segs.Count);
                    foreach (var seg in shape.Segs) segs.Add(new InkPath.Seg(seg.Op, offset + seg.A * s, offset + seg.B * s, offset + seg.C * s));
                    var fill = shape.FillInk ? InkIcons.Ink : shape.Fill;
                    var stroke = shape.StrokeInk ? InkIcons.Ink : shape.Stroke;
                    d.AddPath(segs, fill, shape.Width > 0f ? stroke : Color.clear, Mathf.Max(0f, shape.Width) * s);
                }
            }
            var canvas = new InkCanvas(64, 64, 3);
            d.Rasterize(canvas, new Vector2(2f, 2f), 2f);
            var sprite = Make(canvas, key, new Vector2(0.18f, 0.08f));
            Misc[key] = sprite;
            return sprite;
        }

        public static Sprite Shadow => Simple("shadow", 48, 16, c => c.FillEllipse(24f, 8f, 20f, 6f, InkPalette.WithAlpha(InkPalette.Shadow, 0.85f)), new Vector2(0.5f, 0.5f));

        public static Sprite Ring => Simple("ring", 60, 24, c =>
        {
            c.StrokeEllipse(30f, 12f, 24f, 8f, InkPalette.Ink, 4.6f);
            c.StrokeEllipse(30f, 12f, 24f, 8f, InkPalette.Marker, 2.6f);
        }, new Vector2(0.5f, 0.5f));

        public static Sprite Ball => Simple("ball", 24, 24, c =>
        {
            c.FillCircle(12f, 12f, 9f, Color.white);
            c.StrokeCircle(12f, 12f, 9f, InkPalette.Ink, 2f);
            c.FillCircle(12f, 12f, 3f, InkPalette.Ink);
            c.FillCircle(6.5f, 15f, 1.6f, InkPalette.Ink);
            c.FillCircle(17.5f, 15f, 1.6f, InkPalette.Ink);
            c.FillCircle(12f, 4.5f, 1.6f, InkPalette.Ink);
        }, new Vector2(0.5f, 0.5f));

        static Sprite Simple(string key, int w, int h, System.Action<InkCanvas> paint, Vector2 pivot)
        {
            if (Misc.TryGetValue(key, out var cached)) return cached;
            var canvas = new InkCanvas(w, h, 5);
            paint(canvas);
            var sprite = Make(canvas, key, pivot);
            Misc[key] = sprite;
            return sprite;
        }

        static Sprite Make(InkCanvas canvas, string name, Vector2 pivot)
        {
            var tex = canvas.ToTexture(true, name);
            var sprite = Sprite.Create(tex, new Rect(0, 0, canvas.Width, canvas.Height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }
}
