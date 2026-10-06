using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace SchoolSim.Tests
{
    public class SaveTests
    {
        static readonly DateTime SavedAt = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>Answers every letter with the first allowed option, the same way for any school.</summary>
        static void AnswerLetters(Simulation sim)
        {
            foreach (var m in sim.State.Inbox.ToArray())
            {
                if (!m.NeedsDecision) continue;
                var option = m.Options.FirstOrDefault(o => sim.Inbox.WhyNot(m, o) == null);
                if (option != null) sim.Reply(m.Id, option.Id);
            }
        }

        /// <summary>A school with some history: a few months played, every letter answered.</summary>
        static Simulation PlayedSchool(int seed, int days)
        {
            var sim = NewGame.Create(new NewGameSetup { SchoolName = "Saved School", Seed = seed, Assistant = AssistantRules.Candidates(seed)[0] });
            for (int d = 0; d < days && !sim.State.GameOver; d++)
            {
                sim.AdvanceDay();
                AnswerLetters(sim);
            }
            return sim;
        }

        static byte[] Save(Simulation sim)
        {
            using (var ms = new MemoryStream())
            {
                SaveGame.Compress(SaveGame.Capture(sim, SavedAt, "test"), ms);
                return ms.ToArray();
            }
        }

        static Simulation Load(byte[] bytes, out SaveSummary summary)
        {
            using (var ms = new MemoryStream(bytes)) return SaveGame.ReadFile(ms, out summary);
        }

        [Test]
        public void Save_KeepsEverything()
        {
            var sim = PlayedSchool(11, 330);
            var loaded = Load(Save(sim), out _);
            Assert.AreEqual(SaveGame.Write(sim), SaveGame.Write(loaded), "a loaded school is the saved one, field for field");
            Assert.AreEqual(sim.State.Students.Count, loaded.State.Students.Count);
            Assert.AreEqual(sim.State.Finance.Cash, loaded.State.Finance.Cash);
            Assert.AreEqual(sim.State.Relationships.Count, loaded.State.Relationships.Count);
        }

        [Test]
        public void Save_KeepsSharedObjectsShared()
        {
            var sim = PlayedSchool(5, 120);
            var loaded = Load(Save(sim), out _);
            var pair = loaded.State.Relationships.All.FirstOrDefault();
            Assert.IsNotNull(pair, "the school has relationships after four months");
            Assert.IsTrue(loaded.State.Relationships.For(pair.A).Contains(pair), "the per-student index points at the same pair objects");
            Assert.AreSame(pair, loaded.State.Relationships.Get(pair.A, pair.B));
        }

        [Test]
        public void LoadedSchool_CarriesOnLikeTheOriginal()
        {
            // The original and its loaded copy run the same future, even with another school played
            // in between: nothing outside the save steers the simulation.
            var original = PlayedSchool(23, 200);
            var bytes = Save(original);
            PlayedSchool(99, 60);
            var loaded = Load(bytes, out _);
            for (int d = 0; d < 150; d++)
            {
                original.AdvanceDay();
                AnswerLetters(original);
                loaded.AdvanceDay();
                AnswerLetters(loaded);
            }
            Assert.AreEqual(SaveGame.Write(original), SaveGame.Write(loaded));
        }

        [Test]
        public void Summary_DescribesTheSchool()
        {
            var sim = PlayedSchool(3, 40);
            using (var ms = new MemoryStream(Save(sim)))
            {
                var summary = SaveGame.ReadSummary(ms);
                Assert.AreEqual("Saved School", summary.SchoolName);
                Assert.AreEqual(sim.State.Day, summary.Day);
                Assert.AreEqual(sim.State.Students.Count, summary.Students);
                Assert.AreEqual(sim.State.Progress.SchoolLevel, summary.Level);
                Assert.AreEqual(sim.State.Rooms.Count, summary.Rooms.Count);
                Assert.AreEqual(sim.State.Campus.OwnedParcelCount, summary.Parcels.Count(owned => owned));
                Assert.AreEqual(SavedAt, summary.SavedAtUtc);
                Assert.AreEqual("test", summary.GameVersion);
            }
        }

        [Test]
        public void Load_ReadsPlainTextSavesToo()
        {
            var sim = PlayedSchool(8, 30);
            var text = SaveGame.Capture(sim, SavedAt);
            using (var ms = new MemoryStream(new UTF8Encoding(false).GetBytes(text)))
            {
                var loaded = SaveGame.ReadFile(ms, out var summary);
                Assert.AreEqual(sim.State.SchoolName, summary.SchoolName);
                Assert.AreEqual(SaveGame.Write(sim), SaveGame.Write(loaded));
            }
        }

        [Test]
        public void Load_RejectsFilesThatArentSaves()
        {
            using (var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }))
                Assert.Throws<FormatException>(() => SaveGame.ReadSummary(ms));
            using (var ms = new MemoryStream(new byte[0]))
                Assert.Throws<FormatException>(() => SaveGame.ReadSummary(ms));
            using (var ms = new MemoryStream())
            {
                SaveGame.Compress("{\"hello\":\"world\"}\n{}", ms);
                ms.Position = 0;
                Assert.Throws<FormatException>(() => SaveGame.ReadFile(ms, out _));
            }
            Assert.Throws<FormatException>(() => SaveGame.Read("{\"format\":\"school-game\",\"version\":1,\"state\":[1,2"));
        }

        [Test]
        public void Load_RejectsSavesFromNewerVersions()
        {
            var text = SaveGame.Capture(PlayedSchool(4, 5), SavedAt).Replace("\"version\":" + SaveGame.Version, "\"version\":" + (SaveGame.Version + 1));
            Assert.Throws<FormatException>(() => SaveGame.Read(text));
        }
    }
}
