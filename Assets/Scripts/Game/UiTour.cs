using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>
    /// Automated smoke test for built players. "-uitour &lt;folder&gt;" walks the title menu, founds
    /// a school with an assistant and the tutorial, plays a few months (answering messages with
    /// the assistant's advice), grows the campus, screenshots the people, the campus tools, every
    /// page and pop-up, then quits.
    /// Adding "-gallery" instead builds one of every room at its lowest and highest level and
    /// photographs them, to review the room drawings.
    /// </summary>
    sealed class UiTour : MonoBehaviour
    {
        string _folder;

        public static string RequestedFolder()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-uitour");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static bool GalleryRequested() => Array.IndexOf(Environment.GetCommandLineArgs(), "-gallery") >= 0;

        void Start()
        {
            _folder = RequestedFolder();
            if (_folder == null) { Destroy(this); return; }
            Directory.CreateDirectory(_folder);
            // Saves go to the tour's own folder, starting empty, never the player's.
            string saves = Path.Combine(_folder, "saves");
            if (Directory.Exists(saves)) Directory.Delete(saves, true);
            SaveSlots.Folder = saves;
            StartCoroutine(GalleryRequested() ? Gallery() : Tour());
        }

        IEnumerator Settle()
        {
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
        }

        /// <summary>Lets the UI catch up, takes the screenshot, and waits for the capture before anything changes.</summary>
        IEnumerator Snap(string name, float wait = 0.3f)
        {
            yield return Settle();
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            Shot(name);
            yield return null;
            yield return null;
        }

        static void AnswerWithAdvice(Simulation sim)
        {
            foreach (var m in sim.State.Inbox.ToArray())
            {
                if (!m.NeedsDecision || sim.Day - m.ReceivedDay < 2) continue;
                var usable = m.Options.Where(x => sim.Inbox.WhyNot(m, x) == null).ToList();
                var o = usable.FirstOrDefault(x => x.Id == m.AdvisorOptionId) ?? usable.FirstOrDefault();
                if (o != null) sim.Reply(m.Id, o.Id);
            }
        }

        IEnumerator Tour()
        {
            var game = GameController.Instance;
            var ui = GetComponent<DebugUI>();
            var hud = GetComponent<GameHud>();
            var ctl = CampusController.Instance;
            var view = CampusView.Instance;
            var cam = CampusCamera.Instance;
            yield return Snap("00_title");

            if (hud != null)
            {
                hud.TourNewSchool("Little Oak School", 3, 0);
                yield return Snap("01_new_school");
                hud.TourAssistant(1);
                yield return Snap("02_assistant_pick");
                hud.TourStart(tutorial: true);
            }
            else game.StartNewGame(new NewGameSetup { SchoolName = "Little Oak School", Tutorial = true, Assistant = AssistantRules.Candidates(7)[1] });

            game.TourHold = true;
            var sim = game.Sim;
            var s = sim.State;
            yield return Snap("03_tutorial_hello");
            sim.TutorialSignal("next");
            yield return Snap("04_tutorial_inbox");
            hud?.TourPage(HudPage.Inbox);
            sim.TutorialSignal("open:inbox");
            yield return Snap("04b_tutorial_decide");
            hud?.TourCloseAll();

            // Skip the rest of the tutorial and play ~4 months so the school has some history.
            sim.SkipTutorial();
            for (int d = 0; d < 125; d++)
            {
                sim.AdvanceDay();
                AnswerWithAdvice(sim);
            }

            // Grow the campus: more land, outdoor spaces, a sports complex and some decorations.
            s.Finance.Cash = Math.Max(s.Finance.Cash, 900000f);
            s.Progress.SchoolLevel = Math.Max(s.Progress.SchoolLevel, 8);
            foreach (var (px, py) in new[] { (4, 1), (4, 2), (2, 3), (3, 3), (4, 3) })
                if (s.Campus.IsParcelPurchasable(px, py)) sim.BuyParcel(px, py);
            var outdoors = new List<Room>();
            foreach (var type in new[] { RoomType.SportsComplex, RoomType.PracticeField, RoomType.Playground, RoomType.Garden, RoomType.PicnicArea,
                         RoomType.Fountain, RoomType.Statue, RoomType.Tree, RoomType.Tree, RoomType.Tree, RoomType.FlowerBed, RoomType.FlowerBed,
                         RoomType.Bench, RoomType.Bench })
            {
                var result = sim.BuildRoom(type);
                if (!result.Ok) Debug.LogWarning($"Tour: couldn't build {type}: {result.Message}");
                else outdoors.Add(s.Rooms[s.Rooms.Count - 1]);
            }
            foreach (var r in outdoors) r.ConstructionDoneDay = s.Day;
            sim.BuildRoom(RoomType.StudentLounge);      // something under construction
            s.Campus.RebuildOccupancy(s.Rooms);
            sim.Stats.ComputeIndices();
            ui.TourShow(0);
            hud?.TourCloseAll();
            var people = CampusPeople.Instance;
            yield return Settle();
            people?.TourSimulate(40f);
            cam.Frame(view.CampusCenter() + new Vector2(4f, 2f), 16f);
            yield return Snap("05_campus");
            hud?.TourBubble("Classrooms are packed. It's in your inbox.");
            yield return Snap("06_assistant_bubble");
            hud?.TourCloseAll();

            // Close-ups of people around the campus
            var playground = s.Rooms.FirstOrDefault(r => r.Type == RoomType.Playground);
            var field = s.Rooms.FirstOrDefault(r => r.Type == RoomType.PracticeField || r.Type == RoomType.SportsComplex);
            var cafeteria = s.Rooms.FirstOrDefault(r => r.Type == RoomType.Cafeteria);
            int shot = 0;
            foreach (var r in new[] { playground, field, cafeteria })
            {
                if (r == null) continue;
                var f = r.Footprint;
                cam.Frame(new Vector2(f.X + f.W / 2f, f.Y + f.H / 2f - 1f), 6f);
                people?.TourSimulate(3f);
                yield return Snap($"05{(char)('b' + shot++)}_people_{r.Type}");
            }

            // A student's and a staff member's cards, and the student's file
            if (people != null && hud != null)
            {
                long student = people.TourPickNear(playground, staff: false);
                if (student >= 0)
                {
                    ctl.SelectPerson(student);
                    if (people.TryGetPosition(student, out var at)) cam.Frame(at + new Vector2(0f, 1.5f), 6.5f);
                    yield return Snap("10b_student_card", 0.6f);
                    hud.TourOpenPersonFile();
                    yield return Snap("30_student_file");
                    hud.TourCloseAll();
                }
                long staff = people.TourPickNear(cafeteria, staff: true);
                if (staff >= 0)
                {
                    ctl.SelectPerson(staff);
                    if (people.TryGetPosition(staff, out var at)) cam.Frame(at + new Vector2(0f, 1.5f), 6.5f);
                    yield return Snap("10c_staff_card", 0.6f);
                }
                ctl.SelectPerson(-1);
            }

            // Build mode: a valid spot, then an invalid one on top of an existing room.
            cam.Frame(view.CampusCenter(), 10f);
            if (sim.Facilities.TryFindPlacement(RoomType.DormRoom, out int bx, out int by, out int brot))
            {
                ctl.BeginBuild(RoomType.DormRoom);
                var f = Placement.Footprint(RoomCatalog.Get(RoomType.DormRoom), bx, by, brot);
                ctl.ForcePointerCell(new Vector2Int(f.X + (f.W - 1) / 2, f.Y + (f.H - 1) / 2), brot);
                yield return Snap("07_build_valid");
                var cafe = s.Rooms.First(r => r.Type == RoomType.Cafeteria);
                ctl.ForcePointerCell(new Vector2Int(cafe.X + 2, cafe.Y + 2), 0);
                yield return Snap("08_build_blocked");
            }
            foreach (var (type, name) in new[] { (RoomType.Playground, "07b_build_outdoors"), (RoomType.SportsComplex, "07c_build_sports"), (RoomType.Fountain, "07d_build_decor") })
            {
                ctl.BeginBuild(type);
                if (sim.Facilities.TryFindPlacement(type, out int tx, out int ty, out int trot))
                {
                    var f = Placement.Footprint(RoomCatalog.Get(type), tx, ty, trot);
                    ctl.ForcePointerCell(new Vector2Int(f.X + (f.W - 1) / 2, f.Y + (f.H - 1) / 2), trot);
                }
                yield return Snap(name);
            }

            ctl.BeginLand();
            var map = s.Campus;
            for (int py = 0; py < map.ParcelsY; py++)
                for (int px = 0; px < map.ParcelsX; px++)
                    if (map.IsParcelPurchasable(px, py) && px > NewGame.StartParcelX)
                    {
                        ctl.ForcePointerCell(new Vector2Int(px * CampusMap.ParcelSize + 5, py * CampusMap.ParcelSize + 5));
                        py = map.ParcelsY;
                        break;
                    }
            cam.Frame(view.CampusCenter() + new Vector2(6f, 0f), 17f);
            yield return Snap("09_land");

            ctl.ForcePointerCell(null);
            ctl.Cancel();
            cam.Frame(view.CampusCenter(), 10f);
            var dorm = s.Rooms.First(r => r.Type == RoomType.DormRoom);
            ctl.Select(dorm.Id);
            yield return Snap("10_selected");
            if (playground != null)
            {
                ctl.Select(playground.Id);
                yield return Snap("10a_selected_playground");
            }
            ctl.Select(-1);

            // Every notebook page
            if (hud != null)
            {
                int n = 11;
                foreach (var page in new[] { HudPage.News, HudPage.Stats, HudPage.Ranks, HudPage.Inbox, HudPage.Students, HudPage.Staff,
                             HudPage.Money, HudPage.Rules, HudPage.Admin, HudPage.Admissions, HudPage.Clubs, HudPage.Report,
                             HudPage.Reports, HudPage.Log, HudPage.Rooms })
                {
                    hud.TourPage(page);
                    yield return Snap($"{n++}_{page}");
                }
                hud.TourCloseAll();

                hud.TourMore(true);
                yield return Snap("40_more_menu");
                hud.TourMore(false);
                hud.TourGameMenu(true);
                yield return Snap("41_game_menu");
                hud.TourSettings(true);
                yield return Snap("42_settings");
                hud.TourSettings(false);
                hud.TourLevelUp(2);
                yield return Snap("43_level_up");
                hud.TourCloseAll();
                hud.TourPlain(true);
                yield return Snap("44_plain_debug_view");
                hud.TourPlain(false);
                game.ShowTitle();
                yield return Snap("45_title_continue");

                // Saving and loading: the school leaving for the title went to disk; it must read
                // back as the very same school. A second school fills another slot for the Load screen.
                SaveSlots.Flush();
                bool sameOnDisk = SaveGame.Write(SaveSlots.Load(game.Slot)) == SaveGame.Write(sim);
                var other = NewGame.Create(new NewGameSetup { SchoolName = "Riverside College", Seed = 4242, Emblem = 2, CrestColor = 3, Assistant = AssistantRules.Candidates(4242)[0] });
                for (int d = 0; d < 400; d++)
                {
                    other.AdvanceDay();
                    AnswerWithAdvice(other);
                }
                int otherSlot = SaveSlots.SlotForNewSchool();
                SaveSlots.Write(otherSlot, other, wait: true);
                hud.TourOpenLoad();
                yield return Snap("46_load");
                hud.TourLoadPick(otherSlot);
                hud.TourLoadAskDelete(true);
                yield return Snap("46b_load_delete");
                hud.TourLoadAskDelete(false);
                hud.TourLoadGo();
                bool loaded = game.Sim != null && game.Sim != sim && game.Sim.State.SchoolName == "Riverside College"
                    && SaveGame.Write(game.Sim) == SaveGame.Write(other);
                game.SaveNow();   // shows the clock's "saved" stamp
                cam.Frame(view.CampusCenter(), 10f);
                yield return Snap("47_loaded_school", 0.6f);
                game.ShowTitle();
                yield return Snap("48_title_after_load");

                // A school that closed stays as a keepsake (stamped, can't be loaded) and is the first
                // to make way when a new school needs a slot.
                var closed = NewGame.Create(new NewGameSetup { SchoolName = "Hilltop Academy", Seed = 99, Emblem = 4, CrestColor = 1, Assistant = AssistantRules.Candidates(99)[2] });
                for (int d = 0; d < 60; d++) closed.AdvanceDay();
                closed.State.GameOver = true;
                closed.State.GameOverReason = "The board closed the school.";
                SaveSlots.Write(SaveSlots.SlotForNewSchool(), closed, wait: true);
                hud.TourOpenLoad();
                yield return Snap("46c_load_full");
                hud.TourNewSchool("Pine Ridge School", 1, 2);
                yield return Snap("49_new_school_replaces");
                File.WriteAllText(Path.Combine(_folder, "saves.txt"),
                    $"same on disk: {sameOnDisk}\nloaded other slot: {loaded}\nslots: " +
                    string.Join(", ", SaveSlots.All.Select(x => x.Empty ? "empty" : x.Damaged ? "damaged" : $"{x.Summary.SchoolName} day {x.Summary.Day}")));
            }
            File.WriteAllText(Path.Combine(_folder, "done.txt"), "ok " + Calendar.Format(sim.Day));
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        /// <summary>Builds every room type at level 1 and at its top level, then photographs them.</summary>
        IEnumerator Gallery()
        {
            var game = GameController.Instance;
            var ui = GetComponent<DebugUI>();
            game.StartNewGame(new NewGameSetup { SchoolName = "Gallery School" });
            game.TourHold = true;
            var sim = game.Sim;
            var s = sim.State;
            yield return Settle();

            s.Progress.SchoolLevel = 30;
            s.Finance.Cash = 1e9f;
            sim.Facilities.OpenEstate();
            for (int py = 0; py < s.Campus.ParcelsY; py++)
                for (int px = 0; px < s.Campus.ParcelsX; px++)
                    s.Campus.SetParcelOwned(px, py, true);

            var shown = new List<Room>();
            foreach (var def in RoomCatalog.All.OrderByDescending(d => d.Area))
            {
                if (!def.Buildable) continue;
                if (def.Signature != Specialization.None) s.Specialization = def.Signature;   // each identity's own building
                foreach (int level in def.MaxLevel > 1 ? new[] { 1, def.MaxLevel } : new[] { 1 })
                {
                    var room = sim.Facilities.BuildAuto(def.Type, out string why);
                    if (room == null) { Debug.LogWarning($"Gallery: couldn't place {def.Name}: {why}"); continue; }
                    room.Level = level;
                    room.ConstructionDoneDay = s.Day;
                    room.Name = $"{def.Name} L{level}";
                    shown.Add(room);
                }
            }
            var manor = s.Rooms.First(r => r.Type == RoomType.Manor);   // comes with the estate, restored to its top level
            manor.Level = 3;
            manor.Name = "Old Manor L3";
            shown.Add(manor);
            // One site under construction and one upgrade in progress, to show the signs.
            var site = sim.Facilities.BuildAuto(RoomType.Library, out _);
            var upgrading = s.Rooms.First(r => r.Type == RoomType.Classroom && r.Level == 1);
            upgrading.PendingLevel = 2;
            upgrading.ConstructionStartDay = s.Day - 3;
            upgrading.ConstructionDoneDay = s.Day + 4;
            s.Campus.RebuildOccupancy(s.Rooms);
            sim.Stats.ComputeIndices();
            ui.TourShow(0);

            CampusCamera.Instance.Frame(new Vector2(s.Campus.Width / 2f, s.Campus.Height / 2f), 26f);
            yield return Snap("gallery_00_overview");

            int n = 1;
            foreach (var r in shown.Concat(site != null ? new[] { site } : Array.Empty<Room>()))
            {
                var f = r.Footprint;
                float size = Mathf.Max(3.2f, Mathf.Max(f.H * 0.62f, f.W * 0.36f));
                CampusCamera.Instance.Frame(new Vector2(f.X + f.W / 2f + size * 0.35f, f.Y + f.H / 2f), size);
                yield return Snap($"gallery_{n++:00}_{r.Type}_L{r.Level}");
            }
            File.WriteAllText(Path.Combine(_folder, "done.txt"), $"gallery ok, {shown.Count} rooms");
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"));
    }
}
