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
            var expandedEmployees = new HashSet<string>();
            OpenWindow("员工管理", 1120, 900, w =>
            {
                var rows = S.Employees;
                foreach(var shop in S.MyShops)Line(w.Content,S.ShopTitle(shop)+" · 员工 "+S.StaffCommitted(shop)+" / "+GameSession.StaffLimit(shop.Place.Size)+" 人",17,Ui.Accent);
                Line(w.Content, $"员工 {rows.Count} 人 · 基础日薪合计 ¥{rows.Sum(x => x.Person.DailyWageYuan):N0} · 排班预计 {PCShopSell.Core.Common.Money.FromCents(rows.Sum(x=>GameSession.EstimateShiftPay(x.Person,x.Person.Schedule?.PendingAt>=0?x.Person.Schedule.PendingStart:x.Person.ShiftStart,x.Person.Schedule?.PendingAt>=0?x.Person.Schedule.PendingEnd:x.Person.ShiftEnd).Cents))}", 21, Ui.Accent);
                Line(w.Content, "同店换岗当天生效，跨店调动次日生效。", 16, Ui.Dim);
                var bar = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Button(bar, "查看车辆与占用", OpenFleetPanel, 17), 240);
                Ui.Size(Ui.Button(bar, "在当前店招人", () => { w.Close(); if(S.IsOwnedVenue(S.CurrentPlace))OpenVenueManagement(S.CurrentPlace);else{OpenDock();SelectTab(ShopTab.Staff);} }, 17, Ui.ButtonBg, S.InPlayerShop), 220);
                if (rows.Count == 0) Para(w.Content, "还没有员工。到自己的店里打开“员工”页，聘用后选择工作安排。", 20);
                foreach (var shop in S.MyShops.Where(x => x.Assembler != null || x.EmployeeTrained))
                {
                    var target = shop;
                    Ui.Size(Ui.Button(w.Content, S.ShopTitle(target) + (target.AutoBuild ? " · 自动装机：开" : " · 自动装机：关"), () => { S.ToggleAutoBuildAt(target); w.Rebuild(); }, 16), -1, 34);
                }
                foreach (var row in rows)
                {
                    var e = row.Person;
                    bool expanded = expandedEmployees.Contains(e.Id);
                    var card = Ui.Panel(w.Content, "EmployeeCard_" + e.Id, Ui.PanelBg2).rectTransform;
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
                    Ui.Size(Ui.Label(summary, $"<b>{e.Name}</b> · 日薪 {e.DailyWage}", 19, TextAnchor.MiddleLeft, Ui.Accent), -1, 26);
                    string status = S.EmployeeStatus(row);
                    if (row.Shop != null) status = $"{S.ShopTitle(row.Shop)} · {GameSession.RoleName(row.Role)} · {status}";
                    Ui.Size(Ui.Label(summary, status, 16, TextAnchor.MiddleLeft, Ui.Dim), -1, 24);
                    Ui.Size(Ui.Button(header, expanded ? "收起详情 ▴" : "展开详情 ▾", ToggleDetails, 16,
                        expanded ? Ui.ButtonHot : Ui.ButtonBg), 130, 34).name = "EmployeeDetails_" + e.Id;
                    if (expanded) BuildEmployeeDetails(card, row, w);
                }
            });
        }

        private void BuildEmployeeDetails(RectTransform card, EmployeeAssignment row, Window w)
        {
            var e = row.Person;
            if (row.Shop != null) Line(card, "工作地点 · " + row.Shop.Place.Name, 16);
            Para(card, GameSession.EmployeeAbilities(e), 15, Ui.Dim);
            Para(card, S.EmployeePaySummary(e), 16, Ui.Dim);
            var destination = S.ShopAt(e.TransferShopId);
            Line(card, destination == null ? "明天：保持当前安排" : $"明天：{S.ShopTitle(destination)}（{destination.Place.Name}） · {GameSession.RoleName(S.PlannedEmployeeRole(row))}", 17, destination == null ? Ui.Dim : Ui.Good);
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
                Para(w.Content, GameSession.EmployeeAbilities(row.Person), 17, Ui.Accent);
                Para(w.Content, "不擅长也可以安排，效率按员工能力计算。换岗位不自动培训，也不改变工资。", 17, Ui.Dim);
                if(role==EmployeeRole.PetKeeper)
                    Line(w.Content,"饲养员 · 照顾 2 只宠物 · 魅力 "+GameSession.EmployeeCharm(row.Person)+" · 每日照顾状态 +"+GameSession.PetKeeperCareBonus(row.Person),17,Ui.Good);
                Line(w.Content, "1. 做什么工作", 20);
                var roles = Ui.Row(w.Content, 44);
                foreach (EmployeeRole option in Enum.GetValues(typeof(EmployeeRole)))
                {
                    var choice = option;
                    bool venue=S.IsDedicatedVenue(S.ShopAt(destination)?.Place);
                    Ui.Size(Ui.Button(roles, venue&&choice==EmployeeRole.Cashier?"场馆值守":GameSession.RoleName(choice), () => { role = choice; w.Rebuild(); }, 18, role == choice ? Ui.ButtonHot : Ui.ButtonBg,!venue||choice==EmployeeRole.Cashier||choice==EmployeeRole.Unassigned), -1, -1, 1);
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
                if(role==EmployeeRole.Salesperson)
                    Para(w.Content,$"魅力 {GameSession.EmployeeCharm(row.Person)}：实际在店上班时，顾客购买意愿 +{GameSession.EmployeeSalesBonus(row.Person)*100:0.#}个百分点。收银仍由老板或收银人员负责。",17,Ui.Good);
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
            OpenWindow("定期采购清单 · " + worker.Person.Name + " · " + S.ShopTitle(worker.Shop), 1160, 940, w =>
            {
                var sourceOffers = catalog.Where(o=>browseSource==null || o.Source.Id==browseSource).ToArray();
                var categories = new[]{"全部"}.Concat(sourceOffers.Select(o=>o.Category)).Distinct().ToArray();
                if(!categories.Contains(category))category="全部";
                var panel = w.Root.Find("Panel");
                var previousFooter = panel.Find("PurchaseFooter");
                if(previousFooter!=null){previousFooter.gameObject.SetActive(false);Kill.It(previousFooter.gameObject);}
                bool ownVehicle = draft.Transport == VehicleKind.EBike || draft.Transport == VehicleKind.Tricycle || draft.Transport == VehicleKind.Van;
                bool releaseVehicle = ownVehicle && S.Vehicle == draft.Transport && S.FreeVehicleCount(draft.Transport) == 0;
                float footerHeight=releaseVehicle?190:148;
                w.Content.Anchor(0,0,1,1,0,footerHeight+24,0,50);
                var layout=w.Content.GetComponent<VerticalLayoutGroup>();
                layout.spacing=6;layout.padding=new RectOffset(16,16,10,6);
                var footer=Ui.Rect(panel,"PurchaseFooter").Anchor(0,0,1,0,16,12,16,-(footerHeight+12));
                Ui.VList(footer,6,0);
                var modes = Ui.Row(w.Content, 40);
                Ui.Size(Ui.Button(modes, "固定数量清单" + (!draft.InventoryThreshold ? " ✓" : ""), () => { draft.InventoryThreshold = false; w.Rebuild(); }, 17, !draft.InventoryThreshold ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);
                Ui.Size(Ui.Button(modes, "库存阈值补货" + (draft.InventoryThreshold ? " ✓" : ""), () => { draft.InventoryThreshold = true; w.Rebuild(); }, 17, draft.InventoryThreshold ? Ui.ButtonHot : Ui.ButtonBg), -1, 40, 1);
                Ui.Size(Ui.Button(modes, "商品目标 / 补货责任", () => OpenCityProcurementFor(worker.Shop), 16), 240, 40);
                var savedPlan = worker.Person.PurchasePlan;
                string schedule = savedPlan.Daily
                    ? $"已生效 · {savedPlan.FrequencyText} · {S.EmployeePurchaseNextText(employeeId)}"
                    : "尚未启用定期采购";
                var scheduleStatus = Ui.Size(Ui.Label(w.Content, schedule + " · 修改后点「完成」保存", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 22);
                Ui.Size(Ui.Label(w.Content,S.EmployeePurchaseStorageText(employeeId),15,TextAnchor.MiddleLeft,Ui.Accent),-1,24);
                if (importedOrderId.HasValue)
                    Ui.Size(Ui.Label(w.Content,"订单 #"+importedOrderId.Value+" 缺件已合并，点“完成”保存。",15,TextAnchor.MiddleLeft,Ui.Accent),-1,24);
                var frequency = Ui.Row(w.Content, 38);
                Ui.Size(Ui.Label(frequency, "采购频率", 17), 100);
                foreach (PurchaseFrequency choice in Enum.GetValues(typeof(PurchaseFrequency)))
                {
                    var selectedFrequency=choice;
                    string label=choice==PurchaseFrequency.Daily?"每天一次":choice==PurchaseFrequency.MondayWednesdayFriday?"周一、三、五":"每周三";
                    var button=Ui.Button(frequency,label,()=>{draft.HasWeeklySchedule=true;draft.Frequency=selectedFrequency;w.Rebuild();},17,
                        draft.Schedule==choice?Ui.ButtonHot:Ui.ButtonBg);
                    button.name="PurchaseFrequency_"+choice;Ui.Size(button,150,36);
                }
                string purchaseMode = draft.InventoryThreshold ? "按库存缺口补货" : "按清单顺序和数量采购";
                Ui.Size(Ui.Label(w.Content, purchaseMode + " · 批发市场周末休息 · 额度已扣除其他已保存的采购计划", 15, TextAnchor.MiddleLeft, Ui.Dim), -1, 24);
                var filters = Ui.Row(w.Content, 38);
                string sourceName = browseSource == null ? "全部货源" : S.City.GetPlace(browseSource).Name;
                Ui.Size(Ui.Button(filters, sourceName + "  ▼", () => OpenWindow("浏览货源", 740, 660, picker =>
                {
                    Ui.Size(Ui.Button(picker.Content, "全部货源", () => { BrowseSource(null); picker.Close(); w.Rebuild(); }, 18), -1, 42);
                    foreach (var place in catalog.Select(x => x.Source).GroupBy(x => x.Id).Select(g => g.First()))
                    {
                        var source = place;
                        Ui.Size(Ui.Button(picker.Content, source.Name, () => { BrowseSource(source.Id); picker.Close(); w.Rebuild(); }, 18), -1, 42);
                    }
                }), 16), 255);
                Ui.Size(Ui.Button(filters, category + " ▾", () => OpenWindow("商品分类", 620, 620, picker =>
                {
                    foreach (string name in categories)
                    {
                        string choice = name;
                        Ui.Size(Ui.Button(picker.Content, choice, () => { category = choice; picker.Close(); w.Rebuild(); }, 17), -1, 38);
                    }
                }), 16), 175);
                Ui.Size(Ui.TextInput(filters, search, "搜索商品", v => { search = v; w.RequestRebuild(); }), -1, 36, 1);
                Ui.Size(Ui.Button(filters, selectedOnly ? "只看已选 ✓" : "只看已选", () => { selectedOnly = !selectedOnly; w.Rebuild(); }, 15), 130);
                var goods = Ui.Scroll(w.Content, autoSize: false);
                var goodsFrame=(RectTransform)goods.parent.parent;
                goodsFrame.name="PurchaseCatalog";
                var goodsLayout=Ui.Size(goodsFrame,-1,160).GetComponent<LayoutElement>();
                goodsLayout.preferredHeight=280;goodsLayout.flexibleHeight=2;
                var filtered = sourceOffers.Where(o => (category == "全部" || o.Category == category)
                    && (!selectedOnly || selected.TryGetValue(o.Key, out var selectedLine) && selectedLine.Count > 0)
                    && (string.IsNullOrWhiteSpace(search) || o.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                        || o.Source.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
                PurchaseListViewport.Bind(goods, filtered.Length, 154, (index, card) =>
                {
                    var offer = filtered[index];
                    string offerKey = offer.Key;
                    int count = selected.TryGetValue(offerKey, out var lineItem) ? lineItem.Count : 0;
                    var capacity = S.SupplierAllocationPreview(employeeId, draft, offer);
                    int minimum=S.PurchaseMinimum(offer);
                    void Set(int value,bool maximumSelection=false)
                    {
                        int room = maximumSelection ? capacity.Maximum : capacity.FixedMaximum;
                        value=Math.Min(value,room);
                        if(value>0&&value<minimum)value=room<minimum?0:value<count?0:minimum;
                        if (value <= 0) selected.Remove(offerKey);
                        else
                        {
                            var updated = offer.Line(Math.Min(100, value));
                            updated.BuyMaximum = maximumSelection;
                            selected[offerKey] = updated;
                        }
                        Sync();w.RequestRebuild();
                    }
                    var line = Ui.Row(card, 68);
                    string details = offer.Source.Name+(minimum>1?" · 起订 "+minimum+" 件":"");
                    if (offer.Kind == PurchaseGoodsKind.Part)
                        details += $" · 库存 {worker.Shop.Sim.Stock.CountAll(PartCatalog.Get(offer.Id))} · 在途 {S.PendingPurchaseCount(worker.Shop, offer.Id)}";
                    else if (offer.Kind == PurchaseGoodsKind.Gift)
                        details += $" · 库存 {worker.Shop.Sim.Stock.GoodsCount(offer.Id)}";
                    Ui.Size(ProductText(line, ProductLabel(offer.Name, offer.UnitKg, details), 16), -1, -1, 1);
                    Ui.Size(Ui.Label(line, offer.Price + (offer.Kind == PurchaseGoodsKind.Rice ? "/袋" : offer.Kind == PurchaseGoodsKind.Ingredient ? "/份" : "/件"), 16), 130);
                    QuantityStepper(w, line, offerKey, count, 0, Math.Max(count, capacity.FixedMaximum), 1, value => Set((int)value));
                    Ui.Size(Ui.Button(line, lineItem?.BuyMaximum == true ? "最大 ✓" : "最大", () => Set(capacity.Maximum, true),
                        15, Ui.ButtonBg, capacity.Maximum >= minimum), 65).name = "PurchaseMaximum_" + offerKey;
                    Line(card,"该货源本型号日上限 "+S.PurchaseLimit(offer)+" · 今日实货剩余 "+S.PurchaseRemaining(offer),14,Ui.Dim);
                    Line(card, capacity.Allocation + (lineItem?.BuyMaximum == true ? " · 按各日剩余额度最大采购" : ""), 14, Ui.Accent);
                });
                var summary = Ui.Row(w.Content, 32);
                Ui.Size(Ui.Label(summary, $"已选采购清单：{draft.Items.Length} 种商品 · {draft.Items.Select(x => x.SourceId).Distinct().Count()} 个货源", 18, TextAnchor.MiddleLeft, Ui.Accent), -1, -1, 1);
                Ui.Size(Ui.Button(summary, "清空清单", () => { selected.Clear(); Sync(); w.Rebuild(); }, 15), 130);
                var chosen = draft.Items;
                var chosenList = Ui.Scroll(w.Content, autoSize: false);
                var chosenFrame=(RectTransform)chosenList.parent.parent;
                chosenFrame.name="PurchaseSelected";
                var chosenLayout=Ui.Size(chosenFrame,-1,100).GetComponent<LayoutElement>();
                chosenLayout.preferredHeight=150;chosenLayout.flexibleHeight=1;
                PurchaseListViewport.Bind(chosenList, Math.Max(1, chosen.Length), 94, (index, card) =>
                {
                    if (chosen.Length == 0)
                    {
                        Para(card, "还没选商品。在上面的商品目录加数量后，会集中显示在这里。", 17, Ui.Dim);
                        return;
                    }
                    var choice = chosen[index];
                    var offer = S.EmployeePurchaseOffer(choice, catalog: catalog);
                    var row = Ui.Row(card, 58);
                    string source = offer?.Source.Name ?? choice.SourceId ?? "自动选货源";
                    Ui.Size(ProductText(row, offer == null ? GameSession.PurchaseItemName(choice) + (choice.BuyMaximum ? " ×最大" : " ×" + choice.Count) : ProductLabel(GameSession.PurchaseItemName(choice) + (choice.BuyMaximum ? " ×最大" : " ×" + choice.Count), offer.UnitKg, source), 16), -1, -1, 1);
                    Ui.Size(Ui.Label(row, offer == null ? "货源不可用" : (offer.Price * choice.Count).ToString(), 16), 130);
                    string key = GameSession.PurchaseKey(choice);
                    int priority = order.IndexOf(key);
                    void Move(int offset) { int index = order.IndexOf(key); var other = order[index + offset]; order[index + offset] = key; order[index] = other; Sync(); w.Rebuild(); }
                    Ui.Size(Ui.Button(row, "↑", () => Move(-1), 18, Ui.ButtonBg, priority > 0), 36);
                    Ui.Size(Ui.Button(row, "↓", () => Move(1), 18, Ui.ButtonBg, priority < order.Count - 1), 36);
                    Ui.Size(Ui.Button(row, "移出清单", () => { selected.Remove(GameSession.PurchaseKey(choice)); Sync(); w.Rebuild(); }, 15), 110);
                });
                var transport = Ui.Row(footer,40);
                transport.name="PurchaseTransport";
                Ui.Size(Ui.Label(transport,"运输",17,TextAnchor.MiddleLeft,Ui.Accent),52);
                foreach (var kind in new[] { VehicleKind.Walk, VehicleKind.Taxi, VehicleKind.EBike, VehicleKind.Tricycle, VehicleKind.Van })
                {
                    VehicleKind mode = kind;
                    bool hired = mode == VehicleKind.Walk || mode == VehicleKind.Taxi;
                    string label = mode == VehicleKind.Walk ? "步行 + 货车" : mode == VehicleKind.Taxi ? "打车 + 货车" : Vehicles.Get(mode).Name + $"（空闲 {S.FreeVehicleCount(mode)}）";
                    Ui.Size(Ui.Button(transport, label, () => { draft.Transport = mode; w.Rebuild(); }, 16, draft.Transport == mode ? Ui.ButtonHot : Ui.ButtonBg, hired || S.VehicleCount(mode) > 0), -1, -1, 1);
                }
                var quote = S.EstimateEmployeePurchase(employeeId, draft, catalog);
                string budgetName = draft.Schedule == PurchaseFrequency.Daily ? "每日预算" : "每次采购预算";
                var budgetLabel = Ui.Size(Ui.Label(footer, $"{budgetName}：货款 {quote.Goods} + 车费 {quote.Travel} + 运费 {quote.Freight} = 总计 {quote.Total}", 17),-1,28);
                budgetLabel.name = "EmployeePurchaseBudget";
                if (releaseVehicle)
                    Ui.Size(Ui.Button(footer, "老板改为步行，释放这台车", () => { S.SetVehicle(VehicleKind.Walk); w.Rebuild(); }, 16), -1, 36);
                var actions = Ui.Row(footer,50);
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
                    if(parent!=null) { w.Close(); parent.Rebuild(); RebuildDock(); }
                    else CloseModalWindows();
                }, 20, Ui.ButtonHot), -1, -1, 1);
                if (worker.Person.PurchasePlan.Daily)
                    Ui.Size(Ui.Button(actions, "暂停定期采购", () =>
                    {
                        var plan = worker.Person.PurchasePlan.Copy(); plan.Daily = false;
                        if (S.ConfigureEmployeePurchase(employeeId, plan))
                        {
                            _purchaseDrafts.Remove(employeeId);
                            if(parent!=null) { w.Close(); parent.Rebuild(); RebuildDock(); }
                            else CloseModalWindows();
                        }
                    }, 16), 170);
            },scrollContent:false);
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
