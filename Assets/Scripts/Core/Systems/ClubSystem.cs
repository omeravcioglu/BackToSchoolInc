using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>Clubs: founding, membership, skill growth, competitions and show income.</summary>
    public sealed class ClubSystem
    {
        readonly Simulation _sim;

        public ClubSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;

        public const float FoundingCost = 1000f;

        public void DailyTick()
        {
            if (!_sim.InSession || S.Clubs.Count == 0) return;
            foreach (var c in S.Clubs)
            {
                bool arts = IsArts(c.Type);
                foreach (int id in c.Members)
                {
                    var s = S.FindStudent(id);
                    if (s == null) continue;
                    if (arts) s.Artistic = MathUtil.Clamp100(s.Artistic + 0.01f);
                    else if (c.Type == ClubType.StudentGovernment || c.Type == ClubType.Debate) s.Social = MathUtil.Clamp100(s.Social + 0.008f);
                    if (IsAcademic(c.Type)) s.Academic = MathUtil.Clamp100(s.Academic + 0.006f);
                }
            }
        }

        public void Monthly()
        {
            if (!_sim.InSession || S.Clubs.Count == 0 || S.Day == 0) return;
            Recruit();

            foreach (var c in S.Clubs)
            {
                c.Strength = Strength(c);
                if (c.Members.Count >= 5 && (c.Type == ClubType.Drama || c.Type == ClubType.Music))
                    S.Finance.Post(S.Day, LedgerCategory.ClubEvents, S.Students.Count * 4f + c.Members.Count * 20f, $"{c.Name} show tickets");

                if (c.Type != ClubType.StudentGovernment && c.Members.Count >= 4 && _sim.Rng.Chance(0.2f))
                    Compete(c);
            }
        }

        void Recruit()
        {
            foreach (var s in S.Students)
            {
                if (s.ClubId >= 0 || !_sim.Rng.Chance(0.6f)) continue;
                Club best = null;
                float bestInterest = 0.5f;
                foreach (var c in S.Clubs)
                {
                    if (c.Members.Count >= Club.MaxMembers) continue;
                    float interest = Interest(s, c.Type);
                    if (interest > bestInterest) { bestInterest = interest; best = c; }
                }
                if (best == null) continue;
                best.Members.Add(s.Id);
                s.ClubId = best.Id;
                s.Remember(S.Day, $"Joined the {best.Name}.");
            }
        }

        public static float Interest(Student s, ClubType type)
        {
            float i = 0.3f;
            switch (type)
            {
                case ClubType.Robotics:
                    i += (s.Academic - 50f) / 50f + (s.Has(StudentTrait.Bookworm) ? 0.3f : 0f) + (s.Has(StudentTrait.Hardworking) ? 0.2f : 0f);
                    break;
                case ClubType.Chess:
                    i += (s.Academic - 50f) / 50f + (s.Has(StudentTrait.Introvert) ? 0.3f : 0f) + (s.Has(StudentTrait.Competitive) ? 0.3f : 0f);
                    break;
                case ClubType.Art:
                    i += (s.Artistic - 50f) / 40f + (s.Has(StudentTrait.Artistic) ? 0.5f : 0f);
                    break;
                case ClubType.Drama:
                    i += (s.Social - 50f) / 50f + (s.Has(StudentTrait.Extrovert) ? 0.4f : 0f) + (s.Has(StudentTrait.Artistic) ? 0.3f : 0f);
                    break;
                case ClubType.Music:
                    i += (s.Artistic - 50f) / 40f + (s.Has(StudentTrait.Artistic) ? 0.4f : 0f);
                    break;
                case ClubType.Debate:
                    i += (s.Social + s.Academic - 100f) / 80f + (s.Has(StudentTrait.Competitive) ? 0.3f : 0f) + (s.Has(StudentTrait.Leader) ? 0.3f : 0f);
                    break;
                case ClubType.Gaming:
                    i += 0.2f + (s.Has(StudentTrait.NightOwl) ? 0.3f : 0f) + (s.Has(StudentTrait.Introvert) ? 0.3f : 0f) + (s.Has(StudentTrait.Lazy) ? 0.3f : 0f);
                    break;
                case ClubType.Science:
                    i += (s.Academic - 50f) / 40f + (s.Has(StudentTrait.Bookworm) ? 0.4f : 0f);
                    break;
                case ClubType.StudentGovernment:
                    i += (s.Social - 50f) / 50f + (s.Has(StudentTrait.Leader) ? 0.6f : 0f) + (s.Has(StudentTrait.Extrovert) ? 0.2f : 0f);
                    break;
            }
            return i;
        }

        static bool IsArts(ClubType t) => t == ClubType.Art || t == ClubType.Music || t == ClubType.Drama;
        static bool IsAcademic(ClubType t) => t == ClubType.Robotics || t == ClubType.Chess || t == ClubType.Debate || t == ClubType.Science;

        float Skill(Student s, ClubType t)
        {
            if (IsArts(t)) return s.Artistic;
            if (t == ClubType.Debate) return (s.Academic + s.Social) / 2f;
            if (t == ClubType.Gaming) return (s.Academic + s.Social) / 2f;
            return s.Academic;
        }

        public float Strength(Club c)
        {
            if (c.Members.Count == 0) return 0f;
            float sum = 0f; int n = 0;
            foreach (int id in c.Members)
            {
                var s = S.FindStudent(id);
                if (s == null) continue;
                sum += Skill(s, c.Type);
                if (s.Has(StudentTrait.Leader)) sum += 5f;
                n++;
            }
            if (n == 0) return 0f;
            // The signature building doubles the specialization's edge (a theatre for the arts, a lab for robotics).
            return sum / n * 0.8f + Math.Min(15, n)
                   + SpecializationRules.ClubCompetitionBonus(S.Specialization, c.Type) * (1f + _sim.Indices.Signature);
        }

        void Compete(Club c)
        {
            bool national = _sim.IsUnlocked(Feature.LargeCompetitions);
            float difficulty = national ? 70f : 55f;
            float roll = c.Strength + _sim.Rng.Range(-10f, 10f);
            string level = national ? "national" : "regional";
            var rep = IsAcademic(c.Type) ? ReputationId.Academic : ReputationId.StudentLife;

            if (roll >= difficulty)
            {
                c.Wins++;
                float gain = national ? 4f : 2f;
                _sim.ShockReputation(rep, gain);
                _sim.AddReputationModifier(rep, $"{c.Name} {level} win", gain, 90);
                _sim.Progression.AddSchoolXp(national ? 80 : 40);
                foreach (int id in c.Members)
                {
                    var s = S.FindStudent(id);
                    if (s == null) continue;
                    s.Moodlets.Set("Won a competition", 6f, S.Day + 15);
                    s.Remember(S.Day, $"Won the {level} {c.Type} competition.");
                }
                S.Year.Highlight($"{c.Name} won a {level} competition");
                _sim.Inbox.Notify(SenderType.StudentAffairs, c.Name, $"{c.Name} wins {level} competition!",
                    $"The {c.Name} took first place at the {level} {c.Type} competition. {rep} reputation rises.");
                _sim.News.Publish(NewsTone.Good, IsAcademic(c.Type) ? NewsTopic.Academics : NewsTopic.StudentLife,
                    $"{S.SchoolName} {c.Name} wins the {level} title",
                    $"First place for the {c.Name} at the {level} {c.Type.ToString().ToLowerInvariant()} competition.", 1f, rep, 60, NewsSystem.Gazette);
            }
            else
            {
                foreach (int id in c.Members) S.FindStudent(id)?.Moodlets.Set("Lost a competition", -2f, S.Day + 7);
                _sim.Log($"The {c.Name} competed at a {level} event but didn't place.");
            }
        }

        // ------------------------------------------------------------------------------

        public int ClubSlots()
        {
            int slots = 0;
            foreach (var r in _sim.Operational(RoomType.ClubRoom)) slots += r.Capacity;
            return slots;
        }

        public int UsedSlots()
        {
            int used = 0;
            foreach (var c in S.Clubs) if (c.Type != ClubType.Gaming) used++;
            return used;
        }

        public bool CanCreate(ClubType type, out string reason)
        {
            if (!_sim.IsUnlocked(Feature.Clubs)) { reason = $"Clubs unlock at school level {Balance.UnlockLevel(Feature.Clubs)}"; return false; }
            foreach (var c in S.Clubs) if (c.Type == type) { reason = "Club already exists"; return false; }
            if (type == ClubType.Gaming)
            {
                if (!_sim.HasOperational(RoomType.GamingRoom)) { reason = "Needs a Gaming Room"; return false; }
            }
            else if (UsedSlots() >= ClubSlots()) { reason = "No free Club Room slot"; return false; }
            if (type == ClubType.Robotics && !_sim.HasOperational(RoomType.ComputerLab)) { reason = "Needs a Computer Lab"; return false; }
            if (type == ClubType.Science && !_sim.HasOperational(RoomType.ScienceLab)) { reason = "Needs a Science Lab"; return false; }
            if (S.Finance.Cash < FoundingCost) { reason = "Not enough money"; return false; }
            reason = null;
            return true;
        }

        public Club Create(ClubType type)
        {
            if (!CanCreate(type, out _)) return null;
            S.Finance.Post(S.Day, LedgerCategory.ClubOps, -FoundingCost, "Founded club");
            var club = new Club { Id = S.NewId(), Type = type, FoundedDay = S.Day };
            S.Clubs.Add(club);

            // Seed with the most interested students.
            var pool = new List<Student>(S.Students.FindAll(s => s.ClubId < 0));
            pool.Sort((a, b) => Interest(b, type).CompareTo(Interest(a, type)));
            foreach (var s in pool)
            {
                if (club.Members.Count >= 6 || Interest(s, type) < 0.4f) break;
                club.Members.Add(s.Id);
                s.ClubId = club.Id;
                s.Remember(S.Day, $"Founding member of the {club.Name}.");
            }
            _sim.Log($"Founded the {club.Name} with {club.Members.Count} members.", important: true);
            return club;
        }

        public void Disband(Club c)
        {
            foreach (int id in c.Members)
            {
                var s = S.FindStudent(id);
                if (s != null) s.ClubId = -1;
            }
            S.Clubs.Remove(c);
        }
    }
}
