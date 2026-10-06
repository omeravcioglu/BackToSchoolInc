using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The inbox's reply column: each option as a card with its price, what it visibly does and,
    /// with enough director experience, its hidden consequence; the assistant's advice on a sticky
    /// note; who else could answer letters like this; and once answered, what happened.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>A reply card. Id is null when the option can't be chosen.</summary>
        sealed class IbxCard
        {
            public string Id;
            public InkBox Root, Radio;
            public InkIcon Mark;
        }

        readonly List<IbxCard> _ibxCards = new List<IbxCard>();
        InkBox _ibxSendButton, _ibxNextButton;
        ScrollView _ibxReplyScroll;
        VisualElement _ibxReplyList;
        Label _ibxReplyTitle, _ibxReplyNote, _ibxLaterLabel;
        InkIcon _ibxReplyIcon;
        string _ibxPicked;
        int _ibxReplySig;

        void IbxBuildReply()
        {
            _ibxCards.Clear();
            var reply = Ink("ibx-reply");
            var head = Box("ibx-reply-head");
            _ibxReplyTitle = Text("", "font-hand", "ibx-reply-title");
            head.Add(_ibxReplyTitle);
            var note = Box("ibx-reply-note");
            _ibxReplyIcon = Icon("pencil", "icon-17", "icon--soft");
            note.Add(_ibxReplyIcon);
            _ibxReplyNote = Text("", "font-note", "ibx-reply-note-text");
            note.Add(_ibxReplyNote);
            head.Add(note);
            reply.Add(head);

            _ibxReplyScroll = new ScrollView(ScrollViewMode.Vertical);
            _ibxReplyScroll.AddToClassList("ibx-reply-scroll");
            _ibxReplyList = Box("ibx-reply-list");
            _ibxReplyScroll.Add(_ibxReplyList);
            reply.Add(_ibxReplyScroll);

            var foot = Box("ibx-reply-foot");
            var later = Button(null, "Decide later", CloseSheet, small: true);
            _ibxLaterLabel = later.Q<Label>();
            foot.Add(later);
            _ibxSendButton = Button("ibx-send", "Send reply", IbxSend, primary: true, small: true);
            _ibxSendButton.AddToClassList("ibx-go");
            foot.Add(_ibxSendButton);
            _ibxNextButton = Button("next", "Next letter", IbxNext, primary: true, small: true);
            _ibxNextButton.AddToClassList("ibx-go");
            foot.Add(_ibxNextButton);
            reply.Add(foot);
            _pageHost.Add(reply);
        }

        void IbxRefreshReply(InboxMessage m, bool newDay)
        {
            int sig = IbxReplySignature(m);
            if (newDay || sig != _ibxReplySig)
            {
                _ibxReplySig = sig;
                IbxFillReply(m);
            }
            IbxUpdateReply(m);
        }

        /// <summary>Changes with the letter, its answer, what each option needs, the director's insight and the helpers.</summary>
        int IbxReplySignature(InboxMessage m)
        {
            if (m == null) return 1;
            int h = m.Id * 31 + (m.Resolved ? 7 : 3);
            h = h * 31 + (m.ChosenOptionId?.GetHashCode() ?? 0);
            h = h * 31 + (_sim.Progression.ShowHints ? 1 : 2);
            foreach (var o in m.Options) h = h * 31 + (_sim.Inbox.WhyNot(m, o)?.GetHashCode() ?? 5);
            var a = S.Assistant;
            h = h * 31 + (a == null ? 0 : a.Look * 8 + (int)a.Style + 1);
            var template = _sim.Events.Get(m.TemplateId);
            if (template != null && template.Handler != AdminRole.None)
                h = h * 31 + (_sim.FindAdmin(template.Handler)?.Id ?? -1) * 2 + (_sim.IsUnlocked(Feature.Administrators) ? 1 : 0);
            return h;
        }

        void IbxFillReply(InboxMessage m)
        {
            _ibxReplyList.Clear();
            _ibxCards.Clear();
            if (m == null)
            {
                IbxReplyHead("All caught up", "check", "nothing is waiting for you");
                IbxCalm("envelope", "No letters to answer", "Enjoy the quiet while it lasts.");
            }
            else if (m.NeedsDecision) IbxFillOptions(m);
            else if (m.Resolved) IbxFillOutcome(m);
            else
            {
                IbxReplyHead("Nothing to answer", "check", "just keeping you posted");
                IbxCalm("check", "No reply needed", "Read it, nod, and carry on.");
            }
        }

        void IbxReplyHead(string title, string icon, string note)
        {
            SetText(_ibxReplyTitle, title);
            _ibxReplyIcon.Icon = icon;
            SetText(_ibxReplyNote, note);
        }

        void IbxCalm(string icon, string title, string text)
        {
            var calm = Box("ibx-calm");
            var badge = Ink("ibx-calm-badge");
            badge.Add(Icon(icon, "ibx-calm-icon"));
            calm.Add(badge);
            calm.Add(Text(title, "font-hand", "ibx-calm-title"));
            calm.Add(Text(text, "font-note", "ibx-calm-text"));
            _ibxReplyList.Add(calm);
        }

        // --- Choosing ---------------------------------------------------------------------------

        void IbxFillOptions(InboxMessage m)
        {
            bool hints = false, insight = _sim.Progression.ShowHints;
            foreach (var o in m.Options) hints |= !string.IsNullOrEmpty(o.Hint);
            if (hints && !insight) IbxReplyHead("Your reply", "lock", $"hidden consequences show at Director Lv {Balance.DirectorInsightLevel}");
            else if (hints) IbxReplyHead("Your reply", "eye", "you can sense hidden consequences");
            else IbxReplyHead("Your reply", "pencil", "pick one, then send it");

            var picked = _ibxPicked != null ? m.FindOption(_ibxPicked) : null;
            if (picked == null || _sim.Inbox.WhyNot(m, picked) != null) _ibxPicked = null;

            for (int i = 0; i < m.Options.Count; i++) IbxAddOption(m, m.Options[i], i, insight);
            var a = S.Assistant;
            if (a != null && !string.IsNullOrEmpty(m.AdvisorNote)) IbxAddAdvice(a, m.AdvisorNote);
            IbxAddAdminLine(m);
        }

        void IbxAddOption(InboxMessage m, MessageOption o, int index, bool insight)
        {
            string why = _sim.Inbox.WhyNot(m, o);
            var card = new IbxCard { Id = why == null ? o.Id : null, Root = Ink("ibx-option"), Radio = Ink("ibx-radio") };
            card.Root.EnableInClassList("ibx-option--b", index % 2 == 1);
            card.Root.EnableInClassList("ibx-option--off", why != null);
            card.Mark = Icon(why != null ? "lock" : "check", "icon-17", why != null ? "icon--soft" : "icon--white");
            card.Radio.Add(card.Mark);
            card.Root.Add(card.Radio);

            var body = Box("ibx-option-body");
            var top = Box("ibx-option-top");
            string cost = IbxCost(o, out string label);
            top.Add(Text(label, "font-heavy", "ibx-option-label"));
            if (cost != null) top.Add(Text(cost, "font-heavy", "ibx-cost"));
            body.Add(top);
            if (!string.IsNullOrEmpty(o.Preview)) body.Add(Text(o.Preview, "font-semi", "ibx-option-preview"));
            if (!string.IsNullOrEmpty(o.Hint))
            {
                var hint = Box("ibx-hint");
                hint.EnableInClassList("ibx-hint--open", insight);
                hint.Add(Icon(insight ? "eye" : "lock", "icon-17", insight ? "icon--blue" : "icon--soft"));
                hint.Add(Text(insight ? o.Hint : $"hidden consequence (Director Lv {Balance.DirectorInsightLevel})", "font-note", "ibx-hint-text"));
                body.Add(hint);
            }
            if (why != null)
            {
                var reason = Box("ibx-why");
                reason.Add(Icon("alert", "icon-17", "icon--coral"));
                reason.Add(Text(why, "font-heavy", "ibx-why-text"));
                body.Add(reason);
            }
            card.Root.Add(body);
            if (o.Id == m.AdvisorOptionId) card.Root.Add(IbxPickTag());
            if (card.Id != null)
            {
                string id = card.Id;
                OnClick(card.Root, () =>
                {
                    _ibxPicked = id;
                    IbxUpdateReply(S.FindMessage(_ibxSelected));
                });
            }
            _ibxCards.Add(card);
            _ibxReplyList.Add(card.Root);
        }

        /// <summary>
        /// The option's label and its price chip. Labels usually end with their price, "(… $2,000)",
        /// which moves onto the chip; a label that mentions money anywhere else keeps it and gets no chip.
        /// </summary>
        static string IbxCost(MessageOption o, out string label)
        {
            label = o.Label ?? "";
            if (o.Cost <= 0f) return null;
            string cost = Money(o.Cost), tail = " (" + cost + ")";
            if (label.EndsWith(tail, StringComparison.Ordinal))
            {
                label = label.Substring(0, label.Length - tail.Length);
                return cost;
            }
            return label.IndexOf('$') >= 0 ? null : cost;
        }

        VisualElement IbxPickTag()
        {
            var tag = Ink("ibx-pick");
            tag.Add(Icon("star", "icon-17"));
            var a = S.Assistant;
            tag.Add(Text(a != null ? $"{a.FirstName}'s pick" : "advised", "font-note", "ibx-pick-text"));
            return tag;
        }

        void IbxAddAdvice(Assistant a, string advice)
        {
            var note = Ink("ibx-advice");
            note.Add(IbxHelperFace(a, "ibx-advice-face"));
            var body = Box("ibx-advice-body");
            var title = Text("", "font-hand", "ibx-advice-title");
            SetHandText(title, $"{a.FirstName} says");
            body.Add(title);
            body.Add(Text(advice, "font-semi", "ibx-advice-text"));
            note.Add(body);
            note.Add(TapeStrip("ibx-advice-tape", "tape-sky"));
            _ibxReplyList.Add(note);
        }

        static InkBox IbxHelperFace(Assistant a, string frame)
        {
            var face = Ink(frame);
            var portrait = new Portrait();
            portrait.AddToClassList("ibx-advice-portrait");
            portrait.Show(a.Style, a.Look);
            face.Add(portrait);
            return face;
        }

        /// <summary>Who could answer letters like this one so the director doesn't have to.</summary>
        void IbxAddAdminLine(InboxMessage m)
        {
            var template = _sim.Events.Get(m.TemplateId);
            if (template == null || template.Handler == AdminRole.None) return;
            var admin = _sim.FindAdmin(template.Handler);
            string text;
            if (admin != null) text = $"{admin.Title} {admin.Name} may answer this for you if it falls within your policies.";
            else
            {
                string role = Names.AdminRoleTitle(template.Handler);
                bool vowel = role.StartsWith("HR", StringComparison.Ordinal) || "AEIOU".IndexOf(role[0]) >= 0;
                text = $"{(vowel ? "An" : "A")} {role} could handle letters like this automatically.";
                if (!_sim.IsUnlocked(Feature.Administrators)) text += $" You can hire one from school level {Balance.UnlockLevel(Feature.Administrators)}.";
            }
            var row = Ink("ibx-admin");
            row.Add(Icon("staff", "icon-18", "icon--soft"));
            row.Add(Text(text, "font-semi", "ibx-admin-text"));
            _ibxReplyList.Add(row);
        }

        // --- Answered -----------------------------------------------------------------------------

        void IbxFillOutcome(InboxMessage m)
        {
            bool late = m.ChosenOptionId == IbxExpired;
            var chosen = late ? null : m.FindOption(m.ChosenOptionId);
            if (late) IbxReplyHead("No reply", "alert", "the deadline passed");
            else IbxReplyHead("Answered", "check", m.HandledBy != null ? "handled for you" : "your answer is in the post");

            var card = Ink("ibx-outcome");
            var stamp = Text(late ? "NO REPLY" : m.HandledBy != null ? "HANDLED" : "SENT", "font-heavy", "ibx-outcome-stamp");
            stamp.EnableInClassList("ibx-outcome-stamp--late", late);
            card.Add(stamp);
            if (late) card.Add(Text("You never replied.", "font-heavy", "ibx-outcome-choice", "ibx-outcome-choice--late"));
            else
            {
                card.Add(Text(m.HandledBy != null ? $"{m.HandledBy} replied" : "You replied", "font-note", "ibx-outcome-label"));
                if (chosen != null) card.Add(Text(chosen.Label, "font-heavy", "ibx-outcome-choice"));
            }
            if (!string.IsNullOrEmpty(m.ResolutionText))
            {
                card.Add(Text("what happened", "font-note", "ibx-outcome-label"));
                card.Add(Text(m.ResolutionText, "font-semi", "ibx-outcome-text"));
            }
            _ibxReplyList.Add(card);

            // Looking back at the assistant's pick.
            var a = S.Assistant;
            var advised = m.AdvisorOptionId != null ? m.FindOption(m.AdvisorOptionId) : null;
            if (a == null || advised == null) return;
            var row = Box("ibx-agree");
            row.Add(IbxHelperFace(a, "ibx-agree-face"));
            row.Add(Text(advised == chosen ? $"{a.FirstName} agrees!" : $"{a.FirstName} had suggested \"{advised.Label}\".", "font-note", "ibx-agree-text"));
            _ibxReplyList.Add(row);
        }

        // --- Buttons ------------------------------------------------------------------------------

        /// <summary>Cheap per-refresh state: which card is picked and which buttons make sense.</summary>
        void IbxUpdateReply(InboxMessage m)
        {
            bool open = m != null && m.NeedsDecision;
            foreach (var card in _ibxCards)
            {
                bool on = card.Id != null && card.Id == _ibxPicked;
                card.Root.EnableInClassList("ibx-option--on", on);
                card.Radio.EnableInClassList("ibx-radio--on", on);
                if (card.Id != null) Show(card.Mark, on);
            }
            SetText(_ibxLaterLabel, open ? "Decide later" : "Close");
            Show(_ibxSendButton, open);
            _ibxSendButton.EnableInClassList("btn--disabled", _ibxPicked == null);
            Show(_ibxNextButton, !open && IbxNextLetter() != null);
        }

        /// <summary>The most pressing other letter that still needs an answer.</summary>
        InboxMessage IbxNextLetter()
        {
            foreach (var m in _ibxOpen)
                if (m.Id != _ibxSelected && m.NeedsDecision) return m;
            return null;
        }

        void IbxNext()
        {
            var next = IbxNextLetter();
            if (next == null) return;
            IbxSelect(next, auto: false);
            RefreshInboxPage(false);
        }

        void IbxSend()
        {
            var m = S.FindMessage(_ibxSelected);
            if (m == null || !m.NeedsDecision) return;
            if (_ibxPicked == null)
            {
                ShowToast("Pick a reply first.", "pencil", "note");
                return;
            }
            int id = m.Id;
            string option = _ibxPicked;
            _ibxPicked = null;
            _ibxAuto = false;   // stay on this letter to see what happened
            RunCommand(() => _sim.Reply(id, option));
        }
    }
}
