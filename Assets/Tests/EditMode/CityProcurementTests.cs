using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    // Added for the city-life integration. Do not run without repository validation authorization.
    public sealed class CityProcurementTests
    {
        private static GameSession Make()
        {
            var city = CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            // Start after the 09:00 auto-restock boundary: manual orders advance five minutes,
            // and these tests isolate coverage rather than requesting an extra scheduled purchase.
            var s = new GameSession(city, 7, Money.FromYuan(1000000), GameTimestamp.From(new GameDate(0), 9 * 60 + 1), false);
            s.EventsEnabled = false; s.MainShop.Sim.Settings.BaseCustomersPerHour = 0;
            s.InstallApp(PhoneApps.Wholesale); s.SetRestockTarget("cpu0", 8);
            return s;
        }
        [Test]
        public void ClaimThenConfirmedOrderCountsOnlyOneSupply_AndCancelRestoresDeficit()
        {
            using (var s = Make())
            {
                string shop = s.MainShop.Place.Id;
                string claim = s.ClaimProcurementDemand(shop, PurchaseGoodsKind.Part, "cpu0", 8, ProcurementMethod.Online1788, "test-executor", "s_mid");
                Assert.NotNull(claim);
                Assert.AreEqual(0, s.ProcurementCoverageFor(shop, PurchaseGoodsKind.Part, "cpu0").Uncovered);
                var before = s.Ledger.Balance;
                s.RunAutoRestock(); Assert.Zero(s.Online.Pending.Count); Assert.AreEqual(before, s.Ledger.Balance);
                Assert.True(s.ExecuteProcurementClaim(claim));
                Assert.AreEqual(8, s.PendingProcurementCount(s.MainShop, PurchaseGoodsKind.Part, "cpu0"));
                Assert.Zero(s.ClaimedProcurementCount(shop, PurchaseGoodsKind.Part, "cpu0"));
                Assert.AreEqual(0, s.ProcurementCoverageFor(shop, PurchaseGoodsKind.Part, "cpu0").Uncovered);
                int id = s.Online.Pending.Single().Id;
                Assert.True(s.CancelOnlineProcurementOrder(id)); Assert.False(s.CancelOnlineProcurementOrder(id));
                Assert.AreEqual(before, s.Ledger.Balance);
                Assert.AreEqual(8, s.ProcurementCoverageFor(shop, PurchaseGoodsKind.Part, "cpu0").Uncovered);
            }
        }
        [Test]
        public void ClaimsAndPoliciesSurviveJsonReload_WithoutMoneyMovement()
        {
            using (var s = Make())
            {
                string shop = s.MainShop.Place.Id;
                var policy = s.ProcurementPolicyFor(shop, PurchaseGoodsKind.Part, "cpu0"); policy.Lower = 2;
                Assert.True(s.SetProcurementPolicy(policy));
                string claim = s.ClaimProcurementDemand(shop, PurchaseGoodsKind.Part, "cpu0", 8, ProcurementMethod.Online1788, "test-executor", "s_mid");
                Assert.NotNull(claim); var balance = s.Ledger.Balance;
                var save = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using (var loaded = GameSession.Load(s.City, save))
                {
                    Assert.AreEqual(balance, loaded.Ledger.Balance);
                    Assert.AreEqual(1, loaded.ProcurementClaims.Count(c => c.State == ProcurementClaimState.Claimed));
                    Assert.AreEqual(0, loaded.ProcurementCoverageFor(shop, PurchaseGoodsKind.Part, "cpu0").Uncovered);
                    Assert.AreEqual(2, loaded.ProcurementPolicyFor(shop, PurchaseGoodsKind.Part, "cpu0").Lower);
                }
            }
        }
        [Test]
        public void OutstandingBuildMaterialsDoNotAlsoCoverRetailTarget()
        {
            using (var s = Make())
            {
                s.MainShop.Sim.Settings.BuildOrdersStartDay = 0;
                var at = s.Clock.Now;
                s.MainShop.Sim.RestoreOrders(new[] { new BuildOrder(1, 0, Money.FromYuan(3000), at, at.AddMinutes(600)),
                    new BuildOrder(2, 0, Money.FromYuan(3000), at, at.AddMinutes(700)) }, 3);
                s.MainShop.Sim.Stock.ForceAdd(PartCatalog.Get("cpu0"), true, 1);
                var coverage = s.ProcurementCoverageFor(s.MainShop.Place.Id, PurchaseGoodsKind.Part, "cpu0");
                Assert.Zero(coverage.FreeStock); Assert.AreEqual(1, coverage.ContractMissing); Assert.AreEqual(9, coverage.Uncovered);
                Assert.True(s.PlaceUnifiedOnlinePurchase(s.MainShop.Place.Id, "s_mid", "cpu0", 1));
                coverage = s.ProcurementCoverageFor(s.MainShop.Place.Id, PurchaseGoodsKind.Part, "cpu0");
                Assert.Zero(coverage.InTransit, "the one arriving CPU is committed to the outstanding build, not retail");
                Assert.Zero(coverage.ContractMissing); Assert.AreEqual(8, coverage.Uncovered);
            }
        }
        [Test]
        public void EmptySerializedRouteCannotMoveAnOnDutyPersonHomeDuringLoad()
        {
            using(var s=Make())
            {
                var employee=new Employee{Name="On-site worker",DailyWageYuan=100};s.MainShop.Buyer=employee;
                CityTestPeople.AtAssignedWork(s,employee);
                var save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                var person=save.cityPeople.Single(p=>p.PersonId==s.PersonIdOf(employee));
                person.Route=new CityRoute(); // JsonUtility's empty nested-reference representation.
                string location=person.LocationPlaceId;double x=person.X,y=person.Y;
                using(var loaded=GameSession.Load(s.City,save))
                {
                    var restored=loaded.CityPersonState(loaded.PersonIdOf(loaded.MainShop.Buyer));
                    Assert.IsNull(restored.Route);Assert.AreEqual(location,restored.LocationPlaceId);
                    Assert.AreEqual(x,restored.X);Assert.AreEqual(y,restored.Y);
                    Assert.True(loaded.CityEmployeeAvailable(loaded.MainShop.Buyer,loaded.MainShop));
                }
            }
        }
        [Test]
        public void LegacyOnDutyEmployeeMigratesToOwnShopOnceWithoutMovingNewSavePositions()
        {
            using(var s=Make())
            {
                var worker=new Employee{Name="Migration worker",DailyWageYuan=100};s.MainShop.Buyer=worker;
                var save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                save.cityLifeVersion=0;save.cityPeople=null;
                using(var old=GameSession.Load(s.City,save))
                {
                    var employee=old.MainShop.Buyer;var person=old.CityPersonState(old.PersonIdOf(employee));
                    Assert.AreEqual(old.MainShop.Place.Id,person.LocationPlaceId);Assert.True(old.CityEmployeeAvailable(employee,old.MainShop));
                    person.LocationPlaceId=person.HomePlaceId;person.Route=null;person.X=12;person.Y=34;
                    using(var current=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(old.CreateSave()))))
                    {
                        var restored=current.CityPersonState(current.PersonIdOf(current.MainShop.Buyer));
                        Assert.AreEqual(person.HomePlaceId,restored.LocationPlaceId);
                        Assert.AreEqual(12,restored.X);Assert.AreEqual(34,restored.Y);
                        Assert.False(current.CityEmployeeAvailable(current.MainShop.Buyer,current.MainShop));
                    }
                }
            }
        }
        [Test]
        public void PurchaseTravelUsesOnePersonAndPersistsItsActualRouteProgress()
        {
            using(var s=Make())
            {
                long now=s.Clock.Now.TotalMinutes;
                var trip=new EmployeePurchaseTrip{EmployeeId="route-test",OrderId="route-test-order",ShopId=s.MainShop.Place.Id,
                    DepartAt=now,BoughtAt=now+120,ReturnAt=now+240,DeliveryAt=now+210,Transport=VehicleKind.Walk,
                    Items=new[]{new PurchaseLine{Kind=PurchaseGoodsKind.Part,PartId="cpu0",Count=1,SourceId="stall_cpu"}}};
                var person=s.CityPersonState("life_qiao");Assert.NotNull(person);
                int population=s.CityPeople.Count();var cash=s.Ledger.Balance;
                var apply=typeof(GameSession).GetMethod("ApplyCityPurchasePosition",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                Assert.NotNull(apply);apply.Invoke(s,new object[]{trip,person,now});
                Assert.NotNull(person.Route);Assert.IsNull(person.LocationPlaceId);
                Assert.AreEqual(2,trip.TravelLegs.Length);
                long middle=(trip.TravelLegs[0].DepartMinute+trip.TravelLegs[0].ArriveMinute)/2;
                apply.Invoke(s,new object[]{trip,person,middle});
                double x=person.X,y=person.Y;
                var restored=JsonUtility.FromJson<EmployeePurchaseTrip>(JsonUtility.ToJson(trip));
                apply.Invoke(s,new object[]{restored,person,middle});
                Assert.AreEqual(x,person.X);Assert.AreEqual(y,person.Y);
                apply.Invoke(s,new object[]{restored,person,trip.ReturnAt});
                Assert.IsNull(person.Route);Assert.AreEqual(s.MainShop.Place.Id,person.LocationPlaceId);
                Assert.False(trip.Delivered,"person movement never delivers or duplicates goods");
                Assert.AreEqual(population,s.CityPeople.Count());Assert.AreEqual(cash,s.Ledger.Balance);
            }
        }
        [Test]
        public void QueriesNeverPlaceOrderOrChangePlayerLocation()
        {
            using (var s = Make())
            {
                var cash = s.Ledger.Balance; var place = s.CurrentPlace; var focus = s.CurrentShop; string node = s.PlayerNodeId;
                for (int i = 0; i < 3; i++)
                {
                    s.ProcurementSources(PurchaseGoodsKind.Part, "cpu0");
                    s.ProcurementCoverageFor(s.MainShop.Place.Id, PurchaseGoodsKind.Part, "cpu0");
                    s.ProcurementPolicyFor(s.MainShop.Place.Id, PurchaseGoodsKind.Part, "cpu0");
                }
                Assert.AreEqual(cash, s.Ledger.Balance); Assert.AreSame(place, s.CurrentPlace); Assert.AreSame(focus, s.CurrentShop);
                Assert.AreEqual(node, s.PlayerNodeId); Assert.Zero(s.Online.Pending.Count); Assert.Zero(s.ProcurementClaims.Count);
            }
        }
        [Test]
        public void OldFixedEmployeePlanOwnsOverlappingTarget_WithoutCreatingOnlineOrder()
        {
            using (var s = Make())
            {
                var offer = s.EmployeePurchaseCatalog().First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "cpu0");
                s.MainShop.Buyer = new Employee { Name = "可靠采购员", DailyWageYuan = 150, PurchasePlan = new EmployeePurchasePlan { Daily = true, Items = new[] { offer.Line(8) } } };
                var legacy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                legacy.cityProcurementVersion = 0; legacy.cityProcurementPolicies = null;
                using (var loaded = GameSession.Load(s.City, legacy))
                {
                    var policy = loaded.ProcurementPolicyFor(loaded.MainShop.Place.Id, PurchaseGoodsKind.Part, "cpu0");
                    Assert.AreEqual(ProcurementMethod.Employee, policy.Primary);
                    loaded.RunAutoRestock(); Assert.Zero(loaded.Online.Pending.Count);
                    Assert.False(loaded.MainShop.Buyer.PurchasePlan.InventoryThreshold, "migration must preserve fixed-list intent");
                    var paused = loaded.MainShop.Buyer.PurchasePlan.Copy(); paused.Daily = false;
                    Assert.True(loaded.ConfigureEmployeePurchase(loaded.MainShop.Buyer.Id, paused));
                    loaded.RunAutoRestock(); Assert.Zero(loaded.Online.Pending.Count, "pausing primary execution must not silently activate a fallback");
                }
            }
        }

        [Test]
        public void WednesdayPlanTakesOverAnOldOnlineTargetForTheSameProduct()
        {
            using (var s = Make())
            {
                string shop = s.MainShop.Place.Id;
                var oldPolicy = s.ProcurementPolicyFor(shop, PurchaseGoodsKind.Part, "cpu0");
                Assert.AreEqual(ProcurementMethod.Online1788, oldPolicy.Primary);
                Assert.True(s.SetProcurementPolicy(oldPolicy));

                var buyer = new Employee { Name = "采购员", DailyWageYuan = 150 };
                s.MainShop.Buyer = buyer;
                CityTestPeople.AtAssignedWork(s, buyer);
                var offer = s.EmployeePurchaseCatalog().First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "cpu0");
                Assert.True(s.CompleteEmployeePurchase(buyer.Id, new EmployeePurchasePlan
                {
                    HasWeeklySchedule = true, Frequency = PurchaseFrequency.Wednesday, Items = new[] { offer.Line(2) }
                }));

                foreach (int day in new[] { 1, 2, 3 })
                {
                    s.Clock.Restore(new GameDate(day), 9 * 60);
                    CityTestPeople.AtAssignedWork(s, buyer);
                    s.AdvanceClock(1);
                    Assert.Zero(s.Online.Pending.Count);
                }

                var purchase = s.EmployeePurchaseRecords(shop, buyer.Id).Single();
                Assert.AreEqual(GameWeekday.Wednesday, new GameTimestamp(purchase.DepartAt).Date.Weekday);
            }
        }
    }
}
