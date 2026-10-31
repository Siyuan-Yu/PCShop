using System;
using System.Linq;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Catalog;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void InteractionsFromBatch() => RunSmokeBatch(SmokeInteractions);

        private static Button[] ActiveButtons() => _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Button>())
            .Where(b => b.gameObject.activeInHierarchy).ToArray();
        private static string ButtonText(Button b) => b.GetComponentInChildren<Text>()?.text ?? "";
        private static InputField[] ActiveInputs() => _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<InputField>())
            .Where(i => i.gameObject.activeInHierarchy).ToArray();
        private static void SmokeInteractions()
        {
            SmokeWorkforce();
            var s = _game.Session;
            Teleport(s, s.MainShop.Place.Id);
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            Frame(0);
            _game.Ui.CloseAllWindows();

            _game.Ui.OpenEmployeesPanel();
            Shot("vs1_ux_01_employees");
            Check(!ActiveButtons().Any(b => ButtonText(b) == "辞退装机员"), "旧辞退入口已清除");
            ClickGuideButton("调整班次");
            ClickGuideButton("13:00–21:00");
            Check(!_game.Ui.Canvases.Any(c => c.GetComponentsInChildren<Text>().Any(t => t.text == "选择班次")), "选班后回员工页");
            _game.Ui.CloseAllWindows();

            _game.Ui.OpenDock();
            _game.Ui.DebugSelectTab(1);
            _game.Ui.CloseDock();
            _game.Ui.OpenDock();
            Check(ActiveButtons().Any(b => ButtonText(b).StartsWith("店里纸箱")), "重新开店侧栏记住库存页");
            _game.Ui.DebugSelectTab(6);
            Check(!ActiveButtons().Any(b => ButtonText(b).StartsWith("员工管理（")), "招聘页移除重复员工管理入口");
            Shot("vs1_ux_02_recruitment");
            _game.Ui.CloseAllWindows();

            var buyer = s.Employees.First(x => x.Role == EmployeeRole.Buyer);
            s.ConfigureEmployeePurchase(buyer.Person.Id, new EmployeePurchasePlan
            {
                BudgetYuan = 3000,
                Items = new[] { new PurchaseLine { PartId = "cpu0", Count = 2 }, new PurchaseLine { PartId = "ram0", Count = 2 } }
            });
            _game.Ui.OpenEmployeePurchase(buyer.Person.Id);
            Shot("vs1_ux_03_purchase_1080");
            Shot("vs1_ux_04_purchase_720", 1280, 720);
            var quantity = ActiveInputs().First(i => i.contentType == InputField.ContentType.IntegerNumber && i.transform.parent.GetComponentsInChildren<Text>().Any(t => t.text.StartsWith(PartCatalog.Get("cpu0").Name)));
            quantity.text = "17"; quantity.onEndEdit.Invoke("17");
            _game.Ui.CloseModalWindows();
            _game.Ui.OpenEmployeePurchase(buyer.Person.Id);
            Check(ActiveInputs().Any(i => i.text == "17"), "关闭后采购草稿数量保留");
            ClickGuideButton("全部 ▾"); ClickGuideButton("CPU");
            Check(!ActiveButtons().Any(b => ButtonText(b) == "全部 ▾"), "采购品类筛选可直接选择");
            Shot("vs1_ux_05_purchase_filtered");
            var cpuStall = s.City.OfKind(PCShopSell.Core.World.PlaceKind.Wholesaler)
                .First(p => s.StallSells(p, PartCatalog.Get("cpu0")) && !s.StallSells(p, PartCatalog.Get("ram0")));
            ClickGuideButton("全部货源  ▼");
            ClickGuideButton(cpuStall.Name);
            var purchaseTexts = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Where(t => t.gameObject.activeInHierarchy).Select(t => t.text).ToArray();
            Check(!purchaseTexts.Any(t => t.Contains("该档口不卖") || t.Contains("在选定档口没有卖")) && purchaseTexts.Any(t => t.Contains(PartCatalog.Get("ram0").Name)),
                "筛选CPU档口后已选内存仍然保留，不留下不售商品阻拦");
            Check(ActiveInputs().Any(i => i.text == "17"), "切档口保留可售型号已选数量");
            var cpuQuantity = ActiveInputs().First(i => i.contentType == InputField.ContentType.IntegerNumber);
            cpuQuantity.text = "2"; cpuQuantity.onEndEdit.Invoke("2");
            Frame(0);
            ClickGuideButton("完成");
            Check(buyer.Person.PurchasePlan.Items.Length == 2 && buyer.Person.PurchasePlan.Daily, "完成保存跨档口全部商品，并启用每天采购");
            _game.Ui.OpenEmployeePurchase(buyer.Person.Id);
            Check(ActiveButtons().Any(b => ButtonText(b) == "完成"), "完成按钮在底部始终可用");
            Shot("vs1_ux_19_stall_only");
            Shot("vs1_ux_20_stall_only_720", 1280, 720);
            s.SetVehicle(VehicleKind.Tricycle);
            ActiveButtons().Last(b => ButtonText(b).StartsWith("三轮车（空闲")).onClick.Invoke();
            Check(GuideButton("老板改为步行，释放这台车") != null, "采购单内可直接释放老板占用车辆");
            ClickGuideButton("老板改为步行，释放这台车");
            Check(s.Vehicle == VehicleKind.Walk, "采购页面释放车辆，无需返回底栏");
            _game.Ui.CloseModalWindows();

            s.SetSmartphone(true);
            _game.Ui.OpenPhone();
            var app = ActiveButtons().First(b => ButtonText(b).Contains("1788"));
            app.onClick.Invoke();
            Shot("vs1_ux_06_phone_wholesale");
            _game.Ui.CloseTopWindow();
            Check(_game.Ui.AnyModalOpen && s.Time.IsHeld, "关闭应用回手机，手机保留暂停锁");
            _game.Ui.CloseTopWindow();
            Check(!_game.Ui.AnyModalOpen && !s.Time.IsHeld, "关闭手机正确释放暂停");

            _game.Ui.OpenWholesaleApp(true);
            Check(ActiveInputs().Length >= 3, "补货政策支持直接输入，不再循环切换");
            Shot("vs1_ux_07_restock");
            _game.Ui.CloseAllWindows();

            _game.Ui.DebugQuestPopup();
            Shot("vs1_ux_08_quest_combined");
            ClickGuideButton("继续游戏");
            Check(!_game.Ui.AnyModalOpen, "任务结果与下一步一次关闭，无第二弹窗");

            _game.Ui.OpenDock();
            _game.Ui.DebugSelectTab(4);
            Shot("vs1_ux_09_renovation");
            ClickGuideButton("整理摆设（暂停中调整，完成后继续营业）");
            Frame(0);
            Check(_game.Placing && !s.IsRenovating && s.Time.IsHeld, "快速整理保持编辑模式，不消耗整天营业");
            Shot("vs1_ux_10_quick_edit");
            _game.EndPlacement();
            Check(!s.Time.IsHeld && !s.IsRenovating && !s.CanEditShop, "整理结束释放暂停和编辑权限");
            _game.Ui.CloseAllWindows();

            s.ShopTemp.AddGift(Gifts.Cig, 2);
            s.AdvanceClock(1);
            s.Clock.Restore(s.Clock.Today.AddDays(4), 10 * 60);
            Frame(0);
            Check(!_game.Ui.AnyModalOpen, "临时存放到期只提醒，不强制打开窗口");
            _game.Ui.OpenShopLockerWindow();
            Shot("vs1_ux_11_overdue_storage");
            Check(_game.Ui.CloseTopWindow(), "到期储物窗仍可关闭");
            _game.Ui.OpenBackpackPanel();
            Check(ActiveButtons().Count(b => ButtonText(b) == "关闭") == 1, "背包只保留标题栏关闭");
            Shot("vs1_ux_12_backpack");
            _game.Ui.CloseAllWindows();

            s.SetCourierDest(null);
            _game.Ui.OpenTruckWindow();
            ClickGuideButton("数量");
            Shot("vs1_ux_13_amount_picker");
            var cashBeforeLoading = s.Ledger.Balance;
            ClickGuideButton("全部");
            Check(s.TruckLoad.Count > 0 && s.Ledger.Balance == cashBeforeLoading, "数量全选批量装车，出发前不扣费");
            Shot("vs1_ux_16_truck_loaded");
            _game.Ui.CloseAllWindows();

            _game.Ui.OpenDock(); _game.Ui.DebugSelectTab(6);
            int employeesBefore = s.Employees.Count;
            ClickGuideButton("聘用并安排工作");
            Shot("vs1_ux_17_recruit_preview");
            _game.Ui.CloseTopWindow();
            Check(s.Employees.Count == employeesBefore, "关闭招聘预览不会留下待安排员工或增加工资");
            _game.Ui.CloseAllWindows();

            var assembler = s.MainShop.Assembler;
            var displaced = s.MainShop.Buyer;
            // The earlier Done action now enables daily procurement. Finish that trip before
            // testing immediate replacement; an employee still travelling must remain protected.
            displaced.PurchasePlan.Daily = false;
            var activeTrip = s.EmployeePurchaseTrips.FirstOrDefault(t => t.EmployeeId == displaced.Id);
            if (activeTrip != null)
                s.AdvanceClock((int)Math.Max(1, Math.Max(activeTrip.ReturnAt, activeTrip.DeliveryAt) - s.Clock.Now.TotalMinutes + 1));
            _game.Ui.OpenEmployeeWork(assembler.Id);
            ClickGuideButton("进货员");
            ClickGuideButton("替换安排…");
            Shot("vs1_ux_18_replace_assignment");
            ClickGuideButton("确认替换");
            Check(s.MainShop.Buyer == assembler && s.FindEmployee(displaced.Id).Role == EmployeeRole.Unassigned,
                "一次替换完成安排，原员工保留且待安排");
            _game.Ui.CloseAllWindows();

            _game.Ui.Toast("交互验收：通知支持未读提醒和回看。");
            Frame(0);
            Shot("vs1_ux_14_notifications_badge");
            _game.Ui.OpenNoticeHistory();
            Shot("vs1_ux_15_notifications");
            _game.Ui.CloseAllWindows();
            var staff = s.MainShop.Employee;
            staff.ShiftStart = 0; staff.ShiftEnd = 1440;
            s.ShopTemp.TakeGift(Gifts.Cig);
            s.ShopTemp.TakeGift(Gifts.Cig);
            _game.Ui.ConfirmLeaveShop();
            Check(!s.InPlayerShop && !_game.Ui.AnyModalOpen, "员工值守时离店直接完成，无确认窗");
            Log("interaction flow coverage complete");
        }
    }
}
