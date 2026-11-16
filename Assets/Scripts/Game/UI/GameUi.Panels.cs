using System.Collections.Generic;
using System.Linq;
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
        public void OnEnteredPlace(Place p, string activityId = null)
        {
            if (S.IsWorking) { UpdateWorkScreen(); return; }
            if (TryOpenSignHelp(p, activityId)) return;
            if(p.Kind==PlaceKind.Garage){OpenMotorGarage();return;}
            if(RaceTracks.Get(p.Id)!=null){OpenRaceReception(p);return;}
            if(p.Kind==PlaceKind.Dealer||p.Id=="motor_scooter_mod"){OpenPlaceWindow(p,BuildMotorDealer,1160,900);return;}
            if(S.VentureAt(p.Id)?.Kind=="cars"){OpenPlaceWindow(p,BuildUsedMotorShop,1160,900);return;}
            if(p.Id=="life_expo"){OpenExpoReception();return;}
            if (p.Business == GameSession.BankBusiness)
            {
                OpenBank(p, S.AppLoans.Any(loan => !loan.Repaid) ? BankPage.Loans : BankPage.Transfers);
                return;
            }
            if (p.Business == GameSession.HospitalBusiness)
            {
                if (S.RaceAidAt(p) != null) return;
                OpenPlaceWindow(p, BuildHospital, 640, 450);
                return;
            }
            if(p.Id==GameSession.CommunityMcnId){OpenCommunityMcn();return;}
            if(S.IsOwnedVenue(p)){OpenVenueManagement(p);return;}
            if (UsesCityVenuePanel(p)) { OpenCityVenue(p, activityId); return; }
            if (IsCarryStore(p))
            {
                if (p.Business == GameSession.MallBusiness) OpenMall(p);
                else OpenPlaceWindow(p, BuildCarryStore, StoreWindowWidth, 960);
                return;
            }
            if(!S.IsMyShop(p)&&(p.Id=="shop02"||p.Id=="clothing_boutique")){OpenCityClothingShop(p);return;}
            switch (p.Kind)
            {
                case PlaceKind.Shop when S.IsMyShop(p):
                    OpenDock();
                    break;
                case PlaceKind.Agency:
                    _agencyViewState = new AgencyStreetMap.ViewState();
                    OpenPlaceWindow(p, BuildAgency, 1660, 1000);
                    break;
                case PlaceKind.Home when p != S.Home:
                    Toast(S.CanSleepAt(p) ? "已到住处。走近床按 F 休息，走回门口按 F 出门。" : "走近在家的住户按 F 交谈，走回门口按 F 告辞。");
                    break;
                case PlaceKind.Home:
                    Toast("回到出租屋了。走近床、衣柜、电饭锅、纸箱或门，头顶出现提示时按 F；看房间信息点右上角“我的”。");
                    break;
                case PlaceKind.Shop when p.Business == GameSession.FastFoodBusiness && !S.IsVacant(p) && S.VentureAt(p.Id) == null && !S.IsPlayerPropertyOccupied(p.Id):
                    OpenPlaceWindow(p, BuildFastFood, StoreWindowWidth, 900);
                    break;
                case PlaceKind.Shop when p.Business == GameSession.MallBusiness:
                    OpenMall(p);
                    break;
                case PlaceKind.Shop when p.Business == Gifts.TobaccoBusiness:
                    OpenPlaceWindow(p, BuildTobacco, StoreWindowWidth, 900);
                    break;
                case PlaceKind.Shop when p.Business == GameSession.PhoneBusiness:
                    OpenPlaceWindow(p, BuildCarryStore, StoreWindowWidth, 900);
                    break;
                case PlaceKind.Shop when p.Business == GameSession.FarmBusiness:
                    OpenPlaceWindow(p, BuildRetailWholesale, StoreWindowWidth, 960);
                    break;
                case PlaceKind.Wholesaler:
                    OpenPlaceWindow(p, BuildStall, StoreWindowWidth, 940);
                    break;
                case PlaceKind.Dealer:
                    OpenPlaceWindow(p, BuildDealer, 1180, 760);
                    break;
                case PlaceKind.Station:
                    OpenPlaceWindow(p, BuildStation, holds: S.WalletReturnStation != p);
                    break;
                default:
                    if(S.HasRetailWholesale(p))OpenPlaceWindow(p,BuildRetailWholesale,StoreWindowWidth,940);
                    else OpenPlaceWindow(p, BuildNeighbour);
                    break;
            }
        }

        private void BuildHospital(Window w, Place hospital)
        {
            bool hasFlu = S.FluIllUntil > S.Clock.Now.TotalMinutes;
            bool canPay = S.Ledger.CanAfford(GameSession.OutpatientFee);
            Line(w.Content, "门诊", 22, Ui.Accent);
            Line(w.Content, $"健康 {S.Player.Health:0.#}/100" + (hasFlu ? " · 流感中" : ""), 18);
            Para(w.Content, $"看诊耗时 {GameSession.OutpatientMinutes} 分钟，可恢复最多 {GameSession.OutpatientHealthRecovery} 点健康并处理流感。", 18);
            if (S.Player.Health >= 100 && !hasFlu)
                Para(w.Content, "健康已满，仍可付费看诊。", 16, Ui.Dim);
            Ui.Size(Ui.Button(w.Content, $"支付 {GameSession.OutpatientFee} 看诊", () =>
            {
                if (S.ReceiveOutpatientCare()) CloseWindowSilently(w);
            }, 18, Ui.ButtonHot, canPay), -1, 46);
            if (!canPay)
                Para(w.Content, $"现金不足，还差 {GameSession.OutpatientFee - S.Ledger.Balance}。", 17, Ui.Warn);
        }

        private bool UsesCityVenuePanel(Place place) => place.Business?.StartsWith("venue_") == true
            || S.CityActivities.Any(activity => activity.Active && activity.VenueId == place.Id
                && (S.IsCityActivityVenue(place) || GameSession.IsOutdoorMarketVisit(activity)
                    && (activity.State == CityActivityPhase.Running || S.PendingVenueActivity(place, activity.Id) != null)));

        public void OpenResidence(Place residence)
        {
            int residentCount = S.CityPeople.Count(person => person.HomePlaceId == residence.Id);
            float rosterHeight = Mathf.Clamp(residentCount * 66 + 8, 140, 410);
            float bodyHeight = Mathf.Max(220, rosterHeight + 42);
            OpenWindow("住宅 · " + residence.Name, 800, bodyHeight + 100, window =>
            {
                var row = Ui.Row(window.Content, bodyHeight, 14);
                var roster = Ui.Rect(row, "Residents");
                Ui.Size(roster, 250, -1);
                Ui.VList(roster, 8, 0);
                Line(roster, "住户", 20, Ui.Accent);
                var portraits = Ui.Scroll(roster, rosterHeight);
                var residents = S.CityPeople.Where(person => person.HomePlaceId == residence.Id).ToArray();
                var present = S.ResidentsAtHome(residence).Select(person => person.PersonId).ToHashSet();
                if (residents.Length == 0)
                    Para(portraits, "暂无登记住户。", 17, Ui.Dim);
                foreach (var resident in residents)
                {
                    string id = resident.PersonId;
                    bool atHome = present.Contains(id);
                    var person = S.CityPerson(id);
                    var entry = Ui.Row(portraits, 58, 8);
                    var layout = entry.GetComponent<HorizontalLayoutGroup>();
                    layout.childForceExpandHeight = false;
                    layout.childAlignment = TextAnchor.MiddleLeft;
                    PortraitThumb(entry, id, 46, () => OpenCityPersonProfile(id, fromConversation: true), dimmed: !atHome);
                    var description = Ui.Rect(entry, "ResidentDetails");
                    Ui.Size(description, -1, -1, 1);
                    Ui.VList(description, 2, 0);
                    Line(description, person.Name + (atHome ? " · 在家" : " · 不在家"), 16, atHome ? Ui.Good : Ui.Dim);
                    Line(description, "好感 " + S.Relation(id), 15, Ui.Dim);
                }

                var details = Ui.Rect(row, "Visit");
                Ui.Size(details, -1, -1, 1);
                Ui.VList(details, 10, 0);
                Line(details, residence.Name, 22, Ui.Accent);
                Para(details, "点击左侧头像查看住户资料。", 16, Ui.Dim);
                string blocker = S.ClosedReason(residence);
                Para(details, "与至少一位住户的好感达到 " + GameSession.HomeVisitMinRelation + "，且这位住户在家时，可以进屋拜访。", 18);
                if (blocker != null) Para(details, blocker, 17, Ui.Warn);
                Ui.Size(Ui.Button(details, "进屋拜访", () =>
                {
                    if (S.NearbyPlace != residence || !S.EnterNearby())
                    {
                        window.Rebuild();
                        return;
                    }
                    window.Close();
                    OnEnteredPlace(residence);
                }, 18, Ui.ButtonHot, blocker == null), -1, 46).name = "ResidenceEnter";
            }, scrollContent: false);
        }

        private void OpenPlaceWindow(Place p, System.Action<Window, Place> build, float w = 640, float h = 520, bool holds = true, bool leaveOnClose = true)
        {
            bool isRaceTrack = RaceTracks.Get(p.Id) != null;
            bool fixedLayout = p.Kind == PlaceKind.Agency || p.Kind == PlaceKind.Garage || isRaceTrack;
            OpenWindow(S.PlaceTitle(p), w, h, win =>
            {
                if (S.CurrentPlace != p)
                {
                    win.Close();
                    return;
                }
                int contentBefore = win.Content.childCount;
                if (p.Id != "life_expo"&&p.Kind!=PlaceKind.Garage&&p.Business!=GameSession.BankBusiness
                    && S.OnsiteActivityAt(p) == null)
                    BuildPlayerWorkEntry(win.Content, p, win);
                if (S.HasRetailWholesale(p) && build!=BuildRetailWholesale && p.Business == Gifts.TobaccoBusiness) Ui.Size(Ui.Button(win.Content, "批发进货（可供自己店铺销售）", () => OpenRetailWholesale(p), 18, Ui.ButtonHot), -1, 40);
                RectTransform frame = fixedLayout || S.VentureAt(p.Id) != null || S.IsPlayerPropertyOccupied(p.Id) ? null : BuildRosterFrame(win, p, h - 170 - (win.Content.childCount - contentBefore) * 52, p.Kind==PlaceKind.Dealer || p.Kind==PlaceKind.Station || p.Id=="motor_scooter_mod", w >= StoreWindowWidth ? 150 : 210);
                if (frame != null)
                {
                    RectTransform original = win.Content;
                    win.Content = frame;
                    // 构建中途出错也必须还原 Content，否则下次重建会把整页再嵌进上一次的残页里（越点越套娃）。
                    try { build(win, p); }
                    finally { win.Content = original; }
                }
                else build(win, p);
                win.TitleText.text = S.PlaceTitle(p);
                if (leaveOnClose)
                {
                    var panel=win.Root.Find("Panel");
                    if(panel.Find("LeavePlaceFooter")==null)
                    {
                        var footer=Ui.Rect(panel,"LeavePlaceFooter").Anchor(0,0,1,0,16,10,16,-50);
                        Ui.Button(footer,"离开"+S.PlaceTitle(p),()=>{if(S.CurrentPlace==p&&!S.IsWorking){CloseModalWindows();_ctrl.LeaveCurrentPlace();}},17).GetComponent<RectTransform>().Fill().name="LeavePlace";
                    }
                    var contentFrame = fixedLayout ? win.Content : (RectTransform)win.Content.parent.parent;
                    contentFrame.offsetMin = new Vector2(0, 58);
                }
            }, holds, true, scrollContent: !fixedLayout);
        }

        private static Text Line(Transform parent, string text, int size = 19, Color? color = null)
        {
            Text t = Ui.Label(parent, text, size, TextAnchor.MiddleLeft, color);
            Ui.Size(t, -1, size + 12);
            if (size >= 20 || (size >= 16 && color == Ui.Warn)) t.fontStyle = FontStyle.Bold;
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
            BuildMotorDealer(w,p);
        }

        // ---------- 地铁 ----------

        private void BuildStation(Window w, Place p)
        {
            if (S.WalletReturnStation == p)
            {
                if (w.Holds)
                {
                    S.Time.Release();
                    w.Holds = false;
                }
                BuildWalletHandoff(w);
            }
            else w.RefreshContent = () =>
            {
                if (S.WalletReturnStation == p) w.RequestRebuild();
            };
            Line(w.Content, GameSession.FirstChapterTrial ? "城郊地铁" : "城市地铁 · 全站互通");
            Line(w.Content,$"票价 {GameSession.MetroFare}，约 {GameSession.MetroMinutes} 分钟。运营 6:00–23:00。",18,Ui.Dim);
            foreach(var destination in S.City.OfKind(PlaceKind.Station).Where(x=>x!=p))
            {
                var target=destination;
                string locked=S.DistrictAccessBlocker(target);
                if (GameSession.FirstChapterTrial && locked != null) continue;
                Ui.Size(Ui.Button(w.Content,(locked==null?"前往 ":"未开放 · ")+target.Name+" · "+S.City.StreetName(target.StreetId),()=>
                {
                    if(S.TakeMetro(target.Id)){CloseWindowSilently(w);if(S.CurrentPlace!=null)OnEnteredPlace(S.CurrentPlace);}
                },19,locked==null?Ui.ButtonHot:Ui.ButtonBg,locked==null),-1,44).Tip(locked??"乘地铁前往此站");
            }
        }

        private void OpenWalletReturnDestination()
        {
            var station = S.WalletReturnStation;
            if (station == null) return;
            CloseAllWindows();
            if (S.CurrentPlace == station) OnEnteredPlace(station);
            else S.BeginCityTravel(station.Id);
        }

        private void BuildWalletHandoff(Window w)
        {
            Line(w.Content, "归还钱包", 20, Ui.Accent);
            var status = Line(w.Content, "", 16, Ui.Good);
            status.name = "WalletWaitStatus";
            var actions = Ui.Row(w.Content, 38, 6);
            string owner = S.WalletReturnOwnerName;
            var resume = Ui.Size(Ui.Button(w.Content, "继续等候", () => S.Time.SetPaused(false), 16), -1, 34);
            resume.name = "WalletResumeWait";
            Button toOwner = null;
            if (owner != null)
            {
                toOwner = Ui.Size(Ui.Button(actions, "还给" + owner, () => Complete(true), 16, Ui.ButtonHot), -1, 38, 1);
                toOwner.name = "WalletReturnOwner";
            }
            var toDesk = Ui.Size(Ui.Button(actions, "交给服务台", () => Complete(false), 16, owner == null ? Ui.ButtonHot : Ui.ButtonBg), -1, 38, 1);
            toDesk.name = "WalletReturnDesk";
            w.RefreshContent = Refresh;
            Refresh();

            void Refresh()
            {
                if (S.CurrentPlace != S.WalletReturnStation)
                {
                    w.RequestRebuild();
                    return;
                }
                int left = S.WalletWaitLeft;
                bool ownerHere = S.WalletOwnerAtStation;
                status.text = left > 0 ? $"在站内等候 · 还需 {left} 分钟"
                    : owner == null ? "站务员可以登记失物。"
                    : ownerHere ? owner + "已到站，可以当面归还。" : "失主还没到站，可以继续等或交给服务台。";
                resume.gameObject.SetActive(S.Time.IsUserPaused && (left > 0 || owner != null && !ownerHere));
                toDesk.interactable = left == 0;
                if (toOwner != null) toOwner.interactable = left == 0 && ownerHere;
            }

            void Complete(bool giveToOwner)
            {
                if (!S.HandOverWallet(giveToOwner)) return;
                if (!w.Holds)
                {
                    S.Time.Hold();
                    w.Holds = true;
                }
                w.Rebuild();
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
                Para(w.Content, S.Clock.Today.TotalDays < S.ParkReadyDay ? $"公共区域正在改造，还有 {S.ParkReadyDay - S.Clock.Today.TotalDays} 天开放铺位。" : "园区已开放。选择铺位招商，调整不同业态之间的位置，在资产页查看门店收支。", 18);
                Ui.Size(Ui.Button(w.Content, "查看园区布局", () => OpenBusinessSection(6), 18, Ui.ButtonHot), -1, 42);
                return;
            }
            var venture = S.VentureAt(p.Id);
            if (venture != null || S.IsPlayerPropertyOccupied(p.Id))
            {
                Line(w.Content, S.PlaceTitle(p), 22, Ui.Accent);
                Para(w.Content, venture != null ? "这是你委托店长经营的门店。日常收支留在门店周转金中，可在资产页选择门店，再管理定价、维护和资金。" : "这处物业由中介托管出租，每月账期租金进入你的账本。", 18);
                Ui.Size(Ui.Button(w.Content, "管理这处资产", () => { if (venture != null) OpenBusinessSection(3, p.Id); else OpenCityProperty(p); }, 18, Ui.ButtonHot), -1, 42);
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
                    _ctrl.GoToPlace(PreferredAgency(p));
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

        private void BuildHardwareMarketNews(Window w)
        {
            Line(w.Content, "电脑配件行情", 18, Ui.Accent);
            string Factor(MacroFactor factor) => $"{HardwareMarket.FactorName(factor)} {S.Market.FactorValue(factor):+0.00;-0.00;0.00}";
            Line(w.Content, $"{Factor(MacroFactor.Crypto)} · {Factor(MacroFactor.AiDemand)}", 16, Ui.Dim);
            var header = Ui.Row(w.Content, 28);
            Ui.Size(Ui.Label(header, "配件", 16, color: Ui.Dim), -1, 28, 1);
            foreach (string label in new[] { "较常态报价", "库存", "在途" }) EconomicStoreCell(header, label, Ui.Dim);
            foreach (PartCategory category in PartCatalog.Categories)
            {
                var row = Ui.Row(w.Content, 30);
                Ui.Size(Ui.Label(row, PartCatalog.CategoryName(category), 17), -1, 30, 1);
                double change = (S.Market.Index(category) - 1) * 100;
                var parts = PartCatalog.All.Where(part => part.Category == category).ToArray();
                EconomicStoreCell(row, PriceMovement(change), PriceMovementColor(change));
                EconomicStoreCell(row, parts.Sum(part => S.PlayerShop.Stock.CountAll(part)).ToString(), Ui.TextColor);
                EconomicStoreCell(row, parts.Sum(part => S.PendingOnlineCount(part.Id)).ToString(), Ui.TextColor);
            }
            if (S.Player.Intelligence >= 30)
                Line(w.Content, "币圈主要影响显卡，AI 需求影响内存与显卡，新品上市压低旧 CPU 和主板。", 15, Ui.Dim);
            Line(w.Content, "硬件新闻", 18, Ui.Accent);
            foreach (NewsItem news in S.Market.News.Reverse().Take(20))
                Line(w.Content, $"<color=#ABB5A8>{news.Date}</color>  {news.Headline}", 17);
            if (S.Market.News.Count == 0) Line(w.Content, "暂无硬件新闻。", 16, Ui.Dim);
            BuildTipsSection(w);
        }

        // ---------- 菜单 ----------

        public void OpenSystemPanel()
        {
            OpenWindow(L("game_menu"), 620, 740, w =>
            {
                MenuTitle(w, "game_menu");
                Ui.Size(Ui.Button(w.Content, L("resume"), w.Close, 21, Ui.ButtonHot), -1, 50);
                Ui.Size(Ui.Button(w.Content, L(S.CanSave ? "save" : "cannot_save"), OpenSaveMenu, 20, Ui.ButtonBg, S.CanSave), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("load"), () => OpenLoadMenu(), 20, Ui.ButtonBg, SaveStore.Exists), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("settings"), OpenSettingsMenu, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("help"), OpenHelpMenu, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("about"), OpenAboutMenu, 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("return_title"), () => ShowMainMenu(true), 20), -1, 50);
                Ui.Size(Ui.Button(w.Content, L("quit"), OpenQuitMenu, 20, Ui.Warn), -1, 50);
            }, systemWindow: true);
        }

        private int _ledgerPeriod, _ledgerFilter, _ledgerPage;
        private int _ledgerDay;

        private void OpenLedger()
        {
            _ledgerDay = S.Clock.Today.TotalDays;
            _ledgerPage = 0;
            OpenWorkspace("ledger", "ledger", "现金流水", 1040, 880, w =>
            {
                BuildEventChangeRefunds(w);
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
                    Ui.Tab(periods, names[i], () => { _ledgerPeriod = period; _ledgerPage = 0; w.Rebuild(); }, i == _ledgerPeriod);
                }
                var dates = Ui.Row(w.Content, 42);
                Ui.Size(Ui.Button(dates, "上一" + (_ledgerPeriod == 0 ? "天" : _ledgerPeriod == 1 ? "周" : "月"), () =>
                { _ledgerDay = System.Math.Max(0, start - span); _ledgerPage = 0; w.Rebuild(); }, 17, Ui.ButtonBg, start > 0), 110);
                Ui.Size(Ui.Label(dates, span == 1 ? from.ToString() : from + " — " + to, 18, TextAnchor.MiddleCenter), -1, -1, 1);
                Ui.Size(Ui.Button(dates, "下一" + (_ledgerPeriod == 0 ? "天" : _ledgerPeriod == 1 ? "周" : "月"), () =>
                { _ledgerDay = start + span; _ledgerPage = 0; w.Rebuild(); }, 17, Ui.ButtonBg, start + span <= today), 110);
                Ui.Size(Ui.Button(dates, "回到当前", () => { _ledgerDay = today; _ledgerPage = 0; w.Rebuild(); }, 17), 110);

                var entries = S.Ledger.Entries.Where(e => e.Date >= from && e.Date <= to).ToList();
                var dailyIncome = new long[end - start + 1];
                var dailyExpense = new long[dailyIncome.Length];
                foreach (var entry in entries)
                {
                    int day = entry.Date.TotalDays - start;
                    if (entry.Amount.IsPositive) dailyIncome[day] += entry.Amount.Cents;
                    else dailyExpense[day] -= entry.Amount.Cents;
                }
                Money income = Money.FromCents(dailyIncome.Sum());
                Money expense = Money.FromCents(dailyExpense.Sum());
                Line(w.Content, $"总收入 {income}    总支出 {expense}    净收支 {income - expense}", 21, Ui.Accent);
                Line(w.Content, $"当前现金 {S.Ledger.Balance} · 含经营、个人收支、购置和借还款。", 16, Ui.Dim);
                var legend = Ui.Row(w.Content, 26);
                Ui.Size(Ui.Label(legend, "每日现金收支", 18), -1, 26, 1);
                Ui.Size(Ui.Label(legend, "● 收入", 15, TextAnchor.MiddleRight, Ui.Good), 74);
                Ui.Size(Ui.Label(legend, "● 支出  ·  元", 15, TextAnchor.MiddleRight, AssetTrendGraphic.ExpenseColor), 105);
                var chart = Ui.Size(Ui.Rect(w.Content, "LedgerCashTrend"), -1, 180);
                chart.gameObject.AddComponent<AssetTrendGraphic>().Initialize(dailyIncome, dailyExpense, start, compact: false, groupedBars: true);

                var filters = Ui.Row(w.Content, 36);
                string[] filterNames = { "全部明细", "只看收入", "只看支出" };
                for (int i = 0; i < filterNames.Length; i++)
                {
                    int filter = i;
                    Ui.Tab(filters, filterNames[i], () => { _ledgerFilter = filter; _ledgerPage = 0; w.Rebuild(); }, i == _ledgerFilter);
                }
                var filtered = entries.Where(e => _ledgerFilter == 0 || (_ledgerFilter == 1 ? e.Amount.IsPositive : e.Amount.IsNegative)).Reverse().ToList();
                const int pageSize = 40;
                int pages = System.Math.Max(1, (filtered.Count + pageSize - 1) / pageSize);
                _ledgerPage = System.Math.Min(_ledgerPage, pages - 1);
                var nav = Ui.Row(w.Content, 34);
                Ui.Size(Ui.Button(nav, "上一页", () => { _ledgerPage--; w.Rebuild(); }, 16, Ui.ButtonBg, _ledgerPage > 0), 100);
                Ui.Size(Ui.Label(nav, $"共 {filtered.Count} 笔 · 第 {_ledgerPage + 1}/{pages} 页", 16, TextAnchor.MiddleCenter), -1, -1, 1);
                Ui.Size(Ui.Button(nav, "下一页", () => { _ledgerPage++; w.Rebuild(); }, 16, Ui.ButtonBg, _ledgerPage + 1 < pages), 100);
                RectTransform list = Ui.Scroll(w.Content, 300);
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
                Ui.Size(Ui.Button(w.Content, L("load"), () => OpenLoadMenu(false), 20, Ui.ButtonBg, SaveStore.Exists), -1, 50);
            }, true, false);
        }

        public void ShowStartChoice() => ShowMainMenu();
    }
}
