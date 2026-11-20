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

            OpenWindow(place.Name, 980, kind == LeisureGame.Cinema ? 560 : 510, w =>
            {
                var booking = here ? S.PendingVenueActivity(place, activityId) : null;
                var services = booking != null ? new[] { S.CityActivityService(booking) }
                    : CityLifeCatalog.Activities.Where(a => !a.EventOnly && a.VenueId == place.Id).ToArray();
                var friend = booking?.Participants.FirstOrDefault();
                if (friend != null)
                    PersonSummary(w.Content, friend.PersonId, S.CityPerson(friend.PersonId)?.Name ?? "同行者", "今天与你相约");
                else
                    Line(w.Content, CityPlaceHours(place), 19, Ui.Dim);

                foreach (var service in services.Where(s => s != null))
                {
                    bool confirmed = booking?.ServiceConfirmed == true;
                    string blocker = !here ? "到场后开始" : confirmed
                        ? S.VenueAppointmentStartBlocker(booking) : S.VenueServiceBlocker(service.Id, 0, booking?.Id);
                    var cost = S.CityActivityEntryPrice(service, confirmed ? booking.ServiceChoice : 0, booking);
                    string price = kind switch
                    {
                        LeisureGame.Poker => $"换筹码 ¥{GameSession.PokerBuyInYuan}",
                        LeisureGame.Claw => $"投币 ¥{GameSession.ClawPriceYuan}/次",
                        _ => cost.IsPositive ? cost.ToString() : "免费"
                    };
                    string title = kind == LeisureGame.Cinema ? service.Choices[0] : service.Name;
                    var row = Ui.Row(w.Content, 51, 9);
                    Ui.Size(Ui.Label(row, $"<b>{title}</b>　<size=16>{service.Minutes} 分钟</size>", 21), 0, 46, 1);
                    Ui.Size(Ui.Label(row, price, 18, TextAnchor.MiddleRight, Ui.Accent), 141, 44);
                    string caption = kind == LeisureGame.Cinema ? "购票入场" : service.Choices[0];
                    Ui.Size(Ui.Button(row, caption, () =>
                    {
                        bool started = confirmed ? S.StartVenueAppointment(booking.Id)
                            : S.SelectVenueService(service.Id, 0, booking?.Id);
                        if (started && S.LiveLeisure != null) { w.Close(); OpenLeisurePlay(); }
                        else w.Rebuild();
                    }, 17, Ui.ButtonHot, blocker == null).Tip(blocker), 160, 38).name = "Leisure_" + service.Id;
                }

                if (here && kind == LeisureGame.Pachinko && !S.IsOwnedVenue(place))
                {
                    string blocker = S.PachinkoPurchaseBlocker;
                    string cost = S.PropertyAt(place.Id) == null ? " · " + GameSession.PachinkoTransferPrice : "";
                    Ui.Size(Ui.Button(w.Content, "接手柏青哥店" + cost, () =>
                    {
                        ConfirmAction("接手柏青哥店",
                            "接手后可经营原有机台，并在「经营 → 店」调整每台机的返奖。",
                            "确认接手", () =>
                            {
                                if (S.PurchasePachinko()) { w.Close(); OpenVenueManagement(place); }
                                else w.Rebuild();
                            });
                    }, 17, Ui.ButtonBg, blocker == null).Tip(blocker), -1, 39);
                }
                if (here)
                    Ui.Size(Ui.Button(w.Content, "返回街上", () => { w.Close(); _ctrl.LeaveCurrentPlace(); }, 17), -1, 39);
                else
                    Ui.Size(Ui.Button(w.Content, "前往" + place.Name, () =>
                    {
                        CloseAllWindows();
                        _ctrl.GoToPlace(place);
                    }, 18, Ui.ButtonHot, S.ClosedReason(place) == null).Tip(S.ClosedReason(place)), -1, 40);
            });
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
                string cinemaId = S.LiveLeisure.ActivityId;
                if (S.FinishLeisure()) OpenCityActivitySettlement(cinemaId);
                return;
            }

            var state = S.LiveLeisure;
            var venue = S.CurrentPlace;
            bool exitAfterHand = false;
            GiftClawCabinet cabinet = null;
            bool pointerHeld = false;
            CloseAllWindows();
            _leisureGameWindow = OpenWindow(venue.Name, 1920, 1080, w =>
            {
                var content = w.Content;
                var background = Ui.Panel(content, "LeisureBackground", Ui.ChromeWell).rectTransform;
                background.Fill();
                var header = BuildLeisureHeader(w, venue.Name, "结束游玩", () =>
                {
                    if (state.Poker?.HandActive == true) { exitAfterHand = true; return; }
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
                ProcessControls();

                void Finish()
                {
                    if (!S.FinishLeisure()) return;
                    if (!w.Closed) w.Close();
                    OpenCityActivitySettlement(state.ActivityId);
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
                        if (focused) S.StepLeisure(Time.unscaledDeltaTime, pointerHeld || Input.GetKey(KeyCode.Space));
                        if (exitAfterHand && state.Poker?.HandActive == false) { Finish(); return; }
                        exit.interactable = state.Poker?.HandActive == true || S.CanFinishLeisure;
                        exitText.text = state.Poker?.HandActive == true ? exitAfterHand ? "本手结束后离桌" : "打完这手离桌" : "结束游玩";
                        draw();
                    };
                    draw();
                }

                Action BuildPachinko()
                {
                    var p = state.Pachinko;
                    var board = MakeLeisureBoard(stage, 1.13f);
                    board.Pachinko = p;
                    var counts = Readout("", 70);
                    Readout("发射力度", 32, Ui.Accent);
                    Ui.Size(Ui.Slider(controls, 0, 1, (float)p.Power, value => S.AimPachinko(value)), -1, 35);
                    var power = Readout("");
                    var machines = Ui.Choice(controls, new[] { "1 号机", "2 号机", "3 号机" }, p.Machine,
                        machine => { S.SwitchPachinkoMachine(machine); });
                    Ui.Size(machines, -1, 43);
                    var status = Readout("", 55, Ui.Good);
                    var spacer = Ui.Size(Ui.Rect(controls, "ActionSpace"), -1, 0);
                    spacer.GetComponent<LayoutElement>().flexibleHeight = 1;
                    var fire = Hold("按住发射 · 鼠标或空格");
                    return () =>
                    {
                        board.SetVerticesDirty();
                        counts.text = $"剩余 {p.Beads} 珠\n本局返珠 {p.Returned} 珠";
                        power.text = $"{p.Power:P0}　·　奖励口返 {S.PachinkoPayoutForMachine(p.Machine)} 珠";
                        status.text = p.RewardFlash > 0 ? $"进奖励口 · +{p.LastReward} 珠"
                            : p.Beads == 0 && p.Balls.Count == 0 ? "弹珠已经用完。" : "松手后，盘里的珠子会继续落下。";
                        fire.interactable = p.Beads > 0;
                        machines.interactable = p.Balls.Count == 0;
                        if (machines.value != p.Machine) machines.SetValueWithoutNotify(p.Machine);
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
                    var spacer = Ui.Size(Ui.Rect(controls, "ActionSpace"), -1, 0);
                    spacer.GetComponent<LayoutElement>().flexibleHeight = 1;
                    var fire = Hold("按住蓄力，松手出杆");
                    Vector3 previous = Input.mousePosition;
                    return () =>
                    {
                        var mouse = Input.mousePosition;
                        if (mouse != previous && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            board.rectTransform, mouse, null, out var local))
                        {
                            var rect = board.rectTransform.rect;
                            if (rect.Contains(local))
                            {
                                var pos = Rect.PointToNormalized(rect, local);
                                var (x, y) = LeisureBoardGraphic.ToTable(pos);
                                S.AimBilliards(x, y);
                            }
                        }
                        previous = mouse;
                        board.SetVerticesDirty();
                        score.text = $"你 {pool.PlayerScore} : {pool.OpponentScore} {pool.OpponentName}\n先打进 3 球获胜";
                        message.text = pool.Message;
                        power.text = $"力度 {pool.Power:P0} · 鼠标瞄准";
                        fire.interactable = !pool.Finished && !pool.OpponentTurn && !pool.Rolling;
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
                    var message = Readout("", 80, Ui.Accent);
                    var counts = Readout("", 55);
                    var lateral = Ui.Row(controls, 47, 8);
                    var depth = Ui.Row(controls, 47, 8);
                    MoveButton(lateral, "← 左移", -.13, 0);
                    MoveButton(lateral, "右移 →", .13, 0);
                    MoveButton(depth, "↑ 推远", 0, .13);
                    MoveButton(depth, "靠近 ↓", 0, -.13);
                    var freight = S.CourierQuote(toShop: false).fee;
                    Readout($"装不下会送到家，新叫车 {freight}\n付不起运费则放弃这次的奖品", 70, Ui.Dim);
                    var spacer = Ui.Size(Ui.Rect(controls, "ActionSpace"), -1, 0);
                    spacer.GetComponent<LayoutElement>().flexibleHeight = 1;
                    var grab = Ui.Size(Ui.Button(controls, $"投币并落爪 · ¥{GameSession.ClawPriceYuan}", () => S.DropClaw(),
                        22, Ui.ButtonHot), -1, 60);
                    return () =>
                    {
                        if (_windows.LastOrDefault() == w && Application.isFocused && !claw.Animating)
                        {
                            double step = Math.Min(Time.unscaledDeltaTime, .08f) * 1.12;
                            if (Input.GetKey(KeyCode.LeftArrow)) S.MoveClaw(-step, 0);
                            if (Input.GetKey(KeyCode.RightArrow)) S.MoveClaw(step, 0);
                            if (Input.GetKey(KeyCode.UpArrow)) S.MoveClaw(0, step);
                            if (Input.GetKey(KeyCode.DownArrow)) S.MoveClaw(0, -step);
                            if (Input.GetKeyDown(KeyCode.Space)) S.DropClaw();
                        }
                        cabinet.Render(claw);
                        message.text = claw.Message;
                        counts.text = $"已抓 {claw.Attempts} 次 · 获得 {claw.PrizesWon} 件";
                        grab.interactable = !claw.Animating && claw.HasGifts
                            && S.Ledger.CanAfford(Money.FromYuan(GameSession.ClawPriceYuan));
                    };

                    void MoveButton(Transform row, string caption, double x, double z)
                        => Ui.Size(Ui.Button(row, caption, () => S.MoveClaw(x, z), 17), 0, 44, 1);
                }
            }, holds: false, closable: false, onClosed: () =>
            {
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
            var fold = Ui.Size(Ui.Button(row, "弃牌", () => S.PokerMove(PokerAction.Fold), 18), 0, 47, 1);
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
            Ui.Size(Ui.Button(raiseActions, "取消", () => editingRaise = false, 17), 0, 41, 1);
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
