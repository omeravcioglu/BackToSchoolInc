using System;
using System.Collections.Generic;
using UnityEngine;

namespace SchoolSim.Game
{
    public sealed partial class DebugUI
    {
        static readonly string[] CampusPages = { "Rooms", "Build" };
        static readonly string[] ScholarshipNames = Enum.GetNames(typeof(ScholarshipType));

        Vector2 _campusScroll, _admissionsScroll;
        int _campusPage;
        int _selectedRoom = -1;
        int _offerApplicant = -1;
        int _offerType;
        float _offerDiscount = 0.25f;

        // ================================================================================
        // Rooms (list view of the campus)
        // ================================================================================

        string LandSummary()
        {
            var map = S.Campus;
            return $"Land: {map.OwnedParcelCount} parcels · {map.UsedTiles} of {map.OwnedTiles} tiles built on";
        }

        void DrawRoomsTab()
        {
            GUILayout.BeginHorizontal();
            int page = GUILayout.Toolbar(_campusPage, CampusPages, GUILayout.Width(200));
            if (page != _campusPage) Defer(() => _campusPage = page);
            GUILayout.Space(20);
            GUILayout.Label(LandSummary(), GUILayout.Width(360));
            if (GUILayout.Button("Buy land on the map", GUILayout.Width(180)))
                Defer(() => { _tab = Tab.Campus; CampusController.Instance?.BeginLand(); });
            GUILayout.EndHorizontal();

            _campusScroll = GUILayout.BeginScrollView(_campusScroll);
            if (_campusPage == 0) DrawRooms();
            else DrawBuildMenu();
            GUILayout.EndScrollView();
        }

        void DrawRooms()
        {
            foreach (RoomCategory cat in Enum.GetValues(typeof(RoomCategory)))
            {
                var rooms = S.Rooms.FindAll(r => r.Def.Category == cat);
                if (rooms.Count == 0) continue;
                Section(cat.ToString());
                foreach (var r in rooms) DrawRoomRow(r);
            }
        }

        void DrawRoomRow(Room r)
        {
            var room = r;
            GUILayout.BeginVertical(_panel);
            GUILayout.BeginHorizontal();
            string status = !r.IsBuilt(S.Day) ? $"under construction until {Calendar.Format(r.ConstructionDoneDay)}"
                : r.IsUpgrading ? $"upgrading to L{r.PendingLevel} until {Calendar.Format(r.ConstructionDoneDay)}"
                : r.IsClosed(S.Day) ? $"CLOSED until {Calendar.Format(r.ClosedUntilDay)}"
                : r.LevelName;
            if (GUILayout.Button($"<b>{r.Name}</b> L{r.Level}  <size=11>{status}</size>", _rowButton, GUILayout.Width(420)))
                Defer(() => _selectedRoom = _selectedRoom == room.Id ? -1 : room.Id);
            GUILayout.Label("Condition", _small, GUILayout.Width(58));
            Bar(r.Condition, 60);
            GUILayout.Label("Clean", _small, GUILayout.Width(36));
            Bar(r.Cleanliness, 60);
            GUILayout.Label(Usage(r), _small, GUILayout.Width(150));
            GUILayout.Label($"{Money(r.Upkeep)}/mo", _small, GUILayout.Width(70));
            GUILayout.EndHorizontal();

            if (_selectedRoom == r.Id)
            {
                DrawRoomDetails(r, wide: true);
                if (GUILayout.Button("Show on map", GUILayout.Width(140)))
                    Defer(() => { _tab = Tab.Campus; CampusController.Instance?.Select(room.Id); FocusOn(room); });
            }
            GUILayout.EndVertical();
        }

