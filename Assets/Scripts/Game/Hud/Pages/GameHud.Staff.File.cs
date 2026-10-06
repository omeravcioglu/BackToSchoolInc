using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The file beside the staff roster. For someone on the team: polaroid, traits, contract,
    /// skills, morale and why, energy, pay against the market with raises, cuts and a pay stepper,
    /// training courses, and letting them go. For a candidate: skills, asking pay against the
    /// market, the contract length and the hire button.
    /// </summary>
    public sealed partial class GameHud
    {
        static readonly int[] StfTerms = { 1, 3, 5 };
        static readonly string[] StfQuickNames = { "+5%", "+10%", "Match market", "–5%" };

        int _stfYears = 1, _stfFireArm = -1;
        float _stfFireUntil, _stfDraft = -1f;
        StaffMember _stfShown;

        InkBox _stfFile, _stfBanner, _stfMoraleCard, _stfHireNote, _stfPay, _stfTrain, _stfOffer, _stfFireBtn, _stfSetBtn, _stfHireBtn;
        VisualElement _stfTraits, _stfTeamFacts, _stfHireFacts, _stfBars, _stfCourses;
        ScrollView _stfFileScroll, _stfWhy;
        PersonPortrait _stfPhoto;
        StfGauge _stfGauge;
        InkIcon _stfBannerIcon;
        Label _stfFileEmpty, _stfCaption, _stfName, _stfMeta, _stfBannerText, _stfEffect, _stfMoraleValue, _stfEnergyText;
        Label _stfContract, _stfLoyalty, _stfFireLabel, _stfFireNote, _stfMarketUntil, _stfPrestige;
        Label _stfPayNote, _stfPayYou, _stfPayMarket, _stfDraftLabel, _stfDraftNote, _stfTrainNote;
        Label _stfOfferNote, _stfAsk, _stfAskMarket, _stfHireLabel, _stfHireWhy;
        VisualElement _stfPayYouFill, _stfPayMarketFill, _stfAskFill, _stfAskMarketFill;
        readonly InkIcon[] _stfSkillStars = new InkIcon[5];
        readonly VisualElement[] _stfCells = new VisualElement[5];
        readonly Label[] _stfQuickSubs = new Label[4];
        readonly InkBox[] _stfTermTiles = new InkBox[3];
        readonly Label[] _stfTermPays = new Label[3];
        readonly Label[] _stfTermNotes = new Label[3];

        void StfBuildFile(VisualElement parent)
        {
            _stfShown = null;
            _stfFile = Ink("stf-file");
            _stfFileEmpty = Text("", "font-note", "empty-note", "stf-file-empty");
            _stfFile.Add(_stfFileEmpty);
            _stfFileScroll = new ScrollView(ScrollViewMode.Vertical);
            _stfFileScroll.AddToClassList("stf-file-scroll");
            _stfFile.Add(_stfFileScroll);
            var body = Box("stf-file-body");
            _stfFileScroll.Add(body);

            // Who they are
            var top = Box("stf-top");
            var polaroid = Ink("stf-polaroid");
            var photo = Box("stf-photo");
            _stfPhoto = new PersonPortrait();
            _stfPhoto.AddToClassList("stf-photo-img");
            photo.Add(_stfPhoto);
            polaroid.Add(photo);
            _stfCaption = Text("", "font-note", "stf-caption");
            polaroid.Add(_stfCaption);
            top.Add(polaroid);

            var id = Box("stf-id");
            _stfName = Text("", "font-hand", "stf-name");
            id.Add(_stfName);
            _stfMeta = Text("", "font-semi", "stf-meta");
            id.Add(_stfMeta);
            _stfTraits = Box();
            id.Add(_stfTraits);
            _stfBanner = Ink("stf-banner");
            _stfBannerIcon = Icon("stf-cap", "icon-18");
            _stfBanner.Add(_stfBannerIcon);
            _stfBannerText = Text("", "font-heavy", "stf-banner-text");
            _stfBanner.Add(_stfBannerText);
            id.Add(_stfBanner);
            top.Add(id);

            var facts = Box("stf-facts");
            var skill = Box("stf-fact");
            skill.Add(Text("skill", "font-semi", "stf-fact-text"));
            var stars = Box("stf-stars");
            for (int i = 0; i < _stfSkillStars.Length; i++)
            {
                _stfSkillStars[i] = Icon("star", "stf-star");
                stars.Add(_stfSkillStars[i]);
            }
            skill.Add(stars);
            facts.Add(skill);
            _stfTeamFacts = Box();
            _stfContract = StfFact(_stfTeamFacts, "stf-calendar");
            _stfLoyalty = StfFact(_stfTeamFacts, "heart");
            _stfFireBtn = Ink("stf-fire");
            _stfFireBtn.Add(Icon("door", "icon-18", "stf-fire-icon"));
            _stfFireLabel = Text("", "font-heavy", "stf-fire-label");
            _stfFireBtn.Add(_stfFireLabel);
            _stfFireNote = Text("", "font-note", "stf-fire-note");
            _stfFireBtn.Add(_stfFireNote);
            OnClick(_stfFireBtn, StfFireClick);
            _stfTeamFacts.Add(_stfFireBtn);
            facts.Add(_stfTeamFacts);
            _stfHireFacts = Box();
            _stfMarketUntil = StfFact(_stfHireFacts, "stf-calendar");
            _stfPrestige = StfFact(_stfHireFacts, "crest");
            facts.Add(_stfHireFacts);
            top.Add(facts);
            body.Add(top);

            var mid = Box("stf-mid");
            var colA = Box("stf-col-a");
            var colB = Box("stf-col-b");

            var skills = Ink("stf-card");
            skills.Add(StfCardHead("Skills", out _));
            _stfBars = Box();
            skills.Add(_stfBars);
            _stfEffect = Text("", "font-heavy", "chip", "chip--good", "stf-effect");
            skills.Add(_stfEffect);
            colA.Add(skills);

            // Morale: the gauge and energy battery, and what the mood is made of.
            _stfMoraleCard = Ink("stf-card", "stf-morale");
            var dials = Box("stf-dials");
            var gauge = Box("stf-gauge-box");
            _stfGauge = new StfGauge();
            _stfGauge.AddToClassList("stf-gauge");
            gauge.Add(_stfGauge);
            _stfMoraleValue = Text("", "font-hand", "stf-gauge-value");
            gauge.Add(_stfMoraleValue);
            dials.Add(gauge);
            dials.Add(Text("morale", "font-semi", "stf-dial-label"));
            var battery = Box("stf-battery");
            for (int i = 0; i < _stfCells.Length; i++)
            {
                _stfCells[i] = Box("stf-cell");
                battery.Add(_stfCells[i]);
            }
            battery.Add(Box("stf-nub"));
            dials.Add(battery);
            _stfEnergyText = Text("", "font-semi", "stf-dial-label");
            dials.Add(_stfEnergyText);
            _stfMoraleCard.Add(dials);
            var why = Box("stf-why-box");
            why.Add(Text("Why?", "font-hand", "stf-card-title"));
            _stfWhy = new ScrollView(ScrollViewMode.Vertical);
            _stfWhy.AddToClassList("stf-why");
            why.Add(_stfWhy);
            _stfMoraleCard.Add(why);
            colA.Add(_stfMoraleCard);

            _stfHireNote = Ink("stf-card", "stf-note-card");
            _stfHireNote.Add(Text("Better candidates want a more prestigious school. Longer contracts are cheaper per month and build loyalty, but cost more to break.",
                "font-note", "stf-note-text"));
            _stfHireNote.Add(TapeStrip("stf-note-tape", "tape-sky"));
            colA.Add(_stfHireNote);

            // Pay against the market, quick raises and cuts, and a stepper for any amount.
            _stfPay = Ink("stf-card");
            _stfPay.Add(StfCardHead("Pay", out _stfPayNote));
            _stfPayYou = StfPayRow(_stfPay, "you pay", false, out _stfPayYouFill);
            _stfPayMarket = StfPayRow(_stfPay, "market", true, out _stfPayMarketFill);
            var quick = Box("stf-quick-row");
            for (int i = 0; i < StfQuickNames.Length; i++)
            {
                int kind = i;
                var chip = Ink("stf-quick");
                if (i == 3) chip.AddToClassList("stf-quick--cut");
                chip.Add(Text(StfQuickNames[i], "font-heavy", "stf-quick-name"));
                _stfQuickSubs[i] = Text("", "font-semi", "stf-quick-sub");
                chip.Add(_stfQuickSubs[i]);
                OnClick(chip, () => StfQuick(kind));
                quick.Add(chip);
            }
            _stfPay.Add(quick);
            var stepper = Box("stf-stepper");
            var minus = Ink("stf-step");
            minus.Add(Icon("minus", "icon-13"));
            OnClick(minus, () => StfStep(-1));
            stepper.Add(minus);
            _stfDraftLabel = Text("", "font-hand", "stf-draft");
            stepper.Add(_stfDraftLabel);
            var plus = Ink("stf-step");
            plus.Add(Icon("plus", "icon-13"));
            OnClick(plus, () => StfStep(1));
            stepper.Add(plus);
            _stfSetBtn = Button(null, "Set pay", StfApplyDraft, small: true);
            _stfSetBtn.AddToClassList("stf-set");
            stepper.Add(_stfSetBtn);
            _stfDraftNote = Text("", "font-note", "stf-draft-note");
            stepper.Add(_stfDraftNote);
            _stfPay.Add(stepper);
            colB.Add(_stfPay);

            _stfTrain = Ink("stf-card");
            _stfTrain.Add(StfCardHead("Training", out _stfTrainNote));
            _stfCourses = Box("stf-courses");
            _stfTrain.Add(_stfCourses);
            colB.Add(_stfTrain);

            // A candidate's offer: asking pay, contract length and the hire button.
            _stfOffer = Ink("stf-card");
            _stfOffer.Add(StfCardHead("Offer", out _stfOfferNote));
            _stfAsk = StfPayRow(_stfOffer, "asks", false, out _stfAskFill);
            _stfAskMarket = StfPayRow(_stfOffer, "market", true, out _stfAskMarketFill);
            var terms = Box("stf-terms");
            for (int i = 0; i < StfTerms.Length; i++)
            {
                int years = StfTerms[i];
                var tile = Ink("stf-term");
                tile.Add(Text(years == 1 ? "1 year" : $"{years} years", "font-heavy", "stf-term-name"));
                _stfTermPays[i] = Text("", "font-hand", "stf-term-pay");
                tile.Add(_stfTermPays[i]);
                _stfTermNotes[i] = Text("", "font-note", "stf-term-note");
                tile.Add(_stfTermNotes[i]);
                OnClick(tile, () =>
                {
                    _stfYears = years;
                    StfShowFile();
                });
                terms.Add(tile);
                _stfTermTiles[i] = tile;
            }
            _stfOffer.Add(terms);
            var hire = Box("stf-hire-row");
            _stfHireBtn = Button(null, "Hire", StfHireClick, primary: true, small: true);
            _stfHireLabel = _stfHireBtn.Q<Label>();
            hire.Add(_stfHireBtn);
            _stfHireWhy = Text("", "font-note", "stf-hire-why");
            hire.Add(_stfHireWhy);
            _stfOffer.Add(hire);
            colB.Add(_stfOffer);

            mid.Add(colA);
            mid.Add(colB);
            body.Add(mid);
            parent.Add(_stfFile);
        }

        static VisualElement StfCardHead(string title, out Label note)
        {
            var head = Box("stf-card-head");
            head.Add(Text(title, "font-hand", "stf-card-title"));
            note = Text("", "font-note", "stf-card-note");
            head.Add(note);
            return head;
        }

        static Label StfFact(VisualElement parent, string icon)
        {
            var fact = Box("stf-fact");
            fact.Add(Icon(icon, "icon-17", "stf-fact-icon"));
            var text = Text("", "font-semi", "stf-fact-text");
            fact.Add(text);
            parent.Add(fact);
            return text;
        }

        static Label StfPayRow(VisualElement parent, string key, bool market, out VisualElement fill)
        {
            var row = Box("stf-pay-row");
            row.Add(Text(key, "font-heavy", "stf-pay-key"));
            var bar = Box("stf-pay-bar");
            if (market) bar.AddToClassList("stf-pay-bar--market");
            fill = Box("bar-fill");
            if (market) fill.AddToClassList("stf-pay-fill--market");
            bar.Add(fill);
            row.Add(bar);
            var value = Text("", "font-hand", "stf-pay-value");
            row.Add(value);
            parent.Add(row);
            return value;
        }

        // --- Showing someone ------------------------------------------------------------------------

        /// <summary>Fills the file for the selected staff member or candidate, falling back when they're gone.</summary>
        void StfShowFile()
        {
            if (_stfFile == null) return;
            bool team = _stfTab == 0;
            StaffMember m;
            Candidate c = null;
            if (team)
            {
                m = S.FindStaff(_stfSelected);
                if (m == null)
                {
                    m = _stfTeamSorted.Count > 0 ? _stfTeamSorted[0] : null;
                    _stfSelected = m?.Id ?? -1;
                    _stfDraft = -1f;
                    _stfFireArm = -1;
                    StfMarkCards();
                }
            }
            else
            {
                c = StfFindCandidate(_stfCandidate);
                if (c == null)
                {
                    c = _stfHireSorted.Count > 0 ? _stfHireSorted[0] : null;
                    _stfCandidate = c?.Staff.Id ?? -1;
                    StfMarkCards();
                }
                m = c?.Staff;
            }

            Show(_stfFileScroll, m != null);
            Show(_stfFileEmpty, m == null);
            if (m == null)
            {
                SetText(_stfFileEmpty, team ? "Nobody works here yet. The job market is one tab over." : "When someone applies for a job, their file opens here.");
                return;
            }
            StfShowWho(m, c);
            StfShowSkills(m, c == null);
            foreach (var e in new VisualElement[] { _stfTeamFacts, _stfMoraleCard, _stfPay, _stfTrain }) Show(e, team);
            foreach (var e in new VisualElement[] { _stfHireFacts, _stfHireNote, _stfOffer }) Show(e, !team);
            if (team)
            {
                StfShowMorale(m);
                StfShowPay(m);
                StfShowTraining(m);
                StfShowFire(m);
            }
            else StfShowOffer(c);
        }

        Candidate StfFindCandidate(int staffId)
        {
            foreach (var c in S.Candidates)
                if (c.Staff.Id == staffId) return c;
            return null;
        }

        void StfShowWho(StaffMember m, Candidate c)
        {
            _stfPhoto.ShowStaff(m);
            SetHandText(_stfName, m.Name);
            string experience = $"{m.Experience:0.0} years of experience";
            if (c == null)
            {
                SetText(_stfCaption, m.HiredDay < 0 ? "here before you" : $"since {Calendar.FormatShort(m.HiredDay)}");
                SetText(_stfMeta, $"{m.Title} · {experience} · hired {(m.HiredDay < 0 ? "before you arrived" : Calendar.Format(m.HiredDay))}");
            }
            else
            {
                SetText(_stfCaption, "looking for work");
                SetText(_stfMeta, $"{m.Title} · {experience}");
            }
            if (_stfShown != m)
            {
                _stfShown = m;
                StfShowTraits(m);
            }
            StfShowStars(_stfSkillStars, m.Skill);

            string banner = null;
            if (c == null && m.LeavingDay >= 0) banner = $"Leaving on {Calendar.Format(m.LeavingDay)}.";
            if (c == null && m.IsAway(S.Day))
            {
                string away = m.CurrentTraining.HasValue
                    ? $"Away at a {m.CurrentTraining.Value} until {Calendar.Format(m.AwayUntilDay)}."
                    : $"On leave until {Calendar.Format(m.AwayUntilDay)}.";
                banner = banner == null ? away : banner + " " + away;
            }
            Show(_stfBanner, banner != null);
            SetText(_stfBannerText, banner);
            _stfBanner.EnableInClassList("stf-banner--bad", m.LeavingDay >= 0);
            _stfBannerIcon.Icon = m.LeavingDay >= 0 ? "door" : m.CurrentTraining.HasValue ? "stf-cap" : "sun";

            if (c == null)
            {
                SetText(_stfContract, $"{m.ContractYears}-year contract, ends {Calendar.Format(m.ContractEndDay)}\n"
                    + "last raise: " + (m.LastRaiseDay < 0 ? "before you arrived" : Calendar.Format(m.LastRaiseDay)));
                SetText(_stfLoyalty, $"loyalty {Mathf.RoundToInt(m.Loyalty)}");
            }
            else
            {
                int left = c.ExpiresDay - S.Day;
                SetText(_stfMarketUntil, $"looking until {Calendar.Format(c.ExpiresDay)} · {(left == 1 ? "1 day" : left + " days")}");
                bool reach = S.Prestige >= c.MinPrestige;
                SetText(_stfPrestige, c.MinPrestige > 0f ? $"wants prestige {c.MinPrestige:0}+ (you: {Mathf.RoundToInt(S.Prestige)})" : "any school will do");
                _stfPrestige.EnableInClassList("stf-bad", !reach);
            }
        }

        void StfShowTraits(StaffMember m)
        {
            _stfTraits.Clear();
            for (int i = 0; i < m.Traits.Count; i++)
            {
                var t = m.Traits[i];
                var row = Box("stf-trait");
                var sticker = Ink("stf-sticker", "stf-sticker--" + StfTraitTone(t, i), i % 2 == 0 ? "tilt-a" : "tilt-d");
                sticker.Add(Text(StfWords(t.ToString()), "font-heavy", "stf-sticker-text"));
                row.Add(sticker);
                row.Add(Text(TraitRules.Describe(t), "font-note", "stf-trait-note"));
                _stfTraits.Add(row);
            }
        }

        /// <summary>Sticker colour: yellow for gifts, blush for trouble, blues for the rest.</summary>
        static string StfTraitTone(StaffTrait t, int index) => t switch
        {
            StaffTrait.ExcellentTeacher or StaffTrait.Motivational or StaffTrait.Loyal or StaffTrait.Popular or StaffTrait.Funny => "good",
            StaffTrait.Lazy or StaffTrait.Difficult or StaffTrait.BurnoutProne => "bad",
            _ => index % 2 == 0 ? "a" : "b",
        };

        /// <summary>The debug page's stat bars for the role, and teaching effectiveness for teachers.</summary>
        void StfShowSkills(StaffMember m, bool hired)
        {
            _stfBars.Clear();
            StfBar(m.Role == StaffRole.Coach ? "Coaching" : "Skill", m.Skill);
            if (m.Role == StaffRole.Teacher)
            {
                StfBar("Classroom mgmt.", m.ClassroomManagement);
                StfBar("Patience", m.Patience);
                StfBar("Charisma", m.Charisma);
            }
            if (m.Role == StaffRole.Coach)
            {
                StfBar("Motivation", m.Charisma);
                StfBar("Strategy", m.Strategy);
                StfBar("Player development", m.PlayerDevelopment);
                StfBar("Recruitment", m.Recruitment);
            }
            bool teacher = m.Role == StaffRole.Teacher;
            Show(_stfEffect, teacher);
            if (teacher) SetText(_stfEffect, $"teaching effectiveness {(hired ? "" : "~")}{Mathf.RoundToInt(_sim.Stats.TeacherEffect(m))}");
        }

        void StfBar(string name, float value)
        {
            var row = Box("stf-bar-row");
            row.Add(Text(name, "font-semi", "stf-bar-name"));
            var bar = Box("bar", "stf-bar");
            var fill = Box("bar-fill");
            SetPercent(fill, value);
            fill.EnableInClassList("bar-fill--low", value < 35f);
            bar.Add(fill);
            row.Add(bar);
            row.Add(Text(Mathf.RoundToInt(value).ToString(), "font-hand", "stf-bar-value"));
            _stfBars.Add(row);
        }

        void StfShowMorale(StaffMember m)
        {
            _stfGauge.SetValue(m.Morale);
            SetText(_stfMoraleValue, Mathf.RoundToInt(m.Morale).ToString());
            _stfMoraleValue.EnableInClassList("stf-bad", m.Morale < 35f);
            int cells = Mathf.Clamp(Mathf.CeilToInt(m.Energy / 20f), 0, _stfCells.Length);
            for (int i = 0; i < _stfCells.Length; i++)
            {
                _stfCells[i].EnableInClassList("stf-cell--on", i < cells);
                _stfCells[i].EnableInClassList("stf-cell--low", i < cells && m.Energy < 25f);
            }
            SetText(_stfEnergyText, $"energy {Mathf.RoundToInt(m.Energy)}");

            _stfWhy.Clear();
            var factors = new System.Collections.Generic.List<Factor>(m.MoraleFactors.Factors);
            factors.Sort((a, b) => Mathf.Abs(b.Value).CompareTo(Mathf.Abs(a.Value)));
            int n = 0;
            foreach (var f in factors)
            {
                if (Mathf.Abs(f.Value) < 0.05f) continue;
                _stfWhy.Add(StfFactorRow(f.Label, StfSigned(f.Value), f.Value > 0f ? 1 : -1));
                n++;
            }
            if (n == 0)
            {
                _stfWhy.Add(Text("Nothing to explain yet: new faces settle in overnight.", "font-note", "stf-why-note"));
                return;
            }
            var line = new DashLine();
            line.AddToClassList("stf-why-line");
            _stfWhy.Add(line);
            _stfWhy.Add(StfFactorRow("Target", Mathf.RoundToInt(m.MoraleFactors.Clamped).ToString(), 0));
            _stfWhy.Add(Text("Morale drifts toward the target a little each day.", "font-note", "stf-why-note"));
        }

        static VisualElement StfFactorRow(string label, string value, int tone)
        {
            var row = Box("stf-factor");
            row.Add(Text(label, tone == 0 ? "font-heavy" : "font-semi", "stf-factor-label"));
            var v = Text(value, "font-heavy", "stf-factor-value");
            v.EnableInClassList("stf-good", tone > 0);
            v.EnableInClassList("stf-bad", tone < 0);
            row.Add(v);
            return row;
        }

        // --- Pay ------------------------------------------------------------------------------------

        void StfShowPay(StaffMember m)
        {
            float market = _sim.Staff.MarketValue(m);
            float scale = Mathf.Max(1f, Mathf.Max(m.Salary, market) * 1.12f);
            SetText(_stfPayYou, Money(m.Salary));
            SetPercent(_stfPayYouFill, 100f * m.Salary / scale);
            SetText(_stfPayMarket, Money(market));
            SetPercent(_stfPayMarketFill, 100f * market / scale);
            float diff = market > 0f ? m.Salary / market - 1f : 0f;
            SetText(_stfPayNote, Mathf.Abs(diff) < 0.02f ? "right at market" : diff < 0f ? $"{StfPct(-diff)} under market" : $"{StfPct(diff)} over market");
            _stfPayNote.EnableInClassList("stf-bad", diff <= -0.02f);
            _stfPayNote.EnableInClassList("stf-good", diff >= 0.02f);
            _stfPayYouFill.EnableInClassList("bar-fill--low", diff <= -0.1f);

            SetText(_stfQuickSubs[0], StfDelta(m.Salary, m.Salary * 1.05f));
            SetText(_stfQuickSubs[1], StfDelta(m.Salary, m.Salary * 1.10f));
            SetText(_stfQuickSubs[2], m.Salary >= market ? "already there" : Money(StfRound(market)));
            SetText(_stfQuickSubs[3], StfDelta(m.Salary, m.Salary * 0.95f));
            StfShowDraft(m);
        }

        /// <summary>The debug page's salary buttons: +5%, +10%, match the market (never a cut) and -5%.</summary>
        void StfQuick(int kind)
        {
            var m = S.FindStaff(_stfSelected);
            if (m == null) return;
            float salary = kind switch
            {
                0 => m.Salary * 1.05f,
                1 => m.Salary * 1.10f,
                2 => Mathf.Max(m.Salary, _sim.Staff.MarketValue(m)),
                _ => m.Salary * 0.95f,
            };
            StfSetSalary(m, salary);
        }

        void StfSetSalary(StaffMember m, float salary)
        {
            int id = m.Id;
            _stfDraft = -1f;
            RunCommand(() => _sim.SetSalary(id, salary));
        }

        /// <summary>The stepper instead of the debug page's salary text field: $50 notches, applied with "Set pay".</summary>
        void StfStep(int direction)
        {
            var m = S.FindStaff(_stfSelected);
            if (m == null) return;
            float now = _stfDraft < 0f ? m.Salary : _stfDraft;
            float next = direction > 0 ? Mathf.Floor(now / 50f) * 50f + 50f : Mathf.Ceil(now / 50f) * 50f - 50f;
            _stfDraft = Mathf.Clamp(next, 500f, 50000f);   // the school can't pay less than $500
            StfShowDraft(m);
        }

        void StfShowDraft(StaffMember m)
        {
            float draft = _stfDraft < 0f ? m.Salary : _stfDraft;
            SetText(_stfDraftLabel, Money(draft));
            float delta = StfRound(draft) - m.Salary;
            bool same = Mathf.Abs(delta) < 1f;
            SetText(_stfDraftNote, same ? "or pick any amount" : delta > 0f ? $"+{Money(delta)}/mo · a raise" : $"–{Money(-delta)}/mo · morale and loyalty drop");
            _stfDraftNote.EnableInClassList("stf-bad", !same && delta < 0f);
            _stfSetBtn.EnableInClassList("stf-off", same);
        }

        void StfApplyDraft()
        {
            var m = S.FindStaff(_stfSelected);
            if (m == null || _stfDraft < 0f || Mathf.Abs(StfRound(_stfDraft) - m.Salary) < 1f) return;
            StfSetSalary(m, _stfDraft);
        }

        // --- Training and letting go ----------------------------------------------------------------

        void StfShowTraining(StaffMember m)
        {
            _stfCourses.Clear();
            string firstWhy = null;
            foreach (TrainingProgram p in Enum.GetValues(typeof(TrainingProgram)))
            {
                bool ok = _sim.Staff.CanTrain(m, p, out string why);
                firstWhy ??= why;
                var course = Ink("stf-course");
                course.EnableInClassList("stf-off", !ok);
                course.Add(Text(p.ToString(), "font-heavy", "stf-course-name"));
                course.Add(Text($"{MoneyShort(_sim.Staff.TrainingCost(p))} · {Balance.TrainingDays(p)} days", "font-semi", "stf-course-cost"));
                course.Add(Text(StfCourseEffect(p), "font-note", "stf-course-effect"));
                var program = p;
                int id = m.Id;
                OnClick(course, () => RunCommand(() => _sim.Train(id, program)));
                _stfCourses.Add(course);
            }

            string note;
            bool bad = false;
            if (m.IsAway(S.Day) && m.CurrentTraining.HasValue) note = $"at a {m.CurrentTraining.Value}, back {MonthDay(m.AwayUntilDay)}";
            else if (firstWhy != null)
            {
                note = firstWhy.ToLowerInvariant();
                bad = true;
            }
            else note = StfAwayTraining(m);
            SetText(_stfTrainNote, note);
            _stfTrainNote.EnableInClassList("stf-bad", bad);
        }

        static string StfCourseEffect(TrainingProgram p) => p switch
        {
            TrainingProgram.Workshop => "+skill, energy",
            TrainingProgram.Seminar => "+classroom mgmt, morale",
            _ => "big +skill; raises their market value",
        };

        /// <summary>Who else is off at a course right now.</summary>
        string StfAwayTraining(StaffMember except)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var s in S.Staff)
                if (s != except && s.IsAway(S.Day) && s.CurrentTraining.HasValue) names.Add($"{s.Name} ({s.CurrentTraining.Value})");
            if (names.Count == 0) return "nobody is away training";
            return "away training: " + (names.Count <= 2 ? string.Join(", ", names) : $"{names[0]}, {names[1]} +{names.Count - 2}");
        }

        void StfShowFire(StaffMember m)
        {
            bool armed = _stfFireArm == m.Id && GameClock.Now < _stfFireUntil;
            _stfFireBtn.EnableInClassList("stf-fire--armed", armed);
            SetText(_stfFireLabel, armed ? "Sure? Again" : "Let go…");
            // What the fire command does besides the severance: a popular teacher upsets the students,
            // and colleagues feel it (or are relieved to see a difficult one go).
            string after = m.Has(StaffTrait.Popular) && m.Role == StaffRole.Teacher ? "students will be upset"
                : m.Has(StaffTrait.Difficult) ? "colleagues relieved" : "colleagues upset";
            SetText(_stfFireNote, armed ? after : $"costs {Money(_sim.Staff.Severance(m))}");
        }

        /// <summary>Letting someone go takes two clicks within three seconds, like the debug page's confirm button.</summary>
        void StfFireClick()
        {
            var m = S.FindStaff(_stfSelected);
            if (m == null) return;
            if (_stfFireArm == m.Id && GameClock.Now < _stfFireUntil)
            {
                _stfFireArm = -1;
                int id = m.Id;
                RunCommand(() => _sim.Fire(id));
                return;
            }
            _stfFireArm = m.Id;
            _stfFireUntil = GameClock.Now + 3f;
            StfShowFire(m);
        }

        /// <summary>The cheap refresh between days: lets an unanswered "let go" confirmation lapse.</summary>
        void StfTick()
        {
            if (_stfFireArm < 0 || GameClock.Now < _stfFireUntil) return;
            _stfFireArm = -1;
            var m = S.FindStaff(_stfSelected);
            if (m != null && _stfTab == 0) StfShowFire(m);
        }

        // --- Hiring ----------------------------------------------------------------------------------

        void StfShowOffer(Candidate c)
        {
            float market = _sim.Staff.MarketValue(c.Staff);
            float scale = Mathf.Max(1f, Mathf.Max(c.AskingSalary, market) * 1.12f);
            SetText(_stfAsk, Money(c.AskingSalary));
            SetPercent(_stfAskFill, 100f * c.AskingSalary / scale);
            SetText(_stfAskMarket, Money(market));
            SetPercent(_stfAskMarketFill, 100f * market / scale);
            float diff = market > 0f ? c.AskingSalary / market - 1f : 0f;
            SetText(_stfOfferNote, Mathf.Abs(diff) < 0.02f ? "asks the market rate" : diff > 0f ? $"asks {StfPct(diff)} over market" : $"asks {StfPct(-diff)} under market");

            for (int i = 0; i < StfTerms.Length; i++)
            {
                int years = StfTerms[i];
                float factor = StaffSystem.ContractSalaryFactor(years);
                SetText(_stfTermPays[i], Money(StfRound(c.AskingSalary * factor)) + "/mo");
                SetText(_stfTermNotes[i], factor >= 1f ? "full asking pay" : $"{StfPct(1f - factor)} cheaper · {(i == StfTerms.Length - 1 ? "most" : "more")} loyal");
                _stfTermTiles[i].EnableInClassList("stf-term--on", years == _stfYears);
            }

            bool can = _sim.Staff.CanHire(c, out string why);
            _stfHireBtn.EnableInClassList("btn--disabled", !can);
            SetText(_stfHireLabel, $"Hire · {Money(StfRound(c.AskingSalary * StaffSystem.ContractSalaryFactor(_stfYears)))}/mo");
            SetText(_stfHireWhy, why ?? $"until {Calendar.FormatShort(S.Day + _stfYears * Calendar.DaysPerYear)}");
            _stfHireWhy.EnableInClassList("stf-bad", !can);
        }

        void StfHireClick()
        {
            var c = StfFindCandidate(_stfCandidate);   // still on the market?
            if (c == null) return;
            int years = _stfYears;
            RunCommand(() => _sim.Hire(c, years));
        }

        // --- Small helpers ----------------------------------------------------------------------------

        /// <summary>Salaries change in $10 steps (the staff system rounds them), so previews do too.</summary>
        static float StfRound(float salary) => Mathf.Round(salary / 10f) * 10f;

        static string StfDelta(float from, float to)
        {
            float d = StfRound(to) - from;
            return (d < 0f ? "–" : "+") + Money(Mathf.Abs(d)) + "/mo";
        }

        static string StfPct(float fraction) => Mathf.RoundToInt(fraction * 100f) + "%";

        static string StfSigned(float v) => (v < 0f ? "–" : "+") + Mathf.Abs(v).ToString("0.0");

        /// <summary>"ExcellentTeacher" to "Excellent teacher".</summary>
        static string StfWords(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char ch = name[i];
                if (i > 0 && char.IsUpper(ch))
                {
                    sb.Append(' ');
                    ch = char.ToLowerInvariant(ch);
                }
                sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>The morale dial from the design: a half-circle band that fills from the left.</summary>
        sealed class StfGauge : VisualElement
        {
            static readonly Color Track = InkIcons.Hex(0xEAF3FD);

            float _value = -1f;

            public StfGauge()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void SetValue(float value)
            {
                value = Mathf.Clamp(value, 0f, 100f);
                if (Mathf.Abs(value - _value) < 0.2f) return;
                _value = value;
                MarkDirtyRepaint();
            }

            void Draw(MeshGenerationContext ctx)
            {
                float w = layout.width, h = layout.height;
                if (w < 8f || h < 8f || float.IsNaN(w) || float.IsNaN(h)) return;
                // The design's 120 x 70 box: centre (60, 62), radius 48, an 18 px ink band around a 13 px track.
                float s = Mathf.Min(w / 120f, h / 70f);
                var c = new Vector2(w / 2f, (h - 70f * s) / 2f + 62f * s);
                float r = 48f * s;
                var p = ctx.painter2D;
                p.lineCap = LineCap.Round;
                p.strokeColor = InkIcons.Ink;
                p.lineWidth = 18f * s;
                p.BeginPath();
                p.Arc(c, r, 180f, 360f);
                p.Stroke();
                p.strokeColor = Track;
                p.lineWidth = 13f * s;
                p.BeginPath();
                p.Arc(c, r, 180f, 360f);
                p.Stroke();
                if (_value < 0.5f) return;
                p.strokeColor = _value < 35f ? InkIcons.Coral : InkIcons.Mid;
                p.BeginPath();
                p.Arc(c, r, 180f, 180f + 180f * _value / 100f);
                p.Stroke();
            }
        }
    }
}
