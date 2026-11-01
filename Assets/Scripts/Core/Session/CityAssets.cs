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
        public int LeaseEndDay;
        public int LastSettledMonth = -1;
    }

    public sealed class CityPropertyPurchase
    {
        public string PlaceId, Address, Tenant;
        public Money Price, Fee, DepositCredit, PrepaidCredit, DownPayment, Mortgage, TotalDue;
        public Money CashAfter;
        public bool IsSelfUse;
    }

    [Serializable]
    public sealed class CityEquityState
    {
        public string Id;
        public long PriceCents, Units, CostCents, RealizedCents;
        public long[] History;
        public CityEquityState Copy() => new CityEquityState { Id = Id, PriceCents = PriceCents, Units = Units, CostCents = CostCents,
            RealizedCents = RealizedCents, History = History == null ? null : (long[])History.Clone() };
    }

    public sealed class CityEquityDefinition
    {
        public string Id, Name, Description;
        public int BaseCents;
        public CityEquityDefinition(string id, string name, string description, int cents)
        { Id = id; Name = name; Description = description; BaseCents = cents; }
    }

    public static class CityInvestments
    {
        public static readonly CityEquityDefinition[] All = {
            new CityEquityDefinition("city_chip", "城芯算力", "虚构算力设备企业，跟随硬件产业景气；波动较高。", 6800),
            new CityEquityDefinition("city_life", "晴川生活", "虚构社区零售企业，受本地消费与季节消息影响。", 4200),
            new CityEquityDefinition("city_art", "星桥文创", "虚构原创模型与展会企业，展期活跃但存在回落风险。", 5300),
        };
        public static CityEquityDefinition Get(string id) => Array.Find(All, x => x.Id == id);
        public static Money Fee(Money gross, bool hardware = false) => Money.FromCents(Math.Max(1,
            checked((gross.Cents * (hardware ? 5 : 2) + 999) / 1000)));
    }
}
