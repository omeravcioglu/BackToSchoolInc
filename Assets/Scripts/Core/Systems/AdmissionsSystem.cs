using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Admissions Season (summer) and rolling transfer applications during the year.
    /// Beds are the hard limit: every student boards.
    /// </summary>
    public sealed class AdmissionsSystem
    {
        readonly Simulation _sim;

        public AdmissionsSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;

        public float PriceFactor(float tuition)
        {
            float f = (float)Math.Pow(_sim.FairTuition / Math.Max(1000f, tuition), 1.4);
            return MathUtil.Clamp(f, 0.3f, 1.8f);
        }

        public float ExpectedApplicants(float tuition)
        {
            return Balance.ApplicantBase(S.Prestige) * PriceFactor(tuition)
                   * SpecializationRules.ApplicantMultiplier(S.Specialization) * (1f + S.ApplicantBonus)
                   * (1f + _sim.Indices.SignatureEffect(x => x.Applicants));
        }

        /// <summary>Families from abroad expected next season: more for a prestigious school with a welcoming International House.</summary>
        public float ExpectedInternational() =>
            _sim.IsUnlocked(Feature.InternationalStudents) ? Balance.InternationalApplicants(S.Prestige) * (1f + 1.5f * _sim.Indices.InternationalSupport) : 0f;

        public void OpenSeason()
        {
            _sim.Progression.Unveil(Reveal.Admissions);
            S.Applicants.RemoveAll(a => !a.Transfer);
            float tuition = S.Policies.PendingTuition > 0 ? S.Policies.PendingTuition : S.Policies.AnnualTuition;
            int count = Math.Max(2, (int)Math.Round(ExpectedApplicants(tuition) * _sim.Rng.Range(0.85f, 1.15f)));
            int deadline = Calendar.StartOfMonth(Calendar.Year(S.Day) + 1, 1);

            int athletes = 0;
            for (int i = 0; i < count; i++)
            {
                bool elite = _sim.IsUnlocked(Feature.SportsPrograms) && _sim.Rng.Chance(0.04f + S.Rep(ReputationId.Sports) / 1000f);
                if (elite) athletes++;
                var a = GenerateApplicant(transfer: false, eliteAthlete: elite);
                a.ExpiresDay = deadline;
                S.Applicants.Add(a);
            }
            int abroad = _sim.IsUnlocked(Feature.InternationalStudents) ? (int)Math.Round(ExpectedInternational() * _sim.Rng.Range(0.8f, 1.2f)) : 0;
            int scholars = _sim.IsUnlocked(Feature.EliteInstitution) ? _sim.Rng.Range(2, 5) : 0;
            for (int i = 0; i < abroad + scholars; i++)
            {
                var a = i < abroad ? GenerateApplicant(transfer: false, international: true) : GenerateApplicant(transfer: false, scholar: true);
                a.ExpiresDay = deadline;
                S.Applicants.Add(a);
            }
            S.ApplicantBonus = 0f;

            var extras = new List<string>();
            if (athletes > 0) extras.Add($"{athletes} elite athlete{(athletes == 1 ? "" : "s")}");
            if (abroad > 0) extras.Add($"{abroad} from abroad");
            if (scholars > 0) extras.Add($"{scholars} national scholar{(scholars == 1 ? "" : "s")}");
            int beds = _sim.Facilities.FreeBeds();
            _sim.Inbox.Notify(SenderType.Admissions, "Admissions Office", "Admissions Season is open",
                $"{count + abroad + scholars} families have applied for next year" + (extras.Count > 0 ? ", including " + string.Join(" and ", extras) : "") +
                $". You have {beds} free bed{(beds == 1 ? "" : "s")}. Decide before the school year starts on {Calendar.Format(deadline)}.\n\n" +
                "Each admission is an offer: families with low interest may still choose a rival school. Aid and scholarships raise acceptance.",
                MessagePriority.Important);
            _sim.Log($"Admissions Season opened: {count} applicants.", important: true);
        }

        /// <summary>
        /// A family applying. Families from abroad are wealthier, ask for no aid and pay the international
        /// fee; national scholars (an elite institution's draw) are the best students in the country.
        /// </summary>
        public Applicant GenerateApplicant(bool transfer, bool eliteAthlete = false, bool international = false, bool scholar = false)
        {
            var rng = _sim.Rng;
            var spec = S.Specialization;
            float prestige = S.Prestige;
            float tuition = S.Policies.AnnualTuition;

            float academic = 40f + prestige * 0.35f + SpecializationRules.ApplicantAcademicShift(spec) + (international ? 4f : 0f);
            float athletic = 45f + (_sim.IsUnlocked(Feature.SportsPrograms) ? S.Rep(ReputationId.Sports) * 0.15f : 0f)
                             + SpecializationRules.ApplicantAthleticShift(spec);
            float wealth = 45f + MathUtil.Clamp((tuition / _sim.FairTuition - 1f) * 50f, -10f, 25f) + SpecializationRules.ApplicantWealthShift(spec)
                           + (international ? 18f : 0f);
            float artistic = 45f + SpecializationRules.ApplicantArtisticShift(spec);

            int grade = transfer ? rng.Range(1, 4) : 1;
            var s = StudentFactory.Create(_sim, grade, academic, athletic, wealth, artistic);

            if (eliteAthlete)
            {
                s.Athletic = rng.Range(85f, 98f);
                s.Academic = rng.Range(25f, 55f);
                s.Grades = MathUtil.Clamp100(s.Academic * 0.7f + 15f);
                s.SportTalent = (SportType)rng.Range(1, Enum.GetValues(typeof(SportType)).Length);
            }
            if (scholar)
            {
                s.Academic = rng.Range(88f, 99f);
                s.Discipline = Math.Max(s.Discipline, rng.Range(70f, 90f));
                s.Grades = MathUtil.Clamp100(s.Academic * 0.7f + 25f);
            }
            if (international)
            {
                s.HomeCountry = Names.Country(rng);
                s.Homesick = rng.Range(55f, 85f);
            }

            float aid = 0f;
            if (scholar) aid = 0.3f;   // the merit scholarship every school courting them offers
            else if (international) aid = 0f;
            else if (s.FamilyWealth < 45f) aid = MathUtil.Clamp((45f - s.FamilyWealth) / 100f + rng.Range(0f, 0.2f), 0.05f, 0.6f);
            else if (rng.Chance(0.1f)) aid = 0.1f;
            aid = (float)Math.Round(aid * 20f) / 20f;

            float fair = _sim.FairTuition;
            float interest = 0.6f + (prestige - 30f) / 150f + rng.Range(-0.15f, 0.15f) - Math.Max(0f, (tuition - fair) / fair) * 0.8f;
            if (eliteAthlete) interest -= 0.2f - S.Rep(ReputationId.Sports) / 250f;
            if (s.Academic > 80f) interest -= 0.1f;   // strong students have options
            if (international) interest += 0.2f * _sim.Indices.InternationalSupport - 0.1f;   // a home from home reassures families far away
            interest += _sim.Indices.Beauty * 0.06f;   // campus tours: a lovely campus wins families over

            return new Applicant
            {
                Student = s,
                RequestedAid = aid,
                Interest = MathUtil.Clamp(interest, 0.1f, 0.95f),
                Transfer = transfer,
                EliteAthlete = eliteAthlete,
                Scholar = scholar,
                Pitch = Pitch(s, eliteAthlete, scholar),
            };
        }

        static string Pitch(Student s, bool elite, bool scholar)
        {
            if (elite) return $"Elite {s.SportTalent} prospect; weak academics.";
            if (scholar) return "A national scholar: top marks in the country.";
            if (s.Academic >= 80f) return "Top of their class.";
            if (s.Has(StudentTrait.Troublemaker)) return "Bright but has a history of trouble.";
            if (s.FamilyWealth >= 80f) return "Wealthy family, well connected.";
            if (s.Artistic >= 80f) return "Gifted artist.";
            if (s.Athletic >= 75f) return "Strong athlete.";
            if (s.Has(StudentTrait.Leader)) return "Natural leader.";
            return "Solid all-round student.";
        }

        /// <summary>Chance the family accepts an offer with this aid.</summary>
        public float AcceptChance(Applicant a, float discount, ScholarshipType type)
        {
            float p = a.Interest + discount * 0.9f + (type != ScholarshipType.None ? 0.1f : 0f);
            return MathUtil.Clamp(p, 0.05f, 0.98f);
        }

        public float Score(Applicant a)
        {
            var s = a.Student;
            return s.Academic * 0.5f + s.Athletic * 0.2f + s.Discipline * 0.2f + s.FamilyWealth * 0.1f;
        }

        public bool CanAdmit(Applicant a, ScholarshipType type, out string reason)
        {
            if (_sim.Facilities.FreeBeds() <= 0) { reason = "No free beds. Build or upgrade dorm rooms."; return false; }
            if (type != ScholarshipType.None && type != ScholarshipType.FinancialAid && !_sim.IsUnlocked(Feature.Scholarships))
            {
                reason = $"Scholarships unlock at school level {Balance.UnlockLevel(Feature.Scholarships)}";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>Makes an offer. Returns true if the family accepted and the student enrolled.</summary>
        public bool Admit(Applicant a, ScholarshipType type, float discount, out string result)
        {
            if (!CanAdmit(a, type, out result)) return false;
            S.Applicants.Remove(a);
            var s = a.Student;
            discount = MathUtil.Clamp(discount, 0f, 1f);

            if (!_sim.Rng.Chance(AcceptChance(a, discount, type)))
            {
                var rival = _sim.Rivals.RandomRival();
                result = $"{s.FullName}'s family declined your offer and chose {rival?.Name ?? "another school"}.";
                _sim.Log(result);
                return false;
            }

            s.Scholarship = type;
            s.Discount = discount;
            _sim.Students.Enroll(s);
            if (type != ScholarshipType.None) s.Remember(S.Day, $"Awarded a {type} scholarship ({discount:P0}).");
            else if (discount > 0) s.Remember(S.Day, $"Admitted with {discount:P0} tuition aid.");
            if (a.EliteAthlete) S.Year.Highlight($"Signed elite {s.SportTalent} prospect {s.FullName}");

            result = $"{s.FullName} accepted and moved into {S.FindRoom(s.DormRoomId)?.Name ?? "(no bed)"}.";
            _sim.Log(result);
            return true;
        }

        public void Reject(Applicant a)
        {
            S.Applicants.Remove(a);
        }

        public void Monthly() { }

        public bool MarketingActive => S.MarketingUntilDay > S.Day;

        /// <summary>Expected transfer applications per month right now.</summary>
        public float TransferRate()
        {
            float rate = Balance.TransferRate(S.Prestige) * PriceFactor(S.Policies.AnnualTuition);
            if (_sim.Facilities.FreeBeds() == 0) rate *= 0.25f;
            if (MarketingActive) rate *= 1.8f;
            return rate;
        }

        /// <summary>Weekly roll for mid-year transfer applications (September to April).</summary>
        void RollTransfers()
        {
            if (S.Day == 0 || Calendar.Month(S.Day) > 8 || Calendar.DayOfWeek(S.Day) != 0) return;
            if (S.Tutorial.Active && S.Day < Balance.TutorialQuietDays) return;
            float expected = TransferRate() / 4.3f;
            int count = (int)expected + (_sim.Rng.Chance(expected - (int)expected) ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                bool abroad = _sim.IsUnlocked(Feature.InternationalStudents) && _sim.Rng.Chance(0.15f + 0.15f * _sim.Indices.InternationalSupport);
                var a = GenerateApplicant(transfer: true, international: abroad);
                a.ExpiresDay = S.Day + 15;
                S.Applicants.Add(a);
                _sim.Events.TriggerDirected("transfer_applicant", m => m.OtherId = a.Student.Id, ignoreCooldown: true);
            }
        }

        public bool RunMarketing()
        {
            if (S.Finance.Cash < Balance.MarketingCost || MarketingActive) return false;
            S.Finance.Post(S.Day, LedgerCategory.Other, -Balance.MarketingCost, "Marketing campaign");
            S.MarketingUntilDay = S.Day + Balance.MarketingDays;
            S.ApplicantBonus = Math.Min(0.5f, S.ApplicantBonus + 0.08f);
            _sim.Log("Marketing campaign launched: more transfer applications for 30 days and a bigger admissions pool.", important: true);
            return true;
        }

        public Applicant FindApplicant(int studentId)
        {
            foreach (var a in S.Applicants) if (a.Student.Id == studentId) return a;
            return null;
        }

        public void DailyTick()
        {
            S.Applicants.RemoveAll(a => a.ExpiresDay <= S.Day);
            RollTransfers();
            if (!Calendar.IsAdmissionsSeason(S.Day)) return;
            RunAdmissionsDirector();
        }

        void RunAdmissionsDirector()
        {
            var admin = _sim.FindAdmin(AdminRole.AdmissionsDirector);
            if (admin == null) return;
            var pol = S.AdminPolicies;

            var pool = S.Applicants.FindAll(a => !a.Transfer);
            pool.Sort((x, y) => Score(y).CompareTo(Score(x)));

            bool deadline = Calendar.Month(S.Day) == 12 && Calendar.DayOfMonth(S.Day) >= 24;
            int offersToday = deadline ? 99 : 1;

            foreach (var a in pool)
            {
                if (offersToday <= 0 || _sim.Facilities.FreeBeds() <= 0) break;
                bool qualifies = a.Student.Academic >= pol.AutoAdmitAcademicAbove || (deadline && pol.FillRemainingBedsAtDeadline);
                if (!qualifies) continue;

                float discount = a.RequestedAid <= pol.AutoApproveDiscountBelow
                    ? a.RequestedAid
                    : pol.RejectLargerDiscounts ? 0f : pol.AutoApproveDiscountBelow;
                var type = discount > 0f && _sim.IsUnlocked(Feature.Scholarships)
                    ? (a.Student.Academic >= 80f ? ScholarshipType.Academic : ScholarshipType.FinancialAid)
                    : ScholarshipType.None;

                // Weak directors misjudge applicants now and then.
                if (_sim.Rng.Chance((1f - admin.Skill / 100f) * 0.15f)) discount = Math.Min(0.5f, discount + 0.15f);

                Admit(a, type, discount, out string result);
                _sim.Log($"Admissions Director {admin.Name}: {result}");
                offersToday--;
            }
        }
    }
}
