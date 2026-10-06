using System;
using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    /// <summary>
    /// A complete, if plain, management UI drawn with IMGUI. It exists so the simulation can be
    /// played and tuned before the real 2D presentation is built. All state changes are deferred
    /// to the end of the GUI event so IMGUI layout never sees a list change mid-frame.
    /// </summary>
    public sealed partial class DebugUI : MonoBehaviour
    {
        public enum Tab { Campus, Overview, Inbox, Students, Staff, Rooms, Admissions, Finance, Programs, Admin, Rankings, Reports, Log }

        static readonly string[] TabNames = Enum.GetNames(typeof(Tab));

        [SerializeField] float uiScale = 1f;

        const float TopBarHeight = 76f;

        Tab _tab = Tab.Campus;
        float _guiScale = 1f;

        // Panels drawn this frame, in GUI coordinates; the campus ignores clicks on them.
        readonly List<Rect> _blockers = new List<Rect>();
        readonly List<Action> _deferred = new List<Action>();
        string _toast;
        bool _toastOk;
        float _toastUntil;
        float _w, _h;
        bool _showSettings;

        // Embedded mode: the notebook HUD (GameHud) owns the top bar, tabs and toasts, and tells
        // us where to draw the screens it doesn't have yet. Rects are screen pixels, origin top-left.
        Rect _embedContent, _embedStrip, _embedSide;
        Func<Vector2, bool> _pointerCheck;

        /// <summary>True while the notebook HUD is on screen and this UI only fills in panels.</summary>
        public bool Embedded { get; set; }

        /// <summary>Raised instead of drawing a toast while <see cref="Embedded"/>.</summary>
        public event Action<string, bool> ToastRequested;

        public Tab CurrentTab => _tab;
        public bool SettingsOpen => _showSettings;

        public void ShowTab(Tab tab)
        {
            _tab = tab;
            _showSettings = false;
        }

        public void ToggleSettings() => _showSettings = !_showSettings;

        public void OpenMessage(int messageId)
        {
            ShowTab(Tab.Inbox);
            _selectedMessage = messageId;
            var m = Sim?.State.FindMessage(messageId);
            if (m != null) m.Read = true;
        }

        public void ShowBreakdown(StatId stat)
        {
            ShowTab(Tab.Overview);
            _breakdownStat = (int)stat;
            _breakdownIsReputation = false;
        }

        public void SetEmbeddedLayout(Rect content, Rect strip, Rect side)
        {
            _embedContent = content;
            _embedStrip = strip;
            _embedSide = side;
        }

        Rect ToGui(Rect screen) => new Rect(screen.x / _guiScale, screen.y / _guiScale, screen.width / _guiScale, screen.height / _guiScale);

        GameController Game => GameController.Instance;
        Simulation Sim => Game != null ? Game.Sim : null;
        GameState S => Sim.State;

        // --- Deferred actions -------------------------------------------------------------

        void Defer(Action action) => _deferred.Add(action);

        void Run(Func<CommandResult> command)
        {
            Defer(() =>
            {
                var r = command();
                Toast(r.Message, r.Ok);
            });
        }

        void Toast(string message, bool ok = true)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (Embedded && ToastRequested != null)
            {
                ToastRequested(message, ok);
                return;
            }
            _toast = message;
            _toastOk = ok;
            _toastUntil = Time.unscaledTime + 4f;
        }

        // --- Frame ------------------------------------------------------------------------

        void OnGUI()
        {
            if (Sim == null || Game.Phase != GamePhase.Playing) return;
            EnsureStyles();

            float scale = Mathf.Max(0.7f, Screen.height / 900f) * uiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _guiScale = scale;
            _w = Screen.width / scale;
            _h = Screen.height / scale;
            if (Event.current.type == EventType.Layout) _blockers.Clear();
            HookCampus();

            HandleKeys();
            if (Embedded)
            {
                if (_tab == Tab.Campus) DrawCampusMap();
                else if (_embedContent.width > 10f) DrawContent(ToGui(_embedContent));
            }
            else
            {
                var topBar = new Rect(0, 0, _w, TopBarHeight);
                var tabs = new Rect(4, TopBarHeight + 4, 118, _h - TopBarHeight - 8);
                DrawTopBar(topBar);
                DrawTabs(tabs);
                Block(topBar);
                Block(tabs);

                if (_tab == Tab.Campus) DrawCampusMap();
                else DrawContent(new Rect(124, TopBarHeight + 4, _w - 128, _h - TopBarHeight - 8));
            }

            if (_showSettings) DrawSettings();
            if (S.GameOver && !Embedded) DrawGameOver();
            if (!Embedded) DrawToast();
            DrawTooltip();

            if (_deferred.Count > 0 && Event.current.type != EventType.Layout)
            {
                var actions = _deferred.ToArray();
                _deferred.Clear();
                foreach (var a in actions) a();
            }
        }

        void DrawContent(Rect content)
        {
            Panel(content);
            Block(content);
            GUILayout.BeginArea(new Rect(content.x + 4, content.y, content.width - 8, content.height));
            switch (_tab)
            {
                case Tab.Overview: DrawOverview(); break;
                case Tab.Inbox: DrawInbox(); break;
                case Tab.Students: DrawStudents(); break;
                case Tab.Staff: DrawStaff(); break;
                case Tab.Rooms: DrawRoomsTab(); break;
                case Tab.Admissions: DrawAdmissions(); break;
                case Tab.Finance: DrawFinance(); break;
                case Tab.Programs: DrawPrograms(); break;
                case Tab.Admin: DrawAdmin(); break;
                case Tab.Rankings: DrawRankings(); break;
                case Tab.Reports: DrawReports(); break;
                case Tab.Log: DrawLog(); break;
            }
            GUILayout.EndArea();
        }

        void Block(Rect r) => _blockers.Add(r);

        /// <summary>True when a screen position (pixels, origin bottom-left) is over one of our panels.</summary>
        public bool IsPointerOverUI(Vector2 screen)
        {
            var g = new Vector2(screen.x / _guiScale, (Screen.height - screen.y) / _guiScale);
            foreach (var r in _blockers) if (r.Contains(g)) return true;
            return false;
        }

        void HandleKeys()
        {
            // Time can't be paused or sped up, so the debug UI has no time keys any more.
        }

        // --- Top bar ----------------------------------------------------------------------

        void DrawTopBar(Rect r)
        {
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 8, r.y + 4, r.width - 16, r.height - 8));
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(260));
            GUILayout.Label(S.SchoolName, _h1);
            string season = Sim.InSession ? (Calendar.IsExamMonth(S.Day) ? "Exams" : "School term") : "Summer · Admissions Season";
            GUILayout.Label($"{Calendar.Format(S.Day)} · {season}", _small);
            GUILayout.EndVertical();

            var proj = Sim.Economy.Project();
            TopStat("Cash", MathUtil.Money(S.Finance.Cash), $"{MathUtil.Signed(proj.Net, "N0")}/mo projected",
                S.Finance.Cash < 0 ? Bad : proj.Net < 0 ? Warn : Good, 150);
            TopStat("Students", $"{S.Students.Count} / {Sim.Indices.Beds} beds", $"{Sim.Indices.FreeBeds} free", Sim.Indices.FreeBeds == 0 ? Warn : Neutral, 120);
            TopStat("Prestige", $"{S.Prestige:0} · #{S.NationalRank}", S.Specialization == Specialization.None ? "No specialization" : Names.Specialization(S.Specialization), Neutral, 150);
            TopStat("School level", $"{S.Progress.SchoolLevel}", $"{Sim.Progression.SchoolXpToNext} XP to next", Neutral, 110);
            TopStat("Director", $"Level {S.Progress.DirectorLevel}", Sim.Progression.ShowHints ? "Sees hidden consequences" : "", Neutral, 130);

            int pending = Sim.Inbox.PendingDecisions;
            GUI.color = pending > 0 ? Warn : Color.white;
            if (GUILayout.Button($"Inbox\n{pending} to decide", GUILayout.Width(100), GUILayout.Height(44))) Defer(() => _tab = Tab.Inbox);
            GUI.color = Color.white;

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            if (GUILayout.Button(new GUIContent("Menu", "Settings and main menu"), GUILayout.Width(80), GUILayout.Height(24))) Defer(() => _showSettings = !_showSettings);
            GUILayout.Label($"One day every {GameController.SecondsPerDay:0} s. The clock never stops.", _small, GUILayout.Width(320));
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void TopStat(string label, string value, string sub, Color color, float width)
        {
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label(label, _small);
            GUI.color = color;
            GUILayout.Label(value, _bold);
            GUI.color = Color.white;
            GUILayout.Label(sub, _small);
            GUILayout.EndVertical();
        }

        void DrawTabs(Rect r)
        {
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8));
            for (int i = 0; i < TabNames.Length; i++)
            {
                var tab = (Tab)i;
                string label = TabNames[i];
                if (tab == Tab.Inbox && Sim.Inbox.PendingDecisions > 0) label += $" ({Sim.Inbox.PendingDecisions})";
                if (tab == Tab.Admissions && S.Applicants.Count > 0) label += $" ({S.Applicants.Count})";
                GUI.color = _tab == tab ? Good : Color.white;
                if (GUILayout.Button(label, GUILayout.Height(30))) Defer(() => _tab = tab);
            }
            GUI.color = Color.white;
            GUILayout.EndArea();
        }

        // --- Overlays ---------------------------------------------------------------------

        void DrawSettings()
        {
            float top = Embedded && _embedSide.height > 10f ? ToGui(_embedSide).y : TopBarHeight + 4;
            var rect = new Rect(_w - 330, top, 320, 150);
            Panel(rect);
            Block(rect);
            GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 8, rect.width - 20, rect.height - 16));
            GUILayout.Label("Settings", _h2);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"UI scale {uiScale:0.00}", GUILayout.Width(100));
            float scale = GUILayout.HorizontalSlider(uiScale, 0.7f, 1.6f);
            if (Math.Abs(scale - uiScale) > 0.001f) Defer(() => uiScale = scale);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            if (GUILayout.Button("Main menu (the clock stops while you're away)")) Defer(() =>
            {
                _showSettings = false;
                Game.ShowTitle();
            });
            GUILayout.EndArea();
        }

        void DrawGameOver()
        {
            var rect = new Rect(_w / 2 - 220, _h / 2 - 90, 440, 180);
            Panel(rect);
            Block(rect);
            GUILayout.BeginArea(new Rect(rect.x + 16, rect.y + 12, rect.width - 32, rect.height - 24));
            GUILayout.Label("School closed", _h1);
            GUILayout.Label(S.GameOverReason, _wrap);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back to the main menu", GUILayout.Height(30))) Defer(() =>
            {
                Game.ShowTitle();
                ResetSelections();
            });
            GUILayout.EndArea();
        }

        void DrawToast()
        {
            if (_toast == null || Time.unscaledTime > _toastUntil) return;
            var size = _wrap.CalcHeight(new GUIContent(_toast), 520);
            var rect = new Rect(_w / 2 - 270, _h - size - 40, 540, size + 20);
            GUI.color = _toastOk ? new Color(0.75f, 1f, 0.8f) : new Color(1f, 0.75f, 0.7f);
            Panel(rect);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 10, rect.y + 10, 520, size), _toast, _wrap);
        }

        void DrawTooltip()
        {
            string tip = GUI.tooltip;
            if (string.IsNullOrEmpty(tip)) return;
            var mouse = Event.current.mousePosition;
            float height = _wrap.CalcHeight(new GUIContent(tip), 320);
            var rect = new Rect(Mathf.Min(mouse.x + 16, _w - 340), Mathf.Min(mouse.y + 16, _h - height - 20), 340, height + 16);
            Panel(rect);
            GUI.Label(new Rect(rect.x + 10, rect.y + 8, 320, height), tip, _wrap);
        }

        // --- Used by UiTour to screenshot every tab ------------------------------------------

        internal static int TabCount => TabNames.Length;
        internal static string TabTitle(int index) => TabNames[index];

        internal void TourShow(int index)
        {
            _tab = (Tab)index;
            _showSettings = false;
            _selectedMessage = S.Inbox.Count > 0 ? S.Inbox[S.Inbox.Count - 1].Id : -1;
            foreach (var m in S.Inbox) if (m.NeedsDecision) _selectedMessage = m.Id;
            _selectedStudent = S.Students.Count > 0 ? S.Students[0].Id : -1;
            _selectedStaff = S.Staff.Count > 0 ? S.Staff[0].Id : -1;
            _selectedRoom = S.Rooms.Count > 0 ? S.Rooms[0].Id : -1;
        }

        void ResetSelections()
        {
            _selectedMessage = -1;
            _selectedStudent = -1;
            _selectedStaff = -1;
            _selectedRoom = -1;
            _selectedTeam = -1;
            _selectedReport = -1;
            _offerApplicant = -1;
            _swapFrom = -1;
            _tuitionDraft = -1f;
        }
    }
}
