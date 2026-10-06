using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// A pencil sketch of a saved campus for the Load screen's photos and the title's Continue
    /// button: the land the school owns (dashed), every room as a little floor plan, fields and
    /// trees in sky blue, and dashed blue outlines for sites still being built.
    /// </summary>
    public sealed class MiniCampus : VisualElement
    {
        static readonly Color Ground = InkIcons.Hex(0xEEF5FD), Grid = InkIcons.Hex(0xDCE9F7);
        static readonly Color Land = InkIcons.Hex(0xE4F0FC), LandEdge = InkIcons.Hex(0x9CC3EC);
        static readonly Color Field = InkIcons.Hex(0xBFDCF6), Water = InkIcons.Hex(0xA8D1F7), Lawn = InkIcons.Hex(0xD6E8FA);
        static readonly Color Site = InkIcons.Hex(0x2F6FD0);

        SaveSummary _summary;

        public MiniCampus()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Show(SaveSummary summary)
        {
            _summary = summary;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (w < 1f || h < 1f || float.IsNaN(w) || float.IsNaN(h)) return;
            Sketch(_summary, w, h).Draw(ctx.painter2D, Vector2.zero, 1f);
        }

        /// <summary>The sketch in pixels for a w x h photo.</summary>
        static InkDrawing Sketch(SaveSummary s, float w, float h)
        {
            var d = new InkDrawing(w, h);
            d.Rect(0f, 0f, w, h, Ground);
            float step = w / 9f;
            var grid = new StringBuilder();
            for (float x = step; x < w; x += step) grid.Append($"M{F(x)} 0V{F(h)}");
            for (float y = step; y < h; y += step) grid.Append($"M0 {F(y)}H{F(w)}");
            if (grid.Length > 0) d.Line(grid.ToString(), Grid, 1f, round: false);
            if (s?.Parcels == null || s.ParcelsX <= 0 || s.ParcelsY <= 0 || s.ParcelSize <= 0) return d;

            // Frame the owned land with a little room around it. Tile y runs up, the photo's down.
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int py = 0; py < s.ParcelsY; py++)
                for (int px = 0; px < s.ParcelsX; px++)
                    if (Owned(s, px, py))
                    {
                        minX = Mathf.Min(minX, px);
                        maxX = Mathf.Max(maxX, px);
                        minY = Mathf.Min(minY, py);
                        maxY = Mathf.Max(maxY, py);
                    }
            if (maxX < 0) return d;
            float p = s.ParcelSize;
            float x0 = minX * p, x1 = (maxX + 1) * p, y0 = minY * p, y1 = (maxY + 1) * p;
            float margin = Mathf.Max(x1 - x0, y1 - y0) * 0.08f;
            float scale = Mathf.Min(w / (x1 - x0 + 2f * margin), h / (y1 - y0 + 2f * margin));
            float ox = (w - (x1 - x0) * scale) / 2f, oy = (h - (y1 - y0) * scale) / 2f;
            float X(float x) => ox + (x - x0) * scale;
            float Y(float y) => oy + (y1 - y) * scale;
            float ink = Mathf.Clamp(w / 130f, 0.8f, 2.2f);

            // Owned land: pale parcels with a dashed edge where the school's land ends.
            var edge = new StringBuilder();
            for (int py = 0; py < s.ParcelsY; py++)
                for (int px = 0; px < s.ParcelsX; px++)
                {
                    if (!Owned(s, px, py)) continue;
                    float l = X(px * p), r = X((px + 1) * p), top = Y((py + 1) * p), bottom = Y(py * p);
                    d.Rect(l, top, r - l, bottom - top, Land);
                    float i = ink * 1.2f;   // keep the dashes just inside the land
                    if (!Owned(s, px - 1, py)) edge.Append($"M{F(l + i)} {F(top)}V{F(bottom)}");
                    if (!Owned(s, px + 1, py)) edge.Append($"M{F(r - i)} {F(top)}V{F(bottom)}");
                    if (!Owned(s, px, py + 1)) edge.Append($"M{F(l)} {F(top + i)}H{F(r)}");
                    if (!Owned(s, px, py - 1)) edge.Append($"M{F(l)} {F(bottom - i)}H{F(r)}");
                }
            if (edge.Length > 0) d.Dashed(edge.ToString(), LandEdge, ink, ink * 3.5f, ink * 2.5f);

            // Big outdoor spaces first, then buildings, then the small things on top.
            for (int pass = 0; pass < 3; pass++)
                foreach (var room in s.Rooms)
                {
                    var def = DefOf(room.Type);
                    int layer = def == null ? 1 : def.Category == RoomCategory.Decor ? 2 : def.Outdoor ? 0 : 1;
                    if (layer != pass) continue;
                    float l = X(room.X), top = Y(room.Y + room.H), rw = room.W * scale, rh = room.H * scale;
                    if (room.Building)
                    {
                        d.Rect(l, top, rw, rh, new Color(1f, 1f, 1f, 0.55f));
                        d.Dashed(RectPath(l + ink, top + ink, rw - 2f * ink, rh - 2f * ink), Site, ink * 1.1f, ink * 2.6f, ink * 2f);
                        continue;
                    }
                    DrawRoom(d, room.Type, def, l, top, rw, rh, ink);
                }
            return d;
        }

        static void DrawRoom(InkDrawing d, RoomType type, RoomDefinition def, float l, float t, float w, float h, float ink)
        {
            float cx = l + w / 2f, cy = t + h / 2f, small = Mathf.Min(w, h);
            switch (type)
            {
                case RoomType.Tree:
                    d.Circle(cx, cy, small * 0.48f, Field, InkIcons.Ink, ink * 0.8f);
                    return;
                case RoomType.FlowerBed:
                    d.Ellipse(cx, cy, w * 0.46f, h * 0.42f, InkIcons.Marker, InkIcons.Ink, ink * 0.7f);
                    return;
                case RoomType.Fountain:
                    d.Ellipse(cx, cy, w * 0.45f, h * 0.45f, Water, InkIcons.Ink, ink * 0.8f);
                    return;
                case RoomType.Statue:
                    d.Circle(cx, cy, small * 0.32f, InkIcons.Pale, InkIcons.Ink, ink * 0.8f);
                    return;
                case RoomType.Bench:
                    d.Rect(l + w * 0.1f, cy - h * 0.25f, w * 0.8f, h * 0.5f, InkIcons.Paper, InkIcons.Ink, ink * 0.6f);
                    return;
                case RoomType.SwimmingPool:
                    d.Rect(l, t, w, h, Water, InkIcons.Ink, ink, ink);
                    return;
                case RoomType.SoccerField:
                case RoomType.PracticeField:
                case RoomType.SportsComplex:
                case RoomType.Track:
                case RoomType.BasketballCourt:
                case RoomType.TennisCourt:
                    d.Rect(l, t, w, h, Field, InkIcons.Ink, ink, ink);
                    if (small > 14f)
                    {
                        bool wide = w >= h;
                        d.Line(wide ? $"M{F(cx)} {F(t + ink)}V{F(t + h - ink)}" : $"M{F(l + ink)} {F(cy)}H{F(l + w - ink)}", InkIcons.Paper, ink);
                        d.Circle(cx, cy, small * 0.16f, Color.clear, InkIcons.Paper, ink);
                    }
                    return;
            }
            if (def != null && def.Outdoor)
            {
                d.Rect(l, t, w, h, Lawn, LandEdge, ink * 0.8f, ink);
                return;
            }
            var fill = def == null ? InkIcons.Pale : def.Category switch
            {
                RoomCategory.Academic => InkIcons.Pale,
                RoomCategory.Residential => InkIcons.Hex(0xE8F2FC),
                RoomCategory.Dining => Lawn,
                RoomCategory.StudentLife => InkIcons.Hex(0xC9E1F9),
                RoomCategory.Sports => Field,
                _ => InkIcons.Paper,
            };
            d.Rect(l, t, w, h, fill, InkIcons.Ink, ink);
        }

        static RoomDefinition DefOf(RoomType type)
        {
            try
            {
                return RoomCatalog.Get(type);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return null;   // a room this version doesn't know: drawn as a plain room
            }
        }

        static bool Owned(SaveSummary s, int px, int py) =>
            px >= 0 && py >= 0 && px < s.ParcelsX && py < s.ParcelsY && py * s.ParcelsX + px < s.Parcels.Length && s.Parcels[py * s.ParcelsX + px];

        static string RectPath(float x, float y, float w, float h) => $"M{F(x)} {F(y)}H{F(x + w)}V{F(y + h)}H{F(x)}Z";

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
