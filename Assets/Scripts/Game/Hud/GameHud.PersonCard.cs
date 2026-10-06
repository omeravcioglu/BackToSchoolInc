using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The index card of the student or staff member clicked on the campus: a polaroid of their
    /// face, what they're up to, a few chips, four mood bars, and buttons to open their file or
    /// follow them around with the camera. What they're thinking floats over their head on the map.
    /// </summary>
    public sealed partial class GameHud
    {
        InkBox _pcCard, _pcThought, _pcFollowBtn;
        PersonPortrait _pcPortrait;
        Label _pcCaption, _pcName, _pcDoing, _pcThoughtText, _pcFollowLabel;
        VisualElement _pcChips;
        readonly Label[] _pcMeterNames = new Label[4], _pcMeterValues = new Label[4];
        readonly VisualElement[] _pcMeterFills = new VisualElement[4];
        long _pcShown = -1, _pcFollowing = -1;
        string _pcChipsSignature;
        float _pcNextThought;

        void BuildPersonCard()
        {
            _pcCard = Ink("card", "pc-card", "hidden");
            _pcCard.Add(TapeStrip("pc-tape", "tape-sky"));

            var polaroid = Ink("pc-polaroid");
            var photo = Box("pc-photo");
            _pcPortrait = new PersonPortrait();
            _pcPortrait.AddToClassList("pc-portrait");
            photo.Add(_pcPortrait);
            polaroid.Add(photo);
            _pcCaption = Text("", "font-note", "pc-caption");
            polaroid.Add(_pcCaption);
            _pcCard.Add(polaroid);

            var body = Box("pc-body");
            _pcName = Text("", "font-hand", "pc-name");
            body.Add(_pcName);
            _pcDoing = Text("", "font-note", "pc-doing");
            body.Add(_pcDoing);
            _pcChips = Box("pc-chips");
            body.Add(_pcChips);

            var meters = Box("pc-meters");
            for (int i = 0; i < 4; i++)
            {
                var meter = Box("pc-meter");
                var head = Box("pc-meter-head");
                _pcMeterNames[i] = Text("", "font-heavy", "pc-meter-name");
                _pcMeterValues[i] = Text("", "font-heavy", "pc-meter-value");
                head.Add(_pcMeterNames[i]);
                head.Add(_pcMeterValues[i]);
                meter.Add(head);
                var bar = Box("bar", "pc-bar");
                _pcMeterFills[i] = Box("bar-fill");
                bar.Add(_pcMeterFills[i]);
                meter.Add(bar);
                meters.Add(meter);
            }
            body.Add(meters);

            var buttons = Box("pc-buttons");
            var open = Button("folder", "Open file", OpenPersonFile, primary: true, small: true);
            open.AddToClassList("pc-open");
            buttons.Add(open);
            _pcFollowBtn = Button("eye", "Follow", ToggleFollow, small: true);
            _pcFollowBtn.AddToClassList("pc-follow");
            _pcFollowLabel = _pcFollowBtn.Q<Label>();
            buttons.Add(_pcFollowBtn);
            body.Add(buttons);
            _pcCard.Add(body);

            var close = Ink("round-btn", "close-btn", "pc-close");
            close.Add(Icon("close", "icon-18"));
            OnClick(close, () => CampusController.Instance?.SelectPerson(-1));
            _pcCard.Add(close);

            // Their thought, a bubble that follows them around on the map.
            _pcThought = Ink("pc-thought", "hidden");
            _pcThought.pickingMode = PickingMode.Ignore;
            _pcThoughtText = Text("", "font-note", "pc-thought-text");
            _pcThought.Add(_pcThoughtText);
            _pcThought.Add(Ink("pc-thought-dot", "pc-thought-dot--big"));
            _pcThought.Add(Ink("pc-thought-dot", "pc-thought-dot--small"));

            int dock = _dock != null ? _hud.IndexOf(_dock) : -1;
            if (dock < 0) dock = _hud.childCount;
            _hud.Insert(dock, _pcThought);
            _hud.Insert(dock + 1, _pcCard);   // both under the assistant's speech bubble
        }

        void UpdatePersonCard(bool covered)
        {
            if (_pcCard == null) return;
            var ctl = CampusController.Instance;
            var people = CampusPeople.Instance;
            long key = !covered && ctl != null && ctl.Tool == CampusTool.Inspect ? ctl.SelectedPerson : -1;
            bool staff = CampusPeople.IsStaff(key);
            Student student = null;
            StaffMember member = null;
            if (key >= 0)
            {
                if (staff) member = S.FindStaff(CampusPeople.IdOf(key));
                else student = S.FindStudent(CampusPeople.IdOf(key));
                if (student == null && member == null) key = -1;
            }

            // The camera stops following once they're deselected or someone else is picked.
            var cam = CampusCamera.Instance;
            if (_pcFollowing >= 0 && (_pcFollowing != ctl?.SelectedPerson || cam == null || !cam.Following))
            {
                if (cam != null && cam.Following && _pcFollowing != ctl?.SelectedPerson) cam.StopFollowing();
                _pcFollowing = -1;
            }

            Show(_pcCard, key >= 0);
            if (key < 0)
            {
                _pcShown = -1;
                Show(_pcThought, false);
                return;
            }
            if (key != _pcShown)
            {
                _pcShown = key;
                _pcChipsSignature = null;
                _pcNextThought = 0f;
                _sim.TutorialSignal("select:person");
            }

            if (student != null) FillStudentCard(student);
            else FillStaffCard(member);
            SetText(_pcDoing, people != null ? people.Doing(key) : "");

            if (GameClock.Now >= _pcNextThought)
            {
                _pcNextThought = GameClock.Now + 6f;
                SetText(_pcThoughtText, student != null ? StudentThought(student) : StaffThought(member));
            }

            bool following = _pcFollowing == key;
            _pcFollowBtn.EnableInClassList("pc-follow--on", following);
            SetText(_pcFollowLabel, following ? "Following" : "Follow");

            // Sit above the assistant's speech bubble when it's showing.
            float lift = 0f;
            var bubble = _bubble;
            if (bubble != null && !bubble.ClassListContains("hidden") && !bubble.ClassListContains("bubble--out") && _hud.layout.height > 0f)
                lift = Mathf.Max(0f, _hud.layout.height - (bubble.worldBound.yMin - _hud.worldBound.yMin) + 14f - 116f);
            _pcCard.style.bottom = 116f + lift;

            PlaceThought(people, key);
        }

        /// <summary>Hangs the thought bubble over the person's head, its little dots pointing down at them.</summary>
        void PlaceThought(CampusPeople people, long key)
        {
            var camera = Camera.main;
            var panel = _root.panel;
            Vector2 at = default;
            bool show = people != null && camera != null && panel != null && _pcThoughtText.text.Length > 0
                        && people.TryGetPosition(key, out at);
            if (show)
            {
                var screen = camera.WorldToScreenPoint(new Vector3(at.x, at.y + 1.12f, 0f));
                var local = _hud.WorldToLocal(RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y)));
                float w = _hud.layout.width, h = _hud.layout.height;
                show = local.x > 80f && local.x < w - 60f && local.y > 170f && local.y < h;
                if (show)
                {
                    _pcThought.style.left = local.x - 14f;
                    _pcThought.style.bottom = h - local.y + 30f;
                }
            }
            Show(_pcThought, show);
        }

        void FillStudentCard(Student s)
        {
            _pcPortrait.ShowStudent(s);
            var dorm = s.DormRoomId >= 0 ? S.FindRoom(s.DormRoomId) : null;
            SetText(_pcCaption, dorm != null ? dorm.Name : "no bed!");
            _pcCaption.EnableInClassList("pc-caption--bad", dorm == null);
            SetHandText(_pcName, s.FullName);

            string signature = s.Grade + "|" + string.Join(",", s.Traits);
            if (signature != _pcChipsSignature)
            {
                _pcChipsSignature = signature;
                _pcChips.Clear();
                _pcChips.Add(Text("Grade " + s.Grade, "font-heavy", "pc-chip", "pc-chip--dark"));
                AddTraitChips(s.Traits.ConvertAll(t => PcWords(t.ToString())));
            }

            PcMeter(0, "Happiness", s.Happiness, false);
            PcMeter(1, "Stress", s.Stress, true);
            PcMeter(2, "Grades", s.Grades, false);
            PcMeter(3, "Family mood", s.ParentMood, false);
        }

        void FillStaffCard(StaffMember m)
        {
            _pcPortrait.ShowStaff(m);
            SetText(_pcCaption, m.Title);
            _pcCaption.EnableInClassList("pc-caption--bad", false);
            SetHandText(_pcName, m.Name);

            string signature = m.Role + "|" + string.Join(",", m.Traits);
            if (signature != _pcChipsSignature)
            {
                _pcChipsSignature = signature;
                _pcChips.Clear();
                _pcChips.Add(Text(PcWords(m.Role.ToString()), "font-heavy", "pc-chip", "pc-chip--dark"));
                AddTraitChips(m.Traits.ConvertAll(t => PcWords(t.ToString())));
            }

            PcMeter(0, "Morale", m.Morale, false);
            PcMeter(1, "Energy", m.Energy, false);
            PcMeter(2, "Skill", m.Skill, false);
            PcMeter(3, "Loyalty", m.Loyalty, false);
        }

        void AddTraitChips(List<string> traits)
        {
            for (int i = 0; i < traits.Count && i < 2; i++) _pcChips.Add(Text(traits[i], "font-semi", "pc-chip"));
            if (traits.Count > 2)
            {
                var more = Ink("pc-chip-more");
                more.Add(Text("+" + (traits.Count - 2), "font-semi", "pc-chip-more-label"));
                _pcChips.Add(more);
            }
        }

        /// <summary>A bar of the card: red when it's going badly (high, for stress).</summary>
        void PcMeter(int i, string name, float value, bool highIsBad)
        {
            bool bad = highIsBad ? value > 65f : value < 40f;
            SetText(_pcMeterNames[i], name);
            SetText(_pcMeterValues[i], Mathf.RoundToInt(value).ToString());
            _pcMeterNames[i].parent.EnableInClassList("pc-meter-head--bad", bad);
            SetPercent(_pcMeterFills[i], value);
            _pcMeterFills[i].EnableInClassList("bar-fill--low", bad);
            _pcMeterFills[i].EnableInClassList("pc-fill--calm", highIsBad && !bad);
        }

        void OpenPersonFile()
        {
            long key = _pcShown;
            if (key < 0) return;
            int id = CampusPeople.IdOf(key);
            if (CampusPeople.IsStaff(key))
            {
                _staffFocus = id;
                ShowPageAt(HudPage.Staff);
            }
            else
            {
                _studentFocus = id;
                ShowPageAt(HudPage.Students);
            }
        }

        internal void TourOpenPersonFile() => OpenPersonFile();

        void ToggleFollow()
        {
            var cam = CampusCamera.Instance;
            var people = CampusPeople.Instance;
            if (cam == null || people == null || _pcShown < 0) return;
            if (_pcFollowing == _pcShown)
            {
                cam.StopFollowing();
                _pcFollowing = -1;
                return;
            }
            long key = _pcShown;
            _pcFollowing = key;
            cam.Follow(() =>
            {
                if (people.Selected != key || !people.TryGetPosition(key, out var pos)) return null;
                return pos;
            }, 6f);
        }

        /// <summary>"CleanFreak" to "Clean freak".</summary>
        static string PcWords(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c))
                {
                    sb.Append(' ');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>What's on their mind: the biggest thing pushing their happiness up or down.</summary>
        static string StudentThought(Student s)
        {
            Factor best = default;
            float bestSize = 1f;
            foreach (var f in s.HappinessFactors.Factors)
            {
                if (f.Label == "Base") continue;
                float size = Mathf.Abs(f.Value);
                if (size > bestSize)
                {
                    bestSize = size;
                    best = f;
                }
            }
            if (best.Label == null) return s.Happiness >= 60f ? "life is pretty good!" : "just another day...";
            bool good = best.Value > 0f;
            switch (best.Label)
            {
                case "Food vs expectations": return good ? "lunch was sooo good today" : "is this... meatloaf?";
                case "No bed assigned": return "where do I even sleep?";
                case "Dorm room vs expectations": return good ? "I love my room!" : "my room is kinda sad";
                case "Bathroom queues": return "the bathroom line, again...";
                case "Clean freak in a dirty room": return "this room is SO dirty";
                case "Roommates": return good ? "my roommates are the best" : "my roommates are... a lot";
                case "Living with an enemy": return "I can't stand my roommate";
                case "Introvert: small room": return "nice and quiet in here";
                case "Introvert: crowded room": return "too many people in one room...";
                case "No laundry access": return "running out of clean socks";
                case "Friends": return "hanging out with my friends!";
                case "No friends": return "wish I had someone to sit with";
                case "Enemies": return "ugh, THEM again";
                case "Rivals": return "I'll beat them next time";
                case "Has a crush": return "they smiled at me!!";
                case "Student life": return good ? "so much to do here!" : "there's nothing fun to do";
                case "Teachers' personalities": return good ? "my teachers are so nice" : "my teacher is so grumpy";
                case "Stress": return "too much homework...";
                case "Strict curfew": return "lights out already?";
                case "Relaxed curfew": return "one more chapter tonight!";
                case "Strict discipline": return "can't do anything here";
                case "Lenient discipline": return "nobody minds a bit of fun";
                case "Athlete: the sports complex": return "that sports complex though!";
                case "On a school team": return "go team!";
                case "Somewhere to play": return "race you to the slide!";
                case "Anxious: exams": return "exams... I'm not ready";
                case "Club member": return "club day is the best day";
                case "Artist: no arts club": return "I wish we had an art club";
                case "Injured": return "ouch, my ankle...";
                case "Sick": return "*sniff* I feel awful";
                case "Scholarship": return "so lucky to be here";
            }
            return good ? "I like it here!" : best.Label.ToLowerInvariant() + "... hmm";
        }

        static string StaffThought(StaffMember m)
        {
            if (m.Energy < 30f) return "need... coffee...";
            if (m.Morale < 35f) return "is this job worth it?";
            if (m.Morale > 75f) return "I love working here";
            return m.Role switch
            {
                StaffRole.Teacher => "who ate my red pen?",
                StaffRole.Coach => "hustle, team!",
                StaffRole.Custodian => "who spilled juice here?",
                StaffRole.Maintenance => "that pipe again...",
                StaffRole.Chef => "soup of the day: soup",
                StaffRole.Nurse => "drink water, everyone!",
                StaffRole.Security => "all quiet on campus",
                StaffRole.Counselor => "my door is always open",
                _ => "so much paperwork...",
            };
        }
    }
}
