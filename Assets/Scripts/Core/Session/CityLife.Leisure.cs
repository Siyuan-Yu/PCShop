using System;
using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    public enum LeisureGame { None, Pachinko, Poker, Claw, Billiards, Cinema }

    public static partial class CityLifeCatalog
    {
        public const string PachinkoVenue = "life_pachinko";
        public const string PokerVenue = "life_poker";
        public const string ClawVenue = "life_claw";
        public const string BilliardsVenue = "life_billiards";
        public const string CommunityCinema = "life_cinema_community";
        public const string ParkCinema = "life_cinema_park";

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
            Add("leisure_pachinko", "玩柏青哥", PachinkoVenue, 35, 50, 4, "买 50 珠");
            Add("leisure_poker", "地下牌庄 · 德州扑克", PokerVenue, 45, 0, 3, "坐上四人桌");
            Add("leisure_claw", "抓娃娃", ClawVenue, 25, 0, 4, "打开抓娃娃机");
            Add("leisure_billiards", "打一局台球", BilliardsVenue, 40, 35, 6, "开一局台球");
            AddCinema(CommunityCinema, "community", 40);
            AddCinema(ParkCinema, "park", 55);
            return all.ToArray();

            void Add(string id, string name, string venue, int time, int price, int relation, string action)
            {
                all.Add(new CityActivityDefinition
                {
                    Id = id, Name = name, VenueId = venue, Minutes = time, ServiceOptionCount = 1,
                    Choices = new[] { action }, DefaultService = new CityVenueService(price, mood: 6, relation: relation),
                    StartText = action, EndText = "活动结束"
                });
            }

            void AddCinema(string venue, string suffix, int price)
            {
                Add("leisure_cinema_comedy_" + suffix, "看喜剧", venue, 95, price, 6, "喜剧");
                Add("leisure_cinema_romance_" + suffix, "看爱情片", venue, 105, price, 6, "爱情片");
                Add("leisure_cinema_action_" + suffix, "看动作片", venue, 110, price, 6, "动作片");
                Add("leisure_cinema_mystery_" + suffix, "看悬疑片", venue, 100, price, 6, "悬疑片");
            }
        }
    }
}
