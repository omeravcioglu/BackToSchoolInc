using System;
using System.Collections.Generic;
using System.Text;

namespace SchoolSim
{
    /// <summary>
    /// The academic year: school starts in September (month 1); in July (month 11) seniors
    /// graduate, families decide whether to return, the annual report is written and
    /// Admissions Season opens.
    /// </summary>
    public sealed class YearCycleSystem
    {
        readonly Simulation _sim;

        public YearCycleSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;

        public void StartOfSchoolYear()
        {
            S.Year = new YearTracker
            {
                StudentsStart = S.Students.Count,
                RankStart = S.NationalRank,
                ReputationStart = (float[])S.Reputation.Clone(),
            };
            _sim.Sports.StartSeason();
            if (S.Day > 0)
            {
                _sim.Inbox.Notify(SenderType.StudentAffairs, "Student Affairs", "A new school year begins",
                    $"Year {Calendar.Year(S.Day)} starts with {S.Students.Count} students and {S.Staff.Count} staff. Good luck, Director.");
            }
        }

        public void EndOfSchoolYear()
        {
            int year = Calendar.Year(S.Day);
            var report = new YearReport
            {
                Year = year,
                StudentsStart = S.Year.StudentsStart,
                RankStart = S.Year.RankStart,
                ReputationStart = S.Year.ReputationStart ?? (float[])S.Reputation.Clone(),
                IncidentCount = S.Year.Incidents,
                StaffLeft = S.Year.StaffLeft,
                Championships = S.Year.Championships,
                ParentSatisfaction = S.Stat(StatId.ParentSatisfaction),
                StudentSatisfaction = S.Stat(StatId.StudentSatisfaction),
                StaffMorale = S.Stat(StatId.StaffMorale),
                AvgGrades = _sim.Indices.AvgGrades,
            };

            SumFinances(report, year);
            foreach (var t in S.Teams) { report.SportsWins += t.Wins; report.SportsLosses += t.Losses; }

            Graduate(report, year);
            GraduationNews(year);
            Reenroll(report);
            foreach (var s in S.Students) s.Grade++;

            _sim.Rivals.UpdateRank(announce: false);
            report.RankEnd = S.NationalRank;
            report.ReputationEnd = (float[])S.Reputation.Clone();
            report.StudentsEnd = S.Students.Count;
            report.Highlights.AddRange(S.Year.Highlights);
            S.Reports.Add(report);

            S.WageIndex *= 1f + Balance.WageInflationPerYear;
            _sim.Rivals.Yearly();

            _sim.Inbox.Notify(SenderType.Government, "Board of Trustees", $"Annual report: Year {year}", Describe(report), MessagePriority.Important);
            _sim.Log($"Year {year} complete. Graduates: {report.Graduates}, leavers: {report.Leavers}.", important: true);
            _sim.RaiseYearReport(report);

            _sim.Admissions.OpenSeason();
        }

        /// <summary>
        /// The report card of the school year still under way, as it stands today. Nothing changes:
        /// nobody graduates and the rankings aren't redone. The seniors are listed as the class due
        /// to graduate in July. Only meaningful during the school months.
        /// </summary>
        public YearReport Draft()
        {
            int year = Calendar.Year(S.Day);
            var report = new YearReport
            {
                Year = year,
                StudentsStart = S.Year.StudentsStart,
                RankStart = S.Year.RankStart,
                ReputationStart = S.Year.ReputationStart ?? (float[])S.Reputation.Clone(),
                IncidentCount = S.Year.Incidents,
                StaffLeft = S.Year.StaffLeft,
                Championships = S.Year.Championships,
                ParentSatisfaction = S.Stat(StatId.ParentSatisfaction),
                StudentSatisfaction = S.Stat(StatId.StudentSatisfaction),
                StaffMorale = S.Stat(StatId.StaffMorale),
                AvgGrades = _sim.Indices.AvgGrades,
                RankEnd = S.NationalRank,
                ReputationEnd = (float[])S.Reputation.Clone(),
                StudentsEnd = S.Students.Count,
            };
            SumFinances(report, year);
            foreach (var t in S.Teams) { report.SportsWins += t.Wins; report.SportsLosses += t.Losses; }
            foreach (var s in S.Students)
            {
                if (s.Grade < 4) continue;
                report.Graduates++;
                report.GraduateLines.Add($"{s.FullName}: graduates in July");
            }
            report.Highlights.AddRange(S.Year.Highlights);
            return report;
        }

        void SumFinances(YearReport report, int year)
        {
            var ledgers = new List<MonthLedger>(S.Finance.History) { S.Finance.Current };
            foreach (var l in ledgers)
            {
                bool inYear = (l.Year == year && l.Month <= Calendar.LastSchoolMonth) || (l.Year == year - 1 && l.Month > Calendar.LastSchoolMonth);
                if (!inYear) continue;
                report.Revenue += l.Income;
                report.Expenses += l.Expenses;
                report.ScholarshipsForgone += l.ScholarshipsForgone;
            }
        }

