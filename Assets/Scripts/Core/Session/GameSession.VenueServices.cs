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
            : CityLifeCatalog.Activity(string.IsNullOrEmpty(activity.ServiceDefinitionId) ? activity.DefinitionId : activity.ServiceDefinitionId)?.AtVenue(activity.VenueId);
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
        public Money CityActivityEntryPrice(CityActivityDefinition def, int choice, CityActivity booking = null, bool? playerTreat = null)
        {
            if (booking?.HostPays == true) return Money.Zero;
            Money price = VenuePlayerPrice(def, choice);
            int people = (booking?.Participants.Length ?? 0) + 1;
            bool treat = playerTreat ?? (booking?.PlayerTreat == true);
            if (def.FixedGroupPrice) return treat ? price : Money.FromCents((price.Cents + people - 1) / people);
            return treat ? price * people : price;
        }
        public string CityActivityMidText(CityActivity activity)
        {
            var def = CityActivityService(activity);
            return def?.Service(activity.ServiceChoice).Description ?? def?.MidText;
        }
        public const int CityArrivalWindowMinutes = 60;
        public CityActivity PendingVenueActivity(Place place, string activityId = null) => FindPendingVenueActivity(place, activityId);

        private CityActivity FindPendingVenueActivity(Place place, string activityId = null, bool onsiteOnly = false)
        {
            if (place == null) return null;
            long now = Clock.Now.TotalMinutes;
            return _cityActivities.Where(a => a.Active && a.State != CityActivityPhase.Running && a.VenueId == place.Id
                    && (activityId == null || a.Id == activityId) && (!onsiteOnly || IsOnsiteActivity(a))
                    && (Math.Abs(now - a.ScheduledStart) <= CityArrivalWindowMinutes
                        || !IsOnsiteActivity(a) && a.RendezvousReady && CurrentPlace?.Id == place.Id))
                .OrderByDescending(a => onsiteOnly && a.RendezvousReady)
                .ThenBy(a => Math.Abs(a.ScheduledStart - now)).FirstOrDefault();
        }

        private bool VenueRendezvousReady(CityActivity activity) => activity.RendezvousReady
            && activity.PlayerVenueRoute?.ArriveMinute <= Clock.Now.TotalMinutes
            && activity.Participants.All(participant => participant.Phase == CityParticipantPhase.Waiting
                && CityPersonState(participant.PersonId) is { Route: null } person && person.LocationPlaceId == activity.VenueId);

        // Invitations gather at arrival, before a menu hold can stop the world clock.
        public bool PrepareVenueCompanions(Place place, string activityId = null)
        {
            if (place==null || CurrentPlace!=place || IsWorking || _sleeping || CityActivityExclusive) return false;
            var activity = activityId == null ? OnsiteActivityAt(place) ?? PendingVenueActivity(place) : FindPendingVenueActivity(place, activityId);
            if (activity == null) return false;
            // For race aid, each participant keeps their hospital route even if the player arrives first.
            if (IsRaceAidActivity(activity.DefinitionId))
                return activity.Participants.All(p => p.Phase == CityParticipantPhase.Waiting || p.Phase == CityParticipantPhase.Participating);
            if (VenueRendezvousReady(activity)) return true;
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
                person.ActivityId=activity.Id; person.Task=CityPersonTask.Waiting;
                person.TaskLabel=activity.DefinitionId == SignHelpActivityId ? "到场等你开始帮忙"
                    : IsOnsiteActivity(activity) || activity.ServiceConfirmed ? "到场等你开始活动" : "赴约到场，等你选择服务";
                participant.Phase=CityParticipantPhase.Waiting; participant.DepartMinute=now; participant.IntentId=null;
            }
            activity.RendezvousReady=true; activity.State=CityActivityPhase.Gathering;
            return true;
        }
        public string CityActivityEarlyDepartureHint(CityActivity activity)
        {
            if (IsRaceAidActivity(activity.DefinitionId)) return null;
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
            if (!City.HasPlace(a.VenueId)) return "找不到活动地点";
            if (CityActivityVenueAccessBlocker(City.GetPlace(a.VenueId)) is string access) return access;
            if (IsGameOver || _sleeping || IsWorking) return "正在休息或工作，当前不能出发";
            if (CityActivityExclusive) return "正在参加活动，请先结束当前活动";
            if (Trip != null || CityIntent != null) return "正在前往目的地，请先完成或取消当前行程";
            return LeaveBlocker ?? (checkDepartureTime ? CityActivityEarlyDepartureHint(a) : null);
        }
        public string VenueServiceBlocker(string definitionId, int choice, string activityId = null)
        {
            var def = CityLifeCatalog.Activity(definitionId);
            var booking = PendingVenueActivity(CurrentPlace, activityId);
            bool reservedPass = UsesManualVenuePass(booking);
            bool matchingPass = reservedPass && CityActivityService(booking)?.Id == definitionId;
            if (matchingPass || def?.EventOnly == true && booking?.DefinitionId == def.Id) def = def.AtVenue(booking.VenueId);
            if (def == null || CurrentPlace?.Id != def.VenueId) return "到场后才能选择服务";
            if (choice < 0 || choice >= def.ServiceCount) return "请选择菜单或服务项目";
            if (IsGameOver || _sleeping || IsWorking || CityActivityExclusive) return "请先结束正在进行的事情";
            int duration = matchingPass ? booking.Duration : def.Minutes;
            if (!CityVenueFits(CurrentPlace, Clock.Now.TotalMinutes, duration)) return "该服务无法在闭店前完成";
            if (def.Id.StartsWith("expo_") && !CityExpoDay(Clock.Now.TotalMinutes, (def.LogicalId ?? def.Id) == "expo_comic")) return "本场展会尚未开放";
            if (OnsiteActivityAt(CurrentPlace, activityId) != null) return "请从预约卡片进入现场活动";
            if (definitionId.StartsWith("fishing_premium", StringComparison.Ordinal) && !matchingPass)
                return "请选择钓场的抛竿次数";
            if (definitionId == "fishing_pass_16h" && !matchingPass) return "请选择钓场在售的抛竿次数";
            if (CityLifeCatalog.Activity(booking?.DefinitionId)?.EventOnly == true && definitionId != booking.DefinitionId)
                return "本次为已约定的专属活动，请按原日程参加";
            Money cost = CityActivityEntryPrice(def, choice, booking);
            if (cost.IsPositive && !Ledger.CanAfford(cost)) return $"现金不足，需要 {cost}";
            if (booking?.ServiceConfirmed == true) return "本次已选好服务";
            if(IsOwnedVenue(CurrentPlace))
            {
                if(VenueOperation(CurrentPlace.Id).CustomersHere.Length+1+(booking?.Participants.Length??0)>VenueCapacity(CurrentPlace))return "场地容量已满，请等待顾客结束后再使用";
            }
            if (booking == null) return CityActivityBlocker(definitionId, Array.Empty<string>(), Clock.Now.TotalMinutes);
            if (reservedPass && !matchingPass) return "请按已预约的项目开始";
            if (booking.Duration != duration) return "请选择与本次日程时长一致的服务";
            if (booking.Participants.Any(p => p.Phase != CityParticipantPhase.Waiting && p.Phase != CityParticipantPhase.Participating)) return "同行者还未到齐，稍等一下";
            return null;
        }

        public string VenueAppointmentStartBlocker(CityActivity booking)
        {
            if (booking == null || !booking.ServiceConfirmed || !booking.Active || booking.State == CityActivityPhase.Running)
                return "预约当前无法开始";
            if (CurrentPlace?.Id != booking.VenueId || FindPendingVenueActivity(CurrentPlace, booking.Id) != booking)
                return "请在约定时间到场";
            if (IsOnsiteActivity(booking) || IsRaceAidActivity(booking) || booking.DefinitionId == SignHelpActivityId)
                return "请从该活动的现场入口开始";
            if (IsGameOver || _sleeping || IsWorking || Racing || CityActivityExclusive) return "请先结束当前活动";
            if (DistrictAccessBlocker(CurrentPlace) is string access) return access;
            if (!VenueRendezvousReady(booking)) return "等同行者到齐后开始";
            if (!CityVenueFits(CurrentPlace, Clock.Now.TotalMinutes, booking.Duration)) return "距离场所打烊太近，无法开始";
            if (!string.IsNullOrEmpty(booking.EventGiftId) && !booking.EventGiftDelivered && Backpack.GiftCount(booking.EventGiftId) == 0)
                return "请带上约定的伴手礼";
            if (booking.DefinitionId == "social_70" && !HasOpenedComputerShop) return "请先开办可交付的电脑店";
            var definition = CityActivityService(booking);
            if (definition == null) return "找不到预约的活动";
            var price = CityActivityEntryPrice(definition, booking.ServiceChoice, booking);
            return !booking.EntryPaid && price.IsPositive && !Ledger.CanAfford(price) ? $"现金不足，需要 {price}" : null;
        }

        public bool StartVenueAppointment(string activityId)
        {
            if (string.IsNullOrEmpty(activityId)) return Fail("预约当前无法开始");
            var booking = FindPendingVenueActivity(CurrentPlace, activityId);
            if (booking == null || !booking.ServiceConfirmed) return Fail("预约当前无法开始");
            PrepareVenueCompanions(CurrentPlace, activityId);
            if (VenueAppointmentStartBlocker(booking) is string why) return Fail(why);
            TickCityActivities(Clock.Now.TotalMinutes, booking.Id);
            return booking.State == CityActivityPhase.Running;
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
            TickCityActivities(Clock.Now.TotalMinutes, IsInteractiveFishingActivity(booking) ? booking.Id : null);
            if (def.Id == "hike")
                Say("已选择" + VenueServiceName(def, choice) + "，免费散步。");
            else if (!UsesManualVenuePass(booking))
                Say("已选择" + VenueServiceName(def, choice) + (booking.HostPays ? "，由邀请人请客。"
                    : "，费用 " + VenuePlayerPrice(def, choice) + (IsOwnedVenue(CurrentPlace)?"（老板自用，不计营业收入）":"") + "。"));
            return true;
        }

        public static bool UsesManualVenuePass(CityActivity activity) => IsInteractiveFishingActivity(activity) || IsVenueSport(activity);
    }
}
