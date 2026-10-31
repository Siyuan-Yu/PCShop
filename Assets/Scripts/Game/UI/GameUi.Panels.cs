using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCShopSell.Core.Catalog;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Inventory;
using PCShopSell.Core.Market;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        /// <summary>进入地点后打开对应面板。离开面板 = 回到街上。</summary>
        public void OnEnteredPlace(Place p)
        {
            if (S.IsWorking) { UpdateWorkScreen(); return; }
            switch (p.Kind)
            {
                case PlaceKind.Shop when S.IsMyShop(p):
                    OpenDock();
                    break;
                case PlaceKind.Agency:
                    _agencySelectedId = null;
                    _agencyViewState = new AgencyStreetMap.ViewState();
                    OpenPlaceWindow(p, BuildAgency, 1660, 960);
                    break;
                case PlaceKind.Home:
                    Toast("回到出租屋了。走近床、衣柜、电饭锅、纸箱或门，头顶出现提示时按 F；看房间信息点右上角“我的”。");
                    break;
                case PlaceKind.Shop when p.Business == GameSession.MallBusiness:
                    OpenPlaceWindow(p, BuildMall, 1140, 960);
                    break;
                case PlaceKind.Shop when p.Business == Gifts.TobaccoBusiness:
                    OpenPlaceWindow(p, BuildTobacco, 1040, 900);
                    break;
                case PlaceKind.Shop when p.Business == GameSession.PhoneBusiness:
                    OpenPlaceWindow(p, BuildPhoneShop, 960, 860);
                    break;
                case PlaceKind.Shop when p.Business == GameSession.FarmBusiness:
                    OpenPlaceWindow(p, BuildFarm, 1060, 960);
                    break;
                case PlaceKind.Wholesaler:
                    OpenPlaceWindow(p, BuildStall, 1060, 940);
                    break;
                case PlaceKind.Dealer:
                    OpenPlaceWindow(p, BuildDealer, 1180, 760);
                    break;
                case PlaceKind.Station:
                    OpenPlaceWindow(p, BuildStation);
                    break;
                default:
                    OpenPlaceWindow(p, BuildNeighbour);
                    break;
            }
        }

        private void OpenPlaceWindow(Place p, System.Action<Window, Place> build, float w = 640, float h = 520)
        {
            OpenWindow(S.PlaceTitle(p), w, h, win =>
            {
                if (S.CurrentPlace != p)
                {
                    win.Close();
                    return;
                }
                BuildPlayerWorkEntry(win.Content, p, win);
                RectTransform frame = p.Kind == PlaceKind.Agency || S.VentureAt(p.Id) != null || S.PropertyAt(p.Id) != null ? null : BuildRosterFrame(win, p, h - 170);
                if (frame != null)
                {
                    RectTransform original = win.Content;
                    win.Content = frame;
                    build(win, p);
                    win.Content = original;
                }
                else build(win, p);
                win.TitleText.text = S.PlaceTitle(p) + " · 关闭返回街上";
            }, true, true, () =>
            {
                if (S.CurrentPlace == p && !S.IsWorking) S.Leave();
            });
        }

        private static Text Line(Transform parent, string text, int size = 19, Color? color = null)
        {
            Text t = Ui.Label(parent, text, size, TextAnchor.MiddleLeft, color);
            Ui.Size(t, -1, size + 12);
            return t;
        }

        /// <summary>多行说明：高度跟着换行撑开。</summary>
        private static Text Para(Transform parent, string text, int size = 16, Color? color = null) =>
            Ui.Label(parent, text, size, TextAnchor.UpperLeft, color);

        // ---------- 档口 ----------

        private void BuildStall(Window w, Place p)
        {
            bool open = S.IsWholesalerOpen;
            List<PartDef> sold = PartCatalog.All.Where(x => S.StallSells(p, x)).ToList();
            List<string> tabs = sold.Select(x => PartCatalog.CategoryName(x.Category)).Distinct().ToList();
            string tab = tabs.Count > 0 ? ShopTabs(w, p, tabs) : null;
            Line(w.Content, open ? "营业中（工作日 9:00–18:00）。批发价，只卖盒装正品。" : "<color=#D79780>关门了（工作日 9:00–18:00 营业）</color>");
            Line(w.Content, $"现金 {S.Ledger.Balance}　　{S.CurrentVehicle.Name}还能背 {S.FreeKg:0.##} 千克　　先加进购物车，结账时分好自己背的和叫车送的", 17, Ui.Dim);

            RectTransform list = GoodsList(w.Content);
            foreach (PartDef part in sold.Where(x => PartCatalog.CategoryName(x.Category) == tab))
            {
                PartDef pd = part;
                Money price = S.StallPrice(p, pd);
                Money market = S.Market.PriceOf(pd);
                RectTransform row = Ui.Row(list, 50);
                Ui.Size(Ui.Label(row, $"{pd.Name}\n<size=13><color=#ABB5A8>市价 {market} · {Stock.SpaceOf(pd, true, 1):0.#}格</color></size>", 17), 210);
                Ui.Size(Ui.Label(row, $"{price}", 18, TextAnchor.MiddleLeft, Ui.Good), 84);
                int left = S.StallRemaining(p, pd) - (int)S.CartAmount("part:" + pd.Id);
                Ui.Size(Ui.Label(row, $"余 {left}", 14, TextAnchor.MiddleLeft, left == 0 ? Ui.Warn : Ui.Dim), 52);
                foreach (int n in new[] { 1 })
                {
                    int count = n;
                    CartAddButton(w, row, "part:" + pd.Id, () => S.CartAddStall(p, pd, count), "+" + count, 58, open && left >= count, n == 1);
                }
            }
            CartPanel(w, w.Content, true);
        }

        // ---------- 车行 ----------

        private void BuildDealer(Window w, Place p)
        {
            Line(w.Content, S.IsDealerOpen ? "营业中（每天 9:00–19:00）" : "<color=#D79780>关门了（每天 9:00–19:00）</color>");
            foreach (VehicleSpec v in Vehicles.All.Where(x => x.Purchasable))
            {
                VehicleSpec vs = v;
                RectTransform row = Ui.Row(w.Content, 78);
                string upkeep = vs.Kind == VehicleKind.Car ? " · 魅力 +2 · 日养护 ¥40" : vs.Kind == VehicleKind.Van ? " · 日养护 ¥60" : "";
                string desc = $"{vs.Name}  {vs.Price}{upkeep}\n<size=15>速度 {vs.SpeedPerMinute * 60 / 1000:0} km/h · 载货 {vs.CargoCapacity:0} 格{(vs.Illegal ? " · <color=#D79780>城里不能上路，交警会追</color>" : "")}</size>";
                Ui.Size(Ui.Label(row, desc + "\n<size=14>" + S.VehicleUsage(vs.Kind) + "</size>", 18), 760);
                bool owned = S.Owns(vs.Kind);
                Ui.Size(Ui.Button(row, owned ? "再买一台" : "购买一台", () =>
                {
                    S.BuyVehicle(vs.Kind);
                    w.Rebuild();
                }, 19, Ui.ButtonHot, S.IsDealerOpen && S.Ledger.Balance >= vs.Price), 100);
                if (owned) Ui.Size(Ui.Button(row, "卖回…", () => ConfirmBusiness("出售载具", $"按购价六折 {vs.Price.Scale(.6)} 回收。当前使用此车时需先把携带货物减到步行可承受范围。", () => { S.SellVehicle(vs.Kind); w.Rebuild(); }), 17, Ui.Warn, S.IsDealerOpen), 90);
            }
            if (S.TricycleImpounded)
            {
                Ui.Size(Ui.Button(w.Content, "交 ¥500 赎回一台三轮车", () =>
                {
                    S.RedeemTricycle();
                    w.Rebuild();
                }, 19, Ui.Warn), -1, 46);
            }
            Line(w.Content, "步行能带 6 格货，打车 15 格（按里程收费），小电驴 10 格，三轮车 40 格；轿车 18 格，合法小货车 80 格。", 16, Ui.Dim);
        }

        // ---------- 地铁 ----------

        private void BuildStation(Window w, Place p)
        {
            Place other = S.City.OfKind(PlaceKind.Station).FirstOrDefault(x => x != p);
            Line(w.Content, "地铁 1 号线：商业街站 ⇄ 电子市场站");
            Line(w.Content, $"票价 {GameSession.MetroFare}，约 {GameSession.MetroMinutes} 分钟。运营 6:00–23:00。", 18, Ui.Dim);
            if (other != null)
            {
                Ui.Size(Ui.Button(w.Content, "坐地铁去 " + other.Name, () =>
                {
                    if (S.TakeMetro())
                    {
                        CloseWindowSilently(w);
                        if (S.CurrentPlace != null) OnEnteredPlace(S.CurrentPlace);
                    }
                }, 21, Ui.ButtonHot), -1, 52);
            }
        }

        /// <summary>关窗口但不触发"离开地点"。</summary>
        private void CloseWindowSilently(Window w)
        {
            w.OnClosed = null;
            CloseWindow(w);
        }

        private void BuildNeighbour(Window w, Place p)
        {
            if (p.Id == S.ParkPlaceId)
            {
                Line(w.Content, "电脑乐园", 22, Ui.Accent);
                Para(w.Content, S.Clock.Today.TotalDays < S.ParkReadyDay ? $"公共区域正在改造，还有 {S.ParkReadyDay - S.Clock.Today.TotalDays} 天开放铺位。" : "园区已开放。选择铺位招商，调整不同业态之间的位置，在经营总览管理门店收支。", 18);
                Ui.Size(Ui.Button(w.Content, "查看园区布局", () => { _businessTab = 6; OpenBusinessPanel(); }, 18, Ui.ButtonHot), -1, 42);
                return;
            }
            var venture = S.VentureAt(p.Id);
            if (venture != null || S.PropertyAt(p.Id) != null)
            {
                Line(w.Content, S.PlaceTitle(p), 22, Ui.Accent);
                Para(w.Content, venture != null ? "这是你委托店长经营的门店。日常收支留在门店周转金中，可在经营总览管理定价、维护和资金。" : "这处物业由中介托管出租，每日租金进入你的账本。", 18);
                Ui.Size(Ui.Button(w.Content, "管理这处资产", OpenBusinessPanel, 18, Ui.ButtonHot), -1, 42);
                return;
            }
            if (S.IsVacant(p))
            {
                Line(w.Content, $"卷帘门拉着，门上贴着“{GameSession.VacantTitle}”。", 19);
                Line(w.Content, $"规格：{GameSession.SizeName(p.Size)}（{PCShopSell.Core.Shop.RealEstate.SizeArea(p.Size)}），离地铁站约 {S.StationDistance(p):0} 米。", 18, Ui.Dim);
                Line(w.Content, "联系人：安居房产中介。想租就去中介那里签约。", 18, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "去房产中介", () =>
                {
                    w.Close();
                    _ctrl.GoToPlace(S.City.FirstOfKind(PlaceKind.Agency));
                }, 19, Ui.ButtonHot), -1, 46);
                return;
            }
            Line(w.Content, $"邻居的店：{p.Business}");
            string size = p.Size == PCShopSell.Core.Shop.ShopSize.Large ? "大（216㎡）" : p.Size == PCShopSell.Core.Shop.ShopSize.Medium ? "中（108㎡）" : "小（48㎡）";
            Line(w.Content, "规格：" + size, 18, Ui.Dim);
            if (p.Business == "电脑") Line(w.Content, "同行。老板瞥了你一眼，没说话。", 18, Ui.Dim);
            else Line(w.Content, "生意不错。以后也许能在这里认识些人。", 18, Ui.Dim);
        }

        // ---------- 行情 ----------

        public void OpenMarketPanel()
        {
            S.ReadNews();
            OpenWindow("行情与新闻", 900, 880, w =>
            {
                Line(w.Content, "新闻 → 宏观指标 → 配件价格。读懂新闻，提前囤货或清货。", 18, Ui.Dim);
                foreach (MacroFactor f in new[] { MacroFactor.Crypto, MacroFactor.AiDemand, MacroFactor.NewGeneration })
                {
                    RectTransform row = Ui.Row(w.Content, 28);
                    double v = S.Market.FactorValue(f);
                    Ui.Size(Ui.Label(row, HardwareMarket.FactorName(f), 18), 120);
                    Ui.Bar(row, (float)(v + 1) / 2, v >= 0 ? Ui.Warn : Ui.Accent, 300, 14);
                    Ui.Size(Ui.Label(row, $"{v:+0.00;-0.00}", 17, TextAnchor.MiddleLeft, Ui.Dim), 80);
                }
                Line(w.Content, "品类价格指数（1.00 = 基准价）及近 30 天走势：", 18);
                foreach (PartCategory c in PartCatalog.Categories)
                {
                    RectTransform row = Ui.Row(w.Content, 30);
                    double idx = S.Market.Index(c);
                    Ui.Size(Ui.Label(row, PartCatalog.CategoryName(c), 18), 100);
                    Ui.Size(Ui.Label(row, $"{idx:0.00}", 19, TextAnchor.MiddleLeft, idx > 1.05 ? Ui.Warn : idx < 0.95 ? Ui.Good : Ui.TextColor), 70);
                    Ui.Size(Ui.Label(row, Sparkline(S.Market.History(c)), 18, TextAnchor.MiddleLeft, Ui.Dim), 300);
                    int held = PartCatalog.All.Where(p => p.Category == c).Sum(p => S.PlayerShop.Stock.CountAll(p));
                    int inbound = PartCatalog.All.Where(p => p.Category == c).Sum(p => S.PendingOnlineCount(p.Id));
                    Ui.Size(Ui.Label(row, $"库存 {held} · 在途 {inbound}", 15, TextAnchor.MiddleLeft, Ui.Accent), 230);
                }
                if (S.Player.Intelligence >= 30)
                    Line(w.Content, "研判提示：币圈主要影响显卡；AI 需求影响内存与显卡；新品上市压低旧 CPU 和主板。", 14, Ui.Accent);
                Line(w.Content, "最近新闻：", 18);
                RectTransform list = Ui.Scroll(w.Content, 150);
                foreach (NewsItem n in S.Market.News.Reverse().Take(20))
                {
                    Text t = Ui.Label(list, $"<color=#ABB5A8>{n.Date}</color>  {n.Headline}", 17);
                    Ui.Size(t, -1, 26);
                }
                if (S.Market.News.Count == 0) Ui.Size(Ui.Label(list, "暂时风平浪静。", 17, TextAnchor.MiddleLeft, Ui.Dim), -1, 26);
                BuildTipsSection(w);
            });
        }

        private static string Sparkline(System.Collections.Generic.IReadOnlyList<double> values)
        {
            const string blocks = "▁▂▃▄▅▆▇█";
            var sb = new StringBuilder();
            foreach (double v in values)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((float)((v - 0.6) / 1.2 * 7)), 0, 7);
                sb.Append(blocks[i]);
            }
            return sb.ToString();
        }

        // ---------- 菜单 ----------

        public void OpenSystemPanel()
        {
            OpenWindow(L("game_menu"), 620, 740, w =>
            {
                MenuTitle(w, "game_menu");
                Ui.Size(Ui.Button(w.Content, L("resume"), w.Close, 21, Ui.ButtonHot), -1, 50);
                Ui.Size(Ui.Button(w.Content, L(S.CanSave ? "save" : "cannot_save"), () => Toast(L(_ctrl.SaveGame() ? "saved" : "save_failed")), 20, Ui.ButtonBg, S.CanSave), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("load"), () => OpenLoadMenu(), 20, Ui.ButtonBg, SaveStore.Exists), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("settings"), OpenSettingsMenu, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("help"), OpenHelpMenu, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("about"), OpenAboutMenu, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("return_title"), () => ShowMainMenu(true), 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("quit"), OpenQuitMenu, 20, Ui.Warn), -1, 50);
            });
        }

        private void OpenLedger()
        {
            OpenWindow("账本", 760, 680, w =>
            {
                RectTransform list = Ui.Scroll(w.Content, 560);
                var entries = S.Ledger.Entries;
                for (int i = entries.Count - 1; i >= 0 && i >= entries.Count - 30; i--)
                {
                    var e = entries[i];
                    Text t = Ui.Label(list, $"<color=#ABB5A8>{e.Date}</color>  {e.Category}  {(e.Amount.IsNegative ? "<color=#D79780>" : "<color=#A9C9A9>")}{e.Amount}</color>  {e.Memo}", 16);
                    Ui.Size(t, -1, 24);
                }
            });
        }

        public void ShowGameOver()
        {
            OpenWindow("游戏结束", 600, 340, w =>
            {
                Line(w.Content, S.GameOverReason, 21);
                Line(w.Content, $"坚持到了 {S.Clock.Today}。", 18, Ui.Dim);
                Ui.Size(Ui.Button(w.Content, "重新开始", () => _ctrl.NewGame(), 20, Ui.ButtonHot), -1, 50);
                Ui.Size(Ui.Button(w.Content, "读档", () => _ctrl.LoadGame(), 20, Ui.ButtonBg, SaveStore.Exists), -1, 50);
            }, true, false);
        }

        public void ShowStartChoice() => ShowMainMenu();
    }
}
