using System;
using System.Linq;
using PCShopSell.Core.Session;
using PCShopSell.Core.Home;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _brandTab;
        private bool _brandCreating;
        private string _brandId, _brandName="我的品牌", _brandPosition="quality", _brandShop, _brandProduct, _brandFocus="buyers";
        public void OpenBrandBusiness() => OpenWorkspace("business","brands","品牌经营",1160,840,BuildBrandBusiness);
        private void BrandButton(Transform parent,string label,Func<bool> action,Window w,bool enabled=true)
        { Ui.Size(Ui.Button(parent,label,()=>{action();S.RefreshDistrictNarrative();w.Rebuild();},17,Ui.ButtonBg,enabled),-1,38,1); }
        private void BuildBrandBusiness(Window w)
        {
            var state=S.BrandBusiness;
            var tabs=Ui.Row(w.Content,40,6);string[] names={"品牌","推广与代言","展会","企业竞标","机构资源","人物故事","KOL直播"};
            for(int i=0;i<names.Length;i++){int t=i;Ui.Size(Ui.Button(tabs,names[i],()=>{_brandTab=t;w.Rebuild();},18,t==_brandTab?Ui.ButtonHot:Ui.ButtonBg),-1,40,1);}
            if(S.DistrictCommerceActive)Ui.Size(Ui.Button(w.Content,"主线 · "+S.CurrentDistrictNode.Title,OpenDistrictStory,17,Ui.ButtonHot),-1,36);
            var body=Ui.Scroll(w.Content,570);
            if(state.Brands.Length==0||_brandTab==0)
            {
                if(_brandTab==0&&state.Brands.Length>0)
                    Ui.Size(Ui.Button(body,_brandCreating?"收起创立条件":"＋ 创立其他行业品牌",()=>{_brandCreating=!_brandCreating;w.Rebuild();},17),-1,36);
                if(_brandTab==0&&(state.Brands.Length==0||_brandCreating))
                {
                    Line(body,"创立品牌",22,Ui.Accent);
                    Ui.Size(Ui.TextInput(body,_brandName,"品牌名称（2—16字）",v=>_brandName=v),-1,38);
                    var positioning=Ui.Row(body,36,6);
                    foreach(var p in new[]{"value","quality"}){string pos=p;Ui.Size(Ui.Button(positioning,(p=="value"?"实惠定位":"品质定位")+(_brandPosition==p?" ✓":""),()=>{_brandPosition=pos;w.Rebuild();},17),-1,36,1);}
                    foreach(var kind in GameSession.BrandKinds)
                    {
                        if(state.Brands.Any(b=>b.Kind==kind))continue;
                        string k=kind;Line(body,RetailShopTypes.Name(k),20,Ui.Accent);
                        Para(body,S.BrandQualificationText(k),17);
                        Para(body,"同行指标为市场营收估计，用作资格基准。",14,Ui.Dim);
                        string why=S.BrandFoundBlocker(k);BrandButton(body,why??"接受唐蔚邀请 · 创立品牌",()=>S.FoundBrand(k,_brandName,_brandPosition),w,why==null);
                    }
                }
                if(state.Brands.Length==0){if(_brandTab!=0)Para(body,"请先在品牌页创立品牌。未创立品牌仍可前往会展中心参观。",19);return;}
            }
            if(!state.Brands.Any(b=>b.Id==_brandId))_brandId=state.Brands[0].Id;
            var brands=Ui.Row(body,38,5);
            foreach(var item in state.Brands){string id=item.Id;Ui.Size(Ui.Button(brands,item.Name,()=>{_brandId=id;_brandShop=null;_brandProduct=null;w.Rebuild();},18,id==_brandId?Ui.ButtonHot:Ui.ButtonBg),-1,38,1);}
            var brand=state.Brands.First(b=>b.Id==_brandId);
            Line(body,$"{brand.Name}  ·  影响力 {brand.Influence}  ·  口碑 {brand.Reputation}/100",21,Ui.Accent);
            if(_brandTab==6){BuildKolBrandPage(body,w,brand);return;}
            if(_brandTab==0)
            {
                Para(body,brand.Position=="value"?"实惠定位：参展更适合采购洽谈。":"品质定位：参展更适合设计展示。",17);
                foreach(var shop in S.MyShops.Where(s=>s.RetailKind==brand.Kind))
                {string id=shop.Place.Id;BrandButton(body,(brand.ShopIds.Contains(id)?"✓ ":"＋ ")+shop.Place.Name+" · "+GameSession.DistrictName(S.DistrictTierOf(shop.Place)),()=>S.ToggleBrandShop(brand.Id,id),w);}
                return;
            }
            if(_brandTab==5){BuildBrandStories(body,w,brand);return;}
            if(_brandTab==1){BuildBrandPromotion(body,w,brand,state);return;}
            if(_brandTab==4){BuildBrandInstitutions(body,w,brand,state);return;}
            if(_brandTab==2)BuildQuarterlyExpo(body,w,brand,state);
            else
            {
                Para(body,"年度采购席位：调查、试单与真实交付决定评审。",18);
                BuildBrandStockChoice(body,w,brand);
                BrandButton(body,"报名年度竞标 · ¥1,000",()=>S.EnterBrandTender(brand.Id,_brandShop,_brandProduct),w,_brandProduct!=null);
                foreach(var t in state.Tenders.Where(t=>t.BrandId==brand.Id).Reverse().Take(5))
                {
                    Line(body,$"企业采购 · 截止 {new GameDate(t.Deadline)} · 原供应商：{S.BrandRivalName(brand.Kind)}",19,Ui.Accent);
                    if(t.Result!=null)Para(body,t.Result,17);
                    if(t.Stage<2)
                    {
                        var info=Ui.Row(body,38,5);
                        Ui.Size(Ui.Button(info,(t.Intel&1)!=0?"✓ 采购重视交付":"联系沈远 · 核实需求",()=>OpenBrandContact("life_enterprise"),17,Ui.ButtonBg,(t.Intel&1)==0),-1,38,1);
                        Ui.Size(Ui.Button(info,(t.Intel&2)!=0?"✓ 同行将降价应对":"联系袁岚 · 调查对手",()=>OpenBrandContact("life_tenant"),17,Ui.ButtonBg,(t.Intel&2)==0),-1,38,1);
                    }
                    if(t.Stage==0)
                    {
                        Para(body,"报价按报名商品现价锁定：低价75%／均衡90%／服务100%；联合履约另付¥1,000。",16);
                        var offers=Ui.Row(body,38,5);
                        BrandButton(offers,"低价方案",()=>S.SubmitBrandTender(t.Id,"cheap",false),w);
                        BrandButton(offers,"均衡方案",()=>S.SubmitBrandTender(t.Id,"balanced",false),w);
                        BrandButton(offers,"服务＋联合履约",()=>S.SubmitBrandTender(t.Id,"service",true),w);
                    }
                    if(t.Stage==1){Para(body,$"当前评分 {S.BrandTenderScore(t)} / 对手 {t.RivalScore} · 需求10 + 情报10 + 联合15 + 方案 + 试单20",17);if(!t.Partner)BrandButton(body,"回应对手 · 追加联合履约 ¥1,000",()=>S.ReviseBrandTender(t.Id),w);BrandButton(body,"完成试单后 · 提交评审",()=>S.EvaluateBrandTender(t.Id),w);}
                    if(t.Stage==2)Para(body,$"供应席位履约中 · 第{t.LastRound+1}/3期；每期30件，30天内交付。",17,Ui.Good);
                }
            }
            Line(body,"待交付订单",22,Ui.Accent);
            foreach(var o in state.Orders.Where(o=>o.BrandId==brand.Id&&!o.Done&&!o.Failed))
            {
                Line(body,$"{S.BrandProductName(o.ProductId)} ×{o.Count} · 截止 {new GameDate(o.Due)}",18);
                Para(body,$"从 {S.City.GetPlace(o.ShopId).Name} 发货 · 货款 {Money.FromCents(o.UnitCents*o.Count)} · 运费¥30",16);
                if(o.Shipped)Para(body,"运输中 · 剩余约 "+Math.Max(0,o.Arrival-S.Clock.Now.TotalMinutes)+" 分钟，到货后结算",17,Ui.Good);
                else BrandButton(body,"从指定门店发货 · 到货后结算",()=>S.FulfilBrandOrder(o.Id),w);
            }
            Para(body,$"已交付 {state.Orders.Count(o=>o.BrandId==brand.Id&&o.Done)} 单 · 逾期 {state.Orders.Count(o=>o.BrandId==brand.Id&&o.Failed)} 单",16,Ui.Dim);
        }
        private static string BrandFocusLabel(string f)=>f=="buyers"?"采购洽谈":f=="media"?"媒体接待":"设计展示";
        private void OpenBrandContact(string person)=>OpenPersonDialogue(person,remote:!S.CitySamePlace(person)&&S.RemoteConversationBlocker(person)==null);
        private void BuildBrandStockChoice(Transform body,Window w,BrandRecord b)
        {
            var shops=S.MyShops.Where(s=>b.ShopIds.Contains(s.Place.Id)&&s.RetailKind==b.Kind).ToArray();
            if(!shops.Any(s=>s.Place.Id==_brandShop)){_brandShop=shops.FirstOrDefault()?.Place.Id;_brandProduct=null;}
            var row=Ui.Row(body,36,4);foreach(var s in shops){string id=s.Place.Id;Ui.Size(Ui.Button(row,s.Place.Name,()=>{_brandShop=id;_brandProduct=null;w.Rebuild();},16,id==_brandShop?Ui.ButtonHot:Ui.ButtonBg),-1,36,1);}
            var products=S.BrandProducts(_brandShop).ToArray();if(!products.Contains(_brandProduct))_brandProduct=products.FirstOrDefault();
            Line(body,"参选商品 · 仅本店同业现货",17,Ui.Dim);
            if(products.Length==0){Para(body,"这家店暂无同业现货，请先采购。",18);return;}
            var list=Ui.Scroll(body,Math.Min(110,products.Length*38));foreach(string product in products){string p=product;Ui.Size(Ui.Button(list,S.BrandProductName(p)+(_brandProduct==p?" ✓":""),()=>{_brandProduct=p;w.Rebuild();},17),-1,32);}
        }
        private void BuildBrandPromotion(Transform body,Window w,BrandRecord b,BrandBusinessState state)
        {
            Ui.Size(Ui.Button(body,"社区MCN · 认识与联系创作者",OpenCommunityMcn,18),-1,38);
            Para(body,"推广7天；代言90天＋营业额2%。交情可降低报价。暂停不退固定费；提前解约收剩余固定费30%，重大已核实争议免解约费。",17);
            foreach(string person in GameSession.BrandPeople)
            {
                string p=person;Line(body,S.CityPerson(p)?.Name??p,21,Ui.Accent);
                if(S.KolContactBlocker(p)!=null)
                {Para(body,"尚未交换名片，请到社区MCN安排介绍。",17,Ui.Dim);Ui.Size(Ui.Button(body,"请公司安排认识",OpenCommunityMcn,17),-1,36);continue;}
                Ui.Size(Ui.Button(body,"联系本人",()=>OpenBrandContact(p),17),-1,34);
                Para(body,"本行业受众适配 "+(S.BrandAudienceFit(p,b.Kind)*100).ToString("0")+"%",16,Ui.Dim);
                Para(body,state.Careers.FirstOrDefault(c=>c.PersonId==p)?.News??"暂无动态",16);
                var deal=state.Deals.FirstOrDefault(d=>d.BrandId==b.Id&&d.PersonId==p&&!d.Ended&&d.End>S.Clock.Today.TotalDays);
                var row=Ui.Row(body,38,5);
                if(deal==null)
                {
                    BrandButton(row,"推广 "+Money.FromCents(S.BrandDealQuote(p,"campaign",false)),()=>S.SignBrandDeal(b.Id,p,"campaign",false),w);
                    BrandButton(row,"代言 "+Money.FromCents(S.BrandDealQuote(p,"endorse",false)),()=>S.SignBrandDeal(b.Id,p,"endorse",false),w);
                    BrandButton(row,"独家代言 "+Money.FromCents(S.BrandDealQuote(p,"endorse",true)),()=>S.SignBrandDeal(b.Id,p,"endorse",true),w);
                }
                else
                {
                    Para(body,$"到期 {new GameDate(deal.End)} · 风波级别 {deal.Severity}/3 · 已付分成 {Money.FromCents(deal.SharePaid)} · "+(deal.Response=="pause"?"暂停曝光":deal.Response=="negotiate"?"核实协商中":"合作中"),16);
                    BrandButton(row,"继续合作",()=>S.RespondBrandDeal(deal.Id,"continue"),w);
                    BrandButton(row,"暂停曝光",()=>S.RespondBrandDeal(deal.Id,"pause"),w);
                    BrandButton(row,"核实协商 ¥200",()=>S.RespondBrandDeal(deal.Id,"negotiate"),w,deal.Severity>0&&deal.LastNegotiationDay<deal.NewsDay);
                    BrandButton(row,"按约解约",()=>S.RespondBrandDeal(deal.Id,"end"),w);
                }
            }
        }
        private void BuildBrandInstitutions(Transform body,Window w,BrandRecord b,BrandBusinessState state)
        {
            if(S.DistrictAccessTier<3){Para(body,"进入花园后，通过合作方引荐了解机构与渠道关系。",20);return;}
            string[] ids={"media","exhibition","channel"},modes={"talent","sponsor","channel"};
            string[] labels={"澄光传媒 → 莫文宁／柯承安／乔雨桐","云帆展务 → 任帆 → 会展中心","花园联合采购 → 沈远 → 企业渠道"};
            long[] fees={500000,1000000,2000000};
            Para(body,"人物背后有制作、展务和采购渠道。季度资源合作增加触达机会，不能保证销量，也不能直接决定他人声誉。",18);
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i],mode=modes[i];Line(body,labels[i],21,Ui.Accent);
                var r=state.Resources.FirstOrDefault(x=>x.Institution==id&&x.BrandId==b.Id&&x.End>S.Clock.Today.TotalDays);
                if(r!=null)Para(body,"合作至 "+new GameDate(r.End)+" · 已付 "+Money.FromCents(r.Fee),18,Ui.Good);
                else BrandButton(body,(i==0?"支持制作":i==1?"赞助季度展会":"独家渠道合作")+" · 90天 "+Money.FromCents(fees[i]),()=>S.SignBrandResource(b.Id,id,mode),w);
            }
        }
    }
}
