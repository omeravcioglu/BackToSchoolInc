using System;

namespace SchoolSim
{
    public readonly struct CommandResult
    {
        public readonly bool Ok;
        public readonly string Message;

        public CommandResult(bool ok, string message)
        {
            Ok = ok;
            Message = message;
        }

        public static CommandResult Success(string message = null) => new CommandResult(true, message);
        public static CommandResult Fail(string message) => new CommandResult(false, message);
    }

    /// <summary>Everything the player can do. The UI calls these and never mutates state directly.</summary>
    public sealed partial class Simulation
    {
        // --- Facilities ---------------------------------------------------------------

        /// <summary>Builds a room at the best free spot on owned land (auto-placement).</summary>
        public CommandResult BuildRoom(RoomType type)
        {
            var room = Facilities.BuildAuto(type, out string why);
            if (room == null) return CommandResult.Fail(why);
            Tutorial.Check();
            return CommandResult.Success($"{room.Name} will be ready on {Calendar.Format(room.ConstructionDoneDay)}.");
        }

        /// <summary>Builds a room with its bottom-left tile at (x, y), door facing <paramref name="rotation"/>.</summary>
        public CommandResult BuildRoomAt(RoomType type, int x, int y, int rotation)
        {
            if (!Facilities.CanBuildAt(type, x, y, rotation, out string why)) return CommandResult.Fail(why);
            var room = Facilities.Build(type, x, y, rotation);
            Tutorial.Check();
            return CommandResult.Success($"{room.Name} will be ready on {Calendar.Format(room.ConstructionDoneDay)}.");
        }

        public CommandResult UpgradeRoom(int roomId)
        {
            var room = State.FindRoom(roomId);
            if (room == null) return CommandResult.Fail("No such room");
            if (!Facilities.CanUpgrade(room, out string why)) return CommandResult.Fail(why);
            Facilities.Upgrade(room);
            return CommandResult.Success($"Upgrading {room.Name} to level {room.PendingLevel}.");
        }

        public CommandResult RepairRoom(int roomId)
        {
            var room = State.FindRoom(roomId);
            if (room == null) return CommandResult.Fail("No such room");
            float cost = Facilities.RepairCost(room);
            if (cost < 1f) return CommandResult.Fail("Nothing to repair");
            if (!Facilities.Repair(room)) return CommandResult.Fail("Not enough money");
            return CommandResult.Success($"Repaired {room.Name} for {MathUtil.Money(cost)}.");
        }

        public CommandResult DemolishRoom(int roomId)
        {
            var room = State.FindRoom(roomId);
            if (room == null) return CommandResult.Fail("No such room");
            return Facilities.Demolish(room, out string why) ? CommandResult.Success($"{room.Name} demolished.") : CommandResult.Fail(why);
        }

        /// <summary>Buys the cheapest parcel for sale, nearest the campus centre.</summary>
        public CommandResult BuyLand()
        {
            if (!Facilities.FindLandToBuy(out int px, out int py)) return CommandResult.Fail("No land left for sale");
            float cost = Facilities.ParcelCost(px, py);
            if (State.Finance.Cash < cost) return CommandResult.Fail("Not enough money");
            Facilities.BuyParcel(px, py);
            return CommandResult.Success($"Bought land for {MathUtil.Money(cost)}.");
        }

        public CommandResult BuyParcel(int px, int py)
        {
            float cost = Facilities.ParcelCost(px, py);
            if (!Facilities.CanBuyParcel(px, py, out string why)) return CommandResult.Fail(why);
            Facilities.BuyParcel(px, py);
            return CommandResult.Success($"Bought the parcel for {MathUtil.Money(cost)}.");
        }

        /// <summary>Buys the old estate across the road (school level 25): the manor, and the rest of it for sale.</summary>
        public CommandResult OpenEstate()
        {
            if (!Facilities.CanOpenEstate(out string why)) return CommandResult.Fail(why);
            Facilities.OpenEstate();
            return CommandResult.Success($"The old estate is yours! Its other parcels are for sale from {MathUtil.Money(Facilities.EstateLandCost)}.");
        }

        // --- Endowment ----------------------------------------------------------------

        /// <summary>Gives money to the endowment for good: a yearly payout and prestige in return.</summary>
        public CommandResult Endow(float amount)
        {
            if (!IsUnlocked(Feature.Endowment))
                return CommandResult.Fail($"The endowment opens at school level {Balance.UnlockLevel(Feature.Endowment)}");
            amount = (float)Math.Round(amount / 1000f) * 1000f;
            if (amount < Balance.EndowmentMinGift) return CommandResult.Fail($"Gifts start at {MathUtil.Money(Balance.EndowmentMinGift)}");
            if (State.Finance.Cash < amount) return CommandResult.Fail("Not enough money");
            Economy.Endow(amount);
            return CommandResult.Success($"{MathUtil.Money(amount)} given to the endowment. It now holds {MathUtil.Money(State.Endowment)}.");
        }

        // --- Staff --------------------------------------------------------------------

