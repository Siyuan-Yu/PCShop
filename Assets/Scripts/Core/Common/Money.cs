using System;
using System.Globalization;

namespace PCShopSell.Core.Common
{
    /// <summary>
    /// 金额，内部以"分"为单位的整数存储，避免浮点误差。可为负（负债/支出）。
    /// </summary>
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        public static readonly Money Zero = new Money(0);

        public long Cents { get; }

        private Money(long cents)
        {
            Cents = cents;
        }

        public static Money FromCents(long cents) => new Money(cents);

        public static Money FromYuan(long yuan) => new Money(checked(yuan * 100));

        /// <summary>整数元部分（向零取整）。</summary>
        public long Yuan => Cents / 100;

        public bool IsZero => Cents == 0;
        public bool IsNegative => Cents < 0;
        public bool IsPositive => Cents > 0;

        public Money Abs() => Cents < 0 ? new Money(-Cents) : this;

        public static Money operator +(Money a, Money b) => new Money(checked(a.Cents + b.Cents));
        public static Money operator -(Money a, Money b) => new Money(checked(a.Cents - b.Cents));
        public static Money operator -(Money a) => new Money(-a.Cents);
        public static Money operator *(Money a, long factor) => new Money(checked(a.Cents * factor));
        public static Money operator *(long factor, Money a) => a * factor;

        /// <summary>按比例缩放（四舍五入到分）。适用于折扣、分成、税率等。</summary>
        public Money Scale(double ratio)
        {
            return new Money((long)Math.Round(Cents * ratio, MidpointRounding.AwayFromZero));
        }

        public static bool operator ==(Money a, Money b) => a.Cents == b.Cents;
        public static bool operator !=(Money a, Money b) => a.Cents != b.Cents;
        public static bool operator <(Money a, Money b) => a.Cents < b.Cents;
        public static bool operator >(Money a, Money b) => a.Cents > b.Cents;
        public static bool operator <=(Money a, Money b) => a.Cents <= b.Cents;
        public static bool operator >=(Money a, Money b) => a.Cents >= b.Cents;

        public bool Equals(Money other) => Cents == other.Cents;
        public override bool Equals(object obj) => obj is Money other && Equals(other);
        public override int GetHashCode() => Cents.GetHashCode();
        public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

        public static Money Min(Money a, Money b) => a <= b ? a : b;
        public static Money Max(Money a, Money b) => a >= b ? a : b;

        public override string ToString()
        {
            long abs = Cents < 0 ? -Cents : Cents;
            long yuan = abs / 100;
            long fen = abs % 100;
            string sign = Cents < 0 ? "-" : string.Empty;
            string yuanText = yuan.ToString("N0", CultureInfo.InvariantCulture);
            return fen == 0
                ? sign + "¥" + yuanText
                : sign + "¥" + yuanText + "." + fen.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
