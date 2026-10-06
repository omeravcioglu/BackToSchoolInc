using System;
using System.Linq;
using NUnit.Framework;

namespace SchoolSim.Tests
{
    /// <summary>Signature buildings, students from abroad, the old estate, the endowment and the elite institution.</summary>
    public class LateGameTests
    {
        static Simulation NewSim(int level, float cash = 5_000_000f)
        {
            var sim = NewGame.Create("Test School", 42);
            sim.State.Progress.SchoolLevel = level;
            sim.State.Finance.Cash = cash;
            return sim;
        }

        /// <summary>Gives the school the whole main campus, so big buildings fit.</summary>
        static void OwnMainCampus(Simulation sim)
        {
            var map = sim.State.Campus;
            for (int py = 0; py < map.ParcelsY; py++)
                for (int px = 0; px < map.EstateParcelX; px++)
                    map.SetParcelOwned(px, py, true);
        }

        /// <summary>Builds a room and opens it today.</summary>
        static Room Open(Simulation sim, RoomType type)
        {
            var room = sim.Facilities.BuildAuto(type, out string why);
            Assert.IsNotNull(room, why);
            room.ConstructionDoneDay = sim.Day;
            sim.Stats.ComputeIndices();
            return room;
        }

        static float Term(Breakdown b, string label) => b.Factors.Where(f => f.Label == label).Sum(f => f.Value);

        [Test]
        public void SignatureBuilding_IsOnlyForItsSpecialization()
        {
            int level = Balance.UnlockLevel(Feature.Specializations);
            var sim = NewSim(level);
            Assert.IsFalse(sim.Facilities.CanBuild(RoomType.ScholarsHall, out string why));
            StringAssert.Contains("Academic Academy", why);
            sim.State.Specialization = Specialization.AcademicAcademy;
            Assert.IsTrue(sim.Facilities.CanBuild(RoomType.ScholarsHall, out _));
            Assert.IsFalse(sim.Facilities.CanBuild(RoomType.Arena, out _));
            Assert.IsFalse(ProgressionSystem.RoomsUnlockedAt(level).Any(d => d.Signature != Specialization.None),
                "the level-up card doesn't offer every specialization's building");
        }

        [Test]
        public void SignatureBuilding_LiftsItsStats_MoreAtHigherLevels()
        {
            var sim = NewSim(10);
            sim.State.Specialization = Specialization.AcademicAcademy;
            OwnMainCampus(sim);
            var hall = Open(sim, RoomType.ScholarsHall);
            sim.Stats.UpdateSchoolStats(initialize: false);
            float level1 = Term(sim.Stats.StatFactors[(int)StatId.EducationQuality], hall.Def.Name);
            Assert.Greater(level1, 0f);
            Assert.Greater(Term(sim.Stats.ReputationFactors[(int)ReputationId.Academic], hall.Def.Name), 0f);

            hall.Level = 3;
            sim.Stats.ComputeIndices();
            sim.Stats.UpdateSchoolStats(initialize: false);
            Assert.Greater(Term(sim.Stats.StatFactors[(int)StatId.EducationQuality], hall.Def.Name), level1);
        }

        [Test]
        public void Arena_IsAHomeVenue_AndEarnsOnEventNights()
        {
            var sim = NewSim(10);
            sim.State.Specialization = Specialization.SportsAcademy;
            OwnMainCampus(sim);
            var arena = Open(sim, RoomType.Arena);
            Assert.AreEqual(arena, sim.Sports.HomeVenue(SportType.Basketball));
            Assert.Greater(sim.Economy.SignatureIncome(), 0f);
            Assert.Greater(sim.Economy.Project().Signature, 0f, "event nights are part of the expected month");
        }

        [Test]
        public void StudentsFromAbroad_ApplyFromLevel12_PayTheFee_AndStartHomesick()
        {
            int level = Balance.UnlockLevel(Feature.InternationalStudents);
            var sim = NewSim(level - 1);
            Assert.AreEqual(0f, sim.Admissions.ExpectedInternational());
            sim.State.Progress.SchoolLevel = level;
            Assert.Greater(sim.Admissions.ExpectedInternational(), 0f);

            var a = sim.Admissions.GenerateApplicant(transfer: false, international: true);
            Assert.IsTrue(a.Student.International);
            Assert.Greater(a.Student.Homesick, 40f);
            Assert.AreEqual(0f, a.RequestedAid, "families from abroad don't ask for aid");

            float tuition = sim.Economy.Project().Tuition;
            sim.Students.Enroll(a.Student);
            float month = sim.State.Policies.AnnualTuition / Balance.TuitionInstallments;
            Assert.AreEqual(tuition + month * (1f + Balance.InternationalFeePremium), sim.Economy.Project().Tuition, 1f);
        }

        [Test]
        public void InternationalHouse_HelpsHomesicknessFade()
        {
            float Settled(bool house)
            {
                var sim = NewSim(Balance.UnlockLevel(Feature.InternationalStudents));
                if (house)
                {
                    OwnMainCampus(sim);
                    Open(sim, RoomType.InternationalHouse);
                }
                var s = sim.Admissions.GenerateApplicant(transfer: false, international: true).Student;
                s.Traits.Clear();
                s.Homesick = 80f;
                sim.Students.Enroll(s);
                sim.Stats.ComputeIndices();
                sim.Students.DailyTick();
                return 80f - s.Homesick;
            }
            float alone = Settled(false);
            Assert.Greater(alone, 0f, "homesickness fades by itself, slowly");
            Assert.Greater(Settled(true), alone + 0.2f);
        }

