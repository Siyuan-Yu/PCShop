using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    public static partial class CityLifeCatalog
    {
        private static CityActivityDefinition[] AddExhibitionActivities(CityActivityDefinition[] source)
        {
            var list = new List<CityActivityDefinition>(source);
            Add("social_73", "任帆的电子展邀请", 120, "到展位集合", "走到下一展位", "体验新品", "交流供货条件", "商谈采购与企业需求");
            Add("social_74", "韩征的布展参观", 90, "布展现场集合", "走到下一展台", "听布展安排");
            Add("social_74_work", "韩征的布展调试", 90, "领取现场任务", "走到下一展台", "分离过多供电负载", "检查电源开关", "重接信号线");
            Add("social_75", "陆栀的原创模型展位", 120, "到原创展位集合", "走到下一展示台", "观看原型", "体验连接结构", "反馈设计细节");
            Add("social_77", "沈晓和父亲的城市模型之行", 120, "与孩子和父亲会合", "走到下一展示台", "听他讲小凳子的细节", "一起观察街景搭建", "陪着慢慢看");
            return AddOnsiteActivities(list.ToArray());
            void Add(string id, string name, int minutes, params string[] choices)
            {
                list.Add(new CityActivityDefinition { Id = id, LogicalId = id == "social_74_work" ? "social_74" : null, Name = name,
                    VenueId = "life_expo", Minutes = minutes, EventOnly = true, ServiceOptionCount = 1, Choices = choices,
                    DefaultService = new CityVenueService(0, energy: -8, mood: 12, relation: 6), StartText = "同行者已到展位，按现场互动继续",
                    MidText = "实际看展、交流或处理已选择的现场任务", EndText = name + "结束" });
            }
        }
    }
}
