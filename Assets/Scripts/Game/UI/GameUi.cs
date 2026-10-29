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
        internal GameUi Owner;

        public void Rebuild()
        {
            if (Closed) return;
            Ui.Clear(Content);
            Builder?.Invoke(this);
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

        private Text _dateText, _cashText, _statsText, _placeText, _hintText, _cargoText, _chaseText;
        private readonly List<(Button b, int speed)> _speedButtons = new List<(Button, int)>();
        private readonly List<(Button b, VehicleKind kind)> _vehicleButtons = new List<(Button, VehicleKind)>();
        private Button _pauseButton;
        private float _slowTimer;
        private float _uiTime;

        private GameSession S => _ctrl.Session;

        public bool AnyModalOpen => _windows.Count > 0;

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
            _toastCanvas = Ui.CreateCanvas("UI_Toast", 40);
            BuildHud();
            BuildToasts();
        }

        public IEnumerable<Canvas> Canvases => new[] { _labelCanvas, _hudCanvas, _dockCanvas, _modalCanvas, _toastCanvas };

        // ---------- 每帧 ----------

        public void Update(float dt)
        {
            if (S == null) return;
            _uiTime += dt;
            UpdateHud();
            UpdateLabels();
            UpdatePlayerMarker();
            UpdateQueueLabels();
            UpdateToasts();
            _slowTimer -= dt;
            if (_slowTimer <= 0)
            {
                _slowTimer = 0.4f;
                UpdateVehicleButtons();
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

            _dateText = Ui.Size(Ui.Label(row, "", 24), 330);
            _cashText = Ui.Size(Ui.Label(row, "", 26, TextAnchor.MiddleLeft, Ui.Good), 190);

            _pauseButton = Ui.Size(Ui.Button(row, "暂停", () => S.Time.TogglePause(), 18), 70);
            foreach (int sp in TimeController.Speeds)
            {
                int speed = sp;
                Button b = Ui.Size(Ui.Button(row, speed + "×", () => S.Time.SetSpeed(speed), 18), 54);
                _speedButtons.Add((b, speed));
            }

            _placeText = Ui.Size(Ui.Label(row, "", 20, TextAnchor.MiddleCenter), 250, -1, 1);
            _statsText = Ui.Size(Ui.Label(row, "", 18, TextAnchor.MiddleRight), 440);
            Ui.Size(Ui.Button(row, "目标", OpenGoalsPanel, 18), 80);
            Ui.Size(Ui.Button(row, "行情", OpenMarketPanel, 18, Ui.ButtonHot), 80);
            Ui.Size(Ui.Button(row, "菜单", OpenSystemPanel, 18), 80);

            Image bottom = Ui.Panel(root, "BottomBar", Ui.PanelBg);
            bottom.rectTransform.Anchor(0, 0, 0, 0, 0, 0, -1180, -58);
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
                _vehicleButtons.Add((b, kind));
            }
            _cargoText = Ui.Size(Ui.Label(brow, "", 17), 170);
            Ui.Size(Ui.Label(brow, "快捷前往：", 17), 90);
            Ui.Size(Ui.Button(brow, "去我的店", () => _ctrl.GoToPlace(S.PlayerShopPlace), 17, Ui.ButtonHot), 100);
            Ui.Size(Ui.Button(brow, "回家", () => _ctrl.GoToPlace(S.Home), 17), 70);
            Ui.Size(Ui.Button(brow, "电子市场", () => _ctrl.GoToPlace(S.City.GetPlace("stall_cpu")), 17), 100);

            _hintText = Ui.Label(root, "", 18, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.85f));
            _hintText.rectTransform.Anchor(0, 0, 0, 0, 16, 62, -1100, -92);

            _chaseText = Ui.Label(root, "交警在追你！快跑或者躲进店里！", 34, TextAnchor.MiddleCenter, new Color(1f, 0.3f, 0.25f));
            _chaseText.rectTransform.Anchor(0.2f, 1, 0.8f, 1, 0, -150, 0, 80);
            _chaseText.gameObject.SetActive(false);
        }

        private void UpdateHud()
        {
            GameTimestamp now = S.Clock.Now;
            _dateText.text = $"{now.Date}  {Ui.WeekdayName(now.Date.Weekday)}  {Ui.Hm(now.MinuteOfDay)}";
            _cashText.text = S.Ledger.Balance.ToString();
            _cashText.color = S.Ledger.Balance.IsNegative ? Ui.Warn : Ui.Good;

            bool paused = S.Time.IsUserPaused;
            _pauseButton.image.color = paused ? Ui.Warn : Ui.ButtonBg;
            _pauseButton.GetComponentInChildren<Text>().text = paused ? "继续" : "暂停";
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
            else if (S.CurrentPlace != null) where = "在 " + S.CurrentPlace.Name;
            else where = "在街上";
            string shopState = S.PlayerShop.IsOpen ? "<color=#7CE38B>店：营业中</color>" : "<color=#E0806A>店：关门</color>";
            _placeText.text = where + "   " + shopState;

            if (S.IsGameOver) _hintText.text = "游戏结束：" + S.GameOverReason;
            else if (_ctrl.InInterior) _hintText.text = "WASD 在店里走动，点地板也行 · 点击头顶有黄球的顾客结账 · 右侧面板管理店铺";
            else if (S.Trip != null) _hintText.text = $"快捷前往中，约 {S.Trip.EstimatedMinutes} 分钟 · 按 WASD 随时接管";
            else if (S.NearbyPlace != null) _hintText.text = $"<color=#FFD54A>按 E 进入 {S.NearbyPlace.Name}</color>";
            else if (_ctrl.InHome) _hintText.text = _ctrl.HomeRoom.NearbyId != null ? $"<color=#FFD54A>按 E：{_ctrl.HomeRoom.NearbyLabel}</color>" : "WASD 在屋里走动 · 走到床边睡觉、衣柜换衣服、小桌看状态、门口出门（按 E）";
            else if (S.CurrentPlace != null) _hintText.text = "按 WASD 离开 · 空格暂停";
            else _hintText.text = "WASD 自由走动 · 右键拖动/Q R 转视角 · V 复位 · 滚轮缩放 · 走到门口按 E 进入 · 空格暂停 · 下方按钮可快捷前往";

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
            _cargoText.text = $"随身货 {S.Cargo.UsedSpace:0.#}/{S.Cargo.Capacity:0} 格";
        }

        // ---------- 地点名牌 ----------

        private void UpdateLabels()
        {
            Camera cam = _ctrl.Cam;
            bool show = !_ctrl.InRoom;
            foreach (var kv in _ctrl.World.Places)
            {
                PlaceVisual pv = kv.Value;
                if (!_labels.TryGetValue(kv.Key, out var label))
                {
                    Place place = pv.Place;
                    bool important = place.Kind != PlaceKind.Shop || place.IsPlayerShop;
                    Button b = Ui.Button(_labelCanvas.transform, place.Name, () => Toast("用 WASD 走到门口，按 E 进入；也可以点下方的快捷前往按钮。"),
                        important ? 18 : 15, place.IsPlayerShop ? new Color(0.1f, 0.55f, 0.5f, 0.92f) : new Color(0.1f, 0.12f, 0.16f, important ? 0.85f : 0.6f));
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
            }
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
            switch (p.Kind)
            {
                case PlaceKind.Wholesaler:
                    return p.Name + (S.IsWholesalerOpen ? "" : "\n<size=13><color=#E0806A>关门</color></size>");
                case PlaceKind.Dealer:
                    return p.Name + (S.IsDealerOpen ? "" : "\n<size=13><color=#E0806A>关门</color></size>");
                case PlaceKind.Shop when p.IsPlayerShop:
                    return p.Name + (S.PlayerShop.IsOpen ? "\n<size=13><color=#7CE38B>营业中</color></size>" : "\n<size=13>关门</size>");
                default:
                    return p.Name;
            }
        }

        private void UpdateQueueLabels()
        {
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
                    text.text = c != null ? $"结账 {c.Spend}\n<size=12>{c.Item?.Name}</size>" : "结账";
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
            _toastList = Ui.Rect(_toastCanvas.transform, "Toasts").Anchor(0, 1, 0, 1, 16, -520, -476, 76);
            var v = _toastList.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperLeft;
        }

        public void Toast(string message)
        {
            Image bg = Ui.Panel(_toastList, "Toast", new Color(0.05f, 0.06f, 0.08f, 0.8f));
            var fit = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            fit.padding = new RectOffset(10, 10, 4, 4);
            fit.childControlHeight = true;
            fit.childControlWidth = true;
            fit.childForceExpandHeight = false;
            Text t = Ui.Label(bg.transform, message, 16);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            _toasts.Add((bg.gameObject, _uiTime + 5f));
            if (_toasts.Count > 5)
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
            var w = new Window { Builder = builder, Holds = holds, Owner = this, OnClosed = onClosed };
            Image dim = Ui.Panel(_modalCanvas.transform, "Window_" + title, new Color(0, 0, 0, 0.35f));
            dim.rectTransform.Fill();
            Image panel = Ui.Panel(dim.transform, "Panel", Ui.PanelBg);
            RectTransform prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(width, height);
            prt.anchoredPosition = new Vector2(0, -10 * _windows.Count);

            Image bar = Ui.Panel(prt, "TitleBar", Ui.PanelBg2);
            bar.rectTransform.Anchor(0, 1, 1, 1, 0, -50, 0, 0);
            w.TitleText = Ui.Label(bar.transform, title, 24);
            w.TitleText.rectTransform.Fill(16);
            if (closable)
            {
                Button close = Ui.Button(bar.transform, "关闭", w.Close, 18, Ui.Warn);
                ((RectTransform)close.transform).Anchor(1, 0, 1, 1, -96, 8, 10, 8);
            }

            w.Root = dim.rectTransform;
            w.Content = Ui.Rect(prt, "Content").Anchor(0, 0, 1, 1, 0, 0, 0, 50);
            Ui.VList(w.Content, 8, 16);
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

        public void Popup(string title, string body)
        {
            OpenWindow(title, 640, 380, w =>
            {
                Text t = Ui.Label(w.Content, body, 21, TextAnchor.UpperLeft);
                Ui.Size(t, -1, 230);
                Ui.Size(Ui.Button(w.Content, "知道了", w.Close, 20, Ui.ButtonHot), -1, 48);
            });
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
                    string label = ok ? c.Text : $"{c.Text}\n<size=14><color=#E0806A>{c.DisabledReason}</color></size>";
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
            Popup("欢迎来到电脑城", "你在商业街最东头租了一间最小、离地铁最远的电脑店（48㎡），手里只有 ¥5,000。\n\n" +
                                "· 去 2 公里外的电子市场档口进货，或者在店里电脑上用 1788 网购（便宜但要等，可能有次品）\n" +
                                "· 你在店里才营业：顾客挑好货会排队，点他们结账\n" +
                                "· 有人想装整机会下订单，配件齐了在店里装机交付\n" +
                                "· 第一个月目标：第 30 天时身家 ≥ ¥8,000，并且雇到一个员工");
        }
    }
}
