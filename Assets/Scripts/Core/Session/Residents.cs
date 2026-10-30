using System.Collections.Generic;

namespace PCShopSell.Core.Session
{
    /// <summary>街上的人此刻在干嘛。</summary>
    public enum ResidentTrip
    {
        ToWork,
        ToHome,
        Stroll,
    }

    /// <summary>
    /// 住在这一带的一个真实的人：有名字、立绘（Resources/Portraits/{Person.Id}）、工作的店和作息。
    /// 上班前、下班后、出门闲逛的时段在街上走；聊天、送礼、听他讲故事和店员一样，熟络度按 Person.Id 记。
    /// </summary>
    public sealed class ResidentDef
    {
        public StaffDef Person { get; }

        /// <summary>在哪家店上班（地点 id）；没有工作的是 null。</summary>
        public string WorkPlaceId { get; }

        /// <summary>上下班时间（当天分钟）；没有工作时都是 0。</summary>
        public int WorkStart { get; }
        public int WorkEnd { get; }

        /// <summary>出门在街上逛的时段：[开始, 结束) 两两一组（当天分钟）。</summary>
        public int[] Outings { get; }

        /// <summary>true = 坐地铁来（从街西头地铁站出入）；false = 住城中村（从街东头出入）。</summary>
        public bool FromMetro { get; }

        /// <summary>只有街坊才有的三段故事；店员的故事在 Rosters.StoriesOf 里。</summary>
        public string[] Stories { get; }

        public ResidentDef(StaffDef person, string workPlaceId, int workStart, int workEnd, bool fromMetro, int[] outings = null, string[] stories = null)
        {
            Person = person;
            WorkPlaceId = workPlaceId;
            WorkStart = workStart;
            WorkEnd = workEnd;
            FromMetro = fromMetro;
            Outings = outings ?? new int[0];
            Stories = stories;
        }

        public string Id => Person.Id;
    }

    public static class Residents
    {
        /// <summary>上班前、下班后在街上走的时长（游戏分钟）。</summary>
        public const int CommuteMinutes = 30;

        private static int H(double hour) => (int)System.Math.Round(hour * 60);

        private static StaffDef P(string id, string name, string role, params string[] lines) => new StaffDef(id, name, role, lines);

