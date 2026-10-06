using System;
using System.Collections.Generic;
using System.Linq;
using SchoolSim;

// Headless balance/crash harness: plays N years with a scripted director and prints monthly stats.
// Usage: dotnet run -- <years> <seed> <mode> [-v] [-dump] [-assistant[=Style]] [-news]
//   modes: smart (competent director), rich (starts with $1.5M, builds everything),
//          random (random replies, no management), first (always the first reply), passive (never replies)
//   -v prints important log lines; -dump prints the education breakdown twice a year.
//   -assistant hires the first candidate (or the named style, e.g. -assistant=Thrifty); smart mode
//   then takes the assistant's advice on the problems they bring. -news prints every story.
static class Program
{
    static int Main(string[] args)
    {
        int years = args.Length > 0 ? int.Parse(args[0]) : 4;
        int seed = args.Length > 1 ? int.Parse(args[1]) : 12345;
        string mode = args.Length > 2 ? args[2] : "smart";   // smart | passive | random
        bool verbose = args.Contains("-v");

        var setup = new NewGameSetup { SchoolName = "Little Oak School", Seed = seed };
        var assistantArg = args.FirstOrDefault(a => a.StartsWith("-assistant"));
        if (assistantArg != null)
        {
            var candidates = AssistantRules.Candidates(seed);
            int eq = assistantArg.IndexOf('=');
            setup.Assistant = eq < 0 ? candidates[0]
                : candidates.FirstOrDefault(c => c.Style.ToString().Equals(assistantArg.Substring(eq + 1), StringComparison.OrdinalIgnoreCase))
                  ?? new Assistant { Name = "Sam Reed", Style = Enum.Parse<AssistantStyle>(assistantArg.Substring(eq + 1), true), Skill = 3 };
            if (setup.Assistant.Salary <= 0f) setup.Assistant.Salary = AssistantRules.Salary(setup.Assistant.Style, setup.Assistant.Skill);
            Console.WriteLine($"Assistant: {setup.Assistant.Name}, {AssistantRules.Word(setup.Assistant.Style)}, skill {setup.Assistant.Skill}, {setup.Assistant.Salary:N0}/mo");
        }
        if (args.Contains("-nogating")) foreach (var def in RoomCatalog.All) def.MinSchoolLevel = 1;   // compare with the old, all-rooms-at-once start
        var sim = NewGame.Create(setup);
        if (args.Contains("-news")) sim.NewsPublished += n => Console.WriteLine($"   NEWS [{Calendar.Format(n.Day)}] {n.Tone,-7} {n.Headline}{(n.Effect != 0 ? $" ({n.Effect:+0.0;-0.0} {n.EffectRep})" : "")}");
        if (mode == "rich") sim.State.Finance.Cash = 1500000f;
        var rng = new Random(seed);
        sim.YearReportReady += r =>
        {
            Console.WriteLine($"\n===== YEAR {r.Year} REPORT =====\n{YearCycleSystem.Describe(r)}");
            var ledgers = sim.State.Finance.History.Skip(Math.Max(0, sim.State.Finance.History.Count - 12)).ToList();
            var parts = Enum.GetValues(typeof(LedgerCategory)).Cast<LedgerCategory>()
                .Select(c => (c, v: ledgers.Sum(l => l[c]))).Where(x => Math.Abs(x.v) > 1).OrderBy(x => x.v)
                .Select(x => $"{x.c} {x.v:N0}");
            Console.WriteLine("LEDGER (last 12 months): " + string.Join(" | ", parts));
        };
        if (verbose) sim.Logged += e => { if (e.Important) Console.WriteLine($"   [{Calendar.Format(e.Day)}] {e.Text}"); };

        Header();
        Line(sim);
        int days = years * Calendar.DaysPerYear;
        try
        {
            for (int d = 0; d < days && !sim.State.GameOver; d++)
            {
                sim.AdvanceDay();
                if (mode != "passive") AnswerInbox(sim, rng, mode);
                if ((mode == "smart" || mode == "rich") && Calendar.DayOfWeek(sim.Day) == 0) Manage(sim, mode == "rich");
                if (Calendar.IsFirstDayOfMonth(sim.Day)) Line(sim);
                if (args.Contains("-dump") && Calendar.IsFirstDayOfMonth(sim.Day) && (Calendar.Month(sim.Day) == 5 || Calendar.Month(sim.Day) == 12))
                {
                    Console.WriteLine("  STUDENT SATISFACTION:" + Environment.NewLine + sim.Stats.StatFactors[(int)StatId.StudentSatisfaction].Describe(14));
                    Console.WriteLine("  PARENT SATISFACTION:" + Environment.NewLine + sim.Stats.StatFactors[(int)StatId.ParentSatisfaction].Describe(12));
                    Console.WriteLine("  EDU: " + Calendar.Format(sim.Day) + Environment.NewLine + sim.Stats.StatFactors[(int)StatId.EducationQuality].Describe());
                    foreach (var t in sim.State.Staff.Where(x => x.Role == StaffRole.Teacher))
                        Console.WriteLine($"    {t.Name} skill {t.Skill:0} energy {t.Energy:0} morale {t.Morale:0} away {t.IsAway(sim.Day)} eff {sim.Stats.TeacherEffect(t):0}");
                    Console.WriteLine($"    seats {sim.Indices.ClassroomSeats} students {sim.Indices.Students} ratio {sim.Indices.StudentsPerTeacher:0.0}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRASH on {Calendar.Format(sim.Day)}: {ex}");
            return 1;
        }

        Console.WriteLine();
        if (sim.State.GameOver) Console.WriteLine("GAME OVER: " + sim.State.GameOverReason);
        Summary(sim);
        if (args.Contains("-savetest") && !SaveRoundTrip(sim)) return 2;
        return 0;
    }

    /// <summary>Saves, loads and re-saves the school, then plays both copies on and checks they stay identical.</summary>
    static bool SaveRoundTrip(Simulation sim)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        string first = SaveGame.Write(sim);
        long writeMs = watch.ElapsedMilliseconds;
        watch.Restart();
        var loaded = SaveGame.Read(first);
        long readMs = watch.ElapsedMilliseconds;
        string second = SaveGame.Write(loaded);
        Console.WriteLine();
        Console.WriteLine($"SAVE: {first.Length / 1024} KB, write {writeMs} ms, read {readMs} ms, round trip {(first == second ? "identical" : "DIFFERENT")}");
        if (first != second)
        {
            int i = 0;
            while (i < first.Length && i < second.Length && first[i] == second[i]) i++;
            Console.WriteLine("  first difference at " + i + ": " + first.Substring(Math.Max(0, i - 120), Math.Min(240, first.Length - Math.Max(0, i - 120))));
            Console.WriteLine("  vs: " + second.Substring(Math.Max(0, i - 120), Math.Min(240, second.Length - Math.Max(0, i - 120))));
            return false;
        }
        bool same = true;
        for (int d = 0; d < 90 && !sim.State.GameOver; d++)
        {
            sim.AdvanceDay();
            loaded.AdvanceDay();
            string a = SaveGame.Write(sim), b = SaveGame.Write(loaded);
            if (a == b) continue;
            same = false;
            int i = 0;
            while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
            Console.WriteLine($"SAVE: diverged on day {d + 1} at {i}:");
            Console.WriteLine("  original: " + a.Substring(Math.Max(0, i - 200), Math.Min(300, a.Length - Math.Max(0, i - 200))));
            Console.WriteLine("  loaded:   " + b.Substring(Math.Max(0, i - 200), Math.Min(300, b.Length - Math.Max(0, i - 200))));
            break;
        }
        Console.WriteLine($"SAVE: after 90 more days the loaded school is {(same ? "identical" : "DIFFERENT")} to the original");
        return same;
    }

    static void Header()
    {
        Console.WriteLine("Date         Cash      Net/mo  Stu Beds Staff Lvl Pres Rank | Edu  Food Dorm Safe Life StuS ParS Mor Trust | Inbox");
    }

    static void Line(Simulation sim)
    {
        var s = sim.State;
        var last = s.Finance.LastCompletedMonth;
        Console.WriteLine(
            $"{Calendar.Format(sim.Day),-11} {s.Finance.Cash,9:N0} {(last?.Net ?? 0),8:N0} {s.Students.Count,4} {sim.Indices.Beds,4} {s.Staff.Count,5} {s.Progress.SchoolLevel,3} {s.Prestige,4:0} {s.NationalRank,4} | " +
            $"{s.Stat(StatId.EducationQuality),4:0} {s.Stat(StatId.FoodQuality),5:0} {s.Stat(StatId.DormitoryQuality),4:0} {s.Stat(StatId.SafetyDiscipline),4:0} {s.Stat(StatId.StudentLife),4:0} " +
            $"{s.Stat(StatId.StudentSatisfaction),4:0} {s.Stat(StatId.ParentSatisfaction),4:0} {s.Stat(StatId.StaffMorale),3:0} {s.Stat(StatId.Trust),5:0} | {sim.Inbox.PendingDecisions}");
    }

    static readonly Dictionary<string, int> Seen = new Dictionary<string, int>();

    static void AnswerInbox(Simulation sim, Random rng, string mode)
    {
        foreach (var m in sim.State.Inbox.ToArray())
        {
            if (!m.NeedsDecision) continue;
            // Leave ~25% of messages unanswered for a few days to exercise expiry and admin paths.
            if (mode != "first" && sim.Day - m.ReceivedDay < 2 && rng.NextDouble() < 0.5) continue;
            var usable = m.Options.Where(o => sim.Inbox.WhyNot(m, o) == null).ToList();
            if (usable.Count == 0) continue;
            MessageOption pick;
            if (mode == "random") pick = usable[rng.Next(usable.Count)];
            else if (mode == "first") { pick = usable[0]; if (sim.Day - m.ReceivedDay < 0) continue; }
            else if (m.AdvisorOptionId != null && AssistantSystem.IsProblem(m) && usable.Any(o => o.Id == m.AdvisorOptionId))
                pick = usable.First(o => o.Id == m.AdvisorOptionId);   // a sensible director listens to their assistant
            else pick = SmartPick(sim, m, usable, rng);
            var r = sim.Reply(m.Id, pick.Id);
            Seen[m.TemplateId + ":" + pick.Id] = Seen.TryGetValue(m.TemplateId + ":" + pick.Id, out int n) ? n + 1 : 1;
            if (!r.Ok) Console.WriteLine($"  reply failed: {m.TemplateId}/{pick.Id}: {r.Message}");
        }
    }

    static MessageOption SmartPick(Simulation sim, InboxMessage m, List<MessageOption> usable, Random rng)
    {
        string[] prefs =
        {
            "compensate", "move", "meeting", "reassure", "approve", "accept", "discipline", "counseling", "detention",
            "cleanup", "fail", "admit", "aid", "partial", "counter", "renew3", "renew1", "leave", "emergency", "tech", "deepclean",
            "quarantine", "apply", "scholarship", "fans", "loan40", "enjoy", "statement", "rules", "medical", "repairs", "budget", "reject",
        };
        if (m.TemplateId == "raise_request")
        {
            var st = sim.State.FindStaff(m.StaffId);
            bool small = st != null && m.Amount / st.Salary - 1f <= 0.06f;
            var o = usable.FirstOrDefault(x => x.Id == (small ? "approve" : "partial"));
            if (o != null) return o;
        }
        // The specialization follows the seed, so every signature building gets played.
        if (m.TemplateId == "specialization_choice")
        {
            var o = usable.FirstOrDefault(x => x.Id == ((Specialization)(1 + Math.Abs(sim.Rng.Seed) % 6)).ToString());
            if (o != null) return o;
        }
        // The old estate is bought from Manage, where a reserve is kept; a grand gala more than pays for itself.
        if (m.TemplateId == "estate_offer") return usable.First(x => x.Id == "later");
        if (m.TemplateId == "founders_gala") return usable.FirstOrDefault(x => x.Id == "grand") ?? usable.First(x => x.Id == "skip");
        foreach (var p in prefs)
        {
            var o = usable.FirstOrDefault(x => x.Id == p);
            if (o != null && (o.Cost <= 0 || sim.State.Finance.Cash > o.Cost + 15000)) return o;
        }
        // A careful director avoids promises they can't keep and never ignores people.
        var safe = usable.Where(o => o.Id != "promise" && o.Id != "review" && o.Id != "ignore" && o.Id != "coverup").ToList();
        if (safe.Count > 0) return safe[rng.Next(safe.Count)];
        return usable[rng.Next(usable.Count)];
    }

    /// <summary>A crude "competent director": expands beds, hires to ratios, admits students.</summary>
    static void Manage(Simulation sim, bool rich)
    {
        var s = sim.State;
        var ix = sim.Indices;
        float cash = s.Finance.Cash;

        // Auto-place a room; if the owned land is full, buy the cheapest nearby parcel and try again. Beds
        // can't wait, so a dorm gets its land as soon as the money covers both.
        void Build(RoomType type, string what)
        {
            var result = sim.BuildRoom(type);
            float land = sim.Facilities.FindLandToBuy(out int px, out int py) ? sim.Facilities.ParcelCost(px, py) : sim.Facilities.LandCost;
            if (!result.Ok && result.Message != null && result.Message.Contains("Buy more land")
                && s.Finance.Cash > land + RoomCatalog.Get(type).Cost[0] + (type == RoomType.DormRoom ? 5000 : 20000))
            {
                Try(sim.BuyLand(), "buy land");
                result = sim.BuildRoom(type);
            }
            Try(result, what);
        }

        // Beds: build a dorm when nearly full and affordable.
        if (ix.FreeBeds <= (rich ? 4 : 1) && cash > 25000 && !s.Rooms.Any(r => r.Type == RoomType.DormRoom && !r.IsBuilt(sim.Day)))
            Build(RoomType.DormRoom, "build dorm");
        if (ix.BathroomShortage > 0.05f && cash > 30000 && !s.Rooms.Any(r => r.Type == RoomType.Bathroom && !r.IsBuilt(sim.Day)))
            Build(RoomType.Bathroom, "build bathroom");
        if (ix.ClassroomSeats < ix.Students + 4 && cash > 20000 && !s.Rooms.Any(r => r.Type == RoomType.Classroom && !r.IsBuilt(sim.Day)))
            Build(RoomType.Classroom, "build classroom");
        if (ix.MealCapacity < ix.Students + 2 && cash > 40000)
        {
            var caf = s.Rooms.FirstOrDefault(r => r.Type == RoomType.Cafeteria);
            if (caf != null && sim.Facilities.CanUpgrade(caf, out _)) Try(sim.UpgradeRoom(caf.Id), "upgrade cafeteria");
        }

        // Nice-to-haves once solvent.
        if (cash > 80000 && ix.Students > 20)
        {
            foreach (var t in new[] { RoomType.LaundryRoom, RoomType.StudentLounge, RoomType.Library, RoomType.StaffLounge, RoomType.NurseOffice, RoomType.SecurityRoom, RoomType.BasketballCourt })
                if (!s.Rooms.Any(r => r.Type == t)) { Build(t, "build " + t); break; }
        }
        if (cash > 120000)
        {
            var worst = s.Rooms.Where(r => r.IsBuilt(sim.Day) && (r.Type == RoomType.Classroom || r.Type == RoomType.DormRoom) && sim.Facilities.CanUpgrade(r, out _))
                .OrderBy(r => r.Level).FirstOrDefault();
            if (worst != null) Try(sim.UpgradeRoom(worst.Id), "upgrade " + worst.Name);
        }
        foreach (var r in s.Rooms.Where(r => r.Condition < 40 && sim.Facilities.RepairCost(r) < cash - 30000).ToList())
            Try(sim.RepairRoom(r.Id), "repair");

        // Staff to ratios.
        void HireRole(StaffRole role)
        {
            var c = s.Candidates.Where(x => x.Staff.Role == role && sim.Staff.CanHire(x, out _)).OrderByDescending(x => x.Staff.Skill).FirstOrDefault();
            if (c != null) Try(sim.Hire(c, 3), "hire " + role);
        }
        if (ix.StudentsPerTeacher > 12.5f && cash > 10000) HireRole(StaffRole.Teacher);
        if (ix.BathroomShortage > 0 && cash > 12000 && !s.Rooms.Any(r => r.Type == RoomType.Bathroom && !r.IsBuilt(sim.Day)))
            Build(RoomType.Bathroom, "bathroom");
        // Keep tuition near what families consider fair, every semester.
        int mo = Calendar.Month(sim.Day);
        if ((mo == 4 || mo == 11) && Calendar.DayOfMonth(sim.Day) <= 7 && s.Policies.PendingTuition < 0)
        {
            float fair = sim.FairTuition, t0 = s.Policies.AnnualTuition;
            if (Math.Abs(t0 - fair * 0.97f) > 300) sim.SetTuition(Math.Max(t0 * 0.92f, Math.Min(t0 * 1.1f, fair * 0.97f)));
        }
        if (ix.ChefCoverage < 1 && cash > 20000) HireRole(StaffRole.Chef);
        if (ix.CustodianCoverage < 1 && cash > 20000) HireRole(StaffRole.Custodian);
        if (ix.GuardCoverage < 1 && cash > 15000 && (ix.Students > 15 || s.Stat(StatId.SafetyDiscipline) < 40)) HireRole(StaffRole.Security);
        if (sim.CountStaff(StaffRole.Maintenance) == 0 && cash > 50000 && ix.Students > 25) HireRole(StaffRole.Maintenance);
        if (sim.IsUnlocked(Feature.Administrators) && cash > 60000 && sim.CountStaff(StaffRole.Administrator, false) < sim.Staff.AdminDesks())
            HireRole(StaffRole.Administrator);

        // Rich mode spends freely but keeps a reserve, so it stress-tests a big campus instead of going broke.
        if (rich && cash > 150000)
        {
            foreach (RoomType t in Enum.GetValues(typeof(RoomType)))
                if (!s.Rooms.Any(r => r.Type == t) && sim.Facilities.CanBuild(t, out _) && cash > RoomCatalog.Get(t).Cost[0] + 150000) { Build(t, "build " + t); break; }
            var weak = s.Staff.Where(x => x.Role == StaffRole.Teacher && !x.IsAway(sim.Day)).OrderBy(x => x.Skill).FirstOrDefault();
            if (weak != null && weak.Skill < 70) Try(sim.Train(weak.Id, TrainingProgram.Certification), "train");
            if (!sim.Admissions.MarketingActive && sim.Facilities.FreeBeds() > 3) Try(sim.RunMarketing(), "marketing");
            if (s.Policies.Food < FoodTier.Good) sim.SetFoodTier(FoodTier.Good);
            if (s.Policies.InternetLevel < 3) Try(sim.UpgradeInternet(), "internet");
            foreach (SportType sp in new[] { SportType.Soccer, SportType.Swimming, SportType.Track, SportType.Tennis })
                if (sim.Sports.CanCreateTeam(sp, out _)) Try(sim.CreateTeam(sp), "team " + sp);
            foreach (var r in s.Rooms.Where(r => r.Type == RoomType.Office || r.Type == RoomType.ClubRoom).ToList())
                if (sim.Facilities.CanUpgrade(r, out _)) Try(sim.UpgradeRoom(r.Id), "upgrade office");
        }

        // Food: don't serve budget food if we can afford better.
        if (s.Policies.Food == FoodTier.Budget && cash > 20000) sim.SetFoodTier(FoodTier.Standard);

        // Tuition: nudge toward fair value once a year in spring.
        if (Calendar.Month(sim.Day) == 4 && Calendar.DayOfMonth(sim.Day) <= 7 && s.Policies.PendingTuition < 0)
        {
            float fair = sim.FairTuition;
            float t = s.Policies.AnnualTuition;
            if (t < fair * 0.95f) sim.SetTuition(Math.Min(fair, t * 1.08f));
        }

        // Admissions: fill beds with best applicants.
        foreach (var a in s.Applicants.Where(a => !a.Transfer).OrderByDescending(a => sim.Admissions.Score(a)).ToList())
        {
            if (sim.Facilities.FreeBeds() <= 0) break;
            float aid = a.RequestedAid <= 0.15f ? a.RequestedAid : 0.1f;
            sim.Admit(a, ScholarshipType.None, aid);
        }

        // Clubs and sports.
        if (sim.IsUnlocked(Feature.Clubs))
        {
            if (!s.Rooms.Any(r => r.Type == RoomType.ClubRoom) && cash > 40000) Build(RoomType.ClubRoom, "club room");
            foreach (ClubType c in Enum.GetValues(typeof(ClubType)))
                if (sim.Clubs.CanCreate(c, out _) && cash > 30000) { Try(sim.CreateClub(c), "club " + c); break; }
        }
        if (sim.IsUnlocked(Feature.SportsPrograms) && sim.Sports.CanCreateTeam(SportType.Basketball, out _))
            Try(sim.CreateTeam(SportType.Basketball), "team");
        foreach (var t in s.Teams.Where(t => t.CoachId < 0))
        {
            var coach = s.Candidates.FirstOrDefault(c => c.Staff.Role == StaffRole.Coach && sim.Staff.CanHire(c, out _));
            if (coach != null && cash > 40000) { var r = sim.Hire(coach, 1); if (r.Ok) sim.SetCoach(t.Id, s.Staff.Last().Id); }
        }

        // The late game, once the essentials are covered: the specialization's own building, an International
        // House, the old estate (saved up for) and its manor, keeping a month's running costs in the bank;
        // then the endowment with whatever is left beyond a few months' costs.
        float month = sim.Economy.Project().Expenses;
        bool Spare(float cost) => s.Finance.Cash > cost + Math.Max(60000f, month);
        float reserve = Math.Max(150000f, 2.5f * month);
        void Grow(RoomType type)
        {
            var room = s.Rooms.FirstOrDefault(r => r.Type == type);
            if (room == null && Spare(RoomCatalog.Get(type).Cost[0]) && sim.Facilities.CanBuild(type, out _)) Build(type, "build " + type);
            else if (room != null && sim.Facilities.CanUpgrade(room, out _) && Spare(sim.Facilities.UpgradeCost(room))) Try(sim.UpgradeRoom(room.Id), "upgrade " + room.Name);
        }
        var signature = SpecializationRules.Signature(s.Specialization);
        if (signature != null) Grow(signature.Type);
        if (sim.IsUnlocked(Feature.InternationalStudents)) Grow(RoomType.InternationalHouse);
        if (sim.IsUnlocked(Feature.SecondCampus) && !s.Campus.EstateOpen)
        {
            if (Spare(Balance.EstateOpenCost)) Try(sim.OpenEstate(), "open the estate");
        }
        else
        {
            if (s.Campus.EstateOpen) Grow(RoomType.Manor);
            if (sim.IsUnlocked(Feature.Endowment) && s.Finance.Cash > reserve * 1.5f)
                Try(sim.Endow((s.Finance.Cash - reserve) * 0.5f), "endow");
        }
    }

    static void Try(CommandResult r, string what)
    {
        if (!r.Ok && Environment.GetEnvironmentVariable("HARNESS_DEBUG") == "1") Console.WriteLine($"  ({what} failed: {r.Message})");
    }

    static void Summary(Simulation sim)
    {
        var s = sim.State;
        Console.WriteLine($"Final: {s.Students.Count} students, {s.Staff.Count} staff, cash {s.Finance.Cash:N0}, level {s.Progress.SchoolLevel}, director {s.Progress.DirectorLevel}, rank {s.NationalRank}, prestige {s.Prestige:0}");
        Console.WriteLine("Reputation: " + string.Join("  ", Enum.GetValues(typeof(ReputationId)).Cast<ReputationId>().Select(r => $"{r} {s.Rep(r):0}")));
        Console.WriteLine($"Alumni: {s.Alumni.Count}, Teams: {s.Teams.Count}, Clubs: {s.Clubs.Count}, Promises kept/broken: {s.Promises.Count(p => p.Kept)}/{s.Promises.Count(p => p.Resolved && !p.Kept)}");
        Console.WriteLine($"Relationships tracked: {s.Relationships.Count}");
        Console.WriteLine($"Campus: {s.Campus.OwnedParcelCount} parcels owned, {s.Campus.UsedTiles}/{s.Campus.OwnedTiles} tiles built, {s.Rooms.Count} rooms, land bought {s.LandPurchases}x");
        var signature = SpecializationRules.Signature(s.Specialization);
        var hall = signature != null ? s.Rooms.FirstOrDefault(r => r.Type == signature.Type) : null;
        var manor = s.Rooms.FirstOrDefault(r => r.Type == RoomType.Manor);
        Console.WriteLine($"Late game: {s.Specialization}{(hall != null ? $" with its {hall.Name} L{hall.Level}" : "")}, {s.Students.Count(x => x.International)} students from abroad, " +
                          $"estate {(manor != null ? $"open (manor L{manor.Level}, {s.EstatePurchases} parcels bought)" : "closed")}, endowment {s.Endowment:N0} (given {s.EndowmentGiven:N0})");
        var types = s.Inbox.GroupBy(m => m.TemplateId).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}={g.Count()}");
        Console.WriteLine("Messages in archive: " + string.Join(", ", types));
        Console.WriteLine($"News: {s.News.Count(n => n.AboutUs && n.Tone == NewsTone.Good)} good, {s.News.Count(n => n.AboutUs && n.Tone == NewsTone.Bad)} bad, " +
                          $"{s.News.Count(n => n.AboutUs && n.Tone == NewsTone.Neutral)} neutral, {s.News.Count(n => !n.AboutUs)} about rivals (last {s.News.Count} kept)");
        Console.WriteLine("Revealed: " + string.Join(", ", Enum.GetValues(typeof(Reveal)).Cast<Reveal>().Where(sim.IsRevealed)));
        Console.WriteLine("Assistant problems answered: " + string.Join(", ", Seen.Where(k => k.Key.StartsWith("assist_") || k.Key.StartsWith("press") || k.Key.StartsWith("tutorial")).OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value}")));
        Console.WriteLine($"Tuition: {s.Policies.AnnualTuition:N0} (fair {sim.FairTuition:N0}), food {s.Policies.Food}, loans {s.Finance.Loans.Count}");
        var handled = s.Inbox.Count(m => m.HandledBy != null);
        Console.WriteLine($"Handled by administrators: {handled}; expired: {s.Inbox.Count(m => m.ChosenOptionId == "expired")}");
        var rels = s.Relationships.All.GroupBy(r => r.Type).Select(g => $"{g.Key}={g.Count()}");
        Console.WriteLine("Relationship types: " + string.Join(", ", rels));
        var sample = s.Students.OrderByDescending(x => x.History.Count).FirstOrDefault();
        if (sample != null)
        {
            Console.WriteLine($"\nMost eventful student: {sample.FullName} ({string.Join(", ", sample.Traits)})");
            foreach (var h in sample.History.TakeLast(10)) Console.WriteLine("  " + h);
        }
    }
}
