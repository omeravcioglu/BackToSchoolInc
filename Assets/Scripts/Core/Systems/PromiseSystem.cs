using System;

namespace SchoolSim
{
    /// <summary>
    /// Tracks commitments made in inbox replies. Kept promises build trust; broken ones damage it
    /// and make later complaints harder to settle.
    /// </summary>
    public sealed class PromiseSystem
    {
        readonly Simulation _sim;

        public PromiseSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;

        public Promise Make(PromiseKind kind, string text, int days, float target, int targetId = -1,
            SenderType to = SenderType.Parent, int studentId = -1, float reward = 4f, float penalty = 10f)
        {
            var p = new Promise
            {
                Id = S.NewId(),
                Kind = kind,
                Text = text,
                MadeDay = S.Day,
                DueDay = S.Day + days,
                Target = target,
                TargetId = targetId,
                MadeTo = to,
                StudentId = studentId,
                TrustReward = reward,
                TrustPenalty = penalty,
            };
            S.Promises.Add(p);
            _sim.Log($"Promise made: {text} (due {Calendar.Format(p.DueDay)}).");
            _sim.Progression.Unveil(Reveal.Promises);
            return p;
        }

        public void DailyTick()
        {
            foreach (var p in S.Promises)
            {
                if (p.Resolved) continue;
                if (IsMet(p)) Keep(p);
                else if (S.Day >= p.DueDay) Break(p);
            }
            if (S.Promises.Count > 100) S.Promises.RemoveAll(p => p.Resolved && p.DueDay < S.Day - 360);
        }

        public bool IsMet(Promise p)
        {
            switch (p.Kind)
            {
                case PromiseKind.FoodQualityAtLeast: return S.Stat(StatId.FoodQuality) >= p.Target;
                case PromiseKind.FoodTierAtLeast: return (int)S.Policies.Food >= (int)p.Target;
                case PromiseKind.DormQualityAtLeast: return S.Stat(StatId.DormitoryQuality) >= p.Target;
                case PromiseKind.SafetyAtLeast: return S.Stat(StatId.SafetyDiscipline) >= p.Target;
                case PromiseKind.RoomTypeExists: return _sim.HasOperational((RoomType)p.TargetId);
                case PromiseKind.InternetLevelAtLeast: return S.Policies.InternetLevel >= p.Target;
                case PromiseKind.StaffRoleCountAtLeast: return _sim.CountStaff((StaffRole)p.TargetId, activeOnly: false) >= p.Target;
                case PromiseKind.ClubExists:
                    foreach (var c in S.Clubs) if ((int)c.Type == p.TargetId) return true;
                    return false;
                case PromiseKind.SalaryReview:
                {
                    var staff = S.FindStaff(p.TargetId);
                    return staff == null || staff.LastRaiseDay >= p.MadeDay;
                }
                default: return false;
            }
        }

        void Keep(Promise p)
        {
            p.Resolved = true;
            p.Kept = true;
            _sim.ChangeTrust(p.TrustReward);
            switch (p.MadeTo)
            {
                case SenderType.Parent:
                    _sim.AllParentsMood("The school keeps its promises", 3f, 30);
                    S.FindStudent(p.StudentId)?.ParentMoodlets.Set("Promise kept", 8f, S.Day + 60);
                    break;
                case SenderType.Student:
                    _sim.AllStudentsMood("Our request was granted", 3f, 20);
                    break;
                case SenderType.Teacher:
                case SenderType.Staff:
                    S.FindStaff(p.TargetId)?.Moodlets.Set("Director kept their word", 6f, S.Day + 60);
                    break;
            }
            _sim.Progression.AddDirectorXp(8);
            _sim.Log($"Promise kept: {p.Text}");
        }

        void Break(Promise p)
        {
            p.Resolved = true;
            p.Kept = false;
            _sim.ChangeTrust(-p.TrustPenalty);

            string who;
            switch (p.MadeTo)
            {
                case SenderType.Parent:
                    who = "Parents";
                    _sim.AllParentsMood("Broken promise", -6f, 60);
                    S.FindStudent(p.StudentId)?.ParentMoodlets.Set("You broke your promise to us", -15f, S.Day + 90);
                    break;
                case SenderType.Student:
                    who = "Students";
                    _sim.AllStudentsMood("Broken promise", -5f, 30);
                    break;
                case SenderType.Teacher:
                case SenderType.Staff:
                {
                    who = "Staff";
                    var staff = S.FindStaff(p.TargetId);
                    if (staff != null)
                    {
                        staff.Moodlets.Set("Director broke their word", -15f, S.Day + 90);
                        staff.Loyalty = MathUtil.Clamp100(staff.Loyalty - 15f);
                    }
                    break;
                }
                default:
                    who = "People";
                    break;
            }

            S.Year.Highlight("Broken promise: " + p.Text);
            if (p.MadeTo == SenderType.Parent && _sim.Rng.Chance(0.5f))
                _sim.News.Publish(NewsTone.Bad, NewsTopic.School, $"Parents fume over broken promise at {S.SchoolName}",
                    $"\"They promised: {p.Text.ToLowerInvariant()}. Nothing happened,\" one parent wrote. Others agreed.",
                    -1.5f, ReputationId.Overall, 45, NewsSystem.Forum);
            _sim.Inbox.Notify(p.MadeTo, who, "Broken promise",
                $"You promised: \"{p.Text}\" and did not deliver by {Calendar.Format(p.DueDay)}. " +
                $"Trust in the school fell by {p.TrustPenalty:0}. Future complaints will be harder to settle.",
                MessagePriority.Important);
            _sim.Log($"Broken promise: {p.Text}", important: true);
        }

        public string Describe(Promise p)
        {
            int daysLeft = p.DueDay - S.Day;
            string status = p.Resolved ? (p.Kept ? "Kept" : "Broken") : IsMet(p) ? "On track" : $"{Math.Max(0, daysLeft)} days left";
            return $"{p.Text} [{status}]";
        }
    }
}
