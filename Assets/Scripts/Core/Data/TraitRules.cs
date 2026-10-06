using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>Trait descriptions, mutually exclusive pairs and roommate compatibility.</summary>
    public static class TraitRules
    {
        static readonly (StudentTrait, StudentTrait)[] ExclusiveStudent =
        {
            (StudentTrait.Extrovert, StudentTrait.Introvert),
            (StudentTrait.Messy, StudentTrait.CleanFreak),
            (StudentTrait.Lazy, StudentTrait.Hardworking),
            (StudentTrait.Troublemaker, StudentTrait.Bookworm),
        };

        static readonly (StaffTrait, StaffTrait)[] ExclusiveStaff =
        {
            (StaffTrait.Lazy, StaffTrait.ExcellentTeacher),
            (StaffTrait.Funny, StaffTrait.Difficult),
            (StaffTrait.Popular, StaffTrait.Difficult),
            (StaffTrait.Loyal, StaffTrait.Ambitious),
            (StaffTrait.Lazy, StaffTrait.Motivational),
        };

        public static bool Conflicts(StudentTrait a, StudentTrait b)
        {
            if (a == b) return true;
            foreach (var (x, y) in ExclusiveStudent)
                if ((a == x && b == y) || (a == y && b == x)) return true;
            return false;
        }

        public static bool Conflicts(StaffTrait a, StaffTrait b)
        {
            if (a == b) return true;
            foreach (var (x, y) in ExclusiveStaff)
                if ((a == x && b == y) || (a == y && b == x)) return true;
            return false;
        }

        public static string Describe(StudentTrait t) => t switch
        {
            StudentTrait.Extrovert => "Thrives around people. Wants lounges and busy dorms.",
            StudentTrait.Introvert => "Prefers small rooms and quiet spaces.",
            StudentTrait.Messy => "Leaves rooms dirty. Clashes with clean freaks.",
            StudentTrait.CleanFreak => "Unhappy in dirty rooms. Clashes with messy roommates.",
            StudentTrait.Competitive => "Forms rivalries; rivals push each other's grades.",
            StudentTrait.Lazy => "Learns slower and resists stress.",
            StudentTrait.Hardworking => "Learns faster, but gets more stressed.",
            StudentTrait.NightOwl => "Hates strict curfews and annoys early-sleeping roommates.",
            StudentTrait.Athletic => "Wants sports facilities and teams.",
            StudentTrait.Artistic => "Wants arts clubs.",
            StudentTrait.Troublemaker => "Breaks rules far more often, especially with troublemaker friends.",
            StudentTrait.Bookworm => "Loves the library. Studies well.",
            StudentTrait.Leader => "Boosts clubs and student government.",
            StudentTrait.Anxious => "Stressed easily, especially in exam season.",
            _ => "",
        };

        public static string Describe(StaffTrait t) => t switch
        {
            StaffTrait.Strict => "Raises discipline and grades, lowers student happiness.",
            StaffTrait.Funny => "Students enjoy their classes.",
            StaffTrait.Popular => "Loved by students; firing them upsets students.",
            StaffTrait.Lazy => "Less effective, but rarely burns out.",
            StaffTrait.ExcellentTeacher => "Exceptionally effective teacher.",
            StaffTrait.Motivational => "Students' abilities grow faster.",
            StaffTrait.Difficult => "Drags down colleagues' morale.",
            StaffTrait.BurnoutProne => "Loses energy quickly under heavy workloads.",
            StaffTrait.Loyal => "Hard to poach and rarely quits.",
            StaffTrait.Ambitious => "Asks for raises often and listens to rival offers.",
            _ => "",
        };

        /// <summary>
        /// Roughly -4..+4. Positive values pull a pair's relationship up over time;
        /// negative values push it toward rivalry or hostility.
        /// </summary>
        public static float Compatibility(Student a, Student b, bool roommates)
        {
            float c = 0f;
            float close = roommates ? 1f : 0.35f;

            if (Pair(a, b, StudentTrait.Messy, StudentTrait.CleanFreak)) c -= 3f * close;
            if (roommates && a.Has(StudentTrait.NightOwl) != b.Has(StudentTrait.NightOwl)) c -= 1f;
            if (Both(a, b, StudentTrait.Extrovert)) c += 1f;
            if (Both(a, b, StudentTrait.Introvert)) c += 0.5f;
            if (roommates && Pair(a, b, StudentTrait.Extrovert, StudentTrait.Introvert)) c -= 0.5f;
            if (Both(a, b, StudentTrait.Competitive)) c -= 1f;
            if (Both(a, b, StudentTrait.Troublemaker)) c += 2f;
            if (Pair(a, b, StudentTrait.Troublemaker, StudentTrait.Hardworking)) c -= 1f;
            if (Pair(a, b, StudentTrait.Troublemaker, StudentTrait.Anxious)) c -= 1f;
            if (Pair(a, b, StudentTrait.Hardworking, StudentTrait.Lazy)) c -= 1f * close;
            if (Both(a, b, StudentTrait.Hardworking)) c += 0.5f;
            if (Both(a, b, StudentTrait.Artistic)) c += 1f;
            if (Both(a, b, StudentTrait.Athletic)) c += 1f;
            if (Both(a, b, StudentTrait.Bookworm)) c += 1f;
            if (Both(a, b, StudentTrait.Leader)) c -= 0.5f;

            c += (a.Social + b.Social - 100f) / 100f;
            return c;
        }

        static bool Both(Student a, Student b, StudentTrait t) => a.Has(t) && b.Has(t);

        static bool Pair(Student a, Student b, StudentTrait x, StudentTrait y) =>
            (a.Has(x) && b.Has(y)) || (a.Has(y) && b.Has(x));

        public static List<StudentTrait> RollStudentTraits(SimRandom rng, int count)
        {
            var all = (StudentTrait[])System.Enum.GetValues(typeof(StudentTrait));
            var result = new List<StudentTrait>();
            int guard = 0;
            while (result.Count < count && guard++ < 50)
            {
                var t = all[rng.Range(0, all.Length)];
                bool ok = true;
                foreach (var existing in result)
                    if (Conflicts(existing, t)) { ok = false; break; }
                if (ok) result.Add(t);
            }
            return result;
        }

        public static List<StaffTrait> RollStaffTraits(SimRandom rng, int count)
        {
            var all = (StaffTrait[])System.Enum.GetValues(typeof(StaffTrait));
            var result = new List<StaffTrait>();
            int guard = 0;
            while (result.Count < count && guard++ < 50)
            {
                var t = all[rng.Range(0, all.Length)];
                bool ok = true;
                foreach (var existing in result)
                    if (Conflicts(existing, t)) { ok = false; break; }
                if (ok) result.Add(t);
            }
            return result;
        }
    }
}
