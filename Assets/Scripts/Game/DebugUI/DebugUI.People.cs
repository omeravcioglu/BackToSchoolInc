using System;
using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    public sealed partial class DebugUI
    {
        static readonly string[] StudentSorts = { "Name", "Grade", "Happy", "Grades", "Stress", "Family" };
        static readonly string[] StudentPages = { "Profile", "Relationships", "Happiness", "Family", "History" };
        static readonly string[] StaffPages = { "Staff", "Hire" };

        Vector2 _studentListScroll, _studentDetailScroll, _staffListScroll, _staffDetailScroll;
        int _selectedStudent = -1, _selectedStaff = -1;
        int _studentSort, _studentPage, _staffPage;
        string _studentFilter = "";
        int _swapFrom = -1;
        bool _showRoomPicker;
        string _salaryInput = "";

        // ================================================================================
        // Students
        // ================================================================================

        void DrawStudents()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(420));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Students ({S.Students.Count})", _h2);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Search", _small, GUILayout.Width(44));
            string filter = GUILayout.TextField(_studentFilter, GUILayout.Width(120));
            if (filter != _studentFilter) Defer(() => _studentFilter = filter);
            GUILayout.EndHorizontal();
            int sort = GUILayout.Toolbar(_studentSort, StudentSorts);
            if (sort != _studentSort) Defer(() => _studentSort = sort);
            if (_swapFrom >= 0)
            {
                var from = S.FindStudent(_swapFrom);
                Colored($"Swap mode: click a student in another room to swap with {from?.FirstName}.", Warn, _small);
                if (GUILayout.Button("Cancel swap")) Defer(() => _swapFrom = -1);
            }

            _studentListScroll = GUILayout.BeginScrollView(_studentListScroll);
            foreach (var s in SortedStudents())
            {
                var st = s;
                var room = S.FindRoom(s.DormRoomId);
                GUI.color = _selectedStudent == s.Id ? Good : s.Happiness < 35f ? Bad : Color.white;
                GUILayout.BeginHorizontal();
                string label = $"{s.FullName}  <size=10>G{s.Grade} · {room?.Name ?? "NO BED"}</size>";
                if (GUILayout.Button(label, _rowButton, GUILayout.Width(250)))
                {
                    if (_swapFrom >= 0 && _swapFrom != s.Id)
                    {
                        int a = _swapFrom;
                        Run(() => Sim.SwapStudents(a, st.Id));
                        Defer(() => _swapFrom = -1);
                    }
                    else Defer(() => { _selectedStudent = st.Id; _showRoomPicker = false; });
                }
                GUI.color = Color.white;
                Bar(s.Happiness, 90);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            var sel = S.FindStudent(_selectedStudent);
            if (sel == null) GUILayout.Label("Select a student.", _small);
            else DrawStudentDetail(sel);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        List<Student> SortedStudents()
        {
            var list = new List<Student>();
            foreach (var s in S.Students)
                if (_studentFilter.Length == 0 || s.FullName.IndexOf(_studentFilter, StringComparison.OrdinalIgnoreCase) >= 0) list.Add(s);
            switch (_studentSort)
            {
                case 0: list.Sort((a, b) => string.CompareOrdinal(a.LastName, b.LastName)); break;
                case 1: list.Sort((a, b) => b.Grade.CompareTo(a.Grade)); break;
                case 2: list.Sort((a, b) => a.Happiness.CompareTo(b.Happiness)); break;
                case 3: list.Sort((a, b) => b.Grades.CompareTo(a.Grades)); break;
                case 4: list.Sort((a, b) => b.Stress.CompareTo(a.Stress)); break;
                case 5: list.Sort((a, b) => a.ParentMood.CompareTo(b.ParentMood)); break;
            }
            return list;
        }

        void DrawStudentDetail(Student s)
        {
            var room = S.FindRoom(s.DormRoomId);
            GUILayout.Label(s.FullName, _h1);
            string schol = s.Scholarship != ScholarshipType.None ? $" · {s.Scholarship} scholarship" : s.Discount > 0 ? $" · {s.Discount:P0} aid" : "";
            GUILayout.Label($"Grade {s.Grade} · {room?.ToString() ?? "no bed"} · parents: {s.ParentName}{schol}", _small);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Move to...", GUILayout.Width(90))) Defer(() => _showRoomPicker = !_showRoomPicker);
            if (GUILayout.Button("Swap rooms with...", GUILayout.Width(130))) Defer(() => _swapFrom = s.Id);
            if (ConfirmButton("Expel", "expel" + s.Id, GUILayout.Width(80)))
            {
                int id = s.Id;
                Run(() => Sim.Expel(id));
            }
            GUILayout.EndHorizontal();

            if (_showRoomPicker) DrawRoomPicker(s);

            int page = GUILayout.Toolbar(_studentPage, StudentPages);
            if (page != _studentPage) Defer(() => _studentPage = page);

            _studentDetailScroll = GUILayout.BeginScrollView(_studentDetailScroll);
            switch (_studentPage)
            {
                case 0: StudentProfile(s); break;
                case 1: StudentRelationships(s); break;
                case 2:
                    GUILayout.Label($"Happiness {s.Happiness:0}, heading toward {s.HappinessTarget:0}.", _small);
                    BreakdownView(s.HappinessFactors);
                    break;
                case 3:
                    GUILayout.Label($"{s.ParentName}'s satisfaction: {s.ParentMood:0}. Family wealth {s.FamilyWealth:0}.", _small);
                    GUILayout.Label($"Chance they re-enroll next year: {Sim.Students.RetentionChance(s, S.Policies.AnnualTuition):P0}", _bold);
                    BreakdownView(s.ParentFactors);
                    break;
                case 4:
                    for (int i = s.History.Count - 1; i >= 0; i--) GUILayout.Label(s.History[i], _wrap);
                    break;
            }
            GUILayout.EndScrollView();
        }

        void StudentProfile(Student s)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(300));
            Section("Now");
            LabeledBar("Happiness", s.Happiness);
            LabeledBar("Stress", s.Stress, tooltip: "High stress lowers happiness and grades.");
            LabeledBar("Grades", s.Grades);
            LabeledBar("Behavior", s.Behavior, tooltip: "Recent conduct. Falls with incidents, recovers toward discipline.");
            LabeledBar("Health", s.Health);
            Section("Abilities");
            LabeledBar("Academic", s.Academic);
            LabeledBar("Athletic", s.Athletic);
            LabeledBar("Artistic", s.Artistic);
            LabeledBar("Social", s.Social);
            LabeledBar("Discipline", s.Discipline);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            Section("Traits");
            foreach (var t in s.Traits) GUILayout.Label(new GUIContent($"{t}: {TraitRules.Describe(t)}"), _wrap);
            if (s.SportTalent != SportType.None) GUILayout.Label($"Natural talent for {s.SportTalent}.", _wrap);
            Section("Activities");
            var team = S.FindTeam(s.TeamId);
            var club = S.FindClub(s.ClubId);
            GUILayout.Label(team != null ? $"Plays on the {team.Name}." : "Not on a team.", _wrap);
            GUILayout.Label(club != null ? $"Member of the {club.Name}." : "Not in a club.", _wrap);
            if (s.IsInjured(S.Day)) Colored($"Injured until {Calendar.Format(s.InjuredUntilDay)}.", Warn);
            if (s.IsSick(S.Day)) Colored("Sick.", Warn);
            GUILayout.Label($"Incidents on record: {s.Incidents}", _small);
            if (s.Moodlets.Count > 0)
            {
                Section("Recent events");
                foreach (var m in s.Moodlets) Colored($"{MathUtil.Signed(m.Value, "0")} {m.Label}", m.Value >= 0 ? Good : Bad, _small);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        void StudentRelationships(Student s)
        {
            var rels = new List<Relationship>(S.Relationships.For(s.Id));
            rels.Sort((a, b) => Mathf.Abs(b.Score).CompareTo(Mathf.Abs(a.Score)));
            if (rels.Count == 0) GUILayout.Label("No meaningful relationships yet.", _small);
            foreach (var r in rels)
            {
                if (Mathf.Abs(r.Score) < 10f && !r.Crush) continue;
                var other = S.FindStudent(r.Other(s.Id));
                if (other == null) continue;
                var tags = new List<string>();
                if (other.DormRoomId == s.DormRoomId && s.DormRoomId >= 0) tags.Add("Roommate");
                if (other.TeamId == s.TeamId && s.TeamId >= 0) tags.Add("Teammate");
                if (other.ClubId == s.ClubId && s.ClubId >= 0) tags.Add("Club");
                if (other.Grade == s.Grade) tags.Add("Classmate");
                GUILayout.BeginHorizontal();
                GUI.color = r.Score >= 30f ? Good : r.Score <= -25f ? Bad : Color.white;
                GUILayout.Label(RelationshipSystem.Describe(r.Type), _bold, GUILayout.Width(90));
                GUI.color = Color.white;
                var o = other;
                if (GUILayout.Button(other.FullName, _rowButton, GUILayout.Width(170))) Defer(() => _selectedStudent = o.Id);
                GUILayout.Label($"{r.Score:0}", GUILayout.Width(40));
                GUILayout.Label(string.Join(", ", tags), _small);
                GUILayout.EndHorizontal();
            }
        }

        void DrawRoomPicker(Student s)
        {
            GUILayout.BeginVertical(_panel);
            GUILayout.Label("Rooms with a free bed (compatibility with current residents):", _small);
            foreach (var r in S.Rooms)
            {
                if (r.Type != RoomType.DormRoom || !r.IsOperational(S.Day) || r.Id == s.DormRoomId) continue;
                var residents = Sim.Indices.ResidentsOf(r.Id);
                if (residents.Count >= r.Capacity) continue;
                float compat = 0f;
                foreach (var o in residents) compat += TraitRules.Compatibility(s, o, true);
                string names = residents.Count == 0 ? "empty" : string.Join(", ", residents.ConvertAll(o => o.FirstName));
                var room = r;
                GUI.color = compat < -1f ? Bad : compat > 1f ? Good : Color.white;
                if (GUILayout.Button($"{r.Name} L{r.Level} ({residents.Count}/{r.Capacity}) · compatibility {MathUtil.Signed(compat, "0.0")} · {names}", _rowButton))
                {
                    int id = s.Id;
                    Run(() => Sim.MoveStudent(id, room.Id));
                    Defer(() => _showRoomPicker = false);
                }
                GUI.color = Color.white;
            }
            GUILayout.EndVertical();
        }

        // ================================================================================
        // Staff
        // ================================================================================

        void DrawStaff()
        {
            int page = GUILayout.Toolbar(_staffPage, StaffPages, GUILayout.Width(240));
            if (page != _staffPage) Defer(() => _staffPage = page);
            if (_staffPage == 1)
            {
                DrawHiring();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(400));
            float payroll = 0f;
            foreach (var st in S.Staff) payroll += st.Salary;
            GUILayout.Label($"Staff ({S.Staff.Count}) · payroll {Money(payroll)}/mo", _h2);
            _staffListScroll = GUILayout.BeginScrollView(_staffListScroll);
            var list = new List<StaffMember>(S.Staff);
            list.Sort((a, b) => a.Role != b.Role ? a.Role.CompareTo(b.Role) : b.Skill.CompareTo(a.Skill));
            foreach (var st in list)
            {
                var member = st;
                string state = st.IsAway(S.Day) ? (st.CurrentTraining.HasValue ? " · training" : " · on leave") : st.LeavingDay >= 0 ? " · LEAVING" : "";
                GUI.color = _selectedStaff == st.Id ? Good : st.Morale < 35f || st.LeavingDay >= 0 ? Bad : Color.white;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button($"{st.Name}  <size=10>{st.Title} · skill {st.Skill:0}{state}</size>", _rowButton, GUILayout.Width(260)))
                    Defer(() => { _selectedStaff = member.Id; _salaryInput = member.Salary.ToString("0"); });
                GUI.color = Color.white;
                Bar(st.Morale, 70);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            _staffDetailScroll = GUILayout.BeginScrollView(_staffDetailScroll);
            var sel = S.FindStaff(_selectedStaff);
            if (sel == null) GUILayout.Label("Select a staff member.", _small);
            else DrawStaffDetail(sel);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        void DrawStaffDetail(StaffMember st)
        {
            GUILayout.Label(st.Name, _h1);
            float market = Sim.Staff.MarketValue(st);
            GUILayout.Label($"{st.Title} · {st.Experience:0.0} years' experience · hired {(st.HiredDay < 0 ? "before you arrived" : Calendar.Format(st.HiredDay))}", _small);
            if (st.LeavingDay >= 0) Colored($"Leaving on {Calendar.Format(st.LeavingDay)}.", Bad);
            if (st.IsAway(S.Day)) Colored($"Away until {Calendar.Format(st.AwayUntilDay)}{(st.CurrentTraining.HasValue ? $" ({st.CurrentTraining})" : " (leave)")}.", Warn);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(300));
            Section("Stats");
            LabeledBar(st.Role == StaffRole.Coach ? "Coaching" : "Skill", st.Skill);
            if (st.Role == StaffRole.Teacher)
            {
                LabeledBar("Classroom mgmt.", st.ClassroomManagement);
                LabeledBar("Patience", st.Patience);
                LabeledBar("Charisma", st.Charisma);
                GUILayout.Label($"Teaching effectiveness: {Sim.Stats.TeacherEffect(st):0}", _bold);
            }
            if (st.Role == StaffRole.Coach)
            {
                LabeledBar("Motivation", st.Charisma);
                LabeledBar("Strategy", st.Strategy);
                LabeledBar("Player development", st.PlayerDevelopment);
                LabeledBar("Recruitment", st.Recruitment);
            }
            LabeledBar("Morale", st.Morale);
            LabeledBar("Energy", st.Energy);
            LabeledBar("Loyalty", st.Loyalty);
            Section("Traits");
            foreach (var t in st.Traits) GUILayout.Label($"{t}: {TraitRules.Describe(t)}", _wrap);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            Section("Contract");
            KeyValue("Salary", $"{Money(st.Salary)}/mo (market {Money(market)})");
            KeyValue("Contract", $"{st.ContractYears} year(s), ends {Calendar.Format(st.ContractEndDay)}");
            KeyValue("Firing now costs", Money(Sim.Staff.Severance(st)));
            KeyValue("Last raise", st.LastRaiseDay < 0 ? "before you arrived" : Calendar.Format(st.LastRaiseDay));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+5%")) SetSalary(st, st.Salary * 1.05f);
            if (GUILayout.Button("+10%")) SetSalary(st, st.Salary * 1.10f);
            if (GUILayout.Button("Match market")) SetSalary(st, Mathf.Max(st.Salary, market));
            if (GUILayout.Button("-5%")) SetSalary(st, st.Salary * 0.95f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _salaryInput = GUILayout.TextField(_salaryInput, GUILayout.Width(90));
            if (GUILayout.Button("Set salary", GUILayout.Width(90)) && float.TryParse(_salaryInput, out float custom)) SetSalary(st, custom);
            GUILayout.EndHorizontal();

            Section("Training");
            foreach (TrainingProgram p in Enum.GetValues(typeof(TrainingProgram)))
            {
                var program = p;
                Sim.Staff.CanTrain(st, p, out string why);
                string effect = p == TrainingProgram.Workshop ? "+skill, energy" : p == TrainingProgram.Seminar ? "+classroom mgmt, morale" : "big +skill; raises their market value";
                if (ActionButton($"{p} ({Money(Balance.TrainingCost(p))}, {Balance.TrainingDays(p)} days): {effect}", why))
                {
                    int id = st.Id;
                    Run(() => Sim.Train(id, program));
                }
            }

            Section("Morale");
            BreakdownView(st.MoraleFactors);

            GUILayout.Space(8);
            if (ConfirmButton($"Fire (severance {Money(Sim.Staff.Severance(st))})", "fire" + st.Id))
            {
                int id = st.Id;
                Run(() => Sim.Fire(id));
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        void SetSalary(StaffMember st, float value)
        {
            int id = st.Id;
            Run(() => Sim.SetSalary(id, value));
            Defer(() => _salaryInput = S.FindStaff(id)?.Salary.ToString("0") ?? "");
        }

        void DrawHiring()
        {
            GUILayout.Label("Job market", _h2);
            GUILayout.Label("Better candidates want a more prestigious school. Longer contracts are cheaper per month and build loyalty, but cost more to break.", _small);
            _staffListScroll = GUILayout.BeginScrollView(_staffListScroll);
            var list = new List<Candidate>(S.Candidates);
            list.Sort((a, b) => a.Staff.Role != b.Staff.Role ? a.Staff.Role.CompareTo(b.Staff.Role) : b.Staff.Skill.CompareTo(a.Staff.Skill));
            foreach (var c in list)
            {
                var cand = c;
                var st = c.Staff;
                Sim.Staff.CanHire(c, out string why);
                GUILayout.BeginVertical(_panel);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{st.Name}</b> · {st.Title}", _wrap, GUILayout.Width(300));
                GUILayout.Label($"skill {st.Skill:0} · exp {st.Experience:0.0}y · asks {Money(c.AskingSalary)}/mo · leaves market {Calendar.Format(c.ExpiresDay)}", _small);
                GUILayout.EndHorizontal();
                GUILayout.Label($"Traits: {string.Join(", ", st.Traits)}" + (st.Role == StaffRole.Teacher ? $" · teaching effectiveness ~{Sim.Stats.TeacherEffect(st):0}" : ""), _small);
                if (why != null) Colored(why, Bad, _small);
                GUILayout.BeginHorizontal();
                foreach (int years in new[] { 1, 3, 5 })
                {
                    int y = years;
                    float salary = c.AskingSalary * StaffSystem.ContractSalaryFactor(years);
                    if (ActionButton($"Hire {years}y ({Money(salary)}/mo)", why, GUILayout.Width(170))) Run(() => Sim.Hire(cand, y));
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }
    }
}
