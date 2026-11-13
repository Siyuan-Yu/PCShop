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
        // Only travel duration is reused, never a person's route or departure timestamp.
        // Exact positions preserve commute decisions while avoiding a full route allocation
        // every minute that a resident stays at the same doorway.
        private readonly Dictionary<(double x,double y,string target,bool inside),long> _cityCommuteMinutes = new Dictionary<(double,double,string,bool),long>();
        private int _cityCommuteNodeCount = -1;
        private long EstimateCityCommute(CityPersonState person,string target,long now)
        {
            if(_cityCommuteNodeCount!=City.Map.NodeCount)
            {
                _cityCommuteMinutes.Clear();_cityPathCache.Clear();
                _cityCommuteNodeCount=City.Map.NodeCount;
            }
            var key=(person.X,person.Y,target,person.LocationPlaceId!=null);
            if(_cityCommuteMinutes.TryGetValue(key,out long minutes))return minutes;
            var route=PlanCityRoute(person,target,now);
            minutes=route==null?-1:route.ArriveMinute-now;
            if(_cityCommuteMinutes.Count>=4096)_cityCommuteMinutes.Clear();
            _cityCommuteMinutes[key]=minutes;
            return minutes;
        }
        public IReadOnlyCollection<CityPersonState> CityPeople => _cityPeople.Values;
        public CityPersonDefinition CityPerson(string id) => CityLifeCatalog.Person(id) ?? (_cityExtraDefinitions.TryGetValue(id??"",out var p)?p:null);
        public CityPersonState CityPersonState(string id) => _cityPeople.TryGetValue(id??"",out var p)?p:null;
        public int CityTrust(string id) => _cityTrust.TryGetValue(id??"",out int trust)?trust:0;
        public string CityPersonRole(string id)
        {
            if(string.IsNullOrEmpty(id))return "居民";
            var job=Employees.FirstOrDefault(e=>e.Person.PersonId==id||
                (_cityEmployeeAliases.TryGetValue(e.Person.Id,out var known)&&known==id));
            return job!=null?(job.Role==EmployeeRole.Cashier&&IsDedicatedVenue(job.Shop?.Place)?"场馆值守员":RoleName(job.Role))+"（受聘）":CityPerson(id)?.Role??"居民";
        }
        /// <summary>这个人的名望：玩家是经营出来的共用值，NPC 按身份推算。见 <see cref="PersonStanding"/>。</summary>
        public double PersonStandingOf(string personId)
        {
            if(personId==null)return 0;
            var def=CityPerson(personId);var npc=Npcs.Get(personId);var staff=Rosters.Get(personId)??Residents.Get(personId)?.Person;
            string role=def?.Role??npc?.Role??staff?.Role;
            return PersonStanding.ForNpc(personId,role,def?.Age??35,def?.KeyCharacter==true||npc!=null,npc?.StallId!=null);
        }
        public IEnumerable<CityPersonState> PeopleInPlace(string placeId) => _cityPeople.Values.Where(p=>p.LocationPlaceId==placeId && p.Route==null);
        // Coordinates stay at the entrance as a route anchor while the person is inside.
        // They are not a street position until a commute actually starts.
        public bool CityPersonInside(CityPersonState p)
        {
            if (p == null || p.Route != null || string.IsNullOrEmpty(p.LocationPlaceId)) return false;
            if (StreetServicePlace(p.LocationPlaceId)) return false;
            if(_district.MeetingNodeId==_district.NodeId && _district.MeetingPeople.Contains(p.PersonId) && p.LocationPlaceId==_district.MeetingPlaceId)return false;
            if (!string.IsNullOrEmpty(p.ActivityId) && _cityActivities.Any(a=>a.Id==p.ActivityId && a.Active && a.RendezvousReady)) return false;
            string business = City.HasPlace(p.LocationPlaceId) ? City.GetPlace(p.LocationPlaceId).Business : null;
            // Outdoor workplaces keep their people visible at the actual venue.
            if (business == "venue_court" || business == "venue_golf" || business == "venue_park" || business == "venue_hike") return false;
            return p.Task == CityPersonTask.Home || p.Task == CityPersonTask.Working || p.Task == CityPersonTask.Shopping
                || p.Task == CityPersonTask.Waiting && p.LocationPlaceId == p.WorkPlaceId;
        }
        // Agencies and the expo reception have no walkable city interior. Keep their
        // staff and mainline meetings on the road network, clear of door interactions.
        private bool StreetServicePlace(string placeId) => City.IsLakeCity && City.HasPlace(placeId)
            && (City.GetPlace(placeId).Kind == PlaceKind.Agency
                || City.GetPlace(placeId).Business == "venue_expo");
        private void PositionStreetServicePeople()
        {
            var groups = new Dictionary<string, List<CityPersonState>>();
            foreach (var person in _cityPeople.Values)
            {
                if (person.Route != null) continue;
                bool meeting = _district.MeetingNodeId == _district.NodeId
                    && _district.MeetingPeople.Contains(person.PersonId)
                    && person.LocationPlaceId == _district.MeetingPlaceId;
                string node = meeting ? DistrictMeetingStreetNode : null;
                if (node == null && StreetServicePlace(person.LocationPlaceId))
                    node = CommunityStreetNode(City.GetPlace(person.LocationPlaceId));
                if (node == null) continue;
                if (!groups.TryGetValue(node, out var people)) groups[node] = people = new List<CityPersonState>();
                people.Add(person);
            }
            foreach (var group in groups)
            {
                var point = City.ViewPosition(group.Key);
                var people = group.Value.OrderBy(p => p.PersonId, StringComparer.Ordinal).ToArray();
                for (int i = 0; i < people.Length; i++)
                {
                    // Keep one person at the navigation anchor and give everyone else
                    // a distinct spot within the meeting radius, clear of nearby doors.
                    double angle = 2 * Math.PI * (i - 1) / Math.Max(1, people.Length - 1);
                    people[i].X = point.X + (i == 0 ? 0 : (float)(2.8 * Math.Cos(angle)));
                    people[i].Y = point.Y + (i == 0 ? 0 : (float)(2.8 * Math.Sin(angle)));
                }
            }
        }
        public static bool CityWithinHours(int minute,int open,int close) => open==close || (close>open?minute>=open&&minute<close:minute>=open||minute<close);
        public const int HomeVisitMinRelation = 60;

        public IEnumerable<CityPersonState> ResidentsAtHome(Place home) =>
            PeopleInPlace(home.Id).Where(person => person.HomePlaceId == home.Id && CityPersonInside(person));

        public string HomeVisitBlocker(Place home)
        {
            var residents = CityPeople.Where(person => person.HomePlaceId == home.Id).ToArray();
            bool CanVisit(CityPersonState person) => Relation(person.PersonId) >= HomeVisitMinRelation
                || Flags.Contains("home-invitation:" + person.PersonId);
            if (residents.Length == 0) return "这是私人住宅，暂时没有可拜访的住户。";
            if (!residents.Any(CanVisit))
                return "拜访私人住宅需要与一位住户的好感达到 " + HomeVisitMinRelation + "。可以先在街上或工作地点认识对方。";
            if (!ResidentsAtHome(home).Any(CanVisit))
                return "熟悉的住户现在不在家，晚些再来拜访。";
            return null;
        }

        public bool CitySamePlace(string personId)
        {
            var person = CityPersonState(personId);
            if (person == null) return false;
            if (CurrentPlace != null)
                return person.Route == null && (person.LocationPlaceId == CurrentPlace.Id
                    || City.HasPlace(person.LocationPlaceId) && SharesWholesaleMarket(CurrentPlace, City.GetPlace(person.LocationPlaceId)))
                    && (CurrentPlace.Kind != PlaceKind.Home || CurrentPlace == Home || CityPersonInside(person));
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
                string home=City.HasPlace(d.HomeId)?City.CanonicalPlaceId(d.HomeId):Home.Id;
                var pos=StandOf(City.GetPlace(home));
                var p=new CityPersonState {PersonId=d.Id,HomePlaceId=home,Address=City.GetPlace(home).Name+" "+(1+_cityPeople.Count/8)+"栋"+(101+_cityPeople.Count%8)+"室",
                    WorkPlaceId=City.HasPlace(d.WorkId)?City.CanonicalPlaceId(d.WorkId):null,LocationPlaceId=home,Task=CityPersonTask.Home,X=pos.X,Y=pos.Y};
                _cityPeople.Add(p.PersonId,p);
                if (d.Id == CityLifeCatalog.InvitationTestPersonId)
                {
                    p.Address="安居房产中介附近"; p.LocationPlaceId=null;
                    p.Task=CityPersonTask.Leisure; p.TaskLabel="在中介附近散步，随时可以邀约";
                }
                if(AuthoredCityWorkAt(p,Clock.Now.TotalMinutes))
                { p.LocationPlaceId=p.WorkPlaceId;p.Task=CityPersonTask.Working;pos=StandOf(City.GetPlace(p.WorkPlaceId));p.X=pos.X;p.Y=pos.Y; }
            }
            if(DebugInvitationPersonEnabled)
            {
                _relations[CityLifeCatalog.InvitationTestPersonId]=Npcs.MaxRelation;
                Flags.Add(MetFlag(CityLifeCatalog.InvitationTestPersonId));
            }
            foreach(var e in Employees.Select(a=>a.Person).Concat(_candidates).Where(e=>e!=null)) EnsureCityEmployee(e);
            PositionStreetServicePeople();
        }
        private void AssignCityHomes()
        {
            if (!City.Blocks.Any(block => City.HasPlace(block.homeId))) return;
            var reservedHomes = City.ExpansionResidents.Select(resident => resident.homeId).ToHashSet();
            var homes = City.Places.Where(place => place.Kind == PlaceKind.Home && !place.InitiallyVacant &&
                !reservedHomes.Contains(place.Id) && place != Home && place.Id != "home" &&
                !place.Id.StartsWith("home_tier", StringComparison.Ordinal)).ToArray();
            if (homes.Length == 0) return;
            var householdSize = homes.ToDictionary(home => home, home => 0);
            var vacant = new HashSet<Place>(homes);
            var remaining = _cityPeople.Values.Where(person =>
                CityLifeCatalog.Person(person.PersonId) != null && person.PersonId != CityLifeCatalog.InvitationTestPersonId).ToList();

            // Keep one established resident at each existing address before filling the other buildings.
            remaining.RemoveAll(person =>
            {
                if (!City.HasPlace(person.HomePlaceId)) return false;
                var original = City.GetPlace(person.HomePlaceId);
                if (!vacant.Remove(original)) return false;
                householdSize[original]++;
                return true;
            });
            foreach (bool sameDistrict in new[] { true, false })
            {
                remaining.RemoveAll(person =>
                {
                    var original = City.GetPlace(person.HomePlaceId);
                    var point = City.ViewPosition(original.NodeId);
                    var next = homes.Where(home => vacant.Contains(home) &&
                            (!sameDistrict || DistrictTierOf(home) == DistrictTierOf(original)))
                        .OrderBy(home => DistanceCity(City.ViewPosition(home.NodeId), point)).FirstOrDefault();
                    if (next == null) return false;
                    vacant.Remove(next);
                    householdSize[next]++;
                    MoveCityHome(person, next);
                    return true;
                });
            }
            foreach (var person in remaining)
            {
                var original = City.GetPlace(person.HomePlaceId);
                var point = City.ViewPosition(original.NodeId);
                var next = homes.OrderBy(home => householdSize[home])
                    .ThenBy(home => DistrictTierOf(home) != DistrictTierOf(original))
                    .ThenBy(home => DistanceCity(City.ViewPosition(home.NodeId), point)).First();
                householdSize[next]++;
                MoveCityHome(person, next);
            }
        }

        private void InitializeExpansionResidents()
        {
            if (City.ExpansionResidents.Count == 0 || !Flags.Add("city:expansion-occupants-v1")) return;

            var occupied = _cityPeople.Values.Select(person => person.HomePlaceId).ToHashSet();
            var reserved = City.ExpansionResidents.Select(resident => resident.homeId).ToHashSet();
            bool Available(Place residence) => residence != Home && residence.Kind == PlaceKind.Home &&
                !HasResidentialLease(residence) && !occupied.Contains(residence.Id) && PropertyAt(residence.Id) == null;

            foreach (var entry in City.ExpansionResidents)
            {
                if (!City.HasPlace(entry.homeId) || _cityPeople.ContainsKey(entry.id)) continue;
                var home = City.GetPlace(entry.homeId);
                var work = City.HasPlace(entry.workId) ? City.GetPlace(entry.workId) : null;
                if (!IsCommercialPremises(work) || IsMyShop(work) || IsPendingShop(work) ||
                    VentureAt(work.Id) != null || PropertyAt(work.Id) != null) work = null;

                if (!Available(home))
                {
                    // A shopkeeper may use another unclaimed expansion home in the same district.
                    // A home already occupied by the player or a resident stays with that occupant.
                    if (work == null) continue;
                    int tier = home.Tier;
                    home = City.Places.FirstOrDefault(place => place.Tier == tier && place.InitiallyVacant &&
                        !reserved.Contains(place.Id) && Available(place));
                    if (home == null) continue;
                }

                var definition = new CityPersonDefinition
                {
                    Id = entry.id, Name = entry.name, HomeId = home.Id, WorkId = work?.Id,
                    Role = work == null ? "街坊居民" : work.Business + "店主",
                    Interest = work == null ? "湖边散步" : work.Business,
                    WorkStart = work?.OpenMinute ?? StreetShopOpenMinute,
                    WorkEnd = work?.CloseMinute ?? StreetShopCloseMinute,
                    Topics = new[] { work == null ? "我就住在这条街，最近常去湖边散步。" : "我在" + work.Name + "做生意，有空来坐坐。" }
                };
                _cityExtraDefinitions[entry.id] = definition;
                var person = new CityPersonState
                {
                    PersonId = entry.id, HomePlaceId = home.Id, Address = home.Name, WorkPlaceId = work?.Id,
                    LocationPlaceId = home.Id, Task = CityPersonTask.Home, TaskLabel = "在家休息"
                };
                if (work != null)
                {
                    _listings.RemoveAll(listing => listing.PlaceId == work.Id);
                    _vacant.Remove(work.Id);
                    if (AuthoredCityWorkAt(person, Clock.Now.TotalMinutes))
                    {
                        person.LocationPlaceId = work.Id;
                        person.Task = CityPersonTask.Working;
                        person.TaskLabel = "当班工作";
                    }
                }
                var point = StandOf(City.GetPlace(person.LocationPlaceId));
                person.X = point.X;
                person.Y = point.Y;
                _cityPeople.Add(person.PersonId, person);
                occupied.Add(home.Id);
            }
        }

        private void MoveCityHome(CityPersonState person, Place residence)
        {
            var definition=CityPerson(person.PersonId);
            bool worksAtHome=definition!=null&&definition.WorkId==definition.HomeId;
            if (person.Route == null && (person.Task == CityPersonTask.Home||worksAtHome)
                && person.LocationPlaceId == person.HomePlaceId)
            {
                var door = StandOf(residence);
                person.LocationPlaceId = residence.Id;
                person.X = door.X;
                person.Y = door.Y;
            }
            person.HomePlaceId = residence.Id;
            if(worksAtHome)person.WorkPlaceId=residence.Id;
            person.Address = residence.Name;
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
            var fixedIdentity=CityLifeCatalog.Person(id);
            if(fixedIdentity!=null && e.Name!=fixedIdentity.Name)e.Name=fixedIdentity.Name;
            else if(fixedIdentity==null)
            {
                e.Name=FormalEmployeeName(e.Name);
                if(_cityExtraDefinitions.TryGetValue(id,out var extra))extra.Name=e.Name;
            }
            if(CityPerson(id)==null) _cityExtraDefinitions[id]=new CityPersonDefinition {Id=id,Name=e.Name,Role="受聘员工",Age=28,
                HomeId="life_home_s_oldhomes",Interest="稳定的工作和业余生活",Goal=e.Description??"攒下生活费，找一份可靠工作。",
                Experience="我做过零售和仓储，普通的工作会按规矩可靠完成。",Recruitable=true,
                Topics=new[]{"基本工作按岗位规则做，有问题会明确告诉你。","下班后也想保留一点自己的时间。",e.Description??"我在攒一笔应急钱。","我也认识社区里的街坊。"},
                Stories=new[]{"最初来这座城时，我也从临时工作开始。",e.Description??"现在希望工作和生活都稳定一点。","以后想学一门更熟练的手艺。"}};
            if(!_cityPeople.ContainsKey(id))
            {
                var d=CityPerson(id);string home=City.HasPlace(d.HomeId)?City.CanonicalPlaceId(d.HomeId):Home.Id;var pos=StandOf(City.GetPlace(home));
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
            // Employees builds assignment records; doing that per resident multiplied
            // all roster work by the city population on every simulated minute.
            var jobs = new Dictionary<string, EmployeeAssignment>();
            foreach (var row in Employees)
            {
                string person = PersonIdOf(row.Person);
                if (!jobs.ContainsKey(person)) jobs.Add(person, row);
            }
            var candidates = new Dictionary<string, Employee>();
            foreach (var candidate in _candidates)
            {
                string person = PersonIdOf(candidate);
                if (!candidates.ContainsKey(person)) candidates.Add(person, candidate);
            }
            foreach(var p in _cityPeople.Values.ToArray())
            {
                var d=CityPerson(p.PersonId); if(d==null)continue;
                jobs.TryGetValue(p.PersonId,out var employment);
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
                if(TickDistrictMeetingPerson(p,now))continue;
                if(TickMcnIntroduction(p,now))continue;
                if(TickBrandExpoPerson(p,now))continue;
                if(!string.IsNullOrEmpty(p.ActivityId))continue;
                if(!string.IsNullOrEmpty(p.VisitId)){p.Task=CityPersonTask.Shopping;continue;}
                if(HoldKolLivePerson(p,now))continue;
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
                else p.WorkPlaceId=d.Recruitable?null:d.WorkId==d.HomeId&&City.HasPlace(p.HomePlaceId)?p.HomePlaceId:City.HasPlace(d.WorkId)?d.WorkId:null;
                candidates.TryGetValue(p.PersonId,out var candidate);
                bool shift=employee!=null?employee.OnDuty(Clock.Now):AuthoredCityWorkAt(p,now);
                if(employee==null&&shift&&p.WorkPlaceId!=p.HomePlaceId&&!IsPlaceOpen(City.GetPlace(p.WorkPlaceId)))shift=false;
                if (!CandidateAvailable(candidate) && TickRaceAttendee(p, now, employee != null && shift)) continue;
                if (!CandidateAvailable(candidate) && TickPetOwnerVisit(p, now, shift)) continue;
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
                    long commute=EstimateCityCommute(p,p.WorkPlaceId,now);
                    if(commute>=0 && delta>0 && delta<=commute+3
                        &&(employee==null?AuthoredCityWorkAt(p,now+delta)
                            :p.WorkPlaceId==p.HomePlaceId||CityVenueFits(City.GetPlace(p.WorkPlaceId),now+delta,1)))
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
            PositionStreetServicePeople();
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
                if(!IsPlaceOpen(City.GetPlace(person.LeisureTargetId)))
                { person.LeisureTargetId=null;person.LeisureNextMove=0; }
            }
            if (person.LeisureTargetId != null && City.HasPlace(person.LeisureTargetId))
            {
                if (person.LocationPlaceId != person.LeisureTargetId) return person.LeisureTargetId;
                if (person.LeisureNextMove == 0) person.LeisureNextMove = now + (working ? 5 : 5 + seed % 11);
                if (now < person.LeisureNextMove) return person.LeisureTargetId;
                person.LeisureStop++; person.LeisureNextMove = 0;
            }
            // Visits have real routes and short pauses rather than everyone standing at one park all afternoon.
            if (!working && person.LeisureStop % 3 == 0 && (person.PersonId == Npcs.Zhang
                || person.PersonId == Npcs.Lin || person.PersonId == Npcs.Landlady))
            {
                var shop = ShopAt(CurrentPlace);
                if (shop != null && shop.Place.Id != person.LocationPlaceId && DistrictAccessBlocker(shop.Place) == null
                    && (person.PersonId != Npcs.Landlady || LandladyShop() == shop))
                {
                    person.LeisureTargetId = shop.Place.Id;
                    return person.LeisureTargetId;
                }
            }
            string[] destinations = CityPerson(person.PersonId)?.LeisurePlaces;
            if(destinations==null||destinations.Length==0)destinations=new[]{ "shop14", "shop07", "life_park", "shop15", "shop01", "metro_a", "shop16", "life_court", "metro_b" };
            for (int i = 0; i < destinations.Length; i++)
            {
                string target = destinations[(seed % destinations.Length + person.LeisureStop + i) % destinations.Length];
                if (!City.HasPlace(target) || target == person.HomePlaceId || target == person.LocationPlaceId || target == person.WorkPlaceId) continue;
                var place = City.GetPlace(target);
                if(!IsPlaceOpen(place))continue;
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
            => PlanCityRouteToNode(p, target, now, null, latestArrival, transportChoice);

        private CityRoute PlanCityRouteToNode(CityPersonState p, string target, long now, string streetNode,
            long latestArrival = long.MaxValue, VehicleKind? transportChoice = null)
        {
            if(!City.HasPlace(target))return null;
            if(streetNode==null && StreetServicePlace(target))streetNode=CommunityStreetNode(City.GetPlace(target));
            var place=City.GetPlace(target);string from=City.NearestNode(p.X,p.Y);
            string destination=streetNode??place.NodeId;
            string key=from+"|"+destination;
            if(!_cityPathCache.TryGetValue(key,out var path)){path=Pathfinder.FindPath(City.Map,from,destination);_cityPathCache[key]=path;}
            if(!path.Found)return null;
            var points=new List<MapPoint>{new MapPoint(p.X,p.Y)};
            foreach(string node in path.NodeIds)
            {
                points.Add(City.ViewPosition(node));
            }
            var end=streetNode==null?StandOf(place):City.ViewPosition(streetNode);points.Add(end);
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
            if(e!=null&&BrandExpoPersonReserved(PersonIdOf(e)))return false;
            if(e==null||shop==null||!e.OnDuty(Clock.Now)||EmployeeTrip(e.Id)!=null)return false;
            string id=PersonIdOf(e);var p=CityPersonState(id);
            return p!=null && p.Route==null && p.LocationPlaceId==shop.Place.Id && string.IsNullOrEmpty(p.ActivityId);
        }
        public bool CityPlayerCanWork(OwnedShop shop) => shop!=null&&CurrentPlace==shop.Place&&!CityActivityExclusive&&!IsWorking&&!_sleeping;
        public string CityServiceReason(OwnedShop shop)
        {
            if(shop==null)return "未开店";
            if(IsDedicatedVenue(shop.Place))return VenueOpen(shop.Place)?"场馆正在接客":"场馆暂停接客或等待值守";
            if(IsSpecialBusiness(shop))return SpecialOpen(shop,Clock.Now)?"专用经营正在接客":"专用经营暂停或缺少设施/值守";
            if(CityPlayerCanWork(shop)||ShopStaff(shop,EmployeeRole.Cashier).Any(e=>CityEmployeeAvailable(e,shop)))return "可销售";
            if(ShopStaff(shop,EmployeeRole.Assembler).Any(e=>CityEmployeeAvailable(e,shop)))return shop.EmployeeBuilding?"装机值守，收银需等待":"可销售（装机员兼收银）";
            return "暂停服务：无人值守";
        }
        public string CityTalk(string id,string topic,bool remote=false)
        {
            var p=CityPersonState(id);var d=CityPerson(id);
            if(p==null||d==null)return null;
            if(remote){string why=RemoteConversationBlocker(id);if(why!=null)return why;}
            else if(!CitySamePlace(id))return "对方不在这里。通讯录可查看已知资料，聊天软件可远程交谈。";
            if (topic == "friends" && Relation(id) < AskAboutOthersMinRelation) return "咱们再熟悉一些，再慢慢聊我的朋友吧。";
            GreetPerson(id,remote); int index=topic=="work"?0:topic=="interest"?1:topic=="life"?2:3;
            string key=id+":"+topic;bool fresh=!_cityTopicDay.TryGetValue(key,out int day)||day!=Clock.Today.TotalDays;
            if(fresh){_cityTopicDay[key]=Clock.Today.TotalDays;AddRelation(id,1);p.KnownTopics=p.KnownTopics.Union(new[]{topic}).ToArray();}
            if (topic == "friends")
            {
                var circle = PersonCircle(id).ToArray();
                if (circle.Length > 0) return AskAboutPerson(id, circle[Clock.Today.TotalDays % circle.Length].PersonId,remote);
            }
            string text=d.Topics!=null&&d.Topics.Length>index?d.Topics[index]:d.Goal;
            if(topic=="work")text=(p.WorkPlaceId!=null&&City.HasPlace(p.WorkPlaceId)?"我现在的工作在"+City.GetPlace(p.WorkPlaceId).Name+"。":"我现在没有固定岗位。")+"\n"+text;
            if (topic == "interest") text = "我平时喜欢" + ConversationTraits(id)[0].Name + "。有空的时候，也想给自己放个假。";
            RememberSocial(id,(remote?"通过聊天软件，":"")+(fresh?"聊了新话题：":"再次聊起：")+text);
            OnCityStoryTalk(id,topic);
            return text;
        }
        public string CityHearStory(string id,bool remote=false)
        {
            var p=CityPersonState(id);var d=CityPerson(id);
            if(remote){string why=RemoteConversationBlocker(id);if(why!=null)return why;}
            if(p==null||d==null||!remote&&!CitySamePlace(id))return "需要当面听对方讲。";
            // The authored city biography and legacy staff anecdotes are distinct texts.
            // Do not mark an unheard legacy anecdote as learned when a biography is told.
            if(p.LastStoryDay==Clock.Today.TotalDays||_staffStoryDay.TryGetValue(id,out int toldDay)&&toldDay==Clock.Today.TotalDays)return "今天已经聊了不少，改天再听下一段。";
            if(d.Stories==null||p.StoryChapter>=d.Stories.Length)return d.Goal;
            if(Relation(id)<5+p.StoryChapter*5)return "再熟悉一点，对方愿意慢慢讲自己的经历。";
            string line=d.Stories[p.StoryChapter++];p.LastStoryDay=Clock.Today.TotalDays;
            AddRelation(id,2);RememberSocial(id,line);return line;
        }
    }
}
