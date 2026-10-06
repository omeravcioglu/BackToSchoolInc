using System.Collections.Generic;

namespace SchoolSim
{
    public sealed class Relationship
    {
        public int A;
        public int B;
        public RelationshipType LastType = RelationshipType.Neutral;   // last type reported to the history log

        float _score;
        bool _crush;
        bool _rivalry;

        /// <summary>-100..100. Setting it re-evaluates the relationship type.</summary>
        public float Score
        {
            get => _score;
            set { _score = MathUtil.Clamp(value, -100f, 100f); Refresh(); }
        }

        public bool Crush
        {
            get => _crush;
            set { _crush = value; Refresh(); }
        }

        public bool Rivalry
        {
            get => _rivalry;
            set { _rivalry = value; Refresh(); }
        }

        /// <summary>
        /// Current label. Thresholds have hysteresis so a pair hovering around a boundary doesn't
        /// flip between "enemy" and "rival" every day.
        /// </summary>
        public RelationshipType Type { get; private set; } = RelationshipType.Neutral;

        void Refresh()
        {
            var prev = Type;
            if (_crush) { Type = RelationshipType.Crush; return; }

            float s = _score;
            if (s <= (prev == RelationshipType.Enemy ? -52f : -60f)) Type = RelationshipType.Enemy;
            else if (_rivalry || s <= (prev == RelationshipType.Rival || prev == RelationshipType.Enemy ? -18f : -25f)) Type = RelationshipType.Rival;
            else if (s >= (prev == RelationshipType.BestFriend ? 62f : 70f)) Type = RelationshipType.BestFriend;
            else if (s >= (prev == RelationshipType.Friend || prev == RelationshipType.BestFriend ? 24f : 30f)) Type = RelationshipType.Friend;
            else Type = RelationshipType.Neutral;
        }

        public int Other(int id) => id == A ? B : A;
    }

    /// <summary>
    /// Sparse pairwise relationship store. Only pairs that have interacted exist. The pairs are
    /// kept in creation order (that's what gets saved); the lookups are rebuilt after loading, so
    /// a loaded school walks its relationships in exactly the same order as before.
    /// </summary>
    public sealed class RelationshipGraph : ISaveHooks
    {
        readonly List<Relationship> _ordered = new List<Relationship>();
        [System.NonSerialized] readonly Dictionary<long, Relationship> _pairs = new Dictionary<long, Relationship>();
        [System.NonSerialized] readonly Dictionary<int, List<Relationship>> _byStudent = new Dictionary<int, List<Relationship>>();

        static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        void ISaveHooks.AfterLoad()
        {
            _pairs.Clear();
            _byStudent.Clear();
            foreach (var r in _ordered)
            {
                _pairs[Key(r.A, r.B)] = r;
                Index(r.A).Add(r);
                Index(r.B).Add(r);
            }
        }

        public int Count => _pairs.Count;

        public Relationship Get(int a, int b)
        {
            _pairs.TryGetValue(Key(a, b), out var r);
            return r;
        }

        public float Score(int a, int b) => Get(a, b)?.Score ?? 0f;

        public Relationship GetOrCreate(int a, int b)
        {
            long key = Key(a, b);
            if (_pairs.TryGetValue(key, out var r)) return r;

            r = new Relationship { A = System.Math.Min(a, b), B = System.Math.Max(a, b) };
            _pairs[key] = r;
            _ordered.Add(r);
            Index(a).Add(r);
            Index(b).Add(r);
            return r;
        }

        public IReadOnlyList<Relationship> For(int studentId)
        {
            return _byStudent.TryGetValue(studentId, out var list) ? list : (IReadOnlyList<Relationship>)System.Array.Empty<Relationship>();
        }

        public IEnumerable<Relationship> All => _ordered;

        public int CountOfType(int studentId, RelationshipType type)
        {
            int n = 0;
            foreach (var r in For(studentId))
                if (r.Type == type) n++;
            return n;
        }

        public bool HasCrush(int studentId)
        {
            foreach (var r in For(studentId))
                if (r.Crush) return true;
            return false;
        }

        public void RemoveStudent(int id)
        {
            if (!_byStudent.TryGetValue(id, out var list)) return;
            foreach (var r in list)
            {
                _pairs.Remove(Key(r.A, r.B));
                _ordered.Remove(r);
                int other = r.Other(id);
                if (_byStudent.TryGetValue(other, out var otherList)) otherList.Remove(r);
            }
            _byStudent.Remove(id);
        }

        public void Remove(Relationship r)
        {
            _pairs.Remove(Key(r.A, r.B));
            _ordered.Remove(r);
            if (_byStudent.TryGetValue(r.A, out var la)) la.Remove(r);
            if (_byStudent.TryGetValue(r.B, out var lb)) lb.Remove(r);
        }

        List<Relationship> Index(int id)
        {
            if (!_byStudent.TryGetValue(id, out var list))
            {
                list = new List<Relationship>();
                _byStudent[id] = list;
            }
            return list;
        }
    }
}
