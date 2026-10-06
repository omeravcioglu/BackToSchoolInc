using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The admissions page's applicant cards: face, grade, how keen the family is, the pitch,
    /// abilities, traits and the aid asked for, with a full-price offer, the aid offer, a custom
    /// scholarship offer or a pass. An answered card shows its stamp for a moment, then goes.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>An applicant's card, kept while the family applies so an open custom offer survives a new day.</summary>
        sealed class AdxCard
        {
            public Applicant Applicant;
            public InkBox Root, Offer, AidOffer, CustomButton, Custom;
            public VisualElement Buttons;
            public Label Expiry, AidChance, Stamp, StampNote;
        }

        /// <summary>An answer just given: the card keeps its stamp for a moment before it leaves.</summary>
        sealed class AdxAnswer
        {
            public Applicant Applicant;
            public string Stamp, Note;
            public bool Welcome;
            public float Until;
        }

        const float AdxStampSeconds = 2.6f;

        static readonly string[] AdxTilts = { "adx-card--a", "adx-card--b", "adx-card--c" };
        static readonly string[] AdxTypeLabels = { "None", "Academic", "Athletic", "Arts", "Aid" };   // ScholarshipType order

        readonly Dictionary<int, AdxCard> _adxCards = new Dictionary<int, AdxCard>();
        readonly List<AdxAnswer> _adxAnswers = new List<AdxAnswer>();
        int _adxCustomId = -1;
        ScholarshipType _adxCustomType;
        float _adxCustomDiscount = 0.25f;

        void AdxRefreshCards()
        {
            // Families still applying plus the ones just answered, best fit first.
            var list = new List<Applicant>(S.Applicants);
            foreach (var answer in _adxAnswers)
                if (!list.Contains(answer.Applicant)) list.Add(answer.Applicant);
            list.Sort((x, y) => _sim.Admissions.Score(y).CompareTo(_sim.Admissions.Score(x)));

            var keep = new HashSet<int>();
            foreach (var a in list) keep.Add(a.Student.Id);
            var gone = new List<int>();
            foreach (int id in _adxCards.Keys)
                if (!keep.Contains(id)) gone.Add(id);
            foreach (int id in gone) _adxCards.Remove(id);
            if (!keep.Contains(_adxCustomId)) _adxCustomId = -1;

            bool beds = _sim.Facilities.FreeBeds() > 0;
            _adxGrid.Clear();
            foreach (var a in list)
            {
                if (!_adxCards.TryGetValue(a.Student.Id, out var card) || card.Applicant != a)
                {
                    card = AdxBuildCard(a);
                    _adxCards[a.Student.Id] = card;
                }
                AdxUpdateCard(card, beds);
                _adxGrid.Add(card.Root);
            }

            int waiting = S.Applicants.Count;
            SetText(_adxCount, waiting == 0 ? "" : waiting == 1 ? "1 family is applying · best fit first" : $"{waiting} families are applying · best fit first");
            Show(_adxEmpty, list.Count == 0);
            if (list.Count == 0) AdxRefreshEmpty();
        }

        /// <summary>
        /// Between application seasons: when families come, plus this year's new arrivals so the
        /// page still has the school's newest faces on it.
        /// </summary>
        VisualElement AdxBuildEmpty()
        {
            _adxEmpty = Ink("adx-empty", "hidden");
            var top = Box("adx-empty-top");
            top.Add(new InkPicture(AdxMailbox()) { pickingMode = PickingMode.Ignore });
            top.ElementAt(0).AddToClassList("adx-empty-art");
            var words = Box("adx-empty-words");
            _adxEmptyTitle = Text("", "font-hand", "adx-empty-title");
            words.Add(_adxEmptyTitle);
            _adxEmptyNote = Text("", "font-note", "adx-empty-note");
            words.Add(_adxEmptyNote);
            top.Add(words);
            _adxEmpty.Add(top);
            var line = new DashLine();
            line.AddToClassList("adx-empty-line");
            _adxEmpty.Add(line);
            _adxArrivalsTitle = Text("", "font-heavy", "adx-arrivals-title");
            _adxEmpty.Add(_adxArrivalsTitle);
            _adxArrivals = Box("adx-arrivals");
            _adxEmpty.Add(_adxArrivals);
            return _adxEmpty;
        }

        void AdxRefreshEmpty()
        {
            bool season = Calendar.IsAdmissionsSeason(S.Day);
            SetText(_adxEmptyTitle, season ? "Every family has an answer" : "No families applying right now");
            SetText(_adxEmptyNote, season
                ? "Transfers start applying again in September. Free beds wait for them."
                : "Transfers trickle in week by week until April, and the big intake comes in July.");

            // This school year's new students, newest first.
            int yearStart = Calendar.StartOfMonth(Calendar.Year(S.Day), 1);
            var arrivals = new List<Student>();
            foreach (var st in S.Students)
                if (st.EnrolledDay >= yearStart && st.EnrolledDay > 0) arrivals.Add(st);
            arrivals.Sort((a, b) => b.EnrolledDay.CompareTo(a.EnrolledDay));
            int sig = arrivals.Count;
            foreach (var st in arrivals) sig = sig * 31 + st.Id;
            if (sig == _adxArrivalsSig) return;
            _adxArrivalsSig = sig;
            SetText(_adxArrivalsTitle, arrivals.Count == 0 ? "No new students yet this school year"
                : arrivals.Count == 1 ? "New this school year · 1 student" : $"New this school year · {arrivals.Count} students");
            _adxArrivals.Clear();
            for (int i = 0; i < arrivals.Count && i < 12; i++)
            {
                var st = arrivals[i];
                var face = Ink("adx-arrival");
                var pic = new PersonPortrait();
                pic.AddToClassList("adx-arrival-pic");
                pic.ShowStudent(st);
                face.Add(pic);
                face.Add(Text(st.FirstName, "font-semi", "adx-arrival-name"));
                face.Add(Text(Calendar.MonthName(Calendar.Month(st.EnrolledDay)), "font-note", "adx-arrival-when"));
                int id = st.Id;
                OnClick(face, () =>
                {
                    _studentFocus = id;
                    ShowPageAt(HudPage.Students);
                });
                _adxArrivals.Add(face);
            }
        }

        static InkDrawing _adxMailbox;

        /// <summary>A mailbox on a post, flag down: nobody has written.</summary>
        static InkDrawing AdxMailbox() => _adxMailbox ??= new InkDrawing(96f, 96f)
            .Shape("M44 52h8v38h-8z", InkIcons.Hex(0xDCEBFB), InkIcons.Ink, 2.5f)
            .Shape("M14 30c0-10 8-17 18-17h34c10 0 16 7 16 17v26H14z", InkIcons.Sky, InkIcons.Ink, 3f)
            .Shape("M14 30c0-10 8-17 18-17s18 7 18 17v26H14z", InkIcons.Pale, InkIcons.Ink, 3f)
            .Line("M24 44h16", InkIcons.Ink, 2.5f)
            .Shape("M70 34h4v18h-4z", InkIcons.Ink)
            .Shape("M74 34h12v8H74z", InkIcons.Marker, InkIcons.Ink, 2f)
            .Line("M34 90h28", InkIcons.Ink, 2.5f);


        AdxCard AdxBuildCard(Applicant a)
        {
            var s = a.Student;
            var card = new AdxCard { Applicant = a, Root = Ink("adx-card", AdxTilts[Mathf.Abs(s.Id) % AdxTilts.Length]) };
            card.Root.Add(TapeStrip("adx-card-tape", s.Id % 2 == 0 ? "tape-yellow" : "tape-sky"));

            var top = Box("adx-card-top");
            var chips = Box("adx-chips");
            chips.Add(AdxChip(a.Transfer ? "transfer" : "new intake", null, "adx-chip--pale"));
            if (s.International) chips.Add(AdxChip(s.HomeCountry, "adx-plane", "adx-chip--sky"));
            if (a.EliteAthlete) chips.Add(AdxChip("star athlete", "star", "adx-chip--gold"));
            if (a.Scholar) chips.Add(AdxChip("national scholar", "star", "adx-chip--gold"));
            top.Add(chips);
            card.Expiry = Text("", "font-note", "adx-expiry");
            top.Add(card.Expiry);
            card.Root.Add(top);

            var who = Box("adx-who");
            who.Add(AdxFace(s));
            var id = Box("adx-id");
            var name = Text("", "font-hand", "adx-name");
            SetHandText(name, s.FullName);
            id.Add(name);
            id.Add(Text($"Grade {s.Grade}", "font-semi", "adx-grade"));
            id.Add(AdxHearts(a.Interest));
            who.Add(id);
            card.Root.Add(who);
            card.Root.Add(Text($"\"{a.Pitch}\"", "font-note", "adx-pitch"));

            var abilities = Box("adx-abilities");
            abilities.Add(AdxAbility("book", "Academic", s.Academic));
            abilities.Add(AdxAbility("adx-run", "Athletic", s.Athletic));
            abilities.Add(AdxAbility("adx-palette", "Artistic", s.Artistic));
            abilities.Add(AdxAbility("adx-ruler", "Discipline", s.Discipline));
            abilities.Add(AdxAbility("smile", "Social", s.Social));
            abilities.Add(AdxAbility("coin", "Family wealth", s.FamilyWealth));
            card.Root.Add(abilities);

            if (s.SportTalent != SportType.None || s.Traits.Count > 0)
            {
                var traits = Box("adx-traits");
                if (s.SportTalent != SportType.None) traits.Add(AdxChip($"{s.SportTalent} talent", "trophy", "adx-chip--gold"));
                foreach (var t in s.Traits) traits.Add(AdxChip(AdxTraitName(t), null, AdxTroublesome(t) ? "adx-chip--blush" : "adx-chip--pale"));
                card.Root.Add(traits);
            }

            var aid = Box("adx-aid");
            aid.Add(Icon("coin", "adx-aid-icon"));
            if (a.RequestedAid > 0f)
            {
                aid.Add(Text("asks for", "font-semi", "adx-aid-text"));
                aid.Add(Text($"{AdxPercent(a.RequestedAid)} off", "font-heavy", "adx-aid-chip"));
            }
            else aid.Add(Text(s.International ? $"pays full price + {AdxPercent(Balance.InternationalFeePremium)}" : "pays full price", "font-semi", "adx-aid-text"));
            card.Root.Add(aid);

            card.Buttons = Box("adx-buttons");
            var first = Box("adx-button-row");
            card.Offer = AdxButton("Offer a place", AdxPercent(_sim.Admissions.AcceptChance(a, 0f, ScholarshipType.None)), true,
                () => AdxOffer(a, ScholarshipType.None, 0f), out _);
            card.Offer.AddToClassList("grow");
            first.Add(card.Offer);
            var pass = AdxButton("Pass", null, false, () => AdxPass(a), out _);
            pass.AddToClassList("adx-gap");
            first.Add(pass);
            card.Buttons.Add(first);

            var second = Box("adx-button-row");
            if (a.RequestedAid > 0f)
            {
                card.AidOffer = AdxButton($"With {AdxPercent(a.RequestedAid)} off", "", false,
                    () => AdxOffer(a, AdxAidType(), a.RequestedAid), out card.AidChance);
                card.AidOffer.AddToClassList("grow");
                second.Add(card.AidOffer);
            }
            card.CustomButton = AdxButton(a.RequestedAid > 0f ? "Custom" : "Custom offer", null, false, () => AdxToggleCustom(card), out _);
            card.CustomButton.AddToClassList(a.RequestedAid > 0f ? "adx-gap" : "grow");
            second.Add(card.CustomButton);
            card.Buttons.Add(second);
            card.Root.Add(card.Buttons);

            card.StampNote = Text("", "font-note", "adx-stamp-note");
            card.Root.Add(card.StampNote);
            card.Stamp = Text("", "font-hand", "adx-stamp");
            card.Stamp.pickingMode = PickingMode.Ignore;
            card.Root.Add(card.Stamp);
            return card;
        }

        void AdxUpdateCard(AdxCard card, bool beds)
        {
            var a = card.Applicant;
            var answer = _adxAnswers.Find(x => x.Applicant == a);
            bool answered = answer != null;
            card.Root.EnableInClassList("adx-card--answered", answered);
            Show(card.Buttons, !answered);
            Show(card.Expiry, !answered);
            Show(card.Stamp, answered);
            Show(card.StampNote, answered);
            if (answered)
            {
                SetText(card.Stamp, answer.Stamp);
                card.Stamp.EnableInClassList("adx-stamp--welcome", answer.Welcome);
                SetText(card.StampNote, answer.Note);
                return;
            }

            int left = a.ExpiresDay - S.Day;
            SetText(card.Expiry, left <= 1 ? "last day!" : left <= 30 ? $"{left} days left" : $"until {MonthDay(a.ExpiresDay)}");
            card.Expiry.EnableInClassList("adx-expiry--soon", left <= 3);
            card.Offer.EnableInClassList("adx-btn--off", !beds);
            if (card.AidOffer != null)
            {
                card.AidOffer.EnableInClassList("adx-btn--off", !beds);
                SetText(card.AidChance, AdxPercent(_sim.Admissions.AcceptChance(a, a.RequestedAid, AdxAidType())));
            }
            AdxRefreshCustom(card);
        }

        // --- Answers --------------------------------------------------------------------------

        /// <summary>Offers a place. The family decides at once: they enroll, or choose another school.</summary>
        void AdxOffer(Applicant a, ScholarshipType type, float discount)
        {
            if (!AdxStillApplying(a)) return;
            RunCommand(() =>
            {
                var result = _sim.Admit(a, type, discount);
                if (!S.Applicants.Contains(a)) AdxStamp(a, result.Ok ? "ENROLLED!" : "SAID NO", result.Message, result.Ok);
                return result;
            });
        }

        void AdxPass(Applicant a)
        {
            if (!AdxStillApplying(a)) return;
            RunCommand(() =>
            {
                var result = _sim.RejectApplicant(a);
                AdxStamp(a, "PASSED", "Their family will look elsewhere.", false);
                return result;
            });
        }

        /// <summary>A card can outlive its family by a moment (the day ticked, the director stepped in): don't answer twice.</summary>
        bool AdxStillApplying(Applicant a)
        {
            if (S.Applicants.Contains(a)) return true;
            ShowToast("That family is no longer applying.", "alert", "bad");
            _pageDay = -1;
            return false;
        }

        void AdxStamp(Applicant a, string stamp, string note, bool welcome)
        {
            _adxAnswers.Add(new AdxAnswer { Applicant = a, Stamp = stamp, Note = note, Welcome = welcome, Until = GameClock.Now + AdxStampSeconds });
        }

        /// <summary>The aid offer uses the Financial Aid scholarship once scholarships unlock, a plain discount before.</summary>
        ScholarshipType AdxAidType() => _sim.IsUnlocked(Feature.Scholarships) ? ScholarshipType.FinancialAid : ScholarshipType.None;

        // --- Custom offer ---------------------------------------------------------------------

        void AdxToggleCustom(AdxCard card)
        {
            int id = card.Applicant.Student.Id, before = _adxCustomId;
            _adxCustomId = before == id ? -1 : id;
            if (_adxCustomId == id && card.Applicant.RequestedAid > 0f) _adxCustomDiscount = card.Applicant.RequestedAid;
            if (before >= 0 && before != id && _adxCards.TryGetValue(before, out var other)) AdxRefreshCustom(other);
            AdxRefreshCustom(card);
        }

        /// <summary>Shows or hides a card's custom offer: scholarship type, discount, the family's chance and the yearly cost.</summary>
        void AdxRefreshCustom(AdxCard card)
        {
            var a = card.Applicant;
            bool open = _adxCustomId == a.Student.Id;
            card.CustomButton.EnableInClassList("adx-btn--on", open);
            if (!open)
            {
                card.Custom?.RemoveFromHierarchy();
                card.Custom = null;
                return;
            }
            if (card.Custom == null)
            {
                card.Custom = Ink("adx-custom");
                card.Buttons.Add(card.Custom);
            }
            var panel = card.Custom;
            panel.Clear();
            panel.Add(Text("your own offer", "font-note", "adx-custom-title"));

            var types = Box("adx-types");
            for (int i = 0; i < AdxTypeLabels.Length; i++)
            {
                var type = (ScholarshipType)i;
                var pill = Text(AdxTypeLabels[i], "font-heavy", "adx-type");
                pill.EnableInClassList("adx-type--on", type == _adxCustomType);
                OnClick(pill, () =>
                {
                    _adxCustomType = type;
                    AdxRefreshCustom(card);
                });
                types.Add(pill);
            }
            panel.Add(types);

            var discount = Box("adx-discount");
            discount.Add(AdxStep("minus", () => AdxNudge(card, -0.05f)));
            discount.Add(Text($"{AdxPercent(_adxCustomDiscount)} off", "font-hand", "adx-discount-value"));
            discount.Add(AdxStep("plus", () => AdxNudge(card, 0.05f)));
            discount.Add(Text($"{Money(S.Policies.AnnualTuition * _adxCustomDiscount)} a year", "font-semi", "adx-discount-cost"));
            panel.Add(discount);
            if (_adxCustomType != ScholarshipType.None)
                panel.Add(Text("a named scholarship: +10% chance, and a happier family", "font-note", "adx-custom-hint"));

            bool can = _sim.Admissions.CanAdmit(a, _adxCustomType, out string why);
            if (!can) panel.Add(Text(why, "font-semi", "adx-why"));
            var type0 = _adxCustomType;
            float discount0 = _adxCustomDiscount;
            var make = AdxButton("Make the offer", AdxPercent(_sim.Admissions.AcceptChance(a, discount0, type0)), true,
                () => AdxOffer(a, type0, discount0), out _);
            make.AddToClassList("adx-make");
            make.EnableInClassList("adx-btn--off", !can);
            panel.Add(make);
        }

        void AdxNudge(AdxCard card, float step)
        {
            _adxCustomDiscount = Mathf.Clamp01(Mathf.Round((_adxCustomDiscount + step) * 20f) / 20f);
            AdxRefreshCustom(card);
        }

        // --- Pieces ---------------------------------------------------------------------------

        /// <summary>A compact notebook button; <paramref name="pill"/> (when not null) is a small yellow tag, the family's chance to say yes.</summary>
        static InkBox AdxButton(string label, string pill, bool primary, Action onClick, out Label pillLabel)
        {
            var b = Ink("adx-btn");
            if (primary) b.AddToClassList("adx-btn--primary");
            b.Add(Text(label, "font-heavy", "adx-btn-label"));
            pillLabel = null;
            if (pill != null)
            {
                pillLabel = Text(pill, "font-heavy", "adx-btn-pill");
                b.Add(pillLabel);
            }
            OnClick(b, onClick);
            return b;
        }

        static InkBox AdxStep(string icon, Action onClick)
        {
            var b = Ink("adx-step");
            b.Add(Icon(icon, "icon-13"));
            OnClick(b, onClick);
            return b;
        }

        /// <summary>The student's face in a round frame, cropped at the shoulders, with the ink ring drawn on top.</summary>
        static VisualElement AdxFace(Student s)
        {
            var frame = Ink("adx-face");
            var portrait = new PersonPortrait();
            portrait.AddToClassList("adx-portrait");
            portrait.ShowStudent(s);
            frame.Add(portrait);
            var ring = Ink("adx-face-ring");
            ring.pickingMode = PickingMode.Ignore;
            frame.Add(ring);
            return frame;
        }

        /// <summary>Five hearts for how keen the family is (their chance to take a full-price offer).</summary>
        static VisualElement AdxHearts(float interest)
        {
            var row = Box("adx-hearts");
            int hearts = Mathf.Clamp(Mathf.RoundToInt(interest * 5f + 0.01f), 1, 5);
            for (int i = 0; i < 5; i++)
            {
                var heart = Icon(i < hearts ? "heart-red" : "adx-heart-off", "adx-heart");
                if (i >= hearts) heart.AddToClassList("adx-heart--off");
                row.Add(heart);
            }
            row.Add(Text(interest >= 0.75f ? "very keen" : interest >= 0.55f ? "keen" : interest >= 0.35f ? "not sure yet" : "doubtful", "font-note", "adx-keen"));
            return row;
        }

        static VisualElement AdxAbility(string icon, string name, float value)
        {
            var row = Box("adx-ability");
            row.Add(Icon(icon, "adx-ability-icon"));
            row.Add(Text(name, "font-semi", "adx-ability-name"));
            var bar = Box("bar", "adx-ability-bar");
            var fill = Box("bar-fill");
            if (value >= 80f) fill.AddToClassList("adx-fill--star");
            SetPercent(fill, value);
            bar.Add(fill);
            row.Add(bar);
            row.Add(Text(Mathf.RoundToInt(value).ToString(), "font-heavy", "adx-ability-value"));
            return row;
        }

        static VisualElement AdxChip(string text, string icon, string tone)
        {
            var chip = Box("adx-chip", tone);
            if (icon != null) chip.Add(Icon(icon, "adx-chip-icon"));
            chip.Add(Text(text, "font-heavy", "adx-chip-text"));
            return chip;
        }

        static string AdxTraitName(StudentTrait t) => t switch
        {
            StudentTrait.CleanFreak => "Clean freak",
            StudentTrait.NightOwl => "Night owl",
            _ => t.ToString(),
        };

        static bool AdxTroublesome(StudentTrait t) =>
            t == StudentTrait.Troublemaker || t == StudentTrait.Anxious || t == StudentTrait.Lazy || t == StudentTrait.Messy;

        static void AdxDefineIcons()
        {
            InkIcons.Define("adx-plane", 1.8f, "fill:#DCEBFB M21 15.5v-2l-8-5V4a1.5 1.5 0 0 0-3 0v4.5l-8 5v2l8-2.5V18l-2 1.5V21l3.5-1 3.5 1v-1.5L13 18v-5z");
            InkIcons.Define("adx-run", 2.2f, "circle:14,4.5,2", "M8 21l3-6 3 2v5M14 17l-1-6-4 1-2 4M13 11l3 2 3-1");
            InkIcons.Define("adx-palette", 1.9f,
                "fill:#FFE066 M12 3.5c-5 0-8.5 3.6-8.5 8.2 0 4.4 3.4 8.8 8 8.8 1.6 0 2.3-.9 2.3-2 0-1.4-1.4-1.8-1.4-3 0-1 .8-1.6 2-1.6h2.3c2 0 3.8-1.5 3.8-3.7 0-3.9-3.6-6.7-8.5-6.7z",
                "bare ink circle:8,11,1.3", "bare ink circle:11.3,7.4,1.3", "bare ink circle:15.6,8.2,1.3");
            InkIcons.Define("adx-ruler", 1.9f, "fill:#DCEBFB rect:2.5,7.5,19,9,1.5", "M6.5 7.5v3M10 7.5v4.5M13.5 7.5v3M17 7.5v4.5");
            InkIcons.Define("adx-heart-off", 2f,
                "fill:#FBFDFF M12 20s-7.4-4.3-7.4-9.7c0-2.4 1.8-4.2 4-4.2 1.5 0 2.7.8 3.4 2 .7-1.2 1.9-2 3.4-2 2.2 0 4 1.8 4 4.2C19.4 15.7 12 20 12 20z");
            InkIcons.Define("adx-bed-taken", 2f, "fill:#1E3A6D M2 19V6M2 15h20v4M22 15v-2.5A2.5 2.5 0 0 0 19.5 10H10v5", "fill:#FFE066 circle:6,12,2");
            InkIcons.Define("adx-bed-free", 2f, "fill:#DCEBFB M2 19V6M2 15h20v4M22 15v-2.5A2.5 2.5 0 0 0 19.5 10H10v5", "fill:#FBFDFF circle:6,12,2");
        }
    }
}
