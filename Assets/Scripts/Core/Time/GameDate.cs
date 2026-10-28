using System;

namespace PCShopSell.Core.Time
{
    public enum GameWeekday
    {
        Monday = 0,
        Tuesday = 1,
        Wednesday = 2,
        Thursday = 3,
        Friday = 4,
        Saturday = 5,
        Sunday = 6,
    }

    /// <summary>
    /// 游戏内日期。简化日历：每月 30 天，每年 12 个月（360 天），每周 7 天。
    /// TotalDays 从第 1 年 1 月 1 日（周一）起算，0 为起点。
    /// </summary>
    public readonly struct GameDate : IEquatable<GameDate>, IComparable<GameDate>
    {
        public const int DaysPerWeek = 7;
        public const int DaysPerMonth = 30;
        public const int MonthsPerYear = 12;
        public const int DaysPerYear = DaysPerMonth * MonthsPerYear;

        public int TotalDays { get; }

        public GameDate(int totalDays)
        {
            if (totalDays < 0)
                throw new ArgumentOutOfRangeException(nameof(totalDays), "日期不能早于起点。");
            TotalDays = totalDays;
        }

        public static GameDate FromYmd(int year, int month, int day)
        {
            if (year < 1) throw new ArgumentOutOfRangeException(nameof(year));
            if (month < 1 || month > MonthsPerYear) throw new ArgumentOutOfRangeException(nameof(month));
            if (day < 1 || day > DaysPerMonth) throw new ArgumentOutOfRangeException(nameof(day));
            return new GameDate((year - 1) * DaysPerYear + (month - 1) * DaysPerMonth + (day - 1));
        }

        public int Year => TotalDays / DaysPerYear + 1;
        public int Month => (TotalDays % DaysPerYear) / DaysPerMonth + 1;
        public int Day => TotalDays % DaysPerMonth + 1;
        public GameWeekday Weekday => (GameWeekday)(TotalDays % DaysPerWeek);
        public bool IsWeekend => Weekday == GameWeekday.Saturday || Weekday == GameWeekday.Sunday;
        public bool IsMonthStart => Day == 1;
        public bool IsMonthEnd => Day == DaysPerMonth;

        public GameDate AddDays(int days) => new GameDate(checked(TotalDays + days));

        /// <summary>到另一个日期还有多少天（other 在之后为正）。</summary>
        public int DaysUntil(GameDate other) => other.TotalDays - TotalDays;

        public static bool operator ==(GameDate a, GameDate b) => a.TotalDays == b.TotalDays;
        public static bool operator !=(GameDate a, GameDate b) => a.TotalDays != b.TotalDays;
        public static bool operator <(GameDate a, GameDate b) => a.TotalDays < b.TotalDays;
        public static bool operator >(GameDate a, GameDate b) => a.TotalDays > b.TotalDays;
        public static bool operator <=(GameDate a, GameDate b) => a.TotalDays <= b.TotalDays;
        public static bool operator >=(GameDate a, GameDate b) => a.TotalDays >= b.TotalDays;

        public bool Equals(GameDate other) => TotalDays == other.TotalDays;
        public override bool Equals(object obj) => obj is GameDate other && Equals(other);
        public override int GetHashCode() => TotalDays;
        public int CompareTo(GameDate other) => TotalDays.CompareTo(other.TotalDays);

        public override string ToString() => $"第{Year}年{Month}月{Day}日";
    }
}
