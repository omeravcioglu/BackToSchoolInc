using System;

namespace SchoolSim
{
    public sealed class TuitionEstimate
    {
        public float CurrentTuition;
        public float NewTuition;
        public float FairTuition;
        public float MonthlyRevenueChange;      // during school months
        public float CurrentRetention;          // expected share of families re-enrolling
        public float NewRetention;
        public float ParentReaction;            // average change in parent satisfaction target
        public float ApplicantChange;           // fraction change in next season's applications

        public string Summary =>
            $"Revenue {MathUtil.Signed(MonthlyRevenueChange, "N0")}/mo · Re-enrollment {CurrentRetention:P0} → {NewRetention:P0} · " +
            $"Parent reaction {MathUtil.Signed(ParentReaction, "0.0")} · Applications {MathUtil.Signed(ApplicantChange * 100f, "0")}%";
    }

    public sealed class MonthlyProjection
    {
        public float Tuition, DormFees, Sponsorship, Merchandise, Signature, Salaries, Food, Upkeep, Utilities, Programs, Loans;
        public float Income => Tuition + DormFees + Sponsorship + Merchandise + Signature;
        public float Expenses => Salaries + Food + Upkeep + Utilities + Programs + Loans;
        public float Net => Income - Expenses;
    }

    /// <summary>Billing, recurring costs, tuition policy and solvency.</summary>
    public sealed class EconomySystem
    {
        readonly Simulation _sim;

        public EconomySystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;
        Finance F => _sim.State.Finance;

        public void StartMonth()
        {
            F.StartMonth(Calendar.Year(S.Day), Calendar.Month(S.Day));
        }

        public void DailyTick()
        {
            if (!_sim.InSession || S.Students.Count == 0) return;
            float meals = S.Students.Count * Balance.MealsPerDay * Balance.MealCost(S.Policies.Food);
            F.Post(S.Day, LedgerCategory.Food, -meals);
        }

        public void MonthlyBilling()
        {
            int day = S.Day;
            int month = Calendar.Month(day);
            bool session = month <= Calendar.LastSchoolMonth;

            if ((month == 1 || month == Calendar.FirstSemesterEndMonth + 1) && S.Policies.PendingTuition > 0f)
                ApplyPendingTuition();

            float tuitionMonth = S.Policies.AnnualTuition / Balance.TuitionInstallments;
            float tuition = 0f, abroad = 0f, dorm = 0f, forgone = 0f;
            foreach (var s in S.Students)
            {
                tuition += tuitionMonth * (1f - s.Discount);
                forgone += tuitionMonth * s.Discount;
                if (s.International) abroad += tuitionMonth * (1f - s.Discount) * Balance.InternationalFeePremium;
                if (session) dorm += _sim.EffectiveDormFee(S.FindRoom(s.DormRoomId));
            }
            F.Post(day, LedgerCategory.Tuition, tuition, "Monthly tuition");
            F.Post(day, LedgerCategory.Tuition, abroad, "International fees");
            F.Post(day, LedgerCategory.DormFees, dorm, "Dormitory fees");
            F.Current.ScholarshipsForgone += forgone;

            F.Post(day, LedgerCategory.Salaries, -Payroll(), "Payroll");

            F.Post(day, LedgerCategory.Upkeep, -UpkeepTotal(), "Facility upkeep");
            F.Post(day, LedgerCategory.Utilities, -UtilitiesTotal(), "Utilities and internet");

            if (session)
            {
                if (S.Teams.Count > 0) F.Post(day, LedgerCategory.SportsOps, -S.Teams.Count * 600f, "Team travel and equipment");
                if (S.Clubs.Count > 0) F.Post(day, LedgerCategory.ClubOps, -S.Clubs.Count * 150f, "Club budgets");
                float merch = MerchandiseEstimate();
                if (merch > 0f) F.Post(day, LedgerCategory.Merchandise, merch, "Merchandise sales");
            }

            for (int i = F.Sponsorships.Count - 1; i >= 0; i--)
            {
                var sp = F.Sponsorships[i];
                if (sp.EndDay <= day)
                {
                    _sim.Log($"Sponsorship with {sp.Company} ended.");
                    F.Sponsorships.RemoveAt(i);
                    continue;
                }
                F.Post(day, LedgerCategory.Sponsorship, sp.Monthly, "Sponsorship: " + sp.Company);
            }

            for (int i = F.Loans.Count - 1; i >= 0; i--)
            {
                var loan = F.Loans[i];
                F.Post(day, LedgerCategory.Other, -loan.MonthlyPayment, "Loan repayment: " + loan.Lender);
                if (--loan.PaymentsLeft <= 0)
                {
                    F.Loans.RemoveAt(i);
                    _sim.Log($"Loan from {loan.Lender} fully repaid.", important: true);
                }
            }

            float grantPer = SpecializationRules.AidGrantPerStudent(S.Specialization);
            if (grantPer > 0f && session)
            {
                int aid = 0;
                foreach (var s in S.Students) if (s.Scholarship == ScholarshipType.FinancialAid) aid++;
                if (aid > 0) F.Post(day, LedgerCategory.Grants, aid * grantPer, "Community education grant");
            }

            var signature = _sim.Indices.SignatureBuilding;
            if (signature != null && signature.Income > 0f && signature.IncomeInSummer != session)
                F.Post(day, signature.IncomeCategory, SignatureIncome(), signature.IncomeNote);

            if (month == 1 && S.Endowment > 0f) EndowmentPayout();
        }

