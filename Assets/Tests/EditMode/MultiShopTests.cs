using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;

namespace PCShopSell.Tests
{
    public class MultiShopTests
    {
        private static GameSession TwoShops()
        {
            GameSession s = GameSessionTests.Make(200000);
            s.PlayerShop.Reputation = 80;
            s.DebugAddListing("tea", 0, 60, Money.FromYuan(3000));
            GameSessionTests.Teleport(s, "agency");
            Assert.IsTrue(s.SignLease("tea", true));
            return s;
        }

        private static void CrossInto(GameSession s, int day)
        {
            s.Clock.Restore(new GameDate(day - 1), 23 * 60 + 30);
            s.AdvanceClock(60);
        }

        [Test]
        public void SecondLease_AsNewShop_KeepsTheOldOne()
        {
            GameSession s = TwoShops();
            Assert.AreEqual(2, s.MyShopCount);
            Assert.IsTrue(s.IsMyShop(s.City.GetPlace("pc")), "旧店还是我的");
            Assert.IsTrue(s.IsMyShop(s.City.GetPlace("tea")));
            Assert.IsFalse(s.IsVacant(s.City.GetPlace("pc")), "旧店不会回到旺铺招租");
            Assert.IsFalse(s.Listings.Any(l => l.PlaceId == "pc" || l.PlaceId == "tea"));
            Assert.AreEqual("tea", s.PlayerShopPlace.Id, "签完切到新店");
            Assert.IsTrue(s.PlayerShop.Stock.IsEmpty, "新店是空的");
            Assert.IsFalse(s.MainShop.Sim.Stock.IsEmpty, "主店的货不动");
            Assert.AreEqual(GameSession.MyShopName + "·二店", s.PlaceTitle(s.City.GetPlace("tea")));
        }

        [Test]
        public void EnteringAShop_SwitchesCurrentShop()
        {
            GameSession s = TwoShops();
            GameSessionTests.Teleport(s, "pc");
            Assert.AreEqual("pc", s.PlayerShopPlace.Id);
            Assert.IsTrue(s.InPlayerShop);
            Assert.IsFalse(s.PlayerShop.Stock.IsEmpty);
            Assert.IsTrue(s.ShopAt("pc").Sim.PlayerPresent);
            Assert.IsFalse(s.ShopAt("tea").Sim.PlayerPresent, "人不在的店不算有人看店");
            GameSessionTests.Teleport(s, "tea");
            Assert.AreEqual("tea", s.PlayerShopPlace.Id);
            Assert.IsFalse(s.ShopAt("pc").Sim.PlayerPresent);
        }

        [Test]
        public void Rent_IsChargedForEveryShop()
        {
            GameSession s = TwoShops();
            Money total = s.TotalShopRent;
            Assert.AreEqual((s.RentOf(s.MainShop) + Money.FromYuan(3000)).Cents, total.Cents);
            s.Leave();
            Money before = s.Ledger.Balance;
            CrossInto(s, 30);
            long rent = s.Ledger.Entries.Where(e => e.Category == "房租" && e.Date.TotalDays == 30 && (e.Memo ?? "").StartsWith(GameSession.MyShopName)).Sum(e => System.Math.Abs(e.Amount.Cents));
            Assert.AreEqual(total.Cents, rent, "两家店的月租都扣");
            Assert.Less(s.Ledger.Balance.Cents, before.Cents);
        }

        [Test]
        public void OwnShop_CannotBeLeasedAgain()
        {
            GameSession s = TwoShops();
            StringAssert.Contains("已经是你的店", s.LeaseBlocker(s.DebugAddListing("pc", 0, 60), true));
            Assert.AreEqual(2, s.MyShopCount);
        }

        [Test]
        public void SaveLoad_KeepsAllShops()
        {
            GameSession s = TwoShops();
            GameSessionTests.Teleport(s, "tea");
            s.ShopAt("tea").Sim.Markup = 1.4;
            GameSession b = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual(2, b.MyShopCount);
            Assert.AreEqual("pc", b.MainShop.Place.Id, "主店还是主店");
            Assert.AreEqual("tea", b.PlayerShopPlace.Id, "人在哪家店就是当前店");
            Assert.AreEqual(1.4, b.ShopAt("tea").Sim.Markup, 1e-9);
            Assert.AreEqual(3000, b.RentOf(b.ShopAt("tea")).Yuan);
            Assert.AreEqual(s.MainShop.Sim.Stock.Lines().Sum(l => l.Count), b.MainShop.Sim.Stock.Lines().Sum(l => l.Count));
            Assert.IsFalse(b.IsVacant(b.City.GetPlace("pc")));
            Assert.IsFalse(b.IsVacant(b.City.GetPlace("tea")));
        }

