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
            Orders,
            Online,
            Upgrades,
            Staff,
        }

        private static readonly (ShopTab tab, string name)[] Tabs =
        {
            (ShopTab.Counter, "柜台"), (ShopTab.Stock, "库存"), (ShopTab.Pricing, "定价"),
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
                case ShopTab.Orders: BuildOrders(); break;
                case ShopTab.Online: BuildOnline(); break;
                case ShopTab.Upgrades: BuildUpgrades(); break;
                case ShopTab.Staff: BuildStaff(); break;
            }
        }

        private Transform C => _dockContent;

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
                string patience = waited >= shop.Settings.PatienceMinutes - 5 ? $"<color=#E0806A>等了 {waited} 分钟</color>" : $"等了 {waited} 分钟";
                Ui.Size(Ui.Label(row, $"{c.Item?.Name}（{(c.ItemBoxed ? "盒装" : "散件")}） <color=#7CE38B>{c.Spend}</color>\n<size=14>{patience}</size>", 17), 360);
                Ui.Size(Ui.Button(row, "结账", () => _ctrl.Checkout(cust.Id), 18, Ui.ButtonHot), 100);
            }
            if (shop.Queue.Count == 0) Ui.Size(Ui.Label(list, "没人排队。顾客挑好货会过来，等太久就走。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 30);

            int orders = shop.Orders.Count;
            if (orders > 0) Line(C, $"有 {orders} 个装机订单等你装（去'装机单'页）", 18, Ui.Warn);
            if (S.Online.Pending.Count > 0) Line(C, $"1788 有 {S.Online.Pending.Count} 单在路上，明天 10:00 送到", 17, Ui.Dim);
            Line(C, $"库存 {shop.Stock.UsedSpace:0.#}/{shop.Stock.Capacity:0} 格 · 加价 {shop.Markup:0.00}×", 17, Ui.Dim);
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
            Line(C, "售价 = 今日市场价 × 加价倍率（盒装再贵 12%）", 18);
            Text valueText = Line(C, "", 22, Ui.Good);
            Text chanceText = Line(C, "", 17, Ui.Dim);
            RectTransform examples = Ui.Rect(C, "Examples");
            Ui.VList(examples, 2, 0);
            Ui.Size(examples, -1, 230);

            void Refresh()
            {
                valueText.text = $"加价倍率 {shop.Markup:0.00}×";
                chanceText.text = $"顾客愿意买的概率约 {ShopSettings.BuyChance(shop.Markup) * 100:0}%（盒装更高一点）";
                Ui.Clear(examples);
                foreach (string id in new[] { "ram0", "ssd1", "cpu1", "gpu1", "psu0" })
                {
                    PartDef p = PartCatalog.Get(id);
                    Line(examples, $"{p.Name}：市价 {S.Market.PriceOf(p)} → 盒装 {shop.PriceOf(p, true)} / 散件 {shop.PriceOf(p, false)}", 16, Ui.Dim);
                }
            }

            Ui.Slider(C, 0.9f, 1.6f, (float)shop.Markup, v =>
            {
                S.SetMarkup(Mathf.Round(v * 20) / 20f);
                Refresh();
            });
            Refresh();
            Line(C, "加价越高赚得越多，但更多顾客会嫌贵走掉。", 16, Ui.Dim);
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
            if (!S.HasSmartphone) Line(C, "<color=#E0806A>你还没有智能手机，下不了单。去街上的手机店买一部。</color>", 17);
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
