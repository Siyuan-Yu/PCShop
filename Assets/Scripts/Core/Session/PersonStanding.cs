using System;
using System.Linq;

namespace PCShopSell.Core.Session
{
    /// <summary>
    /// 名望：每个人都有的行业名气，玩家和 NPC 同一把尺子（0 起，没有上限）。
    /// 玩家的名望是实际经营出来的（PlayerShop.Reputation，所有店共用）；NPC 的名望按身份、职务、年纪推算，
    /// 同一个人每次算出来都一样，不进存档。它和“本店知名度”（每家店的客流）是两回事。
    /// </summary>
    public static class PersonStanding
    {
        /// <summary>第一档：默默无闻。</summary>
        public const double Neighbourhood = 8;
        /// <summary>第二档：小有名气（够得上租中型铺的 25）。</summary>
        public const double Known = 25;
        /// <summary>第三档：圈内知名（够得上租大铺的 60）。</summary>
        public const double Famous = 60;

        public static string Label(double value) =>
            value >= Famous ? "圈内知名" : value >= Known ? "小有名气" : value >= Neighbourhood ? "街坊熟知" : "默默无闻";

        private static readonly (string[] Words, int Base)[] RoleTiers =
        {
            (new[] { "董事", "总裁", "总监", "厂长", "集团", "会长", "局长", "教授", "院长", "策展" }, 48),
            (new[] { "老板", "店主", "掌柜", "馆长", "经理", "校长", "律师", "记者", "主播", "设计师", "房东", "创始" }, 30),
            (new[] { "主管", "工程师", "医生", "医师", "厨师", "教练", "车手", "编辑", "摄影", "教师", "老师", "采购", "技师", "师傅" }, 18),
            (new[] { "店员", "学徒", "员工", "助理", "保安", "清洁", "快递", "司机", "服务员", "实习", "学生", "维修", "盘点", "检测", "值守" }, 6),
        };

        /// <summary>按职务、年纪、是否关键人物、是否档口老板推算 NPC 的名望（整数，至少 1）。</summary>
        public static int ForNpc(string personId, string role, int age, bool keyCharacter, bool stallOwner)
        {
            double value = 10;
            string text = role ?? "";
            foreach (var tier in RoleTiers)
                if (tier.Words.Any(word => text.Contains(word))) { value = tier.Base; break; }
            if (keyCharacter) value += 8;
            if (stallOwner) value += 12;
            value += Math.Max(0, Math.Min(5, (age - 25) * .15));
            value += Jitter(personId);
            return (int)Math.Max(1, Math.Round(value));
        }

        /// <summary>同一个 id 永远得到同一个 -3~+3 的偏移，让同职务的人不全是一个数。</summary>
        private static int Jitter(string id)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in id ?? "") hash = hash * 31 + c;
                return Math.Abs(hash % 7) - 3;
            }
        }
    }
}