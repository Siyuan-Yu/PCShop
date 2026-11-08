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
        public double CreatorTrafficMultiplier => CityCampaignTraffic(_focus);
        public Money CreatorFee(CreatorState c) => Money.FromYuan(Creators.Get(c.Id).Fee).Scale((.7+c.Followers/(double)Creators.Get(c.Id).Audience*.3)*(1-c.Relationship*.002));
        public bool SupportCreator(string id)
        {
            var c=Creator(id); if(c==null || (!HasSmartphone&&!CanUseHomeWorkbench) || IsGameOver) return false;
            string contact=KolContactBlocker(id);if(contact!=null)return Fail(contact);
            if(c.LastSupportDay==Clock.Today.TotalDays) return Fail("今天已经支持过这位创作者。");
            if(!Ledger.TryDebit(Clock.Today,"推广",Money.FromYuan(500),Creators.Get(id).Name+"设备与制作赞助")) return Fail("赞助需要 ¥500。");
            c.LastSupportDay=Clock.Today.TotalDays; c.Resources=Math.Min(100,c.Resources+30); c.Relationship=Math.Min(100,c.Relationship+5); return true;
        }
        private void TrackCreatorCampaigns(GameTimestamp now)
        {
            foreach(var c in _creators)
            {
                if(c.CampaignUntilDay<=now.Date.TotalDays) continue;
                long sales=CreatorShopRevenue(c,now.Date);
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
                    long sales=CreatorShopRevenue(c,previous);
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
        private long CreatorShopRevenue(CreatorState creator,GameDate day) => Ledger.Entries.Where(e=>e.Date==day&&e.ShopId==(creator.CampaignShopId??MainShop.Place.Id)
            &&e.Amount.IsPositive&&(e.Category=="零售"||e.Category=="装机")).Sum(e=>e.Amount.Cents);
        private Money HardwareMarketIndex => Money.FromCents((long)Math.Round(PartCatalog.All.Average(x=>Market.PriceOf(x).Cents/(double)x.BasePrice.Cents)*10000));
        public Money FundUnitPrice => Money.FromCents(_stockMarket.IndexPriceCents);
        public Money FundValue => FundUnitPrice*FundUnits;
        public bool TradeFund(int units,bool buy) => TradeCityInvestment("hardware", units, buy);
        public int HomeTier {get;private set;}
        public long HomePriceCents {get;private set;}
        public long HomeMortgageCents {get;private set;}
        public long HomeMonthlyPrincipalCents {get;private set;}
        public string HomeTitle => City.HasPlace(_residentialHomeId)?City.GetPlace(_residentialHomeId).Name:HomeTier==3 ? "花园独栋别墅" : HomeTier==2 ? "公园豪华公寓" : HomeTier==1 ? "宜居改善住宅" : "城郊老破小出租屋";
        public Money CurrentHomeRent => HomePriceCents>0 ? Money.Zero : Money.FromCents(_homeLeaseRentCents);
        public Money HomePurchasePrice => ResidentialPrice(Home);
        public Money HomeTierRentQuote(int tier) => HomeBaseRent(tier).Scale(HomeRentFactor);
        public bool ChangeHomeTier(int tier)
        {
            string why=ResidenceBlocker(tier,false);if(why!=null)return Fail(why);
            Money rent=HomeTierRentQuote(tier);
            if(!PayForResidenceMove(tier,"搬家",rent+Money.FromYuan(500),"住处首月租金及搬家费")) return false;
            _homeLeaseRentCents=rent.Cents;
            _residentialHomeId=null;
            Say("已换为"+HomeTitle+"。家具与储物保留；睡眠质量提高，次月按新月租扣款。"); return true;
        }
        public bool BuyHome(bool mortgage)
        {
            return City.HasPlace(_residentialHomeId)?BuyResidentialHome(Home.Id,mortgage,true):BuyResidence(HomeTier,mortgage);
        }
        public bool RepayHomeMortgage()
        {
            return Fail("住宅仅支持全款购买，没有待还房贷。");
        }
        public bool SellHome()
        {
            if(IsGameOver || !InAgency || HomePriceCents<=0) return false;
            if(City.LayoutVersion>=7 && !AgencyServes(CurrentPlace,Home))return Fail("请到自住房所属片区的中介办理。");
            Money net=HomePurchasePrice.Scale(.9);
            if(net.IsPositive) Ledger.Credit(Clock.Today,"住宅出售",net,"售价九折");
            HomePriceCents=HomeMortgageCents=HomeMonthlyPrincipalCents=0;
            _homeLeaseRentCents=HomeTierRentQuote(HomeTier).Cents;
            Say("住宅已出售，继续租住，次月恢复月租。"); return true;
        }
        private void SettleHomeMaintenance(GameDate today)
        {
            if(!today.IsMonthStart || HomePriceCents<=0) return;
            Ledger.ForceDebit(today,"物业维护",Money.FromCents(HomePriceCents).Scale(.001),"自住房维护费");
        }
        private void OnBusinessDay(GameDate previous,GameDate today)
        {
            if(today.TotalDays<=_lastBusinessDay) return;
            _lastBusinessDay=today.TotalDays;
            TickBrandBusiness(previous,today);
            SettleGarageRent(today);
            if (VehicleCount(VehicleKind.Car) > 0) Ledger.ForceDebit(today,"交通",Money.FromYuan(40) * VehicleCount(VehicleKind.Car),"轿车日养护（含油费保险）");
            if (VehicleCount(VehicleKind.Van) > 0) Ledger.ForceDebit(today,"交通",Money.FromYuan(60) * VehicleCount(VehicleKind.Van),"货车日养护（含油费保险）");
            TickCityAssets(); SettleSupplierDebts(today.TotalDays); SettleVentures(today); SettleProperties(today); SettleHomeMaintenance(today); SettleCreators(previous,today); SettleMcnAndRelationships(previous,today); RefreshEnterpriseOffer();
        }
        private void SaveBusiness(SaveData d)
        {
            d.brandBusiness=_brands.Copy();
            d.brandStories=_brandStories.Select(s=>s.Copy()).ToArray();
            d.hasBusiness=true;
            d.business=new BusinessSave { Debts=_supplierDebts.Select(x=>x.Copy()).ToArray(), Shipments=_directShipments.Select(x=>x.Copy()).ToArray(), Contracts=_contracts.Select(x=>x.Copy()).ToArray(),
                Ventures=_ventures.Select(x=>x.Copy()).ToArray(),Properties=_properties.Select(x=>x.Copy()).ToArray(),Creators=_creators.Select(x=>x.Copy()).ToArray(),
                HasMcn=HasMcn,RomanceId=RomanceId,ParkPlaceId=ParkPlaceId,ParkReadyDay=ParkReadyDay,NextDebtId=_nextDebtId,LastOfferWeek=_lastOfferWeek,LastBusinessDay=_lastBusinessDay,DirectAgreement=DirectAgreement,FundUnits=FundUnits,
                HomeTier=HomeTier,HomePriceCents=HomePriceCents,HomeMortgageCents=HomeMortgageCents,HomeMonthlyPrincipalCents=HomeMonthlyPrincipalCents};
        }
        private void RestoreBusiness(SaveData d)
        {
            _brands=d.brandBusiness?.Copy()??new BrandBusinessState();
            foreach (var deal in _brands.Deals.Where(d => d.Mode == "campaign")) deal.Ended = true;
            _brandStories.Clear();
            _brandStories.AddRange((d.brandStories??Array.Empty<BrandStoryRecord>()).Where(s=>s!=null).GroupBy(s=>s.Id).Select(g=>g.First().Copy()));
            var b=d.business; if(!d.hasBusiness || b==null || b.NextDebtId<=0) return;
            ParkPlaceId=City.HasPlace(b.ParkPlaceId) ? b.ParkPlaceId : null; ParkReadyDay=b.ParkReadyDay; HasMcn=b.HasMcn; RomanceId=Creators.Get(b.RomanceId)!=null ? b.RomanceId : null;
            _supplierDebts.Clear();_supplierDebts.AddRange((b.Debts??Array.Empty<SupplierDebt>()).Select(x=>x.Copy()));
            _directShipments.Clear();_directShipments.AddRange((b.Shipments??Array.Empty<DirectShipment>()).Where(x=>PartCatalog.Exists(x.PartId) && x.Count>0).Select(x=>x.Copy()));
            _contracts.Clear();_contracts.AddRange((b.Contracts??Array.Empty<EnterpriseContract>()).Select(x=>x.Copy()));
            _ventures.Clear();_ventures.AddRange((b.Ventures??Array.Empty<ManagedVenture>()).Where(x=>(City.HasPlace(x.PlaceId) || IsParkVenture(x)) && Ventures.Get(x.Kind)!=null).Select(x=>x.Copy()));
            _properties.Clear();_properties.AddRange((b.Properties??Array.Empty<OwnedProperty>()).Where(x=>City.HasPlace(x.PlaceId)).Select(x=>x.Copy()));
            foreach(var c in b.Creators??Array.Empty<CreatorState>()) {int i=_creators.FindIndex(x=>x.Id==c.Id);if(i>=0)_creators[i]=c.Copy();}
            _nextDebtId=Math.Max(1,b.NextDebtId);_lastOfferWeek=b.LastOfferWeek;_lastBusinessDay=b.LastBusinessDay;DirectAgreement=b.DirectAgreement;FundUnits=Math.Max(0,b.FundUnits);
            HomeTier=Math.Max(0,Math.Min(3,b.HomeTier));HomePriceCents=b.HomePriceCents;HomeMortgageCents=HomeMonthlyPrincipalCents=0;
            ApplyUpgrades();
        }
    }
}
