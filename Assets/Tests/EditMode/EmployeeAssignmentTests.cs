using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class EmployeeAssignmentTests
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
        private static Employee Person() => new Employee { Name = "小王", DailyWageYuan = 130, CheckoutMinutes = 5, AssemblyMinutes = 130, PurchaseMinutes = 6 };
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void Tomorrow(GameSession s)
        {
            s.Clock.Restore(s.Clock.Today, 23 * 60 + 59);
            s.AdvanceClock(2);
        }

        [Test]
        public void RecruitHiresPersonWithoutPermanentOccupation_ThenAnyJobCanBeAssigned()
        {
            using (var s = Make())
            {
                Assert.True(s.PostJobAd());
                s.Clock.Restore(new GameDate(1), 8 * 60 + 59);
                s.AdvanceClock(2);
                int candidates = s.Candidates.Count;
                var employee = s.RecruitEmployee(0);
                Assert.NotNull(employee);
                Assert.AreEqual(candidates - 1, s.Candidates.Count);
                var row = s.FindEmployee(employee.Id);
                Assert.AreEqual(EmployeeRole.Unassigned, row.Role);
                Assert.IsNull(row.Shop);
                Assert.True(s.ArrangeEmployeeWork(employee.Id, "pc", EmployeeRole.Assembler));
                Assert.AreSame(employee, s.MainShop.Assembler, "没有装机职业或入职培训也能安排");
            }
        }

        [Test]
        public void SameEmployeeCanDoAllJobs_WithoutLosingSkillsExperienceWageOrPurchasePlan()
        {
            using (var s = Make())
            {
                var e = Person(); e.BuildsCompleted = 11; e.PurchasePlan.BudgetYuan = 7654;
                s.MainShop.Employee = e;
                var cash = s.Ledger.Balance;
                foreach (var role in new[] { EmployeeRole.Assembler, EmployeeRole.Buyer, EmployeeRole.Cashier })
                {
                    Assert.True(s.ArrangeEmployeeWork(e.Id, "pc", role));
                    Assert.AreEqual(role, s.FindEmployee(e.Id).Role);
                    Assert.AreEqual(1, s.Employees.Count);
                    Assert.AreEqual(130, e.DailyWageYuan);
                    Assert.AreEqual(130, e.AssemblyMinutes);
                    Assert.AreEqual(6, e.PurchaseMinutes);
                    Assert.AreEqual(11, e.BuildsCompleted);
                    Assert.AreEqual(7654, e.PurchasePlan.BudgetYuan);
                    Assert.AreEqual(cash, s.Ledger.Balance, "换岗不能自动收培训费或改工资");
                }
            }
        }

        [Test]
        public void CrossShopAndJobTogether_TakeEffectTomorrow_AndSurviveJson()
        {
            using (var s = Make(true))
            {
                var e = Person(); s.MainShop.Employee = e;
                Assert.True(s.ArrangeEmployeeWork(e.Id, "tea", EmployeeRole.Buyer));
                Assert.AreSame(e, s.MainShop.Employee);
                Assert.IsNull(s.ShopAt("tea").Buyer);
                using (var loaded = Reload(s))
                {
                    Assert.AreEqual(EmployeeRole.Cashier, loaded.FindEmployee(e.Id).Role);
                    Tomorrow(loaded);
                    var row = loaded.FindEmployee(e.Id);
                    Assert.AreEqual("tea", row.Shop.Place.Id);
                    Assert.AreEqual(EmployeeRole.Buyer, row.Role);
                    Assert.AreEqual(130, row.Person.DailyWageYuan);
                    Assert.AreEqual(130, row.Person.AssemblyMinutes);
                    Assert.IsNull(loaded.MainShop.Employee);
                    Assert.AreEqual(1, loaded.Employees.Count);
                }
            }
        }

        [Test]
        public void StandbyRetainsPersonPaysWages_AndCanWorkAgainAfterJson()
        {
            using (var s = Make())
            {
                var e = Person(); s.MainShop.Buyer = e;
                Assert.True(s.ArrangeEmployeeWork(e.Id, null, EmployeeRole.Unassigned));
                Assert.IsNull(s.MainShop.Buyer);
                using (var loaded = Reload(s))
                {
                    Assert.AreEqual(EmployeeRole.Unassigned, loaded.FindEmployee(e.Id).Role);
                    int wages = loaded.Ledger.Entries.Count(x => x.Category == "工资");
                    Tomorrow(loaded);
                    Assert.AreEqual(wages + 1, loaded.Ledger.Entries.Count(x => x.Category == "工资"));
                    Assert.True(loaded.ArrangeEmployeeWork(e.Id, "pc", EmployeeRole.Assembler));
                    Assert.AreEqual(1, loaded.Employees.Count);
                    Assert.NotNull(loaded.MainShop.Assembler);
                }
            }
        }

        [Test]
        public void OccupiedOrReservedJobCannotBeOverwritten()
        {
            using (var s = Make(true))
            {
                var a = Person(); var b = Person(); b.Name = "小李";
                s.MainShop.Employee = a; s.ShopAt("tea").Assembler = b;
                Assert.False(s.ArrangeEmployeeWork(a.Id, "tea", EmployeeRole.Assembler));
                Assert.True(s.ArrangeEmployeeWork(b.Id, "tea", EmployeeRole.Buyer));
                Assert.True(s.ArrangeEmployeeWork(a.Id, "tea", EmployeeRole.Assembler));
                Assert.False(s.ArrangeEmployeeWork(b.Id, "tea", EmployeeRole.Assembler), "明天已有别人调入，不能抢占");
                Assert.AreEqual(2, s.Employees.Count);
            }
        }

        [Test]
        public void CancelWorkPlan_DoesNotChangeTodaysJobOrTomorrow()
        {
            using (var s = Make(true))
            {
                var e = Person(); s.MainShop.Employee = e;
                Assert.True(s.ArrangeEmployeeWork(e.Id, "tea", EmployeeRole.Assembler));
                s.CancelEmployeeTransfer(e.Id);
                Tomorrow(s);
                Assert.AreSame(e, s.MainShop.Employee);
                Assert.AreEqual(-1, e.TransferRole);
                Assert.IsNull(s.ShopAt("tea").Assembler);
            }
        }

        [Test]
        public void UntrainedSlowEmployeeCanActuallyAssemble_AfterReassignment()
        {
            using (var s = Make())
            {
                var e = Person(); s.MainShop.Employee = e;
                Assert.False(e.AssemblyTrained);
                Assert.True(s.ArrangeEmployeeWork(e.Id, "pc", EmployeeRole.Assembler));
                var order = new BuildOrder(1, 0, Money.FromYuan(3000), s.Clock.Now, s.Clock.Now.AddMinutes(600));
                s.PlayerShop.RestoreOrders(new[] { order }, 2);
                foreach (var part in PartCatalog.BuildList(0)) s.PlayerShop.Stock.ForceAdd(part, true, 1);
                s.AdvanceClock(10);
                Assert.IsTrue(order.PartsReserved);
                Assert.Greater(order.EmployeeMinutesLeft, 90, "不擅长只影响耗时，不能拒绝工作");
                s.AdvanceClock(140);
                Assert.AreEqual(0, s.PlayerShop.Orders.Count);
                Assert.AreEqual(1, e.BuildsCompleted);
            }
        }

        [Test]
        public void BuyerOnTripCannotImmediatelyChangeJobOrReleaseVehicle()
        {
            using (var s = Make())
            {
                var e = Person(); s.MainShop.Buyer = e;
                Assert.True(s.ConfigureEmployeePurchase(e.Id, new EmployeePurchasePlan
                {
                    BudgetYuan = 3000, Transport = VehicleKind.Taxi,
                    Items = new[] { new PurchaseLine { PartId = "cpu0", Count = 1 } }
                }));
                Assert.True(s.DispatchEmployeePurchase(e.Id));
                Assert.False(s.ArrangeEmployeeWork(e.Id, "pc", EmployeeRole.Cashier));
                Assert.False(s.ArrangeEmployeeWork(e.Id, null, EmployeeRole.Unassigned));
                Assert.AreSame(e, s.MainShop.Buyer);
            }
        }
    }
}
