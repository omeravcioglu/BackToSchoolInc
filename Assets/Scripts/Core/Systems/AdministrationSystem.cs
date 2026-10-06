using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Administrators answer routine inbox messages in their area, following the director's
    /// policies. Weak administrators sometimes make the wrong call.
    /// </summary>
    public sealed class AdministrationSystem
    {
        readonly Simulation _sim;

        public AdministrationSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;

        public void DailyTick()
        {
            if (!_sim.IsUnlocked(Feature.Administrators)) return;
            var handledToday = new Dictionary<int, int>();

            foreach (var msg in S.Inbox.ToArray())
            {
                // Give the director a day to answer personally first.
                if (!msg.NeedsDecision || msg.ReceivedDay >= S.Day) continue;
                var template = _sim.Events.Get(msg.TemplateId);
                if (template == null || template.Handler == AdminRole.None) continue;

                var admin = _sim.FindAdmin(template.Handler);
                if (admin == null) continue;

                handledToday.TryGetValue(admin.Id, out int done);
                int capacity = Math.Max(2, (int)(admin.Skill / 20f));
                if (done >= capacity) continue;

                string optionId = template.AutoDecide(_sim, msg, S.AdminPolicies);
                if (optionId == null) continue;

                bool mistake = false;
                if (_sim.Rng.Chance((1f - admin.Skill / 100f) * 0.25f))
                {
                    var alternatives = new List<MessageOption>();
                    foreach (var o in msg.Options)
                        if (o.Id != optionId && _sim.Inbox.WhyNot(msg, o) == null) alternatives.Add(o);
                    if (alternatives.Count > 0)
                    {
                        optionId = _sim.Rng.Pick(alternatives).Id;
                        mistake = true;
                    }
                }

                var option = msg.FindOption(optionId);
                if (option == null || _sim.Inbox.WhyNot(msg, option) != null) continue;

                if (_sim.Inbox.Resolve(msg.Id, optionId, out _, admin))
                {
                    handledToday[admin.Id] = done + 1;
                    admin.Energy = MathUtil.Clamp100(admin.Energy - 0.5f);
                    if (mistake)
                    {
                        msg.ResolutionText += $" ({admin.Name} misjudged this one.)";
                        _sim.Log($"{admin.Title} {admin.Name} made a questionable call on \"{msg.Subject}\".", important: true);
                    }
                }
            }
        }
    }
}
