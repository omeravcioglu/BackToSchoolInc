using System.Linq;
using NUnit.Framework;

namespace SchoolSim.Tests
{
    /// <summary>The assistant, the tutorial, progression reveals, news, history and rankings.</summary>
    public class DirectorTests
    {
        static Simulation WithAssistant(AssistantStyle style, bool tutorial = false, int seed = 42)
        {
            var assistant = new Assistant { Name = "Robin Test", Style = style, Skill = 3, Salary = AssistantRules.Salary(style, 3), Look = 5 };
            return NewGame.Create(new NewGameSetup { SchoolName = "Test School", Seed = seed, Assistant = assistant, Tutorial = tutorial });
        }

        [Test]
        public void Candidates_HaveDifferentPersonalities_AndAreRepeatable()
        {
            var a = AssistantRules.Candidates(123);
            var b = AssistantRules.Candidates(123);
            Assert.AreEqual(4, a.Count);
            Assert.AreEqual(4, a.Select(c => c.Style).Distinct().Count(), "every candidate has a different personality");
            Assert.IsTrue(a.All(c => c.Salary > 0f && c.Skill >= 1 && c.Skill <= 5));
            CollectionAssert.AreEqual(a.Select(c => c.Name).ToList(), b.Select(c => c.Name).ToList());
        }

        [Test]
        public void Assistant_IsPaidWithPayroll_AndBringsTheirPerk()
        {
            var plain = NewGame.Create("Test School", 42);
            var strict = WithAssistant(AssistantStyle.Strict);
            Assert.AreEqual(plain.Economy.Payroll() + strict.State.Assistant.Salary, strict.Economy.Payroll(), 0.01f);
            Assert.IsTrue(strict.Stats.StatFactors[(int)StatId.SafetyDiscipline].Factors.Any(f => f.Label.Contains("assistant") && f.Value > 0f));
            Assert.Greater(strict.State.Stat(StatId.SafetyDiscipline), plain.State.Stat(StatId.SafetyDiscipline));
        }

        [Test]
        public void Assistant_BringsProblemsWithFixes_AndAdvice()
        {
            var sim = WithAssistant(AssistantStyle.Handy);
            sim.State.Finance.Cash = 200000f;
            foreach (var r in sim.State.Rooms) r.Cleanliness = 10f;   // a filthy campus
            InboxMessage problem = null;
            for (int i = 0; i < 10 && problem == null; i++)
            {
                sim.AdvanceDay();
                problem = sim.State.Inbox.FirstOrDefault(m => m.TemplateId == "assist_dirty" && m.NeedsDecision);
            }
            Assert.IsNotNull(problem, "the assistant reports the dirt");
            Assert.AreEqual(SenderType.Assistant, problem.Sender);
            Assert.IsNotNull(problem.FindOption("later"), "there's always a 'not now'");
            Assert.IsNotNull(problem.AdvisorOptionId, "the assistant recommends something");

            var result = sim.Reply(problem.Id, "deepclean");
            Assert.IsTrue(result.Ok, result.Message);
            Assert.IsTrue(sim.State.Rooms.All(r => r.Cleanliness >= 90f));
        }

        [Test]
        public void BookishAssistant_DoesNotNoticeDirt()
        {
            var sim = WithAssistant(AssistantStyle.Bookish);
            foreach (var r in sim.State.Rooms) r.Cleanliness = 10f;
            for (int i = 0; i < 12; i++) sim.AdvanceDay();
            Assert.IsFalse(sim.State.Inbox.Any(m => m.TemplateId == "assist_dirty"));
        }

        [Test]
        public void EveryDecisionGetsAdvice_WhenThereIsAnAssistant()
        {
            var sim = WithAssistant(AssistantStyle.Charming);
            var teacher = sim.State.Staff.First(s => s.Role == StaffRole.Teacher);
            Assert.IsTrue(sim.Events.TriggerDirected("raise_request", m => m.StaffId = teacher.Id, ignoreCooldown: true));
            var msg = sim.State.Inbox.Last(m => m.TemplateId == "raise_request");
            Assert.IsNotNull(msg.AdvisorOptionId);
            Assert.IsNotNull(msg.FindOption(msg.AdvisorOptionId));
            StringAssert.Contains(msg.FindOption(msg.AdvisorOptionId).Label, msg.AdvisorNote);
        }

