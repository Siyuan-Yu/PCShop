using System.Linq;
using System.Collections.Generic;
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
            public Dropdown Dropdown;
            public Button Main;
            public Text Caption;
            public RectTransform Group;
            public float MinimumWidth;
            public Place[] Places=System.Array.Empty<Place>();
            public string Stamp;
        }
        private readonly List<ShortcutMenu> _shortcutMenus=new List<ShortcutMenu>();
        private bool _shortcutSelectionExplained;
        private const int SleepShortcut = 4;
        // 0: owned shop, 1: wholesale sources, 2: agencies, 3: custom destination, 4: residence.
        private Place[] ShortcutPlaces(int kind)
        {
            if(S==null)return System.Array.Empty<Place>();
            if (kind == SleepShortcut)
                return new[] { S.Home }.Concat(S.OwnedProperties.Where(p => S.City.HasPlace(p.PlaceId)).Select(p => S.City.GetPlace(p.PlaceId))
                    .Where(p => p != S.Home && S.CanSleepAt(p))).ToArray();
            if(kind==3)return S.City.Places.ToArray();
            if(kind==0)return S.MyShops.Select(s=>s.Place).ToArray();
            if(kind==2)return S.City.Places.Where(p=>p.Kind==PlaceKind.Agency).ToArray();
            return S.City.Places.Where(p=>p.Id=="stall_cpu"||S.HasRetailWholesale(p)).ToArray();
        }
        private string ShortcutSelection(int kind) => kind switch
        {
            0 => S.CityShopShortcut,
            1 => S.CityMarketShortcut,
            3 => S.CityDefaultDestination,
            SleepShortcut => S.CityHomeShortcut,
            _ => S.CityAgencyShortcut
        };
        private Place SelectedShortcut(int kind, Place[] places = null)
        {
            places ??= ShortcutPlaces(kind);
            return places.FirstOrDefault(p=>p.Id==ShortcutSelection(kind))??(kind==3?null:places.FirstOrDefault());
        }
        private void SelectShortcut(int kind,Place place)
        {
            if(kind==3)S.SetCityDefault(place.Id);
            else if(kind==0)S.SetCityShopShortcut(place.Id);
            else if(kind==SleepShortcut)S.SetCityHomeShortcut(place);
            else S.SetCityShortcut(place.Id,kind==2);
            RefreshShortcutMenus();
            if(!_shortcutSelectionExplained)
            {
                _shortcutSelectionExplained=true;
                Toast("已选目的地："+place.Name+"。点击“前往”出发。");
            }
        }
        private void TravelShortcut(int kind)
        {
            var target=SelectedShortcut(kind);
            if(target==null&&kind==3){OpenCustomDestinationPicker();return;}
            if(target==null){Toast("当前没有可前往的地点。");return;}
            if (kind == SleepShortcut) _ctrl.GoHomeToSleep(target);
            else _ctrl.GoToPlace(target);
        }
        private void BuildShortcutButton(Transform parent,string label,int kind,float width)
        {
            var group=Ui.Row(parent,42,1);group.name="Shortcut_"+kind;Ui.Size(group,width+27,42);
            var main=Ui.Button(group,label,()=>TravelShortcut(kind),17,kind==0?Ui.ButtonHot:Ui.ButtonBg);
            main.name="ShortcutGo_"+kind;Ui.Size(main,width,42);
            var caption=main.GetComponentInChildren<Text>();
            caption.horizontalOverflow=HorizontalWrapMode.Wrap;
            caption.resizeTextForBestFit=true;caption.resizeTextMinSize=13;caption.resizeTextMaxSize=17;
            if (kind == SleepShortcut) main.Tip(() => "当前目的地：" + ShortcutPlaceName(kind, SelectedShortcut(kind))
                + "\n点主按钮前往并走到床边，右侧小三角可切换住宅。");
            else main.Tip(()=>kind==0&&S.MyShopCount==0?"尚无店铺，租下或购入店铺后可使用。":"当前目的地："+(SelectedShortcut(kind)?.Name??"暂无")+"\n点主按钮直接前往；右侧小三角可切换目的地。");
            var menu=new ShortcutMenu{Kind=kind,Main=main,Caption=caption,Group=group,MinimumWidth=width};
            if(kind==3)
            {
                var arrow=Ui.Size(Ui.Button(group,"▾",OpenCustomDestinationPicker,17),26,42);
                arrow.name="ShortcutArrow_3";
                arrow.Tip("打开地图选择目的地（只保存选择，不立即出发）");
                _shortcutMenus.Add(menu);return;
            }
            var dropdown=Ui.Choice(group,new[]{"暂无目的地"},0,index=>
            {
                if(index>=0&&index<menu.Places.Length)SelectShortcut(kind,menu.Places[index]);
            });
            menu.Dropdown=dropdown;dropdown.name="ShortcutArrow_"+kind;Ui.Size(dropdown,26,42);
            dropdown.captionText.gameObject.SetActive(false);
            // Bottom-bar choices expand upward and have enough width for street names.
            var template=dropdown.template;
            template.anchorMin=template.anchorMax=new Vector2(1,1);template.pivot=new Vector2(1,0);
            template.anchoredPosition=new Vector2(0,4);template.sizeDelta=new Vector2(330,260);
            dropdown.Tip("选择目的地（只切换，不立即出发）");
            _shortcutMenus.Add(menu);
        }
        private string ShortcutPlaceName(int kind,Place place)
        {
            if (kind == SleepShortcut) return place == S.Home ? S.HomeTitle : place?.Name ?? "回家睡觉";
            if(place==null)return kind==3?"自定目的地":kind==0?"我的店铺":kind==1?"批发市场":"中介";
            if((kind==0||kind==3) && S.ShopAt(place.Id) is OwnedShop shop)return S.ShopTitle(shop);
            return place.Id=="stall_cpu"?"电子批发市场":place.Name;
        }
        private void RefreshShortcutMenus()
        {
            if(S==null)return;
            foreach(var menu in _shortcutMenus)
            {
                var places=ShortcutPlaces(menu.Kind);
                var destination=SelectedShortcut(menu.Kind, places);
                string selected=destination?.Id;
                string caption=destination==null?(menu.Kind==3?"选择目的地":ShortcutPlaceName(menu.Kind,null)):"前往 "+ShortcutPlaceName(menu.Kind,destination);
                var labels=places.Select(p=>ShortcutPlaceName(menu.Kind,p)
                    + (menu.Kind == SleepShortcut && p == S.Home ? "（主住所）" : "")
                    + " · "+S.City.StreetName(p.StreetId)).ToArray();
                string stamp=string.Join("|",places.Select(p=>p.Id))+"/"+string.Join("|",labels)+"/"+selected+"/"+caption;
                if(menu.Stamp==stamp)continue;
                menu.Stamp=stamp;menu.Places=places;
                menu.Caption.text=caption;
                menu.Main.interactable=menu.Kind==3||destination!=null;
                menu.Caption.color=menu.Main.interactable?Ui.TextColor:Ui.Dim;
                // Keep room for the other shortcuts when all destinations have long names.
                float maxWidth = menu.Kind == SleepShortcut ? 124 : 172;
                float width=Mathf.Clamp(menu.Caption.preferredWidth+20,Mathf.Max(100,menu.MinimumWidth),maxWidth);
                Ui.Size(menu.Main,width,42);Ui.Size(menu.Group,width+27,42);
                if(menu.Dropdown==null)continue;
                menu.Dropdown.ClearOptions();
                menu.Dropdown.AddOptions(places.Length==0?new List<string>{"暂无目的地"}:labels.ToList());
                menu.Dropdown.SetValueWithoutNotify(Mathf.Max(0,System.Array.FindIndex(places,p=>p.Id==selected)));
                menu.Dropdown.RefreshShownValue();menu.Dropdown.interactable=places.Length>0;
            }
        }
        private void OpenCustomDestinationPicker()
        {
            OpenCityMap(place=>SelectShortcut(3,place));
        }
        private void OpenCityPlacePeople(PCShopSell.Core.World.Place place)
        {
            OpenWindow(place.Name+" · 在场人物",760,650,w=>
            {
                var people=S.PeopleInPlace(place.Id).ToArray();
                if(people.Length==0)Para(w.Content,"此刻没有可交谈的人在这里。人员按实际日程和活动到场。",20);
                foreach(var p in people){string id=p.PersonId;var def=S.CityPerson(id);if(def==null)continue;
                    OrgNode(w.Content, id, def.Name, S.CityPersonRole(id), p.WorkPlaceId == place.Id, false, () => OpenCityPerson(id));}
            });
        }
    }
}
