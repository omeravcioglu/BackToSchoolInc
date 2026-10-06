using System;
using System.Collections.Generic;

namespace SchoolSim
{
    sealed class StudentRequestEvent : EventTemplate
    {
        public override string Id => "student_request";
        public override int CooldownDays => 18;
        public override float Weight(Simulation sim) => 1.1f;

        List<string> Variants(Simulation sim)
        {
            var st = sim.State;
            var v = new List<string>();
            if (!HasRoom(sim, RoomType.GamingRoom)) v.Add("gaming_room");
            if (st.Policies.Curfew != CurfewPolicy.Relaxed) v.Add("curfew");
            if (sim.IsUnlocked(Feature.Clubs) && MissingClub(sim) != null) v.Add("club");
            if (st.Policies.InternetLevel < 3) v.Add("wifi");
            if (st.Policies.Food < FoodTier.Premium && st.Stat(StatId.FoodQuality) < 70f) v.Add("food");
            if (sim.Indices.SportsFacilities < 2 && !HasRoom(sim, RoomType.BasketballCourt)) v.Add("sports");
            if (!HasRoom(sim, RoomType.StudentLounge)) v.Add("lounge");
            foreach (var r in st.Rooms)
                if (r.Type == RoomType.DormRoom && r.Level <= 2 && sim.Indices.ResidentsOf(r.Id).Count > 0) { v.Add("dorm_gear"); break; }
            return v;
        }

        static bool HasRoom(Simulation sim, RoomType type) => sim.State.Rooms.Exists(r => r.Type == type);

        static ClubType? MissingClub(Simulation sim)
        {
            foreach (ClubType t in Enum.GetValues(typeof(ClubType)))
            {
                if (t == ClubType.Robotics || t == ClubType.Science || t == ClubType.Gaming) continue;
                if (!sim.State.Clubs.Exists(c => c.Type == t)) return t;
            }
            return null;
        }

        static bool HasStudentGovernment(Simulation sim) => sim.State.Clubs.Exists(c => c.Type == ClubType.StudentGovernment);

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var variants = Variants(sim);
            if (variants.Count == 0) return false;
            string v = sim.Rng.Pick(variants);
            m.Tag = v;

            var rep = Ev.RandomStudent(sim, s => s.Has(StudentTrait.Leader)) ?? Ev.RandomStudent(sim, s => true);
            if (rep == null) return false;
            m.StudentId = rep.Id;
            bool gov = HasStudentGovernment(sim);
            string sender = gov ? "Student Government" : rep.FullName;
            var st = sim.State;

