using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The school rules page: the cafeteria menu as four plates, curfew, discipline and the
    /// internet as picture cards whose chips say what each choice does, and home game ticket
    /// prices once the school has a team. The rule in force is highlighted; clicking another
    /// card changes it.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>One choice on the page: a menu, a curfew, a discipline policy or a network level.</summary>
        sealed class RulCard
        {
            public int Option;
            public InkBox Root;
            public VisualElement Chips;
            public Label Meal, Cost, Mood;
        }

        static readonly string[] RulNetNames = { "Basic wifi", "Fast wifi", "Fibre" };
        static readonly Dictionary<string, InkDrawing> RulArt = new Dictionary<string, InkDrawing>();

        // Effects from the core: curfew moves student life by 6 and mood by 4 (night owls feel it
        // more), discipline moves safety by 6 and mood by 3 or 4; incident odds scale by the percent.
        static readonly (string art, string name, (string text, int tone)[] chips)[] RulCurfews =
        {
            ("moon-lock", nameof(CurfewPolicy.Strict), new[] { ("fun –6", -1), ("mood –4", -1), ("trouble –20%", 1) }),
            ("moon", nameof(CurfewPolicy.Normal), new[] { ("no change", 0) }),
            ("moon-stars", nameof(CurfewPolicy.Relaxed), new[] { ("fun +6", 1), ("mood +4", 1), ("trouble +30%", -1) }),
        };

        static readonly (string art, string name, (string text, int tone)[] chips)[] RulDisciplines =
        {
            ("feather", nameof(DisciplinePolicy.Lenient), new[] { ("safety –6", -1), ("mood +3", 1), ("trouble +30%", -1), ("habits slip", -1) }),
            ("scale", nameof(DisciplinePolicy.Balanced), new[] { ("no change", 0) }),
            ("gavel", nameof(DisciplinePolicy.Strict), new[] { ("safety +6", 1), ("mood –4", -1), ("trouble –25%", 1), ("habits improve", 1) }),
        };

        readonly List<RulCard> _rulFood = new List<RulCard>();
        readonly List<RulCard> _rulCurfew = new List<RulCard>();
        readonly List<RulCard> _rulDiscipline = new List<RulCard>();
        readonly List<RulCard> _rulNet = new List<RulCard>();
        VisualElement _rulRoot, _rulTickets;
        Label _rulFoodNow, _rulFunNow, _rulSafetyNow, _rulNetNow, _rulTicketPrice, _rulTicketNote;
        MonTrack _rulTicketTrack;
        int _rulSignature;

        partial void BuildRulesPage()
        {
            RulDefineIcons();
            _rulFood.Clear();
            _rulCurfew.Clear();
            _rulDiscipline.Clear();
            _rulNet.Clear();
            var scroll = MonPageScroll("rul-scroll");

            scroll.Add(RulRow("rul-meal", "Food menu", "three meals a day; the kitchen and chefs matter too", "apple", out _rulFoodNow, out var plates));
            foreach (FoodTier tier in Enum.GetValues(typeof(FoodTier)))
            {
                var t = tier;
                var card = RulPlate(t);
                OnClick(card.Root, () =>
                {
                    if (S.Policies.Food != t) RunCommand(() => _sim.SetFoodTier(t));
                });
                plates.Add(card.Root);
                _rulFood.Add(card);
            }

            scroll.Add(RulRow(null, "Curfew", "lights out; night owls feel it most", "music", out _rulFunNow, out var curfews));
            RulChoices(curfews, _rulCurfew, RulCurfews, () => (int)S.Policies.Curfew, i => RunCommand(() => _sim.SetCurfew((CurfewPolicy)i)));

            scroll.Add(RulRow(null, "Discipline", "when kids misbehave; troublemakers hate strict rules", "shield", out _rulSafetyNow, out var disciplines));
            RulChoices(disciplines, _rulDiscipline, RulDisciplines, () => (int)S.Policies.Discipline, i => RunCommand(() => _sim.SetDiscipline((DisciplinePolicy)i)));

            scroll.Add(RulRow(null, "Internet", "for dorms and labs; slow networks go down more", "rul-wifi", out _rulNetNow, out var nets));
            for (int level = 1; level <= RulNetNames.Length; level++)
            {
                int l = level;
                var card = RulOption("wifi" + level, RulNetNames[level - 1]);
                card.Option = level;
                OnClick(card.Root, () =>
                {
                    if (S.Policies.InternetLevel == l - 1) RunCommand(() => _sim.UpgradeInternet());
                });
                nets.Add(card.Root);
                _rulNet.Add(card);
            }

            _rulTickets = RulRow("rul-ticket", "Home games", "what fans pay at the gate", null, out _, out var tickets);
            var ticketCard = Ink("rul-card", "rul-ticket-card");
            ticketCard.Add(MonStepper("minus", () => RulSetTicket(S.Policies.TicketPrice - 1f), false));
            _rulTicketPrice = Text("", "font-hand", "rul-price");
            ticketCard.Add(_rulTicketPrice);
            ticketCard.Add(MonStepper("plus", () => RulSetTicket(S.Policies.TicketPrice + 1f), false, "mon-step--b"));
            _rulTicketTrack = new MonTrack(1f)
            {
                Changed = v => SetText(_rulTicketPrice, Money(v)),
                Released = RulSetTicket,
            };
            _rulTicketTrack.SetRange(0f, 40f);
            _rulTicketTrack.Root.AddToClassList("rul-ticket-track");
            ticketCard.Add(_rulTicketTrack.Root);
            _rulTicketNote = Text("", "font-note", "rul-ticket-note");
            ticketCard.Add(_rulTicketNote);
            tickets.Add(ticketCard);
            scroll.Add(_rulTickets);

            _pageHost.Add(scroll);
            _rulRoot = scroll;
        }

        partial void RefreshRulesPage(bool newDay)
        {
            if (_rulRoot == null) return;
            int signature = RulSignature();
            if (!newDay && signature == _rulSignature) return;
            _rulSignature = signature;
            var p = S.Policies;

            float meals = S.Students.Count * Balance.MealsPerDay * Calendar.DaysPerMonth;
            foreach (var card in _rulFood)
            {
                var tier = (FoodTier)card.Option;
                int better = tier.CompareTo(p.Food);
                RulMark(card, better == 0, better != 0);
                SetText(card.Meal, $"${Balance.MealCost(tier):0.00} a meal");
                SetText(card.Cost, S.Students.Count > 0 ? Money(meals * Balance.MealCost(tier)) + " a month" : "no mouths to feed yet");
                // Changing the menu sets the whole school's mood for a while (core: +2 better, –4 worse).
                SetText(card.Mood, better == 0 ? "on the menu now" : better > 0 ? "a treat: mood +2" : "grumbles: mood –4");
                card.Mood.EnableInClassList("rul-mood--up", better > 0);
                card.Mood.EnableInClassList("rul-mood--down", better < 0);
            }
            SetText(_rulFoodNow, $"food {Mathf.RoundToInt(S.Stat(StatId.FoodQuality))} now");

            foreach (var card in _rulCurfew) RulMark(card, card.Option == (int)p.Curfew, card.Option != (int)p.Curfew);
            SetText(_rulFunNow, $"fun {Mathf.RoundToInt(S.Stat(StatId.StudentLife))} now");
            foreach (var card in _rulDiscipline) RulMark(card, card.Option == (int)p.Discipline, card.Option != (int)p.Discipline);
            SetText(_rulSafetyNow, $"safety {Mathf.RoundToInt(S.Stat(StatId.SafetyDiscipline))} now");

            RulShowNet();
            RulShowTickets();
        }

        partial void ResetRulesPage()
        {
            _rulRoot = null;
        }

        int RulSignature()
        {
            var p = S.Policies;
            int h = (int)p.Food;
            h = h * 31 + (int)p.Curfew;
            h = h * 31 + (int)p.Discipline;
            h = h * 31 + p.InternetLevel;
            h = h * 31 + Mathf.RoundToInt(p.TicketPrice);
            h = h * 31 + S.Teams.Count;
            h = h * 31 + S.Students.Count;
            h = h * 31 + (int)S.Specialization;
            h = h * 31 + (S.Finance.Cash >= Balance.InternetUpgradeCost(p.InternetLevel + 1) ? 1 : 0);
            return h;
        }

        // --- Rows and cards -------------------------------------------------------------------

        /// <summary>A rule's row: its name, a margin note and today's value on the left, the choices on the right.</summary>
        static VisualElement RulRow(string icon, string title, string hint, string nowIcon, out Label now, out VisualElement choices)
        {
            var row = Box("rul-row");
            var side = Box("rul-side");
            var head = Box("row");
            if (icon != null) head.Add(Icon(icon, "rul-head-icon"));
            head.Add(Text(title, "font-hand", "rul-title"));
            side.Add(head);
            side.Add(Text(hint, "font-note", "rul-hint"));
            now = null;
            if (nowIcon != null)
            {
                var chip = Box("chip", "rul-now");
                chip.Add(Icon(nowIcon, "chip-icon"));
                now = Text("", "font-heavy");
                chip.Add(now);
                side.Add(chip);
            }
            row.Add(side);
            choices = Box("rul-options");
            row.Add(choices);
            return row;
        }

        static RulCard RulPlate(FoodTier tier)
        {
            var card = new RulCard { Option = (int)tier, Root = Ink("rul-card", "rul-plate") };
            var art = new InkPicture(RulPicture("plate" + (int)tier));
            art.AddToClassList("rul-plate-art");
            card.Root.Add(art);
            card.Root.Add(Text(tier.ToString(), "font-heavy", "rul-name"));
            var line = Box("rul-plate-line");
            var quality = Box("rul-q");
            quality.Add(Icon("apple", "icon-13"));
            quality.Add(Text(Balance.FoodBaseQuality(tier).ToString("0"), "font-heavy", "rul-q-text"));
            line.Add(quality);
            card.Meal = Text("", "font-semi", "rul-cost");
            line.Add(card.Meal);
            card.Root.Add(line);
            card.Cost = Text("", "font-semi", "rul-cost");
            card.Root.Add(card.Cost);
            card.Mood = Text("", "font-note", "rul-mood");
            card.Root.Add(card.Mood);
            card.Root.Add(RulCheck());
            return card;
        }

        static RulCard RulOption(string art, string name)
        {
            var card = new RulCard { Root = Ink("rul-card", "rul-option") };
            var badge = Ink("rul-badge");
            var picture = new InkPicture(RulPicture(art));
            picture.AddToClassList("rul-badge-art");
            badge.Add(picture);
            card.Root.Add(badge);
            var text = Box("rul-option-text");
            text.Add(Text(name, "font-heavy", "rul-name"));
            card.Chips = Box("rul-chips");
            text.Add(card.Chips);
            card.Root.Add(text);
            card.Root.Add(RulCheck());
            return card;
        }

        /// <summary>Adds a row of fixed choices (curfew, discipline) whose chips never change.</summary>
        void RulChoices(VisualElement host, List<RulCard> cards, (string art, string name, (string text, int tone)[] chips)[] choices,
            Func<int> current, Action<int> pick)
        {
            for (int i = 0; i < choices.Length; i++)
            {
                int option = i;
                var (art, name, chips) = choices[i];
                var card = RulOption(art, name);
                card.Option = option;
                foreach (var (text, tone) in chips) card.Chips.Add(RulChip(text, tone));
                OnClick(card.Root, () =>
                {
                    if (current() != option) pick(option);
                });
                host.Add(card.Root);
                cards.Add(card);
            }
        }

        static Label RulChip(string text, int tone)
        {
            var chip = Text(text, "font-heavy", "rul-chip");
            chip.EnableInClassList("rul-chip--up", tone > 0);
            chip.EnableInClassList("rul-chip--down", tone < 0);
            return chip;
        }

        static VisualElement RulCheck()
        {
            var check = Ink("rul-check");
            check.Add(Icon("check", "icon-13", "icon--white"));
            return check;
        }

        static void RulMark(RulCard card, bool on, bool pickable)
        {
            card.Root.EnableInClassList("rul-card--on", on);
            card.Root.EnableInClassList("rul-card--pick", pickable);
        }

        // --- Internet and tickets ---------------------------------------------------------------

        /// <summary>Network levels: only the next one up can be bought; the monthly bill follows the level.</summary>
        void RulShowNet()
        {
            int level = Mathf.Clamp(S.Policies.InternetLevel, 1, RulNetNames.Length);
            float bill = SpecializationRules.InternetCostMultiplier(S.Specialization);
            foreach (var card in _rulNet)
            {
                int l = card.Option;
                RulMark(card, l == level, l == level + 1);
                card.Root.EnableInClassList("rul-card--past", l < level);
                card.Chips.Clear();
                card.Chips.Add(RulChip(Money(Balance.InternetMonthlyCost(l) * bill) + "/mo", 0));
                if (l > level)
                {
                    float cost = Balance.InternetUpgradeCost(l);
                    if (l > level + 1) card.Chips.Add(RulChip("after " + RulNetNames[l - 2].ToLowerInvariant(), 0));
                    card.Chips.Add(RulChip(l == level + 1 && S.Finance.Cash < cost ? "can't afford " + Money(cost) : "install " + Money(cost), -1));
                }
                // How often the wi-fi outage letter turns up at this level.
                card.Chips.Add(RulChip(l == 1 ? "outages often" : l == 2 ? "outages sometimes" : "outages rare", l == 1 ? -1 : l == 2 ? 0 : 1));
            }
            SetText(_rulNetNow, $"{RulNetNames[level - 1].ToLowerInvariant()} now");
        }

        void RulShowTickets()
        {
            bool teams = S.Teams.Count > 0;
            Show(_rulTickets, teams);
            if (!teams) return;
            float price = S.Policies.TicketPrice;
            SetText(_rulTicketPrice, Money(price));
            _rulTicketTrack.SetValue(price);
            float now = S.Finance.Current[LedgerCategory.Tickets];
            var last = S.Finance.LastCompletedMonth;
            float before = last != null ? last[LedgerCategory.Tickets] : 0f;
            SetText(_rulTicketNote, now + before < 1f
                ? "fans pay this at every home game"
                : $"ticket sales: {Money(now)} this month, {Money(before)} last month");
        }

        void RulSetTicket(float price)
        {
            price = Mathf.Clamp(Mathf.Round(price), 0f, 100f);
            if (Mathf.Abs(price - S.Policies.TicketPrice) < 0.5f)
            {
                RulShowTickets();
                return;
            }
            RunCommand(() => _sim.SetTicketPrice(price));
        }

        // --- Pictures -----------------------------------------------------------------------------

        static void RulDefineIcons()
        {
            if (InkIcons.Get("rul-meal") != null) return;
            InkIcons.Define("rul-meal", 2f,
                "fill:#FBFDFF M3 14a9 5 0 1 0 18 0a9 5 0 1 0-18 0z",
                "fill:#FFE066 M7 13.5a5 2.5 0 1 0 10 0a5 2.5 0 1 0-10 0z",
                "M5 4v5M7 4v5M6 9v2M18 4c-1.5 1-2 2.5-2 4.5h2V11");
            InkIcons.Define("rul-wifi", 2.4f, "M8.5 15a5 5 0 0 1 7 0M5.5 12a9.2 9.2 0 0 1 13 0M2.5 9a13.5 13.5 0 0 1 19 0", "ink bare circle:12,18.5,1.9");
            InkIcons.Define("rul-ticket", 2f, "fill:#FFE066 M4 7.5h16v3a1.5 1.5 0 0 0 0 3v3H4v-3a1.5 1.5 0 0 0 0-3z", "M15 8.5v1.5M15 11.5v1M15 14v1.5");
        }

        static InkDrawing RulPicture(string key)
        {
            if (!RulArt.TryGetValue(key, out var drawing)) RulArt[key] = drawing = RulDraw(key);
            return drawing;
        }

        /// <summary>The design's plates (120 x 70) and rule icons (24 x 24).</summary>
        static InkDrawing RulDraw(string key)
        {
            var ink = InkIcons.Ink;
            var plate = InkIcons.Hex(0xEAF3FD);
            var blue = InkIcons.Hex(0x7FA6D6);
            switch (key)
            {
                case "plate0":
                    return new InkDrawing(120f, 70f)
                        .Shape("M26 30h68c0 18-15 32-34 32S26 48 26 30z", InkIcons.Paper, ink, 2.6f)
                        .Ellipse(60f, 30f, 34f, 7f, InkIcons.Hex(0xFFF3C4), ink, 2.6f)
                        .Line("M84 12l-14 20", ink, 3f)
                        .Line("M46 29c3-2 6 1 9-1", ink, 1.8f);
                case "plate1":
                    return new InkDrawing(120f, 70f)
                        .Ellipse(60f, 44f, 48f, 20f, InkIcons.Paper, ink, 2.6f)
                        .Ellipse(60f, 42f, 34f, 12f, plate, ink, 1.6f)
                        .Shape("M38 42c0-6 8-9 16-7 4 1 5 6 1 8-5 2-17 3-17-1z", InkIcons.Paper, ink, 2.6f)
                        .Circle(70f, 38f, 7f, blue, ink, 2.6f)
                        .Shape("M76 46c4-4 10-4 12 0-4 3-8 3-12 0z", InkIcons.Sky, ink, 2.6f);
                case "plate2":
                    return new InkDrawing(120f, 70f)
                        .Ellipse(54f, 46f, 44f, 18f, InkIcons.Paper, ink, 2.6f)
                        .Ellipse(54f, 44f, 31f, 11f, plate, ink, 1.6f)
                        .Shape("M32 44c0-6 8-8 15-6 4 1 4 6 0 7-5 2-15 3-15-1z", InkIcons.Paper, ink, 2.6f)
                        .Circle(62f, 40f, 7f, blue, ink, 2.6f)
                        .Shape("M66 48c4-4 10-4 12 0-4 3-8 3-12 0z", InkIcons.Sky, ink, 2.6f)
                        .Circle(50f, 48f, 3.5f, InkIcons.Coral, ink, 1.6f)
                        .Shape("M96 26h14l-2 30h-10z", InkIcons.Pale, ink, 2.6f)
                        .Line("M97 34h12", ink, 1.8f);
                case "plate3":
                    return new InkDrawing(120f, 70f)
                        .Ellipse(48f, 46f, 40f, 17f, InkIcons.Paper, ink, 2.6f)
                        .Ellipse(48f, 44f, 28f, 10f, plate, ink, 1.6f)
                        .Shape("M28 44c0-6 7-8 14-6 4 1 4 6 0 7-5 2-14 3-14-1z", InkIcons.Paper, ink, 2.6f)
                        .Circle(56f, 40f, 7f, blue, ink, 2.6f)
                        .Circle(46f, 48f, 3.5f, InkIcons.Coral, ink, 1.6f)
                        .Shape("M92 58l-4-18h22l-4 18z", InkIcons.Marker, ink, 2.6f)
                        .Shape("M88 40c0-8 22-8 22 0", InkIcons.Blush, ink, 2.6f)
                        .Circle(99f, 30f, 3.5f, InkIcons.Coral, ink, 1.6f)
                        .Shape("M20 16l2 5 5 2-5 2-2 5-2-5-5-2 5-2z", InkIcons.Marker, ink, 1.6f);
                case "moon-lock":
                    return new InkDrawing(24f, 24f)
                        .Shape("M14.5 3.5a8 8 0 1 0 6 13 6.5 6.5 0 0 1-6-13z", InkIcons.Marker, ink, 1.9f)
                        .Rect(14f, 14f, 7f, 6f, InkIcons.Paper, ink, 1.9f, 1.2f)
                        .Line("M15.5 14v-1.5a2 2 0 0 1 4 0V14", ink, 1.9f);
                case "moon":
                    return new InkDrawing(24f, 24f)
                        .Shape("M14.5 3.5a8.5 8.5 0 1 0 6 14 7 7 0 0 1-6-14z", InkIcons.Marker, ink, 1.9f);
                case "moon-stars":
                    return new InkDrawing(24f, 24f)
                        .Shape("M12.5 4.5a7.5 7.5 0 1 0 6 12 6 6 0 0 1-6-12z", InkIcons.Marker, ink, 1.9f)
                        .Shape("M18.5 3l.8 2 2 .8-2 .8-.8 2-.8-2-2-.8 2-.8zM20 11.5l.5 1.2 1.2.5-1.2.5-.5 1.2-.5-1.2-1.2-.5 1.2-.5z",
                            InkIcons.Paper, ink, 1.3f);
                case "feather":
                    return new InkDrawing(24f, 24f)
                        .Shape("M17.5 5.5c-6 0-11 4.5-11 11v3h3c6.5 0 11-5 11-11z", InkIcons.Pale, ink, 1.9f)
                        .Line("M4.5 20.5l11-11", ink, 1.9f);
                case "scale":
                    return new InkDrawing(24f, 24f)
                        .Line("M12 4v16M7.5 20.5h9M5 7h14", ink, 1.9f)
                        .Shape("M5 7l-2.5 6h5zM19 7l-2.5 6h5z", InkIcons.Marker, ink, 1.9f);
                case "gavel":
                    return new InkDrawing(24f, 24f)
                        .Shape("M8.2 4.6l6.4 6.4-2.8 2.8-6.4-6.4z", InkIcons.Marker, ink, 1.9f)
                        .Line("M13.2 12.4l6.3 6.3M3.5 20.5h9", ink, 1.9f)
                        .Line("M6.8 3.2l8.6 8.6M4.3 5.7l8.6 8.6", ink, 1.9f);
                default:
                {
                    // "wifi1".."wifi3": the bars up to the level in ink, the rest pale.
                    int level = key.Length > 4 ? key[key.Length - 1] - '0' : 1;
                    var off = InkIcons.Hex(0xC3DAF3);
                    return new InkDrawing(24f, 24f)
                        .Circle(12f, 18.5f, 1.6f, ink, ink, 2.4f)
                        .Line("M8.5 15a5 5 0 0 1 7 0", ink, 2.4f)
                        .Line("M5.5 12a9.2 9.2 0 0 1 13 0", level >= 2 ? ink : off, 2.4f)
                        .Line("M2.5 9a13.5 13.5 0 0 1 19 0", level >= 3 ? ink : off, 2.4f);
                }
            }
        }
    }
}
