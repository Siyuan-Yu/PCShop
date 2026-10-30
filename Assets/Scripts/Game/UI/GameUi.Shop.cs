using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Session;
using PCShopSell.Core.Shop;
using PCShopSell.Core.Time;
using PCShopSell.Core.Trade;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private enum ShopTab
        {
            Counter,
            Stock,
            Pricing,
            Orders,
            Renovate,
            Upgrades,
            Staff,
        }

        private static readonly (ShopTab tab, string name)[] Tabs =
        {
            (ShopTab.Counter, "柜台"), (ShopTab.Stock, "库存"), (ShopTab.Pricing, "定价"),
            (ShopTab.Orders, "订单"), (ShopTab.Renovate, "装修"), (ShopTab.Upgrades, "升级"), (ShopTab.Staff, "招人"),
        };

        private RectTransform _dock;
        private RectTransform _dockContent;
        private readonly List<(Button b, ShopTab tab)> _tabButtons = new List<(Button, ShopTab)>();
        private ShopTab _tab = ShopTab.Counter;
        private bool _dockHolding;

        public bool DockOpen => _dock != null;

        public void OpenDock()
        {
            if (_dock != null) return;
            Image panel = Ui.Panel(_dockCanvas.transform, "ShopDock", Ui.PanelBg);
            _dock = panel.rectTransform.Anchor(1, 0, 1, 1, -600, 70, 10, 74);

            RectTransform tabs = Ui.Rect(_dock, "Tabs").Anchor(0, 1, 1, 1, 8, -54, 8, 8);
            var h = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            _tabButtons.Clear();
            foreach (var (tab, name) in Tabs)
            {
                ShopTab t = tab;
                _tabButtons.Add((Ui.Button(tabs, name, () => SelectTab(t), 18), t));
            }

            _dockContent = Ui.Scroll(_dock);
            ((RectTransform)_dockContent.parent.parent).Anchor(0, 0, 1, 1, 0, 60, 0, 58);

            bool staffed = S.IsEmployeeOnDuty(S.Clock.Now);
            Button leave = Ui.Button(_dock, staffed ? "离开店铺（店员看店）" : "离开店铺（没人看店就关门）", ConfirmLeaveShop, 19, Ui.Warn);
            ((RectTransform)leave.transform).Anchor(0, 0, 1, 0, 12, 8, 12, -52);

            _tab = ShopTab.Counter;
            SelectTab(ShopTab.Counter);
        }

        private Window _leaveConfirm;

        /// <summary>离开店铺前的二次确认：店里没人就关门，店员在班就照常营业。</summary>
        public void ConfirmLeaveShop()
        {
            if (_leaveConfirm != null) return;
            if (S.LeaveBlocker != null)
            {
                Toast(S.LeaveBlocker);
                return;
            }
            bool staffed = S.IsEmployeeOnDuty(S.Clock.Now);
            int inShop = S.PlayerShop.Queue.Count;
            _leaveConfirm = OpenWindow("要离开店铺吗？", 620, 320, w =>
            {
                if (staffed)
                    Para(w.Content, $"店员{S.OnDutyStaffName}在班，你走了店<color=#7FD18B>照常营业</color>，顾客由他来接待、结账。", 19);
                else
                {
                    Para(w.Content, "店里现在只有你一个人，你一走店就<color=#D79780>关门</color>。", 19);
                    Para(w.Content, inShop > 0 ? $"柜台前还有{inShop}个顾客在排队，他们会白等一场走掉；关门期间也不会有人进店。" : "关门期间不会有顾客进店。", 17, Ui.Dim);
                    if (S.Employee != null) Para(w.Content, $"（店员{S.Employee.Name}现在不在班。）", 16, Ui.Dim);
                }
                RectTransform row = Ui.Row(w.Content, 50, 12);
                Ui.Size(Ui.Button(row, "再待会儿", w.Close, 19), -1, -1, 1);
                Ui.Size(Ui.Button(row, staffed ? "离开" : "关门离开", () =>
                {
                    w.Close();
                    _ctrl.LeaveShop();
                }, 19, Ui.Warn), -1, -1, 1);
            }, onClosed: () => _leaveConfirm = null);
        }

        public void CloseDock()
        {
            if (_dock == null) return;
            if (_dockHolding && S.Time.IsHeld) S.Time.Release();
            _dockHolding = false;
            Kill.It(_dock.gameObject);
            _dock = null;
        }

        /// <summary>自动化冒烟用：切换商店面板的标签页（按枚举序号）。</summary>
        public int DebugTabCount => System.Enum.GetValues(typeof(ShopTab)).Length;

        public void DebugSelectTab(int index)
        {
            if (_dock != null) SelectTab((ShopTab)index);
        }

        private void SelectTab(ShopTab tab)
        {
            _tab = tab;
            bool hold = tab != ShopTab.Counter;
            if (hold && !_dockHolding) S.Time.Hold();
            if (!hold && _dockHolding && S.Time.IsHeld) S.Time.Release();
            _dockHolding = hold;
            foreach (var (b, t) in _tabButtons) b.image.color = t == tab ? Ui.ButtonHot : Ui.ButtonBg;
            RebuildDock();
        }

        private void RefreshDock()
        {
            if (_dock == null) return;
            if (!S.InPlayerShop)
            {
                CloseDock();
                return;
            }
            if (_tab == ShopTab.Counter) RebuildDock();
        }

        private ShopTab? _scrollTab;

        public void RebuildDock()
        {
            if (_dock == null) return;
            var scrolled = _scrollTab == _tab ? Ui.SaveScroll(_dock) : null;
            _scrollTab = _tab;
            Ui.Clear(_dockContent);
            var guide = S.CurrentQuest();
            foreach (var (button, tab) in _tabButtons)
            {
                bool recommended = guide != null && guide.Target == TargetFor(tab);
                button.GetComponentInChildren<Text>().text = (recommended ? "› " : "") + Tabs.First(t => t.tab == tab).name;
            }
            GuideHint(_dockContent, TargetFor(_tab), RebuildDock);
            switch (_tab)
            {
                case ShopTab.Counter: BuildCounter(); break;
                case ShopTab.Stock: BuildStock(); break;
                case ShopTab.Pricing: BuildPricing(); break;
                case ShopTab.Orders: BuildOrders(); break;
                case ShopTab.Renovate: BuildRenovate(); break;
                case ShopTab.Upgrades: BuildUpgrades(); break;
                case ShopTab.Staff: BuildStaff(); break;
            }
            Ui.RestoreScroll(_dock, _dock, scrolled);
            if (scrolled == null) _dockContent.anchoredPosition = Vector2.zero;
        }

        private Transform C => _dockContent;

        // ---------- 柜台 ----------

        /// <summary>营业时间：开门 / 关门各按小时调，或者不限时（有人看店就开）。不在营业时间里，人在店里也不开门。</summary>
        private void ShopHoursRow(ShopSimulation shop)
        {
            const int day = 24 * 60;
            RectTransform row = Ui.Row(C, 36, 8);
            Ui.Size(Ui.Label(row, "营业时间", 16), 80, 36);
            if (shop.AllDay)
            {
                Ui.Size(Ui.Button(row, "不限时  ▼", () => OpenHoursPicker(shop, true), 15, new Color(0.2f, 0.24f, 0.3f)), 150, 34);
                Ui.Size(Ui.Label(row, "有人看店就开", 14, TextAnchor.MiddleLeft, Ui.Dim), -1, 36, 1);
            }
            else
            {
                Ui.Size(Ui.Button(row, $"开门 {Ui.Hm(shop.OpenMinute)}  ▼", () => OpenHoursPicker(shop, true), 15, new Color(0.2f, 0.24f, 0.3f)), 150, 34);
                Ui.Size(Ui.Button(row, $"关门 {Ui.Hm(shop.CloseMinute % day)}  ▼", () => OpenHoursPicker(shop, false), 15, new Color(0.2f, 0.24f, 0.3f)), 150, 34);
                Ui.Size(Ui.Label(row, shop.CloseMinute <= shop.OpenMinute ? "过夜" : "", 14, TextAnchor.MiddleLeft, Ui.Dim), -1, 36, 1);
            }
            if (!shop.AllDay && !shop.InHours(S.Clock.Now))
                Line(C, $"现在不在营业时间，{Ui.Hm(shop.OpenMinute)} 才开门；人在店里也不接客。", 15, new Color32(231, 160, 135, 255));
        }

        private void OpenHoursPicker(ShopSimulation shop, bool pickOpen)
        {
            const int day = 24 * 60;
            int open = shop.AllDay ? 9 * 60 : shop.OpenMinute;
            int close = shop.AllDay ? 21 * 60 : shop.CloseMinute % day;
            OpenWindow(pickOpen ? "几点开门" : "几点关门", 560, 420, w =>
            {
                Ui.Size(Ui.Button(w.Content, "不限时（有人看店就开）", () => { shop.SetHours(0, day); w.Close(); RebuildDock(); },
                    16, shop.AllDay ? Ui.ButtonHot : Ui.ButtonBg), -1, 40);
                RectTransform row = null;
                for (int h = 0; h < 24; h++)
                {
                    if (h % 6 == 0) row = Ui.Row(w.Content, 40, 6);
                    int minute = h * 60;
                    bool current = !shop.AllDay && (pickOpen ? open : close) == minute;
                    bool clash = (pickOpen ? close : open) == minute;
                    Ui.Size(Ui.Button(row, Ui.Hm(minute), () =>
                    {
                        if (pickOpen) shop.SetHours(minute, close == 0 ? day : close);
                        else shop.SetHours(open, minute == 0 ? day : minute);
                        w.Close();
                        RebuildDock();
                    }, 16, current ? Ui.ButtonHot : Ui.ButtonBg, !clash), -1, 38, 1);
                }
            });
        }

        private void BuildCounter()
        {
            ShopSimulation shop = S.PlayerShop;
            bool fitBanner = !S.ShopFitted || S.IsRenovating;
            FitBanner(C);
            Line(C, $"今日进店 {shop.TodayCustomers} 人 · 流失 {shop.TodayLost} 人 · 营业额 {shop.TodaySales}", 18);
            Line(C, $"店里 {shop.ActiveCustomers.Count} 位顾客，{shop.Queue.Count} 位在排队结账（{PaymentText(shop.Accepted)}）", 17, Ui.Dim);
            ShopHoursRow(shop);
            if (S.Employee != null)
                Line(C, $"店员 {S.Employee.Name}：{(S.Employee.OnDuty(S.Clock.Now) ? "在岗" : "下班")}（在班持续收银，你可以协助或装机）", 16, Ui.Dim);

            bool atCounter = _ctrl.Interior != null && _ctrl.Interior.NearCounter;
            if (shop.Queue.Count > 0)
                Line(C, atCounter ? "你在收银台：点“结账”或按 F，把货交给顾客、收钱。" : "<color=#D79780>顾客在收银台前排队等你。走到收银台后面才能结账。</color>", 16);
            RectTransform list = Ui.Scroll(C, fitBanner ? 200 : 420);
            foreach (ShopCustomer c in shop.Queue)
            {
                ShopCustomer cust = c;
                RectTransform row = Ui.Row(list, 44);
                long waited = c.WaitingSince.MinutesUntil(S.Clock.Now);
                string patience = waited >= shop.Settings.PatienceMinutes - 12 ? $"<color=#D79780>等了 {waited} 分钟</color>" : $"等了 {waited} 分钟";
                Ui.Size(Ui.Label(row, $"{c.ItemName}（{(c.DisplayTier >= 0 ? "展示机" : c.GoodsId != null ? "杂货" : c.ItemBoxed ? "盒装" : "散件")}） <color=#A9C9A9>{c.Spend}</color>\n<size=14>{patience}</size>", 17), 360);
                Ui.Size(Ui.Button(row, "结账", () => _ctrl.Checkout(cust.Id), 18, Ui.ButtonHot, atCounter), 100);
            }
            if (shop.Queue.Count == 0) Ui.Size(Ui.Label(list, "没人排队。顾客挑好货会过来，等太久就走。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);

            int orders = shop.Orders.Count;
            if (orders > 0) Line(C, $"有 {orders} 个订单等你处理（去“订单”页）", 18, Ui.Warn);
            if (S.Online.Pending.Count > 0) Line(C, $"1788 有 {S.Online.Pending.Count} 单在路上，明天 10:00 送到", 17, Ui.Dim);
            Line(C, $"库存 {shop.Stock.UsedSpace:0.#}/{shop.Stock.Capacity:0} 格 · 全局加价 {shop.Markup:0.00}×", 17, Ui.Dim);
            if (BusinessUnlocked) Ui.Size(Ui.Button(C, "经营总览 · 合同 / 分店 / 资产", OpenBusinessPanel, 18, Ui.ButtonHot), -1, 40);
            Ui.Size(Ui.Button(C, "经营报表（近 14 天收入曲线）", OpenReportWindow, 18), -1, 40);
        }

        // ---------- 库存 ----------

        private void BuildStock()
        {
            ShopSimulation shop = S.PlayerShop;
            Line(C, $"后仓仓位 {shop.Stock.UsedSpace:0.#} / {shop.Stock.Capacity:0} 格（在途 {S.Online.PendingSpace():0.#} 格）　顾客只买货架上摆着的", 19);
            Ui.Bar(C, shop.Stock.Capacity > 0 ? (float)(shop.Stock.UsedSpace / shop.Stock.Capacity) : 0f, Ui.Accent, 280, 10);

            // 货架：一行一个，点“选择摆什么”弹出选择，点“查看”看货架上摆的样子
            Line(C, "—— 货架（每个货架摆一类货，顾客只买货架上摆着的）——", 16, Ui.Dim);
            var shelfIdx = S.ShelfIndexes();
            if (shelfIdx.Count == 0) Line(C, "店里还没有货架：去家具城买，在“装修”页进入编辑模式摆好。", 16, Ui.Warn);
            for (int si = 0; si < shelfIdx.Count; si++)
            {
                int layoutIdx = shelfIdx[si];
                ShelfKind sk = S.ShelfKindAt(layoutIdx);
                FurnitureDef sdef = FurnitureCatalog.Get(S.ShopLayout[layoutIdx].Id);
                string tier = sdef != null && sdef.ShelfKg > ShelfKinds.CapacityKg ? (sdef.ShelfKg >= 160 ? "重型" : "加固") : "普通";
                RectTransform srow = Ui.Row(C, 38, 4);
                Ui.Size(Ui.Label(srow, $"<b>{si + 1}号</b> <size=12><color=#ABB5A8>{tier}</color></size>", 16, TextAnchor.MiddleLeft), 78);
                Ui.Size(Ui.Button(srow, sk == null ? "选择摆什么 ▾" : sk.Name + "  ▾", () => OpenShelfKindChooser(layoutIdx), 16, sk == null ? Ui.Warn : Ui.ButtonBg), 150);
                string load = "";
                if (sk != null)
                {
                    ShelfFill sf = shop.ShelfFillOf(sk);
                    int same = Mathf.Max(1, S.ShelvesOfKind(sk.Id));
                    load = $"{sf.UsedKg:0.#}/{sf.CapacityKg:0} 公斤{(same > 1 ? $"（{same} 个共用）" : "")}";
                }
                Ui.Size(Ui.Label(srow, load, 14, TextAnchor.MiddleLeft, Ui.Dim), 178);
                Ui.Size(Ui.Button(srow, "查看", () => OpenShelfWindow(layoutIdx), 15, sk == null ? Ui.ButtonBg : Ui.ButtonHot, sk != null), 66);
            }

            // 店里现有的东西：只列有货的，数量写明白（没有的不显示）
            Line(C, "—— 店里现有（件）——", 16, Ui.Dim);
            PartCategory[] cats = { PartCategory.Gpu, PartCategory.Cpu, PartCategory.Memory, PartCategory.Storage, PartCategory.PowerCase, PartCategory.Motherboard };
            bool anything = false;
            foreach (PartCategory cat in cats)
            {
                int boxedN = 0, looseN = 0;
                foreach (StockLine sl in shop.Stock.Lines())
                {
                    if (sl.Part.Category != cat) continue;
                    if (sl.Boxed) boxedN += sl.Count; else looseN += sl.Count;
                }
                int total = boxedN + looseN;
                if (total == 0) continue;
                anything = true;
                RectTransform sum = Ui.Row(C, 32, 4);
                Ui.Size(Ui.Label(sum, "<b>" + PartCatalog.CategoryName(cat) + "</b>", 16, TextAnchor.MiddleLeft), 64);
                if (boxedN > 0)
                {
                    Image bBadge = Ui.Panel(sum, "Boxed", CardboardColor);
                    Ui.Size(bBadge, 76, 26);
                    Ui.Label(bBadge.transform, "整盒 " + boxedN, 14, TextAnchor.MiddleCenter, new Color(0.25f, 0.15f, 0.05f)).rectTransform.Fill(1);
                }
                if (looseN > 0)
                {
                    Image lBadge = Ui.Panel(sum, "Loose", new Color(0.36f, 0.5f, 0.68f));
                    Ui.Size(lBadge, 76, 26);
                    Ui.Label(lBadge.transform, "散件 " + looseN, 14, TextAnchor.MiddleCenter, Color.white).rectTransform.Fill(1);
                }
                int onShelf = shop.ShelfFillOf(cat).Total;
                Ui.Size(Ui.Label(sum, $"共 {total} 件　" + (S.ShelvesOfKind(ShelfKinds.ForCategory(cat).Id) == 0 ? "<color=#D79780>没有货架，卖不出去</color>" : $"货架上 {onShelf} 件"), 15, TextAnchor.MiddleLeft, Ui.Dim), 260);
            }
            foreach (ShelfKind gk in ShelfKinds.GoodsKinds)
            {
                int gTotal = gk.GiftIds.Sum(id => shop.Stock.GoodsCount(id));
                if (gTotal == 0) continue;
                anything = true;
                RectTransform sum = Ui.Row(C, 32, 4);
                Ui.Size(Ui.Label(sum, "<b>" + gk.Name + "</b>", 16, TextAnchor.MiddleLeft), 64);
                Ui.Size(Ui.Label(sum, $"共 {gTotal} 件　" + (S.ShelvesOfKind(gk.Id) == 0 ? "<color=#D79780>没有货架，卖不出去</color>" : $"货架上 {shop.ShelfFillOf(gk).Total} 件"), 15, TextAnchor.MiddleLeft, Ui.Dim), 420);
            }
            if (!anything) Line(C, "店里一件货都没有。", 16, Ui.Dim);
            Line(C, "盒装占 1 格、卖得贵；拆成散件占 0.5 格但便宜，拆了不能复原。", 14, Ui.Dim);
            Line(C, "—— 明细 ——", 16, Ui.Dim);
            if (_ctrl.InInterior)
                Ui.Size(Ui.Button(C, $"店里储物柜（{S.ShopLocker.WeightKg:0.#}/{GameSession.ShopLockerLimitKg:0} 公斤）", OpenShopLockerWindow, 17), -1, 42);
            RectTransform list = Ui.Scroll(C, 250);
            foreach (StockLine l in shop.Stock.Lines())
            {
                StockLine line = l;
                RectTransform row = Ui.Row(list, 40);
                Ui.Size(Ui.Label(row, $"{l.Part.Name} <color=#ABB5A8>{(l.Boxed ? "盒装" : "散件")}</color> ×{l.Count}", 17), 280);
                Ui.Size(Ui.Label(row, $"售 {shop.PriceOf(l.Part, l.Boxed)}", 16, TextAnchor.MiddleLeft, Ui.Good), 100);
                if (l.Boxed)
                {
                    Ui.Size(Ui.Button(row, "拆 1", () =>
                    {
                        S.Unbox(line.Part, 1);
                        RebuildDock();
                    }, 16), 60);
                    Ui.Size(Ui.Button(row, "全拆", () =>
                    {
                        S.Unbox(line.Part, line.Count);
                        RebuildDock();
                    }, 16), 60);
                }
            }
            foreach (var kv in shop.Stock.GoodsLines())
            {
                string gid = kv.Key;
                RectTransform row = Ui.Row(list, 40);
                Ui.Size(Ui.Label(row, $"{Gifts.Get(gid).Name} <color=#ABB5A8>杂货</color> ×{kv.Value}", 17), 280);
                Ui.Size(Ui.Label(row, $"售 {shop.GoodsPriceOf(gid)}", 16, TextAnchor.MiddleLeft, Ui.Good), 100);
                Ui.Size(Ui.Button(row, "收回 1", () =>
                {
                    S.UnlistGoods(gid, 1);
                    RebuildDock();
                }, 16), 80);
            }
            if (shop.Stock.IsEmpty) Ui.Size(Ui.Label(list, "空空如也。去电子市场进货，或用手机上的 1788 app 下单。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);
        }

        // ---------- 定价 ----------

        private void BuildPricing()
        {
            ShopSimulation shop = S.PlayerShop;
            Line(C, "售价 = 今日市场价 × 加价倍率（盒装再贵 12%）。可以给每件配件单独定价，没单独定的跟全局。", 15, Ui.Dim);
            Text valueText = Line(C, "", 20, Ui.Good);
            var refreshers = new List<System.Action>();

            void RefreshAll()
            {
                valueText.text = $"全局加价 {shop.Markup:0.00}×　顾客愿买概率约 {ShopSettings.BuyChance(shop.Markup) * 100:0}%";
                foreach (System.Action r in refreshers) r();
            }

            Ui.Slider(C, 0.9f, 1.6f, (float)shop.Markup, v =>
            {
                S.SetMarkup(Mathf.Round(v * 20) / 20f);
                RefreshAll();
            });

            RectTransform list = Ui.Scroll(C, 330);
            foreach (PartDef part in PartCatalog.All)
            {
                PartDef p = part;
                RectTransform row = Ui.Row(list, 44, 4);
                Text name = Ui.Label(row, "", 15);
                Ui.Size(name, 250);
                Text mk = Ui.Label(row, "", 16, TextAnchor.MiddleCenter);
                Button minus = null, plus = null, follow = null;

                void Refresh()
                {
                    bool own = shop.HasPartMarkup(p);
                    double m = shop.MarkupOf(p);
                    name.text = $"<b>{p.Name}</b>\n<size=13><color=#ABB5A8>市价 {S.Market.PriceOf(p)} → 盒 {shop.PriceOf(p, true)} / 散 {shop.PriceOf(p, false)}</color></size>";
                    mk.text = own ? $"{m:0.00}×" : $"<color=#ABB5A8>{m:0.00}×</color>";
                    follow.interactable = own;
                }

                minus = Ui.Button(row, "−", () =>
                {
                    shop.SetPartMarkup(p, Mathf.Round((float)shop.MarkupOf(p) * 20 - 1) / 20f);
                    Refresh();
                }, 18);
                Ui.Size(mk, 64);
                Ui.Size(minus, 38);
                plus = Ui.Button(row, "+", () =>
                {
                    shop.SetPartMarkup(p, Mathf.Round((float)shop.MarkupOf(p) * 20 + 1) / 20f);
                    Refresh();
                }, 18);
                Ui.Size(plus, 38);
                follow = Ui.Button(row, "跟随", () =>
                {
                    shop.SetPartMarkup(p, null);
                    Refresh();
                }, 14);
                Ui.Size(follow, 62);
                mk.transform.SetSiblingIndex(minus.transform.GetSiblingIndex() + 1);
                refreshers.Add(Refresh);
            }
            RefreshAll();
            Line(C, "加价越高赚得越多，但更多顾客会嫌贵走掉；热门配件可以压低价引流，冷门高价配件可以拉高。", 15, Ui.Dim);
        }

        // ---------- 经营报表 ----------

        /// <summary>近 14 天每天的营业额（绿）和支出（红）柱状图。</summary>
        public void OpenReportWindow()
        {
            OpenWindow("经营报表", 900, 640, w =>
            {
                var days = PCShopSell.Core.Finance.FinanceReport.Daily(S.Ledger, S.Clock.Today, 14);
                Money rev = Money.Zero, exp = Money.Zero, other = Money.Zero;
                long max = 100;
                foreach (var d in days)
                {
                    rev += d.Revenue;
                    exp += d.Expense;
                    other += d.OtherIncome;
                    max = System.Math.Max(max, System.Math.Max(d.Revenue.Cents, d.Expense.Cents));
                }
                Line(w.Content, $"近 {days.Count} 天：营业额 {rev}　支出 {exp}　现金变化 {rev + other - exp}", 19);
                Line(w.Content, "<color=#A9C9A9>■</color> 营业额（零售 + 装机）　<color=#D79780>■</color> 支出（进货、房租、工资、开销等）", 15, Ui.Dim);

                Line(w.Content, $"其他收款 {other}（含退押金等）；采购、押金、家具和生活开销计入现金支出，不代表利润。", 14, Ui.Dim);
                Image plot = Ui.Panel(w.Content, "Chart", new Color(0, 0, 0, 0.22f));
                Ui.Size(plot, -1, 380);
                var h = plot.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(8, 8, 8, 8);
                h.spacing = 4;
                h.childControlWidth = true;
                h.childControlHeight = true;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = true;
                h.childAlignment = TextAnchor.LowerLeft;
                foreach (var d in days)
                {
                    RectTransform col = Ui.Rect(plot.transform, "Day");
                    Ui.Size(col, 56, -1, 0);
                    RectTransform area = Ui.Rect(col, "Bars").Anchor(0, 0, 1, 1, 0, 40, 0, 0);
                    float hr = d.Revenue.Cents <= 0 ? 0 : Mathf.Max(0.01f, (float)d.Revenue.Cents / max);
                    float he = d.Expense.Cents <= 0 ? 0 : Mathf.Max(0.01f, (float)d.Expense.Cents / max);
                    Ui.Panel(area, "Rev", new Color(0.48f, 0.89f, 0.55f)).rectTransform.Anchor(0.05f, 0, 0.48f, hr);
                    Ui.Panel(area, "Exp", new Color(0.88f, 0.5f, 0.42f)).rectTransform.Anchor(0.52f, 0, 0.95f, he);
                    Text lbl = Ui.Label(col, $"{d.Date.Month}/{d.Date.Day}\n<size=11><color=#A9C9A9>{(d.Revenue.IsZero ? "-" : "¥" + d.Revenue.Yuan)}</color></size>\n<size=11><color=#D79780>{(d.Expense.IsZero ? "-" : "¥" + d.Expense.Yuan)}</color></size>", 12, TextAnchor.UpperCenter);
                    lbl.rectTransform.Anchor(0, 0, 1, 0, 0, 0, 0, -40);
                }

                // 最好的一天
                var best = days.OrderByDescending(x => x.Revenue.Cents).FirstOrDefault();
                if (best != null && best.Revenue.IsPositive) Line(w.Content, $"营业额最高：{best.Date.Month}/{best.Date.Day}，营业额 {best.Revenue}", 16, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "关闭", w.Close, 20), -1, 46);
            });
        }

        // ---------- 订单 ----------

        private void BuildOrders()
        {
            ShopSimulation shop = S.PlayerShop;
            Line(C, "顾客下的订单都在这里（目前是装机订单）：两天内做好交付才收钱，超时取消、掉知名度。配件从库存里出。", 15, Ui.Dim);
            RectTransform list = Ui.Scroll(C, 430);
            foreach (BuildOrder o in shop.Orders)
            {
                BuildOrder order = o;
                List<PartDef> missing = S.MissingParts(o);
                long left = S.Clock.Now.MinutesUntil(o.DueAt);
                Text head = Ui.Label(list, $"<b>{PartCatalog.TierName(o.Tier)}整机</b>  报价 <color=#A9C9A9>{o.Price}</color>  剩 {left / 60} 小时", 18);
                Ui.Size(head, -1, 28);
                foreach (PartDef p in PartCatalog.BuildList(o.Tier))
                {
                    bool have = order.PartsReserved || shop.Stock.CountAll(p) > 0;
                    Ui.Size(Ui.Label(list, $"   {(have ? "<color=#A9C9A9>✓</color>" : "<color=#D79780>✗</color>")} {PartCatalog.CategoryName(p.Category)}：{p.Name}", 16), -1, 22);
                }
                Ui.Size(Ui.Button(list, missing.Count == 0 ? "开始装机" : $"还缺 {missing.Count} 样配件", () => OpenBuildMinigame(order), 18, Ui.ButtonHot, missing.Count == 0 && order.EmployeeMinutesLeft == 0), -1, 40);
                OrderActions(list, order, RebuildDock);
            }
            if (shop.Orders.Count == 0) Ui.Size(Ui.Label(list, "暂时没有订单。部分顾客想要装整机，装机订单会出现在这里。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);
        }

        // ---------- 升级 ----------

        private static string PaymentText(PaymentMethods m)
        {
            if ((m & PaymentMethods.SelfCheckout) != 0) return "自助结账，自动收款";
            if ((m & PaymentMethods.QrCode) != 0) return "现金 + 收款码";
            return "只收现金";
        }

        private void BuildUpgrades()
        {
            Para(C, "店里还能添的设备。这里只是清单，要去对应的店里买，买了当天装好，搬店也跟着走。", 16, Ui.Dim);
            Para(C, $"现在：{GameSession.SizeName(S.PlayerShop.Size)}，月租 {S.CurrentShopRent}。想换大铺子或者换个好位置，去商业街中段的「安居房产中介」看在租的铺面。", 15, Ui.Accent);
            RectTransform list = Ui.Scroll(C, 400);
            foreach (ShopUpgradeDef u in ShopUpgrades.All.Where(x => x.Offered))
            {
                bool owned = S.HasUpgrade(u.Id);
                string blocker = owned ? null : S.UpgradeBlocker(u);
                string status = owned ? "<color=#A9C9A9>已装好</color>"
                    : $"{u.Price}　去<color=#DDC18B>{u.SoldAtName}</color>买" + (blocker != null && blocker != "钱不够" ? $"　<color=#D79780>{blocker}</color>" : "");
                Para(list, $"<b>{u.Name}</b>　{status}\n<size=14>{u.Description}</size>", 17);
            }
            Para(list, "智能手机、收款码、自助结账、监控在街上的手机店；招牌灯箱、空调和更大更结实的货架在宜居家具城。自助结账一年目标达标也会赠送。", 14, Ui.Dim);
        }

        // ---------- 招人 ----------

        private void BuildStaff()
        {
            Line(C, $"可雇一位收银员和一位装机员，分别排班；工资按日结算，缩短班次不减薪。", 16, Ui.Dim);
            if (S.Employee != null)
            {
                Employee e = S.Employee;
                Line(C, $"{e.Name}（{e.Description}）日薪 {e.DailyWage}，结账每单 {e.CheckoutMinutes} 分钟", 19);
                if (!S.EmployeeTrained)
                    Ui.Size(Ui.Button(C, "装机培训 ¥1,200（日薪 +¥60）", () => { S.TrainEmployee(); RebuildDock(); }, 17, Ui.ButtonHot), -1, 44);
                else
                    Ui.Size(Ui.Button(C, S.AutoBuildEnabled ? "自动装机：开（点击暂停）" : "自动装机：关（点击启用）", () => { S.ToggleAutoBuild(); RebuildDock(); }, 17), -1, 44);
                Line(C, "培训后按交期装机；先收银、空闲时继续装机，离店后也能交付。", 14, Ui.Dim);
                Ui.Size(Ui.Button(C, $"收银员班次 {Ui.Hm(e.ShiftStart)}–{Ui.Hm(e.ShiftEnd)}（点击切换，日薪不变）", () => { S.CycleShift(false); RebuildDock(); }, 15), -1, 36);
                Ui.Size(Ui.Button(C, "辞退收银员", () =>
                {
                    S.Fire();
                    RebuildDock();
                }, 18, Ui.Warn), -1, 44);
            }
            if (S.Assembler != null)
            {
                var a = S.Assembler;
                Line(C, $"装机员 {a.Name} · 日薪 {a.DailyWage} · 累计完成 {a.BuildsCompleted} 单", 17);
                Ui.Size(Ui.Button(C, $"装机员班次 {Ui.Hm(a.ShiftStart)}–{Ui.Hm(a.ShiftEnd)}（点击切换）", () => { S.CycleShift(true); RebuildDock(); }, 15), -1, 36);
                Ui.Size(Ui.Button(C, S.AutoBuildEnabled ? "自动装机：开" : "自动装机：关", () => { S.ToggleAutoBuild(); RebuildDock(); }, 16), -1, 36);
                Ui.Size(Ui.Button(C, "辞退装机员", () => { S.FireAssembler(); RebuildDock(); }, 16, Ui.Warn), -1, 36);
            }
            if (S.Employee != null && S.Assembler != null) return;
            if (S.Candidates.Count > 0)
            {
                Line(C, "来应聘的人：", 19);
                for (int i = 0; i < S.Candidates.Count; i++)
                {
                    int idx = i;
                    Employee c = S.Candidates[i];
                    RectTransform row = Ui.Row(C, 50);
                    Ui.Size(Ui.Label(row, $"{c.Name}：{c.Description}\n<size=14>日薪 {c.DailyWage} · 结账每单 {c.CheckoutMinutes} 分钟</size>", 16), 300);
                    Ui.Size(Ui.Button(row, "收银", () =>
                    {
                        S.Hire(idx);
                        RebuildDock();
                    }, 16, Ui.ButtonHot, S.Employee == null), 90);
                    Ui.Size(Ui.Button(row, "装机", () => { S.HireAssembler(idx); RebuildDock(); }, 16, Ui.ButtonBg, S.Assembler == null), 90);
                }
                Line(C, "装机岗位：入职培训 ¥1,200，以上日薪另加 ¥60；熟练后每单更快。", 14, Ui.Dim);
                return;
            }
            if (S.JobAdDay >= 0)
            {
                Line(C, "招聘启事已经贴出去了，明天 9:00 会有人来面试。", 19);
                return;
            }
            Ui.Size(Ui.Button(C, $"在门口贴招聘启事（{GameSession.JobAdFee}）", () =>
            {
                S.PostJobAd();
                RebuildDock();
            }, 19, Ui.ButtonHot), -1, 48);
        }
    }
}
