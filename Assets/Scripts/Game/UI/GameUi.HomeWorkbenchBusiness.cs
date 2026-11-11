using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>居家工作台：总览（今日待办）与经营（按街区分组、逐店展开）。</summary>
    public sealed partial class GameUi
    {
        private string _workbenchStreet;                       // null = 全部街区
        private readonly HashSet<string> _workbenchCollapsedStreets = new HashSet<string>();

        // ---------- 总览 ----------

        private void BuildWorkbenchPersonal(Transform parent, Window w)
        {
            var row = Ui.Row(parent, -1, 12); Ui.Size(row, -1, -1, 1).GetComponent<LayoutElement>().flexibleHeight = 1;
            var me = WbColumn(row, "WorkbenchPersonalState");
            WbHeading(me, "我的状态");
            WbMeter(me, "健康", S.Player.Health, 100); WbMeter(me, "精力", S.Player.Energy, S.Player.MaxEnergy, S.Player.Energy.ToString("0") + "/" + S.Player.MaxEnergy.ToString("0"));
            WbMeter(me, "饱腹", S.Player.Hunger, 100); WbMeter(me, "心情", S.Player.Mood, 100); WbMeter(me, "营养", S.Player.Nutrition, 100);
            WbHeading(me, "工作属性");
            WbStat(me, "智力", S.Player.Intelligence.ToString(), null, 15); WbStat(me, "魅力", S.Player.Charm.ToString(), null, 15);
            WbStat(me, "动手", S.Player.Skill.ToString(), null, 15); WbStat(me, "体能", S.Player.Stamina.ToString(), null, 15);
            WbHeading(me, "住处与载具");
            WbStat(me, S.HomeTitle, "周租 " + S.CurrentHomeRent, null, 15); WbStat(me, "载具", S.CurrentVehicle.Name, null, 15);
            WbStat(me, "过去24小时睡眠", S.SleepHoursLast24.ToString("0.#") + " / 8 小时", null, 15);
            var meActions = Ui.Row(me, 34, 6);
            Ui.Size(WbButton(meActions, "生活托管设置", OpenHomeAutomation, "WorkbenchPersonalAutomation"), -1, 34, 1);
            Ui.Size(WbButton(meActions, "背包与家中物品", OpenBackpackPanel, "WorkbenchBackpack"), -1, 34, 1);

            var todo = WbColumn(row, "WorkbenchPersonalDecisions", 1.8f);
            WbHeading(todo, "今日待办");
            BuildWorkbenchTodos(WbScroll(todo, "WorkbenchDecisionsList"), w);
            var links = Ui.Row(todo, 32, 8);
            WbAction(links, "日程与邀约", OpenCityActivities, "WorkbenchCalendar");
            WbAction(links, "手机消息", OpenChatPanel, "WorkbenchMessages"); WbFlex(links);

            var finance = WbColumn(row, "WorkbenchPersonalFinance", 1.1f);
            WbHeading(finance, "我的财务");
            Ui.Size(Ui.Label(finance, S.Ledger.Balance.ToString(), 30, TextAnchor.MiddleLeft, WbInk), -1, 42).fontStyle = FontStyle.Bold;
            Ui.Size(Ui.Label(finance, "净资产 " + S.NetWorth() + (S.TotalDebt.IsPositive ? " · 债务 " + S.TotalDebt : ""), 14, TextAnchor.MiddleLeft, WbDim), -1, 22);
            var periodRow = Ui.Row(finance, 28, 6); WbSegment(periodRow, new[] { "昨日", "近7日", "本月" }, _workbenchPeriod, n => { _workbenchPeriod = n; w.RequestRebuild(); }, "WorkbenchPeriod_"); WbFlex(periodRow); var range = WorkbenchDates(true); var flow = S.WorkbenchPersonalCash(range.from, range.to);
            if (_workbenchPeriod == 0 && S.Clock.Today.TotalDays == 0) flow = (Money.Zero, Money.Zero);
            WbStat(finance, "收入", "+" + flow.income, Ui.FlatUp); WbStat(finance, "支出", "-" + flow.expense, Ui.FlatDown);
            var floating = StockUnrealized;
            WbStat(finance, "证券浮动盈亏", floating.ToString(), floating.IsNegative ? Ui.FlatDown : Ui.FlatUp);
            Ui.Size(Ui.Label(finance, "日常收支 · 投资与借还单列", 13, TextAnchor.MiddleLeft, WbDim), -1, 20);
            WbLink(finance, "完整账本", OpenLedger, "WorkbenchLedger"); WbLink(finance, "物业与资产", OpenCityProperties, "WorkbenchProperties");
            WbLink(finance, "手机借款与还款", OpenLoansApp, "WorkbenchDebt");
        }

        private void WbTodo(Transform parent, string title, string detail, Color accent, string button, Action action, string name)
        {
            var card = Ui.Panel(parent, name, Ui.ButtonBg).rectTransform;
            var layout = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12; layout.padding = new RectOffset(0, 10, 6, 6);
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            Ui.Size(card, -1, 66);   // 固定高度：标题最多两行，再加一行说明
            Ui.Size(Ui.Panel(card, "TodoRail", accent), 5, -1, 0);
            var text = Ui.Rect(card, "TodoText"); Ui.VList(text, 1, 0); Ui.Size(text, -1, -1, 1);
            Ui.Label(text, title, 16);
            if (!string.IsNullOrEmpty(detail)) Ui.Label(text, detail, 13, TextAnchor.MiddleLeft, WbDim);
            if (button != null && action != null) Ui.Size(Ui.Button(card, button, action, 15, Ui.ButtonHot), 104, 34, 0).name = name + "_Action";
            else if (button != null) Ui.Size(Ui.Label(card, button, 14, TextAnchor.MiddleCenter, WbDim), 104, 34, 0);
        }
        private void GoWorkbenchStock(string id, bool shortMode)
        {
            _workbenchTab = 2; _stockPage = StockPage.Exchange; _stockShortMode = shortMode; _stockExpanded = id; _stockCandles = false; _stockWatchOnly = false;
            if (_homeWorkbench != null) { _homeWorkbench.ResetScrollOnRebuild = true; _homeWorkbench.RequestRebuild(); }
        }

        private void GoWorkbenchShop(string shopId, int businessTab)
        {
            _workbenchTab = 1; _workbenchBusinessTab = businessTab; _workbenchShop = shopId; _workbenchStreet = null;
            if (_homeWorkbench != null) { _homeWorkbench.ResetScrollOnRebuild = true; _homeWorkbench.RequestRebuild(); }
        }

        private void BuildWorkbenchTodos(RectTransform list, Window w)
        {
            int count = 0;
            int today = S.Clock.Today.TotalDays;
            if (S.PendingEvent != null)
            { var pending = S.PendingEvent; WbTodo(list, "待处理事件 · " + pending.Title, "需要你做出选择", Ui.Warn, "处理", () => ShowEvent(pending), "WorkbenchPendingEvent"); count++; }
            foreach (var loan in S.AppLoans.Where(l => !l.Repaid).OrderBy(l => l.DueDay))
            {
                int days = loan.DueDay - today;
                if (days > 7) continue;
                var payoff = S.LoanPayoff(loan);
                WbTodo(list, "借款 #" + loan.Id + (days <= 0 ? " 已到期" : " · " + days + " 天后到期") + " · " + payoff,
                    S.Ledger.CanAfford(payoff) ? "现金充足，可在手机借款里还款" : "现金不足，缺 " + (payoff - S.Ledger.Balance),
                    days <= 3 ? Ui.Warn : Ui.Accent, "去还款", OpenLoansApp, "WorkbenchLoan_" + loan.Id); count++;
            }
            foreach (string id in StockIds.Where(id => id != "hardware"))
            {
                var stock = S.StockState(id);
                if (stock == null || stock.ShortUnits <= 0) continue;
                double ratio = S.StockMarginRatio(id);
                if (ratio >= 1.5) continue;
                string target = id;
                WbTodo(list, "做空「" + StockTitle(id) + "」担保比例 " + ratio.ToString("P0") + "（130% 强平）", "补充保证金或买回平仓可回到安全线",
                    ratio < 1.4 ? Ui.Warn : Ui.Accent, "去看盘", () => GoWorkbenchStock(target, true), "WorkbenchShort_" + id); count++;
            }
            foreach (var alert in S.WorkbenchAlerts)
            { WbTodo(list, alert.Text, "生活托管提醒", Ui.Accent, "去设置", OpenHomeAutomation, "WorkbenchAlert_" + alert.Key); count++; }
            foreach (var shop in S.MyShops.Where(s => !S.IsOwnedVenue(s.Place) && WbStockCount(s) == 0))
            {
                string shopId = shop.Place.Id;
                WbTodo(list, S.ShopTitle(shop) + " · 库存已空", "进货或安排进货员补货", Ui.Accent, "去处理", () => GoWorkbenchShop(shopId, 2), "WorkbenchEmpty_" + shopId); count++;
            }
            var offered = S.CityActivities.Where(a => a.State == CityActivityPhase.Offered).ToArray();
            foreach (var activity in offered)
            {
                string id = activity.Id;
                WbTodo(list, "待回复邀约 · " + (CityLifeCatalog.Activities.FirstOrDefault(d => d.Id == activity.DefinitionId)?.Name ?? activity.DefinitionId), "尽快答复", Ui.Good,
                    "查看", () => OpenCityActivityDetail(id), "WorkbenchInvitation_" + id); count++;
            }
            foreach (var activity in S.CityActivities.Where(a => a.State == CityActivityPhase.Scheduled || a.State == CityActivityPhase.Gathering).OrderBy(a => a.ScheduledStart).Take(3))
            {
                string id = activity.Id;
                WbTodo(list, "日程 · " + new PCShopSell.Core.Time.GameTimestamp(activity.ScheduledStart), "已安排", Ui.Good, "查看", () => OpenCityActivityDetail(id), "WorkbenchUpcoming_" + id); count++;
            }
            if (count == 0) { var empty = Ui.Label(list, "今天没有待处理事项", 20, TextAnchor.MiddleCenter, WbDim); Ui.Size(empty, -1, 96); Ui.Size(Ui.Label(list, "出现需要你处理的事件、到期借款、缺货门店时会列在这里。", 14, TextAnchor.MiddleCenter, WbDim), -1, 24); }
        }

        // ---------- 经营 ----------

        private int WbStockCount(OwnedShop shop) =>
            shop.Sim.Stock.Lines().Sum(l => l.Count) + shop.Sim.Stock.GoodsLines().Sum(l => l.Value);

        private string WbStreetOf(OwnedShop shop) => S.City.StreetName(shop.Place.StreetId);

        private IEnumerable<OwnedShop> WbVisibleShops() =>
            S.MyShops.Where(s => _workbenchStreet == null || s.Place.StreetId == _workbenchStreet);

        private Money WbSalesToday(OwnedShop shop) => S.GetCityFinance(S.Clock.Today, S.Clock.Today, shop.Place.Id).Sales;

        private void BuildWorkbenchBusiness(Transform parent, Window w)
        {
            var nav = Ui.Row(parent, 32, 6); string[] names = { "报表", "门店", "人员采购", "订单合作", "资产" };
            for (int i = 0; i < names.Length; i++) { int index = i; WbSubTab(nav, names[i], () => { _workbenchBusinessTab = index; w.RequestRebuild(); }, "WorkbenchBusiness_" + i, _workbenchBusinessTab == i, 128); }
            WbFlex(nav);
            if (_workbenchBusinessTab == 0)
            {
                // 统计周期只是个筛选，做成右上角的小分段，不再占一整行。
                string[] periods = { "今日", "近7日", "本月" };
                for (int i = 0; i < periods.Length; i++) { int n = i; WbSubTab(nav, periods[i], () => { _workbenchPeriod = n; w.RequestRebuild(); }, "WorkbenchPeriod_" + i, _workbenchPeriod == i, 78, 32); }
            }
            switch (_workbenchBusinessTab)
            {
                case 0: BuildWorkbenchReport(parent, w); break;
                case 1: BuildWorkbenchShops(parent, w); break;
                case 2: BuildWorkbenchStaff(parent, w); break;
                case 3: BuildWorkbenchOrdersPage(parent, w); break;
                default: BuildWorkbenchAssets(parent); break;
            }
        }

        /// <summary>街区筛选：只有跨 2 条以上街区时才显示。</summary>
        private void BuildWorkbenchStreetFilter(Transform parent, Window w)
        {
            var streets = S.MyShops.Select(s => s.Place.StreetId).Distinct().OrderBy(id => S.City.StreetName(id)).ToArray();
            if (_workbenchStreet != null && !streets.Contains(_workbenchStreet)) _workbenchStreet = null;
            if (streets.Length < 2) return;
            var row = Ui.Row(parent, 28, 6);
            Ui.Size(Ui.Label(row, "街区", 14, TextAnchor.MiddleLeft, WbDim), 40, 28, 0);
            void Chip(string label, string id)
            {
                Ui.Size(Ui.Button(row, label, () => { _workbenchStreet = id; w.RequestRebuild(); }, 14, _workbenchStreet == id ? Ui.ButtonHot : Ui.ButtonBg), 118, 28, 0)
                    .name = "WorkbenchStreet_" + (id ?? "all");
            }
            Chip("全部", null);
            foreach (string id in streets) Chip(S.City.StreetName(id), id);
        }

        /// <summary>按街区分组，每组下一家家店；点店展开明细（同一时间展开一家）。</summary>
        private void BuildWorkbenchShopGroups(Transform parent, Window w, Func<OwnedShop, (string head, string value)[]> cells, Func<IEnumerable<OwnedShop>, string> groupInfo, Action<Transform, OwnedShop> detail)
        {
            var shops = WbVisibleShops().ToArray();
            if (shops.Length == 0) { WbText(parent, "还没有经营中的门店。", 20, WbDim); return; }
            bool Empty(OwnedShop s) => !S.IsOwnedVenue(s.Place) && WbStockCount(s) == 0;
            var groups = shops.GroupBy(s => s.Place.StreetId ?? "")
                .Select(g => (key: g.Key, shops: g.OrderByDescending(Empty).ToArray()))
                .OrderByDescending(g => g.shops.Count(Empty)).ThenBy(g => S.City.StreetName(g.key)).ToArray();
            if (_workbenchShop == null || (_workbenchShop != "" && shops.All(s => s.Place.Id != _workbenchShop)))
                _workbenchShop = groups.SelectMany(g => g.shops).First().Place.Id;      // 默认展开第一家；"" 表示用户全部收起
            var list = WbScroll(parent, "WorkbenchShopList");
            foreach (var group in groups)
            {
                bool collapsed = _workbenchCollapsedStreets.Contains(group.key);
                string key = group.key; int emptyCount = group.shops.Count(Empty);
                WbGroupBar(list, "WorkbenchStreetGroup_" + group.key, (collapsed ? "▸  " : "▾  ") + S.City.StreetName(group.key), group.shops.Length + " 家店 · " + groupInfo(group.shops),
                    () => { if (!_workbenchCollapsedStreets.Remove(key)) _workbenchCollapsedStreets.Add(key); w.RequestRebuild(); },
                    'r', emptyCount > 0 ? emptyCount + " 家缺货" : null);
                if (collapsed) continue;
                foreach (var shop in group.shops)
                {
                    var target = shop; bool open = _workbenchShop == shop.Place.Id;
                    var row = WbAccordionRow(list, "WorkbenchShop_" + shop.Place.Id, open, () => { _workbenchShop = open ? "" : target.Place.Id; w.RequestRebuild(); });
                    var name = Ui.Label(row, (open ? "▾  " : "▸  ") + S.ShopTitle(shop), 18, TextAnchor.MiddleLeft); name.fontStyle = FontStyle.Bold; name.horizontalOverflow = HorizontalWrapMode.Overflow;
                    Ui.Size(name, 250, -1, 0);
                    if (S.IsOwnedVenue(shop.Place)) WbChip(row, shop.Sim.IsOpen ? "营业中" : "休息", shop.Sim.IsOpen ? 'g' : 'n');
                    else { WbChip(row, shop.Sim.IsOpen ? "营业中" : "休息", shop.Sim.IsOpen ? 'g' : 'n'); if (Empty(shop)) WbChip(row, "缺货", 'r'); }
                    WbFlex(row);
                    foreach (var cell in cells(shop)) WbCell(row, cell.head, cell.value, 120);
                    if (!open) continue;
                    var body = Ui.FlatPanel(list, "WorkbenchShopBody", Ui.FlatCard, Ui.ButtonHot).rectTransform; Ui.VList(body, 8, 12);
                    detail(body, shop);
                }
            }
        }

        private (string head, string value)[] WbShopCells(OwnedShop shop)
        {
            if (S.IsOwnedVenue(shop.Place)) return new[] { ("今日收入", WbSalesToday(shop).ToString()) };
            int stock = WbStockCount(shop);
            return new[]
            {
                ("今日销售", WbSalesToday(shop).ToString()),
                ("客流 / 流失", shop.Sim.TodayCustomers + " / " + shop.Sim.TodayLost),
                ("库存", stock == 0 ? "已空" : stock + " 件"),
                ("员工", S.Employees.Count(e => e.Shop == shop) + " 人"),
            };
        }

        private void BuildWorkbenchShops(Transform parent, Window w)
        {
            if (S.MyShops.Count == 0) { WbText(parent, "还没有经营中的门店。", 20); return; }
            BuildWorkbenchStreetFilter(parent, w);
            BuildWorkbenchShopGroups(parent, w, WbShopCells,
                g => "今日销售 " + Money.FromCents(g.Sum(s => WbSalesToday(s).Cents)) + " · 流失 " + g.Where(s => !S.IsOwnedVenue(s.Place)).Sum(s => s.Sim.TodayLost) + " 人",
                (body, shop) => BuildWorkbenchShopDetail(body, w, shop));
        }

        private void BuildWorkbenchShopDetail(Transform parent, Window w, OwnedShop shop)
        {
            if (S.IsOwnedVenue(shop.Place)) { WbButton(parent, "服务价格、营业与值守", () => OpenVenueManagement(shop.Place), "WorkbenchVenue"); return; }
            var sim = shop.Sim;
            var columns = Ui.Row(parent, -1, 14);
            var left = Ui.Rect(columns, "ShopSettings"); Ui.VList(left, 8, 0); Ui.Size(left, -1, -1, 1);
            var right = Ui.Rect(columns, "ShopGoods"); Ui.VList(right, 4, 0); Ui.Size(right, -1, -1, 1.5f);

            WbHeading(left, "经营设置");
            var hours = Ui.Row(left, 34, 6);
            Ui.Size(Ui.Label(hours, "营业时间", 15, TextAnchor.MiddleLeft, WbDim), 70, -1, 0);
            var options = Enumerable.Range(0, 24).Select(i => i.ToString("00") + ":00").ToArray();
            const int defaultOpenHour = PCShopSell.Core.Shop.ShopSimulation.DefaultOpenMinute / 60, defaultCloseHour = PCShopSell.Core.Shop.ShopSimulation.DefaultCloseMinute / 60;
            int openHour = sim.AllDay ? defaultOpenHour : sim.OpenMinute / 60;
            int closeHour = sim.AllDay ? defaultCloseHour : sim.CloseMinute / 60 % 24;
            var open = Ui.Size(Ui.Choice(hours, options, openHour, i => { S.SetWorkbenchHours(shop.Place.Id, i, closeHour, false); w.RequestRebuild(); }), 96, 34, 0); open.name = "WorkbenchOpenHour";
            var close = Ui.Size(Ui.Choice(hours, options, closeHour, i => { S.SetWorkbenchHours(shop.Place.Id, openHour, i, false); w.RequestRebuild(); }), 96, 34, 0); close.name = "WorkbenchCloseHour";
            Ui.Size(WbButton(hours, sim.AllDay ? "不限时 ✓" : "不限时", () => { S.SetWorkbenchHours(shop.Place.Id, defaultOpenHour, defaultCloseHour, !sim.AllDay); w.RequestRebuild(); }, "WorkbenchAllDay"), -1, 34, 1);
            Ui.Size(Ui.Label(left, "统一加价 " + sim.Markup.ToString("0.00") + "×", 15, TextAnchor.MiddleLeft, WbDim), -1, 22);
            PriceMultiplierEditor(left, "WorkbenchMarkup_" + shop.Place.Id, "统一加价", sim.Markup, PCShopSell.Core.Shop.ShopSettings.MinMarkup, PCShopSell.Core.Shop.ShopSettings.MaxMarkup, .05, value => S.SetWorkbenchMarkup(shop.Place.Id, value), w.RequestRebuild);
            var actions = Ui.Row(left, 32, 6);
            Ui.Size(WbButton(actions, "员工与排班", OpenEmployeesPanel, "WorkbenchShopStaff"), -1, 32, 1);
            Ui.Size(WbButton(actions, "进货员与记录", () => OpenShopBuyers(shop), "WorkbenchShopBuyers"), -1, 32, 1); Ui.Size(WbButton(actions, "本店采购", () => OpenCityProcurementFor(shop), "WorkbenchShopProcurement"), -1, 32, 1);
            
            Ui.Size(Ui.Label(left, "搬运、摆放家具和手工装机需本人到店。", 13, TextAnchor.MiddleLeft, WbDim), -1, 20);

            WbHeading(right, "已陈列商品 · 定价");
            var widths = new[] { 96f, 80f, 80f, 70f };
            Ui.Size(Ui.Rect(WbTable(right, new[] { "商品", "陈列 / 库存", "售价", "购买意愿" }, widths, true), "Gap"), 54, -1, 0);
            var shelfStock = sim.Stock.GoodsLines().ToDictionary(l => l.Key, l => l.Value);
            foreach (var kind in ShelfKinds.All)
            {
                var fill = sim.ShelfFillOf(kind);
                foreach (var line in fill.Goods)
                {
                    string id = line.Key; var price = sim.GoodsPriceOf(id);
                    int intent = (int)Math.Round(sim.GoodsPurchaseIntentAtPrice(id, price.Yuan) * 100);
                    var row = WbTable(right, new[] { Gifts.Get(id).Name, line.Value + " / " + (shelfStock.TryGetValue(id, out int back) ? back : line.Value), price.ToString(), intent + "%" }, widths);
                    Ui.Size(Ui.Button(row, "调价", () => OpenWorkbenchGoodsPrice(shop, id), 14, Ui.ButtonHot), 54, 28, 0).name = "WorkbenchGoods_" + id;
                }
                foreach (var line in fill.Parts)
                {
                    var part = line.Part; bool boxed = line.Boxed; var price = sim.PriceOf(part, boxed);
                    int intent = (int)Math.Round(sim.PartPurchaseIntentAtPrice(part, boxed, price.Yuan) * 100);
                    var row = WbTable(right, new[] { part.Name + (boxed ? "·盒装" : "·散件"), line.Count + " / " + sim.Stock.CountAll(part), price.ToString(), intent + "%" }, widths);
                    Ui.Size(Ui.Button(row, "调价", () => OpenWorkbenchPartPrice(shop, part, boxed), 14, Ui.ButtonHot), 54, 28, 0).name = "WorkbenchPart_" + part.Id + (boxed ? "_b" : "_l");
                }
            }
            var back2 = sim.Stock.GoodsLines().Select(l => Gifts.Get(l.Key).Name + " " + l.Value).Concat(sim.Stock.Lines().Select(l => l.Part.Name + (l.Boxed ? "·盒" : "·散") + " " + l.Count)).ToArray();
            Ui.Size(Ui.Label(right, "后仓库存（查看）：" + (back2.Length == 0 ? "空" : string.Join(" / ", back2)), 13, TextAnchor.UpperLeft, WbDim), -1, 20 + 18 * (back2.Length / 6));
        }
        private void BuildWorkbenchStaff(Transform parent, Window w)
        {
            if (S.MyShops.Count == 0) { WbText(parent, "还没有经营中的门店。", 20); return; }
            var top = Ui.Row(parent, 32, 8);
            WbAction(top, "统一采购与补货责任", OpenCityProcurement, "WorkbenchProcurement", 190);
            WbAction(top, "全部员工 · 岗位 / 排班", OpenEmployeesPanel, "WorkbenchEmployees", 250); WbFlex(top);
            BuildWorkbenchStreetFilter(parent, w);
            BuildWorkbenchShopGroups(parent, w,
                shop => new[] { ("员工", S.Employees.Count(e => e.Shop == shop) + " 人"), ("进货员", S.Employees.Any(e => e.Shop == shop && e.Role == EmployeeRole.Buyer) ? "已安排" : "未指定") },
                g => "员工 " + g.Sum(s => S.Employees.Count(e => e.Shop == s)) + " 人",
                (body, shop) =>
                {
                    var staff = S.Employees.Where(e => e.Shop == shop).ToArray();
                    WbHeading(body, "员工 · 岗位 / 排班");
                    if (staff.Length == 0) WbText(body, "这家店还没有员工。", 17, WbDim);
                    foreach (var employee in staff)
                        WbText(body, employee.Person.Name + " · " + GameSession.RoleName(employee.Role) + " · " + S.EmployeeStatus(employee), 17);
                    var actions = Ui.Row(body, 32, 8);
                    WbAction(actions, "员工与排班", OpenEmployeesPanel, "WorkbenchShopStaff");
                    WbAction(actions, "进货员与采购记录", () => OpenShopBuyers(shop), "WorkbenchBuyers_" + shop.Place.Id, 180);
                    WbAction(actions, "本店采购", () => OpenCityProcurementFor(shop), "WorkbenchShopProcurement"); WbFlex(actions);
                });
        }

        private void BuildWorkbenchOrdersPage(Transform parent, Window w)
        {
            var row = Ui.Row(parent, -1, 12); Ui.Size(row, -1, -1, 1).GetComponent<LayoutElement>().flexibleHeight = 1;
            var left = Ui.Rect(row, "WorkbenchOrderShops"); Ui.Size(left, -1, -1, 1.6f); Ui.VList(left, 8, 0);
            BuildWorkbenchStreetFilter(left, w);
            if (S.MyShops.Count == 0) WbText(left, "还没有经营中的门店。", 20);
            else BuildWorkbenchShopGroups(left, w,
                shop => S.IsOwnedVenue(shop.Place) ? new[] { ("装机订单", "不接") } : new[] { ("装机订单", shop.Sim.Orders.Count + " 单"), ("自动装机", shop.AutoBuild ? "开" : "关") },
                g => "订单 " + g.Sum(s => s.Sim.Orders.Count) + " 单",
                (body, shop) => BuildWorkbenchOrders(body, w, shop));
            var right = WbColumn(row, "WorkbenchCooperation", .9f);
            WbHeading(right, GameSession.FirstChapterTrial ? "直供采购" : "品牌与合作");
            var list = WbScroll(right, "WorkbenchCooperationList");
            if (S.BrandManagementUnlocked)
                WbLink(list, "品牌 · 代言人 / 展会 / 竞标", OpenBrandOverview, "WorkbenchBrandBusiness");
            if (BusinessUnlocked)
            {
                WbLink(list, "直供采购", () => { _businessTab = 1; ShowBusinessPanel(false); }, "WorkbenchDirectSupply");
                if (!GameSession.FirstChapterTrial)
                {
                    WbLink(list, "企业合同", () => { _businessTab = 2; ShowBusinessPanel(false); }, "WorkbenchContracts");
                    WbLink(list, "MCN工作室", () => { _businessTab = 4; ShowBusinessPanel(false); }, "WorkbenchPromotion");
                }
            }
            else WbText(list, GameSession.FirstChapterTrial ? "经营成长后解锁直供采购。" : "经营成长后解锁直供、企业合同和MCN工作室。", 17, WbDim);
        }

        private void BuildWorkbenchOrders(Transform body, Window w, OwnedShop shop)
        {
            if (S.IsOwnedVenue(shop.Place)) { WbText(body, "这类场所没有装机订单。", 17, WbDim); return; }
            WbHeading(body, "自动装机：" + (shop.AutoBuild ? "开" : "关") + " · 订单 " + shop.Sim.Orders.Count + " 单");
            var autoRow = Ui.Row(body, 32, 8); WbAction(autoRow, "切换员工自动装机", () => { S.ToggleAutoBuildAt(shop); w.RequestRebuild(); }, "WorkbenchAutoBuild_" + shop.Place.Id, 170); WbFlex(autoRow);
            foreach (var order in shop.Sim.Orders)
            {
                WbText(body, PartCatalog.TierName(order.Tier) + "整机 · " + order.Price + " · 截止 " + order.DueAt, 18);
                WbText(body, order.PartsReserved ? "配件已领用 · 员工剩余 " + order.EmployeeMinutesLeft + " 分钟" : "等待配件与员工处理", 15, WbDim);
                foreach (var part in PartCatalog.BuildList(order.Tier))
                    WbText(body, part.Name + " · " + (order.PartsReserved || shop.Sim.Stock.CountAll(part) > 0 ? "已备" : "缺货"), 14, WbDim);
            }
            if (shop.Sim.Orders.Count == 0) WbText(body, "当前没有装机订单。", 17, WbDim);
            WbText(body, "员工按真实岗位、在岗位置和配件处理订单。手工装机需本人到店。", 15, WbDim);
        }

        private void BuildWorkbenchAssets(Transform parent)
        {
            var strip = Ui.Row(parent, 66, 10); strip.name = "WorkbenchAssetSummary";
            WbStripCard(strip, "净资产", S.NetWorth().ToString()); WbStripCard(strip, "物业估值", S.PropertyValue.ToString());
            WbStripCard(strip, "现金", S.Ledger.Balance.ToString());
            WbStripCard(strip, "债务", S.TotalDebt.ToString(), S.TotalDebt.IsPositive ? Ui.FlatDown : (Color?)null);
            var row = Ui.Row(parent, -1, 12); Ui.Size(row, -1, -1, 1).GetComponent<LayoutElement>().flexibleHeight = 1;

            var left = WbColumn(row, "WorkbenchAssetList", 2.2f); WbHeading(left, "门店与住处");
            var list = WbScroll(left, "WorkbenchBusinessFunctions");
            var widths = new[] { 100f, 120f, 120f };
            foreach (var group in S.MyShops.GroupBy(s => s.Place.StreetId ?? "").OrderBy(g => S.City.StreetName(g.Key)))
            {
                WbGroupBar(list, "WorkbenchAssetStreet_" + group.Key, S.City.StreetName(group.Key), group.Count() + " 处", null);
                WbTable(list, new[] { "物业", "产权", "周租金", "押金" }, widths, true);
                foreach (var shop in group)
                {
                    bool own = shop.OwnsProperty;
                    WbTable(list, new[] { S.ShopTitle(shop), own ? "自有" : "租赁", own || shop.Lease == null ? "—" : shop.Lease.Rent.ToString(), own || shop.Lease == null ? "—" : shop.Lease.Deposit.ToString() }, widths, false,
                        new[] { default(Color), own ? Ui.FlatUp : default(Color), default(Color), default(Color) }, 32);
                }
            }
            if (!S.MyShops.Any()) Ui.Size(Ui.Label(list, "还没有门店或自有物业。", 17, TextAnchor.MiddleLeft, WbDim), -1, 30);
            WbGroupBar(list, "WorkbenchAssetHome", "住处与载具", S.HomeTitle, null);
            WbStat(list, "住处", S.HomeTitle + (S.CurrentHomeRent.IsPositive ? " · 周租 " + S.CurrentHomeRent : " · 自有"), null, 15);
            WbStat(list, "当前载具", S.CurrentVehicle.Name, null, 15);

            var links = WbColumn(row, "WorkbenchAssetLinks"); WbHeading(links, "详情入口");
            WbLink(links, "具体物业、租约与收租", OpenCityProperties, "WorkbenchAssets");
            WbLink(links, "门店与自有物业 · 融资总览", OpenCityFinanceOverview, "WorkbenchInvestmentDebt");
            WbLink(links, "车辆与占用", OpenFleetPanel, "WorkbenchVehicles");
            if (S.RacingUnlocked) WbLink(links, "湖区赛车", OpenRacingDestinations, "WorkbenchRacing");
            if (BusinessUnlocked) WbLink(links, "住处租约", OpenCityResidence, "WorkbenchPropertyAccounts");
            if (S.HasPark) WbLink(links, "园区管理", () => { _businessTab = 6; ShowBusinessPanel(false); }, "WorkbenchPark");
        }

        private static void WbStripCard(Transform parent, string head, string value, Color? color = null)
        {
            var card = Ui.FlatPanel(parent, "Strip_" + head).rectTransform; Ui.VList(card, 0, 8); Ui.Size(card, -1, 62, 1);
            Ui.Size(Ui.Label(card, head, 14, TextAnchor.MiddleLeft, WbDim), -1, 18);
            var text = Ui.Label(card, value, 25, TextAnchor.MiddleLeft, color ?? WbInk); text.fontStyle = FontStyle.Bold; Ui.Size(text, -1, 32);
        }

        private string _workbenchReportShop = "";

        /// <summary>单店收支明细：按项目汇总收入和支出，再列出区间内的每一笔经营流水（最近 40 笔）。</summary>
        private void BuildWorkbenchShopLedger(Transform parent, OwnedShop shop, PCShopSell.Core.Time.GameDate from, PCShopSell.Core.Time.GameDate to)
        {
            var body = Ui.FlatPanel(parent, "WorkbenchShopLedger", Ui.FlatCard, Ui.ButtonHot).rectTransform; Ui.VList(body, 4, 12);
            var entries = S.Ledger.Entries.Where(e => e.ShopId == shop.Place.Id && e.BusinessKind == PCShopSell.Core.Finance.LedgerBusinessKind.Operating && e.Date >= from && e.Date <= to).ToArray();
            string Signed(Money m) => (m.IsNegative ? "-" : "+") + m.Abs();
            WbHeading(body, S.ShopTitle(shop) + " · 收支明细", 17);
            if (entries.Length == 0) { WbText(body, "这段时间没有经营收支。", 16, WbDim); return; }
            var byCategory = entries.GroupBy(e => e.Category ?? "其他")
                .Select(g => (name: g.Key, income: Money.FromCents(g.Where(e => e.Amount.IsPositive).Sum(e => e.Amount.Cents)), expense: Money.FromCents(g.Where(e => e.Amount.IsNegative).Sum(e => -e.Amount.Cents))))
                .OrderByDescending(g => g.income.Cents + g.expense.Cents).ToArray();
            WbTable(body, new[] { "项目", "收入", "支出" }, new[] { 120f, 120f }, true);
            foreach (var c in byCategory)
                WbTable(body, new[] { c.name, c.income.IsPositive ? "+" + c.income : "—", c.expense.IsPositive ? "-" + c.expense : "—" }, new[] { 120f, 120f }, false,
                    new[] { default(Color), c.income.IsPositive ? Ui.FlatUp : default(Color), c.expense.IsPositive ? Ui.FlatDown : default(Color) }, 28);
            WbHeading(body, "流水（最近 " + Math.Min(40, entries.Length) + " 笔 / 共 " + entries.Length + " 笔）", 15);
            WbTable(body, new[] { "备注", "日期", "金额" }, new[] { 70f, 120f }, true);
            foreach (var e in Enumerable.Reverse(entries).Take(40))
                WbTable(body, new[] { (e.Category ?? "") + (string.IsNullOrEmpty(e.Memo) ? "" : " · " + e.Memo), e.Date.Month + "/" + e.Date.Day, Signed(e.Amount) }, new[] { 70f, 120f }, false,
                    new[] { default(Color), default(Color), e.Amount.IsNegative ? Ui.FlatDown : Ui.FlatUp }, 28);
        }

        private void BuildWorkbenchReport(Transform parent, Window w)
        {
            var range = WorkbenchDates(); var summary = S.GetCityFinance(range.from, range.to);
            var strip = Ui.Row(parent, 66, 10); strip.name = "WorkbenchOperatingSummary";
            WbStripCard(strip, "销售", summary.Sales.ToString()); WbStripCard(strip, "经营支出", summary.OperatingOut.ToString());
            WbStripCard(strip, "现金净额", summary.OperatingNet.ToString(), summary.OperatingNet.IsNegative ? Ui.FlatDown : Ui.FlatUp);
            BuildWorkbenchStreetFilter(parent, w);
            var row = Ui.Row(parent, -1, 12); Ui.Size(row, -1, -1, 1).GetComponent<LayoutElement>().flexibleHeight = 1;
            var report = WbColumn(row, "WorkbenchShopReports", 2.4f); WbHeading(report, "门店收支");
            var list = WbScroll(report, "WorkbenchShopReportList");
            var widths = new[] { 110f, 110f, 110f };
            foreach (var group in WbVisibleShops().GroupBy(s => s.Place.StreetId ?? "").OrderBy(g => S.City.StreetName(g.Key)))
            {
                var totals = group.Select(s => S.GetCityFinance(range.from, range.to, s.Place.Id)).ToArray();
                WbGroupBar(list, "WorkbenchReportStreet_" + group.Key, S.City.StreetName(group.Key), group.Count() + " 家 · 销售 " + Money.FromCents(totals.Sum(t => t.Sales.Cents)) + " / 净额 " + Money.FromCents(totals.Sum(t => t.OperatingNet.Cents)), null);
                WbTable(list, new[] { "门店", "销售", "支出", "净额" }, widths, true);
                foreach (var shop in group)
                {
                    var result = S.GetCityFinance(range.from, range.to, shop.Place.Id);
                    string shopId = shop.Place.Id; bool open = _workbenchReportShop == shopId;
                    var line = WbTable(list, new[] { (open ? "▾  " : "▸  ") + S.ShopTitle(shop) + (shop.Sim.IsOpen ? "" : " · 休息"), result.Sales.ToString(), result.OperatingOut.ToString(), result.OperatingNet.ToString() }, widths, false,
                        new[] { default(Color), default(Color), default(Color), result.OperatingNet.IsNegative ? Ui.FlatDown : Ui.FlatUp }, 32);
                    line.name = "WorkbenchReportShop_" + shopId;
                    var lineImage = line.gameObject.AddComponent<Image>(); lineImage.color = open ? WbRowOpen : new Color(0, 0, 0, 0);
                    var click = line.gameObject.AddComponent<Button>(); click.targetGraphic = lineImage;
                    click.onClick.AddListener(() => { _workbenchReportShop = open ? "" : shopId; w.RequestRebuild(); });
                    if (open) BuildWorkbenchShopLedger(list, shop, range.from, range.to);
                }
            }
            Ui.Size(Ui.Label(report, "现金净额反映经营收付，不等于库存销售利润。", 13, TextAnchor.MiddleLeft, WbDim), -1, 20);
            var market = WbColumn(row, "WorkbenchCategoryPrices"); WbHeading(market, "品类进货行情");
            var prices = WbScroll(market, "WorkbenchCategoryPriceList");
            var kinds = S.MyShops.Select(s => s.RetailKind).Concat(new[] { "clothing", "models", "computer" }).Distinct().Where(k => S.WorkbenchPrices(k).Length > 0).ToArray();
            foreach (string kind in kinds)
            {
                string id = kind; var history = S.WorkbenchPrices(id); double? change = S.WorkbenchPriceChange(id);
                int span = history.Length > 1 ? Math.Min(7, history.Last().Day - history.First().Day) : 0;
                // 一个品类一行：名称 / 迷你折线 / 近 N 日涨跌 / 状态；整行可点，进价格历史。
                var head = Ui.Row(prices, 40, 10); head.name = "WorkbenchPrice_" + id;
                var rowImage = head.gameObject.AddComponent<Image>(); rowImage.color = new Color(0, 0, 0, 0);
                var rowClick = head.gameObject.AddComponent<Button>(); rowClick.targetGraphic = rowImage; rowClick.onClick.AddListener(() => OpenWorkbenchPriceHistory(id));
                Ui.Size(Ui.Label(head, RetailShopTypes.Name(id), 16, TextAnchor.MiddleLeft), 0, -1, 1);
                Ui.Size(WbPriceGraph(head, history, 26), 84, 26, 0);
                string changeText = change.HasValue ? change.Value.ToString("+0.0;-0.0;0.0") + "%" : "—";
                var changeLabel = Ui.Label(head, changeText, 15, TextAnchor.MiddleRight, change.HasValue && Math.Abs(change.Value) > 0.05 ? WbInk : WbDim); changeLabel.raycastTarget = false;
                Ui.Size(changeLabel, 64, -1, 0);
                WbChip(head, S.MyShops.Any(s => s.RetailKind == id) ? "经营中" : "关注", S.MyShops.Any(s => s.RetailKind == id) ? 'g' : 'n');
                WbDivider(head);
            }
        }    }
}
