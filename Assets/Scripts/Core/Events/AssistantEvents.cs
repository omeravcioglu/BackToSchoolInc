using System;
using System.Text;

namespace SchoolSim
{
    /// <summary>
    /// A problem the assistant noticed, written in their voice, with fixes that work straight from
    /// the inbox (each still goes through the normal commands and costs). "Not now" always works.
    /// </summary>
    abstract class AssistantProblemEvent : EventTemplate
    {
        protected abstract ProblemKind Kind { get; }
        public override string Id => AssistantSystem.TemplateId(Kind);
        public override bool Random => false;
        public override bool InSessionOnly => false;
        public override int CooldownDays => Soft ? 45 : 30;
        public override int ResponseDays => 12;
        protected virtual MessagePriority Priority => MessagePriority.Normal;

        /// <summary>Gradual problems (moods, dirt, discipline) come back less often than hard limits like beds.</summary>
        protected virtual bool Soft => false;

        protected abstract string Subject(Simulation sim, InboxMessage m);

        /// <summary>Adds the fixes and returns the facts, or null when there's nothing to report after all.</summary>
        protected abstract string Compose(Simulation sim, InboxMessage m);

        protected abstract string Apply(Simulation sim, InboxMessage m, string option);

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var a = sim.State.Assistant;
            if (a == null) return false;
            string facts = Compose(sim, m);
            if (facts == null || m.Options.Count == 0) return false;
            m.AddOption("later", "Not now", "Nothing changes for now.");

            Ev.From(m, SenderType.Assistant, a.Name, Subject(sim, m), Priority);
            var sb = new StringBuilder();
            sb.Append(AssistantVoice.Opener(a.Style, sim.Rng)).Append(' ').Append(facts);
            string flavor = AssistantVoice.Flavor(a.Style, Kind);
            if (flavor != null) sb.Append(' ').Append(flavor);
            sb.Append("\n\n").Append(AssistantVoice.SignOff(a.Style));
            m.Body = sb.ToString();
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var a = sim.State.Assistant;
            if (option == "later")
            {
                // "Not now" is an answer: the assistant gives it twice as long before raising it again.
                sim.State.EventCooldowns[Id] = sim.Day + CooldownDays * 2;
                return a != null ? AssistantVoice.Later(a.Style) : "Okay.";
            }
            if (option == "land")
            {
                var bought = sim.BuyLand();
                return bought.Ok ? bought.Message + " Now there's room to build." : bought.Message;
            }
            string result = Apply(sim, m, option);
            return a != null && !string.IsNullOrEmpty(result) ? $"{result} {AssistantVoice.Done(a.Style)}" : result;
        }

        public override string Expire(Simulation sim, InboxMessage m) => "The problem is still there.";

        // --- Shared fixes ----------------------------------------------------------------

        protected static void OfferBuild(Simulation sim, InboxMessage m, RoomType type, string preview)
        {
            var def = RoomCatalog.Get(type);
            var o = m.AddOption("build", $"Build a {def.Name.ToLowerInvariant()} ({Ev.Money(def.Cost[0])})", preview, cost: def.Cost[0]);
            if (!sim.Facilities.CanBuild(type, out string why) && why != "Not enough money") o.DisabledReason = why;
            else if (!sim.Facilities.TryFindPlacement(type, out _, out _, out _))
            {
                o.DisabledReason = "No space left on our land. Buy a parcel first.";
                float land = sim.Facilities.FindLandToBuy(out int px, out int py) ? sim.Facilities.ParcelCost(px, py) : sim.Facilities.LandCost;
                m.AddOption("land", $"Buy the next plot of land ({Ev.Money(land)})", "Room for the new building; then build it from the Build tab.", cost: land);
                m.AdvisorOptionId = "land";
            }
        }

        protected static string DoBuild(Simulation sim, RoomType type)
        {
            var r = sim.BuildRoom(type);
            return r.Ok ? r.Message : "Couldn't build it: " + r.Message;
        }

        /// <summary>Offers the best candidate for a role; the candidate is remembered by id.</summary>
        protected static void OfferHire(Simulation sim, InboxMessage m, StaffRole role, string id, string what)
        {
            var c = sim.Assistants.BestCandidate(role);
            if (c == null)
            {
                m.AddOption(id, $"Hire a {what}", "One-year contract.").DisabledReason = $"No {what}s are looking for work right now";
                return;
            }
            m.StaffId = c.Staff.Id;
            string subject = c.Staff.Role == StaffRole.Teacher && c.Staff.Subject != SchoolSim.Subject.None ? $"{c.Staff.Subject}, " : "";
            m.AddOption(id, $"Hire {c.Staff.Name} ({subject}skill {c.Staff.Skill:0}, {Ev.Money(c.AskingSalary)}/mo)",
                $"Starts today on a one-year contract.");
        }

