using System.Collections.Generic;
namespace PCShopSell.Core.Session
{
    public static partial class CityLifeCatalog
    {
        // Existing residents who race as a hobby. The four track contacts host their local meets.
        internal static readonly string[] RacingEnthusiasts = {
            "racer_0", "racer_1", "racer_2", "racer_3", "life_ming", "gaobo", "hexin", "sujun",
            "life_ning", "life_qiao", "life_maker", "life_coach", "life_golfer", "life_bartender",
            "life_singer", "life_clothsupplier", "res_rider", "zengyu", "life_gardener", "life_creator"
        };

        private static void AddRacingPeople(List<CityPersonDefinition> people)
        {
            string[] names={"许星澜","沈知夏","陆景行","顾若宁"};
            string[] roles={"电驱教练","车友俱乐部主理人","赛道整备师","收藏车经纪人"};
            string[] histories={"第一台电驴是自己攒钱买的，后来学会调电控。","陪家人选二手车时踩过坑，才开始认真记录每台车的经历。","曾经只追求马力，跑出赛道后开始重视轮胎和刹车。","在收藏车交易里见过漂亮的故事，也见过没有说出口的故障。"};
            for(int i=0;i<4;i++)people.Add(new CityPersonDefinition{Id="racer_"+i,Name=names[i],Role=roles[i],Age=24+i*3,HomeId=new[]{"life_home_s_oldhomes","life_home_s_services","life_home_s_sports","life_home_s_newtown"}[i],WorkId="race_"+i,WorkStart=540,WorkEnd=1080,
                Interest="赛道驾驶与旧车",Goal="把可靠的车介绍给真正喜欢它的人。",Experience=histories[i],Circle="车友",StoryTitle="弯道之外",FirstEncounter="先跑一圈，再聊聊你喜欢什么车。",
                Topics=new[]{"先减速，再入弯；贵车也救不了过晚的刹车。","车况不只是外观，收车以后要检测。","熟悉以后，每周可以给你介绍一条车源。","车友的关系不是一张报价单，我们也会一起跑圈。"},Stories=new[]{histories[i],new[]{"我把电池和轮胎的更换日期记在车壳内侧，教新手先检查再上场。","有位熟人急着换车，我陪买家测完车况才让他们谈价格。","一次赛前检查发现轮胎内侧偏磨，那以后每次调悬挂都会重新测量。","有人只看稀有颜色，我会把维修记录摊开，让他决定想收藏什么。"}[i],new[]{"我想把初学者都带到安全的封闭场地，慢一点也能享受跑完一圈。","以后想开一间能让车主坐下来讲车的店，手续和故事都认真留下。","我希望每次交车都能解释清楚改了什么，不让客户为听不懂的名词付钱。","我正在整理旧车主的故事，想让下一位买家也知道它曾经陪过谁。"}[i]},LeisurePlaces=new[]{"race_"+i}});
        }
    }
}
