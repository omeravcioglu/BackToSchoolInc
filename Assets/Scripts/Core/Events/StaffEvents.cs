using System;

namespace SchoolSim
{
    abstract class StaffEventBase : EventTemplate
    {
        public override bool Random => false;
        public override AdminRole Handler => AdminRole.HRManager;
        public override int CooldownDays => 0;

        protected static SenderType SenderFor(StaffMember s) => s.Role == StaffRole.Teacher ? SenderType.Teacher : SenderType.Staff;
    }

    sealed class RaiseRequestEvent : StaffEventBase
    {
        public override string Id => "raise_request";

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null || s.LeavingDay >= 0) return false;
            float market = sim.Staff.MarketValue(s);
            float request = Ev.Round10(Math.Max(s.Salary * 1.04f, market * sim.Rng.Range(1.0f, 1.08f)));
            request = Math.Min(request, Ev.Round10(s.Salary * 1.3f));
            float pct = request / s.Salary - 1f;
            m.Amount = request;
            m.Amount2 = Ev.Round10(s.Salary + (request - s.Salary) / 2f);

            string reason;
            if (sim.Staff.Workload(s) > 1.3f) reason = "My workload has grown a lot as the school has grown.";
            else if (s.Has(StaffTrait.Ambitious)) reason = "Frankly, other schools have approached me.";
            else if (s.Salary < market * 0.92f) reason = $"Similar positions pay around {Ev.Money(market)} now.";
            else reason = "I believe my results speak for themselves, and living costs keep rising.";

            float years = (sim.Day - Math.Max(0, s.HiredDay)) / (float)Calendar.DaysPerYear;
            Ev.From(m, SenderFor(s), s.Name, "Salary request", MessagePriority.Important);
            m.Body = $"I've been at {sim.State.SchoolName} for {years:0.#} year{(years > 0.95f && years < 1.05f ? "" : "s")}. {reason} I'd like my salary raised from {Ev.Money(s.Salary)} " +
                     $"to {Ev.Money(request)} per month (+{Ev.Pct(pct)}).\n\n{Ev.StaffProfile(sim, s)}";

