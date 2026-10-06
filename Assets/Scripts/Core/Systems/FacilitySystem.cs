using System;
using System.Collections.Generic;

namespace SchoolSim
{
    public enum PlacementIssue
    {
        None,
        OutOfBounds,
        NotOwned,
        Overlap,
        DoorBlocked,
        DoorNotOwned,
        BlocksDoor,
    }

    /// <summary>
    /// Construction and placement on the campus grid, land, upgrades, cleaning, wear and tear,
    /// and dorm bed assignment.
    /// </summary>
    public sealed class FacilitySystem
    {
        readonly Simulation _sim;

        public FacilitySystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;
        SchoolIndices Ix => _sim.Indices;
        CampusMap Map => _sim.State.Campus;

        public void DailyTick()
        {
            int day = S.Day;
            CompleteConstruction(day);
            HouseDisplacedStudents();
            Decay(day);
            Clean(day);
            Maintain(day);
        }

        void CompleteConstruction(int day)
        {
            foreach (var r in S.Rooms)
            {
                if (r.PendingLevel > 0 && day >= r.ConstructionDoneDay)
                {
                    r.Level = r.PendingLevel;
                    r.PendingLevel = 0;
                    r.Condition = 100f;
                    r.Cleanliness = Math.Max(r.Cleanliness, 90f);
                    Map.Touch();
                    _sim.Log($"{r.Name} upgraded to level {r.Level}: {r.LevelName}.");
                }
                else if (r.PendingLevel == 0 && day == r.ConstructionDoneDay && day > 0)
                {
                    Map.Touch();
                    _sim.Log($"Construction finished: {r.Name} is open.", important: true);
                    Opening(r);
                }

                if (r.ClosedUntilDay == day)
                {
                    Map.Touch();
                    _sim.Log($"{r.Name} has reopened.");
                }
            }
        }

        /// <summary>A showpiece building opening makes the local paper (a signature building, the front page).</summary>
        void Opening(Room r)
        {
            if (r.Def.Signature != Specialization.None)
            {
                S.Year.Highlight($"Opened the {r.Def.Name}");
                _sim.News.Publish(NewsTone.Good, NewsTopic.School, $"{S.SchoolName} opens its {r.Def.Name}",
                    $"Ribbon cut and speeches made: the new {r.Def.Name} is the pride of {S.SchoolName}, a "
                    + $"{Names.Specialization(r.Def.Signature).ToLowerInvariant()} through and through.",
                    2f, ReputationId.Overall, 120, NewsSystem.Courier);
                return;
            }
            ReputationId rep;
            NewsTopic topic;
            switch (r.Def.Category)
            {
                case RoomCategory.Academic when r.Type != RoomType.Classroom:
                    rep = ReputationId.Academic; topic = NewsTopic.Academics; break;
                case RoomCategory.StudentLife:
                    rep = ReputationId.StudentLife; topic = NewsTopic.StudentLife; break;
                case RoomCategory.Sports:
                    rep = ReputationId.Sports; topic = NewsTopic.Sports; break;
                case RoomCategory.Health:
                    rep = ReputationId.Safety; topic = NewsTopic.Safety; break;
                default:
                    return;   // dorms, classrooms and kitchens are expected, not news
            }
            string what = r.Def.Name.ToLowerInvariant();
            _sim.News.Publish(NewsTone.Good, topic, $"{S.SchoolName} opens a new {what}",
                $"Ribbon cut, cake eaten: students at {S.SchoolName} can now use their brand-new {what}.",
                1f, rep, 60, NewsSystem.Courier);
        }

        /// <summary>The housing office gives any student without a bed the best free one.</summary>
        void HouseDisplacedStudents()
        {
            foreach (var s in S.Students)
                if (s.DormRoomId < 0 && AutoAssignBed(s))
                    s.Remember(S.Day, $"Given a bed in {S.FindRoom(s.DormRoomId)?.Name}.");
        }

