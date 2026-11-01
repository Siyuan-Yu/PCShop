using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private readonly Dictionary<string, EmployeePurchasePlan> _purchaseDrafts = new Dictionary<string, EmployeePurchasePlan>();

        public void OpenEmployeesPanel()
        {
            OpenWindow("员工管理", 1120, 900, w =>
            {
                var rows = S.Employees;
                Line(w.Content, $"员工 {rows.Count} 人 · 每日工资合计 ¥{rows.Sum(x => x.Person.DailyWageYuan):N0}", 21, Ui.Accent);
                Para(w.Content, "每个人都能做收银、装机或进货。先聘用，再安排岗位和门店；同店换岗今天生效，跨店明天生效。", 17, Ui.Dim);
                var bar = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Button(bar, "查看车辆与占用", OpenFleetPanel, 17), 240);
                Ui.Size(Ui.Button(bar, "在当前店招人", () => { w.Close(); OpenDock(); DebugSelectTab(6); }, 17, Ui.ButtonBg, S.InPlayerShop), 220);
                if (rows.Count == 0) Para(w.Content, "还没有员工。到自己的店里打开“招人”，聘用后选择工作安排。", 20);
                foreach (var shop in S.MyShops.Where(x => x.Assembler != null || x.EmployeeTrained))
                {
                    var target = shop;
                    Ui.Size(Ui.Button(w.Content, S.ShopTitle(target) + (target.AutoBuild ? " · 自动装机：开" : " · 自动装机：关"), () => { S.ToggleAutoBuildAt(target); w.Rebuild(); }, 16), -1, 34);
                }
                var list = Ui.Scroll(w.Content, 540);
                foreach (var assignment in rows)
                {
                    var row = assignment;
                    var e = row.Person;
                    Line(list, $"<b>{e.Name}</b> · 日薪 {e.DailyWage}", 19, Ui.Accent);
                    Line(list, GameSession.EmployeeAbilities(e) + "（越少越熟练）", 15, Ui.Dim);
                    string current = row.Shop == null ? "待安排工作" : $"{S.ShopTitle(row.Shop)}（{row.Shop.Place.Name}） · {GameSession.RoleName(row.Role)}";
                    Line(list, $"今天：{current} · {S.EmployeeStatus(row)} · 班次 {Ui.Hm(e.ShiftStart)}–{Ui.Hm(e.ShiftEnd)}", 17);
                    var destination = S.ShopAt(e.TransferShopId);
                    Line(list, destination == null ? "明天：保持当前安排" : $"明天：{S.ShopTitle(destination)}（{destination.Place.Name}） · {GameSession.RoleName(S.PlannedEmployeeRole(row))}", 17, destination == null ? Ui.Dim : Ui.Good);
                    var actions = Ui.Row(list, 40);
                    Ui.Size(Ui.Button(actions, "安排工作", () => OpenEmployeeWork(e.Id, w), 17, Ui.ButtonHot), 150);
                    if (destination != null) Ui.Size(Ui.Button(actions, "取消明日安排", () => { S.CancelEmployeeTransfer(e.Id); w.Rebuild(); }, 16), 150);
                    Ui.Size(Ui.Button(actions, "调整班次", () => OpenEmployeeShift(e.Id, w), 16), 140);
                    if (row.Shop != null && row.Role == EmployeeRole.Cashier && !row.Shop.EmployeeTrained)
                        Ui.Size(Ui.Button(actions, "装机培训…", () => ConfirmAction("装机培训", "培训费 ¥1,200，日薪增加 ¥60，可在收银空闲时装机。", "确认培训", () => { S.TrainEmployeeAt(row.Shop); w.Rebuild(); }), 15), 130);
                    if (row.Role == EmployeeRole.Buyer)
                        Ui.Size(Ui.Button(actions, "安排进货", () => OpenEmployeePurchase(e.Id, w), 17, Ui.ButtonHot), 150);
                    Ui.Size(Ui.Button(actions, "辞退…", () => ConfirmAction("辞退员工", $"辞退{e.Name}，同时取消未生效的安排。正在进货时需要先等人和货回店。", "确认辞退", () => { S.DismissEmployee(e.Id); w.Rebuild(); }), 16, Ui.Warn), 110);
                    if (row.Role == EmployeeRole.Buyer)
                        Line(list, e.PurchasePlan.Daily ? "采购频率：" + (e.PurchasePlan.EveryDays == 1 ? "每天一次" : "每三天一次")
                            + " · 下次最早第" + (System.Math.Max(S.Clock.Today.TotalDays, e.PurchasePlan.NextPurchaseDay) + 1) + "天上班时尝试" : "定期采购：未启用或已暂停", 15, Ui.Dim);
                    if (row.Role == EmployeeRole.Buyer) Para(list, S.EmployeePurchaseStorageText(e.Id), 15, Ui.Accent);
                    if (row.Role == EmployeeRole.Buyer && !string.IsNullOrEmpty(e.LastPurchaseNote)) Para(list, e.LastPurchaseNote, 15, Ui.Dim);
                }
            });
        }

        public void OpenEmployeeWork(string employeeId, Window parent = null)
        {
            var row = S.FindEmployee(employeeId);
            if (row == null) return;
            EmployeeRole role = row.Role == EmployeeRole.Unassigned ? EmployeeRole.Cashier : row.Role;
            string destination = (row.Shop ?? S.CurrentShop)?.Place.Id;
            OpenWindow("安排工作 · " + row.Person.Name, 940, 660, w =>
            {
                Line(w.Content, GameSession.EmployeeAbilities(row.Person), 17, Ui.Accent);
                Para(w.Content, "不擅长也可以安排，效率按员工能力计算。换岗位不自动培训，也不改变工资。", 17, Ui.Dim);
                Line(w.Content, "1. 做什么工作", 20);
                var roles = Ui.Row(w.Content, 44);
                foreach (EmployeeRole option in Enum.GetValues(typeof(EmployeeRole)))
                {
                    var choice = option;
                    Ui.Size(Ui.Button(roles, GameSession.RoleName(choice), () => { role = choice; w.Rebuild(); }, 18, role == choice ? Ui.ButtonHot : Ui.ButtonBg), -1, -1, 1);
                }
                if (role != EmployeeRole.Unassigned)
                {
                    Line(w.Content, "2. 在哪家店工作", 20);
                    var shops = Ui.Scroll(w.Content, 210);
                    foreach (var shop in S.MyShops)
                    {
                        string target = shop.Place.Id;
                        Ui.Size(Ui.Button(shops, $"{S.ShopTitle(shop)}（{shop.Place.Name}）", () => { destination = target; w.Rebuild(); }, 18, destination == target ? Ui.ButtonHot : Ui.ButtonBg), -1, 44);
                    }
                }
                bool tomorrow = role != EmployeeRole.Unassigned && row.Shop != null && row.Shop.Place.Id != destination;
                Para(w.Content, role == EmployeeRole.Unassigned ? "暂不分配工作，仍按原日薪发工资；以后可重新安排。"
                    : tomorrow ? $"跨店安排明天生效。今天仍在{S.ShopTitle(row.Shop)}做{GameSession.RoleName(row.Role)}，明天到新店做{GameSession.RoleName(role)}。"
                    : "本次是首次安排或同店换岗，确认后今天起按新岗位工作。", 18, Ui.Accent);
                string blocker = S.EmployeeAssignmentBlocker(employeeId, destination, role);
                if (blocker != null) Para(w.Content, blocker, 16, Ui.Warn);
                var occupant = S.EmployeeInPosition(destination, role);
                if (occupant != null && occupant.Person.Id != employeeId && S.EmployeeAssignmentBlocker(employeeId, destination, role, true) == null)
                    Ui.Size(Ui.Button(w.Content, "替换安排…", () => ConfirmAction("替换工作安排",
                        occupant.Person.Name + "立即设为待安排，保留工资；" + row.Person.Name + (tomorrow ? "明天到岗。" : "今天到岗。"), "确认替换", () =>
                        {
                            if (S.ReplaceEmployeeWork(employeeId, destination, role)) { w.Close(); parent?.Rebuild(); RebuildDock(); }
                        }), 18, Ui.Warn), -1, 42);
                string label = role == EmployeeRole.Unassigned ? "确认设为待安排" : tomorrow ? "确认明天到新店做" + GameSession.RoleName(role) : "确认今天起做" + GameSession.RoleName(role);
                Ui.Size(Ui.Button(w.Content, label, () =>
                {
                    if (S.ArrangeEmployeeWork(employeeId, destination, role)) { w.Close(); parent?.Rebuild(); RebuildDock(); }
                }, 19, Ui.ButtonHot, blocker == null), -1, 48);
            });
        }

        public void OpenFleetPanel()
        {
            OpenWindow("车辆与占用", 860, 650, w =>
            {
                Para(w.Content, "每台车同时只能给一个人用。员工外出进货占用车辆，返店后释放；老板选中的出行车辆也占一台。", 18);
                foreach (var v in Vehicles.All.Where(x => x.Purchasable))
                    Para(w.Content, $"<b>{v.Name}</b> · 载货 {v.CargoCapacity * 2:0} 千克\n{S.VehicleUsage(v.Kind)}", 18);
                Ui.Size(Ui.Button(w.Content, "去车行买车", () =>
                {
                    CloseModalWindows();
                    _ctrl.GoToPlace(S.City.FirstOfKind(PlaceKind.Dealer));
                }, 19, Ui.ButtonHot), -1, 44);
            });
        }

        public void OpenEmployeePurchase(string employeeId, Window parent = null, int? importedOrderId = null)
        {
            var worker = S.FindEmployee(employeeId);
            if (worker == null || worker.Role != EmployeeRole.Buyer) return;
            var draft = _purchaseDrafts.TryGetValue(employeeId, out var savedDraft) ? savedDraft.Copy() : worker.Person.PurchasePlan.Copy();
            S.PrepareEmployeePurchaseDraft(draft);
            _purchaseDrafts[employeeId] = draft;
            string browseSource = null, category = worker.Shop.RetailKind == "flowers" ? "鲜花与盆栽" : worker.Shop.RetailKind == "produce" ? "米与生鲜" : worker.Shop.RetailKind == "food" ? "外带食品" : "全部", search = "";
            bool selectedOnly = importedOrderId.HasValue;
            var selected = draft.Items.ToDictionary(GameSession.PurchaseKey, x => x.Copy());
            var order = draft.Items.Select(GameSession.PurchaseKey).ToList();
            void Sync()
            {
                order.RemoveAll(key => !selected.ContainsKey(key));
                foreach (string key in selected.Keys) if (!order.Contains(key)) order.Add(key);
                draft.Items = order.Where(key => selected[key].Count > 0).Select(key => selected[key].Copy()).ToArray();
            }
            OpenWindow("定期采购清单 · " + worker.Person.Name + " · " + S.ShopTitle(worker.Shop), 1160, 940, w =>
            {
                var panel = w.Root.Find("Panel");
                var previousActions = panel.Find("PurchaseActions");
                if (previousActions != null) Kill.It(previousActions.gameObject);
                ((RectTransform)w.Content.parent.parent).Anchor(0, 0, 1, 1, 0, 76, 0, 50);
                Para(w.Content, "选好商品和采购频率后点“完成”，从明天起自动采购。清单从上到下是购买顺序。", 18, Ui.Accent);
                Para(w.Content, S.EmployeePurchaseStorageText(employeeId), 16, Ui.Accent);
                Para(w.Content, "货源包括电子市场、花店、农贸市场、快餐店和烟酒茶行。鲜花、生鲜与外带食品均可采购；送店后为对应货架设置品类即可销售。", 15, Ui.Dim);
                Para(w.Content, "每次按清单数量重复买入；销售较慢时可减少数量或改为每三天一次。在途预留指已买、尚未送到的货。", 15, Ui.Dim);
                if (importedOrderId.HasValue)
                    Para(w.Content, "订单 #" + importedOrderId.Value + " 的缺件已合并到这份草稿。已有型号保留原数量和货源，可在这里调整；点“完成”后保存。", 16, Ui.Accent);
                var quickKinds = Ui.Row(w.Content, 34);
                foreach (var quick in new[] { "全部", "鲜花与盆栽", "米与生鲜", "外带食品", "饮料" })
                {
                    string pick = quick;
                    Ui.Size(Ui.Button(quickKinds, pick, () => { category = pick; browseSource = null; selectedOnly = false; w.Rebuild(); }, 16,
                        category == pick ? Ui.ButtonHot : Ui.ButtonBg), -1, -1, 1);
                }
                var frequency = Ui.Row(w.Content, 38);
                Ui.Size(Ui.Label(frequency, "采购频率", 17), 100);
                foreach (int days in new[] { 1, 3 })
                {
                    int interval = days;
                    var button = Ui.Button(frequency, days == 1 ? "每天一次" : "每三天一次", () => { draft.IntervalDays = interval; w.Rebuild(); }, 17,
                        draft.EveryDays == days ? Ui.ButtonHot : Ui.ButtonBg);
                    button.name = "PurchaseInterval_" + days;
                    Ui.Size(button, 180, 36);
                }
                Para(w.Content, "从明天开始，每隔" + draft.EveryDays + "天在上班时采购一次。", 15, Ui.Dim);
                var filters = Ui.Row(w.Content, 38);
                string sourceName = browseSource == null ? "全部货源" : S.City.GetPlace(browseSource).Name;
                Ui.Size(Ui.Button(filters, sourceName + "  ▼", () => OpenWindow("浏览货源", 740, 660, picker =>
                {
                    Ui.Size(Ui.Button(picker.Content, "全部货源", () => { browseSource = null; picker.Close(); w.Rebuild(); }, 18), -1, 42);
                    foreach (var place in S.EmployeePurchaseCatalog().Select(x => x.Source).GroupBy(x => x.Id).Select(g => g.First()))
                    {
                        var source = place;
                        Ui.Size(Ui.Button(picker.Content, source.Name, () => { browseSource = source.Id; picker.Close(); w.Rebuild(); }, 18), -1, 42);
                    }
                }), 16), 255);
                Ui.Size(Ui.Button(filters, category + " ▾", () => OpenWindow("商品分类", 620, 620, picker =>
                {
                    foreach (string name in new[] { "全部" }.Concat(S.EmployeePurchaseCatalog().Select(o => o.Category).Distinct()))
                    {
                        string choice = name;
                        Ui.Size(Ui.Button(picker.Content, choice, () => { category = choice; picker.Close(); w.Rebuild(); }, 17), -1, 38);
                    }
                }), 16), 175);
                Ui.Size(Ui.TextInput(filters, search, "搜索商品", v => { search = v; w.RequestRebuild(); }), -1, 36, 1);
                Ui.Size(Ui.Button(filters, selectedOnly ? "只看已选 ✓" : "只看已选", () => { selectedOnly = !selectedOnly; w.Rebuild(); }, 15), 130);
                Line(w.Content, "商品目录（可混选多个档口和商店，换货源只筛选列表，不会清掉已选商品）", 15, Ui.Dim);
                var catalog = S.EmployeePurchaseCatalog();
                var goods = Ui.Scroll(w.Content, 218);
                foreach (var item in catalog.Where(o => (browseSource == null || o.Source.Id == browseSource)
                    && (category == "全部" || o.Category == category)
                    && (string.IsNullOrWhiteSpace(search) || (o.Name + o.Source.Name).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    var offer = item;
                    int count = selected.TryGetValue(offer.Key, out var lineItem) ? lineItem.Count : 0;
                    if (selectedOnly && count == 0) continue;
                    void Set(int value) { if (value <= 0) selected.Remove(offer.Key); else selected[offer.Key] = offer.Line(Math.Min(100, value)); Sync(); w.RequestRebuild(); }
                    var line = Ui.Row(goods, 68);
                    string details = offer.Source.Name;
                    if (offer.Kind == PurchaseGoodsKind.Part)
                        details += $" · 库存 {worker.Shop.Sim.Stock.CountAll(PartCatalog.Get(offer.Id))} · 在途 {S.PendingPurchaseCount(worker.Shop, offer.Id)}";
                    else if (offer.Kind == PurchaseGoodsKind.Gift)
                        details += $" · 库存 {worker.Shop.Sim.Stock.GoodsCount(offer.Id)}";
                    Ui.Size(Ui.Label(line, ProductLabel(offer.Name, offer.UnitKg, details), 16), -1, -1, 1);
                    Ui.Size(Ui.Label(line, offer.Price + (offer.Kind == PurchaseGoodsKind.Rice ? "/袋" : offer.Kind == PurchaseGoodsKind.Ingredient ? "/份" : "/件"), 16), 130);
                    QuantityStepper(w, line, offer.Key, count, 0, 100, 1, value => Set((int)value));
                }
                Sync();
                var summary = Ui.Row(w.Content, 32);
                Ui.Size(Ui.Label(summary, $"已选采购清单：{draft.Items.Length} 种商品 · {draft.Items.Select(x => x.SourceId).Distinct().Count()} 个货源", 18, TextAnchor.MiddleLeft, Ui.Accent), -1, -1, 1);
                Ui.Size(Ui.Button(summary, "清空清单", () => { selected.Clear(); Sync(); w.Rebuild(); }, 15), 130);
                var chosenList = Ui.Scroll(w.Content, 144);
                if (draft.Items.Length == 0) Para(chosenList, "还没选商品。在上面的商品目录加数量后，会集中显示在这里。", 17, Ui.Dim);
                foreach (var item in draft.Items)
                {
                    var choice = item;
                    var offer = S.EmployeePurchaseOffer(choice);
                    var row = Ui.Row(chosenList, 58);
                    string source = offer?.Source.Name ?? choice.SourceId ?? "自动选货源";
                    Ui.Size(Ui.Label(row, offer == null ? GameSession.PurchaseItemName(choice) + " ×" + choice.Count : ProductLabel(GameSession.PurchaseItemName(choice) + " ×" + choice.Count, offer.UnitKg, source), 16), -1, -1, 1);
                    Ui.Size(Ui.Label(row, offer == null ? "货源不可用" : (offer.Price * choice.Count).ToString(), 16), 130);
                    string key = GameSession.PurchaseKey(choice);
                    int priority = order.IndexOf(key);
                    void Move(int offset) { int index = order.IndexOf(key); var other = order[index + offset]; order[index + offset] = key; order[index] = other; Sync(); w.Rebuild(); }
                    Ui.Size(Ui.Button(row, "↑", () => Move(-1), 18, Ui.ButtonBg, priority > 0), 36);
                    Ui.Size(Ui.Button(row, "↓", () => Move(1), 18, Ui.ButtonBg, priority < order.Count - 1), 36);
                    Ui.Size(Ui.Button(row, "移出清单", () => { selected.Remove(GameSession.PurchaseKey(choice)); Sync(); w.Rebuild(); }, 15), 110);
                }
                Line(w.Content, "怎么运回来", 18, Ui.Accent);
                var transport = Ui.Row(w.Content, 44);
                foreach (var kind in new[] { VehicleKind.Walk, VehicleKind.Taxi, VehicleKind.EBike, VehicleKind.Tricycle, VehicleKind.Van })
                {
                    VehicleKind mode = kind;
                    bool hired = mode == VehicleKind.Walk || mode == VehicleKind.Taxi;
                    string label = mode == VehicleKind.Walk ? "步行 + 货车" : mode == VehicleKind.Taxi ? "打车 + 货车" : Vehicles.Get(mode).Name + $"（空闲 {S.FreeVehicleCount(mode)}）";
                    Ui.Size(Ui.Button(transport, label, () => { draft.Transport = mode; w.Rebuild(); }, 16, draft.Transport == mode ? Ui.ButtonHot : Ui.ButtonBg, hired || S.VehicleCount(mode) > 0), -1, -1, 1);
                }
                var quote = S.EstimateEmployeePurchase(employeeId, draft);
                string budgetName = draft.EveryDays == 1 ? "每日预算" : "每次采购预算";
                var budgetLabel = Para(w.Content, $"{budgetName}：货款 {quote.Goods} + 车费 {quote.Travel} + 运费 {quote.Freight} = 总计 {quote.Total}", 17);
                budgetLabel.name = "EmployeePurchaseBudget";
                bool ownVehicle = draft.Transport == VehicleKind.EBike || draft.Transport == VehicleKind.Tricycle || draft.Transport == VehicleKind.Van;
                if (ownVehicle && S.Vehicle == draft.Transport && S.FreeVehicleCount(draft.Transport) == 0)
                    Ui.Size(Ui.Button(w.Content, "老板改为步行，释放这台车", () => { S.SetVehicle(VehicleKind.Walk); w.Rebuild(); }, 16), -1, 36);
                var actions = Ui.Row(panel, 50);
                actions.name = "PurchaseActions";
                actions.Anchor(0, 0, 1, 0, 16, 12, 16, -62);
                Ui.Size(Ui.Button(actions, "完成", () =>
                {
                    Sync(); draft.Daily = draft.Items.Length > 0;
                    if (S.CompleteEmployeePurchase(employeeId, draft))
                    {
                        _purchaseDrafts.Remove(employeeId);
                        Toast(draft.Daily ? "采购安排已保存，明天起每隔" + draft.EveryDays + "天按顺序尝试采购，今天不扣款。" : "已清空采购安排，不再自动进货。");
                        CloseModalWindows();
                    }
                }, 20, Ui.ButtonHot), -1, -1, 1);
                if (worker.Person.PurchasePlan.Daily)
                    Ui.Size(Ui.Button(actions, "暂停定期采购", () =>
                    {
                        var plan = worker.Person.PurchasePlan.Copy(); plan.Daily = false;
                        if (S.ConfigureEmployeePurchase(employeeId, plan)) { _purchaseDrafts.Remove(employeeId); CloseModalWindows(); }
                    }, 16), 170);
            });
        }
    }
}
