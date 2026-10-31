using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;

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
                Assert.True(s.CartAddRice(1)); Assert.True(s.CartAddIngredient("veg", 2));
                Assert.AreEqual(2, s.CartBagKg); Assert.Zero(s.CartTruckKg);
                Assert.AreEqual(Money.Zero, s.CartFreight(false)); Assert.AreEqual(cash, s.Ledger.Balance);
                Assert.True(s.Checkout(false)); Assert.IsEmpty(s.Couriers);
                Assert.AreEqual(1, s.Backpack.Food.RiceKg); Assert.AreEqual(2, s.Backpack.Food.IngredientCount("veg"));
            }
        }

        [Test]
        public void OverflowUsesOnlyRemainingWeightCapacity_AndManualTruckChoiceIsPreservedOnAddingMore()
        {
            using (var s = HomeTests.Make())
            {
                GameSessionTests.Teleport(s, "farm"); s.Backpack.Food.RiceKg = 11.75;
                Assert.True(s.CartAddRice(1));
                Assert.AreEqual(.25, s.CartBagKg); Assert.AreEqual(.75, s.CartTruckKg);
                s.CartAllToTruck(); Assert.True(s.CartAddRice(1));
                Assert.AreEqual(.25, s.CartBagKg); Assert.AreEqual(1.75, s.CartTruckKg);
                Assert.AreEqual(2, s.Cart.Single().Amount);
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
    }
}
