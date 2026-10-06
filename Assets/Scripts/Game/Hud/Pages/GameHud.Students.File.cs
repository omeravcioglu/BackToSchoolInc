using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The student file: a notebook spread with who they are on the left (polaroid, traits,
    /// abilities, activities) and how they are right now on the right (meters, then a sticky with
    /// why they feel that way, their family, friends and foes, or their diary), plus moving rooms,
    /// swapping and expelling.
    /// </summary>
    public sealed partial class GameHud
    {
        static readonly string[] StuMeterNames = { "Happiness", "Stress", "Grades", "Behavior", "Health", "Family mood" };
        static readonly string[] StuTabNames = { "Why?", "Family", "Friends", "Diary" };
        static readonly string[] StuAbilityNames = { "Academic", "Discipline", "Artistic", "Athletic", "Social" };

        /// <summary>A labelled 0-100 meter on the right-hand page.</summary>
        sealed class StuMeter
        {
            public Label Name, Note, Value;
            public VisualElement Fill, Target;
        }

        int _stuTab, _stuShownId = -1, _stuExpelArm = -1;
        float _stuExpelUntil;
        bool _stuPicking;

        InkBox _stuSpread, _stuLeft, _stuSheet, _stuMoveBtn, _stuSwapBtn, _stuExpelBtn;
        VisualElement _stuRight, _stuBinding, _stuTraits, _stuStatus;
        ScrollView _stuSheetBody;
        PersonPortrait _stuPhoto;
        StuRadar _stuRadar;
        Label _stuFileEmpty, _stuCaption, _stuName, _stuMeta, _stuParents, _stuAid, _stuFromText, _stuTalent, _stuTeam, _stuClub;
        VisualElement _stuFrom;
        Label _stuMoveLabel, _stuSwapLabel, _stuExpelLabel, _stuExpelNote;
        readonly Label[] _stuAbility = new Label[5];
        readonly Label[] _stuTabs = new Label[4];
        readonly StuMeter[] _stuMeters = new StuMeter[6];

        static void StuDefineIcons()
        {
            InkIcons.Define("stu-search", 2.2f, "fill:#FBFDFF circle:10.5,10.5,6.2", "M15.2 15.2l4.6 4.6");
            InkIcons.Define("stu-swap", 2.2f, "M4 8h14M14 4l4 4-4 4M20 16H6M10 12l-4 4 4 4");
            InkIcons.Define("stu-bolt", 1.9f, "fill:#FFE066 M13.5 2.5L5 13.5h6l-1.5 8L19 10.5h-6z");
            InkIcons.Define("stu-enemy", 1.9f, "fill:#FFE3DA circle:12,12,8.8", "M8 8.6l3 1.4M16 8.6l-3 1.4",
                "M8.6 16.6c1-1.2 2.2-1.8 3.4-1.8s2.4.6 3.4 1.8");
            InkIcons.Define("stu-crush", 1.8f,
                "fill:#FFE3DA M12 20s-7.4-4.3-7.4-9.7c0-2.4 1.8-4.2 4-4.2 1.5 0 2.7.8 3.4 2 .7-1.2 1.9-2 3.4-2 2.2 0 4 1.8 4 4.2 0 5.4-7.4 9.7-7.4 9.7z",
                "fill:#FFE066 M19.5 2.5l.8 1.9 1.9.8-1.9.8-.8 1.9-.8-1.9-1.9-.8 1.9-.8z");
            InkIcons.Define("stu-bandage", 1.8f, "fill:#FFE3DA M4.9 15.3L15.3 4.9a2.9 2.9 0 0 1 4.1 4.1L9 19.4a2.9 2.9 0 0 1-4.1-4.1z",
                "M9.8 9.8l4.4 4.4");
            InkIcons.Define("stu-thermo", 1.8f, "fill:#FBFDFF M10 5a2 2 0 0 1 4 0v8.6a3.8 3.8 0 1 1-4 0z", "ink circle:12,16.6,1.9", "M12 9.5v6");
            InkIcons.Define("stu-globe", 1.8f, "fill:#DCEBFB circle:12,12,8.6", "M3.4 12h17.2",
                "M12 3.4c2.4 2.3 3.6 5.2 3.6 8.6s-1.2 6.3-3.6 8.6c-2.4-2.3-3.6-5.2-3.6-8.6s1.2-6.3 3.6-8.6z");
        }

        void StuBuildFile()
        {
            _stuShownId = -1;
            _stuSpread = Ink("stu-spread");
            _stuFileEmpty = Text("", "font-note", "empty-note", "stu-file-empty");
            _stuSpread.Add(_stuFileEmpty);

            // Left-hand page: who they are.
            _stuLeft = Ink("stu-left");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("stu-left-scroll");
            _stuLeft.Add(scroll);
            var left = Box("stu-left-body");
            scroll.Add(left);

            var head = Box("stu-head");
            var polaroid = Ink("stu-polaroid");
            var photo = Box("stu-photo");
            _stuPhoto = new PersonPortrait();
            _stuPhoto.AddToClassList("stu-photo-img");
            photo.Add(_stuPhoto);
            polaroid.Add(photo);
            _stuCaption = Text("", "font-note", "stu-caption");
            polaroid.Add(_stuCaption);
            head.Add(polaroid);
            var id = Box("stu-id");
            id.Add(Text("student file", "font-note", "stu-kicker"));
            _stuName = Text("", "font-hand", "stu-name");
            id.Add(_stuName);
            _stuMeta = Text("", "font-semi", "stu-meta");
            id.Add(_stuMeta);
            _stuParents = Text("", "font-semi", "stu-meta");
            id.Add(_stuParents);
            // What the family pays, and where from (students from abroad)
            var chips = Box("stu-pay");
            _stuAid = Text("", "font-heavy", "chip", "stu-aid");
            chips.Add(_stuAid);
            _stuFrom = Box("chip", "stu-from");
            _stuFrom.Add(Icon("stu-globe", "chip-icon", "icon--blue"));
            _stuFromText = Text("", "font-heavy");
            _stuFrom.Add(_stuFromText);
            chips.Add(_stuFrom);
            id.Add(chips);
            head.Add(id);
            left.Add(head);

            _stuTraits = Box("stu-traits");
            left.Add(_stuTraits);

            var lower = Box("stu-lower");
            var abilities = Ink("stu-card");
            abilities.Add(Text("Abilities", "font-hand", "stu-card-title"));
            var radarBox = Box("stu-radar-box");
            _stuRadar = new StuRadar();
            _stuRadar.AddToClassList("stu-radar");
            radarBox.Add(_stuRadar);
            for (int i = 0; i < StuAbilityNames.Length; i++)
            {
                var axis = Box("stu-axis", "stu-axis--" + i);
                axis.pickingMode = PickingMode.Ignore;
                axis.Add(Text(StuAbilityNames[i], "font-heavy", "stu-axis-name"));
                _stuAbility[i] = Text("", "font-hand", "stu-axis-value");
                axis.Add(_stuAbility[i]);
                radarBox.Add(axis);
            }
            abilities.Add(radarBox);
            lower.Add(abilities);
            var acts = Box("stu-acts");
            _stuTalent = StuFact(acts, "star", "sport talent");
            _stuTeam = StuFact(acts, "trophy", "team");
            _stuClub = StuFact(acts, "music", "club");
            lower.Add(acts);
            left.Add(lower);
            _stuSpread.Add(_stuLeft);

            // Right-hand page: how they are right now.
            _stuRight = Box("stu-right");
            var ruled = new StuRuled();
            ruled.AddToClassList("stu-ruled");
            _stuRight.Add(ruled);
            var nowHead = Box("stu-now-head");
            nowHead.Add(Text("Right now", "font-hand", "stu-now-title"));
            _stuStatus = Box("stu-status");
            nowHead.Add(_stuStatus);
            _stuRight.Add(nowHead);

            var meters = Box("stu-meters");
            for (int i = 0; i < _stuMeters.Length; i++)
            {
                var m = new StuMeter();
                var cell = Box("stu-meter");
                var top = Box("stu-meter-top");
                m.Name = Text(StuMeterNames[i], "font-heavy", "stu-meter-name");
                m.Note = Text("", "font-note", "stu-meter-note");
                m.Value = Text("", "font-hand", "stu-meter-value");
                top.Add(m.Name);
                top.Add(m.Note);
                top.Add(m.Value);
                cell.Add(top);
                var track = Box();
                var bar = Box("stu-meter-bar");
                m.Fill = Box("bar-fill");
                bar.Add(m.Fill);
                track.Add(bar);
                if (i == 0)
                {
                    // Where happiness is heading; it sits on the track so the bar's clipping can't cut it.
                    m.Target = Box("stu-meter-target");
                    track.Add(m.Target);
                }
                cell.Add(track);
                meters.Add(cell);
                _stuMeters[i] = m;
            }
            _stuMeters[1].Fill.AddToClassList("stu-fill--calm");
            _stuRight.Add(meters);

            var tabs = Box("stu-tabs");
            for (int i = 0; i < StuTabNames.Length; i++)
            {
                int index = i;
                var tab = Text(StuTabNames[i], "font-heavy", "pill-btn", "stu-tab");
                OnClick(tab, () => StuSetTab(index));
                tabs.Add(tab);
                _stuTabs[i] = tab;
            }
            _stuRight.Add(tabs);

            _stuSheet = Ink("stu-sheet");
            _stuSheetBody = new ScrollView(ScrollViewMode.Vertical);
            _stuSheetBody.AddToClassList("stu-sheet-body");
            _stuSheet.Add(_stuSheetBody);
            _stuSheet.Add(TapeStrip("stu-sheet-tape", "tape-yellow"));
            _stuRight.Add(_stuSheet);

            var actions = Box("stu-actions");
            _stuMoveBtn = StuAction(actions, "bed", "Move room", StuToggleMove, "stu-act--blue");
            _stuMoveLabel = _stuMoveBtn.Q<Label>();
            _stuSwapBtn = StuAction(actions, "stu-swap", "Swap…", StuToggleSwap, null);
            _stuSwapLabel = _stuSwapBtn.Q<Label>();
            _stuExpelBtn = StuAction(actions, "door", "Expel…", StuExpelClick, "stu-act--danger");
            _stuExpelLabel = _stuExpelBtn.Q<Label>();
            _stuExpelNote = Text("", "font-note", "stu-act-note");
            _stuExpelBtn.Add(_stuExpelNote);
            _stuRight.Add(actions);
            _stuSpread.Add(_stuRight);

            // The spiral binding sits over the fold, on top of both pages.
            _stuBinding = Box("stu-binding");
            _stuBinding.pickingMode = PickingMode.Ignore;
            var seam = Box("stu-seam");
            seam.pickingMode = PickingMode.Ignore;
            _stuBinding.Add(seam);
            var rings = Box("stu-rings");
            rings.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 11; i++)
            {
                var ring = Box("stu-ring");
                ring.pickingMode = PickingMode.Ignore;
                rings.Add(ring);
            }
            _stuBinding.Add(rings);
            _stuSpread.Add(_stuBinding);

            _pageHost.Add(_stuSpread);
        }

        static Label StuFact(VisualElement parent, string icon, string key)
        {
            var fact = Box("stu-fact");
            var top = Box("row");
            top.Add(Icon(icon, "icon-17", "stu-fact-icon"));
            top.Add(Text(key, "font-note", "stu-fact-key"));
            fact.Add(top);
            var value = Text("", "font-heavy", "stu-fact-value");
            fact.Add(value);
            parent.Add(fact);
            return value;
        }

        static InkBox StuAction(VisualElement row, string icon, string label, Action onClick, string variant)
        {
            var b = Ink("stu-act");
            if (variant != null) b.AddToClassList(variant);
            b.Add(Icon(icon, "icon-18", "stu-act-icon"));
            b.Add(Text(label, "font-heavy", "stu-act-label"));
            OnClick(b, onClick);
            row.Add(b);
            return b;
        }

        // --- Showing a student -------------------------------------------------------------------

        /// <summary>Fills the spread for the selected student, or picks another when they've left the school.</summary>
        void StuShowFile()
        {
            if (_stuSpread == null) return;
            var s = S.FindStudent(_stuSelected);
            if (s == null)
            {
                s = StuFallback();
                _stuSelected = s?.Id ?? -1;
                _stuPicking = false;
                _stuExpelArm = -1;
                if (s != null) StuMarkRows();
            }
            bool has = s != null;
            Show(_stuLeft, has);
            Show(_stuRight, has);
            Show(_stuBinding, has);
            Show(_stuFileEmpty, !has);
            if (!has)
            {
                SetText(_stuFileEmpty, "Every student gets a file here, with their friends, worries and diary.");
                return;
            }
            StuShowWho(s);
            StuShowNow(s);
            StuShowSheet(s);
            StuShowActions(s);
        }

        Student StuFallback()
        {
            foreach (var s in _stuSorted)
                if (StuMatches(s)) return s;
            return _stuSorted.Count > 0 ? _stuSorted[0] : null;
        }

        void StuShowWho(Student s)
        {
            var room = S.FindRoom(s.DormRoomId);
            _stuPhoto.ShowStudent(s);
            SetText(_stuCaption, $"{s.FirstName}, {StuTimeHere(S.Day - s.EnrolledDay)}");
            SetHandText(_stuName, s.FullName);
            _stuName.EnableInClassList("stu-name--long", s.FullName.Length > 13);
            SetText(_stuMeta, $"Grade {s.Grade} · {(room != null ? room.ToString() : "no bed")}");
            _stuMeta.EnableInClassList("stu-bad", room == null);
            SetText(_stuParents, $"parents: {s.ParentName} · here since {Calendar.FormatShort(s.EnrolledDay)}");
            string aid = s.Scholarship != ScholarshipType.None
                ? $"{StuWords(s.Scholarship.ToString())} scholarship" + (s.Discount > 0f ? $" · {StuPercent(s.Discount)} off" : "")
                : s.Discount > 0f ? $"{StuPercent(s.Discount)} tuition aid" : "pays full tuition";
            SetText(_stuAid, aid);
            _stuAid.EnableInClassList("chip--good", s.Scholarship != ScholarshipType.None || s.Discount > 0f);
            Show(_stuFrom, s.International);
            if (s.International) SetText(_stuFromText, s.HomeCountry);

            if (_stuShownId != s.Id)
            {
                _stuShownId = s.Id;
                StuShowTraits(s);
            }

            _stuRadar.SetValues(s.Academic, s.Discipline, s.Artistic, s.Athletic, s.Social);
            float[] abilities = { s.Academic, s.Discipline, s.Artistic, s.Athletic, s.Social };
            for (int i = 0; i < abilities.Length; i++) SetText(_stuAbility[i], Mathf.RoundToInt(abilities[i]).ToString());

            SetText(_stuTalent, s.SportTalent != SportType.None ? s.SportTalent.ToString() : "none spotted");
            var team = S.FindTeam(s.TeamId);
            SetText(_stuTeam, team != null ? team.Name
                : _sim.IsUnlocked(Feature.SportsPrograms) ? "not on a team" : $"teams at Lv {Balance.UnlockLevel(Feature.SportsPrograms)}");
            var club = S.FindClub(s.ClubId);
            SetText(_stuClub, club != null ? club.Name
                : _sim.IsUnlocked(Feature.Clubs) ? "not in a club" : $"clubs at Lv {Balance.UnlockLevel(Feature.Clubs)}");
        }

        void StuShowTraits(Student s)
        {
            _stuTraits.Clear();
            for (int i = 0; i < s.Traits.Count; i++)
            {
                var t = s.Traits[i];
                var row = Box("stu-trait");
                var sticker = Ink("stu-sticker", "stu-sticker--" + StuTraitTone(t, i), i % 2 == 0 ? "tilt-a" : "tilt-d");
                sticker.Add(Text(StuWords(t.ToString()), "font-heavy", "stu-sticker-text"));
                row.Add(sticker);
                row.Add(Text(TraitRules.Describe(t), "font-note", "stu-trait-note"));
                _stuTraits.Add(row);
            }
            if (s.Traits.Count == 0) _stuTraits.Add(Text("no strong quirks", "font-note", "stu-trait-note"));
        }

        /// <summary>Sticker colour: yellow for strengths, blush for trouble, blues for the rest.</summary>
        static string StuTraitTone(StudentTrait t, int index) => t switch
        {
            StudentTrait.Hardworking or StudentTrait.Bookworm or StudentTrait.Leader => "good",
            StudentTrait.Troublemaker or StudentTrait.Anxious or StudentTrait.Messy or StudentTrait.Lazy => "bad",
            _ => index % 2 == 0 ? "a" : "b",
        };

        void StuShowNow(Student s)
        {
            _stuStatus.Clear();
            if (S.FindRoom(s.DormRoomId) == null) _stuStatus.Add(StuChip("bed", "no bed", -1));
            if (s.IsInjured(S.Day)) _stuStatus.Add(StuChip("stu-bandage", $"hurt until {MonthDay(s.InjuredUntilDay)}", -1));
            if (s.IsSick(S.Day)) _stuStatus.Add(StuChip("stu-thermo", "sick", -1));
            if (s.Homesick >= 15f) _stuStatus.Add(StuChip("cloud", "homesick", s.Homesick >= 45f ? -1 : 0));
            _stuStatus.Add(s.Incidents > 0
                ? StuChip("alert", s.Incidents == 1 ? "1 incident" : $"{s.Incidents} incidents", -1)
                : StuChip("check", "clean record", 0));

            float target = s.HappinessTarget;
            string trend = Mathf.Abs(target - s.Happiness) < 1f ? "steady" : $"heading to {Mathf.RoundToInt(target)}";
            StuSetMeter(_stuMeters[0], s.Happiness, s.Happiness < 35f, trend, target < s.Happiness - 1f);
            _stuMeters[0].Target.style.left = new Length(Mathf.Clamp(target, 0f, 100f), LengthUnit.Percent);
            _stuMeters[0].Target.EnableInClassList("stu-meter-target--up", target >= s.Happiness);
            StuSetMeter(_stuMeters[1], s.Stress, s.Stress > 70f, "hurts mood and grades", false);
            StuSetMeter(_stuMeters[2], s.Grades, s.Grades < 35f, "", false);
            StuSetMeter(_stuMeters[3], s.Behavior, s.Behavior < 35f, "falls with incidents", false);
            StuSetMeter(_stuMeters[4], s.Health, s.Health < 35f, "", false);
            string worry = StuWorry(s.ParentFactors);
            StuSetMeter(_stuMeters[5], s.ParentMood, s.ParentMood < 35f, worry != null ? "upset: " + worry.ToLowerInvariant() : "", worry != null);
        }

        static void StuSetMeter(StuMeter m, float value, bool trouble, string note, bool noteBad)
        {
            SetText(m.Value, Mathf.RoundToInt(value).ToString());
            SetText(m.Note, note);
            m.Note.EnableInClassList("stu-bad", noteBad);
            m.Name.EnableInClassList("stu-bad", trouble);
            m.Value.EnableInClassList("stu-bad", trouble);
            SetPercent(m.Fill, value);
            m.Fill.EnableInClassList("bar-fill--low", trouble);
        }

        /// <summary>The family's biggest complaint, if one weighs on them noticeably.</summary>
        static string StuWorry(Breakdown b)
        {
            Factor? worst = null;
            foreach (var f in b.Factors)
                if (f.Value <= -2f && (worst == null || f.Value < worst.Value.Value)) worst = f;
            return worst?.Label;
        }

        /// <summary>A status chip: tone -1 is trouble, 1 is good news, 0 is plain.</summary>
        static VisualElement StuChip(string icon, string text, int tone)
        {
            var chip = Box("chip");
            chip.EnableInClassList("chip--bad", tone < 0);
            chip.EnableInClassList("chip--good", tone > 0);
            if (icon != null) chip.Add(Icon(icon, "chip-icon", tone < 0 ? "icon--coral" : "icon--blue"));
            chip.Add(Text(text, "font-heavy"));
            return chip;
        }

        // --- The sticky: why, family, friends, diary, or picking a room --------------------------

        void StuSetTab(int tab)
        {
            _stuTab = tab;
            _stuPicking = false;
            var s = S.FindStudent(_stuSelected);
            StuShowSheet(s);
            StuShowActions(s);
        }

        void StuShowSheet(Student s)
        {
            if (s == null) return;
            for (int i = 0; i < _stuTabs.Length; i++) _stuTabs[i].EnableInClassList("pill-btn--on", !_stuPicking && i == _stuTab);
            int pals = StuShowPals(s, null);
            SetText(_stuTabs[2], pals > 0 ? $"Friends · {pals}" : "Friends");

            string tone = _stuPicking ? "move" : _stuTab == 0 ? "why" : _stuTab == 1 ? "family" : _stuTab == 2 ? "pals" : "diary";
            foreach (var t in new[] { "move", "why", "family", "pals", "diary" }) _stuSheet.EnableInClassList("stu-sheet--" + t, t == tone);

            var body = _stuSheetBody;
            body.Clear();
            if (_stuPicking) StuShowPicker(s, body);
            else if (_stuTab == 0) StuShowWhy(s, body);
            else if (_stuTab == 1) StuShowFamily(s, body);
            else if (_stuTab == 2) StuShowPals(s, body);
            else StuShowDiary(s, body);
        }

        void StuShowWhy(Student s, VisualElement body)
        {
            var title = Text("", "font-hand", "stu-sheet-title");
            SetHandText(title, $"Why is {s.FirstName} at {Mathf.RoundToInt(s.Happiness)}?");
            body.Add(title);
            if (StuFactors(body, s.HappinessFactors) == 0)
            {
                body.Add(Text("Nothing to explain yet.", "font-note", "stu-sheet-note"));
                return;
            }
            body.Add(StuTotal("Target", s.HappinessFactors.Clamped));
            body.Add(Text($"{s.FirstName}'s mood drifts toward the target a little each school day.", "font-note", "stu-sheet-note"));
        }

        void StuShowFamily(Student s, VisualElement body)
        {
            var top = Box("stu-fam-top");
            var who = Box("stu-fam-who");
            who.Add(Text(s.ParentName, "font-heavy", "stu-fam-name"));
            who.Add(Text($"satisfaction {Mathf.RoundToInt(s.ParentMood)} · family wealth {Mathf.RoundToInt(s.FamilyWealth)}", "font-semi", "stu-sheet-meta"));
            top.Add(who);
            var odds = Box("stu-odds");
            float chance = _sim.Students.RetentionChance(s, S.Policies.AnnualTuition);
            var value = Text(StuPercent(chance), "font-hand", "stu-odds-value");
            value.EnableInClassList("stu-bad", chance < 0.7f);
            odds.Add(value);
            odds.Add(Text("chance they re-enrol next year", "font-note", "stu-odds-note"));
            top.Add(odds);
            body.Add(top);
            if (StuFactors(body, s.ParentFactors) == 0)
            {
                body.Add(Text("They haven't made up their minds yet.", "font-note", "stu-sheet-note"));
                return;
            }
            body.Add(StuTotal("Target", s.ParentFactors.Clamped));
            body.Add(Text("Family mood drifts toward the target a little each school day.", "font-note", "stu-sheet-note"));
        }

        /// <summary>Lists the student's real friendships and feuds into <paramref name="body"/> (or just counts them).</summary>
        int StuShowPals(Student s, VisualElement body)
        {
            var rels = new List<Relationship>(S.Relationships.For(s.Id));
            rels.Sort((a, b) => Mathf.Abs(b.Score).CompareTo(Mathf.Abs(a.Score)));
            int n = 0;
            foreach (var r in rels)
            {
                if (Mathf.Abs(r.Score) < 10f && !r.Crush) continue;
                var other = S.FindStudent(r.Other(s.Id));
                if (other == null) continue;
                n++;
                body?.Add(StuPalRow(s, other, r));
            }
            if (body != null && n == 0)
                body.Add(Text("No close friends or foes yet. Roommates, classmates, teams and clubs bring students together.",
                    "font-note", "stu-sheet-note"));
            return n;
        }

        VisualElement StuPalRow(Student s, Student other, Relationship r)
        {
            var row = Box("stu-pal");
            var frame = Box("stu-face", "stu-face--small");
            var face = new PersonPortrait();
            face.AddToClassList("stu-face-img");
            face.ShowStudent(other);
            frame.Add(face);
            row.Add(frame);
            var text = Box("stu-row-text");
            text.Add(Text(other.FullName, "font-heavy", "stu-row-name"));
            var tags = new List<string> { RelationshipSystem.Describe(r.Type).ToLowerInvariant() };
            if (s.DormRoomId >= 0 && other.DormRoomId == s.DormRoomId) tags.Add("roommate");
            if (s.TeamId >= 0 && other.TeamId == s.TeamId) tags.Add("teammate");
            if (s.ClubId >= 0 && other.ClubId == s.ClubId) tags.Add("club");
            if (other.Grade == s.Grade) tags.Add("classmate");
            text.Add(Text(string.Join(" · ", tags), "stu-row-sub"));
            row.Add(text);
            var score = Text(StuSigned(r.Score, "0"), "font-hand", "stu-pal-score");
            score.EnableInClassList("stu-good", r.Score >= 30f);
            score.EnableInClassList("stu-bad", r.Score <= -25f);
            row.Add(score);
            row.Add(Icon(StuRelIcon(r.Type), "icon-22"));
            int id = other.Id;
            OnClick(row, () =>
            {
                var o = S.FindStudent(id);
                if (o == null) return;
                if (!StuMatches(o)) StuClearFilter(true);
                StuSelect(id);
                StuScrollToSelected();
            });
            return row;
        }

        static string StuRelIcon(RelationshipType t) => t switch
        {
            RelationshipType.BestFriend => "heart-red",
            RelationshipType.Friend => "smile",
            RelationshipType.Crush => "stu-crush",
            RelationshipType.Rival => "stu-bolt",
            RelationshipType.Enemy => "stu-enemy",
            _ => "minus",
        };

        void StuShowDiary(Student s, VisualElement body)
        {
            if (s.Moodlets.Count > 0)
            {
                body.Add(Text("lately", "font-note", "stu-sheet-kicker"));
                var chips = Box("stu-chips");
                foreach (var m in s.Moodlets)
                    chips.Add(StuChip(null, $"{StuSigned(m.Value, "0")} {m.Label}", m.Value < 0f ? -1 : 1));
                body.Add(chips);
            }
            body.Add(Text("Diary", "font-hand", "stu-sheet-title"));
            for (int i = s.History.Count - 1; i >= 0; i--) body.Add(Text(s.History[i], "font-note", "stu-diary-line"));
            if (s.History.Count == 0) body.Add(Text("Nothing written yet.", "font-note", "stu-sheet-note"));
        }

        /// <summary>The debug room picker: dorm rooms with a free bed and how well the newcomer would get on there.</summary>
        void StuShowPicker(Student s, VisualElement body)
        {
            var head = Box("stu-sheet-head");
            var title = Text("", "font-hand", "stu-sheet-title");
            SetHandText(title, $"Move {s.FirstName} to…");
            head.Add(title);
            var close = Ink("stu-x");
            close.Add(Icon("close", "icon-13"));
            OnClick(close, StuToggleMove);
            head.Add(close);
            body.Add(head);
            body.Add(Text("rooms with a free bed, and how well they'd get on with who sleeps there", "font-note", "stu-sheet-meta-note"));

            int n = 0;
            foreach (var r in S.Rooms)
            {
                if (r.Type != RoomType.DormRoom || !r.IsOperational(S.Day) || r.Id == s.DormRoomId) continue;
                var residents = _sim.Indices.ResidentsOf(r.Id);
                if (residents.Count >= r.Capacity) continue;
                float fit = 0f;
                foreach (var o in residents) fit += TraitRules.Compatibility(s, o, true);
                body.Add(StuRoomRow(s, r, residents, fit));
                n++;
            }
            if (n == 0)
                body.Add(Text("Every other dorm room is full. Build or upgrade a dorm room, or swap rooms instead.", "font-note", "stu-sheet-note"));
        }

        VisualElement StuRoomRow(Student s, Room r, List<Student> residents, float fit)
        {
            var row = Ink("stu-room");
            var text = Box("stu-room-text");
            text.Add(Text(r.Name, "font-heavy", "stu-room-name"));
            var beds = Box("stu-beds");
            for (int i = 0; i < r.Capacity; i++) beds.Add(Box("stu-bed", i < residents.Count ? "stu-bed--taken" : "stu-bed--free"));
            beds.Add(Text($"L{r.Level}", "font-semi", "stu-room-level"));
            text.Add(beds);
            row.Add(text);

            var faces = Box("stu-room-faces");
            for (int i = 0; i < residents.Count && i < 4; i++)
            {
                var face = new PersonPortrait();
                face.AddToClassList("stu-room-face");
                face.ShowStudent(residents[i]);
                faces.Add(face);
            }
            if (residents.Count > 4) faces.Add(Text($"+{residents.Count - 4}", "font-heavy", "stu-room-more"));
            if (residents.Count == 0) faces.Add(Text("empty", "font-note", "stu-room-more"));
            row.Add(faces);

            string word = fit > 1f ? "good fit" : fit < -1f ? "clash" : "okay";
            row.Add(StuChip(null, $"{word} {StuSigned(fit, "0.0")}", fit > 1f ? 1 : fit < -1f ? -1 : 0));
            int sid = s.Id, rid = r.Id;
            OnClick(row, () =>
            {
                _stuPicking = false;
                RunCommand(() => _sim.MoveStudent(sid, rid));
            });
            return row;
        }

        /// <summary>Adds a breakdown's terms, biggest first, as label and signed value. Returns how many it showed.</summary>
        static int StuFactors(VisualElement body, Breakdown b)
        {
            var list = new List<Factor>(b.Factors);
            list.Sort((x, y) => Mathf.Abs(y.Value).CompareTo(Mathf.Abs(x.Value)));
            int n = 0;
            foreach (var f in list)
            {
                if (Mathf.Abs(f.Value) < 0.05f) continue;
                var row = Box("stu-factor");
                row.Add(Text(f.Label, "font-semi", "stu-factor-label"));
                var value = Text(StuSigned(f.Value, "0.0"), "font-heavy", "stu-factor-value");
                value.EnableInClassList("stu-good", f.Value > 0f);
                value.EnableInClassList("stu-bad", f.Value < 0f);
                row.Add(value);
                body.Add(row);
                n++;
            }
            return n;
        }

        static VisualElement StuTotal(string label, float value)
        {
            var total = Box("stu-total");
            var line = new DashLine();
            line.AddToClassList("stu-total-line");
            total.Add(line);
            var row = Box("stu-factor");
            row.Add(Text(label, "font-heavy", "stu-factor-label"));
            row.Add(Text(Mathf.RoundToInt(value).ToString(), "font-heavy", "stu-factor-value"));
            total.Add(row);
            return total;
        }

        // --- Actions ---------------------------------------------------------------------------

        void StuShowActions(Student s)
        {
            if (s == null || _stuMoveBtn == null) return;
            bool noBed = S.FindRoom(s.DormRoomId) == null;
            SetText(_stuMoveLabel, noBed ? "Find a bed" : "Move room");
            _stuMoveBtn.EnableInClassList("stu-act--on", _stuPicking);
            bool swapping = _stuSwapFrom == s.Id;
            SetText(_stuSwapLabel, swapping ? "Stop swap" : "Swap…");
            _stuSwapBtn.EnableInClassList("stu-act--on", swapping);

            bool armed = _stuExpelArm == s.Id && GameClock.Now < _stuExpelUntil;
            _stuExpelBtn.EnableInClassList("stu-act--armed", armed);
            SetText(_stuExpelLabel, armed ? "Sure? Again" : "Expel…");
            int friends = 0;
            foreach (var r in S.Relationships.For(s.Id))
                if (r.Score >= 30f) friends++;
            // Mirrors the expel command: known troublemakers reassure parents, anyone else looks harsh.
            bool firm = s.Incidents >= 2 || s.Has(StudentTrait.Troublemaker);
            SetText(_stuExpelNote, armed
                ? (friends == 0 ? "no close friends" : friends == 1 ? "1 friend takes it hard" : $"{friends} friends take it hard")
                : firm ? "parents approve" : "seen as harsh");
        }

        void StuToggleMove()
        {
            var s = S.FindStudent(_stuSelected);
            if (s == null) return;
            _stuPicking = !_stuPicking;
            StuShowSheet(s);
            StuShowActions(s);
        }

        void StuToggleSwap()
        {
            var s = S.FindStudent(_stuSelected);
            if (s == null) return;
            _stuPicking = false;
            StuSetSwap(_stuSwapFrom == s.Id ? -1 : s.Id);
            StuShowSheet(s);
        }

        /// <summary>Expelling takes two clicks within three seconds, like the debug page's confirm button.</summary>
        void StuExpelClick()
        {
            var s = S.FindStudent(_stuSelected);
            if (s == null) return;
            if (_stuExpelArm == s.Id && GameClock.Now < _stuExpelUntil)
            {
                _stuExpelArm = -1;
                int id = s.Id;
                RunCommand(() => _sim.Expel(id));
                return;
            }
            _stuExpelArm = s.Id;
            _stuExpelUntil = GameClock.Now + 3f;
            StuShowActions(s);
        }

        /// <summary>The cheap refresh between days: lets an unanswered expel confirmation lapse.</summary>
        void StuTick()
        {
            if (_stuExpelArm < 0 || GameClock.Now < _stuExpelUntil) return;
            _stuExpelArm = -1;
            StuShowActions(S.FindStudent(_stuSelected));
        }

        // --- Small helpers -------------------------------------------------------------------------

        /// <summary>"CleanFreak" to "Clean freak".</summary>
        static string StuWords(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c))
                {
                    sb.Append(' ');
                    c = char.ToLowerInvariant(c);
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>The polaroid caption: "first week", "5 weeks in", "a year in"...</summary>
        static string StuTimeHere(int days)
        {
            if (days < 7) return "first week";
            if (days < 2 * Calendar.DaysPerMonth) return $"{days / Calendar.DaysPerWeek} weeks in";
            if (days < Calendar.DaysPerYear) return $"{days / Calendar.DaysPerMonth} months in";
            return days < 2 * Calendar.DaysPerYear ? "a year in" : $"{days / Calendar.DaysPerYear} years in";
        }

        static string StuSigned(float v, string format) => (v < 0f ? "–" : "+") + Mathf.Abs(v).ToString(format);

        static string StuPercent(float fraction) => Mathf.RoundToInt(fraction * 100f) + "%";

        // --- Drawings ------------------------------------------------------------------------------

        /// <summary>The abilities pentagon: five 0-100 values, academic on top, then clockwise.</summary>
        sealed class StuRadar : VisualElement
        {
            static readonly Color Paper = InkIcons.Hex(0xEAF3FD), Half = InkIcons.Hex(0xA8C6EA), Spoke = InkIcons.Hex(0xBCD5F0);
            static readonly Color Line = InkIcons.Hex(0x2F6FD0), Area = new Color(0.353f, 0.608f, 0.918f, 0.45f);

            readonly float[] _values = new float[5];

            public StuRadar()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void SetValues(float a, float b, float c, float d, float e)
            {
                bool changed = Set(0, a) | Set(1, b) | Set(2, c) | Set(3, d) | Set(4, e);
                if (changed) MarkDirtyRepaint();
            }

            bool Set(int i, float v)
            {
                v = Mathf.Clamp(v, 0f, 100f);
                if (Mathf.Abs(_values[i] - v) < 0.2f) return false;
                _values[i] = v;
                return true;
            }

            static Vector2 Corner(Vector2 c, float r, int i)
            {
                float a = (-90f + 72f * i) * Mathf.Deg2Rad;
                return c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }

            void Pentagon(Painter2D p, Vector2 c, float r)
            {
                p.BeginPath();
                p.MoveTo(Corner(c, r, 0));
                for (int i = 1; i < 5; i++) p.LineTo(Corner(c, r, i));
                p.ClosePath();
            }

            void Draw(MeshGenerationContext ctx)
            {
                float w = layout.width, h = layout.height;
                if (w < 8f || h < 8f || float.IsNaN(w) || float.IsNaN(h)) return;
                // The design's 220 x 210 box: centre (110, 112), radius 72.
                float s = Mathf.Min(w / 220f, h / 210f);
                var c = new Vector2(w / 2f, h / 2f + 7f * s);
                float r = 72f * s;
                var p = ctx.painter2D;
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;

                p.fillColor = Paper;
                p.strokeColor = InkIcons.Ink;
                p.lineWidth = 2f;
                Pentagon(p, c, r);
                p.Fill();
                p.Stroke();

                p.strokeColor = Spoke;
                p.lineWidth = 1.5f;
                p.BeginPath();
                for (int i = 0; i < 5; i++)
                {
                    p.MoveTo(c);
                    p.LineTo(Corner(c, r, i));
                }
                p.Stroke();

                // The half-way ring, dashed.
                p.strokeColor = Half;
                p.lineCap = LineCap.Butt;
                p.BeginPath();
                for (int i = 0; i < 5; i++)
                {
                    Vector2 a = Corner(c, r * 0.5f, i), b = Corner(c, r * 0.5f, (i + 1) % 5);
                    float len = Vector2.Distance(a, b);
                    for (float t = 0f; t < len; t += 8f)
                    {
                        p.MoveTo(Vector2.Lerp(a, b, t / len));
                        p.LineTo(Vector2.Lerp(a, b, Mathf.Min(len, t + 4f) / len));
                    }
                }
                p.Stroke();
                p.lineCap = LineCap.Round;

                var pts = new Vector2[5];
                for (int i = 0; i < 5; i++) pts[i] = Corner(c, r * Mathf.Max(0.04f, _values[i] / 100f), i);
                p.fillColor = Area;
                p.strokeColor = Line;
                p.lineWidth = 2.6f;
                p.BeginPath();
                p.MoveTo(pts[0]);
                for (int i = 1; i < 5; i++) p.LineTo(pts[i]);
                p.ClosePath();
                p.Fill();
                p.Stroke();

                p.fillColor = InkIcons.Marker;
                p.strokeColor = InkIcons.Ink;
                p.lineWidth = 1.6f;
                foreach (var pt in pts)
                {
                    p.BeginPath();
                    p.Arc(pt, 4f * Mathf.Max(0.8f, s), 0f, 360f);
                    p.Fill();
                    p.Stroke();
                }
            }
        }

        /// <summary>Ruled notebook lines behind the right-hand page.</summary>
        sealed class StuRuled : VisualElement
        {
            static readonly Color Rule = InkIcons.Hex(0xDCE9F7);

            public StuRuled()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext ctx)
            {
                float w = layout.width, h = layout.height;
                if (w < 1f || h < 1f || float.IsNaN(w) || float.IsNaN(h)) return;
                var p = ctx.painter2D;
                p.strokeColor = Rule;
                p.lineWidth = 1f;
                p.BeginPath();
                for (float y = 31.5f; y < h; y += 32f)
                {
                    p.MoveTo(new Vector2(0f, y));
                    p.LineTo(new Vector2(w, y));
                }
                p.Stroke();
            }
        }
    }
}
