using System.Linq;
using NUnit.Framework;

namespace SchoolSim.Tests
{
    public class SimulationTests
    {
        static Simulation NewSim(int seed = 42) => NewGame.Create("Test School", seed);

        /// <summary>Answers every open message with its first available option.</summary>
        static void AnswerAll(Simulation sim)
        {
            foreach (var m in sim.State.Inbox.ToArray())
            {
                if (!m.NeedsDecision) continue;
                var option = m.Options.FirstOrDefault(o => sim.Inbox.WhyNot(m, o) == null);
                if (option != null) sim.Reply(m.Id, option.Id);
            }
        }

        [Test]
        public void NewGame_StartsSmallAndStruggling()
        {
            var sim = NewSim();
            var s = sim.State;
            Assert.AreEqual(Balance.StartingStudents, s.Students.Count);
            Assert.AreEqual(12, sim.Indices.Beds);
            Assert.AreEqual(4, s.Staff.Count);
            Assert.IsTrue(s.Students.All(x => x.DormRoomId >= 0), "every starting student has a bed");
            Assert.AreEqual(8, s.Rivals.Count);
            Assert.Greater(s.NationalRank, 0);
            Assert.IsTrue(s.Inbox.Any(m => m.Subject.StartsWith("Welcome")));
        }

        [Test]
        public void Simulation_RunsTwoYearsWithoutErrors()
        {
            var sim = NewSim(7);
            for (int i = 0; i < Calendar.DaysPerYear * 2 && !sim.State.GameOver; i++)
            {
                sim.AdvanceDay();
                AnswerAll(sim);
            }
            Assert.GreaterOrEqual(sim.State.Reports.Count, 1, "at least one annual report");
            Assert.Greater(sim.State.Alumni.Count, 0, "seniors graduated");
        }

        [Test]
        public void SameSeedAndActions_AreDeterministic()
        {
            var a = NewSim(99);
            var b = NewSim(99);
            for (int i = 0; i < 200; i++)
            {
                a.AdvanceDay(); AnswerAll(a);
                b.AdvanceDay(); AnswerAll(b);
            }
            Assert.AreEqual(a.State.Finance.Cash, b.State.Finance.Cash, 0.01f);
            Assert.AreEqual(a.State.Students.Count, b.State.Students.Count);
        }

        [Test]
        public void RelationshipType_HasHysteresis()
        {
            var r = new Relationship { A = 1, B = 2 };
            r.Score = -61f;
            Assert.AreEqual(RelationshipType.Enemy, r.Type);
            r.Score = -55f;
            Assert.AreEqual(RelationshipType.Enemy, r.Type, "stays an enemy just above the threshold");
            r.Score = -50f;
            Assert.AreEqual(RelationshipType.Rival, r.Type);
            r.Score = 71f;
            Assert.AreEqual(RelationshipType.BestFriend, r.Type);
            r.Score = 65f;
            Assert.AreEqual(RelationshipType.BestFriend, r.Type);
        }

        [Test]
        public void BrokenPromise_CostsTrust()
        {
            var sim = NewSim();
            float before = sim.State.Stat(StatId.Trust);
            sim.Promises.Make(PromiseKind.FoodQualityAtLeast, "Impossible food", 1, 101f, penalty: 10f);
            sim.AdvanceDay();
            sim.AdvanceDay();
            var p = sim.State.Promises.Last();
            Assert.IsTrue(p.Resolved);
            Assert.IsFalse(p.Kept);
            Assert.Less(sim.State.Stat(StatId.Trust), before - 5f);
        }

        [Test]
        public void KeptPromise_BuildsTrust()
        {
            var sim = NewSim();
            float before = sim.State.Stat(StatId.Trust);
            sim.Promises.Make(PromiseKind.FoodTierAtLeast, "Better menu", 30, (float)FoodTier.Good, reward: 5f);
            sim.SetFoodTier(FoodTier.Good);
            sim.AdvanceDay();
            var p = sim.State.Promises.Last();
            Assert.IsTrue(p.Kept);
            Assert.Greater(sim.State.Stat(StatId.Trust), before);
        }

        [Test]
        public void TuitionIncrease_IsEstimatedAsRiskier()
        {
            var sim = NewSim();
            float current = sim.State.Policies.AnnualTuition;
            var est = sim.Economy.EstimateTuition(current * 1.5f);
            Assert.Greater(est.MonthlyRevenueChange, 0f);
            Assert.Less(est.ParentReaction, 0f);
            Assert.LessOrEqual(est.NewRetention, est.CurrentRetention);
            Assert.Less(est.ApplicantChange, 0f);
        }

        [Test]
        public void DormUpgrade_BlockedWhenResidentsWouldNotFit()
        {
            var sim = NewSim();
            sim.State.Finance.Cash = 1_000_000f;
            var dorm = sim.State.Rooms.First(r => r.Type == RoomType.DormRoom && sim.Indices.ResidentsOf(r.Id).Count > 4);
            var result = sim.UpgradeRoom(dorm.Id);
            Assert.IsFalse(result.Ok);
            StringAssert.Contains("Move", result.Message);
        }

        [Test]
        public void HRManager_AutoApprovesSmallRaisesUnderPolicy()
        {
            var sim = NewSim();
            var s = sim.State;
            s.Progress.SchoolLevel = Balance.UnlockLevel(Feature.Administrators);
            var hr = sim.Staff.CreateStaff(StaffRole.Administrator, 100f, AdminRole.HRManager);
            hr.Skill = 100f;
            s.Staff.Add(hr);
            s.AdminPolicies.AutoApproveRaiseBelow = 0.5f;

            var teacher = s.Staff.First(x => x.Role == StaffRole.Teacher);
            float oldSalary = teacher.Salary;
            Assert.IsTrue(sim.Events.TriggerDirected("raise_request", m => m.StaffId = teacher.Id, ignoreCooldown: true));
            var msg = s.Inbox.Last(m => m.TemplateId == "raise_request");

            sim.AdvanceDay();
            sim.AdvanceDay();
            Assert.IsTrue(msg.Resolved);
            Assert.AreEqual(hr.Name, msg.HandledBy);
            Assert.Greater(teacher.Salary, oldSalary);
        }

        [Test]
        public void IgnoringAMessage_AppliesItsExpiryConsequence()
        {
            var sim = NewSim();
            var teacher = sim.State.Staff.First(x => x.Role == StaffRole.Teacher);
            sim.Events.TriggerDirected("raise_request", m => m.StaffId = teacher.Id, ignoreCooldown: true);
            var msg = sim.State.Inbox.Last(m => m.TemplateId == "raise_request");
            for (int i = 0; i < 15; i++) sim.AdvanceDay();
            Assert.IsTrue(msg.Resolved);
            Assert.AreEqual("expired", msg.ChosenOptionId);
            Assert.IsTrue(teacher.Moodlets.Any(m => m.Label.Contains("ignored")));
        }
    }
}
