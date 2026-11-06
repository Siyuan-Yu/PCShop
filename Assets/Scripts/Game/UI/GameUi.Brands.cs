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
        private const int BrandExpoTab = 1, BrandTenderTab = 2, BrandKolTab = 3;
        private int _brandTab;
        private bool _brandCreating;
        private string _brandId, _brandShop, _brandProduct;
        public void OpenBrandBusiness()
        {
            _brandCreating = false;
            OpenWorkspace("brands","brands","品牌",1160,840,BuildBrandBusiness);
        }
        private void BrandButton(Transform parent,string label,Func<bool> action,Window w,bool enabled=true)
        { Ui.Size(Ui.Button(parent,label,()=>{action();S.RefreshDistrictNarrative();w.Rebuild();},17,Ui.ButtonBg,enabled),-1,38,1); }
        private void BuildBrandBusiness(Window w)
        {
            var state=S.BrandBusiness;
            var tabs=Ui.Row(w.Content,40,6);string[] names={"品牌总览","展会","企业竞标","代言人（KOL）"};
            for(int i=0;i<names.Length;i++){int t=i;Ui.Size(Ui.Button(tabs,names[i],()=>{_brandTab=t;_brandCreating=false;_expoPage="overview";w.Rebuild();},18,t==_brandTab?Ui.ButtonHot:Ui.ButtonBg),-1,40,1);}
            if(S.DistrictCommerceActive)Ui.Size(Ui.Button(w.Content,"主线 · "+S.CurrentDistrictNode.Title,OpenDistrictStory,17,Ui.ButtonHot),-1,36);
            var body=Ui.Scroll(w.Content,690);
            if (_brandTab == 0) { BuildBrandOverview(body, w, state); return; }
            if (_brandTab == BrandKolTab) { BuildKolBrandPage(body, w, state.Brands); return; }
            if (state.Brands.Length == 0)
            {
                Para(body, "请先在品牌总览创建品牌。未创立品牌仍可前往会展中心参观。", 19);
                return;
            }
            if(!state.Brands.Any(b=>b.Id==_brandId))_brandId=state.Brands[0].Id;
            var brands=Ui.Row(body,38,5);
            foreach(var item in state.Brands){string id=item.Id;Ui.Size(Ui.Button(brands,item.Name,()=>{_brandId=id;_expoPage="overview";_brandShop=null;_brandProduct=null;w.Rebuild();},18,id==_brandId?Ui.ButtonHot:Ui.ButtonBg),-1,38,1);}
            var brand=state.Brands.First(b=>b.Id==_brandId);
            Line(body,$"{brand.Name}  ·  知名度 {brand.Influence}  ·  口碑 {brand.Reputation}",21,Ui.Accent);
            if(_brandTab==BrandExpoTab)BuildQuarterlyExpo(body,w,brand,state);
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
    }
}
