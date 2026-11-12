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

        private void OpenCalendar(int? day = null)
        {
            bool monthView = S.HasCalendar;
            bool alreadyOpen = _navigation.TryGetValue("calendar", out var state) && state.Window != null && !state.Window.Closed;
            if (monthView && (day.HasValue || !alreadyOpen || _calendarSession != S))
            {
                _calendarSession = S;
                _calendarDay = day ?? S.Clock.Today.TotalDays;
                if (alreadyOpen) state.Window.ResetScrollOnRebuild = true;
            }
            OpenWorkspace("calendar", "calendar", monthView ? "日历" : "日程", monthView ? 820 : 1040, monthView ? 850 : 660, w =>
            {
                if (monthView) BuildMonthCalendar(w);
                else BuildPersonalSchedule(w);
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
