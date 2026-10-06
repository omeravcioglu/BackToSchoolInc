using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The notebook panels that share the screen with the campus: the build-mode header (tools,
    /// overlays, campus space), the catalog (categories, room cards with their real drawings, the
    /// chosen room's details), buying land on a parcel map (with the old estate across the road once
    /// it's for sale), and the card of a selected room.
    /// </summary>
    public sealed partial class GameHud
    {
        static readonly (RoomCategory cat, string name, string icon)[] BldCategories =
        {
            (RoomCategory.Academic, "Academic", "book"),
            (RoomCategory.Residential, "Dorms & baths", "bed"),
            (RoomCategory.Dining, "Dining", "apple"),
            (RoomCategory.StudentLife, "Student life", "music"),
            (RoomCategory.Outdoors, "Outdoors", "sun"),
            (RoomCategory.Sports, "Sports", "trophy"),
            (RoomCategory.Staff, "Staff", "staff"),
            (RoomCategory.Health, "Health & safety", "shield"),
            (RoomCategory.Decor, "Decorations", "flower"),
        };

        InkBox _bldHead, _bldPanel;
        VisualElement _bldBuildView, _bldLandView, _bldRoomView, _bldCatRow, _bldSpaceFill, _bldParcelMap;
        ScrollView _bldCards;
        Label _bldTitle, _bldHint, _bldSpace, _bldCatName, _bldCatCount, _bldStatus, _bldOverlayNote;
        InkIcon _bldBadgeIcon;
        readonly List<(RoomCategory cat, InkBox chip)> _bldCatChips = new List<(RoomCategory, InkBox)>();
        readonly List<(RoomType type, InkBox card)> _bldCardViews = new List<(RoomType, InkBox)>();
        readonly List<(CampusOverlay overlay, Label pill)> _bldOverlayPills = new List<(CampusOverlay, Label)>();
        Label _bldModeBuild, _bldModeLand;
        RoomCategory _bldCategory = RoomCategory.Residential;
        RoomType _bldDetailShown = (RoomType)(-1);
        int _bldCardsSignature = int.MinValue;

        // Build detail
        Label _bldName, _bldLevels, _bldLevel1, _bldDesc, _bldCost, _bldDays, _bldUpkeep, _bldCapLabel, _bldCap, _bldSize, _bldExtra;
        VisualElement _bldThumbBig;

        // Land
        Label _bldLandPrice, _bldLandOwned, _bldLandHover, _bldEstateText, _bldEstatePrice;
        readonly List<(int px, int py, InkBox cell, Label price)> _bldParcels = new List<(int, int, InkBox, Label)>();
        int _bldParcelVersion = -1;
        InkBox _bldEstate, _bldEstateBuy;

        // Room card
        int _bldRoomShown = -1, _bldDemolishArmed = -1;
        float _bldDemolishUntil;
        InkIcon _bldRoomIcon;
        Label _bldRoomName, _bldRoomLevel, _bldRoomStatus, _bldRoomDesc, _bldRoomUsage, _bldRoomUpkeep, _bldNextTitle, _bldNextText, _bldNextCost;
        Label _bldUpgradeLabel, _bldRepairLabel, _bldRepairPrice, _bldDemolishLabel, _bldRoomWhy;
        VisualElement _bldDots, _bldCondFill, _bldCleanFill, _bldQualFill, _bldResidents, _bldNextCard;
        Label _bldCondValue, _bldCleanValue, _bldQualValue;
        InkBox _bldUpgradeBtn, _bldRepairBtn, _bldDemolishBtn;
        VisualElement _bldSite, _bldSiteFill, _bldHere, _bldHereFaces, _bldCondRow, _bldCleanRow, _bldQualRow;
        Label _bldSiteTitle, _bldSiteDays, _bldSiteNote, _bldHereText;
        readonly List<long> _bldHereKeys = new List<long>();
        float _bldHereNext;
        long _bldHereSig = long.MinValue;

        void BuildCampusPanels()
        {
            InkIcons.Define("flower", 1.8f,
                "fill:#FF8C6E circle:12,7,3", "fill:#FF8C6E circle:7.5,10.6,3", "fill:#FF8C6E circle:16.5,10.6,3",
                "fill:#FF8C6E circle:9.3,15.6,3", "fill:#FF8C6E circle:14.7,15.6,3", "fill:#FFE066 circle:12,11.6,2.4", "M12 18.4v3.6");
            InkIcons.Define("rotate", 2.4f, "M19 12a7 7 0 1 1-2-4.9", "M19 4.5v3.8h-3.8");
            InkIcons.Define("flag", 2f, "M6 21V4", "fill:#FFE066 M6 4.5h11l-2.6 4 2.6 4H6z");
            InkIcons.Define("wand", 2f, "M5 19L15.5 8.5", "fill:#FFE066 M18 3.5l.9 2.2 2.2.9-2.2.9-.9 2.2-.9-2.2-2.2-.9 2.2-.9z", "M9 4.5v2M8 5.5h2M20 13v2M19 14h2");
            InkIcons.Define("target", 2f, "circle:12,12,8", "fill:#FFE066 circle:12,12,3.2", "M12 2.5v3M12 18.5v3M2.5 12h3M18.5 12h3");
            InkIcons.Define("hammer", 2f, "fill:#FFE066 M5.5 7.5l4-3 4.5 3-1.6 2.4-2.9-1-1.8 1.4z", "M11.2 9l7.6 9.8a1.6 1.6 0 0 1-2.4 2L8.8 11.4");
            InkIcons.Define("bin", 2f, "M5 7h14", "M9.5 7V4.6h5V7", "fill:#FFE3DA M6.5 7l1 13h9l1-13z", "M10.4 10.5v6.5M13.6 10.5v6.5");
            InkIcons.Define("broom", 2f, "M14 4l-5.5 9.5", "fill:#FFE066 M7 12l4.4 2.6-2.3 6c-1.9.2-4.6-1.4-5.4-3.2z", "M6.4 15.6l1.2.7M8 17.6l1.1.6");

            // Header: title, tools, overlays and how much land is used
            _bldHead = Ink("card", "bld-head", "hidden");
            var top = Box("bld-row");
            var badge = Ink("bld-badge");
            _bldBadgeIcon = Icon("pencil", "icon-22");
            badge.Add(_bldBadgeIcon);
            top.Add(badge);
            var titles = Box("bld-titles");
            _bldTitle = Text("Build mode", "font-hand", "bld-title");
            _bldHint = Text("", "font-note", "bld-hint");
            titles.Add(_bldTitle);
            titles.Add(_bldHint);
            top.Add(titles);
            var close = Ink("x-btn", "bld-close");
            close.Add(Icon("close", "icon-18"));
            OnClick(close, LeaveCampusTool);
            top.Add(close);
            _bldHead.Add(top);

            var modes = Box("bld-row", "bld-modes");
            _bldModeBuild = Text("Build", "font-heavy", "pill-btn", "bld-mode");
            OnClick(_bldModeBuild, () => CampusController.Instance?.BeginBuild(CampusController.Instance.BuildType));
            _bldModeLand = Text("Buy land", "font-heavy", "pill-btn", "bld-mode");
            OnClick(_bldModeLand, () => CampusController.Instance?.BeginLand());
            modes.Add(_bldModeBuild);
            modes.Add(_bldModeLand);
            modes.Add(Box("spacer"));
            modes.Add(Text("show", "font-note", "bld-overlay-note"));
            foreach (var (overlay, name) in new[] { (CampusOverlay.Condition, "wear"), (CampusOverlay.Cleanliness, "dirt"), (CampusOverlay.Occupancy, "beds") })
            {
                var pill = Text(name, "font-heavy", "pill-btn", "bld-overlay");
                var o = overlay;
                OnClick(pill, () =>
                {
                    var view = CampusView.Instance;
                    if (view != null) view.Overlay = view.Overlay == o ? CampusOverlay.None : o;
                });
                _bldOverlayPills.Add((overlay, pill));
                modes.Add(pill);
            }
            _bldHead.Add(modes);

            var spaceRow = Box("bld-row", "bld-space-row");
            spaceRow.Add(Text("Campus space", "font-heavy", "bld-space-label"));
            _bldSpace = Text("", "font-semi", "bld-space-value");
            spaceRow.Add(_bldSpace);
            _bldHead.Add(spaceRow);
            var spaceBar = Box("bar", "bld-space-bar");
            _bldSpaceFill = Box("bar-fill");
            spaceBar.Add(_bldSpaceFill);
            _bldHead.Add(spaceBar);
            _hud.Add(_bldHead);

            // Right-hand panel: the catalog, land, or the selected room
            _bldPanel = Ink("card", "bld-panel", "hidden");
            BuildCatalogView();
            BuildLandView();
            BuildRoomView();
            _hud.Add(_bldPanel);
        }

        // --- Catalog --------------------------------------------------------------------------

        void BuildCatalogView()
        {
            _bldBuildView = Box("bld-view");
            _bldCatRow = Box("bld-cats");
            foreach (var (cat, _, icon) in BldCategories)
            {
                var chip = Ink("bld-cat");
                chip.Add(Icon(icon, "icon-20"));
                var c = cat;
                OnClick(chip, () =>
                {
                    _bldCategory = c;
                    _bldCardsSignature = int.MinValue;
                    var ctl = CampusController.Instance;
                    foreach (var def in RoomCatalog.All)
                        if (def.Category == c && BldOffered(def) && ctl != null)
                        {
                            ctl.BeginBuild(def.Type);
                            break;
                        }
                });
                _bldCatChips.Add((cat, chip));
                _bldCatRow.Add(chip);
            }
            _bldBuildView.Add(_bldCatRow);

            var head = Box("bld-cat-head");
            _bldCatName = Text("", "font-hand", "bld-cat-name");
            _bldCatCount = Text("", "font-note", "bld-cat-count");
            head.Add(_bldCatName);
            head.Add(_bldCatCount);
            _bldBuildView.Add(head);

            _bldCards = new ScrollView(ScrollViewMode.Vertical);
            _bldCards.AddToClassList("bld-cards");
            _bldBuildView.Add(_bldCards);

            // The chosen room's details
            var detail = Ink("bld-detail");
            var nameRow = Box("bld-row");
            _bldName = Text("", "font-hand", "bld-name");
            _bldLevels = Text("", "font-heavy", "bld-levels");
            nameRow.Add(_bldName);
            nameRow.Add(Box("spacer"));
            nameRow.Add(_bldLevels);
            detail.Add(nameRow);
            var body = Box("bld-row", "bld-detail-body");
            _bldThumbBig = Box("bld-thumb-big");
            body.Add(_bldThumbBig);
            var words = Box("bld-detail-words");
            _bldLevel1 = Text("", "font-note", "bld-level1");
            _bldDesc = Text("", "font-semi", "bld-desc");
            words.Add(_bldLevel1);
            words.Add(_bldDesc);
            body.Add(words);
            detail.Add(body);
            var grid = Box("bld-facts");
            _bldCost = Fact(grid, "cost");
            _bldDays = Fact(grid, "build");
            _bldUpkeep = Fact(grid, "upkeep");
            _bldCap = Fact(grid, "capacity", out _bldCapLabel);
            _bldSize = Fact(grid, "size");
            detail.Add(grid);
            _bldExtra = Text("", "font-heavy", "bld-extra");
            detail.Add(_bldExtra);
            _bldStatus = Text("", "font-note", "bld-status");
            detail.Add(_bldStatus);
            var buttons = Box("bld-row", "bld-buttons");
            buttons.Add(Button("rotate", "Turn (R)", () => CampusController.Instance?.Rotate(), small: true));
            buttons.Add(Button("wand", "Auto-place", AutoPlace, small: true));
            buttons.Add(Box("spacer"));
            buttons.Add(Button("check", "Done", LeaveCampusTool, primary: true, small: true));
            detail.Add(buttons);
            _bldBuildView.Add(detail);
            _bldPanel.Add(_bldBuildView);
        }

        static Label Fact(VisualElement grid, string label) => Fact(grid, label, out _);

        static Label Fact(VisualElement grid, string label, out Label name)
        {
            var cell = Box("bld-fact");
            name = Text(label, "font-semi", "bld-fact-label");
            cell.Add(name);
            var value = Text("", "font-hand", "bld-fact-value");
            cell.Add(value);
            grid.Add(cell);
            return value;
        }

        void AutoPlace()
        {
            var ctl = CampusController.Instance;
            if (ctl == null || _sim == null) return;
            var type = ctl.BuildType;
            RunCommand(() => _sim.BuildRoom(type));
        }

        /// <summary>Rebuilds the room cards when the category, level or money changes enough to matter.</summary>
        void RefreshCatalog(CampusController ctl)
        {
            var def = RoomCatalog.Get(ctl.BuildType);
            if (def.Category != _bldCategory)
            {
                _bldCategory = def.Category;
                _bldCardsSignature = int.MinValue;
            }
            foreach (var (cat, chip) in _bldCatChips) chip.EnableInClassList("bld-cat--on", cat == _bldCategory);
            string catName = "";
            foreach (var c in BldCategories) if (c.cat == _bldCategory) catName = c.name;
            SetText(_bldCatName, catName);

            int count = 0, owned = 0;
            foreach (var d in RoomCatalog.All) if (d.Category == _bldCategory && BldOffered(d)) count++;
            foreach (var r in S.Rooms) if (r.Def.Category == _bldCategory) owned++;
            SetText(_bldCatCount, owned == 0 ? $"{count} to choose from" : $"you have {owned}");

            int signature = (int)_bldCategory * 1000 + S.Progress.SchoolLevel * 10 + (S.Finance.Cash >= 0 ? 1 : 0);
            unchecked
            {
                signature = signature * 31 + (int)S.Specialization;
                foreach (var d in RoomCatalog.All)
                    if (d.Category == _bldCategory) signature = signature * 31 + (S.Finance.Cash >= d.Cost[0] ? 1 : 0);
            }
            if (signature != _bldCardsSignature)
            {
                _bldCardsSignature = signature;
                _bldCards.Clear();
                _bldCardViews.Clear();
                var row = Box("bld-card-row");
                foreach (var d in RoomCatalog.All)
                {
                    if (d.Category != _bldCategory || !BldOffered(d)) continue;
                    if (row.childCount == 2)
                    {
                        _bldCards.Add(row);
                        row = Box("bld-card-row");
                    }
                    var card = RoomCard(d);
                    row.Add(card);
                    _bldCardViews.Add((d.Type, card));
                }
                if (row.childCount > 0) _bldCards.Add(row);
            }
            foreach (var (type, card) in _bldCardViews) card.EnableInClassList("bld-card--on", type == ctl.BuildType);

            if (_bldDetailShown != ctl.BuildType) ShowBuildDetail(def);
            SetText(_bldStatus, !ctl.HasGhost ? (def.Decor ? "Point at your lawn to plant it. It stays in your hand for the next one." : "Point at your land: the door arrow must face a free tile.")
                : ctl.PlacementProblem ?? $"Fits here! Click to build it for {Money(def.Cost[0])}.");
            _bldStatus.EnableInClassList("bld-status--bad", ctl.HasGhost && ctl.PlacementProblem != null);
            _bldStatus.EnableInClassList("bld-status--good", ctl.HasGhost && ctl.PlacementProblem == null);
        }

        /// <summary>What this school can build: its own signature building but not the other five, and never the manor.</summary>
        bool BldOffered(RoomDefinition d) => d.Buildable && (d.Signature == Specialization.None || d.Signature == S.Specialization);

        InkBox RoomCard(RoomDefinition d)
        {
            var card = Ink("bld-card");
            var thumb = Box("bld-thumb");
            thumb.style.backgroundImage = new StyleBackground(CampusArt.Room(d.Type, 1, 0));
            card.Add(thumb);
            card.Add(Text(d.Name, "font-heavy", "bld-card-name"));
            var bottom = Box("bld-row");
            bottom.Add(Text(MoneyShort(d.Cost[0]), "font-hand", "bld-card-cost"));
            bottom.Add(Box("spacer"));
            bottom.Add(Text($"{d.Width}×{d.Height}", "font-semi", "bld-card-size"));
            card.Add(bottom);
            bool locked = S.Progress.SchoolLevel < d.MinSchoolLevel;
            if (locked)
            {
                var lockBadge = Box("bld-lock");
                lockBadge.Add(Icon("lock", "icon-13"));
                lockBadge.Add(Text("Lv " + d.MinSchoolLevel, "font-heavy"));
                card.Add(lockBadge);
            }
            else if (d.Signature != Specialization.None) card.Add(Icon("crest", "icon-18", "bld-sig"));
            card.EnableInClassList("bld-card--locked", locked);
            card.EnableInClassList("bld-card--poor", !locked && S.Finance.Cash < d.Cost[0]);
            var type = d.Type;
            OnClick(card, () => CampusController.Instance?.BeginBuild(type));
            return card;
        }

        void ShowBuildDetail(RoomDefinition d)
        {
            _bldDetailShown = d.Type;
            SetText(_bldName, d.Name);
            SetText(_bldLevels, d.MaxLevel > 1 ? $"Lv 1 of {d.MaxLevel}" : "one size");
            SetText(_bldLevel1, d.LevelNames[0]);
            Show(_bldLevel1, d.LevelNames[0] != d.Description);   // decorations have one line, not two
            SetText(_bldDesc, d.Description);
            SetText(_bldCost, MoneyShort(d.Cost[0]));
            SetText(_bldDays, d.BuildDays[0] == 1 ? "1 day" : d.BuildDays[0] + " days");
            SetText(_bldUpkeep, MoneyShort(d.Upkeep[0]) + "/mo");
            bool hasCap = !d.Decor || d.Capacity[0] > 0;
            Show(_bldCap.parent, hasCap);
            SetText(_bldCapLabel, d.CapacityLabel.ToLowerInvariant());
            SetText(_bldCap, d.Capacity[0].ToString());
            SetText(_bldSize, $"{d.Width}×{d.Height}");
            _bldThumbBig.style.backgroundImage = new StyleBackground(CampusArt.Room(d.Type, d.MaxLevel, 0));

            string extra = "";
            if (d.Signature != Specialization.None) extra = $"The {Names.Specialization(d.Signature).ToLowerInvariant()}'s own building";
            else if (d.Type == RoomType.DormRoom) extra = $"+{Money(d.MonthlyFee[0])} per boarder each month";
            else if (d.Beauty >= 1f) extra = "Makes the campus prettier: happier students, keener families";
            else if (d.Category == RoomCategory.Sports && d.Type != RoomType.PracticeField) extra = "Home ground for a school team";
            if (S.Progress.SchoolLevel < d.MinSchoolLevel) extra = $"Unlocks at school level {d.MinSchoolLevel}";
            SetText(_bldExtra, extra);
            _bldExtra.EnableInClassList("bld-extra--bad", S.Progress.SchoolLevel < d.MinSchoolLevel);
            Show(_bldExtra, extra.Length > 0);
        }

        // --- Land -----------------------------------------------------------------------------

        void BuildLandView()
        {
            _bldLandView = Box("bld-view", "hidden");
            var head = Box("bld-cat-head");
            head.Add(Text("Buy land", "font-hand", "bld-cat-name"));
            head.Add(Text("10 × 10 tiles each", "font-note", "bld-cat-count"));
            _bldLandView.Add(head);

            var price = Ink("bld-land-price");
            price.Add(Icon("flag", "icon-32"));
            var words = Box("bld-detail-words");
            words.Add(Text("next parcel", "font-note", "bld-land-small"));
            _bldLandPrice = Text("", "font-hand", "bld-land-big");
            words.Add(_bldLandPrice);
            _bldLandOwned = Text("", "font-semi", "bld-desc");
            words.Add(_bldLandOwned);
            price.Add(words);
            _bldLandView.Add(price);

            _bldLandView.Add(Text("Pick a parcel next to your campus. Each one costs a bit more than the last.", "font-semi", "bld-desc", "bld-land-text"));
            _bldParcelMap = Box("bld-parcels");
            _bldLandView.Add(_bldParcelMap);
            var legend = Box("bld-row", "bld-legend");
            legend.Add(Box("bld-swatch", "bld-swatch--owned"));
            legend.Add(Text("yours", "font-semi"));
            legend.Add(Box("bld-swatch", "bld-swatch--sale"));
            legend.Add(Text("for sale", "font-semi"));
            legend.Add(Box("bld-swatch"));
            legend.Add(Text("not yet", "font-semi"));
            _bldLandView.Add(legend);

            // The old estate across the road, once it's for sale
            _bldEstate = Ink("bld-estate", "hidden");
            var estateHead = Box("bld-row");
            estateHead.Add(Icon("crest", "icon-24"));
            estateHead.Add(Text("The Old Estate", "font-hand", "bld-estate-title"));
            estateHead.Add(Box("spacer"));
            _bldEstatePrice = Text("", "font-hand", "bld-estate-price");
            estateHead.Add(_bldEstatePrice);
            _bldEstate.Add(estateHead);
            _bldEstateText = Text("", "font-semi", "bld-desc");
            _bldEstate.Add(_bldEstateText);
            _bldEstateBuy = Button("flag", "Buy it", () => RunCommand(() => _sim.OpenEstate()), primary: true, small: true);
            _bldEstateBuy.AddToClassList("bld-estate-buy");
            _bldEstate.Add(_bldEstateBuy);
            _bldLandView.Add(_bldEstate);

            _bldLandHover = Text("", "font-note", "bld-status");
            _bldLandView.Add(_bldLandHover);
            var buttons = Box("bld-row", "bld-buttons");
            buttons.Add(Button("pencil", "Back to building", () => CampusController.Instance?.BeginBuild(CampusController.Instance.BuildType), small: true));
            buttons.Add(Box("spacer"));
            buttons.Add(Button("check", "Done", LeaveCampusTool, primary: true, small: true));
            _bldLandView.Add(buttons);
            _bldPanel.Add(_bldLandView);
        }

        void RefreshLand(CampusController ctl)
        {
            var map = S.Campus;
            float price = _sim.Facilities.LandCost;
            SetText(_bldLandPrice, Money(price));
            SetText(_bldLandOwned, $"You own {map.OwnedParcelCount} parcels · {map.UsedTiles} of {map.OwnedTiles} tiles built on");

            // The estate's columns join the map (in smaller cells) once it comes up for sale.
            bool wide = map.EstateOpen || _sim.IsUnlocked(Feature.SecondCampus);
            int columns = wide ? map.ParcelsX : map.EstateParcelX;
            if (_bldParcelVersion != map.Version || _bldParcels.Count != columns * map.ParcelsY)
            {
                _bldParcelVersion = map.Version;
                _bldParcelMap.EnableInClassList("bld-parcels--wide", wide);
                _bldParcelMap.Clear();
                _bldParcels.Clear();
                for (int py = map.ParcelsY - 1; py >= 0; py--)
                {
                    var row = Box("bld-parcel-row");
                    for (int px = 0; px < columns; px++)
                    {
                        if (px == map.EstateParcelX) row.Add(Box("bld-road"));
                        var cell = Ink("bld-parcel");
                        bool owned = map.IsParcelOwned(px, py), sale = map.IsParcelPurchasable(px, py);
                        cell.EnableInClassList("bld-parcel--owned", owned);
                        cell.EnableInClassList("bld-parcel--sale", sale);
                        cell.EnableInClassList("bld-parcel--estate", map.IsEstateParcel(px, py));
                        var tag = Text(sale ? MoneyShort(_sim.Facilities.ParcelCost(px, py)) : "", "font-heavy", "bld-parcel-price");
                        cell.Add(tag);
                        int x = px, y = py;
                        if (sale) OnClick(cell, () => RunCommand(() => _sim.BuyParcel(x, y)));
                        row.Add(cell);
                        _bldParcels.Add((px, py, cell, tag));
                    }
                    _bldParcelMap.Add(row);
                }
            }
            RefreshEstate(map, wide);

            var hp = ctl.HoverParcel;
            string note;
            if (!map.ParcelInBounds(hp.x, hp.y)) note = "Click a parcel here or one with a price tag on the map.";
            else if (map.IsParcelOwned(hp.x, hp.y)) note = "That parcel is already yours.";
            else if (map.IsEstateParcel(hp.x, hp.y) && !map.EstateOpen)
                note = wide ? "The old estate: buy it with the button above." : $"The old estate comes up for sale at level {Balance.UnlockLevel(Feature.SecondCampus)}.";
            else
            {
                _sim.Facilities.CanBuyParcel(hp.x, hp.y, out string why);
                note = why ?? $"Click to buy it for {Money(_sim.Facilities.ParcelCost(hp.x, hp.y))}.";
            }
            SetText(_bldLandHover, note);
        }

        /// <summary>The estate card: what buying it brings, then what its parcels cost once it's ours.</summary>
        void RefreshEstate(CampusMap map, bool shown)
        {
            Show(_bldEstate, shown);
            if (!shown) return;
            Show(_bldEstateBuy, !map.EstateOpen);
            if (map.EstateOpen)
            {
                SetText(_bldEstatePrice, MoneyShort(_sim.Facilities.EstateLandCost));
                SetText(_bldEstateText, "Yours, with its manor. More of the park is for sale next to what you own; each parcel costs a little more.");
                return;
            }
            int parcels = (map.ParcelsX - map.EstateParcelX) * map.ParcelsY;
            SetText(_bldEstatePrice, MoneyShort(Balance.EstateOpenCost));
            SetText(_bldEstateText, $"Across the road: {parcels} parcels of parkland and an old manor. Buy it for a second campus: the manor and its drive at once, the rest from {MoneyShort(Balance.EstateParcelCost)} a parcel.");
            _bldEstateBuy.EnableInClassList("btn--off", !_sim.Facilities.CanOpenEstate(out _));
        }

        // --- Selected room ----------------------------------------------------------------------

        void BuildRoomView()
        {
            _bldRoomView = Box("bld-view", "hidden");
            var head = Box("bld-row", "bld-room-head");
            var badge = Ink("bld-badge");
            _bldRoomIcon = Icon("house", "icon-22");
            badge.Add(_bldRoomIcon);
            head.Add(badge);
            var titles = Box("bld-titles");
            _bldRoomName = Text("", "font-hand", "bld-title");
            var levelRow = Box("bld-row");
            _bldDots = Box("bld-dots");
            levelRow.Add(_bldDots);
            _bldRoomLevel = Text("", "font-heavy", "bld-room-level");
            levelRow.Add(_bldRoomLevel);
            titles.Add(_bldRoomName);
            titles.Add(levelRow);
            head.Add(titles);
            var close = Ink("x-btn", "bld-close");
            close.Add(Icon("close", "icon-18"));
            OnClick(close, () => CampusController.Instance?.Select(-1));
            head.Add(close);
            _bldRoomView.Add(head);

            _bldRoomStatus = Text("", "font-note", "bld-room-status");
            _bldRoomView.Add(_bldRoomStatus);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("bld-room-scroll");
            _bldRoomDesc = Text("", "font-semi", "bld-desc");
            scroll.Add(_bldRoomDesc);

            // A construction site: how far along it is and when it opens.
            _bldSite = Ink("bld-site");
            var siteHead = Box("bld-row");
            _bldSiteTitle = Text("", "font-heavy", "pill", "bld-site-pill");
            siteHead.Add(_bldSiteTitle);
            siteHead.Add(Box("spacer"));
            _bldSiteDays = Text("", "font-hand", "bld-site-days");
            siteHead.Add(_bldSiteDays);
            _bldSite.Add(siteHead);
            var siteBar = Box("bar", "bld-site-bar");
            _bldSiteFill = Box("bar-fill", "bld-site-fill");
            siteBar.Add(_bldSiteFill);
            _bldSite.Add(siteBar);
            _bldSiteNote = Text("", "font-note", "bld-site-note");
            _bldSite.Add(_bldSiteNote);
            scroll.Add(_bldSite);

            scroll.Add(_bldCondRow = MeterRow("Condition", "hammer", out _bldCondFill, out _bldCondValue));
            scroll.Add(_bldCleanRow = MeterRow("Cleanliness", "broom", out _bldCleanFill, out _bldCleanValue));
            scroll.Add(_bldQualRow = MeterRow("Quality", "star", out _bldQualFill, out _bldQualValue));
            var usageRow = Box("bld-row", "bld-usage-row");
            _bldRoomUsage = Text("", "font-heavy", "bld-usage");
            _bldRoomUpkeep = Text("", "font-semi", "bld-upkeep");
            usageRow.Add(_bldRoomUsage);
            usageRow.Add(Box("spacer"));
            usageRow.Add(_bldRoomUpkeep);
            scroll.Add(usageRow);
            _bldResidents = Box("bld-residents");
            scroll.Add(_bldResidents);

            // Who's in the room right now, like the design's "Maya, Zara and the chef are here".
            _bldHere = Ink("bld-here");
            _bldHereFaces = Box("bld-here-faces");
            _bldHere.Add(_bldHereFaces);
            _bldHereText = Text("", "font-note", "bld-here-text");
            _bldHere.Add(_bldHereText);
            scroll.Add(_bldHere);

            _bldNextCard = Ink("bld-next");
            var nextHead = Box("bld-row");
            _bldNextTitle = Text("", "font-heavy", "pill", "bld-next-pill");
            nextHead.Add(_bldNextTitle);
            nextHead.Add(Box("spacer"));
            _bldNextCost = Text("", "font-hand", "bld-next-cost");
            nextHead.Add(_bldNextCost);
            _bldNextCard.Add(nextHead);
            _bldNextText = Text("", "font-note", "bld-next-text");
            _bldNextCard.Add(_bldNextText);
            scroll.Add(_bldNextCard);
            _bldRoomWhy = Text("", "font-semi", "bld-why");
            scroll.Add(_bldRoomWhy);
            _bldRoomView.Add(scroll);

            var buttons = Box("bld-row", "bld-buttons");
            _bldUpgradeBtn = Button("up", "Upgrade", () => RoomAction(r => _sim.UpgradeRoom(r.Id)), primary: true, small: true);
            _bldUpgradeLabel = _bldUpgradeBtn.Q<Label>();
            buttons.Add(_bldUpgradeBtn);
            _bldRepairBtn = Button("hammer", "Repair", () => RoomAction(r => _sim.RepairRoom(r.Id)), small: true);
            _bldRepairLabel = _bldRepairBtn.Q<Label>();
            _bldRepairPrice = Text("", "font-heavy", "bld-price-tag");   // the cost rides on the corner, so the label fits
            _bldRepairPrice.pickingMode = PickingMode.Ignore;
            _bldRepairBtn.Add(_bldRepairPrice);
            buttons.Add(_bldRepairBtn);
            buttons.Add(Box("spacer"));
            var centre = Ink("x-btn", "bld-round");
            centre.Add(Icon("target", "icon-18"));
            OnClick(centre, () =>
            {
                var room = CampusController.Instance?.SelectedRoom;
                if (room != null) FrameRoom(room);
            });
            buttons.Add(centre);
            _bldDemolishBtn = Ink("x-btn", "bld-round", "bld-demolish");
            _bldDemolishBtn.Add(Icon("bin", "icon-18"));
            _bldDemolishLabel = Text("sure?", "font-heavy", "bld-demolish-label", "hidden");
            _bldDemolishBtn.Add(_bldDemolishLabel);
            OnClick(_bldDemolishBtn, DemolishClicked);
            buttons.Add(_bldDemolishBtn);
            _bldRoomView.Add(buttons);
            _bldPanel.Add(_bldRoomView);
        }

        static VisualElement MeterRow(string label, string icon, out VisualElement fill, out Label value)
        {
            var row = Box("bld-row", "bld-meter");
            row.Add(Icon(icon, "icon-18", "bld-meter-icon"));
            row.Add(Text(label, "font-semi", "bld-meter-label"));
            var bar = Box("bar", "bld-meter-bar");
            fill = Box("bar-fill");
            bar.Add(fill);
            row.Add(bar);
            value = Text("", "font-hand", "bld-meter-value");
            row.Add(value);
            return row;
        }

        void RoomAction(Func<Room, CommandResult> action)
        {
            var room = CampusController.Instance?.SelectedRoom;
            if (room == null || _sim == null) return;
            RunCommand(() => action(room));
            _bldRoomShown = -1;
        }

        void DemolishClicked()
        {
            var room = CampusController.Instance?.SelectedRoom;
            if (room == null) return;
            if (_bldDemolishArmed == room.Id && GameClock.Now < _bldDemolishUntil)
            {
                _bldDemolishArmed = -1;
                int id = room.Id;
                RunCommand(() => _sim.DemolishRoom(id));
                return;
            }
            _bldDemolishArmed = room.Id;
            _bldDemolishUntil = GameClock.Now + 3f;
        }

        static string CategoryIcon(RoomCategory cat)
        {
            foreach (var c in BldCategories) if (c.cat == cat) return c.icon;
            return "house";
        }

        void RefreshRoomCard(Room r)
        {
            int day = S.Day;
            if (_bldRoomShown != r.Id)
            {
                _bldRoomShown = r.Id;
                _bldDemolishArmed = -1;
                _bldRoomIcon.Icon = CategoryIcon(r.Def.Category);
                SetHandText(_bldRoomName, r.Name);
                SetText(_bldRoomDesc, r.Def.Description);
                _bldResidents.userData = null;
                _bldHereSig = long.MinValue;
                _bldHereNext = 0f;
            }
            bool building = !r.IsBuilt(day);
            bool decor = r.Def.Decor;
            _bldDots.Clear();
            for (int lvl = 1; lvl <= r.Def.MaxLevel; lvl++)
            {
                var dot = Box("bld-dot");
                dot.EnableInClassList("bld-dot--on", lvl <= r.Level);
                dot.EnableInClassList("bld-dot--next", lvl == r.PendingLevel);
                _bldDots.Add(dot);
            }
            SetText(_bldRoomLevel, r.Def.MaxLevel > 1 ? $"Lv {r.Level} of {r.Def.MaxLevel}" : "");
            Show(_bldDots.parent, r.Def.MaxLevel > 1);
            string status = r.IsClosed(day) ? $"closed until {Calendar.Format(r.ClosedUntilDay)}" : r.LevelName;
            SetText(_bldRoomStatus, status);
            _bldRoomStatus.EnableInClassList("bld-room-status--bad", r.IsClosed(day));
            Show(_bldRoomDesc, r.Def.Description != status);   // decorations say it once

            Show(_bldSite, building || r.IsUpgrading);
            if (building || r.IsUpgrading)
            {
                int left = Mathf.Max(0, r.ConstructionDoneDay - day);
                SetText(_bldSiteTitle, building ? "Being built" : $"Upgrading to Lv {r.PendingLevel}");
                SetText(_bldSiteDays, left <= 1 ? "almost done" : $"{left} days left");
                SetPercent(_bldSiteFill, r.ConstructionProgress(day) * 100f);
                SetText(_bldSiteNote, building
                    ? $"opens {Calendar.Format(r.ConstructionDoneDay)} · builders on site"
                    : $"done {Calendar.Format(r.ConstructionDoneDay)} · it stays open meanwhile");
            }

            Meter(_bldCondFill, _bldCondValue, r.Condition);
            Meter(_bldCleanFill, _bldCleanValue, r.Def.Decor ? 100f : r.Cleanliness);
            Meter(_bldQualFill, _bldQualValue, r.Quality);
            Show(_bldCondRow, !building);
            Show(_bldCleanRow, !building && !decor);
            Show(_bldQualRow, !building && !decor);
            SetText(_bldRoomUsage, building ? $"{RoomUsage(r)} when it opens" : RoomUsage(r));
            SetText(_bldRoomUpkeep, $"{Money(r.Upkeep)}/mo upkeep");

            // Who lives here (dorms): faces you can click to open their file
            var residents = r.Type == RoomType.DormRoom ? _sim.Indices.ResidentsOf(r.Id) : null;
            int sig = 17;
            if (residents != null) foreach (var s in residents) sig = sig * 31 + s.Id;
            if (!(_bldResidents.userData is int old) || old != sig)
            {
                _bldResidents.userData = sig;
                _bldResidents.Clear();
                if (residents != null)
                    foreach (var s in residents)
                    {
                        var face = Ink("bld-face");
                        var portrait = new PersonPortrait();
                        portrait.AddToClassList("bld-face-pic");
                        portrait.ShowStudent(s);
                        face.Add(portrait);
                        face.Add(Text(s.FirstName, "font-semi", "bld-face-name"));
                        int id = s.Id;
                        OnClick(face, () =>
                        {
                            _studentFocus = id;
                            ShowPageAt(HudPage.Students);
                        });
                        _bldResidents.Add(face);
                    }
            }
            Show(_bldResidents, residents != null && residents.Count > 0);

            // Next level
            bool canGoUp = r.Level < r.Def.MaxLevel;
            Show(_bldNextCard, canGoUp && !r.IsUpgrading && !building);
            if (canGoUp)
            {
                int next = r.Level + 1;
                SetText(_bldNextTitle, $"Lv {next} next");
                SetText(_bldNextCost, MoneyShort(_sim.Facilities.UpgradeCost(r)));
                string capChange = r.Def.Capacity[next - 1] != r.Capacity ? $" · {r.Def.CapacityLabel.ToLowerInvariant()} {r.Capacity} → {r.Def.Capacity[next - 1]}" : "";
                string fee = r.Type == RoomType.DormRoom ? $" · fee {Money(r.Def.MonthlyFee[next - 1])}/mo" : "";
                int days = r.Def.BuildDays[next - 1];
                SetText(_bldNextText, $"{r.Def.LevelNames[next - 1]}{capChange}{fee} · {days} day{(days == 1 ? "" : "s")}");
            }
            _sim.Facilities.CanUpgrade(r, out string whyUp);
            float repair = _sim.Facilities.RepairCost(r);
            string whyRepair = repair < 1f ? "nothing to fix" : S.Finance.Cash < repair ? "not enough money" : null;
            SetText(_bldUpgradeLabel, canGoUp ? "Upgrade" : "Top level");
            _bldUpgradeBtn.EnableInClassList("btn--off", whyUp != null);
            SetText(_bldRepairLabel, "Repair");
            SetText(_bldRepairPrice, repair < 1f ? "" : MoneyShort(repair));
            Show(_bldRepairPrice, repair >= 1f);
            _bldRepairBtn.EnableInClassList("btn--off", whyRepair != null);
            Show(_bldRepairBtn, !decor && !building);
            Show(_bldUpgradeBtn, r.Def.MaxLevel > 1 && !building);
            string why = canGoUp && whyUp != null && !building && !r.IsUpgrading ? whyUp : "";
            SetText(_bldRoomWhy, why);
            Show(_bldRoomWhy, why.Length > 0);

            Show(_bldDemolishBtn, r.Def.Buildable);   // the old manor is a listed building

            Show(_bldHere, !building && !decor);
            if (!building && !decor) RefreshRoomHere(r);
            bool armed = _bldDemolishArmed == r.Id && GameClock.Now < _bldDemolishUntil;
            Show(_bldDemolishLabel, armed);
            _bldDemolishBtn.EnableInClassList("bld-demolish--armed", armed);
        }

        static void Meter(VisualElement fill, Label value, float v)
        {
            SetPercent(fill, v);
            fill.EnableInClassList("bar-fill--low", v < 40f);
            SetText(value, Mathf.RoundToInt(v).ToString());
        }

        /// <summary>The room card's "who's here": faces and names of everyone inside right now.</summary>
        void RefreshRoomHere(Room r)
        {
            float now = GameClock.Now;
            if (now < _bldHereNext) return;
            _bldHereNext = now + 0.5f;
            _bldHereKeys.Clear();
            CampusPeople.Instance?.PeopleIn(r, _bldHereKeys);
            _bldHereKeys.Sort();   // students first, then staff
            long sig = 17;
            foreach (long k in _bldHereKeys) sig = unchecked(sig * 31 + k);
            if (sig == _bldHereSig) return;
            _bldHereSig = sig;

            _bldHereFaces.Clear();
            var names = new List<string>();
            var roles = new List<StaffRole>();
            var roleCounts = new Dictionary<StaffRole, int>();
            foreach (long key in _bldHereKeys)
            {
                int id = CampusPeople.IdOf(key);
                long pick = key;
                PersonPortrait pic = null;
                if (CampusPeople.IsStaff(key))
                {
                    var m = S.FindStaff(id);
                    if (m == null) continue;
                    if (!roleCounts.ContainsKey(m.Role))
                    {
                        roleCounts[m.Role] = 0;
                        roles.Add(m.Role);
                    }
                    roleCounts[m.Role]++;
                    if (_bldHereFaces.childCount < 4)
                    {
                        pic = new PersonPortrait();
                        pic.ShowStaff(m);
                    }
                }
                else
                {
                    var st = S.FindStudent(id);
                    if (st == null) continue;
                    names.Add(st.FirstName);
                    if (_bldHereFaces.childCount < 4)
                    {
                        pic = new PersonPortrait();
                        pic.ShowStudent(st);
                    }
                }
                if (pic == null) continue;
                var face = Ink("bld-here-face");
                pic.AddToClassList("bld-here-pic");
                face.Add(pic);
                OnClick(face, () => CampusController.Instance?.SelectPerson(pick));
                _bldHereFaces.Add(face);
            }
            bool groups = false;
            foreach (var role in roles)
            {
                int n = roleCounts[role];
                names.Add(n == 1 ? "the " + StfRoleWord(role, 1) : $"{n} {StfRoleWord(role, n)}");
                groups |= n > 1;
            }
            Show(_bldHereFaces, _bldHereFaces.childCount > 0);
            SetText(_bldHereText, names.Count switch
            {
                0 => "nobody's in here right now",
                1 => $"{names[0]} {(groups ? "are" : "is")} here",   // "the chef is", "2 chefs are"
                2 => $"{names[0]} and {names[1]} are here",
                3 => $"{names[0]}, {names[1]} and {names[2]} are here",
                _ => $"{names[0]}, {names[1]} and {names.Count - 2} more are here",
            });
        }

        string RoomUsage(Room r)
        {
            switch (r.Type)
            {
                case RoomType.DormRoom when !r.IsBuilt(S.Day): return $"{r.Capacity} beds";
                case RoomType.DormRoom: return $"{_sim.Indices.ResidentsOf(r.Id).Count} of {r.Capacity} beds taken";
                case RoomType.Classroom: return $"{r.Capacity} seats";
                case RoomType.Cafeteria: return $"{r.Capacity} meals a sitting";
                default:
                    if (!r.Def.Decor) return $"{r.Def.CapacityLabel}: {r.Capacity}";
                    string seats = r.Capacity > 0 ? $"{r.Capacity} seats · " : "";
                    return r.Def.Beauty > 0f ? $"{seats}brightens the campus (+{r.Def.Beauty:0.#} beauty)" : seats + "just lovely";
            }
        }

        // --- Frame ------------------------------------------------------------------------------

        /// <summary>Shows the panel that matches the campus tool: catalog, land or the selected room.</summary>
        void UpdateCampusPanels(bool page)
        {
            if (_bldHead == null) return;
            var ctl = CampusController.Instance;
            bool building = !page && ctl != null && ctl.Tool == CampusTool.Build;
            bool land = !page && ctl != null && ctl.Tool == CampusTool.Land;
            var room = !page && ctl != null && ctl.Tool == CampusTool.Inspect ? ctl.SelectedRoom : null;

            Show(_bldHead, building || land);
            Show(_bldPanel, building || land || room != null);
            Show(_bldBuildView, building);
            Show(_bldLandView, land);
            Show(_bldRoomView, room != null);
            if (!building && !land && room == null)
            {
                _bldRoomShown = -1;
                return;
            }

            if (building || land)
            {
                SetText(_bldTitle, land ? "Buy land" : "Build mode");
                _bldBadgeIcon.Icon = land ? "flag" : "pencil";
                SetText(_bldHint, land ? "click a parcel · Esc leaves" : "R turns · Shift keeps building · Esc leaves");
                _bldModeBuild.EnableInClassList("pill-btn--on", building);
                _bldModeLand.EnableInClassList("pill-btn--on", land);
                Show(_bldModeLand, _sim.IsRevealed(Reveal.Land));
                var view = CampusView.Instance;
                foreach (var (overlay, pill) in _bldOverlayPills) pill.EnableInClassList("pill-btn--on", view != null && view.Overlay == overlay);
                var map = S.Campus;
                SetText(_bldSpace, $"{map.UsedTiles} of {map.OwnedTiles} tiles used");
                SetPercent(_bldSpaceFill, map.OwnedTiles == 0 ? 0f : 100f * map.UsedTiles / map.OwnedTiles);
            }
            if (building) RefreshCatalog(ctl);
            else if (land) RefreshLand(ctl);
            else RefreshRoomCard(room);
        }

        void LeaveCampusTool()
        {
            var ctl = CampusController.Instance;
            if (ctl == null) return;
            if (ctl.Tool != CampusTool.Inspect) ctl.Cancel();
            var view = CampusView.Instance;
            if (view != null) view.Overlay = CampusOverlay.None;
        }

        void FrameRoom(Room r)
        {
            var f = r.Footprint;
            CampusCamera.Instance?.Frame(new Vector2(f.X + f.W / 2f, f.Y + f.H / 2f), Mathf.Max(6f, Mathf.Max(f.W, f.H) * 0.9f));
        }
    }
}
