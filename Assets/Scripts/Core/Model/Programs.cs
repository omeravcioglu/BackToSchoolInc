using System.Collections.Generic;

namespace SchoolSim
{
    public sealed class SportsTeam
    {
        public int Id;
        public SportType Sport;
        public int CoachId = -1;
        public readonly List<int> Roster = new List<int>();
        public TrainingIntensity Intensity = TrainingIntensity.Normal;

        public int Wins;
        public int Losses;
        public int NextGameDay;
        public int GamesThisSeason;
        public bool HomeNext = true;
        public int Championships;
        public float Rating;                   // cached daily
        public Breakdown RatingFactors = new Breakdown();
        public readonly List<string> Results = new List<string>();

        public float WinRate => Wins + Losses == 0 ? 0f : (float)Wins / (Wins + Losses);
        public string Name => Sport + " Team";

        public static int MinRoster(SportType s) => s switch
        {
            SportType.Basketball => 5,
            SportType.Soccer => 11,
            SportType.Swimming => 4,
            SportType.Track => 4,
            SportType.Tennis => 2,
            _ => 1,
        };

        public static int MaxRoster(SportType s) => s switch
        {
            SportType.Basketball => 10,
            SportType.Soccer => 18,
            SportType.Swimming => 10,
            SportType.Track => 12,
            SportType.Tennis => 6,
            _ => 1,
        };
    }

    public sealed class Club
    {
        public int Id;
        public ClubType Type;
        public readonly List<int> Members = new List<int>();
        public int FoundedDay;
        public int Wins;
        public float Strength;                 // cached monthly

        public string Name => Type switch
        {
            ClubType.StudentGovernment => "Student Government",
            _ => Type + " Club",
        };

        public const int MaxMembers = 15;
    }

    public sealed class RivalSchool
    {
        public int Id;
        public string Name;
        public Specialization Archetype;
        public float Prestige;
        public float Academic;
        public float SportsRep;
        public float Trend;                    // monthly prestige drift
        public float[] SportStrength;          // indexed by SportType
        public int Students;
        public float Tuition;

        public float Score => Prestige + (float)System.Math.Log(System.Math.Max(10, Students), 2) * 1.5f;
    }

    public sealed class Alumnus
    {
        public string Name;
        public int GraduationYear;
        public UniversityTier Tier;
        public string Achievement;
        public float Wealth;
        public bool Donated;
    }

    public sealed class YearReport
    {
        public int Year;
        public float Revenue;
        public float Expenses;
        public float ScholarshipsForgone;
        public int StudentsStart;
        public int StudentsEnd;
        public int Graduates;
        public int Leavers;
        public float AvgGrades;
        public float ParentSatisfaction;
        public float StudentSatisfaction;
        public float StaffMorale;
        public int StaffLeft;
        public int SportsWins;
        public int SportsLosses;
        public int Championships;
        public int IncidentCount;
        public int RankStart;
        public int RankEnd;
        public float[] ReputationStart;
        public float[] ReputationEnd;
        public readonly List<string> Highlights = new List<string>();
        public readonly List<string> GraduateLines = new List<string>();
    }

    /// <summary>Accumulates facts across a school year for the annual report.</summary>
    public sealed class YearTracker
    {
        public int StudentsStart;
        public int RankStart;
        public float[] ReputationStart;
        public int Incidents;
        public int StaffLeft;
        public int Championships;
        public readonly List<string> Highlights = new List<string>();

        public void Highlight(string text)
        {
            Highlights.Add(text);
            if (Highlights.Count > 30) Highlights.RemoveAt(0);
        }
    }

    public sealed class ReputationModifier
    {
        public ReputationId Rep;
        public string Label;
        public float Value;
        public int ExpiresDay;
    }

    public sealed class AdminPolicies
    {
        public float AutoApproveDiscountBelow = 0.10f;
        public bool RejectLargerDiscounts;
        public float AutoApproveRaiseBelow = 0.05f;
        public bool CounterLargerRaises = true;
        public float AutoCompensateComplaintsBelow = 300f;
        public float AutoAdmitAcademicAbove = 85f;
        public bool FillRemainingBedsAtDeadline = true;
        public float AutoMatchOffersSkillAbove = 75f;
        public string DefaultDisciplineResponse = "detention";
    }

    public sealed class SchoolPolicies
    {
        public float AnnualTuition = Balance.StartingTuition;
        public float PendingTuition = -1f;
        public FoodTier Food = FoodTier.Standard;
        public CurfewPolicy Curfew = CurfewPolicy.Normal;
        public DisciplinePolicy Discipline = DisciplinePolicy.Balanced;
        public int InternetLevel = 1;
        public float TicketPrice = 8f;
    }

    public sealed class Progression
    {
        public int SchoolLevel = 1;
        public int SchoolXp;
        public int DirectorLevel = 1;
        public int DirectorXp;
        public bool SpecializationOffered;

        /// <summary>Which parts of the game the director can see yet, indexed by <see cref="Reveal"/>.</summary>
        public readonly bool[] Revealed = new bool[System.Enum.GetValues(typeof(Reveal)).Length];
    }
}
