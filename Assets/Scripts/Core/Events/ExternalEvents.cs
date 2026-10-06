using System;
using System.Text;

namespace SchoolSim
{
    sealed class InspectionNoticeEvent : EventTemplate
    {
        public override string Id => "inspection_notice";
        public override bool Random => false;
        public override int CooldownDays => 0;
        public override int ResponseDays => InspectionSystem.NoticeDays;

        static float DeepCleanCost(Simulation sim) => Ev.Round100(1500f + sim.State.Rooms.Count * 120f);

        static float RepairsCost(Simulation sim)
        {
            float cost = 0f;
            foreach (var r in sim.State.Rooms)
                if (r.IsBuilt(sim.Day) && r.Condition < 60f) cost += sim.Facilities.RepairCost(r) * 0.8f;
            return Ev.Round100(cost);
        }

        public override bool Build(Simulation sim, InboxMessage m)
        {
            Ev.From(m, SenderType.Inspector, "Education Inspectorate", "Inspection announced", MessagePriority.Urgent);
            var sb = new StringBuilder();
            sb.AppendLine($"Government inspectors will visit on {Calendar.Format((int)m.Amount)}. Your staff's estimate of how you'd score today:");
            sb.AppendLine();
            foreach (var (name, score) in sim.Inspections.Evaluate())
                sb.Append(score < InspectionSystem.CriticalScore ? "[CRITICAL] " : score < InspectionSystem.PassScore ? "[WEAK] " : "[OK] ")
                  .Append(name).Append(": ").AppendLine(score.ToString("0"));
            sb.AppendLine().Append($"Passing needs an average of {InspectionSystem.PassScore:0} and no category below {InspectionSystem.CriticalScore:0}. " +
                                   "You can also hire staff or fix rooms directly before they arrive.");
            m.Body = sb.ToString();

            float clean = DeepCleanCost(sim);
            m.AddOption("deepclean", $"Order a professional deep clean ({Ev.Money(clean)})", "Every room becomes spotless.", cost: clean);
            float repairs = RepairsCost(sim);
            var rep = m.AddOption("repairs", $"Rush repairs on worn-out rooms ({Ev.Money(repairs)})", "Rooms below 60 condition restored to 80.", cost: repairs);
            if (repairs <= 0f) rep.DisabledReason = "No rooms need urgent repairs";
            m.AddOption("prepare", "Brief the staff and prepare the paperwork", "Staff lose some energy; incidents calm down.");
            m.AddOption("nothing", "Carry on as usual", "No cost.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "deepclean":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Upkeep, -DeepCleanCost(sim), "Deep clean before inspection");
                    foreach (var r in sim.State.Rooms) r.Cleanliness = Math.Max(r.Cleanliness, 97f);
                    return "The campus is spotless.";
                case "repairs":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Upkeep, -RepairsCost(sim), "Rush repairs before inspection");
                    foreach (var r in sim.State.Rooms) if (r.Condition < 60f) r.Condition = 80f;
                    return "Worn rooms patched up.";
                case "prepare":
                    foreach (var s in sim.State.Staff) s.Energy = MathUtil.Clamp100(s.Energy - 5f);
                    sim.State.IncidentPressure = Math.Max(0f, sim.State.IncidentPressure - 4f);
                    return "Staff are on their best behavior, and so are the students.";
                default:
                    return "Business as usual.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => "";
    }

