using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Tests
{
    // Added for targeted regression when tests are separately authorized. Not run in the compile-only delivery.
    public sealed class CityAssetIntegrationTests
    {
        private static GameSession Make()
        {
            var s = new GameSession(CityLayout.FromData(GameSessionTests.Layout()), 778, Money.FromYuan(3000000),
                GameTimestamp.From(new GameDate(0), 600), false);
            s.EventsEnabled = false; s.PlayerShop.Settings.BaseCustomersPerHour = 0;
            CompanyWellbeingRegressionTests.Tier(s,3); // 证券交易现于花园阶段开放，门槛由专用回归覆盖。
            GameSessionTests.Teleport(s, "agency"); return s;
        }
        private static GameSession Reload(GameSession s) => GameSession.Load(s.City, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));

        [Test] public void LeaseConversionKeepsOperatingInstanceAndChargesExactPreviewOnce()
        {
            using (var s = Make())
            {
                string id = s.MainShop.Place.Id; var shop = s.MainShop; var sim = shop.Sim;
                var q = s.PreviewCityPropertyPurchase(id, false); var cash = s.Ledger.Balance;
                Assert.True(s.BuyProperty(id, false)); Assert.AreSame(shop, s.ShopAt(id)); Assert.AreSame(sim, s.ShopAt(id).Sim);
                Assert.True(shop.OwnsProperty); Assert.True(shop.Rented); Assert.True(s.HasShop); Assert.AreEqual(Money.Zero, s.RentOf(shop));
                Assert.AreEqual(cash - q.TotalDue, s.Ledger.Balance); Assert.False(s.BuyProperty(id, false));
                Assert.AreEqual(cash - q.TotalDue, s.Ledger.Balance);
                using (var load = Reload(s)) { Assert.True(load.HasShop); Assert.True(load.MainShop.OwnsProperty); Assert.AreEqual(Money.Zero, load.CurrentShopRent); }
            }
        }
        [Test] public void BuyingNpcPremisesPreservesTenantAndDoesNotCreatePlayerBusiness()
        {
            using (var s = Make())
            {
                Assert.True(s.BuyProperty("tea", false));
                var p = s.PropertyAt("tea"); Assert.AreEqual("npc", p.UseMode); Assert.IsNull(s.ShopAt("tea"));
                Assert.AreEqual(s.City.GetPlace("tea").Name, s.PlaceTitle(s.City.GetPlace("tea")));
                Assert.False(s.IsPlayerPropertyOccupied("tea")); Assert.False(s.OpenCityOwnedShop("tea"));
                using (var load = Reload(s)) { Assert.AreEqual("npc", load.PropertyAt("tea").UseMode); Assert.IsNull(load.ShopAt("tea")); }
            }
        }
        [Test] public void SellingSelfUseRequiresExplicitOperatingExit()
        {
            using (var s = Make())
            {
                var id = s.MainShop.Place.Id; Assert.True(s.BuyProperty(id, false)); var cash = s.Ledger.Balance;
                Assert.IsNotNull(s.CityPropertySaleBlocker(id)); Assert.False(s.SellProperty(id)); Assert.AreEqual(cash, s.Ledger.Balance);
            }
        }
        [Test] public void BuyingAndResellingNpcPropertyLosesTransactionCosts()
        {
            using (var s = Make())
            {
                var cash = s.Ledger.Balance; Assert.True(s.BuyProperty("tea", false)); Assert.True(s.SellProperty("tea"));
                Assert.Less(s.Ledger.Balance.Cents, cash.Cents); var after = s.Ledger.Balance; Assert.False(s.SellProperty("tea")); Assert.AreEqual(after, s.Ledger.Balance);
            }
        }
        [Test] public void ReadingCatalogAndPricesDoesNotMoveOrChargeOrAdvanceRandom()
        {
            using (var s = Make())
            {
                var cash = s.Ledger.Balance; var place = s.CurrentPlace; var time = s.Clock.Now; var random = s.Random.State;
                for (int n = 0; n < 8; n++)
                {
                    foreach (var p in s.CityCommercialProperties) s.PreviewCityPropertyPurchase(p.Id, false);
                    foreach (var e in s.CityEquities) { s.CityInvestmentPrice(e.Id); s.CityInvestmentTradeTotal(e.Id, 10, true); }
                }
                Assert.AreEqual(cash, s.Ledger.Balance); Assert.AreSame(place, s.CurrentPlace); Assert.AreEqual(time, s.Clock.Now); Assert.AreEqual(random, s.Random.State);
            }
        }
        [Test] public void EquityFullLiquidationClearsCostAndCannotRoundTripProfit()
        {
            using (var s = Make())
            {
                var cash = s.Ledger.Balance;
                int lot = GameSession.StockLotSize;
                Assert.False(s.TradeCityInvestment("city_chip", 0, true, true));
                Assert.True(s.TradeCityInvestment("city_chip", 2 * lot, true, true));
                Assert.True(s.TradeCityInvestment("city_chip", lot, false));
                Assert.True(s.TradeCityInvestment("city_chip", lot, false));
                Assert.Zero(s.CityInvestmentUnits("city_chip")); Assert.AreEqual(Money.Zero, s.CityInvestmentCost("city_chip"));
                Assert.Less(s.Ledger.Balance.Cents, cash.Cents); Assert.True(s.CityInvestmentRealized("city_chip").IsNegative);
                Assert.False(s.TradeCityInvestment("city_chip", 1, false));
            }
        }
        [Test] public void EquityHistoryHoldingsAndRandomContinuationSurviveSaveLoad()
        {
            using (var s = Make())
            {
                Assert.True(s.TradeCityInvestment("city_art", GameSession.StockLotSize, true, true));
                using (var load = Reload(s))
                {
                    Assert.AreEqual(s.CityInvestmentCost("city_art"), load.CityInvestmentCost("city_art"));
                    Assert.AreEqual(GameSession.StockLotSize, load.CityInvestmentUnits("city_art"));
                    s.Clock.Restore(new GameDate(4), 600); load.Clock.Restore(new GameDate(4), 600);
                    s.TickCityAssets(); load.TickCityAssets();
                    CollectionAssert.AreEqual(s.CityInvestmentHistory("city_art"), load.CityInvestmentHistory("city_art"));
                    Assert.AreEqual(s.CityInvestmentPrice("city_chip"), load.CityInvestmentPrice("city_chip"));
                }
            }
        }

        [Test]
        public void MaximumStockBuyUsesIndividualSharesWithinTheCurrentQuote()
        {
            using var s = Make();
            s.SetSmartphone(true);
            const string id = "city_chip";
            var state = s.StockState(id);
            state.AvailableUnits = 275;
            Assert.That(s.StockMaximumBuyQuantity(id), Is.EqualTo(275));
            Assert.That(s.TradeCityInvestment(id, 200, buy: true, acknowledgeReserve: true), Is.True);
            Assert.That(state.AvailableUnits, Is.EqualTo(75));
            Assert.That(s.StockMaximumBuyQuantity(id), Is.EqualTo(75));
            Assert.That(s.StockTradeBlocker(id, 200, "sell"), Does.Contain("还可成交75"));
            Assert.That(s.CityInvestmentUnits(id), Is.EqualTo(200));
        }

        [Test]
        public void StockMarketHasLargerMovesInBothDirectionsWithinDailyLimits()
        {
            using var s = Make();
            var earlyChanges = new List<double>();
            var laterChanges = new List<double>();
            var intradayChanges = new List<double>();
            int[] quoteHours = { 10, 13, 16 };
            for (int dayIndex = 1; dayIndex <= 70; dayIndex++)
            {
                var day = new GameDate(dayIndex);
                if (day.IsWeekend) continue;
                foreach (int hour in quoteHours)
                {
                    s.Clock.Restore(day, hour * 60);
                    s.TickCityAssets();
                    var changes = CityInvestments.All.Select(stock => s.StockDayChange(stock.Id)).ToArray();
                    intradayChanges.AddRange(changes);
                    if (hour == 16) (dayIndex < 30 ? earlyChanges : laterChanges).AddRange(changes);
                }
            }

            Assert.That(intradayChanges, Is.All.InRange(-.200001, .200001));
            Assert.That(laterChanges.Max(), Is.GreaterThan(.10));
            Assert.That(laterChanges.Min(), Is.LessThan(-.10));
            Assert.That(laterChanges.Average(Math.Abs), Is.GreaterThan(earlyChanges.Average(Math.Abs) * 1.3));
        }

        [Test]
        public void StockBatchValidatesEveryOrderBeforeSellingAndUsesSaleProceedsForPurchase()
        {
            using var s = Make();
            s.SetSmartphone(true);
            const string sell = "city_motor", buy = "city_chip";
            const int lot = GameSession.StockLotSize;
            Assert.True(s.TradeCityInvestment(sell, lot, true, acknowledgeReserve: true));
            s.Ledger.ForceDebit(s.Clock.Today, "测试", s.Ledger.Balance - Money.FromYuan(10));
            var cash = s.Ledger.Balance;
            var orders = new Dictionary<string, long> { [buy] = lot, [sell] = -lot };
            var net = s.CityInvestmentTradeTotal(sell, lot, false) - s.CityInvestmentTradeTotal(buy, lot, true);
            Assert.NotNull(s.StockTradeBlocker(buy, lot, "buy"));

            var quote = s.PreviewStockBatch(orders, shortMode: false);
            Assert.IsNull(quote.Blocker);
            Assert.AreEqual(net, quote.Net);
            Assert.AreEqual(cash + net, quote.CashAfter);
            var buyer = s.StockState(buy);
            long available = buyer.AvailableUnits;
            buyer.AvailableUnits = lot - 1;
            Assert.False(s.TradeStockBatch(orders, shortMode: false, acknowledgeReserve: true));
            Assert.AreEqual(cash, s.Ledger.Balance);
            Assert.AreEqual(lot, s.CityInvestmentUnits(sell));
            Assert.Zero(s.CityInvestmentUnits(buy));
            Assert.That(s.StockExecutions(sell, 0, shortMode: false).Select(e => e.Kind), Is.EqualTo(new[] { StockExecutionKind.Buy }));
            Assert.That(s.StockExecutions(buy, 0, shortMode: false), Is.Empty);

            buyer.AvailableUnits = available;
            Assert.True(s.TradeStockBatch(orders, shortMode: false, acknowledgeReserve: true));
            Assert.AreEqual(cash + net, s.Ledger.Balance);
            Assert.Zero(s.CityInvestmentUnits(sell));
            Assert.AreEqual(lot, s.CityInvestmentUnits(buy));

            void CheckExecutions(GameSession restored)
            {
                Assert.That(restored.StockExecutions(sell, 0, shortMode: false).Select(e => e.Kind),
                    Is.EqualTo(new[] { StockExecutionKind.Buy, StockExecutionKind.Sell }));
                var fill = restored.StockExecutions(buy, 0, shortMode: false).Single();
                Assert.AreEqual(StockExecutionKind.Buy, fill.Kind);
                Assert.AreEqual(lot, fill.Units);
                Assert.AreEqual(s.CityInvestmentPrice(buy).Cents, fill.PriceCents);
                Assert.AreEqual(s.Clock.Now.TotalMinutes, fill.Minute);
            }

            var save = s.CreateSave();
            using var loaded = GameSession.Load(s.City, save);
            CheckExecutions(loaded);
            save.stockMarket.Version = 1;
            save.stockMarket.Executions.Clear();
            using var legacy = GameSession.Load(s.City, save);
            CheckExecutions(legacy);
        }

        [Test]
        public void StockBatchUsesReleasedShortMarginAndAccountsForBothFees()
        {
            using var s = Make();
            s.SetSmartphone(true);
            const string cover = "city_motor", open = "city_chip";
            const int lot = GameSession.StockLotSize;
            Assert.True(s.OpenStockShort(cover, lot, acknowledgeReserve: true));
            s.Ledger.ForceDebit(s.Clock.Today, "测试", s.Ledger.Balance - Money.FromYuan(10));
            var cash = s.Ledger.Balance;
            var orders = new Dictionary<string, long> { [open] = lot, [cover] = -lot };
            var net = s.StockCoverReturn(cover, lot) - s.StockShortOpeningCost(open, lot);
            var fees = CityInvestments.Fee(s.CityInvestmentPrice(cover) * lot) + CityInvestments.Fee(s.CityInvestmentPrice(open) * lot);
            var quote = s.PreviewStockBatch(orders, shortMode: true);
            Assert.IsNull(quote.Blocker);
            Assert.AreEqual(net, quote.Net);
            Assert.AreEqual(fees, quote.Fees);
            Assert.AreEqual(cash, s.Ledger.Balance);

            Assert.True(s.TradeStockBatch(orders, shortMode: true, acknowledgeReserve: true));
            Assert.AreEqual(cash + net, s.Ledger.Balance);
            Assert.Zero(s.StockState(cover).ShortUnits);
            Assert.AreEqual(lot, s.StockState(open).ShortUnits);
            Assert.AreEqual(s.CityInvestmentPrice(open) * lot, s.StockFrozenMargin);
            Assert.That(s.StockExecutions(cover, 0, shortMode: true).Select(e => e.Kind),
                Is.EqualTo(new[] { StockExecutionKind.Short, StockExecutionKind.Cover }));
            Assert.That(s.StockExecutions(cover, 0, shortMode: false), Is.Empty);
        }
    }
}
