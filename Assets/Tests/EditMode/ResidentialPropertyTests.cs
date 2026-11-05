using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;

namespace PCShopSell.Tests
{
    public sealed class ResidentialPropertyTests
    {
        private static GameSession Make()
        {
            var s=DistrictProgressionTests.Make();s.DebugAddCash(10000000);
            typeof(GameSession).GetField("_district",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(s,new DistrictProgress{UnlockedTier=3,Chapter=3});
            return s;
        }
        private static void AtAgency(GameSession s,string id)=>typeof(GameSession).GetProperty("CurrentPlace").SetValue(s,s.AgencyFor(s.City.GetPlace(id)));
        private static string Occupied(GameSession s)=>s.ResidentialPlaces.First(p=>s.ResidentialTenants(p.Id).Length>0).Id;
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void Settle(GameSession s,int day)=>typeof(GameSession).GetMethod("SettleCityProperties",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{new GameDate(day)});
        private static string Vacate(GameSession s)
        {
            string id=Occupied(s);string other=s.ResidentialPlaces.First(p=>p.Id!=id&&p!=s.Home&&!p.Id.StartsWith("home_tier")).Id;
            foreach(var resident in s.ResidentialTenants(id))s.CityPersonState(resident.PersonId).HomePlaceId=other;
            return id;
        }
        [Test] public void AuditActualRegisteredAddressesRatherThanSceneryOrCurrentPresence()
        {
            using(var s=Make())
            {
                var homes=s.ResidentialPlaces;int occupied=homes.Count(p=>s.ResidentialTenants(p.Id).Length>0);
                Debug.Log("RESIDENTIAL AUDIT total="+homes.Length+" occupied="+occupied+" empty="+(homes.Length-occupied)+" registeredNPC="+homes.Sum(p=>s.ResidentialTenants(p.Id).Length));
                Assert.Greater(occupied,4);
                foreach(var place in homes.Where(p=>p.Id=="home"||p.Id.StartsWith("home_tier")))Assert.IsEmpty(s.ResidentialTenants(place.Id));
            }
        }
        [Test] public void BuyingOccupiedHomePreservesNpcAddressesAndPaysOneRealMonthlyRentAfterPurchase()
        {
            using(var s=Make())
            {
                string id=Occupied(s);AtAgency(s,id);var place=s.City.GetPlace(id);var residents=s.ResidentialTenants(id).Select(p=>p.PersonId).ToArray();
                long before=s.Ledger.Balance.Cents;var cost=s.ResidentialPurchaseDue(id,false,false);
                Assert.True(s.BuyResidentialHome(id,false,false));Assert.AreEqual(before-cost.Cents,s.Ledger.Balance.Cents);
                Assert.AreEqual("home",s.Home.Id);CollectionAssert.AreEquivalent(residents,s.ResidentialTenants(id).Select(p=>p.PersonId));
                var property=s.PropertyAt(id);Assert.AreEqual("npc",property.UseMode);Assert.Contains(property.TenantPersonId,residents);
                long after=s.Ledger.Balance.Cents;int next=(s.Clock.Today.TotalDays/30+1)*30;
                Settle(s,next);Assert.AreEqual(after+SNet(s,id),s.Ledger.Balance.Cents);
                long paid=s.Ledger.Balance.Cents;Settle(s,next);Assert.AreEqual(paid,s.Ledger.Balance.Cents);
                using(var l=Reload(s))
                {
                    Settle(l,next);Assert.AreEqual(paid,l.Ledger.Balance.Cents);
                    CollectionAssert.AreEquivalent(residents,l.ResidentialTenants(id).Select(p=>p.PersonId));
                }
            }
        }
        private static long SNet(GameSession s,string id)=>s.PropertyAt(id).MonthlyRentCents-Money.FromCents(s.PropertyAt(id).PriceCents).Scale(.001).Cents;
        [Test] public void ActualMonthBoundaryCreditsRegisteredTenantRentOnceAndReloadDoesNotReplayIt()
        {
            using(var s=Make())
            {
                string id=Occupied(s);AtAgency(s,id);Assert.True(s.BuyResidentialHome(id,false,false));
                long rent=s.PropertyAt(id).MonthlyRentCents;int next=(s.Clock.Today.TotalDays/30+1)*30;
                s.Clock.Restore(new GameDate(next-1),1439);s.AdvanceClock(1);
                var receipts=s.Ledger.Entries.Where(e=>e.Category=="物业租金"&&e.Attribution.PropertyId==id).ToArray();
                Assert.AreEqual(1,receipts.Length);Assert.AreEqual(rent,receipts[0].Amount.Cents);
                using(var loaded=Reload(s))
                {
                    loaded.AdvanceClock(1);
                    Assert.AreEqual(1,loaded.Ledger.Entries.Count(e=>e.Category=="物业租金"&&e.Attribution.PropertyId==id));
                }
            }
        }
        [Test] public void AbsentRegisteredResidentStillBlocksMovingInAndSaleDoesNotEvict()
        {
            using(var s=Make())
            {
                string id=Occupied(s);AtAgency(s,id);var tenants=s.ResidentialTenants(id);
                foreach(var p in tenants)s.CityPersonState(p.PersonId).LocationPlaceId=s.Home.Id;
                long cash=s.Ledger.Balance.Cents;Assert.False(s.RentResidentialHome(id));Assert.False(s.BuyResidentialHome(id,false,true));Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                Assert.True(s.BuyResidentialHome(id,true,false));Assert.False(s.RentResidentialHome(id));Assert.False(s.OpenCityOwnedShop(id));
                Assert.True(s.SellProperty(id));Assert.IsNull(s.PropertyAt(id));
                CollectionAssert.AreEquivalent(tenants.Select(p=>p.PersonId),s.ResidentialTenants(id).Select(p=>p.PersonId));
                Assert.False(s.Listings.Any(l=>l.PlaceId==id));
            }
        }
        [Test] public void AnyGenuinelyEmptyHomeCanBeRentedAndSavedThenBoughtWithoutSecondMoveFee()
        {
            using(var s=Make())
            {
                string id=Vacate(s);AtAgency(s,id);var place=s.City.GetPlace(id);var old=s.HomeBox.Food.RiceKg=4;
                long before=s.Ledger.Balance.Cents;Assert.True(s.RentResidentialHome(id));
                Assert.AreEqual(id,s.Home.Id);Assert.AreEqual(before-s.ResidentialRent(place).Cents-50000,s.Ledger.Balance.Cents);
                using(var l=Reload(s))
                {
                    Assert.AreEqual(id,l.Home.Id);Assert.AreEqual(old,l.HomeBox.Food.RiceKg);
                    before=l.Ledger.Balance.Cents;Assert.True(l.BuyHome(false));
                    Assert.AreEqual(before-l.ResidentialPrice(place).Cents,l.Ledger.Balance.Cents);Assert.Zero(l.CurrentHomeRent.Cents);
                }
            }
        }
        [Test] public void EmptyInvestmentCannotGeneratePhantomRentAndCanBeConvertedToSelfUseWithoutDoubleOwnership()
        {
            using(var s=Make())
            {
                string id=Vacate(s);AtAgency(s,id);Assert.True(s.BuyResidentialHome(id,true,false));
                Assert.False(s.SetCityPropertyRental(id,true));
                int rents=s.Ledger.Entries.Count(e=>e.Category=="物业租金");Settle(s,(s.Clock.Today.TotalDays/30+1)*30);
                Assert.AreEqual(rents,s.Ledger.Entries.Count(e=>e.Category=="物业租金"));
                long principal=s.PropertyAt(id).MortgageCents;long cash=s.Ledger.Balance.Cents;
                Assert.True(s.RentResidentialHome(id));Assert.AreEqual(id,s.Home.Id);Assert.IsNull(s.PropertyAt(id));
                Assert.AreEqual(principal,s.HomeMortgageCents);Assert.AreEqual(cash-50000,s.Ledger.Balance.Cents);Assert.Zero(s.CurrentHomeRent.Cents);
            }
        }
        [Test] public void WrongAgencyAndDuplicatePurchaseCannotDebitOrCreateOwnership()
        {
            using(var s=Make())
            {
                string id=Occupied(s);AtAgency(s,"home_tier3");
                if(s.AgencyServes(s.CurrentPlace,s.City.GetPlace(id)))AtAgency(s,"home");
                long cash=s.Ledger.Balance.Cents;Assert.False(s.BuyResidentialHome(id,false,false));Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                AtAgency(s,id);Assert.True(s.BuyResidentialHome(id,false,false));cash=s.Ledger.Balance.Cents;
                Assert.False(s.BuyResidentialHome(id,false,false));Assert.AreEqual(cash,s.Ledger.Balance.Cents);
                Assert.AreEqual(1,s.OwnedProperties.Count(p=>p.PlaceId==id));
            }
        }
    }
}
