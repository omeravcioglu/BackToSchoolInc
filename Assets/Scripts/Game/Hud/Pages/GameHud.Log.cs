using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The school log as a diary: a section per month with the dates in the margin, big moments in
    /// marker, a filter for the important ones and bookmarks to jump back to a month.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>A month in the diary, for its bookmark.</summary>
        sealed class LogMonth
        {
            public int Index, Count, Big;
            public VisualElement Header;
        }

        const int LogShown = 200;      // newest entries written out on the page

        static readonly string[] LogMonthNames =
            { "September", "October", "November", "December", "January", "February", "March", "April", "May", "June", "July", "August" };

        int _logFilter, _logCount = -1;     // filter: 0 = everything, 1 = important only
        LogEntry _logLast;
        ScrollView _logDiary, _logMonths;
        VisualElement _logPage;
        Label _logNote;
        readonly List<(Label label, int day)> _logRecent = new List<(Label, int)>();

        partial void ResetLogPage()
        {
            _logFilter = 0;
            _logCount = -1;
            _logLast = null;
        }

        partial void BuildLogPage()
        {
            _logCount = -1;
            _logLast = null;
            _logRecent.Clear();

            var diary = Ink("log-diary");
            var head = Box("log-head");
            head.Add(Text("Diary", "font-hand", "log-title"));
            _logNote = Text("", "font-note", "log-note");
            head.Add(_logNote);
            head.Add(Pills(new[] { "Everything", "Important" }, () => _logFilter, i =>
            {
                _logFilter = i;
                _logCount = -1;
                RefreshLogPage(false);
            }));
            diary.Add(head);
            _logDiary = new ScrollView(ScrollViewMode.Vertical);
            _logDiary.AddToClassList("log-scroll");
            _logPage = Box("log-page");
            _logDiary.Add(_logPage);
            diary.Add(_logDiary);
            _pageHost.Add(diary);

            var side = Ink("log-side");
            side.Add(TapeStrip("log-side-tape", "tape-yellow"));
            side.Add(Text("Months", "font-hand", "log-side-title"));
            _logMonths = new ScrollView(ScrollViewMode.Vertical);
            _logMonths.AddToClassList("log-months");
            side.Add(_logMonths);
            side.Add(Text("Every reply, hire, crisis and celebration gets written down here.", "font-note", "log-side-note"));
            _pageHost.Add(side);
        }

        partial void RefreshLogPage(bool newDay)
        {
            if (_logDiary == null || _sim == null) return;
            var log = S.Log;
            var last = log.Count > 0 ? log[log.Count - 1] : null;
            if (log.Count != _logCount || last != _logLast)
            {
                _logCount = log.Count;
                _logLast = last;
                LogRebuild();
            }
            // "today" becomes "yesterday" without a rebuild.
            foreach (var (label, day) in _logRecent)
            {
                SetText(label, LogWeekday(day));
                label.EnableInClassList("log-date-week--now", day >= S.Day - 1);
            }
        }

        void LogRebuild()
        {
            _logPage.Clear();
            _logMonths.Clear();
            _logRecent.Clear();
            var log = S.Log;
            var months = new List<LogMonth>();
            LogMonth month = null;
            VisualElement lines = null;
            int total = 0, shown = 0, day = -1;
            for (int i = log.Count - 1; i >= 0; i--)
            {
                var e = log[i];
                if (_logFilter == 1 && !e.Important) continue;
                total++;
                if (shown >= LogShown) continue;
                shown++;
                int index = e.Day / Calendar.DaysPerMonth;
                if (month == null || month.Index != index)
                {
                    month = new LogMonth { Index = index, Header = LogMonthHeader(e.Day) };
                    months.Add(month);
                    _logPage.Add(month.Header);
                    day = -1;
                }
                if (e.Day != day)
                {
                    day = e.Day;
                    _logPage.Add(LogDay(day, out lines));
                }
                lines.Add(LogLine(e));
                month.Count++;
                if (e.Important) month.Big++;
            }

            string what = _logFilter == 1 ? (total == 1 ? "big moment" : "big moments") : (total == 1 ? "entry" : "entries");
            SetText(_logNote, total == 0 ? "" : total > shown ? $"{total} {what} · the newest {shown}" : $"{total} {what}");
            if (shown == 0)
            {
                _logPage.Add(Text(_logFilter == 1 ? "No big moments yet. They'll come." : "Nothing written down yet. Give it a day.",
                    "font-note", "empty-note"));
                _logMonths.Add(Text("No months yet.", "font-note", "log-side-empty"));
                return;
            }
            foreach (var m in months) _logMonths.Add(LogBookmark(m));
        }

        VisualElement LogMonthHeader(int day)
        {
            var head = Box("log-month");
            head.Add(Text(LogMonthNames[Calendar.Month(day) - 1], "font-hand", "log-month-name"));
            head.Add(Text($"Year {Calendar.Year(day)} · {LogTerm(day)}", "font-note", "log-month-sub"));
            var line = new DashLine();
            line.AddToClassList("log-month-line");
            head.Add(line);
            return head;
        }

        static string LogTerm(int day) =>
            Calendar.IsSummer(day) ? "summer" : Calendar.IsExamMonth(day) ? "exams" : "semester " + Calendar.Semester(day);

        /// <summary>One day: its date in the margin, then everything written down that day.</summary>
        VisualElement LogDay(int day, out VisualElement lines)
        {
            var row = Box("log-day");
            var date = Box("log-date");
            date.Add(Text(Calendar.DayOfMonth(day).ToString(), "font-hand", "log-date-num"));
            var week = Text(LogWeekday(day), "font-note", "log-date-week");
            date.Add(week);
            if (day >= S.Day - 1) _logRecent.Add((week, day));
            row.Add(date);
            lines = Box("log-lines");
            row.Add(lines);
            return row;
        }

        string LogWeekday(int day) => day == S.Day ? "today" : day == S.Day - 1 ? "yesterday" : Weekdays[Calendar.DayOfWeek(day)];

        static VisualElement LogLine(LogEntry e)
        {
            var line = Box("log-line");
            if (e.Important)
            {
                line.AddToClassList("log-line--big");
                line.Add(Icon("star", "log-star"));
            }
            else line.Add(Box("log-bullet"));
            line.Add(Text(e.Text, e.Important ? "font-heavy" : "font-semi", "log-text"));
            return line;
        }

        VisualElement LogBookmark(LogMonth m)
        {
            int day = m.Index * Calendar.DaysPerMonth;
            var row = Box("log-month-row");
            row.Add(Text($"{Calendar.MonthName(Calendar.Month(day))} · Year {Calendar.Year(day)}", "font-heavy", "log-month-row-name"));
            row.Add(Text(m.Count == 1 ? "1 entry" : $"{m.Count} entries", "font-semi", "log-month-row-count"));
            if (m.Big > 0)
            {
                var stars = Box("log-month-row-stars");
                stars.Add(Icon("star", "icon-13"));
                stars.Add(Text(m.Big.ToString(), "font-heavy"));
                row.Add(stars);
            }
            var header = m.Header;
            OnClick(row, () => _logDiary.ScrollTo(header));
            return row;
        }
    }
}
