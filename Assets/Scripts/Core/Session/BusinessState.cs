using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class SupplierDebt
    {
        public int Id, DueDay, LastFeeDay;
        public long Principal, Fees;
        public Money Amount => Money.FromCents(Principal + Fees);
        public SupplierDebt Copy() => (SupplierDebt)MemberwiseClone();
    }
    [Serializable] public sealed class DirectShipment
    {
        public string PartId;
        public int Count;
        public long Arrival;
        public DirectShipment Copy() => (DirectShipment)MemberwiseClone();
    }
    [Serializable] public sealed class EnterpriseContract
    {
        public string Id, Client;
        public int Tier, Count, Completed, Failed, OfferUntilDay;
        public long UnitCents, DueMinute, DepositCents;
        public bool Accepted, Settled;
        public Money Total => Money.FromCents(UnitCents) * Count;
        public EnterpriseContract Copy() => (EnterpriseContract)MemberwiseClone();
    }
    public sealed class VentureDef
    {
        public string Id, Name, Description;
        public long Capital, DailyWages, DailySales;
        public double VariableCost, MinReputation;
        public int HardwareTier = -1, HardwareCount;
        public VentureDef(string id, string name, string description, long capital, long wages, long sales, double costs, double rep, int tier = -1, int machines = 0)
        { Id=id; Name=name; Description=description; Capital=capital; DailyWages=wages; DailySales=sales; VariableCost=costs; MinReputation=rep; HardwareTier=tier; HardwareCount=machines; }
    }
    public static class Ventures
    {
        public static readonly VentureDef[] All = {
            new VentureDef("branch", "电脑分店", "店长负责进货与零售；进货成本随硬件行情浮动。", 28000, 240, 2100, .68, 12),
            new VentureDef("cafe", "社区网吧", "需从主店库存调拨 6 套主流配置；周末客流较好，设备状态影响上座率。", 16000, 220, 1200, .22, 15, 1, 6),
            new VentureDef("esports", "电竞馆", "需调拨 8 套高端配置；高收费带来较高的价格敏感度。", 40000, 420, 2400, .26, 28, 2, 8),
            new VentureDef("food", "街角简餐店", "店长每日采购；食材成本占营业额 45%，周末生意更好。", 22000, 260, 1400, .45, 10),
            new VentureDef("flowers", "花店", "损耗计入采购成本；每月 14 日有鲜花消费高峰。", 14000, 180, 950, .48, 8),
            new VentureDef("stay", "小型民宿", "保洁和前台由店长管理；周末入住较多，维护影响入住率。", 65000, 280, 1500, .24, 22),
            new VentureDef("cars", "二手车行", "委托店长收车和整备；周转额大、毛利薄，需预留充足周转现金。", 120000, 460, 8000, .86, 35),
        };
        public static VentureDef Get(string id) => All.FirstOrDefault(x => x.Id == id);
    }
    [Serializable] public sealed class ManagedVenture
    {
        public string PlaceId, Kind;
        public int OpenDay, LastSettledDay;
        public long CapitalCents, DepositCents, RentCents, ReserveCents, TotalOperatingCents, LastSalesCents, LastCostsCents;
        public double Condition = 1, PriceFactor = 1, LastDemand;
        public bool Paused;
        public ManagedVenture Copy() => (ManagedVenture)MemberwiseClone();
    }
    [Serializable] public sealed class OwnedProperty
    {
        public string PlaceId;
        public long PriceCents, MortgageCents, MonthlyPrincipalCents;
        public int BoughtDay;
        public OwnedProperty Copy() => (OwnedProperty)MemberwiseClone();
    }
    [Serializable] public sealed class CreatorState
    {
        public string Id;
        public int Followers, Relationship, Ambition, Resources, LastSupportDay = -999, CampaignUntilDay;
        public long AttributedSalesCents, LastSalesCents;
        public int LastLost, PoorDays, McnUntilDay, Affection, StoryStage;
        public int LastOutingDay = -99;
        public long McnIncomeCents;
        public CreatorState Copy() => (CreatorState)MemberwiseClone();
    }
    public sealed class CreatorDef
    {
        public string Id, Name, Style;
        public int Audience, Ambition;
        public long Fee;
        public CreatorDef(string id,string name,string style,int audience,int ambition,long fee)
        { Id=id; Name=name; Style=style; Audience=audience; Ambition=ambition; Fee=fee; }
    }
    public static class Creators
    {
        public static readonly CreatorDef[] All = {
            new CreatorDef("momo", "莫莫", "实用装机教程 · 耐心积累口碑", 4000, 30, 600),
            new CreatorDef("ake", "阿可", "电竞直播 · 看重设备与成长", 18000, 65, 1800),
            new CreatorDef("qiao", "乔乔", "数码评测 · 需要稳定供货与服务", 60000, 85, 5000),
        };
        public static CreatorDef Get(string id) => All.FirstOrDefault(x => x.Id == id);
    }
    [Serializable] public sealed class BusinessSave
    {
        public SupplierDebt[] Debts;
        public DirectShipment[] Shipments;
        public EnterpriseContract[] Contracts;
        public ManagedVenture[] Ventures;
        public OwnedProperty[] Properties;
        public CreatorState[] Creators;
        public int NextDebtId = 1, LastOfferWeek = -1, LastBusinessDay = -1, HomeTier;
        public long FundUnits, HomePriceCents, HomeMortgageCents, HomeMonthlyPrincipalCents;
        public bool DirectAgreement;
        public string ParkPlaceId, RomanceId;
        public bool HasMcn;
        public int ParkReadyDay;
    }
}
