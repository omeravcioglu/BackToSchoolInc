using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Facts derived from rooms and staff, recomputed each day. Students, staff and stats all
    /// read from here so every system sees the same picture of the school.
    /// </summary>
    public sealed class SchoolIndices
    {
        public int Students;
        public int Teachers;
        public float TeacherQuality;
        public float StudentsPerTeacher;
        public float TeacherWarmth;          // -1..1: funny/popular vs strict/difficult
        public float StrictShare;            // 0..1
        public bool MotivationalTeacher;
        public int DistinctSubjects;

        public float ClassroomQuality;
        public int ClassroomSeats;
        public float LibraryBonus;
        public float LabBonus;
        public float ScienceLabQuality;
        public float ComputerLabQuality;
        public bool HasLibrary;

        public int Beds;
        public int FreeBeds;
        public float BathroomShortage;       // 0..1
        public float BathroomQuality;
        public float LaundryCoverage;        // 0..1

        public float MealCapacity;
        public float CafeteriaBonus;
        public float CafeteriaCleanliness;
        public bool HasCafeteria;

        public float ChefCoverage;
        public float CustodianCoverage;
        public float GuardCoverage;
        public float NurseCoverage;
        public float CounselorCoverage;

        public float LoungeScore;            // 0..1, quality x coverage
        public float GamingScore;
        public float StaffLoungeQuality;     // 0 when none
        public float NurseOfficeScore;
        public float SecurityRoomScore;
        public int SportsFacilities;
        public float PlayScore;              // 0..1, playgrounds and practice fields
        public float GardenScore;            // 0..1
        public float PicnicScore;            // 0..1
        public float Beauty;                 // 0..1, decorations, gardens and the like for the school's size
        public float ComplexQuality;          // best sports complex's quality, 0 when none
        public float Signature;              // 0..1, the specialization's own building: its level, cleanliness and repair
        public SignatureBuilding SignatureBuilding;   // what that building does, null when there's none
        public float Manor;                  // 0..1, the old manor on the estate: restored and kept up
        public int InternationalStudents;
        public float InternationalSupport;   // 0..1, International House quality x room for everyone from abroad
        public float LanguageSupport;        // how much language teachers speed up settling in
        public float AvgRoomCondition;
        public float AvgCleanliness;
        public float AvgGrades;
        public float AvgBehavior;
        public int ActiveClubs;

        public readonly Dictionary<int, float> DormRoomQuality = new Dictionary<int, float>();
        public readonly Dictionary<int, List<Student>> Residents = new Dictionary<int, List<Student>>();

        public List<Student> ResidentsOf(int roomId)
        {
            return Residents.TryGetValue(roomId, out var list) ? list : Empty;
        }

        /// <summary>One of the signature building's effects at the strength it has today (0 without one).</summary>
        public float SignatureEffect(Func<SignatureBuilding, float> effect) => SignatureBuilding == null ? 0f : effect(SignatureBuilding) * Signature;

        static readonly List<Student> Empty = new List<Student>();
    }

    public sealed class StatSystem
    {
        readonly Simulation _sim;

        public readonly Breakdown[] StatFactors = new Breakdown[GameState.StatCount];
        public readonly Breakdown[] ReputationFactors = new Breakdown[GameState.ReputationCount];
        public readonly float[] ReputationTargets = new float[GameState.ReputationCount];

        const float CategoryDrift = 0.012f;
        const float OverallDrift = 0.008f;

        public StatSystem(Simulation sim)
        {
            _sim = sim;
            for (int i = 0; i < StatFactors.Length; i++) StatFactors[i] = new Breakdown();
            for (int i = 0; i < ReputationFactors.Length; i++) ReputationFactors[i] = new Breakdown();
        }

        GameState S => _sim.State;
        SchoolIndices Ix => _sim.Indices;

        // ------------------------------------------------------------------------------
        // Indices
        // ------------------------------------------------------------------------------

        public void ComputeIndices()
        {
            int day = S.Day;
            var ix = Ix;
            ix.Students = S.Students.Count;
            float n = Math.Max(1, ix.Students);

            // Residents
            ix.Residents.Clear();
            ix.DormRoomQuality.Clear();
            ix.InternationalStudents = 0;
            foreach (var s in S.Students)
            {
                if (s.International) ix.InternationalStudents++;
                if (s.DormRoomId < 0) continue;
                if (!ix.Residents.TryGetValue(s.DormRoomId, out var list))
                {
                    list = new List<Student>();
                    ix.Residents[s.DormRoomId] = list;
                }
                list.Add(s);
            }

            ix.Beds = 0;
            ix.FreeBeds = 0;
            int sharedBathResidents = 0;
            float bathCap = 0f, bathQ = 0f; int bathCount = 0;
            float laundryCap = 0f;
            float seatQuality = 0f; ix.ClassroomSeats = 0;
            float libCap = 0f, libQ = 0f;
            float sciCap = 0f, sciQ = 0f, compCap = 0f, compQ = 0f;
            ix.MealCapacity = 0f; ix.CafeteriaBonus = 0f; ix.CafeteriaCleanliness = 0f; ix.HasCafeteria = false;
            float loungeCap = 0f, loungeQ = 0f, gamingCap = 0f, gamingQ = 0f;
            float staffLoungeQ = 0f;
            float nurseCap = 0f, nurseQ = 0f, secCap = 0f, secQ = 0f;
            ix.SportsFacilities = 0;
            float playCap = 0f, playQ = 0f, gardenCap = 0f, gardenQ = 0f, picnicCap = 0f, picnicQ = 0f, beauty = 0f, complexQ = 0f;
            float signature = 0f, manor = 0f, houseCap = 0f, houseQ = 0f;
            float condSum = 0f, cleanSum = 0f, sizeSum = 0f;
            int cafCount = 0;

            foreach (var r in S.Rooms)
            {
                if (!r.IsOperational(day)) continue;
                beauty += r.Def.Beauty * (0.4f + 0.6f * r.Condition / 100f);
                if (r.Def.Decor) continue;
                float size = r.Def.Area;
                condSum += r.Condition * size;
                cleanSum += r.Cleanliness * size;
                sizeSum += size;
                if (r.Def.Signature != Specialization.None && r.Def.Signature == S.Specialization)
                    signature = Math.Max(signature, SpecializationRules.SignatureStrength(r.Level) * r.Quality / r.BaseQuality);

                switch (r.Type)
                {
                    case RoomType.DormRoom:
                    {
                        int residents = ix.ResidentsOf(r.Id).Count;
                        ix.Beds += r.Capacity;
                        ix.FreeBeds += Math.Max(0, r.Capacity - residents);
                        ix.DormRoomQuality[r.Id] = r.Quality;
                        if (!r.HasPrivateBath) sharedBathResidents += residents;
                        break;
                    }
                    case RoomType.Bathroom:
                        bathCap += r.Capacity; bathQ += r.Quality; bathCount++;
                        break;
                    case RoomType.LaundryRoom:
                        laundryCap += r.Capacity;
                        break;
                    case RoomType.Classroom:
                        ix.ClassroomSeats += r.Capacity;
                        seatQuality += r.Quality * r.Capacity;
                        break;
                    case RoomType.Library:
                        libCap += r.Capacity; libQ = Math.Max(libQ, r.Quality);
                        break;
                    case RoomType.ScienceLab:
                        sciCap += r.Capacity; sciQ = Math.Max(sciQ, r.Quality);
                        break;
                    case RoomType.ComputerLab:
                        compCap += r.Capacity; compQ = Math.Max(compQ, r.Quality);
                        break;
                    case RoomType.Cafeteria:
                        ix.HasCafeteria = true;
                        ix.MealCapacity += r.Capacity;
                        ix.CafeteriaBonus = Math.Max(ix.CafeteriaBonus, r.BaseQuality);
                        ix.CafeteriaCleanliness += r.Cleanliness;
                        cafCount++;
                        break;
                    case RoomType.StudentLounge:
                        loungeCap += r.Capacity; loungeQ = Math.Max(loungeQ, r.Quality);
                        break;
                    case RoomType.GamingRoom:
                        gamingCap += r.Capacity; gamingQ = Math.Max(gamingQ, r.Quality);
                        break;
                    case RoomType.StaffLounge:
                        staffLoungeQ = Math.Max(staffLoungeQ, r.Quality);
                        break;
                    case RoomType.NurseOffice:
                        nurseCap += r.Capacity; nurseQ = Math.Max(nurseQ, r.Quality);
                        break;
                    case RoomType.SecurityRoom:
                        secCap += r.Capacity; secQ = Math.Max(secQ, r.Quality);
                        break;
                    case RoomType.Playground:
                        playCap += r.Capacity; playQ = Math.Max(playQ, r.Quality);
                        break;
                    case RoomType.PracticeField:
                        playCap += r.Capacity * 0.6f; playQ = Math.Max(playQ, r.Quality * 0.8f);
                        break;
                    case RoomType.Garden:
                        gardenCap += r.Capacity; gardenQ = Math.Max(gardenQ, r.Quality);
                        break;
                    case RoomType.PicnicArea:
                        picnicCap += r.Capacity; picnicQ = Math.Max(picnicQ, r.Quality);
                        break;
                    case RoomType.SportsComplex:
                        complexQ = Math.Max(complexQ, r.Quality);
                        break;
                    case RoomType.InternationalHouse:
                        houseCap += r.Capacity; houseQ = Math.Max(houseQ, r.Quality);
                        break;
                    case RoomType.Manor:
                        manor = Math.Max(manor, Balance.ManorStrength(r.Level) * r.Quality / r.BaseQuality);
                        break;
                }

                if (r.Def.Category == RoomCategory.Sports) ix.SportsFacilities++;
            }

            ix.BathroomShortage = sharedBathResidents == 0 ? 0f : MathUtil.Clamp01(1f - bathCap / sharedBathResidents);
            ix.BathroomQuality = bathCount == 0 ? 0f : bathQ / bathCount;
            ix.LaundryCoverage = ix.Students == 0 ? 1f : MathUtil.Clamp01(laundryCap / n);
            ix.ClassroomQuality = ix.ClassroomSeats == 0 ? 0f : seatQuality / ix.ClassroomSeats;
            ix.HasLibrary = libCap > 0;
            ix.LibraryBonus = libQ * 0.1f * Coverage(libCap, n);
            ix.ScienceLabQuality = sciQ * Coverage(sciCap, n) * SpecializationRules.ScienceLabMultiplier(S.Specialization);
            ix.ComputerLabQuality = compQ * Coverage(compCap, n) * SpecializationRules.ComputerLabMultiplier(S.Specialization);
            ix.LabBonus = ix.ScienceLabQuality * 0.06f + ix.ComputerLabQuality * 0.05f;
            if (cafCount > 0) ix.CafeteriaCleanliness /= cafCount;
            ix.LoungeScore = loungeQ / 100f * Coverage(loungeCap, n);
            ix.GamingScore = gamingQ / 100f * Coverage(gamingCap, n);
            ix.StaffLoungeQuality = staffLoungeQ;
            ix.NurseOfficeScore = nurseQ / 100f * Coverage(nurseCap, n);
            ix.SecurityRoomScore = secQ / 100f * Coverage(secCap, n);
            // Not everyone is outside at once: play spaces cover their share of the school.
            ix.PlayScore = playQ / 100f * Coverage(playCap, n * 0.4f);
            ix.GardenScore = gardenQ / 100f * Coverage(gardenCap, n * 0.25f);
            ix.PicnicScore = picnicQ / 100f * Coverage(picnicCap, n * 0.3f);
            ix.Beauty = MathUtil.Clamp01(beauty / (8f + n * 0.25f));
            ix.ComplexQuality = complexQ;
            ix.Signature = signature;
            ix.SignatureBuilding = signature > 0f ? SpecializationRules.Signature(S.Specialization) : null;
            ix.Manor = manor;
            ix.InternationalSupport = houseQ / 100f * Coverage(houseCap, ix.InternationalStudents);
            ix.AvgRoomCondition = sizeSum > 0 ? condSum / sizeSum : 100f;
            ix.AvgCleanliness = sizeSum > 0 ? cleanSum / sizeSum : 100f;

            // Staff
            int teachers = 0, strict = 0, chefs = 0, custodians = 0, guards = 0, nurses = 0, counselors = 0, language = 0;
            float teacherSum = 0f, warmth = 0f;
            ix.MotivationalTeacher = false;
            var subjects = new HashSet<Subject>();
            foreach (var t in S.Staff)
            {
                if (!t.IsActive(day)) continue;
                switch (t.Role)
                {
                    case StaffRole.Teacher:
                        teachers++;
                        teacherSum += TeacherEffect(t);
                        if (t.Has(StaffTrait.Strict)) { strict++; warmth -= 1f; }
                        if (t.Has(StaffTrait.Difficult)) warmth -= 0.8f;
                        if (t.Has(StaffTrait.Funny)) warmth += 1f;
                        if (t.Has(StaffTrait.Popular)) warmth += 0.7f;
                        if (t.Has(StaffTrait.Motivational)) ix.MotivationalTeacher = true;
                        if (t.Subject == Subject.Language) language++;
                        if (t.Subject != Subject.None) subjects.Add(t.Subject);
                        break;
                    case StaffRole.Chef: chefs++; break;
                    case StaffRole.Custodian: custodians++; break;
                    case StaffRole.Security: guards++; break;
                    case StaffRole.Nurse: nurses++; break;
                    case StaffRole.Counselor: counselors++; break;
                }
            }

            ix.Teachers = teachers;
            ix.TeacherQuality = teachers == 0 ? 0f : teacherSum / teachers;
            ix.StudentsPerTeacher = teachers == 0 ? 99f : ix.Students / (float)teachers;
            ix.TeacherWarmth = teachers == 0 ? 0f : MathUtil.Clamp(warmth / teachers, -1f, 1f);
            ix.StrictShare = teachers == 0 ? 0f : strict / (float)teachers;
            ix.DistinctSubjects = subjects.Count;
            ix.LanguageSupport = Math.Min(0.15f, language * 0.05f);

            ix.ChefCoverage = StaffCoverage(chefs, Balance.StudentsPerChef);
            ix.CustodianCoverage = StaffCoverage(custodians, Balance.StudentsPerCustodian);
            ix.GuardCoverage = StaffCoverage(guards, Balance.StudentsPerGuard);
            ix.NurseCoverage = StaffCoverage(nurses, Balance.StudentsPerNurse);
            ix.CounselorCoverage = StaffCoverage(counselors, Balance.StudentsPerCounselor);

            float grades = 0f, behavior = 0f;
            foreach (var s in S.Students) { grades += s.Grades; behavior += s.Behavior; }
            ix.AvgGrades = ix.Students == 0 ? 50f : grades / ix.Students;
            ix.AvgBehavior = ix.Students == 0 ? 70f : behavior / ix.Students;

            ix.ActiveClubs = 0;
            foreach (var c in S.Clubs) if (c.Members.Count >= 3) ix.ActiveClubs++;
        }

        static float Coverage(float capacity, float students) => students <= 0 ? (capacity > 0 ? 1f : 0f) : MathUtil.Clamp01(capacity / students);

        float StaffCoverage(int count, float perStaff)
        {
            if (Ix.Students == 0) return count > 0 ? 1f : 0f;
            float needed = Math.Max(1f, Ix.Students / perStaff);
            return MathUtil.Clamp01(count / needed);
        }

        public float TeacherEffect(StaffMember t)
        {
            float e = t.Skill * 0.45f + t.ClassroomManagement * 0.2f + t.Charisma * 0.12f + t.Patience * 0.1f
                      + Math.Min(t.Experience, 15f) / 15f * 100f * 0.13f;
            if (t.Has(StaffTrait.ExcellentTeacher)) e *= 1.15f;
            if (t.Has(StaffTrait.Lazy)) e *= 0.85f;
            if (t.Has(StaffTrait.Motivational)) e += 3f;
            if (t.Subject == Subject.Science) e += Ix.ScienceLabQuality * 0.08f;
            if (t.Subject == Subject.Technology) e += Ix.ComputerLabQuality * 0.08f;
            e *= 0.75f + 0.25f * t.Energy / 100f;
            e *= 0.85f + 0.15f * t.Morale / 100f;
            return MathUtil.Clamp100(e);
        }

        public float CoachEffect(StaffMember c)
        {
            if (c == null) return 15f;
            float e = c.Skill * 0.4f + c.Strategy * 0.3f + c.Charisma * 0.2f + c.PlayerDevelopment * 0.1f;
            e *= 0.8f + 0.2f * c.Energy / 100f;
            return MathUtil.Clamp100(e);
        }

        // ------------------------------------------------------------------------------
        // School stats and reputation
        // ------------------------------------------------------------------------------

        public void UpdateSchoolStats(bool initialize)
        {
            S.ReputationModifiers.RemoveAll(m => m.ExpiresDay <= S.Day);

            Set(StatId.EducationQuality, ComputeEducation());
            Set(StatId.FoodQuality, ComputeFood());
            Set(StatId.DormitoryQuality, ComputeDorm());
            Set(StatId.Cleanliness, ComputeCleanliness());
            Set(StatId.StudentLife, ComputeStudentLife());
            Set(StatId.SafetyDiscipline, ComputeSafety());
            Set(StatId.StaffMorale, ComputeStaffMorale());
            Set(StatId.StudentSatisfaction, ComputeStudentSatisfaction());
            Set(StatId.ParentSatisfaction, ComputeParentSatisfaction());

            // Trust slowly returns toward neutral when nothing happens.
            int trust = (int)StatId.Trust;
            if (initialize) S.Stats[trust] = 55f;
            else S.Stats[trust] = MathUtil.Approach(S.Stats[trust], 50f + Ix.SignatureEffect(x => x.Trust), 0.002f);
            var tb = StatFactors[trust];
            tb.Clear();
            tb.Add("Kept and broken promises, handled complaints", S.Stats[trust]);

            UpdateReputation(initialize);
        }

        void Set(StatId id, float value) => S.Stats[(int)id] = MathUtil.Clamp100(value);

        Breakdown B(StatId id)
        {
            var b = StatFactors[(int)id];
            b.Clear();
            return b;
        }

        float ComputeEducation()
        {
            var b = B(StatId.EducationQuality);
            var ix = Ix;
            if (ix.Teachers == 0)
            {
                b.Add("No active teachers", 5f);
                return b.Total;
            }

            b.Add("Teacher quality", ix.TeacherQuality * 0.6f);
            b.Add("Classroom quality", ix.ClassroomQuality * 0.25f);
            if (ix.LibraryBonus > 0) b.Add("Library", ix.LibraryBonus);
            if (ix.LabBonus > 0) b.Add("Labs", ix.LabBonus);
            if (ix.DistinctSubjects > 1) b.Add("Subject coverage", Math.Min(6f, (ix.DistinctSubjects - 1) * 1.5f));
            if (ix.StudentsPerTeacher > 14f) b.Add("Too many students per teacher", -(ix.StudentsPerTeacher - 14f) * 2.2f);

            if (ix.Students > 0)
            {
                if (ix.ClassroomSeats == 0) b.Add("No classrooms", -40f);
                else if (ix.Students > ix.ClassroomSeats)
                    b.Add("Overcrowded classrooms", -Math.Min(40f, (ix.Students / (float)ix.ClassroomSeats - 1f) * 60f));
            }

            float spec = SpecializationRules.EducationBonus(S.Specialization);
            if (spec != 0) b.Add("Specialization", spec);
            SignatureTerm(b, x => x.Education);
            AssistantTerm(b, StatId.EducationQuality);
            return b.Total;
        }

        /// <summary>The assistant's personality perk, where it touches this stat.</summary>
        void AssistantTerm(Breakdown b, StatId stat)
        {
            float v = AssistantRules.StatBonus(S.Assistant, stat);
            if (v != 0f) b.Add($"{S.Assistant.FirstName}, your assistant", v);
        }

        /// <summary>The specialization's signature building, where it touches this number.</summary>
        void SignatureTerm(Breakdown b, Func<SignatureBuilding, float> effect)
        {
            float v = Ix.SignatureEffect(effect);
            if (Math.Abs(v) >= 0.05f) b.Add(Ix.SignatureBuilding.Name, v);
        }

        float ComputeFood()
        {
            var b = B(StatId.FoodQuality);
            var ix = Ix;
            if (!ix.HasCafeteria)
            {
                b.Add("No cafeteria", 5f);
                return b.Total;
            }

            b.Add(S.Policies.Food + " menu", Balance.FoodBaseQuality(S.Policies.Food));
            if (ix.CafeteriaBonus > 0) b.Add("Kitchen equipment", ix.CafeteriaBonus);
            b.Add("Kitchen cleanliness", (ix.CafeteriaCleanliness - 70f) * 0.12f);
            SignatureTerm(b, x => x.Food);
            if (ix.ChefCoverage < 1f) b.Scale("Not enough chefs", 0.3f + 0.7f * ix.ChefCoverage);
            if (ix.Students > ix.MealCapacity && ix.MealCapacity > 0)
                b.Add("Long lines (cafeteria too small)", -Math.Min(25f, (ix.Students / ix.MealCapacity - 1f) * 40f));
            return b.Total;
        }

        float ComputeDorm()
        {
            var b = B(StatId.DormitoryQuality);
            var ix = Ix;
            float q = 0f; int residents = 0;
            foreach (var kv in ix.Residents)
            {
                if (!ix.DormRoomQuality.TryGetValue(kv.Key, out var rq)) continue;
                q += rq * kv.Value.Count;
                residents += kv.Value.Count;
            }

            if (residents == 0)
            {
                b.Add("No boarders", ix.Beds > 0 ? 40f : 0f);
                return b.Total;
            }

            b.Add("Room quality", q / residents);
            SignatureTerm(b, x => x.DormQuality);
            if (ix.BathroomShortage > 0) b.Add("Bathroom shortage", -ix.BathroomShortage * 20f);
            b.Add("Laundry", ix.LaundryCoverage >= 1f ? 3f : -(1f - ix.LaundryCoverage) * 8f);
            int homeless = 0;
            foreach (var s in S.Students) if (s.DormRoomId < 0) homeless++;
            if (homeless > 0) b.Add("Students without a bed", -Math.Min(40f, homeless * 6f));
            return b.Total;
        }

        float ComputeCleanliness()
        {
            var b = B(StatId.Cleanliness);
            b.Add("Average room cleanliness", Ix.AvgCleanliness);
            return b.Total;
        }

        float ComputeStudentLife()
        {
            var b = B(StatId.StudentLife);
            var ix = Ix;
            b.Add("Base", 15f);
            if (ix.LoungeScore > 0) b.Add("Student lounge", ix.LoungeScore * 18f);
            if (ix.GamingScore > 0) b.Add("Gaming room", ix.GamingScore * 10f);
            if (ix.ActiveClubs > 0) b.Add("Clubs", Math.Min(20f, ix.ActiveClubs * 4f));
            if (ix.SportsFacilities > 0) b.Add("Sports facilities", Math.Min(15f, ix.SportsFacilities * 4f));
            if (ix.ComplexQuality > 0) b.Add("Sports complex", ix.ComplexQuality * 0.06f);
            if (ix.PlayScore > 0) b.Add("Playgrounds and fields", ix.PlayScore * 8f);
            if (ix.GardenScore > 0) b.Add("Gardens", ix.GardenScore * 5f);
            if (ix.PicnicScore > 0) b.Add("Picnic area", ix.PicnicScore * 4f);
            if (ix.Beauty > 0.05f) b.Add("A pretty campus", ix.Beauty * 6f);
            if (S.Teams.Count > 0) b.Add("School teams", Math.Min(8f, S.Teams.Count * 3f));
            if (ix.HasLibrary) b.Add("Library", 3f);
            if (S.Policies.Curfew == CurfewPolicy.Relaxed) b.Add("Relaxed curfew", 6f);
            if (S.Policies.Curfew == CurfewPolicy.Strict) b.Add("Strict curfew", -6f);
            if (ix.InternationalStudents > 0) b.Add("Students from abroad", Math.Min(4f, 25f * ix.InternationalStudents / ix.Students));
            if (ix.Manor > 0) b.Add("The old manor", ix.Manor * Balance.ManorStudentLife);
            float spec = SpecializationRules.StudentLifeBonus(S.Specialization);
            if (spec != 0) b.Add("Specialization", spec);
            SignatureTerm(b, x => x.StudentLife);
            AssistantTerm(b, StatId.StudentLife);
            return b.Total;
        }

        float ComputeSafety()
        {
            var b = B(StatId.SafetyDiscipline);
            var ix = Ix;
            b.Add("Base", 30f);
            b.Add("Security guards", 30f * ix.GuardCoverage);
            if (ix.SecurityRoomScore > 0) b.Add("Security room", ix.SecurityRoomScore * 10f);
            b.Add("Nurse coverage", 8f * ix.NurseCoverage * (ix.NurseOfficeScore > 0 ? 1f : 0.5f));
            b.Add("Student behavior", ix.AvgBehavior * 0.25f);
            if (S.Policies.Discipline == DisciplinePolicy.Strict) b.Add("Strict discipline policy", 6f);
            if (S.Policies.Discipline == DisciplinePolicy.Lenient) b.Add("Lenient discipline policy", -6f);
            if (S.IncidentPressure > 0.5f) b.Add("Recent incidents", -S.IncidentPressure);
            if (ix.AvgRoomCondition < 50f) b.Add("Worn-out buildings", -(50f - ix.AvgRoomCondition) * 0.3f);
            SignatureTerm(b, x => x.Safety);
            AssistantTerm(b, StatId.SafetyDiscipline);
            return b.Total;
        }

        float ComputeStaffMorale()
        {
            var b = B(StatId.StaffMorale);
            if (S.Staff.Count == 0)
            {
                b.Add("No staff", 50f);
                return b.Total;
            }
            float sum = 0f;
            foreach (var s in S.Staff) sum += s.Morale;
            b.Add("Average staff morale", sum / S.Staff.Count);
            return b.Total;
        }

        float ComputeStudentSatisfaction()
        {
            var b = B(StatId.StudentSatisfaction);
            if (S.Students.Count == 0)
            {
                b.Add("No students", 50f);
                return b.Total;
            }
            return AverageOfIndividuals(b, s => s.HappinessFactors, s => s.Happiness);
        }

        float ComputeParentSatisfaction()
        {
            var b = B(StatId.ParentSatisfaction);
            if (S.Students.Count == 0)
            {
                b.Add("No families", 50f);
                return b.Total;
            }
            return AverageOfIndividuals(b, s => s.ParentFactors, s => s.ParentMood);
        }

        readonly Dictionary<string, float> _aggregate = new Dictionary<string, float>();
        readonly List<string> _aggregateOrder = new List<string>();

        /// <summary>
        /// School-wide satisfaction is the average of individuals. To explain it, average each
        /// individual factor across everyone, then add how far moods still lag behind their targets.
        /// </summary>
        float AverageOfIndividuals(Breakdown b, Func<Student, Breakdown> factors, Func<Student, float> actual)
        {
            _aggregate.Clear();
            _aggregateOrder.Clear();
            float actualSum = 0f;
            foreach (var s in S.Students)
            {
                actualSum += actual(s);
                foreach (var f in factors(s).Factors)
                {
                    if (!_aggregate.ContainsKey(f.Label)) _aggregateOrder.Add(f.Label);
                    _aggregate.TryGetValue(f.Label, out float v);
                    _aggregate[f.Label] = v + f.Value;
                }
            }

            float n = S.Students.Count;
            foreach (var label in _aggregateOrder) b.Add(label, _aggregate[label] / n);
            float average = actualSum / n;
            float lag = average - b.Total;
            if (Math.Abs(lag) >= 0.5f) b.Add("Moods still catching up with conditions", lag);
            return average;
        }

        // ------------------------------------------------------------------------------

        void UpdateReputation(bool initialize)
        {
            var st = S.Stats;
            float Stat(StatId id) => st[(int)id];

            // Academic
            var a = RepB(ReputationId.Academic);
            a.Add("Education quality", Stat(StatId.EducationQuality) * 0.55f);
            a.Add("Student grades", Ix.AvgGrades * 0.30f);
            if (S.AcademicLegacy > 0) a.Add("Graduate success", S.AcademicLegacy);
            SignatureTerm(a, x => x.AcademicRep);

            // Sports
            var sp = RepB(ReputationId.Sports);
            if (S.Teams.Count == 0)
            {
                sp.Add("No competitive teams", 8f);
                if (Ix.SportsFacilities > 0) sp.Add("Sports facilities", Math.Min(12f, Ix.SportsFacilities * 3f));
                if (Ix.ComplexQuality > 0) sp.Add("Sports complex", Ix.ComplexQuality * 0.08f);
            }
            else
            {
                float rating = 0f, wins = 0f, games = 0f;
                foreach (var t in S.Teams) { rating += t.Rating; wins += t.Wins; games += t.Wins + t.Losses; }
                sp.Add("Base", 12f);
                sp.Add("Team strength", rating / S.Teams.Count * 0.5f);
                if (games > 0) sp.Add("Season record", (wins / games - 0.5f) * 20f);
            }
            if (S.SportsLegacy > 0) sp.Add("Championship history", S.SportsLegacy);
            float spSpec = SpecializationRules.SportsRepBonus(S.Specialization);
            if (spSpec != 0) sp.Add("Specialization", spSpec);
            SignatureTerm(sp, x => x.SportsRep);

            RepB(ReputationId.Dormitory).Add("Dormitory quality", Stat(StatId.DormitoryQuality));

            var sl = RepB(ReputationId.StudentLife);
            sl.Add("Student satisfaction", Stat(StatId.StudentSatisfaction) * 0.5f);
            sl.Add("Student life", Stat(StatId.StudentLife) * 0.5f);

            RepB(ReputationId.Safety).Add("Safety & discipline", Stat(StatId.SafetyDiscipline));
            RepB(ReputationId.Food).Add("Food quality", Stat(StatId.FoodQuality));

            for (int i = 0; i < GameState.ReputationCount; i++)
            {
                if (i == (int)ReputationId.Overall) continue;
                ApplyModifiers((ReputationId)i);
                ReputationTargets[i] = ReputationFactors[i].Clamped;
                S.Reputation[i] = initialize
                    ? ReputationTargets[i]
                    : MathUtil.Approach(S.Reputation[i], ReputationTargets[i], CategoryDrift);
            }

            // Overall prestige follows the public categories plus what families say.
            var o = RepB(ReputationId.Overall);
            float R(ReputationId id) => S.Reputation[(int)id];
            o.Add("Academic reputation", R(ReputationId.Academic) * 0.28f);
            o.Add("Sports reputation", R(ReputationId.Sports) * 0.10f);
            o.Add("Dormitory reputation", R(ReputationId.Dormitory) * 0.10f);
            o.Add("Student life reputation", R(ReputationId.StudentLife) * 0.12f);
            o.Add("Safety reputation", R(ReputationId.Safety) * 0.10f);
            o.Add("Food reputation", R(ReputationId.Food) * 0.05f);
            o.Add("Parent satisfaction", Stat(StatId.ParentSatisfaction) * 0.20f);
            o.Add("Trust", Stat(StatId.Trust) * 0.05f);
            if (Ix.Manor > 0) o.Add("The old manor", Ix.Manor * Balance.ManorPrestige);
            bool elite = _sim.IsUnlocked(Feature.EliteInstitution);
            if (elite) o.Add("Elite institution", Balance.ElitePrestige);
            float endowment = Balance.EndowmentPrestige(S.Endowment, elite);
            if (endowment > 0.05f) o.Add("Endowment", endowment);
            ApplyModifiers(ReputationId.Overall);

            int oi = (int)ReputationId.Overall;
            ReputationTargets[oi] = o.Clamped;
            S.Reputation[oi] = initialize
                ? ReputationTargets[oi] * 0.8f   // a brand-new school is less known than it deserves
                : MathUtil.Approach(S.Reputation[oi], ReputationTargets[oi], OverallDrift);
        }

        Breakdown RepB(ReputationId id)
        {
            var b = ReputationFactors[(int)id];
            b.Clear();
            return b;
        }

        void ApplyModifiers(ReputationId id)
        {
            var b = ReputationFactors[(int)id];
            foreach (var m in S.ReputationModifiers)
                if (m.Rep == id) b.Add(m.Label, m.Value);
        }
    }
}
