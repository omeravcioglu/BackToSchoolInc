using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SchoolSim.Game
{
    public enum CampusTool { Inspect, Build, Land }

    /// <summary>
    /// Campus interaction: hover and select rooms, place new rooms with a rotatable ghost, and buy
    /// land parcels. All changes go through <see cref="Simulation"/> commands. Keys: B build,
    /// L land, R rotate, Esc cancel.
    /// </summary>
    public sealed class CampusController : MonoBehaviour
    {
        public static CampusController Instance { get; private set; }

        public CampusTool Tool { get; private set; } = CampusTool.Inspect;
        public RoomType BuildType { get; private set; } = RoomType.DormRoom;
        public int Rotation { get; private set; }
        public int SelectedRoomId { get; private set; } = -1;
        public int HoverRoomId { get; private set; } = -1;
        public long HoverPerson { get; private set; } = -1;

        /// <summary>Why the room can't go where the ghost is (null = it fits).</summary>
        public string PlacementProblem { get; private set; }
        public bool HasGhost { get; private set; }
        public TileRect Ghost { get; private set; }
        public Vector2Int HoverParcel { get; private set; } = new Vector2Int(-1, -1);

        /// <summary>Set by the UI: the map is the active screen.</summary>
        public bool InputEnabled { get; set; }

        /// <summary>Set by the UI: returns true when a screen position is over a panel.</summary>
        public Func<Vector2, bool> PointerOverUI { get; set; }

        public event Action<CommandResult> CommandDone;

        Vector2Int? _forcedCell;
        Vector2 _rightPressPos;

        static Simulation Sim => GameController.Instance != null ? GameController.Instance.Sim : null;

        void Awake() => Instance = this;

        public Room SelectedRoom => Sim?.State.FindRoom(SelectedRoomId);

        /// <summary>The student or staff member picked on the map (a <see cref="CampusPeople"/> key), -1 for none.</summary>
        public long SelectedPerson => CampusPeople.Instance != null ? CampusPeople.Instance.Selected : -1;

        public void BeginBuild(RoomType type)
        {
            Tool = CampusTool.Build;
            BuildType = type;
            SelectedRoomId = -1;
            CampusPeople.Instance?.Select(-1);
        }

        public void BeginLand()
        {
            Tool = CampusTool.Land;
            SelectedRoomId = -1;
            CampusPeople.Instance?.Select(-1);
        }

        public void Cancel()
        {
            if (Tool != CampusTool.Inspect) Tool = CampusTool.Inspect;
            else
            {
                SelectedRoomId = -1;
                CampusPeople.Instance?.Select(-1);
            }
        }

        public void Rotate() => Rotation = (Rotation + 1) & 3;

        public void Select(int roomId)
        {
            Tool = CampusTool.Inspect;
            SelectedRoomId = roomId;
            CampusPeople.Instance?.Select(-1);
        }

        public void SelectPerson(long key)
        {
            Tool = CampusTool.Inspect;
            SelectedRoomId = -1;
            CampusPeople.Instance?.Select(key);
        }

        /// <summary>Drives the pointer from code (automated screenshots). Null returns control to the mouse.</summary>
        public void ForcePointerCell(Vector2Int? cell, int rotation = -1)
        {
            _forcedCell = cell;
            if (rotation >= 0) Rotation = rotation & 3;
        }

        void Update()
        {
            var sim = Sim;
            var view = CampusView.Instance;
            var cam = CampusCamera.Instance;
            if (sim == null || view == null) return;

            if (SelectedRoomId >= 0 && sim.State.FindRoom(SelectedRoomId) == null) SelectedRoomId = -1;

            bool active = InputEnabled && !sim.State.GameOver;
            if (cam != null) cam.InputEnabled = active;
            view.BuildGridVisible = active && Tool == CampusTool.Build;
            view.SetSelected(SelectedRoom);
            if (!active)
            {
                HasGhost = false;
                view.HideGhost();
                view.SetHover(null);
                view.ShowLand(false, -1, -1);
                return;
            }

            HandleKeys();

            var mouse = Mouse.current;
            Vector2 screen = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            bool overUI = PointerOverUI != null && PointerOverUI(screen);
            if (cam != null) cam.PointerBlocked = overUI;

            Vector2Int cell;
            Vector2 world = default;
            bool hasCell;
            if (_forcedCell.HasValue)
            {
                cell = _forcedCell.Value;
                hasCell = true;
                overUI = false;
            }
            else
            {
                var camera = Camera.main;
                hasCell = mouse != null && camera != null && !overUI;
                if (camera != null) world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
                cell = new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
            }

            bool click = !_forcedCell.HasValue && hasCell && mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) _rightPressPos = screen;
            bool rightClick = !_forcedCell.HasValue && !overUI && mouse != null && mouse.rightButton.wasReleasedThisFrame
                              && (screen - _rightPressPos).sqrMagnitude < 36f;

            switch (Tool)
            {
                case CampusTool.Inspect:
                    HasGhost = false;
                    view.HideGhost();
                    view.ShowLand(false, -1, -1);
                    // People stand in front of rooms, so they win the click.
                    var people = CampusPeople.Instance;
                    long person = -1;
                    if (hasCell && !_forcedCell.HasValue && people != null) people.TryPick(world, out person);
                    HoverPerson = person;
                    HoverRoomId = hasCell && person < 0 ? sim.State.Campus.RoomIdAt(cell.x, cell.y) : 0;
                    view.SetHover(HoverRoomId != 0 && HoverRoomId != SelectedRoomId ? sim.State.FindRoom(HoverRoomId) : null);
                    if (click)
                    {
                        if (person >= 0) SelectPerson(person);
                        else
                        {
                            SelectedRoomId = HoverRoomId != 0 ? HoverRoomId : -1;
                            people?.Select(-1);
                        }
                    }
                    if (rightClick)
                    {
                        SelectedRoomId = -1;
                        people?.Select(-1);
                    }
                    break;

                case CampusTool.Build:
                    view.SetHover(null);
                    view.ShowLand(false, -1, -1);
                    if (!hasCell)
                    {
                        HasGhost = false;
                        view.HideGhost();
                    }
                    else UpdateGhost(sim, view, cell, click, shift);
                    if (rightClick) Cancel();
                    break;

                case CampusTool.Land:
                    view.SetHover(null);
                    view.HideGhost();
                    HasGhost = false;
                    int size = CampusMap.ParcelSize;
                    HoverParcel = hasCell ? new Vector2Int(Mathf.FloorToInt(cell.x / (float)size), Mathf.FloorToInt(cell.y / (float)size)) : new Vector2Int(-1, -1);
                    view.ShowLand(true, HoverParcel.x, HoverParcel.y);
                    if (click && sim.State.Campus.IsParcelPurchasable(HoverParcel.x, HoverParcel.y))
                    {
                        var result = sim.BuyParcel(HoverParcel.x, HoverParcel.y);
                        CommandDone?.Invoke(result);
                        if (result.Ok && !shift) Tool = CampusTool.Inspect;
                    }
                    if (rightClick) Cancel();
                    break;
            }
        }

        void UpdateGhost(Simulation sim, CampusView view, Vector2Int cell, bool click, bool shift)
        {
            var def = RoomCatalog.Get(BuildType);
            var probe = Placement.Footprint(def, 0, 0, Rotation);
            int x = cell.x - (probe.W - 1) / 2, y = cell.y - (probe.H - 1) / 2;
            var rect = Placement.Footprint(def, x, y, Rotation);

            string problem = sim.Facilities.WhyCantPlace(BuildType, x, y, Rotation);
            if (problem == null && !sim.Facilities.CanBuild(BuildType, out string why)) problem = why;
            PlacementProblem = problem;
            Ghost = rect;
            HasGhost = true;

            string note = problem ?? $"fits! {MathUtil.Money(def.Cost[0])}";
            view.ShowGhost(rect, Rotation, problem == null, $"{def.Name} · {rect.W}x{rect.H}", note, door: !def.Decor);

            if (!click) return;
            var result = sim.BuildRoomAt(BuildType, x, y, Rotation);
            CommandDone?.Invoke(result);
            if (result.Ok && !shift && !def.Decor)          // trees and benches come in rows: keep planting
            {
                var rooms = sim.State.Rooms;
                SelectedRoomId = rooms[rooms.Count - 1].Id;
                Tool = CampusTool.Inspect;
            }
        }

        void HandleKeys()
        {
            var kb = Keyboard.current;
            var sim = Sim;
            if (kb == null || GUIUtility.keyboardControl != 0) return;
            if (kb.bKey.wasPressedThisFrame && sim.IsRevealed(Reveal.Build))
            {
                if (Tool == CampusTool.Build) Tool = CampusTool.Inspect;
                else BeginBuild(BuildType);
            }
            if (kb.lKey.wasPressedThisFrame && sim.IsRevealed(Reveal.Land))
            {
                if (Tool == CampusTool.Land) Tool = CampusTool.Inspect;
                else BeginLand();
            }
            if (kb.rKey.wasPressedThisFrame && Tool == CampusTool.Build) Rotate();
            if (kb.escapeKey.wasPressedThisFrame) Cancel();
        }
    }
}
