using System;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void OpenMarketView(int tab)
        {
            S.ReadNews();
            OpenWorkspace("market", "market:" + tab, "物价", 1120, 850, w =>
            {
                var tabs = Ui.Row(w.Content, 38, 6);
                string[] names = { "价格走势", "行业动态", "竞争事件", "模型目录" };
                for (int i = 0; i < names.Length; i++)
                {
                    int target = i;
                    Ui.Tab(tabs, names[i], () => OpenMarketView(target), tab == i).name = "MarketTab_" + i;
                }
                switch (tab)
                {
                    case 1: BuildIndustryNews(w); break;
                    case 2: BuildCompetitionMarket(w); break;
                    case 3: BuildModelCatalog(w); break;
                    default: BuildPriceMarket(w); break;
                }
            }, isTab: true);
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
            if (monthView) OpenPhoneAppWorkspace("calendar", "calendar", "日程", BuildMonthCalendar);
            else OpenWorkspace("calendar", "calendar", "日程", 820, 660, BuildPersonalSchedule);
        }

        private Window _peopleReturnPhone;

        private void OpenPeopleWorkspace(string tab, Action<Window> build, Window phone = null)
        {
            if (!_navigation.TryGetValue("people", out var state) || state.Window == null || state.Window.Closed || phone != null)
                _peopleReturnPhone = phone;

            void Build(Window w)
            {
                BuildPeopleNavigation(w, tab);
                build(w);
            }
            if (tab == "contacts" || _peopleReturnPhone is { Closed: false })
                OpenPhoneAppWorkspace("people", tab, "人物", Build, isTab: true);
            else OpenWorkspace("people", tab, "人物", 1300, 850, Build, isTab: true);
        }

        private void BuildPeopleNavigation(Window w, string selected)
        {
            var header = Ui.Row(w.Content, 38, 12);
            var tabs = Ui.Size(Ui.Row(header, 38, 6), 0, 38, 1);
            void Tab(string id, string title, Action open)
            { Ui.Tab(tabs, title, open, selected == id, 108).name = "PeopleTab_" + id; }
            Tab("people", "已认识的人", OpenCityPeople);
            Tab("relations", "关系图", OpenRelationsPanel);
            Tab("contacts", "通讯录", OpenContactsPanel);
            if (_peopleReturnPhone != null && !_peopleReturnPhone.Closed)
                Ui.Size(Ui.Button(header, "‹ 返回手机", w.Close, 16), 132, 38).name = "PeopleReturnPhone";
        }
    }
}
