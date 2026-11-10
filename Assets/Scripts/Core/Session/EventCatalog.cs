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
        public const string EmergencyLightFlag = "emergency_light";

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
                Id = "regulars", Title = "熟客想带朋友", MinDay = 3, CooldownDays = 6, Weight = 10,
                When = s => s.RetailStoryTarget("regulars")!=null,
                Body = "常来的一位熟客说：\"下回我带几个同学一起来，都要配电脑。\"",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "约一次真实饭局（到场后消费）",
                        Enabled = s => CanPay(s, 100), DisabledReason = "预计家常饭费用不足",
                        Apply = s =>
                        {
                            return s.ScheduleLegacyCityMeeting(Npcs.Zhang,"dinner","legacy-regulars-meal:"+s.Clock.Today.TotalDays);
                        },
                    },
                    new EventChoice
                    {
                        Text = "客气两句就好",
                        Apply = s =>
                        {
                            s.PlayerShop.AdjustShopReputation(.5);
                            return "熟客笑笑：\"行，老板实在。\" 口碑 +0.5。";
                        },
                    },
                },
            },
            new EventDef
            {
                Id = "students", Title = "学生团购", MinDay = 6, CooldownDays = 12, Weight = 9,
                When = s => s.RetailStoryTarget("students")!=null,
                Body = "几个学生凑在一起，想一次买 5 条「入门内存」装宿舍的电脑，问你能不能给个团购价。",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "按市价 95 折卖给他们",
                        Apply = s =>
                        {
                            PartDef p = PartCatalog.Get("ram0");
                            for (int i = 0; i < 5; i++) s.PlayerShop.Stock.TakeOne(p);
                            Money total = (s.Market.PriceOf(p) * 5).Scale(0.95);
                            s.EventGain(total, "学生团购");
                            s.PlayerShop.Reputation += 1;
                            return $"5 条内存一次出手，收入 {total}，名望 +1。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "一口价，不讲价（按市价加 10%）",
                        Apply = s =>
                        {
                            if (!s.Random.Chance(0.5)) return "学生们商量了一下，说回去再想想，走了。";
                            PartDef p = PartCatalog.Get("ram0");
                            for (int i = 0; i < 5; i++) s.PlayerShop.Stock.TakeOne(p);
                            Money total = (s.Market.PriceOf(p) * 5).Scale(1.10);
                            s.EventGain(total, "学生团购");
                            return $"他们咬咬牙接受了，收入 {total}。";
                        },
                    },
                    new EventChoice { Text = "不接", Apply = s => "你说库存要留给零售顾客，学生们嘀咕着走了。" },
                },
            },
            new EventDef
            {
                Id = "rival", Title = "隔壁打价格战", MinDay = 7, CooldownDays = 14, Weight = 8,
                When = s => s.PriceWarTarget()!=null,
                Body = "同商圈同行正在促销，有顾客询问是否跟进价格。",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "跟着降价（加价倍率 -0.10）",
                        Apply = s =>
                        {
                            s.PlayerShop.Markup -= 0.10;
                            return $"你把加价倍率调到了 {s.PlayerShop.Markup:0.00}×。薄利多销。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "和同行沟通（看魅力）",
                        Apply = s =>
                        {
                            if (s.Random.Chance(0.3 + s.EffectiveCharm * 0.012))
                            {
                                s.PlayerShop.AdjustShopReputation(1);
                                return "两边说开了，各卖各的，没再恶性竞争。口碑 +1。";
                            }
                            s.PlayerShop.AdjustShopReputation(-1);
                            return "老板阴阳怪气，聊崩了。口碑 -1。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "不理他",
                        Apply = s =>
                        {
                            s.PlayerShop.AdjustShopReputation(-.5);
                            return "有几个顾客转身去了隔壁。口碑 -0.5。";
                        },
                    },
                },
            },
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
