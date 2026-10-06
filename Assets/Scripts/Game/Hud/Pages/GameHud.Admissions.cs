using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The admissions page: a strip of beds taken and free, every family applying (best fit first)
    /// as a card you can answer (see GameHud.Admissions.Cards.cs), and beside them the marketing
    /// campaign and the admissions calendar: when the big intake comes, how fast transfers arrive
    /// and how many families to expect next season.
    /// </summary>
    public sealed partial class GameHud
    {
        const int AdxBedIcons = 24;             // more beds than this show as a meter instead
        const int AdxLastTransferMonth = 8;     // transfers apply week by week from September to April

        readonly List<VisualElement> _adxMonths = new List<VisualElement>();
        VisualElement _adxGrid, _adxBedIcons, _adxCampaignRun, _adxCampaignFill;
        Label _adxBedText, _adxBedSub, _adxCount, _adxEmptyTitle, _adxEmptyNote, _adxArrivalsTitle, _adxCampaignLine, _adxBoost, _adxRunLabel;
        VisualElement _adxEmpty, _adxArrivals;
        int _adxArrivalsSig = int.MinValue;
        Label _adxSeasonTitle, _adxSeasonWhen, _adxTransfers, _adxNextSeason, _adxPrice, _adxDirector;
        InkBox _adxRun;
        int _adxSignature;

        partial void BuildAdmissionsPage()
        {
            AdxDefineIcons();
            _adxCards.Clear();
            _adxAnswers.Clear();
            _adxMonths.Clear();
            _adxSignature = 0;

            var main = Box("adx-main");
            var head = Box("adx-head");
            var beds = Ink("adx-beds");
            _adxBedIcons = Box("adx-bed-icons");
            beds.Add(_adxBedIcons);
            var words = Box("adx-bed-words");
            _adxBedText = Text("", "font-heavy", "adx-bed-text");
            _adxBedSub = Text("", "font-note", "adx-bed-sub");
            words.Add(_adxBedText);
            words.Add(_adxBedSub);
            beds.Add(words);
            head.Add(beds);
            _adxCount = Text("", "font-note", "adx-count");
            head.Add(_adxCount);
            main.Add(head);

            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("adx-list");
            list.contentContainer.AddToClassList("adx-list-content");
            _adxGrid = Box("adx-grid");
            list.Add(_adxGrid);
            list.Add(AdxBuildEmpty());
            main.Add(list);
            _pageHost.Add(main);

            var side = new ScrollView(ScrollViewMode.Vertical);
            side.AddToClassList("adx-side");
            side.contentContainer.AddToClassList("adx-side-content");
            side.Add(AdxBuildCampaign());
            side.Add(AdxBuildSeason());
            _pageHost.Add(side);
        }

        partial void RefreshAdmissionsPage(bool newDay)
        {
            if (_adxGrid == null) return;
            float now = GameClock.Now;
            bool expired = _adxAnswers.RemoveAll(a => now >= a.Until) > 0;
            int signature = AdxSignature();
            if (!newDay && !expired && signature == _adxSignature) return;
            _adxSignature = signature;
            AdxRefreshBeds();
            AdxRefreshCards();
            AdxRefreshCampaign();
            AdxRefreshSeason();
        }

        partial void ResetAdmissionsPage()
        {
            _adxCards.Clear();
            _adxAnswers.Clear();
            _adxCustomId = -1;
            _adxCustomType = ScholarshipType.None;
            _adxCustomDiscount = 0.25f;
        }

        /// <summary>What the page must follow between days too: families arriving or leaving, beds, the campaign, answers.</summary>
        int AdxSignature()
        {
            unchecked
            {
                int h = S.Applicants.Count;
                foreach (var a in S.Applicants) h = h * 31 + a.Student.Id;
                h = h * 31 + _sim.Facilities.FreeBeds();
                h = h * 31 + (_sim.Admissions.MarketingActive ? 1 : 0);
                h = h * 31 + (S.Finance.Cash >= Balance.MarketingCost ? 1 : 0);
                return h * 31 + _adxAnswers.Count;
            }
        }

        // --- Beds -----------------------------------------------------------------------------

        void AdxRefreshBeds()
        {
            int total = 0;
            foreach (var r in S.Rooms)
                if (r.Type == RoomType.DormRoom && r.IsOperational(S.Day)) total += r.Capacity;
            int free = _sim.Facilities.FreeBeds();
            int taken = Mathf.Max(0, total - free);

            _adxBedIcons.Clear();
            if (total <= AdxBedIcons)
            {
                for (int i = 0; i < total; i++) _adxBedIcons.Add(Icon(i < taken ? "adx-bed-taken" : "adx-bed-free", "adx-bed"));
            }
            else
            {
                // Too many beds to draw one by one: a segmented meter, never full while a bed is free.
                int filled = Mathf.RoundToInt(AdxBedIcons * taken / (float)total);
                if (free > 0) filled = Mathf.Min(filled, AdxBedIcons - 1);
                for (int i = 0; i < AdxBedIcons; i++) _adxBedIcons.Add(Box("adx-seg", i < filled ? "adx-seg--taken" : "adx-seg--free"));
            }

            SetText(_adxBedText, total == 0 ? "no dorm beds yet" : free == 0 ? "no free beds" : free == 1 ? "1 bed free" : $"{free} beds free");
            _adxBedText.EnableInClassList("adx-bed-text--full", free == 0);
            SetText(_adxBedSub, total == 0 ? "build a dorm room to take students"
                : free == 0 ? "build or upgrade dorm rooms to admit anyone"
                : $"{taken} of {total} taken");
        }

        // --- Campaign -------------------------------------------------------------------------

        VisualElement AdxBuildCampaign()
        {
            var card = Ink("adx-campaign");
            var art = new InkPicture(AdxMegaphone());
            art.AddToClassList("adx-megaphone");
            card.Add(art);
            card.Add(Text("Campaign", "font-hand", "adx-side-title"));
            card.Add(Text($"{Money(Balance.MarketingCost)} · {Balance.MarketingDays} days", "font-heavy", "adx-campaign-cost"));
            card.Add(Text("more transfers for a month, and more families next season", "font-note", "adx-campaign-note"));

            _adxCampaignRun = Box("adx-campaign-run");
            _adxCampaignLine = Text("", "font-semi", "adx-campaign-line");
            _adxCampaignRun.Add(_adxCampaignLine);
            var bar = Box("bar", "adx-campaign-bar");
            _adxCampaignFill = Box("bar-fill");
            bar.Add(_adxCampaignFill);
            _adxCampaignRun.Add(bar);
            card.Add(_adxCampaignRun);
            _adxBoost = Text("", "font-note", "adx-boost");
            card.Add(_adxBoost);

            _adxRun = Ink("adx-btn", "adx-btn--primary", "adx-run");
            _adxRunLabel = Text("Run it", "font-heavy", "adx-btn-label");
            _adxRun.Add(_adxRunLabel);
            OnClick(_adxRun, () => RunCommand(_sim.RunMarketing));
            card.Add(_adxRun);
            return card;
        }

        void AdxRefreshCampaign()
        {
            bool running = _sim.Admissions.MarketingActive;
            bool afford = S.Finance.Cash >= Balance.MarketingCost;
            Show(_adxCampaignRun, running);
            if (running)
            {
                int left = S.MarketingUntilDay - S.Day;
                SetText(_adxCampaignLine, $"running until {MonthDay(S.MarketingUntilDay)} · {AdxDays(left)} left");
                SetPercent(_adxCampaignFill, 100f * left / Balance.MarketingDays);
            }
            bool boost = S.ApplicantBonus > 0.001f;
            Show(_adxBoost, boost);
            if (boost) SetText(_adxBoost, $"+{AdxPercent(S.ApplicantBonus)} families next season so far");

            // Clicking a greyed button still runs the command, so its toast says why not.
            _adxRun.EnableInClassList("adx-btn--off", running || !afford);
            SetText(_adxRunLabel, running ? "Running" : afford ? "Run it" : "Not enough money");
        }

        static InkDrawing AdxMegaphone() => new InkDrawing(92f, 74f)
            .Shape("M14 30h14l30-18v50L28 44H14z", InkIcons.Sky, InkIcons.Ink, 2.6f)
            .Line("M20 44l6 22h10l-5-22", InkIcons.Ink, 2.6f)
            .Rect(8f, 30f, 8f, 14f, InkIcons.Paper, InkIcons.Ink, 2.6f, 2f)
            .Line("M68 26c4 3 4 17 0 20M76 18c8 6 8 30 0 36", InkIcons.Ink, 2.4f)
            .Shape("M58 12c2 0 4 2 4 25s-2 25-4 25", InkIcons.Marker, InkIcons.Ink, 2.6f);

        // --- Season ---------------------------------------------------------------------------

        VisualElement AdxBuildSeason()
        {
            var note = Ink("adx-season");
            note.Add(TapeStrip("adx-season-tape", "tape-sky"));
            _adxSeasonTitle = Text("Admissions Season", "font-hand", "adx-side-title");
            note.Add(_adxSeasonTitle);
            _adxSeasonWhen = Text("", "font-note", "adx-season-when");
            note.Add(_adxSeasonWhen);

            var months = Box("adx-months");
            for (int m = 1; m <= Calendar.MonthsPerYear; m++)
            {
                var cell = Box("adx-month");
                if (m >= Calendar.AdmissionsMonth) cell.AddToClassList("adx-month--season");
                else if (m <= AdxLastTransferMonth) cell.AddToClassList("adx-month--transfers");
                cell.Add(Text(Calendar.MonthName(m), "font-heavy", "adx-month-label"));
                months.Add(cell);
                _adxMonths.Add(cell);
            }
            note.Add(months);
            var key = Box("adx-key");
            key.Add(Box("adx-key-dot", "adx-month--transfers"));
            key.Add(Text("transfers", "font-semi", "adx-key-text"));
            key.Add(Box("adx-key-dot", "adx-month--season"));
            key.Add(Text("the big intake", "font-semi", "adx-key-text"));
            note.Add(key);

            _adxTransfers = Text("", "font-semi", "adx-season-line");
            _adxNextSeason = Text("", "font-semi", "adx-season-line");
            _adxPrice = Text("", "font-semi", "adx-season-line");
            _adxDirector = Text("", "font-note", "adx-season-note");
            note.Add(_adxTransfers);
            note.Add(_adxNextSeason);
            note.Add(_adxPrice);
            note.Add(_adxDirector);
            return note;
        }

        void AdxRefreshSeason()
        {
            int day = S.Day, month = Calendar.Month(day);
            if (Calendar.IsAdmissionsSeason(day))
            {
                int deadline = Calendar.StartOfMonth(Calendar.Year(day) + 1, 1);
                SetText(_adxSeasonTitle, "Admissions Season!");
                SetText(_adxSeasonWhen, $"open now: answer by {MonthDay(deadline)}, when the school year starts");
            }
            else
            {
                int opens = Calendar.StartOfMonth(Calendar.Year(day), Calendar.AdmissionsMonth);
                SetText(_adxSeasonTitle, "Admissions Season");
                SetText(_adxSeasonWhen, $"opens in July, {AdxDays(opens - day)} from now. "
                    + (month <= AdxLastTransferMonth ? "Until April, transfers apply week by week." : "No transfers in May and June."));
            }
            for (int i = 0; i < _adxMonths.Count; i++) _adxMonths[i].EnableInClassList("adx-month--now", i + 1 == month);

            SetText(_adxTransfers, $"Transfers: about {_sim.Admissions.TransferRate():0.0} a month right now");
            float tuition = S.Policies.PendingTuition > 0f ? S.Policies.PendingTuition : S.Policies.AnnualTuition;
            float abroad = _sim.Admissions.ExpectedInternational();
            SetText(_adxNextSeason, $"Next season: about {_sim.Admissions.ExpectedApplicants(tuition):0} families" + (abroad >= 0.5f ? $", and {abroad:0} from abroad" : ""));
            SetText(_adxPrice, $"Prestige {S.Prestige:0} · tuition {Money(tuition)} · fair {Money(_sim.FairTuition)}");
            _adxPrice.EnableInClassList("adx-price--high", tuition > _sim.FairTuition);

            var director = _sim.FindAdmin(AdminRole.AdmissionsDirector);
            Show(_adxDirector, director != null);
            if (director != null)
            {
                var policy = S.AdminPolicies;
                SetText(_adxDirector, $"{director.Name}, your Admissions Director, offers places to students with academics of {policy.AutoAdmitAcademicAbove:0} or more during the season."
                    + (policy.FillRemainingBedsAtDeadline ? " At the deadline they fill any beds left." : ""));
            }
        }

        static string AdxDays(int days) => days == 1 ? "1 day" : $"{days} days";

        static string AdxPercent(float fraction) => Mathf.RoundToInt(fraction * 100f) + "%";
    }
}
