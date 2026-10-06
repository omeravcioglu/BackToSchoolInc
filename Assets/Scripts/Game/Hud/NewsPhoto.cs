using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The lead story's "photo" in the newspaper: a halftone-dotted snapshot of the story's topic,
    /// drawn in ink from the HUD's own icons (a trophy on the field for sports, books for academics,
    /// the school building for campus news...).
    /// </summary>
    public sealed class NewsPhoto : VisualElement
    {
        static readonly Color Dots = InkIcons.Hex(0xBCD5F0);
        static readonly Color Ground = InkIcons.Hex(0x9CC3EC);
        static readonly Color Shadow = new Color(0.66f, 0.78f, 0.92f, 0.55f);

        NewsTopic _topic;
        bool _good = true;

        public NewsPhoto()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Show(NewsTopic topic, bool good)
        {
            if (topic == _topic && good == _good) return;
            _topic = topic;
            _good = good;
            MarkDirtyRepaint();
        }

        /// <summary>The scene for a topic: the main subject and two smaller things beside it.</summary>
        static (string main, string left, string right) Scene(NewsTopic t, bool good) => t switch
        {
            NewsTopic.Academics => ("book", "pencil", good ? "star" : "board"),
            NewsTopic.Sports => ("trophy", "flag", "star"),
            NewsTopic.Safety => ("shield", "lock", "bell"),
            NewsTopic.Food => ("apple", "mug", good ? "heart" : "cloud"),
            NewsTopic.Dorms => ("bed", "sun", "house"),
            NewsTopic.StudentLife => ("music", "smile", "sparkle"),
            NewsTopic.Money => ("coin", "coin", "chart"),
            NewsTopic.Staff => ("staff", "board", "mug"),
            NewsTopic.Rankings => ("podium", "star", "trophy"),
            NewsTopic.Campus => ("house", "sprout", "sprout"),
            NewsTopic.Rivals => ("crest", "podium", "news"),
            _ => ("crest", "house", "students"),
        };

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 20f || h < 20f || float.IsNaN(w) || float.IsNaN(h)) return;
            var p = ctx.painter2D;

            // Newsprint halftone: dots that grow toward the middle, like a printed photo's light.
            var centre = new Vector2(w * 0.5f, h * 0.45f);
            float reach = Mathf.Max(w, h) * 0.62f;
            p.fillColor = Dots;
            p.BeginPath();
            const float step = 9f;
            for (float y = step * 0.5f; y < h; y += step)
            {
                float shift = Mathf.Repeat(y / step, 2f) < 1f ? 0f : step * 0.5f;
                for (float x = step * 0.5f + shift; x < w; x += step)
                {
                    float d = Vector2.Distance(new Vector2(x, y), centre) / reach;
                    float r = Mathf.Lerp(1.9f, 0.3f, Mathf.Clamp01(d));
                    if (r < 0.45f) continue;
                    p.MoveTo(new Vector2(x + r, y));
                    p.Arc(new Vector2(x, y), r, 0f, 360f);
                }
            }
            p.Fill();

            // The ground the scene stands on.
            float groundY = h * 0.8f;
            p.strokeColor = Ground;
            p.lineWidth = 2f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(w * 0.08f, groundY));
            p.BezierCurveTo(new Vector2(w * 0.35f, groundY - 3f), new Vector2(w * 0.65f, groundY + 3f), new Vector2(w * 0.92f, groundY));
            p.Stroke();

            var (main, left, right) = Scene(_topic, _good);
            float big = Mathf.Min(h * 0.62f, w * 0.3f), small = big * 0.52f;
            Thing(p, right, new Vector2(w * 0.72f, groundY), small, 8f);
            Thing(p, left, new Vector2(w * 0.28f, groundY), small, -8f);
            Thing(p, main, new Vector2(w * 0.5f, groundY), big, 0f);
        }

        /// <summary>One icon standing on the ground at <paramref name="foot"/>, with a soft shadow.</summary>
        static void Thing(Painter2D p, string icon, Vector2 foot, float size, float tilt)
        {
            var shapes = InkIcons.Get(icon);
            if (shapes == null) return;
            p.fillColor = Shadow;
            p.BeginPath();
            InkPath.Emit(p, InkPath.Ellipse(foot.x, foot.y, size * 0.42f, size * 0.09f), Vector2.zero, 1f);
            p.Fill();

            float scale = size / 24f;
            var offset = new Vector2(foot.x - size * 0.5f, foot.y - size * 0.96f);
            // A gentle tilt for the props: rotate each point around the foot.
            float a = tilt * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            foreach (var s in shapes)
            {
                var segs = tilt == 0f ? s.Segs : Rotate(s.Segs, offset, scale, foot, cos, sin);
                var o = tilt == 0f ? offset : Vector2.zero;
                float k = tilt == 0f ? scale : 1f;
                var fill = s.FillInk ? InkIcons.Ink : s.Fill;
                if (fill.a > 0f)
                {
                    p.fillColor = fill;
                    p.BeginPath();
                    InkPath.Emit(p, segs, o, k);
                    p.Fill();
                }
                if (s.Width <= 0f) continue;
                var stroke = s.StrokeInk ? InkIcons.Ink : s.Stroke;
                if (stroke.a <= 0f) continue;
                p.strokeColor = stroke;
                p.lineWidth = Mathf.Min(s.Width, 1.6f) * scale;
                p.BeginPath();
                InkPath.Emit(p, segs, o, k);
                p.Stroke();
            }
        }

        static System.Collections.Generic.List<InkPath.Seg> Rotate(System.Collections.Generic.List<InkPath.Seg> segs, Vector2 offset, float scale,
            Vector2 pivot, float cos, float sin)
        {
            Vector2 F(Vector2 v)
            {
                var q = offset + v * scale - pivot;
                return pivot + new Vector2(q.x * cos - q.y * sin, q.x * sin + q.y * cos);
            }
            var result = new System.Collections.Generic.List<InkPath.Seg>(segs.Count);
            foreach (var s in segs) result.Add(new InkPath.Seg(s.Op, F(s.A), F(s.B), F(s.C)));
            return result;
        }
    }
}
