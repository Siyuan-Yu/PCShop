using System;
using System.Collections.Generic;
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
        public Action<Window> Builder;
        public Action OnClosed;
        public bool Holds;
        public bool Closed;
        public bool Closable = true;
        internal GameUi Owner;

        public void Rebuild()
        {
            if (Closed) return;
            var scrolled = Ui.SaveScroll(Root);
            Ui.Clear(Content);
            Builder?.Invoke(this);
            Ui.RestoreScroll(Root, Root, scrolled);
        }

        public void Close() => Owner.CloseWindow(this);
    }

    public sealed partial class GameUi
    {
        private readonly GameController _ctrl;
        private readonly Canvas _labelCanvas;
        private readonly Canvas _hudCanvas;
        private readonly Canvas _dockCanvas;
        private readonly Canvas _modalCanvas;
        private readonly Canvas _toastCanvas;
        private readonly List<Window> _windows = new List<Window>();
        private readonly List<(GameObject go, float until)> _toasts = new List<(GameObject, float)>();
        private readonly Dictionary<string, (RectTransform rt, Text text)> _labels = new Dictionary<string, (RectTransform, Text)>();
        private readonly List<(RectTransform rt, Text text, Button button)> _queueLabels = new List<(RectTransform, Text, Button)>();
        private RectTransform _toastList;

        private Text _dateText, _cashText, _statsText, _placeText, _hintText, _cargoText, _chaseText, _shopGoText;
        private readonly List<(Button b, int speed)> _speedButtons = new List<(Button, int)>();
        private readonly List<(Button b, VehicleKind kind)> _vehicleButtons = new List<(Button, VehicleKind)>();
        private Button _pauseButton;
        private float _slowTimer;
        private float _uiTime;

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
            _dockCanvas = Ui.CreateCanvas("UI_Dock", 20);
            _modalCanvas = Ui.CreateCanvas("UI_Modal", 30);
            _toastCanvas = Ui.CreateCanvas("UI_Toast", 25);
            BuildHud();
            BuildToasts();
            BuildTip();
        }

        public IEnumerable<Canvas> Canvases => new[] { _labelCanvas, _hudCanvas, _dockCanvas, _modalCanvas, _toastCanvas, _tipCanvas };

        // ---------- 每帧 ----------

        public void Update(float dt)
        {
            if (S == null) return;
            _uiTime += dt;
            UpdateHud();
            UpdateLabels();
            UpdatePlayerMarker();
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
            h.spacing = 12;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childAlignment = TextAnchor.MiddleLeft;

            AddCheatButton(row);
            _dateText = Ui.Size(Ui.Label(row, "", 24), 270);
            _cashText = Ui.Size(Ui.Label(row, "", 26, TextAnchor.MiddleLeft, Ui.Good), 180);

            _pauseButton = Ui.Size(Ui.Button(row, "暂停", () => S.Time.TogglePause(), 18), 70);
            _pauseButton.Tip(() => S.Time.IsHeld
                ? "<b>已暂停</b>：开着面板或窗口时时间自动暂停，关掉后继续。\n（店里右侧的“柜台”页不暂停。）"
                : S.Time.IsUserPaused ? "<b>继续</b>　快捷键：空格" : "<b>暂停</b>　快捷键：空格");
            foreach (int sp in TimeController.Speeds)
            {
                int speed = sp;
                Button b = Ui.Size(Ui.Button(row, speed + "×", () => S.Time.SetSpeed(speed), 18), 54);
                int keyNo = System.Array.IndexOf(TimeController.Speeds, speed) + 1;
                b.Tip($"<b>{speed} 倍速</b>　快捷键：{keyNo}");
                _speedButtons.Add((b, speed));
            }

            _placeText = Ui.Size(Ui.Label(row, "", 20, TextAnchor.MiddleCenter), 260, -1, 1);
            _statsText = Ui.Size(Ui.Label(row, "", 18, TextAnchor.MiddleRight), 330);
            _statsText.Tip(StatsTip);
            _cashText.Tip("<b>现金</b>：所有进出都记在账上。现金连续 3 天为负就破产。");
            Ui.Size(Ui.Button(row, "我的", OpenMyPanel, 18, Ui.ButtonHot), 66).Tip("<b>我的</b>　快捷键：M\n身体状况、住处、背包里的吃的、家具背包；在家或店里可以从这里去摆家具。");
            Ui.Size(Ui.Button(row, "背包 B", ToggleBackpack, 18), 78).Tip("<b>背包</b>　快捷键：B\n身上带的货和负重；在家或店里一键全放下。");
            Ui.Size(Ui.Button(row, "日程", OpenSchedulePanel, 18), 66).Tip("<b>日程</b>　快捷键：C\n约会、师傅上门、网购快递、焖饭等安排（没有手机日历 app 时是小本子）。");
            Ui.Size(Ui.Button(row, "人脉", OpenRelationsPanel, 18), 66).Tip("<b>人脉</b>\n认识的人和你有多熟、他们彼此之间的关系（网状图）。");
            Ui.Size(Ui.Button(row, "手机", OpenPhone, 18), 66).Tip("<b>手机</b>　快捷键：P\n买了智能手机才有；通讯录、1788 批发、外卖等 app。");
            Ui.Size(Ui.Button(row, "任务", OpenQuests, 18), 66).Tip("<b>任务</b>　快捷键：T\n当前任务怎么做、完成奖励；左上角的任务条收起后从这里再打开。");
            _businessButton = Ui.Size(Ui.Button(row, "经营", OpenBusinessPanel, 18), 66);
            _businessButton.gameObject.SetActive(false);
            _businessButton.Tip("<b>经营</b>　快捷键：J\n资产与负债、直供、企业合同、分店、园区等后期经营。");
            Ui.Size(Ui.Button(row, "行情", OpenMarketPanel, 18, Ui.ButtonHot), 66).Tip("<b>行情</b>　快捷键：H\n新闻、宏观指标和各品类配件的价格走势，决定什么时候进货。");
            Ui.Size(Ui.Button(row, "菜单", OpenSystemPanel, 18), 66).Tip("<b>菜单</b>　快捷键：Esc\n存档、读档、目标与人脉、通知记录、音效。");

            Image bottom = Ui.Panel(root, "BottomBar", Ui.PanelBg);
            bottom.rectTransform.Anchor(0, 0, 0, 0, 0, 0, -1360, -58);
            RectTransform brow = Ui.Rect(bottom.transform, "Row").Fill();
            var bh = brow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bh.padding = new RectOffset(12, 12, 8, 8);
            bh.spacing = 8;
            bh.childControlWidth = true;
            bh.childControlHeight = true;
            bh.childForceExpandWidth = false;
            Ui.Size(Ui.Label(brow, "出行：", 18), 60);
            foreach (VehicleSpec v in Vehicles.All)
            {
                VehicleKind kind = v.Kind;
                Button b = Ui.Size(Ui.Button(brow, v.Name, () => S.SetVehicle(kind), 18), 92);
                b.Tip(() => VehicleTip(Vehicles.Get(kind)));
                _vehicleButtons.Add((b, kind));
            }
            _cargoText = Ui.Size(Ui.Label(brow, "", 17), 170);
            Ui.Size(Ui.Label(brow, "快捷前往：", 17), 90);
            Button shopGo = Ui.Size(Ui.Button(brow, "去我的店", GoToMyShop, 17, Ui.ButtonHot), 100);
            _shopGoText = shopGo.GetComponentInChildren<Text>();
            shopGo.Tip(() => S.MyShopCount > 1 ? "开了几家店：点了选去哪一家，自动走到店门口；路上按 WASD 随时接管。" : "自动走到店门口（耗时按路程算）；路上按 WASD 随时接管。");
            Ui.Size(Ui.Button(brow, "回家", () => _ctrl.GoToPlace(S.Home), 17), 70).Tip("自动走回家；路上按 WASD 随时接管。");
            Ui.Size(Ui.Button(brow, "电子市场", () => _ctrl.GoToPlace(S.City.GetPlace("stall_cpu")), 17), 100);

            Image hint = Ui.Panel(root, "HintStrip", new Color(0.12f, 0.18f, 0.17f, 0.93f));
            hint.rectTransform.Anchor(0, 0, 0, 0, 16, 70, -1136, -122);
            hint.raycastTarget = false;
            _hintText = Ui.Label(hint.transform, "", 16, TextAnchor.MiddleLeft, Ui.TextColor);
            _hintText.rectTransform.Fill(10);

            BuildQuestTracker(root);
            BuildSleepBanner(root);

            _chaseText = Ui.Label(root, "交警在追你！快跑或者躲进店里！", 34, TextAnchor.MiddleCenter, new Color(1f, 0.3f, 0.25f));
            _chaseText.rectTransform.Anchor(0.2f, 1, 0.8f, 1, 0, -150, 0, 80);
            _chaseText.gameObject.SetActive(false);
        }

        private Button _businessButton;

        /// <summary>编辑器里才有的作弊工具按钮（打包时整段不编译）。</summary>
        partial void AddCheatButton(Transform row);

        private void UpdateHud()
        {
            GameTimestamp now = S.Clock.Now;
            UpdateSleepBanner();
            bool business = BusinessUnlocked;
            if (_businessButton.gameObject.activeSelf != business) _businessButton.gameObject.SetActive(business);
            _dateText.text = $"{now.Date}  {Ui.WeekdayName(now.Date.Weekday)}  {Ui.Hm(now.MinuteOfDay)}";
            _cashText.text = S.Ledger.Balance.ToString();
            _cashText.color = S.Ledger.Balance.IsNegative ? Ui.Warn : Ui.Good;

            bool paused = !S.Time.IsRunning;
            _pauseButton.image.color = paused ? Ui.Warn : Ui.ButtonBg;
            _pauseButton.GetComponentInChildren<Text>().text = S.Time.IsHeld ? "已暂停" : (S.Time.IsUserPaused ? "继续" : "暂停");
            foreach (var (b, speed) in _speedButtons)
                b.image.color = !paused && S.Time.Speed == speed ? Ui.ButtonHot : Ui.ButtonBg;

            PlayerStats p = S.Player;
            _statsText.text = $"饥饿 {Pct(p.Hunger)}  精力 {Pct(p.Energy)}/{p.MaxEnergy:0}  健康 {Pct(p.Health)}  |  知名度 {S.PlayerShop.Reputation:0.0}";

            string where;
            if (S.Trip != null)
            {
                Place target = S.City.PlaceAtNode(S.Trip.TargetNode);
                where = $"{S.CurrentVehicle.Name}前往 {(target != null ? target.Name : "路口")}";
            }
            else if (S.CurrentPlace != null) where = "在 " + S.PlaceTitle(S.CurrentPlace);
            else where = "在街上";
            string shopName = S.MyShopCount > 1 ? S.ShopTitle(S.CurrentShop) : "店";
            string shopState = !S.HasShop ? "<color=#DDC18B>还没有店铺</color>" : S.PlayerShop.IsOpen ? $"<color=#A9C9A9>{shopName}：营业中</color>" : $"<color=#D79780>{shopName}：关门</color>";
            _shopGoText.text = S.HasShop ? "去我的店" : "房产中介";
            _placeText.text = where + "   " + shopState;

            if (S.HasTempHere && !S.IsGameOver && !AnyModalOpen && !_ctrl.Placing && (_ctrl.InHome || (_ctrl.InInterior && S.InPlayerShop)))
            {
                if (_ctrl.InHome) OpenStorageWindow();
                else OpenShopLockerWindow();
            }

            if (S.IsGameOver) _hintText.text = "游戏结束：" + S.GameOverReason;
            else if (_ctrl.Placing) _hintText.text = "摆放家具：左键放下 · R 旋转 · 右键/Esc 放回 · X 收进背包 · 点已摆的家具可以挪动 · 中键拖动 / Q E 转视角 · V 复位 · 右侧面板点“完成”结束";
            else if (_ctrl.InInterior) _hintText.text = "WASD 在店里走动 · 走到货架前按 F 查看货物 · 柜台边按 F（或点顾客头顶的“结账”）给顾客结账 · 走到门口按 F 出门 · 右侧面板管理店铺";
            else if (S.Trip != null) _hintText.text = $"快捷前往中，约 {S.Trip.EstimatedMinutes} 分钟 · 按 WASD 随时接管";
            else if (S.NearbyPlace != null)
                _hintText.text = S.ClosedReason(S.NearbyPlace) is string closed
                    ? $"<color=#D79780>{closed}</color>"
                    : $"<color=#DDC18B>按 F 进入 {S.PlaceTitle(S.NearbyPlace)}</color>";
            else if (_ctrl.InHome) _hintText.text = _ctrl.HomeRoom.NearbyId != null ? $"<color=#DDC18B>按 F：{_ctrl.HomeRoom.NearbyLabel}</color>" : "WASD 在屋里走动 · 走近床、衣柜、电饭锅、纸箱、凳子（头顶出现提示按 F）· 房间信息、摆放家具看右上角“我的”";
            else if (S.CurrentPlace != null) _hintText.text = "按 WASD 离开 · 空格暂停";
            else _hintText.text = "WASD 自由走动 · 右键拖动/Q E 转视角 · V 复位 · 滚轮缩放 · 走到门口按 F 进入 · 空格暂停 · 下方按钮可快捷前往";

            _chaseText.gameObject.SetActive(S.Police.AnyChasing && S.CurrentPlace == null);
        }

        private static string Pct(double v) => v.ToString("0");

        private void UpdateVehicleButtons()
        {
            foreach (var (b, kind) in _vehicleButtons)
            {
                bool can = S.CanUse(kind);
                b.interactable = can;
                b.image.color = S.Vehicle == kind ? Ui.ButtonHot : Ui.ButtonBg;
                Text t = b.GetComponentInChildren<Text>();
                t.color = can ? Ui.TextColor : Ui.Dim;
                string name = Vehicles.Get(kind).Name;
                if (kind == VehicleKind.Tricycle && S.TricycleImpounded) name = "三轮(扣)";
                t.text = name;
            }
            _cargoText.text = $"负重 {S.CarriedKg:0.#}/{S.CarryLimitKg:0.#} 公斤";
        }

        // ---------- 地点名牌 ----------

        private void UpdateLabels()
        {
            Camera cam = _ctrl.Cam;
            bool show = !_ctrl.InRoom;
            foreach (var kv in _ctrl.World.Places)
            {
                PlaceVisual pv = kv.Value;
                if (pv.Sign != null) pv.Sign.text = S.PlaceTitle(pv.Place);
                if (!_labels.TryGetValue(kv.Key, out var label))
                {
                    Place place = pv.Place;
                    bool important = place.Kind != PlaceKind.Shop || place.IsPlayerShop;
                    Button b = Ui.Button(_labelCanvas.transform, place.Name, () => OpenStreetPlace(place),
                        important ? 18 : 15, LabelColor(place, important));
                    RectTransform rt = (RectTransform)b.transform;
                    rt.anchorMin = rt.anchorMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0);
                    rt.sizeDelta = new Vector2(important ? 170 : 120, important ? 46 : 30);
                    label = (rt, b.GetComponentInChildren<Text>());
                    _labels[kv.Key] = label;
                }

                Vector3 sp = cam.WorldToScreenPoint(pv.LabelPos);
                bool visible = show && sp.z > 0 && sp.x > -100 && sp.x < cam.pixelWidth + 100 && sp.y > -50 && sp.y < cam.pixelHeight + 50;
                label.rt.gameObject.SetActive(visible);
                if (!visible) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_labelCanvas.transform, sp, LabelCam, out Vector2 local);
                var canvasRt = (RectTransform)_labelCanvas.transform;
                label.rt.anchoredPosition = local + canvasRt.rect.size * 0.5f;
                label.text.text = LabelText(pv.Place);
                bool owned = S.IsMyShop(pv.Place) || S.VentureAt(pv.Place.Id) != null || S.PropertyAt(pv.Place.Id) != null;
                bool major = owned || pv.Place.Kind != PlaceKind.Shop || pv.Place.IsPlayerShop;
                label.rt.sizeDelta = new Vector2(major ? 180 : 120, major ? 52 : 30);
                label.text.fontSize = major ? 18 : 15;
                label.rt.GetComponent<Image>().color = LabelColor(pv.Place, pv.Place.Kind != PlaceKind.Shop || pv.Place.IsPlayerShop);
            }
        }

        /// <summary>“去我的店”：只有一家就直接走过去；开了几家先选去哪家；还没店就去中介。</summary>
        private void GoToMyShop()
        {
            var shops = S.MyShops;
            if (shops.Count <= 1)
            {
                _ctrl.GoToPlace(S.HasShop ? S.PlayerShopPlace : S.City.FirstOfKind(PlaceKind.Agency));
                return;
            }
            OpenWindow("去哪家店", 460, 120 + shops.Count * 56, w =>
            {
                foreach (OwnedShop shop in shops)
                {
                    Place p = shop.Place;
                    string state = shop.Sim.IsOpen ? "营业中" : "关门";
                    string staff = shop.Employee != null || shop.Assembler != null ? "有人看店" : "没人看店";
                    Ui.Size(Ui.Button(w.Content, $"{S.ShopTitle(shop)}（{p.Name}）· {state} · {staff}", () =>
                    {
                        w.Close();
                        _ctrl.GoToPlace(p);
                    }, 17, shop == S.CurrentShop ? Ui.ButtonHot : Ui.ButtonBg), -1, 46);
                }
            });
        }

        /// <summary>名牌底色：我的店青色，招租的铺子橙色，中介蓝色，其他深灰。</summary>
        private void OpenStreetPlace(Place p)
        {
            if (S.VentureAt(p.Id) != null || S.PropertyAt(p.Id) != null && !S.IsMyShop(p))
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
            if (S.PropertyAt(p.Id) != null) return Ui.ButtonHot;
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
            Vector3 sp = cam.WorldToScreenPoint(StreetWorldBuilder.ToWorld(S.PlayerPosition, 2.6f));
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
            if (S.PropertyAt(p.Id) != null && !S.IsMyShop(p)) return "我的物业\n<size=13>托管出租中</size>";
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
                        _emoteLabels.Add(lt);
                    }
                    Text label = _emoteLabels[n];
                    label.text = e.Text;
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
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.04f, 0.08f, 0.07f, 0.55f);

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
        private string _lastToast;
        private float _lastToastAt;
        public void OpenNoticeHistory()
        {
            OpenWindow("通知记录（本次会话）", 850, 700, w =>
            {
                var list = Ui.Scroll(w.Content, 590);
                for (int i = _noticeHistory.Count - 1; i >= 0; i--) Para(list, _noticeHistory[i], 16);
                if (_noticeHistory.Count == 0) Line(list, "还没有通知。", 18, Ui.Dim);
            });
        }
        public void Toast(string message)
        {
            if (_lastToast == message && _uiTime - _lastToastAt < 8) return;
            _lastToast = message; _lastToastAt = _uiTime;
            _noticeHistory.Add((S == null ? "" : S.Clock.Now + "　") + message);
            if (_noticeHistory.Count > 150) _noticeHistory.RemoveAt(0);
            Image bg = Ui.Panel(_toastList, "Toast", new Color(0.12f, 0.17f, 0.16f, 0.96f));
            var fit = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            fit.padding = new RectOffset(14, 14, 10, 10);
            fit.childControlHeight = true;
            fit.childControlWidth = true;
            fit.childForceExpandHeight = false;
            Text t = Ui.Label(bg.transform, message, 16);
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

        public Window OpenWindow(string title, float width, float height, Action<Window> builder, bool holds = true, bool closable = true, Action onClosed = null)
        {
            var w = new Window { Builder = builder, Holds = holds, Owner = this, OnClosed = onClosed, Closable = closable };
            Image dim = Ui.Panel(_modalCanvas.transform, "Window_" + title, new Color(0.04f, 0.08f, 0.07f, 0.55f));
            dim.rectTransform.Fill();
            Image panel = Ui.Panel(dim.transform, "Panel", Ui.PanelBg);
            RectTransform prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            Vector2 available = ((RectTransform)_modalCanvas.transform).rect.size;
            float fit = Mathf.Min(1f, Mathf.Min((available.x - 32) / width, (available.y - 32) / height));
            prt.sizeDelta = new Vector2(width, height);
            prt.localScale = Vector3.one * Mathf.Max(0.25f, fit);
            prt.anchoredPosition = new Vector2(0, -10 * _windows.Count);

            Image bar = Ui.Panel(prt, "TitleBar", Ui.PanelBg2);
            bar.rectTransform.Anchor(0, 1, 1, 1, 0, -50, 0, 0);
            w.TitleText = Ui.Label(bar.transform, title, 24);
            w.TitleText.rectTransform.Anchor(0, 0, 1, 1, 24, 0, 112, 0);
            w.TitleText.fontStyle = FontStyle.Bold;
            Ui.Panel(prt, "BrassRule", Ui.Accent).rectTransform.Anchor(0, 1, 1, 1, 20, -51, 20, 49);
            if (closable)
            {
                Button close = Ui.Button(bar.transform, "关闭", w.Close, 18, Ui.Warn);
                ((RectTransform)close.transform).Anchor(1, 0, 1, 1, -96, 8, 10, 8);
            }

            w.Root = dim.rectTransform;
            w.Content = Ui.Scroll(prt);
            ((RectTransform)w.Content.parent.parent).Anchor(0, 0, 1, 1, 0, 0, 0, 50);
            var contentLayout = w.Content.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 8; contentLayout.padding = new RectOffset(16, 16, 16, 16);
            _windows.Add(w);
            if (holds) S?.Time.Hold();
            w.Rebuild();
            return w;
        }

        public void CloseWindow(Window w)
        {
            if (w.Closed) return;
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
            _popups.Add(OpenWindow(title, 640, 380, w =>
            {
                Text t = Ui.Label(w.Content, body, 21, TextAnchor.UpperLeft);
                Ui.Size(t, -1, 230);
                Ui.Size(Ui.Button(w.Content, "知道了", w.Close, 20, Ui.ButtonHot), -1, 48);
            }));
        }

        /// <summary>随机事件：必须选一个选项才能继续（不可关闭）。</summary>
        public void ShowEvent(EventDef def)
        {
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
            OpenWindow("欢迎来到电脑城", 680, 400, w =>
            {
                Para(w.Content, S.HasShop ? "照顾好自己，也照顾好小店。新的任务会随着经营进展出现。" : "你带着 ¥10,000 来到电脑城，想开一家自己的电脑店。先安顿下来，出门去「安居房产中介」找一间负担得起的铺子。", 20);
                Para(w.Content, "WASD 移动，沿街看招牌寻找店铺；靠近门或家具按 F 交互。", 18);
                Para(w.Content, "左上角只提示眼前的一件事；需要说明时点“怎么做”。", 17, Ui.Accent);
                Ui.Size(Ui.Button(w.Content, "开始经营", w.Close, 20, Ui.ButtonHot), -1, 50);
            });
        }
    }
}
