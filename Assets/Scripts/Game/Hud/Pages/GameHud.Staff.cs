using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The Staff room: "Team" lists everyone on the payroll, "Hire" the job market, both grouped by
    /// role. The selected person's file (GameHud.Staff.File.cs) handles pay, training and letting
    /// go, or a candidate's offer and contract length.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>A roster card for a staff member or a candidate; kept and reordered, not rebuilt.</summary>
        sealed class StfCard
        {
            public StaffMember Member;
            public Candidate Candidate;
            public InkBox Root;
            public PersonPortrait Face;
            public Label Name, Pay, Title, Note, Badge;
            public VisualElement Morale, Energy;
            public readonly InkIcon[] Stars = new InkIcon[5];
            public int Stamp;
        }

        /// <summary>A role's divider in a list: "teachers · 3".</summary>
        sealed class StfGroup
        {
            public VisualElement Root;
            public Label Label;
            public int Stamp;
        }

        // Kept while the page is closed.
        int _stfTab, _stfSelected = -1, _stfCandidate = -1;

        // Rebuilt with the page.
        readonly Dictionary<int, StfCard> _stfTeamCards = new Dictionary<int, StfCard>();
        readonly Dictionary<int, StfCard> _stfHireCards = new Dictionary<int, StfCard>();
        readonly Dictionary<StaffRole, StfGroup> _stfTeamGroups = new Dictionary<StaffRole, StfGroup>();
        readonly Dictionary<StaffRole, StfGroup> _stfHireGroups = new Dictionary<StaffRole, StfGroup>();
        readonly List<StaffMember> _stfTeamSorted = new List<StaffMember>();
        readonly List<Candidate> _stfHireSorted = new List<Candidate>();
        readonly List<VisualElement> _stfOrder = new List<VisualElement>();
        readonly List<int> _stfGone = new List<int>();
        readonly List<StaffRole> _stfGoneRoles = new List<StaffRole>();
        readonly int[] _stfRoleCount = new int[Enum.GetValues(typeof(StaffRole)).Length];
        ScrollView _stfTeamList, _stfHireList;
        InkBox _stfHireMore;
        Label _stfTeamTab, _stfHireTab, _stfPayroll, _stfAway, _stfLeaving, _stfListEmpty, _stfHireMoreNote;
        int _stfStamp;
        bool _stfScrollPending;

        static void StfDefineIcons()
        {
            InkIcons.Define("stf-bolt", 1.9f, "fill:#FFE066 M13.5 2.5L5 13.5h6l-1.5 8L19 10.5h-6z");
            InkIcons.Define("stf-star-off", 1.8f, "fill:#FBFDFF M12 3.2l2.7 5.4 5.9.9-4.3 4.1 1 5.9L12 16.7l-5.3 2.8 1-5.9-4.3-4.1 5.9-.9z");
            InkIcons.Define("stf-calendar", 2.1f, "fill:#DCEBFB rect:4,5.5,16,14,2", "M4 10h16M8.5 3.5v4M15.5 3.5v4");
            InkIcons.Define("stf-cap", 1.9f, "fill:#FFE066 M2.5 9.5L12 5l9.5 4.5L12 14z",
                "M6.5 11.6v4c1.5 1.4 3.5 2.1 5.5 2.1s4-.7 5.5-2.1v-4", "M21.5 9.5v5");
        }

        partial void BuildStaffPage()
        {
            StfDefineIcons();
            _stfTeamCards.Clear();
            _stfHireCards.Clear();
            _stfTeamGroups.Clear();
            _stfHireGroups.Clear();
            _stfScrollPending = true;

            // The roster column: Team / Hire tabs, the payroll, then the list.
            var side = Box("stf-side");
            var tabs = Box("stf-tabs");
            _stfTeamTab = StfTab(tabs, 0);
            _stfHireTab = StfTab(tabs, 1);
            side.Add(tabs);
            var chips = Box("stf-chips");
            var payroll = Box("stf-payroll");
            payroll.Add(Icon("coin", "icon-18"));
            _stfPayroll = Text("", "font-heavy", "stf-payroll-text");
            payroll.Add(_stfPayroll);
            chips.Add(payroll);
            _stfAway = Text("", "font-heavy", "chip", "stf-head-chip");
            chips.Add(_stfAway);
            _stfLeaving = Text("", "font-heavy", "chip", "chip--bad", "stf-head-chip");
            chips.Add(_stfLeaving);
            side.Add(chips);

            _stfTeamList = new ScrollView(ScrollViewMode.Vertical);
            _stfTeamList.AddToClassList("stf-list");
            side.Add(_stfTeamList);
            _stfHireList = new ScrollView(ScrollViewMode.Vertical);
            _stfHireList.AddToClassList("stf-list");
            side.Add(_stfHireList);
            _stfListEmpty = Text("", "font-note", "empty-note");
            side.Add(_stfListEmpty);

            // The end of the team list points at the job market, like the design's dashed card.
            _stfHireMore = Ink("stf-more");
            _stfHireMore.Add(Icon("plus", "icon-18"));
            _stfHireMore.Add(Text("Hire someone", "font-heavy", "stf-more-label"));
            _stfHireMoreNote = Text("", "font-note", "stf-more-note");
            _stfHireMore.Add(_stfHireMoreNote);
            OnClick(_stfHireMore, () => StfSetTab(1));
            _pageHost.Add(side);

            StfBuildFile(_pageHost);
        }

        partial void RefreshStaffPage(bool newDay)
        {
            if (_stfTeamList == null || _sim == null) return;
            if (_staffFocus >= 0)
            {
                if (S.FindStaff(_staffFocus) != null)
                {
                    _stfTab = 0;
                    _stfSelected = _staffFocus;
                    _stfDraft = -1f;
                    _stfFireArm = -1;
                    _stfScrollPending = true;
                }
                _staffFocus = -1;
                newDay = true;
            }
            if (!newDay)
            {
                StfTick();
                return;
            }
            StfSync();
            if (_stfScrollPending)
            {
                _stfScrollPending = false;
                StfScrollToSelected();
            }
        }

        partial void ResetStaffPage()
        {
            _stfTab = 0;
            _stfSelected = -1;
            _stfCandidate = -1;
            _stfDraft = -1f;
            _stfFireArm = -1;
            _stfShown = null;
            _stfTeamCards.Clear();
            _stfHireCards.Clear();
            _stfTeamList = null;
        }

        Label StfTab(VisualElement head, int index)
        {
            var tab = Text("", "font-heavy", "stf-tab", index == 0 ? "stf-tab--a" : "stf-tab--b");
            OnClick(tab, () => StfSetTab(index));
            head.Add(tab);
            return tab;
        }

        void StfSetTab(int tab)
        {
            if (_stfTab == tab) return;
            _stfTab = tab;
            _stfFireArm = -1;
            StfSync();
            StfScrollToSelected();
        }

        /// <summary>Everything that can change between days: tabs, payroll, both lists and the file.</summary>
        void StfSync()
        {
            SetText(_stfTeamTab, $"Team · {S.Staff.Count}");
            SetText(_stfHireTab, $"Hire · {S.Candidates.Count}");
            _stfTeamTab.EnableInClassList("stf-tab--on", _stfTab == 0);
            _stfHireTab.EnableInClassList("stf-tab--on", _stfTab == 1);

            float payroll = 0f;
            int training = 0, leave = 0, leaving = 0;
            foreach (var m in S.Staff)
            {
                payroll += m.Salary;
                if (m.IsAway(S.Day))
                {
                    if (m.CurrentTraining.HasValue) training++;
                    else leave++;
                }
                if (m.LeavingDay >= 0) leaving++;
            }
            SetText(_stfPayroll, $"payroll {Money(payroll)}/mo");
            Show(_stfLeaving, leaving > 0);
            SetText(_stfLeaving, $"{leaving} leaving");
            Show(_stfAway, training + leave > 0);
            SetText(_stfAway, training > 0 && leave > 0 ? $"{training} training · {leave} on leave"
                : training > 0 ? $"{training} away training" : $"{leave} on leave");

            StfSyncTeam();
            StfSyncHire();
            bool team = _stfTab == 0;
            int count = team ? S.Staff.Count : S.Candidates.Count;
            Show(_stfTeamList, team && count > 0);
            Show(_stfHireList, !team && count > 0);
            Show(_stfListEmpty, count == 0);
            SetText(_stfListEmpty, team
                ? "Nobody works here yet. Hire someone from the job market."
                : "Nobody is looking for work right now. New candidates turn up at the start of each month.");
            SetText(_stfHireMoreNote, S.Candidates.Count == 1 ? "1 applying" : $"{S.Candidates.Count} applying");
            StfShowFile();
        }

        // --- Lists --------------------------------------------------------------------------------

        /// <summary>The debug list's order: by role, best first.</summary>
        static int StfCompare(StaffMember a, StaffMember b)
        {
            if (a.Role != b.Role) return a.Role.CompareTo(b.Role);
            int c = b.Skill.CompareTo(a.Skill);
            return c != 0 ? c : a.Id.CompareTo(b.Id);
        }

        void StfSyncTeam()
        {
            _stfTeamSorted.Clear();
            _stfTeamSorted.AddRange(S.Staff);
            _stfTeamSorted.Sort(StfCompare);
            Array.Clear(_stfRoleCount, 0, _stfRoleCount.Length);
            foreach (var m in _stfTeamSorted) _stfRoleCount[(int)m.Role]++;

            int stamp = ++_stfStamp;
            _stfOrder.Clear();
            StaffRole? role = null;
            foreach (var m in _stfTeamSorted)
            {
                if (role != m.Role)
                {
                    role = m.Role;
                    _stfOrder.Add(StfGroupFor(_stfTeamGroups, m.Role, stamp).Root);
                }
                if (!_stfTeamCards.TryGetValue(m.Id, out var card) || card.Member != m)
                {
                    card = StfMakeCard(m, null);
                    _stfTeamCards[m.Id] = card;
                }
                card.Stamp = stamp;
                StfFillTeamCard(card);
                _stfOrder.Add(card.Root);
            }
            _stfOrder.Add(_stfHireMore);
            StfForget(_stfTeamCards, _stfTeamGroups, stamp);
            StfArrange(_stfTeamList.contentContainer, _stfOrder);
        }

        void StfSyncHire()
        {
            _stfHireSorted.Clear();
            _stfHireSorted.AddRange(S.Candidates);
            _stfHireSorted.Sort((a, b) => StfCompare(a.Staff, b.Staff));
            Array.Clear(_stfRoleCount, 0, _stfRoleCount.Length);
            foreach (var c in _stfHireSorted) _stfRoleCount[(int)c.Staff.Role]++;

            int stamp = ++_stfStamp;
            _stfOrder.Clear();
            StaffRole? role = null;
            foreach (var c in _stfHireSorted)
            {
                if (role != c.Staff.Role)
                {
                    role = c.Staff.Role;
                    _stfOrder.Add(StfGroupFor(_stfHireGroups, c.Staff.Role, stamp).Root);
                }
                if (!_stfHireCards.TryGetValue(c.Staff.Id, out var card) || card.Candidate != c)
                {
                    card = StfMakeCard(c.Staff, c);
                    _stfHireCards[c.Staff.Id] = card;
                }
                card.Stamp = stamp;
                StfFillHireCard(card);
                _stfOrder.Add(card.Root);
            }
            StfForget(_stfHireCards, _stfHireGroups, stamp);
            StfArrange(_stfHireList.contentContainer, _stfOrder);
        }

        StfGroup StfGroupFor(Dictionary<StaffRole, StfGroup> groups, StaffRole role, int stamp)
        {
            if (!groups.TryGetValue(role, out var g))
            {
                g = new StfGroup { Root = Box("stf-group") };
                g.Label = Text("", "font-note", "stf-group-label");
                g.Root.Add(g.Label);
                var line = new DashLine();
                line.AddToClassList("stf-group-line");
                g.Root.Add(line);
                groups[role] = g;
            }
            g.Stamp = stamp;
            int n = _stfRoleCount[(int)role];
            SetText(g.Label, $"{StfRoleWord(role, n)} · {n}");
            return g;
        }

        static string StfRoleWord(StaffRole role, int n) => role switch
        {
            StaffRole.Teacher => n == 1 ? "teacher" : "teachers",
            StaffRole.Coach => n == 1 ? "coach" : "coaches",
            StaffRole.Custodian => n == 1 ? "custodian" : "custodians",
            StaffRole.Maintenance => "maintenance",
            StaffRole.Chef => n == 1 ? "chef" : "chefs",
            StaffRole.Nurse => n == 1 ? "nurse" : "nurses",
            StaffRole.Security => "security",
            StaffRole.Counselor => n == 1 ? "counselor" : "counselors",
            StaffRole.Administrator => n == 1 ? "director" : "directors",
            _ => role.ToString().ToLowerInvariant(),
        };

        void StfForget(Dictionary<int, StfCard> cards, Dictionary<StaffRole, StfGroup> groups, int stamp)
        {
            _stfGone.Clear();
            foreach (var kv in cards)
                if (kv.Value.Stamp != stamp) _stfGone.Add(kv.Key);
            foreach (int id in _stfGone) cards.Remove(id);
            _stfGoneRoles.Clear();
            foreach (var kv in groups)
                if (kv.Value.Stamp != stamp) _stfGoneRoles.Add(kv.Key);
            foreach (var role in _stfGoneRoles) groups.Remove(role);
        }

        /// <summary>Puts exactly these elements into the container in this order, moving as few as possible.</summary>
        static void StfArrange(VisualElement container, List<VisualElement> order)
        {
            for (int i = 0; i < order.Count; i++)
                if (i >= container.childCount || container[i] != order[i]) container.Insert(i, order[i]);
            while (container.childCount > order.Count) container.RemoveAt(container.childCount - 1);
        }

        StfCard StfMakeCard(StaffMember m, Candidate c)
        {
            var card = new StfCard { Member = m, Candidate = c, Root = Ink("stf-card-row") };
            var frame = Box("stf-face");
            card.Face = new PersonPortrait();
            card.Face.AddToClassList("stf-face-img");
            frame.Add(card.Face);
            card.Root.Add(frame);

            var text = Box("stf-card-text");
            var top = Box("stf-card-top");
            card.Name = Text("", "font-heavy", "stf-card-name");
            card.Pay = Text("", "font-hand", "stf-card-pay");
            top.Add(card.Name);
            top.Add(card.Pay);
            text.Add(top);
            card.Title = Text("", "font-semi", "stf-row-title");
            text.Add(card.Title);
            var bottom = Box("stf-card-bottom");
            if (c == null)
            {
                card.Morale = StfMiniBar(bottom, "smile");
                card.Energy = StfMiniBar(bottom, "stf-bolt");
            }
            else
            {
                var stars = Box("stf-stars", "stf-stars--small");
                for (int i = 0; i < card.Stars.Length; i++)
                {
                    card.Stars[i] = Icon("star", "stf-star");
                    stars.Add(card.Stars[i]);
                }
                bottom.Add(stars);
                card.Note = Text("", "font-note", "stf-row-note");
                bottom.Add(card.Note);
            }
            text.Add(bottom);
            card.Root.Add(text);

            card.Badge = Text("", "font-heavy", "stf-badge");
            if (c != null)
            {
                card.Badge.text = "out of reach";
                card.Badge.AddToClassList("stf-badge--lock");
            }
            card.Root.Add(card.Badge);
            int id = m.Id;
            if (c == null) OnClick(card.Root, () => StfSelect(id));
            else OnClick(card.Root, () => StfSelectCandidate(id));
            return card;
        }

        static VisualElement StfMiniBar(VisualElement parent, string icon)
        {
            var group = Box("stf-mini");
            group.Add(Icon(icon, "icon-13", "stf-mini-icon"));
            var bar = Box("bar", "stf-mini-bar");
            var fill = Box("bar-fill");
            bar.Add(fill);
            group.Add(bar);
            parent.Add(group);
            return fill;
        }

        void StfFillTeamCard(StfCard card)
        {
            var m = card.Member;
            card.Face.ShowStaff(m);
            SetText(card.Name, m.Name);
            SetText(card.Pay, Money(m.Salary));
            bool away = m.IsAway(S.Day);
            string state = m.LeavingDay >= 0 ? "leaving" : away ? (m.CurrentTraining.HasValue ? "training" : "on leave") : null;
            SetText(card.Title, $"{m.Title} · skill {Mathf.RoundToInt(m.Skill)}");
            SetPercent(card.Morale, m.Morale);
            card.Morale.EnableInClassList("bar-fill--low", m.Morale < 35f);
            SetPercent(card.Energy, m.Energy);
            card.Energy.EnableInClassList("bar-fill--low", m.Energy < 25f);
            Show(card.Badge, state != null);
            SetText(card.Badge, m.LeavingDay >= 0 ? "!" : state);
            card.Badge.EnableInClassList("stf-badge--alert", m.LeavingDay >= 0);
            card.Badge.EnableInClassList("stf-badge--away", m.LeavingDay < 0 && away);
            card.Name.EnableInClassList("stf-bad", m.Morale < 35f || m.LeavingDay >= 0);
            card.Root.EnableInClassList("stf-card-row--on", _stfTab == 0 && m.Id == _stfSelected);
        }

        void StfFillHireCard(StfCard card)
        {
            var c = card.Candidate;
            var m = c.Staff;
            card.Face.ShowStaff(m);
            SetText(card.Name, m.Name);
            SetText(card.Pay, Money(c.AskingSalary));
            SetText(card.Title, m.Title);
            StfShowStars(card.Stars, m.Skill);
            int left = c.ExpiresDay - S.Day;
            SetText(card.Note, left <= 1 ? "last day looking" : $"looking {left} more days");
            card.Note.EnableInClassList("stf-bad", left <= 5);
            bool can = _sim.Staff.CanHire(c, out _);
            Show(card.Badge, !can);
            card.Root.EnableInClassList("stf-card-row--on", _stfTab == 1 && m.Id == _stfCandidate);
            card.Root.EnableInClassList("stf-card-row--dim", !can);
        }

        /// <summary>Five stars, one per 20 points of skill.</summary>
        static void StfShowStars(InkIcon[] stars, float skill)
        {
            int on = Mathf.Clamp(Mathf.RoundToInt(skill / 20f), 0, stars.Length);
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].Icon = i < on ? "star" : "stf-star-off";
                stars[i].EnableInClassList("stf-star--off", i >= on);
            }
        }

        void StfSelect(int id)
        {
            if (S.FindStaff(id) == null) return;
            if (_stfSelected != id) _stfDraft = -1f;
            _stfSelected = id;
            _stfFireArm = -1;
            StfMarkCards();
            StfShowFile();
        }

        void StfSelectCandidate(int id)
        {
            _stfCandidate = id;
            StfMarkCards();
            StfShowFile();
        }

        void StfMarkCards()
        {
            foreach (var card in _stfTeamCards.Values)
                card.Root.EnableInClassList("stf-card-row--on", _stfTab == 0 && card.Member.Id == _stfSelected);
            foreach (var card in _stfHireCards.Values)
                card.Root.EnableInClassList("stf-card-row--on", _stfTab == 1 && card.Member.Id == _stfCandidate);
        }

        void StfScrollToSelected()
        {
            var list = _stfTab == 0 ? _stfTeamList : _stfHireList;
            var cards = _stfTab == 0 ? _stfTeamCards : _stfHireCards;
            if (list == null || !cards.TryGetValue(_stfTab == 0 ? _stfSelected : _stfCandidate, out var card)) return;
            var target = card.Root;
            list.schedule.Execute(() =>
            {
                if (target.panel != null && !float.IsNaN(target.layout.height)) list.ScrollTo(target);
            }).StartingIn(60);
        }
    }
}
