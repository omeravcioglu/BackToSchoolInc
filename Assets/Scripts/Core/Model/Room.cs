namespace SchoolSim
{
    public sealed class Room
    {
        public int Id;
        public RoomType Type;
        public string Name;
        public int Level = 1;
        public float Cleanliness = 85f;
        public float Condition = 100f;

        // Position on the campus map. Rotation names the side the door faces (see Placement).
        public int X;
        public int Y;
        public int Rotation;

        /// <summary>New rooms are unusable until this day. Upgrades keep the room usable.</summary>
        public int ConstructionDoneDay;
        public int ConstructionStartDay;
        public int PendingLevel;             // > 0 while an upgrade is in progress
        public int ClosedUntilDay = -1;      // closed by inspectors or a breakdown

        public RoomDefinition Def => RoomCatalog.Get(Type);

        public bool IsBuilt(int day) => day >= ConstructionDoneDay || PendingLevel > 0;
        public bool IsUpgrading => PendingLevel > 0;
        public bool IsClosed(int day) => ClosedUntilDay > day;
        public bool IsOperational(int day) => IsBuilt(day) && !IsClosed(day);

        /// <summary>0..1 progress of the current construction or upgrade.</summary>
        public float ConstructionProgress(int day)
        {
            int total = ConstructionDoneDay - ConstructionStartDay;
            if (total <= 0) return 1f;
            return MathUtil.Clamp01((day - ConstructionStartDay) / (float)total);
        }

        public int Capacity => Def.Capacity[Level - 1];
        public float BaseQuality => Def.Quality[Level - 1];
        public float Upkeep => Def.Upkeep[Level - 1];
        public string LevelName => Def.LevelNames[Level - 1];

        public TileRect Footprint => Placement.Footprint(Def, X, Y, Rotation);
        public int Width => Footprint.W;
        public int Height => Footprint.H;

        public void DoorCell(out int x, out int y) => Placement.DoorCell(Footprint, Rotation, out x, out y);
        public void DoorFront(out int x, out int y) => Placement.DoorFront(Footprint, Rotation, out x, out y);

        /// <summary>Effective quality after cleanliness and wear.</summary>
        public float Quality
        {
            get
            {
                float q = BaseQuality * (0.65f + 0.2f * Cleanliness / 100f + 0.15f * Condition / 100f);
                if (Condition < 30f) q *= 0.75f;
                return q;
            }
        }

        public bool HasPrivateBath => Type == RoomType.DormRoom && Level >= Def.PrivateBathFromLevel;

        public override string ToString() => $"{Name} (L{Level})";
    }
}
