using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The inbox page: a clipboard of letters (decisions first), the open letter on lined paper
    /// (GameHud.Inbox.Letter.cs) and the reply cards with their consequences and the assistant's
    /// advice (GameHud.Inbox.Reply.cs).
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>One letter on the clipboard.</summary>
        sealed class IbxRow
        {
            public InboxMessage Message;
            public InkBox Root;
            public VisualElement Dot;
            public Label Note;
        }

        const int IbxFiledShown = 60;            // finished letters listed before "show older"
        const string IbxExpired = "expired";     // the option id the sim records when nobody replied

        readonly List<IbxRow> _ibxRows = new List<IbxRow>();
        readonly List<InboxMessage> _ibxOpen = new List<InboxMessage>(), _ibxFiled = new List<InboxMessage>();
        int _ibxSelected = -1, _ibxFilter = 1, _ibxListSig;     // filter: 0 = to reply, 1 = all
        bool _ibxAuto, _ibxShowOlder, _ibxOpened, _ibxNotesStale;   // opened: built, not refreshed yet
        ScrollView _ibxList;
        Label _ibxCount, _ibxToReply, _ibxAll, _ibxLockText;
        VisualElement _ibxMarkRead, _ibxLockNote;

        partial void ResetInboxPage()
        {
            _ibxSelected = -1;
            _ibxFilter = 1;
            _ibxAuto = false;
            _ibxShowOlder = false;
            _ibxPicked = null;
        }

        partial void BuildInboxPage()
        {
            IbxIcons();
            _ibxRows.Clear();
            _ibxOpened = true;

            var board = Ink("ibx-board");
            var clip = Box("ibx-clip");
            clip.Add(Box("ibx-clip-slot"));
            board.Add(clip);

            var head = Box("ibx-board-head");
            head.Add(Text("Inbox", "font-hand", "ibx-board-title"));
            _ibxCount = Text("", "font-note", "ibx-board-note");
            head.Add(_ibxCount);
            board.Add(head);

            var bar = Box("ibx-bar");
            var pills = Pills(new[] { "To reply", "All" }, () => _ibxFilter, i =>
            {
                _ibxFilter = i;
                _ibxShowOlder = false;
                RefreshInboxPage(false);
            });
            pills.AddToClassList("ibx-filter");
            _ibxToReply = (Label)pills[0];
            _ibxAll = (Label)pills[1];
            bar.Add(pills);
            _ibxMarkRead = Box("ibx-link");
            _ibxMarkRead.Add(Icon("check", "icon-17", "icon--blue"));
            _ibxMarkRead.Add(Text("mark all read", "font-note", "ibx-link-label"));
            OnClick(_ibxMarkRead, () =>
            {
                _sim.Inbox.MarkAllRead();
                _ibxNotesStale = true;
                RefreshInboxPage(false);
            });
            bar.Add(_ibxMarkRead);
            board.Add(bar);

            _ibxList = new ScrollView(ScrollViewMode.Vertical);
            _ibxList.AddToClassList("ibx-list");
            board.Add(_ibxList);

            _ibxLockNote = Ink("ibx-lock-note");
            _ibxLockNote.Add(Icon("lock", "icon-18", "icon--soft"));
            _ibxLockText = Text("", "font-semi", "ibx-lock-text");
            _ibxLockNote.Add(_ibxLockText);
            board.Add(_ibxLockNote);
            _pageHost.Add(board);

            IbxBuildLetter();
            IbxBuildReply();
        }

        partial void RefreshInboxPage(bool newDay)
        {
            if (_ibxList == null || _sim == null) return;
            bool opened = _ibxOpened;
            if (opened)
            {
                // Back on the page: a letter still waiting for an answer stays open, otherwise start at the top.
                _ibxOpened = false;
                if (S.FindMessage(_ibxSelected)?.NeedsDecision != true) _ibxSelected = -1;
            }
            if (_inboxFocus >= 0)
            {
                var focus = S.FindMessage(_inboxFocus);
                _inboxFocus = -1;
                if (focus != null)
                {
                    if (!focus.NeedsDecision) _ibxFilter = 1;
                    IbxSelect(focus, auto: false);
                }
            }

            // The list only changes shape when letters come, go or get answered; days left update in place.
            int sig = IbxListSignature();
            bool rebuild = opened || sig != _ibxListSig;
            if (rebuild)
            {
                _ibxListSig = sig;
                IbxSort();
                IbxBuildRows();
            }
            var current = IbxCurrent();
            IbxUpdateRows(rebuild || newDay || _ibxNotesStale);
            _ibxNotesStale = false;
            IbxRefreshHead();
            IbxRefreshLetter(current, newDay);
            IbxRefreshReply(current, newDay);
        }

        /// <summary>Changes when a letter arrives, is answered or leaves the archive, or the filter changes.</summary>
        int IbxListSignature()
        {
            int h = 17 + _ibxFilter * 7 + (_ibxShowOlder ? 3 : 0);
            foreach (var m in S.Inbox) h = h * 31 + (m.NeedsDecision ? m.Id : ~m.Id);
            return h;
        }

        /// <summary>Letters that need an answer, most pressing first; then everything else, newest first.</summary>
        void IbxSort()
        {
            _ibxOpen.Clear();
            _ibxFiled.Clear();
            foreach (var m in S.Inbox) (m.NeedsDecision ? _ibxOpen : _ibxFiled).Add(m);
            _ibxOpen.Sort(CompareDecisions);
            _ibxFiled.Sort((a, b) => a.ReceivedDay != b.ReceivedDay ? b.ReceivedDay.CompareTo(a.ReceivedDay) : b.Id.CompareTo(a.Id));
        }

        /// <summary>
        /// The open letter. Until the player picks one it is the most pressing letter, and a letter
        /// that needs an answer takes over from news that was only opened for them.
        /// </summary>
        InboxMessage IbxCurrent()
        {
            var m = _ibxSelected >= 0 ? S.FindMessage(_ibxSelected) : null;
            if (m != null && !(_ibxAuto && !m.NeedsDecision && _ibxOpen.Count > 0)) return m;
            var top = _ibxOpen.Count > 0 ? _ibxOpen[0] : _ibxFilter == 1 && _ibxFiled.Count > 0 ? _ibxFiled[0] : null;
            if (top == null)
            {
                _ibxSelected = -1;
                return null;
            }
            IbxSelect(top, auto: true);
            return top;
        }

        void IbxSelect(InboxMessage m, bool auto)
        {
            if (m.Id != _ibxSelected)
            {
                _ibxPicked = null;
                _ibxLetterScroll.scrollOffset = Vector2.zero;
                _ibxReplyScroll.scrollOffset = Vector2.zero;
            }
            _ibxSelected = m.Id;
            _ibxAuto = auto;
            m.Read = true;   // opening a letter reads it
            _ibxNotesStale = true;
        }

        // --- Clipboard ----------------------------------------------------------------------------

        void IbxBuildRows()
        {
            _ibxList.Clear();
            _ibxRows.Clear();
            bool all = _ibxFilter == 1, both = _ibxOpen.Count > 0 && _ibxFiled.Count > 0;
            if (all && both) _ibxList.Add(IbxGroup("to reply"));
            for (int i = 0; i < _ibxOpen.Count; i++) IbxAddRow(_ibxOpen[i], i > 0);
            if (all)
            {
                if (both) _ibxList.Add(IbxGroup("everything else"));
                int shown = _ibxShowOlder ? _ibxFiled.Count : Math.Min(_ibxFiled.Count, IbxFiledShown);
                for (int i = 0; i < shown; i++) IbxAddRow(_ibxFiled[i], i > 0);
                int older = _ibxFiled.Count - shown;
                if (older > 0)
                {
                    var more = Text(older == 1 ? "show 1 older letter" : $"show {older} older letters", "font-note", "ibx-more");
                    OnClick(more, () =>
                    {
                        _ibxShowOlder = true;
                        RefreshInboxPage(false);
                    });
                    _ibxList.Add(more);
                }
            }
            if (_ibxRows.Count == 0)
                _ibxList.Add(Text(S.Inbox.Count == 0 ? "No letters yet. The post comes every day." : "Nothing needs a reply. Enjoy the quiet!",
                    "font-note", "empty-note"));
        }

        static VisualElement IbxGroup(string title)
        {
            var group = Box("ibx-group");
            group.Add(Text(title, "font-note", "ibx-group-label"));
            var line = new DashLine();
            line.AddToClassList("ibx-group-line");
            group.Add(line);
            return group;
        }

        void IbxAddRow(InboxMessage m, bool line)
        {
            if (line)
            {
                var sep = new DashLine();
                sep.AddToClassList("ibx-sep");
                _ibxList.Add(sep);
            }
            bool open = m.NeedsDecision;
            var row = new IbxRow { Message = m, Root = Ink("ibx-row") };
            row.Root.EnableInClassList("ibx-row--past", !open);

            var badge = Ink("ibx-badge");
            if (open)
            {
                badge.EnableInClassList("ibx-badge--urgent", m.Priority >= MessagePriority.Urgent);
                badge.EnableInClassList("ibx-badge--important", m.Priority == MessagePriority.Important);
                badge.Add(Icon(IbxSenderIcon(m.Sender), "icon-20"));
            }
            else
            {
                badge.AddToClassList("ibx-badge--past");
                string icon = !m.Resolved ? IbxSenderIcon(m.Sender) : m.ChosenOptionId == IbxExpired ? "close" : "check";
                badge.Add(Icon(icon, "icon-18", "icon--soft"));
            }
            row.Dot = Box("ibx-dot");
            badge.Add(row.Dot);
            row.Root.Add(badge);

            var text = Box("ibx-row-text");
            text.Add(Text(SenderLine(m), "font-semi", "ibx-row-sender"));
            text.Add(Text(m.Subject, "ibx-row-subject"));
            var foot = Box("ibx-row-foot");
            row.Note = Text("", "font-note", "ibx-row-note");
            foot.Add(row.Note);
            if (open && m.Priority >= MessagePriority.Important)
            {
                bool urgent = m.Priority >= MessagePriority.Urgent;
                foot.Add(Text(urgent ? "Urgent" : "Important", "font-heavy", "stamp", urgent ? "stamp--urgent" : "stamp--important"));
            }
            text.Add(foot);
            row.Root.Add(text);

            OnClick(row.Root, () =>
            {
                IbxSelect(m, auto: false);
                RefreshInboxPage(false);
            });
            _ibxRows.Add(row);
            _ibxList.Add(row.Root);
        }

        /// <summary>Selection and unread marks every refresh; the notes only when the day or a letter's state moved on.</summary>
        void IbxUpdateRows(bool notes)
        {
            foreach (var row in _ibxRows)
            {
                var m = row.Message;
                row.Root.EnableInClassList("ibx-row--on", m.Id == _ibxSelected);
                row.Root.EnableInClassList("ibx-row--unread", !m.Read);
                Show(row.Dot, !m.Read);
                if (!notes) continue;
                SetText(row.Note, IbxRowNote(m, out bool due));
                row.Note.EnableInClassList("ibx-row-note--due", due);
            }
        }

        /// <summary>The clipboard's third line: time left to answer, or when it came and how it ended.</summary>
        string IbxRowNote(InboxMessage m, out bool due)
        {
            due = false;
            if (m.NeedsDecision)
            {
                if (m.ExpiresDay < 0) return "waiting for you";
                due = IbxDaysLeft(m) <= 3 || m.Priority >= MessagePriority.Urgent;
                return IbxLeftText(m);
            }
            string when = IbxAgo(m.ReceivedDay);
            if (!m.Resolved) return m.Read ? when : when + " · new";
            if (m.ChosenOptionId == IbxExpired) return when + " · no reply";
            return when + (m.HandledBy != null ? " · handled by " + m.HandledBy : " · answered");
        }

        void IbxRefreshHead()
        {
            int pending = _ibxOpen.Count;
            SetText(_ibxCount, pending == 0 ? "all caught up!" : pending == 1 ? "1 needs a reply" : $"{pending} need a reply");
            SetText(_ibxToReply, pending > 0 ? $"To reply · {pending}" : "To reply");
            SetText(_ibxAll, $"All · {S.Inbox.Count}");
            _ibxToReply.EnableInClassList("pill-btn--on", _ibxFilter == 0);
            _ibxAll.EnableInClassList("pill-btn--on", _ibxFilter == 1);

            bool unread = false;
            foreach (var m in S.Inbox)
                if (!m.Read && !m.NeedsDecision)
                {
                    unread = true;
                    break;
                }
            Show(_ibxMarkRead, unread);

            bool locked = !_sim.IsUnlocked(Feature.Administrators);
            Show(_ibxLockNote, locked);
            if (locked)
                SetText(_ibxLockText, $"At school level {Balance.UnlockLevel(Feature.Administrators)} your directors can answer routine mail for you.");
        }

        // --- Shared bits ----------------------------------------------------------------------------

        /// <summary>Days left to answer, counting today: a letter expires as its expiry day begins.</summary>
        int IbxDaysLeft(InboxMessage m) => m.ExpiresDay - S.Day;

        string IbxLeftText(InboxMessage m)
        {
            int left = IbxDaysLeft(m);
            return left <= 1 ? "last day!" : left + " days left";
        }

        /// <summary>The last day a reply still counts.</summary>
        string IbxAnswerBy(InboxMessage m) => IbxDate(Math.Max(m.ReceivedDay, m.ExpiresDay - 1));

        string IbxAgo(int day)
        {
            int ago = S.Day - day;
            return ago <= 0 ? "today" : ago == 1 ? "yesterday" : ago < 30 ? ago + " days ago" : IbxDate(day);
        }

        /// <summary>"Oct 8" this school year, "Y1 Oct 8" before it.</summary>
        string IbxDate(int day) => Calendar.Year(day) == Calendar.Year(S.Day) ? MonthDay(day) : Calendar.Format(day);

        static string IbxSenderIcon(SenderType t) => t switch
        {
            SenderType.Parent => "ibx-home",
            SenderType.Student => "students",
            SenderType.Teacher => "board",
            SenderType.Staff => "mug",
            SenderType.Admissions => "letters",
            SenderType.Finance => "coin",
            SenderType.Sports => "trophy",
            SenderType.Inspector => "clipboard",
            SenderType.Sponsor => "sparkle",
            SenderType.Government => "ibx-hall",
            SenderType.RivalSchool => "podium",
            SenderType.StudentAffairs => "shield",
            SenderType.Facilities => "gear",
            SenderType.Alumni => "ibx-cap",
            SenderType.Assistant => "speech",
            SenderType.Press => "news",
            _ => "envelope",
        };

        /// <summary>Who the sender is, in a few words.</summary>
        static string IbxKind(SenderType t) => t switch
        {
            SenderType.Parent => "parent",
            SenderType.Student => "student",
            SenderType.Teacher => "teacher",
            SenderType.Staff => "staff",
            SenderType.Admissions => "admissions office",
            SenderType.Finance => "finance",
            SenderType.Sports => "athletics",
            SenderType.Inspector => "inspector",
            SenderType.Sponsor => "sponsor",
            SenderType.Government => "the board",
            SenderType.RivalSchool => "rival school",
            SenderType.StudentAffairs => "student affairs",
            SenderType.Facilities => "facilities",
            SenderType.Alumni => "former student",
            SenderType.Assistant => "your assistant",
            SenderType.Press => "the press",
            _ => t.ToString().ToLowerInvariant(),
        };

        /// <summary>The page's own glyphs: a home for parents, the board's hall, a graduate's cap, a paper plane.</summary>
        static void IbxIcons()
        {
            InkIcons.Define("ibx-home", 1.8f,
                "fill:#FBFDFF M6 9.5v10h12v-10",
                "M4 11l8-6.5 8 6.5",
                "fill:#A8D1F7 M12 17.4s-2.6-1.5-2.6-3.3c0-.8.6-1.4 1.3-1.4.6 0 1 .3 1.3.7.3-.4.7-.7 1.3-.7.7 0 1.3.6 1.3 1.4 0 1.8-2.6 3.3-2.6 3.3z");
            InkIcons.Define("ibx-hall", 1.8f,
                "fill:#DCEBFB M3.5 9.2L12 4.2l8.5 5z",
                "M6 11.5v6.5M10 11.5v6.5M14 11.5v6.5M18 11.5v6.5",
                "M3.5 20.5h17");
            InkIcons.Define("ibx-cap", 1.8f,
                "fill:#A8D1F7 M2.5 9.5L12 5l9.5 4.5L12 14z",
                "M6.5 11.6v4.4c1.6 1.4 3.4 2 5.5 2s3.9-.6 5.5-2v-4.4",
                "M21.5 9.5v5.5");
            InkIcons.Define("ibx-send", 2.2f, "M20.5 3.5L3.5 10.5l7 3 3 7z", "M20.5 3.5l-10 10");
        }
    }
}
