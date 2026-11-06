using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public enum RaceMeeting { None, Small, Major }
    public static class RacingSchedule
    {
        public static RaceMeeting On(GameDate date) => date.Weekday == GameWeekday.Monday ? RaceMeeting.Major
            : date.TotalDays % 3 == 2 ? RaceMeeting.Small : RaceMeeting.None;
        public static string Name(RaceMeeting meeting) => meeting == RaceMeeting.Major ? "每周大赛" : meeting == RaceMeeting.Small ? "三日小赛" : "今天无比赛";
        public static int Fee(RaceMeeting meeting) => meeting == RaceMeeting.Major ? 200 : meeting == RaceMeeting.Small ? 100 : 0;
        public static int ChampionPrize(RaceMeeting meeting) => meeting == RaceMeeting.Major ? 2000 : meeting == RaceMeeting.Small ? 1000 : 0;
        public static GameDate Next(GameDate date)
        { for (int i = 1; i <= 7; i++) if (On(date.AddDays(i)) != RaceMeeting.None) return date.AddDays(i); return date.AddDays(7); }
    }
}
