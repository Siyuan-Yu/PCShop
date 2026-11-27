using System;
using NUnit.Framework;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public sealed class VenueAccessRegressionTests
    {
        [Test] public void FishingCanBeReachedAndStartedAfterActualTravel()
        {
            using var s=DistrictProgressionTests.Make();CompanyWellbeingRegressionTests.Tier(s,3);
            s.SetSmartphone(true);s.Time.SetSpeed(8);
            Assert.True(s.BeginCityTravel(GameSession.FishingVenueId));
            for(int step=0;step<16000&&s.CurrentPlace?.Id!=GameSession.FishingVenueId;step++)
            {s.Player.Health=s.Player.Hunger=s.Player.Energy=100;s.Tick(.05);}
            Assert.AreEqual(GameSession.FishingVenueId,s.CurrentPlace?.Id,s.ClosedReason(s.City.GetPlace(GameSession.FishingVenueId)));
            Assert.True(s.SelectVenueService("fishing",0),s.VenueServiceBlocker("fishing",0));Assert.True(s.FishingActive);
        }
        [TestCase(0,600)] [TestCase(6,1380)]
        public void BankScheduledStaffReachCounterWhenWorldAdvances(int day,int minute)
        {
            using var s=DistrictProgressionTests.Make();CompanyWellbeingRegressionTests.Tier(s,3);
            s.Clock.Restore(new GameDate(day),minute-120);
            CompanyWellbeingRegressionTests.Advance(s,120);
            var bank=s.City.GetPlace(GameSession.BankPlaceId);
            Assert.IsNotNull(s.BankTellerOnDuty(bank),s.ClosedReason(bank));
        }
    }
}
