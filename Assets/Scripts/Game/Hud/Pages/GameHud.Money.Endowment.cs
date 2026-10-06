using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The money page's endowment (from school level 16): the fund, what it pays each September and
    /// the prestige it lends, with gifts of three sizes. Money given to it is given for good.
    /// </summary>
    public sealed partial class GameHud
    {
        static readonly float[] MonGifts = { 50000f, 250000f, 1000000f };
        static InkDrawing _monTree;

        InkBox _monEndowment;
        Label _monFund, _monFundNote, _monGiven;
        readonly List<InkBox> _monGiftButtons = new List<InkBox>();

        VisualElement MonBuildEndowment()
        {
            _monGiftButtons.Clear();
            _monEndowment = Ink("mon-card", "mon-endowment");
            var head = MonCardHead("coin", "Endowment", "given for good");
            _monGiven = head.Q<Label>(className: "mon-head-note");
            _monEndowment.Add(head);
            var body = Box("mon-endow-body");
            var art = new InkPicture(MonTree());
            art.AddToClassList("mon-endow-art");
            body.Add(art);
            var words = Box("mon-endow-words");
            _monFund = Text("", "font-hand", "mon-fund");
            words.Add(_monFund);
            _monFundNote = Text("", "font-note", "mon-fund-note");
            words.Add(_monFundNote);
            body.Add(words);
            _monEndowment.Add(body);

            var gifts = Box("mon-gifts");
            foreach (float amount in MonGifts)
            {
                float gift = amount;
                var b = Ink("mon-gift");
                b.Add(Text("+" + MoneyShort(gift), "font-heavy", "mon-gift-label"));
                OnClick(b, () => RunCommand(() => _sim.Endow(gift)));
                gifts.Add(b);
                _monGiftButtons.Add(b);
            }
            _monEndowment.Add(gifts);
            return _monEndowment;
        }

        void MonShowEndowment()
        {
            bool open = _sim.IsUnlocked(Feature.Endowment);
            Show(_monEndowment, open);
            if (!open) return;
            float fund = S.Endowment;
            SetText(_monFund, Money(fund));
            SetText(_monGiven, S.EndowmentGiven > 0f ? MoneyShort(S.EndowmentGiven) + " given so far" : "given for good");
            float prestige = Balance.EndowmentPrestige(fund, _sim.IsUnlocked(Feature.EliteInstitution));
            SetText(_monFundNote, fund <= 0f
                ? "a gift pays a little back every September and lifts prestige"
                : $"{Money(fund * Balance.EndowmentPayout)} every September · prestige +{prestige:0.0}");
            // Clicking a greyed gift still runs the command, so its toast says why not.
            for (int i = 0; i < MonGifts.Length; i++) _monGiftButtons[i].EnableInClassList("mon-gift--off", S.Finance.Cash < MonGifts[i]);
        }

        /// <summary>A sapling with coins for leaves, in a pot: the fund that grows a little every year.</summary>
        static InkDrawing MonTree() => _monTree ??= new InkDrawing(64f, 72f)
            .Shape("M20 52h24l-3 16H23z", InkIcons.Sky, InkIcons.Ink, 2.4f)
            .Line("M32 52V28M32 40l-9-7M32 35l8-6", InkIcons.Ink, 2.6f)
            .Circle(32f, 20f, 8f, InkIcons.Marker, InkIcons.Ink, 2.2f)
            .Circle(18f, 30f, 7f, InkIcons.Marker, InkIcons.Ink, 2.2f)
            .Circle(46f, 25f, 7f, InkIcons.Marker, InkIcons.Ink, 2.2f)
            .Line("M30 17c1.6-1 3.6-.6 4 .8M16 27.6c1.4-.8 3-.4 3.4.6M44 22.6c1.4-.8 3-.4 3.4.6", InkIcons.Ink, 1.6f);
    }
}
