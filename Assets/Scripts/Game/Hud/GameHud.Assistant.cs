using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The assistant on screen: their portrait in the corner, a speech bubble for the tutorial and
    /// for problems, news and new features, an arrow that points at what the tutorial is talking
    /// about, and "new!" stickers on tabs that just appeared.
    /// </summary>
    public sealed partial class GameHud
    {
        VisualElement _dock;
        InkBox _dockFace, _bubble, _bubbleButton;
        Portrait _dockPortrait;
        Label _dockName, _bubbleWho, _bubbleText, _bubbleStep, _bubbleSkip;
        InkIcon _tutArrow;
        VisualElement _tutTarget;
        Action _bubbleAction;
        float _bubbleUntil = -1f, _arrowFlip;
        bool _bubbleOpen, _bubbleIsTutorial, _arrowFar;
        int _tutorialShown = -1;
        readonly Dictionary<VisualElement, (Label tag, float until)> _newTags = new Dictionary<VisualElement, (Label, float)>();

        void BuildDock()
        {
            _dock = _root.Q("dock");
            _tutArrow = _root.Q<InkIcon>("tutArrow");

            _dockFace = Ink("dock-face");
            _dockPortrait = new Portrait();
            _dockPortrait.style.flexGrow = 1f;
            _dockFace.Add(_dockPortrait);
            _dockName = Text("", "font-note", "dock-name");
            _dockFace.Add(_dockName);
            OnClick(_dockFace, OnFaceClicked);

            _bubble = Ink("bubble", "bubble--out", "hidden");
            _bubbleWho = Text("", "font-note", "bubble-who");
            _bubbleText = Text("", "bubble-text");
            var row = Box("bubble-row");
            _bubbleButton = Button(null, "Next", OnBubbleButton, primary: true, small: true);
            _bubbleSkip = Text("skip tutorial", "font-note", "bubble-link");
            OnClick(_bubbleSkip, SkipTutorial);
            _bubbleStep = Text("", "font-heavy", "bubble-step");
            row.Add(_bubbleButton);
            row.Add(_bubbleSkip);
            row.Add(_bubbleStep);
            _bubble.Add(_bubbleWho);
            _bubble.Add(_bubbleText);
            _bubble.Add(row);

            _dock.Add(_dockFace);
            _dock.Add(_bubble);
        }

        void ResetAssistant()
        {
            SetTarget(null);
            HideBubble();
            foreach (var t in _newTags.Values) t.tag.RemoveFromHierarchy();
            _newTags.Clear();
            _tutorialShown = -1;
        }

        void UpdateAssistant()
        {
            var a = S.Assistant;
            // Over a page the assistant steps aside, unless the tutorial is talking.
            Show(_dock, a != null && (!PageOpen || _bubbleIsTutorial || _sim.Tutorial.Active));
            if (a == null)
            {
                SetTarget(null);
                return;
            }
            _dockPortrait.Show(a.Style, a.Look);
            SetText(_dockName, a.FirstName);

            float now = GameClock.Now;
            foreach (var kv in new List<KeyValuePair<VisualElement, (Label tag, float until)>>(_newTags))
                if (now > kv.Value.until) ClearNewSticker(kv.Key);

            var step = _sim.Tutorial.Current;
            if (step != null)
            {
                if (S.Tutorial.Step != _tutorialShown) ShowTutorialStep(step);
                PointAt(step.Target);
            }
            else
            {
                if (_bubbleIsTutorial) HideBubble();
                _tutorialShown = -1;
                SetTarget(null);
                if (_bubbleOpen && _bubbleUntil > 0f && now > _bubbleUntil) HideBubble();
            }
            if (_bubbleOpen && !_bubbleIsTutorial && PageOpen) HideBubble();
            bool shortFrames = _bubbleOpen && _bubbleIsTutorial && PageOpen;
            _pageHost.EnableInClassList("page--short", shortFrames);
            _dockFace.EnableInClassList("dock-face--talking", _bubbleOpen);
        }

        // --- Tutorial -------------------------------------------------------------------------

        void ShowTutorialStep(TutorialStep step)
        {
            _tutorialShown = S.Tutorial.Step;
            OpenBubble(_sim.Tutorial.Text(step), step.Button, () => _sim.TutorialSignal("next"), tutorial: true);
            SetText(_bubbleStep, $"{_sim.Tutorial.StepNumber}/{TutorialSystem.Steps.Length}");
            Show(_bubbleStep, true);
            Show(_bubbleSkip, step.Id != "done");
        }

        void SkipTutorial()
        {
            if (_sim == null) return;
            var r = _sim.SkipTutorial();
            HideBubble();
            SetTarget(null);
            if (r.Ok) ShowToast("Tutorial skipped. Everything's unlocked; I'll still bring you problems.", "speech", "note");
        }

        VisualElement TargetFor(string key) => key switch
        {
            "tab:inbox" => _tabInbox,
            "tab:build" => _tabBuild,
            "tab:students" => _tabStudents,
            "tab:money" => _tabMoney,
            "tab:staff" => _tabStaff,
            "vitals" => _vitalsCard,
            _ => null,
        };

        /// <summary>The tab's page is already open, so there's nothing left to point at.</summary>
        bool TargetOpen(string key)
        {
            return key switch
            {
                "tab:inbox" => _page == HudPage.Inbox,
                "tab:students" => _page == HudPage.Students,
                "tab:staff" => _page == HudPage.Staff,
                "tab:money" => _page == HudPage.Money,
                "tab:build" => CampusController.Instance != null && CampusController.Instance.Tool == CampusTool.Build,
                _ => false,
            };
        }

        void PointAt(string key)
        {
            var target = TargetFor(key);
            bool usable = target != null && !target.ClassListContains("hidden") && target.resolvedStyle.display != DisplayStyle.None
                          && !(key == "vitals" && _rightColumn.ClassListContains("hidden")) && !TargetOpen(key);
            SetTarget(usable ? target : null);
            if (!usable) return;

            var box = target.worldBound;
            if (float.IsNaN(box.x) || box.width < 1f) return;
            bool leftSide = box.center.x < _hud.layout.width / 2f;
            _tutArrow.EnableInClassList("tut-arrow--left", leftSide);
            _tutArrow.style.left = leftSide ? box.xMax + 8f : box.x - 50f;
            _tutArrow.style.top = box.center.y - 20f;

            if (GameClock.Now >= _arrowFlip)
            {
                _arrowFlip = GameClock.Now + 0.45f;
                _arrowFar = !_arrowFar;
                _tutArrow.EnableInClassList("tut-arrow--far", _arrowFar);
            }
        }

        void SetTarget(VisualElement target)
        {
            if (_tutTarget == target)
            {
                Show(_tutArrow, target != null);
                return;
            }
            _tutTarget?.RemoveFromClassList("tut-target");
            _tutTarget = target;
            _tutTarget?.AddToClassList("tut-target");
            Show(_tutArrow, target != null);
        }

        // --- Speech bubble --------------------------------------------------------------------

        /// <summary>
        /// The assistant says something, with an optional button. Falls back to a toast while the
        /// tutorial is talking or when tips are turned off.
        /// </summary>
        void AssistantSays(string text, string button = null, Action action = null, float seconds = 9f, string icon = "speech", string tone = "note")
        {
            if (_sim == null) return;
            // The bubble is for the campus view; over a page, or while the tutorial talks, it's a toast.
            if (!GameSettings.Tips || S.Assistant == null || _sim.Tutorial.Active || PageOpen)
            {
                ShowToast(text, icon, tone);
                return;
            }
            OpenBubble(text, button, action, tutorial: false);
            GameAudio.Play(Sfx.Chime);
            _bubbleUntil = GameClock.Now + seconds;
        }

        void OpenBubble(string text, string button, Action action, bool tutorial)
        {
            var a = S.Assistant;
            SetText(_bubbleWho, a != null ? $"{a.FirstName}, your assistant" : "");
            SetText(_bubbleText, text);
            _bubbleAction = action;
            _bubbleIsTutorial = tutorial;
            _bubbleUntil = -1f;
            Show(_bubbleButton, !string.IsNullOrEmpty(button));
            if (!string.IsNullOrEmpty(button)) SetText(_bubbleButton.Q<Label>(), button);
            Show(_bubbleSkip, tutorial);
            Show(_bubbleStep, false);
            Show(_bubble, true);
            _bubbleOpen = true;
            _bubble.schedule.Execute(() => _bubble.RemoveFromClassList("bubble--out")).StartingIn(30);
        }

        void HideBubble()
        {
            if (!_bubbleOpen) return;
            _bubbleOpen = false;
            _bubbleIsTutorial = false;
            _bubbleAction = null;
            _bubble.AddToClassList("bubble--out");
            _bubble.schedule.Execute(() =>
            {
                if (!_bubbleOpen) Show(_bubble, false);
            }).StartingIn(260);
        }

        void OnBubbleButton()
        {
            var action = _bubbleAction;
            bool tutorial = _bubbleIsTutorial;
            if (!tutorial) HideBubble();
            action?.Invoke();
        }

        void OnFaceClicked()
        {
            if (_sim == null || S.Assistant == null) return;
            var step = _sim.Tutorial.Current;
            if (step != null)
            {
                ShowTutorialStep(step);
                return;
            }
            if (_bubbleOpen)
            {
                HideBubble();
                return;
            }

            int notes = _sim.Assistants.OpenProblems();
            if (notes > 0)
            {
                AssistantSays(notes == 1 ? "I left you a note in the inbox." : $"I left you {notes} notes in the inbox.", "Open", () => ShowPageAt(HudPage.Inbox));
                return;
            }
            var weakest = VitalDefs[0];
            foreach (var v in VitalDefs)
                if (S.Stat(v.stat) < S.Stat(weakest.stat)) weakest = v;
            float value = S.Stat(weakest.stat);
            if (value < 60f)
                AssistantSays($"Our weakest spot is {weakest.name.ToLowerInvariant()} ({value:0}). Want to see why?", "Show me",
                    () => OpenStats(StatsMetric.ForStat(weakest.stat)));
            else
                AssistantSays("All quiet. Everything's in good shape. Keep it up!");
        }

        // --- News, new features ------------------------------------------------------------------

        void OnNews(NewsStory story)
        {
            if (story == null || !story.AboutUs) return;
            int id = story.Id;
            if (story.Tone == NewsTone.Good)
                AssistantSays($"We're in the papers! \"{story.Headline}\"", "Read", () => OpenNews(id), icon: "news", tone: "gold");
            else if (story.Tone == NewsTone.Bad)
                AssistantSays($"Uh oh, bad press: \"{story.Headline}\"", "Read", () => OpenNews(id), icon: "news", tone: "bad");
            else
                ShowToast("In the news: " + story.Headline, "news", "note");
        }

        void OnRevealed(Reveal r, bool announce)
        {
            // Stickers for what the tutorial brings in and for surprises; skipping the tutorial or a
            // level-up (which has its own card) reveals quietly.
            if (announce || _sim.Tutorial.Active) AddNewSticker(TabFor(r));
            if (!announce) return;
            switch (r)
            {
                case Reveal.News:
                    break;   // the first story announces itself
                case Reveal.Rankings:
                    AssistantSays("The national rankings are out. Let's see where we stand!", "Open", () => OpenPage(HudPage.Ranks), icon: "podium", tone: "gold");
                    break;
                case Reveal.Promises:
                    AssistantSays("You made a promise. I'll keep it on a sticky note so we don't forget.", icon: "check");
                    break;
                case Reveal.Admissions:
                    AssistantSays("Admissions Season is open: families are applying. Pick who gets a place.", "Open", () => ShowPageAt(HudPage.Admissions), icon: "envelope", tone: "gold");
                    break;
                default:
                    ShowToast("New: " + ProgressionSystem.DescribeReveal(r), "star", "gold");
                    break;
            }
        }

        VisualElement TabFor(Reveal r) => r switch
        {
            Reveal.Inbox => _tabInbox,
            Reveal.News => _tabNews,
            Reveal.Students => _tabStudents,
            Reveal.Staff => _tabStaff,
            Reveal.Build => _tabBuild,
            Reveal.Land => _tabBuild,
            Reveal.Money => _tabMoney,
            Reveal.Stats => _tabStats,
            Reveal.Rankings => _tabRanks,
            Reveal.Rules => _tabRules,
            Reveal.Admissions => _tabMore,
            _ => null,
        };

        void AddNewSticker(VisualElement tab)
        {
            if (tab == null || _newTags.ContainsKey(tab)) return;
            var tag = Text("new!", "font-heavy", "nav-new");
            tag.pickingMode = PickingMode.Ignore;
            tab.Add(tag);
            _newTags[tab] = (tag, GameClock.Now + 60f);
        }

        void ClearNewSticker(VisualElement tab)
        {
            if (tab == null || !_newTags.TryGetValue(tab, out var t)) return;
            t.tag.RemoveFromHierarchy();
            _newTags.Remove(tab);
        }
    }
}
