using System;
using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    public sealed partial class DebugUI
    {
        Vector2 _overviewScroll, _overviewRightScroll;
        int _breakdownStat = (int)StatId.ParentSatisfaction;
        bool _breakdownIsReputation;

        static readonly string[] StatLabels =
        {
            "Education Quality", "Student Satisfaction", "Parent Satisfaction", "Dormitory Quality", "Staff Morale",
            "Safety & Discipline", "Food Quality", "Cleanliness", "Student Life", "Trust",
        };

        void DrawOverview()
        {
            GUILayout.BeginHorizontal();

            // Left: stats and reputation
            GUILayout.BeginVertical(GUILayout.Width(430));
            _overviewScroll = GUILayout.BeginScrollView(_overviewScroll);
            Section("School stats");
            GUILayout.Label("Click a stat to see exactly what is driving it.", _small);
            for (int i = 0; i < GameState.StatCount; i++)
            {
                int idx = i;
                GUILayout.BeginHorizontal();
                bool selected = !_breakdownIsReputation && _breakdownStat == i;
                GUI.color = selected ? Good : Color.white;
                if (GUILayout.Button(StatLabels[i], _rowButton, GUILayout.Width(170))) Defer(() => { _breakdownStat = idx; _breakdownIsReputation = false; });
                GUI.color = Color.white;
                Bar(S.Stats[i], 180);
                GUILayout.EndHorizontal();
            }

            Section("Reputation");
            GUILayout.Label("Public reputation drifts slowly toward its target.", _small);
            for (int i = 0; i < GameState.ReputationCount; i++)
            {
                int idx = i;
                GUILayout.BeginHorizontal();
                bool selected = _breakdownIsReputation && _breakdownStat == i;
                GUI.color = selected ? Good : Color.white;
                string name = ((ReputationId)i) == ReputationId.Overall ? "Overall Prestige" : ((ReputationId)i) + " Rep.";
                if (GUILayout.Button(name, _rowButton, GUILayout.Width(170))) Defer(() => { _breakdownStat = idx; _breakdownIsReputation = true; });
                GUI.color = Color.white;
                Bar(S.Reputation[i], 180);
                float target = Sim.Stats.ReputationTargets[i];
                float diff = target - S.Reputation[i];
                GUI.color = Math.Abs(diff) < 0.5f ? Dim : diff > 0 ? Good : Bad;
                GUILayout.Label(Math.Abs(diff) < 0.5f ? "steady" : (diff > 0 ? "rising" : "falling"), _note, GUILayout.Width(50));
                GUI.color = Color.white;
                GUILayout.EndHorizontal();
            }

            Section("School");
            KeyValue("Specialization", S.Specialization == Specialization.None ? "None yet" : Names.Specialization(S.Specialization));
            KeyValue("Land", $"{S.Campus.OwnedParcelCount} parcels · {S.Campus.UsedTiles}/{S.Campus.OwnedTiles} tiles built on");
            KeyValue("Students per teacher", Sim.Indices.Teachers == 0 ? "no teachers!" : Sim.Indices.StudentsPerTeacher.ToString("0.0"));
            KeyValue("Fair tuition (families' view)", Money(Sim.FairTuition));
            KeyValue("Next inspection", S.InspectionAnnounced ? Calendar.Format(S.NextInspectionDay) + " (announced)" : "Unknown");
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Right: breakdown, alerts, promises
            GUILayout.BeginVertical();
            _overviewRightScroll = GUILayout.BeginScrollView(_overviewRightScroll);
            if (_breakdownIsReputation)
            {
                Section($"Why is {(ReputationId)_breakdownStat} reputation {S.Reputation[_breakdownStat]:0}?");
                GUILayout.Label($"Target {Sim.Stats.ReputationTargets[_breakdownStat]:0}. Current value moves toward it a little every day.", _small);
                BreakdownView(Sim.Stats.ReputationFactors[_breakdownStat]);
            }
            else
            {
                Section($"Why is {StatLabels[_breakdownStat]} {S.Stats[_breakdownStat]:0}?");
                BreakdownView(Sim.Stats.StatFactors[_breakdownStat]);
                if (_breakdownStat == (int)StatId.StudentSatisfaction || _breakdownStat == (int)StatId.ParentSatisfaction)
                    GUILayout.Label("This is an average of individuals. Open a student to see their personal factors.", _small);
            }

            Section("Needs attention");
            var alerts = Alerts();
            if (alerts.Count == 0) Colored("Nothing urgent. A calm day at school.", Good);
            foreach (var (text, color) in alerts) Colored("• " + text, color);

            Section("Promises");
            int shown = 0;
            for (int i = S.Promises.Count - 1; i >= 0 && shown < 12; i--, shown++)
            {
                var p = S.Promises[i];
                var color = p.Resolved ? (p.Kept ? Good : Bad) : Sim.Promises.IsMet(p) ? Good : Warn;
                Colored(Sim.Promises.Describe(p), color);
            }
            if (S.Promises.Count == 0) GUILayout.Label("You haven't promised anything yet.", _small);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        List<(string, Color)> Alerts()
        {
            var list = new List<(string, Color)>();
            var ix = Sim.Indices;
            int homeless = 0;
            foreach (var s in S.Students) if (s.DormRoomId < 0) homeless++;
            if (homeless > 0) list.Add(($"{homeless} student(s) have no bed.", Bad));
            if (S.Finance.Cash < 0) list.Add(($"The school is overdrawn ({Money(S.Finance.Cash)}). {Balance.InsolvencyGraceDays - S.NegativeCashDays} days before the board steps in.", Bad));
            var proj = Sim.Economy.Project();
            if (proj.Net < 0) list.Add(($"Projected monthly result: {Money(proj.Net)}.", Warn));
            if (ix.Teachers == 0) list.Add(("There are no active teachers!", Bad));
            else if (ix.StudentsPerTeacher > 14f) list.Add(($"Classes are too big ({ix.StudentsPerTeacher:0.0} students per teacher). Hire teachers.", Warn));
            if (ix.Students > ix.ClassroomSeats) list.Add(($"Classrooms are overcrowded ({ix.Students} students, {ix.ClassroomSeats} seats).", Bad));
            if (ix.MealCapacity > 0 && ix.Students > ix.MealCapacity) list.Add(($"Cafeteria too small ({ix.MealCapacity:0} per sitting): long lines.", Warn));
            if (ix.BathroomShortage > 0.01f) list.Add(($"Bathroom shortage: {ix.BathroomShortage:P0} of shared-bathroom residents are underserved.", Warn));
            if (ix.ChefCoverage < 1f) list.Add(($"Kitchen understaffed ({ix.ChefCoverage:P0} chef coverage).", Warn));
            if (ix.CustodianCoverage < 1f) list.Add(($"Not enough custodians ({ix.CustodianCoverage:P0} coverage).", Warn));
            if (ix.GuardCoverage < 1f && ix.Students > 0) list.Add(($"Security coverage {ix.GuardCoverage:P0}. Safety suffers.", ix.GuardCoverage == 0 ? Warn : Dim));
            if (ix.FreeBeds == 0) list.Add(("Every bed is full. Build or upgrade dorms to grow.", Dim));
            int worn = 0;
            foreach (var r in S.Rooms) if (r.IsBuilt(S.Day) && r.Condition < 40f) worn++;
            if (worn > 0) list.Add(($"{worn} room(s) are badly worn. Repair them or hire maintenance.", Warn));
            int exhausted = 0;
            foreach (var s in S.Staff) if (s.Energy < 20f) exhausted++;
            if (exhausted > 0) list.Add(($"{exhausted} staff member(s) are exhausted.", Warn));
            if (S.InspectionAnnounced) list.Add(($"Inspection on {Calendar.Format(S.NextInspectionDay)}.", Warn));
            if (S.Policies.PendingTuition > 0) list.Add(($"Tuition changes to {Money(S.Policies.PendingTuition)} next semester.", Dim));
            int decisions = Sim.Inbox.PendingDecisions;
            if (decisions > 0) list.Add(($"{decisions} message(s) waiting for your decision.", Warn));
            return list;
        }
    }
}
