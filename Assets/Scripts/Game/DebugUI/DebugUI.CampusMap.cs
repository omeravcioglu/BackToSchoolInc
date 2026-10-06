using System;
using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>
    /// Panels for the campus map screen: a tool strip across the top and a side panel with the
    /// build catalog, land buying, or the selected room. The map itself is drawn by CampusView.
    /// </summary>
    public sealed partial class DebugUI
    {
        static readonly RoomCategory[] BuildCategories =
        {
            RoomCategory.Academic, RoomCategory.Residential, RoomCategory.Dining, RoomCategory.StudentLife,
            RoomCategory.Staff, RoomCategory.Health, RoomCategory.Sports, RoomCategory.Outdoors, RoomCategory.Decor,
        };
        static readonly string[] BuildCategoryNames = { "Academic", "Residential", "Dining", "Student life", "Staff", "Health", "Sports", "Outdoors", "Decor" };
        static readonly string[] OverlayNames = { "None", "Condition", "Cleanliness", "Beds" };

        int _buildCategory = 1;
        Vector2 _campusPanelScroll;
        bool _campusHooked;

        void HookCampus()
        {
            var ctl = CampusController.Instance;
            if (ctl == null) return;
            if (!_campusHooked)
            {
                _campusHooked = true;
                ctl.CommandDone += r => Toast(r.Message, r.Ok);
            }
            if (Embedded) return;   // the HUD drives pointer blocking and map input
            if (_pointerCheck == null) _pointerCheck = IsPointerOverUI;
            ctl.PointerOverUI = _pointerCheck;
            ctl.InputEnabled = _tab == Tab.Campus && !_showSettings && !S.GameOver;
        }

        void FocusOn(Room r)
        {
            var f = r.Footprint;
            CampusCamera.Instance?.Frame(new Vector2(f.X + f.W / 2f, f.Y + f.H / 2f), 8f);
        }

        void DrawCampusMap()
        {
            var ctl = CampusController.Instance;
            var view = CampusView.Instance;
            if (ctl == null || view == null) return;

            // Inside the notebook HUD the strip only shows while building, and the side panel only
            // while building or with a room selected: the HUD has its own panels the rest of the time.
            bool tool = ctl.Tool != CampusTool.Inspect;
            bool showStrip = !Embedded || (tool && _embedStrip.width > 10f);
            bool showPanel = !Embedded || ((tool || ctl.SelectedRoom != null) && _embedSide.width > 10f);
            var strip = Embedded ? ToGui(_embedStrip) : new Rect(128, TopBarHeight + 6, Mathf.Min(760, _w - 490), 42);
            var panel = Embedded ? ToGui(_embedSide) : new Rect(_w - 356, TopBarHeight + 6, 350, _h - TopBarHeight - 12);

            // Tool strip
            if (showStrip)
            {
                Panel(strip);
                Block(strip);
                GUILayout.BeginArea(new Rect(strip.x + 6, strip.y + 7, strip.width - 12, strip.height - 10));
                GUILayout.BeginHorizontal();
                ToolButton("Select", ctl.Tool == CampusTool.Inspect, () => { if (ctl.Tool != CampusTool.Inspect) ctl.Cancel(); });
                ToolButton("Build (B)", ctl.Tool == CampusTool.Build, () => ctl.BeginBuild(ctl.BuildType));
                if (Sim.IsRevealed(Reveal.Land)) ToolButton("Buy land (L)", ctl.Tool == CampusTool.Land, ctl.BeginLand);
                GUILayout.Space(14);
                GUILayout.Label("Overlay", _small, GUILayout.Width(46));
                int overlay = GUILayout.Toolbar((int)view.Overlay, OverlayNames, GUILayout.Width(300));
                if (overlay != (int)view.Overlay) Defer(() => view.Overlay = (CampusOverlay)overlay);
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
            if (!showPanel) return;

            // Side panel
            Panel(panel);
            Block(panel);
            GUILayout.BeginArea(new Rect(panel.x + 10, panel.y + 8, panel.width - 20, panel.height - 16));
            _campusPanelScroll = GUILayout.BeginScrollView(_campusPanelScroll);
            switch (ctl.Tool)
            {
                case CampusTool.Build: DrawBuildPalette(ctl); break;
                case CampusTool.Land: DrawLandPanel(ctl); break;
                default:
                    var room = ctl.SelectedRoom;
                    if (room != null) DrawSelectedRoom(ctl, room);
                    else DrawCampusHelp(ctl);
                    break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void ToolButton(string label, bool active, Action onClick)
        {
            GUI.color = active ? Good : Color.white;
            if (GUILayout.Button(label, GUILayout.Width(104), GUILayout.Height(26))) Defer(onClick);
            GUI.color = Color.white;
        }

        void DrawBuildPalette(CampusController ctl)
        {
            GUILayout.Label("Build mode", _h1);
            GUILayout.Label("Click to place · R rotates · Shift+click keeps building · Esc or right-click leaves", _small);
            int cat = GUILayout.SelectionGrid(_buildCategory, BuildCategoryNames, 3);
            if (cat != _buildCategory) Defer(() => _buildCategory = cat);
            GUILayout.Space(4);

            int shown = 0;
            foreach (var def in RoomCatalog.All)
            {
                if (def.Category != BuildCategories[_buildCategory]) continue;
                // Rooms appear as the school grows: what's open now, plus a peek at the next level.
                if (def.MinSchoolLevel > S.Progress.SchoolLevel + 1) continue;
                shown++;
                var type = def.Type;
                Sim.Facilities.CanBuild(type, out string why);
                bool locked = S.Progress.SchoolLevel < def.MinSchoolLevel;
                bool selected = ctl.BuildType == type;
                string label = $"<b>{def.Name}</b>   {def.Width}x{def.Height}   {Money(def.Cost[0])}" + (locked ? $"   [level {def.MinSchoolLevel}]" : "");
                GUI.color = selected ? Good : locked ? Dim : Color.white;
                if (GUILayout.Button(label, _rowButton, GUILayout.Height(30))) Defer(() => ctl.BeginBuild(type));
                GUI.color = Color.white;
            }
            if (shown == 0) GUILayout.Label("Nothing here yet. More rooms unlock as the school levels up.", _small);

            var sel = RoomCatalog.Get(ctl.BuildType);
            GUILayout.Space(8);
            GUILayout.BeginVertical(_panel);
            GUILayout.Label(sel.Name, _h2);
            Colored($"Lv 1 of {sel.MaxLevel}: {sel.LevelNames[0]}", Neutral, _small);
            GUILayout.Label(sel.Description, _wrap);
            KeyValue("Cost", $"{Money(sel.Cost[0])} · {sel.BuildDays[0]} days to build", 90);
            KeyValue("Upkeep", $"{Money(sel.Upkeep[0])}/mo", 90);
            KeyValue(sel.CapacityLabel, sel.Capacity[0].ToString(), 90);
            KeyValue("Footprint", $"{sel.Width}x{sel.Height} tiles, door on a long side", 90);
            if (sel.Type == RoomType.DormRoom) Colored($"+{Money(sel.MonthlyFee[0])} per boarder each month", Good, _small);
            GUILayout.EndVertical();

            GUILayout.Space(6);
            if (!ctl.HasGhost) GUILayout.Label("Move the pointer over your land.", _small);
            else if (ctl.PlacementProblem == null) Colored($"Fits here. Click to build it for {Money(sel.Cost[0])}.", Good);
            else Colored(ctl.PlacementProblem, Bad);
            GUILayout.Label("The door (arrow) must open onto a free tile of your land, and rooms can't block other doors.", _small);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Rotate (R) · door {Placement.SideName(ctl.Rotation)}")) Defer(ctl.Rotate);
            if (GUILayout.Button("Done (Esc)", GUILayout.Width(100))) Defer(ctl.Cancel);
            GUILayout.EndHorizontal();
        }

        void DrawLandPanel(CampusController ctl)
        {
            var map = S.Campus;
            float price = Sim.Facilities.LandCost;
            GUILayout.Label("Buy land", _h1);
            GUILayout.Label($"Land comes in {CampusMap.ParcelSize}x{CampusMap.ParcelSize} parcels. Only parcels touching your campus are for sale, and each one costs more than the last.", _wrap);
            KeyValue("Next parcel", Money(price), 110);
            KeyValue("You own", $"{map.OwnedParcelCount} parcels ({map.OwnedTiles} tiles)", 110);
            KeyValue("Built on", $"{map.UsedTiles} tiles", 110);
            GUILayout.Space(8);
            var hp = ctl.HoverParcel;
            if (map.ParcelInBounds(hp.x, hp.y))
            {
                Sim.Facilities.CanBuyParcel(hp.x, hp.y, out string why);
                if (why == null) Colored($"Click to buy this parcel for {Money(Sim.Facilities.ParcelCost(hp.x, hp.y))}.", Good);
                else Colored(why, map.IsParcelOwned(hp.x, hp.y) ? Neutral : Bad);
            }
            else GUILayout.Label("Point at a highlighted parcel.", _small);
            GUILayout.Space(6);
            if (GUILayout.Button("Done (Esc)")) Defer(ctl.Cancel);
        }

        void DrawSelectedRoom(CampusController ctl, Room r)
        {
            var room = r;
            GUILayout.Label(r.Name, _h1);
            string status = !r.IsBuilt(S.Day) ? $"Under construction · opens {Calendar.Format(r.ConstructionDoneDay)}"
                : r.IsUpgrading ? $"Upgrading to Lv {r.PendingLevel} · done {Calendar.Format(r.ConstructionDoneDay)}"
                : r.IsClosed(S.Day) ? $"CLOSED until {Calendar.Format(r.ClosedUntilDay)}"
                : $"Lv {r.Level}: {r.LevelName}";
            Colored(status, r.IsClosed(S.Day) ? Bad : Neutral, _small);
            GUILayout.Space(4);
            LabeledBar("Condition", r.Condition, 110, 150);
            LabeledBar("Cleanliness", r.Cleanliness, 110, 150);
            LabeledBar("Quality", r.Quality, 110, 150);
            KeyValue("Usage", Usage(r), 110);
            GUILayout.Space(6);
            DrawRoomDetails(r, wide: false);
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Deselect (Esc)")) Defer(() => ctl.Select(-1));
            if (GUILayout.Button("Centre view", GUILayout.Width(110))) Defer(() => FocusOn(room));
            GUILayout.EndHorizontal();
        }

        void DrawCampusHelp(CampusController ctl)
        {
            var map = S.Campus;
            GUILayout.Label("Your campus", _h1);
            KeyValue("Land", $"{map.OwnedParcelCount} parcels", 110);
            KeyValue("Built on", $"{map.UsedTiles} of {map.OwnedTiles} tiles", 110);
            KeyValue("Rooms", S.Rooms.Count.ToString(), 110);
            KeyValue("Free beds", Sim.Indices.FreeBeds.ToString(), 110);
            GUILayout.Space(8);
            GUILayout.Label("Click a room to inspect, upgrade or repair it.", _wrap);
            GUILayout.Label("B opens build mode, L buys land. Pan with WASD, the arrow keys or right-drag; scroll to zoom.", _small);
            GUILayout.Space(8);
            GUILayout.Label("Quick build", _bold);
            foreach (var type in new[] { RoomType.DormRoom, RoomType.Bathroom, RoomType.Classroom, RoomType.StudentLounge })
            {
                var t = type;
                var def = RoomCatalog.Get(type);
                if (GUILayout.Button($"{def.Name} · {Money(def.Cost[0])}", _rowButton, GUILayout.Height(28))) Defer(() => ctl.BeginBuild(t));
            }
            if (Sim.Indices.BathroomShortage > 0.01f) Colored("Boarders are queueing for the bathrooms.", Warn, _small);
            if (Sim.Indices.FreeBeds == 0) Colored("Every bed is taken. More dorm rooms mean more tuition.", Dim, _small);
        }
    }
}
