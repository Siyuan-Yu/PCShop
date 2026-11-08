using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Inventory;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PCShopSell.EditorTools
{
    public static partial class GameSceneBuilder
    {
        private static void AdjustQuantity(string key, int delta)
        {
            var button = ActiveButtons().First(b => b.name.StartsWith(delta > 0 ? "QuantityPlus_" : "QuantityMinus_") && b.name.Contains(key));
            button.GetComponent<HoldRepeat>().Commit(System.Math.Abs(delta)); Frame(0);
        }
        public static void InteractionRevisionFromBatch() => RunSmokeBatch(() =>
        {
            SmokePlayerWork(); // Real scene integration, deliberately no screenshots.
            var s = _game.Session; s.DebugAddCash(500000); s.DebugRentStarterShop(computerScenario: true);
            s.Clock.Restore(s.Clock.Today, 10 * 60);
            Teleport(s, s.MainShop.Place.Id); _game.Ui.CloseAllWindows();
            int index = s.ShelfIndexes().First(); s.SetShelfKind(index, ShelfKinds.ForCategory(PartCategory.Cpu).Id);
            var cpu = PartCatalog.Get("cpu0"); s.PlayerShop.Stock.TryAdd(cpu, true, 4);
            _game.Ui.OpenShelfWindow(index); Frame(0);
            long before = s.PlayerShop.PriceOf(cpu, true).Yuan;
            Check(!ActiveButtons().Any(b => ButtonText(b) == "跟随"), "价签没有含义不明的跟随按钮");
            ActiveButtons().First(b => b.name == "PriceMinus_cpu0_boxed").onClick.Invoke(); Frame(0);
            Check(s.PlayerShop.PriceOf(cpu, true).Yuan == before, "调整价格仅预览，不提前改变成交价");
            ActiveButtons().First(b => b.name == "ConfirmPrice_cpu0_boxed").onClick.Invoke(); Frame(0);
            Check(s.PlayerShop.PriceOf(cpu, true).Yuan == before - 1, "确定按钮保存当前预览售价");
            ActiveButtons().First(b => b.name == "PricePlus_cpu0_boxed").onClick.Invoke(); Frame(0);
            ActiveButtons().First(b => b.name == "ConfirmPrice_cpu0_boxed").onClick.Invoke(); Frame(0);
            Check(s.PlayerShop.PriceOf(cpu, true).Yuan == before, "加价后确定生效");
            Check(ActiveInputs().Length == 0, "商品调价无数字输入框");
            var pricePlus = ActiveButtons().First(b => b.name == "PricePlus_cpu0_boxed");
            var priceHold = pricePlus.GetComponent<HoldRepeat>();
            var demand = pricePlus.transform.parent.Find("PurchaseIntent_cpu0_boxed").GetComponent<Text>();
            string oldDemand = demand.text;
            priceHold.OnPointerDown(new PointerEventData(null)); priceHold.AdvanceHold(1000);
            Check(demand.text != oldDemand, "手指尚未松开时购买意愿已经实时变化");
            long capPrice = s.PlayerShop.PartPriceRange(cpu, true).max;
            string expectedDemand = $"购买意愿约 {s.PlayerShop.PartPurchaseIntentAtPrice(cpu, true, capPrice) * 100:0}%";
            Check(demand.text == expectedDemand && s.PlayerShop.PriceOf(cpu, true).Yuan == before, "实时意愿使用预览价格计算，确认前不改实际售价");
            priceHold.OnPointerUp(new PointerEventData(null)); pricePlus.onClick.Invoke(); Frame(0);
            var cap = ActiveButtons().First(b => b.name == "PricePlus_cpu0_boxed");
            Check(!cap.interactable, "达到预览价格上限禁用加号");
            ActiveButtons().First(b => b.name == "PriceMinus_cpu0_boxed").onClick.Invoke();
            ActiveButtons().First(b => b.name == "PricePlus_cpu0_boxed").GetComponent<HoldRepeat>().Commit(1);
            ActiveButtons().First(b => b.name == "ConfirmPrice_cpu0_boxed").onClick.Invoke(); Frame(0);
            Check(s.PlayerShop.PriceOf(cpu, true).Yuan == capPrice, "连续反向调价后确定正确，长按松手不重复加价");
            Check(!ActiveButtons().First(b => b.name == "ConfirmPrice_cpu0_boxed").interactable, "确认后没有未保存改动时禁用确定");

            Check(_game.Ui.Canvases.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.name == "PurchaseIntent_cpu0_boxed" && t.text.Contains("购买意愿")), "购买意愿显示在同一商品价签");
            _game.Ui.CloseAllWindows();
            var farm = s.City.Places.First(p => p.Business == GameSession.FarmBusiness);
            Teleport(s, farm.Id); _game.Ui.OnEnteredPlace(farm); Frame(0);
            Check(!ActiveInputs().Any(i => i.contentType == InputField.ContentType.IntegerNumber), "买米不再显示数量输入框");
            var plus = ActiveButtons().First(b => b.name == "QuantityPlus_rice");
            var hold = plus.GetComponent<HoldRepeat>(); var pointer = new PointerEventData(null);
            hold.OnPointerDown(pointer); hold.AdvanceHold(.4f); int first = hold.PendingCount;
            hold.AdvanceHold(1f); int next = hold.PendingCount;
            Check(first >= 2 && next - first > first, "长按数量加速递增");
            hold.OnPointerUp(pointer); plus.onClick.Invoke(); Frame(0);
            Check(System.Math.Abs(s.CartAmount("rice") - next * .25) < 1e-8, "松手提交千克数量，点击事件不会重复添加");
            AdjustQuantity("rice", 1); Check(s.CartAmount("rice") > 0, "加号可继续购买");
            _game.Ui.CloseAllWindows(); s.ClearCart();
            s.MainShop.Buyer = new Employee { Name = "测试采购员工", DailyWageYuan = 150 };
            var buyer = s.Employees.First(e => e.Role == EmployeeRole.Buyer);
            _game.Ui.OpenEmployeePurchase(buyer.Person.Id); Frame(0);
            Check(!ActiveInputs().Any(i => i.contentType == InputField.ContentType.IntegerNumber), "员工采购移除数量输入框");
            Check(ActiveButtons().Any(b => b.name == "PurchaseInterval_1") && ActiveButtons().Any(b => b.name == "PurchaseInterval_3"), "采购页可选每天或每三天一次");
            ActiveButtons().First(b => b.name == "PurchaseInterval_3").onClick.Invoke(); Frame(0);
            AdjustQuantity("cpu0", 3);
            Check(!ActiveButtons().Any(b => ButtonText(b).Contains("立即采购")), "每日清单只有完成保存，没有立即采购按钮");
            var purchaseCash = s.Ledger.Balance;
            ClickGuideButton("完成");
            s.AdvanceClock(1);
            Check(s.EmployeePurchaseTrips.Count == 0 && s.Ledger.Balance == purchaseCash, "保存当天不出发、不扣采购款");
            Check(buyer.Person.PurchasePlan.EveryDays == 3, "完成后保存每三天的持续采购频率");
            Check(buyer.Person.PurchasePlan.FirstDay == s.Clock.Today.TotalDays + 1, "每日采购明天才开始");
            Check(buyer.Person.PurchasePlan.Items.Any(i => i.PartId == "cpu0" && i.Count == 3), "员工采购加减数量在完成后保存");
            _game.Ui.CloseAllWindows(); s.SetSmartphone(true); _game.Ui.OpenWholesaleApp(true); Frame(0);
            Check(ActiveInputs().Length == 0 && ActiveButtons().Any(b => b.name == "QuantityPlus_dailyBudget"), "自动补货政策也没有数字输入框");
            _game.Ui.CloseAllWindows(); Teleport(s, s.Home.Id); _game.Ui.OpenBedWindow(); Frame(0);
            ClickGuideButton("+30分钟"); Check(ActiveButtons().Any(b => ButtonText(b) == "睡到选定时间"), "床提供起床时间加减选择与确认");
            _game.Ui.CloseAllWindows();
        });
    }
}

