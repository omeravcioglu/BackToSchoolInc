using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The year reports as school report cards: pick a year, then see how it went (grades for moods,
    /// exams and reputation), the year in numbers, its highlights and the class that graduated.
    /// During the school months "This year" shows the year under way as a draft, so the page has a
    /// report card from the first day.
    /// </summary>
    public sealed partial class GameHud
    {
        static readonly (ReputationId id, string name, string icon)[] YrpReputations =
        {
            (ReputationId.Overall, "Prestige", "star"),
            (ReputationId.Academic, "Academics", "book"),
            (ReputationId.Sports, "Sports", "trophy"),
            (ReputationId.Dormitory, "Dorms", "bed"),
            (ReputationId.StudentLife, "Student life", "music"),
            (ReputationId.Safety, "Safety", "shield"),
            (ReputationId.Food, "Food", "apple"),
        };

        int _yrpPick = -1, _yrpShown = -2, _yrpCount = -1;     // pick: a report index, -1 = the newest, Draft = this year so far
        bool _yrpDraftPill;
        const int YrpDraft = int.MaxValue;
        VisualElement _yrpYears, _yrpBody, _yrpOverall;
        Label _yrpTitle, _yrpSub, _yrpComment;
        Starburst _yrpBurst;

        partial void ResetReportsPage()
        {
            _yrpPick = -1;
            _yrpShown = -2;
            _yrpCount = -1;
        }

        partial void BuildReportsPage()
        {
            InkIcons.Define("yrp-cap", 1.8f,
                "fill:#A8D1F7 M2.5 9.5L12 5l9.5 4.5L12 14z",
                "M6.5 11.6v4.4c1.6 1.4 3.4 2 5.5 2s3.9-.6 5.5-2v-4.4",
                "M21.5 9.5v5.5");
            _yrpShown = -2;
            _yrpCount = -1;

            var column = Box("yrp-col");
            var head = Box("yrp-head");
            var titles = Box("yrp-titles");
            _yrpTitle = Text("", "font-hand", "yrp-title");
            titles.Add(_yrpTitle);
            _yrpSub = Text("", "font-note", "yrp-sub");
            titles.Add(_yrpSub);
            head.Add(titles);

            _yrpOverall = Box("yrp-overall");
            _yrpBurst = new Starburst();
            _yrpBurst.AddToClassList("yrp-burst");
            _yrpOverall.Add(_yrpBurst);
            var words = Box("yrp-overall-text");
            words.Add(Text("overall", "font-semi", "yrp-overall-label"));
            _yrpComment = Text("", "font-note", "yrp-overall-comment");
            words.Add(_yrpComment);
            _yrpOverall.Add(words);
            head.Add(_yrpOverall);

            _yrpYears = Box("yrp-years");
            head.Add(_yrpYears);
            column.Add(head);
            _yrpBody = Box("yrp-body");
            column.Add(_yrpBody);
            _pageHost.Add(column);
        }

        /// <summary>The year under way can be shown as a draft during the school months.</summary>
        bool YrpDraftAvailable => Calendar.IsSchoolMonth(S.Day);

        partial void RefreshReportsPage(bool newDay)
        {
            if (_yrpBody == null || _sim == null) return;
            if (S.Reports.Count != _yrpCount || YrpDraftAvailable != _yrpDraftPill)
            {
                _yrpCount = S.Reports.Count;
                _yrpDraftPill = YrpDraftAvailable;
                _yrpShown = -2;
                YrpBuildYears();
            }
            // Reports never change once written; the draft and the countdown change daily.
            int pick = YrpPicked();
            if (pick == _yrpShown && !(newDay && (pick < 0 || pick == YrpDraft))) return;
            _yrpShown = pick;
            if (pick == YrpDraft) YrpShow(_sim.YearCycle.Draft(), draft: true);
            else if (pick < 0) YrpShowEmpty();
            else YrpShow(S.Reports[pick], draft: false);
        }

        int YrpPicked()
        {
            if (_yrpPick == YrpDraft && YrpDraftAvailable) return YrpDraft;
            if (_yrpPick >= 0 && _yrpPick < S.Reports.Count) return _yrpPick;
            if (S.Reports.Count > 0) return S.Reports.Count - 1;
            return YrpDraftAvailable ? YrpDraft : -1;
        }

        void YrpBuildYears()
        {
            _yrpYears.Clear();
            int count = S.Reports.Count;
            bool draft = YrpDraftAvailable;
            if (count == 0) return;   // the draft alone needs no tabs
            var labels = new string[count + (draft ? 1 : 0)];
            for (int i = 0; i < count; i++) labels[i] = "Year " + S.Reports[i].Year;
            if (draft) labels[count] = "This year";
            var pills = Pills(labels, () => YrpPicked() == YrpDraft ? S.Reports.Count : YrpPicked(), i =>
            {
                // Picking the newest report keeps following new reports.
                _yrpPick = i >= S.Reports.Count ? YrpDraft : i == S.Reports.Count - 1 ? -1 : i;
                RefreshReportsPage(false);
            });
            pills.AddToClassList("yrp-pills");
            _yrpYears.Add(pills);
        }

        /// <summary>Days until the year's final report, written on the first day of July.</summary>
        int YrpDaysToReport()
        {
            int year = Calendar.Year(S.Day), due = Calendar.StartOfMonth(year, Calendar.AdmissionsMonth);
            if (S.Day >= due) due = Calendar.StartOfMonth(year + 1, Calendar.AdmissionsMonth);
            return due - S.Day;
        }

        void YrpShowEmpty()
        {
            SetText(_yrpTitle, "Year reports");
            SetText(_yrpSub, S.SchoolName);
            Show(_yrpOverall, false);
            _yrpBody.Clear();
            _yrpBody.AddToClassList("yrp-body--empty");

            var card = Panel("yrp-empty");
            card.Add(Icon("book", "yrp-empty-icon"));
            card.Add(Text("The first report card is on its way", "font-hand", "yrp-empty-title"));
            int days = YrpDaysToReport();
            card.Add(Text($"It comes at the end of the school year, in July: {(days == 1 ? "1 day" : days + " days")} to go.", "font-note", "yrp-empty-text"));
            var highlights = S.Year.Highlights;
            if (highlights.Count > 0)
            {
                var list = Box("yrp-so-far");
                list.Add(YrpDivider("so far this year"));
                for (int i = highlights.Count - 1; i >= 0 && i >= highlights.Count - 6; i--) list.Add(YrpBullet("star", highlights[i]));
                card.Add(list);
            }
            _yrpBody.Add(card);
        }

        void YrpShow(YearReport r, bool draft)
        {
            int days = YrpDaysToReport();
            SetText(_yrpTitle, draft ? $"Year {r.Year} so far" : $"Year {r.Year} report");
            SetText(_yrpSub, draft
                ? $"{S.SchoolName} · September to today · the final report comes in July, {(days == 1 ? "tomorrow" : $"in {days} days")}"
                : $"{S.SchoolName} · September to July");
            float overall = YrpOverall(r);
            Show(_yrpOverall, true);
            _yrpBurst.Number.text = YrpGrade(overall);
            SetText(_yrpComment, draft ? "so far: " + YrpComment(overall) : YrpComment(overall));
            _yrpBody.Clear();
            _yrpBody.RemoveFromClassList("yrp-body--empty");
            _yrpBody.Add(YrpGrades(r));
            _yrpBody.Add(YrpNumbers(r, draft));
            _yrpBody.Add(YrpNotes(r, draft));
        }

        // --- Grades ---------------------------------------------------------------------------------

        static string YrpGrade(float v) => v >= 80f ? "A" : v >= 65f ? "B" : v >= 50f ? "C" : v >= 35f ? "D" : "F";

        static string YrpComment(float v) =>
            v >= 80f ? "an outstanding year!" : v >= 65f ? "a strong year" : v >= 50f ? "a steady year" : v >= 35f ? "a tough year" : "a year to learn from";

        /// <summary>The year's overall mark: moods, exams and prestige at the end of the year.</summary>
        static float YrpOverall(YearReport r)
        {
            float sum = r.StudentSatisfaction + r.ParentSatisfaction + r.StaffMorale + r.AvgGrades;
            float prestige = YrpRep(r.ReputationEnd, ReputationId.Overall);
            return float.IsNaN(prestige) ? sum / 4f : (sum + prestige) / 5f;
        }

        static float YrpRep(float[] values, ReputationId id) => values != null && (int)id < values.Length ? values[(int)id] : float.NaN;

        static VisualElement YrpGrades(YearReport r)
        {
            var panel = Panel("yrp-grades");
            panel.Add(Text("How we did", "font-hand", "panel-title"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("yrp-scroll");
            var list = Box("yrp-list");
            list.Add(YrpRow("smile", "Student mood", r.StudentSatisfaction, float.NaN));
            list.Add(YrpRow("people", "Parents", r.ParentSatisfaction, float.NaN));
            list.Add(YrpRow("mug", "Staff morale", r.StaffMorale, float.NaN));
            list.Add(YrpRow("board", "Exam grades", r.AvgGrades, float.NaN));
            list.Add(YrpDivider("reputation, and how it moved"));
            foreach (var (id, name, icon) in YrpReputations)
            {
                float end = YrpRep(r.ReputationEnd, id), start = YrpRep(r.ReputationStart, id);
                if (!float.IsNaN(end)) list.Add(YrpRow(icon, name, end, end - start));
            }
            scroll.Add(list);
            panel.Add(scroll);
            return panel;
        }

        /// <summary>A report-card line: the score on a bar, how it moved over the year, and its letter grade.</summary>
        static VisualElement YrpRow(string icon, string name, float value, float change)
        {
            var row = Box("yrp-row");
            row.Add(Icon(icon, "yrp-row-icon"));
            row.Add(Text(name, "font-semi", "yrp-row-name"));
            var bar = Box("bar", "yrp-row-bar");
            var fill = Box("bar-fill");
            fill.EnableInClassList("bar-fill--low", value < 35f);
            SetPercent(fill, value);
            bar.Add(fill);
            row.Add(bar);
            row.Add(Text(Mathf.RoundToInt(value).ToString(), "font-hand", "yrp-row-value"));
            var moved = Text("", "font-heavy", "yrp-row-delta");
            if (!float.IsNaN(change))
            {
                int d = Mathf.RoundToInt(change);
                moved.text = d == 0 ? "same" : d > 0 ? "+" + d : "–" + -d;
                moved.EnableInClassList("yrp-row-delta--up", d > 0);
                moved.EnableInClassList("yrp-row-delta--down", d < 0);
            }
            row.Add(moved);

            string grade = YrpGrade(value);
            var badge = Ink("yrp-grade");
            badge.EnableInClassList("yrp-grade--good", grade == "A" || grade == "B");
            badge.EnableInClassList("yrp-grade--bad", grade == "D" || grade == "F");
            badge.Add(Text(grade, "font-hand"));
            row.Add(badge);
            return row;
        }

        static VisualElement YrpDivider(string title)
        {
            var divider = Box("yrp-divider");
            divider.Add(Text(title, "font-note", "yrp-divider-label"));
            var line = new DashLine();
            line.AddToClassList("yrp-divider-line");
            divider.Add(line);
            return divider;
        }

        // --- Numbers --------------------------------------------------------------------------------

        static VisualElement YrpNumbers(YearReport r, bool draft)
        {
            var panel = Panel("yrp-numbers");
            panel.Add(Text("The year in numbers", "font-hand", "panel-title"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("yrp-scroll");
            var tiles = Box("yrp-tiles");

            float net = r.Revenue + r.Expenses;   // expenses are negative
            string money = $"earned {Money(r.Revenue)} · spent {Money(-r.Expenses)}";
            if (r.ScholarshipsForgone > 0f) money += $" · aid and scholarships {Money(r.ScholarshipsForgone)}";
            tiles.Add(YrpTile(0, "coin", "money", net >= 0f ? 1 : -1, money, (net >= 0f ? "+" : "") + MoneyShort(net)));

            tiles.Add(YrpTile(1, "students", "students", 0, draft
                    ? $"{r.StudentsEnd} now · {r.Graduates} due to graduate in July"
                    : $"{r.StudentsEnd} continuing · {r.Graduates} graduated · {r.Leavers} won't return",
                r.StudentsStart.ToString(), r.StudentsEnd.ToString()));

            int climb = r.RankStart > 0 && r.RankEnd > 0 ? r.RankStart - r.RankEnd : 0;
            string rankNote = r.RankStart <= 0 ? "our first year in the rankings"
                : climb > 0 ? $"up {climb} {(climb == 1 ? "place" : "places")}"
                : climb < 0 ? $"down {-climb} {(climb == -1 ? "place" : "places")}"
                : "held our place";
            tiles.Add(YrpTile(2, "podium", "national rank", Math.Sign(climb), rankNote, YrpRank(r.RankStart), YrpRank(r.RankEnd)));

            bool played = r.SportsWins + r.SportsLosses > 0;
            string titles = r.Championships == 1 ? "1 championship" : $"{r.Championships} championships";
            tiles.Add(YrpTile(3, "trophy", "sports", 0, played ? $"wins and losses · {titles}" : "no games played",
                played ? $"{r.SportsWins}–{r.SportsLosses}" : "–"));

            tiles.Add(YrpTile(4, "shield", "incidents", 0, r.IncidentCount == 0 ? (draft ? "calm so far" : "a calm year") : "fights, parties and other trouble",
                r.IncidentCount.ToString()));
            tiles.Add(YrpTile(5, "staff", "staff who left", 0, r.StaffLeft == 0 ? (draft ? "everyone's still here" : "everyone stayed") : "resigned, retired or let go",
                r.StaffLeft.ToString()));

            scroll.Add(tiles);
            panel.Add(scroll);
            return panel;
        }

        static string YrpRank(int rank) => rank > 0 ? "#" + rank : "–";

        /// <summary>A number card; with <paramref name="to"/> it reads "from → to" and the tone colours the end.</summary>
        static VisualElement YrpTile(int index, string icon, string label, int tone, string sub, string value, string to = null)
        {
            var tile = Ink("yrp-tile");
            tile.EnableInClassList("yrp-tile--b", index % 2 == 1);
            var top = Box("yrp-tile-top");
            top.Add(Icon(icon, "icon-20"));
            top.Add(Text(label, "font-note", "yrp-tile-label"));
            tile.Add(top);

            var row = Box("yrp-tile-row");
            var first = Text(value, "font-hand", "yrp-tile-value");
            row.Add(first);
            var last = first;
            if (to != null)
            {
                row.Add(Icon("next", "icon-18", "icon--soft", "yrp-tile-arrow"));
                last = Text(to, "font-hand", "yrp-tile-value");
                row.Add(last);
            }
            last.EnableInClassList("yrp-tile-value--good", tone > 0);
            last.EnableInClassList("yrp-tile-value--bad", tone < 0);
            tile.Add(row);
            tile.Add(Text(sub, "font-semi", "yrp-tile-sub"));
            return tile;
        }

        // --- Highlights and graduates ---------------------------------------------------------------

        static VisualElement YrpNotes(YearReport r, bool draft)
        {
            var column = Box("yrp-notes");

            var sticky = Ink("yrp-sticky");
            sticky.Add(Text("Highlights", "font-hand", "yrp-note-title"));
            var highlights = new ScrollView(ScrollViewMode.Vertical);
            highlights.AddToClassList("yrp-scroll");
            if (r.Highlights.Count == 0)
                highlights.Add(Text(draft ? "A quiet year so far. Nothing has made the headlines." : "A quiet year. Nothing made the headlines.", "font-note", "yrp-quiet"));
            foreach (var h in r.Highlights) highlights.Add(YrpBullet("star", h));
            sticky.Add(highlights);
            sticky.Add(TapeStrip("yrp-tape", "tape-sky"));
            column.Add(sticky);

            var graduates = Panel("yrp-class");
            graduates.Add(Text($"Class of Year {r.Year}", "font-hand", "yrp-note-title"));
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("yrp-scroll");
            if (r.GraduateLines.Count == 0)
                list.Add(Text(draft ? "Nobody is in their last year yet." : "Nobody graduated this year.", "font-note", "yrp-quiet"));
            foreach (var line in r.GraduateLines) list.Add(YrpGraduate(line));
            graduates.Add(list);
            column.Add(graduates);
            return column;
        }

        static VisualElement YrpBullet(string icon, string text)
        {
            var row = Box("yrp-bullet");
            row.Add(Icon(icon, "yrp-bullet-icon"));
            row.Add(Text(text, "font-semi", "yrp-bullet-text"));
            return row;
        }

        /// <summary>A graduate's line reads "Name: where they went next".</summary>
        static VisualElement YrpGraduate(string line)
        {
            int split = line.IndexOf(": ", StringComparison.Ordinal);
            var row = Box("yrp-bullet");
            row.Add(Icon("yrp-cap", "yrp-bullet-icon"));
            var text = Box("yrp-grad-text");
            text.Add(Text(split > 0 ? line.Substring(0, split) : line, "font-heavy", "yrp-grad-name"));
            if (split > 0) text.Add(Text(line.Substring(split + 2), "font-semi", "yrp-grad-dest"));
            row.Add(text);
            return row;
        }
    }
}