        void Graduate(YearReport report, int year)
        {
            S.AcademicLegacy *= 0.9f;
            foreach (var s in S.Students.FindAll(x => x.Grade >= 4))
            {
                float score = s.Grades * 0.6f + s.Academic * 0.3f + (s.Artistic > 80f ? 5f : 0f) + _sim.Rng.Range(-6f, 6f)
                              + _sim.Indices.SignatureEffect(x => x.Graduates);
                var tier = score >= 85f ? UniversityTier.EliteUniversity
                    : score >= 72f ? UniversityTier.TopUniversity
                    : score >= 55f ? UniversityTier.University
                    : score >= 40f ? UniversityTier.College
                    : UniversityTier.Workforce;

                string achievement = $"Went on to {Names.Tier(tier)}";
                if (s.TeamId >= 0 && s.Athletic >= 85f)
                {
                    if (tier < UniversityTier.University) tier = UniversityTier.University;
                    achievement = $"Earned an athletic scholarship to play {S.FindTeam(s.TeamId)?.Sport} at university";
                }
                if (s.Incidents >= 3 && tier >= UniversityTier.University)
                {
                    achievement += " after turning their life around";
                    S.Year.Highlight($"{s.FullName}, once a regular in the principal's office, graduated to {Names.Tier(tier)}");
                }

                S.AcademicLegacy += tier switch
                {
                    UniversityTier.EliteUniversity => 0.8f,
                    UniversityTier.TopUniversity => 0.4f,
                    UniversityTier.University => 0.15f,
                    UniversityTier.Workforce => -0.1f,
                    _ => 0f,
                };

                S.Alumni.Add(new Alumnus
                {
                    Name = s.FullName,
                    GraduationYear = year,
                    Tier = tier,
                    Achievement = achievement,
                    Wealth = MathUtil.Clamp100(s.FamilyWealth * 0.5f + (int)tier * 12f + _sim.Rng.Range(-10f, 10f)),
                });
                report.Graduates++;
                report.GraduateLines.Add($"{s.FullName}: {achievement}");
                _sim.Students.Remove(s, null);
            }
            S.AcademicLegacy = MathUtil.Clamp(S.AcademicLegacy, 0f, 15f);
            _sim.Progression.AddSchoolXp(report.Graduates * 15);
        }

        void GraduationNews(int year)
        {
            int total = 0, top = 0, work = 0;
            foreach (var a in S.Alumni)
            {
                if (a.GraduationYear != year) continue;
                total++;
                if (a.Tier >= UniversityTier.TopUniversity) top++;
                if (a.Tier == UniversityTier.Workforce) work++;
            }
            if (total == 0) return;
            string n = S.SchoolName;
            if (top > 0)
                _sim.News.Publish(NewsTone.Good, NewsTopic.Academics,
                    top == 1 ? $"{n} graduate heads for a top university" : $"{top} {n} graduates head for top universities",
                    $"The class of year {year} did {n} proud: {top} of {total} graduates won places at leading universities.",
                    2f, ReputationId.Academic, 120, NewsSystem.Weekly);
            else if (work * 2 > total)
                _sim.News.Publish(NewsTone.Bad, NewsTopic.Academics, $"Few {n} graduates make it to university",
                    $"Most of the {total} students who graduated from {n} this year are going straight into work.",
                    -1f, ReputationId.Academic, 90, NewsSystem.Weekly);
            else
                _sim.News.Publish(NewsTone.Neutral, NewsTopic.Academics, $"The class of year {year} graduates from {n}",
                    $"Caps in the air: {total} students graduated from {n} this summer.");
        }

        void Reenroll(YearReport report)
        {
            float tuition = S.Policies.PendingTuition > 0 ? S.Policies.PendingTuition : S.Policies.AnnualTuition;
            foreach (var s in S.Students.ToArray())
            {
                float p = _sim.Students.RetentionChance(s, tuition);
                if (_sim.Rng.Chance(p)) continue;

                string reason;
                if (s.Happiness < 40f) reason = "they were unhappy here";
                else if (s.ParentMood < 40f) reason = "their parents lost faith in the school";
                else if (tuition * (1f - s.Discount) > _sim.FairTuition) reason = "tuition is too high for the family";
                else reason = "the family moved away";

                report.Leavers++;
                report.Highlights.Add($"{s.FullName} will not return: {reason}");
                _sim.Students.Remove(s, reason);
            }
        }

        public static string Describe(YearReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"FINANCES  Revenue {MathUtil.Money(r.Revenue)} · Expenses {MathUtil.Money(-r.Expenses)} · Net {MathUtil.Money(r.Revenue + r.Expenses)}");
            if (r.ScholarshipsForgone > 0) sb.AppendLine($"          Aid and scholarships given: {MathUtil.Money(r.ScholarshipsForgone)}");
            sb.AppendLine($"STUDENTS  {r.StudentsStart} at start · {r.Graduates} graduated · {r.Leavers} won't return · {r.StudentsEnd} continuing");
            sb.AppendLine($"          Avg grades {r.AvgGrades:0} · Student satisfaction {r.StudentSatisfaction:0} · Incidents {r.IncidentCount}");
            sb.AppendLine($"PARENTS   Satisfaction {r.ParentSatisfaction:0}");
            sb.AppendLine($"STAFF     Morale {r.StaffMorale:0} · Departures {r.StaffLeft}");
            if (r.SportsWins + r.SportsLosses > 0)
                sb.AppendLine($"SPORTS    {r.SportsWins}W-{r.SportsLosses}L · Championships {r.Championships}");
            sb.AppendLine($"RANKING   #{r.RankStart} → #{r.RankEnd}");
            sb.Append("REPUTATION");
            for (int i = 0; i < r.ReputationEnd.Length; i++)
            {
                float d = r.ReputationEnd[i] - r.ReputationStart[i];
                sb.Append($"  {(ReputationId)i} {r.ReputationEnd[i]:0} ({MathUtil.Signed(d, "0")})");
            }
            sb.AppendLine();

            if (r.GraduateLines.Count > 0)
            {
                sb.AppendLine().AppendLine("GRADUATES");
                foreach (var g in r.GraduateLines) sb.AppendLine("  " + g);
            }
            if (r.Highlights.Count > 0)
            {
                sb.AppendLine().AppendLine("HIGHLIGHTS");
                foreach (var h in r.Highlights) sb.AppendLine("  " + h);
            }
            return sb.ToString();
        }
    }
}
