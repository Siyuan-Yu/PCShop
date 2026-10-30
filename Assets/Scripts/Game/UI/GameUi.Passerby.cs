using System;
using System.Collections.Generic;
using PCShopSell.Core.Session;

namespace PCShopSell.Game.UI
{
    /// <summary>街上的人都是街坊：走近按 F 打开和店员一样的对话框（立绘、熟络度、闲聊、送礼、听他讲自己的事），另外能打听点街上的消息。</summary>
    public sealed partial class GameUi
    {
        private string[] StreetTips()
        {
            string news = S.Market.News.Count > 0 ? S.Market.News[S.Market.News.Count - 1].Headline : null;
            var tips = new List<string>
            {
                "租铺子去商业街中段的安居房产中介，挂牌上写着多少天后能租的要看清楚，有的要等好几个月。",
                "家具城卖货架、收银台，还有招牌灯箱、空调这些店里用的。",
                "手机店除了卖手机，还能给店里开收款码、装自助结账和监控。",
                "配件进货去电子市场的档口，坐地铁就到，工作日才开门。",
            };
            if (news != null) tips.Add($"你看新闻没？说是“{news}”。");
            return tips.ToArray();
        }

        /// <summary>他现在在干嘛（写在对话框标题的职务后面）。</summary>
        private string ResidentDoing(ResidentDef r, ResidentTrip? trip)
        {
            string work = S.StaffWorkplace(r.Id)?.Name;
            switch (trip)
            {
                case ResidentTrip.ToWork: return work != null ? $"正去{work}上班" : "正赶路";
                case ResidentTrip.ToHome: return r.FromMetro ? "下班去坐地铁" : "下班回城中村";
                case ResidentTrip.Stroll: return "在街上逛";
                default: return work != null ? $"在{work}上班" : "";
            }
        }

        /// <summary>和街上的人说话（走近按 F）。熟络度和在店里见到他时是同一份。</summary>
        public void OpenResidentTalk(ResidentDef r, Action onClosed)
        {
            StaffDef p = r.Person;
            ResidentTrip? trip = Residents.TripAt(r, S.Clock.MinuteOfDay);
            string doing = ResidentDoing(r, trip);
            string role = doing.Length > 0 ? p.Role + " · " + doing : p.Role;
            var rng = new Random(p.Id.GetHashCode() ^ S.Clock.Today.TotalDays);
            string greeting = p.LineFor(S.Clock.Today.TotalDays);
            OpenDialogue(p.Id, p.Name, role, null, greeting, 4, ctx =>
            {
                var list = new List<TalkOpt>();
                list.Add(new TalkOpt
                {
                    Label = "随便聊聊（8 分钟）",
                    Blocker = S.StaffChatBlocker(p.Id),
                    Act = () =>
                    {
                        string said = S.StaffChat(p);
                        if (said != null) ctx.Reply = said;
                        ctx.W.Rebuild();
                    },
                });
                list.Add(new TalkOpt
                {
                    Label = "送他点东西（从背包里挑）",
                    Blocker = S.StaffGiftBlocker(p.Id),
                    Hot = true,
                    Act = () => OpenStaffGiftPicker(p, ctx),
                });
                list.Add(new TalkOpt
                {
                    Label = "听他讲讲自己的事（30 分钟）",
                    Blocker = S.StaffStoryBlocker(p.Id),
                    Act = () =>
                    {
                        string said = S.StaffStory(p);
                        if (said != null) ctx.Reply = said;
                        ctx.W.Rebuild();
                    },
                });
                list.Add(new TalkOpt
                {
                    Label = "打听点街上的事",
                    Act = () =>
                    {
                        string[] tips = StreetTips();
                        ctx.Reply = p.Name + "：" + tips[rng.Next(tips.Length)];
                        ctx.W.Rebuild();
                    },
                });
                return list;
            }, null, p.Id, onClosed);
        }
    }
}
