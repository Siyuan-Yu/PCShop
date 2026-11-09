using System;
using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Finance;
using PCShopSell.Core.Home;
using PCShopSell.Core.Market;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public class MarkupRangeTests
    {
        [TestCase(1.6)] [TestCase(3.0)]
        public void GlobalAndIndividualMultipliersSurviveJsonLoad(double markup)
        {
            using(var s=GameSessionTests.Make())
            {
                var part=PartCatalog.Get("cpu0");s.SetMarkup(markup);
                s.PlayerShop.SetPartMarkup(part,markup);s.PlayerShop.SetGoodsMarkup(Gifts.Soda,markup);
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    Assert.AreEqual(markup,loaded.PlayerShop.Markup);
                    Assert.AreEqual(markup,loaded.PlayerShop.MarkupOf(part));
                    Assert.AreEqual(markup,loaded.PlayerShop.MarkupOfGoods(Gifts.Soda));
                }
                s.SetMarkup(9);s.PlayerShop.SetPartMarkup(part,9);s.PlayerShop.SetGoodsMarkup(Gifts.Soda,9);
                Assert.AreEqual(3,s.PlayerShop.Markup);Assert.AreEqual(3,s.PlayerShop.MarkupOf(part));Assert.AreEqual(3,s.PlayerShop.MarkupOfGoods(Gifts.Soda));
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void ConfirmedPricesAcceptTripleReference_RejectHigher_AndSurviveLoad(bool boxed)
        {
            using(var s=GameSessionTests.Make())
            {
                var sim=s.PlayerShop;var part=PartCatalog.Get("cpu0");
                long max=(long)Math.Round(s.Market.PriceOf(part).Yuan*(boxed?1+sim.Settings.BoxedPremium:1)*3);
                Assert.AreEqual(max,sim.PartPriceRange(part,boxed).max);
                Assert.True(sim.SetPartSalePrice(part,boxed,max));Assert.False(sim.SetPartSalePrice(part,boxed,max+1));
                long goodsMax=Gifts.Get(Gifts.Soda).Price.Yuan*3;
                Assert.AreEqual(goodsMax,sim.GoodsPriceRange(Gifts.Soda).max);
                Assert.True(sim.SetGoodsSalePrice(Gifts.Soda,goodsMax));Assert.False(sim.SetGoodsSalePrice(Gifts.Soda,goodsMax+1));
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                {
                    Assert.AreEqual(max,loaded.PlayerShop.PriceOf(part,boxed).Yuan);
                    Assert.AreEqual(goodsMax,loaded.PlayerShop.GoodsPriceOf(Gifts.Soda).Yuan);
                }
            }
        }
        [Test] public void HighPricesCanReachZero_AndExistingBonusesStillMatter()
        {
            Assert.Greater(ShopSettings.BuyChance(1.5),0);Assert.Zero(ShopSettings.BuyChance(1.6));Assert.Zero(ShopSettings.BuyChance(3));
            using(var s=GameSessionTests.Make())
            {
                var sim=s.PlayerShop;sim.RetailKind="clothing";sim.SalesBonusOf=()=>0;sim.HasClothingMirror=()=>false;
                var clothing=ClothingProducts.All.First();long max=sim.GoodsPriceRange(clothing.Id).max;
                Assert.Zero(sim.GoodsPurchaseIntentAtPrice(clothing.Id,max));
                sim.HasClothingMirror=()=>true;Assert.AreEqual(.05,sim.GoodsPurchaseIntentAtPrice(clothing.Id,max),1e-9);
                sim.SalesBonusOf=()=>.25;Assert.AreEqual(.30,sim.GoodsPurchaseIntentAtPrice(clothing.Id,max),1e-9);
                sim.RetailKind="restaurant";Assert.AreEqual(.45,sim.GoodsPurchaseIntentAtPrice(Gifts.Soda,15),1e-9);
                Assert.AreEqual(.25,sim.MarkupPurchaseIntent(3),1e-9);
                var part=PartCatalog.Get("cpu0");sim.SalesBonusOf=()=>0;
                Assert.Zero(sim.PartPurchaseIntentAtPrice(part,false,sim.PartPriceRange(part,false).max));
                Assert.AreEqual(1-sim.Settings.BoxedTrust,sim.PartPurchaseIntentAtPrice(part,true,sim.PartPriceRange(part,true).max),1e-9);
            }
        }
        [TestCase(0)] [TestCase(.25)]
        public void ActualRetailPurchasesUseZeroOrSalesBonusAtTriplePrice(double bonus)
        {
            var random=new GameRandom(19);var clock=new GameClock(new GameDate(0),600);var ledger=new Ledger();
            using(var sim=new ShopSimulation("test",ShopSize.Large,clock,ledger,new HardwareMarket(random),random,ShopSimulationTests.BusySettings()))
            {
                sim.RetailKind="food";sim.PlayerPresent=true;sim.Markup=3;sim.SalesBonusOf=()=>bonus;
                sim.Stock.ForceAddGoods(Gifts.Snack,1000);int accepted=0;
                sim.CustomerReadyToPay+=c=>{accepted++;sim.Checkout(c.Id);};clock.Advance(60);
                Assert.Greater(sim.TodayCustomers,0);
                if(bonus==0){Assert.Zero(accepted);Assert.Zero(sim.TodaySales.Cents);Assert.AreEqual(1000,sim.Stock.GoodsCount(Gifts.Snack));}
                else {Assert.Greater(accepted,0);Assert.Greater(sim.TodaySales.Cents,0);Assert.Less(sim.Stock.GoodsCount(Gifts.Snack),1000);}
            }
        }
        [Test] public void WorkbenchAllowsThreeButRejectsOutOfRange()
        {
            using(var s=HomeTests.Make(200000,600))
            {
                GameSessionTests.Teleport(s,s.Home.Id);s.HomeBox.AddFurniture(GameSession.HomeWorkbenchId,1);
                Assert.True(s.SetWorkbenchMarkup(s.MainShop.Place.Id,3));Assert.AreEqual(3,s.MainShop.Sim.Markup);
                Assert.False(s.SetWorkbenchMarkup(s.MainShop.Place.Id,3.01));Assert.False(s.SetWorkbenchMarkup(s.MainShop.Place.Id,double.NaN));
            }
        }
        [TestCase(0)] [TestCase(.25)]
        public void ExpensiveMenuActuallyRequiresPurchaseIntent(double salesBonus)
        {
            using(var s=GameSessionTests.Make(500000,600))
            {
                s.EventsEnabled=false;GameSessionTests.Teleport(s,"pc");Assert.True(s.SetShopMainCategory("restaurant"));
                s.CurrentShop.Layout.Clear();
                var ids = GameSession.OpeningFurniture("restaurant");
                for(int i=0;i<ids.Length;i++)s.CurrentShop.Layout.Add(new PlacedFurniture{Id=ids[i],X=1+i,Z=2});
                s.PlayerShop.ForcedClosed=false;s.PlayerShop.SalesBonusOf=()=>salesBonus;
                var state=s.SpecialBusiness("pc");state.MenuMarkup=3;
                s.AdvanceClock(240);
                if(salesBonus==0)Assert.AreEqual(1,state.NextId,"0%购买意愿不会生成堂食订单");
                else Assert.Greater(state.NextId,1,"销售加成能让高价菜品实际成交下单");
                using(var loaded=GameSession.Load(s.City,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()))))
                    Assert.AreEqual(3,loaded.SpecialBusiness("pc").MenuMarkup);
            }
        }
        [Test] public void ShopSliderAndRestaurantMenuReachThree_AndMenuShowsZeroIntent()
        {
            using(var s=GameSessionTests.Make(500000,600))
            {
                s.EventsEnabled=false;GameSessionTests.Teleport(s,"pc");
                var root=new GameObject("MarkupRangeUiTest");root.SetActive(false);var controller=root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller,s);typeof(GameController).GetProperty("City").SetValue(controller,s.City);
                var existing=UnityEngine.EventSystems.EventSystem.current;var ui=new GameUi(controller);
                try
                {
                    ui.OpenDock();ui.DebugSelectTab(2);
                    var slider=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Slider>()).Single(x=>x.name=="GlobalMarkupSlider");
                    Assert.AreEqual(3,slider.maxValue);slider.value=3;Assert.AreEqual(3,s.PlayerShop.Markup);
                    Assert.True(s.SetShopMainCategory("restaurant"));s.PlayerShop.SalesBonusOf=()=>0;
                    s.SpecialBusiness("pc").MenuMarkup=2.9;ui.RebuildDock();
                    var plus=ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Button>()).Single(b=>b.GetComponentInChildren<Text>()?.text=="+");
                    plus.onClick.Invoke();Assert.AreEqual(3,s.SpecialBusiness("pc").MenuMarkup,1e-9);
                    Assert.True(ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<Text>()).Any(t=>t.text.Contains("菜单倍率 ×3.0")&&t.text.Contains("购买意愿 0%")));
                }
                finally
                {
                    ui.CloseAllWindows();foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if(existing==null&&UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
    }
}
