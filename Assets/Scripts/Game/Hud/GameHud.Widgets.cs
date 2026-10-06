using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>The HUD's live panels: top bar numbers, inbox clipboard, promises, vitals and toasts.</summary>
    public sealed partial class GameHud
    {
        static readonly string[] Weekdays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        static readonly (StatId stat, string name, string icon)[] VitalDefs =
        {
            (StatId.EducationQuality, "Education", "book"),
            (StatId.FoodQuality, "Food", "apple"),
            (StatId.DormitoryQuality, "Dorms", "house"),
            (StatId.StudentLife, "Student life", "music"),
            (StatId.StaffMorale, "Staff morale", "mug"),
            (StatId.SafetyDiscipline, "Safety", "shield"),
        };

        Label _schoolName, _levelPill, _nextUnlock, _cashValue, _cashSub, _studentsValue, _bedsValue, _studentsSub;
        Label _prestigeValue, _rankPill, _moodValue, _moodDelta, _trustValue, _trustDelta;
        Label _calMonth, _calDay, _calWeekday, _term, _inboxPill, _inboxEmpty, _promisesNote;
        VisualElement _xpFill, _toastLayer, _trustBox, _trustDivider;
        InkBox _promisesCard, _crest;
        InkIcon _crestIcon;
        int _crestShown = -1;

        /// <summary>Emblems a school can choose for its crest, and the colours.</summary>
        public static readonly string[] Emblems = { "crest", "star", "book", "owl", "bell", "sun" };
        public static readonly Color[] CrestColors =
        {
            InkIcons.Hex(0x2F6FD0), InkIcons.Hex(0x1E3A6D), InkIcons.Hex(0x5A9BEA),
            InkIcons.Hex(0xA8D1F7), InkIcons.Hex(0xFFE066), InkIcons.Hex(0xFF8C6E),
        };

        // The right column drops rows when it would run off the bottom of the screen: 0 shows
        // everything, each level gives up a little more. Reset whenever the content changes.
        int _fit, _fitSignature = int.MinValue;
        int InboxLimit => _fit >= 4 ? 1 : _fit >= 2 ? 2 : 3;
        int PromiseLimit => _fit >= 3 ? 0 : _fit >= 1 ? 1 : 2;

        void RefreshData()
        {
            _sim.Tutorial.Check();
            RefreshNav();
            RefreshTopBar();
            PickInbox();
            PickPromises();
            int signature = 17;
            foreach (var m in _inboxPick) signature = signature * 31 + m.Id;
            for (int i = 0; i < _promisePick.Count && i < _promiseRows.Length; i++) signature = signature * 31 + _promisePick[i].Id;
            if (signature != _fitSignature)
            {
                _fitSignature = signature;
                _fit = 0;
            }
            ShowInbox();
            ShowPromises();
            RefreshVitals();
        }

        void FitRightColumn()
        {
            if (_sim == null || _rightColumn.ClassListContains("hidden")) return;
            float bottom = _rightColumn.layout.yMax, limit = _hud.layout.height - 10f;
            if (float.IsNaN(bottom) || bottom <= limit || _fit >= 4) return;
            _fit++;
            ShowInbox();
            ShowPromises();
        }

        // --- Top bar --------------------------------------------------------------------------

        void BindTopBar()
        {
            _schoolName = _root.Q<Label>("schoolName");
            _levelPill = _root.Q<Label>("levelPill");
            _nextUnlock = _root.Q<Label>("nextUnlock");
            _xpFill = _root.Q("xpFill");
            _cashValue = _root.Q<Label>("cashValue");
            _cashSub = _root.Q<Label>("cashSub");
            _studentsValue = _root.Q<Label>("studentsValue");
            _bedsValue = _root.Q<Label>("bedsValue");
            _studentsSub = _root.Q<Label>("studentsSub");
            _prestigeValue = _root.Q<Label>("prestigeValue");
            _rankPill = _root.Q<Label>("rankPill");
            _moodValue = _root.Q<Label>("moodValue");
            _moodDelta = _root.Q<Label>("moodDelta");
            _trustValue = _root.Q<Label>("trustValue");
            _trustDelta = _root.Q<Label>("trustDelta");
            _calMonth = _root.Q<Label>("calMonth");
            _calDay = _root.Q<Label>("calDay");
            _calWeekday = _root.Q<Label>("calWeekday");
            _term = _root.Q<Label>("term");
            _toastLayer = _root.Q("toasts");
            _trustBox = _root.Q("trustBox");
            _trustDivider = _root.Q("trustDivider");
            _crest = _root.Q<InkBox>("crest");
            _crestIcon = _root.Q<InkIcon>("crestIcon");
        }

        void RefreshTopBar()
        {
            var progress = S.Progress;
            SetHandText(_schoolName, S.SchoolName);
            int crest = S.Emblem * 16 + S.CrestColor;
            if (crest != _crestShown)
            {
                _crestShown = crest;
                _crestIcon.Icon = Emblems[Mathf.Clamp(S.Emblem, 0, Emblems.Length - 1)];
                var color = CrestColors[Mathf.Clamp(S.CrestColor, 0, CrestColors.Length - 1)];
                // The crest disc takes a light tint of the school colour so the ink emblem stays readable.
                _crest.style.backgroundColor = StyleKeyword.Null;
                _crest.SetFill(Color.Lerp(color, Color.white, 0.55f));
            }
            SetText(_levelPill, "Lv " + progress.SchoolLevel);
            int from = Balance.SchoolXpForLevel(progress.SchoolLevel), to = Balance.SchoolXpForLevel(progress.SchoolLevel + 1);
            SetPercent(_xpFill, to > from ? 100f * (progress.SchoolXp - from) / (to - from) : 100f);
            SetText(_nextUnlock, NextUnlock(progress.SchoolLevel));

            float cash = S.Finance.Cash;
            SetText(_cashValue, Money(cash));
            _cashValue.EnableInClassList("res-value--bad", cash < 0f);
            float net = _sim.Economy.Project().Net;
            SetText(_cashSub, Signed(net) + "/mo");
            _cashSub.EnableInClassList("res-sub--bad", net < 0f);
            _cashSub.EnableInClassList("res-sub--good", net >= 0f);

            var ix = _sim.Indices;
            SetText(_studentsValue, S.Students.Count.ToString());
            SetText(_bedsValue, "/" + ix.Beds);
            SetText(_studentsSub, ix.FreeBeds == 1 ? "1 bed free" : ix.FreeBeds + " beds free");

            SetText(_prestigeValue, Mathf.RoundToInt(S.Prestige).ToString());
            SetText(_rankPill, "#" + S.NationalRank);
            _rankPill.EnableInClassList("hidden", S.NationalRank <= 0 || !_sim.IsRevealed(Reveal.Rankings));
            ShowStat(_moodValue, _moodDelta, StatId.StudentSatisfaction);
            ShowStat(_trustValue, _trustDelta, StatId.Trust);
            bool trust = _sim.IsRevealed(Reveal.Promises) || progress.SchoolLevel >= 2;
            _trustBox.EnableInClassList("hidden", !trust);
            _trustDivider.EnableInClassList("hidden", !trust);

            int day = S.Day;
            SetText(_calMonth, Calendar.MonthName(Calendar.Month(day)).ToUpperInvariant());
            SetText(_calDay, Calendar.DayOfMonth(day).ToString());
            SetText(_calWeekday, Weekdays[Calendar.DayOfWeek(day)]);
            string term = !_sim.InSession ? "Summer" : Calendar.IsExamMonth(day) ? "Exams" : "Semester " + Calendar.Semester(day);
            SetText(_term, $"Year {Calendar.Year(day)} · {term}");

            int pending = _sim.Inbox.PendingDecisions;
            SetText(_inboxBadge, pending.ToString());
            _inboxBadge.EnableInClassList("hidden", pending == 0);
            _tabInbox.EnableInClassList("tab-inbox--alert", pending > 0);
            int unreadNews = _sim.News.Unread;
            SetText(_newsBadge, unreadNews > 9 ? "9+" : unreadNews.ToString());
            _newsBadge.EnableInClassList("hidden", unreadNews == 0);
            SetText(_admissionsCount, S.Applicants.Count > 0 ? S.Applicants.Count.ToString() : "");
        }

        void ShowStat(Label value, Label delta, StatId stat)
        {
            SetText(value, Mathf.RoundToInt(S.Stat(stat)).ToString());
            int d = Mathf.RoundToInt(Delta(stat));
            SetText(delta, d == 0 ? "" : d > 0 ? "+" + d : "–" + -d);
            delta.EnableInClassList("delta--up", d > 0);
            delta.EnableInClassList("delta--down", d < 0);
        }

        /// <summary>The next thing the school will unlock, for the badge: "stats at Lv 2".</summary>
        static string NextUnlock(int level)
        {
            for (int next = level + 1; next <= level + 40; next++)
            {
                foreach (var r in ProgressionSystem.RevealsAt(next)) return $"{RevealShort(r)} at Lv {next}";
                foreach (Feature f in Enum.GetValues(typeof(Feature)))
                    if (Balance.UnlockLevel(f) == next) return $"{FeatureShort(f)} at Lv {next}";
                foreach (var def in ProgressionSystem.RoomsUnlockedAt(next)) return $"{def.Name.ToLowerInvariant()} at Lv {next}";
            }
            return "";
        }

        static string RevealShort(Reveal r) => r switch
        {
            Reveal.Rules => "rules",
            Reveal.Stats => "stats",
            Reveal.Land => "land",
            Reveal.Admissions => "admissions",
            _ => r.ToString().ToLowerInvariant(),
        };

        static string FeatureShort(Feature f) => f switch
        {
            Feature.Clubs => "clubs",
            Feature.Administrators => "admins",
            Feature.Scholarships => "grants",
            Feature.AdvancedDorms => "dorms+",
            Feature.Specializations => "majors",
            Feature.SportsPrograms => "sports",
            Feature.InternationalStudents => "abroad",
            Feature.MajorSponsorships => "sponsors",
            Feature.AdvancedDepartments => "depts",
            Feature.LargeCompetitions => "contests",
            Feature.SecondCampus => "estate",
            Feature.Endowment => "fund",
            Feature.EliteInstitution => "elite",
            _ => f.ToString().ToLowerInvariant(),
        };

        // --- Inbox clipboard ------------------------------------------------------------------

        sealed class InboxRow
        {
            public VisualElement Root, Line;
            public InkBox Icon;
            public Label Sender, Subject, Note, Stamp;
            public int MessageId = -1;
        }

        readonly InboxRow[] _inboxRows = new InboxRow[3];
        readonly List<InboxMessage> _inboxPick = new List<InboxMessage>();

        void BuildInbox()
        {
            _inboxPill = _root.Q<Label>("inboxPill");
            _inboxEmpty = _root.Q<Label>("inboxEmpty");
            var host = _root.Q("inboxRows");
            for (int i = 0; i < _inboxRows.Length; i++)
            {
                var row = new InboxRow
                {
                    Line = new DashLine(),
                    Root = new VisualElement(),
                    Icon = new InkBox(),
                    Sender = NewLabel("msg-sender", "font-semi"),
                    Subject = NewLabel("msg-subject"),
                    Note = NewLabel("msg-note", "font-note"),
                    Stamp = NewLabel("stamp", "font-heavy"),
                };
                row.Line.AddToClassList("inbox-sep");
                row.Root.AddToClassList("inbox-row");
                row.Icon.AddToClassList("msg-icon");
                var envelope = new InkIcon("envelope");
                envelope.AddToClassList("icon-20");
                row.Icon.Add(envelope);
                var text = new VisualElement();
                text.AddToClassList("msg-text");
                text.Add(row.Sender);
                text.Add(row.Subject);
                text.Add(row.Note);
                row.Root.Add(row.Icon);
                row.Root.Add(text);
                row.Root.Add(row.Stamp);
                OnClick(row.Root, () =>
                {
                    if (row.MessageId >= 0) OpenMessage(row.MessageId);
                });
                host.Add(row.Line);
                host.Add(row.Root);
                _inboxRows[i] = row;
            }
        }

        void PickInbox()
        {
            // Decisions first (most pressing on top), then unread news.
            _inboxPick.Clear();
            foreach (var m in S.Inbox)
                if (m.NeedsDecision) _inboxPick.Add(m);
            _inboxPick.Sort(CompareDecisions);
            if (_inboxPick.Count > _inboxRows.Length) _inboxPick.RemoveRange(_inboxRows.Length, _inboxPick.Count - _inboxRows.Length);
            for (int i = S.Inbox.Count - 1; i >= 0 && _inboxPick.Count < _inboxRows.Length; i--)
            {
                var m = S.Inbox[i];
                if (!m.NeedsDecision && !m.Read) _inboxPick.Add(m);
            }
        }

        void ShowInbox()
        {
            int pending = _sim.Inbox.PendingDecisions;
            SetText(_inboxPill, pending > 0 ? $"{pending} need you" : "all done");
            _inboxPill.EnableInClassList("pill--coral", pending > 0);
            _inboxPill.EnableInClassList("pill--calm", pending == 0);
            _inboxEmpty.EnableInClassList("hidden", _inboxPick.Count > 0);
            int shown = Math.Min(_inboxPick.Count, InboxLimit);
            for (int i = 0; i < _inboxRows.Length; i++)
                ShowMessage(_inboxRows[i], i < shown ? _inboxPick[i] : null);
        }

        static int CompareDecisions(InboxMessage a, InboxMessage b)
        {
            int byPriority = b.Priority.CompareTo(a.Priority);
            if (byPriority != 0) return byPriority;
            int dueA = a.ExpiresDay < 0 ? int.MaxValue : a.ExpiresDay, dueB = b.ExpiresDay < 0 ? int.MaxValue : b.ExpiresDay;
            int byDue = dueA.CompareTo(dueB);
            return byDue != 0 ? byDue : b.ReceivedDay.CompareTo(a.ReceivedDay);
        }

        void ShowMessage(InboxRow row, InboxMessage m)
        {
            row.Root.EnableInClassList("hidden", m == null);
            row.Line.EnableInClassList("hidden", m == null);
            row.MessageId = m?.Id ?? -1;
            if (m == null) return;

            SetText(row.Sender, SenderLine(m));
            SetText(row.Subject, m.Subject);
            string note;
            bool due = false;
            if (m.NeedsDecision && m.ExpiresDay >= 0)
            {
                int left = m.ExpiresDay - S.Day;
                due = left <= 3 || m.Priority >= MessagePriority.Urgent;
                // It expires as its expiry day begins, so the day before is the last one to reply.
                note = left <= 1 ? "answer today!" : due ? $"answer by {MonthDay(m.ExpiresDay - 1)}!" : $"{left} days left";
            }
            else note = m.NeedsDecision ? "waiting for you" : S.Day == m.ReceivedDay ? "new today" : "new";
            SetText(row.Note, note);
            row.Note.EnableInClassList("msg-note--due", due);

            bool urgent = m.Priority >= MessagePriority.Urgent, important = m.Priority == MessagePriority.Important;
            row.Icon.EnableInClassList("msg-icon--urgent", urgent);
            row.Icon.EnableInClassList("msg-icon--important", important);
            SetText(row.Stamp, urgent ? "Urgent" : important ? "Important" : "");
            row.Stamp.EnableInClassList("hidden", !urgent && !important);
            row.Stamp.EnableInClassList("stamp--urgent", urgent);
            row.Stamp.EnableInClassList("stamp--important", important);
        }

        static string SenderLine(InboxMessage m)
        {
            string kind = m.Sender switch
            {
                SenderType.Parent => "parent",
                SenderType.Student => "student",
                SenderType.Teacher => "teacher",
                SenderType.Staff => "staff",
                SenderType.Admissions => "admissions",
                SenderType.Finance => "finance",
                SenderType.Sports => "sports",
                SenderType.Inspector => "inspector",
                SenderType.Sponsor => "sponsor",
                SenderType.Government => "board",
                SenderType.RivalSchool => "rival school",
                SenderType.Alumni => "alumni",
                SenderType.Assistant => "your assistant",
                SenderType.Press => "press",
                _ => null,
            };
            string name = m.SenderName ?? "";
            if (kind == null || name.IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0) return name;
            return name.Length == 0 ? kind : name + " · " + kind;
        }

        static string MonthDay(int day) => Calendar.MonthName(Calendar.Month(day)) + " " + Calendar.DayOfMonth(day);

        // --- Promises -------------------------------------------------------------------------

        sealed class PromiseRow
        {
            public VisualElement Root, BarHost, BarFill;
            public InkIcon Check;
            public Label Title, Days, Sub;
        }

        readonly PromiseRow[] _promiseRows = new PromiseRow[2];
        readonly List<Promise> _promisePick = new List<Promise>();

        void BuildPromises()
        {
            _promisesCard = _root.Q<InkBox>("promises");
            _promisesNote = _root.Q<Label>("promisesNote");
            var host = _root.Q("promiseRows");
            for (int i = 0; i < _promiseRows.Length; i++)
            {
                var row = new PromiseRow
                {
                    Root = new VisualElement(),
                    BarHost = new VisualElement(),
                    BarFill = new VisualElement(),
                    Check = new InkIcon("check"),
                    Title = NewLabel("promise-title"),
                    Days = NewLabel("promise-days", "font-heavy"),
                    Sub = NewLabel("promise-sub", "font-semi"),
                };
                row.Root.AddToClassList("promise");
                var box = new VisualElement();
                box.AddToClassList("promise-box");
                row.Check.AddToClassList("icon-13");
                box.Add(row.Check);
                var body = new VisualElement();
                body.AddToClassList("promise-body");
                var top = new VisualElement();
                top.AddToClassList("promise-top");
                top.Add(row.Title);
                top.Add(row.Days);
                row.BarHost.AddToClassList("bar");
                row.BarHost.AddToClassList("promise-bar");
                row.BarFill.AddToClassList("bar-fill");
                row.BarHost.Add(row.BarFill);
                body.Add(top);
                body.Add(row.BarHost);
                body.Add(row.Sub);
                row.Root.Add(box);
                row.Root.Add(body);
                OnClick(row.Root, () => ShowPageAt(HudPage.Report));   // the report lists every promise
                host.Add(row.Root);
                _promiseRows[i] = row;
            }
        }

        void PickPromises()
        {
            _promisePick.Clear();
            foreach (var p in S.Promises)
                if (!p.Resolved) _promisePick.Add(p);
            _promisePick.Sort((a, b) => a.DueDay.CompareTo(b.DueDay));
        }

        void ShowPromises()
        {
            int count = _promisePick.Count, shown = Math.Min(count, PromiseLimit);
            _promisesCard.EnableInClassList("hidden", count == 0);
            SetText(_promisesNote, shown == count ? "they remember" : shown == 0 ? $"{count} open" : $"+{count - shown} more");
            for (int i = 0; i < _promiseRows.Length; i++)
            {
                var row = _promiseRows[i];
                bool has = i < shown;
                row.Root.EnableInClassList("hidden", !has);
                if (has) ShowPromise(row, _promisePick[i]);
            }
        }

        void ShowPromise(PromiseRow row, Promise p)
        {
            bool met = _sim.Promises.IsMet(p);
            int left = Math.Max(0, p.DueDay - S.Day);
            bool late = !met && left <= 14;
            SetText(row.Title, p.Text);
            SetText(row.Days, met ? "on track" : left == 1 ? "1 day" : left + " days");
            row.Days.EnableInClassList("promise-days--late", late);
            row.Days.EnableInClassList("promise-days--done", met);
            row.Check.EnableInClassList("hidden", !met);

            float current = p.Kind switch
            {
                PromiseKind.FoodQualityAtLeast => S.Stat(StatId.FoodQuality),
                PromiseKind.DormQualityAtLeast => S.Stat(StatId.DormitoryQuality),
                PromiseKind.SafetyAtLeast => S.Stat(StatId.SafetyDiscipline),
                _ => -1f,
            };
            bool bar = current >= 0f && p.Target > 0f;
            row.BarHost.EnableInClassList("hidden", !bar);
            if (bar)
            {
                SetPercent(row.BarFill, 100f * current / p.Target);
                row.BarFill.EnableInClassList("bar-fill--low", late);
            }
            string state = bar ? $"{Mathf.RoundToInt(current)} now" : met ? "done, keep it" : "not yet";
            string to = PromisedTo(p);
            SetText(row.Sub, to == null ? state : state + " · to " + to);
        }

        string PromisedTo(Promise p)
        {
            switch (p.MadeTo)
            {
                case SenderType.Parent:
                    var student = S.FindStudent(p.StudentId);
                    return student != null ? $"the {student.LastName} family" : "the parents";
                case SenderType.Student:
                    return "the students";
                case SenderType.Teacher:
                case SenderType.Staff:
                    var staff = p.Kind == PromiseKind.SalaryReview ? S.FindStaff(p.TargetId) : null;
                    return staff != null ? staff.Name : "the staff";
                default:
                    return null;
            }
        }

        // --- Vitals ---------------------------------------------------------------------------

        sealed class VitalRow
        {
            public StatId Stat;
            public Label Value;
            public InkIcon Trend;
            public VisualElement Fill;
        }

        readonly VitalRow[] _vitalRows = new VitalRow[VitalDefs.Length];

        void BuildVitals()
        {
            var host = _root.Q("vitalRows");
            for (int i = 0; i < VitalDefs.Length; i++)
            {
                var def = VitalDefs[i];
                var row = new VisualElement();
                row.AddToClassList("vital");
                var icon = new InkIcon(def.icon);
                icon.AddToClassList("icon-18");
                icon.AddToClassList("vital-icon");
                var name = NewLabel("vital-name", "font-semi");
                name.text = def.name;
                var view = new VitalRow
                {
                    Stat = def.stat,
                    Value = NewLabel("vital-value", "font-hand"),
                    Trend = new InkIcon("steady"),
                    Fill = new VisualElement(),
                };
                view.Trend.AddToClassList("icon-13");
                view.Trend.AddToClassList("vital-trend");
                var bar = new VisualElement();
                bar.AddToClassList("vital-bar");
                view.Fill.AddToClassList("bar-fill");
                bar.Add(view.Fill);
                row.Add(icon);
                row.Add(name);
                row.Add(view.Value);
                row.Add(view.Trend);
                row.Add(bar);
                var stat = def.stat;
                OnClick(row, () =>
                {
                    CloseMore();
                    OpenStats(StatsMetric.ForStat(stat));
                });
                host.Add(row);
                _vitalRows[i] = view;
            }
        }

        void RefreshVitals()
        {
            foreach (var row in _vitalRows)
            {
                float value = S.Stat(row.Stat);
                bool low = value < 35f;
                SetText(row.Value, Mathf.RoundToInt(value).ToString());
                row.Value.EnableInClassList("vital-value--low", low);
                SetPercent(row.Fill, value);
                row.Fill.EnableInClassList("bar-fill--low", low);
                float delta = Delta(row.Stat);
                string trend = delta >= 1f ? "up" : delta <= -1f ? "down" : "steady";
                row.Trend.Icon = trend;
                row.Trend.EnableInClassList("icon--blue", trend == "up");
                row.Trend.EnableInClassList("icon--coral", trend == "down");
                row.Trend.EnableInClassList("icon--soft", trend == "steady");
            }
        }

        // --- Trends: a week of daily stat snapshots -------------------------------------------

        const int HistoryDays = 8;
        readonly float[][] _history = new float[HistoryDays][];
        readonly int[] _historyDay = { -1, -1, -1, -1, -1, -1, -1, -1 };

        void ResetHistory()
        {
            for (int i = 0; i < HistoryDays; i++) _historyDay[i] = -1;
        }

        void RecordHistory()
        {
            int day = S.Day, slot = day % HistoryDays;
            if (_historyDay[slot] == day) return;
            if (_history[slot] == null) _history[slot] = new float[GameState.StatCount];
            Array.Copy(S.Stats, _history[slot], GameState.StatCount);
            _historyDay[slot] = day;
        }

        /// <summary>Change over the last week (or as far back as we have).</summary>
        float Delta(StatId stat)
        {
            int day = S.Day;
            for (int back = HistoryDays - 1; back >= 1; back--)
            {
                int d = day - back;
                if (d < 0) continue;
                int slot = d % HistoryDays;
                if (_historyDay[slot] == d) return S.Stat(stat) - _history[slot][(int)stat];
            }
            return 0f;
        }

        // --- Toasts ---------------------------------------------------------------------------

        sealed class Toast
        {
            public VisualElement Root;
            public string Text;
            public float Until;
            public float RemoveAt = -1f;
        }

        const int MaxToasts = 3;
        const float ToastSeconds = 4.5f;
        readonly List<Toast> _toasts = new List<Toast>();
        bool _toastTilt;

        /// <summary>Shows a short note at the bottom of the screen. Tone: null, "bad", "gold" or "note".</summary>
        public void ShowToast(string text, string icon = "check", string tone = null)
        {
            if (!_built || !_visible || string.IsNullOrEmpty(text)) return;
            float now = GameClock.Now;
            int live = 0;
            foreach (var t in _toasts)
            {
                if (t.RemoveAt >= 0f) continue;
                if (t.Text == text)
                {
                    t.Until = now + ToastSeconds;   // same news again: keep it up a little longer
                    return;
                }
                live++;
            }
            // Too many at once (a busy day at 10x): the oldest go straight away, no fade.
            for (int i = 0; live >= MaxToasts && i < _toasts.Count; i++)
            {
                if (_toasts[i].RemoveAt >= 0f) continue;
                _toasts[i].Root.RemoveFromHierarchy();
                _toasts.RemoveAt(i--);
                live--;
            }

            GameAudio.Play(tone == "bad" ? Sfx.Error : Sfx.Pop);
            var box = new InkBox();
            box.AddToClassList("card");
            box.AddToClassList("toast");
            box.AddToClassList("toast--out");
            if (_toastTilt) box.AddToClassList("toast--b");
            _toastTilt = !_toastTilt;
            var badge = new InkBox();
            badge.AddToClassList("toast-icon");
            if (tone != null) badge.AddToClassList("toast-icon--" + tone);
            var glyph = new InkIcon(icon);
            glyph.AddToClassList("icon-17");
            badge.Add(glyph);
            var label = NewLabel("toast-text");
            label.text = text;
            var when = NewLabel("toast-when", "font-note");
            when.text = "just now";
            box.Add(badge);
            box.Add(label);
            box.Add(when);

            var toast = new Toast { Root = box, Text = text, Until = now + ToastSeconds };
            OnClick(box, () => Dismiss(toast));
            _toastLayer.Add(box);
            _toasts.Add(toast);
            box.schedule.Execute(() => box.RemoveFromClassList("toast--out")).StartingIn(30);
        }

        void Dismiss(Toast t)
        {
            if (t.RemoveAt >= 0f) return;
            t.RemoveAt = GameClock.Now + 0.35f;
            t.Root.AddToClassList("toast--out");
        }

        void UpdateToasts()
        {
            float now = GameClock.Now;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                if (t.RemoveAt < 0f)
                {
                    if (now >= t.Until) Dismiss(t);
                }
                else if (now >= t.RemoveAt)
                {
                    t.Root.RemoveFromHierarchy();
                    _toasts.RemoveAt(i);
                }
            }
        }

        void ClearToasts()
        {
            foreach (var t in _toasts) t.Root.RemoveFromHierarchy();
            _toasts.Clear();
        }

        void OnDebugToast(string message, bool ok) => ShowToast(message, ok ? "check" : "alert", ok ? null : "bad");

        void OnMessageArrived(InboxMessage m)
        {
            // News without a decision shows up in the clipboard; important news is also logged (star toast).
            if (m == null || !m.NeedsDecision || _sim == null) return;
            if (m.TemplateId == "tutorial_choice") return;   // the tutorial bubble already says so
            if (m.Sender == SenderType.Assistant)
            {
                int id = m.Id;
                AssistantSays(m.Subject + ". It's in your inbox.", "Open", () => OpenMessage(id));
                return;
            }
            ShowToast("New message: " + m.Subject, "envelope", m.Priority >= MessagePriority.Urgent ? "bad" : "note");
        }

        void OpenMessage(int id)
        {
            CloseMore();
            _inboxFocus = id;
            ShowPageAt(HudPage.Inbox);
            _sim?.TutorialSignal("open:inbox");
        }

        void OnLogged(LogEntry entry)
        {
            if (entry != null && entry.Important) ShowToast(entry.Text, "star", "gold");
        }

        // --- Helpers --------------------------------------------------------------------------

        static Label NewLabel(params string[] classes)
        {
            var label = new Label();
            foreach (var c in classes) label.AddToClassList(c);
            return label;
        }

        static void SetText(Label label, string text)
        {
            text ??= "";
            if (label != null && label.text != text) label.text = text;
        }

        /// <summary>
        /// Sets text meant for the handwriting font, but falls back to Playpen Sans when it contains a
        /// capital G or a lowercase z, which read as 6 and 3 in Gochi Hand.
        /// </summary>
        static void SetHandText(Label label, string text)
        {
            if (label == null) return;
            text ??= "";
            bool plain = text.IndexOf('G') >= 0 || text.IndexOf('z') >= 0;
            label.EnableInClassList("font-hand", !plain);
            label.EnableInClassList("font-heavy", plain);
            if (label.text != text) label.text = text;
        }

        /// <summary>Sets a bar's fill width, skipping the layout pass when nothing changed.</summary>
        static void SetPercent(VisualElement fill, float percent)
        {
            if (fill == null) return;
            percent = Mathf.Round(Mathf.Clamp(percent, 0f, 100f) * 2f) / 2f;
            if (fill.userData is float old && Mathf.Approximately(old, percent)) return;
            fill.userData = percent;
            fill.style.width = new Length(percent, LengthUnit.Percent);
        }

        static string Money(float v) => (v < 0f ? "–" : "") + "$" + Mathf.Abs(Mathf.Round(v)).ToString("N0");

        /// <summary>
        /// A money label for a chart axis, as precise as the chart's range needs: "$823k" across
        /// hundreds of thousands, "$89.5k" across a few thousand, "$89,365" across a few hundred.
        /// </summary>
        static string AxisMoney(float v, float span)
        {
            float a = Mathf.Abs(v);
            string sign = v < -0.5f ? "–" : "";
            if (a >= 1_000_000f && span >= 300_000f) return $"{sign}${a / 1_000_000f:0.#}M";
            if (a >= 10_000f && span >= 30_000f) return $"{sign}${a / 1000f:0}k";
            if (a >= 10_000f && span >= 3_000f) return $"{sign}${a / 1000f:0.#}k";
            return sign + "$" + Mathf.Round(a).ToString("N0");
        }

        /// <summary>Hides a chart's middle axis label when it would just repeat the top or bottom one.</summary>
        static void DedupeAxis(Label top, Label mid, Label bottom)
        {
            if (mid == null) return;
            Show(mid, mid.text != top?.text && mid.text != bottom?.text);
        }

        static string Signed(float v) => (v < 0f ? "–" : "+") + "$" + Mathf.Abs(Mathf.Round(v)).ToString("N0");
    }
}