        void Decay(int day)
        {
            float session = _sim.InSession ? 1f : 0.25f;
            float students = Ix.Students;
            float sharedBathLoad = Ix.BathroomShortage > 0 ? 1.5f : 1f;
            float wear = AssistantRules.WearMultiplier(S.Assistant);

            foreach (var r in S.Rooms)
            {
                if (!r.IsOperational(day)) continue;
                if (r.Def.Decor)
                {
                    r.Condition = MathUtil.Clamp100(r.Condition - 0.02f * session * wear);   // weather, mostly
                    continue;
                }
                float usage;
                switch (r.Type)
                {
                    case RoomType.DormRoom:
                    {
                        var residents = Ix.ResidentsOf(r.Id);
                        if (residents.Count == 0) { usage = 0.05f; break; }
                        int messy = 0;
                        foreach (var s in residents) if (s.Has(StudentTrait.Messy)) messy++;
                        usage = residents.Count / (float)r.Capacity * (1f + 0.6f * messy / residents.Count);
                        break;
                    }
                    case RoomType.Bathroom: usage = sharedBathLoad; break;
                    case RoomType.Classroom: usage = Ix.ClassroomSeats == 0 ? 0f : Math.Min(1.5f, students / Ix.ClassroomSeats); break;
                    case RoomType.Cafeteria: usage = Ix.MealCapacity <= 0 ? 0f : Math.Min(2f, students / Ix.MealCapacity); break;
                    default: usage = students == 0 ? 0.1f : 0.7f; break;
                }

                r.Cleanliness = MathUtil.Clamp100(r.Cleanliness - r.Def.CleanlinessDecay * usage * session);
                r.Condition = MathUtil.Clamp100(r.Condition - (0.03f + 0.035f * usage) * session * wear);
            }
        }

        /// <summary>Relative effort to clean or maintain a room, from its floor area.</summary>
        static float Effort(Room r) => r.Def.Area / 15f;

        void Clean(int day)
        {
            float capacity = 0f;
            foreach (var s in S.Staff)
                if (s.Role == StaffRole.Custodian && s.IsActive(day))
                    capacity += (16f + s.Skill * 0.2f) * (0.6f + 0.4f * s.Energy / 100f);
            if (capacity <= 0f) return;

            var rooms = S.Rooms.FindAll(r => r.IsOperational(day) && r.Cleanliness < 99f && !r.Def.Decor);
            rooms.Sort((a, b) => a.Cleanliness.CompareTo(b.Cleanliness));
            foreach (var r in rooms)
            {
                if (capacity <= 0f) break;
                float effort = Effort(r);
                float gain = Math.Min(Math.Min(100f - r.Cleanliness, 20f), capacity / effort);
                r.Cleanliness += gain;
                capacity -= gain * effort;
            }
        }

        void Maintain(int day)
        {
            float capacity = 0f;
            foreach (var s in S.Staff)
                if (s.Role == StaffRole.Maintenance && s.IsActive(day))
                    capacity += (6f + s.Skill * 0.08f) * (0.6f + 0.4f * s.Energy / 100f);
            if (capacity <= 0f) return;

            var rooms = S.Rooms.FindAll(r => r.IsBuilt(day) && r.Condition < 92f);
            rooms.Sort((a, b) => a.Condition.CompareTo(b.Condition));
            foreach (var r in rooms)
            {
                if (capacity <= 0f) break;
                float effort = Effort(r);
                float gain = Math.Min(Math.Min(100f - r.Condition, 8f), capacity / effort);
                r.Condition += gain;
                capacity -= gain * effort;
            }
        }

        // ------------------------------------------------------------------------------
        // Placement
        // ------------------------------------------------------------------------------