            m.AddOption("approve", $"Approve {Ev.Money(request)} (+{Ev.Pct(pct)})", $"Payroll +{Ev.Money(request - s.Salary)}/mo. Morale and loyalty up.");
            m.AddOption("partial", $"Offer {Ev.Money(m.Amount2)} (+{Ev.Pct(pct / 2f)})", $"Payroll +{Ev.Money(m.Amount2 - s.Salary)}/mo. Partly satisfied.",
                "Ambitious staff resent half measures.");
            m.AddOption("review", "Promise to review it within 60 days", "No cost now. Promise: give them a raise (any size) within 60 days.",
                "Forgetting this promise hurts morale and trust badly.");
            m.AddOption("reject", "Reject the request", "Morale and loyalty fall.", "Rejected staff are far more likely to quit or accept rival offers.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null) return Ev.Gone(m.SenderName);
            int day = sim.Day;
            switch (option)
            {
                case "approve":
                    sim.Staff.SetSalary(s, m.Amount);
                    return $"{s.Name} is pleased.";
                case "partial":
                    sim.Staff.SetSalary(s, m.Amount2);
                    if (s.Has(StaffTrait.Ambitious)) s.Moodlets.Set("Only got half the raise", -6f, day + 60);
                    return $"{s.Name} accepted the smaller raise.";
                case "review":
                    s.Moodlets.Set("Waiting for a salary review", -2f, day + 60);
                    sim.Promises.Make(PromiseKind.SalaryReview, $"Review {s.Name}'s salary", 60, 0f, s.Id, SenderFor(s), reward: 2f, penalty: 5f);
                    return "Remember to adjust their salary in the Staff tab.";
                default:
                    s.Moodlets.Set("Raise rejected", -12f, day + 90);
                    s.Loyalty = MathUtil.Clamp100(s.Loyalty - 10f);
                    return $"{s.Name} is unhappy.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null) return "";
            s.Moodlets.Set("Raise request ignored", -8f, sim.Day + 90);
            s.Loyalty = MathUtil.Clamp100(s.Loyalty - 6f);
            return $"{s.Name} feels ignored.";
        }

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null) return "reject";
            float pct = m.Amount / s.Salary - 1f;
            if (pct <= p.AutoApproveRaiseBelow) return "approve";
            return p.CounterLargerRaises ? "partial" : null;
        }
    }

    sealed class PoachOfferEvent : StaffEventBase
    {
        public override string Id => "poach_offer";
        public override int ResponseDays => 7;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = sim.State.FindStaff(m.StaffId);
            var rival = sim.Rivals.Poacher();
            if (s == null || rival == null || s.LeavingDay >= 0) return false;
            m.OtherId = rival.Id;
            m.Tag = rival.Name;
            m.Amount = Ev.Round10(s.Salary * sim.Rng.Range(1.15f, 1.35f));
            float mid = Ev.Round10(s.Salary + (m.Amount - s.Salary) / 2f);
            float bonus = Ev.Round100((m.Amount - s.Salary) * 6f);
            m.Amount2 = bonus;

            Ev.From(m, SenderFor(s), s.Name, $"{rival.Name} made me an offer", MessagePriority.Urgent);
            m.Body = $"I want to be upfront: {rival.Name} (prestige {rival.Prestige:0}) has offered me {Ev.Money(m.Amount)}/mo, compared to my current {Ev.Money(s.Salary)}. " +
                     $"I'll decide within a week.\n\n{Ev.StaffProfile(sim, s)}";

            m.AddOption("match", $"Match the offer ({Ev.Money(m.Amount)})", $"Payroll +{Ev.Money(m.Amount - s.Salary)}/mo. They'll almost certainly stay.");
            m.AddOption("counter", $"Counteroffer ({Ev.Money(mid)})", $"Stay chance about {Ev.Pct(CounterChance(sim, s))}.");
            m.AddOption("bonus", $"One-time retention bonus ({Ev.Money(bonus)})", $"No payroll increase. Stay chance about {Ev.Pct(BonusChance(sim, s))}.", cost: bonus);
            m.AddOption("contract", "Offer a 3-year contract with a 10% raise", $"Stay chance about {Ev.Pct(ContractChance(sim, s))}. Longer commitment, higher firing cost.");
            m.AddOption("release", "Let them go", "They leave in two weeks.");
            return true;
        }

        static float CounterChance(Simulation sim, StaffMember s) =>
            MathUtil.Clamp(0.4f + s.Loyalty / 200f + sim.Progression.NegotiationBonus + (s.Has(StaffTrait.Loyal) ? 0.3f : 0f) - (s.Has(StaffTrait.Ambitious) ? 0.15f : 0f), 0.05f, 0.95f);

        static float BonusChance(Simulation sim, StaffMember s) =>
            MathUtil.Clamp(0.35f + s.Loyalty / 250f + sim.Progression.NegotiationBonus + (s.Has(StaffTrait.Loyal) ? 0.25f : 0f), 0.05f, 0.9f);

        static float ContractChance(Simulation sim, StaffMember s) =>
            MathUtil.Clamp(0.45f + s.Loyalty / 200f + sim.Progression.NegotiationBonus + (s.Has(StaffTrait.Loyal) ? 0.3f : 0f) - (s.Has(StaffTrait.Ambitious) ? 0.2f : 0f), 0.05f, 0.95f);

        string Leave(Simulation sim, StaffMember s, string rival)
        {
            s.LeavingDay = sim.Day + 14;
            sim.State.Year.Highlight($"Lost {s.Name} to {rival}");
            return $"{s.Name} accepted {rival}'s offer and leaves in two weeks.";
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null) return Ev.Gone(m.SenderName);
            int day = sim.Day;
            switch (option)
            {
                case "match":
                    if (!sim.Rng.Chance(0.95f)) return Leave(sim, s, m.Tag);
                    sim.Staff.SetSalary(s, m.Amount);
                    return $"{s.Name} is staying.";
                case "counter":
                    if (!sim.Rng.Chance(CounterChance(sim, s))) return Leave(sim, s, m.Tag);
                    sim.Staff.SetSalary(s, Ev.Round10(s.Salary + (m.Amount - s.Salary) / 2f));
                    return $"{s.Name} accepted the counteroffer.";
                case "bonus":
                    sim.State.Finance.Post(day, LedgerCategory.Bonuses, -m.Amount2, "Retention bonus for " + s.Name);
                    if (!sim.Rng.Chance(BonusChance(sim, s))) return Leave(sim, s, m.Tag) + " (The bonus was already paid.)";
                    s.Moodlets.Set("Retention bonus", 6f, day + 60);
                    return $"{s.Name} took the bonus and is staying.";
                case "contract":
                    if (!sim.Rng.Chance(ContractChance(sim, s))) return Leave(sim, s, m.Tag);
                    sim.Staff.SetSalary(s, s.Salary * 1.1f);
                    s.ContractYears = 3;
                    s.ContractEndDay = day + 3 * Calendar.DaysPerYear;
                    s.RenewalOffered = false;
                    s.Loyalty = MathUtil.Clamp100(s.Loyalty + 10f);
                    return $"{s.Name} signed a 3-year contract.";
                default:
                    return Leave(sim, s, m.Tag);
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "release");

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null) return "release";
            if (s.Skill >= p.AutoMatchOffersSkillAbove) return m.Amount / s.Salary - 1f <= 0.3f ? "match" : null;
            return "release";
        }
    }

    sealed class ResignationNoticeEvent : StaffEventBase
    {
        public override string Id => "resignation_notice";
        public override AdminRole Handler => AdminRole.None;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null || s.LeavingDay >= 0) return false;

            Factor worst = new Factor("general unhappiness", 0f);
            foreach (var f in s.MoraleFactors.Factors)
                if (f.Value < worst.Value) worst = f;

            Ev.From(m, SenderFor(s), s.Name, "My resignation", MessagePriority.Urgent);
            m.Body = $"I've decided to leave {sim.State.SchoolName}. Morale has been low for a while ({worst.Label.ToLower()} is a big part of it). " +
                     $"Unless something changes, this is my two weeks' notice.\n\n{Ev.StaffProfile(sim, s)}";
            m.AddOption("counter", $"Offer a 15% raise to stay ({Ev.Money(Ev.Round10(s.Salary * 1.15f))})", $"Stay chance about {Ev.Pct(RaiseChance(sim, s))}.");
            m.AddOption("leave", "Offer paid time off to recover", $"Stay chance about {Ev.Pct(LeaveChance(sim, s))}. Away for 10 days.");
            m.AddOption("accept", "Accept the resignation", "They leave in two weeks.");
            return true;
        }

        static float RaiseChance(Simulation sim, StaffMember s) => MathUtil.Clamp(0.55f + s.Loyalty / 300f + sim.Progression.NegotiationBonus, 0.1f, 0.95f);
        static float LeaveChance(Simulation sim, StaffMember s) => MathUtil.Clamp(0.4f + (s.Energy < 30f ? 0.25f : 0f) + sim.Progression.NegotiationBonus, 0.1f, 0.9f);

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null) return Ev.Gone(m.SenderName);
            int day = sim.Day;
            switch (option)
            {
                case "counter":
                    if (sim.Rng.Chance(RaiseChance(sim, s)))
                    {
                        sim.Staff.SetSalary(s, s.Salary * 1.15f);
                        s.Moodlets.Set("Convinced to stay", 10f, day + 60);
                        return $"{s.Name} withdrew the resignation.";
                    }
                    s.LeavingDay = day + 14;
                    return $"{s.Name} appreciated it but is leaving anyway.";
                case "leave":
                    if (sim.Rng.Chance(LeaveChance(sim, s)))
                    {
                        sim.Staff.GiveLeave(s, 10);
                        s.Moodlets.Set("Given time to recover", 12f, day + 60);
                        return $"{s.Name} will take 10 days off and come back.";
                    }
                    s.LeavingDay = day + 14;
                    return $"{s.Name} is leaving regardless.";
                default:
                    s.LeavingDay = day + 14;
                    return $"{s.Name} will leave in two weeks.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "accept");
    }

    sealed class ContractRenewalEvent : StaffEventBase
    {
        public override string Id => "contract_renewal";
        public override int ResponseDays => 20;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null || s.LeavingDay >= 0) return false;
            float ask = Ev.Round10(Math.Max(s.Salary, sim.Staff.MarketValue(s)));
            m.Amount = ask;

            Ev.From(m, SenderFor(s), s.Name, "Contract renewal");
            m.Body = $"My contract ends on {Calendar.Format(s.ContractEndDay)}. I'd like to discuss renewing it at {Ev.Money(ask)}/mo " +
                     $"(currently {Ev.Money(s.Salary)}).\n\n{Ev.StaffProfile(sim, s)}";

            m.AddOption("renew1", $"Renew 1 year at {Ev.Money(ask)}", "Flexible, but no loyalty bonus.");
            m.AddOption("renew3", $"Renew 3 years at {Ev.Money(Ev.Round10(ask * 0.97f))}", "Slightly cheaper; loyalty up. Firing later costs more.");
            var five = m.AddOption("renew5", $"Renew 5 years at {Ev.Money(Ev.Round10(ask * 0.94f))}", "Cheapest per month; loyalty up a lot. Expensive to fire.");
            if (s.Has(StaffTrait.Ambitious)) five.DisabledReason = $"{s.Name} won't commit to 5 years";
            m.AddOption("expire", "Let the contract run out", $"They leave on {Calendar.Format(s.ContractEndDay)} with no severance.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null) return Ev.Gone(m.SenderName);
            int years = option == "renew5" ? 5 : option == "renew3" ? 3 : option == "renew1" ? 1 : 0;
            if (years == 0)
            {
                s.LeavingDay = Math.Max(sim.Day + 1, s.ContractEndDay);
                return $"{s.Name} will leave when the contract ends.";
            }
            float salary = Ev.Round10(m.Amount * StaffSystem.ContractSalaryFactor(years));
            if (Math.Abs(salary - s.Salary) > 1f) sim.Staff.SetSalary(s, salary);
            s.ContractYears = years;
            s.ContractEndDay = Math.Max(s.ContractEndDay, sim.Day) + years * Calendar.DaysPerYear;
            s.RenewalOffered = false;
            s.Loyalty = MathUtil.Clamp100(s.Loyalty + (years >= 5 ? 15f : years >= 3 ? 8f : 0f));
            return $"{s.Name} renewed for {years} year{(years == 1 ? "" : "s")} at {Ev.Money(s.Salary)}/mo.";
        }

        public override string Expire(Simulation sim, InboxMessage m) => "They'll continue on a rolling one-year contract.";

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null) return "expire";
            return s.Skill >= 55f && !s.Has(StaffTrait.Ambitious) ? "renew3" : "renew1";
        }
    }

    sealed class StaffBurnoutEvent : StaffEventBase
    {
        public override string Id => "staff_burnout";

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null || s.IsAway(sim.Day)) return false;
            Ev.From(m, SenderType.Staff, "Staff room", $"{s.Name} is burning out", MessagePriority.Important);
            m.Body = $"{s.Name} ({s.Title}) is exhausted (energy {s.Energy:0}). Their work is suffering and colleagues are worried. " +
                     (sim.Staff.Workload(s) > 1.2f ? "The workload is simply too high; hiring help would fix the root cause." : "");
            m.AddOption("leave", "Give a week of paid leave", "Away 7 days, returns rested.");
            m.AddOption("bonus", "Pay a $500 bonus and ask them to push through", "Morale up a little; still exhausted.", cost: 500f);
            m.AddOption("ignore", "Do nothing", "Morale falls.", "Exhausted, ignored staff resign.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStaff(m.StaffId);
            if (s == null) return Ev.Gone(m.SenderName);
            int day = sim.Day;
            switch (option)
            {
                case "leave":
                    sim.Staff.GiveLeave(s, 7);
                    s.Moodlets.Set("Given rest when needed", 8f, day + 30);
                    return $"{s.Name} is taking a week off.";
                case "bonus":
                    sim.State.Finance.Post(day, LedgerCategory.Bonuses, -500f, "Bonus for " + s.Name);
                    s.Moodlets.Set("Bonus", 6f, day + 30);
                    s.Energy = MathUtil.Clamp100(s.Energy + 5f);
                    return $"{s.Name} will push on.";
                default:
                    s.Moodlets.Set("Ignored when exhausted", -10f, day + 60);
                    return $"{s.Name} feels unsupported.";
            }
        }

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) => "leave";
    }
}