    sealed class GrantOpportunityEvent : EventTemplate
    {
        public override string Id => "grant_opportunity";
        public override AdminRole Handler => AdminRole.FinancialDirector;
        public override int CooldownDays => 100;
        public override bool InSessionOnly => false;
        public override float Weight(Simulation sim) => 0.35f;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            m.Amount = Ev.Round100(5000f + sim.State.Rep(ReputationId.Academic) * 150f + sim.Rng.Range(0f, 5000f));
            m.Amount2 = SuccessChance(sim);
            string program = sim.Rng.Pick(new[] { "STEM Education", "Rural Schools", "Student Wellbeing", "Library Modernization", "Teacher Development" });
            m.Tag = program;
            Ev.From(m, SenderType.Government, "Ministry of Education", $"Grant program: {program}");
            m.Body = $"The {program} fund is accepting applications for grants of up to {Ev.Money(m.Amount)}. Applying takes staff time. " +
                     $"Your estimated chance of success is {Ev.Pct(m.Amount2)}, based on academic reputation and trust.";
            m.AddOption("apply", "Apply", "A decision arrives in about 30 days. Teachers lose some energy writing it.");
            m.AddOption("skip", "Don't apply", "No effect.");
            return true;
        }

        static float SuccessChance(Simulation sim) =>
            MathUtil.Clamp(0.3f + sim.State.Rep(ReputationId.Academic) / 200f + sim.State.Stat(StatId.Trust) / 400f, 0.1f, 0.9f);

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            if (option != "apply") return "Skipped.";
            foreach (var s in sim.State.Staff) if (s.Role == StaffRole.Teacher) s.Energy = MathUtil.Clamp100(s.Energy - 4f);
            sim.Events.Schedule(new ScheduledEvent { Day = sim.Day + 30, TemplateId = Id, Amount = m.Amount, Tag = m.Tag, OtherId = (int)(m.Amount2 * 1000) });
            return "Application submitted.";
        }

