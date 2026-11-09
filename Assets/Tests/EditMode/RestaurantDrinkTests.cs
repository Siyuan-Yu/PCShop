using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;
using PCShopSell.Game;
using PCShopSell.Game.UI;

namespace PCShopSell.Tests
{
    public class RestaurantDrinkTests
    {
        private static GameSession Make(bool shelf=true)
        {
            var s=GameSessionTests.Make(500000,600);s.EventsEnabled=false;
            s.Clock.Restore(new GameDate(15),600);GameSessionTests.Teleport(s,"pc");
            Assert.True(s.SetShopMainCategory("restaurant"));
            s.CurrentShop.Layout.Clear();s.PlayerShop.Stock.Clear();
            s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.ShopCounter,X=4,Z=4});
            if(shelf)
            {
                s.CurrentShop.Layout.Add(new PlacedFurniture{Id=FurnitureCatalog.Shelf,X=2,Z=2});
                Assert.True(s.SetShelfKind(1,"drink"));
            }
            else s.PlayerShop.SetShelfCapacity(new System.Collections.Generic.Dictionary<string,double>());
            s.PlayerShop.ForcedClosed=true;
            s.SpecialBusiness("pc").NextVisit=long.MaxValue;
            return s;
        }
        private static DiningOrder Eating(GameSession s,int id=1)
        {
            var order=new DiningOrder{Id=id,Recipe=0,Price=18,Stage=DiningStage.Eating,
                Arrived=s.Clock.Now.TotalMinutes-15,EatUntil=s.Clock.Now.TotalMinutes+1};
            s.SpecialBusiness("pc").Orders.Add(order);return order;
        }
        private static GameSession Reload(GameSession s)=>GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave())));
        private static void PayMeals(GameSession s,int count)
        {
            s.AdvanceClock(2);
            for(int i=0;i<count;i++)
            {
                var next=s.RestaurantCheckoutCustomer;
                Assert.NotNull(next);
                Assert.True(s.CheckoutRestaurantCustomer(next.Id));
            }
        }

        [TestCase(Gifts.Soda)] [TestCase(Gifts.Drink)]
        public void RestaurantDrinksHaveHigherIntent_StillSensitiveToPrice(string id)
        {
            using(var s=Make())
            {
                var sim=s.PlayerShop;long price=sim.GoodsPriceRange(id).max;
                sim.RetailKind="clothing";double ordinary=sim.GoodsPurchaseIntentAtPrice(id,price);
                sim.RetailKind="restaurant";double restaurant=sim.GoodsPurchaseIntentAtPrice(id,price);
                Assert.AreEqual(ordinary+.20,restaurant,1e-9);
                Assert.Greater(sim.GoodsPurchaseIntentAtPrice(id,Gifts.Get(id).Price.Yuan),restaurant);
                sim.SalesBonusOf=()=>.25;
                Assert.AreEqual(1,sim.GoodsPurchaseIntentAtPrice(id,sim.GoodsPriceRange(id).min));
                Assert.AreEqual(0,sim.RestaurantDrinkPurchaseIntentBonus(Gifts.Snack));
                sim.RetailKind="secondhand";Assert.AreEqual(0,sim.RestaurantDrinkPurchaseIntentBonus(id));
            }
        }
        [TestCase(Gifts.Soda)] [TestCase(Gifts.Drink)]
        public void DiningSettlementSellsOneDisplayedDrinkAtConfirmedPrice_OnceAcrossSaveLoad(string id)
        {
            using(var s=Make())
            {
                s.PlayerShop.Stock.ForceAddGoods(id,3);
                long price=Gifts.Get(id).Price.Yuan;Assert.True(s.PlayerShop.SetGoodsSalePrice(id,price));
                Eating(s);
                using(var loaded=Reload(s))
                {
                    PayMeals(loaded,1);
                    Assert.AreEqual(2,loaded.PlayerShop.Stock.GoodsCount(id));
                    var sale=loaded.Ledger.Entries.Single(e=>e.SourceEventId=="meal:1:drink");
                    Assert.AreEqual(price,sale.Amount.Yuan);Assert.AreEqual("pc",sale.ShopId);
                    Assert.AreEqual(id,sale.Attribution.ProductId);Assert.AreEqual(1,sale.Attribution.Quantity);
                    Assert.AreEqual(18+price,loaded.SpecialBusiness("pc").Revenue);
                    Assert.AreEqual(1,loaded.SpecialBusiness("pc").Paid);
                    using(var again=Reload(loaded))
                    {
                        again.AdvanceClock(5);
                        Assert.AreEqual(2,again.PlayerShop.Stock.GoodsCount(id));
                        Assert.AreEqual(1,again.Ledger.Entries.Count(e=>e.SourceEventId=="meal:1:drink"));
                        Assert.AreEqual(18+price,again.SpecialBusiness("pc").Revenue);
                    }
                }
            }
        }
        [TestCase(false,Gifts.Soda)] [TestCase(true,Gifts.Snack)] [TestCase(true,Gifts.Wine)]
        public void WarehouseOnlyOrNonDrinkGoodsCannotBeSoldWithMeals(bool shelf,string id)
        {
            using(var s=Make(shelf))
            {
                s.PlayerShop.Stock.ForceAddGoods(id,2);Eating(s);PayMeals(s,1);
                Assert.AreEqual(2,s.PlayerShop.Stock.GoodsCount(id));
                Assert.AreEqual(18,s.SpecialBusiness("pc").Revenue);
                Assert.False(s.Ledger.Entries.Any(e=>e.SourceEventId=="meal:1:drink"));
            }
        }
        [Test] public void MultipleDinersCannotOversellLastDrink_AndUnfinishedOrdersDoNotBuy()
        {
            using(var s=Make())
            {
                s.PlayerShop.Stock.ForceAddGoods(Gifts.Soda,1);
                Assert.True(s.PlayerShop.SetGoodsSalePrice(Gifts.Soda,5));
                Eating(s,1);Eating(s,2);var waiting=Eating(s,3);waiting.Stage=DiningStage.Waiting;
                var left=Eating(s,4);left.Stage=DiningStage.Left;
                PayMeals(s,2);
                Assert.Zero(s.PlayerShop.Stock.GoodsCount(Gifts.Soda));
                Assert.AreEqual(41,s.SpecialBusiness("pc").Revenue);
                Assert.AreEqual(2,s.SpecialBusiness("pc").Paid);
                Assert.AreEqual(1,s.Ledger.Entries.Count(e=>e.SourceEventId!=null&&e.SourceEventId.EndsWith(":drink")));
            }
        }
        [Test] public void RestaurantPricingShowsDrinkEditor_AndPreviewUsesActualPurchaseIntent()
        {
            using(var s=Make())
            {
                s.PlayerShop.Stock.ForceAddGoods(Gifts.Soda,2);
                var root=new GameObject("RestaurantDrinkPricingTest");root.SetActive(false);
                var controller=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller,s);
                typeof(GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;
                var ui=new GameUi(controller);
                try
                {
                    ui.OpenDock();ui.DebugSelectTab(2);
                    var edit=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>b.name=="EditPrice_goods_soda");
                    edit.onClick.Invoke();
                    var label=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Single(t=>t.name=="PurchaseIntent_goods_soda");
                    StringAssert.Contains("餐馆+20",label.text);
                    StringAssert.Contains($"{s.PlayerShop.GoodsPurchaseIntentAtPrice(Gifts.Soda,s.PlayerShop.GoodsPriceOf(Gifts.Soda).Yuan)*100:0}%",label.text);
                }
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)Object.DestroyImmediate(canvas.gameObject);
                    Object.DestroyImmediate(root);
                    if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
    }
}