        /// <summary>This month's takings of the signature building (event nights, shows, partners, the council, summer school).</summary>
        public float SignatureIncome() => _sim.Indices.SignatureEffect(x => x.Income);

        /// <summary>Staff salaries plus the assistant's.</summary>
        public float Payroll()
        {
            float salaries = S.Assistant != null ? S.Assistant.Salary : 0f;
            foreach (var s in S.Staff) salaries += s.Salary;
            return salaries;
        }

        public float UpkeepTotal()
        {
            float upkeep = 0f;
            foreach (var r in S.Rooms) if (r.IsBuilt(S.Day)) upkeep += r.Upkeep;
            return upkeep * SpecializationRules.UpkeepMultiplier(S.Specialization) * AssistantRules.UpkeepMultiplier(S.Assistant);
        }

        public float UtilitiesTotal()
        {
            int built = 0;
            foreach (var r in S.Rooms) if (r.IsBuilt(S.Day)) built++;
            return built * Balance.UtilitiesPerRoom
                   + Balance.InternetMonthlyCost(S.Policies.InternetLevel) * SpecializationRules.InternetCostMultiplier(S.Specialization);
        }

        public float MerchandiseEstimate()
        {
            if (S.Teams.Count == 0) return 0f;
            float sports = S.Rep(ReputationId.Sports);
            return ((float)Math.Pow(sports, 1.5) * 1.2f + S.Students.Count * 2f) * SpecializationRules.MerchMultiplier(S.Specialization);
        }

        public MonthlyProjection Project()
        {
            var p = new MonthlyProjection();
            float tuitionMonth = S.Policies.AnnualTuition / Balance.TuitionInstallments;
            foreach (var s in S.Students)
            {
                p.Tuition += tuitionMonth * (1f - s.Discount) * (s.International ? 1f + Balance.InternationalFeePremium : 1f);
                if (_sim.InSession) p.DormFees += _sim.EffectiveDormFee(S.FindRoom(s.DormRoomId));
            }
            foreach (var sp in F.Sponsorships) p.Sponsorship += sp.Monthly;
            p.Merchandise = MerchandiseEstimate();
            var signature = _sim.Indices.SignatureBuilding;
            if (signature != null && signature.IncomeInSummer != _sim.InSession) p.Signature = SignatureIncome();
            p.Salaries = Payroll();
            if (_sim.InSession) p.Food = S.Students.Count * Balance.MealsPerDay * Balance.MealCost(S.Policies.Food) * Calendar.DaysPerMonth;
            p.Upkeep = UpkeepTotal();
            p.Utilities = UtilitiesTotal();
            p.Programs = S.Teams.Count * 600f + S.Clubs.Count * 150f;
            foreach (var l in F.Loans) p.Loans += l.MonthlyPayment;
            return p;
        }

