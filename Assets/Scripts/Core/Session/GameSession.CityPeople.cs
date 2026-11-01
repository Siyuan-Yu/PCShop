using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private readonly Dictionary<string, CityPersonState> _cityPeople = new Dictionary<string, CityPersonState>();
        private readonly Dictionary<string,string> _cityEmployeeAliases = new Dictionary<string,string>();
        private readonly Dictionary<string,CityPersonDefinition> _cityExtraDefinitions = new Dictionary<string,CityPersonDefinition>();
        private readonly Dictionary<string,int> _cityTopicDay = new Dictionary<string,int>();
        private readonly Dictionary<string,int> _cityTrust = new Dictionary<string,int>();
        private readonly Dictionary<string,PathResult> _cityPathCache = new Dictionary<string,PathResult>();
        public IReadOnlyCollection<CityPersonState> CityPeople => _cityPeople.Values;
        public CityPersonDefinition CityPerson(string id) => CityLifeCatalog.Person(id) ?? (_cityExtraDefinitions.TryGetValue(id??"",out var p)?p:null);
        public CityPersonState CityPersonState(string id) => _cityPeople.TryGetValue(id??"",out var p)?p:null;
        public int CityTrust(string id) => _cityTrust.TryGetValue(id??"",out int trust)?trust:0;
        public string CityPersonRole(string id)
        {
            if(string.IsNullOrEmpty(id))return "居民";
            var job=Employees.FirstOrDefault(e=>e.Person.PersonId==id||
                (_cityEmployeeAliases.TryGetValue(e.Person.Id,out var known)&&known==id));
            return job!=null?RoleName(job.Role)+"（受聘）":CityPerson(id)?.Role??"居民";
        }
        public IEnumerable<CityPersonState> PeopleInPlace(string placeId) => _cityPeople.Values.Where(p=>p.LocationPlaceId==placeId && p.Route==null);
        // Coordinates stay at the entrance as a route anchor while the person is inside.
        // They are not a street position until a commute actually starts.
        public bool CityPersonInside(CityPersonState p)
        {
            if (p == null || p.Route != null || string.IsNullOrEmpty(p.LocationPlaceId)) return false;
            if (!string.IsNullOrEmpty(p.ActivityId) && _cityActivities.Any(a=>a.Id==p.ActivityId && a.Active && a.RendezvousReady)) return false;
            string business = City.HasPlace(p.LocationPlaceId) ? City.GetPlace(p.LocationPlaceId).Business : null;
            // Outdoor workplaces keep their people visible at the actual venue.
            if (business == "venue_court" || business == "venue_golf" || business == "venue_park" || business == "venue_hike") return false;
            return p.Task == CityPersonTask.Home || p.Task == CityPersonTask.Working || p.Task == CityPersonTask.Shopping
                || p.Task == CityPersonTask.Waiting && p.LocationPlaceId == p.WorkPlaceId;
        }
        public static bool CityWithinHours(int minute,int open,int close) => open==close || (close>open?minute>=open&&minute<close:minute>=open||minute<close);
        public bool CitySamePlace(string personId)
        {
            var person = CityPersonState(personId);
            if (person == null) return false;
            if (CurrentPlace != null) return person.Route == null && person.LocationPlaceId == CurrentPlace.Id;
            if (CityPersonInside(person) || person.Route != null && person.Route.Transport != VehicleKind.Walk) return false;
            // A walking resident can be stopped for conversation. Match the rendered fractional
            // route position rather than rejecting everyone who is currently on their way somewhere.
            var position = person.Route?.Position(Clock.Now.TotalMinutes + Time.FractionalMinute)
                ?? new MapPoint(person.X, person.Y);
            return DistanceCity(PlayerPosition, position) <= 4;
        }

        private void InitializeCityPeople()
        {
            if(!DebugInvitationPersonEnabled)RemoveInvitationTestPerson();
            foreach(var d in CityLifeCatalog.People)
            {
                if(d.Id==CityLifeCatalog.InvitationTestPersonId && !DebugInvitationPersonEnabled)continue;
                if(_cityPeople.ContainsKey(d.Id)) continue;
                string home=City.HasPlace(d.HomeId)?d.HomeId:Home.Id;
                var pos=StandOf(City.GetPlace(home));
                var p=new CityPersonState {PersonId=d.Id,HomePlaceId=home,Address=City.GetPlace(home).Name+" "+(1+_cityPeople.Count/8)+"栋"+(101+_cityPeople.Count%8)+"室",
                    WorkPlaceId=City.HasPlace(d.WorkId)?d.WorkId:null,LocationPlaceId=home,Task=CityPersonTask.Home,X=pos.X,Y=pos.Y};
                _cityPeople.Add(p.PersonId,p);
                if (d.Id == CityLifeCatalog.InvitationTestPersonId)
                {
                    p.Address="安居房产中介附近"; p.LocationPlaceId=null;
                    p.Task=CityPersonTask.Leisure; p.TaskLabel="在中介附近散步，随时可以邀约";
                }
                if(p.WorkPlaceId!=null && CityWithinHours(Clock.MinuteOfDay,d.WorkStart,d.WorkEnd))
                { p.LocationPlaceId=p.WorkPlaceId;p.Task=CityPersonTask.Working;pos=StandOf(City.GetPlace(p.WorkPlaceId));p.X=pos.X;p.Y=pos.Y; }
            }
            if(DebugInvitationPersonEnabled)
            {
                _relations[CityLifeCatalog.InvitationTestPersonId]=Npcs.MaxRelation;
                Flags.Add(MetFlag(CityLifeCatalog.InvitationTestPersonId));
            }
            foreach(var e in Employees.Select(a=>a.Person).Concat(_candidates).Where(e=>e!=null)) EnsureCityEmployee(e);
        }
        public string PersonIdOf(Employee e)
        {
            if(e==null)return null;
            EnsureCityEmployee(e);
            return _cityEmployeeAliases[e.Id];
        }
        private void EnsureCityEmployee(Employee e)
        {
            if(!_cityEmployeeAliases.TryGetValue(e.Id,out string id))
            {
                id=!string.IsNullOrEmpty(e.PersonId)?e.PersonId:"employee_"+e.Id;
                _cityEmployeeAliases[e.Id]=id;
            }
            e.PersonId=id;
            if(CityPerson(id)==null) _cityExtraDefinitions[id]=new CityPersonDefinition {Id=id,Name=e.Name,Role="受聘员工",Age=28,
                HomeId="life_home_s_oldhomes",Interest="稳定的工作和业余生活",Goal=e.Description??"攒下生活费，找一份可靠工作。",
                Experience="我做过零售和仓储，普通的工作会按规矩可靠完成。",Recruitable=true,
                Topics=new[]{"基本工作按岗位规则做，有问题会明确告诉你。","下班后也想保留一点自己的时间。",e.Description??"我在攒一笔应急钱。","我也认识社区里的街坊。"},
                Stories=new[]{"最初来这座城时，我也从临时工作开始。",e.Description??"现在希望工作和生活都稳定一点。","以后想学一门更熟练的手艺。"}};
            if(!_cityPeople.ContainsKey(id))
            {
                var d=CityPerson(id);string home=City.HasPlace(d.HomeId)?d.HomeId:Home.Id;var pos=StandOf(City.GetPlace(home));
                _cityPeople[id]=new CityPersonState {PersonId=id,HomePlaceId=home,Address=City.GetPlace(home).Name+" 员工合租单元",LocationPlaceId=home,X=pos.X,Y=pos.Y};
            }
        }
        private void BindCityCandidate(Employee e)
        {
            InitializeCityPeople();
            var used=new HashSet<string>(Employees.Select(a=>PersonIdOf(a.Person)).Concat(_candidates.Select(PersonIdOf)));
            var def=CityLifeCatalog.People.FirstOrDefault(p=>p.Recruitable&&!used.Contains(p.Id));
            if(def!=null){e.PersonId=def.Id;e.Name=def.Name;e.Description=def.Goal;}
            EnsureCityEmployee(e);
        }
        private void TickCityPeople(long now)
        {
            InitializeCityPeople();
            foreach(var p in _cityPeople.Values.ToArray())
            {
                var d=CityPerson(p.PersonId); if(d==null)continue;
                var employment=Employees.FirstOrDefault(a=>PersonIdOf(a.Person)==p.PersonId);
                var purchase=employment==null?null:EmployeeTrip(employment.Person.Id);
                if(purchase!=null)
                {
                    p.WorkPlaceId=employment.Shop.Place.Id;
                    ApplyCityPurchasePosition(purchase,p,now);
                    continue;
                }
                if(p.Route!=null)
                {
                    var pos=p.Route.Position(now);p.X=pos.X;p.Y=pos.Y;
                    if(now<p.Route.ArriveMinute)continue;
                    p.LocationPlaceId=p.Route.TargetPlaceId;p.Route=null;
                }
                if(!string.IsNullOrEmpty(p.ActivityId))continue;
                if(!string.IsNullOrEmpty(p.VisitId)){p.Task=CityPersonTask.Shopping;continue;}
                if (p.PersonId == CityLifeCatalog.InvitationTestPersonId)
                {
                    TickInvitationTestWander(p, now);
                    continue;
                }
                string work=employment?.Shop?.Place.Id;
                Employee employee=employment?.Person;
                if(employee!=null)
                {
                    p.WorkPlaceId=work;
                }
                else p.WorkPlaceId=d.Recruitable?null:City.HasPlace(d.WorkId)?d.WorkId:null;
                var candidate=_candidates.FirstOrDefault(e=>PersonIdOf(e)==p.PersonId);
                bool shift=employee!=null?employee.OnDuty(Clock.Now):p.WorkPlaceId!=null&&CityWithinHours(Clock.MinuteOfDay,d.WorkStart,d.WorkEnd);
                string target=shift?p.WorkPlaceId:p.HomePlaceId;
                var desiredTask=shift?CityPersonTask.Working:CityPersonTask.Home;
                if(candidate!=null && CandidateAvailable(candidate)) {target=PlayerShopPlace.Id;desiredTask=CityPersonTask.Waiting;p.TaskLabel="等待面试";}
                if (candidate == null)
                {
                    string outing = CityOutingDestination(p, employee != null, shift, now);
                    if (outing != null)
                    {
                        target = outing;
                        desiredTask = p.LocationPlaceId == outing && City.GetPlace(outing).Kind == PlaceKind.Shop
                            ? CityPersonTask.Shopping : CityPersonTask.Leisure;
                    }
                }
                // Reserve a real commute before the next shift rather than appearing at the counter.
                if(!shift && candidate==null && p.WorkPlaceId!=null)
                {
                    int start=employee?.ShiftStart??d.WorkStart;
                    int delta=(start-Clock.MinuteOfDay+1440)%1440;
                    var commute=PlanCityRoute(p,p.WorkPlaceId,now);
                    if(commute!=null && delta>0 && delta<=commute.ArriveMinute-now+3)
                    {target=p.WorkPlaceId;desiredTask=CityPersonTask.Waiting;p.LeisureTargetId=null;p.LeisureNextMove=0;}
                }
                if(target!=null && City.HasPlace(target) && p.LocationPlaceId!=target)
                {
                    var route=PlanCityRoute(p,target,now);
                    if(route!=null) {p.Route=route;p.LocationPlaceId=null;p.Task=CityPersonTask.Travelling;p.TaskLabel=route.Transport==VehicleKind.Taxi?"搭乘出租车出行（个人支付）":"步行前往";}
                    else {p.TaskLabel="路线暂不可达";}
                }
                else {p.Task=desiredTask;if(desiredTask!=CityPersonTask.Waiting)p.TaskLabel=desiredTask==CityPersonTask.Working?"当班工作":desiredTask==CityPersonTask.Home?"在家休息":desiredTask==CityPersonTask.Shopping?"日常买东西":"日常逛街与散步";}
            }
        }
        private void TickInvitationTestWander(CityPersonState person, long now)
        {
            string agencyId=City.HasPlace("agency")?"agency":City.FirstOfKind(PlaceKind.Agency)?.Id;
            if (agencyId==null) return;
            var agency=City.GetPlace(agencyId); var anchor=StandOf(agency);
            person.WorkPlaceId=null; person.LocationPlaceId=null; person.Task=CityPersonTask.Leisure;
            person.TaskLabel="在中介附近散步，随时可以邀约";
            // After a normal outing, return along the real road network. Never teleport from a venue.
            if (DistanceCity(new MapPoint(person.X,person.Y),anchor)>8)
            {
                person.Route=PlanCityRoute(person,agencyId,now);
                return;
            }
            if (now<person.LeisureNextMove) return;
            double yaw=agency.Heading*Math.PI/180;
            int stop=person.LeisureStop++%4;
            double offset=new[]{-3.0,-1.0,2.5,0.5}[stop];
            var end=new MapPoint(anchor.X+Math.Cos(yaw)*offset,anchor.Y-Math.Sin(yaw)*offset);
            var start=new MapPoint(person.X,person.Y);
            double meters=CityTravelTiming.ViewLegMeters(City,start,end);
            long arrival=now+Math.Max(1,CityTravelTiming.NpcMinutes(meters,Vehicles.Walk));
            person.Route=new CityRoute {
                IntentId="invitation-test:walk:"+now, TargetPlaceId=agencyId, Transport=VehicleKind.Walk,
                Nodes=Array.Empty<string>(), X=new[]{start.X,end.X}, Y=new[]{start.Y,end.Y}, Meters=new[]{meters},
                DepartMinute=now, ArriveMinute=arrival
            };
            person.LeisureNextMove=arrival+3;
        }

        private string CityOutingDestination(CityPersonState person, bool employed, bool working, long now)
        {
            int minute = Clock.MinuteOfDay;
            var resident = Residents.Get(person.PersonId);
            bool outing = false;
            if (!employed && resident != null)
                for (int i = 0; i + 1 < resident.Outings.Length; i += 2)
                    if (minute >= resident.Outings[i] && minute < resident.Outings[i + 1]) { outing = true; break; }
            // Only authored lunch/rest breaks may take a shopkeeper out during work.
            // Hired employees keep their actual shifts, appointments and purchase trips.
            int seed = ConversationSeed(person.PersonId);
            if (!working)
            {
                if (person.WorkPlaceId == null && resident == null)
                    outing = minute >= 8 * 60 + seed % 90 && minute < 20 * 60 + seed % 60;
                else if (!outing && (resident == null || resident.Outings.Length == 0))
                    outing = minute >= 17 * 60 + seed % 120 && minute < 21 * 60 + seed % 60;
            }
            if (!outing || employed && working)
            { person.LeisureTargetId = null; person.LeisureNextMove = 0; return null; }
            if (person.LeisureTargetId != null && City.HasPlace(person.LeisureTargetId))
            {
                if (person.LocationPlaceId != person.LeisureTargetId) return person.LeisureTargetId;
                if (person.LeisureNextMove == 0) person.LeisureNextMove = now + (working ? 5 : 5 + seed % 11);
                if (now < person.LeisureNextMove) return person.LeisureTargetId;
                person.LeisureStop++; person.LeisureNextMove = 0;
            }
            // Visits have real routes and short pauses rather than everyone standing at one park all afternoon.
            string[] destinations = { "shop14", "shop07", "life_park", "shop15", "shop01", "metro_a", "shop16", "life_court", "metro_b" };
            for (int i = 0; i < destinations.Length; i++)
            {
                string target = destinations[(seed % destinations.Length + person.LeisureStop + i) % destinations.Length];
                if (!City.HasPlace(target) || target == person.HomePlaceId || target == person.LocationPlaceId || target == person.WorkPlaceId) continue;
                var place = City.GetPlace(target);
                if (working && person.WorkPlaceId != null && City.HasPlace(person.WorkPlaceId))
                {
                    var from = City.ViewPosition(City.GetPlace(person.WorkPlaceId).NodeId);
                    var to = City.ViewPosition(place.NodeId);
                    if (DistanceCity(from, to) > 30) continue; // Lunch/rest breaks stay in the neighbourhood.
                }
                if (place.Kind == PlaceKind.Shop && (IsVacant(place) || IsMyShop(place) || ClosedReason(place) != null)) continue;
                person.LeisureTargetId = target; return target;
            }
            person.LeisureTargetId = null; return null;
        }

        private CityRoute PlanCityRoute(CityPersonState p,string target,long now,long latestArrival=long.MaxValue,VehicleKind? transportChoice=null)
        {
            if(!City.HasPlace(target))return null;
            var place=City.GetPlace(target);string from=City.NearestNode(p.X,p.Y);
            string key=from+"|"+place.NodeId;
            if(!_cityPathCache.TryGetValue(key,out var path)){path=Pathfinder.FindPath(City.Map,from,place.NodeId);_cityPathCache[key]=path;}
            if(!path.Found)return null;
            var points=new List<MapPoint>{new MapPoint(p.X,p.Y)};
            foreach(string node in path.NodeIds)
            {
                points.Add(City.ViewPosition(node));
            }
            var end=StandOf(place);points.Add(end);
            var meters=CityTravelTiming.RouteMeters(City,new MapPoint(p.X,p.Y),path.NodeIds,end,
                p.LocationPlaceId!=null?City.TravelDistanceScale:CityTravelTiming.LocalScale(City,new MapPoint(p.X,p.Y)));
            // Long cross-city journeys use ordinary personal transport. Both the reservation
            // estimate and actual route use this rule, without changing the player's vehicle/fare.
            int walking=CityTravelTiming.NpcMinutes(meters.Sum(),Vehicles.Walk);
            // Appointments have a real arrival deadline. A previously selected taxi remains
            // selected even if a normal commute moves the person just below the 90-minute
            // walking threshold; otherwise a late switch to walking can make a valid date fail.
            // Walking may upgrade to ordinary personal transport when the remaining window
            // becomes shorter, but it never downgrades again within this activity.
            var vehicle=transportChoice==VehicleKind.Taxi || walking>90 ||
                latestArrival!=long.MaxValue&&now+walking>latestArrival?Vehicles.Taxi:Vehicles.Walk;
            return new CityRoute{IntentId="npc:"+p.PersonId+":"+now+":"+target,TargetPlaceId=target,Nodes=path.NodeIds.ToArray(),
                X=points.Select(x=>x.X).ToArray(),Y=points.Select(x=>x.Y).ToArray(),Meters=meters,DepartMinute=now,
                Transport=vehicle.Kind,ArriveMinute=now+Math.Max(1,CityTravelTiming.NpcMinutes(meters.Sum(),vehicle))};
        }
        private static double DistanceCity(MapPoint a,MapPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));

        public bool CityEmployeeAvailable(Employee e,OwnedShop shop)
        {
            if(e==null||shop==null||!e.OnDuty(Clock.Now)||EmployeeTrip(e.Id)!=null)return false;
            string id=PersonIdOf(e);var p=CityPersonState(id);
            return p!=null && p.Route==null && p.LocationPlaceId==shop.Place.Id && string.IsNullOrEmpty(p.ActivityId);
        }
        public bool CityPlayerCanWork(OwnedShop shop) => shop!=null&&CurrentPlace==shop.Place&&!CityActivityExclusive&&!IsWorking&&!_sleeping;
        public string CityServiceReason(OwnedShop shop)
        {
            if(shop==null)return "未开店";
            if(CityPlayerCanWork(shop)||CityEmployeeAvailable(shop.Employee,shop))return "可销售";
            if(CityEmployeeAvailable(shop.Assembler,shop))return shop.EmployeeBuilding?"装机值守，收银需等待":"可销售（装机员兼收银）";
            return "暂停服务：无人值守";
        }
        public string CityTalk(string id,string topic)
        {
            var p=CityPersonState(id);var d=CityPerson(id);
            if(p==null||d==null)return null;
            if(!CitySamePlace(id))return "对方不在这里。通讯录可查看已知资料，聊天需实际见面。";
            if (topic == "friends" && Relation(id) < AskAboutOthersMinRelation) return "咱们再熟悉一些，再慢慢聊我的朋友吧。";
            GreetPerson(id); int index=topic=="work"?0:topic=="interest"?1:topic=="life"?2:3;
            string key=id+":"+topic;bool fresh=!_cityTopicDay.TryGetValue(key,out int day)||day!=Clock.Today.TotalDays;
            if(fresh){_cityTopicDay[key]=Clock.Today.TotalDays;AddRelation(id,1);p.KnownTopics=p.KnownTopics.Union(new[]{topic}).ToArray();}
            if (topic == "friends")
            {
                var circle = PersonCircle(id).ToArray();
                if (circle.Length > 0) return AskAboutPerson(id, circle[Clock.Today.TotalDays % circle.Length].PersonId);
            }
            string text=d.Topics!=null&&d.Topics.Length>index?d.Topics[index]:d.Goal;
            if(topic=="work")text=(p.WorkPlaceId!=null&&City.HasPlace(p.WorkPlaceId)?"我现在的工作在"+City.GetPlace(p.WorkPlaceId).Name+"。":"我现在没有固定岗位。")+"\n"+text;
            if (topic == "interest") text = "我平时喜欢" + ConversationTraits(id)[0].Name + "。有空的时候，也想给自己放个假。";
            else if (Npcs.Get(id) != null) text = NpcTalk.SmallTalkLine(id, Clock.Today.TotalDays);
            RememberSocial(id,(fresh?"聊了新话题：":"再次聊起：")+text);
            OnCityStoryTalk(id,topic);
            return text;
        }
        public string CityHearStory(string id)
        {
            var p=CityPersonState(id);var d=CityPerson(id);
            if(p==null||d==null||!CitySamePlace(id))return "需要当面听对方讲。";
            p.StoryChapter=Math.Max(p.StoryChapter,StaffStoriesHeard(id));
            if(p.LastStoryDay==Clock.Today.TotalDays||_staffStoryDay.TryGetValue(id,out int toldDay)&&toldDay==Clock.Today.TotalDays)return "今天已经聊了不少，改天再听下一段。";
            if(d.Stories==null||p.StoryChapter>=d.Stories.Length)return d.Goal;
            if(Relation(id)<5+p.StoryChapter*5)return "再熟悉一点，对方愿意慢慢讲自己的经历。";
            string line=d.Stories[p.StoryChapter++];p.LastStoryDay=Clock.Today.TotalDays;
            if(Rosters.Get(id)!=null||Residents.Get(id)!=null){_staffStoryCount[id]=p.StoryChapter;_staffStoryDay[id]=Clock.Today.TotalDays;}
            AddRelation(id,2);RememberSocial(id,line);return line;
        }
    }
}
