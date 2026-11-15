using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public static partial class CityLifeCatalog
    {
        public const string BasketballVenueId = "life_court";
        public const string GolfVenueId = "life_golf";
        public const int DrinkTastingMinutes = 90;

        public static CityActivityDefinition[] SportPasses(string venueId) => Activities
            .Where(activity => activity.VenueId == venueId && activity.IsSportPass)
            .OrderBy(activity => activity.Minutes).ToArray();

        private static CityActivityDefinition[] AddOnsiteActivities(CityActivityDefinition[] source)
        {
            var all=new List<CityActivityDefinition>(source);
            var courtPrices = new[] { 300, 550, 800, 1000 };
            AddPasses("court", BasketballVenueId, "篮球", courtPrices);
            AddPasses("court", "life_court_mid", "篮球", courtPrices);
            AddPasses("court", "life_fitness", "篮球", courtPrices);
            AddPasses("golf", GolfVenueId, "高尔夫练习", new[] { 150, 280, 400, 500 });
            Add(51,"夏澄的新饮品品尝","life_bar",DrinkTastingMinutes,0,6);Add(52,"陶然的朋友聚会","life_bar",90,600,5);
            Add(53,"曾玉的新饮品","life_bar",90,0,6);Add(54,"郑禾的篮球指导","life_court",120,1200,6);
            Add(55,"董宇的新地板体验","life_court",90,600,5);Add(56,"和张绍文打五回合","life_court",120,1200,6);
            Add(57,"陶川的五杆练习","life_golf",120,2500,6);Add(58,"沈远的商务球叙","life_golf",180,6800,7);
            Add(76,"宁汐的展品摄影","life_expo",90,0,6);
            var premium=Make("social_52_premium","陶然的臻享聚会","life_bar",120,1800,8);premium.LogicalId="social_52";all.Add(premium);
            var lesson=Make("social_58_lesson","沈远的指导球叙","life_golf",120,2500,6);lesson.LogicalId="social_58";all.Add(lesson);
            return all.ToArray();
            void AddPasses(string kind, string venue, string name, int[] prices)
            {
                for (int i = 0; i < prices.Length; i++)
                {
                    int hours = (i + 1) * 2;
                    string title = $"{name} · {hours} 小时";
                    string suffix = venue == BasketballVenueId || venue == GolfVenueId ? "" : "_" + venue;
                    all.Add(new CityActivityDefinition
                    {
                        Id = $"{kind}_pass_{hours}h{suffix}", LogicalId = kind, Name = title, VenueId = venue,
                        Minutes = hours * 60, Choices = new[] { title }, ServiceOptionCount = 1,
                        DefaultService = new CityVenueService(prices[i], relation: 5),
                        StartText = "开始" + name, MidText = "在场上自由练习", EndText = "本场租用时间已用完"
                    });
                }
            }
            void Add(int n,string title,string place,int minutes,int fee,int relation)=>all.Add(Make("social_"+n,title,place,minutes,fee,relation));
            CityActivityDefinition Make(string id,string title,string place,int minutes,int fee,int relation)=>new CityActivityDefinition{
                Id=id,Name=title,VenueId=place,Minutes=minutes,EventOnly=true,ServiceOptionCount=1,Choices=new[]{"到现场操作"},
                DefaultService=new CityVenueService(fee,relation:relation),StartText="到场后打开现场操作",MidText="按真实品尝、回合或照片结算",EndText="现场活动完成"};
        }
    }
}
