using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    public static partial class CityLifeCatalog
    {
        private static CityActivityDefinition[] AddOutdoorEventActivities(CityActivityDefinition[] source)
        {
            var all = new List<CityActivityDefinition>(source);
            Add("social_61", "和周国良垂钓", GameSession.FishingVenueId, 90, new CityVenueService(300, mood: 10, energy: 5, relation: 6), "租杆入场", "打开池塘钓鱼", "听河边往事");
            Add("social_62", "卢桥的生态向导路线", "life_hike", 120, new CityVenueService(900, energy: -17, mood: 19, health: 4, intelligence: 1, relation: 8), "生态向导同行", "走向下一站", "休息五分钟", "改走观景支线");
            Add("social_62_picnic", "卢桥的私享野餐路线", "life_hike", 180, new CityVenueService(2200, hunger: 55, energy: -20, mood: 28, health: 5, nutrition: 4, relation: 10), "野餐路线同行", "走向下一站", "休息五分钟", "改走观景支线");
            Add("social_63", "方林的步道现场", "life_hike", 90, new CityVenueService(0, energy: -8, mood: 10, health: 2, relation: 6), "步道同行", "走到花旁赏花", "帮扶倒伏植物", "巡查并记录损坏设施");
            Add("social_64", "石扬的植物辨认", "life_hike", 90, new CityVenueService(0, energy: -8, mood: 10, relation: 6), "走到样株旁", "这是香樟", "这是桂花", "这是银杏", "仔细观察叶片", "走到下一株");
            foreach (int n in new[] { 65, 67 })
            {
                Add("social_" + n, n == 65 ? "莫欣的海味砂锅夜宵" : "吴磊的收工砂锅夜宵", "life_restaurant", 90,
                    new CityVenueService(480, hunger: 70, energy: 6, mood: 14, nutrition: 3, relation: 8), "坐下吃砂锅", "聊今天的经历", "聊工作与合作", "安静吃饭陪伴");
                Add("social_" + n + "_private", n == 65 ? "莫欣的深夜私厨" : "吴磊的收工私厨", "life_restaurant", 120,
                    new CityVenueService(1200, hunger: 85, energy: 8, mood: 21, nutrition: 4, relation: 11), "坐下吃私厨", "聊今天的经历", "聊工作与合作", "安静吃饭陪伴");
            }
            foreach (string id in new[] { "social_66", "social_66_before" })
                Add(id, "茅宁的快餐约饭", "life_restaurant", 45, new CityVenueService(160, hunger: 55, energy: 5, mood: 10, nutrition: 2, relation: 7), "坐下吃快餐", "聊值班生活", "安静吃饭");
            Add("social_66_delivery", "送夜宵到岗亭", "life_restaurant", 20, new CityVenueService(0, relation: 6), "等到岗亭见面", "交付背包中的餐食");
            Add("social_68", "周维成的市场走访", "stall_cpu", 90, new CityVenueService(0, energy: -6, mood: 8, relation: 6), "进入市场", "走访下一档口", "了解供货条件", "选择实际采购");
            Add("social_69", "李承业的同款比价", "stall_cpu", 120, new CityVenueService(0, energy: -8, mood: 8, relation: 7), "开始采集报价", "走访下一核对点", "现场市场单价更合算", "网购单价更合算", "已签直供单价更合算", "尝试谈价 · 智力50交情35起", "选择实际采购");
            Add("social_70", "王振海的显卡看货", "stall_gpu", 90, new CityVenueService(0, energy: -6, mood: 8, relation: 7), "看显卡样品", "走访下一展台", "上机核对所选显卡 · 五分钟", "谈账期 · 智力50交情35起", "选择实际采购");
            Add("social_71", "顾文的钟表旧物局", GameSession.OldMarketVenueId, 90, new CityVenueService(0, energy: -6, mood: 10, relation: 6), "在市场会合", "走到下一旧物摊", "观察机芯", "核对走时", "询问流转记录");
            Add("social_72", "雷鸣的旧电子局", GameSession.OldMarketVenueId, 90, new CityVenueService(0, energy: -6, mood: 10, relation: 6), "在市场会合", "走到下一旧物摊", "学习电路检测", "学习线材检测", "学习接头检测");
            Add("social_85", "帮许文杰换招牌", "stall_cpu", 30, new CityVenueService(0, energy: -5, relation: 4), "一起整理招牌");
            Add("social_93", "骑手歇脚", "stall_cpu", 20, new CityVenueService(0, relation: 4), "歇脚聊会儿");
            Add("social_113", "邻里交流", "life_service", 10, new CityVenueService(0, relation: 1), "说明情况");
            Add("social_114", "流浪狗救助交接", "life_service", 20, new CityVenueService(0, relation: 2), "现场交接");
            Add("social_115", "街头听歌", "life_service", 10, new CityVenueService(0, relation: 3), "听一首歌");
            Add("social_116", "老友见面", "life_service", 30, new CityVenueService(0, relation: 4), "聊聊近况");
            Add("social_119", "夜校课堂", "life_service", 60, new CityVenueService(0, relation: 1), "听课与练习");
            return AddExhibitionActivities(all.ToArray());
            void Add(string id, string name, string venue, int minutes, CityVenueService service, params string[] choices)
            {
                string[] bits = id.Split('_'); string logical = bits.Length > 2 ? bits[0] + "_" + bits[1] : null;
                all.Add(new CityActivityDefinition { Id = id, LogicalId = logical, Name = name, VenueId = venue, EventOnly = true,
                    Minutes = minutes, ServiceOptionCount = 1, DefaultService = service, Choices = choices,
                    StartText = "同行者已到场，按现场选项继续", MidText = "按实际位置、餐食和任务进度继续，未完成操作不给完整奖励", EndText = name + "结束" });
            }
        }
    }
}
