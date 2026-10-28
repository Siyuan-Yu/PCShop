using System;

namespace PCShopSell.Core.Time
{
    /// <summary>
    /// 游戏时钟：持有"今天"，按天推进，并通知订阅者（结算、事件、行情更新等都挂在这里）。
    /// </summary>
    public sealed class GameClock
    {
        public GameDate Today { get; private set; }

        /// <summary>日期推进后触发，参数为 (昨天, 今天)。</summary>
        public event Action<GameDate, GameDate> DayAdvanced;

        public GameClock(GameDate start)
        {
            Today = start;
        }

        public void AdvanceDay()
        {
            GameDate previous = Today;
            Today = previous.AddDays(1);
            DayAdvanced?.Invoke(previous, Today);
        }

        /// <summary>读档时恢复日期，不触发事件。</summary>
        public void Restore(GameDate date)
        {
            Today = date;
        }
    }
}
