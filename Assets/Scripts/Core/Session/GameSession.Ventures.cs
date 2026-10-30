using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly List<ManagedVenture> _ventures = new List<ManagedVenture>();
        private readonly List<OwnedProperty> _properties = new List<OwnedProperty>();
        private int _lastBusinessDay = -1;
        public IReadOnlyList<ManagedVenture> ManagedVentures => _ventures;
        public IReadOnlyList<OwnedProperty> OwnedProperties => _properties;
        public ManagedVenture VentureAt(string id) => _ventures.Find(x => x.PlaceId == id);
        public OwnedProperty PropertyAt(string id) => _properties.Find(x => x.PlaceId == id);
        public Money VentureReserve => Money.FromCents(_ventures.Sum(x => x.ReserveCents));
        public Money VentureValue => Money.FromCents(_ventures.Sum(x => x.ReserveCents + x.DepositCents + (long)(x.CapitalCents * .3 * x.Condition)));
        public Money PropertyValue => Money.FromCents(_properties.Sum(x => x.PriceCents));
        public Money MortgageDebt => Money.FromCents(_properties.Sum(x => x.MortgageCents) + HomeMortgageCents);
        public Money VentureOpeningCost(ShopListing listing, VentureDef def) => Money.FromYuan(def.Capital + 5000) + listing.Rent + RealEstate.Deposit(listing.Rent) + AgentFeeFor(listing.Rent);
        public string VentureBlocker(string placeId, string kind)
        {
            var d = Ventures.Get(kind); var l = _listings.Find(x => x.PlaceId == placeId);
            if (!HasShop) return "先把主店开起来";
            if (d == null || l == null || l.OpenDay > Clock.Today.TotalDays || l.CloseDay < Clock.Today.TotalDays) return "请选择当前可签约的空铺";
            if (IsMyShop(City.GetPlace(placeId)) || VentureAt(placeId) != null) return "这间铺子已在经营";
            if (_ventures.Count(x => !IsParkVenture(x)) >= 6) return "最多管理 6 家委托经营的门店";
            if (PlayerShop.Reputation < d.MinReputation) return $"知名度需要 {d.MinReputation:0}";
            if ((kind == "esports" || kind == "cars" || kind == "stay") && l.Size == ShopSize.Small) return "该业态需要中型或大型铺面";
            if (d.HardwareCount > 0 && PartCatalog.BuildList(d.HardwareTier).Any(p => PlayerShop.Stock.CountAll(p) < d.HardwareCount)) return $"主店需备齐 {d.HardwareCount} 套{PartCatalog.TierName(d.HardwareTier)}散件";
            if (Ledger.Balance < VentureOpeningCost(l, d)) return "现金不足（含装修、首月租金、押金、中介费和 5,000 周转金）";
            return null;
        }
        public bool OpenVenture(string placeId, string kind)
        {
            if (IsGameOver || !InAgency) return Fail("请到房产中介签分店合同。");
            string why = VentureBlocker(placeId, kind); if (why != null) return Fail(why);
            var l = _listings.Find(x => x.PlaceId == placeId); var d = Ventures.Get(kind);
            Money capital = Money.FromYuan(d.Capital);
            if (d.HardwareCount > 0) foreach (var p in PartCatalog.BuildList(d.HardwareTier))
            {
                capital += Market.PriceOf(p) * d.HardwareCount;
                for (int i=0; i<d.HardwareCount; i++) PlayerShop.Stock.TakeOne(p);
            }
            Ledger.ForceDebit(Clock.Today, "企业投资", VentureOpeningCost(l, d), d.Name + "开办及周转金");
            _ventures.Add(new ManagedVenture { PlaceId = placeId, Kind = kind, OpenDay = Clock.Today.TotalDays, LastSettledDay = Clock.Today.TotalDays,
                CapitalCents = capital.Cents, DepositCents = RealEstate.Deposit(l.Rent).Cents, RentCents = l.Rent.Cents, ReserveCents = 500000 });
            _vacant.Remove(placeId); _listings.Remove(l);
            Flags.Add("tutorial:branch");
            Say(d.Name + "已开业，店长负责日常运营。明日起结算；经营总览可调整定价、补充周转金、暂停或退出。");
            return true;
        }
        public bool TransferVentureCash(string id, long yuan, bool withdraw)
        {
            var v = VentureAt(id); if (v == null || yuan <= 0 || yuan > 1000000 || IsGameOver) return false;
            Money amount = Money.FromYuan(yuan);
            if (withdraw)
            {
                if (v.ReserveCents < amount.Cents) return Fail("门店周转金不足。");
                v.ReserveCents -= amount.Cents;
                Ledger.Credit(Clock.Today, "企业资金转回", amount, Ventures.Get(v.Kind).Name);
            }
            else
            {
                if (!Ledger.TryDebit(Clock.Today, "企业周转金", amount, Ventures.Get(v.Kind).Name)) return Fail("现金不足。");
                v.ReserveCents += amount.Cents;
            }
            return true;
        }
        public bool SetVenturePrice(string id, double price)
        {
            var v = VentureAt(id); if (v == null || IsGameOver || double.IsNaN(price) || price < .8 || price > 1.3) return false;
            v.PriceFactor = Math.Round(price, 2); return true;
        }
        public bool ToggleVenture(string id)
        {
            var v=VentureAt(id); if(v==null || IsGameOver) return false;
            v.Paused=!v.Paused; return true;
        }
        public Money VentureRepairCost(ManagedVenture v) => Money.FromYuan(800) + Money.FromCents(v.CapitalCents).Scale(.02);
        public bool MaintainVenture(string id)
        {
            var v=VentureAt(id); if (v==null || v.Condition >= .999 || IsGameOver) return false;
            if(!Ledger.TryDebit(Clock.Today, "企业维护", VentureRepairCost(v), Ventures.Get(v.Kind).Name)) return Fail("维护现金不足。");
            v.Condition=1; return true;
        }
        public Money VentureExitValue(ManagedVenture v) => Money.FromCents(v.ReserveCents + v.DepositCents + (long)(v.CapitalCents * .3 * v.Condition));
        public bool CloseVenture(string id)
        {
            var v=VentureAt(id); if(v==null || IsGameOver) return false;
            Money refund=VentureExitValue(v);
            if(refund.IsPositive) Ledger.Credit(Clock.Today,"企业清算",refund,Ventures.Get(v.Kind).Name);
            else if(refund.IsNegative) Ledger.ForceDebit(Clock.Today,"企业清算",-refund,Ventures.Get(v.Kind).Name);
            _ventures.Remove(v);
            if (IsParkVenture(v)) { Say("园区铺位已清退，可重新招入业态。"); return true; }
            _vacant.Add(id);
            // 自有物业保留，不会因为关闭门店而消失。
            if (PropertyAt(id)==null) AddListing(City.GetPlace(id),Clock.Today.TotalDays,Clock.Today.TotalDays+45,Money.FromCents(v.RentCents));
            Say($"门店已关闭，清算 {refund}（周转金、押金、设备装修残值 30% × 状态）。");
            return true;
        }
        private void SettleVentures(GameDate today)
        {
            int day=today.TotalDays;
            foreach(var v in _ventures)
            {
                if(day<=v.LastSettledDay) continue;
                v.LastSettledDay=day;
                var d=Ventures.Get(v.Kind);
                // 首期租金已付；之后逐日计提。停业仍有租金及 20% 留任工资。
                long rent=(day-v.OpenDay<=30 || PropertyAt(v.PlaceId)!=null || IsParkVenture(v)) ? 0 : v.RentCents/30;
                long wages=d.DailyWages*100;
                double location=LocationFactorOf(VentureLocation(v));
                bool weekend=day%7==5 || day%7==6;
                double seasonal=d.Id=="flowers" && day%30==13 ? 1.8 : weekend && d.Id!="branch" && d.Id!="cars" ? 1.25 : 1;
                double priceDemand=Math.Exp(-(v.PriceFactor-1)*(d.Id=="esports" ? 3 : 2));
                double demand=Math.Min(1.6,(.55+location*.65+Math.Min(.25,PlayerShop.Reputation/200))*seasonal*priceDemand*v.Condition*ParkTrafficBonus(v));
                double hardware=Market.PriceOf(PartCatalog.Get("cpu1")).Cents/(double)PartCatalog.Get("cpu1").BasePrice.Cents;
                double costRatio=Math.Min(.94,d.VariableCost*(d.Id=="branch" ? Math.Max(.8,Math.Min(1.3,hardware)) : 1));
                long sales=(long)(d.DailySales*100*demand*v.PriceFactor);
                long cost=rent+wages+(long)(sales*costRatio);
                if(v.Paused || v.ReserveCents<cost)
                {
                    if(!v.Paused) Say(Ventures.Get(v.Kind).Name+"周转金不足，店长已暂停营业；补资后手动恢复。");
                    v.Paused=true; sales=0; cost=rent+wages/5; demand=0;
                    // 欠的固定费用由总部承担，子账户不无限挂负数。
                    long shortfall=Math.Max(0,cost-v.ReserveCents);
                    if(shortfall>0) { Ledger.ForceDebit(today,"企业固定费",Money.FromCents(shortfall),d.Name+"停业费用"); v.ReserveCents+=shortfall; }
                }
                else v.Condition=Math.Max(.4,v.Condition-(d.HardwareCount>0 ? .003 : .0015));
                v.ReserveCents+=sales-cost;
                v.TotalOperatingCents+=sales-cost;
                v.LastSalesCents=sales; v.LastCostsCents=cost; v.LastDemand=demand;
            }
        }
        public Money PropertyQuote(string id)
        {
            Money rent=ShopAt(id)!=null ? ShopAt(id).Lease.Rent : VentureAt(id)!=null ? Money.FromCents(VentureAt(id).RentCents) : _listings.Find(x=>x.PlaceId==id)?.Rent ?? Money.Zero;
            return rent*120;
        }
        public string PropertyBlocker(string id,bool mortgage)
        {
            if(PropertyAt(id)!=null) return "已经拥有这处物业";
            if(!City.HasPlace(id) || (!IsMyShop(City.GetPlace(id)) && VentureAt(id)==null)) return "先在这处物业开设主店或分店";
            if(PlayerShop.Reputation<20) return "买物业需要知名度 20";
            if(Ledger.Balance<PropertyQuote(id).Scale(mortgage ? .3 : 1)) return "现金不足";
            return null;
        }
        public bool BuyProperty(string id,bool mortgage)
        {
            if(IsGameOver || !InAgency) return Fail("请在房产中介办理买卖。");
            string why=PropertyBlocker(id,mortgage); if(why!=null) return Fail(why);
            Money price=PropertyQuote(id), paid=price.Scale(mortgage ? .3 : 1), debt=price-paid;
            Ledger.ForceDebit(Clock.Today,"购置物业",paid,PlaceTitle(City.GetPlace(id)));
            _properties.Add(new OwnedProperty {PlaceId=id,PriceCents=price.Cents,MortgageCents=debt.Cents,MonthlyPrincipalCents=(long)Math.Ceiling(debt.Cents/36.0),BoughtDay=Clock.Today.TotalDays});
            OwnedShop own=ShopAt(id);
            long deposit=own!=null ? own.Lease.Deposit.Cents : VentureAt(id).DepositCents;
            if(deposit>0) Ledger.Credit(Clock.Today,"押金",Money.FromCents(deposit),"购房退租赁押金");
            if(own!=null) own.Lease.Deposit=Money.Zero; else VentureAt(id).DepositCents=0;
            Say("物业已购入：免付店租，每月维护费房价 0.1%。"+(mortgage ? "贷款 36 期等额本金，月利率 0.4%，可随时提前还清。" : ""));
            return true;
        }
        public bool RepayMortgage(string id)
        {
            var p=PropertyAt(id); if(p==null || p.MortgageCents<=0 || IsGameOver) return false;
            if(!Ledger.TryDebit(Clock.Today,"偿还房贷",Money.FromCents(p.MortgageCents),"提前结清")) return Fail("现金不足。");
            p.MortgageCents=0; return true;
        }
        public Money PropertySaleNet(OwnedProperty p) => Money.FromCents(p.PriceCents).Scale(.9)-Money.FromCents(p.MortgageCents);
        public bool SellProperty(string id)
        {
            var p=PropertyAt(id); if(p==null || !InAgency || IsGameOver) return false;
            if (id==ParkPlaceId && _ventures.Any(IsParkVenture)) return Fail("先清退园区所有铺位，再出售园区物业。");
            Money net=PropertySaleNet(p);
            if(Ledger.Balance+net<Money.Zero) return Fail("出售净额不足清偿贷款。");
            if(net.IsPositive) Ledger.Credit(Clock.Today,"物业出售",net,"售价九折，先清偿贷款");
            else if(net.IsNegative) Ledger.ForceDebit(Clock.Today,"物业出售",-net,"清偿贷款差额");
            _properties.Remove(p);
            if (id==ParkPlaceId) { ParkPlaceId=null; ParkReadyDay=0; }
            if(VentureAt(id)==null && ShopAt(id)==null) AddListing(City.GetPlace(id),Clock.Today.TotalDays,Clock.Today.TotalDays+45);
            Say("物业出售完成；仍在经营的店铺恢复租赁计费。"); return true;
        }
        private void SettleProperties(GameDate today)
        {
            foreach(var p in _properties)
            {
                bool self=ShopAt(p.PlaceId)!=null || VentureAt(p.PlaceId)!=null || p.PlaceId==ParkPlaceId;
                // 关闭自己的门店后由中介托管出租，15% 已包含空置与管理成本。
                if(!self && today.TotalDays>p.BoughtDay)
                    Ledger.Credit(today,"物业租金",Money.FromCents(p.PriceCents).Scale(.85/120/30),"物业托管出租");
                if(!today.IsMonthStart || today.TotalDays<=p.BoughtDay) continue;
                Ledger.ForceDebit(today,"物业维护",Money.FromCents(p.PriceCents).Scale(.001),"物业月维护");
                if(p.MortgageCents>0)
                {
                    long principal=Math.Min(p.MortgageCents,p.MonthlyPrincipalCents);
                    Money interest=Money.FromCents(p.MortgageCents).Scale(.004);
                    Ledger.ForceDebit(today,"偿还房贷",Money.FromCents(principal),"物业月供本金");
                    if(interest.IsPositive) Ledger.ForceDebit(today,"房贷利息",interest,"物业月供利息");
                    p.MortgageCents-=principal;
                }
            }
        }
    }
}
