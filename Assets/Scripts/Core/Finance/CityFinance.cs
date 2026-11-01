using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Finance
{
    public sealed class CityCashReserve
    {
        public int Days;
        public GameDate From, To;
        public Money KnownMandatory, EstimatedDaily, ExtraProtection, Balance;
        public Money Total => KnownMandatory + EstimatedDaily + ExtraProtection;
        public Money Available => Balance - Total;
    }
    public sealed class CityFinanceSummary
    {
        public GameDate From, To;
        public string ShopId;
        public Money OperatingIn, OperatingOut, PersonalNet, InvestmentNet, FinancingNet, InternalNet, HistoricalNet;
        public Money Sales, UnassignedOperatingIn, UnassignedOperatingOut;
        public Money OperatingNet => OperatingIn - OperatingOut;
    }
}
