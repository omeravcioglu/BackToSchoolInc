using System;

namespace SchoolSim
{
    sealed class FoodComplaintEvent : EventTemplate
    {
        public override string Id => "food_complaint";
        public override AdminRole Handler => AdminRole.StudentAffairsDirector;
        public override int CooldownDays => 20;

        public override float Weight(Simulation sim)
        {
            float food = sim.State.Stat(StatId.FoodQuality);
            if (food < 50f) return 0.5f + (50f - food) / 10f;
            return food < 60f ? 0.3f : 0f;
        }

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = Ev.RandomStudent(sim, x => x.ParentMood < 65f && x.LastComplaintDay < sim.Day - 30);
            if (s == null) return false;
            s.LastComplaintDay = sim.Day;
            float food = sim.State.Stat(StatId.FoodQuality);
            m.StudentId = s.Id;
            m.Amount = 150f;
            m.Amount2 = Math.Min(90f, Math.Max(55f, food + 12f));

            Ev.From(m, SenderType.Parent, s.ParentName, "The cafeteria food");
            m.Body = food < 35f
                ? $"{s.FirstName} tells us the food is barely edible and the lines are endless. For what we pay, this is unacceptable. What are you going to do about it?"
                : $"{s.FirstName} says the cafeteria food has been getting worse. We expected better from a boarding school. Can you look into it?";

