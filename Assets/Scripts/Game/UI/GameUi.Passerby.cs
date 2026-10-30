using System;
using System.Linq;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    /// <summary>街上的路人：走近按 F 聊几句。身份按路人编号固定，说的话带点当天的行情和时间。</summary>
    public sealed partial class GameUi
    {
        private sealed class Passerby
        {
            public string Title;
            public string[] Greet;
            public string[] Chat;
            public string Pitch;
        }

        private static readonly Passerby[] Passersby =
        {
            new Passerby
            {
                Title = "赶路的上班族",
                Greet = new[] { "啊？有事吗，我赶时间……", "你好，快说，我还得打卡。" },
                Chat = new[] { "公司电脑卡得要命，老板就是不肯换。", "天天加班，周末只想在家打游戏。", "这条街早上的包子铺不错，就是排队太长。" },
                Pitch = "电脑店？公司要配电脑的话我跟行政提一嘴。",
            },
            new Passerby
            {
                Title = "背书包的大学生",
                Greet = new[] { "嗨～", "同学你好！哦不对，你不是我们学校的吧。" },
                Chat = new[] { "室友都在攒钱配游戏本，我还在用高中的老电脑。", "听说显卡又要涨了，真的假的？", "学校里搞 AI 的那帮人天天抢显卡。" },
                Pitch = "真的吗？学生有优惠吗？我回去发班群里！",
            },
            new Passerby
            {
                Title = "遛弯的退休大爷",
                Greet = new[] { "小伙子，吃了吗？", "哟，年轻人，啥事儿？" },
                Chat = new[] { "我孙子过年回来，非要我给他买个电脑。", "以前这条街都是平房，现在全是铺子了。", "地铁站那边房租贵着呢，听说一个月上万。" },
                Pitch = "电脑我是不懂，回头让我孙子去你那看看。",
            },
            new Passerby
            {
                Title = "拎菜的阿姨",
                Greet = new[] { "哎，你好你好。", "小伙子，问路啊？" },
                Chat = new[] { "农贸市场今天的肉新鲜，下午就没了。", "我闺女说手机店那个收款码办起来挺方便。", "家具城最近在搞活动，我买了个凳子。" },
                Pitch = "我家那老电脑开机要五分钟，回头搬过来给你修修？",
            },
            new Passerby
            {
                Title = "骑车的外卖小哥",
                Greet = new[] { "兄弟快点，我这单要超时了！", "啥事？长话短说啊。" },
                Chat = new[] { "电子市场那边档口工作日才开，周末别白跑。", "这条街晚上九点以后基本都关门了。", "我送过好几单电脑配件，都是从 1788 上买的。" },
                Pitch = "行，送餐路过帮你看看，有人问我就说你家。",
            },
            new Passerby
            {
                Title = "戴眼镜的程序员",
                Greet = new[] { "嗯？", "你好。" },
                Chat = new[] { "内存现在便宜，能多插就多插。", "盒装的贵一点，但出问题好退换，散件就看运气。", "我自己装机，就嫌跑电子市场太远。" },
                Pitch = "有现货的话我下次直接去你那拿，省得等快递。",
            },
            new Passerby
            {
                Title = "推婴儿车的宝妈",
                Greet = new[] { "嘘——宝宝刚睡着。", "你好呀。" },
                Chat = new[] { "想买个电脑给孩子上网课，不知道什么配置够用。", "这附近有没有靠谱的店啊，怕被坑。", "房产中介那家小陈人挺好的，我们家房子就是他介绍的。" },
                Pitch = "那挺好，离家近，坏了也方便找你。",
            },
            new Passerby
            {
                Title = "穿校服的中学生",
                Greet = new[] { "叔叔好！", "你、你好……" },
                Chat = new[] { "我在攒零花钱买显卡，还差好多。", "同学都说网吧的电脑比家里的快。", "放学了不想回家写作业。" },
                Pitch = "哇，那我能去店里看看那些显卡吗？就看看！",
            },
        };

        private string[] PasserbyTips()
        {
            string news = S.Market.News.Count > 0 ? S.Market.News[S.Market.News.Count - 1].Headline : null;
            var tips = new System.Collections.Generic.List<string>
            {
                "租铺子去商业街中段的安居房产中介，挂牌上写着多少天后能租的要看清楚，有的要等好几个月。",
                "家具城卖货架、收银台，还有招牌灯箱、空调这些店里用的。",
                "手机店除了卖手机，还能给店里开收款码、装自助结账和监控。",
                "配件进货去电子市场的档口，坐地铁就到，工作日才开门。",
            };
            if (news != null) tips.Add($"你看新闻没？说是“{news}”。");
            return tips.ToArray();
        }

        private string PasserbyTimeLine()
        {
            int h = S.Clock.Now.Hour;
            if (h < 6) return "这么晚还在外面晃？早点回去睡吧。";
            if (h < 10) return "大早上的，精神不错嘛。";
            if (h < 14) return "快中午了，饿了吧？";
            if (h < 18) return "下午了，街上人慢慢多起来了。";
            if (h < 22) return "晚上这条街挺热闹的。";
            return "都这个点了，铺子差不多都关了。";
        }

        /// <summary>和路人说话（街上走近按 F）。</summary>
        public void OpenPasserbyTalk(int id, Action onClosed)
        {
            Passerby who = Passersby[Mathf.Abs(id) % Passersby.Length];
            var rng = new System.Random(id * 7919 + S.Clock.Today.TotalDays);
            string said = who.Greet[rng.Next(who.Greet.Length)] + "\n" + PasserbyTimeLine();
            bool pitched = false;
            OpenWindow("路人 · " + who.Title, 620, 360, w =>
            {
                Para(w.Content, "“" + said + "”", 19);
                var row = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(row, "随便聊聊", () =>
                {
                    said = who.Chat[rng.Next(who.Chat.Length)];
                    w.Rebuild();
                }, 17), -1, 44, 1);
                Ui.Size(Ui.Button(row, "打听点事", () =>
                {
                    string[] tips = PasserbyTips();
                    said = tips[rng.Next(tips.Length)];
                    w.Rebuild();
                }, 17), -1, 44, 1);
                var row2 = Ui.Row(w.Content, 44, 8);
                Ui.Size(Ui.Button(row2, S.HasShop ? "推荐一下我的店" : "（还没开店）", () =>
                {
                    pitched = true;
                    said = who.Pitch;
                    w.Rebuild();
                }, 17, Ui.ButtonBg, S.HasShop && !pitched), -1, 44, 1);
                Ui.Size(Ui.Button(row2, "再见", w.Close, 17, Ui.ButtonHot), -1, 44, 1);
            }, onClosed: onClosed);
        }
    }
}
