using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>
    /// Hand-drawn people in the canvas style (round heads, ink outlines, rosy cheeks), built as
    /// <see cref="InkDrawing"/>s from a <see cref="PersonLook"/>: head-and-shoulders portraits for
    /// the HUD, and small full-body figures for the campus. The head is drawn once in its own box
    /// and placed into both, so a face in a file matches the kid on the map. Drawings are cached.
    /// </summary>
    public static class PeopleArt
    {
        static readonly Color Ink = InkIcons.Ink;
        static readonly Color White = Color.white;
        static readonly Color Cheek = new Color(1f, 0.549f, 0.431f, 0.55f);

        const string Fringe = "M6.3 14.6C6.8 8.8 11 6 16 6s9.2 2.8 9.7 8.6c-2.6-2.2-6-3.2-9.7-3.2s-7.1 1-9.7 3.2z";

        static readonly Dictionary<long, InkDrawing> Heads = new Dictionary<long, InkDrawing>();
        static readonly Dictionary<long, InkDrawing> Portraits = new Dictionary<long, InkDrawing>();

        /// <summary>A head-and-shoulders portrait in a 32 x 40 box.</summary>
        public static InkDrawing Portrait(PersonLook look, Expression face)
        {
            long key = Key(look, face);
            if (Portraits.TryGetValue(key, out var cached)) return cached;
            var d = new InkDrawing(32f, 40f);
            Shoulders(d, look);
            d.Place(Head(look, face), 0f, 0f, 1f);
            if (Portraits.Count > 600) Portraits.Clear();
            Portraits[key] = d;
            return d;
        }

        /// <summary>The head with hair, face and accessories, centred at (16, 16) of a 32 x 30 box.</summary>
        public static InkDrawing Head(PersonLook look, Expression face)
        {
            long key = Key(look, face);
            if (Heads.TryGetValue(key, out var cached)) return cached;
            var d = new InkDrawing(32f, 30f);
            DrawHead(d, look, face);
            if (Heads.Count > 600) Heads.Clear();
            Heads[key] = d;
            return d;
        }

        internal static long Key(PersonLook look, Expression face)
        {
            unchecked
            {
                long k = look.HairStyle;
                k = k * 31 + look.Skin.GetHashCode();
                k = k * 31 + look.Hair.GetHashCode();
                k = k * 31 + look.Outfit.GetHashCode();
                k = k * 31 + (look.Glasses ? 1 : 0) + (look.Freckles ? 2 : 0) + (look.Bow ? 4 : 0) + (look.Headband ? 8 : 0) + (look.Adult ? 16 : 0);
                k = k * 31 + (int)look.Role;
                k = k * 31 + (int)face;
                return k;
            }
        }

        /// <summary>
        /// A small full-body figure for the campus in a 32 x 46 box, feet at y = 45.
        /// Frames: 0 standing, 1 and 2 walking, 3 sitting (no legs: they're under the desk).
        /// </summary>
        public static InkDrawing Figure(PersonLook look, Expression face, int frame)
        {
            var d = new InkDrawing(32f, 46f);
            FigureBody(d, look, frame);
            d.Place(Head(look, face), 0f, 0f, 1f);
            return d;
        }

        static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        static void FigureBody(InkDrawing d, PersonLook look, int frame)
        {
            var legs = look.Adult ? InkIcons.Hex(0x3F5F93) : Ink;
            if (frame != 3)
            {
                float l = frame == 1 ? -1.6f : frame == 2 ? 1.3f : 0f;
                float r = frame == 1 ? 1.3f : frame == 2 ? -1.6f : 0f;
                foreach (var (x, dx, lift) in new[] { (13f, l, frame == 1 ? 0.8f : 0f), (19f, r, frame == 2 ? 0.8f : 0f) })
                {
                    float fx = x + dx, fy = 43.2f - lift;
                    d.Line($"M{F(x)} 35.6L{F(fx)} {F(fy)}", Ink, 4.4f);
                    d.Line($"M{F(x)} 35.6L{F(fx)} {F(fy)}", legs, 2.6f);
                    d.Ellipse(fx - 0.3f, fy + 0.9f, 2.3f, 1.4f, Ink);
                }
            }

            // Arms swing opposite to the legs
            float swing = frame == 1 ? 1.4f : frame == 2 ? -1.4f : 0f;
            foreach (var (sx, hx) in new[] { (10.6f, 7.8f - swing), (21.4f, 24.2f + swing) })
            {
                d.Line($"M{F(sx)} 28.6L{F(hx)} 34.4", Ink, 4.6f);
                d.Line($"M{F(sx)} 28.6L{F(hx)} 34.4", look.Outfit, 2.8f);
                d.Circle(hx, 34.8f, 1.5f, look.Skin, Ink, 0.9f);
            }

            d.Shape("M9.4 37.4C9 31.2 11.4 26.2 16 26.2S23 31.2 22.6 37.4z", look.Outfit, Ink, 1.4f);
            if (!look.Adult)
            {
                var collar = look.Outfit == PersonLook.GradeColors[0] ? Ink : White;
                d.Line("M13.6 26.8l2.4 2.6 2.4-2.6", collar, 1.2f);
                return;
            }
            switch (look.Role)
            {
                case StaffRole.Teacher:
                    d.Shape("M15.3 27.2h1.4l.6 4.2-1.3 1.5-1.3-1.5z", InkIcons.Hex(0x2F6FD0), Ink, 0.7f);
                    break;
                case StaffRole.Administrator:
                    d.Shape("M13.2 26.6l2.8 4.6 2.8-4.6z", White, Ink, 0.8f);
                    d.Shape("M15.4 27.6h1.2l.5 3.4-1.1 1.3-1.1-1.3z", InkIcons.Marker, Ink, 0.6f);
                    break;
                case StaffRole.Chef:
                    d.Shape("M11.6 31h8.8v6.2h-8.8z", White, Ink, 0.9f);
                    break;
                case StaffRole.Custodian:
                case StaffRole.Maintenance:
                    d.Line("M12.4 27.4v9.6M19.6 27.4v9.6", Ink, 1f);
                    break;
                case StaffRole.Nurse:
                    d.Line("M18.8 29.4v3M17.3 30.9h3", InkIcons.Coral, 1.1f);
                    break;
                case StaffRole.Security:
                    d.Shape("M18.2 29l1.5.6 1.5-.6v1.7c0 .9-.7 1.5-1.5 1.9-.8-.4-1.5-1-1.5-1.9z", InkIcons.Marker, Ink, 0.6f);
                    break;
                case StaffRole.Coach:
                    d.Line("M12.6 27.2c1.2 2.4 2.4 3.6 3.4 3.8", Ink, 0.9f);
                    d.Rect(15.2f, 30.4f, 2.8f, 1.8f, InkIcons.Marker, Ink, 0.7f, 0.7f);
                    break;
                case StaffRole.Counselor:
                    d.Shape("M19.2 31.6c0 0-2-1.2-2-2.5a1 1 0 0 1 2-.3 1 1 0 0 1 2 .3c0 1.3-2 2.5-2 2.5z", InkIcons.Marker, Ink, 0.6f);
                    break;
            }
        }

        // --- Shoulders --------------------------------------------------------------------------

        static void Shoulders(InkDrawing d, PersonLook look)
        {
            if (!look.Adult)
            {
                d.Shape("M5 41c0-7.5 4.8-11.5 11-11.5S27 33.5 27 41z", look.Outfit, Ink, 1.6f);
                var collar = look.Outfit == PersonLook.GradeColors[0] ? Ink : White;
                d.Line("M12.4 29.8l3.6 3.6 3.6-3.6", collar, 1.3f);
                return;
            }

            d.Shape("M3.6 41c0-8 5.4-12.4 12.4-12.4S28.4 33 28.4 41z", look.Outfit, Ink, 1.6f);
            switch (look.Role)
            {
                case StaffRole.Teacher:
                    d.Shape("M15.2 29.2h1.6l.7 4.6-1.5 1.7-1.5-1.7z", InkIcons.Hex(0x2F6FD0), Ink, 0.9f);
                    break;
                case StaffRole.Administrator:
                    d.Shape("M12.8 28.8l3.2 6 3.2-6z", White, Ink, 0.9f);
                    d.Shape("M15.3 30.2h1.4l.6 4-1.3 1.5-1.3-1.5z", InkIcons.Marker, Ink, 0.8f);
                    break;
                case StaffRole.Coach:
                    d.Line("M12.2 29.4c1.4 2.6 2.6 4 3.8 4.2", Ink, 1f);
                    d.Rect(15.2f, 32.6f, 3.2f, 2f, InkIcons.Marker, Ink, 0.8f, 0.8f);
                    break;
                case StaffRole.Nurse:
                case StaffRole.Counselor:
                    d.Shape("M20.4 33.2c0 0-2.4-1.4-2.4-3a1.2 1.2 0 0 1 2.4-.4 1.2 1.2 0 0 1 2.4.4c0 1.6-2.4 3-2.4 3z",
                        look.Role == StaffRole.Nurse ? InkIcons.Coral : InkIcons.Marker, Ink, 0.7f);
                    break;
                case StaffRole.Chef:
                    d.Line("M13 30.4v6M19 30.4v6", Ink, 0.9f);
                    d.Circle(14.6f, 33f, 0.7f, Ink);
                    d.Circle(17.4f, 33f, 0.7f, Ink);
                    break;
                case StaffRole.Security:
                    d.Shape("M19.6 31.6l1.6.6 1.6-.6v1.8c0 1-.8 1.6-1.6 2-.8-.4-1.6-1-1.6-2z", InkIcons.Marker, Ink, 0.7f);
                    break;
                case StaffRole.Custodian:
                case StaffRole.Maintenance:
                    d.Line("M11.6 29.4v8M20.4 29.4v8", Ink, 1.1f);
                    break;
            }
        }

        // --- Head ---------------------------------------------------------------------------

        static bool WearsHat(PersonLook look) => look.Adult && (look.Role == StaffRole.Chef || look.Role == StaffRole.Custodian
            || look.Role == StaffRole.Maintenance || look.Role == StaffRole.Security || look.Role == StaffRole.Coach || look.Role == StaffRole.Nurse);

        static void DrawHead(InkDrawing d, PersonLook look, Expression face)
        {
            bool hat = WearsHat(look);

            // Hair that sits behind the head
            if (!hat || look.HairStyle == 7 || look.HairStyle == 1)
            {
                switch (look.HairStyle)
                {
                    case 1: d.Shape("M5.6 16.6C5.4 9.6 10 5.6 16 5.6s10.6 4 10.4 11v8.2c-1.6.9-3.2.9-4.6 0v-7.4H10.2v7.4c-1.4.9-3 .9-4.6 0z", look.Hair, Ink, 1.3f); break;
                    case 2: if (!hat) d.Circle(16f, 4.8f, 3.4f, look.Hair, Ink, 1.3f); break;
                    case 6:
                        if (hat) break;
                        d.Shape("M23.8 9.4c3.6.4 5.6 3.6 5 7.8-.4 2.6-1.8 4.4-3.4 5.2.6-2.4.4-4.6-.6-6.4z", look.Hair, Ink, 1.3f);
                        d.Circle(26.2f, 10.4f, 1.2f, look.Bow ? InkIcons.Coral : InkIcons.Marker, Ink, 0.8f);
                        break;
                    case 7: d.Shape("M5.4 17C5 9.8 9.8 5.4 16 5.4S27 9.8 26.6 17c-.2 4.6.6 8.6 2 11.4-3 .8-6 .2-7.8-1.6V18.4H11.2v8.4C9.4 28.6 6.4 29.2 3.4 28.4c1.4-2.8 2.2-6.8 2-11.4z", look.Hair, Ink, 1.3f); break;
                }
            }

            d.Circle(16f, 16f, 10f, look.Skin, Ink, 1.6f);

            if (hat) Hat(d, look.Role);
            else
            {
                switch (look.HairStyle)
                {
                    case 3:
                        d.Shape("M6.2 15.4c-.7-2.2.3-4 1.9-4.6.2-2.2 2-3.6 4-3.2 1-1.6 3-2 4.6-1 1.8-.8 3.8 0 4.6 1.8 2 .2 3.4 2 3 4.2 1.4.8 1.8 2.2 1.2 3.4-2.6-1.8-6.2-2.8-9.4-2.8s-7.1.8-9.9 2.2z", look.Hair, Ink, 1.3f);
                        break;
                    case 4:
                        d.Circle(6.6f, 11.4f, 3.6f, look.Hair, Ink, 1.3f);
                        d.Circle(25.4f, 11.4f, 3.6f, look.Hair, Ink, 1.3f);
                        d.Shape(Fringe, look.Hair, Ink, 1.3f);
                        break;
                    case 5:
                        d.Shape("M6.4 15.2L7 10.4 9.4 11.6 10.4 7.6 13.2 9.6 15.4 6 17.8 9.4 20.8 7.4 21.6 11.2 24.6 10.2 25.6 15.2c-2.6-2-6-3-9.6-3s-7 1-9.6 3z", look.Hair, Ink, 1.3f);
                        break;
                    default:
                        d.Shape(Fringe, look.Hair, Ink, 1.3f);
                        break;
                }
                if (look.Headband) d.Line("M6.8 12.6C9 10 12.4 8.6 16 8.6s7 1.4 9.2 4", InkIcons.Marker, 1.6f);
                if (look.Bow && look.HairStyle != 6)
                {
                    d.Shape("M20.6 6.6l2.4 1.6-2.4 1.6zM25.4 6.6l-2.4 1.6 2.4 1.6z", InkIcons.Coral, Ink, 0.9f);
                    d.Circle(23f, 8.2f, 0.9f, InkIcons.Coral, Ink, 0.7f);
                }
            }

            Face(d, look, face);
        }

        static void Hat(InkDrawing d, StaffRole role)
        {
            switch (role)
            {
                case StaffRole.Chef:
                    d.Shape("M9.4 10.4c-2.8-.6-3.4-4.6-.6-5.8.4-3.4 4.8-4.4 6.8-2 2-2.4 6.4-1.4 6.8 2 2.8 1.2 2.2 5.2-.6 5.8z", White, Ink, 1.2f);
                    d.Rect(9.2f, 9.6f, 13.6f, 3f, White, Ink, 1.2f, 1f);
                    break;
                case StaffRole.Nurse:
                    d.Shape("M8.4 10.8c.6-3.6 3.8-5.6 7.6-5.6s7 2 7.6 5.6z", White, Ink, 1.2f);
                    d.Line("M16 6.8v3M14.5 8.3h3", InkIcons.Coral, 1.2f);
                    break;
                case StaffRole.Maintenance:
                    d.Shape("M6.4 12.4c0-5 4.2-8 9.6-8s9.6 3 9.6 8z", InkIcons.Marker, Ink, 1.3f);
                    d.Line("M4.6 12.6h22.8", Ink, 1.5f);
                    d.Line("M16 4.6v7.6", Ink, 1f);
                    break;
                default:
                    var cap = role == StaffRole.Security ? InkIcons.Ink : role == StaffRole.Coach ? InkIcons.Coral : InkIcons.Marker;
                    d.Shape("M6.6 12.8c0-5.2 4.2-8.4 9.4-8.4s9.4 3.2 9.4 8.4z", cap, Ink, 1.3f);
                    d.Line("M5 13h12.4", Ink, 1.6f);
                    if (role == StaffRole.Security) d.Circle(16f, 8.6f, 1.4f, InkIcons.Marker, Ink, 0.7f);
                    break;
            }
        }

        static void Face(InkDrawing d, PersonLook look, Expression face)
        {
            // Eyes
            if (face == Expression.Joyful) d.Line("M10.8 17.8q1.5-1.6 3 0M18.2 17.8q1.5-1.6 3 0", Ink, 1.3f);
            else if (face == Expression.Sleepy) d.Line("M10.8 17.8h3M18.2 17.8h3", Ink, 1.3f);
            else
            {
                d.Circle(12.4f, 17.4f, 1.4f, Ink);
                d.Circle(19.6f, 17.4f, 1.4f, Ink);
                if (face == Expression.Sad || face == Expression.Glum) d.Line("M10.8 14.4l2.6.8M21.2 14.4l-2.6.8", Ink, 1f);
            }
            if (look.Glasses)
            {
                d.Circle(12.4f, 17.4f, 2.8f, Color.clear, Ink, 1.1f);
                d.Circle(19.6f, 17.4f, 2.8f, Color.clear, Ink, 1.1f);
                d.Line("M15.2 17.2h1.6", Ink, 1.1f);
            }

            // Cheeks and freckles
            d.Ellipse(9.8f, 20.6f, 1.9f, 1.1f, Cheek);
            d.Ellipse(22.2f, 20.6f, 1.9f, 1.1f, Cheek);
            if (look.Freckles)
            {
                var spot = InkIcons.Hex(0xB57A54);
                d.Circle(8.6f, 19.4f, 0.45f, spot);
                d.Circle(10.2f, 19.8f, 0.45f, spot);
                d.Circle(21.8f, 19.8f, 0.45f, spot);
                d.Circle(23.4f, 19.4f, 0.45f, spot);
            }

            // Mouth
            switch (face)
            {
                case Expression.Joyful: d.Shape("M14 20.6h4c0 1.7-.9 2.7-2 2.7s-2-1-2-2.7z", Ink); break;
                case Expression.Happy: d.Line("M13.8 21c1.2 1.4 3.2 1.4 4.4 0", Ink, 1.3f); break;
                case Expression.Calm: d.Line("M14.4 21.4c1 .5 2.2.5 3.2 0", Ink, 1.2f); break;
                case Expression.Glum: d.Line("M14.2 21.8h3.6", Ink, 1.2f); break;
                case Expression.Sad: d.Line("M13.8 22.6c1.2-1.3 3.2-1.3 4.4 0", Ink, 1.3f); break;
                case Expression.Stressed: d.Line("M13.4 21.9l1.15-.75 1.15.75 1.15-.75 1.15.75", Ink, 1.1f); break;
                case Expression.Sleepy: d.Ellipse(16f, 21.6f, 1.1f, 1.3f, Ink); break;
            }
            if (face == Expression.Stressed)
                d.Shape("M25.2 9.4c1 1.4 1.6 2.4 1.6 3.2a1.6 1.6 0 0 1-3.2 0c0-.8.6-1.8 1.6-3.2z", InkIcons.Sky, Ink, 0.9f);
            if (face == Expression.Sad)
                d.Shape("M9.4 20.6c.6.9 1 1.5 1 2a1 1 0 0 1-2 0c0-.5.4-1.1 1-2z", InkIcons.Sky, Ink, 0.7f);
        }
    }
}