        public CommandResult Hire(Candidate candidate, int contractYears)
        {
            if (!Staff.CanHire(candidate, out string why)) return CommandResult.Fail(why);
            var s = Staff.Hire(candidate, contractYears);
            return CommandResult.Success($"Hired {s.Name}.");
        }

        public CommandResult Fire(int staffId)
        {
            var s = State.FindStaff(staffId);
            if (s == null) return CommandResult.Fail("No such staff member");
            float severance = Staff.Fire(s);
            return CommandResult.Success($"{s.Name} was let go. Severance: {MathUtil.Money(severance)}.");
        }

        public CommandResult SetSalary(int staffId, float salary)
        {
            var s = State.FindStaff(staffId);
            if (s == null) return CommandResult.Fail("No such staff member");
            if (salary < 500f) return CommandResult.Fail("Salary too low");
            Staff.SetSalary(s, salary);
            return CommandResult.Success($"{s.Name} now earns {MathUtil.Money(s.Salary)}/mo.");
        }

        public CommandResult Train(int staffId, TrainingProgram program)
        {
            var s = State.FindStaff(staffId);
            if (s == null) return CommandResult.Fail("No such staff member");
            if (!Staff.CanTrain(s, program, out string why)) return CommandResult.Fail(why);
            Staff.Train(s, program);
            return CommandResult.Success($"{s.Name} is away at a {program} until {Calendar.Format(s.AwayUntilDay)}.");
        }

        // --- Students -----------------------------------------------------------------

        public CommandResult MoveStudent(int studentId, int roomId)
        {
            var s = State.FindStudent(studentId);
            var room = State.FindRoom(roomId);
            if (s == null || room == null || room.Type != RoomType.DormRoom) return CommandResult.Fail("Invalid move");
            if (!room.IsOperational(Day)) return CommandResult.Fail("That room is not available");
            if (s.DormRoomId == roomId) return CommandResult.Fail("Already there");
            if (Indices.ResidentsOf(roomId).Count >= room.Capacity) return CommandResult.Fail("Room is full. Swap students instead.");
            Facilities.AssignBed(s, room);
            s.Moodlets.Set("Moved rooms", -1f, Day + 5);
            s.Remember(Day, $"Moved to {room.Name}.");
            return CommandResult.Success($"{s.FirstName} moved to {room.Name}.");
        }

        public CommandResult SwapStudents(int studentA, int studentB)
        {
            var a = State.FindStudent(studentA);
            var b = State.FindStudent(studentB);
            if (a == null || b == null) return CommandResult.Fail("Invalid swap");
            var ra = State.FindRoom(a.DormRoomId);
            var rb = State.FindRoom(b.DormRoomId);
            if (ra == null || rb == null || ra.Id == rb.Id) return CommandResult.Fail("Both students need beds in different rooms");
            Facilities.AssignBed(a, rb);
            Facilities.AssignBed(b, ra);
            a.Remember(Day, $"Swapped rooms with {b.FullName}.");
            b.Remember(Day, $"Swapped rooms with {a.FullName}.");
            return CommandResult.Success($"{a.FirstName} and {b.FirstName} swapped rooms.");
        }

        public CommandResult Expel(int studentId)
        {
            var s = State.FindStudent(studentId);
            if (s == null) return CommandResult.Fail("No such student");

            foreach (var r in State.Relationships.For(s.Id))
                if (r.Score >= 30f) State.FindStudent(r.Other(s.Id))?.Moodlets.Set($"Friend {s.FirstName} expelled", -6f, Day + 20);

            if (s.Incidents >= 2 || s.Has(StudentTrait.Troublemaker))
            {
                AllParentsMood("A troublemaker was expelled", 2f, 30);
                AddReputationModifier(ReputationId.Safety, "Firm on discipline", 2f, 90);
            }
            else
            {
                ChangeTrust(-3f);
                AllParentsMood("A harsh expulsion", -3f, 30);
            }
            State.Year.Highlight($"Expelled {s.FullName}");
            Students.Remove(s, "expelled");
            return CommandResult.Success($"{s.FullName} was expelled.");
        }

        // --- Admissions ---------------------------------------------------------------

        public CommandResult Admit(Applicant applicant, ScholarshipType type, float discount)
        {
            if (!Admissions.CanAdmit(applicant, type, out string why)) return CommandResult.Fail(why);
            bool ok = Admissions.Admit(applicant, type, discount, out string result);
            return new CommandResult(ok, result);
        }

        public CommandResult RejectApplicant(Applicant applicant)
        {
            Admissions.Reject(applicant);
            return CommandResult.Success("Application declined.");
        }

        public CommandResult RunMarketing()
        {
            if (Admissions.MarketingActive) return CommandResult.Fail("A campaign is already running");
            if (State.Finance.Cash < Balance.MarketingCost) return CommandResult.Fail("Not enough money");
            Admissions.RunMarketing();
            return CommandResult.Success($"Campaign running until {Calendar.Format(State.MarketingUntilDay)}.");
        }

        // --- Policies -----------------------------------------------------------------

