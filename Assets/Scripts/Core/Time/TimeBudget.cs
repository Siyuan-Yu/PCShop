using System;

namespace PCShopSell.Core.Time
{
    /// <summary>
    /// 每日时间预算（以分钟计）。自己装机、跑货、社交等行动都占用预算，
    /// 这是"自己干 vs 雇人"的核心取舍。
    /// </summary>
    public sealed class TimeBudget
    {
        public int TotalMinutes { get; private set; }
        public int RemainingMinutes { get; private set; }
        public int SpentMinutes => TotalMinutes - RemainingMinutes;
        public bool IsExhausted => RemainingMinutes <= 0;

        public TimeBudget(int totalMinutes)
        {
            if (totalMinutes < 0) throw new ArgumentOutOfRangeException(nameof(totalMinutes));
            TotalMinutes = totalMinutes;
            RemainingMinutes = totalMinutes;
        }

        public bool CanSpend(int minutes) => minutes >= 0 && minutes <= RemainingMinutes;

        public bool TrySpend(int minutes)
        {
            if (minutes < 0) throw new ArgumentOutOfRangeException(nameof(minutes));
            if (minutes > RemainingMinutes) return false;
            RemainingMinutes -= minutes;
            return true;
        }

        /// <summary>新的一天：按当前总量补满。</summary>
        public void Refill()
        {
            RemainingMinutes = TotalMinutes;
        }

        /// <summary>新的一天：按新的总量补满（如状态、车辆、员工使每日可用时间变化）。</summary>
        public void Refill(int newTotalMinutes)
        {
            if (newTotalMinutes < 0) throw new ArgumentOutOfRangeException(nameof(newTotalMinutes));
            TotalMinutes = newTotalMinutes;
            RemainingMinutes = newTotalMinutes;
        }

        /// <summary>当天临时增加可用时间（正数）或扣减（负数，最低到 0）。</summary>
        public void Adjust(int deltaMinutes)
        {
            RemainingMinutes = Math.Max(0, RemainingMinutes + deltaMinutes);
        }
    }
}
