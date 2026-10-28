using System;
using System.Collections.Generic;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.World
{
    public enum TravelStatus
    {
        Success,
        NoPath,
    }

    /// <summary>一次出行的规划结果。</summary>
    public sealed class TravelPlan
    {
        public bool Found { get; }
        public IReadOnlyList<string> NodeIds { get; }
        public double Distance { get; }
        public int Minutes { get; }

        public TravelPlan(bool found, IReadOnlyList<string> nodeIds, double distance, int minutes)
        {
            Found = found;
            NodeIds = nodeIds;
            Distance = distance;
            Minutes = minutes;
        }
    }

    public sealed class TravelResult
    {
        public TravelStatus Status { get; }
        public TravelPlan Plan { get; }
        public bool IsSuccess => Status == TravelStatus.Success;

        public TravelResult(TravelStatus status, TravelPlan plan)
        {
            Status = status;
            Plan = plan;
        }
    }

    public static class TravelCalculator
    {
        /// <summary>
        /// 距离折算为游戏内分钟（向上取整，非零距离至少 1 分钟）。
        /// timeFactor 为耗时系数：1 为正常，大于 1 更慢（下雨、堵车），小于 1 更快。
        /// </summary>
        public static int MinutesFor(double distance, Transport transport, double timeFactor = 1.0)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            if (distance < 0) throw new ArgumentOutOfRangeException(nameof(distance));
            if (timeFactor <= 0) throw new ArgumentOutOfRangeException(nameof(timeFactor));
            if (distance == 0) return 0;

            int minutes = (int)Math.Ceiling(distance * timeFactor / transport.SpeedPerMinute);
            return Math.Max(1, minutes);
        }
    }

    /// <summary>
    /// 出行规划。
    /// 世界时间是连续的：走路和做其他事情一样推进同一个时钟，路上店铺照常营业。
    /// 界面层通常边走边逐步推进时钟（让玩家实时看到顾客进店）；
    /// 无需演出时（快速前往、测试）可调用 <see cref="Travel"/> 一次性推进。
    /// </summary>
    public static class TravelPlanner
    {
        /// <summary>只规划，不推进时间（用于预览"走过去要多久"）。</summary>
        public static TravelPlan Plan(WorldMap map, string from, string to, Transport transport, double timeFactor = 1.0)
        {
            PathResult path = Pathfinder.FindPath(map, from, to);
            if (!path.Found)
                return new TravelPlan(false, Array.Empty<string>(), 0, 0);

            int minutes = TravelCalculator.MinutesFor(path.Distance, transport, timeFactor);
            return new TravelPlan(true, path.NodeIds, path.Distance, minutes);
        }

        /// <summary>一次性出行：找到路径则把世界时钟推进相应分钟。</summary>
        public static TravelResult Travel(
            WorldMap map, string from, string to, Transport transport, GameClock clock, double timeFactor = 1.0)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            TravelPlan plan = Plan(map, from, to, transport, timeFactor);
            if (!plan.Found)
                return new TravelResult(TravelStatus.NoPath, plan);

            clock.Advance(plan.Minutes);
            return new TravelResult(TravelStatus.Success, plan);
        }
    }
}