        /// <summary>
        /// Checks a footprint: inside owned land, not overlapping another room, the tile in front
        /// of the door free and owned, and not covering another room's door front.
        /// </summary>
        public PlacementIssue CheckPlacement(RoomDefinition def, int x, int y, int rotation, int ignoreRoomId, out Room other)
        {
            other = null;
            var f = Placement.Footprint(def, x, y, rotation);
            for (int ty = f.Y; ty < f.YMax; ty++)
            {
                for (int tx = f.X; tx < f.XMax; tx++)
                {
                    if (!Map.InBounds(tx, ty)) return PlacementIssue.OutOfBounds;
                    if (!Map.IsOwned(tx, ty)) return PlacementIssue.NotOwned;
                    int id = Map.RoomIdAt(tx, ty);
                    if (id != 0 && id != ignoreRoomId)
                    {
                        other = S.FindRoom(id);
                        return PlacementIssue.Overlap;
                    }
                    int door = Map.DoorFrontOwnerAt(tx, ty);
                    if (door != 0 && door != ignoreRoomId)
                    {
                        other = S.FindRoom(door);
                        return PlacementIssue.BlocksDoor;
                    }
                }
            }

            if (def.Decor) return PlacementIssue.None;   // no door to keep clear
            Placement.DoorFront(f, rotation, out int fx, out int fy);
            if (!Map.InBounds(fx, fy)) return PlacementIssue.DoorBlocked;
            int front = Map.RoomIdAt(fx, fy);
            if (front != 0 && front != ignoreRoomId)
            {
                other = S.FindRoom(front);
                return PlacementIssue.DoorBlocked;
            }
            if (!Map.IsOwned(fx, fy)) return PlacementIssue.DoorNotOwned;
            return PlacementIssue.None;
        }

        public static string Describe(PlacementIssue issue, Room other) => issue switch
        {
            PlacementIssue.None => null,
            PlacementIssue.OutOfBounds => "That's off the edge of the campus map.",
            PlacementIssue.NotOwned => "You don't own this land yet. Buy the parcel first.",
            PlacementIssue.Overlap => $"Overlaps {other?.Name ?? "another room"}.",
            PlacementIssue.DoorBlocked => other != null ? $"The door would open into {other.Name}." : "The door would open off the map.",
            PlacementIssue.DoorNotOwned => "The door must open onto land you own.",
            PlacementIssue.BlocksDoor => $"Would block the door of {other?.Name ?? "another room"}.",
            _ => "Can't build here.",
        };

        /// <summary>Null when the room fits here, otherwise the reason it doesn't.</summary>
        public string WhyCantPlace(RoomType type, int x, int y, int rotation, int ignoreRoomId = 0)
        {
            var issue = CheckPlacement(RoomCatalog.Get(type), x, y, rotation, ignoreRoomId, out var other);
            return Describe(issue, other);
        }

        /// <summary>
        /// Finds a good spot for a room: valid, close to the existing buildings, and sharing walls
        /// with them where possible. Used by auto-placement and the admin tools.
        /// </summary>
        public bool TryFindPlacement(RoomType type, out int bestX, out int bestY, out int bestRotation)
        {
            var def = RoomCatalog.Get(type);
            bestX = bestY = bestRotation = 0;
            float bestScore = float.MinValue;

            GetCampusCenter(out float cx, out float cy);
            for (int rot = 0; rot < 4; rot++)
            {
                var probe = Placement.Footprint(def, 0, 0, rot);
                for (int y = 0; y <= Map.Height - probe.H; y++)
                {
                    for (int x = 0; x <= Map.Width - probe.W; x++)
                    {
                        if (!Map.IsOwned(x, y) || !Map.IsOwned(x + probe.W - 1, y + probe.H - 1)) continue;
                        if (CheckPlacement(def, x, y, rot, 0, out _) != PlacementIssue.None) continue;

                        float score = SharedWalls(x, y, probe.W, probe.H) * 3f;
                        float dx = x + probe.W / 2f - cx, dy = y + probe.H / 2f - cy;
                        score -= (float)Math.Sqrt(dx * dx + dy * dy) * 0.6f;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestX = x;
                            bestY = y;
                            bestRotation = rot;
                        }
                    }
                }
            }
            return bestScore > float.MinValue;
        }

