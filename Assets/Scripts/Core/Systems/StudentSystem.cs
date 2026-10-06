using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Per-student daily life: happiness, stress, grades, growth, incidents, and what their
    /// parents think of the school.
    /// </summary>
    public sealed class StudentSystem
    {
        readonly Simulation _sim;

        public StudentSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;
        SchoolIndices Ix => _sim.Indices;
        float Stat(StatId id) => S.Stats[(int)id];

        public void DailyTick()
        {
            int day = S.Day;
            S.SchoolMoodlets.Expire(day);
            S.IncidentPressure *= 0.96f;
            foreach (var s in S.Students)
            {
                s.Moodlets.Expire(day);
                s.ParentMoodlets.Expire(day);
            }

            if (!_sim.InSession) return;

            bool exam = Calendar.IsExamMonth(day);
            float schoolMood = S.SchoolMoodlets.Sum();
            foreach (var s in S.Students)
            {
                if (s.Homesick > 0f) Settle(s);
                UpdateHappiness(s, schoolMood);
                UpdateStress(s, exam);
                UpdateGrades(s);
                UpdateGrowth(s);
                UpdateParent(s);
                s.Health = Math.Min(100f, s.Health + 1.5f);
            }

            RollIncidents(exam);
        }

        // ------------------------------------------------------------------------------
        // Happiness
        // ------------------------------------------------------------------------------

        void UpdateHappiness(Student s, float schoolMood)
        {
            float target = ComputeHappinessTarget(s, schoolMood);
            s.HappinessTarget = target;
            s.Happiness = MathUtil.Clamp100(MathUtil.Approach(s.Happiness, target, 0.08f) + _sim.Rng.Range(-0.4f, 0.4f));
        }

        public float ComputeHappinessTarget(Student s, float schoolMood)
        {
            var b = s.HappinessFactors;
            b.Clear();
            int day = S.Day;
            var pol = S.Policies;

            b.Add("Base", 50f);
            b.Add("Food vs expectations", (Stat(StatId.FoodQuality) - Balance.FoodExpectation(S.Prestige)) * 0.25f);

            var room = S.FindRoom(s.DormRoomId);
            if (room == null)
            {
                b.Add("No bed assigned", -15f);
            }
            else
            {
                Ix.DormRoomQuality.TryGetValue(room.Id, out var rq);
                b.Add("Dorm room vs expectations", (rq - Balance.DormExpectation(S.Prestige)) * 0.25f);
                if (!room.HasPrivateBath && Ix.BathroomShortage > 0) b.Add("Bathroom queues", -Ix.BathroomShortage * 12f);
                if (s.Has(StudentTrait.CleanFreak) && room.Cleanliness < 70f) b.Add("Clean freak in a dirty room", -(70f - room.Cleanliness) * 0.25f);

                var roommates = Ix.ResidentsOf(room.Id);
                if (roommates.Count > 1)
                {
                    float sum = 0f; int n = 0; bool enemy = false;
                    foreach (var o in roommates)
                    {
                        if (o.Id == s.Id) continue;
                        float score = S.Relationships.Score(s.Id, o.Id);
                        sum += score;
                        n++;
                        if (score <= -60f) enemy = true;
                    }
                    if (n > 0) b.Add("Roommates", sum / n * 0.12f);
                    if (enemy) b.Add("Living with an enemy", -6f);
                }
                if (s.Has(StudentTrait.Introvert))
                {
                    if (roommates.Count <= 2) b.Add("Introvert: small room", 4f);
                    else if (roommates.Count >= 5) b.Add("Introvert: crowded room", -4f);
                }
            }
            if (Ix.LaundryCoverage < 1f) b.Add("No laundry access", -(1f - Ix.LaundryCoverage) * 4f);

            int best = 0, friends = 0, enemies = 0, rivals = 0; bool crush = false;
            foreach (var r in S.Relationships.For(s.Id))
            {
                switch (r.Type)
                {
                    case RelationshipType.BestFriend: best++; break;
                    case RelationshipType.Friend: friends++; break;
                    case RelationshipType.Enemy: enemies++; break;
                    case RelationshipType.Rival: rivals++; break;
                    case RelationshipType.Crush: crush = true; break;
                }
            }
            if (best + friends > 0) b.Add("Friends", Math.Min(12f, best * 4f + friends * 1.5f));
            else if (s.EnrolledDay < day - 45) b.Add("No friends", -6f);
            if (enemies > 0) b.Add("Enemies", -Math.Min(15f, enemies * 5f));
            if (rivals > 0) b.Add("Rivals", -Math.Min(4f, rivals * 1f));
            if (crush) b.Add("Has a crush", 4f);

            b.Add("Student life", (Stat(StatId.StudentLife) - 50f) * 0.2f);
            if (Ix.Teachers > 0 && Math.Abs(Ix.TeacherWarmth) > 0.05f) b.Add("Teachers' personalities", Ix.TeacherWarmth * 6f);
            if (s.Stress > 45f) b.Add("Stress", -(s.Stress - 45f) * 0.35f);

            if (pol.Curfew == CurfewPolicy.Strict) b.Add("Strict curfew", s.Has(StudentTrait.NightOwl) ? -8f : -4f);
            if (pol.Curfew == CurfewPolicy.Relaxed) b.Add("Relaxed curfew", s.Has(StudentTrait.NightOwl) ? 6f : 4f);
            if (pol.Discipline == DisciplinePolicy.Strict) b.Add("Strict discipline", s.Has(StudentTrait.Troublemaker) ? -8f : -4f);
            if (pol.Discipline == DisciplinePolicy.Lenient) b.Add("Lenient discipline", 3f);

            if (s.Has(StudentTrait.Extrovert)) b.Add(Ix.LoungeScore > 0 ? "Extrovert: has a lounge" : "Extrovert: nowhere to hang out", Ix.LoungeScore > 0 ? 3f : -3f);
            if (s.Has(StudentTrait.Athletic))
            {
                b.Add(Ix.SportsFacilities > 0 ? "Athlete: sports facilities" : "Athlete: no sports facilities", Ix.SportsFacilities > 0 ? 3f : -5f);
                if (Ix.ComplexQuality > 0) b.Add("Athlete: the sports complex", 2f);
                if (s.TeamId >= 0) b.Add("On a school team", 4f);
            }
            if (s.Grade <= 2 && Ix.PlayScore > 0.05f) b.Add("Somewhere to play", Ix.PlayScore * 3f);
            if (s.Has(StudentTrait.Bookworm)) b.Add(Ix.HasLibrary ? "Bookworm: library" : "Bookworm: no library", Ix.HasLibrary ? 3f : -4f);
            if (s.Has(StudentTrait.Anxious) && Calendar.IsExamMonth(day)) b.Add("Anxious: exams", -5f);

            var club = s.ClubId >= 0 ? S.FindClub(s.ClubId) : null;
            if (club != null)
            {
                bool arts = club.Type == ClubType.Art || club.Type == ClubType.Music || club.Type == ClubType.Drama;
                b.Add("Club member", s.Has(StudentTrait.Artistic) && arts ? 7f : 4f);
            }
            else if (s.Has(StudentTrait.Artistic) && _sim.IsUnlocked(Feature.Clubs))
            {
                b.Add("Artist: no arts club", -2f);
            }

            if (s.IsInjured(day)) b.Add("Injured", -8f);
            if (s.IsSick(day)) b.Add("Sick", -6f);
            if (s.Scholarship != ScholarshipType.None) b.Add("Scholarship", 2f);
            if (s.Homesick >= 1f) b.Add("Homesick", -s.Homesick * 0.16f);

            foreach (var m in s.Moodlets) b.Add(m.Label, m.Value);
            if (Math.Abs(schoolMood) > 0.01f)
                foreach (var m in S.SchoolMoodlets) b.Add(m.Label, m.Value);

            return b.Clamped;
        }

        /// <summary>
        /// Students from abroad settle in over the months: faster with friends, a well-kept International
        /// House with room for everyone, and language teachers.
        /// </summary>
        void Settle(Student s)
        {
            float pace = 0.18f + 0.5f * Ix.InternationalSupport + Ix.LanguageSupport;
            int friends = S.Relationships.CountOfType(s.Id, RelationshipType.Friend) + 2 * S.Relationships.CountOfType(s.Id, RelationshipType.BestFriend);
            pace += Math.Min(0.24f, friends * 0.06f);
            if (s.Has(StudentTrait.Extrovert)) pace += 0.1f;
            if (s.Has(StudentTrait.Introvert) || s.Has(StudentTrait.Anxious)) pace -= 0.06f;
            s.Homesick = Math.Max(0f, s.Homesick - pace);
        }

        // ------------------------------------------------------------------------------
        // Stress, grades, growth
        // ------------------------------------------------------------------------------

        void UpdateStress(Student s, bool exam)
        {
            float t = 28f;
            if (exam) t += 18f;
            t += Ix.StrictShare * 10f;
            if (s.Grades < 45f) t += (45f - s.Grades) * 0.3f;
            if (s.Has(StudentTrait.Anxious)) t += 10f;
            if (s.Has(StudentTrait.Hardworking)) t += 5f;
            if (s.Has(StudentTrait.Lazy)) t -= 8f;
            if (s.Happiness < 35f) t += 6f;
            t += s.Homesick * 0.1f;
            t += SpecializationRules.StressBonus(S.Specialization) + Ix.SignatureEffect(x => x.Stress);

            var team = s.TeamId >= 0 ? S.FindTeam(s.TeamId) : null;
            if (team != null) t += team.Intensity == TrainingIntensity.Intense ? 10f : team.Intensity == TrainingIntensity.Normal ? 3f : 0f;

            var room = S.FindRoom(s.DormRoomId);
            if (room != null)
                foreach (var o in Ix.ResidentsOf(room.Id))
                    if (o.Id != s.Id && S.Relationships.Score(s.Id, o.Id) <= -60f) { t += 8f; break; }

            t -= 8f * Ix.CounselorCoverage;
            t -= 3f * Ix.NurseOfficeScore;
            t -= 4f * Ix.LoungeScore;
            t -= 3f * Ix.GardenScore;

            s.Stress = MathUtil.Clamp100(MathUtil.Approach(s.Stress, MathUtil.Clamp100(t), 0.06f));
        }

        void UpdateGrades(Student s)
        {
            float t = s.Academic * 0.42f + Stat(StatId.EducationQuality) * 0.33f + s.Discipline * 0.12f + 10f;
            if (s.Has(StudentTrait.Hardworking)) t += 6f;
            if (s.Has(StudentTrait.Lazy)) t -= 6f;
            if (s.Has(StudentTrait.Bookworm)) t += 4f;
            if (s.Stress > 60f) t -= (s.Stress - 60f) * 0.5f;
            if (s.IsSick(S.Day)) t -= 8f;
            if (s.Has(StudentTrait.Lazy) && Ix.GamingScore > 0) t -= 2f;
            t -= s.Homesick * 0.06f;   // still finding their feet in a new language

            foreach (var r in S.Relationships.For(s.Id))
            {
                if (r.Crush) t -= 3f;
                if (r.Rivalry) t += 2f;
            }

            var team = s.TeamId >= 0 ? S.FindTeam(s.TeamId) : null;
            if (team != null && team.Intensity == TrainingIntensity.Intense) t -= 3f;

            s.Grades = MathUtil.Clamp100(MathUtil.Approach(s.Grades, MathUtil.Clamp100(t), 0.025f));
        }

        void UpdateGrowth(Student s)
        {
            float edu = Stat(StatId.EducationQuality);
            float mult = 1f;
            if (s.Has(StudentTrait.Hardworking)) mult *= 1.3f;
            if (s.Has(StudentTrait.Lazy)) mult *= 0.7f;
            if (Ix.MotivationalTeacher) mult *= 1.2f;
            if (s.Has(StudentTrait.Bookworm) && Ix.HasLibrary) mult *= 1.15f;
            s.Academic = MathUtil.Clamp100(s.Academic + 0.012f * (edu - 45f) / 55f * mult);

            // Discipline is slow to change, but structure and counseling do reform students.
            float d = 0f;
            if (S.Policies.Discipline == DisciplinePolicy.Strict && s.Discipline < 75f) d += 0.01f;
            if (S.Policies.Discipline == DisciplinePolicy.Lenient) d -= 0.005f;
            d += 0.005f * Ix.StrictShare;
            if (s.Discipline < 50f) d += 0.012f * Ix.CounselorCoverage;
            s.Discipline = MathUtil.Clamp100(s.Discipline + d);

            s.Behavior = MathUtil.Clamp100(MathUtil.Approach(s.Behavior, s.Discipline * 0.6f + 40f, 0.01f));
        }

        // ------------------------------------------------------------------------------
        // Parents
        // ------------------------------------------------------------------------------

        void UpdateParent(Student s)
        {
            float target = ComputeParentTarget(s);
            s.ParentMood = MathUtil.Clamp100(MathUtil.Approach(s.ParentMood, target, 0.03f));
        }

        public float ComputeParentTarget(Student s)
        {
            var b = s.ParentFactors;
            b.Clear();
            b.Add("Education quality", Stat(StatId.EducationQuality) * 0.14f);
            b.Add("Child's grades", s.Grades * 0.10f);
            b.Add("Child's happiness", s.Happiness * 0.18f);
            if (Ix.Beauty > 0.1f) b.Add("A lovely campus", Ix.Beauty * 3f);
            b.Add("Safety", Stat(StatId.SafetyDiscipline) * 0.14f);

            var room = S.FindRoom(s.DormRoomId);
            if (room != null && Ix.DormRoomQuality.TryGetValue(room.Id, out var rq)) b.Add("Child's room", rq * 0.10f);
            else b.Add("Child has no bed", -10f);

            b.Add("Food", Stat(StatId.FoodQuality) * 0.08f);
            b.Add("Trust in the school", Stat(StatId.Trust) * 0.10f);
            b.Add("Cleanliness", Stat(StatId.Cleanliness) * 0.06f);
            b.Add("School prestige", S.Prestige * 0.10f);
            b.Add("Value for money", TuitionValueTerm(s, S.Policies.AnnualTuition));

            if (s.Scholarship != ScholarshipType.None) b.Add("Scholarship", 4f);
            if (s.LastIncidentDay > S.Day - 60) b.Add("Child's discipline problems", -3f);
            if (s.Homesick >= 5f) b.Add("Child is homesick", -s.Homesick * 0.08f);
            if (s.International && Ix.InternationalSupport > 0f) b.Add("International House", Ix.InternationalSupport * 4f);
            if (Ix.Manor > 0f) b.Add("The old manor", Ix.Manor * Balance.ManorParents);
            float signature = Ix.SignatureEffect(x => x.Parents);
            if (signature >= 0.05f) b.Add(Ix.SignatureBuilding.Name, signature);
            foreach (var m in s.ParentMoodlets) b.Add(m.Label, m.Value);
            return b.Clamped;
        }

        public float TuitionSensitivity(Student s)
        {
            return (1.5f - s.FamilyWealth / 100f) * SpecializationRules.TuitionSensitivity(S.Specialization);
        }

        /// <summary>Positive when the family feels they get more than they pay for.</summary>
        public float TuitionValueTerm(Student s, float annualTuition)
        {
            var room = S.FindRoom(s.DormRoomId);
            float dorm = _sim.EffectiveDormFee(room) * Balance.DormBillingMonths;
            float paid = annualTuition * (1f - s.Discount) + dorm;
            float fair = _sim.FairTuition + dorm;
            float value = (fair - paid) / fair;
            return MathUtil.Clamp(value * 35f * TuitionSensitivity(s), -20f, 12f);
        }

        /// <summary>Chance this family re-enrolls next year at the given tuition.</summary>
        public float RetentionChance(Student s, float annualTuition)
        {
            float p = 0.97f;
            p -= Math.Max(0f, 45f - s.Happiness) * 0.012f;
            p -= Math.Max(0f, 45f - s.ParentMood) * 0.012f;
            float fair = _sim.FairTuition;
            float paid = annualTuition * (1f - s.Discount);
            if (paid > fair) p -= (paid - fair) / fair * 0.6f * TuitionSensitivity(s);
            if (s.Scholarship != ScholarshipType.None) p += 0.03f;
            p += Math.Min(0.03f, S.Relationships.CountOfType(s.Id, RelationshipType.BestFriend) * 0.01f);
            return MathUtil.Clamp(p, 0.2f, 0.995f);
        }

        // ------------------------------------------------------------------------------
        // Incidents
        // ------------------------------------------------------------------------------

        void RollIncidents(bool exam)
        {
            int day = S.Day;
            var pol = S.Policies;
            float curfew = pol.Curfew == CurfewPolicy.Relaxed ? 1.3f : pol.Curfew == CurfewPolicy.Strict ? 0.8f : 1f;
            float disc = pol.Discipline == DisciplinePolicy.Lenient ? 1.3f : pol.Discipline == DisciplinePolicy.Strict ? 0.75f : 1f;
            float guard = 1.25f - 0.5f * Ix.GuardCoverage;
            float weekend = Calendar.IsWeekend(day) ? 1.4f : 1f;

            // Copy: incidents can trigger messages that inspect the list.
            var students = new List<Student>(S.Students);
            foreach (var s in students)
            {
                float p = 0.0012f * (1.7f - s.Discipline / 100f);
                if (s.Has(StudentTrait.Troublemaker)) p *= 2.6f;

                int troubleFriends = 0;
                foreach (var o in Ix.ResidentsOf(s.DormRoomId))
                    if (o.Id != s.Id && o.Has(StudentTrait.Troublemaker) && S.Relationships.Score(s.Id, o.Id) >= 30f) troubleFriends++;
                p *= 1f + 0.4f * troubleFriends;

                p *= guard * curfew * disc * weekend;
                if (s.Stress > 70f) p *= 1.4f;
                if (s.Happiness < 30f) p *= 1.3f;

                if (_sim.Rng.Chance(p)) Incident(s, exam);
            }
        }

        void Incident(Student s, bool exam)
        {
            int day = S.Day;
            var rng = _sim.Rng;
            s.Incidents++;
            s.LastIncidentDay = day;
            s.Behavior = MathUtil.Clamp100(s.Behavior - 12f);
            S.Year.Incidents++;
            S.IncidentPressure += 5f * Math.Min(1f, 40f / Math.Max(1, S.Students.Count));

            Student enemy = null;
            foreach (var r in S.Relationships.For(s.Id))
                if (r.Type == RelationshipType.Enemy) { enemy = S.FindStudent(r.Other(s.Id)); break; }

            var roomFriends = new List<Student>();
            foreach (var o in Ix.ResidentsOf(s.DormRoomId))
                if (o.Id != s.Id && S.Relationships.Score(s.Id, o.Id) >= 30f) roomFriends.Add(o);

            if (exam && s.Grades < 60f && rng.Chance(0.5f))
            {
                s.Remember(day, "Caught cheating on an exam.");
                if (!_sim.Events.TriggerDirected("cheating_scandal", m => m.StudentId = s.Id))
                    _sim.Log($"{s.FullName} was caught cheating (handled by teachers).");
                return;
            }

            if (enemy != null && rng.Chance(0.6f))
            {
                var rel = S.Relationships.GetOrCreate(s.Id, enemy.Id);
                rel.Score = Math.Max(-100f, rel.Score - 10f);
                if (rng.Chance(0.5f))
                {
                    s.Remember(day, $"Got into a fight with {enemy.FullName}.");
                    enemy.Remember(day, $"Got into a fight with {s.FullName}.");
                    enemy.Behavior = MathUtil.Clamp100(enemy.Behavior - 8f);
                    s.Moodlets.Set("Got in a fight", -6f, day + 7);
                    enemy.Moodlets.Set("Got in a fight", -6f, day + 7);
                    s.ParentMoodlets.Set("Child was in a fight", -6f, day + 20);
                    enemy.ParentMoodlets.Set("Child was in a fight", -6f, day + 20);
                    if (!_sim.Events.TriggerDirected("student_fight", m => { m.StudentId = s.Id; m.StudentId2 = enemy.Id; }))
                        _sim.Log($"{s.FullName} and {enemy.FullName} got into a fight.");
                    if (rng.Chance(0.2f)) _sim.News.Incident("fight");
                }
                else
                {
                    s.Remember(day, $"Bullied {enemy.FullName}.");
                    enemy.Remember(day, $"Was bullied by {s.FullName}.");
                    enemy.Moodlets.Set("Being bullied", -12f, day + 20);
                    enemy.Stress = MathUtil.Clamp100(enemy.Stress + 10f);
                    if (!_sim.Events.TriggerDirected("bullying_report", m => { m.StudentId = enemy.Id; m.StudentId2 = s.Id; }))
                        _sim.Log($"{enemy.FullName} is being bullied by {s.FullName}.");
                    if (rng.Chance(0.15f)) _sim.News.Incident("bullying");
                }
                return;
            }

            var room = S.FindRoom(s.DormRoomId);
            if (room != null && roomFriends.Count >= 2 && rng.Chance(0.6f))
            {
                room.Cleanliness = MathUtil.Clamp100(room.Cleanliness - 25f);
                room.Condition = MathUtil.Clamp100(room.Condition - 5f);
                s.Remember(day, "Threw a late-night party in the dorm.");
                if (!_sim.Events.TriggerDirected("late_night_party", m => { m.StudentId = s.Id; m.RoomId = room.Id; }))
                    _sim.Log($"Late-night party in {room.Name}.");
                if (rng.Chance(0.12f)) _sim.News.Incident("party");
                return;
            }

            if (room != null && s.Has(StudentTrait.Troublemaker) && rng.Chance(0.4f))
            {
                room.Condition = MathUtil.Clamp100(room.Condition - 12f);
                s.Remember(day, $"Vandalized {room.Name}.");
                _sim.Log($"{s.FullName} damaged {room.Name} (-12 condition).");
                if (rng.Chance(0.15f)) _sim.News.Incident("vandalism");
                return;
            }

            s.Behavior = MathUtil.Clamp100(s.Behavior + 6f);  // curfew violations are minor
            _sim.Log($"{s.FullName} was caught out after curfew.");
        }

        // ------------------------------------------------------------------------------
        // Enrollment
        // ------------------------------------------------------------------------------

        public void Enroll(Student s)
        {
            s.EnrolledDay = S.Day;
            S.Students.Add(s);
            _sim.Facilities.AutoAssignBed(s);
            s.Remember(S.Day, $"Enrolled at {S.SchoolName}.");

            // Fill in their factor breakdowns right away so school-wide explanations include them.
            s.HappinessTarget = ComputeHappinessTarget(s, S.SchoolMoodlets.Sum());
            ComputeParentTarget(s);
        }

        public void Remove(Student s, string reason)
        {
            S.Students.Remove(s);
            S.Relationships.RemoveStudent(s.Id);
            if (Ix.Residents.TryGetValue(s.DormRoomId, out var list)) list.Remove(s);
            foreach (var t in S.Teams) t.Roster.Remove(s.Id);
            foreach (var c in S.Clubs) c.Members.Remove(s.Id);
            S.Promises.RemoveAll(p => !p.Resolved && p.StudentId == s.Id);
            if (!string.IsNullOrEmpty(reason)) _sim.Log($"{s.FullName} left the school: {reason}");
        }
    }

    public static class StudentFactory
    {
        public static Student Create(Simulation sim, int grade, float academicMean, float athleticMean, float wealthMean, float artisticMean = 45f)
        {
            var rng = sim.Rng;
            var s = new Student
            {
                Id = sim.State.NewId(),
                FirstName = Names.FirstName(rng),
                LastName = Names.LastName(rng),
                Grade = grade,
                EnrolledDay = sim.Day,
            };
            s.ParentName = Names.Parent(rng, s.LastName);
            s.Academic = rng.Stat(academicMean, 15f);
            s.Athletic = rng.Stat(athleticMean, 16f);
            s.Social = rng.Stat(50f, 16f);
            s.Discipline = rng.Stat(55f, 16f);
            s.Artistic = rng.Stat(artisticMean, 16f);
            s.FamilyWealth = rng.Stat(wealthMean, 20f);

            s.Traits.AddRange(TraitRules.RollStudentTraits(rng, rng.Range(2, 4)));
            if (s.Has(StudentTrait.Athletic)) s.Athletic = MathUtil.Clamp100(s.Athletic + 12f);
            if (s.Has(StudentTrait.Artistic)) s.Artistic = MathUtil.Clamp100(s.Artistic + 15f);
            if (s.Has(StudentTrait.Troublemaker)) s.Discipline = MathUtil.Clamp100(s.Discipline - 20f);
            if (s.Has(StudentTrait.Hardworking)) s.Discipline = MathUtil.Clamp100(s.Discipline + 8f);
            if (s.Has(StudentTrait.Lazy)) s.Discipline = MathUtil.Clamp100(s.Discipline - 6f);
            if (s.Has(StudentTrait.Bookworm)) s.Academic = MathUtil.Clamp100(s.Academic + 6f);
            if (s.Has(StudentTrait.Leader)) s.Social = MathUtil.Clamp100(s.Social + 10f);
            if (s.Has(StudentTrait.Extrovert)) s.Social = MathUtil.Clamp100(s.Social + 8f);
            if (s.Has(StudentTrait.Introvert)) s.Social = MathUtil.Clamp100(s.Social - 5f);

            if (s.Athletic > 65f && rng.Chance(0.5f))
                s.SportTalent = (SportType)rng.Range(1, Enum.GetValues(typeof(SportType)).Length);

            s.Grades = MathUtil.Clamp100(s.Academic * 0.7f + 15f + rng.Range(-5f, 5f));
            s.Behavior = MathUtil.Clamp100(s.Discipline * 0.6f + 35f);
            s.Happiness = 60f;
            s.Stress = 30f;
            s.ParentMood = 55f + rng.Range(-5f, 5f);
            return s;
        }
    }
}