        // ------------------------------------------------------------------------------
        // Tuition
        // ------------------------------------------------------------------------------

        public TuitionEstimate EstimateTuition(float newTuition)
        {
            float old = S.Policies.AnnualTuition;
            var est = new TuitionEstimate
            {
                CurrentTuition = old,
                NewTuition = newTuition,
                FairTuition = _sim.FairTuition,
                ApplicantChange = _sim.Admissions.PriceFactor(newTuition) / _sim.Admissions.PriceFactor(old) - 1f,
            };

            int n = 0;
            float retOld = 0f, retNew = 0f, reaction = 0f;
            float pct = old > 0 ? newTuition / old - 1f : 0f;
            foreach (var s in S.Students)
            {
                est.MonthlyRevenueChange += (newTuition - old) / Balance.TuitionInstallments * (1f - s.Discount);
                reaction += _sim.Students.TuitionValueTerm(s, newTuition) - _sim.Students.TuitionValueTerm(s, old);
                reaction += TuitionChangeShock(s, pct);
                if (s.Grade >= 4) continue;
                retOld += _sim.Students.RetentionChance(s, old);
                retNew += _sim.Students.RetentionChance(s, newTuition);
                n++;
            }

            est.CurrentRetention = n == 0 ? 1f : retOld / n;
            est.NewRetention = n == 0 ? 1f : retNew / n;
            est.ParentReaction = S.Students.Count == 0 ? 0f : reaction / S.Students.Count;
            return est;
        }

        float TuitionChangeShock(Student s, float pct)
        {
            if (pct > 0.001f) return -Math.Min(20f, pct * 60f * _sim.Students.TuitionSensitivity(s));
            if (pct < -0.001f) return Math.Min(10f, -pct * 40f);
            return 0f;
        }

        /// <summary>Tuition changes take effect at the start of the next semester.</summary>
        public void SetTuition(float annual)
        {
            annual = (float)Math.Round(MathUtil.Clamp(annual, 1000f, 80000f) / 100f) * 100f;
            S.Policies.PendingTuition = Math.Abs(annual - S.Policies.AnnualTuition) < 1f ? -1f : annual;
        }

        void ApplyPendingTuition()
        {
            float old = S.Policies.AnnualTuition;
            float next = S.Policies.PendingTuition;
            S.Policies.AnnualTuition = next;
            S.Policies.PendingTuition = -1f;
            float pct = next / old - 1f;

            foreach (var s in S.Students)
            {
                float shock = TuitionChangeShock(s, pct);
                if (Math.Abs(shock) > 0.01f)
                    s.ParentMoodlets.Set(pct > 0 ? "Tuition increase" : "Tuition cut", shock, S.Day + 90);
            }
            _sim.Log($"New tuition in effect: {MathUtil.Money(next)} per year ({MathUtil.Signed(pct * 100f, "0")}%).", important: true);

            if (pct >= 0.05f)
                _sim.News.Publish(NewsTone.Bad, NewsTopic.Money, $"{S.SchoolName} raises fees by {pct * 100f:0}%",
                    $"Families at {S.SchoolName} will pay {MathUtil.Money(next)} a year from now on. Not everyone is pleased.",
                    -1f, ReputationId.Overall, 45, NewsSystem.Forum);
            else if (pct <= -0.05f)
                _sim.News.Publish(NewsTone.Good, NewsTopic.Money, $"{S.SchoolName} cuts its fees",
                    $"Good news for families: tuition at {S.SchoolName} drops {-pct * 100f:0}% to {MathUtil.Money(next)} a year.",
                    1f, ReputationId.Overall, 45, NewsSystem.Forum);
        }

