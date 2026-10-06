namespace SchoolSim
{
    /// <summary>
    /// Central tuning knobs. Anything a designer is likely to tweak lives here rather than
    /// being buried in a system.
    /// </summary>
    public static class Balance
    {
        // --- Start of game -------------------------------------------------------------
        public const float StartingCash = 90000f;
        public const float StartingTuition = 13500f;  // annual
        public const int StartingStudents = 10;

        // --- Economy --------------------------------------------------------------------
        public const int TuitionInstallments = Calendar.MonthsPerYear;    // tuition billed monthly, year-round
        public const int DormBillingMonths = Calendar.LastSchoolMonth;     // dorm fees only while students board (Sep-Jun)
        public const int InsolvencyGraceDays = 120;

        /// <summary>What students expect from food and dorms rises with the school's prestige.</summary>
        public static float FoodExpectation(float prestige) => 45f + prestige * 0.12f;
        public static float DormExpectation(float prestige) => 40f + prestige * 0.2f;

        /// <summary>Staff at prestigious schools expect to be paid more.</summary>
        public static float PrestigeWageFactor(float prestige) => 1f + prestige / 400f;
        public const int MealsPerDay = 3;
        public const float UtilitiesPerRoom = 45f;
        public const float WageInflationPerYear = 0.025f;
        public const float RepairCostPerConditionPoint = 25f;

        public static float FairTuition(float prestige)
        {
            // Roughly what parents think a school of this prestige is worth per year.
            return 11000f + 90f * prestige + 1.8f * prestige * prestige;
        }

        public static float MealCost(FoodTier tier) => tier switch
        {
            FoodTier.Budget => 2.0f,
            FoodTier.Standard => 3.0f,
            FoodTier.Good => 4.6f,
            FoodTier.Premium => 7.0f,
            _ => 3.0f,
        };

        public static float FoodBaseQuality(FoodTier tier) => tier switch
        {
            FoodTier.Budget => 30f,
            FoodTier.Standard => 52f,
            FoodTier.Good => 70f,
            FoodTier.Premium => 86f,
            _ => 52f,
        };

        public static float InternetMonthlyCost(int level) => level switch
        {
            1 => 150f,
            2 => 600f,
            3 => 1800f,
            _ => 150f,
        };

        public static float InternetUpgradeCost(int level) => level switch
        {
            2 => 6000f,
            3 => 18000f,
            _ => 0f,
        };

        // --- Staffing ratios ------------------------------------------------------------
        public const float StudentsPerTeacherIdeal = 12f;
        public const float StudentsPerCustodian = 40f;
        public const float StudentsPerChef = 45f;
        public const float StudentsPerGuard = 50f;
        public const float StudentsPerNurse = 120f;
        public const float StudentsPerCounselor = 100f;

        public static float TrainingCost(TrainingProgram p) => p switch
        {
            TrainingProgram.Workshop => 1500f,
            TrainingProgram.Seminar => 4000f,
            TrainingProgram.Certification => 9500f,
            _ => 0f,
        };

        public static int TrainingDays(TrainingProgram p) => p switch
        {
            TrainingProgram.Workshop => 3,
            TrainingProgram.Seminar => 7,
            TrainingProgram.Certification => 20,
            _ => 0,
        };

        // --- Admissions ---------------------------------------------------------------
        public static float ApplicantBase(float prestige) => 6f + prestige * 0.35f + prestige * prestige * 0.006f;

        /// <summary>Expected transfer applications per school month (while beds are free).</summary>
        public static float TransferRate(float prestige) => 0.6f + prestige / 50f;

        public const float MarketingCost = 3000f;
        public const int MarketingDays = 30;

        // --- International students -----------------------------------------------------
        /// <summary>Families from abroad pay this share of tuition on top.</summary>
        public const float InternationalFeePremium = 0.4f;

        /// <summary>Families from abroad applying in an Admissions Season, before the International House draws more.</summary>
        public static float InternationalApplicants(float prestige) => 2f + prestige / 15f;

        // --- The old estate (second campus) ---------------------------------------------
        public const float EstateOpenCost = 800000f;
        public const float EstateParcelCost = 150000f;     // the first estate parcel; each one after costs 20% more
        public const float EstateParcelGrowth = 1.2f;

        /// <summary>How much of the old manor's full effect each level brings: a dusty ruin, restored, glorious.</summary>
        public static float ManorStrength(int level) => level >= 3 ? 1f : level == 2 ? 0.6f : 0.2f;
        public const float ManorPrestige = 4f;
        public const float ManorStudentLife = 6f;
        public const float ManorParents = 3f;

        // --- Endowment and the elite institution -----------------------------------------
        /// <summary>Share of the fund paid out to the school each September.</summary>
        public const float EndowmentPayout = 0.03f;
        /// <summary>What the fund earns on top of the payout each year, so it keeps its value.</summary>
        public const float EndowmentGrowth = 0.02f;
        public const float EndowmentMinGift = 10000f;

        /// <summary>
        /// Prestige an endowment of this size lends the school: every doubling adds the same, so there is
        /// always more to gain, ever more slowly. An elite institution's fund counts for more.
        /// </summary>
        public static float EndowmentPrestige(float fund, bool elite) =>
            fund <= 0f ? 0f : (elite ? 1.5f : 1.2f) * (float)System.Math.Log(1f + fund / 750000f, 2);

        public const float ElitePrestige = 3f;
        public const float GalaCost = 50000f;
        public const float DinnerCost = 15000f;
        public const int GalaMonth = 7;                     // March

        // --- Progression --------------------------------------------------------------
        public static int SchoolXpForLevel(int level) => level <= 1 ? 0 : (int)(110f * System.Math.Pow(level - 1, 1.75));
        public static int DirectorXpForLevel(int level) => level <= 1 ? 0 : (int)(60f * System.Math.Pow(level - 1, 1.5));

        public static int UnlockLevel(Feature f) => f switch
        {
            Feature.Clubs => 3,
            Feature.Administrators => 4,
            Feature.Scholarships => 5,
            Feature.AdvancedDorms => 7,
            Feature.Specializations => 8,
            Feature.SportsPrograms => 10,
            Feature.InternationalStudents => 12,
            Feature.MajorSponsorships => 15,
            Feature.Endowment => 16,
            Feature.AdvancedDepartments => 18,
            Feature.LargeCompetitions => 20,
            Feature.SecondCampus => 25,
            Feature.EliteInstitution => 30,
            _ => 1,
        };

        /// <summary>Director level at which hidden consequences of inbox options start being hinted.</summary>
        public const int DirectorInsightLevel = 3;

        /// <summary>While the tutorial runs, random events and assistant problems hold off this many days.</summary>
        public const int TutorialQuietDays = 20;
    }
}
