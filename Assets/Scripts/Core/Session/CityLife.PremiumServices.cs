using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public static partial class CityLifeCatalog
    {
        // Extra services are separate definitions so existing saved choice numbers keep their meaning.
        private static CityActivityDefinition[] ExpandPremiumServices(CityActivityDefinition[] originals)
        {
            var all = originals.ToList();
            foreach (var source in originals.Where(d => !d.EventOnly))
            {
                string kind = source.Id.Split('_')[0];
                if (source.Id == "old_market") kind = "old_market";
                if (source.Id.StartsWith("expo_")) kind = source.Id;
                switch (kind)
                {
                    case "dinner":
                        Add("主厨时令套餐", 90, 650, "前菜、时令主菜与甜品，适合认真交流的一餐。", new CityVenueService(650, hunger: 80, energy: 8, mood: 16, nutrition: 5, relation: 12));
                        Add("私宴品鉴席", 120, 1800, "独立雅间、分道上菜与主厨介绍，适合庆祝或商务宴请。", new CityVenueService(1800, hunger: 90, energy: 10, mood: 22, nutrition: 6, charm: 1, relation: 15));
                        break;
                    case "ktv":
                        Add("录音指导包间", 120, 1800, "整间报价；包含录音回听与气息指导，偏重个人练习。", new CityVenueService(1800, energy: -6, mood: 18, charm: 3, relation: 7), true);
                        Add("尊享主题包间", 180, 3800, "整间报价；独立主题包间、灯光编排与合唱时段，适合多人聚会。", new CityVenueService(3800, hunger: 15, energy: -8, mood: 26, charm: 2, relation: 16), true);
                        break;
                    case "bar":
                        Add("主理人风味品鉴", 90, 680, "三款风味饮品与小食，可选全程无酒精版本，侧重品鉴交流。", new CityVenueService(680, hunger: 24, energy: 3, mood: 18, intelligence: 1, relation: 10));
                        Add("湖景臻享套餐", 120, 1600, "湖景预约座、定制无酒精特调与精致小食，适合长时间相聚。", new CityVenueService(1600, hunger: 35, energy: 5, mood: 25, charm: 1, relation: 15));
                        break;
                    case "court":
                        Add("私人技战术指导", 90, 1200, "专业指导脚步与投篮，运动量更大，侧重身体训练。", new CityVenueService(1200, energy: -20, mood: 16, health: 5, relation: 8));
                        Add("包场友谊赛", 120, 3000, "整场报价，预留场地与赛后交流时间，适合约朋友组队。", new CityVenueService(3000, energy: -26, mood: 22, health: 6, relation: 14), true);
                        break;
                    case "golf":
                        Add("私人教练课程", 120, 2500, "一对一分析挥杆与选杆，侧重技巧和见识。", new CityVenueService(2500, energy: -14, mood: 15, health: 3, intelligence: 2, relation: 8));
                        Add("商务球叙套餐", 180, 6800, "预留练习区与会客休息席，含器材服务和充分交流时间。", new CityVenueService(6800, energy: -18, mood: 23, charm: 2, relation: 16));
                        break;
                    case "fishing":
                        Add("专业钓具体验", 120, 800, "租用专业钓具并接受技巧指导，适合认真学习。", new CityVenueService(800, energy: 2, mood: 17, intelligence: 2, relation: 9));
                        Add("湖畔私享钓位", 180, 1800, "预留安静钓位与休憩服务，适合长时间聊天放松。", new CityVenueService(1800, energy: 12, mood: 25, relation: 14));
                        break;
                    case "hike":
                        Add("生态向导路线", 120, 900, "由向导讲解植物与城市生态，路线较长。", new CityVenueService(900, energy: -17, mood: 19, health: 4, intelligence: 2, relation: 10));
                        Add("山湖私享野餐", 180, 2200, "向导与预设野餐点，包含餐食和休息时段。", new CityVenueService(2200, hunger: 55, energy: -20, mood: 28, health: 5, nutrition: 4, relation: 15));
                        break;
                    case "snack":
                        Add("海味砂锅夜宵", 90, 480, "砂锅、热菜与清爽小食，更顶饱。", new CityVenueService(480, hunger: 70, energy: 6, mood: 14, nutrition: 3, relation: 11));
                        Add("深夜私厨套餐", 120, 1200, "预留私厨座席与分道夜宵，适合赛后或久别重聚。", new CityVenueService(1200, hunger: 85, energy: 8, mood: 21, nutrition: 4, relation: 15));
                        break;
                    case "market":
                        Add("供应链导览", 90, 600, "付费讲解渠道与报价，不含商品采购。", new CityVenueService(600, energy: -6, mood: 8, intelligence: 2, relation: 9));
                        Add("专业选品咨询", 120, 1800, "深入交流型号与选品思路，不自动赠送或订购商品。", new CityVenueService(1800, energy: -9, mood: 12, intelligence: 3, relation: 12));
                        break;
                    case "old_market":
                        Add("收藏故事导览", 90, 500, "了解老器材的年代和用途，不含实物购买。", new CityVenueService(500, energy: -5, mood: 13, intelligence: 1, relation: 10));
                        Add("鉴赏工作坊", 120, 1500, "学习辨别工艺与成色，不代替二手商品的实际鉴定。", new CityVenueService(1500, energy: -8, mood: 17, intelligence: 3, relation: 12));
                        break;
                    case "expo_electronics":
                        Add("新品深度导览", 150, 1000, "包含专题讲解，不自动授予采购订单。", new CityVenueService(1000, energy: -9, mood: 14, intelligence: 2, relation: 10));
                        Add("行业交流专场", 180, 3000, "展期内的行业专题与深度交流，实际订单仍需另谈。", new CityVenueService(3000, energy: -12, mood: 19, intelligence: 3, charm: 1, relation: 14));
                        break;
                    case "expo_comic":
                        Add("创作幕后导览", 150, 800, "听创作者讲解作品与制作过程，不包含商品。", new CityVenueService(800, energy: -7, mood: 18, intelligence: 2, relation: 10));
                        Add("创作者交流席", 180, 2500, "作品交流与问答，适合认真了解原创行业。", new CityVenueService(2500, energy: -10, mood: 24, intelligence: 2, charm: 1, relation: 14));
                        break;
                }
                void Add(string name, int minutes, int price, string description, CityVenueService effects, bool group = false)
                {
                    int index = all.Count(d => d.LogicalId == source.Id) + 1;
                    all.Add(new CityActivityDefinition { Id = source.Id + "_premium" + index, LogicalId = source.Id,
                        Name = name, VenueId = source.VenueId, Minutes = minutes, PriceMultiplier = source.PriceMultiplier,
                        FixedGroupPrice = group, ServiceOptionCount = 1, Choices = new[] { name, "交流体验", "聊近况" },
                        StartText = name + "开始。", MidText = description, EndText = name + "结束。",
                        DefaultService = new CityVenueService(price, description, effects.Hunger, effects.Energy, effects.Mood,
                            effects.Nutrition, effects.Health, effects.Charm, effects.Intelligence, effects.Relation) });
                }
            }
            return all.ToArray();
        }

        private static CityActivityDefinition[] AddSocialEventActivities(CityActivityDefinition[] originals)
        {
            var all = new List<CityActivityDefinition>(originals);
            Add(41, "车友庆功宴", "life_restaurant", 90, 3000, new CityVenueService(3000, hunger: 80, energy: 8, mood: 20, charm: 1, relation: 7), "用餐与庆功", "聊比赛亮点", "约下次练习");
            Add(43, "和顾宁观赛", "race_0", 60, 0, new CityVenueService(0, mood: 12, intelligence: 1, relation: 6), "观看正式比赛", "讲解赛道", "聊夜校生活");
            Add(44, "罗师傅的新菜试吃", "life_restaurant", 60, 0, new CityVenueService(0, hunger: 65, energy: 5, mood: 12, nutrition: 3, relation: 6), "品尝新菜", "评价口味", "评价分量", "评价定价");
            Add(45, "招牌炖菜品鉴", "life_restaurant", 75, 0, new CityVenueService(0, hunger: 75, energy: 7, mood: 15, nutrition: 3, relation: 7), "品尝招牌炖菜", "询问炖菜的故事", "聊熟客偏好");
            Add(46, "林荷的下班饭局", "life_restaurant_tier2", 90, 1000, new CityVenueService(1000, hunger: 75, energy: 7, mood: 17, nutrition: 4, relation: 8), "双人餐", "聊值班经历", "聊餐厅经营");
            Add(47, "王婶家的家常饭", "life_restaurant", 90, 0, new CityVenueService(0, hunger: 80, energy: 8, mood: 18, nutrition: 5, relation: 8), "一起吃家常饭", "聊农贸市场", "听家常往事");
            Add(48, "新伴奏试唱专场", "life_ktv", 120, 2000, new CityVenueService(2000, energy: -4, mood: 18, charm: 1, relation: 7), "选歌试唱", "合唱新歌", "反馈伴奏效果");
            Add(49, "音响调试试听", "life_ktv", 90, 0, new CityVenueService(0, energy: -5, mood: 10, relation: 5), "试听设备", "比较人声 · 智力 50 起", "比较低频 · 智力 50 起", "检查杂音 · 智力 50 起");
            Add(50, "社区合唱团排练", "life_ktv", 120, 3000, new CityVenueService(3000, energy: -8, mood: 21, charm: 1, relation: 7), "热身与排练", "一起合唱", "休息交流");
            Add(59, "邵安的周末技巧钓鱼", GameSession.FishingVenueId, 120, 800, new CityVenueService(800, energy: 2, mood: 16, intelligence: 1, relation: 8), "选位抛竿", "请教选饵", "交流提竿技巧");
            Add(60, "乔松的湖畔钓叙", GameSession.FishingVenueId, 180, 1800, new CityVenueService(1800, mood: 12, relation: 8), "安静陪钓", "请教园艺", "听往事", "换远处钓位");
            return AddOutdoorEventActivities(all.ToArray());
            void Add(int number, string name, string venue, int minutes, int price, CityVenueService service, params string[] choices)
            {
                all.Add(new CityActivityDefinition { Id = "social_" + number, Name = name, VenueId = venue,
                    Minutes = minutes, EventOnly = true, ServiceOptionCount = 1, Choices = choices,
                    FixedGroupPrice = number is 41 or 48 or 50,
                    DefaultService = service, StartText = "大家到齐，开始" + name + "。", MidText = "按本次安排体验并交流。", EndText = name + "完成。" });
            }
        }
    }
}
