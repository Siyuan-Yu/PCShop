using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
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
            Restock,
            Orders,
            Online,
            Upgrades,
            Staff,
        }

        private static readonly (ShopTab tab, string name)[] Tabs =
        {
            (ShopTab.Counter, "柜台"), (ShopTab.Stock, "库存"), (ShopTab.Pricing, "定价"), (ShopTab.Restock, "补货"),
            (ShopTab.Orders, "装机单"), (ShopTab.Online, "1788"), (ShopTab.Upgrades, "升级"), (ShopTab.Staff, "招人"),
        };

        private RectTransform _dock;
        private RectTransform _dockContent;
        private readonly List<(Button b, ShopTab tab)> _tabButtons = new List<(Button, ShopTab)>();
        private ShopTab _tab = ShopTab.Counter;
        private bool _dockHolding;
        private string _onlineStore;
        private readonly Dictionary<string, int> _cart = new Dictionary<string, int>();

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

            _dockContent = Ui.Rect(_dock, "Content").Anchor(0, 0, 1, 1, 0, 60, 0, 58);
            Ui.VList(_dockContent, 6, 12);

            Button leave = Ui.Button(_dock, "离开店铺（没人看店就关门）", () =>
            {
                CloseDock();
                S.Leave();
            }, 19, Ui.Warn);
            ((RectTransform)leave.transform).Anchor(0, 0, 1, 0, 12, 8, 12, -52);

            _tab = ShopTab.Counter;
            SelectTab(ShopTab.Counter);
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

        public void RebuildDock()
        {
            if (_dock == null) return;
            Ui.Clear(_dockContent);
            switch (_tab)
            {
                case ShopTab.Counter: BuildCounter(); break;
                case ShopTab.Stock: BuildStock(); break;
                case ShopTab.Pricing: BuildPricing(); break;
                case ShopTab.Restock: BuildRestock(); break;
                case ShopTab.Orders: BuildOrders(); break;
                case ShopTab.Online: BuildOnline(); break;
                case ShopTab.Upgrades: BuildUpgrades(); break;
                case ShopTab.Staff: BuildStaff(); break;
            }
        }

        private Transform C => _dockContent;

        // ---------- 货架 ----------

        /// <summary>走到货架前按 F：看这个货架上这一类配件各有多少（整盒 / 散件）、还能放多少。</summary>
        public void OpenShelfWindow(PartCategory cat)
        {
            OpenWindow("货架：" + PartCatalog.CategoryName(cat), 820, 560, w =>
            {
                Stock st = S.PlayerShop.Stock;
                var lines = st.Lines().Where(l => l.Part.Category == cat).ToList();
                int count = lines.Sum(l => l.Count);
                double used = lines.Sum(l => Stock.SpaceOf(l.Part, l.Boxed, l.Count));
                Line(w.Content, $"这一类现有 {count} 件，占 {used:0.#} 格。整店仓位 {st.UsedSpace:0.#}/{st.Capacity:0.#} 格，还剩 {st.FreeSpace:0.#} 格（所有货架共用一个仓位，放哪类都占这里的格子）。", 16, Ui.Dim);
                if (count == 0) Line(w.Content, "货架是空的，只摆着几件样品。去进货吧。", 18, Ui.Warn);
                RectTransform list = Ui.Scroll(w.Content, 380);
                foreach (PartDef p in PartCatalog.All.Where(x => x.Category == cat))
                {
                    int boxed = st.Count(p, true), loose = st.Count(p, false);
                    RectTransform row = Ui.Row(list, 70);
                    string have = boxed + loose == 0 ? "<color=#9AA3AD>没货</color>" : $"整盒 <b>{boxed}</b> 个　散件 <b>{loose}</b> 个";
                    Ui.Size(Ui.Label(row, $"<b>{p.Name}</b>\n<size=15>{have}</size>", 18), 400);
                    Ui.Size(Ui.Label(row,
                        $"<size=14><color=#9AA3AD>整盒每个占 {Stock.SpaceOf(p, true, 1):0.#} 格，还能放 {st.MaxAddable(p, true)} 个\n散件每个占 {Stock.SpaceOf(p, false, 1):0.#} 格，还能放 {st.MaxAddable(p, false)} 个</color></size>", 14), 330);
                }
                Ui.Size(Ui.Button(w.Content, "关闭", w.Close, 20), -1, 46);
            });
        }

        // ---------- 整机展示台 ----------

        /// <summary>展示台上的整机：三个档次各自的配置、报价、手头配件齐不齐。</summary>
        public void OpenDisplayWindow()
        {
            OpenWindow("整机展示台", 880, 640, w =>
            {
                ShopSimulation shop = S.PlayerShop;
                Line(w.Content, "顾客看中展台上的整机，会下装机单：离店后两天内你按配置装好才收钱（超时取消、掉知名度）。配件从库存里出，先备齐。", 16, Ui.Dim);
                RectTransform list = Ui.Scroll(w.Content, 400);
                for (int tier = 0; tier < 3; tier++)
                {
                    var parts = PartCatalog.BuildList(tier);
                    var missing = parts.Where(p => shop.Stock.CountAll(p) == 0).ToList();
                    string state = missing.Count == 0
                        ? "<color=#7CE38B>配件齐，随时能装</color>"
                        : "<color=#E0806A>缺 " + string.Join("、", missing.Select(p => PartCatalog.CategoryName(p.Category))) + "</color>";
                    Money cost = Money.Zero;
                    foreach (PartDef p in parts) cost += S.Market.PriceOf(p);
                    RectTransform row = Ui.Row(list, 118);
                    Ui.Size(Ui.Label(row, $"<b>{PartCatalog.TierName(tier)}整机</b>\n成交价约 <color=#7CE38B>{shop.BuildPrice(tier)}</color>\n<size=14>配件市价合计 {cost}\n{state}</size>", 18), 300);
                    Ui.Size(Ui.Label(row, "<size=14><color=#9AA3AD>" + string.Join("\n", parts.Select(p => $"{PartCatalog.CategoryName(p.Category)}：{p.Name}（库存 {shop.Stock.CountAll(p)}）")) + "</color></size>", 14), 480);
                }
                int orders = shop.Orders.Count;
                Line(w.Content, orders > 0 ? $"现在有 {orders} 个装机单等你装。" : "现在没有装机单。", 17, orders > 0 ? Ui.Warn : Ui.Dim);
                RectTransform bar = Ui.Row(w.Content, 46);
                if (orders > 0)
                    Ui.Size(Ui.Button(bar, "去处理装机单", () =>
                    {
                        w.Close();
                        OpenDock();
                        SelectTab(ShopTab.Orders);
                    }, 19, Ui.ButtonHot), 300);
                Ui.Size(Ui.Button(bar, "关闭", w.Close, 19), 200);
            });
        }

        // ---------- 柜台 ----------

        private void BuildCounter()
        {
            ShopSimulation shop = S.PlayerShop;
            Line(C, $"今日进店 {shop.TodayCustomers} 人 · 流失 {shop.TodayLost} 人 · 营业额 {shop.TodaySales}", 18);
            Line(C, $"店里 {shop.ActiveCustomers.Count} 位顾客，{shop.Queue.Count} 位在排队结账（{PaymentText(shop.Accepted)}）", 17, Ui.Dim);
            if (S.Employee != null)
                Line(C, $"店员 {S.Employee.Name}：{(S.IsEmployeeOnDuty(S.Clock.Now) ? "在岗" : "下班")}（你在店时你来收银）", 16, Ui.Dim);

            RectTransform list = Ui.Scroll(C, 420);
            foreach (ShopCustomer c in shop.Queue)
            {
                ShopCustomer cust = c;
                RectTransform row = Ui.Row(list, 44);
                long waited = c.WaitingSince.MinutesUntil(S.Clock.Now);
                string patience = waited >= shop.Settings.PatienceMinutes - 12 ? $"<color=#E0806A>等了 {waited} 分钟</color>" : $"等了 {waited} 分钟";
                Ui.Size(Ui.Label(row, $"{c.Item?.Name}（{(c.ItemBoxed ? "盒装" : "散件")}） <color=#7CE38B>{c.Spend}</color>\n<size=14>{patience}</size>", 17), 360);
                Ui.Size(Ui.Button(row, "结账", () => _ctrl.Checkout(cust.Id), 18, Ui.ButtonHot), 100);
            }
            if (shop.Queue.Count == 0) Ui.Size(Ui.Label(list, "没人排队。顾客挑好货会过来，等太久就走。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);

            int orders = shop.Orders.Count;
            if (orders > 0) Line(C, $"有 {orders} 个装机订单等你装（去'装机单'页）", 18, Ui.Warn);
            if (S.Online.Pending.Count > 0) Line(C, $"1788 有 {S.Online.Pending.Count} 单在路上，明天 10:00 送到", 17, Ui.Dim);
            Line(C, $"库存 {shop.Stock.UsedSpace:0.#}/{shop.Stock.Capacity:0} 格 · 全局加价 {shop.Markup:0.00}×", 17, Ui.Dim);
            Ui.Size(Ui.Button(C, "经营报表（近 14 天收入曲线）", OpenReportWindow, 18), -1, 40);
        }

        // ---------- 库存 ----------

        private void BuildStock()
        {
            ShopSimulation shop = S.PlayerShop;
            Line(C, $"已用 {shop.Stock.UsedSpace:0.#} / {shop.Stock.Capacity:0} 格（在途 {S.Online.PendingSpace():0.#} 格）", 19);
            Line(C, "盒装占 1 格、卖得贵、顾客更信任；拆成散件只占 0.4 格但便宜。显卡、电源机箱体积翻倍。拆了不能复原。", 15, Ui.Dim);
            RectTransform list = Ui.Scroll(C, 520);
            foreach (StockLine l in shop.Stock.Lines())
            {
                StockLine line = l;
                RectTransform row = Ui.Row(list, 40);
                Ui.Size(Ui.Label(row, $"{l.Part.Name} <color=#9AA3AD>{(l.Boxed ? "盒装" : "散件")}</color> ×{l.Count}", 17), 280);
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
            if (shop.Stock.IsEmpty) Ui.Size(Ui.Label(list, "空空如也。去电子市场进货，或在 1788 下单。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);
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
                    name.text = $"<b>{p.Name}</b>\n<size=13><color=#9AA3AD>市价 {S.Market.PriceOf(p)} → 盒 {shop.PriceOf(p, true)} / 散 {shop.PriceOf(p, false)}</color></size>";
                    mk.text = own ? $"{m:0.00}×" : $"<color=#9AA3AD>{m:0.00}×</color>";
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

        // ---------- 补货 ----------

        private void BuildRestock()
        {
            Line(C, "给配件设目标库存（0 = 不自动补）。每天 9:00 检查，库存（含在途）低于目标 40% 就用手机在 1788 自动补到目标，次日 10:00 到货。", 15, Ui.Dim);
            string block = S.AppBlocker(PhoneApps.Wholesale);
            Line(C, block == null ? "手机和 1788 都就绪，自动补货可用。" : "现在补不了货：" + block, 16, block == null ? Ui.Good : Ui.Warn);
            Line(C, $"自动补货后至少留 {GameSession.RestockCashReserve} 现金；放不下仓位的会少补。", 15, Ui.Dim);

            RectTransform storeRow = Ui.Row(C, 40);
            Text storeText = Ui.Label(storeRow, "", 16);
            Ui.Size(storeText, 360);
            void RefreshStore()
            {
                string id = S.RestockStoreId;
                storeText.text = id != null && S.Online.IsDiscovered(id)
                    ? "下单的店：" + Online1788.GetStore(id).Name
                    : "下单的店：自动（挑最便宜的，避开次品多的）";
            }
            Ui.Size(Ui.Button(storeRow, "换一家", () =>
            {
                var ids = new List<string> { null };
                ids.AddRange(Online1788.Stores.Where(s => S.Online.IsDiscovered(s.Id)).Select(s => s.Id));
                int i = ids.IndexOf(S.RestockStoreId);
                S.RestockStoreId = ids[(i + 1) % ids.Count];
                RefreshStore();
            }, 16), 100);
            RefreshStore();
            if (!string.IsNullOrEmpty(S.LastRestockNote)) Line(C, S.LastRestockNote, 14, Ui.Dim);

            RectTransform list = Ui.Scroll(C, 290);
            foreach (PartDef part in PartCatalog.All)
            {
                PartDef p = part;
                RectTransform row = Ui.Row(list, 40, 4);
                Text name = Ui.Label(row, "", 15);
                Ui.Size(name, 300);
                Text tgt = Ui.Label(row, "", 16, TextAnchor.MiddleCenter);
                Ui.Size(tgt, 60);
                Button minus = Ui.Button(row, "−", null, 18);
                Button plus = Ui.Button(row, "+", null, 18);
                Ui.Size(minus, 40);
                Ui.Size(plus, 40);

                void Refresh()
                {
                    int have = S.PlayerShop.Stock.CountAll(p), pend = S.PendingOnlineCount(p.Id);
                    string stock = have == 0 ? "<color=#E0806A>缺货</color>" : $"现有 {have}";
                    name.text = $"<b>{p.Name}</b>　<size=13>{stock}{(pend > 0 ? $" · 在途 {pend}" : "")}</size>";
                    int target = S.RestockTarget(p.Id);
                    tgt.text = target == 0 ? "<color=#9AA3AD>关</color>" : target.ToString();
                }

                minus.onClick.AddListener(() =>
                {
                    S.SetRestockTarget(p.Id, S.RestockTarget(p.Id) - 2);
                    Refresh();
                });
                plus.onClick.AddListener(() =>
                {
                    S.SetRestockTarget(p.Id, S.RestockTarget(p.Id) + 2);
                    Refresh();
                });
                Refresh();
            }
        }

        // ---------- 经营报表 ----------

        /// <summary>近 14 天每天的营业额（绿）和支出（红）柱状图。</summary>
        public void OpenReportWindow()
        {
            OpenWindow("经营报表", 900, 640, w =>
            {
                var days = PCShopSell.Core.Finance.FinanceReport.Daily(S.Ledger, S.Clock.Today, 14);
                Money rev = Money.Zero, exp = Money.Zero;
                long max = 100;
                foreach (var d in days)
                {
                    rev += d.Revenue;
                    exp += d.Expense;
                    max = System.Math.Max(max, System.Math.Max(d.Revenue.Cents, d.Expense.Cents));
                }
                Line(w.Content, $"近 {days.Count} 天：营业额 {rev}　支出 {exp}　净 {rev - exp}", 19);
                Line(w.Content, "<color=#7CE38B>■</color> 营业额（零售 + 装机）　<color=#E0806A>■</color> 支出（进货、房租、工资、开销等）", 15, Ui.Dim);

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
                    Text lbl = Ui.Label(col, $"{d.Date.Month}/{d.Date.Day}\n<size=11><color=#7CE38B>{(d.Revenue.IsZero ? "-" : "¥" + d.Revenue.Yuan)}</color></size>\n<size=11><color=#E0806A>{(d.Expense.IsZero ? "-" : "¥" + d.Expense.Yuan)}</color></size>", 12, TextAnchor.UpperCenter);
                    lbl.rectTransform.Anchor(0, 0, 1, 0, 0, 0, 0, -40);
                }

                // 最好的一天
                var best = days.OrderByDescending(x => x.Revenue.Cents).FirstOrDefault();
                if (best != null && best.Revenue.IsPositive) Line(w.Content, $"最好的一天：{best.Date.Month}/{best.Date.Day}，营业额 {best.Revenue}", 16, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "关闭", w.Close, 20), -1, 46);
            });
        }

        // ---------- 装机单 ----------

        private void BuildOrders()
        {
            ShopSimulation shop = S.PlayerShop;
            Line(C, "顾客下单后离开，两天内装好交付才收钱；超时取消、掉知名度。配件从库存里出。", 15, Ui.Dim);
            RectTransform list = Ui.Scroll(C, 430);
            foreach (BuildOrder o in shop.Orders)
            {
                BuildOrder order = o;
                List<PartDef> missing = S.MissingParts(o);
                long left = S.Clock.Now.MinutesUntil(o.DueAt);
                Text head = Ui.Label(list, $"<b>{PartCatalog.TierName(o.Tier)}整机</b>  报价 <color=#7CE38B>{o.Price}</color>  剩 {left / 60} 小时", 18);
                Ui.Size(head, -1, 28);
                foreach (PartDef p in PartCatalog.BuildList(o.Tier))
                {
                    bool have = shop.Stock.CountAll(p) > 0;
                    Ui.Size(Ui.Label(list, $"   {(have ? "<color=#7CE38B>✓</color>" : "<color=#E0806A>✗</color>")} {PartCatalog.CategoryName(p.Category)}：{p.Name}", 16), -1, 22);
                }
                Ui.Size(Ui.Button(list, missing.Count == 0 ? "开始装机" : $"还缺 {missing.Count} 样配件", () => OpenBuildMinigame(order), 18, Ui.ButtonHot, missing.Count == 0), -1, 40);
            }
            if (shop.Orders.Count == 0) Ui.Size(Ui.Label(list, "暂时没有装机订单。约每 8 位顾客里有 1 位想装整机。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);
        }

        // ---------- 1788 ----------

        private void BuildOnline()
        {
            Line(C, "1788 进货平台（用智能手机下单）：比档口便宜，次日 10:00 到店，可能有次品。评分不一定可信。", 15, Ui.Dim);
            string appBlock = S.AppBlocker(PhoneApps.Wholesale);
            if (appBlock != null) Line(C, "<color=#E0806A>" + appBlock + "</color>", 17);
            RectTransform stores = Ui.Scroll(C, 200);
            foreach (OnlineStore st in Online1788.Stores.Where(x => S.Online.IsDiscovered(x.Id)))
            {
                OnlineStore store = st;
                StoreRecord rec = S.Online.RecordOf(st.Id);
                string mine = rec.ItemsReceived > 0 ? $"你的记录：收货 {rec.ItemsReceived} 件，次品 {rec.Defects} 件" : "你还没在这家买过";
                Button b = Ui.Button(stores, $"{st.Name}  ★{st.ShownRating:0.0}  销量 {st.ShownSales}\n<size=14>{mine}</size>", () =>
                {
                    _onlineStore = store.Id;
                    _cart.Clear();
                    RebuildDock();
                }, 16, _onlineStore == st.Id ? Ui.ButtonHot : Ui.ButtonBg);
                Ui.Size(b, -1, 48);
            }

            if (_onlineStore == null || !S.Online.IsDiscovered(_onlineStore))
            {
                Line(C, "选一家店看看。", 17, Ui.Dim);
                return;
            }
            OnlineStore s = Online1788.GetStore(_onlineStore);
            Line(C, "评价：" + string.Join(" / ", s.Reviews), 15, Ui.Dim);

            RectTransform list = Ui.Scroll(C, 250);
            Money total = Online1788.ShippingFee;
            double space = 0;
            foreach (PartDef p in PartCatalog.All)
            {
                PartDef part = p;
                Money price = S.OnlinePrice(s, p);
                _cart.TryGetValue(p.Id, out int n);
                total += price * n;
                space += Stock.SpaceOf(p, true, n);
                RectTransform row = Ui.Row(list, 34);
                Ui.Size(Ui.Label(row, p.Name, 16), 210);
                Ui.Size(Ui.Label(row, $"{price}", 16, TextAnchor.MiddleLeft, Ui.Good), 80);
                Ui.Size(Ui.Label(row, $"市价 {S.Market.PriceOf(p)}", 13, TextAnchor.MiddleLeft, Ui.Dim), 90);
                Ui.Size(Ui.Button(row, "-", () => AddToCart(part, -1), 18), 36);
                Ui.Size(Ui.Label(row, n.ToString(), 17, TextAnchor.MiddleCenter), 30);
                Ui.Size(Ui.Button(row, "+", () => AddToCart(part, 1), 18), 36);
            }
            RectTransform orow = Ui.Row(C, 46);
            Ui.Size(Ui.Label(orow, $"合计 {total}（含运费 {Online1788.ShippingFee}） · {space:0.#} 格", 17), 330);
            Ui.Size(Ui.Button(orow, "下单", () =>
            {
                var lines = _cart.Where(kv => kv.Value > 0).Select(kv => (PartCatalog.Get(kv.Key), kv.Value)).ToList();
                if (S.PlaceOnlineOrder(_onlineStore, lines)) _cart.Clear();
                RebuildDock();
            }, 19, Ui.ButtonHot, _cart.Values.Any(v => v > 0)), 160);
        }

        private void AddToCart(PartDef p, int delta)
        {
            _cart.TryGetValue(p.Id, out int n);
            _cart[p.Id] = Mathf.Max(0, n + delta);
            RebuildDock();
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
            Line(C, $"给小店添点家当。现金 {S.Ledger.Balance}。买一次永久生效。", 16, Ui.Dim);
            RectTransform list = Ui.Scroll(C, 430);
            foreach (ShopExpansionDef ex in ShopExpansions.All)
            {
                ShopExpansionDef e = ex;
                bool has = S.PlayerShop.Size >= e.Size;
                string blocker = S.ExpansionBlocker(e);
                RectTransform erow = Ui.Row(list, 70);
                string status = has ? "<color=#7CE38B>已拥有</color>" : blocker == null ? $"<color=#7CE38B>{e.Price}</color>" : $"{e.Price}  <color=#E0806A>{blocker}</color>";
                Ui.Size(Ui.Label(erow, $"<b>{e.Name}</b>  {status}\n<size=14>{e.Description}</size>", 17), 420);
                Ui.Size(Ui.Button(erow, has ? "已买" : "扩建", () =>
                {
                    S.BuyExpansion(e.Size);
                    RebuildDock();
                }, 18, Ui.ButtonHot, !has && blocker == null), 90);
            }
            foreach (ShopUpgradeDef def in ShopUpgrades.All)
            {
                ShopUpgradeDef u = def;
                bool owned = S.HasUpgrade(u.Id);
                string blocker = S.UpgradeBlocker(u);
                RectTransform row = Ui.Row(list, 62);
                string status = owned ? "<color=#7CE38B>已拥有</color>" : blocker == null ? $"<color=#7CE38B>{u.Price}</color>" : $"{u.Price}  <color=#E0806A>{blocker}</color>";
                Ui.Size(Ui.Label(row, $"<b>{u.Name}</b>  {status}\n<size=14>{u.Description}</size>", 17), 420);
                Ui.Size(Ui.Button(row, owned ? "已买" : "购买", () =>
                {
                    S.BuyUpgrade(u.Id);
                    RebuildDock();
                }, 18, Ui.ButtonHot, !owned && blocker == null), 90);
            }
        }

        // ---------- 招人 ----------

        private void BuildStaff()
        {
            Line(C, $"店员每天 {Ui.Hm(GameSession.DutyStartMinute)}–{Ui.Hm(GameSession.DutyEndMinute)} 看店，只会收银不会装机，日薪每天结算。", 16, Ui.Dim);
            if (S.Employee != null)
            {
                Employee e = S.Employee;
                Line(C, $"{e.Name}（{e.Description}）日薪 {e.DailyWage}，结账每单 {e.CheckoutMinutes} 分钟", 19);
                Ui.Size(Ui.Button(C, "辞退", () =>
                {
                    S.Fire();
                    RebuildDock();
                }, 18, Ui.Warn), -1, 44);
                return;
            }
            if (S.Candidates.Count > 0)
            {
                Line(C, "来应聘的人：", 19);
                for (int i = 0; i < S.Candidates.Count; i++)
                {
                    int idx = i;
                    Employee c = S.Candidates[i];
                    RectTransform row = Ui.Row(C, 50);
                    Ui.Size(Ui.Label(row, $"{c.Name}：{c.Description}\n<size=14>日薪 {c.DailyWage} · 结账每单 {c.CheckoutMinutes} 分钟</size>", 18), 380);
                    Ui.Size(Ui.Button(row, "雇用", () =>
                    {
                        S.Hire(idx);
                        RebuildDock();
                    }, 18, Ui.ButtonHot), 100);
                }
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
