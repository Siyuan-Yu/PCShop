using System;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Shop
{
    /// <summary>
    /// 房产中介挂出来的一间铺面。OpenDay 之前现租户还没搬走：可以提前签，但要到 OpenDay 才能搬进去，租金从签约起算；
    /// 过了 CloseDay 还没人签就被别人租走了。
    /// </summary>
    public sealed class ShopListing
    {
        public string PlaceId;
        public ShopSize Size;
        public Money Rent;
        public int OpenDay;
        public int CloseDay;
    }

    /// <summary>玩家正在租的铺子。押金退租 / 换铺时退还。提前签下的铺子 StartDay 是交房（能搬进去）那天。</summary>
    public sealed class ShopLease
    {
        public string PlaceId;
        public Money Rent;
        public Money Deposit;
        public int StartDay;
        public int SignedDay;
    }

    /// <summary>
    /// 租铺子的价钱：月租按大小和位置（离地铁站越近越贵）定，中介再上下浮动一点。
    /// 签约一次付清：首月租金 + 押金（一个月）+ 中介费（一个月）。
    /// </summary>
    public static class RealEstate
    {
        /// <summary>业态写成这个的铺子本来就空着（地图上的“街尾空铺”）。</summary>
        public const string VacantBusiness = "空铺";

        public static readonly Money SmallBaseRent = Money.FromYuan(1500);
        public static readonly Money MediumBaseRent = Money.FromYuan(4500);
        public static readonly Money LargeBaseRent = Money.FromYuan(12000);

        /// <summary>离地铁站最近的铺子比最远的贵多少（位置系数 0.4→1.0 对应月租 ×1→×(1+这个数)）。</summary>
        public const double BestLocationPremium = 1.5;

        /// <summary>已经开着店再签一间：搬货、搬家具的搬家费。</summary>
        public static readonly Money MoveFee = Money.FromYuan(800);

        public static Money BaseRent(ShopSize size) =>
            size == ShopSize.Large ? LargeBaseRent : size == ShopSize.Medium ? MediumBaseRent : SmallBaseRent;

        /// <summary>月租：基础价 × 位置 × 中介浮动，取整到百元。</summary>
        public static Money RentFor(ShopSize size, double locationFactor, double jitter)
        {
            double loc = 1.0 + BestLocationPremium * Math.Max(0, Math.Min(1, (locationFactor - 0.4) / 0.6));
            double yuan = BaseRent(size).Yuan * loc * jitter;
            return Money.FromYuan((long)Math.Round(yuan / 100.0) * 100);
        }

        public static Money Deposit(Money rent) => rent;

        /// <summary>中介费标价：一个月房租。</summary>
        public static Money AgentFee(Money rent) => rent;

        /// <summary>和中介小陈熟络度每点能打的折扣（0.5%），封顶 <see cref="MaxAgentDiscount"/>。</summary>
        public const double AgentDiscountPerRapport = 0.005;

        public const double MaxAgentDiscount = 0.5;

        public static double AgentDiscount(int rapport) => Math.Max(0, Math.Min(MaxAgentDiscount, rapport * AgentDiscountPerRapport));

        /// <summary>按熟络度打折后的中介费，取整到元。</summary>
        public static Money AgentFeeAfter(Money rent, int rapport) => Money.FromYuan((long)Math.Round(AgentFee(rent).Yuan * (1.0 - AgentDiscount(rapport))));

        /// <summary>签约当场要付的钱（首月 + 押金 + 中介费，中介费按标价；真实价格要看和中介的熟络度）。</summary>
        public static Money SignCost(Money rent) => rent + Deposit(rent) + AgentFee(rent);

        /// <summary>房东要看的知名度（大铺子不租给没名气的）。</summary>
        public static double MinReputation(ShopSize size) => size == ShopSize.Large ? 60 : size == ShopSize.Medium ? 25 : 0;

        public static string SizeArea(ShopSize size) => size == ShopSize.Large ? "216㎡" : size == ShopSize.Medium ? "108㎡" : "48㎡";
    }
}
