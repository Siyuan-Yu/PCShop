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
            public Place[] Places=System.Array.Empty<Place>();
            public string Stamp;
        }
        private readonly List<ShortcutMenu> _shortcutMenus=new List<ShortcutMenu>();
        // 0: owned shop, 1: wholesale sources, 2: agencies.
        private Place[] ShortcutPlaces(int kind)
        {
            if(S==null)return System.Array.Empty<Place>();
            if(kind==0)return S.MyShops.Select(s=>s.Place).ToArray();
            if(kind==2)return S.City.Places.Where(p=>p.Kind==PlaceKind.Agency).ToArray();
            return S.City.Places.Where(p=>p.Id=="stall_cpu"||S.HasRetailWholesale(p)).ToArray();
        }
        private string ShortcutSelection(int kind)=>kind==0?S.CityShopShortcut:kind==1?S.CityMarketShortcut:S.CityAgencyShortcut;
        private Place SelectedShortcut(int kind)
        {
            var places=ShortcutPlaces(kind);
            return places.FirstOrDefault(p=>p.Id==ShortcutSelection(kind))??places.FirstOrDefault();
        }
        private void SelectShortcut(int kind,Place place)
        {
            if(kind==0)S.SetCityShopShortcut(place.Id);
            else S.SetCityShortcut(place.Id,kind==2);
            RefreshShortcutMenus();
        }
        private void TravelShortcut(int kind)
        {
            var target=SelectedShortcut(kind);
            if(target==null&&kind==0)target=SelectedShortcut(2);
            if(target==null){Toast("当前没有可前往的地点。");return;}
            _ctrl.GoToPlace(target);
        }
        private void BuildShortcutButton(Transform parent,string label,int kind,float width)
        {
            var group=Ui.Row(parent,42,1);group.name="Shortcut_"+kind;Ui.Size(group,width+27,42);
            var main=Ui.Button(group,label,()=>TravelShortcut(kind),17,kind==0?Ui.ButtonHot:Ui.ButtonBg);
            main.name="ShortcutGo_"+kind;Ui.Size(main,width,42);
            if(kind==0)_shopGoText=main.GetComponentInChildren<Text>();
            main.Tip(()=>"当前目的地："+(SelectedShortcut(kind)?.Name??(kind==0?"尚无店铺，前往中介":"暂无"))+"\n点主按钮直接前往；右侧小三角可切换目的地。");
            var menu=new ShortcutMenu{Kind=kind};
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
        private void RefreshShortcutMenus()
        {
            if(S==null)return;
            foreach(var menu in _shortcutMenus)
            {
                var places=ShortcutPlaces(menu.Kind);
                string selected=SelectedShortcut(menu.Kind)?.Id;
                var labels=places.Select(p=>(p.Id=="stall_cpu"?"电子批发市场":p.Name)+" · "+S.City.StreetName(p.StreetId)).ToArray();
                string stamp=string.Join("|",places.Select(p=>p.Id))+"/"+string.Join("|",labels)+"/"+selected;
                if(menu.Stamp==stamp)continue;
                menu.Stamp=stamp;menu.Places=places;
                menu.Dropdown.ClearOptions();
                menu.Dropdown.AddOptions(places.Length==0?new List<string>{"暂无目的地"}:labels.ToList());
                menu.Dropdown.SetValueWithoutNotify(Mathf.Max(0,System.Array.FindIndex(places,p=>p.Id==selected)));
                menu.Dropdown.RefreshShownValue();menu.Dropdown.interactable=places.Length>0;
            }
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
