using System;
using System.Collections.Generic;

namespace SchoolSim
{
    public sealed class MonthLedger
    {
        public static readonly int CategoryCount = Enum.GetValues(typeof(LedgerCategory)).Length;

        public int Year;
        public int Month;
        public readonly float[] Amounts = new float[CategoryCount];
        public float ScholarshipsForgone;    // memo: tuition waived through scholarships and discounts

        public float Income
        {
            get
            {
                float s = 0f;
                foreach (var a in Amounts) if (a > 0) s += a;
                return s;
            }
        }

        public float Expenses
        {
            get
            {
                float s = 0f;
                foreach (var a in Amounts) if (a < 0) s += a;
                return s;
            }
        }

        public float Net => Income + Expenses;

        public float this[LedgerCategory c] => Amounts[(int)c];
    }

    public sealed class Transaction
    {
        public int Day;
        public LedgerCategory Category;
        public float Amount;
        public string Note;
    }

    public sealed class Loan
    {
        public string Lender;
        public float MonthlyPayment;
        public int PaymentsLeft;
    }

    public sealed class Sponsorship
    {
        public string Company;
        public float Monthly;
        public int EndDay;
        public string Terms;
    }

    public sealed class Finance
    {
        public float Cash;
        public MonthLedger Current = new MonthLedger();
        public readonly List<MonthLedger> History = new List<MonthLedger>();
        public readonly List<Transaction> Recent = new List<Transaction>();
        public readonly List<Loan> Loans = new List<Loan>();
        public readonly List<Sponsorship> Sponsorships = new List<Sponsorship>();

        public void Post(int day, LedgerCategory category, float amount, string note = null)
        {
            if (Math.Abs(amount) < 0.005f) return;
            Cash += amount;
            Current.Amounts[(int)category] += amount;
            if (note != null)
            {
                Recent.Add(new Transaction { Day = day, Category = category, Amount = amount, Note = note });
                if (Recent.Count > 200) Recent.RemoveAt(0);
            }
        }

        public void StartMonth(int year, int month)
        {
            if (Current.Year != 0) History.Add(Current);
            Current = new MonthLedger { Year = year, Month = month };
        }

        public MonthLedger LastCompletedMonth => History.Count > 0 ? History[History.Count - 1] : null;
    }
}
