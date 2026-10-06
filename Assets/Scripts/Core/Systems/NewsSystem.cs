using System;

namespace SchoolSim
{
    /// <summary>
    /// The papers. Big moments (inspections, finals, rankings, scandals, new buildings) make the
    /// news as they happen; a weekly look at the school turns very good or very bad conditions
    /// into feature stories; rivals get coverage too. Stories about the school nudge its
    /// reputation for a while, and bad ones can bring a reporter's call.
    /// </summary>
    public sealed class NewsSystem
    {
        readonly Simulation _sim;

        public NewsSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;

        public const string Courier = "The County Courier";
        public const string Weekly = "Education Weekly";
        public const string Forum = "Parents' Forum";
        public const string Gazette = "Campus Gazette";

        const int MaxStories = 150;

        public NewsStory Publish(NewsTone tone, NewsTopic topic, string headline, string body,
            float effect = 0f, ReputationId rep = ReputationId.Overall, int days = 60, string outlet = null)
        {
            var story = new NewsStory
            {
                Id = S.NewId(),
                Day = S.Day,
                Tone = tone,
                Topic = topic,
                Headline = headline,
                Body = body,
                Outlet = outlet ?? DefaultOutlet(topic),
            };
            if (Math.Abs(effect) > 0.01f)
            {
                effect *= effect > 0f ? AssistantRules.GoodPress(S.Assistant) : AssistantRules.BadPress(S.Assistant);
                story.Effect = effect;
                story.EffectRep = rep;
                story.EffectDays = days;
                story.EffectLabel = (effect > 0f ? "Good press: " : "Bad press: ") + TopicWord(topic);
                _sim.AddReputationModifier(rep, story.EffectLabel, effect, days);
            }
            Add(story);

            if (tone == NewsTone.Bad && effect <= -1.5f && _sim.Rng.Chance(0.5f))
                _sim.Events.TriggerDirected("press_inquiry", m => m.OtherId = story.Id);
            return story;
        }

        /// <summary>A story about a rival school. Moves the rival a little; no effect on us.</summary>
        public NewsStory PublishRival(string headline, string body)
        {
            var story = new NewsStory
            {
                Id = S.NewId(),
                Day = S.Day,
                Tone = NewsTone.Neutral,
                Topic = NewsTopic.Rivals,
                Headline = headline,
                Body = body,
                Outlet = Weekly,
                AboutUs = false,
            };
            Add(story);
            return story;
        }

        void Add(NewsStory story)
        {
            S.News.Add(story);
            if (S.News.Count > MaxStories) S.News.RemoveAt(0);
            _sim.Progression.Unveil(Reveal.News);
            _sim.RaiseNews(story);
        }

        public int Unread
        {
            get
            {
                int n = 0;
                foreach (var s in S.News) if (!s.Read) n++;
                return n;
            }
        }

        public void MarkAllRead()
        {
            foreach (var s in S.News) s.Read = true;
        }

        static string DefaultOutlet(NewsTopic topic) => topic switch
        {
            NewsTopic.Rankings => Weekly,
            NewsTopic.Academics => Weekly,
            NewsTopic.Food => Forum,
            NewsTopic.Dorms => Forum,
            NewsTopic.StudentLife => Gazette,
            NewsTopic.Rivals => Weekly,
            _ => Courier,
        };

        public static string TopicWord(NewsTopic topic) => topic switch
        {
            NewsTopic.School => "school",
            NewsTopic.Academics => "academics",
            NewsTopic.Sports => "sports",
            NewsTopic.Safety => "safety",
            NewsTopic.Food => "food",
            NewsTopic.Dorms => "dorms",
            NewsTopic.StudentLife => "student life",
            NewsTopic.Money => "money",
            NewsTopic.Staff => "staff",
            NewsTopic.Rankings => "rankings",
            NewsTopic.Campus => "campus",
            NewsTopic.Rivals => "rivals",
            _ => topic.ToString().ToLowerInvariant(),
        };

        // ------------------------------------------------------------------------------
        // Daily: weekly features, student milestones, monthly rival stories
        // ------------------------------------------------------------------------------

