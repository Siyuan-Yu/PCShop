using System;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        public void OpenEmployeesPanel()
        {
            OpenWindow("员工管理", 1120, 860, w =>
            {
                var rows = S.Employees;
                Line(w.Content, $"员工 {rows.Count} 人 · 每日工资合计 ¥{rows.Sum(x => x.Person.DailyWageYuan):N0}", 21, Ui.Accent);
                Para(w.Content, "每家店可安排收银、装机、进货各一人。今天安排调店，明天才到新店；今天仍在原店工作。", 17, Ui.Dim);
                var bar = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Button(bar, "查看车辆与占用", OpenFleetPanel, 17), 240);
                Ui.Size(Ui.Button(bar, "在当前店招人", () => { w.Close(); OpenDock(); DebugSelectTab(6); }, 17, Ui.ButtonBg, S.InPlayerShop), 220);
                if (rows.Count == 0) Para(w.Content, "还没有员工。到自己的店里打开“招人”，贴招聘启事，次日 9:00 面试时选择岗位。", 20);
                var list = Ui.Scroll(w.Content, 600);
                foreach (var assignment in rows)
                {
                    var row = assignment;
                    var e = row.Person;
                    string trained = row.Role == EmployeeRole.Cashier && row.Shop.EmployeeTrained ? "（已培训，可兼装机）" : "";
                    Line(list, $"<b>{e.Name}</b> · {GameSession.RoleName(row.Role)}{trained} · 日薪 {e.DailyWage}", 19, Ui.Accent);
                    Line(list, $"今天：{S.ShopTitle(row.Shop)}（{row.Shop.Place.Name}） · {S.EmployeeStatus(row)} · 班次 {Ui.Hm(e.ShiftStart)}–{Ui.Hm(e.ShiftEnd)}", 17);
                    var destination = S.ShopAt(e.TransferShopId);
                    Line(list, destination == null ? "明天：仍在本店" : $"明天：调到 {S.ShopTitle(destination)}（{destination.Place.Name}）", 17, destination == null ? Ui.Dim : Ui.Good);
                    var actions = Ui.Row(list, 40);
                    Ui.Size(Ui.Button(actions, "安排明天调店", () => OpenEmployeeTransfer(row, w), 16), 170);
                    if (destination != null) Ui.Size(Ui.Button(actions, "取消调店", () => { S.CancelEmployeeTransfer(e.Id); w.Rebuild(); }, 16), 130);
                    Ui.Size(Ui.Button(actions, "调整班次", () => { S.CycleEmployeeShift(e.Id); w.Rebuild(); }, 16), 140);
                    if (row.Role == EmployeeRole.Buyer)
                        Ui.Size(Ui.Button(actions, "安排进货", () => OpenEmployeePurchase(e.Id, w), 17, Ui.ButtonHot), 150);
                    Ui.Size(Ui.Button(actions, "辞退…", () => ConfirmAction("辞退员工", $"辞退{e.Name}，同时取消未生效的调店安排。正在进货时需要先等人和货回店。", "确认辞退", () => { S.DismissEmployee(e.Id); w.Rebuild(); }), 16, Ui.Warn), 110);
                    if (row.Role == EmployeeRole.Buyer && !string.IsNullOrEmpty(e.LastPurchaseNote)) Para(list, e.LastPurchaseNote, 15, Ui.Dim);
                }
            });
        }

        private void OpenEmployeeTransfer(EmployeeAssignment row, Window parent)
        {
            OpenWindow("安排明天调店 · " + row.Person.Name, 740, 600, w =>
            {
                Para(w.Content, $"岗位：{GameSession.RoleName(row.Role)}。今天仍在{S.ShopTitle(row.Shop)}，明天上班去你选的新店，工资、班次和培训保留。", 18);
                foreach (var shop in S.MyShops)
                {
                    var dest = shop;
                    string blocker = S.EmployeeTransferBlocker(row.Person.Id, dest.Place.Id);
                    string label = $"{S.ShopTitle(dest)}（{dest.Place.Name}）\n" + (blocker ?? "安排明天到这里上班");
                    Ui.Size(Ui.Button(w.Content, label, () =>
                    {
                        if (S.ScheduleEmployeeTransfer(row.Person.Id, dest.Place.Id)) { w.Close(); parent.Rebuild(); }
                    }, 17, Ui.ButtonHot, blocker == null), -1, 64);
                }
            });
        }

        public void OpenFleetPanel()
        {
            OpenWindow("车辆与占用", 860, 650, w =>
            {
                Para(w.Content, "每台车同时只能给一个人用。员工外出进货占用车辆，返店后释放；老板选中的出行车辆也占一台。", 18);
                foreach (var v in Vehicles.All.Where(x => x.Purchasable))
                    Para(w.Content, $"<b>{v.Name}</b> · 载货 {v.CargoCapacity * 2:0} 公斤\n{S.VehicleUsage(v.Kind)}", 18);
                Ui.Size(Ui.Button(w.Content, "去车行买车", () =>
                {
                    CloseModalWindows();
                    _ctrl.GoToPlace(S.City.FirstOfKind(PlaceKind.Dealer));
                }, 19, Ui.ButtonHot), -1, 44);
            });
        }

        public void OpenEmployeePurchase(string employeeId, Window parent = null)
        {
            var worker = S.FindEmployee(employeeId);
            if (worker == null || worker.Role != EmployeeRole.Buyer) return;
            var draft = worker.Person.PurchasePlan.Copy();
            var counts = draft.Items.ToDictionary(x => x.PartId, x => x.Count);
            Action sync = () => draft.Items = counts.Where(x => x.Value > 0).Select(x => new PurchaseLine { PartId = x.Key, Count = x.Value }).ToArray();
            OpenWindow("安排进货 · " + worker.Person.Name + " · " + S.ShopTitle(worker.Shop), 1100, 900, w =>
            {
                var trip = S.EmployeeTrip(employeeId);
                if (trip != null)
                    Para(w.Content, $"{S.EmployeeStatus(worker)}，约 {Math.Max(0, trip.ReturnAt - S.Clock.Now.TotalMinutes)} 分钟返店。本趟货送到 {S.ShopTitle(worker.Shop)}。", 18, Ui.Accent);
                Line(w.Content, "1. 去哪里买", 19, Ui.Accent);
                var source = Ui.Row(w.Content, 38);
                string sourceName = string.IsNullOrEmpty(draft.SourceId) ? "电子市场：按品类找档口" : S.City.GetPlace(draft.SourceId).Name;
                Ui.Size(Ui.Button(source, sourceName + "  ▼", () => OpenWindow("选择进货档口", 660, 480, picker =>
                {
                    Ui.Size(Ui.Button(picker.Content, "电子市场：按品类找档口", () => { draft.SourceId = null; picker.Close(); w.Rebuild(); }, 18), -1, 42);
                    foreach (var place in S.City.OfKind(PlaceKind.Wholesaler))
                    {
                        var p = place;
                        Ui.Size(Ui.Button(picker.Content, p.Name, () => { draft.SourceId = p.Id; picker.Close(); w.Rebuild(); }, 18), -1, 42);
                    }
                }), 17), -1, 38, 1);
                Line(w.Content, "2. 买什么（本趟件数；只买清单，不擅自换型号）", 19, Ui.Accent);
                var goods = Ui.Scroll(w.Content, 205);
                foreach (var def in PartCatalog.All)
                {
                    var part = def;
                    int count = counts.TryGetValue(part.Id, out int n) ? n : 0;
                    var line = Ui.Row(goods, 38);
                    Ui.Size(Ui.Label(line, part.Name, 16), -1, -1, 1);
                    var stall = S.City.OfKind(PlaceKind.Wholesaler).FirstOrDefault(p => (string.IsNullOrEmpty(draft.SourceId) || p.Id == draft.SourceId) && S.StallSells(p, part));
                    Ui.Size(Ui.Label(line, stall == null ? "该档口不卖" : S.StallPrice(stall, part) + "/件", 15), 150);
                    Ui.Size(Ui.Button(line, "−", () => { counts[part.Id] = Math.Max(0, count - 1); sync(); w.Rebuild(); }, 18), 42);
                    Ui.Size(Ui.Label(line, count.ToString(), 18, TextAnchor.MiddleCenter), 45);
                    Ui.Size(Ui.Button(line, "+", () => { counts[part.Id] = Math.Min(100, count + 1); sync(); w.Rebuild(); }, 18, Ui.ButtonBg, stall != null), 42);
                    Ui.Size(Ui.Button(line, "+5", () => { counts[part.Id] = Math.Min(100, count + 5); sync(); w.Rebuild(); }, 16, Ui.ButtonBg, stall != null), 52);
                }
                var clear = Ui.Row(w.Content, 32);
                Ui.Size(Ui.Button(clear, "清空清单", () => { counts.Clear(); sync(); w.Rebuild(); }, 15), 150);
                Line(w.Content, "3. 怎么运回来", 19, Ui.Accent);
                var transport = Ui.Row(w.Content, 50);
                foreach (var kind in new[] { VehicleKind.Walk, VehicleKind.Taxi, VehicleKind.EBike, VehicleKind.Tricycle, VehicleKind.Van })
                {
                    VehicleKind mode = kind;
                    bool hired = mode == VehicleKind.Walk || mode == VehicleKind.Taxi;
                    string title = mode == VehicleKind.Walk ? "步行 + 货车" : mode == VehicleKind.Taxi ? "打车 + 货车" : Vehicles.Get(mode).Name + $"（空闲 {S.FreeVehicleCount(mode)}）";
                    Ui.Size(Ui.Button(transport, title, () => { draft.Transport = mode; w.Rebuild(); }, 16, draft.Transport == mode ? Ui.ButtonHot : Ui.ButtonBg, hired || S.VehicleCount(mode) > 0), -1, 50, 1);
                }
                string hint = draft.Transport == VehicleKind.Tricycle ? "三轮车载货 80 公斤，有违规罚款/扣车风险。" : draft.Transport == VehicleKind.EBike ? "小电驴载货 20 公斤，一台车同一时间只能一人使用。" : GameSession.PurchaseTransportName(draft.Transport);
                Line(w.Content, hint, 15, Ui.Dim);
                var budget = Ui.Row(w.Content, 36);
                Ui.Size(Ui.Label(budget, $"本趟预算上限 ¥{draft.BudgetYuan:N0}（含车费）", 17), -1, -1, 1);
                foreach (int delta in new[] { -1000, -100, 100, 1000 })
                {
                    int d = delta;
                    Ui.Size(Ui.Button(budget, d > 0 ? "+" + d : d.ToString(), () => { draft.BudgetYuan = Math.Max(1, Math.Min(10000000, draft.BudgetYuan + d)); w.Rebuild(); }, 15), 70);
                }
                Ui.Size(Ui.Button(w.Content, draft.Daily ? "每天进货：开（工作日上班后尝试一次）" : "每天进货：关（只派出这一趟）", () => { draft.Daily = !draft.Daily; w.Rebuild(); }, 16), -1, 34);
                sync();
                var quote = S.QuoteEmployeePurchase(employeeId, draft);
                Para(w.Content, $"货款 {quote.Goods} + 往返车费 {quote.Travel} + 货车运费 {quote.Freight} = 合计 {quote.Total}\n本趟 {draft.Items.Sum(x => x.Count)} 件 · {quote.Kg:0.#} 公斤 · 货约 {quote.DeliveryMinutes} 分钟到店；出发时统一预付。", 16);
                if (quote.Error != null) Para(w.Content, quote.Error, 16, Ui.Warn);
                if (!string.IsNullOrEmpty(worker.Person.LastPurchaseNote)) Para(w.Content, worker.Person.LastPurchaseNote, 15, Ui.Dim);
                var actions = Ui.Row(w.Content, 44);
                Ui.Size(Ui.Button(actions, "保存安排", () =>
                {
                    sync();
                    if (S.ConfigureEmployeePurchase(employeeId, draft)) { Toast("采购安排已保存。每天进货开启时，上班后每天尝试一次；也可手动派出。"); parent?.Rebuild(); w.Rebuild(); }
                }, 18), -1, -1, 1);
                Ui.Size(Ui.Button(actions, "按清单派出（付 " + quote.Total + "）", () =>
                {
                    sync();
                    if (S.ConfigureEmployeePurchase(employeeId, draft) && S.DispatchEmployeePurchase(employeeId)) { CloseModalWindows(); }
                    else w.Rebuild();
                }, 18, Ui.ButtonHot, quote.Error == null), -1, -1, 1);
            });
        }
    }
}
