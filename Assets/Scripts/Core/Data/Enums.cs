namespace SchoolSim
{
    /// <summary>School-wide operating stats (0-100). Recomputed daily from the underlying state.</summary>
    public enum StatId
    {
        EducationQuality,
        StudentSatisfaction,
        ParentSatisfaction,
        DormitoryQuality,
        StaffMorale,
        SafetyDiscipline,
        FoodQuality,
        Cleanliness,
        StudentLife,
        Trust,
    }

    /// <summary>Public reputation categories (0-100). These drift slowly toward the operating stats.</summary>
    public enum ReputationId
    {
        Academic,
        Sports,
        Dormitory,
        StudentLife,
        Safety,
        Food,
        Overall,
    }

    public enum StudentTrait
    {
        Extrovert,
        Introvert,
        Messy,
        CleanFreak,
        Competitive,
        Lazy,
        Hardworking,
        NightOwl,
        Athletic,
        Artistic,
        Troublemaker,
        Bookworm,
        Leader,
        Anxious,
    }

    public enum StaffTrait
    {
        Strict,
        Funny,
        Popular,
        Lazy,
        ExcellentTeacher,
        Motivational,
        Difficult,
        BurnoutProne,
        Loyal,
        Ambitious,
    }

    public enum RelationshipType
    {
        Neutral,
        Friend,
        BestFriend,
        Rival,
        Enemy,
        Crush,
    }

    public enum StaffRole
    {
        Teacher,
        Coach,
        Custodian,
        Maintenance,
        Chef,
        Nurse,
        Security,
        Counselor,
        Administrator,
    }

    public enum AdminRole
    {
        None,
        HRManager,
        AdmissionsDirector,
        DormDirector,
        AthleticDirector,
        FinancialDirector,
        StudentAffairsDirector,
    }

    public enum Subject
    {
        None,
        Math,
        Science,
        Language,
        History,
        Arts,
        Technology,
    }

    public enum RoomType
    {
        Classroom,
        DormRoom,
        Cafeteria,
        Bathroom,
        LaundryRoom,
        Library,
        ScienceLab,
        ComputerLab,
        Office,
        StaffLounge,
        StudentLounge,
        GamingRoom,
        NurseOffice,
        SecurityRoom,
        ClubRoom,
        Gym,
        BasketballCourt,
        SoccerField,
        SwimmingPool,
        Track,
        TennisCourt,
        SportsComplex,
        Playground,
        Garden,
        PracticeField,
        PicnicArea,
        Tree,
        FlowerBed,
        Bench,
        Fountain,
        Statue,
        // Each specialization's signature building
        ScholarsHall,
        Arena,
        Clubhouse,
        Theatre,
        InnovationLab,
        CommunityCentre,
        InternationalHouse,
        Manor,
    }

    public enum RoomCategory
    {
        Academic,
        Residential,
        Dining,
        StudentLife,
        Staff,
        Health,
        Sports,
        Outdoors,
        Decor,
    }

    public enum FoodTier
    {
        Budget,
        Standard,
        Good,
        Premium,
    }

    public enum CurfewPolicy
    {
        Strict,
        Normal,
        Relaxed,
    }

    public enum DisciplinePolicy
    {
        Lenient,
        Balanced,
        Strict,
    }

    public enum TrainingIntensity
    {
        Light,
        Normal,
        Intense,
    }

    public enum TrainingProgram
    {
        Workshop,
        Seminar,
        Certification,
    }

    public enum ScholarshipType
    {
        None,
        Academic,
        Athletic,
        Arts,
        FinancialAid,
    }

    public enum SportType
    {
        None,
        Basketball,
        Soccer,
        Swimming,
        Track,
        Tennis,
    }

    public enum ClubType
    {
        Robotics,
        Chess,
        Art,
        Drama,
        Music,
        Debate,
        Gaming,
        Science,
        StudentGovernment,
    }

    public enum Specialization
    {
        None,
        AcademicAcademy,
        SportsAcademy,
        LuxuryBoarding,
        ArtsAcademy,
        TechnologySchool,
        CommunitySchool,
    }

    public enum Feature
    {
        Clubs,
        Administrators,
        Scholarships,
        AdvancedDorms,
        Specializations,
        SportsPrograms,
        InternationalStudents,
        MajorSponsorships,
        AdvancedDepartments,
        LargeCompetitions,
        SecondCampus,
        EliteInstitution,
        Endowment,
    }

    public enum SenderType
    {
        Parent,
        Student,
        Teacher,
        Staff,
        Admissions,
        Finance,
        Sports,
        Inspector,
        Sponsor,
        Government,
        RivalSchool,
        StudentAffairs,
        Facilities,
        Alumni,
        Assistant,
        Press,
    }

    /// <summary>The director's assistant's personality: shapes perks, quirks, advice and voice.</summary>
    public enum AssistantStyle
    {
        Cheerful,
        Strict,
        Thrifty,
        Charming,
        Bookish,
        Handy,
    }

    /// <summary>Trouble the assistant watches for and brings to the inbox.</summary>
    public enum ProblemKind
    {
        Cash,
        Beds,
        Bathrooms,
        Seats,
        Teachers,
        Safety,
        Food,
        Dirty,
        Repairs,
        Unhappy,
        StaffMorale,
        Upgrade,
    }

    /// <summary>
    /// Parts of the game that stay hidden until the tutorial, the school's level or an event
    /// brings them in, so a new school starts simple. School systems gated by level use <see cref="Feature"/>.
    /// </summary>
    public enum Reveal
    {
        Inbox,
        Build,
        Students,
        Staff,
        Money,
        Vitals,
        Rules,
        Stats,
        News,
        Rankings,
        Admissions,
        Land,
        Promises,
    }

    public enum NewsTone
    {
        Good,
        Bad,
        Neutral,
    }

    public enum NewsTopic
    {
        School,
        Academics,
        Sports,
        Safety,
        Food,
        Dorms,
        StudentLife,
        Money,
        Staff,
        Rankings,
        Campus,
        Rivals,
    }

    public enum MessagePriority
    {
        Info,
        Normal,
        Important,
        Urgent,
    }

    public enum LedgerCategory
    {
        // Income
        Tuition,
        DormFees,
        Donations,
        Sponsorship,
        Tickets,
        Merchandise,
        Grants,
        ClubEvents,
        // Expenses
        Salaries,
        Food,
        Upkeep,
        Utilities,
        Construction,
        Training,
        Compensation,
        Fines,
        Bonuses,
        Severance,
        SportsOps,
        ClubOps,
        Other,
        // Both ways: gifts into the endowment go out, its yearly payout comes in
        Endowment,
    }

    public enum UniversityTier
    {
        Workforce,
        College,
        University,
        TopUniversity,
        EliteUniversity,
    }
}
