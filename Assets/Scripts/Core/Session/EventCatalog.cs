using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 随机事件目录。每天 11:00 按概率抽一个满足条件的事件，弹出选项让玩家选。
    /// 数值是占位，等试玩后平衡；新增事件只需要在这里加一项。
    /// </summary>
    public static class EventCatalog
    {
        private static Money Y(long yuan) => Money.FromYuan(yuan);

        private static bool CanPay(GameSession s, long yuan) => s.Ledger.Balance >= Y(yuan);

        /// <summary>从店里随机拿走一件货，返回名字；店里没货返回 null。</summary>
        internal static string LoseRandomItem(GameSession s)
        {
            List<StockLine> lines = s.PlayerShop.Stock.Lines();
            if (lines.Count == 0) return null;
            StockLine l = lines[s.Random.Next(0, lines.Count)];
            s.PlayerShop.Stock.TryRemove(l.Part, l.Boxed, 1);
            return l.Part.Name;
        }

        private static readonly IReadOnlyList<EventDef> Base = new List<EventDef>
        {
            new EventDef
            {
                Id = "blackout", Title = "停电了", MinDay = 3, CooldownDays = 25, Weight = 8,
                When = s => s.NeedsEmergencyLighting,
            },
            new EventDef
            {
                Id = "money_parttime_offer", Title = "周末搬货兼职", Group = "money", MinDay = 4, CooldownDays = 30, Weight = 6,
                Body = $"朋友今天缺人帮忙搬货。现在开工，干满 {GameSession.WeekendWorkHours} 小时后结算 ¥{GameSession.WeekendWorkPay}。",
                When = s => s.CanStartWeekendWork,
                Choices =
                {
                    new EventChoice
                    {
                        Text = $"开工 · {GameSession.WeekendWorkHours} 小时",
                        Enabled = s => s.CanStartWeekendWork,
                        DisabledReason = "周末白天且空闲时可以接班",
                        Apply = s => { s.StartWeekendWork(); return null; },
                    },
                    new EventChoice { Text = "不去", Apply = _ => "你婉拒了这次兼职。" },
                },
            },
            new EventDef { Id = "inspection", Title = "市场监管来检查", When = s => s.InspectionShop() != null },
            new EventDef { Id = "regulars", Title = "熟客想带朋友", MinDay = 3, CooldownDays = 6, Weight = 10, When = s => s.ReferralShop() != null },
            new EventDef { Id = "thief", Title = "货少了", When = s => s.DiscrepancyShop() != null },
            new EventDef { Id = "students", Title = "学生团购", MinDay = 6, CooldownDays = 12, Weight = 9, When = s => s.RetailStoryTarget("students") != null },
            new EventDef { Id = "rival", Title = "同行价格战", MinDay = 7, CooldownDays = 14, Weight = 8, When = s => s.EventPromotion() != null },
            new EventDef { Id = "wallet", Title = "捡到钱包", When = s => s.CanFindWallet },
            new EventDef { Id = "friend_meal", Title = "老同学请吃饭", When = s => s.CanOfferClassmateDinner },
            new EventDef { Id = "lend", Title = "亲戚来借钱", When = s => s.CanOfferRelativeLoan },
            new EventDef { Id = "classmate_help", Title = "老同学雪中送炭", Group = "npc",
                When = s => s.NegativeCashDays >= 3 && s.Ledger.Balance.IsNegative },
            new EventDef
            {
                Id = "review", Title = "网上有人说你家卖翻新货", MinDay = 10, CooldownDays = 14, Weight = 7,
                When = s => s.RetailStoryTarget("review")!=null,
                Body = "有人在本地论坛发帖，说你家的配件是翻新的，帖子下面已经有人跟帖。",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "公开回应，晒进货单（看魅力和智力）",
                        Apply = s =>
                        {
                            if (s.Random.Chance(0.35 + s.EffectiveCharm * 0.008 + s.Player.Intelligence * 0.004))
                            {
                                s.PlayerShop.AdjustShopReputation(1.5);
                                return "你晒出档口的进货单，网友反而夸你实在。口碑 +1.5。";
                            }
                            s.PlayerShop.AdjustShopReputation(-1);
                            return "回应没说到点子上，被截图嘲了。口碑 -1。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "私信发帖人，送他一份小礼物（¥100）",
                        Enabled = s => CanPay(s, 100), DisabledReason = "钱不够",
                        Apply = s =>
                        {
                            s.EventPay(Y(100), "小礼物");
                            return "发帖人不好意思地删了帖。花了 ¥100，事情平息了。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "无视",
                        Apply = s =>
                        {
                            s.PlayerShop.AdjustShopReputation(-2);
                            return "帖子越传越广，口碑 -2。";
                        },
                    },
                },
            },
        };

        /// <summary>内容写在 C# 里的事件（要绑定具体店铺、检查货架等，json 表达不了）。顺序和数值在 Resources/Data/events.json 里调。</summary>
        public static IReadOnlyList<EventDef> CodeEvents => Base.Concat(new[] { NpcEvents.ZhouDeal }).ToList();

        /// <summary>全部事件，顺序和数值以 Resources/Data/events.json 为准。</summary>
        public static IReadOnlyList<EventDef> All => _all ?? (_all = EventLoader.Load());
        private static IReadOnlyList<EventDef> _all;

        /// <summary>events.json 的文本从哪来。Game 层启动时设置（读 Resources），Core 自己不能引用 Unity。</summary>
        public static System.Func<string> JsonSource { get; set; }

        /// <summary>上次加载 events.json 发现的格式问题（空 = 没问题）。</summary>
        public static IReadOnlyList<string> LoadErrors { get; internal set; } = new List<string>();

        /// <summary>改了 events.json 之后重新读（编辑器里用）。</summary>
        public static void Reload() => _all = null;

        public static EventDef Get(string id) => All.FirstOrDefault(e => e.Id == id);
    }
}
