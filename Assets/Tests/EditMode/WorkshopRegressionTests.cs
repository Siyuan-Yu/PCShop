using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Core.Finance;

namespace PCShopSell.Tests
{
    public class WorkshopRegressionTests
    {
        private static GameSession Make()
        {
            var s = new GameSession(CityLayout.FromData(GameSessionTests.Layout()), 91, Money.FromYuan(50000),
                GameTimestamp.From(new GameDate(0), 9 * 60), false);
            s.EventsEnabled = false;
            s.PlayerShop.Settings.BaseCustomersPerHour = 0;
            GameSessionTests.Teleport(s, "pc");
            return s;
        }
        private static BuildOrder Order(GameSession s, int dueMinutes = 600)
        {
            var order = new BuildOrder(1, 0, Money.FromYuan(3000), s.Clock.Now, s.Clock.Now.AddMinutes(dueMinutes));
            s.PlayerShop.RestoreOrders(new[] { order }, 2);
            foreach (var part in PartCatalog.BuildList(0)) s.PlayerShop.Stock.ForceAdd(part, true, 1);
            return order;
        }
        [Test]
        public void ReservationPreservesWealth_AndSurvivesSaveLoad()
        {
            using (var s = Make())
            {
                var order = Order(s);
                var worth = s.NetWorth();
                Assert.IsTrue(s.PlayerShop.ReserveBuild(order.Id));
                Assert.AreEqual(worth, s.NetWorth());
                Assert.IsTrue(s.PlayerShop.Stock.IsEmpty);
                using (var loaded = GameSession.Load(s.City, s.CreateSave()))
                {
                    Assert.IsTrue(loaded.PlayerShop.Orders[0].PartsReserved);
                    Assert.AreEqual(worth, loaded.NetWorth());
                    Assert.IsTrue(loaded.PlayerShop.CancelBuild(order.Id));
                    Assert.AreEqual(worth, loaded.NetWorth());
                    Assert.AreEqual(6, loaded.PlayerShop.Stock.Lines().Sum(x => x.Count));
                }
            }
        }
        [Test]
        public void LateOrderCannotBePaidBeforeAssemblyTime()
        {
            using (var s = Make())
            {
                var order = Order(s, 10);
                var balance = s.Ledger.Balance;
                Assert.IsFalse(s.CompleteBuild(order.Id, 0));
                Assert.AreEqual(balance, s.Ledger.Balance);
                Assert.AreEqual(6, s.PlayerShop.Stock.Lines().Sum(x => x.Count));
            }
        }
        [Test]
        public void CompletedAssemblyPaysAfterElapsedTime_AndConsumesOnlyOnce()
        {
            using (var s = Make())
            {
                var order = Order(s);
                long start = s.Clock.Now.TotalMinutes;
                var balance = s.Ledger.Balance;
                Assert.IsTrue(s.CompleteBuild(order.Id, 0));
                Assert.Greater(s.Clock.Now.TotalMinutes, start);
                Assert.AreEqual(balance + order.Price, s.Ledger.Balance);
                Assert.IsFalse(s.CompleteBuild(order.Id, 0));
            }
        }
        [Test]
        public void DisplayAssemblyDoesNotEraseAssetValue()
        {
            using (var s = Make())
            {
                Order(s);
                s.PlayerShop.RestoreOrders(new BuildOrder[0], 2);
                var worth = s.NetWorth();
                Assert.IsTrue(s.PlayerShop.AssembleForDisplay(0));
                Assert.AreEqual(worth, s.NetWorth());
            }
        }
        [Test]
        public void CashFlowIncludesDepositRefundWithoutInflatingRevenue()
        {
            var ledger = new Ledger(Money.Zero);
            var date = new GameDate(0);
            ledger.Credit(date, "押金", Money.FromYuan(1500));
            ledger.Credit(date, "零售", Money.FromYuan(200));
            ledger.ForceDebit(date, "进货", Money.FromYuan(100));
            var report = FinanceReport.Daily(ledger, date, 1)[0];
            Assert.AreEqual(Money.FromYuan(200), report.Revenue);
            Assert.AreEqual(Money.FromYuan(1600), report.CashChange);
        }
        [Test]
        public void EmployeeCanAssembleWhileOwnerIsPresent_AndResumeAfterLoad()
        {
            using (var s = Make())
            {
                var save = s.CreateSave();
                save.hasEmployee = true;
                save.employee = new SaveEmployee { name = "小王", wage = 120, checkoutMinutes = 3 };
                using (var staffed = GameSession.Load(s.City, save))
                {
                    Assert.IsTrue(staffed.TrainEmployee());
                    Order(staffed);
                    staffed.PlayerShop.Settings.BaseCustomersPerHour = 0;
                    staffed.AdvanceClock(30);
                    Assert.Greater(staffed.PlayerShop.Orders[0].EmployeeMinutesLeft, 0);
                    using (var loaded = GameSession.Load(s.City, staffed.CreateSave()))
                    {
                        loaded.EventsEnabled = false;
                        loaded.PlayerShop.Settings.BaseCustomersPerHour = 0;
                        var balance = loaded.Ledger.Balance;
                        loaded.AdvanceClock(70);
                        Assert.AreEqual(0, loaded.PlayerShop.Orders.Count);
                        Assert.AreEqual(balance + Money.FromYuan(3000), loaded.Ledger.Balance);
                    }
                }
            }
        }
        [Test]
        public void BudgetLimitedRestockPurchasesAffordableSubset()
        {
            using (var s = Make())
            {
                s.RestockReserveDays = 0;
                s.RestockDailyBudgetYuan = 2000;
                s.SetRestockTarget("ram0", 30);
                s.RunAutoRestock();
                Assert.AreEqual(1, s.Online.Pending.Count);
                Assert.LessOrEqual(s.Online.Pending[0].TotalCents, 200000);
                Assert.Greater(s.Online.Pending[0].Lines.Sum(x => x.Count), 0);
                Assert.Less(s.Online.Pending[0].Lines.Sum(x => x.Count), 30);
            }
        }
        [Test]
        public void LayoutUndoRedoRestoresDisplayPartsAndFurnitureWithoutDuplication()
        {
            using (var s = Make())
            {
                s.PlayerShop.RestoreDisplay(new[] { 0 });
                Assert.IsTrue(s.BeginRenovation());
                s.Time.Hold(); s.BeginLayoutHistory(true);
                int desk = s.ShopLayout.ToList().FindIndex(p => p.Id == PCShopSell.Core.Home.FurnitureCatalog.ShopDisplay);
                int parts = s.PlayerShop.Stock.Lines().Sum(l => l.Count);
                int furniture = s.FurnitureInStock(PCShopSell.Core.Home.FurnitureCatalog.ShopDisplay);
                Assert.IsTrue(s.StoreFurniture(true, desk));
                Assert.AreEqual(0, s.PlayerShop.DisplayPcs.Count);
                Assert.IsTrue(s.UndoLayout());
                Assert.AreEqual(1, s.PlayerShop.DisplayPcs.Count);
                Assert.AreEqual(parts, s.PlayerShop.Stock.Lines().Sum(l => l.Count));
                Assert.AreEqual(furniture, s.FurnitureInStock(PCShopSell.Core.Home.FurnitureCatalog.ShopDisplay));
                Assert.IsTrue(s.RedoLayout());
                Assert.AreEqual(0, s.PlayerShop.DisplayPcs.Count);
                Assert.AreEqual(parts + 6, s.PlayerShop.Stock.Lines().Sum(l => l.Count));
                Assert.AreEqual(furniture + 1, s.FurnitureInStock(PCShopSell.Core.Home.FurnitureCatalog.ShopDisplay));
                s.EndLayoutHistory(); s.Time.Release();
                Assert.IsFalse(s.UndoLayout());
            }
        }
        [Test]
        public void AssemblerRespectsShiftAndKeepsExperienceAcrossSave()
        {
            using (var s = Make())
            {
                var data = s.CreateSave();
                data.hasAssembler = true; data.assembler = new SaveEmployee { name = "装机员", wage = 180, checkoutMinutes = 3, shiftStart = 10 * 60, shiftEnd = 17 * 60, buildsCompleted = 4 };
                using (var staffed = GameSession.Load(s.City, data))
                {
                    staffed.Flags.Add("employee:auto-build"); staffed.EventsEnabled = false;
                    staffed.PlayerShop.Settings.BaseCustomersPerHour = 0;
                    var order = Order(staffed);
                    staffed.AdvanceClock(59);
                    Assert.AreEqual(0, order.EmployeeMinutesLeft);
                    staffed.AdvanceClock(5);
                    Assert.Greater(order.EmployeeMinutesLeft, 0);
                    using (var loaded = GameSession.Load(s.City, staffed.CreateSave()))
                    {
                        Assert.AreEqual(600, loaded.Assembler.ShiftStart);
                        Assert.AreEqual(1020, loaded.Assembler.ShiftEnd);
                        loaded.EventsEnabled = false; loaded.PlayerShop.Settings.BaseCustomersPerHour = 0;
                        loaded.AdvanceClock(90);
                        Assert.AreEqual(0, loaded.PlayerShop.Orders.Count);
                        Assert.AreEqual(5, loaded.Assembler.BuildsCompleted);
                    }
                }
            }
        }
        [Test]
        public void FiringCashierDoesNotResetDedicatedAssembly()
        {
            using (var s = Make())
            {
                var data = s.CreateSave(); data.hasEmployee = true;
                data.employee = new SaveEmployee { name = "收银", wage = 120, checkoutMinutes = 2 };
                data.hasAssembler = true; data.assembler = new SaveEmployee { name = "装机", wage = 180, checkoutMinutes = 3 };
                using (var staffed = GameSession.Load(s.City, data))
                {
                    staffed.Flags.Add("employee:auto-build"); staffed.EventsEnabled = false;
                    staffed.PlayerShop.Settings.BaseCustomersPerHour = 0;
                    var order = Order(staffed); staffed.AdvanceClock(10);
                    int left = order.EmployeeMinutesLeft;
                    Assert.Greater(left, 0); staffed.Fire();
                    Assert.AreEqual(left, order.EmployeeMinutesLeft);
                    Assert.IsTrue(staffed.IsEmployeeOnDuty(staffed.Clock.Now));
                }
            }
        }
        [Test]
        public void JsonRoundTripDoesNotInventUnhiredAssembler()
        {
            using (var s = Make())
            {
                var data = UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(s.CreateSave()));
                using (var loaded = GameSession.Load(s.City, data))
                {
                    Assert.IsNull(loaded.Assembler);
                    loaded.EventsEnabled = false;
                    loaded.Clock.Restore(loaded.Clock.Today, 23 * 60 + 59);
                    Assert.DoesNotThrow(() => loaded.AdvanceClock(2));
                }
            }
        }
        [Test]
        public void OptInMealDeliveryPaysOnlyWhenNoPackedLunchIsAvailable()
        {
            using (var s = Make())
            {
                s.SetSmartphone(true); s.Backpack.Food.Packed.Clear(); s.Player.Hunger = 30;
                s.ToggleMealDelivery();
                s.Clock.Restore(s.Clock.Today, 11 * 60 + 59);
                var before = s.Ledger.Balance;
                s.AdvanceClock(2);
                Assert.AreEqual(before - Money.FromYuan(25), s.Ledger.Balance);
                Assert.Greater(s.Player.Hunger, 70);
                using (var loaded = GameSession.Load(s.City, s.CreateSave())) Assert.IsTrue(loaded.MealDeliveryEnabled);
            }
        }
    }
}
