namespace SchoolSim
{
    /// <summary>Keeps a daily record of the school for charts, trends and rankings history.</summary>
    public sealed class HistorySystem
    {
        readonly Simulation _sim;

        public HistorySystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;

        /// <summary>About twenty years of days.</summary>
        public const int MaxDays = Calendar.DaysPerYear * 20;

        public void Record()
        {
            var list = S.History;
            if (list.Count > 0 && list[list.Count - 1].Day == S.Day) list.RemoveAt(list.Count - 1);
            var snap = new DaySnapshot
            {
                Day = S.Day,
                Cash = S.Finance.Cash,
                Net = _sim.Economy.Project().Net,
                Students = S.Students.Count,
                Beds = _sim.Indices.Beds,
                Staff = S.Staff.Count,
                Prestige = S.Prestige,
                Score = _sim.Rivals.PlayerScore,
                Rank = S.NationalRank,
                Stats = (float[])S.Stats.Clone(),
                Reputation = (float[])S.Reputation.Clone(),
                RivalScores = new float[S.Rivals.Count],
            };
            for (int i = 0; i < S.Rivals.Count; i++) snap.RivalScores[i] = S.Rivals[i].Score;
            list.Add(snap);
            if (list.Count > MaxDays) list.RemoveRange(0, list.Count - MaxDays);
        }

        /// <summary>The last snapshot on or before a day (the oldest one if the day is earlier).</summary>
        public DaySnapshot At(int day)
        {
            var list = S.History;
            if (list.Count == 0) return null;
            int lo = 0, hi = list.Count - 1;
            if (day <= list[0].Day) return list[0];
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (list[mid].Day <= day) lo = mid;
                else hi = mid - 1;
            }
            return list[lo];
        }
    }
}
