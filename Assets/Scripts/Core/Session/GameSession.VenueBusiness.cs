using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    [Serializable] public sealed class VenueCustomer
    {
        public string PersonId, ReservationId;
        public long Started=-1, Until, PriceCents;
        public VenueCustomer Copy()=>(VenueCustomer)MemberwiseClone();
    }
    [Serializable] public sealed class VenueBusiness
    {
        public string PlaceId;
        public bool Accepting;
        public int Open=540, Close=1260, Day, Customers, MaintenanceDay;
        public long PriceCents=3000, RevenueCents, TotalRevenueCents, LastPulse=-1;
        public VenueCustomer[] CustomersHere=Array.Empty<VenueCustomer>();
        public VenueBusiness Copy(){var copy=(VenueBusiness)MemberwiseClone();copy.CustomersHere=CustomersHere.Select(c=>c.Copy()).ToArray();return copy;}
    }
    public sealed partial class SaveData { public VenueBusiness[] venueBusinesses; }
    public sealed partial class GameSession
    {
        private readonly Dictionary<string,VenueBusiness> _venueBusinesses=new Dictionary<string,VenueBusiness>();
        public bool IsDedicatedVenue(Place p)=>(p?.Business??"").StartsWith("venue_",StringComparison.Ordinal);
        public bool IsOwnedVenue(Place p)=>IsDedicatedVenue(p)&&ShopAt(p)!=null;
        public string VenueBusinessName(Place p)
        {
            switch(p?.Business)
            {
                case "venue_court":return "篮球场";
                case "venue_golf":return "高尔夫练习场";
                case "venue_ktv":return "练歌房";
                case "venue_restaurant":return "家常餐馆";
                case "venue_bar":return "休闲吧";
                default:return p?.Name??"场馆";
            }
        }
        public int VenueCapacity(Place p)=>p?.Business=="venue_court"?6:p?.Business=="venue_golf"?3:12;
        public int VenueMaintenanceYuan(Place p)=>p?.Business=="venue_golf"?40:20;
        public VenueBusiness VenueOperation(string id)
        {
            var shop=ShopAt(id);if(shop==null||!IsDedicatedVenue(shop.Place))return null;
            if(!_venueBusinesses.TryGetValue(id,out var state))
            {
                var def=CityLifeCatalog.Activities.FirstOrDefault(a=>a.VenueId==id);
                state=new VenueBusiness{PlaceId=id,Open=shop.Place.OpenMinute,Close=shop.Place.CloseMinute,
                    PriceCents=(def?.PriceYuan??30)*100L,Day=Clock.Today.TotalDays,MaintenanceDay=Clock.Today.TotalDays};
                _venueBusinesses[id]=state;
            }
            return state;
        }
        private void ResetVenueLease(string id)
        {
            if(_venueBusinesses.TryGetValue(id,out var previous))foreach(var c in previous.CustomersHere)ReleaseVenueCustomer(c);
            _venueBusinesses.Remove(id);
        }
        public bool ConfigureVenue(string id,bool accepting,int open,int close,long priceCents)
        {
            var state=VenueOperation(id);
            if(state==null||IsGameOver||CurrentPlace?.Id!=id)return Fail("请到自己的场馆管理。");
            if(open<0||open>=1440||close<0||close>1440||priceCents<100||priceCents>100000)return Fail("请设置有效的营业时间和 ¥1—1,000 的单次价格。");
            state.Accepting=accepting;state.Open=open;state.Close=close;state.PriceCents=priceCents;return true;
        }
        public bool VenueHasAttendant(OwnedShop shop)=>shop!=null&&(
            CurrentPlace==shop.Place&&!CityActivityExclusive&&!IsWorking&&!_sleeping
            ||ShopStaff(shop,EmployeeRole.Cashier).Any(e=>CityEmployeeAvailable(e,shop)));
        public bool VenueOpen(Place place)
        {
            var state=VenueOperation(place?.Id);
            return state!=null&&state.Accepting&&!IsGameOver&&CityWithinHours(Clock.MinuteOfDay,state.Open,state.Close)&&VenueHasAttendant(ShopAt(place));
        }
        public Money VenuePlayerPrice(CityActivityDefinition def)=>def==null?Money.Zero:
            IsOwnedVenue(City.GetPlace(def.VenueId))?Money.Zero:Money.FromYuan(def.PriceYuan);
        private void ReleaseVenueCustomer(VenueCustomer customer)
        {
            var person=CityPersonState(customer.PersonId);
            if(person==null||person.ActivityId!=customer.ReservationId)return;
            if(person.Route!=null){var at=person.Route.Position(Clock.Now.TotalMinutes);person.X=at.X;person.Y=at.Y;person.Route=null;person.LocationPlaceId=null;}
            person.ActivityId=null;person.Task=CityPersonTask.Leisure;person.TaskLabel="场馆体验结束";
        }
        private void TickVenueBusinesses(long now)
        {
            foreach(var removed in _venueBusinesses.Keys.Where(id=>ShopAt(id)==null).ToArray())
            {foreach(var c in _venueBusinesses[removed].CustomersHere)ReleaseVenueCustomer(c);_venueBusinesses.Remove(removed);}
            foreach(var shop in MyShops.Where(s=>IsDedicatedVenue(s.Place)))
            {
                var state=VenueOperation(shop.Place.Id);int today=Clock.Today.TotalDays;
                if(state.Day!=today){state.Day=today;state.Customers=0;state.RevenueCents=0;}
                while(state.MaintenanceDay<today)
                {
                    state.MaintenanceDay++;
                    Ledger.ForceDebit(Clock.Today,"场馆维护",Money.FromYuan(VenueMaintenanceYuan(shop.Place)),ShopTitle(shop)+" 日常维护",
                        LedgerAttribution.ForShop(shop.Place.Id,"venue-maintenance:"+shop.Place.Id+":"+state.MaintenanceDay,"场馆维护"));
                }
                var remaining=new List<VenueCustomer>();
                foreach(var customer in state.CustomersHere)
                {
                    var person=CityPersonState(customer.PersonId);
                    if(person==null||person.ActivityId!=customer.ReservationId||customer.Started>=0&&now>=customer.Until)
                    {ReleaseVenueCustomer(customer);continue;}
                    if(customer.Started<0&&person.Route==null&&person.LocationPlaceId==shop.Place.Id)
                    {
                        if(!VenueOpen(shop.Place)){ReleaseVenueCustomer(customer);continue;}
                        customer.Started=now;customer.Until=now+VenueDuration(shop.Place);
                        Ledger.Credit(Clock.Today,"场馆服务",Money.FromCents(customer.PriceCents),CityPerson(customer.PersonId).Name+" 使用"+VenueBusinessName(shop.Place),
                            LedgerAttribution.ForShop(shop.Place.Id,customer.ReservationId,"场馆服务"));
                        state.Customers++;state.RevenueCents+=customer.PriceCents;state.TotalRevenueCents+=customer.PriceCents;
                        var at=CityActivityAnchor(shop.Place,remaining.Count+1);person.X=at.X;person.Y=at.Y;
                        person.Task=CityPersonTask.Participating;person.TaskLabel="付费使用"+VenueBusinessName(shop.Place);
                    }
                    remaining.Add(customer);
                }
                state.CustomersHere=remaining.ToArray();
                if(!VenueOpen(shop.Place)||state.LastPulse==now/10)continue;
                state.LastPulse=now/10;
                int ownerSeats=_cityActivities.Where(a=>a.Active&&a.VenueId==shop.Place.Id&&a.RendezvousReady).Sum(a=>1+a.Participants.Length);
                if(state.CustomersHere.Length+ownerSeats>=VenueCapacity(shop.Place))continue;
                double basePrice=(CityLifeCatalog.Activities.FirstOrDefault(a=>a.VenueId==shop.Place.Id)?.PriceYuan??30)*100;
                if(!Random.Chance(Math.Min(.8,.45*basePrice/state.PriceCents)))continue;
                var people=_cityPeople.Values.Where(p=>p.Route==null&&p.ActivityId==null&&p.VisitId==null&&p.Task!=CityPersonTask.Working
                    &&!Employees.Any(e=>PersonIdOf(e.Person)==p.PersonId)
                    &&!_cityActivities.Any(a=>a.Active&&a.Participants.Any(c=>c.PersonId==p.PersonId))).ToArray();
                if(people.Length==0)continue;
                var visitor=people[Random.Next(0,people.Length)];
                var route=PlanCityRoute(visitor,shop.Place.Id,now);
                if(route==null||route.ArriveMinute-now>120||!CityWithinHours((int)((route.ArriveMinute+VenueDuration(shop.Place)-1)%1440),state.Open,state.Close))continue;
                var definition=CityPerson(visitor.PersonId);
                if(visitor.WorkPlaceId!=null&&CityShiftOverlaps(now,route.ArriveMinute+VenueDuration(shop.Place),definition.WorkStart,definition.WorkEnd))continue;
                string reservation="venue-customer:"+shop.Place.Id+":"+now+":"+visitor.PersonId;
                visitor.Route=route;visitor.LocationPlaceId=null;visitor.ActivityId=reservation;visitor.Task=CityPersonTask.Travelling;visitor.TaskLabel="前往"+VenueBusinessName(shop.Place);
                remaining.Add(new VenueCustomer{PersonId=visitor.PersonId,ReservationId=reservation,PriceCents=state.PriceCents});state.CustomersHere=remaining.ToArray();
            }
        }
        private int VenueDuration(Place p)=>CityLifeCatalog.Activities.FirstOrDefault(a=>a.VenueId==p.Id)?.Minutes??60;
        public bool RecoverVenueFurniture(string id,int index)
        {
            var shop=ShopAt(id);
            if(!IsOwnedVenue(shop?.Place)||CurrentPlace!=shop.Place||index<0||index>=shop.Layout.Count)return false;
            if(!shop.Locker.IsEmpty||!shop.Fridge.IsEmpty||shop.Sim.DisplayPcs.Count>0)return Fail("请先转出纸箱、冰箱内物品并拆回展示整机，再收回旧家具。");
            AddFurniture(shop.Layout[index].Id,1);shop.Layout.RemoveAt(index);return true;
        }
        private void ShowVenueHandover(Place place)
        {
            VenueOperation(place.Id);ShopAt(place).Sim.TradingEnabled=false;
            Show("场馆接手完成","已接手"+VenueBusinessName(place)+"，保留原用途与场地设施，不能改为零售店。\n租约、押金照常记录，每月交租，每日维护费 ¥"+VenueMaintenanceYuan(place)+"。\n原教练不自动受雇。可自己值守，或另行招聘值守员，按实际员工排班发工资。进入场地设置营业时间和价格，再开启接客；老板自用不收费。");
        }
        private void LoadVenueBusinesses(SaveData data)
        {
            _venueBusinesses.Clear();
            foreach(var state in data.venueBusinesses??Array.Empty<VenueBusiness>())
                if(state!=null&&ShopAt(state.PlaceId)!=null&&IsDedicatedVenue(ShopAt(state.PlaceId).Place))
                {_venueBusinesses[state.PlaceId]=state.Copy();}
            foreach(var shop in MyShops.Where(s=>IsDedicatedVenue(s.Place)))
            {
                VenueOperation(shop.Place.Id);shop.Sim.TradingEnabled=false;shop.Sim.ClearCustomers();
            }
        }
    }
}
