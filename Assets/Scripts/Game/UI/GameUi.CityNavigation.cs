using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using PCShopSell.Core.World;
using UnityEngine.UI;
using PCShopSell.Core.Session;
namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private sealed class ShortcutMenu
        {
            public int Kind;
            public ChoiceDropdown Dropdown;
            public Button Main;
            public Text Caption;
            public RectTransform Group;
            public float MinimumWidth;
            public Place[] Places=System.Array.Empty<Place>();
            public string Stamp;
        }
        private readonly List<ShortcutMenu> _shortcutMenus=new List<ShortcutMenu>();
        private bool _shortcutSelectionExplained;
        private const int OwnedShopShortcut = 0;
        private const int SleepShortcut = 4;
        private const float ShortcutCaptionPadding = 8;
        // 0: owned shop, 1: wholesale sources, 2: agencies, 3: custom destination, 4: residence.
        private Place[] ShortcutPlaces(int kind)
        {
            if(S==null)return System.Array.Empty<Place>();
            if (kind == SleepShortcut)
                return new[] { S.Home }.Concat(S.RentedResidences)
                    .Concat(S.OwnedProperties.Where(p => S.City.HasPlace(p.PlaceId)).Select(p => S.City.GetPlace(p.PlaceId)))
                    .Where(p => p.Kind == PlaceKind.Home).Distinct()
                    .OrderBy(p => p == S.Home ? -1 : S.DistrictTierOf(p))
                    .ThenBy(p => p.StreetId).ThenBy(p => p.Name).ToArray();
            if(kind==3)return S.City.PublicPlaces.ToArray();
            if (kind == OwnedShopShortcut)
                return S.MyShops.Select(shop => shop.Place)
                    .Concat(S.ManagedVentures.Where(venture => S.City.HasPlace(venture.PlaceId)).Select(venture => S.City.GetPlace(venture.PlaceId)))
                    .Distinct().OrderBy(place => S.DistrictTierOf(place)).ThenBy(place => place.StreetId).ThenBy(place => place.Name).ToArray();
            if(kind==2)return S.City.Places.Where(p=>p.Kind==PlaceKind.Agency).ToArray();
            return S.City.Places.Where(p=>p.Id=="stall_cpu"||p.Business==GameSession.FarmBusiness||S.HasRetailWholesale(p)).ToArray();
        }
        private string ShortcutSelection(int kind) => kind switch
        {
            OwnedShopShortcut => S.CityShopShortcut,
            1 => S.CityMarketShortcut,
            3 => S.CityDefaultDestination,
            SleepShortcut => S.CityHomeShortcut,
            _ => S.CityAgencyShortcut
        };
        private Place SelectedShortcut(int kind, Place[] places = null)
        {
            places ??= ShortcutPlaces(kind);
            string id = ShortcutSelection(kind);
            if (kind == 3 && S.City.HasPlace(id) && S.City.GetPlace(id) is Place counter && counter.IsElectronicsCounter)
                return places.FirstOrDefault(p => p.Kind == PlaceKind.Wholesaler && p.NodeId == counter.NodeId);
            if (kind == SleepShortcut) return places.FirstOrDefault(p => p.Id == id && S.CanSleepAt(p)) ?? S.Home;
            if (kind == OwnedShopShortcut && !places.Any(p => p.Id == id) && S.MainShop.Rented && S.DistrictAccessBlocker(S.MainShop.Place) == null)
                return S.MainShop.Place;
            return places.FirstOrDefault(p => p.Id == id)
                ?? (kind == 3 ? null : places.FirstOrDefault(p => S.DistrictAccessBlocker(p) == null));
        }
        // Already being inside still allows reopening the service or walking to the bed.
        private string ShortcutTravelBlocker(Place place) =>
            S.CityDestinationTravelBlocker(place, out _, out _, allowCurrentPlace: true);
        private string HomeShortcutBlocker(Place place) =>
            S.DistrictAccessBlocker(place) ?? (!S.CanSleepAt(place) ? "有租户，当前不可入住" : ShortcutTravelBlocker(place));

        private string ShortcutBlocker(int kind, Place place) =>
            kind == SleepShortcut ? HomeShortcutBlocker(place) : ShortcutTravelBlocker(place);
        private void SelectShortcut(int kind,Place place)
        {
            if(kind==OwnedShopShortcut)S.SetCityShopShortcut(place.Id);
            else if(kind==SleepShortcut)S.SetCityHomeShortcut(place);
            else S.SetCityShortcut(place.Id,kind==2);
            RefreshShortcutMenus();
            if(!_shortcutSelectionExplained)
            {
                _shortcutSelectionExplained=true;
                Toast("已选目的地："+ShortcutPlaceName(kind,place)+"。点击底栏主按钮出发。");
            }
        }
        private void TravelShortcut(int kind)
        {
            var target=SelectedShortcut(kind);
            if(target==null&&kind==3){OpenCustomDestinationPicker();return;}
            if(ShortcutBlocker(kind,target) is string blocker){Toast(blocker);return;}
            if (kind == SleepShortcut) _ctrl.GoHomeToSleep(target);
            else _ctrl.GoToPlace(target);
        }
        private void BuildShortcutButton(Transform parent,string label,int kind,float width)
        {
            var group=Ui.Row(parent,42,1);group.name="Shortcut_"+kind;Ui.Size(group,width+27,42);
            var main=Ui.Button(group,label,()=>TravelShortcut(kind),16);
            main.name="ShortcutGo_"+kind;Ui.Size(main,width,42);
            var caption=main.GetComponentInChildren<Text>();
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            caption.supportRichText = false;
            caption.rectTransform.Anchor(0, 0, 1, 1, ShortcutCaptionPadding, 3, ShortcutCaptionPadding, 3);
            main.gameObject.AddComponent<RectMask2D>().padding = new Vector4(ShortcutCaptionPadding, 0, ShortcutCaptionPadding, 0);
            main.Tip(() =>
            {
                if (kind == OwnedShopShortcut && S.MyShopCount == 0 && S.ManagedVentures.Count == 0) return "尚无经营中的商铺。";
                var place = SelectedShortcut(kind);
                string address = place == null ? "暂无" : ShortcutPlaceAddress(kind, place);
                string tip = "当前目的地：" + address;
                if (place != null && ShortcutBlocker(kind, place) is string blocker) return tip + "\n" + blocker;
                return tip + (kind == SleepShortcut
                    ? "\n点主按钮前往并走到床边，右侧小三角可切换住宅。"
                    : "\n点主按钮直接前往；右侧小三角可切换目的地。");
            });
            var menu=new ShortcutMenu{Kind=kind,Main=main,Caption=caption,Group=group,MinimumWidth=width};
            if(kind==3)
            {
                var arrow=Ui.Size(Ui.Button(group,"▾",OpenCustomDestinationPicker,17),26,42);
                arrow.name="ShortcutArrow_3";
                arrow.Tip("搜索目的地、立即前往或保存到底栏。");
                _shortcutMenus.Add(menu);return;
            }
            var dropdown=Ui.Choice(group,new[]{"暂无目的地"},0,index=>
            {
                if (index < 0 || index >= menu.Places.Length) return;
                var place = menu.Places[index];
                if (place == null) return;
                if (ShortcutBlocker(kind, place) == null) SelectShortcut(kind, place);
                else menu.Dropdown.SetValueWithoutNotify(ShortcutOptionIndex(menu.Places, SelectedShortcut(kind)));
            });
            dropdown.OptionHeader = index => index < menu.Places.Length && menu.Places[index] == null;
            dropdown.OptionBlocker = index => index < menu.Places.Length && menu.Places[index] is Place place
                ? ShortcutBlocker(kind, place) : null;
            menu.Dropdown=dropdown;dropdown.name="ShortcutArrow_"+kind;Ui.Size(dropdown,26,42);
            dropdown.captionText.gameObject.SetActive(false);
            var item = dropdown.itemText;
            item.rectTransform.Anchor(0, 0, 1, 1, 14, 6, 10, 6);
            item.fontSize = 17;
            item.resizeTextForBestFit = true;
            item.resizeTextMinSize = 14;
            item.resizeTextMaxSize = 17;
            item.verticalOverflow = VerticalWrapMode.Truncate;

            // Open above the bottom bar without changing the width of the shortcut button.
            var template=dropdown.template;
            template.anchorMin=template.anchorMax=new Vector2(1,1);template.pivot=new Vector2(1,0);
            bool addressList = kind is SleepShortcut or OwnedShopShortcut;
            template.anchoredPosition=new Vector2(0,4);template.sizeDelta=new Vector2(addressList ? 455 : 360,addressList ? 350 : 260);
            dropdown.Tip("选择目的地（只切换，不立即出发）");
            _shortcutMenus.Add(menu);
        }
        private string ShortcutPlaceName(int kind,Place place)
        {
            if (kind == SleepShortcut) return place == S.Home ? S.HomeTitle : place?.Name ?? "回家睡觉";
            if(place==null)return kind==3?"自定目的地":kind==OwnedShopShortcut?"我的店铺":kind==1?"批发市场":"中介";
            if(S.ShopAt(place) is OwnedShop shop)return S.ShopTitle(shop);
            if (kind == OwnedShopShortcut && S.VentureAt(place.Id) != null) return S.PlaceTitle(place);
            return place.Id=="stall_cpu"?"电子批发市场":place.Name;
        }
        private string ShortcutPlaceAddress(int kind, Place place)
        {
            string name = ShortcutPlaceName(kind, place);
            string street = S.City.StreetName(place.StreetId);
            if (!S.IsMyShop(place) && name != place.Name) name += "\n" + place.Name;
            return name.Contains(street) ? name : name + "\n" + street;
        }

        private static string NameWithoutStreetPrefix(string name, string street)
        {
            string prefix = street + " · ";
            return name.StartsWith(prefix, System.StringComparison.Ordinal) ? name.Substring(prefix.Length) : name;
        }

        private string[] ShortcutOptionLabels(int kind, Place[] places)
        {
            if (kind is SleepShortcut or OwnedShopShortcut)
                return places.Select((place, index) =>
                {
                    if (place == null)
                    {
                        string streetId = places[index + 1].StreetId;
                        string district = S.City.AgencyDistrictFor(streetId)?.name;
                        string address = S.City.StreetName(streetId);
                        return string.IsNullOrEmpty(district) ? address : district + " · " + address;
                    }
                    string name = ShortcutPlaceName(kind, place), street = S.City.StreetName(place.StreetId);
                    if (kind == OwnedShopShortcut)
                        return name == place.Name ? NameWithoutStreetPrefix(name, street)
                            : name + " · " + NameWithoutStreetPrefix(place.Name, street);
                    string status = place == S.Home ? "主住所" : S.DistrictAccessBlocker(place) != null ? "片区未开放"
                        : !S.CanSleepAt(place) ? "有租户" : S.HasResidentialLease(place) ? "已租" : "已购";
                    return NameWithoutStreetPrefix(name, street) + " · " + status;
                }).ToArray();
            var names = places.Select(place => ShortcutPlaceName(kind, place)).ToArray();
            var duplicates = new HashSet<string>(names.GroupBy(name => name)
                .Where(group => group.Count() > 1).Select(group => group.Key));
            return places.Select((place, index) =>
            {
                string name = names[index];
                if (duplicates.Contains(name))
                    name += " · " + (S.IsMyShop(place) || name == place.Name ? S.City.StreetName(place.StreetId) : place.Name);
                return name;
            }).ToArray();
        }

        private static int ShortcutOptionIndex(Place[] places, Place destination) =>
            Mathf.Max(0, System.Array.IndexOf(places, destination));

        private static void FitShortcutCaption(ShortcutMenu menu, string name, bool hasDestination)
        {
            var label = menu.Caption;
            var settings = label.GetGenerationSettings(Vector2.zero);
            float Measure(string value) => label.cachedTextGeneratorForLayout.GetPreferredWidth(value, settings) / label.pixelsPerUnit;
            float maxWidth = menu.Kind == SleepShortcut ? 160 : 172;
            const float gutter = ShortcutCaptionPadding * 2 + 4;
            float maxTextWidth = maxWidth - gutter;
            name = name.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            string caption = hasDestination ? "前往 " + name : name;
            float preferred = Measure(caption);
            if (preferred > maxTextWidth && hasDestination)
            {
                caption = name;
                preferred = Measure(caption);
            }
            if (preferred > maxTextWidth)
            {
                caption = "…";
                var characterStarts = StringInfo.ParseCombiningCharacters(name);
                for (int i = characterStarts.Length - 1; i > 0; i--)
                {
                    string candidate = name.Substring(0, characterStarts[i]).TrimEnd() + "…";
                    if (Measure(candidate) > maxTextWidth) continue;
                    caption = candidate;
                    break;
                }
            }
            label.text = caption;
            float width = Mathf.Clamp(Mathf.Ceil(preferred) + gutter, Mathf.Max(100, menu.MinimumWidth), maxWidth);
            Ui.Size(menu.Main, width, 42);
            Ui.Size(menu.Group, width + 27, 42);
        }

        private void RefreshShortcutMenus()
        {
            if(S==null)return;
            foreach(var menu in _shortcutMenus)
            {
                var places=ShortcutPlaces(menu.Kind);
                var destination=SelectedShortcut(menu.Kind, places);
                if (menu.Kind is SleepShortcut or OwnedShopShortcut)
                    places = places.GroupBy(place => place.StreetId).SelectMany(group => new Place[] { null }.Concat(group)).ToArray();
                bool canTravel = destination == null ? menu.Kind == 3 : ShortcutBlocker(menu.Kind, destination) == null;
                if (menu.Stamp == null || menu.Main.interactable != canTravel)
                {
                    menu.Main.interactable = canTravel;
                    if (menu.Kind == OwnedShopShortcut)
                        Ui.SetButtonFill(menu.Main, canTravel ? Ui.ButtonHot : Ui.ButtonBg);
                }
                string selected=destination?.Id;
                string name = destination == null && menu.Kind == 3 ? "选择目的地" : ShortcutPlaceName(menu.Kind, destination);
                if (destination?.Kind == PlaceKind.Home)
                    name = NameWithoutStreetPrefix(name, S.City.StreetName(destination.StreetId));
                var labels = menu.Dropdown == null ? System.Array.Empty<string>() : ShortcutOptionLabels(menu.Kind, places);
                string stamp=string.Join("|",places.Select(p=>p?.Id))+"/"+string.Join("|",labels)+"/"+selected+"/"+name+"/"+menu.Caption.fontStyle;
                if (menu.Stamp == stamp)
                {
                    menu.Dropdown?.RefreshOptionAvailability();
                    continue;
                }
                menu.Stamp=stamp;menu.Places=places;
                FitShortcutCaption(menu, name, destination != null);
                if(menu.Dropdown==null)continue;
                menu.Dropdown.ClearOptions();
                menu.Dropdown.AddOptions(places.Length==0?new List<string>{"暂无目的地"}:labels.ToList());
                menu.Dropdown.SetValueWithoutNotify(ShortcutOptionIndex(places, destination));
                menu.Dropdown.RefreshShownValue();menu.Dropdown.interactable=places.Length>0;
                menu.Dropdown.RefreshOptionAvailability();
            }
        }
        private void OpenCityPlacePeople(PCShopSell.Core.World.Place place)
        {
            OpenWindow(S.PlaceTitle(place)+" · 在场人物",760,650,w=>
            {
                var people=S.PeopleInPlace(place.Id).ToArray();
                if(people.Length==0)Para(w.Content,"此刻没有可交谈的人在这里。人员按实际日程和活动到场。",20);
                foreach(var p in people){string id=p.PersonId;var def=S.CityPerson(id);if(def==null)continue;
                    OrgNode(w.Content, id, def.Name, S.CityPersonRole(id), p.WorkPlaceId == place.Id, false, () => OpenCityPerson(id),
                        tooltip: CanTalkToScenePerson(id) ? "当面交谈" : "查看人物资料");}
            });
        }
    }
}