        public override string Expire(Simulation sim, InboxMessage m) => "The deadline passed.";

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) => "apply";

        public override void OnScheduled(Simulation sim, ScheduledEvent e)
        {
            if (sim.Rng.Chance(e.OtherId / 1000f))
            {
                sim.State.Finance.Post(sim.Day, LedgerCategory.Grants, e.Amount, $"{e.Tag} grant");
                sim.State.Year.Highlight($"Won a {Ev.Money(e.Amount)} {e.Tag} grant");
                sim.Inbox.Notify(SenderType.Government, "Ministry of Education", "Grant approved",
                    $"Congratulations: your {e.Tag} application was approved. {Ev.Money(e.Amount)} has been transferred.", MessagePriority.Normal);
            }
            else
            {
                sim.Inbox.Notify(SenderType.Government, "Ministry of Education", "Grant application unsuccessful",
                    $"We regret that your {e.Tag} application was not selected this round.");
            }
        }
    }

    sealed class SponsorshipOfferEvent : EventTemplate
    {
        public override string Id => "sponsorship_offer";
        public override AdminRole Handler => AdminRole.AthleticDirector;
        public override int CooldownDays => 60;
        public override bool InSessionOnly => false;

        public override float Weight(Simulation sim)
        {
            float sports = sim.State.Rep(ReputationId.Sports);
            if (sim.State.Finance.Sponsorships.Count >= sim.State.Teams.Count + 1) return 0f;
            return sim.State.Teams.Count > 0 && sports >= 25f ? 0.5f + sports / 100f : 0f;
        }

        public override bool Build(Simulation sim, InboxMessage m)
        {
            float sports = sim.State.Rep(ReputationId.Sports);
            bool major = sim.IsUnlocked(Feature.MajorSponsorships);
            float monthly = major ? 800f + sports * 30f : 200f + sports * 8f;
            bool controversial = sim.Rng.Chance(0.3f);
            if (controversial) monthly *= 1.4f;
            m.Amount = Ev.Round100(monthly);
            m.Tag = controversial ? (sim.Rng.Chance(0.5f) ? "Kinetic Drinks" : "Burger Barn") : Names.Company(sim.Rng);
            m.OtherId = controversial ? 1 : 0;

            Ev.From(m, SenderType.Sponsor, m.Tag, $"Sponsorship offer from {m.Tag}");
            m.Body = $"{m.Tag} would like to sponsor your athletics program for {Ev.Money(m.Amount)} per month for 12 months, with our logo on team jerseys and at games." +
                     (controversial ? " (Some parents may not love a junk-food brand on school uniforms.)" : "");
            m.AddOption("accept", "Accept the deal", $"+{Ev.Money(m.Amount)}/mo for 12 months." + (controversial ? " Some families object." : ""));
            m.AddOption("negotiate", "Negotiate for more", $"Chance of +25% ({Ev.Pct(NegotiateChance(sim))}); otherwise they walk away.");
            m.AddOption("decline", "Decline", "No change.");
            return true;
        }

        static float NegotiateChance(Simulation sim) => MathUtil.Clamp(0.45f + sim.State.Rep(ReputationId.Sports) / 400f + sim.Progression.NegotiationBonus, 0.1f, 0.9f);

        void Sign(Simulation sim, InboxMessage m, float monthly)
        {
            sim.State.Finance.Sponsorships.Add(new Sponsorship
            {
                Company = m.Tag, Monthly = monthly, EndDay = sim.Day + Calendar.DaysPerYear, Terms = "Jersey and venue branding",
            });
            if (m.OtherId == 1)
            {
                sim.ChangeTrust(-3f);
                sim.AllParentsMood("Junk-food sponsor", -3f, 60);
            }
            sim.State.Year.Highlight($"Signed a sponsorship with {m.Tag}");
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "accept":
                    Sign(sim, m, m.Amount);
                    return "Deal signed.";
                case "negotiate":
                    if (sim.Rng.Chance(NegotiateChance(sim)))
                    {
                        Sign(sim, m, Ev.Round100(m.Amount * 1.25f));
                        return "They agreed to pay more.";
                    }
                    return "They walked away from the table.";
                default:
                    return "Declined.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => "The offer lapsed.";
        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) => m.OtherId == 0 ? "accept" : null;
    }

    sealed class ViralMomentEvent : EventTemplate
    {
        public override string Id => "viral_moment";
        public override int CooldownDays => 60;
        public override float Weight(Simulation sim) => 0.3f;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            bool positive = sim.State.Stat(StatId.StudentLife) >= 55f && sim.State.IncidentPressure < 5f
                ? sim.Rng.Chance(0.75f)
                : sim.Rng.Chance(0.3f);
            m.Tag = positive ? "good" : "bad";

            if (positive)
            {
                Ev.From(m, SenderType.StudentAffairs, "Communications", "A student video went viral!");
                m.Body = "A student's video about daily life at the school has a million views. Comments are overwhelmingly positive: people want to know how to apply.";
                m.AddOption("campaign", "Ride the wave with a marketing push ($2,000)", "+15% applicants next season; student life reputation up.", cost: 2000f);
                m.AddOption("enjoy", "Just enjoy the moment", "Small, temporary reputation boost.");
            }
            else
            {
                string what = sim.State.Stat(StatId.FoodQuality) < 45f ? "the cafeteria food" : sim.State.IncidentPressure > 5f ? "a dorm party getting out of hand" : "a leaking dorm ceiling";
                Ev.From(m, SenderType.StudentAffairs, "Communications", "Embarrassing video going viral", MessagePriority.Important);
                m.Body = $"A video of {what} is spreading on social media, tagged with our school's name. Parents are sharing it.";
                m.AddOption("statement", "Issue a public statement", "Limits the damage; small trust gain.");
                m.AddOption("action", "Announce concrete changes", "Parents reassured. Promise: raise safety & discipline to 60 within 45 days.");
                m.AddOption("ignore", "Ignore it; it'll blow over", "Reputation hit.");
            }
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "campaign":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.Other, -2000f, "Marketing campaign");
                    sim.State.ApplicantBonus = Math.Min(0.5f, sim.State.ApplicantBonus + 0.15f);
                    sim.AddReputationModifier(ReputationId.StudentLife, "Viral video", 4f, 90);
                    sim.AddReputationModifier(ReputationId.Overall, "Viral video", 2f, 90);
                    return "Applications are already picking up.";
                case "enjoy":
                    sim.AddReputationModifier(ReputationId.StudentLife, "Viral video", 3f, 60);
                    return "Everyone enjoyed the attention.";
                case "statement":
                    sim.ShockReputation(ReputationId.Overall, -1f);
                    sim.ChangeTrust(1f);
                    return "The statement calmed things down.";
                case "action":
                    sim.AllParentsMood("School promised changes", 3f, 45);
                    sim.Promises.Make(PromiseKind.SafetyAtLeast, "Raise safety & discipline to 60", 45, 60f, to: SenderType.Parent);
                    return "Parents are watching to see if you follow through.";
                default:
                    sim.AddReputationModifier(ReputationId.Overall, "Viral embarrassment", -4f, 60);
                    sim.ShockReputation(ReputationId.StudentLife, -3f);
                    return "It did not blow over quickly.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, m.Tag == "good" ? "enjoy" : "ignore");
    }

    sealed class AlumniDonationEvent : EventTemplate
    {
        public override string Id => "alumni_donation";
        public override int CooldownDays => 90;
        public override bool InSessionOnly => false;

        public override float Weight(Simulation sim) =>
            sim.State.Alumni.Exists(a => a.Tier >= UniversityTier.TopUniversity && !a.Donated) ? 0.4f : 0f;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var list = sim.State.Alumni.FindAll(a => a.Tier >= UniversityTier.TopUniversity && !a.Donated);
            if (list.Count == 0) return false;
            var alum = sim.Rng.Pick(list);
            alum.Donated = true;
            m.Tag = alum.Name;
            m.Amount = Ev.Round100(3000f + alum.Wealth * 150f);
            Ev.From(m, SenderType.Alumni, alum.Name, "A gift from an old student");
            m.Body = $"I graduated in year {alum.GraduationYear} ({alum.Achievement.ToLower()}). The school changed my life and I'd like to give back {Ev.Money(m.Amount)}.";
            m.AddOption("accept", "Accept with thanks", $"+{Ev.Money(m.Amount)}.");
            m.AddOption("scholarship", "Ask them to fund a named scholarship", "Less cash, but trust and academic reputation rise.");
            m.AddOption("speaker", "Also invite them to speak to students", "Half the money; students inspired.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var fin = sim.State.Finance;
            switch (option)
            {
                case "scholarship":
                    fin.Post(sim.Day, LedgerCategory.Donations, Ev.Round100(m.Amount * 0.6f), $"{m.Tag} scholarship fund");
                    sim.ChangeTrust(3f);
                    sim.AddReputationModifier(ReputationId.Academic, $"{m.Tag} Scholarship", 2f, 360);
                    return "The scholarship fund is established.";
                case "speaker":
                    fin.Post(sim.Day, LedgerCategory.Donations, Ev.Round100(m.Amount * 0.5f), "Alumni gift");
                    sim.AllStudentsMood("Inspired by an alumnus", 4f, 20);
                    return "The talk was a huge hit.";
                default:
                    fin.Post(sim.Day, LedgerCategory.Donations, m.Amount, "Alumni gift from " + m.Tag);
                    return "Gift received.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "accept");
    }

    sealed class BankLoanEvent : EventTemplate
    {
        public override string Id => "bank_loan";
        public override bool Random => false;
        public override int CooldownDays => 0;
        public override int ResponseDays => 20;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            Ev.From(m, SenderType.Finance, "Harbor Community Bank", "Your account is overdrawn", MessagePriority.Urgent);
            m.Body = $"The school's account is at {Ev.Money(sim.State.Finance.Cash)}. If the school stays insolvent for too long, the board will close it. " +
                     "We can offer an emergency loan.";
            m.AddOption("loan40", "Borrow $40,000 ($1,900/mo for 24 months)", "Immediate cash; ongoing repayments.");
            m.AddOption("loan80", "Borrow $80,000 ($3,700/mo for 26 months)", "More breathing room; heavier repayments.");
            m.AddOption("decline", "Decline", "You must fix cash flow yourself.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            switch (option)
            {
                case "loan40":
                    sim.Economy.TakeLoan("Harbor Community Bank", 40000f, 1900f, 24);
                    return "Funds transferred.";
                case "loan80":
                    sim.Economy.TakeLoan("Harbor Community Bank", 80000f, 3700f, 26);
                    return "Funds transferred.";
                default:
                    return "No loan taken.";
            }
        }
    }

    sealed class SpecializationChoiceEvent : EventTemplate
    {
        public override string Id => "specialization_choice";
        public override bool Random => false;
        public override bool InSessionOnly => false;
        public override int CooldownDays => 0;
        public override int ResponseDays => 30;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            if (sim.State.Specialization != Specialization.None) return false;
            Ev.From(m, SenderType.Government, "Board of Trustees", "Choose the school's identity", MessagePriority.Important);
            m.Body = "The school has grown enough to define what it stands for. The board asks you to choose a specialization. " +
                     "It is permanent, so consider what your school is already good at.";
            foreach (Specialization s in Enum.GetValues(typeof(Specialization)))
            {
                if (s == Specialization.None) continue;
                m.AddOption(s.ToString(), Names.Specialization(s), SpecializationRules.Describe(s));
            }
            m.AddOption("later", "Decide later", "The board will ask again in 60 days.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            if (option == "later")
            {
                sim.Events.Schedule(new ScheduledEvent { Day = sim.Day + 60, TemplateId = Id });
                return "The board will ask again.";
            }
            var spec = (Specialization)Enum.Parse(typeof(Specialization), option);
            sim.State.Specialization = spec;
            sim.State.Year.Highlight($"Became a {Names.Specialization(spec)}");
            sim.Log($"{sim.State.SchoolName} is now a {Names.Specialization(spec)}.", important: true);
            sim.News.Publish(NewsTone.Good, NewsTopic.School, $"{sim.State.SchoolName} becomes a {Names.Specialization(spec)}",
                $"The board of {sim.State.SchoolName} has chosen a new identity for the school: {Names.Specialization(spec).ToLowerInvariant()}.");
            var signature = SpecializationRules.Signature(spec);
            return $"{sim.State.SchoolName} is now a {Names.Specialization(spec)}." + (signature != null ? $" Its own building, the {signature.Name}, can now be built." : "");
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "later");

        public override void OnScheduled(Simulation sim, ScheduledEvent e)
        {
            sim.Events.TriggerDirected(Id, null, ignoreCooldown: true);
        }
    }

    sealed class ChampionshipGameEvent : EventTemplate
    {
        public override string Id => "championship_game";
        public override bool Random => false;
        public override int CooldownDays => 0;
        public override int ResponseDays => 4;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var team = sim.State.FindTeam(m.OtherId);
            if (team == null) return false;
            Ev.From(m, SenderType.Sports, "Athletics Department", $"{team.Name} reached the final!", MessagePriority.Important);
            m.Body = $"After a {team.Wins}-{team.Losses} season the {team.Name} are in the championship final, five days from now. " +
                     $"Team rating: {team.Rating:0}. How should we prepare?";
            m.AddOption("fans", "Charter buses so students can cheer ($1,500)", "Home-crowd boost; students love it.", cost: 1500f);
            m.AddOption("practice", "Schedule extra practice sessions", "Better odds; players more stressed.");
            m.AddOption("normal", "Stick to the routine", "No change.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var team = sim.State.FindTeam(m.OtherId);
            if (team == null) return "";
            float bonus = 0f;
            switch (option)
            {
                case "fans":
                    sim.State.Finance.Post(sim.Day, LedgerCategory.SportsOps, -1500f, "Fan buses for the final");
                    sim.AllStudentsMood("Going to the final", 3f, 10);
                    bonus = 4f;
                    break;
                case "practice":
                    foreach (int id in team.Roster)
                    {
                        var s = sim.State.FindStudent(id);
                        if (s != null) s.Stress = MathUtil.Clamp100(s.Stress + 8f);
                    }
                    bonus = 3f;
                    break;
            }
            sim.Events.Schedule(new ScheduledEvent { Day = sim.Day + 5, TemplateId = Id, OtherId = team.Id, Amount = bonus });
            return "The final is set.";
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "normal");

        public override void OnScheduled(Simulation sim, ScheduledEvent e)
        {
            var team = sim.State.FindTeam(e.OtherId);
            if (team != null) sim.Sports.PlayChampionship(team, e.Amount);
        }
    }

    /// <summary>At school level 25 the trustees of the old estate across the road offer it to the school.</summary>
    sealed class EstateOfferEvent : EventTemplate
    {
        public override string Id => "estate_offer";
        public override bool Random => false;
        public override bool InSessionOnly => false;
        public override int CooldownDays => 0;
        public override int ResponseDays => 30;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var map = sim.State.Campus;
            if (map.EstateOpen) return false;
            int parcels = (map.ParcelsX - map.EstateParcelX) * map.ParcelsY;
            Ev.From(m, SenderType.Finance, "The estate's trustees", "The old estate across the road", MessagePriority.Important);
            m.Body = $"The trustees of the old estate across the road would like {sim.State.SchoolName} to have it: the manor, its oaks and {parcels} parcels of " +
                     $"parkland. For {Ev.Money(Balance.EstateOpenCost)} the manor and the drive up to it are yours at once; the rest of the estate is then " +
                     $"for sale parcel by parcel, from {Ev.Money(Balance.EstateParcelCost)}.\n\nThe offer stands: you can also buy it later from the land map.";
            m.AddOption("buy", $"Buy the old estate ({Ev.Money(Balance.EstateOpenCost)})", "A second campus: the manor today, room to grow tomorrow.",
                cost: Balance.EstateOpenCost);
            m.AddOption("later", "Not yet", "Buy it from the land map when you're ready.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option) =>
            option == "buy" ? sim.OpenEstate().Message : "The trustees will wait. The estate is on the land map whenever you're ready.";

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "later");
    }

    /// <summary>An elite institution's Founders' Gala, every spring: wealthy families and old students give generously.</summary>
    sealed class FoundersGalaEvent : EventTemplate
    {
        public override string Id => "founders_gala";
        public override bool Random => false;
        public override int CooldownDays => 0;
        public override int ResponseDays => 20;

        /// <summary>What a grand gala raises: wealthy families, successful old students, the endowment's friends, a restored manor.</summary>
        static float Proceeds(Simulation sim)
        {
            var s = sim.State;
            int wealthy = 0, notable = 0;
            foreach (var st in s.Students) if (st.FamilyWealth >= 70f) wealthy++;
            foreach (var a in s.Alumni) if (a.Tier >= UniversityTier.TopUniversity) notable++;
            float raised = 40000f + wealthy * 600f + notable * 1000f + s.Endowment * 0.005f;
            return Ev.Round100(raised * (1f + 0.3f * sim.Indices.Manor));
        }

        public override bool Build(Simulation sim, InboxMessage m)
        {
            m.Amount = Proceeds(sim);
            Ev.From(m, SenderType.Alumni, "Founders' Gala committee", "Plans for the Founders' Gala", MessagePriority.Important);
            m.Body = $"Every spring the nation's elite schools throw a Founders' Gala. Families and old students of {sim.State.SchoolName} are ready to give " +
                     $"about {Ev.Money(m.Amount)} at a grand one" + (sim.Indices.Manor > 0f ? ", the old manor helping" : "") + ". How shall we celebrate?";
            m.AddOption("grand", $"A grand gala ({Ev.Money(Balance.GalaCost)})", $"Raises about {Ev.Money(m.Amount)}. Prestige up for a while.",
                cost: Balance.GalaCost);
            m.AddOption("endow", $"A grand gala for the endowment ({Ev.Money(Balance.GalaCost)})", "Everything raised goes into the endowment for good.",
                cost: Balance.GalaCost);
            m.AddOption("dinner", $"A donors' dinner ({Ev.Money(Balance.DinnerCost)})", $"Raises about {Ev.Money(Ev.Round100(m.Amount * 0.35f))}.",
                cost: Balance.DinnerCost);
            m.AddOption("skip", "Not this year", "Some families will be disappointed.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var fin = sim.State.Finance;
            if (option == "skip")
            {
                sim.AllParentsMood("No gala this year", -2f, 45);
                return "No gala this year.";
            }
            bool grand = option != "dinner";
            fin.Post(sim.Day, LedgerCategory.Other, -(grand ? Balance.GalaCost : Balance.DinnerCost), grand ? "Founders' Gala" : "Donors' dinner");
            float raised = Ev.Round100(m.Amount * (grand ? 1f : 0.35f) * sim.Rng.Range(0.85f, 1.15f));
            fin.Post(sim.Day, LedgerCategory.Donations, raised, grand ? "Founders' Gala gifts" : "Donors' dinner gifts");
            if (option == "endow") sim.Economy.Endow(raised);
            if (!grand) return $"The dinner raised {Ev.Money(raised)}.";

            sim.AddReputationModifier(ReputationId.Overall, "Founders' Gala", 2f, 120);
            sim.AllParentsMood("A glittering gala", 3f, 60);
            sim.State.Year.Highlight($"The Founders' Gala raised {Ev.Money(raised)}");
            sim.News.Publish(NewsTone.Good, NewsTopic.School, $"{sim.State.SchoolName}'s Founders' Gala raises {Ev.Money(raised)}",
                $"Black ties, a string quartet and a long list of donors: the Founders' Gala at {sim.State.SchoolName} was the event of the season.",
                1f, ReputationId.Overall, 90, NewsSystem.Courier);
            return option == "endow" ? $"The gala raised {Ev.Money(raised)} for the endowment." : $"The gala raised {Ev.Money(raised)}.";
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "skip");

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) => "grand";
    }

    public static class EventCatalog
    {
        public static EventTemplate[] Create() => new EventTemplate[]
        {
            // Parents
            new FoodComplaintEvent(),
            new DormComplaintEvent(),
            new AcademicConcernEvent(),
            new SafetyConcernEvent(),
            new DiscountRequestEvent(),
            new DonationOfferEvent(),
            new BullyingReportEvent(),
            new AthleteInjuryEvent(),
            new ParentPraiseEvent(),
            // Students
            new StudentRequestEvent(),
            new RoommateConflictEvent(),
            new StudentFightEvent(),
            new LateNightPartyEvent(),
            new CheatingScandalEvent(),
            new TransferApplicantEvent(),
            new AthleteRecruitEvent(),
            new StudentAchievementEvent(),
            // Staff
            new RaiseRequestEvent(),
            new PoachOfferEvent(),
            new ResignationNoticeEvent(),
            new ContractRenewalEvent(),
            new StaffBurnoutEvent(),
            // Facilities
            new PlumbingFailureEvent(),
            new WifiOutageEvent(),
            new BrokenEquipmentEvent(),
            new IllnessOutbreakEvent(),
            // External and system
            new InspectionNoticeEvent(),
            new GrantOpportunityEvent(),
            new SponsorshipOfferEvent(),
            new ViralMomentEvent(),
            new AlumniDonationEvent(),
            new BankLoanEvent(),
            new SpecializationChoiceEvent(),
            new EstateOfferEvent(),
            new FoundersGalaEvent(),
            new ChampionshipGameEvent(),
            new PressInquiryEvent(),
            // The assistant
            new CashProblemEvent(),
            new BedsProblemEvent(),
            new BathroomsProblemEvent(),
            new SeatsProblemEvent(),
            new TeachersProblemEvent(),
            new SafetyProblemEvent(),
            new FoodProblemEvent(),
            new DirtyProblemEvent(),
            new RepairsProblemEvent(),
            new UnhappyProblemEvent(),
            new StaffMoraleProblemEvent(),
            new UpgradeIdeaEvent(),
            new TutorialChoiceEvent(),
        };
    }
}