        int SharedWalls(int x, int y, int w, int h)
        {
            int n = 0;
            for (int i = 0; i < w; i++)
            {
                if (Map.RoomIdAt(x + i, y - 1) != 0) n++;
                if (Map.RoomIdAt(x + i, y + h) != 0) n++;
            }
            for (int j = 0; j < h; j++)
            {
                if (Map.RoomIdAt(x - 1, y + j) != 0) n++;
                if (Map.RoomIdAt(x + w, y + j) != 0) n++;
            }
            return n;
        }

        /// <summary>Centre of the existing buildings, or of the owned land if there are none.</summary>
        public void GetCampusCenter(out float cx, out float cy)
        {
            float sx = 0f, sy = 0f;
            int n = 0;
            foreach (var r in S.Rooms)
            {
                var f = r.Footprint;
                sx += f.X + f.W / 2f;
                sy += f.Y + f.H / 2f;
                n++;
            }
            if (n > 0)
            {
                cx = sx / n;
                cy = sy / n;
                return;
            }
            for (int py = 0; py < Map.ParcelsY; py++)
                for (int px = 0; px < Map.ParcelsX; px++)
                    if (Map.IsParcelOwned(px, py))
                    {
                        sx += (px + 0.5f) * CampusMap.ParcelSize;
                        sy += (py + 0.5f) * CampusMap.ParcelSize;
                        n++;
                    }
            cx = n > 0 ? sx / n : Map.Width / 2f;
            cy = n > 0 ? sy / n : Map.Height / 2f;
        }

        // ------------------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------------------

        /// <summary>Level and money requirements, independent of where the room goes.</summary>
        public bool CanBuild(RoomType type, out string reason)
        {
            var def = RoomCatalog.Get(type);
            if (!def.Buildable)
            {
                reason = "Comes with the old estate";
                return false;
            }
            if (S.Progress.SchoolLevel < def.MinSchoolLevel)
            {
                reason = $"Requires school level {def.MinSchoolLevel}";
                return false;
            }
            if (def.Signature != Specialization.None && S.Specialization != def.Signature)
            {
                reason = $"Only a {Names.Specialization(def.Signature)} builds one";
                return false;
            }
            if (S.Finance.Cash < def.Cost[0])
            {
                reason = "Not enough money";
                return false;
            }
            reason = null;
            return true;
        }

        public bool CanBuildAt(RoomType type, int x, int y, int rotation, out string reason)
        {
            if (!CanBuild(type, out reason)) return false;
            reason = WhyCantPlace(type, x, y, rotation);
            return reason == null;
        }

        public Room Build(RoomType type, int x, int y, int rotation)
        {
            if (!CanBuildAt(type, x, y, rotation, out _)) return null;
            var def = RoomCatalog.Get(type);
            int count = 0;
            foreach (var r in S.Rooms) if (r.Type == type) count++;

            var room = new Room
            {
                Id = S.NewId(),
                Type = type,
                Name = count == 0 && def.Type != RoomType.DormRoom && def.Type != RoomType.Classroom
                    ? def.Name
                    : $"{def.Name} {count + 1}",
                Level = 1,
                X = x,
                Y = y,
                Rotation = rotation & 3,
                ConstructionStartDay = S.Day,
                ConstructionDoneDay = S.Day + def.BuildDays[0],
                Cleanliness = 95f,
                Condition = 100f,
            };
            S.Rooms.Add(room);
            Map.RebuildOccupancy(S.Rooms);
            S.Finance.Post(S.Day, LedgerCategory.Construction, -def.Cost[0], "Built " + room.Name);
            int days = def.BuildDays[0];
            _sim.Log($"Construction started: {room.Name} (ready in {days} day{(days == 1 ? "" : "s")}).");
            return room;
        }

