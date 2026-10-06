using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>Small helpers shared by event templates.</summary>
    internal static class Ev
    {
        public static Student RandomStudent(Simulation sim, Predicate<Student> filter)
        {
            var list = sim.State.Students.FindAll(filter);
            return list.Count == 0 ? null : sim.Rng.Pick(list);
        }

        public static StaffMember RandomStaff(Simulation sim, Predicate<StaffMember> filter)
        {
            var list = sim.State.Staff.FindAll(filter);
            return list.Count == 0 ? null : sim.Rng.Pick(list);
        }

        /// <summary>0.5-1.5: how much families believe the school's reassurances.</summary>
        public static float TrustFactor(Simulation sim) => 0.5f + sim.State.Stat(StatId.Trust) / 100f;

        public static void From(InboxMessage m, SenderType type, string name, string subject, MessagePriority priority = MessagePriority.Normal)
        {
            m.Sender = type;
            m.SenderName = name;
            m.Subject = subject;
            m.Priority = priority;
        }

        public static string Money(float v) => MathUtil.Money(v);
        public static string Pct(float f) => (f * 100f).ToString("0") + "%";

        public static string Gone(string who) => $"{who} has since left the school.";

        public static string Profile(Student s)
        {
            string from = s.International ? $", from {s.HomeCountry}" : "";
            return $"{s.FullName} (grade {s.Grade}{from}) — Academic {s.Academic:0}, Athletic {s.Athletic:0}, Artistic {s.Artistic:0}, " +
                   $"Discipline {s.Discipline:0}, family wealth {s.FamilyWealth:0}. Traits: {string.Join(", ", s.Traits)}.";
        }

        public static string StaffProfile(Simulation sim, StaffMember s)
        {
            return $"{s.Name}, {s.Title} — skill {s.Skill:0}, {s.Experience:0.0} years' experience, morale {s.Morale:0}, " +
                   $"salary {Money(s.Salary)}/mo (market {Money(sim.Staff.MarketValue(s))}). Traits: {string.Join(", ", s.Traits)}.";
        }

        public static float Round10(float v) => (float)Math.Round(v / 10f) * 10f;
        public static float Round100(float v) => (float)Math.Round(v / 100f) * 100f;
    }
}
