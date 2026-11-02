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
            if(S.IsOwnedVenue(p)){OpenVenueManagement(p);return;}
            if(p.Business!=null&&p.Business.StartsWith("venue_")||S.IsCityActivityVenue(p)&&S.CityActivities.Any(a=>a.Active&&a.VenueId==p.Id)){OpenCityVenue(p);return;}
            if(IsCarryStore(p)){OpenPlaceWindow(p,BuildCarryStore,1120,960);return;}
            if(!S.IsMyShop(p)&&(p.Id=="shop02"||p.Id=="clothing_boutique")){OpenCityClothingShop(p);return;}
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
                case PlaceKind.Shop when p.Business == GameSession.FastFoodBusiness && !S.IsVacant(p) && S.VentureAt(p.Id) == null && !S.IsPlayerPropertyOccupied(p.Id):
                    OpenPlaceWindow(p, BuildFastFood, 1080, 850);
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
                    if(S.HasRetailWholesale(p))OpenPlaceWindow(p,BuildRetailWholesale,1120,940);
                    else OpenPlaceWindow(p, BuildNeighbour);
                    break;
            }
        }

        private void OpenPlaceWindow(Place p, System.Action<Window, Place> build, float w = 640, float h = 520, bool holds = true, bool leaveOnClose = true)
        {
            OpenWindow(S.PlaceTitle(p), w, h, win =>
            {
                if (S.CurrentPlace != p)
                {
                    win.Close();
                    return;
                }
                BuildPlayerWorkEntry(win.Content, p, win);
                if(p.Kind==PlaceKind.Agency)Ui.Size(Ui.Button(win.Content,"购买 / 我的具体物业",OpenCityProperties,18,Ui.ButtonHot),-1,42);
                if (S.HasRetailWholesale(p) && build!=BuildRetailWholesale && (p.Business==GameSession.FarmBusiness || p.Business==Gifts.TobaccoBusiness)) Ui.Size(Ui.Button(win.Content, "批发进货（可供自己店铺销售）", () => OpenRetailWholesale(p), 18, Ui.ButtonHot), -1, 40);
                RectTransform frame = p.Kind == PlaceKind.Agency || S.VentureAt(p.Id) != null || S.IsPlayerPropertyOccupied(p.Id) ? null : BuildRosterFrame(win, p, h - 170);
                if (frame != null)
                {
                    RectTransform original = win.Content;
                    win.Content = frame;
                    build(win, p);
                    win.Content = original;
                }
                else build(win, p);
                win.TitleText.text = S.PlaceTitle(p) + (leaveOnClose ? " · 关闭返回街上" : " · 关闭菜单后留在场所");
            }, holds, true, () =>
            {
                if (leaveOnClose && S.CurrentPlace == p && !S.IsWorking) S.Leave();
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
            BuildCarryStore(w,p);
        }

        // ---------- 车行 ----------

        private void BuildDealer(Window w, Place p)
        {
            BuildCarryStore(w,p);
        }

        // ---------- 地铁 ----------

        private void BuildStation(Window w, Place p)
        {
            Line(w.Content,"城市地铁 · 全站互通");
            Line(w.Content,$"票价 {GameSession.MetroFare}，约 {GameSession.MetroMinutes} 分钟。运营 6:00–23:00。",18,Ui.Dim);
            foreach(var destination in S.City.OfKind(PlaceKind.Station).Where(x=>x!=p))
            {
                var target=destination;
                Ui.Size(Ui.Button(w.Content,"前往 "+target.Name+" · "+S.City.StreetName(target.StreetId),()=>
                {
                    if(S.TakeMetro(target.Id)){CloseWindowSilently(w);if(S.CurrentPlace!=null)OnEnteredPlace(S.CurrentPlace);}
                },19,Ui.ButtonHot),-1,44);
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
            if (venture != null || S.IsPlayerPropertyOccupied(p.Id))
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
                Ui.Size(Ui.Button(w.Content, "每周物价简报 · " + (S.WeeklyPricesSubscribed ? "已订阅" : "未订阅"), OpenWeeklyPriceReport, 18, Ui.ButtonHot), -1, 40);
                Ui.Size(Ui.Button(w.Content,"科技发布 / 模型作品热度 / 服装供给",OpenIndustryNews,17),-1,36);
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

        private int _ledgerPeriod, _ledgerFilter, _ledgerPage;
        private int _ledgerDay;

        private void OpenLedger()
        {
            _ledgerDay = S.Clock.Today.TotalDays;
            _ledgerPage = 0;
            OpenWindow("总收支账本", 940, 830, w =>
            {
                int today = S.Clock.Today.TotalDays;
                int span = _ledgerPeriod == 0 ? 1 : _ledgerPeriod == 1 ? 7 : 30;
                int start = _ledgerDay / span * span;
                int end = System.Math.Min(today, start + span - 1);
                var from = new PCShopSell.Core.Time.GameDate(start);
                var to = new PCShopSell.Core.Time.GameDate(end);
                var periods = Ui.Row(w.Content, 40);
                string[] names = { "按天", "按周", "按月" };
                for (int i = 0; i < names.Length; i++)
                {
                    int period = i;
                    Ui.Size(Ui.Button(periods, names[i], () => { _ledgerPeriod = period; _ledgerPage = 0; w.Rebuild(); }, 18,
                        i == _ledgerPeriod ? Ui.ButtonHot : Ui.ButtonBg), -1, -1, 1);
                }
                var dates = Ui.Row(w.Content, 42);
                Ui.Size(Ui.Button(dates, "上一" + (_ledgerPeriod == 0 ? "天" : _ledgerPeriod == 1 ? "周" : "月"), () =>
                { _ledgerDay = System.Math.Max(0, start - span); _ledgerPage = 0; w.Rebuild(); }, 17, Ui.ButtonBg, start > 0), 110);
                Ui.Size(Ui.Label(dates, span == 1 ? from.ToString() : from + " — " + to, 18, TextAnchor.MiddleCenter), -1, -1, 1);
                Ui.Size(Ui.Button(dates, "下一" + (_ledgerPeriod == 0 ? "天" : _ledgerPeriod == 1 ? "周" : "月"), () =>
                { _ledgerDay = start + span; _ledgerPage = 0; w.Rebuild(); }, 17, Ui.ButtonBg, start + span <= today), 110);
                Ui.Size(Ui.Button(dates, "回到当前", () => { _ledgerDay = today; _ledgerPage = 0; w.Rebuild(); }, 17), 110);

                var entries = S.Ledger.Entries.Where(e => e.Date >= from && e.Date <= to).ToList();
                Money income = Money.FromCents(entries.Where(e => e.Amount.IsPositive).Sum(e => e.Amount.Cents));
                Money expense = Money.FromCents(-entries.Where(e => e.Amount.IsNegative).Sum(e => e.Amount.Cents));
                Line(w.Content, $"总收入 {income}    总支出 {expense}    净收支 {income - expense}", 21, Ui.Accent);
                Line(w.Content, $"当前现金 {S.Ledger.Balance} · 包含经营、生活、打工、交通、购置、借还款等全部现金往来。", 16, Ui.Dim);
                Para(w.Content, "周按周一至周日，月按游戏日历每月 30 天；当前周期统计至今天。净收支是现金变化，包含借款、押金等，不等于利润。", 15, Ui.Dim);
                var filters = Ui.Row(w.Content, 36);
                string[] filterNames = { "全部明细", "只看收入", "只看支出" };
                for (int i = 0; i < filterNames.Length; i++)
                {
                    int filter = i;
                    Ui.Size(Ui.Button(filters, filterNames[i], () => { _ledgerFilter = filter; _ledgerPage = 0; w.Rebuild(); }, 17,
                        i == _ledgerFilter ? Ui.ButtonHot : Ui.ButtonBg), -1, -1, 1);
                }
                var filtered = entries.Where(e => _ledgerFilter == 0 || (_ledgerFilter == 1 ? e.Amount.IsPositive : e.Amount.IsNegative)).Reverse().ToList();
                const int pageSize = 40;
                int pages = System.Math.Max(1, (filtered.Count + pageSize - 1) / pageSize);
                _ledgerPage = System.Math.Min(_ledgerPage, pages - 1);
                var nav = Ui.Row(w.Content, 34);
                Ui.Size(Ui.Button(nav, "上一页", () => { _ledgerPage--; w.Rebuild(); }, 16, Ui.ButtonBg, _ledgerPage > 0), 100);
                Ui.Size(Ui.Label(nav, $"共 {filtered.Count} 笔 · 第 {_ledgerPage + 1}/{pages} 页", 16, TextAnchor.MiddleCenter), -1, -1, 1);
                Ui.Size(Ui.Button(nav, "下一页", () => { _ledgerPage++; w.Rebuild(); }, 16, Ui.ButtonBg, _ledgerPage + 1 < pages), 100);
                RectTransform list = Ui.Scroll(w.Content, 400);
                if (filtered.Count == 0) Line(list, "这段时间没有符合条件的收支记录。", 18, Ui.Dim);
                foreach (var e in filtered.Skip(_ledgerPage * pageSize).Take(pageSize))
                {
                    string sign = e.Amount.IsPositive ? "+" : "";
                    Line(list, $"{e.Date}  {e.Category}  {sign}{e.Amount}", 18, e.Amount.IsNegative ? Ui.Warn : Ui.Good);
                    Para(list, string.IsNullOrEmpty(e.Memo) ? "无备注" : e.Memo, 16, Ui.Dim);
                    Line(list, $"该笔后现金 {e.BalanceAfter}", 14, Ui.Dim);
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
