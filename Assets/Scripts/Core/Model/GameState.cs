using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// All persistent simulation data. Systems read and mutate this; the UI only reads it and
    /// issues commands through <see cref="Simulation"/>.
    /// </summary>
    public sealed class GameState
    {
        public static readonly int StatCount = Enum.GetValues(typeof(StatId)).Length;
        public static readonly int ReputationCount = Enum.GetValues(typeof(ReputationId)).Length;

        public string SchoolName;
        public int Emblem;                    // crest picture, chosen when the school is founded
        public int CrestColor;
        public int Day;
        public bool GameOver;
        public string GameOverReason;

        public Assistant Assistant;           // null until one is hired (tests and the harness run without)
        public readonly TutorialState Tutorial = new TutorialState();
        public readonly List<NewsStory> News = new List<NewsStory>();
        public readonly List<DaySnapshot> History = new List<DaySnapshot>();
        public int NewsMilestone;             // highest student-count milestone already in the papers

        public readonly SchoolPolicies Policies = new SchoolPolicies();
        public readonly AdminPolicies AdminPolicies = new AdminPolicies();
        public readonly Finance Finance = new Finance();
        public readonly Progression Progress = new Progression();
        public Specialization Specialization = Specialization.None;

        public readonly float[] Stats = new float[StatCount];
        public readonly float[] Reputation = new float[ReputationCount];
        public readonly List<ReputationModifier> ReputationModifiers = new List<ReputationModifier>();
        public readonly List<Moodlet> SchoolMoodlets = new List<Moodlet>();   // apply to every student

        public readonly List<Student> Students = new List<Student>();
        public readonly List<Applicant> Applicants = new List<Applicant>();
        public readonly List<StaffMember> Staff = new List<StaffMember>();
        public readonly List<Candidate> Candidates = new List<Candidate>();
        public readonly List<Room> Rooms = new List<Room>();
        public readonly RelationshipGraph Relationships = new RelationshipGraph();

        public readonly List<InboxMessage> Inbox = new List<InboxMessage>();
        public readonly List<Promise> Promises = new List<Promise>();
        public readonly List<ScheduledEvent> Scheduled = new List<ScheduledEvent>();
        public readonly Dictionary<string, int> EventCooldowns = new Dictionary<string, int>();

        public readonly List<SportsTeam> Teams = new List<SportsTeam>();
        public readonly List<Club> Clubs = new List<Club>();
        public readonly List<RivalSchool> Rivals = new List<RivalSchool>();
        public readonly List<Alumnus> Alumni = new List<Alumnus>();
        public readonly List<YearReport> Reports = new List<YearReport>();
        public YearTracker Year = new YearTracker();

        public readonly List<LogEntry> Log = new List<LogEntry>();

        public readonly CampusMap Campus = new CampusMap();
        public int LandPurchases;
        public int EstatePurchases;           // parcels of the old estate bought since it opened
        public float Endowment;               // the fund: given for good, it pays out each September
        public float EndowmentGiven;          // everything the school has put in over the years
        public float IncidentPressure;
        public float WageIndex = 1f;
        public float AcademicLegacy;          // 0-15, from graduate success
        public float SportsLegacy;            // 0-15, from championships
        public float ApplicantBonus;          // one-season marketing boost (fraction)
        public int MarketingUntilDay = -1;
        public int NextInspectionDay;
        public bool InspectionAnnounced;
        public int NationalRank;
        public int LastLoanOfferDay = -999;
        public int NegativeCashDays;

        int _nextId;
        public int NewId() => ++_nextId;

        // Lookups ----------------------------------------------------------------------

        public Student FindStudent(int id)
        {
            if (id < 0) return null;
            foreach (var s in Students) if (s.Id == id) return s;
            return null;
        }

        public StaffMember FindStaff(int id)
        {
            if (id < 0) return null;
            foreach (var s in Staff) if (s.Id == id) return s;
            return null;
        }

        public Room FindRoom(int id)
        {
            if (id < 0) return null;
            foreach (var r in Rooms) if (r.Id == id) return r;
            return null;
        }

        public SportsTeam FindTeam(int id)
        {
            foreach (var t in Teams) if (t.Id == id) return t;
            return null;
        }

        public Club FindClub(int id)
        {
            foreach (var c in Clubs) if (c.Id == id) return c;
            return null;
        }

        public InboxMessage FindMessage(int id)
        {
            foreach (var m in Inbox) if (m.Id == id) return m;
            return null;
        }

        public float Stat(StatId id) => Stats[(int)id];
        public float Rep(ReputationId id) => Reputation[(int)id];
        public float Prestige => Reputation[(int)ReputationId.Overall];
    }
}
