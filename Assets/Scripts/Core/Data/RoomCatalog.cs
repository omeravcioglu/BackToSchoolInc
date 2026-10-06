using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Static description of a buildable room. Arrays are indexed by level - 1.
    /// Cost[0] is the build cost; Cost[n] is the cost of upgrading to level n + 1.
    /// </summary>
    public sealed class RoomDefinition
    {
        public RoomType Type;
        public string Name;
        public RoomCategory Category;
        public string Description;
        public int Width;                // footprint in tiles; the door is on the south (width) side
        public int Height;
        public bool Outdoor;             // courts and fields: no walls, a gate instead of a door
        public int MinSchoolLevel = 1;
        public float[] Cost;
        public int[] Capacity;           // meaning depends on type: seats, beds, meals, residents served...
        public float[] Quality;          // base quality 0-100 (cafeteria: food bonus)
        public float[] Upkeep;           // monthly
        public int[] BuildDays;
        public string[] LevelNames;
        public float[] MonthlyFee;       // dorm rooms only: per resident per month

        // Levels at or above this need a feature unlocked.
        public int GatedFromLevel = 99;
        public Feature GateFeature;

        public int PrivateBathFromLevel = 99;
        public float CleanlinessDecay = 0.6f;  // per day at full use

        public bool Decor;               // trees, benches, fountains: no door, no name tag, built in a day
        public float Beauty;             // how much it brightens the campus (decor, gardens, playgrounds)

        public Specialization Signature; // a specialization's own building: only a school with that identity builds it
        public bool Buildable = true;    // false for the old manor, which comes with the estate

        public int MaxLevel => Cost.Length;
        public int Area => Width * Height;
        public string CapacityLabel = "Capacity";
    }

    public static class RoomCatalog
    {
        static readonly Dictionary<RoomType, RoomDefinition> Defs = new Dictionary<RoomType, RoomDefinition>();

        public static RoomDefinition Get(RoomType type) => Defs[type];

        public static IEnumerable<RoomDefinition> All => Defs.Values;

        static RoomCatalog()
        {
            Add(new RoomDefinition
            {
                Type = RoomType.Classroom, Name = "Classroom", Category = RoomCategory.Academic, Width = 5, Height = 4,
                Description = "Where teaching happens. Quality multiplies teaching efficiency.",
                Cost = new[] { 12000f, 8000f, 14000f, 24000f, 40000f },
                Capacity = new[] { 12, 16, 20, 24, 30 },
                Quality = new[] { 30f, 48f, 64f, 80f, 94f },
                Upkeep = new[] { 250f, 350f, 500f, 750f, 1100f },
                BuildDays = new[] { 14, 7, 10, 12, 15 },
                LevelNames = new[]
                {
                    "Old desks, chalkboard, dim lighting",
                    "New desks, whiteboard, bright lighting",
                    "Projector and comfortable seating",
                    "Smart board and student computers",
                    "Premium tech: interactive displays, lab kits",
                },
                CapacityLabel = "Students",
                GatedFromLevel = 5, GateFeature = Feature.AdvancedDepartments,
                CleanlinessDecay = 1.0f,
            });

            Add(new RoomDefinition
            {
                Type = RoomType.DormRoom, Name = "Dorm Room", Category = RoomCategory.Residential, Width = 4, Height = 3,
                Description = "Beds for boarders. Better rooms hold fewer students but charge far more.",
                Cost = new[] { 15000f, 10000f, 18000f, 30000f, 50000f },
                Capacity = new[] { 6, 4, 4, 2, 2 },
                Quality = new[] { 25f, 45f, 62f, 80f, 95f },
                Upkeep = new[] { 200f, 300f, 450f, 700f, 1000f },
                BuildDays = new[] { 14, 6, 8, 10, 12 },
                MonthlyFee = new[] { 400f, 600f, 850f, 1300f, 2000f },
                LevelNames = new[]
                {
                    "Bunk beds for 6, shared bathroom",
                    "4 proper beds, desks, shared bathroom",
                    "4 beds, air conditioning, storage",
                    "2 beds, private bathroom, TV",
                    "Luxury: gaming PCs, mini-fridge, decor",
                },
                CapacityLabel = "Beds",
                GatedFromLevel = 3, GateFeature = Feature.AdvancedDorms,
                PrivateBathFromLevel = 4,
                CleanlinessDecay = 1.2f,
            });

            Add(new RoomDefinition
            {
                Type = RoomType.Cafeteria, Name = "Cafeteria", Category = RoomCategory.Dining, Width = 8, Height = 5,
                Description = "Feeds everyone. Too little capacity means long lines and cold food.",
                Cost = new[] { 20000f, 12000f, 20000f, 35000f, 60000f },
                Capacity = new[] { 20, 45, 90, 160, 300 },
                Quality = new[] { 0f, 4f, 8f, 12f, 16f },
                Upkeep = new[] { 300f, 450f, 700f, 1100f, 1700f },
                BuildDays = new[] { 20, 8, 10, 14, 18 },
                LevelNames = new[]
                {
                    "Small kitchen, folding tables",
                    "Better refrigerators, more seating",
                    "Industrial ovens and a salad bar",
                    "Large kitchen and dessert station",
                    "Food hall with multiple stations",
                },
                CapacityLabel = "Meals per sitting",
                CleanlinessDecay = 2.5f,
            });

            Add(new RoomDefinition
            {
                Type = RoomType.Bathroom, Name = "Shared Bathroom", Category = RoomCategory.Residential, Width = 3, Height = 3,
                Description = "Serves residents of dorm rooms without private bathrooms.",
                Cost = new[] { 8000f, 6000f, 10000f },
                Capacity = new[] { 10, 16, 24 },
                Quality = new[] { 35f, 60f, 85f },
                Upkeep = new[] { 120f, 180f, 260f },
                BuildDays = new[] { 8, 5, 6 },
                LevelNames = new[] { "Basic stalls and showers", "Renovated with more showers", "Modern and spacious" },
                CapacityLabel = "Residents served",
                CleanlinessDecay = 2.5f,
            });

            Add(new RoomDefinition
            {
                Type = RoomType.LaundryRoom, Name = "Laundry Room", Category = RoomCategory.Residential, Width = 3, Height = 3,
                Description = "Small comfort that boarders notice when it is missing.",
                Cost = new[] { 6000f, 5000f, 9000f },
                Capacity = new[] { 30, 60, 120 },
                Quality = new[] { 40f, 65f, 85f },
                Upkeep = new[] { 100f, 150f, 220f },
                BuildDays = new[] { 6, 4, 5 },
                LevelNames = new[] { "A few old machines", "Modern washers and dryers", "Laundry service desk" },
                CapacityLabel = "Residents served",
            });

            Add(new RoomDefinition
            {
                Type = RoomType.Library, Name = "Library", Category = RoomCategory.Academic, Width = 6, Height = 5,
                Description = "Boosts education quality. Bookworms love it.",
                MinSchoolLevel = 2,
                Cost = new[] { 18000f, 12000f, 22000f, 40000f },
                Capacity = new[] { 40, 80, 160, 300 },
                Quality = new[] { 40f, 60f, 78f, 92f },
                Upkeep = new[] { 250f, 400f, 600f, 900f },
                BuildDays = new[] { 14, 8, 10, 14 },
                LevelNames = new[] { "Donated books, a few tables", "Study carrels and a real catalogue", "Digital archive and quiet rooms", "Research library" },
                CapacityLabel = "Students served",
            });

            Add(new RoomDefinition
            {
                Type = RoomType.ScienceLab, Name = "Science Lab", Category = RoomCategory.Academic, Width = 5, Height = 5,
                Description = "Improves science teaching and enables the Science Club.",
                MinSchoolLevel = 5,
                Cost = new[] { 25000f, 18000f, 30000f, 50000f },
                Capacity = new[] { 20, 30, 45, 60 },
                Quality = new[] { 45f, 62f, 80f, 95f },
                Upkeep = new[] { 400f, 600f, 900f, 1300f },
                BuildDays = new[] { 18, 10, 12, 16 },
                LevelNames = new[] { "Basic benches and burners", "Fume hood, microscopes", "Modern lab equipment", "University-grade research lab" },
                CapacityLabel = "Students served",
                GatedFromLevel = 4, GateFeature = Feature.AdvancedDepartments,
            });

            Add(new RoomDefinition
            {
                Type = RoomType.ComputerLab, Name = "Computer Lab", Category = RoomCategory.Academic, Width = 5, Height = 5,
                Description = "Improves technology teaching and enables the Robotics Club.",
                MinSchoolLevel = 4,
                Cost = new[] { 22000f, 15000f, 26000f, 45000f },
                Capacity = new[] { 20, 30, 45, 60 },
                Quality = new[] { 45f, 62f, 80f, 95f },
                Upkeep = new[] { 350f, 500f, 800f, 1200f },
                BuildDays = new[] { 14, 8, 10, 14 },
                LevelNames = new[] { "Second-hand PCs", "New workstations", "Fast machines and 3D printers", "Robotics and AI lab" },
                CapacityLabel = "Students served",
                GatedFromLevel = 4, GateFeature = Feature.AdvancedDepartments,
            });

            Add(new RoomDefinition
            {
                Type = RoomType.Office, Name = "Administration Office", Category = RoomCategory.Staff, Width = 3, Height = 3,
                Description = "Desks for administrators. Each level adds room for more directors.",
                Cost = new[] { 8000f, 8000f, 15000f },
                Capacity = new[] { 2, 4, 8 },
                Quality = new[] { 40f, 65f, 85f },
                Upkeep = new[] { 150f, 250f, 400f },
                BuildDays = new[] { 8, 6, 8 },
                LevelNames = new[] { "Cramped desks", "Proper offices", "Executive suite" },
                CapacityLabel = "Administrator desks",
            });

            Add(new RoomDefinition
            {
                Type = RoomType.StaffLounge, Name = "Staff Lounge", Category = RoomCategory.Staff, Width = 4, Height = 4,
                Description = "Staff recover energy and morale faster.",
                MinSchoolLevel = 2,
                Cost = new[] { 7000f, 6000f, 12000f },
                Capacity = new[] { 10, 25, 60 },
                Quality = new[] { 40f, 65f, 88f },
                Upkeep = new[] { 100f, 180f, 300f },
                BuildDays = new[] { 7, 5, 6 },
                LevelNames = new[] { "Coffee machine and a sofa", "Kitchenette and quiet corner", "Premium lounge" },
                CapacityLabel = "Staff served",
            });

            Add(new RoomDefinition
            {
                Type = RoomType.StudentLounge, Name = "Student Lounge", Category = RoomCategory.StudentLife, Width = 5, Height = 4,
                Description = "Social space. Raises student life and lowers stress.",
                Cost = new[] { 9000f, 8000f, 15000f },
                Capacity = new[] { 30, 70, 150 },
                Quality = new[] { 40f, 65f, 88f },
                Upkeep = new[] { 150f, 250f, 400f },
                BuildDays = new[] { 8, 6, 8 },
                LevelNames = new[] { "Sofas and a TV", "Games tables and snacks", "Café-style commons" },
                CapacityLabel = "Students served",
            });

            Add(new RoomDefinition
            {
                Type = RoomType.GamingRoom, Name = "Gaming Room", Category = RoomCategory.StudentLife, Width = 4, Height = 4,
                Description = "Very popular. Lazy students may let their grades slip.",
                MinSchoolLevel = 3,
                Cost = new[] { 14000f, 10000f, 20000f },
                Capacity = new[] { 15, 30, 60 },
                Quality = new[] { 50f, 70f, 90f },
                Upkeep = new[] { 250f, 400f, 600f },
                BuildDays = new[] { 8, 6, 8 },
                LevelNames = new[] { "A few consoles", "Gaming PCs and VR", "Esports arena" },
                CapacityLabel = "Students served",
            });

            Add(new RoomDefinition
            {
                Type = RoomType.NurseOffice, Name = "Nurse Office", Category = RoomCategory.Health, Width = 3, Height = 3,
                Description = "With a nurse on staff: fewer illnesses, lower stress, better inspections.",
                MinSchoolLevel = 2,
                Cost = new[] { 10000f, 8000f, 16000f },
                Capacity = new[] { 60, 150, 400 },
                Quality = new[] { 45f, 65f, 88f },
                Upkeep = new[] { 150f, 250f, 400f },
                BuildDays = new[] { 8, 6, 8 },
                LevelNames = new[] { "First-aid room", "Clinic room with beds", "Full health center" },
                CapacityLabel = "Students covered",
            });

            Add(new RoomDefinition
            {
                Type = RoomType.SecurityRoom, Name = "Security Room", Category = RoomCategory.Health, Width = 3, Height = 3,
                Description = "Cameras and a base for guards. Improves safety and fire-safety scores.",
                MinSchoolLevel = 2,
                Cost = new[] { 9000f, 8000f, 16000f },
                Capacity = new[] { 80, 200, 500 },
                Quality = new[] { 45f, 65f, 88f },
                Upkeep = new[] { 150f, 250f, 400f },
                BuildDays = new[] { 8, 6, 8 },
                LevelNames = new[] { "Guard desk", "CCTV coverage", "Integrated security center" },
                CapacityLabel = "Students covered",
            });

            Add(new RoomDefinition
            {
                Type = RoomType.ClubRoom, Name = "Club Room", Category = RoomCategory.StudentLife, Width = 4, Height = 4,
                Description = "Hosts clubs. Each level hosts one more club.",
                MinSchoolLevel = Balance.UnlockLevel(Feature.Clubs),
                Cost = new[] { 8000f, 7000f, 14000f },
                Capacity = new[] { 1, 2, 3 },
                Quality = new[] { 40f, 65f, 88f },
                Upkeep = new[] { 120f, 200f, 320f },
                BuildDays = new[] { 7, 5, 6 },
                LevelNames = new[] { "Spare room with a whiteboard", "Equipped activity room", "Multi-club studio" },
                CapacityLabel = "Clubs hosted",
            });

            Add(Sports(RoomType.Gym, "Gym", 8, 6, false, "Indoor fitness and basketball. Boosts athletic students.",
                new[] { 30000f, 22000f, 40000f }, new[] { 150, 400, 1200 }, new[] { 400f, 650f, 1000f },
                new[] { "Basic gym hall", "Gym with weights room", "Performance center with arena seating" }, 4));
            Add(Sports(RoomType.BasketballCourt, "Basketball Court", 7, 5, true, "Home court for the basketball team.",
                new[] { 12000f, 10000f, 25000f }, new[] { 100, 300, 1200 }, new[] { 150f, 300f, 800f },
                new[] { "Outdoor asphalt court", "Covered court with bleachers", "Indoor arena" }, 4));
            Add(Sports(RoomType.SoccerField, "Soccer Field", 12, 8, true, "Home field for the soccer team.",
                new[] { 25000f, 20000f, 60000f }, new[] { 200, 800, 3000 }, new[] { 300f, 600f, 1500f },
                new[] { "Grass field", "Floodlit field with stands", "Stadium" }, 6));
            Add(Sports(RoomType.SwimmingPool, "Swimming Pool", 8, 7, true, "Required for the swim team. Expensive to run.",
                new[] { 60000f, 40000f, 90000f }, new[] { 100, 300, 1000 }, new[] { 900f, 1400f, 2200f },
                new[] { "Outdoor pool", "Heated indoor pool", "Olympic aquatics center" }, 7));
            Add(Sports(RoomType.Track, "Running Track", 12, 8, true, "Required for the track team.",
                new[] { 20000f, 18000f, 45000f }, new[] { 150, 500, 2000 }, new[] { 200f, 400f, 900f },
                new[] { "Cinder track", "Rubber track with stands", "Competition athletics venue" }, 6));
            Add(Sports(RoomType.TennisCourt, "Tennis Courts", 6, 5, true, "Required for the tennis team.",
                new[] { 15000f, 12000f, 30000f }, new[] { 60, 200, 800 }, new[] { 150f, 300f, 700f },
                new[] { "Two hard courts", "Four courts with seating", "Tennis center" }, 5));

            Add(new RoomDefinition
            {
                Type = RoomType.SportsComplex, Name = "Sports Complex", Category = RoomCategory.Sports, Width = 12, Height = 9,
                Description = "Everything under one roof: a sports hall, a pool and a running lane. Home ground for "
                              + "basketball, swimming, track and tennis, and a big lift for sports and student life.",
                MinSchoolLevel = Balance.UnlockLevel(Feature.Specializations),
                Cost = new[] { 150000f, 90000f, 160000f },
                Capacity = new[] { 400, 1500, 4000 },
                Quality = new[] { 60f, 78f, 95f },
                Upkeep = new[] { 1600f, 2400f, 3600f },
                BuildDays = new[] { 30, 18, 28 },
                LevelNames = new[]
                {
                    "Sports hall, pool and changing rooms",
                    "Climbing wall, dance studio and a fitness suite",
                    "Olympic-class complex with arena seating",
                },
                CapacityLabel = "Spectators",
                CleanlinessDecay = 1.2f,
            });

            var practice = Sports(RoomType.PracticeField, "Practice Field", 9, 6, true,
                "Open grass for PE, kickabouts and team practice. Students grow sportier and happier outdoors.",
                new[] { 7000f, 6000f, 14000f }, new[] { 40, 120, 300 }, new[] { 60f, 110f, 200f },
                new[] { "Mown grass and a pair of goals", "Lined pitch, cones and a kit shed", "Floodlit training ground" }, 2);
            practice.BuildDays = new[] { 6, 5, 8 };
            practice.CapacityLabel = "Players";
            practice.Beauty = 0.5f;
            Add(practice);

            Add(new RoomDefinition
            {
                Type = RoomType.Playground, Name = "Playground", Category = RoomCategory.Outdoors, Width = 5, Height = 4, Outdoor = true,
                Description = "Swings and slides. Younger students love it, and everyone blows off steam.",
                Cost = new[] { 4000f, 3500f, 7000f },
                Capacity = new[] { 20, 45, 90 },
                Quality = new[] { 40f, 65f, 88f },
                Upkeep = new[] { 40f, 70f, 120f },
                BuildDays = new[] { 5, 4, 6 },
                LevelNames = new[] { "Swings, a slide and a sandpit", "Climbing frame and a roundabout", "Adventure playground with a zip line" },
                CapacityLabel = "Kids at play",
                CleanlinessDecay = 0.5f,
                Beauty = 1f,
            });

            Add(new RoomDefinition
            {
                Type = RoomType.Garden, Name = "Garden", Category = RoomCategory.Outdoors, Width = 4, Height = 4, Outdoor = true,
                Description = "A quiet green corner. Lowers stress and makes the campus lovelier for students and visiting families.",
                Cost = new[] { 3000f, 2500f, 5500f },
                Capacity = new[] { 12, 28, 60 },
                Quality = new[] { 45f, 68f, 90f },
                Upkeep = new[] { 40f, 70f, 110f },
                BuildDays = new[] { 4, 4, 6 },
                LevelNames = new[] { "Flower beds and a bench", "A pond, shady trees and stepping stones", "Botanical garden with a greenhouse" },
                CapacityLabel = "Students relaxing",
                CleanlinessDecay = 0.3f,
                Beauty = 4f,
            });

            Add(new RoomDefinition
            {
                Type = RoomType.PicnicArea, Name = "Picnic Area", Category = RoomCategory.Outdoors, Width = 5, Height = 3, Outdoor = true,
                Description = "Tables on the lawn for lunch in the sun and chatting after class.",
                Cost = new[] { 2500f, 2000f, 4500f },
                Capacity = new[] { 16, 32, 60 },
                Quality = new[] { 40f, 62f, 85f },
                Upkeep = new[] { 25f, 40f, 70f },
                BuildDays = new[] { 3, 3, 4 },
                LevelNames = new[] { "Picnic tables on the lawn", "Tables with sun umbrellas", "Outdoor café terrace" },
                CapacityLabel = "Seats",
                CleanlinessDecay = 0.8f,
                Beauty = 1f,
            });

            Add(Decoration(RoomType.Tree, "Tree", 1, 1, 600f, 5f, 2f, 1, "A young oak: shade, birdsong and a little charm."));
            Add(Decoration(RoomType.FlowerBed, "Flower Bed", 2, 1, 300f, 8f, 1.5f, 1, "A splash of colour along a path."));
            Add(Decoration(RoomType.Bench, "Bench", 2, 1, 250f, 2f, 0.6f, 1, "A spot to sit, chat and people-watch."));
            Add(Decoration(RoomType.Fountain, "Fountain", 2, 2, 6000f, 60f, 8f, 3, "The heart of a proper campus. Visitors always stop here."));
            Add(Decoration(RoomType.Statue, "Founder's Statue", 2, 2, 12000f, 20f, 7f, 5, "The founder in bronze, pointing at the future."));

            // Signature buildings: one per specialization. What they do lives in SpecializationRules.
            Add(Signature(RoomType.ScholarsHall, Specialization.AcademicAcademy, "Scholars' Hall", RoomCategory.Academic, 10, 7,
                "A lecture theatre, a research library and tutors on hand: better teaching, calmer exams, graduates at better universities and a summer school that pays.",
                new[] { 160000f, 120000f, 200000f }, new[] { 120, 240, 400 }, "Seats", new[] { 1800f, 2800f, 4200f },
                new[] { "Lecture theatre and a reading room", "Research library and tutoring rooms", "Observatory dome and visiting professors" }));
            Add(Signature(RoomType.Arena, Specialization.SportsAcademy, "Arena", RoomCategory.Sports, 11, 8,
                "Tiered seats round a sprung court, a physio suite and a strength lab: stronger teams, fewer injuries and big crowds on event nights.",
                new[] { 180000f, 130000f, 220000f }, new[] { 1500, 3500, 8000 }, "Spectators", new[] { 2200f, 3200f, 4800f },
                new[] { "Sprung court with stands on two sides", "Physio suite, strength lab and a scoreboard", "Champions' arena with a roof that opens" }));
            Add(Signature(RoomType.Clubhouse, Specialization.LuxuryBoarding, "Clubhouse", RoomCategory.Residential, 9, 7,
                "A residents' club with a spa, a concierge and fine dining: boarders live better, eat better, and families happily pay more for it.",
                new[] { 150000f, 120000f, 200000f }, new[] { 80, 160, 300 }, "Members", new[] { 2000f, 3000f, 4500f },
                new[] { "Lounge, concierge desk and a terrace", "Spa, sauna and a fine-dining room", "Members' club with a rooftop pool" }));
            Add(Signature(RoomType.Theatre, Specialization.ArtsAcademy, "Theatre", RoomCategory.StudentLife, 10, 8,
                "A stage, rehearsal studios and a gallery: student life blossoms, the arts clubs win more and the shows sell tickets all year.",
                new[] { 150000f, 110000f, 190000f }, new[] { 250, 500, 900 }, "Seats", new[] { 1800f, 2700f, 4000f },
                new[] { "Black-box stage and a rehearsal studio", "Proper stage, orchestra pit and a gallery", "Opera house with a fly tower" }));
            Add(Signature(RoomType.InnovationLab, Specialization.TechnologySchool, "Innovation Lab", RoomCategory.Academic, 9, 7,
                "A makerspace with 3D printers and a robotics arena: better teaching, winning tech clubs and companies paying to work with the students.",
                new[] { 160000f, 120000f, 200000f }, new[] { 40, 80, 150 }, "Makers", new[] { 2000f, 3000f, 4500f },
                new[] { "Makerspace with 3D printers", "Robotics arena and a server room", "Start-up incubator with industry partners" }));
            Add(Signature(RoomType.CommunityCentre, Specialization.CommunitySchool, "Community Centre", RoomCategory.StudentLife, 10, 7,
                "A hall for the whole town with a food bank and after-school clubs: more trust, safer streets, more families applying and a grant from the council.",
                new[] { 130000f, 100000f, 170000f }, new[] { 300, 700, 1500 }, "Visitors a week", new[] { 1500f, 2300f, 3500f },
                new[] { "Town hall and a food bank", "After-school clubs and a cafe", "Community campus with a public library" }));

            Add(new RoomDefinition
            {
                Type = RoomType.InternationalHouse, Name = "International House", Category = RoomCategory.StudentLife, Width = 6, Height = 5,
                Description = "A home from home for students from abroad: a kitchen from home, a world map and a language corner. Homesickness fades much faster.",
                MinSchoolLevel = Balance.UnlockLevel(Feature.InternationalStudents),
                Cost = new[] { 40000f, 30000f, 55000f },
                Capacity = new[] { 15, 35, 80 },
                Quality = new[] { 50f, 72f, 92f },
                Upkeep = new[] { 400f, 700f, 1100f },
                BuildDays = new[] { 18, 10, 14 },
                LevelNames = new[] { "A common room and a kitchen from home", "Language corner and a festival calendar", "Global centre with rooms for visiting families" },
                CapacityLabel = "Students from abroad",
            });

            Add(new RoomDefinition
            {
                Type = RoomType.Manor, Name = "Old Manor", Category = RoomCategory.StudentLife, Width = 10, Height = 7,
                Description = "The old estate's manor house. Restored, its great hall hosts dinners, concerts and visiting families, and the whole school feels grander.",
                MinSchoolLevel = Balance.UnlockLevel(Feature.SecondCampus),
                Buildable = false,
                Cost = new[] { 0f, 220000f, 380000f },
                Capacity = new[] { 80, 200, 400 },
                Quality = new[] { 35f, 75f, 95f },
                Upkeep = new[] { 1200f, 2600f, 4200f },
                BuildDays = new[] { 1, 40, 50 },
                LevelNames = new[] { "Dusty halls under a leaking roof", "Restored great hall and library", "The manor in its glory: ballroom, gallery and gardens" },
                CapacityLabel = "Guests",
                Beauty = 8f,
            });
        }

        /// <summary>A specialization's signature building: big, dear to run, three levels, one design per identity.</summary>
        static RoomDefinition Signature(RoomType type, Specialization spec, string name, RoomCategory category, int width, int height,
            string desc, float[] cost, int[] capacity, string capacityLabel, float[] upkeep, string[] levels)
        {
            return new RoomDefinition
            {
                Type = type, Name = name, Category = category, Width = width, Height = height, Description = desc,
                Signature = spec, MinSchoolLevel = Balance.UnlockLevel(Feature.Specializations),
                Cost = cost, Capacity = capacity, CapacityLabel = capacityLabel, Upkeep = upkeep, LevelNames = levels,
                Quality = new[] { 62f, 80f, 96f },
                BuildDays = new[] { 40, 25, 35 },
                CleanlinessDecay = 0.9f,
            };
        }

        /// <summary>Small things that make the campus lovely: no door, one level, ready the next day.</summary>
        static RoomDefinition Decoration(RoomType type, string name, int width, int height, float cost, float upkeep, float beauty,
            int minLevel, string desc)
        {
            return new RoomDefinition
            {
                Type = type, Name = name, Category = RoomCategory.Decor, Width = width, Height = height, Outdoor = true, Decor = true,
                Description = desc, MinSchoolLevel = minLevel,
                Cost = new[] { cost }, Capacity = new[] { type == RoomType.Bench ? 2 : 0 }, Quality = new[] { 70f },
                Upkeep = new[] { upkeep }, BuildDays = new[] { 1 }, LevelNames = new[] { desc },
                CapacityLabel = "Seats", CleanlinessDecay = 0.05f, Beauty = beauty,
            };
        }

        static RoomDefinition Sports(RoomType type, string name, int width, int height, bool outdoor, string desc,
            float[] cost, int[] spectators, float[] upkeep, string[] levels, int minLevel)
        {
            return new RoomDefinition
            {
                Type = type, Name = name, Category = RoomCategory.Sports, Width = width, Height = height, Outdoor = outdoor, Description = desc,
                Cost = cost, Capacity = spectators, Upkeep = upkeep, LevelNames = levels, MinSchoolLevel = minLevel,
                Quality = new[] { 40f, 65f, 90f },
                BuildDays = new[] { 20, 12, 25 },
                CapacityLabel = "Spectators",
                CleanlinessDecay = 0.8f,
            };
        }

        static void Add(RoomDefinition def) => Defs[def.Type] = def;

        /// <summary>Which facility a sport's team plays in.</summary>
        public static bool SupportsSport(RoomType type, SportType sport) => sport switch
        {
            SportType.Basketball => type == RoomType.Gym || type == RoomType.BasketballCourt || type == RoomType.SportsComplex || type == RoomType.Arena,
            SportType.Soccer => type == RoomType.SoccerField,
            SportType.Swimming => type == RoomType.SwimmingPool || type == RoomType.SportsComplex,
            SportType.Track => type == RoomType.Track || type == RoomType.SportsComplex || type == RoomType.Arena,
            SportType.Tennis => type == RoomType.TennisCourt || type == RoomType.SportsComplex || type == RoomType.Arena,
            _ => false,
        };
    }
}
