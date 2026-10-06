using System;
using System.Collections.Generic;

namespace SchoolSim
{
    public sealed class RankingEntry
    {
        public string Name;
        public float Score;
        public float Prestige;
        public int Students;
        public bool IsPlayer;
        public RivalSchool Rival;          // null for the player's school
        public float Value;                // what this table is sorted by
    }

    /// <summary>The league tables the papers publish.</summary>
    public enum RankingTable
    {
        Overall,
        Academic,
        Sports,
        Size,
    }

    /// <summary>AI rival schools that compete for students, staff, trophies and rank.</summary>
    public sealed class RivalSystem
    {
        readonly Simulation _sim;

        public RivalSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;

        public void Generate(int count)
        {
            var rng = _sim.Rng;
            var used = new HashSet<string> { S.SchoolName };
            var archetypes = new[]
            {
                Specialization.AcademicAcademy, Specialization.SportsAcademy, Specialization.LuxuryBoarding,
                Specialization.ArtsAcademy, Specialization.TechnologySchool, Specialization.CommunitySchool,
                Specialization.None, Specialization.None,
            };

            for (int i = 0; i < count; i++)
            {
                string name;
                do name = Names.School(rng); while (!used.Add(name));
                var arch = archetypes[i % archetypes.Length];
                float prestige = 25f + i * (60f / Math.Max(1, count - 1)) + rng.Range(-5f, 5f);

                var r = new RivalSchool
                {
                    Id = S.NewId(),
                    Name = name,
                    Archetype = arch,
                    Prestige = prestige,
                    Academic = prestige + (arch == Specialization.AcademicAcademy ? 10f : 0f) + rng.Range(-8f, 8f),
                    SportsRep = prestige + (arch == Specialization.SportsAcademy ? 15f : -5f) + rng.Range(-8f, 8f),
                    Trend = rng.Range(-0.2f, 0.3f),
                    Students = (int)(40 + prestige * 6 + rng.Range(-30, 30)),
                    Tuition = Balance.FairTuition(prestige) * rng.Range(0.9f, 1.2f),
                    SportStrength = new float[Enum.GetValues(typeof(SportType)).Length],
                };
                for (int s = 1; s < r.SportStrength.Length; s++)
                    r.SportStrength[s] = MathUtil.Clamp(r.SportsRep * 0.8f + rng.Range(-12f, 12f), 20f, 92f);
                S.Rivals.Add(r);
            }
            UpdateRank(announce: false);
        }

        public void Monthly()
        {
            var rng = _sim.Rng;
            foreach (var r in S.Rivals)
            {
                r.Trend = MathUtil.Clamp(r.Trend + rng.Range(-0.1f, 0.1f), -0.6f, 0.6f);
                r.Prestige = MathUtil.Clamp(r.Prestige + r.Trend + rng.Range(-0.5f, 0.5f), 15f, 95f);
                r.Academic = MathUtil.Clamp100(MathUtil.Approach(r.Academic, r.Prestige, 0.05f) + rng.Range(-0.5f, 0.5f));
                r.SportsRep = MathUtil.Clamp100(r.SportsRep + rng.Range(-0.8f, 0.8f));
                for (int s = 1; s < r.SportStrength.Length; s++)
                    r.SportStrength[s] = MathUtil.Clamp(r.SportStrength[s] + rng.Range(-1f, 1f), 20f, 95f);
                r.Students = Math.Max(30, r.Students + (int)(r.Trend * 5f) + rng.Range(-3, 4));
            }
            if (S.Day <= 0) return;

            if (!_sim.IsRevealed(Reveal.Rankings))
            {
                // The first monthly league table: the school finds out where it stands.
                UpdateRank(announce: false);
                _sim.Progression.Unveil(Reveal.Rankings);
                _sim.News.Publish(NewsTone.Neutral, NewsTopic.Rankings, $"New school rankings: {S.SchoolName} debuts at #{S.NationalRank}",
                    $"The national league table is out. {S.SchoolName} enters at #{S.NationalRank} of {S.Rivals.Count + 1}, " +
                    $"with {Rankings()[0].Name} on top.", 0f, ReputationId.Overall, 0, NewsSystem.Weekly);
                return;
            }
            UpdateRank(announce: true);
        }