        /// <summary>Builds at the best available spot, or returns null with a reason.</summary>
        public Room BuildAuto(RoomType type, out string reason)
        {
            if (!CanBuild(type, out reason)) return null;
            if (!TryFindPlacement(type, out int x, out int y, out int rot))
            {
                reason = $"No room for a {RoomCatalog.Get(type).Name} on your land. Buy more land.";
                return null;
            }
            return Build(type, x, y, rot);
        }

        public float UpgradeCost(Room room) => room.Level < room.Def.MaxLevel ? room.Def.Cost[room.Level] : 0f;

        public bool CanUpgrade(Room room, out string reason)
        {
            var def = room.Def;
            if (room.Level >= def.MaxLevel) { reason = "Already at max level"; return false; }
            if (!room.IsBuilt(S.Day)) { reason = "Still under construction"; return false; }
            if (room.IsUpgrading) { reason = "Upgrade already in progress"; return false; }
            int next = room.Level + 1;
            if (next >= def.GatedFromLevel && !_sim.IsUnlocked(def.GateFeature))
            {
                reason = $"Level {next} requires {def.GateFeature} (school level {Balance.UnlockLevel(def.GateFeature)})";
                return false;
            }
            if (room.Type == RoomType.DormRoom)
            {
                int residents = Ix.ResidentsOf(room.Id).Count;
                int newCap = def.Capacity[room.Level];
                if (residents > newCap)
                {
                    int extra = residents - newCap;
                    reason = $"Level {next} has only {newCap} beds. Move {extra} student{(extra == 1 ? "" : "s")} out first.";
                    return false;
                }
            }
            if (S.Finance.Cash < def.Cost[room.Level]) { reason = "Not enough money"; return false; }
            reason = null;
            return true;
        }

        public bool Upgrade(Room room)
        {
            if (!CanUpgrade(room, out _)) return false;
            float cost = room.Def.Cost[room.Level];
            room.PendingLevel = room.Level + 1;
            room.ConstructionStartDay = S.Day;
            room.ConstructionDoneDay = S.Day + room.Def.BuildDays[room.Level];
            S.Finance.Post(S.Day, LedgerCategory.Construction, -cost, $"Upgraded {room.Name} to L{room.PendingLevel}");
            Map.Touch();
            _sim.Log($"Upgrade started: {room.Name} → level {room.PendingLevel}.");
            return true;
        }

        public float RepairCost(Room room)
        {
            return (100f - room.Condition) * Balance.RepairCostPerConditionPoint * (1f + 0.25f * (room.Level - 1)) * room.Def.Area / 20f
                   * AssistantRules.RepairMultiplier(S.Assistant);
        }

        public bool Repair(Room room)
        {
            float cost = RepairCost(room);
            if (cost < 1f || S.Finance.Cash < cost) return false;
            S.Finance.Post(S.Day, LedgerCategory.Upkeep, -cost, "Repaired " + room.Name);
            room.Condition = 100f;
            Map.Touch();
            return true;
        }

        public bool Demolish(Room room, out string reason)
        {
            if (room.Type == RoomType.DormRoom && Ix.ResidentsOf(room.Id).Count > 0)
            {
                reason = "Move all residents out first";
                return false;
            }
            if (!room.Def.Buildable)
            {
                reason = $"The {room.Def.Name.ToLowerInvariant()} is a listed building";
                return false;
            }
            S.Rooms.Remove(room);
            Map.RebuildOccupancy(S.Rooms);
            _sim.Log($"{room.Name} was demolished.");
            reason = null;
            return true;
        }

        // ------------------------------------------------------------------------------
        // Land
        // ------------------------------------------------------------------------------

        /// <summary>Price of the next parcel on the main campus; each purchase makes the next one dearer.</summary>
        public float LandCost => 25000f * (float)Math.Pow(1.35, S.LandPurchases);

        /// <summary>Price of the next parcel of the old estate, which climbs more gently.</summary>
        public float EstateLandCost => Balance.EstateParcelCost * (float)Math.Pow(Balance.EstateParcelGrowth, S.EstatePurchases);

