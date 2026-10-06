using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>
    /// Draws rooms as ballpoint sketches: walls with a door swing, and furniture that reflects the
    /// room's level and capacity (a level-1 dorm really shows three bunk beds for six boarders).
    /// Rooms are drawn door-south; <see cref="InkCanvas.Rotated"/> turns them afterwards.
    /// </summary>
    static class RoomPainter
    {
        const int T = CampusArt.PPU;
        const int P = CampusArt.RoomPad;

        public static InkCanvas Paint(RoomType type, int level)
        {
            var def = RoomCatalog.Get(type);
            var c = new InkCanvas(def.Width * T + P * 2, def.Height * T + P * 2, (int)type * 17 + level * 3 + 1);
            var p = new Painter(c, def.Width, def.Height, level);
            c.Wobble = 0.55f;

            switch (type)
            {
                case RoomType.Classroom: p.Classroom(def.Capacity[level - 1]); break;
                case RoomType.DormRoom: p.Dorm(); break;
                case RoomType.Cafeteria: p.Cafeteria(); break;
                case RoomType.Bathroom: p.Bathroom(); break;
                case RoomType.LaundryRoom: p.Laundry(); break;
                case RoomType.Library: p.Library(); break;
                case RoomType.ScienceLab: p.ScienceLab(); break;
                case RoomType.ComputerLab: p.ComputerLab(); break;
                case RoomType.Office: p.Office(def.Capacity[level - 1]); break;
                case RoomType.StaffLounge: p.StaffLounge(); break;
                case RoomType.StudentLounge: p.StudentLounge(); break;
                case RoomType.GamingRoom: p.GamingRoom(); break;
                case RoomType.NurseOffice: p.NurseOffice(); break;
                case RoomType.SecurityRoom: p.SecurityRoom(); break;
                case RoomType.ClubRoom: p.ClubRoom(); break;
                case RoomType.Gym: p.Gym(); break;
                case RoomType.BasketballCourt: p.BasketballCourt(); break;
                case RoomType.SoccerField: p.SoccerField(); break;
                case RoomType.SwimmingPool: p.Pool(); break;
                case RoomType.Track: p.Track(); break;
                case RoomType.TennisCourt: p.Tennis(); break;
                case RoomType.SportsComplex: p.SportsComplex(); break;
                case RoomType.Playground: p.Playground(); break;
                case RoomType.Garden: p.Garden(); break;
                case RoomType.PracticeField: p.PracticeField(); break;
                case RoomType.PicnicArea: p.PicnicArea(); break;
                case RoomType.Tree: p.DecorTree(); break;
                case RoomType.FlowerBed: p.FlowerBed(); break;
                case RoomType.Bench: p.Bench(); break;
                case RoomType.Fountain: p.Fountain(); break;
                case RoomType.Statue: p.Statue(); break;
                case RoomType.ScholarsHall: p.ScholarsHall(); break;
                case RoomType.Arena: p.Arena(); break;
                case RoomType.Clubhouse: p.Clubhouse(); break;
                case RoomType.Theatre: p.Theatre(); break;
                case RoomType.InnovationLab: p.InnovationLab(); break;
                case RoomType.CommunityCentre: p.CommunityCentre(); break;
                case RoomType.InternationalHouse: p.InternationalHouse(); break;
                case RoomType.Manor: p.Manor(); break;
            }

            if (def.Decor) return c;          // no walls, no gate
            if (def.Outdoor) p.Fence();
            else p.Walls();
            return c;
        }

        /// <summary>A construction site: dashed outline, half-built walls, bricks, cones and a crane.</summary>
        public static InkCanvas PaintSite(int w, int h)
        {
            var c = new InkCanvas(w * T + P * 2, h * T + P * 2, w * 13 + h);
            var p = new Painter(c, w, h, 1);
            float W = w * T, H = h * T;
            c.Line(p.X(0), p.Y(0), p.X(W), p.Y(0), InkPalette.Blue, 4f, 16f, 9f);
            c.Line(p.X(W), p.Y(0), p.X(W), p.Y(H), InkPalette.Blue, 4f, 16f, 9f);
            c.Line(p.X(W), p.Y(H), p.X(0), p.Y(H), InkPalette.Blue, 4f, 16f, 9f);
            c.Line(p.X(0), p.Y(H), p.X(0), p.Y(0), InkPalette.Blue, 4f, 16f, 9f);

            c.Wobble = 1.1f;
            c.Line(p.X(0), p.Y(H * 0.55f), p.X(0), p.Y(0), InkPalette.Ink, 8f);
            c.Line(p.X(0), p.Y(0), p.X(W * 0.5f), p.Y(0), InkPalette.Ink, 8f);
            c.Wobble = 0.5f;

            // Bricks
            for (int i = 0; i < 3; i++)
            {
                float bx = 26 + (i % 2) * 22 + (i == 2 ? 11 : 0), by = 20 + (i == 2 ? 13 : 0);
                p.Rect(bx, by, bx + 20, by + 12, InkPalette.Sky, 1.8f, 2f);
            }
            // Cones
            for (int i = 0; i < 2; i++)
            {
                float cx = W - 40 - i * 22;
                c.FillTriangle(p.X(cx - 8), p.Y(14), p.X(cx + 8), p.Y(14), p.X(cx), p.Y(38), InkPalette.Coral);
                c.StrokeTriangle(p.X(cx - 8), p.Y(14), p.X(cx + 8), p.Y(14), p.X(cx), p.Y(38), InkPalette.Ink, 2f);
                c.Line(p.X(cx - 4), p.Y(24), p.X(cx + 4), p.Y(24), Color.white, 2.2f);
            }
            // Crane
            if (w >= 4 && h >= 3)
            {
                float mx = W - 36;
                c.Line(p.X(mx), p.Y(40), p.X(mx), p.Y(H - 30), InkPalette.Ink, 3.5f);
                c.Line(p.X(mx - 90), p.Y(H - 30), p.X(mx + 16), p.Y(H - 30), InkPalette.Ink, 3.5f);
                c.Line(p.X(mx), p.Y(H - 44), p.X(mx - 24), p.Y(H - 30), InkPalette.Ink, 2f);
                c.Line(p.X(mx - 70), p.Y(H - 30), p.X(mx - 70), p.Y(H - 80), InkPalette.Ink, 1.6f);
                p.Rect(mx - 80, H - 96, mx - 60, H - 80, InkPalette.Marker, 1.8f, 2f);
                p.Rect(mx - 12, 22, mx + 12, 40, InkPalette.Marker, 2f, 3f);
            }
            return c;
        }

        /// <summary>Drawing helpers in room-local pixels (origin at the footprint's bottom-left).</summary>
        sealed class Painter
        {
            readonly InkCanvas _c;
            readonly int _w, _h;
            readonly int _level;

            public Painter(InkCanvas c, int w, int h, int level)
            {
                _c = c;
                _w = w;
                _h = h;
                _level = level;
            }

            float Wpx => _w * T;
            float Hpx => _h * T;
            float DoorX => (_w / 2 + 0.5f) * T;     // matches Placement.DoorCell for a south door

            public float X(float x) => P + x;
            public float Y(float y) => P + y;

            static Color Ink => InkPalette.Ink;

            // --- primitives ---------------------------------------------------------------

            public void Rect(float x0, float y0, float x1, float y1, Color fill, float stroke = 2.5f, float radius = 4f)
            {
                _c.Box(X(x0), Y(y0), X(x1), Y(y1), fill, stroke, radius);
            }

            void Dot(float x, float y, float r, Color fill, float stroke = 2f) => _c.Dot(X(x), Y(y), r, fill, stroke);
            void Line(float x0, float y0, float x1, float y1, Color col, float w, float dash = 0f, float gap = 0f) => _c.Line(X(x0), Y(y0), X(x1), Y(y1), col, w, dash, gap);

            void Chair(float x, float y, float r = 7f) => Dot(x, y, r, InkPalette.Sky, 1.8f);

            void Monitor(float cx, float cy, float w = 18f, float h = 11f)
            {
                Rect(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2, InkPalette.Pale, 1.8f, 2f);
            }

            void Bed(float x, float y, float w, float h, Color blanket, bool bunk = false)
            {
                if (bunk) Rect(x + 5, y - 5, x + w + 5, y + h - 5, InkPalette.Pale, 2f, 5f);
                Rect(x, y, x + w, y + h, Color.white, 2.2f, 5f);
                _c.FillRect(X(x + 2), Y(y + 2), X(x + w - 2), Y(y + h * 0.62f), blanket, 3f);
                Line(x + 3, y + h * 0.62f, x + w - 3, y + h * 0.62f, Ink, 1.5f);
                Rect(x + 6, y + h - 22, x + w - 6, y + h - 7, Color.white, 1.6f, 4f);
            }

            void Plant(float x, float y, float s = 1f)
            {
                Rect(x - 7 * s, y - 16 * s, x + 7 * s, y - 4 * s, InkPalette.Marker, 1.6f, 2f);
                Dot(x - 6 * s, y + 2 * s, 7 * s, InkPalette.Foliage, 1.6f);
                Dot(x + 6 * s, y + 3 * s, 7 * s, InkPalette.Foliage, 1.6f);
                Dot(x, y + 9 * s, 7.5f * s, InkPalette.Foliage, 1.6f);
            }

            void Rug(float cx, float cy, float rx, float ry)
            {
                _c.FillEllipse(X(cx), Y(cy), rx, ry, InkPalette.Foliage);
                _c.StrokeEllipse(X(cx), Y(cy), rx, ry, InkPalette.WithAlpha(Ink, 0.6f), 1.4f);
            }

            void Shelf(float x0, float y0, float x1, float y1)
            {
                Rect(x0, y0, x1, y1, Color.white, 2.2f, 2f);
                var colors = new[] { InkPalette.Mid, InkPalette.Sky, InkPalette.Marker, InkPalette.Blue, InkPalette.Pale };
                bool horizontal = (x1 - x0) >= (y1 - y0);
                int i = 0;
                if (horizontal)
                    for (float x = x0 + 5; x < x1 - 4; x += 6, i++) Line(x, y0 + 4, x, y1 - 4, colors[i % colors.Length], 3.4f);
                else
                    for (float y = y0 + 5; y < y1 - 4; y += 6, i++) Line(x0 + 4, y, x1 - 4, y, colors[i % colors.Length], 3.4f);
            }

            void Sofa(float x0, float y0, float x1, float y1, Color fill)
            {
                Rect(x0, y0, x1, y1, fill, 2.2f, 9f);
                bool horizontal = (x1 - x0) >= (y1 - y0);
                if (horizontal)
                {
                    Line(x0 + 6, y1 - 11, x1 - 6, y1 - 11, Ink, 1.5f);
                    for (float x = x0 + (x1 - x0) / 3f; x < x1 - 8; x += (x1 - x0) / 3f) Line(x, y0 + 5, x, y1 - 12, Ink, 1.2f);
                }
                else
                {
                    Line(x0 + 11, y0 + 6, x0 + 11, y1 - 6, Ink, 1.5f);
                    for (float y = y0 + (y1 - y0) / 3f; y < y1 - 8; y += (y1 - y0) / 3f) Line(x0 + 12, y, x1 - 5, y, Ink, 1.2f);
                }
            }

            void Toilet(float cx, float cy)
            {
                Rect(cx - 9, cy + 7, cx + 9, cy + 15, Color.white, 1.6f, 2f);
                _c.FillEllipse(X(cx), Y(cy), 8f, 10f, Color.white);
                _c.StrokeEllipse(X(cx), Y(cy), 8f, 10f, Ink, 1.7f);
            }

            void Sink(float cx, float cy)
            {
                Rect(cx - 12, cy - 9, cx + 12, cy + 9, Color.white, 1.8f, 3f);
                _c.StrokeEllipse(X(cx), Y(cy), 7f, 5f, InkPalette.Mid, 1.5f);
            }

            void Washer(float x, float y, float s)
            {
                Rect(x, y, x + s, y + s, Color.white, 2f, 4f);
                Dot(x + s / 2, y + s / 2 - 2, s * 0.3f, InkPalette.Sky, 1.8f);
                Line(x + 5, y + s - 7, x + s * 0.45f, y + s - 7, Ink, 1.4f);
            }

            void Screen(float x0, float y0, float x1, float y1)
            {
                Rect(x0, y0, x1, y1, InkPalette.Board, 2f, 2f);
                Line(x0 + 4, y1 - 4, x0 + (x1 - x0) * 0.35f, y1 - 4, InkPalette.Mid, 1.4f);
            }

            /// <summary>A grand piano from above, keyboard at the bottom, with its stool.</summary>
            void Piano(float x, float y)
            {
                Rect(x, y, x + 76, y + 50, InkPalette.Board, 2.2f, 18f);
                Rect(x + 4, y, x + 72, y + 11, Color.white, 1.4f, 1f);
                for (float k = x + 10; k < x + 70; k += 7) Line(k, y + 2, k, y + 9, Ink, 1f);
                Line(x + 12, y + 20, x + 60, y + 40, InkPalette.WithAlpha(Color.white, 0.55f), 1.6f);
                Rect(x + 24, y - 18, x + 52, y - 8, InkPalette.Marker, 1.6f, 3f);
            }

            /// <summary>Long table with stools drawn as ink-rimmed dots (the design's cafeteria).</summary>
            void TableWithStools(float x0, float y0, float x1, float y1)
            {
                float stool = 9f;
                float top = y1 + stool * 0.9f, bottom = y0 - stool * 0.9f;
                _c.Line(X(x0 + 10), Y(top), X(x1 - 10), Y(top), Ink, 12.5f, 0.01f, 24f);
                _c.Line(X(x0 + 10), Y(top), X(x1 - 10), Y(top), InkPalette.Sky, 8.5f, 0.01f, 24f);
                _c.Line(X(x0 + 10), Y(bottom), X(x1 - 10), Y(bottom), Ink, 12.5f, 0.01f, 24f);
                _c.Line(X(x0 + 10), Y(bottom), X(x1 - 10), Y(bottom), InkPalette.Sky, 8.5f, 0.01f, 24f);
                Rect(x0, y0, x1, y1, Color.white, 2.2f, 4f);
            }

            // --- walls ------------------------------------------------------------------------

            public void Walls()
            {
                float x0 = X(0), y0 = Y(0), x1 = X(Wpx), y1 = Y(Hpx);
                float door = X(DoorX), gap = 46f, wall = 8f;
                _c.Wobble = 1.2f;
                _c.Line(x0, y1, x1, y1, Ink, wall);
                _c.Line(x0, y0, x0, y1, Ink, wall);
                _c.Line(x1, y0, x1, y1, Ink, wall);
                _c.Line(x0, y0, door - gap / 2, y0, Ink, wall);
                _c.Line(door + gap / 2, y0, x1, y0, Ink, wall);
                _c.Wobble = 0f;
                float hx = door - gap / 2 + 2f;
                _c.Line(hx, y0 + 4, hx, y0 + gap - 6, Ink, 1.7f, 4f, 4f);
                _c.Arc(hx, y0 + 4, gap - 10, 0f, Mathf.PI / 2f, Ink, 1.7f, 4f, 4f);
            }

            public void Fence()
            {
                float x0 = X(4), y0 = Y(4), x1 = X(Wpx - 4), y1 = Y(Hpx - 4);
                float door = X(DoorX), gap = 56f;
                var col = InkPalette.WithAlpha(Ink, 0.85f);
                _c.Wobble = 0.8f;
                _c.Line(x0, y1, x1, y1, col, 2.4f, 8f, 6f);
                _c.Line(x0, y0, x0, y1, col, 2.4f, 8f, 6f);
                _c.Line(x1, y0, x1, y1, col, 2.4f, 8f, 6f);
                _c.Line(x0, y0, door - gap / 2, y0, col, 2.4f, 8f, 6f);
                _c.Line(door + gap / 2, y0, x1, y0, col, 2.4f, 8f, 6f);
                _c.Wobble = 0f;
                _c.Dot(door - gap / 2, y0, 4.5f, InkPalette.Paper, 2f);
                _c.Dot(door + gap / 2, y0, 4.5f, InkPalette.Paper, 2f);
            }

            // --- rooms -----------------------------------------------------------------------

            public void Classroom(int capacity)
            {
                float bw = _level >= 3 ? 190f : 150f, bh = 16f;
                float bx = (Wpx - bw) / 2f, by = Hpx - 9f - bh;
                switch (_level)
                {
                    case 1:
                        Rect(bx, by, bx + bw, by + bh, InkPalette.Board, 2.5f, 3f);
                        _c.Squiggle(X(bx + 14), Y(by + 8), 38, 2.5f, 12, Color.white, 1.5f);
                        Line(bx + 64, by + 8, bx + 84, by + 8, Color.white, 1.5f);
                        Line(bx + 96, by + 6, bx + 102, by + 11, Color.white, 1.5f);
                        break;
                    case 2:
                        Rect(bx, by, bx + bw, by + bh, Color.white, 2.5f, 3f);
                        Line(bx + 12, by + 8, bx + 50, by + 8, InkPalette.Blue, 2f);
                        Line(bx + 60, by + 8, bx + 90, by + 8, InkPalette.Mid, 2f);
                        break;
                    case 3:
                        Rect(bx, by, bx + bw, by + bh, InkPalette.Pale, 2.5f, 3f);
                        Line(bx + 14, by + 8, bx + bw - 14, by + 8, InkPalette.Sky, 3f);
                        Rect(Wpx / 2 - 8, Hpx * 0.52f - 5, Wpx / 2 + 8, Hpx * 0.52f + 5, InkPalette.Ink, 1.5f, 2f);
                        Line(Wpx / 2, Hpx * 0.52f + 6, bx + 20, by - 2, InkPalette.WithAlpha(InkPalette.Mid, 0.6f), 1.2f, 4f, 4f);
                        Line(Wpx / 2, Hpx * 0.52f + 6, bx + bw - 20, by - 2, InkPalette.WithAlpha(InkPalette.Mid, 0.6f), 1.2f, 4f, 4f);
                        break;
                    default:
                        Rect(bx, by, bx + bw, by + bh, InkPalette.Ink, 2.5f, 3f);
                        Line(bx + 12, by + 8, bx + 70, by + 8, InkPalette.Mid, 2.4f);
                        Rect(bx + 90, by + 4, bx + 110, by + 12, InkPalette.Sky, 1f, 1f);
                        Rect(bx + 118, by + 4, bx + 138, by + 12, InkPalette.Marker, 1f, 1f);
                        break;
                }

                // Teacher's desk and chair.
                float ty = by - 44;
                Rect(Wpx / 2 - 42, ty, Wpx / 2 + 42, ty + 22, Color.white, 2.3f, 3f);
                Dot(Wpx / 2, ty + 32, 7f, InkPalette.Mid, 1.8f);

                // Student desks: capacity / 2 double desks in a grid facing the board.
                int pairs = Mathf.Max(1, capacity / 2);
                float top = ty - 14, bottom = 40f, avail = top - bottom;
                int cols = 3, rows = 1;
                for (cols = 3; cols <= 6; cols++)
                {
                    rows = Mathf.CeilToInt(pairs / (float)cols);
                    if (avail / rows >= 36f) break;
                }
                cols = Mathf.Min(cols, 6);
                rows = Mathf.CeilToInt(pairs / (float)cols);
                float colW = (Wpx - 36f) / cols, pitch = avail / rows;
                float dw = colW - 14f, dh = Mathf.Min(20f, pitch * 0.4f), cr = Mathf.Min(7f, pitch * 0.15f);
                for (int i = 0; i < pairs; i++)
                {
                    int col = i % cols, row = i / cols;
                    float cx = 18f + colW * (col + 0.5f);
                    float dTop = top - row * pitch;
                    Rect(cx - dw / 2, dTop - dh, cx + dw / 2, dTop, Color.white, 2f, 3f);
                    Chair(cx - dw / 4, dTop - dh - cr - 3, cr);
                    Chair(cx + dw / 4, dTop - dh - cr - 3, cr);
                    if (_level >= 4)
                    {
                        Monitor(cx - dw / 4, dTop - dh / 2, Mathf.Min(14f, dw / 3), dh * 0.55f);
                        Monitor(cx + dw / 4, dTop - dh / 2, Mathf.Min(14f, dw / 3), dh * 0.55f);
                    }
                }
                if (_level >= 5)
                {
                    Plant(16, Hpx - 30);
                    Plant(Wpx - 16, Hpx - 30);
                }
            }

            public void Dorm()
            {
                switch (_level)
                {
                    case 1:
                    {
                        float bw = 54, bh = 100, gap = (Wpx - 3 * bw) / 4f;
                        for (int i = 0; i < 3; i++) Bed(gap + i * (bw + gap), Hpx - 12 - bh, bw, bh, InkPalette.Sky, bunk: true);
                        Rect(12, 12, 72, 32, InkPalette.Sky, 2f, 2f);
                        Line(32, 12, 32, 32, Ink, 1.4f);
                        Line(52, 12, 52, 32, Ink, 1.4f);
                        break;
                    }
                    case 2:
                    case 3:
                    {
                        float bw = 46, bh = 94, gap = (Wpx - 4 * bw) / 5f;
                        for (int i = 0; i < 4; i++) Bed(gap + i * (bw + gap), Hpx - 12 - bh, bw, bh, i % 2 == 0 ? InkPalette.Sky : InkPalette.Mid);
                        Rect(12, 14, 70, 36, Color.white, 2f, 3f);
                        Chair(41, 46, 6f);
                        Rect(Wpx - 70, 14, Wpx - 12, 36, Color.white, 2f, 3f);
                        Chair(Wpx - 41, 46, 6f);
                        if (_level == 3)
                        {
                            Rect(-2, Hpx * 0.35f, 10, Hpx * 0.35f + 40, InkPalette.Pale, 1.8f, 2f);
                            for (float y = Hpx * 0.35f + 7; y < Hpx * 0.35f + 36; y += 7) Line(1, y, 8, y, Ink, 1f);
                        }
                        break;
                    }
                    default:
                    {
                        // Two beds on the left, a private bathroom in the top-right corner.
                        float bw = 58, bh = 108;
                        bool luxury = _level >= 5;
                        Bed(14, Hpx - 12 - bh, bw, bh, luxury ? InkPalette.Marker : InkPalette.Sky);
                        Bed(84, Hpx - 12 - bh, bw, bh, luxury ? InkPalette.Mid : InkPalette.Sky);
                        float px = Wpx - 94, py = Hpx - 96;
                        Line(px, Hpx, px, py, Ink, 4f);
                        Line(px, py, px + 28, py, Ink, 4f);
                        Line(px + 58, py, Wpx, py, Ink, 4f);
                        Toilet(Wpx - 26, Hpx - 34);
                        Rect(px + 10, Hpx - 48, px + 44, Hpx - 14, InkPalette.Wet, 1.8f, 2f);
                        _c.StrokeCircle(X(px + 27), Y(Hpx - 31), 5f, InkPalette.Mid, 1.4f);
                        Rect(Wpx - 58, 16, Wpx - 14, 26, InkPalette.Board, 1.8f, 2f);   // TV
                        if (luxury)
                        {
                            Rect(14, 14, 90, 38, Color.white, 2f, 3f);
                            Monitor(34, 28, 20, 12);
                            Monitor(66, 28, 20, 12);
                            Chair(52, 50, 7f);
                            Rect(Wpx - 58, 40, Wpx - 34, 62, Color.white, 2f, 3f);   // mini fridge
                            Rug(DoorX - 30, 64, 34, 12);
                            Plant(Wpx - 104, 30, 0.9f);
                        }
                        else
                        {
                            Rect(14, 14, 70, 36, Color.white, 2f, 3f);
                            Chair(42, 46, 6f);
                        }
                        break;
                    }
                }
            }

            public void Cafeteria()
            {
                // Kitchen along the back wall.
                float ky0 = Hpx - 58, ky1 = Hpx - 26;
                Rect(20, ky0, Wpx - 84, ky1, Color.white, 2.2f, 3f);
                int burners = 1 + _level;
                for (int i = 0; i < burners; i++)
                {
                    float sx = 40 + i * 34;
                    _c.StrokeCircle(X(sx), Y((ky0 + ky1) / 2), 7f, Ink, 1.6f);
                }
                Rect(Wpx - 70, Hpx - 64, Wpx - 22, Hpx - 10, InkPalette.Pale, 2.2f, 3f);
                Line(Wpx - 46, Hpx - 60, Wpx - 46, Hpx - 14, Ink, 1.4f);
                if (_level >= 3)
                {
                    float sx = 40 + burners * 34 + 16;
                    Rect(sx, ky0 + 4, sx + 70, ky1 - 4, InkPalette.Pale, 1.8f, 2f);
                    for (int i = 0; i < 4; i++) Dot(sx + 12 + i * 15, (ky0 + ky1) / 2, 4f, i % 2 == 0 ? InkPalette.Sky : InkPalette.Marker, 1.2f);
                }
                if (_level >= 4)
                    for (int i = 0; i < 3; i++) Dot(Wpx - 110 - i * 18, (ky0 + ky1) / 2, 6f, InkPalette.Marker, 1.6f);

                // Serving line with trays.
                float sy = Hpx - 94;
                Rect(40, sy, Wpx * 0.55f, sy + 14, InkPalette.Pale, 2f, 2f);
                for (float x = 52; x < Wpx * 0.55f - 16; x += 26) Rect(x, sy + 3, x + 16, sy + 11, Color.white, 1.2f, 1f);

                // Tables either side of an aisle that runs up from the door.
                int tables = new[] { 2, 4, 6, 8, 10 }[Mathf.Clamp(_level - 1, 0, 4)];
                int rows = Mathf.CeilToInt(tables / 2f);
                float top = sy - 30, bottom = 44f, pitch = (top - bottom) / rows;
                float lx0 = 22, lx1 = DoorX - 34, rx0 = DoorX + 34, rx1 = Wpx - 22;
                for (int i = 0; i < tables; i++)
                {
                    int row = i / 2;
                    float cy = top - pitch * (row + 0.5f);
                    float th = Mathf.Min(16f, pitch * 0.3f);
                    if (i % 2 == 0) TableWithStools(lx0, cy - th / 2, lx1, cy + th / 2);
                    else TableWithStools(rx0, cy - th / 2, rx1, cy + th / 2);
                }
            }

            public void Bathroom()
            {
                int stalls = 3;
                float sw = (Wpx - 16) / stalls, front = Hpx - 72;
                for (int i = 1; i < stalls; i++) Line(8 + i * sw, Hpx - 4, 8 + i * sw, front, Ink, 2.5f);
                Line(8, front, Wpx - 8, front, Ink, 1.5f, 4f, 4f);
                for (int i = 0; i < stalls; i++) Toilet(8 + sw * (i + 0.5f), Hpx - 34);
                int sinks = _level >= 2 ? 3 : 2;
                for (int i = 0; i < sinks; i++) Sink(22, 30 + i * 32);
                if (_level >= 2)
                {
                    Rect(Wpx - 52, 18, Wpx - 12, 58, InkPalette.Wet, 2f, 2f);
                    _c.StrokeCircle(X(Wpx - 32), Y(38), 6f, InkPalette.Mid, 1.5f);
                    Line(Wpx - 52, 58, Wpx - 12, 58, Ink, 1.2f, 3f, 3f);
                }
                if (_level >= 3) Plant(Wpx - 30, 86, 0.8f);
            }

            public void Laundry()
            {
                float s = 42;
                int washers = _level >= 3 ? 4 : 3;
                float gap = (Wpx - washers * s) / (washers + 1);
                for (int i = 0; i < washers; i++) Washer(gap + i * (s + gap), Hpx - 12 - s, s);
                if (_level >= 2)
                    for (int i = 0; i < 2; i++) Washer(10, 24 + i * (s + 8), s);
                if (_level >= 3)
                {
                    Rect(Wpx - 70, 20, Wpx - 14, 60, Color.white, 2f, 3f);
                    Rect(Wpx - 60, 64, Wpx - 30, 84, InkPalette.Marker, 1.8f, 3f);
                }
                else Rect(Wpx - 62, 22, Wpx - 18, 40, InkPalette.Marker, 1.8f, 3f);
            }

            public void Library()
            {
                // Shelves along the back wall; more along the sides at higher levels.
                int topShelves = _level >= 2 ? 4 : 3;
                float sw = (Wpx - 40) / topShelves;
                for (int i = 0; i < topShelves; i++) Shelf(20 + i * sw + 4, Hpx - 32, 20 + (i + 1) * sw - 4, Hpx - 12);
                if (_level >= 2)
                {
                    Shelf(10, Hpx * 0.35f, 30, Hpx - 50);
                    Shelf(Wpx - 30, Hpx * 0.35f, Wpx - 10, Hpx - 50);
                }
                int tables = Mathf.Min(4, _level + 1);
                for (int i = 0; i < tables; i++)
                {
                    float cx = Wpx * (i % 2 == 0 ? 0.3f : 0.7f), cy = Hpx * (i < 2 ? 0.62f : 0.36f);
                    Rect(cx - 38, cy - 16, cx + 38, cy + 16, Color.white, 2.2f, 4f);
                    Chair(cx - 20, cy + 26);
                    Chair(cx + 20, cy + 26);
                    Chair(cx - 20, cy - 26);
                    Chair(cx + 20, cy - 26);
                    if (_level >= 3) Monitor(cx, cy, 20, 12);
                }
                if (_level >= 4)
                {
                    Rect(40, 24, 100, 60, InkPalette.Sky, 2f, 12f);
                    Rect(Wpx - 100, 24, Wpx - 40, 60, InkPalette.Sky, 2f, 12f);
                    Plant(Wpx / 2 + 60, 40, 0.9f);
                }
            }

            public void ScienceLab()
            {
                int rows = _level >= 2 ? 3 : 2;
                float top = Hpx - (_level >= 2 ? 70 : 40), bottom = 60f, pitch = (top - bottom) / Mathf.Max(1, rows - 1);
                for (int r = 0; r < rows; r++)
                {
                    float cy = top - r * pitch;
                    float x0 = 30, x1 = Wpx - 30;
                    Rect(x0, cy - 12, x1, cy + 12, Color.white, 2.2f, 3f);
                    Sink(x0 + 22, cy);
                    for (int i = 0; i < 4; i++) Dot(x0 + 70 + i * 40, cy + 2, 5f, i % 2 == 0 ? InkPalette.Sky : InkPalette.Marker, 1.5f);
                    if (_level >= 3) { Dot(x1 - 30, cy, 6f, InkPalette.Pale, 1.8f); Line(x1 - 30, cy + 6, x1 - 24, cy + 12, Ink, 1.6f); }
                    for (float x = x0 + 40; x < x1 - 20; x += 44) Dot(x, cy - 22, 6f, InkPalette.Sky, 1.6f);
                }
                if (_level >= 2)
                {
                    Rect(Wpx - 88, Hpx - 48, Wpx - 14, Hpx - 12, InkPalette.Pale, 2.2f, 3f);
                    for (float x = Wpx - 82; x < Wpx - 18; x += 8) Line(x, Hpx - 44, x + 6, Hpx - 16, InkPalette.WithAlpha(InkPalette.Mid, 0.7f), 1.2f);
                }
                if (_level >= 4) Plant(20, Hpx - 26, 0.9f);
            }

            public void ComputerLab()
            {
                int rows = new[] { 2, 3, 3, 4 }[Mathf.Clamp(_level - 1, 0, 3)];
                float top = Hpx - 40, bottom = 56f, pitch = (top - bottom) / Mathf.Max(1, rows - 1);
                for (int r = 0; r < rows; r++)
                {
                    float cy = top - r * pitch;
                    float x0 = 24, x1 = Wpx - 24;
                    if (_level >= 3 && r == 0) x1 = Wpx - 80;
                    Rect(x0, cy - 10, x1, cy + 10, Color.white, 2.2f, 3f);
                    for (float x = x0 + 20; x < x1 - 10; x += 36)
                    {
                        Monitor(x, cy + 1, 22, 12);
                        Chair(x, cy - 20, 6f);
                    }
                }
                if (_level >= 3)
                {
                    Rect(Wpx - 66, Hpx - 60, Wpx - 18, Hpx - 14, InkPalette.Pale, 2.2f, 3f);
                    Line(Wpx - 50, Hpx - 30, Wpx - 34, Hpx - 30, Ink, 2f);
                    Line(Wpx - 42, Hpx - 38, Wpx - 42, Hpx - 22, Ink, 2f);
                }
                if (_level >= 4)
                {
                    Dot(40, 30, 10f, InkPalette.Marker, 2f);
                    Line(40, 30, 62, 50, Ink, 5f);
                    Line(62, 50, 84, 40, Ink, 4f);
                    Dot(62, 50, 4f, InkPalette.Pale, 1.5f);
                }
            }

            public void Office(int desks)
            {
                desks = Mathf.Min(desks, 6);
                Rect(10, Hpx - 40, 40, Hpx - 10, InkPalette.Pale, 2f, 2f);   // filing cabinet
                Line(10, Hpx - 25, 40, Hpx - 25, Ink, 1.2f);
                Plant(Wpx - 22, Hpx - 26, 0.85f);
                if (desks <= 2)
                {
                    Rect(24, Hpx - 96, 110, Hpx - 64, Color.white, 2.2f, 3f);
                    Monitor(50, Hpx - 80, 22, 13);
                    Chair(67, Hpx - 108, 7.5f);
                    Rect(Wpx - 86, 44, Wpx - 16, 70, Color.white, 2.2f, 3f);
                    Monitor(Wpx - 51, 57, 20, 12);
                    Chair(Wpx - 51, 82, 7f);
                    return;
                }
                int cols = desks <= 4 ? 2 : 3;
                int rows = Mathf.CeilToInt(desks / (float)cols);
                float cw = (Wpx - 30) / cols, top = Hpx - 56, pitch = (top - 44) / rows;
                for (int i = 0; i < desks; i++)
                {
                    float cx = 15 + cw * (i % cols + 0.5f), cy = top - pitch * (i / cols) - pitch * 0.35f;
                    Rect(cx - cw * 0.38f, cy - 10, cx + cw * 0.38f, cy + 10, Color.white, 2f, 3f);
                    Monitor(cx, cy, 16, 10);
                    Chair(cx, cy - 18, 6f);
                }
            }

            public void StaffLounge()
            {
                Sofa(Wpx / 2 - 74, Hpx - 52, Wpx / 2 + 74, Hpx - 12, InkPalette.Sky);
                _c.FillEllipse(X(Wpx / 2), Y(Hpx - 84), 36f, 16f, Color.white);
                _c.StrokeEllipse(X(Wpx / 2), Y(Hpx - 84), 36f, 16f, Ink, 2f);
                Rect(10, 30, 40, 110, Color.white, 2f, 2f);                       // counter
                Rect(14, 88, 36, 106, InkPalette.Ink, 1.5f, 2f);                   // coffee machine
                Dot(25, 70, 5f, InkPalette.Marker, 1.4f);
                if (_level >= 2)
                {
                    Rect(Wpx - 40, 30, Wpx - 10, 150, Color.white, 2f, 2f);
                    Sink(Wpx - 25, 110);
                    Rect(Wpx - 44, 156, Wpx - 10, 206, InkPalette.Pale, 2f, 3f);
                }
                if (_level >= 3)
                {
                    Rect(64, 30, 104, 70, InkPalette.Mid, 2f, 10f);
                    Rect(Wpx - 100, 30, Wpx - 60, 70, InkPalette.Mid, 2f, 10f);
                    Plant(Wpx / 2, 34, 0.9f);
                }
            }

            public void StudentLounge()
            {
                Rect(Wpx / 2 - 46, Hpx - 20, Wpx / 2 + 46, Hpx - 10, InkPalette.Board, 2f, 2f);   // TV
                Rug(Wpx / 2 - 10, Hpx * 0.5f, 70, 30);
                Sofa(Wpx / 2 - 70, Hpx * 0.28f, Wpx / 2 + 50, Hpx * 0.28f + 34, InkPalette.Sky);
                Sofa(26, Hpx * 0.36f, 60, Hpx - 44, InkPalette.Sky);
                _c.FillEllipse(X(Wpx / 2 - 10), Y(Hpx * 0.56f), 26f, 12f, Color.white);
                _c.StrokeEllipse(X(Wpx / 2 - 10), Y(Hpx * 0.56f), 26f, 12f, Ink, 2f);
                if (_level >= 2)
                {
                    Rect(Wpx - 110, Hpx * 0.42f, Wpx - 24, Hpx * 0.42f + 50, InkPalette.Mid, 2.2f, 3f);
                    Line(Wpx - 67, Hpx * 0.42f, Wpx - 67, Hpx * 0.42f + 50, Color.white, 2f);
                }
                else
                {
                    Dot(Wpx - 60, Hpx * 0.6f, 14f, InkPalette.Marker);
                    Dot(Wpx - 36, Hpx * 0.45f, 13f, InkPalette.Sky);
                }
                if (_level >= 3)
                {
                    Rect(Wpx - 42, 26, Wpx - 12, Hpx * 0.38f, Color.white, 2f, 2f);
                    for (float y = 40; y < Hpx * 0.36f; y += 24) Dot(Wpx - 56, y, 6f, InkPalette.Sky, 1.6f);
                    Plant(24, 30, 0.9f);
                }
            }

            public void GamingRoom()
            {
                int tvs = _level >= 3 ? 1 : 2;
                if (_level >= 3)
                {
                    Rect(30, Hpx - 24, Wpx - 30, Hpx - 10, InkPalette.Board, 2.2f, 2f);
                    for (int r = 0; r < 2; r++) Sofa(50, Hpx - 90 - r * 50, Wpx - 50, Hpx - 60 - r * 50, InkPalette.Mid);
                }
                else
                {
                    for (int i = 0; i < tvs; i++)
                    {
                        float cx = Wpx * (i == 0 ? 0.3f : 0.7f);
                        Rect(cx - 30, Hpx - 20, cx + 30, Hpx - 10, InkPalette.Board, 2f, 2f);
                        Rect(cx - 10, Hpx - 34, cx + 10, Hpx - 26, InkPalette.Pale, 1.4f, 1f);
                        Dot(cx - 16, Hpx - 66, 13f, i == 0 ? InkPalette.Marker : InkPalette.Sky);
                        Dot(cx + 16, Hpx - 70, 13f, InkPalette.Mid);
                    }
                }
                if (_level >= 2)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 30 + i * 40;
                        Rect(10, y, 50, y + 24, Color.white, 2f, 3f);
                        Monitor(24, y + 12, 16, 10);
                        Chair(62, y + 12, 6f);
                    }
                }
            }

            public void NurseOffice()
            {
                int beds = Mathf.Clamp(_level, 1, 3);
                float bw = 44, bh = 74, gap = (Wpx - beds * bw) / (beds + 1);
                for (int i = 0; i < beds; i++)
                {
                    float x = gap + i * (bw + gap);
                    Bed(x, Hpx - 12 - bh, bw, bh, InkPalette.Pale);
                    if (i > 0) Line(x - gap / 2, Hpx - 8, x - gap / 2, Hpx - 90, InkPalette.Mid, 1.6f, 5f, 4f);
                }
                Rect(10, 24, 44, 64, InkPalette.Sky, 2f, 2f);
                Line(27, 34, 27, 54, Ink, 3f);
                Line(17, 44, 37, 44, Ink, 3f);
                Rect(Wpx - 76, 24, Wpx - 16, 46, Color.white, 2f, 3f);
                Chair(Wpx - 46, 58, 6.5f);
            }

            public void SecurityRoom()
            {
                int cols = _level + 1, rowsN = _level >= 2 ? 2 : 1;
                float sw = (Wpx - 40) / cols;
                for (int r = 0; r < rowsN; r++)
                    for (int i = 0; i < cols; i++)
                        Screen(20 + i * sw + 3, Hpx - 30 - r * 22, 20 + (i + 1) * sw - 3, Hpx - 12 - r * 22);
                float dy = Hpx - 40 - rowsN * 22;
                Rect(24, dy - 22, Wpx - 24, dy, Color.white, 2.2f, 3f);
                Chair(Wpx / 2, dy - 34, 7f);
                Rect(10, 20, 30, 70, InkPalette.Sky, 2f, 2f);
                Line(10, 45, 30, 45, Ink, 1.2f);
            }

            public void ClubRoom()
            {
                Rect(Wpx / 2 - 60, Hpx - 24, Wpx / 2 + 60, Hpx - 10, Color.white, 2.2f, 2f);
                Line(Wpx / 2 - 46, Hpx - 17, Wpx / 2 + 10, Hpx - 17, InkPalette.Mid, 2f);
                Shelf(Wpx - 30, 40, Wpx - 12, Hpx - 44);
                int tables = Mathf.Clamp(_level, 1, 3);
                for (int i = 0; i < tables; i++)
                {
                    float cx = tables == 1 ? Wpx / 2 - 10 : Wpx * (i % 2 == 0 ? 0.3f : 0.64f);
                    float cy = tables == 1 ? Hpx * 0.5f : Hpx * (i < 2 ? 0.6f : 0.3f);
                    if (tables == 3 && i == 2) cx = Wpx * 0.3f;
                    Dot(cx, cy, 24f, Color.white, 2.2f);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k / 5f * Mathf.PI * 2f + 0.3f;
                        Chair(cx + Mathf.Cos(a) * 34, cy + Mathf.Sin(a) * 34, 6.5f);
                    }
                }
            }

            public void Gym()
            {
                float inset = _level >= 3 ? 70 : 30;
                float x0 = 30, x1 = Wpx - 30, y0 = 30, y1 = Hpx - inset;
                var line = InkPalette.WithAlpha(InkPalette.Mid, 0.9f);
                _c.StrokeRect(X(x0), Y(y0), X(x1), Y(y1), line, 3f);
                Line((x0 + x1) / 2, y0, (x0 + x1) / 2, y1, line, 3f);
                _c.StrokeCircle(X((x0 + x1) / 2), Y((y0 + y1) / 2), 36f, line, 3f);
                float cy = (y0 + y1) / 2;
                _c.StrokeRect(X(x0), Y(cy - 40), X(x0 + 70), Y(cy + 40), line, 3f);
                _c.StrokeRect(X(x1 - 70), Y(cy - 40), X(x1), Y(cy + 40), line, 3f);
                Line(x0 + 8, cy - 18, x0 + 8, cy + 18, Ink, 3.5f);
                _c.StrokeCircle(X(x0 + 18), Y(cy), 8f, InkPalette.CoralText, 2.4f);
                Line(x1 - 8, cy - 18, x1 - 8, cy + 18, Ink, 3.5f);
                _c.StrokeCircle(X(x1 - 18), Y(cy), 8f, InkPalette.CoralText, 2.4f);
                if (_level >= 2)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 44 + i * 26;
                        Line(x0 + 90, y, x0 + 130, y, Ink, 3f);
                        Dot(x0 + 90, y, 6f, InkPalette.Board, 1.2f);
                        Dot(x0 + 130, y, 6f, InkPalette.Board, 1.2f);
                    }
                }
                if (_level >= 3)
                    for (int r = 0; r < 3; r++)
                        Rect(24, Hpx - 20 - r * 16, Wpx - 24, Hpx - 8 - r * 16, r % 2 == 0 ? InkPalette.Pale : InkPalette.Sky, 1.6f, 2f);
            }

            public void BasketballCourt()
            {
                float x0 = 26, x1 = Wpx - 26, y0 = 30, y1 = Hpx - (_level >= 2 ? 64 : 30);
                var white = Color.white;
                _c.StrokeRect(X(x0), Y(y0), X(x1), Y(y1), white, 4f);
                float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
                Line(cx, y0, cx, y1, white, 3.5f);
                _c.StrokeCircle(X(cx), Y(cy), 34f, white, 3.5f);
                _c.FillRect(X(x0), Y(cy - 36), X(x0 + 66), Y(cy + 36), InkPalette.WithAlpha(InkPalette.Mid, 0.35f));
                _c.FillRect(X(x1 - 66), Y(cy - 36), X(x1), Y(cy + 36), InkPalette.WithAlpha(InkPalette.Mid, 0.35f));
                _c.StrokeRect(X(x0), Y(cy - 36), X(x0 + 66), Y(cy + 36), white, 3f);
                _c.StrokeRect(X(x1 - 66), Y(cy - 36), X(x1), Y(cy + 36), white, 3f);
                _c.Arc(X(x0), Y(cy), 110f, -Mathf.PI / 2.4f, Mathf.PI / 2.4f, white, 3f);
                _c.Arc(X(x1), Y(cy), 110f, Mathf.PI - Mathf.PI / 2.4f, Mathf.PI + Mathf.PI / 2.4f, white, 3f);
                Line(x0 + 6, cy - 16, x0 + 6, cy + 16, Ink, 4f);
                _c.StrokeCircle(X(x0 + 16), Y(cy), 8f, InkPalette.CoralText, 2.6f);
                Line(x1 - 6, cy - 16, x1 - 6, cy + 16, Ink, 4f);
                _c.StrokeCircle(X(x1 - 16), Y(cy), 8f, InkPalette.CoralText, 2.6f);
                if (_level >= 2) Stands(Hpx - 56, Hpx - 12, _level >= 3 ? 3 : 2);
            }

            void Stands(float y0, float y1, int rowsN)
            {
                float step = (y1 - y0) / rowsN;
                for (int r = 0; r < rowsN; r++)
                    Rect(24, y0 + r * step, Wpx - 24, y0 + (r + 1) * step - 2, r % 2 == 0 ? InkPalette.Pale : InkPalette.Sky, 1.6f, 2f);
            }

            public void SoccerField()
            {
                float x0 = 44, x1 = Wpx - 44, y0 = 40, y1 = Hpx - (_level >= 2 ? 80 : 40);
                var white = Color.white;
                float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
                _c.StrokeRect(X(x0), Y(y0), X(x1), Y(y1), white, 4.5f);
                Line(cx, y0, cx, y1, white, 4f);
                _c.StrokeCircle(X(cx), Y(cy), 56f, white, 4f);
                _c.FillCircle(X(cx), Y(cy), 5f, white);
                float pbH = (y1 - y0) * 0.56f, gbH = (y1 - y0) * 0.28f;
                _c.StrokeRect(X(x0), Y(cy - pbH / 2), X(x0 + 90), Y(cy + pbH / 2), white, 4f);
                _c.StrokeRect(X(x1 - 90), Y(cy - pbH / 2), X(x1), Y(cy + pbH / 2), white, 4f);
                _c.StrokeRect(X(x0), Y(cy - gbH / 2), X(x0 + 36), Y(cy + gbH / 2), white, 3.5f);
                _c.StrokeRect(X(x1 - 36), Y(cy - gbH / 2), X(x1), Y(cy + gbH / 2), white, 3.5f);
                Rect(x0 - 22, cy - 34, x0, cy + 34, InkPalette.Paper, 2.4f, 2f);
                Rect(x1, cy - 34, x1 + 22, cy + 34, InkPalette.Paper, 2.4f, 2f);
                for (float y = cy - 28; y < cy + 30; y += 9) { Line(x0 - 20, y, x0 - 2, y, InkPalette.Sky, 1.2f); Line(x1 + 2, y, x1 + 20, y, InkPalette.Sky, 1.2f); }
                foreach (var (fx, fy) in new[] { (x0, y0), (x1, y0), (x0, y1), (x1, y1) })
                {
                    Line(fx, fy, fx, fy + 22, Ink, 2f);
                    _c.FillTriangle(X(fx), Y(fy + 22), X(fx), Y(fy + 12), X(fx + 12), Y(fy + 17), InkPalette.Marker);
                }
                if (_level >= 2)
                {
                    Stands(Hpx - 70, Hpx - 14, _level >= 3 ? 4 : 3);
                    foreach (float fx in new[] { 22f, Wpx - 22 })
                    {
                        Dot(fx, 22, 9f, InkPalette.Marker, 2f);
                        Dot(fx, Hpx - 90, 9f, InkPalette.Marker, 2f);
                    }
                }
            }

            public void Pool()
            {
                float x0 = 40, x1 = Wpx - 40, y0 = 60, y1 = Hpx - 40;
                _c.FillRect(X(x0), Y(y0), X(x1), Y(y1), InkPalette.Sky, 6f);
                int lanes = 2 + _level * 2;
                float lh = (y1 - y0) / lanes;
                for (int i = 1; i < lanes; i++)
                    Line(x0 + 6, y0 + i * lh, x1 - 6, y0 + i * lh, i % 2 == 0 ? InkPalette.Marker : Color.white, 3.5f, 8f, 5f);
                for (int i = 0; i < lanes; i++)
                    _c.Squiggle(X(x0 + 30 + (i % 3) * 40), Y(y0 + (i + 0.5f) * lh), 30, 2f, 10, InkPalette.WithAlpha(Color.white, 0.9f), 1.8f);
                _c.StrokeRect(X(x0), Y(y0), X(x1), Y(y1), InkPalette.Ink, 3f, 6f);
                for (int i = 0; i < lanes; i++) Rect(x0 - 16, y0 + (i + 0.5f) * lh - 7, x0 - 2, y0 + (i + 0.5f) * lh + 7, Color.white, 1.6f, 2f);
                Line(x1 - 20, y1 - 2, x1 - 20, y1 + 18, Ink, 2f);
                Line(x1 - 12, y1 - 2, x1 - 12, y1 + 18, Ink, 2f);
                if (_level == 1)
                {
                    for (int i = 0; i < 3; i++) Rect(60 + i * 60, 16, 104 + i * 60, 40, Color.white, 1.8f, 3f);
                    Dot(Wpx - 80, 30, 16f, InkPalette.Marker, 2f);
                }
            }

            public void Track()
            {
                float x0 = 18, x1 = Wpx - 18, y0 = 28, y1 = Hpx - (_level >= 2 ? 64 : 22);
                float h = y1 - y0, r = h / 2f;
                int lanes = 2 + _level * 2;
                float lw = Mathf.Min(13f, (r - 30f) / lanes);
                _c.FillRect(X(x0), Y(y0), X(x1), Y(y1), InkPalette.Hex(0xBFD9F5), r);
                _c.StrokeRect(X(x0), Y(y0), X(x1), Y(y1), InkPalette.Ink, 3f, r);
                for (int i = 1; i < lanes; i++)
                    _c.StrokeRect(X(x0 + i * lw), Y(y0 + i * lw), X(x1 - i * lw), Y(y1 - i * lw), InkPalette.WithAlpha(Color.white, 0.9f), 1.6f, r - i * lw);
                float ix0 = x0 + lanes * lw, iy0 = y0 + lanes * lw, ix1 = x1 - lanes * lw, iy1 = y1 - lanes * lw;
                _c.FillRect(X(ix0), Y(iy0), X(ix1), Y(iy1), InkPalette.Lawn, r - lanes * lw);
                _c.StrokeRect(X(ix0), Y(iy0), X(ix1), Y(iy1), InkPalette.Ink, 2.5f, r - lanes * lw);
                Line((x0 + x1) / 2 + 40, y0 + 2, (x0 + x1) / 2 + 40, iy0 - 2, Color.white, 3f);
                if (_level >= 2) Stands(Hpx - 58, Hpx - 10, _level >= 3 ? 3 : 2);
            }

            public void Tennis()
            {
                float gap = 16, cw = (Wpx - 3 * gap) / 2, y0 = 26, y1 = Hpx - 26;
                var surface = _level >= 3 ? InkPalette.Mid : InkPalette.Hex(0xBFDCF6);
                for (int i = 0; i < 2; i++)
                {
                    float x0 = gap + i * (cw + gap), x1 = x0 + cw;
                    _c.FillRect(X(x0), Y(y0), X(x1), Y(y1), surface, 3f);
                    _c.StrokeRect(X(x0 + 6), Y(y0 + 6), X(x1 - 6), Y(y1 - 6), Color.white, 3f);
                    Line(x0 + 22, y0 + 6, x0 + 22, y1 - 6, Color.white, 2.2f);
                    Line(x1 - 22, y0 + 6, x1 - 22, y1 - 6, Color.white, 2.2f);
                    float my = (y0 + y1) / 2;
                    Line(x0 + 22, my - 50, x1 - 22, my - 50, Color.white, 2.2f);
                    Line(x0 + 22, my + 50, x1 - 22, my + 50, Color.white, 2.2f);
                    Line((x0 + x1) / 2, my - 50, (x0 + x1) / 2, my + 50, Color.white, 2.2f);
                    Line(x0 - 4, my, x1 + 4, my, Ink, 3f, 4f, 3f);
                    if (_level >= 3) Rect(x1 - 4, my + 8, x1 + 8, my + 22, Color.white, 1.6f, 2f);
                }
                if (_level >= 2)
                    for (int i = 0; i < 3; i++) Rect(Wpx / 2 - 6, 40 + i * 50, Wpx / 2 + 6, 70 + i * 50, Color.white, 1.6f, 2f);
            }

            // --- big sports and outdoor areas ------------------------------------------------

            /// <summary>Hall with a court and a running lane, a pool wing and changing rooms.</summary>
            public void SportsComplex()
            {
                var line = InkPalette.WithAlpha(InkPalette.Mid, 0.9f);
                var white = Color.white;
                float split = Wpx * 0.58f;

                // Hall: running lane around a court
                float hx0 = 22, hx1 = split - 18, hy0 = 22, hy1 = Hpx - (_level >= 3 ? 86 : 26);
                _c.FillRect(X(hx0), Y(hy0), X(hx1), Y(hy1), InkPalette.Hex(0xBFD9F5), 26f);
                _c.StrokeRect(X(hx0), Y(hy0), X(hx1), Y(hy1), Ink, 2.6f, 26f);
                float lane = 22f;
                _c.StrokeRect(X(hx0 + lane / 2), Y(hy0 + lane / 2), X(hx1 - lane / 2), Y(hy1 - lane / 2), InkPalette.WithAlpha(white, 0.9f), 1.6f, 20f);
                float cx0 = hx0 + lane + 6, cx1 = hx1 - lane - 6, cy0 = hy0 + lane + 6, cy1 = hy1 - lane - 6;
                _c.FillRect(X(cx0), Y(cy0), X(cx1), Y(cy1), InkPalette.FloorBase, 3f);
                _c.StrokeRect(X(cx0), Y(cy0), X(cx1), Y(cy1), line, 3f);
                float ccx = (cx0 + cx1) / 2, ccy = (cy0 + cy1) / 2;
                Line(ccx, cy0, ccx, cy1, line, 3f);
                _c.StrokeCircle(X(ccx), Y(ccy), 30f, line, 3f);
                _c.StrokeRect(X(cx0), Y(ccy - 34), X(cx0 + 56), Y(ccy + 34), line, 2.6f);
                _c.StrokeRect(X(cx1 - 56), Y(ccy - 34), X(cx1), Y(ccy + 34), line, 2.6f);
                Line(cx0 + 6, ccy - 14, cx0 + 6, ccy + 14, Ink, 3.5f);
                _c.StrokeCircle(X(cx0 + 15), Y(ccy), 7f, InkPalette.CoralText, 2.2f);
                Line(cx1 - 6, ccy - 14, cx1 - 6, ccy + 14, Ink, 3.5f);
                _c.StrokeCircle(X(cx1 - 15), Y(ccy), 7f, InkPalette.CoralText, 2.2f);

                // Wall between hall and wing
                Line(split, 0, split, Hpx, Ink, 5f);

                // Pool wing
                float px0 = split + 22, px1 = Wpx - 22, py0 = Hpx * 0.42f, py1 = Hpx - 22;
                _c.FillRect(X(px0), Y(py0), X(px1), Y(py1), InkPalette.Sky, 6f);
                int lanes = 4;
                float lh = (py1 - py0) / lanes;
                for (int i = 1; i < lanes; i++)
                    Line(px0 + 6, py0 + i * lh, px1 - 6, py0 + i * lh, i % 2 == 0 ? InkPalette.Marker : white, 3f, 8f, 5f);
                for (int i = 0; i < lanes; i++)
                    _c.Squiggle(X(px0 + 24 + (i % 2) * 40), Y(py0 + (i + 0.5f) * lh), 26, 1.8f, 9, InkPalette.WithAlpha(white, 0.9f), 1.6f);
                _c.StrokeRect(X(px0), Y(py0), X(px1), Y(py1), Ink, 2.8f, 6f);
                for (int i = 0; i < lanes; i++) Rect(px0 - 14, py0 + (i + 0.5f) * lh - 6, px0 - 2, py0 + (i + 0.5f) * lh + 6, white, 1.5f, 2f);

                // Changing rooms: lockers and benches
                float ly = Hpx * 0.42f - 26;
                Line(split, ly + 14, Wpx, ly + 14, Ink, 3.5f);
                for (float x = split + 20; x < Wpx - 34; x += 26) Rect(x, ly - 30, x + 22, ly + 4, InkPalette.Pale, 1.8f, 2f);
                Rect(split + 30, 34, Wpx - 60, 50, InkPalette.Marker, 1.8f, 4f);

                if (_level >= 2)
                {
                    // Climbing wall with holds along the hall's west wall
                    Rect(6, Hpx * 0.3f, 18, Hpx * 0.75f, InkPalette.Pale, 1.8f, 2f);
                    var holds = new[] { InkPalette.Coral, InkPalette.Marker, InkPalette.Mid, InkPalette.Blue };
                    for (int i = 0; i < 7; i++) Dot(12, Hpx * 0.32f + i * Hpx * 0.06f, 3.2f, holds[i % holds.Length], 1.2f);
                    // Yoga mats in the wing
                    for (int i = 0; i < 3; i++) Rect(split + 34 + i * 34, 64, split + 58 + i * 34, 104, i % 2 == 0 ? InkPalette.Sky : InkPalette.Coral, 1.6f, 4f);
                }
                if (_level >= 3) Stands(Hpx - 76, Hpx - 14, 3);
            }

            /// <summary>Swings, a slide and a sandpit; bigger levels add a climbing frame, a roundabout and a zip line.</summary>
            public void Playground()
            {
                var white = Color.white;
                // Swing set
                float sx0 = 26, sx1 = 26 + 110, sy = Hpx - 46;
                Line(sx0, sy, sx1, sy, Ink, 4f);
                Line(sx0, sy - 30, sx0, sy + 18, Ink, 3f);
                Line(sx1, sy - 30, sx1, sy + 18, Ink, 3f);
                for (int i = 0; i < 2; i++)
                {
                    float x = sx0 + 30 + i * 46;
                    Line(x - 8, sy, x - 8, sy - 34, InkPalette.SoftInk, 1.4f);
                    Line(x + 8, sy, x + 8, sy - 34, InkPalette.SoftInk, 1.4f);
                    Rect(x - 12, sy - 42, x + 12, sy - 32, i == 0 ? InkPalette.Coral : InkPalette.Marker, 1.8f, 3f);
                }
                // Slide
                float lx = Wpx - 70, ly = Hpx - 120;
                Rect(lx, ly, lx + 34, ly + 34, InkPalette.Pale, 2f, 3f);
                for (int i = 1; i < 4; i++) Line(lx + 4, ly + i * 8.5f, lx + 30, ly + i * 8.5f, Ink, 1.3f);
                _c.FillRect(X(lx + 8), Y(ly - 64), X(lx + 26), Y(ly), InkPalette.Mid, 6f);
                _c.StrokeRect(X(lx + 8), Y(ly - 64), X(lx + 26), Y(ly), Ink, 2.2f, 6f);
                Line(lx + 17, ly - 58, lx + 17, ly - 4, white, 2f);
                // Sandpit with a bucket
                Rect(24, 22, 120, 84, InkPalette.Hex(0xFFF0B8), 2.2f, 14f);
                for (int i = 0; i < 9; i++) Dot(36 + (i * 37) % 76, 34 + (i * 19) % 40, 1.2f, InkPalette.WithAlpha(InkPalette.Ink, 0.4f), 0f);
                Rect(84, 44, 100, 60, InkPalette.Coral, 1.6f, 3f);
                if (_level >= 2)
                {
                    // Climbing frame and a roundabout
                    float fx = 150, fy = 34;
                    Rect(fx, fy, fx + 64, fy + 64, Color.clear, 2.4f, 2f);
                    for (int i = 1; i < 4; i++) { Line(fx + i * 16, fy, fx + i * 16, fy + 64, Ink, 1.5f); Line(fx, fy + i * 16, fx + 64, fy + i * 16, Ink, 1.5f); }
                    float rx = Wpx - 56, ry = 56;
                    Dot(rx, ry, 26f, InkPalette.Marker, 2.2f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI / 2f + 0.4f;
                        Line(rx, ry, rx + Mathf.Cos(a) * 24, ry + Mathf.Sin(a) * 24, Ink, 1.8f);
                    }
                    Dot(rx, ry, 5f, InkPalette.Coral, 1.6f);
                }
                if (_level >= 3)
                {
                    // Zip line with a little playhouse
                    Line(sx1 + 26, Hpx - 30, Wpx - 110, 116, InkPalette.SoftInk, 2f, 6f, 4f);
                    Dot(sx1 + 70, Hpx - 52, 5f, InkPalette.Coral, 1.6f);
                    Rect(150, Hpx - 90, 196, Hpx - 54, InkPalette.Marker, 2.2f, 2f);
                    _c.FillTriangle(X(146), Y(Hpx - 54), X(200), Y(Hpx - 54), X(173), Y(Hpx - 32), InkPalette.Coral);
                    _c.StrokeTriangle(X(146), Y(Hpx - 54), X(200), Y(Hpx - 54), X(173), Y(Hpx - 32), Ink, 2f);
                }
            }

            /// <summary>Flower beds, a winding path and a bench; then a pond and trees; then a greenhouse.</summary>
            public void Garden()
            {
                // Stepping-stone path from the gate
                var stone = InkPalette.Hex(0xEEF2F8);
                for (int i = 0; i < 6; i++)
                {
                    float t = i / 5f;
                    float x = DoorX + Mathf.Sin(t * 3f) * 34f, y = 14 + t * (Hpx - 60);
                    _c.FillEllipse(X(x), Y(y), 11f, 8f, stone);
                    _c.StrokeEllipse(X(x), Y(y), 11f, 8f, InkPalette.WithAlpha(Ink, 0.7f), 1.5f);
                }
                // Flower beds
                FlowerPatch(20, 20, 84, 64, 1);
                FlowerPatch(Wpx - 84, Hpx - 70, Wpx - 20, Hpx - 22, 2);
                // Bench
                BenchAt(Wpx - 94, 30, true);
                if (_level >= 2)
                {
                    // Pond with lily pads, and two shady trees
                    float px = 70, py = Hpx - 60;
                    _c.FillEllipse(X(px), Y(py), 48f, 30f, InkPalette.Sky);
                    _c.StrokeEllipse(X(px), Y(py), 48f, 30f, Ink, 2.2f);
                    _c.Squiggle(X(px - 26), Y(py + 6), 22, 1.6f, 8, Color.white, 1.4f);
                    Dot(px + 18, py - 8, 6f, InkPalette.Foliage, 1.4f);
                    Dot(px - 12, py - 14, 5f, InkPalette.Foliage, 1.4f);
                    Dot(px + 20, py - 9, 1.6f, InkPalette.Coral, 0f);
                    Crown(Wpx - 44, Hpx * 0.48f, 26f);
                }
                if (_level >= 3)
                {
                    // Greenhouse
                    float gx0 = 22, gy0 = 92, gx1 = 92, gy1 = 150;
                    Rect(gx0, gy0, gx1, gy1, InkPalette.WithAlpha(InkPalette.Pale, 0.9f), 2.2f, 3f);
                    for (float x = gx0 + 14; x < gx1; x += 14) Line(x, gy0 + 2, x, gy1 - 2, InkPalette.Mid, 1.2f);
                    Line(gx0 + 2, (gy0 + gy1) / 2, gx1 - 2, (gy0 + gy1) / 2, InkPalette.Mid, 1.2f);
                    Dot(gx0 + 20, gy0 + 16, 6f, InkPalette.Foliage, 1.3f);
                    Dot(gx1 - 20, gy1 - 16, 6f, InkPalette.Foliage, 1.3f);
                }
            }

            void FlowerPatch(float x0, float y0, float x1, float y1, int seed)
            {
                Rect(x0, y0, x1, y1, InkPalette.Hex(0xE7F1FC), 2f, 12f);
                var colors = new[] { InkPalette.Coral, InkPalette.Marker, InkPalette.Mid, Color.white, InkPalette.Coral };
                var rng = new System.Random(seed * 7 + 3);
                for (int i = 0; i < 9; i++)
                {
                    float fx = x0 + 9 + (float)rng.NextDouble() * (x1 - x0 - 18), fy = y0 + 9 + (float)rng.NextDouble() * (y1 - y0 - 18);
                    Dot(fx - 3, fy - 3, 3.6f, InkPalette.Foliage, 1f);
                    Dot(fx, fy, 3.4f, colors[i % colors.Length], 1.2f);
                }
            }

            void Crown(float x, float y, float r)
            {
                _c.FillEllipse(X(x + 4), Y(y - r * 0.8f), r * 0.9f, r * 0.25f, InkPalette.Hex(0xCBE0F6));
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * Mathf.PI * 2f;
                    _c.FillCircle(X(x + Mathf.Cos(a) * r * 0.45f), Y(y + Mathf.Sin(a) * r * 0.4f), r * 0.55f + 2.6f, Ink);
                }
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * Mathf.PI * 2f;
                    _c.FillCircle(X(x + Mathf.Cos(a) * r * 0.45f), Y(y + Mathf.Sin(a) * r * 0.4f), r * 0.55f, InkPalette.Foliage);
                }
                _c.Arc(X(x - r * 0.15f), Y(y + r * 0.15f), r * 0.4f, 1.9f, 2.9f, Color.white, 2.2f);
            }

            void BenchAt(float x, float y, bool horizontal)
            {
                if (horizontal)
                {
                    Rect(x, y, x + 64, y + 16, InkPalette.Marker, 2f, 3f);
                    for (float sx = x + 6; sx < x + 60; sx += 10) Line(sx, y + 3, sx, y + 13, InkPalette.WithAlpha(Ink, 0.5f), 1.1f);
                    Line(x + 2, y + 22, x + 62, y + 22, Ink, 3f);
                }
                else
                {
                    Rect(x, y, x + 16, y + 64, InkPalette.Marker, 2f, 3f);
                    for (float sy = y + 6; sy < y + 60; sy += 10) Line(x + 3, sy, x + 13, sy, InkPalette.WithAlpha(Ink, 0.5f), 1.1f);
                    Line(x + 22, y + 2, x + 22, y + 62, Ink, 3f);
                }
            }

            /// <summary>Open grass with goals and cones for PE and practice.</summary>
            public void PracticeField()
            {
                var white = Color.white;
                float x0 = 30, x1 = Wpx - 30, y0 = 30, y1 = Hpx - 30;
                _c.StrokeRect(X(x0), Y(y0), X(x1), Y(y1), InkPalette.WithAlpha(white, 0.95f), 3.5f);
                float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
                Line(cx, y0, cx, y1, white, 3f);
                _c.StrokeCircle(X(cx), Y(cy), 38f, white, 3f);
                foreach (float gx in new[] { x0, x1 })
                {
                    float dir = gx == x0 ? -1f : 1f;
                    Rect(gx + Mathf.Min(0, dir * 18), cy - 26, gx + Mathf.Max(0, dir * 18), cy + 26, InkPalette.Paper, 2.2f, 2f);
                    for (float y = cy - 20; y < cy + 22; y += 8) Line(gx + dir * 2, y, gx + dir * 16, y, InkPalette.Sky, 1.1f);
                }
                // Cones for drills
                for (int i = 0; i < 5; i++)
                {
                    float kx = x0 + 60 + i * 26, ky = y0 + 34 + (i % 2) * 14;
                    _c.FillTriangle(X(kx - 6), Y(ky - 5), X(kx + 6), Y(ky - 5), X(kx), Y(ky + 9), InkPalette.Coral);
                    _c.StrokeTriangle(X(kx - 6), Y(ky - 5), X(kx + 6), Y(ky - 5), X(kx), Y(ky + 9), Ink, 1.6f);
                }
                Dot(cx + 52, cy + 18, 7f, white, 1.8f);
                if (_level >= 2)
                {
                    // Kit shed
                    Rect(Wpx - 104, Hpx - 64, Wpx - 40, Hpx - 26, InkPalette.Pale, 2.2f, 2f);
                    Line(Wpx - 104, Hpx - 45, Wpx - 40, Hpx - 45, Ink, 1.4f);
                    Rect(Wpx - 80, Hpx - 64, Wpx - 64, Hpx - 48, InkPalette.Marker, 1.4f, 1f);
                }
                if (_level >= 3)
                    foreach (var (fx, fy) in new[] { (14f, 14f), (Wpx - 14, 14f), (14f, Hpx - 14), (Wpx - 14, Hpx - 14) })
                        Dot(fx, fy, 8f, InkPalette.Marker, 2f);
            }

            /// <summary>Picnic tables on the lawn; later with umbrellas and a café counter.</summary>
            public void PicnicArea()
            {
                int tables = _level >= 3 ? 3 : 2;
                for (int i = 0; i < tables; i++)
                {
                    float cx = Wpx * (tables == 2 ? (i == 0 ? 0.3f : 0.7f) : 0.22f + i * 0.28f), cy = Hpx * 0.52f;
                    Rect(cx - 30, cy - 15, cx + 30, cy + 15, InkPalette.Marker, 2.2f, 3f);
                    for (float x = cx - 24; x < cx + 26; x += 8) Line(x, cy - 12, x, cy + 12, InkPalette.WithAlpha(Ink, 0.4f), 1f);
                    Rect(cx - 30, cy + 20, cx + 30, cy + 28, InkPalette.Hex(0xFFF0B8), 1.8f, 2f);
                    Rect(cx - 30, cy - 28, cx + 30, cy - 20, InkPalette.Hex(0xFFF0B8), 1.8f, 2f);
                    if (_level >= 2)
                    {
                        _c.FillCircle(X(cx + 6), Y(cy + 8), 24f, InkPalette.WithAlpha(i % 2 == 0 ? InkPalette.Coral : InkPalette.Mid, 0.85f));
                        _c.StrokeCircle(X(cx + 6), Y(cy + 8), 24f, Ink, 2f);
                        for (int k = 0; k < 4; k++)
                        {
                            float a = k * Mathf.PI / 2f + 0.5f;
                            Line(cx + 6, cy + 8, cx + 6 + Mathf.Cos(a) * 22, cy + 8 + Mathf.Sin(a) * 22, Color.white, 1.4f);
                        }
                        Dot(cx + 6, cy + 8, 3f, Color.white, 1.4f);
                    }
                }
                if (_level >= 3)
                {
                    // String lights across the top
                    for (float x = 24; x < Wpx - 20; x += 22)
                    {
                        float y = Hpx - 22 - Mathf.Sin((x - 24) / (Wpx - 44) * Mathf.PI) * 10f;
                        Dot(x, y, 3.4f, InkPalette.Marker, 1.2f);
                    }
                }
            }

            // --- signature buildings, the International House and the old manor ------------------

            /// <summary>
            /// A lecture theatre in curved tiers round a lectern, and a reading room through the partition;
            /// then tutoring booths, then an observatory dome in the corner.
            /// </summary>
            public void ScholarsHall()
            {
                float cx = 200f, cy = Hpx - 48f;
                Rect(cx - 140, Hpx - 26, cx + 140, Hpx - 10, InkPalette.Board, 2.4f, 3f);
                _c.Squiggle(X(cx - 120), Y(Hpx - 18), 60, 2.4f, 12, Color.white, 1.5f);
                Line(cx + 10, Hpx - 18, cx + 70, Hpx - 18, Color.white, 1.5f);
                Rect(cx - 20, cy - 26, cx + 20, cy - 8, Color.white, 2.2f, 3f);   // lectern
                Dot(cx, cy, 7f, InkPalette.Mid, 1.8f);
                int tiers = _level >= 2 ? 4 : 3;
                float a0 = Mathf.PI + 0.38f, a1 = 2f * Mathf.PI - 0.38f;
                for (int t = 0; t < tiers; t++)
                {
                    float r = 78f + t * 38f;
                    _c.Arc(X(cx), Y(cy), r + 13f, a0, a1, InkPalette.WithAlpha(InkPalette.Mid, 0.55f), 1.6f);
                    int seats = Mathf.RoundToInt(r * (a1 - a0) / 24f);
                    for (int i = 0; i <= seats; i++)
                    {
                        float a = Mathf.Lerp(a0, a1, i / (float)seats);
                        Chair(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, 6.5f);
                    }
                }

                // The foyer: a noticeboard and plants by the door, then a cabinet of prizes
                Rect(10, 60, 26, 150, InkPalette.Pale, 2f, 2f);
                Rect(13, 120, 23, 136, InkPalette.Marker, 1f, 1f);
                Rect(13, 80, 23, 104, Color.white, 1f, 1f);
                Plant(46, 36, 0.85f);
                Plant(286, 36, 0.85f);
                if (_level >= 2)
                {
                    Rect(90, 20, 230, 46, InkPalette.Pale, 2.2f, 2f);
                    for (int i = 0; i < 5; i++) Dot(106 + i * 27, 33, 6f, i % 2 == 0 ? InkPalette.Marker : InkPalette.Sky, 1.6f);
                }

                // Reading room through the partition
                Line(400, 130, 400, Hpx, Ink, 4f);
                bool dome = _level >= 3;
                Shelf(Wpx - 30, 60, Wpx - 10, dome ? Hpx - 150 : Hpx - 60);
                Shelf(420, Hpx - 32, dome ? 520 : Wpx - 40, Hpx - 12);
                for (int i = 0; i < 2; i++)
                {
                    float ty = Hpx * (i == 0 ? 0.6f : 0.38f);
                    Rect(450, ty - 14, 560, ty + 14, Color.white, 2.2f, 3f);
                    Chair(475, ty + 24);
                    Chair(535, ty + 24);
                    Chair(475, ty - 24);
                    Chair(535, ty - 24);
                    if (_level >= 2) Monitor(505, ty, 20, 12);
                }
                if (_level >= 2)
                {
                    // Tutoring booths: a small table, a tutor and a student
                    foreach (float bx in new[] { 440f, 520f })
                    {
                        Rect(bx - 22, 40, bx + 22, 66, Color.white, 2f, 3f);
                        Chair(bx, 28, 6f);
                        Chair(bx, 78, 6f);
                    }
                }
                else
                {
                    // The librarian's desk by the way in
                    Rect(440, 44, 540, 70, Color.white, 2.2f, 3f);
                    Monitor(470, 57, 20, 12);
                    Chair(500, 30, 6.5f);
                }
                if (dome)
                {
                    // Observatory: a dome with its slit open and the telescope pointing out
                    float ox = Wpx - 70, oy = Hpx - 84;
                    _c.FillCircle(X(ox), Y(oy), 54f, InkPalette.Pale);
                    _c.StrokeCircle(X(ox), Y(oy), 54f, Ink, 2.6f);
                    _c.StrokeCircle(X(ox), Y(oy), 40f, InkPalette.WithAlpha(InkPalette.Mid, 0.8f), 1.6f);
                    Line(ox - 8, oy - 8, ox + 30, oy + 28, Ink, 8f);
                    Line(ox - 8, oy - 8, ox + 30, oy + 28, InkPalette.Mid, 4f);
                    Dot(ox - 8, oy - 8, 8f, InkPalette.Marker, 2f);
                    for (int i = 0; i < 4; i++) Dot(ox + 34 - i * 22, oy + 46 - (i % 2) * 6, 1.8f, InkPalette.Marker, 0f);
                    Plant(430, Hpx - 66, 0.85f);
                }
            }

            /// <summary>A sprung court between two stands; then a physio suite, a strength lab and a scoreboard; then stands all round under a roof that opens.</summary>
            public void Arena()
            {
                var white = Color.white;
                float x0 = 112, x1 = Wpx - 112, y0 = 122, y1 = Hpx - (_level >= 3 ? 112 : 92);
                _c.FillRect(X(x0), Y(y0), X(x1), Y(y1), InkPalette.Hex(0xCFE3F8), 4f);
                _c.StrokeRect(X(x0), Y(y0), X(x1), Y(y1), white, 4f);
                float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
                Line(cx, y0, cx, y1, white, 3.5f);
                _c.StrokeCircle(X(cx), Y(cy), 36f, white, 3.5f);
                _c.Arc(X(x0), Y(cy), 118f, -Mathf.PI / 2.4f, Mathf.PI / 2.4f, white, 3f);
                _c.Arc(X(x1), Y(cy), 118f, Mathf.PI - Mathf.PI / 2.4f, Mathf.PI + Mathf.PI / 2.4f, white, 3f);
                foreach (float bx in new[] { cx - 120f, cx + 40f })
                {
                    Rect(bx, y0 - 30, bx + 80, y0 - 16, InkPalette.Marker, 1.8f, 3f);   // team benches
                    for (float x = bx + 12; x < bx + 76; x += 18) Dot(x, y0 - 38, 5f, InkPalette.Sky, 1.4f);
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    float bx = side < 0 ? x0 : x1;
                    _c.FillRect(X(Mathf.Min(bx, bx - side * 70)), Y(cy - 40), X(Mathf.Max(bx, bx - side * 70)), Y(cy + 40), InkPalette.WithAlpha(InkPalette.Mid, 0.35f));
                    _c.StrokeRect(X(Mathf.Min(bx, bx - side * 70)), Y(cy - 40), X(Mathf.Max(bx, bx - side * 70)), Y(cy + 40), white, 3f);
                    Line(bx - side * 6, cy - 16, bx - side * 6, cy + 16, Ink, 4f);
                    _c.StrokeCircle(X(bx - side * 16), Y(cy), 8f, InkPalette.CoralText, 2.6f);
                }
                // Stands down both long sides
                for (int r = 0; r < 3; r++)
                {
                    var fill = r % 2 == 0 ? InkPalette.Pale : InkPalette.Sky;
                    Rect(20 + r * 28, y0 - 10, 44 + r * 28, y1 + 10, fill, 1.6f, 2f);
                    Rect(Wpx - 44 - r * 28, y0 - 10, Wpx - 20 - r * 28, y1 + 10, fill, 1.6f, 2f);
                }
                if (_level >= 2)
                {
                    // Physio suite in one corner, the strength lab in the other, a scoreboard over the court
                    Line(20, 100, 170, 100, Ink, 3f);
                    Line(170, 100, 170, 20, Ink, 3f);
                    Rect(36, 34, 120, 62, Color.white, 2f, 4f);
                    Rect(132, 36, 158, 62, InkPalette.Sky, 1.8f, 2f);
                    Line(145, 42, 145, 56, Ink, 2.4f);
                    Line(138, 49, 152, 49, Ink, 2.4f);
                    Line(Wpx - 20, 100, Wpx - 170, 100, Ink, 3f);
                    Line(Wpx - 170, 100, Wpx - 170, 20, Ink, 3f);
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 38 + i * 22;
                        Line(Wpx - 146, y, Wpx - 106, y, Ink, 3f);
                        Dot(Wpx - 146, y, 6f, InkPalette.Board, 1.2f);
                        Dot(Wpx - 106, y, 6f, InkPalette.Board, 1.2f);
                    }
                    Rect(Wpx - 86, 32, Wpx - 40, 82, InkPalette.Pale, 2f, 3f);
                    Rect(cx - 52, Hpx - 46, cx + 52, Hpx - 18, InkPalette.Board, 2.2f, 3f);
                    for (int i = 0; i < 4; i++) Dot(cx - 36 + i * 24, Hpx - 32, 5f, i < 2 ? InkPalette.Marker : InkPalette.Sky, 1f);
                }
                if (_level >= 3)
                {
                    // Stands along the top, and the roof that slides open over the court
                    for (int r = 0; r < 2; r++)
                    {
                        Rect(x0 - 10, y1 + 14 + r * 26, cx - 64, y1 + 36 + r * 26, r % 2 == 0 ? InkPalette.Pale : InkPalette.Sky, 1.6f, 2f);
                        Rect(cx + 64, y1 + 14 + r * 26, x1 + 10, y1 + 36 + r * 26, r % 2 == 0 ? InkPalette.Pale : InkPalette.Sky, 1.6f, 2f);
                    }
                    _c.StrokeEllipse(X(cx), Y(cy), (x1 - x0) * 0.42f, (y1 - y0) * 0.42f, InkPalette.WithAlpha(InkPalette.Mid, 0.7f), 2.4f);
                }
            }

            /// <summary>A lounge, a concierge desk and a terrace; then a spa, a sauna and fine dining; then a rooftop pool and a grand piano.</summary>
            public void Clubhouse()
            {
                // Terrace along the back, behind glass doors
                float ty = Hpx - 96;
                Line(14, ty, Wpx - 14, ty, Ink, 2f, 10f, 6f);
                bool pool = _level >= 3;
                if (pool)
                {
                    _c.FillRect(X(Wpx * 0.5f), Y(ty + 18), X(Wpx - 30), Y(Hpx - 18), InkPalette.Sky, 8f);
                    _c.StrokeRect(X(Wpx * 0.5f), Y(ty + 18), X(Wpx - 30), Y(Hpx - 18), Ink, 2.4f, 8f);
                    _c.Squiggle(X(Wpx * 0.5f + 20), Y(ty + 48), 70, 2f, 12, Color.white, 1.8f);
                    _c.Squiggle(X(Wpx * 0.5f + 100), Y(ty + 60), 60, 2f, 12, Color.white, 1.8f);
                }
                int umbrellas = pool ? 2 : 3;
                for (int i = 0; i < umbrellas; i++)
                {
                    float ux = 70 + i * 120, uy = ty + 46;
                    _c.FillCircle(X(ux), Y(uy), 24f, InkPalette.WithAlpha(i % 2 == 0 ? InkPalette.Coral : InkPalette.Mid, 0.85f));
                    _c.StrokeCircle(X(ux), Y(uy), 24f, Ink, 2f);
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k * Mathf.PI / 2f + 0.5f;
                        Line(ux, uy, ux + Mathf.Cos(a) * 22, uy + Mathf.Sin(a) * 22, Color.white, 1.4f);
                    }
                }

                // Lounge: sofas round a rug, the concierge by the door
                Rug(150, 230, 92, 46);
                Sofa(56, 286, 246, 320, InkPalette.Marker);
                Sofa(30, 150, 64, 286, InkPalette.Marker);
                _c.FillEllipse(X(150), Y(230), 34f, 16f, Color.white);
                _c.StrokeEllipse(X(150), Y(230), 34f, 16f, Ink, 2f);
                Rect(110, 62, 222, 90, Color.white, 2.4f, 12f);
                Dot(206, 82, 5f, InkPalette.Marker, 1.6f);
                Chair(166, 106, 7f);

                if (_level >= 2)
                {
                    // Spa pool with loungers, a sauna, and tables laid for dinner
                    float sx0 = Wpx - 200, sx1 = Wpx - 40;
                    _c.FillRect(X(sx0), Y(170), X(sx1), Y(270), InkPalette.Sky, 14f);
                    _c.StrokeRect(X(sx0), Y(170), X(sx1), Y(270), Ink, 2.4f, 14f);
                    _c.Squiggle(X(sx0 + 24), Y(220), 90, 2f, 12, Color.white, 1.6f);
                    for (int i = 0; i < 3; i++) Rect(sx0 + 6 + i * 52, 128, sx0 + 40 + i * 52, 152, Color.white, 1.8f, 4f);
                    Rect(Wpx - 120, 30, Wpx - 30, 100, InkPalette.Hex(0xFFF0B8), 2.2f, 3f);
                    for (float y = 44; y < 96; y += 12) Line(Wpx - 114, y, Wpx - 36, y, InkPalette.WithAlpha(Ink, 0.45f), 1.2f);
                    foreach (var (dx, dy) in new[] { (300f, 200f), (300f, 120f) })
                    {
                        Dot(dx, dy, 22f, Color.white, 2.2f);
                        for (int k = 0; k < 4; k++)
                        {
                            float a = k * Mathf.PI / 2f + 0.78f;
                            Chair(dx + Mathf.Cos(a) * 32, dy + Mathf.Sin(a) * 32, 6f);
                        }
                        Dot(dx, dy, 4f, InkPalette.Coral, 1f);
                    }
                }
                else
                {
                    // A quiet reading corner: two armchairs, a side table and the bookcase
                    Sofa(Wpx - 160, 210, Wpx - 104, 250, InkPalette.Mid);
                    Sofa(Wpx - 160, 120, Wpx - 104, 160, InkPalette.Mid);
                    Dot(Wpx - 132, 185, 13f, Color.white, 2f);
                    Shelf(Wpx - 34, 120, Wpx - 14, 300);
                    Plant(Wpx - 60, 60, 0.9f);
                }
                if (pool)
                {
                    // A grand piano and a chandelier over the lounge
                    Piano(24, 66);
                    Dot(150, 232, 9f, InkPalette.Marker, 1.8f);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        Line(150 + Mathf.Cos(a) * 12, 232 + Mathf.Sin(a) * 12, 150 + Mathf.Cos(a) * 20, 232 + Mathf.Sin(a) * 20, InkPalette.Marker, 2f);
                    }
                }
            }

            /// <summary>A stage and rows of seats with an aisle from the door; then curtains, an orchestra pit and a gallery; then boxes, a chandelier and a fly tower.</summary>
            public void Theatre()
            {
                float sx0 = 100, sx1 = Wpx - 100, sy0 = Hpx - 112, sy1 = Hpx - 14;
                Rect(sx0, sy0, sx1, sy1, InkPalette.Hex(0xC9DCF2), 2.6f, 3f);
                for (float x = sx0 + 24; x < sx1 - 10; x += 32) Line(x, sy0 + 4, x, sy1 - 4, InkPalette.WithAlpha(InkPalette.Mid, 0.45f), 1.2f);
                if (_level >= 3)
                    for (float x = sx0 + 6; x < sx1 - 20; x += 18) Line(x, sy1 - 30, x + 22, sy1 - 4, InkPalette.WithAlpha(Ink, 0.35f), 1.2f);   // the fly tower overhead
                if (_level >= 2)
                {
                    foreach (float cx in new[] { sx0 + 18, sx1 - 18 })
                    {
                        Rect(cx - 18, sy0 - 4, cx + 18, sy1 + 2, InkPalette.Coral, 2.2f, 8f);
                        for (float x = cx - 9; x <= cx + 9; x += 9) Line(x, sy0 + 4, x, sy1 - 4, InkPalette.WithAlpha(Ink, 0.4f), 1.2f);
                    }
                    // Orchestra pit with music stands
                    _c.FillEllipse(X(Wpx / 2), Y(sy0 - 16), 120f, 24f, InkPalette.Pale);
                    _c.StrokeEllipse(X(Wpx / 2), Y(sy0 - 16), 120f, 24f, Ink, 2f);
                    for (int i = 0; i < 6; i++) Dot(Wpx / 2 - 85 + i * 34, sy0 - 20 + (i % 2) * 8, 3.6f, Ink, 0f);
                }

                // Rows of seats, with an aisle straight up from the door
                int rows = 4 + _level;
                float top = sy0 - (_level >= 2 ? 52 : 26), pitch = 34f;
                for (int r = 0; r < rows; r++)
                {
                    float y = top - r * pitch;
                    for (float x = sx0 + 6; x <= sx1 - 6; x += 26)
                        if (Mathf.Abs(x - DoorX) > 26f) Chair(x, y, 7f);
                }

                if (_level >= 2)
                {
                    // A gallery of paintings along the right wall
                    for (int i = 0; i < 4; i++)
                    {
                        float y = 70 + i * 66;
                        Rect(Wpx - 34, y, Wpx - 12, y + 44, i % 2 == 0 ? InkPalette.Marker : InkPalette.Mid, 2f, 2f);
                        Rect(Wpx - 30, y + 4, Wpx - 16, y + 40, Color.white, 1.2f, 1f);
                    }
                }
                else
                {
                    // Rehearsal studio: a mirror wall and a barre
                    _c.FillRect(X(6), Y(90), X(84), Y(sy0 - 20), InkPalette.WithAlpha(InkPalette.Pale, 0.8f));
                    Line(84, 90, 84, sy0 - 20, Ink, 3f);
                    Rect(14, 100, 30, sy0 - 30, InkPalette.Pale, 2f, 2f);
                    for (float y = 120; y < sy0 - 40; y += 40) Line(17, y, 27, y + 14, Color.white, 1.6f);
                    Line(44, 100, 44, sy0 - 30, InkPalette.SoftInk, 2.4f);
                }
                // The box office by the door, and posters for the next show
                Rect(130, 36, 250, 62, Color.white, 2.2f, 3f);
                Chair(190, 22, 6.5f);
                Rect(DoorX + 50, 14, DoorX + 82, 54, InkPalette.Coral, 2f, 2f);
                Rect(DoorX + 92, 14, DoorX + 124, 54, InkPalette.Marker, 2f, 2f);
                Dot(DoorX + 66, 40, 6f, Color.white, 1.4f);
                _c.Squiggle(X(DoorX + 96), Y(30), 24, 2f, 8, Ink, 1.4f);

                if (_level >= 3)
                {
                    // Boxes on the left wall and a chandelier over the seats
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 110 + i * 70;
                        Rect(10, y, 70, y + 46, InkPalette.Pale, 2.2f, 10f);
                        Chair(30, y + 23, 6f);
                        Chair(50, y + 23, 6f);
                    }
                    float ccx = DoorX, ccy = top - pitch * 2.5f;   // over the aisle
                    Dot(ccx, ccy, 10f, InkPalette.Marker, 2f);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        Line(ccx + Mathf.Cos(a) * 13, ccy + Mathf.Sin(a) * 13, ccx + Mathf.Cos(a) * 22, ccy + Mathf.Sin(a) * 22, InkPalette.Marker, 2f);
                    }
                }
            }

            /// <summary>Workbenches, 3D printers and a pegboard; then a robotics arena and a server room; then start-up pods round a big screen.</summary>
            public void InnovationLab()
            {
                // Pegboard of tools along the back wall
                Rect(36, Hpx - 40, 270, Hpx - 14, InkPalette.Pale, 2f, 2f);
                for (float x = 48; x < 262; x += 14)
                    for (float y = Hpx - 34; y < Hpx - 18; y += 8) _c.FillCircle(X(x), Y(y), 1.4f, InkPalette.WithAlpha(Ink, 0.4f));
                Line(70, Hpx - 22, 70, Hpx - 36, Ink, 3f);
                Line(110, Hpx - 20, 124, Hpx - 34, Ink, 3f);
                Dot(170, Hpx - 27, 6f, InkPalette.Coral, 1.6f);

                // Workbenches with tools and stools
                foreach (float by in new[] { Hpx - 110f, Hpx - 200f })
                {
                    Rect(40, by - 14, 260, by + 14, Color.white, 2.2f, 3f);
                    for (int i = 0; i < 4; i++) Chair(64 + i * 56, by - 26, 6f);
                    Dot(90, by + 2, 5f, InkPalette.Marker, 1.4f);
                    Rect(150, by - 6, 178, by + 6, InkPalette.Sky, 1.4f, 2f);
                    Dot(220, by, 4f, InkPalette.Coral, 1.2f);
                }
                // 3D printers along the right wall, each with something half printed
                int printers = _level >= 2 ? 2 : 3;
                for (int i = 0; i < printers; i++)
                {
                    float py = 120 + i * 80;
                    Rect(Wpx - 90, py, Wpx - 30, py + 52, InkPalette.Pale, 2.2f, 3f);
                    Line(Wpx - 82, py + 40, Wpx - 38, py + 40, Ink, 1.6f);
                    _c.FillTriangle(X(Wpx - 68), Y(py + 10), X(Wpx - 52), Y(py + 10), X(Wpx - 60), Y(py + 30), InkPalette.Coral);
                    _c.StrokeTriangle(X(Wpx - 68), Y(py + 10), X(Wpx - 52), Y(py + 10), X(Wpx - 60), Y(py + 30), Ink, 1.4f);
                }
                if (_level >= 2)
                {
                    // Robotics arena with a little robot, and racks humming in the server room
                    float ax0 = 300, ax1 = 430, ay0 = 150, ay1 = 280;
                    Rect(ax0, ay0, ax1, ay1, InkPalette.Hex(0xEEF5FD), 2.4f, 2f);
                    for (float x = ax0 + 26; x < ax1; x += 26) Line(x, ay0 + 2, x, ay1 - 2, InkPalette.TileLine, 1.2f);
                    for (float y = ay0 + 26; y < ay1; y += 26) Line(ax0 + 2, y, ax1 - 2, y, InkPalette.TileLine, 1.2f);
                    Rect(350, 200, 380, 226, InkPalette.Mid, 2f, 4f);
                    Dot(346, 204, 4f, Ink, 0f);
                    Dot(384, 204, 4f, Ink, 0f);
                    Dot(365, 232, 3f, InkPalette.Marker, 1f);
                    Line(Wpx - 160, Hpx - 14, Wpx - 160, Hpx - 100, Ink, 3f);
                    Line(Wpx - 160, Hpx - 100, Wpx - 14, Hpx - 100, Ink, 3f);
                    for (int i = 0; i < 3; i++)
                    {
                        float rx = Wpx - 146 + i * 44;
                        Rect(rx, Hpx - 86, rx + 30, Hpx - 24, InkPalette.Board, 1.8f, 2f);
                        for (int k = 0; k < 4; k++) Dot(rx + 8, Hpx - 76 + k * 12, 2.2f, (i + k) % 2 == 0 ? InkPalette.Marker : InkPalette.Sky, 0f);
                    }
                }
                if (_level >= 3)
                {
                    // Start-up pods round a big screen, a rocket on the wall
                    Screen(14, 80, 30, 190);
                    foreach (var (px, py) in new[] { (90f, 110f), (180f, 70f) })
                    {
                        Dot(px, py, 22f, Color.white, 2.2f);
                        Monitor(px, py + 2, 18, 11);
                        Chair(px - 30, py, 6f);
                        Chair(px + 30, py, 6f);
                    }
                    Rect(Wpx - 62, Hpx - 168, Wpx - 26, Hpx - 116, Color.white, 2f, 2f);
                    _c.FillTriangle(X(Wpx - 52), Y(Hpx - 152), X(Wpx - 36), Y(Hpx - 152), X(Wpx - 44), Y(Hpx - 128), InkPalette.Coral);
                    _c.StrokeTriangle(X(Wpx - 52), Y(Hpx - 152), X(Wpx - 36), Y(Hpx - 152), X(Wpx - 44), Y(Hpx - 128), Ink, 1.6f);
                    Line(Wpx - 44, Hpx - 154, Wpx - 44, Hpx - 162, InkPalette.Marker, 3f);
                    Plant(Wpx - 30, 34, 0.85f);
                }
            }

            /// <summary>A town hall with folding chairs and a food bank; then a cafe and a craft table; then a public library corner.</summary>
            public void CommunityCentre()
            {
                // The hall: a long table at the front, folding chairs in rows
                Rect(50, Hpx - 52, 300, Hpx - 26, Color.white, 2.2f, 3f);
                for (int i = 0; i < 4; i++) Chair(80 + i * 64, Hpx - 14, 6f);
                Line(320, Hpx - 20, 320, Hpx - 70, Ink, 2.4f);
                _c.FillTriangle(X(320), Y(Hpx - 22), X(320), Y(Hpx - 44), X(344), Y(Hpx - 33), InkPalette.Marker);
                _c.StrokeTriangle(X(320), Y(Hpx - 22), X(320), Y(Hpx - 44), X(344), Y(Hpx - 33), Ink, 1.6f);
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 6; c++)
                        Chair(76 + c * 40, Hpx - 100 - r * 40, 6f);

                // Food bank: shelves of crates and a packing table
                for (int s = 0; s < 3; s++)
                {
                    float y0 = 70 + s * 110;
                    Rect(Wpx - 86, y0, Wpx - 14, y0 + 84, Color.white, 2.2f, 2f);
                    var colors = new[] { InkPalette.Marker, InkPalette.Sky, InkPalette.Coral, InkPalette.Mid };
                    for (int k = 0; k < 6; k++)
                        Rect(Wpx - 80 + (k % 3) * 22, y0 + 6 + (k / 3) * 40, Wpx - 62 + (k % 3) * 22, y0 + 34 + (k / 3) * 40, colors[(k + s) % colors.Length], 1.4f, 2f);
                }
                Rect(Wpx - 210, 260, Wpx - 120, 290, Color.white, 2.2f, 3f);
                Rect(Wpx - 200, 266, Wpx - 176, 284, InkPalette.Marker, 1.4f, 2f);
                Rect(Wpx - 166, 266, Wpx - 142, 284, InkPalette.Marker, 1.4f, 2f);

                if (_level >= 2)
                {
                    // Cafe corner and the craft table for after-school clubs
                    Rect(Wpx - 250, Hpx - 44, Wpx - 110, Hpx - 18, Color.white, 2.2f, 3f);
                    Rect(Wpx - 132, Hpx - 40, Wpx - 114, Hpx - 22, Ink, 1.4f, 2f);
                    foreach (float tx in new[] { Wpx - 230f, Wpx - 160f })
                    {
                        Dot(tx, Hpx - 110, 15f, Color.white, 2f);
                        Chair(tx - 22, Hpx - 110, 5.5f);
                        Chair(tx + 22, Hpx - 110, 5.5f);
                    }
                    Rect(60, 60, 220, 92, Color.white, 2.2f, 3f);
                    for (int i = 0; i < 5; i++)
                    {
                        Rect(72 + i * 30, 66, 92 + i * 30, 86, Color.white, 1.2f, 1f);
                        Dot(84 + i * 30, 74, 2.6f, i % 2 == 0 ? InkPalette.Coral : InkPalette.Mid, 0f);
                    }
                    for (int i = 0; i < 4; i++) Chair(80 + i * 40, 46, 5.5f);
                }
                if (_level >= 3)
                {
                    // A public library corner and the town noticeboard
                    Shelf(10, 120, 30, Hpx - 130);
                    Rect(240, 120, 290, 160, InkPalette.Pale, 2f, 2f);
                    Rect(246, 140, 262, 154, InkPalette.Marker, 1f, 1f);
                    Rect(268, 128, 284, 142, InkPalette.Coral, 1f, 1f);
                    Rect(250, 124, 264, 136, Color.white, 1f, 1f);
                    Plant(Wpx - 120, 34, 0.85f);
                }
            }

            /// <summary>A kitchen from home, a long table and a world map; then a language corner and bunting; then a room for visiting families.</summary>
            public void InternationalHouse()
            {
                // Kitchen along the back wall
                Rect(20, Hpx - 40, 230, Hpx - 14, Color.white, 2.2f, 3f);
                for (int i = 0; i < 3; i++) _c.StrokeCircle(X(46 + i * 34), Y(Hpx - 27), 7f, Ink, 1.6f);
                Sink(200, Hpx - 27);
                // World map with blobby continents
                Rect(250, Hpx - 46, Wpx - 18, Hpx - 12, InkPalette.Sky, 2.2f, 2f);
                _c.FillEllipse(X(270), Y(Hpx - 26), 9f, 10f, Color.white);
                _c.FillEllipse(X(300), Y(Hpx - 30), 8f, 12f, Color.white);
                _c.FillEllipse(X(330), Y(Hpx - 24), 16f, 8f, Color.white);
                _c.FillEllipse(X(348), Y(Hpx - 37), 6f, 4f, Color.white);
                Dot(318, Hpx - 26, 2.4f, InkPalette.Coral, 0f);

                // The long table where everyone eats together
                Rect(70, 150, 250, 186, Color.white, 2.2f, 4f);
                for (int i = 0; i < 4; i++)
                {
                    Chair(92 + i * 45, 198, 6.5f);
                    Chair(92 + i * 45, 138, 6.5f);
                }
                Dot(130, 168, 5f, InkPalette.Coral, 1.4f);
                Dot(190, 168, 5f, InkPalette.Marker, 1.4f);
                Sofa(14, 40, 48, 128, InkPalette.Mid);

                if (_level >= 2)
                {
                    // Language corner: a board of new words, and bunting of flags
                    Rect(Wpx - 104, 150, Wpx - 18, 186, InkPalette.Board, 2.2f, 2f);
                    _c.Squiggle(X(Wpx - 96), Y(172), 30, 2f, 10, Color.white, 1.4f);
                    Line(Wpx - 58, 172, Wpx - 30, 172, Color.white, 1.4f);
                    _c.Squiggle(X(Wpx - 96), Y(160), 50, 1.6f, 8, Color.white, 1.2f);
                    Rect(Wpx - 90, 104, Wpx - 40, 126, Color.white, 2f, 3f);
                    Chair(Wpx - 100, 115, 5.5f);
                    Chair(Wpx - 30, 115, 5.5f);
                    var flags = new[] { InkPalette.Coral, InkPalette.Marker, InkPalette.Mid, Color.white, InkPalette.Sky };
                    float fy = Hpx - 64;
                    Line(16, fy, Wpx - 16, fy, InkPalette.SoftInk, 1.4f);
                    for (int i = 0; i < 10; i++)
                    {
                        float fx = 30 + i * (Wpx - 60) / 9f;
                        _c.FillTriangle(X(fx - 8), Y(fy), X(fx + 8), Y(fy), X(fx), Y(fy - 16), flags[i % flags.Length]);
                        _c.StrokeTriangle(X(fx - 8), Y(fy), X(fx + 8), Y(fy), X(fx), Y(fy - 16), Ink, 1.2f);
                    }
                }
                if (_level >= 3)
                {
                    // A guest room for families visiting from far away
                    Line(Wpx - 120, 0, Wpx - 120, 96, Ink, 3f);
                    Line(Wpx - 120, 96, Wpx - 64, 96, Ink, 3f);
                    Bed(Wpx - 104, 14, 46, 70, InkPalette.Marker);
                    Plant(Wpx - 30, 40, 0.8f);
                    Plant(40, 225, 0.75f);
                }
            }

            /// <summary>The old manor: dusty and leaking, then restored for dinners, then a ballroom in its glory.</summary>
            public void Manor()
            {
                // The grand staircase sweeping down from the gallery
                float cx = Wpx / 2;
                for (int i = 0; i < 6; i++)
                {
                    float hw = 110 - i * 9, y = Hpx - 120 + i * 16;
                    Rect(cx - hw, y, cx + hw, y + 14, i % 2 == 0 ? InkPalette.Pale : Color.white, 1.8f, 2f);
                }
                Rect(cx - 130, Hpx - 26, cx + 130, Hpx - 12, InkPalette.Sky, 2f, 2f);

                if (_level == 1)
                {
                    // Dust sheets over the old furniture, buckets under the leaks, cobwebs in the corners
                    foreach (var (fx, fy, rx, ry) in new[] { (110f, 250f, 46f, 30f), (200f, 150f, 36f, 26f), (Wpx - 120f, 240f, 52f, 32f), (Wpx - 200f, 140f, 30f, 22f) })
                    {
                        _c.FillEllipse(X(fx), Y(fy), rx, ry, Color.white);
                        _c.StrokeEllipse(X(fx), Y(fy), rx, ry, InkPalette.WithAlpha(Ink, 0.7f), 1.8f);
                        Line(fx - rx * 0.5f, fy - ry * 0.2f, fx + rx * 0.3f, fy + ry * 0.4f, InkPalette.WithAlpha(Ink, 0.3f), 1.2f);
                    }
                    foreach (var (bx, by) in new[] { (300f, 230f), (Wpx - 280f, 110f) })
                    {
                        Rect(bx - 10, by - 10, bx + 10, by + 8, InkPalette.Sky, 1.8f, 3f);
                        Dot(bx, by + 22, 3f, InkPalette.Mid, 1f);
                        Dot(bx, by + 40, 2.4f, InkPalette.Mid, 1f);
                    }
                    var web = InkPalette.WithAlpha(InkPalette.SoftInk, 0.55f);
                    foreach (var (wx, wy, sx, sy) in new[] { (6f, Hpx - 6f, 1f, -1f), (Wpx - 6f, Hpx - 6f, -1f, -1f) })
                    {
                        for (int k = 0; k < 3; k++) Line(wx, wy, wx + sx * (30 + k * 10), wy + sy * (40 - k * 14), web, 1f);
                        for (int k = 1; k <= 3; k++) _c.Arc(X(wx), Y(wy), k * 12f, sx > 0 ? -Mathf.PI / 2f : Mathf.PI, sx > 0 ? 0f : 1.5f * Mathf.PI, web, 1f);
                    }
                    Line(60, 60, 140, 90, InkPalette.WithAlpha(Ink, 0.4f), 1.4f, 6f, 5f);
                    return;
                }

                // Restored: a banquet table on a long rug, a fireplace, the library wall
                Rug(cx, 200, 230, 50);
                Rect(cx - 200, 184, cx + 200, 216, Color.white, 2.2f, 4f);
                for (float x = cx - 180; x <= cx + 180; x += 40)
                {
                    Chair(x, 228, 6f);
                    Chair(x, 172, 6f);
                }
                for (int i = 0; i < 3; i++) Dot(cx - 120 + i * 120, 200, 4.5f, InkPalette.Marker, 1.2f);
                Rect(10, 120, 34, 210, InkPalette.Pale, 2.2f, 3f);
                _c.FillEllipse(X(26), Y(165), 6f, 14f, InkPalette.Marker);
                _c.FillEllipse(X(26), Y(160), 4f, 8f, InkPalette.Coral);
                Shelf(Wpx - 30, 90, Wpx - 10, Hpx - 90);

                if (_level >= 3)
                {
                    // The ballroom floor, chandeliers, portraits and a grand piano
                    for (int i = 0; i < 8; i++)
                        for (int j = 0; j < 2; j++)
                            if ((i + j) % 2 == 0) _c.FillRect(X(cx - 160 + i * 40), Y(84 + j * 40), X(cx - 120 + i * 40), Y(124 + j * 40), InkPalette.WithAlpha(InkPalette.Sky, 0.55f));
                    foreach (float lx in new[] { cx - 120f, cx + 120f })
                    {
                        Dot(lx, 300, 10f, InkPalette.Marker, 2f);
                        for (int k = 0; k < 8; k++)
                        {
                            float a = k * Mathf.PI / 4f;
                            Line(lx + Mathf.Cos(a) * 13, 300 + Mathf.Sin(a) * 13, lx + Mathf.Cos(a) * 22, 300 + Mathf.Sin(a) * 22, InkPalette.Marker, 2f);
                        }
                    }
                    for (int i = 0; i < 2; i++) Rect(10, 240 + i * 70, 30, 290 + i * 70, i == 0 ? InkPalette.Marker : InkPalette.Mid, 2f, 2f);
                    Piano(Wpx - 120, 50);
                    Plant(60, 40, 0.9f);
                }
            }

            // --- decorations --------------------------------------------------------------------

            /// <summary>A young tree planted on a single tile.</summary>
            public void DecorTree()
            {
                float cx = Wpx / 2, cy = Hpx / 2 + 6;
                _c.FillEllipse(X(cx + 5), Y(cy - 22), 22f, 6f, InkPalette.Hex(0xCBE0F6));
                Rect(cx - 4, cy - 24, cx + 4, cy - 6, Color.white, 2f, 2f);
                Crown(cx, cy + 6, 24f);
            }

            public void FlowerBed()
            {
                float x0 = 8, y0 = 12, x1 = Wpx - 8, y1 = Hpx - 12;
                Rect(x0, y0, x1, y1, InkPalette.Hex(0xE4EEF8), 2.2f, 14f);
                var colors = new[] { InkPalette.Coral, InkPalette.Marker, InkPalette.Mid, Color.white };
                for (int i = 0; i < 7; i++)
                {
                    float fx = x0 + 14 + i * (x1 - x0 - 28) / 6f, fy = (y0 + y1) / 2 + (i % 2 == 0 ? 7 : -7);
                    Dot(fx - 4, fy - 4, 4f, InkPalette.Foliage, 1f);
                    Dot(fx, fy, 4.4f, colors[i % colors.Length], 1.3f);
                    Dot(fx, fy, 1.4f, i % 2 == 0 ? InkPalette.Marker : InkPalette.Coral, 0f);
                }
            }

            public void Bench()
            {
                BenchAt(Wpx / 2 - 32, Hpx / 2 - 14, true);
                Line(Wpx / 2 - 26, Hpx / 2 - 18, Wpx / 2 - 26, Hpx / 2 - 14, Ink, 2f);
                Line(Wpx / 2 + 26, Hpx / 2 - 18, Wpx / 2 + 26, Hpx / 2 - 14, Ink, 2f);
            }

            public void Fountain()
            {
                float cx = Wpx / 2, cy = Hpx / 2;
                _c.FillCircle(X(cx), Y(cy), 56f, InkPalette.Hex(0xEEF2F8));
                _c.StrokeCircle(X(cx), Y(cy), 56f, Ink, 3f);
                _c.FillCircle(X(cx), Y(cy), 46f, InkPalette.Sky);
                _c.StrokeCircle(X(cx), Y(cy), 46f, Ink, 2.2f);
                for (int i = 0; i < 3; i++) _c.StrokeCircle(X(cx), Y(cy), 18f + i * 9f, InkPalette.WithAlpha(Color.white, 0.85f), 1.6f);
                Dot(cx, cy, 11f, InkPalette.Pale, 2.2f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f;
                    Dot(cx + Mathf.Cos(a) * 30, cy + Mathf.Sin(a) * 30, 2.4f, Color.white, 1f);
                }
                Dot(cx, cy, 4f, InkPalette.Mid, 1.4f);
            }

            public void Statue()
            {
                float cx = Wpx / 2, cy = Hpx / 2;
                Rect(cx - 44, cy - 44, cx + 44, cy + 44, InkPalette.Hex(0xEEF2F8), 2.8f, 6f);
                Rect(cx - 30, cy - 30, cx + 30, cy + 30, InkPalette.Pale, 2.2f, 4f);
                // The founder from above: shoulders, head and a pointing arm
                _c.FillEllipse(X(cx), Y(cy - 2), 18f, 11f, InkPalette.Hex(0x9CB7D8));
                _c.StrokeEllipse(X(cx), Y(cy - 2), 18f, 11f, Ink, 2f);
                Line(cx + 12, cy + 2, cx + 30, cy + 18, Ink, 6f);
                Line(cx + 12, cy + 2, cx + 30, cy + 18, InkPalette.Hex(0x9CB7D8), 3.4f);
                Dot(cx, cy + 4, 9f, InkPalette.Hex(0xB9CCE4), 2f);
                Rect(cx - 16, cy - 40, cx + 16, cy - 33, InkPalette.Marker, 1.4f, 1f);
            }
        }
    }
}
