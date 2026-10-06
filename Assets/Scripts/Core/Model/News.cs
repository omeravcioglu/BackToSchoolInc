namespace SchoolSim
{
    /// <summary>
    /// A story in the papers. Stories about the school can move its reputation for a while:
    /// good press helps, bad press hurts. Stories about rivals are background (and sometimes
    /// move the rival's standing).
    /// </summary>
    public sealed class NewsStory
    {
        public int Id;
        public int Day;
        public string Outlet;
        public string Headline;
        public string Body;
        public NewsTone Tone;
        public NewsTopic Topic;
        public bool AboutUs = true;

        // Reputation effect actually applied (0 = none), and the modifier it went into.
        public float Effect;
        public ReputationId EffectRep = ReputationId.Overall;
        public int EffectDays;
        public string EffectLabel;

        public bool Read;
    }

    /// <summary>One day of school history, recorded after every tick for the statistics pages.</summary>
    public sealed class DaySnapshot
    {
        public int Day;
        public float Cash;
        public float Net;              // projected monthly result
        public int Students;
        public int Beds;
        public int Staff;
        public float Prestige;
        public float Score;            // national ranking score
        public int Rank;
        public float[] Stats;          // indexed by StatId
        public float[] Reputation;     // indexed by ReputationId
        public float[] RivalScores;    // in GameState.Rivals order
    }

    /// <summary>Where the assistant's tutorial is.</summary>
    public sealed class TutorialState
    {
        public bool Active;
        public bool Done;
        public bool Skipped;
        public int Step;
        public int StepDay;
        public int Baseline;           // e.g. dorm count when the build step began
        public int MessageId = -1;     // the practice decision
    }
}