        /// <summary>Room description, level ladder, residents and the upgrade/repair/demolish actions.</summary>
        void DrawRoomDetails(Room r, bool wide)
        {
            var room = r;
            GUILayout.Label(r.Def.Description, _small);
            GUILayout.Label($"Quality now {r.Quality:0} (base {r.BaseQuality:0}, reduced by dirt and wear). Upkeep {Money(r.Upkeep)}/mo.", _small);
            for (int lvl = 1; lvl <= r.Def.MaxLevel; lvl++)
            {
                string fee = r.Type == RoomType.DormRoom ? $" · fee {Money(r.Def.MonthlyFee[lvl - 1])}/mo" : "";
                var color = lvl == r.Level ? Good : lvl < r.Level ? Dim : Color.white;
                Colored($"L{lvl}: {r.Def.LevelNames[lvl - 1]} · {r.Def.CapacityLabel} {r.Def.Capacity[lvl - 1]}{fee}", color, _small);
            }
            if (r.Type == RoomType.DormRoom)
            {
                var residents = Sim.Indices.ResidentsOf(r.Id);
                GUILayout.Label("Residents: " + (residents.Count == 0 ? "none" : string.Join(", ", residents.ConvertAll(s => $"{s.FullName} ({s.Happiness:0})"))), _small);
            }

            Sim.Facilities.CanUpgrade(r, out string whyUp);
            string upLabel = r.Level < r.Def.MaxLevel ? $"Upgrade to L{r.Level + 1} ({Money(Sim.Facilities.UpgradeCost(r))})" : "Max level";
            float repair = Sim.Facilities.RepairCost(r);
            string whyRepair = repair < 1f ? "Nothing to repair" : S.Finance.Cash < repair ? "Not enough money" : null;
            if (wide) GUILayout.BeginHorizontal();
            if (ActionButton(upLabel, whyUp, GUILayout.Width(wide ? 240 : 300))) Run(() => Sim.UpgradeRoom(room.Id));
            if (ActionButton($"Repair ({Money(repair)})", whyRepair, GUILayout.Width(wide ? 150 : 300))) Run(() => Sim.RepairRoom(room.Id));
            if (ConfirmButton("Demolish", "demolish" + r.Id, GUILayout.Width(wide ? 90 : 300))) Run(() => Sim.DemolishRoom(room.Id));
            if (wide) GUILayout.EndHorizontal();
            if (whyUp != null && r.Level < r.Def.MaxLevel) Colored(whyUp, Warn, _small);
        }

        string Usage(Room r)
        {
            switch (r.Type)
            {
                case RoomType.DormRoom: return $"{Sim.Indices.ResidentsOf(r.Id).Count}/{r.Capacity} beds";
                case RoomType.Classroom: return $"{r.Capacity} seats";
                case RoomType.Cafeteria: return $"{r.Capacity} meals/sitting";
                default: return $"{r.Def.CapacityLabel}: {r.Capacity}";
            }
        }

        void DrawBuildMenu()
        {
            foreach (RoomCategory cat in Enum.GetValues(typeof(RoomCategory)))
            {
                Section(cat.ToString());
                foreach (var def in RoomCatalog.All)
                {
                    if (def.Category != cat) continue;
                    var type = def.Type;
                    Sim.Facilities.CanBuild(def.Type, out string why);
                    int owned = S.Rooms.FindAll(r => r.Type == type).Count;
                    GUILayout.BeginHorizontal(_panel);
                    GUILayout.BeginVertical(GUILayout.Width(520));
                    GUILayout.Label($"<b>{def.Name}</b>{(owned > 0 ? $"  <size=11>(you have {owned})</size>" : "")}", _wrap);
                    GUILayout.Label(def.Description, _small);
                    string fee = def.Type == RoomType.DormRoom ? $" · earns {Money(def.MonthlyFee[0])}/bed/mo" : "";
                    GUILayout.Label($"L1: {def.LevelNames[0]} · {def.CapacityLabel} {def.Capacity[0]} · upkeep {Money(def.Upkeep[0])}/mo · {def.Width}x{def.Height} tiles · {def.BuildDays[0]} days{fee}", _small);
                    GUILayout.EndVertical();
                    if (ActionButton($"Place on map ({Money(def.Cost[0])})", why, GUILayout.Width(190), GUILayout.Height(36)))
                        Defer(() => { _tab = Tab.Campus; CampusController.Instance?.BeginBuild(type); });
                    if (ActionButton("Auto-place", why, GUILayout.Width(90), GUILayout.Height(36))) Run(() => Sim.BuildRoom(type));
                    if (why != null) GUILayout.Label(why, _small, GUILayout.Width(200));
                    GUILayout.EndHorizontal();
                }
            }
        }

        // ================================================================================
        // Admissions
        // ================================================================================

