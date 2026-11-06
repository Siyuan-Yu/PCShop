using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.Finance;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private BrandBusinessState _brands = new BrandBusinessState();
        public BrandBusinessState BrandBusiness => _brands.Copy();
        public static readonly string[] BrandKinds = { "computer", "clothing", "models" };
        public static readonly string[] BrandPeople = { "momo", "ake", "qiao", "life_creator", "life_designer" };
        private int BrandDay => Clock.Today.TotalDays;
        private BrandRecord Brand(string id) => _brands.Brands.FirstOrDefault(b=>b.Id==id);
        private void RefreshBrandTraffic(){foreach(var s in MyShops)WithShop(s,ApplyUpgrades);}
        private IEnumerable<OwnedShop> BrandShops(BrandRecord b) => MyShops.Where(s=>b.ShopIds.Contains(s.Place.Id)&&s.RetailKind==b.Kind);
        private static T[] AppendBrand<T>(T[] array,T value) => (array??Array.Empty<T>()).Concat(new[]{value}).ToArray();
        private bool BrandPay(long cents,string memo,string brandId = null) => !IsGameOver && Ledger.TryDebit(Clock.Today,"品牌经营",Money.FromCents(cents),memo,BrandAttribution(brandId));
        private long BrandRevenue(IEnumerable<OwnedShop> shops,int first,int end)
        {
            var ids=new HashSet<string>(shops.Select(s=>s.Place.Id));
            return Ledger.Entries.Where(e=>ids.Contains(e.ShopId)&&e.Date.TotalDays>=first&&e.Date.TotalDays<end&&e.Amount.IsPositive
                &&(e.Category=="零售"||e.Category=="装机"||e.Category=="餐饮"||e.Category=="品牌订单"||e.Category=="展会零售")).Sum(e=>e.Amount.Cents);
        }
        // Competitor estimates are explicitly a market benchmark, not fabricated ledger entries.
        public long BrandRivalMonthlySales(string kind,int day) => (kind=="computer"?24000000L:kind=="clothing"?12000000L:16000000L)*(90+(day/30*17)%21)/100;
        public string BrandRivalName(string kind) => kind=="computer"?"兴华电脑城":kind=="clothing"?"潮流服饰":"纸风车潮玩";
        public long BrandSuburbRevenue(string kind) => BrandRevenue(MyShops.Where(s=>s.RetailKind==kind&&DistrictTierOf(s.Place)==0),BrandDay-30,BrandDay);
        public string BrandQualificationText(string kind)
        {
            var q=_brands.Qualifications.FirstOrDefault(x=>x.Kind==kind);
            var shops=MyShops.Where(s=>s.RetailKind==kind&&!s.Sim.NotFitted).ToArray();
            return $"同业门店 {shops.Length}/5 · 城郊 {shops.Count(s=>DistrictTierOf(s.Place)==0)}/3 · 社区 {shops.Count(s=>DistrictTierOf(s.Place)==1)}/2\n城郊近30日营收 {Money.FromCents(BrandSuburbRevenue(kind))} / 同行领先基准 {Money.FromCents(BrandRivalMonthlySales(kind,BrandDay))}\n连续领先 {q?.LeadingDays??0}/14 天 · 社区持续经营 {q?.CommunityDays??0}/30 天";
        }
        public string BrandFoundBlocker(string kind)
        {
            if(IsGameOver)return "当前不能经营";
            if(!BrandKinds.Contains(kind))return "该行业尚不开放品牌业务";
            if(DistrictAccessTier<1)return "进入社区后开放品牌创立";
            if(_brands.Brands.Any(b=>b.Kind==kind))return "这个行业已经有品牌";
            var q=_brands.Qualifications.FirstOrDefault(x=>x.Kind==kind);
            if(q==null||q.LeadingDays<14||q.CommunityDays<30)return "先持续达到城郊领先、社区站稳的资格";
            var shops=MyShops.Where(s=>s.RetailKind==kind&&!s.Sim.NotFitted).ToArray();
            if(shops.Length<5||shops.Count(s=>DistrictTierOf(s.Place)==0)<3||shops.Count(s=>DistrictTierOf(s.Place)==1)<2)return "至少5家同业门店，其中城郊3家、社区2家";
            return null;
        }
        public bool FoundBrand(string kind,string name,string position)
        {
            string why=BrandFoundBlocker(kind);if(why!=null)return Fail(why);
            name=(name??"").Trim();if(name.Length<2||name.Length>16)return Fail("品牌名需2—16字");
            if(position!="value"&&position!="quality")return false;
            var b=new BrandRecord {Id="brand:"+kind,Kind=kind,Name=name,Position=position,Founded=BrandDay,
                ShopIds=MyShops.Where(s=>s.RetailKind==kind).Select(s=>s.Place.Id).ToArray()};
            _brands.Brands=AppendBrand(_brands.Brands,b);RefreshBrandTraffic();Say(name+"正式成立。可以准备参展，也可以安排已签约的代言人。");return true;
        }
        public bool RenameBrand(string brandId, string name)
        {
            var brand = Brand(brandId);
            if (IsGameOver || brand == null) return Fail("当前不能为这个品牌改名");

            name = (name ?? "").Trim();
            if (name.Length < 2 || name.Length > 16 || name.Any(char.IsControl))
                return Fail("请输入2—16字的单行品牌名");
            if (name == brand.Name) return Fail("请输入不同的品牌名");

            brand.Name = name;
            brand.Reputation /= 2;
            brand.Influence /= 2;
            RefreshBrandTraffic();
            RefreshDistrictNarrative();
            Say($"品牌已更名为{name}，口碑降至{brand.Reputation}，知名度降至{brand.Influence}。");
            return true;
        }

        public bool ToggleBrandShop(string brandId,string shopId)
        {
            var b=Brand(brandId);var s=ShopAt(shopId);if(IsGameOver||b==null||s==null||!s.Rented||s.RetailKind!=b.Kind)return false;
            if(b.ShopIds.Contains(shopId)){if(b.ShopIds.Length<=1)return Fail("品牌至少保留一家门店");b.ShopIds=b.ShopIds.Where(id=>id!=shopId).ToArray();}
            else b.ShopIds=AppendBrand(b.ShopIds,shopId);RefreshBrandTraffic();return true;
        }
        public bool RespondBrandDeal(string id,string response)
        {
            var d=_brands.Deals.FirstOrDefault(x=>x.Id==id);if(IsGameOver||d==null||d.Mode!="endorse"||d.Ended||d.End<=BrandDay)return false;
            if(!string.IsNullOrEmpty(d.KolContractId))
            {
                var c=FindKolContract(d.KolContractId);
                if(c==null||!KolContractCurrent(c,Clock.Now.TotalMinutes))return false;
                if(response=="end")return EndKolLive(c.Id);
                if(response=="pause"||response=="continue")
                {
                    d.Response=response;return SetKolSchedule(c.Id,c.StartMinute,c.EndMinute,response!="pause");
                }
            }
            if(response=="end")
            {
                long penalty=d.Severity>=3?0:d.Fee*(d.End-BrandDay)/(d.End-d.Start)*30/100;
                if(!BrandPay(penalty,"合作提前解约",d.BrandId))return Fail("解约资金不足");d.Ended=true;if(d.Severity<3)AddRelation(d.PersonId,-3);RefreshBrandTraffic();return true;
            }
            if(response!="continue"&&response!="pause"&&response!="negotiate")return false;
            if(d.Response==response)return false;
            if(response=="negotiate")
            {if(d.Severity==0||d.LastNegotiationDay>=d.NewsDay)return Fail("当前没有尚待核实的新风波");if(!BrandPay(20000,"合作沟通与核实",d.BrandId))return Fail("需要¥200沟通费用");d.LastNegotiationDay=d.NewsDay;AddRelation(d.PersonId,2);}
            d.Response=response;RefreshBrandTraffic();return true;
        }
        public double BrandTraffic(OwnedShop shop)
        {
            var b = _brands.Brands.FirstOrDefault(x => x.Kind == shop.RetailKind);
            if (b == null) return 0;
            double boost = b.ShopIds.Contains(shop.Place.Id)
                ? BrandBackgroundTraffic(b) + BrandPromotionTraffic(b.Id)
                : BrandStandingTraffic(b);
            return Math.Max(-.30,Math.Min(.60,boost));
        }
        public double BrandAudienceFit(string person,string kind)=>person=="life_designer"?(kind=="clothing"?1.3:.5):person=="life_creator"?(kind=="models"?1.3:.6):kind=="computer"?1.2:.6;
        public int NextBrandExpoDay => BrandDay/90*90+75>=BrandDay?BrandDay/90*90+75:BrandDay/90*90+165;
        public string BrandExpoTheme(int day) => new[]{"实用生活","原创设计","品质服务","年度精品"}[(day/90)%4];
        public IEnumerable<string> BrandProducts(string shopId)
        {
            var s=ShopAt(shopId);if(s==null)return Array.Empty<string>();
            return s.Sim.Stock.GoodsLines().Where(g=>g.Value>0&&RetailShopTypes.Sells(s.RetailKind,g.Key)).Select(g=>g.Key)
                .Concat(s.RetailKind=="computer"?PartCatalog.All.Where(p=>s.Sim.Stock.Count(p,true)>0).Select(p=>p.Id):Array.Empty<string>()).ToArray();
        }
        public string BrandProductName(string id) => PartCatalog.Exists(id)?PartCatalog.Get(id).Name:Gifts.Get(id)?.Name??id;
        private long BrandProductPrice(string id) => PartCatalog.Exists(id)?Market.PriceOf(PartCatalog.Get(id)).Cents:
            Gifts.Get(id)==null?0:Gifts.Get(id).Price.Scale(IndustryCycles.GoodsFactor(Gifts.Get(id),BrandDay)).Cents;
        public int BrandProductCount(string shopId,string id)
        {var s=ShopAt(shopId);return s==null||!s.Rented?0:PartCatalog.Exists(id)?s.Sim.Stock.Count(PartCatalog.Get(id),true):s.Sim.Stock.GoodsCount(id);}
        private bool BrandTake(string shopId,string id,int count)
        {var s=ShopAt(shopId);return s!=null&&(PartCatalog.Exists(id)?s.Sim.Stock.TryRemove(PartCatalog.Get(id),true,count):s.Sim.Stock.TryRemoveGoods(id,count));}
        public bool RegisterBrandExpo(string brandId,string shopId,string product,string focus)
        {
            // Existing callers enter the new preparation rules; old saved exhibits retain their format.
            return BookQuarterlyExpo(brandId,shopId,0,new[]{product},ExpoAvailableKols(brandId,NextBrandExpoDay).Take(1).ToArray());
        }
        public string BrandExpoBlocker(string id)
        {
            var modern=_brands.Exhibits.FirstOrDefault(x=>x.Id==id&&x.Format==2);if(modern!=null)return ExpoStartBlocker(id);
            var e=_brands.Exhibits.FirstOrDefault(x=>x.Id==id);if(e==null||e.Finished||e.Missed)return "展会已结束";
            if(BrandDay!=e.Day)return "请在展会当天到场";
            long minute=Clock.Now.TotalMinutes%1440;if(minute<540||minute>=1020)return "展会开放时间09:00—17:00";
            if(CurrentPlace?.Id!="life_expo")return "请进入云帆会展中心";
            if(_sleeping||IsGameOver)return "当前无法参展";
            if(IsWorking||CityActivityExclusive)return "先结束当前工作或活动";
            if(e.Started>0&&Clock.Now.TotalMinutes<e.Started+30)return "接待正在进行，30分钟后处理下一轮";
            return null;
        }
        public bool ActBrandExpo(string id,string choice)
        {
            if(_brands.Exhibits.Any(x=>x.Id==id&&x.Format==2))return Fail("请进入4天展会现场，处理当前事件与舞台互动");
            string why=BrandExpoBlocker(id);if(why!=null)return Fail(why);
            if(choice!="media"&&choice!="buyers"&&choice!="design")return false;
            var e=_brands.Exhibits.First(x=>x.Id==id);var b=Brand(e.BrandId);
            e.Score+=(choice==e.Focus?30:20)+((b.Position=="value"&&choice=="buyers"||b.Position=="quality"&&choice=="design")?5:0);e.Stage++;e.Started=Clock.Now.TotalMinutes;
            if(e.StaffPersonId!=null&&CityPersonState(e.StaffPersonId)?.LocationPlaceId=="life_expo")e.Score+=5;
            if(e.GuestPersonId!=null&&CityPersonState(e.GuestPersonId)?.LocationPlaceId=="life_expo")e.Score+=5;
            if(_brands.Resources.Any(r=>r.BrandId==b.Id&&r.Mode=="sponsor"&&r.End>BrandDay))e.Score+=5;
            if(e.Stage>=3)
            {
                e.Finished=true;b.Influence+=e.Score/3;b.Reputation=b.Reputation+5;
                _brands.Orders=AppendBrand(_brands.Orders,new BrandOrder{Id=e.Id+":lead",BrandId=b.Id,ShopId=e.ShopId,ProductId=e.ProductId,Source="expo",Count=20,
                    Due=BrandDay+14,Influence=70,UnitCents=BrandProductPrice(e.ProductId)*90/100});
                Say("展会结束：获得采购线索，14天内实际交付20件商品后获得70点品牌知名度。请在品牌订单中履约。");
            }
            return true;
        }
        public bool AssignBrandExpoPerson(string exhibitId,string personId,bool staff)
        {
            var e=_brands.Exhibits.FirstOrDefault(x=>x.Id==exhibitId);
            if(IsGameOver||e==null||e.Day<=BrandDay||e.Finished||e.Missed||CityPersonState(personId)==null)return false;
            if(e.Format==2)return Fail("新版展会在报名时选择已签约KOL，不接受临时未签约嘉宾");
            if(!staff&&!_kol.Contracts.Any(c=>c.PersonId==personId&&c.BrandId==e.BrandId&&KolContractCurrent(c,Clock.Now.TotalMinutes)))return Fail("站台嘉宾必须已与本品牌签约");
            if(staff)
            {if(e.StaffPersonId!=null||!Employees.Any(a=>a.Shop?.Place.Id==e.ShopId&&PersonIdOf(a.Person)==personId))return Fail("只能安排展品来源门店的员工，每场一人");}
            else if(e.GuestPersonId!=null||!new[]{"life_creator","life_designer","life_clothier"}.Contains(personId))return false;
            if(!staff&&(Relation(personId)<20||Employees.Any(a=>PersonIdOf(a.Person)==personId)))return Fail("嘉宾需交情达到20且没有受雇工作安排");
            if(_brands.Exhibits.Any(x=>x.Day==e.Day&&(x.StaffPersonId==personId||x.GuestPersonId==personId)))return Fail("此人当天已有参展安排");
            if(!BrandPay(staff?20000:50000,staff?"参展员工出勤及交通补贴":"嘉宾接待及交通预算",e.BrandId))return Fail("接待预算不足");
            if(staff)e.StaffPersonId=personId;else e.GuestPersonId=personId;
            Meet(personId,0);Say("参展安排已记录，当天06:00起前往会展中心，活动或采购未结束时会晚到。");return true;
        }
        private bool BrandExpoPersonReserved(string id)
        {
            if (Clock.MinuteOfDay < 360 || Clock.MinuteOfDay >= 1020) return false;
            if (_brands.Exhibits.Any(e => BrandDay >= e.Day && BrandDay < e.Day + (e.Format == 2 ? 4 : 1) && !e.Finished && !e.Missed &&
                (e.StaffPersonId == id || e.GuestPersonId == id || (e.Format == 2 && e.KolIds.Contains(id) &&
                    _kol.Contracts.Any(c => c.BrandId == e.BrandId && c.PersonId == id && KolContractCurrent(c, Clock.Now.TotalMinutes))))))
                return true;
            return false;
        }
        private bool TickBrandExpoPerson(CityPersonState person,long now)
        {
            if(!BrandExpoPersonReserved(person.PersonId)||!string.IsNullOrEmpty(person.ActivityId)||!string.IsNullOrEmpty(person.VisitId))return false;
            if(person.LocationPlaceId!="life_expo")
            {person.Route=PlanCityRoute(person,"life_expo",now);if(person.Route==null)return false;person.LocationPlaceId=null;person.Task=CityPersonTask.Travelling;}
            else person.Task=CityPersonTask.Working;
            person.TaskLabel="品牌展会接待";return true;
        }
        public bool FulfilBrandOrder(string id)
        {
            var o=_brands.Orders.FirstOrDefault(x=>x.Id==id);if(IsGameOver||o==null||o.Shipped||o.Done||o.Failed||BrandDay>o.Due)return false;
            if(BrandProductCount(o.ShopId,o.ProductId)<o.Count)return Fail("指定门店现货不足，先安排采购");
            var shop=ShopAt(o.ShopId);var start=StandOf(shop.Place);
            var route=PlanCityRoute(new CityPersonState{X=start.X,Y=start.Y,LocationPlaceId=o.ShopId},o.Source=="expo"?"life_expo":"life_company",Clock.Now.TotalMinutes,transportChoice:VehicleKind.Van);
            if(route==null)return Fail("货车路线暂不可达");
            if(route.ArriveMinute>=(o.Due+1L)*1440)return Fail("按当前运输路线来不及按期交货");
            if(!BrandPay(3000,"企业订单运输 · "+id,o.BrandId))return Fail("需要¥30运费");
            if(!BrandTake(o.ShopId,o.ProductId,o.Count))return false;
            o.Shipped=true;o.Arrival=route.ArriveMinute;
            Say("货车已发出，库存已扣减。预计"+(o.Arrival-Clock.Now.TotalMinutes)+"分钟后到货结算。");return true;
        }
        private void TickBrandDeliveries(long now)
        {
            foreach(var o in _brands.Orders.Where(o=>o.Shipped&&!o.Done&&!o.Failed&&o.Arrival<=now))
            {
                o.Done=true;var b=Brand(o.BrandId);if(b!=null)b.Influence+=o.Influence;
                Ledger.Credit(Clock.Today,"品牌订单",Money.FromCents(o.UnitCents*o.Count),"真实商品交付 · "+BrandProductName(o.ProductId),LedgerAttribution.ForShop(o.ShopId,o.Id,"品牌交付"));
                Say("品牌订单已送达并结算："+BrandProductName(o.ProductId)+" ×"+o.Count);
                RefreshBrandTraffic();
            }
        }
        public bool EnterBrandTender(string brandId,string shopId,string product)
        {
            var b=Brand(brandId);if(IsGameOver||b==null||DistrictAccessTier<2||b.Influence<100)return Fail("进入公园且品牌知名度达到100后可参与企业采购");
            if(!BrandShops(b).Any(s=>s.Place.Id==shopId)||!BrandProducts(shopId).Contains(product))return false;
            int year=BrandDay/360;if(_brands.Tenders.Any(t=>t.BrandId==brandId&&t.Id.EndsWith(":"+year)))return Fail("每个品牌每年一轮供应商席位竞标");
            if(!BrandPay(100000,"投标报名与检验",brandId))return Fail("报名需要¥1,000");
            _brands.Tenders=AppendBrand(_brands.Tenders,new BrandTender{Id=brandId+":tender:"+year,BrandId=brandId,ShopId=shopId,ProductId=product,Deadline=BrandDay+14});return true;
        }
        public bool ResearchBrandTender(string id,int clue)
        {
            var t=_brands.Tenders.FirstOrDefault(x=>x.Id==id);if(IsGameOver||t==null||t.Stage>=2||BrandDay>t.Deadline||(clue!=1&&clue!=2)||(t.Intel&clue)!=0)return false;
            if(!CitySamePlace(clue==1?"life_enterprise":"life_tenant"))return Fail("请找到本人，当面核实采购信息。");
            if(!BrandPay(20000,"采购访谈与供应商核实",t.BrandId))return Fail("访谈需要¥200");
            t.Intel|=clue;Say(clue==1?"沈远：采购方更看重按时交付，低价不是唯一标准。":"袁岚：原供应商会降价应对；保留稳定货源比临时承诺更重要。");return true;
        }
        public bool SubmitBrandTender(string id,string offer,bool partner)
        {
            var t=_brands.Tenders.FirstOrDefault(x=>x.Id==id);if(IsGameOver||t==null||t.Stage!=0||BrandDay>t.Deadline)return false;
            if(offer!="cheap"&&offer!="balanced"&&offer!="service")return false;
            if(partner&&!BrandPay(100000,"联合履约合作费",t.BrandId))return Fail("合作费需要¥1,000");
            t.Offer=offer;t.Partner=partner;t.Stage=1;t.RivalScore=offer=="cheap"?82:76;
            _brands.Orders=AppendBrand(_brands.Orders,new BrandOrder{Id=t.Id+":trial",BrandId=t.BrandId,ShopId=t.ShopId,ProductId=t.ProductId,Source="trial",Count=10,Due=t.Deadline,
                UnitCents=BrandProductPrice(t.ProductId)*(offer=="cheap"?75:offer=="service"?100:90)/100});
            Say("试单已发出。原供应商调整报价，现评分"+t.RivalScore+"。完成试单后可提交最终评审。");return true;
        }
        public bool ReviseBrandTender(string id)
        {
            var t=_brands.Tenders.FirstOrDefault(x=>x.Id==id);
            if(IsGameOver||t==null||t.Stage!=1||t.Partner||BrandDay>t.Deadline)return false;
            if(!BrandPay(100000,"竞标追加联合履约保障",t.BrandId))return Fail("追加合作需要¥1,000");
            t.Partner=true;t.RivalScore+=3;
            Say("袁岚加入履约保障；对手追加服务承诺，评分再增加3分。试单原报价不变。");return true;
        }
        public int BrandTenderScore(BrandTender t) => 40+((t.Intel&1)!=0?10:0)+((t.Intel&2)!=0?10:0)+(t.Partner?15:0)
            +(t.Offer=="service"?10:t.Offer=="cheap"?5:8)+(_brands.Orders.Any(o=>o.Id==t.Id+":trial"&&o.Done)?20:0);
        public bool EvaluateBrandTender(string id)
        {
            var t=_brands.Tenders.FirstOrDefault(x=>x.Id==id);if(IsGameOver||t==null||t.Stage!=1||BrandDay>t.Deadline)return false;
            if(!_brands.Orders.Any(o=>o.Id==t.Id+":trial"&&o.Done))return Fail("先完成真实试单");
            int score=BrandTenderScore(t);bool won=score>t.RivalScore;t.Stage=won?2:4;
            t.Result=$"本品牌 {score} / 原供应商 {t.RivalScore}："+(won?"取得供应席位，进入三期履约":"原供应商留任，明年可再次竞标");
            if(won){t.LastRound=0;AddBrandTenderDelivery(t,0);}Say(t.Result);return true;
        }
        private void AddBrandTenderDelivery(BrandTender t,int round)
        {
            _brands.Orders=AppendBrand(_brands.Orders,new BrandOrder{Id=t.Id+":delivery:"+round,BrandId=t.BrandId,ShopId=t.ShopId,ProductId=t.ProductId,Source="contract",Count=30,Due=BrandDay+30,
                UnitCents=BrandProductPrice(t.ProductId)*(t.Offer=="cheap"?75:t.Offer=="service"?100:90)/100,Influence=20});
        }
        public bool SignBrandResource(string brandId,string institution,string mode)
        {
            var b=Brand(brandId);if(IsGameOver||b==null||DistrictAccessTier<3)return Fail("进入花园后才能接触机构资源");
            if(institution!="media"&&institution!="exhibition"&&institution!="channel")return false;
            if(mode!="talent"&&mode!="sponsor"&&mode!="channel")return false;
            if((institution=="media"&&mode!="talent")||(institution=="exhibition"&&mode!="sponsor")||(institution=="channel"&&mode!="channel"))return false;
            if(_brands.Resources.Any(r=>r.Institution==institution&&r.End>BrandDay&&(r.BrandId==brandId||mode=="channel")))return Fail("资源已有有效合约，独家渠道不能重复签约");
            long fee=mode=="channel"?2000000:mode=="sponsor"?1000000:500000;
            if(!BrandPay(fee,"机构合作 · "+institution,brandId))return Fail("合作预算不足");
            _brands.Resources=AppendBrand(_brands.Resources,new BrandResource{BrandId=brandId,Institution=institution,Mode=mode,Start=BrandDay,End=BrandDay+90,Fee=fee});
            RefreshBrandTraffic();Say("季度合作已签署。渠道与宣传影响客流，销售仍需现货、价格与服务配合。");return true;
        }
        private void TickBrandBusiness(GameDate previous,GameDate today)
        {
            int day=today.TotalDays;if(day<=_brands.LastDay)return;_brands.LastDay=day;
            foreach(string kind in BrandKinds)
            {
                var q=_brands.Qualifications.FirstOrDefault(x=>x.Kind==kind);
                if(q==null){q=new BrandQualification{Kind=kind};_brands.Qualifications=AppendBrand(_brands.Qualifications,q);}
                var shops=MyShops.Where(s=>s.RetailKind==kind&&!s.Sim.NotFitted&&s.RenovationDay<day).ToArray();
                q.LeadingDays=shops.Count(s=>DistrictTierOf(s.Place)==0)>=3&&BrandSuburbRevenue(kind)>=BrandRivalMonthlySales(kind,day)?q.LeadingDays+1:0;
                var community=shops.Where(s=>DistrictTierOf(s.Place)==1).ToArray();
                q.CommunityDays=community.Length>=2&&BrandRevenue(community,day-7,day)>0?q.CommunityDays+1:0;
            }
            foreach(string person in BrandPeople)
            {
                var c=_brands.Careers.FirstOrDefault(x=>x.PersonId==person);if(c==null){c=new BrandCareer{PersonId=person};_brands.Careers=AppendBrand(_brands.Careers,c);}
                int offset=Array.IndexOf(BrandPeople,person)*3;
                if((day+offset)%21==0)
                {
                    int phase=(day/21+offset)%5;c.Severity=0;c.Momentum=phase==2?15:0;c.Day=day;
                    c.News=BrandCareerNews(person,phase);
                    if(_brands.Deals.Any(d=>d.PersonId==person&&!d.Ended&&d.End>day))Say((CityPerson(person)?.Name??person)+"："+c.News);
                }
                foreach(var d in _brands.Deals.Where(d=>d.PersonId==person&&!d.Ended&&d.End>day))
                {if(c.Day>d.NewsDay&&c.Severity==0&&d.Response=="continue")AddRelation(person,2);d.Severity=c.Severity;d.NewsDay=c.Day;}
            }
            if(day%7==0&&_brands.Resources.Any(r=>r.Mode=="talent"&&r.End>day))
                foreach(var c in _creators)c.Resources=Math.Min(100,c.Resources+5);
            foreach(var d in _brands.Deals.Where(d=>!d.Ended&&d.Start<day&&d.End>=day&&d.Mode=="endorse"&&string.IsNullOrEmpty(d.KolContractId)))
            {
                var b=Brand(d.BrandId);if(b==null)continue;
                long fee=BrandRevenue(BrandShops(b),day-1,day)*2/100;
                if(fee>0){Ledger.ForceDebit(today,"代言分成",Money.FromCents(fee),b.Name+"昨日营业额2%",BrandAttribution(b.Id));d.SharePaid+=fee;}
                if(d.Response!="pause")b.Reputation=Math.Max(0,b.Reputation-(d.Response=="negotiate"?Math.Max(0,d.Severity-1):d.Severity)+(d.Severity==0&&day%3==0?1:0));
            }
            SettleQuarterlyExpoAbsence(day);
            foreach(var e in _brands.Exhibits.Where(e=>e.Format!=2&&!e.Finished&&!e.Missed&&e.Day<day)){e.Missed=true;Say("错过展会："+Brand(e.BrandId)?.Name+"，可报名下一季度，费用不退。");}
            TickBrandDeliveries(Clock.Now.TotalMinutes);
            foreach(var o in _brands.Orders.Where(o=>!o.Shipped&&!o.Done&&!o.Failed&&o.Due<day)){o.Failed=true;var b=Brand(o.BrandId);if(b!=null)b.Reputation=Math.Max(0,b.Reputation-10);Say("品牌订单逾期，口碑下降："+BrandProductName(o.ProductId));}
            foreach(var t in _brands.Tenders.Where(t=>t.Stage<4))
            {
                if(t.Stage<2&&t.Deadline<day){t.Stage=4;t.Result="竞标截止，未完成评审";continue;}
                if(t.Stage!=2)continue;
                var o=_brands.Orders.FirstOrDefault(x=>x.Id==t.Id+":delivery:"+t.LastRound);
                if(o?.Failed==true){t.Stage=4;t.Result="交付违约，席位回到原供应商";}
                if(o?.Done==true&&day>=o.Due)
                {if(t.LastRound>=2){t.Stage=3;t.Result="连续三期履约完成，获得企业推荐";}else{t.LastRound++;AddBrandTenderDelivery(t,t.LastRound);}}
            }
            RefreshBrandTraffic();
        }
    }
}
