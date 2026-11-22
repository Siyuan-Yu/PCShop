using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class WorkforceTests
    {
        private static GameSession Make(bool two = false)
        {
            var s = GameSessionTests.Make(500000, 9 * 60);
            s.EventsEnabled = false;
            if (two)
            {
                s.PlayerShop.Reputation = 80;
                s.DebugAddListing("tea", 0, 60, Money.FromYuan(3000));
                GameSessionTests.Teleport(s, "agency");
                Assert.True(s.SignLease("tea", true));
            }
            foreach (var shop in s.MyShops) shop.Sim.Settings.BaseCustomersPerHour = 0;
            GameSessionTests.Teleport(s, "pc");
            return s;
        }

        private static Employee Worker(string name = "陈启明") => new Employee { Name = name, DailyWageYuan = 150, CheckoutMinutes = 3 };
        private static EmployeePurchasePlan Plan(VehicleKind kind = VehicleKind.Taxi, int count = 2) => new EmployeePurchasePlan
        {
            SourceId = "stall", Transport = kind, BudgetYuan = 50000,
            Items = new[] { new PurchaseLine { PartId = "cpu0", Count = count } }
        };
        private static GameSession RoundTrip(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void Midnight(GameSession s)
        {
            s.Clock.Restore(s.Clock.Today, 23 * 60 + 59);
            s.AdvanceClock(2);
        }
        private static void Buy(GameSession s, VehicleKind kind, int count = 1)
        {
            GameSessionTests.Teleport(s, "dealer");
            for (int i = 0; i < count; i++) Assert.True(s.BuyVehicle(kind));
            GameSessionTests.Teleport(s, "pc");
        }

        [TestCase(EmployeeRole.Cashier)]
        [TestCase(EmployeeRole.Assembler)]
        [TestCase(EmployeeRole.Buyer)]
        public void Transfer_LeavesTodayUntouched_AndMovesTomorrowAfterJson(EmployeeRole role)
        {
            using (var s = Make(true))
            {
                var e = Worker();
                e.BuildsCompleted = 9;
                if (role == EmployeeRole.Cashier) s.MainShop.Employee = e;
                if (role == EmployeeRole.Assembler) s.MainShop.Assembler = e;
                if (role == EmployeeRole.Buyer) s.MainShop.Buyer = e; CityTestPeople.AtAssignedWork(s,e);
                Assert.True(s.ScheduleEmployeeTransfer(e.Id, "tea"));
                Assert.AreEqual("pc", s.FindEmployee(e.Id).Shop.Place.Id);
                using (var loaded = RoundTrip(s))
                {
                    Assert.AreEqual("pc", loaded.FindEmployee(e.Id).Shop.Place.Id);
                    Midnight(loaded);
                    var row = loaded.FindEmployee(e.Id);
                    Assert.AreEqual("tea", row.Shop.Place.Id);
                    Assert.AreEqual(role, row.Role);
                    Assert.AreEqual(150, row.Person.DailyWageYuan);
                    Assert.AreEqual(9, row.Person.BuildsCompleted);
                    Assert.IsNull(row.Person.TransferShopId);
                    Assert.AreEqual(1, loaded.Employees.Count);
                }
            }
        }

        [Test]
        public void Transfer_CanCancel_AndCannotOverwriteOccupiedRole()
        {
            using (var s = Make(true))
            {
                var e = Worker(); s.MainShop.Employee = e;
                s.ShopAt("tea").Employee = Worker("小李");
                Assert.False(s.ScheduleEmployeeTransfer(e.Id, "tea"));
                Assert.AreEqual(2, s.Employees.Count);
                s.ShopAt("tea").Employee = null;
                Assert.True(s.ScheduleEmployeeTransfer(e.Id, "tea"));
                s.CancelEmployeeTransfer(e.Id);
                Midnight(s);
                Assert.AreEqual("pc", s.FindEmployee(e.Id).Shop.Place.Id);
            }
        }

        [Test]
        public void Json_DoesNotInventBuyerInOldOrEmptySlots()
        {
            using (var s = Make(true))
            using (var loaded = RoundTrip(s))
            {
                Assert.IsTrue(loaded.MyShops.All(x => x.Buyer == null));
                Assert.Zero(loaded.Employees.Count);
                Assert.DoesNotThrow(() => Midnight(loaded));
            }
        }

        [Test]
        public void Procurement_PrepaysOnce_DeliversToAssignedShopOnce_AndResumesFromJson()
        {
            using (var s = Make(true))
            {
                var e = Worker(); s.ShopAt("tea").Buyer = e; CityTestPeople.AtAssignedWork(s,e);
                Assert.True(s.ConfigureEmployeePurchase(e.Id, Plan()));
                var q = s.QuoteEmployeePurchase(e.Id);
                Assert.IsNull(q.Error, q.Error);
                var cash = s.Ledger.Balance;
                int original = s.ShopAt("tea").Sim.Stock.CountAll(PartCatalog.Get("cpu0"));
                Assert.True(s.DispatchEmployeePurchase(e.Id));
                Assert.AreEqual(cash - q.Total, s.Ledger.Balance);
                Assert.AreEqual(original, s.ShopAt("tea").Sim.Stock.CountAll(PartCatalog.Get("cpu0")), "未送到不入库");
                Assert.False(s.DispatchEmployeePurchase(e.Id), "在途不能重复派出");
                using (var loaded = RoundTrip(s))
                {
                    foreach(var shop in loaded.MyShops)shop.Sim.Settings.BaseCustomersPerHour=0;
                    var trip = loaded.EmployeePurchaseTrips.Single();
                    int minutes = (int)(Math.Max(trip.DeliveryAt, trip.ReturnAt) - loaded.Clock.Now.TotalMinutes + 1);
                    var paid = loaded.Ledger.Balance;
                    int other = loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get("cpu0"));
                    loaded.AdvanceClock(minutes);
                    Assert.AreEqual(original + 2, loaded.ShopAt("tea").Sim.Stock.CountAll(PartCatalog.Get("cpu0")));
                    Assert.AreEqual(other, loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get("cpu0")));
                    Assert.AreEqual(paid, loaded.Ledger.Balance, "到货不再扣款");
                    Assert.Zero(loaded.EmployeePurchaseTrips.Count);
                    loaded.AdvanceClock(5);
                    Assert.AreEqual(original + 2, loaded.ShopAt("tea").Sim.Stock.CountAll(PartCatalog.Get("cpu0")));
                }
            }
        }

        [Test]
        public void VehiclePool_TwoBikesCanServeBuyerAndOwner_AndLocksSurviveJson()
        {
            using (var s = Make())
            {
                Buy(s, VehicleKind.EBike);
                var e = Worker(); s.MainShop.Buyer = e; CityTestPeople.AtAssignedWork(s,e);
                Assert.True(s.ConfigureEmployeePurchase(e.Id, Plan(VehicleKind.EBike)));
                Assert.True(s.DispatchEmployeePurchase(e.Id));
                Assert.AreEqual(1, s.BusyVehicleCount(VehicleKind.EBike));
                Assert.False(s.SetVehicle(VehicleKind.EBike));
                Assert.False(s.DismissEmployee(e.Id));
                using (var loaded = RoundTrip(s))
                {
                    Assert.Zero(loaded.FreeVehicleCount(VehicleKind.EBike));
                    Buy(loaded, VehicleKind.EBike);
                    Assert.AreEqual(2, loaded.VehicleCount(VehicleKind.EBike));
                    Assert.True(loaded.SetVehicle(VehicleKind.EBike));
                    Assert.Zero(loaded.FreeVehicleCount(VehicleKind.EBike));
                    loaded.AdvanceClock((int)(loaded.EmployeePurchaseTrips.Single().ReturnAt - loaded.Clock.Now.TotalMinutes + 1));
                    Assert.AreEqual(1, loaded.FreeVehicleCount(VehicleKind.EBike));
                    Assert.AreEqual(2, loaded.VehicleCount(VehicleKind.EBike));
                }
            }
        }

        [Test]
        public void TwoBuyers_CannotReserveTheSameSingleVehicle()
        {
            using (var s = Make(true))
            {
                Buy(s, VehicleKind.EBike);
                var first = Worker("甲"); var second = Worker("乙");
                s.MainShop.Buyer = first; s.ShopAt("tea").Buyer = second; CityTestPeople.AtAssignedWork(s,first); CityTestPeople.AtAssignedWork(s,second);
                Assert.True(s.ConfigureEmployeePurchase(first.Id, Plan(VehicleKind.EBike)));
                Assert.True(s.ConfigureEmployeePurchase(second.Id, Plan(VehicleKind.EBike)));
                Assert.True(s.DispatchEmployeePurchase(first.Id));
                Assert.True(s.DispatchEmployeePurchase(second.Id));
                Assert.AreEqual(2, s.EmployeePurchaseTrips.Count);
                Assert.AreEqual(VehicleKind.EBike, s.EmployeeTrip(first.Id).Transport);
                Assert.IsNull(s.EmployeeTrip(second.Id).VehicleId);
                Assert.AreEqual(VehicleKind.EBike, second.PurchasePlan.Transport);
                Assert.AreEqual(1, s.BusyVehicleCount(VehicleKind.EBike));
            }
        }

        [TestCase(false, VehicleKind.Walk)]
        [TestCase(true, VehicleKind.Taxi)]
        public void ImpoundedPurchaseVehicleUsesFallbackWithoutChangingThePlan(bool shortShift, VehicleKind expectedTransport)
        {
            using var s = Make();
            Buy(s, VehicleKind.Tricycle);
            var buyer = Worker();
            s.MainShop.Buyer = buyer;
            CityTestPeople.AtAssignedWork(s, buyer);
            Assert.IsTrue(s.ConfigureEmployeePurchase(buyer.Id, Plan(VehicleKind.Tricycle)));

            var data = s.CreateSave();
            data.racing.Garage.Single(vehicle => MotorCatalog.Get(vehicle.ModelId).Kind == VehicleKind.Tricycle).Impounded = true;
            using var loaded = GameSession.Load(s.City, data);
            var worker = loaded.MainShop.Buyer;
            CityTestPeople.AtAssignedWork(loaded, worker);
            if (shortShift)
            {
                var taxi = loaded.EstimateEmployeePurchase(worker.Id, Plan(VehicleKind.Taxi));
                var walk = loaded.EstimateEmployeePurchase(worker.Id, Plan(VehicleKind.Walk));
                Assert.Less(taxi.ReturnMinutes, walk.ReturnMinutes);
                worker.ShiftEnd = loaded.Clock.MinuteOfDay + taxi.ReturnMinutes;
                worker.Schedule = new EmployeeSchedule();
            }
            var cash = loaded.Ledger.Balance;
            var quote = loaded.QuoteEmployeePurchase(worker.Id);
            Assert.IsNull(quote.Error);
            Assert.IsTrue(loaded.DispatchEmployeePurchase(worker.Id));
            var trip = loaded.EmployeeTrip(worker.Id);
            Assert.AreEqual(expectedTransport, trip.Transport);
            Assert.IsNull(trip.VehicleId);
            Assert.AreEqual(cash - quote.Total, loaded.Ledger.Balance);
            Assert.Greater(trip.FreightCents, 0);
            Assert.AreEqual(shortShift, trip.TravelCents > 0);
            Assert.AreEqual(1, loaded.ImpoundedVehicleCount(VehicleKind.Tricycle));
            Assert.AreEqual(trip.DepartAt + 3 * GameClock.MinutesPerDay,
                loaded.Garage.Single(vehicle => vehicle.Impounded).ImpoundReleaseAt);
            Assert.AreEqual(VehicleKind.Tricycle, worker.PurchasePlan.Transport);
        }

        [Test]
        public void TricyclesAreImpoundedAndReturnedIndividually()
        {
            using var s = Make();
            Buy(s, VehicleKind.Tricycle);
            var data = s.CreateSave();
            var commuter = data.racing.Garage.Single(v => v.Commuter);
            var spare = commuter.Copy();
            spare.Id = "test-spare-trike";
            spare.Commuter = false;
            data.racing.Garage = data.racing.Garage.Append(spare).ToArray();
            using var loaded = GameSession.Load(s.City, data);
            var impound = typeof(GameSession).GetMethod("ImpoundVehicle",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            bool Impound(string id) => (bool)impound.Invoke(loaded, new object[] { VehicleKind.Tricycle, id });

            Assert.IsTrue(Impound(spare.Id));
            Assert.IsTrue(loaded.CanUse(VehicleKind.Tricycle));
            Assert.AreEqual(1, loaded.ImpoundedVehicleCount(VehicleKind.Tricycle));
            loaded.AdvanceClock(GameClock.MinutesPerDay);
            Assert.IsTrue(Impound(commuter.Id));
            Assert.IsFalse(Impound(spare.Id));

            using var restored = RoundTrip(loaded);
            restored.AdvanceClock(2 * GameClock.MinutesPerDay - 1);
            Assert.AreEqual(2, restored.ImpoundedVehicleCount(VehicleKind.Tricycle));
            restored.AdvanceClock(1);
            Assert.AreEqual(commuter.Id, restored.Garage.Single(v => v.Impounded).Id);
            restored.AdvanceClock(GameClock.MinutesPerDay);
            Assert.AreEqual(0, restored.ImpoundedVehicleCount(VehicleKind.Tricycle));
        }

        [Test]
        public void OverweightPurchase_UnloadsAndContinuesSameDayAfterLoadingSave()
        {
            using (var s = Make())
            {
                Buy(s, VehicleKind.Van);
                var buyer = Worker();
                s.MainShop.Buyer = buyer;
                CityTestPeople.AtAssignedWork(s, buyer);
                var plan = Plan(VehicleKind.Van);
                plan.Items = new[] { "gpu0", "gpu1", "gpu2", "cpu0", "cpu1", "cpu2" }
                    .Select(id => new PurchaseLine { PartId = id, Count = 10 }).ToArray();
                Assert.True(s.ConfigureEmployeePurchase(buyer.Id, plan));
                Assert.True(s.DispatchEmployeePurchase(buyer.Id));
                using (var loaded = RoundTrip(s))
                {
                    loaded.MainShop.Sim.Settings.BaseCustomersPerHour = 0;
                    var first = loaded.EmployeePurchaseTrips.Single();
                    Assert.IsNotNull(first.RemainingItems);
                    Assert.Greater(first.RemainingItems.Length, 0);
                    int firstCount = first.Items.Sum(item => item.Count);
                    int openingStock = plan.Items.Sum(item => loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get(item.PartId)));
                    loaded.AdvanceClock((int)(Math.Max(first.ReturnAt, first.DeliveryAt) - loaded.Clock.Now.TotalMinutes));
                    var second = loaded.EmployeePurchaseTrips.Single();
                    Assert.AreEqual(loaded.Clock.Today.TotalDays, first.DepartAt / GameClock.MinutesPerDay);
                    Assert.AreNotEqual(first.OrderId, second.OrderId);
                    Assert.AreEqual(60, firstCount + second.Items.Sum(item => item.Count));
                    Assert.IsNull(second.RemainingItems);
                    loaded.AdvanceClock((int)(Math.Max(second.ReturnAt, second.DeliveryAt) - loaded.Clock.Now.TotalMinutes + 1));
                    Assert.IsEmpty(loaded.EmployeePurchaseTrips);
                    Assert.AreEqual(openingStock + 60, plan.Items.Sum(item => loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get(item.PartId))));
                }
            }
        }

        [TestCase("cash")]
        [TestCase("source")]
        [TestCase("quota")]
        [TestCase("closed")]
        [TestCase("shift")]
        [TestCase("space")]
        public void InvalidProcurement_DoesNotChargeReserveVehicleOrCreateGoods(string failure)
        {
            using (var s = Make())
            {
                Buy(s, VehicleKind.EBike);
                var e = Worker(); s.MainShop.Buyer = e; CityTestPeople.AtAssignedWork(s,e);
                var plan = Plan(VehicleKind.EBike);
                if (failure == "cash") s.Ledger.ForceDebit(s.Clock.Today, "测试", s.Ledger.Balance - Money.FromYuan(1), "现金只留一元");
                if (failure == "source") plan.Items[0].PartId = "ram0";
                if (failure == "quota") plan.Items[0].Count = 50;
                if (failure == "closed") s.Clock.Restore(new GameDate(5), 10 * 60);
                if (failure == "shift") e.ShiftStart = 13 * 60;
                if (failure == "space") s.MainShop.Sim.Stock.Capacity = s.MainShop.Sim.Stock.UsedSpace;
                if (failure == "source") { Assert.False(s.ConfigureEmployeePurchase(e.Id, plan)); return; }
                Assert.True(s.ConfigureEmployeePurchase(e.Id, plan));
                var before = s.Ledger.Balance;
                Assert.IsNotNull(s.QuoteEmployeePurchase(e.Id).Error);
                Assert.False(s.DispatchEmployeePurchase(e.Id));
                Assert.AreEqual(before, s.Ledger.Balance);
                Assert.Zero(s.EmployeePurchaseTrips.Count);
                Assert.AreEqual(1, s.FreeVehicleCount(VehicleKind.EBike));
            }
        }

        [Test]
        public void Buyer_DoesNotKeepShopOpen_AndWagesFollowTransferWithoutDuplication()
        {
            using (var s = Make(true))
            {
                var e = Worker(); s.MainShop.Buyer = e; CityTestPeople.AtAssignedWork(s,e);
                Assert.False(s.IsEmployeeOnDuty(s.Clock.Now), "进货员不能替代收银值守");
                Assert.True(s.ScheduleEmployeeTransfer(e.Id, "tea"));
                int entries = s.Ledger.Entries.Count(x => x.Category == "工资");
                Midnight(s);
                Assert.AreEqual(entries + 1, s.Ledger.Entries.Count(x => x.Category == "工资"));
                Assert.AreEqual("tea", s.FindEmployee(e.Id).Shop.Place.Id);
                Midnight(s);
                Assert.AreEqual(entries + 2, s.Ledger.Entries.Count(x => x.Category == "工资"));
            }
        }

        [Test]
        public void DailyPlan_OnlyDispatchesOncePerDay_AndPreservesFlagInJson()
        {
            using (var s = Make())
            {
                var e = Worker(); s.MainShop.Buyer = e; CityTestPeople.AtAssignedWork(s,e);
                var plan = Plan(); plan.Daily = true;
                Assert.True(s.ConfigureEmployeePurchase(e.Id, plan));
                s.AdvanceClock(1);
                Assert.AreEqual(1, s.EmployeePurchaseTrips.Count);
                using (var loaded = RoundTrip(s))
                {
                    Assert.True(loaded.FindEmployee(e.Id).Person.PurchasePlan.Daily);
                    var trip = loaded.EmployeePurchaseTrips.Single();
                    loaded.AdvanceClock((int)(Math.Max(trip.ReturnAt, trip.DeliveryAt) - loaded.Clock.Now.TotalMinutes + 2));
                    Assert.Zero(loaded.EmployeePurchaseTrips.Count);
                    loaded.AdvanceClock(10);
                    Assert.Zero(loaded.EmployeePurchaseTrips.Count, "同一天不自动重复购买");
                }
            }
        }

        [Test]
        public void InboundProcurement_ReservesOnlyDestinationSpace_AndRedirectsWhenShopCloses()
        {
            using (var s = Make(true))
            {
                var e = Worker(); var closing = s.ShopAt("tea"); closing.Buyer = e; CityTestPeople.AtAssignedWork(s,e);
                Assert.True(s.ConfigureEmployeePurchase(e.Id, Plan()));
                Assert.True(s.DispatchEmployeePurchase(e.Id));
                GameSessionTests.Teleport(s, "tea");
                Assert.GreaterOrEqual(s.IncomingShopSpace, 2);
                Assert.AreEqual(2, s.PendingOnlineCount("cpu0"));
                Assert.IsNull(s.CloseShopBlocker(closing));
                var trip = s.EmployeePurchaseTrips.Single();
                Assert.True(s.CloseShop("tea"));
                Assert.AreEqual(EmployeeRole.Unassigned, s.FindEmployee(e.Id).Role);
                Assert.True(trip.DeliverHome);
                GameSessionTests.Teleport(s, "pc");
                Assert.Zero(s.IncomingShopSpace);
                Assert.Zero(s.PendingOnlineCount("cpu0"));
                var part = PartCatalog.Get("cpu0");
                int initial = s.HomeBoxParts.CountAll(part) + s.HomeTempParts.CountAll(part);
                using var loaded = RoundTrip(s);
                var pending = loaded.EmployeePurchaseTrips.Single();
                loaded.AdvanceClock((int)(Math.Max(pending.ReturnAt, pending.DeliveryAt) - loaded.Clock.Now.TotalMinutes + 1));
                Assert.That(loaded.HomeBoxParts.CountAll(part) + loaded.HomeTempParts.CountAll(part), Is.EqualTo(initial + 2));
            }
        }

        [Test]
        public void PlanningOutsideBusinessHours_StillShowsFullCostsWithoutDispatching()
        {
            using (var s = Make())
            {
                var e = Worker(); s.MainShop.Buyer = e; CityTestPeople.AtAssignedWork(s,e);
                Assert.True(s.ConfigureEmployeePurchase(e.Id, Plan()));
                var daytime = s.QuoteEmployeePurchase(e.Id);
                Assert.IsNull(daytime.Error);
                s.Clock.Restore(s.Clock.Today, 22 * 60);
                var night = s.QuoteEmployeePurchase(e.Id);
                Assert.IsNotNull(night.Error);
                var planning=s.EstimateEmployeePurchase(e.Id);
                Assert.IsNull(planning.Error,"planning a future shopping list does not dispatch an off-duty employee");
                Assert.AreEqual(daytime.Total,planning.Total);
                Assert.AreEqual(daytime.Goods, night.Goods);
                Assert.AreEqual(daytime.Travel, night.Travel);
                Assert.AreEqual(daytime.Freight, night.Freight);
                Assert.AreEqual(daytime.Total, night.Total);
                Assert.False(s.DispatchEmployeePurchase(e.Id));
            }
        }

        [Test]
        public void CandidateCanBeHiredAsBuyer_AndAppearsInCompanyRoster()
        {
            using (var s = Make())
            {
                GameSessionTests.PostFirstRecruitmentNotice(s);
                s.Clock.Restore(new GameDate(1), 8 * 60 + 59);
                s.AdvanceClock(2);
                Assert.Greater(s.Candidates.Count, 0);
                string candidateId = s.Candidates[0].Id;
                Assert.True(s.HireBuyer(0));
                var employee = s.FindEmployee(candidateId);
                Assert.NotNull(employee);
                Assert.AreEqual(EmployeeRole.Buyer, employee.Role);
                Assert.AreEqual("pc", employee.Shop.Place.Id);
                Assert.IsNull(s.Employee);
                Assert.IsNull(s.Assembler);
                Assert.False(s.IsEmployeeOnDuty(s.Clock.Now));
                using (var loaded = RoundTrip(s)) Assert.AreEqual(candidateId, loaded.MainShop.Buyer.Id);
            }
        }
    }
}
