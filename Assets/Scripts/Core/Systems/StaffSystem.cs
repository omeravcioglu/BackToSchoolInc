using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Staff energy and morale, the job market, contracts, raises, poaching, training and departures.
    /// </summary>
    public sealed class StaffSystem
    {
        readonly Simulation _sim;

        public StaffSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;
        SchoolIndices Ix => _sim.Indices;

        // ------------------------------------------------------------------------------
        // Daily
        // ------------------------------------------------------------------------------

        public void DailyTick()
        {
            int day = S.Day;
            int difficult = 0;
            foreach (var s in S.Staff) if (s.Has(StaffTrait.Difficult)) difficult++;

            foreach (var s in new List<StaffMember>(S.Staff))
            {
                s.Moodlets.Expire(day);

                if (s.LeavingDay >= 0 && day >= s.LeavingDay)
                {
                    Depart(s, "their last day");
                    continue;
                }
                if (s.LeavingDay < 0 && day >= s.ContractEndDay)
                {
                    // Nobody answered the renewal: they stay on a rolling one-year contract, a little annoyed.
                    s.ContractEndDay = day + Calendar.DaysPerYear;
                    s.RenewalOffered = false;
                    s.Moodlets.Set("Contract left unaddressed", -5f, day + 60);
                }

                if (s.CurrentTraining.HasValue && day >= s.AwayUntilDay) CompleteTraining(s);

                UpdateEnergy(s, day);
                UpdateMorale(s, difficult);
                s.Loyalty = MathUtil.Approach(s.Loyalty, s.Morale, 0.004f);
                if (s.Has(StaffTrait.Loyal)) s.Loyalty = Math.Max(s.Loyalty, 60f);
                s.Experience += 1f / Calendar.DaysPerYear;
            }
        }

        void UpdateEnergy(StaffMember s, int day)
        {
            float lounge = Ix.StaffLoungeQuality * 0.02f;
            if (s.IsAway(day) && !s.CurrentTraining.HasValue)
            {
                s.Energy = MathUtil.Clamp100(s.Energy + 8f);
                return;
            }

            bool rest = !_sim.InSession || Calendar.IsWeekend(day) || s.IsAway(day);
            if (rest)
            {
                s.Energy = MathUtil.Clamp100(s.Energy + 5f + lounge);
                return;
            }

            float drain = Workload(s);
            if (s.Has(StaffTrait.BurnoutProne)) drain *= 1.5f;
            if (s.Has(StaffTrait.Lazy)) drain *= 0.7f;
            s.Energy = MathUtil.Clamp100(s.Energy - drain + lounge);
        }

        public float Workload(StaffMember s)
        {
            float students = Ix.Students;
            float Load(StaffRole role, float perStaff)
            {
                int n = Math.Max(1, _sim.CountStaff(role));
                return Math.Max(0.5f, students / (n * perStaff));
            }

            switch (s.Role)
            {
                case StaffRole.Teacher: return Math.Max(0.5f, Ix.StudentsPerTeacher / Balance.StudentsPerTeacherIdeal);
                case StaffRole.Custodian: return Load(StaffRole.Custodian, Balance.StudentsPerCustodian);
                case StaffRole.Chef: return Load(StaffRole.Chef, Balance.StudentsPerChef);
                case StaffRole.Security: return Load(StaffRole.Security, Balance.StudentsPerGuard);
                case StaffRole.Nurse: return Load(StaffRole.Nurse, Balance.StudentsPerNurse);
                case StaffRole.Counselor: return Load(StaffRole.Counselor, Balance.StudentsPerCounselor);
                case StaffRole.Coach:
                    foreach (var t in S.Teams)
                        if (t.CoachId == s.Id) return t.Intensity == TrainingIntensity.Intense ? 1.3f : 0.9f;
                    return 0.5f;
                default: return 0.8f;
            }
        }

        void UpdateMorale(StaffMember s, int difficultCount)
        {
            var b = s.MoraleFactors;
            b.Clear();
            b.Add("Base", 55f);
            float ratio = s.Salary / Math.Max(1f, MarketValue(s));
            b.Add("Pay vs market", MathUtil.Clamp((ratio - 1f) * 120f, -25f, 20f));
            b.Add("Energy", (s.Energy - 50f) * 0.2f);
            b.Add("School prestige", (S.Prestige - 40f) * 0.12f);
            b.Add("Student mood", (S.Stats[(int)StatId.StudentSatisfaction] - 50f) * 0.08f);
            b.Add(Ix.StaffLoungeQuality > 0 ? "Staff lounge" : "No staff lounge", Ix.StaffLoungeQuality > 0 ? Ix.StaffLoungeQuality * 0.06f : -3f);
            int others = difficultCount - (s.Has(StaffTrait.Difficult) ? 1 : 0);
            if (others > 0) b.Add("Difficult colleagues", -Math.Min(8f, others * 2f));
            if (s.Role == StaffRole.Teacher) b.Add("Classroom facilities", (Ix.ClassroomQuality - 50f) * 0.05f);
            if (s.Has(StaffTrait.Loyal)) b.Add("Loyal", 3f);
            foreach (var m in s.Moodlets) b.Add(m.Label, m.Value);

            s.Morale = MathUtil.Clamp100(MathUtil.Approach(s.Morale, b.Clamped, 0.03f));
        }

        // ------------------------------------------------------------------------------
        // Monthly: raise requests, poaching, resignations, renewals, job market
        // ------------------------------------------------------------------------------

        public void Monthly()
        {
            int day = S.Day;
            var rng = _sim.Rng;
            if (day == 0 || (S.Tutorial.Active && day < Balance.TutorialQuietDays))
            {
                RefreshCandidates();   // a new director gets a few weeks before the staff start asking
                return;
            }

            foreach (var s in new List<StaffMember>(S.Staff))
            {
                if (s.LeavingDay >= 0) continue;
                float market = MarketValue(s);

                if (!s.RenewalOffered && s.ContractEndDay - day <= 35)
                {
                    s.RenewalOffered = true;
                    _sim.Events.TriggerDirected("contract_renewal", m => m.StaffId = s.Id, ignoreCooldown: true);
                    continue;
                }

                if (day - s.LastRaiseRequestDay > 150 && (day - s.LastRaiseDay > 300 || s.Salary < market * 0.9f))
                {
                    float chance = s.Has(StaffTrait.Ambitious) ? 0.45f : 0.22f;
                    if (s.Salary < market * 0.9f) chance += 0.15f;
                    if (rng.Chance(chance) && _sim.Events.TriggerDirected("raise_request", m => m.StaffId = s.Id, ignoreCooldown: true))
                    {
                        s.LastRaiseRequestDay = day;
                        continue;
                    }
                }

                float effect = s.Role == StaffRole.Teacher ? _sim.Stats.TeacherEffect(s) : s.Skill;
                if (effect >= 60f && day - s.LastPoachDay > 240)
                {
                    float chance = 0.03f * (1f + (effect - 60f) / 40f) * (1f - s.Loyalty / 200f);
                    if (s.Has(StaffTrait.Ambitious)) chance *= 2f;
                    if (s.Has(StaffTrait.Loyal)) chance *= 0.3f;
                    if (rng.Chance(chance) && _sim.Events.TriggerDirected("poach_offer", m => m.StaffId = s.Id, ignoreCooldown: true))
                    {
                        s.LastPoachDay = day;
                        continue;
                    }
                }

                if (s.Morale < 30f)
                {
                    float chance = (30f - s.Morale) / 100f * (s.Has(StaffTrait.Loyal) ? 0.3f : 1f);
                    if (rng.Chance(chance) && _sim.Events.TriggerDirected("resignation_notice", m => m.StaffId = s.Id, ignoreCooldown: true))
                        continue;
                }

                if (s.Energy < 15f) _sim.Events.TriggerDirected("staff_burnout", m => m.StaffId = s.Id, ignoreCooldown: true);
            }

            RefreshCandidates();
        }

        // ------------------------------------------------------------------------------
        // Salaries
        // ------------------------------------------------------------------------------

        public float MarketValue(StaffMember s)
        {
            float skill = s.Skill;
            float exp = Math.Min(s.Experience, 20f);
            float premium = skill > 60f ? (skill - 60f) * (skill - 60f) * 2f : 0f;
            float v;
            switch (s.Role)
            {
                case StaffRole.Teacher: v = 2200f + skill * 30f + premium + exp * 55f; break;
                case StaffRole.Coach: v = 2000f + skill * 28f + premium + exp * 50f; break;
                case StaffRole.Custodian: v = 1700f + skill * 6f; break;
                case StaffRole.Maintenance: v = 1900f + skill * 9f; break;
                case StaffRole.Chef: v = 1800f + skill * 14f; break;
                case StaffRole.Nurse: v = 2800f + skill * 16f; break;
                case StaffRole.Security: v = 2000f + skill * 10f; break;
                case StaffRole.Counselor: v = 2700f + skill * 20f; break;
                case StaffRole.Administrator: v = 3200f + skill * 40f + premium + exp * 60f; break;
                default: v = 2500f; break;
            }
            if (s.Has(StaffTrait.Popular)) v *= 1.05f;
            if (s.Has(StaffTrait.ExcellentTeacher)) v *= 1.1f;
            return v * S.WageIndex * Balance.PrestigeWageFactor(S.Prestige);
        }

        public void SetSalary(StaffMember s, float newSalary)
        {
            newSalary = (float)Math.Round(newSalary / 10f) * 10f;
            float old = s.Salary;
            if (newSalary > old)
            {
                float pct = newSalary / old - 1f;
                s.LastRaiseDay = S.Day;
                s.Moodlets.Set("Recent raise", Math.Min(15f, pct * 150f), S.Day + 90);
                s.Loyalty = MathUtil.Clamp100(s.Loyalty + Math.Min(10f, pct * 80f));
            }
            else if (newSalary < old)
            {
                s.Moodlets.Set("Pay cut", -20f, S.Day + 120);
                s.Loyalty = MathUtil.Clamp100(s.Loyalty - 15f);
            }
            s.Salary = newSalary;
        }

        public float Severance(StaffMember s)
        {
            float monthsLeft = Math.Max(0, s.ContractEndDay - S.Day) / (float)Calendar.DaysPerMonth;
            return Math.Max(1f, monthsLeft * 0.5f) * s.Salary;
        }

        // ------------------------------------------------------------------------------
        // Hiring and firing
        // ------------------------------------------------------------------------------

        public StaffMember CreateStaff(StaffRole role, float skillMean, AdminRole adminRole = AdminRole.None, SportType sport = SportType.None)
        {
            var rng = _sim.Rng;
            var s = new StaffMember
            {
                Id = S.NewId(),
                Name = Names.Person(rng),
                Role = role,
                AdminRole = adminRole,
                Skill = rng.Stat(skillMean, 12f),
                Patience = rng.Stat(50f, 15f),
                Charisma = rng.Stat(50f, 15f),
                Recruitment = rng.Stat(50f, 15f),
                Morale = 65f,
                Energy = 90f,
                Loyalty = 45f + rng.Range(-10f, 10f),
            };
            s.Experience = MathUtil.Clamp(rng.Gaussian(3f + s.Skill / 12f, 3f), 0f, 35f);
            s.ClassroomManagement = rng.Stat(s.Skill * 0.8f + 10f, 12f);
            s.Strategy = rng.Stat(s.Skill, 12f);
            s.PlayerDevelopment = rng.Stat(s.Skill, 12f);
            s.Traits.AddRange(TraitRules.RollStaffTraits(rng, rng.Range(1, 3)));

            if (role == StaffRole.Teacher)
                s.Subject = (Subject)rng.Range(1, Enum.GetValues(typeof(Subject)).Length);
            if (role == StaffRole.Coach)
                s.Sport = sport != SportType.None ? sport : (SportType)rng.Range(1, Enum.GetValues(typeof(SportType)).Length);

            s.Salary = (float)Math.Round(MarketValue(s) / 10f) * 10f;
            return s;
        }

        public void RefreshCandidates()
        {
            int day = S.Day;
            S.Candidates.RemoveAll(c => c.ExpiresDay <= day);
            int target = Math.Min(14, 6 + S.Progress.SchoolLevel / 3);
            var rng = _sim.Rng;

            while (S.Candidates.Count < target)
            {
                var role = PickCandidateRole();
                var admin = AdminRole.None;
                if (role == StaffRole.Administrator) admin = PickOpenAdminRole();

                float skillMean = 35f + S.Prestige * 0.4f;
                if (rng.Chance(0.15f)) skillMean += 25f;       // the occasional star
                var s = CreateStaff(role, skillMean, admin);
                s.HiredDay = -1;

                float minPrestige = s.Skill > 55f ? (s.Skill - 55f) * 1.1f : 0f;
                S.Candidates.Add(new Candidate
                {
                    Staff = s,
                    AskingSalary = (float)Math.Round(MarketValue(s) * rng.Range(0.95f, 1.15f) / 10f) * 10f,
                    MinPrestige = minPrestige,
                    ExpiresDay = day + rng.Range(30, 60),
                });
            }
        }

        StaffRole PickCandidateRole()
        {
            var roles = new List<StaffRole>
            {
                StaffRole.Teacher, StaffRole.Custodian, StaffRole.Maintenance, StaffRole.Chef,
                StaffRole.Nurse, StaffRole.Security, StaffRole.Counselor,
            };
            if (_sim.IsUnlocked(Feature.SportsPrograms)) roles.Add(StaffRole.Coach);
            if (_sim.IsUnlocked(Feature.Administrators) && PickOpenAdminRole() != AdminRole.None) roles.Add(StaffRole.Administrator);

            return _sim.Rng.PickWeighted(roles, r =>
            {
                switch (r)
                {
                    case StaffRole.Teacher: return 3f + (Ix.StudentsPerTeacher > 14f ? 2f : 0f);
                    case StaffRole.Chef: return Ix.ChefCoverage < 1f ? 2.5f : 0.8f;
                    case StaffRole.Custodian: return Ix.CustodianCoverage < 1f ? 2.5f : 0.8f;
                    case StaffRole.Security: return Ix.GuardCoverage < 1f ? 2f : 0.6f;
                    case StaffRole.Coach: return 1.5f;
                    case StaffRole.Administrator: return 1.2f;
                    default: return 0.8f;
                }
            });
        }

        AdminRole PickOpenAdminRole()
        {
            var open = new List<AdminRole>();
            foreach (AdminRole r in Enum.GetValues(typeof(AdminRole)))
            {
                if (r == AdminRole.None) continue;
                if (r == AdminRole.AthleticDirector && !_sim.IsUnlocked(Feature.SportsPrograms)) continue;
                bool filled = false;
                foreach (var s in S.Staff) if (s.AdminRole == r) { filled = true; break; }
                foreach (var c in S.Candidates) if (c.Staff.AdminRole == r) { filled = true; break; }
                if (!filled) open.Add(r);
            }
            return open.Count == 0 ? AdminRole.None : _sim.Rng.Pick(open);
        }

        public int AdminDesks()
        {
            int desks = 0;
            foreach (var r in _sim.Operational(RoomType.Office)) desks += r.Capacity;
            return desks;
        }

        public bool CanHire(Candidate c, out string reason)
        {
            if (S.Prestige < c.MinPrestige)
            {
                reason = $"Only considers schools with prestige {c.MinPrestige:0}+";
                return false;
            }
            if (c.Staff.Role == StaffRole.Administrator)
            {
                int admins = _sim.CountStaff(StaffRole.Administrator, activeOnly: false);
                if (admins >= AdminDesks())
                {
                    reason = "No free office desk. Build or upgrade an Administration Office.";
                    return false;
                }
                foreach (var s in S.Staff)
                    if (s.AdminRole == c.Staff.AdminRole) { reason = "That position is already filled"; return false; }
            }
            reason = null;
            return true;
        }

        public static float ContractSalaryFactor(int years) => years >= 5 ? 0.94f : years >= 3 ? 0.97f : 1f;

        public StaffMember Hire(Candidate c, int years)
        {
            if (!CanHire(c, out _)) return null;
            var s = c.Staff;
            years = years >= 5 ? 5 : years >= 3 ? 3 : 1;
            s.Salary = (float)Math.Round(c.AskingSalary * ContractSalaryFactor(years) / 10f) * 10f;
            s.ContractYears = years;
            s.HiredDay = S.Day;
            s.ContractEndDay = S.Day + years * Calendar.DaysPerYear;
            s.LastRaiseDay = S.Day;
            s.Loyalty = MathUtil.Clamp100(s.Loyalty + (years >= 5 ? 18f : years >= 3 ? 10f : 0f));
            S.Candidates.Remove(c);
            S.Staff.Add(s);
            _sim.Log($"Hired {s.Name} as {s.Title} ({MathUtil.Money(s.Salary)}/mo, {years}-year contract).", important: true);
            return s;
        }

        public float Fire(StaffMember s)
        {
            float severance = Severance(s);
            S.Finance.Post(S.Day, LedgerCategory.Severance, -severance, "Severance for " + s.Name);
            if (s.Has(StaffTrait.Popular) && s.Role == StaffRole.Teacher)
                _sim.AllStudentsMood($"Favorite teacher {s.Name} was fired", -5f, 20);
            bool wasDifficult = s.Has(StaffTrait.Difficult);
            Depart(s, $"fired (severance {MathUtil.Money(severance)})");
            _sim.AllStaffMood(wasDifficult ? "Difficult colleague gone" : "A colleague was fired", wasDifficult ? 3f : -3f, 20);
            return severance;
        }

        public void Depart(StaffMember s, string reason)
        {
            if (!S.Staff.Remove(s)) return;
            foreach (var t in S.Teams) if (t.CoachId == s.Id) t.CoachId = -1;
            foreach (var p in S.Promises)
                if (!p.Resolved && p.Kind == PromiseKind.SalaryReview && p.TargetId == s.Id) { p.Resolved = true; p.Kept = true; }
            S.Year.StaffLeft++;
            _sim.Log($"{s.Name} ({s.Title}) left the school: {reason}.", important: true);
            if (s.Role == StaffRole.Teacher && s.Skill >= 65f)
                _sim.News.Publish(NewsTone.Bad, NewsTopic.Staff, $"Star teacher leaves {S.SchoolName}",
                    $"{s.Name}, one of the best teachers at {S.SchoolName}, has left the school. Students say they'll miss the lessons.",
                    -1f, ReputationId.Academic, 45, NewsSystem.Courier);
        }

        // ------------------------------------------------------------------------------
        // Training
        // ------------------------------------------------------------------------------

        /// <summary>What a program costs this school (a bookish assistant finds cheaper courses).</summary>
        public float TrainingCost(TrainingProgram p) => (float)Math.Round(Balance.TrainingCost(p) * AssistantRules.TrainingMultiplier(S.Assistant));

        public bool CanTrain(StaffMember s, TrainingProgram p, out string reason)
        {
            if (s.IsAway(S.Day)) { reason = "Already away"; return false; }
            if (s.LeavingDay >= 0) { reason = "Leaving the school"; return false; }
            if (S.Finance.Cash < TrainingCost(p)) { reason = "Not enough money"; return false; }
            reason = null;
            return true;
        }

        public bool Train(StaffMember s, TrainingProgram p)
        {
            if (!CanTrain(s, p, out _)) return false;
            float cost = TrainingCost(p);
            S.Finance.Post(S.Day, LedgerCategory.Training, -cost, $"{p} for {s.Name}");
            s.CurrentTraining = p;
            s.AwayUntilDay = S.Day + Balance.TrainingDays(p);
            _sim.Log($"{s.Name} started a {p} ({Balance.TrainingDays(p)} days).");
            return true;
        }

        void CompleteTraining(StaffMember s)
        {
            var rng = _sim.Rng;
            var p = s.CurrentTraining.Value;
            s.CurrentTraining = null;
            float Gain(float current, float min, float max) => rng.Range(min, max) * (1f - current / 130f);

            switch (p)
            {
                case TrainingProgram.Workshop:
                    s.Skill = MathUtil.Clamp100(s.Skill + Gain(s.Skill, 3f, 6f));
                    s.Strategy = MathUtil.Clamp100(s.Strategy + Gain(s.Strategy, 2f, 4f));
                    s.Energy = MathUtil.Clamp100(s.Energy + 10f);
                    break;
                case TrainingProgram.Seminar:
                    s.ClassroomManagement = MathUtil.Clamp100(s.ClassroomManagement + Gain(s.ClassroomManagement, 5f, 9f));
                    s.PlayerDevelopment = MathUtil.Clamp100(s.PlayerDevelopment + Gain(s.PlayerDevelopment, 4f, 7f));
                    s.Patience = MathUtil.Clamp100(s.Patience + 3f);
                    s.Charisma = MathUtil.Clamp100(s.Charisma + 2f);
                    s.Moodlets.Set("Inspired by seminar", 8f, S.Day + 60);
                    break;
                case TrainingProgram.Certification:
                    s.Skill = MathUtil.Clamp100(s.Skill + Gain(s.Skill, 8f, 12f));
                    s.Strategy = MathUtil.Clamp100(s.Strategy + Gain(s.Strategy, 4f, 7f));
                    s.Experience += 0.5f;
                    s.Moodlets.Set("Newly certified", 6f, S.Day + 90);
                    break;
            }
            _sim.Log($"{s.Name} finished their {p}. Skill is now {s.Skill:0}.");
        }

        /// <summary>Paid leave: the staff member is away and recovers energy quickly.</summary>
        public void GiveLeave(StaffMember s, int days)
        {
            s.AwayUntilDay = S.Day + days;
            s.CurrentTraining = null;
        }
    }
}
