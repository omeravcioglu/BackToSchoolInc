using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The money page: the coin jar (today's balance, the month's result and how long the money
    /// lasts), a month of money in and out by category (expected, so far, last month), the balance
    /// over the last six months, the tuition planner, loans and sponsorships, and the endowment.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>One line of a month's money: a category, its signed amount and its colour.</summary>
        struct MonEntry
        {
            public string Name;
            public float Amount;
            public Color Color;
        }

        static readonly string[] MonViews = { "Expected", "So far", "Last month" };

        // The jar holds one more coin for each of these amounts the balance passes.
        static readonly float[] MonCoinSteps = { 0f, 5000f, 15000f, 30000f, 50000f, 80000f, 120000f };
        static readonly InkDrawing[] MonJars = new InkDrawing[MonCoinSteps.Length + 1];
        static readonly StatsMetric MonCashMetric = new StatsMetric { Name = "Money", Icon = "coin", Read = s => s.Cash, Money = true };
        const int MonChartDays = 6 * Calendar.DaysPerMonth;

        readonly List<MonEntry> _monEntries = new List<MonEntry>();
        int _monView, _monSignature, _monCoins = -1;

        VisualElement _monRoot, _monFlowRows, _monLegendBox, _monLegend, _monFlowFoot, _monInBar, _monOutBar;
        InkBox _monLasts, _monInGap, _monOutGap;
        InkPicture _monJar;
        InkIcon _monLastsIcon;
        LineChart _monChart;
        Label _monJarNote, _monCash, _monNet, _monLastsText, _monFlowNote, _monFlowEmpty, _monInTotal, _monOutTotal;
        Label _monInGapText, _monOutGapText, _monFlowNet, _monWaived, _monAxisTop, _monAxisMid, _monAxisBottom, _monChartStart;

        partial void BuildMoneyPage()
        {
            MonDefineIcons();
            _monCoins = -1;
            _monDraftEdited = false;
            var scroll = MonPageScroll("mon-scroll");
            var columns = Box("mon-columns");
            columns.Add(MonBuildLeft());
            columns.Add(MonBuildRight());
            scroll.Add(columns);
            _pageHost.Add(scroll);
            _monRoot = scroll;
        }

        partial void RefreshMoneyPage(bool newDay)
        {
            if (_monRoot == null) return;
            int signature = MonSignature();
            if (!newDay && signature == _monSignature) return;
            _monSignature = signature;
            MonShowJar();
            MonShowFlow();
            MonShowChart();
            MonShowTuition();
            MonShowLoans();
            MonShowSponsors();
            MonShowEndowment();
        }

        partial void ResetMoneyPage()
        {
            _monRoot = null;
            _monView = 0;
            _monDraftEdited = false;
        }

        /// <summary>Changes when something on the page changes between days (a reply, a command elsewhere).</summary>
        int MonSignature()
        {
            var f = S.Finance;
            int h = Mathf.RoundToInt(f.Cash);
            h = h * 31 + Mathf.RoundToInt(S.Policies.AnnualTuition);
            h = h * 31 + Mathf.RoundToInt(S.Policies.PendingTuition);
            h = h * 31 + S.Students.Count;
            h = h * 31 + Mathf.RoundToInt(S.Endowment);
            foreach (var l in f.Loans) h = h * 31 + l.PaymentsLeft;
            foreach (var s in f.Sponsorships) h = h * 31 + s.EndDay;
            foreach (var m in S.Inbox)
                if (m.NeedsDecision) h = h * 31 + m.Id;
            return h;
        }

        // --- Layout ---------------------------------------------------------------------------

        VisualElement MonBuildLeft()
        {
            var column = Box("mon-left");

            var jar = Ink("mon-card", "mon-jar");
            _monJar = new InkPicture();
            _monJar.AddToClassList("mon-jar-art");
            jar.Add(_monJar);
            var text = Box("mon-jar-text");
            _monJarNote = Text("", "font-note", "mon-jar-note");
            text.Add(_monJarNote);
            _monCash = Text("", "font-hand", "mon-cash");
            text.Add(_monCash);
            _monNet = Text("", "font-heavy", "mon-net");
            text.Add(_monNet);
            _monLasts = Ink("mon-lasts");
            _monLastsIcon = Icon("mon-hourglass", "icon-17");
            _monLasts.Add(_monLastsIcon);
            _monLastsText = Text("", "font-heavy", "mon-lasts-text");
            _monLasts.Add(_monLastsText);
            text.Add(_monLasts);
            jar.Add(text);
            column.Add(jar);

            var flow = Ink("mon-card", "mon-flow");
            var head = Box("mon-head");
            head.Add(Text("Money in & out", "font-hand", "mon-title"));
            head.Add(Pills(MonViews, () => _monView, i =>
            {
                _monView = i;
                MonShowFlow();
            }));
            flow.Add(head);
            _monFlowNote = Text("", "font-note", "mon-note");
            flow.Add(_monFlowNote);
            _monFlowRows = Box();
            _monFlowRows.Add(MonFlowRow("in", out _monInBar, out _monInGap, out _monInGapText, out _monInTotal));
            _monFlowRows.Add(MonFlowRow("out", out _monOutBar, out _monOutGap, out _monOutGapText, out _monOutTotal));
            flow.Add(_monFlowRows);
            _monFlowEmpty = Text("", "font-note", "empty-note");
            flow.Add(_monFlowEmpty);
            _monLegendBox = Box();
            var rule = new DashLine();
            rule.AddToClassList("mon-rule");
            _monLegendBox.Add(rule);
            _monLegend = Box("mon-legend");
            _monLegendBox.Add(_monLegend);
            flow.Add(_monLegendBox);
            _monFlowFoot = Box("mon-flow-foot");
            _monFlowNet = Text("", "font-heavy", "mon-flow-net");
            _monFlowFoot.Add(_monFlowNet);
            _monWaived = Text("", "font-note", "mon-waived");
            _monFlowFoot.Add(_monWaived);
            flow.Add(_monFlowFoot);
            column.Add(flow);
            return column;
        }

        static VisualElement MonFlowRow(string label, out VisualElement bar, out InkBox gap, out Label gapText, out Label total)
        {
            var row = Box("mon-flow-row");
            row.Add(Text(label, "font-heavy", "mon-flow-label"));
            var area = Box("mon-flow-area");
            bar = Box("mon-bar");
            area.Add(bar);
            gap = Ink("mon-gap");
            gapText = Text("", "font-heavy", "mon-gap-text");
            gap.Add(gapText);
            area.Add(gap);
            row.Add(area);
            total = Text("", "font-hand", "mon-flow-total");
            row.Add(total);
            return row;
        }

        VisualElement MonBuildRight()
        {
            var column = Box("mon-right");

            var chart = Ink("mon-card", "mon-chart-card");
            var head = MonCardHead("chart", "Bank balance", "last 6 months");
            chart.Add(head);
            var box = Box("mon-chart-box");
            var axis = Box("mon-chart-axis");
            _monAxisTop = Text("", "font-semi");
            _monAxisMid = Text("", "font-semi");
            _monAxisBottom = Text("", "font-semi");
            axis.Add(_monAxisTop);
            axis.Add(_monAxisMid);
            axis.Add(_monAxisBottom);
            box.Add(axis);
            _monChart = new LineChart();
            _monChart.AddToClassList("mon-chart");
            box.Add(_monChart);
            chart.Add(box);
            var dates = Box("mon-chart-dates");
            _monChartStart = Text("", "font-note");
            dates.Add(_monChartStart);
            dates.Add(Text("today", "font-note"));
            chart.Add(dates);
            column.Add(chart);

            column.Add(MonBuildTuition());

            var bottom = Box("mon-bottom");
            _monLoans = Ink("mon-card", "mon-loans");
            bottom.Add(_monLoans);
            _monSponsors = Ink("mon-card", "mon-sponsors");
            bottom.Add(_monSponsors);
            column.Add(bottom);
            column.Add(MonBuildEndowment());
            return column;
        }

        /// <summary>A card's title row: an icon, a handwritten title and a margin note on the right.</summary>
        static VisualElement MonCardHead(string icon, string title, string note)
        {
            var head = Box("mon-head");
            var left = Box("row");
            left.Add(Icon(icon, "icon-24"));
            left.Add(Text(title, "font-hand", "mon-title", "mon-title--icon"));
            head.Add(left);
            if (!string.IsNullOrEmpty(note)) head.Add(Text(note, "font-note", "mon-head-note"));
            return head;
        }

        // --- The coin jar -----------------------------------------------------------------------

        void MonShowJar()
        {
            float cash = S.Finance.Cash, net = _sim.Economy.Project().Net;
            int coins = 0;
            foreach (float step in MonCoinSteps)
                if (cash > step) coins++;
            if (coins != _monCoins)
            {
                _monCoins = coins;
                _monJar.Drawing = MonJarArt(coins);
            }
            SetText(_monJarNote, cash < 0f ? "overdrawn!" : "in the bank");
            SetText(_monCash, Money(cash));
            _monCash.EnableInClassList("mon-bad", cash < 0f);
            SetText(_monNet, Signed(net) + " a month");
            _monNet.EnableInClassList("mon-good", net >= 0f);
            _monNet.EnableInClassList("mon-bad", net < 0f);

            // How long the money lasts, or how long the board waits once the account is overdrawn.
            string text, icon = "mon-hourglass";
            bool good = false, bad = false;
            if (cash < 0f)
            {
                int left = Mathf.Max(0, Balance.InsolvencyGraceDays - S.NegativeCashDays);
                text = left == 1 ? "the board closes us tomorrow" : $"the board closes us in {left} days";
                icon = "alert";
                bad = true;
            }
            else if (net < 0f)
            {
                float months = cash / -net;
                text = months < 1f ? "runs out this month!" : months < 1.5f ? "lasts about a month" : $"lasts about {Mathf.RoundToInt(months)} months";
                bad = months < 3f;
            }
            else
            {
                text = "growing every month";
                icon = "up";
                good = true;
            }
            _monLastsIcon.Icon = icon;
            _monLastsIcon.EnableInClassList("icon--blue", good);
            _monLastsIcon.EnableInClassList("icon--coral", bad);
            SetText(_monLastsText, text);
            _monLasts.EnableInClassList("mon-lasts--good", good);
            _monLasts.EnableInClassList("mon-lasts--bad", bad);
        }

        // --- Money in and out -------------------------------------------------------------------

        void MonShowFlow()
        {
            float waived = MonCollect(out string note, out string empty);
            float income = 0f, expenses = 0f;
            foreach (var e in _monEntries)
            {
                if (e.Amount > 0f) income += e.Amount;
                else expenses -= e.Amount;
            }
            bool any = _monEntries.Count > 0;
            SetText(_monFlowNote, note);
            SetText(_monFlowEmpty, empty);
            Show(_monFlowEmpty, !any);
            Show(_monFlowRows, any);
            Show(_monLegendBox, any);
            Show(_monFlowFoot, any);
            _monLegend.Clear();
            if (!any) return;

            float max = Mathf.Max(income, expenses), net = income - expenses;
            MonBar(_monInBar, true, income, max);
            MonBar(_monOutBar, false, expenses, max);
            MonGap(_monInGap, _monInGapText, net <= -0.5f, net);
            MonGap(_monOutGap, _monOutGapText, net >= 0.5f, net);
            SetText(_monInTotal, Money(income));
            SetText(_monOutTotal, Money(expenses));

            foreach (var e in _monEntries)
            {
                var key = Box("mon-key");
                var swatch = Box("mon-swatch");
                swatch.style.backgroundColor = e.Color;
                key.Add(swatch);
                var text = Box("mon-key-text");
                text.Add(Text(e.Name, "font-semi", "mon-key-name"));
                text.Add(Text(Money(e.Amount), "font-heavy", "mon-key-amount"));
                key.Add(text);
                _monLegend.Add(key);
            }

            SetText(_monFlowNet, "net " + Signed(net));
            _monFlowNet.EnableInClassList("mon-good", net >= 0f);
            _monFlowNet.EnableInClassList("mon-bad", net < 0f);
            SetText(_monWaived, waived >= 1f ? "aid & scholarships waived " + Money(waived) : "");
        }

        /// <summary>
        /// Lists the chosen month's money by category, money in first and biggest first. Returns the
        /// tuition waived through aid and scholarships; <paramref name="note"/> says which month it is.
        /// </summary>
        float MonCollect(out string note, out string empty)
        {
            _monEntries.Clear();
            float waived = 0f;
            empty = "No money has come in or gone out yet.";
            if (_monView == 0)
            {
                var p = _sim.Economy.Project();
                MonAdd("tuition", p.Tuition, LedgerCategory.Tuition);
                MonAdd("dorm fees", p.DormFees, LedgerCategory.DormFees);
                MonAdd("sponsors", p.Sponsorship, LedgerCategory.Sponsorship);
                MonAdd("merch", p.Merchandise, LedgerCategory.Merchandise);
                var signature = _sim.Indices.SignatureBuilding;
                if (signature?.IncomeNote != null) MonAdd(signature.IncomeNote.ToLowerInvariant(), p.Signature, signature.IncomeCategory);
                MonAdd("salaries", -p.Salaries, LedgerCategory.Salaries);
                MonAdd("food", -p.Food, LedgerCategory.Food);
                MonAdd("upkeep", -p.Upkeep, LedgerCategory.Upkeep);
                MonAdd("utilities", -p.Utilities, LedgerCategory.Utilities);
                MonAdd("teams & clubs", -p.Programs, LedgerCategory.SportsOps);
                MonAdd("loan payments", -p.Loans, LedgerCategory.Other);
                // Billed like the monthly bill: each student's discount off a month of tuition.
                float month = S.Policies.AnnualTuition / Balance.TuitionInstallments;
                foreach (var s in S.Students) waived += month * s.Discount;
                note = _sim.InSession ? "a whole month at today's numbers" : "a summer month: no dorm fees or meals";
            }
            else
            {
                var ledger = _monView == 1 ? S.Finance.Current : S.Finance.LastCompletedMonth;
                if (ledger != null)
                {
                    foreach (LedgerCategory c in Enum.GetValues(typeof(LedgerCategory))) MonAdd(MonCategoryName(c), ledger[c], c);
                    waived = ledger.ScholarshipsForgone;
                }
                else empty = "The first month isn't over yet.";
                note = _monView == 1 ? $"{Calendar.MonthName(Calendar.Month(S.Day))} so far, up to day {Calendar.DayOfMonth(S.Day)}"
                    : ledger != null ? $"{Calendar.MonthName(ledger.Month)} of year {ledger.Year}" : "";
            }
            _monEntries.Sort((a, b) => a.Amount > 0f != b.Amount > 0f
                ? (a.Amount > 0f ? -1 : 1)
                : Mathf.Abs(b.Amount).CompareTo(Mathf.Abs(a.Amount)));
            return waived;
        }

        void MonAdd(string name, float amount, LedgerCategory colour)
        {
            if (Mathf.Abs(amount) >= 0.5f) _monEntries.Add(new MonEntry { Name = name, Amount = amount, Color = MonColor(colour) });
        }

        /// <summary>Fills one stacked bar with the money in (or out), as wide as its share of the bigger side.</summary>
        void MonBar(VisualElement bar, bool income, float total, float max)
        {
            bar.Clear();
            Show(bar, total >= 0.5f);
            if (total < 0.5f) return;
            bar.style.width = new Length(100f * total / max, LengthUnit.Percent);
            VisualElement last = null;
            foreach (var e in _monEntries)
            {
                if (e.Amount > 0f != income) continue;
                last = Box("mon-seg");
                last.style.width = new Length(100f * Mathf.Abs(e.Amount) / total, LengthUnit.Percent);
                last.style.backgroundColor = e.Color;
                bar.Add(last);
            }
            last?.AddToClassList("mon-seg--last");
        }

        /// <summary>The dashed box after the shorter bar: what's missing (red) or what's left over (blue).</summary>
        static void MonGap(InkBox gap, Label text, bool on, float net)
        {
            Show(gap, on);
            if (!on) return;
            gap.EnableInClassList("mon-gap--spare", net > 0f);
            SetText(text, Signed(net));
        }

        static string MonCategoryName(LedgerCategory c) => c switch
        {
            LedgerCategory.DormFees => "dorm fees",
            LedgerCategory.Sponsorship => "sponsors",
            LedgerCategory.Merchandise => "merch",
            LedgerCategory.ClubEvents => "club events",
            LedgerCategory.Construction => "building",
            LedgerCategory.SportsOps => "teams",
            LedgerCategory.ClubOps => "clubs",
            _ => c.ToString().ToLowerInvariant(),
        };

        static Color MonColor(LedgerCategory c) => c switch
        {
            LedgerCategory.Tuition => InkIcons.Mid,
            LedgerCategory.DormFees => InkIcons.Sky,
            LedgerCategory.Donations => InkIcons.Hex(0x2F6FD0),
            LedgerCategory.Sponsorship => InkIcons.Marker,
            LedgerCategory.Tickets => InkIcons.Hex(0xFFF3C4),
            LedgerCategory.Merchandise => InkIcons.Hex(0xC9E1F9),
            LedgerCategory.Grants => InkIcons.Pale,
            LedgerCategory.ClubEvents => InkIcons.Hex(0x9CC3EC),
            LedgerCategory.Salaries => InkIcons.Ink,
            LedgerCategory.Food => InkIcons.Marker,
            LedgerCategory.Upkeep => InkIcons.Hex(0x7FA6D6),
            LedgerCategory.Utilities => InkIcons.Pale,
            LedgerCategory.Construction => InkIcons.Hex(0x3F5F93),
            LedgerCategory.Training => InkIcons.Sky,
            LedgerCategory.Compensation => InkIcons.Blush,
            LedgerCategory.Fines => InkIcons.Coral,
            LedgerCategory.Bonuses => InkIcons.Hex(0xFFF3C4),
            LedgerCategory.Severance => InkIcons.Hex(0xC9E1F9),
            LedgerCategory.SportsOps => InkIcons.Mid,
            LedgerCategory.ClubOps => InkIcons.Hex(0x9CC3EC),
            LedgerCategory.Endowment => InkIcons.Hex(0xFFF3C4),
            _ => InkIcons.Hex(0xE3ECF7),
        };

        // --- Balance chart ----------------------------------------------------------------------

        void MonShowChart()
        {
            var values = Series(MonCashMetric, MonChartDays, 120);
            _monChart.SetValues(values);
            bool line = values.Length > 0;
            float lo = _monChart.Min, hi = _monChart.Max;
            SetText(_monAxisTop, line ? AxisMoney(hi, hi - lo) : "");
            SetText(_monAxisMid, line ? AxisMoney((lo + hi) / 2f, hi - lo) : "");
            SetText(_monAxisBottom, line ? AxisMoney(lo, hi - lo) : "");
            DedupeAxis(_monAxisTop, _monAxisMid, _monAxisBottom);
            SetText(_monChartStart, values.Length < 2 ? "the line grows a day at a time"
                : Calendar.Format(Mathf.Max(S.History[0].Day, S.Day - MonChartDays)));
        }

        // --- Pictures -----------------------------------------------------------------------------

        static void MonDefineIcons()
        {
            if (InkIcons.Get("mon-bank") != null) return;
            InkIcons.Define("mon-hourglass", 2.2f, "M6.5 3.5h11M6.5 20.5h11M7.5 3.5c0 5 9 5 9 8.5s-9 3.5-9 8.5M16.5 3.5c0 5-9 5-9 8.5s9 3.5 9 8.5");
            InkIcons.Define("mon-bank", 2f, "fill:#DCEBFB M3.5 9.5L12 4.5l8.5 5z", "M6 10.5v6.5M10 10.5v6.5M14 10.5v6.5M18 10.5v6.5", "M3.5 20.5h17M4.5 17.5h15");
        }

        static InkDrawing MonJarArt(int coins) => MonJars[coins] ??= MonBuildJar(coins);

        /// <summary>The coin jar from the design, with up to seven coins piled inside.</summary>
        static InkDrawing MonBuildJar(int coins)
        {
            var ink = InkIcons.Ink;
            var d = new InkDrawing(150f, 172f);
            d.Circle(75f, 16f, 11f, InkIcons.Marker, ink, 3f);
            d.Line("M78 11c-1-1.2-2.4-1.6-3.8-1.4-1.8.3-2.8 1.5-2.6 2.8.4 2.8 6.4 1.4 6.4 4.4 0 1.4-1.6 2.4-3.4 2.3-1.3 0-2.4-.6-3-1.4", ink, 2f);
            d.Shape("M30 58c10-6 16-14 16-14h58s6 8 16 14c8 5 14 16 14 34v42c0 18-14 30-32 30H48c-18 0-32-12-32-30V92c0-18 6-29 14-34z",
                InkIcons.Hex(0xEAF3FD), ink, 3.5f);
            float[,] pile =
            {
                { 58f, 152f, 20f, 7f }, { 92f, 152f, 20f, 7f }, { 75f, 143f, 20f, 7f }, { 50f, 135f, 18f, 6.5f },
                { 100f, 134f, 18f, 6.5f }, { 72f, 126f, 19f, 6.5f }, { 88f, 117f, 17f, 6f },
            };
            for (int i = 0; i < Mathf.Min(coins, pile.GetLength(0)); i++)
                d.Ellipse(pile[i, 0], pile[i, 1], pile[i, 2], pile[i, 3], InkIcons.Marker, ink, 2.5f);
            d.Rect(38f, 30f, 74f, 18f, InkIcons.Mid, ink, 3.5f, 5f);
            d.Line("M62 39h26", ink, 4f);
            d.Line("M34 78c-4 10-4 26 0 40", Color.white, 5f);
            return d;
        }
    }
}