        public static readonly ResidentDef[] All =
        {
            // 店里的人：上下班路过这条街
            new ResidentDef(Rosters.MallGuide, "shop12", H(9), H(21), false),
            new ResidentDef(Rosters.MallManager, "shop12", H(8.5), H(20.5), true),
            new ResidentDef(Rosters.MallPorter, "shop12", H(8), H(20), false),
            new ResidentDef(Rosters.FarmBoss, "shop14", H(6), H(18), false),
            new ResidentDef(Rosters.TobaccoBoss, "shop19", H(9), H(22), false),
            new ResidentDef(Rosters.PhoneClerk, "shop04", H(9.5), H(21.5), true),
            new ResidentDef(Rosters.PhoneApprentice, "shop04", H(13), H(21), true),
            new ResidentDef(Rosters.DealerBoss, "dealer", H(8.5), H(19.5), false),
            new ResidentDef(Rosters.AgentChen, "agency", H(9), H(20), true),

            // 街上其他小店的人
            new ResidentDef(P("res_wang", "老王", "修鞋匠",
                    "鞋跟掉了？拿来，十分钟给你钉好。",
                    "这条街我修了二十年鞋，谁家铺子换过几个老板我都知道。",
                    "年轻人开店，别光看客流，房租才是要命的。"),
                "shop05", H(7.5), H(19), false, new[] { H(12), H(12.6) }, new[]
                {
                    "老王十几岁跟着师父挑担子走街串巷修鞋，后来攒钱盘下这间一米多宽的小铺，一干就是二十年。",
                    "他说这条街以前都是平房，晚上一条街就他一盏灯：“那时候修一双鞋五毛钱，人人都认识我。”",
                    "他悄悄说，儿子劝他别干了，可他舍不得：“街坊的鞋坏了没处修，那多不方便。”",
                }),
            new ResidentDef(P("res_fang", "阿芳", "理发师",
                    "头发该剪了哦，有空来店里坐坐。",
                    "我们店里天天有人聊八卦，整条街的事都知道。",
                    "听说电脑城又有新铺子开张了？"),
                "shop09", H(10), H(21), false, new[] { H(15), H(15.7) }, new[]
                {
                    "阿芳十六岁出来学手艺，在别人店里洗了三年头才第一次拿剪刀。",
                    "她店里的老顾客大多是街坊，谁家孩子考上大学、谁家开了新店，她都是第一个知道的。",
                    "她攒钱想再开一家分店：“像你开电脑店一样，一点点来呗。”",
                }),
            new ResidentDef(P("res_tea", "小雨", "奶茶店店员",
                    "新品桃桃乌龙，第二杯半价！",
                    "下午三点最忙，学生一放学全挤过来。",
                    "我男朋友天天在家打游戏，电脑风扇响得跟拖拉机似的。"),
                "shop01", H(10), H(22), true, new[] { H(16), H(16.5) }, new[]
                {
                    "小雨在念夜校的会计，白天在奶茶店打工交学费。",
                    "她能一口气记住二十杯不同的单，店长说她是“人形点单机”。",
                    "她说以后想开一家自己的小店：“不卖奶茶了，天天闻甜味都腻了。”",
                }),
            new ResidentDef(P("res_baker", "刘师傅", "面包师傅",
                    "刚出炉的面包，晚了就没了。",
                    "我凌晨四点就起来和面，这条街我起得最早。",
                    "做吃的跟做生意一样，料要真，口碑才长久。"),
                "shop15", H(5.5), H(15), false, new[] { H(16), H(17.5) }, new[]
                {
                    "刘师傅以前在大酒店做西点，后来嫌规矩太多，自己出来开了面包工坊。",
                    "他每天卖不完的面包都送给城中村的老人，从来不过夜。",
                    "他的手艺是跟一位老师傅学的，老师傅临走前把配方本交给了他，他到现在还压在枕头底下。",
                }),
            new ResidentDef(P("res_flower", "晓晴", "花店老板娘",
                    "开业的话订个花篮吧，喜庆。",
                    "今天的玫瑰特别好，进价却涨了。",
                    "花和电脑一样，行情一天一个价。"),
                "shop16", H(8.5), H(20), true, new[] { H(11), H(11.7) }, new[]
                {
                    "晓晴以前在写字楼上班，有一天看着窗外的花店发呆，第二天就辞了职。",
                    "她最怕情人节：一晚上包几百束花，手被刺扎得全是口子。",
                    "她说开店最难的是第一年：“撑过去就好了，老客会带新客。”",
                }),
            new ResidentDef(P("res_pharm", "何药师", "药房药剂师",
                    "最近降温，注意别感冒了。",
                    "熬夜伤身，开店的人最容易把自己累垮。",
                    "药房的电脑是十年前的，查个处方要等半天。"),
                "shop10", H(8), H(20), true, new[] { H(12.5), H(13.2) }, new[]
                {
                    "何药师读了五年药学，毕业后一直在这家药房，熟客生病都先来问他。",
                    "他有个小本子，记着附近老人吃的每一种药，到时间就提醒他们来拿。",
                    "他说药房的系统该换电脑了：“老板一直嫌贵，你要是有合适的，帮我们配一台。”",
                }),
            new ResidentDef(P("res_cook", "胖哥", "快餐店厨师",
                    "中午来吃饭，给你多打一勺肉！",
                    "后厨太热了，出来透口气。",
                    "这条街的老板们中午都在我这吃，消息全在饭桌上。"),
                "shop08", H(9), H(21), false, new[] { H(14.5), H(15.5) }, new[]
                {
                    "胖哥十五岁就在后厨帮工，切菜切到现在闭着眼都不会切到手。",
                    "他说商业街的老板们中午都来他这吃饭，谁家生意好坏，看点几个菜就知道。",
                    "他打算明年回老家开个小饭馆，把爹妈接过去一起住。",
                }),

            // 住在附近、常在街上走的人
            new ResidentDef(P("res_rider", "小吴", "外卖骑手",
                    "兄弟快点，我这单要超时了！",
                    "电子市场那边档口工作日才开，周末别白跑。",
                    "我送过好几单电脑配件，都是从 1788 上买的。"),
                null, 0, 0, false, new[] { H(9.5), H(14.5), H(16.5), H(22) }, new[]
                {
                    "小吴送外卖三年，整条街哪家店几点开门、哪栋楼电梯坏了，他全记得。",
                    "他攒钱想买一辆好点的电动车：“现在这辆刹车不灵，下雨天骑着心慌。”",
                    "他说最暖心的是城中村一个老太太，每次都给他塞一个苹果：“就像我奶奶。”",
                }),
            new ResidentDef(P("res_grandpa", "周大爷", "退休大爷",
                    "小伙子，吃了吗？",
                    "以前这条街都是平房，现在全是铺子了。",
                    "我孙子过年回来，非要我给他买个电脑。"),
                null, 0, 0, false, new[] { H(6.5), H(11.5), H(14.5), H(19) }, new[]
                {
                    "周大爷以前是厂里的电工，退休后每天在街上遛弯，谁家门口灯坏了他都帮着看看。",
                    "他说老伴走得早，儿子在外地，最盼的就是过年孙子回来。",
                    "“年轻人做生意要诚实。”他拍拍你的肩，“我在厂里干了四十年，最看不起糊弄人的。”",
                }),
            new ResidentDef(P("res_auntie", "张阿姨", "买菜的阿姨",
                    "哎，你好你好。",
                    "农贸市场今天的肉新鲜，下午就没了。",
                    "家具城最近在搞活动，我买了个凳子。"),
                null, 0, 0, false, new[] { H(7), H(10), H(15.5), H(18.5) }, new[]
                {
                    "张阿姨每天买两趟菜，和王婶是二十年的老姐妹。",
                    "她闺女在外企上班，给她买了智能手机，她现在最爱刷短视频。",
                    "她说家里那台老电脑是闺女上大学时买的，舍不得扔：“你帮我看看还能不能用？”",
                }),
            new ResidentDef(P("res_student", "小宇", "大学生",
                    "嗨～",
                    "室友都在攒钱配游戏本，我还在用高中的老电脑。",
                    "学校里搞 AI 的那帮人天天抢显卡。"),
                null, 0, 0, true, new[] { H(10.5), H(14.5), H(17.5), H(23) }, new[]
                {
                    "小宇在附近的大学读计算机，周末去网吧做网管挣生活费。",
                    "他说想自己攒一台电脑：“配置单我早就列好了，就差钱。”",
                    "他偷偷告诉你：班上好几个同学都想配电脑，“要是你家便宜，我帮你拉一车人来。”",
                }),
            new ResidentDef(P("res_coder", "陈工", "程序员",
                    "嗯？你好。",
                    "内存现在便宜，能多插就多插。",
                    "盒装的贵一点，但出问题好退换，散件就看运气。"),
                null, 0, 0, true, new[] { H(8), H(9), H(19), H(22) }, new[]
                {
                    "陈工在城东一家互联网公司写代码，每天坐地铁通勤一个多小时。",
                    "他自己装过七八台电脑，说装机最怕的是电源虚标。",
                    "他说公司明年要换一批办公电脑：“到时候我推荐你，比找大厂便宜。”",
                }),
            new ResidentDef(P("res_mom", "吴姐", "推婴儿车的宝妈",
                    "嘘——宝宝刚睡着。",
                    "想买个电脑给孩子上网课，不知道什么配置够用。",
                    "房产中介那家小陈人挺好的，我们家房子就是他介绍的。"),
                null, 0, 0, false, new[] { H(9), H(12), H(14.5), H(18) }, new[]
                {
                    "吴姐以前是小学老师，孩子出生后在家带娃，每天推着婴儿车在街上走两圈。",
                    "她说一个人带孩子最累的是半夜，老公在外地跑货车，一个月回来一次。",
                    "她想等孩子上幼儿园了，在网上开个小店：“到时候要买电脑，就找你。”",
                }),
            new ResidentDef(P("res_pupil", "乐乐", "中学生",
                    "叔叔好！",
                    "我在攒零花钱买显卡，还差好多。",
                    "放学了不想回家写作业。"),
                null, 0, 0, false, new[] { H(6.8), H(7.7), H(16.5), H(20) }, new[]
                {
                    "乐乐上初二，每天放学都要在电脑城门口看一会儿橱窗里的显卡。",
                    "他说爸妈只准他周末玩一小时电脑：“可我想学编程，不只是打游戏。”",
                    "他把压岁钱都存在一个铁盒子里：“等攒够了，我第一个来你家买。”",
                }),
        };

