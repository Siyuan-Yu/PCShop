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
                RectTransform frame = p.Kind == PlaceKind.Agency || S.VentureAt(p.Id) != null || S.PropertyAt(p.Id) != null ? null : BuildRosterFrame(win, p, h - 170);
                if (frame != null)
                {
                    RectTransform original = win.Content;
                    win.Content = frame;
                    build(win, p);
                    win.Content = original;
                }
                else build(win, p);
                Ui.Size(Ui.Button(win.Content, "离开，回到街上", () => win.Close(), 20), -1, 46);
            }, true, true, () =>
            {
                if (S.CurrentPlace == p) S.Leave();
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
            Line(w.Content, open ? "营业中（工作日 9:00–18:00）。批发价，只卖盒装正品。" : "<color=#D79780>关门了（工作日 9:00–18:00 营业）</color>");
            Line(w.Content, $"现金 {S.Ledger.Balance}　　{S.CurrentVehicle.Name}还能背 {S.FreeKg:0.#} 公斤　　先加进购物车，结账时分好自己背的和叫车送的", 17, Ui.Dim);

            RectTransform list = Ui.Scroll(w.Content, 260);
            foreach (PartDef part in PartCatalog.All.Where(x => S.StallSells(p, x)))
            {
                PartDef pd = part;
                Money price = S.StallPrice(p, pd);
                Money market = S.Market.PriceOf(pd);
                RectTransform row = Ui.Row(list, 50);
                Ui.Size(Ui.Label(row, $"{pd.Name}\n<size=13><color=#ABB5A8>市价 {market} · {Stock.SpaceOf(pd, true, 1):0.#}格</color></size>", 17), 210);
                Ui.Size(Ui.Label(row, $"{price}", 18, TextAnchor.MiddleLeft, Ui.Good), 84);
                int left = S.StallRemaining(p, pd) - (int)S.CartAmount("part:" + pd.Id);
                Ui.Size(Ui.Label(row, $"余 {left}", 14, TextAnchor.MiddleLeft, left == 0 ? Ui.Warn : Ui.Dim), 52);
                foreach (int n in new[] { 1, 5 })
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
                Ui.Size(Ui.Label(row, desc, 19), 590);
                bool owned = S.Owns(vs.Kind);
                Ui.Size(Ui.Button(row, owned ? "已拥有" : "购买", () =>
                {
                    S.BuyVehicle(vs.Kind);
                    w.Rebuild();
                }, 19, Ui.ButtonHot, !owned && S.IsDealerOpen && S.Ledger.Balance >= vs.Price), 100);
                if (owned) Ui.Size(Ui.Button(row, "卖回…", () => ConfirmBusiness("出售载具", $"按购价六折 {vs.Price.Scale(.6)} 回收。当前使用此车时需先把携带货物减到步行可承受范围。", () => { S.SellVehicle(vs.Kind); w.Rebuild(); }), 17, Ui.Warn, S.IsDealerOpen), 90);
            }
            if (S.TricycleImpounded)
            {
                Ui.Size(Ui.Button(w.Content, "交 ¥500 赎回被扣的三轮车", () =>
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
            OpenWindow("菜单", 560, 700, w =>
            {
                Line(w.Content, $"身家（资产 − 负债）：{S.NetWorth()}", 20);
                GoalLine goal = S.NextGoal();
                if (goal != null)
                {
                    Line(w.Content, $"下一个目标：{goal.Title}（第 {goal.Day} 天）", 17, Ui.Dim);
                    Line(w.Content, goal.Progress, 16, Ui.Dim);
                }
                Ui.Size(Ui.Button(w.Content, "目标与人脉", OpenGoalsPanel, 20, Ui.ButtonHot), -1, 40);
                if (BusinessUnlocked) Ui.Size(Ui.Button(w.Content, "经营总览", OpenBusinessPanel, 20, Ui.ButtonHot), -1, 40);
                Ui.Size(Ui.Button(w.Content, "通知记录", OpenNoticeHistory, 18), -1, 36);
                Ui.Size(Ui.Button(w.Content, S.CanSave ? "存档" : "路上不能存档", () =>
                {
                    if (_ctrl.SaveGame()) Toast("已存档。");
                    else Toast(S.CanSave ? SaveStore.LastSaveMessage : "当前状态暂时不能存档。");
                }, 20, Ui.ButtonHot, S.CanSave), -1, 48);
                Ui.Size(Ui.Button(w.Content, "读取手动存档…", () => ConfirmAction("读取手动存档", "回到上一次手动存档的状态。\n现在这局没存档的进度会丢掉。", "读取", () => _ctrl.LoadGame()), 20, Ui.ButtonBg, SaveStore.Exists), -1, 40);
                Ui.Size(Ui.Button(w.Content, "读取最近自动存档…", () => ConfirmAction("读取自动存档", "回到最近一次自动存档的状态。\n现在这局没存档的进度会丢掉。", "读取", () => _ctrl.LoadGame(true)), 20, Ui.ButtonBg, SaveStore.Exists), -1, 40);
                Ui.Size(Ui.Button(w.Content, "重新开始…", () => ConfirmAction("重新开始", "放弃现在这一局，从头开始新的一局。\n没存档的进度都会丢掉。", "重新开始", () => _ctrl.NewGame()), 20, Ui.Warn), -1, 48);
                RectTransform sfx = Ui.Row(w.Content, 48, 8);
                Ui.Size(Ui.Button(sfx, PCShopSell.Game.Runtime.SoundFx.Muted ? "音效：关" : "音效：开", () =>
                {
                    PCShopSell.Game.Runtime.SoundFx.Muted = !PCShopSell.Game.Runtime.SoundFx.Muted;
                    PCShopSell.Game.Runtime.SoundFx.Play(PCShopSell.Game.Runtime.Sfx.Coin);
                    w.Rebuild();
                }, 20), -1, -1, 1);
                Ui.Size(Ui.Button(sfx, $"音量 {PCShopSell.Game.Runtime.SoundFx.Volume * 100:0}%", () =>
                {
                    float v = PCShopSell.Game.Runtime.SoundFx.Volume + 0.2f;
                    PCShopSell.Game.Runtime.SoundFx.Volume = v > 1.01f ? 0.2f : v;
                    PCShopSell.Game.Runtime.SoundFx.Play(PCShopSell.Game.Runtime.Sfx.Coin);
                    w.Rebuild();
                }, 20, Ui.ButtonBg, !PCShopSell.Game.Runtime.SoundFx.Muted), -1, -1, 1);
                Ui.Size(Ui.Button(w.Content, "玩法说明", ShowIntro, 20), -1, 48);
                Ui.Size(Ui.Button(w.Content, "账本（最近 30 条）", OpenLedger, 20), -1, 48);
                Line(w.Content, "快捷键：空格 暂停 · 1–4 倍速 · 滚轮 缩放 · Esc / 右键 关窗口（没开窗口时 Esc 开菜单）· M 我的 · B 背包 · C 日程 · P 手机 · J 经营 · H 行情 · 鼠标停在按钮上有说明", 16, Ui.Dim);
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

        public void ShowStartChoice()
        {
            OpenWindow("电脑城模拟器 VS1", 560, 360, w =>
            {
                Line(w.Content, "发现了存档。", 20);
                Ui.Size(Ui.Button(w.Content, "继续游戏", () =>
                {
                    w.Close();
                    _ctrl.LoadGame();
                }, 21, Ui.ButtonHot), -1, 54);
                Ui.Size(Ui.Button(w.Content, "新游戏", () =>
                {
                    w.Close();
                    ShowIntro();
                }, 21), -1, 54);
            }, true, false);
        }
    }
}
