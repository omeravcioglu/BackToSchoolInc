using System;

namespace SchoolSim
{
    public sealed class TutorialStep
    {
        public string Id;
        /// <summary>What the HUD points at: "tab:inbox", "vitals", "map"... or null.</summary>
        public string Target;
        /// <summary>Parts of the game that appear when this step begins.</summary>
        public Reveal[] Reveals = Array.Empty<Reveal>();
        /// <summary>A UI event that completes the step ("open:inbox"), or null.</summary>
        public string Signal;
        /// <summary>Label of the bubble's button; pressing it sends the "next" signal.</summary>
        public string Button;
    }

    /// <summary>
    /// The assistant's first-week walkthrough. Each step brings one part of the game into view
    /// and waits for the director to use it. Time keeps running; random trouble holds off for the
    /// first few weeks. Skipping reveals everything the tutorial would have.
    /// </summary>
    public sealed class TutorialSystem
    {
        readonly Simulation _sim;

        public TutorialSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;
        TutorialState T => _sim.State.Tutorial;

        public const int XpPerStep = 12;

        /// <summary>What a school without the tutorial starts with.</summary>
        public static readonly Reveal[] Basics =
        {
            Reveal.Inbox, Reveal.Build, Reveal.Students, Reveal.Staff, Reveal.Money, Reveal.Vitals, Reveal.Stats,
        };

        public static readonly TutorialStep[] Steps =
        {
            new TutorialStep { Id = "hello", Reveals = new[] { Reveal.Inbox }, Signal = "next", Button = "Let's go" },
            new TutorialStep { Id = "inbox", Target = "tab:inbox", Signal = "open:inbox" },
            new TutorialStep { Id = "decide", Target = "tab:inbox" },
            new TutorialStep { Id = "build", Target = "tab:build", Reveals = new[] { Reveal.Build } },
            new TutorialStep { Id = "room", Target = "map", Signal = "select:room" },
            new TutorialStep { Id = "students", Target = "tab:students", Reveals = new[] { Reveal.Students }, Signal = "open:students" },
            new TutorialStep { Id = "money", Target = "tab:money", Reveals = new[] { Reveal.Money }, Signal = "open:money" },
            new TutorialStep { Id = "vitals", Target = "vitals", Reveals = new[] { Reveal.Vitals, Reveal.Stats }, Signal = "open:why" },
            new TutorialStep { Id = "staff", Target = "tab:staff", Reveals = new[] { Reveal.Staff }, Signal = "open:staff" },
            new TutorialStep { Id = "done", Signal = "next", Button = "Thanks!" },
        };

        public bool Active => T.Active;

        public TutorialStep Current => T.Active && T.Step >= 0 && T.Step < Steps.Length ? Steps[T.Step] : null;

        public int StepNumber => T.Step + 1;

        public void Start()
        {
            T.Active = true;
            T.Done = false;
            T.Step = 0;
            Enter();
        }

        /// <summary>A UI event: completes the current step if it was waiting for it.</summary>
        public void Signal(string key)
        {
            var step = Current;
            if (step != null && step.Signal == key) Advance();
        }

        /// <summary>Completes steps that wait for something to happen in the school.</summary>
        public void Check()
        {
            var step = Current;
            if (step == null) return;
            switch (step.Id)
            {
                case "decide":
                {
                    var m = S.FindMessage(T.MessageId);
                    if (m == null || m.Resolved) Advance();
                    break;
                }
                case "build":
                    if (CountDorms() > T.Baseline) Advance();
                    break;
            }
        }

        public void Skip()
        {
            if (!T.Active) return;
            T.Active = false;
            T.Skipped = true;
            T.Done = true;
            foreach (var r in Basics) _sim.Progression.Unveil(r, announce: false);
            _sim.RaiseTutorialChanged();
        }

        void Advance()
        {
            _sim.Progression.AddSchoolXp(XpPerStep);
            T.Step++;
            if (T.Step >= Steps.Length) Finish();
            else Enter();
        }

        void Enter()
        {
            var step = Current;
            T.StepDay = S.Day;
            foreach (var r in step.Reveals) _sim.Progression.Unveil(r, announce: false);
            switch (step.Id)
            {
                case "decide":
                    if (_sim.Events.TriggerDirected("tutorial_choice", null, ignoreCooldown: true))
                        T.MessageId = S.Inbox[S.Inbox.Count - 1].Id;
                    break;
                case "build":
                    T.Baseline = CountDorms();
                    break;
            }
            _sim.RaiseTutorialChanged();
            Check();
        }

        void Finish()
        {
            T.Active = false;
            T.Done = true;
            foreach (var r in Basics) _sim.Progression.Unveil(r, announce: false);
            _sim.Log("Tutorial complete. The school is all yours.");
            _sim.RaiseTutorialChanged();
        }

        int CountDorms()
        {
            int n = 0;
            foreach (var r in S.Rooms) if (r.Type == RoomType.DormRoom) n++;
            return n;
        }

        /// <summary>What the assistant says for a step.</summary>
        public string Text(TutorialStep step)
        {
            if (step == null) return "";
            var a = S.Assistant;
            switch (step.Id)
            {
                case "hello":
                    return (a != null ? AssistantVoice.Greeting(a, S.SchoolName, S.Finance.Cash) + " " : "") +
                           "The clock never stops here, so I'll show you the basics as we go.";
                case "inbox":
                    return "Letters arrive all the time: families, staff, the board. Open the Inbox.";
                case "decide":
                    return "Some letters need a decision. I left you one. Pick an answer; I've marked my favourite.";
                case "build":
                    return "Families want to come, but our beds are nearly full. Open Build and put a dorm room on our land.";
                case "room":
                    return "Building takes a few days, and the clock keeps ticking. Meanwhile, click any room on the map to see what it does and how to upgrade it.";
                case "students":
                    return "These are our students. Happy students stay, and their parents keep paying. Open Students.";
                case "money":
                    return "Tuition comes in every month; salaries and upkeep go out. Open Money to see the budget.";
                case "vitals":
                    return "These are the school's vital signs. When one drops, click it to see why.";
                case "staff":
                    return "Last one: our staff. Good teachers make a good school. Open the Staff room.";
                case "done":
                    return "That's the basics! From now on I'll bring problems to your inbox as I spot them. New tools unlock as the school grows.";
                default:
                    return "";
            }
        }
    }
}