        /// <summary>Once a year rivals shake things up: new trends, some invest heavily.</summary>
        public void Yearly()
        {
            var rng = _sim.Rng;
            foreach (var r in S.Rivals)
            {
                r.Trend = rng.Range(-0.3f, 0.4f);
                if (rng.Chance(0.15f))
                {
                    r.Trend += 0.5f;
                    _sim.Log($"{r.Name} announced a major expansion.");
                }
                r.Tuition *= 1f + Balance.WageInflationPerYear;
            }
        }

        public float PlayerScore => S.Prestige + (float)Math.Log(Math.Max(10, S.Students.Count), 2) * 1.5f;

        public List<RankingEntry> Rankings() => Rankings(RankingTable.Overall);

        /// <summary>A league table, best first. Overall is the national ranking.</summary>
        public List<RankingEntry> Rankings(RankingTable table)
        {
            var list = new List<RankingEntry>
            {
                new RankingEntry
                {
                    Name = S.SchoolName, Score = PlayerScore, Prestige = S.Prestige, Students = S.Students.Count, IsPlayer = true,
                    Value = table switch
                    {
                        RankingTable.Academic => S.Rep(ReputationId.Academic),
                        RankingTable.Sports => S.Rep(ReputationId.Sports),
                        RankingTable.Size => S.Students.Count,
                        _ => PlayerScore,
                    },
                },
            };
            foreach (var r in S.Rivals)
            {
                list.Add(new RankingEntry
                {
                    Name = r.Name, Score = r.Score, Prestige = r.Prestige, Students = r.Students, Rival = r,
                    Value = table switch
                    {
                        RankingTable.Academic => r.Academic,
                        RankingTable.Sports => r.SportsRep,
                        RankingTable.Size => r.Students,
                        _ => r.Score,
                    },
                });
            }
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            return list;
        }

        public void UpdateRank(bool announce)
        {
            var list = Rankings();
            int rank = list.FindIndex(e => e.IsPlayer) + 1;
            int old = S.NationalRank;
            S.NationalRank = rank;
            if (!announce || old == 0 || rank == old) return;

            string n = S.SchoolName;
            if (rank < old)
            {
                string passed = list[rank].Name;   // the school now directly below us
                _sim.Log($"Rankings: up to #{rank}, ahead of {passed}.", important: true);
                S.Year.Highlight($"Climbed to #{rank} in the national rankings");
                _sim.Progression.AddSchoolXp(30);
                _sim.News.Publish(NewsTone.Good, NewsTopic.Rankings, $"{n} climbs to #{rank} in the rankings",
                    $"{n} overtakes {passed} in this month's national league table.", 1f, ReputationId.Overall, 60, NewsSystem.Weekly);
            }
            else
            {
                _sim.Log($"Rankings: slipped to #{rank}.", important: true);
                string above = list[rank - 2].Name;   // the school that passed us
                if (rank - old >= 2)
                    _sim.News.Publish(NewsTone.Bad, NewsTopic.Rankings, $"{n} tumbles to #{rank}",
                        $"A bad month for {n}: down {rank - old} places in the national league table.", -1f, ReputationId.Overall, 45, NewsSystem.Weekly);
                else
                    _sim.News.Publish(NewsTone.Neutral, NewsTopic.Rankings, $"{n} slips to #{rank}",
                        $"{above} edges past {n} in this month's national league table.");
            }
        }

        public RivalSchool RandomRival() => S.Rivals.Count == 0 ? null : _sim.Rng.Pick(S.Rivals);

        /// <summary>A rival that can afford to poach from us: prefers stronger schools.</summary>
        public RivalSchool Poacher()
        {
            if (S.Rivals.Count == 0) return null;
            return _sim.Rng.PickWeighted(S.Rivals, r => Math.Max(1f, r.Prestige - S.Prestige + 30f));
        }
    }
}