        public void DailyTick()
        {
            if (S.Day <= 0 || S.GameOver) return;
            if (S.Tutorial.Active && S.Day < Balance.TutorialQuietDays) return;
            Milestones();
            if (Calendar.DayOfWeek(S.Day) == 3) Features();
            if (Calendar.DayOfMonth(S.Day) == 12) RivalStory();
        }

        /// <summary>True (and starts the cooldown) when a story of this kind hasn't run lately.</summary>
        bool Fresh(string key, int cooldown)
        {
            key = "news:" + key;
            if (S.EventCooldowns.TryGetValue(key, out int until) && S.Day < until) return false;
            S.EventCooldowns[key] = S.Day + cooldown;
            return true;
        }

        void Features()
        {
            if (S.Students.Count == 0) return;
            string n = S.SchoolName;
            float food = S.Stat(StatId.FoodQuality), clean = S.Stat(StatId.Cleanliness), safe = S.Stat(StatId.SafetyDiscipline);
            float edu = S.Stat(StatId.EducationQuality), life = S.Stat(StatId.StudentLife), happy = S.Stat(StatId.StudentSatisfaction);
            float staff = S.Stat(StatId.StaffMorale), dorm = S.Stat(StatId.DormitoryQuality), parents = S.Stat(StatId.ParentSatisfaction);

            // At most one feature a week. Bad news travels faster, so it goes first.
            if (food <= 30f && Fresh("food_bad", 60))
                Publish(NewsTone.Bad, NewsTopic.Food, $"Cafeteria food under fire at {n}",
                    $"Parents are sharing photos of grey stew and soggy chips from the {n} cafeteria. \"My son lives on toast,\" one wrote.",
                    -1.5f, ReputationId.Food, 60, Forum);
            else if (clean <= 32f && Fresh("clean_bad", 60))
                Publish(NewsTone.Bad, NewsTopic.Dorms, $"Grubby dorms shock parents at {n}",
                    $"Visiting parents describe overflowing bins and sticky floors in the {n} dormitories.",
                    -2f, ReputationId.Dormitory, 60, Forum);
            else if (safe <= 30f && Fresh("safety_bad", 60))
                Publish(NewsTone.Bad, NewsTopic.Safety, $"Safety fears grow at {n}",
                    $"Reports of fights and late-night chaos have some families asking whether {n} is in control.",
                    -2f, ReputationId.Safety, 60, Courier);
            else if (happy <= 30f && Fresh("happy_bad", 60))
                Publish(NewsTone.Bad, NewsTopic.StudentLife, $"Unhappy students at {n}",
                    $"Students at {n} describe life there as \"boring and stressful\". Several have asked about transferring.",
                    -1.5f, ReputationId.StudentLife, 60, Forum);
            else if (staff <= 30f && Fresh("staff_bad", 60))
                Publish(NewsTone.Bad, NewsTopic.Staff, $"Teachers grumble at {n}",
                    $"Staff at {n} complain of low pay and long hours, according to a teachers' union newsletter.",
                    -1f, ReputationId.Overall, 60, Courier);
            else if (edu >= 75f && Fresh("edu_good", 120))
                Publish(NewsTone.Good, NewsTopic.Academics, $"Teachers at {n} win praise",
                    $"An education blog singles out {n} for lessons that are \"genuinely inspiring\".",
                    1.5f, ReputationId.Academic, 90, Weekly);
            else if (food >= 80f && Fresh("food_good", 90))
                Publish(NewsTone.Good, NewsTopic.Food, $"Students rave about the food at {n}",
                    $"Fresh bread, real salads and a famous Friday curry: the {n} cafeteria is winning fans.",
                    1.5f, ReputationId.Food, 90, Forum);
            else if (life >= 75f && Fresh("life_good", 120))
                Publish(NewsTone.Good, NewsTopic.StudentLife, $"Life at {n} is a blast, say students",
                    $"Clubs, games and a busy social calendar make {n} a popular choice with teenagers.",
                    1.5f, ReputationId.StudentLife, 90, Gazette);
            else if (dorm >= 80f && Fresh("dorm_good", 120))
                Publish(NewsTone.Good, NewsTopic.Dorms, $"Dorms at {n} are \"like a hotel\"",
                    "Comfortable rooms and spotless bathrooms impress visiting families.",
                    1.5f, ReputationId.Dormitory, 90, Forum);
            else if (parents >= 80f && Fresh("parents_good", 120))
                Publish(NewsTone.Good, NewsTopic.School, $"Parents can't stop praising {n}",
                    $"Families give {n} glowing reviews: caring staff, happy kids, no nasty surprises.",
                    1f, ReputationId.Overall, 90, Forum);
        }