        // ------------------------------------------------------------------------------
        // Endowment
        // ------------------------------------------------------------------------------

        static readonly float[] EndowmentMilestones = { 1000000f, 5000000f, 20000000f };

        /// <summary>Puts money into the endowment for good. It pays out each September and lends the school prestige.</summary>
        public void Endow(float amount)
        {
            float before = S.Endowment;
            F.Post(S.Day, LedgerCategory.Endowment, -amount, "Gift to the endowment");
            S.Endowment += amount;
            S.EndowmentGiven += amount;
            _sim.Log($"{MathUtil.Money(amount)} went into the endowment, which now holds {MathUtil.Money(S.Endowment)}.", important: true);
            foreach (float milestone in EndowmentMilestones)
            {
                if (before >= milestone || S.Endowment < milestone) continue;
                S.Year.Highlight($"The endowment passed {MathUtil.Money(milestone)}");
                _sim.News.Publish(NewsTone.Good, NewsTopic.Money, $"{S.SchoolName}'s endowment passes {MathUtil.Money(milestone)}",
                    $"Money given for good: {S.SchoolName} can now count on {MathUtil.Money(S.Endowment * Balance.EndowmentPayout)} a year from its fund.",
                    1f, ReputationId.Overall, 90, NewsSystem.Weekly);
            }
        }

        /// <summary>Each September the fund pays the school its share and grows by the rest of its return.</summary>
        void EndowmentPayout()
        {
            float payout = S.Endowment * Balance.EndowmentPayout;
            F.Post(S.Day, LedgerCategory.Endowment, payout, "Endowment payout");
            S.Endowment *= 1f + Balance.EndowmentGrowth;
            _sim.Log($"The endowment paid out {MathUtil.Money(payout)} for the new school year.");
        }

        // ------------------------------------------------------------------------------
        // Solvency
        // ------------------------------------------------------------------------------

        public void CheckSolvency()
        {
            if (F.Cash >= 0f)
            {
                S.NegativeCashDays = 0;
                return;
            }

            S.NegativeCashDays++;
            if (S.NegativeCashDays == 14)
                _sim.News.Publish(NewsTone.Bad, NewsTopic.Money, $"Money troubles at {S.SchoolName}",
                    $"Suppliers say {S.SchoolName} is paying its bills late. The school's account is said to be overdrawn.",
                    -2f, ReputationId.Overall, 60, NewsSystem.Courier);
            if (S.Day - S.LastLoanOfferDay > 90)
            {
                S.LastLoanOfferDay = S.Day;
                _sim.Events.TriggerDirected("bank_loan", m => { }, ignoreCooldown: true);
            }

            int left = Balance.InsolvencyGraceDays - S.NegativeCashDays;
            if (left == 90 || left == 30 || left == 7)
            {
                _sim.Inbox.Notify(SenderType.Government, "Board of Trustees", $"Insolvency warning: {left} days left",
                    $"The school has been overdrawn for {S.NegativeCashDays} days ({MathUtil.Money(F.Cash)}). " +
                    $"If the account is still negative in {left} days, the board will close the school. Cut costs, raise revenue or take a loan.",
                    MessagePriority.Urgent);
            }

            if (left <= 0)
            {
                S.GameOver = true;
                S.GameOverReason = $"The school stayed insolvent for {Balance.InsolvencyGraceDays} days and the board closed it down.";
                _sim.Log(S.GameOverReason, important: true);
            }
        }

        public void TakeLoan(string lender, float amount, float monthly, int payments)
        {
            F.Post(S.Day, LedgerCategory.Other, amount, "Loan from " + lender);
            F.Loans.Add(new Loan { Lender = lender, MonthlyPayment = monthly, PaymentsLeft = payments });
            _sim.Log($"Took a {MathUtil.Money(amount)} loan from {lender} ({MathUtil.Money(monthly)}/mo for {payments} months).", important: true);
        }
    }
}
