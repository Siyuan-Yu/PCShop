using System;
using System.Linq;
using PCShopSell.Core.Shop;
using PCShopSell.Core.World;

namespace PCShopSell.Core.Session
{
    public sealed partial class GameSession
    {
        private long _nextCityVisit=1;
        private void BindCityCustomer(OwnedShop shop,ShopCustomer customer)
        {
            // All purchases use normal shop demand. Identity is optional, never a limit on arrivals.
            customer.VisitId = shop.Place.Id + ":visit:" + _nextCityVisit++;
            if (!Random.Chance(.25)) return;
            InitializeCityPeople();
            var person = _cityPeople.Values.Where(p => p.PersonId != CityLifeCatalog.InvitationTestPersonId && p.Route == null && p.ActivityId == null && p.VisitId == null
                && p.Task != CityPersonTask.Working && p.Task != CityPersonTask.Waiting
                && !Employees.Any(e => e.Person.PersonId == p.PersonId)
                && !_candidates.Any(e => e.PersonId == p.PersonId)
                && !_cityActivities.Any(activity => activity.Active && activity.Participants.Any(participant => participant.PersonId == p.PersonId))
                && (p.Task == CityPersonTask.Leisure || p.Task == CityPersonTask.Home && p.WorkPlaceId == null)
                && DistanceCity(new MapPoint(p.X, p.Y), StandOf(shop.Place)) <= 65)
                .OrderBy(p => p.LocationPlaceId == shop.Place.Id ? 0 : 1)
                .ThenBy(p => DistanceCity(new MapPoint(p.X, p.Y), StandOf(shop.Place))).FirstOrDefault();
            if (person == null) return; // Fall back to an ordinary customer; do not manufacture a permanent contact.
            var route = PlanCityRoute(person, shop.Place.Id, Clock.Now.TotalMinutes);
            if (route == null) return;
            customer.PersonId = person.PersonId;
            person.VisitId = customer.VisitId; person.Task = CityPersonTask.Shopping; person.TaskLabel = "到" + shop.Place.Name + "购物";
            person.Route = route; person.LocationPlaceId = null;

        }
        private void ReleaseCityCustomer(ShopCustomer customer)
        {
            var person=CityPersonState(customer.PersonId);
            if(person==null||person.VisitId!=customer.VisitId)return;
            person.VisitId=null;person.Task=CityPersonTask.Returning;person.TaskLabel="购物后返程";
        }
        public bool CityCustomerIsInside(ShopCustomer customer,string shopId) => string.IsNullOrEmpty(customer.PersonId)
            || CityPersonState(customer.PersonId) is CityPersonState p && p.Route==null&&p.LocationPlaceId==shopId;
        private string CityRetailSeriesFor(string personId)
        {
            var person = CityPerson(personId);
            if (person == null) return new[] { "rainbot", "harbor", "garden", "starlight" }[ConversationSeed(personId) % 4];
            string interest=person.Interest??"";
            return interest.Contains("模型")||interest.Contains("机械")?"rainbot":interest.Contains("河")||interest.Contains("户外")?"harbor":interest.Contains("植物")||interest.Contains("生态")||interest.Contains("园艺")?"garden":"starlight";
        }
        private long CityRetailBudgetFor(string personId)
        {
            uint hash=2166136261;foreach(char c in personId??"visitor")hash=(hash^c)*16777619;
            return new long[]{8000,15000,30000,60000}[hash%4];
        }
    }
}
