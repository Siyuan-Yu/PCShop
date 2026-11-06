using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly List<CityActivity> _cityActivities = new List<CityActivity>();
        public event Action<string> CityActivitySettled;
        private long _nextCityActivity=1, _nextCityIntent=1;
        private readonly List<string> _cityFavorites = new List<string>(), _cityRecent = new List<string>();
        public string CityDefaultDestination { get; private set; }
        public string CityAgencyShortcut { get; private set; } = "agency";
        public string CityMarketShortcut { get; private set; } = "stall_cpu";
        public string CityShopShortcut { get; private set; }
        public void SetCityShopShortcut(string placeId) {if(MyShops.Any(s=>s.Place.Id==placeId))CityShopShortcut=placeId;}
        public IReadOnlyList<string> CityFavorites => _cityFavorites;
        public IReadOnlyList<string> CityRecent => _cityRecent;
        public IReadOnlyList<CityActivity> CityActivities => _cityActivities;
        public CityTravelIntent CityIntent { get; private set; }
        public event Action<CityTravelIntent> CityTravelRequested;
        public event Action<Place> CityPlaceEntered;
        public CityActivity RunningCityActivity => _cityActivities.FirstOrDefault(a=>a.State==CityActivityPhase.Running);
        public bool CityActivityExclusive => RunningCityActivity!=null||BrandExpoExclusive;
        public bool IsCityActivityVenue(Place place)=>place!=null&&CityLifeCatalog.Activities.Any(a=>a.VenueId==place.Id);
        public static string CityPhaseText(CityActivityPhase phase) => phase==CityActivityPhase.Scheduled?"已预约":phase==CityActivityPhase.Gathering?"赴约集合":phase==CityActivityPhase.Running?"活动中":phase==CityActivityPhase.Completed?"已完成":phase==CityActivityPhase.NoShow?"未赴约":phase==CityActivityPhase.Blocked?"路线阻塞":phase==CityActivityPhase.Interrupted?"已中断":phase==CityActivityPhase.Cancelled?"已取消":"待接受";

        public bool CityExpoOpen(bool comic)
        {
            int day=Clock.Today.Day;
            return comic?day>=22&&day<=24:Clock.Today.TotalDays%90>=75&&Clock.Today.TotalDays%90<=78;
        }
        public string CityExpoCalendar => "品牌展会：每季度一次，每届4天09:00—17:00；原创漫展：每月22—24日。";
        public long SuggestCityActivityStart(string definitionId,string personId=null)
        {
            var def=CityLifeCatalog.Activity(definitionId);if(def==null||!City.HasPlace(def.VenueId))return Clock.Now.TotalMinutes;
            if(IsWorking||_sleeping||IsGameOver)return Clock.Now.TotalMinutes;
            var venue=City.GetPlace(def.VenueId);long now=Clock.Now.TotalMinutes;
            int trip=CityTravelMinutes(venue);
            var person=CityPersonState(personId);var npcRoute=person!=null?PlanCityRoute(person,venue.Id,now):null;
            if(npcRoute!=null)trip=Math.Max(trip,(int)(npcRoute.ArriveMinute-now));
            long start=now+trip+8;
            var employee=Employees.FirstOrDefault(e=>PersonIdOf(e.Person)==personId)?.Person;
            if(employee!=null&&employee.TryShift(now,out _,out long shiftEnd))start=Math.Max(start,shiftEnd+trip+5);
            // Offer only quarter-hour appointments, rounding later so the journey still fits.
            start=(start+14)/15*15;
            for(int i=0;i<60*24*35;i+=15)
            {
                long candidate=start+i;
                var ids=string.IsNullOrEmpty(personId)?Array.Empty<string>():new[]{personId};
                if(CityActivityBlocker(definitionId,ids,candidate)==null)return candidate;
            }
            return start;
        }
        // Reserve the route from the expected departure place. Each person's work check uses
        // their own journey, rather than another participant's much longer trip across town.
        private int CityActivityTravelReserve(Place venue,IEnumerable<string> ids,long start=0)
        {
            int reserve=CityTravelMinutes(venue);
            foreach(string id in ids)
            {
                var person=CityPersonState(id);if(person==null)continue;
                reserve=Math.Max(reserve,CityPersonActivityTravel(person,venue,start));
            }
            return Math.Max(5,reserve);
        }
        private int CityPersonActivityTravel(CityPersonState person,Place venue,long start)
        {
            long now=Clock.Now.TotalMinutes;var probe=person.Copy();
            if(start>now+120)
            {
                var employee=Employees.FirstOrDefault(e=>PersonIdOf(e.Person)==person.PersonId)?.Person;
                string origin=person.WorkPlaceId!=null && (employee!=null?employee.TryShift(start,out _,out _):AuthoredCityWorkAt(person,start))?person.WorkPlaceId:person.HomePlaceId;
                if(origin!=null&&City.HasPlace(origin)){var at=StandOf(City.GetPlace(origin));probe.X=at.X;probe.Y=at.Y;probe.LocationPlaceId=origin;}
            }
            var route=PlanCityRoute(probe,venue.Id,now,start>0?start-3:long.MaxValue);
            return route==null?int.MaxValue/4:Math.Max(1,(int)(route.ArriveMinute-now));
        }
        private static bool CityShiftOverlaps(long from,long until,int open,int close)
        {
            if(open==close)return false;
            for(long day=from/1440-1;day<=until/1440;day++)
            {
                long begin=day*1440+open,end=day*1440+close+(close<open?1440:0);
                if(from<end&&until>begin)return true;
            }
            return false;
        }
        private static bool CityExpoDay(long minute,bool comic)
        {
            int d=(int)(minute/1440)%30+1;return comic?d>=22&&d<=24:d>=10&&d<=12;
        }
        private bool CityVenueFits(Place venue,long start,int minutes)
        {
            if(IsOwnedVenue(venue))return true;
            if(SupplierClosesOnWeekend(venue))
                for(long day=start/1440;day<=(start+Math.Max(1,minutes)-1)/1440;day++)
                    if(new GameDate((int)day).IsWeekend)return false;
            CityVenueHours(venue,out int open,out int close);
            return CityWithinHours((int)(start%1440),open,close)&&CityWithinHours((int)((start+Math.Max(1,minutes)-1)%1440),open,close);
        }
        private void CityVenueHours(Place venue,out int open,out int close)
        {
            // An authored entrance with 00:00–00:00 inherits the building's normal
            // business hours, just as IsPlaceOpen does; it is not a 24-hour venue.
            bool customHours=venue.ExplicitEntrance&&venue.OpenMinute!=venue.CloseMinute;
            open=customHours?venue.OpenMinute:venue.Kind==PlaceKind.Wholesaler?WholesalerOpenMinute
                :venue.Kind==PlaceKind.Dealer?DealerOpenMinute:venue.Business==FarmBusiness?FarmOpenMinute
                :venue.Kind==PlaceKind.Shop||venue.Kind==PlaceKind.Agency?StreetShopOpenMinute:0;
            close=customHours?venue.CloseMinute:venue.Kind==PlaceKind.Wholesaler?WholesalerCloseMinute
                :venue.Kind==PlaceKind.Dealer?DealerCloseMinute:venue.Business==FarmBusiness?FarmCloseMinute
                :venue.Kind==PlaceKind.Shop||venue.Kind==PlaceKind.Agency?StreetShopCloseMinute:0;
        }
        private bool AuthoredCityWorkAt(CityPersonState person,long minute)
        {
            var definition=CityPerson(person.PersonId);
            if(definition==null||!City.HasPlace(person.WorkPlaceId))return false;
            var workplace=City.GetPlace(person.WorkPlaceId);
            if(workplace.Kind==PlaceKind.Shop&&IsVacant(workplace))return false;
            return CityWithinHours((int)(minute%1440),definition.WorkStart,definition.WorkEnd)
                &&(person.WorkPlaceId==person.HomePlaceId||CityVenueFits(workplace,minute,1));
        }
        private bool AuthoredCityWorkOverlaps(CityPersonState person,long from,long until)
        {
            var definition=CityPerson(person.PersonId);
            if(definition==null||!City.HasPlace(person.WorkPlaceId)||until<=from)return false;
            CityVenueHours(City.GetPlace(person.WorkPlaceId),out int open,out int close);
            // Work and shop status can only change at these boundaries or at midnight.
            // Check intervals, rather than iterating every simulated minute per suggestion.
            var boundaries=new[]{0,definition.WorkStart,definition.WorkEnd,open,close,1440}.Distinct().OrderBy(x=>x).ToArray();
            for(long day=Math.Max(0,from/1440);day<=until/1440;day++)
                for(int i=0;i<boundaries.Length-1;i++)
                {
                    long begin=Math.Max(from,day*1440+boundaries[i]);
                    long end=Math.Min(until,day*1440+boundaries[i+1]);
                    if(begin<end&&AuthoredCityWorkAt(person,begin))return true;
                }
            return false;
        }
        public int CityTravelMinutes(Place target)
        {
            var route=PlanTrip(target.NodeId,StandOf(target),out double meters);
            return route==null?int.MaxValue/4:Math.Max(1,route.EstimateRemainingMinutes(Time.SecondsPerMinuteAt1x));
        }
        public string CityActivityBlocker(string definitionId,IEnumerable<string> persons,long start,bool approveEmployee=false)
        {
            var def=CityLifeCatalog.Activity(definitionId);
            if(def==null||!City.HasPlace(def.VenueId))return "找不到活动或实际场所";
            if(IsGameOver||IsWorking||_sleeping)return "当前状态不能参加活动";
            if(start<Clock.Now.TotalMinutes)return "预约时间已经过去";
            var venue=City.GetPlace(def.VenueId);
            if(SupplierClosesOnWeekend(venue)&&new GameDate((int)(start/1440)).IsWeekend)return "该场所周末休息，请选择工作日赴约";
            if(!CityVenueFits(venue,start,def.Minutes))return "该时段不能在闭店前完成，请改约其他时间";
            if(definitionId.StartsWith("expo_")&&!CityExpoDay(start,definitionId=="expo_comic"))return "不在本届展期，下一届按固定月历开放";
            var ids=(persons??Array.Empty<string>()).Where(id=>!string.IsNullOrEmpty(id)).Distinct().ToArray();
            if(CommunitySearchActive)
                for(int i=0;i<3;i++)
                    if(!CommunityHasClue(i)&&def.VenueId==CommunityLeadVenue(i)
                        &&ids.Contains(CommunityLeadPerson(i))&&!CitySamePlace(CommunityLeadPerson(i)))
                        return "这次寻物交往需要当面邀约，请先走到"+(CityPerson(CommunityLeadPerson(i))?.Name??CommunityLeadPerson(i))+"身边，通过人物对话邀请活动。";
            if(ids.Length>5)return "一个活动最多六人（含玩家）";
            if(IsOwnedVenue(venue)&&ids.Length+1>VenueCapacity(venue))return "该场地最多容纳 "+VenueCapacity(venue)+" 人（含玩家），请减少同行人数";
            long end=start+def.Minutes;
            int travel=CityActivityTravelReserve(venue,ids,start);
            if(travel>=int.MaxValue/4)return "目的地路线不可达";
            if(Clock.Now.TotalMinutes+CityTravelMinutes(venue)>start+15)return "按真实路线来不及到达，请选择稍晚时段";
            long occupiedFrom=start-travel,occupiedUntil=end+travel;
            foreach(var existing in _cityActivities.Where(a=>a.Active))
            {
                if(!City.HasPlace(existing.VenueId))continue;
                int oldTravel=existing.OccupiedUntil>existing.ScheduledStart?0:CityActivityTravelReserve(City.GetPlace(existing.VenueId),existing.Participants.Select(p=>p.PersonId),existing.ScheduledStart);
                long from=existing.OccupiedUntil>existing.ScheduledStart?existing.OccupiedFrom:existing.ScheduledStart-oldTravel;
                long until=existing.OccupiedUntil>existing.ScheduledStart?existing.OccupiedUntil:existing.ScheduledStart+existing.Duration+oldTravel;
                if(occupiedFrom<until && occupiedUntil>from)return "玩家已有重叠日程（含真实往返路程），请选择其他时段";
            }
            foreach(string id in ids)
            {
                var p=CityPersonState(id);if(p==null)return "尚未认识或无法联系该人物";
                var route=PlanCityRoute(p,venue.Id,Clock.Now.TotalMinutes,start-3);if(route==null)return "人物到场路线不可达";
                var row=Employees.FirstOrDefault(e=>PersonIdOf(e.Person)==id);
                int personTravel=CityPersonActivityTravel(p,venue,start);
                bool working=row!=null&&EmployeeShiftOverlaps(row.Person,start-personTravel,end+personTravel);
                if(working&&!approveEmployee)return "该员工届时当班；需要明确批准离岗，页面会说明无人值守影响";
                var d=CityPerson(id);
                if(row==null&&!definitionId.StartsWith("expo_")&&p.WorkPlaceId!=null&&d!=null&&AuthoredCityWorkOverlaps(p,start-personTravel,end+personTravel))return d.Name+"届时还在工作，建议下班后再约";
                if(route.ArriveMinute>start+15)return "对方按真实路线来不及在宽限内到达，请晚一点预约";
            }
            int seats=_cityActivities.Where(a=>a.Active&&a.VenueId==venue.Id&&start<a.ScheduledStart+a.Duration&&end>a.ScheduledStart).Sum(a=>a.Participants.Length+1);
            return seats+ids.Length+1>18?"这个时段座位不足，请选择下一时段":null;
        }
        public CityActivity ScheduleCityActivity(string definitionId,IEnumerable<string> persons,long start,bool approveEmployee=false,string sourceId=null)
        {
            InitializeCityPeople();var ids=(persons??Array.Empty<string>()).Distinct().ToArray();
            if(!string.IsNullOrEmpty(sourceId)) {var old=_cityActivities.FirstOrDefault(a=>a.SourceId==sourceId);if(old!=null)return old;}
            // Invitations cannot be backdated. Immediate solo venue purchases have their own
            // explicit walk-in source and may start at the current clock minute.
            if ((sourceId==null || !sourceId.StartsWith("walk-in:",StringComparison.Ordinal))
                && start<=Clock.Now.TotalMinutes+Time.FractionalMinute)
            { Fail("请选择尚未到达的邀约时间"); return null; }
            string reason=CityActivityBlocker(definitionId,ids,start,approveEmployee);if(reason!=null){Fail(reason);return null;}
            var def=CityLifeCatalog.Activity(definitionId);
            var activity=new CityActivity {Id="activity_"+_nextCityActivity++,DefinitionId=def.Id,VenueId=def.VenueId,SourceId=sourceId,
                HostId="player",ScheduledStart=start,Duration=def.Minutes,EmployeeLeaveApproved=approveEmployee,
                OccupiedFrom=start-CityActivityTravelReserve(City.GetPlace(def.VenueId),ids,start),OccupiedUntil=start+def.Minutes+CityActivityTravelReserve(City.GetPlace(def.VenueId),ids,start),State=CityActivityPhase.Scheduled,Seat=_cityActivities.Count%3,
                  Participants=ids.Select(id=>
                  {
                      var route=PlanCityRoute(CityPersonState(id),def.VenueId,Clock.Now.TotalMinutes,start-3);
                      return new CityActivityParticipant {PersonId=id,Phase=CityParticipantPhase.NotDeparted,
                          TravelTransportChosen=route!=null,TravelTransport=route?.Transport??VehicleKind.Walk};
                  }).ToArray()};
            _cityActivities.Add(activity);
            foreach(string id in ids)RememberSocial(id,"约好到"+City.GetPlace(def.VenueId).Name+"参加"+def.Name+"；此时仅预约，尚未消费或完成活动。");
            if (sourceId == null || !sourceId.StartsWith("walk-in:", StringComparison.Ordinal)) Say("预约成立："+def.Name+"，请按日程实际前往。预计费用 "+VenuePlayerPrice(def)+(IsOwnedVenue(City.GetPlace(def.VenueId))?"（老板自用免费）。":"，到场开始时支付。"));
            return activity;
        }
        public bool GoToCityActivity(string id)
        {
            var a=_cityActivities.FirstOrDefault(x=>x.Id==id&&x.Active);
            if(a==null)return Fail("活动已结束或无效");
            string why = CityActivityTravelBlocker(id); if (why != null) return Fail(why);
            return BeginCityTravel(a.VenueId,a.Id);
        }
        public bool BeginCityTravel(string placeId,string activityId=null,bool enter=true)
        {
            if(Racing)return Fail("请先完成或退出当前比赛。");
            if(!City.HasPlace(placeId))return Fail("目的地不存在，请重新选择");
            if(CityActivityExclusive)return Fail("正在参加活动，请先明确提前离开");
            if(IsWorking)return Fail("正在打工，收工后才能出发");
            CancelCityTravel();
            CityIntent=new CityTravelIntent {Id="travel_"+_nextCityIntent++,TargetPlaceId=placeId,ActivityId=activityId,EnterOnArrival=enter};
            if(activityId!=null){var a=_cityActivities.FirstOrDefault(x=>x.Id==activityId);if(a!=null)a.PlayerIntentId=CityIntent.Id;}
            _cityRecent.Remove(placeId);_cityRecent.Insert(0,placeId);if(_cityRecent.Count>6)_cityRecent.RemoveAt(6);
            if(CityTravelRequested!=null)CityTravelRequested(CityIntent);else ActivateCityTravel(CityIntent.Id);
            return true;
        }
        public bool ActivateCityTravel(string id)
        {
            if(CityIntent==null||CityIntent.Id!=id||CityIntent.Cancelled||!City.HasPlace(CityIntent.TargetPlaceId))return false;
            var place=City.GetPlace(CityIntent.TargetPlaceId);
            if(CurrentPlace==place){CityIntent=null;return true;}
            bool result=GoTo(place);
            if(!result) {CancelCityTravel();return false;}
            CompleteCityTravel();return true;
        }
        public void CancelCityTravel()
        {
            if(CityIntent!=null)CityIntent.Cancelled=true;
            CityIntent=null;
        }
        private void CompleteCityTravel()
        {
            var intent=CityIntent;if(intent==null||intent.Cancelled||Trip!=null||!City.HasPlace(intent.TargetPlaceId))return;
            var p=City.GetPlace(intent.TargetPlaceId);
            if(CurrentPlace==p){CityIntent=null;return;}
            if(PlayerNodeId!=p.NodeId||DistanceCity(PlayerPosition,StandOf(p))>EnterRadius)return;
            if(!string.IsNullOrEmpty(intent.ActivityId)&&!_cityActivities.Any(a=>a.Id==intent.ActivityId&&a.Active)) {CancelCityTravel();return;}
            string closed=ClosedReason(p);
            if(intent.EnterOnArrival&&closed!=null)
            {
                if(!intent.NotifiedClosed){intent.NotifiedClosed=true;Say(closed+"，已到门口。可在此等待开门，手动接管会取消自动进入。");}
                return;
            }
            CityIntent=null;
            if(intent.EnterOnArrival&&Enter(p))CityPlaceEntered?.Invoke(p);
        }
        public bool SetCityFavorite(string placeId,bool favorite)
        {
            if(!City.HasPlace(placeId))return false;
            if(!favorite){_cityFavorites.Remove(placeId);return true;}
            if(_cityFavorites.Contains(placeId))return true;
            if(_cityFavorites.Count>=5)return Fail("最多收藏五个地点");_cityFavorites.Add(placeId);return true;
        }
        public void SetCityDefault(string placeId) {if(City.HasPlace(placeId))CityDefaultDestination=placeId;}
        public void SetCityShortcut(string placeId,bool agencySlot) {if(City.HasPlace(placeId)){if(agencySlot)CityAgencyShortcut=placeId;else CityMarketShortcut=placeId;}}

        private MapPoint? CityActivityPlayerPosition()
        {
            var a=_cityActivities.LastOrDefault(x=>x.Active&&CurrentPlace?.Id==x.VenueId&&x.PlayerVenueRoute!=null);
            return a?.PlayerVenueRoute.Position(Clock.Now.TotalMinutes);
        }
        private MapPoint CityActivityAnchor(Place venue,int seat)
        {
            double yaw=venue.Heading;
            if(Math.Abs(yaw)<.001)yaw=Math.Atan2(venue.EntranceX,venue.EntranceY)*180/Math.PI;
            double x=seat%2==0?-venue.Width*.27-1.15:-venue.Width*.27+1.15;
            double z=2.3+(seat/2)*1.7;
            if(venue.Business!="venue_restaurant"){x=(seat%3-1)*1.6;z=2.5+(seat/3)*1.8;}
            if(!venue.ExplicitEntrance)z=-1.7-(seat/3)*1.4;
            var door=DoorOf(venue);double r=yaw*Math.PI/180;
            return new MapPoint(door.X+x*Math.Cos(r)+z*Math.Sin(r),door.Y-x*Math.Sin(r)+z*Math.Cos(r));
        }
        private CityRoute CityActivityLocalRoute(CityActivity a,MapPoint start,int seat,long now)
        {
            var venue=City.GetPlace(a.VenueId);var end=CityActivityAnchor(venue,seat);var door=DoorOf(venue);
            double yaw=venue.Heading;if(Math.Abs(yaw)<.001)yaw=Math.Atan2(venue.EntranceX,venue.EntranceY)*180/Math.PI;
            double r=yaw*Math.PI/180,tx=Math.Cos(r),ty=-Math.Sin(r),nx=Math.Sin(r),ny=Math.Cos(r);
            double lateral=(end.X-door.X)*tx+(end.Y-door.Y)*ty;
            // Use the clear entrance aisle, then approach the chair from its side instead of
            // crossing the dining table on a direct diagonal from the door.
            double aisle=venue.ExplicitEntrance?.6:-.6;
            var front=new MapPoint(door.X+nx*aisle,door.Y+ny*aisle);
            var side=new MapPoint(front.X+tx*lateral,front.Y+ty*lateral);
            var points=new[]{start,front,side,end};
            var meters=Enumerable.Range(0,3).Select(i=>DistanceCity(points[i],points[i+1])*City.TravelDistanceScale).ToArray();
            return new CityRoute{IntentId=a.Id+":seat:"+seat,TargetPlaceId=a.VenueId,Nodes=Array.Empty<string>(),
                X=points.Select(p=>p.X).ToArray(),Y=points.Select(p=>p.Y).ToArray(),Meters=meters,DepartMinute=now,
                ArriveMinute=now+Math.Max(1,(seat == 0 ? CityTravelTiming.Minutes(meters.Sum(),Vehicles.Walk) : CityTravelTiming.NpcMinutes(meters.Sum(),Vehicles.Walk)))};
        }
        private void TickCityActivities(long now)
        {
            foreach(var a in _cityActivities.Where(a=>a.Active).ToArray())
            {
                var def=CityActivityService(a);
                if(def==null||!City.HasPlace(a.VenueId)){EndCityActivity(a,CityActivityPhase.Blocked,"场所配置失效；保留记录，不惩罚履约",now);continue;}
                var venue=City.GetPlace(a.VenueId);
                if(!a.Reminder && now>=a.ScheduledStart-Math.Max(30,CityTravelMinutes(venue))) {a.Reminder=true;Say("日程提醒："+def.Name+"，请实际前往"+venue.Name+"。");}
                if (a.State!=CityActivityPhase.Running && CurrentPlace==venue) PrepareVenueCompanions(venue);
                bool participantsReady=true;
                if(CurrentPlace==venue&&a.PlayerVenueRoute==null&&now>=a.ScheduledStart-CityArrivalWindowMinutes)a.PlayerVenueRoute=CityActivityLocalRoute(a,PlayerPosition,0,now);
                bool playerReady=CurrentPlace==venue&&a.PlayerVenueRoute!=null&&now>=a.PlayerVenueRoute.ArriveMinute;
                foreach(var participant in a.Participants)
                {
                    var person=CityPersonState(participant.PersonId);
                    if(person==null) {participantsReady=false;EndCityActivity(a,CityActivityPhase.Blocked,"参与者记录缺失，暂缓活动",now);break;}
                    if(participant.Phase==CityParticipantPhase.NotDeparted)
                    {
                        if(person.VisitId!=null){participantsReady=false;continue;}
                        var route=PlanCityRoute(person,venue.Id,now,a.ScheduledStart-3,
                            participant.TravelTransportChosen?participant.TravelTransport:(VehicleKind?)null);
                        if(route==null){EndCityActivity(a,CityActivityPhase.Blocked,"参与者路线不可达；不按玩家爽约处理",now);break;}
                        participant.TravelTransportChosen=true;participant.TravelTransport=route.Transport;
                        if(now>=a.ScheduledStart-(route.ArriveMinute-now)-3)
                        {
                            var worker=Employees.FirstOrDefault(e=>PersonIdOf(e.Person)==participant.PersonId);
                            if(worker!=null&&EmployeeTrip(worker.Person.Id)!=null)
                            {EndCityActivity(a,CityActivityPhase.Blocked,"员工仍在已确认采购途中，请返程后改约；不扣消费与关系",now);break;}
                            if(worker!=null&&!a.EmployeeLeaveApproved&&EmployeeShiftOverlaps(worker.Person,now,a.ScheduledStart+a.Duration))
                            {EndCityActivity(a,CityActivityPhase.Blocked,"班次发生变化且未批准离岗，请改约下班后；不扣消费与关系",now);break;}
                            if(person.ActivityId!=null&&person.ActivityId!=a.Id)
                            {EndCityActivity(a,CityActivityPhase.Blocked,"参与者正在另一项活动，请重新约定时段",now);break;}
                            person.ActivityId=a.Id;participant.IntentId=route.IntentId;participant.DepartMinute=now;
                            if(person.LocationPlaceId==venue.Id && person.Route==null)participant.Phase=CityParticipantPhase.Waiting;
                            else {person.Route=route;person.LocationPlaceId=null;person.Task=CityPersonTask.Travelling;person.TaskLabel=route.Transport==VehicleKind.Taxi?"搭乘出租车赴约（个人支付）":"步行赴约";participant.Phase=CityParticipantPhase.Travelling;}
                            a.State=CityActivityPhase.Gathering;
                        }
                    }
                    if(participant.Phase==CityParticipantPhase.Travelling && person.Route==null && person.LocationPlaceId==venue.Id)
                    {
                        var anchor=CityActivityAnchor(venue,Array.IndexOf(a.Participants,participant)+1);
                        if(DistanceCity(new MapPoint(person.X,person.Y),anchor)>.1)
                        {person.Route=CityActivityLocalRoute(a,new MapPoint(person.X,person.Y),Array.IndexOf(a.Participants,participant)+1,now);participant.IntentId=person.Route.IntentId;participantsReady=false;}
                        else participant.Phase=CityParticipantPhase.Waiting;
                    }
                    if(participant.Phase!=CityParticipantPhase.Waiting&&participant.Phase!=CityParticipantPhase.Participating)participantsReady=false;
                    else {person.ActivityId=a.Id;person.Task=a.State==CityActivityPhase.Running?CityPersonTask.Participating:CityPersonTask.Waiting;}
                }
                if(!a.Active)continue;
                if(a.State!=CityActivityPhase.Running)
                {
                    if(now>a.ScheduledStart+CityArrivalWindowMinutes && !playerReady)
                    {EndCityActivity(a,participantsReady?CityActivityPhase.NoShow:CityActivityPhase.Blocked,participantsReady?"未在宽限时间内到场":"参与者未能准时到达，请改约；不扣关系",now);continue;}
                    if (now > a.ScheduledStart + CityArrivalWindowMinutes && !participantsReady)
                    { EndCityActivity(a, CityActivityPhase.Blocked, "同行者未能到齐，请改约；不扣消费与关系", now); continue; }
                    if((now<a.ScheduledStart && !a.RendezvousReady)||!playerReady||!participantsReady)continue;
                    if (!IsOwnedVenue(venue)&&!CityWithinHours(Clock.MinuteOfDay, venue.OpenMinute, venue.CloseMinute))
                    { EndCityActivity(a, CityActivityPhase.Cancelled, "场所已经打烊，尚未消费", now); continue; }
                    // Arriving with friends is not a purchase. Choose a service/menu at the venue first.
                    if (!a.ServiceConfirmed && !a.EntryPaid)
                    {
                        if (now > a.ScheduledStart + CityArrivalWindowMinutes && !a.RendezvousReady) EndCityActivity(a, CityActivityPhase.Cancelled, "本次等候结束，尚未选择服务，不扣消费", now);
                        continue;
                    }
                    if(!CityVenueFits(venue,now,a.Duration)){EndCityActivity(a,CityActivityPhase.Interrupted,"闭店前无法完成，未收整场费用，请改约",now);continue;}
                    Money cost=VenuePlayerPrice(def);
                    string serviceTitle = a.ServiceConfirmed ? VenueServiceName(def, a.ServiceChoice) : def.Name;
                    if(!a.EntryPaid && cost.IsPositive && !Ledger.TryDebit(Clock.Today,"城市活动",cost,serviceTitle,new LedgerAttribution(LedgerBusinessKind.Personal,activityId:a.Id,sourceEventId:a.Id+":entry",transactionKind:"活动消费")))
                    {EndCityActivity(a,CityActivityPhase.Cancelled,"开始时现金不足，未扣活动费",now);continue;}
                    a.PlayerStatsBefore=CityActivityPlayerStats();
                    a.EntryCostCents=cost.Cents;a.EntryCostRecorded=true;
                    foreach(var participant in a.Participants)
                    {
                        participant.ResultCaptured=true;
                        participant.RelationBefore=Relation(participant.PersonId);
                        participant.TrustBefore=CityTrust(participant.PersonId);
                    }
                    a.EntryPaid=true;a.State=CityActivityPhase.Running;a.ActualStart=now;a.Stage=0;
                    foreach(var participant in a.Participants){participant.Phase=CityParticipantPhase.Participating;var p=CityPersonState(participant.PersonId);p.Task=CityPersonTask.Participating;}
                    foreach(var shop in _shops)shop.Sim.PlayerPresent=false;
                    if (a.ServiceConfirmed && a.ServiceChoice >= 0) ChooseCityActivity(a.Id, a.ServiceChoice);
                    Say("开始"+serviceTitle+"："+def.StartText+"。世界时间和后台经营正常继续。");
                }
                // A service ending exactly at closing has fulfilled its full duration.
                if(CurrentPlace!=venue||!IsOwnedVenue(venue)&&!CityWithinHours(Clock.MinuteOfDay,venue.OpenMinute,venue.CloseMinute)&&now<a.ActualStart+a.Duration||_sleeping||IsWorking||IsGameOver)
                {EndCityActivity(a,CityActivityPhase.Interrupted,"离开场所或被必要状态中断，只保留已发生阶段",now);continue;}
                long elapsed=now-a.ActualStart;
                if(elapsed>=a.Duration/3 && a.Stage<1)
                {
                    a.Stage=1;
                    if(!a.MealApplied)
                    {
                        a.MealApplied=true;
                        Player.Hunger=Math.Min(100,Player.Hunger+def.Hunger);
                        if(def.Energy<0)Player.SpendEnergy(-def.Energy);
                        else Player.Energy=Math.Min(Player.MaxEnergy,Player.Energy+def.Energy);
                        Player.Mood=Math.Max(0,Math.Min(100,Player.Mood+def.Mood));
                        Player.Nutrition=Math.Max(0,Math.Min(100,Player.Nutrition+def.Nutrition));
                    }
                    Say(def.MidText);
                }
                if(elapsed>=a.Duration)EndCityActivity(a,CityActivityPhase.Completed,def.EndText,now);
            }
            CompleteCityTravel();
        }
        public bool ChooseCityActivity(string activityId,int choice)
        {
            var a=_cityActivities.FirstOrDefault(x=>x.Id==activityId&&x.State==CityActivityPhase.Running);
            var def=CityActivityService(a);
            if(def==null||choice<0||choice>=def.Choices.Length||CurrentPlace?.Id!=a.VenueId)return false;
            string key=choice.ToString();if(a.Choices.Contains(key)){Say("已经做过这个互动，可以继续相处等待活动结束。");return true;}
            a.Choices=a.Choices.Concat(new[]{key}).ToArray();
            if(!a.MidApplied)
            {
                a.MidApplied=true;
                foreach(var p in a.Participants){AddRelation(p.PersonId,1);RememberSocial(p.PersonId,"活动中："+def.Choices[choice]);}
            }
            OnCityActivityChoice(a,choice);
            Say(def.Choices[choice]+"："+(choice<2?def.MidText:"聊到各自的兴趣和近况。"));return true;
        }
        public bool CancelCityActivity(string id,bool early=false)
        {
            var a=_cityActivities.FirstOrDefault(x=>x.Id==id&&x.Active);if(a==null)return false;
            EndCityActivity(a,a.State==CityActivityPhase.Running?CityActivityPhase.Interrupted:CityActivityPhase.Cancelled,
                a.EntryPaid?"提前离开：已支付的实际消费保留，未完成部分不给完成奖励":"提前取消：未发生消费，不扣整场费用",Clock.Now.TotalMinutes);
            if(CityIntent?.ActivityId==id){StopTrip();CancelCityTravel();}
            return true;
        }
        private void EndCityActivity(CityActivity a,CityActivityPhase phase,string reason,long now)
        {
            if(!a.Active)return;
            if(CurrentPlace?.Id==a.VenueId&&a.PlayerVenueRoute!=null)_free=a.PlayerVenueRoute.Position(now);
            a.State=phase;a.ActualEnd=now;a.EndReason=reason;a.Stage=phase==CityActivityPhase.Completed?2:a.Stage;
            if(CityIntent?.ActivityId==a.Id&&CityIntent.Id==a.PlayerIntentId){StopTrip();CancelCityTravel();}
            if(phase==CityActivityPhase.Completed&&!a.RewardApplied)
            {
                a.RewardApplied=true;var def=CityActivityService(a);
                foreach(var participant in a.Participants)
                {
                    AddRelation(participant.PersonId,def.Relation);_cityTrust[participant.PersonId]=CityTrust(participant.PersonId)+1;
                    _outingDay[participant.PersonId]=Clock.Today.TotalDays;
                    RememberSocial(participant.PersonId,"实际完成"+def.Name+"。"+reason);
                    if(a.DefinitionId=="dinner"&&!IsMet(participant.PersonId))
                    {
                        Meet(participant.PersonId,Relation(participant.PersonId));
                        Say("聚餐后和"+(CityPerson(participant.PersonId)?.Name??"同行者")+"互留了联系方式，已存入通讯录。");
                    }
                }
                Player.Health=Math.Max(0,Math.Min(100,Player.Health+def.Health));
                Player.Charm=Math.Min(100,Player.Charm+def.Charm);
                Player.Intelligence=Math.Min(100,Player.Intelligence+def.Intelligence);
                RecordCommunityOuting(a);
                OnCityActivityCompleted(a);
            }
            foreach(var participant in a.Participants)
            {
                participant.Phase=CityParticipantPhase.Left;
                var p=CityPersonState(participant.PersonId);
                if(p!=null&&p.ActivityId==a.Id)
                {
                    if(p.Route!=null&&p.Route.IntentId==participant.IntentId)
                    {var position=p.Route.Position(now);p.X=position.X;p.Y=position.Y;p.Route=null;p.LocationPlaceId=null;}
                    p.ActivityId=null;
                    if(participant.WorkForEmployer)
                    {
                        var worker=Employees.FirstOrDefault(e=>PersonIdOf(e.Person)==participant.PersonId);
                        if(worker?.Shop!=null&&worker.Person.OnDuty(Clock.Now))
                        {if(worker.Person.Schedule==null)worker.Person.Schedule=new EmployeeSchedule();worker.Person.Schedule.WorkTravelTarget=worker.Shop.Place.Id;}
                    }
                }
                _appointments.RemoveAll(x=>x.NpcId==participant.PersonId && Math.Abs((long)x.Day*1440+x.StartMinute-a.ScheduledStart)<240);
            }
            if (a.EntryPaid && (phase==CityActivityPhase.Completed || phase==CityActivityPhase.Interrupted))
            {
                var def=CityActivityService(a);
                a.PlayerStatsAfter=CityActivityPlayerStats();
                string attendance=a.Participants.Length==0?"你独自":"你和"+string.Join("、",a.Participants.Select(p=>CityPerson(p.PersonId)?.Name??"朋友"));
                var lines=new List<string> {attendance+"在"+City.GetPlace(a.VenueId).Name+"参加了"+(def?.Name??"活动")+"。",
                    "本次服务："+VenueServiceName(def,Math.Max(0,a.ServiceChoice))+" · 支出 "+(a.EntryCostRecorded?Money.FromCents(a.EntryCostCents):Money.FromYuan(def?.PriceYuan??0)),
                    "体验时长："+Math.Max(0,now-a.ActualStart)+" 分钟", ""};
                string[] statNames={"饱腹","精力","心情","健康","魅力","见识","营养"};
                lines.Add("你的变化（含这段时间的正常消耗）：");
                for(int i=0;i<statNames.Length;i++)
                {
                    double after=a.PlayerStatsAfter[i];
                    if(a.PlayerStatsBefore!=null && a.PlayerStatsBefore.Length==statNames.Length)
                    {
                        double before=a.PlayerStatsBefore[i]; double delta=Math.Round(after-before,1);
                        lines.Add(statNames[i]+" "+before.ToString("0.#")+" → "+after.ToString("0.#")
                            +"（"+(delta>0?"+":"")+delta.ToString("0.#")+"）");
                    }
                    else lines.Add(statNames[i]+"："+after.ToString("0.#"));
                }
                lines.Add("");
                foreach(var participant in a.Participants)
                {
                    participant.RelationAfter=Relation(participant.PersonId); participant.TrustAfter=CityTrust(participant.PersonId);
                    string name=CityPerson(participant.PersonId)?.Name??"朋友";
                    lines.Add(name+"：");
                    if(participant.ResultCaptured)
                    {
                        int delta=participant.RelationAfter-participant.RelationBefore;
                        lines.Add("好感 "+participant.RelationBefore+" → "+participant.RelationAfter
                            +(delta==0&&participant.RelationAfter>=Npcs.MaxRelation?"（已满）":"（"+(delta>=0?"+":"")+delta+"）"));
                        lines.Add("履约信任 "+participant.TrustBefore+" → "+participant.TrustAfter);
                    }
                    else lines.Add("当前好感 "+participant.RelationAfter+" · 履约信任 "+participant.TrustAfter);
                    lines.Add(phase==CityActivityPhase.Completed?"共同经历：完成了这次邀约，相处已记入人脉记录。":"本次提前结束，未获得完整赴约奖励。");
                    if(phase==CityActivityPhase.Completed&&a.DefinitionId=="dinner"&&IsMet(participant.PersonId))
                        lines.Add("已互留联系方式，可在手机通讯录继续联系。");
                    lines.Add("");
                }
                var interactions=a.Choices.Select(key=>int.TryParse(key,out int index)&&index>=0&&index<(def?.Choices.Length??0)?def.Choices[index]:null).Where(text=>text!=null).ToArray();
                if(interactions.Length>0)lines.Add((a.Participants.Length==0?"体验内容：":"一起做过：")+string.Join("、",interactions));
                lines.Add(reason);
                a.SettlementSummary=string.Join("\n",lines);
                CityActivitySettled?.Invoke(a.Id);
            }
            Say(reason+"；你仍在当前场所，可回店、回家或继续逛。");
        }
        private double[] CityActivityPlayerStats() => new double[] {
            Player.Hunger,Player.Energy,Player.Mood,Player.Health,Player.Charm,Player.Intelligence,Player.Nutrition
        };

        private void MigrateCityAppointments()
        {
            foreach(var old in _appointments.ToArray())
            {
                string source="legacy_appointment:"+old.NpcId+":"+old.Day+":"+(int)old.Slot;
                if(_cityActivities.Any(a=>a.SourceId==source))continue;
                long start=(long)old.Day*1440+old.StartMinute;
                var a=new CityActivity{Id="activity_"+_nextCityActivity++,DefinitionId="dinner",VenueId="life_restaurant",SourceId=source,
                    ScheduledStart=start,Duration=60,State=start+CityArrivalWindowMinutes<Clock.Now.TotalMinutes?CityActivityPhase.NoShow:CityActivityPhase.Scheduled,
                    EndReason=start+CityArrivalWindowMinutes<Clock.Now.TotalMinutes?"迁移时预约已过期，保留历史且不重新消费":null,
                    Participants=new[]{new CityActivityParticipant{PersonId=old.NpcId}}};
                _cityActivities.Add(a);
            }
        }
    }
}