        public float ParcelCost(int px, int py) => Map.IsEstateParcel(px, py) ? EstateLandCost : LandCost;

        public bool CanBuyParcel(int px, int py, out string reason)
        {
            if (!Map.ParcelInBounds(px, py)) { reason = "Outside the map"; return false; }
            if (Map.IsParcelOwned(px, py)) { reason = "You already own this land"; return false; }
            if (Map.IsEstateParcel(px, py) && !Map.EstateOpen) { reason = "The old estate isn't for sale yet"; return false; }
            if (!Map.IsParcelPurchasable(px, py)) { reason = "Only land next to your campus is for sale"; return false; }
            if (S.Finance.Cash < ParcelCost(px, py)) { reason = "Not enough money"; return false; }
            reason = null;
            return true;
        }

        public bool BuyParcel(int px, int py)
        {
            if (!CanBuyParcel(px, py, out _)) return false;
            float cost = ParcelCost(px, py);
            bool estate = Map.IsEstateParcel(px, py);
            S.Finance.Post(S.Day, LedgerCategory.Construction, -cost, estate ? "Bought estate land" : "Bought land");
            if (estate) S.EstatePurchases++;
            else S.LandPurchases++;
            Map.SetParcelOwned(px, py, true);
            _sim.Log($"Bought a new parcel of {(estate ? "the old estate" : "land")} for {MathUtil.Money(cost)}.", important: true);
            return true;
        }

        /// <summary>The parcel <see cref="BuyLand"/> picks: the cheapest for sale, nearest the middle of the campus.</summary>
        public bool FindLandToBuy(out int bestX, out int bestY)
        {
            GetCampusCenter(out float cx, out float cy);
            bestX = bestY = -1;
            float bestCost = float.MaxValue, best = float.MaxValue;
            for (int py = 0; py < Map.ParcelsY; py++)
            {
                for (int px = 0; px < Map.ParcelsX; px++)
                {
                    if (!Map.IsParcelPurchasable(px, py)) continue;
                    float cost = ParcelCost(px, py);
                    float dx = (px + 0.5f) * CampusMap.ParcelSize - cx, dy = (py + 0.5f) * CampusMap.ParcelSize - cy;
                    float d = dx * dx + dy * dy;
                    if (cost < bestCost - 0.5f || (cost < bestCost + 0.5f && d < best))
                    {
                        bestCost = cost;
                        best = d;
                        bestX = px;
                        bestY = py;
                    }
                }
            }
            return bestX >= 0;
        }

        public bool BuyLand() => FindLandToBuy(out int px, out int py) && BuyParcel(px, py);

        // ------------------------------------------------------------------------------
        // The old estate (second campus)
        // ------------------------------------------------------------------------------

        public bool CanOpenEstate(out string reason)
        {
            if (Map.EstateOpen) { reason = "The old estate is already yours"; return false; }
            if (!_sim.IsUnlocked(Feature.SecondCampus))
            {
                reason = $"The old estate comes up for sale at school level {Balance.UnlockLevel(Feature.SecondCampus)}";
                return false;
            }
            if (S.Finance.Cash < Balance.EstateOpenCost) { reason = "Not enough money"; return false; }
            reason = null;
            return true;
        }

