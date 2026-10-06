using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>Notebook pages the HUD draws itself. Each lives in its own GameHud.Page*.cs file.</summary>
    public enum HudPage
    {
        None, News, Stats, Ranks,
        Inbox, Students, Staff, Money, Rules, Admin, Admissions, Clubs, Report, Reports, Log, Rooms,
    }

    /// <summary>
    /// Everything on screen in the blue notebook style, built with UI Toolkit: the title menu and
    /// new-school screens, then in game the top bar, divider tabs, inbox clipboard, promises,
    /// vitals, toasts, the assistant and its tutorial, the campus panels and a notebook page for
    /// every part of the school. In development builds F1 swaps to the plain <see cref="DebugUI"/>.
    /// </summary>
    public sealed partial class GameHud : MonoBehaviour
    {
        const float RefreshInterval = 0.2f;

        UIDocument _doc;
        PanelSettings _panelSettings;
        VisualElement _root, _hud, _rightColumn, _nav;
        InkBox _sheetHead, _moreMenu, _pageHost, _vitalsCard;
        InkBox _tabInbox, _tabNews, _tabStudents, _tabStaff, _tabBuild, _tabMoney, _tabClubs, _tabStats, _tabRanks, _tabRules, _tabMore;
        InkIcon _sheetIcon;
        Label _sheetTitle, _inboxBadge, _newsBadge, _timeOfDay;
        DayDial _dayDial;

        DebugUI _debug;
        Simulation _sim;
        bool _built, _visible = true, _moreOpen, _campusBusy;
        float _nextRefresh;
        Func<Vector2, bool> _pointerCheck;
        HudPage _page;

        GameController Game => GameController.Instance;
        GameState S => _sim.State;

        void Start()
        {
            _debug = GetComponent<DebugUI>();
            _pointerCheck = IsPointerOverAnyUI;
            if (!BuildDocument())
            {
                enabled = false;   // the debug UI keeps working on its own
                return;
            }
            if (_debug != null)
            {
                _debug.Embedded = true;
                _debug.ToastRequested += OnDebugToast;
            }
            if (Game != null)
            {
                Game.MessageArrived += OnMessageArrived;
                Game.GameStarted += OnGameStarted;
            }
        }

        void OnDestroy()
        {
            if (_debug != null) _debug.ToastRequested -= OnDebugToast;
            if (Game != null)
            {
                Game.MessageArrived -= OnMessageArrived;
                Game.GameStarted -= OnGameStarted;
            }
            UnbindSim();
        }

        bool BuildDocument()
        {
            var tree = Resources.Load<VisualTreeAsset>("Hud/Hud");
            var theme = Resources.Load<ThemeStyleSheet>("Hud/HudTheme");
            if (tree == null || theme == null)
            {
                Debug.LogError("GameHud: Resources/Hud/Hud.uxml or HudTheme.tss is missing, keeping the debug UI.");
                return false;
            }

            // Tools > School Sim > Rebuild HUD Panel Settings writes this asset; build one if it's missing.
            // A copy, so the UI size setting never edits the asset itself.
            var asset = Resources.Load<PanelSettings>("Hud/HudPanelSettings");
            if (asset != null) _panelSettings = Instantiate(asset);
            else
            {
                _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                _panelSettings.themeStyleSheet = theme;
                _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                _panelSettings.screenMatchMode = PanelScreenMatchMode.Expand;
            }
            _panelSettings.name = "Notebook HUD";
            ApplyUiSize();

            var go = new GameObject("Notebook HUD");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            _doc = go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _doc.visualTreeAsset = tree;
            go.SetActive(true);

            _root = _doc.rootVisualElement;
            _hud = _root?.Q("hud");
            if (_hud == null)
            {
                Debug.LogError("GameHud: the HUD layout did not load, keeping the debug UI.");
                Destroy(go);
                return false;
            }
            _root.pickingMode = PickingMode.Ignore;
            Bind();
            BuildScreens();
            _built = true;
            return true;
        }

        /// <summary>The notebook's size setting: "Small" fits more on screen, "Normal" is the design size.</summary>
        void ApplyUiSize()
        {
            if (_panelSettings == null) return;
            _panelSettings.referenceResolution = GameSettings.UiSize == 0 ? new Vector2Int(1600, 900) : new Vector2Int(1440, 810);
        }

        void Bind()
        {
            _rightColumn = _root.Q("rightColumn");
            _nav = _root.Q("nav");
            _sheetHead = _root.Q<InkBox>("sheetHead");
            _sheetTitle = _root.Q<Label>("sheetTitle");
            _sheetIcon = _root.Q<InkIcon>("sheetIcon");
            _moreMenu = _root.Q<InkBox>("moreMenu");
            _pageHost = _root.Q<InkBox>("pageHost");
            _vitalsCard = _root.Q<InkBox>("vitals");
            _inboxBadge = _root.Q<Label>("inboxBadge");
            _newsBadge = _root.Q<Label>("newsBadge");
            _dayDial = _root.Q<DayDial>("dayDial");
            _timeOfDay = _root.Q<Label>("timeOfDay");

            _tabInbox = NavTab("tabInbox", () => OpenPage(HudPage.Inbox));
            _tabNews = NavTab("tabNews", () => OpenPage(HudPage.News));
            _tabStudents = NavTab("tabStudents", () => OpenPage(HudPage.Students));
            _tabStaff = NavTab("tabStaff", () => OpenPage(HudPage.Staff));
            _tabBuild = NavTab("tabBuild", ToggleBuild);
            _tabMoney = NavTab("tabMoney", () => OpenPage(HudPage.Money));
            _tabClubs = NavTab("tabClubs", () => OpenPage(HudPage.Clubs));
            _tabStats = NavTab("tabStats", () => OpenPage(HudPage.Stats));
            _tabRanks = NavTab("tabRanks", () => OpenPage(HudPage.Ranks));
            _tabRules = NavTab("tabRules", () => OpenPage(HudPage.Rules));
            _tabMore = NavTab("tabMore", ToggleMore);

            OnClick(_root.Q("sheetClose"), CloseSheet);
            OnClick(_root.Q("openAll"), () => ShowPageAt(HudPage.Inbox));
            OnClick(_root.Q("whyLink"), () => OpenStats(StatsMetric.ForStat(StatId.StudentSatisfaction)));

            BindTopBar();
            BuildMoreMenu();
            BuildInbox();
            BuildPromises();
            BuildVitals();
            BuildDock();
            BuildCampusPanels();
            BuildPersonCard();

            _rightColumn.RegisterCallback<GeometryChangedEvent>(_ => FitRightColumn());
            _hud.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                _fitSignature = int.MinValue;   // new screen size: try showing everything again
                _nextRefresh = 0f;
            });
        }

        InkBox NavTab(string name, Action onClick)
        {
            var tab = _root.Q<InkBox>(name);
            OnClick(tab, () =>
            {
                ClearNewSticker(tab);
                onClick();
            });
            return tab;
        }

        static void OnClick(VisualElement e, Action action)
        {
            if (e == null) return;
            void Press()
            {
                GameAudio.Play(Sfx.Tap);
                action();
            }
            e.AddManipulator(new Clickable(Press));
            Presses.Remove(e);
            Presses.Add(e, Press);
        }

        /// <summary>Each button's action, so the screenshot tours can press buttons without a mouse.</summary>
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, Action> Presses =
            new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, Action>();

        /// <summary>Presses the button an element belongs to (itself or the nearest parent with a click). For tours.</summary>
        internal static bool TourPress(VisualElement e)
        {
            for (var x = e; x != null; x = x.parent)
                if (Presses.TryGetValue(x, out var press))
                {
                    press();
                    return true;
                }
            return false;
        }

        // --- Frame ----------------------------------------------------------------------------

        void Update()
        {
            if (!_built || Game == null) return;
            var kb = Keyboard.current;
            bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
            bool playing = Game.Phase == GamePhase.Playing && Game.Sim != null;

            if (playing && _sim != Game.Sim) BindSim(Game.Sim);
            if (UpdateMenus(playing, esc)) esc = false;

            _hud.EnableInClassList("hud--hidden", !playing || !_visible || _tourHidden);
            if (!playing)
            {
                // On the title screens the school is out of reach: no map clicks, keys or camera moves.
                var campus = CampusController.Instance;
                if (campus != null) campus.InputEnabled = false;
                return;
            }

            if (Debug.isDebugBuild && kb != null && kb.f1Key.wasPressedThisFrame && !ModalOpen) SetVisible(!_visible);
            if (!_visible) return;

            if (esc) OnEscape();
            CloseMoreOnOutsideClick();
            RecordHistory();
            UpdateScreens();
            UpdateClock();
            UpdateToasts();
            UpdateAssistant();
            if (GameClock.Now >= _nextRefresh)
            {
                _nextRefresh = GameClock.Now + RefreshInterval;
                RefreshData();
                RefreshPage();
            }
        }

        void LateUpdate()
        {
            var ctl = CampusController.Instance;
            _campusBusy = ctl != null && (ctl.Tool != CampusTool.Inspect || ctl.SelectedRoomId >= 0 || ctl.SelectedPerson >= 0);
        }

        void BindSim(Simulation sim)
        {
            UnbindSim();
            _sim = sim;
            _sim.Logged += OnLogged;
            _sim.NewsPublished += OnNews;
            _sim.Revealed += OnRevealed;
            _sim.LeveledUp += OnLeveledUp;
            ResetHistory();
            ClearToasts();
            ResetAssistant();
            ResetPages();
            _nextRefresh = 0f;
        }

        void UnbindSim()
        {
            if (_sim == null) return;
            _sim.Logged -= OnLogged;
            _sim.NewsPublished -= OnNews;
            _sim.Revealed -= OnRevealed;
            _sim.LeveledUp -= OnLeveledUp;
            _sim = null;
        }

        void OnGameStarted()
        {
            _page = HudPage.None;
            _visible = true;
            if (_debug != null)
            {
                _debug.Embedded = true;
                _debug.ShowTab(DebugUI.Tab.Campus);
            }
            var ctl = CampusController.Instance;
            if (ctl != null)
            {
                ctl.Select(-1);
                ctl.Cancel();
            }
            var view = CampusView.Instance;
            if (view != null && CampusCamera.Instance != null) CampusCamera.Instance.Frame(view.CampusCenter(), 10f);
        }

        void SetVisible(bool visible)
        {
            _visible = visible;
            _hud.EnableInClassList("hud--hidden", !visible);
            if (_debug != null)
            {
                _debug.Embedded = visible;
                if (visible) _debug.ShowTab(DebugUI.Tab.Campus);   // its pages stay in the plain view
            }
            if (!visible) CloseMore();
        }

        void OnEscape()
        {
            if (_moreOpen) CloseMore();
            else if (PageOpen) CloseSheet();
            else if (_campusBusy) { }   // the campus controller drops its tool or selection
            else OpenGameMenu();
        }

        bool PageOpen => _page != HudPage.None;

        /// <summary>
        /// Decides what shares the screen with the campus: the notebook panels or a notebook page.
        /// Also keeps map input off while a page is open.
        /// </summary>
        void UpdateScreens()
        {
            var ctl = CampusController.Instance;
            bool page = _page != HudPage.None;
            bool building = ctl != null && ctl.Tool != CampusTool.Inspect;
            bool roomPanel = !page && ctl != null && (building || ctl.SelectedRoomId >= 0);
            bool settings = _debug != null && _debug.SettingsOpen;

            _sheetHead.EnableInClassList("sheet-head--open", page);
            _pageHost.EnableInClassList("hidden", _page == HudPage.None);
            _rightColumn.EnableInClassList("hidden", page || roomPanel || settings);
            if (page) ShowSheetTitle(_page);

            if (ctl != null)
            {
                ctl.PointerOverUI = _pointerCheck;
                ctl.InputEnabled = !page && !settings && !ModalOpen && !S.GameOver;
                if (ctl.SelectedRoomId >= 0) _sim.TutorialSignal("select:room");
            }
            UpdateCampusPanels(page);
            UpdatePersonCard(page || settings);

            SetTabActive(_tabInbox, _page == HudPage.Inbox);
            SetTabActive(_tabNews, _page == HudPage.News);
            SetTabActive(_tabStudents, _page == HudPage.Students);
            SetTabActive(_tabStaff, _page == HudPage.Staff);
            SetTabActive(_tabBuild, !page && building);
            SetTabActive(_tabMoney, _page == HudPage.Money);
            SetTabActive(_tabRules, _page == HudPage.Rules);
            SetTabActive(_tabClubs, _page == HudPage.Clubs);
            SetTabActive(_tabStats, _page == HudPage.Stats);
            SetTabActive(_tabRanks, _page == HudPage.Ranks);
            SetTabActive(_tabMore, _moreOpen || IsMorePage(_page));
        }

        static bool IsMorePage(HudPage page) =>
            page == HudPage.Admissions || page == HudPage.Rooms || page == HudPage.Admin || page == HudPage.Report
            || page == HudPage.Reports || page == HudPage.Log;

        static void SetTabActive(VisualElement tab, bool active) => tab?.EnableInClassList("nav-tab--active", active);

        void ShowSheetTitle(HudPage page)
        {
            var (title, icon) = page switch
            {
                HudPage.News => ("In the news", "news"),
                HudPage.Stats => ("Statistics", "chart"),
                HudPage.Ranks => ("Rankings", "podium"),
                _ => HookedPageTitle(page),
            };
            SetText(_sheetTitle, title);
            _sheetIcon.Icon = icon;
        }

        VisualElement PickAt(Vector2 screen)
        {
            var panel = _root?.panel;
            if (panel == null) return null;
            var pos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            return panel.Pick(pos);
        }

        static bool IsInside(VisualElement e, VisualElement ancestor)
        {
            for (; e != null; e = e.parent)
                if (e == ancestor) return true;
            return false;
        }

        /// <summary>For the campus: is a screen position (origin bottom left) over any HUD element or debug panel?</summary>
        bool IsPointerOverAnyUI(Vector2 screen)
        {
            if (_debug != null && _debug.IsPointerOverUI(screen)) return true;
            if (!_visible) return false;
            var picked = PickAt(screen);
            if (picked == null || picked == _hud || picked == _screens) return false;
            return IsInside(picked, _hud) || IsInside(picked, _screens);
        }

        // --- Navigation -----------------------------------------------------------------------

        void OpenPage(HudPage page)
        {
            CloseMore();
            var ctl = CampusController.Instance;
            if (ctl != null && ctl.Tool != CampusTool.Inspect) ctl.Cancel();
            if (_debug != null) _debug.ShowTab(DebugUI.Tab.Campus);
            _page = _page == page ? HudPage.None : page;
            if (_page == HudPage.None) return;
            GameAudio.Play(Sfx.Page);
            ShowPage(_page);
            switch (_page)
            {
                case HudPage.Inbox: _sim?.TutorialSignal("open:inbox"); break;
                case HudPage.Students: _sim?.TutorialSignal("open:students"); break;
                case HudPage.Staff: _sim?.TutorialSignal("open:staff"); break;
                case HudPage.Money: _sim?.TutorialSignal("open:money"); break;
            }
        }

        void OpenStats(StatsMetric metric)
        {
            _statsMetric = metric;
            if (_page != HudPage.Stats) OpenPage(HudPage.Stats);
            else ShowPage(HudPage.Stats);
            _sim?.TutorialSignal("open:why");
        }

        void CloseSheet()
        {
            CloseMore();
            _page = HudPage.None;
            if (_debug != null) _debug.ShowTab(DebugUI.Tab.Campus);
        }

        void ToggleBuild()
        {
            CloseMore();
            var ctl = CampusController.Instance;
            if (ctl == null) return;
            _page = HudPage.None;
            if (_debug != null) _debug.ShowTab(DebugUI.Tab.Campus);
            if (ctl.Tool == CampusTool.Build) ctl.Cancel();
            else ctl.BeginBuild(ctl.BuildType);
        }

        // --- More menu ------------------------------------------------------------------------

        VisualElement _menuAdmissions, _menuAdmin, _menuReports;
        Label _admissionsCount;

        void BuildMoreMenu()
        {
            _menuAdmissions = AddMenuItem("letters", "Admissions", () => OpenPage(HudPage.Admissions), out _admissionsCount);
            AddMenuItem("house", "Rooms", () => OpenPage(HudPage.Rooms), out _);
            _menuAdmin = AddMenuItem("staff", "Administration", () => OpenPage(HudPage.Admin), out _);
            AddMenuItem("clipboard", "School report", () => OpenPage(HudPage.Report), out _);
            _menuReports = AddMenuItem("book", "Year reports", () => OpenPage(HudPage.Reports), out _);
            AddMenuItem("eye", "School log", () => OpenPage(HudPage.Log), out _);
            var line = new DashLine();
            line.AddToClassList("menu-sep");
            _moreMenu.Add(line);
            AddMenuItem("cog", "Settings", () =>
            {
                CloseMore();
                OpenSettings(fromGame: true);
            }, out _);
            AddMenuItem("door", "Main menu", () =>
            {
                CloseMore();
                LeaveToTitle();
            }, out _);
            if (Debug.isDebugBuild)
            {
                AddMenuItem("sliders", "Plain debug view", () => SetVisible(false), out var key);
                key.text = "F1";
            }
        }

        VisualElement AddMenuItem(string icon, string text, Action onClick, out Label count)
        {
            var row = new VisualElement();
            row.AddToClassList("menu-item");
            var glyph = new InkIcon(icon);
            glyph.AddToClassList("icon-20");
            glyph.AddToClassList("menu-icon");
            var label = new Label(text);
            label.AddToClassList("menu-label");
            count = new Label();
            count.AddToClassList("menu-count");
            row.Add(glyph);
            row.Add(label);
            row.Add(count);
            OnClick(row, onClick);
            _moreMenu.Add(row);
            return row;
        }

        void ToggleMore()
        {
            _moreOpen = !_moreOpen;
            _moreMenu.EnableInClassList("more-menu--open", _moreOpen);
        }

        void CloseMore()
        {
            if (!_moreOpen) return;
            _moreOpen = false;
            _moreMenu.EnableInClassList("more-menu--open", false);
        }

        void CloseMoreOnOutsideClick()
        {
            if (!_moreOpen) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var picked = PickAt(mouse.position.ReadValue());
            if (picked != null && (IsInside(picked, _moreMenu) || IsInside(picked, _tabMore))) return;
            CloseMore();
        }

        /// <summary>Shows only the tabs and menu rows the school has unlocked.</summary>
        void RefreshNav()
        {
            ShowTab(_tabInbox, _sim.IsRevealed(Reveal.Inbox));
            ShowTab(_tabNews, _sim.IsRevealed(Reveal.News));
            ShowTab(_tabStudents, _sim.IsRevealed(Reveal.Students));
            ShowTab(_tabStaff, _sim.IsRevealed(Reveal.Staff));
            ShowTab(_tabBuild, _sim.IsRevealed(Reveal.Build));
            ShowTab(_tabMoney, _sim.IsRevealed(Reveal.Money));
            ShowTab(_tabClubs, _sim.IsUnlocked(Feature.Clubs));
            ShowTab(_tabStats, _sim.IsRevealed(Reveal.Stats));
            ShowTab(_tabRanks, _sim.IsRevealed(Reveal.Rankings));
            ShowTab(_tabRules, _sim.IsRevealed(Reveal.Rules));
            ShowTab(_menuAdmissions, _sim.IsRevealed(Reveal.Admissions));
            ShowTab(_menuAdmin, _sim.IsUnlocked(Feature.Administrators));
            ShowTab(_menuReports, S.Reports.Count > 0);
            _vitalsCard.EnableInClassList("hidden", !_sim.IsRevealed(Reveal.Vitals));
        }

        static void ShowTab(VisualElement e, bool on) => e?.EnableInClassList("hidden", !on);

        // --- Clock ----------------------------------------------------------------------------

        static readonly string[] DayParts = { "morning", "afternoon", "evening", "night" };

        void UpdateClock()
        {
            float t = Game.DayProgress;
            if (_dayDial != null) _dayDial.Progress = t;
            SetText(_timeOfDay, S.GameOver ? "closed" : DayParts[Mathf.Clamp((int)(t * 4f), 0, 3)]);
        }

        // --- Hooks for the automated screenshot tour -------------------------------------------

        internal void TourMore(bool open)
        {
            if (open != _moreOpen) ToggleMore();
        }

        internal void TourPlain(bool plain) => SetVisible(!plain);

        /// <summary>Hides the notebook HUD without switching to the debug view (trailer shots).</summary>
        internal void TourHideHud(bool hide) => _tourHidden = hide;

        bool _tourHidden;

        internal void TourPage(HudPage page)
        {
            if (_page != page) OpenPage(page);
            RefreshPage();
        }

        internal void TourCloseAll()
        {
            CloseSheet();
            CloseMenus();
            TourClearPopups();
            ClearToasts();
            HideBubble();
        }
    }
}
