using System;

namespace SchoolSim
{
    /// <summary>Choices made on the new-school screens.</summary>
    public sealed class NewGameSetup
    {
        public string SchoolName = "Little Oak School";
        public int Seed;
        public int Emblem;
        public int CrestColor;
        /// <summary>The assistant picked from <see cref="AssistantRules.Candidates"/>, or null for none.</summary>
        public Assistant Assistant;
        /// <summary>Start with the assistant's walkthrough and most of the game hidden.</summary>
        public bool Tutorial;
    }

    /// <summary>
    /// Builds the starting scenario: a tiny, struggling school with two classrooms, a small dorm,
    /// a cramped cafeteria, a handful of staff, ten students, and not much money.
    /// </summary>
    public static class NewGame
    {
        /// <summary>A plain start (tests, the harness): no assistant, no tutorial, the basics visible.</summary>
        public static Simulation Create(string schoolName = "Little Oak School", int seed = 0) =>
            Create(new NewGameSetup { SchoolName = schoolName, Seed = seed });

        public static Simulation Create(NewGameSetup setup)
        {
            string schoolName = string.IsNullOrWhiteSpace(setup.SchoolName) ? "Little Oak School" : setup.SchoolName.Trim();
            int seed = setup.Seed != 0 ? setup.Seed : Environment.TickCount;
            var state = new GameState { SchoolName = schoolName, Emblem = setup.Emblem, CrestColor = setup.CrestColor };
            var sim = new Simulation(state, seed);
            state.Finance.Cash = Balance.StartingCash;

            // The school owns a 2x2 block of parcels in the middle of the map.
            for (int py = StartParcelY; py < StartParcelY + 2; py++)
                for (int px = StartParcelX; px < StartParcelX + 2; px++)
                    state.Campus.SetParcelOwned(px, py, true);

            // A classroom wing with the office on the north side, the cafeteria to the south-west,
            // the bathroom off the corridor and a small dorm block to the east.
            AddRoom(sim, RoomType.Classroom, "Classroom 1", 21, 24, Placement.South);
            AddRoom(sim, RoomType.Classroom, "Classroom 2", 26, 24, Placement.South);
            AddRoom(sim, RoomType.DormRoom, "Dorm Room 1", 36, 18, Placement.West);
            AddRoom(sim, RoomType.DormRoom, "Dorm Room 2", 36, 14, Placement.West);
            AddRoom(sim, RoomType.Cafeteria, "Cafeteria", 21, 15, Placement.North);
            AddRoom(sim, RoomType.Bathroom, "Shared Bathroom", 31, 20, Placement.West);
            AddRoom(sim, RoomType.Office, "Administration Office", 31, 25, Placement.South);
            state.Campus.RebuildOccupancy(state.Rooms);
            foreach (var r in state.Rooms)
            {
                var issue = sim.Facilities.CheckPlacement(r.Def, r.X, r.Y, r.Rotation, r.Id, out var other);
                if (issue != PlacementIssue.None)
                    throw new InvalidOperationException($"Starting layout: {r.Name} {FacilitySystem.Describe(issue, other)}");
            }

            AddStaff(sim, StaffRole.Teacher, 42f, Subject.Math);
            AddStaff(sim, StaffRole.Teacher, 38f, Subject.Language);
            AddStaff(sim, StaffRole.Custodian, 40f);
            AddStaff(sim, StaffRole.Chef, 40f);

            int[] grades = { 1, 1, 1, 2, 2, 2, 3, 3, 4, 4 };
            for (int i = 0; i < Balance.StartingStudents; i++)
            {
                var s = StudentFactory.Create(sim, grades[i % grades.Length], 45f, 45f, 45f);
                s.EnrolledDay = 0;
                state.Students.Add(s);
                sim.Facilities.AutoAssignBed(s);
                s.Remember(0, $"Studying at {schoolName}.");
            }
            state.NewsMilestone = Balance.StartingStudents;

            // Students who already know each other start with some history.
            var rng = sim.Rng;
            foreach (var a in state.Students)
                foreach (var b in state.Students)
                {
                    if (a.Id >= b.Id) continue;
                    bool close = a.Grade == b.Grade || a.DormRoomId == b.DormRoomId;
                    if (!close || !rng.Chance(0.6f)) continue;
                    var rel = state.Relationships.GetOrCreate(a.Id, b.Id);
                    rel.Score = rng.Range(-15f, 40f) + TraitRules.Compatibility(a, b, a.DormRoomId == b.DormRoomId) * 5f;
                    rel.LastType = rel.Type;
                }

            if (setup.Assistant != null) sim.Assistants.Hire(setup.Assistant);
            sim.Begin();
            sim.Rivals.Generate(8);
            state.Year.RankStart = state.NationalRank;
            sim.Staff.RefreshCandidates();

            string help = setup.Assistant != null
                ? $"Your assistant, {setup.Assistant.Name}, will help you find your feet and bring problems to you as they come up."
                : "Watch the vitals to see why each stat is rising or falling.";
            sim.Inbox.Notify(SenderType.Government, "Board of Trustees", $"Welcome to {schoolName}, Director",
                $"{schoolName} is small, underfunded and barely known. We have {state.Students.Count} boarders, 12 beds, two tired classrooms, " +
                $"a cramped cafeteria and {MathUtil.Money(state.Finance.Cash)} in the bank.\n\n" +
                "Your job: make this place worth choosing. Fill the beds, hire better teachers, keep families and staff happy, and answer this inbox. " +
                "Every decision has consequences, and promises you make will be remembered.\n\n" + help,
                MessagePriority.Important);
            sim.Log($"You became Director of {schoolName}.", important: true);

            if (setup.Tutorial) sim.Tutorial.Start();
            else foreach (var r in TutorialSystem.Basics) sim.Progression.Unveil(r, announce: false);
            sim.History.Record();
            return sim;
        }

        public const int StartParcelX = 2;
        public const int StartParcelY = 1;

        static void AddRoom(Simulation sim, RoomType type, string name, int x, int y, int rotation)
        {
            var room = new Room
            {
                Id = sim.State.NewId(),
                Type = type,
                Name = name,
                Level = 1,
                X = x,
                Y = y,
                Rotation = rotation,
                ConstructionDoneDay = 0,
                Cleanliness = sim.Rng.Range(65f, 80f),
                Condition = sim.Rng.Range(55f, 75f),
            };
            sim.State.Rooms.Add(room);
        }

        static void AddStaff(Simulation sim, StaffRole role, float skill, Subject subject = Subject.None)
        {
            var s = sim.Staff.CreateStaff(role, skill);
            if (subject != Subject.None) s.Subject = subject;
            s.Salary = Ev.Round10(sim.Staff.MarketValue(s));
            s.HiredDay = -sim.Rng.Range(60, 900);
            s.ContractYears = 1;
            s.ContractEndDay = sim.Rng.Range(120, 360);
            s.LastRaiseDay = s.HiredDay;
            sim.State.Staff.Add(s);
        }
    }
}
