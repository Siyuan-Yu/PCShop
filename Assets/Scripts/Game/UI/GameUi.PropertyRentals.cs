using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private bool _agencyMyProperties;
        private bool _agencyMyResidences;
        private string _agencyMyPropertyId;
        private static readonly Color PropertyOwnedColor = new Color32(232, 190, 98, 255);
        private static readonly Color PropertyRentedColor = new Color32(103, 181, 216, 255);
        private static readonly Color AgencyLocationColor = new Color32(67, 88, 70, 255);

        private bool OwnsPropertyAt(Place place) =>
            place.Kind == PlaceKind.Home ? S.OwnsResidence(place) : S.PropertyAt(place.Id) != null || S.ShopAt(place)?.OwnsProperty == true;

        private bool LeasesPropertyAt(Place place) => place.Kind == PlaceKind.Home ? S.HasResidentialLease(place)
            : !OwnsPropertyAt(place) && (S.ShopAt(place)?.Lease != null || S.VentureAt(place.Id) != null || S.PendingLease?.PlaceId == place.Id);

        private string OwnedPropertyStatus(Place place)
        {
            var property = S.PropertyAt(place.Id);
            if (place == S.Home) return "主住所";
            if (S.ShopAt(place) != null || S.VentureAt(place.Id) != null || place.Id == S.ParkPlaceId) return "自营";
            if (property?.AskingRentCents > 0) return "招租中";
            return property?.UseMode is "leased" or "npc" ? "已出租" : "空置";
        }

        private void BuildPropertyRentalControls(Transform parent, Place place, Window owner)
        {
            var property = S.PropertyAt(place.Id);
            if (property?.UseMode == "leased")
            {
                Line(parent, "租户 " + property.TenantName, 17);
                Line(parent, "每周应收 " + S.PropertyRentReceivable(property), 17, Ui.Good);
                Line(parent, "合同到期 " + new GameDate(property.LeaseEndDay)
                    + (property.RecoverAtLeaseEnd ? " · 到期收回" : ""), 16, Ui.Dim);
                var actions = Ui.Row(parent, 36, 6);
                bool due = S.Clock.Today.TotalDays >= property.LeaseEndDay;
                Ui.Size(Ui.Button(actions, "立即收回…", () => ConfirmRecovery(true), 16, interactable: due), 0, 36, 1);
                Ui.Size(Ui.Button(actions, property.RecoverAtLeaseEnd ? "取消到期收回" : "到期收回…", () =>
                {
                    if (property.RecoverAtLeaseEnd)
                    {
                        if (S.SetPropertyRecovery(place.Id, false)) owner.Rebuild();
                    }
                    else ConfirmRecovery(false);
                }, 16, Ui.ButtonHot, !due && !property.LegacyRentalDiscount), 0, 36, 1);
                return;

                void ConfirmRecovery(bool immediate) => CityAssetConfirm(immediate ? "收回房源" : "预约到期收回",
                    immediate ? "收回后可自用或重新挂牌。" : $"在 {new GameDate(property.LeaseEndDay)} 收回。到期前照常收租。",
                    () => S.SetPropertyRecovery(place.Id, true), owner);
            }

            string blocker = S.PropertyListingBlocker(place);
            if (blocker != null)
            {
                if (property?.UseMode == "npc")
                {
                    Line(parent, "租户 " + property.TenantName + " · 周租 " + S.PropertyRentReceivable(property), 17);
                    Line(parent, "原租约仍有效", 15, Ui.Dim);
                }
                else Line(parent, blocker, 16, Ui.Dim);
                if (property?.AskingRentCents > 0) BuildWithdraw(parent);
                return;
            }

            bool listed = property.AskingRentCents > 0;
            Line(parent, listed ? "招租中 · 当前挂牌 " + Money.FromCents(property.AskingRentCents) : "挂牌出租", 19, Ui.Accent);
            Money reference = S.PropertyReferenceRent(place);
            Money minimum = S.PropertyMinimumAskingRent(place);
            Money maximum = S.PropertyMaximumAskingRent(place);
            Money initial = listed ? Money.FromCents(property.AskingRentCents) : reference;
            Money draft = Money.Max(minimum, Money.Min(maximum, initial));
            var preview = Line(parent, "", 17);
            Text fee = null;
            Button confirm = null;
            var slider = Ui.Slider(parent, 0, 1, Position(draft), value =>
            {
                long step = place.Kind == PlaceKind.Home ? 500 : 5000;
                long cents = (long)Math.Round((minimum.Cents + value * (maximum.Cents - minimum.Cents)) / step) * step;
                draft = Money.Max(minimum, Money.Min(maximum, Money.FromCents(cents)));
                RefreshPrice();
            });
            slider.name = "PropertyRentalPriceSlider";
            var referenceTick = Ui.Panel(slider.handleRect.parent, "PropertyRentalMarketTick", Ui.Dim);
            referenceTick.raycastTarget = false;
            referenceTick.transform.SetAsFirstSibling();
            referenceTick.rectTransform.anchorMin = referenceTick.rectTransform.anchorMax = new Vector2(Position(reference), .23f);
            referenceTick.rectTransform.sizeDelta = new Vector2(2, 6);
            referenceTick.rectTransform.anchoredPosition = Vector2.zero;
            var scales = Ui.Size(Ui.Rect(parent, "PropertyRentalScale"), -1, 22);
            ScaleLabel(minimum.ToString(), 0, TextAnchor.MiddleLeft);
            ScaleLabel("参考 " + reference, Position(reference), TextAnchor.MiddleCenter);
            ScaleLabel(maximum.ToString(), 1, TextAnchor.MiddleRight);
            fee = Line(parent, "", 15, Ui.Dim);
            var row = Ui.Row(parent, 36, 6);
            confirm = Ui.Size(Ui.Button(row, listed ? "确认改价" : "确认出租", () =>
            {
                if (S.ListPropertyForRent(place.Id, draft)) owner.Rebuild();
            }, 17, Ui.ButtonHot), 0, 36, 1);
            Ui.Size(Ui.Button(row, "还原", () =>
            {
                draft = Money.Max(minimum, Money.Min(maximum, initial));
                slider.SetValueWithoutNotify(Position(draft));
                RefreshPrice();
            }, 16), 72, 36);
            if (listed) BuildWithdraw(row);
            else confirm.Tip("挂牌后由中介找租户，签约前可改价或撤牌。");
            RefreshPrice();

            float Position(Money value) => (float)((double)(value.Cents - minimum.Cents) / Math.Max(1, maximum.Cents - minimum.Cents));
            void ScaleLabel(string value, float position, TextAnchor alignment)
            {
                var label = Ui.Label(scales, value, 14, alignment, Ui.Dim);
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(position, .5f);
                rect.pivot = new Vector2(position == 0 ? 0 : position == 1 ? 1 : .5f, .5f);
                rect.sizeDelta = new Vector2(104, 22);
                rect.anchoredPosition = Vector2.zero;
            }
            void RefreshPrice()
            {
                double interest = S.PropertyRentalInterest(place, draft);
                string label = interest >= .42 ? "较高" : interest >= .22 ? "一般" : interest >= .08 ? "较低" : "很低";
                preview.text = $"周租 {draft} · 租客意愿 {label}";
                preview.color = interest >= .42 ? Ui.Good : interest < .22 ? Ui.Warn : Ui.TextColor;
                fee.text = $"成交中介费 {S.AgentFeeFor(draft)}";
                confirm.interactable = !listed || draft != initial;
            }
            void BuildWithdraw(Transform container) => Ui.Size(Ui.Button(container, "撤牌", () =>
            {
                if (S.UnlistProperty(place.Id)) owner.Rebuild();
            }, 16), 72, 36);
        }

        private void BuildResidencePropertyActions(Transform parent, Place place, Window owner, bool includeRental)
        {
            bool canReach = S.CanSleepAt(place) && ShortcutTravelBlocker(place) == null;
            var row = Ui.Row(parent, 37, 6);
            Ui.Size(Ui.Button(row, "前往这套住宅", () => { CloseAllWindows(); _ctrl.GoHomeToSleep(place); },
                16, Ui.ButtonHot, canReach), 0, 37, 1);
            bool preferred = SelectedShortcut(SleepShortcut) == place;
            Ui.Size(Ui.Button(row, preferred ? "默认回家" : "设为默认回家", () =>
            {
                S.SetCityHomeShortcut(place);
                RefreshShortcutMenus();
                owner.Rebuild();
            }, 16, interactable: canReach && !preferred), 0, 37, 1);
            if (includeRental && S.PropertyAt(place.Id) != null) BuildPropertyRentalControls(parent, place, owner);
        }

        private void OpenOwnedPropertyRental(Place place, Window parent)
        {
            var window = OpenWindow("出租管理 · " + place.Name, 670, 540,
                detail => BuildPropertyRentalControls(detail.Content, place, detail), fitContent: true);
            window.OnClosed += parent.RequestRebuild;
        }

        private void BuildAgencyOwnedProperties(Window w, Place agency)
        {
            var properties = S.City.Places.Where(place => S.AgencyServes(agency, place)
                && (place.Kind == PlaceKind.Home) == _agencyMyResidences && OwnsPropertyAt(place))
                .OrderBy(place => place.StreetId).ThenBy(place => place.Name).ToArray();
            var filters = Ui.Row(w.Content, 37, 8);
            foreach (bool homes in new[] { false, true })
                Ui.Size(Ui.Button(filters, homes ? "住宅" : "商铺", () => { _agencyMyResidences = homes; w.Rebuild(); },
                    17, Ui.SelectedButton(_agencyMyResidences == homes)), 110, 37);
            var row = Ui.Row(w.Content, 650, 12);
            var list = Ui.Panel(row, "AgencyOwnedList", Ui.PanelBg2).rectTransform;
            Ui.Size(list, 0, 650, 1);
            Ui.VList(list, 5, 10);
            Line(list, "本片区已购" + (_agencyMyResidences ? "住宅" : "商铺"), 20, Ui.Accent);
            var cards = Ui.Scroll(list, 590);
            var detail = Ui.Panel(row, "AgencyOwnedDetail", Ui.PanelBg2).rectTransform;
            Ui.Size(detail, 480, 650);
            Ui.VList(detail, 6, 10);
            var selected = properties.FirstOrDefault(place => place.Id == _agencyMyPropertyId) ?? properties.FirstOrDefault();
            if (selected == null) { Line(list, "本片区暂无已购房源", 17, Ui.Dim); return; }
            foreach (var place in properties)
            {
                string label = AssetPropertyName(place) + " · " + OwnedPropertyStatus(place);
                Ui.Size(Ui.Button(cards, label, () => { _agencyMyPropertyId = place.Id; w.Rebuild(); },
                    16, Ui.SelectedButton(place == selected)), -1, 44);
            }
            Line(detail, AssetPropertyName(selected), 21);
            Line(detail, S.City.StreetName(selected.StreetId) + " · " + OwnedPropertyStatus(selected), 16, Ui.Dim);
            if (_agencyMyResidences) BuildOwnedResidenceActions(detail, selected, w);
            else if (S.PropertyAt(selected.Id) is OwnedProperty property) BuildCommercialPropertyActions(detail, selected, property, w);
        }
    }
}
