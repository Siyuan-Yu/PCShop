using System;
using System.Globalization;

namespace PCShopSell.Core.Time
{
    /// <summary>
    /// 游戏内时刻：日期 + 当天第几分钟。以"自起点以来的总分钟数"存储，便于比较和计算。
    /// </summary>
    public readonly struct GameTimestamp : IEquatable<GameTimestamp>, IComparable<GameTimestamp>
    {
        public const int MinutesPerDay = 1440;

        public long TotalMinutes { get; }

        public GameTimestamp(long totalMinutes)
        {
            if (totalMinutes < 0)
                throw new ArgumentOutOfRangeException(nameof(totalMinutes), "时刻不能早于起点。");
            TotalMinutes = totalMinutes;
        }

        public static GameTimestamp From(GameDate date, int minuteOfDay)
        {
            if (minuteOfDay < 0 || minuteOfDay >= MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(minuteOfDay));
            return new GameTimestamp((long)date.TotalDays * MinutesPerDay + minuteOfDay);
        }

        public GameDate Date => new GameDate((int)(TotalMinutes / MinutesPerDay));
        public int MinuteOfDay => (int)(TotalMinutes % MinutesPerDay);
        public int Hour => MinuteOfDay / 60;
        public int Minute => MinuteOfDay % 60;

        public GameTimestamp AddMinutes(long minutes) => new GameTimestamp(checked(TotalMinutes + minutes));

        /// <summary>到另一个时刻还有多少分钟（other 在之后为正）。</summary>
        public long MinutesUntil(GameTimestamp other) => other.TotalMinutes - TotalMinutes;

        public static bool operator ==(GameTimestamp a, GameTimestamp b) => a.TotalMinutes == b.TotalMinutes;
        public static bool operator !=(GameTimestamp a, GameTimestamp b) => a.TotalMinutes != b.TotalMinutes;
        public static bool operator <(GameTimestamp a, GameTimestamp b) => a.TotalMinutes < b.TotalMinutes;
        public static bool operator >(GameTimestamp a, GameTimestamp b) => a.TotalMinutes > b.TotalMinutes;
        public static bool operator <=(GameTimestamp a, GameTimestamp b) => a.TotalMinutes <= b.TotalMinutes;
        public static bool operator >=(GameTimestamp a, GameTimestamp b) => a.TotalMinutes >= b.TotalMinutes;

        public bool Equals(GameTimestamp other) => TotalMinutes == other.TotalMinutes;
        public override bool Equals(object obj) => obj is GameTimestamp other && Equals(other);
        public override int GetHashCode() => TotalMinutes.GetHashCode();
        public int CompareTo(GameTimestamp other) => TotalMinutes.CompareTo(other.TotalMinutes);

        public override string ToString()
        {
            return Date + " " + Hour.ToString("00", CultureInfo.InvariantCulture) + ":" +
                   Minute.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