        public static ResidentDef Get(string id)
        {
            foreach (ResidentDef r in All) if (r.Id == id) return r;
            return null;
        }

        /// <summary>这个人现在在不在街上、在干嘛；不在街上返回 null（在家或在店里上班）。</summary>
        public static ResidentTrip? TripAt(ResidentDef r, int minuteOfDay)
        {
            if (r.WorkPlaceId != null)
            {
                if (minuteOfDay >= r.WorkStart - CommuteMinutes && minuteOfDay < r.WorkStart) return ResidentTrip.ToWork;
                if (minuteOfDay >= r.WorkEnd && minuteOfDay < r.WorkEnd + CommuteMinutes) return ResidentTrip.ToHome;
            }
            for (int i = 0; i + 1 < r.Outings.Length; i += 2)
                if (minuteOfDay >= r.Outings[i] && minuteOfDay < r.Outings[i + 1]) return ResidentTrip.Stroll;
            return null;
        }

        /// <summary>这个人现在是否在店里上班（出门吃午饭的时段不算）。</summary>
        public static bool AtWork(ResidentDef r, int minuteOfDay) =>
            r.WorkPlaceId != null && minuteOfDay >= r.WorkStart && minuteOfDay < r.WorkEnd && TripAt(r, minuteOfDay) == null;
    }

    public sealed partial class GameSession
    {
        /// <summary>此刻在街上的人和他们在干嘛。</summary>
        public List<KeyValuePair<ResidentDef, ResidentTrip>> ResidentsOutside()
        {
            var list = new List<KeyValuePair<ResidentDef, ResidentTrip>>();
            int m = Clock.MinuteOfDay;
            foreach (ResidentDef r in Residents.All)
            {
                ResidentTrip? t = Residents.TripAt(r, m);
                if (t.HasValue) list.Add(new KeyValuePair<ResidentDef, ResidentTrip>(r, t.Value));
            }
            return list;
        }
    }
}
