using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The money page's loans and sponsorships: what each loan still costs and when it's paid off,
    /// loans on offer in the inbox (borrowed right here), and the sponsors paying in each month.
    /// </summary>
    public sealed partial class GameHud
    {
        static InkDrawing _monPiggy;

        VisualElement _monLoans, _monSponsors;

        void MonShowLoans()
        {
            _monLoans.Clear();
            var loans = S.Finance.Loans;
            var offers = MonLoanOffers();
            bool none = loans.Count == 0 && offers.Count == 0;
            _monLoans.EnableInClassList("mon-dashed", none);
            if (none)
            {
                _monLoans.Add(MonNothing(new InkPicture(MonPiggy()), "No loans", "banks write when cash runs low"));
                return;
            }

            float owed = 0f;
            foreach (var l in loans) owed += l.MonthlyPayment * l.PaymentsLeft;
            _monLoans.Add(MonCardHead("mon-bank", "Loans", owed > 0f ? Money(owed) + " to pay back" : "none yet"));
            // Repayments come out with each month's bills, the first of the month.
            int monthStart = S.Day - S.Day % Calendar.DaysPerMonth;
            foreach (var l in loans)
            {
                string last = Calendar.FormatShort(monthStart + l.PaymentsLeft * Calendar.DaysPerMonth);
                string left = l.PaymentsLeft == 1 ? "last payment next month" : $"{l.PaymentsLeft} payments left, the last in {last}";
                _monLoans.Add(MonLine(l.Lender, $"{Money(l.MonthlyPayment)} a month · {left}", Money(l.MonthlyPayment * l.PaymentsLeft)));
            }
            foreach (var m in offers) _monLoans.Add(MonOffer(m));
        }

        /// <summary>Letters still waiting for an answer that offer a loan (the bank's, or the assistant's money worries).</summary>
        List<InboxMessage> MonLoanOffers()
        {
            var list = new List<InboxMessage>();
            foreach (var m in S.Inbox)
            {
                if (!m.NeedsDecision) continue;
                foreach (var o in m.Options)
                {
                    if (!MonIsLoan(o)) continue;
                    list.Add(m);
                    break;
                }
            }
            return list;
        }

        static bool MonIsLoan(MessageOption o) => o.Id.StartsWith("loan", StringComparison.Ordinal);

        VisualElement MonOffer(InboxMessage m)
        {
            var box = Ink("mon-offer");
            var head = Box("mon-offer-head");
            head.Add(Icon("envelope", "icon-18"));
            head.Add(Text("On offer from " + m.SenderName + (m.ExpiresDay >= 0 ? ", answer by " + MonthDay(m.ExpiresDay) : ""), "font-heavy", "mon-offer-who"));
            box.Add(head);
            int id = m.Id;
            foreach (var o in m.Options)
            {
                if (!MonIsLoan(o)) continue;
                var option = o.Id;
                var b = Ink("mon-offer-btn");
                b.Add(Icon("coin", "icon-18"));
                b.Add(Text(o.Label, "font-semi", "mon-offer-label"));
                OnClick(b, () => RunCommand(() => _sim.Reply(id, option)));
                box.Add(b);
            }
            var link = Box("mon-link");
            link.Add(Text("read the letter", "font-note", "mon-link-label"));
            link.Add(Icon("next", "icon-13", "icon--blue"));
            OnClick(link, () => OpenMessage(id));
            box.Add(link);
            return box;
        }

        void MonShowSponsors()
        {
            _monSponsors.Clear();
            var list = S.Finance.Sponsorships;
            _monSponsors.EnableInClassList("mon-dashed", list.Count == 0);
            if (list.Count == 0)
            {
                _monSponsors.Add(MonNothing(Icon("trophy"), "No sponsors yet", "they call once your teams draw fans"));
                return;
            }
            float monthly = 0f;
            foreach (var sp in list) monthly += sp.Monthly;
            _monSponsors.Add(MonCardHead("trophy", "Sponsors", Signed(monthly) + " a month"));
            foreach (var sp in list)
            {
                string terms = string.IsNullOrEmpty(sp.Terms) ? "" : " · " + sp.Terms.ToLowerInvariant();
                _monSponsors.Add(MonLine(sp.Company, $"until {Calendar.Format(sp.EndDay)}{terms}", Signed(sp.Monthly)));
            }
        }

        /// <summary>The dashed "nothing here" card: a little picture and a handwritten note.</summary>
        static VisualElement MonNothing(VisualElement art, string title, string note)
        {
            var row = Box("mon-nothing");
            art.AddToClassList("mon-nothing-art");
            row.Add(art);
            var text = Box("mon-nothing-text");
            text.Add(Text(title, "font-hand", "mon-nothing-title"));
            text.Add(Text(note, "font-note", "mon-nothing-note"));
            row.Add(text);
            return row;
        }

        static VisualElement MonLine(string name, string detail, string amount)
        {
            var row = Box("mon-line");
            var text = Box("mon-line-text");
            text.Add(Text(name, "font-semi", "mon-line-name"));
            text.Add(Text(detail, "font-semi", "mon-line-sub"));
            row.Add(text);
            row.Add(Text(amount, "font-hand", "mon-line-amount"));
            return row;
        }

        static InkDrawing MonPiggy() => _monPiggy ??= new InkDrawing(48f, 40f)
            .Shape("M8 20c0-8 7-13 16-13 5 0 9 1.5 12 4l5-2-1 6c2 2 3 4 3 6l3 1v5l-4 1c-1 3-3 5-6 6v4h-6v-3h-8v3h-6v-5c-5-3-8-7-8-13z",
                InkIcons.Sky, InkIcons.Ink, 2.2f)
            .Circle(34f, 17f, 1.4f, InkIcons.Ink)
            .Line("M20 7.5c1-2 4-3 6-1", InkIcons.Ink, 2.2f)
            .Line("M4 18c2 0 3 1 4 2", InkIcons.Ink, 2.2f);
    }
}
