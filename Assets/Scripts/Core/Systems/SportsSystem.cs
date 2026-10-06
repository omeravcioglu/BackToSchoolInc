using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Competitive teams: rosters, coaching, training intensity, a regular season against rival
    /// schools, home-game ticket income, injuries and championships.
    /// </summary>
    public sealed class SportsSystem
    {
        readonly Simulation _sim;

        public SportsSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;

        const int LastRegularSeasonMonth = 9;
        const int ChampionshipMonth = 10;

        public void DailyTick()
        {
            if (S.Teams.Count == 0) return;
            foreach (var t in S.Teams) UpdateRating(t);
            if (!_sim.InSession) return;

            int day = S.Day;
            int month = Calendar.Month(day);
            foreach (var t in S.Teams)
            {
                DevelopAthletes(t);
                if (month <= LastRegularSeasonMonth && day >= t.NextGameDay && t.Roster.Count >= SportsTeam.MinRoster(t.Sport))
                    PlayGame(t);
            }

            if (month == ChampionshipMonth && Calendar.DayOfMonth(day) == 5)
            {
                foreach (var t in S.Teams)
                    if (t.Wins + t.Losses >= 6 && t.WinRate >= 0.6f)
                        _sim.Events.TriggerDirected("championship_game", m => m.OtherId = t.Id, ignoreCooldown: true);
            }
        }

        public void Monthly() { }

        public void StartSeason()
        {
            foreach (var t in S.Teams)
            {
                t.Wins = 0;
                t.Losses = 0;
                t.GamesThisSeason = 0;
                t.Results.Clear();
                t.NextGameDay = S.Day + 14 + _sim.Rng.Range(0, 5);
            }
        }

        // ------------------------------------------------------------------------------

        public static float SportScore(Student s, SportType sport) => s.Athletic + (s.SportTalent == sport ? 12f : 0f);

        public Room HomeVenue(SportType sport)
        {
            Room best = null;
            foreach (var r in S.Rooms)
                if (r.IsOperational(S.Day) && RoomCatalog.SupportsSport(r.Type, sport) && (best == null || r.Quality > best.Quality))
                    best = r;
            return best;
        }

        public void UpdateRating(SportsTeam t)
        {
            var b = t.RatingFactors;
            b.Clear();
            int need = SportsTeam.MinRoster(t.Sport);

            var scores = new List<float>();
            foreach (int id in t.Roster)
            {
                var s = S.FindStudent(id);
                if (s != null && !s.IsInjured(S.Day)) scores.Add(SportScore(s, t.Sport));
            }
            scores.Sort((a, c) => c.CompareTo(a));
            float sum = 0f;
            int used = Math.Min(need, scores.Count);
            for (int i = 0; i < used; i++) sum += scores[i];
            float players = sum / need;   // missing players count as zero
            b.Add(used < need ? $"Players ({need - used} short!)" : "Players", players * 0.55f);

            var coach = S.FindStaff(t.CoachId);
            b.Add(coach == null ? "No coach" : "Coach " + coach.Name, _sim.Stats.CoachEffect(coach) * 0.3f);

            var venue = HomeVenue(t.Sport);
            b.Add(venue == null ? "No home venue" : "Facility: " + venue.Name, venue == null ? 0f : venue.Quality * 0.15f);

            if (t.Intensity == TrainingIntensity.Light) b.Add("Light training", -3f);
            if (t.Intensity == TrainingIntensity.Intense) b.Add("Intense training", 4f);

            float chem = Chemistry(t);
            if (Math.Abs(chem) > 0.1f) b.Add("Team chemistry", chem);

            float spec = SpecializationRules.TeamRatingBonus(S.Specialization);
            if (spec != 0) b.Add("Specialization", spec);
            float signature = _sim.Indices.SignatureEffect(x => x.TeamRating);
            if (signature >= 0.05f) b.Add(_sim.Indices.SignatureBuilding.Name, signature);

            t.Rating = b.Clamped;
        }

        float Chemistry(SportsTeam t)
        {
            if (t.Roster.Count < 2) return 0f;
            float sum = 0f; int n = 0;
            for (int i = 0; i < t.Roster.Count; i++)
                for (int j = i + 1; j < t.Roster.Count; j++)
                {
                    sum += S.Relationships.Score(t.Roster[i], t.Roster[j]);
                    n++;
                }
            return MathUtil.Clamp(sum / n / 10f, -5f, 5f);
        }

        void DevelopAthletes(SportsTeam t)
        {
            var coach = S.FindStaff(t.CoachId);
            float dev = (coach?.PlayerDevelopment ?? 30f) / 50f;
            float mult = t.Intensity == TrainingIntensity.Light ? 0.6f : t.Intensity == TrainingIntensity.Intense ? 1.5f : 1f;
            float injury = t.Intensity == TrainingIntensity.Light ? 0.0002f : t.Intensity == TrainingIntensity.Intense ? 0.0015f : 0.0005f;
            injury *= 1f - _sim.Indices.SignatureEffect(x => x.InjuryCut);   // a physio suite keeps players fit

            foreach (int id in t.Roster)
            {
                var s = S.FindStudent(id);
                if (s == null || s.IsInjured(S.Day)) continue;
                s.Athletic = MathUtil.Clamp100(s.Athletic + 0.015f * dev * mult * (1f - s.Athletic / 120f));

                if (_sim.Rng.Chance(injury))
                {
                    int days = _sim.Rng.Range(14, 40);
                    s.InjuredUntilDay = S.Day + days;
                    s.Remember(S.Day, $"Injured during {t.Sport} training ({days} days out).");
                    s.ParentMoodlets.Set("Child injured in training", -8f, S.Day + 45);
                    if (!_sim.Events.TriggerDirected("athlete_injury", m => { m.StudentId = s.Id; m.OtherId = t.Id; }))
                        _sim.Log($"{s.FullName} was injured in {t.Sport} training.");
                }
            }
        }

        void PlayGame(SportsTeam t)
        {
            var rival = _sim.Rivals.RandomRival();
            if (rival == null) return;
            float strength = rival.SportStrength[(int)t.Sport] + _sim.Rng.Range(-5f, 5f);
            float p = MathUtil.Sigmoid((t.Rating - strength) / 8f);
            bool win = _sim.Rng.Chance(p);
            bool home = t.HomeNext;
            t.HomeNext = !home;
            t.GamesThisSeason++;
            t.NextGameDay = S.Day + 14;

            string where = home ? "vs" : "at";
            if (win) t.Wins++; else t.Losses++;
            string line = $"{Calendar.Format(S.Day)}: {(win ? "W" : "L")} {where} {rival.Name}";

            if (home)
            {
                var venue = HomeVenue(t.Sport);
                float capacity = venue?.Capacity ?? 50f;
                float attendance = Math.Min(capacity, 30f + S.Rep(ReputationId.Sports) * 4f + S.Students.Count * 0.6f);
                float income = attendance * S.Policies.TicketPrice;
                S.Finance.Post(S.Day, LedgerCategory.Tickets, income, $"{t.Sport} home game tickets");
                line += $" ({attendance:0} fans)";
            }

            t.Results.Add(line);
            if (t.Results.Count > 20) t.Results.RemoveAt(0);
            _sim.ShockReputation(ReputationId.Sports, win ? 0.6f : -0.4f);
            foreach (int id in t.Roster)
                S.FindStudent(id)?.Moodlets.Set(win ? "Won last game" : "Lost last game", win ? 3f : -2f, S.Day + 5);
            _sim.Log($"{t.Name}: {line}");
        }

        public bool PlayChampionship(SportsTeam t, float bonus)
        {
            RivalSchool opponent = null;
            foreach (var r in S.Rivals)
                if (opponent == null || r.SportStrength[(int)t.Sport] > opponent.SportStrength[(int)t.Sport]) opponent = r;
            if (opponent == null) return false;

            UpdateRating(t);
            float p = MathUtil.Sigmoid((t.Rating + bonus - opponent.SportStrength[(int)t.Sport]) / 8f);
            bool win = _sim.Rng.Chance(p);

            if (win)
            {
                t.Championships++;
                S.SportsLegacy = Math.Min(15f, S.SportsLegacy + 3f);
                _sim.AddReputationModifier(ReputationId.Sports, $"{t.Sport} champions", 10f, 180);
                _sim.AddReputationModifier(ReputationId.Overall, $"{t.Sport} champions", 3f, 120);
                _sim.ShockReputation(ReputationId.Sports, 5f);
                _sim.AllStudentsMood($"{t.Sport} champions!", 6f, 20);
                _sim.AllParentsMood("Championship pride", 3f, 30);
                _sim.Progression.AddSchoolXp(150);
                S.Finance.Post(S.Day, LedgerCategory.Merchandise, 2000f + S.Students.Count * 15f, "Championship merchandise");
                S.Year.Championships++;
                S.Year.Highlight($"Won the {t.Sport} championship against {opponent.Name}");
                foreach (int id in t.Roster) S.FindStudent(id)?.Remember(S.Day, $"Won the {t.Sport} championship.");
                _sim.Inbox.Notify(SenderType.Sports, "Athletics Department", $"{t.Sport} CHAMPIONS!",
                    $"The {t.Name} beat {opponent.Name} in the final. The whole school is celebrating, sponsors are calling, and sports reputation is soaring.",
                    MessagePriority.Important);
                _sim.News.Publish(NewsTone.Good, NewsTopic.Sports, $"{S.SchoolName} are {t.Sport.ToString().ToLowerInvariant()} champions!",
                    $"The {t.Name} beat {opponent.Name} in a thrilling final. Celebrations went on well past curfew.", 2f, ReputationId.Sports, 90);
            }
            else
            {
                foreach (int id in t.Roster) S.FindStudent(id)?.Moodlets.Set("Lost the final", -4f, S.Day + 10);
                S.Year.Highlight($"{t.Name} lost the final to {opponent.Name}");
                _sim.Inbox.Notify(SenderType.Sports, "Athletics Department", $"{t.Sport}: lost the final",
                    $"The {t.Name} fell to {opponent.Name} in the championship final. Reaching the final still raised the program's profile.");
                _sim.News.Publish(NewsTone.Neutral, NewsTopic.Sports, $"{S.SchoolName} fall in the {t.Sport.ToString().ToLowerInvariant()} final",
                    $"{opponent.Name} were too strong on the day, but the {t.Name} made the whole school proud.");
                _sim.ShockReputation(ReputationId.Sports, 1.5f);
            }
            t.Results.Add($"{Calendar.Format(S.Day)}: FINAL {(win ? "W" : "L")} vs {opponent.Name}");
            return win;
        }

        // ------------------------------------------------------------------------------
        // Team management
        // ------------------------------------------------------------------------------

        public bool CanCreateTeam(SportType sport, out string reason)
        {
            if (!_sim.IsUnlocked(Feature.SportsPrograms))
            {
                reason = $"Sports programs unlock at school level {Balance.UnlockLevel(Feature.SportsPrograms)}";
                return false;
            }
            foreach (var t in S.Teams) if (t.Sport == sport) { reason = "Team already exists"; return false; }
            if (HomeVenue(sport) == null) { reason = "Needs a home venue for " + sport; return false; }
            reason = null;
            return true;
        }

        public SportsTeam CreateTeam(SportType sport)
        {
            if (!CanCreateTeam(sport, out _)) return null;
            var t = new SportsTeam { Id = S.NewId(), Sport = sport, NextGameDay = S.Day + 14 };
            S.Teams.Add(t);
            AutoFillRoster(t);
            UpdateRating(t);
            _sim.Log($"Founded the {t.Name} with {t.Roster.Count} players.", important: true);
            return t;
        }

        public void DisbandTeam(SportsTeam t)
        {
            foreach (int id in t.Roster)
            {
                var s = S.FindStudent(id);
                if (s != null) s.TeamId = -1;
            }
            S.Teams.Remove(t);
            _sim.AllStudentsMood($"{t.Name} disbanded", -2f, 20);
        }

        public void AutoFillRoster(SportsTeam t)
        {
            int max = SportsTeam.MaxRoster(t.Sport);
            var pool = S.Students.FindAll(s => s.TeamId < 0 || s.TeamId == t.Id);
            pool.Sort((a, b) => SportScore(b, t.Sport).CompareTo(SportScore(a, t.Sport)));

            foreach (int id in t.Roster)
            {
                var s = S.FindStudent(id);
                if (s != null) s.TeamId = -1;
            }
            t.Roster.Clear();
            foreach (var s in pool)
            {
                if (t.Roster.Count >= max) break;
                if (SportScore(s, t.Sport) < 40f) break;
                t.Roster.Add(s.Id);
                s.TeamId = t.Id;
            }
        }

        public bool AddToRoster(SportsTeam t, Student s, out string reason)
        {
            if (s.TeamId >= 0 && s.TeamId != t.Id) { reason = "Already on another team"; return false; }
            if (t.Roster.Count >= SportsTeam.MaxRoster(t.Sport)) { reason = "Roster is full"; return false; }
            if (!t.Roster.Contains(s.Id)) t.Roster.Add(s.Id);
            s.TeamId = t.Id;
            reason = null;
            return true;
        }

        public void RemoveFromRoster(SportsTeam t, Student s)
        {
            t.Roster.Remove(s.Id);
            if (s.TeamId == t.Id) s.TeamId = -1;
        }

        public void SetCoach(SportsTeam t, StaffMember coach)
        {
            foreach (var other in S.Teams) if (other.CoachId == coach.Id) other.CoachId = -1;
            t.CoachId = coach.Id;
        }
    }
}