        [Test]
        public void Tutorial_StartsSmall_AndRevealsAsItGoes()
        {
            var sim = WithAssistant(AssistantStyle.Cheerful, tutorial: true);
            Assert.IsTrue(sim.Tutorial.Active);
            Assert.IsTrue(sim.IsRevealed(Reveal.Inbox));
            Assert.IsFalse(sim.IsRevealed(Reveal.Build), "building waits for its tutorial step");
            Assert.IsFalse(sim.IsRevealed(Reveal.Stats));

            sim.TutorialSignal("next");            // hello
            sim.TutorialSignal("open:inbox");      // inbox
            Assert.AreEqual("decide", sim.Tutorial.Current.Id);
            var practice = sim.State.Inbox.Last(m => m.TemplateId == "tutorial_choice");
            Assert.IsTrue(sim.Reply(practice.Id, "save").Ok);
            Assert.AreEqual("build", sim.Tutorial.Current.Id);
            Assert.IsTrue(sim.IsRevealed(Reveal.Build));

            Assert.IsTrue(sim.BuildRoom(RoomType.DormRoom).Ok);
            Assert.AreEqual("room", sim.Tutorial.Current.Id, "building a dorm completes the step");

            sim.SkipTutorial();
            Assert.IsFalse(sim.Tutorial.Active);
            foreach (var r in TutorialSystem.Basics) Assert.IsTrue(sim.IsRevealed(r), r + " is revealed after skipping");
        }

        [Test]
        public void Tutorial_KeepsTheFirstWeeksQuiet()
        {
            var sim = WithAssistant(AssistantStyle.Strict, tutorial: true);
            for (int i = 0; i < Balance.TutorialQuietDays - 1; i++) sim.AdvanceDay();
            bool Quiet(InboxMessage m) => sim.Events.Get(m.TemplateId)?.Random != true && !AssistantSystem.IsProblem(m) && m.TemplateId != "transfer_applicant";
            Assert.IsTrue(sim.State.Inbox.All(Quiet), "no random events, transfer requests or assistant problems in the first weeks");
        }

        [Test]
        public void LevelTwo_RevealsRulesStatsAndLand()
        {
            var sim = NewGame.Create("Test School", 3);
            Assert.IsFalse(sim.IsRevealed(Reveal.Rules));
            int leveled = 0;
            sim.LeveledUp += l => leveled = l;
            sim.Progression.AddSchoolXp(Balance.SchoolXpForLevel(2));
            Assert.AreEqual(2, leveled);
            Assert.IsTrue(sim.IsRevealed(Reveal.Rules));
            Assert.IsTrue(sim.IsRevealed(Reveal.Stats));
            Assert.IsTrue(sim.IsRevealed(Reveal.Land));
        }

        [Test]
        public void Rooms_UnlockWithSchoolLevel()
        {
            var sim = NewGame.Create("Test School", 3);
            sim.State.Finance.Cash = 1_000_000f;
            Assert.IsFalse(sim.Facilities.CanBuild(RoomType.Library, out string why));
            StringAssert.Contains("level", why);
            sim.State.Progress.SchoolLevel = 2;
            Assert.IsTrue(sim.Facilities.CanBuild(RoomType.Library, out _));
        }

        [Test]
        public void BadPress_HurtsReputation_AndCharmingAssistantsSoftenIt()
        {
            var plain = NewGame.Create("Test School", 9);
            var charming = WithAssistant(AssistantStyle.Charming, seed: 9);
            var a = plain.News.Publish(NewsTone.Bad, NewsTopic.Food, "Test scandal", "Body", -2f, ReputationId.Food, 30);
            var b = charming.News.Publish(NewsTone.Bad, NewsTopic.Food, "Test scandal", "Body", -2f, ReputationId.Food, 30);
            Assert.AreEqual(-2f, a.Effect, 0.001f);
            Assert.Greater(b.Effect, a.Effect, "a charming assistant softens bad press");
            Assert.IsTrue(plain.State.ReputationModifiers.Any(m => m.Label == a.EffectLabel && m.Value < 0f));
            Assert.IsTrue(plain.IsRevealed(Reveal.News), "the first story brings the News page");
        }

        [Test]
        public void FirstMonthlyRanking_RevealsRankings_AndMakesTheNews()
        {
            var sim = NewGame.Create("Test School", 11);
            Assert.IsFalse(sim.IsRevealed(Reveal.Rankings));
            for (int i = 0; i < Calendar.DaysPerMonth; i++) sim.AdvanceDay();
            Assert.IsTrue(sim.IsRevealed(Reveal.Rankings));
            Assert.IsTrue(sim.State.News.Any(n => n.Topic == NewsTopic.Rankings));
            var table = sim.Rivals.Rankings(RankingTable.Academic);
            Assert.AreEqual(sim.State.Rivals.Count + 1, table.Count);
            Assert.IsTrue(table.Zip(table.Skip(1), (x, y) => x.Value >= y.Value).All(ok => ok), "tables are sorted best first");
        }

        [Test]
        public void History_RecordsEveryDay()
        {
            var sim = NewGame.Create("Test School", 5);
            for (int i = 0; i < 40; i++) sim.AdvanceDay();
            Assert.AreEqual(41, sim.State.History.Count, "day 0 plus one snapshot per day");
            Assert.AreEqual(20, sim.History.At(20).Day);
            Assert.AreEqual(0, sim.History.At(-5).Day);
            Assert.AreEqual(sim.State.Rivals.Count, sim.State.History.Last().RivalScores.Length);
        }
    }
}
