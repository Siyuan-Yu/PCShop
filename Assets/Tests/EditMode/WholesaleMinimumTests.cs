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
    public sealed class WholesaleMinimumTests
    {
        private static GameSession Make()
        {
            var city=CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(Resources.Load<TextAsset>("Data/city_layout").text));
            var s=new GameSession(city,7,Money.FromYuan(500000),GameTimestamp.From(new GameDate(0),9*60));
            s.EventsEnabled=false;s.MainShop.Sim.Settings.BaseCustomersPerHour=0;PackTests.EmptyBag(s);return s;
        }
        private static Place Source(GameSession s,string id)=>s.City.Places.First(p=>s.HasRetailWholesale(p)&&s.CityRetailGoodsAvailable(p,Gifts.Get(id)));

        [Test]
        public void FarmAndEmployeePurchasingOfferOnlyTheFiveMostCommonFish()
        {
            using var s = Make();
            var farm = Source(s, "produce:rice");
            var expected = new[] { "fish_crucian", "fish_carp", "fish_white_stripe", "fish_bream", "fish_topmouth_gudgeon" };
            CollectionAssert.AreEquivalent(expected, FishingCatalog.Fish
                .Where(fish => s.CityRetailGoodsAvailable(farm, Gifts.Get("produce:" + fish.Id))).Select(fish => fish.Id));
            CollectionAssert.AreEquivalent(expected, s.EmployeePurchaseCatalog().Where(offer => offer.Source == farm && offer.Id.StartsWith("produce:fish_"))
                .Select(offer => Gifts.Get(offer.Id).IngredientId));
            GameSessionTests.Teleport(s, farm.Id);
            var cash = s.Ledger.Balance;
            Assert.False(s.BuyIngredient("fish_farmed_sturgeon", 10));
            Assert.False(s.CartAddRetailWholesale(farm, "produce:fish_farmed_sturgeon", 10));
            Assert.AreEqual(cash, s.Ledger.Balance);
        }

        [TestCase("produce:rice",5)]
        [TestCase("produce:egg",10)]
        [TestCase(Gifts.Cig,10)]
        public void DirectWholesaleRequiresTenRetailUnitsAndDoesNotChargeBelowMinimum(string id,int minimum)
        {
            using(var s=Make())
            {
                var source=Source(s,id);GameSessionTests.Teleport(s,source.Id);
                Assert.AreEqual(minimum,s.CitySupplierMinimum(source.Id,id));var cash=s.Ledger.Balance;
                Assert.False(s.BuyRetailWholesale(source.Id,id,minimum-1,true,null));Assert.AreEqual(cash,s.Ledger.Balance);
                Assert.True(s.BuyRetailWholesale(source.Id,id,minimum,false,s.MainShop.Place.Id));
                Assert.Less(s.Ledger.Balance.Cents,cash.Cents);
                if(id=="produce:rice")StringAssert.Contains("2.5 千克",s.WholesaleMinimumText(source.Id,id));
            }
        }

        [TestCase(Gifts.Soda)]
        [TestCase(Gifts.Drink)]
        public void FarmOffersRestaurantDrinksAndShipsThemAsRegularShelfStock(string id)
        {
            using var s = Make();
            var shop = s.MainShop;
            GameSessionTests.Teleport(s, shop.Place.Id);
            Assert.True(s.SetShopMainCategory("restaurant"));
            var farm = Source(s, "produce:rice");
            var drink = Gifts.Get(id);
            var offers = s.EmployeePurchaseCatalog().Where(offer => offer.Id == id).ToArray();
            var farmOffer = offers.Single(offer => offer.Source == farm);
            Assert.AreEqual("饮料", farmOffer.Category);
            Assert.True(offers.Any(offer => offer.Source.Business == Gifts.TobaccoBusiness));
            Assert.False(s.CityRetailGoodsAvailable(farm, Gifts.Get(Gifts.Wine)));

            GameSessionTests.Teleport(s, farm.Id);
            Assert.AreSame(shop, s.CourierShop(true));
            int minimum = s.PurchaseMinimum(farmOffer);
            Assert.AreEqual(10, minimum);
            int quota = s.CitySupplierRemaining(farm.Id, id);
            int before = shop.Sim.Stock.GoodsCount(id);
            Assert.True(s.CartAddRetailWholesale(farm, id, minimum));
            Assert.AreEqual(s.CityRetailWholesalePrice(farm, drink) * minimum, s.CartGoods);
            s.CartAllToTruck();
            int minutes = s.CourierQuote(shop).minutes;
            Assert.True(s.Checkout(true));
            Assert.AreEqual(quota - minimum, s.CitySupplierRemaining(farm.Id, id));
            s.AdvanceClock(minutes + 5);
            Assert.AreEqual(before + minimum, shop.Sim.Stock.GoodsCount(id));
        }

        [Test]
        public void RetailCanStillBuyOneAndDifferentWholesaleProductsCannotMixToMeetMinimum()
        {
            using(var s=Make())
            {
                var source=Source(s,Gifts.Cig);GameSessionTests.Teleport(s,source.Id);
                Assert.True(s.CartAddGift(Gifts.Cig));Assert.True(s.Checkout(false));Assert.AreEqual(1,s.Backpack.GiftCount(Gifts.Cig));
                var cash=s.Ledger.Balance;
                Assert.True(s.CartAddRetailWholesale(source,Gifts.Cig,5));Assert.True(s.CartAddRetailWholesale(source,Gifts.CigHi,5));
                Assert.NotNull(s.CheckoutBlocker(true));Assert.False(s.Checkout(true));Assert.AreEqual(cash,s.Ledger.Balance);
                Assert.AreEqual(1,s.Backpack.GiftCount(Gifts.Cig));
            }
        }

        [Test]
        public void WholesaleMinimumUsesCartTotalAcrossBackpackAndFreight()
        {
            using(var s=Make())
            {
                var source=Source(s,Gifts.Fruit);GameSessionTests.Teleport(s,source.Id);
                Assert.True(s.CartAddRetailWholesale(source,Gifts.Fruit,10));
                Assert.Greater(s.CartBagKg,0);Assert.Greater(s.CartTruckKg,0);
                Assert.IsNull(s.CheckoutBlocker(true));Assert.True(s.Checkout(true));
                s.AdvanceClock(180);
                Assert.AreEqual(10,s.Backpack.GiftCount(Gifts.Fruit)+s.MainShop.Sim.Stock.GoodsCount(Gifts.Fruit));
            }
        }

        [Test]
        public void RestaurantWholesaleRiceLeavesFridgeRoomForEggs()
        {
            using var template = HomeTests.Make();
            var city = template.City;
            using var s = new GameSession(city, 7, Money.FromYuan(50000), GameTimestamp.From(new GameDate(0), 600),
                startWithShop: false) { EventsEnabled = false };
            GameSessionTests.Teleport(s, "agency");
            Assert.True(s.SignRetailLease("pc", false, "restaurant"));
            GameSessionTests.Teleport(s, "pc");
            foreach (string id in GameSession.OpeningFurniture("restaurant"))
                s.CurrentShop.Layout.Add(new PlacedFurniture { Id = id });
            typeof(GameSession).GetMethod("UpdateShopFit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(s, null);
            GameSessionTests.Teleport(s, "farm");
            var farm = s.CurrentPlace;
            Assert.AreSame(s.MainShop, s.CourierShop(true));
            Assert.AreEqual("stock", s.CurrentQuest().Id);
            Assert.False(s.BuyGift(Gifts.Fruit));
            var fridge = s.MainShop.Fridge;
            var fridgeCapacity = GameSession.FridgeCapacityOf(s.MainShop.Layout);
            double eggWeight = 10 * Gifts.Get("produce:egg").WeightKg;
            fridge.Food.Ingredient.Add(new FoodBatch
            {
                Id = "veg", Count = (int)((fridgeCapacity - eggWeight) / Backpack.IngredientKg("veg")),
                ExpireDay = FoodBatch.NoExpiry
            });
            Assert.True(s.CartAddRetailWholesale(farm, "produce:rice", 5));
            Assert.True(s.CartAddRetailWholesale(farm, "produce:egg", 10));
            s.CartAllToTruck();
            var payment = s.CartGoods + s.CartFreight(true);
            var cash = s.Ledger.Balance;
            var riceBefore = s.KitchenRice(s.MainShop);
            var eggsBefore = s.KitchenIngredientCount(s.MainShop, "egg");
            Assert.IsNull(s.CheckoutBlocker(true));
            Assert.True(s.Checkout(true));
            Assert.AreEqual(payment, cash - s.Ledger.Balance);
            Assert.AreNotEqual("stock", s.CurrentQuest().Id);
            s.AdvanceClock(s.CourierQuote(s.MainShop).minutes + 5);
            Assert.AreEqual(riceBefore + 2.5, s.KitchenRice(s.MainShop), 1e-9);
            Assert.AreEqual(eggsBefore + 10, s.KitchenIngredientCount(s.MainShop, "egg"));
            Assert.Zero(fridge.Food.RiceKg);
            Assert.AreEqual(fridgeCapacity, fridge.WeightKg, 1e-9);
        }

        [Test]
        public void EmployeeDeliversRiceToRestaurantWarehouseWithoutAFridge()
        {
            using var s = Make();
            GameSessionTests.Teleport(s, s.MainShop.Place.Id);
            Assert.True(s.SetShopMainCategory("restaurant"));
            var shop = s.MainShop;
            shop.Layout.RemoveAll(p => FurnitureCatalog.Get(p.Id)?.Role == FurnitureRole.Fridge);
            shop.Buyer = new Employee { Name = "进货员", DailyWageYuan = 100 };
            CityTestPeople.AtAssignedWork(s, shop.Buyer);
            var rice = s.EmployeePurchaseCatalog().First(o => o.Id == "produce:rice");
            string employeeId = shop.Buyer.Id;
            var plan = new EmployeePurchasePlan { Items = new[] { rice.Line(5) }, Transport = VehicleKind.Taxi };
            Assert.True(s.ConfigureEmployeePurchase(employeeId, plan));
            Assert.IsNull(s.QuoteEmployeePurchase(employeeId).Error);
            Assert.True(s.DispatchEmployeePurchase(employeeId));
            var trip = s.EmployeePurchaseTrips.Single();
            double before = s.KitchenRice(shop);
            s.AdvanceClock((int)(trip.DeliveryAt - s.Clock.Now.TotalMinutes) + 1);
            Assert.AreEqual(before + 2.5, s.KitchenRice(shop));
            Assert.Zero(shop.Fridge.Food.RiceKg);
            Assert.AreEqual((int)(before / .5) + 5, s.ProcurementCoverageFor(shop.Place.Id, rice.Kind, rice.Id).FreeStock);
        }

        [Test]
        public void EmployeeCatalogUsesWholesaleOnlyForFarmAndEnforcesMinimum()
        {
            using(var s=Make())
            {
                s.MainShop.Buyer=new Employee{Name="进货员",DailyWageYuan=100};
                CityTestPeople.AtAssignedWork(s,s.MainShop.Buyer);
                var offers=s.EmployeePurchaseCatalog();
                var wholesale=offers.First(o=>o.Kind==PurchaseGoodsKind.Gift&&o.Id==Gifts.Cig);
                var farm=offers.Where(o=>o.Source.Business==GameSession.FarmBusiness).ToArray();
                Assert.That(farm, Is.Not.Empty);
                Assert.True(farm.All(o=>o.Kind==PurchaseGoodsKind.Gift));
                var vegetables=farm.First(o=>o.Id=="produce:veg");
                var rice=offers.First(o=>o.Kind==PurchaseGoodsKind.Gift&&o.Id=="produce:rice");
                var clothing=offers.First(o=>o.Kind==PurchaseGoodsKind.Gift&&ClothingProducts.IsClothing(o.Id));
                Assert.AreEqual(10,s.PurchaseMinimum(wholesale));Assert.AreEqual(5,s.PurchaseMinimum(rice));
                Assert.AreEqual(10,s.PurchaseMinimum(vegetables));Assert.AreEqual(1,s.PurchaseMinimum(clothing));
                Assert.AreEqual(10,s.ProcurementSources(PurchaseGoodsKind.Gift,Gifts.Cig).First(o=>o.SupplierId==wholesale.Source.Id).Minimum);
                string id=s.MainShop.Buyer.Id;
                Assert.True(s.ConfigureEmployeePurchase(id,new EmployeePurchasePlan{Items=new[]{wholesale.Line(9)}}));
                var before=s.Ledger.Balance;Assert.False(s.DispatchEmployeePurchase(id));Assert.AreEqual(before,s.Ledger.Balance);
                Assert.IsEmpty(s.EmployeePurchaseTrips);
                Assert.True(s.ConfigureEmployeePurchase(id,new EmployeePurchasePlan{Items=new[]{wholesale.Line(10)}}));
                Assert.True(s.DispatchEmployeePurchase(id));Assert.AreEqual(10,s.EmployeePurchaseTrips.Single().Items.Single().Count);
            }
        }

        [Test]
        public void PartialEmployeePurchaseSkipsBelowMinimumWithoutBlockingOtherValidItems()
        {
            using(var s=Make())
            {
                s.MainShop.Buyer=new Employee{Name="进货员",DailyWageYuan=100};CityTestPeople.AtAssignedWork(s,s.MainShop.Buyer);
                var offers=s.EmployeePurchaseCatalog();
                var wholesale=offers.First(o=>o.Kind==PurchaseGoodsKind.Gift&&o.Id==Gifts.Cig);
                var part=offers.First(o=>o.Kind==PurchaseGoodsKind.Part&&o.Id=="cpu0");
                string id=s.MainShop.Buyer.Id;
                Assert.True(s.ConfigureEmployeePurchase(id,new EmployeePurchasePlan{Items=new[]{wholesale.Line(9),part.Line(1)}}));
                Assert.True(s.DispatchEmployeePurchase(id));var trip=s.EmployeePurchaseTrips.Single();
                Assert.AreEqual(1,trip.Items.Length);Assert.AreEqual("cpu0",trip.Items[0].PartId);
                StringAssert.Contains("起订量为10",trip.SkippedNote);
            }
        }
    }
}
