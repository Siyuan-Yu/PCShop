using PCShopSell.Core.Session;

namespace PCShopSell.Tests
{
    internal static class CityTestPeople
    {
        public static void ApproachAppointment(GameSession session, CityActivity activity)
        {
            long approach=activity.ScheduledStart-120;
            session.Clock.Restore(new PCShopSell.Core.Time.GameDate((int)(approach/1440)),(int)(approach%1440));
            CompanyWellbeingRegressionTests.Advance(session,120);
        }

        public static void WaitForVenueStaff(GameSession session, CityActivity activity)
        {
            for(int minute=0;minute<GameSession.CityArrivalWindowMinutes && activity.Active
                && session.NpcVenueStaffingBlocker(session.CurrentPlace,activity)!=null;minute++)
                CompanyWellbeingRegressionTests.Advance(session,1);
        }
        // Legacy tests attach a synthetic Employee directly. Arrange their physical presence
        // as well, without weakening the production requirement for actual on-site staff.
        public static void AtAssignedWork(GameSession session,Employee employee)
        {
            var row=session.FindEmployee(employee.Id);if(row?.Shop==null)return;
            var person=session.CityPersonState(session.PersonIdOf(employee));
            var point=session.City.ViewPosition(row.Shop.Place.NodeId);
            person.X=point.X;person.Y=point.Y;person.LocationPlaceId=row.Shop.Place.Id;
            person.WorkPlaceId=row.Shop.Place.Id;person.Route=null;person.ActivityId=null;person.VisitId=null;
            person.Task=CityPersonTask.Working;
        }
    }
}