        /// <summary>
        /// Opens the old estate across the road: the school gets the manor and the drive up to it, and the
        /// rest of the estate goes on sale parcel by parcel.
        /// </summary>
        public bool OpenEstate()
        {
            if (!CanOpenEstate(out _)) return false;
            S.Finance.Post(S.Day, LedgerCategory.Construction, -Balance.EstateOpenCost, "Bought the old estate");
            Map.EstateOpen = true;
            int py = Map.ManorY / CampusMap.ParcelSize;
            Map.SetParcelOwned(Map.EstateParcelX, py, true);
            Map.SetParcelOwned(Map.ManorX / CampusMap.ParcelSize, py, true);
            var def = RoomCatalog.Get(RoomType.Manor);
            S.Rooms.Add(new Room
            {
                Id = S.NewId(),
                Type = RoomType.Manor,
                Name = def.Name,
                X = Map.ManorX,
                Y = Map.ManorY,
                Rotation = Placement.South,
                ConstructionStartDay = S.Day,
                ConstructionDoneDay = S.Day,
                Cleanliness = 45f,
                Condition = 55f,
            });
            Map.RebuildOccupancy(S.Rooms);

            _sim.Log("The old estate across the road is ours: a second campus, with the old manor at its heart.", important: true);
            S.Year.Highlight("Opened a second campus on the old estate");
            _sim.News.Publish(NewsTone.Good, NewsTopic.Campus, $"{S.SchoolName} opens a second campus",
                $"The old estate across the road, with its manor and ancient oaks, now belongs to {S.SchoolName}. " +
                "Locals hope the dusty old house will see dinners and concerts again.",
                2f, ReputationId.Overall, 180, NewsSystem.Courier);
            return true;
        }

        /// <summary>Closes a room. Dorm residents are moved to free beds elsewhere, or left without a bed.</summary>
        public void CloseRoom(Room room, int days, string reason)
        {
            room.ClosedUntilDay = S.Day + days;
            Map.Touch();
            _sim.Log($"{room.Name} closed for {days} days: {reason}", important: true);
            if (room.Type != RoomType.DormRoom) return;

            var displaced = new List<Student>(Ix.ResidentsOf(room.Id));
            foreach (var s in displaced)
            {
                s.DormRoomId = -1;
                s.Moodlets.Set("Displaced from dorm", -8f, S.Day + 14);
            }
            _sim.Stats.ComputeIndices();
            foreach (var s in displaced) AutoAssignBed(s);
            _sim.Stats.ComputeIndices();
        }

        // ------------------------------------------------------------------------------
        // Beds
        // ------------------------------------------------------------------------------

        /// <summary>Best dorm room with a free bed for this student, scored by roommate compatibility.</summary>
        public Room FindBestBed(Student student, int excludeRoomId = -1)
        {
            Room best = null;
            float bestScore = float.MinValue;
            foreach (var r in S.Rooms)
            {
                if (r.Type != RoomType.DormRoom || !r.IsOperational(S.Day) || r.Id == excludeRoomId) continue;
                var residents = Ix.ResidentsOf(r.Id);
                if (residents.Count >= r.Capacity) continue;

                float score = r.Quality * 0.05f;
                foreach (var other in residents)
                {
                    if (other.Id == student.Id) continue;
                    score += TraitRules.Compatibility(student, other, true) * 2f;
                    score += S.Relationships.Score(student.Id, other.Id) * 0.05f;
                    if (other.Grade == student.Grade) score += 0.5f;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = r;
                }
            }
            return best;
        }

        public bool AutoAssignBed(Student student)
        {
            var room = FindBestBed(student, student.DormRoomId);
            if (room == null) return false;
            AssignBed(student, room);
            return true;
        }

        public void AssignBed(Student student, Room room)
        {
            if (student.DormRoomId >= 0 && Ix.Residents.TryGetValue(student.DormRoomId, out var oldList)) oldList.Remove(student);
            student.DormRoomId = room.Id;
            if (!Ix.Residents.TryGetValue(room.Id, out var list))
            {
                list = new List<Student>();
                Ix.Residents[room.Id] = list;
            }
            if (!list.Contains(student)) list.Add(student);
            Map.Touch();
        }

        public int FreeBeds()
        {
            int free = 0;
            foreach (var r in S.Rooms)
                if (r.Type == RoomType.DormRoom && r.IsOperational(S.Day))
                    free += Math.Max(0, r.Capacity - Ix.ResidentsOf(r.Id).Count);
            return free;
        }
    }
}
