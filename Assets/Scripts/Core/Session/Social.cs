using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Catalog;

namespace PCShopSell.Core.Session
{
    /// <summary>约人出去玩的时段：晚上（19:00 起）或周末白天（10:00 起）。</summary>
    public enum OutingSlot
    {
        Evening,
        Weekend,
    }

    public sealed class ActivityDef
    {
        public string Id { get; }
        public string Name { get; }
        public OutingSlot Slot { get; }
        public Money Cost { get; }
        public int DurationMinutes { get; }

        /// <summary>基础交情增量（喜欢这个活动的人 ×1.5）。</summary>
        public double BaseGain { get; }

        public double EnergyChange { get; }
        public double HealthChange { get; }
        public double HungerChange { get; }

        /// <summary>这个活动里对方透露消息的额外概率。</summary>
        public double TipBonus { get; }

        public string Flavor { get; }

        public ActivityDef(string id, string name, OutingSlot slot, long costYuan, int duration, double gain,
            double energy, double health, double hunger, double tipBonus, string flavor)
        {
            Id = id;
            Name = name;
            Slot = slot;
            Cost = Money.FromYuan(costYuan);
            DurationMinutes = duration;
            BaseGain = gain;
            EnergyChange = energy;
            HealthChange = health;
            HungerChange = hunger;
            TipBonus = tipBonus;
            Flavor = flavor;
        }
    }

    public static class Activities
    {
        public const string Dinner = "dinner";
        public const string Ktv = "ktv";
        public const string Snack = "snack";
        public const string Fishing = "fishing";
        public const string MarketWalk = "market";
        public const string Hike = "hike";

        public static readonly IReadOnlyList<ActivityDef> All = new[]
        {
            new ActivityDef(Dinner, "去小馆子吃饭", OutingSlot.Evening, 150, 150, 7, -4, 1, 30, 0.10, "几杯酒下肚，话也多了起来。"),
            new ActivityDef(Ktv, "唱 KTV", OutingSlot.Evening, 260, 180, 10, -12, 0, 10, 0.05, "吼了一晚上，嗓子哑了，关系也近了。"),
            new ActivityDef(Snack, "吃夜宵摊", OutingSlot.Evening, 60, 120, 4, -3, 0, 25, 0.05, "路边摊的烤串，聊得比吃得多。"),
            new ActivityDef(Fishing, "一起钓鱼", OutingSlot.Weekend, 80, 360, 9, -5, 3, 15, 0.10, "鱼没钓上几条，闲话倒是聊了一肚子。"),
            new ActivityDef(MarketWalk, "一起逛电子市场", OutingSlot.Weekend, 100, 300, 7, -8, 0, 15, 0.30, "边逛边听他讲市场里的门道。"),
            new ActivityDef(Hike, "郊游爬山", OutingSlot.Weekend, 160, 420, 12, -10, 6, 25, 0.05, "累是累，山顶的风让人心情舒畅。"),
        };

        public static ActivityDef Get(string id)
        {
            foreach (ActivityDef a in All)
                if (a.Id == id) return a;
            return null;
        }
    }

    /// <summary>约好的行程。到点会弹出选择去哪玩的窗口。</summary>
    public sealed class Appointment
    {
        public string NpcId;
        public OutingSlot Slot;
        public int Day;

        public int StartMinute => Slot == OutingSlot.Evening ? GameSession.EveningStartMinute : GameSession.WeekendStartMinute;
    }

    public enum TipOutcome
    {
        Pending,
        Right,
        Wrong,
    }

    /// <summary>听来的一条行情消息（真假要靠自己验证）。</summary>
    public sealed class MarketTip
    {
        public string NpcId;
        public PartCategory Category;

        /// <summary>+1 说会涨，-1 说会跌。</summary>
        public int Direction;

        public int IssuedDay;
        public int ResolveDay;
        public double IndexAtIssue;
        public TipOutcome Outcome;
    }
}
