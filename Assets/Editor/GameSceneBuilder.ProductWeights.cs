using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Trade;
using PCShopSell.Core.World;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void ProductWeightsFromBatch() => RunSmokeBatch(() =>
        {
            // No screenshot capture: inspect actual UI text against carrying data.
            ShaderUtil.allowAsyncCompilation = false; UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildRig(); _game = new GameObject("Game").AddComponent<GameController>();
            _game.Bootstrap(false, 7); _game.Ui.QuestPopups = false;
            var s = _game.Session; s.EventsEnabled = false; s.DebugAddCash(500000); s.SetSmartphone(true);
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            bool HasWeight(string name, double kg) => _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>())
                .Any(t => t.gameObject.activeInHierarchy && t.text.Contains(name) && t.text.Contains("\n<size=13><color=#A8B0BA>") && t.text.Contains(kg.ToString("0.###") + " 千克"));
            void Enter(Place p) { _game.Ui.CloseAllWindows(); Teleport(s, p.Id); _game.Ui.OnEnteredPlace(p); Frame(0); }
            var cpu = PartCatalog.Get("cpu0"); double cpuKg = Stock.SpaceOf(cpu, true, 1) * Backpack.KgPerSpace;
            var stall = s.City.Places.First(p => p.Kind == PlaceKind.Wholesaler && s.StallSells(p, cpu));
            Enter(stall); Check(HasWeight(cpu.Name, cpuKg), "档口商品名称下显示与盒装承重一致的千克重量");
            ActiveButtons().First(b => b.name == "QuantityPlus_part:cpu0").onClick.Invoke(); Frame(0);
            Check(HasWeight(cpu.Name, cpuKg) && s.CartAmount("part:cpu0") == 1, "购物车继续显示单件和本批重量");
            s.ClearCart();
            Enter(s.City.Places.First(p => p.Business == GameSession.FarmBusiness));
            Check(HasWeight("大米", .25), "散装大米说明每次加购重量");
            ClickGuideButton("菜蛋肉"); Check(HasWeight(Ingredients.All.First().Name, Ingredients.All.First().WeightKg), "食材名称下显示每份实际千克数");
            Enter(s.City.Places.First(p => p.Business == Gifts.TobaccoBusiness));
            Check(HasWeight(Gifts.Get(Gifts.Cig).Name, Gifts.Get(Gifts.Cig).WeightKg), "实体礼品显示重量");
            _game.Ui.CloseAllWindows(); _game.Ui.OpenOnlinePanel(); Frame(0);
            var furniture = FurnitureCatalog.Purchasable(false).First();
            Check(HasWeight(furniture.Name, furniture.WeightKg), "网购家具名称下显示实际重量");
            ClickGuideButton("礼品"); Check(HasWeight(Gifts.All.First().Name, Gifts.All.First().WeightKg), "网购礼品显示重量");
            ClickGuideButton("大米"); Check(HasWeight("大米", GameSession.RiceBagKg), "网购大米显示整袋重量而非散装步长");
            _game.Ui.CloseAllWindows(); s.Online.Discover(Online1788.Stores[0].Id); _game.Ui.OpenWholesaleApp(); Frame(0);
            ActiveButtons().First(b => ButtonText(b).StartsWith(Online1788.Stores[0].Name)).onClick.Invoke(); Frame(0);
            Check(HasWeight(cpu.Name, cpuKg), "1788批发商品显示盒装实际重量");
            _game.Ui.CloseAllWindows(); s.DebugRentStarterShop(); Enter(s.MainShop.Place);
            s.MainShop.Buyer = new Employee { Name = "测试采购", DailyWageYuan = 150 };
            var buyer = s.Employees.First(e => e.Role == EmployeeRole.Buyer);
            _game.Ui.OpenEmployeePurchase(buyer.Person.Id); Frame(0);
            Check(HasWeight(cpu.Name, cpuKg), "员工采购商品显示UnitKg重量");
            _game.Ui.CloseAllWindows();
            int shelf = s.ShelfIndexes().First(); s.SetShelfKind(shelf, ShelfKinds.ForCategory(PartCategory.Cpu).Id);
            s.PlayerShop.Stock.TryAdd(cpu, true, 2); s.PlayerShop.Stock.TryAdd(cpu, false, 2);
            _game.Ui.OpenShelfWindow(shelf); Frame(0);
            Check(HasWeight(cpu.Name, cpuKg) && HasWeight(cpu.Name, Stock.SpaceOf(cpu, false, 1) * Backpack.KgPerSpace), "货架盒装与散件分别显示不同实际重量");
            _game.Ui.CloseAllWindows();
        });
    }
}
