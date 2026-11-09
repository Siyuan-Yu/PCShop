using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using System;

namespace PCShopSell.Tests
{
    public class InteractionFlowTests
    {
        private static GameSession Make() => GameSessionTests.Make(500000, 9 * 60);

        [Test]
        public void ChosenStall_FiltersCatalogWithoutRemovingOtherSourcesFromPlan()
        {
            using (var s = Make())
            {
                var plan = new EmployeePurchasePlan { BudgetYuan = 8000, Daily = true,
                    Items = new[] { new PurchaseLine { PartId = "cpu0", Count = 3 }, new PurchaseLine { PartId = "ram0", Count = 7 } } };
                var original = plan.Copy();
                var parts = s.EmployeePurchaseParts("stall");
                Assert.IsTrue(parts.All(p => s.StallSells(s.City.GetPlace("stall"), p)));
                Assert.IsFalse(parts.Any(p => p.Id == "ram0"));
                Assert.AreEqual(0, s.ChangePurchaseSource(plan, "stall"));
                Assert.IsNull(plan.SourceId);
                Assert.AreEqual(2, plan.Items.Length);
                Assert.AreEqual(3, plan.Items.Single(x => x.PartId == "cpu0").Count);
                Assert.AreEqual(7, plan.Items.Single(x => x.PartId == "ram0").Count);
                Assert.AreEqual(8000, plan.BudgetYuan);
                Assert.IsTrue(plan.Daily);
                Assert.AreEqual(2, original.Items.Length, "不能改动原始已保存清单");
                Assert.AreEqual(0, s.ChangePurchaseSource(plan, "stall"), "换货源只筛选目录，不删除清单");
            }
        }

        [Test]
        public void WholeRowDrag_FillsOnlyAvailableBagCapacity_AndKeepsRemainderInCart()
        {
            using (var s = Make())
            {
                GameSessionTests.Teleport(s, "stall");
                var part = PartCatalog.Get("cpu0");
                int count = Math.Min(10, s.StallRemaining(s.CurrentPlace, part));
                Assert.True(s.CartAddStall(s.CurrentPlace, part, count));
                s.CartAllToTruck();
                Assert.True(s.CartWholeRowToBag("part:cpu0"));
                var line = s.Cart.Single();
                Assert.AreEqual(count, line.InBag + line.ByTruck);
                Assert.LessOrEqual(s.CartBagKg, s.FreeKg);
                Assert.Greater(line.InBag, 1, "拖动整行必须能转移多件");
                Assert.Greater(line.ByTruck, 0, "超容量的留给货车");
            }
        }

        [Test]
        public void MarketCartCombinesFormerCountersAtOnePriceAndUsesSharedStock()
        {
            var layout = GameSessionTests.Layout();
            layout.places = layout.places.Append(new LayoutPlaceData
            {
                id = "market_counter", name = "显卡档口", kind = "wholesaler", node = "mk",
                sells = "Cpu,Gpu,Memory", model = "electronics_counter",
            }).ToArray();
            using (var s = new GameSession(CityLayout.FromData(layout), 7, Money.FromYuan(500000),
                       GameTimestamp.From(new GameDate(0), GameSession.WholesalerCloseMinute - 1)))
            {
                GameSessionTests.Teleport(s, "stall");
                var market = s.CurrentPlace;
                var counter = s.City.GetPlace("market_counter");
                Assert.False(s.City.PublicPlaces.Contains(counter));
                CollectionAssert.AreEquivalent(new[] { market, counter }, s.WholesaleStallsAt(market));
                var cpu = PartCatalog.Get("cpu0");
                var memory = PartCatalog.Get("ram0");
                int marketStock = s.StallRemaining(market, cpu);
                Assert.AreEqual(marketStock, s.StallRemaining(counter, cpu));
                Assert.AreEqual(s.StallPrice(market, cpu), s.StallPrice(counter, cpu));

                Assert.True(s.CartAddStall(market, cpu, 1));
                Assert.True(s.CartAddStall(counter, cpu, 2));
                Assert.True(s.CartAddStall(counter, memory, 1));
                Assert.AreEqual(2, s.Cart.Count);
                s.CartAllToTruck();
                Assert.True(s.Checkout(true));
                Assert.AreEqual(marketStock - 3, s.StallRemaining(market, cpu));
                Assert.AreEqual(marketStock - 3, s.StallRemaining(counter, cpu));
                Assert.AreEqual(3, s.Couriers.SelectMany(run => run.Items).Where(item => item.Id == cpu.Id).Sum(item => item.Count));
                Assert.AreEqual(1, s.Couriers.SelectMany(run => run.Items).Where(item => item.Id == memory.Id).Sum(item => item.Count));
            }
        }

