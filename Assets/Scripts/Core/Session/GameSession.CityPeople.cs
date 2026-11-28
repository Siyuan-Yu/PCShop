using System;
using PCShopSell.Core.Common;
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
        private readonly Queue<(double x,double y,string target,bool inside)> _cityCommuteOrder = new();
        private int _cityCommuteNodeCount = -1;
        private long EstimateCityCommute(CityPersonState person,string target,long now)
        {
            if(_cityCommuteNodeCount!=City.Map.NodeCount)
            {
                _cityCommuteMinutes.Clear();_cityCommuteOrder.Clear();_cityPathCache.Clear();
                _cityCommuteNodeCount=City.Map.NodeCount;
            }
            var key=(person.X,person.Y,target,person.LocationPlaceId!=null);
            if(_cityCommuteMinutes.TryGetValue(key,out long minutes))return minutes;
            var route=PlanCityRoute(person,target,now);
            minutes=route==null?-1:route.ArriveMinute-now;
            if(_cityCommuteMinutes.Count>=4096)_cityCommuteMinutes.Remove(_cityCommuteOrder.Dequeue());
            _cityCommuteMinutes[key]=minutes;
            _cityCommuteOrder.Enqueue(key);
            return minutes;
        }
        public IReadOnlyCollection<CityPersonState> CityPeople => _cityPeople.Values;
        public CityPersonDefinition CityPerson(string id) => CityLifeCatalog.Person(id) ?? (_cityExtraDefinitions.TryGetValue(id??"",out var p)?p:null);
        public CityPersonState CityPersonState(string id) => _cityPeople.TryGetValue(id??"",out var p)?p:null;
        public int CityTrust(string id) => _cityTrust.TryGetValue(id??"",out int trust)?trust:0;

        public (int Intelligence, int Charm, int Dexterity, int Stamina) CityPersonAttributes(string id)
        {
            var employee = CityEmploymentOf(id)?.Person
                ?? _candidates.Find(candidate => candidate.PersonId == id);
            return employee != null
                ? (employee.Intelligence, EmployeeCharm(employee), employee.Dexterity, employee.Stamina)
                : BaseCityPersonAttributes(id);
        }

        private static (int Intelligence, int Charm, int Dexterity, int Stamina) BaseCityPersonAttributes(string id)
        {
            // Named residents need repeatable attributes without advancing the simulation RNG.
            int Score(string attribute, int min, uint range)
            {
                uint hash = 2166136261;
                foreach (char c in id + ":" + attribute)
                    hash = unchecked((hash ^ c) * 16777619);
                return min + (int)(hash % range);
            }
            var talent = KolMarket.Get(id);
            return (talent?.Intelligence ?? Score("intelligence", 20, 76),
                talent?.Charm ?? Score("charm", 20, 71),
                Score("dexterity", 15, 81), Score("stamina", 15, 81));
        }

        public string CityPersonRole(string id)
        {
            if(string.IsNullOrEmpty(id))return "居民";
            var job=CityEmploymentOf(id);
            return job!=null?(job.Role==EmployeeRole.Cashier&&IsDedicatedVenue(job.Shop?.Place)?"场馆值守员":RoleName(job.Role))+"（受聘）":CityPerson(id)?.Role??"居民";
        }
        private EmployeeAssignment CityEmploymentOf(string id) =>
            Employees.FirstOrDefault(row => row.Person.PersonId == id ||
                (_cityEmployeeAliases.TryGetValue(row.Person.Id, out var known) && known == id));
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
            if(p.VisitId=="community-child")return false;
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
        private readonly Dictionary<string, List<CityPersonState>> _streetServiceGroups = new Dictionary<string, List<CityPersonState>>();
        private static readonly Comparison<CityPersonState> StreetPersonOrder =
            (a, b) => StringComparer.Ordinal.Compare(a.PersonId, b.PersonId);
        private void PositionStreetServicePeople()
        {
            var groups = _streetServiceGroups;
            foreach (var members in groups.Values) members.Clear();
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
                if (group.Value.Count == 0) continue;
                var point = City.ViewPosition(group.Key);
                var people = group.Value;
                people.Sort(StreetPersonOrder);
                for (int i = 0; i < people.Count; i++)
                {
                    // Keep one person at the navigation anchor and give everyone else
                    // a distinct spot within the meeting radius, clear of nearby doors.
                    double angle = 2 * Math.PI * (i - 1) / Math.Max(1, people.Count - 1);
                    people[i].X = point.X + (i == 0 ? 0 : (float)(2.8 * Math.Cos(angle)));
                    people[i].Y = point.Y + (i == 0 ? 0 : (float)(2.8 * Math.Sin(angle)));
                }
            }
        }
        public static bool CityWithinHours(int minute,int open,int close) => open==close || (close>open?minute>=open&&minute<close:minute>=open||minute<close);
        public const int HomeVisitMinRelation = 30;

        private (Place Residence, string PersonId) _homeDoorConversation;

        public IEnumerable<CityPersonState> ResidentsAtHome(Place home) =>
            PeopleInPlace(home.Id).Where(person => person.HomePlaceId == home.Id && CityPersonInside(person));

        public bool CanTalkAtHomeDoor(Place residence, string personId)
        {
            if (residence?.Kind != PlaceKind.Home || CurrentPlace != null || Trip != null || IsGameOver || IsWorking || _sleeping
                || DistrictAccessBlocker(residence) != null || Dist(PlayerPosition, DoorOf(residence)) > DoorEnterRadius(residence))
                return false;
            var person = CityPersonState(personId);
            return person?.HomePlaceId == residence.Id && person.LocationPlaceId == residence.Id && CityPersonInside(person);
        }

        public bool TryStartHomeDoorConversation(Place residence, string personId)
        {
            if (_homeDoorConversation.Residence != null || CityPerson(personId) == null || !CanTalkAtHomeDoor(residence, personId))
                return false;
            _homeDoorConversation = (residence, personId);
            return true;
        }

        public void EndHomeDoorConversation(Place residence, string personId)
        {
            if (_homeDoorConversation == (residence, personId)) _homeDoorConversation = default;
        }

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
            // Only the resident who answered the door is available for this conversation.
            if (_homeDoorConversation.PersonId == personId && CanTalkAtHomeDoor(_homeDoorConversation.Residence, personId))
                return true;
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
                    WorkPlaceId=CityNpcWorkplace(d, home),LocationPlaceId=home,Task=CityPersonTask.Home,X=pos.X,Y=pos.Y};
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
            remaining.RemoveAll(person =>
            {
                var original = City.GetPlace(person.HomePlaceId);
                var point = City.ViewPosition(original.NodeId);
                var next = homes.Where(home => vacant.Contains(home) && DistrictTierOf(home) == DistrictTierOf(original))
                    .OrderBy(home => DistanceCity(City.ViewPosition(home.NodeId), point)).FirstOrDefault();
                if (next == null) return false;
                vacant.Remove(next);
                householdSize[next]++;
                MoveCityHome(person, next);
                return true;
            });
            foreach (var person in remaining)
            {
                var original = City.GetPlace(person.HomePlaceId);
                var point = City.ViewPosition(original.NodeId);
                var next = homes.Where(home => DistrictTierOf(home) == DistrictTierOf(original))
                    .OrderBy(home => householdSize[home])
                    .ThenBy(home => DistanceCity(City.ViewPosition(home.NodeId), point)).FirstOrDefault();
                if (next == null) continue;
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
                if (!CityNpcWorkAllowed(work, home.Id) || !IsCommercialPremises(work) || IsMyShop(work) || IsPendingShop(work) ||
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

        private bool CityNpcWorkAllowed(Place work, string homeId) => work != null && (!City.IsLakeCity
            || City.HasPlace(homeId) && DistrictTierOf(work) <= DistrictTierOf(City.GetPlace(homeId)));

        private readonly Dictionary<(string work, string authoredHome, string home), string> _npcWorkplaceCache = new();
        private int _npcWorkplacePlaceCount = -1;
        private string CityNpcWorkplace(CityPersonDefinition definition, string homeId)
        {
            if (definition == null || definition.Recruitable || definition.WorkId == null) return null;
            // 地点及其街区归属固定，住处或定义变动会形成新键；新增地点使旧查询失效。
            // 不缓存营业、空置、在岗状态，这些仍在每个模拟分钟检查。
            if (_npcWorkplacePlaceCount != City.Places.Count)
            {
                _npcWorkplaceCache.Clear();
                _npcWorkplacePlaceCount = City.Places.Count;
            }
            var key = (definition.WorkId, definition.HomeId, homeId);
            if (_npcWorkplaceCache.TryGetValue(key, out var cached)) return cached;
            string workId = definition.WorkId == definition.HomeId ? homeId : definition.WorkId;
            if (!City.HasPlace(workId)) return null;
            var work = City.GetPlace(workId);
            return _npcWorkplaceCache[key] = CityNpcWorkAllowed(work, homeId) ? work.Id : null;
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
                HomeId="life_home_s_oldhomes",Goal=e.Description??"攒下生活费，找一份可靠工作。",
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
            if (def != null)
            {
                e.PersonId = def.Id;
                e.Name = def.Name;
                e.Description = def.Goal;
                (e.Intelligence, e.Charm, e.Dexterity, e.Stamina) = BaseCityPersonAttributes(def.Id);
            }
            EnsureCityEmployee(e);
        }
        private readonly Dictionary<string, EmployeeAssignment> _cityTickJobs = new Dictionary<string, EmployeeAssignment>();
        private readonly Dictionary<string, Employee> _cityTickCandidates = new Dictionary<string, Employee>();
        private readonly List<CityPersonState> _cityTickPeople = new List<CityPersonState>();
        private void TickCityPeople(long now)
        {
            InitializeCityPeople();
            // Employees builds assignment records; doing that per resident multiplied
            // all roster work by the city population on every simulated minute.
            var jobs = _cityTickJobs;
            jobs.Clear();
            foreach (var row in Employees)
            {
                string person = PersonIdOf(row.Person);
                if (!jobs.ContainsKey(person)) jobs.Add(person, row);
            }
            var candidates = _cityTickCandidates;
            candidates.Clear();
            foreach (var candidate in _candidates)
            {
                string person = PersonIdOf(candidate);
                if (!candidates.ContainsKey(person)) candidates.Add(person, candidate);
            }
            // Preserve the minute's snapshot when arrivals add people during processing.
            _cityTickPeople.Clear();
            _cityTickPeople.AddRange(_cityPeople.Values);
            foreach(var p in _cityTickPeople)
            {
                var d=CityPerson(p.PersonId); if(d==null)continue;
                jobs.TryGetValue(p.PersonId,out var employment);
                if (employment?.Shop == null) p.WorkPlaceId = CityNpcWorkplace(d, p.HomePlaceId);
                var purchase=employment==null?null:EmployeeTrip(employment.Person.Id);
                if(purchase!=null)
                {
                    p.WorkPlaceId=employment.Shop?.Place.Id;
                    ApplyCityPurchasePosition(purchase,p,now);
                    continue;
                }
                if(p.Route!=null)
                {
                    var pos=p.Route.Position(now);p.X=pos.X;p.Y=pos.Y;
                    if(now<p.Route.ArriveMinute)continue;
                    p.LocationPlaceId=p.Route.TargetPlaceId;p.Route=null;
                    NotifyCityCustomerEntry(p);
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
                candidates.TryGetValue(p.PersonId,out var candidate);
                bool shift=employee!=null?employee.OnDuty(Clock.Now):AuthoredCityWorkAt(p,now);
                if (employee == null && shift && p.WorkPlaceId != p.HomePlaceId)
                {
                    var workplace = City.GetPlace(p.WorkPlaceId);
                    // At staffed venues the employee's shift opens the doors after arrival.
                    if (workplace.Business != BankBusiness && !UsesNpcVenueRoster(workplace) && !IsPlaceOpen(workplace)) shift = false;
                }
                if (!CandidateAvailable(candidate) && TickRaceAttendee(p, now, employee != null && shift)) continue;
                if (!CandidateAvailable(candidate) && TickPetOwnerVisit(p, now, shift)) continue;
                if (!shift && candidate == null && TickWalletOwner(p, now)) continue;
                string target=shift?p.WorkPlaceId:p.HomePlaceId;
                var desiredTask=shift?CityPersonTask.Working:CityPersonTask.Home;
                if(candidate!=null && CandidateAvailable(candidate)) {target=(RecruitmentInterviewPlace ?? PlayerShopPlace).Id;desiredTask=CityPersonTask.Waiting;p.TaskLabel="等待面试";}
                if (candidate == null)
                {
                    string outing;
                    using (RuntimePerformanceProbe.Measure("Npc.Outing")) outing = CityOutingDestination(p, employee != null, shift, now);
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
                    long commute;
                    using (RuntimePerformanceProbe.Measure("Npc.Commute")) commute=EstimateCityCommute(p,p.WorkPlaceId,now);
                    if(commute>=0 && delta>0 && delta<=commute+3
                        &&(employee==null?AuthoredCityWorkAt(p,now+delta)
                            :employee.OnDuty(new GameTimestamp(now+delta))&&(p.WorkPlaceId==p.HomePlaceId||CityVenueFits(City.GetPlace(p.WorkPlaceId),now+delta,1))))
                    {target=p.WorkPlaceId;desiredTask=CityPersonTask.Waiting;p.LeisureTargetId=null;p.LeisureNextMove=0;}
                }
                if(target!=null && City.HasPlace(target) && p.LocationPlaceId!=target)
                {
                    CityRoute route;
                    using (RuntimePerformanceProbe.Measure("Npc.Route")) route=PlanCityRoute(p,target,now);
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

        private static readonly string[] FallbackLeisurePlaces =
            { "life_hike", "shop14", "shop07", "life_park", "shop15", "shop01", "metro_a", "shop16", "life_court", "metro_b" };

        private string CityOutingDestination(CityPersonState person, bool employed, bool working, long now)
        {
            int minute = Clock.MinuteOfDay;
            var resident = Residents.Get(person.PersonId);
            int outingEnd = -1;
            if (!employed && resident != null)
                for (int i = 0; i + 1 < resident.Outings.Length; i += 2)
                    if (minute >= resident.Outings[i] && minute < resident.Outings[i + 1])
                        outingEnd = Math.Max(outingEnd, resident.Outings[i + 1]);
            // Only authored lunch/rest breaks may take a shopkeeper out during work.
            // Hired employees keep their actual shifts, appointments and purchase trips.
            int seed = ConversationSeed(person.PersonId);
            if (!working)
            {
                if (person.WorkPlaceId == null && resident == null)
                {
                    int end = 20 * 60 + seed % 60;
                    if (minute >= 8 * 60 + seed % 90 && minute < end) outingEnd = end;
                }
                else if (outingEnd < 0 && (resident == null || resident.Outings.Length == 0))
                {
                    int start = person.PersonId == Npcs.Landlady ? 16 * 60 + 30 : 17 * 60 + seed % 120;
                    int end = 21 * 60 + seed % 60;
                    if (minute >= start && minute < end) outingEnd = end;
                }
            }

            int minimumStay = working ? 15 : 60;
            int preferredStay = working ? minimumStay : minimumStay + 15 * (seed % 5);
            if (person.LeisureTargetId != null && City.HasPlace(person.LeisureTargetId))
            {
                var place = City.GetPlace(person.LeisureTargetId);
                if (person.LocationPlaceId == place.Id && IsPlaceOpen(place) && (!working || !employed && outingEnd >= 0))
                {
                    if (person.LeisureNextMove == 0) person.LeisureNextMove = now + preferredStay;
                    if (now < person.LeisureNextMove) return place.Id;
                    person.LeisureStop++;
                }
                person.LeisureTargetId = null;
                person.LeisureNextMove = 0;
            }
            if (outingEnd < 0 || employed && working)
            {
                person.LeisureTargetId = null;
                person.LeisureNextMove = 0;
                return null;
            }
            if (now < person.LeisureNextMove) return null;
            long windowEnd = now - minute + outingEnd;
            // A stop must have room for both the trip and the time spent at the destination.
            if (!working && person.LeisureStop % 3 == 0 && (person.PersonId == Npcs.Zhang
                || person.PersonId == Npcs.Lin || person.PersonId == Npcs.Landlady))
            {
                var shop = ShopAt(CurrentPlace);
                if (shop != null && shop.Place.Id != person.LocationPlaceId
                    && (person.PersonId != Npcs.Landlady || LandladyShop() == shop) && TryPlanVisit(shop.Place))
                    return person.LeisureTargetId;
            }
            var preferred = CityPerson(person.PersonId)?.LeisurePlaces ?? Array.Empty<string>();
            foreach (var destinations in new[] { preferred, FallbackLeisurePlaces })
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
                if (TryPlanVisit(place)) return target;
            }
            person.LeisureNextMove = now + 15 + seed % 10;
            return null;

            bool TryPlanVisit(Place place)
            {
                if (DistrictAccessBlocker(place) != null || !IsPlaceOpen(place)) return false;
                if (City.HasUniformRoadScale)
                {
                    // 直线乘统一路程比例，是任何真实路线的下界。连最快的普通交通都来不及，
                    // 就不必为注定无法成行的候选目的地搜索整张路网。
                    string streetNode = StreetServicePlace(place.Id) ? CommunityStreetNode(place) : null;
                    var end = streetNode == null ? StandOf(place) : City.ViewPosition(streetNode);
                    double minimumMeters = DistanceCity(new MapPoint(person.X, person.Y), end) * City.TravelDistanceScale;
                    int minimumTravel = CityTravelTiming.NpcMinutes(minimumMeters, Vehicles.Taxi);
                    if (now + minimumTravel + minimumStay > windowEnd) return false;
                }
                long travel = EstimateCityCommute(person, place.Id, now);
                if (travel < 0) return false;
                long arrival = now + travel;
                if (arrival + minimumStay > windowEnd || !CityVenueFits(place, arrival, minimumStay)) return false;
                if (working && City.HasPlace(person.WorkPlaceId))
                {
                    string stopNode = StreetServicePlace(place.Id) ? CommunityStreetNode(place) : null;
                    var stop = stopNode == null ? StandOf(place) : City.ViewPosition(stopNode);
                    var departure = new CityPersonState { PersonId = person.PersonId, X = stop.X, Y = stop.Y, LocationPlaceId = place.Id };
                    long returnTrip = EstimateCityCommute(departure, person.WorkPlaceId, arrival + minimumStay);
                    if (returnTrip < 0 || arrival + minimumStay + returnTrip > windowEnd) return false;
                }
                int stay = preferredStay;
                while (stay > minimumStay && (arrival + stay > windowEnd || !CityVenueFits(place, arrival, stay)))
                    stay -= 15;
                person.LeisureTargetId = place.Id;
                person.LeisureNextMove = arrival + stay;
                return true;
            }
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
        public bool CityPlayerCanWork(OwnedShop shop) => shop!=null&&PlayerManagesShop(shop)&&CurrentPlace==shop.Place&&!CityActivityExclusive&&!IsWorking&&!_sleeping;
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
            else if(!CitySamePlace(id))return "对方不在这里。可在通讯录查看资料或打电话。";
            if (topic == "friends" && Relation(id) < AskAboutOthersMinRelation) return "咱们再熟悉一些，再慢慢聊我的朋友吧。";
            GreetPerson(id,remote); int index=topic=="work"?0:topic=="interest"?1:topic=="life"?2:3;
            string key=id+":"+topic;bool fresh=!_cityTopicDay.TryGetValue(key,out int day)||day!=Clock.Today.TotalDays;
            if(fresh)
            {
                _cityTopicDay[key]=Clock.Today.TotalDays;
                AddSocialRelation(id, 1);
                p.KnownTopics=p.KnownTopics.Union(new[]{topic}).ToArray();
            }
            if (topic == "friends")
            {
                var circle = PersonCircle(id).ToArray();
                if (circle.Length > 0) return AskAboutPerson(id, circle[Clock.Today.TotalDays % circle.Length].PersonId,remote);
            }
            string text=d.Topics!=null&&d.Topics.Length>index?d.Topics[index]:d.Goal;
            if(topic=="work")text=(p.WorkPlaceId!=null&&City.HasPlace(p.WorkPlaceId)?"我现在的工作在"+City.GetPlace(p.WorkPlaceId).Name+"。":"我现在没有固定岗位。")+"\n"+text;
            if (topic == "interest" && ConversationTraits(id)[0].Name is string interest)
                text = PersonalInterestLine(id) ?? $"我平时喜欢{interest}。有空的时候，也想给自己放个假。";
            RememberSocial(id, (remote ? PhoneConversationPrefix : "") + (fresh ? NewTopicPrefix : RepeatedTopicPrefix) + text);
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
            if (!GreetPerson(id, remote)) return "现在无法和对方交谈。";
            string line=d.Stories[p.StoryChapter++];p.LastStoryDay=Clock.Today.TotalDays;
            AddSocialRelation(id, 2);
            RememberSocial(id,line);return line;
        }
    }
}