        protected static string DoHire(Simulation sim, InboxMessage m)
        {
            var c = sim.State.Candidates.Find(x => x.Staff.Id == m.StaffId);
            if (c == null) return "That candidate already took another job.";
            var r = sim.Hire(c, 1);
            return r.Ok ? r.Message : r.Message;
        }

        protected static bool Building(Simulation sim, RoomType type) =>
            sim.State.Rooms.Exists(r => r.Type == type && !r.IsBuilt(sim.Day));
    }

    sealed class CashProblemEvent : AssistantProblemEvent
    {
        const float Loan = 30000f, LoanMonthly = 1450f;
        const int LoanMonths = 24;

        protected override ProblemKind Kind => ProblemKind.Cash;
        protected override MessagePriority Priority => MessagePriority.Urgent;
        protected override string Subject(Simulation sim, InboxMessage m) => "Money is running low";

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            var s = sim.State;
            float cash = s.Finance.Cash, net = sim.Economy.Project().Net;
            string facts = net < 0f
                ? $"We have {Ev.Money(cash)} and we're losing about {Ev.Money(-net)} a month. " +
                  (cash > 0f ? $"That's roughly {cash / -net:0.0} months before the account is empty." : "The account is already overdrawn.")
                : $"We have {Ev.Money(cash)} in the bank, which isn't much of a cushion.";

            if (s.Policies.Food > FoodTier.Budget)
            {
                var cheaper = s.Policies.Food - 1;
                float save = s.Students.Count * Balance.MealsPerDay * (Balance.MealCost(s.Policies.Food) - Balance.MealCost(cheaper)) * Calendar.DaysPerMonth;
                m.AddOption("food", $"Serve the {cheaper} menu", $"Saves about {Ev.Money(save)}/month during term. Students will grumble.");
            }
            float tuition = s.Policies.PendingTuition > 0f ? s.Policies.PendingTuition : s.Policies.AnnualTuition;
            m.Amount = Ev.Round100(tuition * 1.05f);
            m.AddOption("tuition", $"Raise tuition 5% next semester ({Ev.Money(m.Amount)}/yr)", "More income from next semester. Some families will object.");
            if (s.Finance.Loans.Count < 2)
                m.AddOption("loan", $"Borrow {Ev.Money(Loan)} ({Ev.Money(LoanMonthly)}/mo for {LoanMonths} months)", "Cash now, repayments later.");
            return facts;
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "food":
                    if (sim.State.Policies.Food > FoodTier.Budget) sim.SetFoodTier(sim.State.Policies.Food - 1);
                    return "The cheaper menu starts tomorrow.";
                case "tuition":
                    return sim.SetTuition(m.Amount).Message;
                case "loan":
                    sim.Economy.TakeLoan("Harbor Community Bank", Loan, LoanMonthly, LoanMonths);
                    return "The loan is in the account.";
                default:
                    return "";
            }
        }
    }

    sealed class BedsProblemEvent : AssistantProblemEvent
    {
        protected override ProblemKind Kind => ProblemKind.Beds;
        protected override MessagePriority Priority => MessagePriority.Important;
        protected override string Subject(Simulation sim, InboxMessage m) => "We're out of beds";

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            if (Building(sim, RoomType.DormRoom)) return null;   // a fix is already on its way
            var ix = sim.Indices;
            int waiting = sim.State.Applicants.Count;
            string facts = (ix.FreeBeds <= 0 ? $"All {ix.Beds} beds are taken" : $"Only {ix.FreeBeds} of our {ix.Beds} beds is free") +
                           (waiting > 0 ? $", and {waiting} {(waiting == 1 ? "family is" : "families are")} waiting to hear from us." : ".") +
                           " No beds means no new students, and new students are how we grow.";
            var def = RoomCatalog.Get(RoomType.DormRoom);
            OfferBuild(sim, m, RoomType.DormRoom, $"{def.Capacity[0]} more beds in {def.BuildDays[0]} days. I'll find a spot on our land.");
            return facts;
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option) =>
            option == "build" ? DoBuild(sim, RoomType.DormRoom) : "";
    }

    sealed class BathroomsProblemEvent : AssistantProblemEvent
    {
        protected override ProblemKind Kind => ProblemKind.Bathrooms;
        protected override string Subject(Simulation sim, InboxMessage m) => "Queues for the bathroom";

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            if (Building(sim, RoomType.Bathroom)) return null;
            float shortage = sim.Indices.BathroomShortage;
            var def = RoomCatalog.Get(RoomType.Bathroom);
            OfferBuild(sim, m, RoomType.Bathroom, $"Serves {def.Capacity[0]} more boarders. Ready in {def.BuildDays[0]} days.");
            return $"Our shared bathrooms are short of about {shortage * 100f:0}% of what the boarders need. There are queues every morning and dorm life is suffering.";
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option) =>
            option == "build" ? DoBuild(sim, RoomType.Bathroom) : "";
    }

    sealed class SeatsProblemEvent : AssistantProblemEvent
    {
        protected override ProblemKind Kind => ProblemKind.Seats;
        protected override MessagePriority Priority => MessagePriority.Important;
        protected override string Subject(Simulation sim, InboxMessage m) => "Classrooms are packed";

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            if (Building(sim, RoomType.Classroom)) return null;
            var ix = sim.Indices;
            var def = RoomCatalog.Get(RoomType.Classroom);
            OfferBuild(sim, m, RoomType.Classroom, $"{def.Capacity[0]} more seats in {def.BuildDays[0]} days.");

            Room cheapest = null;
            foreach (var r in sim.State.Rooms)
                if (r.Type == RoomType.Classroom && sim.Facilities.CanUpgrade(r, out _)
                    && (cheapest == null || sim.Facilities.UpgradeCost(r) < sim.Facilities.UpgradeCost(cheapest)))
                    cheapest = r;
            if (cheapest != null)
            {
                m.RoomId = cheapest.Id;
                float cost = sim.Facilities.UpgradeCost(cheapest);
                int extra = def.Capacity[cheapest.Level] - def.Capacity[cheapest.Level - 1];
                m.AddOption("upgrade", $"Upgrade {cheapest.Name} to level {cheapest.Level + 1} ({Ev.Money(cost)})",
                    $"+{extra} seats and a nicer room. Ready in {def.BuildDays[cheapest.Level]} days.", cost: cost);
            }
            return $"We have {ix.Students} students and only {ix.ClassroomSeats} classroom seats. Overcrowded lessons are dragging education down.";
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option)
        {
            if (option == "build") return DoBuild(sim, RoomType.Classroom);
            if (option == "upgrade") return sim.UpgradeRoom(m.RoomId).Message;
            return "";
        }
    }

    sealed class TeachersProblemEvent : AssistantProblemEvent
    {
        protected override ProblemKind Kind => ProblemKind.Teachers;
        protected override string Subject(Simulation sim, InboxMessage m) => "Teachers are stretched thin";

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            OfferHire(sim, m, StaffRole.Teacher, "hire", "teacher");
            return $"Each teacher has {sim.Indices.StudentsPerTeacher:0.0} students. Above 14, lessons suffer and teachers wear out faster.";
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option) => option == "hire" ? DoHire(sim, m) : "";
    }

    sealed class SafetyProblemEvent : AssistantProblemEvent
    {
        protected override bool Soft => true;
        protected override ProblemKind Kind => ProblemKind.Safety;
        protected override MessagePriority Priority => MessagePriority.Important;
        protected override string Subject(Simulation sim, InboxMessage m) => "Discipline is slipping";

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            var s = sim.State;
            if (sim.Indices.GuardCoverage < 1f) OfferHire(sim, m, StaffRole.Security, "guard", "security guard");
            m.AddOption("assembly", "Hold a discipline assembly (free)", "Incidents calm down. Students sulk for a few days.");
            return $"Safety and discipline is at {s.Stat(StatId.SafetyDiscipline):0}." +
                   (s.IncidentPressure > 5f ? " There have been several incidents lately." : "") +
                   " Parents care about this more than almost anything.";
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "guard":
                    return DoHire(sim, m);
                case "assembly":
                    sim.State.IncidentPressure *= 0.5f;
                    sim.AllStudentsMood("Lecture about behavior", -2f, 10);
                    sim.AddReputationModifier(ReputationId.Safety, "Discipline drive", 3f, 45);
                    return "The assembly made its point.";
                default:
                    return "";
            }
        }
    }

    sealed class FoodProblemEvent : AssistantProblemEvent
    {
        protected override bool Soft => true;
        protected override ProblemKind Kind => ProblemKind.Food;
        protected override string Subject(Simulation sim, InboxMessage m) => "Lunch complaints";

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            var s = sim.State;
            if (s.Policies.Food < FoodTier.Premium)
            {
                var better = s.Policies.Food + 1;
                float extra = s.Students.Count * Balance.MealsPerDay * (Balance.MealCost(better) - Balance.MealCost(s.Policies.Food)) * Calendar.DaysPerMonth;
                m.AddOption("menu", $"Switch to the {better} menu (+{Ev.Money(extra)}/month)", "Better food from tomorrow.");
            }
            if (sim.Indices.ChefCoverage < 1f) OfferHire(sim, m, StaffRole.Chef, "chef", "chef");
            return $"Food quality is {s.Stat(StatId.FoodQuality):0}, but families at a school like ours expect about {Balance.FoodExpectation(s.Prestige):0}. " +
                   $"We're serving the {s.Policies.Food} menu.";
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "menu":
                    if (sim.State.Policies.Food < FoodTier.Premium) sim.SetFoodTier(sim.State.Policies.Food + 1);
                    return "The new menu starts tomorrow.";
                case "chef":
                    return DoHire(sim, m);
                default:
                    return "";
            }
        }
    }

    sealed class DirtyProblemEvent : AssistantProblemEvent
    {
        protected override bool Soft => true;
        protected override ProblemKind Kind => ProblemKind.Dirty;
        protected override string Subject(Simulation sim, InboxMessage m) => "The campus is getting grubby";

        static float CleanCost(Simulation sim) => Ev.Round100(1200f + sim.State.Rooms.Count * 110f);

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            float cost = CleanCost(sim);
            m.AddOption("deepclean", $"Book a professional deep clean ({Ev.Money(cost)})", "Every room sparkles again.", cost: cost);
            OfferHire(sim, m, StaffRole.Custodian, "custodian", "custodian");
            return $"Average cleanliness is down to {sim.Indices.AvgCleanliness:0}. Grubby dorms and kitchens upset students, parents and, sooner or later, inspectors.";
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "deepclean":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Upkeep, -CleanCost(sim), "Professional deep clean");
                    foreach (var r in sim.State.Rooms) r.Cleanliness = Math.Max(r.Cleanliness, 92f);
                    return "The cleaners have been through every room.";
                case "custodian":
                    return DoHire(sim, m);
                default:
                    return "";
            }
        }
    }

    sealed class RepairsProblemEvent : AssistantProblemEvent
    {
        protected override ProblemKind Kind => ProblemKind.Repairs;
        protected override string Subject(Simulation sim, InboxMessage m) => sim.State.FindRoom(m.RoomId) is Room r ? $"{r.Name} is falling apart" : "Rooms need repairs";

        static float AllCost(Simulation sim)
        {
            float cost = 0f;
            foreach (var r in sim.State.Rooms)
                if (r.IsBuilt(sim.Day) && r.Condition < 60f) cost += sim.Facilities.RepairCost(r);
            return Ev.Round100(cost);
        }

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            var worst = sim.Assistants.WorstRoom();
            if (worst == null) return null;
            m.RoomId = worst.Id;
            int others = 0;
            foreach (var r in sim.State.Rooms)
                if (r != worst && r.IsBuilt(sim.Day) && r.Condition < 60f) others++;

            float cost = Ev.Round100(sim.Facilities.RepairCost(worst));
            m.AddOption("repair_worst", $"Repair {worst.Name} ({Ev.Money(cost)})", "Back to 100% condition.", cost: cost);
            if (others > 0)
            {
                float all = AllCost(sim);
                m.AddOption("repair_all", $"Repair every worn room ({Ev.Money(all)})", $"Fixes {others + 1} rooms below 60%.", cost: all);
            }
            if (sim.CountStaff(StaffRole.Maintenance, activeOnly: false) == 0) OfferHire(sim, m, StaffRole.Maintenance, "maintenance", "maintenance worker");
            return $"{worst.Name} is down to {worst.Condition:0}% condition" + (others > 0 ? $", and {others} other room{(others == 1 ? " is" : "s are")} worn too." : ".") +
                   " Worn rooms are unsafe and fail inspections.";
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "repair_worst":
                    return sim.RepairRoom(m.RoomId).Message;
                case "repair_all":
                    int fixedCount = 0;
                    foreach (var r in sim.State.Rooms)
                        if (r.IsBuilt(sim.Day) && r.Condition < 60f && sim.Facilities.Repair(r)) fixedCount++;
                    return $"Repaired {fixedCount} room{(fixedCount == 1 ? "" : "s")}.";
                case "maintenance":
                    return DoHire(sim, m);
                default:
                    return "";
            }
        }
    }

    sealed class UnhappyProblemEvent : AssistantProblemEvent
    {
        protected override bool Soft => true;
        protected override ProblemKind Kind => ProblemKind.Unhappy;
        protected override string Subject(Simulation sim, InboxMessage m) => "Students are miserable";

        static float PartyCost(Simulation sim) => Ev.Round100(800f + sim.State.Students.Count * 30f);
        static float MovieCost(Simulation sim) => Ev.Round10(150f + sim.State.Students.Count * 6f);

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            float party = PartyCost(sim), movie = MovieCost(sim);
            m.AddOption("party", $"Throw a school party ({Ev.Money(party)})", "A big mood lift for three weeks.", cost: party);
            m.AddOption("movie", $"Movie night ({Ev.Money(movie)})", "A small lift for ten days.", cost: movie);
            return $"Student happiness is down to {sim.State.Stat(StatId.StudentSatisfaction):0}. Unhappy students study less and misbehave more, and their parents hear all about it.";
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "party":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Other, -PartyCost(sim), "School party");
                    sim.AllStudentsMood("School party", 6f, 21);
                    return "The party was a hit.";
                case "movie":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Other, -MovieCost(sim), "Movie night");
                    sim.AllStudentsMood("Movie night", 3f, 10);
                    return "Popcorn for everyone.";
                default:
                    return "";
            }
        }
    }

    sealed class StaffMoraleProblemEvent : AssistantProblemEvent
    {
        protected override bool Soft => true;
        protected override ProblemKind Kind => ProblemKind.StaffMorale;
        protected override string Subject(Simulation sim, InboxMessage m) => "Staff are running on empty";

        static float LunchCost(Simulation sim) => Ev.Round10(200f + sim.State.Staff.Count * 40f);
        static float BonusCost(Simulation sim) => Ev.Round100(sim.State.Staff.Count * 250f);

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            float lunch = LunchCost(sim), bonus = BonusCost(sim);
            m.AddOption("lunch", $"Staff appreciation lunch ({Ev.Money(lunch)})", "A friendly boost for a month.", cost: lunch);
            m.AddOption("bonus", $"A surprise bonus for everyone ({Ev.Money(bonus)})", "A big boost for two months.", cost: bonus);
            return $"Staff morale is {sim.State.Stat(StatId.StaffMorale):0}. Unhappy staff work slower, ask for raises and start looking elsewhere.";
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "lunch":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Other, -LunchCost(sim), "Staff appreciation lunch");
                    sim.AllStaffMood("Appreciation lunch", 5f, 30);
                    return "The staff room smells of lasagne.";
                case "bonus":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Bonuses, -BonusCost(sim), "Surprise staff bonus");
                    sim.AllStaffMood("Surprise bonus", 10f, 60);
                    return "Bonuses paid.";
                default:
                    return "";
            }
        }
    }

    sealed class UpgradeIdeaEvent : AssistantProblemEvent
    {
        protected override ProblemKind Kind => ProblemKind.Upgrade;
        public override int CooldownDays => 45;
        protected override string Subject(Simulation sim, InboxMessage m) => sim.State.FindRoom(m.RoomId) is Room r ? $"Upgrade idea: {r.Name}" : "An upgrade idea";

        protected override string Compose(Simulation sim, InboxMessage m)
        {
            var room = sim.Assistants.SuggestUpgrade();
            if (room == null) return null;
            m.RoomId = room.Id;
            float cost = sim.Facilities.UpgradeCost(room);
            int next = room.Level + 1;
            m.AddOption("upgrade", $"Upgrade {room.Name} to level {next} ({Ev.Money(cost)})",
                $"Ready in {room.Def.BuildDays[room.Level]} days.", cost: cost);
            return $"We're in decent shape, with {Ev.Money(sim.State.Finance.Cash)} in the bank. {room.Name} is one of our weakest rooms: " +
                   $"level {next} would bring \"{room.Def.LevelNames[room.Level].ToLowerInvariant()}\".";
        }

        protected override string Apply(Simulation sim, InboxMessage m, string option) =>
            option == "upgrade" ? sim.UpgradeRoom(m.RoomId).Message : "";
    }

    /// <summary>The tutorial's practice decision: small stakes, no wrong answer.</summary>
    sealed class TutorialChoiceEvent : EventTemplate
    {
        public override string Id => "tutorial_choice";
        public override bool Random => false;
        public override bool InSessionOnly => false;
        public override int CooldownDays => 0;
        public override int ResponseDays => 30;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var a = sim.State.Assistant;
            Ev.From(m, a != null ? SenderType.Assistant : SenderType.Government, a?.Name ?? "Board of Trustees", "Our first decision", MessagePriority.Important);
            m.Body = "We have a little money to spend before the term gets going. What matters most to you?\n\n" +
                     "Tip: letters with choices have a deadline. If you don't answer in time, something happens anyway.";
            m.AddOption("classrooms", "Freshen up the classrooms ($2,000)", "Brighter, cleaner rooms and a small boost to academic reputation.", cost: 2000f);
            m.AddOption("party", "Throw a welcome party ($1,000)", "Students start the year happy.", cost: 1000f);
            m.AddOption("save", "Keep the money", "Nothing changes. The money stays in the bank.");
            if (a != null)
            {
                m.AdvisorOptionId = a.Style switch
                {
                    AssistantStyle.Cheerful => "party",
                    AssistantStyle.Charming => "party",
                    AssistantStyle.Bookish => "classrooms",
                    AssistantStyle.Handy => "classrooms",
                    _ => "save",
                };
            }
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "classrooms":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Upkeep, -2000f, "Classroom refresh");
                    foreach (var r in sim.State.Rooms)
                    {
                        if (r.Type != RoomType.Classroom) continue;
                        r.Condition = Math.Max(r.Condition, 90f);
                        r.Cleanliness = 100f;
                    }
                    sim.State.Campus.Touch();
                    sim.AddReputationModifier(ReputationId.Academic, "Fresh classrooms", 2f, 120);
                    return "The classrooms look great.";
                case "party":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Other, -1000f, "Welcome party");
                    sim.AllStudentsMood("Welcome party", 6f, 30);
                    return "Everyone had a great time.";
                default:
                    return "Sensible. Money in the bank is a safety net.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "save");
    }

    /// <summary>After bad press, a reporter wants the school's side of the story.</summary>
    sealed class PressInquiryEvent : EventTemplate
    {
        public override string Id => "press_inquiry";
        public override bool Random => false;
        public override bool InSessionOnly => false;
        public override int CooldownDays => 20;
        public override int ResponseDays => 6;

        static float SpinChance(Simulation sim)
        {
            float chance = 0.35f + sim.State.Stat(StatId.Trust) / 400f;
            if (sim.State.Assistant != null && sim.State.Assistant.Style == AssistantStyle.Charming) chance += 0.25f;
            return MathUtil.Clamp(chance, 0.1f, 0.9f);
        }

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var story = sim.State.News.Find(n => n.Id == m.OtherId);
            if (story == null || story.EffectLabel == null) return false;
            var a = sim.State.Assistant;
            Ev.From(m, a != null ? SenderType.Assistant : SenderType.Press, a?.Name ?? story.Outlet, $"A reporter is asking about \"{story.Headline}\"", MessagePriority.Important);
            m.Tag = story.EffectLabel;
            m.Amount = (int)story.EffectRep;
            m.Body = $"{story.Outlet} wants our side of the story before they run a follow-up. How do we respond?";
            m.AddOption("apologize", "Own it and apologize", "Trust +2. The story fades faster.");
            m.AddOption("spin", "Put a positive spin on it", $"{Ev.Pct(SpinChance(sim))} chance to turn it around. If it backfires, the follow-up is worse.");
            m.AddOption("silent", "No comment", "They run it without us.");
            return true;
        }

        ReputationModifier FindModifier(Simulation sim, InboxMessage m)
        {
            foreach (var mod in sim.State.ReputationModifiers)
                if (mod.Label == m.Tag && (int)mod.Rep == (int)m.Amount) return mod;
            return null;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var mod = FindModifier(sim, m);
            switch (option)
            {
                case "apologize":
                    sim.ChangeTrust(2f);
                    if (mod != null) mod.Value *= 0.5f;
                    return "Readers appreciated the honesty.";
                case "spin":
                    if (sim.Rng.Chance(SpinChance(sim)))
                    {
                        if (mod != null) mod.Value = Math.Abs(mod.Value) * 0.3f;
                        return "The follow-up was surprisingly kind to us.";
                    }
                    if (mod != null) mod.Value *= 1.5f;
                    sim.ChangeTrust(-2f);
                    return "It backfired. The follow-up was worse.";
                default:
                    return "They ran the story without our comment.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "silent");
    }
}
