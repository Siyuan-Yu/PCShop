using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class PurchasePlanTests
    {
        private static GameSession Make()
        {
            var city = CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s = new GameSession(city, 7, Money.FromYuan(500000), GameTimestamp.From(new GameDate(0), 9 * 60));
            s.EventsEnabled = false;
            s.MainShop.Sim.Settings.BaseCustomersPerHour = 0;
            s.MainShop.Buyer = new Employee { Name = "小陈", DailyWageYuan = 150, CheckoutMinutes = 3 };
            return s;
        }
        private static EmployeePurchasePlan Mixed(GameSession s)
        {
            var offers = s.EmployeePurchaseCatalog();
            return new EmployeePurchasePlan
            {
                BudgetYuan = 1, Transport = VehicleKind.Taxi,
                Items = new[] { offers.First(o => o.Id == "cpu0" && o.Kind == PurchaseGoodsKind.Part).Line(1),
                    offers.First(o => o.Id == "ram0" && o.Kind == PurchaseGoodsKind.Part).Line(1),
                    offers.First(o => o.Id == Gifts.Drink && o.Kind == PurchaseGoodsKind.Gift).Line(2) }
            };
        }

        [Test]
        public void MixedSources_AutoCostIgnoresOldBudget_AndShipsAllItemsExactlyOnceAfterJson()
        {
            using (var s = Make())
            {
                string id = s.MainShop.Buyer.Id;
                var plan = Mixed(s);
                Assert.AreEqual(3, plan.Items.Select(x => x.SourceId).Distinct().Count());
                Assert.True(s.ConfigureEmployeePurchase(id, plan));
                var q = s.QuoteEmployeePurchase(id);
                Assert.IsNull(q.Error, q.Error);
                Assert.Greater(q.Total.Cents, 100, "旧存档的一元预算不能拦住实际采购");
                var cash = s.Ledger.Balance;
                int cpu = s.MainShop.Sim.Stock.CountAll(PartCatalog.Get("cpu0"));
                int ram = s.MainShop.Sim.Stock.CountAll(PartCatalog.Get("ram0"));
                int drinks = s.MainShop.Sim.Stock.GoodsCount(Gifts.Drink);
                Assert.True(s.DispatchEmployeePurchase(id));
                Assert.AreEqual(cash - q.Total, s.Ledger.Balance);
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    var trip = loaded.EmployeePurchaseTrips.Single();
                    Assert.AreEqual(3, trip.Items.Select(x => x.SourceId).Distinct().Count());
                    loaded.AdvanceClock((int)(Math.Max(trip.DeliveryAt, trip.ReturnAt) - loaded.Clock.Now.TotalMinutes + 1));
                    Assert.AreEqual(cpu + 1, loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get("cpu0")));
                    Assert.AreEqual(ram + 1, loaded.MainShop.Sim.Stock.CountAll(PartCatalog.Get("ram0")));
                    Assert.AreEqual(drinks + 2, loaded.MainShop.Sim.Stock.GoodsCount(Gifts.Drink));
                    Assert.Zero(loaded.EmployeePurchaseTrips.Count);
                    var paid = loaded.Ledger.Balance;
                    loaded.AdvanceClock(5);
                    Assert.AreEqual(paid, loaded.Ledger.Balance);
                    Assert.AreEqual(drinks + 2, loaded.MainShop.Sim.Stock.GoodsCount(Gifts.Drink));
                }
            }
        }

        [Test]
        public void Complete_EnablesDailyPlanAndRepeatsTheWholeListOnAnotherDay()
        {
            using (var s = Make())
            {
                var buyer = s.MainShop.Buyer;
                Assert.True(s.CompleteEmployeePurchase(buyer.Id, Mixed(s)));
                Assert.True(buyer.PurchasePlan.Daily);
                var cash = s.Ledger.Balance;
                s.AdvanceClock(1);
                Assert.Zero(s.EmployeePurchaseTrips.Count, "完成当天只保存，不出发");
                Assert.AreEqual(cash, s.Ledger.Balance);
                Assert.False(s.DispatchEmployeePurchase(buyer.Id), "不能绕过次日生效规则");
                s.Clock.Restore(new GameDate(1), 8 * 60 + 59); s.AdvanceClock(2);
                var first = s.EmployeePurchaseTrips.Single();
                Assert.AreEqual(3, first.Items.Length);
                s.AdvanceClock((int)(Math.Max(first.DeliveryAt, first.ReturnAt) - s.Clock.Now.TotalMinutes + 1));
                s.AdvanceClock(5);
                Assert.Zero(s.EmployeePurchaseTrips.Count, "同一天只自动尝试一次");
                s.Clock.Restore(new GameDate(2), 8 * 60 + 59);
                s.AdvanceClock(2);
                Assert.AreEqual(3, s.EmployeePurchaseTrips.Single().Items.Length, "次日仍按整份多源清单采购");
                Assert.True(s.CompleteEmployeePurchase(buyer.Id, new EmployeePurchasePlan()));
                Assert.False(buyer.PurchasePlan.Daily, "清空后完成应停止自动采购，已出发的货不受影响");
                Assert.AreEqual(3, s.EmployeePurchaseTrips.Single().Items.Length);
            }
        }

        [Test]
        public void CatalogIncludesAllExistingPhysicalGoods_AndFurnitureFoodReachStorage()
        {
            using (var s = Make())
            {
                var offers = s.EmployeePurchaseCatalog();
                foreach (PurchaseGoodsKind kind in Enum.GetValues(typeof(PurchaseGoodsKind))) Assert.True(offers.Any(o => o.Kind == kind), kind.ToString());
                var food = offers.First(o => o.Kind == PurchaseGoodsKind.Ingredient);
                var rice = offers.First(o => o.Kind == PurchaseGoodsKind.Rice);
                var furniture = offers.First(o => o.Kind == PurchaseGoodsKind.Furniture && o.Id == FurnitureCatalog.HomeBox);
                var plan = new EmployeePurchasePlan { Transport = VehicleKind.Taxi, Items = new[] { food.Line(2), rice.Line(3), furniture.Line(1) } };
                Assert.True(s.ConfigureEmployeePurchase(s.MainShop.Buyer.Id, plan));
                var quote = s.QuoteEmployeePurchase(s.MainShop.Buyer.Id);
                Assert.IsNull(quote.Error, quote.Error);
                Assert.True(s.DispatchEmployeePurchase(s.MainShop.Buyer.Id));
                var trip = s.EmployeePurchaseTrips.Single();
                s.AdvanceClock((int)(Math.Max(trip.ReturnAt, trip.DeliveryAt) - s.Clock.Now.TotalMinutes + 1));
                Assert.AreEqual(2, s.MainShop.Locker.Food.IngredientCount(food.Id) + s.MainShop.Temp.Food.IngredientCount(food.Id));
                Assert.AreEqual(1.5, s.MainShop.Locker.Food.RiceKg + s.MainShop.Temp.Food.RiceKg);
                Assert.AreEqual(1, s.MainShop.Locker.FurnitureCount(furniture.Id) + s.MainShop.Temp.FurnitureCount(furniture.Id));
                Assert.Zero(s.EmployeePurchaseTrips.Count);
            }
        }

        [Test]
        public void SwitchingBrowseSourceDoesNotLoseItemsOrModifyAnActiveTrip()
        {
            using (var s = Make())
            {
                var plan = Mixed(s);
                Assert.True(s.ConfigureEmployeePurchase(s.MainShop.Buyer.Id, plan));
                Assert.True(s.DispatchEmployeePurchase(s.MainShop.Buyer.Id));
                var draft = s.MainShop.Buyer.PurchasePlan.Copy();
                Assert.Zero(s.ChangePurchaseSource(draft, plan.Items[0].SourceId));
                Assert.AreEqual(3, draft.Items.Length);
                Assert.AreEqual(3, s.EmployeePurchaseTrips.Single().Items.Length);
                draft.Items = draft.Items.Take(1).ToArray();
                Assert.True(s.CompleteEmployeePurchase(s.MainShop.Buyer.Id, draft));
                Assert.AreEqual(3, s.EmployeePurchaseTrips.Single().Items.Length, "保存新清单不能改动已出发的采购");
                Assert.AreEqual(1, s.MainShop.Buyer.PurchasePlan.Items.Length);
            }
        }

        private static void Wallet(GameSession s, Money amount)
        { s.Ledger.ForceDebit(s.Clock.Today, "测试", s.Ledger.Balance - amount, "设置可用现金"); }

        [Test] public void PartialCash_BuysInListOrderAndReturnsChangeOnceAfterSave()
        {
            using (var s = Make())
            {
                var offers = s.EmployeePurchaseCatalog();
                var cheap = offers.First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "cpu0");
                var dear = offers.First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "cpu1");
                var plan = new EmployeePurchasePlan { Transport = VehicleKind.Walk, Items = new[] { cheap.Line(5), dear.Line(5) } };
                var id = s.MainShop.Buyer.Id; Assert.True(s.ConfigureEmployeePurchase(id, plan));
                var full = s.QuoteEmployeePurchase(id);
                Money wallet = cheap.Price * 2 + full.Travel + full.Freight + Money.FromYuan(1);
                Wallet(s, wallet); var q = s.QuoteEmployeePurchase(id);
                Assert.IsNull(q.Error); Assert.AreEqual(wallet, q.Budget); Assert.Less(q.Total.Cents, q.RequiredTotal.Cents);
                Assert.True(s.DispatchEmployeePurchase(id)); Assert.AreEqual(Money.Zero, s.Ledger.Balance);
                var trip = s.EmployeePurchaseTrips.Single(); Assert.AreEqual(1, trip.Items.Length);
                Assert.AreEqual("cpu0", trip.Items[0].PartId); Assert.AreEqual(2, trip.Items[0].Count);
                Assert.AreEqual(100, trip.ChangeCents);
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    loaded.EventsEnabled = false;
                    var copy = loaded.EmployeePurchaseTrips.Single(); Assert.AreEqual(100, copy.ChangeCents);
                    loaded.AdvanceClock((int)(System.Math.Max(copy.ReturnAt, copy.DeliveryAt) - loaded.Clock.Now.TotalMinutes + 1));
                    Assert.AreEqual(1, loaded.Ledger.Balance.Yuan);
                    loaded.AdvanceClock(1); Assert.AreEqual(1, loaded.Ledger.Balance.Yuan);
                }
            }
        }
        [Test] public void PartialCash_SkipsUnaffordableEarlyItemButStillBuysLaterCheapItem()
        {
            using (var s = Make())
            {
                var offers = s.EmployeePurchaseCatalog();
                var dear = offers.First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "cpu2");
                var cheap = offers.First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "cpu0");
                var id = s.MainShop.Buyer.Id;
                Assert.True(s.ConfigureEmployeePurchase(id, new EmployeePurchasePlan { Transport = VehicleKind.Walk, Items = new[] { dear.Line(1), cheap.Line(1) } }));
                var full = s.QuoteEmployeePurchase(id);
                Wallet(s, cheap.Price + full.Travel + full.Freight);
                Assert.True(s.DispatchEmployeePurchase(id));
                var items = s.EmployeePurchaseTrips.Single().Items;
                Assert.AreEqual(1, items.Length); Assert.AreEqual("cpu0", items[0].PartId);
            }
        }
        [Test] public void Complete_NextDayEligibilityAndOrderSurviveJsonSave()
        {
            using (var s = Make())
            {
                var plan = Mixed(s); plan.Items = plan.Items.Reverse().ToArray();
                var id = s.MainShop.Buyer.Id; var cash = s.Ledger.Balance;
                Assert.True(s.CompleteEmployeePurchase(id, plan));
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    loaded.EventsEnabled = false; loaded.AdvanceClock(1);
                    Assert.Zero(loaded.EmployeePurchaseTrips.Count); Assert.AreEqual(cash, loaded.Ledger.Balance);
                    Assert.AreEqual(1, loaded.MainShop.Buyer.PurchasePlan.FirstDay);
                    CollectionAssert.AreEqual(plan.Items.Select(x => x.PartId), loaded.MainShop.Buyer.PurchasePlan.Items.Select(x => x.PartId));
                    loaded.Clock.Restore(new GameDate(1), 8 * 60 + 59); loaded.AdvanceClock(2);
                    var trip = loaded.EmployeePurchaseTrips.Single();
                    CollectionAssert.AreEqual(plan.Items.Select(x => x.PartId), trip.Items.Select(x => x.PartId));
                }
            }
        }
        [Test] public void ThreeDayFrequency_RepeatsAndSurvivesSaveWithoutDailyPurchases()
        {
            using (var s = Make())
            {
                var plan = Mixed(s); plan.IntervalDays = 3; var id = s.MainShop.Buyer.Id;
                Assert.True(s.CompleteEmployeePurchase(id, plan));
                s.Clock.Restore(new GameDate(1), 9 * 60); s.AdvanceClock(1);
                var trip = s.EmployeePurchaseTrips.Single();
                s.AdvanceClock((int)(System.Math.Max(trip.ReturnAt, trip.DeliveryAt) - s.Clock.Now.TotalMinutes + 1));
                using (var loaded = GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    loaded.EventsEnabled = false;
                    Assert.AreEqual(3, loaded.MainShop.Buyer.PurchasePlan.EveryDays);
                    Assert.AreEqual(4, loaded.MainShop.Buyer.PurchasePlan.NextPurchaseDay);
                    foreach (int day in new[] { 2, 3 })
                    {
                        loaded.Clock.Restore(new GameDate(day), 9 * 60); loaded.AdvanceClock(1);
                        Assert.Zero(loaded.EmployeePurchaseTrips.Count, "两次采购之间不应每天出发");
                    }
                    loaded.Clock.Restore(new GameDate(4), 9 * 60); loaded.AdvanceClock(1);
                    Assert.AreEqual(1, loaded.EmployeePurchaseTrips.Count, "间隔三天后自动再次采购");
                    Assert.AreEqual(7, loaded.MainShop.Buyer.PurchasePlan.NextPurchaseDay);
                }
            }
        }
        [Test] public void ThreeDayFrequency_FailedAttemptWaitsUntilNextCycle()
        {
            using (var s = Make())
            {
                var plan = Mixed(s); plan.IntervalDays = 3;
                Assert.True(s.CompleteEmployeePurchase(s.MainShop.Buyer.Id, plan));
                Wallet(s, Money.FromYuan(1)); s.Clock.Restore(new GameDate(1), 9 * 60); s.AdvanceClock(1);
                Assert.Zero(s.EmployeePurchaseTrips.Count); Assert.AreEqual(4, s.MainShop.Buyer.PurchasePlan.NextPurchaseDay);
                s.DebugAddCash(500000); s.Clock.Restore(new GameDate(2), 9 * 60); s.AdvanceClock(1);
                Assert.Zero(s.EmployeePurchaseTrips.Count);
                s.Clock.Restore(new GameDate(4), 9 * 60); s.AdvanceClock(1);
                Assert.AreEqual(1, s.EmployeePurchaseTrips.Count);
            }
        }
        [Test] public void LegacyFrequency_ZeroMeansDailyAndCopyKeepsCadence()
        {
            var plan = new EmployeePurchasePlan { IntervalDays = 0, FirstDay = 1, LastDay = 1 };
            Assert.AreEqual(1, plan.EveryDays); Assert.AreEqual(2, plan.NextPurchaseDay);
            Assert.AreEqual(1, plan.Copy().IntervalDays);
            plan.IntervalDays = 3; Assert.AreEqual(4, plan.Copy().NextPurchaseDay);
        }
        [Test]
        public void PostingAnotherAdRetainsPreviousCandidates_AndAddsMoreTomorrow()
        {
            using (var s = GameSessionTests.Make(50000, 9 * 60))
            {
                s.EventsEnabled = false;
                GameSessionTests.Teleport(s, "pc");
                Assert.True(s.PostJobAd());
                s.Clock.Restore(new GameDate(1), 8 * 60 + 59); s.AdvanceClock(2);
                var originals = s.Candidates.Select(x => x.Id).ToArray();
                Assert.AreEqual(2, originals.Length);
                Assert.True(s.PostJobAd());
                Assert.AreEqual(2, s.Candidates.Count, "重贴告示不能删除原候选人");
                s.Clock.Restore(new GameDate(2), 8 * 60 + 59); s.AdvanceClock(2);
                Assert.AreEqual(4, s.Candidates.Count);
                Assert.True(originals.All(id => s.Candidates.Any(x => x.Id == id)));
            }
        }
    }
}
