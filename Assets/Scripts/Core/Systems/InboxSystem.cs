using System;

namespace SchoolSim
{
    /// <summary>The director's inbox: delivery, replies, expiry.</summary>
    public sealed class InboxSystem
    {
        readonly Simulation _sim;

        public InboxSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;

        public void Add(InboxMessage msg)
        {
            msg.Id = S.NewId();
            msg.ReceivedDay = S.Day;
            if (msg.Options.Count > 0) _sim.Assistants.Advise(msg);
            S.Inbox.Add(msg);

            // Keep the archive bounded: drop the oldest finished messages.
            if (S.Inbox.Count > 300)
            {
                int idx = S.Inbox.FindIndex(m => !m.NeedsDecision);
                if (idx >= 0) S.Inbox.RemoveAt(idx);
            }
            _sim.RaiseMessageArrived(msg);
        }

        /// <summary>An informational message with nothing to decide.</summary>
        public InboxMessage Notify(SenderType sender, string senderName, string subject, string body, MessagePriority priority = MessagePriority.Info)
        {
            var msg = new InboxMessage
            {
                TemplateId = "info",
                Sender = sender,
                SenderName = senderName,
                Subject = subject,
                Body = body,
                Priority = priority,
            };
            Add(msg);
            return msg;
        }

        public int PendingDecisions
        {
            get
            {
                int n = 0;
                foreach (var m in S.Inbox) if (m.NeedsDecision) n++;
                return n;
            }
        }

        public int Unread
        {
            get
            {
                int n = 0;
                foreach (var m in S.Inbox) if (!m.Read) n++;
                return n;
            }
        }

        /// <summary>Null when the option can be chosen, otherwise the reason it cannot.</summary>
        public string WhyNot(InboxMessage msg, MessageOption option)
        {
            if (option.DisabledReason != null) return option.DisabledReason;
            if (option.RequiresFeature.HasValue && !_sim.IsUnlocked(option.RequiresFeature.Value))
                return $"Requires {option.RequiresFeature.Value} (school level {Balance.UnlockLevel(option.RequiresFeature.Value)})";
            if (option.Cost > 0f && S.Finance.Cash < option.Cost) return "Not enough money";
            return null;
        }

        public bool Resolve(int messageId, string optionId, out string result, StaffMember handledBy = null)
        {
            result = null;
            var msg = S.FindMessage(messageId);
            if (msg == null || !msg.NeedsDecision) { result = "Nothing to decide"; return false; }
            var option = msg.FindOption(optionId);
            if (option == null) { result = "Unknown option"; return false; }

            string why = WhyNot(msg, option);
            if (why != null) { result = why; return false; }

            var template = _sim.Events.Get(msg.TemplateId);
            string text = template != null ? template.Resolve(_sim, msg, optionId) : "";

            msg.Resolved = true;
            msg.Read = true;
            msg.ChosenOptionId = optionId;
            msg.ResolutionText = text;
            msg.HandledBy = handledBy?.Name;
            result = text;

            if (handledBy == null) _sim.Progression.AddDirectorXp(5);
            string who = handledBy == null ? "You" : $"{handledBy.Title} {handledBy.Name}";
            _sim.Log($"{who} replied to \"{msg.Subject}\": {option.Label}. {text}".Trim());
            return true;
        }

        public void DailyTick()
        {
            int day = S.Day;
            foreach (var msg in S.Inbox.ToArray())
            {
                if (!msg.NeedsDecision || msg.ExpiresDay < 0 || day < msg.ExpiresDay) continue;
                var template = _sim.Events.Get(msg.TemplateId);
                string text = template != null ? template.Expire(_sim, msg) : "";
                msg.Resolved = true;
                msg.ChosenOptionId = "expired";
                msg.ResolutionText = "(No reply) " + text;
                _sim.Log($"You never replied to \"{msg.Subject}\". {text}".Trim());
            }
        }

        public void MarkAllRead()
        {
            foreach (var m in S.Inbox) if (!m.NeedsDecision) m.Read = true;
        }
    }
}
