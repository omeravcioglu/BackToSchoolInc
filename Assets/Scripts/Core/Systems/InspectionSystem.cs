using System;
using System.Collections.Generic;
using System.Text;

namespace SchoolSim
{
    /// <summary>
    /// Government inspections: announced five days ahead, scored across several categories.
    /// Failing brings fines, reputation damage, a follow-up visit and possibly room closures.
    /// </summary>
    public sealed class InspectionSystem
    {
        readonly Simulation _sim;

        public InspectionSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;
        SchoolIndices Ix => _sim.Indices;

        public const int NoticeDays = 5;
        public const float PassScore = 55f;
        public const float CriticalScore = 30f;

        public void DailyTick()
        {
            int day = S.Day;
            if (S.NextInspectionDay <= 0) S.NextInspectionDay = day + _sim.Rng.Range(150, 240);

            // Inspectors only visit while school is in session.
            if (Calendar.IsSummer(S.NextInspectionDay))
                S.NextInspectionDay = Calendar.StartOfMonth(Calendar.Year(S.NextInspectionDay) + 1, 1) + 10;

            if (!S.InspectionAnnounced && day >= S.NextInspectionDay - NoticeDays)
            {
                S.InspectionAnnounced = true;
                _sim.Events.TriggerDirected("inspection_notice", m => m.Amount = S.NextInspectionDay, ignoreCooldown: true);
            }

            if (day >= S.NextInspectionDay) Run();
        }

        public List<(string name, float score)> Evaluate()
        {
            int homeless = 0;
            foreach (var s in S.Students) if (s.DormRoomId < 0) homeless++;
            float ratio = Ix.StudentsPerTeacher;

            return new List<(string, float)>
            {
                ("Cleanliness", S.Stat(StatId.Cleanliness)),
                ("Safety & discipline", S.Stat(StatId.SafetyDiscipline)),
                ("Fire safety", MathUtil.Clamp100(Ix.AvgRoomCondition * 0.8f + (Ix.SecurityRoomScore > 0 ? 15f : 0f) + Ix.GuardCoverage * 5f)),
                ("Dormitory capacity", MathUtil.Clamp100(100f - Ix.BathroomShortage * 60f - homeless * 15f - (S.Stat(StatId.DormitoryQuality) < 30f ? 20f : 0f))),
                ("Food", S.Stat(StatId.FoodQuality)),
                ("Teacher-to-student ratio", ratio <= 14f ? 100f : MathUtil.Clamp100(100f - (ratio - 14f) * 8f)),
                ("Academic quality", S.Stat(StatId.EducationQuality)),
                ("Student wellbeing", MathUtil.Clamp100(S.Stat(StatId.StudentSatisfaction) * 0.6f + Ix.NurseCoverage * 20f + Ix.CounselorCoverage * 20f)),
            };
        }

