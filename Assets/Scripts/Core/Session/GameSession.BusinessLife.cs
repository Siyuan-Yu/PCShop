using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly List<CreatorState> _creators = Creators.All.Select(x=>new CreatorState {Id=x.Id,Followers=x.Audience,Ambition=x.Ambition,Resources=40}).ToList();
        public IReadOnlyList<CreatorState> CreatorPartners => _creators;
        public CreatorState Creator(string id) => _creators.Find(x=>x.Id==id);
        public double CreatorTrafficMultiplier => 1 + _creators.Where(x=>x.CampaignUntilDay>Clock.Today.TotalDays).Sum(x=>Math.Min(.45,.08+x.Followers/200000.0)*(0.6+x.Resources/250.0));
        public Money CreatorFee(CreatorState c) => Money.FromYuan(Creators.Get(c.Id).Fee).Scale((.7+c.Followers/(double)Creators.Get(c.Id).Audience*.3)*(1-c.Relationship*.002));
        public string CreatorCampaignBlocker(CreatorState c)
        {
            if(!HasShop || !HasSmartphone) return "需要主店和智能手机";
            if(PlayerShop.Reputation<8) return "知名度达到 8";
            if(_creators.Any(x=>x.CampaignUntilDay>Clock.Today.TotalDays)) return "已有推广进行中，结束后再合作";
            if(c.Resources<20) return "创作资源不足，先赞助一次设备";
            if(Ledger.Balance<CreatorFee(c)) return "推广费用不足";
            return null;
        }
        public bool StartCreatorCampaign(string id)
        {
            var c=Creator(id); if(c==null || IsGameOver) return false;
            string why=CreatorCampaignBlocker(c); if(why!=null) return Fail(why);
            Ledger.ForceDebit(Clock.Today,"推广",CreatorFee(c),Creators.Get(id).Name+"七日合作");
            c.CampaignUntilDay=Clock.Today.TotalDays+7; c.LastSalesCents=RetailAndBuildRevenueToday(); c.LastLost=PlayerShop.TodayLost; c.AttributedSalesCents=0; c.PoorDays=0;
            Flags.Add("tutorial:creator");
            ApplyUpgrades(); Say("七日推广开始：客流增加，资源与服务表现会影响后续合作。经营总览记录期间营业额（包含自然客流，不等同推广净增收益）。"); return true;
        }
        public bool SupportCreator(string id)
        {
            var c=Creator(id); if(c==null || !HasSmartphone || IsGameOver) return false;
            if(c.LastSupportDay==Clock.Today.TotalDays) return Fail("今天已经支持过这位创作者。");
            if(!Ledger.TryDebit(Clock.Today,"推广",Money.FromYuan(500),Creators.Get(id).Name+"设备与制作赞助")) return Fail("赞助需要 ¥500。");
            c.LastSupportDay=Clock.Today.TotalDays; c.Resources=Math.Min(100,c.Resources+30); c.Relationship=Math.Min(100,c.Relationship+5); return true;
        }
        private long RetailAndBuildRevenueToday() => Ledger.Entries.Where(x=>x.Date==Clock.Today && x.Amount.IsPositive && (x.Category=="零售" || x.Category=="装机")).Sum(x=>x.Amount.Cents);
        private void TrackCreatorCampaigns(GameTimestamp now)
        {
            foreach(var c in _creators)
            {
                if(c.CampaignUntilDay<=now.Date.TotalDays) continue;
                long sales=RetailAndBuildRevenueToday();
                if(sales>c.LastSalesCents) c.AttributedSalesCents+=sales-c.LastSalesCents;
                c.LastSalesCents=sales;
            }
        }
        private void SettleCreators(GameDate previous,GameDate today)
        {
            foreach(var c in _creators)
            {
                if(c.CampaignUntilDay>previous.TotalDays)
                {
                    // 从账本读取昨日，避免 ShopSimulation 午夜重置 TodaySales 导致漏记。
                    long sales=Ledger.Entries.Where(x=>x.Date==previous && x.Amount.IsPositive && (x.Category=="零售" || x.Category=="装机")).Sum(x=>x.Amount.Cents);
                    c.AttributedSalesCents+=Math.Max(0,sales-c.LastSalesCents);
                    c.LastSalesCents=0;
                    bool good=sales>=300000;
                    c.PoorDays=good ? 0 : c.PoorDays+1;
                    c.Relationship=Math.Max(0,Math.Min(100,c.Relationship+(good ? 2 : -2)));
                    c.Resources=Math.Max(0,c.Resources-4);
                    double growth=good ? .008+c.Resources*.00008 : -.004;
                    c.Followers=Math.Max(500,Math.Min(2000000,(int)(c.Followers*(1+growth))));
                    if(c.PoorDays>=3 && c.Relationship==0 && c.Ambition>=80 && today.TotalDays+2<c.CampaignUntilDay)
                    { c.CampaignUntilDay=today.TotalDays; Say(Creators.Get(c.Id).Name+"认为合作销售表现不足，提前结束推广（费用不退）。"); }
                    if(c.CampaignUntilDay==today.TotalDays) Say(Creators.Get(c.Id).Name+"本期推广结束，可查看期间营业额与合作关系。");
                }
                else if(today.TotalDays%7==0) c.Relationship=Math.Max(0,c.Relationship-1);
            }
            ApplyUpgrades();
        }
        public long FundUnits {get;private set;}
        public Money FundUnitPrice => Money.FromCents((long)Math.Round(PartCatalog.All.Average(x=>Market.PriceOf(x).Cents/(double)x.BasePrice.Cents)*10000));
        public Money FundValue => FundUnitPrice*FundUnits;
        public bool TradeFund(int units,bool buy)
        {
            if(IsGameOver || !HasSmartphone || units<=0 || units>10000) return false;
            Money value=FundUnitPrice*units;
            if(buy)
            {
                if(!Ledger.TryDebit(Clock.Today,"指数投资",value.Scale(1.005),"买入硬件景气指数（含 0.5% 手续费）")) return Fail("现金不足。");
                FundUnits+=units;
            }
            else
            {
                if(FundUnits<units) return Fail("持有份额不足。");
                FundUnits-=units; Ledger.Credit(Clock.Today,"指数赎回",value.Scale(.995),"卖出硬件景气指数（扣 0.5% 手续费）");
            }
            return true;
        }
        public int HomeTier {get;private set;}
        public long HomePriceCents {get;private set;}
        public long HomeMortgageCents {get;private set;}
        public long HomeMonthlyPrincipalCents {get;private set;}
        public string HomeTitle => HomeTier==2 ? "安静公寓" : HomeTier==1 ? "整洁单间" : "城中村出租屋";
        public Money CurrentHomeRent => HomePriceCents>0 ? Money.Zero : HomeTier==2 ? Money.FromYuan(3000) : HomeTier==1 ? Money.FromYuan(1200) : HomeRent;
        public Money HomePurchasePrice => Money.FromYuan(HomeTier==2 ? 450000 : 180000);
        public bool ChangeHomeTier(int tier)
        {
            if(IsGameOver || !InAgency) return Fail("去房产中介办理住处升级。");
            if(tier<0 || tier>2 || tier==HomeTier) return false;
            if(HomePriceCents>0) return Fail("先出售自住房，再换住处。");
            Money rent=Money.FromYuan(tier==2 ? 3000 : tier==1 ? 1200 : 450);
            if(!Ledger.TryDebit(Clock.Today,"搬家",rent+Money.FromYuan(500),"住处首月租金及搬家费")) return Fail("需支付新住处首月租金与 ¥500 搬家费。");
            HomeTier=tier;
            Say("已换为"+HomeTitle+"。家具与储物保留；睡眠质量提高，次月按新月租扣款。"); return true;
        }
        public bool BuyHome(bool mortgage)
        {
            if(IsGameOver || !InAgency || HomeTier==0 || HomePriceCents>0) return Fail("在中介先换成整洁单间或公寓，再办理购买。");
            Money price=HomePurchasePrice,paid=price.Scale(mortgage ? .3 : 1);
            if(!Ledger.TryDebit(Clock.Today,"购置住宅",paid,HomeTitle)) return Fail("购房现金不足。");
            HomePriceCents=price.Cents; HomeMortgageCents=(price-paid).Cents; HomeMonthlyPrincipalCents=(long)Math.Ceiling(HomeMortgageCents/36.0);
            Say("自住房购入，免除月租；月维护费为房价 0.1%。按揭为 36 期等额本金、月利率 0.4%。"); return true;
        }
        public bool RepayHomeMortgage()
        {
            if(IsGameOver || HomeMortgageCents<=0) return false;
            if(!Ledger.TryDebit(Clock.Today,"偿还房贷",Money.FromCents(HomeMortgageCents),"住宅提前还款")) return Fail("现金不足。");
            HomeMortgageCents=0; return true;
        }
        public bool SellHome()
        {
            if(IsGameOver || !InAgency || HomePriceCents<=0) return false;
            Money net=Money.FromCents(HomePriceCents).Scale(.9)-Money.FromCents(HomeMortgageCents);
            if(Ledger.Balance+net<Money.Zero) return Fail("现金不足清偿房贷差额。");
            if(net.IsPositive) Ledger.Credit(Clock.Today,"住宅出售",net,"售价九折，扣清剩余房贷");
            else if(net.IsNegative) Ledger.ForceDebit(Clock.Today,"住宅出售",-net,"补足房贷差额");
            HomePriceCents=HomeMortgageCents=HomeMonthlyPrincipalCents=0;
            Say("住宅已出售，继续租住，次月恢复月租。"); return true;
        }
        private void SettleHomeMortgage(GameDate today)
        {
            if(!today.IsMonthStart || HomePriceCents<=0) return;
            Ledger.ForceDebit(today,"物业维护",Money.FromCents(HomePriceCents).Scale(.001),"自住房维护费");
            if(HomeMortgageCents<=0) return;
            long principal=Math.Min(HomeMortgageCents,HomeMonthlyPrincipalCents);
            Money interest=Money.FromCents(HomeMortgageCents).Scale(.004);
            Ledger.ForceDebit(today,"偿还房贷",Money.FromCents(principal),"住宅月供本金");
            if(interest.IsPositive) Ledger.ForceDebit(today,"房贷利息",interest,"住宅月供利息");
            HomeMortgageCents-=principal;
        }
        private void OnBusinessDay(GameDate previous,GameDate today)
        {
            if(today.TotalDays<=_lastBusinessDay) return;
            _lastBusinessDay=today.TotalDays;
            if (_owned.Contains(VehicleKind.Car)) Ledger.ForceDebit(today,"交通",Money.FromYuan(40),"轿车日养护（含油费保险）");
            if (_owned.Contains(VehicleKind.Van)) Ledger.ForceDebit(today,"交通",Money.FromYuan(60),"货车日养护（含油费保险）");
            SettleSupplierDebts(today.TotalDays); SettleVentures(today); SettleProperties(today); SettleHomeMortgage(today); SettleCreators(previous,today); SettleMcnAndRelationships(previous,today); RefreshEnterpriseOffer();
        }
        private void SaveBusiness(SaveData d)
        {
            d.hasBusiness=true;
            d.business=new BusinessSave { Debts=_supplierDebts.Select(x=>x.Copy()).ToArray(), Shipments=_directShipments.Select(x=>x.Copy()).ToArray(), Contracts=_contracts.Select(x=>x.Copy()).ToArray(),
                Ventures=_ventures.Select(x=>x.Copy()).ToArray(),Properties=_properties.Select(x=>x.Copy()).ToArray(),Creators=_creators.Select(x=>x.Copy()).ToArray(),
                HasMcn=HasMcn,RomanceId=RomanceId,ParkPlaceId=ParkPlaceId,ParkReadyDay=ParkReadyDay,NextDebtId=_nextDebtId,LastOfferWeek=_lastOfferWeek,LastBusinessDay=_lastBusinessDay,DirectAgreement=DirectAgreement,FundUnits=FundUnits,
                HomeTier=HomeTier,HomePriceCents=HomePriceCents,HomeMortgageCents=HomeMortgageCents,HomeMonthlyPrincipalCents=HomeMonthlyPrincipalCents};
        }
        private void RestoreBusiness(SaveData d)
        {
            var b=d.business; if(!d.hasBusiness || b==null || b.NextDebtId<=0) return;
            ParkPlaceId=City.HasPlace(b.ParkPlaceId) ? b.ParkPlaceId : null; ParkReadyDay=b.ParkReadyDay; HasMcn=b.HasMcn; RomanceId=Creators.Get(b.RomanceId)!=null ? b.RomanceId : null;
            _supplierDebts.Clear();_supplierDebts.AddRange((b.Debts??Array.Empty<SupplierDebt>()).Select(x=>x.Copy()));
            _directShipments.Clear();_directShipments.AddRange((b.Shipments??Array.Empty<DirectShipment>()).Where(x=>PartCatalog.Exists(x.PartId) && x.Count>0).Select(x=>x.Copy()));
            _contracts.Clear();_contracts.AddRange((b.Contracts??Array.Empty<EnterpriseContract>()).Select(x=>x.Copy()));
            _ventures.Clear();_ventures.AddRange((b.Ventures??Array.Empty<ManagedVenture>()).Where(x=>(City.HasPlace(x.PlaceId) || IsParkVenture(x)) && Ventures.Get(x.Kind)!=null).Select(x=>x.Copy()));
            _properties.Clear();_properties.AddRange((b.Properties??Array.Empty<OwnedProperty>()).Where(x=>City.HasPlace(x.PlaceId)).Select(x=>x.Copy()));
            foreach(var c in b.Creators??Array.Empty<CreatorState>()) {int i=_creators.FindIndex(x=>x.Id==c.Id);if(i>=0)_creators[i]=c.Copy();}
            _nextDebtId=Math.Max(1,b.NextDebtId);_lastOfferWeek=b.LastOfferWeek;_lastBusinessDay=b.LastBusinessDay;DirectAgreement=b.DirectAgreement;FundUnits=Math.Max(0,b.FundUnits);
            HomeTier=Math.Max(0,Math.Min(2,b.HomeTier));HomePriceCents=b.HomePriceCents;HomeMortgageCents=b.HomeMortgageCents;HomeMonthlyPrincipalCents=b.HomeMonthlyPrincipalCents;
            ApplyUpgrades();
        }
    }
}
