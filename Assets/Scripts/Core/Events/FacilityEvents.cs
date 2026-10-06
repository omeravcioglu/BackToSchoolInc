using System;

namespace SchoolSim
{
    sealed class PlumbingFailureEvent : EventTemplate
    {
        public override string Id => "plumbing_failure";
        public override AdminRole Handler => AdminRole.DormDirector;
        public override int CooldownDays => 20;

        static Room Worst(Simulation sim)
        {
            Room worst = null;
            foreach (var r in sim.State.Rooms)
            {
                if (!r.IsOperational(sim.Day) || (r.Type != RoomType.Bathroom && r.Type != RoomType.DormRoom)) continue;
                if (worst == null || r.Condition < worst.Condition) worst = r;
            }
            return worst;
        }

        public override float Weight(Simulation sim)
        {
            var r = Worst(sim);
            if (r == null) return 0f;
            return r.Condition < 60f ? 1f + (60f - r.Condition) / 20f : 0.15f;
        }

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var room = sim.State.FindRoom(m.RoomId) ?? Worst(sim);
            if (room == null) return false;
            m.RoomId = room.Id;
            m.Amount = 1500f + room.Level * 500f;
            Ev.From(m, SenderType.Facilities, "Facilities", $"Burst pipe in {room.Name}", MessagePriority.Important);
            m.Body = $"A pipe burst in {room.Name} (condition {room.Condition:0}). There's water on the floor and the residents are furious.";
            m.AddOption("emergency", $"Call an emergency plumber ({Ev.Money(m.Amount)})", "Fixed properly today.", cost: m.Amount);
            m.AddOption("cheap", "Patch it up cheaply ($400)", "Works for now.", "Cheap patches tend to fail again.", 400f);
            m.AddOption("wait", "Wait for regular maintenance", "Room closed for 5 days; residents unhappy.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var room = sim.State.FindRoom(m.RoomId);
            if (room == null) return "The room no longer exists.";
            int day = sim.Day;
            switch (option)
            {
                case "emergency":
                    sim.State.Finance.Post(day, LedgerCategory.Upkeep, -m.Amount, "Emergency plumber");
                    room.Condition = Math.Max(room.Condition, 80f);
                    room.Cleanliness = MathUtil.Clamp100(room.Cleanliness + 10f);
                    return "Fixed properly.";
                case "cheap":
                    sim.State.Finance.Post(day, LedgerCategory.Upkeep, -400f, "Plumbing patch");
                    room.Condition = MathUtil.Clamp100(room.Condition + 15f);
                    if (sim.Rng.Chance(0.4f))
                        sim.Events.Schedule(new ScheduledEvent { Day = day + sim.Rng.Range(20, 40), TemplateId = Id, OtherId = room.Id });
                    return "Patched. Fingers crossed.";
                default:
                    room.Cleanliness = MathUtil.Clamp100(room.Cleanliness - 20f);
                    foreach (var s in sim.Indices.ResidentsOf(room.Id)) s.Moodlets.Set("Flooded room", -6f, day + 10);
                    if (room.Type == RoomType.Bathroom) sim.AllStudentsMood("Bathroom out of order", -2f, 5);
                    sim.Facilities.CloseRoom(room, 5, "plumbing failure");
                    return "The room is closed while maintenance gets to it.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "wait");

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) =>
            m.Amount <= p.AutoCompensateComplaintsBelow * 8f ? "emergency" : "cheap";

        public override void OnScheduled(Simulation sim, ScheduledEvent e)
        {
            sim.Events.TriggerDirected(Id, msg => msg.RoomId = e.OtherId, ignoreCooldown: true);
        }
    }

    sealed class WifiOutageEvent : EventTemplate
    {
        public override string Id => "wifi_outage";
        public override int CooldownDays => 25;

        public override float Weight(Simulation sim) => sim.State.Policies.InternetLevel switch
        {
            1 => 0.8f,
            2 => 0.3f,
            _ => 0.08f,
        };

