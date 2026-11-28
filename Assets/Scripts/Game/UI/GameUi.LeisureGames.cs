using System;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Home;
using PCShopSell.Core.Session;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _leisureGameWindow;
        private int _managedPachinkoMachine;
        public bool LeisureGameInputActive => _leisureGameWindow != null && !_leisureGameWindow.Closed;

        public string PachinkoCabinetHint(int machine) => $"{machine + 1} 号机 · {PachinkoBoard.Machines[machine].Name}";

        public void OpenPachinkoMachine(int machine) => OpenPachinkoExchange(machine);

        private void OpenPachinkoExchange(int? machine = null)
        {
            var session = S;
            if (session.CurrentPlace?.Id != CityLifeCatalog.PachinkoVenue) return;
            var board = machine is int index ? PachinkoBoard.Machines[index] : null;
            string title = board == null ? session.CurrentPlace.Name + " · 换珠" : $"{machine + 1} 号机 · {board.Name}";
            bool selling = false;
            OpenWindow(title, 770, 490, w =>
            {
                var tradeBlocker = session.PachinkoTradeBlocker;
                if (board != null) Line(w.Content, board.Description, 17, Ui.Dim);
                var toolbar = Ui.Row(w.Content, 43, 8);
                Ui.Tab(toolbar, "买入", () => SelectMode(false), !selling, 110);
                Ui.Tab(toolbar, "卖出", () => SelectMode(true), selling, 110);
                var cash = Ui.Size(Ui.Label(toolbar, "", 17, TextAnchor.MiddleRight, Ui.Accent), 0, -1, 1);
                var sellAll = selling ? Ui.Size(Ui.Button(toolbar, "全部卖出…", null, 16, Ui.ButtonDanger), 132, 38) : null;
                var booking = board == null ? null : session.PendingVenueActivity(session.CurrentPlace);
                string playBlocker = board == null ? null
                    : booking != null && CityLifeCatalog.LeisureService(booking) != LeisureGame.Pachinko ? "请先完成当前的预约。"
                    : booking?.ServiceConfirmed == true ? session.VenueAppointmentStartBlocker(booking)
                    : session.VenueServiceBlocker(CityLifeCatalog.PachinkoService, 0, booking?.Id);
                Button play = null;

                var heading = Ui.Row(w.Content, 32, 8);
                Ui.Size(Ui.Label(heading, "弹珠", 16, color: Ui.Dim), 0, -1, 1);
                Ui.Size(Ui.Label(heading, "持有", 16, TextAnchor.MiddleCenter, Ui.Dim), 104);
                Ui.Size(Ui.Label(heading, selling ? "回收单价 / 回收比例" : "买入单价", 16, TextAnchor.MiddleCenter, Ui.Dim), 190);
                Ui.Size(Ui.Label(heading, "交易", 16, TextAnchor.MiddleCenter, Ui.Dim), 136);
                var tiers = GameSession.PachinkoTiers;
                var rows = new (Text Count, Button Trade)[tiers.Length];
                for (int i = 0; i < tiers.Length; i++)
                {
                    int tier = i;
                    var kind = tiers[i];
                    var row = Ui.Row(w.Content, 45, 8);
                    string tint = ColorUtility.ToHtmlStringRGB(LeisureBoardGraphic.PachinkoBeadColor(i));
                    Ui.Size(Ui.Label(row, $"<color=#{tint}>●</color> {kind.Name}", 19), 0, -1, 1);
                    var count = Ui.Size(Ui.Label(row, "", 18, TextAnchor.MiddleCenter), 104);
                    string price = selling ? $"{Money.FromYuan(kind.Sell)} / {(double)kind.Sell / kind.Buy:P0}" : Money.FromYuan(kind.Buy).ToString();
                    Ui.Size(Ui.Label(row, price, 17, TextAnchor.MiddleCenter, Ui.Accent), 190);
                    var trade = Ui.Size(Ui.Button(row, selling ? "卖 1 珠" : "买 1 珠", null, 17, Ui.ButtonHot), 136, 42);
                    var hold = trade.gameObject.AddComponent<HoldRepeat>();
                    hold.Owner = w;
                    hold.Key = $"Pachinko:{selling}:{tier}";
                    hold.Commit = _ =>
                    {
                        if (S != session || w.Closed) return;
                        if (selling) session.SellPachinkoBeads(tier);
                        else session.BuyPachinkoBead(tier);
                        RefreshTrade();
                    };
                    trade.onClick.AddListener(hold.ClickOnce);
                    rows[i] = (count, trade);
                }

                var actions = Ui.Row(w.Content, 47, 8);
                Ui.Size(Ui.Label(actions, "点按交易 1 珠，长按连续交易", 16, color: Ui.Dim), 0, -1, 1);
                if (machine is int selectedMachine) play = Ui.Size(Ui.Button(actions, "开始游玩", () =>
                {
                    if (S != session || !session.StartPachinkoMachine(selectedMachine)) return;
                    w.Close();
                    OpenLeisurePlay();
                }, 18, Ui.ButtonHot), 136, 44);
                if (sellAll != null) sellAll.onClick.AddListener(ConfirmSellAll);
                if (tradeBlocker != null) Para(w.Content, tradeBlocker, 16, Ui.Warn);
                if (playBlocker != null && playBlocker != tradeBlocker) Para(w.Content, playBlocker, 16, Ui.Warn);
                RefreshTrade();

                void SelectMode(bool sell)
                {
                    if (selling == sell) return;
                    HoldRepeat.CancelActiveHold();
                    selling = sell;
                    w.Rebuild();
                }

                void RefreshTrade()
                {
                    cash.text = $"现金 {session.Ledger.Balance}";
                    bool canTrade = session.PachinkoTradeBlocker == null;
                    for (int tier = 0; tier < rows.Length; tier++)
                    {
                        int count = session.PachinkoBeadCount(tier);
                        rows[tier].Count.text = $"{count:N0} 珠";
                        rows[tier].Trade.interactable = canTrade && (selling ? count > 0
                            : session.PachinkoBeadSpace(tier) > 0 && session.Ledger.CanAfford(Money.FromYuan(tiers[tier].Buy)));
                    }
                    if (sellAll != null) sellAll.interactable = canTrade && session.HasPachinkoBeads;
                    if (play != null) play.interactable = playBlocker == null && session.HasPachinkoBeads;
                }

                void ConfirmSellAll()
                {
                    if (S != session || !selling || !session.HasPachinkoBeads || session.PachinkoTradeBlocker != null) return;
                    HoldRepeat.CancelActiveHold();
                    OpenWindow("全部卖出弹珠", 520, 232, confirm =>
                    {
                        int count = Enumerable.Range(0, tiers.Length).Sum(session.PachinkoBeadCount);
                        Para(confirm.Content, $"卖出持有的全部 {count:N0} 珠，可收回 {session.PachinkoBeadValue}。", 18);
                        var buttons = Ui.Row(confirm.Content, 44, 8);
                        Ui.Size(Ui.Button(buttons, "取消", confirm.Close, 17, Ui.ButtonSecondary), 0, -1, 1);
                        Ui.Size(Ui.Button(buttons, "确认全部卖出", () =>
                        {
                            if (S == session && !w.Closed && session.SellPachinkoBeads(null)) RefreshTrade();
                            confirm.Close();
                        }, 17, Ui.ButtonDanger), 0, -1, 1);
                    }, fitContent: true);
                }
            }, fitContent: true);
        }

        private void MaybeResumeLeisure()
        {
            if (!LeisureGameInputActive && !AnyModalOpen && !OpeningMealRestricted && S.LiveLeisure != null)
                OpenLeisurePlay();
        }

        private bool TryOpenLeisureVenue(Place place, string activityId)
        {
            var kind = CityLifeCatalog.LeisureAt(place.Id);
            if (kind == LeisureGame.None) return false;
            bool here = S.CurrentPlace?.Id == place.Id;
            if (here && S.LiveLeisure != null) { OpenLeisurePlay(); return true; }
            if (here) S.PrepareVenueCompanions(place, activityId);
            if (here && kind == LeisureGame.Pachinko)
            {
                CloseAllWindows();
                return true;
            }

            bool community = S.HasVenueSponsorship(place);
            var sponsor = new VenueSponsorView();
            OpenWindow(place.Name, 980, community ? 620 : kind == LeisureGame.Cinema ? 560 : 510, w =>
            {
                BuildVenueSponsorHeader(w, place, sponsor);
                if (BuildVenueSponsorPage(w, place, sponsor, w.Content)) return;
                float bodyHeight = _venueWalkInOrder?.Window == w ? 440 : kind == LeisureGame.Cinema ? 320 : 270;
                var goods = here ? BuildRosterFrame(w, place, bodyHeight) : w.Content;
                if (BuildVenueWalkIn(w, goods)) return;
                var booking = here ? S.PendingVenueActivity(place, activityId) : null;
                var services = booking != null ? new[] { S.CityActivityService(booking) }
                    : CityLifeCatalog.Activities.Where(a => !a.EventOnly && a.VenueId == place.Id).ToArray();
                var friend = booking?.Participants.FirstOrDefault();
                if (friend != null)
                    PersonSummary(goods, friend.PersonId, S.CityPerson(friend.PersonId)?.Name ?? "同行者", "今天与你相约");
                else
                    Line(goods, CityPlaceHours(place), 19, Ui.Dim);

                foreach (var service in services.Where(s => s != null))
                {
                    if (kind == LeisureGame.Pachinko)
                    {
                        Para(goods, "走到任意机台旁按 F 换珠或游玩。三台都支持各档弹珠。", 18, Ui.Dim);
                        continue;
                    }
                    bool confirmed = booking?.ServiceConfirmed == true;
                    bool canInvite = here && booking == null && service.WalkInCapacity(0) > 1;
                    string blocker = !here ? "到场后开始" : confirmed
                        ? S.VenueAppointmentStartBlocker(booking) : S.VenueServiceBlocker(service.Id, 0, booking?.Id,
                            walkInCompanions: canInvite ? Array.Empty<string>() : null, checkPrice: !canInvite);
                    var cost = S.CityActivityEntryPrice(service, confirmed ? booking.ServiceChoice : 0, booking);
                    string price = kind switch
                    {
                        LeisureGame.Poker => $"换筹码 ¥{GameSession.PokerBuyInYuan}",
                        LeisureGame.Claw => $"投币 ¥{GameSession.ClawPriceYuan}/次",
                        _ => cost.IsPositive ? cost + (canInvite ? "／人" : "") : "免费"
                    };
                    string title = kind == LeisureGame.Cinema ? service.Choices[0] : service.Name;
                    bool showBlocker = here && blocker != null;
                    string effects = VenuePlayerEffects(service.Service(confirmed ? booking.ServiceChoice : 0),
                        canInvite || booking?.Participants.Length > 0, compact: true);
                    var row = Ui.Row(goods, showBlocker ? 72 : 58, 9);
                    string label = $"<b>{title}</b>　<size=15>{ActivityDescriptionText($"{service.Minutes} 分钟")}"
                        + (effects.Length == 0 ? "" : "\n" + effects)
                        + (showBlocker ? "\n" + ActivityDescriptionText(blocker) : "") + "</size>";
                    Ui.Size(Ui.Label(row, label, 21), 0, -1, 1);
                    Ui.Size(Ui.Label(row, price, 18, TextAnchor.MiddleRight, Ui.Accent), 141, 44);
                    string caption = kind == LeisureGame.Cinema ? canInvite ? "选择这场" : "购票入场" : service.Choices[0];
                    Ui.Size(Ui.Button(row, caption, () =>
                    {
                        if (canInvite)
                        {
                            SelectVenueWalkIn(w, service, 0, EnterPlay);
                            return;
                        }
                        bool started = confirmed ? S.StartVenueAppointment(booking.Id)
                            : S.SelectVenueService(service.Id, 0, booking?.Id);
                        if (started) EnterPlay();
                        else w.Rebuild();
                    }, 17, Ui.ButtonHot, blocker == null).Tip(blocker), 160, 38).name = "Leisure_" + service.Id;

                    void EnterPlay()
                    {
                        if (S.LiveLeisure != null) { w.Close(); OpenLeisurePlay(); }
                        else w.Rebuild();
                    }
                }

                if (here)
                    Ui.Size(Ui.Button(goods, "返回街上", () => { w.Close(); _ctrl.LeaveCurrentPlace(); }, 17, Ui.ButtonSecondary), -1, 39);
                else
                    Ui.Size(Ui.Button(w.Content, "前往" + place.Name, () =>
                    {
                        CloseAllWindows();
                        _ctrl.GoToPlace(place);
                    }, 18, Ui.ButtonHot, S.ClosedReason(place) == null).Tip(S.ClosedReason(place)), -1, 40);
            }, fitContent: !community);
            return true;
        }

        private void BuildPachinkoMachineSettings(Transform parent, Window window, VenueBusiness shop)
        {
            int machine = _managedPachinkoMachine;
            int draft = S.PachinkoPayoutForMachine(machine);
            var row = Ui.Row(parent, 37, 8);
            Ui.Size(Ui.Label(row, "机台返奖", 18, color: Ui.Accent), 82);
            Ui.Size(Ui.Choice(row, new[] { "1 号机", "2 号机", "3 号机" }, machine, i =>
            {
                _managedPachinkoMachine = i;
                window.Rebuild();
            }), 107, 33);
            var value = Ui.Size(Ui.Label(row, "", 17), 0, 33, 1);
            void Refresh() => value.text = $"{draft} 珠 / 次 · 预计返奖 {S.PachinkoCustomerReturnRate(draft, shop.PriceCents):P0}";
            Refresh();
            var adjust = Ui.Row(parent, 38, 8);
            Ui.Size(Ui.Slider<Slider>(adjust, GameSession.PachinkoMinimumPayout, GameSession.PachinkoMaximumPayout, draft,
                amount => { draft = Mathf.RoundToInt(amount); Refresh(); }, wholeNumbers: true), 0, 29, 1);
            Ui.Size(Ui.Button(adjust, "应用设置", () =>
            {
                if (S.ConfigurePachinkoMachine(machine, draft)) window.Rebuild();
            }, 17, Ui.ButtonHot), 104, 35);
            Line(parent, $"今日进账 {Money.FromCents(shop.RevenueCents)} · 返奖 {Money.FromCents(shop.PachinkoPaidCents)}", 16, Ui.Dim);
        }

        private void OpenLeisurePlay()
        {
            if (LeisureGameInputActive || S.LiveLeisure == null) return;
            if (S.LiveLeisure.Game == LeisureGame.Cinema)
            {
                S.FinishLeisure();
                return;
            }

            var state = S.LiveLeisure;
            var venue = S.CurrentPlace;
            bool exitAfterHand = false;
            bool exitAfterBeads = false;
            GiftClawCabinet cabinet = null;
            bool pointerHeld = false;
            CloseAllWindows();
            _leisureGameWindow = OpenWindow(venue.Name, 1920, 1080, w =>
            {
                var content = w.Content;
                var background = Ui.Panel(content, "LeisureBackground", Ui.ChromeWell).rectTransform;
                background.Fill();
                string title = state.Pachinko is PachinkoPlay machine ? $"{machine.Machine + 1} 号机 · {machine.Board.Name}" : venue.Name;
                var header = BuildLeisureHeader(w, title, "结束游玩", () =>
                {
                    if (state.Poker?.HandActive == true) { exitAfterHand = true; return; }
                    if (state.Pachinko?.Balls.Count > 0) { exitAfterBeads = true; return; }
                    Finish();
                });
                var exit = header.GetComponentInChildren<Button>();
                var exitText = exit.GetComponentInChildren<Text>();
                var stage = Ui.FlatPanel(content, "LeisureStage", Ui.ChromeWell).rectTransform.Anchor(.018f, .034f, .742f, .902f);
                var panel = Ui.FlatPanel(content, "LeisureControls", Ui.Chrome).rectTransform.Anchor(.753f, .034f, .982f, .902f);
                var controls = Ui.Rect(panel, "ControlStack").Anchor(.045f, .025f, .955f, .976f);
                Ui.VList(controls, 10, 0);
                var layout = controls.GetComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childForceExpandHeight = false;
                Func<bool, bool> readBilliardsCharge = null;
                ProcessControls();

                void Finish()
                {
                    if (!S.FinishLeisure()) return;
                    if (!w.Closed) w.Close();
                }

                Text Readout(string caption, int height = 41, Color? ink = null)
                {
                    var label = Ui.Label(controls, caption, 21, color: ink ?? Ui.TextColor);
                    return Ui.Size(label, -1, height);
                }

                Button Hold(string title)
                {
                    var button = Ui.Size(Ui.Button(controls, title, null, 22, Ui.ButtonHot), -1, 64);
                    button.gameObject.AddComponent<FishingHoldInput>().Held = value => pointerHeld = value;
                    return button;
                }

                void ProcessControls()
                {
                    Action draw = state.Game switch
                    {
                        LeisureGame.Pachinko => BuildPachinko(),
                        LeisureGame.Poker => BuildPokerTable(stage, controls),
                        LeisureGame.Claw => BuildClaw(),
                        _ => BuildBilliards()
                    };
                    w.RefreshContent = () =>
                    {
                        if (S.LiveLeisure != state) { w.Close(); return; }
                        bool focused = _windows.LastOrDefault() == w && Application.isFocused;
                        bool held = readBilliardsCharge?.Invoke(focused) ?? (pointerHeld || Input.GetKey(KeyCode.Space));
                        if (focused)
                        {
                            if (state.Pachinko != null)
                            {
                                if (Input.GetKeyDown(KeyCode.Escape)) exitAfterBeads = true;
                                S.AimPachinko(DirectionalInput.Horizontal * Math.Min(.06, Time.unscaledDeltaTime) * 1.6);
                            }
                            S.StepLeisure(Time.unscaledDeltaTime, !exitAfterBeads && held);
                        }
                        if (exitAfterHand && state.Poker?.HandActive == false) { Finish(); return; }
                        if (exitAfterBeads && S.CanFinishLeisure) { Finish(); return; }
                        exit.interactable = !exitAfterBeads && (state.Poker?.HandActive == true || state.Pachinko != null || S.CanFinishLeisure);
                        exitText.text = exitAfterBeads ? "弹珠落定后离开"
                            : state.Poker?.HandActive == true ? exitAfterHand ? "本手结束后离桌" : "打完这手离桌" : "结束游玩";
                        draw();
                    };
                    draw();
                }

                Action BuildPachinko()
                {
                    var p = state.Pachinko;
                    var board = MakeLeisureBoard(stage, 1.13f);
                    board.Pachinko = p;
                    foreach (var slot in p.Board.Slots)
                    {
                        var label = Ui.Label(board.rectTransform, $"+{slot.Reward}", 24, TextAnchor.MiddleCenter, Ui.ChromeGold);
                        float center = .12f + .76f * (float)slot.X;
                        label.rectTransform.Anchor(center - .049f, .079f, center + .049f, .124f);
                        label.raycastTarget = false;
                    }
                    var counts = Readout("", 48, Ui.ChromeInk);
                    Readout("选择发射的弹珠", 28, Ui.ChromeGold);
                    var tierLabels = new Text[GameSession.PachinkoTiers.Length];
                    var tierNames = new string[tierLabels.Length];
                    for (int i = 0; i < tierLabels.Length; i++)
                    {
                        int tier = i;
                        string tint = ColorUtility.ToHtmlStringRGB(LeisureBoardGraphic.PachinkoBeadColor(i));
                        tierNames[i] = $"<color=#{tint}>●</color> {GameSession.PachinkoTiers[i].Name}";
                        var button = Ui.Size(Ui.Button(controls, "", () => S.SelectPachinkoTier(tier), 18, Ui.ChromeButton), -1, 42);
                        tierLabels[i] = button.GetComponentInChildren<Text>();
                    }
                    Readout("A/D 或 ←/→ 转动发射口", 38, Ui.ChromeInk);
                    var aim = Ui.Row(controls, 45, 8);
                    Ui.Size(Ui.Button(aim, "向左", () => S.AimPachinko(-.11), 18, Ui.ChromeButton), 0, 43, 1);
                    Ui.Size(Ui.Button(aim, "向右", () => S.AimPachinko(.11), 18, Ui.ChromeButton), 0, 43, 1);
                    var status = Readout("", 54, Ui.ChromeGold);
                    var spacer = Ui.Size(Ui.Rect(controls, "ActionSpace"), -1, 0);
                    spacer.GetComponent<LayoutElement>().flexibleHeight = 1;
                    var fire = Hold("发射 · 空格 / 鼠标");
                    return () =>
                    {
                        board.SetVerticesDirty();
                        counts.text = $"已发射 {p.Launched} · 返珠 {p.Returned}";
                        for (int i = 0; i < tierLabels.Length; i++)
                            tierLabels[i].text = $"{tierNames[i]} · {S.PachinkoBeadCount(i)} 珠"
                                + (p.Tier == i ? "　✓" : "");
                        status.text = p.RewardFlash > 0 ? p.LastReward > 0 ? $"入槽 · 返 {p.LastReward} 珠" : "落空"
                            : !S.HasPachinkoBeads && p.Balls.Count == 0 ? "弹珠已用完，可结束游玩后补珠。" : "";
                        fire.interactable = !exitAfterBeads && S.PachinkoBeadCount(p.Tier) > 0;
                    };
                }

                Action BuildBilliards()
                {
                    var pool = state.Billiards;
                    var board = MakeLeisureBoard(stage, 1.84f);
                    board.Billiards = pool;
                    var score = Readout("", 65);
                    var message = Readout("", 65, Ui.Accent);
                    var power = Readout("");
                    Readout("移动鼠标瞄准\n按住左键或空格蓄力，松手出杆", 70, Ui.ChromeInk);
                    var spacer = Ui.Size(Ui.Rect(controls, "ActionSpace"), -1, 0);
                    spacer.GetComponent<LayoutElement>().flexibleHeight = 1;
                    var fire = Hold("按住蓄力，松手出杆");
                    bool tableHeld = false;
                    Vector3 previous = Input.mousePosition;
                    readBilliardsCharge = focused =>
                    {
                        if (!focused)
                        {
                            S.CancelBilliardsCharge();
                            tableHeld = pointerHeld = false;
                            return false;
                        }
                        var mouse = Input.mousePosition;
                        bool leftPressed = Input.GetMouseButtonDown(0);
                        bool leftHeld = Input.GetMouseButton(0);
                        // Freeze the cue direction from the press through the release frame.
                        if (!tableHeld && pool.CanShoot && (mouse != previous || leftPressed)
                            && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            board.rectTransform, mouse, null, out var local))
                        {
                            var rect = board.rectTransform.rect;
                            if (rect.Contains(local))
                            {
                                var pos = Rect.PointToNormalized(rect, local);
                                var (x, y) = LeisureBoardGraphic.ToTable(pos);
                                S.AimBilliards(x, y);
                                tableHeld = leftPressed;
                            }
                        }
                        previous = mouse;
                        tableHeld &= leftHeld;
                        return tableHeld || pointerHeld && leftHeld || Input.GetKey(KeyCode.Space);
                    };
                    return () =>
                    {
                        board.SetVerticesDirty();
                        score.text = $"你 {pool.PlayerScore} : {pool.OpponentScore} {pool.OpponentName}\n先打进 3 球获胜";
                        message.text = pool.Message;
                        power.text = $"力度 {pool.Power:P0}";
                        fire.interactable = pool.CanShoot;
                    };
                }

                Action BuildClaw()
                {
                    var claw = state.Claw;
                    cabinet?.Dispose();
                    cabinet = new GiftClawCabinet();
                    var image = Ui.Rect(stage, "CabinetImage").Fill().gameObject.AddComponent<RawImage>();
                    image.texture = cabinet.Texture;
                    image.raycastTarget = false;
                    var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
                    fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    fitter.aspectRatio = (float)cabinet.Texture.width / cabinet.Texture.height;
                    var message = Readout("", 74, Ui.ChromeGold);
                    var counts = Readout("", 48, Ui.ChromeInk);
                    Readout("A/D 或 ←/→ 左右对准\n空格投币，再按空格落爪", 67, Ui.ChromeInk);
                    var lateral = Ui.Row(controls, 47, 8);
                    var left = Ui.Size(Ui.Button(lateral, "← 左移", () => S.MoveClaw(-.13), 17, Ui.ChromeButton), 0, 44, 1);
                    var right = Ui.Size(Ui.Button(lateral, "右移 →", () => S.MoveClaw(.13), 17, Ui.ChromeButton), 0, 44, 1);
                    var freight = S.CourierQuote(toShop: false).fee;
                    Readout($"装不下会送到家，新叫车 {freight}\n付不起运费则放弃这次的奖品", 70, Ui.Dim);
                    var spacer = Ui.Size(Ui.Rect(controls, "ActionSpace"), -1, 0);
                    spacer.GetComponent<LayoutElement>().flexibleHeight = 1;
                    int lastPressFrame = -1;
                    string insertCaption = $"投币 · ¥{GameSession.ClawPriceYuan}";
                    var grab = Ui.Size(Ui.Button(controls, insertCaption, PressClaw, 22, Ui.ButtonHot), -1, 60);
                    BuildPartnerActivityChoices(w, S.RunningCityActivity, controls, "继续游玩");
                    var grabCaption = grab.GetComponentInChildren<Text>();
                    return () =>
                    {
                        if (_windows.LastOrDefault() == w && Application.isFocused)
                        {
                            float lateralInput = DirectionalInput.Horizontal;
                            if (!claw.Animating && lateralInput != 0)
                                S.MoveClaw(lateralInput * Math.Min(Time.unscaledDeltaTime, .08f) * 1.12);
                            if (Input.GetKeyDown(KeyCode.Space)) PressClaw();
                        }
                        cabinet.Render(claw);
                        message.text = claw.Message;
                        counts.text = $"已抓 {claw.Attempts} 次 · 获得 {claw.PrizesWon} 件";
                        left.interactable = right.interactable = !claw.Animating;
                        grabCaption.text = claw.Sweeping ? "现在落爪" : claw.Animating ? "抓取中" : insertCaption;
                        grab.interactable = claw.Sweeping || (!claw.Animating && claw.HasGifts
                            && S.Ledger.CanAfford(Money.FromYuan(GameSession.ClawPriceYuan)));
                    };

                    void PressClaw()
                    {
                        if (lastPressFrame == Time.frameCount) return;
                        lastPressFrame = Time.frameCount;
                        S.PressClawButton();
                    }
                }
            }, holds: false, closable: false, onClosed: () =>
            {
                if (state.Billiards != null && S?.LiveLeisure == state) S.CancelBilliardsCharge();
                cabinet?.Dispose();
                _leisureGameWindow = null;
            }, scrollContent: false, showTitleBar: false, fullScreen: true);
        }

        private static LeisureBoardGraphic MakeLeisureBoard(Transform parent, float aspect)
        {
            var rect = Ui.Rect(parent, "GameBoard").Fill();
            var fit = rect.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = aspect;
            var graphic = rect.gameObject.AddComponent<LeisureBoardGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private Action BuildPokerTable(Transform stage, Transform controls)
        {
            var state = S.LiveLeisure.Poker;
            var cloth = Ui.Panel(stage, "PokerTable", ArtDirection.Hex("355D4A")).rectTransform
                .Anchor(.15f, .25f, .85f, .79f);
            var pot = Ui.Label(cloth, "", 25, TextAnchor.MiddleCenter, Ui.ChromeGold);
            pot.rectTransform.Anchor(.17f, .68f, .83f, .90f);
            var publicCards = new Text[5];
            for (int i = 0; i < publicCards.Length; i++)
            {
                float left = .225f + .112f * i;
                publicCards[i] = PokerCard(cloth, left, .28f, left + .092f, .66f);
            }

            var seats = new Text[4];
            var pairs = new Text[8];
            BuildSeat(0, .355f, .020f, .645f, .225f);
            BuildSeat(1, .008f, .413f, .185f, .654f);
            BuildSeat(2, .355f, .797f, .645f, .993f);
            BuildSeat(3, .815f, .413f, .992f, .654f);
            var status = Ui.Size(Ui.Label(controls, "", 24, color: Ui.Accent), -1, 70);
            var count = Ui.Size(Ui.Label(controls, "", 22), -1, 63);
            var row = Ui.Row(controls, 50, 8);
            var fold = Ui.Size(Ui.Button(row, "弃牌", () => S.PokerMove(PokerAction.Fold), 18, Ui.ButtonSecondary), 0, 47, 1);
            var call = Ui.Size(Ui.Button(row, "", () => S.PokerMove(PokerAction.Call), 18, Ui.ButtonHot), 0, 47, 1);
            bool editingRaise = false;
            var raise = Ui.Size(Ui.Button(controls, "加注…", () => editingRaise = true, 18), -1, 43);
            var raisePanel = Ui.Rect(controls, "RaiseControls");
            Ui.Size(raisePanel, -1, 156);
            Ui.VList(raisePanel, 8, 0);
            var raiseLabel = Ui.Size(Ui.Label(raisePanel, "", 21, color: Ui.Accent), -1, 40);
            int amount = 0, lastMin = -1, lastMax = -1;
            var slider = Ui.Size(Ui.Slider(raisePanel, 20, 400, 40, value => amount = Mathf.RoundToInt(value)), -1, 36);
            slider.wholeNumbers = true;
            var raiseActions = Ui.Row(raisePanel, 47, 7);
            Ui.Size(Ui.Button(raiseActions, "取消", () => editingRaise = false, 17, Ui.ButtonSecondary), 0, 41, 1);
            Ui.Size(Ui.Button(raiseActions, "确认加注", () =>
            {
                if (S.PokerMove(PokerAction.Raise, amount)) editingRaise = false;
            }, 18, Ui.ButtonHot), 0, 41, 1);
            var next = Ui.Size(Ui.Button(controls, "再打一手", S.PokerNextHand, 21, Ui.ButtonHot), -1, 54);
            return () =>
            {
                bool turn = state.PlayerTurn;
                pot.text = $"底池 ¥{(state.HandActive ? state.Pot : state.LastPot)}";
                for (int i = 0; i < publicCards.Length; i++) Display(publicCards[i], state.Community[i], false);
                for (int i = 0; i < seats.Length; i++)
                {
                    var player = state.Players[i];
                    string dealer = i == state.Dealer ? " · 庄位" : "";
                    seats[i].text = $"{player.Name}{dealer}\n¥{player.Stack}　{player.Action}";
                    seats[i].color = i == state.Turn ? Ui.ChromeGold : Ui.ChromeInk;
                    bool show = i == 0 || state.Showdown && !player.Folded;
                    bool hide = i != 0 && player.Folded;
                    Display(pairs[i * 2], hide ? -1 : player.First, !show);
                    Display(pairs[i * 2 + 1], hide ? -1 : player.Second, !show);
                }
                status.text = turn ? "轮到你" : state.Message;
                count.text = $"小盲 ¥{PokerPlay.SmallBlind} / 大盲 ¥{PokerPlay.BigBlind}\n你的筹码 ¥{state.Players[0].Stack}";
                row.gameObject.SetActive(turn);
                fold.interactable = turn;
                call.GetComponentInChildren<Text>().text = state.ToCall == 0 ? "过牌" : $"跟注 ¥{state.ToCall}";
                if (!state.CanRaise) editingRaise = false;
                raise.gameObject.SetActive(state.CanRaise && !editingRaise);
                raisePanel.gameObject.SetActive(state.CanRaise && editingRaise);
                if (state.CanRaise && editingRaise)
                {
                    int min = state.MinimumRaise, max = state.MaximumRaise;
                    if (lastMin != min || lastMax != max)
                    {
                        slider.minValue = min;
                        slider.maxValue = max;
                        amount = min;
                        slider.SetValueWithoutNotify(amount);
                        lastMin = min; lastMax = max;
                    }
                    slider.interactable = max > min;
                    raiseLabel.text = $"加注到 ¥{amount}\n<size=17>本次再投入 ¥{amount - state.Players[0].StreetBet}</size>";
                }
                else lastMin = -1;
                next.gameObject.SetActive(state.CanPlayNextHand);
            };

            void BuildSeat(int i, float left, float bottom, float right, float top)
            {
                var seat = Ui.Rect(stage, "PokerSeat").Anchor(left, bottom, right, top);
                seats[i] = Ui.Label(seat, "", 20, TextAnchor.MiddleCenter, Ui.ChromeInk);
                seats[i].rectTransform.Anchor(0, .55f, 1, 1);
                pairs[i * 2] = PokerCard(seat, .31f, .04f, .48f, .56f);
                pairs[i * 2 + 1] = PokerCard(seat, .52f, .04f, .69f, .56f);
            }

            Text PokerCard(Transform parent, float l, float b, float r, float t)
            {
                var card = Ui.FlatPanel(parent, "Card", Ui.PanelBg).rectTransform.Anchor(l, b, r, t);
                var label = Ui.Label(card, "", 25, TextAnchor.MiddleCenter, Ui.TextColor);
                label.rectTransform.Fill();
                return label;
            }

            void Display(Text label, int id, bool back)
            {
                label.transform.parent.gameObject.SetActive(id >= 0);
                if (id < 0) return;
                string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
                string[] suits = { "♣", "♦", "♥", "♠" };
                int suit = id / 13;
                label.color = back ? Ui.Accent : suit is 1 or 2 ? Ui.Warn : Ui.TextColor;
                label.text = back ? "▧" : $"{ranks[id % 13]}\n{suits[suit]}";
            }
        }
    }
}