        static readonly int[] StudentMilestones = { 20, 50, 100, 150, 200, 300, 400, 500 };

        void Milestones()
        {
            foreach (int m in StudentMilestones)
            {
                if (m <= S.NewsMilestone) continue;
                if (S.Students.Count < m) return;
                S.NewsMilestone = m;
                Publish(NewsTone.Good, NewsTopic.School, $"{S.SchoolName} welcomes its {m}th student",
                    $"The little school is growing up: {S.Students.Count} students now call {S.SchoolName} home.",
                    1f, ReputationId.Overall, 60, Courier);
                return;
            }
        }

        void RivalStory()
        {
            var r = _sim.Rivals.RandomRival();
            if (r == null) return;
            switch (_sim.Rng.Range(0, 6))
            {
                case 0:
                    r.SportsRep = MathUtil.Clamp100(r.SportsRep + 2f);
                    PublishRival($"{r.Name} opens a new sports hall", $"Athletes at {r.Name} are celebrating a brand-new hall. Rival coaches are taking notes.");
                    break;
                case 1:
                    r.Academic = MathUtil.Clamp100(r.Academic + 2f);
                    PublishRival($"{r.Name} hires a celebrated head teacher", $"{r.Name} has poached an award-winning head of studies from the city.");
                    break;
                case 2:
                    r.Prestige = MathUtil.Clamp(r.Prestige - 2f, 15f, 95f);
                    PublishRival($"Exam-leak inquiry at {r.Name}", $"Officials are investigating claims that exam papers circulated at {r.Name} before the tests.");
                    break;
                case 3:
                    r.Students += 10;
                    PublishRival($"{r.Name} expands its boarding house", $"Ten more beds at {r.Name}: the school says demand has never been higher.");
                    break;
                case 4:
                    r.Tuition *= 1.05f;
                    PublishRival($"{r.Name} raises its fees again", $"Families at {r.Name} face a 5% increase. Some are said to be looking elsewhere.");
                    break;
                default:
                    r.Prestige = MathUtil.Clamp(r.Prestige + 1f, 15f, 95f);
                    PublishRival($"{r.Name} wins the regional debate cup", $"Students from {r.Name} argued their way to the trophy for the second time in three years.");
                    break;
            }
        }

        // ------------------------------------------------------------------------------
        // Stories other systems call in
        // ------------------------------------------------------------------------------

        /// <summary>Some incidents make the papers (at most one every few weeks).</summary>
        public void Incident(string what)
        {
            if (!Fresh("incident", 25)) return;
            string n = S.SchoolName;
            switch (what)
            {
                case "fight":
                    Publish(NewsTone.Bad, NewsTopic.Safety, $"Fight breaks out at {n}",
                        $"Two students at {n} came to blows in a corridor this week. Parents are asking questions.", -1.5f, ReputationId.Safety, 45);
                    break;
                case "bullying":
                    Publish(NewsTone.Bad, NewsTopic.Safety, $"Bullying claims at {n}",
                        $"A parent says their child is being bullied at {n} and that \"nobody seems to notice\".", -1.5f, ReputationId.Safety, 45, Forum);
                    break;
                case "party":
                    Publish(NewsTone.Bad, NewsTopic.Dorms, $"Wild dorm party at {n}",
                        $"Neighbours of {n} complain about loud music past midnight from the school's dorms.", -1f, ReputationId.Safety, 45);
                    break;
                default:
                    Publish(NewsTone.Bad, NewsTopic.Campus, $"Vandals strike {n}",
                        $"Broken furniture and graffiti greeted staff at {n} on Monday morning.", -1f, ReputationId.Safety, 45);
                    break;
            }
        }
    }
}