        public override bool Build(Simulation sim, InboxMessage m)
        {
            int level = sim.State.Policies.InternetLevel;
            Ev.From(m, SenderType.Facilities, "IT support", "Wi-Fi is down across campus", MessagePriority.Important);
            m.Body = "The whole network went down this morning. Students can't submit homework and teachers can't use their slides.";
            m.AddOption("tech", "Pay an emergency technician ($800)", "Back online today.", cost: 800f);
            m.AddOption("wait", "Wait for the provider", "Students and teachers frustrated for a few days.");
            if (level < 3)
            {
                float cost = Balance.InternetUpgradeCost(level + 1);
                m.AddOption("upgrade", $"Upgrade the network now ({Ev.Money(cost)})", "Fixes it and makes future outages rarer. Higher monthly cost.", cost: cost);
            }
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var st = sim.State;
            switch (option)
            {
                case "tech":
                    st.Finance.Post(sim.Day, LedgerCategory.Upkeep, -800f, "Emergency IT technician");
                    return "Back online by lunch.";
                case "upgrade":
                    st.Finance.Post(sim.Day, LedgerCategory.Construction, -Balance.InternetUpgradeCost(st.Policies.InternetLevel + 1), "Network upgrade");
                    st.Policies.InternetLevel++;
                    return $"Internet upgraded to level {st.Policies.InternetLevel}.";
                default:
                    sim.AllStudentsMood("Wi-Fi outage", -5f, 4);
                    sim.AllStaffMood("Wi-Fi outage", -3f, 4);
                    return "Everyone is offline for a few days.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "wait");
    }

    sealed class BrokenEquipmentEvent : EventTemplate
    {
        public override string Id => "broken_equipment";
        public override int CooldownDays => 20;

        static Room Worst(Simulation sim)
        {
            Room worst = null;
            foreach (var r in sim.State.Rooms)
            {
                if (!r.IsOperational(sim.Day)) continue;
                var c = r.Def.Category;
                if (c != RoomCategory.Academic && c != RoomCategory.Sports && c != RoomCategory.Dining && c != RoomCategory.StudentLife) continue;
                if (worst == null || r.Condition < worst.Condition) worst = r;
            }
            return worst;
        }

        public override float Weight(Simulation sim)
        {
            var r = Worst(sim);
            return r == null ? 0f : r.Condition < 50f ? 1f : 0.2f;
        }

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var room = Worst(sim);
            if (room == null) return false;
            m.RoomId = room.Id;
            m.Amount = Ev.Round100(1000f * room.Level * Math.Max(1f, room.Def.Area / 20f));
            string what = room.Type switch
            {
                RoomType.Cafeteria => "The main oven",
                RoomType.Classroom => "The projector and half the desks",
                RoomType.ScienceLab => "The fume hood",
                RoomType.ComputerLab => "A row of computers",
                RoomType.Gym => "The weights machines",
                _ => "Key equipment",
            };
            Ev.From(m, SenderType.Facilities, "Facilities", $"Broken equipment in {room.Name}");
            m.Body = $"{what} in {room.Name} broke down (condition {room.Condition:0}). The room is struggling to function properly.";
            m.AddOption("replace", $"Replace it ({Ev.Money(m.Amount)})", "Room condition restored.", cost: m.Amount);
            m.AddOption("repair", $"Repair it cheaply ({Ev.Money(Ev.Round100(m.Amount * 0.3f))})", "Condition improves a little.", cost: Ev.Round100(m.Amount * 0.3f));
            m.AddOption("ignore", "Make do without it", "Room quality falls further.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var room = sim.State.FindRoom(m.RoomId);
            if (room == null) return "";
            switch (option)
            {
                case "replace":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Upkeep, -m.Amount, "Replaced equipment in " + room.Name);
                    room.Condition = Math.Max(room.Condition, 95f);
                    return "Good as new.";
                case "repair":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Upkeep, -Ev.Round100(m.Amount * 0.3f), "Repaired equipment in " + room.Name);
                    room.Condition = MathUtil.Clamp100(room.Condition + 15f);
                    return "Held together with tape.";
                default:
                    room.Condition = MathUtil.Clamp100(room.Condition - 15f);
                    return $"{room.Name} is in worse shape.";
            }
        }
    }

    sealed class IllnessOutbreakEvent : EventTemplate
    {
        public override string Id => "illness_outbreak";
        public override AdminRole Handler => AdminRole.StudentAffairsDirector;
        public override int CooldownDays => 50;

        public override float Weight(Simulation sim)
        {
            float w = 0.25f + (sim.State.Stat(StatId.Cleanliness) < 60f ? 0.5f : 0f) - sim.Indices.NurseCoverage * 0.15f;
            int month = Calendar.Month(sim.Day);
            if (month >= 3 && month <= 6) w *= 1.5f;   // winter
            return Math.Max(0.05f, w);
        }

        public override bool Build(Simulation sim, InboxMessage m)
        {
            int n = sim.State.Students.Count;
            if (n < 4) return false;
            float rate = 0.1f + (1f - sim.Indices.NurseCoverage) * 0.15f + (1f - sim.State.Stat(StatId.Cleanliness) / 100f) * 0.1f;
            m.Amount = Math.Max(2, (int)(n * rate));
            m.Amount2 = 500f + 150f * m.Amount;
            Ev.From(m, SenderType.StudentAffairs, sim.CountStaff(StaffRole.Nurse) > 0 ? "School nurse" : "Student Affairs", "Flu outbreak", MessagePriority.Important);
            m.Body = $"{m.Amount:0} students have come down with the flu" +
                     (sim.Indices.NurseCoverage < 1f ? ", and without enough nursing staff we're struggling to cope." : ".") +
                     " It's spreading through the dorms.";
            m.AddOption("quarantine", "Quarantine the sick in one wing", "Contains the spread. Sick students miss classes.");
            m.AddOption("doctors", $"Bring in doctors ({Ev.Money(m.Amount2)})", "Quick recovery; parents reassured.", cost: m.Amount2);
            m.AddOption("carry", "Carry on as normal", "It spreads further; parents worried.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            int count = (int)m.Amount;
            int days;
            switch (option)
            {
                case "doctors":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Other, -m.Amount2, "Doctors for flu outbreak");
                    days = 3;
                    sim.AllParentsMood("Illness handled well", 3f, 30);
                    break;
                case "quarantine":
                    days = 5;
                    break;
                default:
                    count = (int)(count * 1.6f);
                    days = 7;
                    sim.AllParentsMood("Illness spreading at school", -5f, 45);
                    sim.AddReputationModifier(ReputationId.Safety, "Flu outbreak", -3f, 60);
                    break;
            }

            var list = new System.Collections.Generic.List<Student>(sim.State.Students);
            sim.Rng.Shuffle(list);
            for (int i = 0; i < Math.Min(count, list.Count); i++)
            {
                list[i].SickUntilDay = sim.Day + days;
                list[i].Health = MathUtil.Clamp100(list[i].Health - 30f);
            }
            return $"{Math.Min(count, list.Count)} students sick for about {days} days.";
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "carry");

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) => "quarantine";
    }
}
