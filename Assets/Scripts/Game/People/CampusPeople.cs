using System;
using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>
    /// The students and staff you see on the campus. Each figure is a real person from the
    /// simulation: kids wear their grade's colour and show their mood on their face, go to class,
    /// eat, rest in their own dorm, run around playgrounds and fields, sit in gardens and on
    /// benches; staff go where their job is. Purely visual: nothing here changes the simulation.
    /// They walk on free land with A* paths, enter rooms through their doors, and now and then
    /// think out loud in a little bubble. Click one to select them.
    /// </summary>
    public sealed class CampusPeople : MonoBehaviour
    {
        public static CampusPeople Instance { get; private set; }

        const int MaxStudents = 64, MaxStaff = 18, SpawnsPerSync = 4;
        const int OrderShadow = -17, OrderPerson = -16, OrderBubble = 15;
        const float KidSpeed = 1.7f, AdultSpeed = 1.35f, RunSpeed = 2.7f;

        enum Act { Idle, Walk, Inside, Play }
        enum Goal { None, Enter, Play, Bench, Wander }

        sealed class Npc
        {
            public long Key;
            public int Id;
            public bool Staff;
            public GameObject Go;
            public SpriteRenderer Body, Shadow, Bubble;
            public Vector2 Pos;
            public readonly List<Vector2> Path = new List<Vector2>();
            public int PathIndex;
            public Act Act;
            public Goal Goal;
            public float Timer;
            public int RoomId = -1;
            public bool Sitting, Running, Left;
            public float Speed, Phase, FrameTimer;
            public int Frame;
            public PersonLook Look;
            public Expression Face;
            public float FaceCheck, BubbleUntil, NextBubble;
            public bool Leaving;
        }

        sealed class Ball
        {
            public SpriteRenderer Sprite;
            public Vector2 From, To;
            public float T, Duration;
            public int AreaId;
        }

        readonly Dictionary<long, Npc> _npcs = new Dictionary<long, Npc>();
        readonly List<Npc> _list = new List<Npc>();
        readonly Dictionary<int, Ball> _balls = new Dictionary<int, Ball>();
        readonly Dictionary<int, List<Npc>> _players = new Dictionary<int, List<Npc>>();
        readonly List<int> _gone = new List<int>();
        readonly System.Random _rng = new System.Random(4242);
        Simulation _sim;
        Transform _root;
        SpriteRenderer _ring, _hoverRing;
        float _syncTimer;
        bool _cameraReady;

        // Walkable grid: free tiles of owned land. Crossing the road to the old estate may take
        // them over land the school doesn't own (any free tile).
        bool[] _walk, _roam;
        bool _crossing;
        int _w, _h, _gridVersion = -1;

        public long Selected { get; private set; } = -1;

        public static long StaffKey(int id) => (1L << 32) | (uint)id;
        public static bool IsStaff(long key) => key >= 0 && (key >> 32) == 1;
        public static int IdOf(long key) => (int)(key & 0xFFFFFFFF);

        void Awake()
        {
            Instance = this;
            _root = new GameObject("People").transform;
            _root.SetParent(transform, false);
            _ring = MakeRenderer(_root, "SelectionRing", OrderShadow);
            _ring.sprite = PeopleSprites.Ring;
            _ring.enabled = false;
            _hoverRing = MakeRenderer(_root, "HoverRing", OrderShadow);
            _hoverRing.sprite = PeopleSprites.Ring;
            _hoverRing.color = new Color(1f, 1f, 1f, 0.45f);
            _hoverRing.enabled = false;

            InkIcons.Define("zzz", 2.2f, "M5 6h5l-5 6h5", "M12 10h4l-4 5h4", "M17.5 14.5h3l-3 4h3");
            InkIcons.Define("sweat", 1.8f, "fill:#A8D1F7 M12 4c3 4.2 5 7.4 5 9.8a5 5 0 0 1-10 0c0-2.4 2-5.6 5-9.8z", "bare fill:#FFFFFF M10.4 13.4a1.6 2.2 0 0 0 1.4 2.6");
            InkIcons.Define("ballicon", 2f, "fill:#FFFFFF circle:12,12,8", "ink circle:12,12,2.6", "M12 4v5.4M4.4 9.6l5.2 1.4M19.6 9.6l-5.2 1.4");
        }

        /// <summary>People lower on the screen draw over the people behind them.</summary>
        void SetUpCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = new Vector3(0f, 1f, 0f);
            _cameraReady = true;
        }

        // --- Public API ---------------------------------------------------------------------

        /// <summary>The person under a world position, front-most first.</summary>
        public bool TryPick(Vector2 world, out long key)
        {
            key = -1;
            float bestY = float.MaxValue;
            foreach (var n in _list)
            {
                if (!n.Go.activeSelf) continue;
                float h = n.Staff ? 1.05f : 0.95f;
                if (world.x < n.Pos.x - 0.34f || world.x > n.Pos.x + 0.34f || world.y < n.Pos.y - 0.05f || world.y > n.Pos.y + h) continue;
                if (n.Pos.y < bestY)
                {
                    bestY = n.Pos.y;
                    key = n.Key;
                }
            }
            return key >= 0;
        }

        public void Select(long key)
        {
            Selected = key;
            _ring.enabled = key >= 0 && _npcs.ContainsKey(key);
            if (key >= 0 && _npcs.TryGetValue(key, out var n)) n.Bubble.enabled = false;
        }

        /// <summary>Everyone standing inside a room right now, students and staff (the room card's "who's here").</summary>
        public void PeopleIn(Room room, List<long> into)
        {
            into.Clear();
            if (room == null) return;
            var f = room.Footprint;
            foreach (var n in _list)
            {
                if (!n.Go.activeSelf) continue;
                if (n.Pos.x >= f.X && n.Pos.x < f.X + f.W && n.Pos.y >= f.Y && n.Pos.y < f.Y + f.H) into.Add(n.Key);
            }
        }

        public bool TryGetPosition(long key, out Vector2 pos)
        {
            if (_npcs.TryGetValue(key, out var n))
            {
                pos = n.Pos;
                return true;
            }
            pos = default;
            return false;
        }

        /// <summary>What the selected person is up to, for the HUD card.</summary>
        public string Doing(long key)
        {
            if (!_npcs.TryGetValue(key, out var n) || _sim == null) return "";
            var room = n.RoomId >= 0 ? _sim.State.FindRoom(n.RoomId) : null;
            switch (n.Act)
            {
                case Act.Play: return room != null ? $"playing at the {room.Def.Name.ToLowerInvariant()}" : "playing";
                case Act.Inside:
                    if (room == null) return "hanging around";
                    if (n.Goal == Goal.Bench) return "sitting on a bench";
                    return room.Type switch
                    {
                        RoomType.Classroom => "in class",
                        RoomType.Library => "studying in the library",
                        RoomType.Cafeteria => "eating in the cafeteria",
                        RoomType.PicnicArea => "having a picnic",
                        RoomType.DormRoom => n.Staff ? "checking the dorms" : "resting in their room",
                        RoomType.Garden => "relaxing in the garden",
                        _ => "in the " + room.Def.Name.ToLowerInvariant(),
                    };
                case Act.Walk:
                    if (room != null) return "on the way to the " + room.Def.Name.ToLowerInvariant();
                    return n.Goal == Goal.Bench ? "looking for a bench" : "strolling around";
                default: return "taking a breather";
            }
        }

        // --- Frame ------------------------------------------------------------------------

        void Update()
        {
            var game = GameController.Instance;
            var sim = game != null ? game.Sim : null;
            if (sim != _sim)
            {
                ClearAll();
                _sim = sim;
                _gridVersion = -1;
            }
            _root.gameObject.SetActive(_sim != null && game.Phase == GamePhase.Playing);
            if (_sim == null || game.Phase != GamePhase.Playing) return;

            if (!_cameraReady) SetUpCamera();
            var map = _sim.State.Campus;
            if (map.Version != _gridVersion) RebuildGrid(map);

            _syncTimer -= GameClock.Delta;
            if (_syncTimer <= 0f)
            {
                _syncTimer = 0.5f;
                Sync();
            }

            float dt = Mathf.Min(GameClock.Delta, 0.1f);
            foreach (var n in _list) Step(n, dt);
            UpdateBalls(dt);

            if (Selected >= 0 && _npcs.TryGetValue(Selected, out var sel))
            {
                _ring.enabled = true;
                _ring.transform.position = new Vector3(sel.Pos.x, sel.Pos.y, 0f);
            }
            else _ring.enabled = false;

            var ctl = CampusController.Instance;
            long hover = ctl != null && ctl.Tool == CampusTool.Inspect ? ctl.HoverPerson : -1;
            if (hover >= 0 && hover != Selected && _npcs.TryGetValue(hover, out var hovered))
            {
                _hoverRing.enabled = true;
                _hoverRing.transform.position = new Vector3(hovered.Pos.x, hovered.Pos.y, 0f);
            }
            else _hoverRing.enabled = false;
        }

        void ClearAll()
        {
            foreach (var n in _list) Destroy(n.Go);
            _list.Clear();
            _npcs.Clear();
            foreach (var b in _balls.Values) Destroy(b.Sprite.gameObject);
            _balls.Clear();
            Selected = -1;
        }

        /// <summary>Spawns people who should be on campus and sends home those who shouldn't.</summary>
        void Sync()
        {
            var s = _sim.State;
            bool summer = Calendar.IsSummer(s.Day);
            var wanted = new HashSet<long>();
            int students = 0;
            foreach (var st in s.Students)
            {
                if (st.IsSick(s.Day)) continue;
                if (summer && (uint)(st.Id * 2654435761u) % 10u >= 3u) continue;   // most go home for the summer
                wanted.Add(st.Id);
                if (++students >= MaxStudents) break;
            }
            int staff = 0;
            foreach (var m in s.Staff)
            {
                if (!m.IsActive(s.Day)) continue;
                wanted.Add(StaffKey(m.Id));
                if (++staff >= MaxStaff) break;
            }

            for (int i = _list.Count - 1; i >= 0; i--)
            {
                var n = _list[i];
                if (wanted.Contains(n.Key)) continue;
                Destroy(n.Go);
                _npcs.Remove(n.Key);
                _list.RemoveAt(i);
                if (Selected == n.Key) Selected = -1;
            }

            int spawned = 0;
            foreach (long key in wanted)
            {
                if (_npcs.ContainsKey(key)) continue;
                if (spawned++ >= SpawnsPerSync) break;   // a few at a time, so sprites are painted gradually
                Spawn(key);
            }
        }

        void Spawn(long key)
        {
            bool staff = IsStaff(key);
            int id = IdOf(key);
            var s = _sim.State;
            var n = new Npc { Key = key, Id = id, Staff = staff };
            if (staff)
            {
                var m = s.FindStaff(id);
                if (m == null) return;
                n.Look = PersonLook.ForStaff(m);
                n.Face = Expressions.For(m);
            }
            else
            {
                var st = s.FindStudent(id);
                if (st == null) return;
                n.Look = PersonLook.ForStudent(st);
                n.Face = Expressions.For(st);
            }
            n.Speed = (staff ? AdultSpeed : KidSpeed) * (0.85f + (float)_rng.NextDouble() * 0.3f);
            n.Phase = (float)_rng.NextDouble() * 10f;
            n.NextBubble = GameClock.Now + 3f + (float)_rng.NextDouble() * 12f;

            n.Go = new GameObject(staff ? "Staff " + id : "Student " + id);
            n.Go.transform.SetParent(_root, false);
            if (staff) n.Go.transform.localScale = Vector3.one * 1.08f;
            n.Shadow = MakeRenderer(n.Go.transform, "Shadow", OrderShadow);
            n.Shadow.sprite = PeopleSprites.Shadow;
            n.Body = MakeRenderer(n.Go.transform, "Body", OrderPerson);
            n.Body.spriteSortPoint = SpriteSortPoint.Pivot;
            n.Body.sprite = PeopleSprites.Figure(n.Look, n.Face, PeopleSprites.Stand);
            n.Bubble = MakeRenderer(n.Go.transform, "Bubble", OrderBubble);
            n.Bubble.transform.localPosition = new Vector3(0.12f, 0.92f, 0f);
            n.Bubble.enabled = false;

            // Step out of a building: their dorm for boarders, otherwise a random door.
            var start = SpawnPoint(staff ? null : s.FindStudent(id));
            n.Pos = start;
            n.Act = Act.Idle;
            n.Timer = (float)_rng.NextDouble() * 1.5f;
            _npcs[key] = n;
            _list.Add(n);
            Place(n);
        }

        Vector2 SpawnPoint(Student st)
        {
            var s = _sim.State;
            Room room = st != null && st.DormRoomId >= 0 ? s.FindRoom(st.DormRoomId) : null;
            if (room == null && s.Rooms.Count > 0) room = s.Rooms[_rng.Next(s.Rooms.Count)];
            if (room != null && !room.Def.Decor)
            {
                room.DoorFront(out int x, out int y);
                if (Walkable(x, y)) return new Vector2(x + 0.5f, y + 0.5f) + Jitter(0.3f);
            }
            _sim.Facilities.GetCampusCenter(out float cx, out float cy);
            var tile = NearestWalkable(Mathf.FloorToInt(cx), Mathf.FloorToInt(cy));
            return new Vector2(tile.x + 0.5f, tile.y + 0.5f);
        }

        static SpriteRenderer MakeRenderer(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        // --- Behaviour ----------------------------------------------------------------------

        void Step(Npc n, float dt)
        {
            n.FaceCheck -= dt;
            if (n.FaceCheck <= 0f)
            {
                n.FaceCheck = 4f + (float)_rng.NextDouble() * 2f;
                RefreshFace(n);
            }

            // A room that vanished (demolished) can't hold anyone
            if (n.RoomId >= 0 && _sim.State.FindRoom(n.RoomId) == null)
            {
                n.RoomId = -1;
                n.Act = Act.Idle;
                n.Timer = 0.5f;
                var tile = NearestWalkable(Mathf.FloorToInt(n.Pos.x), Mathf.FloorToInt(n.Pos.y));
                n.Pos = new Vector2(tile.x + 0.5f, tile.y + 0.5f);
            }

            switch (n.Act)
            {
                case Act.Idle:
                    n.Timer -= dt;
                    if (n.Timer <= 0f) ChooseNext(n);
                    break;
                case Act.Walk:
                    if (Advance(n, dt)) Arrive(n);
                    break;
                case Act.Inside:
                    n.Timer -= dt;
                    if (n.Timer <= 0f) LeaveRoom(n);
                    break;
                case Act.Play:
                    n.Timer -= dt;
                    if (n.Timer <= 0f) LeaveRoom(n);
                    else if (Advance(n, dt)) RunAround(n);
                    break;
            }

            Animate(n, dt);
            UpdateBubble(n);
            Place(n);
        }

        /// <summary>Moves along the path. Returns true when the last point is reached.</summary>
        bool Advance(Npc n, float dt)
        {
            if (n.PathIndex >= n.Path.Count) return true;
            float speed = n.Running ? RunSpeed : n.Speed;
            float travel = speed * dt;
            while (travel > 0f && n.PathIndex < n.Path.Count)
            {
                var target = n.Path[n.PathIndex];
                var delta = target - n.Pos;
                float dist = delta.magnitude;
                if (dist <= travel)
                {
                    n.Pos = target;
                    travel -= dist;
                    n.PathIndex++;
                }
                else
                {
                    n.Pos += delta / dist * travel;
                    if (Mathf.Abs(delta.x) > 0.05f) n.Left = delta.x < 0f;
                    travel = 0f;
                }
            }
            return n.PathIndex >= n.Path.Count;
        }

        void Arrive(Npc n)
        {
            n.Path.Clear();
            n.PathIndex = 0;
            if (n.Leaving)
            {
                n.Leaving = false;
                n.RoomId = -1;
                n.Act = Act.Idle;
                n.Timer = 0.3f + (float)_rng.NextDouble();
                return;
            }
            var room = n.RoomId >= 0 ? _sim.State.FindRoom(n.RoomId) : null;
            switch (n.Goal)
            {
                case Goal.Enter when room != null:
                    n.Act = Act.Inside;
                    n.Sitting = SitsIn(room.Type, n.Staff);
                    n.Timer = Linger(room.Type, n.Staff);
                    ActivityBubble(n, room);
                    break;
                case Goal.Play when room != null:
                    n.Act = Act.Play;
                    n.Running = true;
                    n.Timer = 10f + (float)_rng.NextDouble() * 14f;
                    ActivityBubble(n, room);
                    RunAround(n);
                    break;
                case Goal.Bench when room != null:
                    n.Act = Act.Inside;
                    n.Sitting = true;
                    n.Timer = 6f + (float)_rng.NextDouble() * 10f;
                    n.Pos = BenchSeat(room);
                    break;
                default:
                    n.RoomId = -1;
                    n.Act = Act.Idle;
                    n.Timer = 1f + (float)_rng.NextDouble() * 3f;
                    break;
            }
        }

        void RunAround(Npc n)
        {
            var room = _sim.State.FindRoom(n.RoomId);
            if (room == null) return;
            n.Path.Clear();
            n.PathIndex = 0;
            n.Path.Add(SpotIn(room, 0.7f));
        }

        void LeaveRoom(Npc n)
        {
            n.Sitting = false;
            n.Running = false;
            var room = n.RoomId >= 0 ? _sim.State.FindRoom(n.RoomId) : null;
            n.Path.Clear();
            n.PathIndex = 0;
            if (room != null && !room.Def.Decor)
            {
                room.DoorCell(out int dx, out int dy);
                room.DoorFront(out int fx, out int fy);
                n.Path.Add(new Vector2(dx + 0.5f, dy + 0.5f));
                n.Path.Add(new Vector2(fx + 0.5f, fy + 0.5f));
            }
            n.Leaving = true;
            n.Act = Act.Walk;
        }

        void ChooseNext(Npc n)
        {
            var s = _sim.State;
            var options = new List<(float w, Room room, Goal goal)>();
            if (n.Staff) StaffOptions(n, s.FindStaff(n.Id), options);
            else StudentOptions(n, s.FindStudent(n.Id), options);
            options.Add((n.Staff ? 0.6f : 1.2f, null, Goal.Wander));

            float total = 0f;
            foreach (var o in options) total += o.w;
            float pick = (float)_rng.NextDouble() * total;
            var choice = options[options.Count - 1];
            foreach (var o in options)
            {
                pick -= o.w;
                if (pick <= 0f) { choice = o; break; }
            }

            Vector2Int target;
            if (choice.room == null)
            {
                target = RandomWalkable();
                n.RoomId = -1;
            }
            else
            {
                n.RoomId = choice.room.Id;
                if (choice.goal == Goal.Bench) target = NearestWalkable(choice.room.X, choice.room.Y - 1);
                else
                {
                    choice.room.DoorFront(out int fx, out int fy);
                    target = new Vector2Int(fx, fy);
                }
            }
            n.Goal = choice.goal;

            var start = Vector2Int.FloorToInt(n.Pos);
            if (!Walkable(start.x, start.y)) start = NearestWalkable(start.x, start.y);
            if (!FindPath(start, target, n.Path) && !CrossToEstate(start, target, n.Path))
            {
                n.RoomId = -1;
                n.Goal = Goal.None;
                n.Act = Act.Idle;
                n.Timer = 2f + (float)_rng.NextDouble() * 3f;
                return;
            }
            n.PathIndex = 0;
            if (choice.room != null && (choice.goal == Goal.Enter || choice.goal == Goal.Play))
            {
                choice.room.DoorCell(out int dx, out int dy);
                n.Path.Add(new Vector2(dx + 0.5f, dy + 0.5f));
                n.Path.Add(SpotIn(choice.room, choice.goal == Goal.Play ? 0.7f : 0.75f));
            }
            n.Act = Act.Walk;
            n.Running = false;
        }

        void StudentOptions(Npc n, Student st, List<(float, Room, Goal)> options)
        {
            if (st == null) return;
            int day = _sim.State.Day;
            foreach (var r in _sim.State.Rooms)
            {
                if (!r.IsOperational(day)) continue;
                float w = 0f;
                var goal = Goal.Enter;
                switch (r.Type)
                {
                    case RoomType.Classroom: w = 2.6f; break;
                    case RoomType.Library: w = st.Has(StudentTrait.Bookworm) ? 3f : 1f; break;
                    case RoomType.ScienceLab:
                    case RoomType.ComputerLab: w = 0.8f; break;
                    case RoomType.Cafeteria: w = 2f; break;
                    case RoomType.PicnicArea: w = 1.4f; break;
                    case RoomType.DormRoom: w = r.Id == st.DormRoomId ? 1.6f : 0f; break;
                    case RoomType.StudentLounge: w = st.Has(StudentTrait.Extrovert) ? 2.2f : 1.2f; break;
                    case RoomType.GamingRoom: w = st.Has(StudentTrait.Lazy) ? 2.4f : 1.2f; break;
                    case RoomType.ClubRoom: w = st.ClubId >= 0 ? 1.8f : 0.3f; break;
                    case RoomType.NurseOffice: w = st.IsInjured(day) ? 3f : 0f; break;
                    case RoomType.Garden: w = st.Has(StudentTrait.Anxious) || st.Has(StudentTrait.Introvert) ? 2f : 1f; break;
                    case RoomType.ScholarsHall:
                    case RoomType.InnovationLab: w = st.Has(StudentTrait.Bookworm) ? 2.2f : 1f; break;
                    case RoomType.Theatre: w = st.Has(StudentTrait.Artistic) ? 2.4f : 1f; break;
                    case RoomType.Clubhouse:
                    case RoomType.CommunityCentre:
                    case RoomType.Manor: w = 1f; break;
                    case RoomType.InternationalHouse: w = st.International ? 3f : 0.6f; break;
                    case RoomType.Playground: w = st.Grade <= 2 ? 2.6f : 1.2f; goal = Goal.Play; break;
                    case RoomType.PracticeField:
                    case RoomType.Arena:
                    case RoomType.SoccerField:
                    case RoomType.BasketballCourt:
                    case RoomType.Track:
                    case RoomType.TennisCourt:
                    case RoomType.SwimmingPool:
                        w = st.Has(StudentTrait.Athletic) ? 2.2f : 0.8f;
                        if (st.TeamId >= 0)
                        {
                            var team = _sim.State.FindTeam(st.TeamId);
                            if (team != null && RoomCatalog.SupportsSport(r.Type, team.Sport)) w += 2f;
                        }
                        goal = Goal.Play;
                        break;
                    case RoomType.Gym:
                    case RoomType.SportsComplex:
                        w = st.Has(StudentTrait.Athletic) ? 2f : 0.7f;
                        break;
                    case RoomType.Bench: w = 0.5f; goal = Goal.Bench; break;
                    case RoomType.Fountain:
                    case RoomType.Statue: w = 0.25f; goal = Goal.Bench; break;
                }
                if (w > 0f) options.Add((w, r, goal));
            }
        }

        void StaffOptions(Npc n, StaffMember m, List<(float, Room, Goal)> options)
        {
            if (m == null) return;
            int day = _sim.State.Day;
            foreach (var r in _sim.State.Rooms)
            {
                if (!r.IsOperational(day) || r.Def.Decor) continue;
                float w = 0f;
                switch (m.Role)
                {
                    case StaffRole.Teacher:
                        w = r.Type == RoomType.Classroom ? 3f : r.Type == RoomType.StaffLounge ? 1.2f : r.Type == RoomType.Library || r.Type == RoomType.Office ? 0.5f : 0f;
                        break;
                    case StaffRole.Coach:
                        w = r.Def.Category == RoomCategory.Sports ? 2.5f : r.Type == RoomType.StaffLounge ? 0.6f : 0f;
                        break;
                    case StaffRole.Custodian:
                        w = 0.4f + (100f - r.Cleanliness) / 40f;
                        break;
                    case StaffRole.Maintenance:
                        w = 0.3f + (100f - r.Condition) / 30f;
                        break;
                    case StaffRole.Chef:
                        w = r.Type == RoomType.Cafeteria ? 5f : r.Type == RoomType.StaffLounge ? 0.5f : 0f;
                        break;
                    case StaffRole.Nurse:
                        w = r.Type == RoomType.NurseOffice ? 4f : r.Type == RoomType.StaffLounge ? 0.5f : 0f;
                        break;
                    case StaffRole.Security:
                        w = r.Type == RoomType.SecurityRoom ? 1.5f : 0f;
                        break;
                    case StaffRole.Counselor:
                        w = r.Type == RoomType.Office ? 2f : r.Type == RoomType.StudentLounge ? 1f : 0f;
                        break;
                    default:
                        w = r.Type == RoomType.Office ? 4f : r.Type == RoomType.StaffLounge ? 0.8f : 0f;
                        break;
                }
                if (w > 0f) options.Add((w, r, Goal.Enter));
            }
            if (m.Role == StaffRole.Security) options.Add((3f, null, Goal.Wander));   // patrols
        }

        static bool SitsIn(RoomType type, bool staff) => type switch
        {
            RoomType.Classroom => !staff,
            RoomType.Cafeteria or RoomType.Library or RoomType.StudentLounge or RoomType.GamingRoom or RoomType.ClubRoom
                or RoomType.StaffLounge or RoomType.Office or RoomType.PicnicArea or RoomType.ComputerLab
                or RoomType.ScholarsHall or RoomType.Theatre or RoomType.Clubhouse or RoomType.CommunityCentre
                or RoomType.InternationalHouse or RoomType.Manor => true,
            _ => false,
        };

        float Linger(RoomType type, bool staff)
        {
            float r = (float)_rng.NextDouble();
            return type switch
            {
                RoomType.Classroom => staff ? 14f + r * 10f : 12f + r * 12f,
                RoomType.DormRoom => 10f + r * 14f,
                RoomType.Cafeteria or RoomType.PicnicArea => 8f + r * 8f,
                _ => 6f + r * 10f,
            };
        }

        Vector2 SpotIn(Room room, float inset)
        {
            var f = room.Footprint;
            float x0 = f.X + inset, x1 = f.XMax - inset, y0 = f.Y + inset, y1 = f.YMax - inset;
            if (x1 <= x0) { x0 = x1 = f.X + f.W / 2f; }
            if (y1 <= y0) { y0 = y1 = f.Y + f.H / 2f; }
            return new Vector2(Mathf.Lerp(x0, x1, (float)_rng.NextDouble()), Mathf.Lerp(y0, y1, (float)_rng.NextDouble()));
        }

        /// <summary>A seat on a bench (or the rim of a fountain or plinth): the front edge of the footprint.</summary>
        Vector2 BenchSeat(Room r)
        {
            var f = r.Footprint;
            return new Vector2(f.X + f.W / 2f + ((float)_rng.NextDouble() - 0.5f) * (f.W - 0.6f), f.Y + 0.12f);
        }

        Vector2 Jitter(float amount) => new Vector2(((float)_rng.NextDouble() - 0.5f) * amount, ((float)_rng.NextDouble() - 0.5f) * amount);

        // --- Looks and bubbles --------------------------------------------------------------

        void RefreshFace(Npc n)
        {
            var s = _sim.State;
            Expression face = n.Face;
            if (n.Staff)
            {
                var m = s.FindStaff(n.Id);
                if (m != null) face = Expressions.For(m);
            }
            else
            {
                var st = s.FindStudent(n.Id);
                if (st != null)
                {
                    face = Expressions.For(st);
                    var look = PersonLook.ForStudent(st);     // grade (uniform colour) changes each September
                    if (look.Outfit != n.Look.Outfit || look.Glasses != n.Look.Glasses) n.Look = look;
                }
            }
            n.Face = face;
        }

        void Animate(Npc n, float dt)
        {
            bool moving = (n.Act == Act.Walk || n.Act == Act.Play) && n.PathIndex < n.Path.Count;
            int frame;
            if (n.Sitting && n.Act == Act.Inside) frame = PeopleSprites.Sit;
            else if (moving)
            {
                n.FrameTimer -= dt;
                if (n.FrameTimer <= 0f)
                {
                    n.FrameTimer = n.Running ? 0.12f : 0.18f;
                    n.Frame = n.Frame == PeopleSprites.WalkA ? PeopleSprites.WalkB : PeopleSprites.WalkA;
                }
                frame = n.Frame == PeopleSprites.WalkB ? PeopleSprites.WalkB : PeopleSprites.WalkA;
            }
            else frame = PeopleSprites.Stand;

            n.Body.sprite = PeopleSprites.Figure(n.Look, n.Face, frame, n.Body.sprite);
            n.Body.flipX = n.Left;
            n.Phase += dt * (n.Running ? 14f : moving ? 10f : 2.2f);
        }

        void Place(Npc n)
        {
            bool moving = (n.Act == Act.Walk || n.Act == Act.Play) && n.PathIndex < n.Path.Count;
            float bob = moving ? Mathf.Abs(Mathf.Sin(n.Phase)) * (n.Running ? 0.07f : 0.035f) : Mathf.Sin(n.Phase) * 0.008f;
            n.Go.transform.position = new Vector3(n.Pos.x, n.Pos.y, 0f);
            n.Body.transform.localPosition = new Vector3(0f, bob, 0f);
        }

        void ActivityBubble(Npc n, Room room)
        {
            if (_rng.NextDouble() > 0.45) return;
            string icon = room.Type switch
            {
                RoomType.Classroom or RoomType.Library or RoomType.ScienceLab or RoomType.ComputerLab
                    or RoomType.ScholarsHall or RoomType.InnovationLab => "book",
                RoomType.Cafeteria or RoomType.PicnicArea => "apple",
                RoomType.DormRoom => n.Staff ? null : "zzz",
                RoomType.StudentLounge or RoomType.GamingRoom or RoomType.ClubRoom or RoomType.Theatre or RoomType.Clubhouse => "music",
                RoomType.InternationalHouse or RoomType.CommunityCentre => "heart-red",
                RoomType.Garden => "sprout",
                RoomType.Playground => "star",
                RoomType.NurseOffice => "heart-red",
                _ => room.Def.Category == RoomCategory.Sports ? "ballicon" : null,
            };
            if (n.Staff && n.Look.Role == StaffRole.Custodian) icon = "broom";
            if (icon != null) ShowBubble(n, icon);
        }

        void UpdateBubble(Npc n)
        {
            float now = GameClock.Now;
            if (n.Bubble.enabled && now >= n.BubbleUntil) n.Bubble.enabled = false;
            if (now < n.NextBubble) return;
            n.NextBubble = now + 14f + (float)_rng.NextDouble() * 18f;
            if (n.Staff)
            {
                var m = _sim.State.FindStaff(n.Id);
                if (m != null && m.Energy < 25f) ShowBubble(n, "zzz");
                else if (m != null && m.Morale > 78f && _rng.NextDouble() < 0.5) ShowBubble(n, "heart-red");
                return;
            }
            var st = _sim.State.FindStudent(n.Id);
            if (st == null) return;
            int day = _sim.State.Day;
            if (st.LastIncidentDay >= day - 1) ShowBubble(n, "alert");
            else if (st.Stress > 72f) ShowBubble(n, "sweat");
            else if (st.Happiness < 30f) ShowBubble(n, "cloud");
            else if (st.Happiness > 74f) ShowBubble(n, _rng.NextDouble() < 0.6 ? "heart-red" : "music");
        }

        void ShowBubble(Npc n, string icon)
        {
            if (n.Key == Selected) return;   // the HUD shows what they're thinking
            n.Bubble.sprite = PeopleSprites.Bubble(icon);
            n.Bubble.enabled = true;
            n.BubbleUntil = GameClock.Now + 2.6f;
        }

        // --- Balls on the fields -------------------------------------------------------------

        void UpdateBalls(float dt)
        {
            // Which areas have at least two players right now? (The lists are reused frame to frame.)
            var players = _players;
            foreach (var list in players.Values) list.Clear();
            foreach (var n in _list)
            {
                if (n.Act != Act.Play || n.RoomId < 0) continue;
                if (!players.TryGetValue(n.RoomId, out var list)) players[n.RoomId] = list = new List<Npc>();
                list.Add(n);
            }

            _gone.Clear();
            foreach (var kv in _balls)
                if (!players.TryGetValue(kv.Key, out var list) || list.Count < 2) _gone.Add(kv.Key);
            foreach (int id in _gone)
            {
                Destroy(_balls[id].Sprite.gameObject);
                _balls.Remove(id);
            }

            foreach (var kv in players)
            {
                if (kv.Value.Count < 2) continue;
                var room = _sim.State.FindRoom(kv.Key);
                if (room == null || room.Type == RoomType.SwimmingPool || room.Type == RoomType.Track) continue;
                if (!_balls.TryGetValue(kv.Key, out var ball))
                {
                    var sr = MakeRenderer(_root, "Ball", OrderPerson);
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                    sr.sprite = PeopleSprites.Ball;
                    var p = kv.Value[0].Pos;
                    ball = new Ball { Sprite = sr, AreaId = kv.Key, From = p, To = p, T = 1f, Duration = 1f };
                    _balls[kv.Key] = ball;
                }
                ball.T += dt / ball.Duration;
                if (ball.T >= 1f)
                {
                    ball.From = ball.To;
                    var target = kv.Value[_rng.Next(kv.Value.Count)];
                    ball.To = target.Pos + new Vector2(target.Left ? -0.18f : 0.18f, 0.05f);
                    ball.Duration = 0.5f + Vector2.Distance(ball.From, ball.To) / 6f;
                    ball.T = 0f;
                }
                var at = Vector2.Lerp(ball.From, ball.To, ball.T);
                float hop = Mathf.Sin(ball.T * Mathf.PI) * Mathf.Min(0.9f, Vector2.Distance(ball.From, ball.To) * 0.25f);
                ball.Sprite.transform.position = new Vector3(at.x, at.y + hop + 0.08f, 0f);
            }
        }

        // --- Paths ---------------------------------------------------------------------------

        void RebuildGrid(CampusMap map)
        {
            _gridVersion = map.Version;
            _w = map.Width;
            _h = map.Height;
            if (_walk == null || _walk.Length != _w * _h)
            {
                _walk = new bool[_w * _h];
                _roam = new bool[_w * _h];
            }
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    bool free = map.RoomIdAt(x, y) == 0;
                    _roam[y * _w + x] = free;
                    _walk[y * _w + x] = free && map.IsOwned(x, y);
                }

            // Anyone now standing inside something new walks out of it
            foreach (var n in _list)
            {
                if (n.Act != Act.Walk && n.Act != Act.Idle) continue;
                var t = Vector2Int.FloorToInt(n.Pos);
                if (Walkable(t.x, t.y) || n.PathIndex < n.Path.Count && n.RoomId >= 0) continue;
                var free = NearestWalkable(t.x, t.y);
                n.Pos = new Vector2(free.x + 0.5f, free.y + 0.5f);
                n.Path.Clear();
                n.PathIndex = 0;
                n.Act = Act.Idle;
                n.Timer = 0.2f;
            }
        }

        bool Walkable(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h && (_crossing ? _roam : _walk)[y * _w + x];

        /// <summary>The old estate needn't touch the rest of our land: to get there, people cross the road.</summary>
        bool CrossToEstate(Vector2Int a, Vector2Int b, List<Vector2> result)
        {
            if (!_sim.State.Campus.EstateOpen) return false;
            _crossing = true;
            bool found = FindPath(a, b, result);
            _crossing = false;
            return found;
        }

        Vector2Int NearestWalkable(int x, int y)
        {
            if (Walkable(x, y)) return new Vector2Int(x, y);
            for (int r = 1; r < 40; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        if (Walkable(x + dx, y + dy)) return new Vector2Int(x + dx, y + dy);
                    }
            return new Vector2Int(Mathf.Clamp(x, 0, _w - 1), Mathf.Clamp(y, 0, _h - 1));
        }

        Vector2Int RandomWalkable()
        {
            // Somewhere near the buildings, not out on the far corners of the land
            _sim.Facilities.GetCampusCenter(out float cx, out float cy);
            for (int attempt = 0; attempt < 40; attempt++)
            {
                int x = Mathf.RoundToInt(cx + ((float)_rng.NextDouble() - 0.5f) * 26f);
                int y = Mathf.RoundToInt(cy + ((float)_rng.NextDouble() - 0.5f) * 20f);
                if (Walkable(x, y)) return new Vector2Int(x, y);
            }
            return NearestWalkable(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy));
        }

        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
        float[] _g;
        int[] _from, _heapNodes;
        float[] _heapKeys;
        int[] _closedStamp, _openStamp;
        int _stamp;

        /// <summary>A* over walkable tiles (8 directions, no corner cutting), then smoothed. Fills world points.</summary>
        bool FindPath(Vector2Int a, Vector2Int b, List<Vector2> result)
        {
            result.Clear();
            if (!Walkable(b.x, b.y)) b = NearestWalkable(b.x, b.y);
            if (!Walkable(a.x, a.y) || !Walkable(b.x, b.y)) return false;
            int n = _w * _h;
            if (_g == null || _g.Length != n)
            {
                _g = new float[n];
                _from = new int[n];
                _closedStamp = new int[n];
                _openStamp = new int[n];
                _heapNodes = new int[n * 8 + 8];
                _heapKeys = new float[n * 8 + 8];
            }
            _stamp++;
            int start = a.y * _w + a.x, goal = b.y * _w + b.x;
            int heapCount = 0;
            _g[start] = 0f;
            _from[start] = -1;
            _openStamp[start] = _stamp;
            Push(start, Heuristic(a.x, a.y, b.x, b.y), ref heapCount);

            bool found = false;
            while (heapCount > 0)
            {
                int cur = Pop(ref heapCount);
                if (_closedStamp[cur] == _stamp) continue;
                _closedStamp[cur] = _stamp;
                if (cur == goal) { found = true; break; }
                int cx = cur % _w, cy = cur / _w;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + Dx[k], ny = cy + Dy[k];
                    if (!Walkable(nx, ny)) continue;
                    if (k >= 4 && (!Walkable(cx + Dx[k], cy) || !Walkable(cx, cy + Dy[k]))) continue;
                    int next = ny * _w + nx;
                    if (_closedStamp[next] == _stamp) continue;
                    float g = _g[cur] + (k >= 4 ? 1.4142f : 1f);
                    if (_openStamp[next] == _stamp && g >= _g[next]) continue;
                    _openStamp[next] = _stamp;
                    _g[next] = g;
                    _from[next] = cur;
                    if (heapCount < _heapNodes.Length) Push(next, g + Heuristic(nx, ny, b.x, b.y), ref heapCount);
                }
            }
            if (!found) return false;

            var tiles = new List<int>();
            for (int c = goal; c != -1; c = _from[c]) tiles.Add(c);
            tiles.Reverse();

            // String-pull: skip points we can walk to in a straight line.
            var points = new List<Vector2>(tiles.Count);
            foreach (int t in tiles) points.Add(new Vector2(t % _w + 0.5f, t / _w + 0.5f));
            int i = 0;
            while (i < points.Count - 1)
            {
                int j = Math.Min(points.Count - 1, i + 14);
                while (j > i + 1 && !Clear(points[i], points[j])) j--;
                result.Add(points[j] + Jitter(0.35f));
                i = j;
            }
            if (result.Count == 0) result.Add(points[points.Count - 1]);
            return true;
        }

        static float Heuristic(int x0, int y0, int x1, int y1)
        {
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            return Mathf.Max(dx, dy) + 0.4142f * Mathf.Min(dx, dy);
        }

        bool Clear(Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.CeilToInt(len / 0.25f);
            for (int s = 1; s < steps; s++)
            {
                var p = Vector2.Lerp(a, b, s / (float)steps);
                for (int k = 0; k < 4; k++)
                {
                    float ox = (k & 1) == 0 ? -0.3f : 0.3f, oy = (k & 2) == 0 ? -0.3f : 0.3f;
                    if (!Walkable(Mathf.FloorToInt(p.x + ox), Mathf.FloorToInt(p.y + oy))) return false;
                }
            }
            return true;
        }

        void Push(int node, float key, ref int count)
        {
            int i = count++;
            _heapNodes[i] = node;
            _heapKeys[i] = key;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_heapKeys[parent] <= _heapKeys[i]) break;
                (_heapNodes[parent], _heapNodes[i]) = (_heapNodes[i], _heapNodes[parent]);
                (_heapKeys[parent], _heapKeys[i]) = (_heapKeys[i], _heapKeys[parent]);
                i = parent;
            }
        }

        int Pop(ref int count)
        {
            int top = _heapNodes[0];
            count--;
            _heapNodes[0] = _heapNodes[count];
            _heapKeys[0] = _heapKeys[count];
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < count && _heapKeys[l] < _heapKeys[m]) m = l;
                if (r < count && _heapKeys[r] < _heapKeys[m]) m = r;
                if (m == i) break;
                (_heapNodes[m], _heapNodes[i]) = (_heapNodes[i], _heapNodes[m]);
                (_heapKeys[m], _heapKeys[i]) = (_heapKeys[i], _heapKeys[m]);
                i = m;
            }
            return top;
        }

        // --- Hooks for the automated screenshot tour ------------------------------------------

        /// <summary>The student (or staff member) standing closest to a room, for the tour's close-ups.</summary>
        internal int TourCount => _list.Count;

        internal long TourPickNear(Room room, bool staff)
        {
            long best = -1;
            float bestDist = float.MaxValue;
            Vector2 at = Vector2.zero;
            if (room != null)
            {
                var f = room.Footprint;
                at = new Vector2(f.X + f.W / 2f, f.Y + f.H / 2f);
            }
            foreach (var n in _list)
            {
                if (n.Staff != staff) continue;
                float d = room != null ? Vector2.Distance(n.Pos, at) : 0f;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n.Key;
                }
            }
            return best;
        }

        /// <summary>Lets everyone walk for a while in one go (the tour's clock is held).</summary>
        internal void TourSimulate(float seconds)
        {
            if (_sim == null) return;
            for (int k = 0; k < 30; k++) Sync();
            for (float t = 0f; t < seconds; t += 0.05f)
            {
                foreach (var n in _list) Step(n, 0.05f);
                UpdateBalls(0.05f);
            }
        }
    }
}
