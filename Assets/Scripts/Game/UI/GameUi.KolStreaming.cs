using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private bool _kolMarketPage;
        private readonly Dictionary<string,int[]> _kolScheduleDrafts=new Dictionary<string,int[]>();
        public void OpenKolManagement(){_brandTab=6;_kolMarketPage=true;OpenBrandBusiness();}
        private static string KolTime(int minute)=>$"{minute/60:00}:{minute%60:00}";
        private static string KolHours(KolLiveContract c)=>KolTime(c.StartMinute)+"—"+(c.EndMinute<c.StartMinute?"次日":"")+KolTime(c.EndMinute);
        private void KolAvatar(Transform parent,string id,float width,float height)
        {
            var portrait=Ui.Rect(parent,"KolPortrait_"+id);Ui.Size(portrait,width,height);
            var raw=portrait.gameObject.AddComponent<RawImage>();raw.texture=PortraitOf(id);
            var button=portrait.gameObject.AddComponent<Button>();button.targetGraphic=raw;button.onClick.AddListener(()=>OpenCityPersonProfile(id));
        }
        private void BuildKolBrandPage(Transform parent,Window w,BrandRecord brand)
        {
            Line(parent,S.BrandLevelName(brand.Id)+" · 直播签约 "+S.BrandKolCount(brand.Id)+" / "+S.BrandKolCapacity(brand.Id),22,Ui.Accent);
            string upgrade=S.KolUpgradeBlocker(brand.Id);
            if(brand.Level<2)
            {
                Ui.Size(Ui.Button(parent,brand.Level==0?"升级中级品牌 · 7名KOL · ¥20,000":"升级高级品牌 · 12名KOL · ¥100,000",()=>
                    ConfirmBusiness("升级品牌",brand.Level==0?"支付¥20,000，名额由3人增至7人。":"支付¥100,000，名额由7人增至12人。",()=>{S.UpgradeKolBrand(brand.Id);w.Rebuild();}),18,Ui.ButtonHot,upgrade==null),-1,40).name="KolBrandUpgrade";
                if(upgrade!=null)Line(parent,upgrade,15,Ui.Dim);
            }
            var nav=Ui.Row(parent,40,8);
            Ui.Size(Ui.Button(nav,"我的KOL与排班",()=>{_kolMarketPage=false;w.Rebuild();},18,!_kolMarketPage?Ui.ButtonHot:Ui.ButtonBg),-1,40,1);
            Ui.Size(Ui.Button(nav,"KOL市场 · 头像与能力对比",()=>{_kolMarketPage=true;w.Rebuild();},18,_kolMarketPage?Ui.ButtonHot:Ui.ButtonBg),-1,40,1);
            if(_kolMarketPage){BuildKolMarket(parent,w,brand);return;}
            var state=S.KolStreaming;var contracts=state.Contracts.Where(c=>c.BrandId==brand.Id&&!c.Ended&&c.EndAt>S.Clock.Now.TotalMinutes).ToArray();
            if(contracts.Length==0){Para(parent,"尚未签约直播KOL。到KOL市场比较头像、魅力、智力和粉丝后选择合作。",19);return;}
            Line(parent,$"正在生效：客流 +{S.KolBrandTrafficBonus(brand.Id)*100:0.#}% · 购买意愿 +{S.KolBrandIntentBonus(brand.Id)*100:0.#}个百分点",18,Ui.Good);
            foreach(var contract in contracts)
            {
                var c=contract;var def=KolMarket.Get(c.PersonId);var card=BusinessCard(parent,S.CityPerson(c.PersonId).Name+" · "+def.Topic);
                var header=Ui.Row(card,112,12);KolAvatar(header,c.PersonId,84,112);
                var info=Ui.Rect(header,"KolContractInfo");Ui.Size(info,-1,112,1);Ui.VList(info,3);
                Line(info,$"魅力 {def.Charm} · 智力 {def.Intelligence} · 粉丝 {S.KolFollowers(c.PersonId):N0} · 人气 {S.KolPopularity(c.PersonId)}",18);
                Line(card,"当前日薪 ¥"+S.KolDailyWage(c.PersonId).ToString("N0")+" · 随粉丝和人气上涨，暂停直播仍计薪",16,Ui.Dim);
                Line(info,S.KolLiveBlocker(c.Id)??("直播中 · 在线 "+S.KolRoomViewers(c.Id)),18,Ui.Accent);
                Line(info,"每日 "+KolHours(c)+" · 合约剩余 "+Math.Max(0,(c.EndAt-S.Clock.Now.TotalMinutes+1439)/1440)+" 天",16,Ui.Dim);
                if(!_kolScheduleDrafts.TryGetValue(c.Id,out var draft)){draft=new[]{c.StartMinute,c.EndMinute,c.Enabled?1:0};_kolScheduleDrafts[c.Id]=draft;}
                var timing=Ui.Row(card,38,8);string[] times=Enumerable.Range(0,96).Select(n=>KolTime(n*15)).ToArray();
                Ui.Size(Ui.Label(timing,"每天",17),48);Ui.Size(Ui.Choice(timing,times,draft[0]/15,n=>draft[0]=n*15),145,38);
                Ui.Size(Ui.Label(timing,"至",17),30);Ui.Size(Ui.Choice(timing,times,draft[1]/15,n=>draft[1]=n*15),145,38);
                Ui.Size(Ui.Button(timing,draft[2]==1?"启用":"暂停",()=>{draft[2]=1-draft[2];w.Rebuild();},17),90,38);
                Ui.Size(Ui.Button(timing,"保存排班",()=>{S.SetKolSchedule(c.Id,draft[0],draft[1],draft[2]==1);w.Rebuild();},17,Ui.ButtonHot),-1,38,1).name="KolSaveSchedule_"+c.Id;
                var actions=Ui.Row(card,40,8);
                Ui.Size(Ui.Button(actions,"进入直播间",()=>{CloseAllWindows();OpenKolLiveRoom(c.Id);},18,Ui.ButtonHot),-1,40,1).name="KolWatch_"+c.Id;
                Ui.Size(Ui.Button(actions,"解除合约…",()=>ConfirmBusiness("解除直播合约","停止此KOL为本品牌直播，释放名额；已付签约费不退。",()=>{S.EndKolLive(c.Id);w.Rebuild();}),17,Ui.Warn),180,40);
            }
            HelpLink(parent,"直播效果与排班","可跨午夜，每日15分钟至12小时。魅力提高观看与引流，智力提高真实商品的购买意愿；行业适配也影响效果。主播正在工作、赶路或赴约时暂缓；门店仍需库存、合适售价与服务才能成交。直播无需玩家观看也会推进。");
        }
        private void BuildKolMarket(Transform parent,Window w,BrandRecord brand)
        {
            Para(parent,"90天独家直播合约：一次签约费＋动态日薪，日结，暂停仍计薪。未知联系人可到社区MCN申请介绍并实际见面。",16,Ui.Dim);
            Ui.Size(Ui.Button(parent,"去社区MCN认识KOL",OpenCommunityMcn,17),-1,36);
            for(int i=0;i<KolMarket.All.Length;i+=2)
            {
                var row=Ui.Row(parent,216,10);
                foreach(var talent in KolMarket.All.Skip(i).Take(2))
                {
                    var d=talent;var card=Ui.Panel(row,"KolMarket_"+d.Id,Ui.PanelBg2).rectTransform;Ui.Size(card,-1,216,1);Ui.VList(card,4,8);
                    var top=Ui.Row(card,128,8);KolAvatar(top,d.Id,84,112);var text=Ui.Rect(top,"TalentInfo");Ui.Size(text,-1,128,1);Ui.VList(text,2);
                    Line(text,S.CityPerson(d.Id).Name,20,Ui.Accent);Line(text,d.Topic,15);
                    Line(text,$"魅力 {d.Charm}　智力 {d.Intelligence}",17);Line(text,$"粉丝 {S.KolFollowers(d.Id):N0} · 人气 {S.KolPopularity(d.Id)}",15,Ui.Dim);
                    string why=S.KolSigningBlocker(brand.Id,d.Id);
                    Ui.Size(Ui.Button(card,"签约 ¥"+S.KolSigningFee(d.Id).ToString("N0")+" · 日薪¥"+S.KolDailyWage(d.Id),()=>ConfirmBusiness("直播签约 · "+S.CityPerson(d.Id).Name,"一次支付¥"+S.KolSigningFee(d.Id)+"，独家直播90天，占用1个名额。当前日薪¥"+S.KolDailyWage(d.Id)+"，随粉丝和人气增加，每日结算；暂停仍计薪，解约结清当日。默认18:00至次日02:00，可独立排班。",()=>{S.SignKolLive(brand.Id,d.Id);w.Rebuild();}),17,Ui.ButtonHot,why==null),-1,36).name="KolSign_"+d.Id;
                    if(why!=null)Line(card,why,13,Ui.Dim);
                }
            }
        }
        public void OpenKolLiveRoom(string contractId)
        {
            if(!S.KolStreaming.Contracts.Any(c=>c.Id==contractId)){Toast("没有这份直播合约。");return;}
            OpenWindow("品牌直播间",1140,820,w=>
            {
                w.LiveConversation=true;w.LiveKey=()=>S.Clock.Now.TotalMinutes+":"+S.Ledger.Revision+":"+S.KolLiveBlocker(contractId);w.LiveStamp=w.LiveKey();
                var state=S.KolStreaming;var c=state.Contracts.First(x=>x.Id==contractId);var d=KolMarket.Get(c.PersonId);
                var brand=S.BrandBusiness.Brands.First(b=>b.Id==c.BrandId);string blocked=S.KolLiveBlocker(c.Id);
                var latest=state.Sessions.Where(s=>s.ContractId==c.Id).OrderByDescending(s=>s.StartedAt).FirstOrDefault();
                Line(w.Content,S.CityPerson(c.PersonId).Name+" · "+brand.Name+"　"+(blocked??("直播中 · 在线 "+S.KolRoomViewers(c.Id).ToString("N0"))),22,blocked==null?Ui.Good:Ui.Dim);
                Line(w.Content,"每日 "+KolHours(c)+$"　粉丝 {S.KolFollowers(c.PersonId):N0} · 人气 {S.KolPopularity(c.PersonId)}",17);
                var room=Ui.Row(w.Content,390,14);var stage=Ui.Panel(room,"KolLiveStage",new Color(.12f,.17f,.23f)).rectTransform;Ui.Size(stage,560,390,0);Ui.VList(stage,8,12);
                Line(stage,d.Topic+" · 模拟直播间",22,Ui.Accent);var stageRow=Ui.Row(stage,294,10);KolAvatar(stageRow,c.PersonId,220,294);
                var words=Ui.Rect(stageRow,"HostExplanation");Ui.Size(words,-1,294,1);Ui.VList(words,8);
                string shop=brand.ShopIds.FirstOrDefault(id=>S.ShopAt(id)!=null);string product=shop==null?null:S.BrandProducts(shop).FirstOrDefault();
                Para(words,blocked??(d.Intelligence>=85?"主播讲解：先说明规格、适用场景和注意事项，再帮观众选择。":"主播讲解：来看看今天的推荐，我把亮点演示给大家。"),19,Ui.Good);
                Para(words,"本场品牌："+brand.Name+"\n介绍商品："+(product==null?"门店暂无现货":S.BrandProductName(product)),18);
                Line(words,"魅力 "+d.Charm+" · 智力 "+d.Intelligence,17,Ui.Accent);
                var chat=Ui.Panel(room,"KolLiveChat",Ui.PanelBg2).rectTransform;Ui.Size(chat,-1,390,1);Ui.VList(chat,6,10);
                Line(chat,"模拟观众弹幕",20,Ui.Accent);
                string[] lines={"这款适合什么使用场景？","主播讲清楚规格我再选。","店里的现货够吗？","这个搭配看起来不错。","想看更具体的使用演示。","我去品牌门店看看。","请介绍一下注意事项。","讲解挺清楚，继续听听。"};
                if(blocked==null)for(int n=0;n<7;n++)Para(chat,"观众"+((S.Clock.Now.TotalMinutes/3+n*73)%900+100)+"："+lines[(int)((S.Clock.Now.TotalMinutes/5+n)%lines.Length)],16);
                else Para(chat,"当前未开播，观众弹幕与引流暂停。",18,Ui.Dim);
                Line(w.Content,$"本KOL：客流 +{S.KolRoomTraffic(c.Id)*100:0.#}% · 购买意愿 +{S.KolRoomIntent(c.Id)*100:0.#}个百分点　品牌合计上限：+90% / +20个百分点",17,Ui.Good);
                Line(w.Content,"本场 / 最近一场：直播 "+(latest?.LiveMinutes??0)+" 分钟 · 峰值在线 "+(latest?.PeakViewers??0)+" · 打赏 "+Money.FromCents(latest?.TipsCents??0),17);
                var gifts=Ui.Row(w.Content,42,8);
                foreach(int value in new[]{10,100,1000})
                {
                    int yuan=value;Ui.Size(Ui.Button(gifts,"打赏 ¥"+yuan,()=>{int before=brand.Influence;if(S.TipKolLive(c.Id,yuan)){int after=S.BrandBusiness.Brands.First(b=>b.Id==brand.Id).Influence;Toast(after>before?"打赏成功，人气提升，品牌影响力也增加了。":"打赏成功，直播间与KOL人气提升。");}w.Rebuild();},18,Ui.ButtonHot,blocked==null&&S.Ledger.Balance>=Money.FromYuan(yuan)),-1,42,1).name="KolGift_"+yuan;
                }
                Line(w.Content,"打赏扣真实现金，每15游戏分钟一次；临时提升观众，增加粉丝/人气，并有概率增加品牌影响力。不是直接销售收入。",15,Ui.Dim);
                var controls=Ui.Row(w.Content,40,8);
                Ui.Size(Ui.Button(controls,S.Time.IsRunning?"暂停时间":"继续时间",()=>{S.Time.TogglePause();w.Rebuild();},17),150,40);
                Ui.Size(Ui.Button(controls,"返回KOL排班",()=>{w.Close();_brandTab=6;_brandId=c.BrandId;_kolMarketPage=false;OpenBrandBusiness();},18),-1,40,1);
            },holds:false);
        }
    }
}
