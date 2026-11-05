using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PCShopSell.Core.Session;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _expoBooth;
        private string _expoDraftBrand,_expoDraftShop;
        private readonly HashSet<string> _expoProducts=new HashSet<string>(),_expoKols=new HashSet<string>();
        private Window _quarterlyExpoWindow;
        private void BuildQuarterlyExpoBooking(Transform body,Window w,BrandRecord brand,BrandBusinessState state)
        {
            int day=S.NextBrandExpoDay;
            Line(body,"云帆会展中心 · 每季度一次 · 每届4天",22,Ui.Accent);
            Line(body,new GameDate(day)+"—"+new GameDate(day+3)+" · 每日09:00—17:00 · 展前7天截止报名",17);
            Line(body,"预订本季度摊位",22,Ui.Accent);
            if(_expoDraftBrand!=brand.Id){_expoDraftBrand=brand.Id;_expoProducts.Clear();_expoKols.Clear();_expoDraftShop=null;}
            var sizes=Ui.Row(body,40,6);
            for(int n=0;n<3;n++)
            {int size=n;Ui.Size(Ui.Button(sizes,GameSession.ExpoBoothName(size)+" · ¥"+GameSession.ExpoBoothFee(size).ToString("N0"),()=>{_expoBooth=size;_expoProducts.Clear();w.Rebuild();},17,_expoBooth==size?Ui.ButtonHot:Ui.ButtonBg),-1,40,1);}
            Line(body,"需 "+GameSession.ExpoMinProducts(_expoBooth)+"—"+GameSession.ExpoMaxProducts(_expoBooth)+"种展品，每种100件；至少 "+GameSession.ExpoMinKols(_expoBooth)+"位签约KOL"+(_expoBooth>0?"，另需海报":""),18,Ui.Accent);
            var shops=S.MyShops.Where(s=>s.RetailKind==brand.Kind&&brand.ShopIds.Contains(s.Place.Id)).ToArray();
            if(!shops.Any(s=>s.Place.Id==_expoDraftShop))_expoDraftShop=shops.FirstOrDefault()?.Place.Id;
            var storeRow=Ui.Row(body,36,6);
            foreach(var store in shops){string p=store.Place.Id;Ui.Size(Ui.Button(storeRow,store.Place.Name,()=>{_expoDraftShop=p;_expoProducts.Clear();w.Rebuild();},16,p==_expoDraftShop?Ui.ButtonHot:Ui.ButtonBg),-1,36,1);}
            var goods=S.BrandProducts(_expoDraftShop).ToArray();_expoProducts.RemoveWhere(p=>!goods.Contains(p));
            foreach(string product in goods)
            {
                string p=product;var store=S.ShopAt(_expoDraftShop);int count=PCShopSell.Core.Catalog.PartCatalog.Exists(p)?store.Sim.Stock.Count(PCShopSell.Core.Catalog.PartCatalog.Get(p),true):store.Sim.Stock.GoodsCount(p);
                Ui.Size(Ui.Button(body,(_expoProducts.Contains(p)?"✓ ":"＋ ")+S.BrandProductName(p)+" · 现货"+count+" / 需100",()=>{if(!_expoProducts.Remove(p))_expoProducts.Add(p);w.Rebuild();},17,_expoProducts.Contains(p)?Ui.ButtonHot:Ui.ButtonBg,count>=100||_expoProducts.Contains(p)),-1,34);
            }
            if(goods.Length==0)Para(body,"关联门店暂无同业现货，请先采购准备展品。",18);
            var talents=S.ExpoAvailableKols(brand.Id,day);_expoKols.RemoveWhere(p=>!talents.Contains(p));
            Line(body,"邀请本品牌签约KOL · 报价为4天展会的每日总日酬",18,Ui.Accent);
            for(int n=0;n<talents.Length;n+=3)
            {
                var row=Ui.Row(body,120,8);
                foreach(string person in talents.Skip(n).Take(3))
                {
                    string p=person;var card=Ui.Panel(row,"ExpoKol_"+p,Ui.PanelBg2).rectTransform;Ui.Size(card,-1,120,1);Ui.VList(card,4,6);
                    var info=Ui.Row(card,60,8);KolAvatar(info,p,45,60);var text=Ui.Rect(info,"Name");Ui.Size(text,-1,60,1);Ui.VList(text,3);Line(text,S.CityPerson(p).Name,18);Line(text,"日酬¥"+S.ExpoKolDayQuote(p,_expoBooth).ToString("N0"),16);
                    Ui.Size(Ui.Button(card,_expoKols.Contains(p)?"✓ 已邀请":"邀请站台",()=>{if(!_expoKols.Remove(p))_expoKols.Add(p);w.Rebuild();},16,_expoKols.Contains(p)?Ui.ButtonHot:Ui.ButtonBg),-1,32);
                }
            }
            if(talents.Length==0)Para(body,"没有合约覆盖展期的签约KOL，请先在KOL直播页签约。",17,Ui.Warn);
            Ui.Size(Ui.Button(body,"管理KOL签约",()=>{_brandTab=BrandKolTab;w.Rebuild();},17),-1,36);
            long dayWages=_expoKols.Sum(p=>S.ExpoKolDayQuote(p,_expoBooth));
            Line(body,"报名付款 ¥"+GameSession.ExpoBoothFee(_expoBooth).ToString("N0")+" · 海报 ¥"+GameSession.ExpoPosterFee(_expoBooth).ToString("N0")+" · 4天KOL总日酬约¥"+(dayWages*4).ToString("N0"),17,Ui.Warn);
            string reason=S.ExpoBookingBlocker(brand.Id,_expoDraftShop,_expoBooth,_expoProducts.ToArray(),_expoKols.ToArray());
            if(reason!=null)Line(body,reason,16,Ui.Dim);
            Ui.Size(Ui.Button(body,"确认预订摊位…",()=>ConfirmBusiness("预订4天展会",GameSession.ExpoBoothName(_expoBooth)+"支付¥"+GameSession.ExpoBoothFee(_expoBooth)+"；所选每种展品100件转交展务，未售库存闭展退回；邀请"+_expoKols.Count+"位已签约KOL，4天总日酬约¥"+(dayWages*4)+"，按天结算。海报另行委托，开幕后需亲自连续参与4天。",()=>{if(S.BookQuarterlyExpo(brand.Id,_expoDraftShop,_expoBooth,_expoProducts.ToArray(),_expoKols.ToArray())) _expoPage="overview";w.Rebuild();}),18,Ui.ButtonHot,reason==null),-1,44).name="ExpoBookBooth";
        }
        private void BuildLegacyExpoCard(Transform card,Window w,BrandExhibit e)
        {
            if(e.Finished||e.Missed){Line(card,e.Finished?"已结束":"已错过",17);return;}
            Line(card,"旧存档报名按原接待规则完成；新报名统一采用4天摊位制。",16,Ui.Dim);
            string why=S.BrandExpoBlocker(e.Id);if(why!=null)Line(card,why,16);
            var row=Ui.Row(card,38,6);foreach(string option in new[]{"buyers","media","design"}){string c=option;BrandButton(row,BrandFocusLabel(c),()=>S.ActBrandExpo(e.Id,c),w,why==null);}
        }
        public bool ExpoHudOpen=>_quarterlyExpoWindow!=null&&!_quarterlyExpoWindow.Closed;
        private void ExpoSidePanel(Window w)
        {
            w.Root.name="ExpoSceneHud";
            var dim=w.Root.GetComponent<Image>();dim.color=Color.clear;dim.raycastTarget=false;
            w.Fitter.enabled=false;
            var panel=(RectTransform)w.Root.Find("Panel");panel.localScale=Vector3.one;
            panel.anchorMin=new Vector2(.68f,.10f);panel.anchorMax=new Vector2(.995f,.91f);
            panel.offsetMin=new Vector2(8,0);panel.offsetMax=Vector2.zero;
            w.TitleText.rectTransform.offsetMin=new Vector2(16,0);w.TitleText.rectTransform.offsetMax=new Vector2(-12,0);
            w.TimeStatusText.gameObject.SetActive(false);
        }
        public void OpenQuarterlyExpo(string id=null)
        {
            if(S.CurrentPlace?.Id!="life_expo"||!S.CityExpoOpen(false)){Toast("请在展期亲自前往云帆会展中心。");return;}
            var selected=S.ExpoAtVenue;
            if(id!=null&&(selected==null||selected.Id!=id)){Toast("这届展会当前不在展期内。");return;}
            id=selected?.Id;
            if(_quarterlyExpoWindow!=null&&!_quarterlyExpoWindow.Closed)return;
            _quarterlyExpoWindow=OpenWindow("会展中心 · 现场",600,860,w=>
            {
                ExpoSidePanel(w);
                var e=S.BrandBusiness.Exhibits.FirstOrDefault(x=>x.Id==id);
                w.Closable=e?.Attending!=true;
                var nav=Ui.Row(w.Content,38,4);
                string[] zones={"overview","booth","stage","reception"},names={"总览","展台","舞台","接待"};
                for(int n=0;n<zones.Length;n++){string zone=zones[n];Ui.Size(Ui.Button(nav,names[n],()=>_ctrl.FocusExpo(zone),17),-1,38,1);}
                Line(w.Content,"点选场景切换区域 · 滚轮缩放 · 右拖旋转",15,Ui.Dim);
                var body=w.Content;
                if (e != null) BuildExpoEndorsers(body, e, true);
                if(e==null)
                {
                    Line(body,S.BrandExpoTheme(S.Clock.Today.TotalDays),23,Ui.Accent);
                    var legacy=S.BrandBusiness.Exhibits.FirstOrDefault(x=>x.Format!=2&&x.Day==S.Clock.Today.TotalDays&&!x.Finished&&!x.Missed);
                    if(legacy!=null)
                    {
                        Para(body,"本届保留旧存档已支付的参展安排，可继续原接待流程。",19);
                        Ui.Size(Ui.Button(body,"继续原报名接待",()=>{w.Close();_brandTab=2;OpenBrandBusiness();},18),-1,44);
                    }
                    else Para(body,"本届未报名，可以参观展厅。下一届展位在“品牌 → 展会”筹备。",19);
                }
                else if(e.Attending)
                {
                    Line(body,"第"+Math.Min(4,e.Decision/6+1)+" / 4天 · "+S.Clock.Now,20,Ui.Accent);
                    Line(body,"热度 "+e.Heat+" · 信任 "+e.Trust+" · 已售 "+e.Products.Sum(p=>p.Sold)+"件",19,Ui.Good);
                    var moment=S.CurrentExpoMoment(e.Id);Line(body,moment.Title,24,Ui.Accent);Para(body,moment.Prompt,22);
                    for(int n=0;n<3;n++){int choice=n;Ui.Size(Ui.Button(body,moment.Options[n],()=>{S.ChooseExpoAction(e.Id,choice);w.Rebuild();},20,Ui.ButtonHot),-1,68).name="ExpoChoice_"+n;}
                    if(e.LastFeedback!=null)Para(body,e.LastFeedback,17,Ui.Good);
                    Line(body,"现场营收 "+Money.FromCents(e.RevenueCents)+" · 今日第"+(e.Decision%6+1)+" / 6段",17);
                    Line(body,"选定后推进现场；夜间自动休息，可随时保存。",15,Ui.Dim);
                }
                else if(e.Finished||e.Missed)
                {
                    Line(body,e.Finished?"本届参展结束":"本届未完成参展",23,Ui.Accent);
                    foreach(var p in e.Products)Line(body,S.BrandProductName(p.ProductId)+" · 售出"+p.Sold+" / 100 · 退回"+(100-p.Sold),18);
                    Line(body,"现场营收 "+Money.FromCents(e.RevenueCents),19,Ui.Good);
                    Ui.Size(Ui.Button(body,"查看采购订单",()=>{w.Close();_brandTab=2;OpenBrandBusiness();},18),-1,40);
                }
                else
                {
                    Line(body,GameSession.ExpoBoothName(e.Booth)+" · 参展报到",23,Ui.Accent);
                    Line(body,e.Products.Length+"种展品 · "+e.Products.Sum(p=>p.Reserved)+"件备货",19);
                    int arrived=e.KolIds.Count(person=>{var p=S.CityPersonState(person);return p!=null&&p.LocationPlaceId=="life_expo"&&p.Route==null&&string.IsNullOrEmpty(p.ActivityId);});
                    Line(body,"KOL已到场 "+arrived+" / "+e.KolIds.Length,19,Ui.Good);
                    var why=S.ExpoStartBlocker(e.Id);if(why!=null)Para(body,why,18);
                    Ui.Size(Ui.Button(body,"报到并开始4天参展…",()=>ConfirmBusiness("连续4天参展","报到后留在会场处理现场事件，夜间安排休息。门店照常经营，KOL日薪和站台费正常结算，可保存后继续。",()=>{if(S.StartQuarterlyExpo(e.Id)){CloseAllWindows();OpenQuarterlyExpo(e.Id);}else w.Rebuild();}),19,Ui.ButtonHot,why==null),-1,48).name="ExpoOnsiteStart";
                    if(why!=null&&S.Clock.MinuteOfDay<600)Ui.Size(Ui.Button(body,"在会场等10分钟",()=>{S.AdvanceClock(10);w.Rebuild();},18),-1,42);
                }
                if(e!=null&&e.Journal.Length>0)HelpLink(body,"本届现场记录",string.Join("\n",e.Journal));
                if(e?.Attending==true)Ui.Size(Ui.Button(body,"保存现场进度",()=>Toast(_ctrl.SaveGame()?"现场进度已保存，下次从当前事件继续。":"请先处理待选择事件。"),18),-1,40);
                else Ui.Size(Ui.Button(body,"离开展馆",()=>{w.Close();S.Leave();},18,Ui.Warn),-1,42);
            },holds:true,closable:false);
            _quarterlyExpoWindow.LiveConversation=true;
            _quarterlyExpoWindow.LiveKey=()=>S.Clock.Now.TotalMinutes+":"+string.Join(",",S.ExpoAtVenue?.KolIds.Select(p=>S.CityPersonState(p)?.LocationPlaceId)??System.Array.Empty<string>());
        }
        private void EnsureQuarterlyExpoWindow()
        {
            if(ExpoHudOpen&&(S.CurrentPlace?.Id!="life_expo"||!S.CityExpoOpen(false)))_quarterlyExpoWindow.Close();
            if(S.BrandExpoExclusive&&(_quarterlyExpoWindow==null||_quarterlyExpoWindow.Closed))
            {CloseAllWindows();OpenQuarterlyExpo(S.ActiveBrandExpo.Id);}
        }
    }
}
