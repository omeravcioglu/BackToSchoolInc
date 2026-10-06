using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The Rooms page: every room on campus as a little card (its drawing, level, wear, dirt and
    /// what it's used for), grouped by kind. Clicking one flies the camera to it and opens its card.
    /// </summary>
    public sealed partial class GameHud
    {
        VisualElement _rmsList;
        Label _rmsSummary, _rmsUpkeep;
        int _rmsFilter;                    // 0 = all, 1 = needs care, 2 = being built
        int _rmsSignature = int.MinValue;

        partial void BuildRoomsPage()
        {
            _rmsSignature = int.MinValue;
            var column = Box("rms-col");

            var head = Box("rms-head");
            var badge = Ink("rms-badge");
            badge.Add(Icon("house", "icon-24"));
            head.Add(badge);
            var words = Box("rms-head-words");
            _rmsSummary = Text("", "font-hand", "rms-summary");
            _rmsUpkeep = Text("", "font-note", "rms-upkeep");
            words.Add(_rmsSummary);
            words.Add(_rmsUpkeep);
            head.Add(words);
            head.Add(Pills(new[] { "All", "Needs care", "Being built" }, () => _rmsFilter, i =>
            {
                _rmsFilter = i;
                _rmsSignature = int.MinValue;
                RefreshRoomsPage(true);
            }));
            head.Add(Button("pencil", "Build", () =>
            {
                CloseSheet();
                var ctl = CampusController.Instance;
                ctl?.BeginBuild(ctl.BuildType);
            }, small: true));
            column.Add(head);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("rms-scroll");
            _rmsList = Box("rms-list");
            scroll.Add(_rmsList);
            column.Add(scroll);
            _pageHost.Add(column);
        }

        partial void RefreshRoomsPage(bool newDay)
        {
            if (_rmsList == null) return;
            int day = S.Day;
            float upkeep = 0f;
            int built = 0;
            foreach (var r in S.Rooms)
            {
                if (r.IsBuilt(day)) built++;
                upkeep += r.Upkeep;
            }
            var map = S.Campus;
            SetText(_rmsSummary, $"{built} rooms and places");
            SetText(_rmsUpkeep, $"{Money(upkeep)} a month to keep up · {map.UsedTiles} of {map.OwnedTiles} tiles used");

            // Rebuild the cards when rooms change; refresh their bars cheaply otherwise.
            int signature = _rmsFilter;
            unchecked
            {
                foreach (var r in S.Rooms) signature = signature * 31 + r.Id * 7 + r.Level + (r.IsBuilt(day) ? 1 : 0) + (r.IsUpgrading ? 3 : 0);
            }
            if (signature == _rmsSignature && !newDay) return;
            _rmsSignature = signature;

            _rmsList.Clear();
            int shown = 0;
            foreach (var (cat, name, icon) in BldCategories)
            {
                var rooms = new List<Room>();
                foreach (var r in S.Rooms)
                {
                    if (r.Def.Category != cat) continue;
                    if (_rmsFilter == 1 && !(r.Condition < 50f || (!r.Def.Decor && r.Cleanliness < 50f) || r.IsClosed(day))) continue;
                    if (_rmsFilter == 2 && r.IsBuilt(day) && !r.IsUpgrading) continue;
                    rooms.Add(r);
                }
                if (rooms.Count == 0) continue;

                var group = Box("rms-group");
                var title = Box("rms-group-head");
                title.Add(Icon(icon, "icon-22"));
                title.Add(Text(name, "font-hand", "rms-group-title"));
                title.Add(Text(rooms.Count.ToString(), "font-note", "rms-group-count"));
                group.Add(title);

                if (cat == RoomCategory.Decor)
                {
                    // Decorations are many and tiny: one line of little chips is enough.
                    var chips = Box("rms-decor");
                    foreach (var r in rooms) chips.Add(RmsDecorChip(r));
                    group.Add(chips);
                }
                else
                {
                    var cards = Box("rms-cards");
                    foreach (var r in rooms) cards.Add(RmsCard(r, day));
                    group.Add(cards);
                }
                _rmsList.Add(group);
                shown += rooms.Count;
            }
            if (shown == 0)
                _rmsList.Add(Text(_rmsFilter == 1 ? "Everything is in good shape. Your custodians earn their pay."
                    : _rmsFilter == 2 ? "Nothing is being built right now." : "No rooms yet.", "font-note", "empty-note"));
        }

        partial void ResetRoomsPage()
        {
            _rmsFilter = 0;
            _rmsSignature = int.MinValue;
            _rmsList = null;
        }

        InkBox RmsCard(Room r, int day)
        {
            var card = Ink("rms-card");
            bool closed = r.IsClosed(day);
            card.EnableInClassList("rms-card--closed", closed);
            var thumb = Box("rms-thumb");
            thumb.style.backgroundImage = new StyleBackground(r.IsBuilt(day)
                ? CampusArt.Room(r.Type, r.Level, 0)
                : CampusArt.ConstructionSite(r.Def.Width, r.Def.Height));
            card.Add(thumb);

            var body = Box("rms-card-body");
            var top = Box("rms-row");
            top.Add(Text(r.Name, "font-heavy", "rms-name"));
            top.Add(Box("spacer"));
            if (r.Def.MaxLevel > 1) top.Add(Text("Lv " + r.Level, "font-heavy", "rms-level"));
            body.Add(top);

            string status = !r.IsBuilt(day) ? $"building · {Mathf.RoundToInt(r.ConstructionProgress(day) * 100f)}%"
                : r.IsUpgrading ? $"upgrading to Lv {r.PendingLevel}"
                : closed ? $"closed until {Calendar.Format(r.ClosedUntilDay)}"
                : r.LevelName;
            var note = Text(status, "font-note", "rms-status");
            note.EnableInClassList("rms-status--bad", closed);
            body.Add(note);
            if (r.IsBuilt(day))
            {
                body.Add(RmsMeter("hammer", r.Condition));
                body.Add(RmsMeter("broom", r.Cleanliness));
            }
            else
            {
                // A site has no wear yet: show how far the builders are instead.
                int left = Mathf.Max(0, r.ConstructionDoneDay - day);
                body.Add(RmsSiteBar(r.ConstructionProgress(day), left <= 1 ? "almost done" : $"{left} days left"));
            }
            var bottom = Box("rms-row");
            bottom.Add(Text(r.IsBuilt(day) ? RoomUsage(r) : RoomUsage(r) + " soon", "font-semi", "rms-usage"));
            bottom.Add(Box("spacer"));
            bottom.Add(Text(MoneyShort(r.Upkeep) + "/mo", "font-semi", "rms-usage"));
            body.Add(bottom);
            card.Add(body);

            int id = r.Id;
            OnClick(card, () => RmsShowOnMap(id));
            return card;
        }

        static VisualElement RmsMeter(string icon, float value)
        {
            var row = Box("rms-row", "rms-meter");
            row.Add(Icon(icon, "icon-13"));
            var bar = Box("bar", "rms-bar");
            var fill = Box("bar-fill");
            SetPercent(fill, value);
            fill.EnableInClassList("bar-fill--low", value < 40f);
            bar.Add(fill);
            row.Add(bar);
            row.Add(Text(Mathf.RoundToInt(value).ToString(), "font-heavy", "rms-meter-value"));
            return row;
        }

        static VisualElement RmsSiteBar(float progress, string left)
        {
            var box = Box("rms-site");
            var bar = Box("bar", "rms-site-bar");
            var fill = Box("bar-fill", "rms-site-fill");
            SetPercent(fill, progress * 100f);
            bar.Add(fill);
            box.Add(bar);
            box.Add(Text(left, "font-note", "rms-site-left"));
            return box;
        }

        InkBox RmsDecorChip(Room r)
        {
            var chip = Ink("rms-chip");
            var thumb = Box("rms-chip-thumb");
            thumb.style.backgroundImage = new StyleBackground(CampusArt.Room(r.Type, 1, r.Rotation));
            chip.Add(thumb);
            chip.EnableInClassList("rms-chip--worn", r.Condition < 40f);
            int id = r.Id;
            OnClick(chip, () => RmsShowOnMap(id));
            return chip;
        }

        /// <summary>Closes the page, selects the room and flies the camera to it.</summary>
        void RmsShowOnMap(int roomId)
        {
            var room = _sim?.State.FindRoom(roomId);
            if (room == null) return;
            CloseSheet();
            CampusController.Instance?.Select(roomId);
            FrameRoom(room);
        }
    }
}
