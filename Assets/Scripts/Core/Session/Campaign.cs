using System;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    /// <summary>长线目标里的一个检查点。到日子自动评估一次，过了有奖励，没过有代价，但都不会直接结束游戏。</summary>
    public sealed class MilestoneDef
    {
        public string Id;
        public int Day;
        public string Title;
        public string Goal;
        public Func<GameSession, bool> Check;
        public Func<GameSession, string> Progress;
        public string PassText;
        public string FailText;
    }

    public sealed class EndingDef
    {
        public string Id { get; }
        public string Title { get; }
        public string Summary { get; }

        public EndingDef(string id, string title, string summary)
        {
            Id = id;
            Title = title;
            Summary = summary;
        }
    }

    /// <summary>三年长线目标和结局的数值。全部是占位值，按"普通玩家机器人"的跑分定的门槛，试玩后再调。</summary>
    public static class Campaign
    {
        public const int TotalDays = 1095;
        public const int Year1Day = 365;
        public const int Year2Day = 730;

        public static readonly Money Year1Wealth = Money.FromYuan(300000);
        public const double Year1Reputation = 30;
        public static readonly Money Year2Wealth = Money.FromYuan(1500000);

        public static readonly Money TierStar = Money.FromYuan(6000000);
        public static readonly Money TierSteady = Money.FromYuan(1500000);
        public static readonly Money TierBoss = Money.FromYuan(10000000);
        public const double BossReputation = 50;

        /// <summary>没过检查点的惩罚：店铺月租上涨的比例（可叠加，最多 3 次）。</summary>
        public const double RentHikePerFail = 0.10;

        public const double PassReputationBonus = 5;

        public static readonly IReadOnlyList<MilestoneDef> Milestones = new[]
        {
            new MilestoneDef
            {
                Id = "y1", Day = Year1Day, Title = "站稳一年",
                Goal = $"第 {Year1Day} 天：身家 ≥ {Year1Wealth}，知名度 ≥ {Year1Reputation:0}",
                Check = s => s.NetWorth() >= Year1Wealth && s.PlayerShop.Reputation >= Year1Reputation,
                Progress = s => $"身家 {s.NetWorth()} / {Year1Wealth}　知名度 {s.PlayerShop.Reputation:0} / {Year1Reputation:0}",
                PassText = "一年了，这条街上没人再把你当过路的。知名度 +5。",
                FailText = "一年过去，生意不上不下。房东嘀咕着要涨租，店租 +10%。",
            },
            new MilestoneDef
            {
                Id = "y2", Day = Year2Day, Title = "有名号的店",
                Goal = $"第 {Year2Day} 天：身家 ≥ {Year2Wealth}，并且把店面扩建到中型以上",
                Check = s => s.NetWorth() >= Year2Wealth && s.PlayerShop.Size != ShopSize.Small,
                Progress = s => $"身家 {s.NetWorth()} / {Year2Wealth}　店面：{GameSession.SizeName(s.PlayerShop.Size)}",
                PassText = "两年下来，电脑城里提起你的店都会说一句“靠谱”。知名度 +5。",
                FailText = "两年了还是小打小闹，房东又提了租金。店租 +10%。",
            },
        };

        public static readonly EndingDef Scrape = new EndingDef("scrape", "勉强糊口",
            "三年过去，你的店还开着，只是日子过得紧巴巴。至少，你没被这座城市吞掉。");

        public static readonly EndingDef Steady = new EndingDef("steady", "稳稳当当",
            "三年下来，店里有了固定的客人，账上也有了积蓄。日子不算大富大贵，但是踏实。");

        public static readonly EndingDef Star = new EndingDef("star", "电脑城新星",
            "三年前没人知道这家最东头的小店，如今电脑城里提起你的名字，同行都得敬一杯。");

        public static readonly EndingDef Boss = new EndingDef("boss", "电脑城大亨",
            "整层打通的大店灯火通明。三年前揣着五千块来的年轻人，现在是这条街上最有分量的老板之一。");

        public static EndingDef Evaluate(GameSession s)
        {
            Money worth = s.NetWorth();
            if (worth >= TierBoss && s.PlayerShop.Size == ShopSize.Large && s.PlayerShop.Reputation >= BossReputation) return Boss;
            if (worth >= TierStar) return Star;
            if (worth >= TierSteady) return Steady;
            return Scrape;
        }
    }
}
