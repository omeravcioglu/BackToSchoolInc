using System;
using System.Collections.Generic;
using System.Text;

namespace SchoolSim
{
    /// <summary>
    /// School level (size and prestige), Director level (management experience), and what the
    /// director can see: a new school starts with only a few panels and rooms, and the rest
    /// appears through the tutorial, level-ups and events.
    /// </summary>
    public sealed class ProgressionSystem
    {
        readonly Simulation _sim;

        public ProgressionSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;
        Progression P => _sim.State.Progress;

        public void Monthly()
        {
            if (S.Day == 0) return;
            float xp = S.Students.Count * 1.5f + S.Prestige * 1.8f;
            var last = S.Finance.LastCompletedMonth;
            if (last != null && last.Net > 0f) xp += 20f;
            AddSchoolXp((int)xp);

            if (_sim.IsUnlocked(Feature.EliteInstitution) && Calendar.Month(S.Day) == Balance.GalaMonth)
                _sim.Events.TriggerDirected("founders_gala", null, ignoreCooldown: true);
        }

        public void AddSchoolXp(int amount)
        {
            if (amount <= 0) return;
            P.SchoolXp += amount;
            while (P.SchoolXp >= Balance.SchoolXpForLevel(P.SchoolLevel + 1))
            {
                P.SchoolLevel++;
                OnSchoolLevelUp(P.SchoolLevel);
            }
        }

        public void AddDirectorXp(int amount)
        {
            P.DirectorXp += amount;
            while (P.DirectorXp >= Balance.DirectorXpForLevel(P.DirectorLevel + 1))
            {
                P.DirectorLevel++;
                string perk = P.DirectorLevel == Balance.DirectorInsightLevel
                    ? " You can now sense hidden consequences of your replies."
                    : " Negotiations succeed slightly more often.";
                _sim.Log($"Director level {P.DirectorLevel}!{perk}", important: true);
            }
        }

        /// <summary>Director negotiation bonus (added to success chances).</summary>
        public float NegotiationBonus => (P.DirectorLevel - 1) * 0.02f + AssistantRules.NegotiationBonus(S.Assistant);

        public bool ShowHints => P.DirectorLevel >= Balance.DirectorInsightLevel;

        public int SchoolXpToNext => Balance.SchoolXpForLevel(P.SchoolLevel + 1) - P.SchoolXp;

        void OnSchoolLevelUp(int level)
        {
            var sb = new StringBuilder();
            sb.Append($"{S.SchoolName} is now a level {level} school.");
            foreach (Feature f in Enum.GetValues(typeof(Feature)))
                if (Balance.UnlockLevel(f) == level) sb.Append("\n\nUnlocked: ").Append(DescribeFeature(f));
            foreach (var def in RoomsUnlockedAt(level))
                sb.Append("\n\nNew room: ").Append(def.Name).Append(". ").Append(def.Description);
            foreach (var r in RevealsAt(level))
            {
                Unveil(r, announce: false);
                sb.Append("\n\nNew: ").Append(DescribeReveal(r));
            }

            _sim.Inbox.Notify(SenderType.Government, "Board of Trustees", $"School level {level}", sb.ToString(), MessagePriority.Important);
            _sim.Log($"School level {level}!", important: true);
            S.Year.Highlight($"Reached school level {level}");
            _sim.News.Publish(NewsTone.Good, NewsTopic.School, $"{S.SchoolName} grows into a level {level} school",
                $"Bigger, busier and better known: {S.SchoolName} keeps climbing. Next stop, level {level + 1}.");
            _sim.RaiseLevelUp(level);

            if (level >= Balance.UnlockLevel(Feature.Specializations) && !P.SpecializationOffered)
            {
                P.SpecializationOffered = true;
                _sim.Events.TriggerDirected("specialization_choice", m => { }, ignoreCooldown: true);
            }
            if (level == Balance.UnlockLevel(Feature.SecondCampus) && !S.Campus.EstateOpen)
                _sim.Events.TriggerDirected("estate_offer", null, ignoreCooldown: true);
            if (level == Balance.UnlockLevel(Feature.EliteInstitution))
            {
                S.Year.Highlight("Became an elite national institution");
                _sim.News.Publish(NewsTone.Good, NewsTopic.School, $"{S.SchoolName} joins the nation's elite",
                    $"From a struggling little school to an elite national institution: {S.SchoolName} now courts the country's best students.",
                    3f, ReputationId.Overall, 180, NewsSystem.Weekly);
            }
        }

        // ------------------------------------------------------------------------------
        // What the director can see
        // ------------------------------------------------------------------------------

        public bool IsRevealed(Reveal r) => P.Revealed[(int)r];

        /// <summary>Brings a part of the game into view. Announced with a toast unless told otherwise.</summary>
        public void Unveil(Reveal r, bool announce = true)
        {
            if (P.Revealed[(int)r]) return;
            P.Revealed[(int)r] = true;
            _sim.RaiseRevealed(r, announce);
        }

        /// <summary>Parts of the game that open with a school level (others come from events).</summary>
        public static IEnumerable<Reveal> RevealsAt(int level)
        {
            if (level == 2)
            {
                yield return Reveal.Rules;
                yield return Reveal.Stats;
                yield return Reveal.Land;
                yield return Reveal.Admissions;
            }
        }

        /// <summary>Rooms a level brings for every school (signature buildings come with a specialization, the manor with the estate).</summary>
        public static IEnumerable<RoomDefinition> RoomsUnlockedAt(int level)
        {
            if (level <= 1) yield break;
            foreach (var def in RoomCatalog.All)
                if (def.MinSchoolLevel == level && def.Buildable && def.Signature == Specialization.None) yield return def;
        }

        public static string DescribeReveal(Reveal r) => r switch
        {
            Reveal.Rules => "School rules: tuition, food, curfew and discipline.",
            Reveal.Stats => "Statistics: every number about the school, over time.",
            Reveal.Land => "Land: buy the plots next to the campus.",
            Reveal.Admissions => "Admissions: pick new students from the applicant pool.",
            Reveal.News => "News: what the papers say about the school.",
            Reveal.Rankings => "Rankings: how the school compares nationally.",
            Reveal.Promises => "Promises: the commitments you've made.",
            _ => r.ToString(),
        };

        public static string DescribeFeature(Feature f) => f switch
        {
            Feature.Clubs => "Clubs: build Club Rooms and found clubs.",
            Feature.Administrators => "Administrators: hire directors who answer routine messages using your policies.",
            Feature.Scholarships => "Scholarships: academic, athletic, arts and financial-aid awards.",
            Feature.AdvancedDorms => "Advanced dorms: dorm rooms can be upgraded to levels 3-5.",
            Feature.Specializations => "Specializations: choose your school's identity, then build its signature building.",
            Feature.SportsPrograms => "Sports programs: form teams, hire coaches, compete against rivals.",
            Feature.InternationalStudents => "International students: families from abroad apply and pay an international fee. An International House helps them settle in.",
            Feature.MajorSponsorships => "Major sponsorships: much larger sponsor offers.",
            Feature.AdvancedDepartments => "Advanced departments: top-level classrooms and labs.",
            Feature.LargeCompetitions => "Large-scale competitions: national club championships.",
            Feature.SecondCampus => "Second campus: buy the old estate across the road, with its manor and room to grow.",
            Feature.EliteInstitution => "Elite institution: national scholars apply, prestige rises and a yearly Founders' Gala raises money.",
            Feature.Endowment => "Endowment: give money to a fund for good. It pays out every September and adds prestige.",
            _ => f.ToString(),
        };
    }
}
