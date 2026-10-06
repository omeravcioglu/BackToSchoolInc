using System.Collections.Generic;
using System.Text;

namespace SchoolSim
{
    public readonly struct Factor
    {
        public readonly string Label;
        public readonly float Value;

        public Factor(string label, float value)
        {
            Label = label;
            Value = value;
        }
    }

    /// <summary>
    /// Records every term that went into a computed value, so the UI can answer
    /// "why is parent satisfaction falling?" instead of just showing a number.
    /// </summary>
    public sealed class Breakdown
    {
        public readonly List<Factor> Factors = new List<Factor>();
        public float Total { get; private set; }

        public void Clear()
        {
            Factors.Clear();
            Total = 0f;
        }

        public float Add(string label, float value)
        {
            Factors.Add(new Factor(label, value));
            Total += value;
            return value;
        }

        /// <summary>Scales every recorded term (e.g. a multiplier applied after the fact).</summary>
        public void Scale(string label, float multiplier)
        {
            float before = Total;
            float after = Total * multiplier;
            if (System.Math.Abs(after - before) > 0.01f) Add(label, after - before);
        }

        public float Clamped => MathUtil.Clamp100(Total);

        public string Describe(int maxLines = 12)
        {
            var sb = new StringBuilder();
            var sorted = new List<Factor>(Factors);
            sorted.Sort((a, b) => System.Math.Abs(b.Value).CompareTo(System.Math.Abs(a.Value)));
            int n = 0;
            foreach (var f in sorted)
            {
                if (System.Math.Abs(f.Value) < 0.05f) continue;
                sb.Append(MathUtil.Signed(f.Value, "0.0")).Append("  ").AppendLine(f.Label);
                if (++n >= maxLines) break;
            }
            return sb.ToString();
        }
    }
}
