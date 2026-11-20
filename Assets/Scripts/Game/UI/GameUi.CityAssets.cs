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

        public void OpenCityProperties() => OpenAssetWorkspace();
        private void OpenCityResidence() { _agencyResidential = true; GoToPropertyAgency(S.Home); }
        public void OpenCityProperty(Place place)
        {
            if(OwnsPropertyAt(place)||LeasesPropertyAt(place)){OpenAssetWorkspace(place.Kind == PlaceKind.Home ? 1 : 0,propertyId:place.Id);return;}
            _cityPropertyTarget = place.Id; _cityPropertyStreet = ""; _cityPropertyTab = S.PropertyAt(place.Id) != null ? 2 : 1; OpenCityPropertiesView();
        }
        private void OpenCityPropertiesView()
        {
            OpenWorkspace("property-agency", "purchase", "商铺产权交易", 1160, 840, w =>
            {
                S.VisitQuestTarget(QuestTarget.Property);
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
                HelpLink(w.Content, "物业交易说明", "物业只支持全款购买。售价与新租周租分别每周浮动；已签租约保留原价。购买产权不会接管租户的生意；原租约和人物关系保留，另有月维护费。确认购买前会列出房价、费用、押金抵扣和付款后余额。");
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
                        var p = place; var property = S.PropertyAt(p.Id);
                    var card = BusinessCard(w.Content, S.CityPropertyAddress(p));
                    if (property == null)
                    {
                        var preview = S.PreviewCityPropertyPurchase(p.Id, false);
                        Line(card, $"{GameSession.SizeName(p.Size)} · 房价 {preview.Price} · 新租周租参考 {S.ShopRentQuote(p)}", 18);
                        Line(card, "当前使用：" + preview.Tenant, 17);
                        var actions = Ui.Row(card, 42);
                        Ui.Size(Ui.Button(actions, "全款购买预览", () => CityPropertyConfirm(p.Id, false, w), 18), -1, 42, 1);
                    }
                    else
                    {
                        Line(card, "已购 · " + OwnedPropertyStatus(p), 18);
                        Line(card, "当前估值 " + S.PropertyMarketValue(property), 18, Ui.Accent);
                        Line(card, $"月维护 {Money.FromCents(property.PriceCents).Scale(.001)} · 每周应收 {S.PropertyRentReceivable(property)}", 17, Ui.Dim);
                        BuildCommercialPropertyActions(card,p,property,w);
                    }
                }
                AgencyLink(w,_cityPropertyTarget==null?null:S.City.GetPlace(_cityPropertyTarget));
            });
        }

        private void BuildCommercialPropertyActions(Transform parent, Place place, OwnedProperty property, Window w)
        {
            string id = place.Id;
            bool canOpen = property.UseMode == "vacant" && GameSession.IsCommercialPremises(place);
            Line(parent, "商铺管理", 20, Ui.Accent);
            if (canOpen || property.UseMode is "leased" or "npc")
                BuildPropertyRentalControls(parent, place, w);
            var actions = Ui.Row(parent, 38, 6);
            if (canOpen)
            {
                var kinds = RetailShopTypes.All;
                bool dedicated = S.IsDedicatedVenue(place);
                string kind = RetailShopTypes.Default;
                if (!dedicated)
                    Ui.Size(Ui.Choice(actions, kinds.Select(RetailShopTypes.Name), Array.IndexOf(kinds, kind), index => kind = kinds[index]), 0, 38, 1)
                        .Tip("选择店铺类型");
                Ui.Size(Ui.Button(actions, "自营开店…", () =>
                {
                    string selected = kind;
                    string business = dedicated ? S.VenueBusinessName(place) : RetailShopTypes.Name(selected);
                    string withdraw = property.AskingRentCents > 0 ? "，同时撤下出租挂牌" : "";
                    CityAssetConfirm("自营开店 · " + place.Name, $"登记为自营{business}，免租金{withdraw}。随后可到店装修、准备营业。",
                        () => S.OpenCityOwnedShop(id, selected), w);
                }, 16), 116, 38);
            }
            else if (S.ShopAt(place) is OwnedShop shop)
                Ui.Size(Ui.Button(actions, "结束自营…", () => ConfirmCloseShop(shop), 16), 116, 38);
            string blocker = S.CityPropertySaleBlocker(id);
            Money net = S.PropertySaleNet(property);
            Ui.Size(Ui.Button(actions, "出售产权…", () => CityAssetConfirm("出售商铺产权",
                $"到手 {net}，原租户保留。", () => S.SellProperty(id), w), 16, Ui.Warn, blocker == null), 111, 38)
                .Tip(blocker ?? $"出售到手 {net}");
        }

        private void CityPropertyConfirm(string id, bool mortgage, Window parent)
        {
            var q = S.PreviewCityPropertyPurchase(id, mortgage); if (q == null) return;
            var place = S.City.GetPlace(id);
            bool atAgency = S.InAgency && S.AgencyServes(S.CurrentPlace, place);
            string why = atAgency ? S.CityPropertyPurchaseBlocker(id, mortgage) : "请到房源所属中介签约。";
            string terms = $"全款购买。<b>月维护费 {q.Price.Scale(.001)}</b>。\n"
                + (q.IsSelfUse ? "过户后停止缴纳原周租，现有经营保留。"
                    : q.HasTenant ? "原商户继续经营，商品和员工仍归租户。" : "过户后可自营或挂牌出租。");
            OpenPropertyContract("商铺买卖合同", q.Address, $"使用方 · {q.Tenant}", q.TotalDue, fees =>
                {
                    AgencyFeeLine(fees, "全款售价", q.Price.ToString());
                    AgencyFeeLine(fees, "交易费", q.Fee.ToString());
                    if (q.DepositCredit.IsPositive) AgencyFeeLine(fees, "抵扣押金", "− " + q.DepositCredit);
                    if (q.PrepaidCredit.IsPositive) AgencyFeeLine(fees, "抵扣未用租金", "− " + q.PrepaidCredit);
                }, terms, true, () =>
                {
                    var live = S.PreviewCityPropertyPurchase(id, mortgage);
                    if (live == null || live.TotalDue != q.TotalDue || live.Price != q.Price || live.Tenant != q.Tenant)
                    {
                        Toast("房源或费用有变化，请重新核对。");
                        return false;
                    }
                    return S.BuyProperty(id, mortgage);
                }, () => parent?.Rebuild(), "PropertyConfirmPurchase", why,
                referral: atAgency ? null : content =>
                    Ui.Size(Ui.Button(content, "前往房源所属中介", () => GoToPropertyAgency(place), 17), -1, 38).name = "PropertyAgency");
        }
        private void CityAssetConfirm(string title, string terms, Func<bool> action, Window parent, bool enabled = true)
        {
            bool submitted = false;
            OpenWindow(title, 780, 530, confirmation =>
            {
                Para(confirmation.Content, terms, 19);
                Ui.Size(Ui.Button(confirmation.Content, "确认办理", () => { if (submitted) return; submitted = true; if (action()) { confirmation.Close(); parent?.Rebuild(); } else submitted = false; }, 19, Ui.Warn, enabled), -1, 48);
                Ui.Size(Ui.Button(confirmation.Content, "返回", confirmation.Close, 19), -1, 44);
            }, fitContent: true);
        }
    }
}
