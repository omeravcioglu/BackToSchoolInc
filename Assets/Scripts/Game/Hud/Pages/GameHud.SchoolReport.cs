using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The school report: the ten school stats as a graded report card with a few facts about the
    /// school, a "why?" sticky that breaks down whichever line is picked (with a link to its chart),
    /// the seven reputations with the targets they drift toward, what needs attention, and every
    /// promise with how it stands.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>One line of the report card or of the reputation panel.</summary>
        sealed class RptRow
        {
            public InkBox Root, GradeBadge;
            public VisualElement Fill, Target;
            public Label Value, Grade;
            public InkIcon Trend;
        }

        /// <summary>Something that needs the director: how pressing (2 trouble, 1 worth a look, 0 a note), and the page that deals with it.</summary>
        readonly struct RptAlert
        {
            public readonly int Level;
            public readonly string Glyph, Message;
            public readonly HudPage Page;

            public RptAlert(int level, string glyph, string message, HudPage page = HudPage.None)
            {
                Level = level;
                Glyph = glyph;
                Message = message;
                Page = page;
            }
        }

        /// <summary>The report card lines, in StatId order, with what moves each one (from StatSystem).</summary>
        static readonly (StatId stat, string name, string icon, string tip)[] RptStats =
        {
            (StatId.EducationQuality, "Education quality", "book",
                "Teacher quality counts most, then classrooms. A library and labs add more; more than 14 students per teacher costs points."),
            (StatId.StudentSatisfaction, "Student satisfaction", "smile",
                "The average of every student's mood. Open a student to see their own reasons."),
            (StatId.ParentSatisfaction, "Parent satisfaction", "people",
                "The average of every family's mood. Open a student to see what their family thinks."),
            (StatId.DormitoryQuality, "Dormitory quality", "bed",
                "Better rooms, enough bathrooms and a laundry room. Students without a bed cost points."),
            (StatId.StaffMorale, "Staff morale", "mug",
                "The average of everyone's morale: pay, rest, a staff lounge and how happy the students are."),
            (StatId.SafetyDiscipline, "Safety & discipline", "shield",
                "Guards, a security room, a nurse and well-behaved students. Incidents and worn-out buildings cost points."),
            (StatId.FoodQuality, "Food quality", "apple",
                "The menu and the kitchen. Too few chefs, or a cafeteria that's too small, cost points."),
            (StatId.Cleanliness, "Cleanliness", "sparkle",
                "How clean the rooms are on average. Custodians do the cleaning."),
            (StatId.StudentLife, "Student life", "music",
                "Lounges, a gaming room, clubs, sports and a relaxed curfew all help."),
            (StatId.Trust, "Trust", "heart",
                "Kept promises raise it and broken ones cut it. Left alone it drifts back toward 50."),
        };

        static readonly string[] RptRepNames = { "Academic", "Sports", "Dormitory", "Student life", "Safety", "Food", "Overall" };   // ReputationId order

        readonly List<RptRow> _rptStatRows = new List<RptRow>();
        readonly List<RptRow> _rptRepRows = new List<RptRow>();
        int _rptPick = (int)StatId.ParentSatisfaction;
        bool _rptPickRep;
        Label _rptSub, _rptWhyName, _rptWhyValue, _rptWhyTotal, _rptWhyNote, _rptAlertCount, _rptPromiseCount;
        VisualElement _rptWhyList, _rptWhyLinks, _rptFacts, _rptAlerts, _rptPromises;

        partial void BuildReportPage()
        {
            _rptStatRows.Clear();
            _rptRepRows.Clear();

            // The report card, with the school's facts at the bottom.
            var card = Box("rpt-card");
            var head = Box("rpt-card-head");
            head.Add(Text("Report card", "font-hand", "rpt-title"));
            _rptSub = Text("", "font-note", "rpt-sub");
            head.Add(_rptSub);
            card.Add(head);
            var lines = new ScrollView(ScrollViewMode.Vertical);
            lines.AddToClassList("rpt-scroll");
            lines.contentContainer.AddToClassList("rpt-card-content");
            for (int i = 0; i < RptStats.Length; i++)
            {
                var row = RptStatRow(i);
                _rptStatRows.Add(row);
                lines.Add(row.Root);
            }
            var facts = Ink("rpt-facts");
            facts.Add(Text("School", "font-hand", "rpt-facts-title"));
            _rptFacts = Box("rpt-fact-grid");
            facts.Add(_rptFacts);
            lines.Add(facts);
            card.Add(lines);
            _pageHost.Add(card);

            // Why?
            var whyColumn = new ScrollView(ScrollViewMode.Vertical);
            whyColumn.AddToClassList("rpt-why-col");
            whyColumn.contentContainer.AddToClassList("rpt-why-content");
            var why = Ink("rpt-why");
            why.Add(TapeStrip("rpt-why-tape", "tape-sky"));
            why.Add(Text("why is it", "font-note", "rpt-kicker"));
            var whyHead = Box("rpt-why-head");
            _rptWhyName = Text("", "font-hand", "rpt-why-name");
            _rptWhyValue = Text("", "font-hand", "rpt-why-value");
            whyHead.Add(_rptWhyName);
            whyHead.Add(_rptWhyValue);
            why.Add(whyHead);
            _rptWhyList = Box("rpt-why-list");
            why.Add(_rptWhyList);
            var dash = new DashLine();
            dash.AddToClassList("rpt-dash");
            why.Add(dash);
            var total = Box("rpt-total");
            total.Add(Text("Total", "font-heavy", "rpt-total-text"));
            _rptWhyTotal = Text("", "font-heavy", "rpt-total-text");
            total.Add(_rptWhyTotal);
            why.Add(total);
            _rptWhyNote = Text("", "font-note", "rpt-why-note");
            why.Add(_rptWhyNote);
            _rptWhyLinks = Box("rpt-links");
            why.Add(_rptWhyLinks);
            whyColumn.Add(why);
            _pageHost.Add(whyColumn);

            // Reputation, then what needs attention and the promises.
            var side = Box("rpt-side");
            var reputation = Ink("rpt-rep");
            var repHead = Box("rpt-head");
            repHead.Add(Text("Reputation", "font-hand", "rpt-head-title"));
            repHead.Add(Text("slowly follows your stats", "font-note", "rpt-head-note"));
            reputation.Add(repHead);
            for (int i = 0; i < GameState.ReputationCount; i++)
            {
                if (i == (int)ReputationId.Overall)
                {
                    var line = new DashLine();
                    line.AddToClassList("rpt-rep-dash");
                    reputation.Add(line);
                }
                var row = RptRepRow(i);
                _rptRepRows.Add(row);
                reputation.Add(row.Root);
            }
            side.Add(reputation);

            var notes = new ScrollView(ScrollViewMode.Vertical);
            notes.AddToClassList("rpt-scroll");
            notes.contentContainer.AddToClassList("rpt-side-content");
            var alerts = Ink("rpt-alerts");
            alerts.Add(TapeStrip("rpt-alerts-tape", "tape-yellow"));
            var alertHead = Box("rpt-head");
            alertHead.Add(Text("Needs attention", "font-hand", "rpt-head-title"));
            _rptAlertCount = Text("", "font-note", "rpt-head-note");
            alertHead.Add(_rptAlertCount);
            alerts.Add(alertHead);
            _rptAlerts = Box();
            alerts.Add(_rptAlerts);
            notes.Add(alerts);
            var promises = Ink("rpt-promises");
            var promiseHead = Box("rpt-head");
            promiseHead.Add(Text("Promises", "font-hand", "rpt-head-title"));
            _rptPromiseCount = Text("", "font-note", "rpt-head-note");
            promiseHead.Add(_rptPromiseCount);
            promises.Add(promiseHead);
            _rptPromises = Box();
            promises.Add(_rptPromises);
            notes.Add(promises);
            side.Add(notes);
            _pageHost.Add(side);
        }

        partial void RefreshReportPage(bool newDay)
        {
            // Everything here moves with the daily tick (or a command, which also counts as a new day).
            if (!newDay || _rptWhyList == null) return;
            SetText(_rptSub, $"{S.SchoolName} · {Calendar.MonthName(Calendar.Month(S.Day))}, Year {Calendar.Year(S.Day)} · tap a line to see why");
            for (int i = 0; i < _rptStatRows.Count; i++) RptShowStat(_rptStatRows[i], i);
            for (int i = 0; i < _rptRepRows.Count; i++) RptShowRep(_rptRepRows[i], i);
            RptShowPick();
            RptFillFacts();
            RptFillAlerts();
            RptFillPromises();
        }

        partial void ResetReportPage()
        {
            _rptPick = (int)StatId.ParentSatisfaction;
            _rptPickRep = false;
        }

        // --- Report card ----------------------------------------------------------------------

        RptRow RptStatRow(int i)
        {
            var def = RptStats[i];
            var row = new RptRow { Root = Ink("rpt-stat", i % 2 == 0 ? "rpt-stat--a" : "rpt-stat--b") };
            var badge = Ink("rpt-stat-icon");
            badge.Add(Icon(def.icon, "icon-20"));
            row.Root.Add(badge);
            row.Root.Add(Text(def.name, "font-heavy", "rpt-stat-name"));
            var bar = Box("bar", "rpt-stat-bar");
            row.Fill = Box("bar-fill");
            bar.Add(row.Fill);
            row.Root.Add(bar);
            row.Value = Text("", "font-hand", "rpt-stat-value");
            row.Root.Add(row.Value);
            row.GradeBadge = Ink("rpt-grade");
            row.Grade = Text("", "font-hand", "rpt-grade-letter");
            row.GradeBadge.Add(row.Grade);
            row.Root.Add(row.GradeBadge);
            row.Trend = Icon("steady", "rpt-trend");
            row.Root.Add(row.Trend);
            OnClick(row.Root, () => RptPick(i, false));
            return row;
        }

        void RptShowStat(RptRow row, int i)
        {
            float v = S.Stats[i];
            bool low = v < 35f;
            SetText(row.Value, Mathf.RoundToInt(v).ToString());
            row.Value.EnableInClassList("rpt-value--low", low);
            SetPercent(row.Fill, v);
            row.Fill.EnableInClassList("bar-fill--low", low);
            string grade = RptGrade(v);
            SetText(row.Grade, grade);
            row.GradeBadge.EnableInClassList("rpt-grade--top", grade == "A" || grade == "B");
            row.GradeBadge.EnableInClassList("rpt-grade--low", grade == "D" || grade == "F");
            float week = Delta(RptStats[i].stat);
            RptTrend(row.Trend, week >= 1f ? 1 : week <= -1f ? -1 : 0);
        }

        static string RptGrade(float v) => v >= 80f ? "A" : v >= 65f ? "B" : v >= 50f ? "C" : v >= 35f ? "D" : "F";

        static void RptTrend(InkIcon icon, int direction)
        {
            icon.Icon = direction > 0 ? "up" : direction < 0 ? "down" : "steady";
            icon.EnableInClassList("icon--blue", direction > 0);
            icon.EnableInClassList("icon--coral", direction < 0);
            icon.EnableInClassList("icon--soft", direction == 0);
        }

        void RptFillFacts()
        {
            _rptFacts.Clear();
            var ix = _sim.Indices;
            var land = S.Campus;
            RptFact("crest", "Specialization", S.Specialization == Specialization.None ? "None yet" : Names.Specialization(S.Specialization));
            RptFact("house", "Land", $"{land.OwnedParcelCount} parcels · {land.UsedTiles}/{land.OwnedTiles} tiles built on");
            RptFact("staff", "Students per teacher", ix.Teachers == 0 ? "no teachers!" : ix.StudentsPerTeacher.ToString("0.0"), ix.Teachers == 0);
            RptFact("coin", "Fair tuition (families' view)", Money(_sim.FairTuition));
            RptFact("clipboard", "Next inspection", S.InspectionAnnounced ? $"{Calendar.Format(S.NextInspectionDay)} (announced)" : "Unknown");
            if (_sim.IsRevealed(Reveal.Rankings) && S.NationalRank > 0)
                RptFact("podium", "National rank", $"#{S.NationalRank} of {S.Rivals.Count + 1}", false, () => ShowPageAt(HudPage.Ranks));
        }

        void RptFact(string icon, string label, string value, bool bad = false, Action onClick = null)
        {
            var fact = Box("rpt-fact");
            fact.Add(Icon(icon, "rpt-fact-icon"));
            var words = Box("rpt-fact-words");
            words.Add(Text(label, "font-semi", "rpt-fact-label"));
            var text = Text(value, "font-heavy", "rpt-fact-value");
            text.EnableInClassList("rpt-fact-value--bad", bad);
            words.Add(text);
            fact.Add(words);
            if (onClick != null)
            {
                fact.AddToClassList("rpt-fact--link");
                fact.Add(Icon("next", "rpt-fact-go", "icon--blue"));
                OnClick(fact, onClick);
            }
            _rptFacts.Add(fact);
        }

        // --- Reputation -----------------------------------------------------------------------

        RptRow RptRepRow(int i)
        {
            bool overall = i == (int)ReputationId.Overall;
            var row = new RptRow { Root = Ink("rpt-rep-row") };
            row.Root.Add(Text(RptRepNames[i], overall ? "font-heavy" : "font-semi", "rpt-rep-name"));
            var track = Box("rpt-rep-track");
            var bar = Box("bar", "rpt-rep-bar");
            row.Fill = Box("bar-fill");
            if (overall)
            {
                bar.AddToClassList("rpt-rep-bar--overall");
                row.Fill.AddToClassList("rpt-fill--gold");
            }
            bar.Add(row.Fill);
            track.Add(bar);
            row.Target = Box("rpt-target");
            track.Add(row.Target);
            row.Root.Add(track);
            row.Value = Text("", "font-heavy", "rpt-rep-value");
            row.Root.Add(row.Value);
            row.Trend = Icon("steady", "rpt-rep-trend");
            row.Root.Add(row.Trend);
            OnClick(row.Root, () => RptPick(i, true));
            return row;
        }

        /// <summary>A reputation, its bar, a tick at the target it drifts toward, and which way it's going.</summary>
        void RptShowRep(RptRow row, int i)
        {
            float v = S.Reputation[i], target = _sim.Stats.ReputationTargets[i];
            int direction = Mathf.Abs(target - v) < 0.5f ? 0 : target > v ? 1 : -1;
            SetText(row.Value, Mathf.RoundToInt(v).ToString());
            SetPercent(row.Fill, v);
            row.Target.style.left = new Length(Mathf.Clamp(target, 0f, 100f), LengthUnit.Percent);
            row.Target.EnableInClassList("rpt-target--up", direction > 0);
            row.Target.EnableInClassList("rpt-target--down", direction < 0);
            RptTrend(row.Trend, direction);
        }

        // --- Why? -----------------------------------------------------------------------------

        void RptPick(int index, bool reputation)
        {
            _rptPick = index;
            _rptPickRep = reputation;
            RptShowPick();
        }

        void RptShowPick()
        {
            for (int i = 0; i < _rptStatRows.Count; i++) _rptStatRows[i].Root.EnableInClassList("rpt-stat--on", !_rptPickRep && _rptPick == i);
            for (int i = 0; i < _rptRepRows.Count; i++) _rptRepRows[i].Root.EnableInClassList("rpt-rep-row--on", _rptPickRep && _rptPick == i);
            RptFillWhy();
        }

        /// <summary>Every term behind the picked stat or reputation, biggest first, as plus and minus bars.</summary>
        void RptFillWhy()
        {
            Breakdown breakdown;
            string name, note;
            float value;
            StatsMetric metric = null;
            bool students = false;
            if (_rptPickRep)
            {
                breakdown = _sim.Stats.ReputationFactors[_rptPick];
                value = S.Reputation[_rptPick];
                float target = _sim.Stats.ReputationTargets[_rptPick];
                bool overall = _rptPick == (int)ReputationId.Overall;
                name = overall ? "Prestige" : RptRepNames[_rptPick] + " reputation";
                note = Mathf.Abs(target - value) < 0.5f
                    ? $"Steady at its target of {target:0}."
                    : $"{(target > value ? "Rising" : "Falling")} toward {target:0}: it moves a little closer every day.";
                if (overall) metric = Array.Find(StatsMetric.All, m => m.Name == "Prestige");
            }
            else
            {
                var def = RptStats[_rptPick];
                breakdown = _sim.Stats.StatFactors[_rptPick];
                value = S.Stats[_rptPick];
                name = def.name;
                note = def.tip;
                if (def.stat == StatId.Trust)
                    note += $" Promises kept: {S.Promises.FindAll(p => p.Resolved && p.Kept).Count} · broken: {S.Promises.FindAll(p => p.Resolved && !p.Kept).Count}.";
                metric = StatsMetric.ForStat(def.stat);
                students = def.stat == StatId.StudentSatisfaction || def.stat == StatId.ParentSatisfaction;
            }
            SetText(_rptWhyName, name);
            SetText(_rptWhyValue, Mathf.RoundToInt(value).ToString());

            _rptWhyList.Clear();
            var factors = new List<Factor>(breakdown.Factors);
            factors.RemoveAll(f => Mathf.Abs(f.Value) < 0.05f);
            factors.Sort((x, y) => Mathf.Abs(y.Value).CompareTo(Mathf.Abs(x.Value)));
            float scale = 1f;
            foreach (var f in factors) scale = Mathf.Max(scale, Mathf.Abs(f.Value));
            foreach (var f in factors) _rptWhyList.Add(RptFactorRow(f.Label, f.Value, scale));
            if (factors.Count == 0) _rptWhyList.Add(Text("Nothing to explain yet.", "font-note", "rpt-empty"));
            SetText(_rptWhyTotal, Mathf.RoundToInt(breakdown.Clamped).ToString());
            SetText(_rptWhyNote, note);

            _rptWhyLinks.Clear();
            if (metric != null && _sim.IsRevealed(Reveal.Stats)) _rptWhyLinks.Add(RptLink("chart", "see it over time", () => OpenStats(metric)));
            if (students && _sim.IsRevealed(Reveal.Students)) _rptWhyLinks.Add(RptLink("people", "see the students", () => ShowPageAt(HudPage.Students)));
        }

        static VisualElement RptFactorRow(string label, float value, float scale)
        {
            var row = Box("rpt-factor");
            row.Add(Text(label, "font-semi", "rpt-factor-label"));
            var bar = Box("rpt-factor-bar");
            var minus = Box("rpt-factor-half", "rpt-factor-half--minus");
            var plus = Box("rpt-factor-half");
            var fill = Box("rpt-factor-fill");
            fill.style.width = new Length(Mathf.Max(6f, 100f * Mathf.Abs(value) / scale), LengthUnit.Percent);
            if (value < 0f)
            {
                fill.AddToClassList("rpt-factor-fill--minus");
                minus.Add(fill);
            }
            else plus.Add(fill);
            bar.Add(minus);
            bar.Add(plus);
            row.Add(bar);
            var number = Text((value >= 0f ? "+" : "–") + Mathf.Abs(value).ToString("0.0"), "font-heavy", "rpt-factor-value");
            number.EnableInClassList("rpt-factor-value--minus", value < 0f);
            row.Add(number);
            return row;
        }

        static VisualElement RptLink(string icon, string label, Action onClick)
        {
            var link = Box("rpt-link");
            link.Add(Icon(icon, "rpt-link-icon"));
            link.Add(Text(label, "font-note", "rpt-link-label"));
            OnClick(link, onClick);
            return link;
        }

        // --- Needs attention ------------------------------------------------------------------

        void RptFillAlerts()
        {
            _rptAlerts.Clear();
            var alerts = RptAlerts();
            foreach (var a in alerts) _rptAlerts.Add(RptAlertRow(a));
            if (alerts.Count == 0)
            {
                var calm = Box("rpt-alert");
                var badge = Ink("rpt-alert-badge", "rpt-alert-badge--calm");
                badge.Add(Icon("check", "rpt-alert-icon", "icon--blue"));
                calm.Add(badge);
                calm.Add(Text("Nothing urgent. A calm day at school.", "font-semi", "rpt-alert-text"));
                _rptAlerts.Add(calm);
            }
            SetText(_rptAlertCount, alerts.Count == 0 ? "all clear" : alerts.Count == 1 ? "1 thing" : $"{alerts.Count} things");
        }

        /// <summary>What the school should look at today, worst kinds first.</summary>
        List<RptAlert> RptAlerts()
        {
            var list = new List<RptAlert>();
            var ix = _sim.Indices;
            int homeless = 0;
            foreach (var s in S.Students)
                if (s.DormRoomId < 0) homeless++;
            if (homeless > 0) list.Add(new RptAlert(2, "bed", homeless == 1 ? "1 student has no bed." : $"{homeless} students have no bed."));
            if (S.Finance.Cash < 0f)
                list.Add(new RptAlert(2, "coin", $"The school is overdrawn ({Money(S.Finance.Cash)}). {Balance.InsolvencyGraceDays - S.NegativeCashDays} days before the board steps in.", HudPage.Money));
            float net = _sim.Economy.Project().Net;
            if (net < 0f) list.Add(new RptAlert(1, "chart", $"Projected monthly result: {Money(net)}.", HudPage.Money));
            if (ix.Teachers == 0) list.Add(new RptAlert(2, "staff", "There are no active teachers!", HudPage.Staff));
            else if (ix.StudentsPerTeacher > 14f)
                list.Add(new RptAlert(1, "staff", $"Classes are too big ({ix.StudentsPerTeacher:0.0} students per teacher). Hire teachers.", HudPage.Staff));
            if (ix.Students > ix.ClassroomSeats)
                list.Add(new RptAlert(2, "board", $"Classrooms are overcrowded ({ix.Students} students, {ix.ClassroomSeats} seats).", HudPage.Rooms));
            if (ix.MealCapacity > 0 && ix.Students > ix.MealCapacity)
                list.Add(new RptAlert(1, "apple", $"Cafeteria too small ({ix.MealCapacity:0} per sitting): long lines.", HudPage.Rooms));
            if (ix.BathroomShortage > 0.01f)
                list.Add(new RptAlert(1, "house", $"Bathroom shortage: {RptPercent(ix.BathroomShortage)} of shared-bathroom residents are underserved.", HudPage.Rooms));
            if (ix.ChefCoverage < 1f) list.Add(new RptAlert(1, "apple", $"Kitchen understaffed ({RptPercent(ix.ChefCoverage)} chef coverage).", HudPage.Staff));
            if (ix.CustodianCoverage < 1f) list.Add(new RptAlert(1, "sparkle", $"Not enough custodians ({RptPercent(ix.CustodianCoverage)} coverage).", HudPage.Staff));
            if (ix.GuardCoverage < 1f && ix.Students > 0)
                list.Add(new RptAlert(ix.GuardCoverage == 0f ? 1 : 0, "shield", $"Security coverage {RptPercent(ix.GuardCoverage)}. Safety suffers.", HudPage.Staff));
            if (ix.FreeBeds == 0) list.Add(new RptAlert(0, "bed", "Every bed is full. Build or upgrade dorms to grow."));
            int worn = 0;
            foreach (var r in S.Rooms)
                if (r.IsBuilt(S.Day) && r.Condition < 40f) worn++;
            if (worn > 0)
                list.Add(new RptAlert(1, "house", worn == 1 ? "1 room is badly worn. Repair it or hire maintenance." : $"{worn} rooms are badly worn. Repair them or hire maintenance.", HudPage.Rooms));
            int exhausted = 0;
            foreach (var s in S.Staff)
                if (s.Energy < 20f) exhausted++;
            if (exhausted > 0)
                list.Add(new RptAlert(1, "mug", exhausted == 1 ? "1 staff member is exhausted." : $"{exhausted} staff members are exhausted.", HudPage.Staff));
            if (S.InspectionAnnounced) list.Add(new RptAlert(1, "clipboard", $"Inspection on {Calendar.Format(S.NextInspectionDay)}."));
            if (S.Policies.PendingTuition > 0f) list.Add(new RptAlert(0, "coin", $"Tuition changes to {Money(S.Policies.PendingTuition)} next semester."));
            int decisions = _sim.Inbox.PendingDecisions;
            if (decisions > 0)
                list.Add(new RptAlert(1, "envelope", decisions == 1 ? "1 message is waiting for your decision." : $"{decisions} messages are waiting for your decision.", HudPage.Inbox));
            return list;
        }

        static string RptPercent(float fraction) => Mathf.RoundToInt(fraction * 100f) + "%";

        VisualElement RptAlertRow(RptAlert a)
        {
            var row = Box("rpt-alert");
            var badge = Ink("rpt-alert-badge", a.Level == 2 ? "rpt-alert-badge--bad" : a.Level == 1 ? "rpt-alert-badge--warn" : "rpt-alert-badge--calm");
            badge.Add(Icon(a.Glyph, "rpt-alert-icon"));
            row.Add(badge);
            var text = Text(a.Message, "font-semi", "rpt-alert-text");
            text.EnableInClassList("rpt-alert-text--bad", a.Level == 2);
            text.EnableInClassList("rpt-alert-text--calm", a.Level == 0);
            row.Add(text);
            if (a.Page != HudPage.None)
            {
                row.AddToClassList("rpt-alert--link");
                row.Add(Icon("next", "rpt-alert-go", "icon--blue"));
                var page = a.Page;
                OnClick(row, () => ShowPageAt(page));
            }
            return row;
        }

        // --- Promises -------------------------------------------------------------------------

        /// <summary>Open promises first, soonest due on top, then the kept and broken ones, newest first.</summary>
        void RptFillPromises()
        {
            _rptPromises.Clear();
            var open = S.Promises.FindAll(p => !p.Resolved);
            open.Sort((a, b) => a.DueDay.CompareTo(b.DueDay));
            foreach (var p in open) _rptPromises.Add(RptPromiseRow(p));
            int kept = 0, broken = 0;
            for (int i = S.Promises.Count - 1; i >= 0; i--)
            {
                var p = S.Promises[i];
                if (!p.Resolved) continue;
                if (p.Kept) kept++;
                else broken++;
                _rptPromises.Add(RptPromiseRow(p));
            }
            if (S.Promises.Count == 0) _rptPromises.Add(Text("You haven't promised anything yet.", "font-note", "rpt-empty"));
            SetText(_rptPromiseCount, S.Promises.Count == 0 ? "" : $"{open.Count} open · {kept} kept · {broken} broken");
        }

        VisualElement RptPromiseRow(Promise p)
        {
            bool met = !p.Resolved && _sim.Promises.IsMet(p);
            int left = Math.Max(0, p.DueDay - S.Day);
            var row = Box("rpt-promise");
            row.EnableInClassList("rpt-promise--done", p.Resolved);
            var box = Box("rpt-check");
            if (p.Resolved) box.Add(Icon(p.Kept ? "check" : "close", "rpt-check-icon", p.Kept ? "icon--blue" : "icon--coral"));
            else if (met) box.Add(Icon("check", "rpt-check-icon", "icon--soft"));
            row.Add(box);

            var body = Box("rpt-promise-body");
            body.Add(Text(p.Text, "font-semi", "rpt-promise-text"));
            string to = PromisedTo(p);
            string due = $"due {Calendar.Format(p.DueDay)}";
            body.Add(Text(to == null ? due : $"to {to} · {due}", "font-note", "rpt-promise-sub"));
            row.Add(body);

            string status = p.Resolved ? (p.Kept ? "kept" : "broken") : met ? "on track" : left == 1 ? "1 day left" : $"{left} days left";
            string tone = p.Resolved ? (p.Kept ? "rpt-chip--kept" : "rpt-chip--broken") : met ? "rpt-chip--ok" : left <= 14 ? "rpt-chip--late" : "rpt-chip--open";
            row.Add(Text(status, "font-heavy", "rpt-chip", tone));
            return row;
        }
    }
}
