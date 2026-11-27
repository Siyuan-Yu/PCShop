using System;
using System.Linq;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class SaveData
    {
        public int cityLayoutVersion;
        public int cityLifeVersion;
        public int energyPayrollVersion;
        public long exhaustedAt = -1;
        public int exhaustionReminder;
        public EmployeePayrollDay[] payrollDays;
        public CityPendingDisplay[] pendingDisplays;
        public bool cityLifeTest, cityPlayerOffNode, citySleepRequested;
        public double cityPlayerX, cityPlayerY;
        public CityPersonState[] cityPeople;
        public CityPersonDefinition[] cityExtraPeople;
        public long cityNextVisit;
        public long cityLastAutomaticMealMinute=-1;
        public CityActivity[] cityActivities;
        public CityStoryProgress[] cityStories;
        public bool cityRomanceStateRecorded;
        public RelationshipStoryProgress[] relationshipStories;
        public CityTravelIntent cityTravelIntent;
        public CitySavedTrip cityTrip;
        public long cityNextActivity,cityNextIntent;
        public string[] cityEmployeeIds,cityEmployeePersonIds,cityTopicKeys,cityTrustIds,cityFavorites,cityRecent;
        public int[] cityTopicDays,cityTrustValues;
        public string cityDefaultDestination,cityAgencyShortcut,cityMarketShortcut,cityShopShortcut,cityHomeShortcut;
        public bool cityMarketShortcutExplicit;
    }
    [Serializable] public sealed class CitySavedTrip
    {
        public double[] xs,ys,viewLengths,gameLengths;
        public string[] nodeAt;
        public string target,firstRoadOtherEnd;
        public int segment,vehicle;
        public double progress,viewDone;
    }
    public sealed partial class GameSession
    {
        public bool IsCityLifeTest { get; set; }
        public bool CitySleepRequested { get; set; }
        private void SaveCityLife(SaveData d)
        {
            d.cityLayoutVersion=City.LayoutVersion;
            d.energyPayrollVersion=1;d.exhaustedAt=_energyExhaustedAt;d.exhaustionReminder=_energyReminder;
            d.payrollDays=_employeePayroll.Select(p=>p.Copy()).ToArray();
            d.pendingDisplays=_shops.Where(s=>s.PendingDisplayTier>=0).Select(s=>new CityPendingDisplay{ShopId=s.Place.Id,Tier=s.PendingDisplayTier,Minutes=s.PendingDisplayMinutes}).ToArray();
            d.cityLastAutomaticMealMinute=_lastAutomaticMealMinute;
            d.cityLifeVersion=5;d.cityLifeTest=IsCityLifeTest;d.cityPeople=_cityPeople.Values.Select(p=>p.Copy()).ToArray();
            d.cityExtraPeople=_cityExtraDefinitions.Values.ToArray();d.cityNextVisit=_nextCityVisit;
            d.cityActivities=_cityActivities.Select(a=>a.Copy()).ToArray();d.cityStories=_cityStories.Select(a=>a.Copy()).ToArray();
            d.cityRomanceStateRecorded = true;
            d.cityTravelIntent=CityIntent==null?null:new CityTravelIntent {Id=CityIntent.Id,TargetPlaceId=CityIntent.TargetPlaceId,ActivityId=CityIntent.ActivityId,
                EnterOnArrival=CityIntent.EnterOnArrival,Cancelled=CityIntent.Cancelled,NotifiedClosed=CityIntent.NotifiedClosed};
            d.cityNextActivity=_nextCityActivity;d.cityNextIntent=_nextCityIntent;d.cityTrip=Trip?.SaveCityTrip();
            d.cityPlayerOffNode=OffNode;d.cityPlayerX=PlayerPosition.X;d.cityPlayerY=PlayerPosition.Y;d.citySleepRequested=CitySleepRequested;
            d.cityEmployeeIds=_cityEmployeeAliases.Keys.ToArray();d.cityEmployeePersonIds=_cityEmployeeAliases.Values.ToArray();
            d.cityTopicKeys=_cityTopicDay.Keys.ToArray();d.cityTopicDays=_cityTopicDay.Values.ToArray();d.cityTrustIds=_cityTrust.Keys.ToArray();d.cityTrustValues=_cityTrust.Values.ToArray();
            d.cityFavorites=_cityFavorites.ToArray();d.cityRecent=_cityRecent.ToArray();d.cityDefaultDestination=CityDefaultDestination;
            d.cityAgencyShortcut=CityAgencyShortcut;d.cityMarketShortcut=_cityMarketShortcut;d.cityShopShortcut=CityShopShortcut;
            d.cityMarketShortcutExplicit=_cityMarketShortcut!=null;
            d.cityHomeShortcut = CityHomeShortcut;
            SaveCityStories(d);
            SaveRelationshipStories(d);
            SaveCityVisits(d);
            SaveVenueSponsorships(d);
        }
        private void LoadCityLife(SaveData d)
        {
            _lastAutomaticMealMinute=d.cityLifeVersion>0?d.cityLastAutomaticMealMinute:-1;
            IsCityLifeTest=d.cityLifeTest;CitySleepRequested=d.citySleepRequested;
            _nextCityVisit=Math.Max(1,d.cityNextVisit);_cityExtraDefinitions.Clear();
            foreach(var definition in d.cityExtraPeople??Array.Empty<CityPersonDefinition>())if(definition!=null&&!string.IsNullOrEmpty(definition.Id))_cityExtraDefinitions[definition.Id]=definition;
            _cityEmployeeAliases.Clear();for(int i=0;i<Math.Min(d.cityEmployeeIds?.Length??0,d.cityEmployeePersonIds?.Length??0);i++)_cityEmployeeAliases[d.cityEmployeeIds[i]]=d.cityEmployeePersonIds[i];
            _cityPeople.Clear();foreach(var p in d.cityPeople??Array.Empty<CityPersonState>())if(p!=null&&!string.IsNullOrEmpty(p.PersonId)&&!_cityPeople.ContainsKey(p.PersonId))
            {
                var copy=p.Copy();
                // JsonUtility 将可选字符串的 null 写回为空串；空串并不代表实际来访、赴约或地点。
                copy.VisitId=string.IsNullOrEmpty(copy.VisitId)?null:copy.VisitId;
                copy.ActivityId=string.IsNullOrEmpty(copy.ActivityId)?null:copy.ActivityId;
                copy.LocationPlaceId=string.IsNullOrEmpty(copy.LocationPlaceId)?null:copy.LocationPlaceId;
                copy.WorkPlaceId=string.IsNullOrEmpty(copy.WorkPlaceId)?null:copy.WorkPlaceId;
                copy.LeisureTargetId=string.IsNullOrEmpty(copy.LeisureTargetId)?null:copy.LeisureTargetId;
                if(!City.HasPlace(copy.HomePlaceId))copy.HomePlaceId=Home.Id;
                if(copy.LocationPlaceId!=null&&!City.HasPlace(copy.LocationPlaceId))copy.LocationPlaceId=copy.HomePlaceId;
                // JsonUtility can materialize a nested null route as an empty object. That is
                // "no automatic route", not a missing destination requiring a move home.
                // Even a deleted route target must not teleport the saved physical person.
                if(copy.Route!=null&&(!City.HasPlace(copy.Route.TargetPlaceId)||copy.Route.X==null||copy.Route.Y==null||copy.Route.X.Length<2||copy.Route.Y.Length!=copy.Route.X.Length))
                    copy.Route=null;
                if (copy.PersonId == CityLifeCatalog.BankTellerId && (copy.WorkPlaceId == null || d.cityLifeVersion < 4)
                    && BankPlace is Place bank)
                {
                    // Align older saves with the teller's new shift once; keep subsequent saved routes.
                    copy.WorkPlaceId = bank.Id;
                    if (string.IsNullOrEmpty(copy.ActivityId) && string.IsNullOrEmpty(copy.VisitId)
                        && AuthoredCityWorkAt(copy, Clock.Now.TotalMinutes))
                    {
                        var point = StandOf(bank);
                        copy.LocationPlaceId = bank.Id;
                        copy.Route = null;
                        copy.Task = CityPersonTask.Working;
                        copy.TaskLabel = "当班工作";
                        copy.X = point.X;
                        copy.Y = point.Y;
                    }
                }
                _cityPeople.Add(copy.PersonId,copy);
            }
            InitializeCityPeople();
            if (d.cityLifeVersion < 3) AssignCityHomes();
            if(d.cityLifeVersion==0)
            {
                // Old saves had no authoritative person positions. Preserve their actual on-duty
                // employment once; subsequent saves retain the new route/location without replay.
                foreach(var row in Employees.Where(e=>e.Shop!=null))
                {
                    var person=CityPersonState(PersonIdOf(row.Person));if(person==null)continue;
                    person.WorkPlaceId=row.Shop.Place.Id;
                    var purchase=EmployeeTrip(row.Person.Id);
                    if(purchase!=null)ApplyCityPurchasePosition(purchase,person,Clock.Now.TotalMinutes);
                    else if(row.Person.OnDuty(Clock.Now))
                    {
                        var point=StandOf(row.Shop.Place);person.X=point.X;person.Y=point.Y;
                        person.LocationPlaceId=row.Shop.Place.Id;person.Route=null;person.Task=CityPersonTask.Working;
                    }
                }
            }
            _cityActivities.Clear();foreach(var a in d.cityActivities??Array.Empty<CityActivity>())if(a!=null&&!_cityActivities.Any(x=>x.Id==a.Id))_cityActivities.Add(a.Copy());
            foreach (var activity in _cityActivities.Where(a => a.Active && a.DefinitionId == SignHelpActivityId))
            {
                activity.HostId = Npcs.Jie;
                if (activity.State != CityActivityPhase.Running) activity.ServiceConfirmed = false;
            }
            foreach (var activity in _cityActivities.Where(a => a.Active && a.State != CityActivityPhase.Running && IsOnsiteActivity(a)))
                activity.ServiceConfirmed = false;
            // Older main-story bookings preselected the first dish even though the player never ordered it.
            foreach (var activity in _cityActivities.Where(a => a.Active && !a.EntryPaid
                && IsCommunityLeadDinner(a) && string.IsNullOrEmpty(a.ServiceDefinitionId)))
                activity.ServiceConfirmed = false;
            _nextCityActivity=Math.Max(1,d.cityNextActivity);_nextCityIntent=Math.Max(1,d.cityNextIntent);
            _cityTopicDay.Clear();for(int i=0;i<Math.Min(d.cityTopicKeys?.Length??0,d.cityTopicDays?.Length??0);i++)_cityTopicDay[d.cityTopicKeys[i]]=d.cityTopicDays[i];
            _cityTrust.Clear();for(int i=0;i<Math.Min(d.cityTrustIds?.Length??0,d.cityTrustValues?.Length??0);i++)_cityTrust[d.cityTrustIds[i]]=Math.Max(0,d.cityTrustValues[i]);
            _cityFavorites.Clear();_cityFavorites.AddRange((d.cityFavorites??Array.Empty<string>()).Where(City.HasPlace).Distinct().Take(5));
            _cityRecent.Clear();_cityRecent.AddRange((d.cityRecent??Array.Empty<string>()).Where(City.HasPlace).Distinct().Take(6));
            CityDefaultDestination=City.HasPlace(d.cityDefaultDestination)?d.cityDefaultDestination:null;
            CityAgencyShortcut=City.HasPlace(d.cityAgencyShortcut)?d.cityAgencyShortcut:"agency";
            _cityMarketShortcut=City.HasPlace(d.cityMarketShortcut)&&(d.cityMarketShortcutExplicit||d.cityMarketShortcut!="stall_cpu")?d.cityMarketShortcut:null;
            CityShopShortcut=null;
            SetCityShopShortcut(d.cityShopShortcut);
            CityHomeShortcut = City.HasPlace(d.cityHomeShortcut) ? d.cityHomeShortcut : null;
            CityIntent=d.cityTravelIntent!=null&&City.HasPlace(d.cityTravelIntent.TargetPlaceId)&&!d.cityTravelIntent.Cancelled?d.cityTravelIntent:null;
            if(d.cityTrip!=null)
            {
                Trip=PCShopSell.Core.Session.Trip.RestoreCityTrip(d.cityTrip,City,CurrentVehicle);
                if(Trip!=null){CurrentPlace=null;_free=null;}
            }
            else if(d.cityPlayerOffNode&&CurrentPlace==null)_free=OutsideResidenceWalls(new MapPoint(d.cityPlayerX,d.cityPlayerY));
            LoadCityStories(d);
            LoadRelationshipStories(d);
            LoadCityVisits(d);
            RestoreVenueSponsorships(d);
            if(d.cityLifeVersion<2)MigrateCityAppointments();
            if(!DebugInvitationPersonEnabled)RemoveInvitationTestPerson();
            // Restore only; do not replay time, charge a fee or grant rewards during load.
            foreach(var p in _cityPeople.Values)if(p.ActivityId!=null&&!_cityActivities.Any(a=>a.Id==p.ActivityId&&a.Active)
                && !_venueSponsorships.Any(a => a.Id == p.ActivityId && !a.Completed && a.Guests.Contains(p.PersonId))
                &&!(d.venueBusinesses??Array.Empty<VenueBusiness>()).Any(v=>v!=null&&ShopAt(v.PlaceId)!=null&&(v.CustomersHere??Array.Empty<VenueCustomer>()).Any(c=>c.PersonId==p.PersonId&&c.ReservationId==p.ActivityId)))p.ActivityId=null;
            var employed = Employees.Select(row => PersonIdOf(row.Person)).ToHashSet();
            foreach (var person in _cityPeople.Values)
            {
                if (employed.Contains(person.PersonId)) continue;
                var definition = CityPerson(person.PersonId);
                RestoreCityNpcHomeDistrict(person, definition);
                RefreshCityNpcWorkplace(person, definition);
            }
            foreach(var shop in _shops)shop.Sim.PlayerPresent=CityPlayerCanWork(shop);
            PositionStreetServicePeople();
        }

        private void RestoreCityNpcHomeDistrict(CityPersonState person, CityPersonDefinition definition)
        {
            if (!City.IsLakeCity || CityLifeCatalog.Person(person.PersonId) == null || !City.HasPlace(definition?.HomeId)
                || !City.HasPlace(person.HomePlaceId) || person.ActivityId != null || person.VisitId != null) return;
            var currentHome = City.GetPlace(person.HomePlaceId);
            var authoredHome = City.GetPlace(definition.HomeId);
            int tier = DistrictTierOf(authoredHome);
            int currentTier = DistrictTierOf(currentHome);
            if (tier == currentTier) return;

            bool earlyStoryContact = person.PersonId is Npcs.Lin or Npcs.Landlady or Npcs.Zhou;
            bool restoreLocalHome = earlyStoryContact && currentTier > tier;
            bool restoreHomeForJob = currentTier < tier && CityNpcWorkplace(definition, currentHome.Id) == null
                && CityNpcWorkplace(definition, authoredHome.Id) != null;
            if (!restoreLocalHome && !restoreHomeForJob) return;

            bool Available(Place home) => home.Kind == PlaceKind.Home && home != Home && !home.InitiallyVacant
                && !HasResidentialLease(home) && PropertyAt(home.Id) == null;
            var home = Available(authoredHome) ? authoredHome : City.Places.FirstOrDefault(p => DistrictTierOf(p) == tier && Available(p));
            if (home == null) return;
            if (City.CanonicalPlaceId(person.Route?.TargetPlaceId) == currentHome.Id
                && person.LeisureTargetId != currentHome.Id)
            {
                person.Route = null;
                person.LocationPlaceId = currentHome.Id;
                person.Task = CityPersonTask.Home;
                person.TaskLabel = "在家休息";
            }
            MoveCityHome(person, home);
        }

        private void RefreshCityNpcWorkplace(CityPersonState person, CityPersonDefinition definition)
        {
            string oldWork = City.CanonicalPlaceId(person.WorkPlaceId);
            person.WorkPlaceId = CityNpcWorkplace(definition, person.HomePlaceId);
            if (oldWork == null || person.WorkPlaceId == oldWork || person.ActivityId != null || person.VisitId != null) return;
            bool commuting = City.CanonicalPlaceId(person.Route?.TargetPlaceId) == oldWork && person.LeisureTargetId != oldWork;
            bool atWork = person.Route == null && City.CanonicalPlaceId(person.LocationPlaceId) == oldWork
                && person.Task is CityPersonTask.Working or CityPersonTask.Waiting;
            if (!commuting && !atWork) return;

            bool working = AuthoredCityWorkAt(person, Clock.Now.TotalMinutes);
            person.Route = null;
            person.LocationPlaceId = working ? person.WorkPlaceId : person.HomePlaceId;
            person.Task = working ? CityPersonTask.Working : CityPersonTask.Home;
            person.TaskLabel = working ? "当班工作" : "在家休息";
            var point = StandOf(City.GetPlace(person.LocationPlaceId));
            person.X = point.X;
            person.Y = point.Y;
        }
    }
    public sealed partial class CityStoryProgress
    {
        public CityStoryProgress Copy() => (CityStoryProgress)MemberwiseClone();
    }
}
