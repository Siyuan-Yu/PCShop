using System.Linq;
using NUnit.Framework;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Game;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Tests
{
    public class ShelfItemCapacityTests
    {
        private static GameSession Make(string shelf, string kind)
        {
            var s = GameSessionTests.Make(500000, 600);
            s.EventsEnabled = false;
            GameSessionTests.Teleport(s, "pc");
            s.CurrentShop.Layout.Clear();
            s.PlayerShop.Stock.Clear();
            AddShelf(s, shelf, kind);
            return s;
        }

        private static void AddShelf(GameSession s, string shelf, string kind)
        {
            int index = s.CurrentShop.Layout.Count;
            s.CurrentShop.Layout.Add(new PlacedFurniture { Id = shelf, X = 2 + index, Z = 2 });
            Assert.True(s.SetShelfKind(index, kind));
        }

        [TestCase(FurnitureCatalog.Shelf, 20, 60, false)]
        [TestCase(FurnitureCatalog.RackStrong, 30, 100, false)]
        [TestCase(FurnitureCatalog.RackHeavy, 40, 160, false)]
        [TestCase(FurnitureCatalog.Shelf, 20, 60, true)]
        [TestCase(FurnitureCatalog.RackStrong, 30, 100, true)]
        [TestCase(FurnitureCatalog.RackHeavy, 40, 160, true)]
        public void LightModelsAndClothesHitItemLimitFirst(string shelf, int count, int kg, bool clothes)
        {
            string id = (clothes ? ClothingProducts.All : ModelProducts.All).First().Id;
            var kind = ShelfKinds.ForGift(id);
            using (var s = Make(shelf, kind.Id))
            {
                s.PlayerShop.Stock.ForceAddGoods(id, 100);
                var fill = s.PlayerShop.ShelfFillOf(kind);
                Assert.AreEqual(count, fill.CapacityItems);
                Assert.AreEqual(kg, fill.CapacityKg);
                Assert.AreEqual(count, s.PlayerShop.OnShelfGoods(id));
                Assert.Less(fill.UsedKg, fill.CapacityKg);
                Assert.AreEqual(100, s.PlayerShop.Stock.GoodsCount(id));
            }
        }

        [Test]
        public void HeavyGoodsStillHitWeightLimitFirst()
        {
            using (var s = Make(FurnitureCatalog.Shelf, "gpu"))
            {
                var gpu = PartCatalog.InCategory(PartCategory.Gpu).First();
                s.PlayerShop.Stock.ForceAdd(gpu, true, 100);
                var fill = s.PlayerShop.ShelfFillOf(gpu.Category);
                Assert.AreEqual(20, fill.CapacityItems);
                Assert.AreEqual(15, fill.Total);
                Assert.AreEqual(60, fill.UsedKg);
            }
        }

        [Test]
        public void MultipleModelsShareOneCountLimit_AndRefillWithoutLosingWarehouseGoods()
        {
            var kind = ShelfKinds.ForGift(ModelProducts.All.First().Id);
            using (var s = Make(FurnitureCatalog.Shelf, kind.Id))
            {
                foreach (string id in kind.GiftIds) s.PlayerShop.Stock.ForceAddGoods(id, 100);
                var fill = s.PlayerShop.ShelfFillOf(kind);
                Assert.AreEqual(20, fill.Total);
                Assert.Greater(fill.Goods.Count, 1);
                string sold = fill.Goods.First().Key;
                Assert.True(s.PlayerShop.Stock.TryRemoveGoods(sold, 3));
                Assert.AreEqual(20, s.PlayerShop.ShelfFillOf(kind).Total);
                Assert.AreEqual(97, s.PlayerShop.Stock.GoodsCount(sold));
            }
        }

        [Test]
        public void DifferentShelfTiersAddTogether_ChangingCategoryReturnsOverflowToWarehouse()
        {
            string id = ModelProducts.All.First().Id;
            var kind = ShelfKinds.ForGift(id);
            using (var s = Make(FurnitureCatalog.Shelf, kind.Id))
            {
                AddShelf(s, FurnitureCatalog.RackHeavy, kind.Id);
                s.PlayerShop.Stock.ForceAddGoods(id, 100);
                var fill = s.PlayerShop.ShelfFillOf(kind);
                Assert.AreEqual(60, fill.Total); Assert.AreEqual(60, fill.CapacityItems);
                Assert.AreEqual(220, fill.CapacityKg);
                Assert.True(s.SetShelfKind(1, "drink"));
                Assert.AreEqual(20, s.PlayerShop.OnShelfGoods(id));
                Assert.AreEqual(100, s.PlayerShop.Stock.GoodsCount(id));
                Assert.True(s.SetShelfKind(0, null));
                Assert.AreEqual(0, s.PlayerShop.OnShelfGoods(id));
                Assert.AreEqual(100, s.PlayerShop.Stock.GoodsCount(id));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void DisplayAndShelfAddFiniteLimitsInEitherLayoutOrder(bool displayFirst)
        {
            string id = ModelProducts.All.First().Id;
            var kind = ShelfKinds.ForGift(id);
            using (var s = Make(FurnitureCatalog.Shelf, kind.Id))
            {
                var desk = new PlacedFurniture { Id = FurnitureCatalog.ShopDisplay, Item = kind.Id, DisplayLimit = 6, X = 4, Z = 2 };
                if (displayFirst) s.CurrentShop.Layout.Insert(0, desk);
                else s.CurrentShop.Layout.Add(desk);
                int index = displayFirst ? 0 : 1;
                Assert.True(s.SetDisplayLimit(index, 6));
                s.PlayerShop.Stock.ForceAddGoods(id, 100);
                Assert.AreEqual(26, s.PlayerShop.ShelfFillOf(kind).CapacityItems);
                Assert.AreEqual(26, s.PlayerShop.OnShelfGoods(id));
                Assert.AreEqual(6, s.DisplayFill(index).Total);
                Assert.AreEqual(6, s.DisplayFill(index).CapacityItems);
                Assert.True(s.SetDisplayLimit(index, 1));
                Assert.AreEqual(21, s.PlayerShop.OnShelfGoods(id));
            }
        }

        [TestCase(FurnitureCatalog.Shelf)]
        [TestCase(FurnitureCatalog.ClothingRack)]
        [TestCase(FurnitureCatalog.ClothingFold)]
        [TestCase("shelf_gpu")]
        public void ExistingFurnitureAndLegacyAliasesUseCountLimitAfterJsonReload(string shelf)
        {
            using (var s = Make(shelf, "cpu"))
            {
                var cpu = PartCatalog.InCategory(PartCategory.Cpu).First();
                s.PlayerShop.Stock.ForceAdd(cpu, false, 100);
                var data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s.CreateSave()));
                using (var loaded = GameSession.Load(s.City, data))
                {
                    Assert.AreEqual(20, loaded.PlayerShop.OnShelf(cpu));
                    Assert.AreEqual(100, loaded.PlayerShop.Stock.CountAll(cpu));
                }
            }
        }

        [Test]
        public void UnlimitedWeightCannotBypassExplicitCountLimit()
        {
            var stock = new Stock(double.PositiveInfinity);
            stock.ForceAddGoods(Gifts.Cig, 500);
            var kind = ShelfKinds.ForGift(Gifts.Cig);
            Assert.AreEqual(20, ShelfPlanner.Fill(stock, kind, double.PositiveInfinity, 20).Total);
            Assert.AreEqual(0, ShelfPlanner.Fill(stock, kind, 60, 0).Total);
        }

        [Test]
        public void InventoryAndShelfDetailsShowBothLimits_EvenBeforeChoosingCategory()
        {
            using (var s = Make(FurnitureCatalog.Shelf, "smoke"))
            {
                s.PlayerShop.Stock.ForceAddGoods(Gifts.Cig, 100);
                var root = new GameObject("ShelfItemCapacityUi"); root.SetActive(false);
                var controller = root.AddComponent<GameController>();
                typeof(GameController).GetProperty("Session").SetValue(controller, s);
                typeof(GameController).GetProperty("City").SetValue(controller, s.City);
                var existing = UnityEngine.EventSystems.EventSystem.current;
                var ui = new GameUi(controller);
                Text[] Texts() => ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).ToArray();
                try
                {
                    ui.OpenDock(); ui.DebugSelectTab(1);
                    string text = Texts().Single(t => t.name == "ShelfCapacity_0").text;
                    StringAssert.Contains("20/20 件", text); StringAssert.Contains("/60 千克", text);
                    ui.OpenShelfWindow(0);
                    Assert.True(Texts().Any(t => t.text.Contains("本架最多 20 件、60 千克")));
                    ui.CloseAllWindows(); Assert.True(s.SetShelfKind(0, null));
                    ui.OpenDock(); ui.DebugSelectTab(1);
                    text = Texts().Single(t => t.name == "ShelfCapacity_0").text;
                    StringAssert.Contains("0/20 件", text); StringAssert.Contains("0/60 千克", text);
                }
                finally
                {
                    ui.CloseAllWindows();
                    foreach (var canvas in ui.Canvases) Object.DestroyImmediate(canvas.gameObject);
                    Object.DestroyImmediate(root);
                    if (existing == null && UnityEngine.EventSystems.EventSystem.current != null)
                        Object.DestroyImmediate(UnityEngine.EventSystems.EventSystem.current.gameObject);
                }
            }
        }
    }
}
