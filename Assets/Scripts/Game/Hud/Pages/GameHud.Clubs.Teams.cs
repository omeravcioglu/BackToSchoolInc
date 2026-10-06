using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The teams half of the clubs and teams page: a pennant for each team (and one to start a
    /// new team). The picked team shows its record, season standing and rating, its coach and the
    /// other coaches to choose from, training intensity, the roster with injuries, auto-pick and
    /// disbanding, what its rating is made of and its latest results.
    /// </summary>
    public sealed partial class GameHud
    {
        const int ClbNewTeam = -2;              // _clbTeam while the "start a team" list is open
        const int ClbLastSeasonMonth = 9;       // games run September to May; the final is in June (SportsSystem)
        const int ClbFinalGames = 6;            // a team needs this many games and this share of wins
        const float ClbFinalWinRate = 0.6f;     // to reach the final
        const float ClbAthleteCut = 40f;        // new teams only take students this sporty (SportsSystem.AutoFillRoster)

        int _clbTeam = -1;                      // the team picked, -1 for the first one

        void ClbFillTeams()
        {
            bool unlocked = _sim.IsUnlocked(Feature.SportsPrograms);
            _clbTeams.EnableInClassList("clb-teams--locked", !unlocked);
            _clbPennants.Clear();
            _clbTeamBody.Clear();
            SetText(_clbTeamsNote, unlocked ? "$600/mo each" : "");
            if (!unlocked)
            {
                var flag = new InkPicture(ClbFlag(SportType.None));
                flag.AddToClassList("clb-locked-flag");
                _clbTeamBody.Add(ClbLocked(flag, $"Competitive teams unlock at school level {Balance.UnlockLevel(Feature.SportsPrograms)}.",
                    "Sports facilities still make athletic students happier."));
                return;
            }

            int open = ClbSportsWithoutTeam();
            var team = _clbTeam >= 0 ? S.FindTeam(_clbTeam) : null;
            if (team == null && S.Teams.Count > 0 && (_clbTeam != ClbNewTeam || open == 0)) team = S.Teams[0];
            foreach (var t in S.Teams)
            {
                int id = t.Id;
                _clbPennants.Add(ClbPennant(t.Sport, $"{t.Wins}–{t.Losses}", t == team, () =>
                {
                    _clbTeam = id;
                    ClbRebuild();
                }));
            }
            if (open > 0)
                _clbPennants.Add(ClbPennant(SportType.None, "start a team", team == null, () =>
                {
                    _clbTeam = ClbNewTeam;
                    ClbRebuild();
                }));

            if (team != null) _clbTeamBody.Add(ClbTeamCard(team));
            else ClbFillFounding();
        }

        int ClbSportsWithoutTeam()
        {
            int n = 0;
            foreach (SportType sport in Enum.GetValues(typeof(SportType)))
                if (sport != SportType.None && !S.Teams.Exists(t => t.Sport == sport)) n++;
            return n;
        }

        VisualElement ClbPennant(SportType sport, string caption, bool on, Action onClick)
        {
            var pennant = Box("clb-pennant");
            pennant.EnableInClassList("clb-pennant--on", on);
            pennant.Add(ClbFlagWithIcon(sport, "clb-pennant-flag"));
            pennant.Add(Text(sport == SportType.None ? "New" : sport.ToString(), "font-heavy", "clb-pennant-name"));
            pennant.Add(Text(caption, "font-note", "clb-pennant-sub"));
            OnClick(pennant, onClick);
            return pennant;
        }

        /// <summary>A team pennant in its sport's colour with the sport's icon on the cloth; a dashed blank one for a new team.</summary>
        static VisualElement ClbFlagWithIcon(SportType sport, string size)
        {
            var box = Box("clb-flag-box", size);
            var flag = new InkPicture(ClbFlag(sport));
            flag.AddToClassList("clb-flag");
            box.Add(flag);
            var icon = Icon(sport == SportType.None ? "plus" : ClbSportIcon(sport), "clb-flag-icon");
            if (sport == SportType.None) icon.AddToClassList("icon--soft");
            box.Add(icon);
            return box;
        }

        static InkDrawing ClbFlag(SportType sport)
        {
            const string cloth = "M9 6C24 9 42 14 58 21C42 26 24 31 9 36z";
            var d = new InkDrawing(64f, 48f);
            if (sport == SportType.None)
            {
                var soft = InkIcons.Hex(0x7FA6D6);
                return d.Shape(cloth, InkIcons.Paper).Dashed(cloth, soft, 2.2f, 5f, 4f).Line("M7 4v41", soft, 2.6f);
            }
            return d.Shape(cloth, ClbSportColor(sport), InkIcons.Ink, 2.4f)
                .Line("M7 4v41", InkIcons.Ink, 2.8f)
                .Circle(7f, 3.6f, 2.3f, InkIcons.Marker, InkIcons.Ink, 1.6f);
        }

        // --- A team ---------------------------------------------------------------------------

        VisualElement ClbTeamCard(SportsTeam t)
        {
            int id = t.Id;
            var card = Ink("clb-team");
            var top = Box("clb-team-top");
            top.Add(ClbFlagWithIcon(t.Sport, "clb-team-flag"));
            var title = Box("clb-team-title");
            var name = Text("", "font-hand", "clb-team-name");
            SetHandText(name, t.Name);
            title.Add(name);
            var record = Box("clb-record");
            record.Add(Text($"{t.Wins} won", "font-heavy", "clb-record-won"));
            record.Add(Text($"{t.Losses} lost", "font-heavy", "clb-record-lost"));
            for (int i = 0; i < Mathf.Min(t.Championships, 5); i++) record.Add(Icon("trophy", "clb-trophy"));
            if (t.Championships > 0) record.Add(Text(t.Championships == 1 ? "1 title" : $"{t.Championships} titles", "font-note", "clb-titles"));
            title.Add(record);
            var standing = Text(ClbStanding(t, out bool trouble), "font-note", "clb-standing");
            standing.EnableInClassList("clb-standing--bad", trouble);
            title.Add(standing);
            top.Add(title);
            var rating = Ink("clb-rating");
            rating.Add(Text(Mathf.RoundToInt(t.Rating).ToString(), "font-hand", "clb-rating-value"));
            rating.Add(Text("rating", "font-heavy", "clb-rating-label"));
            top.Add(rating);
            card.Add(top);

            var body = Box("clb-team-body");
            var left = Box("clb-team-left");
            left.Add(ClbSection("Coach", null));
            ClbCoaches(left, t);
            left.Add(ClbSection("Training", null));
            left.Add(Pills(new[] { "Light", "Normal", "Intense" }, () => (int)t.Intensity,
                i => RunCommand(() => _sim.SetIntensity(id, (TrainingIntensity)i))));
            left.Add(Text(ClbIntensityNote(t.Intensity), "font-note", "clb-note"));
            left.Add(ClbSection("Rating", "what it's made of"));
            ClbFactors(left, t.RatingFactors);
            body.Add(left);

            var right = Box("clb-team-right");
            right.Add(ClbSection($"Roster {t.Roster.Count}/{SportsTeam.MaxRoster(t.Sport)}", $"needs {SportsTeam.MinRoster(t.Sport)} to play"));
            var players = new List<Student>();
            foreach (int sid in t.Roster)
            {
                var s = S.FindStudent(sid);
                if (s != null) players.Add(s);
            }
            players.Sort((a, b) => SportsSystem.SportScore(b, t.Sport).CompareTo(SportsSystem.SportScore(a, t.Sport)));
            foreach (var s in players) right.Add(ClbPlayer(t, s));
            if (players.Count == 0) right.Add(Text("No players yet: auto-pick finds the best athletes.", "font-note", "clb-empty"));
            var actions = Box("clb-actions");
            actions.Add(ClbButton("people", "Auto-pick best roster", () => RunCommand(() => _sim.AutoRoster(id))));
            actions.Add(ClbDisband("team" + id, "Disband team", () => RunCommand(() => _sim.DisbandTeam(id))));
            right.Add(actions);
            right.Add(ClbSection("Results", "latest first"));
            int shown = 0;
            for (int i = t.Results.Count - 1; i >= 0 && shown < 8; i--, shown++) right.Add(ClbResult(t.Results[i]));
            if (shown == 0) right.Add(Text("No games yet this season.", "font-note", "clb-empty"));
            body.Add(right);
            card.Add(body);
            return card;
        }

        /// <summary>Where the team stands in the season: next game, whether it can play, and the road to the June final.</summary>
        string ClbStanding(SportsTeam t, out bool trouble)
        {
            trouble = false;
            int games = t.Wins + t.Losses, min = SportsTeam.MinRoster(t.Sport);
            bool qualifies = games >= ClbFinalGames && t.WinRate >= ClbFinalWinRate;
            string share = Mathf.RoundToInt(ClbFinalWinRate * 100f) + "%";
            if (!_sim.InSession) return "Summer break: the new season starts in September.";
            if (Calendar.Month(S.Day) > ClbLastSeasonMonth)
                return qualifies ? "Qualified for the June final!" : $"Missed the final: it takes {ClbFinalGames}+ games and {share} wins.";
            if (t.Roster.Count < min)
            {
                trouble = true;
                return $"Can't play: needs {min} players, has {t.Roster.Count}.";
            }
            string next = (t.NextGameDay <= S.Day + 1 ? "Next game tomorrow" : $"Next game {MonthDay(t.NextGameDay)}") + (t.HomeNext ? ", at home" : ", away");
            string final = games < ClbFinalGames ? $"{ClbFinalGames - games} more to qualify for the final"
                : qualifies ? "on course for the final" : $"needs {share} wins for the final";
            return next + " · " + final;
        }

        static string ClbIntensityNote(TrainingIntensity intensity) => intensity switch
        {
            TrainingIntensity.Light => "rating -3, slower growth, fewest injuries",
            TrainingIntensity.Intense => "rating +4, fastest growth, most injuries",
            _ => "steady growth, a few injuries",
        };

        /// <summary>The coach (or a warning), then every other coach on the staff to pick from.</summary>
        void ClbCoaches(VisualElement parent, SportsTeam t)
        {
            var coach = S.FindStaff(t.CoachId);
            var row = Box("clb-coach");
            if (coach != null)
            {
                row.Add(ClbFace(coach, "clb-face--coach"));
                var text = Box("clb-coach-text");
                text.Add(Text(coach.Name, "font-heavy", "clb-coach-name"));
                text.Add(Text($"coaching skill {coach.Skill:0}" + (coach.Sport != SportType.None ? $" · {coach.Sport} coach" : ""), "font-semi", "clb-coach-sub"));
                row.Add(text);
                int staffId = coach.Id;
                OnClick(row, () => ClbOpenStaff(staffId));
                row.AddToClassList("clb-coach--link");
            }
            else
            {
                var badge = Ink("clb-warn");
                badge.Add(Icon("alert", "icon-17", "icon--coral"));
                row.Add(badge);
                row.Add(Text("No coach! Hire one in the staff room, then pick them here.", "font-semi", "clb-coach-missing"));
            }
            parent.Add(row);

            int teamId = t.Id, others = 0;
            foreach (var c in S.Staff)
            {
                if (c.Role != StaffRole.Coach || c.Id == t.CoachId) continue;
                others++;
                var busy = S.Teams.Find(o => o.CoachId == c.Id);
                var chip = Ink("clb-chip");
                chip.Add(ClbFace(c, "clb-face--small"));
                var text = Box("clb-chip-text");
                text.Add(Text(c.Name, "font-heavy", "clb-chip-name"));
                text.Add(Text($"skill {c.Skill:0}" + (c.Sport != SportType.None ? $" · {c.Sport}" : "") + (busy != null ? $" · coaches the {busy.Name}" : ""),
                    "font-semi", "clb-chip-sub"));
                chip.Add(text);
                chip.Add(Text("pick", "font-heavy", "clb-chip-pick"));
                int coachId = c.Id;
                OnClick(chip, () => RunCommand(() => _sim.SetCoach(teamId, coachId)));
                parent.Add(chip);
            }
            if (coach == null && others == 0) parent.Add(ClbLink("staff", "to the staff room", () => ShowPageAt(HudPage.Staff)));
        }

        VisualElement ClbPlayer(SportsTeam t, Student s)
        {
            int studentId = s.Id, teamId = t.Id;
            var row = Box("clb-player");
            var who = Box("clb-player-who");
            who.Add(ClbFace(s, "clb-face--small"));
            who.Add(Text(s.FullName, "font-semi", "clb-player-name"));
            if (s.SportTalent == t.Sport) who.Add(Icon("star", "clb-talent"));
            if (s.IsInjured(S.Day)) who.Add(Text($"hurt till {MonthDay(s.InjuredUntilDay)}", "font-heavy", "clb-hurt"));
            OnClick(who, () => ClbOpenStudent(studentId));
            row.Add(who);
            row.Add(Text(Mathf.RoundToInt(SportsSystem.SportScore(s, t.Sport)).ToString(), "font-heavy", "clb-player-score"));
            var drop = Ink("clb-x");
            drop.Add(Icon("close", "clb-x-icon"));
            OnClick(drop, () => ClbDrop(teamId, studentId));
            row.Add(drop);
            return row;
        }

        /// <summary>Takes a player off the roster. There's no command for it, so the sports system does it directly.</summary>
        void ClbDrop(int teamId, int studentId)
        {
            var t = S.FindTeam(teamId);
            var s = S.FindStudent(studentId);
            if (t == null || s == null) return;
            RunCommand(() =>
            {
                _sim.Sports.RemoveFromRoster(t, s);
                return CommandResult.Success($"{s.FirstName} is off the {t.Name}.");
            });
        }

        /// <summary>A result line from the sports system ("Y1 Oct 14: W vs ..."), with a W or L mark in front.</summary>
        static VisualElement ClbResult(string line)
        {
            var row = Box("clb-result");
            bool won = line.Contains(": W ") || line.Contains("FINAL W ");
            bool lost = line.Contains(": L ") || line.Contains("FINAL L ");
            if (won || lost) row.Add(Text(won ? "W" : "L", "font-heavy", "clb-mark", won ? "clb-mark--won" : "clb-mark--lost"));
            if (line.Contains("FINAL")) row.Add(Icon("trophy", "clb-final"));
            row.Add(Text(line, "font-semi", "clb-result-text"));
            return row;
        }

        // --- Starting a team ------------------------------------------------------------------

        void ClbFillFounding()
        {
            _clbTeamBody.Add(Text("Start a team", "font-hand", "clb-sub-title"));
            foreach (SportType sport in Enum.GetValues(typeof(SportType)))
                if (sport != SportType.None && !S.Teams.Exists(t => t.Sport == sport)) _clbTeamBody.Add(ClbFoundTeam(sport));
            _clbTeamBody.Add(Text("Teams cost $600/mo in travel and equipment. Wins raise sports reputation, fill the stands and attract sponsors.",
                "font-note", "clb-foot"));
        }

        VisualElement ClbFoundTeam(SportType sport)
        {
            bool can = _sim.Sports.CanCreateTeam(sport, out string why);
            var row = Ink("clb-found");
            row.EnableInClassList("clb-found--off", !can);
            row.Add(ClbFlagWithIcon(sport, "clb-found-flag"));

            var text = Box("clb-found-text");
            var name = Text("", "font-hand", "clb-found-name");
            SetHandText(name, new SportsTeam { Sport = sport }.Name);
            text.Add(name);
            var venue = _sim.Sports.HomeVenue(sport);
            var home = Text(venue != null ? $"home: {venue.Name}" : $"needs a home: {ClbVenueNames(sport)}", "font-semi", "clb-found-line");
            home.EnableInClassList("clb-found-line--ok", venue != null);
            text.Add(home);
            int keen = ClbKeenPlayers(sport);
            text.Add(Text($"{keen} {(keen == 1 ? "student is" : "students are")} sporty enough · needs {SportsTeam.MinRoster(sport)} to play", "font-semi", "clb-found-line"));
            if (venue == null)
            {
                var build = ClbCheapestVenue(sport);
                var def = RoomCatalog.Get(build);
                text.Add(ClbLink("pencil", $"build a {def.Name} ({Money(def.Cost[0])})", () => ClbBuild(build)));
            }
            else if (!can) text.Add(Text(why, "font-semi", "clb-why"));
            row.Add(text);

            var found = ClbButton(null, "Found", () => RunCommand(() =>
            {
                var result = _sim.CreateTeam(sport);
                var team = S.Teams.Find(x => x.Sport == sport);
                if (team != null) _clbTeam = team.Id;
                return result;
            }));
            found.AddToClassList("clb-btn--primary");
            found.EnableInClassList("clb-btn--off", !can);
            row.Add(found);
            return row;
        }

        /// <summary>How many students a new team would take now: not on a team, sporty enough, up to a full roster.</summary>
        int ClbKeenPlayers(SportType sport)
        {
            int n = 0;
            foreach (var s in S.Students)
                if (s.TeamId < 0 && SportsSystem.SportScore(s, sport) >= ClbAthleteCut) n++;
            return Mathf.Min(n, SportsTeam.MaxRoster(sport));
        }

        static string ClbVenueNames(SportType sport)
        {
            var names = new List<string>();
            foreach (RoomType type in Enum.GetValues(typeof(RoomType)))
                if (RoomCatalog.SupportsSport(type, sport)) names.Add(RoomCatalog.Get(type).Name);
            return string.Join(" or ", names);
        }

        static RoomType ClbCheapestVenue(SportType sport)
        {
            var best = RoomType.Gym;
            float cost = float.MaxValue;
            foreach (RoomType type in Enum.GetValues(typeof(RoomType)))
            {
                if (!RoomCatalog.SupportsSport(type, sport) || RoomCatalog.Get(type).Cost[0] >= cost) continue;
                cost = RoomCatalog.Get(type).Cost[0];
                best = type;
            }
            return best;
        }

        static string ClbSportIcon(SportType sport) => sport switch
        {
            SportType.Basketball => "clb-basketball",
            SportType.Soccer => "clb-soccer",
            SportType.Swimming => "clb-swim",
            SportType.Track => "clb-track",
            SportType.Tennis => "clb-tennis",
            _ => "trophy",
        };

        static Color ClbSportColor(SportType sport) => sport switch
        {
            SportType.Basketball => InkIcons.Marker,
            SportType.Soccer => InkIcons.Sky,
            SportType.Swimming => InkIcons.Hex(0x9CC3EC),
            SportType.Track => InkIcons.Hex(0xFFF3C4),
            SportType.Tennis => InkIcons.Pale,
            _ => InkIcons.Paper,
        };
    }
}