        void Run()
        {
            S.InspectionAnnounced = false;
            var results = Evaluate();
            float sum = 0f, min = 100f;
            string worst = null;
            foreach (var (name, score) in results)
            {
                sum += score;
                if (score < min) { min = score; worst = name; }
            }
            float avg = sum / results.Count;
            bool pass = avg >= PassScore && min >= CriticalScore;
            bool excellent = avg >= 75f && min >= 50f;

            var sb = new StringBuilder();
            foreach (var (name, score) in results)
                sb.Append(score < CriticalScore ? "[CRITICAL] " : score < PassScore ? "[WEAK] " : "[OK] ").Append(name).Append(": ").AppendLine(score.ToString("0"));
            sb.AppendLine().Append("Overall score: ").Append(avg.ToString("0")).AppendLine();

            if (excellent)
            {
                _sim.AddReputationModifier(ReputationId.Overall, "Excellent inspection", 5f, 180);
                _sim.AddReputationModifier(ReputationId.Safety, "Excellent inspection", 5f, 180);
                _sim.ChangeTrust(3f);
                _sim.Progression.AddSchoolXp(100);
                sb.AppendLine().Append("Result: EXCELLENT. Inspectors praised the school publicly.");
                S.Year.Highlight("Excellent inspection result");
                S.NextInspectionDay = S.Day + _sim.Rng.Range(240, 360);
            }
            else if (pass)
            {
                _sim.AddReputationModifier(ReputationId.Overall, "Passed inspection", 2f, 120);
                _sim.Progression.AddSchoolXp(50);
                sb.AppendLine().Append("Result: PASSED.");
                S.NextInspectionDay = S.Day + _sim.Rng.Range(180, 300);
            }
            else
            {
                float fine = 3000f + S.Students.Count * 60f;
                S.Finance.Post(S.Day, LedgerCategory.Fines, -fine, "Inspection fine");
                _sim.AddReputationModifier(ReputationId.Overall, "Failed inspection", -6f, 120);
                _sim.AddReputationModifier(ReputationId.Safety, "Failed inspection", -8f, 120);
                _sim.ShockReputation(ReputationId.Overall, -3f);
                _sim.ChangeTrust(-5f);
                _sim.AllParentsMood("School failed inspection", -5f, 60);
                sb.AppendLine().Append($"Result: FAILED. Fine: {MathUtil.Money(fine)}. Follow-up inspection in 60 days.");
                S.Year.Highlight("Failed a government inspection");
                S.NextInspectionDay = S.Day + 60;

                if (min < CriticalScore && (worst == "Fire safety" || worst == "Cleanliness"))
                {
                    var room = WorstRoom();
                    if (room != null)
                    {
                        _sim.Facilities.CloseRoom(room, 30, $"inspectors found critical problems ({worst})");
                        sb.AppendLine().Append($"{room.Name} has been closed for 30 days.");
                    }
                }
                else if (min < CriticalScore && worst == "Dormitory capacity")
                {
                    float extra = 1500f + S.Students.Count * 40f;
                    S.Finance.Post(S.Day, LedgerCategory.Fines, -extra, "Overcrowding penalty");
                    sb.AppendLine().Append($"Additional overcrowding penalty: {MathUtil.Money(extra)}. Fix bathrooms and beds before the follow-up.");
                }
            }

            _sim.Inbox.Notify(SenderType.Inspector, "Education Inspectorate", pass ? "Inspection report: passed" : "Inspection report: FAILED",
                sb.ToString(), pass ? MessagePriority.Normal : MessagePriority.Urgent);
            _sim.Log($"Inspection {(pass ? "passed" : "failed")} with score {avg:0}.", important: true);

            string n = S.SchoolName;
            if (excellent)
                _sim.News.Publish(NewsTone.Good, NewsTopic.Safety, $"Inspectors praise {n}",
                    $"Government inspectors gave {n} an excellent report, scoring it {avg:0} out of 100.", 1.5f, ReputationId.Overall, 90);
            else if (pass)
                _sim.News.Publish(NewsTone.Neutral, NewsTopic.School, $"{n} passes its inspection",
                    $"Inspectors scored {n} {avg:0} out of 100. Room to improve, but a pass.");
            else
                _sim.News.Publish(NewsTone.Bad, NewsTopic.Safety, $"{n} fails government inspection",
                    $"Inspectors found serious problems at {n}, worst of all {worst?.ToLowerInvariant()}, and fined the school.", -2f, ReputationId.Overall, 90);
        }

        /// <summary>
        /// Worst-kept room the inspectors can close. Essentials stay open, and a dorm is only
        /// closed if its residents can be rehoused elsewhere.
        /// </summary>
        Room WorstRoom()
        {
            int freeBeds = _sim.Facilities.FreeBeds();
            Room worst = null;
            foreach (var r in S.Rooms)
            {
                if (!r.IsOperational(S.Day) || r.Def.Decor) continue;
                if (r.Type == RoomType.Cafeteria || r.Type == RoomType.Classroom || r.Type == RoomType.Bathroom) continue;
                if (r.Type == RoomType.DormRoom && Ix.ResidentsOf(r.Id).Count > freeBeds) continue;
                float score = r.Condition + r.Cleanliness;
                if (worst == null || score < worst.Condition + worst.Cleanliness) worst = r;
            }
            return worst;
        }
    }
}
