using System.Linq;
using NUnit.Framework;

namespace SchoolSim.Tests
{
    public class CampusTests
    {
        static Simulation NewSim() => NewGame.Create("Test School", 42);

        [Test]
        public void StartingLayout_IsValidAndOccupiesTheMap()
        {
            var sim = NewSim();
            var map = sim.State.Campus;
            Assert.AreEqual(4, map.OwnedParcelCount);
            foreach (var r in sim.State.Rooms)
            {
                var f = r.Footprint;
                Assert.AreEqual(r.Id, map.RoomIdAt(f.X, f.Y), r.Name);
                Assert.AreEqual(r.Id, map.RoomIdAt(f.XMax - 1, f.YMax - 1), r.Name);
                r.DoorFront(out int dx, out int dy);
                Assert.IsTrue(map.IsFree(dx, dy), $"{r.Name}'s door must open onto a free tile");
            }
        }

        [Test]
        public void Rotation_SwapsFootprintAndMovesTheDoor()
        {
            var dorm = RoomCatalog.Get(RoomType.DormRoom);
            var south = Placement.Footprint(dorm, 0, 0, Placement.South);
            var east = Placement.Footprint(dorm, 0, 0, Placement.East);
            Assert.AreEqual((4, 3), (south.W, south.H));
            Assert.AreEqual((3, 4), (east.W, east.H));
            Placement.DoorFront(south, Placement.South, out int sx, out int sy);
            Assert.AreEqual((2, -1), (sx, sy));
            Placement.DoorFront(east, Placement.East, out int ex, out int ey);
            Assert.AreEqual((3, 2), (ex, ey));
        }

        [Test]
        public void Placement_RejectsOverlapsUnownedLandAndBlockedDoors()
        {
            var sim = NewSim();
            var classroom = sim.State.Rooms.First(r => r.Type == RoomType.Classroom);
            StringAssert.Contains("Overlaps", sim.Facilities.WhyCantPlace(RoomType.DormRoom, classroom.X, classroom.Y, Placement.South));
            StringAssert.Contains("don't own", sim.Facilities.WhyCantPlace(RoomType.DormRoom, 1, 1, Placement.South));

            classroom.DoorFront(out int dx, out int dy);
            string blocked = sim.Facilities.WhyCantPlace(RoomType.Bathroom, dx - 1, dy - 2, Placement.South);
            StringAssert.Contains("block the door", blocked);
        }

        [Test]
        public void AutoPlacement_BuildsOnFreeLandAndUpdatesOccupancy()
        {
            var sim = NewSim();
            int before = sim.State.Campus.UsedTiles;
            var result = sim.BuildRoom(RoomType.DormRoom);
            Assert.IsTrue(result.Ok, result.Message);
            var room = sim.State.Rooms.Last();
            Assert.AreEqual(room.Id, sim.State.Campus.RoomIdAt(room.X, room.Y));
            Assert.AreEqual(before + room.Def.Area, sim.State.Campus.UsedTiles);
            Assert.IsFalse(room.IsBuilt(sim.Day), "new rooms start as construction sites");
        }

        [Test]
        public void Land_OnlyParcelsNextToTheCampusAreForSale_AndPricesRise()
        {
            var sim = NewSim();
            var map = sim.State.Campus;
            Assert.IsFalse(map.IsParcelPurchasable(0, 0));
            Assert.IsTrue(map.IsParcelPurchasable(NewGame.StartParcelX - 1, NewGame.StartParcelY));

            float price = sim.Facilities.LandCost;
            float cash = sim.State.Finance.Cash;
            var result = sim.BuyParcel(NewGame.StartParcelX - 1, NewGame.StartParcelY);
            Assert.IsTrue(result.Ok, result.Message);
            Assert.IsTrue(map.IsParcelOwned(NewGame.StartParcelX - 1, NewGame.StartParcelY));
            Assert.AreEqual(cash - price, sim.State.Finance.Cash, 0.01f);
            Assert.Greater(sim.Facilities.LandCost, price);
            Assert.IsFalse(sim.BuyParcel(0, 4).Ok, "not adjacent to the campus");
        }

        [Test]
        public void Demolishing_FreesTheTiles()
        {
            var sim = NewSim();
            var office = sim.State.Rooms.First(r => r.Type == RoomType.Office);
            Assert.IsTrue(sim.DemolishRoom(office.Id).Ok);
            Assert.IsTrue(sim.State.Campus.IsFree(office.X, office.Y));
            Assert.IsNull(sim.Facilities.WhyCantPlace(RoomType.Office, office.X, office.Y, office.Rotation));
        }
    }
}
