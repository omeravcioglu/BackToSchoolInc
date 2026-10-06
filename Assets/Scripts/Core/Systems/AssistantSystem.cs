using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// The assistant at work: looks around the school every few days and brings what's wrong to the
    /// inbox with ready-made fixes, gives an opinion on every decision, and keeps their
    /// personality's perk and quirk in effect.
    /// </summary>
    public sealed class AssistantSystem
    {
        readonly Simulation _sim;

        public AssistantSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;

        public Assistant Current => S.Assistant;

        /// <summary>Problems the assistant looks for, most urgent first.</summary>
        public static readonly ProblemKind[] Priority =
        {
            ProblemKind.Cash, ProblemKind.Beds, ProblemKind.Bathrooms, ProblemKind.Seats, ProblemKind.Teachers,
            ProblemKind.Safety, ProblemKind.Food, ProblemKind.Dirty, ProblemKind.Repairs, ProblemKind.Unhappy,
            ProblemKind.StaffMorale, ProblemKind.Upgrade,
        };

        public const int MaxOpenProblems = 2;
        public const int ScanEveryDays = 3;

        public static string TemplateId(ProblemKind k) => "assist_" + k.ToString().ToLowerInvariant();

        public static bool IsProblem(InboxMessage m) => m.TemplateId != null && m.TemplateId.StartsWith("assist_", StringComparison.Ordinal);

        public void Hire(Assistant a)
        {
            a.HiredDay = S.Day;
            S.Assistant = a;
            ApplyMoods();
            _sim.Log($"{a.Name} is your new assistant ({AssistantRules.Word(a.Style)}).");
        }

        public void Monthly()
        {
            if (Current != null) ApplyMoods();
        }

        /// <summary>The personality's effect on people: refreshed monthly so it outlasts each moodlet.</summary>
        void ApplyMoods()
        {
            var a = Current;
            const int days = 40;
            switch (a.Style)
            {
                case AssistantStyle.Cheerful: _sim.AllStaffMood($"{a.FirstName}'s sunny mood", 4f, days); break;
                case AssistantStyle.Strict: _sim.AllStudentsMood($"{a.FirstName} is strict", -2f, days); break;
                case AssistantStyle.Thrifty: _sim.AllStaffMood($"{a.FirstName} counts every paperclip", -2f, days); break;
                case AssistantStyle.Charming: _sim.AllParentsMood($"{a.FirstName} is lovely", 3f, days); break;
                case AssistantStyle.Handy: _sim.AllParentsMood($"{a.FirstName} is a bit blunt", -1f, days); break;
            }
        }

        // ------------------------------------------------------------------------------
        // Problems
        // ------------------------------------------------------------------------------

        public void DailyTick()
        {
            var a = Current;
            if (a == null || S.GameOver) return;
            if (S.Tutorial.Active && S.Day < Balance.TutorialQuietDays) return;
            if (S.Day % ScanEveryDays != 0) return;
            if (OpenProblems() >= MaxOpenProblems) return;

            foreach (var kind in Priority)
            {
                if (!AssistantRules.Watches(a.Style, kind) || !Spots(kind)) continue;
                // Each problem has its own cooldown, so the next one gets a turn.
                if (_sim.Events.TriggerDirected(TemplateId(kind), null)) break;
            }
        }

        public int OpenProblems()
        {
            int n = 0;
            foreach (var m in S.Inbox) if (m.NeedsDecision && IsProblem(m)) n++;
            return n;
        }

        /// <summary>Margin by which this assistant notices trouble early (skill) or late.</summary>
        float Keen(ProblemKind kind) => (Current.Skill - 3) * 2.5f + AssistantRules.Keenness(Current.Style, kind);

        /// <summary>Would the current assistant raise this problem today?</summary>
        public bool Spots(ProblemKind kind)
        {
            if (Current == null) return false;
            var ix = _sim.Indices;
            float keen = Keen(kind);
            switch (kind)
            {
                case ProblemKind.Cash:
                {
                    float cash = S.Finance.Cash, net = _sim.Economy.Project().Net;
                    float months = (Current.Style == AssistantStyle.Cheerful ? 1f : 2.5f) + keen * 0.15f;
                    return cash < 0f || (net < 0f && cash < -net * months);
                }
                case ProblemKind.Beds:
                    return ix.Beds > 0 && ix.FreeBeds <= (keen >= 2.5f ? 1 : 0);
                case ProblemKind.Bathrooms:
                    return ix.BathroomShortage > 0.15f - keen * 0.01f;
                case ProblemKind.Seats:
                    return ix.Students > 0 && ix.ClassroomSeats < ix.Students + (keen >= 2.5f ? 2 : 0);
                case ProblemKind.Teachers:
                    return ix.Teachers > 0 && ix.StudentsPerTeacher > 14.5f - keen * 0.3f;
                case ProblemKind.Safety:
                    return S.Students.Count > 0 && (S.Stat(StatId.SafetyDiscipline) < 40f + keen || S.IncidentPressure > 12f - keen);
                case ProblemKind.Food:
                    return ix.HasCafeteria && S.Policies.Food < FoodTier.Premium
                           && S.Stat(StatId.FoodQuality) < Balance.FoodExpectation(S.Prestige) - 6f + keen;
                case ProblemKind.Dirty:
                    return ix.AvgCleanliness < 50f + keen;
                case ProblemKind.Repairs:
                {
                    var worst = WorstRoom();
                    return worst != null && worst.Condition < 40f + keen;
                }
                case ProblemKind.Unhappy:
                    return S.Students.Count > 0 && S.Stat(StatId.StudentSatisfaction) < 45f + keen;
                case ProblemKind.StaffMorale:
                    return S.Staff.Count > 0 && S.Stat(StatId.StaffMorale) < 45f + keen;
                case ProblemKind.Upgrade:
                    return SuggestUpgrade() != null;
                default:
                    return false;
            }
        }

        public Room WorstRoom()
        {
            Room worst = null;
            foreach (var r in S.Rooms)
                if (r.IsBuilt(S.Day) && !r.IsUpgrading && (worst == null || r.Condition < worst.Condition)) worst = r;
            return worst;
        }

        /// <summary>A worthwhile upgrade the school can comfortably afford right now, if any.</summary>
        public Room SuggestUpgrade()
        {
            float cash = S.Finance.Cash;
            var ix = _sim.Indices;
            Room best = null;
            float bestScore = 0f;
            foreach (var r in S.Rooms)
            {
                if (r.Type != RoomType.Classroom && r.Type != RoomType.Cafeteria && r.Type != RoomType.DormRoom && r.Type != RoomType.Library)
                    continue;
                if (!_sim.Facilities.CanUpgrade(r, out _)) continue;
                float cost = _sim.Facilities.UpgradeCost(r);
                if (cost > cash * 0.3f || cash - cost < 40000f) continue;

                float score = 10f - r.Level * 2f;
                if (r.Type == RoomType.Cafeteria && ix.MealCapacity < S.Students.Count * 1.1f) score += 6f;
                if (r.Type == RoomType.Classroom && ix.ClassroomSeats < S.Students.Count + 4) score += 4f;
                if (r.Type == RoomType.DormRoom) score -= 3f;   // better rooms, but fewer beds
                if (score > bestScore)
                {
                    best = r;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>The best candidate on the job market for a role, or null.</summary>
        public Candidate BestCandidate(StaffRole role)
        {
            Candidate best = null;
            foreach (var c in S.Candidates)
            {
                if (c.Staff.Role != role || !_sim.Staff.CanHire(c, out _)) continue;
                if (best == null || c.Staff.Skill > best.Staff.Skill) best = c;
            }
            return best;
        }

        // ------------------------------------------------------------------------------
        // Advice
        // ------------------------------------------------------------------------------

        static readonly string[] Common =
        {
            "compensate", "move", "meeting", "reassure", "approve", "accept", "counseling", "detention", "cleanup",
            "admit", "aid", "partial", "counter", "renew3", "renew1", "emergency", "tech", "deepclean", "quarantine",
            "apply", "scholarship", "fans", "statement", "action", "medical", "repairs", "repair", "fail", "build",
            "hire", "upgrade", "menu", "lunch", "party", "guard", "food",
        };

        static string[] Taste(AssistantStyle s) => s switch
        {
            AssistantStyle.Cheerful => new[] { "party", "enjoy", "campaign", "fans", "speaker", "approve", "lunch", "reassure" },
            AssistantStyle.Strict => new[] { "detention", "discipline", "rules", "assembly", "guard", "strict", "suspend", "fail", "save" },
            AssistantStyle.Thrifty => new[] { "negotiate", "counter", "partial", "save", "apply", "cheap", "food", "movie", "lunch" },
            AssistantStyle.Charming => new[] { "meeting", "statement", "apologize", "reassure", "compensate", "speaker", "campaign", "action", "party" },
            AssistantStyle.Bookish => new[] { "classrooms", "tutoring", "counseling", "scholarship", "apply", "retake", "hire", "build" },
            AssistantStyle.Handy => new[] { "repairs", "repair_all", "repair_worst", "repair", "replace", "emergency", "tech", "deepclean", "build", "upgrade", "classrooms" },
            _ => Array.Empty<string>(),
        };

        static bool Dismissive(string id) => id == "ignore" || id == "later" || id == "coverup" || id == "promise" || id == "nothing";

        /// <summary>Fills in the assistant's recommendation for a message that needs a decision.</summary>
        public void Advise(InboxMessage m)
        {
            var a = Current;
            if (a == null || m.Options.Count == 0) return;
            var usable = m.Options.FindAll(o => _sim.Inbox.WhyNot(m, o) == null);
            if (usable.Count == 0) return;

            MessageOption pick = m.AdvisorOptionId != null ? usable.Find(o => o.Id == m.AdvisorOptionId) : null;
            if (pick == null)
            {
                string auto = _sim.Events.Get(m.TemplateId)?.AutoDecide(_sim, m, S.AdminPolicies);
                if (auto != null) pick = usable.Find(o => o.Id == auto);
            }
            pick ??= ByTaste(a, usable);

            // Less experienced assistants sometimes read the situation wrong.
            if (usable.Count > 1 && _sim.Rng.Chance((5 - a.Skill) * 0.07f))
            {
                var others = usable.FindAll(o => o != pick && !Dismissive(o.Id));
                if (others.Count > 0) pick = _sim.Rng.Pick(others);
            }

            m.AdvisorOptionId = pick.Id;
            m.AdvisorNote = AssistantVoice.Advice(a.Style, pick.Label);
        }

        MessageOption ByTaste(Assistant a, List<MessageOption> usable)
        {
            bool Affordable(MessageOption o) =>
                o.Cost <= 0f || S.Finance.Cash > o.Cost * (a.Style == AssistantStyle.Thrifty ? 5f : 3f) + 5000f;

            foreach (var id in Taste(a.Style))
            {
                var o = usable.Find(x => x.Id == id);
                if (o != null && Affordable(o)) return o;
            }
            foreach (var id in Common)
            {
                var o = usable.Find(x => x.Id == id);
                if (o != null && Affordable(o)) return o;
            }
            var safe = usable.Find(o => !Dismissive(o.Id) && Affordable(o));
            return safe ?? usable[0];
        }
    }
}
