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
        public HomeRoomView HomeRoom { get; private set; }
        public bool InInterior => Session != null && Session.InPlayerShop;
        /// <summary>在自己的出租屋里（真实的小屋，不是街上弹窗）。</summary>
        public bool InHome => Session != null && Session.CurrentPlace != null && Session.CurrentPlace == Session.Home;
        /// <summary>在任意室内场景（店里或家里）：街道隐藏，镜头跟着老板。</summary>
        public bool InRoom => InInterior || InHome;
        private int RoomKind => InInterior ? 1 : InHome ? 2 : 0;

        private CameraRig _rig;
        private DayNightLighting _lighting;
        private AvatarView _avatar;
        private PoliceView _police;
        private CrowdView _crowd;
        private Transform _actors;
        private Transform _policeRoot;
        private Place _pendingEnter;
        private bool _wasChasing;
        private int _wasRoomKind;
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
            HomeRoom = new HomeRoomView(_actors);
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

            _wasRoomKind = RoomKind;
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
            if (Session.Trip == null && !Session.OffNode && Session.PlayerNodeId == p.NodeId)
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
            HandleMovement(dt);
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
            bool room = InRoom;
            float worldDt = Session.Time.IsRunning ? dt * Session.Time.Speed : 0f;
            Interior.SetActive(interior);
            HomeRoom.SetActive(InHome);
            World.Root.SetActive(!room);
            _avatar.Update(Session, dt);
            _police.Update(!room);
            _crowd.Update(worldDt, !room);
            if (interior) Interior.Update(Session, worldDt, dt);
            if (InHome) HomeRoom.Update(Session, dt, Session.Clock.MinuteOfDay);

            if (RoomKind != _wasRoomKind)
            {
                _wasRoomKind = RoomKind;
                SnapCamera();
            }
            else
            {
                _rig.Follow(FocusPoint(), dt);
            }
            _lighting.Apply(Session.Clock.MinuteOfDay, room);

            bool chasing = Session.Police.AnyChasing;
            if (chasing && !_wasChasing) Ui.Toast("被交警发现了！");
            _wasChasing = chasing;
        }

        private Vector3 FocusPoint(float zoom = -1f)
        {
            if (InHome)
            {
                Vector3 c = HomeRoom.Center + new Vector3(0, 0.8f, 0);
                return c + (HomeRoom.OwnerWorld - HomeRoom.Center) * 0.35f;
            }
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
            HandleCameraOrbit();
            if (Input.GetKeyDown(KeyCode.E) && !Ui.AnyModalOpen && !Session.IsGameOver)
            {
                if (InHome) HomeInteract(HomeRoom.NearbyId);
                else if (!InInterior) TryEnterNearby();
            }
        }

        // ---------- 镜头旋转 ----------

        private bool _orbiting;

        /// <summary>街上自由切换视角：右键拖动旋转/俯仰，Q / R 左右转，V 复位。</summary>
        private void HandleCameraOrbit()
        {
            if (InRoom || Ui.AnyModalOpen) { _orbiting = false; return; }
            if (Input.GetMouseButtonDown(1) && !GameUi.PointerOverUi) _orbiting = true;
            if (!Input.GetMouseButton(1)) _orbiting = false;
            if (_orbiting) _rig.Orbit(Input.GetAxis("Mouse X") * 4f, -Input.GetAxis("Mouse Y") * 3f);
            float k = (Input.GetKey(KeyCode.R) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            if (Mathf.Abs(k) > 0.01f) _rig.Orbit(k * 100f * Time.unscaledDeltaTime, 0f);
            if (Input.GetKeyDown(KeyCode.V)) _rig.ResetView();
        }

        // ---------- WASD 走路 ----------

        private bool _taxiHinted;

        /// <summary>WASD / 方向键。相对镜头方向走：W 是屏幕往上（镜头怎么转，方向跟着转）。街上在街道范围内自由走，店里在店内走。</summary>
        private void HandleMovement(float dt)
        {
            if (Ui.AnyModalOpen || Session.IsGameOver)
            {
                Session.SetSteer(0, 0);
                return;
            }
            float h = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float v = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
            {
                Session.SetSteer(0, 0);
                return;
            }

            Vector3 fwd = Cam.transform.forward;
            fwd.y = 0;
            fwd.Normalize();
            Vector3 right = Cam.transform.right;
            right.y = 0;
            right.Normalize();
            Vector3 dir = fwd * v + right * h;

            if (InInterior)
            {
                Session.SetSteer(0, 0);
                Interior.MoveOwner(dir, dt);
                return;
            }
            if (InHome)
            {
                Session.SetSteer(0, 0);
                HomeRoom.MoveOwner(dir, dt);
                return;
            }

            if (!Session.CanSteer)
            {
                if (!_taxiHinted)
                {
                    _taxiHinted = true;
                    Ui.Toast("打车由司机开：点下方“去…”按钮。想自己走，换成步行、小电驴或三轮车。");
                }
                Session.SetSteer(0, 0);
                return;
            }

            if (Session.CurrentPlace != null)
            {
                Ui.CloseDock();
                Session.Leave();
            }
            _pendingEnter = null;
            Session.SetSteer(dir.x, dir.z);
        }

        /// <summary>出租屋里的互动：床 = 睡觉，衣柜 = 换衣服，桌子 = 看状态，门 = 出门。</summary>
        public void HomeInteract(string id)
        {
            switch (id)
            {
                case HomeRoomView.Bed:
                    Ui.OpenBedWindow();
                    break;
                case HomeRoomView.Wardrobe:
                    Ui.OpenWardrobeWindow();
                    break;
                case HomeRoomView.Table:
                    Ui.OpenStatusWindow();
                    break;
                case HomeRoomView.Door:
                    LeaveHome();
                    break;
                default:
                    Ui.Toast("走到床、衣柜、小桌或门边，按 E。");
                    break;
            }
        }

        public void LeaveHome()
        {
            Ui.CloseAllWindows();
            Session.Leave();
        }

        private void TryEnterNearby()
        {
            Place p = Session.NearbyPlace;
            if (p == null)
            {
                Ui.Toast("附近没有能进的地方，走到门口再按 E。");
                return;
            }
            Session.SetSteer(0, 0);
            if (Session.Trip != null) Session.StopTrip();
            if (Session.EnterNearby()) Ui.OnEnteredPlace(p);
        }

        private void HandleClicks()
        {
            if (!Input.GetMouseButtonDown(0) || GameUi.PointerOverUi) return;
            Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

            if (InHome)
            {
                var hf = hit.collider.GetComponentInParent<HomeFurniture>();
                if (hf != null)
                {
                    if (HomeRoom.NearbyId == hf.Id) HomeInteract(hf.Id);
                    else Ui.Toast("再走近一点。");
                }
                return;
            }

            if (InInterior)
            {
                var cm = hit.collider.GetComponentInParent<CustomerMarker>();
                if (cm != null) Checkout(cm.CustomerId);
                else if (hit.collider.GetComponent<InteriorFloorMarker>() != null) Interior.WalkTo(hit.point);
                return;
            }

            // 街上不再点击移动：WASD 走路，E 进门。
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
        public void SnapCamera()
        {
            bool interior = InInterior;
            bool home = InHome;
            _rig.SetInterior(interior || home);
            _rig.MinSize = home ? HomeRoomView.MinZoom : interior ? ShopInteriorView.MinZoom : 5f;
            _rig.MaxSize = home ? HomeRoomView.CameraSize : interior ? Interior.CameraSize : 60f;
            float size = home ? HomeRoomView.DefaultZoom : interior ? Mathf.Min(ShopInteriorView.DefaultZoom, Interior.CameraSize) : 16f;
            _wasRoomKind = RoomKind;
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
