using System;

namespace PCShopSell.Core.Common
{
    /// <summary>
    /// 可存档的随机数（xorshift64*）。状态就是一个 ulong，读档后结果可复现。
    /// </summary>
    public sealed class GameRandom
    {
        public ulong State { get; set; }

        public GameRandom(ulong seed)
        {
            State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public ulong NextULong()
        {
            ulong x = State;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            State = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>[0, 1)。</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        /// <summary>[min, maxExclusive)。</summary>
        public int Next(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextULong() % (ulong)(maxExclusive - min));
        }

        public bool Chance(double p) => NextDouble() < p;

        public double Range(double min, double max) => min + NextDouble() * (max - min);

        /// <summary>近似正态分布（12 个均匀数求和）。</summary>
        public double Gaussian(double sigma)
        {
            double s = 0;
            for (int i = 0; i < 12; i++) s += NextDouble();
            return (s - 6.0) * sigma;
        }

        public T Pick<T>(System.Collections.Generic.IReadOnlyList<T> list)
        {
            if (list == null || list.Count == 0) throw new ArgumentException("列表为空。");
            return list[Next(0, list.Count)];
        }
    }
}