            m.AddOption("apologize", "Apologize and promise better food within 30 days",
                $"Family satisfaction up. Promise: food quality reaches {m.Amount2:0} within 30 days.",
                "A broken promise costs far more trust than an honest 'no'.");
            m.AddOption("budget", "Explain the budget limits honestly", "Family slightly disappointed. Small trust gain for honesty.");
            m.AddOption("compensate", $"Offer a meal credit ({Ev.Money(m.Amount)})", "Family satisfaction up.", "Paying off complaints can invite more of them.", m.Amount);
            m.AddOption("ignore", "Ignore the complaint", "Family satisfaction and trust fall.", "Ignored parents talk to other parents.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStudent(m.StudentId);
            if (s == null) return Ev.Gone("The family");
            int day = sim.Day;
            switch (option)
            {
                case "apologize":
                    s.ParentMoodlets.Set("Complaint heard", 8f * Ev.TrustFactor(sim), day + 30);
                    sim.Promises.Make(PromiseKind.FoodQualityAtLeast, $"Improve the food to quality {m.Amount2:0}", 30, m.Amount2, to: SenderType.Parent, studentId: s.Id);
                    return "The family appreciates the promise. Now you have to deliver.";
                case "budget":
                    s.ParentMoodlets.Set("Told about budget limits", -3f, day + 30);
                    sim.ChangeTrust(1f);
                    return "They were disappointed but appreciated the honesty.";
                case "compensate":
                    sim.State.Finance.Post(day, LedgerCategory.Compensation, -m.Amount, "Meal credit for the " + s.LastName + " family");
                    s.ParentMoodlets.Set("Compensated for food", 6f, day + 45);
                    return "They accepted the meal credit.";
                default:
                    s.ParentMoodlets.Set("Complaint ignored", -10f, day + 60);
                    sim.ChangeTrust(-2f);
                    if (sim.Rng.Chance(0.3f)) sim.AllParentsMood("Word of ignored complaints", -2f, 30);
                    return "The family feels ignored.";
            }
        }

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) =>
            m.Amount <= p.AutoCompensateComplaintsBelow ? "compensate" : "budget";
    }

    sealed class DormComplaintEvent : EventTemplate
    {
        public override string Id => "dorm_complaint";
        public override AdminRole Handler => AdminRole.DormDirector;
        public override int CooldownDays => 25;

        public override float Weight(Simulation sim)
        {
            var ix = sim.Indices;
            if (ix.BathroomShortage > 0.2f) return 1.2f;
            foreach (var kv in ix.DormRoomQuality)
                if (kv.Value < 35f && ix.ResidentsOf(kv.Key).Count > 0) return 0.8f;
            return 0f;
        }

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var ix = sim.Indices;
            var s = Ev.RandomStudent(sim, x =>
                x.LastComplaintDay < sim.Day - 30 && x.DormRoomId >= 0 &&
                ix.DormRoomQuality.TryGetValue(x.DormRoomId, out var q) && (q < 40f || ix.BathroomShortage > 0.2f));
            if (s == null) return false;
            s.LastComplaintDay = sim.Day;
            var room = sim.State.FindRoom(s.DormRoomId);
            m.StudentId = s.Id;
            m.RoomId = room.Id;
            m.Amount = sim.EffectiveDormFee(room);
            m.Amount2 = Math.Min(90f, sim.State.Stat(StatId.DormitoryQuality) + 10f);

            Ev.From(m, SenderType.Parent, s.ParentName, "Living conditions in the dorm");
            m.Body = ix.BathroomShortage > 0.2f
                ? $"{s.FirstName} says there are queues for the showers every morning and the bathrooms are overwhelmed. We pay dormitory fees for this?"
                : $"We visited {s.FirstName}'s room ({room.Name}: {room.LevelName}). Honestly, it is not fit for a boarding school.";

            var better = BetterRoom(sim, s);
            var move = m.AddOption("move", $"Move {s.FirstName} to a better room", "Family satisfaction up a lot.", "The student may miss their old roommates.");
            if (better == null) move.DisabledReason = "No better room has a free bed";
            m.AddOption("promise", "Promise dorm renovations within 45 days",
                $"Family satisfaction up. Promise: dormitory quality reaches {m.Amount2:0} within 45 days.");
            m.AddOption("refund", $"Refund one month of dorm fees ({Ev.Money(m.Amount)})", "Family satisfaction up.", cost: m.Amount);
            m.AddOption("ignore", "Ignore it", "Family satisfaction falls.");
            return true;
        }

        static Room BetterRoom(Simulation sim, Student s)
        {
            var current = sim.State.FindRoom(s.DormRoomId);
            float q = current?.Quality ?? 0f;
            Room best = null;
            foreach (var r in sim.Operational(RoomType.DormRoom))
            {
                if (r.Id == s.DormRoomId || sim.Indices.ResidentsOf(r.Id).Count >= r.Capacity) continue;
                if (r.Quality > q + 10f && (best == null || r.Quality > best.Quality)) best = r;
            }
            return best;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStudent(m.StudentId);
            if (s == null) return Ev.Gone("The family");
            int day = sim.Day;
            switch (option)
            {
                case "move":
                {
                    var room = BetterRoom(sim, s);
                    if (room == null) goto case "promise";
                    sim.Facilities.AssignBed(s, room);
                    s.Remember(day, $"Moved to {room.Name} after a family complaint.");
                    s.ParentMoodlets.Set("Moved to a better room", 12f, day + 60);
                    return $"{s.FirstName} moved into {room.Name}.";
                }
                case "promise":
                    s.ParentMoodlets.Set("Promised renovations", 6f * Ev.TrustFactor(sim), day + 45);
                    sim.Promises.Make(PromiseKind.DormQualityAtLeast, $"Renovate the dorms to quality {m.Amount2:0}", 45, m.Amount2, to: SenderType.Parent, studentId: s.Id);
                    return "The family will hold you to that.";
                case "refund":
                    sim.State.Finance.Post(day, LedgerCategory.Compensation, -m.Amount, "Dorm fee refund");
                    s.ParentMoodlets.Set("Dorm fee refunded", 7f, day + 45);
                    return "The refund softened things.";
                default:
                    s.ParentMoodlets.Set("Dorm complaint ignored", -10f, day + 60);
                    sim.ChangeTrust(-2f);
                    return "The family is unhappy.";
            }
        }

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p)
        {
            var s = sim.State.FindStudent(m.StudentId);
            if (s != null && BetterRoom(sim, s) != null) return "move";
            return m.Amount <= p.AutoCompensateComplaintsBelow ? "refund" : null;
        }
    }

    sealed class AcademicConcernEvent : EventTemplate
    {
        public override string Id => "academic_concern";
        public override AdminRole Handler => AdminRole.StudentAffairsDirector;

        public override float Weight(Simulation sim) =>
            sim.State.Students.Exists(s => s.Grades < 45f && s.LastComplaintDay < sim.Day - 30) ? 1f : 0f;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = Ev.RandomStudent(sim, x => x.Grades < 45f && x.LastComplaintDay < sim.Day - 30);
            if (s == null) return false;
            s.LastComplaintDay = sim.Day;
            m.StudentId = s.Id;
            Ev.From(m, SenderType.Parent, s.ParentName, $"Worried about {s.FirstName}'s grades");
            m.Body = $"{s.FirstName}'s grades have dropped to {s.Grades:0}. We sent our child here for a good education. " +
                     (s.Stress > 60f ? "They also seem very stressed. " : "") + "What is the school doing to help?";
            m.AddOption("tutoring", "Arrange private tutoring ($400)", "Grades up, a little more stress. Family satisfaction up.", cost: 400f);
            m.AddOption("meeting", "Set up a meeting with the teachers", "Family satisfaction up. Costs a teacher some energy.");
            m.AddOption("effort", "Tell them their child needs to work harder", "Family may be offended.", "Lazy students sometimes respond to a push.");
            m.AddOption("ignore", "Ignore it", "Family satisfaction falls.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStudent(m.StudentId);
            if (s == null) return Ev.Gone("The student");
            int day = sim.Day;
            switch (option)
            {
                case "tutoring":
                    sim.State.Finance.Post(day, LedgerCategory.Other, -400f, "Tutoring for " + s.FullName);
                    s.Grades = MathUtil.Clamp100(s.Grades + 6f);
                    s.Stress = MathUtil.Clamp100(s.Stress + 5f);
                    s.ParentMoodlets.Set("Arranged tutoring", 7f, day + 45);
                    return "Tutoring starts next week.";
                case "meeting":
                {
                    var t = Ev.RandomStaff(sim, x => x.Role == StaffRole.Teacher);
                    if (t != null) t.Energy = MathUtil.Clamp100(t.Energy - 10f);
                    s.ParentMoodlets.Set("Met the teachers", 5f * Ev.TrustFactor(sim), day + 45);
                    s.Discipline = MathUtil.Clamp100(s.Discipline + 2f);
                    return "The meeting went well.";
                }
                case "effort":
                    s.ParentMoodlets.Set("Blamed our child", s.Happiness < 45f ? -8f : -4f, day + 45);
                    if (s.Has(StudentTrait.Lazy) && sim.Rng.Chance(0.5f))
                    {
                        s.Discipline = MathUtil.Clamp100(s.Discipline + 5f);
                        return $"The talk seems to have woken {s.FirstName} up.";
                    }
                    s.Stress = MathUtil.Clamp100(s.Stress + 5f);
                    return "The family did not take it well.";
                default:
                    s.ParentMoodlets.Set("Academic concern ignored", -8f, day + 60);
                    return "The family is disappointed.";
            }
        }

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) => "meeting";
    }

    sealed class SafetyConcernEvent : EventTemplate
    {
        public override string Id => "safety_concern";
        public override AdminRole Handler => AdminRole.StudentAffairsDirector;
        public override int CooldownDays => 30;

        public override float Weight(Simulation sim) =>
            sim.State.IncidentPressure > 8f || sim.State.Stat(StatId.SafetyDiscipline) < 40f ? 1.2f : 0f;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = Ev.RandomStudent(sim, x => x.LastComplaintDay < sim.Day - 30);
            if (s == null) return false;
            s.LastComplaintDay = sim.Day;
            m.StudentId = s.Id;
            m.Amount = sim.CountStaff(StaffRole.Security, activeOnly: false) + 1;
            Ev.From(m, SenderType.Parent, s.ParentName, "Is my child safe at your school?", MessagePriority.Important);
            m.Body = $"We keep hearing about fights and trouble in the dorms. {s.FirstName} says nobody supervises the halls at night. " +
                     "We are seriously considering other schools.";
            m.AddOption("hire", "Promise to hire more security within 30 days",
                $"Family reassured. Promise: at least {m.Amount:0} security guard{(m.Amount == 1f ? "" : "s")} within 30 days.");
            m.AddOption("strict", "Switch to a strict discipline policy now", "Parents reassured; students less happy; fewer incidents.");
            m.AddOption("reassure", "Reassure them the school is safe", "Works if the school really is safe.", "If safety is poor, they won't believe you.");
            m.AddOption("ignore", "Ignore it", "Family satisfaction and trust fall.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStudent(m.StudentId);
            int day = sim.Day;
            switch (option)
            {
                case "hire":
                    s?.ParentMoodlets.Set("Promised more security", 6f * Ev.TrustFactor(sim), day + 30);
                    sim.Promises.Make(PromiseKind.StaffRoleCountAtLeast, $"Employ {m.Amount:0} security guard{(m.Amount == 1f ? "" : "s")}", 30, m.Amount, (int)StaffRole.Security, SenderType.Parent, s?.Id ?? -1);
                    return "Now hire someone from the Staff tab.";
                case "strict":
                    sim.State.Policies.Discipline = DisciplinePolicy.Strict;
                    sim.AllParentsMood("Stricter discipline", 3f, 45);
                    return "Discipline policy is now Strict.";
                case "reassure":
                    if (sim.State.Stat(StatId.SafetyDiscipline) >= 50f)
                    {
                        s?.ParentMoodlets.Set("Reassured about safety", 4f, day + 30);
                        return "They were reassured.";
                    }
                    s?.ParentMoodlets.Set("Not convinced about safety", -5f, day + 45);
                    sim.ChangeTrust(-2f);
                    return "They don't believe you.";
                default:
                    s?.ParentMoodlets.Set("Safety concern ignored", -10f, day + 60);
                    sim.ChangeTrust(-3f);
                    return "The family is alarmed.";
            }
        }

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) =>
            sim.State.Stat(StatId.SafetyDiscipline) >= 50f ? "reassure" : null;
    }

    sealed class DiscountRequestEvent : EventTemplate
    {
        public override string Id => "discount_request";
        public override AdminRole Handler => AdminRole.FinancialDirector;
        public override int CooldownDays => 18;

        public override float Weight(Simulation sim) =>
            sim.State.Students.Exists(s => s.FamilyWealth < 50f && s.Discount < 0.05f) ? 1f : 0f;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = Ev.RandomStudent(sim, x => x.FamilyWealth < 50f && x.Discount < 0.05f && x.LastComplaintDay < sim.Day - 60);
            if (s == null) return false;
            s.LastComplaintDay = sim.Day;
            float pct = MathUtil.Clamp((50f - s.FamilyWealth) / 100f + sim.Rng.Range(0.05f, 0.15f), 0.05f, 0.4f);
            pct = (float)Math.Round(pct * 20f) / 20f;
            m.StudentId = s.Id;
            m.Amount = pct;
            var type = SuggestedScholarship(s);
            m.Tag = type.ToString();

            float monthly = sim.State.Policies.AnnualTuition / Balance.TuitionInstallments * pct;
            Ev.From(m, SenderType.Parent, s.ParentName, "Request for tuition assistance");
            m.Body = $"Our family has hit a difficult financial period. We would like to keep {s.FirstName} at your school, but we need a {Ev.Pct(pct)} " +
                     $"tuition discount to manage it.\n\n{Ev.Profile(s)}";

            m.AddOption("approve", $"Approve {Ev.Pct(pct)} discount", $"-{Ev.Money(monthly)}/mo tuition. Family satisfaction up a lot; very likely to stay.");
            m.AddOption("partial", $"Offer {Ev.Pct(pct / 2f)} instead", $"-{Ev.Money(monthly / 2f)}/mo tuition. Family somewhat satisfied.");
            var sch = m.AddOption("scholarship", $"Convert it into a {type} scholarship ({Ev.Pct(pct)})",
                $"-{Ev.Money(monthly)}/mo tuition. Student and family are delighted; boosts {ScholarshipRep(type)} reputation.",
                "Scholarship students are more loyal and work harder.");
            sch.RequiresFeature = Feature.Scholarships;
            m.AddOption("reject", "Decline the request", "Family satisfaction falls sharply. They may leave the school.");
            return true;
        }

        static ScholarshipType SuggestedScholarship(Student s)
        {
            if (s.Academic >= 70f) return ScholarshipType.Academic;
            if (s.Athletic >= 75f) return ScholarshipType.Athletic;
            if (s.Artistic >= 75f) return ScholarshipType.Arts;
            return ScholarshipType.FinancialAid;
        }

        static string ScholarshipRep(ScholarshipType t) => t switch
        {
            ScholarshipType.Academic => "academic",
            ScholarshipType.Athletic => "sports",
            ScholarshipType.Arts => "student life",
            _ => "community (trust)",
        };

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStudent(m.StudentId);
            if (s == null) return Ev.Gone("The student");
            int day = sim.Day;
            float pct = m.Amount;
            switch (option)
            {
                case "approve":
                    s.Discount = pct;
                    s.ParentMoodlets.Set("Tuition aid approved", 12f, day + 90);
                    s.Remember(day, $"Family received a {Ev.Pct(pct)} tuition discount.");
                    return "The family is very grateful.";
                case "partial":
                    s.Discount = pct / 2f;
                    s.ParentMoodlets.Set("Partial tuition aid", 4f, day + 60);
                    return "The family accepted, reluctantly.";
                case "scholarship":
                {
                    var type = (ScholarshipType)Enum.Parse(typeof(ScholarshipType), m.Tag);
                    s.Discount = pct;
                    s.Scholarship = type;
                    s.ParentMoodlets.Set("Scholarship awarded", 15f, day + 120);
                    s.Moodlets.Set("Won a scholarship", 6f, day + 60);
                    s.Remember(day, $"Awarded a {type} scholarship.");
                    switch (type)
                    {
                        case ScholarshipType.Academic: sim.AddReputationModifier(ReputationId.Academic, "Academic scholarships", 1f, 180); break;
                        case ScholarshipType.Athletic: sim.AddReputationModifier(ReputationId.Sports, "Athletic scholarships", 1f, 180); break;
                        case ScholarshipType.Arts: sim.AddReputationModifier(ReputationId.StudentLife, "Arts scholarships", 1f, 180); break;
                        default: sim.ChangeTrust(1f); break;
                    }
                    return $"{s.FirstName} is now a {type} scholar.";
                }
                case "reject":
                    s.ParentMoodlets.Set("Tuition aid denied", -12f, day + 120);
                    s.ParentMood = MathUtil.Clamp100(s.ParentMood - 8f);
                    return "The family is upset and may not return next year.";
                default:
                    s.ParentMoodlets.Set("Aid request ignored", -8f, day + 90);
                    return "The family never heard back.";
            }
        }

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p)
        {
            if (m.Amount <= p.AutoApproveDiscountBelow) return "approve";
            return p.RejectLargerDiscounts ? "reject" : null;
        }
    }

    sealed class DonationOfferEvent : EventTemplate
    {
        public override string Id => "donation_offer";
        public override AdminRole Handler => AdminRole.FinancialDirector;
        public override int CooldownDays => 60;
        public override bool InSessionOnly => false;

        public override float Weight(Simulation sim) =>
            sim.State.Students.Exists(s => s.FamilyWealth >= 75f && s.ParentMood >= 62f) ? 0.8f : 0f;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = Ev.RandomStudent(sim, x => x.FamilyWealth >= 75f && x.ParentMood >= 62f);
            if (s == null) return false;
            m.StudentId = s.Id;
            m.Amount = Ev.Round100(2000f + s.FamilyWealth * 80f + sim.Rng.Range(0f, 4000f));

            string condition = "none";
            if (sim.Rng.Chance(0.5f))
            {
                if (s.TeamId >= 0) condition = "captain";
                else if (sim.HasOperational(RoomType.Library)) condition = "name";
                else condition = "room";
            }
            m.Tag = condition;

            Ev.From(m, SenderType.Parent, s.ParentName, "A donation to the school");
            string ask = condition switch
            {
                "captain" => $" We only ask that {s.FirstName} be made captain of the {sim.State.FindTeam(s.TeamId)?.Name}.",
                "room" => $" We would like {s.FirstName} moved to the best room in the dormitory.",
                "name" => " We would love for the library to carry our family name.",
                _ => "",
            };
            m.Body = $"We are delighted with how {s.FirstName} is doing and would like to donate {Ev.Money(m.Amount)} to the school.{ask}";

            m.AddOption("accept", "Accept gratefully", condition == "none" || condition == "name"
                ? $"+{Ev.Money(m.Amount)}."
                : $"+{Ev.Money(m.Amount)}. Other families may see favoritism.");
            if (condition == "captain" || condition == "room")
                m.AddOption("accept_no_strings", "Accept, but politely decline the condition",
                    "They may withdraw or reduce the gift.", "Integrity earns quiet trust.");
            m.AddOption("decline", "Decline the donation", "No money. Family slightly disappointed.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStudent(m.StudentId);
            int day = sim.Day;
            var fin = sim.State.Finance;
            switch (option)
            {
                case "accept":
                    fin.Post(day, LedgerCategory.Donations, m.Amount, "Donation from the " + (s?.LastName ?? "family"));
                    sim.State.Year.Highlight($"Received a {Ev.Money(m.Amount)} donation");
                    if (s == null) return "Donation received.";
                    s.ParentMoodlets.Set("Donation accepted", 5f, day + 90);
                    if (m.Tag == "captain")
                    {
                        var team = sim.State.FindTeam(s.TeamId);
                        if (team != null)
                            foreach (int id in team.Roster)
                                if (id != s.Id) sim.State.FindStudent(id)?.Moodlets.Set("Captaincy was bought", -4f, day + 45);
                        sim.ChangeTrust(-3f);
                        s.Remember(day, "Made team captain after a family donation.");
                        return "Money received. The team noticed how the captaincy was decided.";
                    }
                    if (m.Tag == "room")
                    {
                        Room best = null;
                        foreach (var r in sim.Operational(RoomType.DormRoom))
                            if (sim.Indices.ResidentsOf(r.Id).Count < r.Capacity && (best == null || r.Quality > best.Quality)) best = r;
                        if (best != null && best.Id != s.DormRoomId) sim.Facilities.AssignBed(s, best);
                        sim.ChangeTrust(-2f);
                        return "Money received and the student moved rooms. Other families grumble about favoritism.";
                    }
                    return "Money received.";
                case "accept_no_strings":
                    if (sim.Rng.Chance(0.6f + sim.Progression.NegotiationBonus))
                    {
                        float gift = Ev.Round100(m.Amount * 0.6f);
                        fin.Post(day, LedgerCategory.Donations, gift, "Donation (no conditions)");
                        sim.ChangeTrust(2f);
                        return $"They respected your position and gave {Ev.Money(gift)}.";
                    }
                    s?.ParentMoodlets.Set("Donation conditions refused", -5f, day + 60);
                    sim.ChangeTrust(1f);
                    return "They withdrew the offer.";
                default:
                    s?.ParentMoodlets.Set("Donation declined", -3f, day + 30);
                    if (m.Tag != "none" && m.Tag != "name") sim.ChangeTrust(2f);
                    return "Declined.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => "The family took the silence as a no.";

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) =>
            m.Tag == "none" || m.Tag == "name" ? "accept" : null;
    }

    sealed class BullyingReportEvent : EventTemplate
    {
        public override string Id => "bullying_report";
        public override bool Random => false;
        public override AdminRole Handler => AdminRole.StudentAffairsDirector;
        public override int CooldownDays => 10;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var victim = sim.State.FindStudent(m.StudentId);
            var bully = sim.State.FindStudent(m.StudentId2);
            if (victim == null || bully == null) return false;
            Ev.From(m, SenderType.Parent, victim.ParentName, $"{victim.FirstName} is being bullied", MessagePriority.Important);
            m.Body = $"{victim.FirstName} called us in tears. Another student, {bully.FullName}, has been bullying them for weeks. " +
                     "We expect the school to protect our child.";

            m.AddOption("discipline", $"Investigate and discipline {bully.FirstName}", "Victim's family reassured; the bully's family is upset.");
            bool sameRoom = victim.DormRoomId == bully.DormRoomId && victim.DormRoomId >= 0;
            m.AddOption("separate", sameRoom ? $"Move {victim.FirstName} to another room" : "Separate them in class groups", "Reduces contact between them.");
            var counsel = m.AddOption("counseling", "Arrange counseling for both", "Can repair the relationship.");
            if (sim.CountStaff(StaffRole.Counselor) == 0) counsel.DisabledReason = "Requires a counselor on staff";
            m.AddOption("ignore", "Treat it as normal teenage conflict", "Family outraged; trust and safety reputation fall.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var victim = sim.State.FindStudent(m.StudentId);
            var bully = sim.State.FindStudent(m.StudentId2);
            if (victim == null) return Ev.Gone("The student");
            int day = sim.Day;
            switch (option)
            {
                case "discipline":
                    if (bully != null)
                    {
                        bully.Discipline = MathUtil.Clamp100(bully.Discipline + 3f);
                        bully.Moodlets.Set("Disciplined for bullying", -6f, day + 14);
                        bully.ParentMoodlets.Set("Child disciplined", -6f, day + 45);
                    }
                    victim.ParentMoodlets.Set("School protected our child", 8f, day + 60);
                    victim.Moodlets.Set("The school stood up for me", 6f, day + 30);
                    sim.ChangeTrust(2f);
                    return "The bullying has stopped for now.";
                case "separate":
                    if (bully != null && victim.DormRoomId == bully.DormRoomId)
                    {
                        var room = sim.Facilities.FindBestBed(victim, victim.DormRoomId);
                        if (room != null) sim.Facilities.AssignBed(victim, room);
                    }
                    else if (bully != null)
                    {
                        var rel = sim.State.Relationships.GetOrCreate(victim.Id, bully.Id);
                        rel.Score = Math.Max(rel.Score, -30f);
                    }
                    victim.ParentMoodlets.Set("Children separated", 4f, day + 45);
                    return "They no longer share space.";
                case "counseling":
                    if (bully != null)
                    {
                        var rel = sim.State.Relationships.GetOrCreate(victim.Id, bully.Id);
                        rel.Score = MathUtil.Clamp(rel.Score + 30f, -100f, 100f);
                    }
                    victim.Stress = MathUtil.Clamp100(victim.Stress - 10f);
                    victim.ParentMoodlets.Set("Counseling arranged", 5f, day + 45);
                    return "Counseling sessions have begun.";
                default:
                    victim.ParentMoodlets.Set("Bullying ignored", -15f, day + 90);
                    victim.Moodlets.Set("Nobody helps me", -6f, day + 30);
                    sim.ChangeTrust(-4f);
                    sim.AddReputationModifier(ReputationId.Safety, "Bullying ignored", -3f, 60);
                    return "The family is outraged.";
            }
        }

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p) =>
            sim.CountStaff(StaffRole.Counselor) > 0 ? "counseling" : "discipline";
    }

    sealed class AthleteInjuryEvent : EventTemplate
    {
        public override string Id => "athlete_injury";
        public override bool Random => false;
        public override AdminRole Handler => AdminRole.AthleticDirector;
        public override int CooldownDays => 10;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = sim.State.FindStudent(m.StudentId);
            var team = sim.State.FindTeam(m.OtherId);
            if (s == null || team == null) return false;
            int days = s.InjuredUntilDay - sim.Day;
            Ev.From(m, SenderType.Parent, s.ParentName, $"{s.FirstName} was injured in training");
            m.Body = $"{s.FirstName} got hurt at {team.Sport} practice and will be out for about {days} days. " +
                     (team.Intensity == TrainingIntensity.Intense ? "Everyone says the training schedule is brutal. " : "") +
                     "Is the school taking athletes' health seriously?";
            var reduce = m.AddOption("reduce", "Reduce the team's training intensity", "Family reassured. Team rating drops a little.");
            if (team.Intensity == TrainingIntensity.Light) reduce.DisabledReason = "Training is already light";
            m.AddOption("medical", "Pay for specialist treatment ($1,200)", "Recovery twice as fast. Family satisfied.", cost: 1200f);
            m.AddOption("explain", "Explain that injuries are part of sport", "Family unimpressed, especially if training is intense.");
            return true;
        }

        public override string Resolve(Simulation sim, InboxMessage m, string option)
        {
            var s = sim.State.FindStudent(m.StudentId);
            var team = sim.State.FindTeam(m.OtherId);
            if (s == null) return Ev.Gone("The student");
            int day = sim.Day;
            switch (option)
            {
                case "reduce":
                    if (team != null && team.Intensity > TrainingIntensity.Light) team.Intensity--;
                    s.ParentMoodlets.Set("Training made safer", 6f, day + 45);
                    return $"Training intensity is now {team?.Intensity}.";
                case "medical":
                    sim.State.Finance.Post(day, LedgerCategory.SportsOps, -1200f, "Specialist treatment for " + s.FullName);
                    s.InjuredUntilDay = day + Math.Max(1, (s.InjuredUntilDay - day) / 2);
                    s.ParentMoodlets.Set("Paid for treatment", 8f, day + 45);
                    return "Recovery time halved.";
                default:
                    bool intense = team != null && team.Intensity == TrainingIntensity.Intense;
                    s.ParentMoodlets.Set("Injury brushed off", intense ? -8f : -3f, day + 45);
                    return "The family is not satisfied.";
            }
        }

        public override string Expire(Simulation sim, InboxMessage m) => Resolve(sim, m, "explain");

        public override string AutoDecide(Simulation sim, InboxMessage m, AdminPolicies p)
        {
            var team = sim.State.FindTeam(m.OtherId);
            return team != null && team.Intensity == TrainingIntensity.Intense ? "reduce" : "explain";
        }
    }

    sealed class ParentPraiseEvent : EventTemplate
    {
        public override string Id => "parent_praise";
        public override int CooldownDays => 40;

        public override float Weight(Simulation sim) => sim.State.Stat(StatId.ParentSatisfaction) >= 65f ? 0.4f : 0f;

        public override bool Build(Simulation sim, InboxMessage m)
        {
            var s = Ev.RandomStudent(sim, x => x.ParentMood >= 70f);
            if (s == null) return false;
            Ev.From(m, SenderType.Parent, s.ParentName, "Thank you", MessagePriority.Info);
            m.Body = $"We just wanted to say how happy we are with {sim.State.SchoolName}. {s.FirstName} loves it here, and we tell all our friends about you.";
            sim.ChangeTrust(1f);
            sim.State.ApplicantBonus = Math.Min(0.3f, sim.State.ApplicantBonus + 0.02f);
            return true;
        }
    }
}
