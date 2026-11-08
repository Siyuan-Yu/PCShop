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
                Assert.False(s.TradeCityInvestment("city_chip", 1, true, true));
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
    }
}
