using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public BrandExhibit ActiveBrandExpo=>_brands.Exhibits.FirstOrDefault(e=>e.Format==2&&e.Attending&&!e.Finished&&!e.Missed);
        public bool BrandExpoExclusive=>ActiveBrandExpo!=null;
        public BrandExhibit ExpoAtVenue=>_brands.Exhibits.Where(e=>e.Format==2&&!e.Missed&&BrandDay>=e.Day&&BrandDay<e.Day+4)
            .OrderByDescending(e=>e.Attending).ThenByDescending(e=>e.Day).FirstOrDefault()?.Copy();
        public bool ExpoTimeAdvancing {get;private set;}
        public static string ExpoBoothName(int size)=>size==0?"小型摊位":size==1?"中型摊位":"大型摊位";
        public static int ExpoMinProducts(int size)=>size==0?1:size==1?4:6;
        public static int ExpoMaxProducts(int size)=>size==0?2:size==1?5:8;
        public static int ExpoMinKols(int size)=>size==0?1:size==1?5:8;
        public static long ExpoBoothFee(int size)=>size==0?5000:size==1?20000:60000;
        public static long ExpoPosterFee(int size)=>size==0?0:size==1?3000:8000;
        public const int ExpoEndorserStageDecision = 3;

        private static bool ExpoEndorserDeal(BrandExhibit exhibit, BrandDeal deal) =>
            deal.BrandId == exhibit.BrandId && deal.Mode == "endorse" && !deal.Ended &&
            deal.Response != "pause" && deal.Start <= exhibit.Day && deal.End > exhibit.Day;

        public string[] ExpoEndorsers(BrandExhibit e) => e == null || e.Format != 2 || e.Finished || e.Missed
            ? Array.Empty<string>()
            : _brands.Deals.Where(d => ExpoEndorserDeal(e, d)).Select(d => d.PersonId).Distinct().ToArray();

        public bool ExpoPersonArrived(string id)
        {
            var person = CityPersonState(id);
            return person != null && person.LocationPlaceId == "life_expo" && person.Route == null &&
                string.IsNullOrEmpty(person.ActivityId) && string.IsNullOrEmpty(person.VisitId);
        }

        public bool ExpoEndorserOnStage(BrandExhibit e, string id) => e != null && e.Attending && !e.Finished && !e.Missed &&
            BrandDay == e.Day && e.Decision == ExpoEndorserStageDecision && ExpoPersonArrived(id);

        public string ExpoEndorserStatus(BrandExhibit e, string id)
        {
            if (BrandDay < e.Day || BrandDay == e.Day && Clock.MinuteOfDay < 360) return "开幕日 09:00 到场 · 13:00 舞台介绍";
            if (BrandDay > e.Day || Clock.MinuteOfDay >= 1020) return "开幕日安排已结束";
            if (ExpoEndorserOnStage(e, id)) return "正在舞台 · 品牌介绍";
            if (ExpoPersonArrived(id)) return "已到场 · 品牌展台";
            return CityPersonState(id)?.Route?.TargetPlaceId == "life_expo" ? "在路上 · 前往会展中心" : "尚未到场";
        }

        public long ExpoKolDayQuote(string id,int booth)=>KolDailyWage(id)*(booth+3);
        public string[] ExpoAvailableKols(string brandId,int day)=>_kol.Contracts.Where(c=>c.BrandId==brandId&&!c.Ended&&c.SignedAt<=Clock.Now.TotalMinutes&&c.EndAt>=(day+3)*1440L+1020)
            .Select(c=>c.PersonId).Distinct().ToArray();
        public string ExpoBookingBlocker(string brandId,string shopId,int booth,string[] products,string[] kols)
        {
            var b=Brand(brandId);int day=NextBrandExpoDay;
            if(IsGameOver||BrandExpoExclusive||b==null||booth<0||booth>2)return "当前无法报名";
            if(BrandDay>day-7)return "展前7天截止报名，请等下一季度";
            if(_brands.Exhibits.Any(e=>e.BrandId==brandId&&e.Day==day))return "这个品牌本季度已经报名";
            if(!BrandShops(b).Any(s=>s.Place.Id==shopId))return "请选择品牌关联的同业门店";
            if(products==null||products.Length<ExpoMinProducts(booth)||products.Length>ExpoMaxProducts(booth)||products.Distinct().Count()!=products.Length)return "请按摊位要求选择不同展品";
            if(products.Any(p=>!BrandProducts(shopId).Contains(p)||BrandProductCount(shopId,p)<100))return "每种展品需100件本店现货；报名后移交展务保管";
            if(kols==null||kols.Length<ExpoMinKols(booth)||kols.Distinct().Count()!=kols.Length)return "签约KOL人数不足，重复选择不计人数";
            var available=ExpoAvailableKols(brandId,day);
            if(kols.Any(p=>!available.Contains(p)))return "只能邀请本品牌已签约且合约覆盖全部4天的KOL";
            if(_brands.Exhibits.Any(e=>!e.Missed&&!e.Finished&&e.Day==day&&((e.KolIds??Array.Empty<string>()).Intersect(kols).Any())))return "某位KOL已有同期站台安排";
            if(Ledger.Balance<Money.FromYuan(ExpoBoothFee(booth)))return "摊位资金不足";
            return null;
        }
        public bool BookQuarterlyExpo(string brandId,string shopId,int booth,string[] products,string[] kols)
        {
            string why=ExpoBookingBlocker(brandId,shopId,booth,products,kols);if(why!=null)return Fail(why);
            if(!Ledger.TryDebit(Clock.Today,"展会摊位",Money.FromYuan(ExpoBoothFee(booth)),"季度展会4天 · "+ExpoBoothName(booth)+" · 含展务餐宿",
                new LedgerAttribution(LedgerBusinessKind.Operating,shopId:shopId,propertyId:brandId,sourceEventId:brandId+":expo:"+NextBrandExpoDay+":booth")))return false;
            foreach(string p in products)BrandTake(shopId,p,100);
            var e=new BrandExhibit{Format=2,Id=brandId+":expo:"+NextBrandExpoDay,BrandId=brandId,ShopId=shopId,ProductId=products[0],Focus="buyers",Day=NextBrandExpoDay,Booth=booth,
                KolIds=kols.ToArray(),KolDayQuotes=kols.Select(p=>ExpoKolDayQuote(p,booth)).ToArray(),Products=products.Select(p=>new ExpoProduct{ProductId=p,UnitCents=BrandProductPrice(p)}).ToArray()};
            _brands.Exhibits=AppendBrand(_brands.Exhibits,e);Say("摊位已预订：每种100件展品已移交展务。请准备海报，开幕当天09:00前后到场，连续参与4天。");return true;
        }
        public string ExpoPosterBlocker(string id,string person)
        {
            var e=_brands.Exhibits.FirstOrDefault(x=>x.Id==id);
            if(IsGameOver||e==null||e.Format!=2||e.Booth==0||e.Attending||e.Finished||e.Missed||e.DesignerId!=null)return "当前无需或不能委托海报";
            if(!new[]{"life_designer","resident_dengyue","life_creator"}.Contains(person)||!IsMet(person))return "先认识这位设计/创作人员";
            if(Clock.Now.TotalMinutes+1440>e.Day*1440L+540)return "海报需制作24小时，已来不及在开幕前完成";
            return Ledger.Balance<Money.FromYuan(ExpoPosterFee(e.Booth))?"海报预算不足":null;
        }
        public bool CommissionExpoPoster(string id,string person)
        {
            string why=ExpoPosterBlocker(id,person);if(why!=null)return Fail(why);
            var e=_brands.Exhibits.First(x=>x.Id==id);
            if(!Ledger.TryDebit(Clock.Today,"展会海报",Money.FromYuan(ExpoPosterFee(e.Booth)),"展会海报制作 · "+CityPerson(person).Name,
                new LedgerAttribution(LedgerBusinessKind.Operating,shopId:e.ShopId,propertyId:e.BrandId,sourceEventId:e.Id+":poster")))return false;
            e.DesignerId=person;e.PosterReadyAt=Clock.Now.TotalMinutes+1440;return true;
        }
        public string ExpoStartBlocker(string id)
        {
            var e=_brands.Exhibits.FirstOrDefault(x=>x.Id==id);
            if(IsGameOver||e==null||e.Format!=2||e.Missed||e.Finished)return "展会已结束或记录不可用";
            if(e.Attending)return null;
            if(BrandExpoExclusive||IsWorking||_sleeping||RunningCityActivity!=null||Trip!=null||PendingEvent!=null)return "请先结束其他工作、活动或出行";
            if(BrandDay!=e.Day||Clock.MinuteOfDay<540||Clock.MinuteOfDay>600)return "请于开幕当天09:00—10:00到会展中心报到";
            if(CurrentPlace?.Id!="life_expo")return "请亲自进入云帆会展中心报到";
            if(ShopAt(e.ShopId)?.Rented!=true)return "来源门店已关闭或搬迁，本届无法开摊；未售展品将在错过后退回家中后仓";
            if(Player.Energy<50||Player.Health<30)return "连续参展前请先休息，至少需要精力50、健康30";
            if(e.Booth>0&&(e.DesignerId==null||e.PosterReadyAt>Clock.Now.TotalMinutes))return "海报尚未制作完成";
            if(e.KolIds.Any(p=>!ExpoAvailableKols(e.BrandId,e.Day).Contains(p)))return "有站台KOL合约失效，请在开幕前更换同品牌签约KOL";
            if(e.KolIds.Any(p=>CityPersonState(p)?.LocationPlaceId!="life_expo"||CityPersonState(p)?.Route!=null||!string.IsNullOrEmpty(CityPersonState(p)?.ActivityId)))return "站台KOL尚未全部到场，请等待或处理其活动冲突";
            return null;
        }
        public bool ReplaceExpoKol(string id,string oldId,string newId)
        {
            var e=_brands.Exhibits.FirstOrDefault(x=>x.Id==id);
            if(IsGameOver||e==null||e.Format!=2||e.Attending||e.Finished||e.Missed||Clock.Now.TotalMinutes>=e.Day*1440L+540)return false;
            int index=Array.IndexOf(e.KolIds,oldId);
            if(index<0||e.KolIds.Contains(newId)||!ExpoAvailableKols(e.BrandId,e.Day).Contains(newId)
                ||_brands.Exhibits.Any(x=>x!=e&&!x.Missed&&!x.Finished&&x.Day==e.Day&&x.KolIds.Contains(newId)))return false;
            e.KolIds[index]=newId;e.KolDayQuotes[index]=ExpoKolDayQuote(newId,e.Booth);return true;
        }
        public bool StartQuarterlyExpo(string id)
        {
            string why=ExpoStartBlocker(id);if(why!=null)return Fail(why);
            var e=_brands.Exhibits.First(x=>x.Id==id);if(e.Attending)return false;
            e.Attending=true;e.Started=Clock.Now.TotalMinutes;PayExpoAttendance(e);return true;
        }
        private void PayExpoAttendance(BrandExhibit e)
        {
            long surcharge=0;
            for(int i=0;i<e.KolIds.Length;i++)surcharge+=Math.Max(0,e.KolDayQuotes[i]-KolDailyWage(e.KolIds[i]));
            if(surcharge>0)Ledger.ForceDebit(Clock.Today,"展会KOL站台",Money.FromYuan(surcharge),"第"+(e.AttendanceDays+1)+"天站台溢价（基础日薪另按日结算）",
                new LedgerAttribution(LedgerBusinessKind.Operating,shopId:e.ShopId,propertyId:e.BrandId,sourceEventId:e.Id+":attendance:"+e.AttendanceDays));
            e.ExpoWagesCents+=surcharge*100;e.AttendanceDays++;
        }
        public ExpoMoment CurrentExpoMoment(string id)
        {
            var e = _brands.Exhibits.FirstOrDefault(x => x.Id == id);
            if (e == null || e.Format != 2 || e.Decision >= 24) return null;
            if (e.Decision == ExpoEndorserStageDecision)
            {
                var present = ExpoEndorsers(e).Where(ExpoPersonArrived).Select(p => CityPerson(p).Name).ToArray();
                if (present.Length > 0)
                    return ExpoMoments.BrandIntroduction(Brand(e.BrandId)?.Name ?? "本品牌", present);
            }
            return ExpoMoments.At(e.Day, e.Decision);
        }
        public bool ChooseExpoAction(string id,int choice)
        {
            var e=ActiveBrandExpo;if(IsGameOver||e==null||e.Id!=id||choice<0||choice>2||ExpoTimeAdvancing||e.Decision>=24)return false;
            if(CurrentPlace?.Id!="life_expo"||BrandDay<e.Day||BrandDay>=e.Day+4)return Fail("现场互动需要本人处于本届展会的会展中心内");
            var moment=CurrentExpoMoment(e.Id);int delta=moment.Scores[choice];
            e.Score=Math.Max(0,e.Score+delta);e.Heat=Math.Max(0,Math.Min(100,e.Heat+moment.Heat[choice]));e.Trust=Math.Max(0,Math.Min(100,e.Trust+delta/2));
            e.LastFeedback=moment.Feedback[choice];e.Journal=AppendBrand(e.Journal,"第"+(e.Decision/6+1)+"天 · "+moment.Title+"："+moment.Options[choice]+"。"+e.LastFeedback);
            int charm=(int)e.KolIds.Average(p=>KolMarket.Get(p).Charm),iq=(int)e.KolIds.Average(p=>KolMarket.Get(p).Intelligence);
            foreach(var p in e.Products)
            {
                int sold=Math.Min(p.Reserved-p.Sold,Math.Max(0,(2+e.Booth+(e.Heat+charm)/70+(e.Trust+iq)/90)+(delta<0?-3:delta>=15?1:0)));
                if(sold<=0)continue;p.Sold+=sold;long revenue=p.UnitCents*sold*90/100;e.RevenueCents+=revenue;
                Ledger.Credit(Clock.Today,"展会零售",Money.FromCents(revenue),BrandProductName(p.ProductId)+" ×"+sold,
                    new LedgerAttribution(LedgerBusinessKind.Operating,shopId:e.ShopId,propertyId:e.BrandId,sourceEventId:e.Id+":sale:"+e.Decision+":"+p.ProductId).Product(p.ProductId,sold,p.UnitCents));
            }
            e.Decision++;e.Stage=e.Decision;
            long roundEnd=(e.Day+(e.Decision-1)/6)*1440L+540+((e.Decision-1)%6+1)*80;
            AdvanceExpoTime((int)Math.Max(0,roundEnd-Clock.Now.TotalMinutes),false);
            if(IsGameOver||e.Missed||!e.Attending)return true;
            if(e.Decision>=24)FinishQuarterlyExpo(e);
            else if(e.Decision%6==0)
            {
                AdvanceExpoTime((int)Math.Max(0,(e.Day+e.Decision/6)*1440L+540-Clock.Now.TotalMinutes),true);
                if(e.Attending&&!e.Missed)PayExpoAttendance(e);
            }
            return true;
        }
        private void AdvanceExpoTime(int minutes,bool overnight)
        {
            ExpoTimeAdvancing=true;
            try
            {
                // Catering is included in the four-day booth package. Night rest is logged,
                // while shops, wages, leases and deliveries continue on the ordinary clock.
                Player.Hunger=Math.Max(Player.Hunger,75);
                if(overnight)
                {
                    long before=Clock.Now.TotalMinutes;bool sleeping=_sleeping;_sleeping=true;
                    try{AdvanceClock(minutes);}finally{_sleeping=sleeping;}
                    _sleepHistory.Add(new[]{Math.Max(before,Clock.Now.TotalMinutes-480),Clock.Now.TotalMinutes});
                    Player.SleepDebt=0;Player.Energy=Player.MaxEnergy;Player.Hunger=90;
                }
                else AdvanceClock(minutes);
            }
            finally{ExpoTimeAdvancing=false;}
        }
        private void ReturnExpoStock(BrandExhibit e)
        {
            if(e.StockReturned)return;var shop=ShopAt(e.ShopId);
            foreach(var p in e.Products)
            {
                int remaining=p.Reserved-p.Sold;if(remaining<=0)continue;
                if(PCShopSell.Core.Catalog.PartCatalog.Exists(p.ProductId))(shop?.Sim.Stock??HomeBoxParts).ForceAdd(PCShopSell.Core.Catalog.PartCatalog.Get(p.ProductId),true,remaining);
                else if(shop!=null)shop.Sim.Stock.ForceAddGoods(p.ProductId,remaining);else HomeBox.AddGift(p.ProductId,remaining);
            }
            e.StockReturned=true;
        }
        private void FinishQuarterlyExpo(BrandExhibit e)
        {
            e.Finished=true;e.Attending=false;ReturnExpoStock(e);var b=Brand(e.BrandId);
            b.Influence+=Math.Max(5,e.Score/4);b.Reputation=Math.Max(0,b.Reputation+(e.Trust>=60?5:e.Trust<30?-5:0));
            foreach(string id in e.KolIds){var career=EnsureKolCareer(id);career.Popularity=Math.Min(1000,career.Popularity+Math.Max(1,e.Heat/10));career.Followers=Math.Min(2000000,KolFollowers(id)+Math.Max(5,e.Score));var creator=Creator(id);if(creator!=null)creator.Followers=career.Followers;}
            if(e.Score>=180)_brands.Orders=AppendBrand(_brands.Orders,new BrandOrder{Id=e.Id+":lead",BrandId=e.BrandId,ShopId=e.ShopId,ProductId=e.ProductId,Source="expo",Count=20+e.Booth*20,Due=BrandDay+14,Influence=70,UnitCents=BrandProductPrice(e.ProductId)*90/100});
            e.LastFeedback="4天展会结束，未售展品已退回来源门店。"+(e.Score>=180?"获得采购订单，请在14天内实际交货。":"本次未获得采购订单。");
            RefreshBrandTraffic();Say(e.LastFeedback);
        }
        private void SettleQuarterlyExpoAbsence(int day)
        {
            foreach(var e in _brands.Exhibits.Where(x=>x.Format==2&&!x.Finished&&!x.Missed&&!x.Attending&&x.Day<day))
            {e.Missed=true;ReturnExpoStock(e);e.LastFeedback="未到场，展位与海报费用不退，未售展品退回。";}
        }
        private void InterruptBrandExpoForHealth()
        {
            var e=ActiveBrandExpo;if(e==null)return;
            e.Attending=false;e.Missed=true;ReturnExpoStock(e);e.LastFeedback="因身体不适中断参展，保留已发生的销售和费用，未售展品退回。";
        }
    }
}
