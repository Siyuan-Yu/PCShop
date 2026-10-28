using System;

namespace PCShopSell.Core.Time
{
    /// <summary>
    /// 世界时钟：整个游戏唯一的一条连续时间线。
    /// 走路、装机、睡觉、店铺营业、顾客进店……所有事情都消耗同一条时间线，
    /// 玩家忙着的时候，别处的生意照常运转。
    /// </summary>
    public sealed class GameClock
    {
        public const int MinutesPerDay = GameTimestamp.MinutesPerDay;

        public GameDate Today { get; private set; }
        public int MinuteOfDay { get; private set; }
        public GameTimestamp Now => GameTimestamp.From(Today, MinuteOfDay);

        /// <summary>
        /// 时间推进后触发，参数为 (起, 止)，区间左闭右开。
        /// 一次 Advance 若跨过午夜，会按"到午夜为止"拆成多段依次触发，
        /// 订阅者（营业模拟、顾客生成）因此永远不会看到跨天的区间。
        /// 触发时时钟状态已更新到"止"。
        /// </summary>
        public event Action<GameTimestamp, GameTimestamp> TimeAdvanced;

        /// <summary>跨过午夜后触发，参数为 (昨天, 今天)。在对应的 TimeAdvanced 之后触发。</summary>
        public event Action<GameDate, GameDate> DayAdvanced;

        public GameClock(GameDate start, int minuteOfDay = 0)
        {
            if (minuteOfDay < 0 || minuteOfDay >= MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(minuteOfDay));
            Today = start;
            MinuteOfDay = minuteOfDay;
        }

        public void Advance(int minutes)
        {
            if (minutes < 0) throw new ArgumentOutOfRangeException(nameof(minutes));

            int remaining = minutes;
            while (remaining > 0)
            {
                GameTimestamp from = Now;
                int untilMidnight = MinutesPerDay - MinuteOfDay;
                int step = Math.Min(remaining, untilMidnight);
                remaining -= step;

                GameTimestamp to = from.AddMinutes(step);
                GameDate previousDay = Today;
                bool crossedMidnight = step == untilMidnight;

                if (crossedMidnight)
                {
                    Today = Today.AddDays(1);
                    MinuteOfDay = 0;
                }
                else
                {
                    MinuteOfDay += step;
                }

                TimeAdvanced?.Invoke(from, to);
                if (crossedMidnight)
                    DayAdvanced?.Invoke(previousDay, Today);
            }
        }

        /// <summary>
        /// 距离下一次出现指定时刻（当天第几分钟）还有多少分钟。
        /// 若现在恰好是该时刻，返回 0。
        /// </summary>
        public int MinutesUntil(int minuteOfDay)
        {
            if (minuteOfDay < 0 || minuteOfDay >= MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(minuteOfDay));
            return (minuteOfDay - MinuteOfDay + MinutesPerDay) % MinutesPerDay;
        }

        /// <summary>推进到下一次出现指定时刻（例如睡到早上 8:00）。</summary>
        public void AdvanceTo(int minuteOfDay)
        {
            Advance(MinutesUntil(minuteOfDay));
        }

        /// <summary>推进到明天 0:00。</summary>
        public void AdvanceDay()
        {
            Advance(MinutesPerDay - MinuteOfDay);
        }

        /// <summary>读档时恢复时钟，不触发事件。</summary>
        public void Restore(GameDate date, int minuteOfDay = 0)
        {
            if (minuteOfDay < 0 || minuteOfDay >= MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(minuteOfDay));
            Today = date;
            MinuteOfDay = minuteOfDay;
        }
    }
}
