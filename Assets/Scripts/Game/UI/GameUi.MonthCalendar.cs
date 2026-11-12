using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private GameSession _calendarSession;
        private int _calendarDay;

        private sealed class CalendarItem
        {
            public ScheduleEntry Entry;
            public string Label, ContractId, HistoryBrandId;
            public bool Completed;
        }

        private void BuildMonthCalendar(Window w)
        {
            int today = S.Clock.Today.TotalDays;
            int first = _calendarDay - _calendarDay % GameDate.DaysPerMonth;
            var monthStart = new GameDate(first);
            var days = CalendarMonthItems(first).ToLookup(i => (int)(i.Entry.Minute / GameTimestamp.MinutesPerDay));
            var navigation = Ui.Row(w.Content, 46, 8);
            Ui.Size(Ui.Label(navigation, $"<b>{monthStart.Month}月</b>  <size=16>第{monthStart.Year}年</size>", 28), 0, -1, 1)
                .name = "CalendarMonthTitle";
            void Select(int day)
            {
                _calendarDay = day;
                w.ResetScrollOnRebuild = true;
                w.Rebuild();
            }
            Ui.Size(Ui.Button(navigation, "‹", () => Select(first - GameDate.DaysPerMonth), 24, Ui.ButtonBg, first > 0), 40, 38)
                .Tip("上个月").name = "CalendarPreviousMonth";
            Ui.Size(Ui.Button(navigation, "今天", () => Select(today), 17), 76, 38).name = "CalendarToday";
            Ui.Size(Ui.Button(navigation, "›", () => Select(first + GameDate.DaysPerMonth), 24), 40, 38)
                .Tip("下个月").name = "CalendarNextMonth";
            Ui.Size(Ui.Button(navigation, "天气", OpenCityClimate, 16), 76, 38).name = "CalendarWeather";

            const int gap = 4, cellHeight = 62, weekdayHeight = 26;
            int offset = (int)monthStart.Weekday;
            int rows = (offset + GameDate.DaysPerMonth + GameDate.DaysPerWeek - 1) / GameDate.DaysPerWeek;
            var month = Ui.Size(Ui.Rect(w.Content, "CalendarMonthGrid"), -1, weekdayHeight + rows * (cellHeight + gap));
            Ui.VList(month, gap, 0);
            var weekdays = Ui.Row(month, weekdayHeight, gap);
            foreach (string weekday in new[] { "一", "二", "三", "四", "五", "六", "日" })
                Ui.Size(Ui.Label(weekdays, weekday, 16, TextAnchor.MiddleCenter, Ui.Dim), 0, weekdayHeight, 1);
            for (int rowIndex = 0; rowIndex < rows; rowIndex++)
            {
                var row = Ui.Row(month, cellHeight, gap);
                for (int column = 0; column < GameDate.DaysPerWeek; column++)
                {
                    int number = rowIndex * GameDate.DaysPerWeek + column - offset;
                    if (number < 0 || number >= GameDate.DaysPerMonth)
                    { Ui.Size(Ui.Rect(row, "CalendarEmptyDay"), 0, cellHeight, 1); continue; }
                    int day = first + number;
                    BuildCalendarDateCell(row, new GameDate(day), day == today, days[day].ToArray(), cellHeight, () => Select(day));
                }
            }

            var selected = days[_calendarDay].ToArray();
            var agenda = Ui.FlatPanel(w.Content, "CalendarDayDetails", Ui.FlatBand, Ui.FlatCardEdge).rectTransform;
            Ui.VList(agenda, 5, 10);
            var heading = Ui.Row(agenda, 34);
            Ui.Size(Ui.Label(heading, "<b>" + DateLabel(_calendarDay) + "</b>", 20), 0, -1, 1).name = "CalendarSelectedDate";
            if (selected.Length == 0)
                Line(agenda, "当天暂无安排", 17, Ui.Dim);
            else
            {
                Ui.Size(Ui.Label(heading, selected.Length + " 项安排", 16, TextAnchor.MiddleRight, Ui.Dim), 110);
                var list = Ui.Scroll(agenda, 590 - rows * cellHeight);
                list.parent.parent.GetComponent<Image>().color = Color.clear;
                foreach (var item in selected) BuildCalendarDayItem(list, item, w);
            }
            w.LiveConversation = true;
            w.LiveKey = () => S.Clock.Now.TotalMinutes.ToString();
            w.LiveStamp = w.LiveKey();
        }

        private void BuildCalendarDateCell(Transform row, GameDate date, bool today, CalendarItem[] items, int height, Action select)
        {
            bool selected = date.TotalDays == _calendarDay;
            var fill = selected ? Color.Lerp(Ui.FlatRow, Ui.ButtonHot, .3f) : Ui.FlatBand;
            var button = Ui.Size(Ui.Button(row, "", select, 17, fill), 0, height, 1);
            button.name = "CalendarDay_" + date.TotalDays;
            Ui.FlatBorder(button.image, selected ? Ui.Accent : Ui.FlatCardEdge);
            button.gameObject.AddComponent<RectMask2D>();
            var caption = button.GetComponentInChildren<Text>();
            caption.text = date.Day + (today ? "  今" : "");
            caption.alignment = TextAnchor.MiddleLeft;
            caption.color = today || selected ? Ui.Accent : Ui.TextColor;
            caption.fontStyle = today || selected ? FontStyle.Bold : FontStyle.Normal;
            caption.rectTransform.Anchor(0, 1, 1, 1, 8, -31, 8, 4);
            if (items.Length == 0) return;

            var item = items.FirstOrDefault(i => !i.Completed) ?? items[0];
            Color color = CalendarItemColor(item);
            var chip = Ui.Panel(button.transform, "CalendarEvent", Color.Lerp(fill, color, .17f));
            chip.raycastTarget = false;
            chip.rectTransform.Anchor(0, 0, 1, 0, 5, 5, 5, -27);
            string title = item.Label.Length > 6 ? item.Label.Substring(0, 5) + "…" : item.Label;
            var label = Ui.Label(chip.transform, title, 15, TextAnchor.MiddleLeft, color);
            label.rectTransform.Fill(4);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (items.Length > 1)
                Ui.Label(button.transform, "+" + (items.Length - 1), 14, TextAnchor.MiddleRight, Ui.Dim)
                    .rectTransform.Anchor(0, 1, 1, 1, 8, -31, 8, 4);
            button.Tip(date + "\n" + string.Join("\n", items.Select(i => (i.Entry.HasTime ? i.Entry.TimeText : "全天") + " " + i.Entry.Text)));
        }

        private static string CalendarEntryLabel(ScheduleEntry e)
        {
            switch (e.Kind)
            {
                case "brand_expo": return "品牌展会";
                case "city_activity": case "appointment": return "朋友邀约";
                case "work": return "打工班次";
                case "repair": case "repairing": return "上门维修";
                case "parcel": return "快递到货";
                case "loan": return "贷款到期";
                case "npc_repayment": return "借款回收";
                case "cook": return "饭菜做好";
                case "kol_live": return "品牌直播";
                case "expo_order": return "采购单截止";
                default: return "我的安排";
            }
        }

        private static Color CalendarItemColor(CalendarItem item) => item.Completed ? Ui.Dim
            : item.Entry.Kind == "brand_expo" || item.Entry.Kind == "expo_order" ? Ui.Accent
            : item.Entry.Kind == "kol_live" ? new Color(.72f, .81f, .9f) : item.Entry.Kind == "loan" ? Ui.FlatDown : Ui.Good;

        private List<CalendarItem> CalendarMonthItems(int first)
        {
            long start = first * 1440L, until = (first + 30L) * 1440;
            var activities = S.CityActivities.Where(a => a.ScheduledStart >= start && a.ScheduledStart < until).ToDictionary(a => a.Id);
            var items = S.Schedule().Where(e => e.Minute >= start && e.Minute < until)
                .Select(e => new CalendarItem
                {
                    Entry = e,
                    Label = e.ActivityId != null && activities.TryGetValue(e.ActivityId, out var activity)
                        ? S.CityActivityService(activity)?.Name ?? CalendarEntryLabel(e) : CalendarEntryLabel(e)
                }).ToList();
            foreach (var activity in activities.Values.Where(a => !a.Active))
            {
                string label = S.CityActivityService(activity)?.Name ?? "活动";
                var entry = new ScheduleEntry { Minute = activity.ScheduledStart, TimeText = GameSession.ClockText((int)(activity.ScheduledStart % 1440)),
                    ActivityId = activity.Id, Kind = "city_activity", Text = label + " · "
                        + (S.City.HasPlace(activity.VenueId) ? S.City.GetPlace(activity.VenueId).Name : "场所失效")
                        + " · " + GameSession.CityPhaseText(activity.State) };
                items.Add(new CalendarItem { Entry = entry, Label = label, Completed = true });
            }
            var brandState = S.BrandBusiness;
            foreach (var e in brandState.Exhibits.Where(e => e.Format == 2))
            for (int offset = 0; offset < 4; offset++)
            {
                int day = e.Day + offset;
                if (day < first || day >= first + 30 || items.Any(i => i.Entry.ExpoId == e.Id && i.Entry.Minute / 1440 == day)) continue;
                bool done = e.Finished || e.Missed || day < S.Clock.Today.TotalDays;
                var entry = new ScheduleEntry { Minute = day * 1440L + 540, ExpoId = e.Id, Kind = "brand_expo", TimeText = "09:00—17:00",
                    Text = (brandState.Brands.FirstOrDefault(b => b.Id == e.BrandId)?.Name ?? "品牌") + " · 云帆会展中心 · 第" + (offset + 1) + "/4天"
                        + (e.Missed ? " · 未完成" : e.Finished ? " · 已闭展" : "") };
                items.Add(new CalendarItem { Entry = entry, Label = "展会 · " + (offset + 1) + "/4天", Completed = done });
            }
            foreach (var order in brandState.Orders.Where(o => o.Due >= first && o.Due < first + 30))
                items.Add(new CalendarItem { Entry = new ScheduleEntry { Minute = order.Due * 1440L, HasTime = false, Kind = "expo_order",
                    ExpoId = order.Source == "expo" && order.Id.EndsWith(":lead", StringComparison.Ordinal) ? order.Id.Substring(0, order.Id.Length - 5) : null,
                    Text = S.BrandProductName(order.ProductId) + " ×" + order.Count + " · " + (order.Done ? "已交付" : order.Failed ? "已逾期" : "交货截止日") },
                    Label = "订单交付", Completed = order.Done || order.Failed });
            var streaming = S.KolStreaming;
            foreach (var session in streaming.Sessions.Where(s => s.BrandId != null && s.StartedAt < until && s.EndsAt > start && s.LiveMinutes > 0))
                AddCalendarBroadcast(items, first, session.StartedAt, session.EndsAt, session.PersonId, session.BrandId,
                    brandState.Brands.FirstOrDefault(b => b.Id == session.BrandId)?.Name ?? "品牌",
                    session.ContractId, session.Settled ? "已结束" : "直播中", session.Settled);
            foreach (var contract in streaming.Contracts.Where(c => !c.Ended && c.Enabled && c.BrandId != null))
            {
                long cursor = Math.Max(start - 1440, S.Clock.Today.TotalDays * 1440L - 1440);
                for (int count = 0; count < 31; count++)
                {
                    long at = S.KolNextSpecialStart(contract, cursor);
                    if (at < 0 || at >= until) break;
                    int day = (int)(at / 1440);
                    bool expo = brandState.Exhibits.Any(e => !e.Missed && !e.Finished && day >= e.Day && day < e.Day + 4 && e.KolIds.Contains(contract.PersonId));
                    bool recorded = streaming.Sessions.Any(s => s.ContractId == contract.Id && s.ScheduledDay == day);
                    long end = at + (contract.EndMinute - contract.StartMinute + 1440) % 1440;
                    if (!expo && !recorded && end > S.Clock.Now.TotalMinutes)
                        AddCalendarBroadcast(items, first, at, end, contract.PersonId, contract.BrandId,
                            brandState.Brands.FirstOrDefault(b => b.Id == contract.BrandId)?.Name ?? "品牌", contract.Id, "计划直播", false);
                    cursor = at + 1;
                }
            }
            return items.OrderBy(i => i.Entry.Minute).ThenBy(i => i.Entry.Kind).ToList();
        }

        private void AddCalendarBroadcast(List<CalendarItem> items, int first, long start, long end, string person, string brand, string brandName, string contract, string status, bool completed)
        {
            long at = Math.Max(start, first * 1440L), until = Math.Min(end, (first + 30L) * 1440);
            while (at < until)
            {
                long partEnd = Math.Min(until, (at / 1440 + 1) * 1440);
                string time = KolTime((int)(at % 1440)) + "—" + (partEnd % 1440 == 0 ? "24:00" : KolTime((int)(partEnd % 1440)));
                items.Add(new CalendarItem { ContractId = completed ? null : contract, HistoryBrandId = completed ? brand : null,
                    Completed = completed, Label = "品牌直播", Entry = new ScheduleEntry { Minute = at, Kind = "kol_live", TimeText = time,
                        Text = S.CityPerson(person).Name + " · " + brandName
                            + " · " + status + (at > start ? "（跨日继续）" : "") } });
                at = partEnd;
            }
        }

        private void BuildCalendarDayItem(Transform list, CalendarItem item, Window w)
        {
            var e = item.Entry;
            var card = BrandCard(list, "");
            var summary = Ui.Row(card, -1, 10);
            var layout = summary.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = false;
            Ui.Size(Ui.Label(summary, e.HasTime ? e.TimeText : "全天", 16, TextAnchor.UpperLeft, CalendarItemColor(item)), 116, 23);
            Ui.Size(Para(summary, e.Text, 17, item.Completed ? Ui.Dim : Ui.TextColor), 0, -1, 1);
            RectTransform actions = null;
            RectTransform Actions() => actions ??= Ui.Row(card, 40, 8);
            void ActionButton(string title, Action click, Color? color = null) =>
                Ui.Size(Ui.Button(Actions(), title, click, 16, color), 160, 36);
            if (e.ActivityId != null)
            {
                string id = e.ActivityId;
                ActionButton("查看活动详情", () => OpenCityActivityDetail(id));
                if (!item.Completed) ScheduleActivityGoButton(Actions(), id);
            }
            if (e.ExpoId != null)
            {
                string id = e.ExpoId;
                var expo = S.BrandBusiness.Exhibits.FirstOrDefault(x => x.Id == id);
                bool closed = expo?.Finished == true || expo?.Missed == true;
                if (e.Kind == "expo_order") ActionButton("查看本届采购订单", () => OpenExpoOrders(id));
                else if (closed) ActionButton("查看本届结算", () => OpenExpoSettlement(id));
                else ActionButton("查看参展安排", () => OpenBookedExpo(id));
                if (!item.Completed) ExpoTravelButton(Actions());
            }
            if (item.ContractId != null)
                ActionButton("查看代言与直播安排", () =>
                { _selectedKolContract = item.ContractId; _brandTab = BrandKolTab; _kolPage = "我的代言人"; OpenBrandBusiness(); });
            if (item.HistoryBrandId != null)
                ActionButton("查看品牌专场记录", () =>
                { _brandId = item.HistoryBrandId; _brandTab = BrandKolTab; _kolPage = "专场记录"; OpenBrandBusiness(); });
            if (e.Kind == "parcel" || e.Kind == "cook" || e.Kind == "repair" || e.Kind == "repairing")
                ActionButton("前往住处", () => { CloseAllWindows(); _ctrl.GoToPlace(S.Home); });
            if (e.Kind == "work" && S.WorkPlace != null)
                ActionButton("前往打工地点", () => { CloseAllWindows(); _ctrl.GoToPlace(S.WorkPlace); });
            if (e.Kind == "loan") ActionButton("查看贷款", OpenLoansApp);
            if (e.Kind == "repair")
            {
                string target = S.HomeVisits.FirstOrDefault(v => v.StartMinute == e.Minute)?.TargetId;
                if (target != null) ActionButton("取消维修（不退款）", () => { S.CancelRepair(target); w.Rebuild(); }, Ui.Warn);
            }
        }
    }
}
