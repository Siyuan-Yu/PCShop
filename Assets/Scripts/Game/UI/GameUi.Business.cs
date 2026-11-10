using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _businessTab, _ventureKind;
        public void DebugOpenBusiness(int tab) { _businessTab = Math.Max(0, Math.Min(6, tab)); ShowBusinessPanel(false); }

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
            OpenCityFinanceOverview();
        }

        /// <summary>中介里只办和房产有关的事：分店签约、物业与住处、园区。</summary>
        public void OpenAgencyBusiness()
        {
            GoToPropertyAgency();
        }

        private string _businessAssetId;
        private void OpenBusinessSection(int tab, string assetId = null)
        {
            _businessTab = tab; _businessAssetId = assetId; ShowBusinessPanel(false);
        }
        private void ShowBusinessPanel(bool agencyOnly)
        {
            if (_businessTab == 0) { OpenAssetWorkspace(); return; }
            if (_businessTab == 1) { OpenProcurementAgreement(); return; }
            if (_businessTab == 5) { OpenCityResidence(); return; }
            S.RefreshEnterpriseOffer();
            int section=_businessTab;string assetId=_businessAssetId;
            string title = section == 2 ? "企业采购订单" : section == 3 ? "托管门店管理" : section == 4 ? "MCN工作室" : "园区管理";
            OpenWorkspace("business-module-" + section, "main", title, 1100, 800, w =>
            {
                _businessAssetId=assetId;
                if (section==3 && assetId != null)
                    Ui.Size(Ui.Button(w.Content, "查看全部托管门店", () => { assetId = null; w.Rebuild(); }, 16), -1, 32);
                S.VisitQuestTarget(BusinessTargets[section]);
                switch (section)
                {
                    case 1: BuildDirectSupply(w); break;
                    case 2: BuildEnterprise(w); break;
                    case 3: BuildVentures(w); break;
                    case 4: BuildCreators(w); break;
                    case 6: BuildPark(w); break;
                    default: BuildBusinessFinance(w); break;
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
        private void AgencyLink(Window w,Place property=null)
        {
            if(S.InAgency&&(property==null||S.AgencyServes(S.CurrentPlace,property))) {Line(w.Content,"当前在所属中介，可办理签约。",16,Ui.Good);return;}
            Para(w.Content,"开设分店、买卖物业和更换住处需当面签约；经营管理可随时查看。",16,Ui.Dim);
            Ui.Size(Ui.Button(w.Content,"前往所属中介",()=>GoToPropertyAgency(property),17),-1,38);
        }
        private void BuildBusinessFinance(Window w)
        {
            var c=BusinessCard(w.Content,"每一笔钱放在哪里");
            Line(c,$"全店库存及在途（市价九折） {S.InventoryValue()}    载具残值（购价六折） {S.VehicleValue}",17);
            Line(c,$"铺面押金 {Money.FromCents(S.MyShops.Sum(shop=>shop.Lease?.Deposit.Cents??0))+(S.PendingLease?.Deposit??Money.Zero)}    企业合同保证金 {S.ContractDeposits}",17);
            Line(c,$"托管门店资产 {S.VentureValue}（含周转金 {S.VentureReserve}）    指数 {S.FundValue} · 企业持仓 {S.CityEquityValue}",17);
            Line(c,$"商用物业账面值 {S.PropertyValue}    自住房账面值 {Money.FromCents(S.HomePriceCents)}",17);
            Line(c,$"待付货款 {S.SupplierDebtTotal}    APP 贷款待还 {S.AppLoanDebt}",17,new Color(.84f,.60f,.50f));
            HelpLink(c,"资产估值说明","净资产 = 现金 + 库存 + 押金 + 载具残值 + 分店清算估值 + 物业买入价 + 指数市值 − 负债。物业出售按九折成交；尚未发生的利息不计入当前本金。分店收支留在自己的周转金中，转回总部才增加可用现金。");
            Line(w.Content,$"未来 7 天货款、贷款与物业维护 {S.UpcomingBusinessPayments(7)} · 自动补货当前保留额 {S.EffectiveRestockReserve}",16,Ui.Accent);
            var row=Ui.Row(w.Content,40);
            Ui.Size(Ui.Button(row,"三年目标",OpenGoalsPanel,17),-1,-1,1);
            Ui.Size(Ui.Button(row,"本店经营趋势",OpenReportWindow,17),-1,-1,1);
            Ui.Size(Ui.Button(row,"现金流水",OpenLedger,17),-1,-1,1);
            Ui.Size(Ui.Button(w.Content,"管理投资持仓",OpenCityInvestments,17),-1,38);
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
            Para(w.Content,"周维成牵线的工厂直供：市场价 84%，每种 3–30 件，次日 10:00 送到主店。可全款，也可 30% 首付、70% 尾款 14 天结清。直供承诺合格交付，不抽次品。",17);
            if(!S.DirectAgreement)
            {
                string why=S.DirectAgreementBlocker;
                Line(w.Content,why??"条件已满足，签约无需费用。",17,why==null ? Ui.Good : Ui.Dim);
                BusinessAction(w.Content,"签订直供协议",S.SignDirectAgreement,w,why==null);return;
            }
            Line(w.Content,$"信用额度 {S.CreditLimit}    已用 {S.SupplierDebtTotal}    仓位已预留在途 {S.IncomingShopSpace:0.#} 格",17);
            Ui.Size(Ui.Button(w.Content, "直供下单", () => OpenDirectSupplyOrder(w), 18, Ui.ButtonHot), -1, 42);
            foreach(var x in S.DirectShipments) Line(w.Content,$"在途：{PartCatalog.Get(x.PartId).Name} ×{x.Count} · {new GameTimestamp(x.Arrival)}",15,Ui.Dim);
            if(S.SupplierDebts.Count>0) BuildDebtList(w);
        }

        private void OpenDirectSupplyOrder(Window parent)
        {
            var parts = PartCatalog.All.Where(part => IndustryCycles.Available(part, S.Clock.Today.TotalDays)).ToArray();
            if (parts.Length == 0) return;
            int selected = 0, quantity = 3;
            bool credit = false;
            var shop = S.MainShop;
            OpenWindow("直供下单", 680, 420, w =>
            {
                var part = parts[selected];
                Line(w.Content, "送到 " + S.ShopTitle(shop) + " · 次日 10:00", 17, Ui.Accent);
                Ui.Size(Ui.Choice(w.Content, parts.Select(item => item.Name), selected,
                    index => { selected = index; w.Rebuild(); }), -1, 40);
                var amount = Ui.Row(w.Content, 42, 8);
                Ui.Size(Ui.Label(amount, "数量", 17), 100);
                QuantityStepper(w, amount, "directQuantity", quantity, 3, 30, 1, value => quantity = (int)value);
                Ui.Size(Ui.Choice(w.Content, new[] { "全款", "付 30% 首款，14 天内结清" }, credit ? 1 : 0,
                    index => { credit = index == 1; w.Rebuild(); }), -1, 40);
                Money total = S.DirectPrice(part) * quantity;
                Line(w.Content, $"总货款 {total} · 本次实付 {(credit ? total.Scale(.3) : total)}", 18);
                Ui.Size(Ui.Button(w.Content, "确认直供下单", () =>
                {
                    if (S.PlaceUnifiedDirectPurchase(shop.Place.Id, part.Id, quantity, credit))
                    {
                        w.Close();
                        parent.Rebuild();
                    }
                }, 18, Ui.ButtonHot), -1, 44);
            });
        }

        private void BuildEnterprise(Window w)
        {
            Para(w.Content,"名望 8 解锁，每周刷新一份采购邀请。接受后自动进入主店装机队列；可以亲自装，也可以交给受训员工。逐台交付收款，全额完成再奖合同额 3%。",17);
            if(S.EnterpriseContracts.Count==0) Line(w.Content,"暂时没有采购邀请。提高名望后再来看看。",17,Ui.Dim);
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
            HelpLink(w.Content,"托管经营说明","主店保留亲自经营，其余铺面由店长托管。每日结算记入各店周转金；定价、位置、周末和维护状态影响销量。停业仍付租金及 20% 留任工资；周转金不足的固定费用由总部补足。");
            foreach(var venture in S.ManagedVentures.Where(v => _businessAssetId == null || v.PlaceId == _businessAssetId))
            {
                var v=venture;var d=Ventures.Get(v.Kind);var c=BusinessCard(w.Content,d.Name+" · "+S.VentureLocationName(v)+(v.Paused ? "  [停业]" : "  [营业]"));
                if(v.Kind=="cars")Ui.Size(Ui.Button(c,"到车行管理车源 / 库存 / 买家",()=>{CloseWindowSilently(w);S.BeginCityTravel(v.PlaceId);},18,Ui.ButtonHot),-1,40);
                Line(c,$"周转金 {Money.FromCents(v.ReserveCents)}   昨日收入 {Money.FromCents(v.LastSalesCents)} / 成本 {Money.FromCents(v.LastCostsCents)}",17);
                Line(c,$"累计经营结余 {Money.FromCents(v.TotalOperatingCents)}    定价 {v.PriceFactor:P0}    维护状态 {v.Condition:P0}",17);
                Para(c,$"周租 {Money.FromCents(v.RentCents)}，首周已付（自有物业免租）。{d.Description}",15,Ui.Dim);
                var row=Ui.Row(c,40);
                BusinessAction(row,"补资 ¥5,000",()=>S.TransferVentureCash(v.PlaceId,5000,false),w,S.Ledger.Balance>=Money.FromYuan(5000));
                BusinessAction(row,"取回 ¥1,000",()=>S.TransferVentureCash(v.PlaceId,1000,true),w,v.ReserveCents>=100000);
                BusinessAction(row,v.Paused ? "恢复营业" : "暂停营业",()=>S.ToggleVenture(v.PlaceId),w);
                if(v.Kind!="cars")
                {
                var controls=Ui.Row(c,40);
                BusinessAction(controls,"定价 −10%",()=>S.SetVenturePrice(v.PlaceId,Math.Max(.8,v.PriceFactor-.1)),w,v.PriceFactor>.8);
                BusinessAction(controls,"定价 +10%",()=>S.SetVenturePrice(v.PlaceId,Math.Min(1.3,v.PriceFactor+.1)),w,v.PriceFactor<1.3);
                BusinessAction(controls,$"维护 · {S.VentureRepairCost(v)}",()=>S.MaintainVenture(v.PlaceId),w,v.Condition<.999 && S.Ledger.Balance>=S.VentureRepairCost(v));
                }
                Ui.Size(Ui.Button(c,$"关闭并清算…  预计回收 {S.VentureExitValue(v)}",()=>ConfirmBusiness("关闭门店", "清退员工，设备与装修按 30% × 维护状态回收，退还押金与周转金。自有物业保留并交由中介出租。",()=>{S.CloseVenture(v.PlaceId);w.Rebuild();}),16,Ui.Warn),-1,36);
            }
            if(_businessAssetId!=null){AgencyLink(w,S.City.GetPlace(_businessAssetId));return;}
            AgencyLink(w);
            var chosen=Ventures.All[_ventureKind];var choose=Ui.Row(w.Content,42);
            Ui.Size(Ui.Button(choose,"上一业态",()=>{_ventureKind=(_ventureKind+Ventures.All.Length-1)%Ventures.All.Length;w.Rebuild();},17),150);
            Ui.Size(Ui.Label(choose,chosen.Name,21,TextAnchor.MiddleCenter,Ui.Accent),620);
            Ui.Size(Ui.Button(choose,"下一业态",()=>{_ventureKind=(_ventureKind+1)%Ventures.All.Length;w.Rebuild();},17),150);
            Para(w.Content,$"{chosen.Description} 装修设备 {Money.FromYuan(chosen.Capital)}，员工日薪 {Money.FromYuan(chosen.DailyWages)}，名望要求 {chosen.MinReputation:0}。另需租赁签约款及 5,000 周转金。",16);
            if (!S.Listings.Any(x=>x.OpenDay<=S.Clock.Today.TotalDays && (!S.InAgency || S.AgencyServes(S.CurrentPlace, S.ListingPlace(x))))) Para(w.Content,"当前没有可签约的空铺。中介会陆续挂出新房源；也可等已挂牌铺面的现租户到期。",16,Ui.Dim);
            foreach(var listing in S.Listings.Where(x=>x.OpenDay<=S.Clock.Today.TotalDays && (!S.InAgency || S.AgencyServes(S.CurrentPlace, S.ListingPlace(x)))))
            {
                var l=listing;string why=S.VentureBlocker(l.PlaceId,chosen.Id);var c=BusinessCard(w.Content,S.City.GetPlace(l.PlaceId).Name+" · "+GameSession.SizeName(l.Size));
                Line(c,$"周租 {l.Rent}    开办现金合计 {S.VentureOpeningCost(l,chosen)}",17);
                if(why!=null) Para(c,why,15,Ui.Dim);
                Ui.Size(Ui.Button(c,"在这里开设"+chosen.Name+"…",()=>ConfirmBusiness("开设"+chosen.Name,$"支付 {S.VentureOpeningCost(l,chosen)}。"+(chosen.HardwareCount>0 ? $"另从主店调拨 {chosen.HardwareCount} 套{PartCatalog.TierName(chosen.HardwareTier)}配件，不会返还成散件。" : "")+"每日结算从次日开始。",()=>{S.OpenVenture(l.PlaceId,chosen.Id);w.Rebuild();}),17,Ui.ButtonHot,S.InAgency && why==null),-1,40);
            }
        }
        private void BuildCreators(Window w)
        {
            BuildMcnHeader(w);
            Ui.Size(Ui.Button(w.Content, "社区MCN · 介绍创作者", OpenCommunityMcn, 18), -1, 40);
            if (!S.HasMcn) return;
            Para(w.Content, "和创作者本人交谈，选择“谈MCN合作”即可洽谈工作室合约。", 17);
            foreach (var c in S.CreatorPartners.Where(c => c.McnUntilDay > S.Clock.Today.TotalDays))
            {
                var card = BusinessCard(w.Content, Creators.Get(c.Id).Name);
                Line(card, $"剩余 {c.McnUntilDay - S.Clock.Today.TotalDays} 天 · 本期分成 {Money.FromCents(c.McnIncomeCents)}", 17);
            }
        }
        private void BuildResidence(Window w)
        {
            Line(w.Content, "默认住处 · " + S.HomeTitle, 20, Ui.Accent);
            foreach (var place in S.RentedResidences)
            {
                var residence = BusinessCard(w.Content, place.Name);
                Line(residence, "签约周租 " + S.SignedResidentialRent(place), 17);
                Ui.Size(Ui.Button(residence, "去中介管理租约", () => GoToPropertyAgency(place), 17), -1, 40);
            }
        }
    }
}
