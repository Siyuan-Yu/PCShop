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

        public void OpenCityProperties() => OpenAssetWorkspace(1);
        private void OpenCityResidence() { _agencyResidential = true; GoToPropertyAgency(S.Home); }
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
                        string mode = property.UseMode == "self" ? "自营" : property.UseMode == "npc" ? "保留原租户" : property.UseMode == "managed" ? "中介托管出租" : "空置";
                        Line(card, $"用途：{mode} · 租户：{property.TenantName}", 18);
                        Line(card, "当前估值 " + S.PropertyMarketValue(property), 18, Ui.Accent);
                        Line(card, $"月维护 {Money.FromCents(property.PriceCents).Scale(.001)} · 周租 {Money.FromCents(property.WeeklyRentCents)}（自营不收租）", 17, Ui.Dim);
                        if (S.ShopAt(p.Id) != null)
                        {
                            Para(card, "结束自营：先搬走库存、食物与在途货物，再在该店内办理。家具收回，产权保留。", 16, Ui.Dim);
                            Ui.Size(Ui.Button(card, "结束自营并保留空置产权", () => CityAssetConfirm("结束自营", "按清店校验结束经营，现有产权保留。", () => S.CloseShop(p.Id), w), 17), -1, 42);
                        }
                        BuildCommercialPropertyActions(card,p,property,w);
                    }
                }
                AgencyLink(w,_cityPropertyTarget==null?null:S.City.GetPlace(_cityPropertyTarget));
            });
        }

        private void BuildCommercialPropertyActions(Transform parent, Place place, OwnedProperty property, Window w)
        {
            string id = place.Id;
            if (property.UseMode == "vacant" && GameSession.IsCommercialPremises(place))
            {
                Ui.Size(Ui.Button(parent,"登记出租…",()=>CityAssetConfirm("登记出租",
                    "托管租期为28天，次月账期开始收租，管理与空置折损15%。",()=>S.SetCityPropertyRental(id,true),w),17),-1,40);
                var kinds = RetailShopTypes.All.ToArray();
                var choice = Ui.Choice(parent,new[]{"选择自营店型…"}.Concat(kinds.Select(RetailShopTypes.Name)),0,index=>
                {
                    if (index == 0) return;
                    string kind = kinds[index-1];
                    CityAssetConfirm("在自有铺面开店","免租金；设施、装修与次日开业规则照常。",()=>S.OpenCityOwnedShop(id,kind),w);
                });
                Ui.Size(choice,-1,40);
            }
            if (property.UseMode == "managed")
                Ui.Size(Ui.Button(parent,"到期恢复空置",()=>{if(S.SetCityPropertyRental(id,false))w?.Rebuild();},17),-1,40);
            string why = S.CityPropertySaleBlocker(id);
            Money net = S.PropertySaleNet(property);
            Line(parent,why ?? $"出售到手 {net}",16,why == null ? Ui.Dim : Ui.Warn);
            Ui.Size(Ui.Button(parent,"出售产权…",()=>CityAssetConfirm("出售商铺产权",
                $"到手 {net}，包含估值折价与交易成本。原租户保留。",()=>S.SellProperty(id),w),17,Ui.Warn,why == null),-1,42);
        }

        private void CityPropertyConfirm(string id, bool mortgage, Window parent)
        {
            var q = S.PreviewCityPropertyPurchase(id, mortgage); if (q == null) return;
            string why = S.CityPropertyPurchaseBlocker(id, mortgage);
            string terms = q.Address + "\n使用方：" + q.Tenant + $"\n房价 {q.Price} + 交易费 {q.Fee}\n抵扣押金 {q.DepositCredit}、本周未使用租金 {q.PrepaidCredit}\n本次付款 {q.TotalDue}，付款后现金 {q.CashAfter}"
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
                Ui.Size(Ui.Button(confirmation.Content, "确认办理", () => { if (submitted) return; submitted = true; if (action()) { confirmation.Close(); parent?.Rebuild(); } else submitted = false; }, 19, Ui.Warn, enabled), -1, 48);
                Ui.Size(Ui.Button(confirmation.Content, "返回", confirmation.Close, 19), -1, 44);
            });
        }
    }
}
