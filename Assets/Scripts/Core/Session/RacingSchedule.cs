using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public enum RaceMeeting { None, Small, Major }
    public static class RacingSchedule
    {
        public const int PracticeFee = 100;
        public const int MeetOpenMinute = 9 * 60;
        public const int MeetCloseMinute = 18 * 60;
        public const int DriverDepartureMinute = 6 * 60;
        public static RaceMeeting On(GameDate date) => date.Weekday == GameWeekday.Monday ? RaceMeeting.Major
            : date.TotalDays % 3 == 2 ? RaceMeeting.Small : RaceMeeting.None;
        public static string Name(RaceMeeting meeting) => meeting == RaceMeeting.Major ? "每周大赛" : meeting == RaceMeeting.Small ? "三日小赛" : "今天无比赛";
        public static int Fee(RaceMeeting meeting) => meeting == RaceMeeting.Major ? 2000 : meeting == RaceMeeting.Small ? 1000 : 0;
        public static int ChampionPrize(RaceMeeting meeting) => meeting == RaceMeeting.Major ? 20000 : meeting == RaceMeeting.Small ? 10000 : 0;
        public static GameDate Next(GameDate date)
        { for (int i = 1; i <= 7; i++) if (On(date.AddDays(i)) != RaceMeeting.None) return date.AddDays(i); return date.AddDays(7); }
    }
}
