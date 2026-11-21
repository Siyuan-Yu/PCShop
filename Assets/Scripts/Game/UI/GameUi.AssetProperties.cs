using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private int _assetPropertyListTab;

        private void BuildAssetProperties(Window w, bool residential)
        {
            S.VisitQuestTarget(QuestTarget.Property);
            string type = residential ? "住宅" : "商铺";
            var owned = (residential ? S.ResidentialPlaces : S.City.Places.Where(p => p.Kind != PlaceKind.Home))
                .Where(OwnsPropertyAt).OrderBy(p => p.StreetId).ThenBy(p => p.Name).ToArray();
            var rented = (residential ? S.ResidentialPlaces : S.City.Places.Where(p => p.Kind != PlaceKind.Home))
                .Where(LeasesPropertyAt).OrderBy(p => p.StreetId).ThenBy(p => p.Name).ToArray();
            var places = owned.Concat(rented).Distinct().ToArray();
            var ownedSet = new HashSet<string>(owned.Select(place => place.Id));
            if (owned.Length == 0 && rented.Length > 0) _assetPropertyListTab = 1;
            else if (rented.Length == 0) _assetPropertyListTab = 0;
            if (!places.Any(place => place.Id == _assetPropertyId)) _assetPropertyId = null;

            var summary = Ui.Row(w.Content, 34, 8);
            Ui.Size(Ui.Label(summary, $"我的{type}位置 · 已购 {owned.Length} 处 · 已租 {rented.Length} 处",
                22, TextAnchor.MiddleLeft, Ui.Accent), 0, 34, 1);
            if (!residential)
                Ui.Size(Ui.Button(summary, "全部商铺经营收支", () => OpenAssetFinance(), 16), 168, 34);
            var navigation = Ui.Row(w.Content, 36, 6);
            var stage = Ui.Size(Ui.Rect(w.Content, "PropertyCity"), -1, 609);
            var mapArea = Ui.Rect(stage, "CityMap");
            mapArea.Anchor(0, 0, 1, 1, 0, 27, 318, 0);
            mapArea.gameObject.AddComponent<RectMask2D>();
            var drawing = Ui.Rect(mapArea, "PropertyMapDrawing").Fill();
            var surface = drawing.gameObject.AddComponent<RawImage>();
            surface.color = Color.white;
            var view = drawing.gameObject.AddComponent<AgencyStreetMap>();
            view.Initialize(_ctrl, _assetMapView, places, 920, 582, assetOverview: true);
            if (_assetMapDistrict != null)
                view.FrameAssetRegion(S.City.AgencyDistricts.FirstOrDefault(d => d.id == _assetMapDistrict), reset: false);

            var detail = Ui.Rect(stage, "PropertyList");
            detail.Anchor(1, 0, 1, 1, -301, 0, 0, 0);
            Ui.VList(detail, 5, 0);
            var outlines = Ui.Rect(mapArea, "OwnedPropertyOutlines").Fill();
            var markers = Ui.Rect(mapArea, "PropertyMarkers").Fill();
            var controls = Ui.Panel(mapArea, "AssetMapZoom", Ui.PanelBg).rectTransform;
            controls.Anchor(0, 0, 0, 0, 12, 12, -176, -50);
            var bar = Ui.Row(controls, 38, 3);
            bar.Fill();
            Ui.Size(Ui.Button(bar, "−", () => view.ZoomBy(1 / 1.3f), 19), 43, 35);
            var zoom = Ui.Size(Ui.Label(bar, "", 15, TextAnchor.MiddleCenter), 65, 35);
            Ui.Size(Ui.Button(bar, "+", () => view.ZoomBy(1.3f), 19), 43, 35);

            var regionButtons = new List<(string id, Button button)>();
            void AddRegion(LayoutAgencyDistrictData district)
            {
                var button = Ui.Size(Ui.Button(navigation, district?.name ?? "全城", () =>
                {
                    _assetMapDistrict = district?.id;
                    view.FrameAssetRegion(district);
                }, 17), district == null ? 84 : 138, 36);
                regionButtons.Add((district?.id, button));
            }
            AddRegion(null);
            foreach (var district in S.City.AgencyDistricts) AddRegion(district);

            var popup = Ui.Panel(mapArea, "AssetBuildingInfo", Ui.PanelBg).rectTransform;
            popup.anchorMin = popup.anchorMax = popup.pivot = new Vector2(.5f, .5f);
            popup.sizeDelta = new Vector2(354, 0);
            Ui.VList(popup, 4, 10);
            popup.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            popup.gameObject.SetActive(false);
            Place selected = null;
            bool popupOpen = false;
            var listButtons = new Dictionary<Place, Button>();

            Vector2 ViewPoint(Place place, out PlaceVisual visual)
            {
                if (!_ctrl.World.Places.TryGetValue(place.Id, out visual)) return new Vector2(-1, -1);
                var bounds = view.BoundsOf(visual);
                var position = view.Project(new Vector3(bounds.center.x, bounds.max.y + .5f, bounds.center.z));
                return new Vector2(position.x / view.Width, 1 - position.y / view.Height);
            }

            void PositionPopup()
            {
                var point = selected == null ? new Vector2(-1, -1) : ViewPoint(selected, out _);
                bool visible = popupOpen && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
                popup.gameObject.SetActive(visible);
                if (!visible) return;
                var rect = mapArea.rect;
                float x = rect.xMin + point.x * rect.width, y = rect.yMin + point.y * rect.height;
                var half = popup.sizeDelta / 2;
                popup.anchoredPosition = new Vector2(Mathf.Clamp(x + half.x + 12, rect.xMin + half.x + 8, rect.xMax - half.x - 8),
                    Mathf.Clamp(y - half.y - 10, rect.yMin + half.y + 8, rect.yMax - half.y - 8));
            }

            void ShowInfo(Place place)
            {
                selected = place;
                popupOpen = true;
                _assetPropertyId = place.Id;
                bool mine = ownedSet.Contains(place.Id);
                int tab = mine ? 0 : 1;
                if (_assetPropertyListTab != tab)
                {
                    _assetPropertyListTab = tab;
                    RenderSide();
                }
                foreach (var pair in listButtons) Ui.SetButtonFill(pair.Value, Ui.SelectedButton(pair.Key == selected));
                Ui.Clear(popup);
                string street = S.City.StreetName(place.StreetId);
                var head = Ui.Row(popup, -1, 8);
                var headLayout = head.GetComponent<HorizontalLayoutGroup>();
                headLayout.childAlignment = TextAnchor.UpperLeft;
                headLayout.childForceExpandHeight = false;
                var name = Ui.Size(Ui.Label(head, NameWithoutStreetPrefix(AssetPropertyName(place), street), 19,
                    TextAnchor.MiddleLeft, Ui.Accent), 0, -1, 1);
                name.fontStyle = FontStyle.Bold;
                name.supportRichText = false;
                name.GetComponent<LayoutElement>().minHeight = 29;
                Ui.Size(Ui.Button(head, "×", () => { popupOpen = false; PositionPopup(); }, 18), 29, 29);
                var address = Ui.Row(popup, -1, 6);
                address.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                Ui.Size(Ui.Button(address, mine ? "已购" : "已租", null, 14, mine ? PropertyOwnedColor : PropertyRentedColor), 49, 25);
                Ui.Size(Ui.Label(address, street + " · " + AssetUseStatus(place, mine), 14), 0, -1, 1);
                if (mine)
                {
                    var property = S.PropertyAt(place.Id);
                    var value = place == S.Home ? S.HomePurchasePrice : S.PropertyMarketValue(property);
                    Line(popup, "当前估值 " + value, 16);
                    Line(popup, property?.AskingRentCents > 0 ? "挂牌周租 " + Money.FromCents(property.AskingRentCents)
                        : "每周应收 " + S.PropertyRentReceivable(property), 16);
                }
                else
                {
                    Line(popup, "签约周租 " + Money.FromCents(AssetLeasedRent(place)), 16);
                    Line(popup, "当前新租参考 " + S.PropertyReferenceRent(place), 16, Ui.Dim);
                }
                if (residential) BuildResidencePropertyActions(popup, place, w, showTravel: true);
                var actions = Ui.Row(popup, 35, 5);
                if (mine && S.PropertyAt(place.Id) != null)
                    Ui.Size(Ui.Button(actions, "出租管理", () => OpenOwnedPropertyRental(place, w), 15), 0, 35, 1);
                if (!residential && (S.ShopAt(place) != null || S.VentureAt(place.Id) != null))
                    Ui.Size(Ui.Button(actions, "经营收支", () => OpenAssetFinance(place.Id), 15), 0, 35, 1);
                Ui.Size(Ui.Button(actions, "前往所属中介", () => GoToPropertyAgency(place), 15), 0, 35, 1);
                popup.gameObject.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(popup);
                RefreshMapOverlay();
            }

            void RenderSide()
            {
                Ui.Clear(detail);
                listButtons.Clear();
                Line(detail, "我的" + type, 21, Ui.Accent);
                var tabs = Ui.Row(detail, 36, 5);
                Ui.Size(Ui.Button(tabs, "已购 · " + owned.Length, () => { _assetPropertyListTab = 0; RenderSide(); },
                    16, Ui.SelectedButton(_assetPropertyListTab == 0)), 0, 36, 1);
                Ui.Size(Ui.Button(tabs, "已租 · " + rented.Length, () => { _assetPropertyListTab = 1; RenderSide(); },
                    16, Ui.SelectedButton(_assetPropertyListTab == 1)), 0, 36, 1);
                bool mine = _assetPropertyListTab == 0;
                var rows = mine ? owned : rented;
                long rent = rows.Sum(place => mine ? S.PropertyRentReceivable(S.PropertyAt(place.Id)).Cents : AssetLeasedRent(place));
                Line(detail, (mine ? "每周应收 " : "每周应付 ") + Money.FromCents(rent), 18, Ui.Accent);
                Line(detail, "单击查看 · 双击定位", 14, Ui.Dim);
                var list = Ui.Scroll(detail, 459);
                if (rows.Length == 0) Line(list, (mine ? "暂无已购" : "暂无已租") + type, 17, Ui.Dim);
                foreach (var place in rows)
                {
                    var row = Ui.Row(list, 54, 5);
                    Ui.Size(Ui.Button(row, mine ? "已购" : "已租", null, 13, mine ? PropertyOwnedColor : PropertyRentedColor), 43, 35);
                    var button = Ui.Size(Ui.Button(row, AssetPropertyName(place) + "\n<size=13>" + AssetUseStatus(place, mine)
                        + "</size>", () => ShowInfo(place), 15, Ui.SelectedButton(place == selected)), 0, 54, 1);
                    button.name = "AssetPropertyRow_" + place.Id;
                    listButtons[place] = button;
                    var caption = button.GetComponentInChildren<Text>();
                    caption.alignment = TextAnchor.MiddleLeft;
                    caption.resizeTextForBestFit = true; caption.resizeTextMinSize = 12; caption.resizeTextMaxSize = 16;
                    button.gameObject.AddComponent<AssetPropertyDoubleClick>().Focus = () =>
                    {
                        _assetMapDistrict = S.City.AgencyDistrictFor(place.StreetId)?.id;
                        view.FrameAssetRegion(S.City.AgencyDistrictFor(place.StreetId));
                        view.Focus(place);
                    };
                }
            }

            void RefreshMapOverlay()
            {
                zoom.text = $"{view.Zoom * 100:0}%";
                foreach (var (id, button) in regionButtons) Ui.SetButtonFill(button, Ui.SelectedButton(id == _assetMapDistrict));
                Ui.Clear(outlines);
                Ui.Clear(markers);
                foreach (var place in places.OrderBy(place => place == selected))
                {
                    Vector2 point = ViewPoint(place, out var building);
                    if (point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) continue;
                    bool mine = ownedSet.Contains(place.Id);
                    bool highlighted = place == selected && popupOpen;
                    Button button;
                    if (mine)
                    {
                        AgencyMapGlow(outlines, view, "OwnedPropertyOutline_" + place.Id, building, PropertyOwnedColor, highlighted);
                        button = Ui.Button(markers, "已购", null, 14, PropertyOwnedColor);
                        Ui.FlatBorder(button.image, Ui.PanelBg, highlighted ? 2 : 1);
                        var caption = button.GetComponentInChildren<Text>();
                        caption.fontStyle = FontStyle.Bold;
                        caption.rectTransform.Fill(2);
                    }
                    else
                    {
                        var dot = Ui.Panel(markers, "PropertyPin_" + place.Id, PropertyRentedColor);
                        dot.sprite = CircleSprite();
                        if (highlighted) Ui.FlatBorder(dot, Ui.PanelBg, 2);
                        button = dot.gameObject.AddComponent<Button>();
                        button.targetGraphic = dot;
                    }
                    button.name = "PropertyPin_" + place.Id;
                    var rect = (RectTransform)button.transform;
                    rect.anchorMin = rect.anchorMax = point;
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = mine ? new Vector2(44, 23) : Vector2.one * (highlighted ? 19 : 12);
                    if (mine)
                    {
                        bool nearTop = point.y > .94f;
                        rect.pivot = new Vector2(point.x < .025f ? 0 : point.x > .975f ? 1 : .5f, nearTop ? 1 : 0);
                        rect.anchoredPosition = new Vector2(0, nearTop ? -5 : 5);
                    }
                    button.onClick.AddListener(() => ShowInfo(place));
                    button.Tip(AssetPropertyName(place) + " · " + (mine ? "已购" : "已租"));
                }
                PositionPopup();
            }

            view.Selected = id => ShowInfo(S.City.GetPlace(id));
            view.Changed = RefreshMapOverlay;
            RenderSide();
            RefreshMapOverlay();
            if (_assetPropertyId != null && places.FirstOrDefault(place => place.Id == _assetPropertyId) is Place focused)
            {
                ShowInfo(focused);
                if (_assetFocusProperty)
                {
                    var district = S.City.AgencyDistrictFor(focused.StreetId);
                    _assetMapDistrict = district?.id;
                    view.FrameAssetRegion(district);
                    view.Focus(focused);
                }
            }
            _assetFocusProperty = false;
            var legend = Ui.Label(stage, Ui.Emphasize("■", PropertyOwnedColor) + " 已购     "
                + Ui.Emphasize("●", PropertyRentedColor) + " 已租     滚轮缩放 · 拖动查看 · 点击标记或建筑查看详情", 15);
            legend.rectTransform.Anchor(0, 0, 1, 0, 0, 0, 318, -25);
        }

        private string AssetPropertyName(Place place) => S.ShopAt(place.Id) is OwnedShop shop ? S.ShopTitle(shop) : place.Name;

        private long AssetLeasedRent(Place place)
        {
            if (place.Kind == PlaceKind.Home) return S.SignedResidentialRent(place).Cents;
            if (S.ShopAt(place.Id) is OwnedShop shop && shop.Rented) return S.RentOf(shop).Cents;
            if (S.VentureAt(place.Id) is ManagedVenture venture) return venture.RentCents;
            return S.PendingLease?.PlaceId == place.Id ? S.PendingLease.Rent.Cents : 0;
        }

        private string AssetUseStatus(Place place, bool mine) => mine ? OwnedPropertyStatus(place)
            : place.Kind == PlaceKind.Home ? (place == S.Home ? "主住所" : "可入住")
            : S.PendingLease?.PlaceId == place.Id ? "待交房" : "经营";
    }
}
