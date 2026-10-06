using System;
using UnityEngine;

namespace SchoolSim.Game
{
    public sealed partial class DebugUI
    {
        static readonly string[] FoodNames = Enum.GetNames(typeof(FoodTier));
        static readonly string[] CurfewNames = Enum.GetNames(typeof(CurfewPolicy));
        static readonly string[] DisciplineNames = Enum.GetNames(typeof(DisciplinePolicy));

        Vector2 _financeScroll, _adminScroll;
        float _tuitionDraft = -1f;

        void DrawFinance()
        {
            _financeScroll = GUILayout.BeginScrollView(_financeScroll);
            GUILayout.BeginHorizontal();

            // Left: ledgers
            GUILayout.BeginVertical(GUILayout.Width(460));
            Section("Money");
            KeyValue("Cash", Money(S.Finance.Cash));
            var p = Sim.Economy.Project();
            Section("Projected month" + (Sim.InSession ? "" : " (summer: no dorm fees or meals)"));
            ProjectionRow("Tuition", p.Tuition);
            ProjectionRow("Dorm fees", p.DormFees);
            if (p.Sponsorship > 0) ProjectionRow("Sponsorships", p.Sponsorship);
            if (p.Merchandise > 0) ProjectionRow("Merchandise", p.Merchandise);
            ProjectionRow("Salaries", -p.Salaries);
            ProjectionRow("Food", -p.Food);
            ProjectionRow("Upkeep", -p.Upkeep);
            ProjectionRow("Utilities", -p.Utilities);
            if (p.Programs > 0) ProjectionRow("Teams and clubs", -p.Programs);
            if (p.Loans > 0) ProjectionRow("Loan repayments", -p.Loans);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Net", _bold, GUILayout.Width(160));
            GUI.color = p.Net >= 0 ? Good : Bad;
            GUILayout.Label(Money(p.Net), _bold);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            Section($"This month so far ({Calendar.MonthName(Calendar.Month(S.Day))})");
            LedgerView(S.Finance.Current);
            var last = S.Finance.LastCompletedMonth;
            if (last != null)
            {
                Section($"Last month ({Calendar.MonthName(last.Month)} Y{last.Year})");
                LedgerView(last);
            }

            if (S.Finance.Loans.Count > 0)
            {
                Section("Loans");
                foreach (var l in S.Finance.Loans) GUILayout.Label($"{l.Lender}: {Money(l.MonthlyPayment)}/mo, {l.PaymentsLeft} payments left", _small);
            }
            if (S.Finance.Sponsorships.Count > 0)
            {
                Section("Sponsorships");
                foreach (var sp in S.Finance.Sponsorships) GUILayout.Label($"{sp.Company}: {Money(sp.Monthly)}/mo until {Calendar.Format(sp.EndDay)}", _small);
            }
            GUILayout.EndVertical();

            // Right: policies
            GUILayout.BeginVertical();
            DrawTuitionPanel();

            Section("Cafeteria menu");
            int food = GUILayout.Toolbar((int)S.Policies.Food, FoodNames);
            if (food != (int)S.Policies.Food) Run(() => Sim.SetFoodTier((FoodTier)food));
            float meals = S.Students.Count * Balance.MealsPerDay * Calendar.DaysPerMonth;
            GUILayout.Label($"{Money(Balance.MealCost(S.Policies.Food))}/meal ≈ {Money(meals * Balance.MealCost(S.Policies.Food))}/mo for {S.Students.Count} students. " +
                            $"Base food quality {Balance.FoodBaseQuality(S.Policies.Food):0}; the kitchen, chefs, cleanliness and capacity adjust it.", _small);

            Section("Curfew");
            int curfew = GUILayout.Toolbar((int)S.Policies.Curfew, CurfewNames);
            if (curfew != (int)S.Policies.Curfew) Run(() => Sim.SetCurfew((CurfewPolicy)curfew));
            GUILayout.Label("Relaxed: happier students (especially night owls), more incidents. Strict: the opposite.", _small);

            Section("Discipline policy");
            int disc = GUILayout.Toolbar((int)S.Policies.Discipline, DisciplineNames);
            if (disc != (int)S.Policies.Discipline) Run(() => Sim.SetDiscipline((DisciplinePolicy)disc));
            GUILayout.Label("Strict: fewer incidents, better safety and discipline over time; troublemakers resent it.", _small);

            Section("Internet");
            int level = S.Policies.InternetLevel;
            GUILayout.Label($"Level {level} · {Money(Balance.InternetMonthlyCost(level) * SpecializationRules.InternetCostMultiplier(S.Specialization))}/mo. Low levels cause outages.", _small);
            if (level < 3)
            {
                float cost = Balance.InternetUpgradeCost(level + 1);
                if (ActionButton($"Upgrade to level {level + 1} ({Money(cost)}, then {Money(Balance.InternetMonthlyCost(level + 1))}/mo)", S.Finance.Cash < cost ? "Not enough money" : null))
                    Run(Sim.UpgradeInternet);
            }

            if (S.Teams.Count > 0)
            {
                Section("Home game tickets");
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Ticket price {Money(S.Policies.TicketPrice)}", GUILayout.Width(140));
                float price = Mathf.Round(GUILayout.HorizontalSlider(S.Policies.TicketPrice, 0f, 40f, GUILayout.Width(200)));
                if (Math.Abs(price - S.Policies.TicketPrice) > 0.5f) Defer(() => Sim.SetTicketPrice(price));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        void ProjectionRow(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _small, GUILayout.Width(160));
            GUI.color = value >= 0 ? Good : Color.white;
            GUILayout.Label(Money(value));
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        void LedgerView(MonthLedger ledger)
        {
            foreach (LedgerCategory c in Enum.GetValues(typeof(LedgerCategory)))
            {
                float v = ledger[c];
                if (Math.Abs(v) < 0.5f) continue;
                ProjectionRow(c.ToString(), v);
            }
            if (ledger.ScholarshipsForgone > 0) GUILayout.Label($"(Aid and scholarships waived: {Money(ledger.ScholarshipsForgone)})", _small);
            GUILayout.Label($"Income {Money(ledger.Income)} · Expenses {Money(ledger.Expenses)} · Net {Money(ledger.Net)}", _bold);
        }

        void DrawTuitionPanel()
        {
            var pol = S.Policies;
            if (_tuitionDraft < 0f) _tuitionDraft = pol.PendingTuition > 0 ? pol.PendingTuition : pol.AnnualTuition;
            Section("Tuition");
            GUILayout.Label($"Current {Money(pol.AnnualTuition)}/year (billed monthly)" +
                            (pol.PendingTuition > 0 ? $" · changing to {Money(pol.PendingTuition)} next semester" : "") +
                            $" · families consider {Money(Sim.FairTuition)} fair for a school of this prestige.", _wrap);
            GUILayout.BeginHorizontal();
            float fair = Sim.FairTuition;
            float draft = GUILayout.HorizontalSlider(_tuitionDraft, fair * 0.5f, fair * 1.8f, GUILayout.Width(300));
            draft = Mathf.Round(draft / 100f) * 100f;
            if (Math.Abs(draft - _tuitionDraft) > 1f) Defer(() => _tuitionDraft = draft);
            GUILayout.Label(Money(_tuitionDraft), _bold, GUILayout.Width(90));
            if (GUILayout.Button("-500", GUILayout.Width(50))) Defer(() => _tuitionDraft = Mathf.Max(1000f, _tuitionDraft - 500f));
            if (GUILayout.Button("+500", GUILayout.Width(50))) Defer(() => _tuitionDraft += 500f);
            GUILayout.EndHorizontal();

            var est = Sim.Economy.EstimateTuition(_tuitionDraft);
            GUILayout.Label("Estimated consequences:", _small);
            GUI.color = est.MonthlyRevenueChange >= 0 ? Good : Bad;
            GUILayout.Label($"Monthly tuition revenue {MathUtil.Signed(est.MonthlyRevenueChange, "N0")}", _note);
            GUI.color = est.NewRetention >= est.CurrentRetention - 0.005f ? Good : Bad;
            GUILayout.Label($"Expected re-enrollment {est.CurrentRetention:P0} → {est.NewRetention:P0}", _note);
            GUI.color = est.ParentReaction >= 0 ? Good : Bad;
            GUILayout.Label($"Parent reaction {MathUtil.Signed(est.ParentReaction, "0.0")} satisfaction", _note);
            GUI.color = est.ApplicantChange >= 0 ? Good : Bad;
            GUILayout.Label($"Applications next season {MathUtil.Signed(est.ApplicantChange * 100f, "0")}%", _note);
            GUI.color = Color.white;

            if (ActionButton("Apply at the start of next semester", Math.Abs(_tuitionDraft - pol.AnnualTuition) < 1f && pol.PendingTuition < 0 ? "No change" : null, GUILayout.Width(300)))
            {
                float value = _tuitionDraft;
                Run(() => Sim.SetTuition(value));
            }
        }

        // ================================================================================
        // Administration and policies
        // ================================================================================

        void DrawAdmin()
        {
            _adminScroll = GUILayout.BeginScrollView(_adminScroll);
            GUILayout.Label("Administration", _h2);
            if (!Sim.IsUnlocked(Feature.Administrators))
            {
                GUILayout.Label($"Administrators unlock at school level {Balance.UnlockLevel(Feature.Administrators)}. Until then, every message is yours to answer.", _wrap);
                GUILayout.EndScrollView();
                return;
            }

            GUILayout.Label($"Directors answer routine messages in their area using the policies below. " +
                            $"Weak directors sometimes misjudge. Office desks: {Sim.CountStaff(StaffRole.Administrator, false)}/{Sim.Staff.AdminDesks()}.", _wrap);
            foreach (AdminRole role in Enum.GetValues(typeof(AdminRole)))
            {
                if (role == AdminRole.None) continue;
                StaffMember admin = null;
                foreach (var s in S.Staff) if (s.AdminRole == role) admin = s;
                GUILayout.BeginHorizontal();
                GUILayout.Label(Names.AdminRoleTitle(role), _bold, GUILayout.Width(200));
                if (admin != null) GUILayout.Label($"{admin.Name} · skill {admin.Skill:0}{(admin.IsAway(S.Day) ? " · away" : "")}");
                else Colored("Vacant: hire from Staff → Hire", Dim, _small);
                GUILayout.EndHorizontal();
            }

            var p = S.AdminPolicies;
            Section("Policies");
            PolicySlider("Auto-approve tuition discounts up to", p.AutoApproveDiscountBelow, 0f, 0.5f, v => p.AutoApproveDiscountBelow = v, "P0", 0.05f);
            PolicyToggle("Reject larger discount requests automatically", p.RejectLargerDiscounts, v => p.RejectLargerDiscounts = v);
            PolicySlider("Auto-approve raises up to", p.AutoApproveRaiseBelow, 0f, 0.2f, v => p.AutoApproveRaiseBelow = v, "P0", 0.01f);
            PolicyToggle("Counter larger raise requests with half", p.CounterLargerRaises, v => p.CounterLargerRaises = v);
            PolicySlider("Auto-compensate complaints up to $", p.AutoCompensateComplaintsBelow, 0f, 2000f, v => p.AutoCompensateComplaintsBelow = v, "0", 50f);
            PolicySlider("Auto-admit applicants with academic ≥", p.AutoAdmitAcademicAbove, 50f, 100f, v => p.AutoAdmitAcademicAbove = v, "0", 1f);
            PolicyToggle("Fill remaining beds before the season ends", p.FillRemainingBedsAtDeadline, v => p.FillRemainingBedsAtDeadline = v);
            PolicySlider("Match rival offers for staff with skill ≥", p.AutoMatchOffersSkillAbove, 40f, 100f, v => p.AutoMatchOffersSkillAbove = v, "0", 1f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Default discipline response", GUILayout.Width(320));
            string[] responses = { "warning", "detention", "parents", "suspend" };
            int current = Array.IndexOf(responses, p.DefaultDisciplineResponse);
            int chosen = GUILayout.Toolbar(Mathf.Max(0, current), responses);
            if (chosen != current) Defer(() => p.DefaultDisciplineResponse = responses[chosen]);
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        void PolicySlider(string label, float value, float min, float max, Action<float> set, string format, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(320));
            float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(220));
            v = Mathf.Round(v / step) * step;
            GUILayout.Label(format == "P0" ? v.ToString("P0") : v.ToString(format), GUILayout.Width(60));
            if (Math.Abs(v - value) > step * 0.1f) Defer(() => set(v));
            GUILayout.EndHorizontal();
        }

        void PolicyToggle(string label, bool value, Action<bool> set)
        {
            bool v = GUILayout.Toggle(value, label);
            if (v != value) Defer(() => set(v));
        }
    }
}
