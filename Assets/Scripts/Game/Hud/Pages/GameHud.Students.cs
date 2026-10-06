using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The Students page: everyone at the school in a searchable, sortable list on the left and
    /// the selected student's file as a notebook spread on the right (GameHud.Students.File.cs).
    /// Swapping rooms picks the second student from this list.
    /// </summary>
    public sealed partial class GameHud
    {
        static readonly string[] StuSorts = { "Name", "Grade", "Happy", "Grades", "Stress", "Family" };

        /// <summary>One student in the list. Rows live as long as the page and are reordered, not rebuilt.</summary>
        sealed class StuRow
        {
            public Student Student;
            public VisualElement Root, Mood;
            public PersonPortrait Face;
            public Label Name, Sub;
            public InkIcon Abroad, Flag;
            public int Stamp;
        }

        // Kept while the page is closed.
        int _stuSelected = -1, _stuSort, _stuSwapFrom = -1;
        string _stuFilter = "";

        // Rebuilt with the page.
        readonly Dictionary<int, StuRow> _stuRows = new Dictionary<int, StuRow>();
        readonly List<Student> _stuSorted = new List<Student>();
        readonly List<VisualElement> _stuOrder = new List<VisualElement>();
        readonly List<int> _stuGone = new List<int>();
        ScrollView _stuList;
        TextField _stuSearch;
        InkBox _stuSwapNote;
        Label _stuCount, _stuSwapText, _stuListEmpty;
        int _stuStamp;
        bool _stuScrollPending;

        partial void BuildStudentsPage()
        {
            StuDefineIcons();
            _stuRows.Clear();
            _stuScrollPending = true;

            var side = Panel("stu-side");
            var head = Box("stu-side-head");
            head.Add(Text("Students", "font-hand", "stu-side-title"));
            _stuCount = Text("", "font-note", "stu-count");
            head.Add(_stuCount);
            side.Add(head);

            var find = Box("stu-find");
            find.Add(Icon("stu-search", "icon-20", "stu-find-icon"));
            _stuSearch = new TextField { maxLength = 24 };
            _stuSearch.AddToClassList("stu-search");
            _stuSearch.textEdition.placeholder = "find a student";
            _stuSearch.SetValueWithoutNotify(_stuFilter);
            _stuSearch.RegisterValueChangedCallback(e =>
            {
                _stuFilter = e.newValue ?? "";
                StuSyncList();
            });
            find.Add(_stuSearch);
            var clear = Ink("stu-clear");
            clear.Add(Icon("close", "icon-13"));
            OnClick(clear, () => StuClearFilter(true));
            find.Add(clear);
            side.Add(find);

            var sorts = Pills(StuSorts, () => _stuSort, i =>
            {
                _stuSort = i;
                StuSyncList();
                StuScrollToSelected();
            });
            sorts.AddToClassList("stu-sorts");
            side.Add(sorts);

            _stuSwapNote = Ink("stu-swap-note");
            _stuSwapNote.Add(Icon("stu-swap", "icon-22"));
            _stuSwapText = Text("", "font-note", "stu-swap-text");
            _stuSwapNote.Add(_stuSwapText);
            var cancel = Ink("stu-mini-btn");
            cancel.Add(Text("Cancel", "font-heavy"));
            OnClick(cancel, () => StuSetSwap(-1));
            _stuSwapNote.Add(cancel);
            side.Add(_stuSwapNote);

            _stuList = new ScrollView(ScrollViewMode.Vertical);
            _stuList.AddToClassList("stu-list");
            side.Add(_stuList);
            _stuListEmpty = Text("", "font-note", "empty-note");
            side.Add(_stuListEmpty);
            _pageHost.Add(side);

            StuBuildFile();
        }

        partial void RefreshStudentsPage(bool newDay)
        {
            if (_stuList == null || _sim == null) return;
            if (_studentFocus >= 0)
            {
                StuFocus(_studentFocus);
                _studentFocus = -1;
                newDay = true;
            }
            if (!newDay)
            {
                StuTick();
                return;
            }
            if (S.FindStudent(_stuSwapFrom) == null) _stuSwapFrom = -1;
            StuSyncList();
            StuShowFile();
            if (_stuScrollPending)
            {
                _stuScrollPending = false;
                StuScrollToSelected();
            }
        }

        partial void ResetStudentsPage()
        {
            _stuSelected = -1;
            _stuSwapFrom = -1;
            _stuFilter = "";
            _stuPicking = false;
            _stuExpelArm = -1;
            _stuShownId = -1;
            _stuRows.Clear();
            _stuList = null;
        }

        /// <summary>Lands on a student another part of the HUD asked for, making sure the list shows them.</summary>
        void StuFocus(int id)
        {
            var s = S.FindStudent(id);
            if (s == null) return;
            _stuSelected = id;
            _stuSwapFrom = -1;
            _stuPicking = false;
            _stuExpelArm = -1;
            if (!StuMatches(s)) StuClearFilter(false);
            _stuScrollPending = true;
        }

        // --- List -----------------------------------------------------------------------------

        /// <summary>Brings the list up to date: rows for newcomers, none for leavers, the chosen order and the search.</summary>
        void StuSyncList()
        {
            if (_stuList == null) return;
            _stuSorted.Clear();
            _stuSorted.AddRange(S.Students);
            _stuSorted.Sort(StuCompare);

            int stamp = ++_stuStamp, shown = 0;
            var swapFrom = S.FindStudent(_stuSwapFrom);
            _stuOrder.Clear();
            foreach (var s in _stuSorted)
            {
                if (!_stuRows.TryGetValue(s.Id, out var row) || row.Student != s)
                {
                    row = StuMakeRow(s);
                    _stuRows[s.Id] = row;
                }
                row.Stamp = stamp;
                _stuOrder.Add(row.Root);
                bool match = StuMatches(s);
                Show(row.Root, match);
                if (!match) continue;
                shown++;
                StuFillRow(row, swapFrom);
            }

            _stuGone.Clear();
            foreach (var kv in _stuRows)
                if (kv.Value.Stamp != stamp) _stuGone.Add(kv.Key);
            foreach (int id in _stuGone) _stuRows.Remove(id);
            StuArrange(_stuList.contentContainer, _stuOrder);

            int count = S.Students.Count;
            SetText(_stuCount, shown == count ? $"· {count}" : $"· {shown} of {count}");
            Show(_stuList, shown > 0);
            Show(_stuListEmpty, shown == 0);
            if (shown == 0)
                SetText(_stuListEmpty, count == 0
                    ? "No students right now. The dorms are very quiet."
                    : $"Nobody here is called \"{_stuFilter.Trim()}\".");
            StuShowSwapNote(swapFrom);
        }

        /// <summary>Puts exactly these elements into the container in this order, moving as few as possible.</summary>
        static void StuArrange(VisualElement container, List<VisualElement> order)
        {
            for (int i = 0; i < order.Count; i++)
                if (i >= container.childCount || container[i] != order[i]) container.Insert(i, order[i]);
            while (container.childCount > order.Count) container.RemoveAt(container.childCount - 1);
        }

        /// <summary>The debug list's sorts, with names breaking ties so rows don't shuffle.</summary>
        int StuCompare(Student a, Student b)
        {
            int c = _stuSort switch
            {
                1 => b.Grade.CompareTo(a.Grade),
                2 => a.Happiness.CompareTo(b.Happiness),
                3 => b.Grades.CompareTo(a.Grades),
                4 => b.Stress.CompareTo(a.Stress),
                5 => a.ParentMood.CompareTo(b.ParentMood),
                _ => 0,
            };
            if (c == 0) c = string.CompareOrdinal(a.LastName, b.LastName);
            if (c == 0) c = string.CompareOrdinal(a.FirstName, b.FirstName);
            return c != 0 ? c : a.Id.CompareTo(b.Id);
        }

        bool StuMatches(Student s)
        {
            string filter = _stuFilter.Trim();
            return filter.Length == 0 || s.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void StuClearFilter(bool sync)
        {
            _stuFilter = "";
            _stuSearch?.SetValueWithoutNotify("");
            if (sync) StuSyncList();
        }

        StuRow StuMakeRow(Student s)
        {
            var row = new StuRow { Student = s, Root = Box("stu-row") };
            var frame = Box("stu-face");
            row.Face = new PersonPortrait();
            row.Face.AddToClassList("stu-face-img");
            frame.Add(row.Face);
            row.Root.Add(frame);
            var text = Box("stu-row-text");
            row.Name = Text("", "font-semi", "stu-row-name");
            row.Sub = Text("", "stu-row-sub");
            text.Add(row.Name);
            text.Add(row.Sub);
            row.Root.Add(text);
            row.Abroad = Icon("stu-globe", "stu-row-flag");
            row.Root.Add(row.Abroad);
            row.Flag = Icon("stu-bandage", "stu-row-flag");
            row.Root.Add(row.Flag);
            var bar = Box("bar", "stu-row-mood");
            row.Mood = Box("bar-fill");
            bar.Add(row.Mood);
            row.Root.Add(bar);
            OnClick(row.Root, () => StuClickRow(s));
            return row;
        }

        void StuFillRow(StuRow row, Student swapFrom)
        {
            var s = row.Student;
            var room = S.FindRoom(s.DormRoomId);
            SetText(row.Name, s.FullName);
            row.Name.EnableInClassList("stu-bad", s.Happiness < 35f);
            SetText(row.Sub, $"Grade {s.Grade} · {(room != null ? room.Name : "no bed")}");
            row.Sub.EnableInClassList("stu-bad", room == null);
            row.Face.ShowStudent(s);
            SetPercent(row.Mood, s.Happiness);
            row.Mood.EnableInClassList("bar-fill--low", s.Happiness < 35f);
            Show(row.Abroad, s.International);
            string flag = s.IsInjured(S.Day) ? "stu-bandage" : s.IsSick(S.Day) ? "stu-thermo" : null;
            Show(row.Flag, flag != null);
            if (flag != null) row.Flag.Icon = flag;
            StuMarkRow(row, swapFrom);
        }

        void StuMarkRow(StuRow row, Student swapFrom)
        {
            var s = row.Student;
            row.Root.EnableInClassList("stu-row--on", s.Id == _stuSelected);
            row.Root.EnableInClassList("stu-row--dim", swapFrom != null && s != swapFrom && !StuCanSwap(swapFrom, s));
        }

        /// <summary>Refreshes the selection and swap marks without touching anything else.</summary>
        void StuMarkRows()
        {
            var swapFrom = S.FindStudent(_stuSwapFrom);
            foreach (var row in _stuRows.Values) StuMarkRow(row, swapFrom);
        }

        /// <summary>The same test the swap command makes: both have beds, in different rooms.</summary>
        bool StuCanSwap(Student a, Student b)
        {
            var ra = S.FindRoom(a.DormRoomId);
            var rb = S.FindRoom(b.DormRoomId);
            return ra != null && rb != null && ra != rb;
        }

        void StuClickRow(Student s)
        {
            if (_stuSwapFrom >= 0 && _stuSwapFrom != s.Id)
            {
                int a = _stuSwapFrom, b = s.Id;
                _stuSwapFrom = -1;
                RunCommand(() => _sim.SwapStudents(a, b));
                return;
            }
            if (_stuSwapFrom == s.Id) _stuSwapFrom = -1;   // clicking the swapper again calls it off
            StuSelect(s.Id);
        }

        void StuSelect(int id)
        {
            if (S.FindStudent(id) == null) return;
            _stuSelected = id;
            _stuPicking = false;
            _stuExpelArm = -1;
            StuMarkRows();
            StuShowSwapNote(S.FindStudent(_stuSwapFrom));
            StuShowFile();
        }

        void StuScrollToSelected()
        {
            if (_stuList == null || !_stuRows.TryGetValue(_stuSelected, out var row)) return;
            var list = _stuList;
            var target = row.Root;
            // New rows have no layout yet: wait a frame or two before scrolling.
            list.schedule.Execute(() =>
            {
                if (target.panel != null && !float.IsNaN(target.layout.height)) list.ScrollTo(target);
            }).StartingIn(60);
        }

        // --- Swap mode ------------------------------------------------------------------------

        void StuSetSwap(int id)
        {
            _stuSwapFrom = id;
            var from = S.FindStudent(id);
            StuMarkRows();
            StuShowSwapNote(from);
            StuShowActions(S.FindStudent(_stuSelected));
        }

        void StuShowSwapNote(Student from)
        {
            Show(_stuSwapNote, from != null);
            if (from != null) SetText(_stuSwapText, $"Swap {from.FirstName}'s room: pick someone who sleeps in another room.");
        }
    }
}