        [Test]
        public void ReplaceEmployee_ValidatesBeforeDisplacing_AndKeepsBothPeople()
        {
            using (var s = Make())
            {
                s.EventsEnabled = false;
                var a = new Employee { Name = "甲", DailyWageYuan = 100 };
                var b = new Employee { Name = "乙", DailyWageYuan = 150 };
                s.MainShop.Employee = a; s.MainShop.Assembler = b;
                Assert.False(s.ReplaceEmployeeWork(a.Id, "missing", EmployeeRole.Assembler));
                Assert.AreSame(b, s.MainShop.Assembler);
                Assert.True(s.ReplaceEmployeeWork(a.Id, s.MainShop.Place.Id, EmployeeRole.Assembler));
                Assert.AreSame(a, s.MainShop.Assembler);
                Assert.AreEqual(EmployeeRole.Unassigned, s.FindEmployee(b.Id).Role);
                Assert.AreEqual(250, s.Employees.Sum(x => x.Person.DailyWageYuan));
                using (var loaded = GameSession.Load(s.City, s.CreateSave()))
                {
                    Assert.AreEqual(2, loaded.Employees.Count);
                    Assert.AreEqual(EmployeeRole.Unassigned, loaded.FindEmployee(b.Id).Role);
                }
            }
        }

        [Test]
        public void ShiftSelection_IsDirect_AndRejectsInvalidTimes()
        {
            using (var s = Make())
            {
                var e = new Employee(); s.MainShop.Employee = e;
                Assert.True(s.SetEmployeeShift(e.Id, 780, 1260));
                Assert.AreEqual(540, e.ShiftStart,"An already active shift keeps its start");
                Assert.AreEqual(780,e.Schedule.PendingStart);
                Assert.True(s.SetEmployeeShift(e.Id, 1260, 540),"Overnight custom hours are valid");
                Assert.AreEqual(1260,e.Schedule.PendingStart);
                Assert.False(s.SetEmployeeShift(e.Id,540,540));
            }
        }

        [Test]
        public void ShopEditing_RequiresRenovationAndClosesShopForTheDay()
        {
            using (var s = Make())
            {
                GameSessionTests.Teleport(s, s.MainShop.Place.Id);
                Assert.False(s.CanEditShop);
                Assert.True(s.BeginRenovation());
                Assert.True(s.CanEditShop); Assert.True(s.IsRenovating);
                Assert.True(s.PlayerShop.ForcedClosed);
                s.Clock.Restore(s.Clock.Today.AddDays(1), 9 * 60);
                Assert.False(s.CanEditShop);
            }
        }

        [Test]
        public void RecruitWithAssignment_RejectsOccupiedRole_WithoutHiringOrRemovingCandidate()
        {
            using (var s = Make())
            {
                s.EventsEnabled = false;
                GameSessionTests.Teleport(s, s.MainShop.Place.Id);
                Assert.True(s.PostJobAd());
                s.Clock.Restore(s.Clock.Today.AddDays(1), 8 * 60 + 59);
                s.AdvanceClock(2);
                var candidate = s.Candidates.First();
                int count = s.Candidates.Count;
                s.MainShop.Employee = new Employee();
                Assert.False(s.RecruitAndAssign(candidate.Id, s.MainShop.Place.Id, EmployeeRole.Cashier));
                Assert.AreEqual(count, s.Candidates.Count);
                Assert.IsNull(s.FindEmployee(candidate.Id));
                Assert.True(s.RecruitAndAssign(candidate.Id, s.MainShop.Place.Id, EmployeeRole.Buyer));
                Assert.AreEqual(count - 1, s.Candidates.Count);
                Assert.AreEqual(EmployeeRole.Buyer, s.FindEmployee(candidate.Id).Role);
            }
        }

        [Test]
        public void LeaseCannotRemoveLockerOrTemporaryGoods()
        {
            using (var s = Make())
            {
                s.PlayerShop.Reputation = 80;
                s.DebugAddListing("tea", 0, 60, Money.FromYuan(3000));
                GameSessionTests.Teleport(s, "agency");
                Assert.True(s.SignLease("tea", true));
                GameSessionTests.Teleport(s, "tea");
                var shop = s.ShopAt("tea");
                shop.Locker.AddGift(Gifts.Cig, 1);
                StringAssert.Contains("纸箱", s.CloseShopBlocker(shop));
                Assert.False(s.CloseShop("tea"));
                shop.Locker.TakeGift(Gifts.Cig);
                shop.Temp.AddGift(Gifts.Cig, 1);
                StringAssert.Contains("临时存放", s.CloseShopBlocker(shop));
                Assert.False(s.CloseShop("tea"));
                Assert.AreEqual(1, shop.Temp.GiftCount(Gifts.Cig));
            }
        }
    }
}
