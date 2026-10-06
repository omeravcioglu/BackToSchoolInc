using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SchoolSim.Game
{
    /// <summary>
    /// Generates every tile and sprite the campus uses, procedurally, in the notebook style.
    /// Results are cached: the same room type, level and rotation always shares one texture.
    /// </summary>
    public static class CampusArt
    {
        public const int PPU = 64;          // pixels per tile
        public const int RoomPad = 14;      // transparent margin around room drawings

        static readonly Dictionary<string, Tile> Tiles = new Dictionary<string, Tile>();
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static Font _font;

        // ---------------------------------------------------------------------------------
        // Fonts
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Handwritten font if one was added to Resources/Fonts (Caveat per the design), otherwise
        /// Unity's built-in font.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/Caveat-Bold")
                        ?? Resources.Load<Font>("Fonts/Caveat")
                        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        // ---------------------------------------------------------------------------------
        // Tiles
        // ---------------------------------------------------------------------------------

        static Tile MakeTile(string key, InkCanvas canvas)
        {
            if (Tiles.TryGetValue(key, out var existing)) return existing;
            var tex = canvas.ToTexture(true, key);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
            tile.colliderType = Tile.ColliderType.None;
            tile.flags = TileFlags.None;
            tile.name = key;
            Tiles[key] = tile;
            return tile;
        }

        static void GridLines(InkCanvas c, Color color, float width)
        {
            c.Line(0.5f, 0f, 0.5f, PPU, color, width);
            c.Line(0f, 0.5f, PPU, 0.5f, color, width);
        }

        /// <summary>Unowned land: plain graph paper.</summary>
        public static Tile Paper
        {
            get
            {
                if (Tiles.TryGetValue("paper", out var t)) return t;
                var c = new InkCanvas(PPU, PPU);
                c.Clear(InkPalette.Graph);
                GridLines(c, InkPalette.GraphLine, 1.6f);
                return MakeTile("paper", c);
            }
        }

        /// <summary>Owned land: the hatched lawn from the design, over faint graph lines.</summary>
        public static Tile Lawn
        {
            get
            {
                if (Tiles.TryGetValue("lawn", out var t)) return t;
                var c = new InkCanvas(PPU, PPU);
                c.Clear(InkPalette.Lawn);
                // 45° hatch with a period that divides the tile, so it runs seamlessly across tiles.
                for (int k = -PPU; k <= PPU * 2; k += 16) c.Line(k, 0f, k - PPU, PPU, InkPalette.LawnHatch, 2.4f);
                GridLines(c, InkPalette.WithAlpha(InkPalette.GraphLine, 0.7f), 1.4f);
                return MakeTile("lawn", c);
            }
        }

        /// <summary>
        /// Dashed boundary around owned land. Mask bits: 1 = north, 2 = east, 4 = south, 8 = west
        /// (the sides that border land you don't own).
        /// </summary>
        public static Tile LawnEdge(int mask)
        {
            string key = "lawnEdge" + mask;
            if (Tiles.TryGetValue(key, out var t)) return t;
            var c = new InkCanvas(PPU, PPU);
            const float inset = 3f, w = 2.6f, dash = 10f, gap = 6f;
            if ((mask & 1) != 0) c.Line(0f, PPU - inset, PPU, PPU - inset, InkPalette.LawnEdge, w, dash, gap);
            if ((mask & 2) != 0) c.Line(PPU - inset, 0f, PPU - inset, PPU, InkPalette.LawnEdge, w, dash, gap);
            if ((mask & 4) != 0) c.Line(0f, inset, PPU, inset, InkPalette.LawnEdge, w, dash, gap);
            if ((mask & 8) != 0) c.Line(inset, 0f, inset, PPU, InkPalette.LawnEdge, w, dash, gap);
            return MakeTile(key, c);
        }

        /// <summary>The old estate's parkland: long grass hatched the other way, with tufts.</summary>
        public static Tile Meadow
        {
            get
            {
                if (Tiles.TryGetValue("meadow", out var t)) return t;
                var c = new InkCanvas(PPU, PPU, 23);
                c.Clear(InkPalette.Lawn);
                for (int k = -PPU; k <= PPU * 2; k += 16) c.Line(k, 0f, k + PPU, PPU, InkPalette.WithAlpha(InkPalette.LawnHatch, 0.8f), 2f);
                foreach (var (x, y) in new[] { (14f, 44f), (42f, 18f), (50f, 50f), (24f, 10f) })
                {
                    c.Line(x - 4, y + 5, x, y, InkPalette.LawnEdge, 1.6f);
                    c.Line(x, y, x + 1, y + 7, InkPalette.LawnEdge, 1.6f);
                    c.Line(x + 5, y + 4, x, y, InkPalette.LawnEdge, 1.6f);
                }
                return MakeTile("meadow", c);
            }
        }

        /// <summary>The gravel drive up to the old manor.</summary>
        public static Tile Gravel
        {
            get
            {
                if (Tiles.TryGetValue("gravel", out var t)) return t;
                var c = new InkCanvas(PPU, PPU, 29);
                c.Clear(InkPalette.Hex(0xEEF3F9));
                var rng = new System.Random(5);
                for (int i = 0; i < 30; i++)
                    c.FillCircle(4 + rng.Next(PPU - 8), 4 + rng.Next(PPU - 8), 1.2f + (float)rng.NextDouble(), InkPalette.WithAlpha(InkPalette.SoftInk, 0.35f));
                return MakeTile("gravel", c);
            }
        }

        /// <summary>
        /// The old stone wall round the estate while it is closed. Mask bits as <see cref="LawnEdge"/>:
        /// 1 = north, 2 = east, 4 = south, 8 = west.
        /// </summary>
        public static Tile EstateWall(int mask)
        {
            string key = "estateWall" + mask;
            if (Tiles.TryGetValue(key, out var t)) return t;
            var c = new InkCanvas(PPU, PPU, 31);
            const float a = 2f, b = 14f;   // the wall runs between these insets
            var stone = InkPalette.Hex(0xE9EFF7);
            if ((mask & 1) != 0) WallBand(c, 0f, PPU - b, PPU, PPU - a, true, stone);
            if ((mask & 4) != 0) WallBand(c, 0f, a, PPU, b, true, stone);
            if ((mask & 2) != 0) WallBand(c, PPU - b, 0f, PPU - a, PPU, false, stone);
            if ((mask & 8) != 0) WallBand(c, a, 0f, b, PPU, false, stone);
            return MakeTile(key, c);
        }

        static void WallBand(InkCanvas c, float x0, float y0, float x1, float y1, bool horizontal, Color stone)
        {
            c.FillRect(x0, y0, x1, y1, stone);
            if (horizontal)
            {
                c.Line(x0, y0, x1, y0, InkPalette.Ink, 2f);
                c.Line(x0, y1, x1, y1, InkPalette.Ink, 2f);
                for (float x = x0 + 8f; x < x1; x += 16f) c.Line(x, y0, x, y1, InkPalette.WithAlpha(InkPalette.Ink, 0.55f), 1.3f);
            }
            else
            {
                c.Line(x0, y0, x0, y1, InkPalette.Ink, 2f);
                c.Line(x1, y0, x1, y1, InkPalette.Ink, 2f);
                for (float y = y0 + 8f; y < y1; y += 16f) c.Line(x0, y, x1, y, InkPalette.WithAlpha(InkPalette.Ink, 0.55f), 1.3f);
            }
        }

        /// <summary>Opaque white tile, tinted per cell for ghosts, highlights and heat maps.</summary>
        public static Tile White
        {
            get
            {
                if (Tiles.TryGetValue("white", out var t)) return t;
                var c = new InkCanvas(PPU, PPU);
                c.Clear(Color.white);
                return MakeTile("white", c);
            }
        }

        /// <summary>The blue blueprint grid shown over everything in build mode.</summary>
        public static Tile BuildGrid
        {
            get
            {
                if (Tiles.TryGetValue("buildGrid", out var t)) return t;
                var c = new InkCanvas(PPU, PPU);
                c.Clear(InkPalette.WithAlpha(InkPalette.Blue, 0.07f));
                GridLines(c, InkPalette.WithAlpha(InkPalette.Blue, 0.22f), 1.8f);
                return MakeTile("buildGrid", c);
            }
        }

        /// <summary>Construction-site hatching.</summary>
        public static Tile Site
        {
            get
            {
                if (Tiles.TryGetValue("site", out var t)) return t;
                var c = new InkCanvas(PPU, PPU);
                c.Clear(InkPalette.Graph);
                for (int k = -PPU; k <= PPU * 2; k += 16) c.Line(k, 0f, k + PPU, PPU, InkPalette.SiteHatch, 5f);
                return MakeTile("site", c);
            }
        }

        public static Tile Floor(RoomType type)
        {
            string key = "floor" + type;
            if (Tiles.TryGetValue(key, out var t)) return t;
            var c = new InkCanvas(PPU, PPU, (int)type + 11);
            switch (type)
            {
                case RoomType.Bathroom:
                case RoomType.LaundryRoom:
                    c.Clear(InkPalette.Wet);
                    for (int i = 0; i < PPU; i += 16) { c.Line(i + 0.5f, 0, i + 0.5f, PPU, InkPalette.WetLine, 1.4f); c.Line(0, i + 0.5f, PPU, i + 0.5f, InkPalette.WetLine, 1.4f); }
                    break;
                case RoomType.ScienceLab:
                case RoomType.ComputerLab:
                case RoomType.NurseOffice:
                    c.Clear(InkPalette.TileFill);
                    for (int i = 0; i < PPU; i += 16) { c.Line(i + 0.5f, 0, i + 0.5f, PPU, InkPalette.TileLine, 1.4f); c.Line(0, i + 0.5f, PPU, i + 0.5f, InkPalette.TileLine, 1.4f); }
                    break;
                case RoomType.Gym:
                    c.Clear(InkPalette.FloorBase);
                    for (int i = 0; i < PPU; i += 16)
                    {
                        c.Line(0, i + 0.5f, PPU, i + 0.5f, InkPalette.TileLine, 1.4f);
                        float j = (i / 16 % 2 == 0) ? 20f : 52f;
                        c.Line(j, i, j, i + 16, InkPalette.TileLine, 1.2f);
                    }
                    break;
                case RoomType.Classroom:
                    c.Clear(InkPalette.ClassroomFloor);
                    break;
                case RoomType.DormRoom:
                    c.Clear(InkPalette.DormFloor);
                    break;
                case RoomType.Cafeteria:
                    c.Clear(InkPalette.CafeteriaFloor);
                    break;
                case RoomType.Office:
                case RoomType.StaffLounge:
                case RoomType.SecurityRoom:
                    c.Clear(InkPalette.OfficeFloor);
                    break;
                case RoomType.Library:
                    c.Clear(InkPalette.Pale);
                    for (int y = 8; y < PPU; y += 16)
                        for (int x = 8; x < PPU; x += 16)
                            c.FillCircle(x, y, 1.3f, InkPalette.WithAlpha(InkPalette.Sky, 0.8f));
                    break;
                case RoomType.StudentLounge:
                case RoomType.GamingRoom:
                case RoomType.ClubRoom:
                    c.Clear(InkPalette.Hex(0xE0EDFB));
                    break;
                case RoomType.BasketballCourt:
                case RoomType.TennisCourt:
                    c.Clear(InkPalette.Hex(0xCFE3F8));
                    var rng = new System.Random(7);
                    for (int i = 0; i < 26; i++) c.FillCircle(rng.Next(PPU), rng.Next(PPU), 1.1f, InkPalette.WithAlpha(InkPalette.Mid, 0.35f));
                    break;
                case RoomType.SoccerField:
                case RoomType.Track:
                case RoomType.PracticeField:
                    c.Clear(InkPalette.Hex(0xDDEFFC));
                    c.FillRect(0, 0, PPU / 2f, PPU, InkPalette.Hex(0xD3E9FB));
                    break;
                case RoomType.Playground:
                {
                    // Soft rubber mulch
                    c.Clear(InkPalette.Hex(0xE8F2FC));
                    var mulch = new System.Random(11);
                    for (int i = 0; i < 40; i++)
                        c.FillCircle(mulch.Next(PPU), mulch.Next(PPU), 1.4f, InkPalette.WithAlpha(i % 3 == 0 ? InkPalette.Marker : InkPalette.Sky, 0.8f));
                    break;
                }
                case RoomType.Garden:
                case RoomType.PicnicArea:
                    c.Clear(InkPalette.Lawn);
                    for (int k = -PPU; k <= PPU * 2; k += 16) c.Line(k, 0f, k - PPU, PPU, InkPalette.LawnHatch, 2.4f);
                    break;
                case RoomType.SportsComplex:
                    c.Clear(InkPalette.FloorBase);
                    for (int i = 0; i < PPU; i += 16) c.Line(0, i + 0.5f, PPU, i + 0.5f, InkPalette.TileLine, 1.2f);
                    break;
                case RoomType.SwimmingPool:
                case RoomType.InnovationLab:
                    c.Clear(InkPalette.TileFill);
                    for (int i = 0; i < PPU; i += 16) { c.Line(i + 0.5f, 0, i + 0.5f, PPU, InkPalette.TileLine, 1.2f); c.Line(0, i + 0.5f, PPU, i + 0.5f, InkPalette.TileLine, 1.2f); }
                    break;
                case RoomType.Arena:
                    c.Clear(InkPalette.FloorBase);
                    for (int i = 0; i < PPU; i += 16) c.Line(0, i + 0.5f, PPU, i + 0.5f, InkPalette.TileLine, 1.2f);
                    break;
                case RoomType.ScholarsHall:
                case RoomType.Manor:
                    // Parquet: squares of three planks, turning a quarter each time
                    c.Clear(InkPalette.Hex(0xDCE9F8));
                    for (int y = 0; y < PPU; y += 16)
                        for (int x = 0; x < PPU; x += 16)
                            for (int k = 1; k < 4; k++)
                            {
                                if ((x / 16 + y / 16) % 2 == 0) c.Line(x + 1, y + k * 4, x + 15, y + k * 4, InkPalette.TileLine, 1.1f);
                                else c.Line(x + k * 4, y + 1, x + k * 4, y + 15, InkPalette.TileLine, 1.1f);
                            }
                    break;
                case RoomType.Clubhouse:
                case RoomType.Theatre:
                    // Carpet with a small repeating diamond
                    c.Clear(type == RoomType.Theatre ? InkPalette.Hex(0xD4E5F8) : InkPalette.Hex(0xE3EEFB));
                    for (int y = 8; y < PPU; y += 16)
                        for (int x = 8; x < PPU; x += 16)
                            c.FillTriangle(x - 2.5f, y, x + 2.5f, y, x, y + 3.5f, InkPalette.WithAlpha(InkPalette.Mid, 0.35f));
                    break;
                case RoomType.CommunityCentre:
                    c.Clear(InkPalette.FloorBase);
                    for (int y = 0; y < PPU; y += 16)
                        for (int x = (y / 16 % 2) * 16; x < PPU; x += 32)
                            c.FillRect(x, y, x + 16, y + 16, InkPalette.Pale);
                    break;
                case RoomType.InternationalHouse:
                    c.Clear(InkPalette.Hex(0xE6F1FC));
                    for (int y = 8; y < PPU; y += 16)
                        for (int x = 8; x < PPU; x += 16)
                            c.FillCircle(x, y, 1.6f, InkPalette.WithAlpha(InkPalette.Marker, 0.9f));
                    break;
                default:
                    c.Clear(InkPalette.FloorBase);
                    break;
            }
            return MakeTile(key, c);
        }

        // ---------------------------------------------------------------------------------
        // Sprites
        // ---------------------------------------------------------------------------------

        static Sprite MakeSprite(string key, InkCanvas canvas, Vector2 pivot)
        {
            if (Sprites.TryGetValue(key, out var existing)) return existing;
            var tex = canvas.ToTexture(true, key);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, PPU, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Sprites[key] = sprite;
            return sprite;
        }

        /// <summary>1x1 white sprite (one tile), for shadows and bars.</summary>
        public static Sprite Pixel
        {
            get
            {
                if (Sprites.TryGetValue("pixel", out var s)) return s;
                var c = new InkCanvas(8, 8);
                c.Clear(Color.white);
                var tex = c.ToTexture(false, "pixel");
                tex.filterMode = FilterMode.Point;
                s = Sprite.Create(tex, new Rect(0, 0, 8, 8), Vector2.zero, 8, 0, SpriteMeshType.FullRect);
                Sprites["pixel"] = s;
                return s;
            }
        }

        /// <summary>
        /// The drawing of a room at a level and orientation (walls, door, furniture). The pivot sits
        /// on the footprint's bottom-left corner, so place it at the room's (X, Y).
        /// </summary>
        public static Sprite Room(RoomType type, int level, int rotation)
        {
            string key = $"room_{type}_{level}_{rotation & 3}";
            if (Sprites.TryGetValue(key, out var s)) return s;
            var canvas = RoomPainter.Paint(type, level).Rotated(rotation);
            var pivot = new Vector2(RoomPad / (float)canvas.Width, RoomPad / (float)canvas.Height);
            return MakeSprite(key, canvas, pivot);
        }

        /// <summary>A construction site for a footprint of w x h tiles.</summary>
        public static Sprite ConstructionSite(int w, int h)
        {
            string key = $"site_{w}_{h}";
            if (Sprites.TryGetValue(key, out var s)) return s;
            var canvas = RoomPainter.PaintSite(w, h);
            return MakeSprite(key, canvas, new Vector2(RoomPad / (float)canvas.Width, RoomPad / (float)canvas.Height));
        }

        /// <summary>Marker-yellow highlight drawn around a selected room.</summary>
        public static Sprite Highlight(int w, int h)
        {
            string key = $"hl_{w}_{h}";
            if (Sprites.TryGetValue(key, out var s)) return s;
            int pad = RoomPad;
            var c = new InkCanvas(w * PPU + pad * 2, h * PPU + pad * 2, 5) { Wobble = 1.2f };
            c.StrokeRect(pad - 4, pad - 4, pad + w * PPU + 4, pad + h * PPU + 4, InkPalette.WithAlpha(InkPalette.Marker, 0.95f), 9f, 10f);
            return MakeSprite(key, c, new Vector2(pad / (float)c.Width, pad / (float)c.Height));
        }

        /// <summary>Dashed placement outline for the build ghost.</summary>
        public static Sprite GhostOutline(int w, int h, bool valid)
        {
            string key = $"ghost_{w}_{h}_{valid}";
            if (Sprites.TryGetValue(key, out var s)) return s;
            int pad = RoomPad;
            var c = new InkCanvas(w * PPU + pad * 2, h * PPU + pad * 2, 9);
            var col = valid ? InkPalette.Blue : InkPalette.CoralText;
            float x0 = pad, y0 = pad, x1 = pad + w * PPU, y1 = pad + h * PPU;
            const float lw = 5f, dash = 14f, gap = 9f;
            c.Line(x0, y0, x1, y0, col, lw, dash, gap);
            c.Line(x1, y0, x1, y1, col, lw, dash, gap);
            c.Line(x1, y1, x0, y1, col, lw, dash, gap);
            c.Line(x0, y1, x0, y0, col, lw, dash, gap);
            return MakeSprite(key, c, new Vector2(pad / (float)c.Width, pad / (float)c.Height));
        }

        /// <summary>A stone gate pillar with a ball on top, either side of the old estate's gate.</summary>
        public static Sprite GatePost
        {
            get
            {
                if (Sprites.TryGetValue("gatePost", out var s)) return s;
                var c = new InkCanvas(44, 44, 37) { Wobble = 0.6f };
                c.Box(6, 6, 38, 38, InkPalette.Hex(0xE9EFF7), 2.6f, 3f);
                c.Dot(22, 22, 9f, InkPalette.Pale, 2.2f);
                return MakeSprite("gatePost", c, new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>Little arrow marking where a room's door opens (build mode).</summary>
        public static Sprite DoorArrow(bool valid)
        {
            string key = "doorArrow" + valid;
            if (Sprites.TryGetValue(key, out var s)) return s;
            var c = new InkCanvas(PPU, PPU, 3);
            var col = valid ? InkPalette.Blue : InkPalette.CoralText;
            c.FillTriangle(32, 50, 14, 22, 50, 22, InkPalette.WithAlpha(col, 0.9f));
            c.StrokeTriangle(32, 50, 14, 22, 50, 22, InkPalette.Ink, 2.5f);
            return MakeSprite(key, c, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Dashed coral cross for closed rooms.</summary>
        public static Sprite ClosedCross(int w, int h)
        {
            string key = $"closed_{w}_{h}";
            if (Sprites.TryGetValue(key, out var s)) return s;
            int pad = RoomPad;
            var c = new InkCanvas(w * PPU + pad * 2, h * PPU + pad * 2, 4) { Wobble = 1f };
            float x0 = pad + 10, y0 = pad + 10, x1 = pad + w * PPU - 10, y1 = pad + h * PPU - 10;
            c.Line(x0, y0, x1, y1, InkPalette.WithAlpha(InkPalette.Coral, 0.85f), 6f, 16f, 8f);
            c.Line(x0, y1, x1, y0, InkPalette.WithAlpha(InkPalette.Coral, 0.85f), 6f, 16f, 8f);
            return MakeSprite(key, c, new Vector2(pad / (float)c.Width, pad / (float)c.Height));
        }

        /// <summary>A small paper card with an ink border and hard shadow (construction signs, price tags).</summary>
        public static Sprite Card(int widthPx, int heightPx)
        {
            string key = $"card_{widthPx}_{heightPx}";
            if (Sprites.TryGetValue(key, out var s)) return s;
            var c = new InkCanvas(widthPx + 8, heightPx + 10, 2) { Wobble = 0.6f };
            c.FillRect(6, 0, widthPx + 6, heightPx + 1, InkPalette.Ink, 8f);                // hard shadow
            c.FillRect(2, 5, widthPx + 2, heightPx + 5, InkPalette.Paper, 8f);
            c.StrokeRect(2, 5, widthPx + 2, heightPx + 5, InkPalette.Ink, 3f, 8f);
            return MakeSprite(key, c, new Vector2(0f, 1f));
        }

        /// <summary>Hand-drawn tree. Size 0 = bush, 1 = small tree, 2 = the big oak.</summary>
        public static Sprite Tree(int size, int variant)
        {
            string key = $"tree_{size}_{variant}";
            if (Sprites.TryGetValue(key, out var s)) return s;
            int px = size == 2 ? 230 : size == 1 ? 150 : 84;
            var c = new InkCanvas(px, px, 100 + variant * 7 + size) { Wobble = 1.4f };
            var rng = new System.Random(variant * 31 + size * 7 + 1);
            float cx = px / 2f, baseY = px * 0.2f;
            c.FillEllipse(cx + 4, baseY - 2, px * 0.34f, px * 0.07f, InkPalette.Hex(0xCBE0F6));
            if (size > 0)
            {
                float tw = px * 0.07f;
                c.FillRect(cx - tw, baseY, cx + tw, baseY + px * 0.28f, Color.white, 3f);
                c.StrokeRect(cx - tw, baseY, cx + tw, baseY + px * 0.28f, InkPalette.Ink, 2.4f, 3f);
            }

            int blobs = size == 2 ? 6 : size == 1 ? 4 : 3;
            var circles = new List<Vector3>();
            float crownY = size == 0 ? px * 0.42f : px * 0.6f;
            float spread = px * (size == 0 ? 0.18f : 0.2f);
            for (int i = 0; i < blobs; i++)
            {
                float a = i / (float)blobs * Mathf.PI * 2f + (float)rng.NextDouble() * 0.6f;
                float r = px * (size == 0 ? 0.2f : 0.17f) * (0.85f + (float)rng.NextDouble() * 0.3f);
                circles.Add(new Vector3(cx + Mathf.Cos(a) * spread, crownY + Mathf.Sin(a) * spread * 0.8f, r));
            }
            circles.Add(new Vector3(cx, crownY, px * (size == 0 ? 0.22f : 0.19f)));

            var foliage = size == 0 ? InkPalette.Hedge : InkPalette.Foliage;
            // Thick ink strokes first, then the fills on top, so only the outer outline shows.
            foreach (var b in circles) c.FillCircle(b.x, b.y, b.z + 3.2f, InkPalette.Ink);
            foreach (var b in circles) c.FillCircle(b.x, b.y, b.z, foliage);
            c.Wobble = 0f;
            foreach (var b in circles)
                if (rng.NextDouble() < 0.6) c.Arc(b.x - b.z * 0.1f, b.y + b.z * 0.1f, b.z * 0.62f, 1.9f, 2.9f, Color.white, 2.6f);
            if (size == 2)
                for (int i = 0; i < 3; i++)
                {
                    var b = circles[rng.Next(circles.Count)];
                    c.FillEllipse(b.x + b.z * 0.2f, b.y - b.z * 0.2f, 5f, 6f, InkPalette.Marker);
                    c.StrokeEllipse(b.x + b.z * 0.2f, b.y - b.z * 0.2f, 5f, 6f, InkPalette.Ink, 1.5f);
                }
            return MakeSprite(key, c, new Vector2(0.5f, baseY / px));
        }
    }
}
