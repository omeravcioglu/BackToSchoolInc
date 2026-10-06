using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Students interact every school day: constantly with roommates, sometimes with classmates,
    /// teammates and club members. Trait compatibility decides which way each pair drifts.
    /// </summary>
    public sealed class RelationshipSystem
    {
        readonly Simulation _sim;
        readonly Dictionary<int, List<Student>> _byGrade = new Dictionary<int, List<Student>>();

        public RelationshipSystem(Simulation sim) => _sim = sim;

        GameState S => _sim.State;
        SchoolIndices Ix => _sim.Indices;

        public void DailyTick()
        {
            if (!_sim.InSession || S.Students.Count < 2) return;
            var rng = _sim.Rng;

            // Roommates: every pair, every day.
            foreach (var kv in Ix.Residents)
            {
                var list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                        Interact(list[i], list[j], 0.35f, 0.4f, roommates: true);
            }

            // Classmates: a couple of random encounters per student.
            _byGrade.Clear();
            foreach (var s in S.Students)
            {
                if (!_byGrade.TryGetValue(s.Grade, out var g)) _byGrade[s.Grade] = g = new List<Student>();
                g.Add(s);
            }
            foreach (var s in S.Students)
            {
                var grade = _byGrade[s.Grade];
                if (grade.Count < 2) continue;
                for (int k = 0; k < 2; k++)
                {
                    var o = grade[rng.Range(0, grade.Count)];
                    if (o.Id != s.Id && o.DormRoomId != s.DormRoomId) Interact(s, o, 0.15f, 0.15f, roommates: false);
                }
            }

            // Teammates and club members bond through shared activity.
            foreach (var t in S.Teams) GroupInteraction(t.Roster);
            foreach (var c in S.Clubs) GroupInteraction(c.Members);
        }

        void GroupInteraction(List<int> ids)
        {
            if (ids.Count < 2) return;
            var rng = _sim.Rng;
            int encounters = Math.Min(ids.Count, 6);
            for (int k = 0; k < encounters; k++)
            {
                var a = S.FindStudent(ids[rng.Range(0, ids.Count)]);
                var b = S.FindStudent(ids[rng.Range(0, ids.Count)]);
                if (a == null || b == null || a.Id == b.Id) continue;
                Interact(a, b, 0.35f, 0.15f, roommates: false);
            }
        }

        void Interact(Student a, Student b, float baseline, float compatWeight, bool roommates)
        {
            var rng = _sim.Rng;
            var rel = S.Relationships.GetOrCreate(a.Id, b.Id);
            float compat = TraitRules.Compatibility(a, b, roommates);
            float delta = baseline + compat * compatWeight + rng.Range(-0.6f, 0.6f);
            rel.Score += delta;

            if (!rel.Crush && rel.Score > 45f && Math.Abs(a.Grade - b.Grade) <= 1 && rng.Chance(0.0006f)
                && !S.Relationships.HasCrush(a.Id) && !S.Relationships.HasCrush(b.Id))
                rel.Crush = true;
            else if (rel.Crush && rel.Score < 15f)
            {
                rel.Crush = false;
                a.Moodlets.Set("Heartbreak", -10f, S.Day + 14);
                b.Moodlets.Set("Heartbreak", -10f, S.Day + 14);
                a.Remember(S.Day, $"Broke up with {b.FullName}.");
                b.Remember(S.Day, $"Broke up with {a.FullName}.");
            }

            if (!rel.Rivalry && a.Has(StudentTrait.Competitive) && b.Has(StudentTrait.Competitive)
                && Math.Abs(a.Academic - b.Academic) < 15f && rel.Score < 20f && rng.Chance(0.01f))
                rel.Rivalry = true;
            else if (rel.Rivalry && rel.Score > 50f)
                rel.Rivalry = false;

            var type = rel.Type;
            if (type != rel.LastType)
            {
                OnTypeChanged(a, b, rel.LastType, type);
                rel.LastType = type;
            }
        }

        void OnTypeChanged(Student a, Student b, RelationshipType from, RelationshipType to)
        {
            int day = S.Day;
            switch (to)
            {
                case RelationshipType.BestFriend:
                    a.Remember(day, $"Became best friends with {b.FullName}.");
                    b.Remember(day, $"Became best friends with {a.FullName}.");
                    break;
                case RelationshipType.Enemy:
                    a.Remember(day, $"Became enemies with {b.FullName}.");
                    b.Remember(day, $"Became enemies with {a.FullName}.");
                    if (a.DormRoomId == b.DormRoomId && a.DormRoomId >= 0)
                        _sim.Events.TriggerDirected("roommate_conflict", m => { m.StudentId = a.Id; m.StudentId2 = b.Id; m.RoomId = a.DormRoomId; });
                    break;
                case RelationshipType.Crush:
                    a.Remember(day, $"Has a crush on {b.FullName}.");
                    b.Remember(day, $"Has a crush on {a.FullName}.");
                    break;
                case RelationshipType.Rival when from != RelationshipType.Enemy:
                    a.Remember(day, $"Became rivals with {b.FullName}.");
                    b.Remember(day, $"Became rivals with {a.FullName}.");
                    break;
            }
        }

        /// <summary>Relationships between students who no longer see each other fade.</summary>
        public void Monthly()
        {
            var fade = new List<Relationship>();
            foreach (var r in S.Relationships.All)
            {
                var a = S.FindStudent(r.A);
                var b = S.FindStudent(r.B);
                bool together = a != null && b != null && a.DormRoomId == b.DormRoomId && a.DormRoomId >= 0;
                if (together) continue;
                r.Score *= 0.95f;
                if (Math.Abs(r.Score) < 3f && !r.Crush && !r.Rivalry) fade.Add(r);
            }
            foreach (var r in fade) S.Relationships.Remove(r);
        }

        public static string Describe(RelationshipType t) => t switch
        {
            RelationshipType.BestFriend => "Best Friend",
            _ => t.ToString(),
        };
    }
}
