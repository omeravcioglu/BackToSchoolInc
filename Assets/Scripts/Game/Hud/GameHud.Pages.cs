using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>A number the statistics page can chart over time.</summary>
    public sealed class StatsMetric
    {
        public string Name, Icon;
        public StatId? Stat;
        public Func<DaySnapshot, float> Read;
        public bool Money, Invert, Whole;

        public static readonly StatsMetric[] All =
        {
            new StatsMetric { Name = "Money", Icon = "coin", Read = s => s.Cash, Money = true },
            new StatsMetric { Name = "Students", Icon = "students", Read = s => s.Students, Whole = true },
            new StatsMetric { Name = "Prestige", Icon = "star", Read = s => s.Prestige },
            new StatsMetric { Name = "Rank", Icon = "podium", Read = s => s.Rank, Invert = true, Whole = true },
            Of(StatId.StudentSatisfaction, "Mood", "smile"),
            Of(StatId.ParentSatisfaction, "Parents", "people"),
            Of(StatId.Trust, "Trust", "heart"),
            Of(StatId.EducationQuality, "Education", "book"),
            Of(StatId.FoodQuality, "Food", "apple"),
            Of(StatId.DormitoryQuality, "Dorms", "house"),
            Of(StatId.Cleanliness, "Clean", "sparkle"),
            Of(StatId.SafetyDiscipline, "Safety", "shield"),
            Of(StatId.StaffMorale, "Staff", "mug"),
            Of(StatId.StudentLife, "Life", "music"),
        };

        static StatsMetric Of(StatId stat, string name, string icon) =>
            new StatsMetric { Name = name, Icon = icon, Stat = stat, Read = s => s.Stats[(int)stat] };

        public static StatsMetric ForStat(StatId stat)
        {
            foreach (var m in All) if (m.Stat == stat) return m;
            return All[0];
        }

        public string Format(float v) => Money ? GameHud.MoneyShort(v) : Invert ? "#" + Mathf.RoundToInt(v) : Mathf.RoundToInt(v).ToString();
    }

    /// <summary>The notebook pages: news, statistics and rankings.</summary>
    public sealed partial class GameHud
    {
        StatsMetric _statsMetric = StatsMetric.All[0];
        int _statsRange = 1;              // 0 = month, 1 = year, 2 = all
        int _newsFilter;                  // 0 = all, 1 = about us, 2 = rivals
        int _newsSelected = -1;
        RankingTable _rankTable = RankingTable.Overall;
        int _pageDay = -1;
        HudPage _builtPage;

        void ResetPages()
        {
            _newsSelected = -1;
            _pageDay = -1;
            _builtPage = HudPage.None;
            _pageHost?.Clear();
            ResetHookedPages();
        }

        void ShowPage(HudPage page)
        {
            _pageHost.Clear();
            _builtPage = page;
            _pageDay = -1;
            switch (page)
            {
                case HudPage.News: BuildNewsPage(); break;
                case HudPage.Stats: BuildStatsPage(); break;
                case HudPage.Ranks: BuildRanksPage(); break;
                default: BuildHookedPage(page); break;
            }
            RefreshPage();
        }

        void RefreshPage()
        {
            if (_page == HudPage.None || _sim == null) return;
            if (_builtPage != _page) ShowPage(_page);
            bool newDay = S.Day != _pageDay;
            _pageDay = S.Day;
            switch (_page)
            {
                case HudPage.News: if (newDay) RefreshNews(); break;
                case HudPage.Stats: RefreshStats(newDay); break;
                case HudPage.Ranks: if (newDay) RefreshRanks(); break;
                default: RefreshHookedPage(_page, newDay); break;
            }
        }

        // --- Shared ---------------------------------------------------------------------------

        static InkBox Panel(params string[] classes)
        {
            var p = Ink("panel");
            foreach (var c in classes) p.AddToClassList(c);
            return p;
        }

        static VisualElement Pills(string[] labels, Func<int> get, Action<int> set)
        {
            var row = Box("pills");
            var pills = new List<Label>();
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var pill = Text(labels[i], "font-heavy", "pill-btn");
                OnClick(pill, () =>
                {
                    set(index);
                    for (int k = 0; k < pills.Count; k++) pills[k].EnableInClassList("pill-btn--on", k == get());
                });
                pills.Add(pill);
                row.Add(pill);
            }
            for (int k = 0; k < pills.Count; k++) pills[k].EnableInClassList("pill-btn--on", k == get());
            return row;
        }

        internal static string MoneyShort(float v)
        {
            float a = Mathf.Abs(v);
            string sign = v < 0f ? "–" : "";
            if (a >= 1_000_000f) return $"{sign}${a / 1_000_000f:0.#}M";
            if (a >= 10_000f) return $"{sign}${a / 1000f:0}k";
            return sign + "$" + Mathf.Round(a).ToString("N0");
        }

        static string RepWord(ReputationId r) => r switch
        {
            ReputationId.Overall => "prestige",
            ReputationId.Academic => "academic rep",
            ReputationId.Sports => "sports rep",
            ReputationId.Dormitory => "dorm rep",
            ReputationId.StudentLife => "student life rep",
            ReputationId.Safety => "safety rep",
            ReputationId.Food => "food rep",
            _ => r.ToString().ToLowerInvariant(),
        };

        static VisualElement EffectChip(NewsStory story)
        {
            bool good = story.Effect > 0f;
            var chip = Box("chip", good ? "chip--good" : "chip--bad");
            chip.Add(Icon(good ? "up" : "down", "chip-icon", good ? "icon--blue" : "icon--coral"));
            chip.Add(Text($"{(good ? "+" : "–")}{Mathf.Abs(story.Effect):0.#} {RepWord(story.EffectRep)} · {story.EffectDays} days", "font-heavy"));
            return chip;
        }

        static string TopicIcon(NewsTopic t) => t switch
        {
            NewsTopic.School => "crest",
            NewsTopic.Academics => "book",
            NewsTopic.Sports => "trophy",
            NewsTopic.Safety => "shield",
            NewsTopic.Food => "apple",
            NewsTopic.Dorms => "bed",
            NewsTopic.StudentLife => "music",
            NewsTopic.Money => "coin",
            NewsTopic.Staff => "staff",
            NewsTopic.Rankings => "podium",
            NewsTopic.Campus => "house",
            _ => "news",
        };

        // --- News -----------------------------------------------------------------------------

        Label _mastheadLabel, _newsDate, _newsIssue, _leadHeadline, _leadStamp, _leadMeta, _leadBody, _newsEmpty;
        InkIcon _leadPictureIcon;
        NewsPhoto _leadPhoto;
        VisualElement _leadChipHost, _newsFront;
        ScrollView _newsList;

        void OpenNews(int storyId)
        {
            _newsSelected = storyId;
            _newsFilter = 0;
            if (_page != HudPage.News) OpenPage(HudPage.News);
            else ShowPage(HudPage.News);
        }

        void BuildNewsPage()
        {
            _newsFront = Ink("news-front");
            _mastheadLabel = Text("", "font-hand", "masthead");
            _newsFront.Add(_mastheadLabel);
            _newsFront.Add(Box("masthead-rule"));
            var dateline = Box("news-dateline");
            _newsDate = Text("", "font-note");
            _newsIssue = Text("", "font-note");
            dateline.Add(_newsDate);
            dateline.Add(_newsIssue);
            _newsFront.Add(dateline);
            var leadRow = Box("lead-row");
            _leadHeadline = Text("", "lead-headline");
            _leadStamp = Text("", "font-heavy", "tone-stamp");
            leadRow.Add(_leadHeadline);
            leadRow.Add(_leadStamp);
            _newsFront.Add(leadRow);
            _leadMeta = Text("", "font-note", "lead-meta");
            _newsFront.Add(_leadMeta);
            var picture = Ink("lead-picture");
            _leadPictureIcon = Icon("news", "lead-picture-icon", "hidden");
            picture.Add(_leadPictureIcon);
            _leadPhoto = new NewsPhoto();
            _leadPhoto.AddToClassList("lead-photo");
            picture.Add(_leadPhoto);
            _newsFront.Add(picture);
            _leadBody = Text("", "lead-body");
            _newsFront.Add(_leadBody);
            _leadChipHost = Box();
            _newsFront.Add(_leadChipHost);
            _newsEmpty = Text("No news yet. The papers will notice the school soon enough, for better or worse.", "font-note", "empty-note");
            _newsFront.Add(_newsEmpty);
            _pageHost.Add(_newsFront);

            var side = Box("news-side");
            var head = Box("news-side-head");
            head.Add(Text("Clippings", "font-hand", "panel-title"));
            head.Add(Pills(new[] { "All", "Us", "Rivals" }, () => _newsFilter, i =>
            {
                _newsFilter = i;
                RefreshNews();
            }));
            side.Add(head);
            _newsList = new ScrollView(ScrollViewMode.Vertical);
            _newsList.AddToClassList("news-list");
            side.Add(_newsList);
            _pageHost.Add(side);
        }

        void RefreshNews()
        {
            var stories = S.News;
            NewsStory lead = null;
            if (_newsSelected >= 0) lead = stories.Find(n => n.Id == _newsSelected);
            if (lead == null)
            {
                for (int i = stories.Count - 1; i >= 0 && lead == null; i--)
                    if (stories[i].AboutUs) lead = stories[i];
                if (lead == null && stories.Count > 0) lead = stories[stories.Count - 1];
                if (lead != null) _newsSelected = lead.Id;
            }

            Show(_newsEmpty, lead == null);
            foreach (var e in new VisualElement[] { _leadHeadline.parent, _leadMeta, _leadPictureIcon.parent, _leadBody, _leadChipHost })
                Show(e, lead != null);
            if (lead != null)
            {
                SetHandText(_mastheadLabel, lead.Outlet);
                SetText(_newsDate, Calendar.Format(lead.Day));
                SetText(_newsIssue, $"issue {lead.Day / 7 + 1}");
                SetText(_leadHeadline, lead.Headline);
                string stamp = lead.Tone == NewsTone.Good ? "good news" : lead.Tone == NewsTone.Bad ? "bad news" : lead.AboutUs ? "news" : "rivals";
                SetText(_leadStamp, stamp);
                _leadStamp.EnableInClassList("tone-stamp--good", lead.Tone == NewsTone.Good);
                _leadStamp.EnableInClassList("tone-stamp--bad", lead.Tone == NewsTone.Bad);
                _leadStamp.EnableInClassList("tone-stamp--neutral", lead.Tone == NewsTone.Neutral);
                SetText(_leadMeta, $"{lead.Outlet} · {(S.Day - lead.Day == 0 ? "today" : S.Day - lead.Day == 1 ? "yesterday" : $"{S.Day - lead.Day} days ago")}");
                _leadPictureIcon.Icon = TopicIcon(lead.Topic);
                _leadPhoto.Show(lead.AboutUs ? lead.Topic : NewsTopic.Rivals, lead.Tone != NewsTone.Bad);
                SetText(_leadBody, lead.Body);
                _leadChipHost.Clear();
                if (lead.EffectLabel != null) _leadChipHost.Add(EffectChip(lead));
            }
            else
            {
                SetText(_mastheadLabel, NewsSystem.Courier);
                SetText(_newsDate, Calendar.Format(S.Day));
                SetText(_newsIssue, "");
            }

            _newsList.Clear();
            int shown = 0;
            for (int i = stories.Count - 1; i >= 0; i--)
            {
                var story = stories[i];
                if (_newsFilter == 1 && !story.AboutUs) continue;
                if (_newsFilter == 2 && story.AboutUs) continue;
                _newsList.Add(ClipRow(story, story == lead));
                if (++shown >= 60) break;
            }
            if (shown == 0) _newsList.Add(Text("Nothing here yet.", "font-note", "empty-note"));
            _sim.News.MarkAllRead();
        }

        VisualElement ClipRow(NewsStory story, bool selected)
        {
            var row = Box("clip-row");
            row.EnableInClassList("clip-row--on", selected);
            row.EnableInClassList("clip-row--unread", !story.Read);
            var badge = Ink("clip-badge");
            badge.EnableInClassList("clip-badge--good", story.Tone == NewsTone.Good);
            badge.EnableInClassList("clip-badge--bad", story.Tone == NewsTone.Bad);
            badge.Add(Icon(story.AboutUs ? TopicIcon(story.Topic) : "news", "icon-20"));
            row.Add(badge);
            var text = Box("clip-text");
            text.Add(Text(story.Headline, "font-semi", "clip-headline"));
            text.Add(Text($"{story.Outlet} · {Calendar.Format(story.Day)}", "font-note", "clip-meta"));
            row.Add(text);
            int id = story.Id;
            OnClick(row, () =>
            {
                _newsSelected = id;
                RefreshNews();
            });
            return row;
        }

        // --- Statistics -----------------------------------------------------------------------

        sealed class MetricCard
        {
            public StatsMetric Metric;
            public InkBox Root;
            public Label Value, Delta;
            public LineChart Spark;
        }

        readonly List<MetricCard> _metricCards = new List<MetricCard>();
        LineChart _chart;
        Label _chartTitle, _chartNow, _axisTop, _axisMid, _axisBottom, _dateStart, _dateEnd, _whyTitle, _whyNote;
        InkIcon _chartIcon;
        VisualElement _whyList;
        StatsMetric _chartShown;
        int _chartRangeShown = -1;

        void BuildStatsPage()
        {
            _metricCards.Clear();
            var column = Box("stats-col");
            var metrics = Box("metrics");
            foreach (var m in StatsMetric.All)
            {
                var card = new MetricCard { Metric = m, Root = Ink("metric") };
                var top = Box("metric-top");
                top.Add(Icon(m.Icon, "metric-icon"));
                top.Add(Text(m.Name, "font-heavy", "metric-name"));
                card.Delta = Text("", "font-heavy", "metric-delta");
                top.Add(card.Delta);
                card.Root.Add(top);
                var bottom = Box("metric-bottom");
                card.Value = Text("", "font-hand", "metric-value");
                bottom.Add(card.Value);
                card.Spark = new LineChart { ShowGrid = false, Area = false, LineWidth = 1.6f, Invert = m.Invert };
                card.Spark.AddToClassList("metric-spark");
                bottom.Add(card.Spark);
                card.Root.Add(bottom);
                var metric = m;
                OnClick(card.Root, () =>
                {
                    _statsMetric = metric;
                    RefreshStats(true);
                });
                metrics.Add(card.Root);
                _metricCards.Add(card);
            }
            column.Add(metrics);

            var main = Box("stats-main");
            var chartPanel = Panel("chart-panel");
            var head = Box("chart-head");
            var titleRow = Box("row", "grow");
            _chartIcon = Icon("chart", "icon-24");
            titleRow.Add(_chartIcon);
            _chartTitle = Text("", "font-hand", "chart-title");
            _chartTitle.style.marginLeft = 8;
            titleRow.Add(_chartTitle);
            _chartNow = Text("", "font-semi", "chart-now");
            titleRow.Add(_chartNow);
            head.Add(titleRow);
            head.Add(Pills(new[] { "Month", "Year", "All" }, () => _statsRange, i =>
            {
                _statsRange = i;
                RefreshStats(true);
            }));
            chartPanel.Add(head);
            var chartBox = Box("chart-box");
            var axis = Box("chart-axis");
            _axisTop = Text("", "font-semi");
            _axisMid = Text("", "font-semi");
            _axisBottom = Text("", "font-semi");
            axis.Add(_axisTop);
            axis.Add(_axisMid);
            axis.Add(_axisBottom);
            chartBox.Add(axis);
            _chart = new LineChart();
            _chart.AddToClassList("chart");
            chartBox.Add(_chart);
            chartPanel.Add(chartBox);
            var dates = Box("chart-dates");
            _dateStart = Text("", "font-note");
            _dateEnd = Text("", "font-note");
            dates.Add(_dateStart);
            dates.Add(_dateEnd);
            chartPanel.Add(dates);
            main.Add(chartPanel);

            var why = Panel("why-panel");
            _whyTitle = Text("Why?", "font-hand", "panel-title");
            why.Add(_whyTitle);
            _whyList = Box("why-list");
            why.Add(_whyList);
            _whyNote = Text("", "font-note", "why-note");
            why.Add(_whyNote);
            main.Add(why);
            column.Add(main);
            _pageHost.Add(column);
            _chartShown = null;
        }

        void RefreshStats(bool force)
        {
            if (_chart == null) return;
            var history = S.History;
            var monthAgo = _sim.History.At(S.Day - 30);
            var now = history.Count > 0 ? history[history.Count - 1] : null;
            if (now == null) return;

            foreach (var card in _metricCards)
            {
                card.Root.EnableInClassList("metric--on", card.Metric == _statsMetric);
                float value = card.Metric.Read(now);
                SetText(card.Value, card.Metric.Format(value));
                float before = monthAgo != null ? card.Metric.Read(monthAgo) : value;
                float change = card.Metric.Invert ? before - value : value - before;
                bool flat = card.Metric.Money ? Mathf.Abs(change) < 500f : Mathf.Abs(change) < 0.5f;
                SetText(card.Delta, flat ? "" : (change > 0f ? "up" : "down"));
                card.Delta.EnableInClassList("metric-delta--up", !flat && change > 0f);
                card.Delta.EnableInClassList("metric-delta--down", !flat && change < 0f);
                if (force || card.Spark.userData == null || (int)card.Spark.userData != S.Day)
                {
                    card.Spark.userData = S.Day;
                    card.Spark.SetValues(Series(card.Metric, 60, 30));
                }
            }

            if (!force && _chartShown == _statsMetric && _chartRangeShown == _statsRange) return;
            _chartShown = _statsMetric;
            _chartRangeShown = _statsRange;

            var m = _statsMetric;
            int days = _statsRange == 0 ? 30 : _statsRange == 1 ? Calendar.DaysPerYear : int.MaxValue;
            var values = Series(m, days, 240);
            float min = float.NaN, max = float.NaN;
            if (m.Stat.HasValue || m.Name == "Prestige") { min = 0f; max = 100f; }
            if (m.Invert) { min = 1f; max = S.Rivals.Count + 1; }
            _chart.Invert = m.Invert;
            _chart.SetValues(values, min, max);
            _chartIcon.Icon = m.Icon;
            SetText(_chartTitle, m.Name);
            float current = m.Read(now);
            float first = values.Length > 0 ? values[0] : current;
            float delta = current - first;
            string trend = Mathf.Abs(delta) < 0.5f ? "steady" : m.Invert
                ? (delta < 0f ? $"up {-delta:0} places" : $"down {delta:0} places")
                : (m.Money ? (delta > 0f ? "+" : "–") + MoneyShort(Mathf.Abs(delta)).Replace("–", "") : (delta > 0f ? "+" : "–") + Mathf.Abs(delta).ToString("0"));
            SetText(_chartNow, $"now {m.Format(current)} · {trend} over {(_statsRange == 0 ? "the month" : _statsRange == 1 ? "the year" : "all time")}");
            float lo = _chart.Min, hi = _chart.Max;
            Func<float, string> axis = m.Money ? v => AxisMoney(v, hi - lo) : m.Format;
            SetText(_axisTop, axis(m.Invert ? lo : hi));
            SetText(_axisMid, axis((lo + hi) / 2f));
            SetText(_axisBottom, axis(m.Invert ? hi : lo));
            DedupeAxis(_axisTop, _axisMid, _axisBottom);
            int startDay = Mathf.Max(history[0].Day, S.Day - Mathf.Min(days, S.Day));
            SetText(_dateStart, Calendar.Format(startDay));
            SetText(_dateEnd, "today");
            RefreshWhy(m);
        }

        /// <summary>The metric's daily values over the last <paramref name="days"/> days, thinned to at most <paramref name="points"/>.</summary>
        float[] Series(StatsMetric m, int days, int points)
        {
            var history = S.History;
            if (history.Count == 0) return Array.Empty<float>();
            int first = 0;
            if (days != int.MaxValue)
            {
                int from = S.Day - days;
                while (first < history.Count - 1 && history[first].Day < from) first++;
            }
            int count = history.Count - first;
            int step = Mathf.Max(1, Mathf.CeilToInt(count / (float)points));
            var list = new List<float>(count / step + 2);
            for (int i = first; i < history.Count; i += step) list.Add(m.Read(history[i]));
            float last = m.Read(history[history.Count - 1]);
            if ((history.Count - 1 - first) % step != 0) list.Add(last);
            return list.ToArray();
        }

        void RefreshWhy(StatsMetric m)
        {
            _whyList.Clear();
            var rows = new List<(string label, float value)>();
            string note = "";
            float scale = 20f;
            if (m.Stat.HasValue)
            {
                foreach (var f in _sim.Stats.StatFactors[(int)m.Stat.Value].Factors) rows.Add((f.Label, f.Value));
                note = m.Stat == StatId.Trust
                    ? $"Promises kept: {S.Promises.FindAll(p => p.Kept).Count} · broken: {S.Promises.FindAll(p => p.Resolved && !p.Kept).Count}"
                    : "Each line adds to or takes away from the score.";
                SetText(_whyTitle, "Why?");
            }
            else if (m.Name == "Prestige" || m.Name == "Rank")
            {
                foreach (var f in _sim.Stats.ReputationFactors[(int)ReputationId.Overall].Factors) rows.Add((f.Label, f.Value));
                note = "Prestige drifts slowly toward these. Rankings also count school size.";
                SetText(_whyTitle, "What drives prestige");
            }
            else if (m.Money)
            {
                var ledger = S.Finance.LastCompletedMonth ?? S.Finance.Current;
                foreach (LedgerCategory c in Enum.GetValues(typeof(LedgerCategory)))
                {
                    float v = ledger[c];
                    string name = MonCategoryName(c);
                    if (Mathf.Abs(v) >= 1f) rows.Add((char.ToUpperInvariant(name[0]) + name.Substring(1), v));
                }
                scale = 0f;
                foreach (var r in rows) scale = Mathf.Max(scale, Mathf.Abs(r.value));
                note = $"Last month: {Signed(ledger.Net)}. Projected this month: {Signed(_sim.Economy.Project().Net)}.";
                SetText(_whyTitle, "Last month's money");
            }
            else
            {
                var ix = _sim.Indices;
                rows.Add(("Beds", ix.Beds));
                rows.Add(("Classroom seats", ix.ClassroomSeats));
                rows.Add(("Meals per sitting", ix.MealCapacity));
                rows.Add(("Families applying", S.Applicants.Count));
                scale = Mathf.Max(1f, S.Students.Count * 1.5f);
                note = "Room to grow: every student needs a bed, a seat and a meal.";
                SetText(_whyTitle, "Room for students");
            }

            rows.Sort((a, b) => Mathf.Abs(b.value).CompareTo(Mathf.Abs(a.value)));
            int n = 0;
            foreach (var (label, value) in rows)
            {
                if (Mathf.Abs(value) < 0.05f) continue;
                if (++n > 10) break;
                _whyList.Add(WhyRow(label, value, scale <= 0f ? 1f : scale, m.Money));
            }
            if (n == 0) _whyList.Add(Text("Nothing to explain yet.", "font-note", "why-note"));
            SetText(_whyNote, note);
        }

        static VisualElement WhyRow(string label, float value, float scale, bool money)
        {
            var row = Box("why-row");
            row.Add(Text(label, "font-semi", "why-label"));
            var bar = Box("why-bar");
            var neg = Box("why-half", "why-half--neg");
            var pos = Box("why-half");
            float pct = Mathf.Clamp01(Mathf.Abs(value) / scale) * 100f;
            var fill = Box("why-fill");
            fill.style.width = new Length(Mathf.Max(4f, pct), LengthUnit.Percent);
            if (value < 0f)
            {
                fill.AddToClassList("why-fill--neg");
                neg.Add(fill);
            }
            else pos.Add(fill);
            bar.Add(neg);
            bar.Add(pos);
            row.Add(bar);
            row.Add(Text(money ? MoneyShort(value) : (value > 0f ? "+" : "–") + Mathf.Abs(value).ToString("0.#"), "font-heavy", "why-value"));
            return row;
        }

        // --- Rankings -------------------------------------------------------------------------

        VisualElement _leagueRows, _liftList;
        Label _ourRank, _ourRankTitle, _ourRankSub, _rankDateStart, _rankAxisBottom;
        LineChart _rankChart;

        static Color ArchetypeColor(Specialization s) => s switch
        {
            Specialization.AcademicAcademy => InkIcons.Sky,
            Specialization.SportsAcademy => InkIcons.Marker,
            Specialization.LuxuryBoarding => InkIcons.Pale,
            Specialization.ArtsAcademy => InkIcons.Hex(0xC9E1F9),
            Specialization.TechnologySchool => InkIcons.Hex(0x9CC3EC),
            Specialization.CommunitySchool => InkIcons.Hex(0xEAF3FD),
            _ => InkIcons.Paper,
        };

        void BuildRanksPage()
        {
            var league = Panel("league");
            var head = Box("league-head");
            head.Add(Text("League table", "font-hand", "panel-title"));
            head.Add(Pills(new[] { "Overall", "Academic", "Sports", "Size" }, () => (int)_rankTable, i =>
            {
                _rankTable = (RankingTable)i;
                RefreshRanks();
            }));
            league.Add(head);
            _leagueRows = Box();
            league.Add(_leagueRows);
            _pageHost.Add(league);

            var side = Box("rank-side");
            var ours = Box("our-rank");
            _ourRank = Text("", "font-hand", "our-rank-num");
            ours.Add(_ourRank);
            var text = Box("our-rank-text");
            _ourRankTitle = Text("", "font-heavy", "our-rank-title");
            _ourRankSub = Text("", "font-note", "our-rank-sub");
            text.Add(_ourRankTitle);
            text.Add(_ourRankSub);
            ours.Add(text);
            side.Add(ours);

            var chartPanel = Panel("rank-chart-panel");
            chartPanel.Add(Text("Our rank over time", "font-hand", "panel-title"));
            var chartBox = Box("chart-box");
            var axis = Box("chart-axis");
            axis.style.width = 34;
            axis.Add(Text("#1", "font-semi"));
            _rankAxisBottom = Text("", "font-semi");
            axis.Add(_rankAxisBottom);
            chartBox.Add(axis);
            _rankChart = new LineChart { Invert = true };
            _rankChart.AddToClassList("chart");
            chartBox.Add(_rankChart);
            chartPanel.Add(chartBox);
            var dates = Box("chart-dates");
            dates.style.marginLeft = 34;
            _rankDateStart = Text("", "font-note");
            dates.Add(_rankDateStart);
            dates.Add(Text("today", "font-note"));
            chartPanel.Add(dates);
            side.Add(chartPanel);

            var lift = Panel();
            lift.style.marginTop = 12;
            lift.Add(Text("What lifts our score", "font-hand", "panel-title"));
            _liftList = Box();
            lift.Add(_liftList);
            side.Add(lift);
            _pageHost.Add(side);
        }

        void RefreshRanks()
        {
            if (_leagueRows == null) return;
            var table = _sim.Rivals.Rankings(_rankTable);
            var monthAgo = _sim.History.At(S.Day - 30);
            float top = 1f;
            foreach (var e in table) top = Mathf.Max(top, e.Value);

            _leagueRows.Clear();
            for (int i = 0; i < table.Count; i++)
            {
                var e = table[i];
                var row = Box("league-row");
                row.EnableInClassList("league-row--us", e.IsPlayer);
                row.Add(Text((i + 1).ToString(), "font-hand", "rank-num"));
                var dot = Ink("school-dot");
                if (e.IsPlayer)
                {
                    dot.SetFill(Color.Lerp(CrestColors[Mathf.Clamp(S.CrestColor, 0, CrestColors.Length - 1)], Color.white, 0.45f));
                    dot.Add(Icon(Emblems[Mathf.Clamp(S.Emblem, 0, Emblems.Length - 1)], "icon-20"));
                }
                else
                {
                    dot.SetFill(ArchetypeColor(e.Rival.Archetype));
                    dot.Add(Text(e.Name.Substring(0, 1), "font-heavy"));
                }
                row.Add(dot);
                row.Add(Text(e.Name, e.IsPlayer ? "font-heavy" : "font-semi", "league-name"));
                var bar = Box("bar", "league-bar");
                var fill = Box("bar-fill");
                SetPercent(fill, 100f * e.Value / top);
                bar.Add(fill);
                row.Add(bar);
                row.Add(Text(_rankTable == RankingTable.Size ? Mathf.RoundToInt(e.Value).ToString() : e.Value.ToString("0"), "font-hand", "league-value"));

                float change = 0f;
                if (_rankTable == RankingTable.Overall && monthAgo != null)
                {
                    if (e.IsPlayer) change = e.Score - monthAgo.Score;
                    else
                    {
                        int index = S.Rivals.IndexOf(e.Rival);
                        if (index >= 0 && index < monthAgo.RivalScores.Length) change = e.Score - monthAgo.RivalScores[index];
                    }
                }
                string trend = change >= 0.5f ? "up" : change <= -0.5f ? "down" : "steady";
                row.Add(Icon(trend, "league-trend", trend == "up" ? "icon--blue" : trend == "down" ? "icon--coral" : "icon--soft"));
                _leagueRows.Add(row);
            }

            // Our standing
            int rank = table.FindIndex(e => e.IsPlayer) + 1;
            SetText(_ourRank, "#" + rank);
            SetText(_ourRankTitle, _rankTable == RankingTable.Overall ? $"of {table.Count} schools" : $"of {table.Count} for {_rankTable.ToString().ToLowerInvariant()}");
            int best = int.MaxValue;
            foreach (var snap in S.History) if (snap.Rank > 0) best = Mathf.Min(best, snap.Rank);
            string sub = best != int.MaxValue ? $"best ever: #{best}" : "";
            if (rank > 1)
            {
                var above = table[rank - 2];
                float gap = above.Value - table[rank - 1].Value;
                sub += $"{(sub.Length > 0 ? " · " : "")}{gap:0.#} {(_rankTable == RankingTable.Size ? "students" : "points")} behind {above.Name}";
            }
            else sub += $"{(sub.Length > 0 ? " · " : "")}top of the table!";
            SetText(_ourRankSub, sub);

            var ranks = Series(StatsMetric.All[3], int.MaxValue, 200);
            _rankChart.SetValues(ranks, 1f, S.Rivals.Count + 1);
            SetText(_rankAxisBottom, "#" + (S.Rivals.Count + 1));
            SetText(_rankDateStart, S.History.Count > 0 ? Calendar.Format(S.History[0].Day) : "");

            // What the national score is made of
            _liftList.Clear();
            var factors = new List<Factor>(_sim.Stats.ReputationFactors[(int)ReputationId.Overall].Factors);
            factors.Sort((a, b) => Mathf.Abs(b.Value).CompareTo(Mathf.Abs(a.Value)));
            float sizeBonus = _sim.Rivals.PlayerScore - S.Prestige;
            var lines = new List<(string, float)> { ("School size", sizeBonus) };
            for (int i = 0; i < factors.Count && lines.Count < 6; i++) lines.Add((factors[i].Label, factors[i].Value));
            float max = 1f;
            foreach (var (_, v) in lines) max = Mathf.Max(max, Mathf.Abs(v));
            foreach (var (label, v) in lines)
            {
                var row = Box("lift-row");
                row.Add(Text(label, "font-semi", "lift-label"));
                row.Add(Text((v >= 0f ? "+" : "–") + Mathf.Abs(v).ToString("0.#"), "font-heavy", "lift-value"));
                var bar = Box("bar", "lift-bar");
                var fill = Box("bar-fill");
                if (v < 0f) fill.AddToClassList("bar-fill--low");
                SetPercent(fill, 100f * Mathf.Abs(v) / max);
                bar.Add(fill);
                row.Add(bar);
                _liftList.Add(row);
            }
        }
    }
}
