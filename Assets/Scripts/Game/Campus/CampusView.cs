using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SchoolSim.Game
{
    public enum CampusOverlay { None, Condition, Cleanliness, Occupancy }

    /// <summary>
    /// Draws the campus from the simulation: Tilemaps for land, floors, heat maps and build-mode
    /// grids; sprites for rooms, trees, signs, highlights and the placement ghost; and across the
    /// road, the old estate (parkland, its wall, gate and boarded-up manor until it opens). Redraws
    /// when the campus map's version changes; construction signs and heat maps refresh continuously.
    /// </summary>
    public sealed class CampusView : MonoBehaviour
    {
        public static CampusView Instance { get; private set; }

        // Sorting orders, back to front.
        const int OrderGround = -100, OrderGroundEdge = -99, OrderTree = -70, OrderShadow = -60, OrderFloor = -40,
            OrderHeat = -35, OrderRoom = -20, OrderCross = -18, OrderPeople = -16, OrderHover = -12, OrderSelect = -11, OrderLabel = 10,
            OrderSign = 12, OrderBuildGrid = 20, OrderGhost = 21, OrderGhostLine = 22, OrderGhostText = 24, OrderLand = 26;

        Grid _grid;
        Tilemap _ground, _groundEdge, _floors, _heat, _buildGrid, _ghostTiles, _landTiles;
        TilemapRenderer _buildGridRenderer;
        Transform _roomRoot, _treeRoot, _landRoot, _estateRoot;
        TextMesh _estateNote;
        int _estateDrawn = -1;               // 1 when the estate scenery shows it open, 0 closed
        bool _estateForSale;

        readonly Dictionary<int, RoomVisual> _rooms = new Dictionary<int, RoomVisual>();
        readonly List<TreeDecor> _trees = new List<TreeDecor>();
        readonly List<LandTag> _landTags = new List<LandTag>();

        Simulation _sim;
        int _version = -1;
        bool[] _ownedCache;
        float _heatTimer;
        CampusOverlay _drawnOverlay;

        SpriteRenderer _ghostOutline, _ghostDoor, _select, _hover;
        TextMesh _ghostLabel, _ghostNote;
        TileRect _ghostRect;
        bool _ghostValid, _ghostShown;
        int _landVersion = -1, _landHoverX = -99, _landHoverY = -99;

        public CampusOverlay Overlay { get; set; }

        sealed class RoomVisual
        {
            public GameObject Root;
            public SpriteRenderer Body, Shadow, Cross;
            public TextMesh Label;
            public GameObject Sign;
            public Transform SignFill;
            public TextMesh SignTitle, SignDays;
            public string Key;
        }

        sealed class TreeDecor
        {
            public GameObject Go;
            public int X, Y, Radius;
        }

        sealed class LandTag
        {
            public GameObject Root;
            public SpriteRenderer Outline;
            public TextMesh Price;
            public int Px, Py;
        }

        // ---------------------------------------------------------------------------------
        // Setup
        // ---------------------------------------------------------------------------------

        void Awake()
        {
            Instance = this;
            var gridGo = new GameObject("CampusGrid");
            gridGo.transform.SetParent(transform, false);
            _grid = gridGo.AddComponent<Grid>();
            _grid.cellSize = Vector3.one;

            _ground = MakeTilemap("Ground", OrderGround, out _);
            _groundEdge = MakeTilemap("GroundEdge", OrderGroundEdge, out _);
            _floors = MakeTilemap("Floors", OrderFloor, out _);
            _heat = MakeTilemap("Heat", OrderHeat, out _);
            _buildGrid = MakeTilemap("BuildGrid", OrderBuildGrid, out _buildGridRenderer);
            _ghostTiles = MakeTilemap("Ghost", OrderGhost, out _);
            _landTiles = MakeTilemap("Land", OrderGhost, out _);
            _buildGridRenderer.enabled = false;

            _roomRoot = new GameObject("Rooms").transform;
            _roomRoot.SetParent(transform, false);
            _treeRoot = new GameObject("Trees").transform;
            _treeRoot.SetParent(transform, false);
            _landRoot = new GameObject("LandTags").transform;
            _landRoot.SetParent(transform, false);
            _estateRoot = new GameObject("Estate").transform;
            _estateRoot.SetParent(transform, false);

            _ghostOutline = MakeSprite(transform, "GhostOutline", OrderGhostLine);
            _ghostDoor = MakeSprite(transform, "GhostDoor", OrderGhostLine);
            _select = MakeSprite(transform, "Selection", OrderSelect);
            _hover = MakeSprite(transform, "Hover", OrderHover);
            _hover.color = new Color(1f, 1f, 1f, 0.45f);
            _ghostLabel = MakeText(transform, "GhostLabel", 0.34f, InkPalette.Ink, TextAnchor.UpperLeft, OrderGhostText);
            _ghostNote = MakeText(transform, "GhostNote", 0.42f, InkPalette.DeepBlue, TextAnchor.MiddleLeft, OrderGhostText);
            HideGhost();
            SetSelected(null);
            SetHover(null);
        }

        Tilemap MakeTilemap(string name, int order, out TilemapRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_grid.transform, false);
            var tm = go.AddComponent<Tilemap>();
            renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            return tm;
        }

        static SpriteRenderer MakeSprite(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>World-space text. <paramref name="height"/> is roughly the cap height in tiles.</summary>
        public static TextMesh MakeText(Transform parent, string name, float height, Color color, TextAnchor anchor, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = CampusArt.Font;
            tm.fontSize = 64;
            tm.characterSize = height / (tm.fontSize * 0.1f);
            tm.anchor = anchor;
            tm.color = color;
            tm.richText = false;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            mr.sortingOrder = order;
            return tm;
        }

        /// <summary>Width of a string in world units for a TextMesh made by <see cref="MakeText"/>.</summary>
        public static float MeasureWidth(TextMesh tm, string text)
        {
            var font = tm.font;
            font.RequestCharactersInTexture(text, tm.fontSize, tm.fontStyle);
            float px = 0f;
            foreach (char ch in text)
                if (font.GetCharacterInfo(ch, out var info, tm.fontSize, tm.fontStyle)) px += info.advance;
            return px * tm.characterSize * 0.1f * tm.transform.lossyScale.x;
        }

        // ---------------------------------------------------------------------------------
        // Frame
        // ---------------------------------------------------------------------------------

        void LateUpdate()
        {
            var sim = GameController.Instance != null ? GameController.Instance.Sim : null;
            if (sim == null) return;

            if (sim != _sim)
            {
                _sim = sim;
                _version = -1;
                _ownedCache = null;
                foreach (var v in _rooms.Values) Destroy(v.Root);
                _rooms.Clear();
                foreach (var t in _trees) Destroy(t.Go);
                _trees.Clear();
                foreach (Transform child in _estateRoot) Destroy(child.gameObject);
                _estateDrawn = -1;
                FillBuildGrid();
            }

            var map = _sim.State.Campus;
            if (map.Version != _version)
            {
                _version = map.Version;
                if (OwnershipChanged(map))
                {
                    DrawGround(map);
                    if (_trees.Count == 0) PlantTrees(map);
                    DrawEstate(map);
                }
                DrawRooms();
                UpdateTreeVisibility(map);
                _heatTimer = 0f;
            }

            UpdateSigns();
            UpdateEstateSign(map);
            _heatTimer -= GameClock.Delta;
            if (_heatTimer <= 0f || Overlay != _drawnOverlay)
            {
                _heatTimer = 0.5f;
                DrawHeat();
            }
        }

        bool OwnershipChanged(CampusMap map)
        {
            int n = map.ParcelsX * map.ParcelsY;
            if (_ownedCache == null || _ownedCache.Length != n)
            {
                _ownedCache = new bool[n];
                for (int i = 0; i < n; i++) _ownedCache[i] = map.IsParcelOwned(i % map.ParcelsX, i / map.ParcelsX);
                return true;
            }
            bool changed = false;
            for (int i = 0; i < n; i++)
            {
                bool owned = map.IsParcelOwned(i % map.ParcelsX, i / map.ParcelsX);
                if (owned != _ownedCache[i]) { _ownedCache[i] = owned; changed = true; }
            }
            return changed;
        }

        void FillBuildGrid()
        {
            var map = _sim.State.Campus;
            _buildGrid.ClearAllTiles();
            var pos = new Vector3Int[map.Width * map.Height];
            var tiles = new TileBase[pos.Length];
            for (int y = 0, i = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++, i++)
                {
                    pos[i] = new Vector3Int(x, y, 0);
                    tiles[i] = CampusArt.BuildGrid;
                }
            _buildGrid.SetTiles(pos, tiles);
        }

        public bool BuildGridVisible
        {
            get => _buildGridRenderer.enabled;
            set => _buildGridRenderer.enabled = value;
        }

        // ---------------------------------------------------------------------------------
        // Land
        // ---------------------------------------------------------------------------------

        void DrawGround(CampusMap map)
        {
            int n = map.Width * map.Height;
            var pos = new Vector3Int[n];
            var ground = new TileBase[n];
            var edges = new TileBase[n];
            for (int y = 0, i = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++, i++)
                {
                    pos[i] = new Vector3Int(x, y, 0);
                    bool owned = map.IsOwned(x, y);
                    ground[i] = IsDrive(map, x, y) ? CampusArt.Gravel : owned ? CampusArt.Lawn : map.IsEstate(x, y) ? CampusArt.Meadow : CampusArt.Paper;
                    if (!owned)
                    {
                        if (map.IsEstate(x, y) && !map.EstateOpen) edges[i] = EstateWall(map, x, y);
                        continue;
                    }
                    int mask = (map.IsOwned(x, y + 1) ? 0 : 1) | (map.IsOwned(x + 1, y) ? 0 : 2)
                             | (map.IsOwned(x, y - 1) ? 0 : 4) | (map.IsOwned(x - 1, y) ? 0 : 8);
                    if (mask != 0) edges[i] = CampusArt.LawnEdge(mask);
                }
            }
            _ground.SetTiles(pos, ground);
            _groundEdge.ClearAllTiles();
            _groundEdge.SetTiles(pos, edges);
        }

        /// <summary>Deterministic scatter of trees and bushes, plus the school's namesake oak and the old estate's oaks.</summary>
        void PlantTrees(CampusMap map)
        {
            var rng = new System.Random(_sim.State.SchoolName.GetHashCode() & 0x7FFFFFFF);
            int estateX = map.EstateParcelX * CampusMap.ParcelSize;
            AddTree(23, 11, 2, 0, 2);
            for (int attempt = 0; attempt < 900; attempt++)
            {
                int x = rng.Next(1, estateX - 1), y = rng.Next(1, map.Height - 1);
                bool owned = map.IsOwned(x, y);
                if (owned && rng.NextDouble() < 0.75) continue;          // mostly outside the campus
                if (!TreeRoom(x, y, 3)) continue;
                int size = rng.NextDouble() < 0.35 ? 0 : 1;
                AddTree(x, y, size, rng.Next(4), size == 0 ? 0 : 1);
                if (_trees.Count > 80) break;
            }
            if (estateX >= map.Width) return;

            // The old estate: two oaks flanking the drive and old trees across the park, clear of the
            // manor, the drive and the sign by the gate.
            var def = RoomCatalog.Get(RoomType.Manor);
            AddTree(estateX + 4, map.ManorY - 5, 2, 1, 2);
            AddTree(estateX + 7, map.ManorY + 2, 2, 2, 2);
            var manor = new TileRect(map.ManorX - 3, map.ManorY - 6, def.Width + 6, def.Height + 8);
            var gate = new TileRect(estateX, map.ManorY - 4, map.ManorX - estateX, 9);
            var estate = new System.Random(rng.Next());
            for (int attempt = 0, planted = 0; attempt < 400 && planted < 26; attempt++)
            {
                int x = estate.Next(estateX + 2, map.Width - 2), y = estate.Next(2, map.Height - 2);
                if (manor.Contains(x, y) || gate.Contains(x, y) || !TreeRoom(x, y, 4)) continue;
                AddTree(x, y, estate.NextDouble() < 0.5 ? 2 : 1, estate.Next(4), 2);
                planted++;
            }
        }

        /// <summary>No other tree within <paramref name="gap"/> tiles.</summary>
        bool TreeRoom(int x, int y, int gap)
        {
            foreach (var t in _trees)
                if (Mathf.Abs(t.X - x) < gap && Mathf.Abs(t.Y - y) < gap) return false;
            return true;
        }

        /// <summary>The gravel drive from the estate's gate to the manor's door.</summary>
        static bool IsDrive(CampusMap map, int x, int y) =>
            map.IsEstate(x, y) && x <= map.ManorX + RoomCatalog.Get(RoomType.Manor).Width / 2 && (y == map.ManorY - 1 || y == map.ManorY - 2);

        /// <summary>The old wall round the closed estate, with a gap for the gate where the drive comes in.</summary>
        static TileBase EstateWall(CampusMap map, int x, int y)
        {
            bool gate = y == map.ManorY - 1 || y == map.ManorY - 2;
            int mask = (y == map.Height - 1 ? 1 : 0) | (x == map.Width - 1 ? 2 : 0) | (y == 0 ? 4 : 0)
                       | (x == map.EstateParcelX * CampusMap.ParcelSize && !gate ? 8 : 0);
            return mask != 0 ? CampusArt.EstateWall(mask) : null;
        }

        /// <summary>
        /// Until the estate is bought, the boarded-up manor stands behind the gate pillars with a sign
        /// for passers-by. Once it's ours the manor is a real room and the scenery goes.
        /// </summary>
        void DrawEstate(CampusMap map)
        {
            int open = map.EstateOpen || EstateOwned(map) ? 1 : 0;
            if (open == _estateDrawn || map.EstateParcelX >= map.ParcelsX) return;
            _estateDrawn = open;
            foreach (Transform child in _estateRoot) Destroy(child.gameObject);
            _estateNote = null;
            if (open == 1) return;

            var def = RoomCatalog.Get(RoomType.Manor);
            var shadow = MakeSprite(_estateRoot, "ManorShadow", OrderShadow);
            shadow.sprite = CampusArt.Pixel;
            shadow.color = InkPalette.Shadow;
            shadow.transform.position = new Vector3(map.ManorX + 0.13f, map.ManorY - 0.16f, 0f);
            shadow.transform.localScale = new Vector3(def.Width, def.Height, 1f);
            var manor = MakeSprite(_estateRoot, "Manor", OrderRoom);
            manor.sprite = CampusArt.Room(RoomType.Manor, 1, Placement.South);
            manor.transform.position = new Vector3(map.ManorX, map.ManorY, 0f);

            float gateX = map.EstateParcelX * CampusMap.ParcelSize + 0.25f;
            foreach (float y in new[] { map.ManorY - 2.3f, map.ManorY + 0.3f })
            {
                var post = MakeSprite(_estateRoot, "GatePost", OrderRoom + 1);
                post.sprite = CampusArt.GatePost;
                post.transform.position = new Vector3(gateX, y, 0f);
            }

            var sign = new GameObject("EstateSign");
            sign.transform.SetParent(_estateRoot, false);
            sign.transform.position = new Vector3(gateX + 0.9f, map.ManorY + 3.4f, 0f);
            var card = MakeSprite(sign.transform, "Card", OrderSign);
            card.sprite = CampusArt.Card(Mathf.RoundToInt(SignWidth * CampusArt.PPU) - 8, 84);
            var title = MakeText(sign.transform, "Title", 0.3f, InkPalette.Ink, TextAnchor.UpperLeft, OrderSign + 2);
            title.fontStyle = FontStyle.Bold;
            title.text = "The Old Estate";
            title.transform.localPosition = new Vector3(0.14f, -0.16f, 0f);
            _estateNote = MakeText(sign.transform, "Note", 0.24f, InkPalette.NoteInk, TextAnchor.UpperLeft, OrderSign + 2);
            _estateNote.transform.localPosition = new Vector3(0.14f, -0.62f, 0f);
        }

        /// <summary>Some of the estate is ours, however it came (the room gallery owns every parcel without opening it).</summary>
        static bool EstateOwned(CampusMap map)
        {
            for (int py = 0; py < map.ParcelsY; py++)
                for (int px = map.EstateParcelX; px < map.ParcelsX; px++)
                    if (map.IsParcelOwned(px, py)) return true;
            return false;
        }

        void UpdateEstateSign(CampusMap map)
        {
            if (_estateNote == null || map.EstateOpen) return;
            bool forSale = _sim.IsUnlocked(Feature.SecondCampus);
            if (forSale == _estateForSale && _estateNote.text.Length > 0) return;
            _estateForSale = forSale;
            _estateNote.text = forSale
                ? "for sale: " + MathUtil.Money(Balance.EstateOpenCost)
                : $"for sale at level {Balance.UnlockLevel(Feature.SecondCampus)}";
        }

        void AddTree(int x, int y, int size, int variant, int radius)
        {
            var sr = MakeSprite(_treeRoot, "Tree", OrderTree + 8 - (int)(y * 0.7f));   // lower trees draw in front
            sr.sprite = CampusArt.Tree(size, variant);
            sr.transform.position = new Vector3(x + 0.5f, y + 0.3f, 0f);
            _trees.Add(new TreeDecor { Go = sr.gameObject, X = x, Y = y, Radius = radius });
        }

        void UpdateTreeVisibility(CampusMap map)
        {
            foreach (var t in _trees)
            {
                bool blocked = false;
                for (int dy = -t.Radius; dy <= t.Radius && !blocked; dy++)
                    for (int dx = -t.Radius; dx <= t.Radius && !blocked; dx++)
                        if (map.RoomIdAt(t.X + dx, t.Y + dy) != 0) blocked = true;
                t.Go.SetActive(!blocked);
            }
        }

        // ---------------------------------------------------------------------------------
        // Rooms
        // ---------------------------------------------------------------------------------

        void DrawRooms()
        {
            int day = _sim.Day;
            _floors.ClearAllTiles();
            var pos = new List<Vector3Int>(1024);
            var tiles = new List<TileBase>(1024);
            var alive = new HashSet<int>();

            foreach (var r in _sim.State.Rooms)
            {
                alive.Add(r.Id);
                bool built = r.IsBuilt(day) || r.Def.Decor;    // decorations go straight in
                UpdateRoom(r, built, day);
                if (r.Def.Decor) continue;                      // they sit on the lawn, no floor
                var floor = built ? CampusArt.Floor(r.Type) : CampusArt.Site;
                var f = r.Footprint;
                for (int y = f.Y; y < f.YMax; y++)
                    for (int x = f.X; x < f.XMax; x++)
                    {
                        pos.Add(new Vector3Int(x, y, 0));
                        tiles.Add(floor);
                    }
            }
            _floors.SetTiles(pos.ToArray(), tiles.ToArray());

            var dead = new List<int>();
            foreach (var kv in _rooms) if (!alive.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (int id in dead)
            {
                Destroy(_rooms[id].Root);
                _rooms.Remove(id);
            }
        }

        void UpdateRoom(Room r, bool built, int day)
        {
            if (!_rooms.TryGetValue(r.Id, out var v))
            {
                v = new RoomVisual { Root = new GameObject("Room " + r.Id) };
                v.Root.transform.SetParent(_roomRoot, false);
                v.Shadow = MakeSprite(v.Root.transform, "Shadow", OrderShadow);
                v.Shadow.sprite = CampusArt.Pixel;
                v.Shadow.color = InkPalette.Shadow;
                v.Body = MakeSprite(v.Root.transform, "Body", OrderRoom);
                v.Cross = MakeSprite(v.Root.transform, "Closed", OrderCross);
                v.Label = MakeText(v.Root.transform, "Label", 0.36f, InkPalette.Ink, TextAnchor.LowerLeft, OrderLabel);
                v.Label.fontStyle = FontStyle.Bold;
                _rooms[r.Id] = v;
            }

            bool closed = r.IsClosed(day);
            var f = r.Footprint;
            string key = $"{r.Type}|{r.Level}|{r.Rotation}|{built}|{closed}|{r.X},{r.Y}|{r.Name}";
            v.Root.transform.position = new Vector3(f.X, f.Y, 0f);
            if (key != v.Key)
            {
                v.Key = key;
                v.Body.sprite = built ? CampusArt.Room(r.Type, r.Level, r.Rotation) : CampusArt.ConstructionSite(f.W, f.H);
                // Decorations stand among the people walking past them (see CampusPeople).
                v.Body.sortingOrder = r.Def.Decor ? OrderPeople : OrderRoom;
                v.Body.spriteSortPoint = SpriteSortPoint.Pivot;
                bool shadow = built && !r.Def.Outdoor;
                v.Shadow.enabled = shadow;
                v.Shadow.transform.localPosition = new Vector3(0.13f, -0.16f, 0f);
                v.Shadow.transform.localScale = new Vector3(f.W, f.H, 1f);
                v.Cross.enabled = closed;
                if (closed) v.Cross.sprite = CampusArt.ClosedCross(f.W, f.H);

                v.Label.gameObject.SetActive(built && !r.Def.Decor);   // construction signs carry the name instead
                v.Label.text = closed ? r.Name + " (closed)" : r.Name;
                v.Label.color = closed ? InkPalette.CoralText : InkPalette.Ink;
                LabelSpot(r, f, out var spot, out float fit);
                v.Label.transform.localPosition = spot;
                v.Label.transform.localScale = Vector3.one;
                float width = MeasureWidth(v.Label, v.Label.text);
                if (width > fit && width > 0.01f) v.Label.transform.localScale = Vector3.one * Mathf.Max(0.5f, fit / width);
            }

            bool needsSign = (!built || r.IsUpgrading) && !r.Def.Decor;
            if (needsSign && v.Sign == null) CreateSign(v, f);
            if (!needsSign && v.Sign != null)
            {
                Destroy(v.Sign);
                v.Sign = null;
            }
            if (v.Sign != null)
            {
                v.Sign.transform.localPosition = new Vector3(0.18f, f.H - 0.16f, 0f);
                string title = built ? $"Upgrading to Lv {r.PendingLevel}" : r.Name;
                if (v.SignTitle.text != title)
                {
                    v.SignTitle.text = title;
                    v.SignTitle.transform.localScale = Vector3.one;
                    float w = MeasureWidth(v.SignTitle, title);
                    if (w > SignInner) v.SignTitle.transform.localScale = Vector3.one * (SignInner / w);
                }
            }
        }

        /// <summary>
        /// Labels run along the wall nearest the viewer's reading position (the bottom). When the
        /// door is in that wall, the label takes the longer stretch beside it so the door swing
        /// stays clear; tiny rooms put it along the top instead.
        /// </summary>
        static void LabelSpot(Room r, TileRect f, out Vector3 position, out float maxWidth)
        {
            if ((r.Rotation & 3) != Placement.South)
            {
                position = new Vector3(0.2f, 0.16f, 0f);
                maxWidth = f.W - 0.4f;
                return;
            }
            float doorLeft = f.W / 2 + 0.5f - 0.45f, doorRight = f.W / 2 + 0.5f + 0.45f;
            float left = doorLeft - 0.3f, right = f.W - doorRight - 0.3f;
            if (Mathf.Max(left, right) < 1.3f)
            {
                position = new Vector3(0.2f, f.H - 0.55f, 0f);
                maxWidth = f.W - 0.4f;
            }
            else if (left >= right)
            {
                position = new Vector3(0.2f, 0.16f, 0f);
                maxWidth = left;
            }
            else
            {
                position = new Vector3(doorRight + 0.1f, 0.16f, 0f);
                maxWidth = right;
            }
        }

        const float SignWidth = 2.5f, SignInner = 2.2f;

        void CreateSign(RoomVisual v, TileRect f)
        {
            var sign = new GameObject("Sign");
            sign.transform.SetParent(v.Root.transform, false);
            var card = MakeSprite(sign.transform, "Card", OrderSign);
            card.sprite = CampusArt.Card(Mathf.RoundToInt(SignWidth * CampusArt.PPU) - 8, 84);
            v.SignTitle = MakeText(sign.transform, "Title", 0.3f, InkPalette.Ink, TextAnchor.UpperLeft, OrderSign + 2);
            v.SignTitle.fontStyle = FontStyle.Bold;
            v.SignTitle.transform.localPosition = new Vector3(0.14f, -0.16f, 0f);

            const float barY = -0.7f, barX = 0.16f;
            var barBack = MakeSprite(sign.transform, "BarBack", OrderSign + 1);
            barBack.sprite = CampusArt.Pixel;
            barBack.color = InkPalette.Ink;
            barBack.transform.localPosition = new Vector3(barX, barY, 0f);
            barBack.transform.localScale = new Vector3(SignInner, 0.17f, 1f);
            var barBg = MakeSprite(sign.transform, "BarBg", OrderSign + 2);
            barBg.sprite = CampusArt.Pixel;
            barBg.color = InkPalette.Paper;
            barBg.transform.localPosition = new Vector3(barX + 0.025f, barY + 0.025f, 0f);
            barBg.transform.localScale = new Vector3(SignInner - 0.05f, 0.12f, 1f);
            var fill = MakeSprite(sign.transform, "BarFill", OrderSign + 3);
            fill.sprite = CampusArt.Pixel;
            fill.color = InkPalette.Mid;
            fill.transform.localPosition = new Vector3(barX + 0.025f, barY + 0.025f, 0f);
            v.SignFill = fill.transform;

            v.SignDays = MakeText(sign.transform, "Days", 0.22f, InkPalette.NoteInk, TextAnchor.UpperLeft, OrderSign + 2);
            v.SignDays.transform.localPosition = new Vector3(barX, barY - 0.12f, 0f);
            v.Sign = sign;
        }

        void UpdateSigns()
        {
            int day = _sim.Day;
            foreach (var r in _sim.State.Rooms)
            {
                if (!_rooms.TryGetValue(r.Id, out var v) || v.Sign == null) continue;
                float p = r.ConstructionProgress(day);
                v.SignFill.localScale = new Vector3((SignInner - 0.05f) * p, 0.12f, 1f);
                int left = Mathf.Max(0, r.ConstructionDoneDay - day);
                v.SignDays.text = left == 1 ? "1 day left" : $"{left} days left";
            }
        }

        // ---------------------------------------------------------------------------------
        // Heat maps
        // ---------------------------------------------------------------------------------

        void DrawHeat()
        {
            _drawnOverlay = Overlay;
            _heat.ClearAllTiles();
            if (Overlay == CampusOverlay.None) return;
            var pos = new List<Vector3Int>(1024);
            var tiles = new List<TileBase>(1024);
            var colors = new List<Color>(1024);
            foreach (var r in _sim.State.Rooms)
            {
                if (!r.IsBuilt(_sim.Day)) continue;
                Color c;
                switch (Overlay)
                {
                    case CampusOverlay.Condition: c = Heat(r.Condition / 100f); break;
                    case CampusOverlay.Cleanliness: c = Heat(r.Cleanliness / 100f); break;
                    default:
                        if (r.Type != RoomType.DormRoom) continue;
                        float fill = _sim.Indices.ResidentsOf(r.Id).Count / (float)Mathf.Max(1, r.Capacity);
                        c = Color.Lerp(InkPalette.Pale, InkPalette.Blue, fill);
                        break;
                }
                c.a = 0.55f;
                var f = r.Footprint;
                for (int y = f.Y; y < f.YMax; y++)
                    for (int x = f.X; x < f.XMax; x++)
                    {
                        pos.Add(new Vector3Int(x, y, 0));
                        tiles.Add(CampusArt.White);
                        colors.Add(c);
                    }
            }
            _heat.SetTiles(pos.ToArray(), tiles.ToArray());
            for (int i = 0; i < pos.Count; i++) _heat.SetColor(pos[i], colors[i]);
        }

        /// <summary>Coral when bad, marker yellow in the middle, blue when good.</summary>
        static Color Heat(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? Color.Lerp(InkPalette.Coral, InkPalette.Marker, t * 2f) : Color.Lerp(InkPalette.Marker, InkPalette.Mid, (t - 0.5f) * 2f);
        }

        // ---------------------------------------------------------------------------------
        // Selection, hover, ghost, land
        // ---------------------------------------------------------------------------------

        public void SetSelected(Room r)
        {
            _select.enabled = r != null;
            if (r == null) return;
            var f = r.Footprint;
            _select.sprite = CampusArt.Highlight(f.W, f.H);
            _select.transform.position = new Vector3(f.X, f.Y, 0f);
        }

        public void SetHover(Room r)
        {
            _hover.enabled = r != null;
            if (r == null) return;
            var f = r.Footprint;
            _hover.sprite = CampusArt.Highlight(f.W, f.H);
            _hover.transform.position = new Vector3(f.X, f.Y, 0f);
        }

        public void ShowGhost(TileRect rect, int rotation, bool valid, string label, string note, bool door = true)
        {
            if (!_ghostShown || rect.X != _ghostRect.X || rect.Y != _ghostRect.Y || rect.W != _ghostRect.W || rect.H != _ghostRect.H || valid != _ghostValid)
            {
                _ghostTiles.ClearAllTiles();
                var tint = valid ? InkPalette.WithAlpha(InkPalette.Mid, 0.3f) : InkPalette.WithAlpha(InkPalette.Coral, 0.32f);
                for (int y = rect.Y; y < rect.YMax; y++)
                    for (int x = rect.X; x < rect.XMax; x++)
                    {
                        var p = new Vector3Int(x, y, 0);
                        _ghostTiles.SetTile(p, CampusArt.White);
                        _ghostTiles.SetColor(p, tint);
                    }
                _ghostRect = rect;
                _ghostValid = valid;
            }
            _ghostShown = true;

            _ghostOutline.enabled = true;
            _ghostOutline.sprite = CampusArt.GhostOutline(rect.W, rect.H, valid);
            _ghostOutline.transform.position = new Vector3(rect.X, rect.Y, 0f);

            Placement.DoorFront(rect, rotation, out int dx, out int dy);
            _ghostDoor.enabled = door;
            _ghostDoor.sprite = CampusArt.DoorArrow(valid);
            _ghostDoor.transform.position = new Vector3(dx + 0.5f, dy + 0.5f, 0f);
            float angle = (rotation & 3) switch { Placement.South => 180f, Placement.East => -90f, Placement.West => 90f, _ => 0f };
            _ghostDoor.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            _ghostLabel.gameObject.SetActive(true);
            _ghostLabel.text = label;
            _ghostLabel.transform.position = new Vector3(rect.X + 0.15f, rect.YMax - 0.12f, 0f);

            _ghostNote.gameObject.SetActive(!string.IsNullOrEmpty(note));
            _ghostNote.text = note ?? "";
            _ghostNote.color = valid ? InkPalette.DeepBlue : InkPalette.CoralDark;
            _ghostNote.transform.position = new Vector3(rect.XMax + 0.35f, rect.Y + rect.H * 0.5f, 0f);
            _ghostNote.transform.rotation = Quaternion.Euler(0f, 0f, 3f);
        }

        public void HideGhost()
        {
            if (_ghostShown) _ghostTiles.ClearAllTiles();
            _ghostShown = false;
            _ghostOutline.enabled = false;
            _ghostDoor.enabled = false;
            _ghostLabel.gameObject.SetActive(false);
            _ghostNote.gameObject.SetActive(false);
        }

        /// <summary>Shows price tags on every parcel for sale (the estate's at its own prices); highlights the hovered one.</summary>
        public void ShowLand(bool show, int hoverX, int hoverY)
        {
            _landRoot.gameObject.SetActive(show);
            if (!show)
            {
                if (_landVersion != -1) _landTiles.ClearAllTiles();
                _landVersion = -1;
                return;
            }
            var map = _sim.State.Campus;
            int size = CampusMap.ParcelSize;
            if (_landVersion != map.Version)
            {
                _landVersion = map.Version;
                _landHoverX = _landHoverY = -99;
                int used = 0;
                for (int py = 0; py < map.ParcelsY; py++)
                    for (int px = 0; px < map.ParcelsX; px++)
                    {
                        if (!map.IsParcelPurchasable(px, py)) continue;
                        if (used >= _landTags.Count)
                        {
                            var root = new GameObject("Parcel");
                            root.transform.SetParent(_landRoot, false);
                            var outline = MakeSprite(root.transform, "Outline", OrderLand);
                            var text = MakeText(root.transform, "Price", 0.55f, InkPalette.DeepBlue, TextAnchor.MiddleCenter, OrderLand + 1);
                            _landTags.Add(new LandTag { Root = root, Outline = outline, Price = text });
                        }
                        var tag = _landTags[used++];
                        tag.Root.SetActive(true);
                        tag.Root.transform.position = new Vector3(px * size, py * size, 0f);
                        tag.Outline.sprite = CampusArt.GhostOutline(size, size, true);
                        tag.Price.transform.localPosition = new Vector3(size / 2f, size / 2f, 0f);
                        tag.Px = px;
                        tag.Py = py;
                    }
                for (int i = used; i < _landTags.Count; i++) _landTags[i].Root.SetActive(false);
            }
            foreach (var tag in _landTags)
                if (tag.Root.activeSelf) tag.Price.text = MathUtil.Money(_sim.Facilities.ParcelCost(tag.Px, tag.Py));

            if (hoverX != _landHoverX || hoverY != _landHoverY)
            {
                _landHoverX = hoverX;
                _landHoverY = hoverY;
                _landTiles.ClearAllTiles();
                if (map.ParcelInBounds(hoverX, hoverY))
                {
                    var tint = map.IsParcelPurchasable(hoverX, hoverY)
                        ? InkPalette.WithAlpha(InkPalette.Mid, 0.25f)
                        : InkPalette.WithAlpha(InkPalette.Coral, 0.15f);
                    if (map.IsParcelOwned(hoverX, hoverY)) return;
                    for (int y = hoverY * size; y < (hoverY + 1) * size; y++)
                        for (int x = hoverX * size; x < (hoverX + 1) * size; x++)
                        {
                            var p = new Vector3Int(x, y, 0);
                            _landTiles.SetTile(p, CampusArt.White);
                            _landTiles.SetColor(p, tint);
                        }
                }
            }
        }

        /// <summary>World-space centre of the owned land, for framing the camera.</summary>
        public Vector2 CampusCenter()
        {
            if (_sim == null) return Vector2.zero;
            _sim.Facilities.GetCampusCenter(out float cx, out float cy);
            return new Vector2(cx, cy);
        }

        public Vector2 MapSize => _sim == null ? new Vector2(70, 50) : new Vector2(_sim.State.Campus.Width, _sim.State.Campus.Height);
    }
}
