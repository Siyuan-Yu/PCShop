using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _cityPropertyTab = 1;
        private string _cityPropertyTarget;
        private string _cityPropertyStreet = "";
        private int _cityAssetQuantity = 10;

        public void OpenCityProperties() => OpenAssetWorkspace(1);
        private void OpenCityResidence() { _agencyResidential = true; GoToPropertyAgency(S.Home); }
        private void OpenAgencyPropertyPurchase() { _cityPropertyTarget=null;_cityPropertyTab=1;OpenCityPropertiesView(); }
        public void OpenCityProperty(Place place)
        {
            if(S.PropertyAt(place.Id)!=null||(place==S.Home&&S.HomePriceCents>0)){OpenAssetWorkspace(1,propertyId:place.Id);return;}
            _cityPropertyTarget = place.Id; _cityPropertyStreet = ""; _cityPropertyTab = S.PropertyAt(place.Id) != null ? 2 : 1; OpenCityPropertiesView();
        }
        private void OpenCityPropertiesView()
        {
            OpenWorkspace("property-agency", "purchase", "商铺产权交易", 1160, 840, w =>
            {
                GuideHint(w.Content,QuestTarget.Property,w.Rebuild);
                if (_cityPropertyTab == 3) { BuildResidence(w); AgencyLink(w,S.Home); return; }
                if (_cityPropertyTab == 0)
                {
                    Para(w.Content, "选择所属中介看房，房源详情可查看租金、商圈和签约费用。", 18);
                    Ui.Size(Ui.Button(w.Content, "打开中介租铺地图", () => GoToPropertyAgency(), 19), -1, 44);
                    return;
                }
                var eligible = _cityPropertyTab == 2
                    ? S.City.Places.Where(p => p.Kind != PlaceKind.Home && S.PropertyAt(p.Id) != null)
                    : S.CityCommercialProperties;
                var places = eligible.Where(p => !S.InAgency || S.AgencyServes(S.CurrentPlace, p)).ToArray();
                Line(w.Content, $"已持有 {S.OwnedProperties.Count} 处 · 估值 {S.PropertyValue} · 现金 {S.Ledger.Balance}", 21, Ui.Accent);
                HelpLink(w.Content, "物业交易说明", "物业只支持全款购买。售价与新租月租分别每周浮动；已签租约保留原价。购买产权不会接管租户的生意；原租约和人物关系保留，另有月维护费。确认购买前会列出房价、费用、押金抵扣和付款后余额。");
                if (_cityPropertyTarget != null) Ui.Size(Ui.Button(w.Content, "查看全部物业", () => { _cityPropertyTarget = null; w.Rebuild(); }, 16), -1, 32);
                if (_cityPropertyTarget == null)
                {
                    var streetIds = places.Select(p=>p.StreetId??"").Distinct().ToArray();
                    var labels = new[]{S.InAgency?"本片区":"全部街区"}.Concat(streetIds.Select(S.City.StreetName)).ToArray();
                    var filter = Ui.Row(w.Content,38,8);Ui.Size(Ui.Label(filter,"筛选街区",17),100);
                    Ui.Size(Ui.Choice(filter,labels,Array.IndexOf(streetIds,_cityPropertyStreet)+1,index=>{_cityPropertyStreet=index==0?"":streetIds[index-1];w.RequestRebuild();}),-1,38,1);
                }
                var list = places.Where(p => (S.InAgency || string.IsNullOrEmpty(_cityPropertyStreet) || p.StreetId == _cityPropertyStreet)
                    && (_cityPropertyTab != 2 || S.PropertyAt(p.Id) != null) && (_cityPropertyTarget == null || p.Id == _cityPropertyTarget));
                foreach (var place in list)
                {
                    var p = place; var property = S.PropertyAt(p.Id); var shop = S.ShopAt(p.Id);
                    var card = BusinessCard(w.Content, S.CityPropertyAddress(p));
                    if (property == null)
                    {
                        var preview = S.PreviewCityPropertyPurchase(p.Id, false);
                        Line(card, $"{GameSession.SizeName(p.Size)} · 房价 {preview.Price} · 新租月租参考 {S.ShopRentQuote(p)}", 18);
                        Line(card, "当前使用：" + preview.Tenant, 17);
                        var actions = Ui.Row(card, 42);
                        Ui.Size(Ui.Button(actions, "全款购买预览", () => CityPropertyConfirm(p.Id, false, w), 18), -1, 42, 1);
                    }
                    else
                    {
                        string mode = property.UseMode == "self" ? "自营" : property.UseMode == "npc" ? "保留原租户" : property.UseMode == "managed" ? "中介托管出租" : "空置";
                        Line(card, $"用途：{mode} · 租户：{property.TenantName}", 18);
                        Line(card, "当前估值 " + S.PropertyMarketValue(property), 18, Ui.Accent);
                        Line(card, $"月维护 {Money.FromCents(property.PriceCents).Scale(.001)} · 月租 {Money.FromCents(property.MonthlyRentCents)}（自营不收租）", 17, Ui.Dim);
                        if (property.UseMode == "vacant" && GameSession.IsCommercialPremises(p))
                        {
                            var row = Ui.Row(card, 42);
                            Ui.Size(Ui.Button(row, "登记出租", () => CityAssetConfirm("登记出租", "签30天托管租约，次月账期开始收租，管理与空置折损15%。", () => S.SetCityPropertyRental(p.Id, true), w), 18), -1, 42, 1);
                            var kinds = Ui.Row(card, 42);
                            foreach (string kind in RetailShopTypes.All)
                            {
                                string selected = kind;
                                Ui.Size(Ui.Button(kinds, "开" + RetailShopTypes.Name(kind), () => CityAssetConfirm("在自有铺面开店", "免租金；设施、装修与次日开业规则照常。", () => S.OpenCityOwnedShop(p.Id, selected), w), 17), -1, 42, 1);
                            }
                        }
                        if (property.UseMode == "managed")
                            Ui.Size(Ui.Button(card, "到期恢复空置", () => { S.SetCityPropertyRental(p.Id, false); w.Rebuild(); }, 17), -1, 40);
                        if (shop != null)
                        {
                            Para(card, "结束自营：先搬走库存、食物与在途货物，再在该店内办理。家具收回，产权保留。", 16, Ui.Dim);
                            Ui.Size(Ui.Button(card, "结束自营并保留空置产权", () => CityAssetConfirm("结束自营", "按清店校验结束经营，现有产权保留。", () => S.CloseShop(p.Id), w), 17), -1, 42);
                        }
                        string why = S.CityPropertySaleBlocker(p.Id);
                        Para(card, why == null ? "出售到手 " + S.PropertySaleNet(property) + "，包含估值折价及交易成本。" : "出售条件：" + why, 17, why == null ? Ui.Dim : Ui.Warn);
                        Ui.Size(Ui.Button(card, "出售确认", () => CityAssetConfirm("出售具体物业", "到手 " + S.PropertySaleNet(property) + "。保留原租户及其人物关系。", () => S.SellProperty(p.Id), w), 18, Ui.Warn, why == null), -1, 42);
                    }
                }
                AgencyLink(w,_cityPropertyTarget==null?null:S.City.GetPlace(_cityPropertyTarget));
            });
        }
        private void CityPropertyConfirm(string id, bool mortgage, Window parent)
        {
            var q = S.PreviewCityPropertyPurchase(id, mortgage); if (q == null) return;
            string why = S.CityPropertyPurchaseBlocker(id, mortgage);
            string terms = q.Address + "\n使用方：" + q.Tenant + $"\n房价 {q.Price} + 交易费 {q.Fee}\n抵扣押金 {q.DepositCredit}、未使用首月租金 {q.PrepaidCredit}\n本次付款 {q.TotalDue}，付款后现金 {q.CashAfter}"
                + "\n全款购买。"
                + $"\n月维护费 {q.Price.Scale(.001)}。"
                + (why == null ? "\n原经营资产与租户不转移给买方。" : "\n当前不能成交：" + why);
            CityAssetConfirm("物业购买结算预览", terms, () => S.BuyProperty(id, mortgage), parent, why == null, S.City.GetPlace(id));
        }
        private void CityAssetConfirm(string title, string terms, Func<bool> action, Window parent, bool enabled = true, Place property = null)
        {
            bool submitted = false;
            OpenWindow(title, 780, 530, confirmation =>
            {
                Para(confirmation.Content, terms, 19);
                if(property!=null&&(!S.InAgency||!S.AgencyServes(S.CurrentPlace,property)))
                    Ui.Size(Ui.Button(confirmation.Content,"前往房源所属中介",()=>GoToPropertyAgency(property),17),-1,38).name="PropertyAgency";
                Ui.Size(Ui.Button(confirmation.Content, "确认办理", () => { if (submitted) return; submitted = true; if (action()) { confirmation.Close(); parent.Rebuild(); } else submitted = false; }, 19, Ui.Warn, enabled), -1, 48);
                Ui.Size(Ui.Button(confirmation.Content, "返回", confirmation.Close, 19), -1, 44);
            });
        }
        public void OpenCityInvestments()
        {
            string blocker=S.AppBlocker(PhoneApps.Securities);
            if(blocker!=null){Toast(blocker);if(S.HasSmartphone)OpenPhoneStore();return;}
            OpenWorkspace("phone-securities", "main", "证券", 1160, 840, w =>
            {
                Line(w.Content, $"现金 {S.Ledger.Balance} · 三企业持仓 {S.CityEquityValue} · 持仓成本 {S.CityEquityCost}", 21, Ui.Accent);
                Line(w.Content, $"已实现 {S.CityEquityRealized} · 浮动盈亏 {S.CityEquityValue - S.CityEquityCost}", 19);
                HelpLink(w.Content, "投资规则", "行情按游戏日更新，不使用借贷或杠杆。买卖前会显示成交金额、手续费和是否动用账单预留资金。硬件指数双向手续费0.5%，企业持仓双向0.2%。");
                Para(w.Content, S.CityInvestmentNews, 18, Ui.Accent);
                var quantity = Ui.Row(w.Content, 42);
                foreach (int n in new[] { 1, 10, 100, 1000 })
                {
                    int count = n; Ui.Size(Ui.Button(quantity, n + "份", () => { _cityAssetQuantity = count; w.Rebuild(); }, 18, n == _cityAssetQuantity ? Ui.ButtonHot : Ui.ButtonBg), -1, 42, 1);
                }
                CityInvestmentCard(w, "hardware", "硬件景气指数", "跟随硬件平均行情，沿用原指数双向0.5%手续费。", S.CityInvestmentHistory("hardware"));
                foreach (var def in CityInvestments.All)
                {
                    var state = S.CityEquities.First(e => e.Id == def.Id);
                    CityInvestmentCard(w, def.Id, def.Name, def.Description + " 买卖各0.2%，最低1分。", state.History);
                }
            });
        }
        private void CityInvestmentCard(Window w, string id, string title, string description, long[] history)
        {
            var c = BusinessCard(w.Content, title); Money price = S.CityInvestmentPrice(id); long held = S.CityInvestmentUnits(id);
            Line(c, $"现价 {price} · 持有 {held}份 · 市值 {price * held}", 20);
            Line(c, $"成本 {S.CityInvestmentCost(id)} · 已实现 {S.CityInvestmentRealized(id)} · 浮动 {price * held - S.CityInvestmentCost(id)}" + (id == "hardware" && S.CityFundCostEstimated ? "（旧档成本按迁移估算）" : ""), 17);
            Para(c, description, 16, Ui.Dim);
            if (history != null && history.Length > 0)
            {
                long lo = history.Min(), hi = history.Max();
                var graph = Ui.Row(c, 58, 2);
                graph.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                graph.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerCenter;
                foreach (long value in history.Skip(Math.Max(0, history.Length - 30)))
                {
                    var bar = Ui.Panel(graph, "Quote", value >= history[0] ? Ui.Good : Ui.Warn).rectTransform;
                    Ui.Size(bar, -1, 8 + (float)(value - lo) / Math.Max(1, hi - lo) * 46, 1);
                }
                Line(c, "近期价格区间 " + Money.FromCents(lo) + " — " + Money.FromCents(hi), 15, Ui.Dim);
            }
            var row = Ui.Row(c, 42);
            foreach (bool buy in new[] { true, false })
            {
                bool buying = buy; int quantity = _cityAssetQuantity;
                var total = S.CityInvestmentTradeTotal(id, quantity, buy);
                string label = (buy ? "买入" : "卖出") + quantity + "份 · " + total;
                Ui.Size(Ui.Button(row, label, () =>
                {
                    bool reserve = buying && S.CityInvestmentUsesReserve(id, quantity);
                    string text = title + "\n成交价 " + S.CityInvestmentPrice(id) + "，数量 " + quantity + "\n" + (buying ? "支付（含手续费）" : "到手（扣手续费）") + S.CityInvestmentTradeTotal(id, quantity, buying)
                        + (reserve ? "\n警告：本次买入将动用未来必付款及保护额，可能影响工资、房租或采购。继续由你决定。" : "\n不自动借贷或动用其他资产。");
                    CityAssetConfirm("投资交易预览", text, () => S.TradeCityInvestment(id, quantity, buying, reserve), w);
                }, 18, Ui.ButtonBg, S.AppBlocker(PhoneApps.Securities)==null && (buy ? S.Ledger.Balance >= total : held >= quantity)), -1, 42, 1).name="Investment"+(buy?"Buy_":"Sell_")+id;
            }
        }
    }
}
