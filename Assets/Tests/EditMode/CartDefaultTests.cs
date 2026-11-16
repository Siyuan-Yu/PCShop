using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;

namespace PCShopSell.Tests
{
    public class CartDefaultTests
    {
        [Test]
        public void NewPurchasesFitInBagAndDoNotCallTruckOrChargeBeforeCheckout()
        {
            using (var s = HomeTests.Make())
            {
                GameSessionTests.Teleport(s, "farm"); var cash = s.Ledger.Balance;
                Assert.True(s.CartAddRice(2.5)); Assert.True(s.CartAddIngredient("veg", 10));
                Assert.AreEqual(7.5, s.CartBagKg); Assert.Zero(s.CartTruckKg);
                Assert.AreEqual(Money.Zero, s.CartFreight(false)); Assert.AreEqual(cash, s.Ledger.Balance);
                Assert.True(s.Checkout(false)); Assert.IsEmpty(s.Couriers);
                Assert.AreEqual(2.5, s.Backpack.Food.RiceKg); Assert.AreEqual(10, s.Backpack.Food.IngredientCount("veg"));
            }
        }

        [Test]
        public void OverflowUsesOnlyRemainingWeightCapacity_AndManualTruckChoiceIsPreservedOnAddingMore()
        {
            using (var s = HomeTests.Make())
            {
                GameSessionTests.Teleport(s, "farm"); s.Backpack.Food.RiceKg = 11.5;
                Assert.True(s.CartAddRice(2.5));
                Assert.AreEqual(.5, s.CartBagKg); Assert.AreEqual(2, s.CartTruckKg);
                s.CartAllToTruck(); Assert.True(s.CartAddRice(2.5));
                Assert.AreEqual(.5, s.CartBagKg); Assert.AreEqual(4.5, s.CartTruckKg);
                Assert.AreEqual(10, s.Cart.Single().Amount);
            }
        }

        [Test]
        public void LightFurnitureDefaultsToBag_AndFridgeToTruckWhenWalking()
        {
            using (var s = HomeTests.Make())
            {
                GameSessionTests.Teleport(s, "mall");
                Assert.True(s.CartAddFurniture("poster")); Assert.True(s.CartAddFurniture(FurnitureCatalog.FridgeSmall));
                Assert.AreEqual(1, s.Cart.Single(l => l.Key == "furn:poster").InBag);
                Assert.Zero(s.Cart.Single(l => l.Key == "furn:" + FurnitureCatalog.FridgeSmall).InBag);
                Assert.AreEqual(1, s.Cart.Single(l => l.Key == "furn:" + FurnitureCatalog.FridgeSmall).ByTruck);
                Assert.LessOrEqual(s.CartBagKg, s.FreeKg);
            }
        }

        [Test]
        public void ElectronicsWholesaleMatchesComputerShopEvenAtAlternateMarket()
        {
            using (var s = HomeTests.Make(200000, 10 * 60))
            {
                s.MainShop.RetailKind = PetShopCatalog.ShopKind;
                s.PlayerShop.Reputation = 80;
                s.DebugAddListing("tea", 0, 60, Money.FromYuan(3000));
                GameSessionTests.Teleport(s, "agency");
                Assert.True(s.SignLease("tea", true, "computer"));
                var computer = s.ShopAt("tea");

                GameSessionTests.Teleport(s, "mall");
                s.SetCourierDest(s.MainShop);
                Assert.True(s.CartAddFurniture(FurnitureCatalog.Shelf));
                Assert.True(s.Checkout(false));

                GameSessionTests.Teleport(s, "stall");
                Assert.True(s.CartAddStall(s.CurrentPlace, PartCatalog.Get("cpu0"), 1));
                s.CartAllToTruck();
                Assert.AreSame(computer, s.CourierShop(true));
                s.SetCourierDest(s.MainShop);
                Assert.AreSame(s.MainShop, s.CourierShop(true), "An explicit destination takes priority.");
                s.CourierDest = null;
                Assert.True(s.Checkout(true));
                Assert.AreEqual(computer.Place.Id, s.Couriers.Last().ShopId);
            }
        }

        [Test]
        public void PurchaseDeliveryRemembersTheLastShippedAddressAcrossStoresAndSave()
        {
            using (var s = HomeTests.Make(200000, 10 * 60))
            {
                s.PlayerShop.Reputation = 80;
                s.DebugAddListing("tea", 0, 60, Money.FromYuan(3000));
                GameSessionTests.Teleport(s, "agency");
                Assert.True(s.SignLease("tea", true));
                var branch = s.ShopAt("tea");
                GameSessionTests.Teleport(s, "mall");
                Assert.AreSame(s.MainShop, s.CourierShop(false));
                s.SetCourierDest(branch);
                Assert.True(s.CartAddFurniture(FurnitureCatalog.Shelf));
                Assert.True(s.Checkout(false));
                Assert.AreEqual(branch.Place.Id, s.Couriers.Last().ShopId);

                GameSessionTests.Teleport(s, "farm");
                Assert.AreSame(branch, s.CourierShop(false));
                s.SetCourierDest(null);
                Assert.True(s.CartAddRice(2.5));
                Assert.True(s.Checkout(false));
                GameSessionTests.Teleport(s, "mall");
                Assert.AreSame(branch, s.CourierShop(false), "自提结账没有改变最近一次送货地址");

                s.SetCourierDest(null);
                Assert.True(s.CartAddFurniture(FurnitureCatalog.Shelf));
                Assert.True(s.Checkout(false));
                Assert.False(s.Couriers.Last().ToShop);
                var save = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using (var loaded = GameSession.Load(s.City, save))
                {
                    GameSessionTests.Teleport(loaded, "farm");
                    Assert.IsNull(loaded.CourierShop(true), "重新进店后仍优先使用上次送货的家里地址");
                }
            }
        }
    }
}
