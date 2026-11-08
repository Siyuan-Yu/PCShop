using System;
using System.Collections.Generic;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    public sealed partial class OwnedProperty
    {
        public int CitySchema;
        public string UseMode; // self, npc, managed, vacant
        public string TenantName;
        public string TenantPersonId;
        public long MonthlyRentCents;
        public long WeeklyRentCents;
        public int CommercialRentFromDay;
        public int LeaseEndDay;
        public int LastSettledMonth = -1;
    }

    public sealed class CityPropertyPurchase
    {
        public string PlaceId, Address, Tenant;
        public Money Price, Fee, DepositCredit, PrepaidCredit, DownPayment, Mortgage, TotalDue;
        public Money CashAfter;
        public bool IsSelfUse, HasTenant;
    }

    [Serializable]
    public sealed class CityEquityState
    {
        public string Id;
        public long PriceCents, Units, CostCents, RealizedCents;
        public long[] History;
        public long PreviousCloseCents, AvailableUnits, ShortUnits, ShortBasisCents, ShortMarginCents, ShortCollateralCents, ShortRealizedCents;
        public long YesterdayRevenueCents, YesterdayProfitCents, QuarterProfitCents, LastDividendCents;
        public double BusinessScale = 1, OperatingMargin;
        public bool Watched;
        public StockQuote[] Quotes = Array.Empty<StockQuote>();
        public string LastReason;
        public CityEquityState Copy() => new CityEquityState { Id = Id, PriceCents = PriceCents, Units = Units, CostCents = CostCents,
            RealizedCents = RealizedCents, History = History == null ? null : (long[])History.Clone(),
            PreviousCloseCents=PreviousCloseCents,AvailableUnits=AvailableUnits,ShortUnits=ShortUnits,ShortBasisCents=ShortBasisCents,
            ShortMarginCents=ShortMarginCents,ShortCollateralCents=ShortCollateralCents,ShortRealizedCents=ShortRealizedCents,
            YesterdayRevenueCents=YesterdayRevenueCents,YesterdayProfitCents=YesterdayProfitCents,QuarterProfitCents=QuarterProfitCents,
            LastDividendCents=LastDividendCents,BusinessScale=BusinessScale,OperatingMargin=OperatingMargin,Watched=Watched,
            Quotes=Array.ConvertAll(Quotes??Array.Empty<StockQuote>(),q=>q.Copy()),LastReason=LastReason };
    }

    public sealed class CityEquityDefinition
    {
        public string Id, Name, Description;
        public int BaseCents;
        public string Industry, RivalKind;
        public double Volatility, Margin;
        public long DailyRevenueYuan, Liquidity;
        public CityEquityDefinition(string id, string name, string description, int cents, string industry,
            long revenue, double margin, double volatility, long liquidity, string rivalKind=null)
        { Id=id;Name=name;Description=description;BaseCents=cents;Industry=industry;DailyRevenueYuan=revenue;Margin=margin;Volatility=volatility;Liquidity=liquidity;RivalKind=rivalKind; }
    }

    public static class CityInvestments
    {
        public static readonly CityEquityDefinition[] All = {
            new CityEquityDefinition("city_chip","城芯算力","算力设备制造与销售，受硬件价格和渠道订单影响。",6800,"硬件",1800000,.08,.024,20000),
            new CityEquityDefinition("city_life","晴川生活","社区连锁零售，消费需求与运营成本共同影响盈利。",4200,"零售",1200000,.07,.014,28000),
            new CityEquityDefinition("city_art","星桥文创","原创商品与展会业务，展期需求活跃，扩张也会增加成本。",5300,"文创",1000000,.09,.022,14000),
            new CityEquityDefinition("rival_pc","兴华电脑城","电脑品牌竞争企业，主营电脑设备与连锁门店。",7600,"硬件",800000,.08,.021,16000,"computer"),
            new CityEquityDefinition("rival_fashion","潮流服饰","服装品牌竞争企业，销售受季节需求与推广成本影响。",3600,"服装",400000,.12,.023,18000,"clothing"),
            new CityEquityDefinition("rival_toys","纸风车潮玩","模型手办品牌竞争企业，依靠新品、展会与代言推广。",5800,"文创",530000,.13,.026,12000,"model"),
            new CityEquityDefinition("city_food","禾味餐饮","经营连锁餐厅，客源增长与食材、人工成本决定利润。",2800,"餐饮",1500000,.06,.016,30000),
            new CityEquityDefinition("city_motor","青禾汽车","生产与销售家用汽车，产品订单与维修成本影响业绩。",8800,"汽车",3200000,.05,.025,10000),
            new CityEquityDefinition("city_logistics","丰运物流","承接城市配送与批发运输，运输需求带动营收。",3200,"物流",2000000,.06,.013,24000),
            new CityEquityDefinition("city_pet","暖爪宠物","宠物用品供应与连锁零售，受居民消费及新品需求影响。",2400,"宠物",850000,.10,.018,22000),
        };
        public static CityEquityDefinition Get(string id) => Array.Find(All, x => x.Id == id);
        public static Money Fee(Money gross, bool hardware = false) => Money.FromCents(Math.Max(1,
            checked((gross.Cents * (hardware ? 5 : 2) + 999) / 1000)));
    }
}