        public CommandResult SetTuition(float annual)
        {
            Economy.SetTuition(annual);
            var p = State.Policies;
            if (p.PendingTuition < 0f) return CommandResult.Success("Tuition unchanged.");
            int nextSemesterMonth = Calendar.Month(Day) <= Calendar.FirstSemesterEndMonth ? Calendar.FirstSemesterEndMonth + 1 : 1;
            return CommandResult.Success($"Tuition will change to {MathUtil.Money(p.PendingTuition)} in {Calendar.MonthName(nextSemesterMonth)}.");
        }

        public CommandResult SetFoodTier(FoodTier tier)
        {
            var old = State.Policies.Food;
            State.Policies.Food = tier;
            if (tier > old) AllStudentsMood("Better food", 2f, 20);
            if (tier < old) AllStudentsMood("Food got worse", -4f, 30);
            return CommandResult.Success($"Cafeteria now serves the {tier} menu ({MathUtil.Money(Balance.MealCost(tier))}/meal).");
        }

        public CommandResult SetCurfew(CurfewPolicy curfew)
        {
            State.Policies.Curfew = curfew;
            return CommandResult.Success($"Curfew: {curfew}.");
        }

        public CommandResult SetDiscipline(DisciplinePolicy policy)
        {
            State.Policies.Discipline = policy;
            return CommandResult.Success($"Discipline policy: {policy}.");
        }

        public CommandResult UpgradeInternet()
        {
            int level = State.Policies.InternetLevel;
            if (level >= 3) return CommandResult.Fail("Already the best network");
            float cost = Balance.InternetUpgradeCost(level + 1);
            if (State.Finance.Cash < cost) return CommandResult.Fail("Not enough money");
            State.Finance.Post(Day, LedgerCategory.Construction, -cost, "Network upgrade");
            State.Policies.InternetLevel++;
            return CommandResult.Success($"Internet upgraded to level {State.Policies.InternetLevel}.");
        }

        public CommandResult SetTicketPrice(float price)
        {
            State.Policies.TicketPrice = MathUtil.Clamp(price, 0f, 100f);
            return CommandResult.Success($"Ticket price: {MathUtil.Money(State.Policies.TicketPrice)}.");
        }

        // --- Programs -----------------------------------------------------------------

        public CommandResult CreateTeam(SportType sport)
        {
            if (!Sports.CanCreateTeam(sport, out string why)) return CommandResult.Fail(why);
            var t = Sports.CreateTeam(sport);
            return CommandResult.Success($"{t.Name} founded with {t.Roster.Count} players.");
        }

        public CommandResult DisbandTeam(int teamId)
        {
            var t = State.FindTeam(teamId);
            if (t == null) return CommandResult.Fail("No such team");
            Sports.DisbandTeam(t);
            return CommandResult.Success($"{t.Name} disbanded.");
        }

        public CommandResult SetCoach(int teamId, int staffId)
        {
            var t = State.FindTeam(teamId);
            var c = State.FindStaff(staffId);
            if (t == null || c == null || c.Role != StaffRole.Coach) return CommandResult.Fail("Choose a coach");
            Sports.SetCoach(t, c);
            return CommandResult.Success($"{c.Name} now coaches the {t.Name}.");
        }

        public CommandResult SetIntensity(int teamId, TrainingIntensity intensity)
        {
            var t = State.FindTeam(teamId);
            if (t == null) return CommandResult.Fail("No such team");
            t.Intensity = intensity;
            return CommandResult.Success($"{t.Name} training: {intensity}.");
        }

        public CommandResult AutoRoster(int teamId)
        {
            var t = State.FindTeam(teamId);
            if (t == null) return CommandResult.Fail("No such team");
            Sports.AutoFillRoster(t);
            return CommandResult.Success($"{t.Name} roster: {t.Roster.Count} players.");
        }

        public CommandResult CreateClub(ClubType type)
        {
            if (!Clubs.CanCreate(type, out string why)) return CommandResult.Fail(why);
            var c = Clubs.Create(type);
            return CommandResult.Success($"{c.Name} founded with {c.Members.Count} members.");
        }

        public CommandResult DisbandClub(int clubId)
        {
            var c = State.FindClub(clubId);
            if (c == null) return CommandResult.Fail("No such club");
            Clubs.Disband(c);
            return CommandResult.Success($"{c.Name} disbanded.");
        }

        // --- Inbox --------------------------------------------------------------------

        public CommandResult Reply(int messageId, string optionId)
        {
            bool ok = Inbox.Resolve(messageId, optionId, out string result);
            if (ok) Tutorial.Check();
            return new CommandResult(ok, result);
        }

        // --- Tutorial -----------------------------------------------------------------

        /// <summary>Tells the tutorial the director did something in the UI ("open:inbox", "next"...).</summary>
        public void TutorialSignal(string key) => Tutorial.Signal(key);

        public CommandResult SkipTutorial()
        {
            if (!Tutorial.Active) return CommandResult.Fail("The tutorial is already over.");
            Tutorial.Skip();
            return CommandResult.Success("Tutorial skipped. Everything is unlocked that the tutorial would have shown you.");
        }
    }
}
