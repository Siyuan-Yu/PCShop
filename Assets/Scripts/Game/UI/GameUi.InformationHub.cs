using System;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void OpenMarketView(int tab)
        {
            S.ReadNews();
            OpenWorkspace("market", "market:" + tab, "行情", 1120, 850, w =>
            {
                var tabs = Ui.Row(w.Content, 38, 6);
                string[] names = { "价格走势", "行业动态", "竞争事件", "模型目录" };
                for (int i = 0; i < names.Length; i++)
                {
                    int target = i;
                    Ui.Size(Ui.Button(tabs, names[i], () => OpenMarketView(target), 17, tab == i ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1).name = "MarketTab_" + i;
                }
                switch (tab)
                {
                    case 1: BuildIndustryNews(w); break;
                    case 2: BuildCompetitionMarket(w); break;
                    case 3: BuildModelCatalog(w); break;
                    default: BuildPriceMarket(w); break;
                }
            });
        }

        private void OpenCalendar(int tab)
        {
            OpenWorkspace("calendar", "calendar:" + tab, "日程", 1040, 820, w =>
            {
                var tabs = Ui.Row(w.Content, 38, 6);
                Ui.Size(Ui.Button(tabs, "我的安排", () => OpenCalendar(0), 17, tab == 0 ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1).name = "CalendarPersonal";
                Ui.Size(Ui.Button(tabs, "城市活动", () => OpenCalendar(1), 17, tab == 1 ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1).name = "CalendarActivities";
                Ui.Size(Ui.Button(tabs, "天气", OpenCityClimate, 17), 110, 38);
                if (tab == 0) BuildPersonalSchedule(w); else BuildActivitySchedule(w);
            });
        }

        private void BuildPeopleNavigation(Window w, string selected)
        {
            var tabs = Ui.Row(w.Content, 38, 6);
            void Tab(string id, string title, Action open)
            { Ui.Size(Ui.Button(tabs, title, open, 17, selected == id ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1).name = "PeopleTab_" + id; }
            Tab("people", "已认识的人", OpenCityPeople);
            Tab("relations", "关系图", OpenRelationsPanel);
            Tab("contacts", "通讯录", OpenContactsPanel);
            Tab("messages", "消息", OpenChatPanel);
        }
    }
}
