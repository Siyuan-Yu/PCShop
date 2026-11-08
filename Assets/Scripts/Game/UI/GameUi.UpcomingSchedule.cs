using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private const int UpcomingScheduleLimit = 7;
        private const float UpcomingScheduleWidth = 330f;
        private static readonly Vector2 UpcomingScheduleRestPosition = new Vector2(-16, -86);
        private RectTransform _upcomingScheduleHud;
        private readonly Button[] _upcomingScheduleRows = new Button[UpcomingScheduleLimit];
        private readonly Text[] _upcomingScheduleTimes = new Text[UpcomingScheduleLimit], _upcomingScheduleTitles = new Text[UpcomingScheduleLimit];
        private CalendarItem[] _upcomingScheduleItems = Array.Empty<CalendarItem>();
        private GameSession _upcomingScheduleSession;
        private long _upcomingScheduleMinute = -1;
        private float _upcomingScheduleRefreshAt;
        private bool _upcomingScheduleDirty = true;

        private void BuildUpcomingScheduleHud(Transform parent)
        {
            _upcomingScheduleHud = Ui.Panel(parent, "UpcomingScheduleHud", Ui.PanelBg).rectTransform;
            _upcomingScheduleHud.anchorMin = _upcomingScheduleHud.anchorMax = new Vector2(1, 1);
            _upcomingScheduleHud.pivot = new Vector2(1, 1);
            _upcomingScheduleHud.anchoredPosition = UpcomingScheduleRestPosition;
            _upcomingScheduleHud.sizeDelta = new Vector2(UpcomingScheduleWidth, 47 + 43 * UpcomingScheduleLimit);
            var border = _upcomingScheduleHud.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(.4f, .45f, .49f); border.effectDistance = new Vector2(1, -1);
            var title = Ui.Label(_upcomingScheduleHud, "接下来的安排", 18);
            title.rectTransform.Anchor(0, 1, .7f, 1, 14, -38, 0, 8);
            var range = Ui.Label(_upcomingScheduleHud, "未来7天", 13, TextAnchor.MiddleRight, Ui.Dim);
            range.rectTransform.Anchor(.7f, 1, 1, 1, 0, -38, 14, 8);
            var divider = Ui.Panel(_upcomingScheduleHud, "UpcomingDivider", new Color(.33f, .39f, .43f));
            divider.raycastTarget = false;
            divider.rectTransform.Anchor(0, 1, 1, 1, 12, -41, 12, 40);
            for (int index = 0; index < UpcomingScheduleLimit; index++)
            {
                int slot = index;
                var button = Ui.Button(_upcomingScheduleHud, "", () => OpenUpcomingScheduleDetail(slot), 16, Color.clear);
                button.name = "UpcomingScheduleRow_" + index;
                var rect = (RectTransform)button.transform;
                rect.Anchor(0, 1, 1, 1, 12, -84 - index * 43, 12, 42 + index * 43);
                button.GetComponent<Image>().sprite = null;
                foreach (var effect in button.GetComponents<Shadow>()) effect.enabled = false;
                var time = Ui.Label(rect, "", 14, TextAnchor.MiddleLeft, Ui.Dim);
                time.rectTransform.Anchor(0, 0, .41f, 1, 2, 0, 4, 0);
                time.verticalOverflow = VerticalWrapMode.Truncate;
                var name = Ui.Label(rect, "", 16);
                name.rectTransform.Anchor(.41f, 0, .96f, 1, 0, 0, 3, 0);
                name.verticalOverflow = VerticalWrapMode.Truncate;
                var arrow = Ui.Label(rect, "›", 20, TextAnchor.MiddleRight, Ui.Dim);
                arrow.rectTransform.Anchor(.96f, 0, 1, 1);
                _upcomingScheduleRows[index] = button;
                _upcomingScheduleTimes[index] = time;
                _upcomingScheduleTitles[index] = name;
            }
            _upcomingScheduleHud.gameObject.SetActive(false);
        }

        private void UpdateUpcomingScheduleHud()
        {
            if (_upcomingScheduleHud == null || S == null) return;
            if (MainMenuOpen || S.IsGameOver || ExpoHudOpen)
            { _upcomingScheduleHud.gameObject.SetActive(false); return; }
            if (_upcomingScheduleSession != S)
            { _upcomingScheduleSession = S; _upcomingScheduleDirty = true; _upcomingScheduleRefreshAt = 0; }
            if (Time.unscaledTime < _upcomingScheduleRefreshAt)
            { _upcomingScheduleHud.gameObject.SetActive(_upcomingScheduleItems.Length > 0); return; }
            _upcomingScheduleRefreshAt = Time.unscaledTime + 1;
            long now = S.Clock.Now.TotalMinutes;
            if (!_upcomingScheduleDirty && _upcomingScheduleMinute == now)
            { _upcomingScheduleHud.gameObject.SetActive(_upcomingScheduleItems.Length > 0); return; }
            _upcomingScheduleDirty = false; _upcomingScheduleMinute = now;
            int first = S.Clock.Today.TotalDays / 30 * 30;
            long limit = now + 7 * 1440L;
            var items = CalendarMonthItems(first);
            if (limit / 1440 >= first + 30) items.AddRange(CalendarMonthItems(first + 30));
            // One four-day exhibition is one activity in this compact list, not four repeated rows.
            var unique = items.Where(i => !i.Completed).GroupBy(i => i.Entry.Kind == "brand_expo" ? "expo:" + i.Entry.ExpoId
                : i.Entry.Kind + ":" + i.Entry.Minute + ":" + i.Entry.ActivityId + ":" + i.ContractId + ":" + i.Entry.Text)
                .Select(group => group.OrderBy(i => i.Entry.Minute).First());
            _upcomingScheduleItems = unique.Where(i => UpcomingScheduleEligible(i, now, limit))
                .OrderBy(i => UpcomingScheduleSortAt(i, now)).ThenBy(i => i.Entry.Kind).Take(UpcomingScheduleLimit).ToArray();
            _upcomingScheduleHud.gameObject.SetActive(_upcomingScheduleItems.Length > 0);
            _upcomingScheduleHud.sizeDelta = new Vector2(UpcomingScheduleWidth, 47 + 43 * _upcomingScheduleItems.Length);
            for (int index = 0; index < UpcomingScheduleLimit; index++)
            {
                bool active = index < _upcomingScheduleItems.Length;
                _upcomingScheduleRows[index].gameObject.SetActive(active);
                if (!active) continue;
                var item = _upcomingScheduleItems[index];
                bool soon = item.Entry.HasTime && item.Entry.Minute <= now + 60;
                _upcomingScheduleTimes[index].text = UpcomingScheduleTime(item.Entry, now);
                _upcomingScheduleTimes[index].color = soon ? Ui.Accent : Ui.Dim;
                _upcomingScheduleTitles[index].text = UpcomingScheduleTitle(item);
                _upcomingScheduleTitles[index].color = soon ? Ui.Accent : Ui.TextColor;
            }
        }

        private static long UpcomingScheduleSortAt(CalendarItem item, long now) => !item.Entry.HasTime && item.Entry.Minute / 1440 == now / 1440 ? now : item.Entry.Minute;

        private bool UpcomingScheduleEligible(CalendarItem item, long now, long limit)
        {
            var e = item.Entry;
            if (UpcomingScheduleSortAt(item, now) > limit) return false;
            if (e.ActivityId != null && S.CityActivities.FirstOrDefault(a => a.Id == e.ActivityId)?.State == CityActivityPhase.Running) return false;
            if (e.Kind == "brand_expo")
            {
                var expo = S.BrandBusiness.Exhibits.FirstOrDefault(x => x.Id == e.ExpoId);
                return expo != null && !expo.Attending && now <= expo.Day * 1440L + 600 && e.Minute <= limit;
            }
            if (!e.HasTime) return e.Minute / 1440 >= now / 1440;
            return e.Minute >= now || e.ActivityId != null && e.Minute >= now - 60;
        }

        private string UpcomingScheduleTitle(CalendarItem item)
        {
            var e = item.Entry;
            if (e.Kind == "brand_expo") return "展会报到";
            if (e.Kind == "repair") return "师傅上门";
            if (e.ActivityId != null)
            {
                var activity = S.CityActivities.FirstOrDefault(a => a.Id == e.ActivityId);
                return (activity == null ? null : S.CityActivityService(activity)?.Name) ?? "朋友活动";
            }
            return item.Label;
        }

        private static string UpcomingScheduleTime(ScheduleEntry e, long now)
        {
            var date = new GameDate((int)(e.Minute / 1440));
            long days = date.TotalDays - now / 1440;
            string day = days == 0 ? "今天" : days == 1 ? "明天" : date.Month + "月" + date.Day + "日";
            if (!e.HasTime) return day;
            if (days == 0 && e.Minute < now) return "已到时间";
            if (days == 0 && e.Minute % 1440 >= 1080) day = "今晚";
            return day + " " + GameSession.ClockText((int)(e.Minute % 1440));
        }

        private void OpenUpcomingScheduleDetail(int slot)
        {
            if (slot < 0 || slot >= _upcomingScheduleItems.Length) return;
            var item = _upcomingScheduleItems[slot];
            OpenWindow(UpcomingScheduleTitle(item), 620, 610, w =>
            {
                Line(w.Content, new GameDate((int)(item.Entry.Minute / 1440)).ToString(), 20, Ui.Accent);
                var list = Ui.Scroll(w.Content, 410);
                BuildCalendarDayItem(list, item, w);
                var actions = Ui.Row(w.Content, 42, 10);
                Ui.Size(Ui.Button(actions, "返回", w.Close, 18), -1, 42, 1);
                Ui.Size(Ui.Button(actions, "查看当天日程", () =>
                {
                    _calendarSession = S; _calendarDay = (int)(item.Entry.Minute / 1440); _calendarMonth = _calendarDay / 30;
                    w.Close(); OpenSchedulePanel();
                }, 18), -1, 42, 1);
            });
        }
    }
}