            switch (v)
            {
                case "gaming_room":
                    Ev.From(m, SenderType.Student, sender, "Can we get a gaming room?");
                    m.Body = "There is nothing to do in the evenings. A gaming room would make dorm life so much better. Pretty please?";
                    m.AddOption("promise", "Promise a Gaming Room within 60 days", "Students happy now. Promise: build a Gaming Room within 60 days.");
                    m.AddOption("reject", "No: evenings are for studying", "Students disappointed.");
                    m.AddOption("delay", "Maybe next year", "Mild disappointment.");
                    break;
                case "curfew":
                    Ev.From(m, SenderType.Student, sender, "Longer curfew, please");
                    m.Body = $"The {st.Policies.Curfew.ToString().ToLower()} curfew feels like prison. Older students especially want an extra hour in the evenings.";
                    m.AddOption("approve", "Relax the curfew", "Students happier; a few more incidents.");
                    m.AddOption("trial", "Weekends only", "A small, safer compromise.");
                    m.AddOption("reject", "Keep the current curfew", "Students disappointed.");
                    break;
                case "club":
                {
                    var club = MissingClub(sim).Value;
                    m.OtherId = (int)club;
                    Ev.From(m, SenderType.Student, sender, $"Can we start a {club} club?");
                    m.Body = $"A group of us want to start a {club} club. We just need a room and a small budget.";
                    var approve = m.AddOption("approve", $"Found the {club} club now ({Ev.Money(ClubSystem.FoundingCost)})", "Students happy; student life improves.", cost: ClubSystem.FoundingCost);
                    if (!sim.Clubs.CanCreate(club, out string why)) approve.DisabledReason = why;
                    m.AddOption("promise", "Promise to found it within 45 days", "Students happy now. Promise: the club exists within 45 days.");
                    m.AddOption("reject", "Not right now", "Students disappointed.");
                    break;
                }
                case "wifi":
                {
                    float cost = Balance.InternetUpgradeCost(st.Policies.InternetLevel + 1);
                    Ev.From(m, SenderType.Student, sender, "The Wi-Fi is terrible");
                    m.Body = "Nobody can load anything in the dorms after 8pm. We can't even do homework. Can the school fix the internet?";
                    m.AddOption("upgrade", $"Upgrade the network now ({Ev.Money(cost)})", $"Students happy. Internet level {st.Policies.InternetLevel + 1}; higher monthly cost.", cost: cost);
                    m.AddOption("promise", "Promise an upgrade within 45 days", "Students happy now. Promise: better internet in 45 days.");
                    m.AddOption("reject", "The network is fine", "Students annoyed.");
                    break;
                }
                case "food":
                {
                    var next = st.Policies.Food + 1;
                    float extra = st.Students.Count * Balance.MealsPerDay * Calendar.DaysPerMonth * (Balance.MealCost(next) - Balance.MealCost(st.Policies.Food));
                    m.OtherId = (int)next;
                    Ev.From(m, SenderType.Student, sender, "Better food, please");
                    m.Body = "The cafeteria food is sad. Could we get fresher meals, more choice, maybe a salad bar?";
                    m.AddOption("upgrade", $"Switch to the {next} menu now", $"Students happy. Food costs +{Ev.Money(extra)}/mo.");
                    m.AddOption("promise", "Promise better food within 30 days", $"Students happy now. Promise: {next} menu within 30 days.");
                    m.AddOption("reject", "The budget doesn't allow it", "Students disappointed.");
                    break;
                }
                case "sports":
                    Ev.From(m, SenderType.Student, sender, "We need somewhere to play sports");
                    m.Body = "There's nowhere to play. Even a basketball court would do. Athletic students are going crazy.";
                    m.AddOption("promise", "Promise a basketball court within 60 days", "Students happy now. Promise: a Basketball Court within 60 days.");
                    m.AddOption("reject", "Not in the budget", "Students disappointed, athletes especially.");
                    break;
                case "lounge":
                    Ev.From(m, SenderType.Student, sender, "A place to hang out");
                    m.Body = "We have nowhere to just sit and talk outside our rooms. Could the school set up a student lounge?";
                    m.AddOption("promise", "Promise a Student Lounge within 45 days", "Students happy now. Promise: a Student Lounge within 45 days.");
                    m.AddOption("reject", "Not now", "Students disappointed.");
                    break;
                case "dorm_gear":
                {
                    int residents = 0;
                    foreach (var r in st.Rooms)
                        if (r.Type == RoomType.DormRoom && r.Level <= 2) residents += sim.Indices.ResidentsOf(r.Id).Count;
                    m.Amount = residents * 120f;
                    Ev.From(m, SenderType.Student, sender, "New mattresses and mini-fridges?");
                    m.Body = $"The basic dorm rooms have lumpy mattresses and nowhere to keep snacks. {residents} of us would love an upgrade.";
                    m.AddOption("approve", $"Buy new mattresses and mini-fridges ({Ev.Money(m.Amount)})", "Residents of basic rooms much happier for months.", cost: m.Amount);
                    m.AddOption("reject", "Not this year", "Students disappointed.");
                    break;
                }
            }
            if (gov) m.Body += "\n\n(Student Government is backing this request.)";
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var st = sim.State;
            int day = sim.Day;
            bool gov = HasStudentGovernment(sim);
            float rejectScale = gov ? 0.6f : 1f;

            if (option == "reject" || option == "ignore")
            {
                sim.AllStudentsMood("Request rejected", -3f * rejectScale, 20);
                return "Students are disappointed.";
            }
            if (option == "delay")
            {
                sim.AllStudentsMood("Request postponed", -1f, 15);
                return "Students will ask again.";
            }

