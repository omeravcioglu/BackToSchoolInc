using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>A temporary mood modifier ("Won the championship +8, 10 days").</summary>
    public sealed class Moodlet
    {
        public string Label;
        public float Value;
        public int ExpiresDay;

        public Moodlet(string label, float value, int expiresDay)
        {
            Label = label;
            Value = value;
            ExpiresDay = expiresDay;
        }
    }

    public static class MoodletExtensions
    {
        /// <summary>Adds a moodlet, replacing any existing one with the same label.</summary>
        public static void Set(this List<Moodlet> list, string label, float value, int expiresDay)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Label == label)
                {
                    list[i].Value = value;
                    list[i].ExpiresDay = expiresDay;
                    return;
                }
            }
            list.Add(new Moodlet(label, value, expiresDay));
        }

        public static void Expire(this List<Moodlet> list, int day)
        {
            list.RemoveAll(m => m.ExpiresDay <= day);
        }

        public static float Sum(this List<Moodlet> list)
        {
            float s = 0f;
            for (int i = 0; i < list.Count; i++) s += list[i].Value;
            return s;
        }
    }

    public sealed class Student
    {
        public int Id;
        public string FirstName;
        public string LastName;
        public string ParentName;
        public int Grade = 1;              // 1-4; graduates after grade 4
        public int EnrolledDay;

        // Abilities (0-100)
        public float Academic;
        public float Athletic;
        public float Social;
        public float Discipline;
        public float Artistic;
        public SportType SportTalent = SportType.None;
        public float FamilyWealth;

        // Dynamic state (0-100)
        public float Happiness = 60f;
        public float Stress = 30f;
        public float Grades = 50f;
        public float Behavior = 70f;
        public float Health = 100f;
        public float ParentMood = 55f;

        public readonly List<StudentTrait> Traits = new List<StudentTrait>();
        public readonly List<Moodlet> Moodlets = new List<Moodlet>();
        public readonly List<Moodlet> ParentMoodlets = new List<Moodlet>();
        public readonly List<string> History = new List<string>();

        // Placement
        public int DormRoomId = -1;
        public int ClubId = -1;
        public int TeamId = -1;
        public int InjuredUntilDay = -1;
        public int SickUntilDay = -1;

        // Money
        public ScholarshipType Scholarship = ScholarshipType.None;
        public float Discount;              // fraction of tuition waived

        // Students from abroad pay the international fee and start out homesick.
        public string HomeCountry;          // null for local families
        public float Homesick;              // 0-100, fades as they settle in

        public int Incidents;
        public int LastIncidentDay = -999;
        public int LastComplaintDay = -999;

        // Cached each day for the UI; not part of the persistent state.
        public Breakdown HappinessFactors = new Breakdown();
        public Breakdown ParentFactors = new Breakdown();
        public float HappinessTarget;

        public string FullName => FirstName + " " + LastName;
        public bool International => HomeCountry != null;
        public bool Has(StudentTrait t) => Traits.Contains(t);
        public bool IsInjured(int day) => InjuredUntilDay > day;
        public bool IsSick(int day) => SickUntilDay > day;

        public void Remember(int day, string text)
        {
            History.Add(Calendar.FormatShort(day) + ": " + text);
            if (History.Count > 40) History.RemoveAt(0);
        }

        public override string ToString() => FullName;
    }

    public sealed class Applicant
    {
        public Student Student;
        public float RequestedAid;          // fraction of tuition the family asks to be waived
        public float Interest;              // 0-1 chance they accept an offer without aid
        public bool Transfer;
        public bool EliteAthlete;
        public bool Scholar;                // a national scholar: top marks, courted by every elite school
        public int ExpiresDay;
        public string Pitch;
    }

    public sealed class StaffMember
    {
        public int Id;
        public string Name;
        public StaffRole Role;
        public AdminRole AdminRole = AdminRole.None;
        public Subject Subject = Subject.None;
        public SportType Sport = SportType.None;

        // Core stats (0-100). For coaches: Skill = coaching skill, Charisma = motivation.
        public float Skill;
        public float Experience;           // years
        public float Patience;
        public float Charisma;
        public float ClassroomManagement;
        public float Strategy;
        public float PlayerDevelopment;
        public float Recruitment;

        public float Morale = 65f;
        public float Energy = 90f;
        public float Loyalty = 50f;

        public readonly List<StaffTrait> Traits = new List<StaffTrait>();
        public readonly List<Moodlet> Moodlets = new List<Moodlet>();

        // Employment
        public float Salary;                // monthly
        public int HiredDay;
        public int ContractYears = 1;
        public int ContractEndDay;
        public int LastRaiseDay;
        public int LastRaiseRequestDay = -999;
        public int LastPoachDay = -999;
        public bool RenewalOffered;

        public int AwayUntilDay = -1;        // training or leave
        public TrainingProgram? CurrentTraining;
        public int LeavingDay = -1;          // set when resigning / contract expiring

        public Breakdown MoraleFactors = new Breakdown();

        public bool Has(StaffTrait t) => Traits.Contains(t);
        public bool IsAway(int day) => AwayUntilDay > day;
        public bool IsActive(int day) => !IsAway(day);

        public string Title
        {
            get
            {
                if (Role == StaffRole.Administrator && AdminRole != AdminRole.None) return Names.AdminRoleTitle(AdminRole);
                if (Role == StaffRole.Teacher && Subject != Subject.None) return Subject + " Teacher";
                if (Role == StaffRole.Coach && Sport != SportType.None) return Sport + " Coach";
                return Role.ToString();
            }
        }

        public override string ToString() => Name;
    }

    public sealed class Candidate
    {
        public StaffMember Staff;
        public float AskingSalary;
        public float MinPrestige;
        public int ExpiresDay;
    }
}
