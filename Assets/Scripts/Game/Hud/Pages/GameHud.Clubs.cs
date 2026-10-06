using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SchoolSim.Game
{
    /// <summary>
    /// The clubs and teams page. Teams hang as pennants on the left (GameHud.Clubs.Teams.cs).
    /// Clubs are on the right: the Club Room slots, each club as a badge card with its members'
    /// faces, size, skill, competition wins and what it does for the school, and every club the
    /// school could still start, with what it needs.
    /// </summary>
    public sealed partial class GameHud
    {
        const float ClbArmSeconds = 3f;
        const int ClbFaceLimit = 10;
        const int ClbCompeteMembers = 4;        // clubs compete, and drama and music put on shows,
        const int ClbShowMembers = 5;           // once they're this big (ClubSystem)
        const float ClbJoinInterest = 0.4f;     // a new club seeds itself with up to six keen students
        const int ClbFoundingMembers = 6;

        int _clbSignature;
        string _clbArmed;                       // the disband button waiting for its second tap
        float _clbArmedUntil;
        VisualElement _clbTeams, _clbPennants, _clbTeamBody, _clbSlots, _clbClubBody;
        Label _clbTeamsNote, _clbSlotsText, _clbSlotsNote;

        partial void BuildClubsPage()
        {
            ClbDefineIcons();
            _clbSignature = 0;
            _clbArmed = null;

            _clbTeams = Box("clb-col", "clb-teams");
            var teamHead = Box("clb-col-head");
            teamHead.Add(Text("Teams", "font-hand", "clb-col-title"));
            _clbTeamsNote = Text("", "font-note", "clb-col-note");
            teamHead.Add(_clbTeamsNote);
            _clbTeams.Add(teamHead);
            _clbPennants = Box("clb-pennants");
            _clbTeams.Add(_clbPennants);
            var teamScroll = new ScrollView(ScrollViewMode.Vertical);
            teamScroll.AddToClassList("clb-scroll");
            teamScroll.contentContainer.AddToClassList("clb-scroll-content");
            _clbTeamBody = teamScroll.contentContainer;
            _clbTeams.Add(teamScroll);
            _pageHost.Add(_clbTeams);

            var clubs = Box("clb-col", "clb-clubs");
            var clubHead = Box("clb-col-head");
            clubHead.Add(Text("Clubs", "font-hand", "clb-col-title"));
            clubHead.Add(Text("$150/mo each", "font-note", "clb-col-note"));
            clubs.Add(clubHead);
            var slotsRow = Box("clb-slots-row");
            _clbSlots = Box("clb-slots");
            slotsRow.Add(_clbSlots);
            var slotWords = Box("clb-slots-words");
            _clbSlotsText = Text("", "font-heavy", "clb-slots-text");
            _clbSlotsNote = Text("", "font-note", "clb-slots-note");
            slotWords.Add(_clbSlotsText);
            slotWords.Add(_clbSlotsNote);
            slotsRow.Add(slotWords);
            clubs.Add(slotsRow);
            var clubScroll = new ScrollView(ScrollViewMode.Vertical);
            clubScroll.AddToClassList("clb-scroll");
            clubScroll.contentContainer.AddToClassList("clb-scroll-content");
            _clbClubBody = clubScroll.contentContainer;
            clubs.Add(clubScroll);
            _pageHost.Add(clubs);
        }

        partial void RefreshClubsPage(bool newDay)
        {
            if (_clbTeams == null) return;
            bool disarm = _clbArmed != null && GameClock.Now >= _clbArmedUntil;
            if (disarm) _clbArmed = null;
            if (!newDay && !disarm && ClbSignature() == _clbSignature) return;
            ClbRebuild();
        }

        partial void ResetClubsPage()
        {
            _clbTeam = -1;
            _clbArmed = null;
        }

        void ClbRebuild()
        {
            _clbSignature = ClbSignature();
            ClbFillTeams();
            ClbFillClubs();
        }

        /// <summary>What the page must follow between days too: teams, rosters, coaches, clubs, members, money for founding.</summary>
        int ClbSignature()
        {
            unchecked
            {
                int h = (_sim.IsUnlocked(Feature.SportsPrograms) ? 1 : 0) + (_sim.IsUnlocked(Feature.Clubs) ? 2 : 0);
                foreach (var t in S.Teams) h = ((h * 31 + t.Id) * 31 + t.Roster.Count * 7 + (int)t.Intensity) * 31 + t.CoachId;
                foreach (var c in S.Clubs) h = (h * 31 + c.Id) * 31 + c.Members.Count;
                foreach (var s in S.Staff)
                    if (s.Role == StaffRole.Coach) h = h * 31 + s.Id;
                h = h * 31 + (S.Finance.Cash >= ClubSystem.FoundingCost ? 1 : 0);
                h = h * 31 + _sim.Clubs.ClubSlots();
                return h * 31 + _clbTeam;
            }
        }

        // --- Clubs ----------------------------------------------------------------------------

        void ClbFillClubs()
        {
            int slots = _sim.Clubs.ClubSlots(), used = _sim.Clubs.UsedSlots();
            _clbSlots.Clear();
            for (int i = 0; i < Mathf.Max(slots, used); i++)
            {
                var slot = Ink("clb-slot", i < used ? "clb-slot--used" : "clb-slot--free");
                slot.Add(Icon("door", "clb-slot-icon"));
                _clbSlots.Add(slot);
            }
            SetText(_clbSlotsText, slots == 0 ? "No Club Room yet" : $"Club Rooms: {used} of {slots} slots used");
            SetText(_clbSlotsNote, slots == 0 ? "each one hosts a club (more once upgraded); the Gaming club meets in the Gaming Room"
                : "the Gaming club meets in the Gaming Room instead");

            _clbClubBody.Clear();
            if (!_sim.IsUnlocked(Feature.Clubs))
            {
                _clbClubBody.Add(ClbLocked(ClbBadge(ClubType.Chess, false), $"Clubs unlock at school level {Balance.UnlockLevel(Feature.Clubs)}.",
                    "Members are happier and learn new skills."));
                return;
            }

            if (S.Clubs.Count > 0)
            {
                var grid = Box("clb-club-grid");
                foreach (var c in S.Clubs) grid.Add(ClbClubCard(c));
                _clbClubBody.Add(grid);
            }
            else _clbClubBody.Add(Text("No clubs yet. Members are happier and learn new skills, so start one below.", "font-note", "empty-note", "clb-clubs-empty"));

            var starts = Box("clb-start-grid");
            foreach (ClubType type in Enum.GetValues(typeof(ClubType)))
                if (!S.Clubs.Exists(c => c.Type == type)) starts.Add(ClbStartTile(type));
            if (starts.childCount > 0)
            {
                _clbClubBody.Add(Text("Start a club", "font-hand", "clb-sub-title"));
                _clbClubBody.Add(starts);
            }
            _clbClubBody.Add(Text("Clubs cost $150/mo. Members are happier, grow their skills, and can win competitions for academic or student-life reputation.",
                "font-note", "clb-foot"));
        }

        VisualElement ClbClubCard(Club c)
        {
            var card = Ink("clb-club", c.Id % 2 == 0 ? "clb-club--a" : "clb-club--b");
            var head = Box("clb-club-head");
            head.Add(ClbBadge(c.Type, false));
            var title = Box("clb-club-title");
            var name = Text("", "font-hand", "clb-club-name");
            SetHandText(name, c.Name);
            title.Add(name);
            title.Add(Text($"since {Calendar.FormatShort(c.FoundedDay)}", "font-note", "clb-club-since"));
            head.Add(title);
            int id = c.Id;
            var disband = ClbDisband("club" + id, "Disband", () => RunCommand(() => _sim.DisbandClub(id)));
            disband.AddToClassList("clb-btn--small");
            head.Add(disband);
            card.Add(head);

            card.Add(ClbMeter("members", 100f * c.Members.Count / Club.MaxMembers, $"{c.Members.Count}/{Club.MaxMembers}"));
            float skill = _sim.Clubs.Strength(c);
            card.Add(ClbMeter("skill", skill, Mathf.RoundToInt(skill).ToString()));

            var medals = Box("clb-medals");
            for (int i = 0; i < Mathf.Min(c.Wins, 6); i++) medals.Add(Icon("clb-medal", "clb-medal"));
            medals.Add(Text(ClbCompetes(c), "font-note", "clb-medal-text"));
            card.Add(medals);
            card.Add(Text(ClbPerks(c), "font-note", "clb-perks"));

            // Members as faces: point at one to see who it is, tap to open their file.
            const string tip = "tap a face to open their file";
            var hint = Text(tip, "font-semi", "clb-hint");
            var faces = Box("clb-faces");
            int count = 0;
            foreach (int sid in c.Members)
            {
                var s = S.FindStudent(sid);
                if (s == null || count++ >= ClbFaceLimit) continue;
                var face = ClbFace(s, "clb-face--member");
                string who = $"{s.FullName}, grade {s.Grade}";
                face.RegisterCallback<PointerEnterEvent>(_ => SetText(hint, who));
                face.RegisterCallback<PointerLeaveEvent>(_ => SetText(hint, tip));
                int studentId = s.Id;
                OnClick(face, () => ClbOpenStudent(studentId));
                faces.Add(face);
            }
            if (count > ClbFaceLimit) faces.Add(Text($"+{count - ClbFaceLimit}", "font-heavy", "clb-more"));
            if (count == 0) faces.Add(Text("no members yet: students join at the start of each month", "font-note", "clb-empty"));
            card.Add(faces);
            if (count > 0) card.Add(hint);
            return card;
        }

        /// <summary>Wins so far and where the club competes (ClubSystem: monthly, from four members, national from a high level).</summary>
        string ClbCompetes(Club c)
        {
            string wins = c.Wins == 0 ? "no wins yet" : c.Wins == 1 ? "1 win" : $"{c.Wins} wins";
            if (c.Type == ClubType.StudentGovernment) return "doesn't enter contests";
            if (c.Members.Count < ClbCompeteMembers) return $"{wins} · enters contests with {ClbCompeteMembers}+ members";
            return _sim.IsUnlocked(Feature.LargeCompetitions)
                ? $"{wins} · national contests"
                : $"{wins} · regional contests (national at Lv {Balance.UnlockLevel(Feature.LargeCompetitions)})";
        }

        /// <summary>What a club does for its members and the school, as ClubSystem runs it.</summary>
        static string ClbPerks(Club c)
        {
            var t = c.Type;
            bool arts = t == ClubType.Art || t == ClubType.Music || t == ClubType.Drama;
            bool academic = t == ClubType.Robotics || t == ClubType.Chess || t == ClubType.Debate || t == ClubType.Science;
            var parts = new List<string>();
            if (arts) parts.Add("grows art skills");
            if (t == ClubType.StudentGovernment || t == ClubType.Debate) parts.Add("grows social skills");
            if (academic) parts.Add("grows academics");
            if (t == ClubType.Drama || t == ClubType.Music)
                parts.Add(c.Members.Count >= ClbShowMembers ? "shows sell tickets" : $"shows from {ClbShowMembers} members");
            if (t != ClubType.StudentGovernment) parts.Add(academic ? "wins lift academic rep" : "wins lift student life rep");
            if (parts.Count == 0) parts.Add("happier members");
            return string.Join(" · ", parts);
        }

        VisualElement ClbStartTile(ClubType type)
        {
            bool can = _sim.Clubs.CanCreate(type, out string why);
            var tile = Ink("clb-start");
            tile.EnableInClassList("clb-start--off", !can);
            tile.Add(ClbBadge(type, true));
            var name = Text("", "font-hand", "clb-start-name");
            SetHandText(name, new Club { Type = type }.Name);
            tile.Add(name);
            int keen = ClbKeenMembers(type);
            tile.Add(Text(keen == 0 ? "nobody keen yet" : keen == 1 ? "1 student would join" : $"{keen} students would join", "font-note", "clb-start-keen"));
            if (!can) tile.Add(Text(why, "font-semi", "clb-why"));
            var room = can ? null : ClbMissingRoom(type);
            if (room.HasValue && S.Progress.SchoolLevel >= RoomCatalog.Get(room.Value).MinSchoolLevel)
            {
                var def = RoomCatalog.Get(room.Value);
                var build = room.Value;
                tile.Add(ClbLink("pencil", $"build one · {MoneyShort(def.Cost[0])}", () => ClbBuild(build)));
            }
            var start = ClbButton(null, $"Start · {Money(ClubSystem.FoundingCost)}", () => RunCommand(() => _sim.CreateClub(type)));
            start.AddToClassList("clb-btn--primary");
            start.AddToClassList("clb-start-btn");
            start.EnableInClassList("clb-btn--off", !can);
            tile.Add(start);
            return tile;
        }

        /// <summary>Students who'd be founding members: not in a club yet and keen enough, at most six (ClubSystem.Create).</summary>
        int ClbKeenMembers(ClubType type)
        {
            int n = 0;
            foreach (var s in S.Students)
                if (s.ClubId < 0 && ClubSystem.Interest(s, type) >= ClbJoinInterest) n++;
            return Mathf.Min(n, ClbFoundingMembers);
        }

        /// <summary>The room that would let this club start, in the order ClubSystem.CanCreate checks them, or null.</summary>
        RoomType? ClbMissingRoom(ClubType type)
        {
            if (type == ClubType.Gaming) return _sim.HasOperational(RoomType.GamingRoom) ? (RoomType?)null : RoomType.GamingRoom;
            if (_sim.Clubs.UsedSlots() >= _sim.Clubs.ClubSlots()) return RoomType.ClubRoom;
            if (type == ClubType.Robotics && !_sim.HasOperational(RoomType.ComputerLab)) return RoomType.ComputerLab;
            if (type == ClubType.Science && !_sim.HasOperational(RoomType.ScienceLab)) return RoomType.ScienceLab;
            return null;
        }

        // --- Shared pieces --------------------------------------------------------------------

        /// <summary>Closes the page and starts placing a room on the campus, as the level-up card does.</summary>
        void ClbBuild(RoomType type)
        {
            CloseSheet();
            CampusController.Instance?.BeginBuild(type);
        }

        void ClbOpenStudent(int id)
        {
            _studentFocus = id;
            ShowPageAt(HudPage.Students);
        }

        void ClbOpenStaff(int id)
        {
            _staffFocus = id;
            ShowPageAt(HudPage.Staff);
        }

        static InkBox ClbButton(string icon, string label, Action onClick)
        {
            var b = Ink("clb-btn");
            if (icon != null) b.Add(Icon(icon, "clb-btn-icon"));
            b.Add(Text(label, "font-heavy", "clb-btn-label"));
            OnClick(b, onClick);
            return b;
        }

        /// <summary>A disband button that needs a second tap within a few seconds.</summary>
        InkBox ClbDisband(string key, string label, Action run)
        {
            bool armed = _clbArmed == key;
            var b = ClbButton(armed ? null : "close", armed ? "Sure? Tap again" : label, () =>
            {
                if (_clbArmed == key && GameClock.Now < _clbArmedUntil)
                {
                    _clbArmed = null;
                    run();
                    return;
                }
                _clbArmed = key;
                _clbArmedUntil = GameClock.Now + ClbArmSeconds;
                ClbRebuild();
            });
            b.AddToClassList(armed ? "clb-btn--armed" : "clb-btn--quiet");
            return b;
        }

        static VisualElement ClbLink(string icon, string label, Action onClick)
        {
            var link = Box("clb-link");
            link.Add(Icon(icon, "clb-link-icon"));
            link.Add(Text(label, "font-note", "clb-link-label"));
            OnClick(link, onClick);
            return link;
        }

        static VisualElement ClbSection(string title, string note)
        {
            var row = Box("clb-section");
            row.Add(Text(title, "font-hand", "clb-section-title"));
            if (note != null) row.Add(Text(note, "font-note", "clb-section-note"));
            return row;
        }

        static VisualElement ClbMeter(string name, float percent, string value)
        {
            var row = Box("clb-meter");
            row.Add(Text(name, "font-semi", "clb-meter-name"));
            var bar = Box("bar", "clb-meter-bar");
            var fill = Box("bar-fill");
            SetPercent(fill, percent);
            bar.Add(fill);
            row.Add(bar);
            row.Add(Text(value, "font-heavy", "clb-meter-value"));
            return row;
        }

        static InkBox ClbBadge(ClubType type, bool small)
        {
            var badge = Ink("clb-badge");
            if (small) badge.AddToClassList("clb-badge--small");
            badge.SetFill(ClbClubColor(type));
            badge.Add(Icon(ClbClubIcon(type), "clb-badge-icon"));
            return badge;
        }

        /// <summary>A sticky note for a part of the page the school hasn't unlocked yet, with a padlock on its picture.</summary>
        static VisualElement ClbLocked(VisualElement art, string text, string note)
        {
            var sticky = Ink("clb-locked");
            sticky.Add(TapeStrip("clb-locked-tape", "tape-yellow"));
            var picture = Box("clb-locked-art");
            picture.Add(art);
            var padlock = Ink("clb-lock");
            padlock.Add(Icon("lock", "icon-17"));
            picture.Add(padlock);
            sticky.Add(picture);
            sticky.Add(Text(text, "font-heavy", "clb-locked-text"));
            sticky.Add(Text(note, "font-note", "clb-locked-note"));
            return sticky;
        }

        static VisualElement ClbFace(Student s, string size)
        {
            var portrait = new PersonPortrait();
            portrait.ShowStudent(s);
            return ClbFrame(portrait, size);
        }

        static VisualElement ClbFace(StaffMember m, string size)
        {
            var portrait = new PersonPortrait();
            portrait.ShowStaff(m);
            return ClbFrame(portrait, size);
        }

        /// <summary>A face cropped to a circle, with the ink ring drawn over the shoulders.</summary>
        static VisualElement ClbFrame(PersonPortrait portrait, string size)
        {
            var frame = Ink("clb-face", size);
            portrait.AddToClassList("clb-portrait");
            frame.Add(portrait);
            var ring = Ink("clb-face-ring");
            ring.pickingMode = PickingMode.Ignore;
            frame.Add(ring);
            return frame;
        }

        /// <summary>A breakdown as rows with bars to the left (minus) and right (plus) of a centre line, biggest first.</summary>
        static void ClbFactors(VisualElement parent, Breakdown b)
        {
            var factors = new List<Factor>(b.Factors);
            factors.RemoveAll(f => Mathf.Abs(f.Value) < 0.05f);
            factors.Sort((x, y) => Mathf.Abs(y.Value).CompareTo(Mathf.Abs(x.Value)));
            float scale = 1f;
            foreach (var f in factors) scale = Mathf.Max(scale, Mathf.Abs(f.Value));
            foreach (var f in factors)
            {
                var row = Box("clb-factor");
                row.Add(Text(f.Label, "font-semi", "clb-factor-label"));
                var bar = Box("clb-factor-bar");
                var minus = Box("clb-factor-half", "clb-factor-half--minus");
                var plus = Box("clb-factor-half");
                var fill = Box("clb-factor-fill");
                fill.style.width = new Length(Mathf.Max(6f, 100f * Mathf.Abs(f.Value) / scale), LengthUnit.Percent);
                if (f.Value < 0f)
                {
                    fill.AddToClassList("clb-factor-fill--minus");
                    minus.Add(fill);
                }
                else plus.Add(fill);
                bar.Add(minus);
                bar.Add(plus);
                row.Add(bar);
                var value = Text((f.Value >= 0f ? "+" : "–") + Mathf.Abs(f.Value).ToString("0.0"), "font-heavy", "clb-factor-value");
                value.EnableInClassList("clb-factor-value--minus", f.Value < 0f);
                row.Add(value);
                parent.Add(row);
            }
            if (factors.Count == 0) parent.Add(Text("Nothing yet: it's worked out every day.", "font-note", "clb-empty"));
        }

        static string ClbClubIcon(ClubType type) => type switch
        {
            ClubType.Robotics => "clb-robot",
            ClubType.Chess => "clb-chess",
            ClubType.Art => "clb-palette",
            ClubType.Drama => "clb-mask",
            ClubType.Music => "music",
            ClubType.Debate => "clb-debate",
            ClubType.Gaming => "clb-gamepad",
            ClubType.Science => "clb-flask",
            ClubType.StudentGovernment => "clb-ballot",
            _ => "star",
        };

        static Color ClbClubColor(ClubType type) => type switch
        {
            ClubType.Robotics => InkIcons.Sky,
            ClubType.Chess => InkIcons.Paper,
            ClubType.Art => InkIcons.Marker,
            ClubType.Drama => InkIcons.Hex(0xFFF3C4),
            ClubType.Music => InkIcons.Hex(0xC9E1F9),
            ClubType.Debate => InkIcons.Pale,
            ClubType.Gaming => InkIcons.Hex(0x9CC3EC),
            ClubType.Science => InkIcons.Hex(0xEAF3FD),
            ClubType.StudentGovernment => InkIcons.Marker,
            _ => InkIcons.Pale,
        };

        static void ClbDefineIcons()
        {
            // Sports
            InkIcons.Define("clb-basketball", 1.8f, "fill:#FFE066 circle:12,12,8.5",
                "M12 3.5v17M3.5 12h17M6.2 5.8c2.8 2.6 2.8 9.8 0 12.4M17.8 5.8c-2.8 2.6-2.8 9.8 0 12.4");
            InkIcons.Define("clb-soccer", 1.8f, "fill:#FBFDFF circle:12,12,8.5", "ink M12 8.6l3.2 2.3-1.2 3.8h-4l-1.2-3.8z",
                "M12 8.6V3.6M15.2 10.9l4.5-1.5M14 14.7l2.8 3.9M10 14.7l-2.8 3.9M8.8 10.9l-4.5-1.5");
            InkIcons.Define("clb-swim", 1.9f, "fill:#A8D1F7 circle:16.5,6.5,2.4", "M4.5 12.5l4.5-3.5 3.5 2.5 3-2",
                "M2.5 16c1.6-1.3 3.2-1.3 4.8 0s3.2 1.3 4.8 0 3.2-1.3 4.8 0 3.2 1.3 4.8 0M2.5 20c1.6-1.3 3.2-1.3 4.8 0s3.2 1.3 4.8 0 3.2-1.3 4.8 0 3.2 1.3 4.8 0");
            InkIcons.Define("clb-track", 2f, "fill:#A8D1F7 circle:14,4.5,2", "M8 21l3-6 3 2v5M14 17l-1-6-4 1-2 4M13 11l3 2 3-1");
            InkIcons.Define("clb-tennis", 1.8f, "fill:#DCEBFB circle:14.5,9.5,5.5", "M11.5 7.5l6 4M10.8 10.4l5.4 3.2M12.8 5.2l5.6 3.4",
                "M10.6 13.4L4.5 19.5", "fill:#FFE066 circle:5.5,6.5,2");
            // Clubs
            InkIcons.Define("clb-robot", 1.8f, "M12 7.5V4.5", "fill:#FFE066 circle:12,3.6,1.4", "fill:#A8D1F7 rect:5,7.5,14,11.5,3",
                "M5 12.5H3.2M19 12.5h1.8", "bare ink circle:9.4,12,1.4", "bare ink circle:14.6,12,1.4", "M9.8 15.6h4.4");
            InkIcons.Define("clb-chess", 1.8f, "fill:#FBFDFF circle:12,6.6,2.8", "fill:#FBFDFF M9.5 10.4h5l1.6 7.2H7.9z",
                "fill:#DCEBFB rect:6.2,17.6,11.6,3,1.3");
            InkIcons.Define("clb-palette", 1.9f,
                "fill:#FFE066 M12 3.5c-5 0-8.5 3.6-8.5 8.2 0 4.4 3.4 8.8 8 8.8 1.6 0 2.3-.9 2.3-2 0-1.4-1.4-1.8-1.4-3 0-1 .8-1.6 2-1.6h2.3c2 0 3.8-1.5 3.8-3.7 0-3.9-3.6-6.7-8.5-6.7z",
                "bare fill:#2F6FD0 circle:8,11,1.4", "bare fill:#A8D1F7 circle:11.3,7.4,1.4", "bare ink circle:15.6,8.2,1.4");
            InkIcons.Define("clb-mask", 1.8f, "fill:#FFF3C4 M5 5.5c4.6-1.6 9.4-1.6 14 0v5.5c0 4.6-3.1 8.5-7 8.5s-7-3.9-7-8.5z",
                "M8.3 10.2c.8-.8 2-.8 2.8 0M12.9 10.2c.8-.8 2-.8 2.8 0", "M8.8 14c1.8 2 4.6 2 6.4 0");
            InkIcons.Define("clb-debate", 1.8f, "fill:#FBFDFF M3 4.5h11v8H8.5l-3 2.5v-2.5H3z", "fill:#A8D1F7 M10.5 10h10.5v8h-2v2.5l-3-2.5h-5.5z");
            InkIcons.Define("clb-gamepad", 1.8f,
                "fill:#A8D1F7 M7 8h10a4.5 4.5 0 0 1 4.5 4.5c0 3-1 5.5-3 5.5-1.6 0-2.4-2-4-2h-5c-1.6 0-2.4 2-4 2-2 0-3-2.5-3-5.5A4.5 4.5 0 0 1 7 8z",
                "M7.5 11v3.4M5.8 12.7h3.4", "bare ink circle:15.6,11.6,1.1", "bare ink circle:17.8,13.6,1.1");
            InkIcons.Define("clb-flask", 1.8f, "fill:#DCEBFB M10.5 4h3v5.2l5.2 8.9a1.7 1.7 0 0 1-1.5 2.6H6.8a1.7 1.7 0 0 1-1.5-2.6l5.2-8.9z",
                "M9.2 4h5.6", "M7.4 15.4c1.5-.9 3-.9 4.6 0s3.1.9 4.6 0");
            InkIcons.Define("clb-ballot", 1.8f, "fill:#FFE066 M9.5 3.5h5v8.5h-5z", "M10.8 7.2l1 1 1.8-2.2", "fill:#FBFDFF rect:4,11,16,9.5,1.5", "M8.5 14h7");
            InkIcons.Define("clb-medal", 1.7f, "fill:#A8D1F7 M8 2.5h3l2 6h-3zM16 2.5h-3l-2 6h3z", "fill:#FFE066 circle:12,15,5.5", "M12 12.6v4.8");
        }
    }
}
