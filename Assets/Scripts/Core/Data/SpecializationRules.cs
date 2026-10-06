using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Bonuses and drawbacks of each school specialization. Systems query these instead of
    /// switching on the specialization themselves.
    /// </summary>
    public static class SpecializationRules
    {
        public static float EducationBonus(Specialization s) => s switch
        {
            Specialization.AcademicAcademy => 8f,
            Specialization.TechnologySchool => 4f,
            Specialization.SportsAcademy => -4f,
            Specialization.ArtsAcademy => -2f,
            _ => 0f,
        };

        public static float StudentLifeBonus(Specialization s) => s switch
        {
            Specialization.ArtsAcademy => 8f,
            Specialization.LuxuryBoarding => 5f,
            Specialization.CommunitySchool => 3f,
            _ => 0f,
        };

        public static float StressBonus(Specialization s) => s == Specialization.AcademicAcademy ? 5f : 0f;
        public static float TeamRatingBonus(Specialization s) => s == Specialization.SportsAcademy ? 6f : 0f;

        public static float SportsRepBonus(Specialization s) => s switch
        {
            Specialization.SportsAcademy => 8f,
            Specialization.AcademicAcademy => -5f,
            _ => 0f,
        };

        public static float FairTuitionMultiplier(Specialization s) => s switch
        {
            Specialization.AcademicAcademy => 1.10f,
            Specialization.LuxuryBoarding => 1.15f,
            Specialization.CommunitySchool => 0.90f,
            _ => 1f,
        };

        public static float DormFeeMultiplier(Specialization s) => s == Specialization.LuxuryBoarding ? 1.25f : 1f;
        public static float TuitionSensitivity(Specialization s) => s == Specialization.LuxuryBoarding ? 0.6f : 1f;
        public static float UpkeepMultiplier(Specialization s) => s == Specialization.LuxuryBoarding ? 1.15f : 1f;
        public static float MerchMultiplier(Specialization s) => s == Specialization.SportsAcademy ? 1.5f : 1f;
        public static float InternetCostMultiplier(Specialization s) => s == Specialization.TechnologySchool ? 0.5f : 1f;
        public static float TrustFloor(Specialization s) => s == Specialization.CommunitySchool ? 40f : 0f;

        public static float ApplicantMultiplier(Specialization s) => s switch
        {
            Specialization.LuxuryBoarding => 0.85f,
            Specialization.CommunitySchool => 1.3f,
            _ => 1f,
        };

        public static float ApplicantWealthShift(Specialization s) => s switch
        {
            Specialization.LuxuryBoarding => 20f,
            Specialization.CommunitySchool => -10f,
            _ => 0f,
        };

        public static float ApplicantAcademicShift(Specialization s) => s switch
        {
            Specialization.AcademicAcademy => 8f,
            Specialization.TechnologySchool => 4f,
            _ => 0f,
        };

        public static float ApplicantAthleticShift(Specialization s) => s == Specialization.SportsAcademy ? 10f : 0f;
        public static float ApplicantArtisticShift(Specialization s) => s == Specialization.ArtsAcademy ? 12f : 0f;

        public static float ComputerLabMultiplier(Specialization s) => s == Specialization.TechnologySchool ? 2f : 1f;
        public static float ScienceLabMultiplier(Specialization s) => s == Specialization.AcademicAcademy ? 1.3f : 1f;

        /// <summary>Monthly grant per financial-aid student.</summary>
        public static float AidGrantPerStudent(Specialization s) => s == Specialization.CommunitySchool ? 180f : 0f;

        public static float ClubCompetitionBonus(Specialization s, ClubType club)
        {
            switch (s)
            {
                case Specialization.ArtsAcademy:
                    return club == ClubType.Art || club == ClubType.Music || club == ClubType.Drama ? 15f : 0f;
                case Specialization.TechnologySchool:
                    return club == ClubType.Robotics || club == ClubType.Gaming ? 15f : 0f;
                case Specialization.AcademicAcademy:
                    return club == ClubType.Chess || club == ClubType.Debate || club == ClubType.Science ? 10f : 0f;
                default:
                    return 0f;
            }
        }

        public static string Describe(Specialization s) => s switch
        {
            Specialization.AcademicAcademy =>
                "+8 education, +10% fair tuition, stronger academic applicants. Students are more stressed; sports reputation suffers. Builds a Scholars' Hall.",
            Specialization.SportsAcademy =>
                "+6 team rating, +8 sports reputation, more athletes apply, +50% merchandise. -4 education. Builds an Arena.",
            Specialization.LuxuryBoarding =>
                "+25% dorm fees, +15% fair tuition, wealthier families who worry less about price. +15% upkeep, fewer applicants. Builds a Clubhouse.",
            Specialization.ArtsAcademy =>
                "+8 student life, artistic applicants, arts clubs win more competitions. -2 education. Builds a Theatre.",
            Specialization.TechnologySchool =>
                "Computer labs count double, +4 education, half-price internet, robotics and gaming clubs excel. Builds an Innovation Lab.",
            Specialization.CommunitySchool =>
                "+30% applicants, a monthly grant per financial-aid student, trust never drops below 40. -10% fair tuition; poorer families. Builds a Community Centre.",
            _ => "No specialization.",
        };

        // --- The signature building ------------------------------------------------------

        static readonly Dictionary<Specialization, SignatureBuilding> Signatures = new Dictionary<Specialization, SignatureBuilding>
        {
            [Specialization.AcademicAcademy] = new SignatureBuilding
            {
                Type = RoomType.ScholarsHall, Education = 10f, AcademicRep = 5f, Stress = -4f, Parents = 2f, Graduates = 6f, Applicants = 0.05f,
                Income = 14000f, IncomeInSummer = true, IncomeCategory = LedgerCategory.Tuition, IncomeNote = "Summer school",
            },
            [Specialization.SportsAcademy] = new SignatureBuilding
            {
                Type = RoomType.Arena, TeamRating = 10f, SportsRep = 12f, InjuryCut = 0.5f, StudentLife = 6f, Parents = 3f, Applicants = 0.1f,
                Income = 12000f, IncomeCategory = LedgerCategory.Tickets, IncomeNote = "Arena event nights",
            },
            [Specialization.LuxuryBoarding] = new SignatureBuilding
            {
                Type = RoomType.Clubhouse, DormQuality = 12f, Food = 8f, StudentLife = 4f, Stress = -2f, Parents = 6f, DormFee = 0.06f,
            },
            [Specialization.ArtsAcademy] = new SignatureBuilding
            {
                Type = RoomType.Theatre, StudentLife = 14f, Stress = -3f, Parents = 4f, Applicants = 0.15f,
                Income = 10000f, IncomeCategory = LedgerCategory.ClubEvents, IncomeNote = "Theatre shows",
            },
            [Specialization.TechnologySchool] = new SignatureBuilding
            {
                Type = RoomType.InnovationLab, Education = 8f, AcademicRep = 3f, StudentLife = 3f, Parents = 2f, Graduates = 3f, Applicants = 0.05f,
                Income = 8000f, IncomeCategory = LedgerCategory.Sponsorship, IncomeNote = "Industry partners",
            },
            [Specialization.CommunitySchool] = new SignatureBuilding
            {
                Type = RoomType.CommunityCentre, Trust = 12f, Safety = 5f, StudentLife = 4f, Stress = -2f, Parents = 6f, Applicants = 0.2f,
                Income = 6000f, IncomeCategory = LedgerCategory.Grants, IncomeNote = "Community centre grant",
            },
        };

        /// <summary>The specialization's own building and what it does, or null for a general school.</summary>
        public static SignatureBuilding Signature(Specialization s) => Signatures.TryGetValue(s, out var b) ? b : null;

        /// <summary>Share of a signature building's full effect at each level, before dirt and wear.</summary>
        public static float SignatureStrength(int level) => level >= 3 ? 1f : level == 2 ? 0.72f : 0.45f;
    }

    /// <summary>
    /// A specialization's signature building and what it does at full strength, a spotless level-3
    /// building. <see cref="SchoolIndices.Signature"/> scales every number by level and upkeep.
    /// </summary>
    public sealed class SignatureBuilding
    {
        public RoomType Type;
        public float Education, AcademicRep, StudentLife, DormQuality, Food, Safety, Parents;
        public float Trust;                 // where trust settles when nothing happens, above the usual 50
        public float Stress;                // negative: tutors, a stage or a spa take the edge off
        public float TeamRating, SportsRep;
        public float InjuryCut;             // share of training injuries the physio prevents
        public float DormFee;               // extra share on dorm fees
        public float Applicants;            // extra share of families applying
        public float Graduates;             // added to graduates' university placement scores
        public float Income;                // a month's takings: event nights, shows, partners, the council
        public bool IncomeInSummer;         // summer school earns in July and August instead of term time
        public LedgerCategory IncomeCategory;
        public string IncomeNote;

        public string Name => RoomCatalog.Get(Type).Name;
    }
}
