using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        [Flags]
        private enum DestinationType
        {
            None = 0, Shop = 1, Supply = 2, Residence = 4, Leisure = 8, Property = 16, Service = 32
        }

        private static readonly (DestinationType Type, string Label)[] DestinationFilters =
        {
            (DestinationType.None, "全部类型"), (DestinationType.Shop, "我的店"),
            (DestinationType.Supply, "采购货源"), (DestinationType.Residence, "住处"),
            (DestinationType.Leisure, "娱乐活动"), (DestinationType.Property, "已购物业"),
            (DestinationType.Service, "公共服务")
        };

        private sealed class DestinationEntry
        {
            public Place Place;
            public string Name, Street, Label, SearchText;
            public DestinationType Types;
        }

        private void OpenCustomDestinationPicker() =>
            OpenWindow("选择目的地", 1120, 730, BuildCustomDestinationPicker, scrollContent: false);

        private void BuildCustomDestinationPicker(Window w)
        {
            var venues = CityLifeCatalog.Activities.Where(a => !a.EventOnly).Select(a => a.VenueId).ToHashSet();
            var options = S.City.PublicPlaces.Where(p => S.DistrictAccessBlocker(p) == null)
                .Select(p => BuildDestinationEntry(p, venues))
                .OrderBy(p => p.Place.Kind == PlaceKind.Home && !S.CanSleepAt(p.Place))
                .ThenBy(p => p.Street).ThenBy(p => p.Name).ToArray();
            var recent = S.CityRecent.Select(id => options.FirstOrDefault(p => p.Place.Id == id))
                .Where(p => p != null).Distinct().ToArray();
            var pinned = SelectedShortcut(3);
            var selected = options.FirstOrDefault(p => p.Place == pinned) ?? recent.FirstOrDefault() ?? options.FirstOrDefault();
            var streets = options.Select(p => p.Place.StreetId).Distinct().OrderBy(S.City.StreetName).ToArray();

            var filterRow = Ui.Row(w.Content, 44, 8);
            var search = Ui.Size(Ui.TextInput(filterRow, "", "搜地点名、类型或街区", null), 0, 44, 1);
            search.name = "DestinationSearch";
            Action refreshFilters = null;
            var type = Ui.Size(Ui.Choice(filterRow, DestinationFilters.Select(f => f.Label), 0, _ => refreshFilters?.Invoke()), 175, 44);
            type.name = "DestinationType";
            var street = Ui.Size(Ui.Choice(filterRow, new[] { "全部街区" }.Concat(streets.Select(S.City.StreetName)),
                0, _ => refreshFilters?.Invoke()), 238, 44);
            street.name = "DestinationStreet";

            var columns = Ui.Row(w.Content, -1, 12);
            columns.GetComponent<LayoutElement>().flexibleHeight = 1;
            var list = Ui.Scroll(columns);
            list.name = "DestinationResults";
            var scroll = list.parent.parent.GetComponent<ScrollRect>();
            Ui.Size(scroll, 0, -1, 1.55f);

            var detail = Ui.Size(Ui.Panel(columns, "DestinationDetails", Ui.FlatWell), 0, -1, 1);
            Ui.VList(detail.rectTransform, 8, 14);
            var category = Line(detail.transform, "", 16, Ui.Accent);
            var name = Ui.Size(Ui.Label(detail.transform, "", 25, TextAnchor.MiddleLeft), -1, 60);
            name.name = "DestinationName";
            name.fontStyle = FontStyle.Bold;
            var address = Line(detail.transform, "", 17, Ui.Dim);
            var hours = Para(detail.transform, "", 18);
            hours.name = "DestinationHours";
            var travel = Para(detail.transform, "", 18);
            travel.name = "DestinationTravel";
            Ui.Size(Ui.Rect(detail.transform, "ActionSpacing"), -1, 0).GetComponent<LayoutElement>().flexibleHeight = 1;

            var go = Ui.Size(Ui.Button(detail.transform, "立即前往", null, 20, Ui.ButtonHot), -1, 46);
            go.name = "DestinationGo";
            var save = Ui.Size(Ui.Button(detail.transform, "保存到底栏", null, 18), -1, 42);
            save.name = "DestinationSave";
            var saveLabel = save.GetComponentInChildren<Text>();
            var buttons = new Dictionary<DestinationEntry, Image>();
            go.onClick.AddListener(() =>
            {
                if (S.CityDestinationTravelBlocker(selected?.Place, out _, out _) != null)
                {
                    RefreshDetail();
                    return;
                }
                Place destination = selected.Place;
                CloseAllWindows();
                _ctrl.GoToPlace(destination);
            });
            save.onClick.AddListener(() =>
            {
                if (selected == null) return;
                S.SetCityDefault(selected.Place.Id);
                RefreshShortcutMenus();
                RefreshDetail();
            });
            refreshFilters = RefreshResults;
            search.onValueChanged.AddListener(_ => RefreshResults());
            RefreshResults();

            void RefreshResults()
            {
                var terms = search.text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                var filter = DestinationFilters[type.value].Type;
                string streetId = street.value > 0 ? streets[street.value - 1] : null;
                bool filtered = terms.Length > 0 || filter != DestinationType.None || streetId != null;
                var visible = options.Where(p => (filter == DestinationType.None || (p.Types & filter) != 0)
                    && (streetId == null || p.Place.StreetId == streetId)
                    && terms.All(term => p.SearchText.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
                if (!visible.Contains(selected)) selected = visible.Length == 1 ? visible[0] : null;

                Ui.Clear(list);
                buttons.Clear();
                if (visible.Length == 0)
                {
                    Line(list, "没有符合当前筛选的地点。", 18, Ui.Dim);
                    Ui.Size(Ui.Button(list, "清空筛选", () =>
                    {
                        search.SetTextWithoutNotify("");
                        type.SetValueWithoutNotify(0);
                        street.SetValueWithoutNotify(0);
                        RefreshResults();
                    }, 17), -1, 40).name = "DestinationClearFilters";
                }
                else if (filtered) AddGroup($"匹配地点 · {visible.Length}", visible);
                else
                {
                    AddGroup("最近前往", recent);
                    AddGroup("其他地点", visible.Except(recent));
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(list);
                scroll.verticalNormalizedPosition = 1;
                RefreshDetail();
            }

            void AddGroup(string title, IEnumerable<DestinationEntry> entries)
            {
                bool first = true;
                foreach (var entry in entries)
                {
                    if (first) { Line(list, title, 16, Ui.Accent); first = false; }
                    var button = Ui.Size(Ui.Button(list, $"<b>{entry.Name}</b>\n<size=15>{entry.Street} · {entry.Label}</size>", () =>
                    {
                        selected = entry;
                        RefreshDetail();
                    }, 19), -1, 54);
                    button.name = "DestinationRow_" + entry.Place.Id;
                    var label = button.GetComponentInChildren<Text>();
                    label.alignment = TextAnchor.MiddleLeft;
                    label.rectTransform.Fill(10);
                    buttons.Add(entry, button.image);
                }
            }

            void RefreshDetail()
            {
                foreach (var button in buttons) button.Value.color = button.Key == selected ? Ui.ButtonHot : Ui.ButtonBg;
                var place = selected?.Place;
                category.text = selected?.Label ?? "";
                name.text = selected?.Name ?? "选择一个地点";
                address.text = selected?.Street ?? "";
                hours.gameObject.SetActive(place != null);
                travel.gameObject.SetActive(place != null);
                if (place != null)
                {
                    hours.text = DestinationOpeningText(place);
                    string blocker = S.CityDestinationTravelBlocker(place, out int minutes, out var fare);
                    travel.text = "出行 · " + (blocker ?? $"{S.CurrentVehicle.Name} · 约 {Ui.Emphasize($"{minutes} 分钟")}"
                        + (fare.IsPositive ? $" · {Ui.Emphasize(fare.ToString())}" : ""));
                    travel.color = blocker == null || S.CurrentPlace == place ? Ui.TextColor : Ui.Warn;
                    go.interactable = blocker == null;
                }
                else go.interactable = false;
                bool saved = place != null && SelectedShortcut(3) == place;
                save.interactable = place != null && !saved;
                saveLabel.text = saved ? "已保存到底栏" : "保存到底栏";
            }
        }

        private DestinationEntry BuildDestinationEntry(Place place, HashSet<string> venues)
        {
            var types = DestinationType.None;
            if (S.IsMyShop(place)) types |= DestinationType.Shop;
            if (place.Kind == PlaceKind.Wholesaler || S.HasRetailWholesale(place)
                || place.Business is GameSession.MallBusiness or GameSession.FarmBusiness) types |= DestinationType.Supply;
            if (place.Kind == PlaceKind.Home) types |= DestinationType.Residence;
            if (venues.Contains(place.Id) || RaceTracks.Get(place.Id) != null || place.Kind == PlaceKind.Garage)
                types |= DestinationType.Leisure;
            if (S.PropertyAt(place.Id) != null) types |= DestinationType.Property;
            if (place.Kind is PlaceKind.Agency or PlaceKind.Station || place.Business is GameSession.BankBusiness or GameSession.HospitalBusiness)
                types |= DestinationType.Service;

            var labels = DestinationFilters.Where(f => (types & f.Type) != 0).Select(f => f.Label);
            string name = place == S.Home ? S.HomeTitle : ShortcutPlaceName(3, place);
            string street = S.City.StreetName(place.StreetId);
            string label = labels.FirstOrDefault() ?? (place.Kind == PlaceKind.Dealer ? "车行" : "地点");
            string terms = place.Business switch
            {
                GameSession.BankBusiness => "存款 贷款 还款",
                GameSession.MallBusiness => "家具 货架 收银台",
                "venue_court" => "篮球 打球",
                _ when place.Kind == PlaceKind.Wholesaler => "批发 进货",
                _ when place.Kind == PlaceKind.Home => "回家 住宅",
                _ => ""
            };
            string shopType = S.ShopAt(place) is OwnedShop shop ? RetailShopTypes.Name(shop.RetailKind) : "";
            return new DestinationEntry { Place = place, Name = name, Street = street, Label = label, Types = types,
                SearchText = string.Join(" ", name, place.Name, street, shopType, terms, string.Join(" ", labels)) };
        }

        private string DestinationOpeningText(Place place)
        {
            if (place.Kind == PlaceKind.Home)
                return "到访 · " + (S.CanSleepAt(place) ? "可回家" : S.ClosedReason(place) ?? "可拜访");
            string hours = S.IsMyShop(place) ? CityPlaceHours(place) : S.OpeningHoursText(place) ?? CityPlaceHours(place);
            string status = S.IsPlaceOpen(place) ? "开放中" : "当前未开放";
            return $"营业 · {status}\n{hours}";
        }
    }
}
