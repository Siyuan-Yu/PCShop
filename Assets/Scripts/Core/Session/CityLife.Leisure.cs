using System;
using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    public enum LeisureGame { None, Pachinko, Poker, Claw, Billiards, Cinema }

    public static partial class CityLifeCatalog
    {
        public const string PachinkoVenue = "life_pachinko";
        public const string PachinkoService = "leisure_pachinko";
        public const string PokerVenue = "life_poker";
        public const string ClawVenue = "life_claw";
        public const string BilliardsVenue = "life_billiards";
        public const string CommunityCinema = "life_cinema_community";
        public const string ParkCinema = "life_cinema_park";
        public const string CommunityFerrisVenue = "life_community_ferris";
        public const string GardenAmusementVenue = "life_garden_amusement";

        public static LeisureGame LeisureAt(string venue) => venue switch
        {
            PachinkoVenue => LeisureGame.Pachinko, PokerVenue => LeisureGame.Poker, ClawVenue => LeisureGame.Claw,
            BilliardsVenue => LeisureGame.Billiards, CommunityCinema or ParkCinema => LeisureGame.Cinema,
            _ => LeisureGame.None
        };

        public static LeisureGame LeisureService(CityActivity activity)
        {
            if (activity == null) return LeisureGame.None;
            var id = string.IsNullOrEmpty(activity.ServiceDefinitionId) ? activity.DefinitionId : activity.ServiceDefinitionId;
            return id?.StartsWith("leisure_", StringComparison.Ordinal) == true ? LeisureAt(activity.VenueId) : LeisureGame.None;
        }

        private static CityActivityDefinition[] AddLeisureActivities(CityActivityDefinition[] existing)
        {
            var all = new List<CityActivityDefinition>(existing);
            Add(PachinkoService, "玩柏青哥", PachinkoVenue, 35, 0, 4, "选机台", mood: 0);
            Add("leisure_poker", "地下牌庄 · 德州扑克", PokerVenue, 45, 0, 3, "坐上四人桌", mood: 0);
            Add("leisure_claw", "抓娃娃", ClawVenue, 25, 0, 4, "打开抓娃娃机");
            Add("leisure_billiards", "打一局台球", BilliardsVenue, 40, 35, 6, "开一局台球", walkInPeople: 2);
            AddCinema(CommunityCinema, "community", 40);
            AddCinema(ParkCinema, "park", 55);
            AddRide("community_ferris", "社区摩天轮", CommunityFerrisVenue, "沿着湖岸看宜居社区。", 60, 10, "乘坐摩天轮");
            AddRide("garden_ferris", "花园摩天轮", GardenAmusementVenue, "升到湖湾上方，俯瞰跨湖桥和赛道岛。", 150, 14, "乘坐摩天轮");
            AddRide("garden_coaster", "过山车", GardenAmusementVenue, "沿湖侧轨道爬升，再越过转弯和下坡。", 120, 12, "选座上车");
            AddRide("garden_carousel", "旋转木马", GardenAmusementVenue, "挑一匹木马或坐进马车。", 70, 8, "选座乘坐");
            AddRide("garden_ringtoss", "投圈摊", GardenAmusementVenue, "一局三个圈，套中瓶子可以带走奖品。", 50, 7, "去投圈");
            return all.ToArray();

            void AddRide(string id, string name, string venue, string description, int price, int mood, string action)
            {
                all.Add(new CityActivityDefinition
                {
                    Id = id, Name = name, VenueId = venue, Minutes = 30, ServiceOptionCount = 1,
                    Choices = new[] { action }, DefaultService = new CityVenueService(price, description, mood: mood, relation: 4, walkInPeople: 4),
                    StartText = "在入口验票。", EndText = "体验结束，回到接待处。"
                });
            }

            void Add(string id, string name, string venue, int time, int price, int relation, string action,
                int walkInPeople = 1, int mood = 6)
            {
                all.Add(new CityActivityDefinition
                {
                    Id = id, Name = name, VenueId = venue, Minutes = time, ServiceOptionCount = 1,
                    Choices = new[] { action }, DefaultService = new CityVenueService(price, mood: mood, relation: relation, walkInPeople: walkInPeople),
                    StartText = action, EndText = "活动结束"
                });
            }

            void AddCinema(string venue, string suffix, int price)
            {
                Add("leisure_cinema_comedy_" + suffix, "看喜剧", venue, 95, price, 6, "喜剧", walkInPeople: 6);
                Add("leisure_cinema_romance_" + suffix, "看爱情片", venue, 105, price, 6, "爱情片", walkInPeople: 6);
                Add("leisure_cinema_action_" + suffix, "看动作片", venue, 110, price, 6, "动作片", walkInPeople: 6);
                Add("leisure_cinema_mystery_" + suffix, "看悬疑片", venue, 100, price, 6, "悬疑片", walkInPeople: 6);
            }
        }

        private static void AddLeisureStaff(List<CityPersonDefinition> people)
        {
            Add("pachinko_night", "程凯", "弹珠店夜班店员", PachinkoVenue, Home(0), 0, 510, "我夜班负责看机台和清点弹珠。");
            Add("pachinko_day", "蒋雯", "弹珠店白班店员", PachinkoVenue, Home(0), 480, 990, "我白天在柜台换珠，你可以选一台空机玩。");
            Add("pachinko_evening", "韩顺", "弹珠店晚班店员", PachinkoVenue, Home(0), 960, 30, "晚班收尾时，我会逐台确认出珠口有没有卡住。");
            Add("poker", "陆衡", "牌庄荷官", PokerVenue, Home(0), 1080, 120, "买好筹码就能上桌，一手牌打完再离桌。");
            Add("claw", "柯洁宁", "抓娃娃店员", ClawVenue, Home(0), 600, 1320, "每天我都要重新摆好被抓乱的奖品。");
            Add("billiards", "沈卓", "台球厅前台", BilliardsVenue, Home(1), 600, 1380, "我在柜台登记开台，也负责擦球和换杆头。");
            Add("cinema_community", "何映宁", "社区电影院售票员", CommunityCinema, Home(1), 600, 1380, "附近的熟客常问今天放什么，我会把场次提前写好。");
            Add("cinema_park", "陶昕然", "公园电影院售票员", ParkCinema, Home(4), 600, 1380, "我开门前先去放映厅检查一遍，再回柜台售票。");
            Add("oldmarket", "许秋实", "旧物市场摊主", GameSession.OldMarketVenueId, Home(0), 540, 1320, "淘货次数在我这里买，几处摊位都能用。");
            Add("ferris_day", "孟春禾", "社区摩天轮售票员", CommunityFerrisVenue, Home(1), 540, 960, "坐到最高处的时候，往左看能认出公园旁那条路。");
            Add("ferris_evening", "蒋晓岚", "社区摩天轮晚班售票员", CommunityFerrisVenue, Home(2), 930, 1320, "天黑以后，沿岸的灯会一盏接一盏亮起来。");
            Add("amusement_day", "陆晓晴", "花园游乐园售票员", GardenAmusementVenue, Home(7), 600, 1020, "每个项目单独买票，同行的人可以一直一起玩。");
            Add("amusement_evening", "林星河", "花园游乐园晚班售票员", GardenAmusementVenue, Home(7), 990, 1380, "摩天轮看得到湖对岸的灯。过山车前排也还有空位。");
            Add("bar_backup", "苏昀", "街坊酒吧吧台调酒师", "life_bar", Home(0), 1080, 120, "晚班我和夏澄一起照看吧台。");
            Add("bar_neighborhood", "庄宁", "邻里酒吧吧台调酒师", "life_bar_tier2", Home(1), 1080, 120, "我会根据客人喜欢的酸甜度调整配方。");
            Add("bar_park", "沈汐", "云庭酒吧吧台调酒师", "life_bar_tier3", Home(4), 1080, 120, "熟客最喜欢一杯闻着有茶香的热饮。");
            Add("bar_garden", "郭苒", "星苑酒吧吧台调酒师", "life_bar_tier4", Home(7), 1080, 120, "夜里来喝杯热茶的人也不少。");

            // Keep existing venue hours. These attendants cover the gaps around the established staff.
            Add("restaurant_night", "许香宁", "街坊餐厅夜班店员", "life_restaurant", Home(0), 0, 555, "夜里来吃面的人多，汤一直温着。");
            Add("restaurant_evening", "方庆云", "街坊餐厅晚班店员", "life_restaurant", Home(0), 1065, 15, "晚饭要忙到换夜班，我来招呼进店的客人。");
            RestaurantShifts("tier2", Home(2), "谭玉珍", "贺敏");
            RestaurantShifts("tier3", Home(4), "谢向南", "余岚");
            RestaurantShifts("tier4", Home(7), "薛文英", "孔遥");

            Add("ktv_night", "潘晓辉", "街坊歌房夜班前台", "life_ktv", Home(0), 0, 555, "夜班我负责开包间和检查麦克风。");
            Add("ktv_evening", "乔琳", "街坊歌房晚班前台", "life_ktv", Home(0), 1065, 15, "晚班的包间要提前试一下音响。");
            KaraokeShifts("tier2", Home(2), "姜圆", "莫千帆", "苏悦");
            KaraokeShifts("tier3", Home(4), "叶晓晨", "白知秋", "何畅");
            KaraokeShifts("tier4", Home(7), "宋言", "钟雨亭", "冯初晴");

            Add("court_morning", "严牧", "青湾球场早班管理员", "life_court", Home(0), 420, 555, "开场前我会检查篮筐和球场地面。");
            Add("court_evening", "董正", "青湾球场晚班管理员", "life_court", Home(0), 1065, 1320, "傍晚来打球的人多，记得把球放回器材架。");
            Add("court_mid_duty", "罗谦", "社区运动场管理员", "life_court_mid", Home(1), 540, 1260, "使用球场和器材可以来找我登记。");
            Add("fitness_duty", "毛雪", "公园健身场管理员", "life_fitness", Home(4), 540, 1260, "我负责场地登记，也会巡查器材。");
            Add("golf_evening", "柳新禾", "练习场晚班管理员", "life_golf", Home(3), 1065, 1140, "日落前还有几轮练球时间，球杆可以来这里还。");
            Add("expo_reception", "许冉", "会展中心前台", "life_expo", Home(6), 540, 1080, "到场的观众可以找我咨询展厅和场次。");
            Add("fishing_night", "汪润", "青湾垂钓场夜班管理员", GameSession.FishingVenueId, Home(0), 0, 375, "夜里也能租杆，沿池边走记得留意灯光。");
            Add("fishing_evening", "唐明川", "青湾垂钓场晚班管理员", GameSession.FishingVenueId, Home(0), 1065, 15, "我负责晚上的钓位登记和租杆归还。");

            void RestaurantShifts(string tier, string home, string daytime, string evening)
            {
                string venue = "life_restaurant_" + tier;
                Add("restaurant_" + tier + "_day", daytime, "餐厅日班店员", venue, home, 600, 975, "我负责白天接客和点单。");
                Add("restaurant_" + tier + "_evening", evening, "餐厅晚班店员", venue, home, 945, 1320, "晚班客人可以直接在我这里点餐。");
            }

            void KaraokeShifts(string tier, string home, string night, string daytime, string evening)
            {
                string venue = "life_ktv_" + tier;
                Add("ktv_" + tier + "_night", night, "歌房夜班前台", venue, home, 0, 510, "夜班要逐间检查话筒和音响。");
                Add("ktv_" + tier + "_day", daytime, "歌房白班前台", venue, home, 480, 990, "到店可以找我开包间。");
                Add("ktv_" + tier + "_evening", evening, "歌房晚班前台", venue, home, 960, 30, "晚班我负责包间登记和设备归还。");
            }

            void Add(string id, string name, string role, string venue, string home, int start, int end, string topic)
            {
                people.Add(new CityPersonDefinition
                {
                    Id = "life_leisure_staff_" + id, Name = name, Role = role, HomeId = home, WorkId = venue,
                    WorkStart = start, WorkEnd = end, Experience = topic, Topics = new[] { topic }
                });
            }
        }
    }
}
