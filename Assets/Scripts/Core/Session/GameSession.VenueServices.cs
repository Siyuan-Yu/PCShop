using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        public CityActivityDefinition CityActivityService(CityActivity activity) => activity == null ? null
            : CityLifeCatalog.Activity(string.IsNullOrEmpty(activity.ServiceDefinitionId) ? activity.DefinitionId : activity.ServiceDefinitionId);
        public static string VenueServiceName(CityActivityDefinition def, int choice = 0)
        {
            if (def == null) return "服务";
            if (def.Id == "dinner") return choice == 0 ? "清淡家常菜套餐" : "招牌炖菜套餐";
            if (def.Id == "snack") return choice == 0 ? "汤面套餐" : "家常小菜套餐";
            string item = def.Choices != null && choice < def.Choices.Length ? def.Choices[choice] : def.Name;
            return def.Id == "ktv" ? "练歌包间 · " + item : item;
        }
        public string VenuePriceRange(CityActivityDefinition def)
        {
            if (def.ServiceCount < 2) return VenuePlayerPrice(def, 0).ToString();
            Money first = VenuePlayerPrice(def, 0);
            Money second = VenuePlayerPrice(def, 1);
            if (first == second) return first.ToString();
            return first < second ? $"{first}–{second}" : $"{second}–{first}";
        }
        public string CityActivityMidText(CityActivity activity)
        {
            var def = CityActivityService(activity);
            return def?.Service(activity.ServiceChoice).Description ?? def?.MidText;
        }
        public const int CityArrivalWindowMinutes = 60;
        public CityActivity PendingVenueActivity(Place place) => place == null ? null : _cityActivities
            .Where(a => a.Active && a.State != CityActivityPhase.Running && a.VenueId == place.Id
                && (Math.Abs(Clock.Now.TotalMinutes-a.ScheduledStart)<=CityArrivalWindowMinutes
                    || a.RendezvousReady && CurrentPlace==place))
            .OrderBy(a => Math.Abs(a.ScheduledStart-Clock.Now.TotalMinutes)).FirstOrDefault();

        // Invitations gather at arrival, before a menu hold can stop the world clock.
        public bool PrepareVenueCompanions(Place place)
        {
            if (place==null || CurrentPlace!=place || IsWorking || _sleeping || CityActivityExclusive) return false;
            var activity=PendingVenueActivity(place);
            if (activity==null || activity.RendezvousReady) return activity?.RendezvousReady ?? false;
            foreach (var participant in activity.Participants)
            {
                var person=CityPersonState(participant.PersonId);
                var worker=Employees.FirstOrDefault(e=>PersonIdOf(e.Person)==participant.PersonId);
                if (person==null || worker!=null && EmployeeTrip(worker.Person.Id)!=null
                    || person.ActivityId!=null && person.ActivityId!=activity.Id) return false;
                if (worker!=null && worker.Person.OnDuty(Clock.Now) && !activity.EmployeeLeaveApproved) return false;
            }
            long now=Clock.Now.TotalMinutes;
            var player=CityActivityAnchor(place,0);
            activity.PlayerVenueRoute=new CityRoute {IntentId=activity.Id+":seat:0",TargetPlaceId=place.Id,
                Transport=VehicleKind.Walk,Nodes=Array.Empty<string>(),X=new[]{player.X,player.X},Y=new[]{player.Y,player.Y},
                Meters=new[]{0.0},DepartMinute=now,ArriveMinute=now};
            for (int i=0;i<activity.Participants.Length;i++)
            {
                var participant=activity.Participants[i]; var person=CityPersonState(participant.PersonId);
                var anchor=CityActivityAnchor(place,i+1);
                if (person.VisitId!=null)
                {
                    // Keep the shop transaction as an ordinary shopper; the named companion
                    // is reserved for this gathering and cannot be rendered in both places.
                    foreach(var shop in _shops)
                        foreach(var customer in shop.Sim.ActiveCustomers.Where(c=>c.PersonId==person.PersonId && c.VisitId==person.VisitId))
                            customer.PersonId=null;
                    person.VisitId=null;
                }
                person.Route=null; person.X=anchor.X; person.Y=anchor.Y; person.LocationPlaceId=place.Id;
                person.ActivityId=activity.Id; person.Task=CityPersonTask.Waiting; person.TaskLabel="赴约到场，等你选择服务";
                participant.Phase=CityParticipantPhase.Waiting; participant.DepartMinute=now; participant.IntentId=null;
            }
            activity.RendezvousReady=true; activity.State=CityActivityPhase.Gathering;
            return true;
        }
        public string CityActivityEarlyDepartureHint(CityActivity activity)
        {
            long remaining = activity.ScheduledStart - Clock.Now.TotalMinutes;
            if (remaining <= CityArrivalWindowMinutes || !City.HasPlace(activity.VenueId)) return null;
            var venue = City.GetPlace(activity.VenueId);
            int travel = CurrentPlace == venue ? 0 : CityTravelMinutes(venue);
            if (travel >= int.MaxValue / 4) travel = 0;
            long lead = (long)travel + CityArrivalWindowMinutes;
            if (remaining <= lead) return null;
            var appointment = new GameTimestamp(activity.ScheduledStart);
            var departure = appointment.AddMinutes(-lead);
            return $"还早呢！预约时间是 {appointment}。\n按当前路程，最早可在 {departure} 出发。";
        }

        public string CityActivityTravelBlocker(string id, bool checkDepartureTime = true)
        {
            var a = _cityActivities.FirstOrDefault(x => x.Id == id && x.Active);
            if (a == null) return "日程已经结束";
            if (IsGameOver || _sleeping || IsWorking) return "正在休息或工作，当前不能出发";
            if (CityActivityExclusive) return "正在参加活动，请先结束当前活动";
            if (Trip != null || CityIntent != null) return "正在前往目的地，请先完成或取消当前行程";
            return LeaveBlocker ?? (checkDepartureTime ? CityActivityEarlyDepartureHint(a) : null);
        }
        public string VenueServiceBlocker(string definitionId, int choice)
        {
            var def = CityLifeCatalog.Activity(definitionId);
            if (def == null || CurrentPlace?.Id != def.VenueId) return "到场后才能选择服务";
            if (choice < 0 || choice >= def.ServiceCount) return "请选择菜单或服务项目";
            if (IsGameOver || _sleeping || IsWorking || CityActivityExclusive) return "请先结束正在进行的事情";
            if (!CityVenueFits(CurrentPlace, Clock.Now.TotalMinutes, def.Minutes)) return "该服务无法在闭店前完成";
            if (def.Id.StartsWith("expo_") && !CityExpoDay(Clock.Now.TotalMinutes, def.Id == "expo_comic")) return "本场展会尚未开放";
            var booking = PendingVenueActivity(CurrentPlace);
            Money cost = booking?.HostPays == true ? Money.Zero : VenuePlayerPrice(def, choice);
            if (cost.IsPositive && !Ledger.CanAfford(cost)) return $"现金不足，需要 {cost}";
            if (booking?.ServiceConfirmed == true) return "本次已选好服务";
            if(IsOwnedVenue(CurrentPlace))
            {
                if(VenueOperation(CurrentPlace.Id).CustomersHere.Length+1+(booking?.Participants.Length??0)>VenueCapacity(CurrentPlace))return "场地容量已满，请等待顾客结束后再使用";
            }
            if (booking == null) return CityActivityBlocker(definitionId, Array.Empty<string>(), Clock.Now.TotalMinutes);
            if (booking.Duration != def.Minutes) return "请选择与本次日程时长一致的服务";
            if (booking.Participants.Any(p => p.Phase != CityParticipantPhase.Waiting && p.Phase != CityParticipantPhase.Participating)) return "同行者还未到齐，稍等一下";
            return null;
        }
        public bool SelectVenueService(string definitionId, int choice)
        {
            PrepareVenueCompanions(CurrentPlace);
            string why = VenueServiceBlocker(definitionId, choice);
            if (why != null) return Fail(why);
            var def = CityLifeCatalog.Activity(definitionId);
            var booking = PendingVenueActivity(CurrentPlace)
                ?? ScheduleCityActivity(definitionId, Array.Empty<string>(), Clock.Now.TotalMinutes, sourceId: "walk-in:" + _nextCityActivity);
            if (booking == null) return false;
            booking.ServiceDefinitionId = definitionId;
            booking.ServiceChoice = choice;
            booking.ServiceConfirmed = true;
            PrepareVenueCompanions(CurrentPlace);
            TickCityActivities(Clock.Now.TotalMinutes);
            Say("已选择" + VenueServiceName(def, choice) + (booking.HostPays ? "，由邀请人请客。"
                : "，费用 " + VenuePlayerPrice(def, choice) + (IsOwnedVenue(CurrentPlace)?"（老板自用，不计营业收入）":"") + "。"));
            return true;
        }
    }
}
