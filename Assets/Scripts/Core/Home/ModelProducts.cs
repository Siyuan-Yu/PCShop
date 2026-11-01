using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Home
{
    /// <summary>原创现货模型；每系列两款常规品及一款漫展限量品，不采用随机付费抽取。</summary>
    public static class ModelProducts
    {
        public const string Business = "潮玩模型";
        public static readonly string[] SeriesIds = { "harbor", "rainbot", "garden", "starlight" };
        public static string SeriesName(string id) => id == "harbor" ? "晨湾航行" : id == "rainbot" ? "雨巷守望" : id == "garden" ? "苔园来客" : "星灯工坊";
        private static GiftDef Item(string series, string id, string name, long price, double weight, string story, bool limited = false) =>
            new GiftDef("model:" + series + ":" + id, name, SeriesName(series) + " · " + story + (limited ? " 漫展限定现货，每届限购5件。" : " 原创常规现货。"), price, weight, Business)
                { ModelSeries = series, LimitedEdition = limited };
        public static readonly IReadOnlyList<GiftDef> All = new[] {
            Item("harbor", "skiff", "晨湾·邮差小艇", 48, .25, "给岛间送信的帆艇，有一片暖黄色帆。"),
            Item("harbor", "beacon", "晨湾·雾灯巡航船", 128, .5, "船头灯塔记录沿岸每一户点亮的灯。"),
            Item("harbor", "tide", "晨湾·潮汐渡轮", 238, .8, "横跨蓝绿海湾的双体渡轮，展期编号铭牌。", true),
            Item("rainbot", "courier", "雨巷·纸伞邮差", 58, .2, "方脑袋送信员会替路边纸箱撑伞。"),
            Item("rainbot", "repairer", "雨巷·钟表修理员", 148, .45, "背着工具箱，认真修好每一个走慢的钟。"),
            Item("rainbot", "nightwatch", "雨巷·夜巡灯手", 268, .65, "戴着透明雨罩的夜间巡灯员，限定铜色底座。", true),
            Item("garden", "sprout", "苔园·豆芽访客", 38, .15, "背着种子的圆角小访客，头顶两片叶。"),
            Item("garden", "mushroom", "苔园·蘑菇旅人", 98, .35, "带着大蘑菇帽，在阳台和花园间旅行。"),
            Item("garden", "greenhouse", "苔园·玻璃花房", 198, .7, "供小访客过冬的微型温室，展期限定。", true),
            Item("starlight", "lamp", "星灯·口袋观测站", 68, .25, "把小型天文台放在掌心，灯罩像一颗星。"),
            Item("starlight", "rover", "星灯·陨石采样车", 168, .55, "四轮小车运回一箱发光石头。"),
            Item("starlight", "tower", "星灯·云上信标塔", 328, .9, "给远行者指路的三层信标，带展期星纹。", true),
        };
        public static bool IsModel(string id) => id != null && id.StartsWith("model:");
        public static IEnumerable<GiftDef> Regular => All.Where(p => !p.LimitedEdition);
        public static IEnumerable<GiftDef> Limited => All.Where(p => p.LimitedEdition);
    }
}
