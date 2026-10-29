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

        public NpcDef(string id, string name, string role, string intro, string giftName, long giftYuan, string giftReply)
        {
            Id = id;
            Name = name;
            Role = role;
            Intro = intro;
            GiftName = giftName;
            GiftPrice = Money.FromYuan(giftYuan);
            GiftReply = giftReply;
        }
    }

    public static class Npcs
    {
        public const string Zhang = "zhang";
        public const string Landlady = "landlady";
        public const string Zhou = "zhou";
        public const string Jie = "jie";
        public const string Lin = "lin";

        public const int MaxRelation = 100;

        /// <summary>送礼后几天内不能再送同一个人。</summary>
        public const int GiftCooldownDays = 5;

        public const double GiftGain = 3.0;

        public static readonly IReadOnlyList<NpcDef> All = new[]
        {
            new NpcDef(Zhang, "老张", "熟客，附近中学的老师", "第一个常来的熟客，认准你这家店。", "一条好烟", 100, "老张笑得合不拢嘴：\"你这小子，会来事。\""),
            new NpcDef(Landlady, "李姐", "房东，住楼上", "你的店面和出租屋都是她的。", "一箱水果", 120, "李姐掂了掂水果：\"哎呀，这么客气，下个月的事好商量。\""),
            new NpcDef(Zhou, "老周", "电子市场老货商", "在 1788 上开店的老货商，认识半个市场。", "两瓶好酒", 200, "老周拍拍你肩膀：\"后生可畏，以后进货找我，给你实价。\""),
            new NpcDef(Jie, "阿杰", "隔壁手机店老板", "年纪相仿的同行，嘴上不饶人，心里有数。", "一顿烧烤", 150, "阿杰咬着串：\"行，以后有事言语一声。\""),
            new NpcDef(Lin, "小林", "发小，在外面打工", "从小一起长大的朋友，听说你开了店就常来看你。", "一顿夜宵", 100, "小林边吃边笑：\"还是你够意思。\""),
        };

        public static NpcDef Get(string id)
        {
            foreach (NpcDef n in All)
                if (n.Id == id) return n;
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
                case Npcs.Zhou: return "40：1788 进货 -3%　70：-6%";
                case Npcs.Jie: return "50：不再打价格战，档口批发价 -2%　80：-4%";
                case Npcs.Lin: return "50：快破产时借你 ¥3,000 救急（仅一次）";
                default: return "";
            }
        }
    }
}
