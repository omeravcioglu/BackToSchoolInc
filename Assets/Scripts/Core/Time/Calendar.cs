namespace SchoolSim
{
    /// <summary>
    /// Simulation calendar. The game counts whole days from 0. A year is 12 months of 30 days.
    /// Month 1 is September: months 1-10 are the school year (Sep-Jun) and months 11-12 are
    /// summer (Jul-Aug), when students go home and Admissions Season runs.
    /// </summary>
    public static class Calendar
    {
        public const int DaysPerMonth = 30;
        public const int MonthsPerYear = 12;
        public const int DaysPerYear = DaysPerMonth * MonthsPerYear;
        public const int DaysPerWeek = 7;

        public const int LastSchoolMonth = 10;
        public const int AdmissionsMonth = 11;
        public const int FirstSemesterEndMonth = 5;

        static readonly string[] MonthNames =
            { "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug" };

        public static int Year(int day) => day / DaysPerYear + 1;
        public static int Month(int day) => day % DaysPerYear / DaysPerMonth + 1;
        public static int DayOfMonth(int day) => day % DaysPerMonth + 1;
        public static int DayOfWeek(int day) => day % DaysPerWeek;

        public static bool IsWeekend(int day) => DayOfWeek(day) >= 5;
        public static bool IsSchoolMonth(int day) => Month(day) <= LastSchoolMonth;
        public static bool IsSummer(int day) => !IsSchoolMonth(day);
        public static bool IsAdmissionsSeason(int day) => Month(day) >= AdmissionsMonth;
        public static bool IsExamMonth(int day) => Month(day) == FirstSemesterEndMonth || Month(day) == LastSchoolMonth;
        public static int Semester(int day) => Month(day) <= FirstSemesterEndMonth ? 1 : 2;

        public static bool IsFirstDayOfMonth(int day) => day % DaysPerMonth == 0;
        public static bool IsFirstDayOfYear(int day) => day % DaysPerYear == 0;

        public static int StartOfMonth(int year, int month) => (year - 1) * DaysPerYear + (month - 1) * DaysPerMonth;

        public static string MonthName(int month) => MonthNames[(month - 1) % MonthsPerYear];

        public static string Format(int day) => $"Y{Year(day)} {MonthName(Month(day))} {DayOfMonth(day)}";

        public static string FormatShort(int day) => $"Y{Year(day)} {MonthName(Month(day))}";
    }
}