        [Test]
        public void CloseShop_InsideAndEmpty_ForfeitsDepositWithoutChargingAgain()
        {
            GameSession s = TwoShops();
            var part = s.MainShop.Sim.Stock.Lines().First().Part;
            s.ShopAt("tea").Sim.Stock.ForceAdd(part, true, 3);
            GameSessionTests.Teleport(s,"flat");
            StringAssert.Contains("在这家店里", s.CloseShopBlocker(s.ShopAt("tea")), "退租要在店里办");
            GameSessionTests.Teleport(s, "tea");
            StringAssert.Contains("货车", s.CloseShopBlocker(s.ShopAt("tea")), "有货先叫货车搬走");
            s.ShopAt("tea").Sim.Stock.Clear();
            Money deposit = s.ShopAt("tea").Lease.Deposit;
            Money before = s.Ledger.Balance;
            Assert.IsTrue(s.CloseShop("tea"));
            Assert.IsNull(s.CurrentPlace, "退完走到门口");
            Assert.AreEqual(1, s.MyShopCount);
            Assert.AreEqual("pc", s.PlayerShopPlace.Id);
            Assert.Greater(deposit.Cents,0);
            Assert.AreEqual(before.Cents, s.Ledger.Balance.Cents, "主动退租不退押金，也不重复扣除");
            Assert.IsTrue(s.IsVacant(s.City.GetPlace("tea")));
            Assert.IsTrue(s.Listings.Any(l => l.PlaceId == "tea"), "铺子重新挂牌");
            GameSessionTests.Teleport(s, "pc");
            StringAssert.Contains("货车", s.CloseShopBlocker(s.MainShop), "最后一家店也可退，只需先搬走存货");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LastRentedShopCanBeClosedFromCounterOrAgencyAndRentedAgainAfterSaving(bool fromAgency)
        {
            using(var s=TwoShops())
            {
                GameSessionTests.Teleport(s,"pc");s.PlayerShop.Stock.Clear();
                Assert.IsTrue(s.CloseShop("pc"));
                GameSessionTests.Teleport(s,"tea");
                var shop=s.CurrentShop;shop.Sim.Stock.Clear();
                var agency=s.AgencyFor(shop.Place);
                if(fromAgency)GameSessionTests.Teleport(s,agency.Id);
                Assert.AreEqual(1,s.MyShopCount);Assert.Greater(shop.Lease.Deposit.Cents,0);
                var furniture=shop.Layout.GroupBy(f=>f.Id).ToDictionary(g=>g.Key,g=>s.FurnitureInStock(g.Key)+g.Count());
                var before=s.Ledger.Balance;
                var root=new UnityEngine.GameObject("LastShopTerminationRegression");root.SetActive(false);
                var controller=root.AddComponent<PCShopSell.Game.GameController>();
                typeof(PCShopSell.Game.GameController).GetProperty("Session").SetValue(controller,s);
                typeof(PCShopSell.Game.GameController).GetProperty("City").SetValue(controller,s.City);
                var existingEventSystem=UnityEngine.EventSystems.EventSystem.current;
                var ui=new PCShopSell.Game.UI.GameUi(controller);
                UnityEngine.UI.Button Button(string name)=>ui.Canvases.SelectMany(c=>c.GetComponentsInChildren<UnityEngine.UI.Button>()).Single(b=>b.name==name);
                try
                {
                    if(fromAgency)
                    {
                        var detail=new UnityEngine.GameObject("AgencyDetailRegression",typeof(UnityEngine.RectTransform)).GetComponent<UnityEngine.RectTransform>();
                        detail.SetParent(ui.Canvases.First().transform,false);
                        typeof(PCShopSell.Game.UI.GameUi).GetMethod("BuildAgencyDetail",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                            .Invoke(ui,new object[]{detail,shop.Place,1,null});
                        shop.Sim.Stock.ForceAdd(PCShopSell.Core.Catalog.PartCatalog.Get("cpu0"),true,1);
                        Button("AgencyCloseShop_"+shop.Place.Id).onClick.Invoke();
                        Assert.IsFalse(Button("ConfirmCloseShop").interactable,"中介退租仍须先搬走存货");
                        Assert.IsFalse(s.CloseShop(shop.Place.Id));Assert.AreEqual(agency,s.CurrentPlace);
                        Assert.AreEqual(before,s.Ledger.Balance);Assert.AreEqual(1,s.MyShopCount);
                        ui.CloseTopWindow();shop.Sim.Stock.Clear();
                        Button("AgencyCloseShop_"+shop.Place.Id).onClick.Invoke();
                    }
                    else{ui.OpenDock();Button("CloseShopCounter").onClick.Invoke();}
                    Assert.IsTrue(Button("ConfirmCloseShop").interactable);
                    StringAssert.Contains("押金不退",Button("ConfirmCloseShop").GetComponentInChildren<UnityEngine.UI.Text>().text);
                    Button("ConfirmCloseShop").onClick.Invoke();
                    Assert.IsFalse(s.HasShop);Assert.AreEqual(0,s.MyShopCount);
                    if(fromAgency)Assert.AreEqual(agency,s.CurrentPlace,"在中介办理后应留在中介");else Assert.IsNull(s.CurrentPlace);
                    Assert.AreEqual(before,s.Ledger.Balance);
                    foreach(var item in furniture)Assert.AreEqual(item.Value,s.FurnitureInStock(item.Key));
                    Assert.IsFalse(s.CloseShop("tea"),"重复点击不能重复结算");
                    using(var loaded=GameSession.Load(s.City,UnityEngine.JsonUtility.FromJson<SaveData>(UnityEngine.JsonUtility.ToJson(s.CreateSave()))))
                    {
                        Assert.IsFalse(loaded.HasShop);Assert.AreEqual(0,loaded.MyShopCount);
                        Assert.AreEqual(before,loaded.Ledger.Balance);
                        Assert.IsTrue(loaded.IsVacant(loaded.City.GetPlace("tea")));
                        GameSessionTests.Teleport(loaded,"agency");
                        Assert.IsTrue(loaded.SignLease("tea"));
                        Assert.AreEqual(1,loaded.MyShopCount);Assert.IsTrue(loaded.HasShop);
                    }
                }
                finally
                {
                    ui.CloseAllWindows();
                    foreach(var canvas in ui.Canvases)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                    if(existingEventSystem==null && UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }

        [Test]
        public void CloseMainShop_NextShopBecomesMain()
        {
            GameSession s = TwoShops();
            s.PlayerShop.Reputation = 55;
            GameSessionTests.Teleport(s, "pc");
            s.PlayerShop.Stock.Clear();
            Assert.IsNull(s.CloseShopBlocker(s.MainShop));
            Assert.IsTrue(s.CloseShop("pc"));
            Assert.AreEqual("tea", s.MainShop.Place.Id);
            Assert.AreEqual(GameSession.MyShopName, s.PlaceTitle(s.City.GetPlace("tea")));
            Assert.AreEqual(55, s.PlayerShop.Reputation, 1e-6, "知名度跟着新主店");
            GameSession b = GameSession.Load(s.City, s.CreateSave());
            Assert.AreEqual("tea", b.MainShop.Place.Id);
            Assert.AreEqual(1, b.MyShopCount);
        }

        [Test]
        public void Courier_CanDeliverToAnyOwnShop()
        {
            GameSession s = TwoShops();
            Assert.AreEqual("tea", s.PlayerShopPlace.Id);
            GameSessionTests.Teleport(s, "flat");
            s.HomeBoxParts.ForceAdd(s.MainShop.Sim.Stock.Lines().First().Part, true, 2);
            var part = s.HomeBoxParts.Lines().First().Part;
            int before = s.MainShop.Sim.Stock.Count(part, true);
            s.SetCourierDest(s.MainShop);
            Assert.AreEqual(s.MainShop, s.TruckDest, "可以选不是当前店的那家");
            string key = s.MovableHere().First(m => m.Key.StartsWith("box|part:")).Key;
            Assert.IsTrue(s.AddToTruck(key, 2));
            Assert.AreEqual(2, s.HomeBoxParts.Count(part, true), "装货单还没出发，东西还在原处");
            Money fee = s.TruckFreight;
            Money cash = s.Ledger.Balance;
            Assert.IsTrue(s.DispatchTruck());
            Assert.AreEqual((cash - fee).Cents, s.Ledger.Balance.Cents, "出发时一起付运费");
            Assert.AreEqual(0, s.HomeBoxParts.Count(part, true));
            Assert.AreEqual("pc", s.Couriers.Single().ShopId);
            s.AdvanceClock(s.CourierQuote(s.MainShop).minutes + 5);
            Assert.AreEqual(before + 2, s.MainShop.Sim.Stock.Count(part, true), "送进了主店，不是当前店");
            Assert.AreEqual(0, s.ShopAt("tea").Sim.Stock.Count(part, true));
        }

        [Test]
        public void Truck_CannotSendToWhereYouStand()
        {
            GameSession s = TwoShops();
            GameSessionTests.Teleport(s, "flat");
            s.SetCourierDest(null);
            Assert.IsNotNull(s.TruckDestBlocker, "人在家不能叫车送家里");
            s.SetCourierDest(s.ShopAt("tea"));
            Assert.IsNull(s.TruckDestBlocker);
        }

        [Test]
        public void Cart_SplitsBagAndTruck_FreightPaidOnce()
        {
            GameSession s = GameSessionTests.Make(200000, 10 * 60);
            Place stall = s.City.GetPlace("stall");
            GameSessionTests.Teleport(s, "stall");
            var part = PCShopSell.Core.Catalog.PartCatalog.All.First(p => s.StallSells(stall, p));
            Assert.IsTrue(s.CartAddStall(stall, part, 3));
            Assert.AreEqual(0, s.Ledger.Entries.Count(e => e.Category == "进货"), "加进购物车还没付钱");
            s.CartAllToTruck(); // Explicit split: one in bag, remaining items by truck.
            Assert.IsTrue(s.CartToBag("part:" + part.Id, 1));
            Assert.AreEqual(1, s.CartCars(true), "剩下两件一辆车");
            Money cash = s.Ledger.Balance;
            Money due = s.CartGoods + s.CartFreight(true);
            Assert.IsNull(s.CheckoutBlocker(true));
            Assert.IsTrue(s.Checkout(true));
            Assert.AreEqual((cash - due).Cents, s.Ledger.Balance.Cents, "货款 + 一辆车的运费");
            Assert.AreEqual(1, s.Cargo.Count(part, true), "一件自己背");
            Assert.AreEqual(2, s.Couriers.Sum(r => r.Items.Sum(i => i.Count)), "两件叫车");
            Assert.AreEqual(1, s.Ledger.Entries.Count(e => e.Category == "运费"), "运费只收一次");
            Assert.AreEqual(0, s.Cart.Count);
        }

        [Test]
        public void Cart_BagOverweight_Blocks()
        {
            GameSession s = GameSessionTests.Make(200000, 10 * 60);
            Place stall = s.City.GetPlace("stall");
            GameSessionTests.Teleport(s, "stall");
            var part = PCShopSell.Core.Catalog.PartCatalog.All.First(p => s.StallSells(stall, p));
            int n = s.StallRemaining(stall, part);
            Assert.IsTrue(s.CartAddStall(stall, part, n));
            s.CartToBag("part:" + part.Id, n);
            Assume.That(s.CartBagKg > s.FreeKg, "老板给的不够多，背包装得下");
            StringAssert.Contains("背包放不下", s.CheckoutBlocker(true));
            s.CartAllToTruck();
            s.CartFillBag();
            Assert.IsNull(s.CheckoutBlocker(true), "背得动的放背包，剩下叫车");
            Assert.LessOrEqual(s.CartBagKg, s.FreeKg + 1e-9);
        }

        [Test]
        public void SecondLease_AsMove_StillRelocates()
        {
            GameSession s = GameSessionTests.Make(200000);
            s.PlayerShop.Reputation = 80;
            s.DebugAddListing("tea", 0, 60, Money.FromYuan(3000));
            GameSessionTests.Teleport(s, "agency");
            Assert.IsTrue(s.SignLease("tea"));
            Assert.AreEqual(1, s.MyShopCount);
            Assert.AreEqual("tea", s.PlayerShopPlace.Id);
            Assert.IsTrue(s.IsVacant(s.City.GetPlace("pc")));
        }
    }
}