        [Test]
        public void OldEstate_IsReservedUntilLevel25_ThenOpensWithItsManor()
        {
            int level = Balance.UnlockLevel(Feature.SecondCampus);
            var sim = NewSim(level - 1);
            var map = sim.State.Campus;
            int ex = map.EstateParcelX, my = map.ManorY / CampusMap.ParcelSize;
            Assert.AreEqual(4, map.OwnedParcelCount, "a new school still starts with its 2x2 block");
            map.SetParcelOwned(ex - 1, my, true);   // our land reaches the road
            Assert.IsFalse(map.IsParcelPurchasable(ex, my), "the estate isn't for sale before it opens");
            Assert.IsFalse(sim.OpenEstate().Ok);

            sim.State.Progress.SchoolLevel = level;
            float cash = sim.State.Finance.Cash;
            var result = sim.OpenEstate();
            Assert.IsTrue(result.Ok, result.Message);
            Assert.AreEqual(cash - Balance.EstateOpenCost, sim.State.Finance.Cash, 1f);
            Assert.IsTrue(map.EstateOpen);
            Assert.IsFalse(sim.OpenEstate().Ok, "only once");

            var manor = sim.State.Rooms.Single(r => r.Type == RoomType.Manor);
            Assert.AreEqual(PlacementIssue.None, sim.Facilities.CheckPlacement(manor.Def, manor.X, manor.Y, manor.Rotation, manor.Id, out _));
            Assert.IsFalse(sim.DemolishRoom(manor.Id).Ok, "the manor is a listed building");
            Assert.IsFalse(sim.Facilities.CanBuild(RoomType.Manor, out _), "it comes with the estate");

            // The rest of the estate is for sale at its own, gentler prices.
            float mainPrice = sim.Facilities.LandCost;
            Assert.IsTrue(map.IsParcelPurchasable(ex + 2, my));
            Assert.AreEqual(Balance.EstateParcelCost, sim.Facilities.ParcelCost(ex + 2, my), 1f);
            Assert.IsTrue(sim.BuyParcel(ex + 2, my).Ok);
            Assert.AreEqual(1, sim.State.EstatePurchases);
            Assert.AreEqual(mainPrice, sim.Facilities.LandCost, 0.01f, "the main campus price doesn't move");
            Assert.Greater(sim.Facilities.ParcelCost(ex + 2, my + 1), Balance.EstateParcelCost);
        }

        [Test]
        public void Endowment_TakesMoneyForGood_PaysOutEachSeptember_AndLendsPrestige()
        {
            int level = Balance.UnlockLevel(Feature.Endowment);
            var sim = NewSim(level - 1, 1_000_000f);
            Assert.IsFalse(sim.Endow(100000f).Ok, "it opens at its level");
            sim.State.Progress.SchoolLevel = level;
            Assert.IsFalse(sim.Endow(1000f).Ok, "too small a gift");
            Assert.IsFalse(sim.Endow(2_000_000f).Ok, "more than the school has");
            Assert.IsTrue(sim.Endow(500000f).Ok);
            Assert.AreEqual(500000f, sim.State.Finance.Cash, 1f);
            Assert.AreEqual(500000f, sim.State.Endowment, 1f);

            sim.Stats.UpdateSchoolStats(initialize: false);
            Assert.Greater(Term(sim.Stats.ReputationFactors[(int)ReputationId.Overall], "Endowment"), 0f);

            // Day 0 is the first of September: the fund pays its share and keeps growing.
            float before = sim.State.Finance.Current[LedgerCategory.Endowment];
            sim.Economy.MonthlyBilling();
            Assert.AreEqual(500000f * Balance.EndowmentPayout, sim.State.Finance.Current[LedgerCategory.Endowment] - before, 1f);
            Assert.AreEqual(500000f * (1f + Balance.EndowmentGrowth), sim.State.Endowment, 1f);
        }

        [Test]
        public void EliteInstitution_DrawsNationalScholars_AndAGalaThatPays()
        {
            var sim = NewSim(Balance.UnlockLevel(Feature.EliteInstitution), 300000f);
            sim.Admissions.OpenSeason();
            Assert.IsTrue(sim.State.Applicants.Any(a => a.Scholar && a.Student.Academic >= 88f));
            sim.Stats.UpdateSchoolStats(initialize: false);
            Assert.Greater(Term(sim.Stats.ReputationFactors[(int)ReputationId.Overall], "Elite institution"), 0f);

            sim.State.Endowment = 10_000_000f;   // the fund's friends come too
            Assert.IsTrue(sim.Events.TriggerDirected("founders_gala", null, ignoreCooldown: true));
            var gala = sim.State.Inbox.Last(m => m.TemplateId == "founders_gala");
            float cash = sim.State.Finance.Cash;
            Assert.IsTrue(sim.Reply(gala.Id, "grand").Ok);
            Assert.Greater(sim.State.Finance.Cash, cash, "a grand gala raises more than it costs");
        }

        [Test]
        public void EveryUnlockIsDescribed_NothingIsPlanned()
        {
            foreach (Feature f in Enum.GetValues(typeof(Feature)))
                StringAssert.DoesNotContain("planned", ProgressionSystem.DescribeFeature(f));
        }
    }
}
