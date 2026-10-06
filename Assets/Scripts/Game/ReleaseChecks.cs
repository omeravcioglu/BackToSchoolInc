using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using MouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace SchoolSim.Game
{
    /// <summary>
    /// Release checks for built players, from the command line. Each writes report.txt (and
    /// screenshots) to its folder, then quits:
    /// "-shots &lt;folder&gt;" takes a quick set of screenshots (campus, people, pages, build mode,
    /// settings) to check other screen sizes; "-perftest &lt;folder&gt;" grows a big school, lets it run
    /// and measures frame times; "-playtest &lt;folder&gt;" plays the main actions with a virtual mouse
    /// clicking the real buttons (founding a school, the tutorial letter, building, buying land,
    /// hiring, tuition, rules, people, settings) and checks that each one worked; "-keyart
    /// &lt;folder&gt;" captures the title art without its menu (with and without the logo) for store
    /// images, at "-supersize N" times the window size; "-trailer &lt;folder&gt;" records the trailer's
    /// frames (see ReleaseChecks.Trailer.cs).
    /// </summary>
    sealed partial class ReleaseChecks : MonoBehaviour
    {
        string _folder;
        readonly StringBuilder _report = new StringBuilder();
        int _passed, _failed, _focusLosses;
        Mouse _mouse;
        UIDocument _doc;

        public static bool Requested() => Arg("-shots") != null || Arg("-perftest") != null || Arg("-playtest") != null || Arg("-keyart") != null || Arg("-trailer") != null;

        static bool Flag(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        GameController Game => GameController.Instance;
        GameHud Hud => GetComponent<GameHud>();

        void Start()
        {
            IEnumerator run;
            if ((_folder = Arg("-playtest")) != null) run = Playtest();
            else if ((_folder = Arg("-perftest")) != null) run = PerfTest();
            else if ((_folder = Arg("-shots")) != null) run = Shots();
            else if ((_folder = Arg("-keyart")) != null) run = KeyArt();
            else if ((_folder = Arg("-trailer")) != null) run = Trailer();
            else
            {
                Destroy(this);
                return;
            }
            Directory.CreateDirectory(_folder);
            // Saves go to the check's own folder, starting empty, never the player's.
            string saves = Path.Combine(_folder, "saves");
            if (Directory.Exists(saves)) Directory.Delete(saves, true);
            SaveSlots.Folder = saves;
            StartCoroutine(Wrap(run));
        }

        IEnumerator Wrap(IEnumerator run)
        {
            Line($"{Application.productName} {Application.version} · {Screen.width}x{Screen.height} · {SystemInfo.processorType} · {SystemInfo.graphicsDeviceName}");
            yield return new WaitForSecondsRealtime(1.5f);   // let the HUD build and the music render
            yield return run;
            if (_focusLosses > 0)
                Line($"\nThe window lost focus {_focusLosses} time(s). Clicks wait for it to come back, but a failure may still come from that.");
            Line(_passed + _failed > 0 ? $"\n{_passed} passed, {_failed} failed" : "\ndone");
            File.WriteAllText(Path.Combine(_folder, "report.txt"), _report.ToString());
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        void Line(string text)
        {
            _report.AppendLine(text);
            Debug.Log("[release check] " + text);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) _focusLosses++;
        }

        void Check(string what, bool ok, string detail = null)
        {
            if (ok) _passed++;
            else _failed++;
            if (!ok && !Application.isFocused) detail = (detail != null ? detail + ", " : "") + "the window wasn't focused";
            Line($"{(ok ? "PASS" : "FAIL")}  {what}{(detail != null ? "  (" + detail + ")" : "")}");
        }

        IEnumerator Snap(string name)
        {
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            int scale = int.TryParse(Arg("-supersize"), out int k) ? Mathf.Clamp(k, 1, 4) : 1;
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"), scale);
            yield return null;
            yield return null;
        }

        static IEnumerator Wait(float seconds) => WaitRealtime(seconds);

        static IEnumerator WaitRealtime(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }

        // --- A school to look at ----------------------------------------------------------------

        /// <summary>Founds a school straight away (no menus), skipping the tutorial.</summary>
        Simulation QuickSchool(int seed)
        {
            Game.StartNewGame(new NewGameSetup { SchoolName = "Little Oak School", Seed = seed, Assistant = AssistantRules.Candidates(seed)[1] });
            var sim = Game.Sim;
            sim.SkipTutorial();
            return sim;
        }

        /// <summary>Plays <paramref name="days"/> days at once, running the school sensibly (a short form of the harness's director).</summary>
        static void Grow(Simulation sim, int days)
        {
            var s = sim.State;
            for (int d = 0; d < days && !s.GameOver; d++)
            {
                sim.AdvanceDay();
                foreach (var m in s.Inbox.ToArray())
                {
                    if (!m.NeedsDecision) continue;
                    var option = m.Options.FirstOrDefault(o => o.Id == m.AdvisorOptionId && sim.Inbox.WhyNot(m, o) == null)
                                 ?? m.Options.FirstOrDefault(o => sim.Inbox.WhyNot(m, o) == null);
                    if (option != null) sim.Reply(m.Id, option.Id);
                }
                if (Calendar.DayOfWeek(sim.Day) != 0) continue;
                var ix = sim.Indices;
                void Build(RoomType type)
                {
                    if (s.Rooms.Any(r => r.Type == type && !r.IsBuilt(sim.Day))) return;
                    if (!sim.BuildRoom(type).Ok && s.Finance.Cash > sim.Facilities.LandCost + 50000)
                    {
                        sim.BuyLand();
                        sim.BuildRoom(type);
                    }
                }
                if (ix.FreeBeds <= 4) Build(RoomType.DormRoom);
                if (ix.BathroomShortage > 0.05f) Build(RoomType.Bathroom);
                if (ix.ClassroomSeats < ix.Students + 4) Build(RoomType.Classroom);
                foreach (var t in new[] { RoomType.Library, RoomType.StudentLounge, RoomType.LaundryRoom, RoomType.StaffLounge, RoomType.Playground, RoomType.Garden, RoomType.BasketballCourt })
                    if (!s.Rooms.Any(r => r.Type == t) && sim.Facilities.CanBuild(t, out _)) { Build(t); break; }
                var caf = s.Rooms.FirstOrDefault(r => r.Type == RoomType.Cafeteria);
                if (ix.MealCapacity < ix.Students + 2 && caf != null && sim.Facilities.CanUpgrade(caf, out _)) sim.UpgradeRoom(caf.Id);
                void Hire(StaffRole role)
                {
                    var c = s.Candidates.Where(x => x.Staff.Role == role && sim.Staff.CanHire(x, out _)).OrderByDescending(x => x.Staff.Skill).FirstOrDefault();
                    if (c != null) sim.Hire(c, 3);
                }
                if (ix.StudentsPerTeacher > 12f) Hire(StaffRole.Teacher);
                if (ix.ChefCoverage < 1f) Hire(StaffRole.Chef);
                if (ix.CustodianCoverage < 1f) Hire(StaffRole.Custodian);
                foreach (var a in s.Applicants.OrderByDescending(a => sim.Admissions.Score(a)).ToList())
                {
                    if (sim.Facilities.FreeBeds() <= 0) break;
                    sim.Admit(a, ScholarshipType.None, Math.Min(a.RequestedAid, 0.15f));
                }
            }
        }

        // --- Screenshots at any screen size ------------------------------------------------------

        /// <summary>A quick tour of the main screens. With "-big", a grown school with outdoor spaces and decorations (store shots).</summary>
        IEnumerator Shots()
        {
            var hud = Hud;
            yield return Snap("00_title");
            bool big = Flag("-big");
            var sim = QuickSchool(31);
            Game.TourHold = true;
            var s = sim.State;
            s.Finance.Cash = Math.Max(s.Finance.Cash, big ? 1500000f : 400000f);
            Grow(sim, big ? 600 : 150);
            if (big) Decorate(sim);
            sim.Stats.ComputeIndices();
            hud.TourCloseAll();
            yield return Wait(1f);
            CampusPeople.Instance?.TourSimulate(30f);
            var cam = CampusCamera.Instance;
            cam.Frame(CampusView.Instance.CampusCenter(), big ? 13f : 10f);
            yield return Snap("01_campus");
            if (big)
            {
                foreach (var type in new[] { RoomType.Playground, RoomType.Cafeteria, RoomType.Classroom, RoomType.PracticeField })
                {
                    var room = s.Rooms.FirstOrDefault(r => r.Type == type);
                    if (room == null) continue;
                    var f = room.Footprint;
                    cam.Frame(new Vector2(f.X + f.W / 2f, f.Y + f.H / 2f - 0.5f), 6.5f);
                    CampusPeople.Instance?.TourSimulate(2f);
                    yield return Snap("01_people_" + type);
                }
                cam.Frame(CampusView.Instance.CampusCenter(), 10f);
            }

            var people = CampusPeople.Instance;
            long student = people != null ? people.TourPickNear(sim.State.Rooms.FirstOrDefault(r => r.Type == RoomType.Classroom), staff: false) : -1;
            if (student >= 0)
            {
                CampusController.Instance.SelectPerson(student);
                if (people.TryGetPosition(student, out var at)) cam.Frame(at + new Vector2(0f, 1.5f), 6.5f);
                yield return Snap("02_person_card");
                CampusController.Instance.SelectPerson(-1);
            }

            cam.Frame(CampusView.Instance.CampusCenter(), 10f);
            CampusController.Instance.BeginBuild(RoomType.DormRoom);
            yield return Snap("03_build");
            CampusController.Instance.Cancel();

            int n = 4;
            foreach (var page in new[] { HudPage.Inbox, HudPage.Students, HudPage.Staff, HudPage.Money, HudPage.Rules, HudPage.Report })
            {
                hud.TourPage(page);
                yield return Snap($"{n++:00}_{page}");
            }
            hud.TourCloseAll();
            hud.TourSettings(true);
            yield return Snap($"{n++:00}_settings");
            hud.TourCloseAll();
            Game.ShowTitle();
            yield return Snap($"{n:00}_title_again");
        }

        /// <summary>Outdoor spaces and decorations around a grown school, finished at once (for store shots).</summary>
        static void Decorate(Simulation sim)
        {
            var s = sim.State;
            s.Progress.SchoolLevel = Math.Max(s.Progress.SchoolLevel, 8);
            var added = new List<Room>();
            foreach (var type in new[] { RoomType.Playground, RoomType.PracticeField, RoomType.Garden, RoomType.PicnicArea, RoomType.Fountain,
                         RoomType.Tree, RoomType.Tree, RoomType.Tree, RoomType.Tree, RoomType.FlowerBed, RoomType.FlowerBed, RoomType.FlowerBed,
                         RoomType.Bench, RoomType.Bench })
            {
                if (!sim.BuildRoom(type).Ok && sim.BuyLand().Ok) sim.BuildRoom(type);
                if (s.Rooms.Count > 0 && s.Rooms[s.Rooms.Count - 1].Type == type) added.Add(s.Rooms[s.Rooms.Count - 1]);
            }
            foreach (var r in added) r.ConstructionDoneDay = s.Day;
            s.Campus.RebuildOccupancy(s.Rooms);
        }

        // --- Store art ---------------------------------------------------------------------------

        IEnumerator KeyArt()
        {
            int scale = int.TryParse(Arg("-supersize"), out int k) ? Mathf.Clamp(k, 1, 4) : 2;
            var root = FindFirstObjectByType<UIDocument>().rootVisualElement;
            void Hide(string className) => root.Query(className: className).ForEach(e => e.style.display = DisplayStyle.None);
            Hide("title-menu");
            Hide("quit-note");
            yield return Wait(0.5f);
            yield return Capture("keyart", scale);
            Hide("title-logo");
            Hide("mascot");
            Hide("tagline");
            yield return Capture("backdrop", scale);
        }

        IEnumerator Capture(string name, int scale)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"), scale);
            yield return Wait(0.5f);
            Line($"{name}.png at {Screen.width * scale}x{Screen.height * scale}");
        }

        // --- Performance -------------------------------------------------------------------------

        IEnumerator PerfTest()
        {
            var sim = QuickSchool(47);
            Game.TourHold = true;
            var s = sim.State;
            s.Finance.Cash = 2000000f;
            var grow = Stopwatch.StartNew();
            Grow(sim, 720);
            Line($"Grew the school for two years in {grow.ElapsedMilliseconds} ms: {s.Students.Count} students, {s.Staff.Count} staff, {s.Rooms.Count} rooms, {s.Campus.OwnedParcelCount} parcels");

            var tick = Stopwatch.StartNew();
            for (int d = 0; d < 60; d++) sim.AdvanceDay();
            Line($"A school day takes {tick.Elapsed.TotalMilliseconds / 60:0.00} ms to simulate (budget: 4000 ms)");
            var save = Stopwatch.StartNew();
            string saved = SaveGame.Capture(sim, DateTime.UtcNow, Application.version);
            Line($"Saving it takes {save.ElapsedMilliseconds} ms on the main thread ({saved.Length / 1024} KB of text before compression)");

            Hud.TourCloseAll();
            Game.TourHold = false;   // the real clock from here on
            var cam = CampusCamera.Instance;
            cam.Frame(CampusView.Instance.CampusCenter(), 14f);
            yield return Wait(8f);    // people spawn and find their feet; their sprites get painted
            yield return Measure("campus, normal zoom", 20f);
            yield return Snap("perf_normal");
            cam.Frame(CampusView.Instance.CampusCenter(), 5f);
            yield return Measure("campus, close up", 10f);
            cam.Frame(CampusView.Instance.CampusCenter(), 26f);
            yield return Measure("campus, zoomed out", 10f);
            Hud.TourPage(HudPage.Students);
            yield return Measure("students page open", 6f);
            Hud.TourCloseAll();

            // How long does a page take to appear, the first time and the next?
            foreach (var page in new[] { HudPage.Inbox, HudPage.Students, HudPage.Staff, HudPage.Money, HudPage.Report, HudPage.Rooms })
            {
                var times = new List<float>();
                for (int round = 0; round < 2; round++)
                {
                    Hud.TourCloseAll();
                    yield return Wait(0.3f);
                    float worst = 0f;
                    Hud.TourPage(page);
                    for (int f = 0; f < 8; f++)
                    {
                        yield return null;
                        worst = Mathf.Max(worst, Time.unscaledDeltaTime * 1000f);
                    }
                    times.Add(worst);
                }
                Line($"opening {page}: slowest frame {times[0]:0} ms the first time, {times[1]:0} ms the next");
            }
            Hud.TourCloseAll();
            Line($"People on the map: {CampusPeople.Instance?.TourCount ?? 0} · managed memory {GC.GetTotalMemory(false) / (1024 * 1024)} MB · " +
                 $"total allocated {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)} MB");
        }

        IEnumerator Measure(string what, float seconds)
        {
            var frames = new List<float>();
            int gc = GC.CollectionCount(0);
            float until = Time.unscaledTime + seconds;
            yield return null;
            while (Time.unscaledTime < until)
            {
                frames.Add(Time.unscaledDeltaTime * 1000f);
                yield return null;
            }
            frames.Sort();
            float Pct(float p) => frames[Mathf.Clamp((int)(p * (frames.Count - 1)), 0, frames.Count - 1)];
            float avg = frames.Average();
            int slow = frames.Count(f => f > 33.4f);
            Line($"{what}: {1000f / avg:0} fps average · median {Pct(0.5f):0.0} ms · 95% {Pct(0.95f):0.0} ms · 99% {Pct(0.99f):0.0} ms · " +
                 $"worst {frames[frames.Count - 1]:0.0} ms · {slow} frames over 33 ms · {GC.CollectionCount(0) - gc} GCs");
        }

        // --- Playtest with a virtual mouse ----------------------------------------------------------

        IEnumerator Playtest()
        {
            _mouse = InputSystem.AddDevice<Mouse>("PlaytestMouse");
            _doc = FindFirstObjectByType<UIDocument>();

            // Found a school through the menus.
            yield return ClickText("New school");
            Check("title: New school opens the form", FindText("Next") != null);
            yield return ClickText("Next");
            Check("new school: Next opens the assistant pick", FindText("Start") != null);
            yield return ClickClass("ap-card");
            yield return ClickText("Start");
            yield return Wait(1f);
            Check("assistant pick: Start founds the school", Game.Phase == GamePhase.Playing && Game.Sim != null);
            if (Game.Sim == null) yield break;
            var sim = Game.Sim;
            var s = sim.State;
            Game.TourHold = true;   // the checks compare numbers; keep the days from moving under them
            yield return Snap("p01_founded");

            // The tutorial: the first letter, answered through the inbox.
            yield return ClickBubbleButton();
            yield return ClickNavTab("Inbox");
            Check("inbox tab opens the inbox", FindClass("ibx-option") != null);
            var letter = s.Inbox.FirstOrDefault(m => m.NeedsDecision);
            yield return ClickClass("ibx-option");
            yield return ClickText("Send reply");
            yield return Wait(0.5f);
            Check("inbox: picking an answer and sending it replies", letter != null && letter.Resolved, letter?.Subject);
            yield return Snap("p02_replied");
            yield return ClickText("skip tutorial");
            Check("tutorial: skip tutorial ends it", !sim.Tutorial.Active);
            yield return PressEscape();

            // Level 2 (through school XP, as in play) brings land, rules and admissions, and a card.
            sim.Progression.AddSchoolXp(Balance.SchoolXpForLevel(2) - s.Progress.SchoolXp);
            yield return Wait(0.5f);
            Check("level-up: the card shows", FindText("Yay!") != null);
            yield return Snap("p02b_level_up");
            yield return ClickText("Yay!");
            Check("level-up: Yay! closes it", FindText("Yay!") == null);

            s.Finance.Cash = Math.Max(s.Finance.Cash, 300000f);   // room to try everything

            // Build a dorm with the build palette and a click on the map.
            int rooms = s.Rooms.Count;
            yield return ClickNavTab("Build");
            Check("build tab opens build mode", CampusController.Instance.Tool == CampusTool.Build);
            var dorm = FindAll<Label>(l => l.text == "Dorm Room" && l.ClassListContains("bld-card-name")).FirstOrDefault();
            if (dorm != null) yield return ClickAt(dorm);
            if (sim.Facilities.TryFindPlacement(RoomType.DormRoom, out int bx, out int by, out int rot))
            {
                var footprint = Placement.Footprint(RoomCatalog.Get(RoomType.DormRoom), bx, by, rot);
                var centre = new Vector2(footprint.X + footprint.W / 2f, footprint.Y + footprint.H / 2f);
                CampusCamera.Instance.Frame(centre + new Vector2(3f, 0f), 8f);   // keep the spot clear of the build panel
                for (int turns = 0; turns < 4 && CampusController.Instance.Rotation != rot; turns++) yield return PressKey(Key.R);
                yield return ClickWorld(centre);
            }
            Check("build: clicking the map places the dorm", s.Rooms.Count == rooms + 1, $"{rooms} → {s.Rooms.Count} rooms");
            yield return Snap("p03_built");

            // Buy land from the parcel map.
            int parcels = s.Campus.OwnedParcelCount;
            if (CampusController.Instance.Tool != CampusTool.Build) yield return ClickNavTab("Build");   // placing a room leaves build mode
            yield return ClickText("Buy land");
            yield return ClickClass("bld-parcel--sale");
            Check("land: clicking a parcel for sale buys it", s.Campus.OwnedParcelCount == parcels + 1, $"{parcels} → {s.Campus.OwnedParcelCount} parcels");
            yield return PressEscape();
            Check("Esc leaves build mode", CampusController.Instance.Tool == CampusTool.Inspect);

            // Hire someone in the staff room.
            int staff = s.Staff.Count;
            yield return ClickNavTab("Staff");
            yield return ClickClass("stf-tab--b");
            var candidate = FindAll<VisualElement>(e => e.ClassListContains("stf-card-row") && !e.ClassListContains("stf-card-row--dim")).FirstOrDefault();
            if (candidate != null) yield return ClickAt(candidate);
            else Line("      (no candidate on screen)");
            var hire = FindAll<Label>(l => l.text != null && l.text.StartsWith("Hire ·") && l.ClassListContains("btn-label")).FirstOrDefault();
            if (hire != null) yield return ClickAt(hire);
            else Line("      (no Hire button on screen)");
            Check("staff: picking a candidate and Hire adds them", s.Staff.Count == staff + 1, $"{staff} → {s.Staff.Count} staff");
            yield return Snap("p04_hired");

            // Raise tuition with the stepper, then apply it.
            yield return ClickNavTab("Money");
            float before = s.Policies.PendingTuition >= 0 ? s.Policies.PendingTuition : s.Policies.AnnualTuition;
            var steps = FindAll<VisualElement>(e => e.ClassListContains("mon-step"));
            if (steps.Count >= 2) yield return ClickAt(steps[1]);
            var apply = FindAll<Label>(l => l.text != null && l.text.StartsWith("Apply in")).FirstOrDefault();
            if (apply != null) yield return ClickAt(apply);
            float after = s.Policies.PendingTuition >= 0 ? s.Policies.PendingTuition : s.Policies.AnnualTuition;
            Check("money: + and Apply plan a higher tuition", after > before, $"{before:N0} → {after:N0}");
            yield return Snap("p05_tuition");

            // Pick better food in the rules.
            yield return ClickNavTab("Rules");
            var good = FindAll<Label>(l => l.text == "Good").FirstOrDefault();
            if (good != null) yield return ClickAt(good);
            Check("rules: clicking Good sets the food", s.Policies.Food == FoodTier.Good, s.Policies.Food.ToString());
            yield return PressEscape();

            // Click a student on the map, open their card, follow them.
            Game.TourHold = false;
            var people = CampusPeople.Instance;
            yield return Wait(4f);
            long person = people != null ? people.TourPickNear(null, staff: false) : -1;
            if (person >= 0 && people.TryGetPosition(person, out var pos))
            {
                CampusCamera.Instance.Frame(pos, 7f);
                yield return Wait(0.2f);
                people.TryGetPosition(person, out pos);
                yield return ClickWorld(pos + new Vector2(0f, 0.45f));
            }
            Check("people: clicking a student selects them", CampusController.Instance.SelectedPerson >= 0);
            Check("people: their card shows", FindText("Open file") != null);
            yield return ClickText("Follow");
            Check("people: Follow makes the camera follow", CampusCamera.Instance.Following);
            yield return Snap("p06_person");
            yield return PressEscape();

            // Settings from the in-game menu: turn the music up.
            yield return PressEscape();
            Check("Esc opens the menu", FindText("Back to school") != null);
            yield return ClickText("Settings");
            float music = GameSettings.Music;
            var plus = FindAll<VisualElement>(e => e.ClassListContains("vol-btn")).ElementAtOrDefault(1);
            if (plus != null) yield return ClickAt(plus);
            Check("settings: + raises the music", GameSettings.Music > music || music >= 1f, $"{music:0.0} → {GameSettings.Music:0.0}");
            GameSettings.Music = music;
            yield return Snap("p07_settings");
            yield return ClickText("Done");
            yield return ClickText("Back to school");
            Check("menu closes again", FindText("Back to school") == null);

            // To the title and back.
            yield return PressEscape();
            yield return ClickText("Main menu");
            Check("Main menu goes to the title", Game.Phase == GamePhase.Title);
            yield return ClickText("Continue");
            Check("Continue goes back to the school", Game.Phase == GamePhase.Playing);
            yield return Snap("p08_back");
        }

        // --- Virtual mouse and keyboard -------------------------------------------------------------

        IEnumerator MoveTo(Vector2 screen)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen });
            yield return null;
            yield return null;
        }

        IEnumerator ClickScreen(Vector2 screen)
        {
            // The game runs in the background, but Unity drops UI clicks while its window isn't
            // focused, so give focus a moment to come back (someone clicked another window).
            for (float waited = 0f; !Application.isFocused && waited < 15f; waited += Time.unscaledDeltaTime)
                yield return null;
            _mouse.MakeCurrent();
            yield return MoveTo(screen);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }.WithButton(MouseButton.Left, true));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }.WithButton(MouseButton.Left, false));
            yield return null;
            yield return null;
            yield return Wait(0.25f);
        }

        IEnumerator ClickAt(VisualElement e) => ClickScreen(ScreenPoint(e));

        IEnumerator ClickWorld(Vector2 world)
        {
            var screen = Camera.main.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            yield return ClickScreen(new Vector2(screen.x, screen.y));
        }

        IEnumerator ClickText(string text)
        {
            var label = FindText(text);
            if (label == null)
            {
                Line($"      (nothing on screen says \"{text}\")");
                yield break;
            }
            yield return ClickAt(label);
        }

        IEnumerator ClickClass(string className)
        {
            var e = FindClass(className);
            if (e == null)
            {
                Line($"      (no .{className} on screen)");
                yield break;
            }
            yield return ClickAt(e);
        }

        IEnumerator ClickNavTab(string name)
        {
            var nav = _doc.rootVisualElement.Q("nav");
            var label = FindAll<Label>(l => l.text == name, nav).FirstOrDefault();
            if (label == null)
            {
                Line($"      (no \"{name}\" tab)");
                yield break;
            }
            yield return ClickAt(label);
        }

        /// <summary>The tutorial bubble's button, if it shows one ("Next", "Show me"...).</summary>
        IEnumerator ClickBubbleButton()
        {
            var bubble = FindClass("bubble");
            var button = bubble != null ? FindAll<VisualElement>(e => e.ClassListContains("btn"), bubble).FirstOrDefault() : null;
            if (button != null) yield return ClickAt(button);
        }

        IEnumerator PressKey(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) yield break;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return Wait(0.2f);
        }

        IEnumerator PressEscape() => PressKey(Key.Escape);

        // --- Finding things on screen ----------------------------------------------------------------

        Label FindText(string text) => FindAll<Label>(l => l.text == text).FirstOrDefault();

        VisualElement FindClass(string className) => FindAll<VisualElement>(e => e.ClassListContains(className)).FirstOrDefault();

        List<T> FindAll<T>(Func<T, bool> match, VisualElement scope = null) where T : VisualElement
        {
            var found = new List<T>();
            (scope ?? _doc.rootVisualElement).Query<T>().ForEach(e =>
            {
                if (match(e) && IsShown(e)) found.Add(e);
            });
            return found;
        }

        static bool IsShown(VisualElement e)
        {
            for (var x = e; x != null; x = x.parent)
                if (x.resolvedStyle.display == DisplayStyle.None || x.resolvedStyle.visibility == Visibility.Hidden || x.resolvedStyle.opacity < 0.05f)
                    return false;
            var b = e.worldBound;
            return b.width >= 1f && b.height >= 1f && !float.IsNaN(b.x);
        }

        /// <summary>The middle of an element in screen pixels, origin bottom left (what the input system uses).</summary>
        static Vector2 ScreenPoint(VisualElement e)
        {
            var size = e.panel.visualTree.layout.size;
            var c = e.worldBound.center;
            return new Vector2(c.x * Screen.width / size.x, Screen.height - c.y * Screen.height / size.y);
        }
    }
}
