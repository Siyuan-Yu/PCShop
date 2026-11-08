using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        // The purchase order owns this route. Its timestamps are the same promises used for
        // delivery/return, so restoring a view cannot create another shopper or another shipment.
        private void ApplyCityPurchasePosition(EmployeePurchaseTrip trip,CityPersonState person,long now)
        {
            if(trip.TravelLegs==null||trip.TravelLegs.Length==0)
                trip.TravelLegs=BuildCityPurchaseLegs(trip,person);
            var legs=trip.TravelLegs;
            if(legs.Length==0){person.TaskLabel="采购路线暂不可达";return;}
            CityRoute current=null,last=null;
            foreach(var leg in legs)
            {
                if(now<leg.DepartMinute)break;
                last=leg;
                if(now<leg.ArriveMinute){current=leg;break;}
            }
            var position=(current??last??legs[0]).Position(now);
            person.X=position.X;person.Y=position.Y;
            person.Route=current;
            person.LocationPlaceId=current!=null?null:last?.TargetPlaceId;
            person.Task=now>=trip.ReturnAt?CityPersonTask.Waiting:current!=null?CityPersonTask.Travelling:CityPersonTask.Shopping;
            person.TaskLabel=now>=trip.ReturnAt?"采购返店":now>=trip.BoughtAt?"采购完成，返店途中":current!=null?"按清单前往供货商":"在供货商处核对与提货";
        }

        private void NormalizePurchaseWalkingLegs(EmployeePurchaseTrip trip,CityRoute[] legs)
        {
            if(trip.Transport!=VehicleKind.Walk||legs==null||legs.Length==0)return;
            long delay=0,oldBought=trip.BoughtAt;
            foreach(var leg in legs)
            {
                long arrival=leg.ArriveMinute;
                leg.DepartMinute+=delay;leg.ArriveMinute+=delay;
                EnforceNpcWalkingPace(leg);
                delay=leg.ArriveMinute-arrival;
            }
            trip.BoughtAt=Math.Max(trip.BoughtAt,legs[legs.Length-1].DepartMinute);
            trip.ReturnAt=Math.Max(trip.ReturnAt,legs[legs.Length-1].ArriveMinute);
            if(!trip.Delivered&&trip.BoughtAt>oldBought)
            {
                trip.DeliveryAt+=trip.BoughtAt-oldBought;
                var order=_cityProcurementOrders.FirstOrDefault(o=>o.Id==trip.OrderId);
                if(order!=null)order.DeliveryAt=trip.DeliveryAt;
            }
        }
        private CityRoute[] BuildCityPurchaseLegs(EmployeePurchaseTrip trip,CityPersonState person)
        {
            var shop=DeliveryShop(trip.ShopId);if(shop==null)return Array.Empty<CityRoute>();
            var origins=(trip.Items??Array.Empty<PurchaseLine>()).Where(i=>i!=null&&City.HasPlace(i.SourceId)).GroupBy(i=>i.SourceId).ToArray();
            if(origins.Length==0)return Array.Empty<CityRoute>();
            var probe=person.Copy();var home=StandOf(shop.Place);
            probe.X=home.X;probe.Y=home.Y;probe.LocationPlaceId=shop.Place.Id;
            var routes=new List<CityRoute>();var offsets=new List<(long start,long end)>();
            long offset=0;
            var worker=FindEmployee(trip.EmployeeId)?.Person;
            int handling=worker==null?3:EmployeePurchaseMinutes(worker);
            foreach(var source in origins)
            {
                var route=PlanCityRoute(probe,source.Key,0);if(route==null)return Array.Empty<CityRoute>();
                long duration=Math.Max(1,CityTravelTiming.NpcMinutes(route.Meters.Sum(),Vehicles.Get(trip.Transport)));
                offsets.Add((offset,offset+duration));offset+=duration+handling*source.Count();routes.Add(route);
                var position=StandOf(City.GetPlace(source.Key));probe.X=position.X;probe.Y=position.Y;probe.LocationPlaceId=source.Key;
            }
            // Legacy orders did not retain a departure. Derive it once without changing their
            // already-promised purchase, delivery or return dates.
            if(trip.DepartAt<=0)trip.DepartAt=Math.Max(0,trip.BoughtAt-offset);
            long span=Math.Max(1,trip.BoughtAt-trip.DepartAt);
            for(int i=0;i<routes.Count;i++)
            {
                routes[i].IntentId="purchase:"+trip.OrderId+":"+trip.EmployeeId+":"+i;
                routes[i].Transport=trip.Transport;
                routes[i].DepartMinute=trip.DepartAt+offsets[i].start*span/Math.Max(1,offset);
                routes[i].ArriveMinute=trip.DepartAt+offsets[i].end*span/Math.Max(1,offset);
            }
            var back=PlanCityRoute(probe,shop.Place.Id,trip.BoughtAt);if(back==null)return Array.Empty<CityRoute>();
            back.IntentId="purchase:"+trip.OrderId+":"+trip.EmployeeId+":return";
            back.Transport=trip.Transport;
            back.DepartMinute=trip.BoughtAt;back.ArriveMinute=trip.ReturnAt;routes.Add(back);
            var result=routes.ToArray();
            NormalizePurchaseWalkingLegs(trip,result);
            return result;
        }
    }
}
