namespace SchoolSim
{
    public static class Names
    {
        static readonly string[] First =
        {
            "Aiden", "Maya", "Leo", "Zara", "Noah", "Ivy", "Omar", "Elena", "Kai", "Nora", "Ethan", "Lina",
            "Mateo", "Aria", "Yusuf", "Chloe", "Ravi", "Sofia", "Theo", "Hana", "Lucas", "Amara", "Finn", "Leila",
            "Jonah", "Mia", "Arjun", "Clara", "Diego", "Ada", "Felix", "Nadia", "Hugo", "Emre", "Priya", "Sami",
            "Oscar", "Selin", "Milo", "Yara", "Ezra", "Luna", "Tomas", "Iris", "Kenji", "Freya", "Malik", "Elif",
            "Caleb", "Rosa", "Idris", "June", "Rafael", "Ines", "Levi", "Mei", "Adam", "Talia", "Jasper", "Deniz",
        };

        static readonly string[] Last =
        {
            "Carter", "Nguyen", "Okafor", "Silva", "Kowalski", "Haddad", "Tanaka", "Moreau", "Yilmaz", "Brennan",
            "Patel", "Lindqvist", "Garcia", "Novak", "Mensah", "Rossi", "Kim", "Andersen", "Farouk", "Duarte",
            "Whitfield", "Sato", "Osei", "Varga", "Castillo", "Demir", "Fischer", "Abara", "Ivanova", "Laurent",
            "Hughes", "Reyes", "Petrov", "Achebe", "Mori", "Keller", "Santos", "Ward", "Aydin", "Quinn",
        };

        static readonly string[] SchoolPrefix =
        {
            "St. Aldric's", "Northgate", "Riverside", "Harrow Hill", "Westbrook", "Kingsmere", "Oakridge",
            "Silverlake", "Ashford", "Brighton Vale", "Crestwood", "Eastmoor", "Fairhaven", "Glenmore",
        };

        static readonly string[] SchoolSuffix = { "Academy", "College", "School", "Preparatory", "Institute", "High" };

        static readonly string[] Countries =
        {
            "Brazil", "Japan", "Nigeria", "Germany", "India", "Korea", "Mexico", "Kenya", "France", "Vietnam",
            "Spain", "Egypt", "Canada", "Turkey", "Ghana", "Chile", "Italy", "Poland", "Norway", "Thailand",
        };

        static readonly string[] Companies =
        {
            "Nimbus Energy", "Brightpath Bank", "Volt Sports", "Orchard Foods", "Helix Tech", "Summit Motors",
            "Blue Harbor Insurance", "Kinetic Drinks", "Atlas Telecom", "Evergreen Realty",
        };

        public static string FirstName(SimRandom rng) => rng.Pick(First);
        public static string LastName(SimRandom rng) => rng.Pick(Last);
        public static string Person(SimRandom rng) => FirstName(rng) + " " + LastName(rng);
        public static string Company(SimRandom rng) => rng.Pick(Companies);
        public static string Country(SimRandom rng) => rng.Pick(Countries);
        public static string School(SimRandom rng) => rng.Pick(SchoolPrefix) + " " + rng.Pick(SchoolSuffix);

        public static string Parent(SimRandom rng, string lastName)
        {
            return (rng.Chance(0.5f) ? "Mr. " : "Ms. ") + lastName;
        }

        public static string AdminRoleTitle(AdminRole role) => role switch
        {
            AdminRole.HRManager => "HR Manager",
            AdminRole.AdmissionsDirector => "Admissions Director",
            AdminRole.DormDirector => "Dorm Director",
            AdminRole.AthleticDirector => "Athletic Director",
            AdminRole.FinancialDirector => "Financial Director",
            AdminRole.StudentAffairsDirector => "Student Affairs Director",
            _ => "Administrator",
        };

        public static string Tier(UniversityTier tier) => tier switch
        {
            UniversityTier.EliteUniversity => "an elite university",
            UniversityTier.TopUniversity => "a top university",
            UniversityTier.University => "university",
            UniversityTier.College => "college",
            _ => "the workforce",
        };

        public static string Specialization(Specialization s) => s switch
        {
            SchoolSim.Specialization.AcademicAcademy => "Academic Academy",
            SchoolSim.Specialization.SportsAcademy => "Sports Academy",
            SchoolSim.Specialization.LuxuryBoarding => "Luxury Boarding School",
            SchoolSim.Specialization.ArtsAcademy => "Arts Academy",
            SchoolSim.Specialization.TechnologySchool => "Technology School",
            SchoolSim.Specialization.CommunitySchool => "Community School",
            _ => "General School",
        };
    }
}
