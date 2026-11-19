using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private sealed class ShopManagementView
        {
            private GameSession _session;
            public string ShopId, Search = "";
            public bool Procurement, OnlyShort;

            public void Bind(GameSession session)
            {
                if (_session == session) return;
                _session = session;
                Select(null);
            }

            public void Select(string shopId)
            {
                ShopId = shopId;
                Procurement = OnlyShort = false;
                Search = "";
            }
        }

        private readonly ShopManagementView _managementShops = new ShopManagementView();
        private readonly ShopManagementView _workbenchShops = new ShopManagementView();

        private void BuildShopManagementTools(RectTransform bar)
        {
            Ui.Size(Ui.Rect(bar, "Space"), 0, -1, 1);
            Ui.Size(Ui.Button(bar, "全店补货预算", OpenProcurementBudget, 16), 136, 36).name = "ProcurementBudget";
            Ui.Size(Ui.Button(bar, "直供协议与账款", OpenProcurementAgreement, 16), 152, 36);
        }

        private void BuildManagedShops(Transform parent, Window w, ShopManagementView view)
        {
            view.Bind(S);
            var shops = S.MyShops.Where(shop => shop.Rented).ToArray();
            var selected = shops.FirstOrDefault(shop => shop.Place.Id == view.ShopId);
            if (selected == null && view.ShopId != null) view.Select(null);
            if (shops.Length == 0)
            {
                Line(parent, "还没有自营门店。", 19, Ui.Dim);
                return;
            }

            var columns = Ui.Row(parent, 0, 12);
            Ui.Size(columns).GetComponent<LayoutElement>().flexibleHeight = 1;
            var left = Ui.Size(Ui.FlatPanel(columns, "ManagedShopList", Ui.FlatBand, Ui.FlatCardEdge),
                selected == null ? 0 : 630, -1, selected == null ? 1 : 0).rectTransform;
            Ui.VList(left, 4, 8);
            var header = Ui.Row(left, 39);
            Ui.Size(Ui.Label(header, "自营门店", 21, color: Ui.Accent), 0, 38, 1).fontStyle = FontStyle.Bold;
            if (selected == null)
                Ui.Size(Ui.Label(header, $"{shops.Length} 家店 · {shops.Count(shop => S.IsPlaceOpen(shop.Place))} 家营业中", 15, TextAnchor.MiddleRight, Ui.Dim), 205, 35);
            else
                Ui.Size(Ui.Button(header, "返回全部门店", () => { view.Select(null); w.Rebuild(); }, 15), 120, 33);

            var list = PhoneFlexibleScroll(left, "ManagedShopRows");
            var today = S.Clock.Today;
            var from = new GameDate(Math.Max(0, today.TotalDays - 6));
            var tasks = S.ShopEventTasks.ToLookup(task => task.ShopId);
            var roster = S.Employees.ToLookup(row => row.Shop);
            var names = Ui.Row(list, 32, 6);
            BuildManagedShopCells(names, selected != null, "门店", "营业状态", "进货员", "最近到货", "今日收入", "近 7 日收支差额", "待处理");
            foreach (var shop in shops)
            {
                string id = shop.Place.Id;
                bool active = shop == selected;
                var staff = roster[shop].ToArray();
                var buyers = staff.Where(row => row.Role == EmployeeRole.Buyer).ToArray();
                var methods = S.ShopProcurementSummary(shop);
                var receipt = S.LatestShopPurchaseReceipt(id);
                string status = ManagedShopStatus(shop, staff);
                string task = ManagedShopTask(shop, status, buyers, methods.Employee, methods.Short, tasks[id].Count());
                var button = Ui.Size(Ui.Button(list, "", () =>
                {
                    bool procurement = view.Procurement;
                    view.Select(active ? null : id);
                    view.Procurement = !active && procurement;
                    w.Rebuild();
                }, 17, Ui.SelectedButton(active)), -1, 68);
                button.name = "ManagedShop_" + id;
                var row = Ui.Row(button.transform, 68, 6).Fill(6);
                string sales = selected == null ? S.GetCityFinance(today, today, id).OperatingIn.ToString() : "";
                string cash = selected == null ? SignedReportCash(S.GetCityFinance(from, today, id).OperatingNet) : "";
                BuildManagedShopCells(row, selected != null, S.ShopTitle(shop) + "\n" + S.City.StreetName(shop.Place.StreetId),
                    status, BuyerText(buyers, methods, receipt), ArrivalText(receipt), sales, cash, task,
                    status == "营业中" ? Ui.Good : status == "已打烊" ? Ui.Dim : Ui.Warn, methods.Employee && buyers.Length == 0,
                    string.IsNullOrEmpty(task) ? Ui.Dim : Ui.Warn);
            }

            if (selected == null) return;
            var right = Ui.Size(Ui.FlatPanel(columns, "ManagedShopDetails", Ui.PanelBg2, Ui.FlatCardEdge), 0, -1, 1).rectTransform;
            Ui.VList(right, 6, 10);
            var selectedStaff = roster[selected].ToArray();
            var title = Ui.Row(right, 35);
            Ui.Size(Ui.Label(title, S.ShopTitle(selected), 23, color: Ui.Accent), 0, 34, 1).fontStyle = FontStyle.Bold;
            var selectedStatus = ManagedShopStatus(selected, selectedStaff);
            Ui.Size(Ui.Label(title, selectedStatus, 16, TextAnchor.MiddleRight, selectedStatus == "营业中" ? Ui.Good : Ui.Dim), 108, 33);
            string kind = S.IsOwnedVenue(selected.Place) ? S.VenueBusinessName(selected.Place) : RetailShopTypes.Name(selected.RetailKind);
            Line(right, S.City.StreetName(selected.Place.StreetId) + " · " + kind, 15, Ui.Dim);
            string shopId = selected.Place.Id;
            Line(right, $"今日收入 {S.GetCityFinance(today, today, shopId).OperatingIn} · 近 7 日收支差额 {SignedReportCash(S.GetCityFinance(from, today, shopId).OperatingNet)}", 16);

            var tabs = Ui.Row(right, 38);
            Ui.Tab(tabs, "概况", () => { view.Procurement = false; w.Rebuild(); }, !view.Procurement, 86);
            Ui.Tab(tabs, "采购", () => { view.Procurement = true; w.Rebuild(); }, view.Procurement, 86);
            var detail = PhoneFlexibleScroll(right, "ManagedShopDetailContent");
            if (view.Procurement)
            {
                S.VisitQuestTarget(QuestTarget.Restock);
                BuildShopProcurement(w, detail, selected, view);
            }
            else
                BuildManagedShopOverview(w, detail, selected, selectedStaff, selectedStatus, tasks[shopId].Count(), view, from);
        }

        private static void BuildManagedShopCells(Transform row, bool compact, string name, string status, string buyer,
            string arrival, string sales, string cash, string task, Color? statusColor = null, bool buyerMissing = false, Color? taskColor = null)
        {
            bool heading = !statusColor.HasValue;
            int size = heading ? 14 : 16;
            Color? label = heading ? Ui.Dim : (Color?)null;
            void Cell(string value, int width, Color? color = null, bool bold = false, bool number = false)
            {
                var text = Ui.Size(Ui.Label(row, value, size, number ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
                    color ?? label), width, -1, width == 0 ? 1 : 0);
                text.raycastTarget = false;
                if (bold) text.fontStyle = FontStyle.Bold;
            }

            Cell(name, compact ? 134 : 192);
            Cell(status, compact ? 84 : 106, statusColor);
            Cell(buyer, compact ? 112 : 150, buyerMissing ? Ui.Warn : label, !heading);
            Cell(arrival, compact ? 108 : 146);
            if (!compact)
            {
                Cell(sales, 100, number: true);
                Cell(cash, 140, heading ? label : cash.StartsWith("+", StringComparison.Ordinal) ? Ui.Good : cash.StartsWith("-", StringComparison.Ordinal) ? Ui.Warn : label, number: true);
            }
            Cell(string.IsNullOrEmpty(task) && !heading ? "—" : task, 0, taskColor);
        }

        private string ManagedShopStatus(OwnedShop shop, EmployeeAssignment[] staff)
        {
            if (S.IsPlaceOpen(shop.Place)) return "营业中";
            if (S.ShopFinishingService(shop)) return "打烊收尾";
            if (S.IsOwnedVenue(shop.Place))
            {
                var venue = S.VenueOperation(shop.Place.Id);
                if (!GameSession.CityWithinHours(S.Clock.MinuteOfDay, venue.Open, venue.Close)) return "已打烊";
                if (!venue.Accepting) return "暂停接客";
                return S.VenueHasAttendant(shop) ? "暂未接客" : "无人值守";
            }
            if (!shop.Sim.InHours(S.Clock.Now)) return "已打烊";
            if (shop.Sim.NotFitted) return "未布置完成";
            if (shop.Sim.ForcedClosed || !shop.Sim.TradingEnabled) return "暂停营业";
            bool attended = S.CityPlayerCanWork(shop) || (S.IsSpecialBusiness(shop)
                ? staff.Any(row => row.Role != EmployeeRole.Buyer && S.CityEmployeeAvailable(row.Person, shop))
                : shop.Sim.EmployeeOnDuty(S.Clock.Now));
            return attended ? "暂未接客" : "无人值守";
        }

        private string BuyerText(EmployeeAssignment[] buyers, (bool Employee, bool Online, int Short) methods, (long Minute, string Channel)? receipt)
        {
            if (buyers.Length == 0)
            {
                if (methods.Employee) return "未安排\n需进货员";
                if (methods.Online) return "网购自动\n无需进货员";
                return receipt?.Channel == "货车送达" ? "自行补货\n无需进货员" : "未设员工代采";
            }
            var buyer = buyers.FirstOrDefault(row => S.EmployeeTrip(row.Person.Id) != null)
                ?? buyers.FirstOrDefault(row => row.Person.PurchasePlan?.Daily == true) ?? buyers[0];
            string name = buyer.Person.Name + (buyers.Length > 1 ? $" 等 {buyers.Length} 人" : "");
            string status = S.EmployeeTrip(buyer.Person.Id) != null ? "外出进货"
                : buyer.Person.PurchasePlan?.Daily == true ? "已安排定期采购" : "未启用定期采购";
            return name + "\n" + status;
        }

        private string ArrivalText((long Minute, string Channel)? receipt)
        {
            if (!receipt.HasValue) return "暂无到货记录";
            var time = new GameTimestamp(receipt.Value.Minute);
            int days = S.Clock.Today.TotalDays - time.Date.TotalDays;
            return days == 0 ? "今天 " + Ui.Hm(time.MinuteOfDay)
                : days == 1 ? "昨天 " + Ui.Hm(time.MinuteOfDay)
                : days > 1 ? days + " 天前 " + Ui.Hm(time.MinuteOfDay) : time.ToString();
        }

        private string ManagedShopTask(OwnedShop shop, string status, EmployeeAssignment[] buyers, bool needsBuyer, int shortage, int tasks)
        {
            if (status == "无人值守") return "营业时段内无人值守";
            if (needsBuyer && buyers.Length == 0) return "安排进货员";
            int ready = S.ModelCommissions(shop).Count(order => order.Stage == ModelCommissionStage.Ready);
            if (ready > 0) return $"{ready} 单模型待交付";
            if (shortage > 0) return $"{shortage} 种补货商品缺货";
            if (tasks > 0) return $"{tasks} 项经营待办";
            return "";
        }

        private void OpenManagementShopEmployees(OwnedShop shop)
        {
            OpenEmployeesPanel();
            if (_managementExpandedShops.Add(shop.Place.Id)) _managementWindow.Rebuild();
        }

        private void BuildManagedShopOverview(Window w, RectTransform parent, OwnedShop shop, EmployeeAssignment[] staff,
            string status, int eventTasks, ShopManagementView view, GameDate from)
        {
            bool venue = S.IsOwnedVenue(shop.Place);
            var venueState = venue ? S.VenueOperation(shop.Place.Id) : null;
            var sim = shop.Sim;
            string Hours(int start, int close) => $"{Ui.Hm(start)}–{(close < start ? "次日 " : "")}{Ui.Hm(close)}";
            Line(parent, "营业与值守", 19, Ui.Accent);
            string hours = venue ? Hours(venueState.Open, venueState.Close)
                : sim.AllDay ? "全天营业" : Hours(sim.OpenMinute, sim.CloseMinute);
            Line(parent, hours, 17);
            var onDuty = staff.Where(row => S.CityEmployeeAvailable(row.Person, shop))
                .Select(row => row.Person.Name + " · " + EmployeeRoleLabel(row.Role, shop)).ToArray();
            Line(parent, onDuty.Length == 0 ? "当前没有员工在岗" : $"在岗 {onDuty.Length} 人 · " + string.Join("、", onDuty), 16, Ui.Dim);
            if (S.CityPlayerCanWork(shop)) Line(parent, "店主在店", 16, Ui.Dim);
            var storeActions = Ui.Row(parent, 39);
            Ui.Size(Ui.Button(storeActions, "员工与排班", () => OpenManagementShopEmployees(shop), 16), 0, 35, 1);
            if (venue)
                Ui.Size(Ui.Button(storeActions, "场馆管理", () => OpenVenueManagement(shop.Place), 16), 0, 35, 1);
            else
                Ui.Size(Ui.Button(storeActions, "营业时间", () => OpenManagedShopHours(w, shop), 16), 0, 35, 1);

            Line(parent, "待处理", 19, Ui.Accent);
            var procurement = S.ShopProcurementSummary(shop);
            var buyers = staff.Where(row => row.Role == EmployeeRole.Buyer).ToArray();
            string task = ManagedShopTask(shop, status, buyers, procurement.Employee, procurement.Short, eventTasks);
            Line(parent, string.IsNullOrEmpty(task) ? "暂无经营待办。" : task, 16, string.IsNullOrEmpty(task) ? Ui.Dim : Ui.Warn);
            if (procurement.Short > 0)
                Ui.Size(Ui.Button(parent, "查看缺货商品", () =>
                {
                    view.Procurement = true;
                    view.OnlyShort = true;
                    w.Rebuild();
                }, 16), -1, 34);
            if (eventTasks > 0)
                Ui.Size(Ui.Button(parent, "查看经营待办", OpenShopEventTasks, 16), -1, 34);
            if (shop == S.ShopAt(S.CurrentPlace) && shop.RetailKind == "model")
                Ui.Size(Ui.Button(parent, "查看模型委托", OpenModelCommissions, 16), -1, 34);

            Line(parent, "本店经营", 19, Ui.Accent);
            if (!venue)
                Line(parent, $"今日客流 {sim.TodayCustomers} 人 · 流失 {sim.TodayLost} 人", 16);
            var actions = Ui.Row(parent, 39);
            Ui.Size(Ui.Button(actions, "本店采购", () => { view.Procurement = true; w.Rebuild(); }, 16), 0, 35, 1);
            Ui.Size(Ui.Button(actions, "本店收支", () =>
            {
                var scope = S.AssetStoreReports(from, S.Clock.Today, false).Where(entry => entry.Id == shop.Place.Id).ToArray();
                if (scope.Length > 0) OpenAssetSources(scope, from, S.Clock.Today);
                else Toast("本店暂无收支记录。");
            }, 16), 0, 35, 1);
            if (!venue && S.CanUseHomeWorkbench)
                Ui.Size(Ui.Button(actions, "货架与定价", () =>
                    OpenWindow("货架与定价 · " + S.ShopTitle(shop), 1100, 800, detail => BuildWorkbenchShopDetail(detail.Content, detail, shop)), 16), 0, 35, 1);
            else if (!venue && shop == S.ShopAt(S.CurrentPlace))
                Ui.Size(Ui.Button(actions, shop.RetailKind == "restaurant" ? "管理食材" : "本店库存", () =>
                {
                    w.Close();
                    if (shop.RetailKind == "restaurant") OpenFridgeWindow();
                    else { OpenDock(); SelectTab(ShopTab.Stock); }
                }, 16), 0, 35, 1);
        }

        private void OpenManagedShopHours(Window parent, OwnedShop shop)
        {
            OpenWindow("营业时间 · " + S.ShopTitle(shop), 650, 240, w =>
                BuildShopHoursControls(w.Content, shop.Sim, shop, () => { w.Rebuild(); parent.Rebuild(); RebuildDock(); }));
        }
    }
}
