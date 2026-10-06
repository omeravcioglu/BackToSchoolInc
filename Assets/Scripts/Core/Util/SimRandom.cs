using System;
using System.Collections.Generic;

namespace SchoolSim
{
    /// <summary>
    /// Seeded RNG. Every random roll in the simulation goes through this so a given seed and
    /// sequence of player commands always replays identically. It is xoshiro128** with its state
    /// in plain fields, so a saved school picks up exactly where it left off.
    /// </summary>
    public sealed class SimRandom
    {
        uint _a, _b, _c, _d;

        public int Seed { get; }

        public SimRandom(int seed)
        {
            Seed = seed;
            ulong z = unchecked((ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL);
            _a = (uint)SplitMix(ref z);
            _b = (uint)SplitMix(ref z);
            _c = (uint)SplitMix(ref z);
            _d = (uint)SplitMix(ref z);
            if ((_a | _b | _c | _d) == 0) _a = 1;
        }

        /// <summary>Continues another generator's sequence from where it is now.</summary>
        public void CopyStateFrom(SimRandom other)
        {
            _a = other._a;
            _b = other._b;
            _c = other._c;
            _d = other._d;
        }

        /// <summary>The generator's position in its sequence, for save files.</summary>
        public uint[] GetState() => new[] { _a, _b, _c, _d };

        public void SetState(uint[] state)
        {
            _a = state[0];
            _b = state[1];
            _c = state[2];
            _d = state[3];
            if ((_a | _b | _c | _d) == 0) _a = 1;
        }

        static ulong SplitMix(ref ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                ulong r = z;
                r = (r ^ (r >> 30)) * 0xBF58476D1CE4E5B9UL;
                r = (r ^ (r >> 27)) * 0x94D049BB133111EBUL;
                return r ^ (r >> 31);
            }
        }

        uint NextUInt()
        {
            unchecked
            {
                uint result = Rotl(_b * 5u, 7) * 9u;
                uint t = _b << 9;
                _c ^= _a;
                _d ^= _b;
                _b ^= _c;
                _a ^= _d;
                _c ^= t;
                _d = Rotl(_d, 11);
                return result;
            }
        }

        static uint Rotl(uint x, int k) => (x << k) | (x >> (32 - k));

        /// <summary>Uniform in [0, 1) with 53 bits of precision.</summary>
        double NextDouble()
        {
            ulong hi = NextUInt() >> 5, lo = NextUInt() >> 6;
            return (hi * 67108864.0 + lo) / 9007199254740992.0;
        }

        int Next(int count) => count <= 1 ? 0 : (int)(((ulong)NextUInt() * (ulong)count) >> 32);

        public float Value() => (float)NextDouble();

        public bool Chance(float probability) => NextDouble() < probability;

        public int Range(int minInclusive, int maxExclusive) =>
            maxExclusive <= minInclusive ? minInclusive : minInclusive + Next(maxExclusive - minInclusive);

        public float Range(float min, float max) => min + (float)NextDouble() * (max - min);

        public float Gaussian(float mean, float stdDev)
        {
            double u1 = 1.0 - NextDouble();
            double u2 = NextDouble();
            double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + (float)normal * stdDev;
        }

        /// <summary>Gaussian clamped to the 0-100 stat range.</summary>
        public float Stat(float mean, float stdDev) => MathUtil.Clamp100(Gaussian(mean, stdDev));

        public T Pick<T>(IReadOnlyList<T> list) => list[Next(list.Count)];

        public T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weight)
        {
            float total = 0f;
            for (int i = 0; i < items.Count; i++) total += Math.Max(0f, weight(items[i]));
            if (total <= 0f) return items[Next(items.Count)];

            float roll = Value() * total;
            for (int i = 0; i < items.Count; i++)
            {
                roll -= Math.Max(0f, weight(items[i]));
                if (roll <= 0f) return items[i];
            }
            return items[items.Count - 1];
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    public static class MathUtil
    {
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Clamp100(float v) => Clamp(v, 0f, 100f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        /// <summary>Exponential approach: moves <paramref name="rate"/> of the remaining gap per call.</summary>
        public static float Approach(float current, float target, float rate) => current + (target - current) * rate;

        public static float Sigmoid(float x) => 1f / (1f + (float)Math.Exp(-x));

        public static string Money(float amount)
        {
            string sign = amount < 0 ? "-" : "";
            return sign + "$" + Math.Abs(Math.Round(amount)).ToString("N0");
        }

        public static string Signed(float value, string format = "0.#")
        {
            return (value >= 0 ? "+" : "") + value.ToString(format);
        }
    }
}
