using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Shop;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public void SaveCityVisits(SaveData d)
        {
            d.cityVisitsVersion = 1;
            d.cityShopVisits = _shops.Where(s => s.Rented).Select(s => s.Sim.SaveCityCustomers()).ToArray();
        }
        public void LoadCityVisits(SaveData d)
        {
            var restoredShops = new HashSet<string>();
            if (d.cityVisitsVersion > 0)
                foreach (var data in d.cityShopVisits ?? Array.Empty<CitySavedShopVisits>())
                {
                    if (data?.shopId == null) continue;
                    if (!restoredShops.Add(data.shopId)) throw new InvalidOperationException("同一门店的来访存档重复：" + data.shopId);
                    var shop = _shops.FirstOrDefault(s => s.Rented && s.Place.Id == data.shopId);
                    shop?.Sim.RestoreCityCustomers(data);
                }
            var alive = new HashSet<string>();
            foreach (var shop in _shops)
                foreach (var c in shop.Sim.ActiveCustomers)
                {
                    if (string.IsNullOrEmpty(c.PersonId) || string.IsNullOrEmpty(c.VisitId)) continue;
                    var p = CityPersonState(c.PersonId);
                    if (p == null) throw new InvalidOperationException("顾客存档缺少人物身份，保留原档以便恢复：" + c.PersonId);
                    if (!alive.Add(c.VisitId)) throw new InvalidOperationException("同一次来访重复占用多个门店：" + c.VisitId);
                    if (p.VisitId != null && p.VisitId != c.VisitId) throw new InvalidOperationException("同一个人存在重复来访：" + c.PersonId);
                    p.VisitId = c.VisitId; p.Task = CityPersonTask.Shopping;
                    if (c.State == CustomerState.WaitingCheckout) { p.LocationPlaceId = shop.Place.Id; p.Route = null; }
                }
            foreach (var p in _cityPeople.Values)
                if (p.VisitId != null && !alive.Contains(p.VisitId))
                {
                    p.VisitId = null; p.Task = CityPersonTask.Returning; p.TaskLabel = "旧来访已恢复结束，按日程继续生活";
                }
        }
    }
}
