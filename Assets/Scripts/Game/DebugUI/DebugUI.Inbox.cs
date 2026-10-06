using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    public sealed partial class DebugUI
    {
        Vector2 _inboxListScroll, _inboxDetailScroll;
        int _selectedMessage = -1;
        bool _decisionsOnly;

        void DrawInbox()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(380));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Inbox", _h2);
            GUILayout.FlexibleSpace();
            bool only = GUILayout.Toggle(_decisionsOnly, "Needs decision only");
            if (only != _decisionsOnly) Defer(() => _decisionsOnly = only);
            if (GUILayout.Button("Mark read", GUILayout.Width(80))) Defer(Sim.Inbox.MarkAllRead);
            GUILayout.EndHorizontal();

            _inboxListScroll = GUILayout.BeginScrollView(_inboxListScroll);
            var list = new List<InboxMessage>(S.Inbox);
            list.Sort((a, b) =>
            {
                int d = b.NeedsDecision.CompareTo(a.NeedsDecision);
                return d != 0 ? d : b.ReceivedDay.CompareTo(a.ReceivedDay);
            });
            foreach (var m in list)
            {
                if (_decisionsOnly && !m.NeedsDecision) continue;
                var msg = m;
                string when = S.Day - m.ReceivedDay == 0 ? "today" : $"{S.Day - m.ReceivedDay}d ago";
                string status = m.NeedsDecision ? (m.ExpiresDay >= 0 ? $" · {m.ExpiresDay - S.Day}d to reply" : "") : m.Resolved ? " · done" : "";
                string dot = !m.Read ? "• " : "   ";
                GUI.color = _selectedMessage == m.Id ? Good
                    : m.NeedsDecision ? (m.Priority >= MessagePriority.Urgent ? Bad : m.Priority >= MessagePriority.Important ? Warn : Color.white)
                    : Dim;
                if (GUILayout.Button($"{dot}<b>{m.Subject}</b>\n     {m.SenderName} · {when}{status}", _rowButton, GUILayout.Height(40)))
                    Defer(() => { _selectedMessage = msg.Id; msg.Read = true; });
                GUI.color = Color.white;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            _inboxDetailScroll = GUILayout.BeginScrollView(_inboxDetailScroll);
            var sel = S.FindMessage(_selectedMessage);
            if (sel == null) GUILayout.Label("Select a message.", _small);
            else DrawMessage(sel);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        void DrawMessage(InboxMessage m)
        {
            GUILayout.Label(m.Subject, _h1);
            GUILayout.Label($"From {m.SenderName} ({m.Sender}) · {Calendar.Format(m.ReceivedDay)}", _small);
            if (m.NeedsDecision && m.ExpiresDay >= 0)
                Colored($"Reply by {Calendar.Format(m.ExpiresDay)} ({m.ExpiresDay - S.Day} days). No reply has consequences too.", Warn, _small);
            GUILayout.Space(8);
            GUILayout.Label(m.Body, _wrap);
            GUILayout.Space(10);

            if (m.Resolved)
            {
                var chosen = m.FindOption(m.ChosenOptionId);
                string who = m.HandledBy != null ? $"{m.HandledBy} replied" : "You replied";
                if (m.ChosenOptionId == "expired") Colored("You never replied.", Bad);
                else if (chosen != null) Colored($"{who}: {chosen.Label}", Good);
                if (!string.IsNullOrEmpty(m.ResolutionText)) GUILayout.Label(m.ResolutionText, _wrap);
                return;
            }

            if (m.Options.Count == 0) return;
            if (!string.IsNullOrEmpty(m.AdvisorNote) && S.Assistant != null)
                Colored($"{S.Assistant.FirstName}, your assistant: {m.AdvisorNote}", new Color(1f, 0.85f, 0.4f), _wrap);
            GUILayout.Label("How do you respond?", _h2);
            foreach (var o in m.Options)
            {
                var option = o;
                string why = Sim.Inbox.WhyNot(m, o);
                GUILayout.BeginVertical(_panel);
                string label = (o.Id == m.AdvisorOptionId ? "(advised) " : "") + o.Label + (o.Cost > 0 && !o.Label.Contains("$") ? $"  [{Money(o.Cost)}]" : "");
                if (ActionButton(label, why, GUILayout.Height(28)))
                {
                    int id = m.Id;
                    Run(() => Sim.Reply(id, option.Id));
                }
                if (!string.IsNullOrEmpty(o.Preview)) GUILayout.Label(o.Preview, _small);
                if (!string.IsNullOrEmpty(o.Hint))
                {
                    if (Sim.Progression.ShowHints) Colored("Insight: " + o.Hint, new Color(0.7f, 0.8f, 1f), _small);
                    else GUILayout.Label($"(Director level {Balance.DirectorInsightLevel} reveals hidden consequences.)", _small);
                }
                if (why != null) Colored(why, Bad, _small);
                GUILayout.EndVertical();
            }

            var template = Sim.Events.Get(m.TemplateId);
            if (template != null && template.Handler != AdminRole.None)
            {
                var admin = Sim.FindAdmin(template.Handler);
                GUILayout.Label(admin != null
                    ? $"{admin.Title} {admin.Name} may answer this for you if it falls within your policies."
                    : $"A {Names.AdminRoleTitle(template.Handler)} could handle messages like this automatically.", _small);
            }
        }
    }
}
