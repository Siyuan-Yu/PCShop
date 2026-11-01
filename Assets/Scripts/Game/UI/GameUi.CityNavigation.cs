using System.Linq;
using UnityEngine.UI;
using PCShopSell.Core.Session;
namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private void GoCityShortcut(bool agency)
        {
            string id=agency?S.CityAgencyShortcut:S.CityMarketShortcut;
            if(S.City.HasPlace(id))_ctrl.GoToPlace(S.City.GetPlace(id));else{Toast("快捷目的地失效，请在地图中重新设置。");OpenCityMap();}
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
