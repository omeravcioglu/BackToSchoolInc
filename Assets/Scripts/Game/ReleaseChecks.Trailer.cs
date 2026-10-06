using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// "-trailer &lt;folder&gt;": plays a scripted tour of the game and writes every frame to
    /// &lt;folder&gt;/frames at 60 frames a second, with the shot list in timeline.txt.
    /// Tools/Trailer/make_trailer.py turns them into the video with the trailer music.
    /// The game clock steps exactly 1/60 s per frame while recording (see GameClock), so the video
    /// is smooth however long each frame takes to capture. Each shot's length comes from
    /// &lt;folder&gt;/cuts.txt ("name frames" per line, written by make_trailer.py from the music's
    /// bars) so the cuts land on the beat; shots it doesn't list use their default length.
    /// </summary>
    sealed partial class ReleaseChecks
    {
        const int TrailerFps = 60;
        string _frames;
        int _frame;
        readonly StringBuilder _timeline = new StringBuilder();
        readonly Dictionary<string, int> _cuts = new Dictionary<string, int>();

        IEnumerator Trailer()
        {
            // A virtual mouse parked in a corner, so the real pointer over the window doesn't
            // outline the room under it.
            _mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("TrailerMouse");
            _mousePark = new Vector2(2f, 2f);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;   // time is fixed per frame: run as fast as capture allows
            bool uiClock = GameClock.FixStep(1f / TrailerFps);
            Line($"game clock fixed at {TrailerFps} fps; UI clock {(uiClock ? "follows it" : "stays real time")}");
            _frames = Path.Combine(_folder, "frames");
            if (Directory.Exists(_frames)) Directory.Delete(_frames, true);
            Directory.CreateDirectory(_frames);
            string cuts = Path.Combine(_folder, "cuts.txt");
            if (File.Exists(cuts))
                foreach (var line in File.ReadAllLines(cuts))
                {
                    var bits = line.Split(' ');
                    if (bits.Length == 2 && int.TryParse(bits[1], out int frames)) _cuts[bits[0]] = frames;
                }

            var hud = Hud;
            var root = FindFirstObjectByType<UIDocument>().rootVisualElement;
            void Show(string className, bool on) => root.Query(className: className).ForEach(e => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None);
            var cam = CampusCamera.Instance;

            // 1. The title, without its menu: the art drifts in slowly behind the logo.
            Show("title-menu", false);
            Show("quit-note", false);
            yield return Hold(10);
            yield return Record("title", 4.3f, t => PushTitle(root, 1f + 0.035f * t));
            PushTitle(root, 1f);
            Show("title-menu", true);
            Show("quit-note", true);

            // 2. A young school: a slow push in over the campus.
            var sim = QuickSchool(31);
            Game.TourHold = true;   // no letters or level-ups popping up mid-shot; people still walk
            var s = sim.State;
            s.Finance.Cash = Math.Max(s.Finance.Cash, 400000f);
            Grow(sim, 45);
            sim.Stats.ComputeIndices();
            hud.TourCloseAll();
            yield return Settle();
            Vector2 centre = CampusView.Instance.CampusCenter();
            yield return Record("young_school", 4.3f, t => cam.Frame(centre + new Vector2(0f, Mathf.Lerp(0.6f, 0f, t)), Mathf.Lerp(11f, 8f, t)));

            // 3. One student: follow them, then open their card.
            var people = CampusPeople.Instance;
            long student = people != null ? people.TourPickNear(s.Rooms.FirstOrDefault(r => r.Type == RoomType.Playground) ?? s.Rooms.FirstOrDefault(r => r.Type == RoomType.Classroom), staff: false) : -1;
            if (student >= 0)
            {
                hud.TourHideHud(true);
                cam.Follow(() => people.TryGetPosition(student, out var p) ? p : (Vector2?)null, 4.5f);
                yield return Hold(40);
                yield return Record("follow", 4.3f, null);
                hud.TourHideHud(false);
                CampusController.Instance.SelectPerson(student);
                cam.Follow(() => people.TryGetPosition(student, out var p) ? p + new Vector2(0f, 1.2f) : (Vector2?)null, 6f);
                yield return Hold(30);
                yield return Record("person_card", 4.3f, null);
                CampusController.Instance.SelectPerson(-1);
                cam.StopFollowing();
            }

            // 4. Letters: let a few days of mail pile up unanswered, then open the inbox.
            for (int d = 0; d < 6; d++) sim.AdvanceDay();
            hud.TourClearPopups();
            hud.TourPage(HudPage.Inbox);
            yield return Hold(30);
            yield return Record("inbox", 4.3f, null);
            hud.TourCloseAll();

            // 5. A grown campus with gardens, fields and decorations.
            sim = QuickSchool(31);
            Game.TourHold = true;
            s = sim.State;
            s.Finance.Cash = Math.Max(s.Finance.Cash, 1500000f);
            Grow(sim, 600);
            Decorate(sim);
            sim.Stats.ComputeIndices();
            hud.TourClearPopups();
            hud.TourCloseAll();
            yield return Settle();
            centre = CampusView.Instance.CampusCenter();
            cam.Frame(centre, 10f);
            // A dorm's ghost glides in over a free spot, then it's built there: the site appears
            // and the toast says when it will be ready.
            Vector2 spot = centre;
            bool canBuild = false;
            int bx = 0, by = 0, rot = 0;
            CampusController.Instance.BeginBuild(RoomType.DormRoom);
            if (sim.Facilities.TryFindPlacement(RoomType.DormRoom, out bx, out by, out rot))
            {
                var fp = Placement.Footprint(RoomCatalog.Get(RoomType.DormRoom), bx, by, rot);
                spot = new Vector2(fp.X + fp.W / 2f, fp.Y + fp.H / 2f);
                CampusController.Instance.ForcePointerCell(new Vector2Int(fp.X + (fp.W - 1) / 2, fp.Y + (fp.H - 1) / 2), rot);
                canBuild = true;
            }
            bool built = false;
            yield return Record("build", 4.3f, t =>
            {
                cam.Frame(spot + new Vector2(Mathf.Lerp(-3.5f, -1.5f, t), 0.5f), 10f);
                if (canBuild && !built && t >= 0.5f)
                {
                    built = true;
                    CampusController.Instance.ForcePointerCell(null);
                    CampusController.Instance.Cancel();
                    var result = sim.BuildRoomAt(RoomType.DormRoom, bx, by, rot);
                    if (result.Ok) hud.ShowToast(result.Message, "check");
                }
            });
            if (!built)
            {
                CampusController.Instance.ForcePointerCell(null);
                CampusController.Instance.Cancel();
            }

            // 6. Close-ups of school life, without the HUD, centred on someone near the room.
            hud.TourClearPopups();
            hud.TourHideHud(true);
            foreach (var type in new[] { RoomType.Playground, RoomType.Cafeteria, RoomType.Classroom, RoomType.PracticeField })
            {
                // The copy nearest the middle: the camera can't centre on one at the map's edge.
                Vector2 Middle(Room r) => new Vector2(r.Footprint.X + r.Footprint.W / 2f, r.Footprint.Y + r.Footprint.H / 2f);
                var room = s.Rooms.Where(r => r.Type == type).OrderBy(r => (Middle(r) - centre).sqrMagnitude).FirstOrDefault();
                if (room == null) continue;
                var at = Middle(room);
                long someone = people != null ? people.TourPickNear(room, staff: false) : -1;
                if (someone >= 0 && people.TryGetPosition(someone, out var seen) && (seen - at).magnitude < 6f) at = (at + seen) / 2f;
                cam.Frame(at, 6.5f);
                yield return Record("life_" + type, 3.2f, t => cam.Frame(at + new Vector2(Mathf.Lerp(-0.6f, 0.6f, t), 0f), Mathf.Lerp(6.5f, 5.8f, t)));
            }
            hud.TourHideHud(false);

            // 7. Running the school: the main pages, one bar each, and the rankings.
            cam.Frame(centre, 10f);
            foreach (var page in new[] { HudPage.Students, HudPage.Staff, HudPage.Money, HudPage.Ranks })
            {
                hud.TourPage(page);
                yield return Hold(16);
                yield return Record("page_" + page, 2.1f, null);
            }
            hud.TourCloseAll();

            // 8. A level-up card.
            hud.TourLevelUp(Math.Min(s.Progress.SchoolLevel + 1, 10));
            yield return Hold(10);
            yield return Record("level_up", 4.3f, null);
            hud.TourClearPopups();
            hud.TourCloseAll();

            // 9. The whole campus: a slow pull back, no HUD.
            hud.TourHideHud(true);
            cam.Frame(centre, 6f);
            yield return Hold(12);
            yield return Record("pull_back", 8.6f, t => cam.Frame(centre, Mathf.Lerp(6f, 12.5f, t)));
            hud.TourHideHud(false);

            // 10. The end card: the title again, without its menu, drifting in while the music ends.
            Game.ShowTitle();
            yield return Hold(20);
            Show("title-menu", false);
            Show("quit-note", false);
            yield return Hold(20);
            yield return Record("end_card", 10f, t => PushTitle(root, 1f + 0.04f * t));

            System.Threading.Tasks.Task.WaitAll(_encodes.ToArray());
            File.WriteAllText(Path.Combine(_folder, "timeline.txt"), _timeline.ToString());
            Line($"{_frame} frames at {TrailerFps} fps ({_frame / (float)TrailerFps:0.0} s), {Screen.width}x{Screen.height}");
        }

        /// <summary>Scales the title art (not the logo) from its centre: a slow push in.</summary>
        static void PushTitle(VisualElement root, float scale)
        {
            var art = root.Query<TitleBackdrop>().First();
            if (art == null) return;
            art.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            art.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        /// <summary>Lets people spawn and find their places before a shot.</summary>
        IEnumerator Settle()
        {
            CampusPeople.Instance?.TourSimulate(30f);
            yield return Hold(60);
        }

        /// <summary>Plays frames without recording them.</summary>
        static IEnumerator Hold(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        /// <summary>
        /// "-still": the camera holds each shot's first framing and frames are saved as PNG, for the
        /// store description's GIFs (a moving camera or JPEG noise changes every pixel of every frame).
        /// </summary>
        static bool Still => Flag("-still");

        /// <summary>
        /// Records a shot: its length from cuts.txt, or <paramref name="seconds"/>.
        /// <paramref name="move"/> gets 0..1 (smoothed) before each frame.
        /// </summary>
        IEnumerator Record(string name, float seconds, Action<float> move)
        {
            int count = _cuts.TryGetValue(name, out int listed) ? listed : Mathf.RoundToInt(seconds * TrailerFps);
            _timeline.AppendLine($"{name} {_frame} {count}");
            // Start from the first camera spot and let newly shown ground get drawn first, so the
            // shot doesn't open on half-painted tiles.
            move?.Invoke(0f);
            for (int i = 0; i < 30; i++)
            {
                ParkMouse();
                yield return null;
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0f;
                if (!Still) move?.Invoke(t * t * (3f - 2f * t));
                ParkMouse();
                yield return new WaitForEndOfFrame();
                // Time is fixed per frame, so the grab can take as long as it needs; the image is
                // encoded on a worker.
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                byte[] raw = shot.GetRawTextureData();
                var format = shot.graphicsFormat;
                uint w = (uint)shot.width, h = (uint)shot.height;
                Destroy(shot);
                string path = Path.Combine(_frames, $"f{_frame:00000}.{(Still ? "png" : "jpg")}");
                bool png = Still;
                _encodes.Add(System.Threading.Tasks.Task.Run(() => File.WriteAllBytes(path, png
                    ? ImageConversion.EncodeArrayToPNG(raw, format, w, h)
                    : ImageConversion.EncodeArrayToJPG(raw, format, w, h, 0, 95))));
                // Don't let unwritten frames pile up (8 MB each) if the encoders fall behind.
                while (_encodes.Count(e => !e.IsCompleted) > 48) System.Threading.Thread.Sleep(5);
                _frame++;
                yield return null;
            }
            Line($"{name}: {count} frames ({count / (float)TrailerFps:0.00} s), captured in {watch.Elapsed.TotalSeconds:0.0} s");
        }

        readonly List<System.Threading.Tasks.Task> _encodes = new List<System.Threading.Tasks.Task>();

        Vector2 _mousePark;

        void ParkMouse()
        {
            _mouse.MakeCurrent();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = _mousePark });
        }
    }
}
