using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The money page's tuition planner: −/+ steppers and a draggable rail with the fair price
    /// marked, the estimated consequences of the new price, and the button that sets it for the
    /// start of next semester. Also the stepper and rail the rules and administration pages share.
    /// </summary>
    public sealed partial class GameHud
    {
        float _monDraft = -1f;          // the tuition being lined up
        bool _monDraftEdited;           // false: the draft follows the planned (or current) price
        MonTrack _monTrack;
        InkBox _monApply;
        VisualElement _monKeepChip, _monParentChip, _monApplicantChip;
        Label _monTag, _monStarts, _monPrice, _monDelta, _monLow, _monFair, _monHigh;
        Label _monKeepNow, _monKeepThen, _monParents, _monApplicants, _monStatus, _monApplyLabel;

        VisualElement MonBuildTuition()
        {
            var card = Ink("mon-card", "mon-tuition");
            var head = Box("mon-head");
            var title = Box("row");
            title.Add(Text("Tuition", "font-hand", "mon-title"));
            _monTag = Text("", "font-heavy", "mon-tag");
            title.Add(_monTag);
            head.Add(title);
            _monStarts = Text("", "font-note", "mon-head-note");
            head.Add(_monStarts);
            card.Add(head);

            var row = Box("mon-price-row");
            row.Add(MonStepper("minus", () => MonSetDraft(_monDraft - 500f), true));
            var price = Box("mon-price-box");
            _monPrice = Text("", "font-hand", "mon-price");
            price.Add(_monPrice);
            price.Add(Text("a year", "font-semi", "mon-per"));
            row.Add(price);
            row.Add(MonStepper("plus", () => MonSetDraft(_monDraft + 500f), true, "mon-step--b"));
            _monDelta = Text("", "font-heavy", "mon-delta");
            row.Add(_monDelta);
            card.Add(row);

            _monTrack = new MonTrack(100f) { Changed = MonSetDraft };
            card.Add(_monTrack.Root);
            var scale = Box("mon-scale");
            _monLow = Text("", "font-semi", "mon-scale-low");
            _monFair = Text("", "font-semi", "mon-scale-fair");
            _monHigh = Text("", "font-semi", "mon-scale-high");
            scale.Add(_monLow);
            scale.Add(_monFair);
            scale.Add(_monHigh);
            card.Add(scale);

            var chips = Box("mon-chips");
            _monKeepChip = Box("chip");
            _monKeepChip.Add(Icon("students", "chip-icon"));
            _monKeepNow = Text("", "font-heavy");
            _monKeepChip.Add(_monKeepNow);
            _monKeepChip.Add(Icon("next", "mon-chip-arrow"));
            _monKeepThen = Text("", "font-heavy");
            _monKeepChip.Add(_monKeepThen);
            chips.Add(_monKeepChip);
            _monParentChip = MonChip("people", out _monParents);
            chips.Add(_monParentChip);
            _monApplicantChip = MonChip("letters", out _monApplicants);
            chips.Add(_monApplicantChip);
            card.Add(chips);

            var foot = Box("mon-tuition-foot");
            _monStatus = Text("", "font-semi", "mon-status");
            foot.Add(_monStatus);
            _monApply = Button("check", "", MonApplyTuition, primary: true, small: true);
            _monApplyLabel = _monApply.Q<Label>(className: "btn-label");
            foot.Add(_monApply);
            card.Add(foot);
            return card;
        }

        static VisualElement MonChip(string icon, out Label text)
        {
            var chip = Box("chip");
            chip.Add(Icon(icon, "chip-icon"));
            text = Text("", "font-heavy");
            chip.Add(text);
            return chip;
        }

        void MonSetDraft(float value)
        {
            _monDraft = Mathf.Clamp(Mathf.Round(value / 100f) * 100f, 1000f, 80000f);
            _monDraftEdited = true;
            MonShowTuition();
        }

        void MonShowTuition()
        {
            var pol = S.Policies;
            if (!_monDraftEdited) _monDraft = pol.PendingTuition > 0f ? pol.PendingTuition : pol.AnnualTuition;
            float draft = _monDraft, fair = _sim.FairTuition;
            // New prices start with the next semester: February, or September after the spring.
            int month = Calendar.Month(S.Day) <= Calendar.FirstSemesterEndMonth ? Calendar.FirstSemesterEndMonth + 1 : 1;
            string when = Calendar.MonthName(month);

            SetText(_monPrice, Money(draft));
            bool pricey = draft > fair * 1.15f, aboveFair = draft > fair;
            SetText(_monTag, pricey ? "pricey" : aboveFair ? "about fair" : "good value");
            _monTag.EnableInClassList("mon-tag--fair", aboveFair && !pricey);
            _monTag.EnableInClassList("mon-tag--pricey", pricey);
            SetText(_monStarts, "new price starts in " + when);

            var est = _sim.Economy.EstimateTuition(draft);
            float change = est.MonthlyRevenueChange;
            bool flat = Mathf.Abs(change) < 1f;
            SetText(_monDelta, flat ? "no change" : Signed(change) + "/mo");
            _monDelta.EnableInClassList("mon-good", !flat && change > 0f);
            _monDelta.EnableInClassList("mon-bad", !flat && change < 0f);

            float lo = Mathf.Round(fair * 0.5f / 100f) * 100f, hi = Mathf.Round(fair * 1.8f / 100f) * 100f;
            _monTrack.SetRange(lo, hi);
            _monTrack.SetValue(draft);
            _monTrack.SetMark(fair);
            SetText(_monLow, MoneyShort(lo));
            SetText(_monHigh, MoneyShort(hi));
            SetText(_monFair, "fair ≈ " + Money(Mathf.Round(fair / 100f) * 100f));
            _monFair.style.left = new Length(_monTrack.Percent(fair), LengthUnit.Percent);

            SetText(_monKeepNow, $"re-enrol {Mathf.RoundToInt(est.CurrentRetention * 100f)}%");
            SetText(_monKeepThen, $"{Mathf.RoundToInt(est.NewRetention * 100f)}%");
            MonTone(_monKeepChip, est.NewRetention - est.CurrentRetention, 0.005f);
            SetText(_monParents, Mathf.Abs(est.ParentReaction) < 0.05f ? "parents don't mind" : "parent mood " + MonSigned(est.ParentReaction, "0.0"));
            MonTone(_monParentChip, est.ParentReaction, 0.05f);
            SetText(_monApplicants, Mathf.Abs(est.ApplicantChange) < 0.005f ? "applications as usual" : "applications " + MonSigned(est.ApplicantChange * 100f, "0") + "%");
            MonTone(_monApplicantChip, est.ApplicantChange, 0.005f);

            bool pending = pol.PendingTuition > 0f;
            SetText(_monStatus, $"now {Money(pol.AnnualTuition)} a year, billed monthly" + (pending ? $" · {Money(pol.PendingTuition)} from {when}" : ""));
            bool same = Mathf.Abs(draft - pol.AnnualTuition) < 1f, planned = pending && Mathf.Abs(draft - pol.PendingTuition) < 1f;
            SetText(_monApplyLabel, planned ? "Planned for " + when : same ? (pending ? "Keep today's price" : "No change") : "Apply in " + when);
            _monApply.EnableInClassList("btn--disabled", planned || (same && !pending));
        }

        /// <summary>Sets the draft for next semester (or, back at today's price, cancels a planned change).</summary>
        void MonApplyTuition()
        {
            var pol = S.Policies;
            bool pending = pol.PendingTuition > 0f;
            bool same = Mathf.Abs(_monDraft - pol.AnnualTuition) < 1f, planned = pending && Mathf.Abs(_monDraft - pol.PendingTuition) < 1f;
            if (planned || (same && !pending)) return;
            float value = _monDraft;
            _monDraftEdited = false;
            RunCommand(() => _sim.SetTuition(value));
        }

        static void MonTone(VisualElement chip, float change, float flat)
        {
            chip.EnableInClassList("chip--good", change > flat);
            chip.EnableInClassList("chip--bad", change < -flat);
        }

        static string MonSigned(float v, string format) => (v < 0f ? "–" : "+") + Mathf.Abs(v).ToString(format);

        // --- Shared with the rules and administration pages ---------------------------------------

        /// <summary>
        /// A vertical scroll view for a whole page: its content keeps its natural height (scrolling
        /// when the page is short) and is at least as tall as the view, so cards can grow to fill it.
        /// </summary>
        static ScrollView MonPageScroll(string className)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList(className);
            scroll.contentContainer.style.flexShrink = 0f;
            scroll.contentContainer.style.minHeight = new Length(100f, LengthUnit.Percent);
            return scroll;
        }

        /// <summary>A round −/+ button; with <paramref name="repeat"/> it keeps stepping while held down.</summary>
        static InkBox MonStepper(string icon, Action step, bool repeat, params string[] classes)
        {
            var b = Ink("mon-step");
            foreach (var c in classes) b.AddToClassList(c);
            b.Add(Icon(icon, "icon-17"));
            b.AddManipulator(repeat ? new Clickable(step, 400, 90) : new Clickable(step));
            return b;
        }

        /// <summary>
        /// A slider rail drawn like the rest of the notebook: a yellow knob the player drags or clicks
        /// to, and an optional mark for a reference value such as the fair tuition.
        /// </summary>
        sealed class MonTrack
        {
            public readonly VisualElement Root;
            public Action<float> Changed, Released;

            readonly VisualElement _mark;
            readonly InkBox _knob;
            readonly float _step;
            float _min, _max = 1f, _value;

            public MonTrack(float step)
            {
                _step = step;
                Root = Box("mon-track");
                var rail = Box("mon-track-rail");
                _mark = Box("mon-track-mark", "hidden");
                _knob = Ink("mon-track-knob");
                foreach (var e in new[] { rail, _mark, _knob })
                {
                    e.pickingMode = PickingMode.Ignore;
                    Root.Add(e);
                }
                Root.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    Root.CapturePointer(e.pointerId);
                    Drag(e.position);
                    e.StopPropagation();
                });
                Root.RegisterCallback<PointerMoveEvent>(e =>
                {
                    if (Root.HasPointerCapture(e.pointerId)) Drag(e.position);
                });
                Root.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (!Root.HasPointerCapture(e.pointerId)) return;
                    Root.ReleasePointer(e.pointerId);
                    Released?.Invoke(_value);
                });
            }

            public void SetRange(float min, float max)
            {
                _min = min;
                _max = Mathf.Max(min + _step, max);
            }

            public float Percent(float v) => 100f * Mathf.Clamp01((v - _min) / (_max - _min));

            public void SetValue(float v)
            {
                _value = v;
                _knob.style.left = new Length(Percent(v), LengthUnit.Percent);
            }

            public void SetMark(float v)
            {
                Show(_mark, true);
                _mark.style.left = new Length(Percent(v), LengthUnit.Percent);
            }

            void Drag(Vector2 world)
            {
                float w = Root.layout.width;
                if (w < 1f || float.IsNaN(w)) return;
                float t = Mathf.Clamp01(Root.WorldToLocal(world).x / w);
                float v = Mathf.Round((_min + t * (_max - _min)) / _step) * _step;
                if (Mathf.Approximately(v, _value)) return;
                SetValue(v);
                Changed?.Invoke(v);
            }
        }
    }
}
