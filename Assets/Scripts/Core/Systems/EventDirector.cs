using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Base class for every inbox event. A template decides when it can fire, writes the message
    /// (text plus options), and applies the consequences of the chosen reply.
    /// </summary>
    public abstract class EventTemplate
    {
        public abstract string Id { get; }

        /// <summary>Days before this template can fire again (random or directed).</summary>
        public virtual int CooldownDays => 25;

        /// <summary>False for events that only fire when a system triggers them directly.</summary>
        public virtual bool Random => true;

        public virtual bool InSessionOnly => true;

        /// <summary>Which administrator can answer this message automatically.</summary>
        public virtual AdminRole Handler => AdminRole.None;

        public virtual int ResponseDays => 10;

        /// <summary>Relative chance of firing today. 0 means not eligible.</summary>
        public virtual float Weight(Simulation sim) => 1f;

        /// <summary>
        /// Fill in the message. The payload may already be set by a directed trigger; random
        /// triggers pick their own subjects. Return false to cancel.
        /// </summary>
        public abstract bool Build(Simulation sim, InboxMessage msg);

        public virtual string Resolve(Simulation sim, InboxMessage msg, string optionId) => "";

        /// <summary>What happens when the director never replies. Defaults to the "ignore" option.</summary>
        public virtual string Expire(Simulation sim, InboxMessage msg)
        {
            return msg.FindOption("ignore") != null ? Resolve(sim, msg, "ignore") : "";
        }

        /// <summary>Option an administrator picks under the current policies, or null to leave it for the director.</summary>
        public virtual string AutoDecide(Simulation sim, InboxMessage msg, AdminPolicies policies) => null;

        public virtual void OnScheduled(Simulation sim, ScheduledEvent e) { }
    }

    public sealed class EventDirector
    {
        readonly Simulation _sim;
        readonly Dictionary<string, EventTemplate> _templates = new Dictionary<string, EventTemplate>();
        readonly List<EventTemplate> _random = new List<EventTemplate>();

        public EventDirector(Simulation sim)
        {
            _sim = sim;
            foreach (var t in EventCatalog.Create())
            {
                _templates[t.Id] = t;
                if (t.Random) _random.Add(t);
            }
        }

        GameState S => _sim.State;

        public EventTemplate Get(string id)
        {
            if (id == null) return null;
            _templates.TryGetValue(id, out var t);
            return t;
        }

        public IEnumerable<EventTemplate> All => _templates.Values;

        public void DailyTick()
        {
            if (S.Students.Count == 0) return;
            // A new director learning the ropes gets a few quiet weeks.
            if (S.Tutorial.Active && S.Day < Balance.TutorialQuietDays) return;

            float rate = Math.Min(0.5f, 0.10f + 0.05f * (float)Math.Sqrt(S.Students.Count / 10f));
            int pending = _sim.Inbox.PendingDecisions;
            if (pending >= 10) rate *= 0.3f;
            else if (pending >= 6) rate *= 0.6f;

            int count = (int)rate + (_sim.Rng.Chance(rate - (int)rate) ? 1 : 0);
            for (int i = 0; i < count; i++) TriggerRandom();
        }

        bool OnCooldown(EventTemplate t) => S.EventCooldowns.TryGetValue(t.Id, out int until) && S.Day < until;

        public bool TriggerRandom()
        {
            var eligible = new List<(EventTemplate t, float w)>();
            foreach (var t in _random)
            {
                if (t.InSessionOnly && !_sim.InSession) continue;
                if (OnCooldown(t)) continue;
                float w = t.Weight(_sim);
                if (w > 0f) eligible.Add((t, w));
            }
            if (eligible.Count == 0) return false;

            // A template may decline to build (no suitable subject); try a few.
            for (int attempt = 0; attempt < 3 && eligible.Count > 0; attempt++)
            {
                var pick = _sim.Rng.PickWeighted(eligible, e => e.w);
                if (Deliver(pick.t, new InboxMessage { TemplateId = pick.t.Id })) return true;
                eligible.Remove(pick);
            }
            return false;
        }

        public bool TriggerDirected(string id, Action<InboxMessage> setup, bool ignoreCooldown = false)
        {
            var t = Get(id);
            if (t == null) throw new ArgumentException("Unknown event template: " + id);
            if (!ignoreCooldown && OnCooldown(t)) return false;

            var msg = new InboxMessage { TemplateId = id };
            setup?.Invoke(msg);
            return Deliver(t, msg);
        }

        bool Deliver(EventTemplate t, InboxMessage msg)
        {
            if (!t.Build(_sim, msg)) return false;
            if (msg.Options.Count > 0 && msg.ExpiresDay < 0) msg.ExpiresDay = S.Day + t.ResponseDays;
            _sim.Inbox.Add(msg);
            S.EventCooldowns[t.Id] = S.Day + t.CooldownDays;
            return true;
        }

        public void Schedule(ScheduledEvent e) => S.Scheduled.Add(e);

        public void RunScheduled(ScheduledEvent e) => Get(e.TemplateId)?.OnScheduled(_sim, e);
    }
}
