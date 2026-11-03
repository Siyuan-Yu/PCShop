using System;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    /// <summary>围绕固定人物的随机事件：初次见面 + 后续往来。文案是占位稿。</summary>
    public static class NpcEvents
    {
        private static Money Y(long yuan) => Money.FromYuan(yuan);

        private static bool CanPay(GameSession s, long yuan) => s.Ledger.Balance >= Y(yuan);

        private static EventChoice Free(string text, Func<GameSession, string> apply) => new EventChoice { Text = text, Apply = apply };

        private static EventChoice Paid(string text, long yuan, Func<GameSession, string> apply) => new EventChoice
        {
            Text = $"{text}（¥{yuan:N0}）",
            Enabled = s => CanPay(s, yuan),
            DisabledReason = "钱不够",
            Apply = apply,
        };

        public static readonly IReadOnlyList<EventDef> All = new List<EventDef>
        {
            // ---------- 初次见面 ----------
            new EventDef
            {
                Id = "intro_zhang", Title = "戴眼镜的熟客", MinDay = 3, CooldownDays = 9999, Weight = 30,
                When = s => s.CitySamePlace(Npcs.Zhang) && !s.IsMet(Npcs.Zhang),
                Body = "一位戴眼镜的中年人在店里转了半天，终于开口：\"小伙子，我是隔壁中学的张绍文，学校机房有几台电脑老坏，你这儿能修吗？\"",
                Choices =
                {
                    Free("热情招呼，泡杯茶慢慢聊", s =>
                    {
                        s.Meet(Npcs.Zhang, 22);
                        s.PlayerShop.Reputation += 0.5;
                        return "张绍文说你实在，留了电话。以后他就是你的熟客了。（认识了张绍文）";
                    }),
                    Free("简单介绍几款入门配置", s =>
                    {
                        s.Meet(Npcs.Zhang, 16);
                        return "张绍文记下了几个型号，说回去和同事商量。（认识了张绍文）";
                    }),
                    Free("店里忙，随口应付", s =>
                    {
                        s.Meet(Npcs.Zhang, 8);
                        return "张绍文点点头走了，看起来不太热络。（认识了张绍文）";
                    }),
                },
            },
            new EventDef
            {
                Id = "intro_landlady", Title = "房东上门", MinDay = 6, CooldownDays = 9999, Weight = 30,
                When = s => s.CitySamePlace(Npcs.Landlady) && !s.IsMet(Npcs.Landlady),
                Body = "一位烫着卷发的中年女人拎着钥匙串上门：\"我是李秀兰，你这铺面和楼上的出租屋都是我的。来看看租客过得怎么样。\"",
                Choices =
                {
                    Free("递上一杯茶，请她坐", s =>
                    {
                        s.Meet(Npcs.Landlady, 22);
                        return "李秀兰喝着茶打量店面：\"小伙子挺勤快的，我喜欢准时交租的。\"（认识了李秀兰）";
                    }),
                    Free("保证每个月准时交租", s =>
                    {
                        s.Meet(Npcs.Landlady, 16);
                        return "李秀兰点点头：\"那就好，别拖我房租。\"（认识了李秀兰）";
                    }),
                    Free("说要去进货，匆匆出门", s =>
                    {
                        s.Meet(Npcs.Landlady, 8);
                        return "李秀兰在门口撇了撇嘴。（认识了李秀兰）";
                    }),
                },
            },
            new EventDef
            {
                Id = "intro_lin", Title = "发小来看你", MinDay = 5, CooldownDays = 9999, Weight = 30,
                When = s => s.CitySamePlace(Npcs.Lin) && !s.IsMet(Npcs.Lin),
                Body = "门口探进一个熟悉的脑袋：\"我听说你在电脑城开店了，来看看你！\" 是你从小一起长大的林远航。",
                Choices =
                {
                    Paid("拉他去吃顿好的", 60, s =>
                    {
                        s.EventPay(Y(60), "请发小");
                        s.Meet(Npcs.Lin, 30);
                        return "两个人边吃边聊起小时候的事。林远航说：\"以后有事儿找我。\"（认识了林远航）";
                    }),
                    Free("在店里聊几句", s =>
                    {
                        s.Meet(Npcs.Lin, 20);
                        return "林远航拍拍你肩膀：\"别太累了，注意身体。\"（认识了林远航）";
                    }),
                },
            },

            // ---------- 张绍文 ----------
            new EventDef
            {
                Id = "zhang_visit", Title = "张绍文来闲坐", MinDay = 5, CooldownDays = 12, Weight = 10,
                When = s => s.CitySamePlace(Npcs.Zhang) && s.IsMet(Npcs.Zhang) && s.Relation(Npcs.Zhang) < 95,
                Body = "张绍文下课后晃进店里，往椅子上一坐：\"今天没什么事，来你这儿歇会儿。\"",
                Choices =
                {
                    Free("约到家常馆慢慢聊（实际赴约）", s =>
                    {
                        return s.ScheduleLegacyCityMeeting(Npcs.Zhang,"dinner","legacy-zhang-chat:"+s.Clock.Today.TotalDays);
                    }),
                    Free("约好一起去市场看样机", s =>
                    {
                        return s.ScheduleLegacyCityMeeting(Npcs.Zhang,"market","legacy-zhang-market:"+s.Clock.Today.TotalDays);
                    }),
                    Free("说店里忙，请他改天", s =>
                    {
                        s.AddRelation(Npcs.Zhang, -1);
                        return "张绍文讪讪地走了。交情 -1。";
                    }),
                },
            },
            new EventDef
            {
                Id = "zhang_family", Title = "张绍文介绍亲戚", MinDay = 20, CooldownDays = 40, Weight = 8,
                When = s => s.CitySamePlace(Npcs.Zhang) && s.Relation(Npcs.Zhang) >= 40,
                Body = "张绍文领着一位亲戚进店：\"我外甥要配台电脑上大学，你给个实在价。\"",
                Choices =
                {
                    Free("登记熟客报价的真实订单", s =>
                    {
                        return s.OfferCityReferralContract("zhang_family","张绍文介绍的学生",1,true);
                    }),
                    Free("登记正常报价的真实订单", s =>
                    {
                        return s.OfferCityReferralContract("zhang_family","张绍文介绍的学生",1,false);
                    }),
                },
            },

            // ---------- 李秀兰 ----------
            new EventDef
            {
                Id = "landlady_rent", Title = "房东想涨租", MinDay = 60, CooldownDays = 90, Weight = 8,
                When = s => s.CitySamePlace(Npcs.Landlady) && s.IsMet(Npcs.Landlady) && s.RentHikes < 3 && s.Relation(Npcs.Landlady) < 70,
                Body = "李秀兰在门口叹气：\"这边房价又涨了，我这铺面租金也得跟着涨涨了……\"",
                Choices =
                {
                    Free("预约饭局，再当面聊租约", s =>
                    {
                        return s.ScheduleLegacyCityMeeting(Npcs.Landlady,"dinner","legacy-landlady-rent:"+s.Clock.Today.TotalDays);
                    }),
                    Free("跟她讨价还价", s =>
                    {
                        double chance = 0.30 + s.EffectiveCharm * 0.01 + s.Relation(Npcs.Landlady) * 0.004;
                        if (s.Random.Chance(chance))
                        {
                            s.AddRelation(Npcs.Landlady, 2);
                            return "你说得有理有据，李秀兰笑着摆摆手：\"好好好，这次先不涨。\"";
                        }
                        s.AddRelation(Npcs.Landlady, -3);
                        s.AddRentHike();
                        return "没说服她，店租上涨 10%。交情 -3。";
                    }),
                    Free("认了，涨就涨吧", s =>
                    {
                        s.AddRelation(Npcs.Landlady, 1);
                        s.AddRentHike();
                        return "店租上涨 10%。李秀兰拍拍你肩膀：\"小伙子懂事。\" 交情 +1。";
                    }),
                },
            },
            new EventDef
            {
                Id = "landlady_help", Title = "李秀兰介绍大客户", MinDay = 90, CooldownDays = 120, Weight = 6,
                When = s => s.CitySamePlace(Npcs.Landlady) && s.Relation(Npcs.Landlady) >= 55,
                Body = "李秀兰上楼送了一盘饺子，顺口说：\"我外甥开了家培训机构，要采购一批机器，我让他来找你。\"",
                Choices =
                {
                    Free("谢谢李秀兰，我给最优惠的价", s =>
                    {
                        return s.OfferCityReferralContract("landlady_training","李秀兰介绍的培训机构",3,true);
                    }),
                    Free("按市价来，不欠人情", s =>
                    {
                        return s.OfferCityReferralContract("landlady_training","李秀兰介绍的培训机构",3,false);
                    }),
                },
            },

            // ---------- 周维成 ----------
            new EventDef
            {
                Id = "zhou_deal", Title = "周维成出清库存", MinDay = 30, CooldownDays = 30, Weight = 8,
                When = s => s.IsMet(Npcs.Zhou) && s.Relation(Npcs.Zhou) >= 25,
                Body = "周维成发来消息：\"手上压了一批货急着出，一条星耀 RX 660 加两条 16G 内存，打包 ¥1,600，只给你留一天。\"",
                Choices =
                {
                    new EventChoice
                    {
                        Text = "全收了（¥1,600）",
                        Enabled = s => CanPay(s, 1600) && s.PlayerShop.Stock.FreeSpace >= DealSpace(),
                        DisabledReason = "钱不够，或者店里放不下",
                        Apply = s =>
                        {
                            s.EventPay(Y(1600), "周维成的货");
                            s.PlayerShop.Stock.ForceAdd(PartCatalog.Get("gpu1"), true, 1);
                            s.PlayerShop.Stock.ForceAdd(PartCatalog.Get("ram1"), true, 2);
                            s.AddRelation(Npcs.Zhou, 4);
                            return "货第二天就到了，比市价便宜一大截。交情 +4。";
                        },
                    },
                    Free("这次算了", s =>
                    {
                        s.AddRelation(Npcs.Zhou, -1);
                        return "周维成回了个“好”。";
                    }),
                },
            },

            // ---------- 唐俊杰 ----------
            new EventDef
            {
                Id = "jie_loan", Title = "唐俊杰周转不开", MinDay = 20, CooldownDays = 40, Weight = 8,
                When = s => s.CitySamePlace(Npcs.Jie) && s.IsMet(Npcs.Jie) && s.Relation(Npcs.Jie) >= 15,
                Body = "唐俊杰一脸愁容过来：\"这月进货款差一点，能不能先借我 ¥1,000？十天内还你。\"",
                Choices =
                {
                    Paid("借给他", 1000, s =>
                    {
                        s.EventPay(Y(1000), "借给唐俊杰");
                        s.ScheduleCredit(10, Y(1100), "唐俊杰还钱（含谢礼）");
                        s.AddRelation(Npcs.Jie, 8);
                        return "唐俊杰感激不尽：\"十天后连本带利还你。\" 交情 +8。";
                    }),
                    Free("自己也紧，抱歉", s =>
                    {
                        s.AddRelation(Npcs.Jie, -3);
                        return "唐俊杰笑了笑说没事，但神情有点失落。交情 -3。";
                    }),
                },
            },
            new EventDef
            {
                Id = "jie_trade", Title = "唐俊杰缺个配件", MinDay = 40, CooldownDays = 30, Weight = 8,
                When = s => s.CitySamePlace(Npcs.Jie) && s.Relation(Npcs.Jie) >= NpcPerks.JieNoPriceWar && !s.PlayerShop.Stock.IsEmpty,
                Body = "唐俊杰急匆匆过来：\"客人等着要货，我这儿刚好缺一件，把你架上的匀一件给我，我按好价格收！\"",
                Choices =
                {
                    Free("没问题，拿去", s =>
                    {
                        string name = EventCatalog.LoseRandomItem(s);
                        if (name == null) return "架子上空空的，没帮上忙。";
                        s.EventGain(Y(600), "转手给唐俊杰");
                        s.AddRelation(Npcs.Jie, 3);
                        return $"把「{name}」匀给了唐俊杰，收了 ¥600。交情 +3。";
                    }),
                    Free("我也要留着卖", s =>
                    {
                        s.AddRelation(Npcs.Jie, -1);
                        return "唐俊杰摆摆手说没事，去别处问了。";
                    }),
                },
            },

            // ---------- 林远航 ----------
            new EventDef
            {
                Id = "lin_visit", Title = "林远航来吐槽", MinDay = 8, CooldownDays = 15, Weight = 10,
                When = s => s.CitySamePlace(Npcs.Lin) && s.IsMet(Npcs.Lin) && s.Relation(Npcs.Lin) < 95,
                Body = "林远航中午溜出来看你，吐槽了一路老板有多难伺候。",
                Choices =
                {
                    Free("预约一起吃饭（到场后消费）", s =>
                    {
                        return s.ScheduleLegacyCityMeeting(Npcs.Lin,"dinner","legacy-lin-meal:"+s.Clock.Today.TotalDays);
                    }),
                    Free("寒暄两句", s =>
                    {
                        s.AddRelation(Npcs.Lin, 1);
                        return "林远航挥挥手回去上班了。交情 +1。";
                    }),
                    Free("说在忙，下次再聊", s =>
                    {
                        s.AddRelation(Npcs.Lin, -2);
                        return "林远航说了声“行”，有点失落。交情 -2。";
                    }),
                },
            },
            new EventDef
            {
                Id = "lin_loan", Title = "林远航想借钱", MinDay = 40, CooldownDays = 60, Weight = 6,
                When = s => s.CitySamePlace(Npcs.Lin) && s.IsMet(Npcs.Lin) && s.Relation(Npcs.Lin) >= 30,
                Body = "林远航有点不好意思：\"我想报个培训班，缺 ¥2,000，能先借我吗？发了工资就还。\"",
                Choices =
                {
                    Paid("借给他", 2000, s =>
                    {
                        s.EventPay(Y(2000), "借给林远航");
                        s.AddRelation(Npcs.Lin, 10);
                        double repay = 0.75 + (s.Relation(Npcs.Lin) >= 70 ? 0.2 : 0.0);
                        if (s.Random.Chance(repay))
                        {
                            s.ScheduleCredit(25, Y(2200), "林远航还钱（含请客）");
                            return "林远航红着眼眶：\"我记着呢。\" 交情 +10。（约 25 天后有回款）";
                        }
                        return "林远航千恩万谢。交情 +10。（后来听说他手头一直紧，这笔钱恐怕要不回来了）";
                    }),
                    Free("我也不宽裕，抱歉", s =>
                    {
                        s.AddRelation(Npcs.Lin, -4);
                        return "林远航勉强笑了笑：\"没事，我再想别的办法。\" 交情 -4。";
                    }),
                },
            },
        };

        private static double DealSpace() =>
            PCShopSell.Core.Inventory.Stock.SpaceOf(PartCatalog.Get("gpu1"), true, 1)
            + PCShopSell.Core.Inventory.Stock.SpaceOf(PartCatalog.Get("ram1"), true, 2);
    }
}
