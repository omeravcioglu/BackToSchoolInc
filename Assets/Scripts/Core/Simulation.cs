using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Facade over the whole simulation. Owns the state, the RNG and every system, and runs the
    /// daily tick. Player actions live in Simulation.Commands.cs.
    /// </summary>
    public sealed partial class Simulation
    {
        public GameState State { get; }
        public SimRandom Rng { get; }
        public SchoolIndices Indices { get; } = new SchoolIndices();

        public StatSystem Stats { get; }
        public FacilitySystem Facilities { get; }
        public StudentSystem Students { get; }
        public RelationshipSystem Relationships { get; }
        public StaffSystem Staff { get; }
        public EconomySystem Economy { get; }
        public InboxSystem Inbox { get; }
        public EventDirector Events { get; }
        public PromiseSystem Promises { get; }
        public AdmissionsSystem Admissions { get; }
        public InspectionSystem Inspections { get; }
        public SportsSystem Sports { get; }
        public ClubSystem Clubs { get; }
        public RivalSystem Rivals { get; }
        public ProgressionSystem Progression { get; }
        public AdministrationSystem Administration { get; }
        public YearCycleSystem YearCycle { get; }
        public AssistantSystem Assistants { get; }
        public NewsSystem News { get; }
        public HistorySystem History { get; }
        public TutorialSystem Tutorial { get; }

        public event Action<InboxMessage> MessageArrived;
        public event Action<LogEntry> Logged;
        public event Action<YearReport> YearReportReady;
        public event Action<NewsStory> NewsPublished;
        /// <summary>A hidden part of the game came into view; the flag says whether to announce it.</summary>
        public event Action<Reveal, bool> Revealed;
        public event Action<int> LeveledUp;
        public event Action TutorialChanged;

        public int Day => State.Day;
        public bool InSession => Calendar.IsSchoolMonth(State.Day);

        public Simulation(GameState state, int seed)
        {
            State = state;
            Rng = new SimRandom(seed);

            Stats = new StatSystem(this);
            Facilities = new FacilitySystem(this);
            Students = new StudentSystem(this);
            Relationships = new RelationshipSystem(this);
            Staff = new StaffSystem(this);
            Economy = new EconomySystem(this);
            Inbox = new InboxSystem(this);
            Events = new EventDirector(this);
            Promises = new PromiseSystem(this);
            Admissions = new AdmissionsSystem(this);
            Inspections = new InspectionSystem(this);
            Sports = new SportsSystem(this);
            Clubs = new ClubSystem(this);
            Rivals = new RivalSystem(this);
            Progression = new ProgressionSystem(this);
            Administration = new AdministrationSystem(this);
            YearCycle = new YearCycleSystem(this);
            Assistants = new AssistantSystem(this);
            News = new NewsSystem(this);
            History = new HistorySystem(this);
            Tutorial = new TutorialSystem(this);
        }

        /// <summary>A simulation around a loaded state, continuing the saved random sequence.</summary>
        public static Simulation FromSave(GameState state, SimRandom rng)
        {
            var sim = new Simulation(state, rng.Seed);
            sim.Rng.CopyStateFrom(rng);
            sim.AfterLoad();
            return sim;
        }

        /// <summary>
        /// Rebuilds what isn't saved (the daily indices and every stat's explanation) without
        /// changing any saved value.
        /// </summary>
        void AfterLoad()
        {
            var stats = (float[])State.Stats.Clone();
            var reputation = (float[])State.Reputation.Clone();
            Stats.ComputeIndices();
            Stats.UpdateSchoolStats(initialize: false);
            Array.Copy(stats, State.Stats, stats.Length);
            Array.Copy(reputation, State.Reputation, reputation.Length);
        }

        /// <summary>Runs the day-0 bookkeeping for a fresh game (first month's billing, first stats pass).</summary>
        public void Begin()
        {
            Stats.ComputeIndices();
            Stats.UpdateSchoolStats(initialize: true);
            StartOfMonth();
            Stats.ComputeIndices();
            Stats.UpdateSchoolStats(initialize: false);
        }

        public void AdvanceDay()
        {
            if (State.GameOver) return;

            State.Day++;
            if (Calendar.IsFirstDayOfMonth(State.Day)) StartOfMonth();

            Facilities.DailyTick();
            Staff.DailyTick();
            Stats.ComputeIndices();
            Students.DailyTick();
            Relationships.DailyTick();
            Sports.DailyTick();
            Clubs.DailyTick();
            Stats.UpdateSchoolStats(initialize: false);
            Economy.DailyTick();
            Promises.DailyTick();
            Inspections.DailyTick();
            RunScheduledEvents();
            Events.DailyTick();
            Admissions.DailyTick();
            Inbox.DailyTick();
            Administration.DailyTick();
            Assistants.DailyTick();
            News.DailyTick();
            Tutorial.Check();
            Economy.CheckSolvency();
            History.Record();
        }

        void StartOfMonth()
        {
            int month = Calendar.Month(State.Day);

            Economy.StartMonth();
            if (month == Calendar.AdmissionsMonth && State.Day > 0) YearCycle.EndOfSchoolYear();
            if (month == 1) YearCycle.StartOfSchoolYear();

            Economy.MonthlyBilling();
            Staff.Monthly();
            Clubs.Monthly();
            Sports.Monthly();
            Rivals.Monthly();
            Relationships.Monthly();
            Admissions.Monthly();
            Progression.Monthly();
            Assistants.Monthly();
        }

        void RunScheduledEvents()
        {
            if (State.Scheduled.Count == 0) return;
            var due = State.Scheduled.FindAll(s => s.Day <= State.Day);
            if (due.Count == 0) return;
            State.Scheduled.RemoveAll(s => s.Day <= State.Day);
            foreach (var s in due) Events.RunScheduled(s);
        }

        // --- Shared helpers used by many systems ---------------------------------------

        public void Log(string text, bool important = false)
        {
            var entry = new LogEntry { Day = State.Day, Text = text, Important = important };
            State.Log.Add(entry);
            if (State.Log.Count > 400) State.Log.RemoveAt(0);
            Logged?.Invoke(entry);
        }

        internal void RaiseMessageArrived(InboxMessage msg) => MessageArrived?.Invoke(msg);
        internal void RaiseYearReport(YearReport report) => YearReportReady?.Invoke(report);
        internal void RaiseNews(NewsStory story) => NewsPublished?.Invoke(story);
        internal void RaiseRevealed(Reveal r, bool announce) => Revealed?.Invoke(r, announce);
        internal void RaiseLevelUp(int level) => LeveledUp?.Invoke(level);
        internal void RaiseTutorialChanged() => TutorialChanged?.Invoke();

        public bool IsUnlocked(Feature f) => State.Progress.SchoolLevel >= Balance.UnlockLevel(f);

        public bool IsRevealed(Reveal r) => Progression.IsRevealed(r);

        /// <summary>
        /// Adds a temporary reputation modifier. Modifiers from the same source don't stack:
        /// a repeat refreshes the duration and keeps the stronger value.
        /// </summary>
        public void AddReputationModifier(ReputationId rep, string label, float value, int days)
        {
            foreach (var m in State.ReputationModifiers)
            {
                if (m.Rep != rep || m.Label != label) continue;
                if (Math.Abs(value) > Math.Abs(m.Value)) m.Value = value;
                m.ExpiresDay = Math.Max(m.ExpiresDay, State.Day + days);
                return;
            }
            State.ReputationModifiers.Add(new ReputationModifier
            {
                Rep = rep, Label = label, Value = value, ExpiresDay = State.Day + days,
            });
        }

        /// <summary>Immediate reputation change. Fades as reputation drifts back toward its target.</summary>
        public void ShockReputation(ReputationId rep, float delta)
        {
            int i = (int)rep;
            State.Reputation[i] = MathUtil.Clamp100(State.Reputation[i] + delta);
        }

        public void ChangeTrust(float delta)
        {
            int i = (int)StatId.Trust;
            float floor = SpecializationRules.TrustFloor(State.Specialization);
            State.Stats[i] = MathUtil.Clamp(State.Stats[i] + delta, floor, 100f);
        }

        /// <summary>Adds a mood change to every current family.</summary>
        public void AllParentsMood(string label, float value, int days)
        {
            foreach (var s in State.Students) s.ParentMoodlets.Set(label, value, State.Day + days);
        }

        public void AllStudentsMood(string label, float value, int days)
        {
            State.SchoolMoodlets.Set(label, value, State.Day + days);
        }

        public void AllStaffMood(string label, float value, int days)
        {
            foreach (var s in State.Staff) s.Moodlets.Set(label, value, State.Day + days);
        }

        public IEnumerable<Room> Operational(RoomType type)
        {
            foreach (var r in State.Rooms)
                if (r.Type == type && r.IsOperational(State.Day)) yield return r;
        }

        public bool HasOperational(RoomType type)
        {
            foreach (var r in State.Rooms)
                if (r.Type == type && r.IsOperational(State.Day)) return true;
            return false;
        }

        public int CountStaff(StaffRole role, bool activeOnly = true)
        {
            int n = 0;
            foreach (var s in State.Staff)
                if (s.Role == role && (!activeOnly || s.IsActive(State.Day))) n++;
            return n;
        }

        public StaffMember FindAdmin(AdminRole role)
        {
            foreach (var s in State.Staff)
                if (s.Role == StaffRole.Administrator && s.AdminRole == role && s.IsActive(State.Day)) return s;
            return null;
        }

        public float EffectiveDormFee(Room room)
        {
            if (room == null || room.Type != RoomType.DormRoom) return 0f;
            return room.Def.MonthlyFee[room.Level - 1] * SpecializationRules.DormFeeMultiplier(State.Specialization)
                   * (1f + Indices.SignatureEffect(x => x.DormFee));
        }

        public float FairTuition => Balance.FairTuition(State.Prestige) * SpecializationRules.FairTuitionMultiplier(State.Specialization);
    }
}
