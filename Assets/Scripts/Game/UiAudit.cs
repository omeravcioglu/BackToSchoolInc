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
    /// "-uiaudit &lt;folder&gt;": a screenshot pass over the notebook UI in the states the -uitour
    /// doesn't reach. It starts with a brand-new school (the empty pages a new player sees), then a
    /// busy one (clubs, teams, directors, a loan, a sponsor, applicants, a year report) with every
    /// page's tabs and filters, the room and person cards, and finally a school that closes. Buttons
    /// are pressed through <see cref="GameHud.TourPress"/>, so no mouse or window focus is needed.
    /// Writes audit.txt with anything it couldn't find, then quits.
    /// </summary>
    sealed class UiAudit : MonoBehaviour
    {
        string _folder;
        UIDocument _doc;
        readonly StringBuilder _report = new StringBuilder();
        int _shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Arg("-uiaudit") == null) return;
            var go = new GameObject("UiAudit");
            go.AddComponent<UiAudit>();
            DontDestroyOnLoad(go);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        void Start()
        {
            _folder = Arg("-uiaudit");
            Directory.CreateDirectory(_folder);
            foreach (var old in Directory.GetFiles(_folder, "*.png")) File.Delete(old);
            string saves = Path.Combine(_folder, "saves");
            if (Directory.Exists(saves)) Directory.Delete(saves, true);
            SaveSlots.Folder = saves;
            StartCoroutine(Run());
        }

        GameController Game => GameController.Instance;
        GameHud Hud => Game.GetComponent<GameHud>();

        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            _doc = FindFirstObjectByType<UIDocument>();
            yield return Act1NewSchool();
            yield return Act2BusySchool();
            yield return Act3Closing();
            File.WriteAllText(Path.Combine(_folder, "audit.txt"), _report.ToString());
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        // --- Act 1: a school founded a moment ago -------------------------------------------------

        IEnumerator Act1NewSchool()
        {
            Game.StartNewGame(new NewGameSetup { SchoolName = "Little Oak School", Seed = 31, Emblem = 3, Assistant = AssistantRules.Candidates(31)[1], Tutorial = false });
            Game.TourHold = true;
            var sim = Game.Sim;
            for (int d = 0; d < 3; d++) sim.AdvanceDay();
            Hud.TourCloseAll();
            yield return Snap("new_campus");
            foreach (var page in AllPages)
            {
                Hud.TourPage(page);
                yield return Snap("new_" + page);
            }
            Hud.TourCloseAll();
        }

        static readonly HudPage[] AllPages =
        {
            HudPage.Inbox, HudPage.News, HudPage.Students, HudPage.Staff, HudPage.Money, HudPage.Clubs, HudPage.Stats,
            HudPage.Ranks, HudPage.Rules, HudPage.Admissions, HudPage.Rooms, HudPage.Admin, HudPage.Report,
            HudPage.Reports, HudPage.Log,
        };

        // --- Act 2: a busy school ----------------------------------------------------------------

        IEnumerator Act2BusySchool()
        {
            var sim = Game.Sim;
            var s = sim.State;
            s.Progress.SchoolLevel = 12;
            s.Finance.Cash = 2_000_000f;
            for (int i = 0; i < 8; i++) sim.BuyLand();
            foreach (var type in new[] { RoomType.DormRoom, RoomType.DormRoom, RoomType.DormRoom, RoomType.Bathroom, RoomType.Classroom, RoomType.Classroom,
                         RoomType.Office, RoomType.ClubRoom, RoomType.ClubRoom, RoomType.Gym, RoomType.SoccerField, RoomType.BasketballCourt,
                         RoomType.Library, RoomType.ScienceLab, RoomType.NurseOffice, RoomType.StaffLounge, RoomType.StudentLounge,
                         RoomType.LaundryRoom, RoomType.Garden, RoomType.Tree, RoomType.Tree, RoomType.Bench, RoomType.Fountain })
            {
                var result = sim.BuildRoom(type);
                if (!result.Ok) Note($"couldn't build {type}: {result.Message}");
            }
            foreach (var r in s.Rooms) r.ConstructionDoneDay = Math.Min(r.ConstructionDoneDay, s.Day);
            s.Campus.RebuildOccupancy(s.Rooms);

            // Play most of the year, answering letters and keeping the money up.
            for (int d = 0; d < 300 && !s.GameOver; d++)
            {
                sim.AdvanceDay();
                AnswerSomeLetters(sim);
                if (s.Finance.Cash < 400_000f) s.Finance.Cash = 900_000f;
                if (d == 40) Staffing(sim);
                if (d == 60) ClubsAndTeams(sim);
            }
            sim.BuildRoom(RoomType.Classroom);   // something under construction
            sim.Economy.TakeLoan("Riverside Savings Bank", 60_000f, 2_750f, 24);
            s.Finance.Sponsorships.Add(new Sponsorship { Company = "Blue Kite Sports", Monthly = 1_500f, EndDay = s.Day + 300, Terms = "Logo on the jerseys" });
            sim.Stats.ComputeIndices();
            Hud.TourClearPopups();
            Hud.TourCloseAll();
            Note($"busy school: day {s.Day} ({Calendar.Format(s.Day)}), {s.Students.Count} students, {s.Staff.Count} staff, {s.Clubs.Count} clubs, {s.Teams.Count} teams, {s.Applicants.Count} applicants, {s.Reports.Count} reports, {s.Inbox.Count(m => m.NeedsDecision)} letters to answer");

            var people = CampusPeople.Instance;
            people?.TourSimulate(30f);
            CampusCamera.Instance.Frame(CampusView.Instance.CampusCenter(), 16f);
            yield return Snap("busy_campus");

            // Inbox
            Hud.TourPage(HudPage.Inbox);
            yield return Snap("inbox");
            yield return Press("To reply");
            yield return Snap("inbox_to_reply");
            var open = Shown<VisualElement>(e => e.ClassListContains("ibx-option")).FirstOrDefault();
            if (open != null)
            {
                GameHud.TourPress(open);
                yield return Snap("inbox_option_picked");
            }
            else Note("no reply options on screen");

            // News, statistics, rankings
            Hud.TourPage(HudPage.News);
            yield return Press("Us");
            yield return Snap("news_us");
            yield return Press("Rivals");
            yield return Snap("news_rivals");
            Hud.TourPage(HudPage.Stats);
            yield return Snap("stats");
            foreach (var tile in new[] { "Students", "Education", "Trust" })
            {
                yield return Press(tile);
                yield return Snap("stats_" + tile.ToLowerInvariant());
            }
            yield return Press("All");
            yield return Snap("stats_all_time");
            Hud.TourPage(HudPage.Ranks);
            foreach (var tab in new[] { "Academic", "Sports", "Size" })
            {
                yield return Press(tab);
                yield return Snap("ranks_" + tab.ToLowerInvariant());
            }

            // Students
            Hud.TourPage(HudPage.Students);
            yield return Snap("students");
            foreach (var tab in new[] { "Family", "Friends", "Diary" })
            {
                yield return Press(tab, startsWith: tab != "Family", within: "stu-right");
                yield return Snap("students_" + tab.ToLowerInvariant());
            }
            yield return Press("Move room");
            yield return Snap("students_move_room");
            Hud.TourPage(HudPage.Students);
            yield return Press("Swap", startsWith: true);
            yield return Snap("students_swap");
            Hud.TourPage(HudPage.Students);
            yield return Press("Expel", startsWith: true);
            yield return Snap("students_expel");
            Hud.TourPage(HudPage.Students);
            yield return ScrollPage();
            yield return Snap("students_scrolled");

            // Staff
            Hud.TourPage(HudPage.Staff);
            yield return Snap("staff");
            yield return Press("Let go", startsWith: true);
            yield return Snap("staff_let_go");
            Hud.TourPage(HudPage.Staff);
            yield return Press("+10%");
            yield return Snap("staff_raise");
            yield return Press("Hire", startsWith: true);
            yield return Snap("staff_hire");
            yield return ScrollPage();
            yield return Snap("staff_hire_scrolled");

            // Money
            Hud.TourPage(HudPage.Money);
            yield return Snap("money");
            yield return Press("So far");
            yield return Snap("money_so_far");
            yield return Press("Last month");
            yield return Snap("money_last_month");
            yield return ScrollPage();
            yield return Snap("money_scrolled");

            // Rules, administration, admissions, clubs
            Hud.TourPage(HudPage.Rules);
            yield return ScrollPage();
            yield return Snap("rules_scrolled");
            Hud.TourPage(HudPage.Admin);
            yield return Snap("admin");
            Hud.TourPage(HudPage.Admissions);
            yield return Snap("admissions");
            yield return ScrollPage();
            yield return Snap("admissions_scrolled");
            Hud.TourPage(HudPage.Clubs);
            yield return Snap("clubs");
            yield return ScrollPage();
            yield return Snap("clubs_scrolled");

            // Reports, log, rooms
            Hud.TourPage(HudPage.Report);
            yield return Snap("report");
            foreach (var line in new[] { "Education quality", "Dormitory quality", "Trust" })
            {
                yield return Press(line);
                yield return Snap("report_" + line.Split(' ')[0].ToLowerInvariant());
            }
            yield return ScrollPage();
            yield return Snap("report_scrolled");
            Hud.TourPage(HudPage.Reports);
            yield return Snap("year_reports");
            yield return ScrollPage();
            yield return Snap("year_reports_scrolled");
            Hud.TourPage(HudPage.Log);
            yield return Press("Important");
            yield return Snap("log_important");
            Hud.TourPage(HudPage.Rooms);
            yield return Press("Needs care");
            yield return Snap("rooms_needs_care");
            yield return Press("Being built");
            yield return Snap("rooms_being_built");
            Hud.TourCloseAll();

            // Room cards of every kind, and the people on them
            var ctl = CampusController.Instance;
            foreach (var type in new[] { RoomType.Classroom, RoomType.ClubRoom, RoomType.Office, RoomType.Cafeteria, RoomType.SoccerField, RoomType.Gym, RoomType.Tree, RoomType.Fountain })
            {
                var room = s.Rooms.FirstOrDefault(r => r.Type == type && r.IsBuilt(s.Day));
                if (room == null) { Note($"no built {type} for its card"); continue; }
                var f = room.Footprint;
                CampusCamera.Instance.Frame(new Vector2(f.X + f.W / 2f + 4f, f.Y + f.H / 2f), 9f);
                ctl.Select(room.Id);
                yield return Snap("card_" + type);
            }
            // A room with people in it right now, for the card's "who's here" strip.
            if (people != null)
            {
                var inside = new List<long>();
                Room busy = null;
                int most = 0;
                foreach (var r in s.Rooms)
                {
                    if (!r.IsBuilt(s.Day) || r.Def.Decor) continue;
                    people.PeopleIn(r, inside);
                    if (inside.Count > most) { most = inside.Count; busy = r; }
                }
                if (busy != null)
                {
                    var f = busy.Footprint;
                    CampusCamera.Instance.Frame(new Vector2(f.X + f.W / 2f + 4f, f.Y + f.H / 2f), 9f);
                    ctl.Select(busy.Id);
                    yield return Snap("card_with_people", 0.8f);
                }
                else Note("nobody inside any room for the who's-here shot");
            }
            var site = s.Rooms.LastOrDefault(r => !r.IsBuilt(s.Day));
            if (site != null)
            {
                ctl.Select(site.Id);
                yield return Snap("card_construction");
            }
            ctl.Select(-1);
            var coach = s.Staff.FirstOrDefault(m => m.Role == StaffRole.Coach) ?? s.Staff.FirstOrDefault(m => m.Role == StaffRole.Teacher);
            if (people != null && coach != null)
            {
                ctl.SelectPerson(CampusPeople.StaffKey(coach.Id));
                yield return Snap("card_staff_member");
                ctl.SelectPerson(-1);
            }

            // Menus
            Hud.TourSettings(true);
            yield return Press("credits", startsWith: true, anywhere: true);
            yield return Snap("settings_credits");
            Hud.TourSettings(false);
            Hud.TourLevelUp(3);
            yield return Snap("level_up_3");
            Hud.TourCloseAll();
            Hud.TourLevelUp(10);
            yield return Snap("level_up_10");
            Hud.TourCloseAll();

            // Into the next school year: last year's report and this year's draft side by side.
            for (int d = 0; d < 70 && !Calendar.IsSchoolMonth(s.Day); d++)
            {
                sim.AdvanceDay();
                AnswerSomeLetters(sim);
                if (s.Finance.Cash < 400_000f) s.Finance.Cash = 900_000f;
            }
            for (int d = 0; d < 20; d++)
            {
                sim.AdvanceDay();
                AnswerSomeLetters(sim);
            }
            Hud.TourClearPopups();
            Hud.TourPage(HudPage.Reports);
            yield return Press("This year");
            yield return Snap("year_reports_this_year");
            Hud.TourCloseAll();
        }

        static void AnswerSomeLetters(Simulation sim)
        {
            foreach (var m in sim.State.Inbox.ToArray())
            {
                if (!m.NeedsDecision || sim.Day - m.ReceivedDay < 3) continue;
                var usable = m.Options.Where(x => sim.Inbox.WhyNot(m, x) == null).ToList();
                var o = usable.FirstOrDefault(x => x.Id == m.AdvisorOptionId) ?? usable.FirstOrDefault();
                if (o != null) sim.Reply(m.Id, o.Id);
            }
        }

        void Staffing(Simulation sim)
        {
            var s = sim.State;
            foreach (var c in s.Candidates.ToArray())
            {
                var role = c.Staff.Role;
                int have = s.Staff.Count(m => m.Role == role);
                int want = role == StaffRole.Teacher ? 6 : role == StaffRole.Administrator ? 2 : 2;
                if (have >= want) continue;
                var r = sim.Hire(c, 3);
                if (!r.Ok) Note($"couldn't hire a {role}: {r.Message}");
            }
        }

        void ClubsAndTeams(Simulation sim)
        {
            foreach (var club in new[] { ClubType.Robotics, ClubType.Art, ClubType.Music, ClubType.Gaming })
            {
                var r = sim.CreateClub(club);
                if (!r.Ok) Note($"couldn't start {club}: {r.Message}");
            }
            foreach (var sport in new[] { SportType.Soccer, SportType.Basketball })
            {
                var r = sim.CreateTeam(sport);
                if (!r.Ok)
                {
                    Note($"couldn't start a {sport} team: {r.Message}");
                    continue;
                }
                var team = sim.State.Teams.Last();
                var coach = sim.State.Staff.FirstOrDefault(m => m.Role == StaffRole.Coach || m.Role == StaffRole.Teacher);
                if (coach != null) sim.SetCoach(team.Id, coach.Id);
                sim.AutoRoster(team.Id);
            }
        }

        // --- Act 3: the end of a school --------------------------------------------------------

        IEnumerator Act3Closing()
        {
            var s = Game.Sim.State;
            s.GameOver = true;
            s.GameOverReason = "The bank closed the account after three months in the red. The board has closed Little Oak School.";
            Game.TourHold = false;
            yield return Snap("game_over", 0.8f);
        }

        // --- Pressing, scrolling and screenshots ---------------------------------------------------

        IEnumerator Press(string text, bool startsWith = false, bool exact = false, string within = null, bool anywhere = false)
        {
            yield return Settle();   // a page opened this frame isn't laid out yet
            var scope = anywhere || within != null ? null : _doc?.rootVisualElement.Q("pageHost");
            if (scope != null && !IsShown(scope)) scope = null;
            bool Match(Label l) => l.text != null && (startsWith && !exact ? l.text.StartsWith(text) : l.text == text)
                && (within == null || HasAncestor(l, within)) && (scope == null || HasAncestor(l, scope));
            var labels = Shown<Label>(Match);
            bool pressed = false;
            foreach (var label in labels)
                if (GameHud.TourPress(label)) { pressed = true; break; }
            if (!pressed)
            {
                var all = new List<string>();
                _doc?.rootVisualElement.Query<Label>().ForEach(l =>
                {
                    if (l.text != null && l.text.StartsWith(text)) all.Add($"\"{l.text}\"{(IsShown(l) ? "" : " (hidden)")}");
                });
                Note($"couldn't press \"{text}\" (before shot {_shot + 1}); labels like it: {string.Join(", ", all.Take(6))}");
            }
            yield return Settle();
        }

        static bool HasAncestor(VisualElement e, VisualElement ancestor)
        {
            for (var x = e; x != null; x = x.parent)
                if (x == ancestor) return true;
            return false;
        }

        static bool HasAncestor(VisualElement e, string className)
        {
            for (var x = e; x != null; x = x.parent)
                if (x.ClassListContains(className)) return true;
            return false;
        }

        /// <summary>Scrolls every visible scroll view to its end.</summary>
        IEnumerator ScrollPage()
        {
            foreach (var sv in Shown<ScrollView>(_ => true))
            {
                float h = sv.contentContainer.layout.height - sv.contentViewport.layout.height;
                if (h > 1f) sv.scrollOffset = new Vector2(sv.scrollOffset.x, h);
            }
            yield return Settle();
        }

        List<T> Shown<T>(Func<T, bool> match) where T : VisualElement
        {
            var found = new List<T>();
            if (_doc == null) return found;
            _doc.rootVisualElement.Query<T>().ForEach(e =>
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

        static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.15f);
        }

        IEnumerator Snap(string name, float wait = 0.3f)
        {
            yield return Settle();
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, $"{++_shot:000}_{name}.png"));
            yield return null;
            yield return null;
        }

        void Note(string text)
        {
            _report.AppendLine(text);
            Debug.Log("[ui audit] " + text);
        }
    }
}
