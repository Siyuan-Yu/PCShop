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
        private int _calendarMonth, _calendarDay;

        private sealed class CalendarItem
        {
            public ScheduleEntry Entry;
            public string Label, ContractId, HistoryBrandId;
            public bool Completed;
        }

        private void BuildMonthCalendar(Window w)
        {
            int today = S.Clock.Today.TotalDays;
            if (_calendarSession != S)
            { _calendarSession = S; _calendarMonth = today / 30; _calendarDay = today; }
            int first = _calendarMonth * 30;
            _calendarDay = Math.Max(first, Math.Min(first + 29, _calendarDay));
            var items = CalendarMonthItems(first);
            var days = items.ToLookup(i => (int)(i.Entry.Minute / 1440));
            var navigation = Ui.Row(w.Content, 42, 10);
            Ui.Size(Ui.Button(navigation, "‹", () => ChangeCalendarMonth(-1, w), 22,
                Ui.ButtonBg, _calendarMonth > 0), 42, 42).name = "CalendarPreviousMonth";
            var date = new GameDate(first);
            Ui.Size(Ui.Label(navigation, "第" + date.Year + "年 " + date.Month + "月", 24, TextAnchor.MiddleCenter, Ui.Accent), 150, 42);
            Ui.Size(Ui.Button(navigation, "›", () => ChangeCalendarMonth(1, w), 22), 42, 42).name = "CalendarNextMonth";
            Ui.Size(Ui.Rect(navigation, "CalendarNavigationSpace"), 0, 42, 1);
            Ui.Size(Ui.Button(navigation, "回到今天 · " + S.Clock.Today.Month + "月" + S.Clock.Today.Day + "日", () =>
            { _calendarMonth = today / 30; _calendarDay = today; w.Rebuild(); }, 17), 210, 42).name = "CalendarToday";

            var upcoming = S.BrandBusiness.Exhibits.Where(e => e.Format == 2 && !e.Finished && !e.Missed)
                .OrderBy(e => e.Day).FirstOrDefault();
            var reminder = Ui.Panel(w.Content, "CalendarExpoReminder", Ui.PanelBg2).rectTransform;
            Ui.Size(reminder, -1, 58); Ui.VList(reminder, 0, 10);
            if (upcoming != null)
            {
                Para(reminder, "<color=#E8BE62>已报名 · 品牌展会</color>   " + new GameDate(upcoming.Day)
                    + "09:00—10:00报到 · 云帆会展中心   <size=15>"
                    + (upcoming.Day > today ? "距开幕" + (upcoming.Day - today) + "天" : "已到展期")
                    + " · 提前一天及出发前提醒</size>", 17);
            }
            else
            {
                Para(reminder, "<color=#E8BE62>今天 · " + S.Clock.Today + " " + Ui.WeekdayName(S.Clock.Today.Weekday)
                    + "</color>   <size=15>点选日期查看当天安排</size>", 17);
            }

            // Anchor both columns explicitly: layout-derived minimum text widths must not squeeze the month.
            var split = Ui.Rect(w.Content, "CalendarColumns"); Ui.Size(split, -1, 570);
            var month = Ui.Rect(split, "CalendarMonthGrid").Anchor(0, 0, .72f, 1);
            Ui.VList(month, 5, 0);
            var divider = Ui.Panel(split, "CalendarColumnDivider", new Color(.33f, .39f, .43f));
            divider.raycastTarget = false;
            divider.rectTransform.Anchor(.74f, 0, .74f, 1); divider.rectTransform.sizeDelta = new Vector2(1, 0);
            var weekdays = Ui.Row(month, 28, 5);
            foreach (string weekday in new[] { "一", "二", "三", "四", "五", "六", "日" })
                Ui.Size(Ui.Label(weekdays, weekday, 17, TextAnchor.MiddleCenter, Ui.Dim), 0, 28, 1);
            int offset = first % 7;
            int rows = (offset + 30 + 6) / 7;
            float cellHeight = (570 - 28 - rows * 5) / rows;
            for (int rowIndex = 0; rowIndex < rows; rowIndex++)
            {
                var row = Ui.Row(month, cellHeight, 5);
                for (int column = 0; column < 7; column++)
                {
                    int number = rowIndex * 7 + column - offset;
                    if (number < 0 || number >= 30)
                    { Ui.Size(Ui.Rect(row, "CalendarEmptyDay"), 0, cellHeight, 1); continue; }
                    int day = first + number;
                    var entries = days[day].ToArray();
                    var button = Ui.Size(Ui.Button(row, "", () => { _calendarDay = day; w.Rebuild(); }, 17,
                        day == _calendarDay ? new Color(.30f, .27f, .20f) : Ui.PanelBg2), 0, cellHeight, 1);
                    button.name = "CalendarDay_" + day;
                    var image = button.GetComponent<Image>(); image.sprite = null;
                    foreach (var effect in button.GetComponents<Shadow>()) effect.enabled = false;
                    if (day == _calendarDay)
                    {
                        var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
                        outline.enabled = true; outline.effectColor = Ui.Accent; outline.effectDistance = new Vector2(1, -1);
                    }
                    button.gameObject.AddComponent<RectMask2D>();
                    var content = Ui.Rect(button.transform, "CalendarDayContent").Fill(5); Ui.VList(content, 1, 0);
                    Ui.Size(Ui.Label(content, (number + 1) + (day == today ? " · 今" : ""), 17,
                        TextAnchor.MiddleLeft, day == today ? Ui.Accent : Ui.TextColor), -1, 22);
                    foreach (var item in entries.Take(entries.Length > 2 ? 1 : 2))
                    {
                        var text = Ui.Size(Ui.Label(content, item.Label, 14, color: CalendarItemColor(item)), -1, 19);
                        text.verticalOverflow = VerticalWrapMode.Truncate;
                    }
                    if (entries.Length > 2) Ui.Size(Ui.Label(content, "还有" + (entries.Length - 1) + "项", 13, color: Ui.Dim), -1, 18);
                    button.Tip(new GameDate(day) + (entries.Length == 0 ? " · 暂无安排"
                        : "\n" + string.Join("\n", entries.Select(i => i.Entry.TimeText + " " + i.Entry.Text))));
                }
            }
            var details = Ui.Rect(split, "CalendarDayDetails").Anchor(.76f, 0, 1, 1);
            Ui.VList(details, 5, 0);
            var selectedDate = new GameDate(_calendarDay);
            Line(details, selectedDate.Month + "月" + selectedDate.Day + "日 · " + Ui.WeekdayName(selectedDate.Weekday), 22, Ui.Accent);
            var selected = days[_calendarDay].ToArray();
            Line(details, selected.Length == 0 ? "当天暂无安排" : selected.Length + "项安排", 16, Ui.Dim);
            if (selected.Length > 0)
            {
                var list = Ui.Scroll(details, 485);
                list.parent.parent.GetComponent<Image>().color = Color.clear;
                foreach (var item in selected) BuildCalendarDayItem(list, item, w);
            }
            var footer = Ui.Row(w.Content, 36, 10);
            if (!GameSession.FirstChapterTrial)
                Ui.Size(Ui.Label(footer, "<color=#E8BE62>●</color> 展会 / 交付    <color=#B8CDE3>●</color> 直播    <color=#B6D578>●</color> 邀约 / 生活", 15, color: Ui.Dim), 0, 36, 1);
            Ui.Size(Ui.Button(footer, "城市活动", () => OpenCalendar(1), 16), 110, 34);
            Ui.Size(Ui.Button(footer, "天气", OpenCityClimate, 16), 80, 34);
            w.LiveConversation = true;
            w.LiveKey = () => S.Clock.Now.TotalMinutes.ToString();
            w.LiveStamp = w.LiveKey();
        }

        private void ChangeCalendarMonth(int delta, Window w)
        {
            _calendarMonth = Math.Max(0, _calendarMonth + delta);
            _calendarDay = _calendarMonth * 30 + Math.Min(29, _calendarDay % 30);
            w.Rebuild();
        }

        private static string CalendarEntryLabel(ScheduleEntry e)
        {
            switch (e.Kind)
            {
                case "brand_expo": return "品牌展会";
                case "city_activity": case "appointment": return "邀约 / 活动";
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

        private static Color CalendarItemColor(CalendarItem item) => item.Entry.Kind == "brand_expo" || item.Entry.Kind == "expo_order" ? Ui.Accent
            : item.Entry.Kind == "kol_live" ? new Color(.72f, .81f, .9f) : item.Entry.Kind == "loan" ? Ui.Warn : Ui.Good;

        private List<CalendarItem> CalendarMonthItems(int first)
        {
            long start = first * 1440L, until = (first + 30L) * 1440;
            var items = S.Schedule().Where(e => e.Minute >= start && e.Minute < until)
                .Select(e => new CalendarItem { Entry = e, Label = CalendarEntryLabel(e) }).ToList();
            foreach (var activity in S.CityActivities.Where(a => !a.Active && a.ScheduledStart >= start && a.ScheduledStart < until))
            {
                var entry = new ScheduleEntry { Minute = activity.ScheduledStart, TimeText = GameSession.ClockText((int)(activity.ScheduledStart % 1440)),
                    ActivityId = activity.Id, Kind = "city_activity", Text = (S.City.HasPlace(activity.VenueId) ? S.City.GetPlace(activity.VenueId).Name : "场所失效") + " · " + GameSession.CityPhaseText(activity.State) };
                items.Add(new CalendarItem { Entry = entry, Label = "活动 · " + GameSession.CityPhaseText(activity.State), Completed = true });
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
            Line(card, e.HasTime ? e.TimeText : "全天 / 当天截止", 16, Ui.Dim);
            Para(card, e.Text, 18, CalendarItemColor(item));
            if (e.ActivityId != null)
            {
                string id = e.ActivityId;
                Ui.Size(Ui.Button(card, "查看活动详情", () => OpenCityActivityDetail(id), 17), -1, 36);
                if (!item.Completed) ScheduleActivityGoButton(card, id);
            }
            if (e.ExpoId != null)
            {
                string id = e.ExpoId;
                var expo = S.BrandBusiness.Exhibits.FirstOrDefault(x => x.Id == id);
                Ui.Size(Ui.Button(card, e.Kind == "expo_order" ? "查看本届采购订单" : expo?.Finished == true || expo?.Missed == true ? "查看本届结算" : "查看参展安排", () =>
                { if (e.Kind == "expo_order") OpenExpoOrders(id); else if (expo?.Finished == true || expo?.Missed == true) OpenExpoSettlement(id); else OpenBookedExpo(id); }, 17), -1, 36);
                if (!item.Completed) ExpoTravelButton(card);
            }
            if (item.ContractId != null)
                Ui.Size(Ui.Button(card, "查看代言与直播安排", () =>
                { _selectedKolContract = item.ContractId; _brandTab = BrandKolTab; _kolPage = "我的代言人"; OpenBrandBusiness(); }, 17), -1, 36);
            if (item.HistoryBrandId != null)
                Ui.Size(Ui.Button(card, "查看品牌专场记录", () =>
                { _brandId = item.HistoryBrandId; _brandTab = BrandKolTab; _kolPage = "专场记录"; OpenBrandBusiness(); }, 17), -1, 36);
            if (e.Kind == "parcel" || e.Kind == "cook" || e.Kind == "repair" || e.Kind == "repairing")
                Ui.Size(Ui.Button(card, "前往住处", () => { CloseAllWindows(); _ctrl.GoToPlace(S.Home); }, 17), -1, 36);
            if (e.Kind == "work" && S.WorkPlace != null)
                Ui.Size(Ui.Button(card, "前往打工地点", () => { CloseAllWindows(); _ctrl.GoToPlace(S.WorkPlace); }, 17), -1, 36);
            if (e.Kind == "loan") Ui.Size(Ui.Button(card, "查看贷款", OpenLoansApp, 17), -1, 36);
            if (e.Kind == "repair")
            {
                string target = S.HomeVisits.FirstOrDefault(v => v.StartMinute == e.Minute)?.TargetId;
                if (target != null) Ui.Size(Ui.Button(card, "取消维修（不退款）", () => { S.CancelRepair(target); w.Rebuild(); }, 16, Ui.Warn), -1, 36);
            }
        }
    }
}
