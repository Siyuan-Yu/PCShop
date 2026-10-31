using System.IO;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void WorkforceFromBatch() => RunSmokeBatch(SmokeWorkforce);

        private static void SmokeWorkforce()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/previews"));
            Directory.CreateDirectory(_outDir);
            ShaderUtil.allowAsyncCompilation = false;
            UrpSetup.Ensure();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = BuildRig();
            _game = new GameObject("Game").AddComponent<GameController>();
            _game.Bootstrap(false, 7);
            _game.Ui.QuestPopups = false;
            foreach (var c in _game.Ui.Canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1;
            }
            var s = _game.Session;
            s.DebugAddCash(500000);
            Check(s.DebugRentStarterShop(), "员工验收：开一家测试店");
            string mainId = s.MainShop.Place.Id;
            s.PlayerShop.Reputation = 80;
            var second = s.City.OfKind(PlaceKind.Shop).First(p => p.Id != mainId);
            s.DebugAddListing(second.Id, 0, 60, Money.FromYuan(3000));
            Teleport(s, s.City.FirstOfKind(PlaceKind.Agency).Id);
            Check(s.SignLease(second.Id, true), "员工验收：开第二家店");
            Teleport(s, s.City.FirstOfKind(PlaceKind.Dealer).Id);
            Check(s.BuyVehicle(VehicleKind.EBike) && s.BuyVehicle(VehicleKind.EBike), "员工验收：可买两台小电驴");
            Check(s.BuyVehicle(VehicleKind.Tricycle), "员工验收：购买三轮车");
            s.MainShop.Employee = new Employee { Name = "王晓燕", DailyWageYuan = 160, CheckoutMinutes = 2 };
            s.MainShop.Assembler = new Employee { Name = "李建国", DailyWageYuan = 210, CheckoutMinutes = 3 };
            var buyer = new Employee { Name = "陈小明", DailyWageYuan = 150, CheckoutMinutes = 3 };
            s.MainShop.Buyer = buyer;
            s.ConfigureEmployeePurchase(buyer.Id, new EmployeePurchasePlan
            {
                BudgetYuan = 3000, Transport = VehicleKind.EBike,
                Items = new[] { new PurchaseLine { PartId = "cpu0", Count = 2 }, new PurchaseLine { PartId = "ram0", Count = 2 } }
            });
            Teleport(s, mainId);
            s.Clock.Restore(s.Clock.Today, 9 * 60 + 20);
            Frame(.01f);
            _game.Ui.CloseModalWindows();
            Shot("vs1_employees_hud");
            _game.Ui.OpenEmployeesPanel();
            Frame(0);
            Shot("vs1_employees_list");
            ClickGuideButton("安排明天调店");
            Shot("vs1_employees_transfer");
            _game.Ui.CloseModalWindows();
            _game.Ui.OpenEmployeesPanel();
            ClickGuideButton("安排进货");
            Frame(0);
            Shot("vs1_employees_purchase");
            Shot("vs1_employees_purchase_720", 1280, 720);
            var dispatch = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<UnityEngine.UI.Button>())
                .Last(b => b.GetComponentInChildren<UnityEngine.UI.Text>()?.text.StartsWith("按清单派出（付") == true);
            Check(dispatch.interactable, "员工验收：采购派出按钮可点击");
            dispatch.onClick.Invoke();
            Check(!_game.Ui.AnyModalOpen && !s.Time.IsHeld, "派出后关闭员工列表及采购单，不残留暂停");
            Check(s.BusyVehicleCount(VehicleKind.EBike) == 1 && s.FreeVehicleCount(VehicleKind.EBike) == 1, "员工验收：占一台，另一台仍可用");
            _game.Ui.OpenFleetPanel();
            Frame(0);
            Shot("vs1_employees_fleet");
            _game.Ui.CloseModalWindows();
            ClickGuideButton("叫货车");
            Check(_game.Ui.AnyModalOpen, "底栏叫货车入口打开装货单");
            Shot("vs1_employees_freight");
            _game.Ui.CloseModalWindows();
            _game.Ui.OpenDock();
            _game.Ui.DebugSelectTab(6);
            Frame(0);
            Shot("vs1_employees_staff_tab");
        }
    }
}
