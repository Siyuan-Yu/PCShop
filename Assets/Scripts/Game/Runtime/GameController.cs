using System;
using PCShopSell.Core.Common;
using PCShopSell.Core.Session;
using PCShopSell.Core.Time;
using PCShopSell.Core.World;
using PCShopSell.Game.Runtime;
using PCShopSell.Game.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PCShopSell.Game
{
    /// <summary>
    /// 游戏入口：读城市布局 → 生成街区 → 开一局（或读档）→ 每帧推进会话、同步画面、处理点击。
    /// 场景里只需要这一个组件（相机、灯光、后处理没有的话会自己建）。
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public const ulong DefaultSeed = 20261028;
        public static readonly Money StartingCash = Money.FromYuan(5000);

        public GameSession Session { get; private set; }
        public CityLayout City { get; private set; }
        public StreetWorldBuilder World { get; private set; }
        public ShopInteriorView Interior { get; private set; }
        public Camera Cam { get; private set; }
        public GameUi Ui { get; private set; }
        public bool InInterior => Session != null && Session.InPlayerShop;

        private CameraRig _rig;
        private DayNightLighting _lighting;
        private AvatarView _avatar;
        private PoliceView _police;
        private CrowdView _crowd;
        private Transform _actors;
        private Transform _policeRoot;
        private Place _pendingEnter;
        private bool _wasChasing;
        private bool _wasInterior;
        private bool _autosaveDue;
        private bool _bootstrapped;
        private bool _eventsOn;
        private bool _gameOverShown;

        private void Start()
        {
            Bootstrap(true);
        }

        /// <summary>搭好整个游戏。showMenus 为假时不弹开局窗口（自动化测试用）。</summary>
        public void Bootstrap(bool showMenus)
        {
            if (_bootstrapped) return;
            _bootstrapped = true;
            _eventsOn = showMenus;
            Application.targetFrameRate = 60;

            var json = Resources.Load<TextAsset>("Data/city_layout");
            City = CityLayout.FromData(JsonUtility.FromJson<CityLayoutData>(json.text));

            SetupRendering();
            World = new StreetWorldBuilder(City, 7);
            World.Build();
            _lighting = new DayNightLighting(Cam, FindSun(), FindVolume(), World.NightLights);

            _actors = new GameObject("Actors").transform;
            _avatar = new AvatarView(_actors);
            Interior = new ShopInteriorView(_actors, City.PlayerShop().Size);
            Ui = new GameUi(this);

            StartSession(NewSession());
            if (showMenus)
            {
                if (SaveStore.Exists) Ui.ShowStartChoice();
                else Ui.ShowIntro();
            }
        }

        private GameSession NewSession() =>
            new GameSession(City, DefaultSeed + (ulong)DateTime.Now.Ticks % 100000, StartingCash,
                GameTimestamp.From(new GameDate(0), GameSession.WakeUpMinute));

        private void StartSession(GameSession s)
        {
            Ui?.CloseAllWindows();
            if (Session != null)
            {
                Session.Notice -= OnNotice;
                Session.Popup -= OnPopup;
                Session.EventRaised -= OnEventRaised;
                Session.ShopExpanded -= OnShopExpanded;
                Session.TripArrived -= OnTripArrived;
                Session.PlayerShop.CustomerArrived -= OnCustomerArrived;
                Session.Clock.DayAdvanced -= OnDayAdvanced;
                Session.Dispose();
            }
            Interior.Clear();
            EnsureInterior(s.PlayerShop.Size);

            Session = s;
            s.Notice += OnNotice;
            s.Popup += OnPopup;
            s.EventRaised += OnEventRaised;
            s.ShopExpanded += OnShopExpanded;
            s.EventsEnabled = _eventsOn;
            s.TripArrived += OnTripArrived;
            s.PlayerShop.CustomerArrived += OnCustomerArrived;
            s.Clock.DayAdvanced += OnDayAdvanced;
            _pendingEnter = null;
            _gameOverShown = false;

            if (_policeRoot != null) Kill.It(_policeRoot.gameObject);
            _policeRoot = new GameObject("Police").transform;
            _policeRoot.SetParent(_actors, false);
            _police = new PoliceView(_policeRoot, s.Police);

            if (_crowd == null)
            {
                Vector3 door = World.Places[s.PlayerShopPlace.Id].Entrance + new Vector3(0, 0, StreetWorldBuilder.FrontZ - 0.3f);
                _crowd = new CrowdView(_actors, -95, 100, door, 34);
            }

            _wasInterior = InInterior;
            SnapCamera();
            if (s.CurrentPlace != null && !s.IsGameOver) Ui.OnEnteredPlace(s.CurrentPlace);
        }

        // ---------- 对 UI 暴露的操作 ----------

        public void NewGame()
        {
            StartSession(NewSession());
            Ui.ShowIntro();
        }

        public bool SaveGame()
        {
            if (!Session.CanSave) return false;
            SaveStore.Save(Session);
            return true;
        }

        public void LoadGame()
        {
            GameSession s = SaveStore.Load(City);
            if (s == null)
            {
                Ui.Toast("没有存档。");
                return;
            }
            StartSession(s);
            Ui.Toast("读档成功。");
        }

        /// <summary>前往某地并在到达后进入。已经在门口就直接进去。</summary>
        public void GoToPlace(Place p)
        {
            if (Session.IsGameOver || Ui.AnyModalOpen) return;
            if (Session.CurrentPlace == p) return;
            if (Session.CurrentPlace != null)
            {
                Ui.CloseDock();
                Session.Leave();
            }
            if (Session.Trip == null && Session.PlayerNodeId == p.NodeId)
            {
                EnterPlace(p);
                return;
            }
            if (Session.GoTo(p.NodeId)) _pendingEnter = p;
        }

        public void Checkout(int customerId)
        {
            if (Session.Checkout(customerId))
            {
                Ui.RebuildDock();
                if (InInterior) Interior.WalkToCounter();
            }
        }

        private void EnterPlace(Place p)
        {
            _pendingEnter = null;
            if (Session.Enter(p)) Ui.OnEnteredPlace(p);
        }

        // ---------- 每帧 ----------

        private void Update()
        {
            if (!_bootstrapped || Session == null) return;
            float dt = Time.unscaledDeltaTime;
            HandleKeys();
            if (!Ui.AnyModalOpen) HandleClicks();

            Session.Tick(dt);
            if (_autosaveDue && Session.CanSave && !Session.IsGameOver)
            {
                _autosaveDue = false;
                SaveStore.Save(Session);
                Ui.Toast("已自动存档。");
            }
            if (Session.IsGameOver && !_gameOverShown)
            {
                _gameOverShown = true;
                Ui.CloseDock();
                Ui.ShowGameOver();
            }
            SyncViews(dt);
            Ui.Update(dt);
        }

        /// <summary>不依赖 Update 的推进（自动化测试用）。</summary>
        public void Step(float dt)
        {
            Session.Tick(dt);
            SyncViews(dt);
            Ui.Update(dt);
        }

        private void SyncViews(float dt)
        {
            bool interior = InInterior;
            float worldDt = Session.Time.IsRunning ? dt * Session.Time.Speed : 0f;
            Interior.SetActive(interior);
            World.Root.SetActive(!interior);
            _avatar.Update(Session, dt);
            _police.Update(!interior);
            _crowd.Update(worldDt, !interior);
            if (interior) Interior.Update(Session, worldDt, dt);

            if (interior != _wasInterior)
            {
                _wasInterior = interior;
                SnapCamera();
            }
            else
            {
                _rig.Follow(FocusPoint(), dt);
            }
            _lighting.Apply(Session.Clock.MinuteOfDay, interior);

            bool chasing = Session.Police.AnyChasing;
            if (chasing && !_wasChasing) Ui.Toast("被交警发现了！");
            _wasChasing = chasing;
        }

        private Vector3 FocusPoint(float zoom = -1f)
        {
            if (InInterior)
                return Interior.OwnerWorld + new Vector3(0, 0.8f, 0) + CameraRig.Rotation * Vector3.right * ((zoom > 0 ? zoom : Cam.orthographicSize) * Cam.aspect * 0.28f);
            Vector3 p = StreetWorldBuilder.ToWorld(Session.PlayerPosition);
            if (Session.CurrentPlace != null && World.Places.TryGetValue(Session.CurrentPlace.Id, out PlaceVisual pv)) p = pv.Entrance;
            return p + new Vector3(0, 1f, 0);
        }

        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Space)) Session.Time.TogglePause();
            if (Input.GetKeyDown(KeyCode.Alpha1)) Session.Time.SetSpeed(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Session.Time.SetSpeed(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Session.Time.SetSpeed(4);
            if (Input.GetKeyDown(KeyCode.Alpha4)) Session.Time.SetSpeed(8);
            if (!GameUi.PointerOverUi) _rig.Zoom(Input.mouseScrollDelta.y);
        }

        private void HandleClicks()
        {
            if (!Input.GetMouseButtonDown(0) || GameUi.PointerOverUi) return;
            Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

            if (InInterior)
            {
                var cm = hit.collider.GetComponentInParent<CustomerMarker>();
                if (cm != null) Checkout(cm.CustomerId);
                else if (hit.collider.GetComponent<InteriorFloorMarker>() != null) Interior.WalkTo(hit.point);
                return;
            }

            var pm = hit.collider.GetComponent<PlaceMarker>();
            if (pm != null)
            {
                GoToPlace(City.GetPlace(pm.PlaceId));
                return;
            }
            if (hit.collider.GetComponent<GroundMarker>() != null)
            {
                if (Session.CurrentPlace != null)
                {
                    Ui.CloseDock();
                    Session.Leave();
                }
                string node = City.NearestNode(hit.point.x, hit.point.z);
                _pendingEnter = null;
                Session.GoTo(node);
            }
        }

        // ---------- 会话事件 ----------

        private void OnNotice(string m) => Ui.Toast(m);

        private void OnEventRaised(EventDef def) => Ui.ShowEvent(def);

        /// <summary>店内场景按店面大小重建（扩建、读档时用）。</summary>
        private void EnsureInterior(PCShopSell.Core.Shop.ShopSize size)
        {
            if (Interior != null && Interior.Size == size) return;
            Interior?.Destroy();
            Interior = new ShopInteriorView(_actors, size);
        }

        private void OnShopExpanded(PCShopSell.Core.Shop.ShopSize size)
        {
            EnsureInterior(size);
            SnapCamera();
        }

        /// <summary>切换场景时重设镜头：店内比较近、跟着老板，街上拉远。</summary>
        private void SnapCamera()
        {
            bool interior = InInterior;
            _rig.MinSize = interior ? ShopInteriorView.MinZoom : 5f;
            _rig.MaxSize = interior ? Interior.CameraSize : 60f;
            float size = interior ? Mathf.Min(ShopInteriorView.DefaultZoom, Interior.CameraSize) : 16f;
            _wasInterior = interior;
            _rig.Snap(FocusPoint(size), size);
        }

        private void OnPopup(string title, string body)
        {
            if (title == "你晕倒了" || title == "住院了")
            {
                Ui.CloseAllWindows();
                _pendingEnter = null;
                if (Session.PendingEvent != null) Ui.ShowEvent(Session.PendingEvent);
                Ui.Popup(title, body);
                if (Session.CurrentPlace != null) Ui.OnEnteredPlace(Session.CurrentPlace);
                return;
            }
            Ui.Popup(title, body);
        }

        private void OnTripArrived(string nodeId)
        {
            if (_pendingEnter != null && _pendingEnter.NodeId == nodeId) EnterPlace(_pendingEnter);
            _pendingEnter = null;
        }

        private void OnCustomerArrived(PCShopSell.Core.Shop.ShopCustomer c)
        {
            if (!InInterior) _crowd?.SendShopper();
        }

        private void OnDayAdvanced(GameDate prev, GameDate today) => _autosaveDue = true;

        // ---------- 渲染 ----------

        private void SetupRendering()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                Cam = go.AddComponent<Camera>();
            }
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.allowHDR = true;
            Cam.allowMSAA = true;
            var extra = Cam.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = true;
            extra.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            _rig = new CameraRig(Cam);
        }

        private static Light FindSun()
        {
            foreach (Light l in FindObjectsOfType<Light>())
            {
                if (l.type == LightType.Directional) return l;
            }
            var go = new GameObject("Sun");
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            return sun;
        }

        private static Volume FindVolume()
        {
            Volume v = FindObjectOfType<Volume>();
            if (v != null) return v;
            var go = new GameObject("PostVolume");
            v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 1;
            return v;
        }
    }
}