        void DrawAdmissions()
        {
            int free = Sim.Facilities.FreeBeds();
            bool season = Calendar.IsAdmissionsSeason(S.Day);
            GUILayout.Label("Admissions", _h2);
            GUILayout.Label(season
                ? $"Admissions Season is open until the school year starts. {free} free bed(s)."
                : $"Admissions Season opens in July. Transfer applications arrive during the year (about {Sim.Admissions.TransferRate():0.0}/month right now). {free} free bed(s).", _wrap);
            float tuition = S.Policies.PendingTuition > 0 ? S.Policies.PendingTuition : S.Policies.AnnualTuition;
            GUILayout.Label($"Expected applicants next season: ~{Sim.Admissions.ExpectedApplicants(tuition):0} (prestige {S.Prestige:0}, tuition {Money(tuition)} vs fair {Money(Sim.FairTuition)}).", _small);

            GUILayout.BeginHorizontal();
            string whyMarketing = Sim.Admissions.MarketingActive ? $"Campaign running until {Calendar.Format(S.MarketingUntilDay)}"
                : S.Finance.Cash < Balance.MarketingCost ? "Not enough money" : null;
            if (ActionButton($"Run a marketing campaign ({Money(Balance.MarketingCost)})", whyMarketing, GUILayout.Width(300))) Run(Sim.RunMarketing);
            GUILayout.Label("More transfer applications for 30 days, and a bigger pool next season.", _small);
            GUILayout.EndHorizontal();

            _admissionsScroll = GUILayout.BeginScrollView(_admissionsScroll);
            if (S.Applicants.Count == 0) GUILayout.Label("No applications right now.", _small);

            var list = new List<Applicant>(S.Applicants);
            list.Sort((a, b) => Sim.Admissions.Score(b).CompareTo(Sim.Admissions.Score(a)));
            foreach (var a in list) DrawApplicant(a, free);
            GUILayout.EndScrollView();
        }

        void DrawApplicant(Applicant a, int freeBeds)
        {
            var s = a.Student;
            var app = a;
            GUILayout.BeginVertical(_panel);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{s.FullName}</b> · grade {s.Grade}{(a.Transfer ? " · transfer" : "")} · <i>{a.Pitch}</i>", _wrap, GUILayout.Width(520));
            GUILayout.Label($"Expires {Calendar.Format(a.ExpiresDay)}", _small);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Academic {s.Academic:0} · Athletic {s.Athletic:0} · Artistic {s.Artistic:0} · Discipline {s.Discipline:0} · Social {s.Social:0} · Family wealth {s.FamilyWealth:0}" +
                            (s.SportTalent != SportType.None ? $" · {s.SportTalent} talent" : ""), _small);
            GUILayout.Label($"Traits: {string.Join(", ", s.Traits)} · Requests {(a.RequestedAid > 0 ? a.RequestedAid.ToString("P0") + " aid" : "no aid")} · " +
                            $"Chance to accept a full-price offer: {Sim.Admissions.AcceptChance(a, 0f, ScholarshipType.None):P0}", _small);

            string noBed = freeBeds <= 0 ? "No free beds" : null;
            GUILayout.BeginHorizontal();
            if (ActionButton("Offer (full price)", noBed, GUILayout.Width(150))) Run(() => Sim.Admit(app, ScholarshipType.None, 0f));
            if (a.RequestedAid > 0)
            {
                var aidType = Sim.IsUnlocked(Feature.Scholarships) ? ScholarshipType.FinancialAid : ScholarshipType.None;
                if (ActionButton($"Offer with {a.RequestedAid:P0} aid ({Sim.Admissions.AcceptChance(a, a.RequestedAid, aidType):P0})", noBed, GUILayout.Width(230)))
                    Run(() => Sim.Admit(app, aidType, app.RequestedAid));
            }
            if (GUILayout.Button("Custom offer...", GUILayout.Width(120)))
                Defer(() => _offerApplicant = _offerApplicant == s.Id ? -1 : s.Id);
            if (GUILayout.Button("Reject", GUILayout.Width(80))) Run(() => Sim.RejectApplicant(app));
            GUILayout.EndHorizontal();

            if (_offerApplicant == s.Id)
            {
                GUILayout.BeginHorizontal();
                int type = GUILayout.Toolbar(_offerType, ScholarshipNames, GUILayout.Width(420));
                if (type != _offerType) Defer(() => _offerType = type);
                GUILayout.Label($"Discount {_offerDiscount:P0}", GUILayout.Width(100));
                float d = GUILayout.HorizontalSlider(_offerDiscount, 0f, 1f, GUILayout.Width(150));
                d = Mathf.Round(d * 20f) / 20f;
                if (Math.Abs(d - _offerDiscount) > 0.001f) Defer(() => _offerDiscount = d);
                GUILayout.EndHorizontal();
                var chosen = (ScholarshipType)_offerType;
                Sim.Admissions.CanAdmit(a, chosen, out string why);
                float chance = Sim.Admissions.AcceptChance(a, _offerDiscount, chosen);
                float yearly = S.Policies.AnnualTuition * _offerDiscount;
                GUILayout.BeginHorizontal();
                if (ActionButton($"Make offer: {chosen} {_offerDiscount:P0} (accept chance {chance:P0}, costs {Money(yearly)}/yr)", why, GUILayout.Width(520)))
                {
                    float discount = _offerDiscount;
                    Run(() => Sim.Admit(app, chosen, discount));
                    Defer(() => _offerApplicant = -1);
                }
                if (why != null) GUILayout.Label(why, _small);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
    }
}
