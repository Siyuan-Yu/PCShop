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
        public void EmployeeCannotBuyDiscountedSmallQuantityButCanBuyRetailIngredients()
        {
            using(var s=Make())
            {
                s.MainShop.Buyer=new Employee{Name="进货员",DailyWageYuan=100};
                CityTestPeople.AtAssignedWork(s,s.MainShop.Buyer);
                var offers=s.EmployeePurchaseCatalog();
                var wholesale=offers.First(o=>o.Kind==PurchaseGoodsKind.Gift&&o.Id==Gifts.Cig);
                var retail=offers.First(o=>o.Kind==PurchaseGoodsKind.Ingredient);
                var rice=offers.First(o=>o.Kind==PurchaseGoodsKind.Gift&&o.Id=="produce:rice");
                var clothing=offers.First(o=>o.Kind==PurchaseGoodsKind.Gift&&ClothingProducts.IsClothing(o.Id));
                Assert.AreEqual(10,s.PurchaseMinimum(wholesale));Assert.AreEqual(5,s.PurchaseMinimum(rice));
                Assert.AreEqual(1,s.PurchaseMinimum(retail));Assert.AreEqual(1,s.PurchaseMinimum(clothing));
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
