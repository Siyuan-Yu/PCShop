using System.Collections.Generic;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 固定出场的人物。每个人有一个 0~100 的"交情"，靠随机事件里的选择和送礼来涨，不管就慢慢淡。
    /// 交情到了门槛会给实打实的好处（见 <see cref="NpcPerks"/>）。数值和文案都是占位稿。
    /// </summary>
    public sealed class NpcDef
    {
        public string Id { get; }
        public string Name { get; }
        public string Role { get; }
        public string Intro { get; }
        public string GiftName { get; }
        public Money GiftPrice { get; }
        public string GiftReply { get; }

        /// <summary>如果是档口老板，这里是他的档口 ID。</summary>
        public string StallId { get; }

        /// <summary>他透露的行情消息有多大概率是真的（0 表示不透露消息）。交情越高越实在。</summary>
        public double Reliability { get; }

        /// <summary>最喜欢的活动 ID（一起出去玩时交情涨 1.5 倍）。</summary>
        public string Likes { get; }

        /// <summary>第一次碰面的开场白。</summary>
        public string FirstMeet { get; }

        public bool IsTipster => Reliability > 0;

        public NpcDef(string id, string name, string role, string intro, string giftName, long giftYuan, string giftReply,
            string stallId = null, double reliability = 0, string likes = null, string firstMeet = null)
        {
            Id = id;
            Name = name;
            Role = role;
            Intro = intro;
            GiftName = giftName;
            GiftPrice = Money.FromYuan(giftYuan);
            GiftReply = giftReply;
            StallId = stallId;
            Reliability = reliability;
            Likes = likes;
            FirstMeet = firstMeet;
        }
    }

    public static class Npcs
    {
        public const string Zhang = "zhang";
        public const string Landlady = "landlady";
        public const string Zhou = "zhou";
        public const string Jie = "jie";
        public const string Lin = "lin";
        public const string Li = "li";
        public const string Wang = "wang";
        public const string Qian = "qian";
        public const string Sun = "sun";

        public const int MaxRelation = 100;

        /// <summary>送礼后几天内不能再送同一个人。</summary>
        public const int GiftCooldownDays = 5;

        public const double GiftGain = 3.0;

        public static readonly IReadOnlyList<NpcDef> All = new[]
        {
            new NpcDef(Zhang, "老张", "熟客，附近中学的老师", "第一个常来的熟客，认准你这家店。", "一条好烟", 100, "老张笑得合不拢嘴：\"你这小子，会来事。\"", likes: Activities.Hike),
            new NpcDef(Landlady, "李姐", "房东，住楼上", "你的店面和出租屋都是她的。", "一箱水果", 120, "李姐掂了掂水果：\"哎呀，这么客气，下个月的事好商量。\"", likes: Activities.Dinner),
            new NpcDef(Zhou, "老周", "电子市场老货商", "在 1788 上开店的老货商，认识半个市场。", "两瓶好酒", 200, "老周拍拍你肩膀：\"后生可畏，以后进货找我，给你实价。\"", reliability: 0.75, likes: Activities.Dinner),
            new NpcDef(Jie, "阿杰", "隔壁手机店老板", "年纪相仿的同行，嘴上不饶人，心里有数。", "一顿烧烤", 150, "阿杰咬着串：\"行，以后有事言语一声。\"", reliability: 0.50, likes: Activities.Ktv),
            new NpcDef(Li, "老李", "CPU 档口老板", "市场里最老的档口，做 CPU 和主板二十年了，说话慢，但很少看走眼。", "一条好烟", 150,
                "老李接过烟，在耳朵上夹了一下：\"小伙子，下次给你留好货。\"", Stalls.Cpu, 0.80, Activities.Fishing,
                "老李眯着眼打量你：\"新面孔？先看货，别急。\""),
            new NpcDef(Wang, "阿王", "显卡王档口老板", "显卡档口的大嗓门，消息灵通，不过嘴上没把门，说的不一定准。", "一箱饮料", 100,
                "阿王哈哈大笑：\"够意思！以后显卡找我，价钱好说！\"", Stalls.Gpu, 0.55, Activities.Ktv,
                "阿王扯着嗓子：\"来来来，显卡看一看！新来的？给你个好价！\""),
            new NpcDef(Qian, "钱姐", "存储专营老板娘", "内存和硬盘档口的老板娘，精明细致，账目清楚。", "一盒点心", 120,
                "钱姐笑着收下：\"还挺会来事，以后多照顾你。\"", Stalls.Memory, 0.70, Activities.Dinner,
                "钱姐头也不抬地敲着计算器：\"要什么货？先说清楚数量。\""),
            new NpcDef(Sun, "老孙", "机箱电源城老板", "做机箱电源的实在人，货量大，价格死板，但从不坑人。", "两瓶白酒", 160,
                "老孙搓搓手：\"这怎么好意思，下回来我给你算便宜点。\"", Stalls.PowerCase, 0.65, Activities.Snack,
                "老孙从货堆后探出头：\"电源机箱？这边，实在价，不二价。\""),
            new NpcDef(Lin, "小林", "发小，在外面打工", "从小一起长大的朋友，听说你开了店就常来看你。", "一顿夜宵", 100, "小林边吃边笑：\"还是你够意思。\"", likes: Activities.Snack),
        };

        public static NpcDef Get(string id)
        {
            foreach (NpcDef n in All)
                if (n.Id == id) return n;
            return null;
        }

        /// <summary>某个档口的老板（没有则 null）。</summary>
        public static NpcDef OwnerOf(string stallId)
        {
            foreach (NpcDef n in All)
                if (n.StallId != null && n.StallId == stallId) return n;
            return null;
        }

        public static string LevelName(int relation)
        {
            if (relation >= 80) return "铁哥们";
            if (relation >= 60) return "好朋友";
            if (relation >= 40) return "有交情";
            if (relation >= 20) return "熟人";
            return "脸熟";
        }
    }

    /// <summary>交情带来的好处。门槛集中放这里，方便平衡。</summary>
    public static class NpcPerks
    {
        public static double TrafficBonus(int zhang) => zhang >= 70 ? 1.10 : zhang >= 40 ? 1.05 : 1.0;

        /// <summary>房租折扣（0.12 = 九折以下）。</summary>
        public static double RentDiscount(int landlady) => landlady >= 70 ? 0.12 : landlady >= 40 ? 0.05 : 0.0;

        /// <summary>1788 进货价折扣。</summary>
        public static double OnlineDiscount(int zhou) => zhou >= 70 ? 0.06 : zhou >= 40 ? 0.03 : 0.0;

        /// <summary>档口批发价折扣。</summary>
        public static double StallDiscount(int jie) => jie >= 80 ? 0.04 : jie >= 50 ? 0.02 : 0.0;

        /// <summary>档口老板的进价折扣（和阿杰的折扣叠加）。</summary>
        public static double OwnerDiscount(int rel) => rel >= 80 ? 0.06 : rel >= 60 ? 0.04 : rel >= 40 ? 0.025 : rel >= 20 ? 0.01 : 0.0;

        /// <summary>档口老板每样配件每天肯卖给你的数量上限。交情越高，货越肯多给。</summary>
        public static int StallQuota(int rel) => rel >= 80 ? 45 : rel >= 60 ? 28 : rel >= 40 ? 18 : rel >= 20 ? 12 : 8;

        /// <summary>阿杰到了这个交情就不会跟你打价格战。</summary>
        public const int JieNoPriceWar = 50;

        /// <summary>小林到了这个交情，你快破产时他会救急一次。</summary>
        public const int LinBailout = 50;

        public static readonly Money BailoutAmount = Money.FromYuan(3000);

        public static string Describe(string npcId)
        {
            switch (npcId)
            {
                case Npcs.Zhang: return "40：客流 +5%　70：客流 +10%";
                case Npcs.Landlady: return "40：店租 -5%　70：店租 -12%";
                case Npcs.Zhou: return "40：1788 进货 -3%　70：-6%。会透露行情";
                case Npcs.Jie: return "50：不再打价格战，档口批发价 -2%　80：-4%。会透露行情（不太靠谱）";
                case Npcs.Lin: return "50：快破产时借你 ¥3,000 救急（仅一次）";
                case Npcs.Li:
                case Npcs.Wang:
                case Npcs.Qian:
                case Npcs.Sun:
                    return "20：进价 -1%，每样每天 12 个　40：-2.5%，18 个　60：-4%，28 个　80：-6%，45 个。会透露行情";
                default: return "";
            }
        }
    }

    /// <summary>档口 ID（和 city_layout.json 对应）。</summary>
    public static class Stalls
    {
        public const string Cpu = "stall_cpu";
        public const string Gpu = "stall_gpu";
        public const string Memory = "stall_mem";
        public const string PowerCase = "stall_psu";
    }
}