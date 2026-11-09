using System;
using System.Linq;
using System.Collections.Generic;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private readonly Dictionary<string, EmployeePurchasePlan> _purchaseDrafts = new Dictionary<string, EmployeePurchasePlan>();

        public void OpenEmployeesPanel()
        {
            var firstShop = S.ShopAt(S.CurrentPlace) ?? S.MyShops.FirstOrDefault();
            var expandedShops = new HashSet<string> { firstShop?.Place.Id };
            var expandedEmployees = new HashSet<string>();
            OpenWindow("员工管理", 1120, 900, w =>
            {
                var rows = S.Employees;
                Line(w.Content, $"员工 {rows.Count} 人 · 基础日薪合计 ¥{rows.Sum(x => x.Person.DailyWageYuan):N0} · 排班预计 {PCShopSell.Core.Common.Money.FromCents(rows.Sum(x=>GameSession.EstimateShiftPay(x.Person,x.Person.Schedule?.PendingAt>=0?x.Person.Schedule.PendingStart:x.Person.ShiftStart,x.Person.Schedule?.PendingAt>=0?x.Person.Schedule.PendingEnd:x.Person.ShiftEnd).Cents))}", 21, Ui.Accent);
                Line(w.Content, "同店换岗当天生效，跨店调动次日生效。", 16, Ui.Dim);
                var bar = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Button(bar, "查看车辆与占用", OpenFleetPanel, 17), 240);
                Ui.Size(Ui.Button(bar, "在当前店招人", () => { w.Close(); if(S.IsOwnedVenue(S.CurrentPlace))OpenVenueManagement(S.CurrentPlace);else{OpenDock();SelectTab(ShopTab.Staff);} }, 17, Ui.ButtonBg, S.InPlayerShop), 220);
                if (rows.Count == 0) Para(w.Content, "还没有员工。到自己的店里打开“员工”页，聘用后选择工作安排。", 20);
                var roster = rows.ToLookup(row => row.Shop);
                foreach (var shop in S.MyShops) BuildGroup(shop);
                if (roster[null].Any()) BuildGroup(null);

                void BuildGroup(OwnedShop shop)
                {
                    string id = shop?.Place.Id;
                    bool expanded = expandedShops.Contains(id);
                    var employees = roster[shop];
                    string label = shop == null ? $"待安排人员 · {employees.Count()} 人"
                        : $"{S.ShopTitle(shop)} · {S.City.StreetName(shop.Place.StreetId)} · {employees.Count()} 人 · 名额 {S.StaffCommitted(shop)}/{GameSession.StaffLimit(shop.Place.Size)}";
                    var header = Ui.Size(Ui.Button(w.Content, (expanded ? "▾  " : "▸  ") + label, () =>
                    {
                        if (!expandedShops.Add(id)) expandedShops.Remove(id);
                        w.Rebuild();
                    }, 19), -1, 42);
                    var heading = header.GetComponentInChildren<Text>();
                    heading.alignment = TextAnchor.MiddleLeft;
                    heading.fontStyle = FontStyle.Bold;
                    if (!expanded) return;

                    var body = Ui.Rect(w.Content, "EmployeeGroup_" + (id ?? "Unassigned"));
                    Ui.VList(body, 5, 0).padding = new RectOffset(14, 0, 0, 3);
                    if (shop != null && (shop.Assembler != null || shop.EmployeeTrained))
                        Ui.Size(Ui.Button(body, "自动装机 · " + (shop.AutoBuild ? "开" : "关"),
                            () => { S.ToggleAutoBuildAt(shop); w.Rebuild(); }, 16), -1, 34);
                    if (!employees.Any()) Line(body, "本店暂无在编员工。", 16, Ui.Dim);
                    foreach (var row in employees) BuildEmployeeCard(body, row, w, expandedEmployees);
                }
            });
        }

        private void BuildEmployeeCard(Transform parent, EmployeeAssignment row, Window w, HashSet<string> expandedEmployees)
        {
            var e = row.Person;
            bool expanded = expandedEmployees.Contains(e.Id);
            var card = Ui.Panel(parent, "EmployeeCard_" + e.Id, Ui.PanelBg2).rectTransform;
            Ui.VList(card, 4, 8);
            var header = Ui.Row(card, 54, 10);
            void ToggleDetails()
            {
                if (!expandedEmployees.Add(e.Id)) expandedEmployees.Remove(e.Id);
                w.Rebuild();
            }
            PortraitThumb(header, S.PersonIdOf(e), 52, ToggleDetails);
            var summary = Ui.Size(Ui.Rect(header, "EmployeeSummary"), -1, -1, 1);
            Ui.VList(summary, 0, 0);
            string role = EmployeeRoleLabel(row.Role, row.Shop);
            Ui.Size(Ui.Label(summary, $"<b>{e.Name} · {role}</b>", 19, TextAnchor.MiddleLeft, Ui.Accent), -1, 26);
            var (duties, _, _) = EmployeeRolePreview(e, row.Role, row.Shop);
            string status = row.Role == EmployeeRole.Unassigned ? duties : $"{duties} · {S.EmployeeStatus(row)}";
            Ui.Size(Ui.Label(summary, $"{status} · 日薪 {e.DailyWage}", 16, TextAnchor.MiddleLeft, Ui.Dim), -1, 24);
            Ui.Size(Ui.Button(header, expanded ? "收起详情 ▴" : "展开详情 ▾", ToggleDetails, 16,
                expanded ? Ui.ButtonHot : Ui.ButtonBg), 130, 34).name = "EmployeeDetails_" + e.Id;
            if (expanded) BuildEmployeeDetails(card, row, w);
        }

        private void BuildEmployeeDetails(RectTransform card, EmployeeAssignment row, Window w)
        {
            var e = row.Person;
            if (row.Shop != null) Line(card, "工作地点 · " + S.ShopTitle(row.Shop), 16);
            var (_, metric, detail) = EmployeeRolePreview(e, row.Role, row.Shop);
            if (row.Role != EmployeeRole.Unassigned) Line(card, metric, 17, Ui.Accent);
            Para(card, detail, 15, Ui.Dim);
            Para(card, S.EmployeePaySummary(e), 16, Ui.Dim);
            var destination = S.ShopAt(e.TransferShopId);
            if (destination != null)
                Line(card, $"明天调往 {S.ShopTitle(destination)} · {EmployeeRoleLabel(S.PlannedEmployeeRole(row), destination)}", 17, Ui.Good);
            var actions = Ui.Row(card, 40);
            Ui.Size(Ui.Button(actions, "安排工作", () => OpenEmployeeWork(e.Id, w), 17, Ui.ButtonHot), 150);
            if (destination != null) Ui.Size(Ui.Button(actions, "取消明日安排", () => { S.CancelEmployeeTransfer(e.Id); w.Rebuild(); }, 16), 150);
            Ui.Size(Ui.Button(actions, "调整班次", () => OpenEmployeeShift(e.Id, w), 16), 140);
            if (row.Shop != null && row.Role == EmployeeRole.Cashier && !e.AssemblyTrained && !S.IsDedicatedVenue(row.Shop.Place))
                Ui.Size(Ui.Button(actions, "装机培训…", () => ConfirmAction("装机培训", "培训费 ¥1,200，日薪增加 ¥60，可在收银空闲时装机。", "确认培训", () => { S.TrainNamedEmployee(e.Id); w.Rebuild(); }), 15), 130);
            if (row.Role == EmployeeRole.Buyer)
                Ui.Size(Ui.Button(actions, "安排进货", () => OpenEmployeePurchase(e.Id, w), 17, Ui.ButtonHot), 150);
            Ui.Size(Ui.Button(actions, "辞退…", () => ConfirmAction("辞退员工", $"辞退{e.Name}，同时取消未生效的安排。正在进货时需要先等人和货回店。", "确认辞退", () => { S.DismissEmployee(e.Id); w.Rebuild(); }), 16, Ui.Warn), 110);
            Ui.Size(Ui.Button(actions, "交谈", () => OpenPersonDialogue(S.PersonIdOf(e)), 16), 140);
            if (row.Role != EmployeeRole.Buyer) return;
            Line(card, e.PurchasePlan.Daily ? "采购频率：" + e.PurchasePlan.FrequencyText
                + " · " + S.EmployeePurchaseNextText(e.Id) : "定期采购：未启用或已暂停", 15, Ui.Dim);
            Para(card, S.EmployeePurchaseStorageText(e.Id), 15, Ui.Accent);
            if (!string.IsNullOrEmpty(e.LastPurchaseNote)) Para(card, e.LastPurchaseNote, 15, Ui.Dim);
        }

        public void OpenEmployeeWork(string employeeId, Window parent = null)
        {
            var row = S.FindEmployee(employeeId);
            if (row == null) return;
            EmployeeRole role = row.Role == EmployeeRole.Unassigned ? EmployeeRole.Cashier : row.Role;
            string destination = (row.Shop ?? S.CurrentShop)?.Place.Id;
            OpenWindow("安排工作 · " + row.Person.Name, 940, 660, w =>
            {
                PersonSummary(w.Content, S.PersonIdOf(row.Person), row.Person.Name, "日薪 " + row.Person.DailyWage);
                Line(w.Content, "1. 做什么工作", 20);
                var roles = Ui.Row(w.Content, 44);
                foreach (EmployeeRole option in Enum.GetValues(typeof(EmployeeRole)))
                {
                    var choice = option;
                    bool venue=S.IsDedicatedVenue(S.ShopAt(destination)?.Place);
                    Ui.Size(Ui.Button(roles, venue&&choice==EmployeeRole.Cashier?"场馆值守":GameSession.RoleName(choice), () => { role = choice; w.Rebuild(); }, 18, role == choice ? Ui.ButtonHot : Ui.ButtonBg,!venue||choice==EmployeeRole.Cashier||choice==EmployeeRole.Unassigned), -1, -1, 1);
                }
                BuildRecruitRolePreview(w.Content, row.Person, role, S.ShopAt(destination));
                if (role != EmployeeRole.Unassigned)
                {
                    Line(w.Content, "2. 在哪家店工作", 20);
                    var shops = Ui.Scroll(w.Content, 210);
                    foreach (var shop in S.MyShops)
                    {
                        string target = shop.Place.Id;
                        Ui.Size(Ui.Button(shops, $"{S.ShopTitle(shop)} · {S.City.StreetName(shop.Place.StreetId)}", () => { destination = target; w.Rebuild(); }, 18, destination == target ? Ui.ButtonHot : Ui.ButtonBg), -1, 44);
                    }
                }
                bool tomorrow = role != EmployeeRole.Unassigned && row.Shop != null && row.Shop.Place.Id != destination;
                Para(w.Content, role == EmployeeRole.Unassigned ? "暂不分配工作，仍按原日薪发工资；以后可重新安排。"
                    : tomorrow ? $"跨店安排明天生效。今天仍在{S.ShopTitle(row.Shop)}做{GameSession.RoleName(row.Role)}，明天到新店做{GameSession.RoleName(role)}。"
                    : "本次是首次安排或同店换岗，确认后今天起按新岗位工作。", 18, Ui.Accent);
                string blocker = S.EmployeeAssignmentBlocker(employeeId, destination, role);
                if (blocker != null) Para(w.Content, blocker, 16, Ui.Warn);
                var occupant = S.EmployeeInPosition(destination, role);
                if (blocker!=null&&occupant != null && occupant.Person.Id != employeeId && S.EmployeeAssignmentBlocker(employeeId, destination, role, true) == null)
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
                Para(w.Content,"通勤车辆与入库车辆可供员工临时使用；管理具体车辆请到赛场旁的车库建筑按 F。",16,Ui.Dim);
                Ui.Size(Ui.Button(w.Content,"湖区四大赛车场",OpenRacingDestinations,18),-1,40);
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
            var catalog = S.EmployeePurchaseCatalog();
            var draft = _purchaseDrafts.TryGetValue(employeeId, out var savedDraft) ? savedDraft.Copy() : worker.Person.PurchasePlan.Copy();
            S.PrepareEmployeePurchaseDraft(draft, catalog);
            _purchaseDrafts[employeeId] = draft;
            string browseSource = S.EmployeePurchaseBrowseSource(draft, catalog);
            string category = browseSource!=null ? "全部" : worker.Shop.RetailKind == "flowers" ? "鲜花与盆栽" : worker.Shop.RetailKind == "produce" ? "米与生鲜" : worker.Shop.RetailKind == "food" ? "外带食品" : "全部", search = "";
            void BrowseSource(string sourceId)
            {
                if(!S.RememberEmployeePurchaseSource(employeeId,sourceId))return;
                browseSource=sourceId;
                draft.BrowseSourceId=sourceId;
                draft.BrowseSourceSelected=true;
            }
            bool selectedOnly = importedOrderId.HasValue;
            var selected = draft.Items.ToDictionary(GameSession.PurchaseKey, x => x.Copy());
            var order = draft.Items.Select(GameSession.PurchaseKey).ToList();
            void Sync()
            {
                order.RemoveAll(key => !selected.ContainsKey(key));
                foreach (string key in selected.Keys) if (!order.Contains(key)) order.Add(key);
                draft.Items = order.Where(key => selected[key].Count > 0).Select(key => selected[key].Copy()).ToArray();
            }
            Sync();
            OpenWindow("定期采购清单 · " + worker.Person.Name + " · " + S.ShopTitle(worker.Shop), 1560, 880, w =>
            {
                var sourceOffers = catalog.Where(o => browseSource == null || o.Source.Id == browseSource).ToArray();
                var categories = new[]{"全部"}.Concat(sourceOffers.Select(o => o.Category)).Distinct().ToArray();
                if (!categories.Contains(category)) category = "全部";
                var savedPlan = worker.Person.PurchasePlan;
                bool threshold = draft.InventoryThreshold;
                var quote = S.EstimateEmployeePurchase(employeeId, draft, catalog);
                bool ownVehicle = draft.Transport == VehicleKind.EBike || draft.Transport == VehicleKind.Tricycle || draft.Transport == VehicleKind.Van;
                bool releaseVehicle = ownVehicle && S.Vehicle == draft.Transport && S.FreeVehicleCount(draft.Transport) == 0;

                var split = Ui.Row(w.Content, 100, 12);
                split.name = "BuyerSplit";
                var splitSize = split.GetComponent<LayoutElement>(); splitSize.flexibleHeight = 1; splitSize.minHeight = 300;
                split.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

                // ---- 左：店家和商品 ----
                var left = Ui.Rect(split, "BuyerCatalog");
                Ui.VList(left, 6, 0);
                Ui.Size(left, 0, -1, 1).GetComponent<LayoutElement>().minWidth = 0;

                // ---- 右：采购了什么 + 怎么跑 ----
                var right = Ui.FlatPanel(split, "BuyerSide", Ui.FlatBand, Ui.FlatHotEdge).rectTransform;
                Ui.VList(right, 6, 12);
                var rightSize = Ui.Size(right, 540, -1, 0).GetComponent<LayoutElement>(); rightSize.minWidth = 540; rightSize.flexibleWidth = 0;

                string intro = importedOrderId.HasValue ? "订单 #" + importedOrderId.Value + " 缺件已合并，点“完成”保存。" : S.EmployeePurchaseStorageText(employeeId);
                Ui.Size(Ui.Label(left, intro, 14, TextAnchor.MiddleLeft, Ui.Accent), -1, 22);
                if (threshold)
                {
                    var note = Ui.FlatPanel(left, "BuyerThresholdNote", Ui.FlatBand, Ui.FlatCardEdge).rectTransform;
                    Ui.VList(note, 8, 16); Ui.Size(note, -1, 170);
                    Ui.Size(Ui.Label(note, "<b>现在是「按补货清单自动补」</b>", 20, TextAnchor.MiddleLeft, Ui.Accent), -1, 30);
                    Ui.Size(Ui.Label(note, "进货员不看这里的商品，而是按店里补货清单的目标 / 下限：库存低于下限，就补到目标。右边是按现在库存算出的下一趟。\n想自己挑店家和数量，点右边的「固定清单」。", 16, TextAnchor.UpperLeft, Ui.Dim), -1, 80);
                    Ui.Size(Ui.Button(note, "去调整补货清单 ›", () => { w.Close(); OpenCityProcurementFor(worker.Shop); }, 17, Ui.ButtonHot), 200, 38).name = "BuyerOpenRestock";
                }
                else
                {
                    var filters = Ui.Row(left, 38, 6);
                    string sourceName = browseSource == null ? "全部货源" : S.City.GetPlace(browseSource).Name;
                    Ui.Size(Ui.Button(filters, sourceName + "  ▼", () => OpenWindow("浏览货源", 740, 660, picker =>
                    {
                        Ui.Size(Ui.Button(picker.Content, "全部货源", () => { BrowseSource(null); picker.Close(); w.Rebuild(); }, 18), -1, 42);
                        foreach (var place in catalog.Select(x => x.Source).GroupBy(x => x.Id).Select(g => g.First()))
                        {
                            var source = place;
                            Ui.Size(Ui.Button(picker.Content, source.Name, () => { BrowseSource(source.Id); picker.Close(); w.Rebuild(); }, 18), -1, 42);
                        }
                    }), 16, Ui.ButtonHot), 255, 36);
                    Ui.Size(Ui.Button(filters, category + " ▾", () => OpenWindow("商品分类", 620, 620, picker =>
                    {
                        foreach (string name in categories)
                        {
                            string choice = name;
                            Ui.Size(Ui.Button(picker.Content, choice, () => { category = choice; picker.Close(); w.Rebuild(); }, 17), -1, 38);
                        }
                    }), 16), 175, 36);
                    Ui.Size(Ui.TextInput(filters, search, "搜索商品", v => { search = v; w.RequestRebuild(); }), -1, 36, 1);
                    Ui.Size(Ui.Button(filters, selectedOnly ? "只看已选 ✓" : "只看已选", () => { selectedOnly = !selectedOnly; w.Rebuild(); }, 15), 130, 36);
                    var goods = Ui.Scroll(left, autoSize: false);
                    var goodsFrame = (RectTransform)goods.parent.parent;
                    goodsFrame.name = "PurchaseCatalog";
                    var goodsLayout = Ui.Size(goodsFrame, -1, 160).GetComponent<LayoutElement>();
                    goodsLayout.preferredHeight = 280; goodsLayout.flexibleHeight = 1;
                    var filtered = sourceOffers.Where(o => (category == "全部" || o.Category == category)
                        && (!selectedOnly || selected.TryGetValue(o.Key, out var selectedLine) && selectedLine.Count > 0)
                        && (string.IsNullOrWhiteSpace(search) || o.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                            || o.Source.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
                    PurchaseListViewport.Bind(goods, filtered.Length, 74, (index, card) =>
                    {
                        var offer = filtered[index];
                        string offerKey = offer.Key;
                        int count = selected.TryGetValue(offerKey, out var lineItem) ? lineItem.Count : 0;
                        var capacity = S.SupplierAllocationPreview(employeeId, draft, offer);
                        int minimum = S.PurchaseMinimum(offer);
                        void Set(int value, bool maximumSelection = false)
                        {
                            int room = maximumSelection ? capacity.Maximum : capacity.FixedMaximum;
                            value = Math.Min(value, room);
                            if (value > 0 && value < minimum) value = room < minimum ? 0 : value < count ? 0 : minimum;
                            if (value <= 0) selected.Remove(offerKey);
                            else
                            {
                                var updated = offer.Line(Math.Min(100, value));
                                updated.BuyMaximum = maximumSelection;
                                selected[offerKey] = updated;
                            }
                            Sync(); w.RequestRebuild();
                        }
                        var line = Ui.Row(card, 66, 8);
                        line.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                        if (count > 0) line.gameObject.AddComponent<Image>().color = Ui.FlatRow;
                        string details = offer.Source.Name + (minimum > 1 ? " · 起订 " + minimum + " 件" : "");
                        if (offer.Kind == PurchaseGoodsKind.Part)
                            details += $" · 库存 {worker.Shop.Sim.Stock.CountAll(PartCatalog.Get(offer.Id))} · 在途 {S.PendingPurchaseCount(worker.Shop, offer.Id)}";
                        else if (offer.Kind == PurchaseGoodsKind.Gift)
                            details += $" · 库存 {worker.Shop.Sim.Stock.GoodsCount(offer.Id)}";
                        var label = CompactProduct(line, offer.Name, offer.UnitKg, details, false);
                        label.Tip("该货源本型号日上限 " + S.PurchaseLimit(offer) + " · 今日实货剩余 " + S.PurchaseRemaining(offer) + "\n" + capacity.Allocation);
                        Ui.Size(Ui.Label(line, offer.Price + (offer.Kind == PurchaseGoodsKind.Rice ? "/袋" : offer.Kind == PurchaseGoodsKind.Ingredient ? "/份" : "/件"), 16, TextAnchor.MiddleLeft, Ui.Good), 110);
                        QuantityStepper(w, line, offerKey, count, 0, Math.Max(count, capacity.FixedMaximum), 1, value => Set((int)value));
                        Ui.Size(Ui.Button(line, lineItem?.BuyMaximum == true ? "最大 ✓" : "最大", () => Set(capacity.Maximum, true),
                            15, Ui.ButtonBg, capacity.Maximum >= minimum), 62, 34).name = "PurchaseMaximum_" + offerKey;
                    });
                }

                // ---- 右侧内容 ----
                var head = Ui.Row(right, 34, 8);
                Ui.Size(Ui.Label(head, "<b>采购了什么</b>", 20, TextAnchor.MiddleLeft, Ui.Accent), -1, 32, 1);
                if (!threshold) Ui.Size(Ui.Button(head, "清空", () => { selected.Clear(); Sync(); w.Rebuild(); }, 14), 64, 30);
                var modes = Ui.Row(right, 36, 6);
                Ui.Size(Ui.Button(modes, "固定清单", () => { draft.InventoryThreshold = false; w.Rebuild(); }, 15, !threshold ? Ui.ButtonHot : Ui.ButtonBg), -1, 34, 1).name = "BuyerModeFixed";
                Ui.Size(Ui.Button(modes, "按补货清单自动补", () => { draft.InventoryThreshold = true; w.Rebuild(); }, 15, threshold ? Ui.ButtonHot : Ui.ButtonBg), -1, 34, 1).name = "BuyerModeThreshold";

                var chosenList = GoodsList(right, 140);
                ((RectTransform)chosenList.parent.parent).name = "PurchaseSelected";
                if (threshold)
                {
                    var preview = quote.Preview;
                    if (preview.Count == 0) Para(chosenList, quote.Error ?? "目前没有需要补的货。", 16, Ui.Dim);
                    foreach (var item in preview)
                    {
                        var row = Ui.Row(chosenList, 50, 6);
                        row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                        var label = Ui.Label(row, item.Name + " <color=#E8BE62>需补 " + item.Count + " 件</color>\n<size=13><color=#AAB4BB>" + item.SourceName + "</color></size>", 16, TextAnchor.MiddleLeft);
                        label.verticalOverflow = VerticalWrapMode.Truncate;
                        Ui.Size(label, -1, 46, 1); { var le = label.GetComponent<LayoutElement>(); le.minWidth = 0; le.preferredWidth = 0; }
                        Ui.Size(Ui.Label(row, item.Total.ToString(), 16, TextAnchor.MiddleRight, Ui.Good), 100, 46);
                    }
                }
                else
                {
                    var chosen = draft.Items;
                    if (chosen.Length == 0) Para(chosenList, "还没选商品。在左边商品目录加数量，会集中显示在这里。", 16, Ui.Dim);
                    foreach (var choice in chosen)
                    {
                        var picked = choice;
                        var offer = S.EmployeePurchaseOffer(choice, catalog: catalog);
                        string key = GameSession.PurchaseKey(choice);
                        string count = choice.BuyMaximum ? "最大" : "×" + choice.Count;
                        var row = Ui.Row(chosenList, 50, 4);
                        row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                        var label = Ui.Label(row, GameSession.PurchaseItemName(choice) + " <color=#E8BE62>" + count + "</color>\n<size=13><color=#AAB4BB>" + (offer?.Source.Name ?? choice.SourceId ?? "自动选货源") + "</color></size>", 16, TextAnchor.MiddleLeft);
                        label.verticalOverflow = VerticalWrapMode.Truncate;
                        Ui.Size(label, -1, 46, 1); { var le = label.GetComponent<LayoutElement>(); le.minWidth = 0; le.preferredWidth = 0; }
                        Ui.Size(Ui.Label(row, offer == null ? "不可用" : (offer.Price * choice.Count).ToString(), 15, TextAnchor.MiddleRight, offer == null ? Ui.Warn : Ui.Good), 84, 46);
                        int priority = order.IndexOf(key);
                        void Move(int offset) { int index = order.IndexOf(key); var other = order[index + offset]; order[index + offset] = key; order[index] = other; Sync(); w.Rebuild(); }
                        Ui.Size(Ui.Button(row, "↑", () => Move(-1), 16, Ui.ButtonBg, priority > 0), 30, 34);
                        Ui.Size(Ui.Button(row, "↓", () => Move(1), 16, Ui.ButtonBg, priority < order.Count - 1), 30, 34);
                        Ui.Size(Ui.Button(row, "移出", () => { selected.Remove(key); Sync(); w.Rebuild(); }, 14), 50, 34);
                    }
                    if (chosen.Length > 1) Ui.Size(Ui.Label(right, "越靠上越优先买；钱或额度不够时，后面的会被跳过。", 13, TextAnchor.MiddleLeft, Ui.Dim), -1, 20);
                }

                string schedule = savedPlan.Daily ? $"已生效 · {savedPlan.FrequencyText} · {S.EmployeePurchaseNextText(employeeId)}" : "尚未启用定期采购";
                var scheduleStatus = Ui.Size(Ui.Label(right, schedule + " · 点「完成」保存", 14, TextAnchor.MiddleLeft, Ui.Dim), -1, 38);
                scheduleStatus.name = "BuyerStatus";
                var frequency = Ui.Row(right, 36, 6);
                Ui.Size(Ui.Label(frequency, "频率", 15, TextAnchor.MiddleLeft, Ui.Dim), 44, 34);
                foreach (PurchaseFrequency choice in Enum.GetValues(typeof(PurchaseFrequency)))
                {
                    var picked = choice;
                    string label = choice == PurchaseFrequency.Daily ? "每天" : choice == PurchaseFrequency.MondayWednesdayFriday ? "周一三五" : "每周三";
                    var button = Ui.Button(frequency, label, () => { draft.HasWeeklySchedule = true; draft.Frequency = picked; w.Rebuild(); }, 15,
                        draft.Schedule == choice ? Ui.ButtonHot : Ui.ButtonBg);
                    button.name = "PurchaseFrequency_" + choice;
                    Ui.Size(button, -1, 34, 1);
                }
                var transport = Ui.Row(right, 36, 4);
                transport.name = "PurchaseTransport";
                Ui.Size(Ui.Label(transport, "交通", 15, TextAnchor.MiddleLeft, Ui.Dim), 44, 34);
                foreach (var kind in new[] { VehicleKind.Walk, VehicleKind.Taxi, VehicleKind.EBike, VehicleKind.Tricycle, VehicleKind.Van })
                {
                    VehicleKind mode = kind;
                    bool hired = mode == VehicleKind.Walk || mode == VehicleKind.Taxi;
                    string label = mode == VehicleKind.Walk ? "步行" : mode == VehicleKind.Taxi ? "打车" : Vehicles.Get(mode).Name;
                    var button = Ui.Button(transport, label, () => { draft.Transport = mode; w.Rebuild(); }, 14,
                        draft.Transport == mode ? Ui.ButtonHot : Ui.ButtonBg, hired || S.VehicleCount(mode) > 0);
                    button.Tip(hired ? "步行 / 打车：由货车把货送回店。" : "自有车辆，当前空闲 " + S.FreeVehicleCount(mode) + " 台。");
                    Ui.Size(button, -1, 34, 1);
                }
                if (releaseVehicle)
                    Ui.Size(Ui.Button(right, "老板改为步行，释放这台车", () => { S.SetVehicle(VehicleKind.Walk); w.Rebuild(); }, 14), -1, 32);
                var budget = Ui.Row(right, 40, 6);
                string budgetName = draft.Schedule == PurchaseFrequency.Daily ? "每日" : "每趟";
                var budgetLabel = Ui.Size(Ui.Label(budget, $"{budgetName}预算  货款 {quote.Goods} + 车费 {quote.Travel} + 运费 {quote.Freight}", 14, TextAnchor.MiddleLeft, Ui.Dim), -1, 38, 1);
                budgetLabel.name = "EmployeePurchaseBudget";
                Ui.Size(Ui.Label(budget, "<b>" + quote.Total + "</b>", 24, TextAnchor.MiddleRight, Ui.Accent), 170, 38);

                var actions = Ui.Row(right, 50, 8);
                actions.name = "PurchaseActions";
                Ui.Size(Ui.Button(actions, "完成", () =>
                {
                    Sync(); draft.Daily = draft.InventoryThreshold || draft.Items.Length > 0;
                    var session = S;
                    string failure = null;
                    void CaptureFailure(string reason) => failure = reason;
                    session.Failed += CaptureFailure;
                    bool saved;
                    try { saved = session.CompleteEmployeePurchase(employeeId, draft); }
                    finally { session.Failed -= CaptureFailure; }
                    if (!saved)
                    {
                        scheduleStatus.text = "未保存 · " + (failure ?? "请检查采购清单") + " · " + schedule;
                        scheduleStatus.color = Ui.Warn;
                        return;
                    }
                    _purchaseDrafts.Remove(employeeId);
                    Toast(draft.Daily ? $"采购安排已保存 · {draft.FrequencyText} · {S.EmployeePurchaseNextText(employeeId)}" : "已清空采购安排，不再自动进货。");
                    if (parent != null) { w.Close(); parent.Rebuild(); RebuildDock(); }
                    else CloseModalWindows();
                }, 20, Ui.ButtonHot), -1, 48, 1).name = "PurchaseDone";
                if (savedPlan.Daily)
                    Ui.Size(Ui.Button(actions, "暂停采购", () =>
                    {
                        var plan = savedPlan.Copy(); plan.Daily = false;
                        if (S.ConfigureEmployeePurchase(employeeId, plan))
                        {
                            _purchaseDrafts.Remove(employeeId);
                            if (parent != null) { w.Close(); parent.Rebuild(); RebuildDock(); }
                            else CloseModalWindows();
                        }
                    }, 15), 110, 48);
            }, scrollContent: false);
        }

    }

    internal sealed class PurchaseListViewport : MonoBehaviour
    {
        private RectTransform _content, _viewport;
        private Action<int, RectTransform> _build;
        private int _count, _first = -1, _end = -1;
        private float _rowHeight;
        private readonly Dictionary<int, RectTransform> _visibleRows = new Dictionary<int, RectTransform>();
        private readonly List<int> _leavingRows = new List<int>();

        public static void Bind(RectTransform content, int count, float rowHeight, Action<int, RectTransform> build)
        {
            var list = content.gameObject.AddComponent<PurchaseListViewport>();
            list._content = content;
            list._viewport = (RectTransform)content.parent;
            list._count = count;
            list._rowHeight = rowHeight;
            list._build = build;
            // The scrollbar keeps the full range even when only nearby rows exist.
            content.sizeDelta = new Vector2(0, count * rowHeight);
            list.Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            float height = _viewport.rect.height;
            float offset = Mathf.Clamp(_content.anchoredPosition.y, 0, Mathf.Max(0, _content.rect.height - height));
            int first = Mathf.Max(0, Mathf.FloorToInt(offset / _rowHeight) - 1);
            int end = Mathf.Min(_count, Mathf.CeilToInt((offset + height) / _rowHeight) + 1);
            if (_first == first && _end == end) return;
            _first = first;
            _end = end;
            _leavingRows.Clear();
            foreach (var entry in _visibleRows)
                if (entry.Key < first || entry.Key >= end) _leavingRows.Add(entry.Key);
            foreach (int index in _leavingRows)
            {
                var row = _visibleRows[index];
                row.gameObject.SetActive(false);
                Kill.It(row.gameObject);
                _visibleRows.Remove(index);
            }
            for (int index = first; index < end; index++)
            {
                if (_visibleRows.ContainsKey(index)) continue;
                var row = Ui.Rect(_content, "PurchaseRow_" + index)
                    .Anchor(0, 1, 1, 1, 6, -(index + 1) * _rowHeight, 6, index * _rowHeight + 4);
                Ui.VList(row, 4, 0);
                _visibleRows.Add(index, row);
                _build(index, row);
            }
        }
    }
}
