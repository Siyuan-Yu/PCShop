using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _businessTab, _directQuantity=3, _ventureKind;
        public void DebugOpenBusiness(int tab) { _businessTab = Math.Max(0, Math.Min(6, tab)); ShowBusinessPanel(false); }
        private bool _directCredit=true;
        private static readonly int[] AgencyTabs = { 3, 5, 6 };

        /// <summary>后期经营（直供、合同、分店、物业、园区……）开局不出现，主店做起来以后才开放。</summary>
        public bool BusinessUnlocked
        {
            get
            {
                if (S == null || !S.HasShop) return false;
                QuestDef q = _questSeen != null ? Quests.Get(_questSeen) : null;
                return S.PlayerShop.Reputation >= 8 || S.QuestCompleted("cashflow") || S.Flags.Contains("tutorial:issued:cashflow")
                    || S.TotalDebt.IsPositive || S.ManagedVentures.Count > 0 || S.OwnedProperties.Count > 0 || S.HasPark
                    || S.HomeTier > 0 || S.DirectAgreement || (q != null && BusinessTargets.Contains(q.Target));
            }
        }

        public void OpenBusinessPanel()
        {
            if (!BusinessUnlocked) return;
            ShowBusinessPanel(false);
        }

        /// <summary>中介里只办和房产有关的事：分店签约、物业与住处、园区。</summary>
        public void OpenAgencyBusiness()
        {
            if (!BusinessUnlocked) return;
            if (Array.IndexOf(AgencyTabs, _businessTab) < 0) _businessTab = AgencyTabs[0];
            ShowBusinessPanel(true);
        }

        private void ShowBusinessPanel(bool agencyOnly)
        {
            S.RefreshEnterpriseOffer();
            OpenWindow(agencyOnly ? "中介业务" : "经营总览", 1080, 820, w=>
            {
                var guide = S.CurrentQuest();
                var tabs=Ui.Row(w.Content,42);
                string[] names={"资产与负债","直供采购","企业合同","分店经营","推广合作","物业与住处","园区布局"};
                for(int i=0;i<names.Length;i++)
                {
                    int tab=i;
                    bool recommended = guide != null && guide.Target == BusinessTargets[i];
                    if (agencyOnly && Array.IndexOf(AgencyTabs, tab) < 0) continue;
                    if (tab != _businessTab && !recommended && !BusinessTabRelevant(tab)) continue;
                    Ui.Size(Ui.Button(tabs,(recommended ? "› " : "") + names[i],()=>{_businessTab=tab;w.Rebuild();ResetGuideScroll(w);},17,tab==_businessTab ? Ui.ButtonHot : Ui.ButtonBg),140);
                }
                Line(w.Content,$"可用现金 {S.Ledger.Balance}     净资产 {S.NetWorth()}     已有负债 {S.TotalDebt}",20,Ui.Accent);
                GuideHint(w.Content, BusinessTargets[_businessTab], w.Rebuild);
                switch(_businessTab)
                {
                    case 0: BuildBusinessFinance(w);break;
                    case 1: BuildDirectSupply(w);break;
                    case 2: BuildEnterprise(w);break;
                    case 3: BuildVentures(w);break;
                    case 4: BuildCreators(w);break;
                    case 5: BuildPropertyPortfolio(w);break;
                    case 6: BuildPark(w);break;
                }
            });
        }
        private bool BusinessTabRelevant(int tab)
        {
            switch (tab)
            {
                case 0: return true;
                case 1: return S.DirectAgreement || (S.HasShop && S.PlayerShop.Reputation >= 10);
                case 2: return S.EnterpriseContracts.Any(c => c.Accepted) || (S.HasShop && S.PlayerShop.Reputation >= 8);
                case 3: return S.ManagedVentures.Count > 0 || (S.HasShop && S.PlayerShop.Reputation >= 8);
                case 4: return S.CreatorPartners.Any(c => c.CampaignUntilDay > 0) || (S.HasShop && S.PlayerShop.Reputation >= 8);
                case 5: return true;
                case 6: return S.HasPark || (S.HasShop && S.PlayerShop.Reputation >= 25);
                default: return false;
            }
        }
        private RectTransform BusinessCard(Transform parent,string title)
        {
            var card=Ui.Panel(parent,"BusinessCard",Ui.PanelBg2).rectTransform;
            Ui.VList(card,8,16);
            Line(card,title,20,Ui.Accent);
            return card;
        }
        private void BusinessAction(Transform parent,string label,Func<bool> action,Window w,bool enabled=true)
        { Ui.Size(Ui.Button(parent,label,()=>{action();w.Rebuild();},17,Ui.ButtonHot,enabled),-1,40,1); }
        private void ConfirmBusiness(string title,string terms,Action action)
        {
            OpenWindow(title,720,420,confirm=>
            {
                Para(confirm.Content,terms,18);
                var row=Ui.Row(confirm.Content,44);
                Ui.Size(Ui.Button(row,"确定办理",()=>{confirm.Close();action();},18,Ui.Warn),-1,-1,1);
                Ui.Size(Ui.Button(row,"返回",confirm.Close,18),-1,-1,1);
            });
        }
        private void AgencyLink(Window w)
        {
            if(S.InAgency) {Line(w.Content,"当前在房产中介，可直接签约。",16,Ui.Good);return;}
            Para(w.Content,"开设分店、买卖物业和更换住处需当面签约；经营管理可随时查看。",16,Ui.Dim);
            Ui.Size(Ui.Button(w.Content,"前往房产中介",()=>{CloseAllWindows();_ctrl.GoToPlace(S.City.FirstOfKind(PlaceKind.Agency));},17),-1,38);
        }
        private void BuildBusinessFinance(Window w)
        {
            var c=BusinessCard(w.Content,"每一笔钱放在哪里");
            Line(c,$"主店库存及在途（市价九折） {S.InventoryValue()}    载具残值（购价六折） {S.VehicleValue}",17);
            Line(c,$"主店押金 {S.Lease?.Deposit ?? Money.Zero}    企业合同保证金 {S.ContractDeposits}",17);
            Line(c,$"分店资产 {S.VentureValue}（其中周转金 {S.VentureReserve}）    指数持仓 {S.FundValue}",17);
            Line(c,$"商用物业账面值 {S.PropertyValue}    自住房账面值 {Money.FromCents(S.HomePriceCents)}",17);
            Line(c,$"待付货款 {S.SupplierDebtTotal}    剩余房贷本金 {S.MortgageDebt}",17,new Color(.84f,.60f,.50f));
            Para(c,"净资产 = 现金 + 库存 + 押金 + 载具残值 + 分店清算估值 + 物业买入价 + 指数市值 − 负债。物业出售按九折成交；尚未发生的利息不计入当前本金。分店收支留在自己的周转金中，转回总部才增加可用现金。",15,Ui.Dim);
            Line(w.Content,$"未来 7 天货款、月供与物业维护 {S.UpcomingBusinessPayments(7)} · 自动补货当前保留额 {S.EffectiveRestockReserve}",16,Ui.Accent);
            var row=Ui.Row(w.Content,40);
            Ui.Size(Ui.Button(row,"三年目标与人脉",OpenGoalsPanel,17),-1,-1,1);
            Ui.Size(Ui.Button(row,"主店现金收支报表",OpenReportWindow,17),-1,-1,1);
            Ui.Size(Ui.Button(row,"查看账本",OpenLedger,17),-1,-1,1);
            var f=BusinessCard(w.Content,"硬件景气指数");
            Line(f,$"现价 {S.FundUnitPrice}/份    持有 {S.FundUnits} 份    市值 {S.FundValue}",18);
            Para(f,"价格跟随所有硬件型号的平均市场涨跌，无保底收益。买卖各收 0.5% 手续费，交易 10 份一手。留足进货、房租和到期账款再投资。",15,Ui.Dim);
            var actions=Ui.Row(f,40);
            BusinessAction(actions,$"买入 10 份 · { (S.FundUnitPrice*10).Scale(1.005)}",()=>S.TradeFund(10,true),w,S.HasSmartphone && S.Ledger.Balance>=(S.FundUnitPrice*10).Scale(1.005));
            BusinessAction(actions,$"卖出 10 份 · 到手 {(S.FundUnitPrice*10).Scale(.995)}",()=>S.TradeFund(10,false),w,S.HasSmartphone && S.FundUnits>=10);
            if(S.SupplierDebts.Count>0) BuildDebtList(w);
        }
        private void BuildDebtList(Window w)
        {
            var c=BusinessCard(w.Content,"直供账款 · 到期有足额现金时自动结清");
            foreach(var debt in S.SupplierDebts)
            {
                var d=debt;var row=Ui.Row(c,42);
                int left=d.DueDay-S.Clock.Today.TotalDays;
                Ui.Size(Ui.Label(row,$"#{d.Id}  {d.Amount}  · {(left>0 ? left+" 天后到期" : "已到期")}  · 含逾期费 {Money.FromCents(d.Fees)}",16),680);
                BusinessAction(row,"立即结清",()=>S.RepaySupplier(d.Id),w,S.Ledger.Balance>=d.Amount);
            }
            Para(c,"逾期暂停直供，日费为本金 0.1%、累计最多 10%；超过 30 天强制扣款并解除直供。",15,Ui.Dim);
        }
        private void BuildDirectSupply(Window w)
        {
            Para(w.Content,"老周牵线的工厂直供：市场价 84%，每种 3–30 件，次日 10:00 送到主店。可全款，也可 30% 首付、70% 尾款 14 天结清。直供承诺合格交付，不抽次品。",17);
            if(!S.DirectAgreement)
            {
                string why=S.DirectAgreementBlocker;
                Line(w.Content,why??"条件已满足，签约无需费用。",17,why==null ? Ui.Good : Ui.Dim);
                BusinessAction(w.Content,"签订直供协议",S.SignDirectAgreement,w,why==null);return;
            }
            Line(w.Content,$"信用额度 {S.CreditLimit}    已用 {S.SupplierDebtTotal}    仓位已预留在途 {S.IncomingShopSpace:0.#} 格",17);
            var settings=Ui.Row(w.Content,40);
            Ui.Size(Ui.Button(settings,$"每批 {_directQuantity} 件 · 点击切换",()=>{_directQuantity=_directQuantity==3 ? 5 : _directQuantity==5 ? 10 : _directQuantity==10 ? 20 : _directQuantity==20 ? 30 : 3;w.Rebuild();},17),340);
            Ui.Size(Ui.Button(settings,_directCredit ? "付款：30% 首付 + 14 天账期" : "付款：全款",()=>{_directCredit=!_directCredit;w.Rebuild();},17),440);
            foreach(var p in PartCatalog.All)
            {
                var part=p;string why=S.DirectOrderBlocker(p.Id,_directQuantity,_directCredit);
                Money total=S.DirectPrice(p)*_directQuantity;
                var row=Ui.Row(w.Content,50);
                Ui.Size(Ui.Label(row,$"{p.Name}\n<size=14>单价 {S.DirectPrice(p)} · 本批合计 {total}</size>",17),470);
                Ui.Size(Ui.Label(row,why??$"现付 {(_directCredit ? total.Scale(.3) : total)}",15,TextAnchor.MiddleLeft,why==null ? Ui.Good : Ui.Dim),340);
                BusinessAction(row,"下单",()=>S.OrderDirect(part.Id,_directQuantity,_directCredit),w,why==null);
            }
            foreach(var x in S.DirectShipments) Line(w.Content,$"在途：{PartCatalog.Get(x.PartId).Name} ×{x.Count} · {new GameTimestamp(x.Arrival)}",15,Ui.Dim);
            if(S.SupplierDebts.Count>0) BuildDebtList(w);
        }
        private void BuildEnterprise(Window w)
        {
            Para(w.Content,"知名度 8 解锁，每周刷新一份采购邀请。接受后自动进入主店装机队列；可以亲自装，也可以交给受训员工。逐台交付收款，全额完成再奖合同额 3%。",17);
            if(S.EnterpriseContracts.Count==0) Line(w.Content,"暂时没有采购邀请。提高知名度后再来看看。",17,Ui.Dim);
            foreach(var contract in S.EnterpriseContracts.Reverse())
            {
                var c=contract;var card=BusinessCard(w.Content,c.Client+" · "+PartCatalog.TierName(c.Tier)+"整机 ×"+c.Count);
                Line(card,$"合同额 {c.Total}    单台收款 {Money.FromCents(c.UnitCents)}    履约保证金 {c.Total.Scale(.05)}",17);
                Money reference=Money.Zero;foreach(var p in PartCatalog.BuildList(c.Tier)) reference+=S.Market.PriceOf(p)*c.Count;
                Para(card,$"当前配件市价合计 {reference}（不是实际采购成本）。交期 {c.Count+2} 天；未交付部分会扣对应保证金，手头零售订单也会占用工时。",15,Ui.Dim);
                if(c.Accepted)
                {
                    Line(card,$"交付 {c.Completed}/{c.Count}    失败 {c.Failed}    {(c.Settled ? "已结案" : "截止 "+new GameTimestamp(c.DueMinute))}",17,c.Settled ? Ui.Dim : Ui.Good);
                    if(!c.Settled) Ui.Size(Ui.Button(card,"终止剩余订单…",()=>ConfirmBusiness("终止企业订单", "尚未交付的订单会取消，已领用配件返回散件库存，未履约部分保证金不退。",()=>{S.CancelEnterprise(c.Id);w.Rebuild();}),16,Ui.Warn),-1,36);
                }
                else
                {
                    string why=S.EnterpriseBlocker(c);
                    Line(card,why??$"邀请有效至第 {c.OfferUntilDay+1} 天，接受时开始计算交期。",16,why==null ? Ui.Good : Ui.Dim);
                    BusinessAction(card,"接受合同并缴纳保证金",()=>S.AcceptEnterprise(c.Id),w,why==null);
                }
            }
        }
        private void BuildVentures(Window w)
        {
            Para(w.Content,"主店保留亲自经营，其余铺面由店长托管。每日结算记入各店周转金；定价、位置、周末和维护状态影响销量。停业仍付租金及 20% 留任工资；周转金不足的固定费用由总部补足。",16);
            foreach(var venture in S.ManagedVentures)
            {
                var v=venture;var d=Ventures.Get(v.Kind);var c=BusinessCard(w.Content,d.Name+" · "+S.VentureLocationName(v)+(v.Paused ? "  [停业]" : "  [营业]"));
                Line(c,$"周转金 {Money.FromCents(v.ReserveCents)}   昨日收入 {Money.FromCents(v.LastSalesCents)} / 成本 {Money.FromCents(v.LastCostsCents)}",17);
                Line(c,$"累计经营结余 {Money.FromCents(v.TotalOperatingCents)}    定价 {v.PriceFactor:P0}    维护状态 {v.Condition:P0}",17);
                Para(c,$"月租 {Money.FromCents(v.RentCents)}，首 30 天已付（自有物业免租）。{d.Description}",15,Ui.Dim);
                var row=Ui.Row(c,40);
                BusinessAction(row,"补资 ¥5,000",()=>S.TransferVentureCash(v.PlaceId,5000,false),w,S.Ledger.Balance>=Money.FromYuan(5000));
                BusinessAction(row,"取回 ¥1,000",()=>S.TransferVentureCash(v.PlaceId,1000,true),w,v.ReserveCents>=100000);
                BusinessAction(row,v.Paused ? "恢复营业" : "暂停营业",()=>S.ToggleVenture(v.PlaceId),w);
                var controls=Ui.Row(c,40);
                BusinessAction(controls,"定价 −10%",()=>S.SetVenturePrice(v.PlaceId,Math.Max(.8,v.PriceFactor-.1)),w,v.PriceFactor>.8);
                BusinessAction(controls,"定价 +10%",()=>S.SetVenturePrice(v.PlaceId,Math.Min(1.3,v.PriceFactor+.1)),w,v.PriceFactor<1.3);
                BusinessAction(controls,$"维护 · {S.VentureRepairCost(v)}",()=>S.MaintainVenture(v.PlaceId),w,v.Condition<.999 && S.Ledger.Balance>=S.VentureRepairCost(v));
                Ui.Size(Ui.Button(c,$"关闭并清算…  预计回收 {S.VentureExitValue(v)}",()=>ConfirmBusiness("关闭门店", "清退员工，设备与装修按 30% × 维护状态回收，退还押金与周转金。自有物业保留并交由中介出租。",()=>{S.CloseVenture(v.PlaceId);w.Rebuild();}),16,Ui.Warn),-1,36);
            }
            AgencyLink(w);
            var chosen=Ventures.All[_ventureKind];var choose=Ui.Row(w.Content,42);
            Ui.Size(Ui.Button(choose,"上一业态",()=>{_ventureKind=(_ventureKind+Ventures.All.Length-1)%Ventures.All.Length;w.Rebuild();},17),150);
            Ui.Size(Ui.Label(choose,chosen.Name,21,TextAnchor.MiddleCenter,Ui.Accent),620);
            Ui.Size(Ui.Button(choose,"下一业态",()=>{_ventureKind=(_ventureKind+1)%Ventures.All.Length;w.Rebuild();},17),150);
            Para(w.Content,$"{chosen.Description} 装修设备 {Money.FromYuan(chosen.Capital)}，员工日薪 {Money.FromYuan(chosen.DailyWages)}，知名度要求 {chosen.MinReputation:0}。另需租赁签约款及 5,000 周转金。",16);
            if (!S.Listings.Any(x=>x.OpenDay<=S.Clock.Today.TotalDays)) Para(w.Content,"当前没有可签约的空铺。中介会陆续挂出新房源；也可等已挂牌铺面的现租户到期。",16,Ui.Dim);
            foreach(var listing in S.Listings.Where(x=>x.OpenDay<=S.Clock.Today.TotalDays))
            {
                var l=listing;string why=S.VentureBlocker(l.PlaceId,chosen.Id);var c=BusinessCard(w.Content,S.City.GetPlace(l.PlaceId).Name+" · "+GameSession.SizeName(l.Size));
                Line(c,$"月租 {l.Rent}    开办现金合计 {S.VentureOpeningCost(l,chosen)}",17);
                if(why!=null) Para(c,why,15,Ui.Dim);
                Ui.Size(Ui.Button(c,"在这里开设"+chosen.Name+"…",()=>ConfirmBusiness("开设"+chosen.Name,$"支付 {S.VentureOpeningCost(l,chosen)}。"+(chosen.HardwareCount>0 ? $"另从主店调拨 {chosen.HardwareCount} 套{PartCatalog.TierName(chosen.HardwareTier)}配件，不会返还成散件。" : "")+"每日结算从次日开始。",()=>{S.OpenVenture(l.PlaceId,chosen.Id);w.Rebuild();}),17,Ui.ButtonHot,S.InAgency && why==null),-1,40);
            }
        }
        private void BuildCreators(Window w)
        {
            BuildMcnHeader(w);
            Para(w.Content,"主店推广同一时间只合作一位创作者，MCN 合约可并行。粉丝量、创作资源影响导流；合作销售表现影响粉丝与关系。高抱负创作者在连续表现不佳时会提前结束合作，不退费用。",16);
            foreach(var creator in S.CreatorPartners)
            {
                var c=creator;var d=Creators.Get(c.Id);var card=BusinessCard(w.Content,d.Name+" · "+d.Style);
                Ui.Size(Ui.Button(card,"合作合约 / 私人相处",()=>OpenCreatorRelations(c.Id,w),17),-1,36);
                Line(card,$"粉丝 {c.Followers:N0}   交情 {c.Relationship}/100   抱负 {c.Ambition}/100   资源 {c.Resources}/100",17);
                if(c.CampaignUntilDay>S.Clock.Today.TotalDays) Line(card,$"推广剩余 {c.CampaignUntilDay-S.Clock.Today.TotalDays} 天   主店客流 ×{S.CreatorTrafficMultiplier:0.00}",18,Ui.Good);
                Para(card,$"本次合作期间主店营业额 {Money.FromCents(c.AttributedSalesCents)}，包含自然客流；不能据此判断推广净增收入或利润。",15,Ui.Dim);
                string why=S.CreatorCampaignBlocker(c);if(why!=null) Line(card,why,15,Ui.Dim);
                var row=Ui.Row(card,40);
                BusinessAction(row,$"合作七天 · {S.CreatorFee(c)}",()=>S.StartCreatorCampaign(c.Id),w,why==null);
                BusinessAction(row,"设备制作赞助 · ¥500",()=>S.SupportCreator(c.Id),w,S.HasSmartphone && S.Ledger.Balance>=Money.FromYuan(500) && c.LastSupportDay!=S.Clock.Today.TotalDays);
                Para(card,"赞助每日一次：资源 +30、交情 +5。推广日资源 −4；日营业额达到 ¥3,000 提升交情和粉丝，否则回落。",14,Ui.Dim);
            }
        }
        private void BuildPropertyPortfolio(Window w)
        {
            AgencyLink(w);
            Para(w.Content,"商用物业售价 = 签约月租 ×120。可全款或首付 30%，贷款 36 期等额本金、月利率 0.4%；每月 1 日扣月供和房价 0.1% 的维护费。出售九折并先还清贷款。关闭门店后的自有物业由中介托管出租，租金扣 15% 空置管理成本。",16);
            var ids=S.OwnedProperties.Select(x=>x.PlaceId).Concat(S.ManagedVentures.Where(x=>!S.IsParkVenture(x)).Select(x=>x.PlaceId));
            if(S.HasShop) ids=ids.Concat(new[]{S.PlayerShopPlace.Id});
            foreach(string place in ids.Distinct())
            {
                string id=place;var property=S.PropertyAt(id);var c=BusinessCard(w.Content,S.PlaceTitle(S.City.GetPlace(id))+" · "+S.City.GetPlace(id).Name);
                if(property!=null)
                {
                    Line(c,$"买入价 {Money.FromCents(property.PriceCents)}    房贷本金 {Money.FromCents(property.MortgageCents)}",17);
                    var row=Ui.Row(c,40);
                    BusinessAction(row,"提前还清房贷",()=>S.RepayMortgage(id),w,property.MortgageCents>0 && S.Ledger.Balance>=Money.FromCents(property.MortgageCents));
                    Ui.Size(Ui.Button(row,$"出售… 净回款 {S.PropertySaleNet(property)}",()=>ConfirmBusiness("出售物业","按原价九折出售并清偿贷款；仍在经营的门店恢复租赁计费。",()=>{S.SellProperty(id);w.Rebuild();}),17,Ui.Warn,S.InAgency),-1,-1,1);
                }
                else
                {
                    Money quote=S.PropertyQuote(id);Line(c,$"售价 {quote}    按揭首付 {quote.Scale(.3)}",17);
                    string why=S.PropertyBlocker(id,true);if(why!=null) Line(c,why,15,Ui.Dim);
                    var row=Ui.Row(c,40);
                    BusinessAction(row,"全款买下",()=>S.BuyProperty(id,false),w,S.InAgency && S.PropertyBlocker(id,false)==null);
                    Ui.Size(Ui.Button(row,"办理按揭…",()=>ConfirmBusiness("物业按揭",$"首付 {quote.Scale(.3)}，贷款 {quote.Scale(.7)}，每月本金约 {quote.Scale(.7/36)} + 剩余本金的 0.4% 利息。另收月维护费 {quote.Scale(.001)}。",()=>{S.BuyProperty(id,true);w.Rebuild();}),17,Ui.ButtonHot,S.InAgency && why==null),-1,-1,1);
                }
            }
            var home=BusinessCard(w.Content,"住处 · "+S.HomeTitle);
            Line(home,$"月租 {S.CurrentHomeRent}    睡眠恢复 ×{S.HomeSleepQuality:0.00}    住宅贷款 {Money.FromCents(S.HomeMortgageCents)}",17);
            Para(home,"住处升级保留家具与储物：整洁单间 ¥1,200/月、安静公寓 ¥3,000/月，分别额外增加 0.08 / 0.16 睡眠质量；搬家支付首月租金 + ¥500。",15,Ui.Dim);
            if(S.HomePriceCents>0)
            {
                BusinessAction(home,"提前还清住宅贷款",S.RepayHomeMortgage,w,S.HomeMortgageCents>0 && S.Ledger.Balance>=Money.FromCents(S.HomeMortgageCents));
                Ui.Size(Ui.Button(home,"出售自住房…",()=>ConfirmBusiness("出售住宅","按购入价九折出售，先还清贷款；继续租住并恢复月租。",()=>{S.SellHome();w.Rebuild();}),17,Ui.Warn,S.InAgency),-1,40);
            }
            else
            {
                var row=Ui.Row(home,40);
                for(int i=0;i<=2;i++) {int tier=i;BusinessAction(row,new[]{"城中村","整洁单间","安静公寓"}[i],()=>S.ChangeHomeTier(tier),w,S.InAgency && tier!=S.HomeTier);}
                if(S.HomeTier>0)
                {
                    Line(home,$"住宅售价 {S.HomePurchasePrice}    按揭首付 {S.HomePurchasePrice.Scale(.3)}",17);
                    var buy=Ui.Row(home,40);
                    BusinessAction(buy,"全款购入",()=>S.BuyHome(false),w,S.InAgency && S.Ledger.Balance>=S.HomePurchasePrice);
                    Ui.Size(Ui.Button(buy,"按揭购入…",()=>ConfirmBusiness("住宅按揭",$"首付 {S.HomePurchasePrice.Scale(.3)}；贷款 36 期等额本金、月利率 0.4%，月维护费 {S.HomePurchasePrice.Scale(.001)}。",()=>{S.BuyHome(true);w.Rebuild();}),17,Ui.ButtonHot,S.InAgency && S.Ledger.Balance>=S.HomePurchasePrice.Scale(.3)),-1,-1,1);
                }
            }
        }
    }
}