            if (gov) sim.ChangeTrust(1f);
            switch (m.Tag)
            {
                case "gaming_room":
                    sim.AllStudentsMood("Gaming room coming", 4f, 30);
                    sim.Promises.Make(PromiseKind.RoomTypeExists, "Build a Gaming Room", 60, 1f, (int)RoomType.GamingRoom, SenderType.Student);
                    return "Students are thrilled. Build a Gaming Room within 60 days.";
                case "curfew":
                    if (option == "approve")
                    {
                        st.Policies.Curfew = st.Policies.Curfew == CurfewPolicy.Strict ? CurfewPolicy.Normal : CurfewPolicy.Relaxed;
                        sim.AllStudentsMood("Curfew relaxed", 4f, 30);
                        return $"Curfew is now {st.Policies.Curfew}.";
                    }
                    sim.AllStudentsMood("Weekend curfew extension", 3f, 60);
                    st.IncidentPressure += 2f;
                    return "Weekend curfew extended.";
                case "club":
                {
                    var type = (ClubType)m.OtherId;
                    if (option == "approve")
                    {
                        var club = sim.Clubs.Create(type);
                        if (club != null)
                        {
                            sim.AllStudentsMood("New club founded", 2f, 20);
                            return $"The {club.Name} is up and running.";
                        }
                    }
                    sim.AllStudentsMood("Club promised", 2f, 30);
                    sim.Promises.Make(PromiseKind.ClubExists, $"Found the {type} club", 45, 1f, (int)type, SenderType.Student);
                    return "Students are waiting for their club.";
                }
                case "wifi":
                    if (option == "upgrade")
                    {
                        float cost = Balance.InternetUpgradeCost(st.Policies.InternetLevel + 1);
                        st.Finance.Post(day, LedgerCategory.Construction, -cost, "Network upgrade");
                        st.Policies.InternetLevel++;
                        sim.AllStudentsMood("Fast Wi-Fi", 4f, 45);
                        return $"Internet upgraded to level {st.Policies.InternetLevel}.";
                    }
                    sim.AllStudentsMood("Wi-Fi upgrade promised", 2f, 30);
                    sim.Promises.Make(PromiseKind.InternetLevelAtLeast, "Upgrade the school internet", 45, st.Policies.InternetLevel + 1, to: SenderType.Student);
                    return "Upgrade the internet from the Finance tab within 45 days.";
                case "food":
                    if (option == "upgrade")
                    {
                        st.Policies.Food = (FoodTier)m.OtherId;
                        sim.AllStudentsMood("Better food", 3f, 30);
                        return $"The cafeteria now serves the {st.Policies.Food} menu.";
                    }
                    sim.AllStudentsMood("Better food promised", 2f, 30);
                    sim.Promises.Make(PromiseKind.FoodTierAtLeast, $"Switch to the {(FoodTier)m.OtherId} menu", 30, m.OtherId, to: SenderType.Student);
                    return "Upgrade the menu within 30 days.";
                case "sports":
                    sim.AllStudentsMood("Court promised", 3f, 30);
                    sim.Promises.Make(PromiseKind.RoomTypeExists, "Build a Basketball Court", 60, 1f, (int)RoomType.BasketballCourt, SenderType.Student);
                    return "Build a Basketball Court within 60 days.";
                case "lounge":
                    sim.AllStudentsMood("Lounge promised", 3f, 30);
                    sim.Promises.Make(PromiseKind.RoomTypeExists, "Build a Student Lounge", 45, 1f, (int)RoomType.StudentLounge, SenderType.Student);
                    return "Build a Student Lounge within 45 days.";
                case "dorm_gear":
                    st.Finance.Post(day, LedgerCategory.Upkeep, -m.Amount, "Dorm furnishings");
                    foreach (var r in st.Rooms)
                        if (r.Type == RoomType.DormRoom && r.Level <= 2)
                            foreach (var s in sim.Indices.ResidentsOf(r.Id)) s.Moodlets.Set("New mattress and mini-fridge", 6f, day + 90);
                    return "The basic rooms are a bit more comfortable now.";
            }
            return "";
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "reject");
    }

    sealed class RoommateConflictEvent : EventTemplate
    {
        public override string Id => "roommate_conflict";
        public override AdminRole Handler => AdminRole.DormDirector;
        public override int CooldownDays => 12;

        public override float Weight(Simulation sim) => FindPair(sim, out _, out _) ? 1.2f : 0f;

        static bool FindPair(Simulation sim, out Student a, out Student b)
        {
            a = b = null;
            float worst = -35f;
            foreach (var kv in sim.Indices.Residents)
            {
                var list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        float score = sim.State.Relationships.Score(list[i].Id, list[j].Id);
                        if (score < worst) { worst = score; a = list[i]; b = list[j]; }
                    }
            }
            return a != null;
        }

        public override bool Build(Simulation sim, InboxMessage m)
        {
            Student a = sim.State.FindStudent(m.StudentId), b = sim.State.FindStudent(m.StudentId2);
            if (a == null || b == null)
            {
                if (!FindPair(sim, out a, out b)) return false;
                m.StudentId = a.Id;
                m.StudentId2 = b.Id;
            }
            if (a.DormRoomId != b.DormRoomId || a.DormRoomId < 0) return false;
            var room = sim.State.FindRoom(a.DormRoomId);
            m.RoomId = room.Id;

            string cause = a.Has(StudentTrait.Messy) && b.Has(StudentTrait.CleanFreak) || b.Has(StudentTrait.Messy) && a.Has(StudentTrait.CleanFreak)
                ? "It started over mess and cleaning, and now they can't stand each other."
                : a.Has(StudentTrait.NightOwl) != b.Has(StudentTrait.NightOwl)
                    ? "One stays up all night; the other can't sleep."
                    : "Nobody quite remembers how it started.";

            Ev.From(m, SenderType.StudentAffairs, "Dorm supervisor", $"Roommate conflict in {room.Name}");
            m.Body = $"{a.FullName} and {b.FullName} are at war. {cause} The rest of the room is miserable too.";

            var move = m.AddOption("move", $"Move {a.FirstName} to another room", "Ends the conflict. Uses a free bed elsewhere.");
            if (sim.Facilities.FindBestBed(a, a.DormRoomId) == null) move.DisabledReason = "No free bed in another room";
            m.AddOption("mediate", "Hold a mediation session", "May repair the relationship; works better with a counselor.");
            m.AddOption("rules", "Set room rules (cleaning rota, quiet hours)", "Softens the conflict, especially over mess and noise.");
            m.AddOption("ignore", "Let them sort it out", "Both unhappier.", "Conflicts left alone often become fights.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var a = sim.State.FindStudent(m.StudentId);
            var b = sim.State.FindStudent(m.StudentId2);
            if (a == null || b == null) return Ev.Gone("One of them");
            var rel = sim.State.Relationships.GetOrCreate(a.Id, b.Id);
            int day = sim.Day;
            switch (option)
            {
                case "move":
                {
                    var room = sim.Facilities.FindBestBed(a, a.DormRoomId);
                    if (room == null) goto case "rules";
                    sim.Facilities.AssignBed(a, room);
                    a.Remember(day, $"Moved to {room.Name} to get away from {b.FullName}.");
                    return $"{a.FirstName} moved to {room.Name}.";
                }
                case "mediate":
                {
                    float chance = 0.35f + sim.Indices.CounselorCoverage * 0.35f + sim.Progression.NegotiationBonus;
                    if (sim.Rng.Chance(chance))
                    {
                        rel.Score = MathUtil.Clamp(rel.Score + 35f, -100f, 100f);
                        a.Moodlets.Set("Made peace with roommate", 3f, day + 14);
                        b.Moodlets.Set("Made peace with roommate", 3f, day + 14);
                        return "Mediation worked. They've agreed to a truce.";
                    }
                    rel.Score = MathUtil.Clamp(rel.Score - 5f, -100f, 100f);
                    return "Mediation failed. It may have made things worse.";
                }
                case "rules":
                {
                    bool mess = a.Has(StudentTrait.Messy) || b.Has(StudentTrait.Messy) || a.Has(StudentTrait.NightOwl) != b.Has(StudentTrait.NightOwl);
                    rel.Score = MathUtil.Clamp(rel.Score + (mess ? 20f : 12f), -100f, 100f);
                    return "Room rules posted on the door.";
                }
                default:
                    a.Moodlets.Set("Roommate war", -4f, day + 14);
                    b.Moodlets.Set("Roommate war", -4f, day + 14);
                    if (sim.Rng.Chance(0.3f)) rel.Score = MathUtil.Clamp(rel.Score - 10f, -100f, 100f);
                    return "The conflict simmers on.";
            }
        }

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p)
        {
            var a = sim.State.FindStudent(m.StudentId);
            return a != null && sim.Facilities.FindBestBed(a, a.DormRoomId) != null ? "move" : "mediate";
        }
    }

    abstract class DisciplineEventBase : EventTemplate
    {
        public override bool Random => false;
        public override AdminRole Handler => AdminRole.StudentAffairsDirector;

        protected static void Punish(Student s, int day, float discipline, float mood, int days, string label)
        {
            s.Discipline = MathUtil.Clamp100(s.Discipline + discipline);
            s.Behavior = MathUtil.Clamp100(s.Behavior + discipline * 2f);
            if (mood != 0) s.Moodlets.Set(label, mood, day + days);
        }
    }

    sealed class StudentFightEvent : DisciplineEventBase
    {
        public override string Id => "student_fight";
        public override int CooldownDays => 7;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var a = sim.State.FindStudent(m.StudentId);
            var b = sim.State.FindStudent(m.StudentId2);
            if (a == null || b == null) return false;
            Ev.From(m, SenderType.StudentAffairs, "Student Affairs", $"Fight: {a.FirstName} vs {b.FirstName}", MessagePriority.Important);
            m.Body = $"{a.FullName} and {b.FullName} got into a physical fight in the hallway. Neither is badly hurt, but half the school saw it. How should we respond?";
            m.AddOption("suspend", "Suspend both for three days", "Strong message: discipline up, safety up. Grades and happiness dip; families upset.");
            m.AddOption("detention", "Detention for both", "Moderate response.");
            m.AddOption("parents", "Call both families in", "Discipline up; families embarrassed. Can ease the rivalry.");
            m.AddOption("warning", "A warning only", "Students relieved.", "Soft responses invite repeat incidents.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var a = sim.State.FindStudent(m.StudentId);
            var b = sim.State.FindStudent(m.StudentId2);
            int day = sim.Day;
            var both = new List<Student>();
            if (a != null) both.Add(a);
            if (b != null) both.Add(b);
            switch (option)
            {
                case "suspend":
                    foreach (var s in both)
                    {
                        Punish(s, day, 3f, -8f, 14, "Suspended");
                        s.Grades = MathUtil.Clamp100(s.Grades - 3f);
                        s.ParentMoodlets.Set("Child suspended", -4f, day + 45);
                        s.Remember(day, "Suspended for fighting.");
                    }
                    sim.State.IncidentPressure = Math.Max(0f, sim.State.IncidentPressure - 3f);
                    return "Both suspended. The hallways are quieter.";
                case "detention":
                    foreach (var s in both) Punish(s, day, 2f, -3f, 7, "Detention");
                    sim.State.IncidentPressure = Math.Max(0f, sim.State.IncidentPressure - 1f);
                    return "Detention served.";
                case "parents":
                    foreach (var s in both)
                    {
                        Punish(s, day, 5f, -2f, 7, "Parents called in");
                        s.ParentMoodlets.Set("Called in over a fight", -5f, day + 45);
                    }
                    if (a != null && b != null)
                    {
                        var rel = sim.State.Relationships.GetOrCreate(a.Id, b.Id);
                        rel.Score = MathUtil.Clamp(rel.Score + 10f, -100f, 100f);
                    }
                    return "The families were not pleased, but the students got the message.";
                default:
                    sim.State.IncidentPressure += 2f;
                    if (a != null && b != null)
                    {
                        var rel = sim.State.Relationships.GetOrCreate(a.Id, b.Id);
                        rel.Score = MathUtil.Clamp(rel.Score - 5f, -100f, 100f);
                    }
                    return "They got off with a warning.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "warning");

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) =>
            m.FindOption(p.DefaultDisciplineResponse) != null ? p.DefaultDisciplineResponse : "detention";
    }

    sealed class LateNightPartyEvent : DisciplineEventBase
    {
        public override string Id => "late_night_party";
        public override int CooldownDays => 10;

        static List<Student> Participants(Simulation sim, InboxMessage m)
        {
            var list = new List<Student>();
            var host = sim.State.FindStudent(m.StudentId);
            if (host == null) return list;
            list.Add(host);
            foreach (var o in sim.Indices.ResidentsOf(m.RoomId))
                if (o.Id != host.Id && sim.State.Relationships.Score(host.Id, o.Id) >= 30f) list.Add(o);
            return list;
        }

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var host = sim.State.FindStudent(m.StudentId);
            var room = sim.State.FindRoom(m.RoomId);
            if (host == null || room == null) return false;
            int n = Participants(sim, m).Count;
            Ev.From(m, SenderType.StudentAffairs, "Night supervisor", $"Party in {room.Name} at 2am");
            m.Body = $"We broke up a loud party in {room.Name} last night, hosted by {host.FullName} with {n - 1} friends. The room is a mess and a lamp is broken.";
            m.AddOption("punish", "Strict punishment for everyone involved", "Discipline up, fewer repeats. Participants unhappy.");
            m.AddOption("cleanup", "Make them clean up and pay for repairs", "Room restored; mild punishment.");
            m.AddOption("slide", "Let it slide this time", "Participants happy.", "Expect more parties.");
            m.AddOption("official", "Channel it: host an official weekend social ($800)", "Everyone happier; fewer secret parties.", cost: 800f);
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var people = Participants(sim, m);
            var room = sim.State.FindRoom(m.RoomId);
            int day = sim.Day;
            switch (option)
            {
                case "punish":
                    foreach (var s in people) Punish(s, day, 3f, -6f, 14, "Punished for the party");
                    sim.State.IncidentPressure = Math.Max(0f, sim.State.IncidentPressure - 2f);
                    sim.AllStudentsMood("Dorm crackdown", -1f, 10);
                    return "Everyone involved was punished.";
                case "cleanup":
                    foreach (var s in people) Punish(s, day, 2f, -2f, 7, "Cleaning duty");
                    if (room != null)
                    {
                        room.Cleanliness = MathUtil.Clamp100(room.Cleanliness + 30f);
                        room.Condition = MathUtil.Clamp100(room.Condition + 5f);
                    }
                    return "The room is spotless again.";
                case "official":
                    sim.State.Finance.Post(day, LedgerCategory.Other, -800f, "Weekend social");
                    sim.AllStudentsMood("School social", 4f, 14);
                    sim.State.IncidentPressure = Math.Max(0f, sim.State.IncidentPressure - 2f);
                    return "The official social was a hit.";
                default:
                    foreach (var s in people)
                    {
                        s.Moodlets.Set("Got away with a party", 3f, day + 10);
                        s.Discipline = MathUtil.Clamp100(s.Discipline - 2f);
                    }
                    sim.State.IncidentPressure += 3f;
                    return "No consequences this time.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "slide");

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) => p.DefaultDisciplineResponse switch
        {
            "suspend" => "punish",
            "warning" => "slide",
            _ => "cleanup",
        };
    }

    sealed class CheatingScandalEvent : DisciplineEventBase
    {
        public override string Id => "cheating_scandal";
        public override int CooldownDays => 20;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = sim.State.FindStudent(m.StudentId);
            if (s == null) return false;
            Ev.From(m, SenderType.Teacher, "Exam board", $"Cheating: {s.FullName}", MessagePriority.Important);
            m.Body = $"{s.FullName} was caught with answers during an exam. It looks like others may have seen the answers too. How do we handle it?";
            m.AddOption("fail", "Fail the students involved", "Grades drop; families upset. Integrity boosts academic reputation.");
            m.AddOption("retake", "Let them retake the exam", "Neutral. Costs teachers time.");
            m.AddOption("coverup", "Quietly cover it up", "Nothing happens... for now.", "If it leaks, the scandal will be far worse.");
            m.AddOption("campaign", "Run an academic integrity campaign ($1,000)", "Fewer incidents; academic reputation up.", cost: 1000f);
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStudent(m.StudentId);
            int day = sim.Day;
            switch (option)
            {
                case "fail":
                    if (s != null)
                    {
                        s.Grades = MathUtil.Clamp100(s.Grades - 10f);
                        s.Discipline = MathUtil.Clamp100(s.Discipline + 4f);
                        s.ParentMoodlets.Set("Child failed for cheating", -8f, day + 60);
                    }
                    sim.AddReputationModifier(ReputationId.Academic, "Upholds academic integrity", 1f, 90);
                    return "The failing grade stands.";
                case "retake":
                {
                    var t = Ev.RandomStaff(sim, x => x.Role == StaffRole.Teacher);
                    if (t != null) t.Energy = MathUtil.Clamp100(t.Energy - 5f);
                    s?.Moodlets.Set("Retaking an exam", -2f, day + 10);
                    return "A retake has been scheduled.";
                }
                case "coverup":
                    sim.Events.Schedule(new ScheduledEvent { Day = day + sim.Rng.Range(20, 40), TemplateId = Id, Tag = "leak", StudentId = m.StudentId });
                    return "The incident was quietly buried.";
                case "campaign":
                    sim.State.Finance.Post(day, LedgerCategory.Other, -1000f, "Academic integrity campaign");
                    sim.State.IncidentPressure = Math.Max(0f, sim.State.IncidentPressure - 2f);
                    sim.AddReputationModifier(ReputationId.Academic, "Integrity campaign", 2f, 90);
                    foreach (var x in sim.State.Students) x.Discipline = MathUtil.Clamp100(x.Discipline + 1f);
                    return "Posters are up and every class discussed honesty.";
                default:
                    return "";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "retake");

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) => "fail";

        public override void OnScheduled(Simulation sim, ScheduledEvent e)
        {
            if (e.Tag != "leak" || !sim.Rng.Chance(0.35f)) return;
            sim.ShockReputation(ReputationId.Academic, -6f);
            sim.AddReputationModifier(ReputationId.Academic, "Cheating cover-up exposed", -4f, 90);
            sim.ChangeTrust(-8f);
            sim.AllParentsMood("Cheating cover-up", -6f, 60);
            sim.State.Year.Highlight("A cheating cover-up leaked to the press");
            sim.Inbox.Notify(SenderType.Parent, "Parents' Association", "Cheating cover-up exposed",
                "A student posted about the exam cheating you covered up. Parents are furious and the local paper is calling.", MessagePriority.Urgent);
        }
    }

    sealed class TransferApplicantEvent : EventTemplate
    {
        public override string Id => "transfer_applicant";
        public override bool Random => false;
        public override AdminRole Handler => AdminRole.AdmissionsDirector;
        public override int CooldownDays => 0;
        public override int ResponseDays => 12;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var a = sim.Admissions.FindApplicant(m.OtherId);
            if (a == null) return false;
            var s = a.Student;
            Ev.From(m, SenderType.Admissions, "Admissions Office", $"Transfer application: {s.FullName}");
            string family = s.International ? $"The family, writing from {s.HomeCountry}," : "The family";
            m.Body = $"{a.Pitch} {family} wants {s.FirstName} to join grade {s.Grade} mid-year.\n\n{Ev.Profile(s)}\n\n" +
                     (a.RequestedAid > 0 ? $"They ask for {Ev.Pct(a.RequestedAid)} tuition aid. "
                         : s.International ? "They can pay full tuition and the international fee. " : "They can pay full tuition. ") +
                     $"Chance they accept a full-price offer: {Ev.Pct(sim.Admissions.AcceptChance(a, 0f, ScholarshipType.None))}.";

            bool beds = sim.Facilities.FreeBeds() > 0;
            var admit = m.AddOption("admit", "Offer a place at full tuition", "One more paying student.");
            if (!beds) admit.DisabledReason = "No free beds";
            if (a.RequestedAid > 0)
            {
                var aid = m.AddOption("aid", $"Offer a place with {Ev.Pct(a.RequestedAid)} aid",
                    $"Likely to accept ({Ev.Pct(sim.Admissions.AcceptChance(a, a.RequestedAid, ScholarshipType.None))}). Less tuition.");
                if (!beds) aid.DisabledReason = "No free beds";
            }
            m.AddOption("reject", "Decline the application", "No change.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var a = sim.Admissions.FindApplicant(m.OtherId);
            if (a == null) return "The family has already gone elsewhere.";
            string result;
            switch (option)
            {
                case "admit":
                    sim.Admissions.Admit(a, ScholarshipType.None, 0f, out result);
                    return result;
                case "aid":
                    var type = sim.IsUnlocked(Feature.Scholarships) ? ScholarshipType.FinancialAid : ScholarshipType.None;
                    sim.Admissions.Admit(a, type, a.RequestedAid, out result);
                    return result;
                default:
                    sim.Admissions.Reject(a);
                    return "Application declined.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "reject");

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p)
        {
            var a = sim.Admissions.FindApplicant(m.OtherId);
            if (a == null) return "reject";
            if (sim.Facilities.FreeBeds() <= 0) return "reject";
            bool good = a.Student.Academic >= p.AutoAdmitAcademicAbove || sim.Admissions.Score(a) >= 50f;
            if (!good) return "reject";
            return a.RequestedAid > 0 && a.RequestedAid <= p.AutoApproveDiscountBelow ? "aid" : "admit";
        }
    }

    sealed class AthleteRecruitEvent : EventTemplate
    {
        public override string Id => "athlete_recruit";
        public override int CooldownDays => 60;
        public override bool InSessionOnly => false;
        public override AdminRole Handler => AdminRole.AthleticDirector;

        public override float Weight(Simulation sim) =>
            sim.IsUnlocked(Feature.SportsPrograms) && sim.State.Teams.Count > 0 ? 0.5f : 0f;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var a = sim.Admissions.GenerateApplicant(transfer: true, eliteAthlete: true);
            var team = sim.State.Teams[sim.Rng.Range(0, sim.State.Teams.Count)];
            a.Student.SportTalent = team.Sport;
            a.Pitch = $"Elite {team.Sport} prospect.";
            a.ExpiresDay = sim.Day + 15;
            sim.State.Applicants.Add(a);
            m.OtherId = a.Student.Id;
            var s = a.Student;

            Ev.From(m, SenderType.Sports, "Athletics scout", $"Elite {team.Sport} recruit: {s.FullName}");
            m.Body = $"{s.FullName} is one of the best young {team.Sport} players in the region (athletic {s.Athletic:0}), but academically weak (academic {s.Academic:0}). " +
                     $"Several schools are chasing them.\n\n{Ev.Profile(s)}";
            var sch = m.AddOption("scholarship", "Offer a 50% athletic scholarship",
                $"Very likely to accept ({Ev.Pct(sim.Admissions.AcceptChance(a, 0.5f, ScholarshipType.Athletic))}). Big boost to the {team.Name}.");
            sch.RequiresFeature = Feature.Scholarships;
            m.AddOption("full", "Offer a place at full tuition", $"Acceptance chance {Ev.Pct(sim.Admissions.AcceptChance(a, 0f, ScholarshipType.None))}.");
            m.AddOption("decline", "Pass: we're an academic school first", "No change.");
            if (sim.Facilities.FreeBeds() <= 0)
                foreach (var o in m.Options) if (o.Id != "decline") o.DisabledReason = "No free beds";
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var a = sim.Admissions.FindApplicant(m.OtherId);
            if (a == null) return "The recruit has already signed elsewhere.";
            string result;
            bool ok;
            switch (option)
            {
                case "scholarship": ok = sim.Admissions.Admit(a, ScholarshipType.Athletic, 0.5f, out result); break;
                case "full": ok = sim.Admissions.Admit(a, ScholarshipType.None, 0f, out result); break;
                default:
                    sim.Admissions.Reject(a);
                    return "You passed on the recruit.";
            }
            if (ok)
            {
                var team = sim.State.Teams.Find(t => t.Sport == a.Student.SportTalent);
                if (team != null) sim.Sports.AddToRoster(team, a.Student, out _);
            }
            return result;
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "decline");
    }

    sealed class StudentAchievementEvent : EventTemplate
    {
        public override string Id => "student_achievement";
        public override int CooldownDays => 45;

        public override float Weight(Simulation sim) =>
            sim.State.Students.Exists(s => s.Academic >= 80f || s.Artistic >= 85f || s.Athletic >= 85f) ? 0.4f : 0f;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = Ev.RandomStudent(sim, x => x.Academic >= 80f || x.Artistic >= 85f || x.Athletic >= 85f);
            if (s == null) return false;
            int day = sim.Day;
            string what;
            ReputationId rep;
            if (s.Academic >= 80f && (s.Academic >= s.Artistic || s.Artistic < 85f) && (s.Academic >= s.Athletic || s.Athletic < 85f))
            {
                what = "placed first in the national mathematics olympiad";
                rep = ReputationId.Academic;
            }
            else if (s.Artistic >= 85f && (s.Artistic >= s.Athletic || s.Athletic < 85f))
            {
                what = "had their artwork chosen for the city gallery";
                rep = ReputationId.StudentLife;
            }
            else
            {
                what = "broke a regional youth record";
                rep = ReputationId.Sports;
            }

            sim.ShockReputation(rep, 2f);
            sim.AddReputationModifier(rep, $"{s.FullName}'s achievement", 2f, 120);
            s.Moodlets.Set("Proud achievement", 8f, day + 30);
            s.ParentMoodlets.Set("Proud of our child", 8f, day + 60);
            s.Remember(day, $"{char.ToUpper(what[0])}{what.Substring(1)}.");
            sim.State.Year.Highlight($"{s.FullName} {what}");
            sim.Progression.AddSchoolXp(40);

            Ev.From(m, SenderType.StudentAffairs, "Student Affairs", $"{s.FullName} {what}!", MessagePriority.Info);
            m.Body = $"Wonderful news: {s.FullName} {what}. Local media are covering the story and {rep} reputation is up.";
            return true;
        }
    }
}
