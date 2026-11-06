using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private void AddExpoSchedule(List<ScheduleEntry> entries)
        {
            foreach (var e in _brands.Exhibits.Where(e => e.Format == 2 && !e.Finished && !e.Missed))
            for (int offset = 0; offset < 4; offset++)
            {
                int day = e.Day + offset;
                if (day < BrandDay) continue;
                entries.Add(new ScheduleEntry { Minute = day * 1440L + 540, ExpoId = e.Id,
                    Day = DescribeDay(day), TimeText = "09:00—17:00", Kind = "brand_expo",
                    Text = (Brand(e.BrandId)?.Name ?? "品牌") + " · 云帆会展中心 · 第" + (offset + 1) + "/4天"
                        + (offset == 0 ? "（09:00—10:00报到，请提前出发）" : "（连续参展）") });
            }
        }

        private void CheckExpoCalendarReminders(long from, long to)
        {
            foreach (var e in _brands.Exhibits.Where(e => e.Format == 2 && !e.Finished && !e.Missed && !e.Attending))
            {
                long opening = e.Day * 1440L + 540;
                if (to < opening - 2880 || to > opening + 60) continue;
                if (e.DepartureReminderAt == 0)
                {
                    int journey = CityTravelMinutes(City.GetPlace("life_expo"));
                    e.DepartureReminderAt = opening - Math.Min(1440, Math.Max(30, (long)journey + 15));
                }
                long[] times = { opening - 1440, e.DepartureReminderAt, opening };
                string[] messages = {
                    "明天09:00开幕，请在09:00—10:00到云帆会展中心报到，连续参加4天。",
                    "请提前出发前往云帆会展中心，" + new GameDate(e.Day) + "09:00—10:00报到。",
                    "展会已开幕，请于今天10:00前到云帆会展中心报到。"
                };
                int latest = -1;
                for (int index = 0; index < times.Length; index++)
                {
                    string key = "expo-reminder:" + e.Id + ":" + index;
                    if (from < times[index] && times[index] <= to && !Flags.Contains(key))
                    { Flags.Add(key); latest = index; }
                }
                if (latest >= 0) Say("参展提醒 · " + (Brand(e.BrandId)?.Name ?? "品牌") + "：" + messages[latest]);
            }
        }
    }
}
