namespace PCShopSell.Core.Session
{
    public static partial class CityLifeCatalog
    {
        private static CityActivityDefinition[] AddOnsiteActivities(CityActivityDefinition[] source)
        {
            var all=new System.Collections.Generic.List<CityActivityDefinition>(source);
            Add(51,"夏澄的新饮品品尝","life_bar",60,0,6);Add(52,"陶然的朋友聚会","life_bar",90,600,5);
            Add(53,"曾玉的配方试饮","life_bar",90,0,6);Add(54,"郑禾的篮球指导","life_court",120,1200,6);
            Add(55,"董宇的新地板体验","life_court",90,600,5);Add(56,"和张绍文打五回合","life_court",120,1200,6);
            Add(57,"陶川的五杆练习","life_golf",120,2500,6);Add(58,"沈远的商务球叙","life_golf",180,6800,7);
            Add(76,"宁汐的展品摄影","life_expo",90,0,6);
            var premium=Make("social_52_premium","陶然的臻享聚会","life_bar",120,1800,8);premium.LogicalId="social_52";all.Add(premium);
            var lesson=Make("social_58_lesson","沈远的指导球叙","life_golf",120,2500,6);lesson.LogicalId="social_58";all.Add(lesson);
            return all.ToArray();
            void Add(int n,string title,string place,int minutes,int fee,int relation)=>all.Add(Make("social_"+n,title,place,minutes,fee,relation));
            CityActivityDefinition Make(string id,string title,string place,int minutes,int fee,int relation)=>new CityActivityDefinition{
                Id=id,Name=title,VenueId=place,Minutes=minutes,EventOnly=true,ServiceOptionCount=1,Choices=new[]{"到现场操作"},
                DefaultService=new CityVenueService(fee,relation:relation),StartText="到场后打开现场操作",MidText="按真实品尝、回合或照片结算",EndText="现场活动完成"};
        }
    }
}
