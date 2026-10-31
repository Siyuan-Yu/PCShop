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
            s.MainShop.Employee = new Employee { Name = "王晓燕", DailyWageYuan = 160, CheckoutMinutes = 2, AssemblyMinutes = 120, PurchaseMinutes = 5 };
            s.MainShop.Assembler = new Employee { Name = "李建国", DailyWageYuan = 210, CheckoutMinutes = 5, AssemblyMinutes = 70, PurchaseMinutes = 6 };
            var buyer = new Employee { Name = "陈小明", DailyWageYuan = 150, CheckoutMinutes = 4, AssemblyMinutes = 110, PurchaseMinutes = 2 };
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
            ClickGuideButton("安排工作");
            ClickGuideButton("收银员");
            ClickGuideButton($"{s.ShopTitle(s.ShopAt(second.Id))}（{second.Name}）");
            Shot("vs1_employees_transfer");
            Shot("vs1_employees_work_assignment");
            ClickGuideButton("确认明天到新店做收银员");
            Check(s.FindEmployee(buyer.Id).Shop.Place.Id == mainId && buyer.TransferShopId == second.Id && buyer.TransferRole == (int)EmployeeRole.Cashier,
                "工作安排提交后今天仍进货，明天跨店做收银");
            Shot("vs1_employees_transfer_scheduled");
            _game.Ui.CloseModalWindows();
            _game.Ui.OpenEmployeesPanel();
            ClickGuideButton("安排进货");
            Frame(0);
            Shot("vs1_employees_purchase");
            Shot("vs1_employees_purchase_720", 1280, 720);
            ClickGuideButton("电子市场：按品类找档口  ▼");
            Shot("vs1_employees_source");
            _game.Ui.CloseTopWindow();
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
            _game.Ui.CloseAllWindows();
            Check(s.PostJobAd(), "满员也可以招聘，先雇员工再安排工作");
            s.Clock.Restore(s.Clock.Today, 23 * 60 + 59);
            s.AdvanceClock(2);
            Check(s.FindEmployee(buyer.Id).Shop.Place.Id == second.Id && s.FindEmployee(buyer.Id).Role == EmployeeRole.Cashier,
                "次日同一个员工到新店换成收银岗位");
            s.Clock.Restore(s.Clock.Today, 8 * 60 + 59);
            s.AdvanceClock(2);
            _game.Ui.CloseAllWindows();
            _game.Ui.OpenDock();
            _game.Ui.DebugSelectTab(6);
            Frame(0);
            Shot("vs1_employees_recruitment");
            string recruitId = s.Candidates.Last().Id;
            ClickGuideButton("聘用并安排工作");
            Check(s.FindEmployee(recruitId)?.Role == EmployeeRole.Unassigned, "聘用先进入待安排，不绑定职业");
            ClickGuideButton("进货员");
            Shot("vs1_employees_newhire_assignment");
            ClickGuideButton("确认今天起做进货员");
            Check(s.FindEmployee(recruitId)?.Role == EmployeeRole.Buyer, "新员工无职业门槛可直接安排进货");
        }
    }
}
