using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>A rectangle of campus tiles. X/Y is the bottom-left tile; Y grows north.</summary>
    public readonly struct TileRect
    {
        public readonly int X, Y, W, H;

        public TileRect(int x, int y, int w, int h)
        {
            X = x; Y = y; W = w; H = h;
        }

        public int XMax => X + W;   // exclusive
        public int YMax => Y + H;   // exclusive
        public bool Contains(int x, int y) => x >= X && x < XMax && y >= Y && y < YMax;
        public bool Overlaps(TileRect o) => X < o.XMax && o.X < XMax && Y < o.YMax && o.Y < YMax;
        public override string ToString() => $"({X},{Y} {W}x{H})";
    }

    /// <summary>
    /// Room orientation is named by the side its door faces. Footprints are defined with the door
    /// on the south edge; East and West swap width and height.
    /// </summary>
    public static class Placement
    {
        public const int South = 0, East = 1, North = 2, West = 3;

        public static TileRect Footprint(RoomDefinition def, int x, int y, int rotation)
        {
            bool turned = (rotation & 1) == 1;
            return new TileRect(x, y, turned ? def.Height : def.Width, turned ? def.Width : def.Height);
        }

        /// <summary>The tile inside the room that holds the door.</summary>
        public static void DoorCell(TileRect r, int rotation, out int x, out int y)
        {
            switch (rotation & 3)
            {
                case East: x = r.XMax - 1; y = r.Y + r.H / 2; break;
                case North: x = r.X + r.W / 2; y = r.YMax - 1; break;
                case West: x = r.X; y = r.Y + r.H / 2; break;
                default: x = r.X + r.W / 2; y = r.Y; break;
            }
        }

        /// <summary>The tile just outside the door, which must stay walkable.</summary>
        public static void DoorFront(TileRect r, int rotation, out int x, out int y)
        {
            DoorCell(r, rotation, out x, out y);
            switch (rotation & 3)
            {
                case East: x++; break;
                case North: y++; break;
                case West: x--; break;
                default: y--; break;
            }
        }

        public static string SideName(int rotation) => (rotation & 3) switch
        {
            East => "east",
            North => "north",
            West => "west",
            _ => "south",
        };
    }

    /// <summary>
    /// The campus map: a grid of tiles split into square land parcels the school can own, plus a
    /// tile → room occupancy index. The parcel columns east of <see cref="EstateParcelX"/> are the
    /// old estate across the road, the second campus: none of it is for sale until it opens.
    /// <see cref="Version"/> changes whenever anything visible changes, so views can redraw lazily.
    /// </summary>
    public sealed class CampusMap
    {
        public const int ParcelSize = 10;

        public readonly int ParcelsX;
        public readonly int ParcelsY;
        public readonly int EstateParcelX;   // first parcel column of the old estate
        public bool EstateOpen;

        readonly bool[] _owned;
        readonly int[] _occupant;          // room id per tile, 0 = empty
        readonly int[] _doorFront;         // id of the room whose door opens onto this tile, 0 = none

        public int Version { get; private set; }

        public CampusMap(int parcelsX = 7, int parcelsY = 5, int estateParcelsX = 3)
        {
            ParcelsX = parcelsX + estateParcelsX;
            ParcelsY = parcelsY;
            EstateParcelX = parcelsX;
            _owned = new bool[ParcelsX * parcelsY];
            _occupant = new int[Width * Height];
            _doorFront = new int[Width * Height];
        }

        public int Width => ParcelsX * ParcelSize;
        public int Height => ParcelsY * ParcelSize;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool ParcelInBounds(int px, int py) => px >= 0 && py >= 0 && px < ParcelsX && py < ParcelsY;

        public bool IsEstateParcel(int px, int py) => ParcelInBounds(px, py) && px >= EstateParcelX;
        public bool IsEstate(int x, int y) => InBounds(x, y) && x >= EstateParcelX * ParcelSize;

        /// <summary>Where the old manor stands: in the middle of the estate, behind the gate, door facing south.</summary>
        public int ManorX => (EstateParcelX + 1) * ParcelSize;
        public int ManorY => ParcelsY / 2 * ParcelSize + 2;

        public bool IsParcelOwned(int px, int py) => ParcelInBounds(px, py) && _owned[py * ParcelsX + px];
        public bool IsOwned(int x, int y) => InBounds(x, y) && _owned[(y / ParcelSize) * ParcelsX + x / ParcelSize];

        public void SetParcelOwned(int px, int py, bool owned)
        {
            if (!ParcelInBounds(px, py)) return;
            _owned[py * ParcelsX + px] = owned;
            Touch();
        }

        public int OwnedParcelCount
        {
            get
            {
                int n = 0;
                foreach (bool o in _owned) if (o) n++;
                return n;
            }
        }

        public int OwnedTiles => OwnedParcelCount * ParcelSize * ParcelSize;

        /// <summary>An unowned parcel that shares an edge with owned land (on the estate, only once it's open).</summary>
        public bool IsParcelPurchasable(int px, int py)
        {
            if (!ParcelInBounds(px, py) || IsParcelOwned(px, py)) return false;
            if (IsEstateParcel(px, py) && !EstateOpen) return false;
            return IsParcelOwned(px - 1, py) || IsParcelOwned(px + 1, py) || IsParcelOwned(px, py - 1) || IsParcelOwned(px, py + 1);
        }

        public int RoomIdAt(int x, int y) => InBounds(x, y) ? _occupant[y * Width + x] : 0;
        public bool IsFree(int x, int y) => InBounds(x, y) && _occupant[y * Width + x] == 0;
        public int DoorFrontOwnerAt(int x, int y) => InBounds(x, y) ? _doorFront[y * Width + x] : 0;

        /// <summary>Tiles covered by rooms (including rooms under construction).</summary>
        public int UsedTiles
        {
            get
            {
                int n = 0;
                foreach (int id in _occupant) if (id != 0) n++;
                return n;
            }
        }

        public void RebuildOccupancy(IReadOnlyList<Room> rooms)
        {
            Array.Clear(_occupant, 0, _occupant.Length);
            Array.Clear(_doorFront, 0, _doorFront.Length);
            foreach (var r in rooms)
            {
                var f = r.Footprint;
                for (int y = f.Y; y < f.YMax; y++)
                    for (int x = f.X; x < f.XMax; x++)
                        if (InBounds(x, y)) _occupant[y * Width + x] = r.Id;
                if (r.Def.Decor) continue;   // decorations have no door
                r.DoorFront(out int dx, out int dy);
                if (InBounds(dx, dy)) _doorFront[dy * Width + dx] = r.Id;
            }
            Touch();
        }

        /// <summary>Marks the map as changed so views redraw.</summary>
        public void Touch() => Version++;
    }
}
