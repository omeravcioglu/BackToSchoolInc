using System;
using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    public sealed partial class DebugUI
    {
        static readonly string[] ProgramPages = { "Sports", "Clubs" };
        static readonly string[] IntensityNames = Enum.GetNames(typeof(TrainingIntensity));

        Vector2 _programsScroll, _rankScroll, _reportScroll, _logScroll;
        int _programPage;
        int _selectedTeam = -1;
        int _selectedReport = -1;
        bool _importantOnly;

        void DrawPrograms()
        {
            int page = GUILayout.Toolbar(_programPage, ProgramPages, GUILayout.Width(200));
            if (page != _programPage) Defer(() => _programPage = page);
            _programsScroll = GUILayout.BeginScrollView(_programsScroll);
            if (_programPage == 0) DrawSports();
            else DrawClubs();
            GUILayout.EndScrollView();
        }

        void DrawSports()
        {
            Section("Teams");
            if (!Sim.IsUnlocked(Feature.SportsPrograms))
                GUILayout.Label($"Competitive teams unlock at school level {Balance.UnlockLevel(Feature.SportsPrograms)}. Sports facilities still make athletic students happier.", _wrap);

            foreach (var t in S.Teams)
            {
                var team = t;
                GUILayout.BeginVertical(_panel);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button($"<b>{t.Name}</b>  {t.Wins}W-{t.Losses}L · rating {t.Rating:0} · championships {t.Championships}", _rowButton, GUILayout.Width(460)))
                    Defer(() => _selectedTeam = _selectedTeam == team.Id ? -1 : team.Id);
                int intensity = GUILayout.Toolbar((int)t.Intensity, IntensityNames, GUILayout.Width(240));
                if (intensity != (int)t.Intensity) Run(() => Sim.SetIntensity(team.Id, (TrainingIntensity)intensity));
                GUILayout.EndHorizontal();

                var coach = S.FindStaff(t.CoachId);
                GUILayout.BeginHorizontal();
                GUILayout.Label(coach != null ? $"Coach: {coach.Name} (skill {coach.Skill:0})" : "No coach! Hire one (Staff → Hire) and assign them here.", _small, GUILayout.Width(320));
                foreach (var c in S.Staff)
                {
                    if (c.Role != StaffRole.Coach || c.Id == t.CoachId) continue;
                    var cc = c;
                    if (GUILayout.Button($"Assign {c.Name} ({c.Sport})", GUILayout.Width(200))) Run(() => Sim.SetCoach(team.Id, cc.Id));
                }
                GUILayout.EndHorizontal();

                if (_selectedTeam == t.Id)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical(GUILayout.Width(360));
                    GUILayout.Label("Rating", _bold);
                    BreakdownView(t.RatingFactors);
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();
                    GUILayout.Label($"Roster ({t.Roster.Count}/{SportsTeam.MaxRoster(t.Sport)}, needs {SportsTeam.MinRoster(t.Sport)})", _bold);
                    foreach (int id in t.Roster)
                    {
                        var s = S.FindStudent(id);
                        if (s == null) continue;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"{s.FullName} · {SportsSystem.SportScore(s, t.Sport):0}{(s.IsInjured(S.Day) ? " (injured)" : "")}", _small, GUILayout.Width(240));
                        var st = s;
                        if (GUILayout.Button("Drop", GUILayout.Width(50))) Defer(() => Sim.Sports.RemoveFromRoster(team, st));
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Auto-pick best roster", GUILayout.Width(170))) Run(() => Sim.AutoRoster(team.Id));
                    if (ConfirmButton("Disband team", "disband" + t.Id, GUILayout.Width(120))) Run(() => Sim.DisbandTeam(team.Id));
                    GUILayout.EndHorizontal();
                    GUILayout.Label("Recent results", _bold);
                    for (int i = t.Results.Count - 1; i >= 0 && i >= t.Results.Count - 8; i--) GUILayout.Label(t.Results[i], _small);
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }

            Section("Found a team");
            foreach (SportType sport in Enum.GetValues(typeof(SportType)))
            {
                if (sport == SportType.None) continue;
                var sp = sport;
                Sim.Sports.CanCreateTeam(sport, out string why);
                if (why == "Team already exists") continue;
                GUILayout.BeginHorizontal();
                if (ActionButton($"Found {sport} team", why, GUILayout.Width(200))) Run(() => Sim.CreateTeam(sp));
                if (why != null) GUILayout.Label(why, _small);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Teams cost $600/mo in travel and equipment. Wins raise sports reputation, fill the stands and attract sponsors.", _small);
        }

        void DrawClubs()
        {
            Section("Clubs");
            if (!Sim.IsUnlocked(Feature.Clubs))
                GUILayout.Label($"Clubs unlock at school level {Balance.UnlockLevel(Feature.Clubs)}.", _wrap);
            GUILayout.Label($"Club Room slots: {Sim.Clubs.UsedSlots()}/{Sim.Clubs.ClubSlots()} (the Gaming club uses the Gaming Room instead).", _small);

            foreach (var c in S.Clubs)
            {
                var club = c;
                GUILayout.BeginVertical(_panel);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{c.Name}</b> · {c.Members.Count}/{Club.MaxMembers} members · strength {c.Strength:0} · wins {c.Wins}", _wrap, GUILayout.Width(460));
                if (ConfirmButton("Disband", "club" + c.Id, GUILayout.Width(90))) Run(() => Sim.DisbandClub(club.Id));
                GUILayout.EndHorizontal();
                var names = new List<string>();
                foreach (int id in c.Members) { var s = S.FindStudent(id); if (s != null) names.Add(s.FullName); }
                GUILayout.Label(string.Join(", ", names), _small);
                GUILayout.EndVertical();
            }

            Section("Found a club");
            foreach (ClubType type in Enum.GetValues(typeof(ClubType)))
            {
                var t = type;
                Sim.Clubs.CanCreate(type, out string why);
                if (why == "Club already exists") continue;
                GUILayout.BeginHorizontal();
                if (ActionButton($"Found {type} ({Money(ClubSystem.FoundingCost)})", why, GUILayout.Width(240))) Run(() => Sim.CreateClub(t));
                if (why != null) GUILayout.Label(why, _small);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Clubs cost $150/mo. Members are happier, grow their skills, and can win competitions for academic or student-life reputation.", _small);
        }

        // ================================================================================

        void DrawRankings()
        {
            GUILayout.Label("National rankings", _h2);
            GUILayout.Label("Score = prestige plus a small bonus for size.", _small);
            _rankScroll = GUILayout.BeginScrollView(_rankScroll);
            var list = Sim.Rivals.Rankings();
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                GUI.color = e.IsPlayer ? Good : Color.white;
                GUILayout.BeginHorizontal(_panel);
                GUILayout.Label($"#{i + 1}", _bold, GUILayout.Width(40));
                GUILayout.Label(e.Name, _bold, GUILayout.Width(260));
                GUILayout.Label($"prestige {e.Prestige:0}", GUILayout.Width(110));
                GUILayout.Label($"{e.Students} students", GUILayout.Width(110));
                GUILayout.Label($"score {e.Score:0.0}", GUILayout.Width(100));
                if (!e.IsPlayer)
                {
                    var rival = S.Rivals.Find(r => r.Name == e.Name);
                    if (rival != null) GUILayout.Label($"{Names.Specialization(rival.Archetype)} · tuition {Money(rival.Tuition)} · trend {MathUtil.Signed(rival.Trend, "0.00")}", _small);
                }
                GUILayout.EndHorizontal();
                GUI.color = Color.white;
            }

            if (S.Alumni.Count > 0)
            {
                Section($"Alumni ({S.Alumni.Count})");
                for (int i = S.Alumni.Count - 1; i >= 0 && i >= S.Alumni.Count - 30; i--)
                {
                    var a = S.Alumni[i];
                    GUILayout.Label($"Class of Y{a.GraduationYear}: {a.Name}. {a.Achievement}.", _small);
                }
            }
            GUILayout.EndScrollView();
        }

        void DrawReports()
        {
            GUILayout.Label("Annual reports", _h2);
            if (S.Reports.Count == 0)
            {
                GUILayout.Label("Your first annual report arrives at the end of the school year (July).", _small);
                return;
            }
            GUILayout.BeginHorizontal();
            for (int i = 0; i < S.Reports.Count; i++)
            {
                int idx = i;
                GUI.color = _selectedReport == i ? Good : Color.white;
                if (GUILayout.Button($"Year {S.Reports[i].Year}", GUILayout.Width(80))) Defer(() => _selectedReport = idx);
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            int sel = _selectedReport >= 0 && _selectedReport < S.Reports.Count ? _selectedReport : S.Reports.Count - 1;
            _reportScroll = GUILayout.BeginScrollView(_reportScroll);
            GUILayout.Label(YearCycleSystem.Describe(S.Reports[sel]), _mono);
            GUILayout.EndScrollView();
        }

        void DrawLog()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("School log", _h2);
            GUILayout.FlexibleSpace();
            bool imp = GUILayout.Toggle(_importantOnly, "Important only");
            if (imp != _importantOnly) Defer(() => _importantOnly = imp);
            GUILayout.EndHorizontal();
            _logScroll = GUILayout.BeginScrollView(_logScroll);
            int shown = 0;
            for (int i = S.Log.Count - 1; i >= 0 && shown < 200; i--)
            {
                var e = S.Log[i];
                if (_importantOnly && !e.Important) continue;
                shown++;
                GUI.color = e.Important ? Warn : Color.white;
                GUILayout.Label($"{Calendar.Format(e.Day)}  {e.Text}", _wrap);
            }
            GUI.color = Color.white;
            GUILayout.EndScrollView();
        }
    }
}
