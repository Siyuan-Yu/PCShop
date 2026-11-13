using System.Linq;
using PCShopSell.Core.Home;

namespace PCShopSell.Core.Session
{
    public sealed class OldMarketGoods
    {
        public string Id, Name, Stall, Description, Symptom;
        public int Price, RepairFee, CorrectRepair;
        public double Weight;
        public bool Faulty => CorrectRepair > 0;
    }
    public static class OldMarketCatalog
    {
        public static readonly string[] Stalls = { "钟表摊", "旧电子摊", "唱片摊", "收藏杂货摊" };
        public static readonly string[] RepairChoices = { "更换电池并清洁接点", "更换老化音频线", "重新固定松脱接头" };
        public static readonly OldMarketGoods[] All = {
            G("watch", "老式手上链腕表", 0, 2000, .15, "走时正常，有些岁月痕迹。"),
            G("clock", "木壳座钟", 0, 4800, 2.4, "钟摆完好，整点报时。"),
            G("pocket", "银色怀表", 0, 12000, .2, "保养不错，还留着旧表链。"),
            G("radio", "便携旧收音机", 1, 1200, .8, "开不了机，修好还能听。", 180, 1, "不通电；电池电压不足，触点有氧化。"),
            G("cassette", "老式磁带随身听", 1, 2400, .5, "磁带会转，就是没声音。", 300, 2, "机芯运转正常；输出音频线有明显破损。"),
            G("speaker", "木壳有源音箱", 1, 3600, 2.5, "接头松了，声音断断续续。", 450, 3, "电源正常，线材完好；端子接头松脱。"),
            G("jazz", "爵士黑胶唱片", 2, 600, .3, "盘面完好，封套有些旧。"),
            G("folk", "民谣唱片套装", 2, 1800, .8, "三张一套，原封套还在。"),
            G("record", "首版唱片收藏盒", 2, 6800, 1.4, "首版唱片，附收藏册。"),
            G("book", "湖城旧街影集", 3, 400, .7, "翻翻照片里的老湖城。"),
            G("pen", "旧款钢笔礼盒", 3, 1600, .3, "写字顺滑，笔身有些磨痕。"),
            G("camera", "机械相机收藏机", 3, 8800, 1.1, "一台适合陈列收藏的老相机。")
        };
        private static OldMarketGoods G(string key, string name, int stall, int price, double weight, string description,
            int fee = 0, int repair = 0, string symptom = null) => new OldMarketGoods { Id = "oldmarket:" + key, Name = name,
                Stall = Stalls[stall], Price = price, Weight = weight, Description = description, RepairFee = fee, CorrectRepair = repair, Symptom = symptom };
        public static OldMarketGoods Get(string id) => All.FirstOrDefault(p => p.Id == id);
        public static string ItemId(OldMarketGoods g, bool repaired = false) => g.Id + (g.Faulty && !repaired ? ":faulty" : "");
        public static readonly GiftDef[] Gifts = All.SelectMany(g => g.Faulty
            ? new[] { new GiftDef(ItemId(g), g.Name + " · 待修", g.Description, g.Price * 6 / 10, g.Weight, "旧物市场专柜"),
                new GiftDef(g.Id, g.Name + " · 已修复", "在旧物市场修理台修复的二手实物。", g.Price, g.Weight, "旧物市场专柜") }
            : new[] { new GiftDef(g.Id, g.Name, g.Description, g.Price, g.Weight, "旧物市场专柜") }).ToArray();
    }
}
