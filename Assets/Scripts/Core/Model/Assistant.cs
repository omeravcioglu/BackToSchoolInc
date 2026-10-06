namespace SchoolSim
{
    /// <summary>
    /// The director's assistant, picked from a few candidates when a school is founded. They run
    /// the tutorial, bring problems to the inbox, comment on decisions and lend the school a perk
    /// (and a quirk) that comes from their personality.
    /// </summary>
    public sealed class Assistant
    {
        public string Name;
        public AssistantStyle Style;
        public int Skill;            // 1-5: how early they notice trouble and how sound their advice is
        public float Salary;         // per month, paid with the payroll
        public int Look;             // portrait variation
        public string Quote;
        public int HiredDay;

        public string FirstName
        {
            get
            {
                if (string.IsNullOrEmpty(Name)) return "";
                int space = Name.IndexOf(' ');
                return space > 0 ? Name.Substring(0, space) : Name;
            }
        }
    }
}
