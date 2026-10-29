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
                Id = "inspection", Title = "市场监管来检查", MinDay = 5, CooldownDays = 20, Weight = 8,
                Body = "两个穿制服的人走进店里，说是例行检查：消防、线路、进货单据都要看。",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "配合整改（¥300，买灭火器、理线）",
                        Enabled = s => CanPay(s, 300), DisabledReason = "钱不够",
                        Apply = s =>
                        {
                            s.EventPay(Y(300), "整改");
                            s.PlayerShop.Reputation += 1;
                            return "检查员点点头：\"态度不错，下次注意。\" 花了 ¥300，口碑 +1。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "塞个红包（¥150）",
                        Enabled = s => CanPay(s, 150), DisabledReason = "钱不够",
                        Apply = s =>
                        {
                            s.EventPay(Y(150), "红包");
                            if (s.Random.Chance(0.6)) return "对方收下，转身走了。花了 ¥150。";
                            s.EventPay(Y(600), "罚款");
                            s.PlayerShop.Reputation -= 2;
                            return "对方当场变脸，开了罚单。红包没收，罚款 ¥600，口碑 -2。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "说好话糊弄过去",
                        Apply = s =>
                        {
                            if (s.Random.Chance(0.35 + s.EffectiveCharm * 0.01)) return "你笑着递烟递水，对方没多为难就走了。";
                            s.EventPay(Y(500), "罚款");
                            s.PlayerShop.Reputation -= 2;
                            return "没糊弄过去，被罚款 ¥500，口碑 -2。";
                        },
                    },
                },
            },
            new EventDef
            {
                Id = "regulars", Title = "熟客想带朋友", MinDay = 3, CooldownDays = 6, Weight = 10,
                When = s => s.PlayerShop.Reputation >= 3,
                Body = "常来的一位熟客说：\"下回我带几个同学一起来，都要配电脑。\"",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "请他们吃顿饭（¥80）",
                        Enabled = s => CanPay(s, 80), DisabledReason = "钱不够",
                        Apply = s =>
                        {
                            s.EventPay(Y(80), "请客");
                            s.PlayerShop.Reputation += 3;
                            return "一顿饭下来都成了朋友。口碑 +3。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "客气两句就好",
                        Apply = s =>
                        {
                            s.PlayerShop.Reputation += 0.5;
                            return "熟客笑笑：\"行，老板实在。\" 口碑 +0.5。";
                        },
                    },
                },
            },
            new EventDef
            {
                Id = "blackout", Title = "停电了", MinDay = 3, CooldownDays = 25, Weight = 8,
                When = s => !s.Flags.Contains(EmergencyLightFlag),
                Body = "整条街突然停电，店里一片漆黑。听说要修一两个小时。",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "买盏应急灯（¥150，以后停电不怕了）",
                        Enabled = s => CanPay(s, 150), DisabledReason = "钱不够",
                        Apply = s =>
                        {
                            s.EventPay(Y(150), "应急灯");
                            s.Flags.Add(EmergencyLightFlag);
                            s.PlayerShop.Reputation += 0.5;
                            return "有了应急灯，顾客觉得这家店靠谱。以后停电不会再影响你。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "趁机歇两个钟头",
                        Apply = s =>
                        {
                            s.EventRest(120);
                            s.PlayerShop.Reputation -= 0.3;
                            return "你靠在椅子上打了个盹，缓过来不少。两小时过去了。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "点蜡烛硬撑",
                        Apply = s =>
                        {
                            if (s.Random.Chance(0.3))
                            {
                                s.EventPay(Y(200), "小火警");
                                return "蜡烛烧着了纸箱，扑灭了但烧坏一些东西，损失 ¥200。";
                            }
                            return "烛光里居然还卖出去一单，气氛不错。";
                        },
                    },
                },
            },
            new EventDef
            {
                Id = "thief", Title = "货少了", MinDay = 4, CooldownDays = 15, Weight = 7,
                When = s => !s.HasUpgrade(ShopUpgrades.Cctv) && !s.PlayerShop.Stock.IsEmpty,
                Body = "你盘点货架，发现好像少了东西，可能被人顺手牵羊了。没有监控，没法确认。",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "报警",
                        Apply = s =>
                        {
                            if (s.Random.Chance(0.3)) return "警察问了一圈，说查到了线索，东西找回来了。";
                            string lost = LoseRandomItem(s);
                            return lost == null ? "警察没查出什么。" : $"警察登记了一下，没下文。丢了「{lost}」一件。（装个监控就不会再有这种事）";
                        },
                    },
                    new EventChoice
                    {
                        Text = "算了，吃个亏",
                        Apply = s =>
                        {
                            string lost = LoseRandomItem(s);
                            return lost == null ? "好在没丢什么。" : $"丢了「{lost}」一件。（装个监控就不会再有这种事）";
                        },
                    },
                },
            },
            new EventDef
            {
                Id = "students", Title = "学生团购", MinDay = 6, CooldownDays = 12, Weight = 9,
                When = s => s.PlayerShop.Reputation >= 2 && s.PlayerShop.Stock.CountAll(PartCatalog.Get("ram0")) >= 5,
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
                            return $"5 条内存一次出手，收入 {total}，口碑 +1。";
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
                When = s => s.PlayerShop.Markup > 1.2 && s.Relation(Npcs.Jie) < NpcPerks.JieNoPriceWar,
                Body = "隔壁电脑店在门口挂出了特价牌，写着\"全场比别家便宜一成\"。有顾客进来先问你能不能也降。",
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
                        Text = "去找老板当面聊聊（看魅力）",
                        Apply = s =>
                        {
                            if (s.Random.Chance(0.3 + s.EffectiveCharm * 0.012))
                            {
                                s.PlayerShop.Reputation += 1;
                                return "两边说开了，各卖各的，没再恶性竞争。口碑 +1。";
                            }
                            s.PlayerShop.Reputation -= 1;
                            return "老板阴阳怪气，聊崩了。口碑 -1。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "不理他",
                        Apply = s =>
                        {
                            s.PlayerShop.Reputation -= 0.5;
                            return "有几个顾客转身去了隔壁。口碑 -0.5。";
                        },
                    },
                },
            },
            new EventDef
            {
                Id = "wallet", Title = "捡到钱包", MinDay = 2, CooldownDays = 12, Weight = 8,
                Body = "你在店门口捡到一个钱包，里面有 ¥300 现金和一张学生证。",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "按学生证联系失主还回去",
                        Apply = s =>
                        {
                            s.Player.Charm = System.Math.Min(100, s.Player.Charm + 1);
                            s.PlayerShop.Reputation += 2;
                            s.EventGain(Y(50), "失主酬谢");
                            return "失主赶来道谢，硬塞给你 ¥50。事情传开了，口碑 +2，魅力 +1。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "把钱留下",
                        Apply = s =>
                        {
                            s.EventGain(Y(300), "捡到的钱");
                            if (s.Random.Chance(0.25))
                            {
                                s.PlayerShop.Reputation -= 4;
                                return "收了 ¥300，但被对面的老板看见，传得街上都知道了。口碑 -4。";
                            }
                            return "收了 ¥300，没人看见。你心里有点不踏实。";
                        },
                    },
                },
            },
            new EventDef
            {
                Id = "friend_meal", Title = "老同学请吃饭", MinDay = 5, CooldownDays = 15, Weight = 7,
                When = s => CanPay(s, 120),
                Body = "大学同学打来电话：\"来电脑城了？晚上一起吃个饭，聊聊。\"",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "去（¥120，两小时）",
                        Enabled = s => CanPay(s, 120), DisabledReason = "钱不够",
                        Apply = s =>
                        {
                            s.EventPay(Y(120), "饭局");
                            s.EventRest(120);
                            s.Player.Hunger = System.Math.Min(100, s.Player.Hunger + 30);
                            s.Player.Charm = System.Math.Min(100, s.Player.Charm + 1);
                            s.Player.Intelligence = System.Math.Min(100, s.Player.Intelligence + 1);
                            return "饭桌上聊起各自的生意，听到不少行情，也放松了一下。魅力 +1，智力 +1。";
                        },
                    },
                    new EventChoice { Text = "推掉，店里忙", Apply = s => "你说这周走不开。同学说下次约。" },
                },
            },
            new EventDef
            {
                Id = "lend", Title = "亲戚来借钱", MinDay = 8, CooldownDays = 20, Weight = 6,
                When = s => CanPay(s, 2000),
                Body = "远房表哥找上门，说周转不开，想借 ¥800，三天就还。",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "借给他（¥800）",
                        Apply = s =>
                        {
                            s.EventPay(Y(800), "借给表哥");
                            if (s.Random.Chance(0.75))
                            {
                                s.ScheduleCredit(3, Y(800), "表哥还钱");
                                return "表哥千恩万谢地走了，说三天后一定还。";
                            }
                            return "表哥拿了钱就走，之后再没提这事。";
                        },
                    },
                    new EventChoice { Text = "婉拒", Apply = s => "你说店里也周转不开。表哥脸色不太好看地走了。" },
                },
            },
            new EventDef
            {
                Id = "review", Title = "网上有人说你家卖翻新货", MinDay = 10, CooldownDays = 14, Weight = 7,
                When = s => s.PlayerShop.Reputation >= 5,
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
                                s.PlayerShop.Reputation += 1.5;
                                return "你晒出档口的进货单，网友反而夸你实在。口碑 +1.5。";
                            }
                            s.PlayerShop.Reputation -= 1;
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
                            s.PlayerShop.Reputation -= 2;
                            return "帖子越传越广，口碑 -2。";
                        },
                    },
                },
            },
            new EventDef
            {
                Id = "flyer", Title = "发传单的上门了", MinDay = 2, CooldownDays = 10, Weight = 9,
                Body = "一个做地推的小伙子问：\"老板，花 ¥100 帮你在附近大学门口发一天传单，要不要？\"",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "来一天（¥100）",
                        Enabled = s => CanPay(s, 100), DisabledReason = "钱不够",
                        Apply = s =>
                        {
                            s.EventPay(Y(100), "传单");
                            s.PlayerShop.Reputation += 2;
                            return "第二天有几个学生拿着传单进店。口碑 +2。";
                        },
                    },
                    new EventChoice { Text = "不用", Apply = s => "小伙子点点头，去下一家了。" },
                },
            },
            new EventDef
            {
                Id = "cold", Title = "身体不舒服", MinDay = 3, CooldownDays = 12, Weight = 9,
                When = s => s.Player.Energy < 45 || s.Player.SleepDebt > 2 || s.Player.Health < 85,
                Body = "你从早上就觉得头沉、嗓子疼，像是要感冒。",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "去诊所看看（¥150）",
                        Enabled = s => CanPay(s, 150), DisabledReason = "钱不够",
                        Apply = s =>
                        {
                            s.EventPay(Y(150), "诊所");
                            s.Player.Health = System.Math.Min(100, s.Player.Health + 20);
                            return "医生开了药，叮嘱你别熬夜。健康 +20。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "吃片药硬扛（¥20）",
                        Enabled = s => CanPay(s, 20), DisabledReason = "钱不够",
                        Apply = s =>
                        {
                            s.EventPay(Y(20), "感冒药");
                            s.Player.Health = System.Math.Max(0, s.Player.Health - 5);
                            s.Player.Energy = System.Math.Max(0, s.Player.Energy - 10);
                            return "药劲上来昏昏沉沉的，健康 -5，精力 -10。";
                        },
                    },
                    new EventChoice
                    {
                        Text = "咬牙硬挺",
                        Apply = s =>
                        {
                            s.Player.Health = System.Math.Max(0, s.Player.Health - 10);
                            s.Player.Energy = System.Math.Max(0, s.Player.Energy - 10);
                            return "没花钱，但整天头昏脑涨。健康 -10，精力 -10。";
                        },
                    },
                },
            },
        };

        /// <summary>全部事件：通用事件 + 围绕固定人物的事件。</summary>
        public static readonly IReadOnlyList<EventDef> All = Base.Concat(NpcEvents.All).ToList();

        public static EventDef Get(string id) => All.FirstOrDefault(e => e.Id == id);
    }
}
