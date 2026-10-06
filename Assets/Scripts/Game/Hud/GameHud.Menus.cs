using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// Full-screen screens from the design canvas: the title menu, founding a school, picking an
    /// assistant, settings, the in-game menu (which doesn't stop the clock), level-ups and the
    /// end of a school. Built from code on top of the HUD.
    /// </summary>
    public sealed partial class GameHud
    {
        enum MenuScreen { None, Title, NewSchool, Assistant, Settings, GameMenu, Load }

        VisualElement _screens, _titleLayer, _newSchoolLayer, _assistantLayer, _settingsLayer, _gameMenuLayer, _levelLayer, _gameOverLayer;
        MenuScreen _menu = MenuScreen.Title, _settingsReturn = MenuScreen.Title;
        bool _quitAsked, _levelShown, _gameOverShown;

        // Title
        InkBox _continueButton, _quitNote;
        Label _continueSub;
        MiniCampus _continueMini;
        (object school, int day) _continueKey;

        // New school
        string _draftName = "Little Oak School";
        int _draftEmblem, _draftColor, _draftSeed;
        TextField _nameField;
        CrestView _crestPreview;
        Label _crestName;
        readonly List<InkBox> _emblemButtons = new List<InkBox>(), _swatches = new List<InkBox>();

        // Assistant pick
        List<Assistant> _candidates;
        int _candidate = -1;
        bool _tutorialOn = true;
        VisualElement _candidateRow;
        readonly List<InkBox> _candidateCards = new List<InkBox>();
        InkBox _startButton;

        // Settings and the in-game menu
        readonly List<Action> _switchRefresh = new List<Action>();
        readonly InkBox[] _sizeButtons = new InkBox[2];
        VisualElement _gmQuitRow;

        // Level-ups wait their turn
        readonly Queue<int> _levelQueue = new Queue<int>();
        VisualElement _levelCardHost;
        VisualElement _gameOverHost;

        /// <summary>Something covers the school (the clock still runs underneath).</summary>
        bool ModalOpen => _menu == MenuScreen.GameMenu || _menu == MenuScreen.Settings || _levelShown || _gameOverShown;

        void BuildScreens()
        {
            _screens = Box("screens");
            _screens.pickingMode = PickingMode.Ignore;
            _root.Add(_screens);
            var sheet = Resources.Load<StyleSheet>("Hud/Screens");
            if (sheet != null && !_root.styleSheets.Contains(sheet)) _root.styleSheets.Add(sheet);
            LoadPageStyles();

            BuildTitle();
            BuildNewSchool();
            BuildLoad();
            BuildAssistantPick();
            BuildSettings();
            BuildGameMenu();
            BuildLevelUp();
            BuildGameOver();
        }

        /// <summary>Shows the right screen for the phase and handles Esc. Returns true when it used the Esc key.</summary>
        bool UpdateMenus(bool playing, bool esc)
        {
            if (!playing)
            {
                if (_menu == MenuScreen.None || _menu == MenuScreen.GameMenu) _menu = MenuScreen.Title;
                if (_settingsReturn == MenuScreen.GameMenu) _settingsReturn = MenuScreen.Title;
                _levelShown = false;
                _gameOverShown = false;
                _levelQueue.Clear();
            }
            else
            {
                if (_menu == MenuScreen.Title || _menu == MenuScreen.NewSchool || _menu == MenuScreen.Assistant || _menu == MenuScreen.Load) _menu = MenuScreen.None;
                if (_sim != null && S.GameOver && !_gameOverShown) ShowGameOver();
                if (!_levelShown && !_gameOverShown && _levelQueue.Count > 0 && _menu == MenuScreen.None) ShowLevelUp(_levelQueue.Dequeue());
            }

            bool used = false;
            if (esc)
            {
                used = true;
                if (_quitAsked) _quitAsked = false;
                else if (_levelShown) _levelShown = false;
                else switch (_menu)
                {
                    case MenuScreen.NewSchool: _menu = MenuScreen.Title; break;
                    case MenuScreen.Assistant: _menu = MenuScreen.NewSchool; break;
                    case MenuScreen.Settings: _menu = _settingsReturn; break;
                    case MenuScreen.GameMenu: _menu = MenuScreen.None; break;
                    case MenuScreen.Load:
                        if (_loadConfirmOpen) _loadConfirmOpen = false;
                        else CloseLoad();
                        break;
                    case MenuScreen.Title: _quitAsked = true; break;
                    default: used = false; break;
                }
            }

            Show(_titleLayer, !playing);
            Show(_newSchoolLayer, _menu == MenuScreen.NewSchool);
            Show(_loadLayer, _menu == MenuScreen.Load);
            Show(_assistantLayer, _menu == MenuScreen.Assistant);
            Show(_settingsLayer, _menu == MenuScreen.Settings);
            Show(_gameMenuLayer, _menu == MenuScreen.GameMenu);
            Show(_levelLayer, _levelShown);
            Show(_gameOverLayer, _gameOverShown);
            Show(_quitNote, _quitAsked && _menu == MenuScreen.Title);
            Show(_gmQuitRow, _quitAsked && _menu == MenuScreen.GameMenu);

            if (_menu == MenuScreen.Title) RefreshTitle();
            if (_menu == MenuScreen.GameMenu)
            {
                _gameMenuDial.Progress = Game.DayProgress;
                RefreshSavedNote();
            }
            UpdateLoad();
            return used;
        }

        static void Show(VisualElement e, bool on) => e?.EnableInClassList("hidden", !on);

        void CloseMenus()
        {
            if (_menu == MenuScreen.GameMenu || _menu == MenuScreen.Settings) _menu = MenuScreen.None;
            _levelShown = false;
            _quitAsked = false;
        }

        void OpenGameMenu()
        {
            _quitAsked = false;
            _menu = MenuScreen.GameMenu;
        }

        void OpenSettings(bool fromGame)
        {
            _settingsReturn = fromGame ? MenuScreen.None : MenuScreen.Title;
            if (fromGame && _menu == MenuScreen.GameMenu) _settingsReturn = MenuScreen.GameMenu;
            foreach (var refresh in _switchRefresh) refresh();
            RefreshSizeButtons();
            _menu = MenuScreen.Settings;
        }

        void LeaveToTitle()
        {
            CloseSheet();
            _levelShown = false;
            _quitAsked = false;
            _menu = MenuScreen.Title;
            Game.ShowTitle();
        }

        // --- Building blocks ------------------------------------------------------------------

        static VisualElement Box(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        static InkBox Ink(params string[] classes)
        {
            var e = new InkBox();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        static InkIcon Icon(string name, params string[] classes)
        {
            var i = new InkIcon(name);
            foreach (var c in classes) i.AddToClassList(c);
            return i;
        }

        static Tape TapeStrip(params string[] classes)
        {
            var t = new Tape();
            t.AddToClassList("tape");
            foreach (var c in classes) t.AddToClassList(c);
            return t;
        }

        VisualElement Layer(params string[] classes)
        {
            var layer = Box("layer", "hidden");
            foreach (var c in classes) layer.AddToClassList(c);
            _screens.Add(layer);
            return layer;
        }

        /// <summary>A notebook button: secondary by default, the big blue one when <paramref name="primary"/>.</summary>
        static InkBox Button(string icon, string label, Action onClick, bool primary = false, bool small = false)
        {
            var b = Ink("btn");
            if (primary) b.AddToClassList("btn--primary");
            if (small) b.AddToClassList("btn--small");
            if (icon != null) b.Add(Icon(icon, small ? "icon-18" : "icon-22", primary ? "icon--white" : "icon--blue"));
            b.Add(Text(label, primary ? "font-hand" : "font-heavy", "btn-label"));
            OnClick(b, onClick);
            return b;
        }

        static InkBox MenuButton(string icon, string label, Action onClick, Color badge, params string[] classes)
        {
            var b = Ink("menu-btn");
            foreach (var c in classes) b.AddToClassList(c);
            var circle = Ink("menu-badge");
            circle.SetFill(badge);
            circle.Add(Icon(icon, "icon-24"));
            b.Add(circle);
            var text = Box();
            text.Add(Text(label, "font-hand", "mbtn-label"));
            b.Add(text);
            OnClick(b, onClick);
            return b;
        }

        InkBox Switch(Func<bool> get, Action<bool> set)
        {
            var sw = Ink("switch");
            var knob = Ink("switch-knob");
            var check = Icon("check", "icon-17", "icon--blue");
            knob.Add(check);
            sw.Add(knob);
            void Refresh()
            {
                bool on = get();
                sw.EnableInClassList("switch--on", on);
                check.EnableInClassList("hidden", !on);
            }
            OnClick(sw, () =>
            {
                set(!get());
                Refresh();
            });
            Refresh();
            _switchRefresh.Add(Refresh);
            return sw;
        }

        /// <summary>The big dotted notebook sheet with its tape, title row and close button.</summary>
        VisualElement Sheet(VisualElement layer, string sizeClass, string icon, string title, Action onClose, out VisualElement body)
        {
            var sheet = Ink("sheet", sizeClass);
            var tape = TapeStrip("sheet-tape");
            tape.style.left = new Length(46f, LengthUnit.Percent);
            sheet.Add(tape);
            var head = Box("sheet-top");
            var left = Box("row");
            var badge = Ink("head-badge");
            badge.Add(Icon(icon, "icon-32"));
            left.Add(badge);
            left.Add(Text(title, "font-hand", "head-title"));
            head.Add(left);
            var close = Ink("x-btn");
            close.Add(Icon("close", "icon-18"));
            OnClick(close, onClose);
            head.Add(close);
            sheet.Add(head);
            body = sheet;
            layer.Add(sheet);
            return sheet;
        }

        // --- Title ----------------------------------------------------------------------------

        void BuildTitle()
        {
            _titleLayer = Layer();
            _titleLayer.RemoveFromClassList("hidden");
            var backdrop = new TitleBackdrop();
            backdrop.AddToClassList("fill");
            _titleLayer.Add(backdrop);

            var logo = Box("title-logo");
            logo.pickingMode = PickingMode.Ignore;
            logo.Add(Text("Back To", "font-hand", "logo-word", "logo-word--lead"));
            logo.Add(Text("School", "font-hand", "logo-word"));
            logo.Add(Text("Inc.", "font-hand", "logo-word", "logo-word--inc"));
            _titleLayer.Add(logo);

            var mascot = Ink("mascot");
            mascot.Add(Icon("crest", "mascot-icon"));
            _titleLayer.Add(mascot);

            var tagline = Ink("tagline");
            tagline.Add(TapeStrip("tagline-tape"));
            tagline.Add(Text("a boarding school sim", "font-note", "tagline-text"));
            _titleLayer.Add(tagline);

            var menu = Box("title-menu");
            _continueButton = MenuButton("play", "Continue", () => Game.Continue(), InkIcons.Marker, "menu-btn--primary", "tilt-a");
            _continueSub = Text("", "font-note", "menu-sub", "continue-sub");
            _continueButton.ElementAt(1).Add(_continueSub);
            _continueButton.ElementAt(1).AddToClassList("continue-text");
            var shot = Ink("continue-shot");
            shot.pickingMode = PickingMode.Ignore;
            _continueMini = new MiniCampus();
            _continueMini.AddToClassList("continue-mini");
            shot.Add(_continueMini);
            _continueButton.Add(shot);
            menu.Add(_continueButton);
            menu.Add(MenuButton("sprout", "New school", OpenNewSchool, InkIcons.Pale, "tilt-b"));
            menu.Add(MenuButton("folder", "Load", OpenLoad, InkIcons.Hex(0xFFF3C4), "menu-btn--b", "tilt-c"));
            menu.Add(MenuButton("cog", "Settings", () => OpenSettings(fromGame: false), InkIcons.Hex(0xC9E1F9), "tilt-d"));
            menu.Add(MenuButton("door", "Quit", () => _quitAsked = true, InkIcons.Blush, "menu-btn--b", "tilt-e"));
            _titleLayer.Add(menu);

            _quitNote = Ink("quit-note", "hidden");
            _quitNote.Add(TapeStrip("quit-tape"));
            var row = Box("row");
            row.Add(Icon("crest", "icon-34"));
            row.Add(Text("Leave already?", "font-hand", "quit-title"));
            _quitNote.Add(row);
            var buttons = Box("quit-row");
            var stay = Button(null, "Stay", () => _quitAsked = false, primary: true, small: true);
            stay.style.flexGrow = 1f;
            buttons.Add(stay);
            var quit = Ink("quit-yes");
            quit.Add(Text("Quit", "font-hand"));
            OnClick(quit, Application.Quit);
            buttons.Add(quit);
            _quitNote.Add(buttons);
            _titleLayer.Add(_quitNote);
        }

        void RefreshTitle()
        {
            bool canContinue = Game != null && Game.CanContinue;
            Show(_continueButton, canContinue);
            if (!canContinue) return;

            // The school in memory, or else the last one saved.
            var sim = Game.Sim;
            bool live = sim != null && !sim.State.GameOver;
            var saved = live ? null : SaveSlots.Latest()?.Summary;
            string name = live ? sim.State.SchoolName : saved?.SchoolName;
            int day = live ? sim.State.Day : saved?.Day ?? 0;
            // Shorten the name ("Little Oak School" to "Little Oak") only when it doesn't fit beside the photo.
            string when = Calendar.FormatShort(day), full = $"{name} · {when}";
            float room = _continueSub.parent.contentRect.width;
            bool fits = float.IsNaN(room) || room < 1f
                || _continueSub.MeasureTextSize(full, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x <= room;
            SetText(_continueSub, fits ? full : $"{ShortSchoolName(name)} · {when}");
            var key = (live ? (object)sim : saved, day);
            if (!key.Equals(_continueKey))
            {
                _continueKey = key;
                _continueMini.Show(live ? SaveSummary.Of(sim, DateTime.UtcNow) : saved);
            }
        }

        /// <summary>"Little Oak School" → "Little Oak", for tight spots.</summary>
        static string ShortSchoolName(string name)
        {
            name = (name ?? "").Trim();
            int space = name.LastIndexOf(' ');
            if (space <= 0) return name;
            switch (name.Substring(space + 1).ToLowerInvariant())
            {
                case "school": case "academy": case "college": case "institute": case "prep": case "high": return name.Substring(0, space);
                default: return name;
            }
        }

        // --- New school -----------------------------------------------------------------------

        void OpenNewSchool()
        {
            _quitAsked = false;
            _draftSeed = Environment.TickCount ^ UnityEngine.Random.Range(1, int.MaxValue);
            if (_draftSeed == 0) _draftSeed = 1;
            _candidates = null;
            _candidate = -1;
            _nameField.SetValueWithoutNotify(_draftName);
            RefreshNewSchool();
            RefreshReplaceNote();
            _menu = MenuScreen.NewSchool;
        }

        void BuildNewSchool()
        {
            _newSchoolLayer = Layer("dim", "center");
            Sheet(_newSchoolLayer, "ns-sheet", "sprout", "New school", () => _menu = MenuScreen.Title, out var sheet);

            var body = Box("ns-body");
            var left = Box("ns-left");
            _crestPreview = new CrestView();
            _crestPreview.AddToClassList("ns-crest");
            left.Add(_crestPreview);
            _crestName = Text("", "font-hand", "ns-name");
            left.Add(_crestName);
            body.Add(left);

            var right = Box("ns-right");
            var nameGroup = Box("ns-group");
            nameGroup.Add(Text("Name", "font-heavy", "field-label"));
            var nameRow = Box("name-row");
            _nameField = new TextField { maxLength = 32 };
            _nameField.AddToClassList("name-field");
            _nameField.RegisterValueChangedCallback(e =>
            {
                _draftName = e.newValue;
                RefreshNewSchool();
            });
            nameRow.Add(_nameField);
            var dice = Ink("dice-btn");
            dice.Add(Icon("dice", "icon-32"));
            OnClick(dice, () =>
            {
                _draftName = Names.School(new SimRandom(UnityEngine.Random.Range(1, int.MaxValue)));
                _nameField.SetValueWithoutNotify(_draftName);
                RefreshNewSchool();
            });
            nameRow.Add(dice);
            nameGroup.Add(nameRow);
            right.Add(nameGroup);

            var emblemGroup = Box("ns-group");
            emblemGroup.Add(Text("Emblem", "font-heavy", "field-label"));
            var emblems = Box("choice-row");
            for (int i = 0; i < Emblems.Length; i++)
            {
                int index = i;
                var b = Ink("choice");
                b.Add(Icon(Emblems[i], "icon-32"));
                OnClick(b, () =>
                {
                    _draftEmblem = index;
                    RefreshNewSchool();
                });
                emblems.Add(b);
                _emblemButtons.Add(b);
            }
            emblemGroup.Add(emblems);
            right.Add(emblemGroup);

            var colourGroup = Box("ns-group");
            colourGroup.Add(Text("Colour", "font-heavy", "field-label"));
            var swatches = Box("choice-row");
            for (int i = 0; i < CrestColors.Length; i++)
            {
                int index = i;
                var sw = Ink("swatch");
                sw.SetFill(CrestColors[i]);
                OnClick(sw, () =>
                {
                    _draftColor = index;
                    RefreshNewSchool();
                });
                swatches.Add(sw);
                _swatches.Add(sw);
            }
            colourGroup.Add(swatches);
            right.Add(colourGroup);

            right.Add(Text("you start with", "font-note", "start-note"));
            var cards = Box("row");
            cards.Add(StartCard("coin", "$90k", "cash", "start-card--a", "tape-sky"));
            cards.Add(StartCard("students", Balance.StartingStudents.ToString(), "students", "start-card--b", "tape-yellow"));
            cards.Add(StartCard("bed", "12", "beds", "start-card--a", "tape-sky"));
            cards.Add(StartCard("board", "2", "classrooms", "start-card--b", "tape-yellow"));
            cards.Add(StartCard("staff", "4", "staff", "start-card--a", "tape-sky"));
            right.Add(cards);
            body.Add(right);
            sheet.Add(body);

            var foot = Box("sheet-foot");
            foot.Add(Button("back", "Back", () => _menu = MenuScreen.Title));
            foot.Add(BuildReplaceNote());
            foot.Add(Button("next", "Next", OpenAssistantPick, primary: true));
            sheet.Add(foot);
        }

        static VisualElement StartCard(string icon, string value, string label, string tilt, string tape)
        {
            var card = Ink("start-card", tilt);
            card.Add(TapeStrip("start-tape", tape));
            card.Add(Icon(icon, "icon-32"));
            card.Add(Text(value, "font-hand", "start-value"));
            card.Add(Text(label, "font-heavy", "start-label"));
            return card;
        }

        void RefreshNewSchool()
        {
            string name = string.IsNullOrWhiteSpace(_draftName) ? "Little Oak School" : _draftName.Trim();
            _crestPreview.Show(CrestColors[_draftColor], Emblems[_draftEmblem], name);
            SetHandText(_crestName, name);
            for (int i = 0; i < _emblemButtons.Count; i++) _emblemButtons[i].EnableInClassList("choice--on", i == _draftEmblem);
            for (int i = 0; i < _swatches.Count; i++) _swatches[i].EnableInClassList("swatch--on", i == _draftColor);
        }

        // --- Assistant pick ----------------------------------------------------------------------

        void OpenAssistantPick()
        {
            if (_candidates == null)
            {
                _candidates = AssistantRules.Candidates(_draftSeed);
                _candidate = -1;
                RebuildCandidates();
            }
            RefreshCandidates();
            _menu = MenuScreen.Assistant;
        }

        void BuildAssistantPick()
        {
            _assistantLayer = Layer("dim", "center");
            Sheet(_assistantLayer, "ap-sheet", "speech", "Pick your assistant", () => _menu = MenuScreen.NewSchool, out var sheet);
            sheet.Add(Text("they'll show you around and bring you problems", "font-note", "ap-sub"));
            _candidateRow = Box("ap-cards");
            sheet.Add(_candidateRow);

            var foot = Box("sheet-foot");
            var tour = Box("tour-toggle");
            tour.Add(Switch(() => _tutorialOn, v => _tutorialOn = v));
            tour.Add(Text("show me around (tutorial)", "font-note", "tour-label"));
            foot.Add(tour);
            var buttons = Box("row");
            buttons.Add(Button("back", "Back", () => _menu = MenuScreen.NewSchool));
            buttons.Add(new VisualElement { style = { width = 14 } });
            _startButton = Button("play", "Start", StartSchool, primary: true);
            buttons.Add(_startButton);
            foot.Add(buttons);
            sheet.Add(foot);
        }

        void RebuildCandidates()
        {
            _candidateRow.Clear();
            _candidateCards.Clear();
            for (int i = 0; i < _candidates.Count; i++)
            {
                var a = _candidates[i];
                int index = i;
                var card = Ink("ap-card", i % 2 == 0 ? "ap-card--a" : "ap-card--b");
                card.Add(TapeStrip("ap-tape", i % 2 == 0 ? "tape-sky" : "tape-yellow"));
                card.Add(Text("hired!", "font-heavy", "ap-hired"));

                var face = Ink("ap-face");
                var portrait = new Portrait();
                portrait.AddToClassList("ap-portrait");
                portrait.Show(a.Style, a.Look);
                face.Add(portrait);
                card.Add(face);

                card.Add(Text(AssistantRules.Word(a.Style), "font-hand", "ap-style"));
                card.Add(Text(a.Name, "font-semi", "ap-name"));
                var stars = Box("ap-stars");
                for (int s = 0; s < 5; s++) stars.Add(Icon("star", "ap-star", s < a.Skill ? "ap-star--on" : "ap-star--off"));
                card.Add(stars);
                card.Add(Text($"\"{a.Quote}\"", "font-note", "ap-quote"));

                var perk = Box("ap-line");
                perk.Add(Icon("plus", "ap-line-icon", "icon--blue"));
                perk.Add(Text(AssistantRules.Perk(a.Style), "font-semi", "ap-line-text"));
                card.Add(perk);
                var quirk = Box("ap-line");
                quirk.Add(Icon("minus", "ap-line-icon", "icon--soft"));
                quirk.Add(Text(AssistantRules.Quirk(a.Style), "font-semi", "ap-line-text"));
                card.Add(quirk);
                card.Add(new VisualElement { style = { flexGrow = 1 } });
                card.Add(Text($"{Money(a.Salary)}/mo", "font-heavy", "ap-pay"));

                OnClick(card, () =>
                {
                    _candidate = index;
                    RefreshCandidates();
                });
                _candidateRow.Add(card);
                _candidateCards.Add(card);
            }
        }

        void RefreshCandidates()
        {
            for (int i = 0; i < _candidateCards.Count; i++) _candidateCards[i].EnableInClassList("ap-card--on", i == _candidate);
            _startButton.EnableInClassList("btn--disabled", _candidate < 0);
        }

        void StartSchool()
        {
            if (_candidates == null || _candidate < 0 || _candidate >= _candidates.Count) return;
            var setup = new NewGameSetup
            {
                SchoolName = string.IsNullOrWhiteSpace(_draftName) ? "Little Oak School" : _draftName.Trim(),
                Seed = _draftSeed,
                Emblem = _draftEmblem,
                CrestColor = _draftColor,
                Assistant = _candidates[_candidate],
                Tutorial = _tutorialOn,
            };
            _menu = MenuScreen.None;
            _candidates = null;
            Game.StartNewGame(setup);
        }

        // --- Settings -------------------------------------------------------------------------

        void BuildSettings()
        {
            InkIcons.Define("speaker", 1.9f, "fill:#FBFDFF M4 9.5h4l5-4v13l-5-4H4z", "M16 9.2a4 4 0 0 1 0 5.6M18.6 6.6a7.6 7.6 0 0 1 0 10.8");
            _settingsLayer = Layer("dim", "center");
            Sheet(_settingsLayer, "set-sheet", "cog", "Settings", () => _menu = _settingsReturn, out var sheet);
            var grid = Box("set-grid");

            var music = SettingTile(grid, "music", "Music", "set-icon--warm");
            music.Add(VolumeControl(() => GameSettings.Music, v => GameSettings.Music = v));
            var sounds = SettingTile(grid, "speaker", "Sounds", "set-icon--warm");
            sounds.Add(VolumeControl(() => GameSettings.Sound, v => GameSettings.Sound = v));

            var full = SettingTile(grid, "screen", "Full screen");
            full.Add(Switch(() => GameSettings.Fullscreen, v => GameSettings.Fullscreen = v));

            var size = SettingTile(grid, "letters", "Size");
            var sizes = Box("row");
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                var b = Ink("size-btn");
                var a = Text("A", "font-hand");
                a.style.fontSize = i == 0 ? 17 : 27;
                b.Add(a);
                OnClick(b, () =>
                {
                    GameSettings.UiSize = index;
                    ApplyUiSize();
                    RefreshSizeButtons();
                });
                sizes.Add(b);
                _sizeButtons[i] = b;
            }
            sizes.Add(Text("small fits more", "font-note", "set-note"));
            size.Add(sizes);

            var tips = SettingTile(grid, "speech", "Tips");
            var tipRow = Box("row");
            tipRow.Add(Switch(() => GameSettings.Tips, v => GameSettings.Tips = v));
            tipRow.Add(Text("your assistant pops up with news", "font-note", "set-note"));
            tips.Add(tipRow);

            var clock = SettingTile(grid, "sun", "Clock");
            var clockRow = Box("row");
            var dial = new DayDial();
            dial.style.width = 40;
            dial.style.height = 40;
            dial.Progress = 0.3f;
            clockRow.Add(dial);
            clockRow.Add(Text($"a day every {GameController.SecondsPerDay:0} seconds. no pause, no fast-forward", "font-note", "set-note"));
            clock.Add(clockRow);

            sheet.Add(grid);
            var credits = BuildCredits();
            sheet.Add(credits);
            var foot = Box("sheet-foot");
            var link = Box("link", "set-credits-link");
            link.Add(Icon("heart", "icon-18", "link-icon"));
            link.Add(Text($"credits · v{Application.version}", "font-note", "link-label"));
            OnClick(link, () => Show(credits, credits.ClassListContains("hidden")));
            foot.Add(link);
            foot.Add(Button("check", "Done", () => _menu = _settingsReturn, primary: true));
            sheet.Add(foot);
        }

        /// <summary>Who made what: the fonts and their licence, shown over the settings on request.</summary>
        VisualElement BuildCredits()
        {
            var card = Ink("set-credits", "hidden");
            card.Add(TapeStrip("set-credits-tape", "tape-yellow"));
            card.Add(Text("Credits", "font-hand", "set-credits-title"));
            card.Add(Text($"{Application.productName} · version {Application.version}", "font-heavy", "set-credits-line"));
            card.Add(Text($"by {Application.companyName}", "font-semi", "set-credits-line"));
            card.Add(Text("Music and sounds", "font-hand", "set-credits-head"));
            card.Add(Text("Written for the game, played on GeneralUser GS by S. Christian Collins", "font-semi", "set-credits-line"));
            card.Add(Text("Fonts", "font-hand", "set-credits-head"));
            card.Add(Text("Gochi Hand by Juan Pablo del Peral", "font-semi", "set-credits-line"));
            card.Add(Text("Playpen Sans by the Playpen Sans Project Authors", "font-semi", "set-credits-line"));
            card.Add(Text("Caveat by the Caveat Project Authors", "font-semi", "set-credits-line"));
            card.Add(Text("All under the SIL Open Font License 1.1 (see the Licenses folder next to the game).", "font-note", "set-credits-note"));
            card.Add(Text("Made with Unity.", "font-note", "set-credits-note"));
            OnClick(card, () => Show(card, false));
            return card;
        }

        /// <summary>Ten bars between a minus and a plus, as in the design; clicking a bar jumps to it.</summary>
        VisualElement VolumeControl(Func<float> get, Action<float> set)
        {
            var row = Box("row", "vol-row");
            var bars = new List<VisualElement>();
            void Refresh()
            {
                int level = Mathf.RoundToInt(get() * 10f);
                for (int i = 0; i < bars.Count; i++) bars[i].EnableInClassList("vol-bar--on", i < level);
            }

            var down = Ink("round-btn", "vol-btn");
            down.Add(Icon("minus", "icon-18"));
            OnClick(down, () =>
            {
                set(Mathf.Clamp01(Mathf.Round(get() * 10f - 1f) / 10f));
                Refresh();
            });
            row.Add(down);
            var column = Box("vol-bars");
            for (int i = 0; i < 10; i++)
            {
                var bar = Box("vol-bar");
                bar.style.height = 12 + i * 2;   // they grow left to right, like a volume wedge
                int level = i + 1;
                OnClick(bar, () =>
                {
                    set(level / 10f);
                    Refresh();
                });
                column.Add(bar);
                bars.Add(bar);
            }
            row.Add(column);
            var up = Ink("round-btn", "vol-btn");
            up.Add(Icon("plus", "icon-18"));
            OnClick(up, () =>
            {
                set(Mathf.Clamp01(Mathf.Round(get() * 10f + 1f) / 10f));
                Refresh();
            });
            row.Add(up);
            Refresh();
            _switchRefresh.Add(Refresh);
            return row;
        }

        static VisualElement SettingTile(VisualElement grid, string icon, string title, string badgeClass = null)
        {
            var tile = Ink("set-tile");
            var badge = Ink("set-icon");
            if (badgeClass != null) badge.AddToClassList(badgeClass);
            badge.Add(Icon(icon, "icon-34"));
            tile.Add(badge);
            var column = Box("grow");
            column.Add(Text(title, "font-hand", "set-title"));
            tile.Add(column);
            grid.Add(tile);
            return column;
        }

        void RefreshSizeButtons()
        {
            for (int i = 0; i < _sizeButtons.Length; i++) _sizeButtons[i]?.EnableInClassList("size-btn--on", GameSettings.UiSize == i);
        }

        // --- In-game menu ---------------------------------------------------------------------

        DayDial _gameMenuDial;

        void BuildGameMenu()
        {
            _gameMenuLayer = Layer("dim--light", "center");
            var card = Ink("gm-card");
            card.Add(Text("Menu", "font-hand", "gm-title"));
            var live = Box("gm-live");
            _gameMenuDial = new DayDial();
            _gameMenuDial.AddToClassList("gm-dial");
            live.Add(_gameMenuDial);
            var notes = Box("grow");
            notes.Add(Text("the school keeps running while you're here", "font-note", "gm-note"));
            _gmSaved = Text("", "font-note", "gm-saved");
            notes.Add(_gmSaved);
            live.Add(notes);
            card.Add(live);
            card.Add(MenuButton("play", "Back to school", () => _menu = MenuScreen.None, InkIcons.Marker, "menu-btn--primary", "tilt-a"));
            card.Add(MenuButton("cog", "Settings", () => OpenSettings(fromGame: true), InkIcons.Hex(0xC9E1F9), "tilt-b"));
            card.Add(MenuButton("door", "Main menu", LeaveToTitle, InkIcons.Pale, "menu-btn--b", "tilt-d"));
            card.Add(MenuButton("close", "Quit game", () => _quitAsked = true, InkIcons.Blush, "tilt-c"));
            _gmQuitRow = Box("quit-row", "hidden");
            var stay = Button(null, "Stay", () => _quitAsked = false, primary: true, small: true);
            stay.style.flexGrow = 1f;
            _gmQuitRow.Add(stay);
            var quit = Ink("quit-yes");
            quit.Add(Text("Quit", "font-hand"));
            OnClick(quit, Application.Quit);
            _gmQuitRow.Add(quit);
            card.Add(_gmQuitRow);
            _gameMenuLayer.Add(card);
        }

        // --- Level up -------------------------------------------------------------------------

        void BuildLevelUp()
        {
            _levelLayer = Layer("dim", "center");
            var confetti = new InkPicture(Illustrations.Confetti, cover: true);
            confetti.AddToClassList("fill");
            _levelLayer.Add(confetti);
            _levelCardHost = Box("center");
            _levelLayer.Add(_levelCardHost);
        }

        void OnLeveledUp(int level) => _levelQueue.Enqueue(level);

        void ShowLevelUp(int level)
        {
            _levelCardHost.Clear();
            var card = Ink("sheet", "lu-card");
            var burst = new Starburst();
            burst.AddToClassList("lu-burst");
            burst.Number.text = level.ToString();
            card.Add(burst);
            card.Add(Text($"School level {level}!", "font-hand", "lu-title"));
            card.Add(Text($"{S.SchoolName} is growing up", "font-note", "lu-sub"));

            var grid = Box("lu-grid");
            int shown = 0;
            RoomDefinition firstRoom = null;
            foreach (var r in ProgressionSystem.RevealsAt(level))
                if (shown < 4) { grid.Add(UnlockItem(RevealIcon(r), RevealTitle(r), ProgressionSystem.DescribeReveal(r))); shown++; }
            foreach (Feature f in Enum.GetValues(typeof(Feature)))
                if (Balance.UnlockLevel(f) == level && shown < 4) { grid.Add(UnlockItem(FeatureIcon(f), FeatureTitle(f), ProgressionSystem.DescribeFeature(f))); shown++; }
            var rooms = new List<RoomDefinition>(ProgressionSystem.RoomsUnlockedAt(level));
            foreach (var def in rooms)
            {
                firstRoom ??= def;
                if (shown >= 4) break;
                grid.Add(UnlockItem("house", def.Name, $"{Money(def.Cost[0])} · {def.Width} x {def.Height} tiles"));
                shown++;
            }
            if (shown == 0) grid.Add(UnlockItem("star", "More prestige", "Bigger schools attract better staff and students."));
            card.Add(grid);

            var next = Ink("lu-next");
            next.Add(Icon("lock", "icon-20", "icon--soft"));
            string unlock = NextUnlock(level);
            next.Add(Text(string.IsNullOrEmpty(unlock) ? $"Next: level {level + 1}" : $"Next: {unlock}", "font-heavy", "lu-next-text"));
            var bar = Box("bar", "lu-next-bar");
            var fill = Box("bar-fill");
            int from = Balance.SchoolXpForLevel(level), to = Balance.SchoolXpForLevel(level + 1);
            SetPercent(fill, to > from ? 100f * (S.Progress.SchoolXp - from) / (to - from) : 0f);
            bar.Add(fill);
            next.Add(bar);
            next.Add(Text($"{Math.Max(0, to - S.Progress.SchoolXp)} XP to go", "font-semi", "lu-next-xp"));
            card.Add(next);

            var buttons = Box("row");
            if (firstRoom != null)
            {
                var type = firstRoom.Type;
                buttons.Add(Button("pencil", $"Build a {firstRoom.Name}", () =>
                {
                    _levelShown = false;
                    CloseSheet();
                    CampusController.Instance?.BeginBuild(type);
                }));
                buttons.Add(new VisualElement { style = { width = 14 } });
            }
            buttons.Add(Button(null, "Yay!", () => _levelShown = false, primary: true));
            card.Add(buttons);
            _levelCardHost.Add(card);
            _levelShown = true;
        }

        static VisualElement UnlockItem(string icon, string name, string description)
        {
            var item = Ink("lu-item");
            item.Add(Text("new!", "font-heavy", "lu-new"));
            var badge = Ink("lu-icon");
            badge.Add(Icon(icon, "icon-32"));
            item.Add(badge);
            var title = Text(name, "font-hand", "lu-name");
            SetHandText(title, name);
            item.Add(title);
            item.Add(Text(WithoutLead(description), "font-note", "lu-desc"));
            return item;
        }

        /// <summary>"School rules: tuition, food…" → "Tuition, food…" (the card already shows the name).</summary>
        static string WithoutLead(string description)
        {
            int colon = description.IndexOf(": ", StringComparison.Ordinal);
            if (colon <= 0 || colon > 40 || colon + 2 >= description.Length) return description;
            string rest = description.Substring(colon + 2);
            return char.ToUpperInvariant(rest[0]) + rest.Substring(1);
        }

        static string RevealIcon(Reveal r) => r switch
        {
            Reveal.Rules => "sliders",
            Reveal.Stats => "chart",
            Reveal.Land => "house",
            Reveal.Admissions => "envelope",
            Reveal.News => "news",
            Reveal.Rankings => "podium",
            _ => "star",
        };

        static string RevealTitle(Reveal r) => r switch
        {
            Reveal.Rules => "School rules",
            Reveal.Stats => "Statistics",
            Reveal.Land => "Land",
            Reveal.Admissions => "Admissions",
            Reveal.News => "News",
            Reveal.Rankings => "Rankings",
            _ => r.ToString(),
        };

        static string FeatureIcon(Feature f) => f switch
        {
            Feature.Clubs => "trophy",
            Feature.Administrators => "staff",
            Feature.Scholarships => "book",
            Feature.AdvancedDorms => "bed",
            Feature.Specializations => "crest",
            Feature.SportsPrograms => "trophy",
            Feature.InternationalStudents => "people",
            Feature.Endowment => "coin",
            Feature.SecondCampus => "house",
            Feature.EliteInstitution => "podium",
            _ => "star",
        };

        static string FeatureTitle(Feature f) => f switch
        {
            Feature.Clubs => "Clubs",
            Feature.Administrators => "Directors",
            Feature.Scholarships => "Scholarships",
            Feature.AdvancedDorms => "Dorms+",
            Feature.Specializations => "Identity",
            Feature.SportsPrograms => "Sports teams",
            Feature.MajorSponsorships => "Big sponsors",
            Feature.AdvancedDepartments => "Departments",
            Feature.LargeCompetitions => "Nationals",
            Feature.InternationalStudents => "From abroad",
            Feature.Endowment => "Endowment",
            Feature.SecondCampus => "Old estate",
            Feature.EliteInstitution => "Elite",
            _ => f.ToString(),
        };

        // --- Game over ------------------------------------------------------------------------

        void BuildGameOver()
        {
            _gameOverLayer = Layer("dim", "center");
            _gameOverHost = Box("center");
            _gameOverLayer.Add(_gameOverHost);
        }

        /// <summary>The end of a school: its crest stamped "closed", how long it ran and what it achieved.</summary>
        void BuildGameOverCard()
        {
            _gameOverHost.Clear();
            var card = Ink("sheet", "go-card");
            var tape = TapeStrip("sheet-tape", "tape-sky");
            tape.style.left = new Length(44f, LengthUnit.Percent);
            card.Add(tape);

            var top = Box("go-top");
            var crestBox = Box("go-crest-box");
            var crest = new CrestView();
            crest.AddToClassList("go-crest");
            crest.Show(CrestColors[Mathf.Clamp(S.CrestColor, 0, CrestColors.Length - 1)], Emblems[Mathf.Clamp(S.Emblem, 0, Emblems.Length - 1)], S.SchoolName);
            crestBox.Add(crest);
            var stamp = Ink("go-stamp");
            stamp.pickingMode = PickingMode.Ignore;
            stamp.Add(Text("closed", "font-hand", "go-stamp-text"));
            crestBox.Add(stamp);
            top.Add(crestBox);

            var titles = Box("go-titles");
            titles.Add(Text("School closed", "font-hand", "go-title"));
            var name = Text("", "font-hand", "go-name");
            SetHandText(name, S.SchoolName);
            titles.Add(name);
            titles.Add(Text($"{Calendar.Format(0)} to {Calendar.Format(S.Day)} · {RanFor(S.Day)}", "font-note", "go-dates"));
            top.Add(titles);
            card.Add(top);

            var reason = Ink("go-reason");
            reason.Add(Icon("alert", "icon-20", "go-reason-icon"));
            reason.Add(Text(S.GameOverReason, "font-semi", "go-reason-text"));
            card.Add(reason);

            // What the school achieved while it lasted.
            int bestRank = S.NationalRank, mostStudents = S.Students.Count;
            float bestPrestige = S.Prestige;
            foreach (var h in S.History)
            {
                if (h.Rank > 0 && (bestRank <= 0 || h.Rank < bestRank)) bestRank = h.Rank;
                mostStudents = Mathf.Max(mostStudents, h.Students);
                bestPrestige = Mathf.Max(bestPrestige, h.Prestige);
            }
            var stats = Box("go-stats");
            stats.Add(GameOverStat("podium", bestRank > 0 ? "#" + bestRank : "–", "best rank"));
            stats.Add(GameOverStat("students", mostStudents.ToString(), "most students"));
            stats.Add(GameOverStat("book", S.Alumni.Count.ToString(), S.Alumni.Count == 1 ? "graduate" : "graduates"));
            stats.Add(GameOverStat("star", Mathf.RoundToInt(bestPrestige).ToString(), "top prestige"));
            card.Add(stats);
            card.Add(Text("it stays on the Load screen as a keepsake", "font-note", "go-note"));

            var buttons = Box("row", "go-buttons");
            buttons.Add(Button("door", "Main menu", LeaveToTitle));
            buttons.Add(new VisualElement { style = { width = 14 } });
            buttons.Add(Button("sprout", "New school", () =>
            {
                LeaveToTitle();
                OpenNewSchool();
            }, primary: true));
            card.Add(buttons);
            _gameOverHost.Add(card);
        }

        static VisualElement GameOverStat(string icon, string value, string label)
        {
            var tile = Ink("go-stat");
            tile.Add(Icon(icon, "icon-24"));
            tile.Add(Text(value, "font-hand", "go-stat-value"));
            tile.Add(Text(label, "font-heavy", "go-stat-label"));
            return tile;
        }

        /// <summary>"2 years, 4 months", "5 months" or "12 days".</summary>
        static string RanFor(int days)
        {
            int years = days / Calendar.DaysPerYear, months = days % Calendar.DaysPerYear / Calendar.DaysPerMonth;
            string y = years == 1 ? "1 year" : $"{years} years", m = months == 1 ? "1 month" : $"{months} months";
            if (years > 0) return months > 0 ? $"{y}, {m}" : y;
            return months > 0 ? m : days == 1 ? "1 day" : $"{days} days";
        }

        // --- Hooks for the automated screenshot tour -------------------------------------------

        internal void TourNewSchool(string name, int emblem, int color)
        {
            OpenNewSchool();
            _draftName = name;
            _draftEmblem = emblem;
            _draftColor = color;
            _nameField.SetValueWithoutNotify(name);
            RefreshNewSchool();
        }

        internal void TourAssistant(int pick)
        {
            OpenAssistantPick();
            _candidate = pick;
            RefreshCandidates();
        }

        internal void TourStart(bool tutorial)
        {
            _tutorialOn = tutorial;
            StartSchool();
        }

        internal void TourGameMenu(bool open)
        {
            if (open) OpenGameMenu();
            else if (_menu == MenuScreen.GameMenu) _menu = MenuScreen.None;
        }

        internal void TourSettings(bool open)
        {
            if (open) OpenSettings(fromGame: true);
            else _menu = MenuScreen.None;
        }

        internal void TourLevelUp(int level) => ShowLevelUp(level);

        internal void TourClearPopups()
        {
            _levelQueue.Clear();
            _levelShown = false;
            if (_menu == MenuScreen.GameMenu || _menu == MenuScreen.Settings) _menu = MenuScreen.None;
        }

        internal void TourBubble(string text) => AssistantSays(text, "Open", null, 30f);

        void ShowGameOver()
        {
            BuildGameOverCard();
            _gameOverShown = true;
            _levelShown = false;
            if (_menu == MenuScreen.GameMenu) _menu = MenuScreen.None;
        }
    }
}
