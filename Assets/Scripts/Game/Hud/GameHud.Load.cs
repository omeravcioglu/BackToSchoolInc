using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The Load screen from the design canvas: the three save slots as cards with a photo of each
    /// campus, plus the little "saved" stamp on the clock when the school saves itself.
    /// </summary>
    public sealed partial class GameHud
    {
        VisualElement _loadLayer, _loadCards;
        InkBox _loadDelete, _loadGo, _loadConfirm, _savedStamp;
        Label _loadConfirmTitle, _loadNote, _savedText;
        InkIcon _savedIcon;
        int _loadPick = -1;
        bool _loadConfirmOpen;
        float _savedUntil, _loadClickAt = -1f;

        /// <summary>The design's trash can (the New school sheet uses it too, and is built first).</summary>
        static void DefineLoadIcons() =>
            InkIcons.Define("load-trash", 2.2f, "M4.5 7h15", "M9.5 7V4.5h5V7", "M6.5 7l1 12.5h9l1-12.5", "M10.5 10.5v6M13.5 10.5v6");

        void BuildLoad()
        {
            DefineLoadIcons();
            var sheetStyle = Resources.Load<StyleSheet>("Hud/Load");
            if (sheetStyle != null && !_root.styleSheets.Contains(sheetStyle)) _root.styleSheets.Add(sheetStyle);

            _loadLayer = Layer("dim", "center");
            var sheet = Sheet(_loadLayer, "load-sheet", "folder", "Load", CloseLoad, out _);
            sheet.Q<InkBox>(className: "head-badge")?.SetFill(InkIcons.Hex(0xFFF3C4));
            sheet.Q(className: "sheet-tape")?.AddToClassList("load-tape");

            _loadCards = Box("load-cards");
            sheet.Add(_loadCards);

            var foot = Box("sheet-foot", "load-foot");
            foot.Add(Button("back", "Back", CloseLoad));
            _loadNote = Text("", "font-note", "load-note");
            foot.Add(_loadNote);
            _loadDelete = Ink("load-delete");
            _loadDelete.Add(Icon("load-trash", "icon-20", "load-delete-icon"));
            _loadDelete.Add(Text("Delete", "font-heavy", "load-delete-label"));
            OnClick(_loadDelete, () =>
            {
                if (PickedSlot() != null) _loadConfirmOpen = true;
            });
            foot.Add(_loadDelete);
            _loadGo = Button("play", "Load", LoadPicked, primary: true);
            foot.Add(_loadGo);
            sheet.Add(foot);

            // "Delete it?" as a sticky note, like the title's quit note.
            _loadConfirm = Ink("load-confirm", "hidden");
            _loadConfirm.Add(TapeStrip("quit-tape"));
            var row = Box("row");
            row.Add(Icon("load-trash", "icon-34", "load-delete-icon"));
            _loadConfirmTitle = Text("", "font-hand", "load-confirm-title");
            row.Add(_loadConfirmTitle);
            _loadConfirm.Add(row);
            _loadConfirm.Add(Text("it's gone for good", "font-note", "load-confirm-sub"));
            var buttons = Box("quit-row");
            var keep = Button(null, "Keep", () => _loadConfirmOpen = false, primary: true, small: true);
            keep.style.flexGrow = 1f;
            buttons.Add(keep);
            var yes = Ink("quit-yes");
            yes.Add(Text("Delete", "font-hand"));
            OnClick(yes, DeletePicked);
            buttons.Add(yes);
            _loadConfirm.Add(buttons);
            _loadLayer.Add(_loadConfirm);

            // The clock's "saved" stamp.
            var clock = _root.Q("clock");
            if (clock != null)
            {
                _savedStamp = Ink("saved-stamp", "saved-stamp--off");
                _savedStamp.pickingMode = PickingMode.Ignore;
                _savedIcon = Icon("check", "icon-13");
                _savedStamp.Add(_savedIcon);
                _savedText = Text("saved", "font-heavy", "saved-text");
                _savedStamp.Add(_savedText);
                clock.Add(_savedStamp);
            }
            if (Game != null) Game.Saved += OnSaved;
        }

        void OnSaved(bool ok)
        {
            if (_savedStamp == null) return;
            _savedStamp.EnableInClassList("saved-stamp--bad", !ok);
            _savedIcon.EnableInClassList("hidden", !ok);
            SetText(_savedText, ok ? "saved" : "couldn't save!");
            _savedUntil = Time.realtimeSinceStartup + (ok ? 2.2f : 8f);
        }

        /// <summary>Per frame, from <see cref="UpdateMenus"/>.</summary>
        void UpdateLoad()
        {
            Show(_loadConfirm, _loadConfirmOpen && _menu == MenuScreen.Load);
            _savedStamp?.EnableInClassList("saved-stamp--off", Time.realtimeSinceStartup > _savedUntil);
        }

        void OpenLoad()
        {
            _quitAsked = false;
            _loadConfirmOpen = false;
            SetLoadNote(null);
            SaveSlots.Refresh();
            var latest = SaveSlots.Latest();
            _loadPick = latest?.Index ?? -1;
            if (_loadPick < 0)
                foreach (var s in SaveSlots.All)
                    if (!s.Empty) { _loadPick = s.Index; break; }
            RebuildLoadCards();
            _menu = MenuScreen.Load;
        }

        void CloseLoad()
        {
            _loadConfirmOpen = false;
            _menu = MenuScreen.Title;
        }

        SaveSlot PickedSlot()
        {
            var all = SaveSlots.All;
            return _loadPick >= 0 && _loadPick < all.Length && !all[_loadPick].Empty ? all[_loadPick] : null;
        }

        void RebuildLoadCards()
        {
            _loadCards.Clear();
            var latest = SaveSlots.Latest();
            foreach (var slot in SaveSlots.All) _loadCards.Add(SlotCard(slot, slot == latest));
            RefreshLoadPicks();
        }

        VisualElement SlotCard(SaveSlot slot, bool lastPlayed)
        {
            int index = slot.Index;
            string tilt = index == 0 ? "load-tilt-a" : index == 1 ? "load-tilt-b" : "load-tilt-c";
            if (slot.Empty)
            {
                var empty = Ink("load-empty", tilt);
                var plus = Ink("load-plus");
                plus.Add(Icon("plus", "load-plus-icon"));
                empty.Add(plus);
                empty.Add(Text("empty", "font-note", "load-empty-label"));
                empty.Add(Text("start a new school", "font-heavy", "load-empty-hint"));
                OnClick(empty, OpenNewSchool);
                return empty;
            }

            var card = Ink("load-card", tilt);
            var photo = Ink("load-photo", index % 2 == 0 ? "load-photo--a" : "load-photo--b");
            var mini = new MiniCampus();
            mini.AddToClassList("load-mini");
            photo.Add(mini);
            card.Add(photo);

            if (slot.Damaged)
            {
                card.Add(Text("Can't read this save", "font-hand", "load-name"));
                card.Add(Text("the file is damaged", "font-note", "load-date"));
            }
            else
            {
                var s = slot.Summary;
                mini.Show(s);
                var name = Text("", "font-hand", "load-name");
                SetHandText(name, s.SchoolName);
                card.Add(name);
                card.Add(Text($"Year {Calendar.Year(s.Day)} · {Calendar.MonthName(Calendar.Month(s.Day))} {Calendar.DayOfMonth(s.Day)}", "font-note", "load-date"));
                var stats = Box("load-stats");
                stats.Add(LoadStat("star", Mathf.RoundToInt(s.Prestige).ToString(), "Prestige"));
                stats.Add(LoadStat("students", s.Students.ToString(), "Students"));
                stats.Add(LoadStat("coin", MoneyShort(s.Cash), "Cash"));
                card.Add(stats);
                if (s.GameOver)
                {
                    var stamp = Ink("load-closed");
                    stamp.pickingMode = PickingMode.Ignore;
                    stamp.Add(Text("closed", "font-hand", "load-closed-text"));
                    photo.Add(stamp);
                }
            }
            if (lastPlayed)
            {
                var sticker = Ink("load-sticker");
                sticker.pickingMode = PickingMode.Ignore;
                sticker.Add(Text("last played", "font-heavy", "load-sticker-text"));
                card.Add(sticker);
            }

            OnClick(card, () =>
            {
                // A second click on the picked card loads it.
                float now = Time.realtimeSinceStartup;
                bool twice = _loadPick == index && now - _loadClickAt < 0.45f;
                _loadClickAt = now;
                _loadPick = index;
                RefreshLoadPicks();
                if (twice && slot.Playable) LoadPicked();
            });
            return card;
        }

        static VisualElement LoadStat(string icon, string value, string tooltip)
        {
            var stat = Box("load-stat");
            stat.tooltip = tooltip;
            stat.Add(Icon(icon, "icon-20"));
            stat.Add(Text(value, "font-hand", "load-stat-value"));
            return stat;
        }

        void RefreshLoadPicks()
        {
            int i = 0;
            foreach (var card in _loadCards.Children())
            {
                card.EnableInClassList("load-card--on", i == _loadPick && card.ClassListContains("load-card"));
                i++;
            }
            var picked = PickedSlot();
            _loadGo.EnableInClassList("btn--disabled", picked == null || !picked.Playable);
            _loadDelete.EnableInClassList("load-delete--off", picked == null);
            if (picked != null)
                SetText(_loadConfirmTitle, picked.Damaged ? "Delete this save?" : $"Delete {picked.Summary.SchoolName}?");
        }

        void LoadPicked()
        {
            var picked = PickedSlot();
            if (picked == null || !picked.Playable || Game == null) return;
            _loadConfirmOpen = false;
            if (Game.LoadGame(picked.Index))
            {
                _menu = MenuScreen.None;
                return;
            }
            // Unreadable after all: show it as damaged.
            SaveSlots.Refresh();
            RebuildLoadCards();
            SetLoadNote("that save couldn't be opened");
        }

        void SetLoadNote(string trouble)
        {
            SetText(_loadNote, trouble ?? "schools save themselves every month");
            _loadNote.EnableInClassList("load-note--bad", trouble != null);
        }

        void DeletePicked()
        {
            var picked = PickedSlot();
            _loadConfirmOpen = false;
            if (picked == null) return;
            SaveSlots.Delete(picked.Index);
            Game?.Forget(picked.Index);
            _loadPick = SaveSlots.Latest()?.Index ?? -1;
            RebuildLoadCards();
        }

        // --- New school: which school a new one replaces when every slot is taken ----------------

        InkBox _replaceNote;
        Label _replaceText;

        VisualElement BuildReplaceNote()
        {
            DefineLoadIcons();
            _replaceNote = Ink("ns-replace", "hidden");
            _replaceNote.pickingMode = PickingMode.Ignore;
            _replaceNote.Add(Icon("load-trash", "icon-18", "load-delete-icon"));
            _replaceText = Text("", "font-heavy", "ns-replace-text");
            _replaceNote.Add(_replaceText);
            return _replaceNote;
        }

        void RefreshReplaceNote()
        {
            if (_replaceNote == null) return;
            SaveSlots.Refresh();
            var slot = SaveSlots.All[SaveSlots.SlotForNewSchool()];
            bool replaces = !slot.Empty;
            Show(_replaceNote, replaces);
            if (replaces)
                SetText(_replaceText, slot.Damaged ? "all 3 slots are full: replaces a damaged save" : $"all 3 slots are full: replaces {slot.Summary.SchoolName}");
        }

        // --- In-game menu: when the school last saved itself -------------------------------------

        Label _gmSaved;

        void RefreshSavedNote()
        {
            if (_gmSaved == null || Game == null) return;
            float at = Game.LastSavedAt;
            string text;
            if (at < 0f) text = "saves itself every month";
            else
            {
                int minutes = Mathf.FloorToInt((Time.realtimeSinceStartup - at) / 60f);
                text = minutes < 1 ? "saved just now" : minutes < 60 ? $"saved {minutes} min ago" : $"saved {minutes / 60} h ago";
            }
            SetText(_gmSaved, text);
        }

        // --- Hooks for the automated screenshot tour -------------------------------------------

        internal void TourOpenLoad() => OpenLoad();

        internal void TourLoadPick(int slot)
        {
            _loadPick = slot;
            RefreshLoadPicks();
        }

        internal void TourLoadAskDelete(bool open) => _loadConfirmOpen = open;

        internal void TourLoadGo() => LoadPicked();
    }
}
