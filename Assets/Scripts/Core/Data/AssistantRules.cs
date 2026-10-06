using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// What each assistant personality brings to the school (a perk and a quirk), what they cost,
    /// what they watch for, and how they talk. Text shown in the debug UI sticks to ASCII.
    /// </summary>
    public static class AssistantRules
    {
        public static readonly AssistantStyle[] Styles = (AssistantStyle[])Enum.GetValues(typeof(AssistantStyle));

        /// <summary>One-word personality tag.</summary>
        public static string Word(AssistantStyle s) => s switch
        {
            AssistantStyle.Cheerful => "Sunny",
            AssistantStyle.Strict => "Stern",
            AssistantStyle.Thrifty => "Thrifty",
            AssistantStyle.Charming => "Charming",
            AssistantStyle.Bookish => "Bookish",
            AssistantStyle.Handy => "Handy",
            _ => s.ToString(),
        };

        public static string Perk(AssistantStyle s) => s switch
        {
            AssistantStyle.Cheerful => "Staff morale +4, student life +5",
            AssistantStyle.Strict => "Safety +6",
            AssistantStyle.Thrifty => "Upkeep -10%, better pay deals",
            AssistantStyle.Charming => "Good press, parents +3",
            AssistantStyle.Bookish => "Education +4, training -20%",
            AssistantStyle.Handy => "Repairs -25%, rooms wear slower",
            _ => "",
        };

        public static string Quirk(AssistantStyle s) => s switch
        {
            AssistantStyle.Cheerful => "Slow to spot money trouble",
            AssistantStyle.Strict => "Students a bit tense (-2 mood)",
            AssistantStyle.Thrifty => "Staff morale -2",
            AssistantStyle.Charming => "Costs 35% more",
            AssistantStyle.Bookish => "Misses dirt and repairs",
            AssistantStyle.Handy => "Blunt with parents (-1)",
            _ => "",
        };

        public static float Salary(AssistantStyle s, int skill)
        {
            float pay = (500f + skill * 300f) * (s == AssistantStyle.Charming ? 1.35f : 1f);
            return (float)Math.Round(pay / 10f) * 10f;
        }

        // --- Effects -------------------------------------------------------------------

        public static float StatBonus(Assistant a, StatId stat)
        {
            if (a == null) return 0f;
            switch (a.Style)
            {
                case AssistantStyle.Cheerful: return stat == StatId.StudentLife ? 5f : 0f;
                case AssistantStyle.Strict: return stat == StatId.SafetyDiscipline ? 6f : 0f;
                case AssistantStyle.Bookish: return stat == StatId.EducationQuality ? 4f : 0f;
                default: return 0f;
            }
        }

        public static float UpkeepMultiplier(Assistant a) => a != null && a.Style == AssistantStyle.Thrifty ? 0.9f : 1f;
        public static float RepairMultiplier(Assistant a) => a != null && a.Style == AssistantStyle.Handy ? 0.75f : 1f;
        public static float WearMultiplier(Assistant a) => a != null && a.Style == AssistantStyle.Handy ? 0.8f : 1f;
        public static float TrainingMultiplier(Assistant a) => a != null && a.Style == AssistantStyle.Bookish ? 0.8f : 1f;
        public static float NegotiationBonus(Assistant a) => a != null && a.Style == AssistantStyle.Thrifty ? 0.05f : 0f;
        public static float GoodPress(Assistant a) => a != null && a.Style == AssistantStyle.Charming ? 1.3f : 1f;
        public static float BadPress(Assistant a) => a != null && a.Style == AssistantStyle.Charming ? 0.6f : 1f;

        /// <summary>Blind spots: a bookish assistant doesn't notice dust or cracks.</summary>
        public static bool Watches(AssistantStyle s, ProblemKind k) =>
            !(s == AssistantStyle.Bookish && (k == ProblemKind.Dirty || k == ProblemKind.Repairs));

        /// <summary>Extra alertness for problems in their line of work (added to the skill-based margin).</summary>
        public static float Keenness(AssistantStyle s, ProblemKind k) => (s, k) switch
        {
            (AssistantStyle.Thrifty, ProblemKind.Cash) => 3f,
            (AssistantStyle.Strict, ProblemKind.Safety) => 4f,
            (AssistantStyle.Handy, ProblemKind.Repairs) => 6f,
            (AssistantStyle.Handy, ProblemKind.Dirty) => 4f,
            (AssistantStyle.Bookish, ProblemKind.Teachers) => 3f,
            (AssistantStyle.Bookish, ProblemKind.Seats) => 3f,
            (AssistantStyle.Charming, ProblemKind.Unhappy) => 4f,
            (AssistantStyle.Charming, ProblemKind.Food) => 4f,
            (AssistantStyle.Cheerful, ProblemKind.StaffMorale) => 4f,
            (AssistantStyle.Cheerful, ProblemKind.Unhappy) => 3f,
            _ => 0f,
        };

        // --- Candidates ---------------------------------------------------------------

        static readonly string[][] Quotes =
        {
            new[] { "Every problem is a party in disguise!", "I bring snacks to meetings.", "Smile. It's contagious!" },
            new[] { "Rules exist for a reason.", "On time is late.", "I don't do excuses." },
            new[] { "A penny saved is a penny earned.", "I reuse envelopes.", "Do we really need that?" },
            new[] { "Leave the parents to me.", "Everyone's my friend. Even inspectors.", "Reputation is everything." },
            new[] { "I've read the manual. Twice.", "Let's look at the data.", "Libraries are my happy place." },
            new[] { "I fix things. Fast.", "There's a toolbox in my car.", "Leaky tap? Already on it." },
        };

        /// <summary>
        /// Applicants for the assistant job: different personalities, a spread of experience, and
        /// salaries to match. Deterministic for a seed, so the pick screen can be rebuilt.
        /// </summary>
        public static List<Assistant> Candidates(int seed, int count = 4)
        {
            var rng = new SimRandom(seed ^ 0x5A17E);
            var styles = new List<AssistantStyle>(Styles);
            rng.Shuffle(styles);
            var skills = new List<int> { rng.Chance(0.35f) ? 5 : 4, 3, 3, 2 };
            rng.Shuffle(skills);

            var used = new HashSet<string>();
            var list = new List<Assistant>();
            for (int i = 0; i < count && i < styles.Count; i++)
            {
                var style = styles[i];
                int skill = skills[i % skills.Count];
                string name;
                do name = Names.Person(rng); while (!used.Add(name.Split(' ')[0]));
                list.Add(new Assistant
                {
                    Name = name,
                    Style = style,
                    Skill = skill,
                    Salary = Salary(style, skill),
                    Look = rng.Range(0, 100000),
                    Quote = rng.Pick(Quotes[(int)style]),
                });
            }
            return list;
        }
    }

    /// <summary>How each assistant talks.</summary>
    public static class AssistantVoice
    {
        public static string Greeting(Assistant a, string school, float cash) => a.Style switch
        {
            AssistantStyle.Cheerful => $"Hi hi! I'm {a.FirstName}, your new assistant. {school} is tiny, but I just know it's going to be great!",
            AssistantStyle.Strict => $"Good morning, Director. {a.FirstName}, your assistant. The school is small and the clock never stops, so let's be efficient.",
            AssistantStyle.Thrifty => $"Hello, Director. I'm {a.FirstName}. We have {MathUtil.Money(cash)} and plenty of bills, so let's spend it wisely.",
            AssistantStyle.Charming => $"Director! {a.FirstName}, delighted. Families are already curious about {school}. Let's give them a reason to come.",
            AssistantStyle.Bookish => $"Hello! I'm {a.FirstName}. I've read everything about running a school. Well, nearly everything.",
            AssistantStyle.Handy => $"Hey, Director. {a.FirstName}. If it's broken, I fix it. If it isn't built yet, I build it.",
            _ => $"Hello, Director. I'm {a.FirstName}.",
        };

        static readonly string[][] Openers =
        {
            new[] { "Okay, don't panic, but...", "Quick one!", "Small hiccup!", "Hey, Director!" },
            new[] { "Director.", "This needs your attention.", "This can't wait.", "A problem:" },
            new[] { "I've been going over the numbers.", "A word about costs.", "Quick heads-up, Director.", "Before this gets expensive:" },
            new[] { "Director, a little bird told me...", "People are talking, Director.", "A delicate matter:", "Between you and me:" },
            new[] { "I've been keeping notes.", "According to my figures:", "An observation:", "I looked into something." },
            new[] { "Heads up, boss.", "Got a job for you.", "Spotted something on my rounds.", "Right, so:" },
        };

        static readonly string[] SignOffs =
        {
            "We've got this!",
            "Your call. Make it soon.",
            "Every dollar counts.",
            "Let's keep everyone smiling.",
            "The numbers don't lie.",
            "Say the word and I'll get on it.",
        };

        public static string Opener(AssistantStyle s, SimRandom rng) => rng.Pick(Openers[(int)s]);

        public static string SignOff(AssistantStyle s) => SignOffs[(int)s];

        /// <summary>A line about a problem from someone for whom it's their pet subject.</summary>
        public static string Flavor(AssistantStyle s, ProblemKind k) => (s, k) switch
        {
            (AssistantStyle.Thrifty, ProblemKind.Cash) => "Honestly, this is the one that keeps me up at night.",
            (AssistantStyle.Strict, ProblemKind.Safety) => "Discipline is the foundation of everything else.",
            (AssistantStyle.Handy, ProblemKind.Repairs) => "I could patch it with tape, but I wouldn't.",
            (AssistantStyle.Handy, ProblemKind.Dirty) => "I'd mop it myself, but I only have two hands.",
            (AssistantStyle.Bookish, ProblemKind.Teachers) => "Class size really matters. So does common sense.",
            (AssistantStyle.Bookish, ProblemKind.Seats) => "Students learn very little standing in the corridor.",
            (AssistantStyle.Charming, ProblemKind.Unhappy) => "Parents hear everything their kids say at dinner.",
            (AssistantStyle.Charming, ProblemKind.Food) => "Nothing spreads faster than a bad lunch story.",
            (AssistantStyle.Cheerful, ProblemKind.StaffMorale) => "A happy staff room is a happy school!",
            (AssistantStyle.Cheerful, ProblemKind.Unhappy) => "Let's bring the fun back!",
            (AssistantStyle.Thrifty, ProblemKind.Upgrade) => "It's a sound investment. I checked twice.",
            (AssistantStyle.Handy, ProblemKind.Upgrade) => "I've already got the paint picked out.",
            _ => null,
        };

        public static string Advice(AssistantStyle s, string label) => s switch
        {
            AssistantStyle.Cheerful => $"I vote \"{label}\"!",
            AssistantStyle.Strict => $"Do this: \"{label}\".",
            AssistantStyle.Thrifty => $"Best value: \"{label}\".",
            AssistantStyle.Charming => $"I'd go with \"{label}\". People will like it.",
            AssistantStyle.Bookish => $"The sensible choice is \"{label}\".",
            AssistantStyle.Handy => $"I'd pick \"{label}\".",
            _ => $"I'd pick \"{label}\".",
        };

        public static string Later(AssistantStyle s) => s switch
        {
            AssistantStyle.Cheerful => "Okay! I'll keep an eye on it.",
            AssistantStyle.Strict => "Noted. It won't fix itself.",
            AssistantStyle.Thrifty => "Fine. Waiting usually costs more, though.",
            AssistantStyle.Charming => "Of course. I'll keep everyone calm meanwhile.",
            AssistantStyle.Bookish => "Understood. I'll keep taking notes.",
            AssistantStyle.Handy => "Alright. You know where to find me.",
            _ => "Okay.",
        };

        public static string Done(AssistantStyle s) => s switch
        {
            AssistantStyle.Cheerful => "Yay! On it!",
            AssistantStyle.Strict => "Done. Good decision.",
            AssistantStyle.Thrifty => "Done, and on budget.",
            AssistantStyle.Charming => "Wonderful. People will notice.",
            AssistantStyle.Bookish => "Excellent. I'll track the results.",
            AssistantStyle.Handy => "Consider it handled.",
            _ => "Done.",
        };
    }
}
