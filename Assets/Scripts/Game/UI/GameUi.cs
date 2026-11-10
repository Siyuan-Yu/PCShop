using System;
using System.Collections.Generic;
using System.Linq;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    /// <summary>模态窗口：打开时暂停时间（Hold），关闭时恢复。内容通过 builder 重建。</summary>
    public sealed class Window
    {
        public RectTransform Root;
        public RectTransform Content;
        public Text TitleText;
        public Text TimeStatusText;
        public Action<Window> Builder;
        public Action OnClosed;
        public bool Holds;
        public bool LiveConversation;
        public Func<string> LiveKey;
        internal string LiveStamp;
        internal float LiveRefreshAt;
        public bool Closed;
        public bool Closable = true;
        internal bool RebuildPending;
        internal bool ResetScrollOnRebuild;
        internal WindowFit Fitter;
        internal GameUi Owner;
        internal bool OpeningMealSystem;

        public void Rebuild()
        {
            if (Closed) return;
            var scrolled = Ui.SaveScroll(Root);
            Ui.Clear(Content);
            Builder?.Invoke(this);
            GameUi.TabStrips.Restyle(Content);
            Ui.RestoreScroll(Root, Root, scrolled);
            if (ResetScrollOnRebuild)
            {
                ResetScrollOnRebuild = false;
                Canvas.ForceUpdateCanvases();
                foreach (var scroll in Root.GetComponentsInChildren<ScrollRect>())
                {
                    scroll.StopMovement();
                    scroll.verticalNormalizedPosition = 1;
                }
            }
        }

        public void Close() => Owner.CloseWindow(this);
        // InputField ends editing during pointer selection. Defer reconstruction so
        // the same click can still reach its original action button.
        public void RequestRebuild() => RebuildPending = true;
    }

    public sealed partial class GameUi
    {
        private readonly GameController _ctrl;
        private readonly Canvas _labelCanvas;
        private readonly Canvas _hudCanvas;
        private readonly CanvasGroup _expoHudGate;
        private readonly Canvas _dockCanvas;
        private readonly Canvas _modalCanvas;
        private readonly Canvas _toastCanvas;
        private readonly List<Window> _windows = new List<Window>();
        private readonly List<(GameObject go, float until)> _toasts = new List<(GameObject, float)>();
        private readonly Dictionary<string, (RectTransform rt, Text text, Image plate, Outline textEdge)> _labels =
            new Dictionary<string, (RectTransform, Text, Image, Outline)>();
        private readonly List<(RectTransform rt, Text text, Button button)> _queueLabels = new List<(RectTransform, Text, Button)>();
        private RectTransform _toastList;

        private Button _storageWarning;
        private Text _noticeButtonText;
        private int _unreadNotices;
        private Text _dateText, _cashText, _statsText, _placeText, _hintText, _cargoText, _chaseText;
        private readonly List<(Button b, int speed)> _speedButtons = new List<(Button, int)>();
        private readonly List<(Button b, VehicleKind kind)> _vehicleButtons = new List<(Button, VehicleKind)>();
        private Button _pauseButton;
        private float _slowTimer;
        private float _uiTime;
        private float _hudRefreshTimer;

        private GameSession S => _ctrl.Session;

        public bool AnyModalOpen => _windows.Count > 0;

        /// <summary>冒烟 / 调试：刷新所有打开的窗口。</summary>
        public void RebuildWindows()
        {
            foreach (Window w in _windows.ToArray()) w.Rebuild();
        }

        /// <summary>鼠标是否在 UI 上（这时不处理场景点击）。</summary>
        public static bool PointerOverUi => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public GameUi(GameController ctrl)
        {
            _ctrl = ctrl;
            Ui.EnsureEventSystem();
            _labelCanvas = Ui.CreateCanvas("UI_Labels", 0);
            _hudCanvas = Ui.CreateCanvas("UI_Hud", 10);
            _expoHudGate=_hudCanvas.gameObject.AddComponent<CanvasGroup>();
            _dockCanvas = Ui.CreateCanvas("UI_Dock", 20);
            _modalCanvas = Ui.CreateCanvas("UI_Modal", 30);
            _toastCanvas = Ui.CreateCanvas("UI_Toast", 25);
            BuildHud();
            BuildOpeningMealGuide();
            BuildToasts();
            BuildTip();
        }

        public IEnumerable<Canvas> Canvases => new[] { _labelCanvas, _hudCanvas, _dockCanvas, _modalCanvas, _toastCanvas, _tipCanvas, _tutorialCanvas };

        // ---------- 每帧 ----------

        public void Update(float dt)
        {
            if (S == null) return;
            UpdateUpcomingScheduleHud();
            EnsureQuarterlyExpoWindow();
            _expoHudGate.interactable=!ExpoHudOpen;
            _labelCanvas.enabled=!ExpoHudOpen&&!_ctrl.PetFocusActive;
            RefreshShopDockVisibility();
            UpdateLoanReminderHud();
            RefreshWorkbenchAccess();
            HoldRepeat.AdvanceActiveHold(Time.unscaledDeltaTime);
            foreach (var window in _windows.ToArray())
            {
                window.Fitter?.Fit();
                if(window.TimeStatusText!=null)window.TimeStatusText.text=S.Time.IsRunning&&!S.OpeningClockFrozen?"时间在推进":"时间已暂停";
                if (window.LiveConversation && Time.unscaledTime >= window.LiveRefreshAt)
                {
                    window.LiveRefreshAt = Time.unscaledTime + 1;
                    string stamp = window.LiveKey?.Invoke();
                    if (stamp != window.LiveStamp) { window.LiveStamp = stamp; window.RequestRebuild(); }
                }
                if (window.RebuildPending && !ListScrollbar.PressedInside(window.Root))
                { window.RebuildPending = false; window.Rebuild(); }
            }
            UpdateAutoExpoChoices();
            UpdateDisplayPreview();
            UpdateOpeningMealGuide();
            UpdateHudMore();
            if (MainMenuOpen) return;
            RefreshOpeningKindChoice();
            UpdateWorkScreen();
            if (S.IsWorking) return;
            CheckEconomicReport();
            _uiTime += dt;
            _hudRefreshTimer -= dt;
            if (_hudRefreshTimer <= 0 || dt <= 0) { _hudRefreshTimer = .1f; UpdateHud(); }
            if (dt <= 0) _labelContentAt = 0;
            UpdateCityLifeHud(dt);
            UpdateLabels();
            UpdatePlayerMarker();
            UpdateDistrictPersonMarkers();
            UpdateInteractMarker();
            UpdatePlacementStatus();
            UpdateEditHud();
            UpdateQueueLabels();
            UpdateToasts();
            UpdateTip(dt);
            _slowTimer -= dt;
            if (_slowTimer <= 0)
            {
                _slowTimer = 0.4f;
                RefreshShortcutMenus();
                UpdateVehicleButtons();
                UpdateQuestTracker();
                if (!Input.GetMouseButton(0)) RefreshDock();
            }
        }

        // ---------- HUD ----------

        private void BuildHud()
        {
            Transform root = _hudCanvas.transform;
            Image top = Ui.Panel(root, "TopBar", Ui.PanelBg);
            top.rectTransform.Anchor(0, 1, 1, 1, 0, -64, 0, 0);
            RectTransform row = Ui.Rect(top.transform, "Row").Fill();
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 8, 8);
            h.spacing = 5;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childAlignment = TextAnchor.MiddleLeft;

            AddCheatButton(row);
            var weatherButton = Ui.Size(Ui.Button(row, "", OpenCityClimate, 20), 216);
            _dateText = weatherButton.GetComponentInChildren<Text>();
            _dateText.alignment = TextAnchor.MiddleLeft;
            _dateText.horizontalOverflow=HorizontalWrapMode.Overflow;
            _dateText.resizeTextForBestFit=true;_dateText.resizeTextMinSize=17;_dateText.resizeTextMaxSize=20;
            weatherButton.Tip(()=>"点击查看天气预报与穿衣建议\n"+S.Clock.Today+" "+Ui.WeekdayName(S.Clock.Today.Weekday)+"\n"+S.CityWeatherForecast);
            var cashButton = Ui.Size(Ui.Button(row, "", OpenLedger, 24), 144);
            _cashText = cashButton.GetComponentInChildren<Text>();
            _cashText.fontStyle = FontStyle.Bold;
            _cashText.alignment = TextAnchor.MiddleLeft;
            _cashText.horizontalOverflow=HorizontalWrapMode.Overflow;
            _cashText.resizeTextForBestFit=true;_cashText.resizeTextMinSize=17;_cashText.resizeTextMaxSize=24;
            cashButton.Tip("<b>总收支账本</b>：点击查看每天、每周、每月的全部收支和逐笔明细。\n资金紧张时可缩减经营、处置资产或打工继续，不会因现金为负自动清档。");

            _pauseButton = Ui.Size(Ui.Button(row, "暂停", () => S.Time.TogglePause(), 16), 68);
            _pauseButton.Tip(() => S.Time.IsHeld
                ? "<b>已暂停</b>：弹出窗口或摆放家具时时间自动暂停，结束后恢复。\n店铺右侧各页签都可用顶部按钮暂停或继续。"
                : S.Time.IsUserPaused ? "<b>继续</b>　快捷键：空格" : "<b>暂停</b>　快捷键：空格");
            foreach (int sp in TimeController.Speeds)
            {
                int speed = sp;
                string label = TimeController.SpeedLabel(speed);
                Button b = Ui.Size(Ui.Button(row, label, () => S.Time.SetSpeed(speed), 17), speed < TimeController.DefaultSpeed ? 50 : 38);
                int keyNo = System.Array.IndexOf(TimeController.Speeds, speed) + 1;
                b.Tip($"<b>{label} 游戏速度</b>　快捷键：{keyNo}");
                _speedButtons.Add((b, speed));
            }

            _placeText = Ui.Size(Ui.Label(row, "", 17, TextAnchor.MiddleCenter), 170, -1, 1);
            _placeText.resizeTextForBestFit = true;
            _placeText.resizeTextMinSize = 14;
            _placeText.resizeTextMaxSize = 17;
            _statsText = Ui.Size(Ui.Label(row, "", 15, TextAnchor.MiddleLeft, Ui.Dim), 215);
            _statsText.Tip(StatsTip);
            Ui.Size(Ui.Button(row, "我的", OpenMyPanel, 18, Ui.ButtonHot), 59).Tip("<b>我的</b>　快捷键：M\n身体状况、住处与饮食；在家可以摆家具，店面整理在店铺“装修”页。");
            Ui.Size(Ui.Button(row, "背包 B", ToggleBackpack, 18), 68).Tip("<b>背包</b>　快捷键：B\n身上带的货和负重；在家或店里一键全放下。");
            Ui.Size(Ui.Button(row, "手机", OpenPhone, 18), 59).Tip("<b>手机</b>　快捷键：P\n买了智能手机才有；通讯录、网购、外卖等 app。");
            Ui.Size(Ui.Button(row, "任务", OpenQuests, 18), 59).Tip("<b>任务</b>　快捷键：T\n当前任务怎么做、完成奖励；左上角的任务条收起后从这里再打开。");
            _businessButton = Ui.Size(Ui.Button(row, "资产", OpenBusinessPanel, 18), 59);
            _businessButton.name = "HudAssets";
            _businessButton.gameObject.SetActive(true);
            _businessButton.Tip("<b>资产</b>　快捷键：J\n门店最近7天收支与来源；已购和租用物业的位置、用途与租金。");
            Ui.Size(Ui.Button(row,"采购",OpenCityProcurement,18),59).Tip("商铺采购、补货目标、在途订单与直供协议。").name="HudProcurement";
            _noticeButtonText = Ui.Size(Ui.Button(row, "通知", OpenNoticeHistory, 16), 59).GetComponentInChildren<Text>();
            BuildHudMoreMenu(row);
            Ui.Size(Ui.Button(row, "菜单", OpenSystemPanel, 18), 59).Tip("<b>菜单</b>　快捷键：Esc\n存档、读档、重新开始和音效设置。");

            Image bottom = Ui.Panel(root, "BottomBar", Ui.PanelBg);
            _travelBar = bottom.gameObject;
            bottom.rectTransform.Anchor(0, 0, 1, 0, 0, 0, 0, -58);
            RectTransform brow = Ui.Rect(bottom.transform, "Row").Fill();
            var bh = brow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bh.padding = new RectOffset(12, 12, 8, 8);
            bh.spacing = 8;
            bh.childControlWidth = true;
            bh.childControlHeight = true;
            bh.childForceExpandWidth = false;
            Ui.Size(Ui.Label(brow, "出行：", 18), 60);
            foreach (var vehicle in Vehicles.All.Where(v=>v.Kind!=VehicleKind.Van))
            {
                var kind = vehicle.Kind;
                var button = Ui.Size(Ui.Button(brow, vehicle.Name, () => S.SetVehicle(kind), 17), 72);
                button.Tip(() => vehicle.Name + (S.Vehicle == kind ? "（当前使用）" : S.CanUse(kind) ? "：点击切换" : "：尚未购买或暂无空闲车辆"));
                _vehicleButtons.Add((button, kind));
            }
            _cargoText = Ui.Size(Ui.Label(brow, "", 16), 155);
            Ui.Size(Ui.Label(brow, "快捷前往：", 17), 90);
            BuildShortcutButton(brow, "回家睡觉", SleepShortcut, 108);
            BuildShortcutButton(brow,"我的店铺",0,100);
            BuildShortcutButton(brow,"批发市场",1,100);
            BuildShortcutButton(brow,"中介",2,72);
            BuildShortcutButton(brow,"自定目的地",3,116);
            RefreshShortcutMenus();

            Ui.Size(Ui.Button(brow, "叫货车", OpenTruckWindow, 17, Ui.ButtonHot), 100).Tip("打开货车装货单，选货物和目的地；手机通讯录里的入口也保留。");
            BuildCityLifeHud(root);
            BuildShopDockToggle(root);

            Image hint = Ui.Panel(root, "HintStrip", new Color(.13f,.17f,.22f,.95f));
            _worldHintStrip = hint.gameObject;
            _hintStrip = hint;
            hint.rectTransform.Anchor(0, 0, 0, 0, 16, 70, -1136, -122);
            hint.raycastTarget = false;
            _hintText = Ui.Label(hint.transform, "", 16, TextAnchor.MiddleLeft, Ui.TextColor);
            _hintText.rectTransform.Fill(10);

            _storageWarning = Ui.Button(root, "", () => { if (S.InPlayerShop) OpenShopLockerWindow(); else OpenStorageWindow(); }, 16, Ui.Warn);
            ((RectTransform)_storageWarning.transform).Anchor(0.33f, 1, 0.67f, 1, 0, -108, 0, 72);
            _storageWarning.gameObject.SetActive(false);
            BuildQuestTracker(root);
            BuildUpcomingScheduleHud(root);
            BuildLoanReminderHud(root);
            BuildSleepBanner(root);
            BuildHungerBanner(root);

            _chaseText = Ui.Notice(root, "交警在追你！快跑或躲进店里！", 27);
            ((RectTransform)_chaseText.transform.parent).Anchor(.35f, 1, .65f, 1, 0, -130, 0, 75);
            _chaseText.transform.parent.gameObject.SetActive(false);
        }

        private Button _businessButton;
        private Button _brandButton;

        /// <summary>编辑器里才有的作弊工具按钮（打包时整段不编译）。</summary>
        partial void AddCheatButton(Transform row);

        private void UpdateHud()
        {
            GameTimestamp now = S.Clock.Now;
            _noticeButtonText.text = _unreadNotices > 0 ? "通知 " + Math.Min(99, _unreadNotices) : "通知";
            UpdateSleepBanner();
            if (!_businessButton.gameObject.activeSelf) _businessButton.gameObject.SetActive(true);
            if(_brandButton!=null)_brandButton.gameObject.SetActive(S.BrandManagementUnlocked);
            _dateText.text = (S.IsCityLifeTest?"测试 ":"")+$"{now.Date.Year}年{now.Date.Month}/{now.Date.Day} {Ui.WeekdayName(now.Date.Weekday)} {Ui.Hm(now.MinuteOfDay)}\n天气 · {S.WeatherSummary}";
            _cashText.text = S.Ledger.Balance.ToString();
            _cashText.color = S.Ledger.Balance.IsNegative ? Ui.Warn : Ui.Good;

            bool paused = !S.Time.IsRunning || S.OpeningClockFrozen;
            _pauseButton.image.color = paused ? Ui.Warn : Ui.ButtonBg;
            _pauseButton.GetComponentInChildren<Text>().text = S.Time.IsHeld ? "窗口暂停" : (S.Time.IsUserPaused ? "继续" : S.OpeningClockFrozen ? "教学暂停" : "暂停");
            foreach (var (b, speed) in _speedButtons)
                b.image.color = !paused && S.Time.Speed == speed ? Ui.ButtonHot : Ui.ButtonBg;

            PlayerStats p = S.Player;
            UpdateHungerBanner(p.Hunger);
            string stress = $"压力 {p.Stress:0}";
            if (p.Stress >= PlayerStats.HighStressThreshold) stress = $"<b><color=#D79780>{stress}</color></b>";
            _statsText.text = $"{HungerHudText(p.Hunger)} · 健康 {Pct(p.Health)} · {stress}\n" + (S.EnergyExhausted
                ? $"<b><color=#D79780>精力耗尽 · {S.EnergyAwakeMinutesLeft/60}:{S.EnergyAwakeMinutesLeft%60:00}</color></b>"
                : $"精力 {Pct(p.Energy)}/{p.MaxEnergy:0} · 名望 {S.PlayerShop.Reputation:0.0}");

            string where;
            if (S.Trip != null)
            {
                Place target = S.City.PlaceAtNode(S.Trip.TargetNode);
                where = $"{S.CurrentVehicle.Name}前往 {(target != null ? S.PlaceTitle(target) : "路口")}";
            }
            else if (S.CurrentPlace != null) where = "在 " + S.PlaceTitle(S.CurrentPlace);
            else where = "在街上";
            string shopName = S.MyShopCount > 1 ? S.ShopTitle(S.CurrentShop) : "店";
            string shopState = !S.HasShop ? "<color=#DDC18B>还没有店铺</color>" : S.IsPlaceOpen(S.PlayerShopPlace) ? $"<color=#A9C9A9>{shopName}：营业中</color>" : $"<color=#D79780>{shopName}：关门</color>";
            _placeText.text = "<b>" + where + "</b>\n" + shopState;

            bool warnStorage = S.HasTempHere && S.TempMinutesLeftHere <= 1440 && !S.IsGameOver && !AnyModalOpen && !_ctrl.Placing;
            _storageWarning.gameObject.SetActive(warnStorage);
            if (warnStorage) _storageWarning.GetComponentInChildren<Text>().text = S.TempDueHere
                ? "临时存放已到期 · 点击清理（可稍后处理）" : "临时存放不足1天 · 点击整理";

            if (_hintBaseDay < -1000000) _hintBaseDay = S.Clock.Today.TotalDays;
            bool early = S.Clock.Today.TotalDays - _hintBaseDay < 2;
            if (S.IsGameOver) _hintText.text = "游戏结束：" + S.GameOverReason;
            else if (_ctrl.Placing) _hintText.text = "左键放置 · R 旋转 · X 收纳 · Esc 取消 · Q/E 转视角 · V 复位";
            else if (_ctrl.InInterior) _hintText.text = early ? "WASD 移动 · 靠近人物或设施按 F 交互 · 门口按 F 离店" : "";
            else if (S.Trip != null) _hintText.text = $"快捷前往中，约 {S.Trip.EstimateRemainingMinutes(S.Time.SecondsPerMinuteAt1x)} 分钟 · 按 WASD 随时接管";
            else if (S.NearbyCommunityBox>=0)
                _hintText.text = "<color=#DDC18B>按 F 调查"+GameSession.CommunityBoxName(S.NearbyCommunityBox)+"，取回这一份图页</color>";
            else if (S.NearbyPlace?.Kind == PlaceKind.Home)
                _hintText.text = $"<color=#DDC18B>按 F 查看住户 · {S.PlaceTitle(S.NearbyPlace)}</color>";
            else if (S.NearbyPlace != null)
                _hintText.text = S.ClosedReason(S.NearbyPlace) is string closed
                    ? $"<color=#D79780>{closed}</color>"
                    : $"<color=#DDC18B>按 F 进入 {S.PlaceTitle(S.NearbyPlace)}</color>";
            else if (_ctrl.InVisit)
                _hintText.text = _ctrl.VisitRoom?.NearbyLabel is string visitHint
                    ? "按 F：" + visitHint
                    : !early ? "" : S.CanSleepAt(S.CurrentPlace) ? "WASD 在屋里走动 · 走近床或房门按 F" : "WASD 在屋里走动 · 走近住户或房门按 F";
            else if (_ctrl.InHome) _hintText.text = _ctrl.HomeRoom.NearbyId != null ? $"<color=#DDC18B>按 F：{_ctrl.HomeRoom.NearbyLabel}</color>" : early ? "WASD 在屋里走动 · 走近床、衣柜、电饭锅、纸箱、凳子（头顶出现提示按 F）· 房间信息、摆放家具看右上角“我的”" : "";
            else if (S.CurrentPlace != null) _hintText.text = early ? "按 WASD 离开 · 空格暂停" : "";
            else _hintText.text = early ? "WASD 移动 · F 进入 · 空格暂停 · 右键拖动 / 滚轮缩放" : "";
            _hintStrip.enabled = !string.IsNullOrEmpty(_hintText.text);

            _chaseText.transform.parent.gameObject.SetActive(S.Police.AnyChasing && S.CurrentPlace == null);
        }

        private static string Pct(double v) => v.ToString("0");

        private void UpdateVehicleButtons()
        {
            foreach (var (b, kind) in _vehicleButtons)
            {
                bool can = S.CanUse(kind);
                b.interactable = can;
                b.gameObject.SetActive(can || S.Vehicle == kind || kind <= VehicleKind.Taxi || S.ImpoundedVehicleCount(kind) > 0);
                b.image.color = S.Vehicle == kind ? Ui.ButtonHot : Ui.ButtonBg;
                Text t = b.GetComponentInChildren<Text>();
                t.color = can ? Ui.TextColor : Ui.Dim;
                string name = Vehicles.Get(kind).Name;
                if (S.ImpoundedVehicleCount(kind) > 0 && !can) name = kind == VehicleKind.Tricycle ? "三轮(扣)" : "电驴(扣)";
                t.text = name;
            }
            _cargoText.text = $"负重 {S.CarriedKg:0.##}/{S.CarryLimitKg:0.##} 千克";
        }

        // ---------- 地点名牌 ----------

        private bool _streetLabelsVisible = true;
        private float _labelContentAt;

        private void UpdateLabels()
        {
            Camera cam = _ctrl.Cam;
            bool show = !_ctrl.InRoom;
            if (!show)
            {
                if (_streetLabelsVisible)
                    foreach (var label in _labels.Values) label.rt.gameObject.SetActive(false);
                _streetLabelsVisible = false;
                return;
            }
            bool refresh = !_streetLabelsVisible || _uiTime >= _labelContentAt;
            _streetLabelsVisible = true;
            if (refresh) _labelContentAt = _uiTime + .25f;
            foreach (var kv in _ctrl.World.Places)
            {
                PlaceVisual pv = kv.Value;
                if (refresh && pv.Sign != null)
                {
                    string title = S.PlaceTitle(pv.Place);
                    if (pv.Sign.text != title)
                    {
                        pv.Sign.text = title;
                        Vector2 size = ((RectTransform)pv.Sign.transform.parent).sizeDelta;
                        pv.Sign.fontSize = Mathf.RoundToInt(Mathf.Min(size.y * .76f, size.x * .9f / Mathf.Max(1, title.Length)));
                    }
                }
                Vector3 sp = cam.WorldToScreenPoint(pv.LabelPos);
                bool visible = sp.z > 0 && sp.x > -100 && sp.x < cam.pixelWidth + 100 && sp.y > -50 && sp.y < cam.pixelHeight + 50;
                bool created = false;
                if (!_labels.TryGetValue(kv.Key, out var label))
                {
                    if (!visible) continue;
                    created = true;
                    Place place = pv.Place;
                    bool important = place.Kind != PlaceKind.Shop || place.IsPlayerShop;
                    Button b = Ui.Button(_labelCanvas.transform, place.Name, () => OpenStreetPlace(place),
                        important ? 18 : 15, LabelColor(place, important));
                    RectTransform rt = (RectTransform)b.transform;
                    rt.anchorMin = rt.anchorMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0);
                    rt.sizeDelta = new Vector2(important ? 170 : 120, important ? 46 : 30);
                    var text = b.GetComponentInChildren<Text>();
                    var textEdge = text.gameObject.AddComponent<Outline>();
                    textEdge.effectColor = new Color(0.07f, 0.10f, 0.13f, 0.9f);
                    textEdge.effectDistance = new Vector2(1f, -1f);
                    label = (rt, text, b.image, textEdge);
                    _labels[kv.Key] = label;
                }

                bool appeared = visible && !label.rt.gameObject.activeSelf;
                label.rt.gameObject.SetActive(visible);
                if (!visible) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_labelCanvas.transform, sp, LabelCam, out Vector2 local);
                var canvasRt = (RectTransform)_labelCanvas.transform;
                label.rt.anchoredPosition = local + canvasRt.rect.size * 0.5f;
                if (!refresh && !created && !appeared) continue;
                Place location = pv.Place;
                var shop = S.ShopAt(location);
                bool owned = shop != null || S.VentureAt(location.Id) != null || S.PropertyAt(location.Id) != null;
                bool major = owned || location.Kind != PlaceKind.Shop || location.IsPlayerShop;
                bool residential = location.Kind == PlaceKind.Home;
                bool plainShop = !major && !S.IsVacant(location) && !HasStreetShopService(location);
                bool plain = residential || plainShop;
                label.text.text = plainShop ? S.PlaceTitle(location) : LabelText(location);
                float width = residential ? 156 : major ? 180 : 120;
                if (shop != null) width = Mathf.Max(width, S.ShopTitle(shop).Length * 18 + 18);
                label.rt.sizeDelta = new Vector2(width, residential ? 38 : major ? 52 : 30);
                label.text.fontSize = plain ? 14 : major ? 18 : 15;
                label.text.color = plain ? Ui.Dim : Ui.TextColor;
                label.textEdge.enabled = plain;
                label.text.raycastTarget = plain;
                label.plate.enabled = !plain;
                if (!plain) label.plate.color = LabelColor(location, location.Kind != PlaceKind.Shop || location.IsPlayerShop);
            }
        }

        private bool HasStreetShopService(Place place) => IsCarryStore(place) || UsesCityVenuePanel(place)
            || RaceTracks.Get(place.Id) != null || place.Id == "motor_scooter_mod" || place.Id == "life_expo"
            || place.Id == GameSession.CommunityMcnId || place.Id == S.ParkPlaceId;

        /// <summary>按地点类型区分街景名牌。</summary>
        private void OpenStreetPlace(Place p)
        {
            if (S.VentureAt(p.Id) != null || S.IsPlayerPropertyOccupied(p.Id) && !S.IsMyShop(p))
            {
                _businessTab = p.Id == S.ParkPlaceId ? 6 : S.VentureAt(p.Id) != null ? 3 : 5;
                OpenBusinessPanel();
            }
            else _ctrl.GoToPlace(p);
        }

        private Color LabelColor(Place p, bool important)
        {
            var venture = S.VentureAt(p.Id);
            if (venture != null) return venture.Paused ? Ui.Warn : Ui.ButtonHot;
            if (S.IsPlayerPropertyOccupied(p.Id)) return Ui.ButtonHot;
            if (S.IsMyShop(p)) return new Color(0.1f, 0.55f, 0.5f, 0.92f);
            if (S.IsVacant(p)) return new Color(0.75f, 0.42f, 0.1f, 0.9f);
            if (p.Kind == PlaceKind.Agency) return new Color(0.15f, 0.35f, 0.7f, 0.92f);
            return new Color(0.1f, 0.12f, 0.16f, important ? 0.85f : 0.6f);
        }

        private RectTransform _playerMark;

        /// <summary>街上头顶的"我"标记：镜头转到建筑背后时，人被楼挡住也找得到自己。</summary>
        private void UpdatePlayerMarker()
        {
            if (_playerMark == null)
            {
                Button b = Ui.Button(_labelCanvas.transform, "我", null, 16, new Color(0.95f, 0.75f, 0.1f, 0.9f));
                b.image.raycastTarget = false;
                foreach (Text t in b.GetComponentsInChildren<Text>()) t.raycastTarget = false;
                _playerMark = (RectTransform)b.transform;
                _playerMark.anchorMin = _playerMark.anchorMax = Vector2.zero;
                _playerMark.pivot = new Vector2(0.5f, 0);
                _playerMark.sizeDelta = new Vector2(34, 24);
            }
            Camera cam = _ctrl.Cam;
            bool show = !_ctrl.InRoom && S.CurrentPlace == null;
            Vector3 sp = cam.WorldToScreenPoint(_ctrl.World.StreetPosition(S.PlayerPosition) + Vector3.up * 2.6f);
            show &= sp.z > 0;
            _playerMark.gameObject.SetActive(show);
            if (!show) return;
            var canvasRt = (RectTransform)_labelCanvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, LabelCam, out Vector2 local);
            _playerMark.anchoredPosition = local + canvasRt.rect.size * 0.5f;
        }

        private string LabelText(Place p)
        {
            var venture = S.VentureAt(p.Id);
            if (venture != null) return S.PlaceTitle(p) + (venture.Paused ? "\n<size=13><color=#D79780>停业 · 点击管理</color></size>" : "\n<size=13>营业中 · 点击管理</size>");
            if (p.Id == S.ParkPlaceId) return "电脑乐园\n<size=13>点击查看布局</size>";
            if (S.IsPlayerPropertyOccupied(p.Id) && !S.IsMyShop(p)) return "我的物业\n<size=13>托管出租中</size>";
            switch (p.Kind)
            {
                case PlaceKind.Wholesaler:
                    return p.Name + (S.IsWholesalerOpen ? "" : "\n<size=13><color=#D79780>关门</color></size>");
                case PlaceKind.Dealer:
                    return p.Name + (S.IsDealerOpen ? "" : "\n<size=13><color=#D79780>关门</color></size>");
                case PlaceKind.Shop when S.IsMyShop(p):
                    return S.PlaceTitle(p) + (S.IsPlaceOpen(p) ? "\n<size=13><color=#A9C9A9>营业中</color></size>" : "\n<size=13>关门</size>");
                case PlaceKind.Agency:
                    return p.Name + (!S.IsPlaceOpen(p) ? "\n<size=13><color=#D79780>已打烊</color></size>" : S.HasShop ? "" : "\n<size=13><color=#DDC18B>先来这里租铺子</color></size>");
                default:
                    return S.PlaceTitle(p) + (p.Kind == PlaceKind.Shop && !S.IsVacant(p) && !S.IsPlaceOpen(p) ? "\n<size=13><color=#D79780>已打烊</color></size>" : "");
            }
        }

        private readonly System.Collections.Generic.List<Text> _emoteLabels = new System.Collections.Generic.List<Text>();
        private readonly List<Button> _emoteChatButtons = new List<Button>();
        private readonly List<string> _emoteChatIds = new List<string>();

        private readonly System.Collections.Generic.List<ShopInteriorView.EmoteInfo> _emoteBuf = new System.Collections.Generic.List<ShopInteriorView.EmoteInfo>();

        /// <summary>头顶的小字：店里顾客想买什么 / 为什么走了 / +¥；家里电饭锅饭好了一直跳着闪，直到把饭取完。</summary>
        private void UpdateEmoteLabels()
        {
            int n = 0;
            _emoteBuf.Clear();
            if (_ctrl.InInterior) _emoteBuf.AddRange(_ctrl.Interior.Emotes);
            if (_ctrl.InHome && S.Pantry.CookedBowls > 0 && _ctrl.HomeRoom.CookerWorld is Vector3 cooker)
            {
                float t = Time.unscaledTime;
                float bob = Mathf.Abs(Mathf.Sin(t * 4f)) * 0.35f;
                float blink = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 9f));
                _emoteBuf.Add(new ShopInteriorView.EmoteInfo
                {
                    Pos = cooker + Vector3.up * bob,
                    Text = $"<b>饭好了！</b>\n<size=16>{S.Pantry.CookedBowls} 碗，按 F 取饭</size>",
                    Color = new Color(1f, 0.85f, 0.25f, blink),
                    Size = 26,
                });
            }
            int nb = 0;
            if (_emoteBuf.Count > 0)
            {
                var canvasRt = (RectTransform)_labelCanvas.transform;
                foreach (var e in _emoteBuf)
                {
                    if (e.Bubble)
                    {
                        if (nb >= _bubbles.Count) _bubbles.Add(NewBubble());
                        var (brt, btext) = _bubbles[nb++];
                        btext.text = e.Text;
                        btext.fontSize = e.Size;
                        btext.color = e.Color;
                        Vector3 bsp = _ctrl.Cam.WorldToScreenPoint(e.Pos);
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, bsp, LabelCam, out Vector2 blocal);
                        brt.anchoredPosition = blocal + canvasRt.rect.size * 0.5f;
                        brt.gameObject.SetActive(bsp.z > 0);
                        continue;
                    }
                    if (n >= _emoteLabels.Count)
                    {
                        Text lt = Ui.Label(_labelCanvas.transform, "", 14, TextAnchor.LowerCenter);
                        lt.horizontalOverflow = HorizontalWrapMode.Overflow;
                        lt.verticalOverflow = VerticalWrapMode.Overflow;
                        lt.supportRichText = true;
                        lt.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.85f);
                        RectTransform r = lt.rectTransform;
                        r.anchorMin = r.anchorMax = Vector2.zero;
                        r.pivot = new Vector2(0.5f, 0);
                        r.sizeDelta = new Vector2(260, 28);
                        int slot = _emoteLabels.Count;
                        var chat = lt.gameObject.AddComponent<Button>(); chat.targetGraphic = lt;
                        chat.onClick.AddListener(() =>
                        {
                            string person = _emoteChatIds[slot];
                            if (!string.IsNullOrEmpty(person) && S.CitySamePlace(person)) OpenCityPerson(person);
                        });
                        _emoteChatButtons.Add(chat); _emoteChatIds.Add(null);
                        _emoteLabels.Add(lt);
                    }
                    Text label = _emoteLabels[n];
                    label.text = e.Text;
                    _emoteChatIds[n] = e.PersonId;
                    bool canTalk = !string.IsNullOrEmpty(e.PersonId);
                    label.raycastTarget = canTalk; _emoteChatButtons[n].interactable = canTalk;
                    if (canTalk && S.Flags.Add("hint_named_people")) Toast("头顶显示姓名的人可以聊天；普通行人和顾客只显示行为状态。");
                    label.fontSize = e.Size;
                    label.color = e.Color;
                    Vector3 sp = _ctrl.Cam.WorldToScreenPoint(e.Pos);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, LabelCam, out Vector2 local);
                    label.rectTransform.anchoredPosition = local + canvasRt.rect.size * 0.5f;
                    label.gameObject.SetActive(sp.z > 0);
                    n++;
                }
            }
            for (; n < _emoteLabels.Count; n++) _emoteLabels[n].gameObject.SetActive(false);
            for (; nb < _bubbles.Count; nb++) _bubbles[nb].rt.gameObject.SetActive(false);
        }

        private readonly System.Collections.Generic.List<(RectTransform rt, Text text)> _bubbles = new System.Collections.Generic.List<(RectTransform, Text)>();

        /// <summary>白底圆角的对话气泡，底下一个小尖角指着说话的人；大小跟着字数走。</summary>
        private (RectTransform, Text) NewBubble()
        {
            Color bg = new Color(0.98f, 0.97f, 0.93f, 0.96f);
            Image panel = Ui.Panel(_labelCanvas.transform, "Bubble", bg);
            panel.raycastTarget = false;
            RectTransform rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0);
            var h = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 12, 6, 7);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(.025f,.035f,.06f,.48f);

            Image tail = Ui.Panel(rt, "Tail", bg);
            tail.raycastTarget = false;
            tail.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RectTransform trt = tail.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0);
            trt.sizeDelta = new Vector2(12, 12);
            trt.anchoredPosition = new Vector2(0, -1);
            trt.localRotation = Quaternion.Euler(0, 0, 45);

            Text text = Ui.Label(rt, "", 16, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.05f;
            rt.anchoredPosition = new Vector2(-9999, -9999);
            return (rt, text);
        }

        /// <summary>Esc：关掉最上面一层窗口。返回有没有关。</summary>
        public bool CloseTopWindow()
        {
            if(ChoiceDropdown.CancelFrame==UnityEngine.Time.frameCount)return true;
            foreach(var canvas in Canvases)foreach(var choice in canvas.GetComponentsInChildren<ChoiceDropdown>())if(choice.CloseChoice())return true;
            if (_windows.Count == 0) return false;
            Window top = _windows[_windows.Count - 1];
            if (!top.Closable) return false;
            top.Close();
            return true;
        }

        private void UpdateQueueLabels()
        {
            UpdateEmoteLabels();
            int i = 0;
            if (_ctrl.InInterior)
            {
                foreach (var kv in _ctrl.Interior.QueueHeads())
                {
                    if (i >= _queueLabels.Count)
                    {
                        int slot = i;
                        Button b = Ui.Button(_labelCanvas.transform, "", null, 16, new Color(0.85f, 0.6f, 0.1f, 0.92f));
                        RectTransform rt = (RectTransform)b.transform;
                        rt.anchorMin = rt.anchorMax = Vector2.zero;
                        rt.pivot = new Vector2(0.5f, 0);
                        rt.sizeDelta = new Vector2(170, 44);
                        _queueLabels.Add((rt, b.GetComponentInChildren<Text>(), b));
                    }
                    var (lrt, text, button) = _queueLabels[i];
                    int id = kv.Key;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => _ctrl.Checkout(id));
                    var c = FindCustomer(id);
                    string act = _ctrl.Interior.NearCounter ? "结账" : "等你到收银台";
                    text.text = c != null ? $"{act} {c.Spend}\n<size=12>{c.ItemName}</size>" : act;
                    Vector3 sp = _ctrl.Cam.WorldToScreenPoint(kv.Value);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_labelCanvas.transform, sp, LabelCam, out Vector2 local);
                    lrt.anchoredPosition = local + ((RectTransform)_labelCanvas.transform).rect.size * 0.5f;
                    lrt.gameObject.SetActive(true);
                    i++;
                }
            }
            for (; i < _queueLabels.Count; i++) _queueLabels[i].rt.gameObject.SetActive(false);
        }

        private Camera LabelCam => _labelCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _labelCanvas.worldCamera;

        private PCShopSell.Core.Shop.ShopCustomer FindCustomer(int id)
        {
            foreach (var c in S.PlayerShop.Queue)
            {
                if (c.Id == id) return c;
            }
            return null;
        }

        // ---------- 飘字 ----------

        private void BuildToasts()
        {
            _toastList = Ui.Rect(_toastCanvas.transform, "Toasts").Anchor(0, 1, 0, 1, 16, -684, -476, 284);
            var v = _toastList.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperLeft;
        }

        private readonly List<string> _noticeHistory = new List<string>();
        private Image _hintStrip;
        private int _hintBaseDay = int.MinValue / 2;
        private string _lastToast;
        private float _lastToastAt;
        public void OpenNoticeHistory()
        {
            _unreadNotices = 0;
            OpenWindow("通知记录（本次会话）", 850, 700, w =>
            {
                var list = Ui.Scroll(w.Content, 590);
                for (int i = _noticeHistory.Count - 1; i >= 0; i--) Para(list, _noticeHistory[i], 16);
                if (_noticeHistory.Count == 0) Line(list, "还没有通知。", 18, Ui.Dim);
            });
        }
        public void Toast(string message)
        {
            _upcomingScheduleDirty = true;
            for (int i = 0; i < _toasts.Count; i++)
            {
                var live = _toasts[i].go != null ? _toasts[i].go.GetComponentInChildren<Text>() : null;
                if (live != null && live.text == message) { _toasts[i] = (_toasts[i].go, _uiTime + 5f); return; }
            }
            if (_lastToast == message && _uiTime - _lastToastAt < 8) return;
            _lastToast = message; _lastToastAt = _uiTime;
            _unreadNotices++;
            _noticeHistory.Add((S == null ? "" : S.Clock.Now + "　") + message);
            if (_noticeHistory.Count > 150) _noticeHistory.RemoveAt(0);
            Image bg = Ui.Panel(_toastList, "Toast", new Color(.13f,.17f,.22f,.96f));
            var fit = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            fit.padding = new RectOffset(14, 14, 10, 10);
            fit.childControlHeight = true;
            fit.childControlWidth = true;
            fit.childForceExpandHeight = false;
            Text t = Ui.Label(bg.transform, message, 18);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            _toasts.Add((bg.gameObject, _uiTime + 5f));
            if (_toasts.Count > 3)
            {
                Kill.It(_toasts[0].go);
                _toasts.RemoveAt(0);
            }
        }

        private void UpdateToasts()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                if (_uiTime > _toasts[i].until)
                {
                    Kill.It(_toasts[i].go);
                    _toasts.RemoveAt(i);
                }
            }
        }

        // ---------- 窗口 ----------

        public Window OpenWindow(string title, float width, float height, Action<Window> builder, bool holds = true, bool closable = true, Action onClosed = null, bool scrollContent = true, bool allowDuringOpeningMeal = false, bool systemWindow = false)
        {
            bool system = systemWindow || _windows.Any(window => window.OpeningMealSystem && !window.Closed);
            if (OpeningMealRestricted && !allowDuringOpeningMeal && !system)
            {
                title = "先做饭"; width = 700; height = 390; holds = true; closable = true; onClosed = null; scrollContent = true;
                builder = BuildOpeningMealReminder;
            }
            var w = new Window { Builder = builder, Holds = holds, Owner = this, OnClosed = onClosed, Closable = closable };
            w.OpeningMealSystem = system;
            Image dim = Ui.Panel(_modalCanvas.transform, "Window_" + title, new Color(.025f,.035f,.06f,.48f));
            dim.rectTransform.Fill();
            Image panel = Ui.Panel(dim.transform, "Panel", Ui.PanelBg);
            RectTransform prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(width, height);
            w.Fitter = prt.gameObject.AddComponent<WindowFit>();
            w.Fitter.Fit();
            prt.anchoredPosition = new Vector2(0, -10 * _windows.Count);

            Image bar = Ui.Panel(prt, "TitleBar", Ui.PanelBg2);
            bar.rectTransform.Anchor(0, 1, 1, 1, 0, -50, 0, 0);
            w.TitleText = Ui.Label(bar.transform, title, 24);
            w.TitleText.rectTransform.Anchor(0, 0, 1, 1, 24, 0, 112, 0);
            w.TitleText.fontStyle = FontStyle.Bold;
            w.TitleText.rectTransform.offsetMax=new Vector2(-220,w.TitleText.rectTransform.offsetMax.y);
            w.TimeStatusText=Ui.Label(bar.transform,"",13,TextAnchor.MiddleRight,Ui.Dim);
            w.TimeStatusText.rectTransform.Anchor(1,0,1,1,-216,0,112,0);
            Ui.Panel(prt, "BrassRule", Ui.Accent).rectTransform.Anchor(0, 1, 1, 1, 20, -51, 20, 49);
            if (closable)
            {
                Button close = Ui.Button(bar.transform, MenuLocalization.Get("close"), w.Close, 18, Ui.Warn);
                ((RectTransform)close.transform).Anchor(1, 0, 1, 1, -96, 8, 10, 8);
            }

            w.Root = dim.rectTransform;
            if(scrollContent)
            {
                w.Content = Ui.Scroll(prt);
                ((RectTransform)w.Content.parent.parent).Anchor(0, 0, 1, 1, 0, 0, 0, 50);
            }
            else
            {
                w.Content = Ui.Rect(prt,"FixedContent").Anchor(0,0,1,1,0,0,0,50);
                Ui.VList(w.Content);
            }
            var contentLayout = w.Content.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 8; contentLayout.padding = new RectOffset(16, 16, 16, 16);
            _windows.Add(w);
            if (holds) S?.Time.Hold();
            w.Rebuild();
            if(w.TimeStatusText!=null)w.TimeStatusText.text=S?.Time.IsRunning==true&&!S.OpeningClockFrozen?"时间在推进":"时间已暂停";
            return w;
        }

        public void CloseWindow(Window w)
        {
            if (w.Closed) return;
            _upcomingScheduleDirty = true;
            w.Closed = true;
            _windows.Remove(w);
            if (w.Holds && S != null && S.Time.IsHeld) S.Time.Release();
            Kill.It(w.Root.gameObject);
            w.OnClosed?.Invoke();
        }

        public void CloseAllWindows()
        {
            CloseModalWindows();
            CloseDock();
        }

        public void CloseModalWindows()
        {
            for (int i = _windows.Count - 1; i >= 0; i--) CloseWindow(_windows[i]);
        }

        private readonly System.Collections.Generic.List<Window> _popups = new System.Collections.Generic.List<Window>();
        private bool PopupOpen
        {
            get
            {
                _popups.RemoveAll(p => p == null || p.Closed);
                return _popups.Count > 0;
            }
        }

        public void Popup(string title, string body)
        {
            if (S.IsWorking) { Toast(title + "：" + body); _workMessages.Add(title); return; }
            bool purchaseResult = title == GameSession.EmployeePurchaseDeliveryTitle || title == GameSession.EmployeePurchaseIncompleteTitle;
            _popups.Add(OpenWindow(title, 640, 380, w =>
            {
                if (purchaseResult) Ui.Label(Ui.Scroll(w.Content, 230), body, 19, TextAnchor.UpperLeft);
                else Ui.Size(Ui.Label(w.Content, body, 21, TextAnchor.UpperLeft), -1, 230);
                if(title.EndsWith("发来主线邀请"))
                    Ui.Size(Ui.Button(w.Content, "查看并回复邀请", () => { w.Close(); OpenDistrictStory(); }, 20, Ui.ButtonHot), -1, 44);
                if (title == GameSession.NpcRepaymentTitle)
                {
                    var actions = Ui.Row(w.Content, 48);
                    Ui.Size(Ui.Button(actions, "查看现金流水", () => { w.Close(); _ledgerFilter = 1; OpenLedger(); }, 20), -1, 48, 1);
                    Ui.Size(Ui.Button(actions, "知道了", w.Close, 20, Ui.ButtonHot), -1, 48, 1);
                }
                else Ui.Size(Ui.Button(w.Content, "知道了", w.Close, 20, Ui.ButtonHot), -1, 48);
            }));
        }

        /// <summary>随机事件：必须选一个选项才能继续（不可关闭）。</summary>
        public void ShowEvent(EventDef def)
        {
            if (S.IsWorking) { _workEvent = def; return; }
            float h = 250 + def.Choices.Count * 62;
            OpenWindow(def.Title, 720, h, w =>
            {
                if (S == null || S.PendingEvent != def)
                {
                    w.Close();
                    return;
                }
                Ui.Size(Ui.Label(w.Content, def.Body, 20, TextAnchor.UpperLeft), -1, 150);
                for (int i = 0; i < def.Choices.Count; i++)
                {
                    int idx = i;
                    EventChoice c = def.Choices[i];
                    bool ok = S.IsChoiceEnabled(c);
                    string label = ok ? c.Text : $"{c.Text}\n<size=14><color=#D79780>{c.DisabledReason}</color></size>";
                    Ui.Size(Ui.Button(w.Content, label, () =>
                    {
                        if (S.ResolveEvent(idx)) w.Close();
                    }, 19, ok ? Ui.ButtonHot : Ui.ButtonBg, ok), -1, ok ? 52 : 58);
                }
            }, true, false);
        }

        /// <summary>开局介绍。</summary>
        public void ShowIntro()
        {
            if (S.OpeningMealTutorialPending) { UpdateOpeningMealGuide(); return; }
            OpenWindow(L("welcome"), 740, 460, w =>
            {
                MenuTitle(w, "welcome");
                Para(w.Content, L(S.HasShop ? "intro_existing" : "intro_body"), 20);
                Para(w.Content, L("intro_move"), 18);
                Para(w.Content, L("intro_tasks"), 17, Ui.Accent);
                Ui.Size(Ui.Button(w.Content, L("begin_business"), w.Close, 20, Ui.ButtonHot), -1, 50);
            });
        }
    }
}
