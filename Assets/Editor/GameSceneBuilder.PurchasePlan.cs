using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        public static void PurchasePlanFromBatch() => RunSmokeBatch(SmokePurchasePlan);
        private static void SmokePurchasePlan()
        {
            SmokeWorkforce();
            var s = _game.Session;
            _game.Ui.CloseAllWindows();
            s.Clock.Restore(s.Clock.Today, 9 * 60 + 5);
            var buyer = s.Employees.First(x => x.Role == EmployeeRole.Buyer);
            var offers = s.EmployeePurchaseCatalog();
            var cpu = offers.First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "cpu0");
            var ram = offers.First(o => o.Kind == PurchaseGoodsKind.Part && o.Id == "ram0");
            var drink = offers.First(o => o.Kind == PurchaseGoodsKind.Gift && o.Id == Gifts.Drink);
            Check(s.ConfigureEmployeePurchase(buyer.Person.Id, new EmployeePurchasePlan
            {
                Transport = VehicleKind.Taxi,
                Items = new[] { cpu.Line(2), ram.Line(2), drink.Line(3) }
            }), "可把两个档口和一家商店合到同一采购清单");
            _game.Ui.OpenEmployeePurchase(buyer.Person.Id);
            Frame(0);
            Check(ActiveButtons().Any(b => ButtonText(b) == "完成"), "完成按钮始终固定在采购窗口底部");
            Check(!ActiveButtons().Any(b => ButtonText(b).Contains("设为本趟费用")), "不需要预算输入或额外预算按钮");
            Shot("vs1_procurement_01_all_sources");
            Shot("vs1_procurement_02_all_sources_720", 1280, 720);
            ClickGuideButton("全部货源  ▼");
            Shot("vs1_procurement_03_source_picker");
            ClickGuideButton(cpu.Source.Name);
            var texts = _game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Where(t => t.gameObject.activeInHierarchy).Select(t => t.text).ToArray();
            Check(texts.Any(t => t.Contains(ram.Name)) && texts.Any(t => t.Contains(drink.Name)), "筛选CPU档口后，内存和饮料仍在已选清单中");
            Shot("vs1_procurement_04_filtered_keeps_list");
            ClickGuideButton("完成");
            Check(!_game.Ui.AnyModalOpen && !s.Time.IsHeld, "点完成保存后关闭窗口，不残留暂停");
            Check(buyer.Person.PurchasePlan.Daily && buyer.Person.PurchasePlan.Items.Length == 3, "完成后启用整份多源清单每日采购");
            s.AdvanceClock(1);
            var trip = s.EmployeePurchaseTrips.Single(t => t.EmployeeId == buyer.Person.Id);
            Check(trip.Items.Length == 3 && trip.Items.Select(x => x.SourceId).Distinct().Count() == 3, "员工一趟采购三个货源的三种商品");
            s.AdvanceClock((int)(Math.Max(trip.ReturnAt, trip.DeliveryAt) - s.Clock.Now.TotalMinutes + 1));
            Check(!s.EmployeePurchaseTrips.Any(t => t.EmployeeId == buyer.Person.Id), "整趟人和货都回来后完成，不重复扣款或入库");
            s.Clock.Restore(s.Clock.Today, 23 * 60 + 59); s.AdvanceClock(2);
            s.Clock.Restore(s.Clock.Today, 8 * 60 + 59); s.AdvanceClock(2);
            Check(s.EmployeePurchaseTrips.Any(t => t.EmployeeId == buyer.Person.Id && t.Items.Length == 3), "次日自动再次执行整份清单，不是只能进一次货");
            _game.Ui.CloseAllWindows();
            _game.Ui.OpenDock(); _game.Ui.DebugSelectTab(6);
            Check(ActiveButtons().Any(b => ButtonText(b).StartsWith("再贴招聘告示")), "保留其他候选人时也能再贴招聘告示");
            Check(ActiveButtons().Any(b => ButtonText(b) == "离开店铺"), "离店按钮只显示离开店铺");
            Shot("vs1_procurement_05_recruit_again");
        }
    }
}
